internal sealed class ShipObstacleScanResult
{
    internal bool ForwardClear { get; set; }
    internal bool BackwardClear { get; set; }
    internal bool LeftClear { get; set; }
    internal bool RightClear { get; set; }

    internal float ForwardDistance { get; set; }
    internal float BackwardDistance { get; set; }
    internal float LeftDistance { get; set; }
    internal float RightDistance { get; set; }
}