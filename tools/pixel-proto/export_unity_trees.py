"""Eksport drzew owocowych do Unity: fazy 0-3 (+ _glow dla świecących owoców), przewracanie dorosłego (4 klatki), ikony.
Uruchom po export_unity.py, export_unity_ui.py, export_unity_zones.py."""
import json
import export_unity_ui as E
import sprites5 as S5
import sprites4 as S4

for sid in S5.SPECIES:
    for st in range(4):
        E.save(S5.tree(sid, st), "Trees", f"tree_{sid}_{st}")
    adult = S5.tree(sid, 2)
    for i, a in enumerate((0, 12, 35, 70)):
        E.save(S4.rotated(adult, a, (adult.ax, adult.ay)), "Trees", f"tree_{sid}_fall_{i}", glow=False)
    E.save(S5.fruit_icon(sid), "UI/Icons", f"icon_fruit_{sid}", pivot=(0.5, 0.5), glow=False)
E.save(S5.sapling_icon(), "UI/Icons", "icon_sapling", pivot=(0.5, 0.5), glow=False)
with open(E.PIVOTS, "w") as f:
    json.dump({"items": sorted(E.items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(E.items)} wpisów w pivots.json")
