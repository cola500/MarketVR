// The basket on the counter: a few fixed slots. Items land in a slot (snapping), can be taken out again,
// and the contents are compared with the order by count per kind.
using System;

namespace Fruktstand.Core
{
    public sealed class Basket
    {
        public const int Capacity = 4; // a full order (max 3) plus room for one wrong item

        readonly Ware?[] slots = new Ware?[Capacity];

        public int Revision { get; private set; }
        public Ware? this[int slot] => slots[slot];
        public bool IsFull => FreeSlots == 0;
        public bool IsEmpty => FreeSlots == Capacity;

        public int FreeSlots
        {
            get
            {
                var n = 0;
                foreach (var s in slots) if (!s.HasValue) n++;
                return n;
            }
        }

        public int Count(Ware p)
        {
            var n = 0;
            foreach (var s in slots) if (s == p) n++;
            return n;
        }

        public int Total => Capacity - FreeSlots;

        // Puts the item in `preferredSlot` if free, otherwise the lowest free slot. Returns the slot, or -1 if full.
        public int Add(Ware p, int preferredSlot = -1)
        {
            var slot = preferredSlot >= 0 && preferredSlot < Capacity && !slots[preferredSlot].HasValue ? preferredSlot : Array.FindIndex(slots, s => !s.HasValue);
            if (slot < 0) return -1;
            slots[slot] = p;
            Revision++;
            return slot;
        }

        public Ware? Remove(int slot)
        {
            if (slot < 0 || slot >= Capacity || !slots[slot].HasValue) return null;
            var p = slots[slot];
            slots[slot] = null;
            Revision++;
            return p;
        }

        public void Clear()
        {
            if (IsEmpty) return;
            Array.Clear(slots, 0, Capacity);
            Revision++;
        }

        public bool Matches(Order order)
        {
            if (order == null) return false;
            foreach (var p in WareInfo.All)
                if (Count(p) != order.Count(p)) return false;
            return true;
        }

        // How many of the ordered units of `p` are in the basket (never more than ordered): one tick per unit.
        public int Satisfied(Order order, Ware p) => order == null ? 0 : Math.Min(Count(p), order.Count(p));
    }
}
