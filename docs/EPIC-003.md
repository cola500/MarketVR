---
title: EPIC-003 – Lilla marknaden
status: implemented (räckvidd med föräldern [H] och barnets test [P] återstår)
last_updated: 2026-11-01
---

# EPIC-003 – Lilla marknaden

**Ersätter** den tidigare EPIC-003 (”Leksaksaffären och lekhörnan”, där spelaren handlade leksaker för sina pengar). Den implementerades aldrig och finns kvar i git-historiken (`b2a36f0`).

**Produktmål:** spelaren driver en liten, mysig marknad och säljer frukt och grönsaker, godis och leksaker. Hon är säljaren vid alla stånd. Barn som köper leksaker går sedan till ett lekbord bakom henne och leker med dem. Spelaren vänder sig om, tar ett kort steg, leker med och vänder sig sedan tillbaka och säljer.

> **Ändrat 2026-10-31 – kompakt marknad utan teleportering:** halvcirkeln (r = 1,5 m) med fem teleportplatser var för stor och kontrollerna för krångliga för spelaren. Alla stånd och lekbordet står nu runt en liten arbetsyta (1,1 × 1,1 m) i en fysisk yta på 1,5 × 2,0 m (konservativt antagande, inte uppmätt). **S4.1–S4.3 och S4.5 (teleportering) är ersatta** av S4K nedan. Planvyn finns i [SPEC §14](SPEC.md#14-lilla-marknaden-epic-003--kompakt-marknad).

**Spelkänsla:** som tidigare. Lugn rollek utan timer, förlust eller missnöjda kunder. Önskemål visas med bilder och antal, och kort svensk text kompletterar. Samma handgrepp och samma köpflöde överallt. Intäkterna går till en gemensam kassa som syns vid alla stånd.

**Regler:** [SPEC.md §14–§20](SPEC.md#14-lilla-marknaden-epic-003). Verifieringssätt: **[T: Namn]** automatiskt test, **[B]** APK-bygge, **[R]** renderad bild, **[H]** manuellt på Quest 3 (utvecklaren), **[P]** barnets speltest (görs inte av utvecklaren). Antaganden är markerade **[A]**.

## Inventering före ändring (2026-10-31)

| Vad finns | Hur det används nu | Beslut |
|---|---|---|
| Fruktståndet: kund, beställning med bilder och antal, korg (överlämningsyta), myntbricka, kassa, tillstånd i `Shop` | verifierat på headset | **Blir den gemensamma försäljningsloopen** för alla kategorier. Korgen är överlämningsytan, myntbrickan är betalningsbrickan och kassan registrerar. |
| `Wallet`: 1 mynt per fruktköp | intäkt | Blir **kassan**. Krediteras med **ordersumman** i samma atomära övergång (`Shop.TryPay`). Inget spenderas längre. |
| Godisståndet: spelaren köper själv (förhandsvisning, betalyta, egen påse, prislappar), ledsna kunder som betalar direkt i handen | EPIC-002 S2–S6, S8 | **Ersätts.** Godisståndet blir ett säljstånd med samma loop som frukten. Spelarens önskemål om ledsna godiskunder behålls: godiskunder är ledsna tills de fått sitt godis. Betalyta, egen påse, prislappar och direktbetalning tas bort. |
| `Produce` (äpple, banan, morot), `Candy` (klubba, polkagris, godispåse) | två separata kataloger | **En gemensam varukatalog** `Ware` med kategori, pris, namn och bild. |
| Stånden placeras framför spelaren, snap turn, omcentrering och Flytta stånden hit | verifierat på headset | Behålls. Placeringen gäller nu hela marknaden runt den aktuella teleportplatsen. |
| Knappar: grepp/avtryckare (ta), spak upp/ned (höjd), spak vänster/höger (vridning), menyknappen (meny), A/B (bara i menyn) | inventerat | Oförändrade. *(Teleportering med bildknappar fanns i första versionen och är borttagen.)* |
| Fysik, lekyta, barn, leksaker | finns inte | Nytt: fysikmodulen (bara för leksaker och lekbord), `PlayArea` (ren logik) och vyer. |
| Sparning | finns inte | Ingen ny sparning. Kassa, order och lek gäller bara under spelpasset. |

## Storyordning och beroenden

```
S1 Gemensam marknad (frukt/grönt + godis, samma loop och kassa)
 └─ S2 Första leksaksförsäljningen (boll till ett barn)
     └─ S3 Från affär till lek (barnet tar bollen till lekbordet och rullar den)
         └─ S4 Spelaren deltar (vänd dig och ta ett steg, rulla tillbaka, vänd tillbaka och sälj)   ← första milstolpen
             ├─ S5 Utökat sortiment (bil och kloss-set med lek)
             └─ S6 Flera barn (reservation, kapacitet, lugnt avslut, städning)
                 └─ S7 Helhet och robusthet (menyer, fokus, räckvidd, prestanda, bygge, headset)
```

**Första milstolpen:** sälj frukt eller godis → sälj en boll till ett barn → se barnet gå och leka → vänd dig om till lekbordet och rulla tillbaka bollen → vänd dig tillbaka och sälj igen.

---

## S1 – Gemensam marknad

*Som spelare vill jag sälja frukt, grönsaker och godis på samma sätt och se att allt hamnar i samma kassa.*

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S1.1 | En gemensam varukatalog: Frukt & grönt (äpple, banan, morot, 1 per vara), Godis (klubba 1, polkagris 2, godispåse 3) och Leksaker (boll 2, bil 3, kloss-set 4). Varje vara har kategori, pris, namn och bild. | [T: Catalog_CategoriesAndPrices] |
| S1.2 | En aktiv beställning åt gången för hela marknaden, från en kategori. Den första är alltid ett äpple. Därefter turas kategorierna om (frukt/grönt, godis, leksaker), och leksaker bara när en lekplats är ledig. | [T: Orders_FirstIsOneApple] [T: Orders_OneCategoryEach] [T: Orders_CategoriesTakeTurns] |
| S1.3 | Kunden står vid ståndet för sin kategori. Bubblan visar bilder, antal och ordersumman (myntbild och siffra). | [T: Scene_CustomerAtStallOfCategory] [R] |
| S1.4 | Varor läggs i ståndets korg. Fel vara, fel kategori eller för många matchar inte och kan tas ur utan straff. Bara exakt innehåll matchar. | [T: Match_ExactCounts] [T: Match_TooFewTooManyWrongKind] [T: Basket_RejectsOtherCategory] [T: Scene_WrongItemCanBeRemovedByHand] |
| S1.5 | Vid exakt matchning lägger kunden fram en betalningsbricka. Den visar ordersumman (siffra och mynt). | [T: Sale_MatchMovesToAwaitingPaymentAndLocksBasket] [T: Scene_TokenShowsOrderSum] |
| S1.6 | Brickan i ståndets kassa registrerar köpet och ökar den gemensamma kassan med ordersumman, exakt en gång. Upprepade händelser räknas inte. | [T: Sale_PaymentCreditsOrderSumOnce] [T: Scene_RepeatedRegisterEventsCountOnce] |
| S1.7 | Kassan (myntbild och siffra) syns vid varje stånd och visar samma summa. | [T: Scene_KassaShownAtEveryStall] |
| S1.8 | Godisståndet använder samma loop. Godiskunder är ledsna (tår) medan de väntar och blir glada när köpet är klart (Spelarens önskemål). | [T: Scene_CandySaleSameLoop] [R] |
| S1.9 | Frukt-, grönsaks- och godiskunder går därifrån efter köpet. Butikslagret fylls på automatiskt. | [T: Sale_ThanksThenLeavesThenNextCustomer] [T: Scene_CrateRefillsAfterGrab] |
| S1.10 | En bildskylt på varje stånd visar kategorin: äpple och morot, klubba, boll och bil. | [R] |
| S1.11 | **Ersätter** EPIC-002 S2–S6 och S8.2 (spelaren köper godis, betalyta, påse, prislappar, direktbetalning). | dokument |

## S2 – Första leksaksförsäljningen

*Som spelare vill jag sälja en boll till ett barn.*

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S2.1 | En leksaksbeställning erbjuds bara när en lekplats för leksaken är ledig, och platsen reserveras när beställningen skapas. | [T: PlayArea_ReservesBeforeToyOrder] [T: Orders_NoToyWhenSpotTaken] |
| S2.2 | Leksakskunder är barn (mindre figur). Bubblan visar leksaken och summan. | [T: Scene_ToyOrderComesFromChild] [R] |
| S2.3 | Bollen från leksaksståndets låda i korgen, sedan brickan (2) i kassan: kassan ökar med 2. | [T: Scene_SellBallToChild] |

## S3 – Från affär till lek

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S3.1 | Efter betalningen tar barnet **samma** bollobjekt ur korgen, bär det och går till sin reserverade plats vid lekbordet. | [T: Scene_ChildCarriesSameBallToPlaySpot] |
| S3.2 | Där rullar barnet bollen över bordet mot spelarens sida. Ligger bollen still nära barnet rullar hon iväg den igen. Leksakens tillstånd går genom Butik → Överlämning → Barnets lek. | [T: Scene_ChildRollsBall] [T: PlayArea_ToyStates] |
| S3.3 | Under tiden kommer nästa kund till stånden. | [T: Scene_SellingContinuesWhileChildPlays] |

## S4 – Spelaren deltar (första milstolpen)

### Ersatt (teleportering, 2026-10-31)

S4.1 (teleportknappar), S4.2 (fem platser), S4.3 (teleportering och fokus) och S4.5 (tillbaka via teleportering) **gäller inte längre**. Deras tester (`Scene_TeleportButtonMovesPlayer`, `Scene_TeleportNeedsFocusAndFreshPress`, `Scene_OrderSurvivesTeleport`, `Layout_SpotsAreClear`, `Layout_EverySpotReachable`) är borttagna och ersatta enligt nedan.

### S4K – Kompakt marknad och fysisk rörelse

*Som spelare vill jag vända mig och ta ett kort steg för att komma till nästa stånd eller lekbordet, utan knappar.*

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S4K.1 | Frukt & grönt står framför, godis till höger, leksaker till vänster och lekbordet bakom. Alla vetter mot en fri arbetsyta på 1,1 × 1,1 m. Inga möbler inkräktar på arbetsytan, och inga möbler överlappar varandra. Måtten följer planvyn i SPEC §14. | [T: Layout_CompactUAroundWorkArea] [R] |
| S4K.2 | Allt spelaren behöver (lådor, korg, bricka, kassa, skylt, spelarens sida av lekbordet, städknappen) nås från en kroppsposition högst 0,50 m (x) och 0,75 m (z) från mitten, alltså med 0,25 m marginal till den fysiska ytan 1,5 × 2,0 m. Inget ligger mer än 0,20 m utanför den ytan. | [T: Layout_EverythingReachableFromInsideTheArea] [H tillsammans med föräldern] |
| S4K.3 | Diskarna är grunda (0,30 m) med varorna nära innerkanten. Figurer, varor och leksaker har sin vanliga storlek, och inget är nedskalat. | [T: Layout_ShallowCountersFullSizeObjects] |
| S4K.4 | Kunder står utanför diskarna. Barnen sitter på andra sidan lekbordet. Barnens gångväg från leksaksståndet till lekbordet korsar aldrig arbetsytan. | [T: Layout_PeopleAndPathsOutsideWorkArea] [T: Scene_ChildWalksAroundTheWorkArea] |
| S4K.5 | Alla stånd är direkt användbara där de står. Ingen station behöver väljas, och inga teleport- eller stationsknappar finns. | [T: Scene_NoTeleportOrStationButtons] [T: Scene_CandySaleSameLoop] |
| S4K.6 | Marknaden placeras bara uttryckligen: vid start, vid omcentrering och med Flytta marknaden hit. Då hamnar arbetsytans mitt där spelaren står och fruktståndet rakt fram. Fysisk rörelse, och att ta av och på headsetet, flyttar den aldrig. | [T: Placement_NeverFollowsPhysicalMovement] [T: Scene_MarketStaysPutWhilePlayerMoves] [H] |
| S4K.7 | Spelaren kan ta bollen, och barnet väntar då. Släppt med fart rullar den, och när den ligger still fortsätter barnet. Barnet och spelaren styr aldrig samtidigt. (Tidigare S4.4) | [T: PlayArea_PlayerAndChildNeverControlTogether] [T: Scene_PlayerRollsBallBackChildContinues] |
| S4K.8 | Beställningen, korgen och kassan är oförändrade när spelaren rör sig mellan stånden och lekbordet. | [T: Scene_OrderSurvivesWalkingAround] |
| S4K.9 | Hela milstolpen i den riktiga scenen, med simulerade händer på de faktiska platserna. | [T: Scene_FirstMilestone] |
| S4K.10 | Räckvidd och komfort verifieras på headset tillsammans med föräldern innan uppställningen räknas som klar. Automatiska måttkontroller visar inte att den är bekväm för spelaren. | [H] [P] |

## S5 – Utökat sortiment

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S5.1 | Bilen: egna beställningar, och barnet kör den längs vägen på bordet. Spelaren kan ta och skjuta den. Den rullar längs sin längdriktning, glider inte i sidled och välter inte. Barnet tar sedan över igen från närmaste punkt på vägen. | [T: Scene_ChildDrivesCarAlongRoad] [T: Scene_PlayerPushesCarChildResumes] |
| S5.2 | Kloss-setet: en vara (4) som blir sex klossar på lekbordet. Barnet bygger ett torn, en kloss i taget. Spelaren kan bygga bredvid eller lägga till. Placeringshjälp ställer en kloss rakt ovanpå en annan när den släpps nära. | [T: Scene_SixBlocksFromOneSet] [T: Scene_ChildBuildsTower] [T: Stack_SnapsOnTopWhenClose] [T: Stack_NoSnapWhenFar] |

## S6 – Flera barn

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S6.1 | Högst tre lekande barn (ett per lekplats: boll, bil, klossar) och en kund vid disken. Är leksakens plats upptagen erbjuds en annan leksak. Är alla upptagna fortsätter frukt- och godisförsäljningen. | [T: PlayArea_MaxThreeChildren] [T: Orders_NoToyWhenAreaFull] [T: Scene_FullPlayAreaOtherSalesContinue] |
| S6.2 | Ett barn leker en lugn stund (45 s) och går sedan med sin leksak, vilket frigör platsen. Leken avslutas inte medan spelaren håller leksaken eller nyss lekte med den (inom 8 s). | [T: PlayArea_CalmFinishNotWhilePlayerPlays] |
| S6.3 | Ingenting ryckts ur spelarens hand, varken vid städning eller när ett barn går. | [T: Scene_ChildLeavingNeverSnatchesToy] [T: Scene_TidyNeverSnatchesHeldToy] |
| S6.4 | En leksak som legat på golvet i 2 s går tillbaka till sin plats, gratis. Städa lekytan (bildknapp vid bordet och i menyn) lägger alla leksaker som inte hålls tillbaka på sina platser. | [T: Scene_LostToyReturns] [T: Scene_TidyPlayArea] |
| S6.5 | Barnens leksaker tas bort när barnet går. Högst 1 boll, 1 bil och 6 klossar med fysik. | [T: Scene_ObjectCountsBounded] |

## S7 – Helhet och robusthet

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S7.1 | Menyn har Fortsätt, Visa kontroller, Stäng/Öppna marknaden, Återställ beställningen, Städa lekytan, Flytta marknaden hit och Nytt spel. Återställ och Städa rör inte kassan. Nytt spel kräver Ja och återställer hela passet. | [T: Menu_MarketItems] [T: Menu_NewGameNeedsConfirmationNoIsDefault] [T: Scene_ResetsKeepKassa] [T: Market_NewGameResetsEverything] |
| S7.2 | Utan fokus: inga köp eller grepp. En leksak i handen läggs stilla på sin plats och barnet fortsätter. Paus bevarar order, kassa och lek. | [T: Scene_PauseWithToyInHand] [T: Router_*] |
| S7.3 | Allt som fungerade innan (EPIC-001: frukt, grepp, meny, höjd, vridning, omcentrering) fungerar. Desktopläget fungerar. | [T: alla tidigare relevanta tester] |
| S7.4 | Fysik bara för leksaker, bord och mark, stegad från spelets `Step`. Högst 8 fysikkroppar och 4 figurer (1 kund + 3 barn). | [T: Scene_ObjectCountsBounded] [B] |
| S7.5 | APK byggs. Den installeras inte medan någon spelar. | [B] |
| S7.6 | Manuellt acceptanstest (barnet): sälja alla kategorier, se att allt går till samma kassa, se ett barn ta sin leksak till lekytan och leka, besöka och delta, gå tillbaka och sälja vidare. | [P] |

## Manuellt headsettest (utvecklaren, tillsammans med föräldern för räckvidd)

0. **Före:** kontrollera att Quests systemgräns (Guardian) är ritad runt rummets verkliga lediga yta. Mät ytan och för in måtten i SPEC §14 om de skiljer sig från 1,5 × 2,0 m.
1. Ställ dig mitt i ytan och håll in Meta-knappen. Fruktståndet står framför dig, godis till höger, leksaker till vänster och lekbordet bakom.
2. Sälj äpplet: kassan ökar med 1, och det syns vid alla stånd.
3. Vänd dig höger mot godiset, ta ett litet steg och sälj till den ledsna kunden. Vänd dig vänster till leksakerna och sälj bollen till barnet. Barnet går runt utsidan till lekbordet.
4. Vänd dig om till lekbordet. Barnet rullar bollen till dig. Ta den (barnet väntar) och rulla tillbaka (barnet fortsätter).
5. Vänd dig tillbaka. Beställningen väntar kvar, och du säljer vidare.
6. **Räckvidd (med föräldern):** nå varje låda, korgen, brickan, kassan, skylten och lekbordets närmre halva utan att gå utanför den markerade ytan och utan att sträcka dig obekvämt. Notera vad som är för långt bort eller för nära.
7. Gå runt i ytan och ta av och på headsetet: marknaden står kvar. Flytta marknaden hit i menyn: arbetsytan hamnar där du står.
8. Systemmenyn med en leksak i handen, Städa lekytan, Återställ beställningen och Nytt spel.

## Verifieringslogg

| Datum | Vad | Resultat |
|---|---|---|
| 2026-10-31 | Den tidigare EPIC-003 (spelaren köper leksaker) är ersatt av denna innan den implementerades | – |
| 2026-10-31 | Epicen skriven före implementation | – |
| 2026-10-31 | Implementation S1–S7 | Core: `Ware`, generell `Order`/`OrderGenerator`/`Basket`/`Shop` (ordersumma till kassan), `Wallet` (kassa, debiteras aldrig), `PlayArea`, `StackHelp`. EPIC-002:s `CandyShop`, `CandyCustomers` och godisvyerna borttagna. Spel: tre stånd med samma vy (`StandView` per kategori), `MarketLayout`, `PlayTableView` (lekbord, barn, leksaksfysik), teleportering via bildknappar (`XrRig.Teleport`), ny meny, desktoptangenter. Fysikmodulen tillagd, stegad från `Step`. |
| 2026-10-31 | Automatiska tester | **EditMode 63/63, PlayMode 40/40** (Snapshots överhoppad, den är explicit). Nya: 20 regeltester i `MarketTests` och 27 scentester i `MarketSceneTests`, bland annat fel vara/kategori, ordersumma, dubbel betalning, gemensam kassa, full lekyta, platsreservation, kontroll som växlar mellan barnet och spelaren, städning som inte rycker leksaker ur handen, paus med leksak och högst 8 fysikkroppar. EPIC-001:s scentester är oförändrade och gröna. EPIC-002:s köptester är borttagna (kraven ersatta). |
| 2026-10-31 | Fel hittade av testerna | (1) Bilen bromsades nästan direkt, eftersom dess låga friktion medelvärdesbildades med bordets. Nu gäller den lägsta friktionen och bilen rullar. (2) Släppfarten togs från en utjämnad hastighet, som halverades den bildruta handen stannade. Nu används den högsta farten under de senaste 0,1 s (vanlig VR-praxis för kast). (3) Ett testfel med en order på fyra varor. Den samlade PlayMode-körningen hängde först efter misslyckade tester. När felen var rättade gick hela sviten igenom utan häng. |
| 2026-10-31 | Renderade kontrollbilder | Fruktståndet med ”Hej!” och kassan, en ledsen godiskund med bubbla och summa (”= 4”), leksaksståndet med ett barn, bricka och ”Tack!”, och lekbordet med barnet, bollen och vägen. Efter första bilderna: platsknapparna låg över äpplelådan och flyttades ned framför bröstet, bordets rundade gavlar stack ut förbi kanten (nu rundade hörn och kant runt om) och barnen var för låga (nu sitter de på pallar i bordshöjd). |
| 2026-10-31 | APK-bygge | Lyckades, 23 MB, `com.lillafruktstandet.prototype`. |
| 2026-10-31 21:50 | Installation och start på Quest 3 (teleportversionen) | `adb install -r` lyckades. Första startförsöket blockerades av systemdialoger (guardian och avstängningsbekräftelse). Efter att de stängts startade appen: stånden placerades, snap turn användes och loggen visar inga fel. 72/72 FPS (GPU ca 28 %) i startläget. Själva marknaden (teleportering, godis- och leksaksförsäljning, lekytan) är **inte provad** på headset. |
| 2026-10-31 | Kompakt marknad utan teleportering (S4K) | `MarketLayout` omskriven: frukt (0 ; 0,70), godis (0,70 ; 0), leksaker (−0,70 ; 0) och lekbord (0 ; −0,80), alla vända mot en arbetsyta på 1,1 × 1,1 m. Grunda diskar (0,30 m), lekbord 1,0 × 0,5 m. Barnen går runt utsidan. Teleportknappar, platsringar, `Teleport` och regeln att stånden flyttas när headsetet tas på långt bort är borttagna. Placering bara vid start, omcentrering och via menyn. Desktop har kameravyer (Tab). **EditMode 65/65, PlayMode 41/41.** Nya tester: `Layout_CompactUAroundWorkArea`, `Layout_EverythingReachableFromInsideTheArea`, `Layout_ShallowCountersFullSizeObjects`, `Layout_PeopleAndPathsOutsideWorkArea`, `Placement_NeverFollowsPhysicalMovement`, `Scene_NoTeleportOrStationButtons`, `Scene_MarketStaysPutWhilePlayerMoves`, `Scene_ChildWalksAroundTheWorkArea`, `Scene_OrderSurvivesWalkingAround`, och `Scene_FirstMilestone` med händerna på de verkliga platserna. Teleporttesterna är borttagna. Ett layouttest hittade att korgens bortre platser på sidostånden låg 2 mm för långt ut, så korgen flyttades 1 cm närmare innerkanten. |
| 2026-10-31 | Renderade kontrollbilder (kompakt) | Översikt ovanifrån: U-formen, fri arbetsyta i mitten, lekbordet bakom och barnet på pall på bortre sidan. Vänd mot godiset: ledsen kund, bubbla med ”= 4” och kassan. Vänd om mot lekbordet: barnet, bollen och städknappen. |
| – | Räckvidd och komfort på headset med föräldern (S4K.10) | **Inte gjort.** Uppställningen räknas inte som klar förrän det är gjort. Rummets yta är inte uppmätt; 1,5 × 2,0 m är ett antagande. |
| – | Barnets acceptanstest (S7.6) | **Inte gjort.** |
