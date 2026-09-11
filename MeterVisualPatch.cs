using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using ADOFAI;
using UnityEngine;
using UnityModManagerNet;
using Image = UnityEngine.UI.Image;

namespace XPerfect
{
    [HarmonyPatch]
    public static class MeterVisualPatch
    {

        private static Sprite straightSprite;
        private static Sprite curvedSprite;
        private static Sprite straightNoOutlineSprite;
        private static Sprite curvedNoOutlineSprite;

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

                if (__instance.straightMeter != null)
                    __instance.straightMeter.SetActive(shape == ErrorMeterShape.Straight);

                if (__instance.curvedMeter != null)
                    __instance.curvedMeter.SetActive(shape == ErrorMeterShape.Curved);

                ApplyCurrentVisualState(__instance);
            }
            catch (Exception ex)
            {
                UnityModManager.Logger.Log($"[MeterVisualPatch/UpdateLayout] {ex}");
            }
        }

        [HarmonyPatch(typeof(Persistence), "set_hitMarginPerfectTick")]
        [HarmonyPostfix]
        public static void HitMarginPerfectTickSetterPostfix()
        {
            RefreshAllMeters();
        }



        private static void CaptureOriginalSprites(scrHitErrorMeter meter)
        {
            if (meter == null) return;

            if (meter.straightMeter != null)
            {
                Image image = meter.straightMeter.GetComponent<Image>();
                if (image != null && !IsXPerfectSprite(image.sprite))
                    originalStraightSprite = image.sprite;
            }

            if (meter.curvedMeter != null)
            {
                Image image = meter.curvedMeter.GetComponent<Image>();
                if (image != null && !IsXPerfectSprite(image.sprite))
                    originalCurvedSprite = image.sprite;
            }
        }

        private static void RestoreOriginalSprites(scrHitErrorMeter meter)
        {
            if (meter == null) return;

            if (meter.straightMeter != null && originalStraightSprite != null)
                ReplaceRootImageOnly(meter.straightMeter, originalStraightSprite);

            if (meter.curvedMeter != null && originalCurvedSprite != null)
                ReplaceRootImageOnly(meter.curvedMeter, originalCurvedSprite);
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

                if (straightNoOutlineSprite == null)
                {
                    string straightNoOutlinePath = Path.Combine(modPath, "XStraightMeterNoOutline.png");
                    straightNoOutlineSprite = LoadSprite(straightNoOutlinePath);
                }

                if (curvedSprite == null)
                {
                    string curvedPath = Path.Combine(modPath, "XCurvedMeter.png");
                    curvedSprite = LoadSprite(curvedPath);
                }

                if (curvedNoOutlineSprite == null)
                {
                    string curvedNoOutlinePath = Path.Combine(modPath, "XCurvedMeterNoOutline.png");
                    curvedNoOutlineSprite = LoadSprite(curvedNoOutlinePath);
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
            bool adopted = false;
            try
            {
                var imageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                if (imageConversionType == null)
                {
                    return null;
                }
                var loadImage = imageConversionType.GetMethod(
                    "LoadImage",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new Type[] { typeof(Texture2D), typeof(byte[]) },
                    null
                );
                if (loadImage == null)
                {
                    return null;
                }
                bool success = (bool)loadImage.Invoke(null, new object[] { texture, bytes });
                if (!success)
                    return null;

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                Rect rect = new Rect(0f, 0f, texture.width, texture.height);
                Vector2 pivot = new Vector2(0.5f, 0.5f);

                adopted = true;
                var spr = Sprite.Create(texture, rect, pivot, 100f);
                spr.name = Path.GetFileNameWithoutExtension(filePath);
                return spr;
            }
            finally
            {
                if (texture != null && !adopted)
                    UnityEngine.Object.Destroy(texture);
            }
        }

        public static void RefreshAllMeters()
        {
            try
            {
                scrHitErrorMeter[] meters = UnityEngine.Object.FindObjectsByType<scrHitErrorMeter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (meters == null || meters.Length == 0)
                    return;

                foreach (var meter in meters)
                {
                    if (meter == null) continue;
                    try
                    {
                        if (!CompetitiveModePrefs.enableCompetitiveMode)
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

            if (!CompetitiveModePrefs.enableCompetitiveMode || IsOnlyPerfectsSetting())
            {
                RestoreOriginalSprites(meter);
                return;
            }

            EnsureSpritesLoaded();

            if (meter.straightMeter != null)
                ReplaceRootImageOnly(meter.straightMeter, GetStraightSprite());

            if (meter.curvedMeter != null)
                ReplaceRootImageOnly(meter.curvedMeter, GetCurvedSprite());
        }

        private static Sprite GetStraightSprite()
        {
            return IsNoOutlineSetting()
                ? straightNoOutlineSprite
                : straightSprite;
        }

        private static Sprite GetCurvedSprite()
        {
            return IsNoOutlineSetting()
                ? curvedNoOutlineSprite
                : curvedSprite;
        }

        private static bool IsNoOutlineSetting()
        {
            return Persistence.hitMarginPerfectTick == HitMarginPerfectTickPreset.ShowXPerfectWithoutOutline;
        }

        private static bool IsOnlyPerfectsSetting()
        {
            return Persistence.hitMarginPerfectTick == HitMarginPerfectTickPreset.Default;
        }

        private static bool IsXPerfectSprite(Sprite sprite)
        {
            return sprite == straightSprite || sprite == curvedSprite ||
                   sprite == straightNoOutlineSprite || sprite == curvedNoOutlineSprite;
        }
    }
}