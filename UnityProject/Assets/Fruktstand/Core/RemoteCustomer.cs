// A human customer who orders from a phone (EPIC-004). Pure rules, no networking: the Quest is authoritative and
// this class decides what a phone *request* turns into. The phone never changes the till, an order's state or a
// payment; it can only ask for an order and ask for its status.
//
//   Request(id, items) --valid, nobody else active--> Waiting --(Shop: next free customer turn)--> Serving
//   Serving --(Shop.TryPay, exactly once)--> Done          any active order --(Cancel: Nytt spel)--> Cancelled
//
// - One human customer and one active order at a time. A different id while one is active is Rejected ("busy").
// - The same id again (a resent or duplicated message, a reconnect) never makes a second order: it returns the
//   current receipt, flagged Duplicate. Ids of finished orders are remembered for the session.
// - The order waits for the *next* free customer turn: an NPC already at the counter is never interrupted, and no
//   new NPC comes while the human customer waits or is served (Shop asks here first).
// - Only fruit/veg and candy, 1..3 items, one category (no toys: they need a play spot and a child), and only wares on
//   sale now (EPIC-009: otherwise Rejected "inactive", not remembered).
// - The sum is always computed here from the Quest's own prices; nothing from the phone is trusted for it.
using System.Collections.Generic;

namespace Fruktstand.Core
{
    public enum RemoteState { Unknown, Waiting, Serving, Done, Cancelled, Rejected }

    public readonly struct RemoteReceipt
    {
        public RemoteReceipt(string orderId, RemoteState state, string reason = null, bool duplicate = false, int sum = 0)
        {
            OrderId = orderId;
            State = state;
            Reason = reason ?? "";
            Duplicate = duplicate;
            Sum = sum;
        }

        public string OrderId { get; }
        public RemoteState State { get; }
        public string Reason { get; } // for Rejected / Cancelled: a short machine word (busy, items, id, newgame)
        public bool Duplicate { get; }
        public int Sum { get; }
        public bool Accepted => State is RemoteState.Waiting or RemoteState.Serving or RemoteState.Done;
    }

    public sealed class RemoteCustomer
    {
        public const int MinIdLength = 8;
        public const int MaxIdLength = 40;
        public const int MaxItems = Order.MaxTotal;
        const int HistoryLimit = 64;

        // The words the phone page uses for wares. Only fruit/veg and candy can be ordered from the phone.
        static readonly Dictionary<string, Ware> Keys = new()
        {
            ["apple"] = Ware.Apple, ["banana"] = Ware.Banana, ["carrot"] = Ware.Carrot,
            ["pear"] = Ware.Pear, ["orange"] = Ware.Orange, ["strawberry"] = Ware.Strawberry, ["grapes"] = Ware.Grapes, ["watermelon"] = Ware.Watermelon,
            ["lollipop"] = Ware.Lollipop, ["polkagris"] = Ware.Polkagris, ["candybag"] = Ware.CandyBag,
            ["gummybear"] = Ware.GummyBear, ["chocolate"] = Ware.Chocolate, ["wrappedcandy"] = Ware.WrappedCandy,
            ["rainbowstrip"] = Ware.RainbowStrip, ["candyring"] = Ware.CandyRing,
        };

        readonly Dictionary<string, RemoteReceipt> history = new();
        readonly Queue<string> historyOrder = new();

        public string ActiveId { get; private set; }
        public Order ActiveOrder { get; private set; }
        public RemoteState ActiveState { get; private set; } = RemoteState.Unknown;
        public string ActivePhotoId { get; private set; }
        public int ServingCustomerNumber { get; private set; } = -1;
        public int Revision { get; private set; } // changes whenever any receipt changes (the link sends status then)
        public int CompletedOrders { get; private set; }

        public bool HasWaiting => ActiveId != null && ActiveState == RemoteState.Waiting;

        // EPIC-008: the market has its party or rests: new orders are refused ("paused"); accepted ones are kept.
        // Asked at the moment of each request (Market points it at MarketRound.HoldsCustomers).
        public System.Func<bool> PausedWhen { get; set; }
        public bool Paused => PausedWhen?.Invoke() ?? false;

        // EPIC-009: whether a ware is on sale now (Market points it at Assortment.IsActive); null = everything.
        public System.Func<Ware, bool> Offers { get; set; }
        public bool IsServing(int customerNumber) => ActiveState == RemoteState.Serving && ServingCustomerNumber == customerNumber;

        public static bool TryParseWare(string key, out Ware ware)
        {
            ware = default;
            return key != null && Keys.TryGetValue(key, out ware);
        }

        public static string KeyOf(Ware w)
        {
            foreach (var kv in Keys) if (kv.Value == w) return kv.Key;
            return null;
        }

        public static bool ValidId(string id)
        {
            if (id == null || id.Length < MinIdLength || id.Length > MaxIdLength) return false;
            foreach (var c in id)
                if (!(c is >= 'a' and <= 'z' || c is >= 'A' and <= 'Z' || c is >= '0' and <= '9' || c == '-' || c == '_')) return false;
            return true;
        }

        // A phone asks for an order. itemKeys: one entry per item ("apple", "apple" = two apples).
        public RemoteReceipt Request(string orderId, IList<string> itemKeys, string photoId = null)
        {
            if (!ValidId(orderId)) return new RemoteReceipt(orderId, RemoteState.Rejected, "id");
            if (history.TryGetValue(orderId, out var known)) return Duplicate(known);
            if (orderId == ActiveId) return Duplicate(Current());
            if (Paused) return new RemoteReceipt(orderId, RemoteState.Rejected, "paused"); // party/rest; not remembered: retry later
            if (ActiveId != null) return new RemoteReceipt(orderId, RemoteState.Rejected, "busy"); // not remembered: may be retried later

            var order = Parse(itemKeys);
            if (order == null) return Remember(new RemoteReceipt(orderId, RemoteState.Rejected, "items"));
            foreach (var w in WareInfo.All) // not remembered: the phone gets the current fruit and may order again
                if (order.Count(w) > 0 && Offers != null && !Offers(w)) return new RemoteReceipt(orderId, RemoteState.Rejected, "inactive");

            ActiveId = orderId;
            ActiveOrder = order;
            ActivePhotoId = photoId is { Length: > 0 and <= MaxIdLength } && ValidId(photoId) ? photoId : null;
            ActiveState = RemoteState.Waiting;
            ServingCustomerNumber = -1;
            Revision++;
            return Current();
        }

        // Current status of an order id (a phone that reconnects asks for this).
        public RemoteReceipt Status(string orderId)
        {
            if (orderId != null && orderId == ActiveId) return Current();
            if (orderId != null && history.TryGetValue(orderId, out var r)) return r;
            return new RemoteReceipt(orderId, RemoteState.Unknown);
        }

        // Called by Shop when a new customer would come: the waiting human customer takes this turn.
        internal bool TryStartTurn(int customerNumber, out Order order)
        {
            order = null;
            if (!HasWaiting) return false;
            ActiveState = RemoteState.Serving;
            ServingCustomerNumber = customerNumber;
            order = ActiveOrder;
            Revision++;
            return true;
        }

        // Called by Shop.TryPay in its single AwaitingPayment -> Thanking transition.
        internal void Paid(int customerNumber)
        {
            if (!IsServing(customerNumber)) return;
            CompletedOrders++;
            Finish(new RemoteReceipt(ActiveId, RemoteState.Done, sum: ActiveOrder.Sum));
        }

        // Nytt spel: an active human order ends without payment; the phone may order again.
        public void Cancel(string reason)
        {
            if (ActiveId == null) return;
            Finish(new RemoteReceipt(ActiveId, RemoteState.Cancelled, reason));
        }

        RemoteReceipt Current() => new(ActiveId, ActiveState, sum: ActiveOrder?.Sum ?? 0);

        static RemoteReceipt Duplicate(RemoteReceipt r) => new(r.OrderId, r.State, r.Reason, true, r.Sum);

        void Finish(RemoteReceipt final)
        {
            ActiveId = null;
            ActiveOrder = null;
            ActivePhotoId = null;
            ActiveState = RemoteState.Unknown;
            ServingCustomerNumber = -1;
            Remember(final);
        }

        RemoteReceipt Remember(RemoteReceipt r)
        {
            if (!history.ContainsKey(r.OrderId)) historyOrder.Enqueue(r.OrderId);
            history[r.OrderId] = r;
            while (historyOrder.Count > HistoryLimit) history.Remove(historyOrder.Dequeue());
            Revision++;
            return r;
        }

        static Order Parse(IList<string> keys)
        {
            if (keys == null || keys.Count < Order.MinTotal || keys.Count > MaxItems) return null;
            var counts = new int[WareInfo.Count];
            Category? category = null;
            foreach (var k in keys)
            {
                if (!TryParseWare(k, out var w)) return null;
                var c = WareInfo.CategoryOf(w);
                if (category.HasValue && category != c) return null;
                category = c;
                counts[(int)w]++;
            }
            return new Order(counts);
        }
    }
}
