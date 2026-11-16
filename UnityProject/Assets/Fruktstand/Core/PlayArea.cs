// The play area's rules (EPIC-003 SPEC §17, EPIC-010 §27): three play spots (zones) on the table: left (ball, top or frogs),
// middle by the road (car or train) and right (blocks or stacking rings). One child and one toy per spot, so at most
// three children play at once. A spot remembers which toy it holds, so a toy that is no longer on sale plays on.
// Pure logic, unit-tested.
//
//   Free --(TryReserve: a toy order is made)--> Reserved --(Sold: paid)--> Walking --(WalkSeconds)--> Playing
//   Playing --(PlaySeconds, but never while the player holds the toy or touched it within LingerSeconds)--> Leaving
//   Leaving --(LeaveSeconds: the child goes, the toy goes too)--> Free
//
// Control of a playing toy is exclusive: Child (the child moves it), Player (in the player's hand) or Free (rolling
// after the player let go). The child takes over again only when the view reports the toy has come to rest.
namespace Fruktstand.Core
{
    public enum SpotState { Free, Reserved, Walking, Playing, Leaving }

    public enum ToyControl { None, Child, Player, Free }

    public sealed class PlayArea
    {
        public const int Spots = 3;
        public const float WalkSeconds = 3f;
        public const float PlaySeconds = 45f;
        public const float LingerSeconds = 8f; // after the player last touched the toy, the child keeps playing
        public const float LeaveSeconds = 2f;

        readonly SpotState[] state = new SpotState[Spots];
        readonly float[] time = new float[Spots];
        readonly ToyControl[] control = new ToyControl[Spots];
        readonly float[] sincePlayer = new float[Spots];
        readonly int[] child = new int[Spots]; // which customer number plays here (for the figure's looks)

        readonly Ware?[] toyIn = new Ware?[Spots];

        public int Revision { get; private set; }

        // The zone a toy is played in.
        public static int SpotOf(Ware toy) => toy switch
        {
            Ware.Ball or Ware.Top or Ware.Frogs => 0,
            Ware.Car or Ware.Train => 1,
            Ware.Blocks or Ware.StackingRings => 2,
            _ => -1,
        };

        // Physics bodies a toy brings to the table (the rings' peg is fixed, without a body).
        public static int BodiesOf(Ware toy) => toy switch
        {
            Ware.Blocks => WareInfo.BlocksInSet,
            Ware.StackingRings => WareInfo.RingsInSet,
            Ware.Frogs => 0, // EPIC-018: the frogs hop in computed arcs, without bodies
            _ => WareInfo.IsToy(toy) ? 1 : 0,
        };

        // The physics budget: the biggest toy of each zone, whatever is on sale (1 + 1 + 6 = 8).
        public static readonly int MaxBodies = ComputeMaxBodies();

        static int ComputeMaxBodies()
        {
            var perSpot = new int[Spots];
            foreach (var t in WareInfo.Toys) perSpot[SpotOf(t)] = System.Math.Max(perSpot[SpotOf(t)], BodiesOf(t));
            var sum = 0;
            foreach (var n in perSpot) sum += n;
            return sum;
        }

        // The toy at a spot (reserved, on its way, played with or leaving), or null when the spot is free.
        public Ware? ToyIn(int spot) => toyIn[spot];

        bool Holds(Ware toy) => WareInfo.IsToy(toy) && toyIn[SpotOf(toy)] == toy;

        // The state of this toy at the table: Free unless its zone holds exactly this toy.
        public SpotState State(Ware toy) => Holds(toy) ? state[SpotOf(toy)] : SpotState.Free;
        public float StateTime(Ware toy) => Holds(toy) ? time[SpotOf(toy)] : 0;
        public ToyControl Control(Ware toy) => Holds(toy) ? control[SpotOf(toy)] : ToyControl.None;
        public int Child(Ware toy) => Holds(toy) ? child[SpotOf(toy)] : 0;

        // Its zone is free, so this toy can be ordered (and the zone reserved).
        public bool IsFree(Ware toy) => WareInfo.IsToy(toy) && state[SpotOf(toy)] == SpotState.Free;

        // EPIC-008: a child is still on the way to the table with a toy just bought (the party waits for that).
        public bool AnyWalking
        {
            get
            {
                foreach (var s in state) if (s == SpotState.Walking) return true;
                return false;
            }
        }

        public int Children
        {
            get
            {
                var n = 0;
                foreach (var s in state) if (s is SpotState.Walking or SpotState.Playing or SpotState.Leaving) n++;
                return n;
            }
        }

        // A toy order is about to be offered: hold its spot. False if taken.
        public bool TryReserve(Ware toy, int customerNumber)
        {
            if (!IsFree(toy)) return false;
            toyIn[SpotOf(toy)] = toy;
            Enter(SpotOf(toy), SpotState.Reserved);
            child[SpotOf(toy)] = customerNumber;
            return true;
        }

        // The reservation is no longer needed (new game). A sold or playing toy is never cancelled this way.
        public void CancelReservation(Ware toy)
        {
            if (State(toy) == SpotState.Reserved) Free(SpotOf(toy));
        }

        // Paid: the child walks to the spot carrying the toy.
        public bool Sold(Ware toy)
        {
            if (State(toy) != SpotState.Reserved) return false;
            Enter(SpotOf(toy), SpotState.Walking);
            return true;
        }

        public void PlayerTook(Ware toy)
        {
            if (State(toy) != SpotState.Playing && State(toy) != SpotState.Leaving) return;
            var s = SpotOf(toy);
            control[s] = ToyControl.Player;
            sincePlayer[s] = 0;
            Revision++;
        }

        public void PlayerReleased(Ware toy)
        {
            if (Control(toy) != ToyControl.Player) return;
            var s = SpotOf(toy);
            control[s] = ToyControl.Free;
            sincePlayer[s] = 0;
            Revision++;
        }

        // The view reports the released toy is lying still where the child can reach it: the child takes over.
        public void ToyAtRest(Ware toy)
        {
            if (Control(toy) != ToyControl.Free || State(toy) != SpotState.Playing) return;
            control[SpotOf(toy)] = ToyControl.Child;
            Revision++;
        }

        public bool ChildMayAct(Ware toy) => State(toy) == SpotState.Playing && Control(toy) == ToyControl.Child;

        public void Tick(float dt)
        {
            if (dt <= 0) return;
            for (var s = 0; s < Spots; s++)
            {
                time[s] += dt;
                if (control[s] != ToyControl.Player) sincePlayer[s] += dt;
                switch (state[s])
                {
                    case SpotState.Walking when time[s] >= WalkSeconds:
                        Enter(s, SpotState.Playing);
                        control[s] = ToyControl.Child;
                        sincePlayer[s] = LingerSeconds;
                        break;
                    case SpotState.Playing when time[s] >= PlaySeconds && control[s] != ToyControl.Player && sincePlayer[s] >= LingerSeconds:
                        Enter(s, SpotState.Leaving); // a calm goodbye; never while the player is playing along
                        break;
                    case SpotState.Leaving when control[s] == ToyControl.Player:
                        time[s] = 0; // never take the toy out of the player's hand
                        break;
                    case SpotState.Leaving when time[s] >= LeaveSeconds:
                        Free(s);
                        break;
                }
            }
        }

        public void Reset()
        {
            for (var s = 0; s < Spots; s++)
            {
                Free(s);
                child[s] = 0;
            }
        }

        void Free(int s)
        {
            Enter(s, SpotState.Free);
            control[s] = ToyControl.None;
            toyIn[s] = null;
        }

        void Enter(int s, SpotState st)
        {
            state[s] = st;
            time[s] = 0;
            Revision++;
        }
    }
}
