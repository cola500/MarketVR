// EPIC-012: point and click in the VR menus. The router asks PointAt(hand, state) which button a hand's ray is on
// (number on the page; -1 = on the panel but no button; null = not on the panel). Names match docs/EPIC-012.md.
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class PointerTests
    {
        static XrState Idle() => new()
        {
            Left = new XrHandState { Tracked = true, Position = new Vector3(-0.2f, 1, 0.3f), Rotation = Quaternion.identity },
            Right = new XrHandState { Tracked = true, Position = new Vector3(0.2f, 1, 0.3f), Rotation = Quaternion.identity },
        };

        static XrState With(System.Func<XrState, XrState> f) => f(Idle());

        // A router with the menu open, and pointers that hit the buttons we say.
        static XrInputRouter OpenMenu(int? left, int? right)
        {
            var r = new XrInputRouter { PointAt = (hand, _) => hand == 0 ? left : right };
            r.Menu.BuildLine = "";
            r.Route(Idle(), 0.01f, out _);
            r.Route(With(x => { x.Left.Menu = true; return x; }), 0.01f, out var opened);
            Assert.AreEqual(MenuAction.None, opened, "the press that opens the menu chooses nothing");
            Assert.IsTrue(r.Menu.IsOpen);
            r.Route(Idle(), 0.01f, out _);
            return r;
        }

        static int ItemOf(GameMenu m, string label)
        {
            foreach (var row in m.Rows()) if (row.Kind == RowKind.Button && row.Text == label) return row.Item;
            Assert.Fail($"'{label}' on the page");
            return -1;
        }

        [Test]
        public void Router_PointerClickActivatesOnce()
        {
            var probe = new GameMenu { BuildLine = "" };
            probe.Open();
            var rest = ItemOf(probe, "Ta vilopaus");
            var tidy = ItemOf(probe, "Städa lekytan");
            var r = OpenMenu(left: tidy, right: rest);
            r.Route(Idle(), 0.01f, out _);
            Assert.AreEqual(rest, r.Menu.Selected, "the button a ray rests on is highlighted (the right hand when both point)");
            Assert.AreEqual(rest, r.PointedItem(1));
            Assert.AreEqual(tidy, r.PointedItem(0));

            // Both triggers in the same frame: one click, the right hand's button.
            var both = With(x => { x.Left.Trigger = true; x.Right.Trigger = true; return x; });
            r.Route(both, 0.01f, out var action);
            Assert.AreEqual(MenuAction.TakeRest, action, "the button the clicking ray is on");
            Assert.AreEqual(1, r.ClickedHand, "for the flash and the buzz");
            Assert.IsFalse(r.Menu.IsOpen);
            r.Route(both, 0.01f, out action);
            Assert.AreEqual(MenuAction.None, action, "held triggers don't click again");

            // The left trigger alone clicks the left ray's button.
            r = OpenMenu(left: tidy, right: null);
            r.Route(With(x => { x.Left.Trigger = true; return x; }), 0.01f, out action);
            Assert.AreEqual(MenuAction.TidyPlay, action);
            Assert.AreEqual(0, r.ClickedHand);

            // On the panel but between buttons: nothing.
            r = OpenMenu(left: null, right: -1);
            r.Route(With(x => { x.Right.Trigger = true; return x; }), 0.01f, out action);
            Assert.AreEqual(MenuAction.None, action);
            Assert.IsTrue(r.Menu.IsOpen);

            // Not pointing at the panel at all: the trigger chooses the selected button, as before.
            r = OpenMenu(left: null, right: null);
            r.Route(With(x => { x.Right.Trigger = true; return x; }), 0.01f, out action);
            Assert.AreEqual(MenuAction.Resume, action, "Fortsätt is preselected");
        }

        [Test]
        public void Router_PointerClickNeverReachesTheWorld()
        {
            var probe = new GameMenu { BuildLine = "" };
            probe.Open();
            var r = OpenMenu(left: null, right: ItemOf(probe, "Fortsätt"));
            // Sticks pushed and grip held while clicking Fortsätt.
            var busy = With(x => { x.Right.Trigger = true; x.Right.Grip = true; x.Left.Stick = new Vector2(0, 1); x.Right.Stick = new Vector2(0, 1); return x; });
            var f = r.Route(busy, 0.1f, out var action);
            Assert.AreEqual(MenuAction.Resume, action);
            for (var i = 0; i < 3; i++)
            {
                Assert.IsFalse(f.Left.Grab || f.Right.Grab, "no grab");
                Assert.AreEqual(Vector2.zero, f.Walk, "no walking");
                Assert.AreEqual(0, f.Turn, "no turning");
                Assert.AreEqual(0, f.CounterHeightDelta, "no height change");
                f = r.Route(busy, 0.1f, out _); // still held after the menu closed
            }
            r.Route(Idle(), 0.01f, out _);
            Assert.IsTrue(r.Route(With(x => { x.Right.Grip = true; return x; }), 0.01f, out _).Right.Grab, "a fresh press works again");
            Assert.AreEqual(-1, r.ClickedHand, "no click without a menu");
            Assert.IsNull(r.PointedItem(1), "no pointer without a menu");
        }

        [Test]
        public void Router_PointerNeedsFocusAndNewPress()
        {
            var probe = new GameMenu { BuildLine = "" };
            probe.Open();
            var r = OpenMenu(left: null, right: ItemOf(probe, "Ta vilopaus"));
            r.SetFocus(false);
            var trigger = With(x => { x.Right.Trigger = true; return x; });
            r.Route(trigger, 0.01f, out var action);
            Assert.AreEqual(MenuAction.None, action, "no menu input without focus");
            Assert.IsNull(r.PointedItem(1), "and no pointer");
            r.SetFocus(true);
            r.Route(trigger, 0.01f, out action);
            Assert.AreEqual(MenuAction.None, action, "a trigger held through the system menu is not a click");
            r.Route(Idle(), 0.01f, out _);
            r.Route(trigger, 0.01f, out action);
            Assert.AreEqual(MenuAction.TakeRest, action, "a fresh press clicks");
        }

        [Test]
        public void Router_ConfirmWithPointerOrButtons()
        {
            // Pointer: Nytt spel -> the confirmation -> Ja.
            var probe = new GameMenu { BuildLine = "" };
            probe.Open();
            var newGame = ItemOf(probe, "Nytt spel");
            var pointed = (int?)newGame;
            var r = new XrInputRouter { PointAt = (hand, _) => hand == 1 ? pointed : null };
            r.Menu.BuildLine = "";
            r.Route(Idle(), 0.01f, out _);
            r.Route(With(x => { x.Right.Menu = true; return x; }), 0.01f, out _);
            r.Route(Idle(), 0.01f, out _);
            var click = With(x => { x.Right.Trigger = true; return x; });
            r.Route(click, 0.01f, out var action);
            Assert.AreEqual(MenuAction.None, action);
            Assert.IsTrue(r.Menu.Confirming);
            Assert.AreEqual(0, r.Menu.Selected, "Nej is preselected");
            r.Route(Idle(), 0.01f, out _);
            pointed = ItemOf(r.Menu, "Ja, nytt spel");
            r.Route(click, 0.01f, out action);
            Assert.AreEqual(MenuAction.NewGame, action);

            // Buttons: the stick down to Nytt spel, A, the stick down to Ja, A.
            var b = new XrInputRouter();
            b.Menu.BuildLine = "";
            b.Route(Idle(), 0.01f, out _);
            b.Route(With(x => { x.Left.Menu = true; return x; }), 0.01f, out _);
            b.Route(Idle(), 0.01f, out _);
            b.Route(With(x => { x.Right.Stick = new Vector2(0, 0.9f); return x; }), 0.01f, out _); // up = wraps to the last
            b.Route(Idle(), 0.01f, out _);
            var a = With(x => { x.Right.Primary = true; return x; });
            b.Route(a, 0.01f, out action);
            Assert.IsTrue(b.Menu.Confirming, "Nytt spel opened the confirmation");
            b.Route(Idle(), 0.01f, out _);
            b.Route(With(x => { x.Right.Stick = new Vector2(0, -0.9f); return x; }), 0.01f, out _);
            b.Route(Idle(), 0.01f, out _);
            b.Route(a, 0.01f, out action);
            Assert.AreEqual(MenuAction.NewGame, action);
        }
    }
}
