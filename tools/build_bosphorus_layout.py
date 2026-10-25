#!/usr/bin/env python3
"""Generates the Bosphorus GP schematic layout from a corner-by-corner design
(straights + arcs), closes the loop by solving two straight lengths, and writes
the control points into the circuit JSON (keeping its other fields).

Bearing convention: 0 = north, clockwise positive. Turns: R positive, L negative.
Coordinates: x = east, y = north (metres, schematic, not georeferenced).

Usage: python3 tools/build_bosphorus_layout.py
"""
import json
import math
import os

OUT = os.path.join(os.path.dirname(__file__), "..",
                   "unity/com.projectapex.game/Runtime/Resources/Tracks/bosphorus_gp.json")

# (kind, value, radius_or_None, elevation_at_end, width, label)
# kind S = straight (value = length), C = corner (value = signed angle, + = right)
DESIGN = [
    ("S", "MAIN", None, 2, 15, "Start/Ziel-Gerade"),
    ("C", 85, 35, 2, 15, "T1 Waterfront-Bremszone (R)"),
    ("S", 110, None, 2, 14, ""),
    ("C", -80, 45, 2, 14, "T2 (L)"),
    ("S", 110, None, 2.5, 14, ""),
    ("C", 35, 140, 3, 14, "T3 Bosporus-Schwung (R)"),
    ("S", 70, None, 3, 14, ""),
    ("C", -35, 140, 3, 14, "T4 (L)"),
    ("S", 60, None, 3.5, 14, ""),
    ("C", 40, 110, 4, 14, "T5 (R)"),
    ("S", 90, None, 5, 14, ""),
    ("C", -115, 30, 7, 13, "T6 Ortaköy-Anstieg (L, eng)"),
    ("S", 260, None, 18, 13, ""),
    ("C", 50, 50, 22, 13, "T7 bergauf (R)"),
    ("S", 140, None, 33, 13, ""),
    ("C", -40, 80, 37, 13, "T8 blinde Kuppe (L)"),
    ("S", 220, None, 45, 12.5, ""),
    ("C", -30, 60, 45, 12.5, "T9 Hügel-Esses (L)"),
    ("S", 30, None, 44.5, 12.5, ""),
    ("C", 75, 50, 44, 12.5, "T10 (R)"),
    ("S", 30, None, 43.5, 12.5, ""),
    ("C", -15, 80, 43, 12.5, "T11 (L)"),
    ("S", 130, None, 38, 14, ""),
    ("C", -150, 28, 35, 15, "T12 Abfahrts-Haarnadel (L)"),
    ("S", "DESCENT", None, 24, 14, ""),
    ("C", 30, 120, 21, 14, "T13 Fährterminal (R)"),
    ("S", 170, None, 17, 14, ""),
    ("C", -50, 90, 14, 14, "T14 (L)"),
    ("S", 170, None, 10, 14, ""),
    ("C", 20, 140, 8, 14, "T15 (R)"),
    ("S", 150, None, 4, 13, ""),
    ("C", -60, 35, 3, 12.5, "T16 Steinschikane (L)"),
    ("S", 35, None, 3, 12.5, ""),
    ("C", 50, 40, 3, 12.5, "T17 (R)"),
    ("S", 35, None, 3, 12.5, ""),
    ("C", -80, 35, 3, 12.5, "T18 (L)"),
    ("S", "LINK", None, 2.5, 14, ""),
    ("C", 45, 120, 2, 14, "T19 schnelle Rechts"),
    ("S", 60, None, 2, 14, ""),
    ("C", -135, 40, 2, 15, "T20 Schlusskurve (L)"),
]

STEP = 25.0  # control point spacing (m)


def trace(lengths):
    """Walks the design; returns list of (x, y, elev, width) and end pose."""
    x = y = 0.0
    bearing = 0.0
    elev = DESIGN[-1][3]
    pts = []
    for kind, value, radius, elev_end, width, _ in DESIGN:
        if kind == "S":
            length = lengths[value] if isinstance(value, str) else value
            n = max(1, round(length / STEP))
            e0 = elev
            for i in range(n):
                t = (i + 1) / n
                x += math.sin(math.radians(bearing)) * length / n
                y += math.cos(math.radians(bearing)) * length / n
                pts.append((x, y, e0 + (elev_end - e0) * t, width))
        else:
            ang = value
            arc = abs(math.radians(ang)) * radius
            n = max(2, round(arc / (STEP * 0.6)))
            e0 = elev
            for i in range(n):
                db = ang / n
                # chord step along the arc
                mid = bearing + db / 2
                chord = 2 * radius * math.sin(abs(math.radians(db)) / 2)
                x += math.sin(math.radians(mid)) * chord
                y += math.cos(math.radians(mid)) * chord
                bearing += db
                t = (i + 1) / n
                pts.append((x, y, e0 + (elev_end - e0) * t, width))
        elev = elev_end
    return pts, (x, y, bearing)


def solve():
    # Unknown straights: DESCENT (bearing 180) and LINK (bearing 90) are solved so
    # the loop closes; MAIN keeps the briefed straight length.
    lengths = {"MAIN": 740.0, "DESCENT": 500.0, "LINK": 150.0}
    for _ in range(20):
        _, (ex, ey, _) = trace(lengths)
        if math.hypot(ex, ey) < 1e-6:
            break
        # Numerical Jacobian over MAIN and LINK.
        J = []
        for key in ("DESCENT", "LINK"):
            l2 = dict(lengths)
            l2[key] += 1.0
            _, (x2, y2, _) = trace(l2)
            J.append((x2 - ex, y2 - ey))
        (a, c), (b, d) = J[0], J[1]
        det = a * d - b * c
        dm = (-ex * d + b * ey) / det
        dl = (-a * ey + c * ex) / det
        lengths["DESCENT"] += dm
        lengths["LINK"] += dl
    return lengths


def main():
    lengths = solve()
    pts, end = trace(lengths)
    print(f"MAIN={lengths['MAIN']:.0f} m  DESCENT={lengths['DESCENT']:.0f} m  LINK={lengths['LINK']:.0f} m  closure=({end[0]:.3f},{end[1]:.3f}) bearing_end={end[2]:.1f}")
    data = json.load(open(OUT))
    # Last generated point coincides with the origin; drop duplicates.
    rows = [[round(p[0], 1), round(p[1], 1), round(p[2], 1), p[3]] for p in pts[:-1]]
    data["points"] = rows
    json.dump(data, open(OUT, "w"), ensure_ascii=False, indent=1)
    print(f"{len(rows)} control points written")


if __name__ == "__main__":
    main()
