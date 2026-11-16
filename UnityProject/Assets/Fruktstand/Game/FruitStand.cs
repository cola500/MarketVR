// The little market (EPIC-003, compact): wires the rules (Core.Market: one Shop with one active order at a time
// across the stalls, the shared till, the PlayArea), grabbing, items, the three stalls and the play table together.
// Everything stands round a small work area; the player turns and steps, nothing teleports (SPEC §14, §18).
// Desktop (keys) by default; switches to VR (XrRig) if an XR device is active at startup. Both modes, and the tests,
// drive the same Step(ShopFrame).
//
// Rules live in Core; this class only translates hands and drops into rule calls and shows the result:
// - a ware released inside the active stall's basket -> Shop.TryAddToBasket (its category only), otherwise it goes home;
// - the payment token released in the active stall's register -> RegisterCoin -> Shop.TryPay (true once per order;
//   credits the order's sum to the shared till). A toy sale hands the very toy from the basket to the child;
// - toys at the play area: grab -> PlayArea.PlayerTook (the child waits), release -> free physics -> child again;
// - the tidy button and the Öppet/Stängt signs act once per fresh press / pass;
// - ReleaseAll (focus lost, paused, headset off, menu open) and lost tracking send held things home and never buy.
using System;
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Fruktstand.Game
{
    public sealed class FruitStand : MonoBehaviour
    {
        const float XrDetectSeconds = 3f; // no XR device by then = desktop for the rest of the session
        public const float EyeToCounter = 0.32f; // the EPIC-001 fruit stand: counter centre in front of a seated player's eyes (old tests, renders)
        public const float CounterBelowEyes = 0.40f;
        const float ReturnSeconds = 0.35f;
        const float SnapSeconds = 0.12f;

        [SerializeField] Camera viewCamera;
        [SerializeField] XrRig xrRig;
        [SerializeField] DesktopHud hud;
        [SerializeField] MeshLibrary meshes = new();
        [SerializeField] Material matte;
        [SerializeField] Material unlit;
        [SerializeField] Material photo; // Unlit/Texture, referenced so the shader ships in the build (EPIC-004)
        [SerializeField] Font font;
        [Tooltip("Off = drive the market through Step() only (tests, other input sources).")]
        [SerializeField] bool readDesktopInput = true;
        [SerializeField] int seed = 1;

        Art art;
        Market market;
        Transform marketRoot, park;
        Ambience ambience;    // EPIC-014: when the birds sing, and how loud
        NatureSounds nature;
        ParkVisits visits;    // EPIC-015
        ParkVisitors visitors;
        (SaleState state, int number, Category category, bool child) lastCustomer; // to hand a leaving customer to the park
        readonly StandView[] stalls = new StandView[3];
        readonly TouchSwitch[] openSwitches = { new(), new(), new() };
        PlayTableView play;
        CrocodileView crocodile;
        PartyView party;
        RoundState lastRound = RoundState.Playing;
        CustomerView customer;
        OrderBubble bubble;
        PhotoStore photos;
        RemoteSession remoteSession;
        RelayLink relay;
        Transform marking, preview;
        Transform available;
        float previewFor = -1;
        bool previewFits;
        Sounds sounds;
        TextMesh statusText;
        TextMesh tokenText;
        readonly Grabber[] grabbers = { new(), new() };
        readonly Item[] lastHeld = new Item[2];
        readonly Transform[] handVisuals = new Transform[2];
        readonly Vector3[] handVelocity = new Vector3[2];
        readonly Vector3?[] lastHand = new Vector3?[2];
        // Recent hand speeds: a release uses the fastest of the last 0.1 s, so a flick still counts when the hand
        // has already slowed down in the frame the button is let go (usual VR throwing practice).
        readonly List<(Vector3 v, float t)>[] recentVelocity = { new(), new() };
        public const float ReleaseWindow = 0.1f;
        readonly List<Item> items = new();
        readonly Item[] crateTops = new Item[WareInfo.Count]; // null for wares not on sale
        int shownAssortment = -1;

        // EPIC-009/010, tests only: the wares the scene starts with (three per category given, bin order; categories not
        // given are classic); null = every category drawn from the clock.
        public static Ware[] StartWares;
        readonly List<(Transform t, Vector3 v, float life)> sparks = new();
        Item coin;
        readonly DesktopInput desktop = new();
        float clock;
        bool modeDecided;

        // hand index (0 = left, 1 = right), amplitude 0..1, seconds
        public event Action<int, float, float> Haptic;

        public Market Market => market;
        public Art Art => art; // EPIC-009 tests: build a ware's model on its own
        public Shop Shop => market.Shop;
        public Wallet Wallet => market.Wallet;
        public PlayArea PlayArea => market.PlayArea;
        public StandView Stand => stalls[0]; // the fruit & veg stall (EPIC-001 tests)
        public StandView Stall(Category c) => stalls[(int)c];
        public StandView ActiveStall => stalls[(int)Shop.Category];
        public PlayTableView Play => play;
        public CrocodileView Crocodile => crocodile; // EPIC-006: always at the play table
        public PartyView Party => party;             // EPIC-008: the market party and the rest
        public OrderBubble Bubble => bubble;
        public CustomerView Customer => customer;
        public RemoteSession RemoteSession => remoteSession; // phone customer protocol (EPIC-004), no networking
        public PhotoStore Photos => photos;
        public RelayLink Relay => relay;
        public Transform Marking => marking;  // EPIC-007: the market's floor marking (shown only while calibrating)
        public Transform Preview => preview;  // the calibration proposal, a separate root; null until first shown
        public Color PreviewColor { get; private set; } // null unless started (StartRelayLink)
        public Sounds Sounds => sounds;
        public Camera ViewCamera => viewCamera;
        public Transform MarketRoot => marketRoot;
        public NatureSound NatureSound => ambience.Level;      // EPIC-014: the parent's setting (normal, low, off)
        public int BirdsSung => nature.BirdsSung;
        public static System.Func<int, int> StrollRandom; // EPIC-015, tests only
        public ParkVisitors Visitors => visitors; // EPIC-015: figures strolling in the park after the counter or the play table
        public ParkVisits Visits => visits;
        public Transform Park => park;        // EPIC-013: the park round the market (a child of the market root)
        public bool XrMode { get; private set; }
        public bool ModeDecided => modeDecided;
        public IReadOnlyList<Item> Items => items;
        public Item Coin => coin;
        public string TokenShown => tokenText.text;
        public Item CrateTop(Ware w) => crateTops[(int)w];
        public Item Held(int hand) => grabbers[hand].Held as Item;
        public Item Hover(int hand) => grabbers[hand].Hover as Item;
        public int SalesRegistered { get; private set; } // RegisterCoin successes, must always equal Shop.CompletedSales
        public DesktopView View { get; private set; } = DesktopView.Overview; // desktop camera only
        public float Clock => clock;
        public string WalletShown => stalls[0].WalletShown;
        public string OpenSignShown => stalls[0].OpenSignShown;
        public Vector3 OpenSignWorld => stalls[0].OpenSignBoard.position;

        public bool ReadDesktopInput
        {
            get => readDesktopInput;
            set => readDesktopInput = value;
        }

        public Item BasketItem(int slot)
        {
            foreach (var i in items) if (i.Place == ItemPlace.Basket && i.Slot == slot) return i;
            return null;
        }

        public Vector3 ToMarket(Vector3 world) => marketRoot.InverseTransformPoint(world);
        public Vector3 ToWorld(Vector3 market) => marketRoot.TransformPoint(market);

        // EPIC-005: where a stick walk from -> to may end (world space), so the seated player stops at the counters
        // and the play table wherever the market has been placed. Physical movement is never constrained.
        public Vector3 ConstrainWalk(Vector3 fromWorld, Vector3 toWorld) =>
            ToWorld(MarketCollision.Constrain(ToMarket(fromWorld), ToMarket(toWorld)));

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            Debug.Log($"[Fruktstand] build {BuildInfo.Id}{(BuildInfo.Dirty ? " (lokala ändringar)" : "")} · {Application.version}");
            Physics.simulationMode = SimulationMode.Script; // stepped from Step(): deterministic, same in VR and tests
            art = new Art(meshes, matte, unlit, font);
            MarketLayout.SetStallDistance(MarketLayout.DefaultStallDistance); // every session starts compact (EPIC-007)
            marketRoot = art.Group(transform, "Market");
            park = ParkView.Build(art, marketRoot, meshes); // EPIC-013: on the ground, follows placement, not the height
            nature = new NatureSounds(park, ParkLayout.BirdSpots);
            ambience = new Ambience(System.Environment.TickCount, nature.Spots, nature.Clips) { Level = LoadNatureSound() };
            var strollRandom = new System.Random();
            var stroll = StrollRandom ?? (n => strollRandom.Next(n));
            visits = new ParkVisits(stroll);
            visitors = new ParkVisitors(art, park, visits, stroll);
            // EPIC-009/010: the first wares of the session; tests fix them (StartWares), the app draws from the clock.
            var assortment = StartWares != null ? new Assortment(seed, StartWares) : new Assortment(System.Environment.TickCount);
            market = new Market(seed, PartyThreshold(), assortment);
            market.Shop.WaitsForArrival = true; // EPIC-015: customers walk in along the park's paths first
            LogAssortment();
            foreach (var c in WareInfo.Categories)
            {
                var stall = new StandView(art, marketRoot, c, market.Assortment.Active(c));
                var (p, yaw) = MarketLayout.Stall(c);
                stall.PlaceLocal(p, yaw);
                stalls[(int)c] = stall;
            }
            sounds = new Sounds(marketRoot);
            play = new PlayTableView(art, marketRoot, items, sounds, () => TidyPlayArea());
            // EPIC-015: a child who has finished playing strolls on in the park from where it is
            play.ChildWentHome += (variant, world) =>
            {
                var at = ToMarket(world);
                at.y = 0;
                visitors.Add(variant, true, plan => ParkRoutes.FromPlay(at, plan), 0);
            };
            crocodile = new CrocodileView(art, play.Top, items, new System.Random().Next);
            var (tp, tyaw) = MarketLayout.PlayTable;
            play.PlaceLocal(tp, tyaw);
            marking = BuildMarking();
            customer = new CustomerView(art, stalls[0].Root);
            bubble = new OrderBubble(art, stalls[0].Top);
            party = new PartyView(art, stalls[0].Top, marketRoot, transform);
            photos = new PhotoStore();
            remoteSession = new RemoteSession(market.Shop, photos)
            {
                // EPIC-008: the parent's pause / next round from the phone: the same rules as the menu. The rest holds new
                // orders at once; anything still in a hand goes home in the next step (RoundChanged).
                OnParentCommand = cmd => cmd switch
                {
                    "rest" => market.TakeRest(RestCause.Parent),
                    "resume" => market.Resume(RestCause.Parent), // EPIC-012
                    "nextRound" => market.StartNextRound(),
                    _ => false,
                },
            };
            customer.Portrait = new Portrait(art, photo, () => remoteSession.PhotoForCurrentCustomer);
            // The phone customer link starts by itself in the app (VR and desktop), never in batch-mode tests unless
            // a test asks for it. Without a relay on the network nothing changes.
            if (!Application.isBatchMode) StartRelayLink();
            shownAssortment = market.Assortment.Revision;
            foreach (var w in WareInfo.All) if (market.Assortment.IsActive(w)) crateTops[(int)w] = SpawnCrateTop(w);
            var token = Models.Coin(art, stalls[0].Top, "Betalningsbricka");
            tokenText = art.Text(token, "Sum", "1", new Vector3(0, 0.012f, 0), 0.004f, Colors.Ink);
            tokenText.transform.localRotation = Quaternion.Euler(90, 0, 0); // lying on the coin, readable from above
            coin = Item.ForCoin(token);
            HideCoin();
            items.Add(coin);
            for (var h = 0; h < 2; h++)
            {
                var hand = art.Group(transform, h == 0 ? "Left Hand" : "Right Hand");
                art.Part(hand, "Mitten", art.Sphere, art.Matte(Colors.Hand), Vector3.zero, new Vector3(0.05f, 0.035f, 0.07f), null, false);
                art.Part(hand, "Thumb", art.Sphere, art.Matte(Colors.Hand), new Vector3(h == 0 ? 0.025f : -0.025f, 0.008f, 0.01f), new Vector3(0.02f, 0.02f, 0.035f), Quaternion.Euler(0, h == 0 ? -30 : 30, 0), false);
                hand.gameObject.SetActive(false);
                handVisuals[h] = hand;
            }
            statusText = art.Text(stalls[0].Top, "Status", "", new Vector3(-0.3f, 0.3f, 0.05f), 0.0012f, Colors.Ink);
            SetHeight(StandView.DefaultHeight);
            PlaceDesktopCamera();
            Present(0);
        }

        // EPIC-008: 80 coins, unless the developer put a lower number in fest.txt (adb push) for a headset test.
        // Never reachable from the child's controls.
        static int PartyThreshold()
        {
            try
            {
                var file = System.IO.Path.Combine(Application.persistentDataPath, "fest.txt");
                if (System.IO.File.Exists(file) && int.TryParse(System.IO.File.ReadAllText(file).Trim(), out var t) && t > 0)
                {
                    Debug.Log($"[Fruktstand] party threshold {t} (developer fest.txt)");
                    return t;
                }
            }
            catch (Exception) { }
            return MarketRound.DefaultThreshold;
        }

        // The round's numbers for the parent page of the menu.
        public string[] PartyParentLines()
        {
            var r = market.Round;
            var lines = new List<string>
            {
                $"Omgången: {r.Earned} av {r.Threshold} pengar ({r.SalesThisRound} köp)",
                $"Speltid i omgången: {r.RoundSeconds / 60f:0} min",
            };
            if (r.SecondsToThreshold is float s) lines.Add($"Tid till {r.Threshold}: {s / 60f:0.0} min".Replace('.', ','));
            lines.Add($"Festtavlan: {r.Stars} {(r.Stars == 1 ? "stjärna" : "stjärnor")}");
            return lines.ToArray();
        }

        public RelayLink StartRelayLink()
        {
            if (relay == null)
            {
                relay = gameObject.AddComponent<RelayLink>();
                relay.Begin(remoteSession);
            }
            return relay;
        }

        void Update()
        {
            if (!modeDecided) DecideMode();
            if (XrMode)
            {
                Step(xrRig.Read(Time.deltaTime));
                xrRig.AfterStep();
            }
            else if (readDesktopInput) Step(desktop.Read(Time.deltaTime, market.Assortment));
        }

        void DecideMode()
        {
            if (XRSettings.isDeviceActive && xrRig != null)
            {
                XrMode = modeDecided = true;
                if (hud != null) hud.enabled = false;
                QualitySettings.shadowDistance = 3; // only the market close by needs shadows; keeps Quest cheap
                xrRig.Activate(viewCamera, this);
            }
            else if (Time.realtimeSinceStartup > XrDetectSeconds) modeDecided = true;
        }

        // VR, only when asked (start, recenter, menu): the work area's centre goes where the player stands, the fruit
        // stall straight ahead, counters a comfortable distance below the eyes. Walking about never moves it.
        public void PlaceInFront(Vector3 eye, Vector3 forward)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            var yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            marketRoot.SetPositionAndRotation(new Vector3(eye.x, 0, eye.z), Quaternion.Euler(0, yaw, 0));
            SetHeight(eye.y - CounterBelowEyes);
            Placements++;
        }

        public int Placements { get; private set; }

        // EPIC-014: the parent's nature-sound setting is kept between starts (never read or written by batch-mode tests).
        const string NatureSoundKey = "natureSound";

        static NatureSound LoadNatureSound() =>
            Application.isBatchMode ? NatureSound.Normal : (NatureSound)Mathf.Clamp(PlayerPrefs.GetInt(NatureSoundKey, 0), 0, (int)NatureSound.Off);

        static void SaveNatureSound(NatureSound level)
        {
            if (Application.isBatchMode) return;
            PlayerPrefs.SetInt(NatureSoundKey, (int)level);
            PlayerPrefs.Save();
        }

        // EPIC-007: the calibration's proposal on the floor (world pose, spacing d), green if it fits, red if not.
        // The market itself never moves here; its own marking shows too, for comparison.
        public void ShowPreview(Vector3 worldCentre, float worldYaw, float d, bool fits)
        {
            if (preview == null || previewFor != d || previewFits != fits)
            {
                if (preview != null) Art.Discard(preview);
                PreviewColor = fits ? Colors.PreviewGood : Colors.PreviewBad;
                preview = LayoutMarking.Build(art, transform, "Calibration Preview", d, PreviewColor);
                previewFor = d;
                previewFits = fits;
            }
            preview.SetPositionAndRotation(new Vector3(worldCentre.x, 0, worldCentre.z), Quaternion.Euler(0, worldYaw, 0));
            preview.gameObject.SetActive(true);
            marking.gameObject.SetActive(true);
        }

        public void HidePreview()
        {
            if (preview != null) preview.gameObject.SetActive(false);
            if (available != null) available.gameObject.SetActive(false);
            marking.gameObject.SetActive(false);
        }

        // The area the parent has set (width x length), as an outline on the floor next to the proposal.
        public void ShowAvailableArea(Vector3 worldCentre, float worldYaw, Vector2 size)
        {
            if (available == null)
            {
                available = art.Group(transform, "Calibration Area");
                for (var i = 0; i < 4; i++) art.Part(available, "Edge", art.Cube, art.Unlit(Colors.Ink), Vector3.zero, Vector3.one, null, false);
            }
            available.SetPositionAndRotation(new Vector3(worldCentre.x, 0, worldCentre.z), Quaternion.Euler(0, worldYaw, 0));
            const float w = 0.03f, y = 0.012f;
            var edges = new[]
            {
                (new Vector3(0, y, size.y / 2), new Vector3(size.x, 0.002f, w)), (new Vector3(0, y, -size.y / 2), new Vector3(size.x, 0.002f, w)),
                (new Vector3(size.x / 2, y, 0), new Vector3(w, 0.002f, size.y)), (new Vector3(-size.x / 2, y, 0), new Vector3(w, 0.002f, size.y)),
            };
            for (var i = 0; i < 4; i++)
            {
                var e = available.GetChild(i);
                e.localPosition = edges[i].Item1;
                e.localScale = edges[i].Item2;
            }
            available.gameObject.SetActive(true);
        }

        // EPIC-007, only on a confirmed calibration: the whole market (stalls, play table, crocodile shelf and the
        // floor marking) to this world pose with spacing d, in one step. Height, orders, till and play are kept.
        public void ApplyPlacement(Vector3 worldCentre, float worldYaw, float d)
        {
            MarketLayout.SetStallDistance(d);
            foreach (var c in WareInfo.Categories)
            {
                var (p, yaw) = MarketLayout.Stall(c);
                stalls[(int)c].PlaceLocal(p, yaw);
            }
            var (tp, tyaw) = MarketLayout.PlayTable;
            play.PlaceLocal(tp, tyaw);
            marketRoot.SetPositionAndRotation(new Vector3(worldCentre.x, 0, worldCentre.z), Quaternion.Euler(0, worldYaw, 0));
            var wasShown = marking.gameObject.activeSelf;
            marking = BuildMarking();
            marking.gameObject.SetActive(wasShown);
            Physics.SyncTransforms();
            Placements++;
            if (!XrMode) PlaceDesktopCamera();
            Debug.Log($"[Fruktstand] market placed by calibration at {worldCentre:0.00}, yaw {worldYaw:0}, stalls {d:0.00} m from the middle");
        }

        Transform BuildMarking()
        {
            if (marking != null) Art.Discard(marking);
            var m = LayoutMarking.Build(art, marketRoot, "Floor Marking", MarketLayout.StallDistance, Colors.MarkingCurrent);
            m.gameObject.SetActive(false);
            return m;
        }

        public float CounterHeight => stalls[0].Height;


        void SetHeight(float h)
        {
            foreach (var s in stalls) s.SetHeight(h);
            play.SetHeight(h);
            Physics.SyncTransforms();
        }

        // Desktop development camera: an overview, or standing in the work area facing one stall or the play table.
        public void SetDesktopView(DesktopView v)
        {
            View = v;
            PlaceDesktopCamera();
        }

        void PlaceDesktopCamera()
        {
            if (viewCamera == null || XrMode) return;
            var h = CounterHeight;
            if (View == DesktopView.Overview)
            {
                viewCamera.transform.position = ToWorld(new Vector3(0, h + 1.5f, -1.6f));
                viewCamera.transform.LookAt(ToWorld(new Vector3(0, h - 0.2f, 0.2f)));
                return;
            }
            var stand = View == DesktopView.Play ? MarketLayout.PlayStandingSpot : MarketLayout.StandingSpot((Category)((int)View - 1));
            var fwd = new Vector3(stand.x, 0, stand.z).normalized; // from the middle outwards
            viewCamera.transform.position = ToWorld(stand + Vector3.up * (h + 0.5f) - fwd * 0.3f);
            viewCamera.transform.LookAt(ToWorld(stand + fwd * 0.5f + Vector3.up * (h + 0.05f)));
        }

        public void SetStatus(string text) => statusText.text = text ?? "";

        public void Step(ShopFrame f)
        {
            var dt = Mathf.Max(0, f.DeltaTime);
            clock += dt;

            if (f.NewGame) NewGame();
            if (f.ToggleOpen) ToggleOpen();
            if (f.ClearBasket) ClearBasket();
            if (f.TidyPlay) TidyPlayArea();
            if (f.TakeRest && market.TakeRest(RestCause.Child)) Debug.Log("[Fruktstand] rest taken from the menu (no star)");
            if (f.ResumeRest && market.Resume(RestCause.Child)) Debug.Log("[Fruktstand] rest ended from the menu: the same round goes on");
            if (f.ParentRest && market.TakeRest(RestCause.Parent)) Debug.Log("[Fruktstand] paused by the parent (VR)");
            if (f.ParentResume && market.Resume(RestCause.Parent)) Debug.Log("[Fruktstand] pause ended by the parent (VR): the same round goes on");
            if (f.NextRound && market.StartNextRound()) Debug.Log("[Fruktstand] next round started by the parent");
            if (f.NextViewDirect) SetDesktopView((DesktopView)(((int)View + 1) % MarketLayout.ViewCount));
            if (f.CounterHeightDelta != 0)
            {
                var before = CounterHeight;
                SetHeight(before + f.CounterHeightDelta);
                if (CounterHeight != before && !XrMode) PlaceDesktopCamera();
            }
            if (f.ReleaseAll) SendHeldHome();

            // The crocodile game (EPIC-006): where each hand is, paused with menus/focus loss, its own clock.
            for (var h = 0; h < 2; h++)
            {
                var hand = f.Hand(h);
                crocodile.Game.UpdateHand(h, hand.Tracked ? crocodile.OnTeeth(hand.Position) : 0, hand.Tracked && crocodile.InMouth(hand.Position));
            }
            // EPIC-008: during the party and the rest nothing is played: no grabs, no crocodile game, no play at the
            // table (the crocodile's party dance still runs during the party).
            var round = market.Round.State;
            var holdPlay = round is RoundState.Celebrating or RoundState.Resting;
            crocodile.Game.SetPaused(f.ReleaseAll || round == RoundState.Resting);
            crocodile.Game.Tick(dt);
            if (f.CycleNatureSound)
            {
                ambience.Level = Ambience.Next(ambience.Level);
                SaveNatureSound(ambience.Level);
                Debug.Log($"[Fruktstand] nature sound: {ambience.Level}");
            }
            nature.Update(ambience, dt, f.ReleaseAll); // EPIC-014: no birds while paused or in the menu

            foreach (var i in items)
                i.Grabbable = i.Place switch
                {
                    ItemPlace.Crate => true,
                    ItemPlace.Basket => Shop.BasketUnlocked && i.Category == Shop.Category,
                    ItemPlace.Tray => !i.Moving,
                    ItemPlace.Play => !i.Moving,
                    ItemPlace.Fixed => true,
                    _ => false,
                };
            play.UpdateFrogGrabbable(); // EPIC-018: a frog only while it sits on the table
            // Teeth are pressable from the very frame the mouth is open (the crocodile has already ticked).
            for (var t = 0; t < CrocodileGame.Teeth; t++) crocodile.ToothItem(t).Grabbable = crocodile.Game.CanPress(t);
            if (holdPlay)
                foreach (var i in items) i.Grabbable = false;

            for (var h = 0; h < 2; h++)
            {
                var hand = f.Hand(h);
                // The rig moved the hand (stick walk/turn): measure speed from where the hand was in the moved rig.
                if (f.RigDelta is Matrix4x4 rig && lastHand[h] is Vector3 before) lastHand[h] = rig.MultiplyPoint3x4(before);
                TrackVelocity(h, hand, dt);
                var change = grabbers[h].Update(hand, items, grabbers[1 - h].Held);
                if (change == GrabChange.Grabbed) OnGrab((Item)grabbers[h].Held, h);
                if (grabbers[h].Held is Item held && !held.IsPress)
                {
                    held.Transform.position = hand.Position;
                    if (held.IsToy && held.Ware == Ware.Frogs) play.AimFrog(held, hand.Rotation); // EPIC-018: turn to aim
                }
                handVisuals[h].gameObject.SetActive(hand.Tracked && !f.ReleaseAll);
                if (hand.Tracked) handVisuals[h].SetPositionAndRotation(hand.Position, hand.Rotation);
                handVisuals[h].GetChild(0).GetComponent<Renderer>().sharedMaterial = art.Matte(grabbers[h].Hover != null ? Colors.HandHover : Colors.Hand);
                if (change == GrabChange.Released) Drop(lastHeld[h], h);
                else if (change == GrabChange.Lost) SendHome(lastHeld[h]);
                // An empty hand moved through any stall's Öppet/Stängt sign flips it (no button needed).
                for (var c = 0; c < stalls.Length; c++)
                    if (openSwitches[c].Update(h, hand.Tracked, grabbers[h].Held == null, Vector3.Distance(hand.Position, stalls[c].OpenSignBoard.position), !f.ReleaseAll))
                    {
                        ToggleOpen();
                        stalls[c].SwingSign();
                        Haptic?.Invoke(h, 0.4f, 0.06f);
                    }
            }
            foreach (var s in openSwitches) s.Tick(dt);

            if (f.AddDirect is Ware add) AddDirect(add);
            if (f.RemoveLastDirect) RemoveLastDirect();
            if (f.PayDirect && coin.Place == ItemPlace.Tray) RegisterCoin();

            // The party starts only with empty hands, no menu/focus loss, and the crocodile not in its own dance.
            var handsEmpty = grabbers[0].Held == null && grabbers[1].Held == null;
            market.Tick(dt, presentationReady: handsEmpty && !f.ReleaseAll && !crocodile.Game.OwnDanceBusy, paused: f.ReleaseAll);
            // EPIC-016: one pling from the stall when the customer is ready to order; silent (and not saved up) behind a
            // menu, the system menu or a rest. The listener on the camera gives the direction.
            var muted = f.ReleaseAll || market.Round.State is RoundState.Celebrating or RoundState.Resting;
            if (arrivalChime.Update(Shop.State, Shop.CustomerNumber, muted))
                sounds.Arrival(ActiveStall.ToWorld(StandView.BasketCentre + Vector3.up * 0.35f));
            play.KeepFrogPresses(grabbers[0].Held, grabbers[1].Held); // EPIC-018: every way a grab ends undoes a press
            if (!holdPlay) play.Step(PlayArea, dt, clock);
            RoundChanged();
            AssortmentChanged();
            Present(dt);
        }

        // EPIC-009/010: new wares on sale (Nytt spel, or the next round between customers). Every stall shows the new
        // bins and sign; fruit no longer on sale leaves the hands and the counter, and the new crates get their top ware.
        void AssortmentChanged()
        {
            var a = market.Assortment;
            if (a.Revision == shownAssortment) return;
            shownAssortment = a.Revision;
            foreach (var c in WareInfo.Categories) stalls[(int)c].SetWares(a.Active(c));
            for (var i = items.Count - 1; i >= 0; i--)
            {
                // Only the stalls' own wares (crate, basket, hand, on the way back). A toy a child carries or plays
                // with, or goods already paid for, stay: the play area never follows the assortment (EPIC-010).
                var item = items[i];
                if (!item.IsWare || a.IsActive(item.Ware) || item.Place is ItemPlace.Carried or ItemPlace.Paid or ItemPlace.Play) continue;
                for (var h = 0; h < 2; h++)
                    if (grabbers[h].Held == item)
                    {
                        grabbers[h].ForceRelease();
                        lastHeld[h] = null;
                    }
                Remove(item);
            }
            foreach (var w in WareInfo.All)
            {
                if (!a.IsActive(w)) crateTops[(int)w] = null;
                else if (crateTops[(int)w] == null || crateTops[(int)w].Place == ItemPlace.Gone) crateTops[(int)w] = SpawnCrateTop(w);
                else crateTops[(int)w].Snap(CrateTopOf(w), LyingDown(w, 0)); // may have moved to another bin
            }
            LogAssortment();
        }

        void LogAssortment()
        {
            var a = market.Assortment;
            Debug.Log($"[Fruktstand] on sale: {string.Join(", ", a.Active(Category.FruitVeg))} | {string.Join(", ", a.Active(Category.Candy))} | {string.Join(", ", a.Active(Category.Toys))}");
        }


        Vector3 CrateTopOf(Ware w) => StandView.CrateTop(market.Assortment.Bin(w), w);

        // EPIC-008: what happens once when the round's state changes.
        void RoundChanged()
        {
            var r = market.Round;
            if (r.State == lastRound) return;
            if (r.State == RoundState.PartyPending)
                Debug.Log($"[Fruktstand] round reached {r.Earned} coins (threshold {r.Threshold}) after {r.SecondsToThreshold ?? 0:0} s and {r.SalesThisRound} sales");
            if (r.State == RoundState.Celebrating)
            {
                crocodile.Game.StartPartyDance();
                sounds.Fanfare();
                Debug.Log($"[Fruktstand] market party (star {r.Stars})");
            }
            if (r.State == RoundState.Resting)
            {
                SendHeldHome(); // a rest from the phone can come mid-grab
                Debug.Log("[Fruktstand] rest break");
            }
            sounds.Volume = r.State == RoundState.Resting ? 0.35f : 1f;
            lastRound = r.State;
        }

        void TrackVelocity(int h, HandFrame hand, float dt)
        {
            var recent = recentVelocity[h];
            if (!hand.Tracked || dt <= 0)
            {
                lastHand[h] = null;
                handVelocity[h] = Vector3.zero;
                recent.Clear();
                return;
            }
            if (lastHand[h] is Vector3 last) recent.Add(((hand.Position - last) / dt, clock));
            lastHand[h] = hand.Position;
            recent.RemoveAll(e => clock - e.t > ReleaseWindow);
            handVelocity[h] = Vector3.zero;
            foreach (var (v, _) in recent) if (v.sqrMagnitude > handVelocity[h].sqrMagnitude) handVelocity[h] = v;
        }

        void OnGrab(Item item, int hand)
        {
            if (crocodile.ToothOf(item) is var tooth and >= 0)
            {
                // A tooth is pressed like a button: once, then the hand lets go. The rules decide what it does.
                grabbers[hand].ForceRelease();
                lastHeld[hand] = null;
                switch (crocodile.Game.Press(hand, tooth))
                {
                    case ToothPress.Safe:
                        sounds.Tick();
                        Haptic?.Invoke(hand, 0.35f, 0.05f);
                        break;
                    case ToothPress.Snap:
                        sounds.Clack();
                        Haptic?.Invoke(hand, 0.6f, 0.1f);
                        Debug.Log($"[Fruktstand] crocodile snapped ({crocodile.Game.Snaps})");
                        break;
                }
                return;
            }
            if (item.IsPress)
            {
                // EPIC-018: a frog's rear stays pressed while the grip is held; letting go (Drop) is the hop.
                if (play.FrogPress(item))
                {
                    lastHeld[hand] = item;
                    Haptic?.Invoke(hand, 0.3f, 0.05f);
                }
                else
                {
                    grabbers[hand].ForceRelease();
                    lastHeld[hand] = null;
                }
                return;
            }
            if (item.IsButton)
            {
                // A press acts once, then lets go at once; the grabber needs a fresh press before anything else.
                grabbers[hand].ForceRelease();
                lastHeld[hand] = null;
                item.OnPress?.Invoke();
                Haptic?.Invoke(hand, 0.35f, 0.05f);
                return;
            }
            lastHeld[hand] = item;
            switch (item.Place)
            {
                case ItemPlace.Crate:
                    crateTops[(int)item.Ware] = SpawnCrateTop(item.Ware); // unlimited stock
                    break;
                case ItemPlace.Basket:
                    Shop.TakeFromBasket(item.Slot);
                    item.Slot = -1;
                    break;
                case ItemPlace.Play:
                    play.PlayerTook(PlayArea, item); // the child waits while the player has it
                    break;
            }
            item.Place = ItemPlace.Held;
            item.OnArrive = null;
            if (!item.IsToy) item.Transform.localScale = Vector3.one;
            sounds.Tick();
            Haptic?.Invoke(hand, 0.25f, 0.04f);
        }

        void Drop(Item item, int hand)
        {
            if (item == null) return;
            if (item.IsPress)
            {
                play.FrogRelease(item); // EPIC-018: the hand lifts: one hop
                Haptic?.Invoke(hand, 0.5f, 0.08f);
                return;
            }
            if (item.IsToy)
            {
                play.PlayerReleased(PlayArea, item, handVelocity[hand]);
                return;
            }
            var stall = ActiveStall;
            var local = stall.ToCounter(item.Position);
            switch (DropRules.Resolve(item.IsCoin, local, StandView.BasketZone, StandView.RegisterZone))
            {
                case DropTarget.Basket when PutInBasket(item, NearestFreeSlot(local)):
                    Haptic?.Invoke(hand, 0.5f, 0.06f);
                    return;
                case DropTarget.Register:
                    if (RegisterCoin()) Haptic?.Invoke(hand, 0.8f, 0.12f);
                    return;
                default:
                    SendHome(item);
                    return;
            }
        }

        int NearestFreeSlot(Vector3 local)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var s = 0; s < Basket.Capacity; s++)
            {
                if (Shop.Basket[s].HasValue) continue;
                var d = (new Vector2(local.x, local.z) - new Vector2(StandView.Slots[s].x, StandView.Slots[s].z)).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = s;
                }
            }
            return best;
        }

        // Only a ware of the active order's category, at its own stall, goes in (it is a child of that stall's top).
        bool PutInBasket(Item item, int preferredSlot)
        {
            if (!item.IsWare || item.Category != Shop.Category) return false;
            var slot = Shop.TryAddToBasket(item.Ware, preferredSlot);
            if (slot < 0) return false;
            item.Place = ItemPlace.Basket;
            item.Slot = slot;
            item.MoveTo(StandView.SlotRest(slot, item.Ware), LyingDown(item.Ware, slot), SnapSeconds);
            sounds.Pop();
            return true;
        }

        // The payment token went into the register. Registers the sale (and the order's sum to the till) through
        // Shop.TryPay, which succeeds exactly once; any repeated event is ignored. A toy goes with the child.
        public bool RegisterCoin()
        {
            if (coin.Place == ItemPlace.Paid || coin.Place == ItemPlace.Gone) return false;
            var order = Shop.Order;
            var number = Shop.CustomerNumber;
            if (!Shop.TryPay())
            {
                SendHome(coin);
                return false;
            }
            SalesRegistered++;
            coin.Place = ItemPlace.Paid;
            coin.Grabbable = false;
            coin.MoveTo(StandView.RegisterCentre + Vector3.up * 0.06f, Quaternion.Euler(90, 0, 0), 0.25f, 0.3f);
            sounds.Pling();
            var stall = ActiveStall;
            Celebrate(stall.ToWorld(StandView.RegisterCentre + Vector3.up * 0.1f));
            Celebrate(customer.Root.position + Vector3.up * (customer.IsChild ? 0.8f : 1.1f));
            Haptic?.Invoke(0, 0.6f, 0.15f);
            Haptic?.Invoke(1, 0.6f, 0.15f);
            if (order.Toy is Ware toy)
            {
                // The child takes the very toy out of the basket and walks to the reserved play spot with it.
                Item bought = null;
                foreach (var i in items) if (i.Place == ItemPlace.Basket && i.Ware == toy) bought = i;
                if (bought != null) items.Remove(bought);
                play.StartCarry(toy, bought, customer.Root.position, number);
            }
            Debug.Log($"[Fruktstand] sold {order} at {WareInfo.Name(order.Category)}; till {Wallet.Balance}");
            return true;
        }

        void AddDirect(Ware w)
        {
            if (!Shop.AcceptsItems || WareInfo.CategoryOf(w) != Shop.Category || !market.Assortment.IsActive(w)) return;
            var item = NewWare(w);
            item.Snap(CrateTopOf(w), Quaternion.identity);
            if (!PutInBasket(item, -1)) SendHome(item);
        }

        void RemoveLastDirect()
        {
            for (var s = Basket.Capacity - 1; s >= 0; s--)
            {
                var item = BasketItem(s);
                if (item == null || Shop.TakeFromBasket(s) == null) continue;
                item.Slot = -1;
                SendHome(item);
                return;
            }
        }

        // Focus lost, paused, menu open or a rest: whatever the hands hold goes home (a frog press is undone), nothing
        // is dropped anywhere.
        void SendHeldHome()
        {
            for (var h = 0; h < 2; h++)
                if (grabbers[h].ForceRelease() is Item held) SendHome(held);
        }

        void SendHome(Item item)
        {
            if (item == null || item.IsButton) return;
            if (item.IsPress)
            {
                play.FrogCancel(item); // EPIC-018: paused, menu, focus or tracking lost mid-press: no hop
                return;
            }
            item.Grabbable = false;
            if (item.IsCoin)
            {
                if (!Shop.CoinOnCounter)
                {
                    HideCoin();
                    return;
                }
                item.Place = ItemPlace.Returning;
                item.MoveTo(StandView.CoinSpot, Quaternion.identity, ReturnSeconds);
                return;
            }
            if (item.IsToy)
            {
                item.Place = ItemPlace.Play;
                play.Reset(PlayArea, item); // lying still on its place; the child carries on
                return;
            }
            item.Slot = -1;
            if (!market.Assortment.IsActive(item.Ware))
            {
                Remove(item); // no bin to go back to (EPIC-009)
                return;
            }
            item.Place = ItemPlace.Returning;
            item.MoveTo(CrateTopOf(item.Ware), Quaternion.identity, ReturnSeconds, 0.5f);
        }

        void ToggleOpen()
        {
            Shop.SetOpen(!Shop.IsOpen);
            sounds.Tick();
            Debug.Log($"[Fruktstand] market {(Shop.IsOpen ? "open" : "closed")}");
        }

        // "Återställ beställningen": the current order starts over; the goods go back to their bins. Till untouched.
        void ClearBasket()
        {
            if (!Shop.ClearBasket()) return;
            foreach (var i in items)
                if (i.Place == ItemPlace.Basket) SendHome(i);
            for (var h = 0; h < 2; h++)
                if (grabbers[h].Held is Item held && held.IsCoin)
                {
                    grabbers[h].ForceRelease();
                    SendHome(held);
                }
        }

        // "Städa lekytan": every toy that isn't in a hand back to its place. Till and play time untouched.
        public void TidyPlayArea()
        {
            play.Tidy(PlayArea);
            sounds.Tick();
        }

        // "Nytt spel": everything back to the start, the till included. Only after the menu's Ja.
        public void NewGame()
        {
            for (var h = 0; h < 2; h++)
            {
                grabbers[h].ForceRelease();
                lastHeld[h] = null;
            }
            play.Clear();
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (item.IsCoin || item.IsButton || item.Place == ItemPlace.Crate) continue;
                Remove(item);
            }
            market.NewGame();
            visitors.Clear(); // EPIC-015: the park empties, the bench is free
            lastCustomer = (Shop.State, Shop.CustomerNumber, Shop.Order.Category, Shop.CustomerIsChild);
            AssortmentChanged(); // EPIC-009: new fruit at once
            crocodile.Game.NewGame(); // the count and the surprise start over; tidying never does this
            HideCoin();
            Debug.Log("[Fruktstand] new game");
        }

        Item SpawnCrateTop(Ware w)
        {
            var item = NewWare(w);
            item.Place = ItemPlace.Crate;
            item.Snap(CrateTopOf(w), LyingDown(w, 0));
            return item;
        }

        Item NewWare(Ware w)
        {
            var stall = stalls[(int)WareInfo.CategoryOf(w)];
            var item = Item.ForWare(Models.Ware(art, stall.Top, w), w);
            items.Add(item);
            return item;
        }

        static Quaternion LyingDown(Ware w, int slot) =>
            StandView.Upright(w) ? Quaternion.identity : Quaternion.Euler(90, slot % 2 == 0 ? 10 : -10, 0);

        void Remove(Item item)
        {
            items.Remove(item);
            item.Place = ItemPlace.Gone;
            if (item.Transform != null) Art.Discard(item.Transform);
        }

        void HideCoin()
        {
            coin.Place = ItemPlace.Gone;
            coin.Grabbable = false;
            coin.Transform.gameObject.SetActive(false);
            coin.Transform.localScale = Vector3.one;
        }

        // The customer lays the payment token on the counter of the active stall; it shows the order's sum.
        void ShowCoin()
        {
            coin.Transform.SetParent(ActiveStall.Top, false);
            coin.Transform.gameObject.SetActive(true);
            coin.Transform.localScale = Vector3.one;
            tokenText.text = Shop.Order.Sum.ToString();
            coin.Snap(StandView.CoinSpot + new Vector3(-0.05f, 0.25f, 0.3f), Quaternion.identity); // from the customer's hand
            coin.MoveTo(StandView.CoinSpot, Quaternion.identity, 0.5f);
            coin.Place = ItemPlace.Tray;
        }

        // Brings every view in line with the rules and advances animations.
        void Present(float dt)
        {
            crocodile.Present(dt, clock);
            var active = ActiveStall;
            customer.AtStall(active.Root);
            bubble.AtStall(active.Top);
            if (Shop.CoinOnCounter && (coin.Place == ItemPlace.Gone || (coin.Place == ItemPlace.Paid && !coin.Moving))) ShowCoin();
            if (!Shop.CoinOnCounter && (coin.Place == ItemPlace.Tray || coin.Place == ItemPlace.Returning)) HideCoin();

            // Basket emptied by the shop (sale done): the goods go to the customer.
            foreach (var i in items)
                if (i.Place == ItemPlace.Basket && Shop.Basket[i.Slot] != i.Ware)
                {
                    i.Place = ItemPlace.Returning; // reuse the tween, then removed below
                    i.Slot = -1;
                    i.MoveTo(stalls[(int)i.Category].ToCounter(customer.Root.position + Vector3.up * 0.6f), Quaternion.identity, 0.5f, 0.2f);
                }

            for (var idx = items.Count - 1; idx >= 0; idx--)
            {
                var i = items[idx];
                if (i.Animate(dt))
                {
                    if (i.Place == ItemPlace.Returning && i.IsWare) Remove(i);
                    else if (i.Place == ItemPlace.Returning && i.IsCoin) i.Place = ItemPlace.Tray;
                    else if (i.Place == ItemPlace.Paid) i.Transform.gameObject.SetActive(false);
                }
            }
            // Items that aren't part of the order wiggle gently in the basket (a hint, never a penalty).
            var extras = new int[WareInfo.Count];
            for (var s = 0; s < Basket.Capacity; s++)
            {
                var i = BasketItem(s);
                if (i == null || i.Moving) continue;
                extras[(int)i.Ware]++;
                var extra = Shop.State == SaleState.Ordering && extras[(int)i.Ware] > Shop.Order.Count(i.Ware);
                var baseRotation = LyingDown(i.Ware, s);
                i.Transform.localRotation = extra ? baseRotation * Quaternion.Euler(0, 0, Mathf.Sin(clock * 6) * 12) : baseRotation;
            }
            // Hover highlight (not for physics toys: their colliders must keep their size).
            foreach (var i in items)
            {
                if (i.Moving || i.IsToy) continue;
                var hovered = i == grabbers[0].Hover || i == grabbers[1].Hover;
                if (i.Place is ItemPlace.Crate or ItemPlace.Basket or ItemPlace.Tray or ItemPlace.Fixed)
                    i.Transform.localScale = Vector3.one * (hovered ? 1.18f : 1f);
            }

            var heldCoinOverRegister = false;
            var heldWareOverBasket = false;
            for (var h = 0; h < 2; h++)
                if (grabbers[h].Held is Item held && !held.IsToy)
                {
                    var local = active.ToCounter(held.Position);
                    if (held.IsCoin) heldCoinOverRegister |= StandView.RegisterZone.Contains(local);
                    else heldWareOverBasket |= Shop.AcceptsItems && held.Category == Shop.Category && StandView.BasketZone.Contains(local);
                }
            foreach (var s in stalls)
            {
                var isActive = s == active;
                s.SetRegisterGlow(isActive && heldCoinOverRegister);
                s.SetBasketGlow(isActive && (heldWareOverBasket || Shop.State == SaleState.AwaitingPayment));
                s.ShowTill(Wallet.Balance, dt);
                s.ShowOpen(Shop.IsOpen, dt);
            }
            HandOverLeavingCustomer();
            var marketHeld = market.Round.State is RoundState.Celebrating or RoundState.Resting; // as Market.Tick
            // EPIC-016: everybody keeps their distance on the paths: the customer to the visitors and the children, the
            // visitors to the customer, the children and each other (Pacer).
            figuresNear.Clear();
            visitors.AddWalkers(figuresNear);
            play.AddChildren(figuresNear, ToMarket);
            customer.Show(Shop, clock, dt, marketHeld, marketRoot, MarketLayout.StallDistance, figuresNear);
            if (Shop.State == SaleState.CustomerArriving && customer.AtCounter) Shop.CustomerArrived();
            figuresNear.Clear();
            if (customer.Root.gameObject.activeInHierarchy)
            {
                var at = ToMarket(customer.Root.position);
                at.y = 0;
                figuresNear.Add(customer.Walking ? new Walker(at, customer.Velocity, CustomerView.Priority) : Walker.Standing(at));
            }
            play.AddChildren(figuresNear, ToMarket);
            visitors.Update(dt, marketHeld, figuresNear);
            foreach (var s in stalls) s.ShowWaiting(s == active && ArrivalChime.Waiting(Shop.State), clock); // EPIC-016
            var celebrating = market.Round.State == RoundState.Celebrating;
            customer.Cheer(celebrating, clock);
            if (celebrating) play.Cheer(clock);
            var shopDone = Shop.State == SaleState.Closed && market.Remote.ActiveId == null && !PlayArea.AnyWalking;
            var holding = grabbers[0].Held != null || grabbers[1].Held != null;
            party.Update(market.Round.State, market.Round.StateTime, market.Round.Stars,
                market.Round.State == RoundState.PartyPending && shopDone && holding, viewCamera != null ? viewCamera.transform : null, dt, clock, market.Round.RestBy);
            bubble.Show(Shop, clock, customer.Near);
            AnimateSparks(dt);
        }

        readonly List<Walker> figuresNear = new();
        readonly ArrivalChime arrivalChime = new(); // EPIC-016

        // EPIC-015: the customer stepped away from the counter and the counter is free (the next customer is coming, or
        // the market closed): a park visitor takes over right there and strolls on. A toy child went to play instead.
        void HandOverLeavingCustomer()
        {
            var (state, number, category, child) = lastCustomer;
            if (state == SaleState.CustomerLeaving && (Shop.State != SaleState.CustomerLeaving || Shop.CustomerNumber != number) && category != Category.Toys)
            {
                var d = MarketLayout.StallDistance;
                visitors.Add(number, child, plan => ParkRoutes.Departure(category, d, plan), customer.Walked, customer.Speed); // walks on at the customer's pace
            }
            lastCustomer = (Shop.State, Shop.CustomerNumber, Shop.Order.Category, Shop.CustomerIsChild);
        }

        void Celebrate(Vector3 at)
        {
            for (var k = 0; k < 7; k++)
            {
                var a = k * Mathf.PI * 2 / 7;
                var star = Models.Star(art, transform, transform.InverseTransformPoint(at), 0.035f);
                star.rotation = Quaternion.LookRotation(star.position - (viewCamera != null ? viewCamera.transform.position : at - Vector3.forward));
                sparks.Add((star, new Vector3(Mathf.Cos(a) * 0.25f, 0.45f + Mathf.Sin(a) * 0.15f, Mathf.Sin(a) * 0.1f), 1.2f));
            }
        }

        void AnimateSparks(float dt)
        {
            for (var k = sparks.Count - 1; k >= 0; k--)
            {
                var (t, v, life) = sparks[k];
                life -= dt;
                if (life <= 0 || t == null)
                {
                    if (t != null) Destroy(t.gameObject);
                    sparks.RemoveAt(k);
                    continue;
                }
                t.position += v * dt;
                v.y -= 0.4f * dt;
                t.localScale = Vector3.one * 0.035f * Mathf.Clamp01(life / 0.6f);
                sparks[k] = (t, v, life);
            }
        }
    }
}
