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
        private const int MaxPlayers = 4;

        private static int[] _plusCount = new int[MaxPlayers];
        private static int[] _xCount = new int[MaxPlayers];
        private static int[] _minusCount = new int[MaxPlayers];
        private static List<DetailedJudge>[] _judgeHistory = new List<DetailedJudge>[MaxPlayers];
        private static int[] _checkpointSize = new int[MaxPlayers];
        private static DetailedJudge[] _lastJudge = new DetailedJudge[MaxPlayers];
        private static DetailedJudge[] _lastJudgeForText = new DetailedJudge[MaxPlayers];
        internal static int _currentPlayerId = 0;

        static AccuracyState()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                _judgeHistory[i] = new List<DetailedJudge>();
                _lastJudge[i] = DetailedJudge.None;
                _lastJudgeForText[i] = DetailedJudge.None;
            }
        }

        internal static int PlayerCount =>
            Math.Min(scrMistakesManager.marginTrackers?.Length ?? 1, MaxPlayers);

        public static int PlusPerfectCount
        {
            get { int p = 0; int n = PlayerCount; for (int i = 0; i < n; i++) p += _plusCount[i]; return p; }
        }
        public static int XPerfectCount
        {
            get { int x = 0; int n = PlayerCount; for (int i = 0; i < n; i++) x += _xCount[i]; return x; }
        }
        public static int MinusPerfectCount
        {
            get { int m = 0; int n = PlayerCount; for (int i = 0; i < n; i++) m += _minusCount[i]; return m; }
        }


        public static int GetCount(int player, DetailedJudge judge)
        {
            if (player < 0 || player >= MaxPlayers) return 0;
            switch (judge)
            {
                case DetailedJudge.PlusPerfect: return _plusCount[player];
                case DetailedJudge.XPerfect: return _xCount[player];
                case DetailedJudge.MinusPerfect: return _minusCount[player];
                default: return 0;
            }
        }

        public static void GetCombinedCounts(out int plus, out int x, out int minus)
        {
            plus = 0; x = 0; minus = 0;
            int n = PlayerCount;
            for (int p = 0; p < n; p++)
            {
                plus += _plusCount[p];
                x += _xCount[p];
                minus += _minusCount[p];
            }
        }

        public static int GetPlayerPlusPerfectCount(int player) => GetCount(player, DetailedJudge.PlusPerfect);
        public static int GetPlayerXPerfectCount(int player) => GetCount(player, DetailedJudge.XPerfect);
        public static int GetPlayerMinusPerfectCount(int player) => GetCount(player, DetailedJudge.MinusPerfect);

        public static DetailedJudge LastJudge => _lastJudge[_currentPlayerId];
        public static DetailedJudge LastJudgeForText => _lastJudgeForText[_currentPlayerId];

        public static DetailedJudge GetPlayerLastJudge(int player)
        {
            return (player >= 0 && player < MaxPlayers) ? _lastJudge[player] : DetailedJudge.None;
        }
        public static DetailedJudge GetPlayerLastJudgeForText(int player)
        {
            return (player >= 0 && player < MaxPlayers) ? _lastJudgeForText[player] : DetailedJudge.None;
        }

        public static void RecordJudge(int player, DetailedJudge judge)
        {
            if (player < 0 || player >= MaxPlayers) return;
            _lastJudge[player] = judge;
            _lastJudgeForText[player] = judge;
        }

        public static void ConsumeJudge(int player)
        {
            if (player < 0 || player >= MaxPlayers) return;
            _lastJudge[player] = DetailedJudge.None;
        }
        public static void ConsumeJudgeForText(int player)
        {
            if (player < 0 || player >= MaxPlayers) return;
            _lastJudgeForText[player] = DetailedJudge.None;
        }

        public static void AddHit(int player, DetailedJudge judge)
        {
            if (player < 0 || player >= PlayerCount) return;
            _judgeHistory[player].Add(judge);
            switch (judge)
            {
                case DetailedJudge.PlusPerfect: _plusCount[player]++; break;
                case DetailedJudge.XPerfect: _xCount[player]++; break;
                case DetailedJudge.MinusPerfect: _minusCount[player]++; break;
            }
        }

        public static void Reset()
        {
            for (int p = 0; p < MaxPlayers; p++)
            {
                _plusCount[p] = 0;
                _xCount[p] = 0;
                _minusCount[p] = 0;
                _judgeHistory[p].Clear();
                _checkpointSize[p] = 0;
                _lastJudge[p] = DetailedJudge.None;
                _lastJudgeForText[p] = DetailedJudge.None;
            }
        }

        public static void MarkCheckpoint(int player = 0)
        {
            if (player < 0 || player >= PlayerCount) return;
            _checkpointSize[player] = _judgeHistory[player].Count;
        }

        public static void MarkAllCheckpoints()
        {
            for (int p = 0; p < PlayerCount; p++)
                _checkpointSize[p] = _judgeHistory[p].Count;
        }

        public static void RevertToCheckpoint(int player = 0)
        {
            if (player < 0 || player >= PlayerCount) return;
            var history = _judgeHistory[player];
            while (history.Count > _checkpointSize[player])
            {
                int last = history.Count - 1;
                DetailedJudge judge = history[last];
                history.RemoveAt(last);
                switch (judge)
                {
                    case DetailedJudge.PlusPerfect: _plusCount[player]--; break;
                    case DetailedJudge.XPerfect: _xCount[player]--; break;
                    case DetailedJudge.MinusPerfect: _minusCount[player]--; break;
                }
            }
        }

        public static void RevertAllToCheckpoint()
        {
            for (int p = 0; p < PlayerCount; p++)
                RevertToCheckpoint(p);
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
            if (RDC.auto)
                return DetailedJudge.XPerfect;

            if (result != HitMargin.Perfect)
                return DetailedJudge.None;

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
        static void Postfix(ref HitMargin __result, float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale = 1f)
        {
            if (!Main.Enabled) return;
            if (scrController.instance == null || scrConductor.instance == null) return;
            if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;

            double bpmTimesSpeed2 = (double)bpmTimesSpeed;
            double conductorPitch2 = (double)conductorPitch;

            DetailedJudge detailedJudge = JudgeCalculator.GetDetailedJudge(
                __result, hitangle, refangle, isCW, bpmTimesSpeed2, conductorPitch2, (float)marginScale);

            if (detailedJudge != DetailedJudge.None)
                AccuracyState.RecordJudge(AccuracyState._currentPlayerId, detailedJudge);
        }
    }

    [HarmonyPatch(typeof(scrMisc), "IsValidHit")]
    [HarmonyPriority(Priority.Normal)]
    public static class IsValidHitPatch
    {
        internal static bool[] ShouldFailPlayer = new bool[0];

        internal static void EnsureCapacity()
        {
            int count = AccuracyState.PlayerCount;
            if (ShouldFailPlayer.Length < count)
                System.Array.Resize(ref ShouldFailPlayer, count);
        }

        static void Postfix(ref bool __result, HitMargin margin)
        {
            if (!Main.Enabled || !Main.Settings.XPerfectOnly) return;
            if (scrController.instance == null || !scrController.instance.gameworld) return;

            if (RDC.auto) return;

            EnsureCapacity();

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
                int pid = AccuracyState._currentPlayerId;
                if (pid >= 0 && pid < ShouldFailPlayer.Length)
                    ShouldFailPlayer[pid] = true;
            }
        }
    }

    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    [HarmonyPriority(Priority.High)]
    public static class PlanetSwitchPrefix
    {
        static void Prefix(scrPlanet __instance)
        {
            AccuracyState._currentPlayerId = (__instance.player != null) ? __instance.player.playerID : 0;
        }
    }

    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    [HarmonyPriority(Priority.Normal)]
    public static class SwitchChosenFailPatch
    {
        static void Postfix(scrPlanet __instance)
        {
            try
            {
                int pid = (__instance.player != null) ? __instance.player.playerID : 0;
                if (pid < 0 || pid >= IsValidHitPatch.ShouldFailPlayer.Length) return;
                if (!IsValidHitPatch.ShouldFailPlayer[pid]) return;
                IsValidHitPatch.ShouldFailPlayer[pid] = false;

                if (!Main.Enabled || !Main.Settings.XPerfectOnly) return;

                var player = __instance.player;
                if (player == null) return;

                player.Die(false, false, "", true);
            }
            catch (System.Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] SwitchChosen error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(scrMarginTracker), "AddHit")]
    [HarmonyPriority(Priority.Normal)]
    public static class MistakesManagerAddHitPatch
    {
        static void Postfix(scrMarginTracker __instance, HitMargin hit)
        {
            if (!Main.Enabled) return;
            if (hit != HitMargin.Perfect) return;
            if (scrController.instance == null || scrConductor.instance == null) return;
            if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;

            int playerId = System.Array.IndexOf(scrMistakesManager.marginTrackers, __instance);
            if (playerId < 0) playerId = 0;

            DetailedJudge detailedJudge = AccuracyState.GetPlayerLastJudge(playerId);
            if (detailedJudge == DetailedJudge.None) return;

            AccuracyState.AddHit(playerId, detailedJudge);
            AccuracyState.ConsumeJudge(playerId);

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
                    AccuracyState.ConsumeJudgeForText(AccuracyState._currentPlayerId);
                }
            }
        }
    }

    [HarmonyPatch(typeof(scrController), "Start_Rewind")]
    public static class LevelStartPatch
    {
        static void Postfix()
        {
            if (GCS.checkpointNum == 0)
            {
                AccuracyState.Reset();
            }
            IsValidHitPatch.EnsureCapacity();
            for (int i = 0; i < IsValidHitPatch.ShouldFailPlayer.Length; i++)
                IsValidHitPatch.ShouldFailPlayer[i] = false;
            CounterDisplay.Refresh();
        }
    }

    [HarmonyPatch(typeof(scrMistakesManager), "MarkCheckpoint")]
    public static class CheckpointMarkPatch
    {
        static void Postfix()
        {
            AccuracyState.MarkAllCheckpoints();
        }
    }

    [HarmonyPatch(typeof(scrMistakesManager), "RevertToLastCheckpoint")]
    public static class CheckpointRestorePatch
    {
        static void Postfix()
        {
            AccuracyState.RevertAllToCheckpoint();
        }
    }

    [HarmonyPatch(typeof(DetailedResults), "Show")]
    public static class DetailedResultsShowPatch
    {
        static void Prefix()
        {
            DetailedResultsXPerfectPatch.ResetCache();
        }
    }

    [HarmonyPatch(typeof(DetailedResults), "ShowForPlayer")]
    public static class DetailedResultsXPerfectPatch
    {
        private static string _baseCongratsText = null;
        private static string _purePerfectText = null;

        public static void ResetCache()
        {
            _baseCongratsText = null;
            _purePerfectText = null;
        }

        static void Postfix(DetailedResults __instance, int playerIndex)
        {
            if (!Main.Enabled) return;
            if (__instance == null || __instance.textComponent == null) return;

            var ctrl = scrController.instance;

            // Cache texts on first call
            if (_baseCongratsText == null && ctrl?.txtCongrats != null)
            {
                _baseCongratsText = ctrl.txtCongrats.text;
                _purePerfectText = string.IsNullOrEmpty(ctrl.customTxtPurePerfect)
                    ? RDString.Get("status.allPurePerfect")
                    : ctrl.customTxtPurePerfect;
            }

            // Per-player congrats text
            // When pure XPerfect: show "X" + pure perfect text (e.g. "XPerfect!")
            // Otherwise: show standard congrats text
            bool startedFromCheckpoint = ctrl != null && ctrl.startedFromCheckpoint;
            bool playerPurePerfect = playerIndex >= 0 && playerIndex < scrMistakesManager.marginTrackers.Length
                && scrMistakesManager.marginTrackers[playerIndex].IsAllPurePerfect();
            bool playerXPerfect = AccuracyState.GetPlayerXPerfectCount(playerIndex) > 0
                && AccuracyState.GetPlayerPlusPerfectCount(playerIndex) == 0
                && AccuracyState.GetPlayerMinusPerfectCount(playerIndex) == 0;

            if (ctrl?.txtCongrats != null)
            {
                if (!startedFromCheckpoint && playerPurePerfect && _purePerfectText != null)
                    ctrl.txtCongrats.text = playerXPerfect ? "X" + _purePerfectText : _purePerfectText;
                else if (_baseCongratsText != null)
                    ctrl.txtCongrats.text = _baseCongratsText;
            }

            // Append XPerfect detail to results text
            int plus = AccuracyState.GetPlayerPlusPerfectCount(playerIndex);
            int x = AccuracyState.GetPlayerXPerfectCount(playerIndex);
            int minus = AccuracyState.GetPlayerMinusPerfectCount(playerIndex);

            if (plus == 0 && x == 0 && minus == 0) return;

            string text = __instance.textComponent.text;
            if (string.IsNullOrEmpty(text)) return;

            string detail = $" <color=#60FF4E>[+{plus}/</color><color=#4DCCFF>{x}</color><color=#60FF4E>/-{minus}]</color>";

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
}