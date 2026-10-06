using System.Collections.Generic;
using UnityEngine;

namespace HexSailingPilot.Models
{
    internal sealed class SailingRouteModel
    {
        private readonly List<Vector3> _waypoints;

        internal Vector3 Origin { get; }
        internal Vector3 Destination { get; }
        internal IReadOnlyList<Vector3> Waypoints => _waypoints;
        internal int CurrentWaypointIndex { get; private set; }
        internal bool IsComplete => CurrentWaypointIndex >= _waypoints.Count;

        internal SailingRouteModel(Vector3 origin, Vector3 destination, List<Vector3> waypoints)
        {
            Origin = origin;
            Destination = destination;
            _waypoints = waypoints ?? new List<Vector3>();
            CurrentWaypointIndex = 0;
        }

        internal Vector3 GetCurrentWaypoint()
        {
            if (IsComplete)
            {
                return Destination;
            }

            return _waypoints[CurrentWaypointIndex];
        }

        internal bool Advance()
        {
            if (IsComplete)
            {
                return false;
            }

            CurrentWaypointIndex++;
            return !IsComplete;
        }
    }
}