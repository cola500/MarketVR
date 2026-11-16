// EPIC-015: who strolls in the park after leaving the counter (or the play table), as rules. A visitor chooses with
// weighted, injected randomness: straight out (5), a detour round the ring to the other gate (3) or a rest on the bench
// (2). The bench has one seat, reserved when chosen and freed when the visitor stands up. At most MaxVisitors at once,
// besides the customer at the counter and the playing children; the caller makes room (Begin throws when full). The
// longer strolls are only chosen when this visitor is the only one, so the park never gets crowded.
using System;
using System.Collections.Generic;

namespace Fruktstand.Core
{
    public enum Stroll { Exit, Detour, Bench }

    public sealed class ParkVisits
    {
        public const int MaxVisitors = 2;
        static readonly (Stroll plan, int weight)[] Weights = { (Stroll.Exit, 5), (Stroll.Detour, 3), (Stroll.Bench, 2) };
        public const int TotalWeight = 10;

        readonly Func<int, int> below;
        readonly int[] seats; // the visitor on each seat, 0 = free
        readonly HashSet<int> active = new();
        int nextId;

        public ParkVisits(Func<int, int> below, int seats = 1)
        {
            this.below = below;
            this.seats = new int[seats];
        }

        public int Active => active.Count;
        public bool Full => active.Count >= MaxVisitors;

        public int FreeSeats
        {
            get
            {
                var n = 0;
                foreach (var s in seats) if (s == 0) n++;
                return n;
            }
        }

        public int Begin()
        {
            if (Full) throw new InvalidOperationException("the park is full: end a visit first");
            active.Add(++nextId);
            return nextId;
        }

        public Stroll Choose(int id)
        {
            var r = below(TotalWeight);
            var plan = Stroll.Exit;
            foreach (var (p, w) in Weights)
            {
                if (r < w)
                {
                    plan = p;
                    break;
                }
                r -= w;
            }
            var room = active.Count < MaxVisitors; // this visitor is the only one
            if (plan == Stroll.Bench && (!room || !TryReserveSeat(id))) plan = Stroll.Detour;
            if (plan == Stroll.Detour && !room) plan = Stroll.Exit;
            return plan;
        }

        bool TryReserveSeat(int id)
        {
            for (var i = 0; i < seats.Length; i++)
                if (seats[i] == 0)
                {
                    seats[i] = id;
                    return true;
                }
            return false;
        }

        public void ReleaseSeat(int id)
        {
            for (var i = 0; i < seats.Length; i++) if (seats[i] == id) seats[i] = 0;
        }

        public void End(int id)
        {
            ReleaseSeat(id);
            active.Remove(id);
        }

        public void Clear()
        {
            active.Clear();
            for (var i = 0; i < seats.Length; i++) seats[i] = 0;
        }
    }
}
