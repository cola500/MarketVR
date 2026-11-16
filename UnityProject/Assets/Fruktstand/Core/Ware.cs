// The market's shared catalogue (EPIC-003, SPEC §15): every ware has a category, a price, a name and a picture
// (Models.Ware). Fruit & veg and candy have eight wares, toys seven (EPIC-009/010/018); the Assortment offers three of each
// category at a time, so every stall has the same three bins. Append new values, never reorder (orders and tests use the
// indices).
namespace Fruktstand.Core
{
    public enum Category { FruitVeg = 0, Candy = 1, Toys = 2 }

    public enum Ware
    {
        Apple = 0, Banana = 1, Carrot = 2,          // Frukt & grönt
        Lollipop = 3, Polkagris = 4, CandyBag = 5,  // Godis
        Ball = 6, Car = 7, Blocks = 8,              // Leksaker (Blocks = a set of six)
        Pear = 9, Orange = 10, Strawberry = 11, Grapes = 12, Watermelon = 13, // Frukt & grönt (EPIC-009)
        GummyBear = 14, Chocolate = 15, WrappedCandy = 16, RainbowStrip = 17, CandyRing = 18, // Godis (EPIC-010)
        Top = 19, Train = 20, StackingRings = 21,  // Leksaker (EPIC-010; StackingRings = three rings and a peg)
        Frogs = 22,                                // Leksaker (EPIC-018; two hopping frogs and a lily pad)
    }

    public static class WareInfo
    {
        public const int Count = 23;
        public const int BlocksInSet = 6;
        public const int RingsInSet = 3;

        public static readonly Ware[] All =
        {
            Ware.Apple, Ware.Banana, Ware.Carrot, Ware.Lollipop, Ware.Polkagris, Ware.CandyBag, Ware.Ball, Ware.Car, Ware.Blocks,
            Ware.Pear, Ware.Orange, Ware.Strawberry, Ware.Grapes, Ware.Watermelon,
            Ware.GummyBear, Ware.Chocolate, Ware.WrappedCandy, Ware.RainbowStrip, Ware.CandyRing, Ware.Top, Ware.Train, Ware.StackingRings, Ware.Frogs,
        };

        public static readonly Category[] Categories = { Category.FruitVeg, Category.Candy, Category.Toys };
        static readonly Ware[] FruitVeg =
        {
            Ware.Apple, Ware.Banana, Ware.Carrot, Ware.Pear, Ware.Orange, Ware.Strawberry, Ware.Grapes, Ware.Watermelon,
        };
        static readonly Ware[] Candy =
        {
            Ware.Lollipop, Ware.Polkagris, Ware.CandyBag, Ware.GummyBear, Ware.Chocolate, Ware.WrappedCandy, Ware.RainbowStrip, Ware.CandyRing,
        };
        public static readonly Ware[] Toys = { Ware.Ball, Ware.Car, Ware.Blocks, Ware.Top, Ware.Train, Ware.StackingRings, Ware.Frogs };

        public static Category CategoryOf(Ware w) => w switch
        {
            Ware.Lollipop or Ware.Polkagris or Ware.CandyBag or Ware.GummyBear or Ware.Chocolate or Ware.WrappedCandy or Ware.RainbowStrip or Ware.CandyRing => Category.Candy,
            Ware.Ball or Ware.Car or Ware.Blocks or Ware.Top or Ware.Train or Ware.StackingRings or Ware.Frogs => Category.Toys,
            _ => Category.FruitVeg,
        };

        public static bool IsToy(Ware w) => CategoryOf(w) == Category.Toys;

        // Every ware of a category (a copy). What a stall offers now is Assortment.Active.
        public static Ware[] Catalogue(Category c) => (Ware[])(c switch
        {
            Category.FruitVeg => FruitVeg,
            Category.Candy => Candy,
            _ => Toys,
        }).Clone();

        public static int Price(Ware w) => w switch
        {
            Ware.Apple or Ware.Banana or Ware.Carrot or Ware.Pear or Ware.Orange or Ware.Strawberry or Ware.Grapes or Ware.Watermelon => 1,
            Ware.Lollipop or Ware.GummyBear or Ware.WrappedCandy => 1,
            Ware.Polkagris or Ware.RainbowStrip or Ware.CandyRing => 2,
            Ware.CandyBag or Ware.Chocolate => 3,
            Ware.Ball or Ware.Top => 2,
            Ware.Car or Ware.StackingRings or Ware.Frogs => 3,
            Ware.Blocks or Ware.Train => 4,
            _ => throw new System.ArgumentOutOfRangeException(nameof(w)),
        };

        public static string Name(Ware w) => w switch
        {
            Ware.Apple => "äpple",
            Ware.Banana => "banan",
            Ware.Carrot => "morot",
            Ware.Lollipop => "klubba",
            Ware.Polkagris => "polkagris",
            Ware.CandyBag => "godispåse",
            Ware.Pear => "päron",
            Ware.Orange => "apelsin",
            Ware.Strawberry => "jordgubbe",
            Ware.Grapes => "vindruvsklase",
            Ware.Watermelon => "vattenmelonskiva",
            Ware.GummyBear => "gelébjörn",
            Ware.Chocolate => "chokladkaka",
            Ware.WrappedCandy => "karamell",
            Ware.RainbowStrip => "regnbågsrem",
            Ware.CandyRing => "godisring",
            Ware.Top => "snurra",
            Ware.Train => "leksakståg",
            Ware.StackingRings => "stapelringar",
            Ware.Ball => "boll",
            Ware.Car => "bil",
            Ware.Blocks => "byggklossar",
            Ware.Frogs => "hoppgrodor",
            _ => w.ToString(),
        };

        public static string Name(Category c) => c switch
        {
            Category.FruitVeg => "Frukt & grönt",
            Category.Candy => "Godis",
            _ => "Leksaker",
        };
    }
}
