"""Narzędzia ogników według zawodu (GDD §11.3 E1): mapy w kluczach ui_kit.KEY, obrys dokłada from_map.

Każde narzędzie ma 2 klatki tej samej wielkości; punkt chwytu (pivot) to GRIP: piksel mapy (x, y od góry, bez marginesu),
który ognik trzyma z boku tułowia. Koszyk zbieracza ma dwie warstwy (tył z wnętrzem i przód), między nimi leżą zbierane rzeczy.
Klucze: o kontur, h j drewno, e ciemna ziemia, s z stal, w kość, g złoto, F X płomień, W L ognik, U M fiolet, n q zieleń.
"""

TOOLS = {
    # drwal: siekiero-kilof uniesiony / uderzenie (głowica w dół, przed ognikiem)
    'axepick': {
        'grip': (4, 6),
        'frames': [
            [".....zz.", "sss.zzzs", "..ssjzs.", "....j...", "....j...", "....h...", "....h..."],
            ["........", "........", "......s.", "hhjjjjjz", "......zz", ".....zzs", "........"],
        ],
    },
    # ogrodnik: sierp (trzymany / zamach)
    'sickle': {
        'grip': (2, 6),
        'frames': [
            ["..zzz..", ".z...z.", ".....z.", "....z..", "...h...", "..h....", "..h...."],
            [".......", ".......", ".......", "hhzz...", "...zz..", "....z..", "...z..."],
        ],
    },
    # siewca: worek z nasionami (trzymany za wiązanie / przechylony, nasiona się sypią)
    'seedbag': {
        'grip': (2, 0),
        'frames': [
            [".hh...", "..h...", ".jjj..", "jjnjj.", "jjjjj.", ".jjj..", "......"],
            [".hh...", "..jj..", ".jjjj.", "jjnjjj", ".jjjj.", "....g.", ".....g"],
        ],
    },
    # zbieracz: koszyk (tył z ciemnym wnętrzem, przód z wikliną)
    'basket': {
        'grip': (4, 0),
        'frames': [
            ["..hhhh..", ".h....h.", "h......h", "eeeeeeee", "eeeeeeee", "........", "........"],
            ["........", "........", "........", "jhjhjhjh", "hjhjhjhj", ".jhjhjh.", "..hhhh.."],
        ],
    },
    # alchemik: fiolka z bulgoczącym wywarem
    'flask': {
        'grip': (1, 1),
        'frames': [
            [".hh.", ".ww.", ".ww.", "wMMw", "MUMM", "wUUw", ".ww."],
            [".hh.", ".wM.", ".ww.", "wMUw", "MMUM", "wUMw", ".ww."],
        ],
    },
    # latarnik: latarenka z płomykiem (migocze)
    'lantern': {
        'grip': (2, 0),
        'frames': [
            ["..h..", ".hhh.", "hFXFh", "hXFXh", "hFXFh", ".hhh."],
            ["..h..", ".hhh.", "hXFXh", "hFFFh", "hXFXh", ".hhh."],
        ],
    },
}

# zawód ognika → narzędzie
KIND_TOOL = {
    'gardener': 'sickle', 'sower': 'seedbag', 'woodcutter': 'axepick',
    'alchemist': 'flask', 'lamplighter': 'lantern', 'gatherer': 'basket',
}
