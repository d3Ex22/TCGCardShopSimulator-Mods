using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SmartWorkerRestock
{
    internal enum ShelfPriority
    {
        /// <summary>The game's choice: a random compartment (a lone worker always takes the first one found).</summary>
        GameDefault,
        /// <summary>The compartment holding the fewest items first.</summary>
        LowestCount,
        /// <summary>The compartment with the lowest fill percentage first.</summary>
        LowestFillPercent,
    }

    /// <summary>
    /// Emptiest shelves first. The game picks the product to restock and then the shop compartment at random.
    /// Two light hooks, no rewrite of the game's code:
    ///  - before the worker looks for a box (AttemptFindBoxForRestock, prefix): the product becomes the one of the emptiest
    ///    compartment among the products that have a box the worker can take now;
    ///  - after the worker picked the compartment to fill (AttemptFindShelf, postfix): he is sent to the emptiest labeled
    ///    compartment of the product he carries.
    /// Compartments another worker is already walking to are skipped, and each other worker fetching a box of a product
    /// "keeps" one compartment of that product, so several workers spread over the emptiest compartments.
    /// </summary>
    internal static class RestockPriority
    {
        private static readonly FieldInfo HeldBox = AccessTools.Field(typeof(Worker), "m_CurrentHoldItemBox");
        private static readonly FieldInfo CanFindStored = AccessTools.Field(typeof(Worker), "m_CanFindStoredItem");

        public static void Patch(Harmony h)
        {
            var find = AccessTools.Method(typeof(Worker), "AttemptFindBoxForRestock");
            var shelf = AccessTools.Method(typeof(Worker), "AttemptFindShelf");
            if (find == null || shelf == null || HeldBox == null || CanFindStored == null)
            { Plugin.Log.LogWarning("Worker restock choice not found (different game version?): emptiest shelves are not preferred."); return; }
            h.Patch(find, prefix: new HarmonyMethod(typeof(RestockPriority), nameof(BeforeFindBox)));
            h.Patch(shelf, postfix: new HarmonyMethod(typeof(RestockPriority), nameof(AfterFindShelf)));
        }

        private static ShelfPriority Mode => Plugin.Instance.RestockPriorityMode.Value;
        private static bool Off => Plugin.Blocked || Mode == ShelfPriority.GameDefault;

        /// <summary>Negative when compartment a needs stock more than b.</summary>
        private static int Compare(Worker w, ShelfCompartment a, ShelfCompartment b)
        {
            int c = Mode == ShelfPriority.LowestCount
                ? a.GetItemCount().CompareTo(b.GetItemCount())
                : Fill(a).CompareTo(Fill(b));
            if (c != 0) return c;
            Vector3 p = w.transform.position; // ties: the nearest
            return (a.m_CustomerStandLoc.position - p).sqrMagnitude.CompareTo((b.m_CustomerStandLoc.position - p).sqrMagnitude);
        }

        private static float Fill(ShelfCompartment c)
        {
            int max = c.GetMaxItemCount();
            return max > 0 ? (float)c.GetItemCount() / max : 1f;
        }

        /// <summary>Labeled shop compartments of this product that are not full and that no other worker is walking to.</summary>
        private static List<ShelfCompartment> FreeCompartments(Worker self, EItemType item)
        {
            var result = new List<ShelfCompartment>();
            var workers = WorkerManager.GetWorkerList();
            foreach (var shelf in ShelfManager.GetShelfListToRestockItem(item, ignoreNoneType: true))
                foreach (var c in shelf.GetNonFullItemCompartmentList(item, ignoreNoneType: true))
                {
                    if (c == null || c.m_CustomerStandLoc == null || c.GetItemType() != item || result.Contains(c)) continue;
                    bool taken = false;
                    if (workers != null)
                        foreach (var w in workers)
                            if (w != null && w != self && w.IsActive() && w.CheckWorkerSameTarget(c.m_CustomerStandLoc)) { taken = true; break; }
                    if (!taken) result.Add(c);
                }
            return result;
        }

        /// <summary>Other workers on their way to pick up a box of this product (they will fill one of its compartments).</summary>
        private static int Fetching(Worker self, EItemType item)
        {
            int n = 0;
            var workers = WorkerManager.GetWorkerList();
            if (workers == null) return 0;
            foreach (var w in workers)
                if (w != null && w != self && w.IsActive() && w.m_WorkerTask == EWorkerTask.RestockShelf
                    && w.m_CurrentState == EWorkerState.SearchingForBox && w.m_TargetBoxItemType == item && HeldBox.GetValue(w) == null)
                    n++;
            return n;
        }

        /// <summary>Products the worker can find a box of right now, with the same rules as AttemptFindBoxForRestock.</summary>
        private static HashSet<EItemType> ProductsWithBox(Worker self)
        {
            var list = RestockManager.GetPackageBoxCandidateList(includeStoredItem: false);
            if (list.Count == 0 || (bool)CanFindStored.GetValue(self)) list = RestockManager.GetPackageBoxCandidateList(includeStoredItem: true);
            var workers = WorkerManager.GetWorkerList();
            var result = new HashSet<EItemType>();
            foreach (var b in list)
            {
                if (!b.IsValidObject() || !b.CanWorkerTakeBox() || b.GetItemCount() <= 0) continue;
                if (!b.GetIsStored() && b.GetPosition().y > 2f) continue; // the game's own reach limit
                bool taken = false;
                if (workers != null)
                    foreach (var w in workers)
                        if (w != null && w != self && w.IsActive() && b.RefersToSameBox(w.GetCurrentItemBox())) { taken = true; break; }
                if (!taken) result.Add(b.GetItemType());
            }
            return result;
        }

        private static void BeforeFindBox(Worker __instance)
        {
            try
            {
                if (Off || __instance == null || __instance.m_WorkerTask != EWorkerTask.RestockShelf || __instance.m_TargetBoxSize != 0
                    || HeldBox.GetValue(__instance) != null) return;
                ShelfCompartment best = null;
                foreach (var item in ProductsWithBox(__instance))
                {
                    if (item == EItemType.None) continue;
                    var comps = FreeCompartments(__instance, item);
                    int skip = Fetching(__instance, item);
                    if (comps.Count <= skip) continue; // other workers already cover every compartment of this product
                    comps.Sort((a, b) => Compare(__instance, a, b));
                    var c = comps[skip];
                    if (best == null || Compare(__instance, c, best) < 0) best = c;
                }
                if (best == null) return; // nothing labeled to fill: keep the game's choice
                var product = best.GetItemType();
                if (product != __instance.m_TargetBoxItemType)
                    Plugin.Log.LogDebug($"Worker '{__instance.name}' restocks {product} first ({best.GetItemCount()}/{best.GetMaxItemCount()} on the shelf) instead of {__instance.m_TargetBoxItemType}.");
                __instance.m_TargetBoxItemType = product;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Emptiest shelf first skipped (product): " + e.Message); }
        }

        private static void AfterFindShelf(Worker __instance, bool __runOriginal)
        {
            try
            {
                if (Off || !__runOriginal || __instance == null || __instance.m_CurrentState != EWorkerState.WalkToShelf) return;
                var held = HeldBox.GetValue(__instance) as InteractablePackagingBox_Item;
                var chosen = __instance.m_CurrentItemCompartment;
                if (held == null || held.m_ItemCompartment == null || chosen == null) return;
                var comps = FreeCompartments(__instance, held.m_ItemCompartment.GetItemType());
                if (comps.Count == 0) return; // only unlabeled compartments: keep the game's choice
                comps.Sort((a, b) => Compare(__instance, a, b));
                var best = comps[0];
                if (best == chosen || (chosen.GetItemType() == best.GetItemType() && Compare(__instance, chosen, best) <= 0)) return;
                var dest = best.m_CustomerStandLoc;
                if (!BoxPreference.StartPath(__instance, dest)) return;
                __instance.m_CurrentItemCompartment = best;
                __instance.m_CurrentShelf = best.GetShelf();
                Plugin.Log.LogDebug($"Worker '{__instance.name}' fills the emptiest {best.GetItemType()} compartment ({best.GetItemCount()}/{best.GetMaxItemCount()}).");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Emptiest shelf first skipped (compartment): " + e.Message); }
        }
    }
}
