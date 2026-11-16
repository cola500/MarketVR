---
title: EPIC-007 – rikta in marknaden mot rummets spelyta (slice)
status: installed-not-tried
last_updated: 2026-11-01
---

# EPIC-007 – rikta in marknaden mot rummets spelyta

Stånden, lekbordet (med krokodilhyllan) och spelets golvmarkering får en gemensam placering och orientering som passar rummets spelyta. Placeringen ändras bara när föräldern uttryckligen kalibrerar och bekräftar. Underlag: [SPIKE-007](SPIKE-007.md). Regler: [SPEC §24](SPEC.md#24-rikta-in-marknaden-mot-spelytan-epic-007).

## Utgångsläge från spiken

- Fyrkanten som syns är **Quests systemgräns**. Spelet ritar ingen egen. Systemgränsen ändras aldrig av appen.
- På Quest 3 ger befintlig stack **bara spelytans mått** (1,71 × 1,86 m), inte form, position eller orientering. Förslaget utgår därför från en rektangel med de måtten, centrerad i spårningsorigo längs spårningsaxlarna **[A, bekräftas av föräldern i förhandsvisningen]**.

## Begrepp och koordinater

- **Spårningsrummet** är det verkliga rummet (golvorigo). Kalibreringen räknas där: centrum (x, z), vridning och tillgänglig bredd × längd.
- **Riggen** flyttas av spakgång och vridning (EPIC-005). Vid bekräftelse omvandlas kalibreringen till världen genom riggens *aktuella* pose: världspunkt = riggen · spårningspunkt, världsvridning = riggens vridning + spårningsvridning. Marknaden hamnar därmed där i det verkliga rummet som föräldern ser i förhandsvisningen, även om spelaren har förflyttat sig med spaken innan.
- **Marknadens rot** bär allt: stånd, lekbord, krokodilhylla, golvavtryck och spelets golvmarkering. Markeringen byggs från samma layout som stånden, så de kan aldrig hamna isär.
- Spakrörelse flyttar riggen, aldrig marknaden eller systemgränsen. Efter spakrörelse står marknaden kvar i världen men inte längre på samma ställe i rummet. Golvmarkeringen visas därför **bara under förhandsvisningen** och sägs aldrig vara ”fritt golv”.

## Layout som anpassas (inte skalas)

- Avståndet mitt–stånd `d` kan vara **0,70–0,90 m** i steg om 0,05. Lekbordet ligger `d + 0,10` bakom. 0,70 är dagens kompakta marknad. Under 0,70 skulle diskarna överlappa. Spelaren, barnen, leksakerna, diskar, varor och krokodilen behåller sin storlek.
- **Ryms** betyder:
  - kroppens yta (den tillgängliga rektangeln minus **0,25 m** marginal till gränsen) innehåller arbetspositionerna;
  - varje nödvändig handinteraktion (lådor, korg, korgplatser, bricka, kassa, Öppet/Stängt, lekbordets närmre del, städknappen, klossarna och krokodilens tänder) ligger högst **0,20 m** utanför kroppens yta.
- För en polygon prövas olika vridningar (polygonens kantriktningar och var 15:e grad) och centrum inne i ytan. Den rektangel som behövs ska ligga **helt inne i polygonen**: alla hörn inne och ingen polygonhörna inne i rektangeln. En omslutande rektangel räcker inte. Störst `d` som ryms väljs, och vid lika väljs centrum närmast ytans tyngdpunkt.
- Ryms inte ens `d = 0,70`: förslaget visas rött med vad som behövs (”behöver minst B × L m”), Bekräfta går inte, och tidigare placering behålls.

## Kalibrering (menyn: Rikta in marknaden)

1. Menyvalet **Rikta in marknaden** öppnar en förhandsvisning. Marknaden står kvar.
2. Finns spelytans mått föreslås en placering med dem. Annars börjar inställningen manuellt: 1,5 × 2,0 m (dagens antagande), centrerad där föräldern står och vriden dit föräldern tittar.
3. Förhandsvisningen visar på golvet: den tillgängliga ytan, kroppens yta, diskarnas, lekbordets och krokodilhyllans avtryck och en pil mot fruktståndet. Grön betyder att det ryms, röd att det inte ryms.
4. Menyrader (spak upp/ned väljer, spak vänster/höger ändrar, avtryckare/A väljer, B avbryter): **Bekräfta**, **Bredd** ±0,1 m, **Längd** ±0,1 m, **Vrid** ±15°, **Mitten där jag står**, **Vrid dit jag tittar**, **Använd systemets spelyta** (om måtten finns) och **Avbryt**. Statusraden visar mått, `d` och om det ryms.
5. **Bekräfta** (bara om det ryms) flyttar hela marknaden och golvmarkeringen på en gång, med vald `d`. Höjden behålls. Order, korg, kassa, lek och krokodilen bevaras.
6. Ingen automatisk omplacering: gång, spakrörelse, att ta av headsetet och `boundaryChanged` flyttar aldrig marknaden. Vid `boundaryChanged` visar menyn ”Spelytan har ändrats – Rikta in marknaden”. Start, omcentrering och Flytta marknaden hit fungerar som förut (med vald `d`).
7. Kalibreringen gäller spelpasset och sparas inte mellan appstarter.

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| R1 | Omvandling spårningsrum → värld via riggens aktuella pose, också efter spakgång och vridning. | [T: Room_ToWorldThroughCurrentRig] |
| R2 | Golvmarkeringen och marknaden har samma rot och samma layout, och markeringens ytor stämmer med ståndens avtryck vid varje `d`. | [T: Scene_MarkingSharesMarketLayout] |
| R3 | Rektangulär yta (också vriden och förskjuten): förslaget ligger i ytans mitt, längs dess kanter, och den behövda rektangeln ligger inne i ytan. | [T: Fit_RectangleCentreAndOrientation] |
| R4 | Spelytan 1,71 × 1,86 m från Questen: det ryms, och förslaget redovisar `d`. | [T: Fit_MeasuredQuestPlayArea] |
| R5 | Oregelbunden yta (L-form, smal korridor): förslaget ligger inne i ytan och aldrig i den omslutande rektangelns tomma del, eller rapporteras som att det inte ryms. | [T: Fit_IrregularLShapeUsesUsableRectangle] [T: Fit_IrregularCorridorTooNarrow] |
| R6 | Saknad gränsdata: ingen gissning. Manuell inställning där föräldern står. | [T: Fit_MissingData] [T: Calib_ManualWhenNoData] |
| R7 | För liten yta: rapporteras med behövda mått, Bekräfta nekas och placeringen behålls. | [T: Fit_TooSmallReportsNeed] [T: Scene_TooSmallKeepsPlacement] |
| R8 | `d` inom 0,70–0,90. Ingen skalning av figurer, varor, leksaker eller möbler. Större yta ger högst 0,90. | [T: Fit_SpacingWithinLimitsNoScaling] |
| R9 | Arbetspositioner och alla nödvändiga handinteraktioner ryms med marginal vid valt `d`. | [T: Fit_InteractionsFitWithMargin] |
| R10 | Förhandsvisning före bekräftelse: marknaden flyttas inte förrän Bekräfta. Avbryt lämnar allt. | [T: Calib_PreviewThenConfirm] [T: Scene_CalibrationMovesWholeMarketOnConfirm] |
| R11 | Manuell justering: bredd, längd, mitt där jag står, vridning ±15° och vrid dit jag tittar. | [T: Calib_ManualAdjustments] |
| R12 | Ingen automatisk omplacering vid gång, spakrörelse, headset av eller `boundaryChanged`. Den sistnämnda föreslår ny kalibrering. | [T: Calib_BoundaryChangeOnlySuggests] [T: Scene_WalkingNeverMovesMarket] |
| R13 | Försäljning, mobilkund, gång/vridning, lek och krokodil fungerar efter kalibrering med annat `d`. | [T: Scene_SellAndPlayAfterCalibration] + befintliga |
| R14 | Menyn: Rikta in marknaden finns, och kalibreringssidan går att styra med spaken. | [T: Menu_CalibrationPage] |
| R15 | APK byggs. | [B] |
| R16 | På Quest: förslaget hamnar på systemgränsen (eller justeras dit), marknaden flyttas först vid Bekräfta, räckvidd och komfort är bra, och inget hoppar vid gång eller headset av. | [H] |

## Manuellt test på Quest

1. Stå i rummet, öppna menyn och välj **Rikta in marknaden**. Gå nära systemgränsen så att den syns. Hamnar den gröna ytan på systemgränsen? Notera avvikelse i läge och vridning.
2. Justera bredd, längd, mitt och vridning. Se att det blir rött när ytan är för liten, och att Bekräfta då inte går.
3. Bekräfta: marknaden flyttas på en gång och står mitt i ytan, med fruktståndet åt pilens håll.
4. Sälj, lek och spela krokodil. Når du allt?
5. Gå med spaken och ta av och på headsetet: marknaden står kvar och ingen markering påstår att golvet är fritt.

## Verifieringslogg

| Datum | Vad | Resultat |
|---|---|---|
| 2026-11-01 | Spike (SPIKE-007) på Quest | Systemgräns konfigurerad, 1,71 × 1,86 m, inga punkter. Se spiken. |
| 2026-11-01 | Slice-specifikation skriven före implementation | – |
| 2026-11-01 | TDD cykel 1: passning och koordinater | `SpaceFitTests` (R1, R3–R9) skrevs först. Rött: typerna saknades. Därefter `SpaceFit`, `RoomPlacement`, och `MarketLayout` med ett gällande avstånd (standard 0,70) samt kollisionsavtryck som följer det. Grönt 18/18, inklusive befintliga layout- och kollisionstester. Questens 1,71 × 1,86 m ger `d` = 0,75 (behov vid 0,75: 1,59 × 1,80 m). |
| 2026-11-01 | TDD cykel 2: kalibrering och meny | `CalibrationTests` (R6, R10–R12, R14) skrevs först. Rött: `Calibration` saknades. Därefter `Calibration`, menysidan och spak vänster/höger i menyn. `Menu_NewGameNeedsConfirmationNoIsDefault` går nu till sista raden i stället för index 6, eftersom en ny rad lagts till; avsikten är densamma. Grönt 24/24. |
| 2026-11-01 | TDD cykel 3: scen | `CalibrationSceneTests` (R2, R7, R10, R13) skrevs först. Rött: `Preview` och `Marking` saknades. Därefter `LayoutMarking`, `ShowPreview`, `HidePreview`, `ApplyPlacement` och `FootprintsAt(d)`. Grönt 4/4. `XrRig` kopplar menyns kalibreringsval och `boundaryChanged`. Den delen läser riktiga kontroller och har inget automatiskt test. |
| 2026-11-01 | Automatiska tester [T] | **EditMode 125/125, PlayMode 60/60** (3 explicita överhoppade), relä 9/9, Editor-kedjan för mobilkunden 2/2 (port 8788, så att den pågående relä-sessionen inte stördes). |
| 2026-11-01 | Renderad bild [R] | `20-calibration-preview`: förhandsvisningen (grön yta, mörka avtryck, pil, svart kontur av den inställda ytan) vriden 15° bredvid marknaden. Efter bilden fick den nuvarande markeringen en egen gråblå ton, eftersom båda var gröna. |
| 2026-11-01 12:54 | APK-bygge [B] | `2026-11-01_125412_34bec1d` (rent). |
| 2026-11-01 12:56 | Installation på Quest 3 | `install`: `Success`, och `installed` visar `1.0+2026-11-01_125412_34bec1d`. Processen körs, men systemets Guardian-ruta låg överst (`ClearActivity`, `GUARDIAN update_state`), så appen hade inte startat sin spelslinga och relän såg ingen Quest. Väntar på att någon tar på headsetet och stänger rutan. |
| – | Headsettest [H] (R16) | **Inte gjort.** |
