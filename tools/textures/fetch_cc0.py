#!/usr/bin/env python3
"""Fetches the circuit's CC0 sets listed in cc0_sets.json from Poly Haven and imports them (import_cc0.py).

  --tier base   1K into Runtime/Resources/Textures/CC0 (committed; 512 px/m on the road)
  --tier ultra  4K into Runtime/Resources/Textures/CC0_Ultra (gitignored, ~50 MB per set; the game prefers
                these when present, the Unity importer caps them at 2048 px on mobile)
Needs network access to api.polyhaven.com and dl.polyhaven.org.
"""
import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import import_cc0  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
TEXTURES = os.path.join(import_cc0.ROOT, "unity", "com.projectapex.game", "Runtime", "Resources", "Textures")
TIERS = {"base": ("1k", "CC0", 1024), "ultra": ("4k", "CC0_Ultra", 4096)}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--tier", choices=sorted(TIERS), default="ultra")
    ap.add_argument("--only", help="comma-separated set names")
    a = ap.parse_args(argv)
    res, folder, size = TIERS[a.tier]
    sets = json.load(open(os.path.join(HERE, "cc0_sets.json"), encoding="utf-8"))["sets"]
    for s in sets:
        if a.only and s["name"] not in a.only.split(","):
            continue
        args = ["--polyhaven", s["polyhaven"], "--res", res, "--name", s["name"], "--max-size", str(size),
                "--out-root", os.path.join(TEXTURES, folder), "--prefer-jpg"]
        if s.get("paint_base"):
            args.append("--paint-base")
        if s.get("detail"):
            args.append("--detail")
        print(f"== {s['name']} <- polyhaven {s['polyhaven']} ({res})", flush=True)
        import_cc0.main(args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
