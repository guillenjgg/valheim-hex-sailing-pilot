using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HexSailingPilot.ShipAccess
{
    // IMPORTANT: These members are not actually public on the real Ship assembly.
    // We are compiling against a publicized assembly, so Harmony AccessTools
    // reflection must be used instead of direct member access.
    internal static class ShipAccessor
    {
        internal static readonly AccessTools.FieldRef<Ship, float> RudderValue = AccessTools.FieldRefAccess<Ship, float>("m_rudderValue");
        internal static readonly AccessTools.FieldRef<Ship, Ship.Speed> ShipSpeed = AccessTools.FieldRefAccess<Ship, Ship.Speed>("m_speed");
        internal static readonly AccessTools.FieldRef<Ship, bool> ForwardPressed = AccessTools.FieldRefAccess<Ship, bool>("m_forwardPressed");
        internal static readonly AccessTools.FieldRef<Ship, bool> BackwardPressed = AccessTools.FieldRefAccess<Ship, bool>("m_backwardPressed");
        internal static readonly AccessTools.FieldRef<Ship, Rigidbody> ShipBody = AccessTools.FieldRefAccess<Ship, Rigidbody>("m_body");
        internal static readonly MethodInfo StopMethod = AccessTools.Method(typeof(Ship), "Stop");
    }
}
