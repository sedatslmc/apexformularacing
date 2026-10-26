#!/usr/bin/env python3
"""Detailed analysis of the routed circuit variant (phase 3): smoothing, corners, sectors,
pit lane, widths, street-level height profile, sightlines (DEM line of sight) and the
plan figures for approval. Reads data/geo/track_variants.json (route_variants.py).

Usage: python3 tools/geo/track_analysis.py [variant]
"""
import json
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

ROOT = dem.ROOT
VARIANTS = os.path.join(ROOT, "data/geo/track_variants.json")
OSM = os.path.join(ROOT, "data/geo/osm/bosphorus_osm.json")
OUT_JSON = os.path.join(ROOT, "data/geo/track_plan_A.json")
IMG_MAP = os.path.join(ROOT, "docs/img/bosphorus_plan_A_map.png")
IMG_PROFILE = os.path.join(ROOT, "docs/img/bosphorus_plan_A_profile.png")

STEP = 5.0
RACE_WIDTH = 11.0          # target racing width for the game (m)
START_OFFSET = 200.0       # start/finish line this far after the first route point (mid pit straight)

# Landmark targets for sightlines: (name, east, north, height above ground of the visible top).
TARGETS = [
    ("Ortaköy-Moschee", 2108.1, 873.4, 30.0),
    ("Bosporusbrücke (Pylon EU)", None, None, 165.0),
    ("Bosporusbrücke (Fahrbahn Mitte)", None, None, 64.0),
    ("Dolmabahçe-Palast (Muayede-Saal)", -90.0, -52.6, 30.0),
    ("Çırağan-Palast", 1153.7, 437.6, 18.0),
]


def resample(points, step):
    pts = [np.array(p, float) for p in points]
    out = [pts[0]]
    carry = 0.0
    for a, b in zip(pts, pts[1:]):
        d = np.linalg.norm(b - a)
        if d < 1e-6:
            continue
        t = (step - carry) / d
        while t <= 1.0:
            out.append(a + (b - a) * t)
            t += step / d
        carry = (1.0 - (t - step / d)) * d
    return np.array(out)


def smooth_closed(p, passes=4, w=5):
    q = p.copy()
    for _ in range(passes):
        acc = np.zeros_like(q)
        for k in range(-w, w + 1):
            acc += np.roll(q, k, axis=0)
        q = acc / (2 * w + 1)
    return q


def curvature(p):
    t = np.roll(p, -1, axis=0) - np.roll(p, 1, axis=0)
    h = np.arctan2(t[:, 0], t[:, 1])                    # heading, 0 = north, clockwise
    dh = np.angle(np.exp(1j * (np.roll(h, -1) - np.roll(h, 1))))
    ds = np.linalg.norm(np.roll(p, -1, axis=0) - np.roll(p, 1, axis=0), axis=1)
    return h, dh / np.maximum(ds, 1e-6)                  # + = turning right (clockwise)


def corners(k, s, min_angle=18.0, thresh=1.0 / 220.0):
    n = len(k)
    mask = np.abs(k) > thresh
    found, i = [], 0
    # Start scanning at a straight sample so a corner is not split by the wrap.
    start = int(np.argmin(np.abs(k)))
    idx = [(start + j) % n for j in range(n)]
    cur = []
    for j in idx + [idx[0]]:
        if mask[j] and (not cur or np.sign(k[j]) == np.sign(k[cur[-1]])):
            cur.append(j)
        else:
            if cur:
                ang = math.degrees(abs(np.sum(k[cur]) * STEP))
                if ang >= min_angle:
                    apex = cur[int(np.argmax(np.abs(k[cur])))]
                    found.append({"apex": apex, "start": cur[0], "end": cur[-1], "angle": ang,
                                  "radius": 1.0 / abs(k[apex]), "right": bool(k[apex] > 0)})
                cur = []
            if mask[j]:
                cur = [j]
    found.sort(key=lambda c: s[c["apex"]])
    # Merge corners of the same direction closer than 40 m (one complex).
    merged = []
    for c in found:
        if merged and merged[-1]["right"] == c["right"] and s[c["start"]] - s[merged[-1]["end"]] < 40:
            m = merged[-1]
            m["end"] = c["end"]; m["angle"] += c["angle"]
            if c["radius"] < m["radius"]:
                m["radius"], m["apex"] = c["radius"], c["apex"]
        else:
            merged.append(c)
    return merged


def corner_speed(radius, a_lat=28.0):
    """Rough cornering speed (m/s) for a downforce car in the balanced simcade model."""
    return min(95.0, math.sqrt(a_lat * radius) * (1.0 + min(radius, 300.0) / 600.0))


def line_of_sight(eye, eye_h, target, target_h, skip_near=40.0, skip_far=60.0):
    d = math.dist(eye, target)
    n = max(10, int(d / 15.0))
    worst = 1e9
    for i in range(1, n):
        t = i / n
        if t * d < skip_near or (1 - t) * d < skip_far:
            continue
        x = eye[0] + (target[0] - eye[0]) * t
        y = eye[1] + (target[1] - eye[1]) * t
        line = eye_h + (target_h - eye_h) * t
        worst = min(worst, line - dem.height(*dem.latlon(x, y)))
    return worst > 0.0


def main():
    name = sys.argv[1] if len(sys.argv) > 1 else "A-Barbaros"
    var = json.load(open(VARIANTS))[name]
    raw = var["points"][:-1]
    p = resample(raw + [raw[0]], STEP)[:-1]
    # Start at the start/finish line (START_OFFSET along the route).
    shift = int(START_OFFSET / STEP)
    p = np.roll(p, -shift, axis=0)
    ps = smooth_closed(p)
    n = len(ps)
    seg = np.linalg.norm(np.roll(ps, -1, axis=0) - ps, axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)[:-1]])
    L = float(np.sum(seg))
    heading, k = curvature(ps)
    cs = corners(k, s)
    # Street-level profile on the smoothed line, then a game profile limited to 12 %.
    hs_raw = np.array([dem.ground_height(x, y) for x, y in ps])
    acc = hs_raw.copy()
    for _ in range(3):
        acc = np.mean([np.roll(acc, j) for j in range(-12, 13)], axis=0)   # ~125 m window
    h_street = acc
    grade = (np.roll(h_street, -8) - np.roll(h_street, 8)) / (16 * STEP)
    h_game = h_street.copy()
    for _ in range(200):
        g = (np.roll(h_game, -1) - h_game) / STEP
        if np.max(np.abs(g)) <= 0.12:
            break
        h_game = 0.5 * h_game + 0.25 * (np.roll(h_game, 1) + np.roll(h_game, -1))
    game_grade = (np.roll(h_game, -8) - np.roll(h_game, 8)) / (16 * STEP)

    # Street names and OSM widths along the line (nearest route edge by projection).
    edges = []
    rp = np.array(raw + [raw[0]], float)
    sv = json.load(open(VARIANTS))[name]["streets"]
    # Street, OSM width and lanes per sample from the routed edges (nearest raw route point).
    segs = var["segments"]
    rp_xy = np.array(raw, float)
    names, widths, lanes, oneway = [], [], [], []
    for x, y in ps:
        j = int(np.argmin(np.hypot(rp_xy[:, 0] - x, rp_xy[:, 1] - y)))
        sg = segs[min(j, len(segs) - 1)]
        names.append(sg["name"])
        widths.append(sg["width"])
        lanes.append(sg["lanes"])
        oneway.append(sg["oneway"])
    osm = json.load(open(OSM))

    # Sectors: S1 ends after the Ortaköy corner, S2 at the bottom of Barbaros (Beşiktaş).
    def nearest_s(e, nn):
        return float(s[int(np.argmin(np.hypot(ps[:, 0] - e, ps[:, 1] - nn)))])
    s1 = nearest_s(1650, 1080)
    s2 = nearest_s(430, 320)
    sectors = [0.0, s1, s2]

    # Braking zones and overtaking: corners after long full-throttle stretches.
    info = []
    prev_end = s[cs[-1]["end"]] - L if cs else 0.0
    for i, c in enumerate(cs):
        straight = s[c["start"]] - prev_end
        v_c = corner_speed(c["radius"])
        v_in = min(95.0, math.sqrt(v_c ** 2 + 2 * 14.0 * max(0.0, straight)))
        brake = max(0.0, (v_in ** 2 - v_c ** 2) / (2 * 38.0))
        info.append({
            "nr": i + 1, "s": round(float(s[c["apex"]])), "street": names[c["apex"]], "dir": "rechts" if c["right"] else "links",
            "angle": round(float(c["angle"])), "radius": round(float(c["radius"])), "v_kmh": round(v_c * 3.6), "approach_m": round(float(straight)),
            "brake_m": round(float(brake)), "v_in_kmh": round(float(v_in) * 3.6), "overtake": bool(straight > 550 and v_in - v_c > 30),
            "pos": [round(float(ps[c["apex"]][0]), 1), round(float(ps[c["apex"]][1]), 1)],
        })
        prev_end = s[c["end"]]

    # Sightlines from the track (every 20 m), eye 1.0 m above street level.
    br_eu = dem.enu(41.05102, 29.02858)
    br_as = dem.enu(41.03998, 29.04010)
    tower = (br_eu[0] + (br_as[0] - br_eu[0]) * 231 / 1560, br_eu[1] + (br_as[1] - br_eu[1]) * 231 / 1560)
    mid = ((br_eu[0] + br_as[0]) / 2, (br_eu[1] + br_as[1]) / 2)
    sights = {}
    for tname, te, tn, th in TARGETS:
        if "Pylon" in tname:
            te, tn, base = tower[0], tower[1], 0.0
        elif "Fahrbahn" in tname:
            te, tn, base = mid[0], mid[1], 0.0
        else:
            base = dem.ground_height(te, tn)
        vis = []
        for i in range(0, n, 4):
            eye = (ps[i][0], ps[i][1])
            if math.dist(eye, (te, tn)) > 4000:
                vis.append(False)
                continue
            vis.append(line_of_sight(eye, h_street[i] + 1.0, (te, tn), base + th))
        ranges, startv = [], None
        for j, v in enumerate(vis + [False]):
            if v and startv is None:
                startv = j
            if not v and startv is not None:
                if (j - startv) * 4 * STEP >= 60:
                    ranges.append([round(float(s[startv * 4])), round(float(s[min(n - 1, (j - 1) * 4)]))])
                startv = None
        sights[tname] = {"ranges": ranges, "visible_m": sum(b - a for a, b in ranges)}

    # Pit lane: left of the start straight from 170 m before to 230 m after the line.
    pit = {"side": "links (landseitig)", "entry": round(L - 170.0), "exit": 230, "length": 400,
           "boxes": "20 Boxen à 9 m auf ≈ 180 m (Garagen in temporären Bauten)"}

    out = {"variant": name, "length": round(L), "corners": info, "sectors": [round(x) for x in sectors],
           "pit": pit, "sightlines": sights,
           "height": {"street_min": round(float(h_street.min()), 1), "street_max": round(float(h_street.max()), 1),
                      "max_grade_street": round(float(np.max(np.abs(grade))) * 100, 1),
                      "max_grade_game": round(float(np.max(np.abs(game_grade))) * 100, 1)},
           "points": [[round(float(x), 2), round(float(y), 2)] for x, y in ps[::2]],
           "profile": {"s": [round(float(x)) for x in s[::4]], "street": [round(float(x), 1) for x in h_street[::4]],
                       "game": [round(float(x), 1) for x in h_game[::4]]},
           "width_sections": []}
    # Width sections (merge consecutive samples by street).
    cur = None
    for i in range(n):
        if cur and cur["street"] == names[i]:
            cur["to"] = float(s[i]); cur["w"].append(widths[i]); cur["ow"] = cur["ow"] or oneway[i]
        else:
            if cur:
                out["width_sections"].append(cur)
            cur = {"street": names[i], "from": float(s[i]), "to": float(s[i]), "w": [widths[i]], "ow": oneway[i]}
    out["width_sections"].append(cur)
    ws = []
    for w in out["width_sections"]:
        if w["to"] - w["from"] < 30:
            continue
        wmin = float(np.median(w["w"]))
        if ws and ws[-1]["street"] == w["street"] and w["from"] - ws[-1]["to"] < 80:
            ws[-1]["to"] = round(w["to"]); ws[-1]["osm_width"] = round(min(ws[-1]["osm_width"], wmin), 1)
        else:
            ws.append({"street": w["street"], "from": round(w["from"]), "to": round(w["to"]), "osm_width": round(wmin, 1),
                       "carriageway": "Richtungsfahrbahn" if w["ow"] else "beide Richtungen"})
    for w in ws:
        wmin = w["osm_width"]
        w["adaptation"] = "unverändert" if wmin >= RACE_WIDTH - 0.5 else f"auf {RACE_WIDTH:.0f} m verbreitern (+{RACE_WIDTH - wmin:.1f} m)"
    out["width_sections"] = ws
    json.dump(out, open(OUT_JSON, "w"), ensure_ascii=False, indent=1)

    # ---- Figures ----
    e0, e1, n0, n1 = -800, 2400, -350, 2000
    la0, lo0 = dem.latlon(e0, n0)
    la1, lo1 = dem.latlon(e1, n1)
    z, (ge0, ge1, gn0, gn1) = dem.grid(la0 - 0.003, la1 + 0.003, lo0 - 0.003, lo1 + 0.003)
    fig, ax = plt.subplots(figsize=(15, 11.5))
    ls = LightSource(azdeg=315, altdeg=40)
    ax.imshow(np.where(z <= 0, np.nan, z), extent=(ge0, ge1, gn0, gn1), cmap="terrain", vmin=-60, vmax=180, alpha=0.55)
    ax.imshow(ls.hillshade(z.astype(float), vert_exag=3, dx=25, dy=30), extent=(ge0, ge1, gn0, gn1), cmap="gray", alpha=0.25)
    ax.imshow(np.where(z <= 0, 1.0, np.nan), extent=(ge0, ge1, gn0, gn1), cmap="Blues", vmin=0, vmax=1.6, alpha=0.75)
    for b in osm["buildings"]:
        o = b["outline"]
        if len(o) > 2 and e0 < o[0][0] < e1 and n0 < o[0][1] < n1:
            xs, ys = zip(*o)
            ax.fill(xs, ys, color="#8a8170", alpha=0.35, lw=0)
    for st in osm["streets"]:
        q = st["points"]
        if len(q) > 1 and st["highway"] not in ("footway", "path", "steps", "cycleway", "service", "track", "pedestrian", "corridor"):
            xs, ys = zip(*[(a[0], a[1]) for a in q])
            ax.plot(xs, ys, color="#ffffff", lw=0.6, alpha=0.8)
    cols = ["#d62728", "#1f77b4", "#2ca02c"]
    bounds = sectors + [L]
    for i in range(3):
        m = (s >= bounds[i]) & (s < bounds[i + 1])
        ax.plot(ps[m, 0], ps[m, 1], color=cols[i], lw=4, solid_capstyle="round", label=f"Sektor {i + 1}")
    for i in range(0, n, n // 24):
        a, b = ps[i], ps[(i + 6) % n]
        ax.annotate("", xy=b, xytext=a, arrowprops=dict(arrowstyle="-|>", color="black", lw=1.2))
    for c in info:
        x, y = c["pos"]
        ax.scatter([x], [y], s=150, color="white", edgecolor="black", zorder=6)
        ax.text(x, y, str(c["nr"]), ha="center", va="center", fontsize=8, weight="bold", zorder=7)
    # Start/finish and pit lane.
    sf = ps[0]
    t = ps[1] - ps[0]; t /= np.linalg.norm(t)
    left = np.array([-t[1], t[0]])
    ax.plot([sf[0] - left[0] * 12, sf[0] + left[0] * 12], [sf[1] - left[1] * 12, sf[1] + left[1] * 12], color="black", lw=4)
    ax.annotate("Start/Ziel", sf, xytext=(-80, -40), textcoords="offset points", fontsize=10, weight="bold",
                arrowprops=dict(arrowstyle="->"))
    pm = (s >= L - 170) | (s <= 230)
    lat = []
    for i in np.where(pm)[0]:
        tt = ps[(i + 1) % n] - ps[i - 1]; tt /= np.linalg.norm(tt)
        lat.append(ps[i] + np.array([-tt[1], tt[0]]) * 18)
    lat = np.array(lat)
    order = np.argsort(np.where(s[pm] > L / 2, s[pm] - L, s[pm]))
    ax.plot(lat[order, 0], lat[order, 1], color="#ff9f1c", lw=3, label="Boxengasse (landseitig, ≈400 m)")
    # Landmarks.
    for tname, te, tn, th in TARGETS:
        if te is None:
            continue
        ax.scatter([te], [tn], marker="*", s=180, color="#ffbf00", edgecolor="black", zorder=8)
        ax.annotate(tname, (te, tn), fontsize=8, xytext=(6, -12), textcoords="offset points")
    ax.plot([br_eu[0], br_as[0]], [br_eu[1], br_as[1]], color="#333", lw=3, label="15-Juli-Märtyrerbrücke")
    ax.set_xlim(e0, e1); ax.set_ylim(n0, n1); ax.set_aspect("equal")
    ax.set_xlabel("Ost [m] (Ursprung Dolmabahçe, 41,03944 N / 29,00167 O)"); ax.set_ylabel("Nord [m] – geografisch Nord = oben")
    ax.set_title(f"Bosphorus GP – Variante A (zur Freigabe): {L / 1000:.2f} km, {len(info)} Kurven, gegen den Uhrzeigersinn\n"
                 "Linie = OSM-Straßenachsen (geglättet) · Gelände: Copernicus DEM · Gebäude/Straßen © OpenStreetMap-Mitwirkende", fontsize=10)
    ax.legend(loc="lower right", fontsize=8)
    fig.tight_layout()
    fig.savefig(IMG_MAP, dpi=105)

    fig2, (a1, a2) = plt.subplots(2, 1, figsize=(15, 5.5), sharex=True, gridspec_kw={"height_ratios": [3, 1.4]})
    a1.plot(s / 1000, hs_raw, color="#ccc", lw=0.6, label="DEM Straßenniveau (Rohwert)")
    a1.plot(s / 1000, h_street, color="#555", lw=1.5, label="Straße geglättet")
    a1.plot(s / 1000, h_game, color="#d62728", lw=2, label="Spielprofil (≤ 12 %)")
    for c in info:
        a1.axvline(c["s"] / 1000, color="#bbb", lw=0.5)
        a1.text(c["s"] / 1000, h_game.max() + 6, str(c["nr"]), ha="center", fontsize=7)
    for b in sectors[1:]:
        a1.axvline(b / 1000, color="black", lw=1, ls="--")
    a1.set_ylabel("Höhe [m]")
    a1.legend(fontsize=7, loc="upper left")
    a2.plot(s / 1000, grade * 100, color="#555", lw=1, label="Steigung Straße")
    a2.plot(s / 1000, game_grade * 100, color="#d62728", lw=1.5, label="Steigung Spiel")
    a2.axhline(12, color="#d62728", ls=":", lw=0.8); a2.axhline(-12, color="#d62728", ls=":", lw=0.8)
    a2.set_ylabel("%"); a2.set_xlabel("Streckenkilometer ab Start/Ziel")
    a2.legend(fontsize=7, loc="upper left")
    fig2.tight_layout()
    fig2.savefig(IMG_PROFILE, dpi=105)

    print(f"{name}: {L:.0f} m, {len(info)} corners, sectors {[round(x) for x in sectors]}, "
          f"street grade max {out['height']['max_grade_street']} %, game {out['height']['max_grade_game']} %")
    for c in info:
        print(f"  T{c['nr']:2d} {c['s']:5d} m {c['street'][:26]:26s} {c['dir']:6s} {c['angle']:4d}° R{c['radius']:4d} "
              f"~{c['v_kmh']:3d} km/h  Anlauf {c['approach_m']:4d} m  Bremsweg {c['brake_m']:3d} m {'ÜBERHOLEN' if c['overtake'] else ''}")
    for tname, v in sights.items():
        print(f"  Sicht {tname}: {v['visible_m']} m {v['ranges'][:6]}")
    for w in ws:
        print(f"  {w['street'][:28]:28s} {w['from']:5d}-{w['to']:5d} m  OSM {w['osm_width']:4.1f} m ({w['carriageway']}) -> {w['adaptation']}")


if __name__ == "__main__":
    main()
