# GDD: Pockland

> **Status:** Etap 1: szkielet systemów w C# (`Assets/_Project/`) gotowy do skopiowania do Unity,
> instrukcja w `docs/UNITY_SETUP.md`. Prototyp wizualny: `tools/pixel-proto/` i `docs/img/`.
> **Ostatnia aktualizacja:** 2026-10-13

---

## 1. Koncept

Mała gra **idle w okienku na pulpicie**, w klimacie **dark fantasy**. Gracz ma małą wyspę na morzu,
sadzi na niej rośliny i czeka, aż urosną. **Rośliny dnia rosną tylko wtedy, gdy gracz pracuje przy komputerze**
(wykrywana aktywność myszy i klawiatury). Plony się zbiera i sprzedaje, a za monety kupuje nasiona, lampy,
ulepszenia i powiększenie wyspy.

**Wyróżniki:**
- Nagroda za coś, co gracz i tak robi: pracę przy komputerze.
- **Cykl gry** steruje porą dnia (od Etapu 6d+, zamiast zegara komputera): doba trwa 1 h, 40 min dnia i 20 min nocy, i idzie
  także offline. W nocy na wyspie robi się ciemno i trzeba ją oświetlić lampami.
- Lampy zasila **Esencja**, czyli energia zdobywana pracą.
- Dwa typy roślin: **dnia** (rosną od aktywności, w świetle) i **nocy** (rosną w ciemności, także gdy gracz śpi).

**Bez postaci.** Nie ma bohatera ani animacji klatkowych postaci. Życie świata dają shadery, cząsteczki i
stworzenia w tle (ryby, coś w głębinie, ogniki).

Inspiracje gatunkowe: Rusty's Retirement (farma przy krawędzi ekranu), desktopowe idlery ze Steama.

---

## 2. Platforma i technika

| Temat | Decyzja |
|---|---|
| Silnik | **Unity 6.6 (`6000.6.5f1`)**, szablon **Universal 2D** (URP z 2D Rendererem), Shader Graph, Input System |
| Platforma | **Windows** (na start) |
| Wykrywanie aktywności | `GetLastInputInfo` (user32.dll), czyli czas od ostatniego inputu w całym systemie. Bez globalnych hooków, więc antywirusy nie reagują. Gracz jest „aktywny”, gdy od ostatniego inputu minęło < ~60 s |
| Okno | Zwykłe małe okno albo (opcje, Etap 6a) bez ramki / zawsze na wierzchu / w rogu ekranu / przezroczyste. **Własny kod WinAPI** (`Scripts/Platform/`), bez paczki UniWindowController |
| Praca w tle | `Application.runInBackground = true` |
| Wydajność (krytyczna, gra działa godzinami) | **Zmiana po teście (§9, 42):** opcja Płynność, domyślnie 60/30 kl./s, ruch płynny. Pierwotnie: animacje skokowe ok. **10 kl./s**, więc `targetFrameRate` 10–15 w tle i ~30 przy hoverze. `vSyncCount = 0`. Rozważyć `OnDemandRendering`. Symulacja w tickach (np. co 1 s), nie w każdym `Update` |
| Batching | Jeden materiał dla roślin. Faza kołysania liczona z pozycji w świecie, a nie z właściwości per-renderer |
| Zapis | JSON w `Application.persistentDataPath`, autosave co ~1 min i przy wyjściu |
| Postęp offline | Przy starcie liczymy, ile **nocnych godzin** minęło od zamknięcia, i doliczamy wzrost roślin nocy. Rośliny dnia offline **nie** rosną |
| Czas | `System.DateTime.Now`. Opcjonalnie później prawdziwy wschód i zachód słońca (z szerokości geograficznej) albo godziny ustawiane w opcjach |
| Faza księżyca | Z cyklu gry: 8 nocy od nowiu do nowiu, pełnia co ósmą noc (bonus, patrz zdarzenia) |

**Architektura (zaimplementowana w Etapie 1, namespace `Wyspa.*`, szczegóły w §10):**
- `ActivityTracker`: co 1 s sprawdza `SystemIdle`, emituje „aktywne sekundy”
- `EssenceStore`: Esencja (przychód z aktywności, zużycie przez lampy, pojemność)
- `DayNightClock`: pora dnia z zegara, gradient koloru otoczenia, czy jest noc
- `CropDefinition` (ScriptableObject): typ (dzień/noc), czas wzrostu, fazy (sprite'y), ceny
- `FarmPlot` / `IslandGrid`: pola i stan roślin (dane w czystych klasach C#, osobno od MonoBehaviour)
- `LampDefinition` (SO) + `Lamp`: zasięg, zużycie, kolor światła, efekt
- `LightField`: które pola są oświetlone (z lamp i pory dnia)
- `NightEventScheduler`: zdarzenia nocne
- `Economy` / `Inventory`, `SaveSystem`

---

## 3. Pętla rozgrywki

```
Praca przy komputerze ──► Esencja ──► Lampy (noc) ──► rośliny dnia rosną też w nocy
        │                                   │
        ▼                                   ▼ (cień poza lampami)
Rośliny dnia rosną ──► Zbiór ──► Monety ──► Nasiona / Lampy / Słój / Wyspa
                                                   ▲
Noc / sen ──► Rośliny nocy ──► Rzadkie surowce ────┘
Mgła ──► Nocne zdarzenia ──► Bonusy (Esencja, nasiona, odłamki)
```

- **Gracz dzienny:** rośliny dnia rosną od pracy, a nocne same, kiedy śpi (postęp offline).
- **Nocny marek:** lampy pozwalają roślinom dnia rosnąć w nocy, **ale gdzie jest światło, tam nocne nie rosną**.
  Decyzja, które grządki oświetlić, to główny wybór taktyczny.

---

## 4. Systemy

### 4.1 Esencja (energia z pracy)
Nazwa robocza: „Esencja” (alternatywy: Żar, Dusza).
- **+1 Esencji za każdą aktywną minutę.** Ta sama aktywność napędza też wzrost roślin dnia.
- **Tempo pracy** (prośba gracza): im więcej piszesz i ruszasz myszą, tym szybciej rosną rośliny dnia, do **x1.5**.
  Bez globalnych hooków: co 0.2 s porównujemy znacznik ostatniego inputu (`GetLastInputInfo`) i pozycję kursora; input bez
  ruchu kursora liczy się jako pisanie (waga 1), sam ruch myszy waga 0.35. Średnia z ostatnich 15 s, pełne tempo przy
  połowie próbek z inputem. Mnożnik widać w HUD („Tempo x1.3”) i w dymku grządki.
- **Zbiornik („słój”):** pojemność na start ok. **60**. Nadmiar przepada. Ulepszenie słoja zwiększa pojemność.
- UI: pasek w prawym górnym rogu (w prototypie jeszcze z ikoną zegara, docelowo słój z płomieniem).
- Esencję zużywają **lampy, tylko w nocy**.
- Gdy Esencja się skończy, lampy gasną jedna po drugiej (od najdroższej), a ciemność i mgła podchodzą bliżej.
  **Bez kar**, to tylko sygnał wizualny.

### 4.2 Rośliny

| | Rośliny dnia | Rośliny nocy |
|---|---|---|
| Przykłady | marchew, zboże, dynia | księżycowy kwiat, upiorna dynia, trupi grzyb, krwawa róża (tylko w pełnię) |
| Rosną, gdy | gracz jest **aktywny** i pole jest **oświetlone** (dzień albo lampa) | jest **noc** i pole jest **w ciemności** (poza zasięgiem lamp) |
| Wymagają aktywności | tak | **nie** (rosną też offline) |
| Charakter | szybkie, tanie, stały dochód | wolne, rzadkie, cenne, surowce do ulepszeń |

- Każda roślina ma **4 fazy**: nasiono, kiełek, rośnie, dojrzała.
- **Sylwetki muszą się różnić** (uwaga z review): marchew to szeroki wachlarz pierzastych liści i pomarańczowa
  główka, a zboże to pionowe wąskie źdźbła, potem zielone kłosy, potem złote kłosy z **ościami**.
- Zmiana fazy ma „pop”: squash & stretch plus wybuch listków (nagroda za pracę).
- Dojrzała roślina: iskierki i lekki puls (pokazuje „gotowe do zbioru”).

### 4.3 Lampy
Każda ma inny kolor światła („strefy nastroju”):

| Lampa | Światło | Zużycie (wstępne) | Zasięg | Uwagi |
|---|---|---|---|---|
| Krzywa latarnia (wygięty słup z zawijasem) | ciepłe, pomarańczowe | 1 / 10 min | duży | podstawowa |
| Czaszka ze świecą (oczy żarzą się w nocy) | ciepłe | 1 / 20 min | mały | tania |
| Klatka z błędnym ognikiem | **zimne, turkusowe** | 1 / 5 min | duży | przyspiesza wzrost w swoim świetle. Ulepszanie: świecące ryby |
| Świecące grzyby | fioletowe | 0 | bardzo mały | rosną same, słabe światło |

Wszystkie liczby są do balansowania.

### 4.4 Pora dnia i oświetlenie
- **Cykl gry** (`GameClock`, Etap 6d+): doba gry trwa 1 h liczoną od stałej daty w UTC (więc idzie też offline): 40 min dnia
  (06:00-20:00 na zegarze gry w HUD) i 20 min nocy (20:00-06:00), zmierzch to ostatnia godzina zegara przed 20:00 (ok. 3 min).
  Każdy cykl to osobna „doba gry”, więc zdarzenia „raz na noc” (kupiec, potwór, ołtarz) są raz na cykl. Opcji godzin nocy już nie ma.
- Pora dnia wyznacza **kolor otoczenia**: dzień bez zmian, zmierzch ciepło-fioletowy, noc ciemnogranatowa
  (w Unity `Gradient` / LUT, Global Light 2D).
- **Światło lamp ma 3 pasma z ditheringiem Bayera**, a nie gładki gradient, żeby zachować pikselowy charakter
  (własny shader kwantyzujący światło albo post-process).
- **Piksele emisyjne** (płomienie, ognik, oczy czaszki, grzyby, świetliki, świecące ryby) zawsze świecą
  pełną jasnością (osobna maska emisji przy sprite'ach).
- Płomienie lekko migoczą (promień światła „oddycha”).
- **UI nie podlega oświetleniu.**
- **Uwaga UX:** gra wisi na pulpicie obok jasnych aplikacji, więc noc nie może być „czarną dziurą”.
  Trzymać średnie tony, a w opcjach dać **suwak jasności** (przesuwa LUT).

### 4.5 Woda (warstwy)
1. **Głębokość:** płycizna przy brzegu, środek, ciemna głębia przy krawędziach okna (dithering między strefami).
   W Unity: statyczna tekstura mapy odległości od brzegu (distance field), przeliczana po powiększeniu wyspy.
2. **Pod wodą:** ryby jako ciemne sylwetki (2 odcienie ciemniejsze od wody nad nimi), pływają po ósemkach i machają ogonem.
   W nocy niektóre są **bioluminescencyjne** (turkusowy piksel emisyjny).
3. **Głębina:** coś dużego (długie falujące cielsko) ledwo widoczne w rogu. W nocy błyska okiem.
   Opcjonalnie bąbelki na powierzchni.
4. **Powierzchnia:** grzbiety fal z cyklem (narasta, szczyt, łamie się w pianę), dryfujące połyski.
5. **Przybój:** fale napływają do brzegu, pulsująca piana przy klifie, cień pod klifem.

### 4.6 Mgła
- **Tylko poza wyspą, na krawędziach okna, nad morzem.** Pasma dryfują (szum z ditheringiem).
- W dzień rzadsza, w nocy gęstsza. Kiedy gasną lampy, podchodzi bliżej.
- Powiększenie wyspy odsłania nowy ląd i odsuwa mgłę.
- Z mgły przychodzą zdarzenia nocne.

### 4.7 Zdarzenia nocne
**Zasada: nagradzają, nie karzą.** Przegapienie oznacza tylko brak bonusu.
- **Kupiec-widmo:** łódź z mgły raz na noc, o losowej porze, czeka ok. 10 min. Sprzedaje nasiona nocne i skupuje plony drożej.
- **Błędne ogniki:** kliknięcie daje +Esencję.
- **Spadająca gwiazda:** kliknięcie daje odłamek gwiazdy (rzadki surowiec do lamp).
- **Pełnia księżyca** (co ósma noc cyklu gry; wcześniej prawdziwa faza): rośliny nocy rosną 2× szybciej, krwawe róże kwitną tylko wtedy.
- **Wynurzenie potwora:** rzadko. Prosi o ofiarę (np. 5 dyń) i daje coś unikalnego.
- **Świecące ryby:** tylko w nocy, do złowienia, składnik do klatki z ognikiem.

### 4.8 Ekonomia i ulepszenia (szkic, pełna wersja w §11)
- Waluta: monety (ze sprzedaży plonów).
- Zakupy: nasiona, lampy, słój Esencji (pojemność), **powiększenie wyspy** (nowe pola i odsunięcie mgły).
- Surowce rzadkie: plony nocne, odłamki gwiazd, świecące ryby, dary potwora.
- Pomysł do rozważenia: **łowienie** (kliknięcie w rybę przy brzegu) jako mikrointerakcja.

---

## 5. Styl graficzny (art bible)

**Kierunek: dark fantasy, stonowany, mroczny, ale czytelny (średnie tony).**
Gracz odrzucił wersję „cukierkową”.

### 5.1 Referencje gracza (przeanalizowane)
- Lis-wędrowiec, „chlebowa kaczka”, pszczoła, gołębie (szaro-lawendowe) oraz paleta „Luca Davincci”:
  `#477495 #63A3B4 #92ABB1 #CC6058 #8B4263 #51395B`.
- Wspólne cechy: **gruby, prawie czarny, ciepły kontur** (nigdy czysty czarny), **mocny hue-shift**
  (cienie w stronę czerwieni i śliwki, światła w stronę złota), **krępe, okrągłe sylwetki**, płaskie tło z prostymi kępkami
  trawy „∩∩”, **miękki cień-elipsa** w ciemniejszym tonie podłoża, wewnętrzne linie konturu (np. linia skrzydła),
  małe kolorowe akcenty (oko, łapki).
- Rozmiary w referencjach: kaczka ok. **23×21 px**, gołąb ok. **43×36 px** (na płótnie 48–128 px).
  „128p” z rozmowy to rozmiar **płótna**, a nie obiektu.

### 5.2 Zasady
1. **Paleta ograniczona**, wszystko tylko z niej (patrz 5.3). Paleta jest podmienialna, a pora dnia i jasność to LUT.
2. **Kontur** `#0f0a10` wokół całej sylwetki obiektu. Elementy terenu (grządki, kopczyki) mają kontur w ciemnym odcieniu ziemi.
3. **Światło z lewej-góry.** Cieniowanie rampą 3–4 tonów z hue-shiftem.
4. **Widok 3/4 z góry.** Wyspa ma widoczny klif (warstwy: trawa, ziemia, śliwka).
5. **Bez antyaliasingu, tylko skalowanie całkowite** (×2, ×3), Pixel Perfect Camera.
6. **Ruch zawsze o całe piksele** (shadery przesuwają UV o całe teksele i/lub Pixel Perfect Camera z Upscale RT).
   Sprite'y, które się kołyszą, mają 1–2 px przezroczystego marginesu.
7. **Animacje skokowe ok. 10 kl./s** (`floor(_Time.y * 10) / 10`).
8. **Jedyne nasycone, ciepłe kolory to nagrody i światło:** dojrzałe plony, monety, płomienie, ogniki, świetliki.
9. Cząsteczki: kwadratowe 1×1 i 2×2, filtr Point, kolory z palety, bez płynnego skalowania.

### 5.3 Paleta (wersja mroczna, aktualna)
| Klucz | Rola | Kolory |
|---|---|---|
| `o` | kontur | `#0f0a10` |
| `p0 p1 p2` | śliwka (cienie, klif, noc) | `#1c1520 #33263a #563447` |
| `co or go cr` | ciepłe (plony, monety, kość) | `#a24d48 #b4673f #c49a55 #d8ccab` |
| `e0..e3` | ziemia, drewno | `#20171c #352629 #523b35 #715444` |
| `g0..g3` | zieleń (oliwkowy teal) | `#141f1c #21342c #344a3b #566d52` |
| `b0..b3 fo` | woda, mgła | `#121c27 #1f3044 #30485f #56697a #848e9b` |
| `fl fx` | płomień (emisja) | `#ffe08a #f08a3a` |
| `wi wl` | ognik, świecące ryby (emisja) | `#5fcfb8 #d2fff0` |
| `mu ml` | świecące grzyby | `#7a4f9a #d49af0` |
| `g4 ro` | drzewa: nasłonecznione liście, odblask owoców (dodane na prośbę: więcej koloru) | `#7a9564 #d9725a` |

Wcześniejsza jasna paleta („cukierkowa”) została odrzucona. Jest w `tools/pixel-proto/sprites.py` (`PAL`) jako baza
kluczy, a wersję mroczną nakłada `run_dark_palette.py`.

### 5.4 Rozdzielczość: decyzja OTWARTA
- Prototyp: **siatka 16 px** (rośliny ok. 18×24, drzewa ok. 32×40), kadr 272×200 (×3 = 816×600).
- Test **32 px** (rośliny ok. 32–40, drzewo ok. 64–80): poziom detalu jak gołąb z referencji. Patrz `docs/img/porownanie_16_vs_32.png`.
- Rekomendacja Claude: **32 px**. Obiekty 128 px odradzane (okno byłoby ogromne, a 4× więcej pracy na asset).
- Koszt 32 px: przy ×2 okno z całą wyspą to ok. 800×560. Pomysł: start z **małą wyspą** (ok. 640×400), a powiększenie
  wyspy rozszerza okno albo pozwala je przewijać.
- Assety finalne przy 32 px najlepiej rysować ręcznie (Aseprite). Sprite'y generowane kodem nadają się na placeholdery.

### 5.5 Animacje: shadery i cząsteczki
- **Shader:** kołysanie roślin i drzew (podstawa stoi, siła z `UV.y`), woda (patrz 4.5), kontur lub puls „gotowe”,
  podświetlenie przy hoverze, dissolve, oświetlenie pasmowe, LUT pory dnia.
- **Cząsteczki:** zbiór (plon wyskakuje, sypie się ziemia, monety lecą łukiem do licznika, który podbija),
  „pop” wzrostu (listki), iskierki nad dojrzałymi, spadające liście, świetliki, ogniki.

---

## 6. Lista assetów (stan prototypu)
Wszystkie są w siatce 16 px, w mrocznej palecie, wyeksportowane do `Assets/_Project/Art/`. Arkusze podglądowe są w `docs/img/`.
- **Rośliny dnia:** marchew, zboże, dynia (4 fazy). Marchew i zboże mają też test 32 px.
- **Rośliny nocy:** księżycowy kwiat, upiorna dynia, trupi grzyb, krwawa róża (4 fazy, nasiono nocne).
  **Hybryda:** dynia-upiór. `arkusz_rosliny_nocy.png`
- **Lampy:** krzywa latarnia, czaszka ze świecą, klatka z ognikiem, świecące grzyby (+ `_glow`).
- **Automatyka** (`arkusz_automation.png`): ognik-ogrodnik (4 klatki), skrzynia na plony (pusta, połowa, pełna),
  skrzynia kupca (zamknięta, otwarta), kamień runiczny (pamięć ziemi), kompostownik, słoje Esencji S/M/L.
- **Morze** (`arkusz_sea.png`): pomost, łódź z latarenką, boja z siecią, latarnia morska, ołtarz ofiarny, kupiec-widmo w łodzi.
- **Strefy** (`arkusz_zones.png`): nagrobki ×3, żelazny płot, trzciny, bagienne oczko, złamana kolumna, ruiny łuku, martwe drzewo.
- **Zdarzenia i cząsteczki** (`arkusz_events.png`): oko potwora (3 klatki), macka (3 klatki), spadająca gwiazda (3 klatki),
  odłamek gwiazdy, ćma (2 klatki), bąbelek.
- **Obiekty bazowe:** okrągłe drzewo, świerk, krzak, kamień, skrzynia, grządka, wyspa, woda.
- **UI** (`ui_zestaw.png`): font 3×5 z polskimi znakami (`WyspaPixel.ttf`), 32 ikony (waluty, menu, statusy, gwiazdki jakości,
  paczki nasion ×8), elementy 9-slice (panel, strona pergaminu, przyciski ×4, zakładki ×2, sloty ×3, tooltip, toast,
  pasek + 3 wypełnienia), koło nasion, kursor.
- Piksele emisyjne (płomienie, ogniki, oczy) są wycięte do osobnych plików `*_glow.png`, renderowanych na materiale Unlit.
- **Strefy** (`arkusz_strefy.png`), dla każdej 3 kafelki podłoża i dekoracje:
  - cmentarz: obelisk, drewniany krzyż, otwarty grób, krypta, stos czaszek, świece, żelazna brama, ciernie, wrona (2 klatki);
  - bagno: namorzyn, lilie wodne, spróchniała kłoda z grzybami, pałki, bąble (3 klatki), żaba (2 klatki);
  - ruiny: zburzony mur, przewrócona kolumna, posąg w kapturze, kamienne schody, gruz, palenisko, tablica runiczna, kolumna z pnączem.
- **Usuwanie dekoracji** (`arkusz_usuwanie.png`): przewracanie drzewa (4 klatki), pnie ×3, pęknięty kamień, gruz, rozbity nagrobek,
  ścięty krzak, cząsteczki (wióry, liść, odprysk, kurz ×3), ikony drewna i kamienia.
- **Drzewa owocowe** (`arkusz_drzewa.png`, `sad_dzien_noc.png`): jabłoń, czarny bez, upiorna grusza, krwawa jarzębina,
  drzewo księżyca. Każde ma 4 fazy (sadzonka, młode, dorosłe, owocujące), przewracanie dorosłego (4 klatki) i ikonę owocu.
  Do tego ikona sadzonki. Owoce gruszy i drzewa księżyca świecą (`_glow`).
- **Brakuje:** sprite'y faz hybryd innych niż dynia, docelowe assety 32 px (jeśli wybierzemy 32), ikony nowych ulepszeń
  w miarę projektowania, animacje wynurzenia potwora w pełnej sekwencji.

## 7. Otwarte decyzje
- [ ] Siatka 16 px czy 32 px (rekomendacja: 32).
- [x] Nazwa gry: **Pockland**. Do sprawdzenia: zbieżność z „Pokéland” (§16.5).
- [ ] Nazwa energii (Esencja / Żar / Dusza).
- [x] Wersja Unity: **6.6 (6000.6.5f1)**, zmienione z 6.3 LTS na prośbę gracza (Hub poleca 6.6, instalacja jednym kliknięciem).
- [x] **Zamówienia na tablicy ogłoszeń** (wybór gracza): po wprowadzeniu tablica zostaje; co 2 cykle gry nowa karteczka
  (maks. 3) od kogoś z mgły (Zielarka z mgły, Grabarz, Latarnik, Wiedźma z bagien, Mnich z ruin, Ślepy rybak, bez postaci
  na ekranie): zebrać 1-2 rodzaje plonów lub owoców w terminie 4-7 cykli. Postęp liczy się sam ze zbiorów (ręcznych i ogników),
  zbiór dalej daje monety. Nagroda: monety (1.8x wartości) + bonus (odłamek 15%, nawóz, nasiona nocy, drewno, kamień).
  Przeterminowana karteczka znika bez kary. Bez tablicy zamówienia czekają; tablicę można postawić w Sklep → Budowle
  (40 monet + 3 drewna). `Orders/OrderBook.cs`, zapis v9.
- [x] **Wskaźnik pracy** (prośba gracza): w pasku HUD ikonki klawiatury i myszy zapalają się, gdy gra widzi input.
  Tempo pracy liczone z ostatnich **15 s** (wcześniej 60 s), więc reaguje szybciej.
- [x] **Mgła „chibi”** (prośba gracza): bulwiaste kłęby jak z kreskówki zamiast miękkiego rozmycia: koła na siatce 11 px
  (promień 4.5-8.5 px, dryfują i „oddychają”), płaskie kolory (jasny brzeg u góry, podstawa, cień od dołu), ciemny obrys
  1 px, prawie nieprzezroczyste. Przezroczyste okno: wał przyklejony do wody (zachodzi 3 px na wodę, bez obrysu od strony
  wody). Zwykłe okno: jednolita mgła dalej od wyspy z bulwiastym brzegiem od strony lądu (w nocy podchodzi bliżej).
  Podgląd: `docs/img/mgla_chibi_*.png`, `tools/pixel-proto/fog_chibi_preview.py`.
- [x] **Menu główne i 3 sloty zapisu** (prośba gracza): nazwa do 10 znaków, data i godzina zapisu, usuwanie z potwierdzeniem,
  powrót do menu z pierścienia akcji (§12.11).
- [x] **Cykl gry zamiast zegara komputera** (prośba gracza po 6d): 1 h = 40 min dnia + 20 min nocy, tylko cykl gry (bez opcji
  zegara). Księżyc: 8 nocy na cykl, pełnia jedną noc (rytuał „Księżycowa rosa”: dwie). Kupiec-widmo w co trzecią noc (34%),
  w pierwszej połowie nocy, zostaje 10 min. Rośliny i drzewa rosną **x3 szybciej** (marchew 3:20, dynia 8:20); rośliny nocy
  liczone w nocach cyklu (wilcza jagoda 3/4 nocy, trupi grzyb 1, księżycowy kwiat 1.25, mandragora 1.75, upiorna dynia
  i dynia-upiór 2, cmentarny bluszcz 4, krwawa róża 1 noc pełni). Drzewa: wzrost 40-100 min, owoce 10-15 min, grusza 2 noce,
  drzewo księżyca 1 noc pełni. Sieci i wyprawy bez zmian.
  Balans po zmianie (symulacja z cyklem): kafelki `10 + 20n + 1.5n²` monet i `3 + n/3` Esencji, odblokowanie nasion
  300/780/1800 (dzień) i 960/2400/4800 (noc), słój 200/800/3000, duszek-handlarz 1600. Wyniki jak wcześniej: ognik po ok. 2 h,
  wszystkie nasiona ok. 49 h, 100 kafelków ok. 100 h, 150 kafelków ok. 180 h.
- [x] Liczby balansu, tempo **szybkie** (wybór gracza, Etap 6d, przed cyklem gry): symulacja `tools/balance/sim.py` (8 h pracy dziennie).
  Wyniki: lampa w kilka minut (wprowadzenie), pierwszy ognik po ok. 1-3 h, wszystkie nasiona po ok. 40 h (tydzień),
  25 kafelków ok. 23 h, 100 kafelków ok. 100 h (2 tygodnie), 150 kafelków ok. 175 h (miesiąc). Zmiany: kafelki droższe (§13.2),
  ognik 100 monet bez plonów nocy, odblokowanie nasion x1.3. Czasy wzrostu i ceny roślin bez zmian.
- [ ] Zasady stawiania lamp (dowolnie czy na slotach) i jak liczyć oświetlenie pól.
- [ ] Noc: stałe godziny, ustawiane w opcjach czy wschód i zachód z lokalizacji.
- [x] Rozbudowa wyspy: pojedyncze kafelki 16×16 (§13).
- [x] Start: **wyspa 2×2 o losowym kształcie**, świat **32×32 generowany z ziarna** (§13.1, §13.4).
- [x] Esencja w koszcie kafelka: tak.
- [x] Okno rośnie z wyspą (§13.6): start 144×112 natywnie (×3 = 432×336), krokami po 16 px do limitu 60% × 70% ekranu,
  potem przewijanie PPM. Limit i skala do ustawienia w opcjach później.
- [x] Do Etapu 3c lampa startowa, skrzynia i automatyka **stają same** na wolnym kafelku trawy (najbliżej startu);
  bez wolnego kafelka czekają (toast). Tryb stawiania przyjdzie w 3c.
- [x] Sklep lamp: płaci się dopiero przy postawieniu (PPM w trybie stawiania = nic nie kosztuje). Lamp można mieć dowolnie dużo.
- [x] Obiekty klika się w kafelek pod nimi (podstawa); klik otwiera panel kontekstowy (Usuń / Sprzedaj / Przenieś).
- [ ] Pasma światła lamp z ditheringiem Bayera (§4.4): przesunięte na później (wymaga własnego światła 2D albo post-processu).
- [x] Krzywa cen kafelków: kwadratowa, mocniejsza od 6d (§13.2).
- [x] Łowienie ryb wchodzi do gry (pomost, sieci, §11 F).
- [ ] Progi rytmu pracy (seria 25 min, przerwa 5 min, zmęczenie 8 h/10 h) do sprawdzenia w praktyce.
- [ ] Mutacje: które pary roślin dają które hybrydy. Na razie jedna para: dynia + upiorna dynia = dynia-upiór (dane w `CropDefinition.parentA/B`).
- [x] Nasiona spoza sklepu (hybrydy, kupiec-widmo, wyprawy) trafiają do **worka nasion**; koło nasion najpierw bierze z worka.
- [x] Nawóz (+1 jakości) na razie tylko od kupca-widma i z wypraw, zużywany automatycznie przy sadzeniu. Kompostownik później.
- [x] Bonus ruin: na razie brak (ruiny mają miejsce na ołtarz, Etap 5b/5c).
- [x] Budowle morskie kosztują monety + drewno: pomost 300 + 10, sieci 150 + 5, łódź 600 + 15 (do balansu).
- [ ] Tabela łupów wypraw łodzią.
- [ ] Treść lore (historia wyspy, mgły, potwora).
- [x] Punkty księżyca (Etap 5c): przy nowiu cykl daje plony/10 + plony nocy/5 + ryby/5 + zdarzenia/3 + kafelki/10, maks. 12;
  pełna strona bestiariusza daje jednorazowo 2 / 1 / 2 / 3 pkt (rośliny / ryby / zjawy / zapiski). Liczby do balansu.
- [x] Pierwsze rytuały (jednorazowe, na zawsze): Zmierzchowe ziarno (rośliny nocy rosną o zmierzchu, 2 pkt + 3 odłamki),
  Ciche lampy (-25% Esencji, 2 pkt + 8 łusek), Księżycowa rosa (pełnia x2 dłużej, 3 pkt + 1 dar), Głębokie korzenie
  (rośliny dnia +10%, 2 pkt + 20 drewna), Szept mgły (kupiec x2 dłużej, 2 pkt + 2 odłamki), Pamięć przypływu (wyprawy -25%,
  3 pkt + 1 dar).
- [x] Chiński: **uproszczony (zh-Hans)**.
- [x] Okno (Etap 6a): własny kod WinAPI zamiast UniWindowController (bez zależności, kompiluje się na atrapach). Działa tylko
  w buildzie na Windows; w edytorze działają skala, mini-tryb i podgląd przezroczystości (schowane morze i mgła).
- [x] Przezroczyste okno: bez mgły, z morza zostaje pas ok. 14 px przy brzegu (krawędź w szachownicę), wyspa „leży” na pulpicie; klik w puste miejsce idzie do pulpitu (ląd, obiekty
  i UI łapią klik). Wymaga wyłączonego „DXGI flip model” (ustawia Wyspa → 1). Łowienie ryb w tym trybie jest trudne (ryby
  są w wodzie), do przemyślenia.
- [x] Mini-tryb = najmniejszy kadr (144×112) w skali ×2. Okno bez ramki przesuwa się LPM za puste miejsce; puszczone przy rogu
  ekranu przykleja się do niego (i rośnie od rogu).
- [x] Reset zapisu: dwa kliknięcia w 4 s, opcje gracza (PlayerPrefs) zostają.
- [x] Automatyka w wielu egzemplarzach (Etap 6b, wybór gracza): ogniki z zasięgiem 5x5 wokół siebie, duszek-handlarz na całą
  wyspę (jedna skrzynia na duszka), runa siewu 3x3 z wyborem nasion, kupno w Sklep → Budowle z ceną rosnącą x1.6 (§11.3 E).
  **Zmiana w 6d:** duszek-handlarz stoi przy swojej skrzyni i sprzedaje ją, gdy jest pełna.
- [x] Nowe nasiona (Etap 6b): odblokowanie w Sklep → Nasiona; koło nasion pokazuje tylko odblokowane (i to, co jest w worku).
  Dzień: czosnek (100), mak (260), piołun (600); noc: wilcza jagoda (320), mandragora (800), cmentarny bluszcz (1600)
  (ceny z balansu 6d, wcześniej 80/200/450/250/600/1200).
- [x] Przezroczyste okno: mgła zostaje pierścieniem wokół pasa wody (zaczyna się ok. 8 px od brzegu, kończy ok. 30 px,
  dalej rozpływa się ditheringiem Bayera).
- [x] Tempo pracy (§4.1): mnożnik tylko dla roślin dnia (Esencja bez zmian). Do balansu: wagi i maks. x1.5.
- [x] Koło nasion pokazuje jedną kategorię (dzień albo noc); przycisk w środku przełącza (wybór zapamiętany).
- [x] Grządkę można zasypać (PPM na grządce → „Zasyp grządkę”, roślina przepada, bez zwrotu). Budowle kupione w grze można
  rozebrać (panel → „Rozbierz”, drugi klik potwierdza): wraca połowa monet, skrzynia sprzedaje zawartość.
- [x] Wyjście z gry: w pierścieniu akcji i w Opcje → System (okno bez ramki nie ma przycisku X). Powiększenie okna x2..x6
  w Opcje → Okno i Ctrl + kółko myszy.
- [x] Przezroczyste okno: pas wody 18 px, mgła tylko nad nim (gęstnieje ku krawędzi), nad pulpitem nic (pierścień nad pulpitem
  wyglądał jak szum).
- [x] UI na pulpicie (§12.10): gracz wybrał **wariant C**, pierścień akcji pod PPM zamiast przycisków w rogu okna. Pasek walut
  i rytm pracy zostają na razie w rogach (do decyzji przy dalszym dopracowaniu UI w 6d).
- [x] HUD (Etap 6d, wybór gracza): **jeden pasek nad wyspą** (waluty i Esencja u góry, rytm, tempo i zegar pod spodem),
  idzie za lądem, chowa się po 5 s bez ruchu myszy, w mini-trybie go nie ma. Toasty w prawym górnym rogu okna.
- [x] Wprowadzenie (Etap 6d, wybór gracza): **tablica ogłoszeń** na czwartym kafelku nowej wyspy (zamiast dekoracji startowej),
  5 zadań (posadź, pracuj, zbierz, dokup kafelek, kup lampę) i karteczka „gotowe”. Karteczka z zadaniem wisi pod wyspą,
  złota strzałka unosi się nad celem. Rozbiórka tablicy kończy wprowadzenie (3 drewna). Stare zapisy: bez wprowadzenia.
- [x] Mgła w przezroczystym oknie (po teście 6d): **gęsty wał przyklejony do wody** (zachodzi 3 px na pas wody, 14-30 px
  szerokości, postrzępiona krawędź z dryfującymi kłębami), zamiast osobnych kłębów dalej od wyspy.
- [x] Wiatr (po teście 6d): podmuch to **fala przechodząca przez wyspę** z lewej na prawą (rośliny pochylają się po kolei),
  łodyga gnie się od czubka (podstawa sztywna), po podmuchu sprężysty powrót z małym odbiciem i drżeniem liści.
- [x] Cząsteczki: **własny system pikselowy** (pula sprite'ów, 12 kl./s) zamiast ParticleSystem, dla ostrych pikseli.
- [x] Woda i mgła: **liczone w shaderze z mapy danych** (port prototypu), ryby i potwór w shaderze, nie jako osobne sprite'y.
- [ ] Rozmiar fontu tekstu: 10 px czy 12 px. Sprawdzimy później w Unity (§16.3).
- [x] Lokalizacja w kodzie: **własna warstwa CSV** (`LocCore`/`Loc`) zamiast paczki Unity Localization (§16.4).
- [ ] Przejście na Unity Localization: później albo wcale (klucze są zgodne, więc migracja jest możliwa).
- [x] Drewno i kamień jako materiały do budowli (§14.3). Odnawialne drewno i owoce: drzewa do sadzenia, na później (§15).

---

## 8. Prototyp wizualny (`tools/pixel-proto/`)
Python 3 + Pillow. Sprite'y generowane proceduralnie (cieniowane elipsy, automatyczny kontur), wyniki w `tools/pixel-proto/out/`.

| Plik | Co robi |
|---|---|
| `sprites.py` | paleta (jasna baza), klasa `Sprite`, sprite'y 16 px (rośliny, obiekty, UI) |
| `sprites2.py` | sprite'y 32 px (marchew, zboże, drzewo, grządka) |
| `water.py` | warstwowa woda (głębokość, ryby, potwór, fale, przybój, piana) |
| `anim.py` | scena wyspy i animacja (48 klatek przy 10 kl./s): kołysanie, zbiór, monety, pop, mgła |
| `daynight.py` | lampy, oświetlenie (pasma i Bayer), pory dnia, nocny GIF |
| `run_dark.py` + `run_dark_palette.py` | uruchamia dowolny skrypt z paletą mroczną |
| `sheet.py`, `compare.py` | arkusz stylu, porównanie 16 vs 32 px |
| `export_unity.py` | eksport placeholderów do `Assets/_Project/Art/` (PNG, `pivots.json`, `prototype_layout.json`) |
| `sprites3.py`, `sheet3.py` | sprite'y do §11 (rośliny nocy, automatyka, morze, strefy, zdarzenia) + arkusze |
| `ui_kit.py` | font 3×5 (+ generator TTF), ikony, elementy 9-slice |
| `mockups.py` | makiety UI (`out/ui_*.png`) i plik `WyspaPixel.ttf` |
| `export_unity_ui.py` | eksport nowych sprite'ów, UI (z granicami 9-slice) i fontu do Unity (uruchamiać po `export_unity.py`) |
| `export_unity_auto.py` | Etapy 6b-6d: nowe rośliny, duszki, ptaki, skrzynia 16 px, tablica ogłoszeń i strzałka wprowadzenia |
| `../balance/sim.py` | Etap 6d: symulacja progresji (godziny pracy do kamieni milowych), liczby jak w grze |
| `export_unity_fx.py` | Etap 3a: `water_base.png`, mapa danych `water_data.png` (odległość od brzegu, klasa piksela), `Art/water_layout.json` (ryby, potwór), sprite'y cząsteczek `Fx/` |

Shadery sprawdzamy poza Unity: `tools/shadercheck/check.py` kompiluje bloki HLSL kompilatorem DXC na prawdziwych plikach
URP 17.6 (opis w pliku i w CLAUDE.md). Wyłapuje błędy w naszym kodzie, ale nie zastępuje testu w Unity.

Uruchamianie (z katalogu `tools/pixel-proto/`):
```
python3 run_dark.py anim.py      # out/dark_prototyp.gif, out/dark_prototyp_klatka.png
python3 run_dark.py compare.py   # out/dark_porownanie_detal.png
python3 run_dark.py sheet.py     # out/arkusz_styl.png
python3 daynight.py              # out/pory_dnia.png, out/noc.gif, out/lampy.png
```

Obrazki referencyjne (kopie): `docs/img/`
- `prototyp_dzien.gif`, `prototyp_noc.gif`, `prototyp_klatka.png`, `pory_dnia.png`, `lampy.png`, `arkusz_styl.png`, `porownanie_16_vs_32.png`

---

## 9. Historia decyzji
1. Idle w okienku, wyspa, wzrost od czasu pracy przy komputerze, sprzedaż, zakupy, powiększanie wyspy.
2. Wstępnie tylko rozmowa, bez kodu w repo. (Wyjątek: ten GDD i prototyp zapisane na prośbę gracza,
   żeby wiedza przetrwała wyczyszczenie kontekstu.)
3. Pixel art. **Bez postaci.** Animacje przez shadery i cząsteczki.
4. Styl wg referencji gracza (kontur, hue-shift, krępe kształty) i palety Luca Davincci.
5. Większy detal, test 32 px. Zboże musi się różnić od marchwi (sylwetki).
6. Wersja „cukierkowa” odrzucona. **Kierunek mroczny, dark fantasy.**
7. Lampy plus pora dnia z zegara komputera (noc jest ciemniejsza).
8. Woda w warstwach: ryby pod wodą, fale morskie.
9. Zaakceptowane: rośliny dnia i nocy, **Esencja z pracy zasila lampy**, **mgła poza wyspą na krawędziach okna**,
   zdarzenia nocne.
10. Start kodu: Unity 6.3 LTS (później zmienione na 6.6, pkt 23). Gracz kopiuje `Assets/_Project` do projektu z szablonu Universal 2D.
    Etap 1 to szkielet systemów + scena budowana z menu „Wyspa”.
11. Przyjęty **cały system ulepszeń i progresji** (§11): automatyzacja, rytm pracy (seria skupienia, bonus za przerwę,
    zmęczenie), powiększanie wyspy ze strefami, lampy z trzema kierunkami ulepszeń, mutacje i jakość, morze i wyprawy,
    rytuały z cyklem księżycowym zamiast prestiżu, kolekcje i lore, opcje wygody.
    Główna gałąź repo powstanie z pusha z Unity. Gałąź Claude zostaje osobno.
12. Sprite'y do nowych systemów + projekt UI (§12): HUD znikający, koło nasion, Księga jako główne menu,
    tryb stawiania z podglądem światła, toasty, własny font 3×5 z polskimi znakami.
13. **Rozbudowa wyspy kafelkami** (§13): pojedyncze kafelki przy brzegu za monety + Esencję, organiczny brzeg,
    strefy i znaleziska odkrywane pod mgłą.
14. Każda gra startuje z **wyspą 2×2 o losowym kształcie**. **Świat 32×32 losowany z ziarna** (strefy, znaleziska).
    Esencja zostaje w koszcie kafelka. Krzywa cen zmieniona na kwadratową (wykładnicza nie pasuje do 1024 kafelków).
15. ~~Zarysy stref we mgle~~ **odrzucone**: strefy odkrywa się bez podpowiedzi.
16. Strefy dostały własne podłoża i zestawy dekoracji (§13.8). Dodane **usuwanie dekoracji** (§14),
    z którego są materiały drewno i kamień.
17. Drewno i kamień przyjęte jako materiały. **Na później:** różne drzewa do sadzenia i zbierania owoców (§15).
18. Sprite'y 5 drzew owocowych gotowe (mechanika na później).
19. Drzewa: więcej koloru, a świecące drzewa (grusza, drzewo księżyca) dają światło w nocy.
20. Światło świecących drzew **liczy się jak lampa** dla wzrostu (bez kosztu Esencji, tylko gdy owocują).
21. **Lokalizacja: PL, EN, DE, JA, ZH** (§16). Font dla JA/ZH: Fusion Pixel Font (OFL 1.1).
22. **Nazwa gry: Pockland.** Chiński: uproszczony. Rozmiar fontu sprawdzimy później.
23. **Unity 6.6 (6000.6.5f1)** zamiast 6.3 LTS. To wydanie bez LTS, wspierane do wyjścia kolejnego:
    aktualizujemy łatki 6000.6.x, a przejście na następne wydanie (np. kolejne LTS) robimy świadomie, z kopią na osobnej gałęzi.
24. Etap 1 przetestowany w Unity (sadzenie, wzrost, zbiór działają). Start Etapu 2 w częściach: 2a waluty + ulepszenia + rytm pracy,
    2b automatyzacja, 2c docelowe UI z lokalizacją.
25. Etap 2a przetestowany (waluty, ulepszenia, rytm pracy działają). Etap 2b: automatyzacja.
    Ognik zbiera po jednym plonie co 30 s gry; ręczny zbiór dalej daje monety od razu, a skrzynię sprzedaje się kliknięciem (100%)
    albo automatycznie skrzynią kupca (90%).
26. Etap 2b przetestowany. **Etap 2c: docelowe UI** budowane z kodu (uGUI + TextMeshPro): HUD, koło nasion, Księga
    (działają Ulepszenia, Statystyki, Opcje), toasty, dymki. HUD debugowy schowany pod **F1**.
    Lokalizacja przez **własną warstwę CSV** zamiast paczki Unity Localization: mniej konfiguracji w edytorze,
    da się ją testować poza Unity, a klucze i format `{0:plural:...}` zostają zgodne, gdyby trzeba było przejść na paczkę.
    Tabele: `Assets/_Project/Localization/` (glossary, game, ui).
27. Etap 2c przetestowany. Etap 3 w dwóch częściach: **3a żywa wyspa** (shadery i cząsteczki), **3b powiększanie wyspy**.
    Shadery piszemy na bazie źródeł URP 17.6 i kompilujemy w kontenerze kompilatorem DXC (`tools/shadercheck`).
    Woda, ryby, potwór i mgła są portem prototypu z Pythona (te same reguły, 10 kl./s), a pętle ruchu są wolniejsze niż w GIF-ie
    (ryby 30 s, mgła 30 s), bo w grze patrzy się na nie godzinami.
28. Etap 3a przetestowany. **Etap 3b: wyspa z kafelków według GDD** (decyzja gracza: pełny GDD w dwóch krokach, okno rośnie z wyspą).
    Start 2×2 z 2 grządkami, dekoracją i latarnią; świat 32×32 z ziarna; kupowanie kafelków (monety + Esencja), znaleziska,
    przekopywanie trawy na grządkę, mgła liczona od brzegu, więc cofa się sama. 3c: tryb stawiania i usuwanie dekoracji.
    Zapis v3 (wyspa); stary zapis zachowuje waluty i ulepszenia, a wyspę dostaje nową.
29. Etap 3b przetestowany. **Etap 3c:** sklep lamp w Księdze i tryb stawiania (duch, czerwony gdy zajęte, okrąg zasięgu,
    „Oświetli: N grządek”), przenoszenie obiektów, panel kontekstowy przy obiektach, usuwanie dekoracji z animacjami
    i pozostałościami (§14), dekoracje na 25% nowych kafelków, ląd wynurza się w 3 klatkach. Latarnia startowa
    jest stawiana w kodzie jak każda kupiona lampa.
30. Etap 3c przetestowany. **Etap 4** w częściach: 4a jakość (1-3, światło ognika, nawóz), mutacje (worek nasion, dynia-upiór),
    pełnia z prawdziwej fazy księżyca; 4b kupiec-widmo (raz na noc 21-3, 10 min, nasiona, nawóz, hybryda, skup skrzyni 150%);
    4c pomost (woda przy brzegu, ląd z boku), łowienie ryb z shadera wody (te same obliczenia na CPU), sieci; 4d łódź
    i wyprawy na 2 albo 4 h aktywnej pracy z losowym łupem. Sklep w Księdze ma podzakładki Lampy i Morze.
31. Etap 4 przetestowany (poprawka: panel kupca mierzy teksty po pokazaniu i łamie długie przyciski; bez przedrostka „Nasiona:”).
    **Etap 5** w częściach: 5a strefy, 5b nocne zdarzenia, potwór i latarnia morska, 5c Księga rytuałów, cykl księżyca,
    bestiariusz i lore. 5a: podłoża stref w malarzu wyspy (port `sprites4.py`), dekoracje stref na 55% kafelków strefy,
    zabytki w środku strefy (krypta, posąg + kamienne schody; na bagnie namorzyn), toast „Odkryto: …”, bonus ziemi
    (cmentarz: rośliny nocy x1.5, bagno: trupi grzyb x1.5), wrony, żaby i bąble jako tło.
32. Poprawka pomostu (gracz zabudował go lądem): woda pod deskami i przy łodzi jest wyłączona z rozbudowy, stare zapisy
    z zasypanym pomostem dostają zwrot kosztów. **5b:** nocne zdarzenia (błędne ognik: +3 Esencji, spadająca gwiazda:
    odłamek leży 2 min, potwór: ofiara 5 plonów nocy = dar z głębin, raz na noc), ołtarz na brzegu (500 monet + 10 kamienia:
    potwór x3, przywołanie za 3 plony nocy), latarnia morska (2000 monet + 10 odłamków, lampa 0.3 Esencji/min:
    mgła o 25% dalej, zdarzenia x2).
33. **5c:** Kronika wyspy liczy osiągnięcia cyklu księżyca (od nowiu do nowiu, z zegara komputera) i przy nowiu zamienia je
    w punkty księżyca (wyspa nic nie traci). Księga ma zakładki Rytuały (6 stałych bonusów za punkty i surowce, §7)
    i Bestiariusz (rośliny, ryby, zjawy, zapiski lore; nieodkryte jako „???”, pełna strona = punkty). Zapis v5.
34. **Etap 6** w częściach: 6a okno i opcje, 6b dźwięk, ikona w zasobniku i powiadomienia, 6c wprowadzenie (karteczki),
    dopracowanie UI i balans. 6a: Opcje w Księdze mają sekcje Ogólne (język, jasność nocy, godziny nocy, rytm pracy, próg
    bezczynności), Okno (skala ×2–×4, mini-tryb, ramka, zawsze na wierzchu, przezroczyste tło, róg ekranu) i System (start
    z Windows przez HKCU\Run, reset zapisu). Okno obsługuje własny kod WinAPI.
35. Poprawka buildu: brak UI, bo build wycinał shader fontu TMP (font tworzony w locie). Wyspa → 1 dołącza go na stałe,
    a bez niego UI startuje z domyślnym fontem TMP.
36. Build startował na pełnym ekranie (Unity pamięta tryb z poprzednich uruchomień): gra przy starcie przełącza się w okno.
    Przezroczyste okno zostawia pas wody przy brzegu (shader wody, `_ShoreRange`).
37. **Etap 6b** (prośba gracza: więcej nasion i automatyki): 6 nowych roślin do odblokowania (Sklep → Nasiona), budowle
    automatyki w wielu egzemplarzach (Sklep → Budowle): ognik z zasięgiem 5x5, runa siewu 3x3 z wyborem nasion, duszek-handlarz
    zamiast skrzyni kupca, wiele skrzyń. Zapis v6 (stare zapisy: skrzynia, skrzynia kupca i kamień runiczny stają się budowlami).
    Mgła w przezroczystym oknie wraca jako pierścień wokół wyspy. Dźwięk i zasobnik przechodzą do 6c, wprowadzenie, UI i balans do 6d.
38. UI (wariant C z §12.10): pierścień akcji pod PPM na wyspie (Księga, Sklep, Opcje, Rozbudowa) zamiast trzech przycisków
    w prawym górnym rogu. PPM z przeciąganiem dalej przesuwa kamerę, PPM przy otwartym trybie/panelu dalej go zamyka.
    Nowy gracz dostaje co 3 min podpowiedź (toast), aż pierwszy raz otworzy pierścień.
39. Uwagi gracza po teście: zasypywanie grządek i rozbiórka budowli, „Wyjdź z gry” (brak X w oknie bez ramki), powiększenie
    x2..x6 + Ctrl + kółko, koło nasion z podziałem dzień / noc, mgła tylko nad pasem wody, **tempo pracy** (pisanie i mysz
    przyspieszają rośliny dnia do x1.5, §4.1).
44. Po teście: mgła w przezroczystym oknie **dalej od wyspy**: nad pasem wody czysto, osobne miękkie kłęby od ok. 4 px za wodą
    do ok. 30 px dalej (okno ma wtedy większy zapas wokół lądu). Drzewa: każde kołysze się we własnej fazie, świecące owoce
    kołyszą się razem z drzewem (materiał SwayTreeGlow), **podmuchy wiatru pochylają rośliny i drzewa** (`_WyspaWind`),
    przy zbiorze owoce spadają z korony (zamiast podskakującego całego drzewa), nowa faza = sprężyste pyknięcie, owocujące
    drzewa iskrzą, ścinanie: trzy uderzenia, liście, przyspieszające przewracanie. **Ptaki** (`AmbientBirds`): w dzień wrona
    albo ptaszek przylatuje co 25-70 s, siada na drzewie lub wolnym kafelku, dziobie, podskakuje, odlatuje; kursor je płoszy.
45. Po teście: **skrzynia na plony ma dokładnie 16 px szerokości** razem z obrysem (wcześniej 20 px na kafelku 16 px),
    więc sąsiednie skrzynie stykają się bokami zamiast na siebie nachodzić. Trzy stany zapełnienia bez zmian.
46. **Etap 6d** (wybory gracza): HUD jako jeden pasek nad wyspą, wprowadzenie z tablicą ogłoszeń i karteczkami (5 zadań,
    strzałka nad celem), balans w tempie „szybkim” z symulacji (`tools/balance/sim.py`: kafelki droższe, ognik 100 monet,
    nasiona do odblokowania x1.3). Prośby gracza w trakcie: ogniki strącają owoce z drzew i zbierają je z ziemi,
    duszek-handlarz stoi przy skrzyni i sprzedaje ją, gdy jest pełna, mgła w przezroczystym oknie gęstsza i przyklejona
    do wody, wiatr jako fala przez wyspę z giętkimi łodygami i sprężystym powrotem. Zapis v8.
47. Po teście 6d (prośby gracza): **cykl gry 1 h** (40 min dnia, 20 min nocy) zamiast zegara komputera, księżyc 8 nocy,
    wzrost roślin i drzew **x3**, rośliny nocy liczone w nocach cyklu, ceny przeliczone symulacją (tempo bez zmian).
    W sklepie i rytuałach za długi opis skraca się z „...” (koszt zostaje cały, pełny tekst w dymku).
48. Prośba gracza: **menu główne** z 3 slotami zapisu (nazwa do 10 znaków, data i godzina zapisu, usuwanie), wyspa w tle,
    język i wyjście w menu, „Menu główne” w pierścieniu akcji. Stary `save.json` → slot 1.
49. Prośba gracza: **mgła w stylu „chibi”** - bulwiaste kłęby z obrysem, płaskie kolory, w obu trybach okna.
50. Po teście: we wprowadzeniu kafelek przed lampą (na starcie nie było miejsca na lampę), karteczka z większym marginesem
    i szersza (mniej linii). **Grządki przycięte do lądu** (SpriteMask z maski lądu, 2 px trawy od brzegu), więc na skrajnych
    kafelkach nie wystają poza zaokrąglony, nierówny brzeg.
51. Tańsze pierwsze 20 kafelków (prośba gracza). Wybór dalszej zabawy: zamówienia, kocioł alchemiczny, chowańce i pogoda,
    dźwięk i zasobnik. Zrobione zamówienia na tablicy; wskaźnik pisania i myszy w HUD, tempo z 15 s.
43. Po teście: mgła w przezroczystym oknie **miękka** (półprzezroczysta w 6 stopniach zamiast szachownicy), kłęby za wodą
    do ok. 16 px. Światło przerobione na **Light 2D jak lampy**: snopy słońca/księżyca to Sprite Light 2D z obrazem snopów
    i wolumetrią (`LightShafts`), lampy i świecące owoce z wolumetrią, ogniki/duszki/runy mają w nocy własną poświatę
    (`NightGlow`). Shader nakładkowy `LightShafts.shader` usunięty. Nowe cząsteczki: podmuchy wiatru (smugi), pyłki w dzień,
    zarodniki w nocy, płynne świetliki. **Drzewa owocowe** (§15.1). Zapis v7.
41. Upiększanie („cozy”, prośba gracza): światło „wolumetryczne” w pikselach (`LightShafts`: snopy słońca w dzień,
    pomarańczowe o zmierzchu, blade promienie księżyca w nocy, mocniejsze w pełnię, dithered poświata zapalonych lamp),
    iskierki odbić na wodzie tam, gdzie pada światło (w kolorze lampy/słońca), płynny lot duszków po łuku i unoszenie
    w kodzie zamiast skaczących klatek, ognik zbiera na jeden kurs do 3 plonów (zbiór w chwili dolotu, niesione plony
    widać pod nim) i dopiero wtedy leci do skrzyni.
42. Po teście: płynność wszystkich animacji (prośba gracza; zmienia wcześniejsze „skokowo ~10 kl./s” z §2 i §5.5):
    opcja Płynność (Eko 30/12, Płynna 60/30 domyślnie, Max 60/60 kl./s z fokusem/w tle), kołysanie roślin, ryby, mgła i snopy
    w płynnym czasie, cząsteczki, lot monet, „pyknięcie” rośliny, łódź kupca i łódź wypraw co klatkę. Klatki sprite'ów
    (płomień, macki) i miganie piany zostają rysunkowe. Mgła w przezroczystym oknie jaśniejsza w nocy, za wodą rzadkie
    kłęby (ok. 10 px). Poświata także wokół ogników, duszków i run; obiekt LightShafts tworzy się sam, gdy scena go nie ma.
40. Fonty CJK: Fusion Pixel 8px (JA i zh-Hans) jako zapasowe dla `WyspaPixel` (§16.2). Log z buildu pokazywał kwadraciki
    zamiast „日本語” i „简体中文” na przyciskach języków.

---

## 10. Kod Unity (`Assets/_Project/`)
Instrukcja uruchomienia: `docs/UNITY_SETUP.md`. Kod kompiluje się poza Unity na atrapach: `tools/cscheck/` (opis w pliku `.csproj`).

| Plik | Rola |
|---|---|
| `Scripts/Core/SystemIdle.cs` | `GetLastInputInfo` (Windows), czas od ostatniego inputu w systemie |
| `Scripts/Core/ActivityTracker.cs` | aktywny, gdy input < 60 s (do testów `forceActive`) |
| `Scripts/Core/GameClock.cs` | noc 20:00–6:00 (do ustawienia), debug godzina, faza księżyca, sekundy nocy między datami (offline) |
| `Scripts/Core/EssenceStore.cs` | Esencja: +1 za minutę aktywności, pojemność 60 |
| `Scripts/Core/GameLoop.cs` | tick co 1 s: aktywność, Esencja, lampy (najtańsze pierwsze), wzrost roślin. `debugTimeScale` |
| `Scripts/Core/AppPerformance.cs` | runInBackground, vSync 0, 30 kl./s z fokusem, 12 w tle |
| `Scripts/Farming/CropDefinition.cs`, `CropDatabase.cs` | ScriptableObjecty roślin (Day/Night, czas, 4 sprite'y faz, ceny) |
| `Scripts/Farming/PlotState.cs` | czyste dane pola + reguły wzrostu (`GrowthContext`) |
| `Scripts/Farming/FarmPlot.cs` | grządka: klik = sadzenie lub zbiór, „pop” przy zmianie fazy |
| `Scripts/Lighting/LampDefinition.cs`, `Lamp.cs` | lampa: Esencja na minutę, zasięg, kolor, mnożnik wzrostu, Light2D + glow (Unlit) |
| `Scripts/Lighting/Illumination.cs` | czy punkt jest oświetlony (dzień albo lampa) |
| `Scripts/Lighting/DayNightLighting.cs` | Global Light 2D: kolor (Gradient) i jasność (krzywa) wg godziny, `minBrightness` |
| `Scripts/Economy/Currency.cs`, `Wallet.cs` | waluty (§11.2: monety, plony nocy, odłamki, łuski, dary, drewno, kamień), portfel na wszystkie, start 50 monet |
| `Scripts/Upgrades/*` | `UpgradeDefinition` (poziomy z kosztem w wielu walutach + wartość efektu, wymagania), `UpgradeDatabase`, `UpgradeState` (posiadane poziomy, zakup), `UpgradeIds` |
| `Scripts/Core/WorkRhythm.cs` | rytm pracy: seria skupienia, „odświeżony” po przerwie, zmęczenie; mnożnik Esencji |
| `Scripts/Automation/*` | budowle w wielu egzemplarzach (Etap 6b): `HarvestCrate` (20 miejsc, klik = sprzedaż 100%), `WispGardener` (zasięg 5x5, do najbliższej skrzyni), `MerchantSpirit` (sprzedaje najpełniejszą skrzynię po 90%), `SeedRune` (sieje 3x3, wybór nasion w kole), `AutomationSystem` (darmowa pierwsza sztuka, ceny, ticki); stawia je `Island.SpawnBuilding` |
| `Scripts/Save/SaveSystem.cs` | JSON w persistentDataPath, autosave co 60 s, offline: rośliny nocy rosną za godziny nocy |
| `Scripts/Input/ClickRouter.cs` | klik, potem Collider2D, potem `IClickable` (Input System albo stary Input) |
| `Scripts/UI/DebugHud.cs` | HUD debugowy IMGUI (F1): waluty, rytm, ulepszenia, przełączniki testowe, czas x60, cheat |
| `Scripts/Core/GameEvents.cs` | zdarzenia dla UI (toast = klucz lokalizacji + argumenty) |
| `Scripts/Localization/LocCore.cs`, `Loc.cs` | lokalizacja: tabele CSV, fallback en, potem pl, `{0}`, `{0:N0}`, `{0:plural:a\|b\|c}` (CLDR), liczby wg kultury; `Loc` = wybór języka (PlayerPrefs `lang`, przy pierwszym starcie język systemu) |
| `Scripts/UI/UiRoot.cs` | korzeń UI: Canvas (Constant Pixel Size, skala UI = skala świata − 1), EventSystem, font TMP tworzony w locie z `WyspaPixel.ttf`, dymek nad grządką |
| `Scripts/UI/Ui.cs`, `UiTheme.cs`, `UiInput.cs`, `LocText.cs` | fabryka elementów (9-slice, tekst, przycisk, pasek), motyw (sprite'y, ikony, kolory, tabele), wejście, tekst z klucza odświeżany po zmianie języka |
| `Scripts/UI/HudView.cs` | HUD: waluty (pokazane po zdobyciu), Esencja, rytm pracy, zegar; chowa się po 5 s bez ruchu myszy |
| `Scripts/UI/ActionRing.cs` | pierścień akcji (PPM na wyspie): Księga, Sklep, Opcje, Rozbudowa; wyzwalany w `UiRoot` (bez przeciągania, gdy nic nie jest otwarte) |
| `Scripts/UI/SeedWheel.cs` | koło nasion wokół pustej grządki, dymki z opisem, PPM/Esc zamyka |
| `Scripts/UI/BookView.cs` | Księga: zakładki Sklep (Lampy, Morze), Ulepszenia, Rytuały, Bestiariusz, Statystyki, Opcje (język, jasność nocy, rytm pracy) |
| `Scripts/UI/ToastView.cs`, `TooltipView.cs` | toasty (maks. 3, 4 s) i dymek przy kursorze |
| `Localization/*.csv` | tabele: `glossary` (słownik terminów), `game` (teksty gry), `ui` (interfejs) |
| `Shaders/SpriteSway.shader`, `SpriteSwayUnlit.shader` | sprite oświetlany (jak Sprite-Lit-Default URP 17.6) + kołysanie wierszy pikseli + kontur; wersja bez oświetlenia dla `*_glow` |
| `Shaders/Water.shader`, `Fog.shader`, `WyspaCommon.hlsl` | woda i mgła liczone z `Ground/water_data.png` (port `water.py` i mgły z `anim.py`), oświetlane światłami 2D |
| `Scripts/Visual/SpriteFx.cs` | faza kołysania z pozycji na świecie i kontur przez MaterialPropertyBlock |
| `Scripts/Visual/WaterAnimator.cs`, `FogController.cs` | ryby i potwór do shadera wody, noc; gęstość mgły (dzień / noc / zgaszone lampy) |
| `Scripts/Visual/PixelParticles.cs`, `IslandFx.cs` | własne cząsteczki pikselowe (pula sprite'ów, 12 kl./s, siatka pikseli); efekty zbioru, sadzenia, wzrostu, iskierki, świetliki, liście |
| `Scripts/Visual/FxMaterial.cs` | przełącznik shaderów efektów (F1): powrót do zwykłego materiału |
| `Scripts/World/WorldTypes.cs`, `WorldGenerator.cs`, `IslandMap.cs`, `IslandPainter.cs` | czyste C# (testowalne poza Unity): kafelki, ziarno, generator świata (port `world_proto.py`), mapa i ceny (§13.2), malowanie lądu, wody i mapy danych (port `tiles_proto.py`, morfologia przez transformatę odległości) |
| `Scripts/World/Island.cs`, `IslandArt.cs`, `TileObject.cs` | wyspa w scenie: tekstury w locie, grządki i dekoracje na kafelkach, zakup, przekopywanie, znaleziska, samoczynne stawianie obiektów, zapis |
| `Scripts/World/ViewController.cs` | kadr i okno rosną z wyspą (Pixel Perfect Camera, `Screen.SetResolution` w buildzie), przewijanie PPM |
| `Scripts/Farming/SeedBag.cs` | worek nasion (hybrydy, kupiec, wyprawy); jakość i mutacje w `PlotState`, `CropDefinition`, `GameLoop.TryMutate` |
| `Scripts/Sea/GhostMerchant.cs` | kupiec-widmo: harmonogram nocy, oferta, skup skrzyni |
| `Scripts/Sea/Pier.cs`, `FishingNets.cs` | pomost i łowisko (ryby liczone tak jak w `Water.shader`), sieci co godzinę |
| `Scripts/Sea/Expedition.cs` | łódź i wyprawy (postęp z aktywnej pracy, łup) |
| `Scripts/Sea/NightEvents.cs`, `WillOWisp.cs`, `StarPickup.cs`, `MonsterEvent.cs`, `Altar.cs` | nocne zdarzenia (harmonogram, ogniki, gwiazdy, potwór), ołtarz |
| `Scripts/Lore/Chronicle.cs`, `Rituals.cs` | kronika: liczniki cyklu, punkty księżyca przy nowiu, odkrycia do bestiariusza, rytuały i ich efekty (statyczne mnożniki) |
| `Scripts/Platform/WinApi.cs`, `DesktopWindow.cs` | okno na pulpicie (tylko build Windows): ramka, na wierzchu, róg ekranu, przeciąganie, przezroczystość z klikiem przez tło, `Autostart` (HKCU\Run) |
| `Scripts/Farming/FruitTree.cs` | drzewa owocowe: `TreeSpecies` (dane 5 gatunków), `TreeArt`, `FruitTree` (wzrost, owoce, zbiór kliknięciem, ścinanie, świecenie) |
| `Scripts/Visual/AmbientBirds.cs` | ptaki: przylot po łuku, siadanie na drzewie lub kafelku, dziobanie, podskoki, odlot, płoszenie kursorem |
| `Scripts/Visual/LightShafts.cs`, `NightGlow.cs` | snopy słońca/księżyca (Sprite Light 2D z wolumetrią, za kamerą), nocna poświata ogników, duszków i run |
| `Scripts/World/Decoration.cs` | dekoracja na kafelku: panel „Usuń”, animacje usuwania (potrząśnięcie, przewracanie, pęknięcie, ścięcie), cząsteczki |
| `Scripts/Core/ContextActions.cs`, `Scripts/UI/ContextPanel.cs` | panel kontekstowy przy obiekcie: komponenty dopisują linie i przyciski (`IContextActions`) |
| `Scripts/UI/PlacementMode.cs` | tryb stawiania: duch na kafelku, okrąg zasięgu światła, licznik oświetlonych grządek, kupno lampy, przenoszenie |
| `Scripts/UI/ExpansionMode.cs`, `TileFrame.cs` | tryb rozbudowy: ramki z ceną lub „?”, dymek z kosztem, kupno, przekopywanie |
| `Editor/PixelArtImporter.cs` | automatyczne ustawienia importu sprite'ów + pivoty z `pivots.json` |
| `Editor/WyspaSetup.cs` | menu „Wyspa”: ustawienia projektu, dane (SO, także `Data/UiTheme.asset`), budowa sceny z `prototype_layout.json` (z obiektem UI) |

Wartości startowe (do balansu): marchew 600 s / 5 / 12, zboże 900 s / 8 / 18, dynia 1500 s / 15 / 40 (wzrost / cena nasiona / cena sprzedaży).
Lampy (Esencji na minutę): latarnia 0.1, czaszka 0.05, ognik 0.2 (wzrost ×1.5), grzyby 0.

**Następne etapy:** patrz §11.7.

---

## 11. Ulepszenia i progresja (przyjęte)

### 11.1 Zasady
1. **Każde ulepszenie widać na wyspie** (nowy obiekt, efekt, światło). Gracz zerka na okienko i widzi postęp.
2. **Wybory zamiast samych liczb** (np. lampa: zasięg ALBO oszczędność).
3. **Tempo w dniach i tygodniach pracy**, nie w minutach.
4. **Zero kar i FOMO.** Przegapione nie przepada.
5. **Gra nie nagradza przepracowania** (zmęczenie, bonus za przerwy).

### 11.2 Waluty
| Waluta | Źródło | Zastosowanie |
|---|---|---|
| Monety | sprzedaż plonów dnia | podstawowe zakupy |
| Esencja | aktywna praca | lampy, rytuały, przyspieszenia |
| Plony nocy | rośliny nocne | ulepszenia magiczne, automatyzacja |
| Odłamki gwiazd | spadające gwiazdy | lampy wyższego poziomu, latarnia morska |
| Łuski / świecące ryby | łowienie, sieci | klatka z ognikiem, latarnia morska |
| Dary z głębin | wynurzenia potwora | unikaty, końcówka Księgi rytuałów |
| Drewno | usuwanie drzew, krzaków, pni | budowle drewniane: pomost, skrzynie, kompostownik, łódź |
| Kamień | usuwanie kamieni, gruzu, nagrobków | budowle kamienne: ołtarz, latarnia morska, palenisko |

### 11.3 Drzewka

**A. Wyspa**
- **Rozbudowa kafelkami** (zastępuje „4 stopnie powiększenia”): kupujesz pojedyncze kafelki 16×16 przy brzegu, szczegóły w **§13**.
- **Strefy:** cmentarzysko (rośliny nocy ×1.5), bagno (grzyby, dziwne zioła), ruiny (miejsce na ołtarz). **Nie kupuje się ich, tylko odkrywa** rozbudową w ich stronę (§13.4).
- **Kompostownik:** niesprzedane i zepsute plony zamienia w nawóz (+25% szybkości wzrostu na jedną uprawę, +1 gwiazdka jakości).

**B. Esencja i rytm pracy (wyróżnik gry)**
- **Słój:** pojemność 60, potem 120, 240, 480. Koszt: 100 / 400 / 1500 monet.
- **Seria skupienia:** po 25 min pracy bez przerwy (przerwy < 2 min się nie liczą) Esencja +25%, aż do przerwy.
- **Bonus za przerwę („odświeżony”):** przerwa 5–30 min daje +15% Esencji przez następne 25 min.
- **Zmęczenie:** po 8 h aktywności w dobie Esencja ×0.5, po 10 h ×0.25. Doba resetuje się o 4:00. Łagodnie, bez kar.
- **Statystyki:** przepracowane dziś / w tygodniu, najdłuższa seria, liczba przerw.

**C. Lampy:** każda ma 3 kierunki ulepszeń (gracz wybiera):
- **Knot:** zużycie −30% / −50%.
- **Soczewka:** zasięg +25% / +50%.
- **Kolor ognia** (efekt specjalny): czerwony nie przeszkadza roślinom nocy, zielony przyciąga ćmy (+Esencja), biały przyspiesza dojrzewanie.
- **Latarnia morska** (2000 monet + 10 odłamków): rozświetla mgłę, zdarzenia i kupiec przychodzą częściej.

**D. Rośliny**
- Kolejne nasiona dnia i nocy (sklep i kupiec-widmo).
- **Mutacje:** przy zbiorze, jeśli sąsiednia grządka (4 kierunki) ma inną roślinę, jest 3% szansy na nasiono hybrydy
  (np. dynia-upiór). Hybrydy wyglądają inaczej i są droższe.
- **Jakość 1–3 gwiazdki:** baza 1, +1 za nawóz, +1 za wzrost w świetle ognika. Cena ×1 / ×1.5 / ×2.5. Kupiec składa zamówienia na jakość.

**E. Automatyzacja (kamienie milowe, bez niej gra przeszkadza w pracy)**
**Stan (Etap 6b):** ulepszenie odblokowuje rodzaj budowli i daje pierwszą sztukę za darmo; kolejne w Sklep → Budowle
(monety x1.6 za każdą posiadaną sztukę + stały materiał). Budowle stawia się i przenosi jak lampy.
1. **Ognik-ogrodnik** (100 monet od Etapu 6d, wcześniej 300 + 5 plonów nocy): co 30 s wylatuje na kurs: zbiera **do 3** dojrzałych plonów z pól
   **5x5 wokół siebie** (po kolei najbliższe), niesie je do najbliższej skrzyni z miejscem i wraca. Lot płynny, po łuku. Kolejne: 250 monet + 5 plonów nocy. **Skrzynia na plony** (20 miejsc): 60 monet + 4 drewna.
   Gdy nie ma dojrzałych plonów, leci do **owocującego drzewa** w zasięgu: dwa razy uderza w koronę, owoce spadają i leżą
   pod drzewem, ognik zbiera je z ziemi (do 3 na kurs) i niesie do skrzyni (sprzedaż po cenie owocu). Resztę zbierze następnym
   kursem albo gracz (klik w drzewo). Leżące owoce są w zapisie.
2. **Runa siewu** (dawna pamięć ziemi, 500 monet): sadzi na pustych grządkach **3x3 wokół siebie** (co 2 s). Klik w runę →
   „Nasiona”: pamięć ziemi (to samo, co rosło), wybrana roślina albo nic. Nasiono z worka, inaczej za monety. Kolejne: 150 monet + 5 kamienia.
3. **Duszek-handlarz** (dawna skrzynia kupca, 800 monet): **od Etapu 6d (prośba gracza) stoi przy jednej skrzyni** (bez
   własnego kafelka, unosi się obok niej) i sprzedaje ją po 90% **od razu, gdy się zapełni** (20/20), zamiast raz na godzinę.
   Kupiony w sklepie: klik w skrzynię bez duszka. Przeniesienie skrzyni zabiera duszka, rozbiórka skrzyni też (zwrot połowy).
   Duszki ze starych zapisów stają przy skrzyni bez duszka (a gdy jej nie ma, na ich kafelku pojawia się skrzynia).
   Kolejne: 500 monet + 8 kamienia.
4. **Sieci rybackie** (wymagają pomostu): bierny połów co godzinę.

**F. Morze**
- **Pomost** (400 monet): łowienie (klik w rybę przy brzegu).
- **Łódź i wyprawy** (800 monet): łódź płynie w mgłę na N godzin **aktywnej pracy** (np. 4 h) i wraca z nasionami, surowcami
  i fragmentami lore. Jedna wyprawa naraz, a ulepszenia łodzi skracają czas i powiększają ładownię.
- **Ołtarz ofiarny na brzegu:** wymiana z potworem.

**G. Rytuały i księżyc (późna gra)**
- **Księga rytuałów:** stałe pasywne bonusy za rzadkie surowce (np. „rośliny nocy rosną też o zmierzchu”).
- **Cykl księżycowy zamiast prestiżu:** co prawdziwe 29,5 dnia (od nowiu do nowiu) cykl się zamyka i daje punkty do Księgi
  za osiągnięcia z tego cyklu. **Wyspa nic nie traci** (świadomie bez resetu postępu).

**H. Kolekcje i lore**
- **Bestiariusz:** ryby, rośliny, hybrydy, zdarzenia, potwór. Ukończenie kategorii daje nagrody.
- **Lore z wypraw i wraków** stopniowo odsłania historię wyspy, mgły i potwora (narracja bez postaci i dialogów).

**I. Wygoda (opcje, nie sklep)**
- Okno: przezroczyste, zawsze na wierzchu, przyklejone do rogu ekranu, mini-tryb.
- Ikona w zasobniku systemowym i powiadomienie „plony dojrzały”.
- Suwak jasności nocy, dźwięki ambientu (fale, ogień) z głośnością.

### 11.4 Tempo rozwoju (cel)
| Okres | Zawartość |
|---|---|
| Dzień 1–3 | 3 rośliny dnia, pierwsza latarnia, słój, 1. powiększenie |
| Tydzień 1 | rośliny nocy, kupiec-widmo, pomost i łowienie, **ognik-ogrodnik** |
| Tydzień 2–4 | strefy (cmentarzysko), mutacje, wyprawy łodzią, latarnia morska |
| Miesiąc+ | Księga rytuałów, cykle księżyca, bestiariusz, potwór i lore |

### 11.5 Priorytety (kolejność wdrażania)
1. Automatyzacja (ognik-ogrodnik, pamięć ziemi).
2. Rytm pracy (seria, przerwa, zmęczenie).
3. Rozbudowa wyspy kafelkami i cofanie mgły (§13).
4. Wyprawy łodzią.
5. Mutacje.

### 11.6 Technicznie (plan)
- `UpgradeDefinition` (SO): id, nazwa, opis, kategoria, poziomy (koszt w wielu walutach), wymagania (inne ulepszenia), efekt.
- `UpgradeState`: posiadane poziomy (w zapisie). Efekty jako małe klasy/strategie odpytywane przez systemy
  (np. `EssenceStore` pyta o mnożnik, `Lamp` o zużycie i zasięg), żeby nie rozsiewać `if`-ów po kodzie.
- `Currencies`: jeden portfel na wiele walut (enum), zamiast osobnych klas.
- `WorkRhythm`: śledzi serię, przerwę i dzienną sumę aktywności, zwraca mnożnik Esencji.
- `HarvestCrate` + `WispGardener`: automatyczny zbiór do skrzyni, `AutoSeller`.
- `Expedition`: stan łodzi (licznik aktywnych sekund, tabela łupów).
- `Mutation`: tabela par roślin i hybrydy, rzut przy zbiorze.

### 11.7 Etapy implementacji
- **Etap 1 (gotowy):** szkielet systemów (§10).
- **Etap 2a (gotowy, do testu w Unity):** waluty, fundament ulepszeń (dane + zakup w HUD debugowym), słój Esencji, rytm pracy, zapis v2. Rośliny nocy dają „plony nocy”.
- **Etap 2b (gotowy, do testu w Unity):** automatyzacja: skrzynia na plony, ognik-ogrodnik, pamięć ziemi, skrzynia kupca (obiekty pojawiają się po zakupie).
- **Etap 2c (gotowy, do testu w Unity):** docelowe UI z lokalizacją: HUD, koło nasion, Księga (Ulepszenia, Statystyki, Opcje),
  toasty, dymki. Sklep, Rytuały i Bestiariusz w Księdze mają na razie „Wkrótce”. CJK czeka na fonty Fusion Pixel.
- **Etap 3a (gotowy, do testu w Unity): żywa wyspa.** Shadery: kołysanie roślin i drzew (całe wiersze pikseli, skokowo),
  kontur przy najechaniu i puls „gotowe”, woda liczona w shaderze (ryby, potwór, połyski, grzbiety fal, przybój, piana), mgła
  przy krawędziach (gęstsza w nocy, bliżej przy zgaszonych lampach). Cząsteczki: zbiór, sadzenie, wzrost, iskierki, świetliki,
  spadające liście, monety lecące do licznika. Przełącznik shaderów w HUD debugowym (F1).
- **Etap 3b (gotowy, do testu w Unity): wyspa z kafelków.** Start 2×2 z ziarna, świat 32×32 (strefy i znaleziska w danych),
  tryb rozbudowy (ramki, ceny, „?”), mgła cofa się od nowego brzegu, okno i kadr rosną z wyspą, przekopywanie trawy na grządkę,
  znaleziska (monety, nasiona nocy, odłamek, lore, źródło, pradawny kamień), zapis v3.
  Strefy mają na razie zwykłą trawę (wygląd stref: Etap 5).
- **Etap 3c (gotowy, do testu w Unity):** sklep lamp (Księga → Sklep), tryb stawiania i przenoszenie obiektów,
  panel kontekstowy, usuwanie dekoracji z nagrodami (drewno, kamień, bonusy), dekoracje na 25% nowych kafelków,
  wynurzanie lądu. Pasma światła Bayera (§4.4) później.
- **Etap 4 (gotowy, do testu w Unity):** jakość i mutacje (dynia-upiór), pełnia (rośliny nocy x2, krwawa róża),
  worek nasion i nawóz, kupiec-widmo, pomost i łowienie, sieci, łódź i wyprawy. Grafiki roślin nocy były gotowe wcześniej.
- **Etap 5a (gotowy, do testu w Unity):** strefy: podłoża, dekoracje, zabytki z lore, bonusy ziemi, stworzenia tła.
- **Etap 5b (gotowy, do testu w Unity):** nocne zdarzenia (błędne ogniki, spadająca gwiazda), potwór i ołtarz, latarnia morska.
- **Etap 5c (gotowy, do testu w Unity):** Księga rytuałów i cykl księżyca, bestiariusz, kolekcja lore.
- **Etap 6a (gotowy, do testu w Unity i w buildzie):** opcje w sekcjach, tryby okna (ramka, na wierzchu, róg, mini, przezroczystość),
  start z Windows, godziny nocy, próg bezczynności, reset zapisu.
- **Etap 6b (gotowy, do testu w Unity):** 6 nowych nasion do odblokowania, automatyka w wielu egzemplarzach (ogniki 5x5, runy
  siewu 3x3 z wyborem nasion, duszki-handlarze, skrzynie), Sklep: Nasiona / Lampy / Budowle / Morze, mgła w przezroczystym oknie.
- **Etap 6c:** dźwięk (ambient, efekty, głośność), ikona w zasobniku i powiadomienia (plony dojrzały, kupiec, potwór).
- **Etap 6d (gotowy, do testu w Unity):** HUD jako pasek nad wyspą (§12.2), wprowadzenie z tablicą ogłoszeń (§12.8), balans
  (symulacja `tools/balance/sim.py`, tempo szybkie), ogniki zbierają owoce, duszek-handlarz przy skrzyni, gęstsza mgła, wiatr falą. Zapis v8.

---

## 12. UI i menu
Makiety: `docs/img/ui_hud.png`, `ui_ksiega_sklep.png`, `ui_ksiega_ulepszenia.png`, `ui_stawianie_noc.png`, zestaw: `ui_zestaw.png`.

### 12.1 Zasady
1. **Najpierw nastrój:** gra wisi na pulpicie, więc UI ma nie przeszkadzać. **HUD znika** po ok. 5 s bez kursora nad oknem
   i wraca po najechaniu. Toasty pojawiają się na chwilę.
2. **Ta sama siatka pikseli co gra** (natywnie 272×200, skala całkowita). Bez antyaliasingu.
3. **Font 3×5, wielkie litery, z polskimi znakami** (`WyspaPixel.ttf`, własny, bez licencji zewnętrznych).
   Małe litery mapują się na wielkie.
4. **Ciemne panele ze śliwkowego drewna z nitami, a treść na pergaminie.** Główne menu to **Księga** (grimoire).
5. **Kontekst przed menu:** większość akcji robi się klikając na wyspie. Księga służy do zakupów i przeglądania.
6. Kolory akcentów: **złoty** (monety, posiadane), **turkus** (magia, zdarzenia, Esencja rytmu), **pomarańcz** (Esencja, ogień),
   **szary/zablokowane** z kłódką.

### 12.2 HUD
- **Pasek nad wyspą (Etap 6d):** górny wiersz: monety, Esencja (ikona + pasek + liczby), plony nocy, odłamki gwiazd, drewno, kamień
  (waluty pojawiają się dopiero po zdobyciu); dolny wiersz: rytm pracy, pasek serii, tempo, zegar. Pasek idzie za lądem.
- **Menu:** pierścień akcji pod prawym klikiem na wyspie (Księga, Sklep, Opcje, Rozbudowa), §12.10. Przycisków w rogu okna już nie ma.
- Rytm pracy (dolny wiersz paska): ikona stanu (seria / odświeżony / zmęczenie), opis („SERIA 32 MIN +25%”), pasek, zegar.
- **Mini-tryb:** HUD całkiem ukryty, zostaje tylko wyspa.

### 12.3 Interakcje na wyspie
- **Klik pustej grządki:** **koło nasion** (menu radialne) z paczkami nasion. Najechanie pokazuje tooltip
  (nazwa, cena, sprzedaż, czas wzrostu). Środek koła lub PPM = anuluj. Niedostępne nasiona są wyszarzone.
- **Klik dojrzałej:** zbiór (cząsteczki, monety lecą do licznika).
- **Najechanie na rosnącą:** tooltip z fazą, czasem do dojrzenia i **warunkami** (czy ma światło, czy gracz jest aktywny, czy noc).
- **Obiekty** (skrzynia, łódź, ołtarz, kupiec, słój) otwierają małe panele kontekstowe przy obiekcie.

### 12.4 Księga (główne menu)
Lewa kolumna: zakładki-ikony. Prawa strona to pergamin z treścią. Na dole pasek opisu zaznaczonego elementu i przycisk **KUP + cena**.
- **Sklep:** podzakładki Nasiona / Lampy / Budowle / Morze. Siatka slotów z ceną, a zablokowane mają kłódkę.
- **Ulepszenia:** kolumny drzewek (Automatyka, Rytm pracy, Wyspa, Lampy, …). Węzły: posiadane (złota ramka),
  dostępne (zwykła), zablokowane (kłódka, linia przerywana). Pasek opisu zawiera wymagania.
- **Rytuały:** Księga rytuałów + postęp bieżącego cyklu księżyca (faza, punkty).
- **Bestiariusz:** kolekcje (ryby, rośliny, hybrydy, zdarzenia, potwór) i fragmenty lore.
- **Statystyki:** praca dziś / w tygodniu, najdłuższa seria, liczba przerw, wykres godzinowy.
- **Opcje** (patrz 12.7).

### 12.5 Tryb stawiania (lampy, budowle)
**Stan (Etap 3c):** działa dla lamp ze sklepu i przenoszenia obiektów (przycisk „Przenieś” w panelu obiektu). Duch jest
półprzezroczysty (alfa, nie dithering), okrąg zasięgu i licznik grządek jak niżej.
- Półprzezroczysty (dithering) „duch” obiektu pod kursorem, przyciągany do siatki.
- Zasięg światła jako przerywana elipsa. Grządki w zasięgu mają podświetlone narożniki, a licznik pokazuje „OŚWIETLI: N GRZĄDEK”.
- Kolizja albo złe miejsce: duch na czerwono. LPM = postaw, PPM = anuluj.

### 12.6 Powiadomienia (toasty)
- Prawy górny róg, maks. 2 naraz, ok. 4 s, kolejka. Pasek akcentu z lewej w kolorze typu.
- Zdarzenia z czasem (kupiec-widmo) pokazują odliczanie. Później powiadomienia także w zasobniku systemowym (tray).

### 12.7 Opcje
**Stan (Etap 6a):** sekcje Ogólne / Okno / System jak niżej, bez dźwięku (6b). Godziny nocy krokami po 1 h.
- **Okno:** skala ×2/×3, zawsze na wierzchu, przezroczyste tło, przyklejenie do rogu ekranu, mini-tryb.
- **Noc:** godziny nocy, minimalna jasność (suwak).
- **Praca:** próg bezczynności (domyślnie 60 s), rytm pracy i zmęczenie (włącz/wyłącz).
- **Dźwięk:** ambient, efekty.
- **System:** start z Windows, język (PL, później EN), reset zapisu (z potwierdzeniem).

### 12.8 Wprowadzenie (bez postaci)
Karteczki przybite do skrzyni i tablicy na wyspie, 3–4 kroki: posadź, pracuj (patrz jak rośnie), zbierz, kup lampę.
Cel jest podświetlony pulsem.

**Stan (Etap 6d):** `UI/IntroNotes.cs` + `UI/NoticeBoard.cs`. Tablica stoi na nowej wyspie (Island.SpawnBoard), kroki:
1. posadź (strzałka nad pustą grządką), 2. pracuj, aż urośnie (strzałka nad rośliną), 3. zbierz, 4. dokup kafelek
(prawy klik → Rozbudowa; ten krok uczy pierścienia akcji), 5. kup lampę i postaw ją na nowym kafelku, 6. „gotowe”
(strzałka nad tablicą). Kafelek przed lampą (po teście): na wyspie 2x2 (2 grządki, tablica, latarnia) nie ma wolnego miejsca.
Po wykonanym kroku karteczka odpada (iskierki) i przybija się nowa. Karteczka z treścią wisi pod wyspą (pasek HUD jest nad nią)
i chowa się razem z HUD-em. Klik w tablicę: treść + „Rozbierz” (koniec wprowadzenia w każdej chwili, 3 drewna).
Krok zapisany w `SaveData.intro` (v8); zapisy sprzed v8 kończą wprowadzenie od razu.

### 12.9 Technicznie (Unity)
- **uGUI:** Canvas Screen Space - Overlay + Canvas Scaler **Constant Pixel Size**, `scaleFactor` = skala okna (całkowita),
  **Reference Pixels Per Unit = 16**. Obrazy typu **Sliced** ze sprite'ów `Art/Sprites/UI/Kit`
  (granice 9-slice ustawia `PixelArtImporter` z `pivots.json`).
- **Stan (Etap 2c):** całe UI budowane z kodu (`UiRoot`), motyw w `Data/UiTheme.asset` (tworzy go Wyspa > 2).
  Font asset TMP powstaje w locie z `WyspaPixel.ttf` (RASTER, atlas dynamiczny 256×256, filtr Point), więc Font Asset Creator nie jest potrzebny.
  Tekst ma rozmiar 8 (jednostki UI), a ×2 daje skala płótna.
- **Font (gdyby trzeba było zrobić asset ręcznie):** TextMeshPro, Font Asset Creator ze źródłem `Art/Fonts/WyspaPixel.ttf`:
  - Sampling Point Size 8 (albo wielokrotność), Render Mode **RASTER** (bez wygładzania), Padding 1, atlas 256×256;
  - znaki: ASCII + `ĄĆĘŁŃÓŚŹŻąćęłńóśźż`;
  - rozmiar tekstu w UI = 8 × skala.
- **Makiety do zrobienia:** Rytuały, Bestiariusz, Statystyki, Opcje, panel łodzi/wyprawy, handel z kupcem.

### 12.11 Menu główne i zapisy (prośba gracza, po 6d)
- Gra startuje w **menu głównym**: panel z drewna na tle przyciemnionej wyspy (stała wyspa z ziarna, bez zapisu, bez HUD-u
  i powiadomień). `UI/MainMenuView.cs`, stan w `SaveSystem.CurrentSlot` (-1 = menu).
- **3 sloty zapisu** (`save_1.json`..`save_3.json`). Slot pokazuje nazwę i **datę z godziną zapisu** (format języka gry).
  Pełny: „Graj” i „Usuń” (drugi klik w 4 s potwierdza). Pusty: „Nowa gra” → pole na **nazwę do 10 znaków** (domyślnie
  „Wyspa N”), Enter albo „Start”. Wybór slotu ładuje scenę od nowa już z tym zapisem.
- Na dole: język (klik = następny) i wyjście z gry. W grze pierścień akcji ma „Menu główne” (zapis i powrót do menu).
- Stary `save.json` przenosi się sam do slotu 1. Reset zapisu w Opcjach czyści tylko bieżący slot (nazwa zostaje).

### 12.10 Do przemyślenia: UI na pulpicie (prośba gracza po Etapie 6a)
**Stan:** pierścień ma Księgę, Sklep, Rozbudowę, Opcje i Wyjście; PPM na grządce dokłada „Zasyp grządkę”.
Problem: HUD (waluty u góry, trzy przyciski w prawym górnym rogu, rytm na dole) przykleja się do rogów okna, a nie do wyspy.
W przezroczystym oknie wisi „w powietrzu” nad pulpitem. **Wybrano C** (zrobione po Etapie 6b). Rozważane warianty:
- **A. HUD przy wyspie:** jeden mały pasek nad wyspą (waluty + Esencja), pokazuje się po najechaniu na wyspę.
- **B. Menu z wyspy:** zamiast trzech przycisków jeden „kamień z Księgą” stojący na wyspie (klik = Księga, PPM = rozbudowa).
- **C. Pierścień akcji:** PPM na pustym miejscu wyspy otwiera koło (Księga, Rozbudowa, Sklep, Opcje), jak koło nasion.
- **D. Zostaje jak jest,** ale w przezroczystym oknie HUD ma półprzezroczyste tło i chowa się szybciej.

---

## 13. Rozbudowa wyspy kafelkami
Prototyp: `docs/img/wyspa_starty.png` (6 losowych startów 2×2), `docs/img/swiat_mapy.png` (3 losowe światy 32×32),
`docs/img/wyspa_etapy.png` (wzrost wyspy, wczesny prototyp ze startem 5×4), `docs/img/wyspa_rozbudowa.png` (tryb rozbudowy).
Skrypty: `tools/pixel-proto/tiles_proto.py`, `world_proto.py`.

### 13.1 Zasada
- Wyspa to **siatka kafelków 16×16** (1 kafelek = 1 grządka = jedno miejsce na mały obiekt).
- Gracz **kupuje pojedyncze kafelki przylegające do lądu** (4 kierunki), a wyspa rośnie o jeden kwadracik.
- **Start: wyspa 2×2 kafelki o losowym kształcie** (kształt brzegu z ziarna). Na niej **2 grządki** (losowe miejsca),
  a na reszcie losowy obiekt (drzewo, krzak, kamień albo martwe drzewo). Start w środku świata, na kafelkach (15–16, 15–16).
- **Brzeg jest organiczny, choć logika jest kwadratowa:** maska z kafelków jest zaokrąglana (otwarcie i domknięcie morfologiczne)
  i zaburzana stałym szumem. Potem dochodzą klif, piana, cień, strefy wody i mgła (jak w prototypie).
  Szum jest przypięty do miejsca w świecie, więc zakup zmienia tylko brzeg w pobliżu.

### 13.2 Koszt
- **Monety + Esencja.** Praca dosłownie „wyrywa ląd z mgły”.
  n = kafelki kupione od startu. Krzywa kwadratowa, bo wykładnicza nie pasuje do świata 32×32:
  - Monety: `10 + 20n + 1.5n²` (balans po cyklu gry i wzroście x3; wcześniej `10 + 8n + 0.5n²`, na początku `10 + 4n + 0.06n²`),
    a pierwsze 20 kafelków taniej (prośba gracza): mnożnik `0.4 + 0.6n/20`, czyli 5. kafelek 81, 10. 252, 15. 550 monet, min. 10;
  - Esencja: `3 + n/3` (wcześniej `3 + n/4`, na początku `3 + n/8`).

  | n | 0 | 10 | 50 | 100 | 300 | 600 | 1020 |
  |---|---|---|---|---|---|---|---|
  | monety | 10 | 252 | 4760 | 17010 | 141010 | 552010 | 1581010 |
  | Esencja | 3 | 6 | 19 | 36 | 103 | 203 | 343 |

  Wysoka cena w Esencji późno w grze wymusza ulepszanie słoja, co jest celowym powiązaniem systemów.
- Ostatnie kafelki świata (krawędź mapy, wnętrza stref) mogą wymagać rzadkich surowców (odłamki gwiazd, dary z głębin).

### 13.3 Co można zrobić z kafelkiem
- Po zakupie to **trawa**.
- Kafelek można **przekopać na grządkę** (tanio) albo **postawić na nim obiekt**: lampa i małe budowle zajmują 1 kafelek,
  duże (latarnia morska, ołtarz, łódź z pomostem) kilka.
- Zasięg lamp liczony w kafelkach upraszcza oświetlenie i podgląd „OŚWIETLI: N GRZĄDEK”.
- Kafelki stref mają własne podłoże i bonus, np. na cmentarzu rośliny nocy rosną ×1.5.
- Pomost stawia się na **kafelkach wody** przy brzegu.

### 13.4 Co jest pod mgłą
- **Świat 32×32 kafelki, losowany z ziarna zapisu** (każda gra inna):
  - **Strefy** (cmentarz, bagno, ruiny): każda to losowa plama 8–14 kafelków, 6–12 kafelków od startu,
    w różnych kierunkach (min. 90° odstępu).
  - **Znaleziska:** ok. 5% kafelków, 12% w pobliżu stref.
  - **Dziwne kafelki:** 1× źródło, 1× pradawny kamień, co najmniej 5 kafelków od startu.
- **Bez podpowiedzi** (decyzja gracza): nie ma znaczników ani zarysów stref we mgle. Gracz odkrywa strefy sam,
  rozbudowując wyspę. Odrzucony prototyp znaczników został usunięty.
- **Znaleziska:** część kafelków ma niespodziankę. Przylegające do lądu są oznaczone **„?”** zamiast ceny.
  Mogą zawierać skrzynkę monet, odłamek gwiazdy, nasiono nocne, fragment lore albo rzadko „dziwny kafelek”
  (źródło: +wzrost wokół; pradawny kamień: rytuał).
- Wszystko (kształt startu, strefy, znaleziska) wynika z jednego ziarna, więc ten sam zapis zawsze daje ten sam świat.

### 13.5 Efekt zakupu
- Tryb rozbudowy: przyległe kafelki mają przerywane ramki z ceną albo „?”. Najechanie pokazuje tooltip (monety + Esencja).
  LPM = kup, PPM = wyjdź.
- Po zakupie mgła się rozstępuje, ląd wynurza się z wody (2–3 skokowe klatki), pojawia się piana i cząsteczki,
  a znalezisko wyskakuje jak plon przy zbiorze.

### 13.6 Okno i kamera
- **Start: kadr natywny 144×112** (9×7 kafelków), przy ×3 to okno 432×336, mały widżet na pulpit.
- Natywny kadr rośnie razem z wyspą **krokami po 16 px** (skala zostaje całkowita), aż do limitu z opcji (np. 40% ekranu).
- Po przekroczeniu limitu kamera przewija się (przeciąganie PPM), a gra sama centruje się na wyspie.
  Do wyboru jest też przełączanie skali ×3/×2.
- Mini-tryb pokazuje tylko wycinek z grządkami.

### 13.7 Technicznie
**Stan (Etap 3b):** jak niżej, z dwiema zmianami: brzeg liczony dokładną transformatą odległości (Felzenszwalb), a nie przesuwaniem
masek, a mgła to shader czytający mapę odległości od brzegu (`Fog.shader`, od 22 px, gęstnieje przez 40 px). Tekstury mają 640×640
(świat 512 px + 64 px zapasu), środek świata w (0,0). Malowanie po zakupie trwa ok. 20–150 ms.
- `IslandMap`: `HashSet<Vector2Int>` kafelków + `Dictionary<Vector2Int, TileType>`, ziarno świata, granice mapy **32×32**.
- `WorldGenerator`: z ziarna wylicza kształt startu, strefy, znaleziska i dziwne kafelki (algorytm jak w `world_proto.py`).
- `ExpansionShop`: lista przyległych kafelków, koszt, znaleziska z `WorldGenerator`.
- **Render (rekomendacja):** generowana w locie tekstura wyspy jak w prototypie: maska, zaokrąglenie z szumem, klif, piana,
  potem `Texture2D` i Sprite. Przeliczana tylko po zakupie, a dla tak małej mapy to ułamek sekundy.
  Mapa odległości (woda, mgła) przeliczana razem z nią.
  Alternatywa: Tilemap z autotilingiem dual-grid (16 wariantów na teren). Bardziej standardowa, ale trudniej o nieregularny brzeg.
- Zapis: lista kafelków z typami + ziarno.

### 13.8 Strefy: wygląd
**Stan (Etap 5a):** zrobione jak niżej; podłoże liczy `IslandPainter.Surface`, dekoracje i stworzenia `Island` (pule stref).
Podgląd: `docs/img/wyspa_strefy.png` (wyspa z trzema strefami), `docs/img/arkusz_strefy.png`.
- **Podłoża są proceduralne**, z hasha pozycji w świecie, więc kafelki łączą się bez szwów:
  - cmentarz: ciemna trawa, płaty ziemi, kamyki, kosteczki;
  - bagno: kałuże, błoto, ciemna trawa;
  - ruiny: płyty kamienne z fugami, pęknięcia, mech.
- **Granice między podłożami są rozmyte szumem**, żeby nie było widać kwadratów kafelków.
- **Dekoracje pojawiają się na kafelkach strefy przy odkryciu** (losowo z puli strefy).
  Świecą: świece, palenisko, tablica runiczna, grzyby na kłodzie, kwiat lilii.
- **Życie stref:** wrona na cmentarzu, żaba i bąble na bagnie. To stworzenia tła, nie postacie.
- **Zabytki** (krypta, posąg, kamienne schody) są **nieusuwalne**. Kliknięcie daje fragment lore albo otwiera interakcję
  (np. rytuał przy posągu).
- **Bonus strefy wynika z podłoża**, a nie z dekoracji. Na cmentarnej ziemi można założyć grządkę i zachowuje bonus
  (rośliny nocy ×1.5).

---

## 14. Usuwanie dekoracji
Podgląd: `docs/img/wyspa_strefy.png` (drzewo w trakcie wycinki), `docs/img/arkusz_usuwanie.png`.

### 14.1 Zasady
- Kliknięcie w dekorację (drzewo, świerk, martwe drzewo, krzak, kamień, nagrobek, gruz, dekoracje stref poza zabytkami)
  otwiera tooltip **„USUŃ”** z kosztem w Esencji i nagrodą. LPM potwierdza.
- Usunięcie zwalnia kafelek pod grządkę lub budowlę. Dotyczy to też losowego obiektu na startowej wyspie 2×2.
- Usuwa się **na zawsze**: dekoracje nie odrastają. Nowe pojawiają się tylko na nowo odkrytych kafelkach (ok. 25%).

### 14.2 Animacje (skokowe, ok. 10 kl./s)
| Dekoracja | Przebieg | Zostaje |
|---|---|---|
| Drzewo | potrząśnięcie (shader albo przesunięcie o 1 px), przewracanie (4 klatki), wióry i liście, kurz | **pień** (można zostawić albo wykarczować) |
| Kamień | pęknięcie, rozpad, odpryski i kurz | **gruz** (wywozi się jak pień) |
| Krzak | ścięcie, liście | ścięty krzak, znika po chwili |
| Nagrobek | pęknięcie, odpryski | rozbity nagrobek (można wywieźć) |

### 14.3 Koszty i nagrody (wstępne)
| Co | Esencja | Nagroda | Szansa na znalezisko |
|---|---|---|---|
| Drzewo | 5 | 3 drewna (pień: +1 drewno za 1 Esencji) | gniazdo z nasionem 10% |
| Świerk | 4 | 2 drewna | |
| Martwe drzewo | 3 | 2 drewna | |
| Krzak | 2 | 1 drewno + jagody (sprzedaż) | |
| Kamień | 6 | 3 kamienia | monety pod kamieniem 15% |
| Nagrobek | 3 | 2 kamienia | kości / fragment lore 10% |
| Gruz | 2 | 1 kamień | |

**Materiały (przyjęte):** drewno i kamień to waluty do budowli (patrz §11.2).
Pętla: odkryj kafelek, usuń dekorację, zbuduj. Odnawialne źródło drewna to drzewa sadzone przez gracza (§15, na później).

### 14.4 Technicznie
**Stan (Etap 3c):** `Decoration` + konfiguracja w `IslandArt.decorations` (koszty i nagrody z tabeli wyżej), zapis rodzaju
dekoracji na kafelku. Nagrobki i dekoracje stref przyjdą ze strefami (Etap 5).
- `Decoration` (MonoBehaviour) + `DecorationDefinition` (SO): koszt, nagrody, szansa znaleziska, klatki usuwania,
  sprite pozostałości, flaga `isLandmark` (nieusuwalne).
- Zapis: lista dekoracji na kafelkach (typ + stan: cała / pień / usunięta).

---

## 15. Na później (backlog)

### 15.1 Drzewa do sadzenia i owoce
- **Sadzonki różnych drzew** kupowane w sklepie albo znajdowane (gniazdo pod wyciętym drzewem, wyprawy).
  Drzewo zajmuje 1 kafelek.
- **Wzrost długi** (dni aktywnej pracy, np. 2–5). Fazy: sadzonka, młode drzewo, dorosłe, owocujące.
- **Owoce odrastają cyklicznie** z dojrzałego drzewa: zbiór kliknięciem, a później przez ognika-ogrodnika. Drzewo się nie zużywa.
- **Wycięcie dorosłego drzewa** daje więcej drewna niż dzikie. To odnawialne źródło drewna, ale tracisz owoce.
- Szkic gatunków w klimacie dark fantasy (do decyzji):
  - jabłoń (zwykłe owoce, dzień);
  - czarny bez (owoce na mikstury lub rytuały);
  - upiorna grusza (owocuje nocą, owoce świecą);
  - krwawa jarzębina (rośnie lepiej na cmentarzu);
  - drzewo księżyca (srebrne owoce tylko w pełnię).
- **Assety gotowe** (`Assets/_Project/Art/Sprites/Trees`, ikony w `UI/Icons`): fazy 0–3 każdego gatunku,
  przewracanie dorosłego (`tree_<gatunek>_fall_0..3`), ikony owoców i sadzonki. Po zbiorze owoców drzewo wraca do sprite'a „dorosłe”,
  a po wycięciu zostaje pień z `Removal`.
- **Więcej koloru** (prośba gracza): jaśniejsze, nasłonecznione liście (`g4`), żywsze owoce (`ro`),
  jarzębina w jesiennych czerwieniach i pomarańczach.
- **Świecące drzewa rozjaśniają okolicę w nocy:** owocujące upiorna grusza i drzewo księżyca są małym źródłem światła
  (pasma z ditheringiem jak lampy):
  - grusza: turkus `(0.45, 1.15, 1.05)`, promień 34 px;
  - drzewo księżyca: lawenda `(0.9, 0.85, 1.25)`, promień 40 px.

  W Unity to Point Light 2D na drzewie, włączany w nocy, gdy drzewo owocuje.
  **Decyzja: światło drzew liczy się jak lampa dla wzrostu roślin.** W zasięgu rosną rośliny dnia (gdy gracz jest aktywny),
  a rośliny nocy nie rosną. Drzewo nie zużywa Esencji, bo to nagroda za długi wzrost.
  Świeci tylko w nocy, kiedy owocuje (po zebraniu owoców gaśnie do czasu odrośnięcia).
  W kodzie: `Illumination.IsLit` uwzględnia owocujące świecące drzewa obok lamp.
- **Stan (zrobione na prośbę gracza):** Sklep → Drzewa (sadzonka stawiana jak lampa), `Farming/FruitTree.cs` + dane
  `TreeSpecies`. Wzrost w **godzinach** aktywnej pracy zamiast dni (szybsza nagroda, do balansu), z tempem pracy.
  Owoce: zwykłe drzewa odrastają od pracy, upiorna grusza nocą, drzewo księżyca tylko w pełnię. **Klik w owocujące drzewo =
  zbiór od razu (monety)**, klik w inne = panel ze stanem i „Zetnij” (drugi klik potwierdza; drewno: dorosłe 6, młode 2).
  Krwawa jarzębina na cmentarzu rośnie x1.5. Świecące owoce: Point Light 2D z wolumetrią + `Illumination.IsLit`.
  Ognik jeszcze nie zbiera owoców (później). Ceny: jabłoń 120 (4x8 co 30 min), bez 200 (5x10 co 40 min), jarzębina 250
  (6x9 co 45 min), grusza 500 + 5 plonów nocy (4x30 co 2 h nocy), drzewo księżyca 800 + 2 odłamki (3x120 co 1 h pełni).

---

## 16. Lokalizacja
**Języki (decyzja gracza):** polski, angielski, niemiecki, japoński, **chiński uproszczony (zh-Hans)**.
Pliki: `Assets/_Project/Localization/glossary.csv` (słownik terminów), `game.csv` i `ui.csv` (teksty),
`docs/localization/strings_sample.csv` (przykłady z odmianą). Format: `key,pl,en,de,ja,zh-Hans,uwagi`, UTF-8.

### 16.1 Zasady
1. **Żadnego tekstu w sprite'ach.** Każdy napis to klucz lokalizacji (TextMeshPro + nasza warstwa `Loc`, §16.4). Nasze sprite'y już są bez tekstu.
2. **Nie sklejamy zdań z kawałków.** Każdy komunikat to jeden szablon z argumentami (`{0}`, `{1}`), bo szyk zdania różni się między językami.
3. **Odmiana przez liczby przez Smart Strings:** `{0:plural:...}` według reguł CLDR (standardowe reguły odmiany przez liczby dla każdego języka).
   - PL ma 3 formy (1 / 2–4 / 5+): „1 grządkę, 2 grządki, 5 grządek”.
   - EN i DE mają 2 formy.
   - JA i ZH nie mają liczby mnogiej, używają liczników (個, 份).
4. **Liczby, ułamki i czas z kultury języka:**
   - 1,250 (EN) / 1.250 (DE) / 1 250 (PL);
   - 0.1 / 0,1;
   - zegar 24 h (PL, DE, JA, ZH) lub zgodny z systemem.
5. **Słownik terminów jest źródłem prawdy** (`glossary.csv`). Nowy termin trafia najpierw do słownika, potem do tekstów.
   Wstępne tłumaczenia przygotował Claude. **JA, ZH i DE wymagają weryfikacji przez native speakera** przed wydaniem.

### 16.2 Fonty
**Stan:** Fusion Pixel **8px proportional** (wersja 2026.09.25) w `Art/Fonts/FusionPixel/` (wariant `ja` i `zh_hans`, licencja
`OFL.txt`, bez zastrzeżonej nazwy). Pobrane ze strony projektu jako WOFF2 i rozpakowane do OTF bez zmian w glifach.
Są **fontami zapasowymi** pikselowego `WyspaPixel` (TMP fallback, RASTER, rozmiar 8, atlas 1024); pierwszy w kolejce jest wariant
języka gry (`UiRoot.OrderCjkFallbacks`). Wszystkie znaki z kolumn JA i ZH w CSV są w fontach (sprawdzone fontTools).
- **JA i ZH: Fusion Pixel Font** (TakWolf), licencja **SIL OFL 1.1** (darmowa, także w grach komercyjnych).
  - Rozmiary 8 / 10 / 12 px, warianty regionalne JA, SC, TC, KR.
  - Wariant **JA** dla japońskiego i **SC** dla chińskiego. Te same znaki Han mają inne kształty w obu językach,
    więc font wybieramy według języka, a nie jeden dla wszystkich.
  - Licencja: dołączamy plik licencji. Zastrzeżona nazwa „Fusion Pixel”: jeśli sami przytniemy lub zmodyfikujemy plik fontu,
    musimy zmienić jego nazwę. Pobranie: GitHub `TakWolf/fusion-pixel-font` (wydania).
  - Alternatywa: Ark Pixel Font (OFL 1.1, 10/12/16 px), ale autor sam zaznacza braki w znakach chińskich.
- **PL, EN, DE:** Fusion Pixel ma też łacinkę. Proponuję **jeden krój dla zwykłego tekstu we wszystkich językach** (spójny wygląd),
  a własny font 3×5 `WyspaPixel` tylko na **cyfry i krótkie nagłówki** (liczniki HUD, ceny).
  `WyspaPixel` obsługuje już PL i DE (Ä Ö Ü, a ß w wielkich literach jako SS).
- **TextMeshPro:**
  - główny font zależny od języka, Fusion Pixel jako fallback (zapasowy font) dla brakujących znaków;
  - dla CJK font asset **dynamiczny** albo statyczny generowany ze znaków z tabel tekstów (mniejszy atlas);
  - zawsze Render Mode RASTER (bez wygładzania), rozmiar = wielokrotność natywnego.

### 16.3 Wpływ na UI
- **Wysokość linii 12 px dla wszystkich języków.** Makiety z §12 (linia 8 px, font 3×5) trzeba poszerzyć,
  bo znaki CJK są nieczytelne poniżej ok. 10 px.
- **UI w siatce ×2, świat w ×3.** Napisy dostają więcej miejsca i są czytelne, a wciąż wyglądają pikselowo
  (popularny kompromis w grach pixel art). Do potwierdzenia testem w Unity.
- **Księga przy małym oknie:** okno startowe (432×336) jest za małe na teksty CJK. Po otwarciu Księgi okno tymczasowo
  powiększa się do minimum (np. 640×480) i wraca po zamknięciu.
- **Długość tekstów:** niemiecki jest zwykle o 20–35% dłuższy (długie złożenia: „Irrlichtgärtner”). Przyciski i etykiety
  mają auto-rozmiar w granicach albo dwie linie. Testujemy na **pseudo-lokalizacji** (wbudowana w Unity Localization):
  sztucznie wydłużone teksty z akcentami.
- **Łamanie linii CJK** (bez spacji): reguły łamania w TextMeshPro (Line Breaking Rules dla JA/ZH).

### 16.4 Technicznie (Unity)
- **Stan (Etap 2c): własna warstwa** `Scripts/Localization/LocCore.cs` (czyste C#, testowalne poza Unity) i `Loc.cs` (Unity):
  - tabele CSV jako TextAsset (lista w `UiTheme.tables`), brak klucza w języku: angielski, potem polski, a na końcu sam klucz (+ ostrzeżenie w konsoli);
  - podzbiór Smart Strings: `{0}`, `{0:N0}`, `{0:0.0}`, `{0:plural:jeden|kilka|wiele}` z regułami CLDR (PL 3 formy, EN/DE 2, JA/ZH 1);
  - liczby według kultury języka; zmiana języka od razu odświeża teksty (`Loc.Changed`, `LocText`).
- **Paczka `com.unity.localization` (plan zapasowy):** klucze i format są zgodne, więc można przenieść tabele do String Tables,
  gdy będą potrzebne Asset Tables (font per język) albo pseudo-lokalizacja.
  - Locale: `pl`, `en`, `de`, `ja`, `zh-Hans`.
- **Wybór języka:** przy pierwszym starcie z `Application.systemLanguage`, a gdy język nie jest obsługiwany, angielski.
  W opcjach lista nazw w ich własnym języku: Polski, English, Deutsch, 日本語, 简体中文.
- **Klucze:** `obszar.element[.wariant]`, np. `crop.carrot.name`, `crop.carrot.desc`, `toast.wisp_harvested`, `ui.shop.title`.
- **Tłumaczenia:** tłumacz pracuje wprost na plikach CSV (ten sam format co `strings_sample.csv`).
- **Lore** (dłuższe teksty) w osobnej tabeli, żeby tłumacz mógł pracować nad nim osobno.

### 16.5 Nazwa gry: Pockland
- **Jedna marka we wszystkich językach.** Logo i tytuł zawsze łacinką „Pockland”.
  - JA: na stronie sklepu można dodać czytanie ポックランド.
  - ZH: nie tłumaczymy fonetycznie (np. „波克兰” myli się z „波兰”, czyli Polską). Zostaje „Pockland”,
    ewentualnie z opisowym podtytułem do ustalenia z native speakerem.
- **Ryzyko do sprawdzenia przed publikacją:** nazwa brzmi podobnie do **„Pokéland”**, gry mobilnej The Pokémon Company
  zapowiedzianej w 2017 roku (o wyspach). The Pokémon Company pilnuje swoich znaków towarowych.
  Przed wydaniem trzeba sprawdzić bazy znaków towarowych (EUIPO, USPTO, WIPO) i Steam. Najlepiej skonsultować to
  z prawnikiem, jeśli gra ma być komercyjna. Wyszukiwanie z 2026-10-13 nie znalazło gry o nazwie „Pockland”.
