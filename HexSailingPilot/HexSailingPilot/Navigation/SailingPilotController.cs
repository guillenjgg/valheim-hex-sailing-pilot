using HexSailingPilot.ShipAccess;
using HexSailingPilot.Terrain;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class SailingPilotController
    {
        private const float ArrivalDistance = 10f;
        private const float ManualSteeringThreshold = 0.5f;
        private const float ManualSpeedThreshold = 0.5f;
        private const float DepthScanInterval = 1f;
        private const float DetourRecheckInterval = 1f;
        private const float MinimumDetourDuration = 3f;
        private const float MinimumDetourDistance = 25f;
        private const int RequiredClearConfirmations = 3;

        private enum PilotStateEnum
        {
            Inactive,
            Sailing,
            Detouring,
            Stopping
        }

        private enum StopReasonEnum
        {
            None,
            Arrival,
            PathBlocked
        }

        private static Ship _lastShip;
        private static PilotStateEnum _state = PilotStateEnum.Inactive;
        private static StopReasonEnum _stopReason = StopReasonEnum.None;
        private static Ship _controlledShip;
        private static SailingCourseModel _course;
        private static Vector3 _detourDirection;
        private static Vector3 _detourStartPosition;
        private static float _nextDepthScanTime;
        private static float _nextDetourRecheckTime;
        private static float _detourStartTime;
        private static int _destinationClearStreak;

        internal static void ApplyControls(Ship ship, Vector3 playerMoveDir, ref Vector3 moveDir)
        {
            if (ship == null)
            {
                return;
            }

            _controlledShip = ship;

            if (IsActive() && CapsizeProtectionController.IsCorrectionNeeded(ship))
            {
                Plugin.Log.LogInfo(
                    $"Capsize protection | " +
                    $"Tilt: {CapsizeProtectionController.GetTiltAngle(ship):F1}°");
            }

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
                _stopReason = StopReasonEnum.Arrival;

                Plugin.Log.LogInfo(
                    $"Pilot arrived | " +
                    $"Distance: {distanceToDestination:F1}m | " +
                    $"ShipSpeed: {ShipAccessor.ShipSpeed(ship)}");

                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            if (_state == PilotStateEnum.Detouring)
            {
                ApplyDetour(ship, rudderValue, ref moveDir);
                return;
            }

            var unsafeWaterDetected = ScanDepth(ship);

            var directionToDestination = _course.Destination - ship.transform.position;
            directionToDestination.y = 0f;

            var obstacleDetected = ShipObstacleScanner.IsDirectionBlocked(
                ship,
                directionToDestination,
                out var obstacleDistance);

            if (unsafeWaterDetected || obstacleDetected)
            {
                if (TryStartDetour(ship))
                {
                    ApplySteering(ship, _detourDirection, rudderValue, ref moveDir);
                    return;
                }

                _state = PilotStateEnum.Stopping;
                _stopReason = StopReasonEnum.PathBlocked;

                Plugin.Log.LogInfo(
                    $"Pilot destination path blocked | " +
                    $"Water: {unsafeWaterDetected} | " +
                    $"Obstacle: {obstacleDetected} | " +
                    $"ObstacleDistance: {obstacleDistance:F1}m | " +
                    $"No detour available");

                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            var lookAheadPoint = SteeringCalculator.GetLookAheadPoint(_course, ship.transform.position);
            var directionToTarget = lookAheadPoint - ship.transform.position;
            directionToTarget.y = 0f;

            ApplySteering(ship, directionToTarget, rudderValue, ref moveDir);
        }

        internal static bool IsActive()
        {
            return _state == PilotStateEnum.Sailing ||
                   _state == PilotStateEnum.Detouring ||
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

        internal static bool IsProtectedShip(Ship ship)
        {
            return IsActive() &&
                   ship != null &&
                   ship == _controlledShip;
        }

        private static void ApplyDetour(Ship ship, float rudderValue, ref Vector3 moveDir)
        {
            if (Time.time >= _nextDetourRecheckTime)
            {
                _nextDetourRecheckTime = Time.time + DetourRecheckInterval;

                var detourElapsed = Time.time - _detourStartTime;
                var detourDistance = GetDetourDistance(ship);
                var pathClearNow = IsDestinationPathClear(ship);

                if (pathClearNow)
                {
                    _destinationClearStreak++;
                }
                else
                {
                    _destinationClearStreak = 0;
                }

                var minimumProgressReached = detourElapsed >= MinimumDetourDuration &&
                                             detourDistance >= MinimumDetourDistance;

                var confirmedClear = minimumProgressReached &&
                                     _destinationClearStreak >= RequiredClearConfirmations;

                if (confirmedClear)
                {
                    _state = PilotStateEnum.Sailing;
                    _detourDirection = Vector3.zero;
                    _detourStartPosition = Vector3.zero;
                    _destinationClearStreak = 0;

                    Plugin.Log.LogInfo(
                        $"Detour complete | " +
                        $"Destination path clear | " +
                        $"Elapsed: {detourElapsed:F1}s | " +
                        $"Distance: {detourDistance:F1}m | " +
                        $"ConfirmedStreak: {RequiredClearConfirmations}");
                    return;
                }

                if (!IsDirectionSafe(ship, _detourDirection))
                {
                    if (!TryStartDetour(ship))
                    {
                        _state = PilotStateEnum.Stopping;
                        _stopReason = StopReasonEnum.PathBlocked;

                        Plugin.Log.LogInfo("Detour blocked | No alternate safe direction");

                        ApplyStop(ship, rudderValue, ref moveDir);
                        return;
                    }
                }
            }

            ApplySteering(ship, _detourDirection, rudderValue, ref moveDir);
        }

        private static bool TryStartDetour(Ship ship)
        {
            if (!DetourDirectionSelector.TrySelect(
                ship,
                _course.Destination,
                out var detourDirection,
                out var detourAngle))
            {
                return false;
            }

            _detourDirection = detourDirection;
            _detourStartPosition = ship.transform.position;
            _state = PilotStateEnum.Detouring;
            _nextDetourRecheckTime = Time.time + DetourRecheckInterval;
            _detourStartTime = Time.time;
            _destinationClearStreak = 0;

            Plugin.Log.LogInfo(
                $"Detour started | " +
                $"Angle: {detourAngle:F0}° | " +
                $"Heading: {SteeringCalculator.GetHeading(_detourDirection):F1}°");

            return true;
        }

        private static float GetDetourDistance(Ship ship)
        {
            var offset = ship.transform.position - _detourStartPosition;
            offset.y = 0f;

            return offset.magnitude;
        }

        private static bool IsDestinationPathClear(Ship ship)
        {
            var directionToDestination = _course.Destination - ship.transform.position;
            directionToDestination.y = 0f;

            if (directionToDestination.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            if (ShipObstacleScanner.IsDirectionBlocked(ship, directionToDestination, out _))
            {
                return false;
            }

            return IsDepthPathSafe(ship, directionToDestination);
        }

        private static bool IsDirectionSafe(Ship ship, Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            if (ShipObstacleScanner.IsDirectionBlocked(ship, direction, out _))
            {
                return false;
            }

            return IsDepthPathSafe(ship, direction);
        }

        private static bool IsDepthPathSafe(Ship ship, Vector3 direction)
        {
            var results = WaterDepthScanner.ScanDirection(ship, direction);

            if (results == null || results.Count == 0)
            {
                return false;
            }

            foreach (var result in results)
            {
                if (!result.HasDepth || !result.IsSafe)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ApplySteering(Ship ship, Vector3 direction, float rudderValue, ref Vector3 moveDir)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                moveDir.x = 0f;
                return;
            }

            SailingPropulsionController.Apply(ship, ref moveDir);

            var targetHeading = SteeringCalculator.GetHeading(direction);
            var currentHeading = SteeringCalculator.GetHeading(ship.transform.forward);
            var headingError = Mathf.DeltaAngle(currentHeading, targetHeading);
            var targetRudder = SteeringCalculator.GetTargetRudder(headingError);

            SteeringCalculator.SetRudderInput(targetRudder, rudderValue, ref moveDir);
        }

        private static bool ScanDepth(Ship ship)
        {
            if (Time.time < _nextDepthScanTime)
            {
                return false;
            }

            _nextDepthScanTime = Time.time + DepthScanInterval;

            var directionToDestination = _course.Destination - ship.transform.position;
            directionToDestination.y = 0f;

            if (directionToDestination.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            var results = WaterDepthScanner.ScanDirection(ship, directionToDestination);

            foreach (var result in results)
            {
                if (result.HasDepth)
                {
                    Plugin.Log.LogInfo(
                        $"Destination depth scan | " +
                        $"Distance: {result.Distance:F0}m | " +
                        $"Depth: {result.Depth:F1}m | " +
                        $"Safe: {result.IsSafe}");
                }
            }

            var nearestResult = results[0];

            return nearestResult.HasDepth && !nearestResult.IsSafe;
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
                $"PlayerInput: ({playerMoveDir.x:+0.00;-0.00;0.00}, {playerMoveDir.z:+0.00;-0.00;0.00})");

            Disengage();
        }

        private static void Disengage()
        {
            ClearCourse();

            _lastShip = null;
            _state = PilotStateEnum.Inactive;
            _stopReason = StopReasonEnum.None;
            _detourDirection = Vector3.zero;
            _detourStartPosition = Vector3.zero;
            _destinationClearStreak = 0;

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

            SailingPropulsionController.Stop(ship, ref moveDir);

            if (ShipAccessor.ShipSpeed(ship) != Ship.Speed.Stop)
            {
                return;
            }

            var body = ShipAccessor.ShipBody(ship);

            if (body != null)
            {
                var horizontalVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
                var horizontalSpeed = horizontalVelocity.magnitude;

                if (horizontalSpeed > 0.1f)
                {
                    BrakeShip(ship);
                    return;
                }
            }

            if (Mathf.Abs(rudderValue) > SteeringCalculator.RudderTolerance)
            {
                return;
            }

            moveDir.x = 0f;

            if (_stopReason == StopReasonEnum.PathBlocked)
            {
                Plugin.Log.LogInfo("Pilot stopped | No safe detour available");

                ClearCourse();
                _lastShip = null;
                _state = PilotStateEnum.Inactive;
                _stopReason = StopReasonEnum.None;
                _detourDirection = Vector3.zero;
                _detourStartPosition = Vector3.zero;
                return;
            }

            Plugin.Log.LogInfo("Pilot complete");

            ClearCourse();
            _lastShip = null;
            _state = PilotStateEnum.Inactive;
            _stopReason = StopReasonEnum.None;
            _detourDirection = Vector3.zero;
            _detourStartPosition = Vector3.zero;
        }

        private static void StartCourse(Ship ship)
        {
            if (!MapDestinationService.Destination.HasValue)
            {
                Plugin.Log.LogInfo("Pilot cannot start | No map destination selected");
                return;
            }

            _lastShip = ship;
            _state = PilotStateEnum.Sailing;
            _stopReason = StopReasonEnum.None;
            _detourDirection = Vector3.zero;
            _detourStartPosition = Vector3.zero;
            _nextDepthScanTime = 0f;
            _nextDetourRecheckTime = 0f;

            _course = new SailingCourseModel(
                ship.transform.position,
                MapDestinationService.Destination.Value);

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