"""Symulacja progresji Pockland (Etap 6d, balans): ile godzin aktywnej pracy do kolejnych kamieni milowych.

Uproszczony gracz: pracuje WORK_H godzin dziennie (dzień, rośliny dnia rosną z tempem pracy), potem noc offline
(rośliny nocy rosną NIGHT_H godzin w ciemności). Co CHECK_MIN minut zagląda na wyspę: zbiera, sieje, kupuje.
Kolejność zakupów: lampa ze sklepu (wprowadzenie), nowe grządki, kafelki, ulepszenia i odblokowania, gdy stać.
Liczby są kopią danych gry (WyspaSetup.CropSpecs, IslandEconomy, AutomationSystem.Cost, ulepszenia) - zmieniaj je tu
i w grze razem. Uruchom: python3 tools/balance/sim.py
"""
import math

WORK_H, NIGHT_H, CHECK_MIN, TEMPO = 8, 8, 20, 1.2
MANUAL_CLICKS = 10                                    # ile grządek gracz zbiera ręcznie przy jednym zajrzeniu
DAYS = 40

# id: (wzrost s, nasiono, sprzedaż, noc?, cena odblokowania)
CROPS = {
    'carrot': (600, 5, 12, False, 0), 'wheat': (900, 8, 18, False, 0), 'pumpkin': (1500, 15, 40, False, 0),
    'garlic': (720, 6, 15, False, 100), 'poppy': (1200, 12, 30, False, 260), 'wormwood': (2400, 25, 70, False, 600),
    'corpseshroom': (3 * 3600, 15, 45, True, 0), 'moonflower': (4 * 3600, 20, 60, True, 0),
    'ghostpumpkin': (6 * 3600, 30, 90, True, 0), 'belladonna': (2 * 3600, 12, 35, True, 320),
    'mandrake': (5 * 3600, 35, 120, True, 800), 'graveivy': (10 * 3600, 60, 260, True, 1600),
}
START_COINS = 50
TILE_COINS = lambda n: round(10 + 8 * n + 0.5 * n * n)
TILE_ESS = lambda n: 3 + n // 4
DIG = lambda soil: 10 + 5 * max(0, soil - 2)
JAR = [(120, 100), (240, 400), (480, 1500)]          # pojemność, monety
ESS_START_CAP = 60
WISP_UPGRADE = (100, 0)                               # monety, plony nocy
WISP_EXTRA = lambda owned: (round(250 * 1.6 ** owned / 5) * 5, 5)
MERCHANT_UPGRADE = 800
WISP_INTERVAL_MIN = 0.5                               # kurs co 30 s
WISP_TILES = 25                                       # zasięg 5x5
NIGHT_SHARE = 0.3                                     # część grządek na rośliny nocy (od pierwszej nocy)
LAMP = 30


def run(cfg=None, verbose=True):
    g = dict(CROPS=CROPS, TILE_COINS=TILE_COINS, TILE_ESS=TILE_ESS, DIG=DIG, JAR=JAR, WISP_UPGRADE=WISP_UPGRADE,
             WISP_EXTRA=WISP_EXTRA, MERCHANT_UPGRADE=MERCHANT_UPGRADE, START_COINS=START_COINS, LAMP=LAMP)
    if cfg: g.update(cfg)
    crops = g['CROPS']
    coins, night, ess, cap = g['START_COINS'], 0, 0.0, ESS_START_CAP
    tiles, soil, objects = 4, 2, 2            # kafelki, grządki, obiekty (tablica, latarnia)
    plots = [None, None]                     # [crop, wzrost s] albo None
    unlocked = {k for k, v in crops.items() if v[4] == 0}
    jar = 0; wisp = False; wisps = 0; merchant = False; lamps = 1
    t_active = 0.0
    events = {}

    def ev(name):
        if name not in events:
            events[name] = t_active / 60.0

    def profit(cid):
        s, seed, sell, nightc, _ = crops[cid]
        return (sell - seed) / s
    def best(nightc):
        opts = [c for c in unlocked if crops[c][3] == nightc and crops[c][1] <= coins]
        return max(opts, key=profit) if opts else None

    def sow():
        nonlocal coins
        n_night = sum(1 for p in plots if p and crops[p[0]][3])
        for i, p in enumerate(plots):
            if p is None:
                want_night = n_night < NIGHT_SHARE * len(plots) and events.get('night1') is not None
                c = best(want_night) or best(not want_night)
                if c is None: return
                coins -= crops[c][1]; plots[i] = [c, 0.0]
                if crops[c][3]: n_night += 1

    def harvest(limit=None):
        nonlocal coins, night
        n = 0
        for i, p in enumerate(plots):
            if p and p[1] >= crops[p[0]][0] and (limit is None or n < limit):
                coins += crops[p[0]][2]; night += 1 if crops[p[0]][3] else 0; plots[i] = None; n += 1

    def shop():
        nonlocal coins, night, ess, cap, tiles, soil, jar, wisp, wisps, merchant, lamps, objects
        for _ in range(50):
            if lamps < 2 and coins >= g['LAMP']:
                coins -= g['LAMP']; lamps += 1; objects += 1; ev('lampa'); continue
            if not wisp and coins >= g['WISP_UPGRADE'][0] and night >= g['WISP_UPGRADE'][1]:
                coins -= g['WISP_UPGRADE'][0]; night -= g['WISP_UPGRADE'][1]; wisp = True; wisps = 1; objects += 2; ev('ognik'); continue
            locked = sorted([c for c in crops if c not in unlocked], key=lambda c: crops[c][4])
            if locked and coins >= crops[locked[0]][4] * 1.3:
                coins -= crops[locked[0]][4]; unlocked.add(locked[0])
                if len(unlocked) == len(crops): ev('wszystkie nasiona')
                continue
            if wisp and not merchant and coins >= g['MERCHANT_UPGRADE'] * 1.2:
                coins -= g['MERCHANT_UPGRADE']; merchant = True; ev('duszek-handlarz'); continue
            if jar < len(g['JAR']) and ess >= cap * 0.95 and coins >= g['JAR'][jar][1]:
                coins -= g['JAR'][jar][1]; cap = g['JAR'][jar][0]; jar += 1; ev(f'słój {jar}'); continue
            if wisp and wisps * WISP_TILES < soil * 0.8:
                c, n = g['WISP_EXTRA'](wisps)
                if coins >= c * 1.5 and night >= n:
                    coins -= c; night -= n; wisps += 1; objects += 1; continue
            free = tiles - soil - objects
            if free > 0 and coins >= g['DIG'](soil):
                coins -= g['DIG'](soil); soil += 1; plots.append(None); continue
            n = tiles - 4
            if coins >= g['TILE_COINS'](n) and ess >= g['TILE_ESS'](n):
                coins -= g['TILE_COINS'](n); ess -= g['TILE_ESS'](n); tiles += 1
                ev('1. kafelek')
                for m in (25, 50, 100, 150):
                    if tiles - 4 >= m: ev(f'{m} kafelków')
                continue
            break

    for day in range(DAYS):
        for minute in range(WORK_H * 60):
            t_active += 1
            ess = min(cap, ess + 1)
            for p in plots:
                if p and not crops[p[0]][3]:
                    p[1] += 60 * TEMPO
            if wisp:
                # ogniki: zbierają dojrzałe w swoim zasięgu na bieżąco, runa siewu pominięta (gracz sieje przy zaglądaniu)
                covered = min(len(plots), wisps * WISP_TILES)
                for i in range(covered):
                    p = plots[i]
                    if p and p[1] >= crops[p[0]][0]:
                        coins += round(crops[p[0]][2] * (0.9 if merchant else 1.0)); night += 1 if crops[p[0]][3] else 0; plots[i] = None
                if merchant:
                    sow()
            if minute % CHECK_MIN == 0:
                harvest(MANUAL_CLICKS); sow(); shop()
            if day == 0 and minute == 0:
                ev('start')
        # noc offline: rośliny nocy rosną, lampy palą Esencję (latarnia 0.1/min)
        ess = max(0.0, ess - 0.1 * 60 * min(lamps, 2) * NIGHT_H * 0.5)
        for p in plots:
            if p and crops[p[0]][3]:
                p[1] += NIGHT_H * 3600
        ev('night1')
        harvest(); sow(); shop()
    if verbose:
        for k, v in sorted(events.items(), key=lambda kv: kv[1]):
            if k in ('start', 'night1'): continue
            print(f"  {k:22s} {v:7.1f} h pracy  (dzień {int(v // WORK_H) + 1})")
        print(f"  po {DAYS} dniach: kafelki {tiles - 4}, grządki {soil}, ogniki {wisps}, monety {coins}, plony nocy {night}")
    return events


if __name__ == "__main__":
    print("Liczby z gry (Etap 6d, tempo szybkie):")
    run()
