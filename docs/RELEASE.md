# Wydanie gry: Misty Packland

Lista kroków od gotowego kodu do gry w sklepie. Pliki do wydania są w `release/`, grafiki generuje `tools/release/`,
a build robi menu **Wyspa → Wydanie** w Unity (`Assets/_Project/Editor/ReleaseBuild.cs`).

## 1. Co jest gotowe w repo

| Co | Gdzie |
|---|---|
| Ikona gry (16-1024 px, pixel art) | `Assets/_Project/Art/Release/icon_*.png`, plik Windows `release/icon/misty_packland.ico` |
| Logo | menu główne `Art/Sprites/UI/logo.png`, ekran startowy `Art/Release/splash_logo.png`, Steam `release/store/steam/library_logo.png` |
| Ustawienia gracza (nazwa, firma, wersja, ikony, ekran startowy, jedna kopia gry) | Wyspa → Wydanie → 1 |
| Build Windows 64-bit + zip | Wyspa → Wydanie → 2 (wynik: `Builds/MistyPackland-<wersja>-win64/` i `.zip`) |
| Pliki obok gry | `release/README.txt`, `release/PRIVACY.txt`, `release/THIRD_PARTY_NOTICES.txt` (build kopiuje je sam) |
| Obrazy Steam | `release/store/steam/` (tabela w p. 5) |
| Okładka itch.io | `release/store/itch/cover.png` (630 x 500) |
| Teksty sklepu (PL, EN, DE, JA, ZH), tagi, wymagania | `release/store/store_texts.md` |
| Press kit | `release/store/press_kit.md` |

Grafiki odświeża `python3 tools/release/release_art.py` (Python 3 + Pillow + numpy). Ikona: `tools/release/icon.py`,
logo: `tools/release/logo.py`. Obrazy sklepowe to placeholdery z prawdziwych sprite'ów gry: docelowo warto je zamówić
albo narysować ręcznie (Steam pokazuje je na stronie głównej, decydują o kliknięciach).

## 2. Przed wydaniem (gra)

- [ ] Balans (GDD §7: zostawiony na koniec), progi rytmu pracy.
- [ ] Pełny test z `docs/TEST_PLAN.md` na buildzie, nie w edytorze.
- [ ] Wersja: `ReleaseBuild.Version` (teraz `0.9.0`). Zasada: `0.x` przed premierą / wczesny dostęp, `1.0.0` na premierę,
      potem `1.0.1` poprawki, `1.1.0` nowa zawartość. Wersja jest widoczna w menu głównym.
- [ ] Zapis: zmiana formatu podbija `SaveSystem` (teraz v14) i musi wczytywać starsze zapisy.
- [ ] Usunąć niepotrzebne paczki (mniejszy build, krótszy start): w `Packages/manifest.json` są m.in.
      `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.collab-proxy`, `com.unity.visualscripting`,
      `com.unity.timeline`. Usuwać w Unity (Window → Package Manager), potem test.
- [ ] Font 12 px i ikony UI (GDD §7, osobny etap), jeśli mają być na premierę.

## 3. Build

1. Unity: **Wyspa → 2** (dane, materiały), potem **Wyspa → Wydanie → 1** (ustawienia) i **Wyspa → Wydanie → 2** (build).
   Z linii poleceń (np. na drugim komputerze):
   `Unity.exe -batchmode -quit -projectPath <repo> -executeMethod Wyspa.EditorTools.ReleaseBuild.BuildWindowsCli -logFile build.log`
2. Wynik: `Builds/MistyPackland-0.9.0-win64/MistyPackland.exe` + pliki .txt, obok `.zip` do wysłania i `-symbols`
   (symbole debugowania, **nie** wysyłać, zachować do analizy awarii).
3. Test na czystym Windows (inny komputer albo nowe konto): start, zapis, wyjście, ponowny start, autostart, zasobnik,
   przezroczyste okno, zmiana języka. Sprawdzić folder zapisów `%USERPROFILE%\AppData\LocalLow\Eziek\Misty Packland\`.
4. **Windows SmartScreen:** niepodpisany .exe przy pierwszym uruchomieniu pokazuje ostrzeżenie „Windows ochronił komputer”.
   Ze Steam to nie przeszkadza (Steam uruchamia grę sam). Przy sprzedaży poza Steam warto rozważyć certyfikat
   podpisywania kodu (Code Signing, kilkaset zł rocznie) albo opisać „Więcej informacji → Uruchom mimo to” na stronie.
5. Antywirusy: gra nie używa hooków klawiatury (tylko `GetLastInputInfo`), więc nie powinna być flagowana.
   Przed premierą można wrzucić zip na VirusTotal.

Po zmianie nazwy produktu Unity zapisuje gry w nowym folderze: `SaveSystem` sam kopiuje zapisy ze starego
(`DefaultCompany/My project (5)`), a autostart przenosi się na nowy plik .exe. Ustawienia z `PlayerPrefs` (język, okno)
zaczynają się od nowa (rejestr pod nową nazwą).

## 4. Sprawy prawne i biznesowe

- [ ] **Nazwa:** sprawdzić „Misty Packland” w bazach znaków towarowych (EUIPO, USPTO, WIPO Global Brand Database)
      i na Steam/itch. Wyszukiwanie z 2026-10-14 nie znalazło gry o tej nazwie (GDD §16.5).
- [ ] **Unity Personal:** darmowe do 200 000 USD przychodu lub finansowania w ostatnich 12 miesiącach. Powyżej: Unity Pro.
- [ ] **Licencje:** Fusion Pixel Font (SIL OFL 1.1) wymaga dołączenia licencji: jest w `THIRD_PARTY_NOTICES.txt`.
      Grafiki, dźwięki (`tools/audio`) i font `WyspaPixel` są własne.
- [ ] **Prywatność:** `release/PRIVACY.txt` (gra offline, czyta tylko czas od ostatniego ruchu myszy/klawiatury).
      Na stronie sklepu warto to napisać wprost: „nie czyta naciśnięć klawiszy”, bo gracze pytają o to przy grach od aktywności.
- [ ] **Firma / sprzedawca:** w Unity jest `CompanyName = "Eziek"` (folder zapisów). Do sklepu: imię i nazwisko albo
      działalność, konto bankowe, wywiad podatkowy Steam (formularz W-8BEN dla osób spoza USA, umowa o unikaniu
      podwójnego opodatkowania PL-USA obniża potrącenie). Przychód ze sklepów rozliczyć w PL (zapytać księgową).
- [ ] **Oceny wiekowe:** Steam ma własną ankietę treści (bez kosztów). Inne sklepy (Microsoft Store, konsole) wymagają
      IARC. Zawartość: mroczny klimat, czaszki, cmentarz, potwór, bez przemocy wobec postaci: zwykle 7+ / PEGI 7.

## 5. Steam

1. **Steamworks:** konto partnera (partner.steamgames.com), opłata Steam Direct **100 USD** za grę (zwracana po
   1000 USD przychodu), dane podatkowe i bankowe. Po opłacie jest okres oczekiwania, zanim można wydać grę.
2. **Strona sklepu („Coming soon”)** jak najwcześniej, żeby zbierać listy życzeń. Musi być widoczna co najmniej
   **2 tygodnie** przed premierą. Steam sprawdza stronę i build przed publikacją (zwykle kilka dni roboczych).
3. **Grafiki** (`release/store/steam/`, wymiary z wytycznych Steam z 2024+; sprawdzić w Steamworks przed wysłaniem):

   | Plik | Rozmiar | Gdzie Steam to pokazuje |
   |---|---|---|
   | `header_capsule.png` | 920 x 430 | nagłówek strony, listy |
   | `small_capsule.png` | 462 x 174 | wyniki wyszukiwania, listy |
   | `main_capsule.png` | 1232 x 706 | duży baner na stronie głównej |
   | `vertical_capsule.png` | 748 x 896 | karuzela i wyróżnienia |
   | `library_capsule.png` | 600 x 900 | siatka biblioteki |
   | `library_hero.png` | 3840 x 1240 | tło biblioteki (bez napisów) |
   | `library_logo.png` | 1280 x 720, przezroczyste | logo na tle biblioteki |
   | `community_icon.png` | 184 x 184 | ikona społeczności |
   | `release/icon/misty_packland.ico` | 16-256 | ikona klienta i skrótu |

   Zasada Steam: na kapsułach tylko grafika gry i jej nazwa (bez ocen, nagród, napisów „nowość”).
4. **Zrzuty ekranu:** co najmniej 5, najlepiej 1920 x 1080 (16:9). Gra jest małym oknem: zrobić zrzuty pulpitu
   z grą w rogu (pokazuje, o co chodzi) i powiększone zrzuty samej wyspy (dzień, noc z lampami, mgła, zdarzenia, Księga).
   Ze zrzutów zrobić też GIF / krótki zwiastun (30-60 s): praca w edytorze tekstu, a wyspa w rogu rośnie.
5. **Teksty:** `release/store/store_texts.md` (krótki opis do 300 znaków, opis, cechy, tagi, wymagania) w 5 językach.
6. **Build na Steam:** SteamPipe (steamcmd / ContentBuilder z SDK Steamworks). Depot Windows: zawartość folderu
   `Builds/MistyPackland-<wersja>-win64/` (bez `-symbols`). Opcja uruchamiania: `MistyPackland.exe`.
7. **Steam Cloud** (zalecane, zapis między komputerami): w Steamworks → Steam Cloud → Auto-Cloud, ścieżka
   `WinAppDataLocalLow` + `Eziek/Misty Packland`, wzorzec `save_*.json`.
8. Opcjonalnie później: osiągnięcia (wymaga Steamworks.NET w kodzie), karty kolekcjonerskie.

## 6. itch.io

1. Nowy projekt: rodzaj „Downloadable”, platforma Windows, okładka `release/store/itch/cover.png`, zrzuty, opis
   z `store_texts.md`, tagi jak na Steam.
2. Wysyłka narzędziem **butler**: `butler push Builds/MistyPackland-0.9.0-win64 <konto>/misty-packland:windows --userversion 0.9.0`
   (aktualizacje wysyła tylko zmiany; aplikacja itch sama aktualizuje grę u graczy).
3. Cena: darmowa / „zapłać, ile chcesz” / stała. Dobra opcja na demo albo wczesny dostęp przed Steamem.

## 7. Marketing (krótko)

- Strona Steam z listą życzeń jak najwcześniej, devlog (itch, Reddit r/incremental_games, r/IndieDev, r/pixelart),
  krótkie filmiki z wyspą w rogu pulpitu (TikTok / Shorts).
- Steam Next Fest z demem (demo = ten sam build z ograniczeniem, osobna aplikacja w Steamworks).
- Press kit: `release/store/press_kit.md`.

## 8. Po premierze

- Poprawki: podbić wersję (`1.0.1`), build, wysłać na Steam (gałąź default) i itch (butler).
- Zachować folder `-symbols` każdej wysłanej wersji (do analizy zgłoszonych awarii).
- Zgłoszenia graczy: dyskusje Steam, strona itch; log gry jest w
  `%USERPROFILE%\AppData\LocalLow\Eziek\Misty Packland\Player.log` (poprzedni: `Player-prev.log`).
