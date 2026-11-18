using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using EnhancedPrefabLoader.API.Models;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace VariableBoosterSize
{
    /// <summary>A pack's slots, in order: each slot holds the rarity names it can draw and their weights.</summary>
    internal sealed class PackDef
    {
        public string ItemType;
        public string Source;
        public bool? Duplicates;
        public float? PriceMultiplier;
        public bool? GodPacks;                         // "CanHaveGodPacks"
        public float? GodChance;                       // "GodPackPercentage"
        public Dictionary<string, float> GodWeights;   // "GodWeights"
        public List<Dictionary<string, float>> Slots = new List<Dictionary<string, float>>();
    }

    /// <summary>
    /// Pack definitions from EPL items whose SlotWeights use our keys, and from *.packslots.json files under
    /// BepInEx/plugins keyed by ItemType, which win over the item. EPL rejects a whole item on a key it does not know,
    /// so our keys are taken out before its check and EPL keeps a valid 7 slot copy.
    /// </summary>
    internal static class PackDefs
    {
        internal static readonly Dictionary<string, PackDef> ByItemType = new Dictionary<string, PackDef>(StringComparer.Ordinal);
        private const int EplSlots = 7;

        /// <summary>Slot numbers (1 based) a key names, or null when it is not a slot key.</summary>
        internal static List<int> SlotsOf(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var result = new List<int>();
            // EPL sanitizes keys before checking them: "Slot1-5" becomes "Slot1_5", "Slot1, Slot2" becomes "Slot1_Slot2"
            key = System.Text.RegularExpressions.Regex.Replace(key, "_(?=Slot)", ",", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Replace('_', '-');
            foreach (var raw in key.Split(','))
            {
                var part = raw.Trim();
                if (!part.StartsWith("Slot", StringComparison.OrdinalIgnoreCase)) return null;
                var range = part.Substring(4).Split('-');
                if (range.Length > 2 || !int.TryParse(range[0].Trim(), out int from)) return null;
                int to = from;
                if (range.Length == 2)
                {
                    var end = range[1].Trim();
                    if (end.StartsWith("Slot", StringComparison.OrdinalIgnoreCase)) end = end.Substring(4);
                    if (!int.TryParse(end, out to)) return null;
                }
                if (from < 1 || to < from || to > 64) return null;
                for (int i = from; i <= to; i++) result.Add(i);
            }
            return result.Count > 0 ? result : null;
        }

        /// <summary>Builds the ordered slot list from slot keys; null (with the reason) when the slots have gaps.</summary>
        private static List<Dictionary<string, float>> Ordered(IEnumerable<KeyValuePair<string, Dictionary<string, float>>> weights, string owner)
        {
            var bySlot = new SortedDictionary<int, Dictionary<string, float>>();
            foreach (var kv in weights)
            {
                var slots = SlotsOf(kv.Key);
                if (slots == null) { Plugin.Log.LogWarning($"{owner}: '{kv.Key}' is not a slot key such as Slot8, Slot1-5 or Slot1, Slot2, ignored"); continue; }
                foreach (int s in slots) bySlot[s] = kv.Value;
            }
            if (bySlot.Count == 0) return null;
            int max = bySlot.Keys.Last();
            if (bySlot.Count != max)
            {
                var missing = Enumerable.Range(1, max).Where(i => !bySlot.ContainsKey(i));
                Plugin.Log.LogError($"{owner}: no weights for slot {string.Join(", ", missing)}, the pack keeps its EPL slots");
                return null;
            }
            return bySlot.Values.Select(w => new Dictionary<string, float>(w)).ToList();
        }

        internal static void LoadFiles()
        {
            ByItemType.Clear();
            string[] files;
            try { files = Directory.GetFiles(Paths.PluginPath, "*.packslots.json", SearchOption.AllDirectories); }
            catch (Exception e) { Plugin.Log.LogError("pack files not searched: " + e.Message); return; }
            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(file);
                try
                {
                    int n = 0;
                    foreach (var p in JObject.Parse(File.ReadAllText(file)).Properties())
                    {
                        if (p.Name.StartsWith("_") || !(p.Value is JObject o)) continue;
                        var weights = new List<KeyValuePair<string, Dictionary<string, float>>>();
                        if (o["SlotWeights"] is JObject sw)
                            foreach (var s in sw.Properties())
                                if (s.Value is JObject w)
                                    weights.Add(new KeyValuePair<string, Dictionary<string, float>>(s.Name,
                                        w.Properties().ToDictionary(r => r.Name, r => (float)r.Value, StringComparer.Ordinal)));
                        var slots = Ordered(weights, $"{name}: {p.Name}");
                        if (slots == null) continue;
                        var def = new PackDef { ItemType = p.Name, Source = name, Slots = slots, Duplicates = (bool?)o["CanHaveDuplicates"], PriceMultiplier = (float?)o["PriceMultiplier"],
                            GodPacks = (bool?)o["CanHaveGodPacks"], GodChance = (float?)o["GodPackPercentage"],
                            GodWeights = (o["GodWeights"] as JObject)?.Properties().ToDictionary(r => r.Name, r => (float)r.Value, StringComparer.Ordinal) };
                        if (ByItemType.TryGetValue(p.Name, out var old)) Plugin.Log.LogWarning($"{p.Name}: {name} replaces the slots from {old.Source}");
                        ByItemType[p.Name] = def;
                        n++;
                    }
                    Plugin.Log.LogInfo($"{name}: {Plugin.Count(n, "pack")}");
                }
                catch (Exception e) { Plugin.Log.LogError($"{name} not read: {e.Message}"); }
            }
        }

        internal static void PatchValidator()
        {
            var t = AccessTools.TypeByName("EnhancedPrefabLoader.Core.LoadingPipeline.Validators.ItemValidator");
            var m = t == null ? null : AccessTools.Method(t, "Validate");
            if (m == null) { Plugin.Log.LogWarning("EPL ItemValidator.Validate not found: slots past Slot7 in EPL items are ignored, pack files still work"); return; }
            Plugin.Harmony.Patch(m, prefix: new HarmonyMethod(typeof(PackDefs), nameof(ValidatePrefix)) { priority = Priority.First });
        }

        // The items of one EPL bundle, before EPL checks them
        private static void ValidatePrefix(object __0)
        {
            if (!(__0 is IEnumerable items)) return;
            foreach (var item in items)
            {
                if (item == null) continue;
                try { Take(item); }
                catch (Exception e) { Plugin.Log.LogError("pack slots not read: " + e.Message); }
            }
        }

        private static void Take(object item)
        {
            var t = Traverse.Create(item);
            if (!t.Property("IsCardPack").GetValue<bool>()) return;
            if (!(t.Property("SlotWeights").GetValue() is Dictionary<string, Dictionary<string, float>> weights) || weights.Count == 0) return;
            // Only items with a key EPL cannot read but we can
            if (weights.Keys.All(k => Enum.TryParse(k, out PackSlot _) || SlotsOf(k) == null)) return;
            string id = t.Property("ItemType").GetValue<string>();
            var slots = Ordered(weights, id);
            if (slots == null) return;
            if (!ByItemType.ContainsKey(id)) ByItemType[id] = new PackDef { ItemType = id, Source = "EPL item", Slots = slots };
            // EPL keeps Slot1 to Slot7, a valid pack for its checks and its fallback
            weights.Clear();
            for (int i = 0; i < Math.Min(EplSlots, slots.Count); i++) weights["Slot" + (i + 1)] = new Dictionary<string, float>(slots[i]);
            Plugin.Log.LogInfo($"{id}: {slots.Count} slots read from its EPL item");
        }
    }
}
