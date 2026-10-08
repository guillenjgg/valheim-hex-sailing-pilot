using HexSailingPilot.Models;
using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Terrain
{
    internal static class WaterDepthScanner
    {
        private const float MinimumNavigableDepth = 1f;
        private const float ScanAngleInterval = 30f;
        private const float RouteScanInterval = 10f;

        private static readonly float[] ScanDistances =
        {
            10f,
            25f,
            50f
        };

        internal static List<WaterDepthScanModel> ScanDirection(Ship ship, Vector3 direction)
        {
            var results = new List<WaterDepthScanModel>(ScanDistances.Length);
            WaterVolume waterVolume = null;

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return results;
            }

            direction.Normalize();

            foreach (var distance in ScanDistances)
            {
                var position = ship.transform.position + direction * distance;

                CompareNavigationDepth(position);

                var hasDepth = TryGetDepth(position, ref waterVolume, out var depth);
                var isSafe = hasDepth && IsNavigableDepth(depth);

                results.Add(new WaterDepthScanModel(
                    distance,
                    position,
                    hasDepth,
                    depth,
                    isSafe));
            }

            return results;
        }

        internal static List<WaterDepthScanModel> ScanRoute(Vector3 start, Vector3 destination)
        {
            var results = new List<WaterDepthScanModel>();

            var direction = destination - start;
            direction.y = 0f;

            var routeDistance = direction.magnitude;

            if (routeDistance <= 0.001f)
            {
                return results;
            }

            direction.Normalize();

            for (var distance = RouteScanInterval; distance < routeDistance; distance += RouteScanInterval)
            {
                var position = start + direction * distance;

                var hasDepth = TryGetNavigationDepth(position, out var depth);
                var isSafe = hasDepth && IsNavigableDepth(depth);

                results.Add(new WaterDepthScanModel(
                    distance,
                    position,
                    hasDepth,
                    depth,
                    isSafe));
            }

            var destinationPosition = start + direction * routeDistance;
            var destinationHasDepth = TryGetNavigationDepth(destinationPosition, out var destinationDepth);
            var destinationIsSafe = destinationHasDepth && IsNavigableDepth(destinationDepth);

            results.Add(new WaterDepthScanModel(
                routeDistance,
                destinationPosition,
                destinationHasDepth,
                destinationDepth,
                destinationIsSafe));

            return results;
        }

        internal static Dictionary<float, List<WaterDepthScanModel>> Scan360(Ship ship)
        {
            var results = new Dictionary<float, List<WaterDepthScanModel>>();

            for (var angle = 0f; angle < 360f; angle += ScanAngleInterval)
            {
                var direction = Quaternion.AngleAxis(angle, Vector3.up) * ship.transform.forward;
                results[angle] = ScanDirection(ship, direction);
            }

            return results;
        }

        internal static bool TryGetDepthUnderShip(Ship ship, out float depth)
        {
            depth = 0f;

            if (ship == null)
            {
                return false;
            }

            WaterVolume waterVolume = null;

            return TryGetDepth(
                ship.transform.position,
                ref waterVolume,
                out depth);
        }

        internal static bool TryGetDepth(Vector3 position, ref WaterVolume waterVolume, out float depth)
        {
            depth = 0f;

            if (ZoneSystem.instance == null)
            {
                return false;
            }

            var waterLevel = Floating.GetWaterLevel(position, ref waterVolume);

            if (waterLevel <= -10000f)
            {
                return false;
            }

            if (!ZoneSystem.instance.GetGroundHeight(position, out var terrainHeight))
            {
                return false;
            }

            depth = waterLevel - terrainHeight;
            return true;
        }

        internal static bool IsNavigableDepth(float depth)
        {
            return depth > MinimumNavigableDepth;
        }

        internal static bool TryGetNavigationDepth(Vector3 position, out float depth)
        {
            depth = 0f;

            if (WorldGenerator.instance == null)
            {
                return false;
            }

            var terrainHeight = WorldGenerator.instance.GetHeight(position);
            var waterLevel = 30f;

            depth = waterLevel - terrainHeight;

            return true;
        }

        internal static void CompareNavigationDepth(Vector3 position)
        {
            if (!TryGetNavigationDepth(position, out var navigationDepth))
            {
                return;
            }

            WaterVolume waterVolume = null;

            if (!TryGetDepth(position, ref waterVolume, out var actualDepth))
            {
                return;
            }

            var difference = Mathf.Abs(navigationDepth - actualDepth);

            Plugin.Log.LogInfo(
                $"Navigation depth comparison | " +
                $"Position: {position} | " +
                $"Procedural: {navigationDepth:F1}m | " +
                $"Actual: {actualDepth:F1}m | " +
                $"Difference: {difference:F1}m");
        }
    }
}