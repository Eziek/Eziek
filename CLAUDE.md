# CLAUDE.md

**Pockland**: gra w Unity, idle w okienku na pulpicie, wyspa w klimacie dark fantasy. Rośliny rosną od pracy gracza przy komputerze,
a lampy zasilane Esencją oświetlają wyspę w nocy (pora dnia z zegara komputera).

## Zanim cokolwiek zrobisz
- **Przeczytaj `docs/GDD.md`.** To źródło prawdy o projekcie: koncept, systemy, styl graficzny, paleta, otwarte decyzje,
  historia ustaleń. Po każdej nowej decyzji projektowej **zaktualizuj GDD** (sekcje 7 i 9).
- Obrazki z prototypu są w `docs/img/`, a skrypty, które je generują, w `tools/pixel-proto/` (Python + Pillow, opis w GDD §8).

## Współpraca
- Rozmowa po **polsku**.
- Silnik: **Unity 6.3 LTS (6000.3.26f1)**, szablon Universal 2D. **Repo jest projektem Unity** (`Assets/`, `Packages/`,
  `ProjectSettings/` w katalogu głównym; instrukcja: `docs/UNITY_SETUP.md`). Gałąź główna `main` pochodzi z pusha z Unity.
  Kod gry piszemy tylko w `Assets/_Project/` (namespace `Wyspa.*`). Nie ruszaj `ProjectSettings/` ani plików szablonu bez potrzeby.
- Unity nie ma w kontenerze. Przed commitem kodu C# skompiluj go na atrapach:
  `dotnet build tools/cscheck -p:ExtraDefines="ENABLE_INPUT_SYSTEM%3BUNITY_STANDALONE_WIN%3BUNITY_EDITOR_WIN"`
  (.NET SDK doinstaluj skryptem `dotnet-install.sh` do scratchpada). Nowe API Unity dopisuj do `tools/cscheck/UnityStubs.cs`
  zgodnie z prawdziwą sygnaturą. Atrapy nie gwarantują zgodności z prawdziwym Unity, więc mów graczowi, co jest niesprawdzone.
- Grafiki-placeholdery generuje `tools/pixel-proto/export_unity.py` (paleta mroczna). Docelowe assety rysuje się ręcznie.
- Gracz woli mroczną, stonowaną estetykę (odrzucił „cukierkową”). Bez postaci, animacje tylko przez shadery i cząsteczki.
- **Lokalizacja (PL, EN, DE, JA, ZH):** żadnego tekstu w sprite'ach ani w kodzie na sztywno. Każdy napis to klucz z tabeli
  (Unity Localization, Smart Strings), a nowe terminy najpierw trafiają do `docs/localization/glossary.csv`. Zasady w GDD §16.
