using UnityEngine;

namespace HexSailingPilot.Models
{
    internal sealed class SailingNavigationNodeModel
    {
        internal Vector3 Position { get; }
        internal bool HasDepth { get; }
        internal float Depth { get; }
        internal bool IsNavigable { get; }

        internal SailingNavigationNodeModel(Vector3 position, bool hasDepth, float depth, bool isNavigable)
        {
            Position = position;
            HasDepth = hasDepth;
            Depth = depth;
            IsNavigable = isNavigable;
        }
    }
}