// Everything in one session of the market (EPIC-003): the shared till, the play area and the shop (one active order
// at a time across the stalls). NewGame is the only thing that empties the till. EPIC-009: the Assortment of fruit is
// drawn anew on NewGame and for the next round.
namespace Fruktstand.Core
{
    public sealed class Market
    {
        public Market(int seed = 1, int threshold = MarketRound.DefaultThreshold, Assortment assortment = null)
        {
            Assortment = assortment ?? new Assortment(seed, Assortment.Classic);
            Round = new MarketRound(threshold);
            Wallet = new Wallet();
            PlayArea = new PlayArea();
            Remote = new RemoteCustomer();
            Shop = new Shop(seed, Wallet, PlayArea, Remote, Round, Assortment);
            Remote.Offers = Assortment.IsActive; // EPIC-009: the phone may only order what is on sale
            Remote.PausedWhen = () => Round.HoldsCustomers;
        }

        public Assortment Assortment { get; } // EPIC-009: three of the eight fruit/veg wares at a time

        public MarketRound Round { get; } // EPIC-008

        public RemoteCustomer Remote { get; }

        public Wallet Wallet { get; }
        public PlayArea PlayArea { get; }
        public Shop Shop { get; }

        // presentationReady: hands empty, no menu/calibration (only FruitStand knows; the safe default is "not ready",
        // so a plain Tick never starts a party); paused: focus lost or a menu open.
        public void Tick(float dt, bool presentationReady = false, bool paused = false)
        {
            var resting = Round.State is RoundState.Celebrating or RoundState.Resting;
            if (!resting)
            {
                Shop.Tick(dt);
                PlayArea.Tick(dt); // the children's play stands still during the party and the rest
            }
            var shopDone = Shop.State == SaleState.Closed && Remote.ActiveId == null && !PlayArea.AnyWalking;
            Round.Tick(dt, shopDone && presentationReady, paused);
        }

        // EPIC-008 menu actions.
        public bool TakeRest(RestCause by = RestCause.Child) => Round.TakeRest(by);

        // EPIC-012: a child's rest or a parent's pause ends in the same round: no new round, no new wares.
        public bool Resume(RestCause by)
        {
            if (!Round.Resume(by)) return false;
            Shop.ResumeAfterHold();
            return true;
        }

        public bool StartNextRound()
        {
            if (!Round.StartNextRound()) return false;
            Assortment.RequestRedraw(); // new fruit for the new round, as soon as no order is in progress (Shop)
            Shop.ResumeAfterHold();
            return true;
        }

        public void NewGame()
        {
            Wallet.Reset();
            Remote.Cancel("newgame"); // a phone order in progress ends unpaid; the phone may order again
            Round.Reset();            // EPIC-008: earnings, party, stars and rest start over
            Assortment.Redraw();      // EPIC-009: new fruit; the phone order is already cancelled
            Shop.Restart(); // also frees the play area
        }
    }
}
