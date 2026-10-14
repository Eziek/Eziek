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

## 4. Testowanie (HUD debugowy: F1)
- **Nasiona:** kliknij pustą grządkę i wybierz nasiono z koła (kosztuje monety). Kliknięcie dojrzałej rośliny ją zbiera.
- **Wzrost** wymaga aktywności, czyli ruchu myszy lub klawiatury w ostatnich 60 s (cały system, nie tylko okno gry)
  i światła (dzień albo lampa).
- **Czas x60:** minuta gry mija w sekundę. Marchew rośnie 10 minut aktywności, więc z x60 w 10 sekund.
- **12:00 / 19:30 / 23:30:** podgląd pory dnia. W nocy lampy świecą, jeśli jest Esencja.
  **Zegar systemowy** wraca do prawdziwej godziny.
- **Wymuś aktywność:** do testów bez ruszania myszą.
- **Zapis** trafia do `%USERPROFILE%/AppData/LocalLow/<firma>/<projekt>/save.json` (co minutę i przy wyjściu).

## 4a. Etap 2a: waluty, ulepszenia, rytm pracy (test)
Po pobraniu zmian: **Wyspa → 3. Zbuduj scenę prototypu** (dodaje dane ulepszeń i nowe komponenty), potem Play.
- **Ulepszenia** w HUD: Słój Esencji (pojemność 60 → 120 → 240 → 480), Ognik-ogrodnik, Pamięć ziemi, Skrzynia kupca.
  Zakup działa i zapisuje się, a efekty automatyzacji dojdą w Etapie 2b. Szybki test: przycisk **Cheat: +500 monet**.
- **Rytm pracy:**
  - przy **x60** po ok. 25 s aktywności pojawia się „Seria … (+25%)”, a Esencja rośnie szybciej;
  - **przerwa:** zaznacz **„Wymuś bezczynność (przerwa)”** i odczekaj 5–30 s (przy x60 to 5–30 min gry), potem ją odznacz.
    Pojawi się „odświeżony”. Bez przełącznika gra uznaje Cię za nieaktywnego dopiero po **60 prawdziwych sekundach**
    bez ruchu myszy i klawiatury (próg nie przyspiesza przy x60);
  - **zmęczenie** po 8 h pracy w dobie (przy x60 ok. 8 min).
- **Rośliny nocy** dają przy zbiorze **plony nocy** (waluta do ulepszeń). Rosną tylko w ciemności, a w scenie prototypu
  lampy oświetlają 7 z 8 grządek (ciemna jest górny rząd, trzecia od lewej). Do testu zaznacz **„Zgaś lampy”**.
  HUD pokazuje stan grządki pod kursorem: czy jest w świetle, czy rośnie i dlaczego nie.
- Stary zapis gry wczyta się bez utraty monet (zapis v2).

## 4a2. Etap 2b: automatyzacja (test)
Po pobraniu zmian: **Wyspa → 3. Zbuduj scenę prototypu** (skrzynia, ognik, skrzynia kupca, kamień runiczny), potem Play.
1. Posadź kilka roślin, ustaw **x60** i „Wymuś aktywność”.
2. **Cheat** i kup **Ognik-ogrodnik**: obok skrzyni pojawi się ognik. Co 30 s gry (przy x60: 0,5 s) leci do dojrzałego plonu
   i wkłada go do skrzyni. Sprite skrzyni zmienia się wraz z napełnieniem (pusta, połowa, pełna; pojemność 20).
3. **Klik w skrzynię** albo przycisk „Sprzedaj” w HUD debugowym (F1) sprzedaje zawartość po pełnej cenie.
4. **Pamięć ziemi** (kamień runiczny przy grządkach): po zbiorze grządka sama sadzi to samo, jeśli masz monety na nasiono.
5. **Skrzynia kupca**: co godzinę gry (przy x60: minuta) sprzedaje zawartość po 90% ceny i na chwilę się otwiera.
6. Zawartość skrzyni zapisuje się w save.

## 4b. Etap 2c: docelowe UI (test)
Raz na projekt: **Window → TextMeshPro → Import TMP Essential Resources** (bez tego tekst się nie pokaże,
a w konsoli będzie błąd „Brak zasobów TextMeshPro”).

Po pobraniu zmian: **Wyspa → 3. Zbuduj scenę prototypu** (krok 3 sam uruchamia krok 2, który tworzy `Data/UiTheme.asset`), potem Play.
1. **HUD:** u góry monety i Esencja z paskiem, w prawym górnym rogu Księga i opcje, w lewym dolnym rytm pracy i zegar.
   Po 5 s bez ruchu myszy HUD znika, wraca po jej poruszeniu. Inne waluty pokażą się dopiero, gdy je zdobędziesz.
2. **Klik w pustą grządkę** otwiera koło nasion. Najedź na nasiono: dymek z opisem. Na nasiona, na które cię nie stać, nie da się kliknąć.
   Klik obok, PPM albo Esc zamyka koło.
3. **Najedź na grządkę:** dymek mówi, co rośnie, ile procent i dlaczego nie rośnie (brak światła, brak pracy, za jasno dla nocnej).
4. **Księga** (ikona książki): Ulepszenia (kupno, koszt, wymagania), Statystyki, Opcje (język, jasność nocy, rytm pracy).
   Zakup daje toast w prawym górnym rogu. Sklep, Rytuały i Bestiariusz mają na razie „Wkrótce”.
5. **Język:** Opcje → Polski / English / Deutsch. Teksty zmieniają się od razu. 日本語 i 简体中文 pokażą puste znaki,
   dopóki nie dodamy fontu Fusion Pixel.
6. **F1** pokazuje HUD debugowy (cheat, x60, przełączniki testowe). Toasty dają też: sprzedaż skrzyni, skrzynia kupca,
   pełna skrzynia przy ogniku.
7. Pliki tekstów: `Assets/_Project/Localization/*.csv` (UTF-8). Po zmianie pliku wystarczy ponownie Play.

## 4c. Etap 3a: żywa wyspa (test)
Po pobraniu zmian: **Wyspa → 3. Zbuduj scenę prototypu** (krok 2 tworzy materiały w `Data/Materials`), potem Play.
1. **Kołysanie:** rośliny i drzewa bujają się o piksel u góry, skokowo. Sąsiednie rośliny nie ruszają się równo.
2. **Kontur:** najedź na grządkę z rośliną: jasny kontur. Dojrzała roślina pulsuje złotym konturem.
3. **Woda:** pod wodą pływają ciemne ryby (powoli, ósemki), w głębi na dole porusza się coś dużego, po powierzchni
   przesuwają się połyski i grzbiety fal, fale napływają do brzegu, piana przy klifie pulsuje.
4. **Mgła:** przy krawędziach okna, nad morzem. W nocy gęstsza, a gdy zgasisz lampy (F1 → „Zgaś lampy”), podchodzi bliżej
   (zmienia się powoli, kilka sekund).
5. **Noc** (F1 → 23:30): niektóre ryby świecą turkusowo, rzadko błyska oko w głębinie, nad wyspą latają świetliki.
6. **Cząsteczki:** zbiór (roślina wyskakuje, sypie się ziemia, monety lecą do licznika i licznik podskakuje), sadzenie (ziemia),
   nowa faza wzrostu (listki), iskierki nad dojrzałymi, w dzień czasem spada liść z drzewa.
7. **F1 → „Shadery efektów”** wyłącza shadery (zwykłe sprite'y). Jeśli coś jest różowe albo dziwne, sprawdź z wyłączonymi
   i wklej mi błędy z konsoli.

## 5. Build
Unity 6: **File → Build Profiles → Windows → Build**.

## Gdy coś nie działa
| Objaw | Co zrobić |
|---|---|
| Sprite'y rozmyte albo w złym miejscu | **Wyspa → Narzędzia → Reimportuj sprite'y** |
| Wszystko czarne | Brak Global Light 2D albo projekt bez 2D Renderera. Użyj szablonu **Universal 2D** i przebuduj scenę |
| Noc nie ciemnieje, lampy nie świecą, w konsoli „URP używa renderera 3D” | Projekt powstał z szablonu Universal 3D. Napraw: patrz sekcja „Przełączenie na 2D Renderer” niżej |
| Płomienie lamp przyciemnione w nocy | Brak materiału `Data/GlowUnlit.mat` (shader URP Sprite-Unlit-Default). Uruchom ponownie krok 2 |
| Klikanie nie działa | Na scenie musi być `ClickRouter` (obiekt `Game`), a grządki muszą mieć BoxCollider2D |
| Brak napisów w UI, w konsoli „Brak zasobów TextMeshPro” | **Window → TextMeshPro → Import TMP Essential Resources**, potem Play |
| Różowa woda, mgła albo rośliny | Shader się nie skompilował. Kliknij plik w `Assets/_Project/Shaders` i wklej mi błędy z Inspectora. F1 → „Shadery efektów” wyłącza efekty |
| Woda bez ryb i fal, w kolorach „tęczy” | Brak `water_data.png` jako tekstury bez sRGB. **Wyspa → Narzędzia → Reimportuj sprite'y**, potem Wyspa → 3 |
| Brak UI, w konsoli „UiRoot: brak motywu UI” | **Wyspa → 3** (tworzy `Data/UiTheme.asset` i podpina go do obiektu `UI`) |
| W konsoli „[Loc] Brak klucza …” | Klucz nie istnieje w żadnym CSV w `Assets/_Project/Localization/`. Zgłoś mi nazwę klucza |

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

## Przełączenie na 2D Renderer (gdy projekt powstał z szablonu Universal 3D)
1. W oknie **Project** kliknij prawym w `Assets/Settings` → **Create → Rendering → URP Asset (with 2D Renderer)**.
   Powstaną dwa pliki, np. `New Universal Render Pipeline Asset` i `..._Renderer2D`. Nazwij asset `Pockland_URP2D`.
2. **Edit → Project Settings → Graphics → Default Render Pipeline:** przeciągnij `Pockland_URP2D`.
3. **Edit → Project Settings → Quality:** dla **każdego** poziomu jakości (klikasz po kolei w wierszach u góry)
   ustaw **Render Pipeline Asset** na `Pockland_URP2D`.
4. **Wyspa → 1. Ustawienia projektu.** W konsoli ma się pojawić „URP z 2D Rendererem: OK”.
5. **Wyspa → 3. Zbuduj scenę prototypu** jeszcze raz i Play. Przyciski 19:30 / 23:30 w HUD powinny przyciemniać scenę,
   a lampy świecić.
6. Opcjonalnie usuń pliki szablonu 3D, których nie używamy: `Assets/TutorialInfo`, `Assets/Readme.asset`,
   `Assets/Settings/PC_*`, `Mobile_*`, `*VolumeProfile*`, `SampleSceneProfile`.
