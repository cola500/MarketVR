---
title: EPIC-001 – Första köpen i fruktståndet
status: implemented-installed (headset 2026-10-31; barnets speltest inte genomfört)
last_updated: 2026-11-01
---

# EPIC-001 – Första köpen i fruktståndet

**Mål:** ett barn kan stå bakom ett fruktstånd i VR, ta emot en kunds beställning (bilder och antal), plocka rätt varor till korgen, ta betalt med en myntbricka och få en glad återkoppling. Sedan kommer nästa kund.

**Hypotes:** se [SPEC.md §2](SPEC.md#2-hypotes). **Regler:** se [SPEC.md §4](SPEC.md#4-regler-domänlogik).

**Verifieringssätt** (används i varje kriterium):
- **[T: Namn]** automatiskt Unity-test (EditMode = ren logik, PlayMode = den riktiga scenen).
- **[B]** kontrolleras i APK-bygget (byggs utan fel, manifest).
- **[H]** manuell kontroll på Quest 3 (av utvecklaren, inte barnet).
- **[R]** renderad bild utanför headsetet.

Status per kriterium uppdateras i avsnittet *Verifieringslogg* längst ned.

## Storyordning och beroenden

```
S1 Quest-bas + miljö
 └─ S2 Plocka äpple → korg (grepp-spike)
     └─ S3 Beställning + matchning
         └─ S4 Betalning + helt köp   ← första spelbara slicen (S1–S4 med en äppelkund)
             └─ S5 Flera kunder, återställning, felhantering
                 └─ S6 Tydlighet, ljud, slutligt headsettest
```

---

## S1 – Återanvändbar Quest-bas och fruktståndsmiljö

*Som förälder vill jag kunna installera och starta ett eget fruktståndsspel på Quest 3 utan att Magic Tiles påverkas.*

Beroenden: inga.

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S1.1 | Eget projekt under `lilla-fruktstandet/`, eget application id `com.lillafruktstandet.prototype`, appnamn ”Lilla fruktståndet”. Inga genererade Unity-mappar, Asset Store-paket eller hemligheter kopierade. | [B] manifest via `aapt2`, granskning av filträdet |
| S1.2 | `scripts/quest.sh build|install|launch|all|log` fungerar som i Magic Tiles och avinstallerar aldrig något. | [B] [H] |
| S1.3 | Appen startar i VR på Quest 3 och XR-sessionen når fokuserat läge. | [H] logg |
| S1.4 | Solig miljö med fruktstånd: disk, randigt tak, tre lådor, korg, kassa. | [R] [H] |
| S1.5 | Disken ställs framför spelaren vid start och kan höjas/sänkas med tumspaken (50–120 cm). Allt på disken följer med. | [T: CounterHeight_MovesEverythingOnCounterAndClamps] [T: Router_CounterStickDeadZoneAndAxis] [H] |
| S1.6 | Utan headset startar samma scen i desktopläge. | [T: Scene_StartsInDesktopModeWithoutXr] |

## S2 – Plocka ett äpple och lämna det i en korg i VR (grepp-spike)

*Som barn vill jag ta ett äpple ur lådan och lägga det i korgen, utan att behöva sikta noga.*

Beroenden: S1.

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S2.1 | Handen nära en vara (≤ 12 cm) markerar varan. Grepp- eller avtryckarknapp tar den. Den följer handen. | [T: Grab_PicksNearestItemWithinReach] [T: Scene_AppleFromCrateToBasket] [H] |
| S2.2 | Att hålla knappen inne och föra handen över en vara tar den inte. Ett nytt tryck krävs. | [T: Grab_HeldButtonDoesNotGrabOnContact] |
| S2.3 | Släppt inom korgens fångstradie hamnar varan stabilt på en ledig plats. | [T: Drop_NearBasketSnapsToFreeSlot] [T: Scene_AppleFromCrateToBasket] [H] |
| S2.4 | Släppt någon annanstans går varan tillbaka till sin låda. Inget faller till marken. | [T: Drop_ElsewhereReturnsHome] [T: Scene_DroppedItemReturnsToCrate] [H] |
| S2.5 | Lådan har alltid en vara att ta (obegränsat lager). | [T: Scene_CrateRefillsAfterGrab] |
| S2.6 | Båda händerna kan plocka. | [T: Grab_TwoHandsHoldTwoItems] [H] |
| S2.7 | Tappar handen spårningen medan den bär något går saken hem (släpps inte i en zon). | [T: Grab_LostTrackingWhileHolding] |

## S3 – Beställning och korrekt matchning

*Som barn vill jag se vad kunden vill ha och se när jag har lagt rätt saker i korgen.*

Beroenden: S2.

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S3.1 | Beställningar har totalt 1–3 varor av de tre sorterna. Första beställningen är ett äpple. Samma frö ger samma följd. Två kunder i rad får aldrig samma beställning. | [T: Orders_FirstIsOneApple] [T: Orders_TotalOneToThree] [T: Orders_Deterministic] [T: Orders_NoRepeatInARow] |
| S3.2 | Pratbubblan visar en bild per vara och en siffra per sort. Läsning krävs inte. | [R] [H] |
| S3.3 | Matchning kräver exakt antal per sort: för få, för många och fel sort matchar inte. | [T: Match_ExactCounts] [T: Match_TooFewTooManyWrongKind] |
| S3.4 | En bock tänds per vara som ligger rätt i korgen. | [T: Progress_SatisfiedPerOrderedUnit] [R] |
| S3.5 | En vara som tas ur korgen räknas inte längre. | [T: Match_RemovedItemNoLongerCounts] |
| S3.6 | Korgen har 4 platser. En vara som inte får plats går tillbaka till lådan. | [T: Basket_FullRefusesAndItemReturns] [T: Basket_PreferredSlotUsedWhenFree] |
| S3.7 | Korgen tar inte emot varor medan kunden går fram. | [T: Sale_ArrivingCustomerDoesNotTakeItemsYet] |

## S4 – Betalning och ett komplett köp (första spelbara slicen)

*Som barn vill jag ta betalt och höra att köpet är klart.*

Beroenden: S3. **Första spelbara slicen:** en kund vill ha ett äpple → plocka → korg → myntbricka → kassa → tack → samma loop igen.

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S4.1 | När korgen matchar låses den och kunden lägger fram en myntbricka. | [T: Sale_MatchMovesToAwaitingPaymentAndLocksBasket] [T: Scene_FullSaleWithHands] |
| S4.2 | Myntbrickan i kassan registrerar köpet exakt en gång. Upprepade händelser räknas inte. | [T: Sale_PaymentRegistersExactlyOnce] [T: Scene_RepeatedRegisterEventsCountOnce] |
| S4.3 | Betalning innan korgen matchar avvisas. | [T: Sale_PaymentBeforeMatchIsRejected] [T: Scene_PaymentBeforeMatchHasNoCoin] |
| S4.4 | Myntbrickan släppt utanför kassan går tillbaka till sin plats på disken. Släppt där den ligger betalar den inte. | [T: Drop_CoinElsewhereReturnsToTray] [T: Scene_CoinDroppedElsewhereReturnsToTray] |
| S4.5 | Köpet ger pling, stjärnor och ”Tack!”. Därefter töms korgen, kunden går och nästa kund kommer. | [T: Sale_ThanksThenLeavesThenNextCustomer] [T: Scene_FullSaleWithHands] [H] |
| S4.6 | Hela loopen går att spela igen efter första köpet. | [T: Scene_TwoSalesInARow] [H] |

## S5 – Flera kunder i följd, återställning och felhantering

*Som barn vill jag kunna fortsätta sälja och rätta mina misstag själv.*

Beroenden: S4.

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S5.1 | Kunderna kommer i följd utan slut och ser lite olika ut. | [T: Sale_ThanksThenLeavesThenNextCustomer] [R] |
| S5.2 | Fel vara kan tas ur korgen och läggas tillbaka utan straff. | [T: Match_RemovedItemNoLongerCounts] [T: Scene_WrongItemCanBeRemovedByHand] |
| S5.3 | Spelmeny: Fortsätt, Visa kontroller, Börja om. Börja om kräver bekräftelse (Nej förvalt) och ger första beställningen igen. *Ersatt i EPIC-002 S6.3–S6.4 av Töm korgen och Nytt spel.* | [T: Menu_NewGameNeedsConfirmationNoIsDefault] [T: Menu_ShowControlsAndBack] [T: Menu_ResumeCloses] [T: Sale_RestartResetsEverything] [T: Scene_RestartFromMenu] [H] |
| S5.4 | Utan inputfokus, vid paus eller med headsetet av sker inga spelhandlingar och kontrollerna döljs. Det man håller i går tillbaka hem. | [T: Router_NoFocusBlocksEverything] [T: Router_FocusLossReleasesHeldItems] [T: Scene_FocusLossReturnsHeldItemHome] [H] |
| S5.5 | Efter fokus, paus, headset eller stängd meny krävs släppta knappar. Inget oavsiktligt grepp, släpp eller köp. | [T: Router_HeldButtonAfterFocusDoesNothingUntilReleased] [T: Router_ClosingMenuWithTriggerDoesNotGrab] [T: Router_MenuPressedThroughFocusLossDoesNotOpen] [H] |
| S5.6 | Systemmenyn fungerar som vanligt (Meta-knappen läses aldrig). | [H] |

## S6 – Tydlighet, ljud och slutligt headsettest

*Som barn vill jag förstå vad som händer utan att läsa.*

Beroenden: S5.

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S6.1 | Varorna skiljs med färg och form (rund röd, lång böjd gul, spetsig orange). | [R] [H] |
| S6.2 | Ljud: pop vid korg, pling vid köp. Allt viktigt syns även utan ljud. | [T: Sounds_AreGenerated] [H] |
| S6.3 | Allt nödvändigt nås sittande vid justerbar diskhöjd. | [H] |
| S6.4 | Slutligt headsettest enligt README-protokollet: tre köp i rad, ett felaktigt objekt rättat, meny, systemmeny, av/på. | [H] |
| S6.5 | Speltestmanus finns ([PLAYTEST.md](PLAYTEST.md)). | dokument |

---

## Manuellt headsettest

Utförs av utvecklaren på Quest 3, sittande. Kryssa i och för in i loggen nedan.

1. **Start (S1.3, S1.4).** `scripts/quest.sh all`, sätt på headsetet. Förväntat: ståndet framför dig, kunden går fram och säger ”Hej!”. `scripts/quest.sh log` visar `[Fruktstand] stand placed`.
2. **Höjd och räckvidd (S1.5, S6.3).** Spak upp/ned på båda händer. Sänk och höj disken. Nå alla tre lådor, korgen och kassan sittande utan att luta dig långt.
3. **Grepp-spiken (S2).** För handen till äpplet: det växer, handen blir gul. Tryck grepp: äpplet följer. Prova avtryckaren också. Håll knappen inne och svep över bananen: ingenting tas. Släpp i luften: varan glider tillbaka. Båda händer samtidigt.
4. **Första köpet (S4, slicen).** Äpple i korgen → bock, glad kund, grön korgkant, myntbricka. Myntbrickan över kassan: springan lyser gult. Släpp: pling, stjärnor, ”Tack!”, kunden vinkar och går.
5. **Följande kunder (S3, S5.1).** Tre köp i rad med 1–3 varor. Bubblan visar en bild per vara och siffror. Lägg en fel vara: den vickar och ingen bock tänds. Ta ur den och slutför köpet (S5.2).
6. **Systemmenyn (S5.4–S5.6).** Håll en vara över korgen och tryck Meta-knappen. Förväntat: händerna försvinner, varan går hem (inte i korgen). Stäng systemmenyn med greppet nedtryckt: inget tas förrän du släppt och tryckt igen.
7. **Spelmenyn (S5.3).** Menyknappen → Visa kontroller → Tillbaka → Börja om → Nej (allt kvar) → Börja om → Ja (första kunden, ett äpple). Stäng med avtryckaren på Fortsätt: inget tas.
8. **Av/på.** Ta av headsetet en kort stund och en gång tills det somnar. Spelet står kvar, inget händer förrän knapparna släppts. Loggen visar `headset off/on`, `app paused/resumed`.
9. **Ljud (S6.2).** Pop vid korg, pling vid köp. Stäng av ljudet: allt syns ändå.

## Verifieringslogg

Uppdateras löpande. Barnets speltest ingår **inte** och är inte genomfört.

| Datum | Vad | Resultat |
|---|---|---|
| 2026-10-31 | Specifikation och epic skrivna före implementation | – |
| 2026-10-31 | Automatiska tester (`scripts/quest.sh test`, batchmode) | **EditMode 33/33, PlayMode 13/13 gröna.** Täcker alla [T]-kriterier ovan. Testerna hittade en bugg som rättades: att ta ur en felaktig vara kontrollerade inte om korgen då matchade (S5.2). |
| 2026-10-31 | Renderade kontrollbilder (`Snapshots`, Metal) | Sittande vy och desktopvy: ”Hej!”, första beställningen, myntbricka + bild mynt→kassa, ”Tack!” med stjärnor, andra beställningen med en bock och en fel vara, låg disk. Efter första bilderna rättades överlagrat innehåll i bubblan vid ombyggnad och myntbilden på kassan byttes mot en guldkant (såg ut som ett mynt att ta). S1.4, S3.2, S3.4, S5.1, S6.1 kontrollerade visuellt utanför headset. |
| 2026-10-31 | APK-bygge (`scripts/quest.sh build`) | **Lyckades, 23 MB.** `aapt2`: paket `com.lillafruktstandet.prototype`, etikett ”Lilla fruktståndet”, target SDK 34, `com.oculus.intent.category.VR`, headtracking, bara arm64-v8a med OVRPlugin/OculusXRPlugin. Vakten lade tillbaka 2 XR-poster i `preloadedAssets`. S1.1, S1.2 (build). |
| 2026-10-31 | Installation på Quest 3 | Vid första försöket var inget headset anslutet. Senare samma dag: `adb install -r` lyckades på Quest 3 (`eureka`) och appen startades. Loggen visar att XR kom igång, `[Fruktstand] stand placed` (disken på 0,50 m, den lägsta tillåtna höjden; headsetet låg troligen inte på huvudet vid start), 72/72 FPS i startläget och 0 Unity-fel. Det verifierar S1.3 (start). Övriga [H]-kriterier är inte återrapporterade punkt för punkt. |
| 2026-10-31 | Rapport från föräldern | Barnet spelade på egen hand i cirka 15 minuter och önskade köpa godis för pengarna → [EPIC-002](EPIC-002.md). Det är en rapport, inte ett genomfört speltest enligt PLAYTEST.md. |
| – | Barnets speltest ([PLAYTEST.md](PLAYTEST.md)) | **Inte genomfört.** |
