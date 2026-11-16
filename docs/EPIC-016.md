---
title: EPIC-016 – kunder som väntar på varandra och ett riktningspling
status: built-not-tried
last_updated: 2026-11-02
---

# EPIC-016 – kunder som väntar på varandra och ett riktningspling

Polish-slice ovanpå EPIC-015. Regler: [SPEC §32](SPEC.md#32-besökare-i-parken-epic-015) (avståndshållning) och
[SPEC §6](SPEC.md#6-presentation) (ankomstpling). Inga nya paket, ingen vägplanering och ingen fysik för figurerna.

## Inventering (2026-11-02)

- Kunden på väg in och ut (`CustomerView.WalkAlong`) tog ingen hänsyn till någon. Parkbesökarna (`ParkVisitors`)
  väntade högst 1,5 s när någon stod inom 0,55 m framför dem och gick sedan rakt igenom. Barnen och kunden vid disken
  räknades bara för besökarna, och ingen räknade med de lekande barnen.
- **Reproducerat:** `Park_FiguresNeverWalkThroughEachOther` (alltid "rakt ut"). Nästa kund och den förra möttes rakt
  framifrån på främre armen och kom **0,006 m** isär (kund 4, z = 5,74). Det vanliga fallet: den som går ut tar
  närmaste grind, och nästa kund kommer ofta från samma grind.
- Ljud: `Sounds` (en källa på marknaden, spatialBlend 0,6, syntetiserade klipp), `NatureSounds` (3D-källor i träden).
  Projektet har **ingen spatializer-plugin** (`AudioManager.m_SpatializerPlugin` är tom), så 3D-ljudet är Unitys
  inbyggda: vänster/höger-panorering och avstånd mot lyssnaren på kameran. `AudioListener.pause` (XrRig) tystar allt
  bakom meny, systemmeny och fokusförlust. Ljud som startas under tiden ligger kvar och spelas när pausen släpps.
- `Shop.Tick` körs även när menyn är öppen eller fokus saknas; bara fest och rast håller kunderna. En kund kan alltså
  bli redo bakom menyn.

## Slice

1. **Avståndshållning** (`Game/Pacer`, ren logik): varje gående figur (kunden in/ut, parkbesökarna) förutser 1,5 s
   framåt vem den skulle komma närmare än 0,36 m.
   - Någon står i vägen, eller går framför åt samma håll: figuren bromsar mjukt, köar bakom och stannar 0,5 m före.
   - Någon står i vägen: figuren kliver åt sidan (högst 0,4 m) runt den.
   - Möte, alltså båda är framför varandra (ansikte mot ansikte eller korsande): den med förtur går vidare, den andra
     väntar. Förtur har först kunden (försäljningen ska flyta), sedan den besökare som kom först. Lika förtur avgörs av
     läget, så båda sidor är alltid överens. Ansikte mot ansikte kliver båda undan från varandra, den som väntar först.
   - Sidosteget hålls tills den andra är bakom, och bara där gruset tillåter (`ParkLayout.OnPath`). Mot slutet av vägen
     går figuren tillbaka till linjen, så den kommer fram precis till disken eller bänken.
   - Ingen låses för alltid: den som har stått still i 3 s går vidare långsamt i 2 s, vad som än står i vägen.
2. **Stillastående:** kunden vid disken, de lekande barnen (`PlayTableView.AddChildren`, även på väg till och från
   bordet) och den som sitter på bänken räknas som någon som står. Bänkplatsen var redan reserverad (EPIC-015).
3. **Mjukt och utan att glida:** farten ändras gradvis (från stilla till full fart på 0,37 s, oavsett gånghastighet).
   Gungning och armsving följer den fart som faktiskt gås (`Pacer.Pace`), så en väntande figur står still. En kund som
   blir besökare går vidare i samma fart.
4. **Ankomstpling** (`Core/ArrivalChime` regel, `Sounds.Arrival` uppspelning, `SoundSynth.Hello`): ett kort, mjukt
   G5→C6 (0,6 s, tystare än försäljningsplinget) när kunden har kommit fram och kan beställa. Ljudet kommer från en
   helt rumslig källa (`spatialBlend` 1) vid det aktiva ståndet, inte från kameran. Riktningen följer headsetets läge och
   vridning, även efter vridning och gång med spakarna.
   - En gång per kund, för NPC och mobilkund lika. Inte igen vid *Töm korgen*, meny, paus eller återanslutning.
   - Blev kunden redo bakom menyn, systemmenyn eller under rast: inget pling, varken då eller efteråt (inga gamla
     pling i kö). Markeringen visar ändå vem som väntar.
   - Följer ljudvolymen, som sänks under rasten.
   - Ingen påminnelse och ingen slinga.
5. **Väntemarkering** (`StandView.ShowWaiting`): en liten guldklocka (4 cm, skuggad) hänger i ett snöre från markisen,
   till höger om kunden och ovanför plånboksbrickan, och gungar lätt. Där ser ett sittande barn den när det tittar mot
   disken. Den visas på det aktiva ståndet från att kunden kan beställa till att myntet är betalt (`Ordering`,
   `AwaitingPayment`), och försvinner när kunden är klar.

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| C1 | Ingen går genom någon på gångarna; försäljningen fortsätter | [T: Park_FiguresNeverWalkThroughEachOther, Traffic_HeadOnMeetingBothPass, Traffic_CustomerMeetsLeavingVisitorOnTheArm] |
| C2 | Bromsar och köar bakom den som går långsammare | [T: Traffic_BrakesBehindSlowerWalker] |
| C3 | Går runt den som står, kliver bara lite åt sidan, kommer tillbaka till linjen | [T: Traffic_StandingFigureIsWalkedRound, Traffic_StepAsideIsKeptUntilTheOtherIsBehind] |
| C4 | Sidosteget stannar på gruset | [T: Traffic_SidestepStaysOnThePath] |
| C5 | Ingen permanent låsning: lika förtur, korsande, inträngd | [T: Traffic_HeadOnMeetingWithEqualPriorityBothPass, Traffic_CrossingWalkersNeverLock, Traffic_NoPermanentLockWhenBoxedIn] |
| C6 | Inga ryck; gången följer farten; uppstart skalar med gånghastigheten | [T: Traffic_SpeedChangesSmoothly, Traffic_PaceShowsHowFastTheFigureWalks, Traffic_FasterWalkRampsUpAsQuickly] |
| C7 | Pling exakt en gång när kunden blir redo, inte vid Töm korgen, nytt spel räknas om | [T: Chime_OnceWhenTheCustomerIsReady, Chime_NewGameStartsOver, Arrival_ChimeOnceFromTheStall] |
| C8 | Ingen extra signal vid paus/återupptagning, inget gammalt pling efter menyn | [T: Chime_PauseAndResumeGiveNoExtraSignal, Chime_NoneForACustomerWhoBecameReadyWhilePaused, Arrival_NoChimeWhenReadyBehindTheMenu] |
| C9 | Ljudet kommer från ståndet, helt 3D, inte från kameran | [T: Arrival_ChimeOnceFromTheStall] |
| C10 | Ljudet är kort och mjukt, utan knäpp | [T: Chime_SoundIsShortAndGentle] |
| C11 | Markeringen följer rätt stånd och kundstatus | [T: Chime_MarkerWhileTheCustomerWaits, Arrival_MarkerFollowsTheStallAndTheCustomer] |
| C11b | Markeringen syns från sittplatsen utan att vrida huvudet, och står fri från korg, kassa och plånbok | [T: Chime_MarkerInViewFromTheSeat] [R: Snapshots_WaitingBell 80/81] |
| C12 | Mobilkunden får samma pling och markering | [T: Arrival_PhoneCustomerGetsTheSameSignal] |
| H1 | Flera kunder möts på gångarna: väntar, kliver undan och går vidare naturligt, utan ryck och utan att glida | [H] |
| H2 | Vänd bort från stånden: plinget hjälper att hitta rätt stånd (vänster/höger), även efter vridning och gång med spakarna | [H] |
| H3 | Klockan syns men stör inte, och man ser vilket stånd den sitter på | [H] [P] |

## Verifieringslogg

**Röda före implementation (2026-11-02):**
- Reproduktionen `Park_FiguresNeverWalkThroughEachOther` föll mot den gamla koden (0,006 m).
- Mot kompilerande stubbar föll 13 av 14 rena test och 5 av 5 scentest.
- `Traffic_NoPermanentLockWhenBoxedIn` gick igenom redan mot stubben, som aldrig stannar. Testet skyddar
  tidsgränsen när inbromsningen finns.
- `Traffic_FasterWalkRampsUpAsQuickly` lades till efter 5-varför 2 nedan och kördes inte rött separat.

**Rättat under arbetet:**
- `Traffic_SidestepStaysOnThePath` föll. När den egna sidan inte fanns på gruset vek figuren åt andra hållet, rakt mot
  den mötande. Nu byter figuren sida bara när den andra står rakt framför.

**5 varför 1: kund och besökare 0,295 m isär (`Park_FiguresNeverWalkThroughEachOther`).**
1. *Varför för nära?* Kunden var tillbaka på mittlinjen (sidosteg 0,00) medan besökaren fortfarande var jämsides,
   trots att kunden hade klivit 0,38 m åt sidan. Belägg: instrumenterat testmeddelande (största sidosteget per kund).
2. *Varför tillbaka redan då?* `Pacer` höll bara sidosteget för stillastående. En gående figur slutar räknas när den
   inte längre är framför. Belägg: A/B-körning, utan hållet rött två gånger med identiska siffror, med det grönt fyra
   gånger av fyra.
3. *Varför så nära?* Båda följer samma regel och vänder i samma ögonblick: sidavståndet krymper med 2 × 0,45 m/s precis
   vid passagen.
4. *Varför fångades det inte?* De rena testen körde med tidssteget 0,02 s och kravet 0,30 m och hamnade precis över
   gränsen. Inget test kontrollerade läget strax efter passagen med scenens tidssteg 0,05 s. Nytt test:
   `Traffic_StepAsideIsKeptUntilTheOtherIsBehind` (0,05 s, krav 0,34 m), som föll utan rättelsen (0,327 m).
5. *Varför var första diagnosen fel?* Bara närmaste ögonblicket syntes, och ett koordinatfel (park- kontra
   marknadsrum) gissades utan belägg. Historiken finns nu kvar i testmeddelandet, och felsökning med 5 varför är en regel
   i CLAUDE.md.

**5 varför 2: tre barn vid lekbordet blev två (`Scene_ObjectCountsBounded`).**
1. *Varför två?* Bollbarnet hade redan lekt klart och gått (klossarna hade lekt 28,3 s, bilen 5,1 s). Belägg: läget för
   varje lekplats i testmeddelandet.
2. *Varför hann det?* Kunderna mellan leksaksbarnen tog längre tid än förut, och lektiden är 45 s.
3. *Varför längre tid?* `Pacer` begränsade accelerationen till absoluta 3 m/s². Testet sätter `WalkSpeed = 30`, och då
   tog varje ankomst ≈ 3 s i stället för ≈ 0,5 s.
4. *Varför märktes det inte?* CLAUDE.md tillåter att test höjer `ParkRoutes.WalkSpeed`, men accelerationen skalade inte
   med farten, och inget `Pacer`-test körde med annan hastighet än 1,1 m/s.
5. *Åtgärd:* uppstarten anges som tid (`RampSeconds` 0,37 s), oavsett hastighet. Den är oförändrad vid 1,1 m/s, och
   `Traffic_FasterWalkRampsUpAsQuickly` täcker det. Förutsägelsen höll: alla gröna.

**5 varför 3: klockan syntes inte i renderingen** (`Snapshots_WaitingBell`, trots att `WaitingShown` var sant).
1. *Varför syntes den inte?* Den låg utanför bildytan. Markisen och stolparnas toppar syntes inte heller.
2. *Varför där?* Den hängde under markisens framkant, 53° upp från ett sittande barns ögon, som tittar ned mot disken.
3. *Varför fångade testet det inte?* Testet kontrollerade bara att klockan var aktiv, inte att den låg i synfältet. Nytt
   test: `Chime_MarkerInViewFromTheSeat`, som föll (53°).
4. *Varför höll inte första rättelsen?* Kriteriet "lika synlig som Öppet-skylten" var fel. Skylten ligger själv utanför
   den sittande bilden, 70° åt sidan (barnet vrider huvudet mot den när den ska röras). Testet mäter nu mot den sittande
   kamerans synfält (90° vertikalt, 4:3): högst 48° åt sidan och 38° upp eller ned. Det föll på skyltplatsen (70°).
5. *Varför märktes det först nu?* Den visuella ändringen renderades innan bygget. Det är just därför den kontrollen
   görs. Därtill var den första klockan oskuggad, 6 cm, och såg ut som en stor platt skiva på 0,4 m avstånd. Nu är
   den 4 cm och skuggad.

Snapshot 01–06 (`Snapshots_RenderStates`) har fasta väntetider från före EPIC-015, så kunden hinner inte fram (02 och
04 visar kunden långt bort på gången). Det rättas inte här, men är noterat.

**Avbrutet verktygsanrop:** ett avbrutet kommando hade redan skrivit ändringen i `Pacer.cs` innan testkörningen
stoppades. De gröna körningarna efteråt hade därför ändringen. Det upptäcktes via `git status` och togs med i A/B-körningen
ovan.

**Hela sviten (efter klockans flytt):** EditMode 227/227, PlayMode 102/102 (9 explicita snapshots överhoppade), relä 14/14.

**Inte verifierat:** H1–H3 på headset, och Quest-prestanda (två–tre gående figurer gör en liten O(n²)-kontroll per
bildruta). Riktningsplinget har bara vänster/höger och avstånd, ingen HRTF: fram/bak hörs inte, och där hjälper
klockan.

**Bygge:** `2026-11-02_124526_7133b24-dirty` (oincheckade ändringar ovanpå `7133b24`), 24 MB, sha256 `3b66eb6f…`.
**Installerat 2026-11-02 12:47** med förälderns klartecken. versionName verifierad på headsetet. Starten låg i kö bakom
Horizon OS avstängningsdialog ("Launch is blocked because: a Reprojected OS dialog is currently showing"). Headsettestet
(H1–H3) väntar.

**Rent bygge:** `2026-11-02_125252_a12b617`, 24 MB, sha256 `eb461862…`, från commit `a12b617` (samma innehåll som
`-dirty`-bygget ovan). Inte installerat ännu.
