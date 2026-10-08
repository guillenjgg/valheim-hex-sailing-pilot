using HexSailingPilot.Models;
using HexSailingPilot.Terrain;
using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal sealed class SailingNavigationGrid
    {
        private const float NodeSpacing = 10f;

        private readonly Dictionary<Vector2Int, SailingNavigationNodeModel> _nodes = new Dictionary<Vector2Int, SailingNavigationNodeModel>();

        internal SailingNavigationNodeModel GetNode(Vector2Int coordinate)
        {
            if (_nodes.TryGetValue(coordinate, out var node))
            {
                return node;
            }

            var position = new Vector3(coordinate.x * NodeSpacing, 0f, coordinate.y * NodeSpacing);
            WaterVolume waterVolume = null;

            var hasDepth = WaterDepthScanner.TryGetNavigationDepth(position, out var depth);
            var isNavigable = hasDepth && WaterDepthScanner.IsNavigableDepth(depth);

            node = new SailingNavigationNodeModel(position, hasDepth, depth, isNavigable);
            _nodes[coordinate] = node;

            return node;
        }

        internal List<SailingNavigationNodeModel> GetNeighbors(Vector2Int coordinate)
        {
            var neighbors = new List<SailingNavigationNodeModel>(8);

            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    neighbors.Add(GetNode(new Vector2Int(coordinate.x + x, coordinate.y + y)));
                }
            }

            return neighbors;
        }

        internal Vector2Int GetCoordinate(Vector3 position)
        {
            return new Vector2Int(Mathf.RoundToInt(position.x / NodeSpacing), Mathf.RoundToInt(position.z / NodeSpacing));
        }
    }
}