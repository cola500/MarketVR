// EPIC-008: how the market party and the rest look. Only shows Core.MarketRound; no rules here.
// - The sign over the fruit stall: "Marknadsfest!" with stars during the party, "Pausdags!" with a headset during the
//   rest (hidden otherwise, so the category picture shows).
// - The party board (festtavlan) on the fruit stall: one gold star per party this session.
// - A panel in front of the player (placed once when it appears, then still): the rest ("Ta av headsetet och vila en
//   stund.") or, while the party waits for empty hands, an open hand with "Släpp det du håller".
// - Effects during the party: soft fireworks flowers high above the stalls and slowly falling confetti. Few objects,
//   unlit pastel colours that shrink away (no flashing), always high above the market (2.45 m+) and away from the face.
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class PartyView
    {
        public const int MaxEffects = 120;
        public const int BoardSlots = 8;
        const float PanelDistance = 1.1f;
        const float BurstEvery = 1.2f, BurstLife = 2.4f, PetalSpeed = 0.3f;
        const int Petals = 12, Confetti = 30;
        const float ConfettiFloor = 2.45f; // above the market's floor; removed below this (0.85 m over a standing child's eyes)

        static readonly Color[] Pastels =
        {
            new(1f, 0.6f, 0.7f), new(1f, 0.85f, 0.45f), new(0.6f, 0.85f, 1f), new(0.7f, 0.95f, 0.6f), new(0.85f, 0.7f, 1f),
        };
        static readonly Color Gold = new(1f, 0.8f, 0.2f), Socket = new(0.75f, 0.72f, 0.65f), Board = new(0.98f, 0.96f, 0.9f);

        readonly Art art;
        readonly Transform marketRoot;
        readonly Transform sign, signStars, signHeadset, panelRoot, restPanel, hintPanel, effectsRoot;
        readonly TextMesh signText, restText, restHow, restTitle, starCount;
        readonly Transform[] boardStars = new Transform[BoardSlots];
        readonly List<(Transform t, Vector3 v, float age, float life, Vector3 scale)> effects = new();
        float burstTimer;
        int burstIndex;
        bool panelPlaced;
        RoundState shown = RoundState.Playing;

        public string SignText => sign.gameObject.activeSelf ? signText.text : "";
        public int StarsShown { get; private set; }
        public int EffectCount => effects.Count;
        public IEnumerable<Transform> Effects { get { foreach (var e in effects) yield return e.t; } }
        public bool ShowingRestPanel => restPanel.gameObject.activeInHierarchy;
        public bool ShowingReleaseHint => hintPanel.gameObject.activeInHierarchy;
        public string RestText => restText.text;
        public string RestHowText => restHow.text;   // EPIC-012
        public string RestTitleText => restTitle.text;

        public PartyView(Art art, Transform fruitStallTop, Transform marketRoot, Transform world)
        {
            this.art = art;
            this.marketRoot = marketRoot;

            // The sign, in front of the fruit stall's picture sign.
            // In front of the picture sign (which hangs at z -0.05 and would show through), wide enough for the text.
            sign = art.Group(fruitStallTop, "Party Sign", new Vector3(0, StandView.PostHeight + 0.22f, -0.22f));
            art.Part(sign, "Board", art.Cube, art.Unlit(Board), Vector3.zero, new Vector3(0.86f, 0.22f, 0.02f), null, false);
            signText = art.Text(sign, "Text", "", new Vector3(0, 0.01f, -0.012f), 0.0095f, Colors.Ink);
            signStars = art.Group(sign, "Stars");
            foreach (var x in new[] { -0.36f, 0.36f })
                art.Part(signStars, "Star", art.Star, art.Unlit(Gold), new Vector3(x, 0.02f, -0.012f), Vector3.one * 0.12f, null, false);
            signHeadset = Headset(sign, new Vector3(0.3f, -0.01f, -0.015f), 0.5f);
            sign.gameObject.SetActive(false);

            // The party board: sockets, filled with gold stars as parties happen.
            var board = art.Group(fruitStallTop, "Party Board", new Vector3(-0.3f, StandView.PostHeight - 0.1f, -0.13f));
            art.Part(board, "Board", art.Cube, art.Unlit(Board), Vector3.zero, new Vector3(0.27f, 0.06f, 0.01f), null, false);
            for (var i = 0; i < BoardSlots; i++)
            {
                var x = -0.105f + i * 0.03f;
                art.Part(board, "Socket", art.Cylinder, art.Unlit(Socket), new Vector3(x, 0, -0.006f), new Vector3(0.022f, 0.001f, 0.022f), Quaternion.Euler(90, 0, 0), false);
                boardStars[i] = art.Part(board, "Star", art.Star, art.Unlit(Gold), new Vector3(x, 0, -0.008f), Vector3.one * 0.03f, null, false);
                boardStars[i].gameObject.SetActive(false);
            }
            starCount = art.Text(board, "More", "", new Vector3(0.15f, 0, -0.008f), 0.003f, Colors.Ink, TextAnchor.MiddleLeft);

            // Panels in front of the player.
            panelRoot = art.Group(world, "Party Panels");
            restPanel = art.Group(panelRoot, "Rest Panel");
            art.Part(restPanel, "Board", art.Cube, art.Unlit(Board), new Vector3(0, -0.025f, 0), new Vector3(0.72f, 0.47f, 0.01f), null, false);
            restTitle = art.Text(restPanel, "Title", "Pausdags!", new Vector3(0, 0.155f, -0.008f), 0.011f, Colors.Ink);
            Headset(restPanel, new Vector3(0, -0.035f, -0.01f), 0.9f); // its "off" arrow stays clear of the title
            restText = art.Text(restPanel, "Text", "Ta av headsetet och vila en stund.", new Vector3(0, -0.15f, -0.008f), 0.0055f, Colors.Ink);
            // EPIC-012: what ends this rest, so nobody has to guess (or start a new game).
            restHow = art.Text(restPanel, "How", "", new Vector3(0, -0.205f, -0.008f), 0.0048f, new Color(0.2f, 0.45f, 0.25f));
            restPanel.gameObject.SetActive(false);
            hintPanel = art.Group(panelRoot, "Release Hint");
            art.Part(hintPanel, "Board", art.Cube, art.Unlit(Board), Vector3.zero, new Vector3(0.5f, 0.3f, 0.01f), null, false);
            OpenHand(hintPanel, new Vector3(0, 0.03f, -0.01f));
            art.Text(hintPanel, "Text", "Släpp det du håller", new Vector3(0, -0.11f, -0.008f), 0.006f, Colors.Ink);
            hintPanel.gameObject.SetActive(false);

            effectsRoot = art.Group(world, "Party Effects");
        }

        // A taken-off headset: a rounded box with two lenses, a strap and a little "off" arrow above it.
        Transform Headset(Transform parent, Vector3 at, float scale)
        {
            var g = art.Group(parent, "Headset", at);
            g.localScale = Vector3.one * scale;
            art.Part(g, "Body", art.Cube, art.Unlit(new Color(0.92f, 0.92f, 0.95f)), Vector3.zero, new Vector3(0.18f, 0.08f, 0.02f), null, false);
            foreach (var x in new[] { -0.045f, 0.045f })
                art.Part(g, "Lens", art.Cylinder, art.Unlit(new Color(0.25f, 0.27f, 0.32f)), new Vector3(x, 0, -0.012f), new Vector3(0.05f, 0.002f, 0.05f), Quaternion.Euler(90, 0, 0), false);
            art.Part(g, "Strap", art.Cube, art.Unlit(new Color(0.35f, 0.35f, 0.4f)), new Vector3(0, -0.055f, 0.0f), new Vector3(0.22f, 0.012f, 0.012f), null, false);
            art.Part(g, "Arrow", art.Cube, art.Unlit(new Color(0.3f, 0.65f, 0.4f)), new Vector3(0, 0.085f, -0.005f), new Vector3(0.012f, 0.05f, 0.005f), null, false);
            foreach (var s in new[] { -1f, 1f })
                art.Part(g, "Arrow Head", art.Cube, art.Unlit(new Color(0.3f, 0.65f, 0.4f)), new Vector3(s * 0.012f, 0.1f, -0.005f), new Vector3(0.012f, 0.03f, 0.005f), Quaternion.Euler(0, 0, s * 40), false);
            return g;
        }

        // An open hand: a palm with four fingers and a thumb, spread.
        void OpenHand(Transform parent, Vector3 at)
        {
            var g = art.Group(parent, "Open Hand", at);
            var skin = art.Unlit(new Color(0.98f, 0.82f, 0.7f));
            art.Part(g, "Palm", art.Sphere, skin, Vector3.zero, new Vector3(0.07f, 0.08f, 0.01f), null, false);
            for (var i = 0; i < 4; i++)
                art.Part(g, "Finger", art.Capsule, skin, new Vector3(-0.03f + i * 0.02f, 0.07f, 0), new Vector3(0.016f, 0.03f, 0.008f), Quaternion.Euler(0, 0, (i - 1.5f) * -8), false);
            art.Part(g, "Thumb", art.Capsule, skin, new Vector3(0.05f, 0.01f, 0), new Vector3(0.016f, 0.028f, 0.008f), Quaternion.Euler(0, 0, -50), false);
        }

        public void Update(RoundState state, float stateTime, int stars, bool showReleaseHint, Transform head, float dt, float clock, RestCause restBy = RestCause.Party)
        {
            // EPIC-012: why it rests and what goes on with it.
            (restTitle.text, restHow.text) = restBy switch
            {
                RestCause.Child => ("Vilopaus", "Spela igen: öppna menyn och välj Fortsätt spela."),
                RestCause.Parent => ("Paus", "En vuxen fortsätter spelet (mobilen eller För föräldrar)."),
                _ => ("Pausdags!", "En vuxen startar nästa omgång."),
            };
            // Sign.
            var showSign = state is RoundState.Celebrating or RoundState.Resting;
            sign.gameObject.SetActive(showSign);
            if (showSign)
            {
                var party = state == RoundState.Celebrating;
                signText.text = party ? "Marknadsfest!" : "Pausdags!";
                signText.transform.localPosition = new Vector3(party ? 0 : -0.07f, 0.01f, -0.012f);
                signStars.gameObject.SetActive(party);
                signHeadset.gameObject.SetActive(!party);
                if (party) signStars.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock * 2) * 6); // a gentle sway, no blinking
            }

            // Party board.
            StarsShown = stars;
            for (var i = 0; i < BoardSlots; i++) boardStars[i].gameObject.SetActive(i < stars);
            starCount.text = stars > BoardSlots ? $"+{stars - BoardSlots}" : "";

            // Panels in front of the player: placed once when they appear, then left still (comfortable to read).
            var rest = state == RoundState.Resting;
            var hint = !rest && showReleaseHint;
            if ((rest || hint) && !panelPlaced && head != null)
            {
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                forward.Normalize();
                panelRoot.SetPositionAndRotation(head.position + forward * PanelDistance + Vector3.down * 0.15f, Quaternion.LookRotation(forward));
                panelPlaced = true;
            }
            if (!rest && !hint) panelPlaced = false;
            restPanel.gameObject.SetActive(rest);
            hintPanel.gameObject.SetActive(hint);

            // Effects: only during the party; cleared as soon as it ends.
            if (state == RoundState.Celebrating)
            {
                if (shown != RoundState.Celebrating) StartConfetti();
                burstTimer -= dt;
                if (burstTimer <= 0 && effects.Count + Petals <= MaxEffects)
                {
                    Burst();
                    burstTimer = BurstEvery;
                }
            }
            else if (effects.Count > 0) ClearEffects();
            AnimateEffects(dt, clock);
            shown = state;
        }

        // A soft fireworks flower high above one of the stalls (2.9-3.5 m), petals drifting out and shrinking away.
        void Burst()
        {
            var angle = burstIndex++ * 137.5f * Mathf.Deg2Rad;
            var centre = marketRoot.TransformPoint(new Vector3(Mathf.Cos(angle) * 1.0f, 2.9f + (burstIndex % 3) * 0.3f, Mathf.Sin(angle) * 1.0f));
            var colour = Pastels[burstIndex % Pastels.Length];
            for (var i = 0; i < Petals; i++)
            {
                var a = i / (float)Petals * Mathf.PI * 2;
                var v = new Vector3(Mathf.Cos(a), 0.3f * Mathf.Sin(a * 3), Mathf.Sin(a)).normalized * PetalSpeed;
                var t = art.Part(effectsRoot, "Petal", art.Sphere, art.Unlit(colour), centre, Vector3.one * 0.07f, null, false);
                effects.Add((t, v, 0, BurstLife, t.localScale));
            }
        }

        void StartConfetti()
        {
            for (var i = 0; i < Confetti && effects.Count < MaxEffects; i++)
            {
                var a = i * 2.4f;
                var r = 0.4f + (i % 5) * 0.2f;
                var p = marketRoot.TransformPoint(new Vector3(Mathf.Cos(a) * r, 3.1f + (i % 4) * 0.1f, Mathf.Sin(a) * r));
                var t = art.Part(effectsRoot, "Confetti", art.Cube, art.Unlit(Pastels[i % Pastels.Length]), p, new Vector3(0.03f, 0.003f, 0.02f), Quaternion.Euler(i * 37, i * 53, 0), false);
                effects.Add((t, new Vector3(0, -0.15f, 0), 0, 8f, t.localScale));
            }
        }

        void AnimateEffects(float dt, float clock)
        {
            var floor = marketRoot.position.y + ConfettiFloor;
            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var (t, v, age, life, scale) = effects[i];
                age += dt;
                var p = t.position + v * dt;
                if (t.name == "Confetti")
                {
                    p += new Vector3(Mathf.Sin(clock * 2 + i) * 0.05f * dt, 0, Mathf.Cos(clock * 1.7f + i) * 0.05f * dt); // a gentle sway
                    t.Rotate(40 * dt, 25 * dt, 0);
                }
                if (age >= life || p.y < floor)
                {
                    Art.Discard(t);
                    effects.RemoveAt(i);
                    continue;
                }
                t.position = p;
                if (t.name == "Petal") t.localScale = scale * (1 - Mathf.SmoothStep(0, 1, age / life)); // shrink away, never flash
                effects[i] = (t, v, age, life, scale);
            }
        }

        void ClearEffects()
        {
            foreach (var e in effects) Art.Discard(e.t);
            effects.Clear();
        }
    }
}
