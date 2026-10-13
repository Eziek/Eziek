# Uruchomienie w Unity

**Wersja:** Unity **6.3 LTS**, czyli `6000.3.26f1` (najnowsza wersja LTS z 2026-10-08, wsparcie do grudnia 2027).
Każda nowsza łatka `6000.3.x` też będzie dobra.

## 1. Nowy projekt
1. Unity Hub, zakładka **Installs**, zainstaluj `6000.3.26f1` z modułem **Windows Build Support**.
2. **New project**, szablon **Universal 2D** (URP z 2D Rendererem i Input System już skonfigurowanymi).

## 2. Skopiuj pliki z repo
Skopiuj cały folder **`Assets/_Project`** z tego repo do folderu `Assets/` w nowym projekcie.
Unity sam zaimportuje pliki, a skrypt `PixelArtImporter` ustawi sprite'om:
- PPU 16,
- filtr Point,
- brak kompresji,
- pivot w punkcie styku z ziemią.

## 3. Menu „Wyspa” (pasek u góry edytora)
1. **Wyspa → 1. Ustawienia projektu**: okno 816×600, praca w tle, sprawdzenie, czy jest URP.
2. **Wyspa → 2. Utwórz dane (rośliny, lampy)**: tworzy ScriptableObjecty w `Assets/_Project/Data`.
3. **Wyspa → 3. Zbuduj scenę prototypu**: buduje `Assets/_Project/Scenes/Prototype.unity`:
   - kamera Pixel Perfect 272×200,
   - Global Light 2D sterowany porą dnia,
   - woda, wyspa, drzewa,
   - 8 grządek,
   - 4 lampy z Light 2D,
   - obiekt `Game` z całą logiką.

Potem naciśnij **Play**.

## 4. Testowanie (HUD w lewym górnym rogu)
- **Nasiona:** wybierz roślinę i kliknij pustą grządkę, żeby posadzić (kosztuje monety). Kliknięcie dojrzałej rośliny ją zbiera.
- **Wzrost** wymaga aktywności, czyli ruchu myszy lub klawiatury w ostatnich 60 s (cały system, nie tylko okno gry)
  i światła (dzień albo lampa).
- **Czas x60:** minuta gry mija w sekundę. Marchew rośnie 10 minut aktywności, więc z x60 w 10 sekund.
- **12:00 / 19:30 / 23:30:** podgląd pory dnia. W nocy lampy świecą, jeśli jest Esencja.
  **Zegar systemowy** wraca do prawdziwej godziny.
- **Wymuś aktywność:** do testów bez ruszania myszą.
- **Zapis** trafia do `%USERPROFILE%/AppData/LocalLow/<firma>/<projekt>/save.json` (co minutę i przy wyjściu).

## 5. Build
Unity 6: **File → Build Profiles → Windows → Build**.

## Gdy coś nie działa
| Objaw | Co zrobić |
|---|---|
| Sprite'y rozmyte albo w złym miejscu | **Wyspa → Narzędzia → Reimportuj sprite'y** |
| Wszystko czarne | Brak Global Light 2D albo projekt bez 2D Renderera. Użyj szablonu **Universal 2D** i przebuduj scenę |
| Płomienie lamp przyciemnione w nocy | Brak materiału `Data/GlowUnlit.mat` (shader URP Sprite-Unlit-Default). Uruchom ponownie krok 2 |
| Klikanie nie działa | Na scenie musi być `ClickRouter` (obiekt `Game`), a grządki muszą mieć BoxCollider2D |

## Co już jest, a czego jeszcze nie ma
**Jest (Etap 1, szkielet systemów):**
- aktywność z systemu (`GetLastInputInfo`),
- Esencja (+1 za minutę aktywności, pojemność 60),
- lampy zużywające Esencję w nocy (najdroższe gasną pierwsze),
- oświetlenie pól,
- rośliny dnia i nocy (logika),
- pora dnia z zegara i faza księżyca,
- zapis JSON z postępem offline roślin nocy,
- „pop” przy zmianie fazy,
- adaptacyjne FPS (30 z fokusem, 12 w tle).

**Jeszcze nie ma (kolejne etapy):**
- shader kołysania roślin (Shader Graph, ruch o całe piksele),
- animowana woda (shader z mapą odległości),
- ryby, mgła,
- cząsteczki (monety, iskierki, listki),
- rośliny nocy (grafiki),
- sklep i powiększanie wyspy,
- docelowe UI pixel art,
- zdarzenia nocne,
- pasmowe światło z ditheringiem (na razie zwykłe Light 2D).

## Sugestia: cały projekt w tym repo
Najwygodniej będzie trzymać **cały projekt Unity w tym repozytorium**. W katalogu głównym jest już `.gitignore` dla Unity.
Wtedy mogę pracować bezpośrednio na plikach projektu, a nie tylko na `Assets/_Project`.
