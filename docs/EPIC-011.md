---
title: EPIC-011 – förlåtande leksaksfysik
status: built-not-installed
last_updated: 2026-11-01
---

# EPIC-011 – förlåtande leksaksfysik

**Mål:** leksakerna tål ungefärliga rörelser från en fyraåring. Spelaren ska inte behöva undvika överlapp exakt eller förstå fysik. Ingenting försvinner, faller genom bordet eller skjuts iväg för att det släpptes "fel". Regler: [SPEC §28](SPEC.md#28-förlåtande-leksaksfysik-epic-011).

## Rapporterad bugg

"När en byggkloss släpps överlappande en annan kloss försvinner den ibland. Släpps den en bit ovanför fungerar det bättre."

**Grundorsak** (ur koden, bekräftas av regressionstesterna nedan):

0. **Klossen i handen knuffade undan andra** (hittat av regressionstestet). En hållen leksak var kinematisk men hade kvar sin kollider. När handen fördes in i en annan kloss knuffades den klossen undan redan före släppet, och då hamnade även en lyckad hjälp på fel ställe.
1. **Staplingshjälpen gav upp vid djupt överlapp.** `StackHelp.Snap` hoppade över fall där klossens underkant låg mer än 30 % av kantlängden *under* överkanten på klossen nedanför. Klossen släpptes då som fri fysik inuti en annan kloss. Fysikmotorn tryckte isär dem med `maxDepenetrationVelocity` på standardvärdet 10 m/s, så klossen sköts iväg, ofta över kanten.
2. **Regeln för borttappade leksaker kunde aldrig slå till för klossar.** Räknaren (`Child.Lost`) var gemensam för barnets alla delar, och varje kloss som var fast i tornet (kinematisk) eller i handen nollställde den varje bildruta. En kloss på golvet kom därför aldrig tillbaka så länge tornet hade en kloss. Den "försvann".
3. Släpp delvis inne i bordsskivan gav samma isärtryckning mot bordet.

## Regler (förlåtande släpp)

- **Klossar:** ett släpp som överlappar en annan kloss, eller ligger nära ovanför den, hamnar ovanpå den stapeln, om platsen där är ledig. Annars hamnar klossen på närmaste lediga plats på bordet nära släppet (högst 0,15 m bort, aldrig över bordet). Finns ingen hamnar den där den låg senast innan spelaren tog den. Överlapp kontrolleras för klossens hela volym. Andra klossar flyttas aldrig, och inget tas ur handen.
- **Alla leksaker:** ett släpp ovanför bordsskivan som delvis ligger inne i bordet eller kanten lyfts upp och in på bordet. Ett släpp som tydligt är utanför bordet får falla, och regeln för borttappade leksaker tar tillbaka det.
- **Fysikgränser:** isärtryckning högst 0,3 m/s, vinkelhastighet högst 20 rad/s, släppfart högst 1,2 m/s (som förut). Riggens rörelse ger ingen släppfart (som förut).
- **Borttappat:** varje del har sin egen tidsräknare. En del som legat på golvet i 2 s går tillbaka till sin plats, eller till närmaste lediga plats om den är upptagen. Det som hålls återställs aldrig.
- **Ringar:** ett släpp inom 8 cm från pinnen hamnar på pinnen (som förut).
- **Bil och tåg:** släpps de var som helst på bordet tar barnet över från närmaste punkt på vägen (som förut).
- **Hålls aldrig av två:** en leksak i handen kolliderar inte, så den knuffar inte undan andra. Under barnets korta flytt (0,5 s) går leksaken inte att greppa, så barnet och spelaren styr den aldrig samtidigt (som förut).

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| F1 | En kloss som släpps delvis inuti en annan hamnar stabilt ovanpå den och ligger kvar efter flera fysiksteg; ingen kloss rör sig, alla sex finns kvar och ingen överlappar en annan. | [T: Scene_BlockReleasedInsideAnotherLandsOnTop] |
| F2 | En kloss som släpps delvis inne i bordsskivan hamnar på bordet. | [T: Scene_BlockReleasedInsideTableLandsOnIt] |
| F3 | En kloss på golvet kommer tillbaka även när tornet har fasta klossar. | [T: Scene_LostBlockReturnsWhileTowerStands] |
| F4 | Placering (ren logik): ovanpå vid överlapp; närmaste lediga plats om toppen är blockerad; senast kända plats om inget är ledigt; hela volymen kontrolleras; ingen placering utanför bordet. | [T: Placement_*] |
| F5 | Övriga leksaker (boll, snurra, bil, tåg, ringar): ett släpp delvis inne i bordet hamnar på bordet; leksaken skjuts inte iväg (fart under gränsen) och finns kvar. | [T: Scene_EveryToySurvivesASloppyRelease] |
| F6 | Ett lågt, snabbt släpp ger högst 1,2 m/s. Riggens rörelse ger ingen släppfart. | [T: Scene_FastReleaseIsCapped] [T: Scene_WalkWithHeldItemNoThrow] (befintligt) |
| F8 | Släpp vid bordskanten: in på bordet nära släppet. Spelaren tar en kloss ur barnets torn, och barnet tar över igen utan trasiga torn. Höjdjustering med lösa leksaker. | [T: Scene_BlockNearTheEdgeStaysOn] [T: Scene_TakeBlockFromTheChildsTower] [T: Scene_HeightChangeKeepsLooseToys] |
| F7 | Headset: klossar som släpps rakt i varandra bildar ett torn; släpp vid bordskant och delvis i skivan; leksaker tas från barnet och lämnas tillbaka; hjälpen känns förutsägbar och inget försvinner. | [H] |

## Verifieringslogg

**Reproduktion (röd före rättning, 2026-11-01):**
- `Scene_BlockReleasedInsideAnotherLandsOnTop`: klossen låg *bredvid* på bordet i stället för ovanpå. Efter placeringsrättningen låg den på rätt höjd men 5,7 cm fel, och testet "den andra klossen flyttades inte" visade då grundorsak 0.
- `Scene_LostBlockReturnsWhileTowerStands`: när spelaren håller en annan kloss låg klossen kvar på golvet, (−0,27; −0,69; −1,01). Den första versionen av testet var grön av fel skäl, eftersom barnet själv hämtade klossen. Testet skärptes så att barnet väntar.
- `BlockPlacementTests`: röda mot skelett.
- `Stack_NoSnapWhenFar` krävde tidigare uttryckligen *ingen* hjälp när klossen släpps inuti en annan. Det var buggen, inskriven som förväntat beteende. Fallet är ändrat.

**Gröna redan före rättning:**
- `Scene_BlockReleasedInsideTableLandsOnIt` (bordet tryckte upp klossen i det fallet).
- `Scene_EveryToySurvivesASloppyRelease`.
- `Scene_FastReleaseIsCapped`, `Scene_BlockNearTheEdgeStaysOn`, `Scene_HeightChangeKeepsLooseToys`, `Scene_TakeBlockFromTheChildsTower`.

De låser nu beteendet. Den generella lyftningen och fysikgränserna är försiktighetsåtgärder.

**Slutläge:** EditMode 177/177, PlayMode 85 godkända + 5 explicita, relä 13/13. Alla körningar gick via `quest.sh`, utan hängning.

**Rättningar:**
- `BlockPlacement` ersätter `StackHelp`.
- En leksak i handen kolliderar inte.
- Tid på golvet räknas per del.
- En återställd kloss hamnar på närmaste lediga plats.
- Generell lyftning in på bordet.
- Isärtryckning högst 0,3 m/s och vinkelhastighet högst 20 rad/s.
- Loggrad `[Fruktstand] block placed: …` för headsettestet.

**Granskning av övriga leksaker:**

| Leksak | Grepp / släpp | Barnet | Återställning | Status |
|---|---|---|---|---|
| boll | fart högst 1,2 m/s; riggrörelse ingen fart | rullar, hämtar efter 10 s | hem (bollhörnet) | ok, test |
| snurra | landar upprätt; snurrar igen på bordet | snurrar bara på bordet (rättat i EPIC-010) | hem | ok, test |
| bil / tåg | landar upprätt, glider inte i sidled | tar över från närmaste vägpunkt | hem på vägen | ok, test |
| klossar | `BlockPlacement` | bygger torn | närmaste lediga plats | rättat |
| ringar | pinnen inom 8 cm | staplar största först | hem i högen | ok, test |

**Större fynd, inte åtgärdade (dokumenteras, ingen ombyggnad):**
- Placeringen jämför klossar som axelriktade kuber. En vriden kloss (upp till 45°) kan sticka ut ≈ 1 cm, men överlappet trycks då isär varsamt (≤ 0,3 m/s).
- Den generella lyftningen tar inte hänsyn till *andra* leksaker. Det gäller bara när leksaker från olika zoner släpps i varandra och ger då samma varsamma isärtryckning.
- Barnets kinematiska flytt av en kloss till tornet kan knuffa lösa klossar i vägen.
- Placeringshjälp **före** släpp (en skugga eller markering) finns inte. Det nuvarande systemet saknar genomskinliga material och markerar inte leksaker (det skulle störa kolliderarna). Det är ett förslag till en egen slice om headsettestet visar behov.

**Bygge:** `2026-11-01_173907_1df05da`, rent, 24 MB, sha256 `fbd46af2…`. **Inte installerat.**

**Headset [H]:** inte gjort. Följ F7: släpp klossar rakt i varandra och bygg ett litet torn; släpp vid bordskanten och delvis i skivan; ta leksaker från barnet och lämna tillbaka dem; prova alla leksaker med ungefärliga rörelser. Placeringarna syns i `scripts/quest.sh log | grep "block placed"`.
