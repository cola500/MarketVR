// The crocodile game at the play table (EPIC-006). Pure rules, no rendering; the random source is injected.
// Eight teeth, one of them (drawn anew every round) snaps the mouth shut; the others just go down and stay down.
//
//   Open --(trigger tooth)--> Closing --> Closed --(pause)--> Opening --> Open (teeth up, new trigger)
//   After the DanceAfterSnaps-th snap, once per session: Closed --> WaitingToDance (until no hand is in the mouth
//   area) --> Dancing --> Opening. Only Nytt spel (NewGame) resets the count and the surprise.
// A press counts once: a hand that pressed a tooth must leave that tooth's press area before it can press it again,
// and when the mouth reopens or play resumes, every tooth a hand is already on is blocked for that hand until it
// has left it. Paused (menus, focus lost): no presses, and the clock stands still.
using System;

namespace Fruktstand.Core
{
    public enum CrocState { Open, Closing, Closed, WaitingToDance, Dancing, Opening }

    public enum ToothPress { Ignored, Safe, Snap }

    public sealed class CrocodileGame
    {
        public const int Teeth = 8;
        public const float CloseSeconds = 0.15f;  // quick but playful [A]
        public const float ClosedSeconds = 1.5f;  // a short pause with the mouth shut [A]
        public const float OpenSeconds = 0.5f;
        public const float DanceSeconds = 2.5f;
        public const int DanceAfterSnaps = 5;

        readonly Func<int, int> random; // random(n) -> 0..n-1
        readonly bool[] pressed = new bool[Teeth];
        readonly bool[] handInMouth = new bool[2];
        readonly int[] handOnTeeth = new int[2]; // bit t: the hand is within tooth t's press area
        readonly int[] blocked = new int[2];     // bit t: this hand must leave tooth t before pressing it
        bool paused;

        public CrocodileGame(Func<int, int> random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            TriggerTooth = random(Teeth);
        }

        public CrocState State { get; private set; } = CrocState.Open;
        public float StateTime { get; private set; }
        public int Round { get; private set; } = 1;   // 1-based
        public int Snaps { get; private set; }        // rounds that ended with a snap
        public bool Danced { get; private set; }      // the surprise has happened this session
        public bool PartyDancing { get; private set; } // EPIC-008: dancing along with the market party (not the surprise)
        // Its own surprise dance is on or about to start: the market party waits rather than compete with it.
        public bool OwnDanceBusy => !PartyDancing && State is CrocState.Dancing or CrocState.WaitingToDance;
        public bool AnyHandInMouth => handInMouth[0] || handInMouth[1];
        public int TriggerTooth { get; private set; } // never shown to the player
        public int PressedCount { get; private set; }

        public bool IsPressed(int tooth) => Valid(tooth) && pressed[tooth];
        public bool CanPress(int tooth) => !paused && State == CrocState.Open && Valid(tooth) && !pressed[tooth];

        public ToothPress Press(int hand, int tooth)
        {
            if (!CanPress(tooth) || hand is < 0 or > 1 || (blocked[hand] & (1 << tooth)) != 0) return ToothPress.Ignored;
            blocked[hand] |= 1 << tooth;
            if (tooth == TriggerTooth)
            {
                Snaps++;
                Enter(CrocState.Closing);
                return ToothPress.Snap;
            }
            pressed[tooth] = true;
            PressedCount++;
            return ToothPress.Safe;
        }

        public void Tick(float dt)
        {
            if (dt <= 0 || paused) return;
            StateTime += dt;
            switch (State)
            {
                case CrocState.Closing when StateTime >= CloseSeconds:
                    Enter(CrocState.Closed);
                    break;
                case CrocState.Closed when StateTime >= ClosedSeconds:
                    Enter(Snaps == DanceAfterSnaps && !Danced ? CrocState.WaitingToDance : CrocState.Opening);
                    if (State == CrocState.WaitingToDance && !AnyHandInMouth) Enter(CrocState.Dancing);
                    break;
                case CrocState.WaitingToDance when !AnyHandInMouth:
                    Enter(CrocState.Dancing);
                    break;
                case CrocState.Dancing when StateTime >= DanceSeconds:
                    if (PartyDancing) PartyDancing = false; // the party dance counts for nothing
                    else Danced = true;
                    Enter(CrocState.Opening);
                    break;
                case CrocState.Opening when StateTime >= OpenSeconds:
                    NewRound();
                    break;
            }
        }

        // Where a hand is, every frame: onTeeth = bit t set when the hand is within tooth t's press area; inMouth =
        // within the mouth area (the dance waits until no hand is there).
        public void UpdateHand(int hand, int onTeeth, bool inMouth)
        {
            if (hand is < 0 or > 1) return;
            handInMouth[hand] = inMouth;
            handOnTeeth[hand] = onTeeth;
            blocked[hand] &= onTeeth; // leaving a tooth's area frees it for this hand
        }

        // EPIC-008: the market party. Not while its own surprise is on; afterwards it reopens for a fresh round.
        public bool StartPartyDance()
        {
            if (OwnDanceBusy || PartyDancing) return false;
            PartyDancing = true;
            Enter(CrocState.Dancing);
            return true;
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            if (!paused) BlockTeethUnderHands(); // a hand resting on a tooth through the pause never presses it
        }

        // Nytt spel: the count and the surprise start over, and the mouth opens for a fresh round at once.
        public void NewGame()
        {
            Snaps = 0;
            Danced = false;
            PartyDancing = false;
            NewRound();
        }

        void NewRound()
        {
            Array.Clear(pressed, 0, Teeth);
            PressedCount = 0;
            TriggerTooth = random(Teeth);
            Round++;
            BlockTeethUnderHands();
            Enter(CrocState.Open);
        }

        void BlockTeethUnderHands()
        {
            for (var h = 0; h < 2; h++) blocked[h] = handOnTeeth[h];
        }

        void Enter(CrocState state)
        {
            State = state;
            StateTime = 0;
        }

        static bool Valid(int tooth) => tooth >= 0 && tooth < Teeth;
    }
}
