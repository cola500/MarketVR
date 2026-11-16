---
title: EPIC-002 – Godisståndet
status: delvis ersatt av EPIC-003 (spelaren köper inte längre godis; godisståndet är ett säljstånd)
last_updated: 2026-10-31
---

# EPIC-002 – Godisståndet

> **Delvis ersatt av [EPIC-003](EPIC-003.md) (2026-10-31):** S2–S6 (spelaren köper godis) och S8.2 (direktbetalning) gäller inte längre. Godisståndet är ett säljstånd med samma loop som frukten, och kassan får ordersumman. S1 (intäkt från registrerade köp, exakt en gång), skylten Öppet/Stängt, Nytt spel, omcentrering och snap turn gäller fortfarande. Spelarens ledsna godiskunder lever kvar i EPIC-003 S1.8.

**Bakgrund (rapporterat av föräldern 2026-10-31):** barnet spelade fruktståndet på egen hand i ungefär 15 minuter och vill nu kunna köpa godis för pengarna hon tjänar. Inga andra speltestresultat är dokumenterade som bekräftade. [PLAYTEST.md](PLAYTEST.md) är fortfarande inte ifyllt.

**Mål:** spelaren säljer frukt, får spelpengar i sin plånbok och handlar virtuellt godis i ett godisstånd bredvid. Sedan går hon tillbaka till fruktförsäljningen.

**Regler:** se [SPEC.md §11–§13](SPEC.md#11-plånbok-och-ekonomi). Verifieringssätten är samma som i [EPIC-001](EPIC-001.md): **[T: Namn]** automatiskt test, **[B]** bygge, **[H]** manuellt på Quest 3 (utvecklaren), **[R]** renderad bild. **[P]** betyder barnets speltest, som inte görs av utvecklaren.

## Inventering av ekonomin (före implementation)

- Intäkter fanns inte som pengar. Det enda som räknade dem var `Shop.CompletedSales`, som ökar på ett enda ställe: övergången `AwaitingPayment → Thanking` i `Shop.TryPay`. Den är redan skyddad mot dubbelräkning (EPIC-001 S4.2).
- Ingen sparning finns.
- **Beslut:** plånboken (`Wallet`) krediteras med 1 spelpeng i samma atomära övergång i `Shop.TryPay`, och bara där. Någon separat intäktsräknare finns inte. `CompletedSales` är statistik (antal köp). Saldot är pengarna. Invariant: `Balance == CompletedSales × 1 − Spent`.

## Storyordning och beroenden

```
S1 Plånbok: intäkt från riktiga fruktköp
 └─ S2 Godisståndet: plats, utseende, räckvidd
     └─ S3 Köp en klubba (första kompletta slicen)
         ├─ S4 Tre sorter, pris, ”har jag råd?”, avbryt, för lite pengar
         ├─ S5 Godispåsen
         └─ S6 Samspel: stäng/öppna, töm korgen, Nytt spel, fokus och meny
             └─ S7 Bygge och headsettest
```

**Tillägg (S8):** sälja godis till ledsna kunder, se S8.

**Första kompletta slicen (S1–S3):** sälj frukt → 1 spelpeng → ta en klubba → lägg den på betalytan → pengen dras exakt en gång och klubban hamnar i påsen → sälj igen och handla igen.

---

## S1 – Plånbok med intäkt från riktiga fruktköp

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S1.1 | Varje registrerat fruktköp ger exakt 1 spelpeng. Upprepade betalhändelser ger inget mer. | [T: Wallet_EachSaleEarnsExactlyOnce] [T: Scene_SaleEarnsOneCoinOnce] |
| S1.2 | Saldot är ett heltal som aldrig blir negativt. Ogiltiga belopp avvisas. | [T: Wallet_NeverNegative] |
| S1.3 | Saldot visas med myntbild och siffra vid fruktståndet och vid godisståndet. Det uppdateras direkt vid intäkt och köp. | [T: Scene_WalletShownAtBothStands] [R] [H] |
| S1.4 | Fruktförsäljningen fungerar som tidigare: alla EPIC-001-tester är gröna. | [T: alla EPIC-001-tester] |

## S2 – Godisståndet: plats, utseende och räckvidd

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S2.1 | Godisståndet står snett bakom fruktdiskens högra ände och vänder sig mot spelaren. En hög skylt med en klubba syns redan när man tittar mot kassan. *Ändrat 2026-10-31: större disk vid ca 125° (SPEC §12), så skylten syns sämre från kassan än förut.* | [R] [H] |
| S2.2 | Hylla, betalyta, påse och kundens hand ligger högst 0,53 m från ögonen och inom ±35° från diskens riktning, och nås sittande när man vänt sig mot disken (tre vridningar med spaken). Ingen förflyttning behövs. *Ändrat 2026-10-31 från ”0,50 m, kvarts vridning”.* | [T: Layout_CandyWithinSeatedReach] [H] |
| S2.3 | Godisståndet krockar inte med fruktståndet. | [T: Layout_CandyStandClearOfFruitCounter] |
| S2.4 | Disken följer den gemensamma höjdjusteringen. | [T: Scene_CandyCounterFollowsHeight] |
| S2.5 | Ståndet har ett eget utseende: rosa och mintgrönt, randigt tak i rosa och vitt, trappstegshylla. | [R] |

## S3 – Köp en klubba (första kompletta slicen)

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S3.1 | Att ta en klubba från hyllan är bara förhandsvisning och kostar inget. Skylten visar klubban, priset (1 mynt) och om pengarna räcker. | [T: Candy_PickUpIsFree] [T: Scene_PreviewShowsPriceAndAffordability] |
| S3.2 | Klubban släppt på betalytan bekräftar köpet. Saldot minskar exakt en gång med priset och klubban hamnar i påsen. | [T: Candy_BuyWithExactBalance] [T: Scene_FirstLollipopSlice] |
| S3.3 | Efter köpet kan spelaren sälja frukt och handla igen. | [T: Scene_FirstLollipopSlice] [H] |

## S4 – Tre sorter, pris, ”har jag råd?”, avbryt och för lite pengar

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S4.1 | Klubba 1, polkagris 2 och godispåse 3 spelpengar. De skiljs åt i form och färg. | [T: Candy_Prices] [R] |
| S4.2 | Varje sort har en prislapp med lika många myntbilder som priset. Lappen är grön när pengarna räcker och grå annars. | [T: Scene_PriceTagsShowAffordability] [R] |
| S4.3 | Släpps godiset någon annanstans än på betalytan avbryts köpet. Godiset går tillbaka till hyllan utan kostnad. | [T: Candy_CancelIsFree] [T: Scene_CancelReturnsCandyFree] |
| S4.4 | Med för lite pengar: vänlig återkoppling (”Du behöver 1 mynt till”, saknade mynt visas bleka). Inget dras, inget levereras och godiset går tillbaka till hyllan. | [T: Candy_NotEnoughMoney] [T: Scene_NotEnoughMoneyIsFriendlyAndFree] |
| S4.5 | Upprepade köphändelser (dubbel utlösning, samma släpp två gånger) ger bara ett köp. | [T: Candy_RepeatedBuyEventsBuyOnce] [T: Scene_RepeatedMatEventsBuyOnce] |
| S4.6 | Flera köp i följd ger rätt återstående saldo. | [T: Candy_SeveralPurchasesLeaveCorrectBalance] |
| S4.7 | Bara ett godis i taget kan hållas från hyllan, så att det som visas på skylten alltid är det som köps. | [T: Candy_OnlyTheSelectedCandyCanBeBought] [T: Scene_OneShelfCandyAtATime] |

## S5 – Godispåsen

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S5.1 | Köpt godis syns i spelarens egen påse, skild från ståndets varor. Under påsen visas antalet per sort. | [T: Scene_BagShowsBoughtCandy] [R] |
| S5.2 | Högst 3 synliga godisbitar per sort. Fler visas med en siffra. Scenen fylls inte med objekt. | [T: Scene_BagCapsVisibleObjects] |
| S5.3 | Godis i påsen kan plockas upp och läggs tillbaka i påsen när man släpper det. Det kan aldrig köpas igen, inte heller via betalytan. | [T: Scene_BagCandyCanBeHeldButNeverRebought] |

## S6 – Samspel mellan stånden

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S6.1 | En skylt **Öppet/Stängt** på fruktståndet, som slår om när man drar en tom hand genom den (ändrat efter headsettest, tidigare knapptryck), och samma val i menyn, pausar nya fruktkunder. Den slår om en gång per svep, inte för en hand som bär något och inte medan en meny är öppen. En pågående beställning finns kvar och kan slutföras. Ingen timer finns och ingen blir arg. | [T: Shop_ClosedKeepsCurrentOrderAndStopsNewCustomers] [T: Scene_ClosedSignPausesNewCustomers] [T: Touch_FlipsOncePerPassFromOutside] [T: Touch_IgnoresFullHandsDisabledAndQuickDoubleSwipes] [T: Scene_SignIgnoresFullHandsAndMenus] |
| S6.2 | Saldo och påse finns kvar mellan stånden och genom paus, systemmeny och spelmeny. | [T: Scene_WalletAndBagSurviveFocusLossAndMenu] |
| S6.3 | **Töm korgen** (menyn) återställer bara den aktuella beställningen: korgen töms, samma kund och beställning finns kvar, pengar och godis rörs inte. | [T: Shop_ClearBasketKeepsOrderAndMoney] [T: Scene_ClearBasketKeepsMoneyAndCandy] |
| S6.4 | **Nytt spel** tömmer pengar, godis och försäljning först efter ett tydligt Ja. Nej är förvalt. | [T: Menu_NewGameNeedsConfirmationNoIsDefault] [T: Market_NewGameResetsEverything] [T: Scene_NewGameEmptiesWalletAndBag] |
| S6.5 | Spelmenyn, förlorat inputfokus och återupptagning kan inte genomföra ett köp. Det man håller går hem och köpet avbryts. | [T: Scene_FocusLossWhileHoldingCandyBuysNothing] [T: Router_*] |
| S6.6 | Ingen sparning mellan appstarter: saldo och godis börjar på 0 vid varje start. Det står i SPEC och README. | dokument |

## S8 – Sälja godis till ledsna kunder (Spelarens önskemål)

**Bakgrund:** ”hon vill kunna sälja godis till kunder som är ledsna” (Spelaren via föräldern, 2026-10-31; ett önskemål, inget speltestresultat). Utvecklaren har beslutat att kunderna kommer till godisståndet, att godiset hämtas från hyllan och att kunden betalar godisets pris. Att betalningen sker direkt när kunden får godiset är ett antagande **[A]** (SPEC §12).

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S8.1 | Ibland kommer en ledsen kund till godisståndet: hängande mun, en tår, lugn rörelse. Kunden står till höger bakom disken och sträcker fram en öppen hand över disken. En bubbla visar ett ledset ansikte och bilden av godiset kunden vill ha (ett godis). Kunden blir aldrig mer ledsen, ingen timer finns och kunden går aldrig iväg utan godis. | [T: SadCustomer_WaitsWithoutTimer] [R] [H] |
| S8.2 | Rätt godis från hyllan släppt i kundens hand säljs: kunden blir glad (stort leende, hjärtan, hopp) och betalar godisets pris (klubba 1, polkagris 2, godispåse 3) till plånboken, exakt en gång. Godiset tas inte från spelarens påse och påverkar inte hennes köp. | [T: SadCustomer_ServeRightCandyPaysPriceOnce] [T: Scene_SellCandyToSadCustomer] |
| S8.3 | Fel godis i handen tas inte emot. Det går tillbaka till hyllan utan straff och kunden väntar kvar. | [T: SadCustomer_WrongCandyIsNotTaken] [T: Scene_WrongCandyGoesBackToShelf] |
| S8.4 | Godis ur spelarens egen påse kan inte säljas, och betalytan fungerar som förut för hennes egna köp även när en ledsen kund väntar. | [T: Scene_BagCandyCannotBeSold] [T: Scene_PayMatStillBuysForMeWhileCustomerWaits] |
| S8.5 | När en glad kund gått kommer nästa ledsna kund efter en kort paus (4 s). Skylten Öppet/Stängt pausar nya kunder vid båda stånden. Den som redan väntar stannar. | [T: SadCustomer_ClosedStopsNewButKeepsWaiting] |
| S8.6 | Systemmeny, spelmeny eller förlorat fokus med godis i handen över kundens hand säljer ingenting. | [T: Scene_FocusLossOverCustomerHandSellsNothing] |
| S8.7 | Nytt spel börjar om även godiskunderna. Plånbokens intäkt är fruktköp plus godisförsäljning, och inget annat. | [T: Market_NewGameResetsCandyCustomers] [T: Wallet_IncomeIsFruitSalesPlusCandySales] |

## S7 – Bygge och headsettest

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S7.1 | Alla automatiska tester är gröna och desktopläget fungerar (tangenterna 4/5/6, Enter, O, C). | [T] |
| S7.2 | APK:n byggs med befintligt arbetsflöde. Den installeras **inte** medan barnet spelar, utan ligger klar tills föräldern säger att passet är slut. | [B] |
| S7.3 | Manuellt acceptanstest: barnet säljer frukt, hittar godisståndet, förstår vad hon har råd med, köper godis och går tillbaka till fruktförsäljningen utan hjälp med kontrollerna. | [P] [H] |

## Manuellt headsettest (utvecklaren)

1. Sälj till en kund. Plånboken vid kassan går från 0 till 1 med en kort puls.
2. Titta åt höger: godisståndet och klubbskylten syns. Vrid dig en kvarts varv och nå hyllan, betalytan och påsen sittande.
3. Ta klubban: skylten visar klubban, 1 mynt och ”Det räcker!”. Släpp den i luften: den går tillbaka och inget dras.
4. Lägg klubban på betalytan: pling, saldot blir 0 och klubban hamnar i påsen.
5. Ta polkagrisen med saldo 0: ”Du behöver 2 mynt till”. Lägg den på betalytan: inget dras och den går tillbaka.
6. Dra handen genom skylten så att den visar Stängt mitt i en beställning. Kunden står kvar och beställningen går att slutföra, men ingen ny kund kommer. Öppna igen: nästa kund kommer.
7. Håll ett godis över betalytan och tryck på Meta-knappen. Inget köps och godiset går hem. Stäng menyn med knappen nedtryckt: inget händer.
8. Menyn: Töm korgen (pengar och godis finns kvar), sedan Nytt spel → Nej, sedan Nytt spel → Ja (allt på 0).

## Verifieringslogg

| Datum | Vad | Resultat |
|---|---|---|
| 2026-10-31 | Epic skriven före implementation | – |
| 2026-10-31 | Automatiska tester (`scripts/quest.sh test`) | **EditMode 49/49, PlayMode 31/31 gröna** (1 explicit `Snapshots` överhoppad). Det är 16 nya regeltester, 18 nya scentester och de 46 från EPIC-001, varav 2 anpassade: `Börja om` heter nu `Nytt spel`, och kontrollen av vad som finns kvar efter ett nytt spel räknar med skylten och hyllan. Täcker alla [T]-kriterier ovan, bland annat intäkt exakt en gång, köp med exakt saldo, för lite pengar, avbrutet köp, upprepade köphändelser, flera köp med rätt saldo, paus/meny/fokus och Nytt spel. |
| 2026-10-31 | Fel hittade under arbetet | (1) `Buy` kunde anropas för ett godis som stod på hyllan och då flytta det. Nu verkar köpet bara på ett godis i handen eller i förhandsvisningen. (2) Efter ett misslyckat test hängde PlayMode-sviten i ett väntetest. Väntan har nu en tidsgräns, så ett fel blir ett felmeddelande i stället för ett häng. |
| 2026-10-31 | Placering (beräknad och låst i test) | Fruktdisken täcker redan synfältet ungefär −60° till +80°. Godisståndet står därför 100° åt höger. Betalytan ligger 0,39 m från ögonen (87°), påsen 0,38 m (114°) och hyllan 0,49–0,52 m (98–109°). Ingen krock med fruktdisken. Fruktståndet är oförändrat. |
| 2026-10-31 | Renderade kontrollbilder (`Snapshots`) | Sittande blick mot kassan: godisståndet, klubbskylten och plånboken (3) syns. Vänd mot godiset: förhandsvisad polkagris med 2 guldmynt och ”Det räcker!”, köpt polkagris i påsen, godispåse med 1 mynt: ”Du behöver 2 mynt till.” Efter första bilderna: myntbilderna gjordes oupplysta (de var bruna i motljus), texterna förstorades och plånboksmärket flyttades fram (det flimrade ihop med skylten). S1.3, S2.1, S2.5, S4.1, S4.2, S5.1 kontrollerade utanför headset. |
| 2026-10-31 | APK-bygge | **Lyckades, 23 MB**, `com.lillafruktstandet.prototype`, arm64. **Inte installerad:** enligt instruktionen väntar den tills föräldern säger att spelpasset är slut. |
| 2026-10-31 | Installation på Quest 3 (efter att föräldern sagt att passet var slut) | `adb install -r` lyckades och appen startades (ny process). Loggen visar `[Fruktstand] stand placed` med disken på 0,50 m, den lägsta tillåtna höjden (headsetet satt troligen inte på huvudet vid start), 72/72 FPS (GPU ca 41 %) och 0 Unity-fel eller exceptions. Den gamla versionen stängdes; den hade inget sparat att förlora. Detta verifierar bara start. Inga [H]-kriterier är provade än. |
| 2026-10-31 | Headset (utvecklaren): spelaren startade långt från stånden | Tidslinjen i loggen: appen startade 18:04:29 och stånden placerades direkt utifrån headsetets läge; headsetet togs på 18:05:15 (`PROX_ON`); en systemomcentrering 18:06:16 ignorerades av appen. **Åtgärd** (`StandPlacement`, testad i `Placement_WaitsUntilWornThenOnlyOnRequestOrFarAway` och `Menu_PlaceStandsInFront`): placering först när headsetet sitter på och golvorigo är aktivt, ny placering vid omcentrering, via menyvalet Flytta stånden hit och när headsetet tas på långt bort. Ingen teleportering (utanför scope). Installerad och startad: loggen visar placering med `worn=True, origin=Floor` och att omcentreringen fångas. Att stånden hamnar rätt framför spelaren är **inte bekräftat** av utvecklaren. |
| 2026-10-31 | Diskhöjd 0,50 m vid varje start | Diagnostiken visar `origin=Floor` och ögonhöjd **0,63 m** över golvet. Höjden kläms då till minimum 0,50 m. Antingen låg headsetet inte på huvudet trots att närvarosensorn rapporterade det, eller så är Quests golvnivå felkalibrerad (en sittande vuxen har normalt ögonen på ca 1,1–1,2 m). **Ej utrett.** |
| 2026-10-31 | Headset (utvecklaren) | **Bekräftat av utvecklaren:** stånden hamnar nu rätt framför spelaren. Önskemål: skylten Öppet/Stängt ska slå om när man drar handen genom den, utan knapp. Åtgärd: `TouchSwitch` (en gång per svep, handen måste komma utifrån, bara tom hand, inte under meny, 0,5 s spärr mellan händerna). Tester: EditMode 53/53, PlayMode 32/32. |
| 2026-10-31 | Ändrad avgränsning på utvecklarens begäran | **Snap turn** med tumspaken vänster/höger (45° per utslag, runt huvudet) för att kunna vända sig om sittande. Epicen uteslöt nya förflyttningskontroller; det här är vridning på stället, inte förflyttning. Samma skydd som övrig input: ett steg per utslag, diagonaler ignoreras, inget under meny eller systemmeny eller innan spaken släppts efter återkomst. Tester: `Router_SnapTurnOneStepPerFlick`, `Router_SnapTurnBlockedByMenuAndFocus`. EditMode 55/55, PlayMode 32/32. |
| 2026-10-31 | Headset (utvecklaren) | **Bekräftat av utvecklaren:** snap turn fungerar. Loggen visade vridningar åt båda hållen direkt efter start. |
| 2026-10-31 | Diskhöjd vid start | Orsaken hittades i loggen: närvarosensorn rapporterade `worn=True` ungefär 3 s före `PROX_ON`, så stånden placerades medan headsetet hölls i handen (ögon 0,63–0,88 m → disken på minimum 0,50 m). Åtgärd: första placeringen väntar tills headsetet suttit på i 1 s (`StandPlacement.SettleSeconds`, testat i `Placement_FirstWaitsForTheHeadsetToSettleRecenterDoesNot`). En omcentrering placerar om direkt och räknar om höjden från ögonhöjden. Installerad: vid start placerades stånden efter 1,0 s med ögonen på 0,98 m och disken på **0,58 m**. Att en manuell omcentrering räknar om höjden på headsetet är **ännu inte provat**. EditMode 56/56, PlayMode 32/32. |
| 2026-10-31 | Omcentrering räknar om diskhöjden | Första försöket: tre manuella omcentreringar (18:27) gav **ingen** reaktion; Unitys `trackingOriginUpdated` kommer bara vid appens egen omcentrering vid start, inte när man håller Meta-knappen. Åtgärd: omcentrering upptäcks också från huvudets pose, som hoppar mer än en riktig rörelse hinner på en bildruta (> 0,25 m i sidled eller > 40° vridning; `StandPlacement.LooksLikeRecenter`, testat i `Placement_RecenterDetectedFromHeadPoseJump`). **Verifierat på headset 18:31:19:** omcentreringen upptäcktes (hopp 0,62 m), stånden placerades framför spelaren och disken räknades om från 0,50 m till **0,65 m** (ögon 1,05 m). Två omcentreringar strax efter gav så små hopp att de inte räknades, eftersom stånden redan stod rätt (avsiktligt). Vid starten hamnade disken ändå på 0,50 m (ögon 0,44 m trots att sensorn rapporterat headsetet påtaget i 1 s), så omcentrering är sättet att rätta starthöjden. EditMode 57/57, PlayMode 32/32. |
| 2026-10-31 | Headset (utvecklaren) | **Bekräftat av utvecklaren:** godisköpet fungerar (S3). Vilka steg i protokollet som provades är inte återrapporterade punkt för punkt. Köp loggas inte, så loggen kan inte styrka detaljerna. Inte uttryckligen bekräftat: för lite pengar (S4.4), skylten Öppet/Stängt med en kund kvar (S6.1), systemmenyn med godis i handen (S6.5), Töm korgen och Nytt spel (S6.3–S6.4). |
| 2026-10-31 | S8 (Spelarens önskemål) och större godisdisk | Ledsna kunder vid godisståndet. Beslut från utvecklaren: kunderna kommer till godisståndet, godiset hämtas från hyllan och kunden betalar godisets pris. Antagande: betalningen sker direkt. Efter återkoppling på headset blev disken större (56 × 34 cm vid ca 125°), godiset fick 18 cm mellanrum och kunden står så att skylten inte skymmer. Kunden blir glad (leende, hjärtan, vinkning) när hon fått godiset. **EditMode 63/63, PlayMode 37/37** (7 nya regeltester och 5 nya scentester). I kontrollbilderna syns den ledsna kunden med tår och en bubbla med det önskade godiset, och efter försäljningen glad kund med hjärtan och mynt på väg till plånboken. Efter första bilderna flyttades kundens hand fram, eftersom den skymde mittenhyllan. |
| 2026-10-31 18:53 | Installation av S8 och den större godisdisken | `adb install -r` lyckades och appen startades. Inga fel i loggen. Disken på 0,50 m (ögonen på 0,41 m vid start, så headsetet satt inte på; rättas med omcentrering). |
| 2026-10-31 | Headsetåterkoppling på S8 | Bubblan över kundens huvud skymdes delvis, och kundens arm gick genom godiset. Åtgärd: kundens önskemål (ledset ansikte och godiset, sedan glatt ansikte och hjärta) visas på den stora skylten, och bubblan är borttagen. Spelarens egen priskontroll visas i stället på prislapparna: lappen växer när man håller godiset, grön eller grå, och skakar när pengarna inte räcker. Texterna ”Det räcker!” och ”Du behöver N mynt till” är borttagna, vilket ändrar S3.1 och S4.4. Kunden står vid diskens högra kortände på en pall och sträcker sig in mellan hyllan och påsen (`Layout_CustomerArmClearsCandyAndBag`). EditMode 64/64, PlayMode 38/38. |
| 2026-10-31 19:02 | Installerad för barnets test före läggdags, på utvecklarens begäran | `adb install -r` lyckades och appen startades, inga fel i loggen. **Känt och inte åtgärdat i denna version:** på kontrollbilderna står kunden fortfarande nära spelaren och skymmer högra kanten av synfältet, och pallen syns stor. Ska åtgärdas senare. |
| 2026-10-31 | Kunden stod för nära (utvecklarens återkoppling) | Ny uppställning: hyllan på vänster halva, påsen mitt fram och kunden bakom högra halvan (ca 0,9 m från ögonen, ansiktet i ögonhöjd) med handen rakt fram till höger. Testat i `Layout_CandyWithinSeatedReach` (kunden minst 0,8 m bort, allt nåbart inom 0,53 m) och `Layout_CustomerArmClearsCandyAndBag`. EditMode 64/64, PlayMode 38/38. Kontrollbilder: kunden syns bakom disken och skymmer varken godiset eller synfältet. Två mellanförsök förkastades efter bilderna: kunden vid kortänden stod fortfarande för nära, och en större kund bakom disken fick ansiktet för högt. |
| – | S8 på headset | **Inte provat** av utvecklaren. |
| – | Barnets acceptanstest (S7.3, [PLAYTEST.md](PLAYTEST.md) del 2) | **Inte gjort.** |
