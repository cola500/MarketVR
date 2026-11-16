---
title: EPIC-013 – en mysig park runt marknaden
status: installed-measured-not-playtested
last_updated: 2026-11-02
---

# EPIC-013 – en mysig park runt marknaden

Regler: [SPEC §30](SPEC.md#30-parken-runt-marknaden-epic-013). Ingen ny spelmekanik.

## Mål

Marknaden ska kännas som en liten mysig park, och spelaren ska lika lätt hitta varorna, nå allt och sälja och leka som förut. Marknaden och lekbordet är mitten, och parken ramar in dem.

## Visuell slice-specifikation

- **Träd:** fem rundade low-poly-träd i en ring 3,4–4,0 m från mitten. Två står på var sin sida om gången framåt, ett på varje sida och ett bakom. Ingen skog.
- **Buskar och blommor:** grästuvor vid trädens fötter och vid bänken, två stenar och sex låga blomrabatter (sju blommor var) utanför gången, mellan kundernas vägar.
- **Bänk:** en parkbänk vid leksakssidan (x ≈ −2,65), vänd mot marknaden.
- **Grusgång:** en ring av sandfärgat grus under kundernas och barnens vägar (1,0–1,85 m från mitten), med en arm framåt bakom fruktståndet och en bakåt där barnen går hem. Gången är platt (1,5 mm över gräset) och ligger under kalibreringsmarkeringen. Arbetsytan är fortfarande gräs.
- **Mark och himmel:** samma soliga procedurella himmel. Gräsmattan är lite friskare grön. Under träden och bänken ligger mjuka skuggfläckar i stället för realtidsskuggor.
- **Formspråk:** paketets mjuka pastellfärger och runda kronor. Blommorna är runda klot, som frukterna.
- **Fritt:** arbetsytan, diskarna, lekbordet, krokodilhyllan, kundernas och barnens vägar (vid alla avstånd 0,70–0,90) och gångarmarna. Ingen dekor inom 1 m utanför ytan där kroppen får vara.
- **Ingen terräng:** paketets markbitar (upphöjda öar, 0,3–1,6 m) används inte.
- **Fågelljud:** inte med. Inget lämpligt licensierat ljud fanns, och ljud var inget krav.

## Integration

- Parken byggs i runtime som barn till marknadens rot (`ParkView` från `ParkLayout`). Den följer därför start, omcentrering, *Flytta marknaden hit* och kalibrering, men aldrig gång eller huvudrörelse.
- Höjdjusteringen flyttar bara diskar och lekbord. Parken står på marken.
- Inga kolliders, ingen skuggkastning och inget greppbart. Menystrålar, grepp och leksaksfysik påverkas inte.
- **Spakgång** stoppas vid träd, bänk och stenar med samma regel som vid diskarna (`MarketCollision.Obstacles`). Blommor och gräs går man igenom. Kalibreringens markering och passningsregler använder fortfarande bara marknadens egna avtryck.

## Assetförteckning

| Del | Källa | Licens | Hur |
|---|---|---|---|
| 4 träd (`spherical tree`, `tree round foliage`, `tree oak`, `deciduous tree`), bänk (`bench 1`), gräs (`grass 3`, `grass 4`), stenar (`stone 1`, `stone 3`), materialet `mat main` och paletten `texture main.png` (256 px) | Unity Asset Store: [Low Poly Environment Park](https://assetstore.unity.com/packages/3d/environments/low-poly-environment-park-242702), Palmov Island, v1.0.0, gratis | Standard Unity Asset Store EULA (tillägg). Får finnas i appen, men källfilerna committas inte. | `scripts/park-assets.sh` packar upp **bara** de 11 filerna (med paketets `.meta`) från den lokala Asset Store-cachen till `UnityProject/Assets/Palmov Island/` (gitignorerad). Inga demoscener, prefabs, ProjectSettings eller shaders följer med. |
| Grusgång, skuggfläckar, blomrabatter | egen kod (`ParkView`) | projektets | procedurella meshar och färger |

**Granskning (2026-11-02):** paketet innehåller 50 FBX (10–972 trianglar), 50 prefabs, 6 demoscener, ett material på den inbyggda **Standard**-shadern (opak, ingen glans, ingen metall) och en palettextur. Det har inga skript, inga shaders, inga ProjectSettings och inga paketberoenden. Render pipeline ändras inte. FBX-noderna har 90° rotation och skala 0,01. `SceneSetup` sparar nodens transform tillsammans med meshen, och paketets filer ändras inte. Paketet saknar blommor och platt grus.

**Utan paketet** (ren klon utan `scripts/park-assets.sh`) bygger `SceneSetup` scenen utan parkmodeller och varnar om det. Gången och blommorna finns kvar, och spelet fungerar som förut.

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| P1 | Ingen dekor på eller nära kundernas och barnens vägar (marginal 0,35 m) vid avstånd 0,70, 0,80 och 0,90. | [T: Park_KeepsRoutesFree] |
| P2 | Ingen dekor i eller nära stånd, lekbord, krokodilhylla eller ytan där kroppen får vara (minst 1 m utanför). | [T: Park_KeepsMarketAndWorkAreaFree] |
| P3 | Kunderna och barnen går på grusgången. Arbetsytan är gräs. | [T: Park_CustomersWalkOnTheGravelPath] |
| P4 | Träd, bänk, stenar och blommor står bredvid gången, inte på den. | [T: Park_TreesStandOffThePath] |
| P5 | Spakgång stoppas vid träd, bänk och stenar och går aldrig in i dem. | [T: Walk_StopsAtParkTreesBenchAndStones] |
| P6 | Parken följer marknadens placering och kalibrering, men inte diskhöjden. Gången ligger platt på marken. | [T: Park_FollowsPlacementNotCounterHeight] |
| P7 | Inga kolliders och inga realtidsskuggor i parken. | [T: Park_HasNothingToGrabOrHit] |
| P8 | Regression: försäljning, menyer, gång, höjd, lek, kalibrering och fest som förut. | hela `scripts/quest.sh test` |
| P9 | Varor, bubblor och skyltar har kvar tydlig kontrast mot parken. Parken ramar in utan att skymma. | [R: Snapshots_Park] |
| P10 | Headset: mysigt intryck, inget i vägen, varorna lätta att hitta. Strålar och grepp opåverkade. | [H] [P] |
| P11 | Quest: jämn bildfrekvens (72 FPS) med kund, tre lekande barn och leksaksfysik, jämfört med baslinjen. | [D] (mätning på headset, se nedan) |

## Prestanda

Budgetmått från Editor (`Snapshots_Park`: kund vid fruktdisken, två lekande barn, 7 leksakskroppar). Det är **inte** uppmätt prestanda, bara vad scenen ritar före batching:

| | Renderare | Delmeshar | Trianglar |
|---|---|---|---|
| Före (gamla buskar, `17dfdb0`) | 653 | 771 | 165 362 |
| Efter (park) | 629 | 753 | 152 507 |

Antalet blev lägre. De sju gamla buskarna och 21 blommorna var Unitys sfär (768 trianglar var). Parken ritar nu fem objekt: paketmodellerna ihopslagna till en mesh (ett material), grusgången, skuggfläckarna, blomrabatterna (ett material per färg, 7) och ingen skuggpassage.

**Headset:** mäts med `VrApi`-raderna i logcat (FPS, GPU %, CPU/GPU-nivå) under 2 minuter med kund och lekande barn. Först baslinjen med `2026-11-01_180221_42924a2` (samma körkod som före parken), sedan med parkbygget. Resultat i verifieringsloggen.

## Verifieringslogg

**Före-bilder (2026-11-02):** `Snapshots_Park` på `17dfdb0` (utan park): spelarvyer mot fruktståndet, godiset, leksakerna och lekbordet, plus nära och vid översikt.

**Röda före implementation (2026-11-02):**
- `ParkTests` (EditMode): `Park_KeepsRoutesFree`, `Park_CustomersWalkOnTheGravelPath` och `Walk_StopsAtParkTreesBenchAndStones` föll mot ett tomt skelett (inga föremål, ingen gång, inget hinder).
- `ParkSceneTests` (PlayMode): båda föll på att marknaden saknade park.

**Gröna (2026-11-02):** `ParkTests` 5/5 och `ParkSceneTests` 2/2.

**Iakttagelse:** `TEST_FILTER=Park` matchade även det explicita `Snapshots_Park`, som då renderade utan grafikenhet och kraschade Unity (segfault i skuggrenderingen). Snapshots-testerna hoppas nu över utan grafikenhet (Boy Scout).

**Bygge:** `2026-11-02_084346_eb93c01`, rent, 24 MB, sha256 `44772962…`. Snapshots-bilderna efter ändringen är tagna på samma commit.

**Quest 3, mätning [D] (2026-11-02, föräldern spelade, trådlös ADB, `scripts/quest.sh perf 60`).** Båda mätningarna gjordes under vanligt spel med kunder vid alla tre stånd och lekande barn. Talen är systemets `VrApi`-rader, en per sekund. GPU-belastning i procent finns inte i de raderna, bara klocknivåer.

| | Bygge | Försäljning under mätningen | FPS medel / min (mål 72) | s under mål | Eftersläpande bildrutor | CPU/GPU-nivå max |
|---|---|---|---|---|---|---|
| Före (ingen park) | `2026-11-01_180221_42924a2` (08:53:00–08:54:05) | frukt, godis, leksak (boll); tåg sålt strax innan | 72,5 / 71 | 0 | 41 (39 av dem i en sekund) | 4/3 |
| Efter (park) | `2026-11-02_084346_eb93c01` (08:55:39–08:56:39) | frukt ×2, godis ×2, leksak ×2 (bil, stapelringar) | 72,6 / 72 | 0 | 0 | 4/3 |

- Parken gav ingen mätbar kostnad: full bildfrekvens hela minuten och samma klocknivåer. Det enstaka rycket i baslinjen kom inte tillbaka.
- **Begränsning:** parkbygget startade ett nytt spel, så barnen kom till bordet under mätningen (bil 08:56:00, ringar 08:56:29). Tre barn samtidigt mättes inte i något av fallen. Unity-loggen hade 0 fel eller exceptions.
- Parkbygget är installerat (bekräftat med `scripts/quest.sh installed` och appens logg). **Inte gjort:** PLAYTEST del 12 med spelaren, och [H]-kontroll av spakgång mot träd och bänk, höjdjustering, strålar och kalibrering på headset.
