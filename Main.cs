using HarmonyLib;
using System.Reflection;
using UnityModManagerNet;

namespace XPerfect
{
    public static class Main
    {
        public static XPerfectSettings Settings;
        public static string ModPath;
        public static bool Enabled { get; private set; }

        private static Harmony _harmony;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Settings = UnityModManager.ModSettings.Load<XPerfectSettings>(modEntry);
            ModPath = modEntry.Path;

            _harmony = new Harmony(modEntry.Info.Id);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            Enabled = true;
            AccuracyState.Reset();

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            AccuracyState.Reset();
            MeterVisualPatch.RefreshAllMeters();

            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.HideXPerfect = UnityEngine.GUILayout.Toggle(
                Settings.HideXPerfect,
                "Hide XPerfect"
            );
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }
    }

    public class XPerfectSettings : UnityModManager.ModSettings
    {
        public bool HideXPerfect = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}