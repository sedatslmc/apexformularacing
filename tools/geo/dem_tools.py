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
