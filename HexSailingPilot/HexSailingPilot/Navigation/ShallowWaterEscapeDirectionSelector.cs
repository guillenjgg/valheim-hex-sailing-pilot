using HexSailingPilot.Terrain;
using System.Collections.Generic;

namespace HexSailingPilot.Navigation
{
    internal static class ShallowWaterEscapeDirectionSelector
    {
        internal static ShallowWaterEscapeDirectionEnum Select(
            List<WaterDepthScanModel> forwardResults,
            List<WaterDepthScanModel> backwardResults,
            List<WaterDepthScanModel> leftResults,
            List<WaterDepthScanModel> rightResults)
        {
            var bestDirection = ShallowWaterEscapeDirectionEnum.None;
            var bestScore = float.NegativeInfinity;

            EvaluateDirection(
                ShallowWaterEscapeDirectionEnum.Forward,
                forwardResults,
                ref bestDirection,
                ref bestScore);

            EvaluateDirection(
                ShallowWaterEscapeDirectionEnum.Backward,
                backwardResults,
                ref bestDirection,
                ref bestScore);

            EvaluateDirection(
                ShallowWaterEscapeDirectionEnum.Left,
                leftResults,
                ref bestDirection,
                ref bestScore);

            EvaluateDirection(
                ShallowWaterEscapeDirectionEnum.Right,
                rightResults,
                ref bestDirection,
                ref bestScore);

            return bestDirection;
        }

        internal static bool TryGetScore(
            List<WaterDepthScanModel> results,
            out float score)
        {
            score = 0f;

            if (results == null || results.Count == 0)
            {
                return false;
            }

            foreach (var result in results)
            {
                if (!result.HasDepth || !result.IsSafe)
                {
                    return false;
                }
            }

            score = results[results.Count - 1].Depth - results[0].Depth;
            return true;
        }

        private static void EvaluateDirection(
            ShallowWaterEscapeDirectionEnum direction,
            List<WaterDepthScanModel> results,
            ref ShallowWaterEscapeDirectionEnum bestDirection,
            ref float bestScore)
        {
            if (!TryGetScore(results, out float score))
            {
                return;
            }

            if (score <= bestScore)
            {
                return;
            }

            bestDirection = direction;
            bestScore = score;
        }
    }
}