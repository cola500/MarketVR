# Blender → Unity → Quest: modellflödet

*Spiken (bananen) visade att kedjan fungerar. Sedan EPIC-009/010 är flödet gemensamt för alla varor: 8 frukt/grönt, 8 godis, 6 leksaker, plus lekpjäserna ring och pinne.*

## Flödet nu (EPIC-009)

| | |
|---|---|
| Källa | `art/fruit/make_fruit.py`, `art/candy/make_candy.py`, `art/toys/make_toys.py` (en funktion per sort) med gemensamma byggstenar i `art/kit.py` (`lathe`, `blob`, `stick`, `box`, `torus`, `tube`, export, kontaktbild); `.blend` i varje mapp |
| Export | `UnityProject/Assets/Fruktstand/Models/<Ware>.fbx` för alla 22 varor, plus `Ring.fbx` och `RingPeg.fbx` |
| Kommando | `blender -b --factory-startup -P art/<fruit\|candy\|toys>/make_<…>.py [-- Namn …]`, sedan `scripts/quest.sh scene` |
| Kontaktbild | `art/fruit/`, `art/candy/`, `art/toys/contact_sheet.png` (renderade av skripten; per modell kan en vridning anges, t.ex. fordon från sidan) |
| Unity | `SceneSetup` sätter importinställningarna (inga material, ingen rigg eller animation) och lägger varje mesh i `MeshLibrary.WareModels[(int)ware]` (och `RingModel`/`RingPegModel`). `Models.Ware` använder modellen för alla varor, och `Models.ModelColours(ware)` ger färgen per delmesh. Saknas en mesh blir det primitiver. Leksakernas kollisionsvolymer sätts av `PlayTableView`, inte av modellen |
| Kontroll | `Scene_WaresAreBlenderModels`: en mesh per vara, delmeshar och färger, pivot i greppunkten, storlek mot greppradien, banan och morot längs +x |

Trianglar totalt: frukt 2 928, godis 2 620, leksaker + pjäser 2 852. Högst 904 per modell (stapelringarna). Frukt, per sort: äpple 316/3, banan 364/3, morot 312/2, päron 348/3, apelsin 292/2, jordgubbe 344/3, vindruvsklase 660/2, vattenmelonskiva 292/4. Totalt 2 928 trianglar.

**Ny sort:**
1. Lägg till en funktion i rätt `make_*.py` och lägg in den i `WARES`.
   Koordinater i Blender, efter den inbyggda vridningen: Unity x = Blender x, Unity y = Blender z, Unity z = Blender y (fordon kör mot +y).
2. Lägg till ett enum-värde sist i `Ware`, färger i `Colors`/`ModelColours` och en `Radius`.
3. Kör skriptet och `scripts/quest.sh scene`.
4. Granska kontaktbilden och `scripts/quest.sh snapshots`.

**Erfarenhet (EPIC-010):** Unitys nullkontroll fungerar inte med `is`/`?.` på Unity-objekt. Använd `!= null`/`TryGetComponent` (gäller `Art.ModelOf` och mätkoden i `Snapshots`).

## Spiken (bananen, historik)


Syfte: bevisa hela kedjan med en enkel modell. Bara bananens utseende ändras; identitet, pris, greppradie (`Models.Radius` 0,07 m), ordermatchning och placering i lådor, skyltar och pratbubblor är oförändrade.

## Arbetsgång

| | |
|---|---|
| Verktyg | Blender 5.2.3 LTS (`/opt/homebrew/bin/blender`), Unity 6000.4.7f1, inbyggd render pipeline (Standard-shader) |
| Källa | *(då)* `art/banana/make_banana.py`; nu en del av `art/fruit/make_fruit.py` |
| Export | `UnityProject/Assets/Fruktstand/Models/Banana.fbx` (24 kB) |
| Kommando | *(då)* `blender -b --factory-startup -P art/banana/make_banana.py` |
| Modell | 356 trianglar, 3 material/delmeshar: 0 Skal, 1 Stjälk, 2 Spets. 0,200 × 0,079 × 0,048 m, pivot i boundsmitten (greppunkten) |

FBX-export: `axis_forward=-Z`, `axis_up=Y`, `bake_space_transform`, `FBX_SCALE_UNITS` → Unity får en ren mesh i meter utan rotation på roten.

Unity-sidan: `SceneSetup` sätter importinställningarna (inga material, ingen rigg eller animation) och lägger meshen i `MeshLibrary.BananaModel`, så att den följer med i bygget. `Models.Produce` färgar delmesharna med `Art.Matte(Colors.…)`, alltså samma material och ljus som allt annat. Saknas meshen används den gamla procedurella bananen. Unity importerar inte `.blend`-filen själv eftersom den ligger utanför `Assets`.

## Problem i importkedjan (lösta i skriptet)

1. **x speglas.** Unitys FBX-import går från ett högerhänt till ett vänsterhänt system, så Blenders +x hamnar på Unitys −x och stjälken satt åt fel håll. Lösning: bananen vrids 180° runt upp-axeln i Blender före export.
2. **Delmesharnas ordning** följer den ordning materialen först förekommer bland ytorna, inte materialplatserna. Lösning: ytorna sorteras efter material före export.

Båda fångas av `Scene_BananaIsTheBlenderModel`: stjälken ligger på +x, spetsen på −x, och modellen har tre delmeshar och rätt färger.

## Verifiering

- **Blender:** förhandsbilden renderas av skriptet, och skriptet skriver ut triangelantal och mått.
- **Unity (automatiskt):** först rött (tre primitiva delar), sedan grönt `Scene_BananaIsTheBlenderModel`. Hela sviten: EditMode 143/143, PlayMode 69 godkända och 3 överhoppade (explicita).
- **Unity (visuellt):** `Snapshots` 02 och 09 visar bananen i lådan, på skylten och i högen, vänd åt rätt håll och i ungefär samma storlek som förut.
- **Bygge:** `2026-11-01_160748_e8082b2` (rent bygge, 24 MB), inte installerat.
- **Headset [H]:** inte gjort än. Godkänt när: (1) bananen är gul med brun stjälk och spets och ser rätt ut från alla håll (inga hål eller svarta ytor = normalerna stämmer); (2) den kan greppas som förut och går att lägga i korgen; (3) en beställning med banan går att sälja och betala; (4) bilduppdateringen känns oförändrad.

## Rekommendation

Ja för enkla, hårda föremål. Skriptet går att köra om, FBX-filen är liten, och färgerna kommer från koden, så materialen fungerar på Quest utan extra arbete. Mallen för fler saker: ett skript per modell i `art/<namn>/`, en FBX i `Models/`, en rad i `SceneSetup.AssignMeshes`, ett fall i `Models` med reserv, och ett scentest för riktning och delmeshar. Mer organiska former (djur, figurer) skulle vinna på att modelleras för hand i Blender. Då är `.blend`-filen källan, och ett litet exportskript bör behålla samma inställningar.
