using HexSailingPilot.ShipAccess;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class SailingPropulsionController
    {
        private const float MinimumSailingWindFactor = 0.05f;
        private const float PropulsionModeDebounce = 2f;

        private static bool _releaseSpeedInput;
        private static Ship.Speed _targetSpeed = Ship.Speed.Stop;
        private static float _nextTargetSpeedChangeTime;

        internal static void Apply(Ship ship, ref Vector3 moveDir)
        {
            var requestedSpeed = GetTargetSpeed(ship);

            if (_targetSpeed == Ship.Speed.Stop)
            {
                _targetSpeed = requestedSpeed;
                _nextTargetSpeedChangeTime = Time.time + PropulsionModeDebounce;
            }
            else if (requestedSpeed != _targetSpeed &&
                     Time.time >= _nextTargetSpeedChangeTime)
            {
                Plugin.Log.LogInfo(
                    $"Propulsion mode changed | " +
                    $"{_targetSpeed} -> {requestedSpeed}");

                _targetSpeed = requestedSpeed;
                _nextTargetSpeedChangeTime = Time.time + PropulsionModeDebounce;
            }

            ApplySpeed(ship, _targetSpeed, ref moveDir);
        }

        internal static void Stop(Ship ship, ref Vector3 moveDir)
        {
            _targetSpeed = Ship.Speed.Stop;
            _nextTargetSpeedChangeTime = 0f;

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