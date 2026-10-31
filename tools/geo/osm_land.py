#!/usr/bin/env python3
"""Land masses and shorelines for the Bosphorus GP from the OSM coastline (data/geo/osm/bosphorus_osm.json).

Replaces the earlier DEM coastline (Copernicus GLO-30, land = height > 0 m): near the shore the DEM is not 0 m
over water (30 m pixels mix quays, ships and sea; the surface model carries noise), so its 0.5 m contour ran
200-450 m out into the Bosphorus between Dolmabahçe and Ortaköy, and the game invented a wide strip of land
(then filled with trees) between the waterfront landmarks and the water. The OSM coastline is the surveyed
waterline (quays included) and lies where the landmarks really stand.

Method: the coastline ways (land on the left, OSM convention) are stitched into chains; chains that leave the
extract are closed along its bounding box, keeping land on the left; the resulting polygons are simplified
(Douglas-Peucker, 2.5 m) and classified like before: European side (contains the circuit), Asian side
(contains the bridge's Asian end), historic peninsula (contains the Sultan Ahmed Mosque).
The shorelines (x as a function of z, used for quays and ship routes) are scanned from these polygons.

Usage: python3 tools/geo/osm_land.py [--apply]   (--apply updates geo.land, shoreline and shoreline_asia in
       unity/.../Tracks/bosphorus_a.json and leaves every other field untouched)
Data © OpenStreetMap contributors, ODbL 1.0.
"""
import json
import math
import os
import sys

import numpy as np
from matplotlib.path import Path

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OSM = os.path.join(ROOT, "data/geo/osm/bosphorus_osm.json")
TRACK = os.path.join(ROOT, "unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_a.json")
SIMPLIFY = 2.5


def chains(ways):
    """Stitch ways sharing end nodes into chains (closed rings stay as they are)."""
    # The extract merges two Overpass queries (core and wider coast), so some ways appear twice: keep one per id.
    seen, unique = set(), []
    for w in ways:
        if w["id"] not in seen and len(w["points"]) >= 2:
            seen.add(w["id"])
            unique.append(w)
    ways = [list(map(tuple, w["points"])) for w in unique]
    by_start = {}
    for i, w in enumerate(ways):
        by_start.setdefault(w[0], []).append(i)
    used = [False] * len(ways)
    out = []
    for i, w in enumerate(ways):
        if used[i]:
            continue
        # Walk back to the head of this chain (a way no other way ends into), unless it is a ring.
        used[i] = True
        chain = list(w)
        while True:
            nxt = [j for j in by_start.get(chain[-1], []) if not used[j]]
            if not nxt:
                break
            used[nxt[0]] = True
            chain += ways[nxt[0]][1:]
        while True:
            prev = [j for j, v in enumerate(ways) if not used[j] and v[-1] == chain[0]]
            if not prev:
                break
            used[prev[0]] = True
            chain = ways[prev[0]][:-1] + chain
        out.append(chain)
    return out


def simplify(pts, tol):
    if len(pts) < 3:
        return pts
    stack, keep = [(0, len(pts) - 1)], {0, len(pts) - 1}
    P = np.array(pts)
    while stack:
        a, b = stack.pop()
        if b <= a + 1:
            continue
        ab = P[b] - P[a]
        L = np.hypot(*ab)
        seg = P[a + 1:b] - P[a]
        d = np.abs(ab[0] * seg[:, 1] - ab[1] * seg[:, 0]) / L if L > 1e-9 else np.hypot(seg[:, 0], seg[:, 1])
        i = int(np.argmax(d))
        if d[i] > tol:
            k = a + 1 + i
            keep.add(k)
            stack += [(a, k), (k, b)]
    return [pts[i] for i in sorted(keep)]


def land_polygons(osm):
    cs = chains(osm["coastline"])
    rings = [c for c in cs if c[0] == c[-1]]
    opens = [c for c in cs if c[0] != c[-1]]
    pts = np.array([p for c in cs for p in c])
    x0, z0 = pts.min(axis=0) - 1.0
    x1, z1 = pts.max(axis=0) + 1.0
    perim = 2 * ((x1 - x0) + (z1 - z0))

    def param(p):
        """Position along the box boundary, counter-clockwise from (x0, z0)."""
        x, z = p
        d = [abs(z - z0), abs(x - x1), abs(z - z1), abs(x - x0)]
        side = int(np.argmin(d))
        if side == 0:
            return x - x0
        if side == 1:
            return (x1 - x0) + (z - z0)
        if side == 2:
            return (x1 - x0) + (z1 - z0) + (x1 - x)
        return 2 * (x1 - x0) + (z1 - z0) + (z1 - z)

    def at(s):
        s %= perim
        w, h = x1 - x0, z1 - z0
        if s < w:
            return (x0 + s, z0)
        if s < w + h:
            return (x1, z0 + s - w)
        if s < 2 * w + h:
            return (x1 - (s - w - h), z1)
        return (x0, z1 - (s - 2 * w - h))

    corners = [0.0, x1 - x0, (x1 - x0) + (z1 - z0), 2 * (x1 - x0) + (z1 - z0)]
    polys = [list(r[:-1]) for r in rings]
    # Close open chains: from a chain's end, follow the boundary counter-clockwise (land stays on the left)
    # to the next chain start, continue with that chain, until back at the first chain.
    starts = [(param(c[0]), k) for k, c in enumerate(opens)]
    done = [False] * len(opens)
    for k0 in range(len(opens)):
        if done[k0]:
            continue
        poly, k = [], k0
        while True:
            done[k] = True
            poly += opens[k]
            s_end = param(opens[k][-1])
            # next start counter-clockwise from s_end
            cand = sorted(starts, key=lambda sk: (sk[0] - s_end) % perim)
            s_next, k_next = cand[0]
            # boundary corners passed on the way
            span = (s_next - s_end) % perim
            for cpos in sorted(corners, key=lambda c: (c - s_end) % perim):
                if 0 < (cpos - s_end) % perim < span:
                    poly.append(at(cpos))
            if k_next == k0 or done[k_next]:
                break
            k = k_next
        polys.append(poly)
    out = []
    for p in polys:
        if len(p) < 4:
            continue
        ring = simplify(p + [p[0]], SIMPLIFY)[:-1]
        area = 0.5 * abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(ring, ring[1:] + ring[:1])))
        if area > 2.0e4:
            out.append(ring)
    return out


def classified(osm, landmarks):
    pts = {"europe": (0.0, 150.0), "asia": tuple(landmarks["bridge_as"]), "peninsula": tuple(landmarks["sultan_ahmed"])}
    params = {"europe": (70, 700), "asia": (120, 900), "peninsula": (45, 320)}
    result = []
    for poly in land_polygons(osm):
        path = Path(poly)
        for name, pt in pts.items():
            # The historic peninsula joins the European side round the head of the Golden Horn: one polygon.
            if path.contains_point(pt) and all(m["id"] != name and m["polygon"][0] != [round(poly[0][0], 1), round(poly[0][1], 1)] for m in result):
                rise, scale = params[name]
                result.append({"id": name, "rise": rise, "scale": scale, "source": "OSM coastline",
                               "polygon": [[round(x, 1), round(z, 1)] for x, z in poly]})
    return result


def shoreline(masses, name, x_from, x_to, step, zs):
    """x of the waterline for each z: first point (scanning from x_from towards x_to) followed by 3 water samples."""
    poly = next(m for m in masses if m["id"] == name)["polygon"]
    path = Path(poly)
    out = []
    for z in zs:
        xs = np.arange(x_from, x_to, step)
        water = ~path.contains_points(np.column_stack([xs, np.full_like(xs, z)]))
        run = np.convolve(water.astype(int), np.ones(3, int), "valid")
        hit = np.where(run == 3)[0]
        if len(hit):
            out.append([round(float(xs[hit[0]]), 1), z])
    return out


def main(argv):
    osm = json.load(open(OSM, encoding="utf-8"))
    track = json.load(open(TRACK, encoding="utf-8"))
    lm = {l["id"]: l["game"] for l in track["geo"]["landmarks"]}
    masses = classified(osm, lm)
    for m in masses:
        print(f"{m['id']}: {len(m['polygon'])} points")
    zs = list(range(-900, 2601, 20))
    shore = shoreline(masses, "europe", -1500.0, 4200.0, 2.0, zs)
    asia = shoreline(masses, "asia", 4200.0, 1500.0, -2.0, zs) if any(m["id"] == "asia" for m in masses) else []
    print(f"shoreline {len(shore)} points, Asian shoreline {len(asia)} points")
    if "--apply" in argv:
        track["geo"]["land"] = masses
        track["shoreline"] = shore
        track["geo"]["shoreline_asia"] = asia
        track["geo"]["land_source"] = "OSM coastline (tools/geo/osm_land.py), © OpenStreetMap contributors, ODbL 1.0"
        json.dump(track, open(TRACK, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print("updated", os.path.relpath(TRACK, ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
