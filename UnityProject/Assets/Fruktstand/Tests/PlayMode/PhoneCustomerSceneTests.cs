// EPIC-004 in the real scene: a phone order (fed to RemoteSession as the relay would) becomes a customer at the
// stall, is sold with simulated hands and the usual token/register, and the till moves exactly once.
// RelayChain_EditorEndToEnd is [Explicit]: it needs the real relay running on localhost (scripts/kund.sh spike).
using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class PhoneCustomerSceneTests
    {
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x); // synchronous load, classic fruit (see SceneLoad)
            s = UnityEngine.Object.FindAnyObjectByType<FruitStand>();
            Assert.IsNotNull(s);
            s.ReadDesktopInput = false;
            yield return null;
        }

        void Step(Vector3? right = null, bool grab = false, float dt = 0.02f) => SceneSteps.Step(s, right, grab, dt);

        void Wait(float seconds) => SceneSteps.Wait(seconds, 0.05f, dt => Step(dt: dt));

        void WaitForOrdering()
        {
            var guard = 0;
            while (s.Shop.State != SaleState.Ordering && guard++ < 600) Step(dt: 0.1f);
            Assert.AreEqual(SaleState.Ordering, s.Shop.State);
        }

        void PickAndDrop(Item item, Vector3 to, float settle = 0.5f)
        {
            var from = item.Position;
            Step(from);
            Step(from, true);
            Assert.AreSame(item, s.Held(1));
            Step(Vector3.Lerp(from, to, 0.5f), true);
            Step(to, true);
            Step(to, false);
            Wait(settle);
        }

        // Sells the current order with the hands at the real basket and register.
        void SellWithHands()
        {
            WaitForOrdering();
            var order = s.Shop.Order;
            var stall = s.ActiveStall;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) PickAndDrop(s.CrateTop(w), stall.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f), 0.2f);
            Wait(0.6f);
            PickAndDrop(s.Coin, stall.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f), 0.1f);
        }

        static string PhotoJson(string id, Color32 c)
        {
            var raw = new byte[128 * 128 * 4];
            for (var i = 0; i < raw.Length; i += 4) { raw[i] = c.r; raw[i + 1] = c.g; raw[i + 2] = c.b; raw[i + 3] = 255; }
            return $"{{\"t\":\"photo\",\"photoId\":\"{id}\",\"w\":128,\"h\":128,\"rgba\":\"{Convert.ToBase64String(raw)}\"}}";
        }

        [UnityTest]
        public IEnumerator Scene_PhoneOrderSoldWithHandsOnce()
        {
            // Spike flow steps 4–8, without the network: the phone orders one apple, the customer comes after the NPC.
            WaitForOrdering();
            s.RemoteSession.Handle(PhotoJson("photo-0001abcd", new Color32(30, 200, 90, 255)));
            s.RemoteSession.Handle("{\"t\":\"order\",\"orderId\":\"order-0001abcd\",\"items\":[\"apple\"],\"photoId\":\"photo-0001abcd\"}");
            Assert.IsFalse(s.Shop.CustomerIsHuman, "the NPC already here is served first");
            SellWithHands();
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 0.2f);

            WaitForOrdering();
            Assert.IsTrue(s.Shop.CustomerIsHuman);
            Assert.AreEqual(Order.Of(Ware.Apple), s.Shop.Order);
            Assert.AreSame(s.Stall(Category.FruitVeg).Root, s.Customer.Stall, "at the fruit stall");
            Assert.IsTrue(s.Customer.Portrait.Visible, "the phone photo on the customer");
            Assert.AreSame(s.Photos.Get("photo-0001abcd"), s.Customer.Portrait.Shown);
            Assert.AreEqual(RemoteState.Serving, s.Market.Remote.Status("order-0001abcd").State);

            var till = s.Wallet.Balance;
            var sales = s.SalesRegistered;
            SellWithHands();
            Assert.AreEqual(till + 1, s.Wallet.Balance, "the till moves exactly once");
            Assert.AreEqual(sales + 1, s.SalesRegistered);
            Assert.IsFalse(s.RegisterCoin(), "a repeated register event");
            s.RemoteSession.Handle("{\"t\":\"order\",\"orderId\":\"order-0001abcd\",\"items\":[\"apple\"]}"); // resent
            Assert.AreEqual(till + 1, s.Wallet.Balance);
            Assert.AreEqual(RemoteState.Done, s.Market.Remote.Status("order-0001abcd").State, "the phone hears Tack, köpet är klart");

            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 0.2f);
            WaitForOrdering();
            Assert.IsFalse(s.Shop.CustomerIsHuman, "the game goes on with NPC customers");
            Assert.IsFalse(s.Customer.Portrait.Visible);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PhoneOrderWithoutPhotoUsesTheUsualFigure()
        {
            s.RemoteSession.Handle("{\"t\":\"order\",\"orderId\":\"order-0002abcd\",\"items\":[\"lollipop\"]}");
            SellWithHands();
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 0.2f);
            WaitForOrdering();
            Assert.IsTrue(s.Shop.CustomerIsHuman);
            Assert.AreSame(s.Stall(Category.Candy).Root, s.Customer.Stall);
            Assert.IsFalse(s.Customer.Portrait.Visible, "no photo: the default figure");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_NewGameDuringPhoneOrder()
        {
            s.RemoteSession.Handle("{\"t\":\"order\",\"orderId\":\"order-0003abcd\",\"items\":[\"banana\"]}");
            SellWithHands();
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 0.2f);
            Assert.IsTrue(s.Shop.CustomerIsHuman);
            s.NewGame();
            Assert.AreEqual(RemoteState.Cancelled, s.Market.Remote.Status("order-0003abcd").State);
            Assert.AreEqual(0, s.Wallet.Balance);
            Assert.IsFalse(s.Shop.CustomerIsHuman);
            yield return null;
        }

        // ---------- Editor chain with the real relay ----------

        sealed class FakePhone : IDisposable
        {
            readonly ClientWebSocket ws = new();
            readonly StringBuilder log = new();
            Task reader;
            public string Seen { get { lock (log) return log.ToString(); } }

            public async Task Open(string url)
            {
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                reader = Read();
            }

            async Task Read()
            {
                var buf = new byte[256 * 1024];
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    lock (log) log.Append(Encoding.UTF8.GetString(buf, 0, r.Count)).Append('\n');
                }
            }

            public Task Send(string json) => ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);

            public void Dispose()
            {
                try { ws.Abort(); } catch (Exception) { }
                ws.Dispose();
            }
        }

        static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(condition(), what);
        }

        static System.Diagnostics.Process StartRelay(int port)
        {
            var node = Environment.GetEnvironmentVariable("NODE") ?? "/opt/homebrew/bin/node";
            var server = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../relay/server.mjs"));
            return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(node, $"\"{server}\" --port {port} --code 4821 --no-beacon --quiet")
            {
                UseShellExecute = false, CreateNoWindow = true,
            });
        }

        // H5 in the real chain: the relay disappears in the middle of an accepted order and comes back. The Quest keeps
        // the order, reconnects by itself, the phone asks for the status, and nothing is ordered or paid twice.
        [UnityTest, Explicit("starts its own relay with node (scripts/kund.sh spike runs it)"), Category("Relay")]
        public IEnumerator RelayChain_RelayRestartKeepsOrder()
        {
            const int port = 8799;
            var relay = StartRelay(port);
            try
            {
                RelayLink.OverrideAddress = $"ws://127.0.0.1:{port}/ws";
                var link = s.StartRelayLink();
                yield return Until(() => link.State == LinkState.Connected, 15, "connected");
                var phone = new FakePhone();
                var open = phone.Open($"ws://127.0.0.1:{port}/ws?role=phone&code=4821&client=phone-unity02");
                yield return Until(() => open.IsCompleted, 5, "phone connected");
                yield return Until(() => phone.Seen.Contains("online\":true") || phone.Seen.Contains("questOnline\":true"), 5, "phone sees the Quest");
                phone.Send("{\"t\":\"order\",\"orderId\":\"order-unity002\",\"items\":[\"carrot\"]}");
                yield return Until(() => phone.Seen.Contains("\"state\":\"waiting\""), 5, "accepted");

                relay.Kill();
                relay.WaitForExit(3000);
                phone.Dispose();
                yield return Until(() => link.State != LinkState.Connected, 20, "the Quest notices the relay is gone");
                SellWithHands(); // the game goes on without the relay: the NPC, then the accepted phone order comes up
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 0.2f);
                Assert.IsTrue(s.Shop.CustomerIsHuman, "the accepted order is kept while disconnected");

                relay = StartRelay(port);
                yield return Until(() => link.State == LinkState.Connected, 20, "the Quest reconnects by itself");
                var phone2 = new FakePhone();
                var open2 = phone2.Open($"ws://127.0.0.1:{port}/ws?role=phone&code=4821&client=phone-unity02");
                yield return Until(() => open2.IsCompleted, 5, "phone reconnected");
                yield return Until(() => phone2.Seen.Contains("questOnline\":true"), 5, "phone sees the Quest again");
                phone2.Send("{\"t\":\"sync\",\"orderId\":\"order-unity002\"}");
                phone2.Send("{\"t\":\"order\",\"orderId\":\"order-unity002\",\"items\":[\"carrot\"]}"); // a resend as well
                yield return Until(() => phone2.Seen.Contains("\"state\":\"serving\""), 5, "the phone gets the current status from the Quest");

                var till = s.Wallet.Balance;
                SellWithHands();
                yield return Until(() => phone2.Seen.Contains("\"state\":\"done\""), 5, "köpet är klart");
                Assert.AreEqual(till + 1, s.Wallet.Balance);
                Assert.AreEqual(1, s.Market.Remote.CompletedOrders, "one order, one payment");
                phone2.Dispose();
                link.End();
            }
            finally
            {
                RelayLink.OverrideAddress = null;
                if (!relay.HasExited) relay.Kill();
            }
        }

        [UnityTest, Explicit("needs the relay: scripts/kund.sh spike"), Category("Relay")]
        public IEnumerator RelayChain_EditorEndToEnd()
        {
            var host = Environment.GetEnvironmentVariable("FRUKTSTAND_RELAY") ?? "127.0.0.1:8787";
            var code = Environment.GetEnvironmentVariable("FRUKTSTAND_CODE") ?? "4821";
            // FRUKTSTAND_DISCOVER=1: find the relay through its UDP beacon, like the Quest does.
            RelayLink.OverrideAddress = Environment.GetEnvironmentVariable("FRUKTSTAND_DISCOVER") == "1" ? null : $"ws://{host}/ws";
            var link = s.StartRelayLink();
            yield return Until(() => link.State == LinkState.Connected, 15, "Quest side connected to the relay");
            Debug.Log($"[Fruktstand] relay chain: link connected to {link.Address}");

            using var phone = new FakePhone();
            var open = phone.Open($"ws://{host}/ws?role=phone&code={code}&client=phone-unity01");
            yield return Until(() => open.IsCompleted, 5, "phone connected");
            Assert.IsFalse(open.IsFaulted, open.Exception?.ToString());
            yield return Until(() => phone.Seen.Contains("\"online\":true") || phone.Seen.Contains("\"questOnline\":true"), 5, "phone sees the Quest");

            phone.Send(PhotoJson("photo-unity001", new Color32(220, 60, 60, 255)));
            phone.Send("{\"t\":\"order\",\"orderId\":\"order-unity001\",\"items\":[\"apple\"],\"photoId\":\"photo-unity001\"}");
            phone.Send("{\"t\":\"order\",\"orderId\":\"order-unity001\",\"items\":[\"apple\"],\"photoId\":\"photo-unity001\"}"); // duplicate
            yield return Until(() => phone.Seen.Contains("\"state\":\"waiting\""), 5, "phone hears: väntar på tur");
            Assert.IsNotNull(s.Market.Remote.ActiveId);

            SellWithHands(); // the NPC first
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 0.2f);
            yield return null;
            Assert.IsTrue(s.Shop.CustomerIsHuman);
            yield return Until(() => phone.Seen.Contains("\"state\":\"serving\""), 5, "phone hears: beställning mottagen");
            yield return Until(() => s.Customer.Portrait.Visible, 5, "photo on the customer");

            var till = s.Wallet.Balance;
            SellWithHands();
            Assert.AreEqual(till + 1, s.Wallet.Balance);
            yield return Until(() => phone.Seen.Contains("\"state\":\"done\""), 5, "phone hears: köpet är klart");

            phone.Send("{\"t\":\"pay\",\"orderId\":\"order-unity001\"}");
            phone.Send("{\"t\":\"order\",\"orderId\":\"order-unity001\",\"items\":[\"apple\"]}");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(till + 1, s.Wallet.Balance, "the till moved exactly once");
            Assert.IsNull(s.Market.Remote.ActiveId, "no second order");
            phone.Dispose();
            link.End();
            RelayLink.OverrideAddress = null;
            Debug.Log("[Fruktstand] relay chain OK. Phone saw:\n" + phone.Seen);
        }
    }
}
