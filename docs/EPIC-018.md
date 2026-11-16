---
title: EPIC-018 – hoppgrodor
status: installed-not-tried
last_updated: 2026-11-16
---

# EPIC-018 – hoppgrodor

**Mål:** en ny leksak, ett grodset med två grodor. Barnet köper setet, visar ett hopp vid lekbordet och låter sedan spelaren prova med sin egen groda. Regler: [SPEC §34](SPEC.md#34-hoppgrodor-epic-018). Bygger på EPIC-010 (sortiment och lekzoner) och EPIC-017 (turordning). Inga nya paket. Fysikbudgeten är oförändrad: grodorna hoppar i beräknade bågar och har inga fysikkroppar.

## Slice

### Hela kedjan
1. **Vara:** `Ware.Frogs` (*hoppgrodor*, 3 mynt) är en leksak bland nu **sju**. Den finns i sortimentet när den dras som en av de tre leksakerna.
2. **Lekzon:** vänster (som boll och snurra), alltså högst ett grodbarn i taget. Inga fysikkroppar (`BodiesOf` = 0).
3. **Från order till lekbord:** som de andra leksakerna. Barnet beställer setet, spelaren lägger det i korgen och tar betalt, och barnet bär det till lekbordet. Där blir setet en dammatta, ett stort näckrosblad och två grodor:
   - **barnets groda** (orange), på barnets sida, vänd mot bladet;
   - **spelarens groda** (grön), på spelarens sida, vänd mot bladet.
4. **Testscenario:** `SceneLoad.Fresh(..., wares)` med grodorna bland leksakerna gör dem tillgängliga direkt. Tester och `PlaySequences_Frogs` använder det.

### Leken (`Core/FrogPond`, ren logik)
1. **Ett hopp per tryck:**
   - Ett grepp på grodans bakdel (gripknappen nära bakdelen) trycker ned den, och grodan fjädrar ihop.
   - När handen släpper hoppar den, en gång. Ett släpp utan tryck hoppar inte.
   - En groda i luften kan inte tryckas.
2. **Hoppet:** en båge **16 cm** framåt i grodans riktning, 7 cm hög, 0,5 s lång.
   - Hemmaplatserna ligger exakt ett hopp från bladets mitt, så ett första tryck utan att sikta landar på bladet.
   - **Generöst mål:** landar grodan inom bladets radie (8 cm) + 5 cm räknas det som på bladet, och den hamnar på bladet.
3. **Sikta:** spelaren kan lyfta sin groda (grepp om kroppen) och vrida handen. Grodan vänds då åt samma håll och sätts ned på bordet där den släpps, med den riktningen.
4. **På bladet:** ett kort kvack och en liten ring på vattnet.
5. **Miss:**
   - Landar grodan bredvid bladet ligger den kvar och kan plockas upp.
   - Hamnar den utanför bordet faller den ned och kommer tillbaka till sin hemmaplats efter 2 s (som tappade leksaker).
6. **Turordning:**
   - Barnet visar först ett hopp (efter 2,5 s), hämtar sin groda och väntar sedan på spelaren i upp till **8 s**. Tiden står still medan spelaren håller eller trycker på en groda.
   - När spelaren har hoppat hoppar barnet igen efter 2,5 s.
   - Spelaren får hoppa när som helst, i sin egen takt. Barnet väntar då ändå 2,5 s efter spelarens senaste hopp.
7. **Barnet tar aldrig en groda som spelaren håller.** Barnet rör bara sin egen groda, och bara när ingen hand håller eller trycker på den.
8. **Paus, fokus, meny och vilopaus:** ett pågående tryck avbryts utan hopp. Efteråt krävs ett nytt tryck (som för alla grepp). *Städa lekytan* ställer båda grodorna hemma.

### Utseende
- En egen stiliserad groda (`art/toys/make_toys.py`): bred kropp, stora ögon och en tydlig fjäder under bakdelen.
- Samma groda finns i butiken, i orderbubblan (`Frogs.fbx`, två grodor på ett litet blad) och i leken (`Frog.fbx`, en groda färgad per ägare).

## Acceptanskriterier

| ID | Kriterium | Verifiering |
|---|---|---|
| G1 | Ett hopp per tryck och släpp; släpp utan tryck och tryck i luften ger inget | [T: Frog_OneHopPerPressAndRelease, Frog_ReleaseWithoutPressDoesNothing, Frog_NoPressInTheAir] |
| G2 | Avbrutet tryck (paus) hoppar aldrig | [T: Frog_CancelledPressNeverHops, Scene_FrogPauseMidPressNoHop] |
| G3 | Hoppet går i grodans riktning, fast längd; hemifrån landar det på bladet; generöst mål | [T: Frog_HopFollowsTheFacing, Frog_HomeHopLandsOnThePad, Frog_GenerousPad] |
| G4 | Utanför bordet: miss som kommer tillbaka | [T: Frog_OffTheTableIsAMiss, Scene_FrogMissCarriesOn] |
| G5 | Barnet visar, väntar på spelaren, tidsgräns, väntar när spelaren håller | [T: Frog_ChildShowsThenWaits, Frog_PlayerTurnTimesOut] |
| G6 | Spelaren hoppar i egen takt; barnet väntar efter spelarens hopp | [T: Frog_PlayerOwnPace] |
| G7 | Hela kedjan: order, försäljning, barnet bär setet, visar ett hopp | [T: Scene_FrogsSoldCarriedAndShown] |
| G8 | Tryck och släpp med handen; turordning i scenen; kvack på bladet | [T: Scene_FrogTurnsWithTheChild] |
| G9 | Barnet tar aldrig en groda som spelaren håller | [T: Scene_ChildNeverTakesAHeldFrog] |
| G10 | Butik, order och lek visar samma groda | [T: Scene_FrogsSoldCarriedAndShown (modellerna)], [R: snapshots] |
| H1 | Förstår spelaren hur grodan hoppar efter att ha sett barnet en gång? | [H]/[P] |
| H2 | Vill hon prova igen? | [P] |

Headset (H1, H2): ingen instruktion. Notera om spelaren trycker på bakdelen efter barnets första hopp, hur många försök det tar till ett hopp, och om hon hoppar igen självmant.

## Verifieringslogg

**Röda före implementation (2026-11-16):**
- `FrogTests` (11 regeltest) föll alla mot en stubbe: ett hopp per tryck, avbrutet tryck, riktning, bladet, miss och turordning.
- Katalogtesten (`Catalogue_*`, `Catalog_CategoriesAndPrices`, `Frogs_AToyForTheLeftZone`) föll när bara enum-värdet fanns.
- Scentesten (5 st) föll alla: grodorna hade ingen egen lek och hamnade i klossarnas gren (`NullReferenceException` i `PlayBlocks`).
- `Frog_DroppedOffTheTableFalls` skrevs samtidigt som `FrogPond.Fall` (en rad) och kördes inte rött separat.

**Rättat under arbetet:** i `PlaySequences_Frogs` landade barnets groda mitt på bladet där spelarens redan satt, så de två grodorna låg i varandra (loggen: båda på (−0,27; 0,02)). Nytt test `Frog_TwoFrogsShareThePad` föll (0,0 m isär). Nu landar grodan 7 cm bredvid, på bladet.

**Automatiska test** (2026-11-16):
- `scripts/quest.sh test`: EditMode 255/255, PlayMode 112 gröna (12 explicita hoppade).
- `scripts/kund.sh test`: 14/14. Protokollet är oförändrat, och leksaker beställs inte från mobilen.

**Leksekvensen** `PlaySequences_Frogs` (samma simulerade händer som boll och klossar; bilder i `UnityProject/Logs/epic-018/`):

| Steg | Resultat |
|---|---|
| Butik och order | barnet beställer 1 hoppgrodor (3 mynt); samma groda i lådan och i bubblan |
| Barnets visning | hoppar 2,85 s efter att leken börjat, landar på bladet (kvack), hämtar grodan hem efter 4,9 s och väntar |
| Spelarens tryck | tryck 0,3 s (grodan fjädrar ihop), släpp: ett hopp till bladet, kvack och ring |
| Turordning | barnet hoppar igen 3,2 s efter spelarens tryck och landar bredvid spelarens groda |
| Sikta och miss | lyft, vriden 90°, nedsatt vid kanten: hoppet går av bordet; hemma igen 3,6 s efter trycket |

**Modeller:** `Frog.fbx` 1 432 trianglar, `Frogs.fbx` 2 944, genererade av `art/toys/make_toys.py`. De befintliga leksakernas FBX-filer exporterades om utan ändring och återställdes.

**Boy Scout:** `PlayTableView` delades i en `partial` (grodorna i en egen fil), så att vyfilen inte växer. `PlaySequences.Shot` fick en allmän kamera (`ShotFrom`).

**Bygge:** `2026-11-16_103837_50befaf`, 24 MB, sha256 `2c6b9719…`, rent (från commit `50befaf`). Inte installerat: installation bara med förälderns klartecken. H1 och H2 väntar.

**Installerat 2026-11-16 10:47** via USB med förälderns klartecken. versionName `1.0+2026-11-16_103837_50befaf` är verifierad på headsetet. Inte startat. H1 och H2 väntar.

**Namnbyte till MarketVR, installerat 2026-11-16 11:09** via USB med förälderns klartecken: `2026-11-16_110759_1516b56` (rent, från commit `1516b56`; samma spel som ovan, appen heter nu MarketVR). versionName är verifierad på headsetet. Inte startat.
