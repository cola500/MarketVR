// Near-body grabbing for one hand. Pure logic: it only sees positions and a "grab held" flag, so it is unit-tested.
// - Hover: the grabbable item nearest the hand within Reach (+ the item's own radius) is highlighted.
// - Grab: only on a new press (rising edge) with something hovered. Holding the button and moving over an item
//   never grabs it, so nothing is picked up by accident.
// - Release: when the button is let go, or the hand loses tracking (then the item must go home).
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Game
{
    public interface IGrabbable
    {
        Vector3 Position { get; }
        float Radius { get; }
        bool CanGrab { get; }
    }

    public enum GrabChange { None, Grabbed, Released, Lost }

    public sealed class Grabber
    {
        public const float Reach = 0.12f; // metres from the hand point to the item's surface

        bool wasPressed = true; // a button already held when the grabber starts is not a press

        public IGrabbable Held { get; private set; }
        public IGrabbable Hover { get; private set; }

        public GrabChange Update(in HandFrame hand, IEnumerable<IGrabbable> items, IGrabbable heldByOtherHand)
        {
            var pressed = hand.Tracked && hand.Grab;
            var press = pressed && !wasPressed;
            wasPressed = pressed;
            Hover = null;

            if (Held != null)
            {
                if (!hand.Tracked) return Drop(GrabChange.Lost);
                return pressed ? GrabChange.None : Drop(GrabChange.Released);
            }
            if (!hand.Tracked) return GrabChange.None;

            Hover = Nearest(hand.Position, items, heldByOtherHand);
            if (press && Hover != null)
            {
                Held = Hover;
                return GrabChange.Grabbed;
            }
            return GrabChange.None;
        }

        // Focus lost, menu opened, restart: let go without any drop logic. The caller sends the item home.
        public IGrabbable ForceRelease()
        {
            var item = Held;
            Held = null;
            Hover = null;
            wasPressed = true; // require a fresh press afterwards
            return item;
        }

        GrabChange Drop(GrabChange change)
        {
            Held = null;
            return change;
        }

        public static IGrabbable Nearest(Vector3 hand, IEnumerable<IGrabbable> items, IGrabbable exclude = null)
        {
            IGrabbable best = null;
            var bestDistance = float.MaxValue;
            foreach (var item in items)
            {
                if (item == null || item == exclude || !item.CanGrab) continue;
                var d = Vector3.Distance(hand, item.Position) - item.Radius;
                if (d <= Reach && d < bestDistance)
                {
                    bestDistance = d;
                    best = item;
                }
            }
            return best;
        }
    }
}
