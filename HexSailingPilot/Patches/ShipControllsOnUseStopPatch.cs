using HarmonyLib;
using HexSailingPilot.Navigation;

[HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.OnUseStop))]
internal static class ShipControllsOnUseStopPatch
{
    [HarmonyPrefix]
    private static void Prefix(ShipControlls __instance)
    {
        SailingPilotController.Stop(__instance.m_ship);
    }
}