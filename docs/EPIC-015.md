---
title: EPIC-015 – kunderna använder parkens gångar och bänk
status: built-not-tried
last_updated: 2026-11-02
---

# EPIC-015 – kunderna använder parkens gångar och bänk

Regler: [SPEC §32](SPEC.md#32-besökare-i-parken-epic-015). Bygger på EPIC-013/014 (parken). Inga nya spelregler, ingen tidspress och ingen kö. Prestandatestläget är uppskjutet (föräldern, 2026-11-02).

## Inventering (2026-11-02)

- **Gångar:** en grusring 1,0–1,85 m från mitten och armar framåt och bakåt ut till 8 m (`ParkLayout`). Bänken står vid (−2,65; 1,3) och vänder sig mot marknaden. Sitsen ligger 0,38 m upp (0,48 m i modellen × 0,8), med ryggstödet bortåt.
- **Kunder:** `CustomerView` återanvänds för varje kund. Den glider in 1,6 m längs disken (`Shop.ArriveSeconds` 1,5 s) och ut 1,6 m åt andra hållet (`LeaveSeconds` 1,5 s). Den syns alltså dyka upp och försvinna nära disken.
- **Barn:** leksakskunden tas över av lekbordet (`PlayTableView`) och går dit. När leken är slut går barnet 1,5 m rakt bakåt och försvinner.
- **Figurer:** primitiver (`CustomerFigure`): ben som en kapsel, kropp, armar och ansikte med humör. Rörelsen består av hopp och vinkning.

## Slice

1. **Ankomst:** kunden kommer från en entré i slutet av gången framåt eller bakåt (6,5 m bort, bakom en buske), går längs gången och ringen till sitt stånd och kliver fram till disken.
2. **Försäljning bara framme:** `Shop` står kvar i `CustomerArriving` tills kunden har kommit fram (`CustomerArrived`). Ingen beställning, korg eller betalning dessförinnan. Bubblan säger *Hej!* först när kunden är nära.
3. **Avfärd:** efter tacket kliver kunden åt sidan längs disken (`LeaveSeconds`, som förut). Sedan är disken ledig och nästa kund börjar komma. Den gamla kunden tas över av en **parkbesökare** som går vidare längs ringen och ut genom en entré.
4. **Variation** (viktad, injicerbar slump, `ParkVisits`):
   - *ut* (5): närmaste utgång;
   - *omväg* (3): runt ringen och ut genom den andra utgången;
   - *bänk* (2): bara om sitsen är ledig. Sätter sig 8–15 s, reser sig och går ut.
5. **Leksakskunder** går som förut till lekbordet. När leken är slut tar en parkbesökare över barnet på gången bakåt.
6. **Gräns:** högst två parkbesökare samtidigt, utöver kunden vid disken och de lekande barnen. Om en tredje behövs tas den besökare bort som är längst ut och närmast en utgång. Omväg och bänk väljs bara när det finns plats.
7. **Bänkplats:** en sits som reserveras när bänken väljs och frigörs när besökaren reser sig. Två besökare sätter sig aldrig på samma plats.
8. **Trängsel:** *ersatt av [EPIC-016](EPIC-016.md)* (avståndshållning för kunden och besökarna). Ursprungligen stannade en besökare högst 1,5 s för en figur närmare än 0,5 m framför sig, och kunden väjde inte alls.
9. **Rörelse:** 0,95 m/s, bildfrekvensoberoende. Gungningen följer steglängden, svängarna är mjuka (riktningen glider mot gångens riktning) och fötterna är på marken. Sittandet är en enkel övergång på 0,6 s mellan stående och sittande ställning (benen snett framåt, höften på sitsen).
10. **Koordinater:** rutterna ligger i marknadens koordinater (`ParkRoutes`) och räknas om med ståndens avstånd `d`. Besökarna är barn till parken, så de följer kalibreringen och står kvar på marken vid höjdjustering.
11. **Paus:** besökarna står still när marknaden står still (fest, vilopaus och föräldrapaus), precis som kunden och barnen. Nytt spel tar bort alla besökare och frigör bänken.
12. **Mobilkunden** använder samma ankomst och avfärd. Fotot följer inte med parkbesökaren.

## Rutter

- **Slinga:** en rundad kvadrat med halva sidan 1,72 m och hörnradie 0,6 m. Den ligger i gruset, utanför diskarna, lekbordet och barnens pallar.
- **Armar:** från slingan rakt ut till entréerna (0; ±6,5).
- **Disken:** `A` (inkliv, 1,6 m åt ena sidan längs disken) → kundplatsen `S` → `D` (utkliv åt andra sidan).
- **Bänken:** från slingan ut på gräset till en punkt framför bänken, sedan sitsen.

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| B1 | Varje ankomstrutt börjar vid en entré och slutar vid rätt stånds kundplats, vid alla `d`. | [T: Routes_ArriveAtTheRightStall] |
| B2 | Rutterna går på gången (bänkens sista bit undantagen) och aldrig genom diskar, lekbord, hylla, bänk, pallar eller arbetsyta. | [T: Routes_StayOnThePathAndClear] |
| B3 | `Shop` väntar på ankomsten: ingen beställning eller betalning innan, och ankomsten följer inte med till nästa kund. | [T: Shop_WaitsForTheCustomerToArrive] |
| B4 | Viktad variation med injicerad slump. Bänken väljs bara när sitsen är ledig, och omväg och bänk bara när det finns plats. | [T: Visits_WeightedChoice] [T: Visits_BenchReservedAndFreed] [T: Visits_LimitedVisitors] |
| B5 | I scenen: kunden går in längs gången och handlar först framme. När disken är ledig börjar nästa kund komma, och den förra blir parkbesökare. | [T: Park_CustomerWalksInAndSellsOnArrival] [T: Park_NextCustomerWhenCounterFree] |
| B6 | Efter ett leksaksköp går barnet till lekbordet, inte ut i parken. Först när leken är slut blir barnet besökare. | [T: Park_ToyChildGoesToPlayFirst] |
| B7 | Aldrig fler än två besökare. Nytt spel tar bort alla och frigör bänken. | [T: Park_VisitorsLimitedAndClearedOnNewGame] |
| B8 | Bänken: besökaren sätter sig på sitsen (höften på sitsen, ingen svävning), en åt gången, och går sedan. | [T: Park_VisitorSitsOnTheBenchAndLeaves] |
| B9 | Paus (vilopaus) och återupptagning: inga dubbla kunder eller betalningar, och besökarna står still under pausen. | [T: Park_PauseHoldsVisitorsNoDoubleSale] |
| B10 | Regression: försäljning, mobilkund, lek, fest, menyer, kalibrering. | hela `scripts/quest.sh test` |
| B11 | Översiktsbild med rutterna markerade. | [R: Snapshots_ParkRoutes] |
| B12 | Headset: gångar, fötter, svängar och sittande ser rimliga ut. Tempot (väntan på kunden) känns bra. | [H] [P] |

## Verifieringslogg

**Röda före implementation (2026-11-02):** mot skelett föll alla 12 nya tester: `ParkVisitTests` (6), där `Shop` inte väntade, valen saknades och rutterna var tomma, och `ParkVisitorSceneTests` (6), där det inte fanns någon promenad och inga besökare.

**Gröna:** `ParkVisitTests` 6/6 och `ParkVisitorSceneTests` 6/6 vid första fullständiga körningen.

**Äldre tester som antog 1,5 s ankomst** (15 föll i hela sviten):
- `StandSceneTests` (12) och `PauseSceneTests` väntar nu tills kunden är framme, i stället för en fast tid.
- `Scene_ObjectCountsBounded` och `Scene_PhysicsWithinBudget` handlar om tre barn samtidigt vid lekbordet, inte om promenaden. De sätter därför `ParkRoutes.WalkSpeed` högt under testet och återställer den efteråt. Med normal fart hinner första barnet leka klart (45 s) innan det tredje kommer. Det är en följd av det nya tempot och ska prövas på headset.

**Siktlinjetestet** (`Park_KeepsSightlinesAlongThePath`, EPIC-014) gäller nu längs gången fram till dess slut på 9 m. Grindarna (en buske och två träd) stänger vyn där bortom, med avsikt.

**Renderingar [R]** (`Snapshots_ParkRoutes`):
- `70-routes-overview` visar rutterna markerade (blå ankomst, grön ut, orange omväg, lila bänk, rosa barnens väg hem) och en kund som kommer fram bakom grindbusken.
- `71-customer-walking-in` visar kunden på gången framåt.
- `72` och `74` visar en besökare på bänken från arbetsytan.
- `73-visitor-on-the-bench` är en närbild.

**Hittat i renderingarna:**
1. Med en enda benkapsel snett nedåt gick benen genom sitsen. Nu ligger låren över sitsen och ett underben hänger ner.
2. Bubblan sa *Hej!* medan kunden var långt bort, eftersom `Rebuild` valde efter tillståndet och inte efter nyckeln. `OrderBubble.Words` och en kontroll i `Park_CustomerWalksInAndSellsOnArrival` lades till först (röd), sedan rättelsen.

**Hela sviten:** EditMode 208/208 och PlayMode 97/97 (8 explicita snapshots överhoppade).

**Inte verifierat:** allt [H]/[P] i B12, alltså gångar, fötter, svängar, sittande och tempo på headset, samt Quest-prestanda med besökare.


**Bygge:** `2026-11-02_094413_e7700d8`, rent, 24 MB, sha256 `7c0ca439…`. **Installerat 2026-11-02** (override: spelet låg i förgrunden, föräldern gav klartecken), versionName bekräftad med `scripts/quest.sh install`, och appen startad. Headsettestet (del 14) väntar.
