using System;
using System.Reflection;
using HarmonyLib;

namespace SmartWorkerRestock
{
    /// <summary>
    /// Boxes left lying around are put away first. When a worker is about to fetch a box for a shop shelf while a loose box
    /// has room on a warehouse shelf, he stores that box instead, the way the game does once the shop shelves are full.
    /// </summary>
    internal static class StoreFirst
    {
        private static readonly FieldInfo HeldBox = AccessTools.Field(typeof(Worker), "m_CurrentHoldItemBox");
        private static readonly FieldInfo LastStored = AccessTools.Field(typeof(Worker), "m_LastStoredItemBox");
        private static readonly FieldInfo NoLabel = AccessTools.Field(typeof(Worker), "m_IsFillShelfWithoutLabel");
        private static readonly MethodInfo FindBoxToStore = AccessTools.Method(typeof(Worker), "AttemptFindBoxToStore");

        public static void Patch(Harmony h)
        {
            var m = AccessTools.Method(typeof(Worker), "AttemptFindBoxForRestock");
            if (m == null || HeldBox == null || LastStored == null || NoLabel == null || FindBoxToStore == null)
            {
                Plugin.Log.LogWarning("Worker box storing not found: store loose boxes first is off.");
                return;
            }
            // Ahead of the other box search prefixes, which then see the original skipped
            h.Patch(m, prefix: new HarmonyMethod(typeof(StoreFirst), nameof(Prefix)) { priority = Priority.First + 1 });
        }

        private static bool Prefix(Worker __instance)
        {
            try
            {
                if (Plugin.Blocked || !Plugin.Instance.StoreLooseBoxesFirst.Value || WorkerAiConflict.YieldSteering) return true;
                if (__instance == null || __instance.m_WorkerTask != EWorkerTask.RestockShelf || HeldBox.GetValue(__instance) != null) return true;
                var box = LooseBox(__instance);
                if (box == null) return true;
                var (item, size) = (__instance.m_TargetBoxItemType, __instance.m_TargetBoxSize);
                __instance.m_TargetBoxItemType = box.m_ItemCompartment.GetItemType();
                __instance.m_TargetBoxSize = box.m_IsBigBox ? 2 : 1;
                FindBoxToStore.Invoke(__instance, null);
                if (__instance.m_CurrentState != EWorkerState.SearchingForBoxToStore)
                {
                    (__instance.m_TargetBoxItemType, __instance.m_TargetBoxSize) = (item, size);
                    return true;
                }
                Plugin.Log.LogInfo($"{__instance.name} puts away a loose {__instance.m_TargetBoxItemType} box before restocking.");
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Store loose boxes first skipped: " + e.GetBaseException().Message);
                return true;
            }
        }

        /// <summary>A box on the floor, within reach and not taken by another worker, with room for it on a warehouse shelf.</summary>
        private static InteractablePackagingBox_Item LooseBox(Worker self)
        {
            var claimed = BoxPreference.ClaimedBoxes(self);
            var last = LastStored.GetValue(self);
            bool ignoreNoneType = !(bool)NoLabel.GetValue(self);
            foreach (var b in RestockManager.GetItemPackagingBoxListWithItem(includeStoredItem: false))
            {
                if (b == null || b == (object)last || claimed.Contains(b) || !b.CanWorkerTakeBox() || b.transform.position.y > 2f) continue;
                var item = b.m_ItemCompartment.GetItemType();
                foreach (var shelf in ShelfManager.GetWarehouseShelfListToStoreItem(item, b.m_IsBigBox, ignoreNoneType))
                    if (shelf != null && shelf.GetNonFullItemCompartmentList(item, b.m_IsBigBox, ignoreNoneType).Count > 0)
                        return b;
            }
            return null;
        }
    }
}
