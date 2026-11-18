using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VariableBoosterSize
{
    /// <summary>
    /// The game's opening has 7 card places and stops the reveal at the 7th card. For our packs the card lists get the
    /// pack's size, using the game's first places or copies of its last one, the reveal goes on to the last card, and the
    /// final screen shows one row for a smaller pack or two rows fitted to the screen for a bigger one. Other packs get
    /// the game's own lists back.
    /// </summary>
    internal static class Opening
    {
        private static AccessTools.FieldRef<CardOpeningSequence, Item> _item;

        private static CardOpeningSequence _seq;
        private static List<Card3dUIGroup> _ui;           // the game's own lists
        private static List<Animation> _anim;
        private static List<Transform> _pos;
        private static int _gameSize = 7;
        private static Vector3[] _pile;                   // local positions of the game's card pile
        // The game scales each card from its base scale when it is revealed, one entry per card place
        private static AccessTools.FieldRef<CardOpeningSequence, List<Vector3>> _baseScales;
        private static List<Vector3> _gameScales;

        private static readonly List<Card3dUIGroup> ExtraUi = new List<Card3dUIGroup>();
        private static readonly List<Animation> ExtraAnim = new List<Animation>();
        private static readonly List<Transform> RevealPos = new List<Transform>();

        private static int _size;                         // 0 when the game's lists are used
        private static bool _fitted;
        private static readonly List<Transform> ScaledAnims = new List<Transform>();
        private static readonly List<Vector3> ScaledFrom = new List<Vector3>();
        private static int _patched;

        internal static void Apply()
        {
            _item = AccessTools.FieldRefAccess<CardOpeningSequence, Item>("m_CurrentItem");
            _baseScales = AccessTools.Field(typeof(CardOpeningSequence), "m_CardBaseScaleList") != null
                ? AccessTools.FieldRefAccess<CardOpeningSequence, List<Vector3>>("m_CardBaseScaleList") : null;
            // Before EPL's OpenScreen prefix, which fills as many card places as the list holds
            Plugin.Harmony.Patch(AccessTools.Method(typeof(CardOpeningSequence), "OpenScreen"),
                prefix: new HarmonyMethod(typeof(Opening), nameof(OpenPrefix)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(Opening), nameof(OpenPostfix)));
            _cards = AccessTools.FieldRefAccess<CardOpeningSequence, List<CardData>>("m_ManualOpenCardDataList");
            _patched = 0;
            // First: the final screen places go back to the game's before another mod reads them
            Plugin.Harmony.Patch(AccessTools.Method(typeof(CardOpeningSequence), "Update"),
                prefix: new HarmonyMethod(typeof(Opening), nameof(PlacesPrefix)) { priority = Priority.First });
            Plugin.Harmony.Patch(AccessTools.Method(typeof(CardOpeningSequence), "Update"),
                // Last: MyCurio Products also places the final screen's cards, ours must win for our packs
                prefix: new HarmonyMethod(typeof(Opening), nameof(UpdatePrefix)) { priority = Priority.Last },
                transpiler: new HarmonyMethod(typeof(Opening), nameof(UpdateTranspiler)) { priority = Priority.Last });
            if (_patched != 4) Plugin.Log.LogWarning($"Pack opening: {_patched} of the game's 4 card count checks found, packs past 7 cards may stop early");
        }

        internal static void Revert()
        {
            if (_seq != null && _ui != null) UseGameLists(_seq);
            Forget();
        }

        /// <summary>Destroys our card places, on a new scene or when we unload.</summary>
        private static void Forget()
        {
            foreach (var ui in ExtraUi) if (ui != null) UnityEngine.Object.Destroy(ui.gameObject);
            foreach (var t in RevealPos) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            ExtraUi.Clear(); ExtraAnim.Clear(); RevealPos.Clear(); ScaledAnims.Clear(); ScaledFrom.Clear();
            _seq = null; _ui = null; _anim = null; _pos = null; _pile = null; _gameScales = null; _size = 0; _fitted = false;
        }

        // The reveal compares the card index with 7 (end) and 6 (last card): both follow the pack's size
        private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var index = AccessTools.Field(typeof(CardOpeningSequence), "m_CurrentOpenedCardIndex");
            var fit = AccessTools.Method(typeof(Opening), nameof(Fit));
            var list = new List<CodeInstruction>(instructions);
            for (int i = 0; i < list.Count - 1; i++)
            {
                if (!list[i].LoadsField(index) || !(Is(list[i + 1], 7) || Is(list[i + 1], 6))) continue;
                list.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_0));
                list.Insert(i + 3, new CodeInstruction(OpCodes.Call, fit));
                _patched++;
                i += 3;
            }
            return list;
        }

        private static bool Is(CodeInstruction c, int value)
        {
            if (c.opcode == (value == 7 ? OpCodes.Ldc_I4_7 : OpCodes.Ldc_I4_6)) return true;
            return (c.opcode == OpCodes.Ldc_I4_S || c.opcode == OpCodes.Ldc_I4) && Convert.ToInt32(c.operand) == value;
        }

        private static int Fit(int game, CardOpeningSequence seq) => _size > 0 && seq == _seq ? _size - (_gameSize - game) : game;

        private static void OpenPrefix(CardOpeningSequence __instance)
        {
            try
            {
                Know(__instance);
                var item = _item(__instance);
                int size = item != null ? Generators.SizeOf(item.GetItemType()) : 0;
                if (size > 0 || _size > 0) Use(__instance, size);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Pack opening: " + e);
                try { if (_seq == __instance) UseGameLists(__instance); } catch { }
            }
        }

        // The opening follows the number of cards actually drawn, which differs for EPL's own god packs
        private static AccessTools.FieldRef<CardOpeningSequence, List<CardData>> _cards;

        private static void OpenPostfix(CardOpeningSequence __instance)
        {
            if (_size == 0 || __instance != _seq) return;
            try
            {
                int drawn = _cards(__instance)?.Count ?? 0;
                if (drawn > 0 && drawn != _size) Use(__instance, drawn);
            }
            catch (Exception e) { Plugin.Log.LogError("Pack opening: " + e); }
        }

        /// <summary>Keeps the game's own lists, once per scene.</summary>
        private static void Know(CardOpeningSequence seq)
        {
            // On a new scene the old opening is destroyed: compare references, Unity's == sees it as null
            if (!ReferenceEquals(_seq, null) && _seq != seq) Forget();
            if (_seq != null) return;
            _seq = seq;
            _ui = seq.m_Card3dUIList; _anim = seq.m_CardAnimList; _pos = seq.m_ShowAllCardPosList;
            _gameSize = Math.Min(_ui.Count, Math.Min(_anim.Count, _pos.Count));
            var scales = _baseScales != null ? _baseScales(seq) : null;
            _gameScales = scales != null ? new List<Vector3>(scales) : null;
            KeepPlaces();
        }

        // The game's final screen places. MyCurio Products moves the first ones for its short packs and leaves them moved.
        private static Vector3[] _placeLocal;
        private static Quaternion[] _placeTurn;

        private static void KeepPlaces()
        {
            _placeLocal = new Vector3[_gameSize];
            _placeTurn = new Quaternion[_gameSize];
            for (int i = 0; i < _gameSize; i++) { _placeLocal[i] = _pos[i].localPosition; _placeTurn[i] = _pos[i].localRotation; }
            // The places are evenly spaced: after a hot reload, moved ones are rebuilt from the even run at the end
            if (_gameSize < 3) return;
            var step = _placeLocal[_gameSize - 1] - _placeLocal[_gameSize - 2];
            int k = _gameSize - 2;
            while (k > 0 && (_placeLocal[k] - _placeLocal[k - 1] - step).sqrMagnitude < 1e-8f) k--;
            if (k == 0) return;
            for (int i = k - 1; i >= 0; i--) { _placeLocal[i] = _placeLocal[i + 1] - step; _placeTurn[i] = _placeTurn[k]; }
            Plugin.Log.LogDebug($"Pack opening: {Plugin.Count(k, "final screen place")} rebuilt");
        }

        /// <summary>Puts the final screen places back, or MyCurio Products could later show a 7 card pack off centre.</summary>
        private static void RestorePlaces()
        {
            if (_placeLocal == null || _pos == null) return;
            for (int i = 0; i < _gameSize; i++)
            {
                if (_pos[i] == null) continue;
                _pos[i].localPosition = _placeLocal[i];
                _pos[i].localRotation = _placeTurn[i];
            }
        }

        private static bool _placesRestored;

        // Every pack, at the start of its final screen
        private static void PlacesPrefix(CardOpeningSequence __instance)
        {
            if (_placeLocal == null || __instance != _seq) return;
            if (__instance.m_StateIndex == 7) { if (!_placesRestored) { _placesRestored = true; RestorePlaces(); } }
            else _placesRestored = false;
        }

        private static Vector3 PlaceAt(int i) { var p = _pos[0].parent; return p != null ? p.TransformPoint(_placeLocal[i]) : _placeLocal[i]; }
        private static Quaternion TurnAt(int i) { var p = _pos[0].parent; return p != null ? p.rotation * _placeTurn[i] : _placeTurn[i]; }

        private static void MakeRevealPos(int count)
        {
            while (RevealPos.Count < count)
            {
                var t = new GameObject("VbsRevealPos" + (RevealPos.Count + 1)).transform;
                t.SetParent(_pos[0].parent, false);
                RevealPos.Add(t);
            }
        }

        private static void Use(CardOpeningSequence seq, int size)
        {
            Know(seq);
            Unscale();
            if (size <= 0 || size == _gameSize) { UseGameLists(seq); return; }

            List<Card3dUIGroup> ui;
            List<Animation> anim;
            if (size < _gameSize)
            {
                // The game hides only the cards of the list: the places left out are hidden here
                UseGameLists(seq);
                ui = _ui.GetRange(0, size);
                anim = _anim.GetRange(0, size);
                for (int i = size; i < _gameSize; i++) if (_ui[i] != null && _ui[i].gameObject.activeSelf) _ui[i].gameObject.SetActive(false);
            }
            else
            {
                Grow(size - _gameSize);
                ui = new List<Card3dUIGroup>(_ui.GetRange(0, _gameSize));
                anim = new List<Animation>(_anim.GetRange(0, _gameSize));
                for (int i = 0; i < size - _gameSize; i++) { ui.Add(ExtraUi[i]); anim.Add(ExtraAnim[i]); }
                Repile(ui);
            }
            MakeRevealPos(size);
            LayRow(size);
            seq.m_Card3dUIList = ui;
            seq.m_CardAnimList = anim;
            seq.m_ShowAllCardPosList = RevealPos.GetRange(0, size);
            SetBaseScales(seq, size);
            _size = size;
            _fitted = false;
        }

        /// <summary>One row between the game's first and last places, until a bigger pack's rows are fitted.</summary>
        private static void LayRow(int size)
        {
            for (int i = 0; i < size; i++)
            {
                float f = size == 1 ? (_gameSize - 1) * 0.5f : (float)i / (size - 1) * (_gameSize - 1);
                int a = Mathf.Min((int)f, _gameSize - 2);
                RevealPos[i].position = Vector3.Lerp(PlaceAt(a), PlaceAt(a + 1), f - a);
                RevealPos[i].rotation = Quaternion.Slerp(TurnAt(a), TurnAt(a + 1), f - a);
            }
        }

        /// <summary>The game's own lists and pile back, our card places hidden.</summary>
        private static void UseGameLists(CardOpeningSequence seq)
        {
            Unscale();
            if (_pile != null)
                for (int i = 0; i < _gameSize; i++) if (_ui[i] != null) _ui[i].transform.localPosition = _pile[i];
            // The game hides only the cards of the list
            foreach (var extra in ExtraUi) if (extra != null && extra.gameObject.activeSelf) extra.gameObject.SetActive(false);
            seq.m_Card3dUIList = _ui; seq.m_CardAnimList = _anim; seq.m_ShowAllCardPosList = _pos;
            SetBaseScales(seq, _gameScales?.Count ?? 0);
            _size = 0;
        }

        /// <summary>One base scale per card place: the game's, and its last one for the places we added.</summary>
        private static void SetBaseScales(CardOpeningSequence seq, int count)
        {
            if (_baseScales == null || _gameScales == null || _gameScales.Count == 0) return;
            var list = _baseScales(seq);
            if (list == null) return;
            list.Clear();
            for (int i = 0; i < count; i++) list.Add(_gameScales[Math.Min(i, _gameScales.Count - 1)]);
        }

        // A bigger pile is spread over the game's pile depth, the pack sits behind the last card
        private static void Repile(List<Card3dUIGroup> cards)
        {
            if (_pile == null)
            {
                _pile = new Vector3[_gameSize];
                for (int i = 0; i < _gameSize; i++) _pile[i] = _ui[i].transform.localPosition;
            }
            for (int i = 0; i < cards.Count; i++)
                cards[i].transform.localPosition = Vector3.Lerp(_pile[0], _pile[_gameSize - 1], (float)i / (cards.Count - 1));
        }

        /// <summary>Copies the game's last card place until there are <paramref name="extra"/> extra places.</summary>
        private static void Grow(int extra)
        {
            var lastUi = _ui[_gameSize - 1];
            var lastAnim = _anim[_gameSize - 1];
            while (ExtraUi.Count < extra)
            {
                var go = UnityEngine.Object.Instantiate(lastUi.gameObject, lastUi.transform.parent);
                go.name = lastUi.name + "_Vbs" + (_gameSize + ExtraUi.Count + 1);
                var ui = go.GetComponent<Card3dUIGroup>();
                var anim = lastAnim.transform.IsChildOf(lastUi.transform) ? Find(go.transform, lastUi.transform, lastAnim.transform).GetComponent<Animation>() : null;
                if (anim == null)
                {
                    UnityEngine.Object.Destroy(go);
                    throw new InvalidOperationException("the game's card place is not built as expected (its Animation is not inside it)");
                }
                // A copy of an inactive card has not run Awake, which links the card to its UI: EPL needs that link
                if (ui.m_CardUI != null) ui.m_CardUI.InitCard3dUIGroup(ui);
                go.SetActive(false);
                ExtraUi.Add(ui);
                ExtraAnim.Add(anim);
            }
        }

        // The copy of target under copyRoot, by its path under root
        private static Transform Find(Transform copyRoot, Transform root, Transform target)
        {
            var path = new List<int>();
            for (var t = target; t != root; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            var c = copyRoot;
            foreach (int i in path) c = c.GetChild(i);
            return c;
        }

        private static void UpdatePrefix(CardOpeningSequence __instance)
        {
            if (_size == 0 && !_fitted) return;
            if (__instance != _seq) return;
            int state = __instance.m_StateIndex;
            if (state >= 7 && state <= 10)
            {
                if (!_fitted && _size > 0)
                {
                    _fitted = true;
                    // Laid out again: another mod may have moved the places since the pack opened
                    if (_size > _gameSize) FitToScreen(_size);
                    else LayRow(_size);
                }
            }
            else if (_fitted)
            {
                _fitted = false;
                Unscale();
            }
        }

        private static void Unscale()
        {
            for (int i = 0; i < ScaledAnims.Count; i++) if (ScaledAnims[i] != null) ScaledAnims[i].localScale = ScaledFrom[i];
            ScaledAnims.Clear(); ScaledFrom.Clear();
        }

        private const float Margin = 0.03f;          // share of the screen kept free at each edge
        private const float GameCardHeight = 0.61f;  // share of the screen's height a final screen card takes at the game's FOV
        private const float GameFov = 40f;
        private const float Nearer = 0.97f;          // bottom row distance and size against the top row, so it draws over it
        private const float NearerSize = 0.95f;
        private static string _lastFit;

        /// <summary>
        /// Two rows fitted to the player's screen, the top one taking the odd card. Cards keep the game's distance, or
        /// they would go behind the pack's wrapper, and shrink through their Animation's scale.
        /// </summary>
        private static void FitToScreen(int count)
        {
            try
            {
                var cam = CSingleton<InteractionPlayerController>.Instance.m_Cam;
                var parent = _pos[0].parent;
                var ct = cam.transform;
                var gameW = new Vector3[_gameSize];
                for (int i = 0; i < _gameSize; i++) gameW[i] = PlaceAt(i);
                float depth = Vector3.Dot((gameW[0] + gameW[_gameSize - 1]) * 0.5f - ct.position, ct.forward);
                if (depth <= 0f) return;

                float cardH = GameCardHeight * Mathf.Tan(GameFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float cardW = cardH * (63f / 88f) / Mathf.Max(0.1f, cam.aspect);
                float room = 1f - 2f * Margin;
                int top = (count + 1) / 2, bottom = count - top;
                float spacing = Plugin.RowSpacing.Value;
                float scale = Mathf.Min(Plugin.CardScale.Value, room / (cardH * (1f + spacing)));
                // A row's cards may overlap by up to a tenth of a card, as the game's do
                if (top > 1) scale = Mathf.Min(scale, room / (cardW * (1f + 0.9f * (top - 1))));

                var gx = new float[_gameSize];
                for (int i = 0; i < _gameSize; i++) gx[i] = cam.WorldToViewportPoint(gameW[i]).x;
                float gMid = (gx[0] + gx[_gameSize - 1]) * 0.5f, gSpan = Mathf.Abs(gx[_gameSize - 1] - gx[0]);
                float spread = gSpan > 0f ? Mathf.Clamp((room - cardW * scale) / gSpan, 0.2f, 1f) : 1f;
                float rowGap = cardH * scale * spacing * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    bool upper = i < top;
                    int n = upper ? top : bottom, j = upper ? i : i - top;
                    float f = n == 1 ? (_gameSize - 1) * 0.5f : (float)j / (n - 1) * (_gameSize - 1);
                    int a = Mathf.Min((int)f, _gameSize - 2);
                    float k = f - a;
                    float x = 0.5f + (Mathf.Lerp(gx[a], gx[a + 1], k) - gMid) * spread;
                    float y = 0.5f + (upper ? rowGap : -rowGap);
                    var t = RevealPos[i];
                    t.position = cam.ViewportToWorldPoint(new Vector3(x, y, upper ? depth : depth * Nearer));
                    t.rotation = Quaternion.Slerp(TurnAt(a), TurnAt(a + 1), k);

                    var anim = _seq.m_CardAnimList[i].transform;
                    ScaledAnims.Add(anim);
                    ScaledFrom.Add(anim.localScale);
                    anim.localScale *= scale * (upper ? 1f : NearerSize);
                }

                string fit = $"Final screen: {count} cards at {scale:P0} size on a {cam.pixelWidth}x{cam.pixelHeight} screen";
                if (fit != _lastFit) { _lastFit = fit; Plugin.Log.LogInfo(fit); }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Final screen: " + e.Message);
            }
        }
    }
}
