// EPIC-014 slice 1: the park's nature sound (birds only: the wind was taken out after the headset test, 2026-11-02).
// Birds are sporadic and varied, pauses hold them (no burst afterwards), "Av" is silent, no call is sharp, and they
// stay under the feedback sounds. The parent switches it in the menu. Names match the [T: ...] references in docs/EPIC-014.md.
using System.Collections.Generic;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class AmbienceTests
    {
        const float Frame = 1f / 72;

        static List<(float at, int spot, int clip)> Run(Ambience a, float seconds, System.Func<float, bool> paused = null, float start = 0)
        {
            var calls = new List<(float, int, int)>();
            var frames = Mathf.RoundToInt(seconds / Frame);
            for (var i = 0; i < frames; i++)
            {
                var t = start + i * Frame; // from the frame number: a float sum would drift over 20 minutes
                var call = a.Tick(Frame, paused?.Invoke(t) ?? false);
                if (call is { } c) calls.Add((t, c.Spot, c.Clip));
            }
            return calls;
        }

        [Test]
        public void Ambience_BirdsAreSporadicAndVaried()
        {
            var a = new Ambience(7, spots: 6, clips: 5);
            var calls = Run(a, 1200);
            Assert.That(calls.Count, Is.InRange((int)(1200 / Ambience.MaxGap), (int)(1200 / Ambience.MinGap) + 1));
            Assert.GreaterOrEqual(calls[0].at, Ambience.MinGap - Frame, "not straight away at the start");
            for (var i = 1; i < calls.Count; i++)
            {
                var gap = calls[i].at - calls[i - 1].at;
                Assert.That(gap, Is.InRange(Ambience.MinGap - Frame, Ambience.MaxGap + Frame), $"gap {i}");
                Assert.AreNotEqual(calls[i - 1].spot, calls[i].spot, "never the same tree twice in a row");
                Assert.AreNotEqual(calls[i - 1].clip, calls[i].clip, "never the same call twice in a row");
            }
            Assert.AreEqual(6, calls.Select(c => c.spot).Distinct().Count(), "every tree sings sometimes");
            Assert.AreEqual(5, calls.Select(c => c.clip).Distinct().Count(), "every call is heard");
        }

        [Test]
        public void Ambience_PauseHoldsTheBirds()
        {
            var a = new Ambience(3, 4, 4);
            Run(a, 5);
            var during = Run(a, 120, _ => true, start: 5);
            Assert.IsEmpty(during, "no birds while paused (menu, focus lost, headset off)");
            var after = Run(a, 3, start: 125);
            Assert.LessOrEqual(after.Count, 1, "no burst of held-back birds afterwards");
        }

        [Test]
        public void Ambience_OffIsSilent()
        {
            var a = new Ambience(5, 4, 4) { Level = NatureSound.Off };
            Assert.AreEqual(0, a.BirdVolume);
            Assert.IsEmpty(Run(a, 120), "no birds when off");
            a.Level = NatureSound.Normal;
            Assert.Greater(a.BirdVolume, 0);
            var calls = Run(a, 60, start: 120);
            Assert.IsNotEmpty(calls);
            Assert.GreaterOrEqual(calls[0].at - 120, Ambience.MinGap - Frame, "switched on: the first bird still waits");
            a.Level = NatureSound.Low;
            Assert.That(a.BirdVolume, Is.GreaterThan(0).And.LessThan(new Ambience(5, 4, 4).BirdVolume), "low is quieter than normal");
            Assert.AreEqual(NatureSound.Low, Ambience.Next(NatureSound.Normal));
            Assert.AreEqual(NatureSound.Off, Ambience.Next(NatureSound.Low));
            Assert.AreEqual(NatureSound.Normal, Ambience.Next(NatureSound.Off));
        }

        static float Peak(float[] d) => d.Max(Mathf.Abs);

        [Test]
        public void Synth_BirdsAreSoft()
        {
            var birds = SoundSynth.Birds();
            Assert.GreaterOrEqual(birds.Length, 4, "several different calls");
            var rate = SoundSynth.Rate;
            foreach (var b in birds)
            {
                Assert.That(b.Length, Is.InRange(rate / 10, rate * 2), "a short call");
                Assert.LessOrEqual(Peak(b), 0.5f, "no loud peak");
                var firstMs = b.Take(rate / 1000 * 3).Max(Mathf.Abs);
                Assert.LessOrEqual(firstMs, Peak(b) * 0.5f, "a soft start, no click");
                Assert.LessOrEqual(Mathf.Abs(b[b.Length - 1]), 0.01f, "fades out");
            }
        }

        [Test]
        public void Synth_AmbienceBelowFeedback()
        {
            var a = new Ambience(1, 4, 4) { Level = NatureSound.Normal };
            var birdPeak = SoundSynth.Birds().Max(Peak) * a.BirdVolume;
            var pling = Peak(SoundSynth.Pling()) * Sounds.PlingVolume;
            var pop = Peak(SoundSynth.Pop()) * Sounds.PopVolume;
            Assert.Less(birdPeak, pling * 0.5f, "a bird is well under the sale's pling");
            Assert.Less(birdPeak, pop * 0.6f, "and under the basket's pop");
        }

        [Test]
        public void Menu_ParentCyclesNatureSound()
        {
            var menu = new GameMenu { BuildLine = "", NatureSound = NatureSound.Normal };
            menu.OpenParent();
            var row = menu.Rows().First(r => r.Kind == RowKind.Button && r.Text.StartsWith("Naturljud"));
            StringAssert.Contains("normalt", row.Text);
            Assert.AreEqual(MenuAction.CycleNatureSound, menu.SelectItem(row.Item));
            Assert.IsTrue(menu.IsOpen && menu.ShowingParent, "the menu stays open on the parent page");
            menu.NatureSound = NatureSound.Off; // the game answers by changing the setting
            StringAssert.Contains("Naturljud: av", string.Join("\n", menu.Lines()));
        }
    }
}
