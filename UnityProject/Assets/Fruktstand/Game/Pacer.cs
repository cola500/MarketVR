// EPIC-016: one walking figure's pace along its route, so figures on the park's paths do not walk through each other.
// Pure (no scene, no physics), stepped by whoever moves the figure (CustomerView, ParkVisitors); market space, on the
// ground. No path planning: the figure stays on its route and at most steps a little to the side.
//
// - Looks ahead PredictSeconds at everyone near: someone it would come closer to than Clearance is "in the way".
// - In the way and standing, or in front walking the same way: it brakes softly (speed eases over RampSeconds), so it
//   queues behind or stops short of them, and steps aside round someone standing.
// - A meeting (both in front of each other: face to face, or crossing): the figure with priority walks on, the other
//   gives way; equal priorities are settled by position, so both sides always agree. Face to face, both step aside away
//   from each other, the one giving way first. A step aside only goes where `walkable` allows (the gravel).
// - Nobody is held for ever: after MaxWait held up, a figure walks on slowly for PushSeconds whatever is in the way.
// - A step aside is kept until the other is behind (never back into someone still alongside).
// - Speed is the speed actually walked: the gait (Pace) follows it, so a waiting figure's legs and arms rest.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Game
{
    // Another figure as a walker sees it: where it is, how it moves (zero = standing) and its priority at a meeting.
    public readonly struct Walker
    {
        public readonly Vector3 Position, Velocity;
        public readonly int Priority;

        public Walker(Vector3 position, Vector3 velocity, int priority)
        {
            Position = position;
            Velocity = velocity;
            Priority = priority;
        }

        public static Walker Standing(Vector3 p) => new(p, Vector3.zero, int.MaxValue);
        public bool Moving => Velocity.sqrMagnitude > 0.05f * 0.05f;
    }

    public sealed class Pacer
    {
        public const float Clearance = 0.36f;    // closer than this (centre to centre) is "in the way": a figure is ~0.3 m wide
        public const float Notice = 1.5f;         // starts to slow down this far from someone in the way
        public const float Stop = 0.5f;           // and stands this far from them
        public const float PredictSeconds = 1.5f;
        public const float MaxSide = 0.4f;        // the largest step aside from the route
        public const float SideSpeed = 0.45f;     // m/s sideways
        public const float RampSeconds = 0.37f;   // from standing to full pace (and back): no sudden stop or start. A
                                                  // time, not m/s², so a test that speeds up the walk speeds this up too
        public static float MaxAccel(float maxSpeed) => maxSpeed / RampSeconds; // ≈ 3 m/s² at the usual 1.1 m/s
        public const float MaxWait = 3f;          // held up this long: walks on anyway
        public const float PushSeconds = 2f, PushPace = 0.5f;

        float waited, push;

        public Pacer(float speed = 0) => Speed = speed;

        public float Speed { get; private set; }  // m/s actually walked
        public float Side { get; private set; }   // metres to the right of the route
        public float Pace(float maxSpeed) => maxSpeed > 0 ? Mathf.Clamp01(Speed / maxSpeed) : 0; // 0 = standing, 1 = full stride

        public Vector3 Offset(Vector3 direction) => Right(direction) * Side;

        static Vector3 Right(Vector3 d) => new Vector3(d.z, 0, -d.x).normalized;
        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        // Moves the figure on: returns how far along the route it walks this step. onRoute/direction: the route's point
        // and direction at the figure's distance; remaining: route length still ahead (the step aside is gone at the end).
        public float Step(float dt, float maxSpeed, Vector3 onRoute, Vector3 direction, int priority, IReadOnlyList<Walker> others,
            float remaining, Func<Vector3, bool> walkable = null)
        {
            if (dt <= 0) return 0;
            var dir = Flat(direction).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;
            var right = Right(dir);
            var here = Flat(onRoute) + right * Side;
            var mine = dir * maxSpeed;

            var brakeGap = float.MaxValue;
            var away = 0;          // -1 left, +1 right, 0 no step aside wanted
            var deadAhead = false; // nobody is on either side yet: either side will do
            var awayGap = float.MaxValue;
            foreach (var o in others)
            {
                var r = Flat(o.Position) - here;
                var gap = r.magnitude;
                if (gap > Notice * 2.5f) continue;
                var ahead = Vector3.Dot(r, dir);
                var lateral = Vector3.Dot(r, right);
                var v = Flat(o.Velocity);
                var rel = v - mine;
                var t = rel.sqrMagnitude > 1e-6f ? Mathf.Clamp(-Vector3.Dot(r, rel) / rel.sqrMagnitude, 0, PredictSeconds) : 0;
                var closest = (r + rel * t).magnitude;
                var inWay = closest < Clearance && ahead > 0.05f;
                // Keep the step aside while someone standing is ahead or beside, and while someone walking past is still
                // alongside: back to the route's line only once they are behind.
                var beside = Mathf.Abs(lateral) < Clearance + 0.1f && ahead > -0.45f && ahead < (o.Moving ? 0.3f : Notice);

                var oDir = v.normalized;
                var meeting = o.Moving && Vector3.Dot(-r, oDir) > 0.05f; // I am in front of them too
                var headOn = meeting && Vector3.Dot(oDir, dir) < -0.3f;
                var wins = meeting && (priority > o.Priority || priority == o.Priority && Before(here, o.Position));

                if (inWay && (!wins || gap < Stop)) brakeGap = Mathf.Min(brakeGap, gap);
                // Step aside round someone standing, or away from someone coming face to face.
                if ((beside || inWay && (!o.Moving || headOn)) && gap < awayGap)
                {
                    awayGap = gap;
                    deadAhead = Mathf.Abs(lateral) < 0.03f;
                    if (!deadAhead) away = lateral > 0 ? -1 : 1;
                    else away = wins && o.Moving ? 0 : 1; // dead ahead: the one giving way picks its right first
                }
            }

            // Sideways: towards the wanted side if the gravel allows it, back to the route's line otherwise.
            var targetSide = away == 0 ? (awayGap < float.MaxValue ? Side : 0) : Reachable(onRoute, right, away, deadAhead, walkable);
            targetSide = Mathf.Clamp(targetSide, -Mathf.Max(0, remaining - 0.15f), Mathf.Max(0, remaining - 0.15f));
            Side = Mathf.MoveTowards(Side, targetSide, SideSpeed * dt);
            Side = Mathf.Clamp(Side, -Mathf.Max(0, remaining - 0.15f), Mathf.Max(0, remaining - 0.15f));

            // Forwards: ease towards the speed the way ahead allows.
            float target;
            if (push > 0)
            {
                push -= dt;
                target = maxSpeed * PushPace;
            }
            else target = brakeGap < float.MaxValue ? maxSpeed * Mathf.Clamp01((brakeGap - Stop) / (Notice - Stop)) : maxSpeed;
            Speed = Mathf.MoveTowards(Speed, target, MaxAccel(maxSpeed) * dt);
            if (push <= 0 && Speed < 0.1f * maxSpeed && target < 0.1f * maxSpeed)
            {
                waited += dt;
                if (waited >= MaxWait)
                {
                    waited = 0;
                    push = PushSeconds; // never held for ever
                }
            }
            else if (push <= 0) waited = Mathf.Max(0, waited - dt);
            return Mathf.Min(Speed * dt, Mathf.Max(0, remaining));
        }

        // The step aside towards `side` (+1 right, -1 left) that stays walkable: the full step or a smaller one; the other
        // side only when the other figure is dead ahead (never towards someone already on that side); else none.
        float Reachable(Vector3 onRoute, Vector3 right, int side, bool eitherSide, Func<Vector3, bool> walkable)
        {
            if (walkable == null) return side * MaxSide;
            foreach (var s in eitherSide ? new[] { side, -side } : new[] { side })
                foreach (var k in new[] { 1f, 0.75f })
                    if (walkable(onRoute + right * (s * MaxSide * k))) return s * MaxSide * k;
            return walkable(onRoute + right * Side) ? Side : 0;
        }

        // A tie-break both sides agree on: by position.
        static bool Before(Vector3 a, Vector3 b) => a.x != b.x ? a.x > b.x : a.z > b.z;
    }
}
