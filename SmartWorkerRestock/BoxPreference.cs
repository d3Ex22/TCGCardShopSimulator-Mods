using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SmartWorkerRestock
{
    /// <summary>
    /// Opened boxes first. The game (and the other worker mods that hook this search) picks the box a worker will restock
    /// from, preferring boxes on the floor: new deliveries got opened while half-used boxes waited on warehouse shelves.
    /// After that choice (postfix, last), if the chosen box is a FULL box lying on the floor and an opened box of the same
    /// item and size exists (on the floor or stored on a warehouse shelf, not already taken by another worker), the worker
    /// is sent to the emptiest opened one instead. A box the game took out of a warehouse shelf is never swapped (it is
    /// already the shelf's emptiest).
    /// </summary>
    internal static class BoxPreference
    {
        private static readonly FieldInfo CurrentItemBox = AccessTools.Field(typeof(Worker), "m_CurrentItemBox");
        private static readonly FieldInfo WaitingForPath = AccessTools.Field(typeof(Worker), "m_IsWaitingForPathCallback");
        private static readonly FieldInfo UnableToFindQueue = AccessTools.Field(typeof(Worker), "m_UnableToFindQueue");
        private static readonly MethodInfo OnPathComplete = AccessTools.Method(typeof(Worker), "OnPathComplete");
        private static readonly FieldInfo SeekerField = AccessTools.Field(typeof(Worker), "m_Seeker");
        private static MethodInfo _startPath; private static Type _pathDelegate; private static bool _looked;

        public static void Patch(Harmony h)
        {
            var m = AccessTools.Method(typeof(Worker), "AttemptFindBoxForRestock");
            if (m == null || CurrentItemBox == null || OnPathComplete == null)
            { Plugin.Log.LogWarning("Worker box search not found (different game version?): opened boxes are not preferred."); return; }
            h.Patch(m, postfix: new HarmonyMethod(typeof(BoxPreference), nameof(Postfix)) { priority = Priority.Last });
        }

        private static void Postfix(Worker __instance, bool __runOriginal)
        {
            try
            {
                // Another mod may skip the game's search (e.g. to send the worker to rest): the box field then holds an old box
                if (!__runOriginal || Plugin.Blocked || !Plugin.Instance.PreferOpenedBoxes.Value || __instance == null) return;
                var chosen = CurrentItemBox.GetValue(__instance) as InteractablePackagingBox_Item;
                if (chosen == null || chosen.m_ItemCompartment == null) return;
                int chosenCount = chosen.m_ItemCompartment.GetItemCount(), chosenMax = chosen.m_ItemCompartment.GetMaxItemCount();
                if (chosenMax <= 0 || chosenCount < chosenMax) return; // only replace a full box

                var item = chosen.m_ItemCompartment.GetItemType();
                bool big = chosen.m_IsBigBox;
                var claimed = ClaimedBoxes(__instance);

                // Emptiest opened box object (floor or carried back to a shelf), same item and size
                InteractablePackagingBox_Item bestLive = null; int bestCount = chosenCount;
                var rm = Game.Get<RestockManager>();
                if (rm != null && rm.m_ItemPackagingBoxList != null)
                    foreach (var b in rm.m_ItemPackagingBoxList)
                    {
                        if (b == null || b == chosen || b.m_ItemCompartment == null || b.m_IsBigBox != big || claimed.Contains(b)) continue;
                        if (b.m_ItemCompartment.GetItemType() != item || !b.IsValidObject() || !b.CanWorkerTakeBox()) continue;
                        if (!b.m_IsStored && b.transform.position.y > 2f) continue; // the game's own reach limit
                        int n = b.m_ItemCompartment.GetItemCount();
                        if (n > 0 && n < bestCount) { bestCount = n; bestLive = b; }
                    }
                // Emptiest opened box stored on a warehouse shelf (taken out like the game does: the shelf's last record)
                ShelfCompartment bestShelf = null;
                var comps = Game.WarehouseCompartments();
                if (comps != null)
                    foreach (var c in comps)
                    {
                        if (c == null || c.GetStoredBoxRecordCount() == 0) continue;
                        var ws = c.GetWarehouseShelf();
                        if (ws == null || !ws.IsValidObject()) continue;
                        var last = c.PeekStoredBoxRecord(c.GetStoredBoxRecordCount() - 1);
                        if (last.itemType != item || last.isBigBox != big) continue;
                        // The game keeps a shelf's records sorted fullest → emptiest; check the emptiest of this shelf
                        int min = int.MaxValue;
                        for (int k = 0; k < c.GetStoredBoxRecordCount(); k++) min = Math.Min(min, c.PeekStoredBoxRecord(k).amount);
                        if (min > 0 && min < bestCount) { bestCount = min; bestShelf = c; bestLive = null; }
                    }
                if (bestLive == null && bestShelf == null) return;

                // A full box the game just took out of a warehouse shelf goes back there first, as data, like the game does
                ShelfCompartment chosenShelf = chosen.m_IsStored ? chosen.GetBoxStoredCompartment() : null;
                if (chosen.m_IsStored && !PutBack(chosen)) return;
                InteractablePackagingBox_Item target = bestLive;
                if (bestShelf != null)
                {
                    bestShelf.ArrangeBoxItemBasedOnItemCount(); // emptiest record last, as the game expects
                    target = RestockManager.MaterializeStoredCandidate(new PackageBoxCandidate
                    { storedCompartment = bestShelf, storedRecordIndex = bestShelf.GetStoredBoxRecordCount() - 1 });
                }
                // Could not take the opened box out: the box that was put back is taken out again, as the game had it
                if (target == null && chosenShelf != null)
                {
                    target = RestockManager.MaterializeStoredCandidate(new PackageBoxCandidate
                    { storedCompartment = chosenShelf, storedRecordIndex = chosenShelf.GetStoredBoxRecordCount() - 1 });
                    if (target != null) Retarget(__instance, target);
                    return;
                }
                if (target == null) return;
                if (!Retarget(__instance, target)) return;
                Plugin.Log.LogInfo($"Worker '{__instance.name}' uses an opened box of {item} ({target.m_ItemCompartment.GetItemCount()}/{chosenMax} left, " +
                                   $"{(target.m_IsStored ? "from a warehouse shelf" : "on the floor")}) instead of opening a full one.");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Opened box preference skipped: " + e.Message); }
        }

        /// <summary>
        /// Turns a box the game just took out of a warehouse shelf back into that shelf's data record, the way the game
        /// itself stores a box (InteractablePackagingBox_Item.OnFinishLerp: record added, meshes hidden, OnDestroyed).
        /// </summary>
        internal static bool PutBack(InteractablePackagingBox_Item box)
        {
            var shelf = box.GetBoxStoredCompartment();
            if (shelf == null || box.m_ItemCompartment == null) return false;
            shelf.AddStoredBoxRecord(new StoredBoxRecord
            {
                itemType = box.m_ItemCompartment.GetItemType(),
                amount = box.m_ItemCompartment.GetItemCount(),
                isBigBox = box.m_IsBigBox,
            });
            if (box.m_RigMeshGrp != null) box.m_RigMeshGrp.SetActive(false);
            if (box.m_StaticMeshGrp != null) box.m_StaticMeshGrp.SetActive(false);
            box.OnDestroyed();
            return true;
        }

        /// <summary>Points the worker at another box, the way AttemptFindBoxForRestock ends.</summary>
        private static bool Retarget(Worker w, InteractablePackagingBox_Item box)
        {
            Transform dest = box.transform;
            var stored = box.GetBoxStoredCompartment();
            if (stored != null && stored.m_CustomerStandLoc != null) dest = stored.m_CustomerStandLoc.transform;
            if (!StartPath(w, dest)) return false;
            CurrentItemBox.SetValue(w, box);
            return true;
        }

        /// <summary>Sends the worker walking to dest, the way the game's Attempt* methods end.</summary>
        internal static bool StartPath(Worker w, Transform dest)
        {
            object seeker = SeekerField?.GetValue(w); // by reflection: its type lives in the pathfinding assembly
            if (seeker == null || dest == null || OnPathComplete == null) return false;
            if (!_looked)
            {
                _looked = true;
                foreach (var m in seeker.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (m.Name != "StartPath") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(Vector3) && ps[1].ParameterType == typeof(Vector3)) { _startPath = m; _pathDelegate = ps[2].ParameterType; break; }
                }
            }
            if (_startPath == null) return false;
            w.m_TargetTransform = dest;
            _startPath.Invoke(seeker, new object[] { w.transform.position, dest.position, Delegate.CreateDelegate(_pathDelegate, w, OnPathComplete) });
            WaitingForPath?.SetValue(w, true);
            UnableToFindQueue?.SetValue(w, false);
            return true;
        }

        private static readonly HashSet<InteractablePackagingBox_Item> Claimed = new HashSet<InteractablePackagingBox_Item>();

        private static HashSet<InteractablePackagingBox_Item> ClaimedBoxes(Worker self)
        {
            Claimed.Clear();
            var list = WorkerManager.GetWorkerList();
            if (list == null) return Claimed;
            foreach (var w in list)
                if (w != null && w != self && w.IsActive())
                {
                    var b = w.GetCurrentItemBox();
                    if (b != null) Claimed.Add(b);
                }
            return Claimed;
        }
    }
}
