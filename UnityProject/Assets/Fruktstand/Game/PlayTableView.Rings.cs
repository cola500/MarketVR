// The stacking rings at the play table (EPIC-010): a fixed peg and three rings; the child stacks one ring at a time,
// the biggest first; a ring the player lets go of near the peg goes onto it, on top of the others.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        public bool HasPeg(Ware toy) => ChildOf(toy)?.Play is RingsPlay rings && rings.Peg != null;

        public int RingsOnPeg
        {
            get
            {
                var c = ChildOf(Ware.StackingRings);
                if (c == null) return 0;
                var n = 0;
                foreach (var t in c.Toys) if (t.Slot >= 0) n++;
                return n;
            }
        }

        sealed class RingsPlay : ToyPlay
        {
            public Transform Peg { get; private set; } // fixed, no collider, not grabbable
            float act;

            public RingsPlay(PlayTableView view, Child child) : base(view, child) { }

            public override void PutOnTable()
            {
                // The little set the child carried becomes a fixed peg and three full-size rings (EPIC-010).
                RemoveCarried();
                Peg = Models.RingPeg(v.art, v.top);
                Peg.localPosition = RingSpot;
                for (var i = 0; i < WareInfo.RingsInSet; i++)
                {
                    var r = Item.ForToy(Models.Ring(v.art, v.top, i), Ware.StackingRings, Models.RingRadius * Models.RingScale[i]);
                    r.Index = i;
                    r.Slot = -1; // not on the peg
                    r.Transform.localPosition = RingPile(i);
                    var k = Models.RingScale[i];
                    AddBody(r, new Vector3(Models.RingRadius * 2 * k, Models.RingThickness * k, Models.RingRadius * 2 * k), 0.04f, 0.6f, 0.3f);
                    v.items.Add(r);
                    c.Toys.Add(r);
                }
                v.sounds.Pop();
            }

            // One ring at a time onto the peg, the biggest that isn't there yet.
            public override void Play(float dt)
            {
                act += dt;
                if (act < RingEverySeconds) return;
                Item next = null;
                foreach (var r in c.Toys)
                    if (r.Slot < 0 && r.Place == ItemPlace.Play && !r.Moving && (r.Body == null || r.Body.linearVelocity.magnitude < RestSpeed) && (next == null || r.Index < next.Index))
                        next = r;
                if (next == null) return;
                act = 0;
                var height = TowerTop();
                next.Body.isKinematic = true;
                next.Slot = height;
                next.MoveTo(RingStack(height), Quaternion.identity, 0.5f);
                c.Figure.Wave(-20);
            }

            int TowerTop()
            {
                var n = 0;
                foreach (var b in c.Toys) if (b.Slot >= 0) n = Mathf.Max(n, b.Slot + 1);
                return n;
            }

            // Taken off the peg: whatever stood on top of it falls freely.
            public override void Taken(Item toy)
            {
                if (toy.Slot < 0) return;
                foreach (var b in c.Toys)
                    if (b != toy && b.Slot > toy.Slot) { b.Slot = -1; b.Body.isKinematic = false; }
                toy.Slot = -1;
            }

            // Generous help: a ring let go near the peg goes onto it, on top of the others.
            public override void AfterPlace(Item toy, Vector3 handVelocity, ref Vector3 velocity)
            {
                var local = v.top.InverseTransformPoint(toy.Position);
                if (new Vector2(local.x - RingSpot.x, local.z - RingSpot.z).magnitude >= RingHelpRadius) return;
                toy.Slot = TowerTop();
                toy.Transform.SetLocalPositionAndRotation(RingStack(toy.Slot), Quaternion.identity);
                velocity = Vector3.zero;
            }

            // Sent home off the peg: the rings above fall (a ring in a hand stays there).
            public override void Unstack(Item toy)
            {
                foreach (var b in c.Toys) if (b != toy && b.Slot > toy.Slot && toy.Slot >= 0) { b.Slot = -1; if (b.Place != ItemPlace.Held) b.Body.isKinematic = false; }
            }

            public override void Leaving()
            {
                if (Peg != null) Peg.gameObject.SetActive(false); // Unity's null check (not ?.)
            }

            public override void Removed() => Art.Discard(Peg);
        }
    }
}
