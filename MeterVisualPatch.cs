using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using Image = UnityEngine.UI.Image;

namespace XPerfect
{
    [HarmonyPatch]
    public static class MeterVisualPatch
    {
        private static readonly FieldInfo CachedTickImagesField =
            AccessTools.Field(typeof(scrHitErrorMeter), "cachedTickImages");

        private static readonly FieldInfo TickIndexField =
            AccessTools.Field(typeof(scrHitErrorMeter), "tickIndex");

        private static readonly FieldInfo TickCacheSizeField =
            AccessTools.Field(typeof(scrHitErrorMeter), "tickCacheSize");

        private static readonly FieldInfo MeterShapeField =
            AccessTools.Field(typeof(scrHitErrorMeter), "meterShape");

        private static Sprite straightSprite;
        private static Sprite curvedSprite;
        private static bool loaded = false;

        private static Sprite originalStraightSprite;
        private static Sprite originalCurvedSprite;
        private static bool originalSpritesCaptured = false;

        [HarmonyPatch(typeof(scrHitErrorMeter), "UpdateLayout")]
        [HarmonyPostfix]
        public static void UpdateLayoutPostfix(
            scrHitErrorMeter __instance,
            ErrorMeterSize size = ErrorMeterSize.Normal,
            ErrorMeterShape shape = ErrorMeterShape.Straight)
        {
            try
            {
                if (__instance == null)
                    return;

                CaptureOriginalSprites(__instance);

                ErrorMeterShape actualShape = shape;

                if (!Main.Enabled)
                {
                    RestoreOriginalSprites(__instance);

                    if (__instance.straightMeter != null)
                        __instance.straightMeter.SetActive(actualShape == ErrorMeterShape.Straight);

                    if (__instance.curvedMeter != null)
                        __instance.curvedMeter.SetActive(actualShape == ErrorMeterShape.Curved);

                    return;
                }

                EnsureSpritesLoaded();

                if (__instance.straightMeter != null)
                    __instance.straightMeter.SetActive(actualShape == ErrorMeterShape.Straight);

                if (__instance.curvedMeter != null)
                    __instance.curvedMeter.SetActive(actualShape == ErrorMeterShape.Curved);

                if (__instance.straightMeter != null && straightSprite != null)
                    ReplaceRootImageOnly(__instance.straightMeter, straightSprite);

                if (__instance.curvedMeter != null && curvedSprite != null)
                    ReplaceRootImageOnly(__instance.curvedMeter, curvedSprite);
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[MeterVisualPatch/UpdateLayout] {ex}");
            }
        }

        [HarmonyPatch(typeof(scrHitErrorMeter), "AddHit")]
        [HarmonyPostfix]
        public static void AddHitPostfix(scrHitErrorMeter __instance, float angleDiff, float marginScale = 1f)
        {
            try
            {
                if (!Main.Enabled)
                    return;

                if (__instance == null)
                    return;

                if (scrConductor.instance == null || scrController.instance == null)
                    return;

                Image[] cachedTickImages = CachedTickImagesField.GetValue(__instance) as Image[];
                if (cachedTickImages == null || cachedTickImages.Length == 0)
                    return;

                int tickIndex = (int)TickIndexField.GetValue(__instance);
                int tickCacheSize = (int)TickCacheSizeField.GetValue(__instance);
                ErrorMeterShape meterShape = (ErrorMeterShape)MeterShapeField.GetValue(__instance);

                int justAddedTickIndex = tickIndex - 1;
                if (justAddedTickIndex < 0)
                    justAddedTickIndex = tickCacheSize - 1;

                if (justAddedTickIndex < 0 || justAddedTickIndex >= cachedTickImages.Length)
                    return;

                Image tickImage = cachedTickImages[justAddedTickIndex];
                if (tickImage == null)
                    return;

                float normalizedAngle = GetMeterAngleFromTick(tickImage, meterShape);

                double bpmTimesSpeed = AccuracyMath.GetBpmTimesSpeed();
                double conductorPitch = AccuracyMath.GetConductorPitch();

                double pureBoundaryDeg = scrMisc.GetAdjustedAngleBoundaryInDeg(
                    HitMarginGeneral.Pure,
                    bpmTimesSpeed,
                    conductorPitch,
                    marginScale
                );

                double countedBoundaryDeg = scrMisc.GetAdjustedAngleBoundaryInDeg(
                    HitMarginGeneral.Counted,
                    bpmTimesSpeed,
                    conductorPitch,
                    marginScale
                );

                if (countedBoundaryDeg <= 0.0)
                    return;

                double scale = 60.0 / countedBoundaryDeg;
                double normalizedPureBoundary = pureBoundaryDeg * scale;

                if (normalizedAngle < -normalizedPureBoundary || normalizedAngle > normalizedPureBoundary)
                {
                    return;
                }

                const float xCompress = 0.75f;

                if (AccuracyState.LastJudge == DetailedJudge.XPerfect)
                {
                    float finalAngle = normalizedAngle * xCompress;
                    ApplyTickAngle(tickImage, meterShape, finalAngle);
                }
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[MeterVisualPatch/AddHit] {ex}");
            }
        }

        [HarmonyPatch(typeof(scrHitErrorMeter), "CalculateTickColor")]
        public static class MeterTickColorPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref Color __result, float angle, float marginScale = 1f)
            {
                try
                {
                    if (!Main.Enabled)
                        return true;

                    if (scrController.instance == null || scrConductor.instance == null)
                        return true;

                    double bpmTimesSpeed = AccuracyMath.GetBpmTimesSpeed();
                    double conductorPitch = AccuracyMath.GetConductorPitch();

                    double xPerfectBoundary = AccuracyMath.GetMeterXPerfectBoundaryDeg(
                        bpmTimesSpeed,
                        conductorPitch,
                        marginScale
                    );

                    if (Math.Abs(angle) <= xPerfectBoundary)
                    {
                        __result = new Color(0.3f, 0.8f, 1f, 1f);
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    UnityModManager.Logger.Log($"[MeterTickColorPatch] {ex}");
                    return true;
                }
            }
        }

        private static void CaptureOriginalSprites(scrHitErrorMeter meter)
        {
            if (originalSpritesCaptured || meter == null)
                return;

            if (meter.straightMeter != null)
            {
                Image image = meter.straightMeter.GetComponent<Image>();
                if (image != null)
                    originalStraightSprite = image.sprite;
            }

            if (meter.curvedMeter != null)
            {
                Image image = meter.curvedMeter.GetComponent<Image>();
                if (image != null)
                    originalCurvedSprite = image.sprite;
            }

            originalSpritesCaptured = true;
        }

        private static void RestoreOriginalSprites(scrHitErrorMeter meter)
        {
            if (meter == null)
                return;

            if (meter.straightMeter != null && originalStraightSprite != null)
                ReplaceRootImageOnly(meter.straightMeter, originalStraightSprite);

            if (meter.curvedMeter != null && originalCurvedSprite != null)
                ReplaceRootImageOnly(meter.curvedMeter, originalCurvedSprite);
        }

        private static float GetMeterAngleFromTick(Image tickImage, ErrorMeterShape meterShape)
        {
            if (tickImage == null)
                return 0f;

            if (meterShape == ErrorMeterShape.Curved)
                return tickImage.rectTransform.localEulerAngles.z;

            if (meterShape == ErrorMeterShape.Straight)
                return -tickImage.rectTransform.anchoredPosition.x / 2.5f;

            return 0f;
        }

        private static void ApplyTickAngle(Image tickImage, ErrorMeterShape meterShape, float angle)
        {
            if (tickImage == null)
                return;

            if (meterShape == ErrorMeterShape.Curved)
            {
                tickImage.rectTransform.rotation = Quaternion.Euler(0f, 0f, angle);
                return;
            }

            if (meterShape == ErrorMeterShape.Straight)
            {
                tickImage.rectTransform.anchoredPosition = new Vector2(-angle * 2.5f, -62f);
            }
        }

        private static void ReplaceRootImageOnly(GameObject root, Sprite sprite)
        {
            if (root == null || sprite == null)
                return;

            Image image = root.GetComponent<Image>();
            if (image != null)
                image.sprite = sprite;
        }

        private static void EnsureSpritesLoaded()
        {
            if (loaded)
                return;

            loaded = true;

            try
            {
                string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string straightPath = Path.Combine(modPath, "XStraightMeter.png");
                string curvedPath = Path.Combine(modPath, "XCurvedMeter.png");

                if (File.Exists(straightPath))
                    straightSprite = LoadSprite(straightPath);

                if (File.Exists(curvedPath))
                    curvedSprite = LoadSprite(curvedPath);
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[MeterVisualPatch/EnsureSpritesLoaded] {ex}");
            }
        }

        private static Sprite LoadSprite(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);

            if (!texture.LoadImage(bytes))
                return null;

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            return Sprite.Create(texture, rect, pivot, 100f);
        }
        public static void RefreshAllMeters()
        {
            try
            {
                scrHitErrorMeter[] meters = UnityEngine.Object.FindObjectsOfType<scrHitErrorMeter>(true);
                if (meters == null || meters.Length == 0)
                    return;

                foreach (var meter in meters)
                {
                    if (meter == null)
                        continue;

                    try
                    {
                        ApplyCurrentVisualState(meter);
                    }
                    catch (Exception ex)
                    {
                        UnityModManager.Logger.Log($"[MeterVisualPatch/RefreshAllMeters] {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[MeterVisualPatch/RefreshAllMeters/Outer] {ex}");
            }
        }

        private static void ApplyCurrentVisualState(scrHitErrorMeter meter)
        {
            if (meter == null)
                return;

            CaptureOriginalSprites(meter);

            if (!Main.Enabled)
            {
                RestoreOriginalSprites(meter);
                return;
            }

            EnsureSpritesLoaded();

            if (meter.straightMeter != null && straightSprite != null)
                ReplaceRootImageOnly(meter.straightMeter, straightSprite);

            if (meter.curvedMeter != null && curvedSprite != null)
                ReplaceRootImageOnly(meter.curvedMeter, curvedSprite);
        }
    }
}