---
title: MarketVR – specifikation
status: draft-implemented
last_updated: 2026-11-02
---

# MarketVR – specifikation

Appen hette tidigare *Lilla fruktståndet*. Android-appens id (`com.lillafruktstandet.prototype`) är oförändrat, så nya byggen uppdaterar den installerade appen.

Ett litet VR-spel för Meta Quest 3 där spelaren är försäljare i ett frukt- och grönsaksstånd en solig dag.
Specifikationen gäller **EPIC-001** (fruktståndet, [EPIC-001.md](EPIC-001.md)) **EPIC-002** (plånbok och godisstånd, [EPIC-002.md](EPIC-002.md), §11–§13) **EPIC-003** (Lilla marknaden, [EPIC-003.md](EPIC-003.md), §14–§20) **EPIC-004** (förälder som kund från mobilen, [EPIC-004.md](EPIC-004.md), §21) **EPIC-005** (sittande spel: gång och vridning med spakarna, [EPIC-005.md](EPIC-005.md), §5 och §18) **EPIC-006** (krokodilspelet vid lekbordet, [EPIC-006.md](EPIC-006.md), §23) **EPIC-007** (rikta in marknaden mot spelytan, [EPIC-007.md](EPIC-007.md), §24) **EPIC-008** (marknadsfest och vilopaus, [EPIC-008.md](EPIC-008.md), §25) **EPIC-013** (parken runt marknaden, [EPIC-013.md](EPIC-013.md), §30) **EPIC-014** (parkens atmosfär och naturljud, [EPIC-014.md](EPIC-014.md), §31) **EPIC-015** (besökare i parken, [EPIC-015.md](EPIC-015.md), §32) och **EPIC-016** (avståndshållning och ankomstpling, [EPIC-016.md](EPIC-016.md), §6 och §32). Spårbara byggen: §22. EPIC-003 ersätter delar av EPIC-002: se markeringarna i §11–§13. Den hålls uppdaterad när implementationen ändras.
Antaganden är markerade **[A]**.

## 1. Målgrupp

- Ett barn som gillar att leka affär och föredrar konkreta, fysiska saker (valde riktiga magnetbrickor framför Magic Tiles i VR).
- Läser inte säkert. Bilder, former och färger ska bära all information. Kort svensk text får komplettera.
- Spelar med en vuxen i närheten, som startar appen och kan visa en första beställning.
- **[A]** Barnet klarar att hålla en Quest Touch-kontroll och trycka på en knapp, men finmotorik i VR (exakt sikte, små mål) ska inte krävas.

## 2. Hypotes

> Barnet tycker om att vara försäljare och kan, efter högst en visad beställning, genomföra tre köp med bildstödet och utan hjälp med kontrollerna.

Bekräftas eller förkastas i första användartestet ([PLAYTEST.md](PLAYTEST.md)). Det testet är **inte** genomfört i denna epic.

## 3. Spel-loop

1. En kund går fram till disken. En pratbubbla visar beställningen med stora bilder av varorna, en bild per styck, och en siffra per sort.
2. Spelaren plockar varor ur lådorna på sin sida av disken.
3. Spelaren släpper varan nära korgen. Den hamnar stabilt på en ledig plats i korgen (snapping). En bock tänds i bubblan för varje rätt vara.
4. Fel eller för många varor kan plockas ur korgen igen. Släpps de utanför korg och kassa åker de tillbaka till sin låda. Inget straff.
5. När korgen innehåller exakt beställningen blir kunden glad och lägger en **myntbricka** på disken.
6. Spelaren lägger myntbrickan i **kassan**. Köpet registreras exakt en gång: pling, stjärnor, kunden hoppar till och säger ”Tack!”.
7. Korgen töms, kunden går och nästa kund kommer.

Ingen timer, ingen förlust, inga missnöjda kunder. Loopen fortsätter så länge spelaren vill.

## 4. Regler (domänlogik)

Domänlogiken ligger i `Fruktstand.Core` utan XR, fysik eller rendering, och testas automatiskt.

### Varor
- *(EPIC-001; ersatt av §15 och §26: åtta sorter, tre aktiva åt gången.)* Tre varor: **äpple** (rött, runt), **banan** (gul, lång och böjd), **morot** (orange, spetsig kon med gröna blad). Skiljs med både färg och form.
- Obegränsat lager: lådan visar alltid en vara att ta. Tas den, dyker en ny upp.

### Beställning
- 1–3 varor totalt, 1–3 sorter, bara de tre varorna.
- Första beställningen efter start eller ”Nytt spel” är alltid **ett äpple** (EPIC-001; på marknaden gäller nu §16: den första aktiva sorten) (den första slicen och visningsbeställningen i speltestet).
- Därefter slumpas beställningar deterministiskt från ett frö: totalt antal ökar mjukt (kund 2–3: 1–2 varor, därefter 1–3) **[A]**.
- Två kunder i rad får inte samma beställning **[A]**, så att det känns som en ny kund.

### Korg
- Korgen har **4 platser** **[A]**: plats för en full beställning plus en felaktig vara. Är korgen full åker varan tillbaka till lådan.
- En vara som släpps inom korgens fångstradie hamnar på närmaste lediga plats.
- Innehållet **matchar** när antalet av varje sort är exakt lika med beställningen (ordningen spelar ingen roll). För många av en sort eller en sort som inte beställts matchar inte.

### Köp – tillstånd

| Tillstånd | Vad som händer | Lämnas när |
|---|---|---|
| `CustomerArriving` | Kunden går fram (1,5 s). Korgen tar inte emot varor ännu. | tiden har gått |
| `Ordering` | Beställningen visas. Varor kan läggas i och tas ur korgen. | korgen matchar exakt → `AwaitingPayment` |
| `AwaitingPayment` | Korgen är låst (kunden har ”tagit emot” varorna). Myntbrickan ligger på disken. | myntbrickan läggs i kassan → `Thanking` |
| `Thanking` | Pling och tack (2,5 s). Köpet är registrerat. | tiden har gått → `CustomerLeaving` |
| `CustomerLeaving` | Korgen töms, kunden går (1,5 s). | tiden har gått → ny kund i `CustomerArriving`, eller `Closed` om ståndet är stängt |
| `Closed` | Ståndet är stängt (skylten visar Stängt). Ingen kund, ingen timer. | ståndet öppnas → ny kund i `CustomerArriving` |

- **Betalning registreras bara i `AwaitingPayment`**, och bara en gång: övergången sker atomärt till `Thanking` och krediterar samtidigt plånboken med 1 spelpeng (§11). Upprepade trigger-, släpp- eller kollisionshändelser efter det ger `false` och räknar inte köpet igen.
- **Betalning för tidigt** (innan korgen matchar) kan inte ske: det finns ingen myntbricka. Anropet avvisas ändå i logiken.
- Att ta ur en vara räknas om direkt: tas en felaktig vara ur och resten stämmer går köpet vidare till betalning.
- **[A]** Korgen låses när den matchar. Det förhindrar att beställningen ändras medan myntbrickan ligger framme. Barnet har då redan gjort rätt.
- **Töm korgen** (spelmenyn) återställer bara den aktuella beställningen: korgen töms (varorna går hem), samma kund och beställning står kvar. Låg myntbrickan framme går den tillbaka till kunden och beställningen är öppen igen. Pengar och godis rörs inte.
- **Nytt spel** (spelmenyn, efter bekräftelse där ”Nej” är förvalt) nollställer allt: antal köp, plånbok, godispåse, korgen, ståndet öppnas och första kunden kommer (ett äpple). Ersätter EPIC-001:s ”Börja om”.
- **Stängt/Öppet**: dra en tom hand genom skylten som hänger i två snören från en kort pinne ut från ståndets högra främre stolpe (ingen knapp; ändrat 2026-11-01, tidigare satt den på stolpen), eller använd menyn. Skylten slår om en gång per svep och bara om handen kom utifrån; en hand som bär något, eller en hand medan en meny är öppen, slår inte om den (`TouchSwitch`). Skylten gungar runt upphängningen när den slår om. Stängt pausar bara *nya* kunder; en kund som redan är där står kvar och beställningen kan slutföras. Inga kunder blir arga och ingen timer löper.
- Antal genomförda köp räknas (`CompletedSales`) och visas diskret som små stjärnor på ståndet.

## 5. Interaktion

### VR (Quest 3, Touch-kontroller)
- **Plocka**: håll handen (kontrollen) nära en vara och tryck på **greppknappen eller avtryckaren**. Varan följer handen så länge någon av dem hålls in. Varan närmast handen inom 12 cm lyser upp innan man tar den.
- **Släppa**: släpp knappen.
  - Nära korgen (≤ 22 cm från korgens mitt): varan hamnar på en ledig plats.
  - Myntbrickan nära kassan (≤ 20 cm): betalningen registreras.
  - Annars: varan glider tillbaka till sin låda, myntbrickan tillbaka till sin plats på disken. Inget tappas bort, inget faller till marken.
- Båda händerna kan plocka, en sak per hand.
- Ett tryck krävs för att ta något. Att hålla knappen inne och föra handen över en vara tar den inte (förhindrar oavsiktliga grepp).
- Vibration: kort puls när man tar något och när det landar i korg eller kassa.
- **Disken**: ståndet ställs framför spelaren vid start. **Spakarna (EPIC-005, ersätter 2026-11-01 den tidigare mappningen där båda spakarna höjde disken och vred 45°):** vänster spak går (upp/ned framåt/bakåt, vänster/höger i sidled, relativt headsetets horisontella riktning, 0,5 m/s) och ändrar aldrig höjden. Höger spak vänster/höger vrider 30° per utslag runt huvudet, och spaken måste tillbaka till mitten före nästa steg. Höger spak upp/ned höjer/sänker diskarna och lekbordet (50–120 cm, högst 25 cm/s) utan att flytta spelaren eller golvet. Höger spak låser den dominanta axeln tills spaken är tillbaka i mitten, så ett utslag ger antingen vridning eller höjd. Inget av detta sker under meny eller systemmeny, eller innan spakarna släppts efter en återkomst. Detaljer i §18. Allt på disken följer med. Vid start hamnar diskens mitt 32 cm framför ögonen och disken 40 cm under dem. Lådor, korg, myntbricka och kassa ligger på diskens närmaste halva, inom ca 35–40 cm från en axel, så det går att spela sittande **[A, kontrolleras på headset]**. Ingen förflyttning krävs.
- **Spelmeny** (menyknappen ☰), aktuell: Fortsätt, Visa kontroller, Stäng/Öppna marknaden, Återställ beställningen, Städa lekytan, Flytta marknaden hit, Rikta in marknaden (§24), Ta vilopaus (§25), För föräldrar (omgångens siffror och Starta nästa omgång, §25) och Nytt spel (kräver ”Ja”, ”Nej” är förvalt). Valfri spak upp/ned flyttar markeringen, spak vänster/höger ändrar värden på kalibreringssidan, avtryckare eller A väljer, och B går tillbaka. *(I EPIC-001 hette raderna ”Stäng/Öppna fruktståndet” och ”Töm korgen”.)*
- **Systemmenyn** (Meta-knappen) hanteras helt av Quest och läses aldrig av appen.
- **Fokus**: utan inputfokus, vid paus eller med headsetet av stoppas alla spelhandlingar och kontrollerna döljs. Det spelaren håller i går tillbaka till sin plats (aldrig till korg eller kassa). Efter återkomst, och efter att spelmenyn stängts, krävs släppta knappar innan man kan ta något. Inget grepp, släpp eller köp kan alltså ske av en knapp som hölls genom avbrottet.

### Desktop (för snabb kontroll av logiken)
Samma scen och logik utan headset. Tangenterna gäller den nuvarande marknaden (EPIC-003, §20). EPIC-001:s och EPIC-002:s tangenter för godisköp finns inte längre.

| Tangent | Handling |
|---|---|
| 1–9 | Lägg vara 1–9 i den aktiva korgen (1–3 frukt/grönt, 4–6 godis, 7–9 boll, bil, klossar) |
| Backsteg | Ta ur senaste varan ur korgen |
| Mellanslag | Lägg betalningsbrickan i kassan |
| Tab | Byt kameravy (översikt, frukt, godis, leksaker, lekbordet) |
| O | Stäng / öppna marknaden |
| C | Återställ beställningen |
| P | Städa lekytan |
| R, sedan R igen inom 2 s | Nytt spel |
| + / − | Höj / sänk diskarna och lekbordet |

## 6. Presentation

- Solig utomhusmiljö: procedurell himmel, sol med hårda skuggor, grön gräsmatta (samma upplägg som Magic Tiles, inbyggda shaders). Runt marknaden ligger en liten park med träd, bänk, blommor och en grusgång (§30).
- Ståndet: disk med trä-färger och randig front, randigt tak (rött/vitt) på stolpar, tre lådor med högar av varor och en bild av varan på framsidan, en flätad korg med fyra ljusa platsmarkeringar, en kassa (turkos låda med svart springa i guldkant, samma guld som myntbrickan).
- Varor och kund byggda av primitiver: kund med kropp, huvud, ögon och leende, några färgvarianter med hatt.
- Pratbubbla ovanför disken bredvid kunden: en bild per vara som ska köpas (rad per sort) och en stor siffra. Varje bild står på en grå skiva som blir grön med en bock när varan ligger rätt. När korgen stämmer byts bubblan mot en bild: mynt → kassa.
- Återkoppling utan ljud: varan närmast handen växer lite och handen blir gul; korgkanten lyser grönt när en vara hålls över den och när beställningen stämmer; kassans springa lyser gult när myntbrickan hålls över den; en vara som inte hör till beställningen vickar sakta i korgen; kunden ler stort, hoppar och vinkar vid köp; varje köp ger en liten stjärna på disken.
- Text (kort svenska) bara som komplement: ”Tack!”, ”Hej!”.
- Ljud kompletterar men krävs aldrig: ”pop” när en vara landar i korgen, ”pling” vid köp, och (EPIC-016) ett kort, mjukt ankomstpling från ståndet när en ny kund kan beställa: en gång per kund, rumsligt (riktningen följer headsetet), aldrig bakom meny, systemmeny eller rast och inga gamla pling efteråt. Medan kunden väntar hänger en liten guldklocka i ett snöre från ståndets markis, till höger om kunden. Ljuden syntetiseras i koden (inga ljudfiler). Under dem ligger parkens naturljud (§31).

## 7. Teknik

- Unity 6000.4.7f1, inbyggd renderingspipeline, gamla Input Manager.
- Paket: `com.unity.xr.management` 4.5.0, `com.unity.xr.oculus` 4.5.0, test-framework och modulerna androidjni/imgui/jsonserialize/audio/physics (fysik bara för leksakerna, §20). Inget XR Interaction Toolkit, inget Input System och inget Meta SDK. Från Asset Store används bara några få parkmodeller (§30), som importeras selektivt och inte committas. Relän för mobilen (§21) är Node 22+ utan npm-paket.
- Application id `com.lillafruktstandet.prototype`. Appnamn ”Lilla fruktståndet”.
- Assembly-uppdelning:
  - `Fruktstand.Core` (ren logik): varor (`Ware`), beställning, beställningsgenerator, korg, köpets tillståndsmaskin (`Shop`), kassan (`Wallet`), lekplatserna (`PlayArea`), `Market`, mobilkunden (`RemoteCustomer`), krokodilspelet (`CrocodileGame`), omgång/fest/paus (`MarketRound`), släppregler (`DropRules`). Bara `Vector3`/`Mathf` från Unity.
  - `Fruktstand.Game`: scenbygge i runtime, vyer (stånd, lekbord, krokodil, fest), grepp (`Grabber`), layout och golvavtryck (`MarketLayout`, `MarketCollision`), inriktning (`SpaceFit`, `Calibration`, `LayoutMarking`), desktopinput, ljud och bygg-id (`BuildInfo`). `Game/Remote`: mobilens protokoll och nätverk.
  - `Fruktstand.Game.Xr`-delen: `XrRig` (enheter), `XrInputRouter` (fokus, meny, spakar, neutralkrav – ren logik), `Locomotion`, `StandPlacement`, `BoundaryProbe`, `GameMenu`.
  - `Fruktstand.EditorTools`: scen-generering och Quest-bygge.
- Återanvänt från Magic Tiles (kopierat och anpassat, originalet orört): Quest-konfiguration och `preloadedAssets`-vakten, `InputDevices`-läsning, fokus-/paus-/närvarohantering, menyns tillståndsmaskin, höjdbart bord, miljö, `scripts/quest.sh`.

## 8. Omfattning – med

*Uppdaterad 2026-11-01.* En spelare på Quest 3 och en förälder som kund från mobilen (§21). Marknaden har tre stånd: frukt & grönt, godis och leksaker, med nio varor (§15). En kund åt gången, beställningar på 1–3 varor, korg med snapping, betalningsbricka och en gemensam kassa, pling och tack, och obegränsat lager. Barnen leker vid lekbordet (§17). Krokodilspelet (§23). Kompakt uppställning med fysisk rörelse (§14, §18), gång och vridning med spakarna (§5, §18), höjdbara diskar, och inriktning mot rummets spelyta (§24). Marknadsfest och vilopaus (§25). Spelmeny, fokushantering, desktopläge och spårbara byggen (§22).

## 9. Undantag – inte med

Multiplayer (utom en mobilkund som skickar beställningar och en förälder som kan pausa, §21 och §25), mobilapp (mobilsidan är en webbsida), säker barnspärr (föräldradelarna är praktiska kontroller), hand tracking, röstigenkänning, avancerad kund-AI, odling, matlagning, lagerhantering, sparsystem (saldo och godis sparas inte mellan appstarter), växel och fysisk mynträkning, lagerbrist, butikspersonal, riktiga köp, ätande av godis, teleportering, mjuk vridning och vertikal förflyttning (gång med vänster spak och stegvis vridning finns, §5 och §18), poäng, tidspress och förlust i lekhörnan, motor eller styrning för leksaksbilen, fler varor, lemonad, fysiksimulering av varor (de faller aldrig fritt).

## 10. Öppna frågor till speltestet

- *(EPIC-002, inaktuell: godisköpet är ersatt.)* Hittar barnet godisståndet till höger utan att någon visar det? Förstår hon prislapparna och ”Det räcker!”? Går hon tillbaka till frukten själv?
- (EPIC-006) Förstår hon att trycka ned krokodilens tänder med greppet, eller försöker hon bara röra dem? (Avgör om beröring, K16–K20, behövs.)
- (EPIC-008) Hur lång tid tar 80 pengar? Förstår hon att omgången är färdig, och hjälper festen henne att ta paus?
- (EPIC-007) Hamnar förslaget på systemgränsen, och når hon allt efter inriktningen?

- Är ”håll inne för att bära” rätt för barnet, eller behövs ”tryck för att ta, tryck för att släppa”?
- Räcker bilder + bockar för att förstå när beställningen är klar?
- Är myntbrickan och kassan begripliga utan förklaring?

## 11. Plånbok och ekonomi (EPIC-002) – delvis ersatt

> **Ersatt av §15 (EPIC-003):** plånboken är nu marknadens gemensamma **kassa**. Den krediteras med **ordersumman** (frukt/grönt 1 per vara, godis och leksaker enligt pris) i `Shop.TryPay`. Spelaren spenderar inga pengar, och godisförsäljningens direktbetalning är borttagen. Det som står nedan gäller bara historiskt.

- **Två källor till intäkt, inga andra:** (1) **1 spelpeng** per registrerat fruktköp, i den atomära övergången `AwaitingPayment → Thanking` i `Shop.TryPay`; (2) **godisets pris** när en ledsen kund vid godisståndet får rätt godis, i den atomära övergången `Waiting → Happy` i `CandyCustomers.TryServe` (§12). `CompletedSales` är antal fruktköp (stjärnorna), saldot är pengarna. Invariant: `Saldo = CompletedSales × 1 + intäkt från godisförsäljning − använt`.
- Saldot är ett heltal ≥ 0. `Spend` med för lite pengar eller ett ogiltigt belopp ändrar ingenting.
- Bara spelpengar. Inga riktiga köp, ingen växel, ingen mynträkning.
- **Visning:** ett myntmärke med siffra vid kassan på fruktdisken och på godisståndets skylt. Det pulserar kort vid ändring.
- **Ingen sparning:** saldo och godis lever i minnet. De finns kvar mellan stånden och genom paus, systemmeny och spelmeny, men börjar på 0 vid varje appstart **[A]**.

## 12. Godisståndet (EPIC-002) – ersatt

> **Ersatt av §14–§16 (EPIC-003):** godisståndet är ett säljstånd med samma loop som fruktståndet. Det som står nedan om att spelaren köper godis (förhandsvisning, betalyta, egen påse, prislappar), om direktbetalning från ledsna kunder och om godisdiskens placering vid 125° gäller inte längre. Spelarens önskemål lever kvar: godiskunder är ledsna tills de fått sitt godis (§16).

### Regler (`CandyShop`, ren logik)

| Godis | Pris | Utseende |
|---|---|---|
| Klubba | 1 | Rund platt rosa-vit spiral på vit pinne |
| Polkagris | 2 | Rak vit stång med röda ränder |
| Godispåse | 3 | Liten ljusblå påse med rosett och färgade godisbitar |

- Hyllan har alltid en av varje sort (obegränsat).
- **Förhandsvisning:** att ta ett godis från hyllan väljer det (`Select`). Skylten visar godiset, priset som myntbilder och om pengarna räcker. Det kostar inget.
- **Bekräfta = betalytan:** godiset släppt på den rosa betalytan (med myntbild) köper det (`TryBuy`). Räcker pengarna dras priset exakt en gång och godiset hamnar i påsen. Valet töms i samma steg, så upprepade händelser ger `NothingSelected`.
- **För lite pengar:** `NotEnoughMoney`. Inget dras och inget levereras. Godiset går tillbaka till hyllan och skylten säger vänligt ”Du behöver N mynt till”, där de saknade mynten visas bleka.
- **Avbryt:** godiset släppt någon annanstans går tillbaka till hyllan (`Cancel`), utan kostnad. Detsamma gäller när spelmenyn öppnas, fokus förloras eller handen tappar spårningen.
- Bara ett godis från hyllan kan hållas åt gången. `TryBuy(sort)` kräver att sorten är den valda, så det som visas på skylten är alltid det som köps.
- **Påsen:** spelarens egen papperspåse på godisdisken. Antal per sort räknas obegränsat, men högst 3 bitar per sort visas (sammanlagt högst 9 objekt) och en siffra per sort står på påsen. Godis i påsen kan plockas upp och går tillbaka till påsen när man släpper det. Det kan inte köpas igen.
- **Prislappar:** en per sort på hyllan, med lika många myntbilder som priset. Grön när saldot räcker, grå annars.

### Sälja godis till ledsna kunder (EPIC-002 S8, spelarens önskemål)

- **`CandyCustomers`** (ren logik), tillstånd: `Away` (paus 4 s) → `Arriving` (1,5 s) → `Waiting` (ledsen, vill ha ett godis) → `Happy` (2,5 s, har betalat) → `Leaving` (1,5 s) → `Away`. Vilket godis kunden vill ha slumpas deterministiskt, och aldrig samma två gånger i rad. Den första ledsna kunden kommer 4 s efter start.
- **Ledsen, men aldrig ledsnare:** hängande mun, en tår och ett lugnt gungande. Ingen timer. Kunden står kvar tills hon fått rätt godis eller tills Nytt spel.
- **Sälja:** ta godiset från hyllan, vilket som vanligt är gratis förhandsvisning, och släpp det i kundens utsträckta hand (fångstradie 8 cm). `TryServe(godis)`: rätt godis i `Waiting` ger pris till plånboken, och kunden blir glad (hjärtan, pling per mynt). Det sker exakt en gång. Vid fel godis eller fel tillstånd ändras inget och godiset går tillbaka till hyllan.
- **Betalningen sker direkt** när kunden tar emot godiset. Mynten flyger till plånboken på skylten. **[A]** Ingen myntbricka eller kassa vid godisståndet: disken är för liten, och betalytan är spelarens egen köpyta (§12 ovan).
- Spelarens egen påse kan inte säljas ur. Betalytan köper som förut, oavsett kund.
- Skylten Öppet/Stängt pausar nya kunder vid **båda** stånden. Den som redan väntar stannar.
- Desktop: **G** ger det förhandsvisade godiset till kunden.

### Placering

- **Ändrat 2026-10-31 efter återkoppling på headset:** godiset stod för tätt, och de ledsna kunderna behöver synas. Godisdisken är nu **56 × 34 cm** med hylla, betalyta, påse och kundens hand. Rosa och mintgrön, med ett rosa/vitt randigt tak.
- Den står med mitten ungefär **125° till höger** om spelarens startriktning, 0,42 m från ögonen, och är vänd mot spelaren. Tre vridningar med spaken (3 × 45° = 135°) gör att den står nästan rakt fram.
- **Uppställning (efter återkoppling på headset om att kunden stod för nära och armen gick genom godiset):** vänstra halvan har hyllan bak (tre sorter, 13 cm mellanrum) och betalytan fram. Mitt fram står spelarens påse. Den ledsna kunden står bakom högra halvan, ca 0,9 m från ögonen med ansiktet ungefär i ögonhöjd, och sträcker handen rakt fram till disken fram till höger. Armen passerar aldrig över godiset eller påsen. Skylten ovanför hyllan visar kundens önskemål.
- Allt man behöver nå ligger högst 0,53 m från ögonen och inom ±35° från diskens riktning. Betalytan och kundens hand ligger högst 0,40 m bort, och kunden står minst 0,8 m bort. Att släppa ett godis där det stod köper eller säljer aldrig något. Låses av `Layout_CandyWithinSeatedReach`, `Layout_CustomerArmClearsCandyAndBag` och `Layout_CandyStandClearOfFruitCounter`.
- **Varför så långt åt höger:** fruktdisken (1,1 m bred, 32 cm framför ögonen) täcker redan ungefär −60° till +80°. En större disk inom sittande räckvidd får bara plats längre bak. Fruktståndet är oförändrat. Det gör att godisståndet syns sämre från fruktståndet än förut. Den höga klubbskylten och stationsknapparna i EPIC-003 ska kompensera för det.
- Disken har samma höjd som fruktdisken och följer samma höjdjustering. Kunden står på en liten pall om disken är hög, så att armen alltid når över disken.

## 13. Samspel och tillstånd (EPIC-002) – delvis ersatt

> Skylten Öppet/Stängt, Töm korgen (nu *Återställ beställningen*), Nytt spel, placering vid omcentrering och snap turn gäller fortfarande. Kassan och menyn beskrivs i §15 och §19.

- Godisståndet är alltid öppet. Fruktståndet kan stängas med skylten Öppet/Stängt eller med menyn (§4).
- En pågående fruktbeställning står kvar medan man handlar. Ingen timer finns.
- Spelmeny och fokus: samma skydd som i EPIC-001. Det man håller går hem och köpet avbryts. Efter återkomst krävs släppta knappar. Skylten Öppet/Stängt slår bara om för en hand som kommit utifrån, så en hand som redan är i skylten när man kommer tillbaka från en meny gör ingenting.
- **Nytt spel** är det enda som tömmer pengar och godis.
- **Ledsna kunder vid godisståndet (EPIC-002 S8):** se §12. Skylten Öppet/Stängt pausar nya kunder vid båda stånden.
- **Placering av stånden (VR):** stånden ställs framför spelaren först när headsetet sitter på (närvarosensorn) och golvorigo är aktivt. De flyttas framför spelaren igen vid en systemomcentrering (håll Meta-knappen; upptäcks som ett hopp i huvudets pose, eftersom Unitys händelse inte kommer vid manuell omcentrering) och via menyvalet **Flytta marknaden hit**. *Regeln att stånden flyttas när headsetet tas på långt bort är borttagen i EPIC-003 (§18).* Under spel flyttas de aldrig av sig själva. Diskhöjden räknas då om från ögonhöjden. Teleportering finns inte; stånden kommer till spelaren i stället (§9).

## 14. Lilla marknaden (EPIC-003) – kompakt marknad

Spelaren är säljaren vid tre stånd: **Frukt & grönt**, **Godis** och **Leksaker**. Barn som köper leksaker går till ett **lekbord** och leker. Pengarna går till en gemensam kassa och används inte till något annat.

> **Ändrat 2026-10-31 (kompakt marknad):** halvcirkeln med 1,5 m radie och de fem teleportplatserna är **ersatta**. Alla stånd och lekbordet står runt en liten gemensam arbetsyta. Spelaren står i mitten och vänder sig och tar korta steg. Ingen teleportering, inga stationsknappar, inga markeringar på marken.

### Planvy (marknadens koordinater i meter: origo = arbetsytans mitt, +z = mot fruktståndet, +x = åt höger)

```
 z (m)                 kund ○ (frukt)
  1,20 ·                    
  1,00 ┼ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┼  fysisk yta 1,5 × 2,0 (x ±0,75, z ±1,00)
  0,85 │     ┌─────────── FRUKT & GRÖNT ───────────┐   │
  0,55 │     └─ lådor ─ korg ─ bricka ─ kassa ─────┘   │  innerkant
       │ ┌──┐                                     ┌──┐ │
       │ │L │  ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐  │ G│ │
  kund○│ │E │       arbetsyta 1,1 × 1,1             │ O│ │○ kund
 (x −1,2)│K │  │    (fri golvyta, x/z ±0,55)    │  │ D│ │(x 1,2)
  0,00 │ │S │               ● spelaren              │ I│ │
       │ │A │  │                                 │  │ S│ │
       │ │K │   ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─   │  │ │
       │ └──┘                                     └──┘ │
 −0,55 │     ┌─ boll ──── bilväg ──── klossar ─────┐   │  innerkant
       │     │            LEKBORD 1,0 × 0,5        │   │
 −1,00 ┼ ─ ─ ┼ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┼ ─ ┼
 −1,05       └─────────────────────────────────────┘
 −1,25          ○ barn          ○ barn          ○ barn   (på pallar)
       x: −0,85  −0,55          0            0,55  0,85
```

Sidostånden är 0,30 m djupa (x −0,85…−0,55 och 0,55…0,85) och 1,0 m långa (z −0,5…0,5). Kunder (○) och barn står utanför den fysiska ytan. Det går bra, eftersom de bara är virtuella och spelaren aldrig behöver gå dit.

| Del | Mitt (x ; z) | Mått (bredd × djup) | Vänd mot | Innerkant |
|---|---|---|---|---|
| Frukt & grönt | (0 ; 0,70) | 1,0 × 0,30 m | mitten | z = 0,55 |
| Godis | (0,70 ; 0) | 1,0 × 0,30 m | mitten | x = 0,55 |
| Leksaker | (−0,70 ; 0) | 1,0 × 0,30 m | mitten | x = −0,55 |
| Lekbord | (0 ; −0,80) | 1,0 × 0,50 m, kant 2,5 cm | mitten | z = −0,55 |
| Arbetsyta (fri golvyta) | (0 ; 0) | 1,1 × 1,1 m | – | – |
| Fysisk yta (konservativ, inte uppmätt) | (0 ; 0) | 1,5 × 2,0 m | – | – |

- **Grunda diskar** (0,30 m i stället för 0,40). Lådor, korg, betalningsbricka, kassa och skylt ligger på den inre halvan, högst 0,16 m från innerkanten. Samma uppställning på alla tre stånd: tre lådor till vänster, korgen i mitten, brickans plats och kassan till höger.
- **Var spelaren står:** ungefär 0,30 m innanför en disk eller bordet, alltså (0 ; 0,25) för frukt, (±0,25 ; 0) för godis och leksaker och (0 ; −0,25) för lekbordet. Hon behöver bara vända sig och ta ett kort steg. Kroppen behöver aldrig vara mer än 0,50 m (x) eller 0,75 m (z) från mitten. Det ger 0,25 m marginal till den konservativa fysiska ytan.
- **Räckvidd:** inget som behövs ligger mer än 0,20 m utanför den yta där kroppen får vara. Barnens sida av lekbordet (bollens och tornets startplatser) är barnens, och spelaren behöver inte nå dit.
- **Kunder** står utanför diskarna, alltså utanför den fysiska ytan: frukt vid z ≈ 1,2, godis vid x ≈ 1,2 och leksaker vid x ≈ −1,2. De går in och ut längs ytterkanten.
- **Barnen** sitter på pallar på andra sidan lekbordet (z ≈ −1,25). Barnet som köpt en leksak går från leksaksståndets kundplats först utåt och sedan längs utsidan till sin plats. Gångvägen korsar aldrig arbetsytan.
- Möblernas mått och innehåll är anpassade var för sig. Figurer, varor och leksaker har sin vanliga storlek, och inget är nedskalat.
- **Marknadens markering är ingen gräns.** Quests systemgräns (Guardian) gäller alltid och ska vara satt runt rummets verkliga yta.
- Disken och bordet har samma höjd och följer höjdjusteringen.
- En bildskylt på taket visar varje ståndets kategori. Färger: frukt rött och vitt, godis rosa och vitt, leksaker blått och gult.

## 15. Varukatalog, priser och kassa (EPIC-003)

| Kategori | Varor och pris |
|---|---|
| Frukt & grönt | äpple, banan, morot, päron, apelsin, jordgubbe, vindruvsklase, vattenmelonskiva: 1 per vara. **Tre åt gången** är aktiva (§26) |
| Godis | klubba 1, polkagris 2, godispåse 3, gelébjörn 1, chokladkaka 3, karamell 1, regnbågsrem 2, godisring 2. **Tre åt gången** (§27) |
| Leksaker | boll 2, bil 3, kloss-set 4 (sex klossar), snurra 2, leksakståg 4, stapelringar 3 (tre ringar och en pinne). **Tre åt gången** (§27) |

- `Ware` (ren logik) har kategori, pris och namn, och `Models` ger bilden. Priserna för godis behålls från EPIC-002. Frukt/grönt får nu 1 per vara i stället för 1 per köp, eftersom betalningsbrickan ska motsvara en ordersumma (prisregeln från epicen).
- **Kassan** (`Wallet`) krediteras bara i `Shop.TryPay` med `Order.Sum`, i den atomära övergången `AwaitingPayment → Thanking`. Den kan aldrig debiteras och aldrig bli negativ. Invariant: kassan är summan av alla registrerade orders summor.
- Kassan (myntbild och siffra) syns vid varje stånds kassa och visar samma summa.
- Ingen sparning mellan appstarter.

## 16. Gemensam försäljningsloop och kunder (EPIC-003)

1. En kund kommer till ståndet för sin kategori och visar beställningen i en bubbla: en bild per vara, ett antal per sort och **ordersumman** (mynt och siffra).
2. Spelaren plockar ur ståndets lådor (obegränsat lager) och lägger i ståndets korg (överlämningsytan, 4 platser).
3. Fel vara, fel kategori eller för många matchar inte. De kan tas ur utan straff. En vara från en annan kategori tas inte emot och går hem.
4. Exakt innehåll gör att kunden lägger fram en **betalningsbricka** med summan (siffra och myntbilder).
5. Brickan i ståndets kassa registrerar köpet exakt en gång, och kassan ökar med summan.
6. Kunden tackar och tar varorna. Frukt-, grönsaks- och godiskunder går därifrån. Leksakskunder är barn som går till lekytan (§17).

- **En aktiv beställning åt gången** för hela marknaden, alltid från en kategori. Ordningen: kund 1 vill ha en vara av den första aktiva frukt/grönt-sorten (EPIC-009; tidigare ett äpple). Därefter turas kategorierna om, frukt/grönt → godis → leksaker. Leksaker erbjuds bara om en lekplats för någon leksak är ledig (annars hoppas de över), och platsen **reserveras när beställningen skapas**.
- Storlek: frukt/grönt 1–3 varor (kund 2–3 högst 2), godis 1–2 varor och leksaker exakt 1. Samma beställning kommer aldrig två gånger i rad.
- Godiskunder är ledsna (tår) tills köpet är klart (Spelarens önskemål). Leksakskunder är barn, alltså mindre figurer. Barn kan också handla frukt eller godis **[A: ibland, utan särskild betydelse]**.
- **Öppet/Stängt** (dra handen genom skylten på varje stånd, eller via menyn) pausar nya kunder. En kund som redan är där stannar.
- Ingen timer. Kunder väntar lugnt, och ingen intäkt kan förloras.

## 17. Lekytan och barnens lek (EPIC-003)

- Lekbordet (1,0 × 0,5 m, rundade hörn, låg kant, §14) har tre **lekplatser** (EPIC-010: lekzoner): **vänster** (boll eller snurra), **mitten** med den ovala vägen (bil eller tåg) och **höger** (klossar eller stapelringar). Ett barn i taget per plats, alltså högst tre lekande barn. Barnen sitter på pallar på bordets bortre sida, och spelaren står vid den närmre.
- `PlayArea` (ren logik) har platsernas tillstånd `Free → Reserved` (beställningen skapad) `→ Walking` (köpt, barnet går dit med leksaken, 3 s) `→ Playing` (45 s) `→ Leaving` (barnet går med leksaken, 2 s) `→ Free`. Nytt spel frigör alla platser.
- **Leksakens tillstånd:** `Shop` (i lådan) → `Handover` (i korgen) → `Carried` (barnet bär den) → `ChildPlay` (barnet styr) ⇄ `PlayerHeld` (spelaren håller) ⇄ `Free` (rullar fritt efter släpp) → `Resetting` (på väg tillbaka till sin plats) → borttagen när barnet går.
- **Samma objekt:** bollen och bilen som läggs i korgen är exakt de objekt barnet bär och leker med. Kloss-setet är en liten stapel av sex klossar som på lekbordet blir sex stora klossar.
- **Barnets lek (enkel och förutbestämd):**
  - **Boll:** ligger bollen still nära barnet rullar barnet den över bordet mot spelarens sida. Ligger den på spelarens sida väntar barnet. Efter 10 s utan spelare hämtar barnet den själv. Spelaren kan rulla tillbaka eller kasta (EPIC-017, §33).
  - **Bil:** barnet kör bilen längs den ovala vägen (0,15 m/s).
  - **Klossar:** barnet och spelaren bygger ihop på en gemensam byggplats och turas om (EPIC-017, §33). Tidigare byggde barnet ett torn, en kloss varannan sekund, med alla klossar.
- **Kontroll är exklusiv:** när spelaren håller leksaken styr barnet den inte och väntar vänligt. Efter släppet är leksaken fri (fysik) tills den ligger still. Då tar barnet över igen: bollen om den ligger på barnets sida, bilen från närmaste punkt på vägen och klossarna en i taget.
- **Lugnt avslut:** när leken är slut går barnet. Men aldrig medan spelaren håller barnets leksak, och inte inom 8 s efter att spelaren senast rört den. Då förlängs leken. Barnet tar aldrig något ur spelarens hand.
- **Tappade leksaker:** en leksak som legat på golvet (mer än 0,25 m under bordet) i 2 s glider tillbaka till sin plats, gratis. **Städa lekytan** (stor bildknapp vid bordet och i menyn) lägger alla leksaker som inte hålls tillbaka på sina platser. Det rör inte kassan.

## 18. Fysisk rörelse och placering (EPIC-003 + EPIC-005)

> **Ersätter** ”Teleportering och platser” (bildknappsrad, fem platser och markeringar på marken), 2026-10-31.

- Spelaren rör sig fysiskt inom den lilla ytan: hon vänder sig och tar korta steg. Alla stånd och lekbordet går att använda direkt där de står. Ingen station behöver väljas.
- **Uttrycklig placering:** vid start, vid systemomcentrering (håll Meta-knappen), med menyvalet **Flytta marknaden hit** och med **Rikta in marknaden** (kalibrering mot rummets spelyta, §24) placeras marknaden så att arbetsytans mitt hamnar där spelaren står, och så att fruktståndet hamnar rakt fram. Höjden räknas om från ögonhöjden.
- **Ingen automatisk omplacering:** fysisk rörelse flyttar aldrig marknaden. Inte heller att ta av och på headsetet längre bort gör det. Den tidigare regeln ”tas headsetet på mer än 0,6 m bort flyttas stånden” är borttagen.
- Ingen automatisk vridning. Fysisk vändning och fysiska steg räcker för hela loopen. För sittande spel finns spakarna (EPIC-005):
  - **Gång (vänster spak):** riggen flyttas, och marknaden står kvar. Riktningen är headsetets yaw. Att titta upp/ned eller luta huvudet ändrar varken fart eller höjd. 0,5 m/s vid fullt utslag **[A]**, bildfrekvensoberoende, radiell dödzon 0,2, och diagonalt inte snabbare än rakt. Aldrig vertikalt.
  - **Vridning (höger spak vänster/höger):** 30° per utslag runt huvudets aktuella position, så att ett huvud som är förskjutet från riggens mitt inte hoppar. Ingen mjuk vridning.
  - **Höjd (höger spak upp/ned):** alla diskar och lekbordet, med varor, korg, bricka, kassa och leksaker. Spelaren och golvet flyttas inte. Höger spak låser den axel som dominerar när den lämnar dödzonen (0,3) tills den är tillbaka nära mitten (< 0,25).
  - **Möbler:** diskar och lekbord har golvavtryck. Spakgång stoppas när huvudet kommer närmare än 0,18 m och glider längs kanten. Allt som behövs ligger inom 0,45 m vågrätt från närmaste plats spaken kan ta en till **[A, H]**. Fysisk rörelse begränsas aldrig: den spårade huvudpositionen ändras inte. Står man för nära kan spaken bara föra en därifrån.
  - **Grepp under rörelse:** handens fart mäts utan riggens rörelse, så gång eller vridning ger ingen kastfart.
  - Spelmeny, fokusförlust, paus eller headset av stoppar gång, vridning och höjd. Efter återkomst krävs att alla knappar släppts och båda spakarna är neutrala.
  - Spakrörelse flyttar aldrig marknaden och räknas aldrig som omcentrering. Den spårade huvudposen, som omcentreringen läses från, påverkas inte.
- Desktop: en separat kameranavigering för utveckling. Tab växlar mellan översikt, frukt, godis, leksaker och lekbordet. Ingen teleportering i spelet.

## 19. Menyer, fokus och återställning (EPIC-003)

- Spelmenyn: Fortsätt, Visa kontroller, Stäng/Öppna marknaden, **Återställ beställningen** (korgen töms, samma kund och samma order), **Städa lekytan**, Flytta marknaden hit och **Nytt spel** (kräver Ja, Nej är förvalt, och återställer kassa, kunder, lek och plats). Återställ och Städa rör aldrig kassan.
- Utan fokus, vid paus, med headsetet av eller med menyn öppen: inga köp eller grepp. Det man håller går hem. En leksak läggs stilla på sin lekplats, och barnet fortsätter. Efter återkomst krävs släppta knappar.
- Order, kassa och lek finns kvar genom vanlig paus.

## 20. Teknik och gränser (EPIC-003)

- Fysikmodulen (`com.unity.modules.physics`) används bara för leksaker, lekbord, kant och mark. Den stegas från `FruitStand.Step` (skript-läge, delsteg på högst 1/72 s), så den är deterministisk i testerna och fungerar likadant i VR och desktopläge.
- Högst 8 fysikkroppar (1 boll, 1 bil, 6 klossar) och högst 4 figurer (1 kund + 3 barn).
- Logiken (`Ware`, `Order`, `Shop`, `Wallet`, `PlayArea`, `StackHelp`) är skild från XR, fysik och presentation.
- Desktop: 1–9 lägger vara 1–9 i den aktiva korgen, mellanslag betalar, backsteg tar ur, Tab växlar kameravy (bara desktop), O öppnar/stänger, C återställer beställningen, P städar lekytan och R två gånger startar nytt spel.

## 21. Kund från mobilen (EPIC-004)

Föräldern beställer som kund från en webbsida på mobilen. Spelaren säljer som vanligt i VR. Vägvalet är prövat i [SPIKE-004](SPIKE-004.md).

```
 Mobil (webbläsare)  ──WebSocket──►  MacBook: relä (Node, port 8787)  ◄──WebSocket──  Quest
   önskemål: order, foto, sync         mobilsidan, sessionskod, förmedling,      auktoritativ: order,
   visar status                       foto i minnet, UDP-fyr (port 47811)       varor, betalning, kassa
```

- **Quest är auktoritativ.** Mobilen skickar bara önskemål: `order` (order-id, 1–3 varunamn, foto-id), `photo` och `sync` (fråga om status). Quest validerar, räknar ut summan från sina egna priser och svarar med `status`: `waiting`, `serving`, `done`, `cancelled`, `rejected` (orsak `busy`, `items`, `id`, `paused` eller `inactive`) eller `unknown`. Relän släpper bara igenom dessa typer och fält. Inget meddelande kan ändra kassa, orderstatus eller betalning.
- **Regler** (`RemoteCustomer`, ren logik):
  - En mobilkund och en aktiv mobilorder åt gången. En annan order under tiden avvisas med `busy`.
  - Bara frukt/grönt eller godis, 1–3 varor och en kategori. Leksaker kan inte beställas, eftersom de kräver en lekplats och ett barn.
  - Mobilordern tar **nästa lediga kundtur**. En NPC som redan står vid disken avbryts aldrig, och inga nya NPC-kunder kommer medan mobilkunden väntar eller betjänas. Stängt pausar även mobilkunden. Efter mobilkunden kommer NPC-kunder igen.
  - Mobilkunden säljs exakt som andra kunder (korg, bricka, kassa). `Shop.TryPay` krediterar kassan exakt en gång och markerar mobilordern `done` i samma övergång.
  - Samma order-id igen ger aldrig en andra kund eller betalning, bara den aktuella statusen (`duplicate`). Avslutade id:n kommer man ihåg under spelpasset.
  - Nytt spel avbryter en pågående mobilorder (`cancelled`, orsak `newgame`) utan betalning.
- **Anslutning:** relän startas med `scripts/kund.sh start`. Den visar QR-kod, adress och en sessionskod på 4 siffror i terminalen och på en värdsida (bara från datorn själv). Quest hittar relän via fyren. Reserv för utvecklaren: `relay.txt` med `ws://adress:port/ws` i appens datakatalog. Utan relä är spelet som vanligt.
- **Avbrott:** Quest behåller en accepterad order och återansluter själv (tystnad i 15 s räknas som avbrott, och relän pingar var 5:e s). Mobilen återansluter själv. En order som Quest bekräftat följs upp med `sync`, och en obekräftad skickas igen med samma id. Är ordern okänd för Quest (appen omstartad) visar mobilen det och skapar ingen ny order av sig själv.
- **Foto:** mobilen beskär till mittkvadrat och skalar ned till 128 × 128 (JPEG, PNG, WebP och HEIC/HEIF; högst 20 MB in). Fotot skickas en gång som råa RGBA-rader (cirka 87 kB base64). Relän behåller senaste fotot per mobil i minnet och ger det till Quest bara när Quest saknar det. Quest tar bara emot kvadratiska 16–128 px med exakt rätt längd, avkodar varje id en gång och har högst 3 i minnet. Ingenting sparas på disk och ingenting skickas till internet. Fotot visas som en inramad porträttbild framför kundens ansikte, och hatten döljs. Utan foto visas standardfiguren.
- **Mobilsidans lägen:** ansluter, ansluten (”väntar på spelet” om Quest saknas), väntar på tur, beställning mottagen, köp klart (”Tack, köpet är klart!”) och frånkopplad.
- **Teknik:** inga nya Unity-paket (`System.Net.WebSockets.ClientWebSocket` och `UdpClient` ingår i runtimen) och inga npm-paket (Node 22 eller senare). Nätverket körs på en bakgrundstråd, och reglerna körs bara på huvudtråden. `QuestBuild` begär `android.permission.INTERNET`. `RelayLink` startar inte i batch-tester om inte testet ber om det.
- **Nätverkskrav:** samma wifi eller LAN för MacBook, Quest och mobil, utan klientisolering (gästnät har det ofta). Se README.

## 22. Spårbara byggen

- `scripts/quest.sh build` ger varje lyckat bygge ett bygg-id: `<datum>_<tid>_<kort sha>`, med tid i Europe/Stockholm och `-dirty` om arbetskopian hade ocommittade ändringar (spårade eller nya filer). APK:n arkiveras som `Builds/archive/lilla-fruktstandet_<bygg-id>.apk` med manifestet `<samma namn>.json` bredvid. Manifestet innehåller bygg-id, ISO-tid med tidszon, git-commit, `gitDirty`, Unity-version, application-id, versionName, versionCode och sha256. Ett bygge med lokala ändringar får också `<namn>.source.tar.gz` med källkoden det byggdes från.
- Arkiverade filer skrivs aldrig över (noclobber, skrivskyddade) och rensas aldrig automatiskt. Ett misslyckat bygge arkiveras inte.
- Application id `com.lillafruktstandet.prototype` och versionCode **1** är oförändrade, vilket är det befintliga flödet. `adb install -r` accepterar samma versionCode, så varje bygge installeras som en uppdatering, och ett äldre arkiverat bygge kan installeras tillbaka utan avinstallation. versionName är `1.0+<bygg-id>` (`scripts/quest.sh installed` visar det utan att störa spelet).
- Appens meny visar `Bygge <bygg-id>`, och ”(lokala ändringar)” för dirty-byggen. Bygg-id loggas vid start.
- `scripts/quest.sh install [apk]` installerar en uttrycklig APK (standard: senaste bygget). Skriptet skriver först ut vilket bygge det är: manifestet om hashen stämmer, annars ”okänt bygge” och vad APK:n själv anger.

## 23. Krokodilspelet (EPIC-006)

- **Plats:** en leksakskrokodil står alltid på en egen liten hylla på lekbordets ände vid godisståndet, på spelarens sida. Hyllan sitter i bordsskivans höjd och följer höjdjusteringen. Krokodilen ligger på längden med nosen mot bordet, och munnen öppnas uppåt. Hyllan har ett eget golvavtryck för spakgången (§18). Den ligger utanför bordsskivan och barnens gångväg, så boll, bil, klossar och barn påverkas inte. Krokodilen har varken fysik eller kolliderare, så käften kan inte fånga eller knuffa något.
- **Spelet** (`CrocodileGame`, ren logik med injicerbar slump):
  1. Munnen är öppen. En av åtta tänder är utlösande och slumpas på nytt varje omgång. Samma tand får komma igen.
  2. En tand trycks ned som städknappen: grepp eller avtryckare intill tanden, ett nytt tryck. En säker tand sjunker och förblir nedtryckt, och varje tand kan tryckas en gång per omgång.
  3. Den utlösande tanden slår igen munnen (0,15 s), med ett lågt ”klack” och en kort vibration i handen som utlöste den. Omgången räknas som ett snäpp, exakt en gång.
  4. Munnen förblir stängd en stund (1,5 s) och öppnas sedan av sig själv (0,5 s). Tänderna åker upp och en ny utlösande tand slumpas. Under stängning, paus, dans och öppning ignoreras alla tryck.
  5. En hand som tryckt en tand måste lämna tandens tryckyta (samma räckvidd som för grepp) innan den kan trycka den igen. Vid återöppning och när spelet återupptas räknas tänder som handen redan är vid som spärrade tills handen lämnat dem.
- **Överraskning:** efter femte snäppet, en gång per spelpass, dansar krokodilen i 2,5 s (vickar, gungar, hoppar lite) när ingen hand är i munområdet. Sedan står den exakt på sin plats och öppnar för nästa omgång. Ingen räknare och ingen förklaring visas. **Nytt spel** återställer räknaren och överraskningen. Städa lekytan och Återställ beställningen gör det inte.
- **Paus:** spelmeny, systemmeny, fokusförlust, paus och headset av blockerar tryck och stoppar krokodilens tidur.
- Ingen koppling till pengar, beställningar eller barnens lek. Barnen spelar inte krokodilspelet.

## 24. Rikta in marknaden mot spelytan (EPIC-007)

- **Systemgränsen** (Quests spelyta och Guardian) är systemets egen. Appen läser den bara och ändrar den aldrig. Spelets egen **golvmarkering** visas bara under kalibreringen och är ingen garanti för fritt golv.
- **Vad Questen ger** (SPIKE-007): bara spelytans mått (1,71 × 1,86 m på den testade Questen), ingen form, position eller orientering. Förslaget är en rektangel med de måtten, centrerad i spårningsorigo längs spårningsaxlarna **[A]**. Föräldern jämför förhandsvisningen med systemgränsen och bekräftar eller justerar.
- **Koordinater:** kalibreringen görs i spårningsrummet (det verkliga rummet) och omvandlas till världen genom riggens aktuella pose först vid bekräftelse. Marknadens rot bär stånd, lekbord, krokodilhylla, golvavtryck och golvmarkering, och markeringen byggs från samma layout.
- **Layouten anpassas, skalas inte:** avståndet mitt–stånd `d` är 0,70–0,90 m (steg 0,05), och lekbordet ligger `d + 0,10` bakom. Det ryms när arbetspositionerna ligger inom kroppens yta (spelytan minus 0,25 m) och alla nödvändiga handinteraktioner ligger högst 0,20 m utanför den. För en polygon måste den behövda rektangeln ligga helt inne i polygonen. Största `d` som ryms väljs. Vid 0,70 behövs 1,59 × 1,80 m. Questens 1,71 × 1,86 m ger `d` = 0,75.
- **Kalibrering** (menyn **Rikta in marknaden**): förhandsvisning (grön om det ryms, röd om inte) av layoutens yta, kroppens yta, ståndens, lekbordets och hyllans avtryck och en pil mot fruktståndet, plus konturen av den inställda ytan. Rader: Bekräfta, Bredd ±0,1 m, Längd ±0,1 m, Vrid ±15°, Mitten där jag står, Vrid dit jag tittar, Använd systemets spelyta (om måtten finns) och Avbryt. Utan systemmått börjar inställningen manuellt på 1,5 × 2,0 m där föräldern står. Bekräfta går bara när det ryms och flyttar då hela marknaden på en gång. Annars behålls placeringen.
- **Aldrig automatiskt:** gång, spakrörelse, headset av och `boundaryChanged` flyttar inte marknaden. Vid `boundaryChanged` står ”Spelytan har ändrats: välj Rikta in marknaden” i menyn. Kalibreringen gäller spelpasset, och varje start börjar kompakt (0,70).

## 25. Marknadsfest och vilopaus (EPIC-008)

- **Omgång:** intäkten i omgången räknas från de försäljningar som `Shop.TryPay` registrerar, exakt en gång per köp. Kassan rörs aldrig. Tröskeln är **80** **[A: hypotes; tiden till 80 loggas och följs upp i PLAYTEST]**. Utvecklaren kan sänka den med `fest.txt` i appens datakatalog. Det finns ingen genväg bland barnets kontroller.
- **Tillstånd** (`MarketRound`, ren logik): Pågående omgång → Fest väntar → Firande (12 s) → Vilopaus → (föräldern) Ny omgång. Ta vilopaus leder direkt till Vilopaus, utan stjärna och utan krav på tröskeln.
- **Fest väntar:** inga nya NPC-kunder. Kunden vid disken tackar och går, och en mobilorder som redan accepterats betjänas klart. Nya mobilorder avvisas med `paused`, direkt och utan att sparas. Firandet börjar först när ingen kund och ingen mobilorder återstår, inget barn är på väg till lekbordet, krokodilen inte dansar sin egen dans, händerna är tomma (annars visas en bild med en öppen hand och texten ”Släpp det du håller”) och ingen meny, kalibrering eller fokusförlust pågår.
- **Firande:** skylten över fruktståndet visar ”Marknadsfest!” med stjärnor, och festtavlan på fruktståndet får en guldstjärna. Kunden och barnen vinkar. Mjuka fyrverkeriblommor (2,9–3,5 m upp) och dalande konfetti (försvinner vid 2,45 m) syns, högst 120 effektobjekt och minst 0,8 m från ett barnhuvud. Krokodilen gör en festdans som inte räknas som dess påskägg. En mjuk fanfar spelas. Fokusförlust och menyer stoppar firandets tidsförlopp. Firandet sker högst en gång per omgång.
- **Vilopaus:**
  - Skylten visar ”Pausdags!”, och en tavla framför spelaren visar ett avtaget headset och texten ”Ta av headsetet och vila en stund.”
  - Inga kunder, ingen försäljning, inga grepp, inget krokodilspel och ingen lek. Det som hålls går hem.
  - Ljudet sänks till 35 %. Ingen nedräkning och ingen bonus.
  - Pausen efter festen avslutas bara av föräldern: menyn → **För föräldrar** → **Starta nästa omgång** → Ja (Nej är förvalt), eller från mobilen. *(EPIC-012: en vilopaus från menyn och en föräldrapaus avslutas utan ny omgång, §29.)*
  - Nästa omgång behåller kassan och stjärnorna, nollställer intäkten, och kunder och lek fortsätter.
- **Nytt spel** nollställer allt, även stjärnorna och pausen.
- **Mobilen** (praktisk föräldrakontroll i den lokala sessionen, ingen säker spärr):
  - Delen **Förälder** har knapparna Pausa spelet och Starta nästa omgång, med bekräftelse.
  - Föräldrakommandon skickas som ett eget meddelande (`parent`, bara `rest`/`nextRound`, ett id per kommando) och verkar exakt en gång.
  - Quest kvitterar (`parentAck`). ”Spelet är pausat” visas först när Quest skickat marknadens status (`market`: `playing`/`party`/`resting`), som relän sparar och ger till nya mobiler.
  - Vid bruten anslutning står ”Inte bekräftat”. Under fest och paus är Beställ avstängd.


## 26. Växlande fruktsortiment (EPIC-009)

- **Katalog:** åtta frukt/grönt-sorter (§15), alla 1 per vara. Godis och leksaker är oförändrade.
- **Sortiment** (`Assortment`, ren logik med egen seedad slump): exakt tre olika frukt/grönt-sorter är aktiva i ståndets tre lådor. Lådor, skylt, högar, kundbubblor och mobilsidan visar samma tre.
- **Byte** (ny dragning, aldrig samma tre som nyss):
  - vid **Nytt spel**;
  - vid **Starta nästa omgång** efter vilopausen. Om en order pågår eller en mobilorder är accepterad, väntar bytet tills nästa kund ska komma och ingen mobilorder finns kvar.
  - Appstart drar ett sortiment från ett tidsbaserat frö.
  Aldrig vid paus (fokus/meny), omcentrering, höjdjustering, inriktning, Töm korgen eller under en beställning. Vid byte går frukt som hålls i handen eller ligger kvar hem och försvinner.
- **Beställningar:** NPC-kunder beställer bara aktiva sorter, med samma gränser som förut.
- **Mobilen:** `market` har fältet `fruit` (de tre aktiva nycklarna, i lådordning). Relän sparar det och ger det till nya mobiler. Mobilsidan visar bara de aktiva sorterna. En order på en inaktiv sort avvisas med `inactive` (sparas inte), och mobilen visar ”Den frukten finns inte i ståndet just nu”.
- **Modeller:** Blender-skript i `art/fruit/` som exporterar FBX till `Assets/Fruktstand/Models/`, med material färgade i koden (se SPIKE-BLENDER). Den procedurella modellen är reserv om en FBX saknas.

## 27. Godis och leksaker med växlande sortiment (EPIC-010)

- **Kataloger:** godis har åtta sorter, leksaker sju (§15; priser i [EPIC-010](EPIC-010.md); hoppgrodor 3 mynt, EPIC-018). Varje vara är ett greppobjekt i ståndet.
- **Sortiment:** `Assortment` väljer tre olika aktiva sorter i **varje** kategori, med samma bytesregler som frukt (§26): Nytt spel och nästa omgång, mellan kunder och aldrig med en öppen mobilorder. Lådor, skyltar, bubblor och mobilen visar de aktiva sorterna. NPC-order innehåller bara aktiva sorter.
- **Lekzoner:** varje leksak hör till en zon:
  - vänster: boll, snurra, hoppgrodor (EPIC-018);
  - mitten: bil, tåg;
  - höger: klossar, stapelringar.

  En zon har högst ett barn (högst tre lekande barn). En leksaksorder erbjuds och reserverar bara en ledig zon. Ett sortimentsbyte rör inte lekytan: pågående lek fortsätter tills barnet går.
- **Nya lekar:**
  - **Snurra:** barnet sätter den i snurr (≈ 3 s, saktar in, vilar, snurrar igen). Spelaren kan lyfta och ställa den; den snurrar igång när den landar stilla på bordet.
  - **Tåg:** följer vägen som bilen men långsammare. Det har ett eget lok och en vagn.
  - **Stapelringar:** barnet staplar en ring varannan sekund på pinnen, största först. Spelaren kan ta en ring (ringarna ovanpå lossnar) och släppa den inom 8 cm från pinnen, så snäpper den överst.
- **Kontroll, städning och tappade leksaker** som i §17, för alla sorter och delar.
- **Fysikbudget:** högst **8** fysikkroppar på lekbordet (1 + 1 + 6, den största leksaken per zon). Pinnen är fast.
- **Mobilen:** `market` har även `candy` (tre nycklar). Mobilen visar bara aktivt godis, och inaktivt godis avvisas med `inactive`. Leksaker kan inte beställas från mobilen.

## 28. Förlåtande leksaksfysik (EPIC-011)

- **Klossar:** ett släpp som överlappar en annan kloss (hela volymen) eller ligger nära ovanför den hamnar ovanpå den stapeln, om platsen där är ledig. Annars hamnar klossen på närmaste lediga plats på bordet inom 0,15 m. Finns ingen hamnar den där den låg innan spelaren tog den. Andra klossar flyttas inte, och inget tas ur handen. Fritt byggande fungerar som förut (placeringen är `BlockPlacement`, ren logik).
- **Alla leksaker:** ett släpp ovanför bordet som delvis ligger inne i skivan eller kanten lyfts upp och in på bordet. Ett släpp tydligt utanför bordet får falla, och tappade leksaker kommer tillbaka.
- **Gränser:** isärtryckning högst 0,3 m/s, vinkelhastighet högst 20 rad/s, släppfart högst 1,2 m/s. Riggens rörelse ger ingen släppfart.
- **Borttappat:** varje del har sin egen räknare (tidigare delade barnets alla delar en, så klossar kom aldrig tillbaka). Tillbaka till sin plats, eller till närmaste lediga plats om den är upptagen. Det som hålls återställs aldrig.

## 29. Paus, menyer och instruktioner (EPIC-012)

- **Pauslägen** (`MarketRound.RestBy`): barnets vilopaus (menyn *Ta vilopaus*), föräldrapaus (mobilen eller VR *För föräldrar → Pausa spelet*) och pausen efter festen.
  - Barnets vilopaus avslutas med menyns *Fortsätt spela*, eller av föräldern.
  - Föräldrapausen avslutas **bara** av föräldern: *Fortsätt spelet* på mobilen eller VR-föräldrasidan.
  - Pausen efter festen avslutas bara med *Starta nästa omgång*.
  - Att fortsätta efter en vanlig paus bevarar allt (order, kassa, omgångens intäkt, lek, stjärnor) och byter aldrig sortiment. *Starta nästa omgång* nollställer omgångens intäkt och byter sortiment mellan kunder (§26). Bara *Nytt spel* nollställer spelpasset.
  - Pausvyn visar skälet och vad som fortsätter spelet.
- **Mobilen:** föräldrakommandot `resume` (*Fortsätt spelet*). `market` har `restBy` (`child`/`parent`/`party`, tomt när man spelar).
- **Menyer i VR:**
  - En stråle från varje spårad kontroll; raden som träffas markeras, och avtryckaren på samma hand aktiverar den (blink och lätt vibration). Exakt en aktivering per klick.
  - Spak, A/X och B/Y fungerar som förut. Avtryckaren utan träff på panelen väljer markerad rad.
  - Med menyn öppen påverkas inte världen, och efter stängning krävs neutralt läge. Ingen meny utan fokus. Meta-knappen läses aldrig.
- **Layout:** raderna är egna knappar med mellanrum och radbryts till panelens bredd, och panelen växer med innehållet. Kontrollhjälpen har sidor. Återställande val är röda och *Fortsätt*-val har en egen färg. Panelen placeras när menyn öppnas och står sedan still.

## 30. Parken runt marknaden (EPIC-013)

- **Syfte:** marknaden står i en liten mysig park. Marknaden och lekbordet är mitten, och parken ramar in dem utan ny spelmekanik.
- **Innehåll** (`ParkLayout`, marknadens koordinater som i §14):
  - fem rundade low-poly-träd 3,4–4,0 m från mitten (två vid gången framåt, ett på varje sida, ett bakom);
  - en parkbänk vid leksakssidan (x ≈ −2,65), vänd mot marknaden;
  - två stenar, sex grästuvor och sex låga blomrabatter utanför gången;
  - en platt grusgång: en ring 1,0–1,85 m från mitten under kundernas och barnens vägar, plus en arm framåt och en bakåt (där barnen går hem), 1,1 m bred och ut till 8 m.
- **Fritt:** arbetsytan (gräs), stånden, lekbordet och krokodilhyllan, kundernas och barnens vägar vid alla avstånd 0,70–0,90 och gångens armar. All dekor står minst 1 m utanför ytan där kroppen får vara.
- **Mark:** helt platt. Gången ligger 1,5 mm över gräset och under kalibreringsmarkeringen. Ingen terräng.
- **Placering:** parken hör till marknadens rot. Den följer start, omcentrering, *Flytta marknaden hit* och kalibrering, men aldrig gång, spakrörelse eller huvudrörelse. Höjdjusteringen lyfter inte mark, träd eller bänk.
- **Interaktion:** inga kolliders och inget greppbart, så strålar, grepp och leksaker påverkas inte. Spakgång stoppas vid träd, bänk och stenar på samma sätt som vid diskarna (§18). Blommor och gräs går man igenom. Kalibreringens markering och passningsregler (§24) använder bara marknadens egna avtryck.
- **Quest:** paketmodellerna delar ett material och slås ihop till en mesh. Gången, skuggfläckarna och blommorna är några få platta eller enkla meshar. Ingen dekor kastar realtidsskuggor; skuggfläckar under träd och bänk ersätter dem. Ingen transparens och inget tätt gräs.
- **Källor:** träd, bänk, gräs och stenar kommer från *Low Poly Environment Park* (Palmov Island, Unity Asset Store, gratis, Standard EULA). Bara 13 filer importeras med `scripts/park-assets.sh`, och de committas inte. Utan dem byggs marknaden utan parkmodeller. Grus, skuggor och blommor görs i kod. Förteckning: [EPIC-013](EPIC-013.md#assetförteckning).

## 31. Parkens atmosfär och naturljud (EPIC-014)

- **Parken runt om:** utöver §30 finns grupper av träd och runda buskar i en bruten ring. Mittringen ligger på cirka 7,5 m (tre träd och en eller två buskar per grupp) och den yttre på cirka 15 m (tre till fyra större träd). Ingen sektor på mer än 70° saknar träd mellan 6 och 20 m. Längs gången framåt och bakåt står inget högt närmare än 1,5 m från gångens mittlinje (siktlinjer). Spakgång stoppas vid stammar och buskar. Inget är utforskbart.
- **Horisont:** tolv mjuka, låga kullar 60–80 m bort, de bortersta blekare. Gräsmattan har parkträdens gröna ton.
- **Naturljud** (`Core/Ambience` regler, `NatureSounds` uppspelning, `SoundSynth` syntes):
  - ingen ljudbädd: ett vind- och lövprassel provades och togs bort efter headsettestet (”direkt jobbigt”);
  - fågelkvitter från några av de närmaste träden (3D-ljud), med 7–18 s mellan dem, sex olika läten och aldrig samma träd eller läte två gånger i rad;
  - tydligt under återkopplingsljuden (pop, pling, tick, fanfar);
  - ingen bakgrundsmusik.
- **Inställning:** *För föräldrar → Naturljud: normalt / svagt / av*. Menyn stängs inte. Valet sparas mellan starterna och påverkar inget annat.
- **Paus och fokus:** naturljudet tystnar som allt annat ljud med menyn öppen, utan fokus, bakom systemmenyn och med headsetet av. Under tiden står fåglarnas klocka still, så inga fåglar sparas till en klump efteråt. Vid marknadens vilopaus fortsätter fåglarna.
- **Källor:** alla naturljud syntetiseras i koden (projektets egen kod). Parkens nya trädsorter (björk, högt träd) kommer från samma Asset Store-paket som §30.

## 32. Besökare i parken (EPIC-015)

- **Ankomst:** varje kund, mobilkunden också, kommer från en grind i slutet av gången framåt eller bakåt (bakom en buske, cirka 11 m bort). Kunden går längs gången och ringen (en slinga 1,72 m från mitten, i gruset) till sitt stånd och kliver fram längs disken. Försäljningen (`Shop`) väntar i *kommer* tills kunden är framme. Före det kan inget läggas i korgen och inget betalas. Bubblan säger *Hej!* när kunden är nära.
- **Avfärd:** efter tacket kliver kunden åt sidan längs disken under 1,5 s, som förut. Sedan är disken ledig och nästa kund börjar komma. Den gamla kunden fortsätter som **parkbesökare**: rakt ut genom närmaste grind (vikt 5), en omväg runt ringen till den andra grinden (3) eller en stund på bänken, 8–15 s (2). Valet görs med injicerbar slump.
- **Leksakskunder** går till lekbordet som förut. När leken är slut tar en parkbesökare över barnet på gången bakåt.
- **Gränser:** högst två parkbesökare samtidigt, utöver kunden och de lekande barnen. Omväg och bänk väljs bara när besökaren är ensam. Behövs en tredje tas den bort som är längst ut och närmast en grind. Bänken har en sits som reserveras och frigörs. Nytt spel tar bort alla besökare och frigör bänken.
- **Rörelse:** 1,1 m/s **[A, prövas på headset]**, bildfrekvensoberoende. Gungningen följer steglängden, svängarna är mjuka och fötterna är på marken. Avståndshållning (EPIC-016, `Pacer`): figurerna på gångarna bromsar mjukt bakom någon, kliver lite åt sidan (på gruset) runt den som står och väjer för varandra vid möten; kunden har förtur, sedan den besökare som kom först. Lekande barn, kunden vid disken och den på bänken räknas som stillastående. Ingen väntar mer än 3 s i sträck. Gången följer farten. Att sätta sig och resa sig tar 0,6 s: höften på sitsen, låren över sitsen och underbenen hängande.
- **Fritt:** rutterna går i gruset (bara sista biten till bänken och grindarna går på gräset), aldrig genom diskar, lekbord, krokodilhylla, bänk, barnens pallar eller arbetsytan, vid alla avstånd 0,70–0,90.
- **Placering och paus:** rutterna ligger i marknadens koordinater och besökarna hör till parken. De följer alltså kalibreringen och står kvar på marken när diskarna höjs eller sänks. Vid fest, vilopaus och föräldrapaus står kunden och besökarna still, som försäljningen.
- **Tempo:** ankomsten tar nu cirka 8–11 s i stället för 1,5 s, så tiden mellan kunderna blir längre, och tre barn vid lekbordet samtidigt blir ovanligare. `ParkRoutes.WalkSpeed` och grindarnas läge går att justera efter headsettestet.

## 33. Gemensam lek vid lekbordet (EPIC-017)

- **Bollkast** (`BallToss`, bordets koordinater):
  - En boll som släpps nära bordet rullar som förut (släppfart högst 1,2 m/s).
  - En boll som lyfts så att mitten är minst 8 cm över skivan och släpps medan handen rör sig framåt (vågrätt minst 0,25 m/s, bort från spelaren) blir ett mjukt kast i en båge. Bågen toppar 8 cm över den högre änden och har ingen luftbromsning. Farten är högst 2,4 m/s.
  - Inom 35° från barnets fångpunkt (−0,30; 0,10; 0,20) går kastet exakt dit, hur fort handen än rör sig: ett litet kast räcker. Annars landar det längs rörelsen, 0,20–0,60 m bort, och kan missa bordet.
- **Fånga och skicka tillbaka:**
  - Kommer en kastad boll inom 12 cm från fångpunkten fångar barnet den, håller den 0,5 s, lägger den i bollhörnet och rullar genast tillbaka.
  - En boll som landar fel hämtas som förut. Bollen på golvet kommer tillbaka med regeln för tappade leksaker (§28).
  - Riggens rörelse (spaken) ger ingen fart (§28).
- **Gemensamt bygge** (`BuildSite`):
  - Byggplatsen är ett rutnät med 3 kolumner och 3 lager, med mitten av den nedersta raden i (0,30; 0,04). En ruta är stabil på bordet eller på en upptagen ruta.
  - Barnets tre klossar ligger i raden närmast bygget och spelarens tre i raden närmast spelaren. Båda raderna ligger inom räckhåll (§14).
  - Barnet flyttar bara sina egna klossar, aldrig en som spelaren har tagit (förrän *Städa lekytan*), och aldrig en som står i bygget eller bär en annan.
- **Turordning:**
  - Barnet lägger en kloss (den första efter 2 s) i första lediga stabila ruta: mitten, vänster, höger, sedan lagret ovanför.
  - Sedan är det spelarens tur i upp till 8 s. Tiden står still medan spelaren håller en kloss. När spelaren har lagt en kommer barnets nästa efter 2 s.
- **Hjälp vid släpp:**
  - Inom 8 cm vågrätt från en rutas mitt och högst 10 cm ovanför den hamnar klossen i närmaste lediga stabila ruta. Vågrätt avstånd och uppåt räknas fullt, nedåt en fjärdedel, så en kloss över en kolumn hamnar överst i den kolumnen.
  - Spelaren väljer formen, högre eller bredare. Längre bort gäller §28.
  - Klossar i bygget är fasta och står kvar. Tas en kloss ur bygget faller det som stod på den.

## 34. Hoppgrodor (EPIC-018)

- **Vara och lekzon:** *hoppgrodor* (3 mynt) är en leksak i vänster zon. Setet säljs och bärs till lekbordet som de andra leksakerna. Där blir det en dammatta, ett stort näckrosblad (radie 8 cm, mitt i (−0,27; 0,02)) och två grodor:
  - barnets (orange) på barnets sida;
  - spelarens (grön) på spelarens sida.

  Båda står exakt ett hopp från bladet och är vända mot det. Grodorna har inga fysikkroppar, så fysikbudgeten är oförändrad (8).
- **Ett hopp per tryck** (`FrogPond`, ren logik):
  - Ett grepp nära bakdelen trycker ned den, och grodan fjädrar ihop. När handen släpper hoppar den en gång.
  - Hoppet är en båge 16 cm framåt i grodans riktning, 7 cm hög och 0,5 s lång.
  - Släpp utan tryck, tryck i luften och tryck på en groda i handen gör ingenting.
- **Sikta:** ett grepp mitt på grodan lyfter den. Den vrids med handen (bara runt uppåtaxeln) och sätts ned där den släpps.
- **Mål:**
  - En landning inom bladets radie + 5 cm räknas som på bladet och hamnar på bladet, med ett kvack och en ring på vattnet. Sitter den andra grodan där landar den bredvid (7 cm isär).
  - Bredvid bladet ligger grodan kvar.
  - Utanför bordet faller den och kommer hem igen efter 2 s (§28).
- **Turordning:**
  - Barnet visar ett hopp efter 2,5 s och hämtar sin groda hem 1 s efter landningen.
  - Sedan är det spelarens tur i upp till 8 s. Tiden står still medan spelaren håller eller trycker på en groda.
  - Spelaren får hoppa när som helst. Barnet hoppar 2,5 s efter spelarens senaste hopp.
  - Barnet rör bara sin egen groda och aldrig en groda i en hand.
- **Paus, meny, fokus och vilopaus:** ett pågående tryck avbryts utan hopp, och efteråt krävs ett nytt tryck. *Städa lekytan* ställer grodorna hemma.
