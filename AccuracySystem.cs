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
        public static int PlusPerfectCount;
        public static int XPerfectCount;
        public static int MinusPerfectCount;

        public static DetailedJudge LastJudge = DetailedJudge.None;

        public static void Reset()
        {
            PlusPerfectCount = 0;
            XPerfectCount = 0;
            MinusPerfectCount = 0;

            LastJudge = DetailedJudge.None;
        }
    }

    public static class AccuracyMath
    {
        public const double XPerfectBaseDeg = 15.0;
        public const double XPerfectMinTimeSec = 0.01667;

        public static float GetSignedDeltaDeg(float hitAngle, float refAngle, bool isCW)
        {
            float deltaDeg = Mathf.DeltaAngle(
                refAngle * Mathf.Rad2Deg,
                hitAngle * Mathf.Rad2Deg
            );

            return isCW ? deltaDeg : -deltaDeg;
        }

        public static double GetBpmTimesSpeed()
        {
            if (scrConductor.instance == null || scrController.instance == null)
                return 0.0;

            return scrConductor.instance.bpm * scrController.instance.speed;
        }

        public static double GetConductorPitch()
        {
            if (scrConductor.instance == null || scrConductor.instance.song == null)
                return 1.0;

            return scrConductor.instance.song.pitch;
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
            if (result != HitMargin.Perfect)
                return DetailedJudge.None;

            if (RDC.auto)
                return DetailedJudge.XPerfect;

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
    public static class HitMarginPatch
    {
        static void Postfix(ref HitMargin __result, float hitangle, float refangle, bool isCW)
        {
            try
            {
                if (!Main.Enabled)
                    return;

                if (scrController.instance == null || scrConductor.instance == null)
                    return;

                if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl)
                    return;

                double bpmTimesSpeed = AccuracyMath.GetBpmTimesSpeed();
                double conductorPitch = AccuracyMath.GetConductorPitch();

                DetailedJudge detailedJudge = JudgeCalculator.GetDetailedJudge(
                    __result,
                    hitangle,
                    refangle,
                    isCW,
                    bpmTimesSpeed,
                    conductorPitch
                );

                if (detailedJudge == DetailedJudge.None)
                    return;

                AccuracyState.LastJudge = detailedJudge;

                switch (detailedJudge)
                {
                    case DetailedJudge.PlusPerfect:
                        AccuracyState.PlusPerfectCount++;
                        break;
                    case DetailedJudge.XPerfect:
                        AccuracyState.XPerfectCount++;
                        break;
                    case DetailedJudge.MinusPerfect:
                        AccuracyState.MinusPerfectCount++;
                        break;
                }
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] HitMargin error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(scrController), "Start_Rewind")]
    public static class LevelStartPatch
    {
        static void Postfix()
        {
            AccuracyState.Reset();
        }
    }

    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    public static class ResultsTextPatch
    {
        static void Postfix(scrController __instance)
        {
            if (!Main.Enabled)
                return;

            if (__instance == null)
                return;

            if (__instance.txtCongrats != null)
            {
                string congratsText = __instance.txtCongrats.text;

                bool isPureXPerfectRun =
                    !__instance.startedFromCheckpoint &&
                    AccuracyState.XPerfectCount > 0 &&
                    AccuracyState.PlusPerfectCount == 0 &&
                    AccuracyState.MinusPerfectCount == 0;

                if (!string.IsNullOrEmpty(congratsText) &&
                    isPureXPerfectRun &&
                    !congratsText.StartsWith("X"))
                {
                    __instance.txtCongrats.text = "X" + congratsText;
                }
            }

            if (__instance.txtResults == null)
                return;

            string text = __instance.txtResults.text;
            if (string.IsNullOrEmpty(text))
                return;

            string detail =
                $" <color=#60FF4E>[+{AccuracyState.PlusPerfectCount}/</color>" +
                $"<color=#4DCCFF>{AccuracyState.XPerfectCount}</color>" +
                $"<color=#60FF4E>/-{AccuracyState.MinusPerfectCount}]</color>";

            if (text.Contains(detail))
                return;

            string closeTag = "</color>";
            int firstClose = text.IndexOf(closeTag, StringComparison.Ordinal);
            if (firstClose == -1)
                return;

            int secondClose = text.IndexOf(closeTag, firstClose + closeTag.Length, StringComparison.Ordinal);
            if (secondClose == -1)
                return;

            int insertIndex = secondClose + closeTag.Length;
            __instance.txtResults.text = text.Insert(insertIndex, detail);
        }
    }

    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
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
                if (__instance == null)
                    return;

                var textMesh = __instance.GetComponent<TextMesh>();
                if (textMesh == null)
                    return;

                var meshRenderer = __instance.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    return;

                string originalText = textMesh.text;

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

                    textMesh.text = baseText;
                    textMesh.color = perfectColor;
                    meshRenderer.material.color = perfectColor;
                    return;
                }

                DetailedJudge judge = AccuracyState.LastJudge;
                if (judge == DetailedJudge.None)
                    return;

                Color xPerfectColor = new Color(0.3f, 0.8f, 1f, 1f);

                if (judge == DetailedJudge.XPerfect && Main.Settings.HideXPerfect)
                {
                    textMesh.text = "\u00A0";
                    return;
                }

                Color finalColor = judge == DetailedJudge.XPerfect ? xPerfectColor : perfectColor;

                textMesh.text = BuildDetailedText(judge, originalText);
                textMesh.color = finalColor;
                meshRenderer.material.color = finalColor;

                AccuracyState.LastJudge = DetailedJudge.None;
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] HitTextPatch error: {ex}");
            }
        }
    }
}