#!/usr/bin/env python3
"""City data for the Bosphorus GP (variant A): the real buildings, streets, green areas and walls
around the circuit, from the OSM import (data/geo/osm/bosphorus_osm.json) and the Copernicus DEM.

Output: unity/com.projectapex.game/Runtime/Resources/City/bosphorus_a.json
  buildings  footprint (local metres, rounded to 0.1 m, simplified), height (m) and its source:
             "osm" (height tag), "levels" (building:levels x 3.2 m), "estimate" (median of the tagged
             buildings within 300 m, else 16 m, +-1 storey by id so a street is not one flat cornice;
             small footprints and houses capped). The 30 m surface model is not used for heights: against
             106 tagged buildings it underestimated by a factor of 2.8 (correlation 0.3).
  streets    centreline (x, z), highway class, width, name; the circuit's own streets are left out
             (points within the track corridor)
  green      park, garden, forest/wood, cemetery, grass, scrub outlines
  walls      retaining and city walls (polylines)

The file is an ODbL derivative database (© OpenStreetMap contributors); heights include Copernicus DEM
data. See docs/CITY_FROM_OSM.md.

Usage: python3 tools/geo/build_city_a.py [--radius 750]
"""
import argparse
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import dem_tools as dem  # noqa: E402

ROOT = dem.ROOT
OSM = os.path.join(ROOT, "data/geo/osm/bosphorus_osm.json")
TRACK = os.path.join(ROOT, "unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_a.json")
OUT = os.path.join(ROOT, "unity/com.projectapex.game/Runtime/Resources/City/bosphorus_a.json")
STREET_CLASSES = {"primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link", "residential",
                  "unclassified", "living_street", "service", "pedestrian", "footway", "steps", "path"}
GREEN = {"park", "garden", "forest", "wood", "cemetery", "grass", "scrub", "recreation_ground"}
LEVEL_HEIGHT = 3.2
# Historic ensembles OSM does not name, identified by place and form (local metres, radius, storeys):
# Akaretler row houses (Akaretler Sıra Evleri, 1875, Sarkis Balyan) on Şair Nedim Caddesi: the 18 three-storey
# terraced houses in a row of about 7 m frontages.
ENSEMBLES = {"akaretler": {"centre": (121.0, 315.0), "radius": 70.0, "levels": 3, "max_area": 130.0}}


def track_points():
    t = json.load(open(TRACK, encoding="utf-8"))
    return np.array([[p[0], p[1]] for p in t["points"]], dtype=float), float(t.get("default_width", 11.0))


def densify(pts, step=8.0):
    out = []
    for a, b in zip(pts, np.roll(pts, -1, axis=0)):
        n = max(1, int(np.linalg.norm(b - a) // step))
        for k in range(n):
            out.append(a + (b - a) * k / n)
    return np.array(out)


class Nearest:
    """Distance to the track polyline (densified points, uniform grid buckets)."""

    def __init__(self, pts, cell=100.0):
        self.pts, self.cell, self.bins = pts, cell, {}
        for i, (x, z) in enumerate(pts):
            self.bins.setdefault((int(x // cell), int(z // cell)), []).append(i)

    def dist(self, x, z, reach=1):
        cx, cz = int(x // self.cell), int(z // self.cell)
        best = 1e9
        for dx in range(-reach, reach + 1):
            for dz in range(-reach, reach + 1):
                for i in self.bins.get((cx + dx, cz + dz), ()):
                    px, pz = self.pts[i]
                    best = min(best, (px - x) ** 2 + (pz - z) ** 2)
        return math.sqrt(best) if best < 1e9 else float("inf")


def simplify(outline, tol=0.4):
    """Douglas-Peucker on a closed ring (keeps at least 3 points)."""
    pts = [tuple(p[:2]) for p in outline]
    if len(pts) > 3 and pts[0] == pts[-1]:
        pts = pts[:-1]

    def dp(seg):
        if len(seg) < 3:
            return seg
        a, b = np.array(seg[0]), np.array(seg[-1])
        ab = b - a
        L = np.linalg.norm(ab)
        best, idx = 0.0, 0
        for i in range(1, len(seg) - 1):
            p = np.array(seg[i])
            q = p - a
            d = abs(ab[0] * q[1] - ab[1] * q[0]) / L if L > 1e-6 else np.linalg.norm(q)
            if d > best:
                best, idx = d, i
        if best <= tol:
            return [seg[0], seg[-1]]
        return dp(seg[:idx + 1])[:-1] + dp(seg[idx:])
    if len(pts) < 4:
        return pts
    half = len(pts) // 2
    ring = dp(pts[:half + 1])[:-1] + dp(pts[half:] + [pts[0]])[:-1]
    return ring if len(ring) >= 3 else pts


def area(ring):
    a = 0.0
    for (x0, z0), (x1, z1) in zip(ring, ring[1:] + ring[:1]):
        a += x0 * z1 - x1 * z0
    return abs(a) * 0.5


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--radius", type=float, default=750.0, help="keep features within this distance of the centreline (m)")
    a = ap.parse_args(argv)
    osm = json.load(open(OSM, encoding="utf-8"))
    tp, width = track_points()
    near = Nearest(densify(tp))
    reach = int(math.ceil(a.radius / 100.0)) + 1
    corridor = width * 0.5 + 6.0

    buildings = []
    for b in osm["buildings"]:
        o = b["outline"]
        if len(o) < 3:
            continue
        cx = sum(p[0] for p in o) / len(o)
        cz = sum(p[1] for p in o) / len(o)
        if near.dist(cx, cz, reach) > a.radius:
            continue
        ring = simplify(o)
        ar = area(ring)
        if ar < 12.0:
            continue
        h, src = None, None
        if b.get("height") and b.get("height_source") in ("osm", "levels"):
            h, src = float(b["height"]), b["height_source"]
        buildings.append({"id": b["id"], "type": b.get("type") or "yes", "name": b.get("name") or "",
                          "historic": b.get("historic") or "", "amenity": b.get("amenity") or "",
                          "outline": [[round(x, 1), round(z, 1)] for x, z in ring], "height": h, "source": src,
                          "_c": (cx, cz), "_a": ar})
    STRUCTURES = ("column", "bridge", "tower", "mast", "chimney", "silo", "storage_tank")
    # Neighbourhood estimate for the rest: median of tagged heights within 300 m (whole OSM extract).
    measured = []
    for t in osm["buildings"]:
        # Structures are no neighbourhood reference: the Bosphorus Bridge towers (OSM "column", 165 m) made the
        # houses of Ortaköy 60 m tall.
        if (t.get("height") and t.get("height_source") in ("osm", "levels") and len(t["outline"]) >= 3
                and (t.get("type") or "yes") not in STRUCTURES and float(t["height"]) <= 60.0):
            o = t["outline"]
            measured.append(((sum(p[0] for p in o) / len(o), sum(p[1] for p in o) / len(o)), float(t["height"])))
    mx = np.array([c for c, _ in measured]) if measured else np.zeros((0, 2))
    mh = np.array([h for _, h in measured]) if measured else np.zeros(0)
    for b in buildings:
        if b["height"] is None:
            if len(mx):
                d2 = ((mx - np.array(b["_c"])) ** 2).sum(axis=1)
                sel = mh[d2 < 300.0 ** 2]
                h = float(np.median(sel)) if len(sel) >= 3 else 16.0
            else:
                h = 16.0
            # +-1 storey, deterministic per building.
            h += ((int(b["id"]) * 2654435761) % 3 - 1) * LEVEL_HEIGHT
            # Small footprints rarely carry tall blocks; houses and kiosks stay low.
            if b["_a"] < 60.0:
                h = min(h, 6.5)
            elif b["type"] in ("house", "roof", "kiosk", "shed", "garage"):
                h = min(h, 7.0)
            b["height"], b["source"] = h, "estimate"
        b["height"] = round(min(max(b["height"], 3.0), 60.0), 1)
        for name, e in ENSEMBLES.items():
            if (math.dist(b["_c"], e["centre"]) < e["radius"] and b["_a"] < e["max_area"]
                    and b["source"] == "levels" and abs(b["height"] - e["levels"] * LEVEL_HEIGHT) < 1.0):
                b["ensemble"] = name
        del b["_c"], b["_a"]

    streets = []
    for s in osm["streets"]:
        if s["highway"] not in STREET_CLASSES or s.get("tunnel"):
            continue
        pts = s["points"]
        d = [near.dist(p[0], p[1], reach) for p in pts]
        if min(d) > a.radius:
            continue
        # The circuit's own streets: most points inside the track corridor.
        inside = sum(1 for x in d if x < corridor)
        if inside >= 0.5 * len(pts):
            continue
        streets.append({"id": s["id"], "highway": s["highway"], "name": s.get("name") or "", "width": s.get("width") or 0,
                        "oneway": bool(s.get("oneway")), "bridge": bool(s.get("bridge")),
                        "points": [[round(p[0], 1), round(p[1], 1)] for p in pts]})

    green = []
    for g in osm["green"]:
        if g.get("kind") not in GREEN:
            continue
        o = g["outline"]
        if near.dist(sum(p[0] for p in o) / len(o), sum(p[1] for p in o) / len(o), reach + 3) > a.radius + 300.0:
            continue
        green.append({"id": g["id"], "kind": g["kind"], "name": g.get("name") or "", "outline": [[round(p[0], 1), round(p[1], 1)] for p in simplify(o, 1.0)]})

    walls = [{"id": w["id"], "points": [[round(p[0], 1), round(p[1], 1)] for p in w["points"]]}
             for w in osm["walls"] if min(near.dist(p[0], p[1], reach) for p in w["points"]) < a.radius]

    out = {
        "frame": osm["frame"],
        "source": "© OpenStreetMap contributors (ODbL 1.0), heights partly Copernicus DEM GLO-30; derived by tools/geo/build_city_a.py",
        "radius": a.radius,
        "buildings": buildings, "streets": streets, "green": green, "walls": walls,
    }
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    ens = sum(1 for b in buildings if b.get("ensemble"))
    print(f"{ens} buildings in historic ensembles")
    srcs = {}
    for b in buildings:
        srcs[b["source"]] = srcs.get(b["source"], 0) + 1
    print(f"{len(buildings)} buildings {srcs}, {len(streets)} streets, {len(green)} green areas, {len(walls)} walls; "
          f"{os.path.getsize(OUT) / 1e6:.1f} MB -> {os.path.relpath(OUT, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
