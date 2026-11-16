// Desktop: the only place that reads keyboard input (legacy Input Manager). Quick check of the market's rules without
// a headset: 1–3 put the fruit/veg on sale (bin order), 4–6 lollipop, polkagris, candy bag, 7–9 ball, car, blocks in
// the active stall's basket, Backspace takes the last one out, Space puts the payment token in the register, Tab switches the
// desktop camera view, O closes/opens the market, C restarts the current order, P tidies the play area, R twice (within
// 2 s) starts a new game, +/- raise/lower the counters.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class DesktopInput
    {
        public const float NewGameConfirmSeconds = 2f;
        float newGameArmedUntil = -1;

        public bool NewGameArmed => Time.unscaledTime < newGameArmedUntil;

        static Ware? WareKey(Assortment assortment)
        {
            for (var i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    return assortment.Active(WareInfo.Categories[i / 3])[i % 3];
            return null;
        }

        public ShopFrame Read(float dt, Assortment assortment)
        {
            var frame = new ShopFrame
            {
                DeltaTime = dt,
                AddDirect = WareKey(assortment),
                RemoveLastDirect = Input.GetKeyDown(KeyCode.Backspace),
                PayDirect = Input.GetKeyDown(KeyCode.Space),
                NextViewDirect = Input.GetKeyDown(KeyCode.Tab),
                ToggleOpen = Input.GetKeyDown(KeyCode.O),
                ClearBasket = Input.GetKeyDown(KeyCode.C),
                TidyPlay = Input.GetKeyDown(KeyCode.P),
                CounterHeightDelta = ((Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.PageUp) ? 1 : 0)
                    - (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.PageDown) ? 1 : 0)) * XrInputRouter.CounterSpeed * dt,
            };
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (NewGameArmed)
                {
                    frame.NewGame = true;
                    newGameArmedUntil = -1;
                }
                else newGameArmedUntil = Time.unscaledTime + NewGameConfirmSeconds;
            }
            return frame;
        }
    }
}
