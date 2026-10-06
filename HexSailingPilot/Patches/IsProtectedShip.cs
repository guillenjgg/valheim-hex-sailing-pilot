using HarmonyLib;
using HexSailingPilot.Navigation;

namespace HexSailingPilot.Patches
{
    [HarmonyPatch]
    internal static class DamageProtectionPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        private static bool Character_Damage_Prefix(Character __instance)
        {
            if (!SailingPilotController.IsActive())
            {
                return true;
            }

            if (__instance != Player.m_localPlayer)
            {
                return true;
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        private static bool WearNTear_Damage_Prefix(WearNTear __instance)
        {
            if (!SailingPilotController.IsActive())
            {
                return true;
            }

            Ship ship = __instance.GetComponentInParent<Ship>();

            if (!SailingPilotController.IsProtectedShip(ship))
            {
                return true;
            }

            return false;
        }
    }
}