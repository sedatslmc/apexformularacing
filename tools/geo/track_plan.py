#!/usr/bin/env python3
"""Draft geographic track plan for the Bosphorus GP (phase 3, awaiting approval).

Draws the Copernicus DEM (hillshade, DEM coastline), verified landmarks and the
DRAFT corridor sketch along real street corridors. The corridor waypoints are
approximate (from street knowledge, NOT from OSM): they only show which streets
the circuit uses. Once an OSM extract is available, tools/geo/osm_import.py
snaps the circuit to real centrelines and this script re-checks it.

Usage: python3 tools/geo/track_plan.py
"""
import math
import os
import sys
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.colors import LightSource

sys.path.insert(0, os.path.dirname(__file__))
import dem_tools as dem

OUT = os.path.join(dem.ROOT, "docs/img/bosphorus_track_plan_draft.png")
PROFILE = os.path.join(dem.ROOT, "docs/img/bosphorus_track_plan_profile.png")

# Verified landmark coordinates (see docs/BOSPHORUS_ENVIRONMENT_PLAN.md for sources).
LANDMARKS = [
    ("Dolmabahçe-Palast", 41.03944, 29.00167, True),
    ("Ortaköy-Moschee", 41.04694, 29.02694, True),
    ("Brücke EU", 41.05102, 29.02858, True),
    ("Brücke AS", 41.03998, 29.04010, True),
    ("Galata-Turm", 41.02566, 28.97416, True),
    ("Leanderturm", 41.02117, 29.00425, True),
    ("Sultan-Ahmed-Moschee", 41.00526, 28.97687, True),
    ("Çırağan-Palast (≈)", 41.0436, 29.0160, False),
    ("Beşiktaş-Anleger (≈)", 41.0418, 29.0073, False),
    ("Yıldız-Park (≈)", 41.0480, 29.0135, False),
]

# DRAFT corridor (counter-clockwise), approximate: (lat, lon, street/section label or None).
CORRIDOR = [
    (41.0368, 28.9950, "S/Z – Dolmabahçe Cd."),
    (41.0385, 28.9985, None),
    (41.0400, 29.0012, None),
    (41.0418, 29.0048, None),
    (41.0428, 29.0068, "T1 Beşiktaş-Platz"),
    (41.0432, 29.0100, "Çırağan Cd."),
    (41.0436, 29.0135, None),
    (41.0442, 29.0175, None),
    (41.0452, 29.0210, None),
    (41.0465, 29.0240, None),
    (41.0478, 29.0258, "Ortaköy-Haarnadel"),
    (41.0490, 29.0245, None),
    (41.0505, 29.0215, "Anstieg (Palanga Cd.)"),
    (41.0520, 29.0185, None),
    (41.0532, 29.0150, "Yıldız-Kuppe"),
    (41.0530, 29.0110, None),
    (41.0515, 29.0085, None),
    (41.0490, 29.0070, "Abfahrt (Yıldız Cd.)"),
    (41.0460, 29.0045, None),
    (41.0440, 29.0020, "Spor Cd. / Akaretler"),
    (41.0415, 28.9990, None),
    (41.0390, 28.9960, None),
    (41.0372, 28.9938, "Kabataş-Haarnadel"),
]


def densify(pts, step=10.0):
    out = []
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        d = math.dist(a, b)
        k = max(1, int(d / step))
        for j in range(k):
            t = j / k
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out


def near_sea(x, y, r=45.0):
    for a in range(0, 360, 30):
        la, lo = dem.latlon(x + r * math.cos(math.radians(a)), y + r * math.sin(math.radians(a)))
        if dem.height(la, lo) <= 0.0:
            return True
    return dem.height(*dem.latlon(x, y)) <= 0.0


def keep_inland(pts):
    """Moves sketch points that touch the DEM sea inland (up the slope), max 200 m."""
    out = []
    for x, y in pts:
        moved = 0
        while near_sea(x, y) and moved < 200:
            gx = dem.height(*dem.latlon(x + 15, y)) - dem.height(*dem.latlon(x - 15, y))
            gy = dem.height(*dem.latlon(x, y + 15)) - dem.height(*dem.latlon(x, y - 15))
            g = math.hypot(gx, gy) or 1.0
            x, y = x + 10 * gx / g, y + 10 * gy / g
            moved += 10
        out.append((x, y))
    return out


def main():
    lat0, lat1, lon0, lon1 = 41.025, 41.062, 28.985, 29.045
    z, (e0, e1, n0, n1) = dem.grid(lat0, lat1, lon0, lon1)
    fig, ax = plt.subplots(figsize=(11, 10))
    ls = LightSource(azdeg=315, altdeg=40)
    land = np.ma.masked_where(z <= 0.0, z)
    ax.imshow(np.where(z <= 0, np.nan, z), extent=(e0, e1, n0, n1), cmap="terrain", vmin=-60, vmax=180, alpha=0.9)
    ax.imshow(ls.hillshade(z.astype(float), vert_exag=3, dx=25, dy=30), extent=(e0, e1, n0, n1), cmap="gray", alpha=0.35)
    ax.imshow(np.where(z <= 0, 1.0, np.nan), extent=(e0, e1, n0, n1), cmap="Blues", vmin=0, vmax=1.6, alpha=0.8)
    ys = np.linspace(n1, n0, z.shape[0])
    xs = np.linspace(e0, e1, z.shape[1])
    ax.contour(xs, ys, z, levels=[0.5], colors="#1f4e79", linewidths=1.2)
    ax.contour(xs, ys, z, levels=list(range(20, 200, 20)), colors="#5a4a3a", linewidths=0.3, alpha=0.6)
    pts = keep_inland([dem.enu(la, lo) for la, lo, _ in CORRIDOR])
    path = densify(pts)
    px, py = zip(*(path + path[:1]))
    ax.plot(px, py, color="#d62728", lw=7, alpha=0.25, solid_capstyle="round")
    ax.plot(px, py, color="#d62728", lw=2.2, ls="--", label="Entwurf Streckenkorridor (Skizze, ±50–100 m)")
    for (la, lo, label), (x, y) in zip(CORRIDOR, pts):
        if label:
            ax.annotate(label, (x, y), fontsize=8, xytext=(6, -10), textcoords="offset points",
                        bbox=dict(boxstyle="round,pad=0.2", fc="white", alpha=0.8, lw=0))
    # Direction arrows.
    for i in range(0, len(path), max(1, len(path) // 14)):
        a, b = path[i], path[(i + 3) % len(path)]
        ax.annotate("", xy=b, xytext=a, arrowprops=dict(arrowstyle="->", color="#a00", lw=1.6))
    for name, la, lo, ok in LANDMARKS:
        x, y = dem.enu(la, lo)
        if not (e0 < x < e1 and n0 < y < n1):
            continue
        ax.scatter([x], [y], s=40, color="#111" if ok else "#ff7f0e", zorder=5, edgecolor="white")
        ax.annotate(name, (x, y), fontsize=8, xytext=(5, 5), textcoords="offset points", color="#111")
    bx = [dem.enu(41.05102, 29.02858), dem.enu(41.03998, 29.04010)]
    ax.plot([bx[0][0], bx[1][0]], [bx[0][1], bx[1][1]], color="#333", lw=3, label="15-Juli-Märtyrerbrücke (geprüfte Endpunkte)")
    # Sightline Ortaköy approach -> mosque + bridge.
    sx, sy = dem.enu(41.0452, 29.0210)
    for la, lo in ((41.04694, 29.02694), (41.0455, 29.0343)):
        tx, ty = dem.enu(la, lo)
        ax.plot([sx, tx], [sy, ty], color="#ffbf00", lw=1.2, ls=":")
    ax.annotate("Sichtachse Moschee + Brücke", (sx, sy), fontsize=8, color="#7a5c00", xytext=(-10, 14), textcoords="offset points")
    ax.set_aspect("equal")
    ax.set_xlim(e0, e1); ax.set_ylim(n0, n1)
    ax.set_xlabel("Ost [m] (Ursprung Dolmabahçe-Palast)"); ax.set_ylabel("Nord [m]  –  geografisch Nord = oben")
    L = sum(math.dist(path[i], path[(i + 1) % len(path)]) for i in range(len(path)))
    prof = [dem.height(*dem.latlon(x, y)) for x, y in path]
    onwater = sum(1 for h in prof if h <= 0.0)
    ax.set_title("Bosphorus GP – ENTWURF Streckenplan (zur Freigabe)\n"
                 f"Gelände/Küste: Copernicus DEM GLO-30 · Korridor: Skizze, noch NICHT an OSM-Straßenachsen ausgerichtet · ≈{L / 1000:.1f} km",
                 fontsize=10)
    ax.legend(loc="lower right", fontsize=8)
    fig.tight_layout()
    fig.savefig(OUT, dpi=110)
    # Elevation profile along the draft (DSM, smoothed over 60 m).
    h = np.array(prof)
    w = 15
    hs = np.convolve(np.concatenate([h[-w:], h, h[:w]]), np.ones(w) / w, mode="same")[w:-w]
    s = np.cumsum([0] + [math.dist(path[i], path[i + 1]) for i in range(len(path) - 1)])
    fig2, ax2 = plt.subplots(figsize=(11, 3.2))
    ax2.plot(s / 1000, h, color="#bbb", lw=0.8, label="DEM roh (inkl. Dächer/Bäume)")
    ax2.plot(s / 1000, hs, color="#d62728", lw=2, label="geglättet (150 m)")
    for (la, lo, label), p in zip(CORRIDOR, pts):
        if label:
            i = min(range(len(path)), key=lambda k: math.dist(path[k], p))
            ax2.axvline(s[i] / 1000, color="#999", lw=0.5)
            ax2.text(s[i] / 1000, hs.max() + 3, label.split(" – ")[-1], rotation=60, fontsize=7, ha="left", va="bottom")
    ax2.set_xlabel("Streckenkilometer (Entwurf)"); ax2.set_ylabel("Höhe [m]")
    ax2.set_ylim(min(0, hs.min() - 5), hs.max() + 45)
    ax2.legend(fontsize=7, loc="upper left")
    fig2.tight_layout()
    fig2.savefig(PROFILE, dpi=110)
    grade = np.diff(hs) / np.maximum(1e-3, np.diff(s))
    print(f"length {L:.0f} m, samples on water {onwater}, height smoothed {hs.min():.0f}..{hs.max():.0f} m, "
          f"max grade {grade.max() * 100:.1f} % up / {grade.min() * 100:.1f} %")


if __name__ == "__main__":
    main()
