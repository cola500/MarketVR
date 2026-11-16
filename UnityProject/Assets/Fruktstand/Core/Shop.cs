// One sale after another at the market (EPIC-003: one active order at a time, from one category, at the stall of
// that category). Pure state machine (no XR, physics or rendering), driven by explicit calls and Tick(dt).
//
//   CustomerArriving --(ArriveSeconds, and with WaitsForArrival the customer has walked in: EPIC-015)--> Ordering --(basket matches exactly)--> AwaitingPayment
//   AwaitingPayment --(TryPay: coin in register, once)--> Thanking --(ThankSeconds)--> CustomerLeaving
//   CustomerLeaving --(LeaveSeconds, basket cleared)--> CustomerArriving (next customer, next order), or Closed
//   Closed --(SetOpen(true))--> CustomerArriving
//
// The registered sale credits the shared till (Wallet) with the order's Sum in that same transition: the only
// source of income. A toy order reserves its play spot when it is made, and the paid toy goes to the play area.
//
// The sale is registered by the single transition AwaitingPayment -> Thanking, so repeated trigger, release or
// collision events can never count it twice. Nothing ever fails: no timer, no penalty, wrong items just don't match.
//
// EPIC-004: a human customer ordering from a phone (RemoteCustomer) takes the next free customer turn. The NPC at the
// counter is never interrupted; while the human order waits or is served no new NPC comes. Its sale is the same
// TryPay as any other, which also marks the phone order Done.
namespace Fruktstand.Core
{
    public enum SaleState { CustomerArriving, Ordering, AwaitingPayment, Thanking, CustomerLeaving, Closed }

    public sealed class Shop
    {
        public const float ArriveSeconds = 1.5f;
        public const float ThankSeconds = 2.5f;
        public const float LeaveSeconds = 1.5f;
        readonly OrderGenerator orders;

        public Shop(int seed = 1, Wallet wallet = null, PlayArea playArea = null, RemoteCustomer remote = null, MarketRound round = null, Assortment assortment = null)
        {
            Assortment = assortment ?? new Assortment(seed, Assortment.Classic);
            orders = new OrderGenerator(seed, Assortment);
            Wallet = wallet ?? new Wallet();
            PlayArea = playArea ?? new PlayArea();
            Remote = remote ?? new RemoteCustomer();
            Round = round ?? new MarketRound();
            Restart();
        }

        public Assortment Assortment { get; } // EPIC-009: the wares on sale; a requested redraw is applied in NextCustomer

        public MarketRound Round { get; } // EPIC-008: counts registered sales; holds new customers for the party/rest

        // A new customer may come: open, and no party or rest (a phone order accepted before the party still may).
        bool MayServeNext => IsOpen && (!Round.HoldsCustomers || (Round.State == RoundState.PartyPending && Remote.HasWaiting));

        // EPIC-008: after the rest, the market carries on where it stopped.
        public void ResumeAfterHold()
        {
            if (State == SaleState.Closed && MayServeNext) NextCustomer();
        }

        public Wallet Wallet { get; }
        public PlayArea PlayArea { get; }
        public RemoteCustomer Remote { get; }

        // The customer at the counter is the parent ordering from the phone (EPIC-004).
        // Kept for the whole turn (also while thanking and leaving, after the phone order is Done).
        public bool CustomerIsHuman => humanCustomer == CustomerNumber && CustomerNumber > 0;
        public string HumanPhotoId { get; private set; } // the phone's photo for this customer, or null
        int humanCustomer = -1;
        public Category Category => Order.Category;
        public int LastSaleSum { get; private set; }

        // Toy customers are children; now and then a child buys fruit or candy too [A].
        public bool CustomerIsChild => !CustomerIsHuman && (Order.Category == Category.Toys || CustomerNumber % 5 == 4);
        public bool IsOpen { get; private set; } = true;

        public SaleState State { get; private set; }
        public float StateTime { get; private set; } // seconds since entering State
        public Order Order { get; private set; }
        public Basket Basket { get; } = new();
        public int CustomerNumber { get; private set; } // 1-based, increases with every new customer
        public int CompletedSales { get; private set; }
        // EPIC-015: in the game the customer walks in along the park's paths; the order starts only once they are at the
        // counter (CustomerArrived, from the view). Off by default: the rules and their tests keep the plain timer.
        public bool WaitsForArrival { get; set; }
        bool arrived;

        public void CustomerArrived()
        {
            if (State == SaleState.CustomerArriving) arrived = true; // never carried over to the next customer
        }
        public int Revision { get; private set; } // changes whenever anything a view shows changes

        public bool AcceptsItems => State == SaleState.Ordering && !Basket.IsFull;
        public bool BasketUnlocked => State == SaleState.Ordering;
        public bool CoinOnCounter => State == SaleState.AwaitingPayment;
        public bool BasketMatches => Basket.Matches(Order);

        // Pausing only stops *new* customers: one who is already here stays and can be served. No timer.
        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            Revision++;
            if (open && State == SaleState.Closed && MayServeNext) NextCustomer();
        }

        // "Töm korgen": the current order starts over with an empty basket; same customer, same order, no money moved.
        // A coin already offered is taken back (AwaitingPayment -> Ordering). Nothing to do in other states.
        public bool ClearBasket()
        {
            if (State != SaleState.Ordering && State != SaleState.AwaitingPayment) return false;
            Basket.Clear();
            if (State == SaleState.AwaitingPayment) Enter(SaleState.Ordering);
            Revision++;
            return true;
        }

        public void Restart()
        {
            IsOpen = true;
            orders.Reset();
            PlayArea.Reset();
            LastSaleSum = 0;
            CompletedSales = 0;
            CustomerNumber = 0;
            humanCustomer = -1;
            HumanPhotoId = null;
            Basket.Clear();
            NextCustomer();
        }

        // Returns the slot the item landed in, or -1 if the basket doesn't take it now (then it goes back to its crate).
        // Wares from another category are never taken (they belong to another stall).
        public int TryAddToBasket(Ware p, int preferredSlot = -1)
        {
            if (!AcceptsItems || WareInfo.CategoryOf(p) != Order.Category) return -1;
            var slot = Basket.Add(p, preferredSlot);
            if (slot >= 0)
            {
                Revision++;
                CheckMatch();
            }
            return slot;
        }

        // Taking an item out is only allowed while ordering; once it matches the customer has "taken" the basket.
        public Ware? TakeFromBasket(int slot)
        {
            if (!BasketUnlocked) return null;
            var p = Basket.Remove(slot);
            if (p.HasValue)
            {
                Revision++;
                CheckMatch(); // taking out a wrong item can complete the order
            }
            return p;
        }

        // The coin tray was put in the register. True exactly once per sale; false before the basket matches
        // (paying too early) and for any repeat event after the sale is registered.
        public bool TryPay()
        {
            if (State != SaleState.AwaitingPayment) return false;
            CompletedSales++;
            LastSaleSum = Order.Sum;
            Wallet.Earn(Order.Sum);
            if (Order.Toy is Ware toy) PlayArea.Sold(toy); // the child takes the toy to the play area
            Remote.Paid(CustomerNumber); // a phone order is Done in this same transition (no-op for NPCs)
            Round.SaleRegistered(Order.Sum); // EPIC-008: the round's earnings, from this one registering transition
            Enter(SaleState.Thanking);
            return true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0) return;
            StateTime += dt;
            switch (State)
            {
                case SaleState.CustomerArriving when StateTime >= ArriveSeconds && (!WaitsForArrival || arrived):
                    Enter(SaleState.Ordering);
                    CheckMatch(); // (basket is empty here, orders are never empty, so this stays Ordering)
                    break;
                case SaleState.Thanking when StateTime >= ThankSeconds:
                    Basket.Clear();
                    Enter(SaleState.CustomerLeaving);
                    break;
                case SaleState.CustomerLeaving when StateTime >= LeaveSeconds:
                    if (MayServeNext) NextCustomer();
                    else Enter(SaleState.Closed);
                    break;
            }
        }

        void CheckMatch()
        {
            if (State == SaleState.Ordering && Basket.Matches(Order)) Enter(SaleState.AwaitingPayment);
        }

        void NextCustomer()
        {
            Basket.Clear();
            // EPIC-009: the next round's new fruit arrive between customers, never with a phone order still open.
            if (Assortment.RedrawPending && Remote.ActiveId == null) Assortment.Redraw();
            CustomerNumber++;
            // A waiting phone order takes this turn (EPIC-004); otherwise the usual NPC order.
            var photo = Remote.ActivePhotoId;
            if (Remote.TryStartTurn(CustomerNumber, out var human))
            {
                humanCustomer = CustomerNumber;
                HumanPhotoId = photo;
                Order = human;
                Enter(SaleState.CustomerArriving);
                return;
            }
            // A toy order is only made if its play spot can be reserved first.
            Order = orders.Next(CustomerNumber, PlayArea.IsFree);
            if (Order.Toy is Ware toy && !PlayArea.TryReserve(toy, CustomerNumber)) Order = Order.Of(Assortment.FirstFruit);
            Enter(SaleState.CustomerArriving);
        }

        void Enter(SaleState s)
        {
            State = s;
            StateTime = 0;
            arrived = false;
            Revision++;
        }
    }
}
