#!/usr/bin/env python3
"""Ortaköy R20 layout (approved by the product owner on 2026-10-31, docs/BOSPHORUS_WATERFRONT.md section 4).

Replaces the Ortaköy hairpin at the end of Çırağan Cd. by a temporary Grand Prix routing past Ortaköy Square:
Muallim Naci Cd. eastbound (real street), a right-hander onto the square, a 20 m left-hand hairpin round the open
north part of the square, back up the square's east side and west on Muallim Naci Cd. (parallel carriageway with a
median wall) into the existing Palanga climb. The mosque, the Etz Ahayim Synagogue, all historic buildings and
the coastline stay untouched; four small non-listed buildings on the north side of Muallim Naci Cd. give way
(fictional temporary configuration).

Used by build_track_a.py (--layout r20). Geometry in the unrotated game frame (x = east, z = north, metres).
"""
import math

import numpy as np

# Splice on the existing plan: leave Çırağan Cd. here (outbound) and rejoin the Palanga climb here (return).
SPLICE_OUT = (1862.98, 922.4)
SPLICE_IN = (1821.17, 1013.9)
# Control points (game metres). Outbound: Muallim Naci south lane, right turn onto the west side of the square.
# The section leaves Çırağan Cd. on its straight, 40 m before the old junction, so the Muallim Naci bend (about
# 30° right, braked from full speed) is one even curve of about 137 m radius without a counter-kink (found by a
# search over the control points against the façades). Both carriageways sit as far north as the street allows, so that the houses on the south side, the Etz Ahayim
# Synagogue and the corner block at the square entry keep the city's 3 m clearance beyond the barrier; only the
# approved small buildings on the north side give way.
MN_OUT = [(1862.98, 922.4), (1885.0, 940.7), (1905.0, 956.7), (1925.0, 971.0), (1945.0, 981.4), (1965.0, 988.4), (1985.0, 992.7),
          (2005.0, 995.5), (2030.0, 997.7), (2059.0, 999.8)]
ENTRY = [(2080, 1000.5), (2095.5, 1000), (2107.5, 994.5), (2116.5, 980)]
HAIRPIN_CENTRE, HAIRPIN_R = (2133.0, 962.0), 20.0
# Exit: round the hairpin (S -> E -> N -> NW) and onto the Muallim Naci north lane, then the existing hairpin exit.
EXIT = [(2118, 1002.5), (2102, 1010.5)]
# After the exit the line runs straight on along Muallim Naci Cd. and through the former hairpin area into the
# Palanga climb in one gentle curve (radius > 110 m); an earlier version dipped south here and made an S on the 11 %
# climb that spun dry keyboard laps.
MN_RET = [(2080, 1011.7), (2059.8, 1012.8), (2029.7, 1011.0), (1999.7, 1008.5), (1977.6, 1006.8), (1957.7, 1005.6), (1940.2, 1005.5),
          (1923.1, 1007.0), (1907.0, 1008.6), (1892.2, 1010.2), (1866.0, 1012.8), (1840.5, 1014.6), (1821.17, 1013.9)]
STEP = 4.0
# Street bends without kerbs: the Muallim Naci bend is taken under full braking with the outer wheels near the
# median wall; a kerb there unsettles the car (dry keyboard laps without assists spun on it).
NO_KERBS = {"Muallim Naci"}
# Corners of the new section (names in Turkish as in the game), located by position.
CORNERS = [("Muallim Naci", (1958, 982), "rechts", 30), ("Meydan Girişi", (2108, 985), "rechts", 85),
           ("Ortaköy Meydan Virajı", (2133, 942), "links", 250), ("Meydan Çıkışı", (2104, 1008), "links", 70)]
# Street-level heights (m) for the vertical alignment's reference, from the OSM street nodes (DEM at the street
# axis): Muallim Naci 10-11 m, the square 6-7 m. Interpolated by position, inverse-distance weighted.
STREET_LEVEL = [(1902, 949, 9.2), (1925, 962, 9.6), (1945, 975, 10.0), (1965, 987, 10.3), (2005, 996, 10.5),
                (2045, 1000, 10.4), (2080, 1004, 10.0), (2104, 1000, 9.0), (2118, 985, 8.0), (2120, 962, 7.0),
                (2136, 962, 6.8), (2150, 962, 6.8), (2133, 942, 6.5), (2140, 985, 7.5)]


def _arc(c, r, a0, a1, step=30.0):
    n = max(2, int(abs(a1 - a0) / step) + 1)
    return [(c[0] + r * math.cos(math.radians(a)), c[1] + r * math.sin(math.radians(a))) for a in np.linspace(a0, a1, n)]


def _catmull(ctrl, step):
    c = np.array(ctrl, float)
    c = np.vstack([c[0] * 2 - c[1], c, c[-1] * 2 - c[-2]])
    out = []
    for i in range(1, len(c) - 2):
        p0, p1, p2, p3 = c[i - 1], c[i], c[i + 1], c[i + 2]
        n = max(2, int(np.hypot(*(p2 - p1)) / 0.4))
        for k in range(n):
            u = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3))
    out.append(c[-2])
    out = np.array(out)
    d = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(out, axis=0).T))])
    s = np.linspace(0, d[-1], max(2, int(round(d[-1] / step)) + 1))
    return np.column_stack([np.interp(s, d, out[:, 0]), np.interp(s, d, out[:, 1])])


def centreline():
    """The new section from SPLICE_OUT to SPLICE_IN, about STEP metres apart (both ends included)."""
    ctrl = MN_OUT + ENTRY + _arc(HAIRPIN_CENTRE, HAIRPIN_R, 180, 420) + EXIT + MN_RET
    return _catmull(ctrl, STEP)


def street_level(x, z):
    c = np.array(STREET_LEVEL, float)
    d = np.hypot(c[:, 0] - x, c[:, 1] - z)
    if d.min() < 0.5:
        return float(c[np.argmin(d), 2])
    w = 1 / d ** 2
    return float(np.sum(w * c[:, 2]) / np.sum(w))


def is_parallel(x, z):
    """Side-by-side carriageways (median wall on the left of both): Muallim Naci Cd. and the square's neck."""
    return (1928 < x < 2112 and 975 < z < 1013) or (2104 < x < 2130 and 975 < z < 1006)


def is_narrow(x, z):
    """The 180 m between the old junction and the square, between the façades: 9.5 m per carriageway with 1 m to
    the median wall on both (the return leg runs 2 m north, where the four small buildings gave way)."""
    return 1928 < x < 2010 and 975 < z < 1005


def exit_runoff(x, z):
    """Outside of the square exit (left onto Muallim Naci Cd.): the open forecourt north of the street gives 4 m of
    run-off where a car runs wide accelerating out (wet keyboard laps did)."""
    return 2.0 if (2030 < x < 2112 and 1005 < z < 1018) else 0.0


def facade_runoff(x, z):
    """Outbound leg along the south façades of Muallim Naci Cd. (houses, Etz Ahayim Synagogue, the corner block at
    the square entry): the barrier stands 0.3 m from the road edge so every façade keeps the city's 3 m building
    clearance beyond the barrier (OsmCity.BuildingClearance). None of these buildings gives way."""
    return 0.3 if (1945 < x < 2105 and 955 < z < 999) else None


def apply(plan):
    """Returns a copy of the plan with the R20 section; plus the splice (lap s0, s1 of the old plan) and the
    length change dl, for shifting the lap-distance constants of build_track_a.py."""
    pts = [tuple(p) for p in plan["points"]]
    n = len(pts)
    arc = [0.0]
    for i in range(1, n):
        arc.append(arc[-1] + math.dist(pts[i - 1], pts[i]))
    i0 = min(range(n), key=lambda i: math.dist(pts[i], SPLICE_OUT))
    i1 = min(range(n), key=lambda i: math.dist(pts[i], SPLICE_IN))
    assert i0 < i1, "splice order"
    new = centreline()
    # Join exactly at the plan points.
    new[0] = pts[i0]
    new[-1] = pts[i1]
    seg_len = float(np.sum(np.hypot(*np.diff(new, axis=0).T)))
    s0, s1 = arc[i0], arc[i1]
    dl = seg_len - (s1 - s0)
    out = dict(plan)
    out["points"] = [list(p) for p in pts[:i0]] + [[round(float(x), 2), round(float(z), 2)] for x, z in new] + [list(p) for p in pts[i1 + 1:]]
    # Street-level profile: replace the samples inside the splice, shift the rest.
    prof = plan["profile"]
    s_new = [s0 + float(d) for d in np.concatenate([[0], np.cumsum(np.hypot(*np.diff(new, axis=0).T))])]
    ps, ph = [], []
    for s, h in zip(prof["s"], prof["street"]):
        if s < s0:
            ps.append(s); ph.append(h)
    old = np.array(pts)
    for (x, z), s in zip(new[1:-1], s_new[1:-1]):
        # On the stretches the new section shares with the old line (hairpin exit into the Palanga climb), the
        # approved plan's own street profile; on the new streets the OSM street level.
        k = int(np.argmin(np.hypot(old[:, 0] - x, old[:, 1] - z)))
        if math.hypot(old[k, 0] - x, old[k, 1] - z) < 12.0 and ((x < 1932 and z > 995) or x < 1902):
            h = float(np.interp(arc[k], prof["s"], prof["street"]))
        else:
            h = street_level(x, z)
        ps.append(round(s, 1)); ph.append(round(h, 2))
    for s, h in zip(prof["s"], prof["street"]):
        if s > s1:
            ps.append(round(s + dl, 1)); ph.append(h)
    out["profile"] = {"s": ps, "street": ph}
    # Corners: the Ortaköy hairpin goes, the four new ones come in; later corners shift by dl.
    corners = []
    for c in plan["corners"]:
        if s0 <= c["s"] <= s1:
            continue
        c2 = dict(c)
        if c["s"] > s1:
            c2["s"] = c["s"] + dl
        corners.append(c2)
    for name, pos, d, ang in CORNERS:
        k = int(np.argmin(np.hypot(new[:, 0] - pos[0], new[:, 1] - pos[1])))
        c = {"s": round(s_new[k], 1), "street": name, "dir": d, "angle": ang, "pos": [round(float(new[k, 0]), 1), round(float(new[k, 1]), 1)]}
        if name in NO_KERBS:
            c["kerbs"] = False
        corners.append(c)
    corners.sort(key=lambda c: c["s"])
    out["corners"] = corners
    out["sectors"] = [s if s < s0 else s + dl for s in plan["sectors"]]
    out["length"] = plan["length"] + dl
    return out, s0, s1, dl
