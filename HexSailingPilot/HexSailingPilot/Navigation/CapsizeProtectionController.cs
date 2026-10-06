using UnityEngine;

namespace HexSailingPilot.Navigation
{
    internal static class CapsizeProtectionController
    {
        private const float ActivationAngle = 45f;

        internal static bool IsCorrectionNeeded(Ship ship)
        {
            if (ship == null)
            {
                return false;
            }

            var tiltAngle = Vector3.Angle(ship.transform.up, Vector3.up);

            return tiltAngle >= ActivationAngle;
        }

        internal static float GetTiltAngle(Ship ship)
        {
            if (ship == null)
            {
                return 0f;
            }

            return Vector3.Angle(ship.transform.up, Vector3.up);
        }
    }
}