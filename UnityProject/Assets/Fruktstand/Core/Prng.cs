// Small seeded PRNG (mulberry32): the same sequence in the editor, tests and IL2CPP on Quest. Used by OrderGenerator
// and Assortment so their sequences are reproducible from a seed.
namespace Fruktstand.Core
{
    public sealed class Prng
    {
        uint state;

        public Prng(int seed) => state = (uint)seed;

        public uint Next()
        {
            unchecked
            {
                state += 0x6D2B79F5;
                var t = state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return t ^ (t >> 14);
            }
        }

        // 0 .. n-1
        public int Below(int n) => (int)(Next() % (uint)n);
    }
}
