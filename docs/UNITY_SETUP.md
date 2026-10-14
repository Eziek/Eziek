# Uruchomienie w Unity

**Wersja:** Unity **6.6**, czyli `6000.6.5f1` (wydanie polecane przez Hub). Każda nowsza łatka `6000.6.x` też będzie dobra.
To wydanie bez LTS, więc na kolejne wydanie Unity przechodzimy świadomie (kopia projektu na osobnej gałęzi).

## 0. Instalacja (raz)
1. **Klient Gita:** GitHub Desktop (https://desktop.github.com) **albo Fork** (https://git-fork.com, ma wbudowanego Gita;
   darmowy okres próbny, potem jednorazowa licencja). W Fork: File → Accounts → dodaj konto GitHub.
2. **Unity Hub:** https://unity.com/download. Zaloguj się (Unity ID) i aktywuj darmową licencję **Personal**
   (Hub → Preferences → Licenses → Add).
3. W Hub: **Installs → Install Editor → Unity 6.6** (`6000.6.5f1`, oznaczone „Recommended”). Zaznacz moduły:
   - **Windows Build Support (IL2CPP)**,
   - **Microsoft Visual Studio Community** (edytor kodu; jeśli masz już Rider albo VS Code, pomiń).

## 1. Repozytorium = projekt Unity
Repo jest od razu projektem Unity: `Assets/`, `Packages/` i `ProjectSettings/` w katalogu głównym, obok `docs/` i `tools/`.

1. **Sklonuj repo:** GitHub Desktop → File → Clone repository → `Eziek/Eziek` → folder np. `C:\Projekty\Pockland`.
2. **Gałąź `main`:** przełącz na `claude/unity-project-discussion-rlr9ae`, potem Branch → New branch → `main`
   (na bazie bieżącej). Na `main` będzie projekt z Unity.
3. **Projekt tymczasowy z szablonu:** Unity Hub → New project → Unity 6.6 → szablon **Universal 2D**
   → nazwa `Pockland` → lokalizacja **inna niż repo**, np. `C:\Projekty\Tmp`. Create, poczekaj na otwarcie edytora i **zamknij Unity**.

   Dlaczego tak: Hub nie tworzy projektu w niepustym folderze, a szablon Universal 2D ma gotowy URP z 2D Rendererem
   (`Assets/Settings`), którego nie chcemy robić ręcznie.
4. **Przenieś pliki projektu do repo:** z `C:\Projekty\Tmp\Pockland` skopiuj do `C:\Projekty\Pockland` foldery:
   - `Assets` (scal z istniejącym, czyli `_Project` zostaje obok `Scenes`, `Settings` z szablonu),
   - `Packages`,
   - `ProjectSettings`.

   **Nie** kopiuj `Library`, `Temp`, `Logs`, `UserSettings` (są w `.gitignore`). Folder tymczasowy możesz potem usunąć.
5. **Dodaj projekt w Hub:** Projects → Add → Add project from disk → wybierz `C:\Projekty\Pockland`. Otwórz
   (pierwszy import potrwa kilka minut).
6. **Sprawdź ustawienia pod Gita:** Edit → Project Settings → Editor:
   - Version Control → Mode: **Visible Meta Files**,
   - Asset Serialization → Mode: **Force Text**.

   Oba są domyślne w Unity 6, ale warto sprawdzić.
7. Zrób kroki z punktu 3 poniżej (menu „Wyspa”) i naciśnij Play.
   *Fork zamiast GitHub Desktop:* klon przez File → Clone (URL `https://github.com/Eziek/Eziek.git`); gałąź:
   w panelu Remotes → origin dwuklik `claude/unity-project-discussion-rlr9ae`, potem prawy klik → New Branch `main`
   (z zaznaczonym Checkout); commit w widoku Local Changes (Stage All → opis → Commit); push przyciskiem Push
   (gałąź `main` do `origin`, z opcją śledzenia).
8. **Commit i push:** w GitHub Desktop (albo Fork) powinny być zmiany w `Assets/` (z plikami `.meta`), `Packages/` i `ProjectSettings/`,
   a **nie** w `Library/` ani `Temp/`. Wpisz opis (np. „Projekt Unity 6.6, szablon Universal 2D”) → Commit to main →
   **Publish branch**.
9. **Domyślna gałąź:** na GitHubie Settings → Branches → Default branch → `main`.

## 2. (Stary sposób) Kopiowanie do osobnego projektu
Jeśli nie chcesz trzymać projektu w repo: utwórz projekt z szablonu **Universal 2D** i skopiuj do niego folder
`Assets/_Project`. Unity sam zaimportuje pliki, a `PixelArtImporter` ustawi sprite'om PPU 16, filtr Point, brak kompresji
i pivoty.

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

## 4b. UI i font (przygotowane, jeszcze niepodpięte w kodzie)
- Sprite'y UI: `Assets/_Project/Art/Sprites/UI/Kit` (9-slice ustawiony automatycznie) i `UI/Icons`.
- Font: `Assets/_Project/Art/Fonts/WyspaPixel.ttf`. W **Window → TextMeshPro → Font Asset Creator** ustaw:
  - Sampling Point Size **8**, Padding **1**, Render Mode **RASTER**, Atlas **256×256**;
  - Character Set: Custom Characters, wklej ASCII i `ĄĆĘŁŃÓŚŹŻąćęłńóśźż`.

  Szczegóły w GDD §12.9.
- Rośliny nocy są w danych (menu „Wyspa → 2”). Rosną tylko w nocy, poza zasięgiem lamp, także gdy gra jest zamknięta.
  Do testu: przycisk **23:30** w HUD i **Czas x60**.

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
