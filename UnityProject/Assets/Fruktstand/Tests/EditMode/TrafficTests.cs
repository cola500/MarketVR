// EPIC-016: figures on the park's paths keep their distance without physics or path planning (Game/Pacer): one brakes
// softly behind another, steps aside round someone standing, and at a meeting the one with lower priority gives way.
// Nobody is held for ever: after Pacer.MaxWait a held-up figure walks on slowly. Names match docs/EPIC-016.md.
using System.Collections.Generic;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class TrafficTests
    {
        const float Dt = 0.02f, Speed = 1.1f, Body = 0.3f;

        sealed class Sim
        {
            public Polyline Route;
            public readonly Pacer Pacer = new();
            public float At, MaxSpeedChange, WorstSide;
            public int Priority;
            public Vector3 Position, Heading;
            public bool Done => At >= Route.Length - 1e-3f;
        }

        static Sim Walk(int priority, params Vector3[] points) => new() { Route = new Polyline(points), Priority = priority };

        // Steps every walker until all are done or the time is up; returns the closest any two figures came.
        static float Run(IReadOnlyList<Sim> sims, IReadOnlyList<Vector3> standing, float seconds, System.Func<Vector3, bool> walkable = null, float dt = Dt)
        {
            var closest = float.MaxValue;
            for (var t = 0f; t < seconds; t += dt)
            {
                foreach (var s in sims)
                {
                    var (p, dir) = s.Route.At(s.At);
                    s.Position = p + s.Pacer.Offset(dir);
                    s.Heading = dir;
                }
                var allDone = true;
                foreach (var s in sims)
                {
                    if (s.Done) continue;
                    allDone = false;
                    var others = new List<Walker>();
                    foreach (var o in sims) if (o != s && !o.Done) others.Add(new Walker(o.Position, o.Heading * o.Pacer.Speed, o.Priority));
                    foreach (var p in standing) others.Add(Walker.Standing(p));
                    var (onRoute, dir) = s.Route.At(s.At);
                    var before = s.Pacer.Speed;
                    s.At += s.Pacer.Step(dt, Speed, onRoute, dir, s.Priority, others, s.Route.Length - s.At, walkable);
                    s.MaxSpeedChange = Mathf.Max(s.MaxSpeedChange, Mathf.Abs(s.Pacer.Speed - before));
                    s.WorstSide = Mathf.Max(s.WorstSide, Mathf.Abs(s.Pacer.Side));
                    foreach (var o in sims) if (o != s && !o.Done) closest = Mathf.Min(closest, Flat(s.Position - o.Position));
                    foreach (var p in standing) closest = Mathf.Min(closest, Flat(s.Position - p));
                }
                if (allDone) break;
            }
            return closest;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        [Test]
        public void Traffic_BrakesBehindSlowerWalker()
        {
            var leader = new Sim { Route = new Polyline(new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 4) }), Priority = 1 };
            var follower = Walk(2, new Vector3(0, 0, 0), new Vector3(0, 0, 8));
            var closest = float.MaxValue;
            for (var t = 0f; t < 4; t += Dt)
            {
                leader.At += 0.4f * Dt; // a slow walker ahead, on the same path
                var (lp, ld) = leader.Route.At(leader.At);
                var (p, dir) = follower.Route.At(follower.At);
                follower.At += follower.Pacer.Step(Dt, Speed, p, dir, follower.Priority, new[] { new Walker(lp, ld * 0.4f, leader.Priority) }, follower.Route.Length - follower.At);
                closest = Mathf.Min(closest, Flat(lp - (p + follower.Pacer.Offset(dir))));
            }
            Assert.GreaterOrEqual(closest, Pacer.Stop * 0.8f, "keeps a little distance behind");
            Assert.AreEqual(0.4f, follower.Pacer.Speed, 0.1f, "and walks at the pace of the one in front");
            Assert.AreEqual(0, follower.Pacer.Side, 1e-3f, "queues behind, no overtaking");
        }

        [Test]
        public void Traffic_HeadOnMeetingBothPass()
        {
            var a = Walk(1, new Vector3(0, 0, 0), new Vector3(0, 0, 6));
            var b = Walk(2, new Vector3(0, 0, 6), new Vector3(0, 0, 0));
            var closest = Run(new[] { a, b }, new Vector3[0], 12);
            Assert.GreaterOrEqual(closest, Body, "never through each other");
            Assert.IsTrue(a.Done && b.Done, "both got past and on to the end");
        }

        [Test]
        public void Traffic_HeadOnMeetingWithEqualPriorityBothPass()
        {
            var a = Walk(5, new Vector3(0, 0, 0), new Vector3(0, 0, 6));
            var b = Walk(5, new Vector3(0, 0, 6), new Vector3(0, 0, 0));
            Assert.GreaterOrEqual(Run(new[] { a, b }, new Vector3[0], 12), Body);
            Assert.IsTrue(a.Done && b.Done, "a tie never locks");
        }

        [Test]
        public void Traffic_StandingFigureIsWalkedRound()
        {
            var a = Walk(1, new Vector3(0, 0, 0), new Vector3(0, 0, 5));
            var closest = Run(new[] { a }, new[] { new Vector3(0.05f, 0, 2.5f) }, 10);
            Assert.GreaterOrEqual(closest, Body, "round the one standing on the path, not through");
            Assert.IsTrue(a.Done);
            Assert.AreEqual(0, a.Pacer.Side, 1e-3f, "back on the path's line at the end");
            Assert.LessOrEqual(a.WorstSide, Pacer.MaxSide + 1e-4f, "only a small step aside");
        }

        [Test]
        public void Traffic_SidestepStaysOnThePath()
        {
            // a narrow path (|x| < 0.5): the step aside goes the way that stays on it
            System.Func<Vector3, bool> path = p => Mathf.Abs(p.x - 0.3f) < 0.5f; // only room to the right (+x)
            var a = Walk(1, new Vector3(0, 0, 0), new Vector3(0, 0, 5));
            var b = Walk(2, new Vector3(0, 0, 5), new Vector3(0, 0, 0));
            var closest = Run(new[] { a, b }, new Vector3[0], 14, path);
            Assert.IsTrue(a.Done && b.Done);
            Assert.GreaterOrEqual(closest, Body);
        }

        [Test]
        public void Traffic_CrossingWalkersNeverLock()
        {
            var a = Walk(3, new Vector3(-3, 0, 0), new Vector3(3, 0, 0));
            var b = Walk(3, new Vector3(0, 0, -3), new Vector3(0, 0, 3));
            var closest = Run(new[] { a, b }, new Vector3[0], 14);
            Assert.IsTrue(a.Done && b.Done, "both cross");
            Assert.GreaterOrEqual(closest, Body);
        }

        [Test]
        public void Traffic_NoPermanentLockWhenBoxedIn()
        {
            // a row of people across a path with no room to step aside: after MaxWait the walker goes on, slowly
            System.Func<Vector3, bool> narrow = p => Mathf.Abs(p.x) < 0.2f;
            var a = Walk(1, new Vector3(0, 0, 0), new Vector3(0, 0, 4));
            Run(new[] { a }, new[] { new Vector3(-0.3f, 0, 2), new Vector3(0, 0, 2), new Vector3(0.3f, 0, 2) }, Pacer.MaxWait + 8, narrow);
            Assert.IsTrue(a.Done, "a blocked figure is never held for ever");
        }

        [Test]
        public void Traffic_SpeedChangesSmoothly()
        {
            var a = Walk(1, new Vector3(0, 0, 0), new Vector3(0, 0, 6));
            var b = Walk(2, new Vector3(0, 0, 6), new Vector3(0, 0, 0));
            Run(new[] { a, b }, new Vector3[0], 12);
            var limit = Pacer.MaxAccel(Speed) * Dt + 1e-3f;
            Assert.LessOrEqual(a.MaxSpeedChange, limit, "no sudden stop or start");
            Assert.LessOrEqual(b.MaxSpeedChange, limit);
        }

        [Test]
        public void Traffic_StepAsideIsKeptUntilTheOtherIsBehind()
        {
            // scene-sized steps: just after passing, neither turns back into the other
            var a = Walk(1, new Vector3(0, 0, 0), new Vector3(0, 0, 6));
            var b = Walk(2, new Vector3(0, 0, 6), new Vector3(0, 0, 0));
            Assert.GreaterOrEqual(Run(new[] { a, b }, new Vector3[0], 12, null, 0.05f), Pacer.Clearance - 0.02f);
        }

        [Test]
        public void Traffic_CustomerMeetsLeavingVisitorOnTheArm()
        {
            // the real routes: the next customer walks in from the front gate as the last one walks out of it
            var d = MarketLayout.DefaultStallDistance;
            var customer = new Sim { Route = ParkRoutes.Arrival(Fruktstand.Core.Category.FruitVeg, d), Priority = CustomerView.Priority };
            var visitor = new Sim { Route = ParkRoutes.Departure(Fruktstand.Core.Category.FruitVeg, d, Fruktstand.Core.Stroll.Exit), Priority = 1000 };
            var closest = Run(new[] { customer, visitor }, new Vector3[0], 40, ParkLayout.OnPath);
            Assert.IsTrue(customer.Done && visitor.Done);
            var what = $"closest {closest:F2}: customer stepped {customer.WorstSide:F2} m aside, the visitor {visitor.WorstSide:F2} m";
            Assert.GreaterOrEqual(closest, Body, what);
            Assert.Greater(customer.WorstSide, 0.05f, "both step aside, away from each other: " + what);
        }

        [Test]
        public void Traffic_FasterWalkRampsUpAsQuickly()
        {
            // tests may raise ParkRoutes.WalkSpeed to shorten walks: the start must scale with it
            foreach (var speed in new[] { 1.1f, 30f })
            {
                var pacer = new Pacer();
                var t = 0f;
                for (; t < 5 && pacer.Speed < speed * 0.99f; t += Dt) pacer.Step(Dt, speed, Vector3.zero, Vector3.forward, 1, new Walker[0], 1000);
                Assert.AreEqual(Pacer.RampSeconds, t, 0.05f, $"full pace after the same short ramp at {speed} m/s");
            }
        }

        [Test]
        public void Traffic_PaceShowsHowFastTheFigureWalks()
        {
            var pacer = new Pacer();
            for (var t = 0f; t < 3; t += Dt) pacer.Step(Dt, Speed, Vector3.zero, Vector3.forward, 1, new[] { Walker.Standing(new Vector3(0, 0, 0.4f)) }, 10, _ => false);
            Assert.Less(pacer.Pace(Speed), 0.05f, "held up: the gait stands still too");
            pacer = new Pacer();
            for (var t = 0f; t < 3; t += Dt) pacer.Step(Dt, Speed, Vector3.zero, Vector3.forward, 1, new Walker[0], 10);
            Assert.AreEqual(1, pacer.Pace(Speed), 0.02f, "free: full stride");
        }
    }
}
