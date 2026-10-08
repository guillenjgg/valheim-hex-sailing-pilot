using HexSailingPilot.Models;
using HexSailingPilot.ShipAccess;
using HexSailingPilot.Terrain;
using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class SailingPilotController
    {
        private const float ArrivalDistance = 10f;
        private const float WaypointReachedDistance = 10f;
        private const float WaypointPassedDistance = 5f;
        private const float ManualSteeringThreshold = 0.5f;
        private const float ManualSpeedThreshold = 0.5f;
        private const float DepthScanInterval = 1f;
        private const float DetourRecheckInterval = 1f;
        private const float MinimumDetourDuration = 3f;
        private const int RequiredClearConfirmations = 3;
        private const float LeviathanAvoidanceTurnDegrees = 45f;
        private const float LeviathanClearance = 15f;

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
        private static List<Vector3> _waypoints = new List<Vector3>();
        private static int _currentWaypointIndex;
        private static Vector3 _detourDirection;
        private static Vector3 _detourStartPosition;
        private static float _nextDepthScanTime;
        private static float _nextDetourRecheckTime;
        private static float _detourStartTime;
        private static int _destinationClearStreak;

        private static bool _leviathanAvoidanceActive;
        private static Vector3 _leviathanAvoidanceDirection;
        private static Vector3 _leviathanPassDirection;
        private static Bounds _leviathanBounds;
        private static float _leviathanAvoidanceSide;

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

            var distanceToDestination = SteeringCalculator.GetDistanceToDestination(
                _course,
                ship.transform.position);

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

            UpdateWaypointTracking(ship);

            if (_state == PilotStateEnum.Detouring)
            {
                ApplyDetour(ship, rudderValue, ref moveDir);
                return;
            }

            var unsafeWaterDetected = ScanDepth(ship);

            var directionToDestination = GetNavigationTarget() - ship.transform.position;
            directionToDestination.y = 0f;

            var obstacleResult = ShipObstacleScanner.ScanDirection(
                ship,
                directionToDestination);

            var obstacleDetected = obstacleResult.IsBlocked;

            if (unsafeWaterDetected || obstacleDetected)
            {
                if (obstacleResult.Leviathan != null)
                {
                    var leviathans = ShipObstacleScanner.ScanLeviathans(
                        ship,
                        directionToDestination);

                    if (TryGetLeviathanClusterBounds(leviathans, out var clusterBounds))
                    {
                        obstacleResult.Bounds = clusterBounds;
                        obstacleResult.HasBounds = true;

                        if (TryStartLeviathanAvoidance(ship, obstacleResult))
                        {
                            ApplyDetour(ship, rudderValue, ref moveDir);
                            return;
                        }
                    }
                }

                if (TryStartDetour(ship))
                {
                    ApplySteering(
                        ship,
                        _detourDirection,
                        rudderValue,
                        ref moveDir);

                    return;
                }

                _state = PilotStateEnum.Stopping;
                _stopReason = StopReasonEnum.PathBlocked;

                Plugin.Log.LogInfo(
                    $"Pilot destination path blocked | " +
                    $"Water: {unsafeWaterDetected} | " +
                    $"Obstacle: {obstacleDetected} | " +
                    $"ObstacleDistance: {obstacleResult.Distance:F1}m | " +
                    $"No detour available");

                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            var directionToTarget = GetNavigationTarget() - ship.transform.position;
            directionToTarget.y = 0f;

            ApplySteering(
                ship,
                directionToTarget,
                rudderValue,
                ref moveDir);
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
            if (_leviathanAvoidanceActive)
            {
                ApplyLeviathanAvoidance(ship, rudderValue, ref moveDir);

                return;
            }

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

                var confirmedClear = detourElapsed >= MinimumDetourDuration &&
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

                        Plugin.Log.LogInfo(
                            "Detour blocked | No alternate safe direction");

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
                GetNavigationTarget(),
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
            var directionToDestination =
                GetNavigationTarget() - ship.transform.position;

            directionToDestination.y = 0f;

            if (directionToDestination.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            if (ShipObstacleScanner.IsDirectionBlocked(
                ship,
                directionToDestination,
                out _))
            {
                return false;
            }

            return IsDepthPathSafe(
                ship,
                directionToDestination);
        }

        private static bool IsDirectionSafe(
            Ship ship,
            Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            if (ShipObstacleScanner.IsDirectionBlocked(
                ship,
                direction,
                out _))
            {
                return false;
            }

            return IsDepthPathSafe(ship, direction);
        }

        private static bool IsDepthPathSafe(
            Ship ship,
            Vector3 direction)
        {
            var results = WaterDepthScanner.ScanDirection(
                ship,
                direction);

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

        private static void ApplySteering(
            Ship ship,
            Vector3 direction,
            float rudderValue,
            ref Vector3 moveDir)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                moveDir.x = 0f;
                return;
            }

            SailingPropulsionController.Apply(
                ship,
                ref moveDir);

            var targetHeading =
                SteeringCalculator.GetHeading(direction);

            var currentHeading =
                SteeringCalculator.GetHeading(ship.transform.forward);

            var headingError =
                Mathf.DeltaAngle(currentHeading, targetHeading);

            var targetRudder =
                SteeringCalculator.GetTargetRudder(headingError);

            SteeringCalculator.SetRudderInput(
                targetRudder,
                rudderValue,
                ref moveDir);
        }

        private static bool ScanDepth(Ship ship)
        {
            if (Time.time < _nextDepthScanTime)
            {
                return false;
            }

            _nextDepthScanTime =
                Time.time + DepthScanInterval;

            var directionToDestination =
                GetNavigationTarget() - ship.transform.position;

            directionToDestination.y = 0f;

            if (directionToDestination.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            var results = WaterDepthScanner.ScanDirection(
                ship,
                directionToDestination);

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

            return nearestResult.HasDepth &&
                   !nearestResult.IsSafe;
        }

        private static void StopShip(Ship ship)
        {
            if (ship == null)
            {
                return;
            }

            if (ShipAccessor.StopMethod == null)
            {
                Plugin.Log.LogWarning(
                    "Ship.Stop method was not found.");

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

        private static void TakeManualControl(
            Ship ship,
            Vector3 playerMoveDir)
        {
            ShipAccessor.ForwardPressed(ship) = false;
            ShipAccessor.BackwardPressed(ship) = false;

            Plugin.Log.LogInfo(
                $"Pilot manual takeover | " +
                $"PlayerInput: " +
                $"({playerMoveDir.x:+0.00;-0.00;0.00}, " +
                $"{playerMoveDir.z:+0.00;-0.00;0.00})");

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

            ClearLeviathanAvoidance();

            Plugin.Log.LogInfo("Pilot disengaged");
        }

        private static bool HasManualInput(Vector3 playerMoveDir)
        {
            return Mathf.Abs(playerMoveDir.x) >
                       ManualSteeringThreshold ||
                   playerMoveDir.z >
                       ManualSpeedThreshold ||
                   playerMoveDir.z <
                       -ManualSpeedThreshold;
        }

        private static void ApplyStop(Ship ship, float rudderValue, ref Vector3 moveDir)
        {
            SteeringCalculator.SetRudderInput(
                0f,
                rudderValue,
                ref moveDir);

            SailingPropulsionController.Stop(
                ship,
                ref moveDir);

            if (ShipAccessor.ShipSpeed(ship) != Ship.Speed.Stop)
            {
                return;
            }

            var body = ShipAccessor.ShipBody(ship);

            if (body != null)
            {
                var horizontalVelocity =
                    new Vector3(
                        body.linearVelocity.x,
                        0f,
                        body.linearVelocity.z);

                var horizontalSpeed =
                    horizontalVelocity.magnitude;

                if (horizontalSpeed > 0.1f)
                {
                    BrakeShip(ship);
                    return;
                }
            }

            if (Mathf.Abs(rudderValue) >
                SteeringCalculator.RudderTolerance)
            {
                return;
            }

            moveDir.x = 0f;

            if (_stopReason == StopReasonEnum.PathBlocked)
            {
                Plugin.Log.LogInfo(
                    "Pilot stopped | No safe detour available");

                ClearCourse();

                _lastShip = null;
                _state = PilotStateEnum.Inactive;
                _stopReason = StopReasonEnum.None;
                _detourDirection = Vector3.zero;
                _detourStartPosition = Vector3.zero;
                _destinationClearStreak = 0;

                ClearLeviathanAvoidance();
                return;
            }

            Plugin.Log.LogInfo("Pilot complete");

            ClearCourse();

            _lastShip = null;
            _state = PilotStateEnum.Inactive;
            _stopReason = StopReasonEnum.None;
            _detourDirection = Vector3.zero;
            _detourStartPosition = Vector3.zero;
            _destinationClearStreak = 0;

            ClearLeviathanAvoidance();
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

            ClearLeviathanAvoidance();

            _course = new SailingCourseModel(ship.transform.position, MapDestinationService.Destination.Value);

            var routeScan = WaterDepthScanner.ScanRoute(_course.Origin, _course.Destination);
            WaterDepthScanModel firstUnsafePoint = null;

            foreach (var result in routeScan)
            {
                if (!result.HasDepth || !result.IsSafe)
                {
                    firstUnsafePoint = result;
                    break;
                }
            }

            Plugin.Log.LogInfo(
                $"Pilot course started | " +
                $"Origin: {_course.Origin} | " +
                $"Destination: {_course.Destination} | " +
                $"Distance: {_course.Length:F0}m | " +
                $"Heading: {SteeringCalculator.GetHeading(_course.Direction):F1}°");

            if (firstUnsafePoint == null)
            {
                Plugin.Log.LogInfo(
                    $"Route scan | " +
                    $"CLEAR | " +
                    $"Distance: {_course.Length:F0}m | " +
                    $"Samples: {routeScan.Count}");
            }
            else
            {
                Plugin.Log.LogInfo(
                    $"Route scan | " +
                    $"BLOCKED | " +
                    $"RouteDistance: {_course.Length:F0}m | " +
                    $"FirstUnsafeDistance: {firstUnsafePoint.Distance:F0}m | " +
                    $"Depth: {firstUnsafePoint.Depth:F1}m | " +
                    $"HasDepth: {firstUnsafePoint.HasDepth} | " +
                    $"Position: {firstUnsafePoint.Position}");
            }

            _waypoints = SailingPathfinder.FindPath(_course.Origin, _course.Destination);
            _currentWaypointIndex = 0;

            if (_waypoints.Count == 0)
            {
                Plugin.Log.LogWarning("Pathfinder | No route");
                return;
            }

            Plugin.Log.LogInfo(
                $"Pathfinder | Route found | " +
                $"Nodes: {_waypoints.Count} | " +
                $"Start: {_waypoints[0]} | " +
                $"End: {_waypoints[_waypoints.Count - 1]}");

            Plugin.Log.LogInfo($"Waypoint tracking started | Count: {_waypoints.Count}");
        }

        private static Vector3 GetNavigationTarget()
        {
            if (_waypoints.Count == 0 || _currentWaypointIndex >= _waypoints.Count)
            {
                return _course.Destination;
            }

            return _waypoints[_currentWaypointIndex];
        }

        private static void UpdateWaypointTracking(Ship ship)
        {
            if (_waypoints.Count == 0 || _currentWaypointIndex >= _waypoints.Count)
            {
                return;
            }

            var shipPosition = ship.transform.position;

            while (_currentWaypointIndex < _waypoints.Count)
            {
                var waypoint = _waypoints[_currentWaypointIndex];
                var offset = waypoint - shipPosition;
                offset.y = 0f;

                var distance = offset.magnitude;
                var reached = distance <= WaypointReachedDistance;
                var passed = false;

                if (!reached && _currentWaypointIndex > 0)
                {
                    var previousWaypoint = _waypoints[_currentWaypointIndex - 1];
                    var segment = waypoint - previousWaypoint;
                    segment.y = 0f;

                    if (segment.sqrMagnitude > 0.001f)
                    {
                        var fromPrevious = shipPosition - previousWaypoint;
                        fromPrevious.y = 0f;

                        var progress = Vector3.Dot(fromPrevious, segment) / segment.sqrMagnitude;
                        var closestPoint = previousWaypoint + segment * Mathf.Clamp01(progress);
                        var lateralOffset = shipPosition - closestPoint;
                        lateralOffset.y = 0f;

                        passed = progress >= 1f && lateralOffset.magnitude <= WaypointPassedDistance;
                    }
                }

                if (!reached && !passed)
                {
                    break;
                }

                _currentWaypointIndex++;

                Plugin.Log.LogInfo(
                    $"Waypoint {(passed ? "passed" : "reached")} | " +
                    $"Index: {_currentWaypointIndex}/{_waypoints.Count} | " +
                    $"Position: {waypoint} | " +
                    $"Distance: {distance:F1}m");
            }

            if (_currentWaypointIndex == _waypoints.Count)
            {
                Plugin.Log.LogInfo("Waypoint navigation complete | Proceeding to map destination");
            }
        }

        private static void ClearCourse()
        {
            _course = null;
            _waypoints.Clear();
            _currentWaypointIndex = 0;
        }

        private static bool TryStartLeviathanAvoidance(Ship ship, ShipObstacleScanResultModel obstacle)
        {
            var directionToDestination = GetNavigationTarget() - ship.transform.position;
            directionToDestination.y = 0f;

            if (directionToDestination.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            directionToDestination.Normalize();

            var leftDirection = Quaternion.AngleAxis(-LeviathanAvoidanceTurnDegrees, Vector3.up) * directionToDestination;
            var rightDirection = Quaternion.AngleAxis(LeviathanAvoidanceTurnDegrees, Vector3.up) * directionToDestination;

            leftDirection.y = 0f;
            rightDirection.y = 0f;

            leftDirection.Normalize();
            rightDirection.Normalize();

            var leftSafe = IsDepthPathSafe(ship, leftDirection);
            var rightSafe = IsDepthPathSafe(ship, rightDirection);

            Plugin.Log.LogInfo(
                $"Leviathan avoidance candidates | " +
                $"LeftSafe: {leftSafe} | " +
                $"RightSafe: {rightSafe} | " +
                $"LeftHeading: {SteeringCalculator.GetHeading(leftDirection):F1}° | " +
                $"RightHeading: {SteeringCalculator.GetHeading(rightDirection):F1}°");

            if (!leftSafe && !rightSafe)
            {
                Plugin.Log.LogInfo("Leviathan avoidance rejected | No safe avoidance direction");
                return false;
            }

            var useLeft = leftSafe;

            _leviathanAvoidanceActive = true;
            _leviathanAvoidanceDirection = useLeft ? leftDirection : rightDirection;
            _leviathanAvoidanceSide = useLeft ? -1f : 1f;
            _leviathanPassDirection = directionToDestination;
            _leviathanBounds = obstacle.Bounds;

            _state = PilotStateEnum.Detouring;
            _detourDirection = Vector3.zero;
            _detourStartPosition = ship.transform.position;
            _destinationClearStreak = 0;

            Plugin.Log.LogInfo(
                $"Leviathan avoidance started | " +
                $"Side: {(useLeft ? "Left" : "Right")} | " +
                $"Heading: {SteeringCalculator.GetHeading(_leviathanAvoidanceDirection):F1}° | " +
                $"ObstacleCenter: {_leviathanBounds.center} | " +
                $"ObstacleSize: {_leviathanBounds.size}");

            return true;
        }

        private static void ApplyLeviathanAvoidance(Ship ship, float rudderValue, ref Vector3 moveDir)
        {
            if (!_leviathanAvoidanceActive)
            {
                _state = PilotStateEnum.Sailing;
                return;
            }

            var centerToShip = ship.transform.position - _leviathanBounds.center;
            centerToShip.y = 0f;

            var lateralDirection = Vector3.Cross(Vector3.up, _leviathanPassDirection).normalized;
            var lateralPosition = Vector3.Dot(centerToShip, lateralDirection);
            var lateralExtent = GetBoundsExtentAlongDirection(_leviathanBounds, lateralDirection);
            var lateralProgress = lateralPosition * _leviathanAvoidanceSide;
            var requiredLateralProgress = lateralExtent + LeviathanClearance;

            var hasLateralClearance = lateralProgress >= requiredLateralProgress;
            var destinationPathClear = hasLateralClearance && !ShipObstacleScanner.IsDirectionBlocked(ship, GetNavigationTarget() - ship.transform.position, out _);

            if (destinationPathClear)
            {
                Plugin.Log.LogInfo(
                    $"Leviathan avoidance complete | " +
                    $"LateralProgress: {lateralProgress:F1}m | " +
                    $"Required: {requiredLateralProgress:F1}m | " +
                    $"Destination path clear | " +
                    $"Resuming destination course");

                ClearLeviathanAvoidance();
                _state = PilotStateEnum.Sailing;
                return;
            }

            if (!IsDepthPathSafe(ship, _leviathanAvoidanceDirection))
            {
                Plugin.Log.LogInfo("Leviathan avoidance blocked | Committed direction is no longer depth safe");

                _state = PilotStateEnum.Stopping;
                _stopReason = StopReasonEnum.PathBlocked;

                ApplyStop(ship, rudderValue, ref moveDir);
                return;
            }

            var currentHeading = SteeringCalculator.GetHeading(ship.transform.forward);
            var targetHeading = SteeringCalculator.GetHeading(_leviathanAvoidanceDirection);

            Plugin.Log.LogInfo(
                $"Leviathan avoidance | " +
                $"CurrentHeading: {currentHeading:F1}° | " +
                $"TargetHeading: {targetHeading:F1}° | " +
                $"LateralProgress: {lateralProgress:F1}m | " +
                $"Required: {requiredLateralProgress:F1}m");

            ApplySteering(ship, _leviathanAvoidanceDirection, rudderValue, ref moveDir);
        }

        private static void ClearLeviathanAvoidance()
        {
            _leviathanAvoidanceActive = false;
            _leviathanAvoidanceDirection = Vector3.zero;
            _leviathanAvoidanceSide = 0f;
            _leviathanPassDirection = Vector3.zero;
            _leviathanBounds = default;
        }

        private static float GetBoundsExtentAlongDirection(
            Bounds bounds,
            Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return 0f;
            }

            direction.Normalize();

            return Mathf.Abs(direction.x) * bounds.extents.x +
                   Mathf.Abs(direction.z) * bounds.extents.z;
        }

        private static bool TryGetLeviathanClusterBounds(List<ShipObstacleScanResultModel> leviathans, out Bounds clusterBounds)
        {
            clusterBounds = default;

            if (leviathans == null || leviathans.Count == 0)
            {
                return false;
            }

            var initialized = false;

            foreach (var leviathan in leviathans)
            {
                if (!leviathan.HasBounds)
                {
                    continue;
                }

                if (!initialized)
                {
                    clusterBounds = leviathan.Bounds;
                    initialized = true;
                }
                else
                {
                    clusterBounds.Encapsulate(leviathan.Bounds);
                }
            }

            if (!initialized)
            {
                return false;
            }

            Plugin.Log.LogInfo(
                $"Leviathan cluster | " +
                $"Count: {leviathans.Count} | " +
                $"Center: ({clusterBounds.center.x:F1}, {clusterBounds.center.y:F1}, {clusterBounds.center.z:F1}) | " +
                $"Size: ({clusterBounds.size.x:F1}, {clusterBounds.size.y:F1}, {clusterBounds.size.z:F1})");

            return true;
        }
    }
}
