// The crocodile toy on its own shelf at the play table (EPIC-006): only shows Core.CrocodileGame. Built from
// primitives; it lies along its own +x (the snout), the mouth opens upwards and the eight teeth on the lower jaw
// are pressed down from above like any button. Nothing here has colliders or physics: the jaw is purely visual and
// can never catch, move or push a hand or a toy.
using System;
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class CrocodileView
    {
        public const float OpenDegrees = 40f;
        const float ToothRadius = 0.013f;
        const float ToothSink = 0.014f;
        const float JawTop = 0.035f; // lower jaw's upper surface (croc space)
        static readonly float[] ToothX = { 0.05f, 0.095f, 0.14f, 0.185f }; // along the jaw, 4.5 cm apart
        const float ToothZ = 0.04f;                                          // the two rows, 8 cm apart
        static readonly Vector3 Hinge = new(0.0f, 0.05f, 0);
        // The mouth area (croc space): the dance waits while a hand is here.
        static readonly Bounds MouthArea = new(new Vector3(0.11f, 0.12f, 0), new Vector3(0.28f, 0.28f, 0.2f));

        static readonly Color Green = new(0.35f, 0.72f, 0.3f), DarkGreen = new(0.22f, 0.52f, 0.2f), Mouth = new(0.95f, 0.55f, 0.6f);

        readonly Transform upperJaw;
        readonly Item[] teeth = new Item[CrocodileGame.Teeth];
        readonly Vector3 bodyBase;

        public CrocodileGame Game { get; }
        public Transform Root { get; }   // fixed on the shelf
        public Transform Body { get; }   // what dances; always back at its base pose otherwise
        public float MouthOpen { get; private set; } = 1;

        // Tooth centre in croc space (0..3 the near row, 4..7 the far row, front to back).
        public static Vector3 ToothLocal(int i) => new(ToothX[i % 4], JawTop + 0.012f, i < 4 ? -ToothZ : ToothZ);

        public CrocodileView(Art art, Transform tableTop, List<Item> items, Func<int, int> random)
        {
            Game = new CrocodileGame(random);
            Root = art.Group(tableTop, "Crocodile", PlayTableView.CrocOrigin);
            Root.localRotation = Quaternion.Euler(0, PlayTableView.CrocYaw, 0);
            Body = art.Group(Root, "Body");
            bodyBase = Body.localPosition;

            // Tail, body and legs.
            art.Part(Body, "Belly", art.Capsule, art.Matte(Green), new Vector3(-0.08f, 0.035f, 0), new Vector3(0.09f, 0.1f, 0.1f), Quaternion.Euler(0, 0, 90));
            art.Part(Body, "Tail", art.Cone, art.Matte(Green), new Vector3(-0.16f, 0.03f, 0), new Vector3(0.06f, 0.1f, 0.05f), Quaternion.Euler(0, 0, 90));
            for (var i = 0; i < 6; i++)
                art.Part(Body, "Bump", art.Sphere, art.Matte(DarkGreen), new Vector3(-0.14f + i * 0.03f, 0.075f, 0), Vector3.one * 0.022f, null, false);
            foreach (var x in new[] { -0.12f, -0.03f })
                foreach (var z in new[] { -0.05f, 0.05f })
                    art.Part(Body, "Leg", art.Sphere, art.Matte(DarkGreen), new Vector3(x, 0.012f, z), new Vector3(0.035f, 0.024f, 0.03f));

            // Lower jaw with a pink mouth and the eight teeth.
            art.Part(Body, "Lower Jaw", art.Cube, art.Matte(Green), new Vector3(0.1f, 0.018f, 0), new Vector3(0.21f, 0.034f, 0.12f));
            art.Part(Body, "Tongue", art.Cube, art.Matte(Mouth), new Vector3(0.1f, JawTop + 0.001f, 0), new Vector3(0.18f, 0.002f, 0.06f), null, false);
            for (var i = 0; i < CrocodileGame.Teeth; i++)
            {
                var tooth = art.Group(Body, $"Tooth {i}", ToothLocal(i));
                art.Part(tooth, "Enamel", art.Cylinder, art.Matte(Color.white), Vector3.zero, new Vector3(ToothRadius * 2, 0.012f, ToothRadius * 2), null, false);
                art.Part(tooth, "Tip", art.Sphere, art.Matte(Color.white), Vector3.up * 0.012f, Vector3.one * ToothRadius * 2, null, false);
                teeth[i] = Item.ForButton(tooth, ToothRadius, null);
                items.Add(teeth[i]);
            }

            // Upper jaw on a hinge behind the mouth, with friendly eyes and nostrils.
            upperJaw = art.Group(Body, "Upper Jaw", Hinge);
            art.Part(upperJaw, "Snout", art.Cube, art.Matte(Green), new Vector3(0.1f, 0.012f, 0), new Vector3(0.21f, 0.026f, 0.115f));
            art.Part(upperJaw, "Palate", art.Cube, art.Matte(Mouth), new Vector3(0.1f, -0.002f, 0), new Vector3(0.18f, 0.002f, 0.07f), null, false);
            foreach (var z in new[] { -0.035f, 0.035f })
            {
                art.Part(upperJaw, "Eye", art.Sphere, art.Matte(Color.white), new Vector3(0.0f, 0.035f, z), Vector3.one * 0.032f, null, false);
                art.Part(upperJaw, "Pupil", art.Sphere, art.Unlit(Colors.Ink), new Vector3(0.012f, 0.042f, z), Vector3.one * 0.014f, null, false);
                art.Part(upperJaw, "Nostril", art.Sphere, art.Unlit(DarkGreen), new Vector3(0.195f, 0.027f, z * 0.4f), Vector3.one * 0.01f, null, false);
            }
            Present(0, 0);
        }

        public Item ToothItem(int i) => teeth[i];
        public Vector3 ToothWorld(int i) => Body.TransformPoint(ToothLocal(i));

        public int ToothOf(Item item)
        {
            for (var i = 0; i < teeth.Length; i++) if (teeth[i] == item) return i;
            return -1;
        }

        // Bit t set when the point is within tooth t's press area (the same reach as grabbing a button).
        public int OnTeeth(Vector3 world)
        {
            var mask = 0;
            for (var i = 0; i < teeth.Length; i++)
                if (Vector3.Distance(world, Root.TransformPoint(ToothLocal(i))) - ToothRadius <= Grabber.Reach) mask |= 1 << i;
            return mask;
        }

        public bool InMouth(Vector3 world) => MouthArea.Contains(Root.InverseTransformPoint(world));

        // Brings the jaw, teeth and dance in line with the rules.
        public void Present(float dt, float time)
        {
            var g = Game;
            MouthOpen = g.State switch
            {
                CrocState.Open => 1,
                CrocState.Closing => 1 - Mathf.Clamp01(g.StateTime / CrocodileGame.CloseSeconds),
                CrocState.Opening => Ease(Mathf.Clamp01(g.StateTime / CrocodileGame.OpenSeconds)),
                _ => 0,
            };
            upperJaw.localRotation = Quaternion.Euler(0, 0, OpenDegrees * MouthOpen);
            for (var i = 0; i < teeth.Length; i++)
            {
                var t = teeth[i].Transform;
                t.localPosition = ToothLocal(i) + (g.IsPressed(i) ? Vector3.down * ToothSink : Vector3.zero);
            }
            if (g.State == CrocState.Dancing)
            {
                // A silly little dance on the spot: wiggle, rock and hop.
                var s = g.StateTime;
                var fade = Mathf.Clamp01((CrocodileGame.DanceSeconds - s) / 0.3f); // settles smoothly at the end
                Body.localPosition = bodyBase + Vector3.up * (Mathf.Abs(Mathf.Sin(s * 9)) * 0.035f * fade);
                Body.localRotation = Quaternion.Euler(0, Mathf.Sin(s * 7) * 22 * fade, Mathf.Sin(s * 11) * 9 * fade);
                upperJaw.localRotation = Quaternion.Euler(0, 0, (0.5f + 0.5f * Mathf.Sin(s * 14)) * 18 * fade);
            }
            else
            {
                Body.localPosition = bodyBase; // exactly its place whenever it isn't dancing
                Body.localRotation = Quaternion.identity;
            }
        }

        static float Ease(float x) => x * x * (3 - 2 * x);
    }
}
