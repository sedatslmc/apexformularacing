#!/usr/bin/env python3
"""Engine-agnostic circuit layout report: length, corners (auto-detected from
curvature), handedness, sectors, clearance, elevation profile and a schematic
plot with districts and landmark sightlines.

Usage: python3 tools/circuit_report.py unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_gp.json docs/img/out.png
"""
import json
import math
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

sys.path.insert(0, __file__.rsplit("/", 1)[0])
from preview_track import sample  # noqa: E402


def resample(dense, spacing=2.0):
    n = len(dense)
    cum = [0.0]
    for i in range(n):
        cum.append(cum[-1] + math.dist(dense[i][:2], dense[(i + 1) % n][:2]))
    length = cum[-1]
    count = round(length / spacing)
    sp = length / count
    out, j = [], 0
    for k in range(count):
        d = k * sp
        while cum[j + 1] < d:
            j += 1
        t = (d - cum[j]) / max(cum[j + 1] - cum[j], 1e-9)
        a, b = dense[j], dense[(j + 1) % n]
        out.append([a[i] + (b[i] - a[i]) * t for i in range(len(a))])
    return out, length, sp


def main():
    data = json.load(open(sys.argv[1]))
    pts = [[p[0], p[1], p[2], p[3]] for p in data["points"]]
    dense = sample(pts)
    rs, length, sp = resample(dense)
    n = len(rs)
    heads = [math.atan2(rs[(k + 1) % n][1] - rs[k - 1][1], rs[(k + 1) % n][0] - rs[k - 1][0]) for k in range(n)]
    curv = []
    for k in range(n):
        dh = heads[(k + 2) % n] - heads[k - 2]
        dh = (dh + math.pi) % (2 * math.pi) - math.pi
        curv.append(dh / (4 * sp))  # >0 = left turn (CCW) in x-east/y-north
    total_turn = sum(curv) * sp
    # Corner detection: contiguous runs with |k| above threshold and one sign.
    thr = 1.0 / 450.0
    corners = []
    k = 0
    start_k = next(i for i in range(n) if abs(curv[i]) < thr)
    i = start_k
    visited = 0
    while visited < n:
        if abs(curv[i % n]) >= thr:
            sgn = 1 if curv[i % n] > 0 else -1
            j = i
            angle = 0.0
            kmax = 0.0
            while abs(curv[j % n]) >= thr and (1 if curv[j % n] > 0 else -1) == sgn:
                angle += curv[j % n] * sp
                kmax = max(kmax, abs(curv[j % n]))
                j += 1
            if abs(math.degrees(angle)) >= 12:
                mid = (i + j) // 2 % n
                corners.append({"idx": mid, "dir": "L" if sgn > 0 else "R",
                                "angle": abs(math.degrees(angle)), "radius": 1 / kmax})
            visited += j - i
            i = j
        else:
            i += 1
            visited += 1
    start = data.get("start_line_distance", 0.0)
    corners.sort(key=lambda c: (c["idx"] * sp - start) % length)
    elev = [p[2] for p in rs]
    grade = max(abs(rs[(k + 5) % n][2] - rs[k][2]) / (5 * sp) for k in range(n))
    # clearance
    step = 4
    min_clear = 1e9
    for a in range(0, n, step):
        for b in range(a + step, n, step):
            along = min(b - a, n - (b - a)) * sp
            if along < 200:
                continue
            min_clear = min(min_clear, math.dist(rs[a][:2], rs[b][:2]))
    print(f"Länge: {length:.0f} m | Richtung: {'gegen Uhrzeigersinn' if total_turn > 0 else 'im Uhrzeigersinn'} "
          f"(Gesamtdrehung {math.degrees(total_turn):+.0f}°)")
    print(f"Kurven: {len(corners)} | Höhe {min(elev):.0f}–{max(elev):.0f} m (Δ {max(elev) - min(elev):.0f} m) | "
          f"max. Steigung {grade * 100:.1f} % | min. Abstand nicht benachbarter Abschnitte {min_clear:.0f} m")
    for num, c in enumerate(corners, 1):
        d = (c["idx"] * sp - start) % length
        print(f"  T{num:<2} {c['dir']}  {c['angle']:5.0f}°  r_min {c['radius']:6.0f} m  bei {d:5.0f} m  Höhe {rs[c['idx']][2]:4.1f} m")

    fig = plt.figure(figsize=(12, 13))
    ax = fig.add_axes([0.06, 0.3, 0.9, 0.66])
    colors = ["#e8483b", "#2f8fdd", "#e0a020"]
    secs = [f * length for f in data["sector_fractions"]] + [length]
    labelled = [False, False, False]
    for s in range(3):
        xs, ys = [], []
        for k2 in range(n):
            d = (k2 * sp - start) % length
            if secs[s] <= d < secs[s + 1]:
                xs.append(rs[k2][0]); ys.append(rs[k2][1])
            elif xs:
                ax.plot(xs, ys, "-", color=colors[s], lw=4, label=f"Sektor {s + 1}" if not labelled[s] else None)
                labelled[s] = True; xs, ys = [], []
        if xs:
            ax.plot(xs, ys, "-", color=colors[s], lw=4, label=f"Sektor {s + 1}" if not labelled[s] else None)
    for name, poly in data.get("districts", {}).items():
        px = [p[0] for p in poly] + [poly[0][0]]
        py = [p[1] for p in poly] + [poly[0][1]]
        ax.fill(px, py, alpha=0.08, color="gray")
        ax.plot(px, py, ":", color="gray", lw=1)
        ax.text(sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly), name,
                fontsize=8, color="dimgray", ha="center", style="italic")
    for num, c in enumerate(corners, 1):
        p = rs[c["idx"]]
        ax.annotate(f"T{num}", (p[0], p[1]), xytext=(8, 8), textcoords="offset points",
                    fontsize=10, weight="bold")
    sl = rs[int(start / sp) % n]
    ax.plot([sl[0] - 15, sl[0] + 15], [sl[1], sl[1]], "k-", lw=3)
    ax.text(sl[0] + 20, sl[1], "Start/Ziel", fontsize=9)
    for s in data.get("sightlines", []):
        x0, y0 = s["from"]
        b = math.radians(s["bearing_deg"])
        ax.annotate("", xy=(x0 + 260 * math.sin(b), y0 + 260 * math.cos(b)), xytext=(x0, y0),
                    arrowprops=dict(arrowstyle="->", color="teal", lw=1.2, ls="--"))
        ax.text(x0 + 280 * math.sin(b), y0 + 280 * math.cos(b), s["label"], fontsize=8, color="teal")
    shore = data.get("shoreline")
    if shore:
        sx = [p[0] for p in shore] + [900, 900]
        sy = [p[1] for p in shore] + [shore[-1][1], shore[0][1]]
        ax.fill(sx, sy, color="#9cc3e6", alpha=0.35)
        ax.text(600, 300, "BOSPORUS\n(schematisch)", color="#2a6aa0", fontsize=14, ha="center")
    ax.set_aspect("equal")
    ax.set_xlim(-800, 700)
    ax.legend(loc="lower left")
    ax.set_title(f"{data['name']} — {length:.0f} m, {len(corners)} Kurven, "
                 f"{'gegen' if total_turn > 0 else 'im'} Uhrzeigersinn (schematisch, nicht georeferenziert)")
    ax.grid(alpha=0.2)
    ax2 = fig.add_axes([0.06, 0.05, 0.9, 0.18])
    ds = [((k2 * sp - start) % length) for k2 in range(n)]
    order = sorted(range(n), key=lambda k2: ds[k2])
    ax2.plot([ds[k2] for k2 in order], [elev[k2] for k2 in order], "k-")
    for num, c in enumerate(corners, 1):
        d = (c["idx"] * sp - start) % length
        ax2.text(d, rs[c["idx"]][2] + 1.5, f"T{num}", fontsize=7, ha="center")
    for s in range(1, 3):
        ax2.axvline(secs[s], color="gray", ls="--")
    ax2.set_xlabel("Distanz ab Start/Ziel [m]")
    ax2.set_ylabel("Höhe [m]")
    ax2.set_title("Höhenprofil (Designziel)")
    fig.savefig(sys.argv[2], dpi=80)


if __name__ == "__main__":
    main()
