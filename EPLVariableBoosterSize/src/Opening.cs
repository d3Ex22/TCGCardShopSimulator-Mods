using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VariableBoosterSize
{
    /// <summary>
    /// The game's pack opening has 7 card places (m_Card3dUIList, their animations m_CardAnimList and the final screen
    /// places m_ShowAllCardPosList) and ends the card by card reveal at the 7th card. For a pack of more cards only:
    /// - when it opens, copies of the 7th card place are added and the lists get exactly the pack's size (EPL fills as
    ///   many places as the list holds); the pile of cards in front of the pack keeps the game's depth;
    /// - the card by card reveal goes on to the last card (the "7" and "6" the game compares the card index with follow
    ///   the pack's size);
    /// - the final screen shows the cards on two rows fitted to the player's screen.
    /// Packs of 7 cards or fewer get the game's own lists back, untouched.
    /// </summary>
    internal static class Opening
    {
        private static AccessTools.FieldRef<CardOpeningSequence, Item> _item;

        private static CardOpeningSequence _seq;          // the opening the lists below belong to
        private static List<Card3dUIGroup> _ui;           // the game's own lists
        private static List<Animation> _anim;
        private static List<Transform> _pos;
        private static int _gameSize = 7;
        private static Vector3[] _pile;                   // the game's pile (local positions of its card places)

        private static readonly List<Card3dUIGroup> ExtraUi = new List<Card3dUIGroup>();
        private static readonly List<Animation> ExtraAnim = new List<Animation>();
        private static readonly List<Transform> RevealPos = new List<Transform>();

        private static int _size;                         // cards of the pack being opened, 0 when the game's lists are used
        private static bool _fitted;
        private static readonly List<Transform> ScaledAnims = new List<Transform>();
        private static readonly List<Vector3> ScaledFrom = new List<Vector3>();
        private static int _patched;

        internal static void Apply()
        {
            _item = AccessTools.FieldRefAccess<CardOpeningSequence, Item>("m_CurrentItem");
            // Before EPL's own OpenScreen prefix, which fills as many card places as the list holds.
            Plugin.Harmony.Patch(AccessTools.Method(typeof(CardOpeningSequence), "OpenScreen"),
                prefix: new HarmonyMethod(typeof(Opening), nameof(OpenPrefix)) { priority = Priority.First });
            _patched = 0;
            Plugin.Harmony.Patch(AccessTools.Method(typeof(CardOpeningSequence), "Update"),
                prefix: new HarmonyMethod(typeof(Opening), nameof(UpdatePrefix)),
                transpiler: new HarmonyMethod(typeof(Opening), nameof(UpdateTranspiler)) { priority = Priority.Last });
            if (_patched != 4) Plugin.Log.LogWarning($"pack opening: {_patched} of the game's 4 card count checks found; packs past 7 cards may end early");
        }

        internal static void Revert()
        {
            if (_seq != null && _ui != null) UseGameLists(_seq);
            Forget();
        }

        /// <summary>Destroys our card places (a new scene has new ones, or we are unloaded).</summary>
        private static void Forget()
        {
            foreach (var ui in ExtraUi) if (ui != null) UnityEngine.Object.Destroy(ui.gameObject);
            foreach (var t in RevealPos) if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            ExtraUi.Clear(); ExtraAnim.Clear(); RevealPos.Clear(); ScaledAnims.Clear(); ScaledFrom.Clear();
            _seq = null; _ui = null; _anim = null; _pos = null; _pile = null; _size = 0; _fitted = false;
        }

        // ---- Card by card reveal: "index >= 7" ends it, "index < 6" means "not the last card yet" ----

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

        // 7 -> the pack's size, 6 -> one less. Anything else (other mods' shorter packs) passes through.
        private static int Fit(int game, CardOpeningSequence seq) => _size > 0 && seq == _seq ? _size - (_gameSize - game) : game;

        // ---- Opening a pack ----

        private static void OpenPrefix(CardOpeningSequence __instance)
        {
            try
            {
                var item = _item(__instance);
                int size = item != null ? Generators.SizeOf(item.GetItemType()) : 0;
                if (size > __instance.m_Card3dUIList.Count || _size > 0) Use(__instance, size);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("pack opening: " + e);
                try { if (_seq == __instance) UseGameLists(__instance); } catch { }
            }
        }

        /// <summary>Keeps the game's own lists of this opening (once per scene).</summary>
        private static void Know(CardOpeningSequence seq)
        {
            // A new scene: the old opening is destroyed (compare the references, Unity's == treats it as null).
            if (!ReferenceEquals(_seq, null) && _seq != seq) Forget();
            if (_seq != null) return;
            _seq = seq;
            _ui = seq.m_Card3dUIList; _anim = seq.m_CardAnimList; _pos = seq.m_ShowAllCardPosList;
            _gameSize = Math.Min(_ui.Count, Math.Min(_anim.Count, _pos.Count));
        }

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
            if (size <= _gameSize) { UseGameLists(seq); return; }

            Grow(size - _gameSize);
            var ui = new List<Card3dUIGroup>(_ui.GetRange(0, _gameSize));
            var anim = new List<Animation>(_anim.GetRange(0, _gameSize));
            for (int i = 0; i < size - _gameSize; i++) { ui.Add(ExtraUi[i]); anim.Add(ExtraAnim[i]); }
            MakeRevealPos(size);
            // Until the final screen fits them to the screen: one row over the game's places.
            for (int i = 0; i < size; i++)
            {
                float f = (float)i / (size - 1) * (_gameSize - 1);
                int a = Mathf.Min((int)f, _gameSize - 2);
                RevealPos[i].position = Vector3.Lerp(_pos[a].position, _pos[a + 1].position, f - a);
                RevealPos[i].rotation = Quaternion.Slerp(_pos[a].rotation, _pos[a + 1].rotation, f - a);
            }
            Repile(ui);
            seq.m_Card3dUIList = ui;
            seq.m_CardAnimList = anim;
            seq.m_ShowAllCardPosList = RevealPos.GetRange(0, size);
            _size = size;
            _fitted = false;
        }

        /// <summary>The game's own lists and pile back; our card places hidden.</summary>
        private static void UseGameLists(CardOpeningSequence seq)
        {
            Unscale();
            if (_pile != null)
                for (int i = 0; i < _gameSize; i++) if (_ui[i] != null) _ui[i].transform.localPosition = _pile[i];
            // The game and EPL only hide the cards of the list when a pack opens: ours would stay on screen.
            foreach (var extra in ExtraUi) if (extra != null && extra.gameObject.activeSelf) extra.gameObject.SetActive(false);
            seq.m_Card3dUIList = _ui; seq.m_CardAnimList = _anim; seq.m_ShowAllCardPosList = _pos;
            _size = 0;
        }

        // The cards wait in a pile in front of the pack, each a little behind the one before; behind the 7th is the pack
        // itself. A bigger pile is spread over the game's pile depth (local to the opening group, which follows the player).
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

        /// <summary>Adds card places (copies of the game's last one) until there are <paramref name="extra"/> of them.</summary>
        private static void Grow(int extra)
        {
            var lastUi = _ui[_gameSize - 1];
            var lastAnim = _anim[_gameSize - 1];
            while (ExtraUi.Count < extra)
            {
                // The card place's root is its Card3dUIGroup, its Animation is inside it.
                var go = UnityEngine.Object.Instantiate(lastUi.gameObject, lastUi.transform.parent);
                go.name = lastUi.name + "_Vbs" + (_gameSize + ExtraUi.Count + 1);
                var ui = go.GetComponent<Card3dUIGroup>();
                var anim = lastAnim.transform.IsChildOf(lastUi.transform) ? Find(go.transform, lastUi.transform, lastAnim.transform).GetComponent<Animation>() : null;
                if (anim == null)
                {
                    UnityEngine.Object.Destroy(go);
                    throw new InvalidOperationException("the game's card place is not built as expected (its Animation is not inside it)");
                }
                // A card links its UI to itself in Awake, which a copy of an inactive card has not run yet: without the
                // link EPL cannot put the set's card back on it.
                if (ui.m_CardUI != null) ui.m_CardUI.InitCard3dUIGroup(ui);
                go.SetActive(false);
                ExtraUi.Add(ui);
                ExtraAnim.Add(anim);
            }
        }

        // The copy of `target`, found under `copyRoot` by its path under `root`.
        private static Transform Find(Transform copyRoot, Transform root, Transform target)
        {
            var path = new List<int>();
            for (var t = target; t != root; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            var c = copyRoot;
            foreach (int i in path) c = c.GetChild(i);
            return c;
        }

        // ---- Final screen ----

        private static void UpdatePrefix(CardOpeningSequence __instance)
        {
            if (_size == 0 && !_fitted) return;   // no pack of ours: the game's frame, untouched
            if (__instance != _seq) return;
            int state = __instance.m_StateIndex;
            if (state >= 7 && state <= 10)
            {
                if (!_fitted && _size > 0) { _fitted = true; FitToScreen(_size); }
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

        private const float Margin = 0.03f;          // free space kept at each screen edge (share of the screen)
        private const float GameCardHeight = 0.61f;  // a card on the game's final screen: share of the screen's height (measured, 40° view)
        private const float GameFov = 40f;
        private const float Nearer = 0.97f;          // bottom row: distance from the camera, as a share of the top row's
        private const float NearerSize = 0.95f;      // and size, so both rows look alike (tuned in game)
        private static string _lastFit;

        /// <summary>
        /// Lays the cards out on two rows (the top one gets the extra card of an odd count) for the player's screen,
        /// seen through the player's camera when the final screen starts: card size CardScale, smaller if both rows do
        /// not fit the screen's height or a row its width; each row follows the game's places across the screen,
        /// narrowed to fit, centred. The cards stay at the game's distance (further back they go behind the pack's
        /// wrapper) and get smaller through their Animation's scale; the bottom row sits a little nearer to be drawn
        /// over the top one where they overlap.
        /// </summary>
        private static void FitToScreen(int count)
        {
            try
            {
                var cam = CSingleton<InteractionPlayerController>.Instance.m_Cam;
                var parent = _pos[0].parent;
                var ct = cam.transform;
                var gameW = new Vector3[_gameSize];
                for (int i = 0; i < _gameSize; i++) gameW[i] = _pos[i].position;
                float depth = Vector3.Dot((gameW[0] + gameW[_gameSize - 1]) * 0.5f - ct.position, ct.forward);
                if (depth <= 0f) return;

                float cardH = GameCardHeight * Mathf.Tan(GameFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float cardW = cardH * (63f / 88f) / Mathf.Max(0.1f, cam.aspect);
                float room = 1f - 2f * Margin;
                int top = (count + 1) / 2, bottom = count - top;
                float spacing = Plugin.RowSpacing.Value;
                // The field of view is vertical: the height fit is the same on every screen shape.
                float scale = Mathf.Min(Plugin.CardScale.Value, room / (cardH * (1f + spacing)));
                // Width: a row's cards may overlap a little, as the game's own do, never more than a tenth of a card.
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
                    t.rotation = Quaternion.Slerp(_pos[a].rotation, _pos[a + 1].rotation, k);

                    var anim = _seq.m_CardAnimList[i].transform;
                    ScaledAnims.Add(anim);
                    ScaledFrom.Add(anim.localScale);
                    anim.localScale *= scale * (upper ? 1f : NearerSize);
                }

                string fit = $"final screen: packs of {count} cards at {scale:P0} size, rows at {spread:P0} of the game's width ({cam.pixelWidth}x{cam.pixelHeight})";
                if (fit != _lastFit) { _lastFit = fit; Plugin.Log.LogInfo(fit); }
            }
            catch (Exception e)
            {
                // The game's places stay where they were laid (one row would overflow): the screen still works.
                Plugin.Log.LogWarning("final screen: " + e.Message);
            }
        }
    }
}
