// EPIC-012 in the real scene: a rest from the menu ends with "Fortsätt spela" in the same game; a parent's pause
// from the phone is ended only by the parent; the rest panel says why and what goes on.
using System.Collections;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class PauseSceneTests
    {
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x);
            Assert.IsNotNull(s);
        }

        void Frame(ShopFrame f)
        {
            f.DeltaTime = 0.02f;
            s.Step(f);
        }

        void Wait(float seconds) => SceneSteps.Wait(seconds, 0.05f, dt => SceneSteps.Step(s, null, false, dt));

        static string Parent(string cmd, string id) => $"{{\"t\":\"parent\",\"cmd\":\"{cmd}\",\"cmdId\":\"{id}\"}}";

        [UnityTest]
        public IEnumerator Scene_RestFromMenuEndsInTheSameGame()
        {
            for (var i = 0; i < 400 && s.Shop.State != SaleState.Ordering; i++) Wait(0.1f); // walks in first (EPIC-015)
            Frame(new ShopFrame { AddDirect = Ware.Apple });
            Wait(0.6f);
            Assert.IsTrue(s.RegisterCoin());
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + Shop.ArriveSeconds + 0.2f);
            var order = s.Shop.Order;
            var customer = s.Shop.CustomerNumber;
            var till = s.Wallet.Balance;
            var earned = s.Market.Round.Earned;

            Frame(new ShopFrame { TakeRest = true });
            Wait(0.5f);
            Assert.AreEqual(RoundState.Resting, s.Market.Round.State);
            Assert.IsTrue(s.Party.ShowingRestPanel);
            Assert.AreEqual("Vilopaus", s.Party.RestTitleText);
            StringAssert.Contains("Fortsätt spela", s.Party.RestHowText, "the panel says what goes on");

            Frame(new ShopFrame { ResumeRest = true });
            Wait(0.5f);
            Assert.AreEqual(RoundState.Playing, s.Market.Round.State, "no new game, no new round");
            Assert.IsFalse(s.Party.ShowingRestPanel);
            Assert.AreEqual(customer, s.Shop.CustomerNumber, "the same customer");
            Assert.AreEqual(order, s.Shop.Order, "with the same order");
            Assert.AreEqual(till, s.Wallet.Balance);
            Assert.AreEqual(earned, s.Market.Round.Earned);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ParentPauseFromPhoneOnlyParentEnds()
        {
            Wait(0.5f);
            s.RemoteSession.Handle(Parent("rest", "cmd-pause0001"));
            Wait(0.3f);
            Assert.AreEqual(RestCause.Parent, s.Market.Round.RestBy);
            Assert.AreEqual("Paus", s.Party.RestTitleText);
            StringAssert.Contains("En vuxen fortsätter", s.Party.RestHowText);
            StringAssert.Contains("\"restBy\":\"parent\"", s.RemoteSession.PumpMarket() ?? "", "the phone learns who paused");

            Frame(new ShopFrame { ResumeRest = true }); // the child's "Fortsätt spela" can't end it
            Wait(0.3f);
            Assert.AreEqual(RoundState.Resting, s.Market.Round.State);

            s.RemoteSession.Handle(Parent("resume", "cmd-pause0002"));
            Wait(0.3f);
            Assert.AreEqual(RoundState.Playing, s.Market.Round.State, "the parent continues it from the phone");
            s.RemoteSession.Handle(Parent("resume", "cmd-pause0002")); // resent: once only
            Assert.AreEqual(RoundState.Playing, s.Market.Round.State);

            Frame(new ShopFrame { ParentRest = true }); // the VR parent page
            Assert.AreEqual(RestCause.Parent, s.Market.Round.RestBy);
            Frame(new ShopFrame { ParentResume = true });
            Assert.AreEqual(RoundState.Playing, s.Market.Round.State, "and from the VR parent page");
            yield return null;
        }
    }
}
