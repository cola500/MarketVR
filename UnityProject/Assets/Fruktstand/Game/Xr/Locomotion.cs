// Seated play (EPIC-005): moving the XR rig through the world with the sticks. The market never moves; only the rig
// (the tracking space) is shifted or turned, so natural head and body movement stays exactly as tracked.
// - Walk: horizontal only, relative to the head's yaw (looking up/down or tilting the head changes neither the speed
//   nor the height), WalkSpeed at full stick, frame-rate independent (speed * dt).
// - Turn: snap steps around the head's current position, so a head that is off the rig's centre does not jump.
// - The walk target is passed through a constraint (the furniture, MarketCollision) before the rig moves.
// Pure transform maths, tested in EditMode with plain GameObjects.
using System;
using UnityEngine;

namespace Fruktstand.Game
{
    public static class Locomotion
    {
        public const float WalkSpeed = 0.5f; // m/s at full stick [A: calm, check on the headset]

        // The head's horizontal heading. Looking straight up or down, the head's up vector gives the heading instead.
        public static Vector3 Heading(Quaternion head)
        {
            var f = head * Vector3.forward;
            var flat = new Vector3(f.x, 0, f.z);
            if (flat.sqrMagnitude < 0.01f)
            {
                var u = head * Vector3.up;
                flat = f.y < 0 ? new Vector3(u.x, 0, u.z) : new Vector3(-u.x, 0, -u.z);
            }
            return flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.forward;
        }

        // World displacement for one frame (y is always 0). walk: x = sideways, y = forward, |walk| <= 1.
        public static Vector3 Displacement(Vector2 walk, Quaternion head, float dt)
        {
            if (dt <= 0 || walk == Vector2.zero) return Vector3.zero;
            if (walk.sqrMagnitude > 1) walk.Normalize();
            var forward = Heading(head);
            var rightDir = new Vector3(forward.z, 0, -forward.x);
            return (forward * walk.y + rightDir * walk.x) * (WalkSpeed * dt);
        }

        // Snap turn of the rig around the head's world position (the head stays where it is).
        public static void Turn(Transform rig, Transform head, float degrees)
        {
            if (degrees == 0) return;
            rig.RotateAround(head.position, Vector3.up, degrees);
        }

        // Walks the rig; constrain(fromHead, toHead) may shorten or redirect the move (world space). Returns the move.
        public static Vector3 Walk(Transform rig, Transform head, Vector2 walk, float dt, Func<Vector3, Vector3, Vector3> constrain = null)
        {
            var d = Displacement(walk, head.rotation, dt);
            if (d == Vector3.zero) return d;
            var from = head.position;
            var to = constrain != null ? constrain(from, from + d) : from + d;
            var move = new Vector3(to.x - from.x, 0, to.z - from.z); // never vertical
            rig.position += move;
            return move;
        }
    }
}
