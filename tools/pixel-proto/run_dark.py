import os, sys, runpy
import sprites
DARK = {
    'o':  "#0f0a10",
    'p0': "#1c1520", 'p1': "#33263a", 'p2': "#563447",
    'co': "#a24d48", 'or': "#b4673f", 'go': "#c49a55", 'cr': "#d8ccab",
    'e0': "#20171c", 'e1': "#352629", 'e2': "#523b35", 'e3': "#715444",
    'g0': "#141f1c", 'g1': "#21342c", 'g2': "#344a3b", 'g3': "#566d52",
    'b0': "#121c27", 'b1': "#1f3044", 'b2': "#30485f", 'b3': "#56697a", 'fo': "#848e9b",
}
sprites.PAL.update(DARK)
sprites.RGB.update({k: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) for k, v in DARK.items()})
os.environ["OUT"] = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out") + "/dark_"
os.environ["DARK"] = "1"
runpy.run_path(sys.argv[1], run_name="__main__")
