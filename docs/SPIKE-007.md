---
title: SPIKE-007 – spelytans gräns på Quest
status: run-on-quest
last_updated: 2026-11-01
---

# SPIKE-007 – spelytans gräns på Quest

Underlag för [EPIC-007](EPIC-007.md) (rikta in marknaden mot rummets spelyta).

## Frågor

1. Är fyrkanten som syns i spelet vår egen markering eller Quests systemgräns?
2. Hur ser XR-stacken och koordinatsystemen ut?
3. Vad ger befintliga XR-API:er om spelytan på en riktig Quest 3, utan nytt SDK?

## Inventering (kod, 2026-11-01)

| Vad | Svar |
|---|---|
| Spelets egen golvmarkering | **Finns inte** som fyrkant. Den enda golvdekoren är en oval matta under lekbordet (`PlayTableView`, ”Rug”). Fyrkanten som syns i headset är därmed **Quests systemgräns** (Guardian/spelytan), som appen varken ritar eller ändrar. *(Slutsats från koden. Bekräftas i headset genom att se om den syns även utanför appen.)* |
| XR-stack | Unity 6000.4.7f1, `com.unity.xr.management` 4.5.0, `com.unity.xr.oculus` 4.5.0 (Oculus-loader). Inget OpenXR-paket, inget Meta XR SDK, inget XRI. |
| Koordinatsystem | **Spårningsrummet** (tracking space): golvorigo (`TrackingOriginModeFlags.Floor`), och appen gör `TryRecenter` vid start, så origo hamnar under huvudet vid start. **XR-riggen** är spårningsrummets föräldratransform i världen. Den står i origo vid start och flyttas eller vrids av spakgång och vridning (EPIC-005). **Marknadens rot** är en världstransform som placeras vid start, vid omcentrering och via menyn. Alla stånd, lekbordet, krokodilhyllan och golvavtrycken för gången (`MarketCollision`) är i marknadens koordinater (`MarketLayout`). Huvudets och händernas spårade poser ändras aldrig av appen. |
| API: `XRInputSubsystem.TryGetBoundaryPoints` | Unity-standard. Ger gränsens polygon i spårningsrummet *om providern stöder det*. Okänt för Oculus-providern på Quest 3. **Prövas på enheten.** |
| API: `Unity.XR.Oculus.Boundary` | `GetBoundaryConfigured`, `GetBoundaryVisible`, `SetBoundaryVisible` och `GetBoundaryDimensions(PlayArea/OuterBoundary)`. Ger bara **storlek** (axelparallell låda), ingen position eller orientering, och inga punkter. **Prövas på enheten.** |
| Nytt SDK | Läggs inte till. Meta XR Core SDK/OpenXR har rikare scen- och gräns-API:er, men det är ett stort beroende. |

## Sond

`BoundaryProbe` (bara läsning) loggar båda källorna `[Fruktstand] boundary (...)` 3 s och 15 s efter start, vid omcentrering och vid `boundaryChanged`: ursprungsläge, configured/visible, PlayArea- och OuterBoundary-mått, antal punkter, punkternas omslutande rektangel och upp till 16 punkter.

**På enheten:** installera bygget, starta, vänta 15 s, håll in Meta-knappen en gång och kör `scripts/quest.sh log | grep boundary`.

## Resultat på Quest (2026-11-01 12:41, bygge `…_123810_21cb72d`, Quest 3 `QUEST-SERIAL`)

```
boundary (tracking origin updated): origin=Floor configured=True visible=False playArea=(1.71, 10.00, 1.86) outer=none points=0
boundary (15 s after start (head (-2.31, 0.61, -2.56))): origin=Floor configured=True visible=False playArea=(1.71, 10.00, 1.86) outer=none points=0
```

| Fråga | Svar |
|---|---|
| `TryGetBoundaryPoints` | **Fungerar inte här:** 0 punkter. Vi får ingen polygon och ingen form. |
| `Oculus.Boundary` | Gränsen är konfigurerad. Spelytan är **1,71 × 1,86 m** (x × z, höjden rapporteras som 10 m). Den yttre gränsen saknas. Bara mått, ingen position eller orientering. |
| Ändras mätvärdet över tid? | Nej, 3 s och 15 s efter start gav samma värde. |
| Origo och omcentrering | Huvudet (headsetet låg ned) var vid (−2,31 ; −2,56), alltså 3,4 m från spårningsorigo, trots att appen gör `TryRecenter` vid start. **Omcentreringen flyttar alltså inte origo på Quest 3 med golvläge.** |
| Hypotes, inte bekräftad | Golvorigo är spelytans mitt och axlarna följer spelytans rektangel (Oculus brukliga definition). Mitt-testet (stå mitt i gränsen och läs huvudets position) är inte gjort. **[A]** |

**Vad saknas:** gränsens form (oregelbunden yta), och bekräftelse på var spelytan ligger och hur den är vriden. Inget av det ges av den befintliga stacken. Meta XR SDK läggs inte till enbart för detta.

**Beslut för EPIC-007:** förslaget är en rektangel med Oculus-måtten, centrerad i spårningsorigo och längs spårningsaxlarna **[A]**. Föräldern ser förslaget som en förhandsvisning på golvet, jämför med systemgränsen och bekräftar eller justerar manuellt. Saknas måtten (`GetBoundaryDimensions` misslyckas) börjar inställningen manuellt där föräldern står. Passningen mot ytan tar emot en godtycklig polygon och testas även med oregelbundna ytor, så att en framtida polygonkälla kan användas direkt.

## Konsekvens för EPIC-007

EPIC-007 byggs så att båda utfallen fungerar: med en polygon föreslås placering automatiskt, och utan polygon görs manuell inställning (med Oculus-måttet som förifyllt förslag, om det finns). Spiken avgör vilken väg spelaren faktiskt får.
