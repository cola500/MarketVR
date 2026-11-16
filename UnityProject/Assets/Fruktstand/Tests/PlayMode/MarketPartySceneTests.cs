// EPIC-008 in the real scene, with a low developer threshold (the shipped default stays 80): the sale that reaches it,
// the wait for empty hands, the party (sign, star, crocodile, effects), the calm rest and the next round.
// Names match docs/EPIC-008.md.
using System.Collections;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class MarketPartySceneTests
    {
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x); // synchronous load, classic fruit (see SceneLoad)
            s = Object.FindAnyObjectByType<FruitStand>();
            s.ReadDesktopInput = false;
            s.Market.Round.SetThreshold(1); // developer threshold: the first customer's apple reaches it
            yield return null;
        }

        void Step(Vector3? right = null, bool grab = false, float dt = 0.05f) => SceneSteps.Step(s, right, grab, dt);

        void Wait(float seconds) => SceneSteps.Wait(seconds, 0.05f, dt => Step(dt: dt));

        void SellFirstCustomer()
        {
            for (var i = 0; i < 400 && s.Shop.State != SaleState.Ordering; i++) Step();
            s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = Ware.Apple });
            Wait(0.6f);
            Assert.IsTrue(s.RegisterCoin());
        }

        void UntilResting()
        {
            for (var i = 0; i < 1000 && s.Market.Round.State != RoundState.Resting; i++) Step();
            Assert.AreEqual(RoundState.Resting, s.Market.Round.State);
        }

        [UnityTest]
        public IEnumerator Scene_PartyAfterThresholdSale()
        {
            Assert.AreEqual(MarketRound.DefaultThreshold, new MarketRound().Threshold, "the shipped default is 80");
            var till = s.Wallet.Balance;
            var played = s.Sounds.Played;
            SellFirstCustomer();
            Assert.AreEqual(RoundState.PartyPending, s.Market.Round.State);
            Assert.AreEqual(till + 1, s.Wallet.Balance, "the sale that reached it is registered whole");
            Assert.AreNotEqual("Marknadsfest!", s.Party.SignText, "the customer thanks and leaves first");
            for (var i = 0; i < 200 && s.Market.Round.State == RoundState.PartyPending; i++) Step();
            Assert.AreEqual(RoundState.Celebrating, s.Market.Round.State);
            Assert.AreEqual(SaleState.Closed, s.Shop.State, "nobody new at the counter");
            Step();
            Assert.AreEqual("Marknadsfest!", s.Party.SignText);
            Assert.AreEqual(1, s.Party.StarsShown, "a star on the party board");
            Assert.IsTrue(s.Crocodile.Game.PartyDancing, "the crocodile dances along");
            Assert.IsTrue(s.Customer.Cheering, "the customer comes back to wave");
            Assert.Greater(s.Sounds.Played, played, "a little fanfare");
            Wait(3);
            Assert.Greater(s.Party.EffectCount, 0, "fireworks flowers and confetti");
            UntilResting();
            Assert.AreEqual(0, s.Party.EffectCount, "the effects are cleared away");
            Assert.AreEqual("Pausdags!", s.Party.SignText);
            Assert.AreEqual(till + 1, s.Wallet.Balance, "the party costs nothing");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PartyWaitsForHeldItem()
        {
            SellFirstCustomer();
            var apple = s.CrateTop(Ware.Apple);
            Step(apple.Position);
            Step(apple.Position, true);
            Assert.AreSame(apple, s.Held(1));
            for (var i = 0; i < 200; i++) Step(apple.Position, true);
            Assert.AreEqual(RoundState.PartyPending, s.Market.Round.State, "never while something is held");
            Assert.IsTrue(s.Party.ShowingReleaseHint, "a friendly picture: let go of what you hold");
            Assert.AreSame(apple, s.Held(1), "nothing is snatched from the hand");
            Step(apple.Position, false);
            for (var i = 0; i < 40 && s.Market.Round.State == RoundState.PartyPending; i++) Step();
            Assert.AreEqual(RoundState.Celebrating, s.Market.Round.State, "starts once the hand is empty");
            Assert.IsFalse(s.Party.ShowingReleaseHint);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_RestIsCalm()
        {
            SellFirstCustomer();
            UntilResting();
            Assert.IsTrue(s.Party.ShowingRestPanel, "Pausdags! with a picture in front of the player");
            StringAssert.Contains("Ta av headsetet och vila en stund.", s.Party.RestText);
            Assert.Less(s.Sounds.Volume, 0.6f, "the sound is turned down");
            var apple = s.CrateTop(Ware.Apple);
            Step(apple.Position);
            Step(apple.Position, true);
            Assert.IsNull(s.Held(1), "nothing can be taken in the rest");
            Step(apple.Position, false);
            var tooth = s.Crocodile.ToothWorld(0) + Vector3.up * 0.02f;
            Step(tooth);
            Step(tooth, true);
            Assert.IsFalse(s.Crocodile.Game.IsPressed(0), "no crocodile game in the rest");
            Step(tooth, false);
            var customers = s.Shop.CustomerNumber;
            for (var i = 0; i < 600; i++) Step(); // half a minute
            Assert.AreEqual(RoundState.Resting, s.Market.Round.State, "no countdown; only the parent ends the rest");
            Assert.AreEqual(customers, s.Shop.CustomerNumber, "no new customers");
            Assert.AreEqual(RemoteState.Rejected, s.Market.Remote.Request("order-0001abcd", new[] { "apple" }).State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_NextRoundContinues()
        {
            SellFirstCustomer();
            UntilResting();
            var till = s.Wallet.Balance;
            s.Market.Round.SetThreshold(80); // a real round next: no immediate party either way
            s.Step(new ShopFrame { DeltaTime = 0.02f, NextRound = true });
            Assert.AreEqual(RoundState.Playing, s.Market.Round.State);
            Assert.AreEqual(0, s.Market.Round.Earned);
            Assert.AreEqual(till, s.Wallet.Balance, "the till stays");
            Assert.AreEqual(1, s.Party.StarsShown, "the star stays");
            Assert.IsFalse(s.Party.ShowingRestPanel);
            Assert.AreNotEqual("Pausdags!", s.Party.SignText);
            Assert.GreaterOrEqual(s.Sounds.Volume, 0.99f, "sound back up");
            for (var i = 0; i < 400 && s.Shop.State != SaleState.Ordering; i++) Step();
            Assert.AreEqual(SaleState.Ordering, s.Shop.State, "customers come again");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ManualRestFromMenu()
        {
            s.Market.Round.SetThreshold(80);
            for (var i = 0; i < 400 && s.Shop.State != SaleState.Ordering; i++) Step();
            s.Step(new ShopFrame { DeltaTime = 0.02f, TakeRest = true });
            Assert.AreEqual(RoundState.Resting, s.Market.Round.State, "a rest at any time");
            Step();
            Assert.IsTrue(s.Party.ShowingRestPanel);
            Assert.AreEqual(0, s.Party.StarsShown, "no star for an early rest");
            Assert.AreEqual(0, s.Party.EffectCount, "no party either");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CrocWorksAfterRest()
        {
            SellFirstCustomer();
            UntilResting();
            s.Step(new ShopFrame { DeltaTime = 0.02f, NextRound = true });
            for (var i = 0; i < 200 && s.Crocodile.Game.State != CrocState.Open; i++) Step();
            var croc = s.Crocodile.Game;
            Assert.IsFalse(croc.Danced, "the party dance was not the surprise");
            var at = s.Crocodile.ToothWorld(croc.TriggerTooth) + Vector3.up * 0.02f;
            Step(at + Vector3.up * 0.3f);
            Step(at);
            Step(at, true);
            Step(at, false);
            Assert.AreEqual(1, croc.Snaps, "the crocodile game works after the rest");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PartyEffectsBoundedAndAwayFromFace()
        {
            SellFirstCustomer();
            for (var i = 0; i < 200 && s.Market.Round.State != RoundState.Celebrating; i++) Step();
            // Where the player's head can be: the middle of the work area and every work position, seated to standing.
            var heads = new System.Collections.Generic.List<Vector3>();
            var spots = WareInfo.Categories.Select(c => MarketLayout.StandingSpot(c)).Append(MarketLayout.PlayStandingSpot).Append(Vector3.zero);
            foreach (var spot in spots)
                foreach (var h in new[] { 0.9f, 1.25f, 1.6f })
                    heads.Add(s.ToWorld(new Vector3(spot.x, h, spot.z)));
            var most = 0;
            for (var i = 0; i < 240 && s.Market.Round.State == RoundState.Celebrating; i++)
            {
                Step();
                most = Mathf.Max(most, s.Party.EffectCount);
                foreach (var e in s.Party.Effects)
                {
                    Assert.GreaterOrEqual(e.position.y, s.MarketRoot.position.y + 1.8f, "high above the market");
                    foreach (var head in heads)
                        Assert.Greater(Vector3.Distance(e.position, head), 0.8f, "never near a seated or standing child's face");
                }
            }
            Assert.Greater(most, 10);
            Assert.LessOrEqual(most, 120, "light enough for the Quest");
            yield return null;
        }

        static string Parent(string cmd, string id) => $"{{\"t\":\"parent\",\"cmd\":\"{cmd}\",\"cmdId\":\"{id}\"}}";

        [UnityTest]
        public IEnumerator Scene_PhoneParentPauseAndNextRound()
        {
            s.Market.Round.SetThreshold(80);
            for (var i = 0; i < 400 && s.Shop.State != SaleState.Ordering; i++) Step();
            var apple = s.CrateTop(Ware.Apple);
            Step(apple.Position);
            Step(apple.Position, true);
            Assert.AreSame(apple, s.Held(1), "The player is holding an apple");

            var ack = s.RemoteSession.Handle(Parent("rest", "cmd-scene0001")).Single();
            StringAssert.Contains("\"accepted\":true", ack, "received by the game");
            Assert.AreEqual(RemoteState.Rejected, s.Market.Remote.Request("order-0001abcd", new[] { "apple" }).State, "no new orders from that moment");
            Step(apple.Position, true);
            Assert.AreEqual(RoundState.Resting, s.Market.Round.State, "the same rest as the menu's, in the game's own step");
            Assert.IsNull(s.Held(1), "what was held went home, nothing kept in the hand");
            Assert.AreEqual(0, s.Market.Round.Stars, "no reward for a parent's pause");
            Assert.IsTrue(s.Party.ShowingRestPanel);
            StringAssert.Contains("resting", s.RemoteSession.PumpMarket() ?? "", "the phone learns the game is paused");

            s.RemoteSession.Handle(Parent("rest", "cmd-scene0001")); // resent
            s.RemoteSession.Handle(Parent("nextRound", "cmd-scene0002"));
            Step();
            Assert.AreEqual(RoundState.Playing, s.Market.Round.State, "the next round from the phone");
            s.RemoteSession.Handle(Parent("nextRound", "cmd-scene0002")); // resent
            s.RemoteSession.Handle(Parent("rest", "cmd-scene0001")); // and the old pause again
            for (var i = 0; i < 10; i++) Step();
            Assert.AreEqual(RoundState.Playing, s.Market.Round.State, "exactly once each");
            for (var i = 0; i < 400 && s.Shop.State != SaleState.Ordering; i++) Step();
            Assert.AreEqual(SaleState.Ordering, s.Shop.State, "customers come again");
            yield return null;
        }
    }
}
