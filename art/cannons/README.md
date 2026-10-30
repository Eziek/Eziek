# Armaty 3D (stylizowane)

Dwie armaty na wzór obrazka referencyjnego (gra karciana na pokładzie statku): kute żelazo w granatowo-stalowym
odcieniu, mosiężne okucia, drewniana podstawa z narożnikami i obrotowym pierścieniem zębatym.

| Plik | Co to jest |
|---|---|
| `Cannon_Twin.fbx` / `.glb` | bliźniacze działko: dwie krótkie lufy, seria 4 strzałów na zmianę (L, P, L, P) |
| `Cannon_Heavy.fbx` / `.glb` | ciężka armata: lufa z mosiężnymi pierścieniami na jarzmie, jeden mocny strzał |
| `cannons.blend` | scena Blendera (obie armaty, scena podglądu, kamera, światła); tekstury spakowane w pliku |
| `textures/` | po 1024 px na armatę: `BaseColor`, `Normal` (OpenGL, tangent), `Roughness`, `Metallic`, `MetallicSmoothness` (Unity) |
| `renders/` | `preview.png`, `preview_fire.png`, `fire.gif` |

Wszystko generuje `tools/blender/cannons.py` (opis na górze pliku). Ponowne wygenerowanie:

```bash
python -m pip install bpy==5.2.3   # Python 3.13; albo: blender -b -P tools/blender/cannons.py -- --out art/cannons
python tools/blender/cannons.py --out art/cannons
```

## Budowa i animacja

Hierarchia (te same nazwy w FBX i GLB; w Blenderze lufy patrzą w -Y, w Unity powinny w +Z, nie sprawdzone w edytorze):

```
Cannon_Heavy                      Cannon_Twin
├ Heavy_Base (drewno + okucia)    ├ Twin_Base
├ Heavy_Turntable (pierścień)     ├ Twin_Turntable
├ Heavy_Turret      obrót Y = celowanie w poziomie
│ └ Heavy_Elevation obrót X = uniesienie lufy
│   └ Heavy_Barrel  odrzut wzdłuż lufy       └ Twin_Body └ Twin_Barrel_L / _R
│     └ Heavy_MuzzleFlash                                  └ Twin_MuzzleFlash_L / _R
└ Heavy_Smoke_01..07                     └ Twin_Smoke_L_01.. / _R_01..
```

Klip `Fire`: 60 klatek przy 30 fps (2 s), zaczyna się i kończy w pozie spoczynkowej, więc można go zapętlić.
Ciężka armata: przygarbienie (kl. 8-12), strzał w kl. 13 (odrzut lufy, podrzut jarzma, ściśnięcie i rozciągnięcie
wieżyczki, drżenie), powrót do kl. ~40. Działko: strzały w kl. 9, 15, 21, 27.
Błysk i dym to siatki skalowane od zera; w silniku lepiej podmienić je na system cząsteczek i zostawić same
punkty zaczepienia (`*_MuzzleFlash`).

Około 20 tys. trójkątów na armatę (fazowane krawędzie są w siatce, nie tylko w normal mapie).

## Import do Unity (URP)

1. Skopiuj `Cannon_*.fbx` i `textures/` do `Assets/` (np. `Assets/_Project/Models/Cannons/`).
2. Model: zakładka *Model*: *Bake Axis Conversion* włączone. *Animation*: klip `Fire`, przy zapętleniu *Loop Time*.
3. Materiał URP Lit: *Base Map* = `*_BaseColor`, *Metallic Map* = `*_MetallicSmoothness` (Source: Metallic Alpha),
   *Normal Map* = `*_Normal` (w imporcie tekstury typ *Normal map*). Normal mapa jest w konwencji OpenGL (Y+), tak jak w Unity.
4. Błysk (`M_MuzzleFlash`, `M_MuzzleCore`) najlepiej dać na materiał Unlit/addytywny.

## Tekstury: skąd się wzięły

Substance Paintera ani Designera nie ma w środowisku, w którym powstały pliki, więc tekstury są wypieczone w Blenderze
(Cycles) z proceduralnych materiałów działających jak warstwy w Painterze: rozjaśnione krawędzie przerywane
„pędzlem” (szum), cień w zagłębieniach (AO tylko w obrębie części, bo części się ruszają), światło od góry
wmalowane w kolor, ziarno drewna i drobne młotkowanie żelaza w normal mapie. Każda armata ma jedną mapę UV
(atlas), więc w Painterze można otworzyć FBX i malować dalej na tych samych UV.
