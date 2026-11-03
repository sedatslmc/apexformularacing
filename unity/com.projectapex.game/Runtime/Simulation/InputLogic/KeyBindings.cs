using System;
using System.Collections.Generic;
using System.Text;

namespace Apex.InputLogic
{
    /// <summary>
    /// Player key assignment for the keyboard (handling brief 2026-11, section 4: "Die Steuerungseinstellungen sollen
    /// individuell anpassbar sein"). Each action has two slots; keys are stored by the names of the Input System's
    /// Key enum ("W", "UpArrow", "LeftShift" …) so this stays engine-free and testable. Assigning a key that another
    /// slot already uses swaps the two, so no key ever drives two actions and nothing gets lost. Escape, Enter, the
    /// function keys and the digit keys stay fixed (pause, menus, help/telemetry, team radio).
    /// Serialised into SettingsData.keyBindings ("Throttle=W,UpArrow;…"; empty = defaults).
    /// </summary>
    public sealed class KeyBindings
    {
        public enum Action { Throttle, Brake, SteerLeft, SteerRight, ShiftUp, ShiftDown, Camera, Radio, Pit, ResetCar, Tower, Map, HudPreset }

        public const int Slots = 2;
        public static readonly Action[] All = (Action[])Enum.GetValues(typeof(Action));

        static readonly Dictionary<Action, string[]> Defaults = new Dictionary<Action, string[]>
        {
            [Action.Throttle] = new[] { "W", "UpArrow" },
            [Action.Brake] = new[] { "S", "DownArrow" },
            [Action.SteerLeft] = new[] { "A", "LeftArrow" },
            [Action.SteerRight] = new[] { "D", "RightArrow" },
            [Action.ShiftUp] = new[] { "E", "" },
            [Action.ShiftDown] = new[] { "Q", "" },
            [Action.Camera] = new[] { "C", "" },
            [Action.Radio] = new[] { "R", "" },
            [Action.Pit] = new[] { "P", "" },
            [Action.ResetCar] = new[] { "Backspace", "" },
            [Action.Tower] = new[] { "Tab", "" },
            [Action.Map] = new[] { "M", "" },
            [Action.HudPreset] = new[] { "H", "" },
        };

        readonly Dictionary<Action, string[]> keys = new Dictionary<Action, string[]>();

        public KeyBindings() => ResetToDefaults();

        public void ResetToDefaults()
        {
            keys.Clear();
            foreach (var kv in Defaults) keys[kv.Key] = (string[])kv.Value.Clone();
        }

        /// <summary>Key name in a slot ("" = free).</summary>
        public string Get(Action a, int slot) => keys[a][Math.Clamp(slot, 0, Slots - 1)];

        public IEnumerable<string> KeysOf(Action a)
        {
            foreach (var k in keys[a]) if (!string.IsNullOrEmpty(k)) yield return k;
        }

        /// <summary>Keys that keep their fixed meaning and cannot be assigned.</summary>
        public static bool IsReserved(string key)
        {
            if (string.IsNullOrEmpty(key)) return true;
            if (key == "Escape" || key == "Enter" || key == "NumpadEnter") return true;
            if (key.Length >= 2 && key[0] == 'F' && int.TryParse(key.Substring(1), out int f) && f >= 1 && f <= 12) return true;
            if (key.StartsWith("Digit")) return true;
            return false;
        }

        /// <summary>
        /// Puts <paramref name="key"/> into a slot. A slot elsewhere that had this key gets this slot's previous key
        /// (a swap). Returns false for reserved keys (nothing changes).
        /// </summary>
        public bool Assign(Action a, int slot, string key)
        {
            if (IsReserved(key)) return false;
            slot = Math.Clamp(slot, 0, Slots - 1);
            string previous = keys[a][slot];
            foreach (var kv in keys)
                for (int s = 0; s < Slots; s++)
                    if (kv.Value[s] == key && !(kv.Key == a && s == slot))
                        kv.Value[s] = previous;
            keys[a][slot] = key;
            return true;
        }

        /// <summary>Frees a slot.</summary>
        public void Clear(Action a, int slot) => keys[a][Math.Clamp(slot, 0, Slots - 1)] = "";

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var a in All)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(a).Append('=').Append(keys[a][0]).Append(',').Append(keys[a][1]);
            }
            return sb.ToString();
        }

        /// <summary>Reads a serialised assignment; unknown actions and reserved keys are ignored, missing actions keep their defaults.</summary>
        public static KeyBindings Parse(string text)
        {
            var b = new KeyBindings();
            if (string.IsNullOrWhiteSpace(text)) return b;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0 || !Enum.TryParse(part.Substring(0, eq).Trim(), out Action a)) continue;
                var ks = part.Substring(eq + 1).Split(',');
                for (int s = 0; s < Slots; s++)
                {
                    string k = s < ks.Length ? ks[s].Trim() : "";
                    b.keys[a][s] = IsReserved(k) ? "" : k;
                }
            }
            // A hand-edited file could list a key twice: keep its first use only.
            var seen = new HashSet<string>();
            foreach (var a in All)
                for (int s = 0; s < Slots; s++)
                {
                    string k = b.keys[a][s];
                    if (k == "") continue;
                    if (!seen.Add(k)) b.keys[a][s] = "";
                }
            return b;
        }

        /// <summary>German menu name of an action.</summary>
        public static string ActionName(Action a)
        {
            switch (a)
            {
                case Action.Throttle: return "Gas";
                case Action.Brake: return "Bremse";
                case Action.SteerLeft: return "Lenken links";
                case Action.SteerRight: return "Lenken rechts";
                case Action.ShiftUp: return "Hochschalten";
                case Action.ShiftDown: return "Runterschalten";
                case Action.Camera: return "Kamera";
                case Action.Radio: return "Teamfunk";
                case Action.Pit: return "Boxenstopp";
                case Action.ResetCar: return "Auto zurücksetzen";
                case Action.Tower: return "Timing-Tower";
                case Action.Map: return "Minimap";
                default: return "HUD-Variante";
            }
        }

        /// <summary>Readable key name for the menu.</summary>
        public static string KeyName(string key)
        {
            switch (key)
            {
                case "": return "–";
                case "UpArrow": return "↑";
                case "DownArrow": return "↓";
                case "LeftArrow": return "←";
                case "RightArrow": return "→";
                case "Space": return "Leertaste";
                case "Backspace": return "Rücktaste";
                case "Tab": return "Tab";
                case "LeftShift": return "Umschalt links";
                case "RightShift": return "Umschalt rechts";
                case "LeftCtrl": return "Strg links";
                case "RightCtrl": return "Strg rechts";
                case "LeftAlt": return "Alt links";
                case "RightAlt": return "Alt Gr";
            }
            if (key.StartsWith("Numpad")) return "Num " + key.Substring(6);
            return key;
        }

        /// <summary>"W / ↑" for a menu row.</summary>
        public string Describe(Action a)
        {
            string k0 = keys[a][0], k1 = keys[a][1];
            if (k1 == "") return KeyName(k0);
            if (k0 == "") return KeyName(k1);
            return KeyName(k0) + " / " + KeyName(k1);
        }
    }
}
