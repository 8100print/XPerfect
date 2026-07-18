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

        private static readonly List<DetailedJudge> JudgeHistory = new List<DetailedJudge>();
        private static int CheckpointSize;

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
            JudgeHistory.Add(judge);

            switch (judge)
            {
                case DetailedJudge.PlusPerfect:
                    PlusPerfectCount++;
                    break;

                case DetailedJudge.XPerfect:
                    XPerfectCount++;
                    break;

                case DetailedJudge.MinusPerfect:
                    MinusPerfectCount++;
                    break;
            }
        }

        public static void Reset()
        {
            PlusPerfectCount = 0;
            XPerfectCount = 0;
            MinusPerfectCount = 0;

            JudgeHistory.Clear();
            CheckpointSize = 0;

            LastJudge = DetailedJudge.None;
            LastJudgeForText = DetailedJudge.None;
        }
        public static void MarkCheckpoint()
        {
            CheckpointSize = JudgeHistory.Count;
        }

        public static void RevertToCheckpoint()
        {
            while (JudgeHistory.Count > CheckpointSize)
            {
                int last = JudgeHistory.Count - 1;
                DetailedJudge judge = JudgeHistory[last];
                JudgeHistory.RemoveAt(last);

                switch (judge)
                {
                    case DetailedJudge.PlusPerfect:
                        PlusPerfectCount--;
                        break;

                    case DetailedJudge.XPerfect:
                        XPerfectCount--;
                        break;

                    case DetailedJudge.MinusPerfect:
                        MinusPerfectCount--;
                        break;
                }
            }
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

        public static double GetActualXPerfectBoundaryDeg(double bpmTimesSpeed, double conductorPitch, float marginScale = 1f)
        {
            double xPerfectMinTimeDeg =
                scrMisc.TimeToAngleInRad(XPerfectMinTimeSec, bpmTimesSpeed, conductorPitch, false) * Mathf.Rad2Deg;

            return Math.Max(XPerfectBaseDeg * marginScale, xPerfectMinTimeDeg);
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

            return GetMeterXPerfectBoundaryDeg(bpmTimesSpeed, conductorPitch, countedBoundaryDeg, marginScale);
        }

        public static double GetMeterXPerfectBoundaryDeg(
            double bpmTimesSpeed,
            double conductorPitch,
            double countedBoundaryDeg,
            float marginScale = 1f)
        {
            double actualXPerfectBoundaryDeg = GetActualXPerfectBoundaryDeg(bpmTimesSpeed, conductorPitch, marginScale);
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
            double conductorPitch,
            float marginScale = 1f)
        {
            if (result != HitMargin.Perfect)
                return DetailedJudge.None;

            if (RDC.auto)
                return DetailedJudge.XPerfect;

            float signedDeltaDeg = AccuracyMath.GetSignedDeltaDeg(hitAngle, refAngle, isCW);
            float absDeltaDeg = Mathf.Abs(signedDeltaDeg);

            double xPerfectBoundaryDeg =
                AccuracyMath.GetActualXPerfectBoundaryDeg(bpmTimesSpeed, conductorPitch, marginScale);

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
        static void Postfix(ref HitMargin __result, float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, float marginScale = 1f)
        {
            try
            {
                if (!Main.Enabled) return;
                if (scrController.instance == null || scrConductor.instance == null) return;
                if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;

                double bpmTimesSpeed2 = (double)bpmTimesSpeed;
                double conductorPitch2 = (double)conductorPitch;

                DetailedJudge detailedJudge = JudgeCalculator.GetDetailedJudge(
                    __result, hitangle, refangle, isCW, bpmTimesSpeed2, conductorPitch2, marginScale);

                if (detailedJudge != DetailedJudge.None)
                    AccuracyState.RecordJudge(detailedJudge);
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] HitMargin error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(scrMisc), "IsValidHit")]
    [HarmonyPriority(Priority.Normal)]
    public static class IsValidHitPatch
    {
        internal static bool ShouldFailPlayer = false;

        static void Postfix(ref bool __result, HitMargin margin)
        {
            try
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
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] IsValidHit error: {ex}");
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
            try
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
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] AddHit error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    [HarmonyPriority(Priority.Low)]
    public static class HitTextPatch
    {
        private static string StripPrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "\u00A0")
                return null;

            return text;
        }
        private static string GetFallbackBaseText()
        {
            try
            {
                string rd = RDString.Get("HitMargin.Perfect", null);
                if (!string.IsNullOrWhiteSpace(rd))
                    return rd;
            }
            catch { }

            return "Perfect!";
        }

        private static string BuildDetailedText(DetailedJudge judge, HitMargin hitMargin, string currentText)
        {

            string baseText = null;

            try
            {
                baseText = RDString.Get("HitMargin." + hitMargin.ToString(), null);
            }
            catch { }

            if (string.IsNullOrWhiteSpace(baseText))
                baseText = GetFallbackBaseText();

            switch (judge)
            {
                case DetailedJudge.XPerfect:
                    return "X" + baseText;

                case DetailedJudge.PlusPerfect:
                    return "+" + baseText;

                case DetailedJudge.MinusPerfect:
                    return "-" + baseText;

                default:
                    return baseText;
            }
        }

        static void Postfix(scrHitTextMesh __instance)
        {
            try
            {
                if (__instance == null) return;
                if (__instance.hitMargin != HitMargin.Perfect) return;

                var tmp = __instance.text;
                if (tmp == null) return;

                string originalText = tmp.text;

                Color perfectColor = (Color)XPerfectColors.PlusMinus;

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

                Color xPerfectColor = (Color)XPerfectColors.XPerfect;

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

                Color finalColor = judge == DetailedJudge.XPerfect ? (Color)XPerfectColors.XPerfect : (Color)XPerfectColors.PlusMinus;

                tmp.text = BuildDetailedText(judge, __instance.hitMargin, originalText);
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

    [HarmonyPatch(typeof(DetailedResults), "Show")]
    public static class DetailedResultsShowPatch
    {
        static void Prefix()
        {
        }
    }

    [HarmonyPatch(typeof(DetailedResults), "ShowForPlayer")]
    public static class ResultsTextPatch
    {
        static void Postfix(DetailedResults __instance)
        {
            var ctrl = scrController.instance;
            if (ctrl == null) return;

            if (!Main.Enabled) return;
            if (__instance == null) return;

            bool isPureXPerfectRun =
                !ctrl.startedFromCheckpoint &&
                AccuracyState.XPerfectCount > 0 &&
                AccuracyState.PlusPerfectCount == 0 &&
                AccuracyState.MinusPerfectCount == 0;

            if (isPureXPerfectRun &&
                string.IsNullOrEmpty(ctrl.customTxtPurePerfect) &&
                ctrl.mistakesManager.IsAllPurePerfect() &&
                ctrl.txtCongrats != null)
            {
                string shown = ctrl.txtCongrats.text;

                if (!string.IsNullOrEmpty(shown) && !shown.StartsWith("X"))
                    ctrl.txtCongrats.text = "X" + shown;
            }

            if (__instance == null || __instance.textComponent == null) return;

            string text = __instance.textComponent.text;
            if (string.IsNullOrEmpty(text)) return;

            string detail =
                $" <color=#{XPerfectColors.PlusMinusHex}>[+{AccuracyState.PlusPerfectCount}/</color>" +
                $"<color=#{XPerfectColors.XPerfectHex}>{AccuracyState.XPerfectCount}</color>" +
                $"<color=#{XPerfectColors.PlusMinusHex}>/-{AccuracyState.MinusPerfectCount}]</color>";

            if (text.Contains(detail)) return;

            const string separator = "     ";
            int firstNewline = text.IndexOf('\n');
            string firstLine = firstNewline >= 0 ? text.Substring(0, firstNewline) : text;
            string rest = firstNewline >= 0 ? text.Substring(firstNewline) : "";

            string[] tokens = firstLine.Split(new string[] { separator }, System.StringSplitOptions.None);
            if (tokens.Length >= 2)
            {
                tokens[1] = tokens[1] + detail;
                __instance.textComponent.text = string.Join(separator, tokens) + rest;
            }
            else
            {
                __instance.textComponent.text = text + detail;
            }
        }
    }

    [HarmonyPatch(typeof(scrMistakesManager), "MarkCheckpoint")]
    public static class CheckpointMarkPatch
    {
        static void Postfix()
        {
            AccuracyState.MarkCheckpoint();
        }
    }

    [HarmonyPatch(typeof(scrMistakesManager), "RevertToLastCheckpoint")]
    public static class CheckpointRestorePatch
    {
        static void Postfix()
        {
            AccuracyState.RevertToCheckpoint();

            CounterDisplay.Refresh();
        }
    }
}