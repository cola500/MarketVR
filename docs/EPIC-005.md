---
title: EPIC-005 – sittande spel: gång och vridning med spakarna (slice)
status: headset-checked (spakarna ”funkar fint”, skylten bekräftad; barnets test återstår)
last_updated: 2026-11-01
---

# EPIC-005 – sittande spel: gång och vridning med spakarna

En liten slice. En sittande spelare ska kunna förflytta sig mellan stånden och lekbordet med spakarna, i stället för att resa sig och gå. Den kompakta marknaden (EPIC-003), fysisk rörelse, försäljning, mobilkunden (EPIC-004) och barnens lek är oförändrade. Regler: [SPEC §5 och §18](SPEC.md#18-fysisk-rörelse-och-placering-epic-003--epic-005).

**Ersätter** den tidigare spakmappningen (SPEC §5, EPIC-001): *båda* spakarna upp/ned = diskhöjd och *båda* vänster/höger = 45° vridning.

## Knappmappning

| Kontroll | Funktion |
|---|---|
| Vänster spak, alla riktningar | Gå: upp/ned framåt/bakåt, vänster/höger i sidled, relativt headsetets horisontella riktning. Ingen höjdjustering. |
| Höger spak vänster/höger | Vrid 30° per utslag, runt huvudets position. Spaken måste tillbaka till mitten innan nästa steg. |
| Höger spak upp/ned | Höj/sänk alla diskar och lekbordet (50–120 cm, 0,25 m/s). Spelaren och golvet flyttas inte. |
| Grepp/avtryckare, menyknappen, A/B i menyn | Oförändrade. |
| Meta-knappen | Läses aldrig av appen. Systemmenyn och omcentrering fungerar som vanligt. |

## Beslut och antaganden

- Gångfart **0,5 m/s** vid fullt utslag **[A, prövas på headset]**. Den är bildfrekvensoberoende (m/s × dt). Utslaget begränsas till längd 1, så diagonalt går lika fort som rakt fram. Det finns en radiell dödzon på 0,2.
- Riktningen är headsetets horisontella riktning (yaw). Att titta upp eller ned ändrar varken fart eller höjd. Tittar man rakt upp eller ned används huvudets uppåtriktning för att hitta yaw.
- Höger spak: en **dominant axel låses** när spaken lämnar dödzonen (0,3), och låset gäller tills spaken är tillbaka nära mitten (< 0,25). Ett utslag ger alltså antingen ett vridsteg (vid |x| ≥ 0,6) eller höjdjustering, aldrig båda.
- **Kollision:** diskarna och lekbordet får ett golvavtryck. Rörelse med spaken stoppas när huvudets horisontella position kommer närmare än **0,18 m** till ett avtryck, och glider längs kanten. Fysisk huvudrörelse blockeras aldrig. Står man redan för nära (fysiskt) får spaken bara föra en därifrån, inte längre in.
- Riggen flyttas och vrids, inte marknaden. Ingen teleportering, ingen mjuk vridning och ingen vertikal förflyttning.
- **Kastfart:** handens fart räknas utan riggens rörelse. Att gå eller vrida med något i handen ger alltså ingen kastfart vid släpp.
- **Meny och fokus:** spelmenyn öppen, fokus borta, paus eller headsetet av stoppar gång, vridning och höjd. Efter återkomst måste alla knappar släppas och båda spakarna vara neutrala innan något fungerar igen (samma neutralkrav som för grepp).

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| G1 | Vänster spak går framåt, bakåt och sidledes relativt huvudets yaw, vid flera huvudriktningar. | [T: Walk_FollowsHeadYaw] |
| G2 | Max fart 0,5 m/s, lika diagonalt som rakt. Bildfrekvensoberoende. | [T: Walk_DiagonalSameMaxSpeed] [T: Walk_FrameRateIndependent] |
| G3 | Att titta upp, ned eller luta huvudet ger ingen vertikal rörelse och ingen ändrad fart. | [T: Walk_PitchAndRollDoNotChangeSpeedOrHeight] |
| G4 | Vänster spak ändrar aldrig diskhöjden eller vrider. | [T: Router_LeftStickOnlyWalks] |
| G5 | Höger spak vänster/höger ger ett vridsteg om 30° per utslag och kräver neutralläge innan nästa. | [T: Router_RightStickOneTurnPerFlick] |
| G6 | Höger spak upp/ned ändrar höjden, och per utslag antingen höjd eller vridning (låst dominant axel, även när utslaget glider diagonalt). | [T: Router_RightStickAxisLock] |
| G7 | Vridning sker runt huvudets position: huvudet står kvar i världen även när det är förskjutet från riggens mitt. | [T: Scene_TurnPivotsOnHead] |
| G8 | Höjdjusteringen flyttar inte spelaren eller golvet. Diskar, varor, korg, kassa och lekbord följer med. | [T: Scene_HeightMovesFurnitureNotPlayer] |
| G9 | Gång genom diskar och lekbord stoppas, och spelaren glider längs kanten. Varor, korg, bricka, kassa och lekbordets närmre halva nås från närmaste tillåtna plats. | [T: Collision_StopsAtFurniture] [T: Collision_SlidesAlongEdge] [T: Layout_ReachableFromClosestWalkPosition] |
| G10 | Kollisionen blockerar aldrig fysisk rörelse. Står man för nära kan spaken bara föra en därifrån. | [T: Collision_NeverPushesDeeper] |
| G11 | Spelmeny, fokusförlust, paus och headset av stoppar gång, vridning och höjd. Efter återkomst krävs neutrala spakar. | [T: Router_SticksBlockedByMenuFocusAndNeedNeutral] |
| G12 | Grepp och släpp fungerar under gång och vridning, och gång eller vridning ger ingen kastfart. | [T: Scene_WalkWithHeldItemNoThrow] [T: Scene_SellWhileWalkingBetweenStalls] |
| G13 | Marknaden flyttas aldrig av spakrörelse eller fysisk rörelse. Bara start, omcentrering och menyvalet. Beställning, korg, kassa och lek bevaras. | [T: Scene_WalkingNeverMovesMarket] + befintliga EPIC-003/004-tester |
| G14 | Kontrollhjälpen i menyn och README visar den nya mappningen. | [T: Menu_ControlsShowStickMapping] |
| G15 | APK byggs enligt byggflödet. | [B] |
| G16 | Sittande på headset: gå mellan stånden, vrid, höj/sänk, sälj, lek, och systemmenyn utan oavsiktlig rörelse. | [H] [P] |

## Manuellt headsettest (sittande, utvecklaren och sedan spelaren)

Görs när ingen spelar och föräldern har gett klartecken att installera. Sitt på en stol mitt i den lediga ytan och håll in Meta-knappen så att marknaden placeras.

1. **Gå (G1–G3):** vänster spak framåt till fruktståndet: du stannar vid disken och når äpplelådan, korgen och kassan. Bakåt och i sidled till godiset och leksakerna. Titta upp och ned medan du går: farten och höjden ändras inte. Notera om 0,5 m/s känns lugnt, eller för långsamt eller snabbt.
2. **Vrid (G5, G7):** höger spak åt höger: ett steg på 30°. Håll kvar: inget mer. Släpp och för åt vänster: ett steg tillbaka. Luta dig åt sidan och vrid: ingen hoppande känsla.
3. **Höjd (G6, G8):** höger spak upp och ned: diskarna och lekbordet rör sig, varorna följer med, och du och golvet står kvar. För spaken snett: antingen vridning eller höjd, aldrig båda.
4. **Vänster spak och höjd (G4):** för vänster spak rakt upp och ned: du går, och höjden ändras inte.
5. **Sälj och lek (G9, G12):** gå med ett äpple i handen till korgen och sälj det. Gå till lekbordet, ta bollen, vrid dig och släpp den stilla: den rullar inte iväg.
6. **Möbler (G9, G10):** försök gå rakt in i en disk och lekbordet: du stannar och glider längs kanten. Luta dig fysiskt in över disken: inget trycker bort huvudet.
7. **Meny och systemmeny (G11):** håll vänster spak framåt och tryck på menyknappen: gången stannar. Stäng menyn med spaken kvar: inget händer förrän du släppt den. Samma med systemmenyn (Meta-knappen): ingen oavsiktlig rörelse när du kommer tillbaka.
8. **Marknaden står kvar (G13):** gå runt en stund. Marknaden flyttas aldrig. Flytta marknaden hit i menyn: den placeras där du sitter.
9. **Komfort:** notera illamående, osäkerhet eller om spelaren vill resa sig och gå i stället.

## Uppföljning efter headsettest: Öppet/Stängt-skylten hänger på en pinne

**Fynd (headset, 2026-11-01):** skylten sitter i ståndet. Orsak: brädets mitt låg exakt på ståndets högra främre stolpe, så stolpen gick genom skylten.

**Ändring:** en kort pinne sticker ut åt sidan från den högra främre stolpen, och skylten hänger i två snören under pinnens yttre del. Den svänger runt upphängningen när den slår om. Funktionen är oförändrad: dra en tom hand genom skylten.

| # | Kriterium | Verifiering |
|---|---|---|
| U1 | Skylten skär inte stolparna, disken, taket eller något annat i sitt eget eller grannståndets stånd (minst 1 cm luft). | [T: Scene_OpenSignHangsClearOfTheStall] |
| U2 | En pinne går från stolpen ut till upphängningen, och skylten hänger under pinnen (snören från pinnen till brädet). | [T: Scene_OpenSignHangsClearOfTheStall] [R] |
| U3 | Svepet fungerar som förut, mätt mot brädets mitt, och skylten nås från arbetsytan. | befintliga skylttester [T: Layout_EverythingReachableFromInsideTheArea] |
| U4 | I headsetet: skylten ser ut att hänga på en pinne, inget går igenom den, och den slår om när handen dras igenom. | [H] |

## Verifieringslogg

| Datum | Vad | Resultat |
|---|---|---|
| 2026-11-01 | Slice-specifikation skriven före implementation | – |
| 2026-11-01 | Implementation | Router: vänster spak `Walk` (radiell dödzon, längd ≤ 1), höger spak `RightStick` (låst dominant axel: vridning 30° per utslag eller höjd). `Locomotion` (gång relativt huvudets yaw, vridning runt huvudet, bara riggen flyttas), `MarketCollision` (golvavtryck, 0,18 m, glidning, aldrig djupare in), `FruitStand.ConstrainWalk`, `ShopFrame.RigDelta` (handens fart utan riggens rörelse), `XrRig` kopplar ihop det. Kontrollhjälpen i menyn, SPEC §5/§18 och README uppdaterade. Inga nya paket. |
| 2026-11-01 | Testordning (TDD) | Router-, gång- och kollisionskoden skrevs **före** testerna, innan arbetsreglerna i CLAUDE.md infördes. Deras tester skrevs efteråt och var gröna direkt. `Menu_ControlsShowStickMapping`, `Scene_WalkWithHeldItemNoThrow` och `Scene_WalkStopsAtCounterWhereverTheMarketIsPlaced` skrevs först och fallerade av rätt anledning (gammal hjälptext, kastfart 1,2 m/s, gång rakt igenom disken) före implementationen. `Scene_HeightMovesFurnitureNotPlayer`, `Scene_SellWhileWalkingBetweenStalls` och `Scene_WalkingNeverMovesMarket` var gröna direkt: de skyddar befintligt beteende. |
| 2026-11-01 | Automatiska tester [T] | **EditMode 95/95** (16 nya i `SeatedMovementTests`; 3 gamla routertester för den tidigare mappningen borttagna och ersatta), **PlayMode 49/49** (5 nya scentester; 3 explicita överhoppade), relä 9/9, Editor-kedjan för mobilkunden 2/2. |
| 2026-11-01 | Fynd under testningen | Kastkontrollen visade först 0,04 m/s horisontellt. Det kom från bollens kontakt med bordet, inte från rörelsen. Med bollen fritt i luften blev det 0,000 horisontellt (det vertikala är tyngdkraft). |
| 2026-11-01 11:37 | APK-bygge [B] | `2026-11-01_113748_fddb7aa` (rent, 24 MB, `com.lillafruktstandet.prototype`, versionCode 1, Unity 6000.4.7f1). Inte installerat. |
| 2026-11-01 11:39 | Installation och start på Quest 3 (`QUEST-SERIAL`, USB) | `scripts/quest.sh install` av den arkiverade APK:n: `Success`, utan avinstallation (före: `…_110311_160d3de`). `installed` visar `versionName=1.0+2026-11-01_113748_fddb7aa`. `launch`: appen startade, loggen visar bygg-id och att marknaden placerades (ögonhöjd 0,60 m: headsetet låg troligen ned, omcentrera när det är på). Inga undantag. Bara via ADB: spakarna är inte provade. |
| 2026-11-01 | Headsettest [H], bygge `…_113748_fddb7aa` | Användaren provade spakarna i headsetet: ”funkar fint”. Ett övergripande omdöme: de enskilda stegen i protokollet är inte rapporterade var för sig, och komfort och fart (steg 9) är inte särskilt kommenterade. Fynd: Öppet/Stängt-skylten sitter inne i ståndet (stolpen går genom skylten), se uppföljningen nedan. |
| 2026-11-01 | Skylten på pinne (U1–U3) | `Scene_OpenSignHangsClearOfTheStall` skrevs först och fallerade av rätt anledning (”the sign touches Post”). Därefter: pinne från den högra främre stolpen, brädet 2 cm från stolpen och hängande i två snören 7 cm under upphängningen, och svängning runt upphängningen. Svepet mäts mot brädets mitt (`StandView.OpenSignBoard`). `Scene_SwipeThroughOpenSignFlipsIt` var grönt före och efter: det fanns inget scentest för svepet tidigare. **EditMode 95/95, PlayMode 51/51.** Renderad bild `16-open-sign-on-rod` [R]. U4 i headset återstår. |
| 2026-11-01 11:50 | Bygge och installation `2026-11-01_114909_c276bc1` | Rent bygge [B]. `install`: `Success`, utan avinstallation (före: `…_113748_fddb7aa`). `installed` visar `versionName=1.0+2026-11-01_114909_c276bc1`. Appen startade utan undantag (ögonhöjd 0,37 m vid start: headsetet låg ned). Bara via ADB: skylten (U4) är inte sedd i headset. |
| 2026-11-01 | Skylten i headset (U4), bygge `…_114909_c276bc1` | Användaren: ”Skylten ser bra ut.” U4 godkänd för utseendet. Att den slår om vid svep är inte särskilt rapporterat. |
| – | Barnets test [P] | **Inte gjort.** |
