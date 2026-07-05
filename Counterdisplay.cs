using HarmonyLib;
using TMPro;
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
        private static readonly Color32 PlusMinusColor = new Color32(96, 255, 78, 255);
        private static readonly Color32 XColor = new Color32(77, 204, 255, 255);

        private static readonly string PlusMinusHex = ColorUtility.ToHtmlStringRGB(PlusMinusColor);
        private static readonly string XColorHex = ColorUtility.ToHtmlStringRGB(XColor);

        private static string _cachedSpace = "";
        private static int _lastSpacing = -1;

        private static Vector2 _lastAppliedPos = Vector2.zero;
        private static int _lastAppliedFontSize = -1;

        private static GameObject _canvasObj;
        private static TMP_Text _text;
        private static TMP_FontAsset _cachedFont;

        private static readonly StringBuilder _counterBuilder = new StringBuilder(128);


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

            var mat = new Material(tmp.fontSharedMaterial);
            mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Outline);
            mat.DisableKeyword(TMPro.ShaderUtilities.Keyword_Underlay);
            tmp.fontSharedMaterial = mat;

            return tmp;
        }

        public static void ApplyFont()
        {
            try
            {
                _cachedFont = FindFont();
                if (_cachedFont == null)
                {
                    return;
                }
                if (_text != null) _text.font = _cachedFont;
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] ApplyFont error: {ex}");
            }
        }

        private static TMP_FontAsset FindFont()
        {
            try
            {
                var fontTMP = RDString.fontData.fontTMP;
                if (fontTMP != null)
                {
                    return fontTMP;
                }
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] RDString.fontData error: {ex}");
            }

            foreach (var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (t == null || t.font == null) continue;
                if (ReferenceEquals(t, _text)) continue;
                if (t.font.name.IndexOf("Liberation", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return t.font;
            }
            return null;
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
            sb.Append(PlusMinusHex);
            sb.Append(">+");
            AppendInt(sb, AccuracyState.PlusPerfectCount);
            sb.Append("</color>");
            sb.Append(space);
            sb.Append("<color=#");
            sb.Append(XColorHex);
            sb.Append(">");
            AppendInt(sb, AccuracyState.XPerfectCount);
            sb.Append("</color>");
            sb.Append(space);
            sb.Append("<color=#");
            sb.Append(PlusMinusHex);
            sb.Append(">-");
            AppendInt(sb, AccuracyState.MinusPerfectCount);
            sb.Append("</color>");

            _text.text = sb.ToString();

            ApplySettings();
        }

        private static void AppendInt(StringBuilder sb, int value)
        {
            if (value >= 1000)
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
                _cachedSpace = new string(' ', s);
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