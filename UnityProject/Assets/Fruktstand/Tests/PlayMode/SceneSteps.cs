// Shared stepping for the PlayMode scene tests (BACKLOG 2): one frame with an optional right hand, waiting a while,
// and waiting for a condition. Each test file keeps its own time steps and passes its own Step to Wait/WaitUntil, so
// the frames a test sends (their order, their DeltaTime) are exactly what they were. Helpers that behave differently
// (frame counts with rounding, checks after every step, a given frame) stay in their own test files.
using System;
using Fruktstand.Game;
using UnityEngine;

namespace Fruktstand.Tests
{
    static class SceneSteps
    {
        // One frame. The right hand is tracked when given (Rotation identity unless turned); releaseAll is the menu,
        // a pause or lost focus; rig is how the stick moved the rig this frame (EPIC-005).
        public static void Step(FruitStand stand, Vector3? right, bool grab, float dt, bool releaseAll = false, Quaternion? turn = null, Matrix4x4? rig = null)
        {
            var frame = new ShopFrame { DeltaTime = dt, ReleaseAll = releaseAll, RigDelta = rig };
            if (right.HasValue) frame.Right = new HandFrame { Tracked = true, Position = right.Value, Rotation = turn ?? Quaternion.identity, Grab = grab };
            stand.Step(frame);
        }

        // `seconds` of game time in steps of dt.
        public static void Wait(float seconds, float dt, Action<float> step)
        {
            for (var t = 0f; t < seconds; t += dt) step(dt);
        }

        // Steps 0.05 s at a time until the condition holds (at most `seconds` of game time); true if it did.
        public static bool WaitUntil(Func<bool> condition, float seconds, Action<float> step)
        {
            const float dt = 0.05f;
            for (var t = 0f; t < seconds; t += dt)
            {
                if (condition()) return true;
                step(dt);
            }
            return condition();
        }
    }
}
