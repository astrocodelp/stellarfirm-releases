using System.Text;
using Emberfall.Core;
using Emberfall.Data;
using UnityEngine;

namespace Emberfall.Gameplay
{
    /// <summary>
    /// Graybox HUD on IMGUI: labelled bars (numbers always shown, never colour
    /// alone), event log, pause menu, inventory, attribute screen, death screen,
    /// save/load. Every action has a keyboard shortcut shown on its button.
    /// The shipping UI replaces this with Input System + TextMeshPro; the
    /// accessibility rules (text scale, labels, no colour-only meaning) carry over.
    /// </summary>
    public sealed class GrayboxHud : MonoBehaviour
    {
        private GameSession _session;
        private GrayboxBootstrap _bootstrap;
        private GUIStyle _label;
        private GUIStyle _button;
        private GUIStyle _panel;
        private GUIStyle _big;
        private readonly StringBuilder _sb = new StringBuilder(256);
        private readonly System.Collections.Generic.Queue<string> _log = new System.Collections.Generic.Queue<string>();
        private bool _showInventory;
        private bool _showAttributes;
        private int _inventorySelection;
        private float _uiScale = -1f;
        private Texture2D _panelTex;
        private Font _runtimeFont;
        private string _toast;
        private float _toastTimer;

        public bool IsPaused { get; private set; }

        public void Bind(GameSession session, GrayboxBootstrap bootstrap)
        {
            _session = session;
            _bootstrap = bootstrap;
            _session.Log += OnLog;
        }

        private void OnLog(string line)
        {
            _log.Enqueue(line);
            while (_log.Count > 5) _log.Dequeue();
        }

        private void Update()
        {
            if (_session == null) return;
            if (_toastTimer > 0f) _toastTimer -= Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                IsPaused = !IsPaused;
                _showInventory = false;
                _showAttributes = false;
                Time.timeScale = IsPaused ? 0f : 1f;
            }
            if (IsPaused) return;

            if (Input.GetKeyDown(KeyCode.Tab)) { _showInventory = !_showInventory; _showAttributes = false; }
            if (Input.GetKeyDown(KeyCode.C)) { _showAttributes = !_showAttributes; _showInventory = false; }
            if (Input.GetKeyDown(KeyCode.F5)) _session.SaveGame(_session.SaveSlot);
            if (Input.GetKeyDown(KeyCode.F9)) TryLoad(_session.SaveSlot);

            if (_session.Player.IsAlive && !_showAttributes && !_showInventory)
            {
                TrySkillKey(KeyCode.Alpha1, 0);
                TrySkillKey(KeyCode.Alpha2, 1);
                TrySkillKey(KeyCode.Alpha3, 2);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                _session.RespawnPlayer();
            }

            if (_showInventory) HandleInventoryKeys();
            if (_showAttributes) HandleAttributeKeys();
        }

        private void TrySkillKey(KeyCode key, int index)
        {
            if (!Input.GetKeyDown(key)) return;
            var skills = _session.Database != null ? _session.Database.Skills : null;
            if (skills == null || index >= skills.Count) return;
            if (!_session.Player.TryCastSkill(skills[index], _session.SkillCooldowns, index))
                Toast(skills[index].DisplayName + " not ready");
        }

        private void HandleInventoryKeys()
        {
            var items = _session.Inventory.Items;
            if (items.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.DownArrow)) _inventorySelection = (_inventorySelection + 1) % items.Count;
            if (Input.GetKeyDown(KeyCode.UpArrow)) _inventorySelection = (_inventorySelection - 1 + items.Count) % items.Count;
            if (Input.GetKeyDown(KeyCode.Return)) EquipSelected();
        }

        private void HandleAttributeKeys()
        {
            if (_session.UnspentPoints <= 0) return;
            AttributeKind kind;
            if (Input.GetKeyDown(KeyCode.Alpha1)) kind = AttributeKind.Strength;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) kind = AttributeKind.Dexterity;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) kind = AttributeKind.Vigor;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) kind = AttributeKind.Focus;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) kind = AttributeKind.Will;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) kind = AttributeKind.Luck;
            else return;
            var attrs = _session.Player.Attributes;
            int points = _session.UnspentPoints;
            if (Progression.Spend(ref attrs, kind, ref points))
            {
                _session.Player.Attributes = attrs;
                _session.UnspentPoints = points;
                _session.Player.RecalculateDerived(false);
                _session.Ember.SetCapacity(DerivedStats.MaxEmber(attrs));
            }
        }

        private void EquipSelected()
        {
            var items = _session.Inventory.Items;
            if (items.Count == 0) return;
            _inventorySelection = Mathf.Clamp(_inventorySelection, 0, items.Count - 1);
            var item = items[_inventorySelection];
            var def = _session.Database.FindItem(item.DefinitionId);
            if (def == null) return;
            _session.Inventory.Equip(item.InstanceId, def.Slot, false);
            _session.Player.RecalculateDerived(false);
            Toast("Equipped " + def.DisplayName);
        }

        private void TryLoad(int slot)
        {
            if (_session.LoadGame(slot)) Toast("Loaded slot " + (slot + 1));
            else Toast("Slot " + (slot + 1) + " is empty");
        }

        private void Toast(string message)
        {
            _toast = message;
            _toastTimer = 2.5f;
        }

        // ---- Drawing ------------------------------------------------------

        private void EnsureStyles()
        {
            float scale = _session != null && _session.Settings != null ? Mathf.Clamp(_session.Settings.TextScale, 1f, 2f) : 1f;
            if (_label != null && Mathf.Approximately(_uiScale, scale)) return;
            _uiScale = scale;
            // Unity 6 removed the built-in Arial: IMGUI skin then has no font and
            // all text renders blank. Load the engine font once and set it below.
            if (_runtimeFont == null)
                _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * scale), wordWrap = true };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(22 * scale), fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * scale) };
            _button.normal.textColor = Color.white;
            _button.hover.textColor = Color.yellow;
            _button.focused.textColor = Color.yellow;
            _panel = new GUIStyle(GUI.skin.box);
            if (_panelTex != null) Destroy(_panelTex);
            _panelTex = MakeTex(new Color(0.07f, 0.07f, 0.1f, 0.92f));
            _panel.normal.background = _panelTex;
            if (_runtimeFont != null)
            {
                _label.font = _runtimeFont;
                _big.font = _runtimeFont;
                _button.font = _runtimeFont;
            }
        }

        private static Texture2D MakeTex(Color c)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (_session == null || _session.Player == null) return;
            EnsureStyles();
            DrawBars();
            DrawLog();
            DrawSkillBar();
            if (_toastTimer > 0f && _toast != null)
                GUI.Label(new Rect(Screen.width / 2f - 150f, Screen.height - 140f, 300f, 30f), _toast, _label);
            if (!_session.Player.IsAlive) DrawDeath();
            if (_showInventory) DrawInventory();
            if (_showAttributes) DrawAttributes();
            if (IsPaused) DrawPause();
            DrawHints();
        }

        private void DrawBars()
        {
            float w = 260f * _uiScale;
            GUILayout.BeginArea(new Rect(12f, 12f, w + 20f, 190f * _uiScale), _panel);
            GUILayout.Label("EMBERBEARER — Level " + _session.Level, _big);
            BarRow("Health", _session.Player.Health.Current, _session.Player.Health.Max);
            BarRow("Stamina", _session.Player.Combat.Stamina, _session.Player.Combat.MaxStamina);
            BarRow("Focus", _session.Player.Focus, _session.Player.MaxFocus);
            string emberState = _session.Ember.IsLow ? "EMBER LOW — the dark hunts you" : "Ember";
            BarRow(emberState, _session.Ember.Current, _session.Ember.Capacity);
            GUILayout.Label("XP " + _session.Xp + " / " + Progression.XpToNext(_session.Level)
                + (_session.UnspentPoints > 0 ? "   [C] " + _session.UnspentPoints + " points to spend" : ""), _label);
            GUILayout.EndArea();
        }

        private void BarRow(string name, float current, float max)
        {
            _sb.Length = 0;
            _sb.Append(name).Append(": ").Append(Mathf.CeilToInt(current)).Append(" / ").Append(Mathf.RoundToInt(max));
            GUILayout.Label(_sb.ToString(), _label);
            Rect r = GUILayoutUtility.GetRect(10f, 8f * _uiScale, GUILayout.ExpandWidth(true));
            GUI.Box(r, GUIContent.none);
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            GUI.Box(new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * frac, r.height - 2f), GUIContent.none, GUI.skin.button);
        }

        private void DrawLog()
        {
            float h = 26f * _uiScale * 5f + 10f;
            GUILayout.BeginArea(new Rect(12f, Screen.height - h - 40f, 380f * _uiScale, h), _panel);
            foreach (var line in _log) GUILayout.Label(line, _label);
            GUILayout.EndArea();
        }

        private void DrawSkillBar()
        {
            var skills = _session.Database != null ? _session.Database.Skills : null;
            if (skills == null) return;
            float bw = 170f * _uiScale;
            float x = Screen.width / 2f - (skills.Count * (bw + 8f)) / 2f;
            for (int i = 0; i < skills.Count && i < 3; i++)
            {
                float cd = _session.SkillCooldowns[i];
                _sb.Length = 0;
                _sb.Append('[').Append(i + 1).Append("] ").Append(skills[i].DisplayName);
                if (cd > 0f) _sb.Append(" (").Append(Mathf.CeilToInt(cd)).Append("s)");
                var rect = new Rect(x + i * (bw + 8f), Screen.height - 66f, bw, 30f * _uiScale);
                if (GUI.Button(rect, _sb.ToString(), _button))
                {
                    if (!_session.Player.TryCastSkill(skills[i], _session.SkillCooldowns, i))
                        Toast(skills[i].DisplayName + " not ready");
                }
            }
        }

        private void DrawHints()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 330f * _uiScale, 12f, 318f * _uiScale, 130f * _uiScale), _panel);
            GUILayout.Label("WASD move · J/LMB light · K/RMB heavy\nSpace dodge · Shift block/parry\n[Tab] Inventory · [C] Attributes\n[1-3] Skills · [Esc] Pause\n[F5] Save · [F9] Load", _label);
            GUILayout.EndArea();
        }

        private void DrawDeath()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 70f, 400f, 150f), _panel);
            GUILayout.Label("YOU FELL", _big);
            GUILayout.Label("Half your ember waits at your death-mark.", _label);
            if (GUILayout.Button("[R] Rise again", _button)) _session.RespawnPlayer();
            GUILayout.EndArea();
        }

        private void DrawInventory()
        {
            float w = 460f * _uiScale, h = 420f * _uiScale;
            GUILayout.BeginArea(new Rect(Screen.width - w - 16f, 150f, w, h), _panel);
            GUILayout.Label("INVENTORY  (↑/↓ select, Enter equip, Tab close)", _big);
            var items = _session.Inventory.Items;
            if (items.Count == 0) GUILayout.Label("Empty. The Hollow provides — keep fighting.", _label);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var def = _session.Database.FindItem(item.DefinitionId);
                string name = def != null ? def.DisplayName : item.DefinitionId;
                _sb.Length = 0;
                if (i == _inventorySelection) _sb.Append("> ");
                _sb.Append(name).Append(" — ").Append(RarityRules.Label(item.Rarity));
                if (IsEquipped(item)) _sb.Append(" [equipped]");
                if (GUILayout.Button(_sb.ToString(), _button)) { _inventorySelection = i; EquipSelected(); }
                if (item.Affixes.Count > 0)
                {
                    _sb.Length = 0;
                    _sb.Append("    ");
                    for (int a = 0; a < item.Affixes.Count; a++)
                    {
                        if (a > 0) _sb.Append(", ");
                        _sb.Append(item.Affixes[a].Kind).Append(" +").Append(item.Affixes[a].Value);
                    }
                    GUILayout.Label(_sb.ToString(), _label);
                }
            }
            GUILayout.EndArea();
        }

        private bool IsEquipped(ItemInstance item)
        {
            foreach (var kv in _session.Inventory.Equipped)
                if (kv.Value == item) return true;
            return _session.Inventory.SecondRing == item;
        }

        private void DrawAttributes()
        {
            float w = 380f * _uiScale, h = 330f * _uiScale;
            GUILayout.BeginArea(new Rect(16f, 220f, w, h), _panel);
            GUILayout.Label("ATTRIBUTES — " + _session.UnspentPoints + " points", _big);
            var a = _session.Player.Attributes;
            AttrRow("[1] Strength", a.Strength);
            AttrRow("[2] Dexterity", a.Dexterity);
            AttrRow("[3] Vigor", a.Vigor);
            AttrRow("[4] Focus", a.Focus);
            AttrRow("[5] Will", a.Will);
            AttrRow("[6] Luck", a.Luck);
            GUILayout.Label("Press the number to spend a point. [C] closes.", _label);
            GUILayout.EndArea();
        }

        private void AttrRow(string name, int value)
        {
            GUILayout.Label(name + ": " + value, _label);
        }

        private void DrawPause()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 190f, Screen.height / 2f - 170f, 380f, 340f), _panel);
            GUILayout.Label("PAUSED", _big);
            if (GUILayout.Button("[Esc] Resume", _button)) { IsPaused = false; Time.timeScale = 1f; }
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
            {
                int s = slot;
                bool exists = SaveSystem.SlotExists(s);
                if (GUILayout.Button("Save to slot " + (s + 1) + (exists ? " (overwrite)" : ""), _button))
                    _session.SaveGame(s);
                GUI.enabled = exists;
                if (GUILayout.Button("Load slot " + (s + 1), _button)) { TryLoad(s); IsPaused = false; Time.timeScale = 1f; }
                GUI.enabled = true;
            }
            GUILayout.Label("Text scale", _label);
            float scale = GUILayout.HorizontalSlider(_session.Settings.TextScale, 1f, 2f);
            _session.Settings.TextScale = scale;
            GUILayout.Label(_session.Settings.ToggleBlock ? "Block: toggle (CapsLock)" : "Block: hold (Shift)", _label);
            if (GUILayout.Button("Switch block mode", _button))
            {
                _session.Settings.ToggleBlock = !_session.Settings.ToggleBlock;
                _session.Player.SetToggleBlock(_session.Settings.ToggleBlock);
            }
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Log -= OnLog;
            if (_panelTex != null) Destroy(_panelTex);
            Time.timeScale = 1f;
        }
    }
}
