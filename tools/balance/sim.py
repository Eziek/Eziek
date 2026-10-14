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
CROPS = {
    'carrot': (200, 5, 12, False, 0), 'wheat': (300, 8, 18, False, 0), 'pumpkin': (500, 15, 40, False, 0),
    'garlic': (240, 6, 15, False, 300), 'poppy': (400, 12, 30, False, 780), 'wormwood': (800, 25, 70, False, 1800),
    'corpseshroom': (1200, 15, 45, True, 0), 'moonflower': (1500, 20, 60, True, 0),
    'ghostpumpkin': (2400, 30, 90, True, 0), 'belladonna': (900, 12, 35, True, 960),
    'mandrake': (2100, 35, 120, True, 2400), 'graveivy': (4800, 60, 260, True, 4800),
}
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
    st = dict(coins=g['START_COINS'], night=0, ess=0.0, cap=ESS_START_CAP, tiles=4, soil=2, objects=2,
              jar=0, wisp=False, wisps=0, merchant=False, lamps=1)
    plots = [None, None]                     # [crop, wzrost s] albo None
    unlocked = {k for k, v in crops.items() if v[4] == 0}
    events = {}
    clock = dict(active=0.0)

    def ev(name):
        if name not in events:
            events[name] = clock['active'] / 60.0

    def profit(cid):
        s, seed, sell, nightc, _ = crops[cid]
        return (sell - seed) / s

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
        st['coins'] += round(crops[p[0]][2] * mult); st['night'] += 1 if crops[p[0]][3] else 0; plots[i] = None

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

    minute = 0
    for day in range(DAYS):
        for m in range(24 * 60):
            active = m < WORK_H * 60
            is_night = (minute % CYCLE) >= DAY_MIN
            minute += 1
            day_lit = 1.0 if not is_night else min(1.0, st['lamps'] * LAMP_PLOTS / max(1.0, len(plots) * (1 - NIGHT_SHARE)))
            if active:
                clock['active'] += 1
                st['ess'] = min(st['cap'], st['ess'] + 1)
                if is_night:
                    st['ess'] = max(0.0, st['ess'] - LAMP_BURN * st['lamps'])
            for p in plots:
                if not p: continue
                if crops[p[0]][3]:
                    if is_night: p[1] += 60
                elif active:
                    p[1] += 60 * TEMPO * day_lit
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
            if m % CHECK_MIN == 0:
                harvest(MANUAL_CLICKS); sow(); shop()
        harvest(MANUAL_CLICKS); sow(); shop()   # rano: to, co urosło w nocy offline
    if verbose:
        for k, v in sorted(events.items(), key=lambda kv: kv[1]):
            print(f"  {k:22s} {v:7.1f} h pracy  (dzień {int(v // WORK_H) + 1})")
        print(f"  po {DAYS} dniach: kafelki {st['tiles'] - 4}, grządki {st['soil']}, lampy {st['lamps']}, "
              f"ogniki {st['wisps']}, monety {st['coins']}, plony nocy {st['night']}")
    return events


if __name__ == "__main__":
    print("Liczby z gry (cykl 1 h, wzrost x3):")
    run()
