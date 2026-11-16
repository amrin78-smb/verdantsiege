"""The Emberwild (biome B) environment kit: battlefield tiles, lane edge, lava channel, vent hazard cell and scenery.
Volcanic soil, glowing roots, ashfall, lava channels. Same contract as model_props.py (see Art/ART_BIBLE.md):
metres, Z up, root EMPTY at the ground centre, one `VS_Vertex` material, `Col` sRGB with ALPHA = emission mask.
Usage: blender -b --factory-startup --python Tools/Blender/model_emberwild.py -- [Name ...] (default all)."""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

# ------------------------------------------------------------------ palette (sRGB)
BAS0, BAS1, BAS2 = hexc("#3B3436"), hexc("#5C5050"), hexc("#8A7A74")      # basalt, warm greys
ASH0, ASH1 = hexc("#A39890"), hexc("#D6CCBE")                              # ash dusting
RUST0, RUST1, RUST2 = hexc("#7A3420"), hexc("#B0502C"), hexc("#DD8448")    # warm rust / terracotta
CHAR0, CHAR1 = hexc("#2A2120"), hexc("#55423A")                            # charred wood
OBS0, OBS1, OBS2 = hexc("#241E30"), hexc("#43385A"), hexc("#7C6A9C")       # obsidian
LAVA = [hexc("#B8301C"), hexc("#F25A1C"), hexc("#FF9A2E"), hexc("#FFE07A")]


def lava(t, a=1.0):
    """t 0 (deep red) .. 1 (yellow-white hot)."""
    t = max(0.0, min(1.0, t)) * 3
    i = min(int(t), 2)
    c = mix(LAVA[i], LAVA[i + 1], t - i)
    return (c[0], c[1], c[2], a)


def basalt(p, n, ash_k=0.72, rust=0.35, seed=0.0, lo=BAS0, hi=BAS2):
    c = mix(lo, hi, 0.5 + 0.38 * vnoise(p, 2.4, seed) + 0.12 * vnoise(p, 8.0, seed + 3))
    c = mix(c, mix(RUST0, RUST1, 0.5 + 0.5 * vnoise(p, 5, seed + 1)), smoothstep(0.15, 0.55, vnoise(p, 1.8, seed + 7)) * rust)
    c = mix(c, mix(ASH0, ASH1, 0.5 + 0.5 * vnoise(p, 6, seed + 2)), smoothstep(ash_k - 0.12, ash_k + 0.15, n.z) * 0.85)
    return c


def vpaint_idx(g, fn):
    """Per-vertex paint with averaged normals and the vertex index: fn(i, p, n) -> rgba."""
    acc = [Vector((0, 0, 0)) for _ in g.v]
    for fi, f in enumerate(g.f):
        nn = g.face_normal(fi)
        for i in f:
            acc[i] += nn
    vn = [a.normalized() if a.length > 1e-9 else Vector((0, 0, 1)) for a in acc]
    for fi, f in enumerate(g.f):
        g.c[fi] = [fn(i, g.v[i], vn[i]) for i in f]
    return g


def glow_root(pts, radii, sides=6, seam=0.45, amount=0.9, ends=True, seed=0.0, flat=(1.0, 1.0)):
    """Charred root tube with a molten seam along its upper side (brightest mid-length)."""
    g = tube(pts, radii, sides=sides, cap0=False, cap1=False, round1=True, flat=flat)
    n_pts = len(pts)
    sd = (len(g.v) - 1) // n_pts
    def fn(i, p, n):
        t = min(i // sd, n_pts - 1) / (n_pts - 1)
        bark = shade(mix(CHAR0, CHAR1, 0.5 + 0.5 * vnoise(p, 5, seed)), 1.0)
        k = smoothstep(seam, seam + 0.3, n.z) * (0.7 + 0.3 * math.sin(t * 17 + seed))
        if ends:
            k *= smoothstep(0.0, 0.18, t) * smoothstep(1.0, 0.8, t)
        c = mix(bark, lava(0.3 + 0.6 * k), smoothstep(0.05, 0.6, k))
        return (c[0], c[1], c[2], amount * smoothstep(0.1, 0.7, k))
    return vpaint_idx(g, fn)


def ash_blades(center, n, h, seed, spread=0.06, glow_tips=0.7):
    """Ash grass: grey-olive blades with ember-orange glowing tips."""
    rnd = random.Random(seed)
    g = Geo()
    c0 = Vector(center)
    for i in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        b = c0 + Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(0, spread)
        hh = h * rnd.uniform(0.7, 1.2)
        bl = blade(b, Vector((math.cos(a), math.sin(a), 0)), hh, 0.016, bend=rnd.uniform(0.2, 0.5))
        def fn(p, n_, fc, fi, z0=c0.z, hh=hh):
            t = smoothstep(z0, z0 + hh, p.z)
            c = mix(hexc("#4E4A3E"), hexc("#A79B80"), smoothstep(0, 0.7, t))
            k = smoothstep(0.72, 1.0, t)
            c = mix(c, lava(0.55), k)
            return (c[0], c[1], c[2], glow_tips * k)
        bl.paint(fn)
        g.extend(bl)
    return g


# ------------------------------------------------------------------ cracked plates (Voronoi slabs)
def _clip(poly, nx, ny, c):
    out = []
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        da = nx * a[0] + ny * a[1] - c
        db = nx * b[0] + ny * b[1] - c
        if da <= 0:
            out.append(a)
        if (da <= 0) != (db <= 0):
            t = da / (da - db)
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    clean = []
    for p in out:
        if not clean or math.hypot(p[0] - clean[-1][0], p[1] - clean[-1][1]) > 0.008:
            clean.append(p)
    if len(clean) > 2 and math.hypot(clean[0][0] - clean[-1][0], clean[0][1] - clean[-1][1]) <= 0.008:
        clean.pop()
    return clean


def voronoi(seeds, boundary, gap):
    """Convex cells (CCW) of `seeds` clipped to the convex CCW `boundary`, separated by `gap`."""
    cells = []
    for i, p in enumerate(seeds):
        poly = list(boundary)
        for j, q in enumerate(seeds):
            if i == j:
                continue
            dx, dy = q[0] - p[0], q[1] - p[1]
            L = math.hypot(dx, dy)
            nx, ny = dx / L, dy / L
            c = nx * (p[0] + q[0]) / 2 + ny * (p[1] + q[1]) / 2 - gap / 2
            poly = _clip(poly, nx, ny, c)
            if len(poly) < 3:
                break
        cells.append(poly)
    return cells


def circle_poly(r, n=16, phase=0.0):
    return [(r * math.cos(2 * math.pi * k / n + phase), r * math.sin(2 * math.pi * k / n + phase)) for k in range(n)]


def slab(poly, h, cham, z0=0.0, bottom=False):
    """Extruded convex polygon with a soft chamfered top rim (smooth top edge, crisp corners). Bottom face optional."""
    cx = sum(p[0] for p in poly) / len(poly); cy = sum(p[1] for p in poly) / len(poly)
    g = Geo()
    n = len(poly)
    def inset(p, d):
        vx, vy = cx - p[0], cy - p[1]
        L = math.hypot(vx, vy)
        k = min(d / L, 0.45) if L > 1e-9 else 0.0
        return (p[0] + vx * k, p[1] + vy * k)
    levels = [(z0, 0.0)] + ([(z0 + h - cham, 0.0)] if h > cham * 1.2 else []) + [(z0 + h, min(cham, h * 0.6))]
    rings = []
    for zz, d in levels:
        ring = []
        for p in poly:
            q = inset(p, d)
            ring.append(len(g.v)); g.v.append(Vector((q[0], q[1], zz)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for s in range(n):
            t = (s + 1) % n
            g.add_face([a[s], a[t], b[t], b[s]], True)
    g.add_face(rings[-1], True)
    if bottom:
        g.add_face(rings[0][::-1], False)
    return g, Vector((cx, cy, z0))


def hot_plate(poly, h, cham, seed, hot=0.9, ash_k=0.8, lo=BAS0, hi=BAS2, bottom=False):
    """A crust plate whose lower sides glow (heat coming up through the cracks)."""
    g, c = slab(poly, h, cham, bottom=bottom)
    def fn(p, n, fc, fi):
        base = basalt(p, n, ash_k, 0.4, seed, lo, hi)
        k = smoothstep(h * 0.85, 0.0, p.z) * smoothstep(0.75, 0.2, abs(n.z)) * hot
        col = mix(base, lava(0.35 + 0.5 * k), smoothstep(0.0, 0.7, k))
        return (col[0], col[1], col[2], k)
    paint_vertex_normals(g, fn)
    return g, c


MODELS = {}
def model(fn):
    MODELS[fn.__name__] = fn
    return fn


# ------------------------------------------------------------------ battlefield: tiles
H_EDGE, H_MID, INSET, TN = 0.05, 0.062, 0.035, 6


def tile_height(x, y):
    d = max(abs(x), abs(y)) / (0.5 * (1 - 2 * INSET))
    return H_MID - (H_MID - H_EDGE) * min(1.0, d) ** 3


def tile_base(seed, paint_fn):
    """Same footprint/profile as GroundTileA/B (domed 6x6 top, rolled edge, 1.0 x 1.0 m, rim 0.05 m)."""
    g = Geo()
    idx = {}
    for i in range(TN + 1):
        for j in range(TN + 1):
            u = -0.5 + i / TN; v = -0.5 + j / TN
            x = u * (1 - 2 * INSET); y = v * (1 - 2 * INSET)
            d = max(abs(u), abs(v)) * 2
            z = H_MID - (H_MID - H_EDGE) * d ** 3 + 0.002 * math.sin(i * 1.7 + j * 2.3 + seed)
            idx[(i, j)] = len(g.v); g.v.append(Vector((x, y, z)))
    for i in range(TN):
        for j in range(TN):
            g.add_face([idx[(i, j)], idx[(i + 1, j)], idx[(i + 1, j + 1)], idx[(i, j + 1)]], True)
    ring = [idx[(i, 0)] for i in range(TN + 1)] + [idx[(TN, j)] for j in range(1, TN + 1)] + \
           [idx[(i, TN)] for i in range(TN - 1, -1, -1)] + [idx[(0, j)] for j in range(TN - 1, 0, -1)]
    prev = ring
    for (grow, zf) in ((0.55, 0.62), (1.0, 0.0)):
        nxt = []
        for k in ring:
            p = g.v[k]
            ox = math.copysign(INSET * grow, p.x) if abs(abs(p.x) - 0.5 * (1 - 2 * INSET)) < 1e-6 else 0.0
            oy = math.copysign(INSET * grow, p.y) if abs(abs(p.y) - 0.5 * (1 - 2 * INSET)) < 1e-6 else 0.0
            nxt.append(len(g.v)); g.v.append(Vector((p.x + ox, p.y + oy, p.z * zf)))
        for k in range(len(ring)):
            a_, b_ = prev[k], prev[(k + 1) % len(ring)]
            c_, d_ = nxt[(k + 1) % len(ring)], nxt[k]
            g.add_face([a_, d_, c_, b_], True)
        prev = nxt
    paint_vertex_normals(g, paint_fn)
    return g


def ember_tile(name, lo, hi, seed, vein, pebbles):
    root = new_scene(name)
    def tp(p, n, fc, fi):
        c = mix(lo, hi, 0.5 + 0.45 * vnoise(p, 3.0, seed) + 0.15 * vnoise(p, 9, seed + 4))
        c = mix(c, ASH0, smoothstep(0.2, 0.7, vnoise(p, 2.6, seed + 9)) * 0.45)       # soft ash drifts
        c = mix(c, shade(RUST0, 0.9), smoothstep(0.25, 0.7, vnoise(p, 3.1, seed + 17)) * 0.4)   # scorched patches
        if n.z < 0.75:
            c = mix(c, shade(lo, 0.72), smoothstep(0.75, 0.2, n.z))
        return c
    g = tile_base(seed, tp)
    # one glowing root vein, half buried in the soil
    pts = [Vector((x, y, tile_height(x, y) - 0.003)) for (x, y) in vein]
    pts = bezier(*pts, n=5)
    for p in pts:
        p.z = tile_height(p.x, p.y) - 0.004
    g.extend(glow_root(pts, taper(len(pts), 0.016, 0.009), sides=4, seam=0.3, amount=0.6, seed=seed, flat=(1.0, 0.6)))
    for k, (x, y, r) in enumerate(pebbles):
        pb = ellipsoid((x, y, tile_height(x, y) - 0.008), (r, r * 0.85, r * 0.6), sides=7, rings=4)   # 42 tris
        paint_vertex_normals(pb, lambda p, n, fc, fi, k=k: shade(basalt(p, n, 1.2, 0.3, k), 0.8))
        g.extend(pb)
    part("Mesh", g, root)
    finish(root, "Battlefield", view=(30, 45))

@model
def EmberTileA(): ember_tile("EmberTileA", hexc("#A06A4C"), hexc("#C48A62"), 1,
                             [(-0.36, 0.2), (-0.22, 0.12), (-0.12, 0.2), (0.02, 0.3)], [(0.22, -0.26, 0.045), (-0.25, -0.2, 0.03)])
@model
def EmberTileB(): ember_tile("EmberTileB", hexc("#7A4E40"), hexc("#9A6650"), 2,
                             [(0.34, -0.36), (0.24, -0.22), (0.1, -0.24), (0.0, -0.12)], [(-0.27, -0.22, 0.04), (0.28, 0.25, 0.032)])


# ------------------------------------------------------------------ battlefield: lane edge (basalt columns)
def hex_column(c, r, h, seed, cham=0.022, sides=6, tilt=(0.0, 0.0)):
    prof = [(r, 0.0), (r, min(0.07, h * 0.3)), (r, h - cham), (r - cham * 0.9, h), (0.0, h + 0.004)]
    g = lathe(prof, sides, smooth=True, cap_bottom=False, boost=False,
              jitter=lambda pi, si, a: 1.0 + 0.06 * math.sin(si * 2.3 + seed))
    g.transform(rot(0, 0, (seed * 37) % 60))
    g.displace(lambda p: Vector((0, 0, (p.x * tilt[0] + p.y * tilt[1]) * smoothstep(0, h, p.z))))
    return g.transform(T(*c))


@model
def EmberLaneEdge():
    root = new_scene("EmberLaneEdge")
    rnd = random.Random(71)
    g = Geo()
    cols = []
    x = -0.44
    k = 0
    while x < 0.46:
        r = rnd.uniform(0.075, 0.105)
        h = rnd.uniform(0.13, 0.3) if k % 3 else rnd.uniform(0.22, 0.34)
        y = rnd.uniform(-0.05, 0.05) + (0.03 if k % 2 else -0.03)
        cols.append((x, y, r, h))
        x += r * 1.75; k += 1
    for k, (x, y, r, h) in enumerate(cols):
        x = min(x, 0.5 - r)
        col = hex_column((x, y, 0), r, h, k, tilt=(rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25)))
        paint_vertex_normals(col, lambda p, n, fc, fi, h=h, k=k: ao_ground(lambda p, n, fc, fi: basalt(p, n, 0.8, 0.3, k), 0, 0.12, 0.6)(p, n, fc, fi))
        g.extend(col)
    # short secondary columns in front/back
    for k, (x, y, r, h) in enumerate(((-0.3, -0.16, 0.06, 0.09), (0.12, 0.15, 0.065, 0.11), (0.36, -0.15, 0.055, 0.07))):
        col = hex_column((x, y, 0), r, h, 20 + k)
        paint_vertex_normals(col, lambda p, n, fc, fi, k=k: basalt(p, n, 0.8, 0.3, 20 + k))
        g.extend(col)
    # glowing root threading along the base, plus ember pebbles in the gaps
    pts = bezier((-0.5, -0.13, 0.0), (-0.15, -0.2, 0.09), (0.15, 0.05, -0.02), (0.5, -0.12, 0.03), n=8)
    g.extend(glow_root(pts, taper(len(pts), 0.03, 0.022), sides=6, seam=0.5, amount=0.85, ends=False, seed=3))
    for k, (x, y) in enumerate(((-0.17, 0.13), (0.27, 0.12), (0.03, -0.13))):
        e = pebble((x, y, 0.0), (0.035, 0.03, 0.03), seed=40 + k, lump=0.2)
        e.paint(lambda p, n, fc, fi: lava(0.5 + 0.4 * n.z, 0.9))
        g.extend(e)
    g.extend(ash_blades((-0.36, 0.16, 0), 5, 0.17, 2, 0.04))
    g.extend(ash_blades((0.4, 0.17, 0), 4, 0.14, 3, 0.04))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Battlefield")


# ------------------------------------------------------------------ battlefield: lava channel (tiles along X)
@model
def EmberLavaChannel():
    """1.0 m long segment along X (tiles seamlessly: all shape/colour variation is periodic in X, ends open),
    0.86 m wide (Y): two rounded basalt banks with a sunken flowing lava ribbon and a few floating crust plates."""
    root = new_scene("EmberLavaChannel")
    NX = 16
    def per(x, k, ph):          # periodic in X with period 1.0
        return math.sin(2 * math.pi * k * (x + 0.5) + ph)
    g = Geo()
    # banks: profile (y offset from the outer edge, z), outer -> inner
    prof = [(0.0, 0.0), (0.012, 0.07), (0.045, 0.118), (0.1, 0.138), (0.16, 0.122), (0.2, 0.08), (0.225, 0.035), (0.24, 0.0)]
    W = 0.43
    for side in (-1, 1):
        rows = []
        for i in range(NX + 1):
            x = -0.5 + i / NX
            row = []
            for j, (dy, z) in enumerate(prof):
                wob = 0.018 * per(x, 2, side + j * 0.3) + 0.01 * per(x, 3, 1.7 * side)
                hz = 1.0 + 0.16 * per(x, 2, side * 2.1) + 0.08 * per(x, 5, side)
                y = side * (W - dy - (wob if 0 < j else 0.0))
                row.append(len(g.v)); g.v.append(Vector((x, y, z * (hz if 0 < j < len(prof) - 1 else 1.0))))
            rows.append(row)
        g.add_face(rows[0] if side < 0 else rows[0][::-1], True)          # end caps (hidden when segments abut)
        g.add_face(rows[NX][::-1] if side < 0 else rows[NX], True)
        for i in range(NX):
            for j in range(len(prof) - 1):
                a, b, c, d = rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]
                g.add_face([a, b, c, d] if side < 0 else [a, d, c, b], True)
    def bank(p, n, fc, fi):
        c = mix(BAS0, BAS2, 0.55 + 0.25 * per(p.x, 3, p.y * 7) + 0.12 * per(p.x, 7, p.y * 3))
        c = mix(c, mix(ASH0, ASH1, 0.5 + 0.5 * per(p.x, 4, 1)), smoothstep(0.7, 0.95, n.z) * 0.8)
        heat = smoothstep(0.31, 0.2, abs(p.y)) * smoothstep(0.1, 0.0, p.z)
        c = mix(c, lava(0.3 + 0.4 * heat), heat)
        return (c[0], c[1], c[2], heat * 0.85)
    paint_vertex_normals(g, bank)
    # lava ribbon (extends under the banks)
    lv = Geo()
    NY = 6
    LW = 0.23
    grid = {}
    for i in range(NX + 1):
        for j in range(NY + 1):
            x = -0.5 + i / NX; y = -LW + 2 * LW * j / NY
            z = 0.04 + 0.006 * per(x, 3, y * 9) + 0.004 * per(x, 5, 2 + y * 5)
            grid[(i, j)] = len(lv.v); lv.v.append(Vector((x, y, z)))
    for i in range(NX):
        for j in range(NY):
            lv.add_face([grid[(i, j)], grid[(i + 1, j)], grid[(i + 1, j + 1)], grid[(i, j + 1)]], True)
    def lp(p, n, fc, fi):
        centre = 1.0 - smoothstep(0.0, LW, abs(p.y + 0.03 * per(p.x, 2, 0.5)))
        streak = 0.5 + 0.5 * per(p.x, 3, p.y * 14)
        return lava(0.25 + 0.55 * centre + 0.2 * streak * centre)
    paint_vertex_normals(lv, lp)
    # end skirts (sheet edge down to the ground) so an exposed channel end never shows a see-through gap
    for i, sgn in ((0, -1), (NX, 1)):
        base = []
        for j in range(NY + 1):
            q = lv.v[grid[(i, j)]]
            base.append(len(lv.v)); lv.v.append(Vector((q.x, q.y, 0.0)))
        for j in range(NY):
            quad = [grid[(i, j)], grid[(i, j + 1)], base[j + 1], base[j]]
            lv.add_face(quad if sgn < 0 else quad[::-1], True, lava(0.15, 0.8))
    g.extend(lv)
    # floating crust plates (kept off the ends so the strip still tiles)
    rnd = random.Random(81)
    for k, (cx, cy, r) in enumerate(((-0.28, 0.07, 0.07), (0.05, -0.08, 0.09), (0.3, 0.09, 0.06), (0.12, 0.12, 0.04))):
        poly = [(cx + r * (1 + 0.25 * math.sin(3 * a + k)) * math.cos(a), cy + r * 0.8 * (1 + 0.25 * math.sin(3 * a + k)) * math.sin(a))
                for a in [2 * math.pi * q / 7 for q in range(7)]]
        pl, _ = hot_plate(poly, 0.03, 0.012, k, hot=1.0, ash_k=0.85)
        pl.transform(T(0, 0, 0.032))
        g.extend(pl)
    part("Mesh", g, root)
    finish(root, "Battlefield", view=(60, 40))


# ------------------------------------------------------------------ battlefield: vent hazard cell
@model
def EmberVentCell():
    """Temporarily unsafe cell: a ring of heaved basalt crust plates around a vent, molten light in every crack.
    Crust = plates (always visible); Fissure = the molten layer + vent bubble under them (hide/pulse it to show the
    hazard's state). Origin = cell centre at the surface it sits on (put the root on the tile top, ~0.06 m)."""
    root = new_scene("EmberVentCell")
    rnd = random.Random(91)
    R = 0.46
    seeds = [(0.0, 0.0)]
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.3
        rr = 0.15 + rnd.uniform(-0.02, 0.02)
        seeds.append((rr * math.cos(a), rr * math.sin(a)))
    for k in range(10):
        a = 2 * math.pi * k / 10 + rnd.uniform(-0.12, 0.12)
        rr = 0.35 + rnd.uniform(-0.03, 0.03)
        seeds.append((rr * math.cos(a), rr * math.sin(a)))
    cells = voronoi(seeds, circle_poly(R, 20), 0.032)
    crust = Geo()
    for k, poly in enumerate(cells):
        if k == 0 or len(poly) < 3:
            continue                                  # centre cell = the vent mouth
        cx = sum(p[0] for p in poly) / len(poly); cy = sum(p[1] for p in poly) / len(poly)
        rc = math.hypot(cx, cy)
        inner = 1.0 - smoothstep(0.1, R, rc)
        h = 0.03 + 0.06 * inner
        pl, c = hot_plate(poly, h, 0.016, k, hot=1.0, ash_k=0.82, bottom=True)
        # heave: lift the inner edge (plates pushed up and outward by the vent)
        ang = 6 + 16 * inner
        ax = Vector((-cy, cx, 0)).normalized()
        pl.transform(T(cx, cy, 0) @ Matrix.Rotation(math.radians(ang), 4, ax) @ T(-cx, -cy, 0))
        lo = min(p.z for p in pl.v)
        pl.transform(T(0, 0, -lo))
        crust.extend(pl)
    part("Crust", crust, root)
    # molten layer: a slightly domed disc (top-facing fan), white-hot at the vent, red at the rim
    prof = [(R - 0.01, 0.004), (0.36, 0.008), (0.24, 0.012), (0.14, 0.018), (0.09, 0.034), (0.05, 0.05), (0.0, 0.056)]
    fis = lathe(prof, 18, smooth=True, cap_bottom=False, cap_top=False,
                jitter=lambda pi, si, a: 1.0 + (0.12 * math.sin(a * 3 + 1) if pi >= 3 else 0.0))
    fis.paint(lambda p, n, fc, fi: lava(1.0 - smoothstep(0.03, R, math.hypot(p.x, p.y)) * 0.85))
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.4
        b = sphere((0.075 * math.cos(a), 0.075 * math.sin(a), 0.045), 0.02 + 0.006 * k, sides=6, rings=4)
        b.fill(lava(0.95))
        fis.extend(b)
    part("Fissure", fis, root)
    finish(root, "Battlefield", view=(40, 50))


# ------------------------------------------------------------------ props: rocks
@model
def EmberRockA():
    """Split basalt boulder: two rounded halves leaning apart around a molten core, plus a small companion stone."""
    root = new_scene("EmberRockA")
    g = Geo()
    for k, (x, ang, rad, sd) in enumerate(((-0.2, -9, (0.36, 0.42, 0.42), 4), (0.23, 11, (0.3, 0.38, 0.34), 5))):
        h = pebble((0, 0, rad[2] * 0.7), rad, seed=sd, lump=0.13)
        # flatten the inner (split) face so the halves read as one broken boulder
        side = 1 if x < 0 else -1
        h.displace(lambda p, side=side, r=rad[0]: Vector((-(p.x * side - r * 0.45) * side if p.x * side > r * 0.45 else 0.0, 0, 0)))
        h.transform(T(x, 0, 0) @ rot(0, ang, 0))
        def fn(p, n, fc, fi, x=x):
            c = basalt(p, n, 0.7, 0.35, 1.0 + x)
            heat = smoothstep(0.5, 0.95, n.dot(Vector((-1 if x > 0 else 1, 0, 0)))) * smoothstep(0.12, 0.0, abs(p.x - 0.02))
            heat = max(heat, smoothstep(0.08, 0.0, abs(p.x - 0.02)) * 0.8)
            c = mix(c, lava(0.35 + 0.5 * heat), heat)
            return (c[0], c[1], c[2], heat)
        paint_vertex_normals(h, fn)
        g.extend(h)
    core = soft_blob((0.02, 0, 0.3), (0.12, 0.3, 0.3), seed=7, lump=0.2, sides=8, rings=6)
    core.paint(lambda p, n, fc, fi: lava(0.75 + 0.25 * smoothstep(0.0, 0.5, p.z)))
    g.extend(core)
    st = pebble((0.5, -0.32, 0.08), (0.15, 0.13, 0.11), seed=14, lump=0.12)
    paint_vertex_normals(st, lambda p, n, fc, fi: basalt(p, n, 0.7, 0.35, 2.0))
    g.extend(st)
    for k, (x, y) in enumerate(((0.05, -0.42), (-0.1, 0.45))):
        e = ellipsoid((x, y, 0.0), (0.04, 0.035, 0.03), sides=6, rings=4)
        e.paint(lambda p, n, fc, fi: lava(0.5 + 0.4 * n.z, 0.9))
        g.extend(e)
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")


@model
def EmberRockB():
    """Basalt column cluster: chunky hexagonal columns with ash-dusted tops and an ember glow at the foot."""
    root = new_scene("EmberRockB")
    rnd = random.Random(7)
    g = Geo()
    spots = [(0.0, 0.0, 0.2, 1.05), (0.3, 0.12, 0.17, 0.78), (-0.25, 0.18, 0.18, 0.62), (0.08, -0.3, 0.16, 0.5),
             (-0.2, -0.22, 0.14, 0.36), (0.34, -0.2, 0.12, 0.26), (-0.42, -0.02, 0.12, 0.22)]
    for k, (x, y, r, h) in enumerate(spots):
        col = hex_column((x, y, 0), r, h, k, cham=0.04, tilt=(rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35)))
        col.transform(T(x, y, 0) @ rot(rnd.uniform(-4, 4), rnd.uniform(-4, 4), 0) @ T(-x, -y, 0))
        def fn(p, n, fc, fi, k=k):
            c = basalt(p, n, 0.8, 0.3, k)
            heat = smoothstep(0.12, 0.0, p.z) * smoothstep(0.8, 0.2, abs(n.z))
            c = mix(c, lava(0.45), heat * 0.8)
            return (c[0], c[1], c[2], heat * 0.7)
        paint_vertex_normals(col, fn)
        g.extend(col)
    for k in range(4):
        a = rnd.uniform(0, 2 * math.pi)
        pb = pebble((0.5 * math.cos(a), 0.45 * math.sin(a), 0), (0.07, 0.06, 0.05), seed=k + 60, lump=0.15)
        paint_vertex_normals(pb, lambda p, n, fc, fi, k=k: basalt(p, n, 0.8, 0.2, k + 61))
        g.extend(pb)
    g.extend(ash_blades((-0.36, 0.3, 0), 5, 0.2, 5, 0.05))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")


def shard(base, direction, r, length, sides=5, seed=0):
    """Faceted crystal: prism body with a pointed tip (flat shaded)."""
    d = Vector(direction).normalized()
    prof = [(r * 0.75, -0.02), (r, length * 0.12), (r * 0.95, length * 0.68), (0.0, length)]
    g = lathe(prof, sides, smooth=False, cap_bottom=False, boost=False,
              jitter=lambda pi, si, a: 1.0 + 0.18 * math.sin(si * 2.7 + seed))
    g.transform(rot(0, 0, seed * 23))
    return g.transform(T(*base) @ align(d))


@model
def EmberRockC():
    """Obsidian shard cluster: glassy violet-black crystals with lit facets and a molten heart at the base."""
    root = new_scene("EmberRockC")
    g = Geo()
    base = pebble((0, 0, 0.06), (0.3, 0.26, 0.12), seed=33, lump=0.15)
    paint_vertex_normals(base, lambda p, n, fc, fi: basalt(p, n, 0.75, 0.3, 3))
    g.extend(base)
    sh = Geo()
    for k, (x, y, dx, dy, r, L) in enumerate(((0.0, 0.0, 0.05, 0.0, 0.1, 0.62), (0.12, 0.06, 0.6, 0.3, 0.07, 0.42),
                                              (-0.1, 0.08, -0.5, 0.4, 0.075, 0.45), (0.04, -0.12, 0.2, -0.6, 0.06, 0.34),
                                              (-0.12, -0.08, -0.5, -0.5, 0.05, 0.26))):
        sh.extend(shard((x, y, 0.04), (dx, dy, 1.0), r, L, 5, k))
    def fn(p, n, fc, fi):
        sun = Vector((0.4, -0.5, 0.75)).normalized()
        c = mix(OBS0, OBS1, smoothstep(-0.3, 0.8, n.dot(sun)))
        c = mix(c, OBS2, smoothstep(0.75, 0.98, n.dot(sun)) * 0.8)
        heat = smoothstep(0.16, 0.04, p.z)
        c = mix(c, lava(0.5), heat * 0.85)
        return (c[0], c[1], c[2], heat * 0.8)
    sh.paint(fn, per_face=True)
    g.extend(sh)
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")


# ------------------------------------------------------------------ props: glowing roots, flowers, grass, lava pool
@model
def EmberRoots():
    """Cluster of charred roots arching out of the ash, molten light along their backs, two glowing seed pods."""
    root = new_scene("EmberRoots")
    rnd = random.Random(17)
    g = Geo()
    knot = soft_blob((0, 0, 0.08), (0.24, 0.2, 0.16), seed=3, lump=0.2, sides=10, rings=6, flatten_bottom=True)
    paint_vertex_normals(knot, lambda p, n, fc, fi: mix(shade(mix(CHAR0, CHAR1, 0.5 + 0.5 * vnoise(p, 5)), 1.0), ASH0, smoothstep(0.75, 1.0, n.z) * 0.5))
    g.extend(knot)
    for k in range(6):
        a = 2 * math.pi * k / 6 + rnd.uniform(-0.25, 0.25)
        d = Vector((math.cos(a), math.sin(a), 0))
        L = rnd.uniform(0.45, 0.7); hgt = rnd.uniform(0.18, 0.38)
        p0 = d * 0.08 + Vector((0, 0, 0.1))
        p3 = d * L + Vector((0, 0, -0.06))
        side = Vector((-d.y, d.x, 0)) * rnd.uniform(-0.12, 0.12)
        pts = bezier(p0, p0 + d * 0.15 + Vector((0, 0, hgt)), p3 + side - d * 0.15 + Vector((0, 0, hgt * 0.8)), p3, n=5)
        g.extend(glow_root(pts, taper(len(pts), 0.06, 0.028), sides=6, seam=0.55, amount=0.95, seed=k))
    for k, (a, z) in enumerate(((0.9, 0.24), (3.6, 0.2))):
        c = Vector((0.16 * math.cos(a), 0.16 * math.sin(a), z))
        pod = ellipsoid(c, (0.06, 0.06, 0.075), sides=6, rings=3)
        pod.paint(lambda p, n, fc, fi, c=c: lava(0.6 + 0.4 * smoothstep(-0.05, 0.07, p.z - c.z)))
        g.extend(pod)
    g.extend(ash_blades((0.3, -0.3, 0), 4, 0.18, 9, 0.05))
    for k in range(2):
        a = rnd.uniform(0, 2 * math.pi)
        pb = pebble((0.5 * math.cos(a), 0.5 * math.sin(a), 0), (0.06, 0.05, 0.045), seed=k + 70, lump=0.15)
        paint_vertex_normals(pb, lambda p, n, fc, fi, k=k: basalt(p, n, 0.8, 0.3, k))
        g.extend(pb)
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")


def ember_flower(center, size, stem, lean, seed, petals=5):
    rnd = random.Random(seed)
    g = Geo()
    c = Vector(center)
    top = c + Vector((lean[0], lean[1], stem))
    pts = bezier(c, c + Vector((0, 0, stem * 0.55)), top, n=4)
    st = tube(pts, taper(len(pts), 0.012, 0.008), sides=3, smooth=False, cap0=False)
    st.paint(lambda p, n, fc, fi: mix(hexc("#3C4A2A"), hexc("#7E8A48"), smoothstep(c.z, top.z, p.z)))
    g.extend(st)
    for side in (-1, 1):
        lf = leaf(length=min(0.118, size * 1.5), width=size * 0.55, segs=3, across=1, curl=0.5, fold=0.3, thick=0.006)
        lf.transform(T(*(c + Vector((0, 0, stem * 0.25)))) @ rot(0, 0, 90 * side + seed * 40) @ rot(0, -30, 0))
        lf.paint(lambda p, n, fc, fi: mix(hexc("#3C4A2A"), hexc("#8C9A50"), smoothstep(0, 0.6, n.z)))
        g.extend(lf)
    head = Geo()
    for j in range(petals):
        pl = leaf(length=size, width=size * 0.62, segs=3, across=1, curl=0.75, fold=0.25, thick=0.008, tip_power=0.7)
        pl.paint(lambda p, n, fc, fi: lava(0.1 + 0.85 * smoothstep(0.0, size, p.x), 0.25 + 0.75 * smoothstep(0.2 * size, size, p.x)))
        pl.transform(rot(0, 0, 360 * j / petals + seed * 17) @ rot(0, -48 + 10 * (j % 2), 0))
        head.extend(pl)
    core = sphere((0, 0, size * 0.12), size * 0.22, sides=6, rings=4)
    core.fill(lava(1.0))
    head.extend(core)
    head.transform(T(*top) @ rot(lean[1] * -120, lean[0] * 120, 0))
    g.extend(head)
    return g


@model
def EmberFlowerCluster():
    """Ember-blooms: flame-shaped glowing cups on dark stems, among ash grass and cinders."""
    root = new_scene("EmberFlowerCluster")
    rnd = random.Random(19)
    g = Geo()
    for k in range(5):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0.1, 0.26) if k else 0.0
        g.extend(ember_flower((math.cos(a) * r, math.sin(a) * r, 0), rnd.uniform(0.085, 0.11), rnd.uniform(0.14, 0.3),
                              (rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04)), k))
    for k in range(2):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0.12, 0.3)
        g.extend(ash_blades((math.cos(a) * r, math.sin(a) * r, 0), 4, 0.14, k + 4, 0.04, glow_tips=0.4))
    for k in range(3):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0.15, 0.32)
        pb = ellipsoid((math.cos(a) * r, math.sin(a) * r, 0.0), (0.05, 0.045, 0.035), sides=7, rings=4)
        paint_vertex_normals(pb, lambda p, n, fc, fi, k=k: basalt(p, n, 0.8, 0.3, k))
        g.extend(pb)
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")


@model
def EmberGrassTuft():
    root = new_scene("EmberGrassTuft")
    g = ash_blades((0, 0, 0), 14, 0.3, 4, 0.14)
    g.extend(ash_blades((0.12, 0.08, 0), 6, 0.2, 7, 0.06))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")


@model
def EmberLavaPool():
    """Small round lava pool: a rim of rounded basalt stones around a gently domed glowing pool with crust islands."""
    root = new_scene("EmberLavaPool")
    rnd = random.Random(23)
    g = Geo()
    R = 0.5
    prof = [(R + 0.04, 0.012), (0.4, 0.03), (0.28, 0.04), (0.16, 0.046), (0.0, 0.05)]
    pool = lathe(prof, 16, smooth=True, cap_bottom=False, cap_top=False,
                 jitter=lambda pi, si, a: 1.0 + 0.07 * math.sin(a * 3 + 0.5) + 0.04 * math.sin(a * 5))
    pool.paint(lambda p, n, fc, fi: lava(0.95 - 0.6 * smoothstep(0.05, R + 0.04, math.hypot(p.x * 1.1 + 0.05, p.y)) + 0.08 * math.sin(p.x * 30 + p.y * 12)))
    g.extend(pool)
    # rounded basalt rim (one lumpy lathe ring: reads as fused stones) + a few accent boulders
    def rim_j(pi, si, a):
        return 1.0 + 0.07 * math.sin(a * 3 + 0.5) + 0.04 * math.sin(a * 5) + 0.035 * math.sin(a * 11 + 1.3)
    rim = lathe([(R + 0.2, 0.0), (R + 0.21, 0.05), (R + 0.16, 0.11), (R + 0.08, 0.13), (R + 0.01, 0.09), (R - 0.03, 0.03), (R - 0.04, 0.0)],
                18, smooth=True, cap_bottom=False, cap_top=False, jitter=rim_j)
    rim.displace(lambda p: Vector((0, 0, 0.045 * vnoise(p, 3.5, 2.0) * smoothstep(0.02, 0.1, p.z))))
    def fn(p, n_, fc, fi):
        c = basalt(p, n_, 0.75, 0.35, 5)
        heat = smoothstep(0.3, -0.6, n_.dot(Vector((p.x, p.y, 0)).normalized())) * smoothstep(0.11, 0.0, p.z)
        c = mix(c, lava(0.45), heat)
        return (c[0], c[1], c[2], heat * 0.85)
    paint_vertex_normals(rim, fn)
    g.extend(rim)
    for k, (a, s) in enumerate(((0.6, 0.15), (2.4, 0.12), (4.2, 0.17), (5.4, 0.1))):
        rr = (R + 0.12) * rim_j(0, 0, a)
        st = soft_blob((rr * math.cos(a), rr * math.sin(a), 0.03), (s * 1.2, s, s * 0.8), seed=k + 90, lump=0.15,
                       freq=1.3, sides=9, rings=5, flatten_bottom=True)
        paint_vertex_normals(st, lambda p, n_, fc, fi, k=k: basalt(p, n_, 0.75, 0.35, k + 40))
        g.extend(st)
    for k, (cx, cy, r) in enumerate(((-0.18, 0.12, 0.09), (0.17, -0.06, 0.07), (0.02, 0.27, 0.05))):
        poly = [(cx + r * (1 + 0.2 * math.sin(3 * a + k)) * math.cos(a), cy + r * 0.85 * (1 + 0.2 * math.sin(3 * a + k)) * math.sin(a))
                for a in [2 * math.pi * q / 7 for q in range(7)]]
        pl, _ = hot_plate(poly, 0.03, 0.012, k + 3, hot=1.0)
        pl.transform(T(0, 0, 0.035))
        g.extend(pl)
    bub = sphere((0.05, -0.2, 0.045), 0.035, sides=7, rings=4)
    bub.fill(lava(1.0))
    g.extend(bub)
    g.extend(ash_blades((-0.55, -0.45, 0), 5, 0.18, 11, 0.05))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props", view=(35, 40))


# ------------------------------------------------------------------ props: trees
def trunk_paint(height, seed):
    def fn(p, n, fc, fi):
        c = shade(mix(CHAR0, CHAR1, smoothstep(0, height, p.z) * 0.6 + 0.3 + 0.2 * vnoise(p, 5, seed)), 1.0)
        crack = smoothstep(0.09, 0.0, abs(vnoise(Vector((p.x * 2.5, p.y * 2.5, p.z * 0.7)), 2.2, seed))) * smoothstep(height * 0.95, 0.1, p.z)
        c = mix(c, lava(0.4 + 0.5 * crack), smoothstep(0.1, 0.7, crack))
        return (c[0], c[1], c[2], smoothstep(0.15, 0.8, crack))
    return fn


@model
def EmberTreeA():
    """Charred oak with smouldering ember-red foliage and glowing cracks in the bark."""
    root = new_scene("EmberTreeA")
    rnd = random.Random(3)
    height, spread, tr_r = 2.3, 1.0, 0.21
    pts = bezier((0, 0, 0), (0.08, 0.03, height * 0.35), (-0.06, -0.03, height * 0.62), n=5)
    wood = tube(pts, taper(len(pts), tr_r, tr_r * 0.68), sides=9, cap0=False, cap1=True)
    wood.extend(lathe([(0.0, 0.0), (tr_r * 2.1, 0.0), (tr_r * 1.35, 0.1), (tr_r * 1.02, 0.32), (0, 0.33)], sides=9,
                      jitter=lambda pi, si, a: 1 + 0.2 * math.sin(a * 5)))
    tips = [Vector((0, 0, height + 0.2))]
    for k in range(4):
        a = 2 * math.pi * k / 4 + rnd.uniform(-0.3, 0.3) + 0.4
        p0 = pts[-1] - Vector((0, 0, rnd.uniform(0.0, 0.45)))
        d = Vector((math.cos(a), math.sin(a), 0))
        p2 = p0 + d * spread * rnd.uniform(0.7, 1.0) + Vector((0, 0, rnd.uniform(0.45, 0.8)))
        br = bezier(p0, p0 + d * 0.35 + Vector((0, 0, 0.25)), p2, n=4)
        wood.extend(tube(br, taper(len(br), tr_r * 0.58, tr_r * 0.22), sides=6, cap0=False, cap1=True))
        tips.append(p2)
    paint_vertex_normals(wood, trunk_paint(height, 2.0))
    part("Trunk", clamp_ground(wood), root)
    can = Geo()
    lo, mid, hi = hexc("#6E2216"), hexc("#C9472A"), hexc("#FFB04E")
    for k, t in enumerate(tips):
        r = 0.7 if k == 0 else rnd.uniform(0.5, 0.62)
        b = soft_blob((t.x, t.y, t.z + 0.12), (r * 1.08, r * 1.02, r * 0.86), seed=31 + k, lump=0.2, freq=1.9, sides=11, rings=7)
        tint = 0.92 + 0.16 * hash01(k, 3)
        def fn(p, n, fc, fi, tint=tint, k=k):
            c = shade(foliage_soft(lo, mid, hi, 0.12, k)(p, n, fc, fi), tint)
            a = 0.3 * smoothstep(0.75, 1.0, n.z)                    # sun-warmed tops glow softly
            return (c[0], c[1], c[2], a)
        paint_vertex_normals(b, fn)
        can.extend(b)
    # a few glowing ember-fruit hanging under the canopy
    for k in range(5):
        t = tips[1 + k % 4]
        e = sphere((t.x + rnd.uniform(-0.25, 0.25), t.y + rnd.uniform(-0.25, 0.25), t.z - 0.35), 0.048, sides=7, rings=5)
        e.paint(lambda p, n, fc, fi: lava(0.65 + 0.3 * n.z))
        can.extend(e)
    part("Canopy", can, root, (0, 0, height * 0.75))
    finish(root, "Props")


@model
def EmberTreeB():
    """Cinder pine: soft layered tiers in charcoal with ash-dusted tops and smouldering ember rims."""
    root = new_scene("EmberTreeB")
    height, base_r = 3.6, 1.15
    trunk = tube([(0, 0, 0), (0, 0, height * 0.35)], [0.13, 0.1], sides=7, cap0=False)
    trunk.extend(lathe([(0.0, 0.0), (0.25, 0.0), (0.15, 0.08), (0.12, 0.2), (0, 0.21)], sides=7,
                       jitter=lambda pi, si, a: 1 + 0.2 * math.sin(a * 3)))
    paint_vertex_normals(trunk, trunk_paint(height * 0.4, 5.0))
    part("Trunk", trunk, root)
    tiers = Geo()
    n = 4
    z = height * 0.22
    for k in range(n):
        r = base_r * (1 - k / (n + 0.6))
        h = (height - z) / (n - k) * 1.45
        prof = [(0.0, 0.05), (r, -0.06), (r * 0.86, h * 0.1), (r * 0.5, h * 0.45), (0.0, h)]
        tier = lathe(prof, sides=14, round_profile=1,
                     jitter=lambda pi, si, a, k=k: (1 + 0.2 * ((si + k) % 2) - 0.06) if pi in (1, 2) else 1 + 0.06 * math.sin(a * 5))
        z0 = z
        def fn(p, n_, fc, fi, z0=z0, r=r):
            c = mix(hexc("#2C2626"), hexc("#5A4C48"), smoothstep(-0.6, 0.6, n_.z))
            c = mix(c, mix(ASH0, ASH1, 0.5 + 0.5 * vnoise(p, 4)), smoothstep(0.55, 0.9, n_.z) * 0.75)
            rim = smoothstep(r * 0.86, r * 1.0, math.hypot(p.x, p.y)) * smoothstep(0.5, -0.2, n_.z)
            c = mix(c, lava(0.2 + 0.45 * rim), smoothstep(0.1, 0.8, rim))
            return (c[0], c[1], c[2], smoothstep(0.3, 1.0, rim) * 0.6)
        tier.transform(T(0, 0, z))
        paint_vertex_normals(tier, fn)
        tiers.extend(tier)
        z += h * 0.52
    part("Canopy", tiers, root, (0, 0, height * 0.22))
    finish(root, "Props")


# ------------------------------------------------------------------ props: landmark
@model
def EmberForge():
    """Ruined beehive kiln/forge shrine: terracotta dome with a glowing mouth (-Y), smouldering chimney,
    heat-cracked flagstone apron, anvil stone with a hot ingot, charred logs and glowing roots creeping over it."""
    root = new_scene("EmberForge")
    rnd = random.Random(29)
    g = Geo()
    # flagstone apron with molten seams
    seeds = [(0.0, 0.0)]
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.2
        seeds.append((0.95 * math.cos(a), 0.85 * math.sin(a)))
    for k in range(9):
        a = 2 * math.pi * k / 9 + rnd.uniform(-0.1, 0.1)
        seeds.append((1.55 * math.cos(a), 1.4 * math.sin(a)))
    cells = voronoi(seeds, [(1.85 * x, 1.65 * y) for (x, y) in circle_poly(1.0, 18)], 0.05)
    floor = Geo()
    for k, poly in enumerate(cells):
        if len(poly) < 3:
            continue
        pl, c = hot_plate(poly, 0.07, 0.025, k, hot=0.9, ash_k=0.85, lo=hexc("#5A4A44"), hi=hexc("#9A8578"))
        pl.transform(T(c.x, c.y, 0) @ rot(rnd.uniform(-2.5, 2.5), rnd.uniform(-2.5, 2.5), 0) @ T(-c.x, -c.y, 0))
        floor.extend(pl)
    g.extend(floor)
    seam = lathe([(1.75, 0.01), (1.0, 0.02), (0.0, 0.03)], 12, cap_bottom=False, cap_top=False)
    seam.transform(S(1.0, 0.92, 1.0))
    seam.paint(lambda p, n, fc, fi: lava(0.75 - 0.4 * smoothstep(0.3, 1.8, math.hypot(p.x, p.y)), 0.9))
    g.extend(seam)
    # kiln dome
    Z0 = 0.06
    PROF = [(0.0, Z0), (0.92, Z0), (0.95, Z0 + 0.36),
            (1.0, Z0 + 0.4), (1.0, Z0 + 0.5), (0.93, Z0 + 0.54),                    # lower ledge course
            (0.85, Z0 + 0.85),
            (0.88, Z0 + 0.88), (0.86, Z0 + 0.97), (0.79, Z0 + 1.0),               # upper ledge course
            (0.62, Z0 + 1.24), (0.4, Z0 + 1.45),
            (0.3, Z0 + 1.52), (0.31, Z0 + 1.7), (0.2, Z0 + 1.73), (0.18, Z0 + 1.58), (0.0, Z0 + 1.56)]
    LEDGE = (3, 4, 5, 7, 8, 9, 12, 13, 14)
    dome = lathe(PROF, sides=13, round_profile=0,
                 jitter=lambda pi, si, a: 1.0 + (0.03 * math.sin(a * 4 + 1) + 0.02 * math.sin(a * 9) if 1 <= pi <= 11 else 0.0))
    def dp(p, n, fc, fi):
        r = math.hypot(p.x, p.y)
        c = mix(RUST1, RUST2, 0.5 + 0.35 * vnoise(p, 2.5, 3) + 0.1 * vnoise(p, 8, 1))
        ledge = any(abs(p.z - PROF[i][1]) < 1e-4 and abs(r - PROF[i][0]) < 0.08 for i in LEDGE)
        if ledge:
            c = basalt(p, n, 0.8, 0.15, 3, hexc("#5A4A44"), hexc("#A08B7C"))
        c = mix(c, shade(RUST0, 0.7), smoothstep(Z0 + 1.0, Z0 + 1.6, p.z) * 0.85)           # soot near the top
        c = mix(c, ASH1, smoothstep(0.75, 0.95, n.z) * 0.4 * smoothstep(0.25, 0.5, r))     # ash on the shoulders
        hot = smoothstep(0.2, 0.12, r) * smoothstep(Z0 + 1.5, Z0 + 1.6, p.z) * smoothstep(0.5, -0.5, n.z * (1 if r > 0.15 else -1))
        hot = max(hot, smoothstep(0.2, 0.1, r) * smoothstep(Z0 + 1.6, Z0 + 1.55, p.z))
        c = mix(c, lava(0.85), hot)
        return (c[0], c[1], c[2], hot)
    paint_vertex_normals(dome, dp)
    g.extend(dome)
    # stone buttress ring at the foot of the dome
    stones = Geo()
    for k in range(10):
        a = 2 * math.pi * k / 10 + 0.13
        if abs(math.degrees(a) % 360 - 270) < 32:
            continue                        # leave the mouth free
        s = box((0, 0, 0), (0.3, 0.22, rnd.uniform(0.2, 0.28)), bevel=0.06, segments=1, smooth=True)
        s.transform(T(1.0 * math.cos(a), 0.98 * math.sin(a), Z0 + 0.1) @ rot(0, 0, math.degrees(a) + 90) @ rot(rnd.uniform(-6, 6), 0, 0))
        stones.extend(s)
    paint_vertex_normals(stones, lambda p, n, fc, fi: basalt(p, n, 0.8, 0.25, 7, hexc("#5A4A44"), hexc("#A08B7C")))
    g.extend(stones)
    # glowing mouth (on -Y) with a stone arch
    mouth = ellipsoid((0, -0.9, Z0), (0.33, 0.07, 0.5), sides=10, rings=4)
    mouth.paint(lambda p, n, fc, fi: lava(1.0 - 0.75 * smoothstep(0.0, 0.5, math.hypot(p.x, (p.z - Z0) * 0.8)) + 0.0))
    g.extend(mouth)
    arch_pts = [Vector((0.38 * math.cos(math.radians(a)), -0.96, Z0 + 0.42 * math.sin(math.radians(a)) * 1.25)) for a in range(0, 181, 15)]
    arch = tube(arch_pts, 0.075, sides=6, cap0=True, cap1=True)
    paint_vertex_normals(arch, lambda p, n, fc, fi: basalt(p, n, 0.75, 0.2, 9, hexc("#5A4A44"), hexc("#A89484")))
    g.extend(arch)
    # anvil stone + hot ingot (front-right)
    anv = box((0.95, -1.05, 0.2), (0.5, 0.3, 0.3), bevel=0.06, segments=1, smooth=True)
    anv.extend(box((0.95, -1.05, 0.4), (0.62, 0.22, 0.12), bevel=0.035, segments=1, smooth=True))
    paint_vertex_normals(anv, lambda p, n, fc, fi: basalt(p, n, 0.7, 0.15, 11, hexc("#38323A"), hexc("#6E6470")))
    g.extend(anv)
    ingot = box((0.98, -1.05, 0.49), (0.2, 0.09, 0.05), bevel=0.015, smooth=True)
    ingot.fill(lava(0.85))
    g.extend(ingot)
    # charred log pile (back-left)
    logs = Geo()
    for k, (x, y, z, a) in enumerate(((-1.05, 0.75, 0.13, 20), (-1.15, 0.45, 0.13, 35), (-1.1, 0.62, 0.34, 28))):
        d = Vector((math.cos(math.radians(a)), math.sin(math.radians(a)), 0))
        c = Vector((x, y, z))
        lg = tube([c - d * 0.38, c + d * 0.38], 0.11, sides=8, cap0=True, cap1=True)
        lg.paint(lambda p, n, fc, fi, d=d: hexc("#C9762E", 0.0) if abs(n.dot(d)) > 0.9 else hexc("#2A2120"))
        logs.extend(lg)
    g.extend(logs)
    # glowing roots creeping over the dome from the back
    for k, (a, h) in enumerate(((60, 1.15), (135, 0.85))):
        ar = math.radians(a)
        d = Vector((math.cos(ar), math.sin(ar), 0))
        p0 = d * 1.6 + Vector((0, 0, -0.03))
        p1 = d * 1.0 + Vector((0, 0, 0.25))
        p2 = d * 0.84 + Vector((0, 0, h * 0.7))
        p3 = d * 0.62 + Vector((0, 0, h + 0.1))
        pts = bezier(p0, p1, p2, p3, n=6)
        g.extend(glow_root(pts, taper(len(pts), 0.075, 0.03), sides=6, seam=0.45, amount=0.9, seed=k))
    # rubble and ash grass
    for k in range(4):
        a = rnd.uniform(0, 2 * math.pi)
        rr = rnd.uniform(1.4, 1.9)
        pb = soft_blob((rr * math.cos(a), rr * math.sin(a) * 0.9, 0.02), (0.12, 0.1, 0.09), seed=k + 120, lump=0.15, freq=1.3,
                       sides=6, rings=3, flatten_bottom=True)
        paint_vertex_normals(pb, lambda p, n, fc, fi, k=k: basalt(p, n, 0.8, 0.3, k))
        g.extend(pb)
    g.extend(ash_blades((-1.5, -0.6, 0), 6, 0.26, 12, 0.08))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props", view=(70, 28))


if __name__ == "__main__":
    names = argv() or list(MODELS)
    for n in names:
        MODELS[n]()
