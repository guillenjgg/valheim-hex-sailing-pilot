using HarmonyLib;
using HexSailingPilot.Navigation;
using UnityEngine;

namespace HexSailingPilot.Patches
{
    [HarmonyPatch(typeof(Chat), nameof(Chat.SendPing))]
    internal static class ChatPatch
    {
        [HarmonyPostfix]
        private static void SendPingPostfix(Vector3 position)
        {
            MapDestinationService.SetDestination(position);
        }
    }
}