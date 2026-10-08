using HexSailingPilot.Models;
using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class SailingPathfinder
    {
        private const int MaximumSearchedNodes = 10000;

        internal static List<Vector3> FindPath(Vector3 start, Vector3 destination)
        {
            var grid = new SailingNavigationGrid();
            var startCoordinate = grid.GetCoordinate(start);
            var destinationCoordinate = grid.GetCoordinate(destination);

            var openNodes = new List<SailingPathNodeModel>();
            var pathNodes = new Dictionary<Vector2Int, SailingPathNodeModel>();
            var closedCoordinates = new HashSet<Vector2Int>();

            var startNode = GetPathNode(grid, pathNodes, startCoordinate);
            var destinationNode = grid.GetNode(destinationCoordinate);

            Plugin.Log.LogInfo($"Pathfinder start | Coordinate: {startCoordinate} | Position: {startNode.NavigationNode.Position} | HasDepth: {startNode.NavigationNode.HasDepth} | Depth: {startNode.NavigationNode.Depth:F1}m | Navigable: {startNode.NavigationNode.IsNavigable}");
            Plugin.Log.LogInfo($"Pathfinder destination | Coordinate: {destinationCoordinate} | Position: {destinationNode.Position} | HasDepth: {destinationNode.HasDepth} | Depth: {destinationNode.Depth:F1}m | Navigable: {destinationNode.IsNavigable}");

            startNode.CostFromStart = 0f;
            startNode.EstimatedCostToDestination = GetDistance(startCoordinate, destinationCoordinate);

            openNodes.Add(startNode);

            var minCoordinate = startCoordinate;
            var maxCoordinate = startCoordinate;

            while (openNodes.Count > 0 && closedCoordinates.Count < MaximumSearchedNodes)
            {
                var currentNode = GetLowestCostNode(openNodes);
                var currentCoordinate = grid.GetCoordinate(currentNode.NavigationNode.Position);

                minCoordinate = Vector2Int.Min(minCoordinate, currentCoordinate);
                maxCoordinate = Vector2Int.Max(maxCoordinate, currentCoordinate);

                if (currentCoordinate == destinationCoordinate)
                {
                    var path = BuildPath(currentNode);

                    Plugin.Log.LogInfo($"Pathfinder | Route found | Waypoints: {path.Count} | Searched: {closedCoordinates.Count} nodes | Min: {minCoordinate} | Max: {maxCoordinate}");

                    return path;
                }

                openNodes.Remove(currentNode);
                closedCoordinates.Add(currentCoordinate);

                foreach (var neighbor in grid.GetNeighbors(currentCoordinate))
                {
                    var neighborCoordinate = grid.GetCoordinate(neighbor.Position);

                    if (!neighbor.IsNavigable || closedCoordinates.Contains(neighborCoordinate))
                    {
                        continue;
                    }

                    var neighborPathNode = GetPathNode(grid, pathNodes, neighborCoordinate);
                    var newCostFromStart = currentNode.CostFromStart + GetDistance(currentCoordinate, neighborCoordinate);

                    if (newCostFromStart >= neighborPathNode.CostFromStart)
                    {
                        continue;
                    }

                    neighborPathNode.Parent = currentNode;
                    neighborPathNode.CostFromStart = newCostFromStart;
                    neighborPathNode.EstimatedCostToDestination = GetDistance(neighborCoordinate, destinationCoordinate);

                    if (!openNodes.Contains(neighborPathNode))
                    {
                        openNodes.Add(neighborPathNode);
                    }
                }
            }

            var reason = openNodes.Count == 0 ? "Open set exhausted" : "Search limit reached";

            Plugin.Log.LogWarning($"Pathfinder | No route found | Reason: {reason} | Searched: {closedCoordinates.Count} nodes | Sampled: {pathNodes.Count} path nodes | Min: {minCoordinate} | Max: {maxCoordinate}");

            return new List<Vector3>();
        }

        private static SailingPathNodeModel GetPathNode(SailingNavigationGrid grid, Dictionary<Vector2Int, SailingPathNodeModel> pathNodes, Vector2Int coordinate)
        {
            if (pathNodes.TryGetValue(coordinate, out var pathNode))
            {
                return pathNode;
            }

            pathNode = new SailingPathNodeModel(grid.GetNode(coordinate));
            pathNodes[coordinate] = pathNode;

            return pathNode;
        }

        private static SailingPathNodeModel GetLowestCostNode(List<SailingPathNodeModel> openNodes)
        {
            var bestNode = openNodes[0];

            for (var i = 1; i < openNodes.Count; i++)
            {
                var node = openNodes[i];

                if (node.TotalCost < bestNode.TotalCost)
                {
                    bestNode = node;
                }
            }

            return bestNode;
        }

        private static float GetDistance(Vector2Int first, Vector2Int second)
        {
            return Vector2Int.Distance(first, second);
        }

        private static List<Vector3> BuildPath(SailingPathNodeModel destinationNode)
        {
            var path = new List<Vector3>();
            var currentNode = destinationNode;

            while (currentNode != null)
            {
                path.Add(currentNode.NavigationNode.Position);
                currentNode = currentNode.Parent;
            }

            path.Reverse();
            return path;
        }
    }
}