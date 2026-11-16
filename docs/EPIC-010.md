---
title: EPIC-010 – godis och leksaker med växlande sortiment
status: built-not-installed
last_updated: 2026-11-01
---

# EPIC-010 – godis och leksaker med växlande sortiment

**Produktmål:** godisståndet och leksaksståndet visar, precis som frukt/grönt (EPIC-009), **tre olika sorter åt gången** ur egna, större kataloger. Alla varor får lekfulla Blender-modeller i fruktmodellernas stil. Barnen använder de nya leksakerna på lekbordet, och spelaren kan vara med och leka. Regler: [SPEC §27](SPEC.md#27-godis-och-leksaker-med-växlande-sortiment-epic-010). Modellflödet: [SPIKE-BLENDER](SPIKE-BLENDER.md). Testregler: [TESTING](TESTING.md).

## Kataloger

**Godis (åtta sorter, 1–3 spelpengar)**

| Sort | Pris | Mobilnyckel | Form och färg |
|---|---|---|---|
| klubba | 1 | `lollipop` | rund spiral på pinne, rosa/vit (ny modell) |
| polkagris | 2 | `polkagris` | randig stång med böj, röd/vit (ny modell) |
| godispåse | 3 | `candybag` | prasslig påse med knut, ljusblå (ny modell; innehållet simuleras inte) |
| gelébjörn | 1 | `gummybear` | knubbig björn, genomskinligt grön |
| chokladkaka | 3 | `chocolate` | rutig kaka, halvt i rött omslag |
| karamell | 1 | `wrappedcandy` | rund karamell med tvinnat papper i båda ändar, gul/orange |
| regnbågsrem | 2 | `rainbowstrip` | platt ränder-rem i regnbågens färger, lätt böjd |
| godisring | 2 | `candyring` | ring med stor "pärla", rosa/turkos |

**Leksaker (sex sorter, 2–4 spelpengar)**

| Sort | Pris | Lekzon | Lek |
|---|---|---|---|
| boll | 2 | vänster (bollhörnet) | barnet rullar den till spelaren (som förut) |
| snurra | 2 | vänster (bollhörnet) | barnet sätter den i snurr; spelaren kan lyfta, ställa och släppa den, och den snurrar igång när den landar |
| bil | 3 | mitten (vägen) | körs runt vägen (som förut) |
| leksakståg | 4 | mitten (vägen) | samma väglogik som bilen, långsammare, eget lok med vagn |
| byggkloss-set | 4 | höger (byggzonen) | barnet bygger torn av sex klossar (som förut) |
| stapelringar | 3 | höger (byggzonen) | tre ringar och en pinne; barnet staplar störst först; spelaren kan ta ringar och släppa dem nära pinnen (generös hjälp) |

Varje vara är en vara och ett greppobjekt i ståndet. På bordet är klossar och ringar flera greppobjekt.

## Regler

- **Sortiment:** samma mekanism som frukt (`Assortment`): exakt tre olika aktiva sorter per kategori, ny dragning vid Nytt spel och vid nästa omgång (bytet sker mellan kunder, aldrig medan en mobilorder är öppen). Inga byten under beställning, meny, fokusförlust, omcentrering eller städning.
- **Lekzoner** (`PlayArea`): bordet har tre zoner, och varje leksakssort hör till en av dem. En zon har högst ett barn, alltså högst **tre lekande barn**. En leksaksorder erbjuds bara om dess zon är ledig, och zonen reserveras när ordern skapas (som förut).
- Ett sortimentsbyte **rör aldrig lekytan**: barn som leker med en leksak som inte längre säljs leker klart. Zonen blir ledig först när barnet har gått.
- **Kontroll:** exklusiv som förut (barn, spelare eller fri). Det gäller alla sorter och alla delar (klossar, ringar). Städa lekytan och borttappade leksaker går aldrig åt något som hålls i handen.
- **Fysikbudget:** varje leksak har ett känt antal fysikkroppar: boll 1, snurra 1, bil 1, tåg 1, klossar 6, ringar 3 (pinnen är fast och har ingen kropp). Budgeten är summan av den största leksaken per zon: **1 + 1 + 6 = 8**, samma som förut. Den gäller oavsett sortiment.
- **Krokodilspelet** påverkas inte av sortiment eller städning.
- **Mobilen:** godis kan beställas som förut. `market` innehåller även `candy`, de tre aktiva godisnycklarna, och mobilen visar bara dem. En order på inaktivt godis avvisas med `inactive`. Leksaker kan fortfarande inte beställas från mobilen.

## Stories och acceptanskriterier

### S1 – Kataloger och sortiment

| # | Kriterium | Verifiering |
|---|---|---|
| G1 | Godiskatalogen har åtta sorter (pris 1–3) och leksakskatalogen sex (pris 2–4), med namn; godis har mobilnycklar. Befintliga priser är oförändrade. | [T: Catalogue_CandyAndToys] |
| G2 | Tre olika aktiva sorter i varje kategori, för många frön och dragningar; samma frö ger samma sortiment, och en ny dragning ändrar varje kategori. | [T: Assortment_ThreeDistinctPerCategory] [T: Assortment_RedrawChangesEveryCategory] |
| G3 | NPC-order innehåller bara aktiva sorter i alla kategorier. | [T: Orders_OnlyActiveCandyAndToys] |
| G4 | Alla nya godissorter och leksaker kan säljas till sitt pris. | [T: Sale_EveryNewCandyAndToy] |

### S2 – Lekzoner, kapacitet och budget

| # | Kriterium | Verifiering |
|---|---|---|
| L1 | Varje leksakssort har en zon; en reservation tar zonen, och en annan leksak i samma zon kan inte reserveras förrän den är fri. | [T: Play_ZonePerToyAndReservation] |
| L2 | En leksaksorder erbjuds bara när dess zon är ledig. Aldrig fler än tre barn. | [T: Play_ToyOrderOnlyWithFreeZone] |
| L3 | En pågående lek (alla tillstånd) överlever ett sortimentsbyte; zonen blir fri när barnet gått. | [T: Play_SurvivesAssortmentChange] |
| L4 | Fysikbudgeten är 8 för varje möjligt sortiment, och i scenen överstiger antalet kroppar aldrig budgeten. | [T: Play_PhysicsBudget] [T: Scene_PhysicsWithinBudget] |

### S3 – De nya lekarna (scen)

| # | Kriterium | Verifiering |
|---|---|---|
| P1 | Snurran: barnet bär den till bordet och sätter den i snurr. Spelaren kan ta den (barnet väntar), och när den släpps och landar snurrar den igen; sedan tar barnet över. | [T: Scene_TopSpinsAndPlayerCanJoin] |
| P2 | Tåget körs runt vägen som bilen; spelaren kan lyfta och ställa det, och barnet kör vidare från närmaste punkt. | [T: Scene_TrainDrivesTheRoad] |
| P3 | Stapelringarna: barnet staplar alla tre på pinnen, största först. Spelaren kan ta en ring (ringar ovanpå faller av) och släppa den nära pinnen, så hamnar den överst. | [T: Scene_RingsAreStackedAndPlayerHelps] |
| P4 | Städa lekytan och borttappade leksaker fungerar för alla nya sorter utan att ta något ur handen. | [T: Scene_TidyAndLostForNewToys] |

### S4 – Mobilen

| # | Kriterium | Verifiering |
|---|---|---|
| D1 | `market` har `candy` med de tre aktiva godisnycklarna; relän sparar och vidarebefordrar dem. | [T: Session_MarketCarriesActiveCandy] [T relay: Relay_MarketCandyKeptForNewPhones] |
| D2 | Order på inaktivt godis avvisas med `inactive`. | [T: Remote_InactiveCandyRejected] |
| D3 | Mobilsidan visar bara aktivt godis. | [E] manuellt i webbläsare |

### S5 – Modeller

| # | Kriterium | Verifiering |
|---|---|---|
| M1 | Godis (8) och leksaker (6, plus ring och pinne) är Blender-modeller från skript, med låg polygonmängd och material färgade i koden. Kontaktbild för godis och för leksaker. | [R: art/candy/contact_sheet.png, art/toys/contact_sheet.png] [T: Scene_WaresAreBlenderModels] |
| M2 | Samma modell i lådan, på skylten, i bubblan och, för leksaker, på lekbordet. Kollisionsvolymerna för boll, bil och klossar är oförändrade. | [R: Snapshots] [T: Scene_WaresAreBlenderModels] |
| M3 | Sorterna skiljs åt i headsetet. Snurran, tåget och ringarna går bra att greppa och leka med. | [H] |
| M5 | Bildfrekvensen är jämn (ingen synlig hackning) med alla tre stånd i bild och tre lekande barn. Scenen ritar ≈ 800 delmeshar före batching. | [H] (mätt i Editor: se loggen) |
| M4 | Spelaren säljer de tre aktuella godis- och leksakssorterna. Barnen leker med köpta leksaker, och spelaren kan vara med. Nästa omgång kan ge nytt sortiment utan att pågående lek går sönder. | [P] |

## Verifieringslogg

**Automatiska tester (2026-11-01)**
- Kärnan (`CandyToyTests`) var röd först, med skelett, av rätt skäl. Tre tester gick igenom av fel skäl i skelettet, eftersom de nya godissorterna då räknades som frukt; de blev meningsfulla efter implementationen.
- Lektesterna (`ToyPlaySceneTests`) och modelltestet (`Scene_WaresAreBlenderModels`, där bollen var först att falla) var röda först.
- Reläns `Relay_MarketCandyKeptForNewPhones` var röd först.
- **Felrättning:** ringtestet hittade att en leksak med fasta delar i en stapel aldrig gick tillbaka till barnet efter att spelaren släppt den. Felet fanns redan för klossarna och är rättat i samma kodväg.
- Slutläge: EditMode 169/169, PlayMode 77 godkända + 4 explicita överhoppade, relä 13/13. Alla Unity-körningar gick via `quest.sh`, utan hängning.

**Blender (5.2.3):**
- `art/candy/make_candy.py`: 8 modeller, 2 620 trianglar (största: klubban, 632).
- `art/toys/make_toys.py`: 6 leksaker + ring + pinne, 2 852 trianglar (största: stapelringarna, 904).
- Gemensamma byggstenar i `art/kit.py`. Fruktmodellerna gav identiska trianglar och mått efter utbrytningen.
- Kontaktbilder: `art/candy/contact_sheet.png`, `art/toys/contact_sheet.png`.

**Unity (visuellt):**
- `scripts/quest.sh snapshots`, bild 28–32: godis- och leksaksståndet med de nya sorterna, och lekbordet där tåget kör på vägen och ringarna sitter på pinnen.
- Budget i Editor (`Snapshots_CandyAndToys`): 710 renderare, 807 delmeshar, 168 602 trianglar, 4 leksakskroppar (≤ 8).

**Mobilsidan [E]:** inte prövad i webbläsare. JavaScript-koden parsar, och protokollet är testat.

**Bygge:** `2026-11-01_172620_692c402`, rent, 24 MB, sha256 `b1427e2a…`. **Inte installerat.**

**Headset [H] och spelaren [P]:** inte gjort. Se PLAYTEST del 9.

**Boy Scout:**
- `art/kit.py` bröts ut ur fruktskriptet.
- Två obegränsade `while` i `Snapshots` fick gränser.
- `FruitStand.StartFruit` heter nu `StartWares`.
- `WareInfo.Price`/`Name` har inga dolda standardvärden längre.
- `Art.ModelOf` är Unity-null-säker.

**Kvar:**
- Draw calls på Quest (M5) mäts i headsetet.
- Klossarna på bordet är fortfarande primitiver (som avsett: kollisionsvolymerna är oförändrade); bara setet i ståndet är en Blender-modell.
