// EPIC-013: the park belongs to the market: it follows an explicit placement (calibration), stays on the ground when
// the counters are raised or lowered, and has nothing that grabbing, pointing or toy physics could hit.
using System.Collections;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class ParkSceneTests
    {
        FruitStand s;

        [TearDown]
        public void ResetSpacing() => MarketLayout.SetStallDistance(MarketLayout.DefaultStallDistance);

        static void AssertFollowsMarket(FruitStand s, string when)
        {
            Assert.AreEqual(0, Vector3.Distance(s.ToWorld(Vector3.zero), s.Park.position), 1e-4f, $"{when}: park centred on the market");
            Assert.AreEqual(0, Quaternion.Angle(s.MarketRoot.rotation, s.Park.rotation), 0.01f, $"{when}: park turned with the market");
            Assert.AreEqual(0, s.Park.position.y, 1e-5f, $"{when}: park on the ground");
            Assert.AreEqual(ParkLayout.PathLift, s.Park.Find("Gravel Path").position.y, 1e-5f, $"{when}: path flat on the ground");
        }

        [UnityTest]
        public IEnumerator Park_FollowsPlacementNotCounterHeight()
        {
            yield return SceneLoad.Fresh(x => s = x);
            Assert.IsNotNull(s.Park, "the market has a park");
            AssertFollowsMarket(s, "start");

            s.ApplyPlacement(new Vector3(0.8f, 0, -0.6f), 35, 0.85f);
            AssertFollowsMarket(s, "after calibration");

            var before = s.Park.Find("Gravel Path").position;
            var height = s.CounterHeight;
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = 0.15f });
            Assert.Greater(s.CounterHeight, height, "the counters went up");
            Assert.AreEqual(before, s.Park.Find("Gravel Path").position, "the path did not");
            AssertFollowsMarket(s, "after raising the counters");
        }

        [UnityTest]
        public IEnumerator Park_HasNothingToGrabOrHit()
        {
            yield return SceneLoad.Fresh(x => s = x);
            Assert.IsEmpty(s.Park.GetComponentsInChildren<Collider>(true), "no colliders: rays, hands and toys pass the park by");
            foreach (var r in s.Park.GetComponentsInChildren<Renderer>(true))
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, $"{r.name}: no real-time shadow (cheap on Quest)");
        }
    

        // EPIC-014: birds only (no wind source), they follow the parent's setting and never sing while paused.
        [UnityTest]
        public IEnumerator Ambience_BirdsFollowSettingAndPauses()
        {
            yield return SceneLoad.Fresh(x => s = x);
            void Frames(float seconds, bool paused = false, bool cycle = false)
            {
                var n = Mathf.RoundToInt(seconds / 0.05f);
                for (var i = 0; i < n; i++) s.Step(new ShopFrame { DeltaTime = 0.05f, ReleaseAll = paused, CycleNatureSound = cycle && i == 0 });
            }

            Assert.IsEmpty(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(a => a.loop && a.clip != null),
                "no looping sound bed: the wind was too much (headset test 2026-11-02)");
            Assert.AreEqual(NatureSound.Normal, s.NatureSound);
            Frames(2 * Ambience.MaxGap);
            var sung = s.BirdsSung;
            Assert.Greater(sung, 0, "birds sing");
            Frames(60, paused: true); // menu open, focus lost or headset off
            Assert.AreEqual(sung, s.BirdsSung, "no birds while paused");
            Frames(1);
            Assert.LessOrEqual(s.BirdsSung - sung, 1, "and no burst afterwards");

            Frames(0.05f, cycle: true);
            Assert.AreEqual(NatureSound.Low, s.NatureSound);
            Frames(0.05f, cycle: true);
            Assert.AreEqual(NatureSound.Off, s.NatureSound);
            sung = s.BirdsSung;
            Frames(60);
            Assert.AreEqual(sung, s.BirdsSung, "off is silent");
            Frames(0.05f, cycle: true);
            Assert.AreEqual(NatureSound.Normal, s.NatureSound);
            Frames(2 * Ambience.MaxGap);
            Assert.Greater(s.BirdsSung, sung, "back on");
        }
    }
}
