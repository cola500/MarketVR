---
title: Backlog
last_updated: 2026-11-16
---

# Backlog

Förbättringar som inte är påbörjade, i prioritetsordning. Från genomgången av `FruitStand.cs` och `PlayTableView.cs` vid namnbytet till MarketVR (2026-11-16). Varje punkt görs i egna commits, refaktoreringar med oförändrat beteende och verifierade med testerna.

1. **Mät det nuvarande bygget** med `scripts/quest.sh perf 120` under spel med kunder, besökare och tre lekande barn, när headsetet är ledigt. Optimera bara det som mätningen pekar ut.
   - Senaste mätningen (EPIC-013, 2026-11-02): 72,6 FPS i medel, minst 72, CPU/GPU-nivå högst 4/3.
   - EPIC-015–018 är inte mätta: gående kunder och besökare, bollkast, byggplats och grodor.
2. **`PlayTableView`: en klass per leksak**, som grodorna redan har påbörjat (`PlayTableView.Frogs.cs`). I dag fördelas varje leksak via `switch` i sex metoder (`PutOnTable`, `Step`, `Play`, `PlayerTook`, `PlayerReleased`, `Reset`), och barnets tillstånd (`Child`) samlar fält för alla leksaker.
3. **`FruitStand.Step` (108 rader):** dela upp i menyval, händer och logik för grepp och släpp (`OnGrab`, `Drop` och `SendHome`, som kan bli en egen testbar klass), tick och presentation.

## Klart

- **Gemensam testhjälp för scentesterna** (2026-11-16, `04c8435`, `e40442d`).
  - `Tests/PlayMode/SceneSteps.cs` har ett steg, `Wait` och `WaitUntil`. Varje fil behåller sina tidssteg och standardvärden.
  - Dessa filer använder hjälpen: ToyPlay, ForgivingPlay, PlaySequences (med sin tidlogg), Calibration, PhoneCustomer, MarketParty, Market, Stand, Frog, SharedPlay (med sin callback efter varje steg), Pause och Snapshots.
  - Kvar lokalt, eftersom de beter sig annorlunda:
    - Arrival och ParkVisitor räknar antal steg med avrundning och kontrollerar efter varje steg.
    - Assortment stegar en given frame.
    - `Frame`/`RigFrame` är specialfall.
    - Varje `SellToyAndWaitForPlay` reserverar lekplatser och väntar på kunder på sitt eget sätt.
  - Verifierat:
    - `quest.sh test`: EditMode 257/257, PlayMode 112.
    - `quest.sh snapshots` (Snapshots och PlaySequences): 10/10.
    - Logg för boll och klossar identisk med EPIC-017.
- **Deterministisk grodsekvens** (2026-11-16).
  - `PlaySequences` seedar parkbesökarnas val (`FruitStand.StrollRandom`), så att loggarna blir lika i varje körning.
  - Verifierat med två körningar: identiska loggar för alla tre sekvenserna. Boll och klossar är fortfarande lika med EPIC-017.
  - Kedjan finns i [TESTING.md](TESTING.md).
