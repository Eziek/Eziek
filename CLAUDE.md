# CLAUDE.md

Projekt gry w Unity: idle w okienku na pulpicie, wyspa w klimacie dark fantasy. Rośliny rosną od pracy gracza przy komputerze,
a lampy zasilane Esencją oświetlają wyspę w nocy (pora dnia z zegara komputera).

## Zanim cokolwiek zrobisz
- **Przeczytaj `docs/GDD.md`.** To źródło prawdy o projekcie: koncept, systemy, styl graficzny, paleta, otwarte decyzje,
  historia ustaleń. Po każdej nowej decyzji projektowej **zaktualizuj GDD** (sekcje 7 i 9).
- Obrazki z prototypu są w `docs/img/`, a skrypty, które je generują, w `tools/pixel-proto/` (Python + Pillow, opis w GDD §8).

## Współpraca
- Rozmowa po **polsku**.
- Projekt jest w fazie koncepcyjnej. Nie twórz kodu ani projektu Unity w repo, dopóki gracz o to nie poprosi.
  Pomysły i prototypy wizualne rób w scratchpadzie i pokazuj jako pliki.
- Gracz woli mroczną, stonowaną estetykę (odrzucił „cukierkową”). Bez postaci, animacje tylko przez shadery i cząsteczki.
