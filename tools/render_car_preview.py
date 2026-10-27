#!/usr/bin/env python3
"""Renders the OBJ exported by build/Apex.ModelPreview with back-face culling
(Unity convention: front face where cross(b-a, c-a) points at the viewer), flat
Lambert shading and a painter's sort. Missing faces reveal winding errors.

Usage: python3 tools/render_car_preview.py <car.obj> <out.png> [eye_x,eye_y,eye_z]
"""
import sys
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection

COLORS = {
    "CarPaint": (0.05, 0.33, 0.62), "CarAccent": (0.1, 0.85, 0.95), "Carbon": (0.12, 0.12, 0.14),
    "Titanium": (0.55, 0.57, 0.6), "Tire": (0.08, 0.08, 0.08), "Rim": (0.55, 0.56, 0.6),
    "Mirror": (0.8, 0.85, 0.9), "Interior": (0.2, 0.2, 0.22), "Belt": (0.75, 0.1, 0.1),
    "Helmet": (0.92, 0.92, 0.95), "Visor": (0.05, 0.05, 0.08), "Glove": (0.15, 0.15, 0.15),
    "Display": (0.0, 0.0, 0.0),
    "Marble": (0.93, 0.92, 0.89), "Stone": (0.78, 0.75, 0.68), "Lead": (0.45, 0.48, 0.5), "Gilt": (0.83, 0.66, 0.3),
    # City life (--citylife export).
    "CityCar0": (0.86, 0.86, 0.85), "CityCar4": (0.1, 0.15, 0.3), "CityCar5": (0.4, 0.07, 0.08),
    "CityTaxi": (0.95, 0.75, 0.05), "CityBus": (0.82, 0.84, 0.86), "Glass": (0.18, 0.24, 0.29),
    "KitDark": (0.16, 0.17, 0.18), "Bark": (0.42, 0.3, 0.2), "Concrete": (0.64, 0.64, 0.62),
    "Foliage": (0.27, 0.39, 0.18), "Seat": (0.06, 0.42, 0.62), "Skin": (0.74, 0.57, 0.45),
    "BalconyFlag": (0.89, 0.04, 0.09), "CityShirt0": (0.85, 0.85, 0.84), "CityShirt1": (0.12, 0.16, 0.28), "CityTrousers0": (0.12, 0.14, 0.2),
}


def load(path):
    verts, faces, mats, mat = [], [], [], "Carbon"
    for line in open(path):
        if line.startswith("v "):
            verts.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("usemtl"):
            mat = line.split()[1]
        elif line.startswith("f "):
            faces.append([int(x.split("/")[0]) - 1 for x in line.split()[1:4]])
            mats.append(mat)
    return np.array(verts), np.array(faces), mats


def view(ax, V, F, M, fwd, up, title):
    fwd = np.array(fwd, float); fwd /= np.linalg.norm(fwd)
    right = np.cross(up, fwd); right /= np.linalg.norm(right)
    up2 = np.cross(fwd, right)
    a, b, c = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    n = np.cross(b - a, c - a)
    norm = np.linalg.norm(n, axis=1) + 1e-12
    n = n / norm[:, None]
    visible = (n @ -fwd) > 0
    light = np.array([0.4, 0.8, -0.45]); light /= np.linalg.norm(light)
    shade = 0.35 + 0.65 * np.clip(n @ light, 0, 1)
    depth = ((a + b + c) / 3) @ fwd
    order = np.argsort(-depth)
    polys, cols = [], []
    for i in order:
        if not visible[i]:
            continue
        tri = np.stack([a[i], b[i], c[i]])
        polys.append(np.stack([tri @ right, tri @ up2], axis=1))
        cols.append(tuple(np.clip(np.array(COLORS.get(M[i], (1, 0, 1))) * shade[i], 0, 1)))
    ax.add_collection(PolyCollection(polys, facecolors=cols, edgecolors=cols, linewidths=0.2))
    pts = np.concatenate(polys)
    ax.set_xlim(pts[:, 0].min() - 0.1, pts[:, 0].max() + 0.1)
    ax.set_ylim(pts[:, 1].min() - 0.1, pts[:, 1].max() + 0.1)
    ax.set_aspect("equal")
    ax.set_title(title, fontsize=9)
    ax.axis("off")


def cockpit(ax, V, F, M, eye, fov_deg=70.0, aspect=16 / 9):
    """Perspective onboard view from eye (car space) looking along +z."""
    eye = np.array(eye, float)
    a, b, c = V[F[:, 0]] - eye, V[F[:, 1]] - eye, V[F[:, 2]] - eye
    n = np.cross(b - a, c - a)
    n /= (np.linalg.norm(n, axis=1) + 1e-12)[:, None]
    centre = (a + b + c) / 3
    visible = (np.einsum("ij,ij->i", n, -centre) > 0) & (a[:, 2] > 0.05) & (b[:, 2] > 0.05) & (c[:, 2] > 0.05)
    light = np.array([0.4, 0.8, -0.45]); light /= np.linalg.norm(light)
    shade = 0.35 + 0.65 * np.clip(n @ light, 0, 1)
    f = 1 / np.tan(np.radians(fov_deg) / 2)
    order = np.argsort(-np.linalg.norm(centre, axis=1))
    polys, cols = [], []
    for i in order:
        if not visible[i]:
            continue
        tri = np.stack([a[i], b[i], c[i]])
        polys.append(np.stack([tri[:, 0] / tri[:, 2] * f / aspect, tri[:, 1] / tri[:, 2] * f], axis=1))
        cols.append(tuple(np.clip(np.array(COLORS.get(M[i], (1, 0, 1))) * shade[i], 0, 1)))
    ax.add_collection(PolyCollection(polys, facecolors=cols, edgecolors=cols, linewidths=0.2))
    ax.set_xlim(-1, 1); ax.set_ylim(-1, 1)
    ax.set_aspect(1 / aspect)
    ax.set_facecolor((0.75, 0.85, 0.95))
    ax.set_xticks([]); ax.set_yticks([])
    ax.set_title("Cockpit-Kamera (70° FOV)", fontsize=9)


def main():
    V, F, M = load(sys.argv[1])
    fig, axes = plt.subplots(3, 2, figsize=(14, 13))
    views = [
        ((-1, 0, 0), (0, 1, 0), "Seite (von rechts)"),
        ((0, -1, 0), (0, 0, 1), "Draufsicht"),
        ((0, 0, -1), (0, 1, 0), "Front"),
        ((0, 0, 1), (0, 1, 0), "Heck"),
        ((-0.7, -0.35, -0.62), (0, 1, 0), "3/4 vorne rechts"),
        ((0.7, -0.4, 0.6), (0, 1, 0), "3/4 hinten links"),
    ]
    for ax, (f, u, t) in zip(axes.flat, views):
        view(ax, V, F, M, f, np.array(u, float), t)
    if len(sys.argv) > 3:
        fig2, ax2 = plt.subplots(figsize=(10, 5.8))
        cockpit(ax2, V, F, M, [float(x) for x in sys.argv[3].split(",")])
        fig2.tight_layout()
        fig2.savefig(sys.argv[2].replace(".png", "_cockpit.png"), dpi=110)
    fig.suptitle("Prozeduraler Platzhalter (kein finales Modell) – " + sys.argv[1].split("/")[-1], fontsize=11)
    fig.tight_layout()
    fig.savefig(sys.argv[2], dpi=110)


if __name__ == "__main__":
    main()
