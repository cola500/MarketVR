---
title: EPIC-012 – paus som går att avsluta, pekbara menyer, läsbara instruktioner
status: installed-not-tried
last_updated: 2026-11-01
---

# EPIC-012 – paus, pekbara menyer och läsbara instruktioner

Regler: [SPEC §29](SPEC.md#29-paus-menyer-och-instruktioner-epic-012). Inga nya spelfunktioner.

## Rapporterat problem och grundorsak

"Spelaren kunde inte lämna pausläget och fortsätta, utan behövde börja om spelomgången."

**Grundorsak:** menyns *Ta vilopaus* (EPIC-008) gick till **samma** tillstånd som pausen efter marknadsfesten (`RoundState.Resting`). Det tillståndet avslutades bara av föräldrarnas *Starta nästa omgång*, som nollställer omgångens intäkt och byter sortiment, eller av *Nytt spel*. Menyns *Fortsätt* stängde bara menyn, och mobilen kunde bara pausa eller starta nästa omgång. Två olika pauser blandades ihop: en vanlig paus som ska kunna avslutas och slutet på en omgång.

## Pauslägen

| Läge | Startas av | Avslutas av | Bevaras | Byter sortiment |
|---|---|---|---|---|
| **Spelmeny öppen** | menyknappen | *Fortsätt* i menyn (eller menyknappen) | allt | nej |
| **Vilopaus (barnet)** | menyn: *Ta vilopaus* | menyn: *Fortsätt spela* (eller föräldern) | order, kassa, omgångens intäkt, lek, stjärnor | nej |
| **Föräldrapaus** | föräldern: mobilen *Pausa spelet* eller VR *För föräldrar → Pausa spelet* | **bara** föräldern: *Fortsätt spelet* (mobil eller VR-föräldrasidan) | som ovan | nej |
| **Paus efter fest** | omgångens slut (80 pengar) | föräldern: *Starta nästa omgång* (bekräftelse) | kassa, stjärnor, pågående lek | ja, mellan kunder (EPIC-009/010) |
| **Systemets paus** | Meta-knappen, headset av, fokus borta | systemet | allt; nytt tryck krävs efteråt | nej |
| **Nytt spel** | menyn, med bekräftelse | – | inget (det enda som nollställer spelpasset) | ja |

- En föräldrapaus kan inte avslutas av barnet. Startar föräldern en paus medan barnet vilar blir det en föräldrapaus.
- Fortsätt gör aldrig något utan XR-inputfokus, eftersom ingen inmatning läses då (oförändrat).
- Pausvyn säger varför det är pausat och vad som fortsätter spelet.

## Pekbara menyer

- När menyn är öppen visas en stråle från varje spårad kontroll. Raden som strålen träffar markeras. **Avtryckaren** på samma hand aktiverar raden, med en kort blink och en lätt vibration.
- Stort mellanrum mellan raderna. Raderna är egna knappar med en bakgrund.
- Spak och A/X väljer som förut. B/Y går tillbaka. Avtryckaren när strålen **inte** träffar panelen väljer markerad rad, som förut. Träffar strålen panelen men inte en knapp händer ingenting.
- Ett klick ger exakt en aktivering, även med två pekare.
- **Inputskydd:** med menyn öppen greppas ingenting, ingen rörelse sker och höjden ändras inte. Stängs menyn med ett klick krävs släppta knappar och neutrala spakar innan spelet tar emot inmatning igen (oförändrat). Trycket som öppnar menyn väljer inget. Utan fokus finns ingen meny-interaktion. Meta-knappen läses aldrig.

## Instruktioner och layout

- Texten radbryts så att den ryms i panelens bredd. Panelens höjd följer innehållet. Kontrollhjälpen delas i sidor i stället för att krympas.
- Kontrollhjälpen motsvarar dagens knappar, inklusive pekaren.
- Bekräftelser: *Nej* är förvalt. Återställande val (*Nytt spel*) är röda, och *Fortsätt*-val har en egen färg.
- Menyn placeras när den öppnas och står sedan still (oförändrat), på sittande läshöjd.

## Acceptanskriterier

| # | Kriterium | Verifiering |
|---|---|---|
| A1 | Regression: vilopaus från menyn → *Fortsätt spela* → samma order, kassa, intäkt, lek och sortiment. | [T: Pause_ChildRestResumesSameGame] |
| A2 | Föräldrapaus kan bara avslutas av föräldern (mobil och VR-föräldrasidan); barnets *Fortsätt* gör inget då. | [T: Pause_ParentRestNeedsParent] [T: Session_ParentResume] |
| A3 | Paus efter fest: bara *Starta nästa omgång*. Kassa, stjärnor och lek bevaras, och sortimentet byts. | [T: Pause_PartyRestOnlyNextRound] |
| A4 | Vanlig paus byter aldrig sortiment. | [T: Pause_ChildRestResumesSameGame] [T: Pause_ParentRestNeedsParent] |
| A5 | Menyn visar rätt val och skäl för varje pausläge; *Ta vilopaus* syns inte under vila. | [T: Menu_RestItemsFollowTheCause] |
| A6 | Ett pekarklick aktiverar exakt den rad strålen träffar, en gång, även med två pekare. | [T: Router_PointerClickActivatesOnce] |
| A7 | Klick i menyn greppar inte, flyttar inte och ändrar inte höjden; inmatning kräver neutralt läge efter stängning; trycket som öppnar väljer inget. | [T: Router_PointerClickNeverReachesTheWorld] + befintliga `Router_*` |
| A8 | Utan fokus: ingen pekare och inget klick; efter fokus krävs ett nytt tryck. | [T: Router_PointerNeedsFocusAndNewPress] |
| A9 | Bekräftelser fungerar med pekare och med spak + A. | [T: Router_ConfirmWithPointerOrButtons] |
| A10 | Varje menysida (längsta texter) ryms i panelen, utan överlapp eller klippning. | [T: Scene_MenuPagesFitTheirPanel] [R: Snapshots] |
| A11 | Kontrollhjälpen nämner pekaren, avtryckaren, A, B, menyknappen och spakarna som de faktiskt fungerar. | [T: Menu_ControlsShowStickMapping] (uppdaterad) |
| A12 | Headset: peka och klicka bekvämt sittande; texten läsbar; föräldrapaus från mobilen och återupptagning; inga menytryck påverkar spelet. | [H] |

## Verifieringslogg

**Röda före implementation (2026-11-01):**
- `PauseTests` (5 tester), mot skelett. Regressionstestet `Pause_ChildRestResumesSameGame` föll på att vilopausen inte hade något eget skäl och inte kunde avslutas.
- `PointerTests` (4).
- Reläns `Relay_ParentResumeAndRestBy`.

**Skrivna samtidigt med koden** (inte sedda röda först, en avvikelse):
- `MenuLayoutTests` och `Scene_MenuPagesFitTheirPanel`. Det senare hittade ett tomt textobjekt på kalibreringssidan.
- `PauseSceneTests`.

**Slutläge:** EditMode 189/189, PlayMode 88 godkända + 6 explicita, relä 14/14. Alla körningar gick via `quest.sh`, utan hängning. De befintliga meny- och routertesterna gick igenom oförändrade, eftersom de hittar raderna via etiketterna.

**Unity, visuellt:** `scripts/quest.sh snapshots` (`Snapshots_MenuPanels`), bild 40–52: alla sidor i alla pauslägen med de längsta texterna, plus en stråle på *Ta vilopaus*. Bilderna visade att kundens pratbubbla kunde **täcka menyn**. Menyn ritas nu ovanpå världen (shadern `Fruktstand/MenuOverlay`, säker för stereo-rendering på Quest).

**Småfixar (Boy Scout):**
- Menyn ritas ovanpå världen.
- *Starta nästa omgång (bara i vilopaus)* är gråtonad.
- Kontrollhjälpen nämner menyknappen och pekaren, och den långa raden om gång är kortare.
- Fotraden förklarar pekaren i stället för en ensam spakrad.
- Pekarna släpps när menyn stängs med menyknappen.
- Panelen hoppar över tomma rader.

**Större fynd (dokumenteras, inte åtgärdade):**
- Strålens riktning är kontrollerns grepp-pose (Oculus-pluginen), utan Quests "aim"-pose. Om strålen pekar för lågt eller högt i headsetet: justera `XrRig.PointerPitch`.
- Bildsymboler i menyn finns inte. Typsnittet saknar säkra symboler; det skulle kräva bilder.
- Mobilens föräldradel har inte prövats i webbläsare [E].

**Bygge:** `2026-11-01_180221_42924a2`, rent, 24 MB, sha256 `7970fff6…`. **Inte installerat.**

**Installerat 2026-11-01 18:09:07, oavsiktligt:** ett nytt skripttest körde det gamla `quest.sh` mot den riktiga `adb` när headsetet just hade anslutits via USB, och installerade då `2026-11-01_180221_42924a2`. Appen var inte i förgrunden och startades inte. Tidigare installerat: `2026-11-01_132248_ba7f845`. Testet har nu en spärr: det vägrar köra om skriptet inte använder låtsas-adb.

**Trådlös installation [D], 2026-11-01:** `scripts/quest.sh wifi` med headsetet i USB gav 192.168.1.20:5555. Därefter installerades `2026-11-01_180221_42924a2` igen med `QUEST_DEVICE=192.168.1.20:5555 scripts/quest.sh install …` (`adb -s 192.168.1.20:5555 install -r`, Success), och versionName `1.0+2026-11-01_180221_42924a2` verifierades över wifi. Appen startades inte. **Inte prövat:** `connect` efter att kabeln dragits ur eller Macen/headsetet har sovit.

**Headset [H]:** inte gjort. Se A12 och PLAYTEST del 11: strålens riktning, läsbarhet sittande, föräldrapaus från mobilen och att menytryck inte påverkar spelet.
