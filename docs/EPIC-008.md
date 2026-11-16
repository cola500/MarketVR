---
title: EPIC-008 – marknadsfest och vilopaus
status: installed-not-tried
last_updated: 2026-11-01
---

# EPIC-008 – marknadsfest och vilopaus

**Produktmål:** spelaren får ett glatt avslut på en spelomgång och en naturlig anledning att ta av headsetet och vila. När hon har tjänat **80** spelpengar under omgången blir det en kort **marknadsfest**, följd av ett tydligt **pausläge**. Hon kan också ta paus tidigare.

**Hypotes, inte faktum:** att 80 pengar är en lagom lång omgång är obekräftat. Tiden till 80 loggas och förs in i PLAYTEST efter speltestet. Regler: [SPEC §25](SPEC.md#25-marknadsfest-och-vilopaus-epic-008). Epicen hålls skild från spelplanskalibreringen (EPIC-007): inga ändringar där.

## Tillstånd

```
Pågående omgång --(intäkt ≥ tröskel)--> Fest väntar --(redo)--> Firande (≈ 12 s) --> Vilopaus --(föräldern: Starta nästa omgång)--> Pågående omgång
Pågående omgång --(menyn: Ta vilopaus)--------------------------------------------> Vilopaus
Alla tillstånd --(Nytt spel)--> Pågående omgång (allt nollställt)
```

- **Intäkt i omgången:** summan av försäljningar som registrerats av `Shop.TryPay` sedan omgången startade. Det finns ingen egen betalningslogik. Kassan (`Wallet`) behålls alltid, och pausen kostar ingenting.
- **Tröskeln** är konfigurerbar och 80 som standard. För headsettest kan utvecklaren lägga en lägre tröskel i `fest.txt` i appens datakatalog (adb push). Det finns ingen genväg bland barnets kontroller.
- **Fest väntar** (tröskeln nådd):
  - Inga nya NPC-kunder kommer. Den aktuella kunden tackar och går, och en leksak som just sålts kommer fram till lekbordet.
  - En mobilorder som redan accepterats (väntar eller betjänas) betjänas klart, så att ingenting försvinner. Nya mobilorder avvisas med orsaken `paused`.
- **Redo för firande** betyder:
  - ingen kund vid disken och ingen mobilorder kvar;
  - inget barn på väg till lekbordet;
  - krokodilen dansar inte sin egen dans;
  - inget hålls i händerna;
  - ingen meny, kalibrering, systemmeny eller fokusförlust.
  Håller spelaren något visas en vänlig bild med en öppen hand och texten ”Släpp det du håller”.
- **Firande** (≈ 12 s, tidsförloppet pausas av fokusförlust och menyer):
  - Skylten över fruktståndet visar ”Marknadsfest!” med stjärnor.
  - En stjärna läggs på festtavlan.
  - Kunden och barnen vinkar glatt.
  - Mjuka fyrverkeriblommor syns högt ovanför marknaden och konfetti dalar en kort stund.
  - Krokodilen gör en festdans som inte påverkar dess eget påskägg.
  - En kort mjuk fanfar spelas.
  - Inga skarpa blinkningar, inga smällar och inget nära ansiktet.
  Firandet sker högst en gång per omgång.
- **Vilopaus:**
  - Skylten visar ”Pausdags!”, och en tavla framför spelaren visar ett avtaget headset och texten ”Ta av headsetet och vila en stund.”
  - Inga nya kunder, ingen försäljning, inget krokodilspel, ingen aktiv lek (barnen och leksakerna står stilla) och inga grepp.
  - Ljudet tonas ned. Ingen nedräkning, ingen förlorad belöning och ingen bonus.
  - En vanlig tryckning avslutar inte pausen.
- **Starta nästa omgång** finns i menyn under **För föräldrar** och kräver bekräftelse (Nej är förvalt). Det är ingen säker föräldralåsning och kallas inte det. Kassan och festtavlans stjärnor behålls, intäkten i omgången nollställs, kunder och lek fortsätter, och en ny fest kan inte utlösas direkt.
- **Ta vilopaus** finns i spelmenyn och ger samma lugna paus utan krav på 80 pengar och utan stjärna. Tröskeln hindrar aldrig en tidigare paus.
- **Nytt spel** (befintlig bekräftelse) nollställer allt: kassan, intäkten, festen, stjärnorna och pausen.
- **Mobilen:** när marknaden har fest eller paus visar mobilsidan ”Marknaden har paus”, och Beställ är avstängd. Order under pausen avvisas med orsaken `paused`.

## Stories och acceptanskriterier

### S1 – Intäktsgräns och säkert avslut av omgången

| # | Kriterium | Verifiering |
|---|---|---|
| F1 | Under tröskeln: ingen fest. Vid exakt tröskeln och vid passage över den: fest väntar. Köpet som passerar registreras helt och exakt en gång. | [T: Round_Under80NoParty] [T: Round_Exactly80AndPassingMakePartyPending] |
| F2 | En upprepad försäljningshändelse räknas inte dubbelt. | [T: Round_RepeatedSaleEventNotCounted] |
| F3 | När festen väntar kommer inga nya NPC-kunder. Den aktuella kunden avslutas, och en redan accepterad mobilorder betjänas klart. | [T: Round_NoNewCustomersWhenPartyPendingOrResting] [T: Round_AcceptedPhoneOrderServedBeforeParty] |
| F4 | Firandet börjar först när kunden är klar, inget barn är på väg, krokodilen inte dansar, händerna är tomma och ingen meny eller fokusförlust pågår. | [T: Round_CelebrationWaitsUntilReady] [T: Scene_PartyWaitsForHeldItem] |
| F5 | Kassan behålls. Tröskeln är konfigurerbar och 80 som standard. Tiden till tröskeln registreras. | [T: Round_WalletKeptThresholdConfigurableTimeRecorded] |

### S2 – Enkelt firande

| # | Kriterium | Verifiering |
|---|---|---|
| F6 | Firandet: skylten ”Marknadsfest!”, en stjärna på festtavlan och krokodilens festdans. Ungefär 12 s, en gång per omgång. | [T: Round_CelebrationOncePerRound] [T: Scene_PartyAfterThresholdSale] |
| F7 | Krokodilens festdans konkurrerar inte med påskägget, och krokodilspelet och påskägget fungerar efter pausen. | [T: Croc_PartyDanceDoesNotCountAsSurprise] [T: Scene_CrocWorksAfterRest] |

### S3 – Vilopaus och nästa omgång

| # | Kriterium | Verifiering |
|---|---|---|
| F8 | Vilopaus: ”Pausdags!”, bild och text framför spelaren, inga kunder, ingen försäljning, inga grepp, inget krokodilspel, stillastående lek och nedtonat ljud. En tryckning avslutar inte pausen. | [T: Scene_RestIsCalm] |
| F9 | Starta nästa omgång under För föräldrar med bekräftelse (Nej förvalt): kassan och stjärnorna behålls, intäkten nollställs, kunder och lek fortsätter, och ingen ny fest kommer direkt. | [T: Menu_ParentPageNextRoundNeedsConfirmation] [T: Round_NextRoundResetsCounterNoImmediateParty] [T: Scene_NextRoundContinues] |
| F10 | Ta vilopaus i menyn: samma paus, ingen stjärna, inget krav på tröskeln. | [T: Round_ManualRestNoStar] [T: Scene_ManualRestFromMenu] |
| F11 | Nytt spel nollställer kassan, intäkten, stjärnorna, festen och pausen. | [T: Round_NewGameResetsEverything] |

### S4 – Fyrverkerier, applåder och ljud

| # | Kriterium | Verifiering |
|---|---|---|
| F12 | Mjuka fyrverkeriblommor högt ovanför marknaden (minst 1,8 m över golvet, aldrig vid ansiktet) och dalande konfetti, högst 120 effektobjekt. Kunden och barnen vinkar. En mjuk fanfar spelas. Effekterna tas bort efter firandet. | [T: Scene_PartyEffectsBoundedAndAwayFromFace] [R] |
| F13 | Inga blinkningar eller smällar: färgerna tonar mjukt och ljudet är lågt. | [H] |

### S5 – Mobilstatus och robusthet

| # | Kriterium | Verifiering |
|---|---|---|
| F14 | Mobilorder under fest eller paus avvisas med `paused`, och mobilen visar ”Marknaden har paus”. Status om marknaden skickas utan bilddata. | [T: Protocol_OrdersRejectedWhilePaused] [T: Relay_MarketStatusReachesPhones] |
| F15 | Fokusförlust pausar firandets tidsförlopp. Efter återkomst krävs släppta och neutrala kontroller (befintligt skydd). | [T: Round_FocusLossPausesCelebration] [T: Router_*] |
| F16 | Försäljning, mobilkund, gång, krokodil och kalibrering fungerar som förut. Spelet fungerar utan mobil eller server. | [T: alla befintliga] |
| F17 | APK byggs. | [B] |
| F18 | Headsettest: festen känns glad, pausen är begriplig, ljud och fyrverkerier är behagliga, och nästa omgång fortsätter korrekt. | [H] [P] |

### S5b – Föräldern pausar och startar nästa omgång från mobilen (tillägg 2026-11-01)

En diskret del **Förälder** på mobilsidan har knapparna **Pausa spelet** och **Starta nästa omgång**. Båda kräver en uttrycklig bekräftelse. Det är en praktisk föräldrakontroll inom den lokala sessionen (samma sessionskod), **inte en säker barnspärr**.

- **Eget meddelande**, skilt från kundens (`order`, `photo`, `sync`): `{"t":"parent","cmd":"rest"|"nextRound","cmdId":"…"}`. Relän släpper bara igenom just dessa två kommandon, från en mobil med rätt sessionskod och med giltigt id. Inget annat fält följer med. Det finns ingen väg att ändra kassa, order eller tillstånd godtyckligt.
- **Samma flöde som menyn:** `rest` blir samma säkra vilopaus som Ta vilopaus. Den tas i spelets nästa steg: det som hålls går hem, ingen stjärna ges och 80 pengar krävs inte. `nextRound` gäller bara under vilopaus.
- **Exakt en gång:** samma `cmdId` igen ger bara en kvittens (`duplicate`), aldrig en ny paus eller omgång.
- **Kvittens i två steg:** Quest svarar `{"t":"parentAck","cmdId","cmd","accepted","duplicate"}` direkt. Mobilen visar ”Spelet är pausat” först när Quest skickat marknadens nya tillstånd `{"t":"market","state":"resting"}`. Relän sparar senaste tillståndet och ger det till mobiler som ansluter.
- **Bruten anslutning:** visas som ”Inte bekräftat – ingen anslutning till spelet”. Mobilen påstår aldrig att spelet pausats utan Quests besked.

| # | Kriterium | Verifiering |
|---|---|---|
| F19 | Bara `rest` och `nextRound`, i ett eget meddelande, från en mobil med rätt sessionskod. Annat avvisas eller ignoreras. Kunden kan inte ändra kassa eller tillstånd. | [T: Relay_ParentCommandsOnlyKnownFromAuthorisedPhone] [T: Protocol_ParentCommandsSeparateFromCustomer] |
| F20 | Samma kommando-id igen ger bara en kvittens. Paus och återupptagning sker exakt en gång. | [T: Protocol_ParentCommandsExactlyOnce] |
| F21 | Kvittens direkt. ”Spelet är pausat” först efter Quests marknadsstatus. Status för marknaden skickas vid ändring, utan bilddata, och relän ger den till nya mobiler. | [T: Protocol_MarketStatusOnChange] [T: Relay_MarketStatusReachesPhones] |
| F22 | I scenen: pausen från mobilen stoppar nya order direkt, skickar hem det som hålls, ger ingen stjärna, och nästa omgång från mobilen fortsätter spelet. | [T: Scene_PhoneParentPauseAndNextRound] |
| F23 | Mobilsidan: bekräftelse före sändning och ”Inte bekräftat” vid bruten anslutning. | [D] (sidan har inga automatiska tester; syntaxen kontrolleras) |

## Verifieringslogg

| Datum | Vad | Resultat |
|---|---|---|
| 2026-11-01 | Epic, acceptanskriterier och PLAYTEST skrivna före implementation | – |
| 2026-11-01 | TDD cykel 1: intäktsgräns (S1) | `MarketRoundTests` (F1–F3, F5) skrevs först. Rött: `Market(threshold:)` saknades. Därefter `MarketRound`, kopplad i `Shop.TryPay` och vid val av nästa kund. **Fel hittat:** en mobilorder direkt efter köpet som passerade tröskeln släpptes igenom, eftersom pausflaggan bara synkades i `Market.Tick`. Mobilen frågar nu omgången direkt (`PausedWhen`). Ordning: `paused` före `busy`. |
| 2026-11-01 | TDD cykel 2: regler (S1, S3) | `Round_CelebrationWaitsUntilReady`, `…OncePerRound`, `…NextRound…`, `…ManualRestNoStar`, `…FocusLoss…` och `…NewGame…`. Ärligt: tillståndsmaskinen skrevs i sin helhet i cykel 1, så de här testerna kördes mot färdig kod. De fångade ändå ett designfel: `Market.Tick(dt)` antog att presentationen var redo, så varje vanligt tick kunde starta festen. Standarden är nu *inte redo*. |
| 2026-11-01 | TDD cykel 3: krokodilens festdans (F7) | `Croc_PartyDanceDoesNotCountAsSurprise` skrevs först. Rött: `StartPartyDance` saknades. Därefter grönt. |
| 2026-11-01 | TDD cykel 4: meny (F9, F10) | `Menu_ParentPageNextRoundNeedsConfirmation` skrevs först. Rött: typerna saknades. Därefter Ta vilopaus, sidan För föräldrar och bekräftelsen. Grönt. |
| 2026-11-01 | TDD cykel 5: scen (S2–S4) | Sju scentester skrevs före `PartyView`. Rött: typerna saknades. Därefter grönt, utom effekttestet. Det mätte först mot Editorns översiktskamera (ingen spelare) och skärptes till barnhuvuden på 0,9–1,6 m och 0,8 m avstånd. Då hittade det att konfettin dalade till 1,85 m, bara 25 cm över en stående spelares ögon. Effekterna lyftes (blommor 2,9–3,5 m, konfetti borta vid 2,45 m), och testet blev grönt. |
| 2026-11-01 | TDD tillägg: föräldern via mobilen (S5b) | `Protocol_ParentCommandsSeparateFromCustomer`, `…ExactlyOnce`, `Protocol_MarketStatusOnChange`, `Protocol_OrdersRejectedWhilePaused`, `Scene_PhoneParentPauseAndNextRound` och två relätester skrevs först och var röda (typerna saknades, `accepted:false`, okända meddelandetyper). Därefter grönt. Pausen sätts i reglerna när kommandot tas emot (nya order stoppas direkt), och det som hålls går hem i spelets nästa steg. Mobilsidan har inga automatiska tester; syntaxen är kontrollerad med `node --check`. |
| 2026-11-01 | Renderade bilder [R] | `21-market-party`, `22-market-party-from-the-work-area` och `23-rest-break`. Första bilden visade att texten var bredare än skylten och att fruktståndets bildskylt syntes igenom. Skylten flyttades fram och breddades. Paustavlans pil gick in i rubriken och flyttades ned. Efter rättningen ser båda rätt ut. Fyrverkerierna ligger utanför bildernas utsnitt. |
| 2026-11-01 | Automatiska tester [T] | **EditMode 143/143, PlayMode 68/68** (3 explicita överhoppade). Efter bildrättningen kördes festens scentester om: 9/9. Relä **11/11**. Editor-kedjan för mobilkunden 2/2 (port 8788). |
| 2026-11-01 13:22 | APK-bygge [B] | `2026-11-01_132248_ba7f845` (rent). Inte installerat. |
| 2026-11-01 | Installation på Quest 3 (`QUEST-SERIAL`, USB) | `install`: `Success`, utan avinstallation (före: `…_125412_34bec1d`). `installed` visar `1.0+2026-11-01_132248_ba7f845`. `launch`: appen startade, loggen visar bygg-id och att marknaden placerades. Inga undantag. Ingen `fest.txt` på headsetet, så tröskeln är 80 (ingen utvecklarrad i loggen). Bara via ADB. |
| – | Headsettest [H] (F13, F18) och barnets test [P] | **Inte gjort.** Barnets reaktion förs in först när föräldern rapporterat. |
