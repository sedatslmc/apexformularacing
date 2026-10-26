#!/usr/bin/env python3
"""Turns a CC0 PBR texture set (Poly Haven or ambientCG) into the files the game loads.

Input: a folder or .zip with the source maps (as downloaded in a browser), or a Poly Haven asset id
(--polyhaven, needs network access to api.polyhaven.com / dl.polyhaven.org).
Recognised maps (case-insensitive):
  base colour  *_diff_* / *diffuse* / *_Color*        (required)
  normal       *_nor_gl_* / *NormalGL*  (OpenGL, as Unity) or *_nor_dx_* / *NormalDX* (green flipped)
  roughness    *_rough_* / *Roughness*
  AO           *_ao_* / *AmbientOcclusion*
  ARM          *_arm_* (AO, roughness, metal packed in R, G, B; used when single maps are missing)

Output: unity/com.projectapex.game/Runtime/Resources/Textures/CC0/<name>/
  BaseColor.jpg          sRGB albedo (JPG q95 by default, --color-format png for lossless)
  PaintBase.jpg          (--paint-base) desaturated albedo normalised to a light mean, for tinted paint (kerbs)
  Normal.png             OpenGL tangent-space normal (Unity's convention)
  MetallicSmoothness.png URP Lit metallic/smoothness map: R = metallic (0), A = smoothness (1 - roughness)
  Occlusion.jpg          grey AO
  source.json            name, real-world size, source, author, licence, mean smoothness, mean linear albedo,
                         SHA-256 of the outputs

Usage:
  python3 tools/textures/import_cc0.py <folder|zip> --name asphalt --metres 4 --source-url <page> --author "<name>"
  python3 tools/textures/import_cc0.py --polyhaven <asset_id> --res 2k --name asphalt
Every imported set needs a line in docs/ASSET_LICENSES.md (the script prints it).
"""
import argparse
import datetime
import hashlib
import io
import json
import os
import re
import sys
import tempfile
import urllib.request
import zipfile

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(ROOT, "unity", "com.projectapex.game", "Runtime", "Resources", "Textures", "CC0")

PATTERNS = {
    "color": [r"_diff(use)?[_.]", r"diffuse", r"_color[_.]", r"basecolor", r"albedo"],
    "normal_gl": [r"_nor_gl[_.]", r"normalgl", r"normal_gl"],
    "normal_dx": [r"_nor_dx[_.]", r"normaldx", r"normal_dx"],
    "rough": [r"_rough(ness)?[_.]", r"roughness"],
    "ao": [r"_ao[_.]", r"ambientocclusion", r"_occlusion"],
    "arm": [r"_arm[_.]"],
}
IMAGE_EXT = (".png", ".jpg", ".jpeg", ".tif", ".tiff", ".exr")


def find_maps(files):
    """files: list of (name, opener). Returns {kind: opener} for the recognised maps."""
    maps = {}
    for name, opener in sorted(files):
        low = os.path.basename(name).lower()
        if not low.endswith(IMAGE_EXT) or low.endswith(".exr"):
            continue
        for kind, pats in PATTERNS.items():
            if kind not in maps and any(re.search(p, low) for p in pats):
                maps[kind] = opener
                break
    return maps


def list_source(path):
    if os.path.isdir(path):
        out = []
        for d, _, names in os.walk(path):
            for n in names:
                p = os.path.join(d, n)
                out.append((p, lambda p=p: open(p, "rb").read()))
        return out
    if zipfile.is_zipfile(path):
        z = zipfile.ZipFile(path)
        return [(n, lambda n=n: z.read(n)) for n in z.namelist()]
    raise SystemExit(f"not a folder or zip: {path}")


def load(opener, mode, size):
    img = Image.open(io.BytesIO(opener()))
    if img.mode in ("I;16", "I;16B", "I"):
        a = np.asarray(img, dtype=np.float32) / 65535.0
        img = Image.fromarray((a * 255).clip(0, 255).astype(np.uint8))
    img = img.convert(mode)
    if size and max(img.size) > size:
        img = img.resize((size, size), Image.LANCZOS)
    return img


def write(img, path):
    if path.endswith(".jpg"):
        img.convert("RGB").save(path, quality=95, subsampling=0, optimize=True)
    else:
        img.save(path, optimize=True)
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def polyhaven_download(asset, res, tmp):
    """Downloads the 2k/4k maps of a Poly Haven texture (API: /info/<id>, /files/<id>)."""
    def get(url):
        req = urllib.request.Request(url, headers={"User-Agent": "ProjectApex-import/1.0"})
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.read()
    info = json.loads(get(f"https://api.polyhaven.com/info/{asset}"))
    files = json.loads(get(f"https://api.polyhaven.com/files/{asset}"))
    wanted = {"diffuse": "diff", "nor_gl": "nor_gl", "rough": "rough", "ao": "ao", "arm": "arm"}
    for key, value in files.items():
        tag = wanted.get(key.lower())
        if not tag or res not in value:
            continue
        fmts = value[res]
        fmt = "png" if "png" in fmts else "jpg" if "jpg" in fmts else None
        if fmt is None:
            continue
        data = get(fmts[fmt]["url"])
        with open(os.path.join(tmp, f"{asset}_{tag}_{res}.{fmt}"), "wb") as f:
            f.write(data)
    authors = ", ".join(info.get("authors", {}).keys())
    dims = info.get("dimensions")  # millimetres [x, y] where given
    metres = max(dims) / 1000.0 if dims else None
    return f"https://polyhaven.com/a/{asset}", authors, metres


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("source", nargs="?", help="folder or zip with the downloaded maps")
    ap.add_argument("--polyhaven", help="Poly Haven asset id (downloads; needs network access)")
    ap.add_argument("--res", default="2k")
    ap.add_argument("--name", required=True, help="set name the game loads (asphalt, kerb, ...)")
    ap.add_argument("--metres", type=float, help="real-world size of one texture tile (m)")
    ap.add_argument("--source-url", default="")
    ap.add_argument("--author", default="")
    ap.add_argument("--licence", default="CC0 1.0")
    ap.add_argument("--max-size", type=int, default=2048, help="largest output size (px); platform limits are set on import")
    ap.add_argument("--paint-base", action="store_true", help="also write PaintBase.png (desaturated, for tinted paint)")
    ap.add_argument("--out-root", default=OUT_ROOT)
    ap.add_argument("--color-format", choices=("jpg", "png"), default="jpg",
                    help="colour, paint base and AO as high-quality JPG (default, small repo) or PNG; normal and smoothness are always PNG")
    a = ap.parse_args(argv)

    tmp = None
    source_url, author, metres = a.source_url, a.author, a.metres
    if a.polyhaven:
        tmp = tempfile.mkdtemp(prefix="cc0_")
        url, authors, dim_m = polyhaven_download(a.polyhaven, a.res, tmp)
        source_url = source_url or url
        author = author or authors
        metres = metres or dim_m
        src = tmp
    else:
        if not a.source:
            raise SystemExit("give a folder/zip or --polyhaven <id>")
        src = a.source
    if not metres:
        raise SystemExit("--metres (real-world size of one tile) is required for this source")

    maps = find_maps(list_source(src))
    if "color" not in maps:
        raise SystemExit("no base colour map found")
    out = os.path.join(a.out_root, a.name)
    os.makedirs(out, exist_ok=True)
    size = a.max_size
    hashes = {}

    color = load(maps["color"], "RGB", size)
    size = color.size[0]
    # Mean linear albedo (sRGB decoded, Rec. 709 luminance): the game calibrates tints against it.
    srgb = np.asarray(color, dtype=np.float32) / 255.0
    lin = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
    mean_albedo = float((lin @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).mean())
    cext = "." + a.color_format
    for old in ("BaseColor", "PaintBase", "Occlusion"):   # a re-import in the other format must not leave both
        for ext in (".png", ".jpg"):
            if ext != cext and os.path.exists(os.path.join(out, old + ext)):
                os.remove(os.path.join(out, old + ext))
    hashes["BaseColor" + cext] = write(color, os.path.join(out, "BaseColor" + cext))

    if a.paint_base:
        lum = np.asarray(color.convert("L"), dtype=np.float32) / 255.0
        # Keep the variation, move the mean to 0.82 so a tint gives the paint colour with wear and grain.
        lum = (lum - lum.mean()) * 0.6 + 0.82
        hashes["PaintBase" + cext] = write(Image.fromarray((lum.clip(0, 1) * 255).astype(np.uint8)).convert("RGB"), os.path.join(out, "PaintBase" + cext))

    if "normal_gl" in maps or "normal_dx" in maps:
        n = np.asarray(load(maps.get("normal_gl") or maps["normal_dx"], "RGB", size), dtype=np.uint8).copy()
        if "normal_gl" not in maps:
            n[..., 1] = 255 - n[..., 1]   # DirectX -> OpenGL (Unity): flip green
        hashes["Normal.png"] = write(Image.fromarray(n), os.path.join(out, "Normal.png"))

    arm = np.asarray(load(maps["arm"], "RGB", size), dtype=np.float32) / 255.0 if "arm" in maps else None
    if "rough" in maps:
        rough = np.asarray(load(maps["rough"], "L", size), dtype=np.float32) / 255.0
    elif arm is not None:
        rough = arm[..., 1]
    else:
        rough = None
    mean_smooth = None
    if rough is not None:
        smooth = 1.0 - rough
        mean_smooth = float(smooth.mean())
        ms = np.zeros(smooth.shape + (4,), dtype=np.uint8)
        ms[..., 3] = (smooth * 255).round().astype(np.uint8)
        hashes["MetallicSmoothness.png"] = write(Image.fromarray(ms, "RGBA"), os.path.join(out, "MetallicSmoothness.png"))
    if "ao" in maps:
        ao = load(maps["ao"], "L", size)
    elif arm is not None:
        ao = Image.fromarray((arm[..., 0] * 255).round().astype(np.uint8))
    else:
        ao = None
    if ao is not None:
        hashes["Occlusion" + cext] = write(ao.convert("RGB"), os.path.join(out, "Occlusion" + cext))

    meta = {
        "name": a.name,
        "metres": metres,
        "meanSmoothness": round(mean_smooth, 4) if mean_smooth is not None else -1,
        "meanAlbedoLinear": round(mean_albedo, 4),
        "source": source_url,
        "author": author,
        "licence": a.licence,
        "imported": datetime.date.today().isoformat(),
        "size": size,
        "files": hashes,
    }
    with open(os.path.join(out, "source.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"wrote {out}: {', '.join(sorted(hashes))}")
    print("ASSET_LICENSES.md line:")
    print(f"| CC0-Texturset `{a.name}` ({size} px, {metres:g} m je Kachel) | {source_url or '?'} ({author or '?'}) | {a.licence} | "
          f"`Resources/Textures/CC0/{a.name}`, importiert {meta['imported']} mit tools/textures/import_cc0.py |")
    return 0


if __name__ == "__main__":
    sys.exit(main())
