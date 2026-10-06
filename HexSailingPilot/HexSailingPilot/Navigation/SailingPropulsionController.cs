using HexSailingPilot.ShipAccess;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class SailingPropulsionController
    {
        private const float MinimumSailingWindFactor = 0.05f;

        private static bool _releaseSpeedInput;

        internal static void Apply(Ship ship, ref Vector3 moveDir)
        {
            ApplySpeed(ship, GetTargetSpeed(ship), ref moveDir);
        }

        internal static void Stop(Ship ship, ref Vector3 moveDir)
        {
            ApplySpeed(ship, Ship.Speed.Stop, ref moveDir);
        }

        private static void ApplySpeed(Ship ship, Ship.Speed targetSpeed, ref Vector3 moveDir)
        {
            if (_releaseSpeedInput)
            {
                moveDir.z = 0f;
                _releaseSpeedInput = false;
                return;
            }

            var currentSpeed = ShipAccessor.ShipSpeed(ship);

            if (currentSpeed == targetSpeed)
            {
                moveDir.z = 0f;
                return;
            }

            moveDir.z = currentSpeed < targetSpeed ? 1f : -1f;
            _releaseSpeedInput = true;
        }

        private static Ship.Speed GetTargetSpeed(Ship ship)
        {
            var windFactor = ship.GetWindAngleFactor();

            if (windFactor < MinimumSailingWindFactor)
            {
                return Ship.Speed.Slow;
            }

            return Ship.Speed.Full;
        }
    }
}