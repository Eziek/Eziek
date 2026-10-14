# GDD: Misty Packland

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
- **Tylko poza wyspą, na krawędziach okna, nad morzem.** Kłębiaste chmury w pixel arcie (§7), powoli dryfują.
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

### 5.4 Rozdzielczość: 16 px, woda w 32 px (decyzja gracza po teście Etapu 7, §9 p. 74)
- Stan obecny: sprite'y i teren w **16 px** (ostre, mocny kontrast), woda liczona w shaderze w **32 px**, kamera PPU 32.
  Próba przerobienia wszystkiego na 32 px (Scale2x, paleta dark fantasy) została odrzucona jako „wygładzona i nijaka”.
  Poniżej pierwotne ustalenia z testu 32 px (historia).
- Prototyp: **siatka 16 px** (rośliny ok. 18×24, drzewa ok. 32×40), kadr 272×200 (×3 = 816×600).
- Test **32 px** (rośliny ok. 32–40, drzewo ok. 64–80): poziom detalu jak gołąb z referencji. Patrz `docs/img/porownanie_16_vs_32.png`.
- Rekomendacja: **32 px**. Obiekty 128 px odradzane (okno byłoby ogromne, a 4× więcej pracy na asset).
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
- [x] Siatka: **16 px dla sprite'ów i terenu, woda w 32 px** (decyzja gracza po teście Etapu 7, §9 p. 74; wcześniejszy
  wybór „wszystko w 32 px” wycofany, bo przerobione grafiki wyszły „wygładzone i nijakie”). Font 12 px osobnym etapem.
- [x] **Etap 8** (prośby gracza): losowe sadzenie wg pory dnia, 60 ulepszeń handlu (`docs/UPGRADES_TRADE.md`),
  prestiż „Rejs we mgłę” z Perłami i Darami Mgły (§11.8). Balans liczb na koniec.
- [x] Nazwa gry: **Misty Packland** (decyzja gracza 2026-10-14; wcześniej robocza „Pockland”, ryzyko zbieżności z „Pokéland”, §16.5).
- [x] **Pakiet do wydania** (prośba gracza): ikona (pixel art, plakietka: nocne morze, wyspa z latarnią, księżyc, chmury
  mgły; 32 px i uproszczona 16 px), logo własnym krojem pikselowym (w menu głównym, na ekranie startowym, w sklepie),
  obrazy Steam i itch.io z prawdziwych sprite'ów gry, menu Wyspa → Wydanie (ustawienia gracza, build Windows + zip),
  README, prywatność, licencje, teksty sklepu w 5 językach, press kit. Firma w Unity „Eziek”, wersja 0.9.0.
  Instrukcja: `docs/RELEASE.md`. Do zrobienia przez gracza: zrzuty ekranu, zwiastun, konto Steamworks, sprawdzenie nazwy.
- [x] Nazwa energii: **Żar** (wybór gracza; EN Ember, DE Glut, JA 熾火, ZH 余火). W kodzie i starszych częściach GDD
  nadal „Essence/Esencja”. Imię ognika „Żarek” zmienione na „Węgielek” (EN Cinder), żeby nie myliło się z walutą.
- [x] Wersja Unity: **6.6 (6000.6.5f1)**, zmienione z 6.3 LTS na prośbę gracza (Hub poleca 6.6, instalacja jednym kliknięciem).
- [x] **Ikona w zasobniku Windows** (Etap 6c, tylko build): lewy klik pokazuje / chowa okno, prawy klik: menu (pokaż/schowaj,
  zawsze na wierzchu, mini-tryb, wyjście). Powiadomienia Windows, gdy okno schowane albo nieaktywne (najwyżej co minutę):
  kupiec-widmo, potwór, nowe/wykonane zamówienie, chowaniec, gotowa mikstura, co najmniej 3 dojrzałe plony. Opcje → System →
  „Powiadomienia Windows”. Ikona z pliku gry (Player Settings), komunikaty przez osobne okno HWND_MESSAGE. `Platform/TrayIcon.cs`.
- [x] **Dźwięk** (Etap 6c): tło dnia (fale, wiatr, rzadkie ptaki), nocy (ciche fale, wiatr, pomruk głębin, rzadka daleka sowa; świerszcze usunięte po teście - drażniły) i deszczu (ponad tłem,
  burza głośniej), przenikanie przy zmianie pory; efekty: sadzenie, zbiór, moneta, kopanie, ścinanie/usuwanie, stawianie,
  klik, Księga, powiadomienie, grzmot, magia (kocioł, zamówienie, chowaniec), nowy kafelek, bąbel. Placeholdery syntezowane
  w `tools/audio/gen_audio.py` (numpy + ffmpeg, OGG) do `Resources/Audio`; `Audio/AudioManager.cs`, `GameEvents.PlaySound`.
  Opcje → Dźwięk: tło i efekty (co 10%), cisza, gdy okno nieaktywne.
- [x] **Pogoda** (wybór gracza): cykl gry dzielony na 3 części po 20 min (dwie w dzień, jedna w nocy), w każdej losowana
  pogoda z numeru cyklu (bez zapisu): burza 8%, deszcz 22%, w nocy mgła 18%, reszta pogodnie. **Deszcz**: rośliny dnia +20%,
  ukośne krople tylko nad lądem (w przezroczystym oknie nie pada na pulpit), trochę ciemniej, częstszy wiatr. **Burza**: ulewa,
  ciemniej, błyski piorunów co 12-35 s, co czwarty zostawia odłamek gwiazdy (spada jak spadająca gwiazda). **Mgła** (noc): mgła
  gęstsza x1.6, błędne ogniki 2x częściej. Toast przy zmianie, F1 wymusza pogodę. `Weather/WeatherSystem.cs`.
- [x] **Chowańce** (wybór gracza): przychodzą same, gdy wyspa spełni warunek, i zostają na zawsze (bez postaci gracza, tylko
  zwierzęta): **czarny kot** (10 kupionych kafelków i skrzynia; mutacje x2, raz na cykl przynosi kilka monet; chodzi po
  lądzie, siada, w nocy drzemie), **kruk** (dorosłe drzewo owocowe; drzewa rosną i owocują o 25% szybciej; przelatuje między
  koronami drzew, tablicą i lampami), **ropucha** (20 zebranych roślin nocy; rośliny nocy +15%; skacze przy grządkach),
  **nietoperz** (3 lampy; lampy -20% Esencji; w nocy krąży wokół lamp, w dzień wisi pod drzewem). Kursor obok: iskierki
  i dymek z nazwą i bonusem. `Familiars/*`, zapis v11.
- [x] **Kocioł alchemiczny** (wybór gracza): budowla w Sklep → Budowle (300 monet + 6 kamienia, wymaga ognika, kolejne x1.6).
  Składniki bierze **ze skrzyń na plony** (skrzynia = spiżarnia; uwaga: duszek-handlarz sprzedaje pełną skrzynię razem
  ze składnikami). Warzenie 5-10 min gry (ogień i bąble), potem wywar świeci na fioletowo, klik „Wypij” włącza miksturę
  na 2 cykle (czas prawdziwy, mija też offline; ta sama mikstura przedłuża czas). Przepisy:
  Wywar wzrostu (6 roślin dnia → dzień +30%), Eliksir nocy (4 rośliny nocy → noc +50%), Napar skupienia (3 owoce + 3 rośliny
  dnia → Esencja +50%), Płynne światło (3 księżycowe kwiaty + 2 owoce → lampy bez Esencji), Wabik na widmo (2 mandragory +
  2 owoce → kupiec-widmo najbliższej nocy, do użycia), Mgła w butelce (2 cmentarne bluszcze + 2 upiorne dynie → zdarzenia
  nocne x2). Przepisy z nieodblokowanymi nasionami są ukryte. `Alchemy/Cauldron.cs`, `Alchemy/Potions.cs`, zapis v10.
- [x] **Zamówienia na tablicy ogłoszeń** (wybór gracza): po wprowadzeniu tablica zostaje; co 2 cykle gry nowa karteczka
  (maks. 3) od kogoś z mgły (Zielarka z mgły, Grabarz, Latarnik, Wiedźma z bagien, Mnich z ruin, Ślepy rybak, bez postaci
  na ekranie): zebrać 1-2 rodzaje plonów lub owoców w terminie 4-7 cykli. Postęp liczy się sam ze zbiorów (ręcznych i ogników),
  zbiór dalej daje monety. Nagroda: monety (1.8x wartości) + bonus (odłamek 15%, nawóz, nasiona nocy, drewno, kamień).
  Przeterminowana karteczka znika bez kary. Bez tablicy zamówienia czekają; tablicę można postawić w Sklep → Budowle
  (40 monet + 3 drewna). `Orders/OrderBook.cs`, zapis v9.
- [x] **Wskaźnik pracy** (prośba gracza): w pasku HUD ikonki klawiatury i myszy zapalają się, gdy gra widzi input.
  Tempo pracy liczone z ostatnich **15 s** (wcześniej 60 s), więc reaguje szybciej.
- [x] **Czas gry w pasku HUD** (prośba gracza): trzeci wiersz paska: ikona słońca/księżyca, godzina gry i odliczanie
  w prawdziwym czasie, np. „14:05 noc za 23:40” albo „21:17 świt za 6:12”.
- [x] **Szerszy pas wody w przezroczystym oknie** (po teście): 46 px zamiast 18 px, żeby mieścił pomost (33 px od brzegu)
  i łódź; mgła zaczyna się za nim, okno ma więcej zapasu wokół lądu (72 px).
- [x] **Mgła „chibi”** (prośba gracza): bulwiaste kłęby jak z kreskówki zamiast miękkiego rozmycia: koła na siatce 11 px
  (promień 4.5-8.5 px, dryfują i „oddychają”), płaskie kolory (jasny brzeg u góry, podstawa, cień od dołu), ciemny obrys
  1 px, prawie nieprzezroczyste. Przezroczyste okno: wał przyklejony do wody (zachodzi 3 px na wodę, bez obrysu od strony
  wody). Zwykłe okno: jednolita mgła dalej od wyspy z bulwiastym brzegiem od strony lądu (w nocy podchodzi bliżej).
  Podgląd: `docs/img/mgla_chibi_*.png`, `tools/pixel-proto/fog_chibi_preview.py`. **Zastąpiona** chmurami (niżej).
- [x] **Mgła jak pixel-artowe cumulusy** (prośba gracza, referencja z chmurami, „w brudnej palecie dark fantasy”):
  chmury z guzków (sylwetka kalafiora), bliższa zasłania dalszą, bryła cieniowana jak kula, jasny rąbek od światła
  z lewej-góry, łuki guzków w środku, 6 tonów od granatowo-szarego do brudnego pergaminu, bez obrysu, w głąb mgły
  ciemniej. Siatka 32 px jak woda. Podgląd: `docs/img/mgla_chmury.png`, `tools/pixel-proto/fog_clouds_preview.py`.
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
- [x] Mutacje (wybór gracza: 5 nowych hybryd, razem 6): pary w §11.3 D (dane w `CropDefinition.parentA/B`, `WyspaSetup.CropSpecs`).
- [x] Nasiona spoza sklepu (hybrydy, kupiec-widmo, wyprawy) trafiają do **worka nasion**; koło nasion najpierw bierze z worka.
- [x] Nawóz (+1 jakości) na razie tylko od kupca-widma i z wypraw, zużywany automatycznie przy sadzeniu. Kompostownik później.
- [x] Bonus ruin: na razie brak (ruiny mają miejsce na ołtarz, Etap 5b/5c).
- [x] Budowle morskie kosztują monety + drewno: pomost 300 + 10, sieci 150 + 5, łódź 600 + 15 (do balansu).
- [x] Wyprawy łodzią (wybór gracza): **cele wypraw** zamiast krótkiej/długiej, każdy z własną tabelą łupów i zapiskami (§11.3 F).
- [x] Lore (wybór gracza): historia **„Ostatnia latarnia”** (§11.3 H), 33 zapiski odkrywane po kolei w każdym źródle.
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
- [x] Font tekstu: **12 px** (wybór gracza, §16.3). Obecny `WyspaPixel` (8 px, wielkie litery 3×5) zostaje do przesiadki
  na 32 px; wtedy nowy font 12 px (małe i wielkie litery, PL/DE i CJK) i przebudowa układów UI (~1,5× więcej miejsca na tekst).
- [x] Lokalizacja w kodzie: **własna warstwa CSV** (`LocCore`/`Loc`) zamiast paczki Unity Localization (§16.4).
- [ ] Przejście na Unity Localization: później albo wcale (klucze są zgodne, więc migracja jest możliwa).
- [x] Drewno i kamień jako materiały do budowli (§14.3). Odnawialne drewno i owoce: drzewa do sadzenia, na później (§15).
- [x] Skala UI (po teście: przy powiększonym oknie i małej wyspie pasek zajmował pół okna): **zależy od rozdzielczości
  monitora, nie od powiększenia okna** (1080p x2, 1440p x3, 4K x4), najwyżej o 1 mniej niż skala świata. Opcje → Okno →
  „Rozmiar interfejsu”: auto albo x1-x4.
- [x] **Scenki** (wybór gracza): rejs (odpłynięcie, podsumowanie, przypłynięcie) i świt/zmierzch; potem narzędzia ogników
  według zawodu i sklep z czapkami dla ogników (za dużo monet albo perły). §9 p. 84, 86-88.
- [x] Czapki ogników: kosmetyka na zawsze (także po rejsie), zakładane w panelu gniazda każdemu ognikowi osobno (§9 p. 88).
  Do balansu: ceny w monetach i perłach.
- [x] Start gry **zawsze rano** (07:00 na zegarze gry), także po resecie i po rejsie; każdy zapis ma własne przesunięcie
  cyklu (§9 p. 82).
- [x] Prestiż: **Przystań we mgle** między rejsami, dary **losowane** za perły (1 perła = 1 losowanie, animacja walki ikon),
  rzadkość z kolorami, 37 darów, kolekcja z wyszarzałymi niezdobytymi (§11.8, §9 p. 81). Wcześniejszy zakup darów w Księdze
  zastąpiony.
- [x] Pogaduszki ogników (prośba gracza): pary wolnych ogników „rozmawiają” runami w dymkach, z szczebiotem bez znaczenia;
  ogniki nie przenikają się, tylko ocierają. Pomost (z sieciami i łodzią) można przenieść. §11.3 E1 i F, §9 p. 80.
- [x] Rój ogników (prośba gracza): **gniazdo na 3 ogniki**, 6 zawodów (ogrodnik, siewca, drwal, alchemik, latarnik,
  zbieracz), **poziomy** z imionami, krążenie bez pracy. Siewca zastępuje runę siewu, a runa staje się **runą dziczy**
  (wokół wyrastają drzewa i kamienie). Szczegóły w §11.3 E. Zapis v12 (stary ognik → gniazdo z ogrodnikiem).

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
| `wisp_hats.py` + `export_unity_hats.py` | czapki ogników (15, pivot = środek dołu) → `Automation/hat_*.png` |
| `wisp_tools.py` + `export_unity_wisp_tools.py` | narzędzia ogników (6 zawodów, po 2 klatki, pivot = chwyt) → `Automation/tool_*.png` |
| `gift_icons.py` + `export_unity_gifts.py` | ikony 37 Darów Mgły (12 px) → `UI/Icons/icon_gift_<id>.png`, arkusz `docs/img/ikony_darow.png` |
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
    Główna gałąź repo powstanie z pusha z Unity. Gałąź robocza zostaje osobno.
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
52. Kocioł alchemiczny: 6 mikstur ze składników ze skrzyń, działanie 2 cykle, zapis v10.
53. Chowańce: kot, kruk, ropucha, nietoperz - przychodzą po spełnieniu warunku, małe bonusy, zapis v11.
54. Pogoda: deszcz, burza z piorunami, nocna mgła, losowana na każde 20 min cyklu gry.
55. Etap 6c, część 1: dźwięk (tła, efekty, głośność w Opcjach), placeholdery z syntezy.
56. Etap 6c, część 2: ikona w zasobniku Windows z menu i powiadomieniami.
57. Przegląd kodu (3 obszary) przed testem: poprawione m.in. ciche efekty dźwiękowe, mikstury przechodzące między slotami,
    zamówienia blokowane przez karteczkę „gotowe”, atomowy zapis i kopia uszkodzonego pliku (`save_N.corrupt.json`, slot
    „Zapis nieczytelny”), ikona w zasobniku po restarcie Eksploratora, karetka w polu nazwy, przyciski panelu w małym oknie,
    ołtarz i kupiec przy otwartym panelu, deszcz po długiej klatce, owoce drzewa księżyca w zamówieniach.
    Lista testowa: `docs/TEST_PLAN.md`; F1: „+20 min”, „+1 cykl”, „Wszystkie chowańce”, „Zamówienie teraz”.
58. Rój ogników: gniazdo ogników (3 miejsca, pole 7x7), zawody ogrodnik, siewca, drwal, alchemik, latarnik, zbieracz,
    poziomy 1-6 z imionami (12 imion w 5 językach), krążenie wokół gniazda i lamp. Runa siewu → runa dziczy (drzewa i kamienie
    wokół), sianie przejął siewca. Kocioł: przepis dla alchemika. Zapis v12, stare ogniki stają się gniazdami z ogrodnikiem.
59. Skala UI od rozdzielczości monitora zamiast od powiększenia okna (pasek HUD i karteczki nie rosną z x6),
    nowa opcja „Rozmiar interfejsu” (auto, x1-x4).
60. Treść: 5 nowych hybryd (wilcza marchew, popielne zboże, księżycowy mak, wrzeszczący czosnek, krwawy piołun), cele wypraw
    z tabelami łupów (wrak, rafa, zatopiona latarnia, cmentarzysko statków, Serce mgły), historia „Ostatnia latarnia”
    (33 zapiski po kolei). Zapis v13 (cel wyprawy, liczba wypraw).
61. Decyzje gracza: siatka 32 px, font 12 px (oba przy osobnej przesiadce), energia nazywa się **Żar** (teksty w 5 językach;
    ognik „Żarek” → „Węgielek”).
62. Drzewko ulepszeń (prośba gracza): strona Ulepszenia jako odkrywane drzewko z ognikiem-ogrodnikiem w środku,
    18 ulepszeń w 4 gałęziach (rój, Żar, ziemia, handel), 14 nowych. Słój Żaru wymaga teraz ognika-ogrodnika (100 monet).
63. Zakończenie historii: Światło Opiekunów na ołtarzu w noc pełni, ostatnie wynurzenie oka z epilogiem, stałe nagrody.
64. Wiatr: pionowa „kreska” przy kołysaniu to ucinanie przesuniętych pikseli na krawędzi sprite'a. Kołyszące się sprite'y
    (rośliny, drzewa, krzaki) mają teraz 6 px przezroczystego marginesu po bokach (`tools/pixel-proto/pad_sway.py`, po eksporcie).
65. Błąd logiczny (zgłoszenie gracza): po nocy posadzone rośliny nocne stały i nie dało się nic z nimi zrobić.
    Klik w rosnącą roślinę otwiera koło nasion w trybie podmiany (stare nasiono wraca do worka, postęp przepada),
    w pierścieniu akcji „Wykop roślinę”, dymek grządki podpowiada podmianę. Siewca: osobne nasiona na dzień i na noc
    oraz „Podmiana o świcie i zmroku” (domyślnie wł.): wymienia rośliny, które o tej porze nie rosną (uwaga: rośliny
    wielonocne nie dorosną, gdy podmiana jest włączona). Zapis v14.
66. Skład (prośba gracza: za dużo drewna i kamienia): budowla w Sklep → Budowle (120 monet + 10 drewna), co 10 s sprzedaje
    do 10 drewna (po 3 monety) i 10 kamienia (po 5) ponad rezerwę (domyślnie 30, w panelu ±10), „Sprzedaj nadwyżkę teraz”.
67. Etap 7, część 1: wszystkie grafiki świata w 32 px (`tools/art32/convert32.py`: obecne projekty 16 px przez hd2x crisp,
    paleta dark fantasy, poświaty liczone od nowa). Świat w tych samych jednostkach (kafelek = 1), importer PPU 32 dla grafik
    z "hd": 2, kamera PPU 32 i kadr x2; skala okna parzysta (Opcje: x1-x3 w pikselach 32 px). Kołysanie x2 (piksele tekstury).
    Teren, woda i mgła jeszcze w 16 px (części 2-3).
68. Etap 7, część 2: teren w 32 px (`IslandPainter.PaintHi`, podgląd `docs/img/art32_teren.png`): brzeg wygładzony Scale2x,
    trawa w płatach z kępkami i kwiatkami, klif z poziomymi warstwami ziemi, kamieniami, korzeniami i zwisającą trawą,
    kontur 1 px i linia piany. Logika (maska, dane wody, pozycje) zostaje w siatce 16 px.
69. Etap 7, część 3a: woda w shaderze w 32 px (odległość od brzegu interpolowana): dno z kamieniami i wodorostami
    z paralaksą względem kamery i załamaniem przy brzegu, światełka przy dnie (nocą świecą), fale idące do brzegu
    i rozbijające się w pianę z rozbryzgiem i cofką, kręgi na wodzie. Ryby, potwór, połyski i odbicia lamp zostają.
70. Księga (po teście gracza): treść każdej zakładki w przewijanym oknie (ScrollRect, przycięcie RectMask2D):
    kółko myszy przewija, przeciąganie przesuwa (drzewko ulepszeń także w poziomie), okno kończy się nad podpowiedzią.
71. Etap 7, część 3b: mgła liczona w 32 px (gładkie kłęby, obrys 1 px), ciemniejsza obwódka (prośba gracza);
    w przezroczystym oknie twardy limit zasięgu 18 px za pasem wody, więc obrys zamyka się przed krawędzią okna.
72. Etap 7, część 4 (prośba gracza: więcej animacji): ćmy krążą nocą wokół zapalonych lamp, ryba co 18-45 s wyskakuje
    z wody przy brzegu z pluskiem (`Visual/AmbientLife`), świecące rośliny pulsują nocą każda w swoim rytmie.
73. Ocena gracza po zrzutach Etapu 7: „za bardzo wygładzone, nijakie, wcześniej lepiej wyglądało”. Decyzja, czy wracamy do
    16 px, czy robimy 32 px bez wygładzania, czeka na nową sesję (opcje w `docs/HANDOFF.md`).
74. Decyzja gracza po teście Etapu 7: **powrót do grafiki 16 px** („ostrzejsza, z większym kontrastem, przyjemniejsza”),
    **woda zostaje w 32 px** („woda jest świetna”). Sprite'y świata i teren wracają do stanu sprzed Etapu 7 (PNG 16 px
    z `export_unity*.py` + `pad_sway.py`, bez `convert32.py`; `IslandPainter` bez `PaintHi`, ląd PPU 16), kołysanie x1.
    Mgła znów liczona w siatce 16 px (z p. 71 zostają ciemniejsza obwódka i limit zasięgu 18 px w przezroczystym oknie).
    Zostają: shader wody z p. 69, kamera PPU 32 z kadrem x2 (bez niej woda straciłaby detal), więc skala okna jest parzysta
    (Opcje: x2, x4, x6), oraz animacje z p. 72 (ryba w 16 px). Narzędzia `tools/art32/` zostają jako archiwum.
75. Mgła jak pixel-artowe cumulusy z referencji gracza, ale w brudnej palecie dark fantasy (§7): chmury z guzków,
    cieniowanie bryły i rąbków w 6 tonach, bez obrysu, siatka 32 px (jak woda). Zastępuje mgłę „chibi” z p. 49 i obwódkę z p. 71;
    limit zasięgu 18 px w przezroczystym oknie zostaje. Podgląd `docs/img/mgla_chmury.png` (zwykłe okno, przezroczyste, noc).
76. Po teście mgły (prośba gracza: „więcej mgły, grubsza, dalej widoczna przy przesuwaniu, nie tylko mały pasek”):
    przesuwanie PPM sięga 96 x 80 px dalej w mgłę (kadr nie wychodzi poza mapę), w przezroczystym oknie wał chmur grubszy
    (chmury 0,8 zamiast 0,6, środki 4-20 px za pasem wody, limit zasięgu 34 px zamiast 18), okno ma o 16 px więcej zapasu.
77. Nazwa gry **Misty Packland** i pakiet do wydania (§7, `docs/RELEASE.md`): `tools/release/` (ikona, logo, obrazy
    sklepowe), `Editor/ReleaseBuild.cs` (Wyspa → Wydanie), `release/` (README, PRIVACY, THIRD_PARTY_NOTICES, teksty sklepu,
    press kit). Logo w menu głównym i numer wersji. Zapisy kopiowane ze starego folderu (`DefaultCompany/My project (5)`),
    wpis autostartu „Pockland” przenoszony na nowy plik. Jedna kopia gry naraz.
78. Etap 8 (prośby gracza):
    - **Losowe sadzenie** według pory dnia (w nocy losowa roślina nocy, w dzień dnia): kostka w kole nasion, tryb „losowo”
      dla ognika-siewcy (dzień i noc osobno, z podmianą) i akcja w pierścieniu PPM „Obsiej puste grządki losowo”.
    - **60 ulepszeń handlu i dochodów** (`docs/UPGRADES_TRADE.md`, podgląd `docs/img/drzewko_ulepszen.png`): drzewko ma 78
      węzłów w 9 nowych gałęziach, ceny liczy jedno miejsce (`Economy/Market.cs`). Księga otwiera drzewko na środku.
    - **Prestiż „Rejs we mgłę”** (§11.8): Perły Mgły z zarobku rejsu, blask pereł (+2% cen za każdą), 10 Darów Mgły.
    - PPM przesuwa widok także w przezroczystym oknie (zapas 40 px).
    Zapis v15.
79. Porządki UI i wody (zrzuty gracza, przegląd kodu UI):
    - Czytelna czcionka **Fusion Pixel 8px** (małe litery, PL/DE, linia 10 px jak w układzie), domyślnie włączona,
      Opcje → Ogólne → Czcionka (dawna 3x5 do wyboru). Próbki `docs/img/ui_fonty.png`. Wersje 10/12 px wymagają przebudowy układu.
    - Księga: czyszczenie strony przed pomiarem (ucięte pierwsze litery), ciemne złoto i szary na pergaminie, status w sklepie
      w wierszu nazwy, opisy darów skracane, przycisk rytuału na szerokość tekstu.
    - Skala UI dopasowana do okna (płótno min. ok. 216x176), Księga w skali gracza; dymki do 220 px z zawijaniem i
      odwracaniem przy krawędzi; toasty nad Księgą, pod paskiem HUD, dłużej przy długim tekście; pasek HUD bez skakania.
    - Łodzie i boja w wodzie (`SpriteFloat.shader`, `Visual/Floating`): dno pod wodą, piana, kołysanie, kręgi.
    - Mgła: ciemny pas od krawędzi wody w przezroczystym oknie, chmury w zwykłym oknie dalej od wyspy.
    - Scena gry nazywa się **MainLevel** (`Assets/_Project/Scenes/MainLevel.unity`, Wyspa → 3).
    - Komentarze w kodzie bez historii rozmów (same opisy techniczne).
80. Ogniki i morze (prośby gracza):
    - **Pogaduszki ogników** (§11.3 E1): wolne ogniki zlatują się parami, nad głowami dymki z runami, szczebiot bez
      znaczenia („simlish”) z syntezowanych sylab, każdy ognik innym głosem. Przełącznik w opcjach dźwięku.
    - Ogniki **nie przenikają się**, tylko delikatnie ocierają (odsuwanie sprite'ów, trasa bez zmian).
    - **Przenoszenie pomostu i sieci** (§11.3 F): klik w deski albo w boję → „Przenieś”; sieci i łódź płyną z pomostem.
81. Prestiż przebudowany (prośba gracza, §11.8): po „Odpłyń we mgłę” najpierw **Przystań we mgle**, gdzie perły wydaje się
    na **losowania darów** (1 perła = 1 losowanie, „walka” ikon na środku, zostaje jeden, potem wszystkie wracają na miejsca),
    i dopiero potem nowa wyspa z darami. Dary mają **rzadkość z kolorami** (zwykły, niezwykły, rzadki, epicki, legendarny),
    **27 nowych** (razem 37), kolekcja: zdobyte w kolorze, niezdobyte wyszarzałe. Zakup darów w Księdze usunięty (Księga
    pokazuje kolekcję). Zapis v16. Gałąź główna repozytorium to teraz `main`.
    Każdy z 37 darów ma własną ikonę 12 px w palecie mrocznej (`gift_icons.py`, podgląd `docs/img/ikony_darow.png`).
    Wygrana w losowaniu: fanfara (dłuższa z gongiem dla epickich i legendarnych) i fajerwerki w kolorze rzadkości.
82. **Nowa wyspa zawsze zaczyna się rano** (zgłoszenie gracza: po resecie trafił na noc). Każdy zapis ma własne przesunięcie
    cyklu dnia i nocy (`GameClock.SaveOffsetSeconds`, pole `clockOffset` w zapisie): nowa gra, reset i wypłynięcie z Przystani
    ustawiają je tak, żeby była 07:00 (ok. 37 min dnia przed pierwszą nocą). Potem czas płynie normalnie, także offline.
    Starsze zapisy mają przesunięcie 0 (bez zmian), menu główne też.
83. **Darmowa pierwsza budowla zawsze dostaje miejsce** (zgłoszenie gracza: „Wierny ognik” na nowej wyspie 2x2 nie dał
    ognika, bo nie było wolnego kafelka). Pierwsza sztuka z ulepszenia albo daru (skrzynia, gniazdo z ognikiem, runa): gdy
    wyspa nie ma wolnego kafelka, mgła odsłania obok darmowy kafelek trawy (bez znaleziska, nie podnosi ceny kolejnych).
    Zapis, w którym ognik przepadł, dostaje gniazdo przy następnym wczytaniu.
84. **Scenka rejsu** (wybór gracza z propozycji scenek, §11.8): odpłynięcie (interfejs znika, ogniki wracają do gniazd, lampy
    gasną po kolei od najdalszej, kamera zsuwa się ku morzu, pikselowa mgła zamyka okno od brzegów), **podsumowanie rejsu**
    w Przystani (pergamin: dni na wyspie, zarobek, zebrane plony, sprzedane, kafelki, ogniki, perły; raz), przypłynięcie
    (okno w mgle, mgła rozstępuje się od środka, kamera podpływa, latarnia rozbłyska, tytuł „Rejs nr N” i jedna z 8 linijek
    zapisków Opiekuna, litera po literze). Klik przyspiesza. Kod: `UI/VoyageScene.cs`, `UI/MistCurtain.cs`.
85. Poprawki z przeglądu logiki: ogniki ponad 3 w gnieździe (Przytulne gniazdo, Przestronne gniazdo) nie znikają przy
    wczytaniu; zwroty (rozbiórka, nieudane postawienie) nie liczą się do zarobku rejsu (perły); Skarbonka 1. poziomu działa
    (limit odsetek 1000 monet za 1%); odmiana „perła/pereł” w zakładce Rejs; cykl księżyca nie zamyka się wstecz po rejsie;
    toast złotego interesu pokazuje prawdziwy mnożnik (x2.5); znalezisko spod darmowego kafelka nie przepada.
86. **Scenki świtu i zmierzchu** (wybór gracza): zmierzch: lampy zapalają się łańcuchem od latarni przy środku wyspy
    (0,35 s na jednostkę odległości, każda z błyskiem), ćmy nadlatują z ciemności do zapalonych lamp; świt: mgła stoi pod
    samym brzegiem i przez ok. 22 s cofa się na zwykłą odległość (nie w przezroczystym oknie), z koron drzew zrywa się
    2-4 ptaków, ogniki ziewają (dymek z wielokropkiem i senne mruczenie). Tylko przy zmianie pory w trakcie gry, nie przy
    wczytaniu i nie w scenkach rejsu. Kod: `Visual/DayTransitions.cs` (+ Lamp, FogController, AmbientBirds, AmbientLife, Wisp).
87. **Narzędzia ogników** (prośba gracza): każdy zawód trzyma swoje narzędzie z boku tułowia (obraca się z ognikiem):
    drwal siekiero-kilof (przy każdym uderzeniu w drzewo albo kamień opada), ogrodnik sierp (zamach przy zbiorze),
    siewca worek nasion (przechyla się i sypie przy sadzeniu), alchemik fiolka (wywar bulgocze i świeci), latarnik
    latarenka (płomyk migocze i świeci w nocy), zbieracz koszyk: zebrane rzeczy (owoce, odłamki, błyskotki) leżą w koszyku.
    Grafika: `tools/pixel-proto/wisp_tools.py` + `export_unity_wisp_tools.py` → `Automation/tool_<nazwa>_<0|1>.png`
    (pivot = chwyt), w Unity Wyspa → 2. Kod: `Wisp.SetTool`, `Wisp.Pose`. Podgląd: `docs/img/narzedzia_ognikow.png`.
88. **Sklep z czapkami dla ogników** (prośba gracza): Księga → Sklep → Czapki. 15 czapek, kosmetyka bez wpływu na grę:
    za monety (słomkowy kapelusz 3000, czapka z pomponem 4000, szlafmyca 5000, wianek 6000, kapelusz muchomora 8000,
    beret z piórem 10 000, cylinder 12 000, kapelusz wiedźmy 15 000, dyniowy hełm 20 000, rogi 25 000) albo za Perły Mgły
    (piracki trójgraniasty 2, hełm z czaszki 3, świeca 3, korona 4, aureola mgły 5). Kupione **zostają na zawsze**
    (zapis w PrestigeSave, także po rejsie). Zakłada się je w panelu gniazda, każdemu ognikowi osobno (klik = następna
    posiadana czapka albo bez); dymek pogaduszek wędruje nad czapkę. Świeca, aureola, korona, wianek i dynia mają piksele
    emisyjne. Grafika `tools/pixel-proto/wisp_hats.py` + `export_unity_hats.py`, podgląd `docs/img/czapki_ognikow.png`.
    Kod: `Economy/Hats.cs`, `Wisp.SetHat`, `WispSave.hat`.
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

**Drzewko ulepszeń w Księdze** (prośba gracza: „bardziej progresywny wygląd, na środku ognik-ogrodnik, drzewko się odkrywa”).
Strona Ulepszenia to mapa węzłów zamiast listy: w środku Ognik-ogrodnik, cztery gałęzie. Węzeł kupiony świeci złotem,
dostępny i na kieszeń delikatnie pulsuje, następny za nim jest „we mgle” (kłódka, nazwa ukryta), dalsze nie są widoczne.
Najechanie: nazwa, poziom, efekt, koszt; klik kupuje. Pozycje i ikony w `Upgrades/UpgradeTree.cs`, wartości w `WyspaSetup`,
efekty w `Upgrades/UpgradeEffects.cs` (wartości wstępne do balansu):

| Gałąź | Ulepszenie | Poziomy (efekt) | Wymaga |
|---|---|---|---|
| środek | Ognik-ogrodnik | gniazdo + ogrodnik | - |
| rój (góra) | Ognik-siewca | siewca, runa dziczy | ogrodnik |
| | Lekkie skrzydła | ogniki +15% / +30% szybsze | siewca |
| | Przestronne gniazdo | 4 / 5 miejsc | siewca |
| | Bystre ogniki | +50% doświadczenia | siewca |
| | Pojemne skrzynie | 30 / 40 miejsc | gniazdo |
| Żar (lewo) | Słój Żaru | 120 / 240 / 480 | ogrodnik |
| | Rozpałka | praca +10/20/30% Żaru | słój |
| | Drugi oddech | zmęczenie: Żar do 65% / 80% | rozpałka |
| | Głębokie skupienie | seria +35% / +50% | rozpałka |
| | Pajęcze knoty | lampy -15% / -30% Żaru | słój |
| ziemia (prawo) | Żyzna gleba | rośliny dnia +10/20/30% | ogrodnik |
| | Nocna rosa | rośliny nocy +10/20/30% | gleba |
| | Krzyżowanie | mutacje 5% / 8% | rosa |
| | Dzikie runy | runa dziczy -30% / -50% czasu | rosa |
| handel (dół) | Duszek-handlarz | sprzedaje pełne skrzynie | ogrodnik |
| | Uczciwy handel | sprzedaż 95% / 100% ceny | handlarz |
| | Szybka łódź | wyprawy -15% / -30% czasu | handlarz |

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
  (np. dynia-upiór). Hybrydy wyglądają inaczej i są droższe. Pary (rodzice → hybryda, cena sprzedaży):
  dynia + upiorna dynia → dynia-upiór (noc, 150); marchew + wilcza jagoda → wilcza marchew (dzień, 55);
  zboże + trupi grzyb → popielne zboże (dzień, 70); mak + księżycowy kwiat → księżycowy mak (noc, 160);
  czosnek + mandragora → wrzeszczący czosnek (noc, 220); piołun + krwawa róża → krwawy piołun (tylko pełnia, 450).
  Hybryd nie da się kupić w sklepie: mutacja, kupiec-widmo (losowa hybryda) albo wyprawy. Dymek nasiona pokazuje rodziców.
- **Jakość 1–3 gwiazdki:** baza 1, +1 za nawóz, +1 za wzrost w świetle ognika. Cena ×1 / ×1.5 / ×2.5. Kupiec składa zamówienia na jakość.

**E. Automatyzacja (kamienie milowe, bez niej gra przeszkadza w pracy)**
**Stan (Etap 6b):** ulepszenie odblokowuje rodzaj budowli i daje pierwszą sztukę za darmo; kolejne w Sklep → Budowle
(monety x1.6 za każdą posiadaną sztukę + stały materiał). Budowle stawia się i przenosi jak lampy.
1. **Rój ogników** (od wersji z rojem, prośba gracza: „więcej ogników, które latają i coś robią”). Ulepszenie
   **Ognik-ogrodnik** (100 monet) daje **gniazdo ogników** z darmowym ogrodnikiem i skrzynię. Gniazdo (kolejne: 150 monet
   + 6 drewna) to dom dla **3 ogników**, pracują w polu **7x7** wokół niego (latarnik i zbieracz na całej wyspie).
   Klik w gniazdo: lista ogników (imię, zawód, poziom, doświadczenie) i przywołanie nowego: 200/250/350 monet x1.5 za
   każdego ognika tego zawodu + 3 plony nocy. Zawody:
   - **Ogrodnik:** zbiera dojrzałe plony (kurs do „udźwigu”), niesie do skrzyni; bez plonów strąca owoce z drzew i zbiera je z ziemi.
   - **Siewca** (wymaga ulepszenia „Ognik-siewca”, dawniej „Runa siewu”): sieje na pustych grządkach; w gnieździe osobno
     nasiona na dzień i na noc (pamięć ziemi, wybrana roślina, nic) i podmiana o świcie i zmroku (rośliny, które o tej porze
     nie rosną, wracają do worka). Nasiono z worka, inaczej za monety.
   - **Drwal:** rozbiera dzikie drzewa, krzaki, kamienie i pnie w zasięgu (bez Esencji, bez zabytków), niesie drewno/kamień do gniazda.
   - **Alchemik** (wymaga kotła): warzy w kotle przepis wybrany w panelu kotła („Ognik-alchemik warzy”) ze składników ze
     skrzyń i wypija gotowy, gdy mikstura nie działa albo zaraz się skończy.
   - **Latarnik:** nocą dolewa Esencji lampom: przez 2 min (+20 s na poziom) lampa pali o połowę mniej.
   - **Zbieracz:** zbiera spadłe gwiazdy, owoce zostawione pod drzewami, co kilka minut znajduje w trawie kilka monet.
   **Poziomy 1-6** (doświadczenie za każdą pracę: 10/30/70/150/300): szybszy lot (+8% na poziom), większy udźwig, drwal
   uderza mniej razy, ognik jest trochę większy i jaśniejszy, awans ogłasza toast. Bez pracy ogniki krążą wokół gniazda
   i lamp albo odpoczywają nad pniem. Każdy zawód ma inny kolor płomienia. **Skrzynia na plony** (20 miejsc): 60 monet + 4 drewna.
   **Pogaduszki:** wolny ognik co jakiś czas (pierwsza po 10-45 s, potem co 45-120 s) zaprasza wolnego sąsiada
   (do 3,5 kafelka), oba zlatują się w pół drogi, patrzą na siebie i na zmianę „mówią” 3-5 kwestii w dymkach z runami
   (17 znaków futharku 3x5 px, na koniec reakcja: „!”, „?”, wielokropek albo nutka). Każda runa ma swoją sylabę
   „mowy ogników” (12 sylab i 4 reakcje, `tools/audio/gen_babble.py`), wysokość głosu zależy od imienia i zawodu.
   Szczebiot słychać tylko z ogników na ekranie; Opcje → Dźwięk → „Głosy ogników”. Najwyżej 2 rozmowy naraz.
   Ogniki **nie wchodzą w siebie**: gdy tory lotu zbliżą się na mniej niż 0,6 kafelka, sprite'y delikatnie się odsuwają
   (najwyżej o 0,3), trasa i praca zostają bez zmian. Kod: `Automation/Wisp.cs`, dymek `Automation/WispBubble.cs`.
2. **Skład** (prośba gracza: nadmiar drewna i kamienia z drwala i run): sprzedaje nadwyżkę ponad rezerwę na budowy
   (drewno 3, kamień 5 monet, porcja co 10 s; rezerwa domyślnie 30, ustawiana w panelu; przycisk „Sprzedaj nadwyżkę teraz”).
2. **Runa dziczy** (prośba gracza, zastępuje runę siewu; pierwsza za darmo z „Ognik-siewca”, kolejne 150 monet + 5 kamienia):
   co 6 min gry na wolnym kafelku trawy wokół runy (3x3 bez środka) wyrasta dzikie drzewo, krzak albo kamień.
   Z drwalem daje stałe drewno i kamień.
3. **Duszek-handlarz** (dawna skrzynia kupca, 800 monet): **od Etapu 6d (prośba gracza) stoi przy jednej skrzyni** (bez
   własnego kafelka, unosi się obok niej) i sprzedaje ją po 90% **od razu, gdy się zapełni** (20/20), zamiast raz na godzinę.
   Kupiony w sklepie: klik w skrzynię bez duszka. Przeniesienie skrzyni zabiera duszka, rozbiórka skrzyni też (zwrot połowy).
   Duszki ze starych zapisów stają przy skrzyni bez duszka (a gdy jej nie ma, na ich kafelku pojawia się skrzynia).
   Kolejne: 500 monet + 8 kamienia.
4. **Sieci rybackie** (wymagają pomostu): bierny połów co godzinę. Klik w boję: czas do połowu i „Przenieś” (z pomostem).

**F. Morze**
- **Pomost** (400 monet): łowienie (klik w rybę przy brzegu). Klik w deski (bez ryby): panel z „Przenieś” (za darmo,
  te same warunki co przy budowie); sieci i łódź płyną razem z nim, łódź zachowuje wyprawę, sieci odliczanie połowu.
- **Łódź i wyprawy** (800 monet): łódź płynie w mgłę na N godzin **aktywnej pracy** i wraca z łupem. Jedna wyprawa naraz.
  **Cele wypraw** (wybór gracza; dane w `Sea/Destinations.cs`): każda wyprawa daje kolejny zapisek z tego miejsca i 2-6 losowań
  z tabeli celu (wagi), dalekie cele jeszcze jedno rzadkie (odłamek albo hybryda).
  | Cel | Czas | Odblokowanie | Łup (główne) |
  |---|---|---|---|
  | Wrak „Czapli” | 2 h | od początku | drewno, monety, nawóz |
  | Kościana rafa | 2 h | 1 wyprawa | kamień, nasiona nocy, nawóz |
  | Zatopiona latarnia | 3 h | 3 wyprawy | odłamki, monety, kamień, rzadko hybryda |
  | Cmentarzysko statków | 4 h | 5 wypraw | drewno, monety, odłamki, hybrydy (+ rzadkie) |
  | Serce mgły | 6 h | wszystkie zapiski z 4 celów | odłamki, hybrydy, monety (+ rzadkie) |
  Rytuał „Pamięć przypływu” skraca wszystkie o 25%. Nowy cel ogłasza toast.
- **Ołtarz ofiarny na brzegu:** wymiana z potworem.

**G. Rytuały i księżyc (późna gra)**
- **Księga rytuałów:** stałe pasywne bonusy za rzadkie surowce (np. „rośliny nocy rosną też o zmierzchu”).
- **Cykl księżycowy zamiast prestiżu:** co prawdziwe 29,5 dnia (od nowiu do nowiu) cykl się zamyka i daje punkty do Księgi
  za osiągnięcia z tego cyklu. **Wyspa nic nie traci** (świadomie bez resetu postępu).

**H. Kolekcje i lore**
- **Bestiariusz:** ryby, rośliny, hybrydy, zdarzenia, potwór. Ukończenie kategorii daje nagrody.
- **Lore z wypraw i wraków** stopniowo odsłania historię wyspy, mgły i potwora (narracja bez postaci i dialogów).
  **„Ostatnia latarnia”** (wybór gracza; `Lore/LoreBook.cs`, teksty `lore.*` w game.csv). Świat zjadła mgła. Kiedyś trzymał ją
  w ryzach **Łańcuch Latarni**: wyspy, których lampy palą się od pracy opiekunów (stąd Esencja z pracy gracza). Latarnie gasły
  jedna po drugiej. Wyspa gracza jest ostatnia. Poprzedni **Opiekun** (podpis „O.”) zostawił zapiski i popłynął w mgłę.
  Na końcu okazuje się, że potwór-oko to **Pierwszy Opiekun**: od stu lat trzyma mgłę na dnie. Lampy dają mu siłę, a ofiary
  na ołtarzu są dla niego łykiem powietrza. Poprzedni Opiekun został z nim („Pal światło, a my utrzymamy resztę”).
  Źródła (w każdym zapiski po kolei): Opiekun 8 (znaleziska na kafelkach, nagrobki), zabytki 3 i oko 1,
  wrak 4, rafa 4, zatopiona latarnia 4, cmentarzysko 4, Serce mgły 5, epilog 3. Razem 36 na stronie Zapiski w bestiariuszu.
  **Zakończenie** (`Lore/Story.cs`): po ostatnim zapisku z Serca mgły toast wskazuje ołtarz. W noc pełni na ołtarzu:
  „Zapal Światło Opiekunów” (10 plonów nocy + 3 odłamki). Oko wynurza się po raz ostatni jako Pierwszy Opiekun,
  świeci cały czas, panel pokazuje epilog (3 zapiski, „Dalej”, „Pożegnaj”). Nagroda stała: lampy -20% Żaru,
  rośliny nocy +10%, mgła rzadsza (×0,7). Potem potwór przy zwykłych wynurzeniach nazywa się Pierwszym Opiekunem
  i ma łagodny opis. Gra toczy się dalej (idle bez końca); stan w kronice („story:finale”).

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

### 11.8 Prestiż: Rejs we mgłę (Etap 8, przebudowa: Przystań we mgle i losowane dary)
- **Po co:** po kilku tygodniach wyspa jest „skończona”. Rejs pozwala zacząć od nowa, mocniejszym, z nową wyspą (nowe
  ziarno świata, inne strefy we mgle).
- **Jak:** Księga → zakładka Rejs (ikona perły) → „Odpłyń we mgłę” (drugi klik potwierdza) → **Przystań we mgle**: ekran
  między rejsami (wyspa w tle jak w menu głównym). Tam gracz wydaje perły na losowania darów, a dopiero „Wypłyń na nową
  wyspę” zaczyna grę z wylosowanymi darami. Zamknięcie gry w Przystani niczego nie gubi: slot w menu ma dopisek „We mgle”,
  a „Graj” wraca do Przystani. Z Przystani można wyjść do menu głównego.
- **Perły Mgły** = pierwiastek z (monety zarobione w tym rejsie / 10 000): 10 tys. = 1, 40 tys. = 2, 1 mln = 10.
  Zarobek liczy każda moneta wpadająca do portfela (sprzedaż, zamówienia, odsetki, wyprawy). Niewydane perły zostają.
- **Blask pereł:** każda kiedykolwiek zdobyta perła +2% do wszystkich cen i dochodów (na zawsze, także po wydaniu perły).
- **Losowanie:** 1 perła = 1 losowanie. Najpierw rzadkość: zwykły 50%, niezwykły 28%, rzadki 14%, epicki 6%, legendarny 2%
  (rzadkości bez darów do zdobycia wypadają z losowania, reszta proporcjonalnie), potem dar tej rzadkości po równo.
  Ten sam dar drugi raz: poziom wyżej. Dar na najwyższym poziomie już nie wypada. Każde losowanie od razu trafia do zapisu.
- **Walka darów** (animacja losowania): ikony wszystkich darów, które mogą wypaść, zlatują się w krąg na środku, krąg kręci
  się coraz szybciej i zacieśnia, ikony rzucają się na siebie i po kolei wylatują na boki (szarzeją), ostatni cios z grzmotem.
  Zwycięzca rośnie x2 na środku w rozbłysku koloru rzadkości, napis z nazwą i poziomem, **fanfara z dzwonków** (przy epickim
  i legendarnym dłuższa, z gongiem) i **fajerwerki**: rakiety z dołu okna pękają na iskry w kolorze rzadkości, złota i bieli
  (zwykły 2 rakiety, niezwykły 3, rzadki 4, epicki 6, legendarny 9), z wystrzałem i trzaskiem. Potem wszystkie ikony wracają
  na swoje miejsca w kolekcji, a nowy dar mruga obwódką. Klik w trakcie przyspiesza. Ok. 5 s.
  Dźwięki: `tools/audio/gen_harbor.py` (`sfx_fanfare`, `sfx_fanfare_big`, `sfx_firework`).
- **Kolekcja** (Przystań i zakładka Rejs w Księdze): kratka 37 darów w ramkach koloru rzadkości (kamień, mech, błękit,
  fiolet, stare złoto). Zdobyte w pełnym kolorze z kropkami poziomów, niezdobyte wyszarzałe; dymek: nazwa, rzadkość,
  efekt, poziom. Podgląd: `docs/img/przystan_mgly.png`.
- **Zostaje:** perły, dary, liczba rejsów, kronika (bestiariusz, historia, rytuały), nazwa wyspy, opcje.
  **Przepada:** wyspa, waluty, ulepszenia z drzewka, budowle, worek nasion, chowańce, zamówienia.
- **Dary Mgły** (37; wartości na kolejnych poziomach):

| Rzadkość | Dar | Efekt |
|---|---|---|
| zwykły | Posag | nowa wyspa +500 / 2000 / 8000 monet |
| zwykły | Tania ziemia | kafelki -10/20/30% monet |
| zwykły | Pełna spiżarnia | start z 30 / 80 / 150 drewna i kamienia |
| zwykły | Skrzydła mgły | ogniki latają +10/20/30% szybciej |
| zwykły | Głębokie skrzynie | skrzynie +5/10/15 miejsc |
| zwykły | Słoneczne nasiona | rośliny dnia +8/16/24% |
| zwykły | Księżycowa rosa | rośliny nocy +8/16/24% |
| zwykły | Gęsty olej | lampy -8/16/24% Żaru |
| zwykły | Kupiecki topór | skład +1/2/3 za drewno |
| zwykły | Kamieniarz z mgły | skład +1/2/3 za kamień |
| zwykły | Mądre ogniki | doświadczenie ogników +15/30/45% |
| zwykły | Nawóz z mgły | start z 3 / 6 / 10 porcjami nawozu |
| niezwykły | Wierny ognik | ognik-ogrodnik od startu |
| niezwykły | Żar Opiekunów | Żar z pracy +10/20/30% |
| niezwykły | Zielone ręce | wzrost roślin +10/20/30% |
| niezwykły | Szczodry duszek | duszek-handlarz i ogniki +3/6/9% ceny |
| niezwykły | Znajomy widma | kupiec-widmo -5/10/15% |
| niezwykły | Sad przodków | owoce +10/20/30% |
| niezwykły | Stała klientela | zamówienia +10/20/30% |
| niezwykły | Kapryśne nasiona | mutacje +1/2/3 pkt proc. |
| niezwykły | Wiatr we mgle | wyprawy -8/16/24% czasu |
| niezwykły | Nocny jarmark | rośliny nocy +8/16/24% ceny |
| niezwykły | Żar w zanadrzu | start z pełnym słojem Żaru |
| rzadki | Mgielny targ | ceny i dochody +10/20/30/40/50% |
| rzadki | Pamięć nasion | odblokowane nasiona zostają |
| rzadki | Stare znajomości | ulepszenia -10/20/30% monet |
| rzadki | Przytulne gniazdo | gniazdo +1 ognik |
| rzadki | Skarbonka | odsetki o świcie +0,5/1% |
| rzadki | Pełna tablica | +1 zamówienie na tablicy |
| rzadki | Niezmordowany | zmęczenie spowalnia o 10/20% mniej |
| rzadki | Dniówka z mgły | +1/2 monety za minutę pracy |
| epicki | Perłowe szczęście | złoty interes +3/6/9% szansy |
| epicki | Złote runo | mnożnik złotego interesu +0,5/1 |
| epicki | Gwiezdny traf | odłamek gwiazdy przy sprzedaży +2/4% |
| legendarny | Korona mgły | ceny i dochody +25% |
| legendarny | Perłowa łaska | +25% pereł z rejsu |
| legendarny | Serce wyspy | wszystkie rośliny +25% |

- **Scenki rejsu:** odpłynięcie (lampy gasną po kolei, ogniki wracają, mgła zamyka okno), podsumowanie rejsu w Przystani
  (raz, przed losowaniem), przypłynięcie (mgła się rozstępuje, latarnia rozbłyska, zapiski Opiekuna „Rejs nr N”).
  `UI/VoyageScene.cs`, kurtyna mgły `UI/MistCurtain.cs`, teksty `voyage.*` i `ui.summary.*`.

Kod: `Economy/Prestige.cs` (dary, losowanie, `ApplyToEffects` po `UpgradeEffects.Apply`), Przystań `UI/MistHarborView.cs`,
kolekcja `UI/GiftGrid.cs`, zapis `Save/SaveSystem` (`SailIntoMist`, `LoadMist`, `SaveMist`, `LeaveMist`, zapis v16 z polem
`mist`). Każdy dar ma własną ikonę 12 px (`tools/pixel-proto/gift_icons.py`, eksport `export_unity_gifts.py` →
`UI/Icons/icon_gift_<id>.png`, arkusz `docs/img/ikony_darow.png`); bez niej grafika zapasowa z ikon UI. Liczby do balansu.

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
- **Etap 6c (gotowy, do testu w Unity i w buildzie):** dźwięk (tła, efekty, głośność), ikona w zasobniku i powiadomienia Windows.
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
   Tłumaczenia są wstępne. **JA, ZH i DE wymagają weryfikacji przez native speakera** przed wydaniem.

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

### 16.5 Nazwa gry: Misty Packland
- **Nazwa ostateczna: „Misty Packland”** (decyzja gracza 2026-10-14). Wcześniej robocza „Pockland” (niżej, historia).
- **Jedna marka we wszystkich językach.** Logo i tytuł zawsze łacinką „Misty Packland”.
  - JA: na stronie sklepu można dodać czytanie ミスティ・パックランド.
  - ZH: nie tłumaczymy fonetycznie. Zostaje „Misty Packland”, ewentualnie z opisowym podtytułem do ustalenia z native speakerem.
- Wyszukiwanie z 2026-10-14 nie znalazło gry o nazwie „Misty Packland”. Przed wydaniem i tak sprawdzić bazy znaków
  towarowych (EUIPO, USPTO, WIPO) i Steam (`docs/RELEASE.md`).
- Historia, nazwa robocza „Pockland”:
- **Ryzyko do sprawdzenia przed publikacją:** nazwa brzmi podobnie do **„Pokéland”**, gry mobilnej The Pokémon Company
  zapowiedzianej w 2017 roku (o wyspach). The Pokémon Company pilnuje swoich znaków towarowych.
  Przed wydaniem trzeba sprawdzić bazy znaków towarowych (EUIPO, USPTO, WIPO) i Steam. Najlepiej skonsultować to
  z prawnikiem, jeśli gra ma być komercyjna. Wyszukiwanie z 2026-10-13 nie znalazło gry o nazwie „Pockland”.
