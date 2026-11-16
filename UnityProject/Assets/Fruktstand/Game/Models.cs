// Produce (EPIC-009: Blender models, primitives as the fallback), coin token, register and star models. Told apart by colour AND shape:
// apple = round red, banana = long curved yellow, carrot = pointed orange cone with green leaves.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public static class Colors
    {
        public static readonly Color Apple = new(0.86f, 0.11f, 0.12f);
        public static readonly Color Banana = new(0.98f, 0.84f, 0.16f);
        public static readonly Color Carrot = new(1f, 0.5f, 0.06f);
        public static readonly Color Leaf = new(0.2f, 0.62f, 0.18f);
        public static readonly Color Stem = new(0.38f, 0.24f, 0.12f);
        public static readonly Color BananaTip = new(0.22f, 0.14f, 0.07f);
        public static readonly Color Pear = new(0.72f, 0.84f, 0.25f);          // EPIC-009 fruit
        public static readonly Color Orange = new(1f, 0.58f, 0.08f);
        public static readonly Color Strawberry = new(0.93f, 0.13f, 0.22f);
        public static readonly Color StrawberrySeed = new(1f, 0.92f, 0.55f);
        public static readonly Color Grape = new(0.52f, 0.24f, 0.62f);
        public static readonly Color GrapeStem = new(0.45f, 0.42f, 0.18f);
        public static readonly Color MelonFlesh = new(0.97f, 0.3f, 0.36f);
        public static readonly Color MelonRind = new(0.18f, 0.55f, 0.22f);
        public static readonly Color MelonRindLight = new(0.82f, 0.93f, 0.7f);
        public static readonly Color StickWhite = new(0.97f, 0.96f, 0.92f);    // EPIC-010 candy
        public static readonly Color BagDot = new(1f, 0.85f, 0.2f);
        public static readonly Color Gummy = new(0.35f, 0.85f, 0.35f);
        public static readonly Color Chocolate = new(0.42f, 0.24f, 0.13f);
        public static readonly Color Wrapper = new(0.85f, 0.15f, 0.2f);
        public static readonly Color Foil = new(0.85f, 0.82f, 0.7f);
        public static readonly Color Toffee = new(1f, 0.6f, 0.12f);
        public static readonly Color CandyPaper = new(1f, 0.88f, 0.3f);
        public static readonly Color RingBand = new(0.4f, 0.85f, 0.85f);
        public static readonly Color ToyRed = new(0.95f, 0.25f, 0.25f);     // EPIC-010 toys
        public static readonly Color ToyYellow = new(1f, 0.82f, 0.2f);
        public static readonly Color ToyBlue = new(0.25f, 0.5f, 0.95f);
        public static readonly Color ToyGreen = new(0.35f, 0.78f, 0.4f);
        // EPIC-018: the frogs (art/toys/make_toys.py), the lily pad and the pond mat.
        public static readonly Color FrogGreen = new(0.35f, 0.78f, 0.3f);
        public static readonly Color FrogOrange = new(1f, 0.55f, 0.2f);
        public static readonly Color FrogBelly = new(0.85f, 0.95f, 0.6f);
        public static readonly Color LilyPad = new(0.2f, 0.6f, 0.3f);
        public static readonly Color Pond = new(0.45f, 0.72f, 0.92f);
        public static readonly Color ToyWindow = new(0.8f, 0.92f, 1f);
        public static readonly Color ToyWood = new(0.85f, 0.66f, 0.42f);
        public static readonly Color[] RingColours = { ToyRed, ToyYellow, ToyBlue }; // biggest first
        public static readonly Color[] Rainbow = { new(0.93f, 0.2f, 0.2f), new(1f, 0.58f, 0.1f), new(1f, 0.88f, 0.2f), new(0.35f, 0.8f, 0.35f), new(0.3f, 0.55f, 0.95f), new(0.6f, 0.35f, 0.85f) };
        public static readonly Color Gold = new(1f, 0.78f, 0.15f);
        public static readonly Color GoldLight = new(1f, 0.92f, 0.5f);
        public static readonly Color Wood = new(0.72f, 0.5f, 0.3f);
        public static readonly Color WoodDark = new(0.5f, 0.33f, 0.19f);
        public static readonly Color Crate = new(0.84f, 0.66f, 0.42f);
        public static readonly Color Wicker = new(0.78f, 0.58f, 0.3f);
        public static readonly Color AwningRed = new(0.9f, 0.22f, 0.25f);
        public static readonly Color AwningWhite = new(0.98f, 0.96f, 0.92f);
        public static readonly Color Register = new(0.2f, 0.6f, 0.62f);
        public static readonly Color RegisterGlow = new(1f, 0.9f, 0.3f);
        public static readonly Color Slot = new(0.12f, 0.12f, 0.14f);
        public static readonly Color Bubble = new(1f, 1f, 1f);
        public static readonly Color BubbleEdge = new(0.25f, 0.25f, 0.3f);
        public static readonly Color TickOff = new(0.86f, 0.86f, 0.86f);
        public static readonly Color TickOn = new(0.25f, 0.75f, 0.3f);
        public static readonly Color Ink = new(0.12f, 0.12f, 0.16f);
        public static readonly Color Hand = new(0.98f, 0.9f, 0.78f);
        public static readonly Color HandHover = new(1f, 0.85f, 0.2f);
        public static readonly Color Star = new(1f, 0.82f, 0.1f);
        public static readonly Color CandyPink = new(1f, 0.45f, 0.72f);
        public static readonly Color CandyPinkLight = new(1f, 0.8f, 0.9f);
        public static readonly Color Mint = new(0.55f, 0.9f, 0.78f);
        public static readonly Color MintDark = new(0.3f, 0.7f, 0.6f);
        public static readonly Color CaneRed = new(0.9f, 0.1f, 0.15f);
        public static readonly Color BagBlue = new(0.55f, 0.75f, 1f);
        public static readonly Color Paper = new(0.8f, 0.65f, 0.45f);
        public static readonly Color Affordable = new(0.45f, 0.85f, 0.45f);
        public static readonly Color NotAffordable = new(0.82f, 0.82f, 0.82f);
        public static readonly Color OpenSign = new(0.3f, 0.78f, 0.35f);
        public static readonly Color PreviewGood = new(0.3f, 0.75f, 0.45f); // EPIC-007 calibration: fits
        public static readonly Color PreviewBad = new(0.9f, 0.35f, 0.3f);   // doesn't fit
        public static readonly Color MarkingCurrent = new(0.5f, 0.58f, 0.72f); // where the market is now (grey-blue)
        public static readonly Color ClosedSign = new(0.9f, 0.3f, 0.3f);
    }

    public static class Models
    {
        public static float Radius(Core.Ware w) => w switch
        {
            Core.Ware.Apple => 0.045f,
            Core.Ware.Banana => 0.07f,
            Core.Ware.Carrot => 0.06f,
            Core.Ware.Pear => 0.05f,
            Core.Ware.Orange => 0.045f,
            Core.Ware.Strawberry => 0.04f,
            Core.Ware.Grapes => 0.055f,
            Core.Ware.Watermelon => 0.06f,
            Core.Ware.Lollipop => 0.05f,
            Core.Ware.Polkagris => 0.06f,
            Core.Ware.CandyBag => 0.05f,
            Core.Ware.GummyBear => 0.04f,
            Core.Ware.Chocolate => 0.05f,
            Core.Ware.WrappedCandy => 0.05f,
            Core.Ware.RainbowStrip => 0.06f,
            Core.Ware.CandyRing => 0.045f,
            Core.Ware.Ball => BallRadius,
            Core.Ware.Car => 0.06f,
            Core.Ware.Top => 0.045f,
            Core.Ware.Train => 0.07f,
            Core.Ware.StackingRings => 0.055f,
            Core.Ware.Frogs => 0.06f,
            _ => 0.06f,
        };

        public const float BallRadius = 0.045f;
        public const float BlockSize = 0.06f;
        public static readonly Vector3 CarSize = new(0.07f, 0.045f, 0.12f); // body box (x wide, y tall, z long)
        public static readonly Color[] BlockColors = { new(0.95f, 0.3f, 0.3f), new(1f, 0.75f, 0.2f), new(0.35f, 0.7f, 0.95f), new(0.45f, 0.8f, 0.4f), new(0.75f, 0.5f, 0.9f), new(1f, 0.55f, 0.75f) };

        // Any ware's picture/model, root at its centre (the grab point): its Blender model when there is one
        // (EPIC-009/010), otherwise primitives.
        public static Transform Ware(Art art, Transform parent, Core.Ware w, string name = null)
        {
            if (art.ModelOf(w) is Mesh model)
            {
                var root = art.Group(parent, name ?? WareInfo.Name(w));
                ModelPart(art, root, "Model", model, ModelColours(w));
                return root;
            }
            switch (WareInfo.CategoryOf(w))
            {
                case Category.FruitVeg: return Produce(art, parent, w, name);
                case Category.Candy: return Candy(art, parent, w, name);
                default: return Toy(art, parent, w, name);
            }
        }

        public const float CoinRadius = 0.045f;

        // Root at the candy's centre (the grab point). Told apart by shape and colour: flat round swirl on a stick,
        // long red-and-white striped stick, little blue bag with a bow.
        static Transform Candy(Art art, Transform parent, Core.Ware c, string name = null)
        {
            var root = art.Group(parent, name ?? WareInfo.Name(c));
            switch (c)
            {
                case Core.Ware.Lollipop:
                    art.Part(root, "Stick", art.Cylinder, art.Matte(Color.white), new Vector3(0, -0.05f, 0), new Vector3(0.008f, 0.04f, 0.008f));
                    art.Part(root, "Candy", art.Cylinder, art.Matte(Colors.CandyPink), new Vector3(0, 0.015f, 0), new Vector3(0.075f, 0.007f, 0.075f), Quaternion.Euler(90, 0, 0));
                    for (var i = 0; i < 2; i++)
                        art.Part(root, "Swirl", art.Cylinder, art.Matte(i == 0 ? Colors.CandyPinkLight : Color.white), new Vector3(0, 0.015f, -0.0075f - i * 0.001f),
                            new Vector3(0.05f - i * 0.022f, 0.001f, 0.05f - i * 0.022f), Quaternion.Euler(90, 0, 0), false);
                    break;
                case Core.Ware.Polkagris:
                    art.Part(root, "Stick", art.Cylinder, art.Matte(Color.white), Vector3.zero, new Vector3(0.022f, 0.07f, 0.022f));
                    for (var i = 0; i < 6; i++)
                        art.Part(root, "Stripe", art.Cylinder, art.Matte(Colors.CaneRed), new Vector3(0, -0.055f + i * 0.022f, 0), new Vector3(0.0235f, 0.004f, 0.0235f), Quaternion.Euler(0, 0, 18), false);
                    break;
                case Core.Ware.GummyBear or Core.Ware.Chocolate or Core.Ware.WrappedCandy or Core.Ware.RainbowStrip or Core.Ware.CandyRing:
                    // fallback without the asset: a ball in the ware's main colour
                    art.Part(root, "Candy", art.Sphere, art.Matte(ModelColours(c)[0]), Vector3.zero, Vector3.one * Radius(c) * 1.4f);
                    break;
                default:
                    art.Part(root, "Bag", art.Cube, art.Matte(Colors.BagBlue), new Vector3(0, -0.01f, 0), new Vector3(0.065f, 0.06f, 0.04f));
                    art.Part(root, "Top", art.Sphere, art.Matte(Colors.BagBlue), new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.03f, 0.03f));
                    art.Part(root, "Bow", art.Sphere, art.Matte(Colors.CandyPink), new Vector3(-0.012f, 0.022f, -0.02f), new Vector3(0.022f, 0.014f, 0.01f), Quaternion.Euler(0, 0, 25), false);
                    art.Part(root, "Bow", art.Sphere, art.Matte(Colors.CandyPink), new Vector3(0.012f, 0.022f, -0.02f), new Vector3(0.022f, 0.014f, 0.01f), Quaternion.Euler(0, 0, -25), false);
                    var dots = new[] { Colors.Banana, Colors.Apple, Colors.Leaf, Colors.CandyPink };
                    for (var i = 0; i < dots.Length; i++)
                        art.Part(root, "Sweet", art.Sphere, art.Matte(dots[i]), new Vector3(-0.022f + i * 0.015f, -0.012f + (i % 2) * 0.016f, -0.021f), Vector3.one * 0.012f, null, false);
                    break;
            }
            return root;
        }

        // "You have N": coin + big number, as a small board. Text faces away from its forward (+z), like TextMesh.
        public static TextMesh WalletBadge(Art art, Transform parent, Vector3 position, float size, out Transform root)
        {
            root = art.Group(parent, "Wallet", position);
            art.Part(root, "Board", art.Quad, art.Unlit(Colors.Bubble), new Vector3(0, 0, 0.004f), new Vector3(size * 2.1f, size, 1), null, false);
            art.Part(root, "Edge", art.Quad, art.Unlit(Colors.Gold), new Vector3(0, 0, 0.006f), new Vector3(size * 2.1f + 0.008f, size + 0.008f, 1), null, false);
            CoinIcon(art, root, new Vector3(-size * 0.5f, 0, -0.002f), size * 0.8f);
            return art.Text(root, "Amount", "0", new Vector3(size * 0.5f, 0, -0.004f), size * 0.065f, Colors.Ink);
        }

        // Root at the item's centre (the grab point).
        // Toys: a red-and-yellow ball, a blue car (body, cabin, four wheels; +z is forward), and the block set as a
        // little 2 x 3 stack of the six coloured blocks (it becomes six full-size blocks on the play table).
        static Transform Toy(Art art, Transform parent, Core.Ware t, string name = null)
        {
            var root = art.Group(parent, name ?? WareInfo.Name(t));
            switch (t)
            {
                case Core.Ware.Ball:
                    art.Part(root, "Ball", art.Sphere, art.Matte(new Color(0.95f, 0.25f, 0.25f)), Vector3.zero, Vector3.one * BallRadius * 2);
                    art.Part(root, "Band", art.Sphere, art.Matte(Colors.Banana), Vector3.zero, new Vector3(BallRadius * 2.02f, BallRadius * 0.7f, BallRadius * 2.02f));
                    break;
                case Core.Ware.Car:
                    var blue = art.Matte(new Color(0.25f, 0.5f, 0.95f));
                    art.Part(root, "Body", art.Cube, blue, new Vector3(0, 0, 0), CarSize);
                    art.Part(root, "Cabin", art.Cube, blue, new Vector3(0, CarSize.y * 0.8f, -0.01f), new Vector3(CarSize.x * 0.85f, CarSize.y * 0.7f, CarSize.z * 0.5f));
                    art.Part(root, "Window", art.Cube, art.Unlit(new Color(0.8f, 0.92f, 1f)), new Vector3(0, CarSize.y * 0.85f, CarSize.z * 0.25f - 0.01f + 0.001f), new Vector3(CarSize.x * 0.75f, CarSize.y * 0.45f, 0.004f), null, false);
                    foreach (var x in new[] { -1f, 1f })
                    foreach (var z in new[] { -1f, 1f })
                        art.Part(root, "Wheel", art.Cylinder, art.Matte(Colors.Ink), new Vector3(x * CarSize.x * 0.5f, -CarSize.y * 0.45f, z * CarSize.z * 0.32f), new Vector3(0.032f, 0.008f, 0.032f), Quaternion.Euler(0, 0, 90));
                    break;
                case Core.Ware.Top or Core.Ware.Train or Core.Ware.StackingRings or Core.Ware.Frogs:
                    // fallback without the asset: a ball in the toy's main colour
                    art.Part(root, "Toy", art.Sphere, art.Matte(ModelColours(t)[0]), Vector3.zero, Vector3.one * Radius(t) * 1.4f);
                    break;
                default:
                    for (var i = 0; i < WareInfo.BlocksInSet; i++)
                        art.Part(root, "Block", art.Cube, art.Matte(BlockColors[i]), new Vector3(-0.022f + 0.022f * (i % 3), -0.011f + 0.022f * (i / 3), 0), Vector3.one * 0.021f);
                    break;
            }
            return root;
        }

        // EPIC-010 stacking rings at the play table: ring 0 is the biggest. Root at the ring's centre.
        public static readonly float[] RingScale = { 1f, 0.83f, 0.67f };
        public const float RingRadius = 0.041f;   // outer radius of the biggest ring (Blender: 0.03 + 0.011)
        public const float RingThickness = 0.022f; // of the biggest ring

        public static Transform Ring(Art art, Transform parent, int index)
        {
            var root = art.Group(parent, "Ring " + (index + 1));
            var colour = new[] { Colors.RingColours[index % Colors.RingColours.Length] };
            var part = art.RingModel != null
                ? ModelPart(art, root, "Ring", art.RingModel, colour)
                : art.Part(root, "Ring", art.Cylinder, art.Matte(colour[0]), Vector3.zero, new Vector3(RingRadius * 2, RingThickness / 2, RingRadius * 2));
            part.localScale *= RingScale[index];
            return root;
        }

        // The rings' fixed base and peg (no collider, not grabbable). Root at the base's bottom centre.
        public static Transform RingPeg(Art art, Transform parent)
        {
            var root = art.Group(parent, "Ring Peg");
            if (art.RingPegModel != null)
            {
                var part = ModelPart(art, root, "Peg", art.RingPegModel, new[] { Colors.ToyWood });
                part.localPosition = new Vector3(0, art.RingPegModel.bounds.extents.y, 0);
            }
            else art.Part(root, "Peg", art.Cylinder, art.Matte(Colors.ToyWood), new Vector3(0, 0.05f, 0), new Vector3(0.016f, 0.05f, 0.016f));
            return root;
        }

        // EPIC-018: one hopping frog for the play table (the shop set's frog), coloured by owner: the player's green,
        // the child's orange. Root at the model's centre; +z is the way it faces.
        public static Transform Frog(Art art, Transform parent, bool childs)
        {
            var root = art.Group(parent, childs ? "Child's Frog" : "Player's Frog");
            var body = childs ? Colors.FrogOrange : Colors.FrogGreen;
            if (art.FrogModel != null)
                ModelPart(art, root, "Frog", art.FrogModel, new[] { body, Colors.FrogBelly, Colors.StickWhite, Colors.Ink, Colors.ToyYellow });
            else
            {
                art.Part(root, "Body", art.Sphere, art.Matte(body), Vector3.zero, new Vector3(0.07f, 0.045f, 0.08f));
                foreach (var x in new[] { -1f, 1f }) art.Part(root, "Eye", art.Sphere, art.Matte(Colors.StickWhite), new Vector3(x * 0.019f, 0.022f, 0.03f), Vector3.one * 0.026f);
                art.Part(root, "Rear", art.Sphere, art.Matte(Colors.ToyYellow), new Vector3(0, 0.01f, -0.035f), new Vector3(0.036f, 0.014f, 0.03f));
            }
            return root;
        }

        // A full-size building block for the play table.
        public static Transform Block(Art art, Transform parent, int index)
        {
            var root = art.Group(parent, "Block " + (index + 1));
            art.Part(root, "Block", art.Cube, art.Matte(BlockColors[index % BlockColors.Length]), Vector3.zero, Vector3.one * BlockSize);
            art.Part(root, "Dot", art.Cylinder, art.Matte(Color.white), new Vector3(0, BlockSize / 2, 0), new Vector3(BlockSize * 0.4f, 0.001f, BlockSize * 0.4f), null, false);
            return root;
        }

        static Transform Produce(Art art, Transform parent, Core.Ware p, string name = null)
        {
            var root = art.Group(parent, name ?? WareInfo.Name(p));
            switch (p) // procedural fallback when a model is missing
            {
                case Core.Ware.Apple:
                    art.Part(root, "Fruit", art.Sphere, art.Matte(Colors.Apple), Vector3.zero, new Vector3(0.09f, 0.082f, 0.09f));
                    art.Part(root, "Stem", art.Cylinder, art.Matte(Colors.Stem), new Vector3(0, 0.047f, 0), new Vector3(0.008f, 0.012f, 0.008f), Quaternion.Euler(0, 0, 12));
                    art.Part(root, "Leaf", art.Sphere, art.Matte(Colors.Leaf), new Vector3(0.016f, 0.052f, 0), new Vector3(0.03f, 0.006f, 0.016f), Quaternion.Euler(0, 20, 25));
                    break;
                case Core.Ware.Pear or Core.Ware.Orange or Core.Ware.Strawberry or Core.Ware.Grapes or Core.Ware.Watermelon:
                    // fallback without the asset: a ball in the ware's main colour
                    art.Part(root, "Fruit", art.Sphere, art.Matte(ModelColours(p)[0]), Vector3.zero, Vector3.one * Radius(p) * 1.6f);
                    break;
                case Core.Ware.Banana:
                    // fallback without the asset. Mesh spans x ≈ -0.39..0.39 (scale 1), arc bulging down; centre it on the root
                    art.Part(root, "Fruit", art.Banana, art.Matte(Colors.Banana), new Vector3(0, -0.008f, 0), new Vector3(0.24f, 0.24f, 0.24f));
                    art.Part(root, "Tip", art.Sphere, art.Matte(Colors.Stem), new Vector3(-0.093f, 0.034f, 0), Vector3.one * 0.014f);
                    art.Part(root, "Stalk", art.Cylinder, art.Matte(Colors.Stem), new Vector3(0.097f, 0.04f, 0), new Vector3(0.012f, 0.012f, 0.012f), Quaternion.Euler(0, 0, -50));
                    break;
                default:
                    // lying along x: tip at +x, leaves at -x
                    art.Part(root, "Root", art.Cone, art.Matte(Colors.Carrot), new Vector3(-0.06f, 0, 0), new Vector3(0.05f, 0.15f, 0.05f), Quaternion.Euler(0, 0, -90));
                    for (var i = -1; i <= 1; i++)
                        art.Part(root, "Leaf", art.Cylinder, art.Matte(Colors.Leaf), new Vector3(-0.085f, i * 0.006f, 0), new Vector3(0.007f, 0.025f, 0.007f), Quaternion.Euler(0, 0, 90 + i * 28));
                    break;
            }
            return root;
        }

        // A Blender model (art/*/make_*.py): already in metres, pivot at its centre, one colour per submesh.
        public static Transform ModelPart(Art art, Transform parent, string name, Mesh model, Color[] colours)
        {
            var materials = new Material[colours.Length];
            for (var i = 0; i < colours.Length; i++) materials[i] = art.Matte(colours[i]);
            return art.Part(parent, name, model, materials, Vector3.zero, Vector3.one);
        }

        // The colours of a Blender model's submeshes, in the order of its material slots (art/*/make_*.py).
        public static Color[] ModelColours(Core.Ware w) => w switch
        {
            Core.Ware.Apple => new[] { Colors.Apple, Colors.Stem, Colors.Leaf },
            Core.Ware.Banana => new[] { Colors.Banana, Colors.Stem, Colors.BananaTip },
            Core.Ware.Carrot => new[] { Colors.Carrot, Colors.Leaf },
            Core.Ware.Pear => new[] { Colors.Pear, Colors.Stem, Colors.Leaf },
            Core.Ware.Orange => new[] { Colors.Orange, Colors.Leaf },
            Core.Ware.Strawberry => new[] { Colors.Strawberry, Colors.Leaf, Colors.StrawberrySeed },
            Core.Ware.Grapes => new[] { Colors.Grape, Colors.GrapeStem },
            Core.Ware.Watermelon => new[] { Colors.MelonFlesh, Colors.MelonRind, Colors.MelonRindLight, Colors.Ink },
            Core.Ware.Lollipop => new[] { Colors.CandyPink, Colors.CandyPinkLight, Colors.StickWhite },
            Core.Ware.Polkagris => new[] { Colors.StickWhite, Colors.CaneRed },
            Core.Ware.CandyBag => new[] { Colors.BagBlue, Colors.CandyPink, Colors.BagDot },
            Core.Ware.GummyBear => new[] { Colors.Gummy, Colors.Ink },
            Core.Ware.Chocolate => new[] { Colors.Chocolate, Colors.Wrapper, Colors.Foil },
            Core.Ware.WrappedCandy => new[] { Colors.Toffee, Colors.CandyPaper },
            Core.Ware.RainbowStrip => Colors.Rainbow,
            Core.Ware.CandyRing => new[] { Colors.RingBand, Colors.CandyPink },
            Core.Ware.Ball => new[] { Colors.ToyRed, Colors.ToyYellow, Colors.ToyBlue, Colors.StickWhite },
            Core.Ware.Car => new[] { Colors.ToyBlue, Colors.ToyWindow, Colors.Ink, Colors.StickWhite, Colors.ToyYellow },
            Core.Ware.Blocks => BlockColors,
            Core.Ware.Top => new[] { Colors.ToyRed, Colors.ToyYellow, Colors.ToyBlue, Colors.ToyWood },
            Core.Ware.Train => new[] { Colors.ToyRed, Colors.ToyYellow, Colors.Ink, Colors.ToyGreen },
            Core.Ware.StackingRings => new[] { Colors.ToyWood, Colors.ToyRed, Colors.ToyYellow, Colors.ToyBlue },
            Core.Ware.Frogs => new[] { Colors.FrogGreen, Colors.FrogBelly, Colors.StickWhite, Colors.Ink, Colors.ToyYellow, Colors.FrogOrange, Colors.LilyPad },
            _ => new[] { Color.white },
        };

        // The coin token: one big gold coin with a star on top. It stands for the whole payment.
        public static Transform Coin(Art art, Transform parent, string name = "Myntbricka")
        {
            var root = art.Group(parent, name);
            art.Part(root, "Coin", art.Cylinder, art.Matte(Colors.Gold), Vector3.zero, new Vector3(0.085f, 0.007f, 0.085f));
            art.Part(root, "Rim", art.Cylinder, art.Matte(Colors.GoldLight), new Vector3(0, 0.0045f, 0), new Vector3(0.066f, 0.0015f, 0.066f));
            art.Part(root, "Star", art.Star, art.Matte(Colors.Gold), new Vector3(0, 0.0065f, 0), Vector3.one * 0.05f, Quaternion.Euler(90, 0, 0));
            return root;
        }

        // A flat, unlit coin picture facing -z (signs, price tags, wallet): always bright gold, whatever the light.
        public static Transform CoinIcon(Art art, Transform parent, Vector3 position, float diameter, bool faded = false, string name = "Coin Icon")
        {
            var root = art.Group(parent, name, position);
            var face = faded ? new Color(0.9f, 0.88f, 0.84f) : Colors.Gold;
            var rim = faded ? new Color(0.78f, 0.76f, 0.72f) : new Color(0.85f, 0.6f, 0.05f);
            var q = Quaternion.Euler(90, 0, 0);
            art.Part(root, "Rim", art.Cylinder, art.Unlit(rim), new Vector3(0, 0, 0.002f), new Vector3(diameter, 0.001f, diameter), q, false);
            art.Part(root, "Face", art.Cylinder, art.Unlit(face), Vector3.zero, new Vector3(diameter * 0.82f, 0.001f, diameter * 0.82f), q, false);
            art.Part(root, "Star", art.Star, art.Unlit(faded ? rim : Colors.GoldLight), new Vector3(0, 0, -0.0015f), Vector3.one * diameter * 0.55f, null, false);
            return root;
        }

        // A pink heart facing -z (sign, happy customers).
        public static Transform Heart(Art art, Transform parent)
        {
            var h = art.Group(parent, "Heart");
            var pink = art.Unlit(Colors.CandyPink);
            art.Part(h, "Left", art.Sphere, pink, new Vector3(-0.009f, 0.006f, 0), Vector3.one * 0.022f, null, false);
            art.Part(h, "Right", art.Sphere, pink, new Vector3(0.009f, 0.006f, 0), Vector3.one * 0.022f, null, false);
            art.Part(h, "Point", art.Cube, pink, new Vector3(0, -0.004f, 0), new Vector3(0.019f, 0.019f, 0.012f), Quaternion.Euler(0, 0, 45), false);
            return h;
        }

        // Small upright star (sales counter, celebration).
        public static Transform Star(Art art, Transform parent, Vector3 position, float size)
        {
            return art.Part(parent, "Star", art.Star, art.Unlit(Colors.Star), position, Vector3.one * size, null, false);
        }
    }
}
