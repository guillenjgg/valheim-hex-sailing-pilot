using HexSailingPilot.Models;
using UnityEngine;

namespace HexSailingPilot.Navigation
{
    // Pure course/steering math. Operates on a SailingCourseModel and ship
    // position/heading data only - no Ship reflection or pilot state here.
    internal static class SteeringCalculator
    {
        internal const float LookAheadDistance = 50f;
        internal const float HeadingTolerance = 2f;
        internal const float FullRudderHeadingError = 20f;
        internal const float RudderTolerance = 0.02f;

        internal static Vector3 GetLookAheadPoint(SailingCourseModel course, Vector3 shipPosition)
        {
            var fromOrigin = shipPosition - course.Origin;
            fromOrigin.y = 0f;

            var distanceAlongCourse = Vector3.Dot(fromOrigin, course.Direction);
            var lookAheadDistance = Mathf.Min(distanceAlongCourse + LookAheadDistance, course.Length);

            return course.Origin + course.Direction * lookAheadDistance;
        }

        internal static float GetDistanceToDestination(SailingCourseModel course, Vector3 shipPosition)
        {
            var offset = course.Destination - shipPosition;
            offset.y = 0f;

            return offset.magnitude;
        }

        internal static float GetCrossTrackError(SailingCourseModel course, Vector3 position)
        {
            var fromOrigin = position - course.Origin;
            fromOrigin.y = 0f;

            return Vector3.Dot(fromOrigin, Vector3.Cross(Vector3.up, course.Direction));
        }

        internal static float GetTargetRudder(float headingError)
        {
            if (Mathf.Abs(headingError) <= HeadingTolerance)
            {
                return 0f;
            }

            return Mathf.Clamp(headingError / FullRudderHeadingError, -1f, 1f);
        }

        internal static void SetRudderInput(float targetRudder, float rudderValue, ref Vector3 moveDir)
        {
            var rudderError = targetRudder - rudderValue;

            if (Mathf.Abs(rudderError) <= RudderTolerance)
            {
                moveDir.x = 0f;
                return;
            }

            moveDir.x = Mathf.Sign(rudderError);
        }

        internal static float GetHeading(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return 0f;
            }

            direction.Normalize();

            var heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            return NormalizeHeading(heading);
        }

        private static float NormalizeHeading(float heading)
        {
            heading %= 360f;

            if (heading < 0f)
            {
                heading += 360f;
            }

            return heading;
        }
    }
}
