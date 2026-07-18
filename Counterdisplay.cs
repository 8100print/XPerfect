using HarmonyLib;
using TMPro;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;
using System;
using System.Collections;
using System.Text;

namespace XPerfect
{
    public static class CounterDisplay
    {
        private static Material _cachedMaterial;

        private static string _cachedSpace = "";
        private static int _lastSpacing = -1;

        private static Vector2 _lastAppliedPos = Vector2.zero;
        private static int _lastAppliedFontSize = -1;

        private static GameObject _canvasObj;
        private static TMP_Text _text;
        private static TMP_FontAsset _cachedFont;
        private static Font _sourceFont;


        private static readonly StringBuilder _counterBuilder = new StringBuilder(64);


        public static void Create()
        {
            if (_canvasObj != null) return;

            _canvasObj = new GameObject("XPerfect_Canvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvasObj);

            var canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                Screen.width,
                Screen.height
            );

            _text = CreateLabel(_canvasObj);

            if (_cachedFont != null)
                _text.font = _cachedFont;
        }

        public static void Destroy()
        {
            if (_canvasObj == null) return;
            UnityEngine.Object.Destroy(_canvasObj);
            _canvasObj = null;
            _text = null;

            if (_cachedFont != null)
            {
                UnityEngine.Object.Destroy(_cachedFont);
                _cachedFont = null;
            }
            if (_sourceFont != null)
            {
                UnityEngine.Object.Destroy(_sourceFont);
                _sourceFont = null;
            }
            if (_cachedMaterial != null)
            {
                try { UnityEngine.Object.Destroy(_cachedMaterial); } catch { }
                _cachedMaterial = null;
            }
        }


        private static TMP_Text CreateLabel(GameObject parent)
        {
            var go = new GameObject("CounterText");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, 100f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = Main.Settings.CounterFontSize;
            tmp.text = "0 0 0";
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TMPro.TextOverflowModes.Overflow;

            if (_cachedMaterial != null)
            {
                try { UnityEngine.Object.Destroy(_cachedMaterial); } catch { }
                _cachedMaterial = null;
            }

            var mat = new Material(tmp.fontSharedMaterial);
            _cachedMaterial = mat;
            mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Outline);
            mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Underlay);

            try
            {
                mat.SetFloat("_OutlineWidth", 0f); mat.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0f));
            }
            catch { }
            tmp.fontSharedMaterial = mat;

            return tmp;
        }

        public static void ApplyFont()
        {
            try
            {
                _cachedFont = LoadFont();
                if (_cachedFont == null)
                {
                    return;
                }
                if (_text != null)
                {
                    _text.font = _cachedFont;

                    try
                    {
                        if (_cachedMaterial != null)
                        {
                            try { UnityEngine.Object.Destroy(_cachedMaterial); } catch { }
                            _cachedMaterial = null;
                        }

                        var mat = new Material(_text.fontSharedMaterial);
                        mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Outline);
                        mat.SetFloat("_OutlineWidth", 0f);
                        mat.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0f));
                        _cachedMaterial = mat;
                        _text.fontSharedMaterial = mat;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] ApplyFont error: {ex}");
            }
        }

        private static TMP_FontAsset LoadFont()
        {
            string path = Path.Combine(UnityModManager.modsPath, "XPerfect", "Maplestory OTF Bold.otf");

            try
            {
                if (_cachedFont != null)
                {
                    UnityEngine.Object.Destroy(_cachedFont);
                    _cachedFont = null;
                }
                if (_sourceFont != null)
                {
                    UnityEngine.Object.Destroy(_sourceFont);
                    _sourceFont = null;
                }

                if (!File.Exists(path))
                {
                    UnityModManager.Logger.Log($"[XPerfect] font not found: {path}");
                    return null;
                }

                _sourceFont = new Font(path);
                var asset = TMP_FontAsset.CreateFontAsset(_sourceFont);
                asset.isMultiAtlasTexturesEnabled = true;
                return asset;
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] LoadFont error: {ex}");
                return null;
            }
        }

        public static void ApplySettings()
        {
            if (_text == null) return;

            Vector2 pos = new Vector2(Main.Settings.CounterX, Main.Settings.CounterY);
            if (pos != _lastAppliedPos)
            {
                _text.rectTransform.anchoredPosition = pos;
                _lastAppliedPos = pos;
            }

            if (Main.Settings.CounterFontSize != _lastAppliedFontSize)
            {
                _text.fontSize = Main.Settings.CounterFontSize;
                _lastAppliedFontSize = Main.Settings.CounterFontSize;
            }
        }

        public static void Refresh()
        {
            if (_canvasObj == null) return;

            var ctrl = scrController.instance;
            var cdt = scrConductor.instance;

            bool isPlaying = ctrl != null && cdt != null && !ctrl.paused && cdt.isGameWorld;

            bool visible = Main.Enabled && Main.Settings.ShowCounter && isPlaying;
            _canvasObj.SetActive(visible);
            if (!visible) return;

            string space = GetSpace();

            var sb = _counterBuilder;
            sb.Length = 0;

            sb.Append("<color=#");
            sb.Append(XPerfectColors.PlusMinusHex);
            sb.Append(">");
            AppendInt(sb, AccuracyState.PlusPerfectCount);
            sb.Append("</color>");
            sb.Append(space);
            sb.Append("<color=#");
            sb.Append(XPerfectColors.XPerfectHex);
            sb.Append(">");
            AppendInt(sb, AccuracyState.XPerfectCount);
            sb.Append("</color>");
            sb.Append(space);
            sb.Append("<color=#");
            sb.Append(XPerfectColors.PlusMinusHex);
            sb.Append(">");
            AppendInt(sb, AccuracyState.MinusPerfectCount);
            sb.Append("</color>");

            _text.text = sb.ToString();

            ApplySettings();
        }

        private static void AppendInt(StringBuilder sb, int value)
        {
            if (value >= 10000)
            {
                sb.Append((char)('0' + (value / 10000) % 10));
                sb.Append((char)('0' + (value / 1000) % 10));
                sb.Append((char)('0' + (value / 100) % 10));
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            }
            else if (value >= 1000)
            {
                sb.Append((char)('0' + (value / 1000) % 10));
                sb.Append((char)('0' + (value / 100) % 10));
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            }
            else if (value >= 100)
            {
                sb.Append((char)('0' + (value / 100) % 10));
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            }
            else if (value >= 10)
            {
                sb.Append((char)('0' + (value / 10) % 10));
                sb.Append((char)('0' + value % 10));
            }
            else
            {
                sb.Append((char)('0' + value));
            }
        }

        private static string GetSpace()
        {
            int s = Main.Settings.CounterSpacing * 2;
            if (s != _lastSpacing)
            {
                if (s <= 0)
                {
                    _cachedSpace = string.Empty;
                }
                else
                {
                    _cachedSpace = "<space=" + s.ToString() + "px>";
                }
                _lastSpacing = s;
            }
            return _cachedSpace;
        }
    }

    public class CounterRunner : MonoBehaviour
    {
        private bool _wasPaused = false;

        private void Update()
        {
            var ctrl = scrController.instance;
            if (ctrl == null) return;

            bool isPaused = ctrl.paused;
            if (isPaused != _wasPaused)
            {
                _wasPaused = isPaused;
                CounterDisplay.Refresh();
            }
        }

        private void Awake()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void OnSceneChanged(
            UnityEngine.SceneManagement.Scene _,
            UnityEngine.SceneManagement.Scene __)
        {
            StartCoroutine(DelayedRefresh());
        }

        private IEnumerator DelayedRefresh()
        {
            yield return null;
            CounterDisplay.ApplyFont();
            CounterDisplay.Refresh();
        }
    }

    [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
    public static class EditorSwitchToEditModePatch
    {
        static void Postfix()
        {
            try { CounterDisplay.Refresh(); }
            catch (Exception ex) { UnityModManager.Logger.Log($"[XPerfect] SwitchToEditMode error: {ex}"); }
        }
    }
}