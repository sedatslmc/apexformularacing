#!/usr/bin/env python3
"""Vertical alignment (height profile) of the Bosphorus GP, designed for a low formula car.

The Copernicus street-level profile is only a geographic reference. The drivable profile is the
closed curve h(s) that stays as close to it as possible while meeting, at every point:

* |grade| <= GRADE_MAX,
* crest curvature  -h'' <= max_crest(s)  and  sag curvature  h'' <= max_sag(s),

where max_crest/max_sag come from the vehicle and the racing-line speed at that point
(`dotnet run -- limits <track> <csv>` in build/Apex.Tuning, class Apex.Tracks.VerticalLimits:
body-collider clearance, aero compression over speed, wheel unloading on crests).

Method: one quadratic programme on a periodic 5 m grid: stay close to the reference (least squares)
with a small curvature penalty (smoothing spline, continuous slope), subject to the grade and the
speed-dependent crest/sag limits as hard constraints. Solved with ADMM (numpy only). The large
features (Yildiz climb, Barbaros descent) stay; only what is too steep or too abrupt is rounded off,
and the deviation from the reference is reported.
"""
import csv
import math

import numpy as np

STEP = 5.0            # design grid (m)
GRADE_MAX = 0.11      # stays below the 12.5 %/50 m test limit with margin
LAMBDA = 60.0         # curvature penalty (second differences on the 5 m grid): removes < ~60 m wiggles
LAMBDA3 = 400.0       # penalty on the change of curvature (third differences): curvature eases in and out
SAFETY = 0.85         # design to 85 % of the vehicle curvature limits
PAIR_TOL = 0.05       # side-by-side legs of the same street: equal height within ±5 cm
MIN_KAPPA = 1.0 / 20000.0   # where the vehicle allows ~nothing (top speed) keep at least this (R 20 km)


def periodic_interp(s_ref, h_ref, L, s):
    s_ref = np.asarray(s_ref, float)
    h_ref = np.asarray(h_ref, float)
    xs = np.concatenate([s_ref - L, s_ref, s_ref + L])
    hs = np.concatenate([h_ref, h_ref, h_ref])
    return np.interp(np.mod(s, L), xs, hs)


def load_limits(path, L, s):
    rows = list(csv.DictReader(open(path)))
    ls = np.array([float(r["lap_s"]) for r in rows])
    crest = np.array([float(r["max_crest"]) for r in rows])
    sag = np.array([float(r["max_sag"]) for r in rows])
    speed = np.array([float(r["speed"]) for r in rows])
    c = periodic_interp(ls, crest, L, s)
    g = periodic_interp(ls, sag, L, s)
    v = periodic_interp(ls, speed, L, s)
    return np.maximum(c * SAFETY, MIN_KAPPA), np.maximum(g * SAFETY, MIN_KAPPA), v


def d1_matrix(n):
    D = np.zeros((n, n))
    for i in range(n):
        D[i, i] = -1.0
        D[i, (i + 1) % n] = 1.0
    return D


def d2_matrix(n):
    D = np.zeros((n, n))
    for i in range(n):
        D[i, (i - 1) % n] = 1.0
        D[i, i] = -2.0
        D[i, (i + 1) % n] = 1.0
    return D


def design(s_ref, h_ref, L, limits_csv, pairs=(), log=print):
    """Quadratic programme: min 1/2 |h - r|^2 + LAMBDA/2 |D2 h|^2 + LAMBDA3/2 |D3 h|^2  s.t.  grade and curvature bounds.

    `pairs` = (s_a, s_b) lap distances that lie side by side on the same street (both legs of a
    shared road); their heights are tied to within PAIR_TOL, or a tolerance given as a third element.
    Solved with ADMM (the OSQP iteration) on row-scaled constraints; one dense factorisation."""
    n = int(round(L / STEP))
    ds = L / n
    s = np.arange(n) * ds
    r = periodic_interp(s_ref, h_ref, L, s)
    kc, kg, v = load_limits(limits_csv, L, s)
    D1 = d1_matrix(n) / ds
    D2 = d2_matrix(n) / ds ** 2
    # Constraints l <= C h <= u, each row scaled to unit bound.
    rows = [D1 / GRADE_MAX, D2 / np.maximum(kc, kg)[:, None]]
    lo = [-np.ones(n), -kc / np.maximum(kc, kg)]
    hi = [np.ones(n), kg / np.maximum(kc, kg)]
    if len(pairs):
        E = np.zeros((len(pairs), n))
        tol = np.array([p[2] if len(p) > 2 else PAIR_TOL for p in pairs])
        for k, (sa, sb) in enumerate(p[:2] for p in pairs):
            for sv, sign in ((sa, 1.0), (sb, -1.0)):
                x = (sv % L) / ds
                i = int(x) % n
                t = x - int(x)
                E[k, i] += sign * (1 - t)
                E[k, (i + 1) % n] += sign * t
        rows.append(E / tol[:, None])
        lo.append(-np.ones(len(pairs)))
        hi.append(np.ones(len(pairs)))
    C = np.vstack(rows)
    lo = np.concatenate(lo)
    hi = np.concatenate(hi)
    D3 = (d2_matrix(n) @ d1_matrix(n)) / ds ** 3
    Pm = np.eye(n) + LAMBDA * ds ** 4 * (D2.T @ D2) + LAMBDA3 * ds ** 6 * (D3.T @ D3)
    q = -r
    rho, sigma = 2.0, 1e-6
    K = np.linalg.inv(Pm + sigma * np.eye(n) + rho * (C.T @ C))
    x = r.copy()
    z = np.clip(C @ x, lo, hi)
    y = np.zeros(len(lo))
    for it in range(20000):
        x = K @ (sigma * x - q + C.T @ (rho * z - y))
        Cx = C @ x
        z_new = np.clip(Cx + y / rho, lo, hi)
        y += rho * (Cx - z_new)
        prim = np.abs(Cx - z_new).max()
        dual = rho * np.abs(C.T @ (z_new - z)).max()
        z = z_new
        if it % 1000 == 0:
            log(f"  iter {it}: primal {prim:.2e} dual {dual:.2e} max |dev| {np.abs(x - r).max():.2f} m")
        if prim < 1e-5 and dual < 1e-5:
            break
    h = x
    # Strict feasibility: a final pass of tiny corrections is not needed if the residual is below 1e-4 (scaled).
    k = D2 @ h
    gr = D1 @ h
    viol = max(((-k) / kc).max(), (k / kg).max(), (np.abs(gr) / GRADE_MAX).max())
    if len(pairs):
        pv = np.abs(E @ h).max()
        viol_pairs = (np.abs(E @ h) / tol).max()
        log(f"  {len(pairs)} side-by-side pairs, largest height difference {pv * 100:.1f} cm")
        viol = max(viol, viol_pairs)
    log(f"  result after {it} iterations: grade max {np.abs(gr).max() * 100:.2f} %, crest R min {1 / max((-k).max(), 1e-9):.0f} m, "
        f"sag R min {1 / max(k.max(), 1e-9):.0f} m, worst use of a limit {viol * 100:.1f} %, height {h.min():.1f}..{h.max():.1f} m "
        f"(reference {r.min():.1f}..{r.max():.1f} m), deviation max {np.abs(h - r).max():.2f} m, rms {np.sqrt(np.mean((h - r) ** 2)):.2f} m")
    if viol > 1.01:
        raise SystemExit("vertical alignment: constraints not met")
    return s, h, r


def evaluate_c2(s_grid, h_grid, L, s):
    """Profile height at lap distances s: periodic cubic spline through the design grid (C2). Its curvature is
    linear between the nodes, so it never exceeds the designed node curvatures (the C1 Hermite form can
    overshoot next to a change of curvature)."""
    h = np.asarray(h_grid, float)
    n = len(h)
    ds = L / n
    # Second derivatives M of the periodic spline on the uniform grid: M[i-1] + 4 M[i] + M[i+1] = 6 D2h[i].
    A = 4 * np.eye(n) + np.roll(np.eye(n), 1, axis=1) + np.roll(np.eye(n), -1, axis=1)
    M = np.linalg.solve(A, 6 * (np.roll(h, -1) - 2 * h + np.roll(h, 1)) / ds ** 2)
    x = np.asarray(s, float) % L
    i = (x // ds).astype(int) % n
    j = (i + 1) % n
    t = (x - i * ds) / ds
    return (1 - t) * h[i] + t * h[j] + ds ** 2 / 6 * (((1 - t) ** 3 - (1 - t)) * M[i] + (t ** 3 - t) * M[j])


def evaluate(s_grid, h_grid, L, s):
    """Profile height at lap distances s (periodic cubic Hermite on the design grid, C1)."""
    n = len(s_grid)
    ds = L / n
    m = (np.roll(h_grid, -1) - np.roll(h_grid, 1)) / (2 * ds)
    out = []
    for x in np.atleast_1d(s):
        x = x % L
        i = int(x // ds) % n
        t = (x - i * ds) / ds
        j = (i + 1) % n
        h00 = 2 * t ** 3 - 3 * t ** 2 + 1
        h10 = t ** 3 - 2 * t ** 2 + t
        h01 = -2 * t ** 3 + 3 * t ** 2
        h11 = t ** 3 - t ** 2
        out.append(h00 * h_grid[i] + h10 * ds * m[i] + h01 * h_grid[j] + h11 * ds * m[j])
    return np.array(out)
