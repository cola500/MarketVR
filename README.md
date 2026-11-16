# MarketVR

Ett litet VR-spel för Meta Quest 3. Spelaren driver en mysig marknad en solig dag och säljer **frukt och grönsaker**, **godis** och **leksaker**. Kunderna visar sina beställningar med bilder, antal och summa. Hon plockar varorna i korgen och lägger kundens betalningsbricka i kassan, och allt hamnar i en gemensam kassa. Barn som köper leksaker går till lekbordet och leker, och spelaren kan leka med. Vid lekbordet står en krokodil med tänder att trycka ned. Efter 80 intjänade pengar blir det marknadsfest och sedan en vilopaus. En förälder kan handla som kund från mobilen.

Alla stånd och lekbordet står runt en liten fri arbetsyta. Spelaren kan stå och vända sig och ta korta steg, eller sitta och gå med spakarna. Marknaden kan riktas in mot rummets spelyta. Kassa, beställningar och lek sparas inte mellan appstarter.

## Dokumentation

| Dokument | Innehåll |
|---|---|
| [docs/SPEC.md](docs/SPEC.md) | Regler, interaktion, omfattning (§1–§25) |
| [EPIC-001](docs/EPIC-001.md) | Fruktståndet |
| [EPIC-002](docs/EPIC-002.md) | Plånbok och godisstånd (delvis ersatt av EPIC-003) |
| [EPIC-003](docs/EPIC-003.md) | Lilla marknaden: tre stånd, gemensam kassa, lekbordet, kompakt uppställning |
| [EPIC-004](docs/EPIC-004.md) | Förälder som kund från mobilen (med [SPIKE-004](docs/SPIKE-004.md)) |
| [EPIC-005](docs/EPIC-005.md) | Sittande spel: gång och vridning med spakarna |
| [EPIC-006](docs/EPIC-006.md) | Krokodilspelet vid lekbordet |
| [EPIC-007](docs/EPIC-007.md) | Rikta in marknaden mot rummets spelyta (med [SPIKE-007](docs/SPIKE-007.md)) |
| [EPIC-008](docs/EPIC-008.md) | Marknadsfest och vilopaus (också från mobilen) |
| [docs/PLAYTEST.md](docs/PLAYTEST.md) | Manus för speltester och vilket bygge som testades |

Varje epic har stories, acceptanskriterier med testnamn och en verifieringslogg. I loggarna hålls automatiska tester, bygge, headsettest (utvecklaren/föräldern) och barnets speltest isär.

## Teknik

Unity **6000.4.7f1**, inbyggd renderingspipeline och gamla Input Manager. Paket:
- `com.unity.xr.management` 4.5.0 och `com.unity.xr.oculus` 4.5.0;
- test-framework;
- modulerna androidjni, audio, imgui, jsonserialize och physics (bara för leksakerna och lekbordet).

Inget XR Interaction Toolkit, inget Input System, inget Meta SDK och inga Asset Store-paket. Allt grafiskt byggs av Unitys primitiver och några procedurella former, och ljuden syntetiseras i koden. Relän för mobilen är Node 22+ utan npm-paket.

## Öppna och köra (desktop)

1. Unity Hub → **Add** → **Add project from disk** → välj `UnityProject/`. Första öppningen bygger `Library/` och tar en stund.
2. Öppna `Assets/Scenes/FruitStand.unity` och tryck **Play**. Klicka i Game-fönstret så att det får fokus.

Saknas scenen: menyn **Fruktstånd → Bygg om scenen** (eller `scripts/quest.sh scene`).

Desktopläget är till för att snabbt kontrollera spellogiken, inte för att spela:

| Tangent | Handling |
|---|---|
| 1–9 | Lägg vara 1–9 i den aktiva korgen (1–3 frukt/grönt, 4–6 godis, 7–9 boll, bil, klossar) |
| Backsteg | Ta ur senaste varan |
| Mellanslag | Lägg betalningsbrickan i kassan |
| Tab | Byt kameravy (översikt, frukt, godis, leksaker, lekbordet; bara desktop) |
| O | Stäng / öppna marknaden |
| C | Återställ beställningen |
| P | Städa lekytan |
| R, sedan R igen inom 2 s | Nytt spel (tömmer kassan) |
| + / − (eller PgUp / PgDn) | Höj / sänk diskarna och lekbordet |

## Bygga, testa och installera på Quest 3

Application id: `com.lillafruktstandet.prototype`. Appnamn: MarketVR. **En gång per headset:** slå på Developer Mode i Meta Quest-appen, koppla in USB-C och välj *Allow USB debugging* i headsetet.

**Parkmodellerna (EPIC-013), en gång per klon:** lägg till det kostnadsfria paketet [Low Poly Environment Park](https://assetstore.unity.com/packages/3d/environments/low-poly-environment-park-242702) i *My Assets* och välj *Download* (inte *Import*) i Package Manager. Kör sedan `scripts/park-assets.sh` och `scripts/quest.sh scene`. Bara 13 filer packas upp, och de committas inte (Asset Store EULA). Utan dem saknar parken träd, bänk och stenar, men spelet fungerar.

Från repots rot (Unity-editorn måste vara stängd för `build`, `scene` och `test`):

```sh
scripts/quest.sh test      # EditMode + PlayMode med progress; en körning som hänger sig stoppas (docs/TESTING.md)
scripts/quest.sh build     # bygg APK och arkivera den med bygg-id och manifest
scripts/quest.sh builds    # lista arkiverade byggen
scripts/quest.sh install <bygg-id>   # adb install -r av ett arkiverat bygge (avinstallerar aldrig), kontrollerar
                                     # manifestet, vägrar om appen är i förgrunden och verifierar bygg-id:t efteråt
scripts/quest.sh installed # bygg-id för appen på headsetet (läser bara)
scripts/quest.sh devices   # anslutna headset: USB eller wifi
scripts/quest.sh wifi      # slå på trådlös ADB (headsetet i USB en gång)
scripts/quest.sh connect   # återanslut över wifi
scripts/quest.sh launch    # starta appen
scripts/quest.sh log       # följ Unity-loggen från headsetet ([Fruktstand] ...)
scripts/quest.sh scene     # generera om scenen
```

**Installera eller starta inte om appen medan någon spelar.** `install` vägrar om appen är i förgrunden.

### Trådlös installation (ADB över wifi)

Metoden är Androids och Metas egen: `adb tcpip` över USB och sedan `adb connect` över wifi. Macen och headsetet ska vara på samma wifi; gästnät blockerar ofta enheter från varandra.

1. **Första gången och efter varje omstart av headsetet:** koppla in USB-C, godkänn *Allow USB debugging* och kör `scripts/quest.sh wifi`. Skriptet läser headsetets wifi-adress, slår på trådlös ADB, ansluter och sparar adressen i `scripts/.quest-wifi` (lokal, inte i git). Sedan kan kabeln kopplas ur.
2. **Senare (Macen eller headsetet har sovit, wifi har bytts):** `scripts/quest.sh connect`. Svarar headsetet inte: väck det (sätt på det eller tryck kort på strömknappen) och försök igen.
3. **Efter en omstart av headsetet** är trådlös ADB avstängd. Gör om steg 1. Har headsetet fått en ny adress löser `wifi` det.
4. **Installera:** `scripts/quest.sh builds`, sedan `scripts/quest.sh install <bygg-id>`. Bygg-id:t verifieras på headsetet efteråt.

- **Samma headset via USB och wifi:** skriptet använder USB och säger det. Alla adb-anrop har `-s <enhet>`, så det blir aldrig "more than one device". Flera headset: `QUEST_DEVICE=<serienummer eller ip:5555> scripts/quest.sh …`.
- **macOS lokalt nätverk:** svarar headsetet men `adb` får "No route to host" har macOS blockerat `adb` (Sekretess och säkerhet → Lokalt nätverk). Slå på det för appen som kör terminalen (Terminal, iTerm, VS Code, Claude …), kör `adb kill-server` och försök igen. Skriptet känner igen fallet och säger det.
- Avsluta: `scripts/quest.sh disconnect`. Skriptets tester: `scripts/test/quest-install.test.sh` (en låtsas-adb, når aldrig ett riktigt headset).

**Spårbara byggen:**
- Varje lyckat bygge får ett bygg-id som `2026-11-01_143052_abc1234`: svensk tid och git-commit, och `-dirty` om det byggdes med ocommittade ändringar.
- Bygget arkiveras som `UnityProject/Builds/archive/lilla-fruktstandet_<bygg-id>.apk` med ett manifest (`.json`) bredvid. Dirty-byggen får dessutom en källkodsbild (`.source.tar.gz`).
- Arkivet skrivs aldrig över och rensas aldrig automatiskt.
- Bygg-id:t står sist i appens meny och i versionName (`1.0+<bygg-id>`). versionCode är alltid 1, så varje bygge installeras som en uppdatering och ett äldre bygge kan installeras tillbaka.

I headsetet startas appen under **Appar → Okända källor** → **MarketVR**. Om en systemruta (Guardian) ligger överst efter installationen: sätt på headsetet och stäng den.

### Kontroller i VR

| | |
|---|---|
| Handen nära en vara eller leksak | Den växer lite och handen blir gul: den går att ta |
| Grepp- eller avtryckarknapp | Ta. Håll inne för att bära. Båda händerna fungerar |
| Släpp knappen ovanför korgen | Varan hamnar i korgen. Fel vara, eller en vara från ett annat stånd, går tillbaka till sin låda |
| Betalningsbrickan i kassan | Köpet registreras och kassan ökar med summan (pling, ”Tack!”) |
| Leksak på lekbordet | Ta, rulla, skjut och stapla. Barnet väntar medan du håller leksaken. Släpp i fart så rullar den |
| Krokodilen vid lekbordet | Tryck ned tänderna (grepp/avtryckare intill en tand). En av dem får munnen att slå igen |
| Städa-knappen vid lekbordet | Alla leksaker som inte hålls läggs tillbaka på sina platser (gratis) |
| Dra handen genom skylten Öppet/Stängt | Pausa nya kunder (en kund som redan är där stannar) |
| Vänd dig och ta ett kort steg | Frukt & grönt framför dig, godis till höger, leksaker till vänster, lekbordet bakom |
| Vänster tumspak | Gå framåt/bakåt och i sidled, åt det håll du tittar (0,5 m/s). Du stannar vid diskarna och lekbordet |
| Höger tumspak vänster / höger | Vänd dig 30° per utslag. Släpp spaken till mitten före nästa |
| Höger tumspak upp / ned | Höj / sänk diskarna och lekbordet (50–120 cm). Du själv flyttas inte |
| Menyknappen (☰) | Spelmenyn, se nedan. Peka med strålen och tryck på avtryckaren, eller spak upp/ned och A. B går tillbaka |
| Håll in Meta-knappen | Systemmenyn och omcentrering (marknaden placeras med arbetsytans mitt där du står). Appen läser aldrig Meta-knappen själv |

**Spelmenyn:**
- **Fortsätt** och **Visa kontroller**. Under barnets vilopaus är första valet **Fortsätt spela** (EPIC-012).
- **Stäng/Öppna marknaden**, **Återställ beställningen** och **Städa lekytan**.
- **Flytta marknaden hit** placerar marknaden där du står.
- **Rikta in marknaden:** förhandsvisning mot rummets spelyta. Spak vänster/höger ändrar mått och vridning, och Bekräfta flyttar marknaden.
- **Ta vilopaus.**
- **För föräldrar:** omgångens siffror, **Pausa spelet** / **Fortsätt spelet** (en föräldrapaus kan bara föräldern avsluta) och **Starta nästa omgång**, som kräver Ja.
- **Nytt spel** kräver Ja och tömmer kassan och stjärnorna.

## Rikta in marknaden (EPIC-007)

Quest 3 ger bara spelytans mått, inte var den ligger. Menyn → **Rikta in marknaden** visar ett förslag på golvet: grönt om det ryms, rött om inte. Jämför med systemgränsen och justera bredd, längd, mitt och vridning. Marknaden flyttas först när du bekräftar, och bara om det ryms. Avståndet mellan stånden anpassas (0,70–0,90 m från mitten), men ingenting skalas. Spakgång, att ta av headsetet och ändrad systemgräns flyttar aldrig marknaden. Spelets golvmarkering syns bara under inriktningen och lovar inte att golvet är fritt.

## Marknadsfest och vilopaus (EPIC-008)

Efter **80** intjänade pengar i en omgång blir det en kort marknadsfest och sedan en vilopaus. Under pausen står ”Pausdags! Ta av headsetet och vila en stund.” framför spelaren. En vanlig tryckning avslutar inte pausen. Föräldern startar nästa omgång i menyn (För föräldrar → Starta nästa omgång → Ja) eller från mobilsidans del Förälder. Ta vilopaus i menyn eller Pausa spelet på mobilen ger samma paus när som helst, utan stjärna.

För headsettest med lägre tröskel: `echo 5 > fest.txt && adb push fest.txt /sdcard/Android/data/com.lillafruktstandet.prototype/files/`. **Ta bort filen före barnets speltest** (`adb shell rm /sdcard/Android/data/com.lillafruktstandet.prototype/files/fest.txt`). Appen loggar tröskeln vid start och tiden till tröskeln (`scripts/quest.sh log | grep -E "threshold|round"`).

## Kund och förälder från mobilen (EPIC-004, EPIC-008)

Föräldern beställer som kund från mobilen, och kunden i VR visar förälderns foto. MacBook, Quest och mobil ska vara på **samma wifi**.

```sh
scripts/kund.sh start   # relän: QR-kod, adress och sessionskod i terminalen, värdsidan öppnas i webbläsaren
scripts/kund.sh test    # reläns tester (node --test)
scripts/kund.sh spike   # Editor-kedjan: relä + Unity-test (beacon: hitta relän via fyren som Quest gör; PORT=8788 om 8787 är upptagen)
```

1. Starta relän på MacBooken. Värdsidan (`http://localhost:8787/host`) visar QR-koden och om Quest är ansluten.
2. Starta spelet i Quest. Det hittar relän själv via en UDP-fyr på nätverket.
3. Skanna QR-koden med mobilen, eller skriv adressen och den fyrsiffriga sessionskoden. Välj foto och vara, och tryck Beställ.
4. Delen **Förälder** längst ned på mobilsidan har **Pausa spelet** och **Starta nästa omgång**, båda med bekräftelse. ”Spelet är pausat” visas först när Quest bekräftat. Det är en praktisk föräldrakontroll inom den lokala sessionen, ingen säker barnspärr.

Inget internet behövs, och ingenting sparas. Varje start av relän ger en ny sessionskod.

**Vanliga hinder**
- **Gästnät eller nät med klientisolering:** enheterna når inte varandra. Använd hemnätet. Är det inte möjligt kan alla tre enheter anslutas till mobilens hotspot eller MacBookens internetdelning.
- **macOS brandvägg:** första gången kan macOS fråga om ”node” får ta emot anslutningar. Svara Tillåt. Skripten ändrar aldrig brandväggen själva. Har du nekat: Systeminställningar → Nätverk → Brandvägg → Alternativ.
- **VPN på datorn eller mobilen** kan skicka trafiken fel väg. Stäng av det under spelet.
- **Quest hittar inte relän** (”relay …” saknas i `scripts/quest.sh log`): kontrollera att Quest är på samma wifi. Utvecklarreserv: `echo ws://<mac-ip>:8787/ws > relay.txt && adb push relay.txt /sdcard/Android/data/com.lillafruktstandet.prototype/files/`.
- **Mobilen visar Frånkopplad:** kontrollera att adressen i QR-koden är datorns wifi-adress (terminalen listar alla).

## Struktur

| Mapp | Innehåll |
|---|---|
| `Assets/Fruktstand/Core` | Regler utan XR, fysik eller rendering: `Ware`, `Order`, `OrderGenerator`, `Basket`, `Shop` (en aktiv beställning), `Wallet` (kassan), `PlayArea`, `StackHelp`, `DropRules`, `Market`, `RemoteCustomer` (mobilkunden), `CrocodileGame`, `MarketRound` (omgång, fest och paus) |
| `Assets/Fruktstand/Game` | `FruitStand` (samordnar allt), `MarketLayout`, `MarketCollision` (golvavtryck för spakgång), `SpaceFit`/`Calibration`/`LayoutMarking` (inriktning), `StandView`, `PlayTableView`, `CrocodileView`, `PartyView`, `CustomerView`/`CustomerFigure`, `OrderBubble`, `Grabber`, `TouchSwitch`, `Item`, `Models`, `Art`, `Sounds`, `BuildInfo`, `DesktopInput`, `DesktopHud` |
| `Assets/Fruktstand/Game/Xr` | `XrRig` (enheter, haptik, kalibreringsflödet), `XrInputRouter` (fokus, meny, spakar – ren logik), `Locomotion` (gång och vridning), `StandPlacement`, `BoundaryProbe` (läser spelytan), `GameMenu`, `MenuPanel` |
| `Assets/Fruktstand/Game/Remote` | Mobilen: `RemoteSession` (protokollet, utan nätverk), `RelayLink` (fyr, WebSocket, återanslutning), `PhotoStore`, `Portrait` |
| `Assets/Fruktstand/Models` | Alla varors modeller exporterade från Blender (`<Ware>.fbx`, `Ring.fbx`, `RingPeg.fbx`); källor i `art/`, se `docs/SPIKE-BLENDER.md` |
| `Assets/Fruktstand/Editor` | `SceneSetup` (genererar scenen), `QuestBuild` (Quest-inställningar, APK, bygg-id, `preloadedAssets`-vakt) |
| `Assets/Fruktstand/Tests` | EditMode: regler, protokoll, grepp, router, meny, layout och inriktning. PlayMode: den riktiga scenen styrd med simulerade händer. `Snapshots` och reläkedjorna är explicita |
| `art/` | Blender-källor: `kit.py` (gemensamma byggstenar), `fruit/`, `candy/`, `toys/` med var sitt `make_*.py` (genererar `.blend`, en FBX per vara och `contact_sheet.png`) |
| `relay/` | Relän för MacBooken: `server.mjs`, `ws.mjs` (WebSocket), `qr.mjs` (QR-kod), `public/phone.html` (mobilsidan), `test/` |
| `scripts/` | `quest.sh` (test, bygge, installation) och `kund.sh` (relän) |

## Kontrollbilder

```sh
scripts/quest.sh snapshots   # vaktad körning; PNG i UnityProject/Logs/shots/ (se docs/TESTING.md)
```

Kräver grafik (inte `-nographics`). Bilderna 01–23 täcker fruktståndet, marknaden, mobilkunden med foto, skylten, krokodilen, inriktningen, festen och pausen. 24–27 visar de nya frukterna (EPIC-009) i stånd och bubblor, 28–32 godis och leksaker (EPIC-010) och lekbordet med de nya leksakerna.

## Headsettest (utvecklaren och föräldern)

Varje epic har ett manuellt protokoll:
- marknaden: [EPIC-003](docs/EPIC-003.md#manuellt-headsettest-utvecklaren-tillsammans-med-föräldern-för-räckvidd);
- mobilen: [EPIC-004](docs/EPIC-004.md#enhetstest-quest--mobil);
- sittande spel: [EPIC-005](docs/EPIC-005.md#manuellt-headsettest-sittande-utvecklaren-och-sedan-spelaren);
- krokodilen: [EPIC-006](docs/EPIC-006.md#manuellt-acceptanstest);
- inriktningen: [EPIC-007](docs/EPIC-007.md#manuellt-test-på-quest);
- festen och pausen: [EPIC-008](docs/EPIC-008.md) och [PLAYTEST del 7](docs/PLAYTEST.md).

För in resultatet i epicens verifieringslogg. Barnets reaktioner förs in först när föräldern har rapporterat dem.

## Återanvänt

Från **Magic Tiles** (kopierat och anpassat, originalet orört): ProjectSettings och XR-inställningar (nytt product GUID och id), `QuestBuild` med `preloadedAssets`-vakten, `scripts/quest.sh`, `InputDevices`-läsning och fokus-/paus-/närvarohantering (`XrRig`, `XrInputRouter`), menyns tillståndsmaskin (`GameMenu`, `MenuPanel`), det höjdbara bordet (nu disk) och den soliga miljön.

Från **CampfireVR**: bara mönster via Magic Tiles (Oculus-loader, `InputDevices`, golvorigo). Inga paket, assets eller nycklar.

Inte kopierat: `Library/`, `Builds/`, `Logs/`, `UserSettings/`, XR Interaction Toolkit, röst, Asset Store-innehåll, keystores. Nätverket för mobilen är skrivet för det här projektet.
