// EPIC-018 (SPEC §34): the hopping frogs at the play table, the view of Core.FrogPond. A pond mat with a big lily pad
// and two frogs: the child's (orange, on the child's side) and the player's (green, on the player's side), both facing
// the pad. A hand presses a frog's rear (Item.ForPress) and lets go: one hop. A hand may also lift a frog by its middle
// and turn it to aim. The child shows a hop, fetches its frog home and waits for the player (FrogPond's turns).
// The frogs have no physics bodies: a hop is the pond's arc, a fall off the table a short drop to the floor.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        const float FrogRadius = 0.04f;        // grab the frog by its middle
        const float RearRadius = 0.025f;       // press it by its rear
        const float ChildPressSeconds = 0.35f; // the child holds its frog's rear down this long
        const float FetchAfterLanding = 1f;
        const float RippleSeconds = 0.8f;
        const int RippleDots = 12;

        public FrogPond Frogs => ChildOf(Ware.Frogs)?.Pond;
        public Item FrogItem(FrogOwner o) => ChildOf(Ware.Frogs)?.Frog[(int)o];
        public Item FrogRear(FrogOwner o) => ChildOf(Ware.Frogs)?.Rear[(int)o];
        public int Quacks { get; private set; }

        // How high a frog's centre sits over the table top (half the model's height).
        public float FrogLift => art.FrogModel != null ? art.FrogModel.bounds.extents.y : 0.0225f;
        Vector3 RearLocal => art.FrogModel != null
            ? new Vector3(0, art.FrogModel.bounds.extents.y * 0.4f, -art.FrogModel.bounds.extents.z * 0.7f)
            : new Vector3(0, 0.01f, -0.035f);

        void PutFrogsOnTable(Child c)
        {
            if (c.Carried != null) Remove(c.Carried);
            c.Carried = null;
            c.Pond = new FrogPond(TableHalf);
            c.Pond.Landed += (o, onPad, off) => FrogLanded(c, o, onPad, off);
            var pad = FrogPond.PadCentre;
            c.PondParts = art.Group(top, "Pond");
            art.Part(c.PondParts, "Water", art.Cylinder, art.Matte(Colors.Pond), new Vector3(pad.x, 0.0012f, pad.z), new Vector3(0.26f, 0.0012f, 0.44f), null, false);
            art.Part(c.PondParts, "Lily Pad", art.Cylinder, art.Matte(Colors.LilyPad), new Vector3(pad.x, 0.003f, pad.z), new Vector3(FrogPond.PadRadius * 2, 0.0015f, FrogPond.PadRadius * 2), null, false);
            art.Part(c.PondParts, "Pad Notch", art.Cube, art.Matte(Colors.Pond), new Vector3(pad.x + 0.05f, 0.0035f, pad.z + 0.01f), new Vector3(0.06f, 0.0015f, 0.012f), Quaternion.Euler(0, -20, 0), false);
            c.Ripple = art.Group(c.PondParts, "Ripple");
            for (var i = 0; i < RippleDots; i++) art.Part(c.Ripple, "Drop", art.Cylinder, art.Unlit(Color.white), Vector3.zero, new Vector3(0.018f, 0.0012f, 0.018f), null, false);
            c.Ripple.gameObject.SetActive(false);
            foreach (FrogOwner o in new[] { FrogOwner.Child, FrogOwner.Player })
            {
                var frog = Item.ForToy(Models.Frog(art, top, o == FrogOwner.Child), Ware.Frogs, FrogRadius);
                frog.Index = (int)o;
                var rear = art.Group(frog.Transform, "Rear", RearLocal);
                var press = Item.ForPress(rear, Ware.Frogs, RearRadius);
                press.Index = (int)o;
                c.Frog[(int)o] = frog;
                c.Rear[(int)o] = press;
                c.SinceLanding[(int)o] = FetchAfterLanding;
                items.Add(frog);
                items.Add(press);
                c.Toys.Add(frog);
                PoseFrog(c, o);
            }
            sounds.Pop();
        }

        // Where the pond says the frog is, squashed while its rear is held down.
        void PoseFrog(Child c, FrogOwner o)
        {
            var frog = c.Frog[(int)o];
            var pressed = c.Pond.Phase(o) == FrogPhase.Pressed;
            frog.Transform.SetLocalPositionAndRotation(c.Pond.Position(o) + Vector3.up * FrogLift * (pressed ? 0.8f : 1f),
                Quaternion.Euler(pressed ? -12 : 0, c.Pond.Yaw(o), 0));
            frog.Transform.localScale = pressed ? new Vector3(1.06f, 0.82f, 1.02f) : Vector3.one;
        }

        // Every frame while the frog child plays: the pond's clock, the frogs' poses, falls and the ripple.
        void FrogStep(Child c, float dt)
        {
            var busy = AnyToyHeld(Ware.Frogs) || c.PlayerPressing[0] || c.PlayerPressing[1];
            c.Pond.Tick(dt, busy);
            foreach (FrogOwner o in new[] { FrogOwner.Child, FrogOwner.Player })
            {
                var i = (int)o;
                var frog = c.Frog[i];
                c.SinceLanding[i] += dt;
                if (frog.Place == ItemPlace.Held || frog.Moving) continue;
                if (c.Pond.Phase(o) == FrogPhase.Fallen)
                {
                    c.FallTime[i] += dt;
                    if (c.FallTime[i] >= LostSeconds) ResetFrog(c, o); // the lost-toy rule: back home
                    continue;
                }
                PoseFrog(c, o);
            }
            if (c.ChildPressLeft > 0)
            {
                c.ChildPressLeft -= dt;
                if (c.ChildPressLeft <= 0 && c.Pond.Release(FrogOwner.Child))
                {
                    sounds.Tick();
                    Debug.Log("[Fruktstand] frog hop: the child's");
                }
            }
            if (c.Ripple.gameObject.activeSelf)
            {
                c.RippleTime += dt;
                var k = c.RippleTime / RippleSeconds;
                if (k >= 1) c.Ripple.gameObject.SetActive(false);
                else
                    for (var d = 0; d < RippleDots; d++)
                    {
                        var a = d * Mathf.PI * 2 / RippleDots;
                        var dot = c.Ripple.GetChild(d);
                        dot.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Mathf.Lerp(0.04f, 0.11f, k);
                        dot.localScale = new Vector3(0.018f, 0.0012f, 0.018f) * (1 - k * 0.7f);
                    }
            }
        }

        void FrogLanded(Child c, FrogOwner o, bool onPad, bool offTable)
        {
            c.SinceLanding[(int)o] = 0;
            var frog = c.Frog[(int)o];
            if (offTable)
            {
                // A miss off the table: it drops to the floor and comes back after LostSeconds.
                c.FallTime[(int)o] = 0;
                var at = c.Pond.Position(o);
                frog.MoveTo(new Vector3(at.x, -Height + FrogLift, at.z), Quaternion.Euler(0, c.Pond.Yaw(o), 0), 0.35f);
                Debug.Log($"[Fruktstand] frog missed off the table: the {o}'s");
                return;
            }
            if (!onPad) return;
            Quacks++;
            sounds.Quack();
            c.Ripple.localPosition = c.Pond.Position(o) + Vector3.up * 0.004f;
            c.RippleTime = 0;
            c.Ripple.gameObject.SetActive(true);
            Debug.Log($"[Fruktstand] frog on the lily pad: the {o}'s ({Quacks})");
        }

        void ResetFrog(Child c, FrogOwner o)
        {
            var i = (int)o;
            c.Pond.Reset(o);
            c.PlayerPressing[i] = false;
            if (o == FrogOwner.Child) c.ChildPressLeft = 0;
            c.FallTime[i] = 0;
            var frog = c.Frog[i];
            frog.Place = ItemPlace.Play;
            frog.Transform.localScale = Vector3.one;
            frog.MoveTo(FrogPond.Home(o) + Vector3.up * FrogLift, Quaternion.Euler(0, FrogPond.HomeYaw(o), 0), 0.5f);
        }

        // The child's play (only while it has the toy): fetch its frog home a moment after a hop, then hop on its turn.
        void PlayFrogs(Child c)
        {
            var frog = c.Frog[(int)FrogOwner.Child];
            if (frog.Place != ItemPlace.Play || frog.Moving || c.ChildPressLeft > 0) return; // never a frog in a hand
            if (c.Pond.Phase(FrogOwner.Child) != FrogPhase.Sitting) return;
            var home = FrogPond.Home(FrogOwner.Child);
            var away = Vector3.Distance(c.Pond.Position(FrogOwner.Child), home) > 0.005f || Mathf.Abs(Mathf.DeltaAngle(c.Pond.Yaw(FrogOwner.Child), FrogPond.HomeYaw(FrogOwner.Child))) > 1;
            if (away)
            {
                if (c.SinceLanding[(int)FrogOwner.Child] < FetchAfterLanding) return;
                c.Pond.Reset(FrogOwner.Child);
                frog.MoveTo(home + Vector3.up * FrogLift, Quaternion.Euler(0, FrogPond.HomeYaw(FrogOwner.Child), 0), 0.6f);
                c.Figure.Wave(-20);
                return;
            }
            if (!c.Pond.ChildMayHop || !c.Pond.Press(FrogOwner.Child)) return;
            c.ChildPressLeft = ChildPressSeconds; // presses its frog's rear down, then lets go (FrogStep)
            c.Figure.Wave(-35);
        }

        // ---- The player's hands (FruitStand) ----

        Child FrogChild(Item item) => item != null && item.Ware == Ware.Frogs ? ChildOf(Ware.Frogs) : null;

        // A hand presses a frog's rear down. False if it can't be pressed now (the grab is let go at once).
        public bool FrogPress(Item rear)
        {
            var c = FrogChild(rear);
            if (c?.Pond == null || c.Frog[rear.Index].Place == ItemPlace.Held || !c.Pond.Press((FrogOwner)rear.Index)) return false;
            c.PlayerPressing[rear.Index] = true;
            return true;
        }

        // The hand lets go: one hop.
        public void FrogRelease(Item rear)
        {
            var c = FrogChild(rear);
            if (c?.Pond == null) return;
            c.PlayerPressing[rear.Index] = false;
            if (!c.Pond.Release((FrogOwner)rear.Index)) return;
            sounds.Tick();
            Debug.Log($"[Fruktstand] frog hop: the {(FrogOwner)rear.Index}'s, by the player");
        }

        // Pause, menu, focus lost, tracking lost: the press is undone, no hop.
        public void FrogCancel(Item rear)
        {
            var c = FrogChild(rear);
            if (c?.Pond == null) return;
            c.PlayerPressing[rear.Index] = false;
            c.Pond.Cancel((FrogOwner)rear.Index);
        }

        // A press only lasts while a hand holds the rear (every way a grab ends undoes it).
        public void KeepFrogPresses(IGrabbable left, IGrabbable right)
        {
            var c = ChildOf(Ware.Frogs);
            if (c?.Pond == null) return;
            for (var i = 0; i < 2; i++)
                if (c.PlayerPressing[i] && left != c.Rear[i] && right != c.Rear[i]) FrogCancel(c.Rear[i]);
        }

        // A frog can be taken or pressed only while it sits on the table, not while it hops, falls or comes back.
        public void UpdateFrogGrabbable()
        {
            var c = ChildOf(Ware.Frogs);
            if (c?.Pond == null) return;
            for (var i = 0; i < 2; i++)
            {
                var frog = c.Frog[i];
                var sitting = c.Pond.Phase((FrogOwner)i) == FrogPhase.Sitting && !frog.Moving && (i == 1 || c.ChildPressLeft <= 0);
                if (frog.Place == ItemPlace.Play) frog.Grabbable = sitting;
                c.Rear[i].Grabbable = sitting && frog.Place == ItemPlace.Play;
            }
        }

        // A frog in the hand turns with it (only about up): that is how it is aimed.
        public void AimFrog(Item frog, Quaternion hand)
        {
            var yaw = (Quaternion.Inverse(top.rotation) * hand).eulerAngles.y;
            frog.Transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        void FrogTaken(Child c, Item frog)
        {
            if (c?.Pond == null) return;
            c.Pond.Cancel((FrogOwner)frog.Index);
            c.PlayerPressing[frog.Index] = false;
            frog.Transform.localScale = Vector3.one;
        }

        // Set down where the hand let go, facing the way the hand turned it; off the table it falls.
        void FrogReleased(Child c, Item frog)
        {
            var o = (FrogOwner)frog.Index;
            var local = top.InverseTransformPoint(frog.Position);
            var yaw = (Quaternion.Inverse(top.rotation) * frog.Transform.rotation).eulerAngles.y;
            var onTable = Mathf.Abs(local.x) <= TableHalf.x + 0.03f && Mathf.Abs(local.z) <= TableHalf.y + 0.03f && local.y > -0.05f && local.y < 0.5f;
            if (!onTable)
            {
                c.Pond.Fall(o, new Vector3(local.x, 0, local.z));
                c.FallTime[frog.Index] = 0;
                frog.MoveTo(new Vector3(local.x, -Height + FrogLift, local.z), Quaternion.Euler(0, yaw, 0), 0.35f);
                return;
            }
            var mx = TableHalf.x - FrogPond.Rim - FrogRadius;
            var mz = TableHalf.y - FrogPond.Rim - FrogRadius;
            var at = new Vector3(Mathf.Clamp(local.x, -mx, mx), 0, Mathf.Clamp(local.z, -mz, mz));
            c.Pond.Place(o, at, yaw);
            c.SinceLanding[frog.Index] = 0;
            frog.MoveTo(at + Vector3.up * FrogLift, Quaternion.Euler(0, yaw, 0), 0.15f);
        }
    }
}
