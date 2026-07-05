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
        private static readonly AccessTools.FieldRef<scrHitErrorMeter, Image[]> GetCachedTickImages =
            AccessTools.FieldRefAccess<scrHitErrorMeter, Image[]>("cachedTickImages");

        private static readonly AccessTools.FieldRef<scrHitErrorMeter, int> GetTickIndex =
            AccessTools.FieldRefAccess<scrHitErrorMeter, int>("tickIndex");

        private static readonly AccessTools.FieldRef<scrHitErrorMeter, int> GetTickCacheSize =
            AccessTools.FieldRefAccess<scrHitErrorMeter, int>("tickCacheSize");

        private static readonly AccessTools.FieldRef<scrHitErrorMeter, ErrorMeterShape> GetMeterShape =
            AccessTools.FieldRefAccess<scrHitErrorMeter, ErrorMeterShape>("meterShape");

        private static Sprite straightSprite;
        private static Sprite curvedSprite;

        private static Sprite originalStraightSprite;
        private static Sprite originalCurvedSprite;

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
        [HarmonyPriority(Priority.Normal)]
        [HarmonyPostfix]
        public static void AddHitPostfix(scrHitErrorMeter __instance, float angleDiff, float marginScale = 1f, scrPlanet planet = null, scrFloor hitFloor = null)
        {
            if (!Main.Enabled)
                return;

            if (__instance == null)
                return;

            if (scrConductor.instance == null || scrController.instance == null)
                return;

            Image[] cachedTickImages = GetCachedTickImages(__instance);
            if (cachedTickImages == null || cachedTickImages.Length == 0)
                return;

            int tickIndex = GetTickIndex(__instance);
            int tickCacheSize = GetTickCacheSize(__instance);
            ErrorMeterShape meterShape = GetMeterShape(__instance);

            int justAddedTickIndex = tickIndex - 1;
            if (justAddedTickIndex < 0)
                justAddedTickIndex = tickCacheSize - 1;

            if (justAddedTickIndex < 0 || justAddedTickIndex >= cachedTickImages.Length)
                return;

            Image tickImage = cachedTickImages[justAddedTickIndex];
            if (tickImage == null)
                return;

            float normalizedAngle = GetMeterAngleFromTick(tickImage, meterShape);

            float? floorSpeedAdd = (hitFloor ?? planet?.player?.currFloor?.prevfloor)?.speed;
            double bpmTimesSpeed = scrConductor.instance.bpm * (floorSpeedAdd ?? 1.0);
            double conductorPitch = scrConductor.instance.song.pitch;

            double pureBoundaryDeg = scrMisc.GetAdjustedAngleBoundaryInDeg(
                HitMarginGeneral.Pure, bpmTimesSpeed, conductorPitch, marginScale);

            double countedBoundaryDeg = scrMisc.GetAdjustedAngleBoundaryInDeg(
                HitMarginGeneral.Counted, bpmTimesSpeed, conductorPitch, marginScale);

            if (countedBoundaryDeg <= 0.0)
                return;

            double scale = 60.0 / countedBoundaryDeg;
            double normalizedPureBoundary = pureBoundaryDeg * scale;

            if (normalizedAngle < -normalizedPureBoundary || normalizedAngle > normalizedPureBoundary)
                return;

            const float xCompress = 0.75f;

            double xPerfectMeterBoundary = AccuracyMath.GetMeterXPerfectBoundaryDeg(
                bpmTimesSpeed, conductorPitch, countedBoundaryDeg);

            if (Math.Abs(normalizedAngle) <= xPerfectMeterBoundary)
            {
                float finalAngle = normalizedAngle * xCompress;
                ApplyTickAngle(tickImage, meterShape, finalAngle);
            }
        }

        [HarmonyPatch(typeof(scrHitErrorMeter), "CalculateTickColor")]
        public static class MeterTickColorPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref Color __result, float angle, float marginScale = 1f, scrFloor hitFloor = null)
            {
                if (!Main.Enabled)
                    return true;

                if (scrController.instance == null || scrConductor.instance == null)
                    return true;

                double bpmTimesSpeed = scrConductor.instance.bpm * ((hitFloor ?? scrController.instance.playerOne?.currFloor?.prevfloor)?.speed ?? 1.0);
                double conductorPitch = scrConductor.instance.song.pitch;

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
        }

        private static void CaptureOriginalSprites(scrHitErrorMeter meter)
        {
            if (meter == null) return;

            if (meter.straightMeter != null)
            {
                Image image = meter.straightMeter.GetComponent<Image>();
                if (image != null && image.sprite != straightSprite)
                    originalStraightSprite = image.sprite;
            }

            if (meter.curvedMeter != null)
            {
                Image image = meter.curvedMeter.GetComponent<Image>();
                if (image != null && image.sprite != curvedSprite)
                    originalCurvedSprite = image.sprite;
            }
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
            try
            {
                string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                if (straightSprite == null)
                {
                    string straightPath = Path.Combine(modPath, "XStraightMeter.png");
                    straightSprite = LoadSprite(straightPath);
                }

                if (curvedSprite == null)
                {
                    string curvedPath = Path.Combine(modPath, "XCurvedMeter.png");
                    curvedSprite = LoadSprite(curvedPath);
                }
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

            var imageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (imageConversionType == null)
            {
                UnityModManager.Logger.Log("[MeterVisualPatch] ImageConversion type not found");
                return null;
            }
            var loadImage = imageConversionType.GetMethod(
                "LoadImage",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null,
                new System.Type[] { typeof(Texture2D), typeof(byte[]) },
                null
            );
            if (loadImage == null)
            {
                UnityModManager.Logger.Log("[MeterVisualPatch] LoadImage method not found");
                return null;
            }
            bool success = (bool)loadImage.Invoke(null, new object[] { texture, bytes });
            if (!success)
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
                scrHitErrorMeter[] meters = UnityEngine.Object.FindObjectsByType<scrHitErrorMeter>(FindObjectsSortMode.None);
                if (meters == null || meters.Length == 0)
                    return;

                foreach (var meter in meters)
                {
                    if (meter == null) continue;
                    try
                    {
                        if (!Main.Enabled)
                        {
                            CaptureOriginalSprites(meter);
                            RestoreOriginalSprites(meter);
                        }
                        else
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