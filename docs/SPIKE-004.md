---
title: SPIKE-004 – förälder som kund från mobilen
status: run-editor-and-device (H1/H2 delvis bekräftade på enheter 2026-11-01)
last_updated: 2026-11-01
---

# SPIKE-004 – förälder som kund från mobilen

Underlag för [EPIC-004](EPIC-004.md). Spiken ska visa om vägvalet håller innan funktionen byggs ut. Den skrivs före spiken, och resultaten förs in längst ned.

**Idé:** spelaren säljer i Quest. Föräldern öppnar en webbsida på mobilen, ansluter till samma spelpass och beställer som kund. Kunden i VR visar förälderns foto. Spelaren plockar och tar betalt med de vanliga kontrollerna.

## Inventering (2026-11-01)

| Vad | Finns | Betydelse |
|---|---|---|
| Nätverkspaket i Unity (Netcode, Mirror, NGO, WebSocket-bibliotek) | **inga** | Inget att återanvända, och inget behov av ett multiplayerpaket: bara meddelanden, ingen synkroniserad värld. |
| .NET-klassbiblioteket i Unity 6 (`System.Net.WebSockets.ClientWebSocket`, `System.Net.Sockets.UdpClient`) | ingår i runtime, inget paket | Prövas som Quest-klient. |
| `com.unity.modules.jsonserialize` (`JsonUtility`) | finns | Räcker för platta meddelanden. |
| `com.unity.modules.imageconversion` (`Texture2D.LoadImage` för JPEG/PNG) | **finns inte** | Undviks: mobilen skickar färdigskalade råa RGBA-pixlar, och Quest avkodar ingen bildfil. |
| Android-behörighet `INTERNET` | `ForceInternetPermission: 0` | Måste slås på i `QuestBuild`, annars kan appen inte öppna uttag. |
| På MacBooken | Node 26 (Homebrew), Python 3.9 (systemet) | Relän skrivs för Node utan npm-paket. |
| Spellogik | `Shop` (en aktiv beställning, `TryPay` exakt en gång), `Wallet` krediteras bara i `TryPay` | Mobilordern ska bli en vanlig `Order` i samma `Shop`. |

## Arkitektur som prövas

```
 Mobil (webbläsare)  ──WebSocket──►  MacBook: relä (Node, ingen npm)  ◄──WebSocket──  Quest (Unity-appen)
   önskemål: order, foto, "status?"     serverar mobilsidan, kontrollerar         auktoritativ: order, varor,
   visar status                        sessionskoden, förmedlar, cachar          betalning, kassa
                                       fotot i minnet, UDP-fyr på LAN
```

- **Quest är auktoritativ.** Mobilen skickar bara önskemål (`order`, `photo`, `sync`). Quest validerar, räknar ut summan från sina egna priser och svarar med status. Ingen meddelandetyp kan ändra kassa, orderstatus eller betalning.
- **Relän** på MacBooken serverar mobilsidan över HTTP, tar emot WebSocket från mobil och Quest, kontrollerar sessionskoden för mobilen, begränsar storlekar och förmedlar meddelanden. Ingen spellogik.
- **Hitta servern:** relän sänder en UDP-fyr (broadcast, port 47811) varje sekund med sin adress. Quest lyssnar och ansluter. Reserv för utvecklaren: en adress i `server.txt` i appens datakatalog (adb push).
- **Mobilen ansluter** via QR-kod (eller adressen) som relän visar i terminalen och på en värdsida på MacBooken. Adressen innehåller en kort sessionskod.
- **Foto:** mobilen beskär och skalar ned bilden till 128 × 128 och skickar råa RGBA-pixlar en gång (cirka 87 kB base64). Relän håller det i minnet under sessionen och skickar det till Quest vid behov. Quest visar det på en porträttskylt framför kundens ansikte.
- Inget internet, ingen molntjänst, inga konton och ingen sparning på disk.

## Hypoteser

| # | Hypotes | Hur den prövas | Godkänd när |
|---|---|---|---|
| H1 | Mobilwebbläsare och Quest kan ansluta stabilt till relän på samma wifi. | Relä + Quest + mobil i 10 min, med loggade anslutningar och återanslutningar. | Inga oväntade frånkopplingar under 10 min. Efter ett avbrott återansluter båda inom 5 s. |
| H2 | `ClientWebSocket` och UDP-fyren fungerar i en IL2CPP/ARM64-APK på Quest, inte bara i Editorn. | Spikbygget installeras på Quest (när det är tillåtet) och loggen läses. | Loggen visar ”relay connected” i APK:n, utan undantag från stripping eller behörigheter. |
| H3 | En mobilbeställning blir en vanlig kundorder i befintlig spellogik. | Automatiska tester av regeln och scenen, samt Editor-kedjan med riktig relä. | ”Ett äpple” från mobilen blir en kund vid fruktståndet. Försäljningen görs med vanliga händer, kassan ökar exakt 1 och mobilen får ”klart”. |
| H4 | Ett foto kan överföras och visas på kunden i VR. | Test med ett känt mönster av pixlar, Editor-kedjan och en renderad bild. | Rätt pixlar på kundens porträttskylt. Felaktig storlek avvisas. Bilddata finns inte i statusmeddelanden. |
| H5 | Avbrott och återanslutning ger inga dubbla order eller fel i kassan. | Automatiska tester (upprepade order-id, frånkoppling mitt i köp, Quest-omstart av anslutningen). | Samma order-id ger aldrig en andra kund. Kassan ökar exakt en gång. Mobilen får rätt status efter återanslutning. |

## Tidsbudget

En arbetsdag för spiken (relä, Quest-klient, minsta mobilsida, regel och tester), plus enhetstest när headset och mobil finns tillgängliga och ingen spelar.

## Alternativ (om ett antagande faller)

| Antagande som faller | Minsta alternativ |
|---|---|
| `ClientWebSocket` fungerar inte i IL2CPP-bygget | Rå TCP (`TcpClient`) med en JSON-rad per meddelande mellan Quest och relän. Mobilen behåller WebSocket. Relän får en extra TCP-port. |
| UDP-fyren når inte Quest (wifi filtrerar broadcast) | Quest provar adressen från förra lyckade anslutningen. Annars visar menyn en sifferkod (de sista två oktetterna i Mac-adressen) som föräldern väljer en gång. |
| Gästnät med klientisolering, eller nät där enheterna inte når varandra | Inget att lösa i appen. Använd hemnätet, eller MacBookens internetdelning eller telefonens hotspot med båda enheterna på den. Dokumenteras. |
| macOS brandvägg blockerar inkommande anslutningar | Tillåt ”node” i dialogen som macOS visar. Ingen automatisk ändring av brandväggen. |
| Rå RGBA är för stort | JPEG plus `com.unity.modules.imageconversion` (inbyggd modul, inget externt paket). |

## Minsta end-to-end-flöde

1. Starta relän på MacBooken. 2. Quest ansluter. 3. Mobilen öppnar sidan och ansluter till sessionen. 4. Välj ”ett äpple” och skicka. 5. En kund i VR visar beställningen. 6. Spelaren säljer med den befintliga betalningen. 7. Mobilen visar ”Tack, köpet är klart”. 8. Kassan ökar exakt en gång.

## Verifieringsnivåer

**[T]** automatiskt test (EditMode/PlayMode eller relätest i Node) · **[E]** Editor-kedja (Unity Editor + riktig relä på localhost) · **[B]** APK-bygge · **[D]** faktiskt enhetstest (Quest + mobil på samma wifi) · **[P]** förälderns acceptanstest.

## Resultat (2026-11-01)

**Sammanfattning:** vägvalet håller i allt som gick att pröva utan enheter. Hela kedjan relä → `ClientWebSocket` → spellogik → försäljning med händer → ”klart” till mobilen fungerar i Unity Editor. Det gäller både med fast adress och när Unity hittar relän via UDP-fyren, och när relän startas om mitt i en order. Inget grundantagande har fallit. **Spiken är inte provad på Quest eller mobil:** inget headset var anslutet, och appen ska inte installeras medan någon spelar. H1 och H2 är därför öppna, och de är spikens största risker.

| # | Resultat | Nivå | Belägg |
|---|---|---|---|
| H1 | **Öppen.** Relä och upptäckt fungerar på MacBooken: fyren når en lyssnare via LAN-adressen 192.168.1.10, och Unity hittar relän via fyren. Stabilitet över wifi mellan tre enheter är inte prövad. | [E] | `scripts/kund.sh spike beacon` → ”relay beacon from 192.168.1.10:8787”, ”link connected to ws://192.168.1.10:8787/ws” |
| H2 | **Delvis.** APK:n byggs med nätverkskoden. Behörigheten `android.permission.INTERNET` finns, och `ClientWebSocket`, `ManagedWebSocket`, `WebSocketHandle` och `UdpClient` finns kvar efter IL2CPP-strippingen (global-metadata). Att de fungerar på Quest är **inte** visat. | [B] | Bygge `2026-11-01_105633_b2a36f0-dirty`, `aapt2 dump permissions` |
| H3 | **Godkänd (Editor).** Mobilordern ”ett äpple” blir nästa kund vid fruktståndet, efter NPC-kunden som redan stod där. Den säljs med simulerade händer, korg och bricka, kassan ökar exakt 1 och mobilen får `waiting → serving → done`. | [T] [E] | `Scene_PhoneOrderSoldWithHandsOnce`, `RelayChain_EditorEndToEnd` |
| H4 | **Godkänd (Editor).** Ett 128 × 128-foto (råa RGBA-rader, 85 kB base64) kommer fram, avkodas en gång, rätt väg upp och ospeglat, och visas på kunden. Fel storlek, fel längd och fel base64 avvisas. Statusmeddelanden innehåller aldrig bilddata, och relän skickar fotot bara när Quest saknar det. | [T] [E] [R] | `Photo_*`, `Protocol_StatusNeverCarriesImageData`, `Relay_PhotoIsCachedAndSentToQuestOnlyWhenMissing`, renderad bild 15 |
| H5 | **Godkänd (Editor).** Upprepade order-id ger aldrig en andra kund. Kassan ökar en gång. Relän dödas mitt i en accepterad order: Quest behåller ordern, spelet fortsätter utan relä och Quest återansluter själv. Mobilen hämtar sedan status (`serving`) och köpet blir `done` exakt en gång. | [T] [E] | `Remote_SameIdNeverMakesASecondOrder`, `RelayChain_RelayRestartKeepsOrder` |

**Mätningar och iakttagelser**
- Ordervägen relä ↔ Editor på samma Mac tar under 1 s från order till `waiting`, och hela kedjetestet tar 0,8 s. Det säger inget om wifi-latensen.
- Fyren gick först ut innan servern lyssnade (port 0). Quest ignorerar port 0, men fyren startar nu först när porten är känd.
- Den första kunden bar hatt bakom porträttet. Hatten döljs nu när ett foto visas.
- Inga nya Unity-paket. Ingen npm. Relän är cirka 300 rader Node. QR-kodaren är kontrollerad mot macOS egen QR-läsare (CoreImage) i version 1, 3 och 6.

**Uppföljning på enheter (2026-11-01, se [EPIC-004:s logg](EPIC-004.md#verifieringslogg)):** Quest hittade relän via fyren och anslöt utan inmatning i APK:n. Två köp från en riktig mobil gick hela vägen till `done`, och en kort frånkoppling återanslöt samma sekund. **H2 är bekräftad. H1 är delvis bekräftad:** 10 minuters stabilitet är inte mätt.

**Beslut:** fortsätt med vägvalet (WebSocket via relä, UDP-fyr, råa pixlar) och bygg ut epicen. Före [P] krävs ett enhetstest [D] enligt [EPIC-004](EPIC-004.md#enhetstest-quest--mobil). Om H2 faller på Quest gäller alternativen ovan i den ordningen: först `link.xml` (behåll `System.Net.WebSockets`), sedan rå TCP.
