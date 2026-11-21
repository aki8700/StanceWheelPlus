using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace StanceWheelPlus
{
    // Extends Fontaine's Stance Overhaul (com.fontaine.spt-stance-overhaul) key+scroll input
    // from a 2-way toggle (High/Low Ready) to stepping through a configurable stance list.
    // Displays the active stance in the bottom-right AmmoCountPanel (same as ammo check).
    // Stance Overhaul targets netstandard2.1, so all members are resolved via reflection.
    [BepInPlugin("com.aki8700.stancewheelplus", "StanceWheelPlus", "1.1.0")]
    [BepInDependency("com.fontaine.spt-stance-overhaul")]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        internal static ManualLogSource Log;

        ConfigEntry<bool> _enableWheel, _wrap, _showIndicator, _showDetails, _showForKeys, _showForActiveAim;
        ConfigEntry<string> _order, _language;
        ConfigEntry<float> _cooldown;

        // Stance Overhaul reflection targets
        FieldInfo _controllerField;
        PropertyInfo _currentType;
        Type _eventsType;
        readonly Dictionary<string, MethodInfo> _raise = new Dictionary<string, MethodInfo>();
        readonly List<PropertyInfo> _keybinds = new List<PropertyInfo>();
        PropertyInfo _activeAimKey;
        FieldInfo _aimField;
        PropertyInfo _aimProp;

        AmmoCountPanel _panel;
        float _nextWheel, _showWindowUntil, _lastWheelTime;
        string _lastTarget, _lastSeen;

        readonly Dictionary<string, string> _loc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, string> ToggleOf = new Dictionary<string, string>
        {
            { "PatrolStance", "RaiseTogglePatrolStance" }, { "LowReady", "RaiseToggleLowReady" },
            { "HighReady", "RaiseToggleHighReady" }, { "ShortStock", "RaiseToggleShortStock" },
            { "ActiveAiming", "RaiseToggleActiveAim" },
        };

        static readonly Dictionary<string, Dictionary<string, string>> BuiltInLoc = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>
            {
                ["Stance.PatrolStance"] = "Patrol",
                ["Stance.LowReady"] = "Low Ready",
                ["Stance.None"] = "Neutral",
                ["Stance.HighReady"] = "High Ready",
                ["Stance.ShortStock"] = "Short Stock",
                ["Stance.ActiveAiming"] = "Active Aim",
                ["Stance.LeftShoulder"] = "Left Shoulder",
                ["Stance.PistolCompress"] = "Pistol Compress",
                ["Details.None"] = "Standard stance (shouldered) / No modifiers",
                ["Details.BlocksFiring"] = "Cancels on fire",
                ["Details.CanFire"] = "Can fire",
                ["Details.WalkSpeed"] = "Move {0}",
                ["Details.Hipfire"] = "Hipfire spread ×{0}",
                ["Details.StaminaRegen"] = "Arm stamina regen",
                ["Details.StaminaDrain"] = "Arm stamina drain",
                ["Details.TacSprint"] = "Tac sprint",
                ["Details.Separator"] = " / ",
            },
            ["ja"] = new Dictionary<string, string>
            {
                ["Stance.PatrolStance"] = "パトロール",
                ["Stance.LowReady"] = "ローレディ",
                ["Stance.None"] = "ニュートラル",
                ["Stance.HighReady"] = "ハイレディ",
                ["Stance.ShortStock"] = "ショートストック",
                ["Stance.ActiveAiming"] = "アクティブエイム",
                ["Stance.LeftShoulder"] = "左肩",
                ["Stance.PistolCompress"] = "ピストル（引きつけ）",
                ["Details.None"] = "通常の構え（肩付け）/ 補正なし",
                ["Details.BlocksFiring"] = "撃つと解除",
                ["Details.CanFire"] = "射撃可",
                ["Details.WalkSpeed"] = "移動{0}",
                ["Details.Hipfire"] = "腰だめのブレ×{0}",
                ["Details.StaminaRegen"] = "腕スタミナ回復",
                ["Details.StaminaDrain"] = "腕スタミナ消費",
                ["Details.TacSprint"] = "タクティカルスプリント可",
                ["Details.Separator"] = " / ",
            },
            ["ru"] = new Dictionary<string, string>
            {
                ["Stance.PatrolStance"] = "Патруль",
                ["Stance.LowReady"] = "Низкая готовность",
                ["Stance.None"] = "Нейтральная",
                ["Stance.HighReady"] = "Высокая готовность",
                ["Stance.ShortStock"] = "Укороченный приклад",
                ["Stance.ActiveAiming"] = "Активное прицеливание",
                ["Stance.LeftShoulder"] = "Левое плечо",
                ["Stance.PistolCompress"] = "Сжатый хват пистолета",
                ["Details.None"] = "Обычная стойка (у плеча) / Без модификаторов",
                ["Details.BlocksFiring"] = "Сброс при выстреле",
                ["Details.CanFire"] = "Можно стрелять",
                ["Details.WalkSpeed"] = "Движение {0}",
                ["Details.Hipfire"] = "Разброс от бедра ×{0}",
                ["Details.StaminaRegen"] = "Восст. стамины рук",
                ["Details.StaminaDrain"] = "Расход стамины рук",
                ["Details.TacSprint"] = "Такт. спринт",
                ["Details.Separator"] = " / ",
            },
            ["es"] = new Dictionary<string, string>
            {
                ["Stance.PatrolStance"] = "Patrulla",
                ["Stance.LowReady"] = "Listo bajo",
                ["Stance.None"] = "Neutral",
                ["Stance.HighReady"] = "Listo alto",
                ["Stance.ShortStock"] = "Culata corta",
                ["Stance.ActiveAiming"] = "Apuntado activo",
                ["Stance.LeftShoulder"] = "Hombro izquierdo",
                ["Stance.PistolCompress"] = "Pistola recogida",
                ["Details.None"] = "Postura estándar (al hombro) / Sin modificadores",
                ["Details.BlocksFiring"] = "Se cancela al disparar",
                ["Details.CanFire"] = "Puede disparar",
                ["Details.WalkSpeed"] = "Velocidad {0}",
                ["Details.Hipfire"] = "Dispersión cadera ×{0}",
                ["Details.StaminaRegen"] = "Regenera estamina brazos",
                ["Details.StaminaDrain"] = "Consume estamina brazos",
                ["Details.TacSprint"] = "Esprint táctico",
                ["Details.Separator"] = " / ",
            },
            ["zh"] = new Dictionary<string, string>
            {
                ["Stance.PatrolStance"] = "巡逻姿态",
                ["Stance.LowReady"] = "低姿戒备",
                ["Stance.None"] = "默认姿态",
                ["Stance.HighReady"] = "高姿戒备",
                ["Stance.ShortStock"] = "短托抵肩",
                ["Stance.ActiveAiming"] = "主动瞄准",
                ["Stance.LeftShoulder"] = "左肩切换",
                ["Stance.PistolCompress"] = "手枪近距收枪",
                ["Details.None"] = "常规持枪（抵肩）/ 无加成",
                ["Details.BlocksFiring"] = "开火即取消",
                ["Details.CanFire"] = "可射击",
                ["Details.WalkSpeed"] = "移速 {0}",
                ["Details.Hipfire"] = "腰射散布 ×{0}",
                ["Details.StaminaRegen"] = "恢复手臂体力",
                ["Details.StaminaDrain"] = "消耗手臂体力",
                ["Details.TacSprint"] = "可战术冲刺",
                ["Details.Separator"] = " / ",
            },
        };

        void Awake()
        {
            Instance = this;
            Log = Logger;
            const string s1 = "1. Wheel";
            const string s2 = "2. Indicator";
            const string s3 = "3. Localization";

            _enableWheel = Config.Bind(s1, "Enable Step Wheel", true,
                "Replaces Stance Overhaul's key+scroll (High/Low only) with stepping through the list below. Disable to restore original behavior.");
            _order = Config.Bind(s1, "Stance Order (Down to Up)", "PatrolStance,LowReady,None,HighReady,ShortStock",
                "Scrolling down moves left, scrolling up moves right. Valid names: PatrolStance, LowReady, None, HighReady, ShortStock, ActiveAiming");
            _wrap = Config.Bind(s1, "Wrap Around", false,
                "If enabled, scrolling past the top wraps to the bottom and vice versa.");
            _cooldown = Config.Bind(s1, "Input Cooldown (sec)", 0.12f,
                new ConfigDescription("Minimum interval between scroll steps to prevent skipping stances on fast wheel spins.", new AcceptableValueRange<float>(0f, 0.5f)));

            _showIndicator = Config.Bind(s2, "Show Stance Indicator", true,
                "Shows the current stance name in the bottom-right panel (same as ammo check) when changing stances.");
            _showDetails = Config.Bind(s2, "Show Stance Details", true,
                "Shows a one-line summary (firing, move speed, arm stamina, etc.) under the stance name.");
            _showForKeys = Config.Bind(s2, "Show On Keybind Change", true,
                "Also show the indicator when changing stances with Stance Overhaul hotkeys.");
            _showForActiveAim = Config.Bind(s2, "Show For Active Aim", false,
                "Show the indicator when entering/exiting Active Aim. Disabled by default to reduce clutter in combat.");

            _language = Config.Bind(s3, "Language", "en",
                "Display language code (built-in: en, ja, ru, es, zh). Loads lang/<code>.json next to StanceWheelPlus.dll if present, then falls back to built-in translations.");
            _language.SettingChanged += (_, __) => LoadLocalization();
            LoadLocalization();

            if (!Resolve())
            {
                Log.LogError("Could not resolve Stance Overhaul internals; disabling plugin (version mismatch?).");
                enabled = false;
                return;
            }

            var target = AccessTools.Method("StanceOverhaul.SubSystem.StanceInput.StanceInputListener:HandleScrollInput");
            new Harmony("com.aki8700.stancewheelplus").Patch(target,
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(HandleScrollPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
            Log.LogInfo($"StanceWheelPlus enabled (language: {_language.Value}).");
        }

        void LoadLocalization()
        {
            _loc.Clear();
            foreach (var kv in BuiltInLoc["en"]) _loc[kv.Key] = kv.Value;

            string code = (_language?.Value ?? "en").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(code)) code = "en";

            if (BuiltInLoc.TryGetValue(code, out var builtIn))
            {
                foreach (var kv in builtIn) _loc[kv.Key] = kv.Value;
            }

            try
            {
                string dir = Path.GetDirectoryName(Info.Location);
                if (!string.IsNullOrEmpty(dir))
                {
                    string file = Path.Combine(dir, "lang", code + ".json");
                    if (File.Exists(file))
                    {
                        var parsed = ParseFlatJson(File.ReadAllText(file, Encoding.UTF8));
                        foreach (var kv in parsed) _loc[kv.Key] = kv.Value;
                        Log?.LogInfo($"Loaded localization file: {file} ({parsed.Count} keys)");
                    }
                    else if (!BuiltInLoc.ContainsKey(code))
                    {
                        Log?.LogWarning($"Localization '{code}' not found in built-in or {file}; using English fallback.");
                    }
                }
            }
            catch (Exception e)
            {
                Log?.LogWarning($"Failed to load localization file for '{code}': {e.Message}");
            }
        }

        string T(string key, string fallback = null)
        {
            return _loc.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val) ? val : (fallback ?? key);
        }

        // Minimal flat JSON object parser ({"key": "value", ...}) to avoid external dependencies.
        static Dictionary<string, string> ParseFlatJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json)) return result;

            int i = 0, n = json.Length;
            while (i < n)
            {
                SkipWs(json, ref i);
                if (i >= n) break;
                if (json[i] == '{' || json[i] == ',' || json[i] == '}') { i++; continue; }
                if (json[i] != '"') { i++; continue; }

                string key = ReadJsonString(json, ref i);
                SkipWs(json, ref i);
                if (i < n && json[i] == ':') i++;
                SkipWs(json, ref i);
                if (i < n && json[i] == '"')
                {
                    string val = ReadJsonString(json, ref i);
                    if (!string.IsNullOrEmpty(key)) result[key] = val;
                }
            }
            return result;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static string ReadJsonString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return string.Empty;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length && int.TryParse(s.Substring(i, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        bool Resolve()
        {
            var tPlugin = AccessTools.TypeByName("StanceOverhaul.Plugin");
            _controllerField = tPlugin != null ? AccessTools.Field(tPlugin, "StanceControllerInstance") : null;
            var tController = AccessTools.TypeByName("StanceOverhaul.Controllers.StanceController");
            _currentType = tController != null ? AccessTools.Property(tController, "CurrentStanceType") : null;
            _eventsType = AccessTools.TypeByName("StanceOverhaul.SubSystem.StanceInput.StanceInputEvents");
            if (_controllerField == null || _currentType == null || _eventsType == null) return false;
            foreach (var m in ToggleOf.Values)
            {
                var mi = AccessTools.Method(_eventsType, m);
                if (mi == null) return false;
                _raise[m] = mi;
            }
            var tCfg = AccessTools.TypeByName("StanceOverhaul.PluginConfig");
            if (tCfg != null)
            {
                foreach (var n in new[] { "HighReadyKeybind", "LowReadyKeybind", "ShortStockKeybind", "PatrolKeybind" })
                {
                    var p = AccessTools.Property(tCfg, n);
                    if (p != null) _keybinds.Add(p);
                }
                _activeAimKey = AccessTools.Property(tCfg, "ActiveAimKeybind");
            }
            var tCommon = AccessTools.TypeByName("RealismCommonLib.Plugin");
            if (tCommon != null)
            {
                _aimField = AccessTools.Field(tCommon, "AimStateInstance");
                if (_aimField == null) _aimProp = AccessTools.Property(tCommon, "AimStateInstance");
            }
            return true;
        }

        object Controller => _controllerField.GetValue(null);

        string Current()
        {
            var c = Controller;
            return c == null ? null : _currentType.GetValue(c, null)?.ToString();
        }

        bool IsAiming()
        {
            try
            {
                object aim = _aimField != null ? _aimField.GetValue(null) : _aimProp?.GetValue(null, null);
                return aim != null && Traverse.Create(aim).Property("IsAiming").GetValue<bool>();
            }
            catch { return false; }
        }

        // Returning false skips the original HandleScrollInput (2-way High/Low toggle).
        static bool HandleScrollPrefix(float direction)
        {
            var p = Instance;
            if (p == null || !p._enableWheel.Value) return true;
            try { p.Step(direction > 0f ? 1 : -1); }
            catch (Exception e) { Log.LogError("Step wheel failed: " + e); }
            return false;
        }

        void Step(int dir)
        {
            if (Time.unscaledTime < _nextWheel) return;
            _nextWheel = Time.unscaledTime + _cooldown.Value;

            var order = _order.Value.Split(',').Select(x => x.Trim()).Where(x => x == "None" || ToggleOf.ContainsKey(x)).Distinct().ToList();
            if (order.Count < 2) return;

            // CurrentStanceType lags slightly during transitions; anchor on the last selected stance.
            string cur = (Time.unscaledTime - _lastWheelTime < 0.6f && _lastTarget != null) ? _lastTarget : Current();
            int i = order.IndexOf(cur ?? "None");
            if (i < 0) i = Math.Max(0, order.IndexOf("None"));
            int j = i + dir;
            if (j < 0 || j >= order.Count)
            {
                if (!_wrap.Value) return;
                j = (j + order.Count) % order.Count;
            }
            string target = order[j];
            GoTo(target, Current());
            _lastTarget = target;
            _lastWheelTime = Time.unscaledTime;
            Show(target);
            _lastSeen = target;
        }

        void GoTo(string target, string current)
        {
            if (target == current) return;
            if (target == "None")
            {
                // Re-invoking the current stance's toggle event exits to Neutral.
                if (current != null && ToggleOf.TryGetValue(current, out var ev)) _raise[ev].Invoke(null, null);
                return;
            }
            _raise[ToggleOf[target]].Invoke(null, null);
        }

        void Update()
        {
            if (!_showIndicator.Value || Controller == null) return;
            if (_showForKeys.Value && AnyStanceKeyDown()) _showWindowUntil = Time.unscaledTime + 1.0f;
            if (_showForActiveAim.Value && KeyDownOrUp(_activeAimKey)) _showWindowUntil = Time.unscaledTime + 1.0f;

            string cur = Current();
            if (cur == null || cur == _lastSeen) return;
            string prev = _lastSeen;
            _lastSeen = cur;
            if (Time.unscaledTime > _showWindowUntil) return;
            if (cur == "None" && IsAiming()) return;
            if ((cur == "ActiveAiming" || prev == "ActiveAiming") && !_showForActiveAim.Value) return;
            if (!_loc.ContainsKey("Stance." + cur)) return;
            Show(cur);
        }

        bool AnyStanceKeyDown()
        {
            foreach (var p in _keybinds)
            {
                try
                {
                    var entry = p.GetValue(null, null) as ConfigEntry<KeyboardShortcut>;
                    if (entry != null && entry.Value.MainKey != KeyCode.None && entry.Value.IsDown()) return true;
                }
                catch { }
            }
            return false;
        }

        static bool KeyDownOrUp(PropertyInfo p)
        {
            try
            {
                var entry = p?.GetValue(null, null) as ConfigEntry<KeyboardShortcut>;
                if (entry == null || entry.Value.MainKey == KeyCode.None) return false;
                return Input.GetKeyDown(entry.Value.MainKey) || Input.GetKeyUp(entry.Value.MainKey);
            }
            catch { return false; }
        }

        void Show(string stance)
        {
            if (!_showIndicator.Value) return;
            try
            {
                var panel = Panel();
                if (panel == null) return;
                string name = T("Stance." + stance, stance);
                panel.Show(name, _showDetails.Value ? Details(stance) : null);
            }
            catch (Exception e) { Log.LogWarning("Failed to show stance indicator: " + e.Message); }
        }

        AmmoCountPanel Panel()
        {
            if (_panel != null) return _panel;
            _panel = Resources.FindObjectsOfTypeAll<AmmoCountPanel>().FirstOrDefault(x => x != null && x.gameObject.scene.IsValid());
            return _panel;
        }

        string Details(string stance)
        {
            if (stance == "None") return T("Details.None");
            var c = Controller;
            if (c == null) return null;
            string propName = stance == "ActiveAiming" ? "ActiveAim" : stance;
            var sp = AccessTools.Property(c.GetType(), propName);
            var s = sp?.GetValue(c, null);
            if (s == null) return null;
            var t = Traverse.Create(s);
            var parts = new List<string>();
            bool blocks = t.Property("BlocksFiring").GetValue<bool>();
            parts.Add(T(blocks ? "Details.BlocksFiring" : "Details.CanFire"));
            float walk = t.Property("WalkSpeedBonus").GetValue<float>();
            if (Math.Abs(walk - 1f) > 0.001f) parts.Add(string.Format(T("Details.WalkSpeed"), Pct(walk)));
            float hip = t.Property("HipfireBonus").GetValue<float>();
            if (Math.Abs(hip - 1f) > 0.001f) parts.Add(string.Format(T("Details.Hipfire"), hip.ToString("0.##")));
            string stamina = t.Property("StaminaMode").GetValue()?.ToString();
            if (stamina == "Regen") parts.Add(T("Details.StaminaRegen"));
            else if (stamina == "Drain") parts.Add(T("Details.StaminaDrain"));
            if (t.Property("CanDoTacSprint").GetValue<bool>()) parts.Add(T("Details.TacSprint"));
            return string.Join(T("Details.Separator", " / "), parts.ToArray());
        }

        static string Pct(float m)
        {
            float d = (m - 1f) * 100f;
            return (d >= 0 ? "+" : "") + d.ToString("0.#") + "%";
        }
    }
}
