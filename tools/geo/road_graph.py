"""Drivable road graph from the OSM core extract (node level), in the local frame of dem_tools.

Edges carry length, street name, class, width, oneway, bridge/tunnel and DEM heights of
both ends. Used to route the circuit along real street centrelines.
"""
import heapq
import math
import os
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(__file__))
import dem_tools as dem

CORE = os.path.join(dem.ROOT, "data/geo/osm/bosphorus_core.osm")
DRIVABLE = {"motorway", "trunk", "primary", "secondary", "tertiary", "unclassified", "residential",
            "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link", "living_street"}
DEFAULT_WIDTH = {"motorway": 14, "trunk": 14, "primary": 12, "secondary": 10, "tertiary": 8,
                 "residential": 6, "unclassified": 6, "living_street": 5}


def _width(tags):
    try:
        if "width" in tags:
            return float(tags["width"].replace("m", "").strip()), "osm"
        if "lanes" in tags:
            return 3.3 * float(tags["lanes"].split(";")[0]), "lanes"
    except ValueError:
        pass
    h = tags.get("highway", "").replace("_link", "")
    return float(DEFAULT_WIDTH.get(h, 6)), "default"


class RoadGraph:
    def __init__(self, path=CORE):
        self.pos = {}     # node id -> (e, n)
        self.h = {}       # node id -> DEM height
        self.adj = {}     # node id -> list of edge dicts
        self.ways = []
        nodes = {}
        for _, el in ET.iterparse(path, events=("end",)):
            if el.tag == "node":
                nodes[el.get("id")] = (float(el.get("lat")), float(el.get("lon")))
                el.clear()
            elif el.tag == "way":
                tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
                if tags.get("highway") in DRIVABLE and tags.get("area") != "yes":
                    refs = [nd.get("ref") for nd in el.findall("nd")]
                    self.ways.append((el.get("id"), refs, tags))
                el.clear()
        for wid, refs, tags in self.ways:
            w, wsrc = _width(tags)
            for r in refs:
                if r in nodes and r not in self.pos:
                    la, lo = nodes[r]
                    self.pos[r] = dem.enu(la, lo)
                    self.h[r] = dem.height(la, lo)
            refs = [r for r in refs if r in self.pos]
            oneway = tags.get("oneway") in ("yes", "1", "-1") or tags.get("junction") == "roundabout"
            for a, b in zip(refs, refs[1:]):
                e = {"a": a, "b": b, "way": wid, "name": tags.get("name", ""), "highway": tags["highway"],
                     "width": w, "width_src": wsrc, "oneway": oneway, "lanes": tags.get("lanes"),
                     "bridge": tags.get("bridge") == "yes", "tunnel": tags.get("tunnel") == "yes",
                     "len": math.dist(self.pos[a], self.pos[b])}
                self.adj.setdefault(a, []).append(e)
                r = dict(e, a=b, b=a)
                self.adj.setdefault(b, []).append(r)

    def nearest(self, e, n, name=None):
        best, bid = 1e18, None
        for nid, p in self.pos.items():
            if name is not None and not any(x["name"] == name for x in self.adj.get(nid, [])):
                continue
            d = (p[0] - e) ** 2 + (p[1] - n) ** 2
            if d < best:
                best, bid = d, nid
        return bid

    def route(self, start, goal, cost, forbidden=frozenset()):
        """Dijkstra with a per-edge cost function; forbidden nodes cannot be entered."""
        dist = {start: 0.0}
        prev = {}
        pq = [(0.0, start)]
        while pq:
            d, u = heapq.heappop(pq)
            if u == goal:
                break
            if d > dist.get(u, 1e18):
                continue
            for e in self.adj.get(u, []):
                v = e["b"]
                if v in forbidden and v != goal:
                    continue
                c = cost(e)
                if c is None:
                    continue
                nd = d + c
                if nd < dist.get(v, 1e18):
                    dist[v] = nd
                    prev[v] = (u, e)
                    heapq.heappush(pq, (nd, v))
        if goal not in dist:
            return None
        path, edges, v = [goal], [], goal
        while v != start:
            u, e = prev[v]
            path.append(u)
            edges.append(e)
            v = u
        return path[::-1], edges[::-1]
