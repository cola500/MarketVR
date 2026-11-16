// Decides each frame who gets the controllers: nobody, the game menu, or the stand. Pure logic, unit-tested.
// Adapted from Magic Tiles' XrInputRouter (focus, pause, presence, neutral requirement) for two grabbing hands.
//
// - No XR input focus (system menu open), app paused, or headset off: everything is ignored, hands are hidden
//   (Meta VRC.Quest.Input.4) and ReleaseAll sends whatever is held home. The system button is never read.
// - Menu button (either hand) toggles the game menu; while it is open the controllers drive only the menu, and
//   anything held goes home. EPIC-012: a ray from each tracked hand highlights the button it is on and that hand's
//   trigger clicks it (one click per frame); the stick + A/X and B/Y work as before.
// - Whenever control returns to the stand (focus back, unpaused, headset on, menu closed), grabbing stays off
//   until every button is released and every stick is back near neutral, so nothing held over from before can
//   grab, drop or pay.
// - Sticks (EPIC-005, seated play): the LEFT stick walks (forward/back/sideways relative to the head; XrRig moves the
//   rig) and never changes the counter height. The RIGHT stick locks onto its dominant axis when it leaves the dead
//   zone and keeps that axis until it is back near the middle: left/right = one snap turn of TurnDegrees per flick,
//   up/down = raise/lower the counters (smoothly). So one flick either turns or changes height, never both.
using UnityEngine;

namespace Fruktstand.Game
{
    public struct XrHandState
    {
        public bool Tracked;
        public Vector3 Position;    // tracking space
        public Quaternion Rotation; // tracking space
        public bool Grip;
        public bool Trigger;
        public bool Primary;   // A / X
        public bool Secondary; // B / Y
        public Vector2 Stick;
        public bool Menu;      // Menu button as the runtime reports it (never the system button)

        public bool AnyPressed => Grip || Trigger || Primary || Secondary || Menu;
    }

    public struct XrState
    {
        public XrHandState Left;
        public XrHandState Right;
    }

    // One step per stick flick: fires when |x| passes Engage, then waits until the stick is back inside Release.
    public sealed class StickStepper
    {
        public const float Engage = 0.6f;
        public const float Release = 0.25f;

        bool armed = true;

        public int Update(float x)
        {
            if (!armed)
            {
                if (Mathf.Abs(x) < Release) armed = true;
                return 0;
            }
            if (Mathf.Abs(x) < Engage) return 0;
            armed = false;
            return x > 0 ? 1 : -1;
        }
    }

    // The right stick: the axis that dominates when it leaves the dead zone is locked until the stick is back near the
    // middle. Turn: one step when |x| passes StickStepper.Engage. Height: -1..1 from y beyond the dead zone.
    public sealed class RightStick
    {
        public enum Axis { None, Turn, Height }

        bool turned;

        public Axis Locked { get; private set; }

        public void Reset()
        {
            Locked = Axis.None;
            turned = false;
        }

        public (int turn, float height) Update(Vector2 stick)
        {
            float ax = Mathf.Abs(stick.x), ay = Mathf.Abs(stick.y);
            if (Locked != Axis.None && ax < StickStepper.Release && ay < StickStepper.Release) Reset();
            if (Locked == Axis.None)
            {
                if (stick.magnitude <= XrInputRouter.CounterDeadZone) return (0, 0);
                Locked = ax >= ay ? Axis.Turn : Axis.Height;
            }
            if (Locked == Axis.Turn)
            {
                if (turned || ax < StickStepper.Engage) return (0, 0);
                turned = true;
                return (stick.x > 0 ? 1 : -1, 0);
            }
            if (ay <= XrInputRouter.CounterDeadZone) return (0, 0);
            return (0, Mathf.Sign(stick.y) * (ay - XrInputRouter.CounterDeadZone) / (1 - XrInputRouter.CounterDeadZone));
        }
    }

    public sealed class XrInputRouter
    {
        public const float NeutralStick = StickStepper.Release;
        public const float CounterSpeed = 0.25f;  // m/s at full stick
        public const float CounterDeadZone = 0.3f; // right stick: leaving this picks (and locks) the dominant axis
        public const float TurnDegrees = 30f;
        public const float WalkDeadZone = 0.2f;    // left stick, radial

        readonly StickStepper menuNav = new();
        readonly StickStepper menuSide = new(); // left/right on the calibration page (EPIC-007)
        readonly RightStick right = new();
        readonly GameMenu menu = new();
        bool hasFocus = true;
        bool running = true; // not paused
        bool present = true; // headset worn
        bool waitForNeutral;
        XrState previous;

        public GameMenu Menu => menu;
        // EPIC-012: which menu button a hand's ray is on (0 left, 1 right): the button number on the page, -1 on the panel
        // but between buttons, null off the panel. Set by XrRig (MenuPanel's hit test); tests give their own.
        public System.Func<int, XrHandState, int?> PointAt { get; set; }
        readonly int?[] pointed = new int?[2];
        int? lastHover;
        public int? PointedItem(int hand) => pointed[hand];
        public int ClickedHand { get; private set; } = -1; // the hand whose trigger clicked this frame (flash, buzz)
        public bool HasFocus => hasFocus;
        public bool Paused => !running;
        public bool Present => present;
        public bool WaitingForNeutral => waitForNeutral;
        // Hands may be shown (and input read) only with focus, unpaused and worn.
        public bool InputAllowed => hasFocus && running && present;
        public bool PlayActive => InputAllowed && !menu.IsOpen && !waitForNeutral;

        public void SetFocus(bool value) => Set(ref hasFocus, value);
        public void SetPaused(bool value) => Set(ref running, !value);
        public void SetPresent(bool value) => Set(ref present, value);

        void Set(ref bool flag, bool allowed)
        {
            if (flag == allowed) return;
            flag = allowed;
            waitForNeutral = true;
        }

        public static bool IsNeutral(in XrHandState h) =>
            !h.AnyPressed && Mathf.Abs(h.Stick.x) < NeutralStick && Mathf.Abs(h.Stick.y) < NeutralStick;

        public static bool IsNeutral(in XrState s) => IsNeutral(s.Left) && IsNeutral(s.Right);

        // Left stick -> walking input: radial dead zone, length at most 1 (a diagonal is no faster than straight).
        public static Vector2 WalkInput(Vector2 stick)
        {
            var m = stick.magnitude;
            if (m <= WalkDeadZone) return Vector2.zero;
            var scaled = (Mathf.Min(m, 1f) - WalkDeadZone) / (1f - WalkDeadZone);
            return stick / m * scaled;
        }

        public bool RightLockedToTurn => right.Locked == RightStick.Axis.Turn;
        public bool RightLockedToHeight => right.Locked == RightStick.Axis.Height;

        // Hand poses always pass through (for showing the hands); Grab only while play is active.
        public ShopFrame Route(in XrState state, float dt, out MenuAction action)
        {
            action = MenuAction.None;
            ClickedHand = -1;
            var frame = new ShopFrame { DeltaTime = dt, Left = Pose(state.Left), Right = Pose(state.Right) };

            if (!InputAllowed)
            {
                pointed[0] = pointed[1] = lastHover = null; // no menu input without focus
                right.Reset();
                previous = state; // a button still held when focus returns is not a new press
                frame.ReleaseAll = true;
                frame.Left.Tracked = frame.Right.Tracked = false;
                return frame;
            }

            var menuPressed = (state.Left.Menu && !previous.Left.Menu) || (state.Right.Menu && !previous.Right.Menu);
            if (menuPressed)
            {
                menu.Toggle();
                pointed[0] = pointed[1] = lastHover = null;
                if (!menu.IsOpen) waitForNeutral = true;
            }
            else if (menu.IsOpen)
            {
                // EPIC-012 pointer: the button a ray rests on is highlighted (the right hand's when both point), but only
                // when it changes, so the stick can still move the highlight while a ray rests on the panel.
                pointed[0] = state.Left.Tracked ? PointAt?.Invoke(0, state.Left) : null;
                pointed[1] = state.Right.Tracked ? PointAt?.Invoke(1, state.Right) : null;
                var hover = pointed[1] >= 0 ? pointed[1] : pointed[0] >= 0 ? pointed[0] : null;
                if (hover != lastHover && hover is int h) menu.Hover(h);
                lastHover = hover;

                var y = Mathf.Abs(state.Right.Stick.y) >= Mathf.Abs(state.Left.Stick.y) ? state.Right.Stick.y : state.Left.Stick.y;
                var step = menuNav.Update(y);
                if (step != 0) menu.Navigate(-step); // stick up = previous item
                var x = Mathf.Abs(state.Right.Stick.x) >= Mathf.Abs(state.Left.Stick.x) ? state.Right.Stick.x : state.Left.Stick.x;
                var side = menuSide.Update(x);
                if (side != 0) action = menu.Adjust(side);

                // One click per frame: the right trigger first. On a button: that button; on the panel between buttons:
                // nothing; not on the panel: the highlighted button (as before the pointer).
                var clicked = false;
                foreach (var hand in new[] { 1, 0 })
                {
                    var now = hand == 1 ? state.Right : state.Left;
                    var before = hand == 1 ? previous.Right : previous.Left;
                    if (!now.Trigger || before.Trigger) continue;
                    clicked = true;
                    if (pointed[hand] is int item)
                    {
                        if (item >= 0)
                        {
                            action = menu.SelectItem(item);
                            ClickedHand = hand;
                        }
                    }
                    else
                    {
                        action = menu.Select();
                        ClickedHand = hand;
                    }
                    break;
                }
                if (!clicked)
                {
                    if (Pressed(state, previous, h => h.Primary)) action = menu.Select();
                    else if (Pressed(state, previous, h => h.Secondary)) action = menu.Back();
                }
                if (!menu.IsOpen)
                {
                    waitForNeutral = true;
                    pointed[0] = pointed[1] = lastHover = null;
                }
            }
            else pointed[0] = pointed[1] = lastHover = null;
            previous = state;

            if (menu.IsOpen || waitForNeutral)
            {
                right.Reset(); // a stick held from before can't turn or move the counters on the way back
                if (!menu.IsOpen && IsNeutral(state)) waitForNeutral = false;
                frame.ReleaseAll = menu.IsOpen;
                frame.NewGame = action == MenuAction.NewGame;
                frame.ToggleOpen = action == MenuAction.ToggleOpen;
                frame.ClearBasket = action == MenuAction.ClearBasket;
                frame.TidyPlay = action == MenuAction.TidyPlay;
                frame.TakeRest = action == MenuAction.TakeRest;
                frame.NextRound = action == MenuAction.NextRound;
                frame.ResumeRest = action == MenuAction.ResumeRest;
                frame.ParentRest = action == MenuAction.ParentRest;
                frame.ParentResume = action == MenuAction.ParentResume;
                frame.CycleNatureSound = action == MenuAction.CycleNatureSound;
                return frame; // Grab stays false
            }

            frame.Left.Grab = state.Left.Grip || state.Left.Trigger;
            frame.Right.Grab = state.Right.Grip || state.Right.Trigger;
            frame.Walk = WalkInput(state.Left.Stick);
            var (turnStep, height) = right.Update(state.Right.Stick);
            frame.Turn = turnStep;
            frame.CounterHeightDelta = height * CounterSpeed * dt;
            return frame;
        }

        static HandFrame Pose(in XrHandState h) => new() { Tracked = h.Tracked, Position = h.Position, Rotation = h.Rotation };

        static bool Pressed(in XrState now, in XrState before, System.Func<XrHandState, bool> button) =>
            (button(now.Left) && !button(before.Left)) || (button(now.Right) && !button(before.Right));
    }
}
