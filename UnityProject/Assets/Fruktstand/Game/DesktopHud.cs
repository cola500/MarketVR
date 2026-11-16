// Desktop on-screen status (IMGUI, no UI package). Disabled in VR, where the bubbles and pictures carry everything.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class DesktopHud : MonoBehaviour
    {
        const string Controls =
            "1–9: lägg vara i korgen (1–3 frukt/grönt, 4–6 godis, 7–9 leksaker)\n" +
            "Backsteg: ta ur senaste   Mellanslag: brickan i kassan\n" +
            "Tab: kameravy   O: stäng/öppna   C: återställ beställningen\n" +
            "P: städa lekytan   R två gånger: nytt spel   + / −: höjd";

        [SerializeField] FruitStand stand;
        GUIStyle box;

        public static string StateText(SaleState s) => s switch
        {
            SaleState.CustomerArriving => "Kunden kommer…",
            SaleState.Ordering => "Plocka beställningen.",
            SaleState.AwaitingPayment => "Rätt! Lägg brickan i kassan.",
            SaleState.Thanking => "Tack!",
            SaleState.Closed => "Marknaden är stängd. Tryck O för att öppna.",
            _ => "Kunden går. Nästa kund kommer.",
        };

        void OnGUI()
        {
            if (stand == null || stand.Market == null) return;
            box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, wordWrap = true, padding = new RectOffset(10, 10, 8, 8) };
            var s = stand.Shop;
            var basket = "";
            for (var i = 0; i < Basket.Capacity; i++) basket += s.Basket[i].HasValue ? WareInfo.Name(s.Basket[i].Value) + " " : "· ";
            var area = stand.PlayArea;
            var children = "";
            foreach (var t in WareInfo.Toys) children += $"{WareInfo.Name(t)}: {area.State(t)}  ";
            var text = $"Vy: {MarketLayout.Name(stand.View)}\nKund {s.CustomerNumber} vid {WareInfo.Name(s.Category)} vill ha: {s.Order}\n" +
                       $"Korgen: {basket}\n{StateText(s.State)}\nKassan: {stand.Wallet.Balance} mynt   Köp: {s.CompletedSales}\nLekytan: {children}";
            GUI.Box(new Rect(12, 12, 520, 160), text, box);
            GUI.Box(new Rect(12, Screen.height - 110, 520, 98), Controls, box);
        }
    }
}
