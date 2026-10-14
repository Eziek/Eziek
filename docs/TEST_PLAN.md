# Pockland: test całościowy (po Etapie 6d i dodatkach)

Jeden scenariusz zamiast sekcji 4s–4zb z `UNITY_SETUP.md`. Około 30–45 minut. Przy każdym punkcie: **OK** albo krótki opis,
co jest nie tak (najlepiej ze zrzutem i `Player.log` przy błędach). F1 otwiera HUD debugowy.

## 0. Przygotowanie
1. Pobierz gałąź, otwórz projekt, poczekaj na import (dźwięki `.ogg`, nowe sprite'y).
2. **Wyspa → 2** (dane: rośliny, ulepszenia, motyw UI, grafiki kotła, chowańców, tablicy). Potem **Wyspa → 3** nie jest potrzebne.
3. Window → General → Console: wyczyść, w trakcie testu patrz na czerwone błędy.

## 1. Menu główne i zapisy
1. Play: menu główne (nazwa gry, 3 sloty, język, wyjście), za nim przyciemniona wyspa. Bez HUD-u i powiadomień.
2. Stary zapis (jeśli był) jest w slocie 1 jako „Wyspa 1” z datą i godziną.
3. Klik „Język” kilka razy: menu przełącza się na EN / DE / 日本語 / 简体中文 i z powrotem (znaki CJK czytelne).
4. Pusty slot → „Nowa gra” → wpisz nazwę (sprawdź limit 10 znaków, Enter działa) → nowa wyspa.

## 2. Wprowadzenie (nowa wyspa)
1. Na czwartym kafelku tablica z karteczką, pod wyspą karteczka „1/5”, złota strzałka nad pustą grządką.
2. Posadź → „Pracuj” (pisz/ruszaj myszą; ikonki klawiatury i myszy w pasku HUD się zapalają) → zbierz.
3. „Dokup kafelek” (prawy klik → Rozbudowa) → „Kup lampę” (Sklep → Lampy) i postaw na nowym kafelku → „Gotowe”.
4. Tekst karteczki nie jest ucięty, strzałka jest nad właściwym celem.
5. Grządki na brzegu wyspy nie wystają nad wodę (pasek trawy przy brzegu).

## 3. Pasek HUD, cykl gry, pogoda, dźwięk
1. Pasek nad wyspą: waluty, Esencja, rytm, tempo; trzeci wiersz: słońce/księżyc, godzina gry, „noc za 23:40” / „świt za 6:12”
   (odliczanie co sekundę). Znika po 5 s bez ruchu myszy.
2. F1 → 19:30 / 23:30 / 12:00: zmierzch, noc (ciemniej, lampy, świerszcze), dzień (fale). Przenikanie dźwięku.
3. F1 → „+20 min” / „+1 cykl”: czas gry przeskakuje (pora dnia, pogoda, zamówienia, księżyc). Odliczanie w pasku się zgadza.
4. F1 → Deszcz / Burza / Mgła / Pogoda z cyklu: krople tylko nad wyspą, błyski i grzmot, gęsta mgła nocą.
5. Opcje → Dźwięk: suwaki tła i efektów działają, „Cisza, gdy okno nieaktywne”.

## 4. Gospodarka i budowle
1. Ceny: pierwsze kafelki tanie (5. ok. 81 monet), marchew rośnie ok. 3 min.
2. F1 → „+500 monet” kilka razy, kup ognika (Księga → Ulepszenia): dostajesz ognika i skrzynię.
3. Drzewo owocowe (Sklep → Drzewa), F1 → dojrzałe drzewa: ognik leci, dwa razy uderza w koronę, owoce spadają i zbiera je z ziemi.
4. Duszek-handlarz (Księga → Ulepszenia / Sklep → Budowle → klik w skrzynię): unosi się obok skrzyni, sprzedaje przy 20/20.
5. Kocioł (Sklep → Budowle): składniki ze skrzyń, „Wywar wzrostu” warzy się (ogień, bąble), potem „Wypij”; panel pokazuje działanie.
6. Sklep: długie opisy kończą się „...” przed przyciskiem, dymek pokazuje całość.

## 5. Zamówienia i chowańce
1. Przy karteczce „gotowe” zaczynają przychodzić zamówienia (toast „Nowa karteczka na tablicy”); po ok. 20 s karteczka
   wprowadzenia znika. Klik w tablicę: lista (kto, czego ile, cykle, nagroda). F1 → „Zamówienie teraz” dokłada kolejną.
2. Zbieraj plony z listy: postęp rośnie (także przy ogniku). Po wykonaniu: toast, monety lecą od tablicy.
3. F1 → „Wszystkie chowańce”: kot chodzi i siada, kruk lata między drzewami, ropucha skacze, nietoperz nocą
   krąży wokół lamp. Kursor obok: iskierki i dymek z bonusem.

## 6. Zapis, wczytanie, powrót do menu
1. Prawy klik → „Menu główne”: slot ma nową godzinę zapisu.
2. „Graj” na tym slocie: wszystko wraca (chowańce, kocioł w trakcie warzenia, mikstury, zamówienia, owoce pod drzewem, duszek przy skrzyni).
3. Drugi slot: nowa gra. Sprawdź, że **nic nie przeszło z pierwszego slotu** (mikstury, chowańce, zamówienia).
4. „Usuń” w menu: pierwszy klik „Na pewno?”, drugi usuwa.

## 6a. Rzeczy poprawione po przeglądzie kodu (sprawdź przy okazji)
1. Efekty dźwiękowe słychać (wcześniej grały z głośnością 0).
2. Pole nazwy w menu: widać migający kursor i zaznaczenie domyślnej nazwy.
3. Mały panel obiektu z 3 przyciskami w małym oknie: przyciski układają się jeden pod drugim, nic nie wystaje.
4. Mikstura wypita w slocie 1 nie działa w nowej grze w slocie 2.
5. Ołtarz: otwarty panel po świcie albo po wynurzeniu potwora nie zabiera plonów nocy.
6. Kupiec-widmo: otwarty panel po jego odpłynięciu nie sprzedaje.

## 7. Build Windows (osobno)
1. File → Build Profiles → Windows → Build, uruchom.
2. Ikona przy zegarze: lewy klik chowa/pokazuje okno, prawy klik: menu (pokaż/schowaj, na wierzchu, mini-tryb, wyjście).
3. Kliknij w inną aplikację i poczekaj na zdarzenie (np. F1 → kupiec-widmo): powiadomienie Windows (najwyżej raz na minutę).
4. Przezroczyste okno: mgła „chibi” wokół wody, deszcz nie pada na pulpit.
5. Restart Eksploratora (Menedżer zadań → Eksplorator Windows → Uruchom ponownie): ikona wraca sama.
6. Po wyjściu ikona znika. Przy problemach: `%USERPROFILE%\AppData\LocalLow\<firma>\<gra>\Player.log`.
