# Testkörning – regler och lärdomar

## Så körs testerna

| Kommando | Vad |
|---|---|
| `scripts/quest.sh test` | EditMode + PlayMode (batchmode, `-nographics`) |
| `TEST_FILTER=<regex> scripts/quest.sh test` | ett urval |
| `scripts/quest.sh snapshots` | de explicita `Snapshots` (kräver grafik); PNG i `UnityProject/Logs/shots/` |
| `scripts/kund.sh test` | reläns tester (Node) |

Varje Unity-körning **vaktas**:
- Testassemblyerna skriver en rad `[test-progress]` när varje test startar och slutar (`Tests/*/TestProgress.cs`). Skriptet visar framstegen.
- Om inget test blir klart på `TEST_STALL` sekunder (120), eller om hela körningen tar mer än `TEST_TIMEOUT` (600), stoppas Unity.
- Skriptet säger då vilket test som körde och sparar ett stackprov av alla trådar (`UnityProject/Logs/<namn>-hang.txt`, macOS `sample`).

**Regler:**
1. Kör aldrig Unity-tester direkt mot Unity-binären. Gå alltid via `quest.sh` (`test` eller `snapshots`), så att vakten och stackprovet finns med.
2. PlayMode-tester laddar scenen med `SceneLoad.Fresh(...)` (synkront), **aldrig** med `SceneManager.LoadSceneAsync`.
3. En hängning felsöks från stackprovet, inte från gissningar. Gör om försöket minst två gånger innan en orsak skrivs ned.
4. Ett `while` som väntar i ett test har alltid en övre gräns (antal steg eller tid).

## 5 varför: testkörningar som hängde sig (2026-11-01, EPIC-009)

**Problem:** under EPIC-009 hängde Unity-testkörningen fyra gånger. Ingen progress syntes, och det tog lång tid att märka det och hitta orsaken.

1. **Varför hängde körningen?**
   Unity-editorn fastnade i ett dödläge mellan två trådar:
   - Huvudtråden förstörde en `MeshRenderer` (uppskjuten `Destroy`). I editorn avregistreras renderaren då från `AssetChangedNotifier`, som väntar på `PersistentManager`-låset.
   - Scenladdningstråden (`PreloadManager` → `LoadSceneOperation`) höll det låset och väntade på objektlåset, som huvudtråden höll.

   Belägg: stackprov `UnityProject/Logs/hang2.txt`.
2. **Varför förstördes renderare samtidigt som en scen laddades?**
   Varje PlayMode-test laddade om scenen med `LoadSceneAsync`. Samtidigt kördes det förra testets uppskjutna `Destroy`, alltså `Art.Discard` av vyer som byggts om. Hängningen kom därför precis vid omladdningen, efter ett test som byggt om vyer.
3. **Varför först nu?**
   - Sedan Blender-spiken (`e8082b2`) pekar renderarna på **importerade FBX-tillgångar**, inte bara på inbyggda primitiver. Bara renderare med importerade tillgångar tar vägen via `AssetChangedNotifier`/`PersistentManager`.
   - Med EPIC-009 kom fler vyombyggnader (sortimentsbyte, kontroll av modeller) och fler scenladdningar.
   - Det är ett race, så det drabbade olika test i olika körningar.
4. **Varför tog det så lång tid att se och förstå?**
   - Körningen hade bara en total gräns (600 s), och loggen visade inte vilket test som körde.
   - Vakten i `quest.sh` dog själv, eftersom `set -e`/`pipefail` slog till på en `grep` utan träff. Då blev Unity föräldralöst.
   - Snapshots kördes direkt mot Unity, utan vakt.
   - Första hypotesen (`[SetUpFixture]` + `[Timeout]`) byggde på **ett** försök och skrevs ned som fakta. Den var fel.
5. **Varför fanns inget skydd?**
   Det fanns ingen regel för hur tester laddar scener, ingen progress, inget automatiskt stackprov vid hängning, och ingen gemensam väg för alla Unity-körningar.

**Åtgärder:**

| Åtgärd | Var |
|---|---|
| Synkron scenladdning i alla PlayMode-tester (ingen laddtråd som kan låsa sig mot `Destroy`) | `Tests/PlayMode/SceneLoad.cs` |
| Progress per test, stoppvakt (`TEST_STALL`), namn på det hängande testet, automatiskt stackprov | `Tests/*/TestProgress.cs`, `scripts/quest.sh` |
| `quest.sh snapshots` med samma vakt; ingen direktkörning | `scripts/quest.sh` |
| Vaktens egna fel: `\|\| true` på alla `grep` i loopen; bash 3.2-säker tom array | `scripts/quest.sh` |
| Den felaktiga anteckningen om `SetUpFixture` rättad | `CLAUDE.md` |
| Obegränsat `while` i `Snapshots` begränsat | `Tests/PlayMode/Snapshots.cs` |

**Verifiering:** reprofallet (`Snapshots_NewFruit`, som hängde sig vid scenbyte nummer 2) gick igenom med synkron laddning. Hela sviten kördes sedan tre gånger i rad utan hängning (se EPIC-009, verifieringsloggen).

**Gäller inte Quest:** `AssetChangedNotifier` finns bara i editorn, och spelet laddar bara en scen en gång. Dödläget är en testmiljöfråga.
