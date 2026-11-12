using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SmartWorkerRestock
{
    /// <summary>Game managers read from their CSingleton instance field (CSingleton.Instance can search the whole scene or create one).</summary>
    internal static class Game
    {
        private static readonly Dictionary<Type, FieldInfo> Fields = new Dictionary<Type, FieldInfo>();

        public static T Get<T>() where T : Component
        {
            if (!Fields.TryGetValue(typeof(T), out var f)) Fields[typeof(T)] = f = AccessTools.Field(typeof(CSingleton<T>), "instance");
            var o = f?.GetValue(null) as T;
            return o != null ? o : null;
        }

        private static readonly FieldInfo WarehouseCompartmentsField = AccessTools.Field(typeof(RestockManager), "m_WarehouseCompartmentList");

        public static List<ShelfCompartment> WarehouseCompartments()
        {
            var rm = Get<RestockManager>();
            return rm != null ? WarehouseCompartmentsField?.GetValue(rm) as List<ShelfCompartment> : null;
        }

        public static bool InShop() => GameInstance.m_FinishedSavefileLoading && GameInstance.m_HasFinishHideLoadingScreen
                                        && Get<ShelfManager>() != null && Get<RestockManager>() != null;
    }

    /// <summary>
    /// Stock check and automatic order.
    ///  - Need: for each shop shelf compartment filled at or below the threshold, the items missing to fill it, per item.
    ///  - Available: everything the workers can use without ordering: boxes stored on warehouse shelves (data records),
    ///    every box object (floor, carried, on a shelf) and the boxes already ordered and waiting to arrive.
    ///  - Order: (need + reserve buffer − available) rounded up to whole boxes, emptiest shelves first, within the money
    ///    above the reserve, paid and booked exactly like the game's restock checkout.
    /// </summary>
    internal static class OrderPlanner
    {
        private static readonly Dictionary<EItemType, float> LastOrdered = new Dictionary<EItemType, float>();

        private class Need { public EItemType Item; public int Missing; public float WorstFill = 1f; }
        private class Line { public EItemType Item; public RestockData Data; public int Boxes; public int PerBox; public float UnitCost; }

        public static void Run()
        {
            var p = Plugin.Instance;
            try
            {
                if (Plugin.Blocked || !Game.InShop()) return;
                if (p.RequireWorker.Value && (Game.Get<WorkerManager>() == null || WorkerManager.GetActiveWorkerCount() <= 0)) return;

                var needs = ShelfNeeds(p);
                if (needs.Count == 0) return;
                var available = AvailableStock();

                var lines = new List<Line>();
                float now = Time.unscaledTime;
                foreach (var n in needs.Values.OrderBy(x => x.WorstFill))
                {
                    if (lines.Count >= p.MaxItemTypesPerOrder.Value) break;
                    if (LastOrdered.TryGetValue(n.Item, out float t) && now - t < p.ReorderCooldownSeconds.Value) continue; // still on its way
                    var data = PickRestockData(n.Item, p.PreferBigBox.Value);
                    if (data == null) continue; // license not bought
                    int perBox = Math.Max(1, RestockManager.GetMaxItemCountInBox(data.itemType, data.isBigBox));
                    available.TryGetValue(n.Item, out int have);
                    int want = n.Missing + p.ExtraBoxesInReserve.Value * perBox - have;
                    if (want <= 0) continue; // what is available covers the shelves
                    int boxes = (want + perBox - 1) / perBox;
                    lines.Add(new Line { Item = n.Item, Data = data, Boxes = boxes, PerBox = perBox, UnitCost = CPlayerData.GetItemCost(n.Item) });
                }
                if (lines.Count > 0) Buy(lines);
            }
            catch (Exception e) { Plugin.Log.LogError($"Stock check failed: {e}"); }
        }

        private static Dictionary<EItemType, Need> ShelfNeeds(Plugin p)
        {
            var needs = new Dictionary<EItemType, Need>();
            var sm = Game.Get<ShelfManager>();
            if (sm == null || sm.m_ShelfList == null) return needs;
            float threshold = Mathf.Clamp(p.OrderBelowPercent.Value, 0, 100) / 100f;
            foreach (var shelf in sm.m_ShelfList)
            {
                if (shelf == null || shelf.m_ItemNotForSale) continue;
                var comps = shelf.GetItemCompartmentList();
                if (comps == null) continue;
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var item = c.GetItemType();
                    if (item == EItemType.None) continue;
                    int count = Math.Max(0, c.GetItemCount()), max = Math.Max(0, c.GetMaxItemCount());
                    if (max <= 0 || count >= max) continue;
                    float fill = (float)count / max;
                    if (fill > threshold) continue;
                    if (!needs.TryGetValue(item, out var n)) needs[item] = n = new Need { Item = item };
                    n.Missing += max - count;
                    n.WorstFill = Mathf.Min(n.WorstFill, fill);
                }
            }
            return needs;
        }

        /// <summary>Items the workers can use without ordering, per item.</summary>
        private static Dictionary<EItemType, int> AvailableStock()
        {
            var have = new Dictionary<EItemType, int>();
            void Add(EItemType i, int n) { if (i != EItemType.None && n > 0) have[i] = (have.TryGetValue(i, out int v) ? v : 0) + n; }

            // Boxes stored on warehouse shelves: data records since the game's storage rework (Auto Worker Order missed them)
            var comps = Game.WarehouseCompartments();
            if (comps != null)
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    for (int k = 0; k < c.GetStoredBoxRecordCount(); k++) { var r = c.PeekStoredBoxRecord(k); Add(r.itemType, r.amount); }
                }
            // Every box object: on the floor, carried, or stored but taken out (materialized)
            var rm = Game.Get<RestockManager>();
            if (rm != null && rm.m_ItemPackagingBoxList != null)
                foreach (var b in rm.m_ItemPackagingBoxList)
                    if (b != null && b.m_ItemCompartment != null) Add(b.m_ItemCompartment.GetItemType(), b.m_ItemCompartment.GetItemCount());
            // Ordered, waiting to be delivered (box counts)
            var idx = CPlayerData.m_SpawnBoxRestockIndexWaitingList; var cnt = CPlayerData.m_SpawnBoxItemCountWaitingList;
            if (idx != null && cnt != null)
                for (int i = 0; i < Math.Min(idx.Count, cnt.Count); i++)
                {
                    var d = InventoryBase.GetRestockData(idx[i]);
                    if (d != null) Add(d.itemType, cnt[i] * Math.Max(1, RestockManager.GetMaxItemCountInBox(d.itemType, d.isBigBox)));
                }
            return have;
        }

        /// <summary>A restock entry for this item whose license is bought (the preferred box size when both are).</summary>
        private static RestockData PickRestockData(EItemType item, bool preferBig)
        {
            var list = InventoryBase.GetRestockDataUsingItemType(item);
            if (list == null) return null;
            RestockData any = null;
            foreach (var d in list)
            {
                if (d == null || !CPlayerData.GetIsItemLicenseUnlocked(d.index)) continue;
                if (d.isBigBox == preferBig) return d;
                if (any == null) any = d;
            }
            return any;
        }

        private static float DeliveryFee(int boxes) => boxes <= 0 ? 0f : Mathf.Clamp(10 * boxes / 5, 5, 1000); // the game's checkout formula

        /// <summary>Fits the order in the money above the reserve (emptiest shelves first), then pays it like the game's checkout.</summary>
        private static void Buy(List<Line> lines)
        {
            var p = Plugin.Instance;
            double budget = CPlayerData.m_CoinAmountDouble - Math.Max(0f, p.MoneyReserve.Value);
            float goods = 0f; int boxes = 0;
            var bought = new List<Line>();
            foreach (var l in lines)
            {
                int n = l.Boxes;
                while (n > 0 && goods + l.UnitCost * l.PerBox * n + DeliveryFee(boxes + n) > budget) n--;
                if (n <= 0) continue;
                l.Boxes = n;
                goods += l.UnitCost * l.PerBox * n;
                boxes += n;
                bought.Add(l);
            }
            if (bought.Count == 0)
            {
                Plugin.Log.LogInfo($"Stock check: {lines.Count} item(s) to order, but not enough money above the reserve ({p.MoneyReserve.Value:0}).");
                return;
            }
            float fee = DeliveryFee(boxes), total = goods + fee;

            // Same steps as RestockItemCheckoutScreen.OnPressConfirmCheckout + RestockItemScreen.EvaluateCartCheckout
            foreach (var l in bought)
            {
                int units = l.Boxes * l.PerBox;
                CPlayerData.UpdateAverageItemCost(l.Item, units, l.UnitCost);
                PriceChangeManager.AddTransaction(0f - units * l.UnitCost, ETransactionType.Restock, (int)l.Item, units);
            }
            PriceChangeManager.AddTransaction(0f - fee, ETransactionType.RestockDeliveryFee, 0);
            CEventManager.QueueEvent(new CEventPlayer_ReduceCoin(total));
            CPlayerData.m_GameReportDataCollect.supplyCost -= total;
            CPlayerData.m_GameReportDataCollectPermanent.supplyCost -= total;
            float now = Time.unscaledTime;
            foreach (var l in bought)
            {
                RestockManager.SpawnPackageBoxItemMultipleFrame(l.Data.index, l.Boxes);
                LastOrdered[l.Item] = now;
            }
            CEventManager.QueueEvent(new CEventPlayer_AddShopExp(boxes * 5));
            Plugin.Log.LogInfo($"Workers ordered {boxes} box(es) of {bought.Count} item(s): " +
                               string.Join(", ", bought.Select(l => $"{l.Item} x{l.Boxes}")) +
                               $". Goods {goods:0.00} + delivery {fee:0.00} = {total:0.00}.");
        }
    }
}
