using HarmonyLib;
using HexSailingPilot.Navigation;
using UnityEngine;

namespace HexSailingPilot.Patches
{
    [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.ApplyControlls))]
    internal static class ShipControllsPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ShipControlls __instance, ref Vector3 moveDir)
        {
            SailingPilotController.ApplyControls(__instance.m_ship, moveDir, ref moveDir);
        }
    }
}