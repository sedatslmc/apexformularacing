#!/usr/bin/env python3
"""Geographic plan for the Bosphorus GP environment.

Places verified landmark coordinates (WGS84) and approximate shorelines on the
schematic circuit with ONE rigid transform (rotation + translation, scale 1:1),
so distances, the order along the shore and the bearings between landmarks stay
true; only the compass is rotated relative to the game frame. The transform is
chosen by a grid search so that the track keeps clear of the water, the
waterfront sections run close to it, the Ortaköy crest looks at the mosque and
the bridge, and Dolmabahçe lies next to the start/finish precinct.

Writes a "geo" block (landmarks, European and Asian shorelines in game metres,
rotation, provenance) into the circuit JSON, replaces the schematic shoreline,
recomputes sightlines, and renders docs/img/bosphorus_geo_plan.png.

Usage: python3 tools/bosphorus_geo.py
"""
import json
import math
import os

import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

ROOT = os.path.join(os.path.dirname(__file__), "..")
TRACK = os.path.join(ROOT, "unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_gp.json")
IMG = os.path.join(ROOT, "docs/img/bosphorus_geo_plan.png")

# Landmarks: (id, name, lat, lon, verified, source, kind, height_m)
LANDMARKS = [
    ("dolmabahce", "Dolmabahçe-Palast", 41.03944, 29.00167, True, "Wikipedia (41°02′22″N 29°00′06″E)", "palace", 25),
    ("ortakoy_mosque", "Ortaköy-Moschee (Büyük Mecidiye)", 41.04694, 29.02694, True, "Wikipedia (41°02′49″N 29°01′37″E)", "mosque", 40),
    ("bridge_eu", "15-Juli-Märtyrerbrücke, europäisches Ende", 41.05102, 29.02858, True, "Structurae", "bridge_end", 0),
    ("bridge_as", "15-Juli-Märtyrerbrücke, asiatisches Ende", 41.03998, 29.04010, True, "Structurae", "bridge_end", 0),
    ("galata", "Galata-Turm", 41.02566, 28.97416, True, "Wikipedia (41°1′32″N 28°58′27″E)", "tower", 63),
    ("maidens_tower", "Leanderturm (Kız Kulesi)", 41.02117, 29.00425, True, "Wikipedia (41°01′16″N 29°00′15″E)", "islet_tower", 23),
    ("sultan_ahmed", "Sultan-Ahmed-Moschee", 41.00526, 28.97687, True, "Pleiades/OSM", "mosque_big", 64),
    ("hagia_sophia", "Hagia Sophia", 41.0086, 28.9802, False, "Näherung (nicht geprüft)", "mosque_big", 55),
    ("besiktas_pier", "Beşiktaş-Anleger", 41.0418, 29.0073, False, "Näherung (nicht geprüft)", "pier", 0),
    ("uskudar_pier", "Üsküdar-Anleger", 41.0267, 29.0150, False, "Näherung (nicht geprüft)", "pier", 0),
    ("beylerbeyi", "Beylerbeyi-Palast", 41.0428, 29.0398, False, "Näherung (nicht geprüft)", "palace_small", 15),
    ("ciragan", "Çırağan-Palast", 41.0436, 29.0160, False, "Näherung (nicht geprüft)", "palace_small", 15),
    ("karakoy_pier", "Karaköy-Anleger", 41.0222, 28.9765, False, "Näherung (nicht geprüft)", "pier", 0),
    ("eminonu_pier", "Eminönü-Anleger", 41.0177, 28.9735, False, "Näherung (nicht geprüft)", "pier", 0),
    ("yildiz_park", "Yıldız-Park", 41.0480, 29.0135, False, "Näherung (nicht geprüft)", "park", 0),
    ("levent", "Levent (Hochhaus-Cluster)", 41.0800, 29.0110, False, "Näherung (nicht geprüft)", "skyline", 180),
]

# Approximate shorelines (south -> north), NOT survey data: replace with licensed
# OSM coastline (ODbL, attribution) before final art.
SHORE_EU = [(41.0300, 28.9880), (41.0345, 28.9950), (41.0385, 29.0010), (41.0412, 29.0068), (41.0428, 29.0110),
            (41.0440, 29.0165), (41.0458, 29.0225), (41.0470, 29.0268), (41.0505, 29.0290), (41.0555, 29.0365),
            (41.0620, 29.0410), (41.0690, 29.0440)]
SHORE_AS = [(41.0150, 29.0120), (41.0230, 29.0135), (41.0268, 29.0158), (41.0320, 29.0250), (41.0355, 29.0310),
            (41.0400, 29.0390), (41.0430, 29.0412), (41.0480, 29.0480), (41.0540, 29.0555), (41.0620, 29.0600)]

# Distant land masses for the skyline layer (approximate outlines, NOT survey data).
# The European mass continues SHORE_EU southwards past Tophane/Karaköy to the Golden
# Horn and closes far inland; the Asian mass lies east of SHORE_AS; the historic
# peninsula lies south of the Golden Horn. rise/scale shape the hills (metres).
FAR_LAND = [
    ("europe", 70, 700, [(41.0255, 28.9810), (41.0222, 28.9760), (41.0236, 28.9700), (41.0290, 28.9590),
                         (41.0350, 28.9450)], [(41.0900, 29.0480), (41.0900, 28.9600)]),
    ("asia", 120, 900, None, [(41.0080, 29.0180), (40.9900, 29.0300), (40.9900, 29.1200), (41.0620, 29.1200)]),
    ("peninsula", 45, 320, [(41.0165, 28.9855), (41.0175, 28.9720), (41.0225, 28.9610), (41.0300, 28.9450),
                            (41.0250, 28.9200), (41.0000, 28.9200), (41.0000, 28.9700), (41.0040, 28.9810),
                            (41.0095, 28.9860)], None),
]

LAT0, LON0 = 41.03944, 29.00167  # local origin: Dolmabahçe


def enu(lat, lon):
    """Local tangent plane metres (east, north) around the origin."""
    k = math.cos(math.radians(LAT0))
    return ((lon - LON0) * 111320.0 * k, (lat - LAT0) * 110574.0)


def transform(p, rot_deg, tx, tz):
    """Rotate (counter-clockwise positive: north turns towards west) and translate into the game frame."""
    a = math.radians(rot_deg)
    e, n = p
    x = e * math.cos(a) - n * math.sin(a)
    z = e * math.sin(a) + n * math.cos(a)
    return (x + tx, z + tz)


def densify(points, step=10.0):
    out = []
    for i in range(len(points)):
        a, b = points[i], points[(i + 1) % len(points)]
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(1, int(d / step))
        for k in range(n):
            t = k / n
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out


def seg_dist(p, a, b):
    ax, az = a; bx, bz = b; px, pz = p
    dx, dz = bx - ax, bz - az
    L = dx * dx + dz * dz
    t = 0 if L == 0 else max(0, min(1, ((px - ax) * dx + (pz - az) * dz) / L))
    return math.hypot(px - (ax + t * dx), pz - (az + t * dz))


def poly_dist(p, poly):
    return min(seg_dist(p, poly[i], poly[i + 1]) for i in range(len(poly) - 1))


def poly_dist_np(P, poly):
    """Distance of each point in P (N x 2) to a polyline."""
    best = np.full(len(P), np.inf)
    for i in range(len(poly) - 1):
        a = np.array(poly[i]); b = np.array(poly[i + 1])
        d = b - a
        L = max(float(d @ d), 1e-9)
        t = np.clip(((P - a) @ d) / L, 0, 1)
        q = a + t[:, None] * d
        best = np.minimum(best, np.hypot(*(P - q).T))
    return best


def water_side_np(P, shore):
    s = np.array(shore)
    sx = np.interp(P[:, 1], s[:, 1], s[:, 0])
    return P[:, 0] > sx


def water_side(p, shore):
    """+1 if p lies east of the (monotonic in z) European shore polyline, i.e. in the water."""
    x, z = p
    if z <= shore[0][1]:
        sx = shore[0][0]
    elif z >= shore[-1][1]:
        sx = shore[-1][0]
    else:
        sx = shore[-1][0]
        for i in range(len(shore) - 1):
            if shore[i][1] <= z <= shore[i + 1][1]:
                t = (z - shore[i][1]) / max(shore[i + 1][1] - shore[i][1], 1e-6)
                sx = shore[i][0] + (shore[i + 1][0] - shore[i][0]) * t
                break
    return 1 if x > sx else -1


def evaluate(track, rot, tx, tz, districts):
    shore = [transform(enu(*q), rot, tx, tz) for q in SHORE_EU]
    if any(shore[i + 1][1] <= shore[i][1] for i in range(len(shore) - 1)):
        return 1e9, None  # shoreline must run "north" in the game frame (water east)
    lm = {l[0]: transform(enu(l[2], l[3]), rot, tx, tz) for l in LANDMARKS}
    P = track
    dist = poly_dist_np(P, shore)
    wet = water_side_np(P, shore)
    cost = float(np.sum(wet) * 1e4 + np.sum(dist[wet]) * 100)
    cost += float(np.sum(np.clip(20 - dist, 0, None)) * 50)
    dmin = float(dist.min())
    # Waterfront district B should hug the water (40-90 m).
    b = districts["B Bosporus-Schwung (Ufer)"]
    bz0, bz1 = min(q[1] for q in b), max(q[1] for q in b)
    m = (P[:, 1] >= bz0) & (P[:, 1] <= bz1) & (P[:, 0] > 0)
    if m.any():
        cost += float(np.mean(np.clip(dist[m] - 90, 0, None)) * 20)
    # Start/finish (district A) within ~250 m of the water, Dolmabahçe close by.
    m = (P[:, 1] >= -60) & (P[:, 1] <= 560) & (P[:, 0] >= -150) & (P[:, 0] <= -20)
    if m.any():
        cost += max(0.0, float(dist[m].min()) - 220) * 10
    # Dolmabahçe: on the water, clear of the track (>= 80 m), south of the paddock.
    pal = np.array([lm["dolmabahce"]])
    dtrack = float(np.min(np.hypot(*(P - pal[0]).T)))
    cost += max(0.0, 80 - dtrack) * 50
    # Its ~600 m waterfront facade must fit entirely south of the circuit.
    zmin = float(P[:, 1].min())
    cost += max(0.0, lm["dolmabahce"][1] - (zmin - 340)) * 20
    cost += max(0.0, (zmin - 340) - lm["dolmabahce"][1] - 150) * 5
    # Beşiktaş pier next to the paddock / start-finish precinct.
    px, pz = lm["besiktas_pier"]
    cost += max(0.0, abs(pz - 300) - 300) * 3
    # Ortaköy crest (district C) sees the mosque at 250-900 m and the bridge.
    cx, cz = 60, 1640
    mx, mz = lm["ortakoy_mosque"]
    dm = math.hypot(mx - cx, mz - cz)
    cost += max(0, 250 - dm) * 20 + max(0, dm - 900) * 10
    return cost, (shore, lm, dmin)


def main():
    data = json.load(open(TRACK))
    track = densify([(p[0], p[1]) for p in data["points"]], 8.0)
    P = np.array(track)
    best = (1e18, None, None)
    # Coarse-to-fine search over rotation and translation.
    rots, txs, tzs = range(-90, 91, 6), range(-600, 1001, 60), range(-1600, 801, 60)
    for stage in range(3):
        for rot in rots:
            for tx in txs:
                for tz in tzs:
                    c, info = evaluate(P, rot, tx, tz, data["districts"])
                    if c < best[0]:
                        best = (c, (rot, tx, tz), info)
        r0, x0, z0 = best[1]
        step_r, step_t = (3, 20) if stage == 0 else (1, 5)
        rots = [r0 + k * step_r for k in range(-3, 4)]
        txs = [x0 + k * step_t for k in range(-4, 5)]
        tzs = [z0 + k * step_t for k in range(-4, 5)]
    cost, (rot, tx, tz), (shore, lm, dmin) = best
    asia = [transform(enu(*q), rot, tx, tz) for q in SHORE_AS]
    print(f"rotation {rot}° tx {tx} tz {tz} cost {cost:.1f} min track-water distance {dmin:.0f} m")
    for l in LANDMARKS:
        print(f"  {l[1]:42s} -> ({lm[l[0]][0]:7.0f}, {lm[l[0]][1]:7.0f})  {'geprüft' if l[4] else 'Näherung'}")

    data["shoreline"] = [[round(x, 1), round(z, 1)] for x, z in shore]
    data["geo"] = {
        "note": "Rigid placement of WGS84 landmarks on the schematic circuit (1:1 scale, rotated). "
                "Shorelines are approximations, not survey data.",
        "origin": {"lat": LAT0, "lon": LON0, "name": "Dolmabahçe"},
        "rotation_deg": rot,
        "translation": [tx, tz],
        "true_north_bearing_deg": (-rot) % 360,
        "min_track_water_distance_m": round(dmin, 1),
        "landmarks": [
            {"id": l[0], "name": l[1], "kind": l[6], "height": l[7], "game": [round(lm[l[0]][0], 1), round(lm[l[0]][1], 1)],
             "lat": l[2], "lon": l[3], "verified": l[4], "source": l[5]}
            for l in LANDMARKS
        ],
        "shoreline_asia": [[round(x, 1), round(z, 1)] for x, z in asia],
        "land": [],
    }
    for name, rise, scale, outline, inland in FAR_LAND:
        if name == "europe":
            pts = list(reversed(outline)) + SHORE_EU + inland
        elif name == "asia":
            pts = list(reversed(SHORE_AS)) + inland
        else:
            pts = outline
        poly = [transform(enu(*q), rot, tx, tz) for q in pts]
        data["geo"]["land"].append({"id": name, "rise": rise, "scale": scale,
                                    "polygon": [[round(x, 1), round(z, 1)] for x, z in poly]})
    # Sightlines recomputed from geometry (bearing in the game frame, 0 = +z).
    def bearing(a, b):
        return round(math.degrees(math.atan2(b[0] - a[0], b[1] - a[1])) % 360, 1)
    crest, panorama, hill = (60, 1640), (0, 20), (-400, 2000)
    mid_bridge = ((lm["bridge_eu"][0] + lm["bridge_as"][0]) / 2, (lm["bridge_eu"][1] + lm["bridge_as"][1]) / 2)
    data["sightlines"] = [
        {"label": "Bosporus-Brücke", "from": list(crest), "bearing_deg": bearing(crest, mid_bridge)},
        {"label": "Ortaköy-Moschee (Ufer)", "from": list(crest), "bearing_deg": bearing(crest, lm["ortakoy_mosque"])},
        {"label": "Asiatisches Ufer / Beylerbeyi", "from": [250, 1000], "bearing_deg": bearing((250, 1000), lm["beylerbeyi"])},
        {"label": "Galata / Historische Halbinsel (fern)", "from": list(hill), "bearing_deg": bearing(hill, lm["galata"])},
        {"label": "Brücke + Panorama beim Ausgang", "from": list(panorama), "bearing_deg": bearing(panorama, mid_bridge)},
    ]
    # District F lies on the inland Yıldız slope in this placement, not at a ferry terminal.
    if "F Fährterminal / Steinschikane" in data["districts"]:
        data["districts"]["F Yıldız-Abfahrt / Steinschikane"] = data["districts"].pop("F Fährterminal / Steinschikane")
    names = data.get("corner_names", [])
    for i, n in enumerate(names):
        if n.startswith("Feribot"):
            names[i] = n.replace("Feribot", "Yıldız")
    with open(TRACK, "w") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)

    fig, ax = plt.subplots(figsize=(10, 12))
    tx_, tz_ = zip(*track)
    ax.plot(tx_, tz_, color="#222", lw=2.5, label="Strecke")
    for poly, c, lbl in ((shore, "#1f77b4", "Europäisches Ufer (Näherung)"), (asia, "#2ca02c", "Asiatisches Ufer (Näherung)")):
        xs, zs = zip(*poly)
        ax.plot(xs, zs, color=c, lw=1.5, label=lbl)
    ax.fill_betweenx([p[1] for p in shore], [p[0] for p in shore], [p[0] + 3000 for p in shore], color="#cfe3f3", alpha=0.5)
    for land in data["geo"]["land"]:
        xs, zs = zip(*(land["polygon"] + land["polygon"][:1]))
        ax.fill(xs, zs, color="#e9e2cf", alpha=0.6, zorder=0)
    b0, b1 = lm["bridge_eu"], lm["bridge_as"]
    ax.plot([b0[0], b1[0]], [b0[1], b1[1]], color="#d62728", lw=3, label="15-Juli-Märtyrerbrücke")
    for l in LANDMARKS:
        x, z = lm[l[0]]
        ax.scatter([x], [z], color="#d62728" if l[4] else "#ff7f0e", s=30, zorder=5)
        ax.annotate(l[1] + ("" if l[4] else " (≈)"), (x, z), fontsize=7, xytext=(4, 3), textcoords="offset points")
    for name, poly in data["districts"].items():
        xs, zs = zip(*(poly + [poly[0]]))
        ax.plot(xs, zs, color="#999", lw=0.8, ls="--")
        ax.annotate(name.split(" ")[0], (sum(xs) / len(xs), sum(zs) / len(zs)), color="#666", fontsize=9)
    north = math.radians(-rot)
    ax.annotate("", xy=(-900 + 200 * math.sin(north), 2400 + 200 * math.cos(north)), xytext=(-900, 2400),
                arrowprops=dict(arrowstyle="->", lw=2))
    ax.text(-900 + 230 * math.sin(north), 2400 + 230 * math.cos(north), "N", fontsize=12, ha="center")
    ax.set_aspect("equal")
    ax.set_xlim(-2500, 3500)
    ax.set_ylim(-5000, 4000)
    ax.set_title(f"Bosporus GP – geografischer Plan (Spielkoordinaten, Maßstab 1:1, Nord um {(-rot) % 360:.0f}° gedreht)\n"
                 f"min. Abstand Strecke–Wasser {dmin:.0f} m · rot = geprüfte Koordinaten, orange = Näherung", fontsize=9)
    ax.legend(loc="lower right", fontsize=7)
    fig.tight_layout()
    fig.savefig(IMG, dpi=110)


if __name__ == "__main__":
    main()
