// EPIC-017/018: play sequences at the play table (ball, blocks, frogs), played with simulated hands, the same steps before and after the
// slice. They log what happens ([seq] lines, also Logs/shots/seq-<toy>.txt) and, with a graphics device, render the
// seated player's view at the moments where play may lose its flow (Logs/shots/seq-*.bmp).
// Explicit: run with `TEST_FILTER=PlaySequences scripts/quest.sh snapshots`.
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    [Explicit, Category("PlaySequences")]
    public class PlaySequences
    {
        const float Dt = 1 / 72f;
        FruitStand s;
        readonly StringBuilder log = new();
        float clock;
        string toyName;

        void Log(string line)
        {
            var l = $"[seq] {toyName} t={clock:F2}s {line}";
            Debug.Log(l);
            log.AppendLine(l);
        }

        void Step(Vector3? right = null, bool grab = false, float dt = Dt)
        {
            SceneSteps.Step(s, right, grab, dt);
            clock += dt;
        }

        void Wait(float seconds) => SceneSteps.Wait(seconds, 0.05f, dt => Step(dt: dt));

        bool WaitUntil(System.Func<bool> condition, float seconds) =>
            SceneSteps.WaitUntil(condition, seconds, dt => Step(dt: dt));

        void SellToyAndWaitForPlay(Ware toy)
        {
            foreach (var t in Assortment.ClassicOf(Category.Toys)) if (PlayArea.SpotOf(t) != PlayArea.SpotOf(toy)) s.PlayArea.TryReserve(t, 99);
            for (var k = 0; k < 10; k++)
            {
                for (var i = 0; i < 600 && s.Shop.State != SaleState.Ordering; i++) Step(dt: 0.1f);
                var order = s.Shop.Order;
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                s.RegisterCoin();
                if (order.Toy == toy) break;
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Assert.IsTrue(WaitUntil(() => s.PlayArea.State(toy) == SpotState.Playing, PlayArea.WalkSeconds + 6), $"the {toy} child plays");
            clock = 0;
        }

        Vector3 Local(Item i) => s.Play.ToTable(i.Position);

        // The seated player's eyes at the play table, looking at the given table point.
        void Shot(string name, Vector3 lookAtTable) => ShotFrom(name, s.Play.ToWorld(new Vector3(0, 0.40f, -0.55f)), s.Play.ToWorld(lookAtTable), 80);

        void ShotFrom(string name, Vector3 eye, Vector3 lookAt, float fov)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var cam = s.ViewCamera;
            var saved = (cam.transform.position, cam.transform.rotation, cam.fieldOfView);
            cam.transform.position = eye;
            cam.transform.LookAt(lookAt);
            cam.fieldOfView = fov;
            const int w = 960, h = 720;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Directory.CreateDirectory("Logs/shots");
            WriteBmp($"Logs/shots/seq-{name}.bmp", tex.GetPixels32(), w, h);
            Object.Destroy(rt);
            Object.Destroy(tex);
            (cam.transform.position, cam.transform.rotation, cam.fieldOfView) = saved;
            Log($"shot {name}");
        }

        static void WriteBmp(string path, Color32[] px, int w, int h)
        {
            using var f = new BinaryWriter(File.Create(path));
            var row = w * 3;
            var size = 54 + row * h;
            f.Write((byte)'B'); f.Write((byte)'M'); f.Write(size); f.Write(0); f.Write(54);
            f.Write(40); f.Write(w); f.Write(h); f.Write((short)1); f.Write((short)24); f.Write(0); f.Write(row * h);
            f.Write(2835); f.Write(2835); f.Write(0); f.Write(0);
            foreach (var p in px) { f.Write(p.b); f.Write(p.g); f.Write(p.r); }
        }

        void Save()
        {
            Directory.CreateDirectory("Logs/shots");
            File.WriteAllText($"Logs/shots/seq-{toyName}.txt", log.ToString());
        }

        // Grab the item with the right hand, from just above it.
        void Take(Item item)
        {
            Step(item.Position);
            Step(item.Position, true);
            Assert.AreSame(item, s.Held(1), $"took {item.Transform.name}");
        }

        // A hand path from the hand's current spot through table-space waypoints (each reached in `seconds`), held.
        Vector3 Move(Vector3 from, Vector3 toTable, float seconds)
        {
            var to = s.Play.ToWorld(toTable);
            var n = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            for (var i = 1; i <= n; i++) Step(Vector3.Lerp(from, to, i / (float)n), true);
            return to;
        }

        // ---------------- Ball ----------------

        // Where the ball is and who has it, once the ball has settled or the time is up.
        string BallState(Item ball) => $"ball at {Local(ball):F2} speed {ball.Body.linearVelocity.magnitude:F2} control {s.PlayArea.Control(Ware.Ball)}";

        // The ball came to rest on the player's half and the player may take it.
        bool AtPlayer(Item ball) => Local(ball).z < 0 && Local(ball).y > -0.02f && ball.Body.linearVelocity.magnitude < 0.05f && !ball.Moving;

        [UnityTest]
        public IEnumerator PlaySequences_Ball()
        {
            toyName = "ball";
            log.Clear();
            yield return SceneLoad.Fresh(x => s = x);
            SellToyAndWaitForPlay(Ware.Ball);
            var ball = s.Play.Toys(Ware.Ball).First();
            Shot("ball-01-child-has-it", new Vector3(-0.25f, 0, 0.1f));
            var ok = WaitUntil(() => AtPlayer(ball), 10);
            Log($"child rolled it to the player: {ok}; {BallState(ball)}");

            // 1. Three lifts and soft tosses towards the child (the seated gesture: lift ~15 cm, then a short push
            //    forward and up, let go while moving).
            for (var pass = 1; pass <= 3; pass++)
            {
                if (!AtPlayer(ball)) WaitUntil(() => AtPlayer(ball), 12);
                var start = clock;
                Take(ball);
                var at = Move(ball.Position, Local(ball) + new Vector3(0, 0.15f, 0), 0.3f);
                var aim = (s.Play.ToTable(s.Play.ChildWorld(Ware.Ball)) - Local(ball));
                aim.y = 0;
                aim = aim.normalized;
                at = Move(at, s.Play.ToTable(at) + aim * 0.06f + Vector3.up * 0.03f, 0.1f);
                Step(at, false);
                Log($"pass {pass}: tossed, {BallState(ball)}");
                var maxY = 0f;
                var shotMid = false;
                for (var t = 0f; t < 1.2f; t += Dt)
                {
                    Step();
                    maxY = Mathf.Max(maxY, Local(ball).y);
                    if (!shotMid && t > 0.2f && pass == 1) { Shot("ball-02-toss-in-flight", new Vector3(-0.25f, 0.1f, 0.1f)); shotMid = true; }
                }
                Log($"pass {pass}: peak {maxY:F2} m over the table, after 1.2 s {BallState(ball)}");
                if (pass == 1) Shot("ball-03-toss-landed", new Vector3(-0.25f, 0, 0.1f));
                var back = WaitUntil(() => AtPlayer(ball) && s.PlayArea.Control(ball.Ware) == ToyControl.Child, 20);
                Log($"pass {pass}: back at the player {back} after {clock - start:F1} s; {BallState(ball)}");
            }

            // 2. A missed toss: off to the side, past the table's end.
            if (!AtPlayer(ball)) WaitUntil(() => AtPlayer(ball), 12);
            var missStart = clock;
            Take(ball);
            var p = Move(ball.Position, Local(ball) + new Vector3(0, 0.15f, 0), 0.3f);
            p = Move(p, s.Play.ToTable(p) + new Vector3(-0.08f, 0.03f, 0.02f), 0.1f);
            Step(p, false);
            Wait(0.8f);
            Log($"missed toss: after 0.8 s {BallState(ball)}");
            Shot("ball-04-missed", new Vector3(-0.35f, -0.2f, 0f));
            var again = WaitUntil(() => AtPlayer(ball), 20);
            Log($"missed toss: back at the player {again} after {clock - missStart:F1} s; {BallState(ball)}");
            Shot("ball-05-back-after-miss", new Vector3(-0.25f, 0, 0f));
            Save();
            yield return null;
        }

        // ---------------- Blocks ----------------

        string Blocks() => string.Join(" ", s.Play.Toys(Ware.Blocks).Select(b => $"#{b.Index}{(b.Slot >= 0 ? $"s{b.Slot}" : "")}{(b.Body.isKinematic ? "k" : "")}@({Local(b).x:F2},{Local(b).y:F2},{Local(b).z:F2})"));

        [UnityTest]
        public IEnumerator PlaySequences_Blocks()
        {
            toyName = "blocks";
            log.Clear();
            yield return SceneLoad.Fresh(x => s = x);
            SellToyAndWaitForPlay(Ware.Blocks);
            var build = new Vector3(0.3f, 0.05f, 0.05f);
            Shot("blocks-01-start", build);
            Wait(2.5f);
            Log($"child's first block: {Blocks()}");
            Shot("blocks-02-child-placed-one", build);

            // The player takes a free block from the player's side and puts it beside the child's first block.
            Item Free() => s.Play.Toys(Ware.Blocks).Where(b => b.Slot < 0 && b.Place == ItemPlace.Play && !b.Moving && Local(b).y < 0.04f)
                .OrderBy(b => Local(b).z).FirstOrDefault();
            Item Placed(int k) => s.Play.Toys(Ware.Blocks).Where(b => b.Slot >= 0).OrderBy(b => b.Slot).ElementAtOrDefault(k);

            var first = Placed(0) ?? s.Play.Toys(Ware.Blocks).OrderByDescending(b => Local(b).z).First();
            var mine = Free();
            Take(mine);
            var hand = Move(mine.Position, Local(first) + new Vector3(-Models.BlockSize - 0.01f, 0.06f, -0.01f), 0.4f); // beside: wider
            Step(hand, false);
            Wait(0.3f);
            Log($"player put one beside: #{mine.Index} at {Local(mine):F3}; {Blocks()}");
            Shot("blocks-03-player-beside", build);
            Wait(3f);
            Log($"3 s later: #{mine.Index} at {Local(mine):F3}; {Blocks()}");

            // The player puts the next one on top of the own block (higher, the player's own shape), let go a little
            // off and partly inside it.
            var mine2 = Free();
            if (mine2 != null)
            {
                Take(mine2);
                hand = Move(mine2.Position, Local(mine) + new Vector3(0.015f, Models.BlockSize * 0.7f, 0.01f), 0.4f);
                Step(hand, false);
                Wait(0.3f);
                Log($"player put one on the own block: #{mine2.Index} at {Local(mine2):F3}; {Blocks()}");
                Shot("blocks-04-player-on-own", build);
            }
            Wait(6f);
            Log($"6 s later (the child went on): {Blocks()}");
            Shot("blocks-05-after-six-seconds", build);
            Wait(10f);
            Log($"10 s more: {Blocks()}");
            Shot("blocks-06-later", build);
            Save();
            yield return null;
        }
        // ---------------- Frogs (EPIC-018) ----------------

        string Frogs() => $"child {s.Play.Frogs.Phase(FrogOwner.Child)} hops {s.Play.Frogs.Hops(FrogOwner.Child)} at {s.Play.Frogs.Position(FrogOwner.Child):F2}; " +
                          $"player {s.Play.Frogs.Phase(FrogOwner.Player)} hops {s.Play.Frogs.Hops(FrogOwner.Player)} at {s.Play.Frogs.Position(FrogOwner.Player):F2} yaw {s.Play.Frogs.Yaw(FrogOwner.Player):F0}; quacks {s.Play.Quacks}";

        // Press a frog's rear with the right hand, hold, lift the hand.
        void PressAndLift(FrogOwner o, float hold, string shotPressed = null)
        {
            var at = s.Play.FrogRear(o).Position;
            Step(at);
            Step(at, true);
            for (var t = 0f; t < hold; t += Dt) Step(at, true);
            if (shotPressed != null) Shot(shotPressed, FrogPond.PadCentre);
            Step(at + Vector3.up * 0.05f, false);
        }

        [UnityTest]
        public IEnumerator PlaySequences_Frogs()
        {
            toyName = "frogs";
            log.Clear();
            yield return SceneLoad.Fresh(x => s = x, FrogSceneTests.FrogToys); // the frogs are on sale from the start
            s.PlayArea.TryReserve(Ware.Car, 99);
            s.PlayArea.TryReserve(Ware.Blocks, 99);
            for (var k = 0; k < 12; k++)
            {
                for (var i = 0; i < 600 && s.Shop.State != SaleState.Ordering; i++) Step(dt: 0.1f);
                if (s.Shop.Order.Toy == Ware.Frogs) break;
                var order = s.Shop.Order;
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                s.RegisterCoin();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Log($"a child orders {s.Shop.Order}");
            var spot = MarketLayout.StandingSpot(Category.Toys);
            ShotFrom("frogs-01-shop-and-order", s.ToWorld(spot + Vector3.up * (s.CounterHeight + FruitStand.CounterBelowEyes)),
                s.ToWorld(spot + Vector3.left * 0.6f + Vector3.up * (s.CounterHeight + 0.05f)), 95);
            s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = Ware.Frogs });
            Wait(0.6f);
            s.RegisterCoin();
            WaitUntil(() => s.PlayArea.State(Ware.Frogs) == SpotState.Playing, PlayArea.WalkSeconds + 6);
            clock = 0;
            Step(dt: 0.05f);
            var pond = s.Play.Frogs;
            Shot("frogs-02-at-the-table", FrogPond.PadCentre);
            WaitUntil(() => pond.Phase(FrogOwner.Child) == FrogPhase.Flying, 6);
            Log($"the child hops (its demo): {Frogs()}");
            for (var t = 0f; t < FrogPond.HopSeconds * 0.45f; t += Dt) Step();
            Shot("frogs-03-child-hops", FrogPond.PadCentre);
            WaitUntil(() => pond.Hops(FrogOwner.Child) == 1 && pond.Phase(FrogOwner.Child) == FrogPhase.Sitting && !s.Play.FrogItem(FrogOwner.Child).Moving
                            && Vector3.Distance(pond.Position(FrogOwner.Child), FrogPond.Home(FrogOwner.Child)) < 0.01f, 5);
            Log($"the child has fetched its frog and waits: {Frogs()}");

            var pressed = clock;
            PressAndLift(FrogOwner.Player, 0.3f, "frogs-04-player-presses");
            Log($"the player pressed and let go: {Frogs()}");
            for (var t = 0f; t < FrogPond.HopSeconds * 0.45f; t += Dt) Step();
            Shot("frogs-05-player-hop", FrogPond.PadCentre);
            WaitUntil(() => pond.Phase(FrogOwner.Player) == FrogPhase.Sitting, 1);
            for (var t = 0f; t < 0.2f; t += Dt) Step();
            Shot("frogs-06-on-the-pad", FrogPond.PadCentre);
            Log($"landed: on the pad {pond.OnPad(FrogOwner.Player)}; {Frogs()}");
            WaitUntil(() => pond.Hops(FrogOwner.Child) == 2, 8);
            Log($"the child's turn came {clock - pressed:F1} s after the player's press: {Frogs()}");

            // Aim: lift the frog, turn the hand to face the table's end, set it down near the edge, hop: a miss.
            WaitUntil(() => pond.Phase(FrogOwner.Player) == FrogPhase.Sitting && !s.Play.FrogItem(FrogOwner.Player).Moving, 2);
            var frog = s.Play.FrogItem(FrogOwner.Player);
            Step(frog.Position);
            Step(frog.Position, true);
            var turned = s.Play.Top.rotation * Quaternion.Euler(0, -90, 0);
            var edge = s.Play.ToWorld(new Vector3(-0.42f, 0.08f, -0.05f));
            var from = frog.Position;
            for (var i = 1; i <= 20; i++)
                s.Step(new ShopFrame { DeltaTime = Dt, Right = new HandFrame { Tracked = true, Position = Vector3.Lerp(from, edge, i / 20f), Rotation = turned, Grab = true } });
            s.Step(new ShopFrame { DeltaTime = Dt, Right = new HandFrame { Tracked = true, Position = edge, Rotation = turned, Grab = false } });
            Wait(0.3f);
            Log($"aimed at the table's end: {Frogs()}");
            var missAt = clock;
            PressAndLift(FrogOwner.Player, 0.2f);
            Wait(0.7f);
            Shot("frogs-07-miss", new Vector3(-0.45f, -0.2f, -0.05f));
            Log($"missed: {Frogs()}");
            WaitUntil(() => pond.Phase(FrogOwner.Player) == FrogPhase.Sitting && !frog.Moving, 5);
            Log($"back home {clock - missAt:F1} s after the press: {Frogs()}");
            Shot("frogs-08-back-home", FrogPond.PadCentre);
            Save();
            yield return null;
        }
    }
}
