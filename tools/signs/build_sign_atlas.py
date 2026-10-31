#!/usr/bin/env python3
"""Shop-sign texture atlas for the street fronts of the real city (StreetFronts.Catalog).

The catalog lives in C# (unity/.../Simulation/Environment/StreetFronts.cs); export it first:
    cd build/Apex.ModelPreview && dotnet run -c Release -- <dir> --sign-catalog
then render the atlas:
    python3 tools/signs/build_sign_atlas.py <dir>/sign_catalog.json [--preview <png>]

Writes Runtime/Resources/Textures/Signs/ShopSigns.jpg (2048 x 2048, 4 x 16 cells of 512 x 128, cell order =
catalog order, row 0 at the top) and ShopSigns.json (manifest: texts in order, font, size; a test compares it with
the catalog). Lettering: DejaVu Sans Bold / Book (Bitstream Vera licence with DejaVu changes in the public domain;
covers the Turkish letters ç ğ ı İ ö ş ü). Generic trade names only: no real businesses, brands or logos.
"""
import argparse
import json
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "unity/com.projectapex.game/Runtime/Resources/Textures/Signs")
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
BOOK = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
CW, CH = 512, 128


def fit(draw, text, font_path, max_w, max_h):
    size = max_h
    while size > 10:
        f = ImageFont.truetype(font_path, size)
        l, t, r, b = draw.textbbox((0, 0), text, font=f)
        if r - l <= max_w and b - t <= max_h:
            return f
        size -= 1
    return ImageFont.truetype(font_path, 10)


def mix(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def cell(sign):
    board, letters = tuple(sign["board"]), tuple(sign["letters"])
    im = Image.new("RGB", (CW, CH), board)
    d = ImageDraw.Draw(im)
    # Painted metal board: a soft vertical gradient, an inset border line and four fixing screws.
    for y in range(CH):
        d.line([(0, y), (CW, y)], fill=mix(board, 1.06 - 0.12 * y / CH))
    d.rectangle([8, 8, CW - 9, CH - 9], outline=mix(letters, 0.92), width=3)
    for x, y in ((16, 16), (CW - 17, 16), (16, CH - 17), (CW - 17, CH - 17)):
        d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=mix(board, 0.6))
    text, sub = sign["text"], sign["subtitle"]
    if sub:
        f = fit(d, text, BOLD, CW - 70, 58)
        fs = fit(d, sub, BOOK, CW - 90, 22)
        d.text((CW / 2, 52), text, font=f, fill=letters, anchor="mm")
        d.text((CW / 2, 98), sub, font=fs, fill=letters, anchor="mm")
    else:
        f = fit(d, text, BOLD, CW - 70, 70)
        d.text((CW / 2, CH / 2 + 2), text, font=f, fill=letters, anchor="mm")
    return im


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("catalog")
    ap.add_argument("--preview", help="also write a scaled preview PNG of the used cells")
    args = ap.parse_args()
    cat = json.load(open(args.catalog, encoding="utf-8"))
    cols, rows = cat["columns"], cat["rows"]
    signs = cat["signs"]
    assert len(signs) <= cols * rows, "catalog larger than the atlas"
    atlas = Image.new("RGB", (cols * CW, rows * CH), (40, 40, 42))
    for i, s in enumerate(signs):
        atlas.paste(cell(s), ((i % cols) * CW, (i // cols) * CH))
    os.makedirs(OUT, exist_ok=True)
    atlas.save(os.path.join(OUT, "ShopSigns.jpg"), quality=90, optimize=True)
    manifest = {"generator": "tools/signs/build_sign_atlas.py", "font": "DejaVu Sans Bold / Book",
                "columns": cols, "rows": rows, "cell": [CW, CH], "texts": [s["text"] for s in signs]}
    with open(os.path.join(OUT, "ShopSigns.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=1)
    print(f"wrote {len(signs)} signs to {os.path.relpath(OUT, ROOT)}/ShopSigns.jpg")
    if args.preview:
        used = (len(signs) + cols - 1) // cols
        atlas.crop((0, 0, cols * CW, used * CH)).resize((cols * CW // 2, used * CH // 2), Image.LANCZOS).save(args.preview)


if __name__ == "__main__":
    main()
