using HarmonyLib;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace XPerfect
{
    public static class Main
    {
        public static XPerfectSettings Settings;
        public static string ModPath;
        public static bool Enabled { get; private set; }

        private static Harmony _harmony;
        private static GameObject _runnerGo;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Settings = UnityModManager.ModSettings.Load<XPerfectSettings>(modEntry);
            ModPath = modEntry.Path;

            _harmony = new Harmony(modEntry.Info.Id);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUnload = OnUnload;

            Enabled = true;
            AccuracyState.Reset();

            _runnerGo = new GameObject("XPerfect_Runner");
            UnityEngine.Object.DontDestroyOnLoad(_runnerGo);
            var runner = _runnerGo.AddComponent<CounterRunner>();
            runner.StartCoroutine(InitialCreate(runner));

            return true;
        }

        private static IEnumerator InitialCreate(CounterRunner runner)
        {
            yield return null;
            CounterDisplay.Create();
            yield return null;
            CounterDisplay.ApplyFont();
            CounterDisplay.Refresh();
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            AccuracyState.Reset();
            MeterVisualPatch.RefreshAllMeters();
            CounterDisplay.Refresh();
            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            try
            {
                Enabled = false;

                MeterVisualPatch.RefreshAllMeters();

                CounterDisplay.Destroy();

                if (_runnerGo != null)
                {
                    UnityEngine.Object.Destroy(_runnerGo);
                    _runnerGo = null;
                }

                if (_harmony != null)
                {
                    try { _harmony.UnpatchAll(modEntry.Info.Id); } catch { }
                    _harmony = null;
                }
            }
            catch { }
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.HideXPerfect = GUILayout.Toggle(Settings.HideXPerfect, "Hide XPerfect");
            Settings.HidePlusMinus = GUILayout.Toggle(Settings.HidePlusMinus, "Hide +Perfect / -Perfect");
            Settings.XPerfectOnly = GUILayout.Toggle(Settings.XPerfectOnly, "XPerfect Only");
            bool prevShow = Settings.ShowCounter;
            Settings.ShowCounter = GUILayout.Toggle(Settings.ShowCounter, "Show Counter");
            if (Settings.ShowCounter != prevShow)
                CounterDisplay.Refresh();

            if (Settings.ShowCounter)
            {
                GUILayout.BeginVertical();

                //Font Size
                int prevSize = Settings.CounterFontSize;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Font Size:", GUILayout.Width(90));
                int sliderFontSize = Mathf.Clamp(Settings.CounterFontSize, 0, 100);
                sliderFontSize = (int)GUILayout.HorizontalSlider(sliderFontSize, 0, 100, GUILayout.Width(180));
                if (sliderFontSize != Mathf.Clamp(prevSize, 0, 100))
                    Settings.CounterFontSize = sliderFontSize;

                string sizeStr = GUILayout.TextField(Settings.CounterFontSize.ToString(), GUILayout.Width(60));
                if (int.TryParse(sizeStr, out int parsedSize))
                {
                    Settings.CounterFontSize = parsedSize;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterFontSize != prevSize)
                    CounterDisplay.ApplySettings();

                //Spacing
                int prevSpacing = Settings.CounterSpacing;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Spacing:", GUILayout.Width(90));
                int sliderSpacing = Mathf.Clamp(Settings.CounterSpacing, 0, 50);
                sliderSpacing = (int)GUILayout.HorizontalSlider(sliderSpacing, 0f, 50f, GUILayout.Width(180));
                if (sliderSpacing != Mathf.Clamp(prevSpacing, 0, 50))
                    Settings.CounterSpacing = sliderSpacing;

                string spacingStr = GUILayout.TextField(Settings.CounterSpacing.ToString(), GUILayout.Width(60));
                if (int.TryParse(spacingStr, out int parsedSpacingInt))
                {
                    Settings.CounterSpacing = parsedSpacingInt;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterSpacing != prevSpacing)
                    CounterDisplay.Refresh();

                //Position X
                float prevX = Settings.CounterX;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Position X:", GUILayout.Width(90));
                float sliderX = Mathf.Clamp(Settings.CounterX, -Screen.width, Screen.width);
                sliderX = GUILayout.HorizontalSlider(sliderX, -Screen.width, Screen.width, GUILayout.Width(180));
                if (!Mathf.Approximately(sliderX, Mathf.Clamp(prevX, -Screen.width, Screen.width)))
                    Settings.CounterX = sliderX;

                string xStr = GUILayout.TextField(Settings.CounterX.ToString("F0"), GUILayout.Width(60));
                if (float.TryParse(xStr, out float parsedX))
                {
                    Settings.CounterX = parsedX;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterX != prevX)
                    CounterDisplay.ApplySettings();

                //Position Y
                float prevY = Settings.CounterY;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Position Y:", GUILayout.Width(90));
                float sliderY = Mathf.Clamp(Settings.CounterY, -Screen.height, Screen.height);
                sliderY = GUILayout.HorizontalSlider(sliderY, -Screen.height, Screen.height, GUILayout.Width(180));
                if (!Mathf.Approximately(sliderY, Mathf.Clamp(prevY, -Screen.height, Screen.height)))
                    Settings.CounterY = sliderY;

                string yStr = GUILayout.TextField(Settings.CounterY.ToString("F0"), GUILayout.Width(60));
                if (float.TryParse(yStr, out float parsedY))
                {
                    Settings.CounterY = parsedY;
                }
                GUILayout.EndHorizontal();
                if (Settings.CounterY != prevY)
                    CounterDisplay.ApplySettings();

                GUILayout.EndVertical();
            }


        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }
    }

    public class XPerfectSettings : UnityModManager.ModSettings
    {
        public bool HideXPerfect = false;
        public bool HidePlusMinus = false;
        public bool XPerfectOnly = false;

        public bool ShowCounter = false;
        public int CounterFontSize = 40;
        public int CounterSpacing = 20;
        public float CounterX = 0f;
        public float CounterY = 0f;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}