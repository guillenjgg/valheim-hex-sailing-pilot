using HexSailingPilot.Terrain;
using System.Collections.Generic;

namespace HexSailingPilot.Navigation
{
    internal static class ShallowWaterEscapeDirectionSelector
    {
        internal static bool TrySelect(
            Dictionary<float, List<WaterDepthScanModel>> scanResults,
            out float bestAngle,
            out float bestScore)
        {
            bestAngle = 0f;
            bestScore = float.NegativeInfinity;
            var found = false;

            foreach (var scan in scanResults)
            {
                if (!TryGetScore(scan.Value, out var score))
                {
                    continue;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestAngle = scan.Key;
                bestScore = score;
                found = true;
            }

            return found;
        }

        internal static bool TryGetScore(List<WaterDepthScanModel> results, out float score)
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
    }
}