using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace HexSailingPilot
{
    internal static class SailingPilotController
    {
        private const float TestDestinationDistance = 150f;
        private const float ArrivalDistance = 10f;
        private const float LookAheadDistance = 50f;
        private const float HeadingTolerance = 2f;
        private const float FullRudderHeadingError = 20f;
        private const float RudderTolerance = 0.02f;
        private const float ManualSteeringThreshold = 0.5f;
        private const float ManualSpeedThreshold = 0.5f;

        private enum PilotStateEnum
        {
            Inactive,
            Sailing,
            Stopping,
            Complete
        }

        private static readonly AccessTools.FieldRef<Ship, float> RudderValue = AccessTools.FieldRefAccess<Ship, float>("m_rudderValue");
        private static readonly AccessTools.FieldRef<Ship, Ship.Speed> ShipSpeed = AccessTools.FieldRefAccess<Ship, Ship.Speed>("m_speed");
        private static readonly AccessTools.FieldRef<Ship, bool> ForwardPressed = AccessTools.FieldRefAccess<Ship, bool>("m_forwardPressed");
        private static readonly AccessTools.FieldRef<Ship, bool> BackwardPressed = AccessTools.FieldRefAccess<Ship, bool>("m_backwardPressed");
        private static readonly MethodInfo StopMethod = AccessTools.Method(typeof(Ship), "Stop");

        private static Ship _lastShip;
        private static Vector3 _courseOrigin;
        private static Vector3 _courseDirection;
        private static Vector3 _destination;
        private static float _courseLength;
        private static PilotStateEnum _state = PilotStateEnum.Inactive;
        private static Ship _controlledShip;

        internal static void ApplyControls(Ship ship, Vector3 playerMoveDir, ref Vector3 moveDir)
        {
            if (ship == null)
            {
                return;
            }

            _controlledShip = ship;

            if (!IsActive())
            {
                return;
            }

            if (_lastShip != ship)
            {
                Disengage();
                return;
            }

            if (HasManualInput(playerMoveDir))
            {
                TakeManualControl(ship, playerMoveDir);
                return;
            }

            var rudderValue = RudderValue(ship);

            if (_state == PilotStateEnum.Stopping)
            {
                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            var distanceToDestination = GetDistanceToDestination(ship);

            if (distanceToDestination <= ArrivalDistance)
            {
                _state = PilotStateEnum.Stopping;

                Plugin.Log.LogInfo(
                    $"Pilot arrived | " +
                    $"Distance: {distanceToDestination:F1}m | " +
                    $"ShipSpeed: {ShipSpeed(ship)}");

                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            moveDir.z = 1f;

            var lookAheadPoint = GetLookAheadPoint(ship);
            var directionToTarget = lookAheadPoint - ship.transform.position;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude <= 0.001f)
            {
                moveDir.x = 0f;
                return;
            }

            var targetHeading = GetHeading(directionToTarget);
            var currentHeading = GetHeading(ship.transform.forward);
            var headingError = Mathf.DeltaAngle(currentHeading, targetHeading);
            var targetRudder = GetTargetRudder(headingError);

            SetRudderInput(targetRudder, rudderValue, ref moveDir);

            var crossTrackError = GetCrossTrackError(ship.transform.position);

            Plugin.Log.LogInfo(
                $"Pilot course | " +
                $"Distance: {distanceToDestination:F1}m | " +
                $"Target: {targetHeading:F1}° | " +
                $"Current: {currentHeading:F1}° | " +
                $"Error: {headingError:+0.0;-0.0;0.0}° | " +
                $"CrossTrack: {crossTrackError:+0.0;-0.0;0.0}m | " +
                $"TargetRudder: {targetRudder:+0.00;-0.00;0.00} | " +
                $"Rudder: {rudderValue:+0.00;-0.00;0.00} | " +
                $"Input: {moveDir.x:+0.0;-0.0;0.0}");
        }

        internal static bool IsActive()
        {
            return _state == PilotStateEnum.Sailing ||
                   _state == PilotStateEnum.Stopping;
        }

        internal static void Toggle(Ship ship)
        {
            if (ship == null)
            {
                return;
            }

            if (IsActive())
            {
                Disengage();
                StopShip(ship);

                Plugin.Log.LogInfo("Autopilot disengaged by hotkey.");
                return;
            }

            StartCourse(ship);
        }

        internal static void Stop(Ship ship)
        {
            Disengage();

            if (ship != null)
            {
                StopShip(ship);
            }
        }

        internal static void Toggle()
        {
            if (_controlledShip == null)
            {
                return;
            }

            Toggle(_controlledShip);
        }

        private static void StopShip(Ship ship)
        {
            if (ship == null)
            {
                return;
            }

            if (StopMethod == null)
            {
                Plugin.Log.LogWarning("Ship.Stop method was not found.");
                return;
            }

            StopMethod.Invoke(ship, null);

            Plugin.Log.LogInfo(
                $"Ship stop requested | " +
                $"ShipSpeed: {ShipSpeed(ship)} | " +
                $"Rudder: {RudderValue(ship):+0.00;-0.00;0.00}");
        }

        private static void TakeManualControl(Ship ship, Vector3 playerMoveDir)
        {
            ForwardPressed(ship) = false;
            BackwardPressed(ship) = false;

            Plugin.Log.LogInfo(
                $"Pilot manual takeover | " +
                $"PlayerInput: ({playerMoveDir.x:+0.00;-0.00;0.00}, {playerMoveDir.z:+0.00;-0.00;0.00}) | " +
                $"ShipSpeed: {ShipSpeed(ship)} | " +
                $"Rudder: {RudderValue(ship):+0.00;-0.00;0.00}");

            Disengage();
        }

        private static void Disengage()
        {
            ClearCourse();

            _lastShip = null;
            _state = PilotStateEnum.Inactive;

            Plugin.Log.LogInfo("Pilot disengaged");
        }

        private static bool HasManualInput(Vector3 playerMoveDir)
        {
            return Mathf.Abs(playerMoveDir.x) > ManualSteeringThreshold ||
                   playerMoveDir.z > ManualSpeedThreshold ||
                   playerMoveDir.z < -ManualSpeedThreshold;
        }

        private static void ApplyStop(Ship ship, float rudderValue, ref Vector3 moveDir)
        {
            SetRudderInput(0f, rudderValue, ref moveDir);

            if (ShipSpeed(ship) != Ship.Speed.Stop)
            {
                moveDir.z = -1f;

                Plugin.Log.LogInfo(
                    $"Pilot stopping | " +
                    $"Distance: {GetDistanceToDestination(ship):F1}m | " +
                    $"ShipSpeed: {ShipSpeed(ship)} | " +
                    $"Rudder: {rudderValue:+0.00;-0.00;0.00}");

                return;
            }

            moveDir.z = 0f;

            if (Mathf.Abs(rudderValue) > RudderTolerance)
            {
                Plugin.Log.LogInfo(
                    $"Pilot centering rudder | " +
                    $"Distance: {GetDistanceToDestination(ship):F1}m | " +
                    $"Rudder: {rudderValue:+0.00;-0.00;0.00} | " +
                    $"Input: {moveDir.x:+0.0;-0.0;0.0}");

                return;
            }

            moveDir.x = 0f;
            _state = PilotStateEnum.Complete;

            Plugin.Log.LogInfo(
                $"Pilot complete | " +
                $"Distance: {GetDistanceToDestination(ship):F1}m | " +
                $"ShipSpeed: {ShipSpeed(ship)} | " +
                $"Rudder: {rudderValue:+0.00;-0.00;0.00}");
        }

        private static void StartCourse(Ship ship)
        {
            _lastShip = ship;
            _state = PilotStateEnum.Sailing;

            _courseOrigin = ship.transform.position;

            _courseDirection = ship.transform.forward;
            _courseDirection.y = 0f;
            _courseDirection.Normalize();

            _destination = _courseOrigin + _courseDirection * TestDestinationDistance;
            _courseLength = TestDestinationDistance;

            Plugin.Log.LogInfo(
                $"Pilot course started | " +
                $"Origin: {_courseOrigin} | " +
                $"Destination: {_destination} | " +
                $"Distance: {_courseLength:F0}m | " +
                $"Heading: {GetHeading(_courseDirection):F1}°");
        }

        private static void ClearCourse()
        {
            _courseOrigin = Vector3.zero;
            _courseDirection = Vector3.zero;
            _destination = Vector3.zero;
            _courseLength = 0f;
        }

        private static Vector3 GetLookAheadPoint(Ship ship)
        {
            var fromOrigin = ship.transform.position - _courseOrigin;
            fromOrigin.y = 0f;

            var distanceAlongCourse = Vector3.Dot(fromOrigin, _courseDirection);
            var lookAheadDistance = Mathf.Min(distanceAlongCourse + LookAheadDistance, _courseLength);

            return _courseOrigin + _courseDirection * lookAheadDistance;
        }

        private static float GetDistanceToDestination(Ship ship)
        {
            var offset = _destination - ship.transform.position;
            offset.y = 0f;

            return offset.magnitude;
        }

        private static float GetCrossTrackError(Vector3 position)
        {
            var fromOrigin = position - _courseOrigin;
            fromOrigin.y = 0f;

            return Vector3.Dot(fromOrigin, Vector3.Cross(Vector3.up, _courseDirection));
        }

        private static float GetTargetRudder(float headingError)
        {
            if (Mathf.Abs(headingError) <= HeadingTolerance)
            {
                return 0f;
            }

            return Mathf.Clamp(headingError / FullRudderHeadingError, -1f, 1f);
        }

        private static void SetRudderInput(float targetRudder, float rudderValue, ref Vector3 moveDir)
        {
            var rudderError = targetRudder - rudderValue;

            if (Mathf.Abs(rudderError) <= RudderTolerance)
            {
                moveDir.x = 0f;
                return;
            }

            moveDir.x = Mathf.Sign(rudderError);
        }

        private static float GetHeading(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return 0f;
            }

            direction.Normalize();

            var heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            return NormalizeHeading(heading);
        }

        private static float NormalizeHeading(float heading)
        {
            heading %= 360f;

            if (heading < 0f)
            {
                heading += 360f;
            }

            return heading;
        }
    }
}