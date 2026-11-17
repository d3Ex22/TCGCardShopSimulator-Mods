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
                if (config.CanHaveGodPacks)
                    Plugin.Log.LogInfo($"{def.ItemType}: its god packs stay EPL's own (7 cards)");
                done++;
            }
            Plugin.Log.LogInfo($"{done} pack(s) with their own slots: " + string.Join(", ", Active.Select(kv => $"{kv.Key} ({kv.Value.Size})")));
        }

        private static EnhancedPrefabLoader.API.Models.PackConfigData SafeConfig(EnhancedPrefabLoader.API.Assets.IItemLibrary items, EItemType type)
        {
            try { return items.GetPackConfig(type); } catch { return null; }
        }

        // EPL keeps the generator in its internal generation data; read it to put it back when we are unloaded.
        private static IPackGenerator CurrentGenerator(EItemType type)
        {
            try
            {
                var runtime = AccessTools.TypeByName("EnhancedPrefabLoader.Core.EplRuntimeData");
                var lookup = Traverse.Create(runtime).Property("Assets").Property("LookupLibrary").Property("GenerationDataLookup").GetValue();
                if (lookup is System.Collections.IDictionary d && d.Contains(type))
                    return Traverse.Create(d[type]).Property("PackGenerator").GetValue() as IPackGenerator;
            }
            catch { }
            return null;
        }

        internal static void Restore()
        {
            if (Active.Count == 0 || !Epl.IsAvailable) { Active.Clear(); return; }
            foreach (var kv in Active)
            {
                try { if (kv.Value.Original != null) Epl.Api.Services.ItemService.SetPackGenerator(kv.Key, kv.Value.Original); }
                catch { }
            }
            Active.Clear();
        }
    }
}
