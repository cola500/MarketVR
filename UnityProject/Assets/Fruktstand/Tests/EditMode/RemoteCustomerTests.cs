// EPIC-004 rules and protocol: a phone order becomes an ordinary customer order, the Quest stays authoritative,
// duplicates and reconnects never make a second order or payment. Names match docs/EPIC-004.md.
using System;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class RemoteCustomerTests
    {
        const string Id = "order-0001abcd";

        static void UntilOrdering(Shop shop)
        {
            var guard = 0;
            while (shop.State != SaleState.Ordering && guard++ < 200) shop.Tick(0.25f);
            Assert.AreEqual(SaleState.Ordering, shop.State);
        }

        // Serves whoever is at the counter, including paying, and walks on to the next customer.
        static Order Sell(Market m)
        {
            UntilOrdering(m.Shop);
            var order = m.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) Assert.GreaterOrEqual(m.Shop.TryAddToBasket(w), 0);
            Assert.IsTrue(m.Shop.TryPay());
            m.Shop.Tick(Shop.ThankSeconds);
            m.Shop.Tick(Shop.LeaveSeconds);
            return order;
        }

        static RemoteReceipt Ask(Market m, string id, params string[] items) => m.Remote.Request(id, items);

        [Test]
        public void Remote_OrderTakesTheNextFreeTurn()
        {
            var m = new Market();
            UntilOrdering(m.Shop);
            var npc = m.Shop.Order;
            var number = m.Shop.CustomerNumber;
            var r = Ask(m, Id, "apple");
            Assert.AreEqual(RemoteState.Waiting, r.State);
            Assert.AreEqual(1, r.Sum);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State, "the NPC at the counter is not interrupted");
            Assert.AreSame(npc, m.Shop.Order);
            Assert.AreEqual(number, m.Shop.CustomerNumber);
            Assert.IsFalse(m.Shop.CustomerIsHuman);

            Sell(m);
            Assert.IsTrue(m.Shop.CustomerIsHuman, "the human customer is next");
            Assert.AreEqual(Order.Of(Ware.Apple), m.Shop.Order, "an ordinary order in the ordinary shop");
            Assert.AreEqual(Category.FruitVeg, m.Shop.Category);
            Assert.IsFalse(m.Shop.CustomerIsChild);
            Assert.AreEqual(RemoteState.Serving, m.Remote.Status(Id).State);
        }

        [Test]
        public void Remote_NoNewNpcWhileHumanWaitsOrIsServed()
        {
            var m = new Market();
            Sell(m); // customer 1 (apple) done
            UntilOrdering(m.Shop);
            Ask(m, Id, "banana", "carrot");
            Sell(m); // the NPC that was already here
            var human = m.Shop.CustomerNumber;
            Assert.IsTrue(m.Shop.CustomerIsHuman);
            for (var i = 0; i < 40; i++) m.Shop.Tick(0.5f); // a long wait: nobody else comes, nothing times out
            Assert.AreEqual(human, m.Shop.CustomerNumber);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State);
            Sell(m);
            Assert.AreEqual(human + 1, m.Shop.CustomerNumber, "NPC customers come again after the human customer");
            Assert.IsFalse(m.Shop.CustomerIsHuman);
        }

        [Test]
        public void Remote_WaitsWhileMarketClosed()
        {
            var m = new Market();
            UntilOrdering(m.Shop);
            m.Shop.SetOpen(false);
            Ask(m, Id, "apple");
            Sell(m);
            Assert.AreEqual(SaleState.Closed, m.Shop.State, "Stängt pauses every new customer, the phone one too");
            Assert.AreEqual(RemoteState.Waiting, m.Remote.Status(Id).State);
            m.Shop.SetOpen(true);
            Assert.IsTrue(m.Shop.CustomerIsHuman);
        }

        [Test]
        public void Remote_PaymentCreditsOnceAndIsDone()
        {
            var m = new Market();
            Ask(m, Id, "polkagris", "candybag");
            Sell(m);
            UntilOrdering(m.Shop);
            Assert.IsTrue(m.Shop.CustomerIsHuman);
            var before = m.Wallet.Balance;
            Assert.GreaterOrEqual(m.Shop.TryAddToBasket(Ware.Polkagris), 0);
            Assert.GreaterOrEqual(m.Shop.TryAddToBasket(Ware.CandyBag), 0);
            Assert.IsTrue(m.Shop.TryPay());
            Assert.IsFalse(m.Shop.TryPay(), "a repeated register event");
            Assert.IsFalse(m.Shop.TryPay());
            Assert.AreEqual(before + 5, m.Wallet.Balance, "exactly once, with the Quest's prices (2 + 3)");
            var r = m.Remote.Status(Id);
            Assert.AreEqual(RemoteState.Done, r.State);
            Assert.AreEqual(5, r.Sum);
            Assert.AreEqual(1, m.Remote.CompletedOrders);
            Assert.IsTrue(m.Shop.CustomerIsHuman, "still the same customer while thanking");
        }

        [Test]
        public void Remote_SameIdNeverMakesASecondOrder()
        {
            var m = new Market();
            Assert.IsFalse(Ask(m, Id, "apple").Duplicate);
            var again = Ask(m, Id, "apple");
            Assert.IsTrue(again.Duplicate);
            Assert.AreEqual(RemoteState.Waiting, again.State);
            Sell(m);
            Assert.IsTrue(Ask(m, Id, "apple").Duplicate, "resent while being served");
            var balance = m.Wallet.Balance;
            Sell(m); // the human customer
            Assert.AreEqual(balance + 1, m.Wallet.Balance);
            var afterDone = Ask(m, Id, "apple", "apple");
            Assert.IsTrue(afterDone.Duplicate);
            Assert.AreEqual(RemoteState.Done, afterDone.State, "a replay after the sale only reports it");
            for (var i = 0; i < 5; i++)
            {
                Assert.IsFalse(m.Shop.CustomerIsHuman, "no second human turn");
                Sell(m);
            }
            Assert.AreEqual(1, m.Remote.CompletedOrders);
        }

        [Test]
        public void Remote_OneActiveOrderAtATime()
        {
            var m = new Market();
            Ask(m, Id, "apple");
            var other = Ask(m, "order-0002abcd", "banana");
            Assert.AreEqual(RemoteState.Rejected, other.State);
            Assert.AreEqual("busy", other.Reason);
            Sell(m);
            Sell(m); // the human customer done
            Assert.AreEqual(RemoteState.Waiting, Ask(m, "order-0002abcd", "banana").State, "the next one is fine afterwards");
        }

        [Test]
        public void Remote_InvalidRequestsAreRejected()
        {
            var m = new Market();
            string[][] bad =
            {
                new string[0], new[] { "apple", "apple", "apple", "apple" }, new[] { "apple", "lollipop" },
                new[] { "ball" }, new[] { "car" }, new[] { "blocks" }, new[] { "6" }, new[] { "Apple" }, new[] { "kiwi" },
                new string[] { null }, null,
            };
            var n = 0;
            foreach (var items in bad)
            {
                var r = m.Remote.Request($"order-bad{n++:0000}", items);
                Assert.AreEqual(RemoteState.Rejected, r.State, string.Join(",", items ?? new[] { "null" }));
                Assert.AreEqual("items", r.Reason);
            }
            foreach (var id in new[] { null, "", "short", new string('a', 41), "order 0001", "order-0001<>", "äpple-00001" })
                Assert.AreEqual("id", m.Remote.Request(id, new[] { "apple" }).Reason, id ?? "null");
            Assert.IsNull(m.Remote.ActiveId);
            Assert.AreEqual(RemoteState.Waiting, Ask(m, Id, "apple", "apple", "carrot").State, "3 of fruit/veg is fine");
        }

        [Test]
        public void Remote_NewGameCancelsTheOrder()
        {
            var m = new Market();
            Ask(m, Id, "apple");
            Sell(m);
            Assert.IsTrue(m.Shop.CustomerIsHuman);
            m.NewGame();
            Assert.AreEqual(RemoteState.Cancelled, m.Remote.Status(Id).State);
            Assert.AreEqual("newgame", m.Remote.Status(Id).Reason);
            Assert.IsFalse(m.Shop.CustomerIsHuman);
            Assert.AreEqual(Order.Of(m.Assortment.FirstFruit), m.Shop.Order, "the new game starts as always (EPIC-009: with its first fruit)");
            Assert.AreEqual(1, m.Shop.CustomerNumber);
            Assert.AreEqual(0, m.Wallet.Balance);
            Assert.AreEqual(RemoteState.Cancelled, Ask(m, Id, "apple").State, "the old id stays cancelled");
            Assert.AreEqual(RemoteState.Waiting, Ask(m, "order-0003abcd", RemoteCustomer.KeyOf(m.Assortment.FirstFruit)).State, "a new order is fine");
        }

        [Test]
        public void Remote_StatusAfterReconnect()
        {
            var m = new Market();
            Assert.AreEqual(RemoteState.Unknown, m.Remote.Status(Id).State, "an id the Quest never saw (e.g. after an app restart)");
            Ask(m, Id, "apple");
            Assert.AreEqual(RemoteState.Waiting, m.Remote.Status(Id).State);
            Sell(m);
            Assert.AreEqual(RemoteState.Serving, m.Remote.Status(Id).State);
            Sell(m);
            Assert.AreEqual(RemoteState.Done, m.Remote.Status(Id).State);
        }

        // ---------- protocol (RemoteSession) ----------

        static (Market m, RemoteSession s, PhotoStore p) Session()
        {
            var m = new Market();
            var p = new PhotoStore();
            return (m, new RemoteSession(m.Shop, p), p);
        }

        static string OrderJson(string id, string items, string extra = "") =>
            $"{{\"t\":\"order\",\"orderId\":\"{id}\",\"items\":[{items}]{extra}}}";

        [Test]
        public void Protocol_OrderJsonBecomesCustomer()
        {
            var (m, s, _) = Session();
            var replies = s.Handle(OrderJson(Id, "\"apple\""));
            Assert.AreEqual(1, replies.Count);
            StringAssert.Contains("\"state\":\"waiting\"", replies[0]);
            StringAssert.Contains("\"duplicate\":false", replies[0]);
            Sell(m);
            Assert.IsTrue(m.Shop.CustomerIsHuman);
            StringAssert.Contains("\"state\":\"serving\"", s.Pump());
            Assert.IsNull(s.Pump(), "only on change");
            StringAssert.Contains("\"state\":\"serving\"", s.Handle($"{{\"t\":\"sync\",\"orderId\":\"{Id}\"}}")[0]);
            StringAssert.Contains("\"duplicate\":true", s.Handle(OrderJson(Id, "\"apple\""))[0], "a resent order");
            Sell(m);
            StringAssert.Contains("\"state\":\"done\"", s.Pump());
        }

        [Test]
        public void Protocol_PhoneCannotChangeTheTill()
        {
            var (m, s, _) = Session();
            var before = m.Wallet.Balance;
            string[] attempts =
            {
                "{\"t\":\"pay\",\"orderId\":\"order-0001abcd\"}",
                "{\"t\":\"status\",\"orderId\":\"order-0001abcd\",\"state\":\"done\"}",
                "{\"t\":\"wallet\",\"balance\":1000}",
                "{\"t\":\"register\"}",
                OrderJson(Id, "\"apple\"", ",\"sum\":99,\"wallet\":100,\"state\":\"done\",\"price\":50"),
            };
            foreach (var a in attempts) s.Handle(a);
            Assert.AreEqual(before, m.Wallet.Balance, "no message moves money");
            Assert.AreEqual(RemoteState.Waiting, m.Remote.Status(Id).State, "a phone cannot mark an order done");
            Assert.AreEqual(1, m.Remote.Status(Id).Sum, "the sum comes from the Quest's prices");
            Assert.AreEqual(4, s.Ignored);
            Assert.AreEqual(before, m.Wallet.Balance);
        }

        [Test]
        public void Protocol_MalformedMessagesAreIgnored()
        {
            var (m, s, _) = Session();
            foreach (var junk in new[] { null, "", "not json", "{", "[1,2]", "{\"t\":5}", new string('x', RemoteSession.MaxMessageChars + 1), OrderJson(Id, "1,2") })
                Assert.DoesNotThrow(() => s.Handle(junk));
            Assert.IsNull(m.Remote.ActiveId);
        }

        static string Pattern(int side, out byte[] raw)
        {
            raw = new byte[side * side * 4];
            for (var y = 0; y < side; y++)
                for (var x = 0; x < side; x++)
                {
                    var i = (y * side + x) * 4;
                    raw[i] = (byte)(y == 0 ? 255 : 0); // top row (canvas order) red
                    raw[i + 1] = (byte)(x == 0 ? 255 : 0); // left column green
                    raw[i + 2] = 40;
                    raw[i + 3] = 255;
                }
            return Convert.ToBase64String(raw);
        }

        [Test]
        public void Photo_DecodedOnceTopRowUp()
        {
            var (_, s, p) = Session();
            var b64 = Pattern(128, out _);
            s.Handle($"{{\"t\":\"photo\",\"photoId\":\"photo-0001abcd\",\"w\":128,\"h\":128,\"rgba\":\"{b64}\"}}");
            var tex = p.Get("photo-0001abcd");
            Assert.IsNotNull(tex);
            Assert.AreEqual(128, tex.width);
            Assert.Greater(tex.GetPixel(64, 127).r, 0.9f, "the canvas' top row is the texture's top");
            Assert.Less(tex.GetPixel(64, 0).r, 0.1f);
            Assert.Greater(tex.GetPixel(0, 64).g, 0.9f, "not mirrored");
            s.Handle($"{{\"t\":\"photo\",\"photoId\":\"photo-0001abcd\",\"w\":128,\"h\":128,\"rgba\":\"{b64}\"}}");
            Assert.AreSame(tex, p.Get("photo-0001abcd"), "a resend is not decoded again");
            CollectionAssert.AreEqual(new[] { "photo-0001abcd" }, p.Ids());
            p.Clear();
        }

        [Test]
        public void Photo_LimitsAreEnforced()
        {
            var p = new PhotoStore();
            var ok = Pattern(64, out _);
            Assert.IsTrue(p.TryAdd("photo-0064abcd", 64, 64, ok), "smaller squares are fine");
            Assert.IsFalse(p.TryAdd("photo-0002abcd", 64, 32, ok), "not square");
            Assert.IsFalse(p.TryAdd("photo-0003abcd", 256, 256, Pattern(256, out _)), "too large");
            Assert.IsFalse(p.TryAdd("photo-0004abcd", 8, 8, Pattern(8, out _)), "too small");
            Assert.IsFalse(p.TryAdd("photo-0005abcd", 64, 64, ok.Substring(4)), "wrong length");
            Assert.IsFalse(p.TryAdd("photo-0006abcd", 64, 64, "!" + ok.Substring(1)), "not base64");
            Assert.IsFalse(p.TryAdd("bad id", 64, 64, ok));
            Assert.IsFalse(p.TryAdd("photo-0007abcd", 64, 64, null));
            for (var i = 0; i < 5; i++) Assert.IsTrue(p.TryAdd($"photo-1{i:000}abcd", 64, 64, ok));
            Assert.AreEqual(PhotoStore.MaxPhotos, p.Count, "only the latest few are kept");
            p.Clear();
        }

        [Test]
        public void Protocol_StatusNeverCarriesImageData()
        {
            var (m, s, p) = Session();
            var b64 = Pattern(128, out _);
            s.Handle($"{{\"t\":\"photo\",\"photoId\":\"photo-0001abcd\",\"w\":128,\"h\":128,\"rgba\":\"{b64}\"}}");
            var outgoing = s.Handle(OrderJson(Id, "\"apple\"", ",\"photoId\":\"photo-0001abcd\"")).ToList();
            outgoing.Add(s.HelloMessage());
            Sell(m);
            outgoing.Add(s.Pump());
            Assert.IsTrue(m.Shop.CustomerIsHuman);
            Assert.IsNotNull(s.PhotoForCurrentCustomer, "the customer has the photo");
            Sell(m);
            outgoing.Add(s.Pump());
            foreach (var o in outgoing)
            {
                Assert.Less(o.Length, 400, o);
                StringAssert.DoesNotContain("rgba", o);
            }
            StringAssert.Contains("photo-0001abcd", outgoing[1], "hello lists the photo ids the Quest has, not the photos");
            p.Clear();
        }

        [Test]
        public void Beacon_ParsesPort()
        {
            Assert.AreEqual(8787, Beacon.Port("{\"fruktstand\":\"relay\",\"port\":8787}"));
            Assert.AreEqual(-1, Beacon.Port("{\"fruktstand\":\"relay\"}"));
            Assert.AreEqual(-1, Beacon.Port("{\"port\":0}"));
            Assert.AreEqual(-1, Beacon.Port("{\"port\":99999999}"));
            Assert.AreEqual(-1, Beacon.Port(null));
        }

        // ---------- EPIC-008: the parent's pause/next round from the phone, and the market's status ----------

        static (Market m, RemoteSession s, System.Collections.Generic.List<string> asked) ParentSession()
        {
            var m = new Market(threshold: 80);
            var asked = new System.Collections.Generic.List<string>();
            var s = new RemoteSession(m.Shop, new PhotoStore())
            {
                // What the game does with an accepted command (FruitStand queues it for its next step).
                OnParentCommand = cmd =>
                {
                    asked.Add(cmd);
                    if (cmd == "rest") return m.TakeRest();
                    return cmd == "nextRound" && m.StartNextRound();
                },
            };
            return (m, s, asked);
        }

        static string Parent(string cmd, string id) => $"{{\"t\":\"parent\",\"cmd\":\"{cmd}\",\"cmdId\":\"{id}\"}}";

        [Test]
        public void Protocol_ParentCommandsSeparateFromCustomer()
        {
            var (m, s, asked) = ParentSession();
            foreach (var junk in new[]
            {
                "{\"t\":\"order\",\"orderId\":\"order-0001abcd\",\"items\":[\"apple\"],\"cmd\":\"rest\"}", // a customer message never pauses
                Parent("wallet", "cmd-00001abc"), Parent("newGame", "cmd-00002abc"), Parent("setState", "cmd-00003abc"),
                Parent("rest", "bad id"), "{\"t\":\"parent\",\"cmd\":\"rest\"}",
            })
                s.Handle(junk);
            Assert.AreEqual(0, asked.Count, "only rest/nextRound, with a valid command id, in a parent message");
            Assert.AreEqual(RoundState.Playing, m.Round.State);
            var reply = s.Handle(Parent("rest", "cmd-00004abc")).Single();
            StringAssert.Contains("\"t\":\"parentAck\"", reply);
            StringAssert.Contains("\"accepted\":true", reply);
            Assert.AreEqual(RoundState.Resting, m.Round.State, "the same rest as the menu's");
            Assert.AreEqual(0, m.Round.Stars, "no reward");
        }

        [Test]
        public void Protocol_ParentCommandsExactlyOnce()
        {
            var (m, s, asked) = ParentSession();
            s.Handle(Parent("rest", "cmd-00001abc"));
            var again = s.Handle(Parent("rest", "cmd-00001abc")).Single();
            StringAssert.Contains("\"duplicate\":true", again, "a resent command is only acknowledged");
            Assert.AreEqual(1, asked.Count, "paused exactly once");
            s.Handle(Parent("nextRound", "cmd-00002abc"));
            Assert.AreEqual(RoundState.Playing, m.Round.State);
            s.Handle(Parent("rest", "cmd-00001abc")); // the old pause arriving late (a reconnect) must not pause again
            s.Handle(Parent("nextRound", "cmd-00002abc"));
            Assert.AreEqual(RoundState.Playing, m.Round.State, "resumed exactly once, and no late pause");
            Assert.AreEqual(2, asked.Count);
            var refused = s.Handle(Parent("nextRound", "cmd-00003abc")).Single();
            StringAssert.Contains("\"accepted\":false", refused, "the next round only from a rest");
        }

        [Test]
        public void Protocol_MarketStatusOnChange()
        {
            var (m, s, _) = ParentSession();
            StringAssert.Contains("\"state\":\"playing\"", s.PumpMarket(), "the first status");
            Assert.IsNull(s.PumpMarket(), "only on change");
            m.TakeRest();
            var resting = s.PumpMarket();
            StringAssert.Contains("\"t\":\"market\"", resting);
            StringAssert.Contains("\"state\":\"resting\"", resting);
            StringAssert.DoesNotContain("rgba", resting);
            Assert.Less(resting.Length, 200);
            s.ResendMarket();
            StringAssert.Contains("resting", s.PumpMarket(), "sent again after a reconnect");
        }

        [Test]
        public void Protocol_OrdersRejectedWhilePaused()
        {
            var (m, s, _) = ParentSession();
            m.TakeRest();
            var reply = s.Handle(OrderJson(Id, "\"apple\"")).Single();
            StringAssert.Contains("\"state\":\"rejected\"", reply);
            StringAssert.Contains("\"reason\":\"paused\"", reply);
            Assert.IsNull(m.Remote.ActiveId);
        }
    }
}
