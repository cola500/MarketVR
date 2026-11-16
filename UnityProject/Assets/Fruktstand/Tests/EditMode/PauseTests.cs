// EPIC-012: pauses that can be ended. A child's rest, a parent's pause and the rest after the party are told apart
// (MarketRound.RestBy). Names match the [T: ...] references in docs/EPIC-012.md.
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;

namespace Fruktstand.Tests
{
    public class PauseTests
    {
        static void UntilOrdering(Market m)
        {
            for (var i = 0; i < 400 && m.Shop.State != SaleState.Ordering; i++) m.Tick(0.25f);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State);
        }

        static void Pay(Market m)
        {
            UntilOrdering(m);
            foreach (var w in WareInfo.All)
                for (var i = 0; i < m.Shop.Order.Count(w); i++) Assert.GreaterOrEqual(m.Shop.TryAddToBasket(w), 0);
            Assert.IsTrue(m.Shop.TryPay());
            m.Tick(Shop.ThankSeconds);
            m.Tick(Shop.LeaveSeconds);
        }

        // Everything a plain pause must keep.
        static string Snapshot(Market m) =>
            $"{m.Shop.State}|{m.Shop.CustomerNumber}|{m.Shop.Order}|{m.Shop.Basket.Total}|{m.Wallet.Balance}|{m.Round.Earned}|{m.Round.Stars}|" +
            $"{m.Assortment.Revision}|{string.Join(",", WareInfo.Categories.SelectMany(c => m.Assortment.Active(c)))}|" +
            $"{m.PlayArea.State(Ware.Ball)}|{m.PlayArea.Children}";

        [Test]
        public void Pause_ChildRestResumesSameGame()
        {
            var m = new Market(assortment: new Assortment(4));
            Pay(m);
            UntilOrdering(m);
            var ware = m.Assortment.Active(m.Shop.Order.Category).First(w => m.Shop.Order.Count(w) > 0);
            Assert.GreaterOrEqual(m.Shop.TryAddToBasket(ware), 0); // an order half done
            Assert.IsTrue(m.PlayArea.TryReserve(Ware.Ball, 9));
            var before = Snapshot(m);

            Assert.IsTrue(m.TakeRest(RestCause.Child), "Ta vilopaus from the menu");
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            Assert.AreEqual(RestCause.Child, m.Round.RestBy);
            for (var i = 0; i < 40; i++) m.Tick(0.5f, presentationReady: true);

            Assert.IsTrue(m.Resume(RestCause.Child), "Fortsätt spela: no new round, no new game");
            Assert.AreEqual(RoundState.Playing, m.Round.State);
            Assert.AreEqual(RestCause.None, m.Round.RestBy);
            Assert.AreEqual(before, Snapshot(m), "the same order, basket, till, earnings, wares and play");
            for (var i = 0; i < 4; i++) m.Tick(0.5f);
            Assert.AreEqual(before.Split('|')[8], Snapshot(m).Split('|')[8], "a plain pause never changes the wares");
        }

        [Test]
        public void Pause_ParentRestNeedsParent()
        {
            var m = new Market();
            UntilOrdering(m);
            var before = Snapshot(m);
            Assert.IsTrue(m.TakeRest(RestCause.Parent));
            Assert.IsFalse(m.Resume(RestCause.Child), "the child can't end the parent's pause");
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            Assert.IsTrue(m.Resume(RestCause.Parent));
            Assert.AreEqual(before, Snapshot(m));

            // The child rests, then the parent pauses too: it is the parent's pause now.
            Assert.IsTrue(m.TakeRest(RestCause.Child));
            Assert.IsTrue(m.TakeRest(RestCause.Parent));
            Assert.AreEqual(RestCause.Parent, m.Round.RestBy);
            Assert.IsFalse(m.Resume(RestCause.Child));
            Assert.IsTrue(m.Resume(RestCause.Parent));
            // The parent may also end the child's own rest.
            Assert.IsTrue(m.TakeRest(RestCause.Child));
            Assert.IsTrue(m.Resume(RestCause.Parent));
            Assert.AreEqual(RoundState.Playing, m.Round.State);
            Assert.IsFalse(m.Resume(RestCause.Parent), "nothing to end while playing");
        }

        [Test]
        public void Pause_PartyRestOnlyNextRound()
        {
            var m = new Market(threshold: 1, assortment: new Assortment(6));
            Pay(m); // the party
            for (var i = 0; i < 60 && m.Round.State != RoundState.Resting; i++) m.Tick(0.5f, presentationReady: true);
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            Assert.AreEqual(RestCause.Party, m.Round.RestBy);
            var till = m.Wallet.Balance;
            var stars = m.Round.Stars;
            Assert.IsFalse(m.Resume(RestCause.Child), "the round is over: no plain continue");
            Assert.IsFalse(m.Resume(RestCause.Parent));
            Assert.IsFalse(m.TakeRest(RestCause.Parent), "already resting");
            var wares = m.Assortment.Revision;
            Assert.IsTrue(m.StartNextRound());
            Assert.AreEqual(RoundState.Playing, m.Round.State);
            Assert.AreEqual(0, m.Round.Earned, "a new round");
            Assert.AreEqual(till, m.Wallet.Balance, "the till is kept");
            Assert.AreEqual(stars, m.Round.Stars, "and the stars");
            UntilOrdering(m);
            Assert.AreNotEqual(wares, m.Assortment.Revision, "new wares for the new round");
        }

        [Test]
        public void Menu_RestItemsFollowTheCause()
        {
            var menu = new GameMenu { BuildLine = "" };
            menu.RestBy = RestCause.None;
            menu.Open();
            var playing = string.Join("\n", menu.Lines());
            StringAssert.Contains("Ta vilopaus", playing);
            StringAssert.DoesNotContain("Fortsätt spela", playing);

            menu.RestBy = RestCause.Child;
            menu.Open();
            var resting = string.Join("\n", menu.Lines());
            StringAssert.DoesNotContain("Ta vilopaus", resting);
            StringAssert.Contains("Fortsätt spela", resting);
            Assert.AreEqual(MenuAction.ResumeRest, menu.Select(), "the first item ends the child's rest");
            Assert.IsFalse(menu.IsOpen);

            menu.RestBy = RestCause.Parent;
            menu.Open();
            var parent = string.Join("\n", menu.Lines());
            StringAssert.Contains("Föräldern har pausat", parent);
            StringAssert.DoesNotContain("Fortsätt spela", parent);
            Assert.AreEqual(MenuAction.Resume, menu.Select(), "the child's first item only closes the menu");

            menu.RestBy = RestCause.Party;
            menu.Open();
            StringAssert.Contains("En vuxen startar nästa omgång", string.Join("\n", menu.Lines()));

            // The parent page: pause, continue and next round, as the cause allows.
            menu.RestBy = RestCause.None;
            menu.OpenParent();
            StringAssert.Contains("Pausa spelet", string.Join("\n", menu.Lines()));
            Assert.AreEqual(MenuAction.ParentRest, menu.Select());
            menu.RestBy = RestCause.Parent;
            menu.OpenParent();
            var parentPage = string.Join("\n", menu.Lines());
            StringAssert.Contains("Fortsätt spelet", parentPage);
            StringAssert.Contains("Starta nästa omgång", parentPage);
            Assert.AreEqual(MenuAction.ParentResume, menu.Select());
            menu.RestBy = RestCause.Party;
            menu.OpenParent();
            StringAssert.DoesNotContain("Fortsätt spelet", string.Join("\n", menu.Lines()), "after the party only the next round");
        }

        [Test]
        public void Session_ParentResume()
        {
            var m = new Market();
            var s = new RemoteSession(m.Shop, new PhotoStore())
            {
                OnParentCommand = cmd => cmd switch
                {
                    "rest" => m.TakeRest(RestCause.Parent),
                    "resume" => m.Resume(RestCause.Parent),
                    "nextRound" => m.StartNextRound(),
                    _ => false,
                },
            };
            s.PumpMarket();
            StringAssert.Contains("\"accepted\":true", s.Handle("{\"t\":\"parent\",\"cmd\":\"rest\",\"cmdId\":\"cmd-res00001\"}").Single());
            var paused = s.PumpMarket();
            StringAssert.Contains("\"state\":\"resting\"", paused);
            StringAssert.Contains("\"restBy\":\"parent\"", paused);
            StringAssert.Contains("\"accepted\":true", s.Handle("{\"t\":\"parent\",\"cmd\":\"resume\",\"cmdId\":\"cmd-res00002\"}").Single());
            var playing = s.PumpMarket();
            StringAssert.Contains("\"state\":\"playing\"", playing);
            StringAssert.Contains("\"restBy\":\"\"", playing);
            Assert.AreEqual(RoundState.Playing, m.Round.State);
        }
    }
}
