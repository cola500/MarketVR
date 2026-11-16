// EPIC-014: plays the park's birds (rules in Core/Ambience): a few 3D sources up in the trees (children of the park, so
// they follow the market) that only ever play short calls. No looping bed (the wind was taken out after the headset test).
// Pauses need nothing extra here: AudioListener.pause (XrRig) silences and resumes every source in place, and
// Ambience holds the birds' clock. Synthesised clips only (SoundSynth), no audio files.
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class NatureSounds
    {
        readonly AudioSource[] birds;
        readonly AudioClip[] calls;

        public NatureSounds(Transform park, IReadOnlyList<Vector3> birdSpots)
        {
            var clips = SoundSynth.Birds();
            calls = new AudioClip[clips.Length];
            for (var i = 0; i < clips.Length; i++) calls[i] = SoundSynth.Clip($"Bird {i}", clips[i]);
            birds = new AudioSource[birdSpots.Count];
            for (var i = 0; i < birds.Length; i++)
            {
                var go = new GameObject($"Bird {i}");
                go.transform.SetParent(park, false);
                go.transform.localPosition = birdSpots[i];
                var b = go.AddComponent<AudioSource>();
                b.playOnAwake = false;
                b.spatialBlend = 1;
                b.rolloffMode = AudioRolloffMode.Linear;
                b.minDistance = 3;
                b.maxDistance = 30;
                b.dopplerLevel = 0;
                b.priority = 190;
                birds[i] = b;
            }
        }

        public int Spots => birds.Length;
        public int Clips => calls.Length;
        public int BirdsSung { get; private set; } // for tests and the log

        public void Update(Ambience ambience, float dt, bool paused)
        {
            if (ambience.Tick(dt, paused) is { } call)
            {
                birds[call.Spot].PlayOneShot(calls[call.Clip], ambience.BirdVolume);
                BirdsSung++;
            }
        }
    }
}
