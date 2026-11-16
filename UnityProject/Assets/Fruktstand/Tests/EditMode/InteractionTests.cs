// Grabbing, drop zones, router (focus / menu / neutral) and menu. Pure logic; the scene tests cover the wiring.
using System.Collections.Generic;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class InteractionTests
    {
        sealed class Thing : IGrabbable
        {
            public Vector3 Position { get; set; }
            public float Radius { get; set; } = 0.04f;
            public bool CanGrab { get; set; } = true;
        }

        static HandFrame Hand(Vector3 at, bool grab, bool tracked = true) => new() { Tracked = tracked, Position = at, Rotation = Quaternion.identity, Grab = grab };

        [Test]
        public void Grab_PicksNearestItemWithinReach()
        {
            var near = new Thing { Position = new Vector3(0.1f, 0, 0) };
            var nearer = new Thing { Position = new Vector3(0.06f, 0, 0) };
            var far = new Thing { Position = new Vector3(0.5f, 0, 0) };
            var g = new Grabber();
            var items = new List<IGrabbable> { near, nearer, far };
            g.Update(Hand(Vector3.zero, false), items, null);
            Assert.AreSame(nearer, g.Hover, "nearest is highlighted");
            Assert.AreEqual(GrabChange.Grabbed, g.Update(Hand(Vector3.zero, true), items, null));
            Assert.AreSame(nearer, g.Held);
            Assert.AreEqual(GrabChange.None, g.Update(Hand(Vector3.one, true), items, null), "kept while held");
            Assert.AreEqual(GrabChange.Released, g.Update(Hand(Vector3.one, false), items, null));
            Assert.IsNull(g.Held);

            var g2 = new Grabber();
            g2.Update(Hand(new Vector3(0.3f, 0, 0), false), new List<IGrabbable> { far }, null);
            Assert.IsNull(g2.Hover, "0.16 m from the surface is out of reach");
            Assert.AreEqual(GrabChange.None, g2.Update(Hand(new Vector3(0.3f, 0, 0), true), new List<IGrabbable> { far }, null));
            var locked = new Thing { Position = Vector3.zero, CanGrab = false };
            Assert.IsNull(Grabber.Nearest(Vector3.zero, new List<IGrabbable> { locked }), "locked items are not offered");
        }

        [Test]
        public void Grab_HeldButtonDoesNotGrabOnContact()
        {
            var item = new Thing { Position = new Vector3(1, 0, 0) };
            var items = new List<IGrabbable> { item };
            var g = new Grabber();
            g.Update(Hand(Vector3.zero, true), items, null); // pressed far away
            Assert.AreEqual(GrabChange.None, g.Update(Hand(new Vector3(1, 0, 0), true), items, null), "sweeping over it with the button held");
            Assert.IsNull(g.Held);
            g.Update(Hand(new Vector3(1, 0, 0), false), items, null);
            Assert.AreEqual(GrabChange.Grabbed, g.Update(Hand(new Vector3(1, 0, 0), true), items, null), "a fresh press grabs");

            var fresh = new Grabber();
            Assert.AreEqual(GrabChange.None, fresh.Update(Hand(new Vector3(1, 0, 0), true), new List<IGrabbable> { new Thing { Position = new Vector3(1, 0, 0) } }, null),
                "a button already held when the grabber starts is not a press");
        }

        [Test]
        public void Grab_TwoHandsHoldTwoItems()
        {
            var a = new Thing { Position = Vector3.zero };
            var b = new Thing { Position = new Vector3(0.05f, 0, 0) };
            var items = new List<IGrabbable> { a, b };
            var left = new Grabber();
            var right = new Grabber();
            left.Update(Hand(Vector3.zero, false), items, null);
            right.Update(Hand(Vector3.zero, false), items, null);
            Assert.AreEqual(GrabChange.Grabbed, left.Update(Hand(Vector3.zero, true), items, right.Held));
            Assert.AreSame(a, left.Held);
            Assert.AreEqual(GrabChange.Grabbed, right.Update(Hand(Vector3.zero, true), items, left.Held), "the other hand can't take the same item");
            Assert.AreSame(b, right.Held);
        }

        [Test]
        public void Grab_LostTrackingWhileHolding()
        {
            var item = new Thing { Position = Vector3.zero };
            var g = new Grabber();
            var items = new List<IGrabbable> { item };
            g.Update(Hand(Vector3.zero, false), items, null);
            g.Update(Hand(Vector3.zero, true), items, null);
            Assert.AreEqual(GrabChange.Lost, g.Update(Hand(Vector3.zero, true, tracked: false), items, null), "lost, not released into a zone");
            Assert.IsNull(g.Held);

            g.Update(Hand(Vector3.zero, false), items, null);
            g.Update(Hand(Vector3.zero, true), items, null);
            Assert.AreSame(item, g.ForceRelease(), "ForceRelease hands back what was held");
            Assert.IsNull(g.Held);
            Assert.AreEqual(GrabChange.None, g.Update(Hand(Vector3.zero, true), items, null), "button still held after a forced release: no grab");
        }

        [Test]
        public void Drop_NearBasketSnapsToFreeSlot()
        {
            var basket = StandView.BasketZone;
            Assert.AreEqual(DropTarget.Basket, DropRules.Resolve(false, StandView.BasketCentre + Vector3.up * 0.15f, basket, StandView.RegisterZone), "above the basket");
            Assert.AreEqual(DropTarget.Basket, DropRules.Resolve(false, StandView.BasketCentre + new Vector3(0.15f, 0.05f, 0), basket, StandView.RegisterZone), "near the edge");
            Assert.AreEqual(2, DropRules.NearestSlot(StandView.Slots[2] + new Vector3(0.01f, 0.1f, 0.01f), StandView.Slots));
        }

        [Test]
        public void Drop_ElsewhereReturnsHome()
        {
            Assert.AreEqual(DropTarget.Home, DropRules.Resolve(false, StandView.BasketCentre + new Vector3(0.3f, 0, 0), StandView.BasketZone, StandView.RegisterZone), "beside");
            Assert.AreEqual(DropTarget.Home, DropRules.Resolve(false, StandView.BasketCentre + Vector3.up * 0.6f, StandView.BasketZone, StandView.RegisterZone), "high above");
            Assert.AreEqual(DropTarget.Home, DropRules.Resolve(false, StandView.BasketCentre + Vector3.down * 0.3f, StandView.BasketZone, StandView.RegisterZone), "under the counter");
            Assert.AreEqual(DropTarget.Home, DropRules.Resolve(false, StandView.RegisterCentre + Vector3.up * 0.1f, StandView.BasketZone, StandView.RegisterZone), "produce in the register");
            foreach (var p in WareInfo.All)
                for (var bin = 0; bin < 3; bin++)
                    Assert.AreEqual(DropTarget.Home, DropRules.Resolve(false, StandView.CrateTop(bin, p), StandView.BasketZone, StandView.RegisterZone), $"released right at the {p} crate {bin}");
        }

        [Test]
        public void Drop_CoinElsewhereReturnsToTray()
        {
            Assert.AreEqual(DropTarget.Register, DropRules.Resolve(true, StandView.RegisterCentre + Vector3.up * 0.14f, StandView.BasketZone, StandView.RegisterZone));
            Assert.AreEqual(DropTarget.Home, DropRules.Resolve(true, StandView.CoinSpot, StandView.BasketZone, StandView.RegisterZone), "released where it lies: no accidental payment");
            Assert.AreEqual(DropTarget.Home, DropRules.Resolve(true, StandView.BasketCentre + Vector3.up * 0.1f, StandView.BasketZone, StandView.RegisterZone), "coin in the basket");
        }

        // ---- Router: focus, pause, presence, menu, neutral requirement ----

        static XrState Idle() => new()
        {
            Left = new XrHandState { Tracked = true, Position = new Vector3(-0.2f, 1, 0.3f), Rotation = Quaternion.identity },
            Right = new XrHandState { Tracked = true, Position = new Vector3(0.2f, 1, 0.3f), Rotation = Quaternion.identity },
        };

        static XrState With(System.Func<XrState, XrState> f) => f(Idle());

        [Test]
        public void Router_NoFocusBlocksEverything()
        {
            foreach (var block in new System.Action<XrInputRouter>[] { r => r.SetFocus(false), r => r.SetPaused(true), r => r.SetPresent(false) })
            {
                var r = new XrInputRouter();
                block(r);
                var s = With(x => { x.Right.Trigger = true; x.Left.Grip = true; x.Right.Stick = new Vector2(0, 1); x.Left.Stick = new Vector2(0, 1); x.Left.Menu = true; return x; });
                var f = r.Route(s, 0.1f, out var action);
                Assert.IsFalse(f.Left.Grab || f.Right.Grab, "no grab");
                Assert.IsFalse(f.Left.Tracked || f.Right.Tracked, "hands hidden");
                Assert.AreEqual(0, f.CounterHeightDelta, "no counter move");
                Assert.AreEqual(Vector2.zero, f.Walk, "no walking");
                Assert.AreEqual(0, f.Turn, "no turning");
                Assert.IsFalse(r.Menu.IsOpen, "not even the menu button");
                Assert.AreEqual(MenuAction.None, action);
                Assert.IsTrue(f.ReleaseAll);
            }
        }

        [Test]
        public void Router_FocusLossReleasesHeldItems()
        {
            var r = new XrInputRouter();
            var holding = With(x => { x.Right.Grip = true; return x; });
            Assert.IsTrue(r.Route(holding, 0.01f, out _).Right.Grab);
            r.SetFocus(false);
            var f = r.Route(holding, 0.01f, out _);
            Assert.IsTrue(f.ReleaseAll, "held item goes home (not dropped where the hand is)");
            Assert.IsFalse(f.Right.Grab);
        }

        [Test]
        public void Router_HeldButtonAfterFocusDoesNothingUntilReleased()
        {
            var r = new XrInputRouter();
            r.Route(Idle(), 0.01f, out _);
            r.SetFocus(false);
            var held = With(x => { x.Right.Trigger = true; return x; });
            r.Route(held, 0.01f, out _);
            r.SetFocus(true);
            Assert.IsFalse(r.Route(held, 0.01f, out _).Right.Grab, "trigger held through the system menu");
            Assert.IsTrue(r.WaitingForNeutral);
            var stick = With(x => { x.Left.Stick = new Vector2(0, 0.9f); x.Right.Stick = new Vector2(0, 0.9f); return x; });
            var pushed = r.Route(stick, 0.1f, out _);
            Assert.AreEqual(0, pushed.CounterHeightDelta, "stick still pushed: no counter move");
            Assert.AreEqual(Vector2.zero, pushed.Walk, "nor walking");
            r.Route(Idle(), 0.01f, out _);
            Assert.IsFalse(r.WaitingForNeutral);
            Assert.IsTrue(r.Route(held, 0.01f, out _).Right.Grab, "a fresh press works");

            // same after pause and after taking the headset off
            foreach (var flip in new System.Action<XrInputRouter, bool>[] { (x, v) => x.SetPaused(!v), (x, v) => x.SetPresent(v) })
            {
                var q = new XrInputRouter();
                flip(q, false);
                flip(q, true);
                Assert.IsFalse(q.Route(held, 0.01f, out _).Right.Grab);
                q.Route(Idle(), 0.01f, out _);
                Assert.IsTrue(q.Route(held, 0.01f, out _).Right.Grab);
            }
        }

        [Test]
        public void Router_ClosingMenuWithTriggerDoesNotGrab()
        {
            var r = new XrInputRouter();
            r.Route(Idle(), 0.01f, out _);
            var f = r.Route(With(x => { x.Left.Menu = true; return x; }), 0.01f, out _);
            Assert.IsTrue(r.Menu.IsOpen);
            Assert.IsTrue(f.ReleaseAll, "opening the menu sends held items home");
            r.Route(Idle(), 0.01f, out _);
            var trigger = With(x => { x.Right.Trigger = true; return x; });
            f = r.Route(trigger, 0.01f, out var action); // "Fortsätt" is preselected
            Assert.AreEqual(MenuAction.Resume, action);
            Assert.IsFalse(r.Menu.IsOpen);
            Assert.IsFalse(f.Right.Grab, "the trigger that chose Fortsätt does not grab");
            Assert.IsFalse(r.Route(trigger, 0.01f, out _).Right.Grab, "still held: nothing");
            r.Route(Idle(), 0.01f, out _);
            Assert.IsTrue(r.Route(trigger, 0.01f, out _).Right.Grab);
        }

        [Test]
        public void Router_MenuPressedThroughFocusLossDoesNotOpen()
        {
            var r = new XrInputRouter();
            r.SetFocus(false);
            var menu = With(x => { x.Left.Menu = true; return x; });
            r.Route(menu, 0.01f, out _);
            r.SetFocus(true);
            r.Route(menu, 0.01f, out _);
            Assert.IsFalse(r.Menu.IsOpen);
        }

        [Test]
        public void Menu_NewGameNeedsConfirmationNoIsDefault()
        {
            var m = new GameMenu();
            m.Open();
            m.Navigate(-1); // Nytt spel is the last item
            Assert.AreEqual(MenuAction.None, m.Select(), "Nytt spel opens the confirmation");
            Assert.IsTrue(m.Confirming);
            StringAssert.Contains("Kassan töms", string.Join("\n", m.Lines()));
            Assert.AreEqual(MenuAction.None, m.Select(), "Nej is preselected");
            Assert.IsTrue(m.IsOpen);
            Assert.IsFalse(m.Confirming);
            m.Select(); // Nytt spel again (selection kept on it)
            m.Navigate(1);
            Assert.AreEqual(MenuAction.NewGame, m.Select());
            Assert.IsFalse(m.IsOpen);
        }

        [Test]
        public void Menu_ToggleOpenAndClearBasketAreSeparateFromNewGame()
        {
            var m = new GameMenu { StandOpen = true };
            m.Open();
            CollectionAssert.Contains(m.Lines(), "   Stäng marknaden");
            m.Navigate(2);
            Assert.AreEqual(MenuAction.ToggleOpen, m.Select(), "no confirmation needed: nothing is lost");
            m.StandOpen = false;
            m.Open();
            CollectionAssert.Contains(m.Lines(), "   Öppna marknaden", "label follows the market");
            m.Navigate(3);
            Assert.AreEqual(MenuAction.ClearBasket, m.Select());
            Assert.IsFalse(m.IsOpen);
        }

        static void Go(GameMenu m, string label)
        {
            var rows = m.Lines().Where(l => l.StartsWith("> ") || l.StartsWith("   ")).Select(l => l.Substring(2).Trim()).ToList();
            var at = rows.IndexOf(label);
            Assert.GreaterOrEqual(at, 0, $"'{label}' is in the menu");
            m.Navigate(at - m.Selected);
        }

        [Test]
        public void Menu_ParentPageNextRoundNeedsConfirmation()
        {
            var m = new GameMenu { ParentLines = new[] { "Omgången: 34 av 80 pengar" } };
            m.Open();
            Go(m, "Ta vilopaus");
            Assert.AreEqual(MenuAction.TakeRest, m.Select(), "a rest from the ordinary menu, any time");
            Assert.IsFalse(m.IsOpen);

            m.Open();
            Go(m, "För föräldrar");
            Assert.AreEqual(MenuAction.None, m.Select());
            Assert.IsTrue(m.ShowingParent);
            StringAssert.Contains("34 av 80", string.Join("\n", m.Lines()), "the round's numbers for the parent");
            Go(m, "Starta nästa omgång (bara i vilopaus)");
            Assert.AreEqual(MenuAction.None, m.Select(), "only during a rest");

            m.Resting = true;
            Go(m, "Starta nästa omgång");
            Assert.AreEqual(MenuAction.None, m.Select(), "opens the confirmation");
            Assert.IsTrue(m.ConfirmingNextRound);
            Assert.AreEqual(MenuAction.None, m.Select(), "Nej is preselected");
            Assert.IsTrue(m.ShowingParent, "back on the parent page");
            Go(m, "Starta nästa omgång");
            m.Select();
            m.Navigate(1);
            Assert.AreEqual(MenuAction.NextRound, m.Select(), "Ja");
            Assert.IsFalse(m.IsOpen);
            Assert.IsFalse(m.Lines().Any(), "a closed menu shows nothing (a trigger in the rest does nothing)");
            Assert.AreEqual(MenuAction.None, new GameMenu().Select(), "a closed menu never starts a round");
        }

        [Test]
        public void Menu_ShowsBuildId()
        {
            var m = new GameMenu();
            m.Open();
            CollectionAssert.Contains(m.Lines(), "Bygge editor", "editor without baked build info");
            m.BuildLine = "Bygge 2026-11-01_143052_abc1234-dirty  (lokala ändringar)";
            CollectionAssert.Contains(m.Lines(), m.BuildLine, "the installed build can be identified from the menu");
            m.Navigate(-1);
            Assert.AreEqual(MenuAction.None, m.Select(), "the build line is not a menu item: the last item is still Nytt spel");
            Assert.IsTrue(m.Confirming);
        }

        [Test]
        public void Menu_PlaceStandsInFront()
        {
            var m = new GameMenu();
            m.Open();
            CollectionAssert.Contains(m.Lines(), "   Flytta marknaden hit");
            m.Navigate(5);
            Assert.AreEqual(MenuAction.PlaceInFront, m.Select());
            Assert.IsFalse(m.IsOpen);
        }

        [Test]
        public void Menu_MarketItems()
        {
            var m = new GameMenu();
            m.Open();
            var lines = string.Join("\n", m.Lines());
            foreach (var item in new[] { "Fortsätt", "Visa kontroller", "Stäng marknaden", "Återställ beställningen", "Städa lekytan", "Flytta marknaden hit", "Nytt spel" })
                StringAssert.Contains(item, lines);
            m.Navigate(3);
            Assert.AreEqual(MenuAction.ClearBasket, m.Select(), "Återställ beställningen: no confirmation, nothing is lost");
            m.Open();
            m.Navigate(4);
            Assert.AreEqual(MenuAction.TidyPlay, m.Select(), "Städa lekytan: separate from Nytt spel");
        }

        [Test]
        public void Placement_WaitsUntilWornThenOnlyOnRequest()
        {
            var p = new StandPlacement();
            Assert.IsFalse(p.ShouldPlace(hasPose: true, worn: false), "headset lying on the table: wait");
            Assert.IsFalse(p.ShouldPlace(hasPose: false, worn: true), "no head pose yet: wait");
            Assert.IsTrue(p.ShouldPlace(true, true), "first time it is worn");
            Assert.IsFalse(p.ShouldPlace(true, true), "never moves by itself during play");
            p.Request(); // system recenter or the menu choice
            Assert.IsFalse(p.ShouldPlace(true, false), "not while the headset is off");
            Assert.IsTrue(p.ShouldPlace(true, true));
            Assert.IsFalse(p.ShouldPlace(true, true), "once per request");

            var unknown = new StandPlacement();
            Assert.IsTrue(unknown.ShouldPlace(true, null), "no presence sensor reported: place at once");
        }

        // Compact market (EPIC-003 S4K.6): walking about, or taking the headset off and on anywhere, never moves the
        // market; only start, recenter and the menu do.
        [Test]
        public void Placement_NeverFollowsPhysicalMovement()
        {
            var p = new StandPlacement();
            Assert.IsTrue(p.ShouldPlace(true, true));
            for (var i = 0; i < 100; i++) Assert.IsFalse(p.ShouldPlace(true, true, 5f), "walking about");
            Assert.IsFalse(p.ShouldPlace(true, false), "headset off");
            Assert.IsFalse(p.ShouldPlace(true, true, 0f), "headset on again, wherever");
            p.Request();
            Assert.IsTrue(p.ShouldPlace(true, true), "only an explicit request places it again");
        }

        [Test]
        public void Touch_FlipsOncePerPassFromOutside()
        {
            var t = new TouchSwitch();
            Assert.IsFalse(t.Update(1, true, true, 0.02f, true), "a hand that appears inside never flips it");
            Assert.IsFalse(t.Update(1, true, true, 0.3f, true));
            Assert.IsTrue(t.Update(1, true, true, 0.05f, true), "coming in from outside flips it");
            t.Tick(1);
            Assert.IsFalse(t.Update(1, true, true, 0.03f, true), "staying inside: once per pass");
            Assert.IsFalse(t.Update(1, true, true, 0.09f, true), "between Enter and Exit: not re-armed");
            Assert.IsFalse(t.Update(1, true, true, 0.05f, true));
            Assert.IsFalse(t.Update(1, true, true, 0.2f, true));
            Assert.IsTrue(t.Update(1, true, true, 0.05f, true), "out and in again flips again");
        }

        [Test]
        public void Touch_IgnoresFullHandsDisabledAndQuickDoubleSwipes()
        {
            var t = new TouchSwitch();
            t.Update(1, true, false, 0.3f, true);
            Assert.IsFalse(t.Update(1, true, false, 0.05f, true), "carrying something");
            t.Update(1, true, true, 0.3f, true);
            Assert.IsFalse(t.Update(1, true, true, 0.05f, enabled: false), "menu open / no focus");
            Assert.IsFalse(t.Update(1, true, true, 0.05f, true), "disabled disarms: must come from outside again");
            t.Update(0, true, true, 0.3f, true);
            t.Update(1, true, true, 0.3f, true);
            Assert.IsTrue(t.Update(1, true, true, 0.05f, true));
            Assert.IsFalse(t.Update(0, true, true, 0.05f, true), "the other hand straight after: cooldown");
            t.Tick(TouchSwitch.Cooldown);
            t.Update(0, true, true, 0.3f, true);
            Assert.IsTrue(t.Update(0, true, true, 0.05f, true));
            Assert.IsFalse(t.Update(0, false, true, 0.05f, true), "untracked");
        }

        [Test]
        public void Placement_FirstWaitsForTheHeadsetToSettleRecenterDoesNot()
        {
            var p = new StandPlacement();
            Assert.IsFalse(p.ShouldPlace(true, true, wornSeconds: 0), "presence just turned true: maybe still in the hands");
            Assert.IsFalse(p.ShouldPlace(true, true, StandPlacement.SettleSeconds * 0.5f));
            Assert.IsTrue(p.ShouldPlace(true, true, StandPlacement.SettleSeconds), "worn for a second: place");
            p.Request();
            Assert.IsTrue(p.ShouldPlace(true, true, 0.1f), "a recenter is immediate (and sets the height again)");
            Assert.IsTrue(new StandPlacement().ShouldPlace(true, null, 0), "no presence sensor: no settle needed");
        }

        [Test]
        public void Placement_RecenterDetectedFromHeadPoseJump()
        {
            var at = new Vector3(0.1f, 1.1f, 0.2f);
            var facing = Quaternion.Euler(0, 30, 0);
            const float frame = 1 / 72f;
            Assert.IsFalse(StandPlacement.LooksLikeRecenter(at, facing, at + new Vector3(0.02f, 0, 0.01f), Quaternion.Euler(5, 33, 0), frame), "normal head movement");
            Assert.IsFalse(StandPlacement.LooksLikeRecenter(at, facing, at + Vector3.down * 0.5f, facing, frame), "standing up / bending down is vertical only");
            Assert.IsTrue(StandPlacement.LooksLikeRecenter(at, Quaternion.Euler(0, 70, 0), new Vector3(0, 1.1f, 0), Quaternion.identity, frame), "recenter: yaw back to 0");
            Assert.IsFalse(StandPlacement.LooksLikeRecenter(at, facing, new Vector3(0, 1.1f, 0), Quaternion.identity, frame),
                "a small recenter (22 cm, 30°) is not detected: the stands are still roughly in front, nothing to fix");
            Assert.IsTrue(StandPlacement.LooksLikeRecenter(new Vector3(0.6f, 1.1f, 0.4f), facing, new Vector3(0, 1.1f, 0), facing, frame), "recenter: position back to the origin");
            Assert.IsFalse(StandPlacement.LooksLikeRecenter(at, facing, Vector3.zero, Quaternion.identity, 2f), "a long gap (paused, headset off) is not a recenter");
            Assert.IsTrue(StandPlacement.LooksLikeRecenter(at, Quaternion.Euler(0, 350, 0), at, Quaternion.Euler(0, 300, 0), frame), "yaw compared across 0/360");
            Assert.IsFalse(StandPlacement.LooksLikeRecenter(at, Quaternion.Euler(0, 355, 0), at, Quaternion.Euler(0, 5, 0), frame), "small turn across 0/360");
        }

        [Test]
        public void Menu_ShowControlsAndBack()
        {
            var m = new GameMenu();
            m.Open();
            CollectionAssert.Contains(m.Lines(), "> Fortsätt");
            m.Navigate(1);
            m.Select();
            Assert.IsTrue(m.ShowingControls);
            StringAssert.Contains("Grepp eller avtryckare", string.Join("\n", m.Lines()));
            m.Back();
            Assert.IsFalse(m.ShowingControls);
            Assert.IsTrue(m.IsOpen);
            m.Back();
            Assert.IsFalse(m.IsOpen);
        }

        [Test]
        public void Menu_ResumeCloses()
        {
            var m = new GameMenu();
            m.Open();
            Assert.AreEqual(MenuAction.Resume, m.Select());
            Assert.IsFalse(m.IsOpen);
        }

        [Test]
        public void Sounds_AreGenerated()
        {
            foreach (var data in new[] { SoundSynth.Pop(), SoundSynth.Pling(), SoundSynth.Tick() })
            {
                Assert.Greater(data.Length, 1000);
                var peak = 0f;
                foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                Assert.That(peak, Is.InRange(0.1f, 1f), "audible and not clipping");
            }
        }
    }
}
