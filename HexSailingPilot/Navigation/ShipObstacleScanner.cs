using HexSailingPilot.Models;
using HexSailingPilot.ShipAccess;
using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class ShipObstacleScanner
    {
        private const float MinimumScanDistance = 25f;
        private const float MaximumScanDistance = 50f;
        private const float ScanDistancePerSpeed = 10f;
        private const float RayHeight = 0.5f;
        private const float LeviathanScanRadius = 50f;

        internal static bool IsDirectionBlocked(Ship ship, Vector3 direction, out float nearestDistance)
        {
            var result = ScanDirection(ship, direction);

            nearestDistance = result.Distance;

            return result.IsBlocked;
        }

        internal static ShipObstacleScanResultModel ScanDirection(Ship ship, Vector3 direction)
        {
            var scanDistance = GetScanDistance(ship);

            var result = new ShipObstacleScanResultModel
            {
                Distance = scanDistance
            };

            if (ship == null)
            {
                return result;
            }

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return result;
            }

            direction.Normalize();

            var shipBounds = GetShipBounds(ship);
            var center = shipBounds.center;
            center.y = shipBounds.min.y + RayHeight;

            var perpendicular = Vector3.Cross(Vector3.up, direction).normalized;
            var halfWidth = Mathf.Max(shipBounds.extents.x, shipBounds.extents.z);

            for (var i = -1; i <= 1; i++)
            {
                var origin = center + perpendicular * halfWidth * i;
                var hits = Physics.RaycastAll(origin, direction, scanDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

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

                    if (!IsBlockingObstacle(hit.collider, layerName))
                    {
                        continue;
                    }

                    if (result.IsBlocked && hit.distance >= result.Distance)
                    {
                        break;
                    }

                    result.IsBlocked = true;
                    result.Distance = hit.distance;
                    result.Collider = hit.collider;
                    result.Leviathan = hit.collider.GetComponentInParent<Leviathan>();
                    result.HasBounds = TryGetObstacleBounds(hit.collider, result.Leviathan, out var obstacleBounds);
                    result.Bounds = obstacleBounds;

                    break;
                }
            }

            LogScanResult(result, scanDistance);

            return result;
        }

        internal static List<ShipObstacleScanResultModel> ScanLeviathans(Ship ship, Vector3 direction)
        {
            var results = new List<ShipObstacleScanResultModel>();

            if (ship == null)
            {
                return results;
            }

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return results;
            }

            direction.Normalize();

            var scanDistance = GetScanDistance(ship);
            var scanCenter = ship.transform.position + direction * scanDistance;
            var colliders = Physics.OverlapSphere(scanCenter, LeviathanScanRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var detectedLeviathans = new HashSet<Leviathan>();

            foreach (var collider in colliders)
            {
                if (IsShipCollider(ship, collider))
                {
                    continue;
                }

                var leviathan = collider.GetComponentInParent<Leviathan>();

                if (leviathan == null || !detectedLeviathans.Add(leviathan))
                {
                    continue;
                }

                if (!TryGetLeviathanBounds(leviathan, out var bounds))
                {
                    continue;
                }

                var closestPoint = bounds.ClosestPoint(ship.transform.position);
                var offset = closestPoint - ship.transform.position;
                offset.y = 0f;

                results.Add(new ShipObstacleScanResultModel
                {
                    IsBlocked = true,
                    Distance = offset.magnitude,
                    Collider = collider,
                    Leviathan = leviathan,
                    HasBounds = true,
                    Bounds = bounds
                });
            }

            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            Plugin.Log.LogInfo(
                $"Leviathan scan | " +
                $"Count: {results.Count} | " +
                $"ScanDistance: {scanDistance:F1}m | " +
                $"Radius: {LeviathanScanRadius:F1}m");

            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];

                Plugin.Log.LogInfo(
                    $"Leviathan scan result | " +
                    $"Index: {i + 1} | " +
                    $"Distance: {result.Distance:F1}m | " +
                    $"Center: ({result.Bounds.center.x:F1}, {result.Bounds.center.y:F1}, {result.Bounds.center.z:F1}) | " +
                    $"Size: ({result.Bounds.size.x:F1}, {result.Bounds.size.y:F1}, {result.Bounds.size.z:F1})");
            }

            return results;
        }

        private static bool IsBlockingObstacle(Collider collider, string layerName)
        {
            if (layerName == "terrain" || layerName == "static_solid")
            {
                return true;
            }

            return collider.GetComponentInParent<Leviathan>() != null;
        }

        private static bool TryGetObstacleBounds(Collider collider, Leviathan leviathan, out Bounds bounds)
        {
            if (leviathan != null)
            {
                return TryGetLeviathanBounds(leviathan, out bounds);
            }

            if (collider != null)
            {
                bounds = collider.bounds;
                return true;
            }

            bounds = new Bounds();
            return false;
        }

        private static bool TryGetLeviathanBounds(Leviathan leviathan, out Bounds bounds)
        {
            bounds = new Bounds();

            if (leviathan == null)
            {
                return false;
            }

            var colliders = leviathan.GetComponentsInChildren<Collider>();
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

            return initialized;
        }

        private static void LogScanResult(ShipObstacleScanResultModel result, float scanDistance)
        {
            Plugin.Log.LogInfo(
                $"Destination obstacle scan | " +
                $"{(result.IsBlocked ? "BLOCKED" : "CLEAR")} | " +
                $"Distance: {result.Distance:F1}m | " +
                $"ScanDistance: {scanDistance:F1}m");

            if (!result.IsBlocked)
            {
                return;
            }

            Plugin.Log.LogInfo(
                $"Blocking obstacle | " +
                $"Collider: {result.Collider?.name} | " +
                $"Leviathan: {result.Leviathan != null} | " +
                $"HasBounds: {result.HasBounds}");

            if (!result.HasBounds)
            {
                return;
            }

            Plugin.Log.LogInfo(
                $"Blocking obstacle bounds | " +
                $"Center: ({result.Bounds.center.x:F1}, {result.Bounds.center.y:F1}, {result.Bounds.center.z:F1}) | " +
                $"Size: ({result.Bounds.size.x:F1}, {result.Bounds.size.y:F1}, {result.Bounds.size.z:F1}) | " +
                $"Min: ({result.Bounds.min.x:F1}, {result.Bounds.min.y:F1}, {result.Bounds.min.z:F1}) | " +
                $"Max: ({result.Bounds.max.x:F1}, {result.Bounds.max.y:F1}, {result.Bounds.max.z:F1})");
        }

        private static float GetScanDistance(Ship ship)
        {
            if (ship == null)
            {
                return MinimumScanDistance;
            }

            var body = ShipAccessor.ShipBody(ship);

            if (body == null)
            {
                return MinimumScanDistance;
            }

            var horizontalVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            var speed = horizontalVelocity.magnitude;

            return Mathf.Clamp(MinimumScanDistance + speed * ScanDistancePerSpeed, MinimumScanDistance, MaximumScanDistance);
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

            return collider.transform == ship.transform || collider.transform.IsChildOf(ship.transform);
        }
    }
}