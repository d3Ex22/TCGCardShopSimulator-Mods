using BepInEx;
using BepInEx.Logging;
using EnhancedPrefabLoader.API;
using HarmonyLib;

namespace VariableBoosterSize
{
    /// <summary>
    /// Enhanced Prefab Loader card packs of any size. Slots come from the pack's EPL item or from a *.packslots.json file;
    /// packs are generated through EPL's pack generator API and the game's opening shows every card.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("EnhancedPrefabLoader")]
    [BepInDependency("EnhancedPrefabLoader.API")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "gab.tcgshop.eplvariableboostersize";
        public const string Name = "EPL Variable Booster Size";
        public const string Version = "1.2.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        internal static BepInEx.Configuration.ConfigEntry<float> RowSpacing, CardScale;

        private void Awake()
        {
            Log = Logger;
            RowSpacing = Config.Bind("Pack opening", "RowSpacing", 0.73f, new BepInEx.Configuration.ConfigDescription(
                "Packs of more than 7 cards: distance between the middles of the two rows on the final screen, in card heights. Under 1, the bottom row overlaps the top one.",
                new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 2f)));
            CardScale = Config.Bind("Pack opening", "CardScale", 0.75f, new BepInEx.Configuration.ConfigDescription(
                "Packs of more than 7 cards: card size on the final screen, 1 being the game's size. Cards shrink further when the rows do not fit the screen.",
                new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 1f)));
            Harmony = new Harmony(Guid);
            PackDefs.LoadFiles();
            PackDefs.PatchValidator();   // before EPL reads its bundles (it starts once the game manager starts)
            Opening.Apply();
            Prices.Apply();
            if (Epl.IsAvailable) Epl.Api.Events.OnBundleLoadingComplete += Generators.Register;
            else Log.LogError("EPL API not available: packs keep their EPL size.");
            Log.LogInfo($"{Name} {Version} loaded.");
        }

        internal static string Count(int n, string one, string many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";

        private void OnDestroy()
        {
            try { if (Epl.IsAvailable) Epl.Api.Events.OnBundleLoadingComplete -= Generators.Register; } catch { }
            Generators.Restore();
            Opening.Revert();
            Harmony?.UnpatchSelf();
        }
    }
}
