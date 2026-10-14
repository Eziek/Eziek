"""Kompilacja shaderów Unity (.shader z blokami HLSLPROGRAM) poza Unity, kompilatorem DXC na prawdziwych include'ach URP.

Wyłapuje błędy składni, typów i brakujące symbole w NASZYCH shaderach. Nie sprawdza wszystkiego, co robi Unity
(warianty multi_compile poza pierwszym, inne platformy niż D3D11), więc wynik w Unity trzeba i tak potwierdzić.

Przygotowanie (raz, do scratchpada; patrz CLAUDE.md):
  DXC:      https://github.com/microsoft/DirectXShaderCompiler/releases (linux_dxc_*.x86_64.tar.gz) -> <dxc>/bin/dxc
  Biblioteki: git clone --depth 1 --filter=blob:none --sparse https://github.com/Unity-Technologies/Graphics.git <gfx>
              git -C <gfx> sparse-checkout set Packages/com.unity.render-pipelines.core/ShaderLibrary \
                  Packages/com.unity.render-pipelines.universal/ShaderLibrary Packages/com.unity.render-pipelines.universal/Shaders/2D/Include
              (gałąź master = URP 17.6, zgodne z Unity 6.6)
Uruchom:
  python3 tools/shadercheck/check.py --dxc <dxc>/bin/dxc --gfx <gfx> Assets/_Project/Shaders/*.shader
"""
import argparse, itertools, os, re, subprocess, sys, tempfile

BASE_DEFINES = ["SHADER_API_D3D11", "UNITY_VERSION=6000", "UNITY_UV_STARTS_AT_TOP=1", "UNITY_REVERSED_Z=1",
                "UNITY_NEAR_CLIP_VALUE=(1.0)", "SHADER_TARGET=45", "UNITY_COLORSPACE_GAMMA"]


def blocks(src):
    for m in re.finditer(r"HLSLPROGRAM(.*?)ENDHLSL", src, re.S):
        yield m.group(1)


def variants(code):
    """Pierwszy wariant każdego multi_compile/shader_feature (zwykle '_' = wyłączony) + każdy wariant pojedynczo."""
    groups = []
    for m in re.finditer(r"#pragma\s+(?:multi_compile|shader_feature)(?:_local)?(?:_fragment|_vertex)?\s+(.+)", code):
        opts = [o for o in m.group(1).split() if o]
        if opts and not opts[0].startswith("_instancing"):
            groups.append(opts)
    combos = [[]]
    for g in groups:
        for o in g:
            if o not in ("_", "__"):
                combos.append([o])
    return combos


def prepare(code, include_dirs):
    code = re.sub(r"#include_with_pragmas", "#include", code)
    code = re.sub(r"^\s*#pragma\s+(?!warning).*$", "", code, flags=re.M)
    return code


def compile_block(dxc, gfx, path, idx, code, entry_v, entry_f, defines):
    tmp = tempfile.mkdtemp(prefix="shc_")
    link = os.path.join(tmp, "Packages")
    os.symlink(os.path.join(gfx, "Packages"), link)
    shader_dir = os.path.dirname(os.path.abspath(path))
    f = os.path.join(tmp, f"block{idx}.hlsl")
    with open(f, "w") as fh:
        fh.write(prepare(code, None))
    ok = True
    for stage, entry, prof in (("VERTEX", entry_v, "vs_6_0"), ("FRAGMENT", entry_f, "ps_6_0")):
        args = [dxc, "-T", prof, "-E", entry, "-I", tmp, "-I", shader_dir, "-HV", "2018", "-Wno-everything", f]
        for d in BASE_DEFINES + [f"SHADER_STAGE_{stage}"] + defines:
            args += ["-D", d]
        r = subprocess.run(args, capture_output=True, text=True)
        if r.returncode != 0:
            ok = False
            err = "\n".join(l for l in r.stderr.splitlines() if "error" in l or "note" not in l)[:3000]
            print(f"  BŁĄD {os.path.basename(path)} blok {idx} {stage} {defines}:\n{err}")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dxc", required=True)
    ap.add_argument("--gfx", required=True)
    ap.add_argument("files", nargs="+")
    a = ap.parse_args()
    all_ok = True
    for path in a.files:
        src = open(path, encoding="utf-8").read()
        n = 0
        for i, code in enumerate(blocks(src)):
            v = re.search(r"#pragma\s+vertex\s+(\w+)", code)
            fr = re.search(r"#pragma\s+fragment\s+(\w+)", code)
            if not v or not fr:
                continue
            for combo in variants(code):
                n += 1
                all_ok &= compile_block(a.dxc, a.gfx, path, i, code, v.group(1), fr.group(1), combo)
        print(f"{'OK ' if all_ok else '!! '} {path}: {n} kompilacji")
    sys.exit(0 if all_ok else 1)


if __name__ == "__main__":
    main()
