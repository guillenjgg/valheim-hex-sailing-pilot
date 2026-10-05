using UnityEngine;

namespace HexSailingPilot.Terrain
{
    internal sealed class WaterDepthScanModel
    {
        internal float Distance { get; }
        internal Vector3 Position { get; }
        internal bool HasDepth { get; }
        internal float Depth { get; }
        internal bool IsSafe { get; }

        internal WaterDepthScanModel(float distance, Vector3 position, bool hasDepth, float depth, bool isSafe)
        {
            Distance = distance;
            Position = position;
            HasDepth = hasDepth;
            Depth = depth;
            IsSafe = isSafe;
        }
    }
}