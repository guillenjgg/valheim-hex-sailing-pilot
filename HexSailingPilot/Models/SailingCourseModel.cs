using UnityEngine;

namespace HexSailingPilot.Models
{
    internal sealed class SailingCourseModel
    {
        internal Vector3 Origin { get; }
        internal Vector3 Direction { get; }
        internal Vector3 Destination { get; }
        internal float Length { get; }

        internal SailingCourseModel(Vector3 origin, Vector3 destination)
        {
            Origin = origin;
            Destination = destination;

            var direction = Destination - Origin;
            direction.y = 0f;

            Length = direction.magnitude;
            Direction = direction.normalized;
        }
    }
}