// What one customer wants: a count per ware, all from one category (EPIC-003: one category per order).
// Immutable value; the order of items never matters. Sum is what the payment token is worth.
using System;
using System.Text;

namespace Fruktstand.Core
{
    public sealed class Order : IEquatable<Order>
    {
        public const int MinTotal = 1;
        public const int MaxTotal = 3;

        readonly int[] counts = new int[WareInfo.Count];

        public Order(int[] wareCounts)
        {
            if (wareCounts == null || wareCounts.Length != WareInfo.Count) throw new ArgumentException("one count per ware");
            Category? category = null;
            for (var i = 0; i < WareInfo.Count; i++)
            {
                if (wareCounts[i] < 0) throw new ArgumentOutOfRangeException(nameof(wareCounts), "negative count");
                counts[i] = wareCounts[i];
                if (counts[i] == 0) continue;
                var c = WareInfo.CategoryOf((Ware)i);
                if (category.HasValue && category != c) throw new ArgumentException("an order has one category");
                category = c;
            }
            if (Total < MinTotal || Total > MaxTotal) throw new ArgumentOutOfRangeException(nameof(wareCounts), $"total {Total} outside {MinTotal}..{MaxTotal}");
            Category = category ?? Category.FruitVeg;
        }

        public static Order Of(params Ware[] items)
        {
            var c = new int[WareInfo.Count];
            foreach (var w in items) c[(int)w]++;
            return new Order(c);
        }

        public Category Category { get; }
        public int Count(Ware w) => counts[(int)w];

        public int Total
        {
            get
            {
                var n = 0;
                foreach (var c in counts) n += c;
                return n;
            }
        }

        public int Sum
        {
            get
            {
                var s = 0;
                foreach (var w in WareInfo.All) s += counts[(int)w] * WareInfo.Price(w);
                return s;
            }
        }

        // Number of different wares in the order (rows in the speech bubble).
        public int Kinds
        {
            get
            {
                var n = 0;
                foreach (var c in counts) if (c > 0) n++;
                return n;
            }
        }

        // The toy a toy order is for (toy orders hold exactly one toy).
        public Ware? Toy
        {
            get
            {
                if (Category != Category.Toys) return null;
                foreach (var w in WareInfo.Toys) if (Count(w) > 0) return w;
                return null;
            }
        }

        public bool Equals(Order other)
        {
            if (other == null) return false;
            for (var i = 0; i < WareInfo.Count; i++) if (counts[i] != other.counts[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as Order);

        public override int GetHashCode()
        {
            var h = 17;
            foreach (var c in counts) h = h * 31 + c;
            return h;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var w in WareInfo.All)
                if (Count(w) > 0) sb.Append(sb.Length > 0 ? ", " : "").Append(Count(w)).Append(' ').Append(WareInfo.Name(w));
            return sb.Append(" (").Append(Sum).Append(" mynt)").ToString();
        }
    }
}
