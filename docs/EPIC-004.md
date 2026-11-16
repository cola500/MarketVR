---
title: EPIC-004 – förälder som kund från mobilen
status: device-partly-verified (köp från mobil på Quest 2026-11-01; fullt enhetstest återstår)
last_updated: 2026-11-01
---

# EPIC-004 – förälder som kund från mobilen

**Produktmål:** spelaren säljer i Quest. Föräldern öppnar en webbsida på mobilen, ansluter till samma spelpass och beställer som kund. Kunden i VR visar förälderns foto. Spelaren plockar och tar betalt med de vanliga kontrollerna, och mobilen visar när köpet är klart.

**Vägval:** verifierat i [SPIKE-004](SPIKE-004.md) (Editor och bygge, ännu inte på enheter). Relä i Node på MacBooken, WebSocket från mobil och Quest, UDP-fyr för att Quest ska hitta relän, och råa 128 × 128-pixlar för fotot. Quest är auktoritativ. Regler: [SPEC §21](SPEC.md#21-kund-från-mobilen-epic-004).

**Avgränsning:** ingen synkroniserad fysik, ingen fjärrstyrd kundfigur, ingen röst eller video, inget internetspel, en mobilkund åt gången och ingen delad styrning av VR-världen. Mobilen väljer beställning och följer köpet. Kundfiguren och betalningsloopen är de befintliga.

Verifieringssätt: **[T: Namn]** automatiskt test (Unity EditMode/PlayMode eller `relay/test`), **[E: Namn]** Editor-kedja med riktig relä (`scripts/kund.sh spike`), **[R]** renderad bild, **[B]** APK-bygge, **[D]** enhetstest (Quest + mobil på samma wifi), **[P]** förälderns acceptanstest.

## S1 – Lokal anslutning

*Som förälder vill jag att mobil och Quest hittar varandra via datorn på samma wifi, utan internet eller konton.*

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S1.1 | Relän (`scripts/kund.sh start`) serverar mobilsidan och tar emot WebSocket från mobil och Quest på port 8787. Inga npm-paket, inget internet. | [T: Relay_ServesPhonePageAndLocalStatus] [E: RelayChain_EditorEndToEnd] |
| S1.2 | Quest hittar relän via dess UDP-fyr (port 47811) utan inmatning. Reserv för utvecklaren: `relay.txt` i appens datakatalog. | [T: Beacon_ParsesPort] [E: `kund.sh spike beacon`] [D] |
| S1.3 | Mobilen ansluter bara med rätt sessionskod (4 siffror). Fel kod avvisas. | [T: Relay_WrongSessionCodeIsRefused] |
| S1.4 | Quest-appen begär `INTERNET` och har WebSocket-klienten kvar efter IL2CPP-bygget. Anslutningen fungerar i APK:n på Quest. | [B] [D] |
| S1.5 | Utan relä är spelet exakt som vanligt. Inget väntar på nätverket, och inget fel når spelaren. | [T: alla EPIC-001–003-tester, körda utan relä] [E: RelayChain_RelayRestartKeepsOrder] [D] |

## S2 – En mobilorder och återkoppling

*Som förälder vill jag beställa ett äpple från mobilen och se när spelaren har sålt det.*

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S2.1 | En mobilorder blir en vanlig `Order` i samma `Shop`. Kunden står vid ståndet för kategorin, och försäljningen görs med korg, bricka och kassa. | [T: Remote_OrderTakesTheNextFreeTurn] [T: Scene_PhoneOrderSoldWithHandsOnce] |
| S2.2 | Mobilkunden får nästa lediga kundtur. En NPC som redan står vid disken avbryts inte. Inga nya NPC-kunder kommer medan mobilkunden väntar eller betjänas. Stängt pausar även mobilkunden. | [T: Remote_NoNewNpcWhileHumanWaitsOrIsServed] [T: Remote_WaitsWhileMarketClosed] |
| S2.3 | Bara frukt/grönt eller godis, 1–3 varor av samma kategori. Allt annat avvisas med en orsak. Summan räknas alltid ut på Quest. | [T: Remote_InvalidRequestsAreRejected] [T: Remote_PaymentCreditsOnceAndIsDone] |
| S2.4 | Mobilen kan inte ändra kassa, orderstatus eller betalning: relän släpper bara igenom `order`, `photo` och `sync`, och Quest ignorerar allt annat och alla extra fält. | [T: Protocol_PhoneCannotChangeTheTill] [T: Relay_OrderReachesQuestWithOnlyAllowedFields] |
| S2.5 | Mobilen visar: ansluter, ansluten, väntar på tur, beställning mottagen, köp klart (”Tack, köpet är klart!”) och frånkopplad. | [T: Protocol_OrderJsonBecomesCustomer] [E: RelayChain_EditorEndToEnd] [D] |
| S2.6 | En mobilkund och en aktiv mobilorder åt gången. En annan order under tiden avvisas (”busy”). | [T: Remote_OneActiveOrderAtATime] |
| S2.7 | Nytt spel avbryter en pågående mobilorder utan betalning, och mobilen får beställa igen. | [T: Remote_NewGameCancelsTheOrder] [T: Scene_NewGameDuringPhoneOrder] |

## S3 – Foto på kunden

*Som förälder vill jag att spelaren ser mitt foto på kunden.*

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S3.1 | Mobilen beskär fotot till en mittkvadrat, skalar ned till 128 × 128 och skickar det en gång som råa pixlar. Bara JPEG, PNG, WebP och HEIC/HEIF, högst 20 MB in. | [T: Relay_OversizePhotoAndStatusWithImageDataAreDropped] [D] |
| S3.2 | Quest tar bara emot kvadratiska 16–128 px med exakt rätt längd. Bilden avkodas en gång per id, högst 3 sparas i minnet och ingenting sparas på disk. | [T: Photo_LimitsAreEnforced] [T: Photo_DecodedOnceTopRowUp] |
| S3.3 | Fotot visas som en enkel inramad porträttbild framför kundens ansikte, rätt väg upp. Hatten döljs. Ingen 3D-avatar och ingen animering. | [T: Scene_PhoneOrderSoldWithHandsOnce] [R: 15-phone-customer-with-photo] |
| S3.4 | Utan foto visas den vanliga kundfiguren. | [T: Scene_PhoneOrderWithoutPhotoUsesTheUsualFigure] |
| S3.5 | Bilddata finns aldrig i statusmeddelanden. Relän skickar fotot till Quest bara när Quest saknar det, och mobilen laddar inte upp det igen efter återanslutning. | [T: Protocol_StatusNeverCarriesImageData] [T: Relay_PhotoIsCachedAndSentToQuestOnlyWhenMissing] |

## S4 – Återanslutning och skydd mot dubbla händelser

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S4.1 | Varje order har ett slumpat id från mobilen. Samma id igen, under eller efter köpet, ger aldrig en andra kund eller betalning. | [T: Remote_SameIdNeverMakesASecondOrder] [E: RelayChain_EditorEndToEnd] |
| S4.2 | Kassan krediteras bara i `Shop.TryPay`, exakt en gång. Ordern blir `done` i samma övergång. | [T: Remote_PaymentCreditsOnceAndIsDone] [T: Scene_PhoneOrderSoldWithHandsOnce] |
| S4.3 | Vid frånkoppling behåller Quest en accepterad order. Spelet fortsätter, och kunden kommer i tur och ordning. | [E: RelayChain_RelayRestartKeepsOrder] |
| S4.4 | Quest återansluter själv. Mobilen återansluter själv och frågar Quest om status (`sync`). En order som aldrig bekräftats skickas igen med samma id. | [E: RelayChain_RelayRestartKeepsOrder] [T: Relay_OrderWhileQuestOfflineTellsThePhone] [D] |
| S4.5 | Om Quest-appen startats om (ordern okänd) visar mobilen det och skapar inte en ny order av sig själv. | [T: Remote_StatusAfterReconnect] (mobilsidans logik: [D]) |

## S5 – Enkel start och anslutning för föräldern

| # | Acceptanskriterium | Verifiering |
|---|---|---|
| S5.1 | `scripts/kund.sh start` startar relän och visar en QR-kod, adressen och sessionskoden i terminalen. Värdsidan med stor QR-kod och Quest-status öppnas på MacBooken. | [T: Relay_ServesPhonePageAndLocalStatus] [T: Qr_EncodesUrlSizes] (QR kontrollerad mot CoreImage) |
| S5.2 | QR-koden innehåller sessionskoden. Utan QR skriver man adressen och anger koden på sidan. | [D] |
| S5.3 | Vanliga hinder står i README: gästnät med klientisolering, olika nät (2,4/5 GHz går bra, gästnät inte), macOS brandvägg (svara Tillåt) och VPN. Ingen automatisk brandväggsändring. | dokument |
| S5.4 | Menyn i Quest visar bygg-id, så att man ser vilken version som körs. | [T: Menu_ShowsBuildId] |

## Enhetstest (Quest + mobil)

Görs av utvecklaren **när ingen spelar** och föräldern har gett klartecken att installera.

0. `scripts/quest.sh install UnityProject/Builds/archive/<bygget>.apk`. Skriptet skriver ut bygg-id. Kontrollera samma id i menyn.
1. `scripts/kund.sh start` på MacBooken (wifi). Svara Tillåt om macOS frågar om node.
2. Starta appen. `scripts/quest.sh log` ska visa `relay beacon from …` och `relay connected …` inom 10 s, och värdsidan ska visa ”Quest ansluten” (H1, H2).
3. Skanna QR-koden med mobilen. Sidan ska visa ”Ansluten”.
4. Välj ett foto och ett äpple, och tryck Beställ. Mobilen visar ”Väntar på tur” eller ”Beställning mottagen”.
5. I VR: kunden med fotot står vid fruktståndet (efter den kund som redan stod där). Sälj äpplet. Kassan ökar med 1. Mobilen visar ”Tack, köpet är klart!”.
6. **Avbrott:** beställ igen, stäng av wifi på mobilen i 20 s och slå på igen. Mobilen visar ”Frånkopplad” och sedan rätt status. Stoppa relän (Ctrl+C) medan kunden står vid disken, sälj klart i VR och starta relän igen. Quest och mobilen återansluter, mobilen visar ”klart” och kassan har ökat en gång.
7. 10 minuter med relän igång: notera eventuella frånkopplingar i loggen.
8. Stäng relän och spela vidare: spelet fungerar som vanligt.

## Verifieringslogg

| Datum | Vad | Resultat |
|---|---|---|
| 2026-11-01 | Spike-spec skriven före spiken | [SPIKE-004](SPIKE-004.md) |
| 2026-11-01 | Inventering | Inga nätverkspaket finns, och inga läggs till. `ClientWebSocket` och `UdpClient` ingår i .NET-runtimen. `imageconversion` saknas och undviks med råa pixlar. `INTERNET` slås på i `QuestBuild`. |
| 2026-11-01 | Implementation S1–S5 | Core: `RemoteCustomer` (begäran, validering, idempotens, status), kopplad till `Shop`/`Market`. Game: `RemoteSession` (protokoll), `PhotoStore`, `Portrait`, `RelayLink` (fyr, WebSocket, återanslutning; startar inte i batch-tester). Relä: `relay/server.mjs`, `ws.mjs`, `qr.mjs`, `public/phone.html`. Skript: `scripts/kund.sh`. |
| 2026-11-01 | Automatiska tester [T] | **EditMode 82/82, PlayMode 44/44** (3 explicita överhoppade: Snapshots och två reläkedjor). **Relä 9/9** (`node --test relay/test/`). Nya: 17 i `RemoteCustomerTests`, 3 i `PhoneCustomerSceneTests` och `Menu_ShowsBuildId`. |
| 2026-11-01 | Editor-kedja [E] | `scripts/kund.sh spike`: **2/2** (`RelayChain_EditorEndToEnd`, `RelayChain_RelayRestartKeepsOrder`). `scripts/kund.sh spike beacon`: 2/2, med upptäckt via fyren på 192.168.1.10. |
| 2026-11-01 | Renderad bild [R] | `15-phone-customer-with-photo`: inramat porträtt framför ansiktet, rätt orientering. Efter första bilden döljs hatten. |
| 2026-11-01 | APK-bygge [B] | `2026-11-01_105633_b2a36f0-dirty` (24 MB): `INTERNET` och WebSocket-typerna finns i bygget. |
| 2026-11-01 11:03 | Rent bygge [B] efter commit | `2026-11-01_110311_160d3de` (`gitDirty: false`, sha256 `38d5505e…`). |
| 2026-11-01 11:21 | Installation och start på Quest 3 (`QUEST-SERIAL`, USB) | `scripts/quest.sh install` av den arkiverade APK:n: `adb install -r` gav `Success`, utan avinstallation. Före installationen fanns `versionName=1.0` (2026-10-31 21:48). `scripts/quest.sh installed` visar `versionName=1.0+2026-11-01_110311_160d3de`. `scripts/quest.sh launch`: appen startade, loggen visar `[Fruktstand] build 2026-11-01_110311_160d3de` och att marknaden placerades (ögonhöjd 0,60 m vid start, troligen headsetet på bordet; omcentrera när det är på). Inga undantag i loggen. **Ingen relä startad**: Spelaren spelar med vanliga kunder, och mobilkunden är inte prövad. Bekräftat via ADB, inte genom att någon tittat i headsetet. |
| – | Enhetstest [D] | **Inte gjort.** Bygget är installerat (se ovan), men mobilkedjan (relä, fyr, WebSocket på Quest, mobil) är inte prövad på enheterna. |
| 2026-11-01 12:18–12:28 | **Enhetstest [D], första delen**, bygge `…_121238_8baa430` på Quest 3, relä på MacBooken (192.168.1.10), föräldern med mobil (192.168.1.42) på samma wifi | Relän startad med `scripts/kund.sh start`. **Quest hittade relän via UDP-fyren och anslöt utan inmatning** (12:18:33, `Quest connected from 192.168.1.20`). Därmed är H2 bekräftad: `ClientWebSocket` och fyren fungerar i IL2CPP-APK:n. Mobilen anslöt. Order 1 (äpple + banan) gick `waiting → serving → done`, och order 2 (polkagris) likadant. Föräldern: ”det funkade, köpet blev klart på mobilen”. Foton laddades upp (85 kB). 12:23:28 tappade både mobilen och Quest anslutningen och återanslöt **samma sekund**, utan att något gick förlorat. Iakttagelser: (1) mobilen laddar upp fotot igen vid varje beställning (12:28:08 och 12:28:12). Relän skickar det inte vidare till Quest en andra gång, men det är onödig trafik. (2) `waiting` skickas två gånger per order (svaret och statusändringen), vilket inte skadar. **Inte kontrollerat:** att fotot syntes på kunden i VR, att kassan ökade exakt en gång, 10 minuters stabilitet och spelet efter frånkoppling. |
| – | Förälderns acceptanstest [P] | **Inte gjort.** Se [PLAYTEST, del 4](PLAYTEST.md#del-4-förälder-som-kund-från-mobilen-epic-004). |
