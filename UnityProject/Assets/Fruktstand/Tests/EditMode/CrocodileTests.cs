// EPIC-006: the crocodile game's rules (Core.CrocodileGame), with an injected random source so every test is
// deterministic. Names match the [T: ...] references in docs/EPIC-006.md.
using System.Collections.Generic;
using Fruktstand.Core;
using NUnit.Framework;

namespace Fruktstand.Tests
{
    public class CrocodileTests
    {
        // Random source that hands out the given trigger teeth in order (and records what was asked).
        sealed class Script
        {
            readonly Queue<int> next;
            public readonly List<int> Asked = new();
            public Script(params int[] values) => next = new Queue<int>(values);
            public int Pick(int n)
            {
                Asked.Add(n);
                return next.Count > 0 ? next.Dequeue() : 0;
            }
        }

        static CrocodileGame Croc(params int[] triggers) => new(new Script(triggers).Pick);

        [Test]
        public void Croc_OneTriggerToothPerRound()
        {
            var script = new Script(5);
            var c = new CrocodileGame(script.Pick);
            Assert.AreEqual(8, CrocodileGame.Teeth);
            Assert.AreEqual(CrocState.Open, c.State, "the mouth starts open");
            Assert.AreEqual(5, c.TriggerTooth);
            CollectionAssert.AreEqual(new[] { 8 }, script.Asked, "one of the eight teeth, drawn once");
            for (var t = 0; t < 8; t++) Assert.IsFalse(c.IsPressed(t));
        }

        [Test]
        public void Croc_SafeToothPressedOnce()
        {
            var c = Croc(5);
            Assert.AreEqual(ToothPress.Safe, c.Press(0, 2));
            Assert.IsTrue(c.IsPressed(2), "a safe tooth goes down and stays down");
            Assert.IsFalse(c.CanPress(2));
            Assert.AreEqual(ToothPress.Ignored, c.Press(1, 2), "once per round, whichever hand");
            Assert.AreEqual(1, c.PressedCount);
            Assert.AreEqual(CrocState.Open, c.State, "a safe tooth never closes the mouth");
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, -1));
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 8), "no such tooth");
        }

        // Runs the clock until the mouth is open again (or the guard runs out).
        static void UntilOpen(CrocodileGame c)
        {
            for (var i = 0; i < 400 && c.State != CrocState.Open; i++) c.Tick(0.05f);
            Assert.AreEqual(CrocState.Open, c.State);
        }

        [Test]
        public void Croc_TriggerSnapsOnce()
        {
            var c = Croc(3, 6);
            Assert.AreEqual(ToothPress.Safe, c.Press(0, 0));
            Assert.AreEqual(ToothPress.Snap, c.Press(1, 3), "the trigger tooth snaps the mouth shut");
            Assert.AreEqual(CrocState.Closing, c.State);
            Assert.AreEqual(1, c.Snaps, "the round is over, counted once");
            Assert.AreEqual(ToothPress.Ignored, c.Press(1, 3), "a repeated event");
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 3));
            Assert.AreEqual(1, c.Snaps);
            Assert.AreEqual(1, c.Round);
        }

        [Test]
        public void Croc_PressesIgnoredWhileLocked()
        {
            var c = Croc(3, 6);
            c.Press(0, 3);
            var seen = new HashSet<CrocState>();
            for (var i = 0; i < 400 && c.State != CrocState.Open; i++)
            {
                seen.Add(c.State);
                for (var t = 0; t < 8; t++) Assert.AreEqual(ToothPress.Ignored, c.Press(0, t), $"{c.State}: tooth {t}");
                Assert.IsFalse(c.CanPress(0));
                c.Tick(0.05f);
            }
            CollectionAssert.IsSupersetOf(seen, new[] { CrocState.Closing, CrocState.Closed, CrocState.Opening });
            Assert.AreEqual(1, c.Snaps);
        }

        [Test]
        public void Croc_ReopensAndResets()
        {
            var c = Croc(3, 6);
            c.Press(0, 1);
            c.Press(0, 2);
            c.Press(0, 3);
            c.Tick(CrocodileGame.CloseSeconds + 0.01f);
            Assert.AreEqual(CrocState.Closed, c.State);
            c.Tick(CrocodileGame.ClosedSeconds - 0.1f);
            Assert.AreEqual(CrocState.Closed, c.State, "a short pause with the mouth shut");
            UntilOpen(c);
            for (var t = 0; t < 8; t++) Assert.IsFalse(c.IsPressed(t), $"tooth {t} is up again");
            Assert.AreEqual(0, c.PressedCount);
            Assert.AreEqual(6, c.TriggerTooth, "a new trigger tooth");
            Assert.AreEqual(2, c.Round);
            Assert.AreEqual(ToothPress.Safe, c.Press(0, 3), "the old trigger is just a tooth now");
        }

        [Test]
        public void Croc_NewTriggerEachRound()
        {
            var script = new Script(4, 4, 0);
            var c = new CrocodileGame(script.Pick);
            foreach (var expected in new[] { 4, 4, 0 })
            {
                Assert.AreEqual(expected, c.TriggerTooth, "drawn anew; the same tooth may come again");
                c.Press(0, c.TriggerTooth);
                UntilOpen(c);
            }
            Assert.AreEqual(4, script.Asked.Count, "one draw per round (3 rounds + the next one)");
            CollectionAssert.AreEqual(new[] { 8, 8, 8, 8 }, script.Asked);
        }

        // Snaps the mouth once (pressing the trigger) and runs the clock until the mouth is shut and paused.
        static void SnapAndWaitClosed(CrocodileGame c)
        {
            Assert.AreEqual(ToothPress.Snap, c.Press(0, c.TriggerTooth));
            c.Tick(CrocodileGame.CloseSeconds + 0.01f);
            c.Tick(CrocodileGame.ClosedSeconds + 0.01f);
        }

        [Test]
        public void Croc_DancesOnceAfterFiveSnaps()
        {
            var c = Croc();
            for (var round = 1; round <= 4; round++)
            {
                SnapAndWaitClosed(c);
                Assert.AreEqual(CrocState.Opening, c.State, $"snap {round}: no dance yet");
                UntilOpen(c);
            }
            SnapAndWaitClosed(c);
            Assert.AreEqual(5, c.Snaps);
            Assert.AreEqual(CrocState.Dancing, c.State, "the fifth snap: a dance (no hand in the mouth)");
            c.Tick(CrocodileGame.DanceSeconds - 0.1f);
            Assert.AreEqual(CrocState.Dancing, c.State, "2.5 s");
            Assert.IsFalse(c.Danced);
            c.Tick(0.2f);
            Assert.AreEqual(CrocState.Opening, c.State, "then it opens for the next round");
            Assert.IsTrue(c.Danced);
            UntilOpen(c);
            for (var round = 6; round <= 12; round++)
            {
                SnapAndWaitClosed(c);
                Assert.AreNotEqual(CrocState.Dancing, c.State, $"snap {round}: once per session");
                Assert.AreNotEqual(CrocState.WaitingToDance, c.State);
                UntilOpen(c);
            }
            Assert.AreEqual(12, c.Snaps);
        }

        [Test]
        public void Croc_NoDanceWhileHandInMouth()
        {
            var c = Croc();
            for (var round = 1; round <= 4; round++)
            {
                SnapAndWaitClosed(c);
                UntilOpen(c);
            }
            c.UpdateHand(1, 0, true); // the right hand is in the mouth area
            SnapAndWaitClosed(c);
            Assert.AreEqual(CrocState.WaitingToDance, c.State);
            for (var i = 0; i < 100; i++) c.Tick(0.1f);
            Assert.AreEqual(CrocState.WaitingToDance, c.State, "waits as long as a hand is in the mouth");
            c.UpdateHand(1, 0, false);
            c.Tick(0.01f);
            Assert.AreEqual(CrocState.Dancing, c.State, "starts once the hand has left");
            c.UpdateHand(0, 0, true); // a hand coming back during the dance does not stop it
            c.Tick(CrocodileGame.DanceSeconds + 0.01f);
            Assert.AreEqual(CrocState.Opening, c.State);
        }

        [Test]
        public void Croc_NewGameResetsSurprise()
        {
            var c = Croc();
            for (var round = 1; round <= 5; round++)
            {
                SnapAndWaitClosed(c);
                UntilOpen(c);
            }
            Assert.IsTrue(c.Danced);
            c.Press(0, (c.TriggerTooth + 1) % 8);
            c.NewGame();
            Assert.AreEqual(0, c.Snaps, "Nytt spel resets the count");
            Assert.IsFalse(c.Danced, "and the surprise");
            Assert.AreEqual(CrocState.Open, c.State);
            Assert.AreEqual(0, c.PressedCount, "a fresh round");
            for (var round = 1; round <= 5; round++)
            {
                SnapAndWaitClosed(c);
                if (round < 5) UntilOpen(c);
            }
            Assert.AreEqual(CrocState.Dancing, c.State, "the surprise can come again in the new session");

            var mid = Croc();
            mid.Press(0, mid.TriggerTooth);
            mid.NewGame();
            Assert.AreEqual(CrocState.Open, mid.State, "Nytt spel in the middle of a snap opens the mouth at once");
        }

        static int On(params int[] teeth)
        {
            var mask = 0;
            foreach (var t in teeth) mask |= 1 << t;
            return mask;
        }

        [Test]
        public void Croc_HandMustLeaveToothBeforeNextPress()
        {
            var c = Croc(7, 7);
            c.UpdateHand(0, On(1), true);
            Assert.AreEqual(ToothPress.Safe, c.Press(0, 1));
            c.UpdateHand(0, On(1, 2), true);
            Assert.AreEqual(ToothPress.Safe, c.Press(0, 2), "another tooth: fine, the hand left nothing it must leave");
            // The same hand on the trigger tooth after the round: still on it when the mouth reopens.
            c.UpdateHand(0, On(7), true);
            Assert.AreEqual(ToothPress.Snap, c.Press(0, 7));
            UntilOpen(c);
            c.UpdateHand(0, On(7), true);
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 7), "a hand left on a tooth never presses it again by itself");
            c.UpdateHand(0, 0, true);
            c.UpdateHand(0, On(7), true);
            Assert.AreEqual(ToothPress.Snap, c.Press(0, 7), "after leaving the tooth's area it works again");
        }

        [Test]
        public void Croc_HandOnToothWhenReopenedMustLeaveFirst()
        {
            var c = Croc(0, 0);
            c.Press(1, 0);
            for (var i = 0; i < 400 && c.State != CrocState.Open; i++)
            {
                c.UpdateHand(0, On(3, 4), true); // the other hand rests on teeth 3 and 4 while the mouth reopens
                c.Tick(0.05f);
            }
            Assert.AreEqual(CrocState.Open, c.State);
            c.UpdateHand(0, On(3, 4), true);
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 3), "it was already there when the mouth opened");
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 4));
            Assert.AreEqual(ToothPress.Safe, c.Press(1, 3), "the other hand is free to press it");
            c.UpdateHand(0, On(4), true);
            c.UpdateHand(0, On(4, 5), true);
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 4), "still hasn't left tooth 4");
            Assert.AreEqual(ToothPress.Safe, c.Press(0, 5), "tooth 5 was not under the hand at the reopening");
        }

        [Test]
        public void Croc_PausedBlocksPressesAndTimer()
        {
            var c = Croc(2, 6);
            c.SetPaused(true);
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 1), "system menu / focus lost: no presses");
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 2));
            Assert.IsFalse(c.CanPress(1));
            c.SetPaused(false);
            Assert.AreEqual(ToothPress.Snap, c.Press(0, 2));
            c.SetPaused(true);
            for (var i = 0; i < 200; i++) c.Tick(0.1f);
            Assert.AreEqual(CrocState.Closing, c.State, "the timer stands still while paused");
            Assert.AreEqual(0, c.StateTime, 1e-6);
            c.SetPaused(false);
            UntilOpen(c);
            Assert.AreEqual(6, c.TriggerTooth);
        }

        [Test]
        public void Croc_ResumeRequiresLeavingTooth()
        {
            var c = Croc(2);
            c.UpdateHand(1, On(5), true);
            c.SetPaused(true);
            c.UpdateHand(1, 0, false); // hands are hidden while paused
            c.UpdateHand(1, On(5), true); // back: the hand is still resting on tooth 5
            c.SetPaused(false);
            Assert.AreEqual(ToothPress.Ignored, c.Press(1, 5), "a hand left on a tooth through the pause does not press it");
            c.UpdateHand(1, 0, true);
            c.UpdateHand(1, On(5), true);
            Assert.AreEqual(ToothPress.Safe, c.Press(1, 5), "a fresh approach works");
            Assert.AreEqual(ToothPress.Ignored, c.Press(0, 9));
        }

        [Test]
        public void Croc_PartyDanceDoesNotCountAsSurprise()
        {
            var c = Croc(3, 6);
            c.Press(0, 1);
            Assert.IsTrue(c.StartPartyDance(), "the market party: the crocodile dances along");
            Assert.AreEqual(CrocState.Dancing, c.State);
            Assert.IsTrue(c.PartyDancing);
            Assert.AreEqual(ToothPress.Ignored, c.Press(1, 2), "no presses while dancing");
            c.Tick(CrocodileGame.DanceSeconds + 0.01f);
            Assert.IsFalse(c.Danced, "the party dance is not the hidden surprise");
            Assert.AreEqual(0, c.Snaps, "and no snap");
            UntilOpen(c);
            Assert.IsFalse(c.PartyDancing);
            Assert.AreEqual(0, c.PressedCount, "a fresh round after it");

            // Its own surprise still comes after five snaps; the party never interrupts that dance.
            for (var round = 1; round <= 5; round++)
            {
                SnapAndWaitClosed(c);
                if (round < 5) UntilOpen(c);
            }
            Assert.AreEqual(CrocState.Dancing, c.State);
            Assert.IsFalse(c.PartyDancing);
            Assert.IsTrue(c.OwnDanceBusy, "its own dance is on");
            Assert.IsFalse(c.StartPartyDance(), "the party waits instead of competing with it");
            c.Tick(CrocodileGame.DanceSeconds + 0.01f);
            Assert.IsTrue(c.Danced);
            Assert.IsFalse(c.OwnDanceBusy);
        }
    }
}
