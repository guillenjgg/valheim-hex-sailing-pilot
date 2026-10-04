using HarmonyLib;
using UnityEngine;
using HexSailingPilot.Navigation;
using HexSailingPilot.ShipAccess;

namespace HexSailingPilot
{
    internal static class SailingPilotController
    {
        private const float TestDestinationDistance = 150f;
        private const float ArrivalDistance = 10f;
        private const float ManualSteeringThreshold = 0.5f;
        private const float ManualSpeedThreshold = 0.5f;

        private enum PilotStateEnum
        {
            Inactive,
            Sailing,
            Stopping,
            Complete
        }

        private static Ship _lastShip;
        private static PilotStateEnum _state = PilotStateEnum.Inactive;
        private static Ship _controlledShip;
        private static SailingCourseModel _course;

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

            var rudderValue = ShipAccessor.RudderValue(ship);

            if (_state == PilotStateEnum.Stopping)
            {
                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            var distanceToDestination = SteeringCalculator.GetDistanceToDestination(_course, ship.transform.position);

            if (distanceToDestination <= ArrivalDistance)
            {
                _state = PilotStateEnum.Stopping;

                Plugin.Log.LogInfo(
                    $"Pilot arrived | " +
                    $"Distance: {distanceToDestination:F1}m | " +
                    $"ShipSpeed: {ShipAccessor.ShipSpeed(ship)}");

                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            moveDir.z = 1f;

            var lookAheadPoint = SteeringCalculator.GetLookAheadPoint(_course, ship.transform.position);
            var directionToTarget = lookAheadPoint - ship.transform.position;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude <= 0.001f)
            {
                moveDir.x = 0f;
                return;
            }

            var targetHeading = SteeringCalculator.GetHeading(directionToTarget);
            var currentHeading = SteeringCalculator.GetHeading(ship.transform.forward);
            var headingError = Mathf.DeltaAngle(currentHeading, targetHeading);
            var targetRudder = SteeringCalculator.GetTargetRudder(headingError);

            SteeringCalculator.SetRudderInput(targetRudder, rudderValue, ref moveDir);

            var crossTrackError = SteeringCalculator.GetCrossTrackError(_course, ship.transform.position);

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
                BrakeShip(ship);

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
                BrakeShip(ship);
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

            if (ShipAccessor.StopMethod == null)
            {
                Plugin.Log.LogWarning("Ship.Stop method was not found.");
                return;
            }

            ShipAccessor.StopMethod.Invoke(ship, null);

            Plugin.Log.LogInfo(
                $"Ship stop requested | " +
                $"ShipSpeed: {ShipAccessor.ShipSpeed(ship)} | " +
                $"Rudder: {ShipAccessor.RudderValue(ship):+0.00;-0.00;0.00}");
        }

        private static void BrakeShip(Ship ship)
        {
            var body = ShipAccessor.ShipBody(ship);

            if (body == null)
            {
                return;
            }

            var velocity = body.linearVelocity;
            velocity.x *= 0.1f;
            velocity.z *= 0.1f;
            body.linearVelocity = velocity;
        }

        private static void TakeManualControl(Ship ship, Vector3 playerMoveDir)
        {
            ShipAccessor.ForwardPressed(ship) = false;
            ShipAccessor.BackwardPressed(ship) = false;

            Plugin.Log.LogInfo(
                $"Pilot manual takeover | " +
                $"PlayerInput: ({playerMoveDir.x:+0.00;-0.00;0.00}, {playerMoveDir.z:+0.00;-0.00;0.00}) | " +
                $"ShipSpeed: {ShipAccessor.ShipSpeed(ship)} | " +
                $"Rudder: {ShipAccessor.RudderValue(ship):+0.00;-0.00;0.00}");

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
            SteeringCalculator.SetRudderInput(0f, rudderValue, ref moveDir);

            if (ShipAccessor.ShipSpeed(ship) != Ship.Speed.Stop)
            {
                moveDir.z = -1f;

                Plugin.Log.LogInfo(
                    $"Pilot stopping | " +
                    $"Distance: {SteeringCalculator.GetDistanceToDestination(_course, ship.transform.position):F1}m | " +
                    $"ShipSpeed: {ShipAccessor.ShipSpeed(ship)} | " +
                    $"Rudder: {rudderValue:+0.00;-0.00;0.00}");

                return;
            }

            moveDir.z = 0f;

            if (Mathf.Abs(rudderValue) > SteeringCalculator.RudderTolerance)
            {
                Plugin.Log.LogInfo(
                    $"Pilot centering rudder | " +
                    $"Distance: {SteeringCalculator.GetDistanceToDestination(_course, ship.transform.position):F1}m | " +
                    $"Rudder: {rudderValue:+0.00;-0.00;0.00} | " +
                    $"Input: {moveDir.x:+0.0;-0.0;0.0}");

                return;
            }

            moveDir.x = 0f;
            _state = PilotStateEnum.Complete;

            Plugin.Log.LogInfo(
                $"Pilot complete | " +
                $"Distance: {SteeringCalculator.GetDistanceToDestination(_course, ship.transform.position):F1}m | " +
                $"ShipSpeed: {ShipAccessor.ShipSpeed(ship)} | " +
                $"Rudder: {rudderValue:+0.00;-0.00;0.00}");
        }

        private static void StartCourse(Ship ship)
        {
            _lastShip = ship;
            _state = PilotStateEnum.Sailing;

            _course = new SailingCourseModel(ship.transform.position, ship.transform.forward, TestDestinationDistance);

            Plugin.Log.LogInfo(
                $"Pilot course started | " +
                $"Origin: {_course.Origin} | " +
                $"Destination: {_course.Destination} | " +
                $"Distance: {_course.Length:F0}m | " +
                $"Heading: {SteeringCalculator.GetHeading(_course.Direction):F1}°");
        }

        private static void ClearCourse()
        {
            _course = null;
        }
    }
}