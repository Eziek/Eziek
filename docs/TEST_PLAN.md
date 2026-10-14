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
