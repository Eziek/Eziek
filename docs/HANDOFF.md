# Przekazanie sesji (stan na 2026-10-14)

Notatka dla nowej sesji po czyszczeniu kontekstu. Najpierw przeczytaj `CLAUDE.md` i `docs/GDD.md`.
Historia decyzji jest w GDD §9 (punkty 1-72), a lista testów w `docs/TEST_PLAN.md`.

## Gdzie jesteśmy

- Gałąź robocza: `claude/unity-project-discussion-rlr9ae`. PR-ów nie otwieramy bez prośby gracza.
- Wersja zapisu: **v14**.
- Gracz testuje w Unity 6.6 i w buildzie Windows, odsyła zrzuty i uwagi.
- Kod kompiluje się na atrapach (`tools/cscheck`), shadery przez `tools/shadercheck`.
- **Ostatnich zmian (Etap 7) gracz nie widział jeszcze w Unity.**

### Zrobione w ostatniej serii (od GDD §9 p. 58)

Funkcje gry:
- **Rój ogników.** Gniazdo, 6 zawodów, poziomy, imiona. Runa dziczy (wokół wyrastają drzewa i kamienie).
- **Skala UI zależna od monitora.** Opcja „Rozmiar interfejsu”.
- **Treść:**
  - 5 nowych hybryd;
  - cele wypraw łodzią z tabelami łupów;
  - historia „Ostatnia latarnia” (36 zapisków) z finałem „Światło Opiekunów” przy ołtarzu w noc pełni.
- **Żar zamiast Esencji** w tekstach (w kodzie nadal `Essence`).
- **Drzewko ulepszeń.** Odkrywana mapa, 18 ulepszeń, efekty w `UpgradeEffects`. Kropki poziomów, przewijanie.
- **Podmiana roślin dzień/noc:**
  - klik w rosnącą roślinę otwiera koło nasion (stare nasiono wraca do worka);
  - w pierścieniu akcji „Wykop”;
  - siewca ma osobne nasiona na dzień i na noc oraz przełącznik „Podmiana o świcie i zmroku”.
- **Skład.** Sprzedaje nadwyżkę drewna i kamienia ponad rezerwę.
- **Wiatr.** Margines 6 px dla kołyszących się sprite'ów (`tools/pixel-proto/pad_sway.py`), bez pionowej kreski.
- **Księga.** Przewijanie i przesuwanie treści (ScrollRect).

### Etap 7 (32 px): zrobiony, ale gracz go **nie akceptuje**

Ocena gracza po zrzutach: *„za bardzo wygładzone wszystko, takie nijakie, wcześniej lepiej wyglądało”*.
Wcześniej zaakceptował próbki: wodę i teren z `tools/art32/probka32.py` (`docs/img/art32_woda.gif`) i rośliny „crisp + dark fantasy”
(`docs/img/art32_rosliny.png`). Efekt w grze (Scale2x, wygładzone brzegi) uznał za gorszy od starego 16 px.

Commity Etapu 7, od najstarszego:

| Commit | Co |
|---|---|
| `9979804` | próbka stylu (`tools/art32/probka32.py`, `px.py`) |
| `a8f3ec9`, `617b2b1` | `tools/art32/hd2x.py` (Scale2x + detal, tryb crisp, paleta DARK_FANTASY), arkusz porównania |
| `f39f5c1` | **część 1**: `convert32.py` przerobił 255 PNG na 32 px; importer PPU z pola `"hd"` w `pivots.json`; kamera PPU 32 i kadr x2 (`ViewController.ArtScale`), skala okna parzysta; kołysanie x2 |
| `386864d` | **część 2**: teren 32 px (`IslandPainter.PaintHi`, EPX na brzegu); `Island` ładuje `islandHi` z PPU 32 |
| `1fc2f3d` | **część 3a**: `Water.shader` w 32 px (paralaksa dna, światełka, łamiące się fale, kręgi) |
| `e71c827` | **część 3b**: `Fog.shader` w 32 px, ciemniejsza obwódka, limit zasięgu w przezroczystym oknie |
| `364ae2c` | poprawka: poświaty tylko tam, gdzie były |
| `e2da7f0` | **część 4**: `Visual/AmbientLife` (ćmy przy lampach, skacząca ryba), pulsowanie świecących roślin |

Wcześniejsze commity z tego samego czasu nie należą do Etapu 7 i mają zostać: `d45457d` (przewijanie Księgi), `07d4a72` (Skład),
`cc01b12` (podmiana roślin), `0df4923` (margines kołysania).

## Do decyzji z graczem na start nowej sesji

1. **Co z grafiką 32 px?** Możliwe drogi:
   - **A. Powrót do starej grafiki 16 px.** Sprite'y: uruchomić `export_unity*.py` i `pad_sway.py` **bez** `convert32.py`.
     Eksport nadpisze PNG wersjami 16 px i usunie z `pivots.json` pola `"hd"`, a importer da PPU 16. Teren: w `Island.Rebuild` wrócić do
     `picture.island` i PPU 16 (albo dodać przełącznik zamiast `HiScale`). Kamera z `ArtScale = 2` działa też z grafikami 16 px,
     ale można wrócić do `ArtScale = 1` i dawnych skal okna x2-x6. Woda i mgła: shadery z `1fc2f3d` i `e71c827` liczą w 32 px,
     do starego wyglądu trzeba je cofnąć (`git revert` tych dwóch commitów) albo zostawić same efekty (dno, światełka, fale).
   - **B. Zostać przy 32 px, ale bez wygładzania.** Zamiast Scale2x zwykłe x2 (najbliższy sąsiad) i ręcznie dodany detal,
     teren bez EPX (kanciasty brzeg jak w 16 px). Podejrzenie: „nijakość” to głównie Scale2x i paleta DARK_FANTASY.
   - **C. Mieszanka.** Stare sprite'y 16 px, nowa woda z efektami (gracz chwalił próbkę wody) i stary teren.
   Najpierw zapytać gracza, co konkretnie było lepiej „wcześniej”. Warto pokazać obok siebie zrzut starego 16 px i nowego 32 px.
2. **Progi rytmu pracy** (seria 25 min, przerwa 5-30 min, zmęczenie 8/10 h). Propozycja: zostawić do balansu na końcu.
   Gracz nie odpowiedział.
3. **Font 12 px i ikony UI**: osobny etap, nic nie zaczęte.
4. **Balans** na samym końcu (decyzja gracza).

## Ważne zasady pracy (skrót z CLAUDE.md i rozmów)

- Rozmowa po polsku. Gracz pisze krótko i chce, żeby działać. Przy decyzjach o stylu pokazywać próbki (obrazki), zanim się wdroży.
- Gracz lubi mroczną, stonowaną estetykę i „pixel art, a nie anime”. Nie znosi „klusek” (kształtów z kulek).
- Każdy tekst w 5 językach (PL, EN, DE, JA, ZH) w CSV. Czcionka `WyspaPixel` nie ma „ ” „ – …, więc w PL/EN/DE używać `"` i `-`.
- Po zmianach: GDD §7 i §9, `docs/TEST_PLAN.md`, kompilacja na atrapach (oba warianty define), shadery, commit ze stopką, push.
- Grafiki: eksport → `pad_sway.py` → (Etap 7) `convert32.py`. Placeholdery i próbki rysuje kod w `tools/pixel-proto` i `tools/art32`.
