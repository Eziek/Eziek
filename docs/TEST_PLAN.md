# Misty Packland: test całościowy (po Etapie 6d, dodatkach i roju ogników)

Jeden scenariusz zamiast sekcji 4s–4zb z `UNITY_SETUP.md`. Około 30–45 minut. Przy każdym punkcie: **OK** albo krótki opis,
co jest nie tak (najlepiej ze zrzutem i `Player.log` przy błędach). F1 otwiera HUD debugowy.

## 0. Przygotowanie
1. Pobierz gałąź, otwórz projekt, poczekaj na import (dźwięki `.ogg`, nowe sprite'y).
2. **Wyspa → 2** (dane: rośliny, ulepszenia, motyw UI, grafiki kotła, chowańców, tablicy, gniazda i ogników). Potem **Wyspa → 3** nie jest potrzebne.
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
5. Opcje → Okno → Powiększenie x6 przy małej wyspie: pasek HUD i karteczka zostają w rozsądnym rozmiarze (na 1080p jak przy x3).
   „Rozmiar interfejsu”: auto / x1-x4 zmienia wielkość paska, karteczek i Księgi.
6. Opcje → Dźwięk: suwaki tła i efektów działają, „Cisza, gdy okno nieaktywne”.

## 4. Gospodarka i budowle
1. Ceny: pierwsze kafelki tanie (5. ok. 81 monet), marchew rośnie ok. 3 min.
2. F1 → „+500 monet” kilka razy, kup ognika (Księga → Ulepszenia): dostajesz gniazdo ogników z ogrodnikiem i skrzynię.
3. Drzewo owocowe (Sklep → Drzewa), F1 → dojrzałe drzewa: ognik leci, dwa razy uderza w koronę, owoce spadają i zbiera je z ziemi.
4. Duszek-handlarz (Księga → Ulepszenia / Sklep → Budowle → klik w skrzynię): unosi się obok skrzyni, sprzedaje przy 20/20.
5. Kocioł (Sklep → Budowle): składniki ze skrzyń, „Wywar wzrostu” warzy się (ogień, bąble), potem „Wypij”; panel pokazuje działanie.
6. Sklep: długie opisy kończą się „...” przed przyciskiem, dymek pokazuje całość.

## 4a. Rój ogników
1. Klik w gniazdo: lista ogników (imię, zawód, poziom, doświadczenie) i przyciski „Przywołaj” z ceną. Przywołaj drwala
   i zbieracza (F1 → monety, plony nocy). Każdy zawód ma inny kolor płomienia, trzy ogniki unoszą się nad pniem.
2. Bez pracy: ogniki krążą wokół gniazda i lamp, odpoczywają nad pniem, w locie zostawiają smugę iskier.
3. Drwal: leci do dzikiego drzewa / kamienia w polu 7x7, kilka razy uderza (dźwięk, liście), rzecz znika, ognik niesie
   ikonkę drewna/kamienia do gniazda, licznik drewna/kamienia w pasku rośnie. Po drzewie zostaje pień (drwal go potem też zabierze).
4. Ulepszenie „Ognik-siewca”: runa dziczy staje sama; klik w runę pokazuje odliczanie. Po 6 min gry:
   obok runy wyrasta drzewo, krzak albo kamień (z błyskiem). Przywołaj siewcę, w gnieździe „Siewca sieje: ...” otwiera koło nasion.
5. Kocioł + alchemik: w panelu kotła przycisk „Ognik-alchemik warzy: ...” przełącza przepis; ognik leci do skrzyni, potem do kotła
   i warzy; gotową miksturę wypija sam.
6. Noc: latarnik krąży od lampy do lampy (błysk nad lampą). Zbieracz zbiera spadłą gwiazdę (F1 → „Gwiazda”).
7. F1 → „Ogniki: +50 doświadczenia”: toast o awansie, ognik trochę większy i jaśniejszy, w gnieździe nowy poziom.
8. Stary zapis z ognikiem: po wczytaniu w miejscu ognika stoi gniazdo z ogrodnikiem. Zapis i wczytanie zachowują ogniki
   (imiona, poziomy), tryb siewcy i przepis alchemika.
9. Przeniesienie gniazda: ogniki wracają do nowego miejsca; rozbiórka: ogniki znikają, niesione plony trafiają do skrzyni.

## 4a2. Drzewko ulepszeń
1. Wyspa → 2 (nowe ulepszenia). Nowa gra → Księga → Ulepszenia: w środku ognik, wokół 4 węzły z kłódką („Ukryte we mgle”).
2. Kup ognika (klik): węzeł złoty, sąsiednie odkrywają się z ikonami, linie do kupionych są złote, a dalsze znów za mgłą.
3. Dymek: nazwa, poziom, efekt w procentach, koszt, „Kliknij, aby kupić” albo „Za mało środków”.
4. Drzewko mieści się na stronie (bez nachodzenia na tytuł i tekst na dole), także po niemiecku i w CJK.
5. Efekty: „Przestronne gniazdo” → w gnieździe 4 miejsca; „Pojemne skrzynie” → skrzynia 0/30; „Krzyżowanie” → dymek mówi 5%.

## 4a3. Podmiana roślin (dzień / noc)
1. Posadź roślinę nocną, F1 → 12:00: dymek grządki „czeka na noc” i „Kliknij, aby podmienić”. Klik: koło z nasionami dnia,
   wybór sadzi nową, nocne nasiono jest w worku (licznik w kole). PPM na grządce: „Wykop roślinę”.
2. Gniazdo z siewcą: przyciski „Siewca w dzień”, „Siewca w nocy” (koło od razu w dobrej kategorii), „Podmiana o świcie i zmroku”.
3. Ustaw dzień: marchew, noc: księżycowy kwiat. F1 → 23:30: siewca wymienia marchew bez lampy na kwiaty; F1 → 12:00: odwrotnie.
4. Podmiana wył.: siewca sadzi tylko na pustych grządkach. Zapis i wczytanie zachowuje oba ustawienia.

## 4a4. Skład
1. Sklep → Budowle → Skład (po ogniku-ogrodniku), postaw. Klik: ceny, rezerwa 30, przyciski ±10, sprzedaż wł./wył., „Sprzedaj teraz”.
2. Z drewnem/kamieniem ponad rezerwę: co 10 s monety lecą ze składu (do 10 sztuk każdego), surowce spadają do rezerwy.
3. Zapis i wczytanie zachowuje rezerwę i przełącznik.

## 4b. Hybrydy, wyprawy, zapiski
1. Wyspa → 2 (nowe rośliny). F1 → „Mutacje zawsze”: posadź obok siebie marchew i wilczą jagodę, zbierz: toast mutacji,
   w worku „Wilcza marchew”. Dymek nasiona w kole: „Krzyżówka: Marchew + Wilcza jagoda”. Sprawdź wygląd 4 faz każdej hybrydy.
2. Łódź: panel pokazuje „Dokąd płyniemy?”, dostępny tylko wrak, reszta z liczbą wypraw do odblokowania.
3. Wyślij na wrak, F1 → „Wyprawa: wróć teraz” → Rozładuj: tytuł panelu to nazwa celu, pierwsza linia „Zapisek: ...”, potem łup.
   Toast „Nowy cel wypraw: Kościana rafa”.
4. F1 → „Wyprawy +3 (cele)”: odblokowuje latarnię i cmentarzysko. Serce mgły: linia z licznikiem zapisków (np. 2/16).
5. Bestiariusz → Zapiski: zapiski w kolejności, 4 na stronę, strzałki stron. Nowe znaleziska „lore” na kafelkach dają
   zapiski Opiekuna po kolei (1, 2, 3...).
6. Stary zapis z łodzią w drodze: po wczytaniu wyprawa trwa dalej (cel: wrak albo cmentarzysko dla dawnej długiej).

## 4c. Zakończenie historii
1. Postaw ołtarz (Sklep → Morze). F1 → „Historia: odkryj zapiski”. Klik w ołtarz: napis o świetle w noc pełni,
   linia „Najbliższa noc pełni za ok. ... h”, przycisk „Zapal Światło Opiekunów (...)” nieaktywny.
2. F1 → 23:30 i „Wymuś pełnię”, F1 → monety/plony nocy, odłamki (gwiazda). Klik w przycisk: iskry nad lampami,
   dźwięk, toast, wynurza się oko (świeci cały czas), panel „Pierwszy Opiekun” z epilogiem: „Dalej” x2, „Pożegnaj”.
3. Po finale: ołtarz pokazuje nagrody, mgła w nocy rzadsza, w bestiariuszu 3 zapiski epilogu.
   Zapis i wczytanie: finał zostaje (nie da się go powtórzyć), zwykłe wynurzenia oka mają nowy opis.

## 4d. Grafika 16 px z wodą 32 px (powrót po Etapie 7)
1. Pobierz, poczekaj na import (setki PNG), **Wyspa → 2** (dane, materiały: kołysanie x1, obwódka mgły, sprite'y ryby i ćmy).
2. Rośliny, drzewa, obiekty, budowle, ogniki, stworzenia: jak przed Etapem 7 (16 px, ostre, mocny kontrast).
3. Teren: dawny klif i trawa 16 px, kanciasty brzeg; grządki pasują.
4. Woda bez zmian względem Etapu 7: gładkie pasy głębi, dno z kamieniami przesuwa się przy przesuwaniu kamery (PPM),
   światełka przy dnie (nocą jasne), fale idą do brzegu i rozbijają się w pianę, kręgi. Brzeg lądu nie odstaje od piany.
5. Mgła: kłębiaste chmury jak na `docs/img/mgla_chmury.png` (jasne czapy od lewej-góry, bliższe chmury zasłaniają dalsze,
   bez obrysu, w głębi ciemniej), drobne piksele jak woda. Powoli dryfują, bez skoku co pół minuty.
   W przezroczystym oknie gruby wał chmur (dwie warstwy) za pasem wody, nie wchodzi na pomost i łódź, kończy się przed
   krawędzią okna. Noc: ciemniejsza, ale widoczna. Zwykłe okno: przeciąganie PPM przesuwa widok daleko w mgłę
   (także gdy wyspa mieści się w kadrze), ale nigdy poza nią (bez pustego tła).
6. Opcje → Okno → Powiększenie: x2, x4, x6 (piksele ostre, bez rozmycia); Ctrl + kółko skacze co jeden poziom.
7. Wiatr: rośliny i drzewa kołyszą się o 1 piksel jak dawniej (nie za mocno), bez pionowej kreski.
8. Noc: ćmy wokół zapalonych lamp, świecące rośliny pulsują; co jakiś czas ryba (16 px) wyskakuje z wody z pluskiem.
9. Księga: drzewko ulepszeń i lista budowli przewijają się kółkiem i przeciąganiem.

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
4. Przezroczyste okno: wał chmur mgły wokół wody, deszcz nie pada na pulpit.
5. Restart Eksploratora (Menedżer zadań → Eksplorator Windows → Uruchom ponownie): ikona wraca sama.
6. Po wyjściu ikona znika. Przy problemach: `%USERPROFILE%\AppData\LocalLow\Eziek\Misty Packland\Player.log`.

## 8. Wydanie: Misty Packland (nazwa, ikona, build)
1. **Wyspa → 2**, potem **Wyspa → Wydanie → 1**: w konsoli „Ustawienia wydania: Misty Packland 0.9.0 (Eziek)”.
   Project Settings → Player: nazwa, firma, wersja, ikony (podgląd 16-1024), Splash Image: logo na granacie, bez logo Unity.
2. Menu główne: logo „MISTY PACKLAND” zamiast napisu, ostre piksele; na dole „v0.9.0”. Zmiana języka: logo bez zmian.
3. **Wyspa → Wydanie → 2**: powstaje `Builds/MistyPackland-0.9.0-win64/` (exe, README.txt, PRIVACY.txt,
   THIRD_PARTY_NOTICES.txt), obok `.zip` i `-symbols` (foldery DoNotShip poza paczką).
4. Build: ekran startowy z logo, ikona okna i paska zadań, ikona w zasobniku = ikona gry, podpowiedź „Misty Packland”.
5. Stare zapisy (z czasu „My project (5)”) pojawiają się w menu w nowym buildzie. Autostart włączony w starym buildzie
   po pierwszym uruchomieniu nowego wskazuje nowy plik (Menedżer zadań → Autostart: „Misty Packland”, bez „Pockland”).
6. Druga kopia gry się nie uruchamia (jedna kopia naraz).

## 9. Etap 8: losowe sadzenie, 60 ulepszeń handlu, prestiż
Najpierw **Wyspa → 2** (nowe ulepszenia, ikony kostki i perły).
1. Koło nasion na pustej grządce: pierwsza jest kostka. W dzień sadzi losową roślinę dnia, w nocy nocy (dymek mówi, którą porę).
   Bez pieniędzy na żadne nasiono kostka jest szara.
2. PPM na wyspie (bez przeciągania): w pierścieniu kostka „Obsiej puste grządki losowo”; wszystkie puste grządki dostają nasiona.
3. Gniazdo → „Siewca w dzień / w nocy” → kostka „losowo”: ognik sadzi na każdej grządce inną roślinę, o świcie i zmierzchu podmienia.
4. Księga → Ulepszenia: drzewko otwiera się na ogniku-ogrodniku, przewija się w obie strony. Po Uczciwym handlu, Szybkiej łodzi,
   Pojemnych skrzyniach, Żyznej glebie, Nocnej rosie, Krzyżowaniu, Dzikich runach, Słoju, Drugim oddechu i Skupieniu
   pojawiają się nowe węzły za mgłą (kłódka). Dymek: nazwa, poziom, efekt z liczbą, koszt.
5. F1: dużo monet, kup Stragan, Targowanie, Złoty interes. Zbieraj ręcznie: ceny wyższe, czasem toast „Złoty interes! x3”.
6. Popyt dnia: o świcie toast z rośliną; w kole nasion przy niej „Dziś popyt: x1.5”. Odsetki i Dzierżawa: toast o świcie z monetami.
7. Gorliwy handlarz: duszek sprzedaje przy połowie skrzyni. Kupiec-widmo: napiwek przy przybyciu, tańsze towary, dłuższy pobyt.
8. Księga → Rejs (ikona perły): perły, blask, zarobek rejsu i próg następnej perły, pod spodem kolekcja darów (bez kupowania).
   Szczegóły rejsu i Przystani: sekcja 12.
9. (zastąpione przez sekcję 12)
10. Stary zapis (v14) wczytuje się bez błędów: zarobek rejsu od zera, perły 0.

## 10. UI i woda (porządki)
1. Księga: Ulepszenia, potem Rytuały, Bestiariusz, Sklep - żadna strona nie przewija się w bok, pierwsze litery całe.
2. Teksty na pergaminie (status rytuału, poziomy darów, opisy w sklepie) czytelne; nazwy zapisów w menu głównym widoczne.
3. Opcje → Ogólne → Czcionka: Czytelna / Pikselowa 3x5 przełącza od razu, wiersze się nie rozjeżdżają.
4. Mini-tryb i okno x2: Księga, dymki i toasty w czytelnej skali (nie x1).
5. Dymek przy prawej i dolnej krawędzi przeskakuje na drugą stronę kursora; długi opis (Ognik-ogrodnik, DE) się zawija.
6. Kup ulepszenie z kilkoma poziomami i nie ruszaj kursora: dymek pokazuje nowy poziom i koszt.
7. Toast przy otwartej Księdze (np. kupno ulepszenia) widać nad Księgą; toasty nie zasłaniają paska HUD.
8. Pasek HUD nie skacze na boki, gdy pojawia się i znika „Tempo”.
9. Łódź przy pomoście, łódź kupca i boja: dół w wodzie, piana przy burtach, kręgi co kilka sekund.
10. Przezroczyste okno: między wodą a chmurami nie widać pulpitu. Zwykłe okno: woda między wyspą a mgłą.
11. Scena: Wyspa → 3 buduje `MainLevel.unity` i ustawia ją w Build Settings.

## 11. Pogaduszki ogników, ocieranie, przenoszenie pomostu
1. Gniazdo z 2-3 ognikami, mało pracy (np. wszystko zebrane): po ok. 10-45 s dwa wolne ogniki zlatują się, patrzą na siebie
   i na zmianę pokazują dymki z runami (runy pojawiają się po kolei, z rozbłyskiem), na koniec czasem „!”, „?”, „...” albo nutka.
2. Szczebiot: każda runa daje sylabę, każdy ognik mówi trochę innym głosem. Opcje → Dźwięk → „Głosy ogników” wyłącza dźwięk
   (dymki zostają). Ognik poza ekranem nie słychać.
3. W nocy dymek i runy są czytelne (bez przyciemnienia). Dymek nad ognikiem na wyższym poziomie ma równe piksele.
4. Rozmowa się przerywa bez błędów, gdy gniazdo przeniesiesz albo rozbierzesz w trakcie (dymek znika).
5. Ogniki przy gnieździe i przy mijaniu się w locie nie wchodzą w siebie: odsuwają się płynnie, nie migoczą; niesione plony
   lecą razem ze sprite'em; po rozminięciu wracają na tor.
6. Klik w deski pomostu (nie w rybę): panel „Pomost” z „Przenieś”. Duch pomostu na wodzie przy brzegu, czerwony w złym miejscu.
   Po postawieniu: sieci i łódź przy nowym końcu pomostu, ryby wokół nowego końca, zapis i wczytanie trzymają nowe miejsce.
7. Łódź na wyprawie w trakcie przenoszenia: po przeniesieniu nadal na wyprawie (postęp bez zmian), wraca do nowego pomostu.
8. Klik w boję sieci: panel z czasem do połowu i „Przenieś” (to samo przenoszenie pomostu). Dymek nad boją: „Sieci rybackie”.

## 12. Przystań we mgle i losowane dary
Najpierw **Wyspa → 2** (motyw UI wczytuje nowe ikony `icon_gift_*`; bez tego dary mają zapasowe ikony UI).
1. F1 → „Perły mgły +5”, monety na co najmniej 10 000 zarobku, Księga → Rejs → „Odpłyń we mgłę” → drugi klik:
   zamiast nowej wyspy ekran **Przystań we mgle** (wyspa w tle przygaszona mgłą), u góry perły, nr rejsu, „dary X/37”.
2. Kolekcja: 37 ramek, kolor ramki = rzadkość (szary, zielony, niebieski, fioletowy, złoty). Niezdobyte wyszarzałe,
   dymek: nazwa, rzadkość, efekt, „Jeszcze nie zdobyty”.
3. „Losuj dar”: ikony zlatują się w krąg, kręcą się i zacieśniają, po kolei wylatują na boki i szarzeją, ostatnia z grzmotem;
   zwycięzca x2 na środku w rozbłysku koloru rzadkości, napis „Nowy dar: ...” albo „...: poziom 2/3”, potem wszystkie
   wracają na miejsca, a nowy dar mruga i jest w kolorze z kropką poziomu. Klik w trakcie przyspiesza. Perły -1.
   Przy wygranej fanfara (epicki i legendarny: dłuższa, z gongiem) i fajerwerki w kolorze rzadkości (więcej rakiet przy rzadszych
   darach), z wystrzałem i trzaskiem; fajerwerki dogasają także po powrocie ikon i nie blokują przycisków.
4. Kilka losowań pod rząd: ten sam dar wchodzi na wyższy poziom; dar z jednym poziomem po zdobyciu już nie walczy.
   Bez pereł przycisk „Brak pereł” (nieaktywny). Dymek przycisku: szanse rzadkości.
5. Zamknij grę w Przystani i uruchom ponownie: slot w menu „We mgle: czeka w przystani”, „Graj” wraca do Przystani,
   perły i dary jak przed zamknięciem. „Menu główne” z Przystani też działa.
6. „Wypłyń na nową wyspę”: nowa wyspa z darami (Posag: monety, Pełna spiżarnia: drewno i kamień, Wierny ognik, Pamięć nasion,
   Nawóz z mgły, Żar w zanadrzu: pełny słój). Dary efektowe działają (np. Skrzydła mgły: szybsze ogniki, Głębokie skrzynie).
7. Księga → Rejs w nowej grze: kolekcja zgodna z Przystanią, rejs nr 2.
8. Zapis v15 (stare dary kupione w Księdze) wczytuje się: poziomy darów zostają (przycięte do nowych maksimów).

## 13. Start zawsze rano
1. Nowa gra w pustym slocie, gdy cykl jest w nocy (np. F1 → „+20 min” w menu nic nie da, sprawdź po prostu kilka razy o różnych
   porach): zegar w HUD pokazuje 07:00, jest dzień, do zmroku ok. 37 min.
2. Opcje → Reset: znów 07:00. Wypłynięcie z Przystani we mgle na nową wyspę: 07:00.
3. Zapis i wczytanie po kilku minutach: zegar idzie dalej od miejsca zapisu (nie wraca do 07:00), noc przychodzi normalnie.
4. Stary zapis (sprzed tej zmiany): pora dnia taka jak wcześniej.

## 14. Darmowa budowla na pełnej wyspie
1. Rejs z darem „Wierny ognik”: na nowej wyspie (2x2, pełna) mgła odsłania 1-2 darmowe kafelki obok, stoją na nich
   skrzynia i gniazdo z ognikiem-ogrodnikiem. Cena następnego kafelka w rozbudowie taka jak bez tych kafelków.
2. Stary zapis, w którym „Ognik-ogrodnik” był kupiony, a gniazda nie ma: po wczytaniu gniazdo z ognikiem się pojawia.
3. Zwykła gra: kup „Ognik-ogrodnik”, gdy wyspa jest pełna: to samo (darmowy kafelek), tylko raz na rodzaj budowli.

## 15. Scenka rejsu
1. Wieczorem z zapalonymi lampami: Księga → Rejs → „Odpłyń we mgłę” → drugi klik. HUD znika, ogniki lecą do gniazd, lampy
   gasną po kolei (latarnia przy środku ostatnia), kamera zsuwa się w dół, napis „Mgła zamyka się nad wyspą...” pisze się
   litera po literze, pikselowa mgła zamyka okno od brzegów. Klik przyspiesza. Potem Przystań.
2. Przystań: mgła rozstępuje się nad pergaminem „Koniec rejsu nr N”, wiersze pojawiają się po kolei, perły złotem z dźwiękiem
   monety. „Do przystani” → losowanie darów. Zamknij grę i wróć: podsumowanie już się nie pokazuje.
3. „Wypłyń na nową wyspę”: okno zaczyna w mgle (bez mignięcia wyspy), mgła rozstępuje się od środka, kamera podpływa,
   latarnia rozbłyska (także w dzień), na dole „Rejs nr N” i linijka zapisków. Potem HUD wraca, można grać.
   Po scenie tabliczka znika w całości (nie zostaje sam tytuł).
4. Przewijanie PPM, Ctrl+kółko, pierścień PPM i kliki w wyspę nie działają w trakcie scenek.
5. Czwarty ognik (Przytulne gniazdo) zostaje po zapisie i wczytaniu. Rozbiórka budowli nie podnosi „zarobku rejsu”.

## 16. Świt i zmierzch
1. F1 → godzina tuż przed 20:00 (albo „+20 min” w odpowiedniej chwili): o zmroku lampy zapalają się po kolei od latarni
   przy środku, każda z małym błyskiem; ćmy nadlatują z daleka do zapalonych lamp.
2. Tuż przed świtem (06:00): mgła na moment podchodzi pod sam brzeg i przez ok. 20 s się cofa; z drzew zrywa się kilka
   ptaków; ogniki pokazują dymki z „...” z sennym mruczeniem.
3. Wczytanie gry w nocy albo rano: bez scenek (lampy od razu w swoim stanie). Przezroczyste okno: bez cofania się mgły.

## 17. Narzędzia ogników
Najpierw **Wyspa → 2** (grafiki narzędzi). Bez tego ogniki są bez narzędzi, jak wcześniej.
1. Każdy zawód ma narzędzie z boku: drwal siekiero-kilof, ogrodnik sierp, siewca worek, alchemik fiolka, latarnik
   latarenka, zbieracz koszyk. Narzędzie przechodzi na drugą stronę, gdy ognik leci w lewo.
2. Drwal: przy każdym uderzeniu głowica opada (dźwięk „chop”). Ogrodnik: zamach sierpem przy zbiorze. Siewca: worek
   przechyla się nad grządką przy sadzeniu.
3. Zbieracz: zebrane owoce, odłamki gwiazd i monety z trawy leżą w koszyku (wystają nad brzeg), obracają się z koszykiem.
   Ogrodnik i siewca nie mają pod sobą plonów ani nasion (zbiór, dostawa do skrzyni i sadzenie działają jak wcześniej).
4. Latarenka i fiolka świecą w nocy (piksele emisyjne), płomyk migocze, wywar bulgocze.
5. Pogaduszki i odsuwanie się ogników działają jak wcześniej, narzędzia nie zasłaniają dymków.

## 18. Sklep z czapkami
Najpierw **Wyspa → 2** (grafiki czapek).
1. Księga → Sklep → Czapki: 15 czapek z podglądem, cena w monetach albo perłach (ikona perły), „Kup” nieaktywne bez środków.
2. Kup czapkę za monety (F1: dużo monet) i za perły (F1: „Perły mgły +5”): toast, wiersz pokazuje „Masz”.
3. Panel gniazda: przy każdym ogniku przycisk „Czapka – Imię: ...”; klik przełącza posiadane czapki i „bez czapki”.
   Czapka siedzi na czubku głowy, obraca się z ognikiem, dymek pogaduszek pojawia się nad czapką.
4. Świeca, aureola, korona, wianek, dynia: w nocy świecące piksele.
5. Zapis i wczytanie: czapki na ognikach zostają. Rejs we mgłę: kupione czapki zostają w sklepie („Masz”) na nowej wyspie.
6. Czapki są małe i siedzą na czubku głowy (oczy widoczne), także gdy ognik leci w lewo. Dwa ogniki blisko siebie:
   jeden rysuje się w całości przed drugim (narzędzie i czapka nie wchodzą na ciało sąsiada), odsuwają się wyraźniej.

## 19. Ognik-siewca i podsumowanie rejsu
1. Kup „Ognik-siewca”: w pierwszym gnieździe pojawia się siewca (błysk, toast „Ognik-siewca dołączył do gniazda”), runa
   dziczy nie pojawia się sama; w Sklep → Budowle runę da się kupić.
2. Wszystkie gniazda pełne: toast „Gniazda pełne: ognik-siewca czeka...”; po zwolnieniu miejsca i kolejnym zakupie
   ulepszenia albo wczytaniu siewca dołącza.
3. Stary zapis z „Ognik-siewca” i runą: po wczytaniu dochodzi siewca, runa zostaje.
4. Podsumowanie rejsu (PL, DE, EN): żaden wiersz nie wchodzi na ramkę, okno szersze przy dłuższych tekstach.

## 20. Pasek HUD nad wyspą
1. Wysokie obiekty w górnym rzędzie (dynia, latarnia, runa, gniazdo): pasek HUD wisi nad nimi, nie zasłania ich.
2. Duża wyspa albo małe okno: pasek dalej mieści się w oknie (przy górnej krawędzi).

## 21. Jakość plonów
1. Dynia posadzona w dzień, rośnie przy pisaniu (Tempo x1,3+), bez nawozu i bez klatki z ognikiem: dojrzała ma jakość 1
   (dymek bez „Jakość”).
2. Z nawozem (kupiec-widmo albo dar „Nawóz z mgły”): „Jakość: 2/3 (nawóz)”.
3. Roślina dnia rosnąca nocą głównie w świetle klatki z ognikiem: „Jakość: 2/3 (światło ognika)”, z nawozem 3/3.

## 22. Żar i kupno kafelka
1. Żar tuż poniżej ceny kafelka (np. 2,6 przy cenie 3, F1): HUD pokazuje 2/60, dymek kafelka „Brakuje Żaru: 1”, cena
   czerwona. Po dojściu do 3: HUD 3/60, cena jasna, klik kupuje kafelek.

## 23. Płynność przy zakupie kafelka
1. Duża wyspa (F1: kilkadziesiąt kafelków), kup kafelek w trybie rozbudowy: ogniki, woda i kursor nie zatrzymują się,
   kafelek wynurza się w 3 krokach jak wcześniej (piana od razu, ląd po chwili).
2. Kilka zakupów szybko pod rząd: każdy kafelek pojawia się, nic nie znika i nie miga stary obraz.
3. Kupno „Ognik-ogrodnik” na pełnej wyspie zaraz po zakupie kafelka: darmowy kafelek pod budowlę jest widoczny.

## 24. Panel gniazda z zakładkami
1. Klik w gniazdo: pod tytułem zakładki Ogniki / Czapki / Siewca / Przywołaj (Czapki tylko z kupioną czapką, Siewca tylko
   z siewcą). Ogniki: zasięg i lista ogników. Przenieś i Rozbierz pod każdą zakładką.
2. Czapki: klik w wiersz ognika zmienia czapkę, panel zostaje otwarty, można klikać dalej (aż do „bez czapki”).
3. Siewca: „Podmiana o świcie i zmroku” przełącza się bez zamykania; wybór nasion otwiera koło nasion (panel się zamyka).
4. Przywołaj: przywołany ognik od razu widać w zakładce Ogniki po przełączeniu; panel zostaje otwarty; pełne gniazdo = napis.
5. Zamknij panel i otwórz to samo gniazdo: ta sama zakładka; inne gniazdo: pierwsza zakładka.

## 25. Menu główne, zapis i burza
1. Start gry: menu ma logo i tylko Start, Ustawienia, Wyjdź. W tle deszcz, co kilka sekund piorun za wyspą (w morze,
   nie w ląd), rozbłysk sceny i grzmot chwilę później. Góra pioruna chowa się w chmurach mgły.
2. Klik w tło (poza przyciskami): piorun w to miejsce. Klik w przycisk nie wywołuje pioruna.
3. Ustawienia: Księga tylko z zakładką Opcje, bez resetu zapisu i bez „Wyjdź” na stronie; zamknięcie wraca do menu.
4. Start bez żadnego zapisu: nowa wyspa (07:00). Start z zapisem: wczytuje ostatnio grany zapis (także po starych slotach
   2-3 ten najnowszy). Start po zamknięciu gry w Przystani: Przystań we mgle.
5. W grze: PPM → „Zapisz grę” (ikona zwoju z pieczęcią po Wyspa → 2): toast „Zapisano grę”. Ctrl+S robi to samo.
   Ctrl+S w menu i podczas scenki rejsu nic nie robi.
6. Menu główne z pierścienia: powrót do menu z burzą; Start wraca do tej samej wyspy.

## 26. Pierścień akcji z podmenu, Ustawienia z menu
1. PPM na pustej grządce rosnącej rośliny (najwięcej ikon: zasyp, wykop, losowe sadzenie + 4): ikony nie nachodzą na
   siebie, także podświetlona.
2. Ikona „Menu”: w tym samym miejscu drugie koło (Opcje, Zapisz grę, Menu główne z domkiem, Wyjdź), w środku ikona
   „Wstecz” wraca do pierwszego koła. Dymek z poprzedniej ikony nie zostaje na ekranie.
3. Koło przy krawędzi okna mieści się w oknie (pierwsze i drugie).
4. Menu główne → Ustawienia: Księga bez kolumny zakładek po lewej, strona przy lewej krawędzi, krzyżyk w rogu.
   W grze Księga ma zakładki jak dawniej.

## 27. Krótki wzrost i 6 faz
Najpierw Wyspa → 2 (nowe czasy, ceny i sprite'y faz).
1. Marchew przy pracy w dzień: dojrzewa w ok. 20 s (z tempem pracy szybciej), po drodze 6 faz: nasiono, kiełek z listkami,
   sadzonka, rośnie, niedojrzała (zielona główka), dojrzała. Przy każdej zmianie „pyknięcie”.
2. Dynia: niedojrzała to zielona dynia, po 2 min pomarańczowa. Zboże: zielone kłosy, potem złote.
3. Rośliny nocy: trupi grzyb w 2 min nocy, cmentarny bluszcz w 10 min; świecenie dopiero w fazie dojrzałej.
4. Koło nasion i Sklep → Nasiona: czasy „20 s pracy”, „2 min pracy”, „1 min 30 s nocy”, „10 min nocy” (bez „0 min”).
5. Zbiór rośliny nocy: plony nocy nie przy każdym zbiorze krótkich roślin (trupi grzyb ok. co piąty), bluszcz zawsze 1.
6. Drzewo: jabłoń dorosła po ok. 4 min pracy, owoce co 1,5 min.
7. Zamówienia: więcej sztuk (np. 6-10 marchwi na początku), zapłata proporcjonalna.
8. Kupiec-widmo: paczki po 10 nasion, hybryda po 3 nasiona.
9. Stary zapis: rośliny w trakcie wzrostu od razu dojrzałe albo prawie (wzrost liczony w sekundach), nic się nie psuje.

## 28. Świt i zmierzch
1. F1: godzina 06:30 (świt). Pasek HUD: „06:30  świt, dzień za …”. Roślina nocy (nie pod lampą) rośnie dalej, dymek:
   „Świt: rośnie jeszcze …”. Roślina dnia też rośnie (gracz aktywny).
2. Siewca z podmianą: o świcie nie rusza rośliny nocy, która jeszcze rośnie; po 08:00 podmienia tylko te urośnięte
   najwyżej w połowie (np. 30%), a roślina na 80% zostaje na grządce do zmierzchu.
3. 18:30 (zmierzch): HUD „zmierzch, noc za …”, roślina nocy już rośnie, roślina dnia rośnie, dymek „Zmierzch: rośnie
   jeszcze …”. Snopy światła pomarańczowe o świcie i zmierzchu.
4. W dzień (np. 12:00): HUD „zmierzch za …”; w nocy „świt za …”.
5. Zamknij grę na ok. 1 h: rośliny nocy urosły o ok. 31 min (noc + świt + zmierzch), nie 20.
6. Rytuał Zmierzchowe ziarno: opis „O świcie i zmierzchu rośliny rosną 50% szybciej”.

## 29. Wschód, zachód, światło i cienie
1. F1, godziny po kolei 05:30 → 08:30: przed świtem fioletowo, potem różowo, złoto, dzień jak wcześniej. Po prawej stronie
   wyspy ciepła poświata, gaśnie około 08:00. Snopy światła padają z prawej.
2. 18:00 → 21:00: złota godzina, pomarańcz, fiolet, noc jak wcześniej. Poświata z lewej strony. Snopy z lewej.
3. Cienie w dzień: ok. 07:00 długie w lewo, 13:00 krótkie (prawie pod obiektem), 17:00-19:00 długie w prawo. Cienie
   nie wychodzą na wodę, są pod obiektami i nad grządkami, nie migają przy przesuwaniu kamery.
4. Deszcz: cienie słabsze. Przeniesienie budowli, nowa dekoracja, ścięte drzewo: cień idzie za obiektem albo znika (do 0,5 s).
5. Noc: obiekty przy zapalonej lampie mają cień od lampy (w przeciwną stronę); dalej od lamp słaby cień od księżyca.
   Lampa nie rzuca cienia od własnego światła.
6. Wydajność: duża wyspa (F1, kilkadziesiąt obiektów) bez spadku płynności.
7. Przezroczyste okno i menu główne: cienie i kolory działają, nic nie wychodzi poza ląd.

## 30. Cienie roślin i ogników, wyrywanie, zabawy ogników
1. W dzień rośliny na grządkach mają cień jak drzewa (rano w lewo, wieczorem w prawo); ogniki mają pod sobą plamkę
   cienia, która idzie za nimi. W nocy plamki nie ma.
2. Ogrodnik przy dojrzałej roślinie: zniża się, roślina drży i wyciąga się, okruszki ziemi, wyskakuje z ziemi (ziemia
   pryska), leci łukiem do ręki, ognik podskakuje. Plon trafia do skrzyni jak wcześniej, jakość bez zmian.
3. Dwa lub więcej wolnych ogników przy gnieździe (np. wszystko zebrane): co jakiś czas zabawa. Dzień: berek, wyścig
   (do lampy albo drzewa, „! ! !” na start, zwycięzca pętla z gwiazdką), podrzucanie plonu. Noc: taniec w kółku,
   drzemka z „Z”. Po zabawie ogniki wracają do pracy.
4. Zabawa w trakcie: pojawia się praca (dojrzała roślina) - zabawa kończy się najpóźniej po kilku sekundach, potem praca.
5. Mijające się w locie ogniki: z czapką unoszą ją na chwilę, bez czapki iskry między nimi. Nie częściej niż co 25 s.
   Ogniki wiszące obok siebie przy gnieździe nie witają się w kółko.
6. Awans ognika (F1: doświadczenie): wolne ogniki obok przylatują i krążą wokół niego z gwiazdką.
7. Odpłynięcie we mgłę w trakcie zabawy: wszystkie ogniki wracają do gniazd, nikt nie zostaje zawieszony.

## 31. Chowańce
Najpierw Wyspa → 2 (nowe grafiki chowańców). F1: „Wszystkie chowańce”, „Więź +1”.
1. Osiem chowańców na wyspie, nowe sprite'y (kot, kruk, ropucha, nietoperz, ćma, sowa, ślimak, lis-widmo w nocy).
2. Kot: chodzi (4 klatki), siedzi z ogonem i mruganiem, myje łapkę, w nocy śpi przy gnieździe z „Z”, przeciąga się;
   skacze na ognika lecącego nisko obok (ognik podskakuje z „!”).
3. Kruk na drzewie/lampie dziobie i kracze, ląduje przy grządkach i skacze; kot podchodzi - kruk odlatuje.
4. Ropucha: w nocy język (iskra), w deszczu rechot (nadęty worek). Nietoperz w dzień wisi i się kołysze, w nocy przy
   lampach, czasem goni ćmę (ćma ucieka).
5. Ćma w nocy przy lampach świeci (fioletowe światło), w dzień siedzi ze złożonymi skrzydłami. Sowa w dzień z zamkniętymi
   oczami, w nocy obraca się w stronę ogników i pohukuje. Ślimak pełznie i zostawia ślad, pod kursorem chowa się.
   Lis-widmo pojawia się o zmierzchu (półprzezroczysty, migocze), znika o świcie, w dzień nie da się go kliknąć.
6. Klik w chowańca: panel z bonusem, „Więź: 0/4”, „Pogłaszcz” (serce, poza), „Nakarm: przysmak (koszt)”. Po
   nakarmieniu: toast, „Następny przysmak w kolejnej dobie”; po 1, 3, 6, 10 karmieniach nowy poziom i większy bonus.
7. Prezenty: kot monety raz na dobę, kruk nasiona, nietoperz nawóz o świcie (toasty).
8. Bestiariusz → Chowańce: nieprzybyłe jako ciemna sylwetka, „???”, podpowiedź z postępem (np. „Postaw lampy (1/3)”);
   przybyłe z bonusem i więzią.
9. Zapis i wczytanie: chowańce, więź, „nakarmiony dziś”, liczniki warunków zostają.

## 32. Domki chowańców, ognisko, sennik, nastrój ogników
Najpierw Wyspa → 2. F1: „Wszystkie chowańce”, „Ogniki zmęczone”, „Ogniki wypoczęte”.
1. Sklep → Budowle: domki tylko przybyłych chowańców; po postawieniu „Już stoi”. Ognisko i sennik z ceną rosnącą.
2. Kot z koszem: spaceruje w pobliżu kosza, w nocy (i czasem w dzień) śpi w koszu, rysuje się na koszu, nie za nim.
   Lis-widmo: biega wokół budy, późną nocą zwija się przed wejściem. Sowa w dziupli w dzień, nietoperz pod budką,
   kruk na grzędzie, ropucha na liściu w oczku, ćma przy latarence, ślimak na kamieniu.
3. Bonus chowańca z domkiem wyższy (dymek i panel: np. ropucha 15% → 18%); panel domku „Tu mieszka: …”.
4. Ognisko obok gniazda: wolne ogniki krążą i wiszą przy ogniu (twarzą do ognia), zabawy przy ognisku.
5. „Ogniki zmęczone”: ogniki ziewają i lecą spać do sennika (najwyżej 3), reszta nad gniazdo; przygasają, „Z”,
   po przebudzeniu gwiazdka. Panel gniazda: „odpoczywa”, potem „nastrój 85% (praca 100%)”.
6. Nastrój poniżej 60: w panelu „praca 80%” itp., ognik lata wolniej.
7. Bez ogniska i sennika przy zmęczonych ognikach: podpowiedź w panelu gniazda.
8. Zapis i wczytanie: nastrój ogników zostaje, domki stoją, chowańce wracają do domków.


## 33. Drzewa, ozdobniki na wodzie, wysepki
Najpierw Wyspa → 2 (nowe grafiki drzew i ozdobników). F1: „Do wysepki”, „+10 kafelków”, „Nowy świat”.
1. Drzewa: okrągłe, świerk, krzak i martwe drzewo mają nowe grafiki (kępy liści, kora, korzenie); kołyszą się bez
   ucinania liści na brzegach; ścięte okrągłe drzewo przewraca się w nowej grafice. Drzewa owocowe: wszystkie fazy
   i przewracanie w nowym stylu, świecące owoce (gruszka-widmo, księżycowe) dalej świecą nocą.
2. Woda: przy brzegu kilka lilii, liści, rzęsy; każdy powoli dryfuje (pół minuty na okrążenie), bez migotania
   i rozmycia. Ozdobniki nie leżą na lądzie, pomoście ani łodzi. Nocą mglista (fioletowa) lilia lekko świeci.
3. Po zakupie kafelka obok ozdobnika: ozdobnik pojawia się gdzie indziej, reszta zostaje. Przy większej wyspie jest ich więcej.
4. „Do wysepki”: z mgły wyłania się wysepka (toast, piana), okno obejmuje ją; ma klif, kontur i szczątki (sekcja 34).
5. Klik w wysepkę: panel „Odcięta wysepka” z opisem, liczbą kafelków, odległością w kafelkach wody i „Rozbudowa”.
   Na wysepce nie da się kopać ani usuwać ozdób.
6. Tryb rozbudowy: kafelek tuż obok wysepki (także po skosie) ma „+” zamiast ceny i podpowiedź „Przyłączy wysepkę za darmo!”.
7. Kupno tego kafelka: toast „Wysepka przyłączona: +3/4 kafelki”, znaleziska spod jej kafelków, na kafelkach szczątki
   do odbudowy (sekcja 34); cena kolejnego kafelka rośnie tylko o jeden zakup.
8. Zapis i wczytanie: przyłączona wysepka zostaje, odcięte wracają w tych samych miejscach. Stary zapis wczytuje się
   bez błędów (wysepki tuż przy lądzie po prostu się nie pojawiają).

## 34. Wyspy fabularne: monumenty, spadające liście
Najpierw Wyspa → 2. F1: „Do wysepki”, „Monument: etap”, waluty (drewno, kamień, monety), „Ogniki wypoczęte”.
1. Nowy świat: 2 wysepki z ruiną monumentu (jeden duży obiekt na 3x2 kafelkach) i 1-2 zwykłe z drzewami i kamieniami.
   Klik w wysepkę z monumentem: „Na wysepce stoi zrujnowany monument: …”.
2. Po przyłączeniu: toast o monumencie. Klik w monument: opis odbudowy, co da budowla, „Etap odbudowy: 0/4”, koszt,
   „Odbuduj” (wyszarzone bez surowców). Kafelków pod monumentem nie da się przekopać ani zabudować.
3. „Odbuduj”: surowce znikają, toast „Ogniki zaczynają budowę”. Wolne, wypoczęte ogniki (najwyżej 3) lecą do monumentu,
   stukają przy rusztowaniu (dołek co chwilę), sypią się wióry i odpryski. Panel: „Budowa: X% (ogniki na rusztowaniu: n/3)”.
4. Bez pracy gracza procent stoi. Z jednym ognikiem etap trwa ok. 10 min pracy, z trzema wyraźnie krócej.
5. Koniec etapu: monument zmienia wygląd (mur wyżej, rusztowanie), kłęby pyłu, toast „Ukończono etap 1/4” z zapiskiem;
   ogniki wracają do swoich zajęć. Następny etap droższy o 40%.
6. Etap 4: gotowa budowla (świecące okna nocą), toast z nazwą i zakończeniem historii; panel: co daje i kiedy następny dar.
7. Zapis i wczytanie w trakcie budowy (etap i procent zostają) i po niej (licznik daru zachowany).
8. Liście: z każdego drzewa (także owocowych) co kilka sekund spada liść, kołysze się i chwilę leży pod drzewem; nocą
   rzadziej; w podmuchu wiatru częściej i znoszone w bok.

## 35. Generatory Żaru
Najpierw Wyspa → 2. F1: waluty (drewno, kamień, monety).
1. Sklep → Budowle: „Piec Żaru” i „Kryształ Żaru” dostępne od początku, z opisem i ceną (kolejna sztuka droższa).
2. Piec z gniazdem ogników: wolny ognik leci do gniazda, bierze 2 polana (widać je pod nim), leci do pieca i wrzuca je
   w ogień (iskry, drewno ubywa ze składu dopiero przy wrzuceniu). Drwal po ścięciu drzewa niesie drewno do pieca, gdy ten
   czeka na opał. Panel: „W palenisku: X/6 drewna”, „Zostawia drewna: 20”, stan (pali się / słój pełny / brak drewna /
   czeka na ognika / wygaszony), rezerwa ±10, wł./wył. Co 20 s z paleniska ubywa 2 drewna, przybywa 1 Żaru.
   Bez żadnego ognika na wyspie piec sam bierze drewno ze składu. Działa też bez pracy gracza.
3. Kryształ: pulsuje, słój od razu większy o 20 (HUD: np. 60 → 80), Żar rośnie powoli także bez pracy i nocą.
   Rozbiórka kryształu zmniejsza słój z powrotem.
4. Zapis i wczytanie: rezerwa i wyłączenie pieca zostają; Żar ponad 60 przy krysztale nie przepada po wczytaniu.

## 36. Gatunki ryb pod wodą
1. Wokół wyspy pływa do 16 ryb w różnych kształtach: zwykłe, ławica 4 rybek, wijący się węgorz, płaszczka machająca
   skrzydłami, duży karp (rozwidlony ogon), jasna pulsująca meduza z czułkami, żabnica z przynętą.
2. Duże gatunki (płaszczka, karp, meduza, żabnica) pływają dalej od brzegu i wolniej; żadna ryba nie wpływa na ląd.
3. Nocą świecą: oczko co trzeciej zwykłej ryby i przynęta żabnicy.
4. Pomost: klik w rybę łowi ją (żabnica nocą jak świecąca), w meduzę nic nie robi; złowiona ryba znika i wraca.

## 37. Ptaki
Najpierw Wyspa → 2. W dzień, kilka minut obserwacji (pomost ułatwia mewy).
1. Oprócz wron i zięb pojawiają się: sroka (na trawie, podskakuje), gil i sikora (w koronach drzew, czasem po 2-3),
   mewa (siada na końcu pomostu albo na wodzie przy brzegu, czasem krąży), czapla (stoi w płytkiej wodzie przy brzegu,
   co chwilę uderza dziobem w wodę, plusk).
2. Ptaki nie siadają na lądzie w wodzie ani na pomoście w złym miejscu; czapla nie stoi na trawie.
3. Kursor blisko ptaka płoszy go; nocą ptaków nie ma; o świcie z drzew zrywa się kilka ptaków (bez mew i czapli).

## 38. Odbicia w wodzie
Najpierw Wyspa → 2 (materiał `WaterReflection`).
1. Drzewa, lampy, budowle, pomost i łódź przy brzegu mają odwrócone odbicie na wodzie, falujące i słabnące w głąb;
   odbicie nie wchodzi na ląd ani na klif. Obiekty kilka kafelków od brzegu odbicia nie mają.
2. Ognik i chowaniec nad wodą albo przy brzegu odbijają się na bieżąco (z animacją).
3. Nocą odbicia są ciemne, ale okna, lampy i oczy świecą na wodzie.
4. Po rozbiórce obiektu albo dokupieniu kafelka przy brzegu odbicie znika albo przesuwa się w ciągu pół sekundy.
5. Pod całym brzegiem odbija się klif i pas trawy (zanika w głąb), piana przy brzegu nadal widoczna; po dokupieniu
   kafelka odbicie brzegu od razu pasuje do nowego kształtu. Rośliny z grządek nie mają odbić.
6. Lilie: kilkanaście szybkich klików obok odpycha je dalej, ale płynnie (bez skakania), potem wolno wracają.
7. Seria klików w ziemię, potem klik w wodę: tylko jeden zestaw kręgów w miejscu kliknięcia w wodę.
8. Czapla stoi na cienkich czerwonych nogach.

## 39. Kursor i zabawa kursorem
1. Kursor to pikselowa strzałka z żarzącym się czubkiem (ostry, bez rozmycia, przy każdej skali okna).
2. Nad grządką, drzewem, lampą itp. czubek płonie i miga iskrami; nad wodą jest turkusowy; podczas przeciągania widoku
   zmienia się w strzałki w 4 strony; nad Księgą i paskiem zwykły.
3. Szybki ruch nad wyspą zostawia kilka iskierek.
4. Przesuwany kursor delikatnie (ok. 1 px) odgina rośliny i korony drzew, które powoli wracają.
5. Klik w wodę: jeden krąg, który płynnie rośnie, cienieje i gaśnie (bez migania), lilie nad nim, bąbel, pobliskie ryby uciekają, lilie odpływają. Klik w drzewo/krzak: liście i ptaki odlatują.
   Klik w zapaloną lampę nocą: ćmy. Kliki dalej robią swoje (np. pomost łowi).
6. Kursor nad ptakiem albo rybą: dymek z nazwą gatunku; pierwszy raz toast „Nowe w bestiariuszu: …”.

## 40. Rzadkie noce i „Widziane”
F1: „Rzadka noc” uruchamia kolejne zjawisko (po kolei, każde do świtu albo do następnego kliknięcia).
1. Świecąca toń: delikatne turkusowe plamy i iskry przy brzegu, lekko jarząca się piana i ryby; woda nie jest zalana turkusem.
2. Deszcz gwiazd: co chwilę przez niebo przelatuje spadająca gwiazda (nad mgłą).
3. Zorza: falujące kurtyny u góry kadru, pikselowe, przy przesuwaniu widoku zostają u góry.
4. Korowód ogników: 5-7 bladych ogników sunie gęsiego po wodzie pod wyspą, wyłaniając się i znikając przy krawędziach.
5. Krwawy księżyc: noc wyraźnie czerwona; o świcie kolory wracają.
6. Każde zjawisko pokazuje toast i wpis w Bestiariuszu → „Widziane”; kolejne razy dodają „×N”.
7. Bez F1: kilka nocy pod rząd; mniej więcej co trzecia ma zjawisko. Wczytanie gry nocą nie losuje drugi raz.
8. Bestiariusz po niemiecku: zakładki działów mieszczą się (w razie potrzeby w dwóch rzędach), treść ich nie zasłania.

## 41. Woda: księżyc, deszcz, płycizny, wiatr
Pora dnia i pogoda z F1 (przewijanie czasu, wymuszenie pogody).
1. Noc, pogodnie: na wodzie pionowy pas migoczących srebrnych iskier; w ciągu nocy przesuwa się z prawej na lewą;
   w pełnię wyraźniejszy, w nów ledwo widać. W deszczu znika (płynnie), we mgle słabnie. Iskry nie są jaskrawe.
2. Deszcz: na wodzie co chwilę rozbryzgi i małe kółka; burza gęściej; po deszczu znikają w ciągu kilku sekund.
3. Dzień, pogodnie: na płyciźnie przy brzegu przesuwa się jasna siatka refleksów (słabnie w głąb, w deszczu prawie
   niewidoczna); na płyciźnie kołyszą się ciemne źdźbła wodorostów (przy przesuwaniu widoku płyną z dnem).
4. Podmuch wiatru (gdy drzewa się odginają): po wodzie przebiegają cienkie, ciemniejsze poziome smugi w stronę wiatru.
5. Żaden efekt nie wchodzi na ląd, klif ani pianę; w przezroczystym oknie widać je tylko w pasie wody przy brzegu.

## 42. Horyzont, cienie chmur, poranna mgiełka
Najpierw Wyspa → 2 (materiał `Atmosphere`). F1: „Horyzont” pokazuje kolejną sylwetkę od razu.
1. Wieloryb: daleko na wodzie, przy mgle; zmarszczka, ciemny grzbiet z płetwą, fontanna, zanurzenie, płetwa ogonowa.
   Kłęby mgły częściowo go zasłaniają. Nie wchodzi na ląd ani na wysepki.
2. Wąż morski: trzy garby wynurzają się po kolei, potem łeb z bladym okiem; powoli płynie w bok.
3. Statek-widmo: blady, półprzezroczysty, podarte żagle, powoli przepływa i kołysze się; nocą latarnia na rufie świeci.
4. Kursor nad sylwetką: dymek z nazwą, za pierwszym razem toast „Nowe w bestiariuszu”; w Bestiariuszu → Widziane są nowe pozycje.
5. Bez F1: sylwetka mniej więcej co 2,5-6 minut, zawsze w kadrze.
6. Dzień, pogodnie: po lądzie i wodzie powoli suną duże, miękkie cienie chmur (także po drzewach i budowlach);
   w deszczu i nocą ich nie ma; w przezroczystym oknie nie ma ich na pulpicie.
7. Świt (6:00-8:00): nad wodą blade, poziome smugi mgiełki, najgęstsze o 6:00, rzedną do 8:00; na trawie migoczą pojedyncze
   piksele rosy.

## 43. Ścinanie i rozbijanie seriami kliknięć
1. Najechanie na drzewo, kamień albo krzak: dymek z nazwą, kosztem w Żarze, nagrodą i „Kliknij szybko kilka razy”.
2. Jedno kliknięcie: obiekt drga, lecą odpryski, stuk, nad nim kropki (1 pełna); po ok. 2,5 s bez kliknięcia kropki znikają
   i obiekt zostaje.
3. Szybka seria: krzak znika po 3 kliknięciach, pień po 4, drzewo po 5, kamień po 6 (animacja usuwania, nagroda, Żar ubywa).
4. Za mało Żaru: kliknięcie pokazuje „Potrzeba X Żaru” i nic nie robi.
5. Drzewo owocowe z owocami: jedno kliknięcie zbiera owoce i nic więcej; dalsze szybkie kliknięcia (razem 6) ścinają je
   (drewno). Bez owoców: każde kliknięcie to uderzenie, po 6 szybkich drzewo pada; sadzonka po 3.
6. PPM na drzewie owocowym → w pierścieniu ikona owocu „Szczegóły” → panel ze stanem, „Zetnij” i „Przenieś”.
7. Zabytek (np. stary posąg): klik dalej otwiera panel z historią.
8. Ognik-drwal dalej sam rozbiera dzicz (bez zmian).

## 44. Drzewko ulepszeń: poziomy
1. Księga → Ulepszenia: węzły bliżej siebie i trochę większe; linie między nimi dalej widać.
2. Ulepszenie z 2-3 poziomami: na dolnej krawędzi węzła ciemna plakietka z kreskami; kupione jasnopomarańczowe, reszta szara.
3. Gdy stać cię na kolejny poziom, jego kreska miga; po zakupie zapala się na stałe.
4. Plakietka nie zasłania ikony ani nie wchodzi na węzeł poniżej.

## 45. Dym z pieca, para nad kotłem
1. Piec Żaru, gdy się pali (w palenisku drewno): z komina stale unosi się szary dym, kłęby rosną i rzedną, smuga lekko
   pochyla się w prawo. Zgaszony albo bez drewna: dymu nie ma (ostatnie kłęby dolatują i znikają).
2. Kocioł: w trakcie warzenia i gdy wywar czeka gotowy, nad kotłem unosi się jasna para; pusty kocioł bez pary.
3. Podmuch wiatru (rośliny się odginają): smuga dymu i pary wyraźnie odchyla się w prawo, potem wraca do pionu.
4. Deszcz i burza: dym od razu bardziej pochylony.
5. Przeniesienie pieca lub kotła: dym idzie z nowego miejsca; dym nie odbija się w wodzie i nie trzęsie się z obiektem.
