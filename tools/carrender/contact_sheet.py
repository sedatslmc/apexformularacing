#!/usr/bin/env python3
"""Combines inspection renders into one labelled contact sheet.
Usage: python3 contact_sheet.py <prefix> <out.png> view1 view2 ..."""
import sys
from PIL import Image, ImageDraw

prefix, out, views = sys.argv[1], sys.argv[2], sys.argv[3:]
tiles = [Image.open(f"{prefix}_{v}.png").convert("RGB") for v in views]
w, h = 800, 450
cols = 2
rows = (len(tiles) + cols - 1) // cols
sheet = Image.new("RGB", (w * cols, h * rows), (10, 10, 12))
for i, (t, v) in enumerate(zip(tiles, views)):
    t = t.resize((w, h), Image.LANCZOS)
    d = ImageDraw.Draw(t)
    d.text((12, 10), v, fill=(230, 230, 230))
    sheet.paste(t, ((i % cols) * w, (i // cols) * h))
sheet.save(out)
