using BepInEx;
using BepInEx.Logging;
using EnhancedPrefabLoader.API;
using HarmonyLib;

namespace VariableBoosterSize
{
    /// <summary>
    /// Extends Enhanced Prefab Loader (EPL) card packs past its 7 cards. A pack can have any number of slots, given in
    /// its EPL item ("Slot8", "Slot9"... in SlotWeights, or ranges such as "Slot1-5") or in a separate *.packslots.json
    /// file that overrides a pack of another mod without editing it. The pack is generated through EPL's own pack
    /// generator API, and the game's pack opening shows every card: same card by card reveal, all cards on the final
    /// screen.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("EnhancedPrefabLoader")]
    [BepInDependency("EnhancedPrefabLoader.API")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "gab.tcgshop.eplvariableboostersize";
        public const string Name = "EPL Variable Booster Size";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        internal static BepInEx.Configuration.ConfigEntry<float> RowSpacing, CardScale;

        private void Awake()
        {
            Log = Logger;
            RowSpacing = Config.Bind("Pack opening", "RowSpacing", 0.73f, new BepInEx.Configuration.ConfigDescription(
                "Packs of more than 7 cards show their cards on two rows on the final screen: distance between the middles of the two rows, in card heights (under 1, the bottom row covers the foot of the top one).",
                new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 2f)));
            CardScale = Config.Bind("Pack opening", "CardScale", 0.75f, new BepInEx.Configuration.ConfigDescription(
                "Packs of more than 7 cards: size of the cards on the final screen (1 = the game's size). They get smaller on their own when the rows do not fit the screen.",
                new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 1f)));
            Harmony = new Harmony(Guid);
            PackDefs.LoadFiles();
            PackDefs.PatchValidator();   // before EPL reads its bundles (it starts once the game manager starts)
            Opening.Apply();
            if (Epl.IsAvailable) Epl.Api.Events.OnBundleLoadingComplete += Generators.Register;
            else Log.LogError("EPL API not available: packs keep their EPL size.");
            Log.LogInfo($"{Name} {Version} loaded.");
        }

        private void OnDestroy()
        {
            try { if (Epl.IsAvailable) Epl.Api.Events.OnBundleLoadingComplete -= Generators.Register; } catch { }
            Generators.Restore();
            Opening.Revert();
            Harmony?.UnpatchSelf();
        }
    }
}
