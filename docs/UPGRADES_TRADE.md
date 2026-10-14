# Ulepszenia handlu i dochodów (Etap 8)

60 ulepszeń dodanych na prośbę gracza („więcej pieniędzy ze sprzedaży”). Wartości i koszty są wstępne, do balansu na końcu.
Ceny liczy `Assets/_Project/Scripts/Economy/Market.cs`: cena = podstawa x jakość x (1 + suma premii) x kanał x blask pereł
x popyt dnia / dzień targowy / pełnia, potem targowanie (szansa na x2, Złoty interes x3/x4). Premie z gałęzi sumują się (procenty),
mnożniki chwili mnożą się. Dane: `Editor/WyspaSetup.cs` (UpgradeSpecs), pozycje: `Upgrades/UpgradeTree.cs`, efekty: `Upgrades/UpgradeEffects.cs`,
podgląd drzewka: `docs/img/drzewko_ulepszen.png`.

## Targ (dół-lewo)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Stragan | Wszystkie plony i owoce +N% ceny | 5% / 10% / 15% | 1 000 mon. / 3 000 mon. / 9 000 mon. | Uczciwy handel |
| Uczciwa waga | Wszystkie plony i owoce jeszcze +N% ceny | 5% / 10% | 4 000 mon. / 12 000 mon. | Stragan |
| Targowanie | N% szansy, że sprzedaż da podwójną zapłatę | 5% / 10% / 15% | 2 500 mon. / 7 500 mon. / 20 000 mon. | Stragan |
| Złoty interes | Udane targowanie płaci xN zamiast x2 | x3 / x4 | 10 000 mon. + 1 odł. / 30 000 mon. + 3 odł. | Targowanie |
| Hurt | Sprzedaż co najmniej 10 plonów naraz +N% ceny | 10% / 20% | 3 000 mon. + 20 drewna / 9 000 mon. + 40 drewna | Stragan |
| Gorliwy handlarz | Duszek-handlarz sprzedaje już przy połowie skrzyni | 0.5 | 5 000 mon. | Hurt |
| Renoma | +1% ceny za każde 200 sprzedanych plonów w tym rejsie (do +N%) | 10% / 20% / 30% | 6 000 mon. / 18 000 mon. / 50 000 mon. | Uczciwa waga |
| Popyt dnia | Co dobę jedna roślina sprzedaje się xN | x1.5 / x2 / x2.5 | 4 000 mon. / 12 000 mon. / 36 000 mon. | Targowanie |
| Dzień targowy | Co czwartą dobę wszystkie ceny +N% | +25% / +50% | 15 000 mon. / 45 000 mon. | Popyt dnia |
| Zbiór seryjny | Każda sprzedaż do 15 s po poprzedniej +2% ceny (do +N%) | 10% / 20% / 30% | 5 000 mon. / 15 000 mon. / 40 000 mon. | Złoty interes |
| Odsetki | O świcie +N% monet z sakiewki (do 1000 za każdy %) | 1% / 2% / 3% | 20 000 mon. / 60 000 mon. / 150 000 mon. | Renoma |
| Targ skrzyń | Sprzedaż skrzyni ręcznie +N% ceny | 10% / 20% | 6 000 mon. / 18 000 mon. | Gorliwy handlarz |

## Zamówienia

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Sakiewka | Monety za zamówienia +N% | 15% / 30% / 50% | 3 000 mon. / 9 000 mon. / 27 000 mon. | Dzień targowy |
| Pełna tablica | +N miejsca na zamówienia | 1 / 2 | 5 000 mon. + 30 drewna / 15 000 mon. + 60 drewna | Sakiewka |
| Częste zlecenia | Nowe zamówienie co dobę zamiast co dwie | 1 | 8 000 mon. | Sakiewka |
| Szlachetni klienci | N% szansy, że zamówienie płaci podwójnie | 10% / 20% | 10 000 mon. / 30 000 mon. | Pełna tablica |
| Zapłata w nasionach | N% szansy na nasiono hybrydy za zamówienie | 15% / 30% | 8 000 mon. + 10 pl. nocy / 24 000 mon. + 25 pl. nocy | Częste zlecenia |

## Kupiec-widmo i morze (dół-prawo)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Targ z widmem | Kupiec-widmo płaci za plony +N% więcej | 20% / 40% | 2 500 mon. + 5 pl. nocy / 8 000 mon. + 15 pl. nocy | Szybka łódź |
| Cierpliwy kupiec | Kupiec-widmo czeka +N% dłużej | +50% / +100% | 2 000 mon. / 6 000 mon. | Szybka łódź |
| Widmowy napiwek | Każda wizyta kupca-widma: +N monet | 100 / 300 / 800 | 3 000 mon. / 9 000 mon. / 27 000 mon. | Cierpliwy kupiec |
| Widmowe rabaty | Towary kupca-widma -N% monet | 15% / 30% | 4 000 mon. / 12 000 mon. | Targ z widmem |
| Bogaty ładunek | Łupy z wypraw +N% | +25% / +50% | 5 000 mon. + 30 drewna / 15 000 mon. + 60 drewna | Targ z widmem |
| Morski handel | Każda wyprawa przywozi też N monet | 150 / 400 | 6 000 mon. / 18 000 mon. | Bogaty ładunek |

## Skład i ogniki (góra)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Handel drewnem | Skład płaci +N monety za drewno | 2 / 4 | 1 500 mon. + 20 drewna / 4 500 mon. + 50 drewna | Pojemne skrzynie |
| Kamieniarz | Skład płaci +N monety za kamień | 2 / 4 | 1 500 mon. + 20 kamienia / 4 500 mon. + 50 kamienia | Pojemne skrzynie |
| Wielki skład | Skład sprzedaje xN więcej naraz | x2 / x3 | 4 000 mon. + 30 drewna + 30 kamienia / 12 000 mon. + 60 drewna + 60 kamienia | Pojemne skrzynie |
| Ognik-kupiec | Ognik bez miejsca w skrzyni sprzedaje plon za N% ceny | 100% / 110% | 5 000 mon. / 15 000 mon. | Wielki skład |
| Napiwki ogników | Każdy zbiór ognika: +N monety | 1 / 2 / 3 | 3 000 mon. / 9 000 mon. / 27 000 mon. | Ognik-kupiec |

## Plony dnia (góra-prawo)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Słoneczny zbiór | Rośliny dnia +N% ceny | 5% / 10% / 15% | 1 500 mon. / 4 500 mon. / 13 500 mon. | Żyzna gleba |
| Prosto z grządki | Zbiór ręczny (klik w roślinę) +N% ceny | 10% / 20% / 30% | 2 000 mon. / 6 000 mon. / 18 000 mon. | Słoneczny zbiór |
| Pracowite ręce | Gdy pracujesz przy komputerze, sprzedaż +N% | 10% / 20% | 3 000 mon. / 9 000 mon. | Słoneczny zbiór |
| Korzenny targ | Marchew, czosnek i wilcza marchew +N% ceny | 25% / 50% | 2 500 mon. / 7 500 mon. | Prosto z grządki |
| Cech młynarzy | Zboże i popielne zboże +N% ceny | 25% / 50% | 3 000 mon. / 9 000 mon. | Korzenny targ |
| Dyniowe pole | Wszystkie dynie +N% ceny | 25% / 50% | 4 000 mon. / 12 000 mon. | Prosto z grządki |
| Poranny targ | Przez 10 minut po świcie sprzedaż +N% | 25% / 50% | 4 000 mon. / 12 000 mon. | Dyniowe pole |
| Zielarz | Mak, piołun i ich hybrydy +N% ceny | 25% / 50% | 5 000 mon. / 15 000 mon. | Poranny targ |

## Plony nocy (prawo)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Księżycowy targ | Rośliny nocy +N% ceny | 5% / 10% / 15% | 2 000 mon. + 5 pl. nocy / 6 000 mon. + 10 pl. nocy / 18 000 mon. + 20 pl. nocy | Nocna rosa |
| Sowi kupiec | Sprzedaż w nocy +N% | 10% / 20% | 4 000 mon. + 10 pl. nocy / 12 000 mon. + 20 pl. nocy | Księżycowy targ |
| Latarnie kupieckie | +2% ceny za każdą zapaloną lampę (do +N%) | 10% / 20% | 6 000 mon. / 18 000 mon. | Sowi kupiec |
| Cmentarne dobra | Trupi grzyb, bluszcz, mandragora, wrzeszczący czosnek +N% | 25% / 50% | 4 000 mon. + 10 pl. nocy / 12 000 mon. + 20 pl. nocy | Księżycowy targ |
| Kwiaciarka | Księżycowy kwiat, krwawa róża, wilcza jagoda +N% | 25% / 50% | 4 000 mon. + 10 pl. nocy / 12 000 mon. + 20 pl. nocy | Sowi kupiec |
| Obfite noce | +N plonu nocy z każdej rośliny nocy | 1 / 2 | 5 000 mon. + 15 pl. nocy / 15 000 mon. + 30 pl. nocy | Cmentarne dobra |
| Jarmark pełni | W pełnię plony sprzedają się xN | x1.5 / x2 | 8 000 mon. + 1 odł. / 24 000 mon. + 3 odł. | Kwiaciarka |

## Jakość i hybrydy (prawo-góra)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Gwiazdy jakości | Premia za gwiazdki jakości +N% | 25% / 50% | 5 000 mon. + 10 pl. nocy / 15 000 mon. + 25 pl. nocy | Krzyżowanie |
| Sława hybryd | Hybrydy +N% ceny | 25% / 50% / 75% | 4 000 mon. + 10 pl. nocy / 12 000 mon. + 20 pl. nocy / 36 000 mon. + 40 pl. nocy | Krzyżowanie |
| Staranne ręce | N% szansy na +1 gwiazdkę jakości przy sprzedaży | 10% / 20% | 6 000 mon. / 18 000 mon. | Gwiazdy jakości |
| Zbieracz nasion | N% szansy na darmowe nasiono z każdego zbioru | 5% / 10% | 5 000 mon. / 15 000 mon. | Sława hybryd |
| Kolekcjoner | +2% ceny za każdy zebrany gatunek z bestiariusza (do +N%) | 20% / 40% | 8 000 mon. / 24 000 mon. | Staranne ręce |
| Pamiątkowe odmiany | Nasiona tańsze o N% | 10% / 20% / 30% | 2 000 mon. / 6 000 mon. / 18 000 mon. | Gwiazdy jakości |
| Szczęśliwa moneta | N% szansy na odłamek gwiazdy przy sprzedaży | 1% / 2% | 10 000 mon. + 2 odł. / 30 000 mon. + 5 odł. | Zbieracz nasion |

## Sad (prawo-dół)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Owocowy stragan | Owoce +N% ceny | 20% / 40% / 60% | 2 000 mon. / 6 000 mon. / 18 000 mon. | Dzikie runy |
| Dojrzałe owoce | Drzewa owocują N% szybciej | +15% / +30% | 3 000 mon. / 9 000 mon. | Owocowy stragan |
| Ciężkie gałęzie | N% szansy na podwójną zapłatę za owoce | 10% / 20% | 5 000 mon. / 15 000 mon. | Dojrzałe owoce |
| Szczepienie | Sadzonki drzew tańsze o N% monet | 20% / 40% | 3 000 mon. / 9 000 mon. | Ciężkie gałęzie |
| Owoce pełni | W pełnię owoce sprzedają się xN | x1.5 / x2 | 6 000 mon. + 1 odł. / 18 000 mon. + 2 odł. | Dojrzałe owoce |

## Żar i praca (lewo)

| Ulepszenie | Efekt (PL) | Poziomy | Koszt | Wymaga |
|---|---|---|---|---|
| Żar w złoto | Żar ponad pełny słój zamienia się w N monety za sztukę | 2 / 4 / 6 | 3 000 mon. / 9 000 mon. / 27 000 mon. | Słój Żaru |
| Dniówka | Każda minuta pracy przy komputerze: +N monety | 1 / 2 / 3 | 4 000 mon. / 12 000 mon. / 36 000 mon. | Żar w złoto |
| Dzierżawa | O świcie N monet za każdy kupiony kafelek | 5 / 10 | 5 000 mon. / 15 000 mon. | Drugi oddech |
| Skupiony sprzedawca | W serii skupienia sprzedaż +N% | 10% / 20% | 4 000 mon. / 12 000 mon. | Głębokie skupienie |
| Wypoczęty kupiec | Po przerwie (bonus wypoczęty) sprzedaż +N% | 15% / 30% | 4 000 mon. / 12 000 mon. | Drugi oddech |
