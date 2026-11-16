// A market stall (EPIC-003): counter (height-adjustable, from Magic Tiles' Table), striped awning in the category's
// colours with a picture sign on top, three bins (one per ware of the category), basket (the handover surface),
// register, payment token spot, the shared till's badge and an Öppet/Stängt sign. All three stalls share the fruit
// stand's proven layout, which works because every category has exactly three wares. Everything the player touches is
// a child of `Top`, whose origin is the middle of the counter surface, so it all rises and sinks with the counter.
// +z points from the player to the customer.
//
// Compact market (EPIC-003, SPEC §14): shallow counters (0.30 m) facing a small shared work area, with the goods,
// basket, token and register on the inner half, at most 0.16 m from the inner edge (counter-local z = -0.15).
// Tune here and update the plan view in the spec.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class StandView
    {
        public const float MinHeight = 0.5f;
        public const float MaxHeight = 1.2f;
        public const float DefaultHeight = 0.72f;
        public static readonly Vector3 CounterSize = new(1.0f, 0.04f, 0.30f);
        public const float InnerEdge = -0.15f; // counter-local z of the edge facing the work area

        // Counter-top space positions.
        public static readonly Vector3 BasketCentre = new(0.06f, 0, -0.05f);
        public static readonly Vector3 RegisterCentre = new(0.41f, 0, -0.06f);
        public static readonly Vector3 CoinSpot = new(0.25f, 0.008f, -0.02f);
        // Öppet/Stängt hangs from a short rod sticking out sideways from the right front post (the board used to sit
        // on the post itself, which the headset test showed going through it). OpenSignPosition = the board's centre.
        public static readonly Vector3 OpenSignHook = new(0.56f, 0.37f, -0.12f);
        static readonly Vector3 OpenSignHang = new(0, -0.07f, 0); // board centre below the hook
        public static readonly Vector3 OpenSignPosition = OpenSignHook + OpenSignHang;
        static readonly Vector3 OpenSignBoardSize = new(0.11f, 0.06f, 0.012f);
        public static readonly Vector3 WalletPosition = new(0.41f, 0.2f, 0.06f);
        public const float CrateZ = -0.06f;
        public static readonly float[] CrateX = { -0.42f, -0.285f, -0.15f }; // bins 0..2
        static readonly Vector3 CrateSize = new(0.12f, 0.07f, 0.13f);
        static readonly Vector3 BasketSize = new(0.26f, 0.06f, 0.18f);
        static readonly Vector3 RegisterSize = new(0.15f, 0.09f, 0.13f);
        public const float PostHeight = 0.78f;
        const float PostX = 0.47f; // posts at x = ±PostX, z = ±0.12 (counter space)

        // Catch zones (generous, horizontal radius + height band above the counter).
        public static readonly DropZone BasketZone = new(BasketCentre, 0.17f, 0.05f, 0.35f);
        public static readonly DropZone RegisterZone = new(RegisterCentre + Vector3.up * RegisterSize.y, 0.14f, 0.12f, 0.3f);

        public static readonly Vector3[] Slots =
        {
            BasketCentre + new Vector3(-0.065f, 0, -0.042f), BasketCentre + new Vector3(0.065f, 0, -0.042f),
            BasketCentre + new Vector3(-0.065f, 0, 0.042f), BasketCentre + new Vector3(0.065f, 0, 0.042f),
        };

        // Wares that stand upright (round or boxy); the long ones lie down.
        public static bool Upright(Ware w) => w is not (Ware.Banana or Ware.Carrot or Ware.Lollipop or Ware.Polkagris);

        public static Vector3 SlotRest(int slot, Ware w) => Slots[slot] + Vector3.up * (0.012f + Models.Radius(w) * (Upright(w) ? 0.95f : 0.5f));
        // Where the next ware to take lies in bin 0..2 (EPIC-009: the bin comes from the Assortment).
        public static Vector3 CrateTop(int bin, Ware w) => new(CrateX[bin], CrateSize.y + Models.Radius(w) * (Upright(w) ? 0.9f : 0.45f), CrateZ);

        readonly Art art;
        readonly Transform root;
        readonly Transform top;
        readonly Transform[] legs = new Transform[4];
        readonly Transform skirt;
        readonly Renderer registerSlot;
        readonly Renderer basketRim;
        readonly Transform openSign;
        readonly Renderer openSignBoard;
        readonly TextMesh openSignText;
        readonly TextMesh walletText;
        readonly Transform walletRoot;
        Transform waresRoot; // crates + category sign: rebuilt when the assortment changes (EPIC-009)
        float signSwing;
        int shownBalance = -1;
        float walletPulse;

        public Category Category { get; }
        public Ware[] Wares { get; private set; } // what the bins hold now, left to right
        public Transform Root => root;
        public Transform Top => top;
        public Transform OpenSign => openSign;          // the hanging group (pivot at the hook on the rod)
        public Transform OpenSignBoard => openSignBoard.transform; // what the hand passes through
        public float Height { get; private set; }

        // EPIC-016: a small bell while this stall's customer waits to be served, so the pling is never the only sign of
        // where to go. It hangs on a string from the awning, right of the customer and above the wallet, where a seated
        // child looking at the counter sees it (under the awning's edge, or beside the post like the Öppet sign, it was
        // out of view: see EPIC-016). The string's top is the pivot of a gentle sway.
        public static readonly Vector3 WaitingBellCentre = new(0.28f, 0.42f, -0.02f); // counter space
        public static readonly Vector3 WaitingBellHook = new(WaitingBellCentre.x, PostHeight - 0.01f, WaitingBellCentre.z);
        Transform waitingBell;
        public bool WaitingShown => waitingBell.gameObject.activeSelf;
        public Vector3 WaitingBellPosition => waitingBell.TransformPoint(WaitingBellCentre - WaitingBellHook);

        public void ShowWaiting(bool on, float clock)
        {
            if (waitingBell.gameObject.activeSelf != on) waitingBell.gameObject.SetActive(on);
            if (on) waitingBell.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock * 2.4f) * 3); // a gentle sway
        }

        void BuildWaitingBell()
        {
            waitingBell = art.Group(top, "Waiting Bell", WaitingBellHook);
            var bell = WaitingBellCentre - WaitingBellHook; // below the hook
            var stringLength = -bell.y - 0.02f;
            art.Part(waitingBell, "String", art.Cube, art.Matte(Colors.WoodDark), new Vector3(0, -stringLength / 2, 0), new Vector3(0.003f, stringLength, 0.003f), null, false);
            art.Part(waitingBell, "Bell", art.Sphere, art.Matte(Colors.Gold), bell, new Vector3(0.04f, 0.045f, 0.04f), null, false);
            art.Part(waitingBell, "Rim", art.Cylinder, art.Matte(Colors.GoldLight), bell + new Vector3(0, -0.017f, 0), new Vector3(0.046f, 0.003f, 0.046f), null, false);
            art.Part(waitingBell, "Clapper", art.Sphere, art.Matte(Colors.WoodDark), bell + new Vector3(0, -0.024f, 0), Vector3.one * 0.013f, null, false);
            waitingBell.gameObject.SetActive(false);
        }
        public string WalletShown => walletText.text;
        public string OpenSignShown => openSignText.text;

        public StandView(Art art, Transform parent, Category category, Ware[] wares)
        {
            this.art = art;
            Category = category;
            root = art.Group(parent, "Stall " + WareInfo.Name(category));
            top = art.Group(root, "Counter Top");
            var (stripe, white) = AwningColours(category);

            // Counter: top slab, legs, a front skirt on the customer side (the player's side is open for the legs).
            art.Part(top, "Slab", art.Cube, art.Matte(Colors.Wood), new Vector3(0, -CounterSize.y / 2, 0), CounterSize);
            for (var i = 0; i < 4; i++) legs[i] = art.Part(root, "Leg", art.Cube, art.Matte(Colors.WoodDark), Vector3.zero, Vector3.one);
            skirt = art.Group(root, "Skirt");
            for (var i = 0; i < 6; i++)
            {
                var w = CounterSize.x / 6;
                art.Part(skirt, "Plank", art.Cube, art.Matte(i % 2 == 0 ? stripe : white),
                    new Vector3(-CounterSize.x / 2 + w * (i + 0.5f), 0.5f, 0), new Vector3(w, 1, 0.015f));
            }

            BuildAwning(stripe, white);
            BuildWaitingBell();
            SetWares(wares);
            basketRim = BuildBasket();
            registerSlot = BuildRegister();

            // Öppet/Stängt: a board hanging on two strings from a rod out of the right front post; it swings round the
            // hook when it flips. Moving a hand through the board flips it.
            var rodFrom = PostX + 0.012f;
            var rodTo = OpenSignHook.x + OpenSignBoardSize.x / 2 + 0.01f;
            art.Part(top, "Sign Rod", art.Cylinder, art.Matte(Colors.WoodDark), new Vector3((rodFrom + rodTo) / 2, OpenSignHook.y, OpenSignHook.z),
                new Vector3(0.012f, (rodTo - rodFrom) / 2, 0.012f), Quaternion.Euler(0, 0, 90), false);
            openSign = art.Group(top, "Open Sign", OpenSignHook);
            openSignBoard = art.Part(openSign, "Board", art.Cube, art.Unlit(Colors.OpenSign), OpenSignHang, OpenSignBoardSize, null, false).GetComponent<Renderer>();
            var stringLength = -OpenSignHang.y - OpenSignBoardSize.y / 2;
            foreach (var x in new[] { -0.04f, 0.04f })
                art.Part(openSign, "String", art.Cube, art.Matte(Colors.WoodDark), new Vector3(x, -stringLength / 2, 0), new Vector3(0.003f, stringLength, 0.003f), null, false);
            openSignText = art.Text(openSign, "Words", "Öppet", OpenSignHang + new Vector3(0, 0, -0.008f), 0.0032f, Color.white);

            // The shared till: coin + amount, the same at every stall.
            walletText = Models.WalletBadge(art, top, WalletPosition, 0.05f, out walletRoot);

            SetHeight(DefaultHeight);
        }

        static (Color stripe, Color white) AwningColours(Category c) => c switch
        {
            Category.FruitVeg => (Colors.AwningRed, Colors.AwningWhite),
            Category.Candy => (Colors.CandyPink, Colors.AwningWhite),
            _ => (new Color(0.3f, 0.55f, 0.95f), new Color(1f, 0.85f, 0.3f)),
        };

        static Color BinColour(Category c) => c switch
        {
            Category.FruitVeg => Colors.Crate,
            Category.Candy => Colors.CandyPinkLight,
            _ => new Color(0.75f, 0.85f, 1f),
        };

        void BuildAwning(Color stripe, Color white)
        {
            var posts = new[] { new Vector3(-PostX, 0, -0.12f), new Vector3(PostX, 0, -0.12f), new Vector3(-PostX, 0, 0.12f), new Vector3(PostX, 0, 0.12f) };
            foreach (var p in posts)
                art.Part(top, "Post", art.Cylinder, art.Matte(Colors.WoodDark), p + Vector3.up * PostHeight / 2, new Vector3(0.03f, PostHeight / 2, 0.03f));
            var roof = art.Group(top, "Awning", new Vector3(0, PostHeight, 0.0f));
            roof.localRotation = Quaternion.Euler(12, 0, 0); // slopes down towards the customer
            const int stripes = 9;
            for (var i = 0; i < stripes; i++)
            {
                var w = 1.04f / stripes;
                art.Part(roof, "Stripe", art.Cube, art.Matte(i % 2 == 0 ? stripe : white),
                    new Vector3(-0.52f + w * (i + 0.5f), 0, 0), new Vector3(w, 0.012f, 0.42f));
                art.Part(roof, "Scallop", art.Sphere, art.Matte(i % 2 == 0 ? stripe : white),
                    new Vector3(-0.52f + w * (i + 0.5f), -0.01f, 0.21f), new Vector3(w * 0.98f, 0.07f, 0.02f));
            }
        }

        // The bins and the sign show these wares (bin order). Rebuilt only when they differ.
        public void SetWares(Ware[] wares)
        {
            if (Wares != null && System.Linq.Enumerable.SequenceEqual(Wares, wares)) return;
            Wares = (Ware[])wares.Clone();
            Art.Discard(waresRoot);
            waresRoot = art.Group(top, "Wares");
            BuildCategorySign();
            for (var bin = 0; bin < Wares.Length; bin++) BuildCrate(bin, Wares[bin]);
        }

        // A picture sign on top of the awning: the category's wares, big, so the stalls are easy to find.
        void BuildCategorySign()
        {
            var sign = art.Group(waresRoot, "Category Sign", new Vector3(0, PostHeight + 0.2f, -0.05f));
            art.Part(sign, "Board", art.Quad, art.Unlit(Colors.Bubble), new Vector3(0, 0, 0.004f), new Vector3(0.5f, 0.2f, 1), null, false);
            art.Part(sign, "Edge", art.Quad, art.Unlit(Colors.BubbleEdge), new Vector3(0, 0, 0.006f), new Vector3(0.515f, 0.215f, 1), null, false);
            var wares = Wares;
            for (var i = 0; i < wares.Length; i++)
            {
                var pic = Models.Ware(art, sign, wares[i], "Picture");
                pic.localPosition = new Vector3(-0.15f + 0.15f * i, 0, -0.04f);
                pic.localScale = Vector3.one * 1.2f;
            }
        }

        void BuildCrate(int bin, Ware w)
        {
            var c = art.Group(waresRoot, "Bin " + WareInfo.Name(w), new Vector3(CrateX[bin], 0, CrateZ));
            var m = art.Matte(BinColour(Category));
            var s = CrateSize;
            art.Part(c, "Bottom", art.Cube, m, new Vector3(0, 0.005f, 0), new Vector3(s.x, 0.01f, s.z));
            art.Part(c, "Front", art.Cube, m, new Vector3(0, s.y / 2, -s.z / 2), new Vector3(s.x, s.y, 0.01f));
            art.Part(c, "Back", art.Cube, m, new Vector3(0, s.y / 2, s.z / 2), new Vector3(s.x, s.y, 0.01f));
            art.Part(c, "Left", art.Cube, m, new Vector3(-s.x / 2, s.y / 2, 0), new Vector3(0.01f, s.y, s.z));
            art.Part(c, "Right", art.Cube, m, new Vector3(s.x / 2, s.y / 2, 0), new Vector3(0.01f, s.y, s.z));
            // a pile inside, so the bin reads as "full of apples / lollipops / balls"
            var upright = Upright(w);
            var pile = upright
                ? new[] { new Vector3(-0.03f, 0.04f, -0.03f), new Vector3(0.03f, 0.04f, 0.03f), new Vector3(0.03f, 0.04f, -0.03f), new Vector3(-0.03f, 0.04f, 0.03f) }
                : new[] { new Vector3(0, 0.03f, -0.035f), new Vector3(0, 0.03f, 0.035f), new Vector3(0, 0.045f, 0) };
            if (WareInfo.IsToy(w)) pile = new[] { new Vector3(-0.025f, 0.035f, 0.025f), new Vector3(0.025f, 0.035f, -0.025f) };
            foreach (var at in pile)
            {
                var item = Models.Ware(art, c, w, "Pile");
                item.localPosition = at;
                item.localScale = Vector3.one * (upright ? 0.8f : 0.62f);
                item.localRotation = Quaternion.Euler(upright ? 0 : 90, at.z * 300, 0);
            }
            // a picture on the front, facing the player, as on a market sign
            var sign = Models.Ware(art, c, w, "Sign");
            sign.localPosition = new Vector3(0, s.y * 0.5f, -s.z / 2 - 0.012f);
            sign.localScale = Vector3.one * 0.45f;
        }

        Renderer BuildBasket()
        {
            var b = art.Group(top, "Basket", BasketCentre);
            var m = art.Matte(Colors.Wicker);
            var s = BasketSize;
            art.Part(b, "Bottom", art.Cube, m, new Vector3(0, 0.005f, 0), new Vector3(s.x, 0.01f, s.z));
            art.Part(b, "Front", art.Cube, m, new Vector3(0, s.y / 2, -s.z / 2), new Vector3(s.x, s.y, 0.012f));
            art.Part(b, "Back", art.Cube, m, new Vector3(0, s.y / 2, s.z / 2), new Vector3(s.x, s.y, 0.012f));
            art.Part(b, "Left", art.Cube, m, new Vector3(-s.x / 2, s.y / 2, 0), new Vector3(0.012f, s.y, s.z));
            art.Part(b, "Right", art.Cube, m, new Vector3(s.x / 2, s.y / 2, 0), new Vector3(0.012f, s.y, s.z));
            for (var i = 1; i < 3; i++)
                art.Part(b, "Weave", art.Cube, art.Matte(Colors.WoodDark), new Vector3(0, s.y * i / 3, -s.z / 2 - 0.007f), new Vector3(s.x, 0.005f, 0.003f), null, false);
            for (var i = 0; i <= 6; i++)
            {
                var a = Mathf.PI * i / 6;
                art.Part(b, "Handle", art.Sphere, m, new Vector3(Mathf.Cos(a) * s.x * 0.45f, s.y + Mathf.Sin(a) * 0.09f, 0), Vector3.one * 0.016f);
            }
            for (var i = 0; i < Slots.Length; i++)
                art.Part(b, "Slot", art.Cylinder, art.Matte(new Color(0.9f, 0.78f, 0.55f)), Slots[i] - BasketCentre + Vector3.up * 0.011f, new Vector3(0.07f, 0.001f, 0.07f), null, false);
            return art.Part(b, "Rim", art.Cube, art.Unlit(Colors.TickOn), new Vector3(0, s.y + 0.002f, -s.z / 2), new Vector3(s.x, 0.006f, 0.014f), null, false).GetComponent<Renderer>();
        }

        Renderer BuildRegister()
        {
            var r = art.Group(top, "Register", RegisterCentre);
            var s = RegisterSize;
            art.Part(r, "Body", art.Cube, art.Matte(Colors.Register), new Vector3(0, s.y / 2, 0), s);
            art.Part(r, "Drawer", art.Cube, art.Matte(new Color(0.15f, 0.45f, 0.47f)), new Vector3(0, s.y * 0.3f, -s.z / 2 - 0.004f), new Vector3(s.x * 0.8f, s.y * 0.3f, 0.008f));
            art.Part(r, "Knob", art.Sphere, art.Matte(Colors.Gold), new Vector3(0, s.y * 0.3f, -s.z / 2 - 0.012f), Vector3.one * 0.016f);
            var slot = art.Part(r, "Coin Slot", art.Cube, art.Unlit(Colors.Slot), new Vector3(0, s.y + 0.001f, 0), new Vector3(0.095f, 0.003f, 0.025f), null, false);
            art.Part(r, "Slot Frame", art.Cube, art.Matte(Colors.Gold), new Vector3(0, s.y + 0.0005f, 0), new Vector3(0.115f, 0.003f, 0.045f), null, false);
            return slot.GetComponent<Renderer>();
        }

        public static float Clamp(float height) => Mathf.Clamp(height, MinHeight, MaxHeight);

        // Stall centred at `point` (x, z, in the parent's space) facing `yaw` (local, relative to the parent).
        public void PlaceLocal(Vector3 point, float yaw)
        {
            root.localPosition = new Vector3(point.x, 0, point.z);
            root.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        public void SetHeight(float height)
        {
            Height = Clamp(height);
            top.localPosition = new Vector3(0, Height, 0);
            var legHeight = Height - CounterSize.y;
            var x = CounterSize.x / 2 - 0.05f;
            var z = CounterSize.z / 2 - 0.05f;
            var corners = new[] { new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z) };
            for (var i = 0; i < 4; i++)
            {
                legs[i].localPosition = corners[i] + Vector3.up * (legHeight / 2);
                legs[i].localScale = new Vector3(0.05f, legHeight, 0.05f);
            }
            skirt.localPosition = new Vector3(0, 0.05f, CounterSize.z / 2 - 0.01f);
            skirt.localScale = new Vector3(1, Mathf.Max(0.01f, legHeight - 0.05f), 1);
        }

        public Vector3 ToWorld(Vector3 counterPoint) => top.TransformPoint(counterPoint);
        public Vector3 ToCounter(Vector3 worldPoint) => top.InverseTransformPoint(worldPoint);

        public void SetRegisterGlow(bool on) => registerSlot.sharedMaterial = art.Unlit(on ? Colors.RegisterGlow : Colors.Slot);
        public void SetBasketGlow(bool on) => basketRim.enabled = on;

        public void ShowOpen(bool open, float dt)
        {
            openSignBoard.sharedMaterial = art.Unlit(open ? Colors.OpenSign : Colors.ClosedSign);
            openSignText.text = open ? "Öppet" : "Stängt";
            signSwing = Mathf.Max(0, signSwing - dt);
            openSign.localRotation = Quaternion.Euler(Mathf.Sin(signSwing * 25) * 35 * signSwing, 0, 0);
        }

        public void SwingSign() => signSwing = 0.6f;

        public void ShowTill(int balance, float dt)
        {
            if (balance != shownBalance)
            {
                if (shownBalance >= 0) walletPulse = 0.4f;
                shownBalance = balance;
                walletText.text = balance.ToString();
            }
            walletPulse = Mathf.Max(0, walletPulse - dt);
            walletRoot.localScale = Vector3.one * (1 + Mathf.Sin(walletPulse / 0.4f * Mathf.PI) * 0.25f);
        }
    }
}
