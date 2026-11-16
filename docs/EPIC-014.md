---
title: EPIC-014 – parkens atmosfär och vänligare kunder
status: slice-1-in-progress
last_updated: 2026-11-02
---

# EPIC-014 – parkens atmosfär och vänligare kunder

Regler: [SPEC §31](SPEC.md#31-parkens-atmosfär-och-naturljud-epic-014). Bygger på EPIC-013 (parken). Inga nya spelmekaniker. Spelregler, kontroller och kundflöden är oförändrade.

**Problem (föräldern, 2026-11-02):** omgivningen upplevs som en stor öppen savann med en liten parkbit i mitten.

Två slicar som provas var för sig. Slice 2 påbörjas först när slice 1 är verifierad visuellt och på headset.

## Slice 1 – parkkänsla och ljud

### Miljö

- **Bakgrundsgrupper:** trädklungor (3–5 träd) och runda buskar i en bruten ring 6–20 m från mitten. Det ger djup i tre lager: parkens fem träd nära (3–4 m), klungor i mellanavstånd och tätare klungor längre bort.
- **Horisonten:** mjuka, låga gröna kullar 45–70 m bort bryter den tomma linjen. Gräsmattan är större (ingen kant syns) och har parkträdens gröna ton.
- **Öppningar:** en siktlinje längs gångarmen framåt och en bakåt hålls fri (ingen dekor närmare än 1,5 m från gångens mittlinje). Mellan klungorna finns luckor.
- **Inget utforskbart:** inga nya stigar och inget att gå till. Spakgång stoppas vid stammarna som vid parkens träd.
- **Fritt:** samma krav som EPIC-013, alltså arbetsyta, kalibrering, rörelse och kundernas och barnens vägar. Inget nära de platser där spelaren behöver vara.
- **Quest:** samma modeller och material. Allt slås ihop till en mesh, utan skuggkastning och utan transparens.

### Ljud

- ~~**Bas:** ett svagt, jämnt vind- och lövprassel.~~ **Borttaget 2026-11-02** efter headsettestet: föräldern tyckte att ”vinden var direkt jobbig, den kan vi ta bort”. Ingen ljudbädd som loopar.
- **Fåglar:** sporadiska kvitter från några av träden (3D-ljud), med 7–18 s mellan dem och aldrig samma träd eller samma klipp två gånger i rad. Flera kvittervarianter, mjuka ansatser och inga skarpa toppar.
- **Balans:** miljöljudet ligger tydligt under betalnings-, korg- och övriga återkopplingsljud.
- **Inställning:** *För föräldrar → Naturljud: normalt / svagt / av*. Den sparas mellan starterna.
- **Paus och fokus:** som allt annat ljud tystnar miljöljudet bakom systemmenyn, utan fokus, med headsetet av och med menyn öppen (`AudioListener.pause`). Under paus kommer inga fåglar, och de kommer inte i klump efteråt. Det finns exakt en vindkälla, även efter återupptagning.
- **Ingen bakgrundsmusik.**

### Ljudkällor

| Ljud | Källa | Licens |
|---|---|---|
| Fågelkvitter | syntetiseras i koden (`SoundSynth.Birds`), som spelets övriga ljud | projektets egen kod |

**Inventering (2026-11-02):** projektet har inga ljudfiler. CampfireVR har ett eldsprak utan licensuppgift och ett Zapsplat-klipp som inte får spridas, så inget av dem används. Asset Store-cachen innehåller inga naturljud.

**Alternativ om syntesen låter konstgjord i headsetet** (inget köps utan godkännande):
- CC0-inspelningar från [Freesound](https://freesound.org) (filtrera på licens *Creative Commons 0*). Kräver gratis inloggning.
- Sonniss *GDC Game Audio Bundle*: gratis och royaltyfri för spel, med naturinspelningar.
- Unity Asset Store: gratis naturljudpaket med Standard EULA, hanterade som parkmodellerna (selektiv import, inte i git).

### Acceptanskriterier, slice 1

| # | Kriterium | Verifiering |
|---|---|---|
| A1 | Bakgrundsgrupperna håller kundernas och barnens vägar, marknaden och arbetsytan fria, och står bredvid gången. | [T: Park_KeepsRoutesFree] [T: Park_KeepsMarketAndWorkAreaFree] [T: Park_TreesStandOffThePath] (gäller alla föremål) |
| A2 | Siktlinjer längs gången framåt och bakåt är fria. | [T: Park_KeepsSightlinesAlongThePath] |
| A3 | Parken omger marknaden runt om (ingen sektor över 70° utan träd mellan 6 och 20 m), utom siktlinjerna. | [T: Park_SurroundsTheMarket] |
| A4 | Spakgång stoppas vid alla stammar. | [T: Walk_StopsAtParkTreesBenchAndStones] (gäller alla föremål) |
| A5 | Fåglar är sporadiska: 7–18 s mellan kvitter, aldrig samma träd eller klipp två gånger i rad. | [T: Ambience_BirdsAreSporadicAndVaried] |
| A6 | Under paus inga fåglar och ingen klump efteråt. | [T: Ambience_PauseHoldsTheBirds] |
| A7 | *Av* ger tystnad, och efter *Av* → *normalt* dröjer det minst 7 s till första fågeln. | [T: Ambience_OffIsSilent] |
| A8 | Ingen fågel har en skarp ansats eller topp. (Vindslingan är borttagen.) | [T: Synth_BirdsAreSoft] |
| A9 | Miljöljudet ligger under återkopplingsljuden. | [T: Synth_AmbienceBelowFeedback] |
| A10 | Menyn: *För föräldrar* visar och växlar *Naturljud*, menyn stängs inte, och valet påverkar inte spelet. | [T: Menu_ParentCyclesNatureSound] |
| A11 | Ingen loopande ljudbädd i scenen. Fåglarna följer inställningen, och under paus kommer inga fåglar, inte heller i klump efteråt. | [T: Ambience_BirdsFollowSettingAndPauses] |
| A12 | Regression: försäljning, lek, menyer, kalibrering. | hela `scripts/quest.sh test` |
| A13 | Parken känns sammanhängande, horisonten är bruten och marknaden syns tydligt. | [R: Snapshots_Park] |
| A14 | Headset: ljudet är diskret, ingen skarv hörs och fåglarna störs inte. Bildfrekvensen är som förut. | [H] [D] |

## Slice 2 – vänligare figurer i Blender

Påbörjas efter slice 1 på headset. Planen preciseras då.

1. **Inventera** kundfigurerna (`CustomerFigure`: primitiver, humör och vinkning), barnfigurerna och mobilkundens porträtt (`Portrait`).
2. **En provfigur** i `art/people/make_people.py` på `art/kit.py`: rundad och lekfull, med tydligt ansikte, mjuk kropp, enkla händer, frisyr och kläder. Ansiktet behåller ledsen och glad (godiskunderna) och plats för mobilkundens foto. Logiska objekt, rörelsebanor och vinkning behålls. Inget nytt animationssystem.
3. **Liv:** diskret gungning eller huvudrörelse när figuren väntar, en tydlig vinkning eller nickning vid hälsning och tack, och fötter som inte glider orimligt.
4. **Visa** provfiguren i närbild och i spelarvy innan fler varianter görs. Därefter ett litet urval med delade meshar och material.

## Verifieringslogg

**Slice 1, röda före implementation (2026-11-02):** mot skelett föll `AmbienceTests` (6 av 7: inga fåglar, ingen vind, inga syntesklipp, ingen menyrad), `Park_SurroundsTheMarket` (inga grupper) och `Ambience_OneWindSourceThroughPauses` (ingen vindkälla). Två tester gick igenom redan mot skelettet: `Ambience_PauseHoldsTheBirds` var tomt eftersom skelettet inte gav några fåglar, och `Park_KeepsSightlinesAlongThePath` gick igenom eftersom EPIC-013:s park redan höll siktlinjerna fria.

**Under arbetet:**
- `Ambience_BirdsAreSporadicAndVaried` föll först på en lucka på 18,035 s mot max 18,014 s. Testets klocka var en flyttalssumma som drev under 20 simulerade minuter. Testet räknar nu tiden från rutnumret, och `Ambience` tar med sig överskottet till nästa väntan.
- `Walk_StopsAtParkTreesBenchAndStones` startade ibland inuti ett grannträd i samma grupp. Testet väljer nu en fri startpunkt.
- Granen (`fir tree 1`, 10 trianglar) syntes i spelarvyn som en stor platt kon på cirka 7 m och togs bort ur urvalet. Det är nu 13 paketfiler.

**Gröna (2026-11-02):** `scripts/quest.sh test` gav EditMode 203/203 och PlayMode 91/91 (7 explicita snapshots överhoppade).

**Renderingar [R]** (`Snapshots_Park`, samma vyer som EPIC-013): trädgrupper runt om, kullar vid horisonten, fri siktlinje längs gången framåt och bakåt, och varor och beställningsbubbla med oförändrad kontrast. Naturljuden skrivs också som WAV (`Logs/shots/nature-*.wav`) för lyssning utanför headsetet.

**Budget i Editor** (samma läge som EPIC-013: kund vid disken, två lekande barn, 7 leksakskroppar):

| | Renderare | Delmeshar | Trianglar |
|---|---|---|---|
| EPIC-013 (park) | 629 | 753 | 152 507 |
| EPIC-014 slice 1 | 629 | 757 | 169 825 |

Parkmodellerna är fortfarande en mesh med ett material. Buskar och kullar ligger i formmeshen (en delmesh per färg, 4 nya). Två nya ljudkällor tillkommer: en vindkälla och 8 fågelkällor som bara spelar korta klipp.

**Headset, slice 1 (2026-11-02, bygge `2026-11-02_091109_3abbce1`, föräldern):** ”vinden var direkt jobbig, den kan vi ta bort”. Därför är vinden borttagen: källan, syntesen (`SoundSynth.Wind`) och volymen. Fåglarna och inställningen är kvar. Tester: `Synth_WindLoopIsSeamlessAndEven` togs bort, och `Ambience_OneWindSourceThroughPauses` ersattes av `Ambience_BirdsFollowSettingAndPauses` (ingen loopande källa; fåglarna följer inställningen och pausen). Övriga intryck av parken och fåglarna är inte rapporterade än.
