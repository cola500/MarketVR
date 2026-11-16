// When to put the stands in front of the player. Pure logic, unit-tested.
// Found on the headset (2026-10-31): the stands were placed from the first valid head pose, while the headset was
// still lying on a table, so the player started far away; a system recenter (hold the Meta button) was ignored.
// Rules:
// - First placement waits until the headset has been worn for SettleSeconds (or presence is not reported at all):
//   on the headset (2026-10-31) presence read true about 3 s before the proximity sensor's PROX_ON, while the headset
//   was still in the hands, which put the counter at its minimum height.
// - Every placement also sets the counter height from the current eye height (FruitStand.PlaceInFront).
// - A system recenter, or "Flytta stånden hit" in the menu, places them in front again (when worn).
//   On the headset (2026-10-31) a manual recenter (hold the Meta button) did NOT raise Unity's trackingOriginUpdated
//   (only the app's own recenter at start did), so a recenter is also detected from the head pose: the tracking
//   space is replaced, which makes the head pose jump further in one frame than a head can move.
// - Nothing else moves the market: walking about the small play space, or taking the headset off and on, never
//   does (EPIC-003 compact market, SPEC §18; the earlier "put on far away" rule is removed).
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class StandPlacement
    {
        public const float SettleSeconds = 1f;  // worn this long before the first placement
        public const float JumpMetres = 0.25f;  // head position change in one frame that only a recenter explains
        public const float JumpDegrees = 40f;   // head yaw change in one frame that only a recenter explains
        public const float MaxFrameSeconds = 0.1f;

        // Head pose in tracking space, this frame vs the previous one. A snap turn rotates the rig, not this pose,
        // so it never looks like a recenter. Long gaps (paused, headset off) are ignored.
        public static bool LooksLikeRecenter(Vector3 previousPosition, Quaternion previousRotation, Vector3 position, Quaternion rotation, float dt)
        {
            if (dt <= 0 || dt > MaxFrameSeconds) return false;
            var moved = new Vector2(position.x - previousPosition.x, position.z - previousPosition.z).magnitude;
            var yaw = Mathf.Abs(Mathf.DeltaAngle(previousRotation.eulerAngles.y, rotation.eulerAngles.y));
            return moved > JumpMetres || yaw > JumpDegrees;
        }

        bool requested;

        public bool Placed { get; private set; }
        public bool Requested => requested;

        // Recenter event or menu choice: place again as soon as possible.
        public void Request() => requested = true;

        // worn: true/false from the headset's presence sensor, null if not reported.
        // wornSeconds: how long the headset has been worn without a break (ignored when presence is not reported).
        public bool ShouldPlace(bool hasPose, bool? worn, float wornSeconds = SettleSeconds)
        {
            if (!hasPose || worn == false) return false;
            if (!Placed && worn == true && wornSeconds < SettleSeconds) return false;
            if (!Placed || requested)
            {
                Placed = true;
                requested = false;
                return true;
            }
            return false;
        }
    }
}
