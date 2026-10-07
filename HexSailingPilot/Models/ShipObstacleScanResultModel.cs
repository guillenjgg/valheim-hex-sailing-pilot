using UnityEngine;

namespace HexSailingPilot.Models
{
    internal sealed class ShipObstacleScanResultModel
    {
        internal bool IsBlocked { get; set; }
        internal float Distance { get; set; }
        internal Collider Collider { get; set; }
        internal Leviathan Leviathan { get; set; }
        internal Bounds Bounds { get; set; }
        internal bool HasBounds { get; set; }
    }
}