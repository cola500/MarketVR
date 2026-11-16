// EPIC-008: the round's earnings threshold, the party and the rest break (Core.MarketRound + Market/Shop wiring).
// Names match the [T: ...] references in docs/EPIC-008.md.
using Fruktstand.Core;
using NUnit.Framework;

namespace Fruktstand.Tests
{
    public class MarketRoundTests
    {
        static void UntilOrdering(Market m)
        {
            for (var i = 0; i < 400 && m.Shop.State != SaleState.Ordering; i++) m.Tick(0.25f);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State);
        }

        // Serves the customer at the counter (basket + pay) without walking on.
        static int Pay(Market m)
        {
            UntilOrdering(m);
            var order = m.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) Assert.GreaterOrEqual(m.Shop.TryAddToBasket(w), 0);
            Assert.IsTrue(m.Shop.TryPay());
            return order.Sum;
        }

        static void Leave(Market m)
        {
            m.Tick(Shop.ThankSeconds);
            m.Tick(Shop.LeaveSeconds);
        }

        // Sells until the round's earnings are at least `target`; returns the sum of the sale that crossed it.
        static int SellUntil(Market m, int target)
        {
            var last = 0;
            for (var i = 0; i < 200 && m.Round.Earned < target; i++)
            {
                last = Pay(m);
                if (m.Round.Earned < target) Leave(m);
            }
            return last;
        }

        [Test]
        public void Round_Under80NoParty()
        {
            var m = new Market();
            Assert.AreEqual(80, MarketRound.DefaultThreshold);
            Assert.AreEqual(80, m.Round.Threshold);
            for (var i = 0; i < 200 && m.Round.Earned + 4 < 80; i++)
            {
                Pay(m);
                Leave(m);
            }
            Assert.Less(m.Round.Earned, 80);
            Assert.AreEqual(RoundState.Playing, m.Round.State, "under the threshold: no party");
            UntilOrdering(m);
            Assert.AreEqual(m.Wallet.Balance, m.Round.Earned, "earnings come from the registered sales");
        }

        [Test]
        public void Round_Exactly80AndPassingMakePartyPending()
        {
            var exact = new Market(threshold: 1); // customer 1 always wants one apple: exactly 1
            Assert.AreEqual(1, Pay(exact));
            Assert.AreEqual(RoundState.PartyPending, exact.Round.State, "exactly the threshold");

            var passing = new Market(threshold: 4);
            var before = 0;
            for (var i = 0; i < 50 && passing.Round.State == RoundState.Playing; i++)
            {
                before = passing.Wallet.Balance;
                var sum = Pay(passing);
                if (passing.Round.State == RoundState.Playing) Leave(passing);
                else Assert.AreEqual(before + sum, passing.Wallet.Balance, "the sale that crosses is registered whole");
            }
            Assert.AreEqual(RoundState.PartyPending, passing.Round.State);
            Assert.GreaterOrEqual(passing.Round.Earned, 4);
        }

        [Test]
        public void Round_RepeatedSaleEventNotCounted()
        {
            var m = new Market(threshold: 10);
            Pay(m);
            var earned = m.Round.Earned;
            Assert.IsFalse(m.Shop.TryPay(), "a repeated register event");
            Assert.IsFalse(m.Shop.TryPay());
            Assert.AreEqual(earned, m.Round.Earned, "counted once");
            Assert.AreEqual(1, m.Round.SalesThisRound);
        }

        [Test]
        public void Round_NoNewCustomersWhenPartyPendingOrResting()
        {
            var m = new Market(threshold: 1);
            Pay(m);
            var number = m.Shop.CustomerNumber;
            Leave(m);
            for (var i = 0; i < 40; i++) m.Tick(0.5f);
            Assert.AreEqual(SaleState.Closed, m.Shop.State, "the customer thanked and left; nobody new");
            Assert.AreEqual(number, m.Shop.CustomerNumber);
            Assert.IsTrue(m.Shop.IsOpen, "the Öppet/Stängt choice is untouched");
            m.Shop.SetOpen(false);
            m.Shop.SetOpen(true);
            Assert.AreEqual(SaleState.Closed, m.Shop.State, "flipping the sign doesn't bring customers during the party");
        }

        [Test]
        public void Round_AcceptedPhoneOrderServedBeforeParty()
        {
            var m = new Market(threshold: 1);
            UntilOrdering(m);
            Assert.AreEqual(RemoteState.Waiting, m.Remote.Request("order-0001abcd", new[] { "banana" }).State, "accepted before the threshold");
            Pay(m); // the threshold is reached
            Assert.AreEqual(RoundState.PartyPending, m.Round.State);
            var late = m.Remote.Request("order-0002abcd", new[] { "apple" });
            Assert.AreEqual(RemoteState.Rejected, late.State, "new phone orders wait for the next round");
            Assert.AreEqual("paused", late.Reason);
            Leave(m);
            UntilOrdering(m);
            Assert.IsTrue(m.Shop.CustomerIsHuman, "the accepted phone order is still served: nothing disappears");
            Pay(m);
            Leave(m);
            for (var i = 0; i < 20; i++) m.Tick(0.5f);
            Assert.AreEqual(SaleState.Closed, m.Shop.State, "then nobody new");
            Assert.AreEqual(RemoteState.Done, m.Remote.Status("order-0001abcd").State);
        }

        [Test]
        public void Round_WalletKeptThresholdConfigurableTimeRecorded()
        {
            var m = new Market(threshold: 3);
            m.Tick(5f);
            SellUntil(m, 3);
            Assert.AreEqual(RoundState.PartyPending, m.Round.State);
            var balance = m.Wallet.Balance;
            Assert.IsTrue(m.Round.SecondsToThreshold.HasValue, "the time to the threshold is recorded");
            Assert.Greater(m.Round.SecondsToThreshold.Value, 5f);
            Leave(m);
            for (var i = 0; i < 100; i++) m.Tick(0.5f, presentationReady: true, paused: false);
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            Assert.AreEqual(balance, m.Wallet.Balance, "the party and the rest cost nothing");
        }

        // Reaches the threshold and lets the customer leave: the party is pending and the shop done.
        static Market Pending(int threshold = 1)
        {
            var m = new Market(threshold: threshold);
            SellUntil(m, threshold);
            Leave(m);
            Assert.AreEqual(RoundState.PartyPending, m.Round.State);
            return m;
        }

        static void Celebrate(Market m)
        {
            m.Tick(0.1f, presentationReady: true, paused: false);
            Assert.AreEqual(RoundState.Celebrating, m.Round.State);
            for (var i = 0; i < 400 && m.Round.State == RoundState.Celebrating; i++) m.Tick(0.1f, true, false);
            Assert.AreEqual(RoundState.Resting, m.Round.State);
        }

        [Test]
        public void Round_CelebrationWaitsUntilReady()
        {
            // The customer is still thanking: no party yet, even when the presentation is ready.
            var m = new Market(threshold: 1);
            Pay(m);
            m.Tick(0.1f, presentationReady: true, paused: false);
            Assert.AreEqual(RoundState.PartyPending, m.Round.State, "the customer finishes first");
            Leave(m);
            // Something still in a hand (presentation not ready): waits as long as it takes.
            for (var i = 0; i < 100; i++) m.Tick(0.1f, presentationReady: false, paused: false);
            Assert.AreEqual(RoundState.PartyPending, m.Round.State);
            m.Tick(0.1f, presentationReady: true, paused: true);
            Assert.AreEqual(RoundState.PartyPending, m.Round.State, "not during a menu / focus loss");
            m.Tick(0.1f, presentationReady: true, paused: false);
            Assert.AreEqual(RoundState.Celebrating, m.Round.State);

            // A child still walking to the table with a toy just bought: waits for that too.
            var toy = new Market(threshold: 1);
            Assert.IsTrue(toy.PlayArea.TryReserve(Ware.Ball, 7));
            Assert.IsTrue(toy.PlayArea.Sold(Ware.Ball));
            Pay(toy);
            Leave(toy);
            toy.Tick(0.1f, true, false);
            Assert.IsTrue(toy.PlayArea.AnyWalking || toy.Round.State == RoundState.Celebrating);
            for (var i = 0; i < 100 && toy.Round.State == RoundState.PartyPending; i++)
            {
                Assert.IsTrue(toy.PlayArea.AnyWalking, "waiting only while the child is still on the way");
                toy.Tick(0.1f, true, false);
            }
            Assert.AreEqual(RoundState.Celebrating, toy.Round.State);
        }

        [Test]
        public void Round_CelebrationOncePerRound()
        {
            var m = Pending(threshold: 2);
            Celebrate(m);
            Assert.AreEqual(1, m.Round.Stars, "one star for the round");
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            m.Round.SaleRegistered(50); // a stray late event in the rest
            for (var i = 0; i < 50; i++) m.Tick(0.5f, true, false);
            Assert.AreEqual(RoundState.Resting, m.Round.State, "no second party in the same round");
            Assert.AreEqual(1, m.Round.Stars);
        }

        [Test]
        public void Round_NextRoundResetsCounterNoImmediateParty()
        {
            var m = Pending(threshold: 2);
            Celebrate(m);
            var balance = m.Wallet.Balance;
            Assert.IsTrue(m.StartNextRound());
            Assert.AreEqual(RoundState.Playing, m.Round.State);
            Assert.AreEqual(0, m.Round.Earned, "the round's earnings start over");
            Assert.AreEqual(balance, m.Wallet.Balance, "the till stays");
            Assert.AreEqual(1, m.Round.Stars, "the stars stay");
            for (var i = 0; i < 20; i++) m.Tick(0.5f, true, false);
            Assert.AreEqual(RoundState.Playing, m.Round.State, "no new party straight away");
            Assert.AreEqual(SaleState.Ordering, m.Shop.State, "customers come again");
            SellUntil(m, 2);
            Assert.AreEqual(RoundState.PartyPending, m.Round.State, "the next round can reach its own party");
        }

        [Test]
        public void Round_ManualRestNoStar()
        {
            var m = new Market();
            UntilOrdering(m);
            Assert.IsTrue(m.TakeRest(), "a rest any time: the threshold never stops an earlier break");
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            Assert.AreEqual(0, m.Round.Stars, "no star without the threshold");
            var order = m.Shop.Order;
            for (var i = 0; i < 40; i++) m.Tick(0.5f, true, false);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State, "the customer waits calmly through the rest");
            Assert.AreSame(order, m.Shop.Order);
            Assert.AreEqual(RemoteState.Rejected, m.Remote.Request("order-0009abcd", new[] { "apple" }).State, "no phone orders while resting");
            Assert.IsTrue(m.StartNextRound());
            Assert.AreSame(order, m.Shop.Order, "and is served after it");
            Assert.IsFalse(m.StartNextRound(), "only from a rest");
            Assert.IsTrue(Pending().TakeRest(), "also while a party is pending");
        }

        [Test]
        public void Round_FocusLossPausesCelebration()
        {
            var m = Pending();
            m.Tick(0.1f, true, false);
            Assert.AreEqual(RoundState.Celebrating, m.Round.State);
            m.Tick(5f, true, false);
            for (var i = 0; i < 100; i++) m.Tick(0.5f, true, paused: true);
            Assert.AreEqual(RoundState.Celebrating, m.Round.State, "the party's clock stands still");
            Assert.AreEqual(5f, m.Round.StateTime, 1e-4);
            m.Tick(MarketRound.CelebrationSeconds, true, false);
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            Assert.IsFalse(m.TakeRest(), "already resting");
        }

        [Test]
        public void Round_NewGameResetsEverything()
        {
            var m = Pending();
            Celebrate(m);
            m.NewGame();
            Assert.AreEqual(RoundState.Playing, m.Round.State);
            Assert.AreEqual(0, m.Round.Earned);
            Assert.AreEqual(0, m.Round.Stars, "the party board starts over with the session");
            Assert.AreEqual(0, m.Wallet.Balance);
            Assert.IsFalse(m.Round.SecondsToThreshold.HasValue);
            UntilOrdering(m);
            Assert.AreEqual(Order.Of(m.Assortment.FirstFruit), m.Shop.Order, "the new game starts as always (EPIC-009: with its first fruit)");
        }
    }
}
