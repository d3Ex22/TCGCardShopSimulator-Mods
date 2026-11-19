using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SmartWorkerRestock
{
    /// <summary>
    /// Smart Worker Restock: workers order the stock the shop shelves need, counting boxes stored on warehouse shelves,
    /// put loose boxes away, use up opened boxes before opening new ones, and restock the emptiest shelves first.
    /// Replaces Auto Worker Order.
    /// </summary>
    [BepInPlugin(Guid, "Smart Worker Restock", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "gab.tcgshop.smartworkerrestock";
        public const string Version = "1.6.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal ConfigEntry<bool> Enabled, RequireWorker, PreferBigBox, PreferOpenedBoxes, StoreLooseBoxesFirst;
        internal ConfigEntry<int> OrderBelowPercent, ExtraBoxesInReserve, MaxItemTypesPerOrder, ReorderCooldownSeconds;
        internal ConfigEntry<float> CheckIntervalSeconds, MoneyReserve;
        internal ConfigEntry<ShelfPriority> RestockPriorityMode;
        internal ConfigEntry<WorkerAiMode> WorkerAiOverhaulMode;

        private Harmony _harmony;
        private float _next;
        private bool _blockedByAwo, _awoChecked;

        internal static bool Blocked => Instance == null || !Instance.Enabled.Value || Instance._blockedByAwo;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            const string g = "1. General", o = "2. Ordering", w = "3. Workers";
            Enabled = Config.Bind(g, "Enabled", true, "Master switch: automatic orders, opened boxes first and emptiest shelves first.");
            RequireWorker = Config.Bind(o, "RequireHiredWorker", true, "Only order while at least one worker is on the clock.");
            CheckIntervalSeconds = Config.Bind(o, "CheckIntervalSeconds", 45f, "Seconds between two stock checks.");
            OrderBelowPercent = Config.Bind(o, "OrderBelowFillPercent", 80, new ConfigDescription(
                "A shelf compartment needs stock when it is filled at or below this percentage.", new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes { ShowRangeAsPercent = true }));
            ExtraBoxesInReserve = Config.Bind(o, "ExtraBoxesInReserve", 0, new ConfigDescription(
                "Extra boxes to keep for each item that needs stock, on top of what the shelves need.", new AcceptableValueRange<int>(0, 20)));
            MoneyReserve = Config.Bind(o, "MoneyReserve", 500f, "Money automatic orders never spend.");
            MaxItemTypesPerOrder = Config.Bind(o, "MaxItemTypesPerOrder", 20, new ConfigDescription(
                "Different items in one order, emptiest shelves first.", new AcceptableValueRange<int>(1, 100),
                new ConfigurationManagerAttributes { ShowRangeAsPercent = false }));
            ReorderCooldownSeconds = Config.Bind(o, "ReorderCooldownSeconds", 180, new ConfigDescription(
                "Wait before ordering the same item again, so boxes still on their way are not ordered twice.", new AcceptableValueRange<int>(0, 1800)));
            PreferBigBox = Config.Bind(o, "PreferBigBox", true, "Order big boxes when both sizes are unlocked.");
            PreferOpenedBoxes = Config.Bind(w, "UseOpenedBoxesFirst", true,
                "A worker about to open a full box takes the emptiest opened box of the same item instead, on the floor or on a warehouse shelf.");
            StoreLooseBoxesFirst = Config.Bind(w, "StoreLooseBoxesFirst", true,
                "Workers put boxes left on the floor away on warehouse shelves before restocking the shop shelves.");
            RestockPriorityMode = Config.Bind(w, "RestockPriority", ShelfPriority.LowestCount,
                "Which shop compartment workers restock first. LowestCount: the fewest items. LowestFillPercent: the lowest fill. GameDefault: the game's random choice.");
            WorkerAiOverhaulMode = Config.Bind(w, "WorkerAIOverhaulMode", WorkerAiMode.Ask,
                "With Worker AI Overhaul, which also chooses the box and shelf workers restock. Ask: asked once a save is loaded. " +
                "Compatibility: Worker AI Overhaul chooses, Smart Worker Restock only orders stock. Override: Smart Worker Restock chooses.");

            // One id per load: on a ScriptEngine reload the old instance is destroyed after the new one started
            _harmony = new Harmony(Guid + "." + DateTime.Now.Ticks);
            StoreFirst.Patch(_harmony);
            BoxPreference.Patch(_harmony);
            RestockPriority.Patch(_harmony);
            Log.LogInfo($"Smart Worker Restock {Version} loaded.");
        }

        private void Update()
        {
            if (!Enabled.Value) return;
            if (!_awoChecked)
            {
                _awoChecked = true;
                _blockedByAwo = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "AutoWorkerOrder");
                if (_blockedByAwo) Log.LogWarning("Auto Worker Order is installed: Smart Worker Restock replaces it and stays idle until it is removed.");
            }
            if (_blockedByAwo || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Mathf.Max(10f, CheckIntervalSeconds.Value);
            OrderPlanner.Run();
        }

        private void OnGUI()
        {
            if (!Blocked) WorkerAiConflict.OnGUI();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }
}
