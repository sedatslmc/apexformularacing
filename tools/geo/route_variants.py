#!/usr/bin/env python3
"""Routes circuit variants along real OSM street centrelines (phase 3).

Each variant is a list of legs: (via point east/north, preferred street names). Legs are
routed with Dijkstra on the drivable graph; edges outside the preferred streets cost 8x,
and nodes used by earlier legs are forbidden, so a variant that only closes by crossing
or re-using its own road fails visibly. Writes data/geo/track_variants.json (derived
from OSM, ODbL).
"""
import json
import math
import os
import pickle
import sys

sys.path.insert(0, os.path.dirname(__file__))
import dem_tools as dem
from road_graph import RoadGraph

OUT = os.path.join(dem.ROOT, "data/geo/track_variants.json")
RETURN = {"Kabataş: Bayıldım Cd. -> Kadırgalar Cd. (temporäre Verbindung)", "Süleyman Seba Caddesi", "Maçka Caddesi", "Bayıldım Caddesi", "Dolmabahçe Gazhane Caddesi", "Kadırgalar Caddesi",
          "Dolmabahçe Caddesi", "Meclis-i Mebusan Caddesi"}
SHORE = {"Dolmabahçe Caddesi", "Beşiktaş Caddesi", "Çırağan Caddesi"}

# Legs: (via east/north, street the via point snaps to, preferred streets for the leg).
VARIANTS = {
    # Return through the Ihlamur valley and Akaretler (Süleyman Seba): no shared road.
    "A-Ihlamur": [
        ((-200, 40), "Dolmabahçe Caddesi", None),
        ((1900, 960), "Çırağan Caddesi", SHORE),
        ((1250, 1400), "Palanga Caddesi", {"Palanga Caddesi", "Taşbasamak Sokağı", "Çırağan Caddesi", "Muallim Naci Caddesi"}),
        ((690, 1815), "Palanga Caddesi", {"Palanga Caddesi"}),
        ((565, 1185), "Barbaros Bulvarı", {"Barbaros Bulvarı"}),
        ((330, 1180), "Ihlamur Yıldız Caddesi", {"Ihlamur Yıldız Caddesi", "Yıldız Caddesi", "Barbaros Bulvarı"}),
        ((120, 330), "Ihlamurdere Caddesi", {"Ihlamurdere Caddesi"}),
        ((-560, 470), "Süleyman Seba Caddesi", {"Süleyman Seba Caddesi", "Ihlamurdere Caddesi"}),
        ((-200, 40), "Dolmabahçe Caddesi", RETURN),
    ],
    # Full Barbaros descent; ~300 m of Beşiktaş Cd. used on its two separate carriageways.
    "A-Barbaros": [
        ((-200, 40), "Dolmabahçe Caddesi", None),
        ((1900, 960), "Çırağan Caddesi", SHORE),
        ((1250, 1400), "Palanga Caddesi", {"Palanga Caddesi", "Taşbasamak Sokağı", "Çırağan Caddesi", "Muallim Naci Caddesi"}),
        ((690, 1815), "Palanga Caddesi", {"Palanga Caddesi"}),
        ((452, 450), "Barbaros Bulvarı", {"Barbaros Bulvarı"}),
        ((250, 255), "Beşiktaş Caddesi", {"Barbaros Bulvarı", "Beşiktaş Caddesi"}),
        ((-300, 400), "Süleyman Seba Caddesi", {"Süleyman Seba Caddesi", "Beşiktaş Caddesi"}),
        ((-200, 40), "Dolmabahçe Caddesi", RETURN),
    ],
}


# Fictional racing links (documented): short temporary connections where the real
# junction would force a detour through an interchange loop.
EXTRA_LINKS = [
    ("Kabataş: Bayıldım Cd. -> Kadırgalar Cd. (temporäre Verbindung)", (-552, 150), "Bayıldım Caddesi", (-562, 168), "Kadırgalar Caddesi"),
]


def add_extra_links(g):
    for label, pa, na, pb, nb in EXTRA_LINKS:
        a, b = g.nearest(*pa, name=na), g.nearest(*pb, name=nb)
        d = math.dist(g.pos[a], g.pos[b])
        if d > 40:
            print("Hinweis: Verbindung zu lang, ausgelassen:", label, round(d))
            continue
        e = {"a": a, "b": b, "way": "fictional", "name": label, "highway": "fictional", "width": 11.0, "width_src": "fictional",
             "oneway": False, "lanes": None, "bridge": False, "tunnel": False, "len": d}
        g.adj.setdefault(a, []).append(e)
        g.adj.setdefault(b, []).append(dict(e, a=b, b=a))


def load_graph():
    cache = "/tmp/apex_road_graph.pkl"
    if os.path.exists(cache) and os.path.getmtime(cache) > os.path.getmtime(os.path.join(dem.ROOT, "data/geo/osm/bosphorus_core.osm")):
        return pickle.load(open(cache, "rb"))
    g = RoadGraph()
    pickle.dump(g, open(cache, "wb"))
    return g


def route_variant(g, legs):
    used = set()
    nodes, edges = [], []
    start = g.nearest(*legs[0][0], name=legs[0][1])
    cur = start
    for k, (via, snap, names) in enumerate(legs[1:], 1):
        goal = start if k == len(legs) - 1 else g.nearest(*via, name=snap)
        cost = lambda e, names=names: None if e["tunnel"] else e["len"] * (1.0 if e["name"] in names else (1.6 if not e["name"] else 8.0))
        res = g.route(cur, goal, cost, forbidden=frozenset(used - {cur, goal}))
        if res is None:
            return None, f"Abschnitt {k} (zu {snap}) nicht ohne Kreuzung/Doppelnutzung schließbar"
        path, es = res
        used.update(path[:-1])
        nodes += path[:-1]
        edges += es
        cur = goal
    return (nodes, edges), None


def summarize(g, nodes, edges):
    pts = [g.pos[n] for n in nodes] + [g.pos[nodes[0]]]
    s, h = dem.road_profile(pts, step=10.0, smooth=60.0)
    streets = []
    for e in edges:
        if streets and streets[-1]["name"] == e["name"]:
            streets[-1]["len"] += e["len"]
            streets[-1]["widths"].append(e["width"])
        else:
            streets.append({"name": e["name"] or "(ohne Namen)", "len": e["len"], "widths": [e["width"]], "highway": e["highway"]})
    seg_info = [{"name": e["name"] or "(ohne Namen)", "width": e["width"], "width_src": e["width_src"],
                 "lanes": e["lanes"], "oneway": e["oneway"], "highway": e["highway"]} for e in edges]
    return {
        "segments": seg_info,
        "length": s[-1],
        "points": [[round(p[0], 2), round(p[1], 2)] for p in pts],
        "profile": {"s": [round(x, 1) for x in s], "h": [round(x, 1) for x in h]},
        "height_range": [min(h), max(h)],
        "max_grade_80m": dem.max_grade(s, h, 80.0),
        "max_grade_200m": dem.max_grade(s, h, 200.0),
        "streets": [{"name": x["name"], "len": round(x["len"]), "highway": x["highway"],
                     "width_min": min(x["widths"]), "width_max": max(x["widths"])} for x in streets if x["len"] > 15],
    }


def main():
    g = load_graph()
    add_extra_links(g)
    out = {}
    for name, legs in VARIANTS.items():
        res, err = route_variant(g, legs)
        if err:
            print(f"{name}: FEHLER – {err}")
            out[name] = {"error": err}
            continue
        sm = summarize(g, *res)
        out[name] = sm
        print(f"{name}: {sm['length']:.0f} m, Höhe {sm['height_range'][0]:.0f}..{sm['height_range'][1]:.0f} m, "
              f"max. Steigung {sm['max_grade_80m'] * 100:.1f} % (80 m) / {sm['max_grade_200m'] * 100:.1f} % (200 m)")
        for st in sm["streets"]:
            print(f"    {st['name']:32s} {st['len']:5d} m  {st['highway']:12s} Breite {st['width_min']:.1f}–{st['width_max']:.1f} m")
    json.dump(out, open(OUT, "w"), ensure_ascii=False)


if __name__ == "__main__":
    main()
