// EPIC-016: when to give the "a customer is here" pling, as a rule. Exactly once per customer turn, the moment the
// customer is ready to order (Ordering), for NPCs and the phone customer alike. Muted (a menu, the system menu, focus
// lost, a rest): the turn counts as announced without a sound, so nothing old is played once the pause ends; the
// waiting marker (Waiting) still shows who is there. Töm korgen (AwaitingPayment -> Ordering) is the same customer.
// Nytt spel starts the customer numbers over, and customer 1 again is a new customer.
namespace Fruktstand.Core
{
    public sealed class ArrivalChime
    {
        int announced; // the last customer number whose arrival was announced (or silently passed)

        // True on the one update where the pling should play.
        public bool Update(SaleState state, int customer, bool muted)
        {
            if (customer < announced) announced = 0; // a new game
            if (state != SaleState.Ordering || customer == announced) return false;
            announced = customer;
            return !muted;
        }

        // A customer stands at the counter waiting to be served: from ready to order until the coin is paid.
        public static bool Waiting(SaleState state) => state is SaleState.Ordering or SaleState.AwaitingPayment;
    }
}
