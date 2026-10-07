using HexSailingPilot.Models;
using HexSailingPilot.Terrain;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class DetourDirectionSelector
    {
        private const float ScanAngleInterval = 30f;

        internal static bool TrySelect(
            Ship ship,
            Vector3 destination,
            out Vector3 bestDirection,
            out float bestAngle)
        {
            bestDirection = Vector3.zero;
            bestAngle = 0f;

            var directionToDestination = destination - ship.transform.position;
            directionToDestination.y = 0f;

            if (directionToDestination.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            directionToDestination.Normalize();

            var bestHeadingError = float.PositiveInfinity;
            var found = false;

            for (var angle = 0f; angle < 360f; angle += ScanAngleInterval)
            {
                var direction = Quaternion.AngleAxis(angle, Vector3.up) * directionToDestination;

                var depthResults = WaterDepthScanner.ScanDirection(ship, direction);

                if (!IsDepthPathSafe(depthResults))
                {
                    continue;
                }

                if (ShipObstacleScanner.IsDirectionBlocked(ship, direction, out _))
                {
                    continue;
                }

                var headingError = Vector3.Angle(directionToDestination, direction);

                if (headingError >= bestHeadingError)
                {
                    continue;
                }

                bestHeadingError = headingError;
                bestDirection = direction.normalized;
                bestAngle = angle;
                found = true;
            }

            return found;
        }

        private static bool IsDepthPathSafe(System.Collections.Generic.List<WaterDepthScanModel> results)
        {
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
    }
}