// Off-headset renders for checking the look ([R] in docs/EPIC-001.md). Explicit: needs a graphics device, so it
// is not part of the normal (-nographics) test run. Writes BMP files to Logs/shots/ (the project has no
// imageconversion module, so no EncodeToPNG); convert with `sips -s format png`.
// Run: Unity -batchmode -projectPath UnityProject -runTests -testPlatform PlayMode -testFilter Snapshots
using System.Collections;
using System.IO;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fruktstand.Tests
{
    [Explicit, Category("Snapshots")]
    public class Snapshots
    {
        FruitStand s;

        // A filter that happens to match a snapshot name (TEST_FILTER=Park) would otherwise render without a graphics
        // device in the -nographics test run and crash Unity (seen 2026-11-02).
        [SetUp]
        public void NeedsGraphics()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: scripts/quest.sh snapshots");
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator Snapshots_RenderStates()
        {
            yield return SceneLoad.Fresh(x => s = x); // synchronous load, classic fruit (see SceneLoad)
            s = Object.FindAnyObjectByType<FruitStand>();
            s.ReadDesktopInput = false;
            yield return null;
            Directory.CreateDirectory("Logs/shots");

            Wait(0.6f);
            Shot("01-desktop-arriving", desktop: true);
            Wait(1.2f);
            // seated view, one hand hovering the apple crate
            Hover(s.CrateTop(Ware.Apple));
            Shot("02-seated-first-order");
            s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = Ware.Apple });
            Wait(0.7f);
            Shot("03-seated-coin-on-counter");
            s.RegisterCoin();
            Wait(0.25f);
            Shot("04-seated-thanks");
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + Shop.ArriveSeconds);
            // second customer: one right item and one wrong item in the basket
            var order = s.Shop.Order;
            var right = System.Array.Find(WareInfo.All, p => order.Count(p) > 0);
            var wrong = System.Array.Find(WareInfo.All, p => order.Count(p) == 0);
            s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = right });
            s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = wrong });
            Wait(0.4f);
            Shot("05-seated-second-order-progress");
            Shot("06-desktop-second-order", desktop: true);
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = -0.15f });
            Wait(0.1f);
            Shot("07-seated-low-counter");
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = 0.15f });

            // EPIC-003, compact market: everything round the work area; the player turns and steps.
            s.Step(new ShopFrame { DeltaTime = 0.02f, NewGame = true });
            Overview("08-compact-market-overview");
            Standing("09-standing-facing-fruit", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.forward);
            for (var k = 0; k < 4; k++)
            {
                for (var i = 0; i < 300 && s.Shop.State != SaleState.Ordering; i++) s.Step(new ShopFrame { DeltaTime = 0.1f });
                if (s.Shop.Category == Category.Candy) Standing("10-turned-to-candy", MarketLayout.StandingSpot(Category.Candy), Vector3.right);
                if (s.Shop.Category == Category.Toys) Standing("11-turned-to-toys-child", MarketLayout.StandingSpot(Category.Toys), Vector3.left);
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < s.Shop.Order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                s.RegisterCoin();
                Wait(1.5f);
                if (k == 3) Overview("12-child-walks-round-the-outside");
                Wait(Shop.ThankSeconds);
            }
            Wait(PlayArea.WalkSeconds + 2f);
            Standing("13-turned-round-to-play-table", MarketLayout.PlayStandingSpot, Vector3.back);
            Overview("14-overview-with-child-playing");

            // EPIC-004: the parent orders one apple from the phone, with a photo (a drawn stand-in portrait).
            var raw = new byte[128 * 128 * 4];
            for (var y = 0; y < 128; y++)
                for (var x = 0; x < 128; x++)
                {
                    var i = (y * 128 + x) * 4;
                    float dx = x - 64, dy = y - 60;
                    var face = dx * dx / 1600 + dy * dy / 2300 < 1;
                    var eye = (Mathf.Abs(dx - 15) < 5 || Mathf.Abs(dx + 15) < 5) && Mathf.Abs(dy + 10) < 6;
                    var mouth = Mathf.Abs(dy - 22 + dx * dx / 120) < 3 && Mathf.Abs(dx) < 22;
                    var hair = y < 40 && dx * dx / 2000 + (y - 34) * (y - 34) / 500f < 1;
                    Color32 c = hair ? new Color32(110, 70, 40, 255) : eye || mouth ? new Color32(40, 30, 30, 255)
                        : face ? new Color32(240, 200, 170, 255) : new Color32(120, 180, 230, 255);
                    raw[i] = c.r; raw[i + 1] = c.g; raw[i + 2] = c.b; raw[i + 3] = 255;
                }
            s.RemoteSession.Handle($"{{\"t\":\"photo\",\"photoId\":\"photo-snapshot1\",\"w\":128,\"h\":128,\"rgba\":\"{System.Convert.ToBase64String(raw)}\"}}");
            s.RemoteSession.Handle("{\"t\":\"order\",\"orderId\":\"order-snapshot1\",\"items\":[\"apple\"],\"photoId\":\"photo-snapshot1\"}");
            for (var guard = 0; guard < 6 && !s.Shop.CustomerIsHuman; guard++)
            {
                while (s.Shop.State != SaleState.Ordering) s.Step(new ShopFrame { DeltaTime = 0.1f });
                if (s.Shop.CustomerIsHuman) break;
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < s.Shop.Order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                s.RegisterCoin();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            while (s.Shop.State != SaleState.Ordering) s.Step(new ShopFrame { DeltaTime = 0.1f });
            Standing("15-phone-customer-with-photo", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.forward);

            // EPIC-005 follow-up: the Öppet/Stängt sign hangs from a rod out of the right front post.
            var cam = s.ViewCamera;
            var signAt = s.Stand.OpenSignBoard.position;
            cam.transform.position = signAt + s.Stand.Root.rotation * new Vector3(-0.25f, 0.12f, -0.45f);
            cam.transform.LookAt(signAt + Vector3.up * 0.03f);
            cam.fieldOfView = 50;
            Shot("16-open-sign-on-rod", keep: true);

            // EPIC-006: the crocodile on its shelf at the play table, open, then one safe tooth down, then snapped.
            var croc = s.Crocodile;
            var crocAt = croc.Root.position;
            cam.transform.position = s.ToWorld(MarketLayout.PlayStandingSpot + Vector3.up * (s.CounterHeight + FruitStand.CounterBelowEyes));
            cam.transform.LookAt(crocAt + Vector3.up * 0.05f);
            cam.fieldOfView = 60;
            Shot("17-crocodile-open", keep: true);
            var safeTooth = (croc.Game.TriggerTooth + 2) % CrocodileGame.Teeth;
            croc.Game.Press(0, safeTooth);
            s.Step(new ShopFrame { DeltaTime = 0.02f });
            Shot("18-crocodile-one-tooth-down", keep: true);
            croc.Game.Press(0, croc.Game.TriggerTooth);
            Wait(CrocodileGame.CloseSeconds + 0.05f);
            Shot("19-crocodile-snapped", keep: true);

            // EPIC-007: the calibration preview for the Quest's 1.71 x 1.86 m play area, next to the current marking.
            var cal = new Calibration();
            cal.Start(new Vector2(1.71f, 1.86f), Vector2.zero, 0);
            s.ShowPreview(s.ToWorld(new Vector3(0.3f, 0, -0.2f)), s.MarketRoot.eulerAngles.y + 15, cal.Fit.StallDistance, cal.Fit.Fits);
            s.ShowAvailableArea(s.ToWorld(new Vector3(0.3f, 0, -0.2f)), s.MarketRoot.eulerAngles.y + 15, cal.Size);
            Overview("20-calibration-preview");
            s.HidePreview();

            // EPIC-008: the market party (a developer threshold of 1) and the rest.
            s.Market.Round.SetThreshold(1);
            s.Step(new ShopFrame { DeltaTime = 0.02f, NewGame = true });
            s.Market.Round.SetThreshold(1);
            for (var i = 0; i < 200 && s.Shop.State != SaleState.Ordering; i++) s.Step(new ShopFrame { DeltaTime = 0.1f });
            s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = s.Market.Assortment.FirstFruit });
            Wait(0.6f);
            s.RegisterCoin();
            for (var i = 0; i < 400 && s.Market.Round.State != RoundState.Celebrating; i++) s.Step(new ShopFrame { DeltaTime = 0.05f });
            Wait(3f);
            Overview("21-market-party");
            Standing("22-market-party-from-the-work-area", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.forward);
            for (var i = 0; i < 400 && s.Market.Round.State != RoundState.Resting; i++) s.Step(new ShopFrame { DeltaTime = 0.05f });
            Wait(0.2f);
            Standing("23-rest-break", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.forward);
            Debug.Log($"[Snapshots] order 2 = {order}, written to {Path.GetFullPath("Logs/shots")}");
        }

        // Standing in the work area (eyes 0.40 m above the counters), looking the given way (market space).
        void Standing(string name, Vector3 market, Vector3 look)
        {
            var cam = s.ViewCamera;
            var eye = s.ToWorld(market + Vector3.up * (s.CounterHeight + FruitStand.CounterBelowEyes));
            cam.transform.position = eye;
            cam.transform.LookAt(s.ToWorld(market + look * 0.6f + Vector3.up * (s.CounterHeight + 0.05f)));
            cam.fieldOfView = 95;
            Shot(name, keep: true);
        }

        // Seen from above and behind the play table: the whole U, the work area and the play table.
        void Overview(string name)
        {
            var cam = s.ViewCamera;
            cam.transform.position = s.ToWorld(new Vector3(0, s.CounterHeight + 2.2f, -2.2f));
            cam.transform.LookAt(s.ToWorld(new Vector3(0, 0.2f, 0.1f)));
            cam.fieldOfView = 70;
            Shot(name, keep: true);
        }

        void Wait(float seconds) => SceneSteps.Wait(seconds, 0.05f, dt => SceneSteps.Step(s, null, false, dt));

        void Hover(Item item)
        {
            var at = item.Position + new Vector3(0.02f, 0.05f, -0.03f);
            s.Step(new ShopFrame { DeltaTime = 0.02f, Right = new HandFrame { Tracked = true, Position = at, Rotation = Quaternion.Euler(30, -20, 0) } });
        }

        // EPIC-009: the new fruit in the stall (bins, sign, pile) and in the customers' bubbles.
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator Snapshots_NewFruit()
        {
            Directory.CreateDirectory("Logs/shots");
            var sets = new[]
            {
                new[] { Ware.Pear, Ware.Strawberry, Ware.Grapes },
                new[] { Ware.Orange, Ware.Watermelon, Ware.Apple },
            };
            for (var set = 0; set < sets.Length; set++)
            {
                yield return SceneLoad.Fresh(x => s = x, sets[set]);
                s = Object.FindAnyObjectByType<FruitStand>();
                s.ReadDesktopInput = false;
                yield return null;
                var tag = string.Join("-", System.Array.ConvertAll(sets[set], w => w.ToString().ToLowerInvariant()));
                Wait(Shop.ArriveSeconds + 0.3f);
                Shot($"{24 + set * 2}-fruit-{tag}");
                Shot($"{24 + set * 2}-fruit-{tag}-crates", lookAt: new Vector3(0, 0.05f, 0.1f));
                // serve customer 1, then customer 2 wants fruit again (categories take turns): its bubble
                s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = s.Market.Assortment.FirstFruit });
                Wait(0.7f);
                s.RegisterCoin();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds + Shop.ArriveSeconds + 0.3f);
                Shot($"{25 + set * 2}-fruit-{tag}-second-order");
            }
        }

        // EPIC-010: new candy and toys in their stalls and bubbles, and the new toys being played with at the table.
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator Snapshots_CandyAndToys()
        {
            Directory.CreateDirectory("Logs/shots");
            yield return SceneLoad.Fresh(x => s = x, new[] { Ware.GummyBear, Ware.Chocolate, Ware.CandyRing, Ware.Top, Ware.Train, Ware.StackingRings });
            Standing("28-candy-stall", MarketLayout.StandingSpot(Category.Candy), Vector3.right);
            Standing("29-toy-stall", MarketLayout.StandingSpot(Category.Toys), Vector3.left);
            for (var k = 0; k < 12; k++) // customer 1 + rounds of fruit -> candy -> toys: all three toys are sold
            {
                for (var i = 0; i < 300 && s.Shop.State != SaleState.Ordering; i++) s.Step(new ShopFrame { DeltaTime = 0.1f });
                if (k == 1) Standing("30-candy-customer", MarketLayout.StandingSpot(Category.Candy), Vector3.right);
                if (k == 2) Standing("31-toy-customer", MarketLayout.StandingSpot(Category.Toys), Vector3.left);
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < s.Shop.Order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                s.RegisterCoin();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Wait(PlayArea.WalkSeconds + 6f); // the rings are stacked, the top spins, the train drives
            Standing("32-play-table-new-toys", MarketLayout.PlayStandingSpot, Vector3.back);
            LogBudget(); // Quest budget evidence (EPIC-010 §6): the whole market with the new models and three children
        }

        // EPIC-013 park: the same views before and after, with a customer at a counter and children playing.
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator Snapshots_Park()
        {
            Directory.CreateDirectory("Logs/shots");
            yield return SceneLoad.Fresh(x => s = x);
            for (var k = 0; k < 7; k++) // customer 1 + two rounds of fruit -> candy -> toys: two children play
            {
                for (var i = 0; i < 300 && s.Shop.State != SaleState.Ordering; i++) s.Step(new ShopFrame { DeltaTime = 0.1f });
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < s.Shop.Order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                s.RegisterCoin();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            for (var i = 0; i < 300 && s.Shop.State != SaleState.Ordering; i++) s.Step(new ShopFrame { DeltaTime = 0.1f });
            Wait(1f);
            Standing("60-park-player-fruit", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.forward);
            Standing("61-park-player-candy", MarketLayout.StandingSpot(Category.Candy), Vector3.right);
            Standing("62-park-player-toys", MarketLayout.StandingSpot(Category.Toys), Vector3.left);
            Standing("63-park-player-play-table", MarketLayout.PlayStandingSpot, Vector3.back);
            Overview("64-park-overview-close");
            var cam = s.ViewCamera;
            cam.transform.position = s.ToWorld(new Vector3(-4.5f, 4.2f, -6f));
            cam.transform.LookAt(s.ToWorld(new Vector3(0, 0.3f, 0.4f)));
            cam.fieldOfView = 60;
            Shot("65-park-overview-wide", keep: true);
            Debug.Log($"[Snapshots] park: {s.Play.PhysicsBodies} toy bodies, customer {s.Shop.State} at {s.Shop.Category}");
            LogBudget();
            // EPIC-014: the bird calls as WAV files, to listen to off the headset
            var birds = SoundSynth.Birds();
            for (var i = 0; i < birds.Length; i++) WriteWav($"Logs/shots/nature-bird-{i}.wav", birds[i]);
        }

        static void WriteWav(string path, float[] data)
        {
            using var f = new BinaryWriter(File.Create(path));
            f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); f.Write(36 + data.Length * 2);
            f.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); f.Write(16); f.Write((short)1); f.Write((short)1);
            f.Write(SoundSynth.Rate); f.Write(SoundSynth.Rate * 2); f.Write((short)2); f.Write((short)16);
            f.Write(System.Text.Encoding.ASCII.GetBytes("data")); f.Write(data.Length * 2);
            foreach (var x in data) f.Write((short)Mathf.RoundToInt(Mathf.Clamp(x, -1, 1) * 32767));
        }

        // EPIC-015: the routes drawn on the ground for review, a visitor sitting on the bench, customers on the path.
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator Snapshots_ParkRoutes()
        {
            Directory.CreateDirectory("Logs/shots");
            FruitStand.StrollRandom = n => n - 1; // the bench first
            try
            {
                yield return SceneLoad.Fresh(x => s = x);
                var d = MarketLayout.StallDistance;
                var routes = s.Art.Group(s.Park, "Route Marking");
                void Draw(Polyline r, Color c, float lift)
                {
                    for (var i = 1; i < r.Points.Count; i++)
                    {
                        Vector3 a = r.Points[i - 1], b = r.Points[i];
                        var len = Vector3.Distance(a, b);
                        if (len < 1e-3f) continue;
                        s.Art.Part(routes, "Route", s.Art.Cube, s.Art.Unlit(c), (a + b) / 2 + Vector3.up * lift,
                            new Vector3(0.05f, 0.004f, len + 0.05f), Quaternion.LookRotation(b - a), false);
                    }
                }
                var lift0 = 0.006f;
                foreach (var c in WareInfo.Categories)
                {
                    Draw(ParkRoutes.Arrival(c, d), new Color(0.2f, 0.45f, 1f), lift0);
                    Draw(ParkRoutes.Departure(c, d, Stroll.Exit), new Color(0.15f, 0.75f, 0.3f), lift0 + 0.001f);
                    Draw(ParkRoutes.Departure(c, d, Stroll.Detour), new Color(1f, 0.55f, 0.1f), lift0 + 0.002f);
                    Draw(ParkRoutes.Departure(c, d, Stroll.Bench), new Color(0.65f, 0.3f, 0.9f), lift0 + 0.003f);
                }
                foreach (var toy in new[] { Ware.Ball, Ware.Car, Ware.Blocks })
                    Draw(ParkRoutes.FromPlay(ParkRoutes.PlayLeft(toy, d), Stroll.Exit), new Color(1f, 0.4f, 0.7f), lift0 + 0.004f);
                Draw(ParkRoutes.BenchToExit(), new Color(0.65f, 0.3f, 0.9f), lift0 + 0.003f);
                var cam = s.ViewCamera;
                cam.transform.position = s.ToWorld(new Vector3(0, 16f, -7f));
                cam.transform.LookAt(s.ToWorld(new Vector3(0, 0, 0.5f)));
                cam.fieldOfView = 60;
                Shot("70-routes-overview", keep: true);
                routes.gameObject.SetActive(false);

                // customer 1 walking in: seen from the work area, facing the fruit stall
                for (var t = 0f; t < 3; t += 0.05f) s.Step(new ShopFrame { DeltaTime = 0.05f });
                Standing("71-customer-walking-in", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.forward);
                for (var i = 0; i < 800 && s.Shop.State != SaleState.Ordering; i++) s.Step(new ShopFrame { DeltaTime = 0.05f });
                s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = s.Market.Assortment.FirstFruit });
                Wait(0.6f);
                s.RegisterCoin();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 1.2f);
                Standing("72-visitor-leaving-next-coming", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.left);
                for (var i = 0; i < 800 && s.Visitors.SittingCount == 0; i++) s.Step(new ShopFrame { DeltaTime = 0.05f });
                Wait(ParkVisitors.SitDownSeconds + 0.2f);
                cam.transform.position = s.ToWorld(ParkRoutes.BenchStand + ParkRoutes.BenchFront * 1.4f + new Vector3(0.4f, 1.0f, -0.6f));
                cam.transform.LookAt(s.ToWorld(ParkRoutes.BenchSeat));
                cam.fieldOfView = 55;
                Shot("73-visitor-on-the-bench", keep: true);
                Standing("74-bench-from-the-toy-stall", MarketLayout.StandingSpot(Category.Toys), Vector3.left);
                Debug.Log($"[Snapshots] routes: visitors {s.Visitors.Count}, sitting {s.Visitors.SittingCount}, customer {s.Shop.CustomerNumber} {s.Shop.State}");
            }
            finally { FruitStand.StrollRandom = null; }
        }

        // What the scene draws (before dynamic batching): renderers, submeshes, triangles.
        void LogBudget()
        {
            long tris = 0;
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var submeshes = 0;
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue; // text has no MeshFilter
                var mesh = filter.sharedMesh;
                submeshes += mesh.subMeshCount;
                for (var i = 0; i < mesh.subMeshCount; i++) tris += mesh.GetIndexCount(i) / 3;
            }
            Debug.Log($"[Fruktstand] scene budget: {renderers.Length} mesh renderers, {submeshes} submeshes (draws before batching), {tris} triangles, {s.Play.PhysicsBodies} toy bodies");
        }

        // EPIC-016: the small bell under the awning while the customer waits: seated at the fruit stall, and from the work
        // area turned towards the candy stall's customer.
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator Snapshots_WaitingBell()
        {
            Directory.CreateDirectory("Logs/shots");
            ParkRoutes.WalkSpeed = 4;
            try
            {
                yield return SceneLoad.Fresh(x => s = x);
                for (var i = 0; i < 400 && s.Shop.State != SaleState.Ordering; i++) s.Step(new ShopFrame { DeltaTime = 0.05f });
                Assert.IsTrue(s.ActiveStall.WaitingShown, "the bell shows while the customer waits");
                Shot("80-waiting-bell-seated");
                Standing("81-waiting-bell-from-the-work-area", MarketLayout.StandingSpot(Category.FruitVeg), Vector3.forward);
            }
            finally { ParkRoutes.WalkSpeed = ParkRoutes.DefaultWalkSpeed; }
        }

        // EPIC-012: every menu page with its longest texts, and a pointer ray on a button, as the player sees them.
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator Snapshots_MenuPanels()
        {
            Directory.CreateDirectory("Logs/shots");
            yield return SceneLoad.Fresh(x => s = x);
            var panel = Object.FindAnyObjectByType<MenuPanel>(FindObjectsInactive.Include);
            panel.transform.SetParent(null, true); // the XR rig is inactive on the desktop: lift the panel out for the photos
            var cam = s.ViewCamera;
            var head = new GameObject("Snapshot Head").transform;
            head.position = s.Stand.ToWorld(new Vector3(0, FruitStand.CounterBelowEyes, -FruitStand.EyeToCounter));
            head.rotation = Quaternion.LookRotation(s.Stand.Top.forward);
            var n = 40;
            foreach (var (name, menu) in MenuPanelSceneTests.Pages())
            {
                panel.Show(new GameMenu(), head);
                panel.Show(menu, head);
                yield return null;
                cam.transform.SetPositionAndRotation(head.position, Quaternion.LookRotation(panel.transform.position - head.position));
                cam.fieldOfView = 60;
                if (name == "main-None")
                {
                    // a right-hand ray resting on "Ta vilopaus"
                    var b = panel.Layout.Boxes.First(x => x.Kind == RowKind.Button && x.Lines[0] == "Ta vilopaus");
                    menu.Hover(b.Item);
                    panel.Show(menu, head);
                    var target = panel.transform.TransformPoint(new Vector3(b.Rect.center.x, b.Rect.center.y, 0));
                    var origin = head.position + head.right * 0.18f + Vector3.down * 0.25f;
                    panel.SetRay(1, true, origin, (target - origin).normalized);
                    yield return null;
                }
                Shot($"{n++}-menu-{name}", keep: true);
                panel.SetRay(1, false, Vector3.zero, Vector3.forward);
            }
            panel.Show(new GameMenu(), head);
            Object.Destroy(head.gameObject);
        }

        void Shot(string name, bool desktop = false, Vector3? lookAt = null, bool keep = false)
        {
            var cam = s.ViewCamera;
            var saved = (cam.transform.position, cam.transform.rotation, cam.fieldOfView);
            if (keep) { }
            else if (!desktop)
            {
                // a seated child's eyes: 40 cm above the counter, 32 cm behind its centre, looking down at it
                var eye = s.Stand.ToWorld(new Vector3(0, FruitStand.CounterBelowEyes, -FruitStand.EyeToCounter));
                cam.transform.position = eye;
                cam.transform.LookAt(s.Stand.ToWorld(lookAt ?? new Vector3(0, 0.12f, 0.25f)));
                cam.fieldOfView = 90;
            }
            const int w = 1280, h = 960;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            WriteBmp($"Logs/shots/{name}.bmp", tex.GetPixels32(), w, h);
            Object.Destroy(rt);
            Object.Destroy(tex);
            (cam.transform.position, cam.transform.rotation, cam.fieldOfView) = (saved.Item1, saved.Item2, saved.Item3);
        }

        static void WriteBmp(string path, Color32[] px, int w, int h)
        {
            var rowSize = (w * 3 + 3) & ~3;
            using var f = new BinaryWriter(File.Create(path));
            f.Write((byte)'B'); f.Write((byte)'M');
            f.Write(54 + rowSize * h); f.Write(0); f.Write(54);
            f.Write(40); f.Write(w); f.Write(h); f.Write((short)1); f.Write((short)24);
            f.Write(0); f.Write(rowSize * h); f.Write(2835); f.Write(2835); f.Write(0); f.Write(0);
            var pad = new byte[rowSize - w * 3];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    f.Write(c.b); f.Write(c.g); f.Write(c.r);
                }
                f.Write(pad);
            }
        }
    }
}
