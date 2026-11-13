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
    /// Smart Worker Restock: workers order the stock the shop shelves need, counting what is really available, use up
    /// opened boxes before opening new ones, and restock the emptiest shelves first. Replaces "Auto Worker Order" (stays idle while that mod is loaded).
    ///
    /// Why a new mod: the game now keeps boxes stored on warehouse shelves as data records (StoredBoxRecord) instead of box
    /// objects. Auto Worker Order only counts box objects, so it saw an empty warehouse and reordered every pass (confirmed
    /// in game: it ordered an item while 16 of it were stored), which kept new boxes on the floor that workers took first.
    /// </summary>
    [BepInPlugin(Guid, "Smart Worker Restock", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "gab.tcgshop.smartworkerrestock";
        public const string Version = "1.4.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal ConfigEntry<bool> Enabled, RequireWorker, PreferBigBox, PreferOpenedBoxes, HoldTakenBoxes;
        internal ConfigEntry<int> OrderBelowPercent, ExtraBoxesInReserve, MaxItemTypesPerOrder, ReorderCooldownSeconds;
        internal ConfigEntry<float> CheckIntervalSeconds, MoneyReserve;
        internal ConfigEntry<ShelfPriority> RestockPriorityMode;

        private float _next;
        private bool _blockedByAwo, _awoChecked;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            const string g = "1. General", o = "2. Ordering", w = "3. Workers";
            Enabled = Config.Bind(g, "Enabled", true, "Master switch: automatic orders, opened boxes first and emptiest shelves first.");
            RequireWorker = Config.Bind(o, "RequireHiredWorker", true, "Only order while at least one worker is on the clock.");
            CheckIntervalSeconds = Config.Bind(o, "CheckIntervalSeconds", 45f, "Seconds between two stock checks.");
            OrderBelowPercent = Config.Bind(o, "OrderBelowFillPercent", 80, new ConfigDescription(
                "A shelf compartment counts as needing stock when it is filled at or below this percentage.", new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes { ShowRangeAsPercent = true }));
            ExtraBoxesInReserve = Config.Bind(o, "ExtraBoxesInReserve", 0, new ConfigDescription(
                "Boxes to keep in reserve (warehouse, floor, on the way) for each item that needs stock, on top of what the shelves need.",
                new AcceptableValueRange<int>(0, 20)));
            MoneyReserve = Config.Bind(o, "MoneyReserve", 500f, "Money never spent by automatic orders.");
            MaxItemTypesPerOrder = Config.Bind(o, "MaxItemTypesPerOrder", 20, new ConfigDescription(
                "Maximum number of different items in one order (the emptiest shelves come first).", new AcceptableValueRange<int>(1, 100),
                new ConfigurationManagerAttributes { ShowRangeAsPercent = false }));
            ReorderCooldownSeconds = Config.Bind(o, "ReorderCooldownSeconds", 180, new ConfigDescription(
                "After ordering an item, it is not ordered again for this long (boxes may take a while to appear, e.g. with a delivery truck mod).",
                new AcceptableValueRange<int>(0, 1800)));
            PreferBigBox = Config.Bind(o, "PreferBigBox", true, "Order big boxes when both sizes are unlocked.");
            PreferOpenedBoxes = Config.Bind(w, "UseOpenedBoxesFirst", true,
                "When a worker would open a full box, he takes the emptiest opened box of the same item instead (on the floor or stored on a warehouse shelf).");

            RestockPriorityMode = Config.Bind(w, "RestockPriority", ShelfPriority.LowestCount,
                "Which shop compartment workers restock first. LowestCount: the one holding the fewest items. " +
                "LowestFillPercent: the one with the lowest fill percentage. GameDefault: the game's random choice.");

            HoldTakenBoxes = Config.Bind(w, "KeepTakenBoxesInShelf", true,
                "A box a worker goes to take from a warehouse shelf stays still in the shelf until he picks it up, instead of " +
                "being pushed out onto the floor (game behaviour where shelves stand side by side). If he changes his mind, " +
                "the box is stored in the shelf again.");

            // One id per load (ScriptEngine reload: the old instance is destroyed after the new one started)
            _harmony = new Harmony(Guid + "." + DateTime.Now.Ticks);
            BoxPreference.Patch(_harmony);
            RestockPriority.Patch(_harmony);
            StoredBoxHold.Patch(_harmony);
            Log.LogInfo($"Smart Worker Restock {Version} loaded.");
        }

        private void Update()
        {
            if (!Enabled.Value) return;
            StoredBoxHold.Tick();
            if (!_awoChecked)
            {
                _awoChecked = true;
                _blockedByAwo = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "AutoWorkerOrder");
                if (_blockedByAwo)
                    Log.LogWarning("Auto Worker Order is also installed: Smart Worker Restock replaces it and stays idle until it is removed " +
                                   "(both would order and steer workers).");
            }
            if (_blockedByAwo || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Mathf.Max(10f, CheckIntervalSeconds.Value);
            OrderPlanner.Run();
        }

        private Harmony _harmony;

        /// <summary>Unloaded (game closing, or a reload of BepInEx/scripts during development): every patch goes.</summary>
        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            StoredBoxHold.Clear();
            if (Instance == this) Instance = null;
        }

        internal static bool Blocked => Instance == null || !Instance.Enabled.Value || Instance._blockedByAwo;
    }
}
