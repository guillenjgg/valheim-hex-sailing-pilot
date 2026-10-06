using UnityEngine;

namespace HexSailingPilot
{
    internal static class ShipObstacleScanner
    {
        private const float ScanDistance = 15f;
        private const float RayHeight = 0.5f;

        internal static bool IsDirectionBlocked(Ship ship, Vector3 direction, out float nearestDistance)
        {
            nearestDistance = ScanDistance;

            if (ship == null)
            {
                return false;
            }

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            direction.Normalize();

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

            var blocked = false;

            for (var i = -1; i <= 1; i++)
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

                    LogCandidateHit(i, hit, layerName);

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
                $"Destination obstacle scan | " +
                $"{(blocked ? "BLOCKED" : "CLEAR")} | " +
                $"Distance: {nearestDistance:F1}m");

            return blocked;
        }

        private static void LogCandidateHit(int rayIndex, RaycastHit hit, string layerName)
        {
            var collider = hit.collider;
            var gameObject = collider.gameObject;
            var root = collider.transform.root;

            var piece = collider.GetComponentInParent<Piece>();
            var destructible = collider.GetComponentInParent<Destructible>();
            var character = collider.GetComponentInParent<Character>();
            var hitShip = collider.GetComponentInParent<Ship>();

            Plugin.Log.LogInfo(
                $"Candidate hit | " +
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