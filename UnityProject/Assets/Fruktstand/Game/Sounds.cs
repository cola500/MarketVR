// Sounds synthesised in code (no audio files): a soft pop when something lands in the basket, a two-note pling
// when a sale is registered, a tiny tick on pick-up. Sound only complements what is shown; nothing needs it.
using UnityEngine;

namespace Fruktstand.Game
{
    public static class SoundSynth
    {
        public const int Rate = 44100;

        public static float[] Pop() => Render(0.09f, (t, d) => Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(520, 900, t / d) * t) * Envelope(t, 0.004f, 0.06f) * 0.5f);

        // EPIC-008: a short, soft fanfare (C5 E5 G5, then C6 held), bell-like and quiet; never a bang.
        public static float[] Fanfare() => Render(1.6f, (t, d) =>
            (Bell(t, 523.3f) + Bell(t - 0.18f, 659.3f) + Bell(t - 0.36f, 784f) + Bell(t - 0.6f, 1046.5f) * 1.2f) * 0.22f);

        // A soft, low wooden "klack" for the crocodile's snap: two quick knocks, quiet, never startling.
        public static float[] Clack() => Render(0.12f, (t, d) =>
            (Mathf.Sin(2 * Mathf.PI * 330 * t) * Envelope(t, 0.002f, 0.02f) + Mathf.Sin(2 * Mathf.PI * 440 * (t - 0.05f)) * (t < 0.05f ? 0 : Envelope(t - 0.05f, 0.002f, 0.02f))) * 0.3f);

        // EPIC-014: six short, soft bird calls (a few syllables of gliding whistle with a quiet overtone). Every syllable
        // swells in and fades out (a sine-shaped envelope): no clicks, peaks at most 0.45.
        public static float[][] Birds() => new[]
        {
            Call((0.00f, 0.07f, 2800, 4200), (0.16f, 0.07f, 2900, 4300)),                 // tvitt-tvitt
            Call(Trill(7, 0.035f, 0.045f, 3600, 3900)),                                   // a little trill
            Call((0.00f, 0.35f, 3200, 2200)),                                            // a falling whistle
            Call((0.00f, 0.09f, 2400, 3000), (0.13f, 0.09f, 2400, 3000), (0.26f, 0.09f, 2500, 3100)), // tju-tju-tju
            Call((0.00f, 0.08f, 4000, 4100), (0.12f, 0.16f, 3000, 2800)),                // ti-tuu
            Call((0.00f, 0.05f, 3300, 3700), (0.08f, 0.05f, 3500, 3900), (0.2f, 0.12f, 3800, 3000)), // tri-li-lu
        };

        static (float start, float length, float from, float to)[] Trill(int notes, float length, float step, float lo, float hi)
        {
            var s = new (float, float, float, float)[notes];
            for (var i = 0; i < notes; i++) s[i] = (i * step, length, i % 2 == 0 ? lo : hi, i % 2 == 0 ? hi : lo);
            return s;
        }

        static float[] Call(params (float start, float length, float from, float to)[] syllables)
        {
            var end = 0f;
            foreach (var s in syllables) end = Mathf.Max(end, s.start + s.length);
            var n = Mathf.CeilToInt((end + 0.03f) * Rate);
            var data = new float[n];
            foreach (var (start, length, from, to) in syllables)
            {
                var phase = 0.0;
                var first = Mathf.RoundToInt(start * Rate);
                var count = Mathf.RoundToInt(length * Rate);
                for (var i = 0; i < count && first + i < n; i++)
                {
                    var k = i / (float)count;
                    var f = Mathf.Lerp(from, to, k * k * (3 - 2 * k));
                    phase += 2 * System.Math.PI * f / Rate;
                    var env = Mathf.Sin(Mathf.PI * k); // swells and fades: no click at either end
                    data[first + i] += (float)(System.Math.Sin(phase) + 0.18 * System.Math.Sin(2 * phase)) * Mathf.Max(0, env) * 0.38f;
                }
            }
            for (var i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -0.45f, 0.45f);
            return data;
        }

        // EPIC-018: a short, friendly quack: a nasal tone gliding down, twice, quickly.
        public static float[] Quack() => Render(0.22f, (t, d) =>
        {
            var k = t < 0.1f ? t : t - 0.11f;
            if (k < 0) return 0;
            var f = Mathf.Lerp(620, 430, k / 0.1f);
            var tone = Mathf.Sin(2 * Mathf.PI * f * t) + 0.6f * Mathf.Sin(4 * Mathf.PI * f * t) + 0.35f * Mathf.Sin(6 * Mathf.PI * f * t);
            return tone * Envelope(k, 0.008f, 0.07f) * 0.22f;
        });

        public static float[] Tick() => Render(0.04f, (t, d) => Mathf.Sin(2 * Mathf.PI * 1400 * t) * Envelope(t, 0.002f, 0.015f) * 0.25f);

        // EPIC-016: "a customer is here": a soft rising G5 -> C6, shorter and quieter than the sale's pling, faded out to
        // silence at the end (no click).
        public static float[] Hello() => Render(0.6f, (t, d) =>
            (Bell(t, 784f) * 0.5f + Bell(t - 0.11f, 1046.5f) * 0.55f) * Mathf.Clamp01((d - t) / 0.08f));

        // E6 then A6, bell-like (fundamental + a quiet octave), each with an exponential decay.
        public static float[] Pling() => Render(0.9f, (t, d) => Bell(t, 1318.5f) * 0.45f + Bell(t - 0.12f, 1760f) * 0.5f);

        static float Bell(float t, float f) =>
            t < 0 ? 0 : (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * f * 2 * t)) * Envelope(t, 0.003f, 0.28f) * 0.8f;

        static float Envelope(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        static float[] Render(float seconds, System.Func<float, float, float> f)
        {
            var n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (var i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, seconds), -1, 1);
            return data;
        }

        public static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    public sealed class Sounds
    {
        readonly AudioSource source;
        readonly AudioSource arrival; // EPIC-016: fully 3D, moved to the stall where the customer waits
        readonly AudioClip pop, pling, tick, clack, fanfare, hello, quack;

        public Sounds(Transform at)
        {
            source = at.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.6f;
            // The listener sits on the camera, so the direction follows the headset's position and turn (Unity's own 3D
            // panning: the project has no spatializer plugin).
            var go = new GameObject("Arrival Sound");
            go.transform.SetParent(at, false);
            arrival = go.AddComponent<AudioSource>();
            arrival.playOnAwake = false;
            arrival.spatialBlend = 1;
            arrival.rolloffMode = AudioRolloffMode.Linear;
            arrival.minDistance = 1.5f;
            arrival.maxDistance = 25;
            arrival.dopplerLevel = 0;
            hello = SoundSynth.Clip("Hello", SoundSynth.Hello());
            pop = SoundSynth.Clip("Pop", SoundSynth.Pop());
            pling = SoundSynth.Clip("Pling", SoundSynth.Pling());
            tick = SoundSynth.Clip("Tick", SoundSynth.Tick());
            clack = SoundSynth.Clip("Clack", SoundSynth.Clack());
            fanfare = SoundSynth.Clip("Fanfare", SoundSynth.Fanfare());
            quack = SoundSynth.Clip("Quack", SoundSynth.Quack());
        }

        public const float PopVolume = 0.9f, PlingVolume = 1f, HelloVolume = 0.7f;
        public int Played { get; private set; } // for tests
        public int Arrivals { get; private set; }
        public Transform ArrivalSource => arrival.transform;
        public float ArrivalSpatialBlend => arrival.spatialBlend;

        // EPIC-016: the customer is ready to order; played once per customer (Core/ArrivalChime decides) at the stall.
        public void Arrival(Vector3 world)
        {
            arrival.transform.position = world;
            arrival.PlayOneShot(hello, HelloVolume);
            Arrivals++;
            Played++;
        }

        public void Pop() => Play(pop, PopVolume);
        public void Pling() => Play(pling, PlingVolume);
        public void Tick() => Play(tick, 0.6f);
        public void Clack() => Play(clack, 0.5f);
        public void Fanfare() => Play(fanfare, 0.45f);
        public void Quack() => Play(quack, 0.55f); // EPIC-018: a frog on the lily pad

        // EPIC-008: turned down during the rest.
        public float Volume
        {
            get => source.volume;
            set => source.volume = arrival.volume = Mathf.Clamp01(value);
        }

        void Play(AudioClip clip, float volume)
        {
            Played++;
            source.PlayOneShot(clip, volume);
        }
    }
}
