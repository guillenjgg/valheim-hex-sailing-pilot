using UnityEngine;

namespace HexSailingPilot
{
    internal static class SailingPilotController
    {
        private static bool _releaseForward;

        internal static void ApplyControls(Ship ship, ref Vector3 moveDir)
        {
            if (ship == null)
            {
                return;
            }

            moveDir.x = 0f;

            if (ship.GetSpeedSetting() == Ship.Speed.Full)
            {
                moveDir.z = 0f;
                _releaseForward = false;
                return;
            }

            if (_releaseForward)
            {
                moveDir.z = 0f;
                _releaseForward = false;
                return;
            }

            moveDir.z = 1f;
            _releaseForward = true;

            Plugin.Log.LogInfo($"Pilot | Advancing from {ship.GetSpeedSetting()}");
        }

        internal static void Stop(Ship ship)
        {
            _releaseForward = false;

            if (ship == null)
            {
                return;
            }

            ship.Stop();
            Plugin.Log.LogInfo("Pilot | Stopped");
        }
    }
}