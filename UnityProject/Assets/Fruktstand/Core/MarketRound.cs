// EPIC-008: one round at the market, ending in a party and a rest break. Pure rules, no rendering or sound.
//
//   Playing --(earnings this round >= Threshold)--> PartyPending --(ready)--> Celebrating --(CelebrationSeconds)--> Resting
//   Playing / PartyPending --(TakeRest: the menu's "Ta vilopaus")----------------------------------------------> Resting
//   Resting --(StartNextRound: the parent, confirmed)--> Playing          any --(Reset: Nytt spel)--> Playing
//   Resting (a child's rest or a parent's pause, EPIC-012) --(Resume)--> Playing, the same round (nothing reset)
//
// Earnings come only from sales registered by Shop.TryPay (SaleRegistered), so nothing is counted twice and the till
// is never touched. While the party is pending or the market rests no new NPC customers come (Shop asks
// HoldsCustomers) and new phone orders are refused. The party happens at most once per round, and only a threshold
// round earns a star.
namespace Fruktstand.Core
{
    public enum RoundState { Playing, PartyPending, Celebrating, Resting }
    // EPIC-012: why the market rests. A child's rest is ended from the menu (or by the parent); a parent's pause only
    // by the parent; the rest after the party only by starting the next round.
    public enum RestCause { None, Child, Parent, Party }

    public sealed class MarketRound
    {
        public const int DefaultThreshold = 80;      // a first hypothesis, not a measured good length [A]
        public const float CelebrationSeconds = 12f;

        public MarketRound(int threshold = DefaultThreshold)
        {
            Threshold = threshold > 0 ? threshold : DefaultThreshold;
        }

        public int Threshold { get; private set; }

        // Developer/test only (fest.txt on the headset, scene tests): never from the child's controls.
        public void SetThreshold(int threshold)
        {
            if (threshold > 0) Threshold = threshold;
        }
        public RoundState State { get; private set; } = RoundState.Playing;
        public float StateTime { get; private set; }
        public int Earned { get; private set; }              // this round
        public int SalesThisRound { get; private set; }
        public int Stars { get; private set; }               // party stars this session (the party board)
        public float RoundSeconds { get; private set; }      // playing time this round (not counting pauses)
        public float? SecondsToThreshold { get; private set; }
        public int Revision { get; private set; }            // changes with every state change (for the phone status)
        public bool PartiedThisRound { get; private set; }
        public RestCause RestBy { get; private set; }

        // No new customers (and no new phone orders) while the party is pending, on or the market rests.
        public bool HoldsCustomers => State != RoundState.Playing;

        // Called by Shop.TryPay in its single registering transition, with the order's sum.
        public void SaleRegistered(int sum)
        {
            if (sum <= 0) return;
            Earned += sum;
            SalesThisRound++;
            if (State == RoundState.Playing && !PartiedThisRound && Earned >= Threshold)
            {
                SecondsToThreshold = RoundSeconds;
                Enter(RoundState.PartyPending);
            }
        }

        // ready: the shop and the play area are done and the presentation is ready (hands empty, no menu ...);
        // paused: focus lost / a menu open: the party's clock stands still.
        public void Tick(float dt, bool ready, bool paused)
        {
            if (dt <= 0 || paused) return;
            StateTime += dt;
            switch (State)
            {
                case RoundState.Playing:
                    RoundSeconds += dt;
                    break;
                case RoundState.PartyPending when ready:
                    Stars++;
                    PartiedThisRound = true;
                    Enter(RoundState.Celebrating);
                    break;
                case RoundState.Celebrating when StateTime >= CelebrationSeconds:
                    RestBy = RestCause.Party;
                    Enter(RoundState.Resting);
                    break;
            }
        }

        // "Ta vilopaus" (the child, from the menu) or "Pausa spelet" (the parent): the same calm rest, without the
        // threshold and without a star. Not in the middle of a party. A parent pausing a child's rest makes it theirs.
        public bool TakeRest(RestCause by = RestCause.Child)
        {
            if (by is not (RestCause.Child or RestCause.Parent) || State == RoundState.Celebrating) return false;
            if (State == RoundState.Resting)
            {
                if (by != RestCause.Parent || RestBy != RestCause.Child) return false;
                RestBy = RestCause.Parent;
                Revision++;
                return true;
            }
            RestBy = by;
            Enter(RoundState.Resting);
            return true;
        }

        // EPIC-012 "Fortsätt spela" / "Fortsätt spelet": back to the same round, nothing reset. The child may end only
        // its own rest; the parent may end a child's rest or their own pause. The rest after the party needs the next round.
        public bool Resume(RestCause by)
        {
            if (State != RoundState.Resting) return false;
            var allowed = RestBy == RestCause.Child ? by is RestCause.Child or RestCause.Parent : RestBy == RestCause.Parent && by == RestCause.Parent;
            if (!allowed) return false;
            RestBy = RestCause.None;
            Enter(RoundState.Playing);
            return true;
        }

        // "Starta nästa omgång": earnings start over; the till and the stars stay.
        public bool StartNextRound()
        {
            if (State != RoundState.Resting) return false;
            RestBy = RestCause.None;
            NewRound();
            Enter(RoundState.Playing);
            return true;
        }

        // "Nytt spel": the whole session starts over.
        public void Reset()
        {
            Stars = 0;
            RestBy = RestCause.None;
            NewRound();
            Enter(RoundState.Playing);
        }

        void NewRound()
        {
            Earned = 0;
            SalesThisRound = 0;
            RoundSeconds = 0;
            SecondsToThreshold = null;
            PartiedThisRound = false;
        }

        void Enter(RoundState s)
        {
            State = s;
            StateTime = 0;
            Revision++;
        }
    }
}
