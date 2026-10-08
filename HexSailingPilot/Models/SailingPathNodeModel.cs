namespace HexSailingPilot.Models
{
    internal sealed class SailingPathNodeModel
    {
        internal SailingNavigationNodeModel NavigationNode { get; }
        internal SailingPathNodeModel Parent { get; set; }
        internal float CostFromStart { get; set; }
        internal float EstimatedCostToDestination { get; set; }
        internal float TotalCost => CostFromStart + EstimatedCostToDestination;

        internal SailingPathNodeModel(SailingNavigationNodeModel navigationNode)
        {
            NavigationNode = navigationNode;
            CostFromStart = float.PositiveInfinity;
        }
    }
}