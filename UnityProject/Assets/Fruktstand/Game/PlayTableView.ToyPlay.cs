// One class per toy at the play table (BACKLOG 2). PlayTableView keeps the shared flow (the children's walk and seat,
// control and rest, physics, lost toys, the order of taking, letting go and sending home); a ToyPlay adds what only
// its toy does at the same points, in the same order as before. Each lives in its own file, nested in PlayTableView
// so it can use the view's table, art and items: Ball, Top, Vehicle (car and train), Rings, Blocks and Frogs.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed partial class PlayTableView
    {
        abstract class ToyPlay
        {
            protected readonly PlayTableView v;
            protected readonly Child c;

            protected ToyPlay(PlayTableView view, Child child)
            {
                v = view;
                c = child;
            }

            public static ToyPlay For(PlayTableView view, Child child) => child.Toy switch
            {
                Ware.Ball => new BallPlay(view, child),
                Ware.Top => new TopPlay(view, child),
                Ware.Car => new VehiclePlay(view, child, CarSpeed),
                Ware.Train => new VehiclePlay(view, child, TrainSpeed),
                Ware.StackingRings => new RingsPlay(view, child),
                Ware.Frogs => new FrogsPlay(view, child),
                _ => new BlocksPlay(view, child),
            };

            // The carried set becomes the toy (or toys) on the table.
            public abstract void PutOnTable();

            // Every frame while the child plays, before the child's turn (flights, timers).
            public virtual void Tick(PlayArea area, float dt) { }

            // The player's release lies still and the child takes over.
            public virtual void AtRest() { }

            // The child's play, only while the child has the toy.
            public abstract void Play(float dt);

            // The player took one of its pieces (after it became kinematic and stopped colliding).
            public virtual void Taken(Item toy) { }

            // The player let go: true if this toy handles it all (no physics release afterwards).
            public virtual bool ReleasedInstead(Item toy) => false;

            // The player let go of a piece: true if this toy placed it (then it is not lifted onto the table).
            public virtual bool Place(Item toy, ref Vector3 velocity) => false;

            // After placing or lifting: a last say on the piece and its velocity (a toss, a ring onto the peg).
            public virtual void AfterPlace(Item toy, Vector3 handVelocity, ref Vector3 velocity) { }

            // Sent home (lost, tidied, focus lost): true if this toy handles it all.
            public virtual bool ResetInstead(PlayArea area, Item toy) => false;

            // Sent home: the piece leaves a stack (before its slot is cleared), then it is on its way.
            public virtual void Unstack(Item toy) { }
            public virtual void Returning(Item toy) { }
            public virtual Vector3 HomeFor(Item toy, Vector3 home) => home;
            public virtual void HomeArrived() { }

            public virtual void Tidied() { }
            public virtual void Leaving() { }
            public virtual void Removed() { }

            // The same ball, top, car or train the child carried from the shop becomes the play toy.
            protected Item PutCarried(float radius, Quaternion rotation, System.Action<Item> addBody)
            {
                var item = c.Carried;
                c.Carried = null;
                if (item == null) return null;
                v.items.Remove(item);
                var t = Item.ForToy(item.Transform, c.Toy, radius);
                t.Transform.SetParent(v.top, true);
                t.Transform.localScale = Vector3.one;
                t.Transform.SetLocalPositionAndRotation(Home(c.Toy), rotation);
                addBody(t);
                v.items.Add(t);
                c.Toys.Add(t);
                return t;
            }

            // A set the child carried becomes its pieces: the carried picture goes.
            protected void RemoveCarried()
            {
                if (c.Carried != null) v.Remove(c.Carried);
                c.Carried = null;
            }
        }
    }
}
