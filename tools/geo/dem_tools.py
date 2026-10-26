"""Copernicus DEM GLO-30 helpers for the Bosphorus plan (tiles in data/geo/dem/).

Local frame: east/north metres around Dolmabahçe (41.03944 N, 29.00167 E), true north = +z.
Copernicus DEM is a surface model: in dense districts roofs and trees raise values by
several metres, so road profiles must be smoothed before use. Sea is encoded as 0.
"""
import math
import os
import numpy as np
import tifffile

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
DEM_DIR = os.path.join(ROOT, "data/geo/dem")
LAT0, LON0 = 41.03944, 29.00167
K_LON = 111320.0 * math.cos(math.radians(LAT0))
K_LAT = 110574.0
_tiles = {}


def tile(lon_deg):
    name = f"cop_N41_00_E0{lon_deg:02d}_00.tif"
    if name not in _tiles:
        _tiles[name] = tifffile.imread(os.path.join(DEM_DIR, name))
    return _tiles[name]


def height(lat, lon):
    """Bilinear DEM height (m) at a WGS84 position inside N41, E028–E029."""
    t = tile(int(math.floor(lon)))
    fy = (42.0 - lat) * 3600.0 - 0.5
    fx = (lon - math.floor(lon)) * 3600.0 - 0.5
    y0, x0 = int(math.floor(fy)), int(math.floor(fx))
    ty, tx = fy - y0, fx - x0
    y0, x0 = max(0, min(3598, y0)), max(0, min(3598, x0))
    v = t[y0:y0 + 2, x0:x0 + 2].astype(float)
    return (v[0, 0] * (1 - tx) + v[0, 1] * tx) * (1 - ty) + (v[1, 0] * (1 - tx) + v[1, 1] * tx) * ty


def enu(lat, lon):
    return ((lon - LON0) * K_LON, (lat - LAT0) * K_LAT)


def latlon(e, n):
    return (LAT0 + n / K_LAT, LON0 + e / K_LON)


def grid(lat0, lat1, lon0, lon1):
    """DEM crop (rows north->south) plus its extent in local metres."""
    a, b = tile(28), tile(29)
    m = np.concatenate([a, b], axis=1)
    r0, r1 = int((42 - lat1) * 3600), int((42 - lat0) * 3600)
    c0, c1 = int((lon0 - 28) * 3600), int((lon1 - 28) * 3600)
    z = m[r0:r1, c0:c1]
    e0, n0 = enu(lat0, lon0)
    e1, n1 = enu(lat1, lon1)
    return z, (e0, e1, n0, n1)


def ground_height(e, n, radius=60.0, q=5):
    """Street-level estimate from the surface model: a low percentile of DEM samples within
    `radius` metres (roads lie below roofs and tree crowns). Sea pixels (0) are ignored."""
    vals = []
    for dx in (-radius, -radius / 2, 0.0, radius / 2, radius):
        for dy in (-radius, -radius / 2, 0.0, radius / 2, radius):
            if dx * dx + dy * dy > radius * radius * 1.01:
                continue
            la, lo = latlon(e + dx, n + dy)
            h = height(la, lo)
            if h > 0.0 and not is_roof(la, lo):
                vals.append(h)
    if not vals:
        return 0.0
    vals.sort()
    return vals[min(len(vals) - 1, int(len(vals) * q / 100))]


def road_profile(points, step=10.0, smooth=60.0):
    """Distances and smoothed street-level heights along a polyline [(e, n), ...]."""
    s, h, acc = [], [], 0.0
    for i in range(len(points) - 1):
        a, b = points[i], points[i + 1]
        d = math.dist(a, b)
        k = max(1, int(d / step))
        for j in range(k):
            t = j / k
            x, y = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
            s.append(acc + d * t)
            h.append(ground_height(x, y))
        acc += d
    s.append(acc)
    h.append(ground_height(*points[-1]))
    w = max(1, int(smooth / step / 2))
    hs = [sum(h[max(0, i - w):i + w + 1]) / len(h[max(0, i - w):i + w + 1]) for i in range(len(h))]
    return s, hs


def max_grade(s, h, window=80.0):
    worst = 0.0
    j = 0
    for i in range(len(s)):
        while j < len(s) and s[j] - s[i] < window:
            j += 1
        if j >= len(s):
            break
        worst = max(worst, abs(h[j] - h[i]) / (s[j] - s[i]))
    return worst


_mask = {}


def building_mask(lon_deg):
    """Boolean mask per DEM tile: True where an OSM building footprint covers the pixel
    (data/geo/osm/bosphorus_osm.json). Such pixels are roofs, not ground."""
    if lon_deg in _mask:
        return _mask[lon_deg]
    import json
    from matplotlib.path import Path
    t = tile(lon_deg)
    m = np.zeros(t.shape, dtype=bool)
    src = os.path.join(ROOT, "data/geo/osm/bosphorus_osm.json")
    if os.path.exists(src):
        for b in json.load(open(src))["buildings"]:
            o = b["outline"]
            if len(o) < 3:
                continue
            ll = [latlon(e, n) for e, n in o]
            lats = [p[0] for p in ll]
            lons = [p[1] - lon_deg for p in ll]
            if max(lons) < 0 or min(lons) > 1:
                continue
            r0, r1 = int((42 - max(lats)) * 3600) - 1, int((42 - min(lats)) * 3600) + 1
            c0, c1 = int(min(lons) * 3600) - 1, int(max(lons) * 3600) + 1
            r0, c0 = max(r0, 0), max(c0, 0)
            r1, c1 = min(r1, 3599), min(c1, 3599)
            if r1 < r0 or c1 < c0:
                continue
            rr, cc = np.mgrid[r0:r1 + 1, c0:c1 + 1]
            pts = np.column_stack([(cc.ravel() + 0.5) / 3600.0, 42 - (rr.ravel() + 0.5) / 3600.0])
            inside = Path(list(zip(lons, lats))).contains_points(pts)
            m[rr.ravel()[inside], cc.ravel()[inside]] = True
    _mask[lon_deg] = m
    return m


def is_roof(lat, lon):
    m = building_mask(int(math.floor(lon)))
    r = int((42.0 - lat) * 3600.0)
    c = int((lon - math.floor(lon)) * 3600.0)
    return bool(m[min(max(r, 0), 3599), min(max(c, 0), 3599)])
