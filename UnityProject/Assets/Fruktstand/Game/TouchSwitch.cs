// A switch you flip by moving an empty hand through it (the Öppet/Stängt sign). Pure logic, unit-tested.
// - Flips when a hand comes within Enter of it, but only if that hand was first seen outside Exit (armed): a hand
//   that is already inside when it appears (tracking regained, back from a menu) never flips it.
// - One flip per pass: the hand must leave beyond Exit before it can flip again (hysteresis), and a short cooldown
//   stops the other hand flipping it straight back in the same swipe.
// - Only an empty hand counts, so carrying the coin past the sign to the register never closes the stand.
// - Disabled (menu open, no focus, paused) or untracked: the hand is disarmed.
namespace Fruktstand.Game
{
    public sealed class TouchSwitch
    {
        public const float Enter = 0.07f;   // metres from the sign's centre
        public const float Exit = 0.11f;
        public const float Cooldown = 0.5f; // seconds

        readonly bool[] armed = new bool[2];
        float cooldown;

        public void Tick(float dt) => cooldown = cooldown > dt ? cooldown - dt : 0;

        public bool Update(int hand, bool tracked, bool empty, float distance, bool enabled)
        {
            if (!tracked || !enabled)
            {
                armed[hand] = false;
                return false;
            }
            if (distance > Exit)
            {
                armed[hand] = true;
                return false;
            }
            if (distance < Enter && armed[hand])
            {
                armed[hand] = false;
                if (!empty || cooldown > 0) return false;
                cooldown = Cooldown;
                return true;
            }
            return false;
        }
    }
}
