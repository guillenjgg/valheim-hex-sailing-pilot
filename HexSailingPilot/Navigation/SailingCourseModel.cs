using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal sealed class SailingCourseModel
    {
        internal Vector3 Origin { get; }
        internal Vector3 Direction { get; }
        internal Vector3 Destination { get; }
        internal float Length { get; }

        internal SailingCourseModel(Vector3 origin, Vector3 direction, float length)
        {
            Origin = origin;

            direction.y = 0f;
            direction.Normalize();

            Direction = direction;
            Length = length;
            Destination = Origin + Direction * Length;
        }
    }
}