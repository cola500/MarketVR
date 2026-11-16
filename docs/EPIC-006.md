---
title: EPIC-006 – krokodilspelet vid lekbordet (slice)
status: headset-tried (beröring som förslag pausat; barnets test återstår)
last_updated: 2026-11-01
---

# EPIC-006 – krokodilspelet vid lekbordet

En liten slice. En vänlig leksakskrokodil står alltid vid lekbordet, oberoende av kunder, försäljning och köpta leksaker. Den har öppen mun och åtta tänder att trycka ned. En slumpad tand får munnen att slå igen. Efter fem snäpp dansar krokodilen en gång, som en dold överraskning. Regler: [SPEC §23](SPEC.md#23-krokodilspelet-epic-006).

Försäljning, mobilkund, gång och vridning (EPIC-005) och barnens lek är oförändrade. Inga pengar, inga beställningar, ingen ny knapp, ingen meny och ingen egen scen.

## Beslut

- **Plats:** en egen liten hylla på lekbordets ände vid godisståndet, på spelarens sida. Lekbordets skiva är redan full (boll, väg, klossar, städknapp), och barnens gångväg går längs bordets andra ände. Hyllan sitter på bordsskivan, följer höjdjusteringen och får ett eget golvavtryck för gången. Krokodilen ligger på längden med nosen mot arbetsytan. Munnen öppnas uppåt och tänderna trycks ned ovanifrån.
- **Tryck:** samma handgrepp som städknappen (grepp eller avtryckare nära tanden, ett nytt tryck krävs). Tanden närmast handen lyser som andra saker. En nedtryckt tand kan inte tryckas igen i samma omgång.
- **Regler i ren logik** (`Core/CrocodileGame`) med injicerbar slump. Presentationen (`CrocodileView`) bara visar.
- Tider **[A, prövas på headset]**: stängning 0,15 s, stängd paus 1,5 s, dans 2,5 s och öppning 0,5 s.
- **Ny aktivering:** en hand som tryckt en tand måste lämna den tandens tryckyta innan den kan trycka den igen. Vid återöppning och efter fokusförlust räknas alla tänder handen redan är vid som spärrade tills handen lämnat dem.
- **Paus:** spelmeny, systemmeny, fokusförlust, paus och headset av blockerar tryck och stoppar krokodilens tidur.
- **Ljud och haptik:** ett mjukt ”klack” (syntetiserat, lågt) och en kort vibration i handen som utlöste snäppet. Säkra tänder ger samma lätta klick och vibration som städknappen.
- Barnen spelar inte. Krokodilen har inga fysikkroppar och knuffar ingenting.

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| K1 | Vid start och vid varje ny omgång är munnen öppen och exakt en av åtta tänder är utlösande, slumpad på nytt varje omgång (samma tand får komma igen). | [T: Croc_OneTriggerToothPerRound] [T: Croc_NewTriggerEachRound] |
| K2 | En säker tand sjunker ned och förblir nedtryckt. Varje tand kan tryckas en gång per omgång. | [T: Croc_SafeToothPressedOnce] |
| K3 | Den utlösande tanden stänger munnen och avslutar omgången exakt en gång. | [T: Croc_TriggerSnapsOnce] |
| K4 | Under stängning, stängd paus, dans och öppning ignoreras tandtryck. | [T: Croc_PressesIgnoredWhileLocked] |
| K5 | Efter pausen öppnas munnen av sig själv, tänderna återställs och en ny utlösande tand slumpas. | [T: Croc_ReopensAndResets] |
| K6 | En hand som tryckt en tand måste lämna dess tryckyta före nästa aktivering, även efter återöppning. En hand som ligger kvar trycker aldrig igen. | [T: Croc_HandMustLeaveToothBeforeNextPress] [T: Croc_HandOnToothWhenReopenedMustLeaveFirst] |
| K7 | Efter femte snäppet dansar krokodilen 2,5 s, men först när ingen hand är i munområdet. Sedan står den exakt i sin grundposition och öppnar för nästa omgång. Dansen sker en gång per spelpass, och vanligt spel fortsätter efteråt. | [T: Croc_DancesOnceAfterFiveSnaps] [T: Croc_NoDanceWhileHandInMouth] [T: Scene_CrocDanceEndsInBasePose] |
| K8 | Nytt spel återställer räknaren och överraskningen. Städa lekytan och Återställ beställningen gör det inte. | [T: Croc_NewGameResetsSurprise] [T: Scene_CrocResetRules] |
| K9 | Systemmeny, spelmeny och fokusförlust blockerar tryck och pausar tiduret. Efter återkomst trycker en hand som ligger kvar inte. | [T: Croc_PausedBlocksPressesAndTimer] [T: Croc_ResumeRequiresLeavingTooth] |
| K10 | Krokodilen finns alltid vid lekbordet och finns kvar när barn kommer och går och när lekytan städas. Ingen räknare visas. | [T: Scene_CrocAlwaysThere] |
| K11 | Riktiga händer i scenen: säkra tänder sjunker, den utlösande slår igen munnen (klack och haptik), och efter fem snäpp kommer dansen. Försäljning, kassa och lek påverkas inte. | [T: Scene_CrocPlayedWithHands] |
| K12 | Hyllan och krokodilen blockerar inte boll, bil, klossar, barn eller barnens gångväg. Alla tänder nås sittande från närmaste plats gången tar en till (0,45 m). Hyllan följer höjdjusteringen. | [T: Layout_CrocOwnPlaceReachable] [T: Scene_CrocFollowsHeight] |
| K13 | Käften är bara visuell: den fångar, flyttar och knuffar inte handen eller något annat. | [T: Scene_CrocPlayedWithHands] (handen och leksakerna oförändrade) [H] |
| K14 | I headset: krokodilen syns och känns vänlig, tänderna är lätta att nå sittande, snäppet är lekfullt och inte skrämmande, ljudet är lågt, och dansen är rolig. | [H] [P] |
| K15 | APK byggs enligt byggflödet. | [B] |

## Förslag efter headsettest: tänderna trycks ned genom beröring (2026-11-01) – pausat

**Fynd:** föräldern efter headsettestet: Spelaren ska slippa trycka på knappen. Tanden ska åka ned när man rör den.

**Pausat 2026-11-01:** föräldern vill först låta spelaren prova det nuvarande bygget (`…_121238_8baa430`), där tanden trycks ned med grepp eller avtryckare, eftersom hon redan kan det handgreppet. Beslut om beröring tas efter hennes test. Testerna för K16–K19 är skrivna men inte committade: de finns i `git stash` (”EPIC-006 K16-K20: touch-to-press tests …”). Ingen produktionskod är ändrad.

**Föreslagen ändring:** en tom hand som rör en tand trycker ned den, utan grepp eller avtryckare. Beröring betyder att handens grepp-punkt kommer inom 4 cm från tanden. För att samma tand ska kunna tryckas igen måste handen först vara mer än 6 cm från den. Det är samma princip som skylten Öppet/Stängt. En hand som bär något trycker inte. Grepp vid tänderna gör ingenting, och tänderna kan inte tas. Rör handen två tänder samtidigt trycks den närmaste. Övriga regler (K1–K15) gäller oförändrat.

| # | Kriterium | Verifiering |
|---|---|---|
| K16 | En tom hand som rör en tand trycker ned den, utan knapp. Den utlösande tanden snäpper på samma sätt. | [T: Scene_CrocToothGoesDownOnTouch] |
| K17 | En hand som ligger kvar trycker inte igen. Den måste ut förbi 6 cm från tanden först. Att röra sig längs raden trycker en tand i taget, den närmaste. | [T: Scene_CrocToothGoesDownOnTouch] [T: Croc_HandMustLeaveToothBeforeNextPress] |
| K18 | En hand som bär något trycker inte, och grepp vid tänderna tar ingenting. | [T: Scene_CrocToothGoesDownOnTouch] |
| K19 | En hand som dyker upp mitt på en tand (spårningen tillbaka) trycker inte förrän den lämnat tanden. | [T: Croc_HandAppearingOnToothDoesNotPress] |
| K20 | I headset: tänderna går ned lätt när spelaren rör dem, utan oavsiktliga tryck när hon sträcker sig efter annat. | [H] [P] |

## Manuellt acceptanstest

Spelaren hittar krokodilen vid lekbordet, trycker ned tänder tills den snäpper, kan spela igen direkt efter återöppning och upptäcker dansen efter fem snäpp. Notera särskilt K14: räckvidd sittande, om snäppet skrämmer, ljudnivån och om dansen märks och är rolig.

## Verifieringslogg

| Datum | Vad | Resultat |
|---|---|---|
| 2026-11-01 | Slice-specifikation skriven före implementation | – |
| 2026-11-01 | TDD, regler (`CrocodileTests`) | Fyra cykler, varje test kört och sett misslyckas före koden. (1) `Croc_OneTriggerToothPerRound`, `Croc_SafeToothPressedOnce`: rött (klassen saknades), grönt. (2) `Croc_TriggerSnapsOnce`, `Croc_PressesIgnoredWhileLocked`, `Croc_ReopensAndResets`, `Croc_NewTriggerEachRound`: rött (`Tick`, `Snaps`, `Round` saknades), grönt. (3) `Croc_DancesOnceAfterFiveSnaps`, `Croc_NoDanceWhileHandInMouth`, `Croc_NewGameResetsSurprise`: rött, grönt. (4) `Croc_HandMustLeaveToothBeforeNextPress`, `Croc_HandOnToothWhenReopenedMustLeaveFirst`, `Croc_PausedBlocksPressesAndTimer`, `Croc_ResumeRequiresLeavingTooth`: med en tom `SetPaused` föll alla fyra på beteendet (t.ex. ”Expected: Ignored, But was: Snap”), sedan grönt. 13/13. |
| 2026-11-01 | TDD, scen och layout | `Layout_CrocOwnPlaceReachable` och fem scentester (`Scene_CrocAlwaysThere`, `Scene_CrocPlayedWithHands`, `Scene_CrocDanceEndsInBasePose`, `Scene_CrocResetRules`, `Scene_CrocFollowsHeight`) skrevs före `CrocodileView` och hyllan. Rött: typerna saknades. Därefter grönt. **Fel hittat av testerna:** tänderna gick inte att trycka förrän en bildruta efter att munnen öppnats, eftersom `Grabbable` sattes före krokodilens `Tick`. Rättat genom att krokodilen uppdateras först. Den första samlade PlayMode-körningen hängde efter det misslyckade testet och stoppades. Därefter kördes testerna ett och ett med tidsgräns. `Scene_NoTeleportOrStationButtons` uppdaterad: tänderna är tillåtna tryckbara leksaker, och testets syfte (inga station- eller teleportknappar) är kvar. `Collision_StopsAtFurniture` hoppar över hyllan, som inte nås rakt och testas i sitt layouttest. |
| 2026-11-01 | Automatiska tester [T] | **EditMode 109/109** (13 nya regeltester + layouttestet), **PlayMode 56/56** (5 nya; 3 explicita överhoppade), relä 9/9, Editor-kedjan för mobilkunden 2/2. |
| 2026-11-01 | Renderade bilder [R] | `17-crocodile-open`, `18-crocodile-one-tooth-down` och `19-crocodile-snapped`: krokodilen på hyllan vid bordets ände bredvid godisståndets kassa, gapet uppåt, åtta vita tänder och en nedtryckt tand. Stängd med ögonen synliga. Räckvidd, ljud och känsla är inte bedömda (K14, headset). |
| 2026-11-01 12:12 | APK-bygge [B] | `2026-11-01_121238_8baa430` (rent, 24 MB, `com.lillafruktstandet.prototype`, versionCode 1, Unity 6000.4.7f1). Inte installerat. |
| 2026-11-01 12:14 | Installation och start på Quest 3 (`QUEST-SERIAL`, USB) | `install`: `Success`, utan avinstallation (före: `…_114909_c276bc1`). `installed` visar `versionName=1.0+2026-11-01_121238_8baa430`. `launch`: appen startade, loggen visar bygg-id och att marknaden placerades (ögonhöjd 0,58 m: headsetet låg ned). Inga undantag. Bara via ADB: krokodilen är inte sedd i headset. |
| 2026-11-01 | Headsettest, bygge `…_121238_8baa430` | Föräldern har provat: önskar att tanden åker ned vid beröring så att spelaren slipper trycka (→ K16–K20). Övriga iakttagelser (räckvidd, snäpp, ljud, dans) är inte rapporterade. |
| – | Speltest [P] | **Inte gjort.** |
