using UnityEngine;

namespace HexSailingPilot
{
    internal static class ShipObstacleScanner
    {
        private const float ScanDistance = 15f;
        private const float RayHeight = 0.5f;
        private const float HeightSampleStart = 50f;
        private const float HeightSampleDistance = 100f;
        private const float HeightSampleInterval = 5f;
        private const float MinimumWaterDepth = 1f;

        internal static ShipObstacleScanResult Scan(Ship ship)
        {
            if (ship == null)
            {
                return null;
            }

            var transform = ship.transform;
            var result = new ShipObstacleScanResult();

            result.ForwardDistance = ScanDirection(ship, "Forward", transform.forward);
            result.BackwardDistance = ScanDirection(ship, "Backward", -transform.forward);
            result.LeftDistance = ScanDirection(ship, "Left", -transform.right);
            result.RightDistance = ScanDirection(ship, "Right", transform.right);

            var forwardDepthClear = ScanWaterDepth(ship, "Forward", transform.forward);
            var backwardDepthClear = ScanWaterDepth(ship, "Backward", -transform.forward);
            var leftDepthClear = ScanWaterDepth(ship, "Left", -transform.right);
            var rightDepthClear = ScanWaterDepth(ship, "Right", transform.right);

            result.ForwardClear =
                result.ForwardDistance >= ScanDistance &&
                forwardDepthClear;

            result.BackwardClear =
                result.BackwardDistance >= ScanDistance &&
                backwardDepthClear;

            result.LeftClear =
                result.LeftDistance >= ScanDistance &&
                leftDepthClear;

            result.RightClear =
                result.RightDistance >= ScanDistance &&
                rightDepthClear;

            return result;
        }

        private static bool ScanWaterDepth(Ship ship, string directionName, Vector3 direction)
        {
            var shipPosition = ship.transform.position;
            var clear = true;

            for (float distance = HeightSampleInterval; distance <= ScanDistance; distance += HeightSampleInterval)
            {
                var samplePosition = shipPosition + direction * distance;
                var waterDepth = GetWaterDepth(samplePosition);

                if (float.IsPositiveInfinity(waterDepth))
                {
                    Plugin.Log.LogInfo(
                        $"Water depth | {directionName} | " +
                        $"Distance: {distance:F0}m | UNKNOWN");

                    continue;
                }

                var sampleClear = waterDepth >= MinimumWaterDepth;

                Plugin.Log.LogInfo(
                    $"Water depth | {directionName} | " +
                    $"Distance: {distance:F0}m | " +
                    $"Depth: {waterDepth:F2}m | " +
                    $"{(sampleClear ? "CLEAR" : "SHALLOW")}");

                if (!sampleClear)
                {
                    clear = false;
                }
            }

            Plugin.Log.LogInfo(
                $"Depth scan | {directionName} | " +
                $"{(clear ? "CLEAR" : "SHALLOW")}");

            return clear;
        }

        private static float GetWaterDepth(Vector3 position)
        {
            var rayOrigin = position + Vector3.up * HeightSampleStart;

            var hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                HeightSampleDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float? waterHeight = null;
            float? terrainHeight = null;

            foreach (var hit in hits)
            {
                var layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);

                if (layerName == "Water")
                {
                    waterHeight = hit.point.y;
                }
                else if (layerName == "terrain")
                {
                    terrainHeight = hit.point.y;
                }
            }

            if (!waterHeight.HasValue || !terrainHeight.HasValue)
            {
                return float.PositiveInfinity;
            }

            return waterHeight.Value - terrainHeight.Value;
        }

        private static float ScanDirection(Ship ship, string directionName, Vector3 direction)
        {
            var bounds = GetShipBounds(ship);

            var center = bounds.center;
            center.y = bounds.min.y + RayHeight;

            var directionLocal = ship.transform.InverseTransformDirection(direction);
            var forwardBackward = Mathf.Abs(directionLocal.z) > Mathf.Abs(directionLocal.x);

            var perpendicular = forwardBackward
                ? ship.transform.right
                : ship.transform.forward;

            var halfWidth = forwardBackward
                ? bounds.extents.x
                : bounds.extents.z;

            var nearestDistance = ScanDistance;
            var blocked = false;

            for (int i = -1; i <= 1; i++)
            {
                var origin = center + perpendicular * halfWidth * i;

                var hits = Physics.RaycastAll(
                    origin,
                    direction,
                    ScanDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    if (IsShipCollider(ship, hit.collider))
                    {
                        continue;
                    }

                    var layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);

                    if (layerName == "Water")
                    {
                        continue;
                    }

                    LogCandidateHit(directionName, i, hit, layerName);

                    if (layerName != "terrain" && layerName != "static_solid")
                    {
                        continue;
                    }

                    blocked = true;
                    nearestDistance = Mathf.Min(nearestDistance, hit.distance);
                    break;
                }
            }

            Plugin.Log.LogInfo(
                $"Obstacle scan | {directionName} | " +
                $"{(blocked ? "BLOCKED" : "CLEAR")} | " +
                $"{nearestDistance:F1}m");

            return nearestDistance;
        }

        private static void LogCandidateHit(string directionName, int rayIndex, RaycastHit hit, string layerName)
        {
            var collider = hit.collider;
            var gameObject = collider.gameObject;
            var root = collider.transform.root;

            var piece = collider.GetComponentInParent<Piece>();
            var destructible = collider.GetComponentInParent<Destructible>();
            var character = collider.GetComponentInParent<Character>();
            var hitShip = collider.GetComponentInParent<Ship>();

            Plugin.Log.LogInfo(
                $"Candidate hit | {directionName} | " +
                $"Ray: {rayIndex} | " +
                $"Collider: {collider.name} | " +
                $"Object: {gameObject.name} | " +
                $"Root: {root.name} | " +
                $"Layer: {layerName} | " +
                $"Tag: {gameObject.tag} | " +
                $"Piece: {piece != null} | " +
                $"Destructible: {destructible != null} | " +
                $"Character: {character != null} | " +
                $"Ship: {hitShip != null} | " +
                $"Distance: {hit.distance:F2}m");
        }

        private static Bounds GetShipBounds(Ship ship)
        {
            var colliders = ship.GetComponentsInChildren<Collider>();
            var bounds = new Bounds(ship.transform.position, Vector3.zero);
            var initialized = false;

            foreach (var collider in colliders)
            {
                if (!collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return bounds;
        }

        private static bool IsShipCollider(Ship ship, Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            return collider.transform == ship.transform ||
                   collider.transform.IsChildOf(ship.transform);
        }
    }
}