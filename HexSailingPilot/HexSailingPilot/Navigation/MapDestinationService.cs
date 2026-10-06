using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class MapDestinationService
    {
        internal static Vector3? Destination { get; private set; }

        internal static void SetDestination(Vector3 position)
        {
            Destination = position;

            Plugin.Log.LogInfo(
                $"Map destination set | " +
                $"Position: {position}");
        }

        internal static void ClearDestination()
        {
            Destination = null;
        }
    }
}