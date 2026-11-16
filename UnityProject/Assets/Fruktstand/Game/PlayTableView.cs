// The play area (EPIC-003 SPEC §17, EPIC-010 §27): a cosy rug, cushions and a low rounded play table with a low rim, a
// painted oval road, a ball corner and a building corner. Three children (one per zone) carry their toy here from the
// toy stall and play: the ball is rolled to the player's side and the top is set spinning (left), the car and the train
// are driven round the road (middle), the blocks become a tower and the rings go onto their peg (right).
// The rules (who may move a toy, when a child comes or goes) are Core.PlayArea; this view only moves things.
// Table space: x across (player's view), +z away from the player, y = 0 on the table top.
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        public static readonly Vector3 TableSize = new(1.0f, 0.04f, 0.5f); // compact market (SPEC §14)
        public const float RimHeight = 0.025f;
        public const float MaxThrowSpeed = 1.2f;   // m/s, released toys never fly off wildly
        public const float LostBelow = 0.25f;      // under the table top by this much = on the floor
        public const float LostSeconds = 2f;
        const float ChildPushSpeed = 0.55f;
        const float CarSpeed = 0.15f;
        const float FetchAfterSeconds = 10f;
        const float RestSpeed = 0.04f;

        // Table-top space.
        public static readonly Vector3 BallHome = new(-0.3f, Models.BallRadius, 0.12f);
        public static readonly Vector3 BallTarget = new(-0.25f, Models.BallRadius, -0.15f);
        public static readonly Vector3 RoadCentre = new(0.02f, 0, 0f);
        public static readonly Vector2 RoadRadii = new(0.14f, 0.09f);
        // EPIC-017: the shared building site (the middle of its bottom row, BuildSite's grid) and the child's catch point:
        // the child's hands just inside the far rim, above the ball corner.
        public static readonly Vector3 BuildSpot = new(0.3f, 0, 0.04f);
        public static readonly Vector3 CatchPoint = new(-0.30f, 0.10f, 0.20f);
        public const float CatchRadius = 0.12f;
        const float CatchHoldSeconds = 0.5f;
        const float BallDamping = 0.35f;
        public static readonly Vector3 RingSpot = new(0.3f, 0, 0.12f); // the rings' peg (the building corner's zone)
        public static readonly Vector3 TrainSize = new(0.055f, 0.077f, 0.152f); // collider (the Blender model's bounds)
        public static readonly Vector3 TopSize = new(0.06f, 0.074f, 0.06f);
        public const float SpinSeconds = 3f;
        const float SpinRestSeconds = 2f;
        const float TrainSpeed = 0.1f;
        const float RingEverySeconds = 2f;
        public const float RingHelpRadius = 0.08f; // a ring released this close to the peg (horizontally) goes onto it

        static bool OnRoad(Ware toy) => toy is Ware.Car or Ware.Train;
        static float RoadLift(Ware toy) => toy == Ware.Train ? TrainSize.y / 2 + 0.009f : Models.CarSize.y / 2 + 0.019f;
        public static readonly Vector3 TidyButton = new(0.05f, 0.03f, -0.29f);
        // EPIC-006: the crocodile's own little shelf on the table's end by the candy stall (player's side), level with
        // the table top. The crocodile lies along table +x (its snout points at the work area beyond the table's end).
        public static readonly Vector3 ShelfSize = new(0.40f, 0.03f, 0.22f);
        public static readonly Vector3 ShelfCentre = new(-0.70f, 0, -0.14f);
        public static readonly Vector3 CrocOrigin = new(-0.705f, 0, -0.14f);
        public const float CrocYaw = 0f;

        // EPIC-017: the child's three blocks wait in the row next to the build, the player's three in the row nearest
        // the player. Both rows are within a seated player's reach (SPEC §14), as every toy must be.
        public static Vector3 BlockPile(int i) => new(0.24f + 0.07f * (i % 3), Models.BlockSize / 2, IsChildsBlock(i) ? -0.06f : -0.13f);

        public static Vector3 RoadPoint(float t) => RoadPoint(t, Ware.Car);

        public static Vector3 RoadPoint(float t, Ware vehicle)
        {
            var a = t * Mathf.PI * 2;
            return RoadCentre + new Vector3(Mathf.Cos(a) * RoadRadii.x, RoadLift(vehicle), Mathf.Sin(a) * RoadRadii.y);
        }

        public static Vector3 RingPile(int i) => new(0.17f + 0.085f * i, Models.RingThickness * Models.RingScale[i] / 2, -0.12f);

        // Where the ring in stack slot k sits on the peg (slot 0 on the base).
        public static Vector3 RingStack(int slot) => RingSpot + Vector3.up * (0.013f + Models.RingThickness * (slot + 0.5f));

        public static float NearestRoadT(Vector3 local)
        {
            var d = local - RoadCentre;
            var a = Mathf.Atan2(d.z / RoadRadii.y, d.x / RoadRadii.x);
            return Mathf.Repeat(a / (Mathf.PI * 2), 1);
        }

        // Ground positions in table space: the children sit across the table, away from the work area.
        public event System.Action<int, Vector3> ChildWentHome; // EPIC-015: (look, world position) after playing
        public const float LeaveDistance = 1.5f; // a child leaves straight away from the table (table space, +z)
        public static Vector3 ChildSpot(Ware toy) => new(PlayArea.SpotOf(toy) switch { 0 => -0.32f, 1 => 0f, _ => 0.32f }, 0, 0.45f);

        // A child walks from the toy stall round the outside: first straight to the far side's line, then along it,
        // so the path never crosses the player's work area (SPEC §14).
        public static Vector3 WalkPoint(Vector3 from, Vector3 to, float k)
        {
            var corner = new Vector3(from.x, 0, to.z);
            var a = Vector3.Distance(from, corner);
            var b = Vector3.Distance(corner, to);
            var d = k * (a + b);
            return d <= a ? Vector3.Lerp(from, corner, a > 0 ? d / a : 1) : Vector3.Lerp(corner, to, b > 0 ? (d - a) / b : 1);
        }

        public static Vector3 Home(Ware toy, int index = 0) => toy switch
        {
            Ware.Ball => BallHome,
            Ware.Top => new Vector3(BallHome.x, TopSize.y / 2, BallHome.z),
            Ware.Car or Ware.Train => RoadPoint(0.75f, toy),
            Ware.StackingRings => RingPile(index),
            Ware.Frogs => FrogPond.Home((FrogOwner)index), // EPIC-018: by owner (on the table top)
            _ => BlockPile(index),
        };

        sealed class Child
        {
            public Transform Root, Carry, Stool, FigureRoot;
            public CustomerFigure Figure;
            public Ware Toy;               // what this child plays with (EPIC-010: the zone may hold any of its toys)
            public Vector3 From;           // table space, where the walk starts (the toy stall)
            public Item Carried;           // the very toy bought, until it is put on the table
            public readonly List<Item> Toys = new();
            public bool Placed;
            public float Rest;             // the player's release lying still (and the ball's own wait before a roll)
            public int Variant;            // EPIC-015: the figure's look, carried on into the park
            public bool WentHome;          // EPIC-015: walked away from the table (Leaving), not cleared
            public ToyPlay Play;           // what only this toy does (PlayTableView.<Toy>.cs)
        }

        readonly Art art;
        readonly Transform root;
        readonly Transform top;
        readonly Transform[] legs = new Transform[4];
        readonly List<Item> items;
        readonly Sounds sounds;
        readonly Child[] children = new Child[PlayArea.Spots];
        float accumulator;

        // The child playing with exactly this toy, if any (its zone may hold another toy of the same zone).
        Child ChildOf(Ware toy)
        {
            var s = PlayArea.SpotOf(toy);
            if (s < 0) return null;
            var c = children[s];
            return c != null && c.Toy == toy ? c : null;
        }

        public Transform Root => root;
        public Transform Top => top;
        public Item TidyButtonItem { get; private set; }
        public float Height { get; private set; }

        public PlayTableView(Art art, Transform parent, List<Item> items, Sounds sounds, System.Action tidy)
        {
            this.art = art;
            this.items = items;
            this.sounds = sounds;
            root = art.Group(parent, "Play Area");
            top = art.Group(root, "Table Top");

            // Rug, cushions and a cosy floor.
            art.Part(root, "Rug", art.Cylinder, art.Matte(new Color(1f, 0.82f, 0.6f)), new Vector3(0, 0.003f, 0.2f), new Vector3(1.6f, 0.003f, 1.1f), null, false);
            art.Part(root, "Rug Ring", art.Cylinder, art.Matte(new Color(0.95f, 0.6f, 0.7f)), new Vector3(0, 0.005f, 0.2f), new Vector3(1.3f, 0.003f, 0.85f), null, false);
            art.Part(root, "Rug Centre", art.Cylinder, art.Matte(new Color(0.6f, 0.8f, 0.95f)), new Vector3(0, 0.007f, 0.2f), new Vector3(1.0f, 0.003f, 0.6f), null, false);
            var cushions = new[] { (new Vector3(-0.75f, 0.08f, 0.55f), new Color(1f, 0.75f, 0.4f)), (new Vector3(0.75f, 0.08f, 0.6f), new Color(0.7f, 0.85f, 0.6f)) };
            foreach (var (at, c) in cushions) art.Part(root, "Cushion", art.Sphere, art.Matte(c), at, new Vector3(0.38f, 0.16f, 0.32f));

            // The table: soft yellow top with rounded corners and a low, soft pink rim all round, short legs.
            var soft = art.Matte(new Color(1f, 0.93f, 0.7f));
            const float corner = 0.06f;
            art.Part(top, "Top", art.Cube, soft, new Vector3(0, -TableSize.y / 2, 0), new Vector3(TableSize.x - 2 * corner, TableSize.y, TableSize.z));
            art.Part(top, "Top", art.Cube, soft, new Vector3(0, -TableSize.y / 2, 0), new Vector3(TableSize.x, TableSize.y, TableSize.z - 2 * corner));
            var rimMat = art.Matte(new Color(1f, 0.6f, 0.7f));
            foreach (var x in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
            {
                var c = new Vector3(x * (TableSize.x / 2 - corner), 0, z * (TableSize.z / 2 - corner));
                art.Part(top, "Corner", art.Cylinder, soft, c + Vector3.down * TableSize.y / 2, new Vector3(corner * 2, TableSize.y / 2, corner * 2));
                art.Part(top, "Rim Corner", art.Cylinder, rimMat, c + new Vector3(x * (corner - 0.01f) * 0.7f, RimHeight / 2, z * (corner - 0.01f) * 0.7f), new Vector3(0.024f, RimHeight / 2, 0.024f), null, false);
            }
            foreach (var z in new[] { -1f, 1f })
                art.Part(top, "Rim", art.Cube, rimMat, new Vector3(0, RimHeight / 2, z * (TableSize.z / 2 - 0.01f)), new Vector3(TableSize.x - 2 * corner, RimHeight, 0.02f), null, false);
            foreach (var x in new[] { -1f, 1f })
                art.Part(top, "Rim", art.Cube, rimMat, new Vector3(x * (TableSize.x / 2 - 0.01f), RimHeight / 2, 0), new Vector3(0.02f, RimHeight, TableSize.z - 2 * corner), null, false);
            for (var i = 0; i < 4; i++) legs[i] = art.Part(root, "Leg", art.Cylinder, art.Matte(new Color(0.85f, 0.7f, 0.5f)), Vector3.zero, Vector3.one);

            // Painted road (dashed oval) and the ball corner and building corner markings.
            for (var i = 0; i < 24; i++)
            {
                var t = i / 24f;
                var p = RoadPoint(t);
                var next = RoadPoint(t + 1f / 48);
                art.Part(top, "Road", art.Cube, art.Matte(new Color(0.45f, 0.45f, 0.5f)), new Vector3(p.x, 0.001f, p.z), new Vector3(0.055f, 0.002f, 0.035f),
                    Quaternion.LookRotation(next - p), false);
                if (i % 2 == 0)
                    art.Part(top, "Dash", art.Cube, art.Unlit(Color.white), new Vector3(p.x, 0.0025f, p.z), new Vector3(0.006f, 0.001f, 0.018f), Quaternion.LookRotation(next - p), false);
            }
            art.Part(top, "Ball Corner", art.Cylinder, art.Matte(new Color(0.98f, 0.8f, 0.8f)), new Vector3(BallHome.x, 0.001f, BallHome.z), new Vector3(0.12f, 0.001f, 0.12f), null, false);
            art.Part(top, "Building Corner", art.Cube, art.Matte(new Color(0.8f, 0.9f, 1f)), new Vector3(BuildSpot.x, 0.001f, BuildSpot.z),
                new Vector3(BuildSite.Columns * Models.BlockSize + 0.02f, 0.001f, Models.BlockSize + 0.02f), null, false);

            // Colliders: table top, rim, and the floor of the play area (toys only; nothing else has colliders).
            AddBox(top, new Vector3(0, -TableSize.y / 2, 0), TableSize);
            foreach (var z in new[] { -1f, 1f }) AddBox(top, new Vector3(0, RimHeight / 2, z * (TableSize.z / 2 - 0.01f)), new Vector3(TableSize.x, RimHeight * 2, 0.02f));
            foreach (var x in new[] { -1f, 1f }) AddBox(top, new Vector3(x * (TableSize.x / 2 - 0.01f), RimHeight / 2, 0), new Vector3(0.02f, RimHeight * 2, TableSize.z));
            AddBox(root, new Vector3(0, -0.05f, 0), new Vector3(4f, 0.1f, 4f));

            // The crocodile's shelf (EPIC-006), with a little bracket under it.
            art.Part(top, "Croc Shelf", art.Cube, soft, ShelfCentre + Vector3.down * ShelfSize.y / 2, ShelfSize);
            art.Part(top, "Croc Shelf Bracket", art.Cube, art.Matte(new Color(0.55f, 0.4f, 0.28f)), new Vector3(-0.53f, -0.06f, ShelfCentre.z), new Vector3(0.04f, 0.09f, 0.14f));

            // Big picture button: Städa lekytan (a table with a round arrow).
            var button = art.Group(top, "Tidy Button", TidyButton);
            art.Part(button, "Disc", art.Cylinder, art.Unlit(new Color(0.3f, 0.7f, 0.45f)), Vector3.zero, new Vector3(0.09f, 0.008f, 0.09f), null, false);
            for (var i = 0; i < 9; i++)
            {
                var a = i / 12f * Mathf.PI * 2;
                art.Part(button, "Arrow", art.Sphere, art.Unlit(Color.white), new Vector3(Mathf.Cos(a) * 0.028f, 0.009f, Mathf.Sin(a) * 0.028f), Vector3.one * 0.011f, null, false);
            }
            art.Part(button, "Arrow Head", art.Cube, art.Unlit(Color.white), new Vector3(0.028f, 0.009f, -0.012f), new Vector3(0.018f, 0.004f, 0.018f), Quaternion.Euler(0, 45, 0), false);
            TidyButtonItem = Item.ForButton(button, 0.05f, tidy);
            items.Add(TidyButtonItem);

            SetHeight(StandView.DefaultHeight);
        }

        static void AddBox(Transform parent, Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        public void PlaceLocal(Vector3 point, float yaw)
        {
            root.localPosition = new Vector3(point.x, 0, point.z);
            root.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        public void SetHeight(float height)
        {
            Height = StandView.Clamp(height);
            top.localPosition = new Vector3(0, Height, 0);
            var x = TableSize.x / 2 - 0.12f;
            var z = TableSize.z / 2 - 0.1f;
            var corners = new[] { new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z) };
            for (var i = 0; i < 4; i++)
            {
                legs[i].localPosition = corners[i] + Vector3.up * (Height / 2);
                legs[i].localScale = new Vector3(0.05f, Height / 2, 0.05f);
            }
        }

        // EPIC-008: the children at the table wave happily at the market party.
        public void Cheer(float clock)
        {
            for (var i = 0; i < children.Length; i++)
            {
                var c = children[i];
                if (c?.Figure == null) continue;
                c.Figure.SetMood(Mood.Happy);
                c.Figure.Wave(Mathf.Sin(clock * 9 + i) * 30);
            }
        }

        public Vector3 ToTable(Vector3 world) => top.InverseTransformPoint(world);
        public Vector3 ToWorld(Vector3 table) => top.TransformPoint(table);

        public int ToyCount(Ware toy) => ChildOf(toy)?.Toys.Count ?? 0;
        public bool ChildPresent(Ware toy) => ChildOf(toy) != null;
        public Item CarriedToy(Ware toy) => ChildOf(toy)?.Carried;
        public Vector3 ChildWorld(Ware toy) => ChildOf(toy)?.Root.position ?? Vector3.zero;
        public int PhysicsBodies
        {
            get
            {
                var n = 0;
                foreach (var c in children) if (c != null) foreach (var t in c.Toys) if (t.Body != null) n++;
                return n;
            }
        }

        public IEnumerable<Item> Toys(Ware toy)
        {
            var c = ChildOf(toy);
            if (c == null) yield break;
            foreach (var t in c.Toys) yield return t;
        }

        // EPIC-017: tossed balls the child has caught (since the table was built).
        public int BallCatches { get; private set; }

        // EPIC-016: where the children are (world), walking to the table, playing or leaving: the figures on the paths
        // keep their distance to them as to someone standing (the children's walk is timed by Core.PlayArea).
        public void AddChildren(List<Walker> into, System.Func<Vector3, Vector3> toMarket)
        {
            foreach (var c in children)
                if (c != null)
                {
                    var at = toMarket(c.Root.position);
                    at.y = 0;
                    into.Add(Walker.Standing(at));
                }
        }

        // The child who just paid takes the very toy from the basket and starts walking from the toy stall.
        public void StartCarry(Ware toy, Item bought, Vector3 fromWorld, int variant)
        {
            var s = PlayArea.SpotOf(toy);
            RemoveChild(s);
            var c = new Child { Toy = toy, Variant = variant };
            c.Root = art.Group(root, "Child " + WareInfo.Name(toy));
            c.From = root.InverseTransformPoint(fromWorld);
            c.From.y = 0;
            c.Root.localPosition = c.From;
            var figureRoot = art.Group(c.Root, "Figure");
            figureRoot.localScale = Vector3.one * CustomerView.ChildScale;
            c.Figure = new CustomerFigure(art, figureRoot, variant);
            c.FigureRoot = figureRoot;
            // A soft stool at the play spot, so a small child sits at table height (shown once she's there).
            c.Stool = art.Part(root, "Stool", art.Cylinder, art.Matte(new Color(0.75f, 0.85f, 1f)), ChildSpot(toy), Vector3.one);
            c.Stool.gameObject.SetActive(false);
            c.Carry = art.Group(c.Root, "Carry", new Vector3(0, 0.5f, -0.14f));
            c.Play = ToyPlay.For(this, c);
            if (bought != null)
            {
                bought.Place = ItemPlace.Carried;
                bought.Grabbable = false;
                bought.StopTween();
                bought.Transform.SetParent(c.Carry, false);
                bought.Transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                bought.Transform.localScale = Vector3.one;
                c.Carried = bought;
            }
            children[s] = c;
        }

        // Every frame: physics, then the children's play, following Core.PlayArea.
        public void Step(PlayArea area, float dt, float clock)
        {
            Simulate(dt);
            for (var s = 0; s < PlayArea.Spots; s++)
            {
                var c = children[s];
                if (c == null) continue;
                var toy = c.Toy;
                var st = area.State(toy);
                if (st == SpotState.Free)
                {
                    // EPIC-015: a child who walked away after playing strolls on in the park (never after tidying)
                    if (c.WentHome) ChildWentHome?.Invoke(c.Variant, c.Root.position);
                    RemoveChild(s);
                    continue;
                }
                var spot = ChildSpot(toy);
                switch (st)
                {
                    case SpotState.Walking:
                        var k = Mathf.Clamp01(area.StateTime(toy) / PlayArea.WalkSeconds);
                        var e = k * k * (3 - 2 * k);
                        var here = WalkPoint(c.From, spot, e);
                        Face(c, WalkPoint(c.From, spot, Mathf.Min(1, e + 0.05f)) - here);
                        c.Root.localPosition = here + Vector3.up * Mathf.Abs(Mathf.Sin(clock * 9)) * 0.02f;
                        c.Figure.SetMood(Mood.Happy);
                        break;
                    case SpotState.Playing:
                        if (!c.Placed)
                        {
                            c.Placed = true;
                            c.Play.PutOnTable();
                        }
                        var lift = SeatLift;
                        c.Stool.gameObject.SetActive(lift > 0.02f);
                        c.Stool.localPosition = spot + Vector3.up * (lift / 2);
                        c.Stool.localScale = new Vector3(0.3f, Mathf.Max(0.01f, lift / 2), 0.3f);
                        c.Root.localPosition = spot + Vector3.up * (lift + Mathf.Sin(clock * 2) * 0.004f);
                        Face(c, Vector3.back);
                        c.Figure.SetMood(area.Control(toy) == ToyControl.Player ? Mood.Neutral : Mood.Happy); // waits kindly
                        c.Play.Tick(area, dt);
                        Play(area, toy, c, dt);
                        break;
                    case SpotState.Leaving:
                        if (area.Control(toy) == ToyControl.Player) break; // never take a toy out of the player's hand
                        c.Stool.gameObject.SetActive(false);
                        foreach (var t in c.Toys) if (t.Place != ItemPlace.Held) t.Transform.gameObject.SetActive(false);
                        c.Play.Leaving();
                        var l = Mathf.Clamp01(area.StateTime(toy) / PlayArea.LeaveSeconds);
                        c.WentHome = l > 0.5f; // the rules may free the spot in the same tick the walk ends
                        c.Root.localPosition = spot + new Vector3(0, 0, l * LeaveDistance);
                        Face(c, Vector3.forward);
                        c.Figure.Wave(Mathf.Sin(clock * 10) * 25);
                        break;
                }
                foreach (var t in c.Toys) LostCheck(area, t, dt);
            }
        }

        // How high a child sits (on a stool) so her shoulders are a little above the table top.
        float SeatLift => Mathf.Max(0, Height + 0.15f - 0.72f * CustomerView.ChildScale);

        static void Face(Child c, Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude > 1e-4f) c.Root.localRotation = Quaternion.LookRotation(-dir); // the figure's face is -z
        }

        // EPIC-011: an overlap is pushed apart gently, and nothing spins wildly.
        public const float MaxDepenetration = 0.3f;
        public const float MaxSpin = 20f;

        static void Limit(Rigidbody rb)
        {
            rb.maxDepenetrationVelocity = MaxDepenetration;
            rb.maxAngularVelocity = MaxSpin;
        }

        static void AddBall(Item t)
        {
            var col = t.Transform.gameObject.AddComponent<SphereCollider>();
            col.radius = Models.BallRadius;
            col.material = new PhysicsMaterial("Ball") { bounciness = 0.3f, dynamicFriction = 0.3f, staticFriction = 0.4f };
            var rb = t.Transform.gameObject.AddComponent<Rigidbody>();
            Limit(rb);
            rb.mass = 0.1f;
            rb.linearDamping = 0.35f;
            rb.angularDamping = 0.6f;
            t.Body = rb;
        }

        static void AddBody(Item t, Vector3 size, float mass, float friction, float damping, bool freezeTilt = false)
        {
            var col = t.Transform.gameObject.AddComponent<BoxCollider>();
            col.size = size;
            col.material = new PhysicsMaterial("Toy")
            {
                bounciness = 0.05f, dynamicFriction = friction, staticFriction = friction + 0.1f,
                frictionCombine = freezeTilt ? PhysicsMaterialCombine.Minimum : PhysicsMaterialCombine.Average, // the car rolls on its wheels
            };
            var rb = t.Transform.gameObject.AddComponent<Rigidbody>();
            Limit(rb);
            rb.mass = mass;
            rb.linearDamping = damping;
            rb.angularDamping = 1f;
            if (freezeTilt) rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            t.Body = rb;
        }

        // ---- The children's play (only while Core says the child has the toy) ----

        void Play(PlayArea area, Ware toy, Child c, float dt)
        {
            var mine = area.ChildMayAct(toy);
            var allStill = true;
            foreach (var t in c.Toys)
            {
                if (t.Place == ItemPlace.Returning) { mine = false; continue; }
                // A piece fixed in a tower or on the peg (kinematic) counts as lying still (EPIC-010: before, a stack
                // with fixed pieces never handed the toy back to the child).
                if (t.Place == ItemPlace.Held || (t.Body != null && !t.Body.isKinematic && t.Body.linearVelocity.magnitude >= RestSpeed)) allStill = false;
            }
            if (area.Control(toy) == ToyControl.Free)
                c.Rest = allStill ? c.Rest + dt : 0; // released by the player: free physics until everything lies still
            if (area.Control(toy) == ToyControl.Free && c.Rest > 0.5f)
            {
                c.Rest = 0;
                area.ToyAtRest(toy);
                c.Play.AtRest();
                mine = area.ChildMayAct(toy);
            }
            if (mine) c.Play.Play(dt);
        }

        bool OnTable(Item toy)
        {
            var local = top.InverseTransformPoint(toy.Position);
            return local.y > -0.02f && Mathf.Abs(local.x) < TableSize.x / 2 && Mathf.Abs(local.z) < TableSize.z / 2;
        }

        // EPIC-011: one timer per piece (a shared one was reset every frame by the tower's fixed blocks or a held piece,
        // so a block on the floor never came back).
        void LostCheck(PlayArea area, Item t, float dt)
        {
            if (t.Place != ItemPlace.Play || t.Body == null || t.Body.isKinematic) { t.LostTime = 0; return; }
            t.LostTime = top.InverseTransformPoint(t.Position).y < -LostBelow ? t.LostTime + dt : 0;
            if (t.LostTime < LostSeconds) return;
            t.LostTime = 0;
            Reset(area, t); // back to its place, free
        }

        // ---- The player's hands ----

        public void PlayerTook(PlayArea area, Item toy)
        {
            toy.LastValid = top.InverseTransformPoint(toy.Position); // EPIC-011: where it can go back to
            area.PlayerTook(toy.Ware);
            var c = ChildOf(toy.Ware);
            toy.StopTween();
            toy.OnArrive = null;
            if (toy.Body != null)
            {
                toy.Body.isKinematic = true;
                toy.Body.detectCollisions = false; // EPIC-011: a toy in the hand never shoves the others
            }
            c?.Play.Taken(toy); // the child waits, a top stops, a stack loses what stood on it
        }

        public void PlayerReleased(PlayArea area, Item toy, Vector3 handVelocity)
        {
            toy.Place = ItemPlace.Play;
            if (toy.Body != null) toy.Body.detectCollisions = true;
            var play = ChildOf(toy.Ware)?.Play;
            if (play != null && play.ReleasedInstead(toy)) // EPIC-018: frogs have no physics
            {
                area.PlayerReleased(toy.Ware);
                return;
            }
            if (OnRoad(toy.Ware) || toy.Ware == Ware.Top) toy.Transform.rotation = Quaternion.Euler(0, toy.Transform.eulerAngles.y, 0); // lands upright
            var velocity = Vector3.ClampMagnitude(handVelocity, MaxThrowSpeed);
            var placed = play != null && play.Place(toy, ref velocity); // blocks: the build or a free place
            if (!placed && toy.Slot < 0) LiftOntoTable(toy); // EPIC-011: released partly inside the table
            play?.AfterPlace(toy, handVelocity, ref velocity); // a toss, a ring onto the peg
            if (toy.Body != null)
            {
                toy.Body.isKinematic = toy.Slot >= 0;
                if (!toy.Body.isKinematic)
                {
                    toy.Body.linearVelocity = velocity;
                    toy.Body.angularVelocity = Vector3.zero;
                }
            }
            area.PlayerReleased(toy.Ware);
        }

        public static readonly Vector2 TableHalf = new(TableSize.x / 2, TableSize.z / 2);

        // EPIC-011: a toy let go above the table but partly inside its top or rim is set on the table at that spot.
        // Clearly off the table it may fall (the lost-toy rule brings it back).
        void LiftOntoTable(Item toy)
        {
            if (!toy.Transform.TryGetComponent<Collider>(out var col)) return;
            var local = top.InverseTransformPoint(toy.Position);
            if (Mathf.Abs(local.x) > TableHalf.x + 0.03f || Mathf.Abs(local.z) > TableHalf.y + 0.03f || local.y > 0.5f) return;
            var half = col.bounds.extents;
            var mx = TableHalf.x - BlockPlacement.Rim - Mathf.Max(half.x, half.z);
            var mz = TableHalf.y - BlockPlacement.Rim - Mathf.Max(half.x, half.z);
            var p = new Vector3(Mathf.Clamp(local.x, -mx, mx), Mathf.Max(local.y, half.y + 0.002f), Mathf.Clamp(local.z, -mz, mz));
            if (p != local) toy.Transform.localPosition = p;
        }

        // Back to its place on the table, lying still (lost, tidy, focus lost). Never touches a toy in a hand.
        public void Reset(PlayArea area, Item toy)
        {
            if (toy.Place == ItemPlace.Held) return;
            var play = ChildOf(toy.Ware)?.Play;
            if (play != null && play.ResetInstead(area, toy)) // EPIC-018: a frog goes home its own way
            {
                area.PlayerReleased(toy.Ware);
                return;
            }
            toy.Place = ItemPlace.Returning;
            toy.Grabbable = false;
            if (toy.Body != null)
            {
                toy.Body.isKinematic = true;
                toy.Body.detectCollisions = true; // (it may come from a hand: focus lost)
            }
            play?.Unstack(toy);
            toy.Slot = -1;
            play?.Returning(toy);
            var home = Home(toy.Ware, toy.Index);
            if (play != null) home = play.HomeFor(toy, home);
            toy.MoveTo(home, OnRoad(toy.Ware) ? RoadRotation(0.75f) : Quaternion.identity, 0.5f);
            toy.OnArrive = () =>
            {
                toy.Place = ItemPlace.Play;
                if (toy.Body != null)
                {
                    toy.Body.isKinematic = false;
                    toy.Body.linearVelocity = Vector3.zero;
                    toy.Body.angularVelocity = Vector3.zero;
                }
                play?.HomeArrived();
                if (area.Control(toy.Ware) == ToyControl.Free) area.ToyAtRest(toy.Ware);
            };
            area.PlayerReleased(toy.Ware);
        }

        // Städa lekytan: every toy that isn't in a hand back to its place. Money and play time are untouched.
        public void Tidy(PlayArea area)
        {
            foreach (var c in children)
                if (c != null)
                {
                    foreach (var t in c.Toys)
                        if (t.Place != ItemPlace.Held) Reset(area, t);
                    c.Play.Tidied();
                }
        }

        public bool AnyToyHeld(Ware toy)
        {
            var c = ChildOf(toy);
            if (c == null) return false;
            foreach (var t in c.Toys) if (t.Place == ItemPlace.Held) return true;
            return false;
        }

        void RemoveChild(int s)
        {
            var c = children[s];
            if (c == null) return;
            foreach (var t in c.Toys) if (t.Place != ItemPlace.Held) Remove(t);
            if (c.Carried != null) Remove(c.Carried);
            c.Play.Removed();
            Art.Discard(c.Stool);
            Art.Discard(c.Root);
            children[s] = null;
        }

        void Remove(Item t)
        {
            items.Remove(t);
            t.Place = ItemPlace.Gone;
            if (t.Transform != null) Art.Discard(t.Transform);
        }

        public void Clear()
        {
            for (var s = 0; s < PlayArea.Spots; s++) RemoveChild(s);
        }

        // Physics is stepped here, from the game's Step (script mode), in sub-steps of at most 1/72 s.
        void Simulate(float dt)
        {
            accumulator = Mathf.Min(accumulator + dt, 0.1f);
            const float step = 1f / 72;
            while (accumulator >= step)
            {
                accumulator -= step;
                Physics.Simulate(step);
                // Cars and trains roll along their length only: no sliding sideways.
                foreach (var c in children)
                    if (c != null)
                        foreach (var t in c.Toys)
                            if (OnRoad(t.Ware) && t.Body != null && !t.Body.isKinematic)
                            {
                                var v = t.Body.linearVelocity;
                                var side = t.Transform.right;
                                t.Body.linearVelocity = v - side * Vector3.Dot(v, side);
                            }
            }
        }
    }
}
