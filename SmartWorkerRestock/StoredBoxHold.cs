using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SmartWorkerRestock
{
    /// <summary>
    /// Boxes stay in the warehouse shelf until the worker picks them up. When a worker goes for a box stored on a warehouse
    /// shelf, the game turns the stored record back into a box object right away (RestockManager.MaterializeStoredCandidate),
    /// with physics on, while the worker is still walking there. Where shelves stand side by side, the new box overlaps the
    /// next shelf and gets pushed out onto the floor. We hold that box still (kinematic) where it was created, and watch it:
    ///  - the worker picks it up: physics comes back as usual (the game handles a held box);
    ///  - no worker goes for it any more (he changed his mind): it goes back into the shelf as a stored box, as if it had
    ///    never been taken out, so the player can use it normally; if the shelf has no room left, it gets its physics back.
    /// </summary>
    internal static class StoredBoxHold
    {
        private static readonly Dictionary<InteractablePackagingBox_Item, int> Held = new Dictionary<InteractablePackagingBox_Item, int>();
        private static float _next;

        public static void Patch(Harmony h)
        {
            var m = AccessTools.Method(typeof(RestockManager), nameof(RestockManager.MaterializeStoredCandidate));
            if (m == null) { Plugin.Log.LogWarning("Stored box creation not found (different game version?): boxes taken from shelves are not held."); return; }
            h.Patch(m, postfix: new HarmonyMethod(typeof(StoredBoxHold), nameof(Postfix)));
        }

        private static void Postfix(InteractablePackagingBox_Item __result)
        {
            try
            {
                if (Plugin.Blocked || !Plugin.Instance.HoldTakenBoxes.Value || __result == null || !__result.m_IsStored) return;
                var rb = __result.m_Rigidbody;
                if (rb == null || rb.isKinematic) return;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                Held[__result] = 0;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Stored box hold skipped: " + e.Message); }
        }

        private static readonly System.Reflection.FieldInfo DataOnlyField = AccessTools.Field(typeof(InteractablePackagingBox_Item), "m_MarkForDataOnlyDestroy");
        private static readonly System.Reflection.FieldInfo LerpingField = AccessTools.Field(typeof(InteractableObject), "m_IsLerpingToPos");
        private static float _nextFind;

        /// <summary>
        /// Boxes held still that we no longer track (the mod was reloaded, or a save was loaded while one was held): a box
        /// taken out of a shelf, still marked as stored, kinematic, and not on its way into a shelf (that one is about to
        /// become data again). Found every 5 s and watched like the others.
        /// </summary>
        private static void FindUntracked()
        {
            var rm = Game.Get<RestockManager>();
            if (rm == null || rm.m_ItemPackagingBoxList == null || DataOnlyField == null || LerpingField == null) return;
            foreach (var b in rm.m_ItemPackagingBoxList)
            {
                if (b == null || !b.m_IsStored || Held.ContainsKey(b) || b.m_Rigidbody == null || !b.m_Rigidbody.isKinematic) continue;
                if ((bool)DataOnlyField.GetValue(b) || (bool)LerpingField.GetValue(b) || BeingHeld(b) || b.GetBoxStoredCompartment() == null) continue;
                Held[b] = 0;
            }
        }

        /// <summary>Once a second, from Plugin.Update.</summary>
        public static void Tick()
        {
            if (Plugin.Instance.HoldTakenBoxes.Value && Time.unscaledTime >= _nextFind && Game.InShop())
            {
                _nextFind = Time.unscaledTime + 5f;
                try { FindUntracked(); } catch (Exception e) { Plugin.Log.LogWarning("Stored box hold scan: " + e.Message); }
            }
            if (Held.Count == 0 || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            var done = new List<InteractablePackagingBox_Item>();
            var abandoned = new List<InteractablePackagingBox_Item>();
            foreach (var box in new List<InteractablePackagingBox_Item>(Held.Keys))
            {
                if (box == null || !box.IsValidObject() || BeingHeld(box) || !box.m_IsStored) { done.Add(box); continue; } // picked up or gone
                if (Wanted(box)) { Held[box] = 0; continue; }
                if (++Held[box] >= 3) abandoned.Add(box); // nobody has wanted it for 3 s
            }
            foreach (var b in done) Held.Remove(b);
            foreach (var box in abandoned)
            {
                Held.Remove(box);
                try
                {
                    if (CanGoBack(box) && BoxPreference.PutBack(box)) Plugin.Log.LogInfo($"A worker left a {box.m_ItemCompartment.GetItemType()} box he had taken out of a shelf: it is stored there again.");
                    else Drop(box);
                }
                catch (Exception e) { Plugin.Log.LogWarning("Stored box hold release: " + e.Message); }
            }
        }

        private static readonly System.Reflection.FieldInfo BeingHoldField = AccessTools.Field(typeof(InteractableObject), "m_IsBeingHold");
        private static bool BeingHeld(InteractablePackagingBox_Item box) => BeingHoldField != null && (bool)BeingHoldField.GetValue(box);

        /// <summary>
        /// No room left in its shelf: the box is set down in front of the shelf (where a worker stands to reach it), as a
        /// normal loose box, instead of getting its physics back inside the shelf and being pushed out at random.
        /// </summary>
        private static void Drop(InteractablePackagingBox_Item box)
        {
            var c = box.GetBoxStoredCompartment();
            if (c != null && c.m_CustomerStandLoc != null)
                box.transform.position = c.m_CustomerStandLoc.position + Vector3.up * 0.4f;
            box.m_IsStored = false;
            box.SetBoxStoredCompartment(null);
            if (box.m_Rigidbody != null) box.m_Rigidbody.isKinematic = false;
            Plugin.Log.LogInfo($"A worker left a {box.m_ItemCompartment.GetItemType()} box he had taken out of a full shelf: it is set down in front of it.");
        }

        private static bool Wanted(InteractablePackagingBox_Item box)
        {
            var list = WorkerManager.GetWorkerList();
            if (list == null) return false;
            foreach (var w in list)
                if (w != null && w.IsActive() && w.GetCurrentItemBox() == box) return true;
            return false;
        }

        /// <summary>Its shelf still takes it: same item and size as what is stored there, and a free slot.</summary>
        private static bool CanGoBack(InteractablePackagingBox_Item box)
        {
            var c = box.GetBoxStoredCompartment();
            if (c == null || box.m_ItemCompartment == null) return false;
            int n = c.GetStoredBoxRecordCount();
            if (n > 0)
            {
                var first = c.PeekStoredBoxRecord(0);
                if (first.itemType != box.m_ItemCompartment.GetItemType() || first.isBigBox != box.m_IsBigBox) return false;
            }
            return c.GetSlotTransform(n) != null;
        }

        public static void Clear() => Held.Clear();
    }
}
