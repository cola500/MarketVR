---
title: EPIC-017 – gemensam lek vid lekbordet
status: built-not-installed
last_updated: 2026-11-16
---

# EPIC-017 – gemensam lek vid lekbordet

**Mål:** barnet vid lekbordet leker *med* spelaren, inte bredvid. Med bollen ska spelaren och barnet kunna kasta fram och tillbaka. Med klossarna ska de turas om och bygga ihop. Regler: [SPEC §33](SPEC.md#33-gemensam-lek-vid-lekbordet-epic-017).

Bygger på EPIC-003 (lekbordet), EPIC-010 (lekzoner) och EPIC-011 (förlåtande fysik). Inga nya paket. Fysikbudgeten är oförändrad (8 kroppar).

## Utforskning före ändringen (2026-11-16, `a12b617`)

Två leksekvenser spelades med simulerade händer genom spelets vanliga grepp och släpp: `PlaySequences_Ball` och `PlaySequences_Blocks` (explicit, `TEST_FILTER=PlaySequences scripts/quest.sh snapshots`). Samma steg körs efter ändringen. Loggen (`[seq]`) och bilderna hamnar i `UnityProject/Logs/shots/seq-*`.

**Boll** (lyft 15 cm på 0,3 s, sedan 6 cm framåt och 3 cm upp på 0,1 s mot barnet, släpp i rörelse):
- Kastet blir en fri båge (högst 0,23 m över bordet, handfart 0,64 m/s) som landar på barnets halva. Barnet fångar inte. Bollen ligger tills barnet hämtar den till bollhörnet och rullar tillbaka: **3,7 s per pass**, tre gånger i rad.
- Ett kast åt sidan hamnar på golvet. Den befintliga återställningen (tappad leksak efter 2 s) ger tillbaka bollen hos spelaren efter **4,7 s**. Det fungerar redan.
- Flytet tappas av att barnet aldrig tar emot. Ett kast och en rullning ser ut att få samma svar.

**Klossar** (barnet bygger, spelaren lägger en kloss bredvid barnets första och en på sin egen):
- Barnet **tar spelarens kloss** som ligger bredvid tornet och sätter den i sitt eget torn efter 3 s. Barnet och spelaren styr alltså samma kloss.
- Barnet lägger en kloss varannan sekund utan att vänta. Efter 13 s står alla sex klossar i **ett enda torn** (0,33 m högt). Det blir aldrig bredare, och spelaren hinner inte vara med.

Bilder: `seq-ball-02-toss-in-flight`, `seq-ball-03-toss-landed`, `seq-blocks-03-player-beside`, `seq-blocks-05-after-six-seconds`.

## Slice

### Boll: ett mjukt kast som barnet tar emot

1. **Rullning som förut:** ett släpp nära bordet (bollens mitt under 8 cm över skivan) ger samma rullning som tidigare, med släppfart högst 1,2 m/s.
2. **Kast** (`Core/BallToss`, ren logik): bollen lyfts tydligt (mitten minst **8 cm** över skivan) och släpps medan handen rör sig framåt (vågrät fart minst **0,25 m/s**, bort från spelaren). Då blir det ett mjukt kast i en båge som toppar **8 cm** över den högre änden:
   - **Mot barnet:** pekar rörelsen högst **35°** från barnets händer går kastet exakt till barnets fångpunkt. Farten beror inte på hur fort handen rörde sig, så ett litet kast räcker.
   - **Åt annat håll:** bågen landar i rörelsens riktning, 0,20–0,70 m bort beroende på handens fart. Den kan missa bordet.
   - Kastets fart är högst **2,4 m/s**. Ingen luftbromsning under bågen.
3. **Barnet fångar:** en kastad boll som kommer inom **12 cm** från fångpunkten fångas. Barnet håller den en kort stund (0,5 s), lägger den i bollhörnet och rullar genast tillbaka till spelaren.
4. **Kast som landar fel:**
   - En boll som landar på barnets halva hämtas och rullas tillbaka som förut.
   - En boll som hamnar på golvet kommer tillbaka med den befintliga återställningen (EPIC-011). Därefter rullar barnet den till spelaren.
5. **Spaken ger ingen fart:** handens fart mäts utan riggens rörelse (`RigDelta`, EPIC-005). Att gå eller vrida med spaken under kastet ändrar inte kastet.

### Klossar: turas om och bygg ihop

1. **Byggplats** (`Core/BuildSite`, ren logik): ett fast rutnät med 3 kolumner och 3 lager i byggplatsens mitt, (0,30; 0,04) i bordets koordinater. En ruta är **stabil** när den står på bordet eller på en upptagen ruta.
2. **Delad plats, egna klossar:** de sex klossarna delas upp.
   - Barnets tre ligger i raden närmast bygget (z = −0,06), och barnet flyttar bara sina egna.
   - Spelarens tre ligger i raden närmast spelaren (z = −0,13).
   - Båda raderna ligger inom räckhåll för en sittande spelare, som alla leksaker måste (SPEC §14). En hög på barnets sida (z = +0,17) bröt `Layout_*` och `SpaceFit`.
   - En kloss som spelaren har tagit flyttar barnet aldrig, förrän *Städa lekytan*. Barnet flyttar aldrig en kloss som står i bygget eller bär en annan kloss.
3. **Turordning:**
   - Barnet lägger en kloss (första efter 2 s) i den första lediga stabila rutan, i ordningen mitten, vänster, höger, och sedan lagret ovanför.
   - Sedan är det spelarens tur i upp till **8 s**. Tiden står still medan spelaren håller en kloss.
   - När spelaren lägger en kloss i bygget kommer barnets nästa efter 2 s. Gör spelaren ingenting lägger barnet nästa efter 8 s.
4. **Hjälp vid släpp:**
   - Ett släpp nära bygget (vågrätt högst **8 cm** från en rutas mitt och högst 10 cm ovanför den) hamnar i den närmaste lediga **stabila** rutan, alltså bredvid, ovanpå eller i en lucka.
   - Spelaren väljer själv formen, högre eller bredare, och barnet bygger vidare på den.
   - Ett släpp inne i en upptagen ruta hamnar i den närmaste lediga stabila rutan.
   - Längre bort gäller EPIC-11 som förut (`BlockPlacement`).
5. **Står kvar:** klossar i bygget är fasta (kinematiska) och står kvar genom fysiksteg. Tas en kloss ur bygget faller det som stod på den.

## Acceptanskriterier

| ID | Kriterium | Verifiering |
|---|---|---|
| B1 | Låg släpp rullar som förut | [T: Toss_LowReleaseRolls, Scene_PlayerRollsBallBackChildContinues] |
| B2 | Lyft och släpp framåt mot barnet går exakt till fångpunkten, i en båge | [T: Toss_LiftAndPushReachesTheChild] |
| B3 | Ett litet kast räcker; snabbare hand ger samma kast mot barnet | [T: Toss_SmallFlickIsEnough, Toss_FasterHandSameLob] |
| B4 | Kast åt annat håll landar längs rörelsen, avståndet begränsat | [T: Toss_OffAimFollowsTheGesture] |
| B5 | Lyft utan rörelse, eller rörelse bakåt, är inget kast | [T: Toss_StillHandIsNoToss] |
| B6 | Tre bollpass i rad: kast, barnet fångar, rullar tillbaka | [T: Scene_ThreeBallPassesInARow] |
| B7 | Missat kast: bollen kommer tillbaka och nästa kast fångas | [T: Scene_MissedTossCarriesOn] |
| B8 | Gå med spaken under kastet ger samma kast | [T: Scene_TossWhileWalkingSameToss] |
| K1 | Bara stabila rutor; barnet börjar i mitten | [T: Build_OnlyStableCells, Build_ChildStartsInTheMiddle] |
| K2 | Barnet lägger en kloss och väntar på spelarens tur (tidsgräns, väntar när spelaren håller) | [T: Build_ChildWaitsForThePlayer, Build_PlayerTurnTimesOut] |
| K3 | Släpp nära bygget hamnar i närmaste stabila ruta, bredare eller högre | [T: Build_SnapNearTheHand, Build_SnapOutOfAnOccupiedCell, Build_FarReleaseIsNotSnapped] |
| K4 | Tar man en kloss ur bygget faller det som stod på den | [T: Build_RemovingAFootFreesWhatStoodOnIt] |
| K5 | Barnet flyttar aldrig spelarens klossar; barnet lägger en och lämnar plats | [T: Scene_ChildLaysOneAndLeavesRoom] |
| K6 | Gemensamt bygge (barnets och spelarens, både bredare och högre) står kvar efter många fysiksteg | [T: Scene_SharedBuildStands] |
| H1 | Räcker ett litet kast för spelaren? Fångar barnet tydligt? | [H] |
| H2 | Bygger spelaren vidare på barnets torn utan instruktion? | [H]/[P] |

Headset (H1, H2): Spelaren sitter. Ingen instruktion ges. Notera antal kast till första fångst, om hon kastar eller rullar, och om hon lägger en kloss på barnets bygge inom två minuter.

## Verifieringslogg

**Röda före implementation (2026-11-16):**
- Mot kompilerande stubbar föll 12 av 14 rena test. `Toss_LowReleaseRolls` och `Toss_StillHandIsNoToss` gick igenom redan mot stubben, som aldrig kastar. De skyddar mot ett för ivrigt kast.
- Scentesten föll 4 av 5: ingen fångst, ingen uppdelning av klossarna (6 i stället för 3) och ingen byggplats.
- `Scene_TossWhileWalkingSameToss` gick igenom redan före: `RigDelta` håller spakens rörelse utanför handfarten sedan EPIC-005. Det befintliga skyddet räcker, och testet blir kvar som skydd även för kastet.
- `Scene_MissedTossCarriesOn` föll först på den efterföljande fångsten. Själva återställningen efter missen fungerade redan.

**5 varför: kast under gång gav 0,64 m/s annan hastighet (`Scene_TossWhileWalkingSameToss`, första versionen).**
1. *Varför olika hastighet?* Stående blev kastet `Wide`, under gång `ToChild`. Belägg: loggraderna `[Fruktstand] ball toss`.
2. *Varför olika sort?* Handfarten var densamma, 0,69 m/s i båda (samma loggrader). Riggen bar handen åt sidan, så släppunkten låg närmare barnets linje, och 45° åt vänster hamnade inom 35°-konen sett därifrån.
3. *Varför räknas siktet från släppunkten?* Med avsikt: siktet mäts från där bollen lämnar handen.
4. *Varför fångades det inte?* Testets gest låg nära konens kant, så några centimeters förflyttning ändrade kastsorten.
5. *Åtgärd:* testet kastar tydligt utanför konen (cirka 80°). Då gav stående och gående exakt samma hastighet, (1,097; 1,117; −0,197). Koden ändrades inte.

**Räckvidd:** barnets klossar på barnets sida (z = +0,17) föll i `Layout_EverythingReachableFromInsideTheArea`, `Layout_ReachableFromClosestWalkPosition`, `SpaceFit` och därmed kalibreringstesten (7 st). Alla leksaker måste nås sittande (§14), så raderna blev de två som redan var godkända (−0,06 och −0,13). Uppdelningen kommer av ägarskapet.

**Ändrade äldre test** (beteendet ändrades med avsikt):
- `Scene_ChildBuildsTower`: barnet lägger en kloss och väntar, och spelarens kloss hamnar ovanpå.
- `Scene_LostBlockReturnsWhileTowerStands`, `Scene_TakeBlockFromTheChildsTower`: `Build.Count` och rutor i stället för `TowerHeight` och `TowerSpot`. Den andra väntar nu på spelarens tur (upp till 12 s).

**Automatiska test** (2026-11-16): `scripts/quest.sh test`: EditMode 241/241, PlayMode 107 gröna (11 explicita hoppade). Reläet ändrades inte, så `kund.sh test` behövdes inte.

**Leksekvenserna efter** (`PlaySequences`, samma steg som före; bilder i `UnityProject/Logs/epic-017/{before,after}/`):

| | Före | Efter |
|---|---|---|
| Kast mot barnet | fri båge, landar, barnet hämtar och rullar: 3,7 s per pass, ingen fångst | barnet fångar alla tre, rullar tillbaka: 2,9 s per pass |
| Missat kast | på golvet, tillbaka hos spelaren efter 4,7 s | samma, 4,7 s |
| Spelarens kloss bredvid barnets | barnet tog den till sitt torn efter 3 s | snäpper till vänster om barnets, barnet rör den inte |
| Efter 13 s | ett torn av alla sex, 0,33 m | 2 brett × 2 högt + barnets kloss ovanpå; spelarens sista kloss orörd |

**Bygge:** `2026-11-16_101513_a12b617-dirty`, 24 MB, sha256 `abb3d6fe…`. Källorna arkiverades bredvid APK:n. Inte installerat: installation bara med förälderns klartecken. H1 och H2 väntar.

**Boy Scout:** `BlockEverySeconds` togs bort (oanvänd). Väntan på 0,8 s innan barnet rullar fick ett namn (`RollAfterSeconds`). Kommentaren om "tornet" i `PlayerTook` gäller nu bara ringarna. Verifierat med hela sviten.

**Rent bygge:** `2026-11-16_102017_a5efb50`, 24 MB, sha256 `96c25389…`, från commit `a5efb50` (samma innehåll som `-dirty`-bygget ovan). Inte installerat ännu.
