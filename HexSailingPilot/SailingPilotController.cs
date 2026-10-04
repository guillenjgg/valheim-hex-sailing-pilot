using HarmonyLib;
using UnityEngine;

namespace HexSailingPilot
{
    internal static class SailingPilotController
    {
        private const float TestDestinationDistance = 150f;
        private const float ApproachDistance = 30f;
        private const float ArrivalDistance = 5f;
        private const float LookAheadDistance = 50f;
        private const float HeadingTolerance = 2f;
        private const float FullRudderHeadingError = 20f;
        private const float RudderTolerance = 0.02f;

        private enum PilotState
        {
            Sailing,
            Approaching,
            Stopping,
            Complete
        }

        private static readonly AccessTools.FieldRef<Ship, float> RudderValue = AccessTools.FieldRefAccess<Ship, float>("m_rudderValue");

        private static readonly AccessTools.FieldRef<Ship, Ship.Speed> ShipSpeed = AccessTools.FieldRefAccess<Ship, Ship.Speed>("m_speed");

        private static Ship _lastShip;
        private static Vector3 _courseOrigin;
        private static Vector3 _courseDirection;
        private static Vector3 _destination;
        private static float _courseLength;
        private static PilotState _state;

        internal static void ApplyControls(Ship ship, ref Vector3 moveDir)
        {
            if (ship == null)
            {
                return;
            }

            if (_lastShip != ship)
            {
                StartCourse(ship);
            }

            var rudderValue = RudderValue(ship);

            if (_state == PilotState.Complete)
            {
                moveDir.x = 0f;
                moveDir.z = 0f;
                return;
            }

            if (_state == PilotState.Stopping)
            {
                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            var distanceToDestination = GetDistanceToDestination(ship);

            if (distanceToDestination <= ArrivalDistance)
            {
                _state = PilotState.Stopping;

                Plugin.Log.LogInfo(
                    $"Pilot arrived | " +
                    $"Distance: {distanceToDestination:F1}m | " +
                    $"ShipSpeed: {ShipSpeed(ship)} | " +
                    $"Rudder: {rudderValue:+0.00;-0.00;0.00}");

                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            if (_state == PilotState.Sailing &&
                distanceToDestination <= ApproachDistance)
            {
                _state = PilotState.Approaching;

                Plugin.Log.LogInfo(
                    $"Pilot approaching | " +
                    $"Distance: {distanceToDestination:F1}m | " +
                    $"ShipSpeed: {ShipSpeed(ship)}");
            }

            ApplyThrottle(ship, ref moveDir);
            ApplySteering(ship, rudderValue, distanceToDestination, ref moveDir);
        }

        private static void ApplyThrottle(
            Ship ship,
            ref Vector3 moveDir)
        {
            if (_state == PilotState.Sailing)
            {
                moveDir.z = 1f;
                return;
            }

            if (_state == PilotState.Approaching)
            {
                if (ShipSpeed(ship) > Ship.Speed.Slow)
                {
                    moveDir.z = -1f;
                    return;
                }

                if (ShipSpeed(ship) < Ship.Speed.Slow)
                {
                    moveDir.z = 1f;
                    return;
                }

                moveDir.z = 0f;
            }
        }

        private static void ApplySteering(
            Ship ship,
            float rudderValue,
            float distanceToDestination,
            ref Vector3 moveDir)
        {
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

            var headingError = Mathf.DeltaAngle(
                currentHeading,
                targetHeading);

            var targetRudder = GetTargetRudder(headingError);

            SetRudderInput(
                targetRudder,
                rudderValue,
                ref moveDir);

            var crossTrackError =
                GetCrossTrackError(ship.transform.position);

            Plugin.Log.LogInfo(
                $"Pilot course | " +
                $"State: {_state} | " +
                $"Distance: {distanceToDestination:F1}m | " +
                $"Speed: {ShipSpeed(ship)} | " +
                $"Target: {targetHeading:F1}° | " +
                $"Current: {currentHeading:F1}° | " +
                $"Error: {headingError:+0.0;-0.0;0.0}° | " +
                $"CrossTrack: {crossTrackError:+0.0;-0.0;0.0}m | " +
                $"TargetRudder: {targetRudder:+0.00;-0.00;0.00} | " +
                $"Rudder: {rudderValue:+0.00;-0.00;0.00} | " +
                $"Input: {moveDir.x:+0.0;-0.0;0.0}");
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
            _state = PilotState.Complete;

            Plugin.Log.LogInfo(
                $"Pilot complete | " +
                $"Distance: {GetDistanceToDestination(ship):F1}m | " +
                $"ShipSpeed: {ShipSpeed(ship)} | " +
                $"Rudder: {rudderValue:+0.00;-0.00;0.00}");
        }

        private static void StartCourse(Ship ship)
        {
            _lastShip = ship;
            _state = PilotState.Sailing;

            _courseOrigin = ship.transform.position;

            _courseDirection = ship.transform.forward;
            _courseDirection.y = 0f;
            _courseDirection.Normalize();

            _destination =
                _courseOrigin +
                _courseDirection * TestDestinationDistance;

            _courseLength = TestDestinationDistance;

            Plugin.Log.LogInfo(
                $"Pilot course started | " +
                $"Origin: {_courseOrigin} | " +
                $"Destination: {_destination} | " +
                $"Distance: {_courseLength:F0}m | " +
                $"Heading: {GetHeading(_courseDirection):F1}°");
        }

        private static Vector3 GetLookAheadPoint(Ship ship)
        {
            var fromOrigin =
                ship.transform.position -
                _courseOrigin;

            fromOrigin.y = 0f;

            var distanceAlongCourse =
                Vector3.Dot(
                    fromOrigin,
                    _courseDirection);

            var lookAheadDistance =
                Mathf.Min(
                    distanceAlongCourse + LookAheadDistance,
                    _courseLength);

            return
                _courseOrigin +
                _courseDirection * lookAheadDistance;
        }

        private static float GetDistanceToDestination(Ship ship)
        {
            var offset =
                _destination -
                ship.transform.position;

            offset.y = 0f;

            return offset.magnitude;
        }

        private static float GetCrossTrackError(Vector3 position)
        {
            var fromOrigin =
                position -
                _courseOrigin;

            fromOrigin.y = 0f;

            return Vector3.Dot(
                fromOrigin,
                Vector3.Cross(
                    Vector3.up,
                    _courseDirection));
        }

        private static float GetTargetRudder(float headingError)
        {
            if (Mathf.Abs(headingError) <= HeadingTolerance)
            {
                return 0f;
            }

            return Mathf.Clamp(
                headingError / FullRudderHeadingError,
                -1f,
                1f);
        }

        private static void SetRudderInput(
            float targetRudder,
            float rudderValue,
            ref Vector3 moveDir)
        {
            var rudderError =
                targetRudder -
                rudderValue;

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

            var heading =
                Mathf.Atan2(
                    direction.x,
                    direction.z) *
                Mathf.Rad2Deg;

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

        internal static void Stop(Ship ship)
        {
            _lastShip = null;
            _courseOrigin = Vector3.zero;
            _courseDirection = Vector3.zero;
            _destination = Vector3.zero;
            _courseLength = 0f;
            _state = PilotState.Complete;

            if (ship != null)
            {
                ship.Stop();
            }
        }
    }
}