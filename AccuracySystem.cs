using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace XPerfect
{
    public static class AccuracyState
    {
        public static bool HasPendingJudge { get; private set; }

        public static void RecordJudge()
        {
            HasPendingJudge = true;
        }

        public static void ConsumeJudge()
        {
            HasPendingJudge = false;
        }

        public static int GetXPerfectCount()
        {
            scrMarginTracker tracker = GetTracker();
            if (tracker == null) return 0;
            return RDC.auto
                ? tracker.GetHits(HitMargin.XPerfect, HitMargin.PerfectMinus, HitMargin.PerfectPlus)
                : tracker.GetHits(HitMargin.XPerfect);
        }

        public static int GetPlusPerfectCount()
        {
            scrMarginTracker tracker = GetTracker();
            return tracker == null || RDC.auto
                ? 0
                : tracker.GetHits(HitMargin.PerfectPlus);
        }

        public static int GetMinusPerfectCount()
        {
            scrMarginTracker tracker = GetTracker();
            return tracker == null || RDC.auto
                ? 0
                : tracker.GetHits(HitMargin.PerfectMinus);
        }

        private static scrMarginTracker GetTracker()
        {
            if (scrMistakesManager.marginTrackers == null || scrMistakesManager.marginTrackers.Length == 0)
            {
                return null;
            }

            return scrMistakesManager.marginTrackers[0];
        }

        public static void Reset()
        {
            HasPendingJudge = false;
        }
    }

    [HarmonyPatch(typeof(scrMisc), "SelectHitMarginByTimeBoundary")]
    [HarmonyPriority(Priority.High)]
    public static class HitMarginPatch
    {
        static void Postfix(HitMargin __result)
        {
            try
            {
                if (!Main.Enabled) return;
                if (scrController.instance == null || scrConductor.instance == null) return;
                if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;
                if (!CompetitiveModePrefs.enableCompetitiveMode) return;

                if (IsTrackedMargin(__result))
                    AccuracyState.RecordJudge();

                if (Main.Settings.XPerfectOnly && __result != HitMargin.XPerfect)
                {
                    if (ADOBase.controller != null && !ADOBase.controller.noFail && !ADOBase.controller.currFloor.freeroam)
                    {
                        XPerfectOnlyState.PendingFailMargin = __result;
                        XPerfectOnlyState.ShouldFailPlayer = true;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] HitMargin error: {ex}");
            }
        }

        private static bool IsTrackedMargin(HitMargin margin)
        {
            return margin == HitMargin.XPerfect ||
                   margin == HitMargin.PerfectMinus ||
                   margin == HitMargin.PerfectPlus;
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
                if (hit != HitMargin.XPerfect && hit != HitMargin.PerfectMinus &&
                    hit != HitMargin.PerfectPlus && hit != HitMargin.Auto) return;
                if (scrController.instance == null || scrConductor.instance == null) return;
                if ((States)scrController.instance.stateMachine.GetState() != States.PlayerControl) return;

                if (!AccuracyState.HasPendingJudge) return;

                AccuracyState.ConsumeJudge();

                CounterDisplay.Refresh();
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] AddHit error: {ex}");
            }
        }
    }


    [HarmonyPatch(typeof(scrController), "Start_Rewind")]
    public static class LevelStartPatch
    {
        static void Postfix()
        {
            AccuracyState.Reset();
            XPerfectOnlyState.ShouldFailPlayer = false;
            CounterDisplay.Refresh();
        }
    }

    [HarmonyPatch(typeof(scrMarginTracker), "RevertToLastCheckpoint")]
    public static class CheckpointRestorePatch
    {
        static void Postfix()
        {
            CounterDisplay.Refresh();
        }
    }


    public static class XPerfectOnlyState
    {
        public static bool ShouldFailPlayer;
        public static HitMargin PendingFailMargin;
    }

    public static class CompetitiveModePrefs
    {
        public static bool runtimeDisabled = false;
        public static bool enableCompetitiveMode
        {
            get
            {
                return Persistence.generalPrefs.GetBool("enableCompetitiveMode", false) && !CompetitiveModePrefs.runtimeDisabled;
            }
        }
    }

    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    [HarmonyPriority(Priority.Low)]
    public static class XPerfectOnlySwitchChosenPatch
    {
        static void Postfix()
        {
            try
            {
                if (!XPerfectOnlyState.ShouldFailPlayer) return;

                HitMargin failedMargin = XPerfectOnlyState.PendingFailMargin;

                if (!CompetitiveModePrefs.enableCompetitiveMode) return;
                if (!Main.Enabled || !Main.Settings.XPerfectOnly) return;

                var ctrl = scrController.instance;
                if (ctrl == null || ctrl.playerOne == null || !ctrl.playerOne.alive) return;

                string failMessage = RDString.Get("status.purePerfectOnlyExplainer", new Dictionary<string, object>
            {
                { "judgement", failedMargin.ToLocalized(null) }
            });

                ctrl.playerOne.Die(false, false, failMessage, false);
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[XPerfect] XPerfectOnlySwitchChosenPatch error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HitMarginHelper), "IsCounted")]
    [HarmonyPriority(Priority.First)]
    public static class XPerfectOnlyIsCountedPatch
    {
        static void Postfix(HitMargin hitMargin, HitMarginLimit? hitMarginLimit, ref bool __result)
        {
            if (!Main.Enabled || !Main.Settings.XPerfectOnly)
                return;

            if (!XPerfectOnlyState.ShouldFailPlayer)
                return;

            if (hitMargin != HitMargin.XPerfect)
                __result = false;
        }
    }
}