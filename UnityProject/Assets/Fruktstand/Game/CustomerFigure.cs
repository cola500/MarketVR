// A friendly customer figure made of primitives, facing -z (the player). Shared by the fruit customers and the
// sad customers at the candy stand. Mood switches the face: Neutral (smile), Happy (big smile), Sad (gentle frown
// and one tear; never anything scarier).
using UnityEngine;

namespace Fruktstand.Game
{
    public enum Mood { Neutral, Happy, Sad }

    public sealed class CustomerFigure
    {
        static readonly Color[] Shirts = { new(0.25f, 0.45f, 0.9f), new(0.6f, 0.35f, 0.8f), new(0.2f, 0.65f, 0.55f), new(0.95f, 0.45f, 0.6f), new(0.95f, 0.65f, 0.25f) };
        static readonly Color[] Skins = { new(0.98f, 0.82f, 0.7f), new(0.8f, 0.6f, 0.45f), new(0.55f, 0.38f, 0.26f) };
        static readonly Color[] Hats = { new(0.95f, 0.8f, 0.2f), new(0.3f, 0.3f, 0.35f), new(0.9f, 0.3f, 0.2f) };
        static readonly Color Tear = new(0.55f, 0.8f, 1f);

        readonly Art art;
        readonly Transform smile, bigSmile, frown, tear;

        public Transform Body { get; }
        public Transform WaveArm { get; }
        public Transform Legs { get; } // EPIC-015: bent forward when sitting on the bench
        public Transform LeftArm { get; }
        public Mood Mood { get; private set; }

        // variant picks colours and hat; the same variant always looks the same.
        public CustomerFigure(Art art, Transform parent, int variant)
        {
            this.art = art;
            var v = Mathf.Max(0, variant - 1);
            var shirt = Shirts[v % Shirts.Length];
            var skin = Skins[v % Skins.Length];
            Body = art.Group(parent, "Body");
            Legs = art.Part(Body, "Legs", art.Capsule, art.Matte(new Color(0.25f, 0.27f, 0.35f)), new Vector3(0, 0.25f, 0), new Vector3(0.2f, 0.25f, 0.16f));
            art.Part(Body, "Torso", art.Capsule, art.Matte(shirt), new Vector3(0, 0.6f, 0), new Vector3(0.3f, 0.24f, 0.22f));
            art.Part(Body, "Head", art.Sphere, art.Matte(skin), new Vector3(0, 0.96f, 0), Vector3.one * 0.26f);
            foreach (var x in new[] { -0.05f, 0.05f })
            {
                art.Part(Body, "Eye", art.Sphere, art.Matte(Color.white), new Vector3(x, 1.0f, -0.112f), new Vector3(0.05f, 0.055f, 0.02f), null, false);
                art.Part(Body, "Pupil", art.Sphere, art.Unlit(Colors.Ink), new Vector3(x, 0.998f, -0.123f), new Vector3(0.026f, 0.03f, 0.01f), null, false);
                art.Part(Body, "Cheek", art.Sphere, art.Matte(new Color(1f, 0.6f, 0.6f)), new Vector3(x * 1.7f, 0.93f, -0.105f), new Vector3(0.04f, 0.025f, 0.01f), null, false);
            }
            smile = Mouth("Smile", 0.035f, 0.012f);
            bigSmile = Mouth("Big Smile", 0.05f, 0.03f);
            frown = Mouth("Frown", 0.03f, -0.012f);
            tear = art.Part(Body, "Tear", art.Sphere, art.Unlit(Tear), new Vector3(0.06f, 0.955f, -0.122f), new Vector3(0.014f, 0.024f, 0.008f), null, false);
            var armL = art.Group(Body, "Arm L", new Vector3(-0.16f, 0.72f, 0));
            art.Part(armL, "Sleeve", art.Capsule, art.Matte(shirt), new Vector3(0, -0.12f, 0), new Vector3(0.08f, 0.13f, 0.08f));
            art.Part(armL, "Hand", art.Sphere, art.Matte(skin), new Vector3(0, -0.26f, 0), Vector3.one * 0.07f);
            armL.localRotation = Quaternion.Euler(0, 0, -10);
            LeftArm = armL;
            WaveArm = art.Group(Body, "Arm R", new Vector3(0.16f, 0.72f, 0));
            art.Part(WaveArm, "Sleeve", art.Capsule, art.Matte(shirt), new Vector3(0, -0.12f, 0), new Vector3(0.08f, 0.13f, 0.08f));
            art.Part(WaveArm, "Hand", art.Sphere, art.Matte(skin), new Vector3(0, -0.26f, 0), Vector3.one * 0.07f);
            if (variant % 2 == 0)
            {
                var hat = Hats[(variant / 2 - 1) % Hats.Length];
                art.Part(Body, "Brim", art.Cylinder, art.Matte(hat), new Vector3(0, 1.065f, 0), new Vector3(0.32f, 0.006f, 0.32f));
                art.Part(Body, "Crown", art.Cylinder, art.Matte(hat), new Vector3(0, 1.11f, 0), new Vector3(0.2f, 0.045f, 0.2f));
            }
            SetMood(Mood.Neutral);
        }

        // depth > 0 curves down in the middle (smile), < 0 curves up (frown).
        Transform Mouth(string name, float width, float depth)
        {
            var g = art.Group(Body, name, new Vector3(0, depth < 0 ? 0.915f : 0.925f, -0.118f));
            for (var i = 0; i <= 6; i++)
            {
                var t = i / 6f;
                var x = Mathf.Lerp(-width, width, t);
                var y = -Mathf.Sin(Mathf.PI * t) * depth;
                art.Part(g, "Dot", art.Sphere, art.Unlit(Colors.Ink), new Vector3(x, y, 0), new Vector3(0.012f, 0.012f, 0.006f), null, false);
            }
            return g;
        }

        public void SetMood(Mood mood)
        {
            Mood = mood;
            smile.gameObject.SetActive(mood == Mood.Neutral);
            bigSmile.gameObject.SetActive(mood == Mood.Happy);
            frown.gameObject.SetActive(mood == Mood.Sad);
            tear.gameObject.SetActive(mood == Mood.Sad);
        }

        public void Wave(float degrees) => WaveArm.localRotation = degrees != 0 ? Quaternion.Euler(0, 0, 150 + degrees) : Quaternion.Euler(0, 0, 10);
    }
}
