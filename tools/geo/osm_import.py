#!/usr/bin/env python3
"""OSM import pipeline for the Bosphorus GP (phase 2).

Reads OSM XML extracts (data/geo/osm/*.osm, e.g. from the Overpass queries next to
them), converts every node into the shared local metric frame of tools/geo/dem_tools.py
(east/north metres around Dolmabahçe, true north = +z, no rotation) and writes
data/geo/osm/bosphorus_osm.json with streets, coastline, buildings, green areas,
walls and landmarks. Heights come from the Copernicus DEM where tiles are present.

The output is an ODbL derivative database: keep it separate from game code and offer
it (or this script plus the extract) under ODbL when the game is published.

Usage: python3 tools/geo/osm_import.py [extract.osm ...]
       python3 tools/geo/osm_import.py --selftest
"""
import json
import os
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(__file__))
import dem_tools as dem

OSM_DIR = os.path.join(dem.ROOT, "data/geo/osm")
OUT = os.path.join(OSM_DIR, "bosphorus_osm.json")

# Streets the draft plan relies on (checked after import).
REQUIRED_STREETS = [
    "Dolmabahçe Caddesi", "Çırağan Caddesi", "Muallim Naci Caddesi", "Barbaros Bulvarı",
    "Palanga Caddesi", "Yıldız Caddesi", "Spor Caddesi", "Serencebey Yokuşu", "Dereboyu Caddesi",
]

# Default carriageway widths (m) when OSM has no width/lanes.
DEFAULT_WIDTH = {"motorway": 14, "trunk": 14, "primary": 12, "secondary": 10, "tertiary": 8,
                 "residential": 6, "unclassified": 6, "service": 4, "living_street": 5, "pedestrian": 5}


def parse(paths):
    nodes, ways = {}, []
    for path in paths:
        for _, el in ET.iterparse(path, events=("end",)):
            if el.tag == "node":
                tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
                nodes[el.get("id")] = (float(el.get("lat")), float(el.get("lon")), tags)
                el.clear()
            elif el.tag == "way":
                refs = [nd.get("ref") for nd in el.findall("nd")]
                tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
                ways.append((el.get("id"), refs, tags))
                el.clear()
    return nodes, ways


def dem_height(lat, lon):
    try:
        return round(dem.height(lat, lon), 1)
    except (FileNotFoundError, OSError):
        return None


def to_local(nodes, refs, with_height):
    pts = []
    for r in refs:
        if r not in nodes:
            continue
        lat, lon, _ = nodes[r]
        e, n = dem.enu(lat, lon)
        p = [round(e, 2), round(n, 2)]
        if with_height:
            p.append(dem_height(lat, lon))
        pts.append(p)
    return pts


def width_of(tags):
    try:
        if "width" in tags:
            return float(tags["width"].replace("m", "").strip())
        if "lanes" in tags:
            return 3.3 * float(tags["lanes"].split(";")[0])
    except ValueError:
        pass
    return DEFAULT_WIDTH.get(tags.get("highway"), 6)


def levels_height(tags):
    for k in ("height", "building:height"):
        if k in tags:
            try:
                return float(tags[k].replace("m", "").strip()), "osm"
            except ValueError:
                pass
    if "building:levels" in tags:
        try:
            return 3.1 * float(tags["building:levels"]) + 1.0, "levels"
        except ValueError:
            pass
    return None, "unknown"


def convert(nodes, ways):
    out = {"frame": {"origin": [dem.LAT0, dem.LON0], "axes": "x = east, z = north (metres), no rotation",
                     "source": "© OpenStreetMap contributors (ODbL 1.0); heights: Copernicus DEM GLO-30"},
           "streets": [], "coastline": [], "buildings": [], "green": [], "walls": [], "landmarks": [], "bridges": []}
    for wid, refs, tags in ways:
        if "highway" in tags:
            out["streets"].append({"id": wid, "name": tags.get("name", ""), "highway": tags["highway"],
                                   "width": width_of(tags), "oneway": tags.get("oneway") == "yes",
                                   "bridge": tags.get("bridge") == "yes", "tunnel": tags.get("tunnel") == "yes",
                                   "points": to_local(nodes, refs, True)})
        if tags.get("natural") == "coastline":
            out["coastline"].append({"id": wid, "points": to_local(nodes, refs, False)})
        if "building" in tags or "building:part" in tags:
            h, src = levels_height(tags)
            out["buildings"].append({"id": wid, "type": tags.get("building", tags.get("building:part")), "name": tags.get("name", ""),
                                     "height": h, "height_source": src, "historic": tags.get("historic", ""),
                                     "amenity": tags.get("amenity", ""), "outline": to_local(nodes, refs, False)})
        if tags.get("leisure") in ("park", "garden") or tags.get("landuse") in ("grass", "forest", "recreation_ground", "cemetery") \
                or tags.get("natural") in ("wood", "scrub"):
            out["green"].append({"id": wid, "kind": tags.get("leisure") or tags.get("landuse") or tags.get("natural"),
                                 "name": tags.get("name", ""), "outline": to_local(nodes, refs, False)})
        if tags.get("barrier") == "wall":
            out["walls"].append({"id": wid, "points": to_local(nodes, refs, False)})
        if tags.get("man_made") == "bridge" or (tags.get("bridge") == "yes" and tags.get("highway") in ("motorway", "trunk")):
            out["bridges"].append({"id": wid, "name": tags.get("name", ""), "points": to_local(nodes, refs, True)})
        if tags.get("amenity") == "place_of_worship" or "historic" in tags or tags.get("tourism") in ("attraction", "museum"):
            pts = to_local(nodes, refs, False)
            if pts:
                cx = sum(p[0] for p in pts) / len(pts); cz = sum(p[1] for p in pts) / len(pts)
                out["landmarks"].append({"id": "w" + wid, "name": tags.get("name", ""), "kind": tags.get("historic") or tags.get("amenity") or tags.get("tourism"),
                                         "centre": [round(cx, 1), round(cz, 1)], "outline": pts})
    for nid, (lat, lon, tags) in nodes.items():
        if tags.get("amenity") == "place_of_worship" or "historic" in tags or tags.get("tourism") in ("attraction", "museum", "viewpoint"):
            e, n = dem.enu(lat, lon)
            out["landmarks"].append({"id": "n" + nid, "name": tags.get("name", ""), "kind": tags.get("historic") or tags.get("amenity") or tags.get("tourism"),
                                     "centre": [round(e, 1), round(n, 1)], "outline": []})
    return out


def report(out):
    names = {s["name"] for s in out["streets"]}
    print(f"streets {len(out['streets'])}, coastline ways {len(out['coastline'])}, buildings {len(out['buildings'])}, "
          f"green {len(out['green'])}, landmarks {len(out['landmarks'])}, bridges {len(out['bridges'])}")
    for s in REQUIRED_STREETS:
        print(f"  {'OK  ' if s in names else 'FEHLT'} {s}")
    known = sum(1 for b in out["buildings"] if b["height_source"] != "unknown")
    if out["buildings"]:
        print(f"  building heights known for {known}/{len(out['buildings'])} ({100 * known / len(out['buildings']):.0f} %)")


SAMPLE = """<?xml version='1.0' encoding='UTF-8'?>
<osm version="0.6">
 <node id="1" lat="41.0400" lon="29.0010"/><node id="2" lat="41.0410" lon="29.0030"/>
 <node id="3" lat="41.0420" lon="29.0050"/><node id="4" lat="41.0405" lon="29.0005"/>
 <node id="5" lat="41.0406" lon="29.0005"/><node id="6" lat="41.0406" lon="29.0007"/>
 <node id="7" lat="41.04694" lon="29.02694"><tag k="amenity" v="place_of_worship"/><tag k="name" v="Ortaköy Camii"/></node>
 <way id="10"><nd ref="1"/><nd ref="2"/><nd ref="3"/><tag k="highway" v="primary"/><tag k="name" v="Dolmabahçe Caddesi"/><tag k="lanes" v="3"/></way>
 <way id="11"><nd ref="4"/><nd ref="5"/><nd ref="6"/><nd ref="4"/><tag k="building" v="apartments"/><tag k="building:levels" v="5"/></way>
</osm>"""


def selftest():
    import tempfile
    with tempfile.NamedTemporaryFile("w", suffix=".osm", delete=False, encoding="utf-8") as f:
        f.write(SAMPLE)
    out = convert(*parse([f.name]))
    os.unlink(f.name)
    st = out["streets"][0]
    assert st["name"] == "Dolmabahçe Caddesi" and abs(st["width"] - 9.9) < 1e-6
    e, n = st["points"][0][:2]
    assert abs(e - (29.0010 - dem.LON0) * dem.K_LON) < 0.05 and abs(n - (41.0400 - dem.LAT0) * dem.K_LAT) < 0.05
    assert out["buildings"][0]["height"] == 3.1 * 5 + 1.0
    assert any(l["name"] == "Ortaköy Camii" for l in out["landmarks"])
    print("selftest OK")


def main():
    if "--selftest" in sys.argv:
        selftest()
        return
    paths = [a for a in sys.argv[1:] if a.endswith(".osm")] or \
        [os.path.join(OSM_DIR, f) for f in sorted(os.listdir(OSM_DIR)) if f.endswith(".osm")]
    if not paths:
        print("Keine OSM-Datei gefunden. Erwartet: data/geo/osm/bosphorus_core.osm (siehe bosphorus_core.overpassql).")
        sys.exit(2)
    out = convert(*parse(paths))
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False)
    report(out)
    print("->", OUT)


if __name__ == "__main__":
    main()
