// One frame of player input, from any source (VR controllers, desktop keys, tests). FruitStand.Step only ever
// sees this, never a device, so the whole game can be driven and tested without a headset.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public struct HandFrame
    {
        public bool Tracked;
        public Vector3 Position;    // world space: the point the hand grabs with
        public Quaternion Rotation; // world space
        public bool Grab;           // grip or trigger held this frame
    }

    public struct ShopFrame
    {
        public HandFrame Left;
        public HandFrame Right;
        public bool ReleaseAll;            // focus lost / paused / menu open: whatever is held goes home, nothing is dropped anywhere
        public float CounterHeightDelta;   // metres to raise (+) or lower (-) the counter this frame
        public int Turn;                   // VR snap turn: ±1 step of XrInputRouter.TurnDegrees (handled by XrRig)
        public Vector2 Walk;               // VR left stick after its dead zone, length <= 1 (x = sideways, y = forward; XrRig moves the rig)
        // How the rig moved the player this frame (walk/turn), world -> world. Hand speed is measured without it, so
        // walking or turning with something in the hand never throws it. Null = the rig did not move.
        public Matrix4x4? RigDelta;
        public float DeltaTime;            // seconds; drives customer timings
        // Desktop shortcuts (quick logic check without a headset).
        public Ware? AddDirect;      // put this ware in the active stall's basket
        public bool RemoveLastDirect;
        public bool PayDirect;
        public bool NextViewDirect;  // desktop: next camera view (development only)
        // Menu actions (VR menu or desktop keys).
        public bool NewGame;         // empties the till and starts over (after confirmation)
        public bool ToggleOpen;      // close / open the market for new customers
        public bool ClearBasket;     // restart only the current order
        public bool TidyPlay;        // put every toy that isn't held back in its place
        public bool TakeRest;        // EPIC-008 "Ta vilopaus"
        public bool NextRound;       // EPIC-008 "Starta nästa omgång" (parent, confirmed)
        public bool ResumeRest;      // EPIC-012 "Fortsätt spela": the child ends its own rest
        public bool ParentRest;      // EPIC-012 "Pausa spelet" on the VR parent page
        public bool ParentResume;    // EPIC-012 "Fortsätt spelet" on the VR parent page
        public bool CycleNatureSound; // EPIC-014 "Naturljud" on the VR parent page: normal -> low -> off

        public HandFrame Hand(int i) => i == 0 ? Left : Right;
    }
}
