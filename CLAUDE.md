# CLAUDE.md

**Misty Packland** (do 2026-10 nazwa robocza Pockland): gra w Unity, idle w okienku na pulpicie, wyspa w klimacie dark fantasy. Rośliny rosną od pracy gracza przy komputerze,
a lampy zasilane Żarem (energia z pracy; w kodzie `Essence`) oświetlają wyspę w nocy (pora dnia z cyklu gry: 1 h = 40 min dnia + 20 min nocy).

## Zanim cokolwiek zrobisz
- **Przeczytaj `docs/HANDOFF.md`**: stan po ostatniej sesji i otwarte decyzje (m.in. grafika 32 px, której gracz nie zaakceptował).
- **Przeczytaj `docs/GDD.md`.** To źródło prawdy o projekcie: koncept, systemy, styl graficzny, paleta, otwarte decyzje,
  historia ustaleń. Po każdej nowej decyzji projektowej **zaktualizuj GDD** (sekcje 7 i 9).
- Obrazki z prototypu są w `docs/img/`, a skrypty, które je generują, w `tools/pixel-proto/` (Python + Pillow, opis w GDD §8).

## Współpraca
- Rozmowa po **polsku**.
- Silnik: **Unity 6.6 (6000.6.5f1)**, szablon Universal 2D (6.6 to wydanie bez LTS: aktualizujemy łatki 6000.6.x, a na kolejne wydanie przechodzimy świadomie). **Repo jest projektem Unity** (`Assets/`, `Packages/`,
  `ProjectSettings/` w katalogu głównym; instrukcja: `docs/UNITY_SETUP.md`). Gałąź główna `main` pochodzi z pusha z Unity.
  Kod gry piszemy tylko w `Assets/_Project/` (namespace `Wyspa.*`). Nie ruszaj `ProjectSettings/` ani plików szablonu bez potrzeby.
- Unity nie ma w kontenerze. Przed commitem kodu C# skompiluj go na atrapach:
  `dotnet build tools/cscheck -p:ExtraDefines="ENABLE_INPUT_SYSTEM%3BUNITY_STANDALONE_WIN%3BUNITY_EDITOR_WIN"`
  (.NET SDK doinstaluj skryptem `dotnet-install.sh` do scratchpada). Nowe API Unity dopisuj do `tools/cscheck/UnityStubs.cs`
  zgodnie z prawdziwą sygnaturą. Atrapy nie gwarantują zgodności z prawdziwym Unity, więc mów graczowi, co jest niesprawdzone.
- Shadery (`Assets/_Project/Shaders/`) piszemy na bazie źródeł URP 17.6 (repo Unity-Technologies/Graphics, gałąź master)
  i przed commitem kompilujemy: `python3 tools/shadercheck/check.py --dxc <dxc> --gfx <gfx> Assets/_Project/Shaders/*.shader`
  (DXC i biblioteki shaderów do scratchpada, instrukcja na górze `check.py`). To nie zastępuje testu w Unity.
- Grafiki-placeholdery generuje `tools/pixel-proto/export_unity.py` (paleta mroczna), a dodatki kolejne skrypty `export_unity_*.py`
  (dopisują do `pivots.json`). Po eksporcie zawsze `tools/pixel-proto/pad_sway.py` (margines na kołysanie, bez niego wiatr ucina liście),
  a po nim `export_unity_stages.py` (dodatkowe fazy roślin z gotowych PNG). Chowańce: `export_unity_familiars.py`, domki i budowle ogników: `export_unity_homes.py`.
  Drzewa: `export_unity_trees2.py` (nadpisuje starsze drzewa z `export_unity.py`, `export_unity_trees.py` i `export_unity_zones.py`, więc idzie po nich, przed `pad_sway.py`),
  ozdobniki na wodzie: `export_unity_water.py`, wyspy fabularne (szczątki i budowle): `export_unity_story.py`.
  Grafiki świata i teren są w **16 px**, tylko woda (shader) liczy obraz w 32 px, dlatego kamera ma PPU 32 i skala okna jest parzysta.
  `tools/art32/convert32.py` (przeróbka na 32 px) gracz odrzucił, nie uruchamiać. Docelowe assety rysuje się ręcznie.
- Gracz woli mroczną, stonowaną estetykę (odrzucił „cukierkową”). Bez postaci, animacje tylko przez shadery i cząsteczki.
- **Lokalizacja (PL, EN, DE, JA, ZH):** żadnego tekstu w sprite'ach ani w kodzie na sztywno. Każdy napis to klucz z tabeli CSV
  w `Assets/_Project/Localization/` (`game.csv`, `ui.csv`; własna warstwa `Wyspa.Localization.Loc`, format `{0:plural:...}`),
  a nowe terminy najpierw trafiają do `Assets/_Project/Localization/glossary.csv`. Zasady w GDD §16.
- Wydanie: `docs/RELEASE.md`. Ikona, logo i obrazy sklepowe generuje `tools/release/release_art.py`, ustawienia gracza
  i build: menu Wyspa → Wydanie (`Editor/ReleaseBuild.cs`), pliki dla gracza w `release/`.
- UI budujemy z kodu (`Scripts/UI/`, `UiRoot`). HUD debugowy (`DebugHud`, F1) może mieć napisy na sztywno, bo nie trafia do gry.
