// Deterministic sequence of orders for the whole market (EPIC-003). Customer 1 wants one of the first offered fruit
// (EPIC-009: of the Assortment; an apple with the classic stall). After that
// the categories take turns: fruit/veg -> candy -> toys; a toy order is only made when `toyFree` says that toy's play
// spot can be reserved (otherwise another free toy, or the next category). Fruit/veg 1–3 items (customers 2–3 at
// most 2), candy 1–2 items, toys exactly one. Only wares the Assortment offers now. Two customers in a row never want exactly the same.
// Own Prng so the sequence is identical in the editor, tests and IL2CPP on Quest.
using System;
using System.Collections.Generic;

namespace Fruktstand.Core
{
    public sealed class OrderGenerator
    {
        readonly int seed;
        readonly Assortment assortment;
        Prng random;
        Order previous;
        int turn; // index into the category rotation

        public OrderGenerator(int seed = 1, Assortment assortment = null)
        {
            this.seed = seed;
            this.assortment = assortment ?? new Assortment(seed, Assortment.Classic);
            Reset();
        }

        public void Reset()
        {
            random = new Prng(seed);
            previous = null;
            turn = 0;
        }

        public static int MaxTotalFor(int customerNumber) => customerNumber <= 1 ? 1 : customerNumber <= 3 ? 2 : Order.MaxTotal;

        // customerNumber is 1-based. toyFree(toy) tells whether that toy's play spot is free (null = no play area).
        public Order Next(int customerNumber, Func<Ware, bool> toyFree = null)
        {
            Order order;
            if (customerNumber <= 1) order = Order.Of(assortment.FirstFruit);
            else
            {
                order = null;
                for (var tries = 0; tries < WareInfo.Categories.Length && order == null; tries++)
                {
                    var category = WareInfo.Categories[turn % WareInfo.Categories.Length];
                    turn++;
                    order = Make(category, customerNumber, toyFree);
                }
                order ??= Make(Category.FruitVeg, customerNumber, null);
            }
            previous = order;
            return order;
        }

        Order Make(Category category, int customerNumber, Func<Ware, bool> toyFree)
        {
            if (category == Category.Toys)
            {
                var free = new List<Ware>();
                foreach (var t in assortment.Active(Category.Toys))
                    if (toyFree != null && toyFree(t)) free.Add(t);
                if (free.Count == 0) return null;
                var start = random.Below(free.Count);
                for (var i = 0; i < free.Count; i++)
                {
                    var o = Order.Of(free[(start + i) % free.Count]);
                    if (!o.Equals(previous) || free.Count == 1) return o;
                }
                return null;
            }
            var max = category == Category.Candy ? Math.Min(2, MaxTotalFor(customerNumber)) : MaxTotalFor(customerNumber);
            var wares = assortment.Active(category);
            Order order;
            var guard = 0;
            do
            {
                var total = 1 + random.Below(max);
                var c = new int[WareInfo.Count];
                for (var i = 0; i < total; i++) c[(int)wares[random.Below(wares.Length)]]++;
                order = new Order(c);
            }
            while (order.Equals(previous) && ++guard < 50);
            return order;
        }

    }
}
