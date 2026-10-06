using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Terrain
{
    internal static class WaterDepthScanner
    {
        private const float MinimumNavigableDepth = 1f;
        private const float ScanAngleInterval = 30f;

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
    }
}