using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace XPerfect
{
    public enum DetailedJudge
    {
        None,
        XPerfect,
        PlusPerfect,
        MinusPerfect
    }

    public static class AccuracyState
    {
        public static int PlusPerfectCount { get; private set; }
        public static int XPerfectCount { get; private set; }
        public static int MinusPerfectCount { get; private set; }

        public static DetailedJudge LastJudge { get; private set; } = DetailedJudge.None;
        public static DetailedJudge LastJudgeForText { get; private set; } = DetailedJudge.None;

        public static void RecordJudge(DetailedJudge judge)
        {
            LastJudge = judge;
            LastJudgeForText = judge;
        }

        public static void ConsumeJudge()
        {
            LastJudge = DetailedJudge.None;
        }
        public static void ConsumeJudgeForText()
        {
            LastJudgeForText = DetailedJudge.None;
        }

        public static void IncrementCount(DetailedJudge judge)
        {
            switch (judge)
            {
                case DetailedJudge.PlusPerfect: PlusPerfectCount++; break;
                case DetailedJudge.XPerfect: XPerfectCount++; break;
                case DetailedJudge.MinusPerfect: MinusPerfectCount++; break;
            }
        }

        public static void Reset()
        {
            PlusPerfectCount = 0;
            XPerfectCount = 0;
            MinusPerfectCount = 0;
            LastJudge = DetailedJudge.None;
            LastJudgeForText = DetailedJudge.None;
        }
    }

    public static class AccuracyMath
    {
        public const double XPerfectBaseDeg = 15.0;
        public const double XPerfectMinTimeSec = 0.01667;

        public static float GetSignedDeltaDeg(float hitAngle, float refAngle, bool isCW)
        {
            float delta = (hitAngle - refAngle) * Mathf.Rad2Deg;
            return isCW ? delta : -delta;
        }

        public static double GetActualXPerfectBoundaryDeg(double bpmTimesSpeed, double conductorPitch)
        {
            double xPerfectMinTimeDeg =
                scrMisc.TimeToAngleInRad(XPerfectMinTimeSec, bpmTimesSpeed, conductorPitch, false) * Mathf.Rad2Deg;

            return Math.Max(XPerfectBaseDeg, xPerfectMinTimeDeg);
        }

        public static double GetMeterScale(double countedBoundaryDeg)
        {
            if (countedBoundaryDeg <= 0.0)
                return 1.0;

            return 60.0 / countedBoundaryDeg;
        }

        public static double GetMeterXPerfectBoundaryDeg(
            double bpmTimesSpeed,
            double conductorPitch,
            float marginScale = 1f)
        {
            double countedBoundaryDeg = scrMisc.GetAdjustedAngleBoundaryInDeg(
                HitMarginGeneral.Counted,
                bpmTimesSpeed,
                conductorPitch,
                marginScale
            );

            return GetMeterXPerfectBoundaryDeg(bpmTimesSpeed, conductorPitch, countedBoundaryDeg);
        }

        public static double GetMeterXPerfectBoundaryDeg(
            double bpmTimesSpeed,
            double conductorPitch,
            double countedBoundaryDeg)
        {
            double actualXPerfectBoundaryDeg = GetActualXPerfectBoundaryDeg(bpmTimesSpeed, conductorPitch);
            double meterScale = GetMeterScale(countedBoundaryDeg);

            return actualXPerfectBoundaryDeg * meterScale;
        }
    }

    public static class JudgeCalculator
    {
        public static DetailedJudge GetDetailedJudge(
            HitMargin result,
            float hitAngle,
            float refAngle,
            bool isCW,
            double bpmTimesSpeed,
            double conductorPitch)
        {
            if (RDC.auto)
                return DetailedJudge.XPerfect;

            if (result != HitMargin.Perfect)
                return DetailedJudge.None;

            float signedDeltaDeg = AccuracyMath.GetSignedDeltaDeg(hitAngle, refAngle, isCW);
            float absDeltaDeg = Mathf.Abs(signedDeltaDeg);

            double xPerfectBoundaryDeg =
                AccuracyMath.GetActualXPerfectBoundaryDeg(bpmTimesSpeed, conductorPitch);

            if (absDeltaDeg <= xPerfectBoundaryDeg)
                return DetailedJudge.XPerfect;

            if (signedDeltaDeg < 0f)
                return DetailedJudge.PlusPerfect;

            return DetailedJudge.MinusPerfect;
        }
    }

    [HarmonyPatch(typeof(scrMisc), "GetHitMargin")]
    [HarmonyPriority(Priority.High)]
    public static class HitMarginPatch
    {
        static void Postfix(ref HitMargin __result, float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale = 1f)
        {
            if (!Main.Enabled) return;
            if (scrController.instance == null || scrConductor.instance == null) return;
            if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;

            double bpmTimesSpeed2 = (double)bpmTimesSpeed;
            double conductorPitch2 = (double)conductorPitch;

            DetailedJudge detailedJudge = JudgeCalculator.GetDetailedJudge(
                __result, hitangle, refangle, isCW, bpmTimesSpeed2, conductorPitch2);

            if (detailedJudge != DetailedJudge.None)
                AccuracyState.RecordJudge(detailedJudge);
        }
    }

    [HarmonyPatch(typeof(scrMisc), "IsValidHit")]
    [HarmonyPriority(Priority.Normal)]
    public static class IsValidHitPatch
    {
        internal static bool ShouldFailPlayer = false;

        static void Postfix(ref bool __result, HitMargin margin)
        {
            if (!Main.Enabled || !Main.Settings.XPerfectOnly) return;
            if (scrController.instance == null || !scrController.instance.gameworld) return;

            if (RDC.auto) return;

            bool shouldBlock = false;

            if (margin != HitMargin.Perfect)
            {
                shouldBlock = true;
            }
            else
            {
                DetailedJudge judge = AccuracyState.LastJudge;
                if (judge == DetailedJudge.PlusPerfect || judge == DetailedJudge.MinusPerfect)
                    shouldBlock = true;
            }

            if (shouldBlock)
            {
                __result = false;
                ShouldFailPlayer = true;
            }
        }
    }

    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    [HarmonyPriority(Priority.Normal)]
    public static class SwitchChosenFailPatch
    {
        static void Postfix()
        {
            try
            {
                if (!IsValidHitPatch.ShouldFailPlayer) return;
                IsValidHitPatch.ShouldFailPlayer = false;

                if (!Main.Enabled || !Main.Settings.XPerfectOnly) return;

                var ctrl = scrController.instance;
                if (ctrl == null) return;

                ctrl.playerOne.Die(false, false, "", true);
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] SwitchChosen error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(scrMarginTracker), "AddHit")]
    [HarmonyPriority(Priority.Normal)]
    public static class MistakesManagerAddHitPatch
    {
        static void Postfix(HitMargin hit)
        {
            if (!Main.Enabled) return;
            if (hit != HitMargin.Perfect) return;
            if (scrController.instance == null || scrConductor.instance == null) return;
            if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;

            DetailedJudge detailedJudge = AccuracyState.LastJudge;
            if (detailedJudge == DetailedJudge.None) return;

            AccuracyState.IncrementCount(detailedJudge);
            AccuracyState.ConsumeJudge();

            CounterDisplay.Refresh();
        }
    }

    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    [HarmonyPriority(Priority.Low)]
    public static class HitTextPatch
    {
        private static readonly Dictionary<SystemLanguage, string> PerfectTextCache =
            new Dictionary<SystemLanguage, string>();

        private static string StripPrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "\u00A0")
                return null;

            if (text.StartsWith("X") || text.StartsWith("+") || text.StartsWith("-"))
                text = text.Substring(1);

            if (string.IsNullOrWhiteSpace(text) || text == "\u00A0")
                return null;

            return text;
        }

        private static void RememberPerfectBaseText(string text)
        {
            string baseText = StripPrefix(text);
            if (string.IsNullOrWhiteSpace(baseText))
                return;

            PerfectTextCache[Persistence.language] = baseText;
        }

        private static string GetFallbackBaseText()
        {
            if (PerfectTextCache.TryGetValue(Persistence.language, out string cached) &&
                !string.IsNullOrWhiteSpace(cached))
            {
                return cached;
            }

            return "Perfect!";
        }

        private static string BuildDetailedText(DetailedJudge judge, string currentText)
        {
            string baseText = StripPrefix(currentText);
            if (string.IsNullOrWhiteSpace(baseText))
                baseText = GetFallbackBaseText();

            switch (judge)
            {
                case DetailedJudge.XPerfect: return "X" + baseText;
                case DetailedJudge.PlusPerfect: return "+" + baseText;
                case DetailedJudge.MinusPerfect: return "-" + baseText;
                default: return baseText;
            }
        }

        static void Postfix(scrHitTextMesh __instance)
        {
            try
            {
                if (__instance == null) return;

                var tmp = __instance.text;
                if (tmp == null) return;

                string originalText = tmp.text;

                if (__instance.hitMargin == HitMargin.Perfect)
                    RememberPerfectBaseText(originalText);

                if (__instance.hitMargin != HitMargin.Perfect)
                    return;

                Color perfectColor = new Color(0.376f, 1.000f, 0.306f, 1.000f);

                if (!Main.Enabled)
                {
                    string baseText = StripPrefix(originalText);
                    if (string.IsNullOrWhiteSpace(baseText))
                        baseText = GetFallbackBaseText();

                    tmp.text = baseText;
                    tmp.color = perfectColor;
                    return;
                }

                DetailedJudge judge = AccuracyState.LastJudgeForText;
                if (judge == DetailedJudge.None)
                    return;

                Color xPerfectColor = new Color(0.3f, 0.8f, 1f, 1f);

                if (judge == DetailedJudge.XPerfect && Main.Settings.HideXPerfect)
                {
                    tmp.text = "\u00A0";
                    return;
                }

                if ((judge == DetailedJudge.PlusPerfect || judge == DetailedJudge.MinusPerfect)
                    && Main.Settings.HidePlusMinus)
                {
                    tmp.text = "\u00A0";
                    return;
                }

                Color finalColor = judge == DetailedJudge.XPerfect ? xPerfectColor : perfectColor;

                tmp.text = BuildDetailedText(judge, originalText);
                tmp.color = finalColor;
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] HitTextPatch error: {ex}");
            }
            finally
            {
                if (__instance != null && __instance.hitMargin == HitMargin.Perfect)
                {
                    AccuracyState.ConsumeJudgeForText();
                }
            }
        }
    }

    [HarmonyPatch(typeof(scrController), "Start_Rewind")]
    public static class LevelStartPatch
    {
        static void Postfix()
        {
            AccuracyState.Reset();
            IsValidHitPatch.ShouldFailPlayer = false;
            CounterDisplay.Refresh();
        }
    }

    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    public static class ResultsTextPatch
    {
        static void Postfix(scrController __instance)
        {
            if (!Main.Enabled) return;
            if (__instance == null) return;

            bool isPureXPerfectRun =
                !__instance.startedFromCheckpoint &&
                AccuracyState.XPerfectCount > 0 &&
                AccuracyState.PlusPerfectCount == 0 &&
                AccuracyState.MinusPerfectCount == 0;

            if (isPureXPerfectRun &&
                string.IsNullOrEmpty(__instance.customTxtPurePerfect) &&
                __instance.mistakesManager.IsAllPurePerfect() &&
                __instance.txtCongrats != null)
            {
                string shown = __instance.txtCongrats.text;
                if (!string.IsNullOrEmpty(shown) && !shown.StartsWith("X"))
                    __instance.txtCongrats.text = "X" + shown;
            }

            var detailedResults = __instance.detailedResults;
            if (detailedResults == null) return;
            string text = detailedResults.textComponent.text;
            if (string.IsNullOrEmpty(text)) return;

            string detail =
                $" <color=#60FF4E>[+{AccuracyState.PlusPerfectCount}/</color>" +
                $"<color=#4DCCFF>{AccuracyState.XPerfectCount}</color>" +
                $"<color=#60FF4E>/-{AccuracyState.MinusPerfectCount}]</color>";

            if (text.Contains(detail)) return;

            const string separator = "     ";
            int firstNewline = text.IndexOf('\n');
            string firstLine = firstNewline >= 0 ? text.Substring(0, firstNewline) : text;
            string rest = firstNewline >= 0 ? text.Substring(firstNewline) : "";

            string[] tokens = firstLine.Split(new string[] { separator }, System.StringSplitOptions.None);
            if (tokens.Length >= 2)
            {
                tokens[1] = tokens[1] + detail;
                detailedResults.textComponent.text = string.Join(separator, tokens) + rest;
            }
            else
            {
                detailedResults.textComponent.text = text + detail;
            }
        }
    }

}