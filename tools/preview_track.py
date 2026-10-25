#!/usr/bin/env python3
"""Render a top-down preview of a track layout JSON and report geometry stats.

Mirrors the centripetal Catmull-Rom sampling in scripts/track/track_geometry.gd
so layout edits can be checked without opening Godot.

Usage: python3 tools/preview_track.py data/tracks/kestrel_ridge.json out.png
"""
import json
import math
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


def catmull_rom(p0, p1, p2, p3, t, alpha=0.5):
    def tj(ti, a, b):
        d = math.dist(a, b)
        return ti + max(d, 1e-4) ** alpha
    t0 = 0.0
    t1 = tj(t0, p0, p1)
    t2 = tj(t1, p1, p2)
    t3 = tj(t2, p2, p3)
    t = t1 + (t2 - t1) * t
    def lerp(a, b, ta, tb):
        return [(tb - t) / (tb - ta) * x + (t - ta) / (tb - ta) * y for x, y in zip(a, b)]
    a1 = lerp(p0, p1, t0, t1)
    a2 = lerp(p1, p2, t1, t2)
    a3 = lerp(p2, p3, t2, t3)
    b1 = lerp(a1, a2, t0, t2)
    b2 = lerp(a2, a3, t1, t3)
    return lerp(b1, b2, t1, t2)


def sample(points, per_seg=60):
    n = len(points)
    out = []
    for i in range(n):
        p0, p1, p2, p3 = (points[(i + k - 1) % n] for k in range(4))
        for s in range(per_seg):
            out.append(catmull_rom(p0, p1, p2, p3, s / per_seg))
    return out


def main():
    data = json.load(open(sys.argv[1]))
    pts = [[p[0], p[1], p[3]] for p in data["points"]]  # x, z, width
    xz = [[p[0], p[1]] for p in pts]
    s = sample(xz)
    w = sample([[p[2]] for p in pts])
    length = sum(math.dist(s[i], s[(i + 1) % len(s)]) for i in range(len(s)))
    # Resample at 2 m and measure curvature exactly like track_geometry.gd
    # (heading change across +-2 samples over 8 m).
    cum = [0.0]
    for i in range(len(s)):
        cum.append(cum[-1] + math.dist(s[i], s[(i + 1) % len(s)]))
    count = round(length / 2.0)
    sp = length / count
    rs, j = [], 0
    for k in range(count):
        d = k * sp
        while cum[j + 1] < d:
            j += 1
        t = (d - cum[j]) / max(cum[j + 1] - cum[j], 1e-9)
        a, b = s[j], s[(j + 1) % len(s)]
        rs.append([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t])
    heads = []
    for k in range(count):
        a, b = rs[k - 1], rs[(k + 1) % count]
        heads.append(math.atan2(b[1] - a[1], b[0] - a[0]))
    radii = []
    for k in range(count):
        dh = heads[(k + 2) % count] - heads[k - 2]
        dh = (dh + math.pi) % (2 * math.pi) - math.pi
        radii.append(4 * sp / abs(dh) if abs(dh) > 1e-9 else 1e9)
    kmin = min(range(count), key=lambda k: radii[k])
    print(f"tightest corner near x={rs[kmin][0]:.0f} z={rs[kmin][1]:.0f}")
    # Clearance between non-neighbouring parts of the track
    step = 4
    sub = s[::step]
    min_clear = 1e9
    total = len(sub)
    for i in range(total):
        for j in range(i + 1, total):
            gap = min(j - i, total - (j - i)) * step
            if gap * (length / len(s)) < 150:
                continue
            min_clear = min(min_clear, math.dist(sub[i], sub[j]))
    print(f"length_m={length:.0f} min_radius_m={min(radii):.1f} min_clearance_m={min_clear:.1f}")
    fig, ax = plt.subplots(figsize=(10, 8))
    ax.plot([p[0] for p in s] + [s[0][0]], [p[1] for p in s] + [s[0][1]], "k-", lw=3)
    ax.plot([p[0] for p in xz], [p[1] for p in xz], "ro", ms=3)
    for i, p in enumerate(xz):
        ax.annotate(str(i), p, fontsize=7, color="blue")
    ax.set_aspect("equal")
    ax.invert_yaxis()
    ax.set_title(f"{data['name']}  {length:.0f} m")
    fig.savefig(sys.argv[2], dpi=90)


if __name__ == "__main__":
    main()
