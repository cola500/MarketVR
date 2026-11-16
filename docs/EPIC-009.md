---
title: EPIC-009 – fler frukter och växlande sortiment
status: built-not-installed
last_updated: 2026-11-01
---

# EPIC-009 – fler frukter och växlande sortiment

**Produktmål:** frukt- och grönsaksståndet visar alltid **tre olika sorter**, valda ur en katalog med **åtta**. Sortimentet kan vara ett annat i nästa spelomgång. Godis och leksaker ändras inte. Regler: [SPEC §26](SPEC.md#26-växlande-fruktsortiment-epic-009). Modellerna görs med arbetssättet från [SPIKE-BLENDER](SPIKE-BLENDER.md).

## Katalog (frukt & grönt, 1 spelpeng per vara)

| Sort | Mobilnyckel | Form och färg |
|---|---|---|
| äpple | `apple` | rund, röd, stjälk och blad (ny modell) |
| banan | `banana` | lång och böjd, gul (modellen från spiken) |
| morot | `carrot` | spetsig och orange, gröna blad (ny modell) |
| päron | `pear` | droppform, gulgrön, stjälk |
| apelsin | `orange` | klot, orange, liten grön fläck |
| jordgubbe | `strawberry` | liten och röd, hjärtform, gröna blad, ljusa prickar |
| vindruvsklase | `grapes` | triangulär klase av lila bär, en stjälk (**en** vara, **ett** greppobjekt) |
| vattenmelonskiva | `watermelon` | halvcirkel: grönt skal, rött fruktkött, svarta kärnor |

## Regler

- **Sortiment** (`Core/Assortment`, ren logik, egen seedad slump): exakt tre olika frukt/grönt-sorter är aktiva, i lådordning (vänster–mitten–höger). Godis och leksaker är alltid sina tre.
- **Ny dragning** (inte samma tre som förut, om det går):
  - vid **Nytt spel**;
  - vid **Starta nästa omgång** efter vilopausen. Pågår en kunds order eller finns en accepterad mobilorder kvar, byts sortimentet först när nästa kund ska komma och ingen mobilorder är aktiv. En accepterad mobilorder tappas alltså aldrig.
  - Appstart drar ett sortiment med ett tidsbaserat frö; testerna sätter ett fast sortiment.
- **Aldrig byte** under en pågående beställning, vid meny- eller fokuspaus, omcentrering, höjdjustering, inriktning eller Töm korgen.
- **Beställningar:** NPC-kunder beställer bara aktiva sorter. Gränserna för antal behålls (1–3 varor, kund 2–3 högst 2). Kund 1 vill ha **en** vara av den första aktiva sorten (tidigare: alltid ett äpple).
- **Mobilen:** marknadsstatusen (`market`) bär med sig de aktiva sorterna (`fruit`: tre nycklar). Mobilsidan visar bara dem. En order på en inaktiv sort avvisas med orsaken `inactive`; den sparas inte, så mobilen kan beställa igen. Quest validerar alltid själv.
- Pris, kassa, korg, betalning och försäljningsflöde är oförändrade. Ingen lagerbrist.

## Stories och acceptanskriterier

### S1 – Katalog och tre aktiva sorter

| # | Kriterium | Verifiering |
|---|---|---|
| A1 | Katalogen har åtta frukt/grönt-sorter, alla 1 spelpeng, med namn och mobilnyckel. Godis och leksaker är oförändrade. | [T: Catalogue_EightFruitAllPriceOne] |
| A2 | Ett sortiment är exakt tre olika giltiga frukt/grönt-sorter. | [T: Assortment_ThreeDistinctFruit] |
| A3 | Samma frö ger samma sortiment; olika frön ger mer än ett sortiment. | [T: Assortment_SameSeedSameWares] [T: Assortment_VariesOverSeeds] |
| A4 | Ny dragning ger ett annat sortiment än det förra. | [T: Assortment_RedrawDiffers] |

### S2 – Beställningar och försäljning

| # | Kriterium | Verifiering |
|---|---|---|
| B1 | NPC-order innehåller bara aktiva sorter (många kunder, flera frön). | [T: Orders_OnlyActiveFruit] |
| B2 | Kund 1 vill ha en vara av den första aktiva sorten. | [T: Orders_FirstCustomerWantsFirstActiveFruit] |
| B3 | Alla åtta sorter kan säljas för 1 per vara, och kassan ökar med summan. | [T: Sale_EveryFruitSellsForOne] |

### S3 – När sortimentet byts

| # | Kriterium | Verifiering |
|---|---|---|
| C1 | Nytt spel drar nytt sortiment. | [T: Market_NewGameDrawsNewAssortment] |
| C2 | Nästa omgång efter vilopaus drar nytt sortiment; vilopausen själv och festen gör det inte. | [T: Market_NextRoundDrawsNewAssortment] |
| C3 | En order, Töm korgen och paus (fokus/meny) byter inte sortiment. | [T: Market_AssortmentKeptDuringOrderAndPause] |
| C4 | Ta vilopaus mitt i en order och starta nästa omgång: ordern fortsätter med samma sortiment, bytet sker när nästa kund kommer. | [T: Market_ChangeWaitsForOrderInProgress] |
| C5 | Scen: omcentrering och höjdjustering byter inte sortiment; vid byte visar lådor, skylt och ståndets varor de nya sorterna och inget gammalt ligger kvar. | [T: Scene_AssortmentChangeRebuildsFruitStall] [T: Scene_RecentreKeepsAssortment] |

### S4 – Mobilkund

| # | Kriterium | Verifiering |
|---|---|---|
| D1 | Order på inaktiv sort avvisas med `inactive`, sparas inte och kan göras om. | [T: Remote_InactiveFruitRejected] |
| D2 | En accepterad mobilorder överlever att nästa omgång startas. | [T: Market_AcceptedPhoneOrderSurvivesRoundChange] |
| D3 | `market` innehåller de aktiva sorterna och skickas igen när de ändras. | [T: Session_MarketCarriesActiveFruit] |
| D4 | Relän sparar och vidarebefordrar `fruit` till nya och anslutna mobiler. | [T relay: Relay_MarketFruitKeptForNewPhones] |
| D5 | Mobilsidan visar bara de aktiva sorterna och förklarar en avvisning med `inactive`. | [E] manuellt i webbläsare |

### S5 – Modeller

| # | Kriterium | Verifiering |
|---|---|---|
| M1 | Åtta modeller från Blender-skript (källa + FBX), låg polygonmängd, enkla material färgade i koden. Kontaktbild med alla åtta. | [R: art/fruit/contact_sheet.png] [T: Scene_WaresAreBlenderModels] (hette Scene_FruitAreBlenderModels före EPIC-010) |
| M2 | Varje sort syns i lådan, på skylten, i högen och i kundens bubbla med samma modell. | [R: Snapshots] |
| M3 | Sorterna skiljs åt med både form och färg i headsetet; vindruvsklasen greppas som ett föremål. | [H] |
| M4 | Spelaren förstår kundernas bilder och säljer som förut; efter en ny omgång kan sortimentet vara ett annat, och godis, leksaker och övrigt fungerar. | [P] |

## Verifieringslogg

**Automatiska tester (2026-11-01)**
- Rött först, av rätt skäl, med skelett för `AssortmentTests` (C# Core). Därefter grönt.
- Reläns `Relay_MarketFruitKeptForNewPhones` var rött först och sedan grönt.
- Scentesterna (`AssortmentSceneTests`, `Scene_FruitAreBlenderModels`) skrevs efter vy-implementationen och sågs inte röda först. Det är en avvikelse från TDD-regeln.
- Slutläge: EditMode 158/158, PlayMode 71 godkända + 4 explicita överhoppade (snapshots och reläkedjor), relä 12/12.
- Hela Unity-sviten kördes tre gånger i rad utan hängning, efter rättningen i [TESTING.md](TESTING.md).

**Blender (5.2.3):** `art/fruit/make_fruit.py` gav åtta FBX (totalt 2 928 trianglar; den största är vindruvsklasen med 660) och kontaktbilden `art/fruit/contact_sheet.png`. Granskad visuellt.

**Unity (visuellt):** `scripts/quest.sh snapshots`, bilderna 24–27. Päron/jordgubbe/vindruvor och apelsin/vattenmelon/äpple syns i lådor, högar, på skylten och i bubblorna, och bubblan äpple + apelsin visar summan 2.

**Mobilsidan [E]:** inte prövad i webbläsare. JavaScript-koden parsar, och protokollet är testat (`Session_MarketCarriesActiveFruit`, relätestet).

**Bygge:** `2026-11-01_165940_24448a7`, rent, 24 MB, sha256 `88f29e5c…`. **Inte installerat.**

**Headset [H] och spelaren [P]:** inte gjort. Se PLAYTEST del 8.

**Boy Scout:**
- `Prng` bröts ut ur `OrderGenerator` och delas med `Assortment` (samma sekvenser, testerna gröna).
- `PlayArea.SpotOf` beror inte längre av varuindex.
- Testkörningen fick progress, stoppvakt, stackprov och `quest.sh snapshots`, och ett obegränsat `while` i `Snapshots` fick en gräns. Allt detta verifierades med tre fulla körningar.

**Kvar:**
- `DesktopInput` (tangenterna 1–9 följer nu sortimentet) är bara manuellt användbar och inte testad.
- Progress-räknaren visar katalogens totala antal tester även när ett filter används.
