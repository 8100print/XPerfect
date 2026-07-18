using UnityEngine;

namespace XPerfect
{
    public static class XPerfectColors
    {
        public static readonly Color32 PlusMinus = new Color32(96, 255, 78, 255);
        public static readonly Color32 XPerfect = new Color32(77, 204, 255, 255);

        public static readonly string PlusMinusHex = ColorUtility.ToHtmlStringRGB(PlusMinus);
        public static readonly string XPerfectHex = ColorUtility.ToHtmlStringRGB(XPerfect);
    }
}