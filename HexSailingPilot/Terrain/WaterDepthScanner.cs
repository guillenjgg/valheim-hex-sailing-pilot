using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Terrain
{
    internal static class WaterDepthScanner
    {
        private const float MinimumSafeDepth = 5f;

        private static readonly float[] ScanDistances =
        {
            10f,
            25f,
            50f
        };

        internal static List<WaterDepthScanModel> ScanForward(Ship ship)
        {
            var results = new List<WaterDepthScanModel>(ScanDistances.Length);
            WaterVolume waterVolume = null;

            foreach (float distance in ScanDistances)
            {
                Vector3 position = ship.transform.position + ship.transform.forward * distance;

                bool hasDepth = TryGetDepth(position, ref waterVolume, out float depth);
                bool isSafe = hasDepth && IsSafeDepth(depth);

                results.Add(new WaterDepthScanModel(
                    distance,
                    position,
                    hasDepth,
                    depth,
                    isSafe));
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

            float waterLevel = Floating.GetWaterLevel(position, ref waterVolume);

            if (waterLevel <= -10000f)
            {
                return false;
            }

            if (!Heightmap.GetHeight(position, out float terrainHeight))
            {
                return false;
            }

            depth = waterLevel - terrainHeight;
            return true;
        }

        internal static bool IsSafeDepth(float depth)
        {
            return depth >= MinimumSafeDepth;
        }
    }
}