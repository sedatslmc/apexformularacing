#!/usr/bin/env python3
"""Land-mass outlines for the Bosphorus GP from the Copernicus DEM (sea = 0 m).

Traces the 0.5 m contour of the DEM land mask over the wider area, simplifies it
(Douglas-Peucker, 20 m) and classifies the polygons into the European side (contains
the circuit), the Asian side (contains the bridge's Asian end) and the historic
peninsula (contains the Sultan Ahmed Mosque). Returns game-frame polygons
(east/north metres, unrotated frame of dem_tools).
"""
import math
import os
import sys

import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.path import Path

sys.path.insert(0, os.path.dirname(__file__))
import dem_tools as dem

AREA = (40.995, 41.095, 28.935, 29.095)


def simplify(pts, tol):
    if len(pts) < 3:
        return pts
    a, b = np.array(pts[0]), np.array(pts[-1])
    ab = b - a
    L = np.linalg.norm(ab)
    P = np.array(pts)
    if L < 1e-9:
        d = np.linalg.norm(P - a, axis=1)
    else:
        d = np.abs(ab[0] * (P[:, 1] - a[1]) - ab[1] * (P[:, 0] - a[0])) / L
    i = int(np.argmax(d))
    if d[i] > tol:
        left = simplify(pts[:i + 1], tol)
        right = simplify(pts[i:], tol)
        return left[:-1] + right
    return [pts[0], pts[-1]]


def polygons():
    lat0, lat1, lon0, lon1 = AREA
    z, (e0, e1, n0, n1) = dem.grid(lat0, lat1, lon0, lon1)
    land = (z > 0.0).astype(float)
    # Pad with sea so every land contour closes inside the area.
    pad = np.zeros((land.shape[0] + 2, land.shape[1] + 2))
    pad[1:-1, 1:-1] = land
    ys = np.linspace(n1, n0, z.shape[0])
    xs = np.linspace(e0, e1, z.shape[1])
    dx, dy = xs[1] - xs[0], ys[1] - ys[0]
    xs = np.concatenate([[xs[0] - dx], xs, [xs[-1] + dx]])
    ys = np.concatenate([[ys[0] - dy], ys, [ys[-1] + dy]])
    cs = plt.contour(xs, ys, pad, levels=[0.5])
    out = []
    for path in cs.get_paths():
        for poly in path.to_polygons():
            if len(poly) < 20:
                continue
            area = 0.5 * abs(np.dot(poly[:, 0], np.roll(poly[:, 1], 1)) - np.dot(poly[:, 1], np.roll(poly[:, 0], 1)))
            if area < 2.0e5:
                continue
            out.append([tuple(p) for p in simplify([tuple(p) for p in poly], 20.0)])
    plt.close("all")
    return out


def classified(track_point, asia_point, peninsula_point):
    result = {}
    for poly in polygons():
        path = Path(poly)
        for name, pt, rise, scale in (("europe", track_point, 70, 700), ("asia", asia_point, 120, 900),
                                      ("peninsula", peninsula_point, 45, 320)):
            if name not in result and path.contains_point(pt):
                result[name] = {"id": name, "rise": rise, "scale": scale,
                                "polygon": [[round(x, 1), round(y, 1)] for x, y in poly[:-1]]}
    return list(result.values())


if __name__ == "__main__":
    tp = (0.0, 150.0)
    ap = dem.enu(41.03998, 29.04010 + 0.004)
    pp = dem.enu(41.00526, 28.97687)
    for m in classified(tp, ap, pp):
        print(m["id"], len(m["polygon"]), "points")
