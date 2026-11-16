// The market's shared till (kassan, EPIC-003 SPEC §15): whole coins, never negative, never debited. Credited only by
// Shop.TryPay with the order's sum, once per registered sale. In memory only: no saving between app launches.
namespace Fruktstand.Core
{
    public sealed class Wallet
    {
        public int Balance { get; private set; }
        public int Earned => Balance;
        public int Revision { get; private set; }

        internal void Earn(int coins)
        {
            if (coins <= 0) return;
            Balance += coins;
            Revision++;
        }

        public void Reset()
        {
            Balance = 0;
            Revision++;
        }
    }
}
