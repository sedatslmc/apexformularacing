#!/usr/bin/env python3
"""Builds the circuit file for approved Variant A (phase 4) from data/geo/track_plan_A.json.

Writes unity/.../Resources/Tracks/bosphorus_a.json in the existing CircuitDefinition
format: points [east, north, elevation, width, run-off left, run-off right] in the unrotated local frame of
tools/geo/dem_tools.py (true north = +z), the drivable height profile (tools/geo/vertical_alignment.py), widths
(11 m, 13 m on Dolmabahçe Cd.), pit lane, sectors, corner names, DEM shorelines and
the verified landmarks. Derived from OSM (ODbL) and Copernicus DEM.
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import dem_tools as dem

ROOT = dem.ROOT
PLAN = os.path.join(ROOT, "data/geo/track_plan_A.json")
OUT = os.path.join(ROOT, "unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_a.json")
LEAD = 300.0       # points start this far before the start/finish line
LIMITS = os.path.join(ROOT, "data/geo/bosphorus_a_limits.csv")   # build/Apex.Tuning: dotnet run -- limits bosphorus_a <csv>
SPACING = 10.0

LANDMARKS = [
    ("dolmabahce", "Dolmabahçe-Palast", "palace", 25, 41.03944, 29.00167, True, "Wikipedia"),
    ("ortakoy_mosque", "Ortaköy-Moschee (Büyük Mecidiye)", "mosque", 40, None, None, True, "OSM-Grundriss (Büyük Mecidiye Camii)"),
    ("bridge_eu", "15-Juli-Märtyrerbrücke, europäisches Ende", "bridge_end", 0, 41.05102, 29.02858, True, "Structurae, OSM ±20 m"),
    ("bridge_as", "15-Juli-Märtyrerbrücke, asiatisches Ende", "bridge_end", 0, 41.03998, 29.04010, True, "Structurae"),
    ("galata", "Galata-Turm", "tower", 63, 41.02566, 28.97416, True, "Wikipedia"),
    ("maidens_tower", "Leanderturm (Kız Kulesi)", "islet_tower", 23, 41.02117, 29.00425, True, "Wikipedia"),
    ("sultan_ahmed", "Sultan-Ahmed-Moschee", "mosque_big", 64, 41.00526, 28.97687, True, "Pleiades/OSM"),
    ("ciragan", "Çırağan-Palast", "palace_small", 15, None, None, True, "OSM-Grundriss"),
    ("hagia_sophia", "Hagia Sophia", "mosque_big", 55, 41.0086, 28.9802, False, "Näherung"),
    ("beylerbeyi", "Beylerbeyi-Palast", "palace_small", 15, 41.0428, 29.0398, False, "Näherung"),
    ("besiktas_pier", "Beşiktaş-Anleger", "pier", 0, 41.0418, 29.0073, True, "OSM (Fähranleger)"),
    ("uskudar_pier", "Üsküdar-Anleger", "pier", 0, 41.0267, 29.0150, True, "OSM (Fähranleger)"),
    ("karakoy_pier", "Karaköy-Anleger", "pier", 0, 41.0222, 28.9765, True, "OSM (Fähranleger)"),
    ("eminonu_pier", "Eminönü-Anleger", "pier", 0, 41.0177, 28.9735, True, "OSM (Fähranleger)"),
    ("yildiz_park", "Yıldız-Park", "park", 0, 41.0480, 29.0135, False, "Näherung"),
    ("levent", "Levent (Hochhaus-Cluster)", "skyline", 180, 41.0800, 29.0110, False, "Näherung"),
]
OSM_POS = {"ortakoy_mosque": (2108.1, 873.4), "ciragan": (1153.7, 437.6),
           # Ferry piers from OSM (Şehir Hatları / Turyol terminals).
           "besiktas_pier": (475.0, 189.0), "uskudar_pier": (1143.0, -1249.0),
           "karakoy_pier": (-2096.0, -1992.0), "eminonu_pier": (-2133.0, -2467.0)}
# Documented game adaptations of the corner radius (lap distance ranges, metres): the
# S-bend on the Yıldız ridge in front of the Balmumcu braking zone is straightened slightly.
EASE = [(3360.0, 3740.0, 6)]
# Street-circuit barriers: run-off (m) from the track edge to the wall, left/right.
RUNOFF_DEFAULT = 1.5
# Beşiktaş Cd.: outgoing and return legs on the two carriageways (lap ranges), 9.5 m each,
# a shared median wall on their left.
# Where the two legs run side by side (centrelines < 16 m apart: 250–460 and 5313–5519 m true lap
# distance), plus 20 m for the transitions.
PARALLEL = [(230.0, 480.0), (5293.0, 5540.0)]
PARALLEL_WIDTH = 9.5
# Escape roads on the outside of the heavy braking zones: (from, to, side, metres).
ESCAPES = [(1950.0, 2200.0, "right", 12.0),   # T1 Ortaköy
           (3600.0, 3800.0, "right", 10.0),   # T5 Balmumcu
           (5150.0, 5290.0, "left", 8.0),     # T6 Beşiktaş (ends where the other carriageway begins)
           (6100.0, 6280.0, "right", 8.0),    # T8 Maçka
           (6760.0, 6880.0, "right", 6.0),    # T10 Bayıldım
           (7050.0, 7200.0, "right", 6.0)]    # T11 Kabataş
# Event names for the 13 approved corners (brief: Ortaköy Hairpin, Palanga Climb, Yıldız Crest,
# Barbaros Descent, Akaretler Esses, Bayıldım Hairpin, Kabataş Return), Turkish in game.
CORNER_NAMES = ["Ortaköy Virajı", "Palanga 1", "Palanga 2", "Yıldız Sırtı", "Balmumcu", "Barbaros İnişi", "Akaretler 1",
                "Akaretler 2", "Bayıldım 1", "Bayıldım Virajı", "Kabataş Dönüşü", "Saray 1", "Saray 2"]


def shoreline(x_from, x_to, step, zs):
    """DEM coastline: first x (scanning from x_from towards x_to) with 3 consecutive sea samples."""
    out = []
    for z in zs:
        run = 0
        x = x_from
        found = None
        while (x <= x_to) if step > 0 else (x >= x_to):
            if dem.height(*dem.latlon(x, z)) <= 0.0:
                run += 1
                if run == 3:
                    found = x - 2 * step
                    break
            else:
                run = 0
            x += step
        if found is not None:
            out.append([round(found, 1), z])
    return out


def height_grid(cell=100.0):
    """Street-level terrain on a coarse grid: low percentile of the DEM per cell (roofs and
    tree crowns excluded as far as possible); sea cells are 0."""
    import numpy as np
    lat0, lat1, lon0, lon1 = 40.995, 41.095, 28.935, 29.095
    z, (e0, e1, n0, n1) = dem.grid(lat0, lat1, lon0, lon1)
    # Exclude roof pixels (OSM building footprints) from the terrain estimate.
    roof = np.concatenate([dem.building_mask(28), dem.building_mask(29)], axis=1)
    r0, r1 = int((42 - lat1) * 3600), int((42 - lat0) * 3600)
    c0, c1 = int((lon0 - 28) * 3600), int((lon1 - 28) * 3600)
    z = np.where(roof[r0:r1, c0:c1], -1.0, z)
    nx, nz = int((e1 - e0) // cell), int((n1 - n0) // cell)
    ys = np.linspace(n1, n0, z.shape[0])
    xs = np.linspace(e0, e1, z.shape[1])
    h = []
    for j in range(nz + 1):
        cn = n0 + j * cell
        rows = np.where(np.abs(ys - cn) <= cell * 0.6)[0]
        for i in range(nx + 1):
            ce = e0 + i * cell
            cols = np.where(np.abs(xs - ce) <= cell * 0.6)[0]
            v = z[np.ix_(rows, cols)].ravel() if len(rows) and len(cols) else np.array([0.0])
            land = v[v > 0.0]
            sea = (v == 0.0).sum()
            h.append(round(float(np.percentile(land, 10)), 1) if land.size and sea < v.size * 0.7 else 0.0)
    return {"origin": [round(e0, 1), round(n0, 1)], "cell": cell, "nx": nx + 1, "nz": nz + 1, "h": h,
            "source": "Copernicus DEM GLO-30, 5th percentile per cell (street level)"}


def main():
    plan = json.load(open(PLAN))
    pts = plan["points"]                       # every 10 m from the start line (5 m resample, [::2])
    prof_s = plan["profile"]["s"]
    n = len(pts)
    # Rotate so the file starts LEAD metres before the line.
    k = int(round(LEAD / SPACING))
    order = list(range(n - k, n)) + list(range(0, n - k))
    rows = []
    dolmabahce = [w for w in plan["width_sections"] if w["street"] == "Dolmabahçe Caddesi"]

    import numpy as np
    arr = np.array(pts, float)
    for a, b, w in EASE:
        i0, i1 = int(a / SPACING), int(b / SPACING)
        for _ in range(8):
            sm = arr.copy()
            for i in range(i0, i1):
                sm[i] = arr[i - w:i + w + 1].mean(axis=0)
            # Blend at the ends so the eased part joins smoothly.
            for i in range(i0, i1):
                t = min(1.0, (i - i0) / w, (i1 - i) / w)
                arr[i] = arr[i] * (1 - t) + sm[i] * t
    pts = arr.tolist()
    # Lap distance of each point = true arc length from the start line (the points are ~9.7 m apart,
    # not exactly SPACING; using idx * SPACING shifted heights and widths by up to ~200 m).
    arc = [0.0]
    for i in range(1, n):
        arc.append(arc[-1] + math.dist(pts[i - 1], pts[i]))
    L = arc[-1] + math.dist(pts[-1], pts[0])
    # Drivable height profile (vertical alignment) from the street-level DEM profile and the vehicle limits.
    import vertical_alignment as va
    print("vertical alignment:")
    # Side-by-side legs (Beşiktaş Cd.): each point in a PARALLEL range is tied to the nearest point of the
    # other leg (centrelines < 20 m apart) so both carriageways have one height.
    pairs = []
    for i in range(n):
        if not any(a <= arc[i] <= b for a, b in PARALLEL[:1]):
            continue
        best, bj = 1e9, -1
        for j in range(n):
            along = abs(arc[i] - arc[j])
            if min(along, L - along) < 300:
                continue
            dd = math.dist(pts[i], pts[j])
            if dd < best:
                best, bj = dd, j
        if best < 20.0:
            # Foot of the perpendicular on the other leg (between bj and its neighbours).
            pa, pb = pts[bj], pts[(bj + 1) % n]
            seg = math.dist(pa, pb)
            t = max(0.0, min(1.0, ((pts[i][0] - pa[0]) * (pb[0] - pa[0]) + (pts[i][1] - pa[1]) * (pb[1] - pa[1])) / max(seg * seg, 1e-9)))
            pairs.append((arc[i], (arc[bj] + t * seg) % L))
    grid_s, grid_h, _ = va.design(prof_s, plan["profile"]["street"], L, LIMITS, pairs)
    heights = va.evaluate(grid_s, grid_h, L, np.array(arc))
    # Raw (file) distance of the start line: arc length of the k points before it.
    lead = L - arc[n - k]
    for idx in order:
        s = arc[idx]
        width = 11.0
        for w in dolmabahce:
            if w["from"] - 5 <= s <= w["to"] + 5:
                width = 13.0
        left = right = RUNOFF_DEFAULT
        for a, b in PARALLEL:
            if a <= s <= b:
                width = PARALLEL_WIDTH
                left = 0.5
        for a, b, side, m in ESCAPES:
            if a <= s <= b:
                ramp = min(1.0, (s - a) / 40.0, (b - s) / 20.0)
                v = RUNOFF_DEFAULT + (m - RUNOFF_DEFAULT) * max(0.0, ramp)
                if side == "right":
                    right = max(right, v)
                else:
                    left = max(left, v)
        x, z = pts[idx]
        rows.append([round(x, 2), round(z, 2), round(float(heights[idx]), 5), width, round(left, 2), round(right, 2)])
    sec = [round(((b + lead) % L) / L, 4) for b in plan["sectors"]]
    data = {
        "id": "bosphorus_a",
        "name": "Bosphorus Grand Prix",
        "description": "Temporär gesperrter Stadtkurs in Istanbul entlang realer Straßen (Variante A): Dolmabahçe, Beşiktaş, "
                       "Çırağan, Ortaköy, Palanga, Yıldız, Barbaros, Akaretler, Bayıldım, Kabataş. Straßenachsen © OpenStreetMap-Mitwirkende "
                       "(ODbL), Gelände Copernicus DEM. Breiten und Kreuzungen für das Rennen angepasst.",
        "direction": "counter_clockwise",
        "identity": "bosphorus",
        "default_width": 11.0,
        "sector_fractions": sorted(sec),
        "start_line_distance": round(lead, 2),
        "points": rows,
        "pit": {"side": -1, "entry": round(lead - 170), "exit": round(lead + 230), "box_start": round(lead - 90),
                "box_spacing": 9, "speed_limit_kmh": 80},
        "corner_names": CORNER_NAMES,
        # Approved corners: raw distance of the apex (file distance = lap distance + LEAD) and direction.
        "corners": [{"name": CORNER_NAMES[i], "distance": round((c["s"] + lead) % L, 1), "right": c["dir"] == "rechts",
                     "angle": c["angle"]} for i, c in enumerate(plan["corners"])],
        "sightlines": [],
        "shoreline": shoreline(-1500.0, 2600.0, 10.0, list(range(-900, 2601, 50))),
        "geo": {
            "note": "Unrotated local frame around Dolmabahçe (true north = +z); street axes from OSM, heights from Copernicus DEM.",
            "origin": {"lat": dem.LAT0, "lon": dem.LON0, "name": "Dolmabahçe"},
            "rotation_deg": 0, "translation": [0, 0], "true_north_bearing_deg": 0,
            "landmarks": [],
            "shoreline_asia": shoreline(4200.0, 1500.0, -10.0, list(range(-900, 2601, 50))),
        },
    }
    for lid, name, kind, h, la, lo, ok, src in LANDMARKS:
        e, nn = OSM_POS[lid] if lid in OSM_POS else dem.enu(la, lo)
        data["geo"]["landmarks"].append({"id": lid, "name": name, "kind": kind, "height": h, "game": [round(e, 1), round(nn, 1)],
                                          "verified": ok, "source": src})
    # Monumental Turkish flag on the Yıldız slope above the Bosphorus straight: site chosen by
    # `cd build/Apex.Tuning && dotnet run -c Release -- flagsite bosphorus_a` (MonumentalFlagSite:
    # on land, clear of every track part, best terrain visibility from the lap). Game frame (east, north).
    data["monumental_flag"] = {"game": [903.3, 786.5], "source": "MonumentalFlagSite (Projektentscheidung, kein realer Standort)"}
    # Signature sightline: Çırağan Cd. before Ortaköy towards mosque and bridge.
    bridge_mid = dem.enu(41.0455, 29.0343)
    for label, frm, tgt in (("Ortaköy-Moschee", (1500, 720), OSM_POS["ortakoy_mosque"]),
                            ("Bosporusbrücke vom Palanga-Anstieg", (1300, 1300), bridge_mid),
                            ("Bosporusbrücke von der Yıldız-Kuppe", (1000, 1615), bridge_mid)):
        b = math.degrees(math.atan2(tgt[0] - frm[0], tgt[1] - frm[1])) % 360
        data["sightlines"].append({"label": label, "from": list(frm), "bearing_deg": round(b, 1)})
    # Land masses (DEM coastline) and a coarse street-level height grid (Copernicus DEM).
    import land_masses
    masses = land_masses.classified((0.0, 150.0), dem.enu(41.03998, 29.044), dem.enu(41.00526, 28.97687))
    seen = set()
    data["geo"]["land"] = []
    for m in masses:
        key = tuple(map(tuple, m["polygon"][:5]))
        if key in seen:
            continue          # the historic peninsula joins the European side beyond the Golden Horn
        seen.add(key)
        data["geo"]["land"].append(m)
    data["geo"]["height_grid"] = height_grid()
    json.dump(data, open(OUT, "w"), ensure_ascii=False, indent=1)
    print(f"{OUT}: {len(rows)} points, length ≈ {L} m, sectors {data['sector_fractions']}, shore {len(data['shoreline'])} / {len(data['geo']['shoreline_asia'])}")


if __name__ == "__main__":
    main()
