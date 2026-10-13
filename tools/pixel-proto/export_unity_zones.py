"""Eksport do Unity: podłoża i dekoracje stref, usuwanie dekoracji (klatki, pnie, pozostałości, cząsteczki), ikony materiałów.
Uruchom po export_unity.py i export_unity_ui.py (dopisuje do pivots.json)."""
import export_unity_ui as E           # uruchamia też eksport UI (idempotentny) i daje save()/items
import json
import sprites4 as S4

for zone, decor in S4.ZONE_DECOR.items():
    sub = "Zones/" + zone.capitalize()
    for i in range(3):
        E.save(S4.ground_tile(zone, i * 16, 0), sub, f"ground_{zone}_{i}", pivot=(0.5, 0.5), glow=False)
    for name, fn in decor.items():
        E.save(fn(), sub, f"{zone}_{name}")
rs = S4.removal_sets()
for i, s in enumerate(rs['tree_round_fall']):
    E.save(s, "Removal", f"tree_round_fall_{i}")
for n, s in zip(("stump_round", "stump_spruce", "stump_dead"), rs['stumps']):
    E.save(s, "Removal", n)
for n, s in zip(("rock_cracked", "rubble", "gravestone_broken", "bush_cut"), rs['remains']):
    E.save(s, "Removal", n)
for n, s in zip(("fx_woodchip", "fx_leaf", "fx_stonechip", "fx_dust_0", "fx_dust_1", "fx_dust_2"), rs['particles']):
    E.save(s, "Removal", n, pivot=(0.5, 0.5))
for n, s in zip(("icon_wood", "icon_stone"), rs['icons']):
    E.save(s, "UI/Icons", n, pivot=(0.5, 0.5), glow=False)
with open(E.PIVOTS, "w") as f:
    json.dump({"items": sorted(E.items.values(), key=lambda i: i["name"])}, f, indent=2)
print(f"ok: {len(E.items)} wpisów w pivots.json")
