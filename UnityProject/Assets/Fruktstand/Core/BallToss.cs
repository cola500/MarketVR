// EPIC-017 (SPEC §33): a lifted ball let go while the hand moves forward becomes a soft toss in an arc that works
// seated. Pure function in table space (y = 0 on the table top, the player's side is -z, the child's +z); unit-tested.
//
//   released low (centre under LiftAbove)              -> None: it rolls as before
//   lifted, hand still or moving back to the player    -> None: it just falls
//   lifted, moving forward within AimCone of the child -> ToChild: exactly into the child's hands (catch point),
//                                                         whatever the hand's speed, so a small toss is enough
//   lifted, moving forward elsewhere                    -> Wide: lands along the movement, MinDistance..MaxDistance
//                                                         (more with a faster hand); it may miss the table
//
// The arc tops out Apex above the higher end. The toss's speed comes from the plan, never straight from the hand.
using UnityEngine;

namespace Fruktstand.Core
{
    public static class BallToss
    {
        public enum Kind { None, ToChild, Wide }

        public const float LiftAbove = 0.08f;    // ball centre over the table top (it lies at 0.045)
        public const float MinHandSpeed = 0.25f; // m/s, horizontal
        public const float AimCone = 35f;        // degrees either side of straight at the child
        public const float Apex = 0.08f;         // the arc's top over the higher end
        public const float MinDistance = 0.20f, MaxDistance = 0.60f; // a wide toss (horizontal)
        const float DistancePerSpeed = 0.6f;     // m further per m/s of hand speed over MinHandSpeed
        public const float MaxSpeed = 2.4f;
        public const float Gravity = 9.81f;

        public readonly struct Toss
        {
            public Toss(Kind kind, Vector3 velocity, Vector3 target, float flight)
            {
                Kind = kind;
                Velocity = velocity;
                Target = target;
                Flight = flight;
            }

            public Kind Kind { get; }
            public Vector3 Velocity { get; }
            public Vector3 Target { get; }
            public float Flight { get; } // seconds until it is at Target
        }

        public static Toss Plan(Vector3 release, Vector3 hand, Vector3 catchPoint)
        {
            var flat = new Vector3(hand.x, 0, hand.z);
            if (release.y < LiftAbove || flat.magnitude < MinHandSpeed || flat.z <= 0) return default;
            var toChild = catchPoint - release;
            toChild.y = 0;
            if (Vector3.Angle(flat, toChild) <= AimCone) return Arc(Kind.ToChild, release, catchPoint);
            var distance = Mathf.Clamp(MinDistance + (flat.magnitude - MinHandSpeed) * DistancePerSpeed, MinDistance, MaxDistance);
            var target = release + flat.normalized * distance;
            target.y = 0;
            return Arc(Kind.Wide, release, target);
        }

        static Toss Arc(Kind kind, Vector3 from, Vector3 to)
        {
            var top = Mathf.Max(from.y, to.y) + Apex;
            var up = Mathf.Sqrt(2 * Gravity * (top - from.y));
            var flight = up / Gravity + Mathf.Sqrt(2 * (top - to.y) / Gravity);
            var across = new Vector3(to.x - from.x, 0, to.z - from.z) / flight;
            return new Toss(kind, Vector3.ClampMagnitude(across + Vector3.up * up, MaxSpeed), to, flight);
        }
    }
}
