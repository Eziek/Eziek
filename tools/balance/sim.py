"""Symulacja progresji Pockland (balans): ile godzin aktywnej pracy do kolejnych kamieni milowych.

Cykl gry: doba gry trwa 1 h - 40 min dnia, 20 min nocy - i idzie także offline.
Gracz pracuje WORK_H godzin dziennie (aktywny), resztę doby gra jest wyłączona.
- Rośliny dnia rosną, gdy gracz jest aktywny i grządka jest oświetlona (dzień albo lampa w nocy), z tempem pracy.
- Rośliny nocy rosną w nocy cyklu w ciemności (także offline). Gracz trzyma je na grządkach poza zasięgiem lamp.
- Lampy palą Esencję w nocy, tylko gdy gra działa.
Co CHECK_MIN minut gracz zagląda na wyspę: zbiera do MANUAL_CLICKS grządek, sieje, kupuje (lampa, ognik, nasiona, słój,
grządki, kafelki). Ogniki (zasięg 25 grządek) zbierają na bieżąco, duszki-handlarze sprzedają, runy sieją.
Liczby są kopią danych gry (WyspaSetup.CropSpecs, IslandEconomy, AutomationSystem.Cost, ulepszenia) - zmieniaj je tu
i w grze razem. Uruchom: python3 tools/balance/sim.py
"""

WORK_H, CHECK_MIN, TEMPO, MANUAL_CLICKS = 8, 20, 1.2, 10
DAYS = 30
CYCLE, DAY_MIN = 60, 40                               # minuty: doba gry i jej dzień

# id: (wzrost s, nasiono, sprzedaż, noc?, cena odblokowania); rośliny nocy: sekundy nocy (noc = 1200 s)
# balans §9 p. 98: dzień 20 s - 3 min, noc 1,5-10 min (wcześniej 200-800 s i 900-4800 s)
CROPS = {
    'carrot': (20, 1, 3, False, 0), 'wheat': (45, 2, 6, False, 0), 'pumpkin': (120, 5, 18, False, 0),
    'garlic': (30, 2, 5, False, 300), 'poppy': (75, 4, 12, False, 780), 'wormwood': (180, 8, 30, False, 1800),
    'corpseshroom': (120, 4, 14, True, 0), 'moonflower': (180, 5, 19, True, 0),
    'ghostpumpkin': (360, 8, 34, True, 0), 'belladonna': (90, 3, 10, True, 960),
    'mandrake': (300, 9, 40, True, 2400), 'graveivy': (600, 15, 80, True, 4800),
}
STEP = 10                                             # krok symulacji w sekundach (marchew rośnie 20 s)
START_COINS = 50
TILE_COINS = lambda n: max(10, round((10 + 20 * n + 1.5 * n * n) * (0.4 + 0.6 * n / 20 if n < 20 else 1)))
TILE_ESS = lambda n: 3 + n // 3
DIG = lambda soil: 10 + 5 * max(0, soil - 2)
JAR = [(120, 200), (240, 800), (480, 3000)]          # pojemność, monety
ESS_START_CAP = 60
WISP_UPGRADE = (100, 0)                               # monety, plony nocy
WISP_EXTRA = lambda owned: (round(250 * 1.6 ** owned / 5) * 5, 5)
MERCHANT_UPGRADE = 1600
WISP_TILES = 25                                       # zasięg 5x5
LAMP_PLOTS = 9                                        # ile grządek oświetla jedna latarnia
NIGHT_SHARE = 0.3                                     # część grządek na rośliny nocy
LAMP, LAMP_BURN = 30, 0.1                             # latarnia: cena, Esencja na minutę nocy


def run(cfg=None, verbose=True):
    g = dict(CROPS=CROPS, TILE_COINS=TILE_COINS, TILE_ESS=TILE_ESS, DIG=DIG, JAR=JAR, WISP_UPGRADE=WISP_UPGRADE,
             WISP_EXTRA=WISP_EXTRA, MERCHANT_UPGRADE=MERCHANT_UPGRADE, START_COINS=START_COINS, LAMP=LAMP)
    if cfg: g.update(cfg)
    crops = g['CROPS']
    st = dict(coins=g['START_COINS'], earned=0, night=0, ess=0.0, cap=ESS_START_CAP, tiles=4, soil=2, objects=2,
              jar=0, wisp=False, wisps=0, merchant=False, lamps=1)
    plots = [None, None]                     # [crop, wzrost s] albo None
    unlocked = {k for k, v in crops.items() if v[4] == 0}
    events = {}
    clock = dict(active=0.0)

    def ev(name):
        if name not in events:
            events[name] = clock['active'] / 60.0

    def profit(cid):
        # bez ognika gracz zbiera tylko, gdy zagląda: krótka roślina stoi dojrzała do następnego zajrzenia
        s, seed, sell, nightc, _ = crops[cid]
        return (sell - seed) / (s if st['wisp'] else max(s, CHECK_MIN * 60))

    def best(nightc):
        opts = [c for c in unlocked if crops[c][3] == nightc and crops[c][1] <= st['coins']]
        return max(opts, key=profit) if opts else None

    def sow():
        n_night = sum(1 for p in plots if p and crops[p[0]][3])
        for i, p in enumerate(plots):
            if p is None:
                want_night = n_night < NIGHT_SHARE * len(plots)
                c = best(want_night) or best(not want_night)
                if c is None: return
                st['coins'] -= crops[c][1]; plots[i] = [c, 0.0]
                if crops[c][3]: n_night += 1

    def take(i, mult=1.0):
        p = plots[i]
        coins = round(crops[p[0]][2] * mult)
        st['coins'] += coins; st['earned'] += coins
        st['night'] += crops[p[0]][0] / 600 if crops[p[0]][3] else 0   # Market.NightYield: 1 na 10 min wzrostu
        plots[i] = None

    def harvest(limit):
        n = 0
        for i, p in enumerate(plots):
            if p and p[1] >= crops[p[0]][0] and n < limit:
                take(i); n += 1

    def shop():
        for _ in range(60):
            c = st['coins']
            if st['lamps'] < 2 and c >= g['LAMP']:
                st['coins'] -= g['LAMP']; st['lamps'] += 1; st['objects'] += 1; ev('lampa'); continue
            if not st['wisp'] and c >= g['WISP_UPGRADE'][0] and st['night'] >= g['WISP_UPGRADE'][1]:
                st['coins'] -= g['WISP_UPGRADE'][0]; st['night'] -= g['WISP_UPGRADE'][1]
                st['wisp'] = True; st['wisps'] = 1; st['objects'] += 2; ev('ognik'); continue
            locked = sorted([k for k in crops if k not in unlocked], key=lambda k: crops[k][4])
            if locked and c >= crops[locked[0]][4] * 1.3:
                st['coins'] -= crops[locked[0]][4]; unlocked.add(locked[0])
                if len(unlocked) == len(crops): ev('wszystkie nasiona')
                continue
            if st['wisp'] and not st['merchant'] and c >= g['MERCHANT_UPGRADE'] * 1.2:
                st['coins'] -= g['MERCHANT_UPGRADE']; st['merchant'] = True; ev('duszek-handlarz'); continue
            if st['jar'] < len(g['JAR']) and st['ess'] >= st['cap'] * 0.95 and c >= g['JAR'][st['jar']][1]:
                st['coins'] -= g['JAR'][st['jar']][1]; st['cap'] = g['JAR'][st['jar']][0]; st['jar'] += 1
                ev(f"słój {st['jar']}"); continue
            day_plots = st['soil'] * (1 - NIGHT_SHARE)
            if st['lamps'] * LAMP_PLOTS < day_plots * 0.8 and c >= g['LAMP'] * 3:
                st['coins'] -= g['LAMP']; st['lamps'] += 1; st['objects'] += 1; continue
            if st['wisp'] and st['wisps'] * WISP_TILES < st['soil'] * 0.8:
                wc, wn = g['WISP_EXTRA'](st['wisps'])
                if c >= wc * 1.5 and st['night'] >= wn:
                    st['coins'] -= wc; st['night'] -= wn; st['wisps'] += 1; st['objects'] += 1; continue
            free = st['tiles'] - st['soil'] - st['objects']
            if free > 0 and c >= g['DIG'](st['soil']):
                st['coins'] -= g['DIG'](st['soil']); st['soil'] += 1; plots.append(None); continue
            n = st['tiles'] - 4
            if c >= g['TILE_COINS'](n) and st['ess'] >= g['TILE_ESS'](n):
                st['coins'] -= g['TILE_COINS'](n); st['ess'] -= g['TILE_ESS'](n); st['tiles'] += 1
                ev('1. kafelek')
                for m in (25, 50, 100, 150):
                    if st['tiles'] - 4 >= m: ev(f'{m} kafelków')
                continue
            break

    per_min = 60 // STEP
    tick = 0
    for day in range(DAYS):
        for t in range(24 * 60 * per_min):
            m, sub = divmod(t, per_min)
            active = m < WORK_H * 60
            is_night = ((tick // per_min) % CYCLE) >= DAY_MIN
            tick += 1
            day_lit = 1.0 if not is_night else min(1.0, st['lamps'] * LAMP_PLOTS / max(1.0, len(plots) * (1 - NIGHT_SHARE)))
            if active:
                clock['active'] += 1 / per_min
                st['ess'] = min(st['cap'], st['ess'] + 1 / per_min)
                if is_night:
                    st['ess'] = max(0.0, st['ess'] - LAMP_BURN * st['lamps'] / per_min)
            for p in plots:
                if not p: continue
                if crops[p[0]][3]:
                    if is_night: p[1] += STEP
                elif active:
                    p[1] += STEP * TEMPO * day_lit
            if not active:
                continue
            if st['wisp']:
                covered = min(len(plots), st['wisps'] * WISP_TILES)
                for i in range(covered):
                    p = plots[i]
                    if p and p[1] >= crops[p[0]][0]:
                        take(i, 0.9 if st['merchant'] else 1.0)
                if st['merchant']:
                    sow()
            if sub == 0 and m % CHECK_MIN == 0:
                harvest(MANUAL_CLICKS); sow(); shop()
        harvest(MANUAL_CLICKS); sow(); shop()   # rano: to, co urosło w nocy offline
    if verbose:
        for k, v in sorted(events.items(), key=lambda kv: kv[1]):
            print(f"  {k:22s} {v:7.1f} h pracy  (dzień {int(v // WORK_H) + 1})")
        print(f"  po {DAYS} dniach: kafelki {st['tiles'] - 4}, grządki {st['soil']}, lampy {st['lamps']}, "
              f"ogniki {st['wisps']}, monety {st['coins']} (zarobione {st['earned']}), plony nocy {round(st['night'])}")
    return events


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1:
        CHECK_MIN = int(sys.argv[1])          # np. 5: gracz zagląda częściej (szybkie rośliny)
    print(f"Liczby z gry (cykl 1 h, rośliny 20 s - 10 min, zaglądanie co {CHECK_MIN} min):")
    run()
