// EPIC-016: a short pling when a new customer is ready to order, exactly once per customer (NPC or phone); never
// behind a menu, the system menu or a rest (no queue of old plings afterwards); and a waiting marker at that stall
// while the customer waits. Names match docs/EPIC-016.md.
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class ArrivalChimeTests
    {
        [Test]
        public void Chime_OnceWhenTheCustomerIsReady()
        {
            var chime = new ArrivalChime();
            Assert.IsFalse(chime.Update(SaleState.CustomerArriving, 1, false), "not while walking in");
            Assert.IsTrue(chime.Update(SaleState.Ordering, 1, false), "ready to order");
            for (var i = 0; i < 50; i++) Assert.IsFalse(chime.Update(SaleState.Ordering, 1, false), "once, not every frame");
            Assert.IsFalse(chime.Update(SaleState.AwaitingPayment, 1, false));
            Assert.IsFalse(chime.Update(SaleState.Ordering, 1, false), "Töm korgen: the same customer again, no new pling");
            Assert.IsFalse(chime.Update(SaleState.Thanking, 1, false));
            Assert.IsFalse(chime.Update(SaleState.CustomerArriving, 2, false));
            Assert.IsTrue(chime.Update(SaleState.Ordering, 2, false), "the next customer gets one");
        }

        [Test]
        public void Chime_NoneForACustomerWhoBecameReadyWhilePaused()
        {
            var chime = new ArrivalChime();
            Assert.IsFalse(chime.Update(SaleState.Ordering, 1, true), "behind the menu or the system menu: silent");
            Assert.IsFalse(chime.Update(SaleState.Ordering, 1, false), "and no old pling once it is closed");
            Assert.IsTrue(chime.Update(SaleState.Ordering, 2, false));
        }

        [Test]
        public void Chime_PauseAndResumeGiveNoExtraSignal()
        {
            var chime = new ArrivalChime();
            Assert.IsTrue(chime.Update(SaleState.Ordering, 1, false));
            for (var i = 0; i < 10; i++) Assert.IsFalse(chime.Update(SaleState.Ordering, 1, true));
            Assert.IsFalse(chime.Update(SaleState.Ordering, 1, false), "resumed: nothing again");
        }

        [Test]
        public void Chime_NewGameStartsOver()
        {
            var chime = new ArrivalChime();
            chime.Update(SaleState.Ordering, 1, false);
            chime.Update(SaleState.Ordering, 2, false);
            Assert.IsTrue(chime.Update(SaleState.Ordering, 1, false), "Nytt spel: customer 1 again is a new customer");
        }

        [Test]
        public void Chime_MarkerWhileTheCustomerWaits()
        {
            Assert.IsFalse(ArrivalChime.Waiting(SaleState.CustomerArriving));
            Assert.IsTrue(ArrivalChime.Waiting(SaleState.Ordering));
            Assert.IsTrue(ArrivalChime.Waiting(SaleState.AwaitingPayment), "the basket is right, the coin not yet paid");
            Assert.IsFalse(ArrivalChime.Waiting(SaleState.Thanking), "done");
            Assert.IsFalse(ArrivalChime.Waiting(SaleState.CustomerLeaving));
            Assert.IsFalse(ArrivalChime.Waiting(SaleState.Closed));
        }

        [Test]
        public void Chime_SoundIsShortAndGentle()
        {
            var hello = SoundSynth.Hello();
            Assert.Less(hello.Length / (float)SoundSynth.Rate, 0.8f, "short");
            var peak = 0f;
            foreach (var v in hello) peak = Mathf.Max(peak, Mathf.Abs(v));
            var pling = 0f;
            foreach (var v in SoundSynth.Pling()) pling = Mathf.Max(pling, Mathf.Abs(v));
            Assert.Greater(peak * Sounds.HelloVolume, 0.1f, "audible");
            Assert.Less(peak * Sounds.HelloVolume, pling * Sounds.PlingVolume, "softer than the sale's pling: a hello, not a reward");
            Assert.Less(Mathf.Abs(hello[0]), 0.02f, "no click at the start");
            Assert.Less(Mathf.Abs(hello[^1]), 0.02f, "or at the end");
        }
    
        [Test]
        public void Chime_MarkerInViewFromTheSeat()
        {
            // a seated child looking at the counter (as the seated renders: 40 cm above it, 32 cm behind its centre, looking
            // at the basket area) sees the bell without turning: well inside a 90 x 106 degree view, not at its edge
            var eye = new Vector3(0, FruitStand.CounterBelowEyes, -FruitStand.EyeToCounter); // counter space
            var look = (new Vector3(0, 0.12f, 0.25f) - eye).normalized;
            var to = (StandView.WaitingBellCentre - eye).normalized;
            var sideways = Vector3.Angle(new Vector3(look.x, 0, look.z), new Vector3(to.x, 0, to.z));
            var upDown = Mathf.Abs(Mathf.Asin(to.y) - Mathf.Asin(look.y)) * Mathf.Rad2Deg;
            Assert.LessOrEqual(sideways, 48, "not out at the side");
            Assert.LessOrEqual(upDown, 38, "not up above the eyes or down under the counter");
            var tooClose = new[] { StandView.BasketCentre, StandView.RegisterCentre, StandView.WalletPosition };
            foreach (var p in tooClose)
                Assert.Greater(Vector3.Distance(StandView.WaitingBellCentre, p), 0.15f, "clear of the basket, the till and the wallet");
        }
    }
}
