# Przekazanie sesji (stan na 2026-10-14)

Notatka dla nowej sesji po czyszczeniu kontekstu. Najpierw przeczytaj `CLAUDE.md` i `docs/GDD.md`.
Historia decyzji jest w GDD §9 (punkty 1-89), a lista testów w `docs/TEST_PLAN.md`.

## Gdzie jesteśmy

- Gałąź robocza: **`main`** (historia przepisana: bez stopek asystenta, autor Eziek). Stara gałąź `claude/...` do usunięcia
  na GitHubie po ustawieniu `main` jako domyślnej. PR-ów nie otwieramy bez prośby gracza.
- Wersja zapisu: **v16** (Przystań we mgle: slot między rejsami, pole `mist`; `clockOffset`: własny cykl dnia zapisu,
  nowa wyspa zaczyna się o 07:00).
- Gracz testuje w Unity 6.6 i w buildzie Windows, odsyła zrzuty i uwagi.
- Kod kompiluje się na atrapach (`tools/cscheck`), shadery przez `tools/shadercheck`.
- **Gra nazywa się Misty Packland** (GDD §16.5). Pakiet do wydania: `docs/RELEASE.md`.
- **Etap 8** (losowe sadzenie, 60 ulepszeń handlu, prestiż „Rejs we mgłę”): GDD §11.8, §9 p. 78, `docs/UPGRADES_TRADE.md`,
  TEST_PLAN 9. Ceny sprzedaży liczy tylko `Economy/Market.cs` (nowe źródła sprzedaży muszą iść przez niego).
- **Scena gry: `MainLevel.unity`** (dawniej Prototype). UI: czytelna czcionka Fusion Pixel 8px (opcja), poprawki z przeglądu
  kodu UI (GDD §9 p. 79, TEST_PLAN 10). Komentarze w kodzie bez historii rozmów; commity bez stopek o asystencie.
- **Ostatnio (wybór gracza):** scenka rejsu, świt/zmierzch, narzędzia ogników, sklep z czapkami (GDD §9 p. 84, 86-88).
  Następne z listy propozycji: zdarzenia na horyzoncie z sekcją „Widziane” w Bestiariuszu (gracz jeszcze nie wybrał).
- **Prestiż przebudowany:** Przystań we mgle między rejsami, dary losowane za perły z animacją walki, 37 darów z rzadkością
  (GDD §11.8, §9 p. 81, TEST_PLAN 12). Przenosiny `CLAUDE.md` gracz chce zrobić później.
- **Pogaduszki ogników** (dymki z runami, szczebiot z `tools/audio/gen_babble.py`), ogniki ocierają się zamiast przenikać,
  pomost i sieci da się przenieść (GDD §9 p. 80, TEST_PLAN 11). Sprite ognika siedzi teraz na dziecku „Body” (odsuwanie).
- **Gracz nie widział jeszcze w Unity:** Przystani we mgle (p. 81), pogaduszek i przenoszenia pomostu (p. 80), porządków UI (p. 79), Etapu 8, powrotu do 16 px z wodą 32 px, nowej mgły (chmury, GDD §9 p. 75-76),
  logo w menu, menu Wyspa → Wydanie i buildu z nową nazwą (TEST_PLAN 4d i 8).

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

### Etap 7 (32 px): odrzucony, wróciliśmy do 16 px, woda została

Ocena gracza: *„za bardzo wygładzone, nijakie, wcześniej było ostrzej i z większym kontrastem; woda jest świetna”*.
Decyzja (GDD §9 p. 74, wariant C):
- sprite'y świata: znów 16 px (PNG i `pivots.json` ze stanu `07d4a72` + ryba z `export_unity_auto.py`, potem `pad_sway.py`);
- teren: `Island.cs` i `IslandPainter.cs` jak przed `386864d` (bez `PaintHi`, ląd PPU 16);
- kołysanie: `_SwayAmount` 1 (WyspaSetup);
- mgła: nowa, kłębiaste chmury jak z referencji gracza w brudnej palecie (GDD §9 p. 75, podgląd `docs/img/mgla_chmury.png`), siatka 32 px;
- **zostaje**: `Water.shader` w 32 px, kamera PPU 32 z kadrem x2 (`ViewController.ArtScale`), parzysta skala okna (x2, x4, x6),
  `AmbientLife` (ćmy, ryba), pulsowanie roślin, obsługa pola `"hd"` w importerze (nieużywana);
- `tools/art32/` zostaje jako archiwum, `convert32.py` nie uruchamiać.

**Gracz jeszcze nie widział tego połączenia w Unity** (potrzebne: import PNG, **Wyspa → 2**). Ryzyko do sprawdzenia:
grafiki 16 px obok wody z pikselem o połowę mniejszym. Jeśli będzie zgrzytać, można dać wodzie siatkę 16 px w shaderze.

## Do decyzji z graczem na start nowej sesji

1. **Wygląd 16 px + woda 32 px i nowa mgła** po teście w Unity (sekcja 4d w `docs/TEST_PLAN.md`).
2. **Wydanie:** gracz ocenia ikonę, logo i obrazy sklepowe (`release/store/`, `docs/img/ikona_podglad.png`);
   do ustalenia: nazwa firmy w sklepie (w Unity „Eziek”), wersja (0.9.0), cena, data, Steam czy najpierw itch.io.
3. **Progi rytmu pracy** (seria 25 min, przerwa 5-30 min, zmęczenie 8/10 h). Propozycja: zostawić do balansu na końcu.
   Gracz nie odpowiedział.
4. **Font 12 px i ikony UI**: osobny etap, nic nie zaczęte.
5. **Balans** na samym końcu (decyzja gracza).

## Ważne zasady pracy (skrót z CLAUDE.md i rozmów)

- Rozmowa po polsku. Gracz pisze krótko i chce, żeby działać. Przy decyzjach o stylu pokazywać próbki (obrazki), zanim się wdroży.
- Gracz lubi mroczną, stonowaną estetykę i „pixel art, a nie anime”. Nie znosi „klusek” (kształtów z kulek).
- Każdy tekst w 5 językach (PL, EN, DE, JA, ZH) w CSV. Czcionka `WyspaPixel` nie ma „ ” „ – …, więc w PL/EN/DE używać `"` i `-`.
- Po zmianach: GDD §7 i §9, `docs/TEST_PLAN.md`, kompilacja na atrapach (oba warianty define), shadery, commit ze stopką, push.
- Grafiki: eksport → `pad_sway.py` (bez `convert32.py`). Placeholdery i próbki rysuje kod w `tools/pixel-proto` i `tools/art32`.
