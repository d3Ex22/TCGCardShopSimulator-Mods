using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VariableBoosterSize
{
    /// <summary>
    /// PriceMultiplier: the cost and market price of a pack and of its boxes are scaled when the game reads them, so it
    /// works on existing saves and goes away with the entry.
    /// </summary>
    internal static class Prices
    {
        internal static readonly Dictionary<EItemType, float> Factor = new Dictionary<EItemType, float>();

        // CPlayerData wraps RestockManager and can be inlined, so both are patched; the depth keeps the factor from applying twice
        [ThreadStatic] private static int _depth;

        internal static void Apply()
        {
            var post = new HarmonyMethod(typeof(Prices), nameof(Postfix));
            var outerPre = new HarmonyMethod(typeof(Prices), nameof(OuterPrefix));
            var outerPost = new HarmonyMethod(typeof(Prices), nameof(OuterPostfix));
            foreach (var name in new[] { "GetItemCost", "GetItemMarketPrice", "GetItemMarketPriceCustomPercent" })
            {
                var inner = AccessTools.Method(typeof(RestockManager), name);
                if (inner != null) Plugin.Harmony.Patch(inner, postfix: post);
                else Plugin.Log.LogWarning($"RestockManager.{name} not found: pack price multipliers do not apply to it");
                var outer = AccessTools.Method(typeof(CPlayerData), name);
                if (outer != null) Plugin.Harmony.Patch(outer, prefix: outerPre, postfix: outerPost);
            }
        }

        private static void OuterPrefix() => _depth++;

        private static void OuterPostfix(EItemType itemType, ref float __result)
        {
            _depth--;
            Scale(itemType, ref __result);
        }

        private static void Postfix(EItemType itemType, ref float __result)
        {
            if (_depth > 0) return;
            Scale(itemType, ref __result);
        }

        private static void Scale(EItemType itemType, ref float result)
        {
            if (Factor.Count == 0 || !Factor.TryGetValue(itemType, out float f)) return;
            result = Mathf.RoundToInt(result * f * 100f) / 100f;
        }

        /// <summary>Sets the multiplier of a pack and of the boxes that hold it.</summary>
        internal static void Set(EItemType pack, float factor, string id)
        {
            Factor[pack] = factor;
            int boxes = 0;
            foreach (EItemType t in Enum.GetValues(typeof(EItemType)))
            {
                EItemType? spawned = null;
                try { spawned = EnhancedPrefabLoader.API.Epl.Api.Assets.Items.GetSpawnedPackType(t); } catch { }
                if (spawned == pack) { Factor[t] = factor; boxes++; }
            }
            Plugin.Log.LogInfo($"{id}: prices x{factor} for the pack and {Plugin.Count(boxes, "box", "boxes")}");
        }
    }
}
