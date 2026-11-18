using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SmartWorkerRestock
{
    /// <summary>
    /// Opened boxes first. When the box search (the game's, or another mod's such as Worker AI Overhaul) gives a worker a
    /// full box, he is sent to the emptiest opened box of the same item and size instead, on the floor or stored on a
    /// warehouse shelf, unless another worker already goes for it.
    /// </summary>
    internal static class BoxPreference
    {
        private static readonly FieldInfo CurrentItemBox = AccessTools.Field(typeof(Worker), "m_CurrentItemBox");
        internal static readonly FieldInfo PendingShelf = AccessTools.Field(typeof(Worker), "m_PendingStoredCompartment");
        private static readonly FieldInfo WaitingForPath = AccessTools.Field(typeof(Worker), "m_IsWaitingForPathCallback");
        private static readonly FieldInfo UnableToFindQueue = AccessTools.Field(typeof(Worker), "m_UnableToFindQueue");
        private static readonly MethodInfo OnPathComplete = AccessTools.Method(typeof(Worker), "OnPathComplete");
        private static readonly FieldInfo SeekerField = AccessTools.Field(typeof(Worker), "m_Seeker");
        private static MethodInfo _startPath;
        private static Type _pathDelegate;
        private static bool _looked;

        // Another mod may replace or skip the game's search: only a box chosen during this call is considered
        [ThreadStatic] private static InteractablePackagingBox_Item _before;

        private static readonly HashSet<InteractablePackagingBox_Item> Claimed = new HashSet<InteractablePackagingBox_Item>();
        private static readonly HashSet<ShelfCompartment> ClaimedShelf = new HashSet<ShelfCompartment>();

        public static void Patch(Harmony h)
        {
            var m = AccessTools.Method(typeof(Worker), "AttemptFindBoxForRestock");
            if (m == null || CurrentItemBox == null || PendingShelf == null || OnPathComplete == null)
            {
                Plugin.Log.LogWarning("Worker box search not found: opened boxes first is off.");
                return;
            }
            h.Patch(m, prefix: new HarmonyMethod(typeof(BoxPreference), nameof(Before)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(BoxPreference), nameof(Postfix)) { priority = Priority.Last });
        }

        private static void Before(Worker __instance)
        {
            _before = __instance != null ? CurrentItemBox.GetValue(__instance) as InteractablePackagingBox_Item : null;
        }

        private static void Postfix(Worker __instance, bool __runOriginal)
        {
            try
            {
                if (Plugin.Blocked || !Plugin.Instance.PreferOpenedBoxes.Value || WorkerAiConflict.YieldSteering || __instance == null) return;
                var chosen = CurrentItemBox.GetValue(__instance) as InteractablePackagingBox_Item;
                if (!__runOriginal && chosen == _before) return;
                // A stored box chosen by the game is only its shelf for now, and already that shelf's emptiest box
                if (chosen == null || chosen.m_ItemCompartment == null) return;
                int full = chosen.m_ItemCompartment.GetMaxItemCount();
                if (full <= 0 || chosen.m_ItemCompartment.GetItemCount() < full) return;

                var item = chosen.m_ItemCompartment.GetItemType();
                bool big = chosen.m_IsBigBox;
                int bestCount = full;

                InteractablePackagingBox_Item bestBox = null;
                var claimed = ClaimedBoxes(__instance);
                var boxes = Game.Get<RestockManager>()?.m_ItemPackagingBoxList;
                if (boxes != null)
                    foreach (var b in boxes)
                    {
                        if (b == null || b == chosen || b.m_ItemCompartment == null || b.m_IsBigBox != big || claimed.Contains(b)) continue;
                        if (b.m_ItemCompartment.GetItemType() != item || !b.IsValidObject() || !b.CanWorkerTakeBox()) continue;
                        if (!b.m_IsStored && b.transform.position.y > 2f) continue; // the game's reach limit
                        int n = b.m_ItemCompartment.GetItemCount();
                        if (n > 0 && n < bestCount) { bestCount = n; bestBox = b; }
                    }

                // Stored records are kept fullest first: the last one is the shelf's emptiest, the one taken out
                ShelfCompartment bestShelf = null;
                var claimedShelves = ClaimedShelves(__instance);
                var comps = Game.WarehouseCompartments();
                if (comps != null)
                    foreach (var c in comps)
                    {
                        if (c == null || c.GetStoredBoxRecordCount() == 0 || claimedShelves.Contains(c)) continue;
                        var shelf = c.GetWarehouseShelf();
                        if (shelf == null || !shelf.IsValidObject()) continue;
                        var last = c.PeekStoredBoxRecord(c.GetStoredBoxRecordCount() - 1);
                        if (last.itemType != item || last.isBigBox != big) continue;
                        if (last.amount > 0 && last.amount < bestCount) { bestCount = last.amount; bestShelf = c; bestBox = null; }
                    }
                if (bestBox == null && bestShelf == null) return;

                // A full box another mod already took out of a shelf goes back there first
                if (chosen.m_IsStored && !PutBack(chosen)) return;
                bool sent = bestShelf != null ? AimAtShelf(__instance, bestShelf) : Retarget(__instance, bestBox);
                if (sent)
                    Plugin.Log.LogInfo($"{__instance.name} takes an opened {item} box with {bestCount}/{full} left {(bestShelf != null || bestBox.m_IsStored ? "from a warehouse shelf" : "from the floor")} instead of a full one.");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Opened boxes first skipped: " + e.Message); }
        }

        /// <summary>Stores a box taken out of a warehouse shelf back as that shelf's record, the way the game stores a box.</summary>
        private static bool PutBack(InteractablePackagingBox_Item box)
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

        /// <summary>Sends the worker to a warehouse shelf: the game takes the box out when he gets there.</summary>
        private static bool AimAtShelf(Worker w, ShelfCompartment shelf)
        {
            int last = shelf.GetStoredBoxRecordCount() - 1;
            var dest = shelf.m_CustomerStandLoc != null ? shelf.m_CustomerStandLoc.transform : last >= 0 ? shelf.GetSlotTransform(last) : null;
            if (!StartPath(w, dest)) return false;
            CurrentItemBox.SetValue(w, null);
            PendingShelf.SetValue(w, shelf);
            return true;
        }

        private static bool Retarget(Worker w, InteractablePackagingBox_Item box)
        {
            var dest = box.transform;
            var stored = box.GetBoxStoredCompartment();
            if (stored != null && stored.m_CustomerStandLoc != null) dest = stored.m_CustomerStandLoc.transform;
            if (!StartPath(w, dest)) return false;
            CurrentItemBox.SetValue(w, box);
            PendingShelf.SetValue(w, null);
            return true;
        }

        /// <summary>Sends the worker walking to dest, the way the game's Attempt* methods end.</summary>
        internal static bool StartPath(Worker w, Transform dest)
        {
            object seeker = SeekerField?.GetValue(w); // its type lives in the pathfinding assembly
            if (seeker == null || dest == null) return false;
            if (!_looked)
            {
                _looked = true;
                foreach (var m in seeker.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (m.Name != "StartPath") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(Vector3) && ps[1].ParameterType == typeof(Vector3))
                    {
                        _startPath = m;
                        _pathDelegate = ps[2].ParameterType;
                        break;
                    }
                }
            }
            if (_startPath == null) return false;
            w.m_TargetTransform = dest;
            _startPath.Invoke(seeker, new object[] { w.transform.position, dest.position, Delegate.CreateDelegate(_pathDelegate, w, OnPathComplete) });
            WaitingForPath?.SetValue(w, true);
            UnableToFindQueue?.SetValue(w, false);
            return true;
        }

        private static HashSet<InteractablePackagingBox_Item> ClaimedBoxes(Worker self)
        {
            Claimed.Clear();
            var list = WorkerManager.GetWorkerList();
            if (list != null)
                foreach (var w in list)
                    if (w != null && w != self && w.IsActive() && w.GetCurrentItemBox() is InteractablePackagingBox_Item b) Claimed.Add(b);
            return Claimed;
        }

        /// <summary>Warehouse shelf compartments other workers are walking to.</summary>
        internal static HashSet<ShelfCompartment> ClaimedShelves(Worker self)
        {
            ClaimedShelf.Clear();
            var list = WorkerManager.GetWorkerList();
            if (list != null)
                foreach (var w in list)
                    if (w != null && w != self && w.IsActive() && PendingShelf.GetValue(w) is ShelfCompartment c) ClaimedShelf.Add(c);
            return ClaimedShelf;
        }
    }
}
