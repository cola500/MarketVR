// The building blocks at the play table (EPIC-003, EPIC-011, EPIC-017): six blocks, three the child's and three the
// player's, built together on a shared site in turns. A block let go near the build snaps to the nearest stable cell;
// elsewhere it is never left inside another block or the table (BlockPlacement).
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        public BuildSite Build => (ChildOf(Ware.Blocks)?.Play as BlocksPlay)?.Site;

        // EPIC-017: the first half of the set is the child's, the rest the player's.
        public static bool IsChildsBlock(int index) => index < WareInfo.BlocksInSet / 2;

        sealed class BlocksPlay : ToyPlay
        {
            public BuildSite Site { get; private set; }
            readonly HashSet<Item> playerTouched = new(); // EPIC-017: blocks the child never moves

            public BlocksPlay(PlayTableView view, Child child) : base(view, child) { }

            public override void PutOnTable()
            {
                // The little set the child carried becomes six full-size blocks in the building corner.
                RemoveCarried();
                Site = new BuildSite(BuildSpot, Models.BlockSize);
                for (var i = 0; i < WareInfo.BlocksInSet; i++)
                {
                    var b = Item.ForToy(Models.Block(v.art, v.top, i), Ware.Blocks, Models.BlockSize * 0.8f);
                    b.Index = i;
                    b.Slot = -1; // not in the build
                    b.Transform.localPosition = BlockPile(i);
                    AddBody(b, new Vector3(Models.BlockSize, Models.BlockSize, Models.BlockSize), 0.05f, 0.5f, 0.1f);
                    v.items.Add(b);
                    c.Toys.Add(b);
                }
                v.sounds.Pop();
            }

            // EPIC-017: the player's turn waits while a block is held.
            public override void Tick(PlayArea area, float dt) => Site.Tick(dt, v.AnyToyHeld(Ware.Blocks));

            // EPIC-017: on the child's turn, one of its own blocks into the next free stable cell; then the player's turn.
            public override void Play(float dt)
            {
                if (!Site.ChildMayPlace) return;
                var cell = Site.NextForChild();
                if (cell < 0) return;
                Item next = null;
                foreach (var b in c.Toys)
                    if (IsChildsBlock(b.Index) && !playerTouched.Contains(b) && b.Slot < 0 && b.Place == ItemPlace.Play && !b.Moving
                        && b.Body.linearVelocity.magnitude < RestSpeed && !Carries(b)) { next = b; break; }
                if (next == null) return;
                next.Body.isKinematic = true;
                next.Slot = cell;
                next.MoveTo(Site.Centre(cell), Quaternion.identity, 0.5f);
                Site.Place(cell);
                Site.ChildPlaced();
                c.Figure.Wave(-20);
            }

            // Another block stands on this one (the child never pulls a block out from under another).
            bool Carries(Item block)
            {
                var p = v.top.InverseTransformPoint(block.Position);
                foreach (var o in c.Toys)
                {
                    if (o == block || o.Place == ItemPlace.Held) continue;
                    var q = v.top.InverseTransformPoint(o.Position);
                    if (new Vector2(q.x - p.x, q.z - p.z).magnitude < Models.BlockSize * 0.9f && q.y > p.y + Models.BlockSize * 0.5f && q.y < p.y + Models.BlockSize * 1.5f) return true;
                }
                return false;
            }

            // EPIC-011: the blocks a released or returning block must keep clear of: all but itself, those in a hand and
            // those gone (one rule for letting go and for coming home).
            List<Item> OtherBlocks(Item block) => c.Toys.FindAll(b => b != block && b.Place != ItemPlace.Held && b.Place != ItemPlace.Gone);

            // EPIC-017: whatever stood on a block taken out of the build falls freely.
            void TakeOutOfBuild(Item block)
            {
                if (block.Slot < 0 || Site == null) return;
                var fell = Site.Remove(block.Slot);
                block.Slot = -1;
                foreach (var b in c.Toys)
                    if (b != block && fell.Contains(b.Slot))
                    {
                        b.Slot = -1;
                        if (b.Place != ItemPlace.Held) b.Body.isKinematic = false;
                    }
            }

            public override void Taken(Item toy)
            {
                playerTouched.Add(toy); // EPIC-017: the child leaves it to the player
                TakeOutOfBuild(toy);
            }

            public override bool Place(Item toy, ref Vector3 velocity)
            {
                var cell = Site != null ? Site.Snap(v.top.InverseTransformPoint(toy.Position)) : -1;
                if (cell >= 0)
                {
                    // EPIC-017: near the shared build: the nearest free stable cell, the player's own shape.
                    Debug.Log($"[Fruktstand] block built: cell {cell} (let go at {v.top.InverseTransformPoint(toy.Position):F3})");
                    toy.Transform.SetLocalPositionAndRotation(Site.Centre(cell), Quaternion.identity);
                    toy.Slot = cell;
                    Site.Place(cell);
                    Site.PlayerPlaced();
                    return true;
                }
                // EPIC-011: never left inside another block or the table (the physics engine would shoot it away).
                var blocks = OtherBlocks(toy);
                var others = blocks.ConvertAll(b => v.top.InverseTransformPoint(b.Position));
                var r = BlockPlacement.Resolve(v.top.InverseTransformPoint(toy.Position), others, Models.BlockSize, TableHalf, toy.LastValid);
                if (r.Kind != BlockPlacement.Kind.AsReleased)
                {
                    Debug.Log($"[Fruktstand] block placed: {r.Kind} at {r.Position:F3} (let go at {v.top.InverseTransformPoint(toy.Position):F3})");
                    var yaw = r.Below >= 0 ? (Quaternion.Inverse(v.top.rotation) * blocks[r.Below].Transform.rotation).eulerAngles.y : toy.Transform.localEulerAngles.y;
                    toy.Transform.SetLocalPositionAndRotation(r.Position, Quaternion.Euler(0, yaw, 0));
                    velocity = Vector3.zero;
                }
                return true;
            }

            public override void Unstack(Item toy) => TakeOutOfBuild(toy);

            // EPIC-011: its own place may be taken by another block: the nearest free spot (else its own place).
            public override Vector3 HomeFor(Item toy, Vector3 home)
            {
                var others = OtherBlocks(toy).ConvertAll(b => v.top.InverseTransformPoint(b.Position));
                return BlockPlacement.NearestFree(home, others, Models.BlockSize, TableHalf) ?? home;
            }

            public override void Tidied() => playerTouched.RemoveWhere(t => t.Place != ItemPlace.Held); // back in their owners' piles
        }
    }
}
