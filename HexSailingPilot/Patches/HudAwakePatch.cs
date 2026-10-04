using HarmonyLib;
using TMPro;
using UnityEngine;

namespace HexSailingPilot.Patches
{
    [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
    internal static class HudAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Hud __instance)
        {
            if (__instance.m_shipControlsRoot == null)
            {
                return;
            }

            var hoverName = FindHoverName(__instance);

            if (hoverName == null)
            {
                Plugin.Log.LogWarning("Autopilot HUD | HoverName text not found.");
                return;
            }

            var textObject = new GameObject("HexSailingPilot_AutoPilotText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(__instance.m_shipControlsRoot.transform, false);

            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -10f);
            rectTransform.sizeDelta = new Vector2(250f, 30f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = $"[{Plugin.AutoPilotHotKey.Value}] Autopilot";
            text.font = hoverName.font;
            text.fontSharedMaterial = hoverName.fontSharedMaterial;
            text.fontSize = hoverName.fontSize;
            text.fontStyle = hoverName.fontStyle;
            text.color = hoverName.color;
            text.alignment = TextAlignmentOptions.Center;

            Plugin.Log.LogInfo($"Autopilot HUD created | Font: {text.font.name} | Material: {text.fontSharedMaterial.name}");
        }

        private static TextMeshProUGUI FindHoverName(Hud hud)
        {
            var texts = hud.GetComponentsInChildren<TextMeshProUGUI>(true);

            foreach (var text in texts)
            {
                if (text.gameObject.name == "HoverName")
                {
                    return text;
                }
            }

            return null;
        }
    }
}