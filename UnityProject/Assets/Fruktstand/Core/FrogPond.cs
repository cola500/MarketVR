// EPIC-018 (SPEC §34): the hopping frogs at the play table, one the child's and one the player's. Pure logic in table
// space (y = 0 on the table top, the player's side is -z, the child's +z; yaw in degrees about up, 0 faces +z);
// unit-tested. The frogs have no physics bodies: a hop is this fixed arc.
//
//   Sitting --Press--> Pressed --Release--> Flying --(HopSeconds)--> Sitting (on the table) or Fallen (off it)
//   Pressed --Cancel (pause, menu, focus)--> Sitting, no hop.   Fallen --Reset (the lost-toy rule)--> Sitting at home.
//
// A hop goes HopLength the way the frog faces. Both homes are one hop from the lily pad, facing it, so a first press
// lands on the pad. Landing within PadRadius + PadHelp counts as on the pad and is pulled onto it.
// Turns: the child shows a hop after ChildBeatSeconds, then it is the player's turn for up to PlayerTurnSeconds (still
// while the player holds or presses a frog). The player may hop any time; the child waits ChildBeatSeconds after the
// player's latest hop.
using UnityEngine;

namespace Fruktstand.Core
{
    public enum FrogOwner { Child = 0, Player = 1 }
    public enum FrogPhase { Sitting, Pressed, Flying, Fallen }
    public enum FrogTurn { Child, Player }

    public sealed class FrogPond
    {
        public const float HopLength = 0.16f, HopHeight = 0.07f, HopSeconds = 0.5f;
        public const float PadRadius = 0.08f, PadHelp = 0.05f;
        public const float ChildBeatSeconds = 2.5f, PlayerTurnSeconds = 8f;
        public const float Rim = 0.02f;
        public const float FrogsApart = 0.07f; // two frogs' centres at least this far apart on the pad
        public static readonly Vector3 PadCentre = new(-0.27f, 0, 0.02f);

        public static Vector3 Home(FrogOwner o) => PadCentre + Vector3.forward * (o == FrogOwner.Player ? -HopLength : HopLength);
        public static float HomeYaw(FrogOwner o) => o == FrogOwner.Player ? 0 : 180;
        public static Vector3 Facing(float yaw) => new(Mathf.Sin(yaw * Mathf.Deg2Rad), 0, Mathf.Cos(yaw * Mathf.Deg2Rad));

        sealed class Frog
        {
            public FrogPhase Phase;
            public Vector3 At, From, To;
            public float Yaw, Time;
            public bool OnPad, ToPad, OffTable;
            public int Hops, PadLandings;
        }

        readonly Vector2 tableHalf;
        readonly Frog[] frogs = { new(), new() };
        float timer;

        // A landing: who, on the pad, off the table (for the quack, the ring and the fall).
        public event System.Action<FrogOwner, bool, bool> Landed;

        public FrogPond(Vector2 tableHalf)
        {
            this.tableHalf = tableHalf;
            Reset(FrogOwner.Child);
            Reset(FrogOwner.Player);
        }

        Frog F(FrogOwner o) => frogs[(int)o];

        public FrogPhase Phase(FrogOwner o) => F(o).Phase;
        public float Yaw(FrogOwner o) => F(o).Yaw;
        public int Hops(FrogOwner o) => F(o).Hops;
        public int PadLandings(FrogOwner o) => F(o).PadLandings;
        public bool OnPad(FrogOwner o) => F(o).OnPad;

        public Vector3 Position(FrogOwner o)
        {
            var f = F(o);
            if (f.Phase != FrogPhase.Flying) return f.At;
            var s = Mathf.Clamp01(f.Time / HopSeconds);
            return Vector3.Lerp(f.From, f.To, s) + Vector3.up * (4 * HopHeight * s * (1 - s));
        }

        public bool Press(FrogOwner o)
        {
            if (F(o).Phase != FrogPhase.Sitting) return false;
            F(o).Phase = FrogPhase.Pressed;
            return true;
        }

        public bool Release(FrogOwner o)
        {
            var f = F(o);
            if (f.Phase != FrogPhase.Pressed) return false;
            f.Phase = FrogPhase.Flying;
            f.Time = 0;
            f.From = f.At;
            var to = f.At + Facing(f.Yaw) * HopLength;
            var offset = to - PadCentre;
            offset.y = 0;
            f.ToPad = offset.magnitude <= PadRadius + PadHelp;
            if (f.ToPad && offset.magnitude > PadRadius) to = PadCentre + offset.normalized * (PadRadius * 0.7f);
            if (f.ToPad) to = BesideTheOther(o, to);
            f.OffTable = Mathf.Abs(to.x) > tableHalf.x - Rim || Mathf.Abs(to.z) > tableHalf.y - Rim;
            f.To = to;
            f.OnPad = false;
            f.Hops++;
            timer = 0;
            Whose = o == FrogOwner.Child ? FrogTurn.Player : FrogTurn.Child;
            return true;
        }

        // The other frog sits on the pad where this one would land: land beside it, across the hop, still on the pad.
        Vector3 BesideTheOther(FrogOwner o, Vector3 to)
        {
            var other = F(o == FrogOwner.Child ? FrogOwner.Player : FrogOwner.Child);
            if (other.Phase is FrogPhase.Flying or FrogPhase.Fallen || Flat(to - other.At).magnitude >= FrogsApart) return to;
            var across = Vector3.Cross(Vector3.up, Facing(F(o).Yaw));
            if (Vector3.Dot(Flat(to - other.At), across) < 0) across = -across;
            var beside = other.At + across * FrogsApart;
            if (Flat(beside - PadCentre).magnitude > PadRadius) beside = other.At - across * FrogsApart; // the other side
            return new Vector3(beside.x, 0, beside.z);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        public void Cancel(FrogOwner o)
        {
            if (F(o).Phase == FrogPhase.Pressed) F(o).Phase = FrogPhase.Sitting;
        }

        // Set down by a hand (lifted and turned to aim), or fetched by the child.
        public void Place(FrogOwner o, Vector3 at, float yaw)
        {
            var f = F(o);
            f.At = new Vector3(at.x, 0, at.z);
            f.Yaw = Mathf.Repeat(yaw, 360);
            f.Phase = FrogPhase.Sitting;
            f.OnPad = false;
        }

        public void Reset(FrogOwner o) => Place(o, Home(o), HomeYaw(o));

        // Let go of off the table by a hand: it falls (the lost-toy rule brings it back with Reset).
        public void Fall(FrogOwner o, Vector3 at)
        {
            var f = F(o);
            f.At = at;
            f.Phase = FrogPhase.Fallen;
            f.OnPad = false;
        }

        public FrogTurn Whose { get; private set; } = FrogTurn.Child;
        public bool ChildMayHop => Whose == FrogTurn.Child && timer >= ChildBeatSeconds && F(FrogOwner.Child).Phase == FrogPhase.Sitting;

        public void Tick(float dt, bool playerBusy)
        {
            if (dt <= 0) return;
            foreach (FrogOwner o in new[] { FrogOwner.Child, FrogOwner.Player })
            {
                var f = F(o);
                if (f.Phase != FrogPhase.Flying) continue;
                f.Time += dt;
                if (f.Time < HopSeconds) continue;
                f.At = f.To;
                f.Phase = f.OffTable ? FrogPhase.Fallen : FrogPhase.Sitting;
                f.OnPad = f.ToPad && !f.OffTable;
                if (f.OnPad) f.PadLandings++;
                Landed?.Invoke(o, f.OnPad, f.OffTable);
            }
            if (Whose == FrogTurn.Player && playerBusy) return;
            timer += dt;
            if (Whose == FrogTurn.Player && timer >= PlayerTurnSeconds)
            {
                Whose = FrogTurn.Child; // the player did not hop: the child goes on
                timer = ChildBeatSeconds;
            }
        }
    }
}
