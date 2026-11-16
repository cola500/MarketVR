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

        FrogsPlay FrogsAtTable => ChildOf(Ware.Frogs)?.Play as FrogsPlay;
        public FrogPond Frogs => FrogsAtTable?.Pond;
        public Item FrogItem(FrogOwner o) => FrogsAtTable?.Frog[(int)o];
        public Item FrogRear(FrogOwner o) => FrogsAtTable?.Rear[(int)o];
        public int Quacks { get; private set; }

        // How high a frog's centre sits over the table top (half the model's height).
        public float FrogLift => art.FrogModel != null ? art.FrogModel.bounds.extents.y : 0.0225f;
        Vector3 RearLocal => art.FrogModel != null
            ? new Vector3(0, art.FrogModel.bounds.extents.y * 0.4f, -art.FrogModel.bounds.extents.z * 0.7f)
            : new Vector3(0, 0.01f, -0.035f);

        // ---- The player's hands (FruitStand) ----

        FrogsPlay FrogsOf(Item item) => item != null && item.Ware == Ware.Frogs ? FrogsAtTable : null;

        // A hand presses a frog's rear down. False if it can't be pressed now (the grab is let go at once).
        public bool FrogPress(Item rear) => FrogsOf(rear)?.Press(rear) ?? false;

        // The hand lets go: one hop.
        public void FrogRelease(Item rear) => FrogsOf(rear)?.Release(rear);

        // Pause, menu, focus lost, tracking lost: the press is undone, no hop.
        public void FrogCancel(Item rear) => FrogsOf(rear)?.Cancel(rear);

        // A press only lasts while a hand holds the rear (every way a grab ends undoes it).
        public void KeepFrogPresses(IGrabbable left, IGrabbable right) => FrogsAtTable?.KeepPresses(left, right);

        // A frog can be taken or pressed only while it sits on the table, not while it hops, falls or comes back.
        public void UpdateFrogGrabbable() => FrogsAtTable?.UpdateGrabbable();

        // A frog in the hand turns with it (only about up): that is how it is aimed.
        public void AimFrog(Item frog, Quaternion hand)
        {
            var yaw = (Quaternion.Inverse(top.rotation) * hand).eulerAngles.y;
            frog.Transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        sealed class FrogsPlay : ToyPlay
        {
            public FrogPond Pond { get; private set; }
            public readonly Item[] Frog = new Item[2], Rear = new Item[2]; // by FrogOwner
            readonly bool[] playerPressing = new bool[2];
            readonly float[] sinceLanding = new float[2], fallTime = new float[2];
            float childPressLeft, rippleTime;
            Transform pondParts, ripple;

            public FrogsPlay(PlayTableView view, Child child) : base(view, child) { }

            public override void PutOnTable()
            {
                RemoveCarried();
                Pond = new FrogPond(TableHalf);
                Pond.Landed += Landed;
                var pad = FrogPond.PadCentre;
                var art = v.art;
                pondParts = art.Group(v.top, "Pond");
                art.Part(pondParts, "Water", art.Cylinder, art.Matte(Colors.Pond), new Vector3(pad.x, 0.0012f, pad.z), new Vector3(0.26f, 0.0012f, 0.44f), null, false);
                art.Part(pondParts, "Lily Pad", art.Cylinder, art.Matte(Colors.LilyPad), new Vector3(pad.x, 0.003f, pad.z), new Vector3(FrogPond.PadRadius * 2, 0.0015f, FrogPond.PadRadius * 2), null, false);
                art.Part(pondParts, "Pad Notch", art.Cube, art.Matte(Colors.Pond), new Vector3(pad.x + 0.05f, 0.0035f, pad.z + 0.01f), new Vector3(0.06f, 0.0015f, 0.012f), Quaternion.Euler(0, -20, 0), false);
                ripple = art.Group(pondParts, "Ripple");
                for (var i = 0; i < RippleDots; i++) art.Part(ripple, "Drop", art.Cylinder, art.Unlit(Color.white), Vector3.zero, new Vector3(0.018f, 0.0012f, 0.018f), null, false);
                ripple.gameObject.SetActive(false);
                foreach (FrogOwner o in new[] { FrogOwner.Child, FrogOwner.Player })
                {
                    var frog = Item.ForToy(Models.Frog(art, v.top, o == FrogOwner.Child), Ware.Frogs, FrogRadius);
                    frog.Index = (int)o;
                    var rear = art.Group(frog.Transform, "Rear", v.RearLocal);
                    var press = Item.ForPress(rear, Ware.Frogs, RearRadius);
                    press.Index = (int)o;
                    Frog[(int)o] = frog;
                    Rear[(int)o] = press;
                    sinceLanding[(int)o] = FetchAfterLanding;
                    v.items.Add(frog);
                    v.items.Add(press);
                    c.Toys.Add(frog);
                    Pose(o);
                }
                v.sounds.Pop();
            }

            // Where the pond says the frog is, squashed while its rear is held down.
            void Pose(FrogOwner o)
            {
                var frog = Frog[(int)o];
                var pressed = Pond.Phase(o) == FrogPhase.Pressed;
                frog.Transform.SetLocalPositionAndRotation(Pond.Position(o) + Vector3.up * v.FrogLift * (pressed ? 0.8f : 1f),
                    Quaternion.Euler(pressed ? -12 : 0, Pond.Yaw(o), 0));
                frog.Transform.localScale = pressed ? new Vector3(1.06f, 0.82f, 1.02f) : Vector3.one;
            }

            // Every frame while the frog child plays: the pond's clock, the frogs' poses, falls and the ripple.
            public override void Tick(PlayArea area, float dt)
            {
                var busy = v.AnyToyHeld(Ware.Frogs) || playerPressing[0] || playerPressing[1];
                Pond.Tick(dt, busy);
                foreach (FrogOwner o in new[] { FrogOwner.Child, FrogOwner.Player })
                {
                    var i = (int)o;
                    var frog = Frog[i];
                    sinceLanding[i] += dt;
                    if (frog.Place == ItemPlace.Held || frog.Moving) continue;
                    if (Pond.Phase(o) == FrogPhase.Fallen)
                    {
                        fallTime[i] += dt;
                        if (fallTime[i] >= LostSeconds) ResetFrog(o); // the lost-toy rule: back home
                        continue;
                    }
                    Pose(o);
                }
                if (childPressLeft > 0)
                {
                    childPressLeft -= dt;
                    if (childPressLeft <= 0 && Pond.Release(FrogOwner.Child))
                    {
                        v.sounds.Tick();
                        Debug.Log("[Fruktstand] frog hop: the child's");
                    }
                }
                if (ripple.gameObject.activeSelf)
                {
                    rippleTime += dt;
                    var k = rippleTime / RippleSeconds;
                    if (k >= 1) ripple.gameObject.SetActive(false);
                    else
                        for (var d = 0; d < RippleDots; d++)
                        {
                            var a = d * Mathf.PI * 2 / RippleDots;
                            var dot = ripple.GetChild(d);
                            dot.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Mathf.Lerp(0.04f, 0.11f, k);
                            dot.localScale = new Vector3(0.018f, 0.0012f, 0.018f) * (1 - k * 0.7f);
                        }
                }
            }

            void Landed(FrogOwner o, bool onPad, bool offTable)
            {
                sinceLanding[(int)o] = 0;
                var frog = Frog[(int)o];
                if (offTable)
                {
                    // A miss off the table: it drops to the floor and comes back after LostSeconds.
                    fallTime[(int)o] = 0;
                    var at = Pond.Position(o);
                    frog.MoveTo(new Vector3(at.x, -v.Height + v.FrogLift, at.z), Quaternion.Euler(0, Pond.Yaw(o), 0), 0.35f);
                    Debug.Log($"[Fruktstand] frog missed off the table: the {o}'s");
                    return;
                }
                if (!onPad) return;
                v.Quacks++;
                v.sounds.Quack();
                ripple.localPosition = Pond.Position(o) + Vector3.up * 0.004f;
                rippleTime = 0;
                ripple.gameObject.SetActive(true);
                Debug.Log($"[Fruktstand] frog on the lily pad: the {o}'s ({v.Quacks})");
            }

            void ResetFrog(FrogOwner o)
            {
                var i = (int)o;
                Pond.Reset(o);
                playerPressing[i] = false;
                if (o == FrogOwner.Child) childPressLeft = 0;
                fallTime[i] = 0;
                var frog = Frog[i];
                frog.Place = ItemPlace.Play;
                frog.Transform.localScale = Vector3.one;
                frog.MoveTo(FrogPond.Home(o) + Vector3.up * v.FrogLift, Quaternion.Euler(0, FrogPond.HomeYaw(o), 0), 0.5f);
            }

            // The child's play (only while it has the toy): fetch its frog home a moment after a hop, then hop on its turn.
            public override void Play(float dt)
            {
                var frog = Frog[(int)FrogOwner.Child];
                if (frog.Place != ItemPlace.Play || frog.Moving || childPressLeft > 0) return; // never a frog in a hand
                if (Pond.Phase(FrogOwner.Child) != FrogPhase.Sitting) return;
                var home = FrogPond.Home(FrogOwner.Child);
                var away = Vector3.Distance(Pond.Position(FrogOwner.Child), home) > 0.005f || Mathf.Abs(Mathf.DeltaAngle(Pond.Yaw(FrogOwner.Child), FrogPond.HomeYaw(FrogOwner.Child))) > 1;
                if (away)
                {
                    if (sinceLanding[(int)FrogOwner.Child] < FetchAfterLanding) return;
                    Pond.Reset(FrogOwner.Child);
                    frog.MoveTo(home + Vector3.up * v.FrogLift, Quaternion.Euler(0, FrogPond.HomeYaw(FrogOwner.Child), 0), 0.6f);
                    c.Figure.Wave(-20);
                    return;
                }
                if (!Pond.ChildMayHop || !Pond.Press(FrogOwner.Child)) return;
                childPressLeft = ChildPressSeconds; // presses its frog's rear down, then lets go (Tick)
                c.Figure.Wave(-35);
            }

            public bool Press(Item rear)
            {
                if (Pond == null || Frog[rear.Index].Place == ItemPlace.Held || !Pond.Press((FrogOwner)rear.Index)) return false;
                playerPressing[rear.Index] = true;
                return true;
            }

            public void Release(Item rear)
            {
                if (Pond == null) return;
                playerPressing[rear.Index] = false;
                if (!Pond.Release((FrogOwner)rear.Index)) return;
                v.sounds.Tick();
                Debug.Log($"[Fruktstand] frog hop: the {(FrogOwner)rear.Index}'s, by the player");
            }

            public void Cancel(Item rear)
            {
                if (Pond == null) return;
                playerPressing[rear.Index] = false;
                Pond.Cancel((FrogOwner)rear.Index);
            }

            public void KeepPresses(IGrabbable left, IGrabbable right)
            {
                if (Pond == null) return;
                for (var i = 0; i < 2; i++)
                    if (playerPressing[i] && left != Rear[i] && right != Rear[i]) Cancel(Rear[i]);
            }

            public void UpdateGrabbable()
            {
                if (Pond == null) return;
                for (var i = 0; i < 2; i++)
                {
                    var frog = Frog[i];
                    var sitting = Pond.Phase((FrogOwner)i) == FrogPhase.Sitting && !frog.Moving && (i == 1 || childPressLeft <= 0);
                    if (frog.Place == ItemPlace.Play) frog.Grabbable = sitting;
                    Rear[i].Grabbable = sitting && frog.Place == ItemPlace.Play;
                }
            }

            public override void Taken(Item frog)
            {
                if (Pond == null) return;
                Pond.Cancel((FrogOwner)frog.Index);
                playerPressing[frog.Index] = false;
                frog.Transform.localScale = Vector3.one;
            }

            // Set down where the hand let go, facing the way the hand turned it; off the table it falls.
            public override bool ReleasedInstead(Item frog)
            {
                if (Pond == null) return false;
                var o = (FrogOwner)frog.Index;
                var local = v.top.InverseTransformPoint(frog.Position);
                var yaw = (Quaternion.Inverse(v.top.rotation) * frog.Transform.rotation).eulerAngles.y;
                var onTable = Mathf.Abs(local.x) <= TableHalf.x + 0.03f && Mathf.Abs(local.z) <= TableHalf.y + 0.03f && local.y > -0.05f && local.y < 0.5f;
                if (!onTable)
                {
                    Pond.Fall(o, new Vector3(local.x, 0, local.z));
                    fallTime[frog.Index] = 0;
                    frog.MoveTo(new Vector3(local.x, -v.Height + v.FrogLift, local.z), Quaternion.Euler(0, yaw, 0), 0.35f);
                    return true;
                }
                var mx = TableHalf.x - FrogPond.Rim - FrogRadius;
                var mz = TableHalf.y - FrogPond.Rim - FrogRadius;
                var at = new Vector3(Mathf.Clamp(local.x, -mx, mx), 0, Mathf.Clamp(local.z, -mz, mz));
                Pond.Place(o, at, yaw);
                sinceLanding[frog.Index] = 0;
                frog.MoveTo(at + Vector3.up * v.FrogLift, Quaternion.Euler(0, yaw, 0), 0.15f);
                return true;
            }

            // Home, sitting, any press undone.
            public override bool ResetInstead(PlayArea area, Item frog)
            {
                if (Pond == null) return false;
                ResetFrog((FrogOwner)frog.Index);
                return true;
            }

            public override void Removed()
            {
                foreach (var r in Rear) if (r != null) v.items.Remove(r); // the frogs' rears go with them
                Art.Discard(pondParts);
            }
        }
    }
}
