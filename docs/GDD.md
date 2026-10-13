# GDD: Wyspa (robocza nazwa)

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
- **Prawdziwy zegar komputera** steruje porą dnia. W nocy na wyspie robi się ciemno i trzeba ją oświetlić lampami.
- Lampy zasila **Esencja**, czyli energia zdobywana pracą.
- Dwa typy roślin: **dnia** (rosną od aktywności, w świetle) i **nocy** (rosną w ciemności, także gdy gracz śpi).

**Bez postaci.** Nie ma bohatera ani animacji klatkowych postaci. Życie świata dają shadery, cząsteczki i
stworzenia w tle (ryby, coś w głębinie, ogniki).

Inspiracje gatunkowe: Rusty's Retirement (farma przy krawędzi ekranu), desktopowe idlery ze Steama.

---

## 2. Platforma i technika

| Temat | Decyzja |
|---|---|
| Silnik | **Unity 6.3 LTS (`6000.3.26f1`)**, szablon **Universal 2D** (URP z 2D Rendererem), Shader Graph, Input System |
| Platforma | **Windows** (na start) |
| Wykrywanie aktywności | `GetLastInputInfo` (user32.dll), czyli czas od ostatniego inputu w całym systemie. Bez globalnych hooków, więc antywirusy nie reagują. Gracz jest „aktywny”, gdy od ostatniego inputu minęło < ~60 s |
| Okno | Na start zwykłe małe okno. Później bez ramki / zawsze na wierzchu / przezroczyste (paczka **UniWindowController**, kirurobo) |
| Praca w tle | `Application.runInBackground = true` |
| Wydajność (krytyczna, gra działa godzinami) | Animacje skokowe ok. **10 kl./s**, więc `targetFrameRate` 10–15 w tle i ~30 przy hoverze. `vSyncCount = 0`. Rozważyć `OnDemandRendering`. Symulacja w tickach (np. co 1 s), nie w każdym `Update` |
| Batching | Jeden materiał dla roślin. Faza kołysania liczona z pozycji w świecie, a nie z właściwości per-renderer |
| Zapis | JSON w `Application.persistentDataPath`, autosave co ~1 min i przy wyjściu |
| Postęp offline | Przy starcie liczymy, ile **nocnych godzin** minęło od zamknięcia, i doliczamy wzrost roślin nocy. Rośliny dnia offline **nie** rosną |
| Czas | `System.DateTime.Now`. Opcjonalnie później prawdziwy wschód i zachód słońca (z szerokości geograficznej) albo godziny ustawiane w opcjach |
| Faza księżyca | Liczona z daty (pełnia daje bonus, patrz zdarzenia) |

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
- Pora dnia z zegara komputera wyznacza **kolor otoczenia**: dzień bez zmian, zmierzch ciepło-fioletowy, noc ciemnogranatowa
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
- **Pełnia księżyca** (prawdziwa faza): rośliny nocy rosną 2× szybciej, krwawe róże kwitną tylko wtedy.
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
- **Rośliny dnia:** marchew, zboże, dynia (po 4 fazy, wersje 16 px; marchew i zboże też 32 px).
- **Rośliny nocy:** _brak_ (do zaprojektowania).
- **Lampy:** krzywa latarnia, czaszka ze świecą, klatka z ognikiem, świecące grzyby.
- **Obiekty:** okrągłe drzewo (16 i 32 px), świerk, krzak z jagodami, kamień, skrzynka na plony, grządka.
- **UI:** moneta, licznik (font 3×5), pasek Esencji, ikona zegara, przedmiot „marchew”.
- **Do zrobienia (dark fantasy):** więcej elementów na wyspie (np. martwe drzewo, ruiny, kości), kupiec-widmo z łodzią,
  potwór (wynurzenie), ogniki, rośliny nocy, słój Esencji.

---

## 7. Otwarte decyzje
- [ ] Siatka 16 px czy 32 px (rekomendacja: 32).
- [ ] Nazwa energii (Esencja / Żar / Dusza) i nazwa gry.
- [x] Wersja Unity: **6.3 LTS (6000.3.26f1)**.
- [ ] Liczby balansu (przychód Esencji, zużycie lamp, czasy wzrostu, ceny).
- [ ] Zasady stawiania lamp (dowolnie czy na slotach) i jak liczyć oświetlenie pól.
- [ ] Noc: stałe godziny, ustawiane w opcjach czy wschód i zachód z lokalizacji.
- [ ] Rozmiar okna i jak rośnie przy powiększaniu wyspy.
- [x] Łowienie ryb wchodzi do gry (pomost, sieci, §11 F).
- [ ] Progi rytmu pracy (seria 25 min, przerwa 5 min, zmęczenie 8 h/10 h) do sprawdzenia w praktyce.
- [ ] Mutacje: które pary roślin dają które hybrydy.
- [ ] Tabela łupów wypraw łodzią.
- [ ] Treść lore (historia wyspy, mgły, potwora).

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
10. Start kodu: **Unity 6.3 LTS**. Gracz kopiuje `Assets/_Project` do projektu z szablonu Universal 2D.
    Etap 1 to szkielet systemów + scena budowana z menu „Wyspa”.
11. Przyjęty **cały system ulepszeń i progresji** (§11): automatyzacja, rytm pracy (seria skupienia, bonus za przerwę,
    zmęczenie), powiększanie wyspy ze strefami, lampy z trzema kierunkami ulepszeń, mutacje i jakość, morze i wyprawy,
    rytuały z cyklem księżycowym zamiast prestiżu, kolekcje i lore, opcje wygody.
    Główna gałąź repo powstanie z pusha z Unity. Gałąź Claude zostaje osobno.

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
| `Scripts/Economy/Wallet.cs` | monety (start 50) |
| `Scripts/Save/SaveSystem.cs` | JSON w persistentDataPath, autosave co 60 s, offline: rośliny nocy rosną za godziny nocy |
| `Scripts/Input/ClickRouter.cs` | klik, potem Collider2D, potem `IClickable` (Input System albo stary Input) |
| `Scripts/UI/DebugHud.cs` | tymczasowy HUD IMGUI: monety, Esencja, nasiona, pora dnia, czas x60 |
| `Editor/PixelArtImporter.cs` | automatyczne ustawienia importu sprite'ów + pivoty z `pivots.json` |
| `Editor/WyspaSetup.cs` | menu „Wyspa”: ustawienia projektu, dane (SO), budowa sceny z `prototype_layout.json` |

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

### 11.3 Drzewka

**A. Wyspa**
- **Powiększenia (4 stopnie):** +4 grządki każde, mgła się cofa, okno rośnie albo da się je przewijać. Koszt: 200 / 800 / 3000 / 10000 monet.
- **Strefy:** cmentarzysko (rośliny nocy ×1.5), bagno (grzyby, dziwne zioła), ruiny (miejsce na ołtarz).
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
1. **Ognik-ogrodnik** (300 monet + 5 plonów nocy): co 30 s zbiera dojrzałe plony do skrzyni (pojemność 20).
2. **Pamięć ziemi** (500 monet): po zbiorze grządka sama sadzi to samo (płaci za nasiono z portfela).
3. **Skrzynia kupca** (800 monet): raz na godzinę sama sprzedaje zawartość skrzyni (90% ceny).
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
3. Powiększanie wyspy i cofanie mgły.
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
- **Etap 2:** fundament ulepszeń (`UpgradeDefinition`/`UpgradeState`/`Currencies`), sklep (proste UI), słój,
  **rytm pracy**, **ognik-ogrodnik + skrzynia + pamięć ziemi + skrzynia kupca**.
- **Etap 3:** shadery (kołysanie, woda, mgła), cząsteczki (monety, iskierki, listki), powiększanie wyspy z cofaniem mgły.
- **Etap 4:** rośliny nocy (grafiki), kupiec-widmo, pomost i łowienie, łódź i wyprawy, mutacje i jakość.
- **Etap 5:** strefy, latarnia morska, Księga rytuałów i cykle księżyca, bestiariusz, lore, potwór.
- **Etap 6:** wygoda (tryby okna, tray, dźwięk), docelowe UI pixel art, balans.
