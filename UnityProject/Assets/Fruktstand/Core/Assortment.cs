// EPIC-009/010: which wares the stalls offer. Every category shows exactly three different wares out of its catalogue
// (fruit & veg 8, candy 8, toys 6), in bin order (left, middle, right). Pure rules with its own seeded PRNG, so a seed
// gives the same assortments in tests and on the headset.
//
// Redraw picks three new wares in every category (never exactly the same three as now). Market redraws on Nytt spel at
// once, and asks for a redraw on the next round (RequestRedraw); Shop applies it when the next customer comes and no
// phone order is open, so an order in progress (NPC or phone) is never left with wares that are no longer on sale.
// The play area never looks here: a child keeps playing with a toy that is no longer on sale.
using System;
using System.Collections.Generic;

namespace Fruktstand.Core
{
    public sealed class Assortment
    {
        public const int ActivePerCategory = 3;
        public static readonly Ware[] Classic = { Ware.Apple, Ware.Banana, Ware.Carrot }; // EPIC-001..008's fruit stall

        // The stalls before EPIC-009/010: the first three of each catalogue (apple, banana, carrot; lollipop,
        // polkagris, candy bag; ball, car, blocks).
        public static Ware[] ClassicOf(Category c)
        {
            var all = WareInfo.Catalogue(c);
            return new[] { all[0], all[1], all[2] };
        }

        readonly Prng random;
        readonly Ware[][] active = new Ware[WareInfo.Categories.Length][];

        // start: three different wares per category to begin with (bin order). Categories not given keep their classic
        // wares; nothing given at all = draw every category from the seed.
        public Assortment(int seed, params Ware[] start)
        {
            random = new Prng(seed);
            if (start == null || start.Length == 0)
            {
                foreach (var c in WareInfo.Categories) active[(int)c] = Draw(c, null);
                return;
            }
            foreach (var c in WareInfo.Categories)
            {
                var given = new List<Ware>();
                foreach (var w in start) if (WareInfo.CategoryOf(w) == c) given.Add(w);
                if (given.Count == 0) active[(int)c] = ClassicOf(c);
                else
                {
                    Validate(c, given);
                    active[(int)c] = given.ToArray();
                }
            }
        }

        public int Revision { get; private set; }  // changes with every redraw (views and the phone status follow it)
        public bool RedrawPending { get; private set; }
        public Ware FirstFruit => active[(int)Category.FruitVeg][0];

        // The offered wares of a category, in bin order (a copy).
        public Ware[] Active(Category c) => (Ware[])active[(int)c].Clone();

        public bool IsActive(Ware w) => Bin(w) >= 0;

        // Bin 0..2 of an offered ware within its stall, or -1 if it is not on sale now.
        public int Bin(Ware w) => Array.IndexOf(active[(int)WareInfo.CategoryOf(w)], w);

        public void Redraw()
        {
            foreach (var c in WareInfo.Categories) active[(int)c] = Draw(c, active[(int)c]);
            RedrawPending = false;
            Revision++;
        }

        public void RequestRedraw() => RedrawPending = true;

        Ware[] Draw(Category c, Ware[] previous)
        {
            var catalogue = WareInfo.Catalogue(c);
            for (var guard = 0; ; guard++)
            {
                var pool = (Ware[])catalogue.Clone();
                for (var i = 0; i < ActivePerCategory; i++) // partial Fisher–Yates
                {
                    var j = i + random.Below(pool.Length - i);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }
                var drawn = new[] { pool[0], pool[1], pool[2] };
                if (previous == null || !SameSet(drawn, previous) || guard >= 20) return drawn;
            }
        }

        static bool SameSet(Ware[] a, Ware[] b)
        {
            foreach (var w in a) if (Array.IndexOf(b, w) < 0) return false;
            return true;
        }

        static void Validate(Category c, List<Ware> given)
        {
            if (given.Count != ActivePerCategory) throw new ArgumentException($"exactly {ActivePerCategory} {c} wares, not {given.Count}");
            for (var i = 0; i < given.Count; i++)
                if (given.IndexOf(given[i]) != i) throw new ArgumentException($"{given[i]} twice");
        }
    }
}
