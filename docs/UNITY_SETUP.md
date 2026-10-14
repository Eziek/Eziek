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

## 4d. Etap 3b: wyspa z kafelków (test)
Po pobraniu zmian: **Wyspa → 1** (okno startowe 432×336), **Wyspa → 3**, potem Play.
W edytorze ustaw okno Game na **432×336** (lista rozdzielczości u góry okna Game, „+” = własna), żeby widzieć to, co w buildzie.
Okno samo rośnie tylko w buildzie, w edytorze rośnie kadr.
1. **Start:** mała wyspa 2×2 o losowym kształcie: 2 grządki, drzewo/kamień/krzak/martwe drzewo i latarnia. Każdy nowy świat jest inny.
2. **Rozbudowa:** przycisk **„+”** w prawym górnym rogu. Kafelki przy lądzie mają przerywane ramki z ceną, a niektóre **„?”**
   (znalezisko). Najedź: dymek z kosztem (monety + Esencja). **LPM** kupuje, **PPM** albo **Esc** wychodzi.
3. Po zakupie ląd się zmienia, mgła odsuwa się od nowego brzegu, kadr rośnie krokami po 16 px. Znalezisko daje toast
   (monety, plony nocy, odłamek gwiazdy, fragment lore, źródło albo pradawny kamień).
4. **Grządka:** w trybie rozbudowy wolna trawa ma złote przerywane ramki: LPM przekopuje ją na grządkę (10 monet, potem drożej).
5. **Automatyka:** po kupnie ognika skrzynia staje sama na wolnym kafelku (ognik obok). Bez wolnego kafelka zobaczysz toast
   „brak wolnego kafelka”: dokup kafelek, a skrzynia pojawi się sama.
6. **Duża wyspa:** gdy przerośnie limit okna, przeciągnij **PPM**, żeby przesunąć widok.
7. **F1** (HUD debugowy): ziarno, liczba kafelków, cena, kadr; przyciski **+1 / +10 kafelków** i **Nowy świat**,
   cheat **+30 Esencji**.
8. Zapis: wyspa zapisuje się z resztą gry. Stary zapis (sprzed 3b) zachowuje monety i ulepszenia, a wyspę dostaje nową.

## 4e. Etap 3c: sklep, stawianie, usuwanie (test)
Po pobraniu zmian: **Wyspa → 3**, potem Play. Zapis z 3b zostaje (wyspa, lampa startowa wraca na swoje miejsce).
1. **Sklep:** Księga → Sklep → Lampy. **Kup** zamyka Księgę i pokazuje ducha lampy przy kursorze.
   Na wolnym kafelku duch jest jasny, na zajętym czerwony. Przerywany okrąg pokazuje zasięg światła, a dymek mówi, ile grządek
   oświetli. **LPM** stawia (dopiero wtedy płacisz), **PPM** albo **Esc** anuluje.
2. **Obiekty:** klik w kafelek pod lampą, skrzynią, skrzynią kupca albo kamieniem runicznym otwiera panel
   (zasięg i stan lampy, zawartość skrzyni, **Sprzedaj**, **Przenieś**). „Przenieś” działa jak stawianie; ognik leci za skrzynią.
3. **Usuwanie:** klik w kafelek pod drzewem, kamieniem albo krzakiem: panel z kosztem w Esencji i nagrodą, przycisk **Usuń**.
   Drzewo się przewraca i zostaje pień, kamień pęka i zostaje gruz, krzak zostaje ścięty i po chwili znika.
   Pień i gruz też można usunąć. Drewno i kamień lecą do licznika (pojawią się w HUD).
4. **Nowe kafelki:** kupiony kafelek wysuwa się z lądu w 3 klatkach, a na co czwartym rośnie dekoracja.
5. Brak Esencji na usunięcie: przycisk jest wyszarzony (F1 → +30 Esencji do testów).

## 4f. Etap 4: rośliny, kupiec-widmo, morze (test)
Po pobraniu zmian: **Wyspa → 3**, potem Play. Zapis z 3c zostaje (zapis v4 dokłada worek nasion, nawóz i morze).
Przydatne w F1: **x60**, **23:30**, **Wymuś pełnię**, **Mutacje zawsze**, **Przywołaj kupca-widmo**, cheaty.
1. **Jakość:** roślina dnia rosnąca w nocy w świetle klatki z ognikiem (lampa ze sklepu) dostaje jakość 2/3 (dymek nad grządką),
   a zbiór daje x1.5 monet. Nawóz od kupca daje +1 (jakość 3/3 = x2.5).
2. **Mutacje:** posadź dynię i upiorną dynię na sąsiednich grządkach (obok siebie, nie po skosie). Zbiór jednej z nich daje
   3% szansy (F1: zawsze) na nasiono **dyni-upiora**: toast i nowe nasiono w kole nasion z licznikiem.
3. **Pełnia:** F1 → „Wymuś pełnię”, potem 23:30: toast o pełni, rośliny nocy rosną 2x szybciej, krwawa róża rośnie tylko wtedy
   (poza pełnią dymek mówi „czeka na pełnię”).
4. **Kupiec-widmo:** F1 → „Przywołaj kupca-widmo” (normalnie raz na noc, losowo 21:00-3:00). Łódź wynurza się z mgły pod wyspą.
   Klik: paczki nasion nocy (do worka), nawóz, czasem nasiono hybrydy, skup skrzyni za 150%. Po 10 min odpływa.
5. **Pomost:** Księga → Sklep → **Morze** → Pomost (300 monet + 10 drewna; drewno z usuwania drzew). Stawia się na wodzie
   przy brzegu, gdzie ląd jest z lewej albo z prawej. Najedź na rybę blisko pomostu: dymek „LPM: złów”. Złowiona ryba daje łuski
   i monety, świecąca (w nocy) więcej, i wraca po 1,5 min.
6. **Sieci:** Sklep → Morze → Sieci: co godzinę gry łuski (F1 x60 = co minutę).
7. **Łódź:** Sklep → Morze → Łódź. Klik w łódź: krótka (2 h pracy) albo długa (4 h) wyprawa. Liczy się tylko aktywna praca
   (F1: x60 i „Wymuś aktywność”). Po powrocie klik → **Rozładuj**: panel z łupem.

## 4g. Etap 5a: strefy (test)
Po pobraniu zmian: **Wyspa → 3**, potem Play. Strefy leżą 6-12 kafelków od startu, w trzech różnych kierunkach.
Najszybciej: F1 → **+10 kafelków** kilka razy (albo cheat monet i rozbudowa w jedną stronę).
1. **Podłoża:** cmentarz (ciemna trawa, płaty ziemi, kamyki, kosteczki), bagno (kałuże, błoto), ruiny (płyty z fugami, mech).
   Granice są rozmyte, bez widocznych kwadratów. Pierwszy kafelek strefy daje toast „Odkryto: …”.
2. **Dekoracje stref:** nagrobki, krzyże, świece, ciernie / namorzyny, pałki, kłody, lilie / mury, kolumny, palenisko, tablice.
   Świecące (świece, palenisko, tablica, kłoda, lilie) widać w nocy. Klik: „Usuń” jak w 3c (kamień albo drewno,
   nagrobek pęka i zostaje rozbity nagrobek, czasem fragment lore).
3. **Zabytki:** w środku cmentarza krypta, w środku ruin posąg i kamienne schody. Klik: fragment historii, bez „Usuń”.
4. **Bonusy:** grządka przekopana na cmentarzu - rośliny nocy rosną 1.5x szybciej; na bagnie trupi grzyb 1.5x (dymek nad grządką).
5. **Życie stref:** wrona na cmentarzu, żaba i bąble na bagnie (skokowe klatki, losowe przerwy).

## 4h. Etap 5b: nocne zdarzenia, potwór, latarnia (test)
Po pobraniu zmian: **Wyspa → 3**, potem Play. W nocy (F1 → 23:30) zdarzenia przychodzą same; do testu F1: **Ognik / Gwiazda / Potwór**.
1. **Błędny ognik** krąży nad wodą przy brzegu przez minutę: klik = +3 Esencji.
2. **Spadająca gwiazda** przelatuje ukosem i zostawia na lądzie odłamek (świeci, miga przed zniknięciem po 2 min): klik = +1 odłamek.
3. **Potwór**: macki i oko wychodzą z wody pod wyspą. Klik: ofiara 5 plonów nocy = dar z głębin. Po 3 min się zanurza.
4. **Ołtarz** (Sklep → Morze, 500 monet + 10 kamienia; na lądzie przy wodzie): potwór częściej, w nocy przywołanie za 3 plony nocy.
5. **Latarnia morska** (2000 monet + 10 odłamków; na brzegu): świeci jak lampa, mgła odsuwa się, zdarzenia dwa razy częściej.
6. **Pomost:** woda pod deskami i przy łodzi nie ma już ramek rozbudowy. Zapis z zasypanym pomostem: toast o zwrocie kosztów.

## 4i. Etap 5c: rytuały, cykl księżyca, bestiariusz (test)
Po pobraniu zmian: **Wyspa → 3** (dodaje obiekt kroniki do sceny), potem Play.
1. **Księga → Rytuały:** u góry punkty księżyca, dni do nowiu i ile punktów da obecny cykl. Zbieraj plony, łów ryby,
   kupuj kafelki: liczba „cykl da” rośnie (co 10 plonów, 5 plonów nocy, 5 ryb, 3 zdarzenia, 10 kafelków = 1 pkt, maks. 12).
2. F1 → **Zamknij cykl księżyca teraz**: toast „Nów: cykl zamknięty, +N pkt”, liczniki od zera. **+5 pkt księżyca** do testu.
3. **Odpraw** rytuał (punkty + surowiec, np. Ciche lampy: 8 łusek): toast, wiersz pokazuje „Odprawiony”, efekt działa od razu
   i po wczytaniu zapisu (np. Szept mgły: kupiec zostaje dłużej, Pamięć przypływu: krótsze wyprawy).
4. **Księga → Bestiariusz:** Rośliny / Ryby / Zjawy / Zapiski. Nieodkryte to „???”; zebrane plony, złowione ryby, zdarzenia
   (ognik, gwiazda, kupiec, potwór, pełnia) i lore (znaleziska, zabytki) się odkrywają. Pełna strona: przycisk „Odbierz: +N pkt”.
5. Zapiski lore: po 4 na stronę, strzałki na dole.

## 4j. Etap 6a: okno i opcje (test)
Po pobraniu zmian: **Wyspa → 1** (wyłącza „DXGI flip model”, potrzebne do przezroczystego okna). Sceny nie trzeba budować od nowa.
**W edytorze** (Księga → Opcje):
1. **Ogólne:** język, jasność nocy, godziny nocy (np. 22:00 - 6:00; F1 → godzina, żeby sprawdzić), rytm pracy, próg bezczynności.
2. **Okno:** skala ×2/×3/×4 i mini-tryb zmieniają kadr. **Przezroczyste tło** w edytorze tylko chowa mgłę i dalekie morze (podgląd).
   Ramka, wierzch i róg są wyszarzone (działają w buildzie).
3. **System:** Reset zapisu: pierwszy klik zmienia napis na „Na pewno?”, drugi w ciągu 4 s zaczyna grę od nowa. Opcje zostają.

**W buildzie** (File → Build Profiles → Windows → Build):
4. Ramka wył.: okno bez paska tytułu; przeciągnij je LPM za puste miejsce (woda). Puść przy rogu ekranu: przyklei się.
   Kupienie kafelka (okno rośnie) w rogu: okno rośnie od rogu.
5. Zawsze na wierzchu: okno zostaje nad innymi programami.
6. Przezroczyste tło: wyspa z pasem wody przy brzegu na pulpicie; klik w puste miejsce trafia w pulpit/okno pod spodem, w ląd i UI w grę.
   Jeśli zamiast przezroczystości widać czarne tło: sprawdź, czy Wyspa → 1 wyłączyło flip model (Player Settings →
   Resolution and Presentation → „Use DXGI flip model swapchain for D3D11” odznaczone) i czy kamera nie ma HDR w URP Asset.
7. Start z Windows: wpis „Pockland” w Menedżerze zadań → Aplikacje autostartu. Wył. usuwa wpis.

## 4k. Etap 6b: nowe nasiona i automatyka (test)
Po pobraniu zmian: **Wyspa → 2** (nowe rośliny, sprite'y budowli w `IslandArt`), potem **Wyspa → 3** (scena bez starej skrzyni,
ognika i skrzyni kupca: teraz stawia je wyspa). Stary zapis: skrzynia, skrzynia kupca i kamień runiczny wracają jako budowle,
ognik pojawia się sam przy wolnym kafelku.
1. **Księga → Sklep → Nasiona:** 6 roślin z ceną odblokowania. „Odblokuj” → toast, roślina pojawia się w kole nasion.
   F1 → „Odblokuj wszystkie nasiona” do testu. Koło nasion rośnie, gdy nasion jest więcej.
2. **Sklep → Budowle:** skrzynia, ognik, duszek-handlarz, runa. Bez ulepszenia: „Wymaga: …”. Po kupnie tryb stawiania jak lampa;
   ognik i runa pokazują przerywany kwadrat zasięgu i „Obejmie: N grządek”. Każda kolejna sztuka jest droższa („Masz: N”).
3. **Ognik:** zbiera tylko w kwadracie 5x5 wokół siebie i leci z plonem do najbliższej skrzyni. Klik: zasięg i czas do zbioru.
4. **Runa:** klik → „Nasiona” → koło: pamięć ziemi (ikona rytuału), rośliny, „nic” (kłódka). Puste grządki 3x3 wokół
   obsiewają się co 2 s (nasiono z worka albo za monety).
5. **Duszek-handlarz:** co godzinę (F1 → czas x60) leci do najpełniejszej skrzyni i ją sprzedaje. Dwa duszki = dwie skrzynie.
6. **Przezroczyste tło:** wokół pasa wody wraca mgła (pierścień, który rozpływa się w pikselach).

## 4l. Pierścień akcji (test)
Sceny nie trzeba budować od nowa (UI powstaje z kodu). Przycisków w prawym górnym rogu już nie ma.
1. Prawy klik na wyspie (bez przeciągania): koło z czterema ikonami przy kursorze. Najechanie = nazwa, klik = akcja:
   Księga (Ulepszenia), Sklep, Opcje, Rozbudowa.
2. Klik obok koła, PPM albo Esc zamyka. PPM z przeciąganiem dalej przesuwa kamerę (gdy wyspa większa niż okno).
3. PPM w trybie rozbudowy, stawiania, przy otwartym panelu albo kole nasion tylko je zamyka (koło się nie otwiera).
4. Nowa gra (Opcje → System → Reset): po 3 s toast „Prawy klik na wyspie: …”, powtarza się co 3 min, aż użyjesz koła.
5. Przezroczyste okno: PPM na lądzie otwiera koło; na pulpicie obok wyspy klik idzie do pulpitu.

## 4m. Poprawki po teście: zasypywanie, wyjście, powiększenie, koło nasion, tempo pracy
Po pobraniu: **Wyspa → 2** (nowe ikony: słońce, księżyc, wyjście, łopata w motywie UI), potem Play.
1. PPM na grządce: w pierścieniu jest łopata „Zasyp grządkę” (dymek ostrzega, że roślina przepadnie). Klik = z powrotem trawa.
2. Klik w budowlę (lampa, skrzynia, ognik, duszek, runa, ołtarz) → „Rozbierz” → „Na pewno rozebrać?” → wraca połowa monet.
3. Pierścień → drzwi „Wyjdź z gry” (w edytorze zatrzymuje Play). Też Opcje → System → Wyjdź.
4. Opcje → Okno → Powiększenie x2..x6; Ctrl + kółko myszy robi to samo.
5. Koło nasion: tylko nasiona dnia albo nocy; środek koła (słońce/księżyc) przełącza. Runa siewu ma to samo.
6. Tempo pracy: pisz albo ruszaj myszą przez minutę: w HUD obok zegara „Tempo x1.3”, w dymku grządki dnia „Tempo pracy”.
   F1 → „Tempo: pełne” wymusza x1.5 do testu.
7. Przezroczyste tło: mgła tylko nad pasem wody, gęstnieje ku jego krawędzi; dalej czysty pulpit.

## 4n. Fonty japońskie i chińskie (test)
Po pobraniu: **Wyspa → 2** (motyw UI dostaje fonty Fusion Pixel z `Art/Fonts/FusionPixel/`), potem Play.
1. Opcje → Ogólne: przyciski „日本語” i „简体中文” pokazują znaki zamiast kwadracików.
2. Przełącz na 日本語 i 简体中文: teksty w HUD, Księdze, dymkach są czytelne (piksele 8 px, bez rozmycia).
3. W `Player.log` nie ma już „The character … was not found in the font asset”.

## 4o. Upiększanie: światło, odbicia na wodzie, płynne duszki (test)
Po pobraniu: **Wyspa → 2** (materiał `LightShafts`, nowe klatki ognika i duszka bez podskoku), potem **Wyspa → 3**
(obiekt `LightShafts` w scenie). Jeśli sprite'y wyglądają źle: **Wyspa → Narzędzia → Reimportuj sprite'y**.
1. Dzień: ukośne, ciepłe snopy światła przez wyspę i wodę (pikselowe, powoli dryfują). Zmierzch: pomarańczowe.
2. Noc (F1 → 23:30): blade promienie księżyca (w pełnię wyraźniejsze, F1 → pełnia), wokół zapalonych lamp poświata w pikselach.
3. Woda: iskierki odbić; w dzień po całej wodzie, w nocy tylko przy lampach (w ich kolorze).
4. Ognik: płynny lot po łuku; zbiera do 3 dojrzałych plonów (widać je pod nim), potem leci do skrzyni i wraca.
   Klik w ognika w locie: „W locie: niesie N”. Duszek-handlarz też lata płynnie, a oba unoszą się w miejscu.
5. F1 → „Shadery efektów” wyłącza też światło (porównanie przed/po).
6. Bez Wyspa → 3 obiekt `LightShafts` tworzy się sam przy starcie (wystarczy Wyspa → 2 dla materiału).
7. Płynność: Opcje → Ogólne → Płynność (Eko / Płynna / Max). Rośliny kołyszą się, ryby pływają, cząsteczki i monety lecą płynnie.
8. Przezroczyste okno w nocy: mgła nad pasem wody jaśniejsza (blada poświata), za wodą rzadkie kłęby; ogniki i runy świecą poświatą.

## 4p. Światło Light 2D, miękka mgła, wiatr, drzewa owocowe (test)
Po pobraniu: **Wyspa → 2** (sprite'y drzew w `IslandArt`), potem Play (scena nie wymaga przebudowy; obiekt snopów tworzy się sam).
Stary materiał `Data/Materials/LightShafts.mat` można usunąć (shader nakładkowy usunięty).
1. Dzień: ukośne, ciepłe snopy światła rozjaśniają wyspę i widać je w powietrzu (wolumetria Light 2D). Noc: blade promienie
   księżyca (w pełnię wyraźniejsze). Lampy mają poświatę w powietrzu, ogniki/duszki/runy świecą w nocy.
   Przezroczyste okno: snopy tylko oświetlają wyspę (bez poświaty nad pulpitem).
2. Mgła w przezroczystym oknie: miękka, półprzezroczysta otoczka wokół wody, kłęby rozpływają się nad pulpitem.
3. Co kilka sekund przez wyspę przelatują smugi wiatru; w dzień w powietrzu unoszą się pyłki, w nocy turkusowe zarodniki.
4. Księga → Sklep → **Drzewa**: kup jabłoń i postaw ją jak lampę. Sadzonka rośnie od pracy (F1 → czas x60 albo
   „Drzewa: dorosłe z owocami”). Owocujące drzewo: klik = zbiór (toast, monety). Klik w drzewo bez owoców: panel
   ze stanem i „Zetnij” (drugi klik potwierdza): drzewo się przewraca, dostajesz drewno.
5. Upiorna grusza / drzewo księżyca z owocami w nocy świecą i oświetlają grządki w zasięgu (rośliny dnia rosną jak przy lampie).
6. Zapis: drzewa (wzrost, owoce) wracają po ponownym uruchomieniu.

## 4q. Mgła dalej od wyspy, ładniejsze drzewa, ptaki (test)
Po pobraniu: **Wyspa → 2** (materiał `SwayTreeGlow`, ikony owoców i ptaki w `IslandArt`), potem Play.
1. Przezroczyste okno: nad pasem wody czysto, dalej osobne, miękkie kłęby mgły z prześwitami; okno ma większy zapas.
2. Podmuch wiatru (smugi): rośliny i drzewa pochylają się z wiatrem i wracają.
3. Drzewa kołyszą się każde w swoim rytmie; świecące owoce gruszy/drzewa księżyca kołyszą się razem z drzewem.
4. Zbiór owoców: owoce spadają z korony na ziemię, drzewo lekko się otrząsa (bez skakania). Nowa faza: sprężyste pyknięcie.
5. Ścinanie: trzy uderzenia z liśćmi, potem przewracanie przyspiesza, na końcu deszcz liści.
6. Ptaki: w dzień co 25-70 s przylatuje wrona albo ptaszek, siada na drzewie albo na trawie, dziobie, na ziemi podskakuje,
   po chwili odlatuje. Najedź kursorem blisko: zrywa się do lotu. W nocy ptaków nie ma.
7. Skrzynie na plony: postaw dwie obok siebie. Stykają się bokami, nie nachodzą na siebie (sprite ma teraz 16 px).
   Jeśli skrzynia stoi krzywo albo jest rozmyta: **Wyspa → Narzędzia → Reimportuj sprite'y**.

## 4r. Etap 6d: pasek HUD, tablica ogłoszeń, balans, owoce, duszek przy skrzyni (test)
Po pobraniu: **Wyspa → 2** (tablica, strzałka, nowe ceny nasion i ognika w danych), potem Play. Wprowadzenie widać tylko na
nowej wyspie: Opcje → System → reset zapisu (albo usuń `save.json`). Jeśli sprite'y są rozmyte: **Wyspa → Narzędzia → Reimportuj sprite'y**.
1. HUD: jeden pasek tuż nad wyspą (waluty i Esencja u góry, rytm, tempo i zegar pod spodem). Chowa się po 5 s bez ruchu myszy.
   Przy przewiniętej dużej wyspie zostaje u góry okna. Toasty w prawym górnym rogu.
2. Nowa wyspa: na czwartym kafelku stoi tablica z karteczką, pod wyspą karteczka „1/5”, złota strzałka nad pustą grządką.
   Posadź → karteczka odpada, nowa: „pracuj”, potem „zbierz”, „kup lampę”, „dokup kafelek”, na końcu „gotowe”.
   Klik w tablicę: treść i „Rozbierz” (drugi klik potwierdza, +3 drewna, wprowadzenie znika).
3. Ognik i drzewo: owocujące drzewo w zasięgu ognika (5x5). Ognik podlatuje, dwa razy wpada w koronę, owoce spadają pod drzewo,
   ognik zbiera je z ziemi i niesie do skrzyni. Klik w drzewo z owocami na ziemi: zbierasz je sam.
4. Duszek-handlarz: unosi się obok skrzyni. Gdy skrzynia dojdzie do 20/20, od razu sprzedaje (monety lecą). Sklep → Budowle →
   duszek: kliknij skrzynię bez duszka. Przeniesienie skrzyni zabiera duszka.
5. Przezroczyste okno: mgła gęsta, przyklejona do pasa wody (bez przerwy), z postrzępioną krawędzią.
6. Wiatr: przy smugach wiatru rośliny pochylają się po kolei z lewej na prawą, czubki gną się mocniej, potem wracają
   z małym odbiciem.
7. Ceny: kafelki droższe (np. 10. kafelek 140 monet + 5 Esencji), ognik 100 monet, czosnek 100, piołun 600.

## 4s. Cykl gry 1 h, wzrost x3, opisy w sklepie (test)
Po pobraniu: **Wyspa → 2** (nowe czasy wzrostu i ceny w danych roślin i ulepszeń), potem Play.
1. Zegar w pasku HUD idzie szybko: 06:00-20:00 przez 40 min, potem noc 20:00-06:00 przez 20 min. Zmierzch ok. 3 min przed nocą.
   F1 → 12:00 / 19:30 / 23:30 dalej przestawia porę (do testów).
2. Marchew dojrzewa w ok. 3 min pracy (z tempem szybciej). Rośliny nocy: trupi grzyb w jedną noc (20 min ciemności).
3. Kupiec-widmo: nie każdej nocy (ok. co trzecią), w pierwszej połowie nocy.
4. Księga → Rytuały: „Nów za N nocy” (cykl księżyca 8 nocy). Pełnia: F1 pokazuje fazę.
5. Sklep → Morze: długi opis latarni morskiej kończy się „...” przed przyciskiem, koszt widać cały; najechanie pokazuje całość.
6. Opcje → Ogólne: nie ma już „Godziny nocy”.

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
| W buildzie brak UI, w `Player.log` „ArgumentNullException … shader” przy `UiRoot.CreateFont` | Build wyciął shader fontu TMP. **Wyspa → 1** (dopisuje `TextMeshPro/Mobile/Bitmap` do Always Included Shaders), potem zbuduj ponownie. Log buildu: `%USERPROFILE%\AppData\LocalLow\<firma>\<gra>\Player.log` |
| Brak UI, w konsoli „UiRoot: brak motywu UI” | **Wyspa → 3** (tworzy `Data/UiTheme.asset` i podpina go do obiektu `UI`) |
| Księga nie mieści się w oknie | W buildzie okno samo rośnie na czas Księgi; w edytorze UI się zmniejsza. Ustaw większe okno Game |
| Brak wyspy, w konsoli NullReference w `Island` | Scena sprzed 3b: uruchom **Wyspa → 3** (tworzy `Data/IslandArt.asset` i nową scenę) |
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
