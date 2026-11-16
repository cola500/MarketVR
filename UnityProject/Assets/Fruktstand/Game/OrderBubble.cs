// The customer's speech bubble, above the counter on the customer's side and facing the player.
// Ordering: one row per kind with a big number and one picture per unit; each picture sits on a disc that turns
// green with a tick when that unit is in the basket; the bottom row shows the order's sum (coin + number).
// AwaitingPayment: coin with the sum -> register picture. Thanking: "Tack!". Shown at the stall of the order.
// Pictures carry everything; the short Swedish words are extras.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class OrderBubble
    {
        public static readonly Vector3 Position = new(-0.17f, 0.36f, 0.26f); // counter-top space
        const float Icon = 0.085f;  // spacing between pictures
        const float Row = 0.1f;

        readonly Art art;
        readonly Transform root;
        Transform content;
        Renderer[] discs = new Renderer[0];
        GameObject[] ticks = new GameObject[0];
        Ware[] discKinds = new Ware[0];
        int[] discIndex = new int[0];
        string shownKey;

        public Transform Root => root;
        public int TicksOn { get; private set; }
        public int TickCount => ticks.Length;
        public int ShownSum { get; private set; }
        public string Words { get; private set; } = ""; // the words shown (Hej!, Tack!), empty otherwise

        public void AtStall(Transform stallTop)
        {
            if (root.parent == stallTop) return;
            root.SetParent(stallTop, false);
        }

        public OrderBubble(Art art, Transform top)
        {
            this.art = art;
            root = art.Group(top, "Order Bubble", Position);
            root.localRotation = Quaternion.Euler(-8, 0, 0); // tilted slightly up towards the player's eyes
        }

        // EPIC-015: customerNear: the customer walking in says hello only once close to the counter.
        public void Show(Shop shop, float time, bool customerNear = true)
        {
            var key = shop.State switch
            {
                SaleState.Ordering => "order " + shop.CustomerNumber,
                SaleState.AwaitingPayment => "pay " + shop.CustomerNumber,
                SaleState.Thanking => "thanks",
                SaleState.CustomerArriving when customerNear => "hello",
                _ => "none",
            };
            if (key != shownKey)
            {
                shownKey = key;
                Rebuild(shop, key);
            }
            if (shop.State == SaleState.Ordering) UpdateTicks(shop);
            // gentle float so it feels alive
            root.localPosition = Position + Vector3.up * (Mathf.Sin(time * 1.6f) * 0.004f);
        }

        void Rebuild(Shop shop, string key)
        {
            Art.Discard(content);
            content = art.Group(root, "Content");
            discs = new Renderer[0];
            ticks = new GameObject[0];
            TicksOn = 0;
            Words = "";
            switch (shop.State)
            {
                case SaleState.Ordering: BuildOrder(shop.Order); break;
                case SaleState.AwaitingPayment: BuildPay(shop.Order.Sum); break;
                case SaleState.Thanking: BuildWords("Tack!", true); break;
                case SaleState.CustomerArriving: if (key == "hello") BuildWords("Hej!", false); break; // EPIC-015: once close
            }
        }

        void Panel(float width, float height)
        {
            art.Part(content, "Panel", art.Quad, art.Unlit(Colors.Bubble), new Vector3(0, 0, 0.004f), new Vector3(width, height, 1), null, false);
            art.Part(content, "Edge", art.Quad, art.Unlit(Colors.BubbleEdge), new Vector3(0, 0, 0.006f), new Vector3(width + 0.012f, height + 0.012f, 1), null, false);
            // tail pointing to the customer (right and back)
            art.Part(content, "Tail", art.Quad, art.Unlit(Colors.Bubble), new Vector3(width / 2 - 0.02f, -height / 2 - 0.012f, 0.004f), new Vector3(0.035f, 0.035f, 1), Quaternion.Euler(0, 0, 45), false);
        }

        void BuildOrder(Order order)
        {
            var rows = order.Kinds;
            var maxUnits = 0;
            foreach (var p in WareInfo.All) maxUnits = Mathf.Max(maxUnits, order.Count(p));
            var width = 0.1f + maxUnits * Icon + 0.03f;
            var height = (rows + 1) * Row + 0.03f; // + the sum row
            Panel(width, height);
            var n = order.Total;
            discs = new Renderer[n];
            ticks = new GameObject[n];
            discKinds = new Ware[n];
            discIndex = new int[n];
            var row = 0;
            var k = 0;
            foreach (var p in WareInfo.All)
            {
                var count = order.Count(p);
                if (count == 0) continue;
                var y = height / 2 - 0.015f - Row * (row + 0.5f);
                var x0 = -width / 2 + 0.015f;
                art.Text(content, "Count", count.ToString(), new Vector3(x0 + 0.035f, y, 0), 0.006f, Colors.Ink);
                for (var i = 0; i < count; i++)
                {
                    var x = x0 + 0.1f + Icon * (i + 0.5f) - 0.01f;
                    var disc = art.Part(content, "Disc", art.Cylinder, art.Unlit(Colors.TickOff), new Vector3(x, y, 0), new Vector3(0.075f, 0.001f, 0.075f), Quaternion.Euler(90, 0, 0), false);
                    discs[k] = disc.GetComponent<Renderer>();
                    var icon = Models.Ware(art, content, p, "Icon");
                    icon.localPosition = new Vector3(x, y, -0.03f);
                    icon.localScale = Vector3.one * (StandView.Upright(p) ? 0.75f : 0.5f);
                    icon.localRotation = p is Ware.Carrot or Ware.Polkagris or Ware.Lollipop ? Quaternion.Euler(0, 0, 35) : Quaternion.identity;
                    var tick = art.Group(content, "Tick", new Vector3(x + 0.026f, y - 0.026f, -0.04f));
                    art.Part(tick, "Badge", art.Cylinder, art.Unlit(Colors.TickOn), Vector3.zero, new Vector3(0.03f, 0.001f, 0.03f), Quaternion.Euler(90, 0, 0), false);
                    art.Part(tick, "Short", art.Cube, art.Unlit(Color.white), new Vector3(-0.004f, -0.002f, -0.002f), new Vector3(0.004f, 0.01f, 0.001f), Quaternion.Euler(0, 0, 45), false);
                    art.Part(tick, "Long", art.Cube, art.Unlit(Color.white), new Vector3(0.004f, 0.002f, -0.002f), new Vector3(0.004f, 0.018f, 0.001f), Quaternion.Euler(0, 0, -35), false);
                    tick.gameObject.SetActive(false);
                    ticks[k] = tick.gameObject;
                    discKinds[k] = p;
                    discIndex[k] = i;
                    k++;
                }
                row++;
            }
            // Sum: "= (coin) N", what the payment token will be worth.
            var sy = height / 2 - 0.015f - Row * (row + 0.5f);
            var sx = -width / 2 + 0.015f;
            art.Text(content, "Equals", "=", new Vector3(sx + 0.035f, sy, 0), 0.005f, Colors.Ink);
            Models.CoinIcon(art, content, new Vector3(sx + 0.1f, sy, -0.004f), 0.06f);
            art.Text(content, "Sum", order.Sum.ToString(), new Vector3(sx + 0.17f, sy, -0.002f), 0.006f, Colors.Ink);
            ShownSum = order.Sum;
        }

        void UpdateTicks(Shop shop)
        {
            var on = 0;
            for (var i = 0; i < ticks.Length; i++)
            {
                var done = discIndex[i] < shop.Basket.Satisfied(shop.Order, discKinds[i]);
                if (ticks[i].activeSelf != done)
                {
                    ticks[i].SetActive(done);
                    discs[i].sharedMaterial = art.Unlit(done ? Colors.TickOn : Colors.TickOff);
                }
                if (done) on++;
            }
            TicksOn = on;
        }

        void BuildPay(int sum)
        {
            Panel(0.3f, 0.13f);
            Models.CoinIcon(art, content, new Vector3(-0.08f, 0, -0.004f), 0.075f);
            art.Text(content, "Sum", sum.ToString(), new Vector3(-0.08f, 0, -0.008f), 0.0045f, Colors.Ink);
            ShownSum = sum;
            // arrow
            art.Part(content, "Arrow", art.Cube, art.Unlit(Colors.Ink), new Vector3(0.0f, 0, -0.005f), new Vector3(0.05f, 0.01f, 0.002f), null, false);
            art.Part(content, "Head", art.Cube, art.Unlit(Colors.Ink), new Vector3(0.025f, 0.008f, -0.005f), new Vector3(0.025f, 0.008f, 0.002f), Quaternion.Euler(0, 0, -40), false);
            art.Part(content, "Head", art.Cube, art.Unlit(Colors.Ink), new Vector3(0.025f, -0.008f, -0.005f), new Vector3(0.025f, 0.008f, 0.002f), Quaternion.Euler(0, 0, 40), false);
            // register
            art.Part(content, "Register", art.Cube, art.Matte(Colors.Register), new Vector3(0.085f, -0.01f, -0.02f), new Vector3(0.07f, 0.05f, 0.03f));
            art.Part(content, "Slot", art.Cube, art.Unlit(Colors.Slot), new Vector3(0.085f, 0.016f, -0.036f), new Vector3(0.045f, 0.006f, 0.002f), null, false);
        }

        void BuildWords(string words, bool stars)
        {
            Words = words;
            Panel(0.2f, 0.1f);
            art.Text(content, "Words", words, new Vector3(0, 0, -0.002f), 0.005f, Colors.Ink);
            if (!stars) return;
            Models.Star(art, content, new Vector3(-0.075f, 0.03f, -0.003f), 0.03f);
            Models.Star(art, content, new Vector3(0.075f, 0.03f, -0.003f), 0.03f);
        }
    }
}
