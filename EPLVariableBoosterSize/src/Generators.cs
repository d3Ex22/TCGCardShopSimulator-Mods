using System;
using System.Collections.Generic;
using System.Linq;
using EnhancedPrefabLoader.API;
using EnhancedPrefabLoader.API.Behaviors;
using HarmonyLib;
using UnityEngine;

namespace VariableBoosterSize
{
    /// <summary>
    /// One card per slot, like EPL's "Guaranteed" packs, for any number of slots. Set through EPL's own API
    /// (ItemService.SetPackGenerator): EPL still makes the cards (foils, ghost cards, its events) from what we pick.
    /// </summary>
    internal sealed class SlotPackGenerator : IPackGenerator
    {
        private readonly string _id;
        private readonly List<Dictionary<ERarity, float>> _slots;
        private readonly bool _duplicates;
        private bool _warned;

        internal int Size => _slots.Count;
        internal readonly IPackGenerator Original;

        internal SlotPackGenerator(string id, List<Dictionary<ERarity, float>> slots, bool duplicates, IPackGenerator original)
        {
            _id = id; _slots = slots; _duplicates = duplicates; Original = original;
        }

        public List<MonsterData> GeneratePack(ECardExpansionType cardExpansion, List<MonsterData> cardPool, bool hasRandomFoils)
        {
            var result = new List<MonsterData>(_slots.Count);
            if (cardPool == null || cardPool.Count == 0) return result;
            var byRarity = new Dictionary<ERarity, List<MonsterData>>();
            foreach (var card in cardPool)
            {
                if (!byRarity.TryGetValue(card.Rarity, out var list)) byRarity[card.Rarity] = list = new List<MonsterData>();
                list.Add(card);
            }
            int empty = 0;
            foreach (var slot in _slots)
            {
                float total = 0f;
                foreach (var kv in slot) if (kv.Value > 0f && byRarity.ContainsKey(kv.Key)) total += kv.Value;
                if (total <= 0f) { empty++; continue; }
                float roll = UnityEngine.Random.value * total;
                ERarity pick = default;
                foreach (var kv in slot)
                {
                    if (kv.Value <= 0f || !byRarity.ContainsKey(kv.Key)) continue;
                    pick = kv.Key;
                    if ((roll -= kv.Value) < 0f) break;
                }
                var pool = byRarity[pick];
                int i = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[i]);
                if (!_duplicates)
                {
                    pool[i] = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                    if (pool.Count == 0) byRarity.Remove(pick);
                }
            }
            // A slot with none of its rarities left (or in the set): a random card, as EPL does.
            while (result.Count < _slots.Count) result.Add(cardPool[UnityEngine.Random.Range(0, cardPool.Count)]);
            if (empty > 0 && !_warned)
            {
                _warned = true;
                Plugin.Log.LogWarning($"{_id}: {empty} slot(s) had no card of their rarities left, filled with random cards");
            }
            return result;
        }
    }

    internal static class Generators
    {
        /// <summary>Packs we generate, by item type, and their size.</summary>
        internal static readonly Dictionary<EItemType, SlotPackGenerator> Active = new Dictionary<EItemType, SlotPackGenerator>();

        internal static int SizeOf(EItemType type) => Active.TryGetValue(type, out var g) ? g.Size : 0;

        internal static void Register()
        {
            try { RegisterAll(); }
            catch (Exception e) { Plugin.Log.LogError("packs not registered: " + e); }
        }

        private static void RegisterAll()
        {
            var items = Epl.Api.Assets.Items;
            var service = Epl.Api.Services.ItemService;
            int done = 0;
            foreach (var def in PackDefs.ByItemType.Values)
            {
                if (!Enum.TryParse(def.ItemType, out EItemType type))
                {
                    Plugin.Log.LogInfo($"{def.ItemType} ({def.Source}): pack not installed, skipped");
                    continue;
                }
                var config = SafeConfig(items, type);
                if (config == null)
                {
                    Plugin.Log.LogWarning($"{def.ItemType} ({def.Source}): not an EPL card pack; skipped");
                    continue;
                }
                var slots = new List<Dictionary<ERarity, float>>();
                var unknown = new HashSet<string>();
                foreach (var s in def.Slots)
                {
                    var w = new Dictionary<ERarity, float>();
                    foreach (var kv in s)
                    {
                        // EPL registers rarities under their sanitized name ("Holo Rare Holofoil" -> "HoloRareHolofoil").
                        if (Enum.TryParse(EnhancedPrefabLoader.API.Extensions.StringExtensions.Sanitize(kv.Key), out ERarity r)) w[r] = kv.Value;
                        else unknown.Add(kv.Key);
                    }
                    slots.Add(w);
                }
                if (unknown.Count > 0)
                    Plugin.Log.LogWarning($"{def.ItemType}: unknown rarities ignored: {string.Join(", ", unknown.Select(u => "'" + u + "'"))}");
                Active.TryGetValue(type, out var previous);
                var original = previous != null ? previous.Original : CurrentGenerator(type);
                var gen = new SlotPackGenerator(def.ItemType, slots, def.Duplicates ?? config.CanHaveDuplicates, original);
                service.SetPackGenerator(type, gen);
                Active[type] = gen;
                if (def.GodPacks.HasValue || def.GodChance.HasValue)
                {
                    if (!GodConfigOriginals.ContainsKey(type)) GodConfigOriginals[type] = (config.CanHaveGodPacks, config.GodPackPercentage);
                    service.SetGodPack(type, def.GodPacks ?? config.CanHaveGodPacks, def.GodChance ?? config.GodPackPercentage);
                }
                if (def.GodPacks ?? config.CanHaveGodPacks) GodPacks(def.ItemType, type, slots.Count, Rarities(def.GodWeights, def.ItemType));
                if (def.PriceMultiplier is float pm && pm > 0f && Math.Abs(pm - 1f) > 0.0001f) Prices.Set(type, pm, def.ItemType);
                done++;
            }
            Plugin.Log.LogInfo($"{done} pack(s) with their own slots: " + string.Join(", ", Active.Select(kv => $"{kv.Key} ({kv.Value.Size})")));
            if (GodOriginals.Count > 0) Plugin.Log.LogInfo($"{GodOriginals.Count} of them get god packs of their own size: " + string.Join(", ", GodOriginals.Keys));
        }

        private static EnhancedPrefabLoader.API.Models.PackConfigData SafeConfig(EnhancedPrefabLoader.API.Assets.IItemLibrary items, EItemType type)
        {
            try { return items.GetPackConfig(type); } catch { return null; }
        }

        // EPL keeps the generators in its internal generation data (one per pack): read to put them back when we are
        // unloaded, and for god packs (the API only sets the normal one) written there too.
        private static string _dataError;

        private static object GenerationData(EItemType type)
        {
            _dataError = null;
            try
            {
                var runtime = AccessTools.TypeByName("EnhancedPrefabLoader.Core.EplRuntimeData");
                if (runtime == null) { _dataError = "EplRuntimeData not found"; return null; }
                var assets = AccessTools.Property(runtime, "Assets")?.GetValue(null);
                var library = assets == null ? null : AccessTools.Property(assets.GetType(), "LookupLibrary")?.GetValue(assets);
                var lookup = library == null ? null : AccessTools.Property(library.GetType(), "GenerationDataLookup")?.GetValue(library);
                if (!(lookup is System.Collections.IDictionary d)) { _dataError = "generation data not found"; return null; }
                if (!d.Contains(type)) { _dataError = "no generation data for the pack"; return null; }
                return d[type];
            }
            catch (Exception e) { _dataError = e.Message; return null; }
        }

        private static System.Reflection.PropertyInfo GeneratorProperty(object data, string name) =>
            data == null ? null : AccessTools.Property(data.GetType(), name);

        private static IPackGenerator CurrentGenerator(EItemType type)
        {
            var data = GenerationData(type);
            return GeneratorProperty(data, "PackGenerator")?.GetValue(data) as IPackGenerator;
        }

        /// <summary>A pack file's rarity weights (names as in the set), as EPL's rarities; null when none given.</summary>
        private static Dictionary<ERarity, float> Rarities(Dictionary<string, float> names, string id)
        {
            if (names == null) return null;
            var w = new Dictionary<ERarity, float>();
            foreach (var kv in names)
            {
                if (Enum.TryParse(EnhancedPrefabLoader.API.Extensions.StringExtensions.Sanitize(kv.Key), out ERarity r)) w[r] = kv.Value;
                else Plugin.Log.LogWarning($"{id}: unknown god pack rarity ignored: '{kv.Key}'");
            }
            return w;
        }

        /// <summary>God pack settings of EPL changed by a pack file, put back when we are unloaded.</summary>
        private static readonly Dictionary<EItemType, (bool on, float chance)> GodConfigOriginals = new Dictionary<EItemType, (bool, float)>();

        /// <summary>Original god pack generators, by pack.</summary>
        private static readonly Dictionary<EItemType, IPackGenerator> GodOriginals = new Dictionary<EItemType, IPackGenerator>();

        /// <summary>
        /// A god pack of the pack's size: every slot drawn from the set's god pack weights, duplicates allowed, as EPL's own
        /// god packs (which always have 7 cards).
        /// </summary>
        private static void GodPacks(string id, EItemType type, int size, Dictionary<ERarity, float> own)
        {
            try
            {
                IReadOnlyDictionary<ERarity, float> weights = own != null && own.Count > 0 ? own : Epl.Api.Assets.Items.GetPackWeights(type)?.GodWeights;
                var data = GenerationData(type);
                var god = GeneratorProperty(data, "GodPackGenerator");
                string why = weights == null || weights.Count == 0 ? "no god pack weights" : data == null ? _dataError : god == null || !god.CanWrite ? "god pack generator not found" : null;
                if (why != null)
                {
                    Plugin.Log.LogWarning($"{id}: its god packs stay EPL's own (7 cards): {why}");
                    return;
                }
                if (!GodOriginals.ContainsKey(type)) GodOriginals[type] = god.GetValue(data) as IPackGenerator;
                var slot = weights.ToDictionary(kv => kv.Key, kv => kv.Value);
                var slots = Enumerable.Range(0, size).Select(_ => new Dictionary<ERarity, float>(slot)).ToList();
                god.SetValue(data, new SlotPackGenerator(id + " (god pack)", slots, true, null));
            }
            catch (Exception e) { Plugin.Log.LogWarning($"{id}: god packs stay EPL's own (7 cards): {e.Message}"); }
        }

        internal static void Restore()
        {
            Prices.Factor.Clear();
            if (!Epl.IsAvailable) { Active.Clear(); GodOriginals.Clear(); GodConfigOriginals.Clear(); return; }
            foreach (var kv in Active)
            {
                try { if (kv.Value.Original != null) Epl.Api.Services.ItemService.SetPackGenerator(kv.Key, kv.Value.Original); }
                catch { }
            }
            foreach (var kv in GodConfigOriginals)
            {
                try { Epl.Api.Services.ItemService.SetGodPack(kv.Key, kv.Value.on, kv.Value.chance); }
                catch { }
            }
            GodConfigOriginals.Clear();
            foreach (var kv in GodOriginals)
            {
                try { var data = GenerationData(kv.Key); GeneratorProperty(data, "GodPackGenerator")?.SetValue(data, kv.Value); }
                catch { }
            }
            Active.Clear();
            GodOriginals.Clear();
        }
    }
}
