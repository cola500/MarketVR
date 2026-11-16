// EPIC-015: the park's visitors: figures that leave the counter (or the play table) and stroll on along ParkRoutes,
// out through a gate, the long way round, or to the bench for a while. Rules in Core/ParkVisits (choice, the one seat,
// at most MaxVisitors); this only moves and poses them. Children of the park, so they follow the market and stay on
// the ground. Frame-rate independent: everything advances with dt, and stands still while the market rests.
// Walking: a bob and arm swing tied to the distance walked (no sliding feet), the heading turning smoothly. EPIC-016:
// each walking visitor keeps its distance with a Pacer (brakes softly, steps aside on the gravel, gives way by priority:
// the customer first, then the visitor who came first; never held for ever), and the gait follows the speed actually
// walked. Sitting visitors are someone standing to the others. Sitting: 0.6 s from standing in front of the bench to
// the hip on the seat with the legs bent forward, and back.
using System;
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class ParkVisitors
    {
        public const float SitDownSeconds = 0.6f, SitMinSeconds = 8f, SitMaxSeconds = 15f;
        const float TurnRate = 7f;
        static readonly Vector3 Hip = new(0, 0.37f, -0.05f);         // figure space (scale 1): the torso's bottom
        static readonly Vector3 LegsStanding = new(0, 0.25f, 0);
        static readonly Vector3 Thighs = new(0, 0.44f, -0.27f);        // seated: the legs lie forward over the seat
        static readonly Quaternion ThighsTurn = Quaternion.Euler(-90, 0, 0);
        static readonly Vector3 Shins = new(0, 0.2f, -0.47f);          // and the shins hang down in front of it

        enum Phase { Walking, SittingDown, Sitting, StandingUp }

        sealed class Visitor
        {
            public int Id;
            public Transform Root, FigureRoot;
            public CustomerFigure Figure;
            public Transform Shins;
            public bool Child;
            public Stroll Plan;
            public Polyline Route;
            public float At, Step, Timer, SitFor;
            public Phase Phase;
            public Quaternion Heading;
            public Pacer Pacer;
            public Vector3 Direction; // along the route, where it is now
            public int Priority => int.MaxValue / 2 - Id; // the one who came first goes first
        }

        readonly Art art;
        readonly Transform park;
        readonly ParkVisits visits;
        readonly Func<int, int> random;
        readonly List<Visitor> visitors = new();

        public ParkVisitors(Art art, Transform park, ParkVisits visits, Func<int, int> random)
        {
            this.art = art;
            this.park = park;
            this.visits = visits;
            this.random = random;
        }

        public int Count => visitors.Count;
        public int SittingCount
        {
            get
            {
                var n = 0;
                foreach (var v in visitors) if (v.Phase != Phase.Walking) n++;
                return n;
            }
        }

        public IEnumerable<Transform> Roots { get { foreach (var v in visitors) yield return v.Root; } }

        // The seated visitor's hip (world), for tests; zero if nobody sits.
        public Vector3 SittingHip
        {
            get
            {
                foreach (var v in visitors) if (v.Phase == Phase.Sitting) return v.FigureRoot.TransformPoint(Hip);
                return Vector3.zero;
            }
        }

        // A figure leaves the counter or the play table: it becomes a visitor where it is, on the route its plan asks for.
        public void Add(int variant, bool child, Func<Stroll, Polyline> route, float startAt, float startSpeed = 0)
        {
            if (visits.Full) RemoveFarthest(); // out of sight first: the one nearest a gate
            var v = new Visitor { Id = visits.Begin(), Child = child };
            v.Plan = visits.Choose(v.Id);
            v.Route = route(v.Plan);
            v.At = Mathf.Clamp(startAt, 0, v.Route.Length);
            v.Root = art.Group(park, $"Visitor {v.Id}");
            v.FigureRoot = art.Group(v.Root, "Figure");
            v.FigureRoot.localScale = Vector3.one * (child ? CustomerView.ChildScale : 1f);
            v.Figure = new CustomerFigure(art, v.FigureRoot, variant);
            v.Figure.SetMood(Mood.Happy);
            v.Shins = art.Part(v.Figure.Body, "Shins", art.Capsule, v.Figure.Legs.GetComponent<MeshRenderer>().sharedMaterial, Shins, new Vector3(0.18f, 0.2f, 0.15f));
            v.Shins.gameObject.SetActive(false);
            var (p, dir) = v.Route.At(v.At);
            v.Heading = Quaternion.LookRotation(-dir);
            v.Direction = dir;
            v.Pacer = new Pacer(startSpeed);
            v.Root.SetLocalPositionAndRotation(p, v.Heading);
            visitors.Add(v);
        }

        void RemoveFarthest()
        {
            Visitor far = null;
            foreach (var v in visitors)
                if (far == null || v.Root.localPosition.sqrMagnitude > far.Root.localPosition.sqrMagnitude) far = v;
            if (far != null) Remove(far);
        }

        void Remove(Visitor v)
        {
            visits.End(v.Id);
            Art.Discard(v.Root);
            visitors.Remove(v);
        }

        public void Clear()
        {
            foreach (var v in visitors) Art.Discard(v.Root);
            visitors.Clear();
            visits.Clear();
        }

        // The visitors as the other figures see them (market space): walking with their speed, or standing (sitting).
        public void AddWalkers(List<Walker> into)
        {
            foreach (var v in visitors) into.Add(AsWalker(v));
        }

        static Walker AsWalker(Visitor v)
        {
            var at = v.Root.localPosition;
            at.y = 0;
            return v.Phase == Phase.Walking ? new Walker(at, v.Direction * v.Pacer.Speed, v.Priority) : Walker.Standing(at);
        }

        // others: the other figures (market space): the customer, the children, so nobody walks through anybody.
        public void Update(float dt, bool held, IReadOnlyList<Walker> others)
        {
            if (held || dt <= 0) return;
            for (var i = visitors.Count - 1; i >= 0; i--)
            {
                var v = visitors[i];
                switch (v.Phase)
                {
                    case Phase.Walking:
                        Walk(v, dt, others);
                        if (v.At >= v.Route.Length - 1e-4f)
                        {
                            if (v.Plan == Stroll.Bench)
                            {
                                v.Phase = Phase.SittingDown;
                                v.Timer = 0;
                                v.SitFor = SitMinSeconds + (SitMaxSeconds - SitMinSeconds) * random(1000) / 999f;
                            }
                            else Remove(v);
                        }
                        break;
                    case Phase.SittingDown:
                        v.Timer += dt;
                        Pose(v, Mathf.Clamp01(v.Timer / SitDownSeconds));
                        if (v.Timer >= SitDownSeconds)
                        {
                            v.Phase = Phase.Sitting;
                            v.Timer = 0;
                        }
                        break;
                    case Phase.Sitting:
                        v.Timer += dt;
                        Pose(v, 1);
                        v.FigureRoot.localRotation = Quaternion.Euler(0, Mathf.Sin(v.Timer * 0.7f) * 12, 0); // looks about a little
                        if (v.Timer >= v.SitFor)
                        {
                            v.Phase = Phase.StandingUp;
                            v.Timer = 0;
                        }
                        break;
                    case Phase.StandingUp:
                        v.Timer += dt;
                        v.FigureRoot.localRotation = Quaternion.identity;
                        Pose(v, 1 - Mathf.Clamp01(v.Timer / SitDownSeconds));
                        if (v.Timer >= SitDownSeconds)
                        {
                            visits.ReleaseSeat(v.Id);
                            v.Plan = Stroll.Exit;
                            v.Route = ParkRoutes.BenchToExit();
                            v.At = 0;
                            v.Pacer = new Pacer();
                            v.Phase = Phase.Walking;
                        }
                        break;
                }
            }
        }

        readonly List<Walker> around = new();

        void Walk(Visitor v, float dt, IReadOnlyList<Walker> others)
        {
            around.Clear();
            around.AddRange(others);
            foreach (var w in visitors) if (w != v) around.Add(AsWalker(w));
            var (p, dir) = v.Route.At(v.At);
            var move = v.Pacer.Step(dt, ParkRoutes.WalkSpeed, p, dir, v.Priority, around, v.Route.Length - v.At, ParkLayout.OnPath);
            v.At += move;
            v.Step += move / (v.Child ? 0.4f : 0.55f) * Mathf.PI; // one bob per step: the feet never slide
            (p, dir) = v.Route.At(v.At);
            v.Direction = dir;
            v.Heading = Quaternion.Slerp(v.Heading, Quaternion.LookRotation(-dir), 1 - Mathf.Exp(-TurnRate * dt));
            var pace = v.Pacer.Pace(ParkRoutes.WalkSpeed); // the gait follows the speed: a waiting visitor stands still
            var scale = v.Child ? CustomerView.ChildScale : 1f;
            v.Root.SetLocalPositionAndRotation(p + v.Pacer.Offset(dir) + Vector3.up * Mathf.Abs(Mathf.Sin(v.Step)) * 0.025f * scale * pace, v.Heading);
            var swing = Mathf.Sin(v.Step) * 20 * pace;
            v.Figure.LeftArm.localRotation = Quaternion.Euler(swing, 0, -10);
            v.Figure.WaveArm.localRotation = Quaternion.Euler(-swing, 0, 10);
        }

        // 0 = standing in front of the bench, 1 = seated (hip on the seat, legs bent forward).
        void Pose(Visitor v, float k)
        {
            k = k * k * (3 - 2 * k);
            var facing = Quaternion.LookRotation(-ParkRoutes.BenchFront); // the figure looks along its -z
            var scale = v.Child ? CustomerView.ChildScale : 1f;
            var seated = ParkRoutes.BenchSeat - facing * (Hip * scale);
            v.Root.SetLocalPositionAndRotation(Vector3.Lerp(ParkRoutes.BenchStand, seated, k), Quaternion.Slerp(v.Heading, facing, k));
            v.Figure.Legs.localPosition = Vector3.Lerp(LegsStanding, Thighs, k);
            v.Figure.Legs.localRotation = Quaternion.Slerp(Quaternion.identity, ThighsTurn, k);
            v.Shins.gameObject.SetActive(k > 0.5f);
            v.Figure.LeftArm.localRotation = Quaternion.Euler(-30 * k, 0, -10);
            v.Figure.WaveArm.localRotation = Quaternion.Euler(-30 * k, 0, 10);
        }
    }
}
