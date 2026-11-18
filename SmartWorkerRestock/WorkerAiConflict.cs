using BepInEx.Bootstrap;
using UnityEngine;

namespace SmartWorkerRestock
{
    /// <summary>How Smart Worker Restock works alongside Worker AI Overhaul.</summary>
    public enum WorkerAiMode
    {
        /// <summary>Not chosen yet: the player is asked once a save is loaded.</summary>
        Ask,
        /// <summary>Smart Worker Restock leaves the workers' box and shelf choices to Worker AI Overhaul and only orders stock.</summary>
        Compatibility,
        /// <summary>Smart Worker Restock's choices override Worker AI Overhaul's.</summary>
        Override,
    }

    /// <summary>
    /// Worker AI Overhaul runs its own box and shelf search for workers. Once a save is loaded, the player chooses who
    /// makes those choices.
    /// </summary>
    internal static class WorkerAiConflict
    {
        private const string Guid = "munch.workeraioverhaul";
        private static bool? _installed;
        private static GUIStyle _text, _title;
        private static Texture2D _bg;
        private static bool _freed;

        internal static bool Installed => _installed ??= Chainloader.PluginInfos.ContainsKey(Guid);

        internal static bool YieldSteering => Installed && Plugin.Instance.WorkerAiOverhaulMode.Value == WorkerAiMode.Compatibility;

        private static bool Showing => Installed && Plugin.Instance.WorkerAiOverhaulMode.Value == WorkerAiMode.Ask && Game.InShop();

        internal static void OnGUI()
        {
            if (!Showing) return;
            // The shop view locks the cursor: free it while the window is up, the game locks it again afterwards
            _freed = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_text == null)
            {
                _bg = Solid(new Color(0.08f, 0.08f, 0.08f, 1f));
                _text = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
                _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            float w = Mathf.Min(620f, Screen.width - 40f), h = 300f;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            // The window skin is see-through: a solid backing behind it
            GUI.DrawTexture(rect, _bg);
            GUI.ModalWindow(0x5357525, rect, Draw, "Smart Worker Restock");
        }

        private static void Draw(int id)
        {
            GUILayout.Space(6);
            GUILayout.Label("Worker AI Overhaul is installed", _title);
            GUILayout.Space(8);
            GUILayout.Label("Both mods choose which box and which shelf your workers restock, so they conflict.\n\n" +
                            "Compatibility mode: Smart Worker Restock leaves these choices to Worker AI Overhaul and only keeps its automatic orders.\n\n" +
                            "Otherwise, Smart Worker Restock's choices (opened boxes first, emptiest shelves first) override Worker AI Overhaul's.", _text);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Compatibility mode", GUILayout.Height(34))) Choose(WorkerAiMode.Compatibility);
            if (GUILayout.Button("Keep Smart Worker Restock's choices", GUILayout.Height(34))) Choose(WorkerAiMode.Override);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            GUILayout.Label("You can change this later: WorkerAIOverhaulMode in the settings.", _text);
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static void Choose(WorkerAiMode mode)
        {
            Plugin.Instance.WorkerAiOverhaulMode.Value = mode;
            if (_freed) { _freed = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            Plugin.Log.LogInfo(mode == WorkerAiMode.Compatibility
                ? "Worker AI Overhaul: compatibility mode, Smart Worker Restock only orders stock."
                : "Worker AI Overhaul: Smart Worker Restock chooses the boxes and shelves.");
        }
    }
}
