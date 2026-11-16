// EPIC-014: the park's nature sound, as rules: sporadic birds from a few trees (a wind bed was tried and taken out
// after the headset test: "directly tiresome"). The wait between
// birds is drawn anew each time (MinGap..MaxGap); the next bird is never from the same tree or the same call as the
// last. The clock stands still while paused (menu, focus lost, headset off), so nothing is held back and released in
// a burst. "Av" is silent; switching on again starts a fresh wait. Volumes are gains on the synthesised clips, well
// under the feedback sounds (AmbienceTests).
namespace Fruktstand.Core
{
    public enum NatureSound { Normal, Low, Off }

    public readonly struct BirdCall
    {
        public BirdCall(int spot, int clip)
        {
            Spot = spot;
            Clip = clip;
        }

        public int Spot { get; }
        public int Clip { get; }
    }

    public sealed class Ambience
    {
        public const float MinGap = 7f, MaxGap = 18f;

        readonly Prng random;
        readonly int spots, clips;
        float wait;
        int lastSpot = -1, lastClip = -1;
        NatureSound level;

        public Ambience(int seed, int spots, int clips)
        {
            random = new Prng(seed);
            this.spots = spots;
            this.clips = clips;
            wait = NextWait();
        }

        public NatureSound Level
        {
            get => level;
            set
            {
                if (level == NatureSound.Off && value != NatureSound.Off) wait = NextWait(); // no bird the moment it's on
                level = value;
            }
        }

        public float BirdVolume => level switch { NatureSound.Normal => 0.35f, NatureSound.Low => 0.16f, _ => 0f };

        public static NatureSound Next(NatureSound l) => l switch
        {
            NatureSound.Normal => NatureSound.Low,
            NatureSound.Low => NatureSound.Off,
            _ => NatureSound.Normal,
        };

        // One frame: the bird to start now, if any.
        public BirdCall? Tick(float dt, bool paused)
        {
            if (paused || level == NatureSound.Off || spots == 0 || clips == 0) return null;
            wait -= dt;
            if (wait > 0) return null;
            wait += NextWait(); // keep the overshoot: the gaps don't creep by a frame each time
            lastSpot = Other(lastSpot, spots);
            lastClip = Other(lastClip, clips);
            return new BirdCall(lastSpot, lastClip);
        }

        float NextWait() => MinGap + (MaxGap - MinGap) * (random.Below(1000) / 999f);

        int Other(int last, int n)
        {
            if (n == 1) return 0;
            var pick = random.Below(last < 0 ? n : n - 1);
            return last >= 0 && pick >= last ? pick + 1 : pick;
        }
    }
}
