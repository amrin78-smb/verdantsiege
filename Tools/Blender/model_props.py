"""Verdant Heartlands environment set + battlefield tiles + FX models. Usage: blender ... -- [Name ...] (default all)."""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_STONE0, C_STONE1, C_STONE2 = hexc("#6E6558"), hexc("#A89A82"), hexc("#D3C6A8")
C_MOSS0, C_MOSS1 = hexc("#3F7A2E"), hexc("#86C04E")
C_GRASS0, C_GRASS1, C_GRASS2 = hexc("#2F6B2A"), hexc("#5DA83E"), hexc("#A6D85C")
C_WOOD0, C_WOOD1 = hexc("#4A3220"), hexc("#8A6440")
C_THATCH0, C_THATCH1 = hexc("#7E5E2E"), hexc("#D1A75A")
C_LEAF0, C_LEAF1, C_LEAF2 = hexc("#1E5A2C"), hexc("#3D8E3A"), hexc("#A4D65E")
C_DIRT = hexc("#6B5034")
FLOWERS = [hexc("#FF7A9A"), hexc("#FFD447"), hexc("#B48CFF"), hexc("#FFF3E6"), hexc("#FF9E4A")]


def stone_paint(moss_k=0.75, base=None):
    lo, hi = (C_STONE0, C_STONE2) if base is None else base
    def fn(p, n, fc, fi):
        c = mix(lo, hi, 0.55 + 0.35 * vnoise(p, 2.2, 1.0) + 0.12 * vnoise(p, 7.0, 5.0))
        c = mix(c, mix(C_MOSS0, C_MOSS1, 0.5 + 0.5 * vnoise(p, 5.0, 2.0)), smoothstep(moss_k - 0.12, moss_k + 0.12, n.z) * 0.9)
        return c
    return fn


def foliage_paint(k=1.0, warm=0.0):
    def fn(p, n, fc, fi):
        t = smoothstep(-0.3, 0.9, n.z)
        c = mix(C_LEAF0, C_LEAF1, t)
        c = mix(c, C_LEAF2, smoothstep(0.7, 1.0, n.z) * 0.75)
        c = mix(c, hexc("#C8C850"), warm * smoothstep(0.5, 1.0, n.z))
        return shade(c, k * (0.88 + 0.24 * hash01(fc.x * 1.7, fc.y * 2.3, fc.z)))
    return fn


def flower(center, color, size=0.05, petals=5, stem=0.12, seed=0):
    g = Geo()
    c = Vector(center)
    st = tube([c, c + Vector((0.01, 0, stem * 0.5)), c + Vector((0, 0.005, stem))], 0.006, sides=3, smooth=False).fill(C_GRASS1)
    g.extend(st)
    head = Geo()
    for j in range(petals):
        pl = leaf(length=size, width=size * 0.7, segs=2, across=1, curl=0.3, fold=0.1, thick=0.006)
        pl.transform(rot(0, 0, 360 * j / petals) @ rot(0, -20, 0))
        head.extend(pl)
    head.fill(color)
    head.extend(sphere((0, 0, 0.005), size * 0.3, sides=5, rings=3).fill(hexc("#FFE45C", 0.4)))
    head.transform(T(*(c + Vector((0, 0.005, stem)))))
    g.extend(head)
    return g


def grass_clump(center, n=6, h=0.18, seed=0, spread=0.06):
    rnd = random.Random(seed)
    g = Geo()
    for i in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        b = Vector(center) + Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(0, spread)
        g.extend(blade(b, Vector((math.cos(a), math.sin(a), 0)), h * rnd.uniform(0.7, 1.2), 0.016, bend=rnd.uniform(0.2, 0.5)))
    g.paint(lambda p, n_, fc, fi: mix(C_GRASS0, C_GRASS2, smoothstep(center[2], center[2] + h, p.z)))
    return g


MODELS = {}
def model(fn):
    MODELS[fn.__name__] = fn
    return fn


# ------------------------------------------------------------------ rocks
def rock(name, radii, seed, extra=()):
    root = new_scene(name)
    g = pebble((0, 0, radii[2] * 0.62), radii, seed=seed, lump=0.12)
    for (c, r, s_) in extra:
        g.extend(pebble(c, r, seed=s_, lump=0.12))
    paint_vertex_normals(g, stone_paint(0.72))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")

@model
def RockA(): rock("RockA", (0.55, 0.45, 0.42), 1, [((0.45, 0.3, 0.12), (0.18, 0.16, 0.14), 11)])
@model
def RockB(): rock("RockB", (0.35, 0.32, 0.5), 2, [((-0.3, -0.2, 0.08), (0.14, 0.12, 0.1), 12), ((0.25, -0.25, 0.06), (0.1, 0.1, 0.08), 13)])
@model
def RockC(): rock("RockC", (0.26, 0.22, 0.18), 3)


# ------------------------------------------------------------------ bushes
def bush(name, blobs, seed, berries=None, flowers_=0):
    root = new_scene(name)
    rnd = random.Random(seed)
    g = Geo()
    for k, (c, r) in enumerate(blobs):
        b = soft_blob(c, r, seed=seed * 10 + k, lump=0.15, freq=1.8, sides=11 if r[0] > 0.3 else 8, rings=7 if r[0] > 0.3 else 5)
        paint_vertex_normals(b, foliage_soft(C_LEAF0, C_LEAF1, C_LEAF2, var=0.12, seed=k))
        g.extend(b)
    if berries:
        for k in range(berries[1]):
            a = rnd.uniform(0, 2 * math.pi); z = rnd.uniform(0.25, 0.6)
            c, r = blobs[0]
            p = Vector((c[0] + math.cos(a) * r[0] * 0.95, c[1] + math.sin(a) * r[1] * 0.95, z))
            g.extend(sphere(p, 0.035, sides=6, rings=4).fill(berries[0]))
    for k in range(flowers_):
        a = rnd.uniform(0, 2 * math.pi)
        c, r = blobs[k % len(blobs)]
        p = Vector((c[0] + math.cos(a) * r[0] * 0.8, c[1] + math.sin(a) * r[1] * 0.8, c[2] + r[2] * 0.75))
        f = flower(p - Vector((0, 0, 0.05)), FLOWERS[k % 3 + 0], size=0.05, stem=0.05)
        g.extend(f)
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")

@model
def BushA(): bush("BushA", [((0, 0, 0.35), (0.48, 0.42, 0.38)), ((0.3, 0.25, 0.25), (0.28, 0.25, 0.24)),
                            ((-0.3, -0.2, 0.22), (0.26, 0.24, 0.22))], 5, berries=(hexc("#E8384F", 0.3), 9))
@model
def BushB(): bush("BushB", [((0, 0, 0.28), (0.4, 0.34, 0.3)), ((0.22, -0.2, 0.2), (0.22, 0.2, 0.2))], 6, flowers_=6)


# ------------------------------------------------------------------ small ground dressing
@model
def FlowerCluster():
    root = new_scene("FlowerCluster")
    rnd = random.Random(9)
    g = Geo()
    for k in range(8):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0.0, 0.28)
        c = (math.cos(a) * r, math.sin(a) * r, 0.0)
        g.extend(flower(c, FLOWERS[k % len(FLOWERS)], size=rnd.uniform(0.045, 0.065), stem=rnd.uniform(0.12, 0.24), seed=k))
    for k in range(3):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0.05, 0.3)
        g.extend(grass_clump((math.cos(a) * r, math.sin(a) * r, 0.0), n=4, h=0.15, seed=k))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")

@model
def GrassTuft():
    root = new_scene("GrassTuft")
    g = grass_clump((0, 0, 0), n=14, h=0.3, seed=4, spread=0.14)
    g.extend(grass_clump((0.12, 0.08, 0), n=6, h=0.2, seed=7, spread=0.06))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")

@model
def MushroomCluster():
    root = new_scene("MushroomCluster")
    g = Geo()
    for k, (x, y, s, col) in enumerate(((0, 0, 1.0, "#E8763A"), (0.14, 0.08, 0.7, "#E8763A"), (-0.12, 0.1, 0.6, "#C94A3A"),
                                        (0.05, -0.15, 0.5, "#F0B048"), (-0.18, -0.08, 0.45, "#E8763A"))):
        h = 0.22 * s
        st = lathe([(0, 0), (0.045 * s, 0), (0.035 * s, h * 0.6), (0.03 * s, h), (0, h)], sides=7)
        st.paint(lambda p, n, fc, fi: mix(hexc("#D8C8A8"), hexc("#FFF6E2"), smoothstep(0, 0.1, p.z)))
        r = 0.12 * s
        cp = lathe([(0.0, 0.0), (r * 0.95, 0.0), (r, r * 0.25), (r * 0.65, r * 0.62), (0, r * 0.72)], sides=10)
        base = hexc(col)
        cp.paint(lambda p, n, fc, fi, base=base: hexc("#F7E4C8") if n.z < -0.3 else mix(base, shade(base, 1.25), smoothstep(0, r, p.z - h)))
        cp.transform(T(0, 0, h - 0.01))
        dots = Geo()
        for j in range(4):
            a = 2 * math.pi * j / 4 + k
            dots.extend(sphere((math.cos(a) * r * 0.5, math.sin(a) * r * 0.5, h + r * 0.5), r * 0.12, sides=5, rings=3))
        dots.fill(hexc("#FFF6E2"))
        m = merge(st, cp, dots)
        m.transform(T(x, y, 0) @ rot(8 * (k % 3 - 1), 6 * (k % 2), 0))
        g.extend(m)
    g.extend(grass_clump((0.05, 0.02, 0), n=6, h=0.12, seed=3, spread=0.2))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")


# ------------------------------------------------------------------ trees (background)

def oak(name, seed, height, spread, n_br=4, warm=0.0):
    root = new_scene(name)
    rnd = random.Random(seed)
    tr_r = 0.2
    pts = bezier((0, 0, 0), (0.05, 0.02, height * 0.35), (-0.03, -0.02, height * 0.6), n=5)
    wood = tube(pts, taper(len(pts), tr_r, tr_r * 0.7), sides=9, cap0=False, cap1=True)
    wood.extend(lathe([(0.0, 0.0), (tr_r * 2.0, 0.0), (tr_r * 1.3, 0.1), (tr_r * 1.02, 0.3), (0, 0.31)], sides=9,
                      jitter=lambda pi, si, a: 1 + 0.18 * math.sin(a * 4)))
    tips = [Vector((0, 0, height + 0.25))]
    for k in range(n_br):
        a = 2 * math.pi * k / n_br + rnd.uniform(-0.3, 0.3)
        p0 = pts[-1] - Vector((0, 0, rnd.uniform(0.0, 0.4)))
        d = Vector((math.cos(a), math.sin(a), 0))
        p2 = p0 + d * spread * rnd.uniform(0.7, 1.0) + Vector((0, 0, rnd.uniform(0.5, 0.9)))
        br = bezier(p0, p0 + d * 0.3 + Vector((0, 0, 0.35)), p2, n=4)
        wood.extend(tube(br, taper(len(br), tr_r * 0.6, tr_r * 0.25), sides=6, cap0=False, cap1=True))
        tips.append(p2)
    paint_vertex_normals(wood, lambda p, n, fc, fi: shade(mix(C_WOOD0, C_WOOD1, smoothstep(0, height, p.z)), 0.92 + 0.15 * vnoise(p, 6)))
    part("Trunk", clamp_ground(wood), root)
    can = Geo()
    for k, t in enumerate(tips):
        r = (0.75 if k == 0 else rnd.uniform(0.55, 0.7))
        b = soft_blob((t.x, t.y, t.z + 0.15), (r * 1.1, r * 1.05, r * 0.72), seed=seed * 7 + k, lump=0.16, freq=1.7,
                      sides=11, rings=7)
        tint = 0.92 + 0.16 * hash01(k, seed)
        paint_vertex_normals(b, lambda p, n, fc, fi, tint=tint: shade(foliage_soft(C_LEAF0, C_LEAF1, C_LEAF2, 0.12, k)(p, n, fc, fi), tint))
        can.extend(b)
    part("Canopy", can, root, (0, 0, height * 0.75))
    finish(root, "Props")

def pine(name, seed, height, base_r):
    root = new_scene(name)
    rnd = random.Random(seed)
    trunk = tube([(0, 0, 0), (0, 0, height * 0.35)], [0.13, 0.1], sides=7, cap0=False)
    trunk.extend(lathe([(0.0, 0.0), (0.24, 0.0), (0.15, 0.08), (0.12, 0.2), (0, 0.21)], sides=7,
                       jitter=lambda pi, si, a: 1 + 0.2 * math.sin(a * 3)))
    paint_vertex_normals(trunk, lambda p, n, fc, fi: shade(C_WOOD0, 1.0 + 0.15 * vnoise(p, 6)))
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
        tier.transform(T(0, 0, z))
        tiers.extend(tier)
        z += h * 0.52
    def pp(p, n_, fc, fi):
        c = mix(hexc("#1A4A2E"), hexc("#3E8A48"), smoothstep(-0.6, 0.6, n_.z))
        c = mix(c, hexc("#7FBF5A"), smoothstep(0.6, 0.9, n_.z) * 0.5)
        return shade(c, 1.0 + 0.1 * vnoise(p, 5))
    paint_vertex_normals(tiers, pp)
    part("Canopy", tiers, root, (0, 0, height * 0.22))
    finish(root, "Props")

@model
def TreeA(): oak("TreeA", 1, 2.6, 1.0, n_br=4)
@model
def TreeB(): pine("TreeB", 2, 3.9, 1.25)


# ------------------------------------------------------------------ ruins & rustic structures
def stone_block(c, s, seed, chip=0.02, bevel=True):
    # rounded (2-segment bevel, smooth shaded) so ruins read soft, not blocky
    g = box(c, s, bevel=min(s) * 0.2 if bevel else 0.0, segments=1, smooth=bevel)
    rnd = random.Random(seed)
    g.displace(lambda p: Vector((rnd.uniform(-chip, chip), rnd.uniform(-chip, chip), rnd.uniform(-chip, chip) * 0.5)))
    return g

@model
def RuinWall():
    root = new_scene("RuinWall")
    rnd = random.Random(21)
    g = Geo()
    heights = [1.35, 1.1, 0.8, 0.55, 0.3]
    x = -0.95
    for col in range(5):
        w = 0.38
        hcol = heights[col] * rnd.uniform(0.9, 1.05)
        z = 0.0
        row = 0
        while z < hcol - 0.05:
            bh = rnd.uniform(0.22, 0.3)
            off = 0.09 if row % 2 else 0.0
            g.extend(stone_block((x + w / 2 + off * (1 if col < 4 else 0), rnd.uniform(-0.02, 0.02), z + bh / 2), (w - 0.03, 0.42, bh - 0.025), rnd.random() * 99))
            z += bh; row += 1
        x += w
    # fallen blocks
    for k, (px, py) in enumerate(((0.75, 0.45), (0.95, -0.1), (0.4, 0.55))):
        g.extend(stone_block((px, py, 0.1), (0.3, 0.25, 0.2), 50 + k).transform(Matrix()))
    paint_vertex_normals(g, stone_paint(0.8))
    ivy = Geo()
    for k in range(10):
        a = rnd.uniform(-0.9, 0.2)
        z = rnd.uniform(0.3, 1.2)
        lf = leaf(length=0.12, width=0.09, segs=2, across=1, curl=0.2, fold=0.2, thick=0.01)
        lf.transform(T(a, -0.22, z) @ rot(90, 0, rnd.uniform(-60, 60)))
        ivy.extend(lf)
    ivy.paint(foliage_paint())
    g.extend(ivy)
    g.extend(grass_clump((-0.6, -0.3, 0), n=6, h=0.25, seed=1, spread=0.15))
    g.extend(grass_clump((0.2, 0.3, 0), n=5, h=0.2, seed=2, spread=0.12))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")

@model
def FencePost():
    root = new_scene("FencePost")
    g = Geo()
    for x, h in ((-0.42, 0.85), (0.42, 0.78)):
        post = box((x, 0, h / 2), (0.1, 0.1, h), bevel=0.02)
        post.extend(cone((x, 0, h), (x, 0, h + 0.08), 0.07, sides=4).transform(Matrix()))
        g.extend(post)
    for z, tilt in ((0.62, 2.0), (0.32, -3.0)):
        rail = box((0, 0.065, z), (0.98, 0.04, 0.09), bevel=0.012, rotm=rot(tilt, 0, 0) @ rot(0, tilt, 0))
        g.extend(rail)
    g.displace(lambda p: Vector((0, 0.01 * math.sin(p.z * 9 + p.x * 3), 0)))
    g.paint(lambda p, n, fc, fi: shade(mix(C_WOOD0, C_WOOD1, 0.4 + 0.5 * hash01(fc.x * 3, fc.z * 7)),
                                       1.0 if n.z < 0.7 else 1.15), per_face=True)
    g.extend(grass_clump((-0.42, 0.05, 0), n=5, h=0.22, seed=3))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props")

@model
def StonePath():
    root = new_scene("StonePath")
    rnd = random.Random(31)
    g = Geo()
    for k, (cx, cy, rx, ry) in enumerate(((-0.23, -0.22, 0.24, 0.22), (0.24, -0.2, 0.23, 0.25), (-0.2, 0.24, 0.26, 0.22),
                                          (0.25, 0.26, 0.21, 0.21), (0.02, 0.02, 0.13, 0.12))):
        n = 7
        prof = []
        slab = lathe([(0.0, 0.0), (1.0, 0.0), (0.97, 0.03), (0.88, 0.052), (0.0, 0.058)], sides=10,
                     jitter=lambda pi, si, a, k=k: 1 + 0.1 * math.sin(a * 3 + k) + 0.05 * math.sin(a * 5 - k))
        slab.transform(T(cx, cy, 0.0) @ rot(0, 0, rnd.uniform(0, 60)) @ S(rx, ry, 1))
        g.extend(slab)
    paint_vertex_normals(g, stone_paint(1.5, (hexc("#8A7F6E"), hexc("#CFC2A4"))))
    part("Mesh", g, root)
    finish(root, "Props", view=(30, 50))

@model
def RuinArch():
    root = new_scene("RuinArch")
    rnd = random.Random(41)
    g = Geo()
    R, cz = 1.05, 1.75
    # two pillars of stacked blocks; the right one has crumbled
    for x, h in ((-R, cz), (R, 1.15)):
        z = 0
        while z < h - 0.05:
            bh = min(rnd.uniform(0.32, 0.42), h - z)
            g.extend(stone_block((x + rnd.uniform(-0.03, 0.03), 0, z + bh / 2), (0.55, 0.6, bh - 0.03), rnd.random() * 99, 0.03))
            z += bh
    # voussoirs around a true semicircle from the left; the last third has fallen
    n = 9
    for k in range(n):
        if k >= 6:
            break
        a0 = math.pi - (k + 0.5) * math.pi / n
        c = Vector((math.cos(a0) * R, 0, cz + math.sin(a0) * R))
        blk = stone_block((0, 0, 0), (2 * R * math.sin(math.pi / n / 2) * 1.6, 0.58, 0.42), 70 + k, 0.02)
        blk.transform(T(*c) @ rot(0, 90 - math.degrees(a0), 0) @ rot(0, 90, 0))
        g.extend(blk)
    keystone_fallen = stone_block((0.55, 0.45, 0.14), (0.34, 0.5, 0.28), 88, 0.03)
    keystone_fallen.transform(Matrix())
    g.extend(keystone_fallen)
    for k in range(5):
        g.extend(stone_block((rnd.uniform(0.6, 1.45), rnd.uniform(-0.45, 0.45), 0.1), (0.28, 0.24, 0.2), 90 + k))
    paint_vertex_normals(g, stone_paint(0.75))
    vine_pts = bezier((-R - 0.3, -0.31, 0.15), (-R - 0.35, -0.32, 1.3), (-0.9, -0.32, 2.75), (0.1, -0.31, 2.85), n=10)
    vine = tube(vine_pts, 0.025, sides=4, smooth=False).fill(C_LEAF0)
    leaves = Geo()
    for k, p in enumerate(vine_pts[1:]):
        lf = leaf(length=0.14, width=0.1, segs=2, across=1, curl=0.2, fold=0.2, thick=0.01)
        lf.transform(T(*p) @ rot(90, 0, 40 * (1 if k % 2 else -1)))
        leaves.extend(lf)
    leaves.paint(foliage_paint())
    g.extend(vine); g.extend(leaves)
    g.extend(grass_clump((-R, 0.4, 0), n=6, h=0.3, seed=5, spread=0.2))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props", view=(70, 18))


@model
def Cottage():
    root = new_scene("Cottage")
    rnd = random.Random(51)
    g = Geo()
    W, D, H = 2.6, 2.0, 1.5
    # stone walls (one corner broken down)
    for side in range(4):
        for row in range(5):
            z = row * 0.3
            n = 6
            for k in range(n):
                if side == 1 and row >= 3 and k >= 3:
                    continue      # collapsed corner
                t = (k + 0.5 + (0.5 if row % 2 else 0)) / n - 0.5
                if abs(t) > 0.5: continue
                if side in (0, 2):
                    c = (t * W, (D / 2) * (1 if side == 0 else -1) + rnd.uniform(-0.03, 0.03), z + 0.15)
                    s = (W / n - 0.04 - rnd.uniform(0, 0.06), 0.3, 0.27 - rnd.uniform(0, 0.04))
                else:
                    c = ((W / 2) * (1 if side == 1 else -1) + rnd.uniform(-0.03, 0.03), t * D, z + 0.15)
                    s = (0.3, D / n - 0.04 - rnd.uniform(0, 0.06), 0.27 - rnd.uniform(0, 0.04))
                if side == 2 and 1 <= k <= 2 and row <= 3:
                    continue      # doorway in the front wall (-Y)
                g.extend(stone_block(c, s, rnd.random() * 999, 0.035, bevel=False))
    paint_vertex_normals(g, stone_paint(0.85, (hexc("#857865"), hexc("#D9CCAE"))))
    # door frame + window
    wood = Geo()
    wood.extend(box((-W / 6 + 0.02, -D / 2, 1.22), (0.7, 0.36, 0.1), bevel=0.02))
    wood.extend(box((W / 3, -D / 2 - 0.02, 0.85), (0.5, 0.06, 0.06), bevel=0.01))
    wood.extend(box((W / 3, -D / 2 - 0.02, 0.6), (0.5, 0.06, 0.06), bevel=0.01))
    # roof: thatched, partly collapsed (rafters exposed on the broken corner)
    roof = Geo()
    for side in (1, -1):
        rp = box((0, side * 0.6, H + 0.55), (W + 0.5, 1.45, 0.16), bevel=0.05, rotm=rot(side * -38, 0, 0))
        if side == 1:
            # cut a ragged hole: drop verts on the +X end
            rp.displace(lambda p: Vector((0, 0, -0.12 if p.x > 0.6 else 0.0)))
        roof.extend(rp)
    roof.paint(lambda p, n, fc, fi: shade(mix(C_THATCH0, C_THATCH1, smoothstep(H, H + 1.1, p.z)), 0.85 + 0.3 * hash01(fc.x * 4, fc.z * 3)), per_face=True)
    for k in range(4):
        x = 0.7 + k * 0.18
        wood.extend(box((x, 0.55, H + 0.62), (0.07, 1.4, 0.07), bevel=0.01, rotm=rot(-38, 0, 0)))
    wood.extend(box((0, 0, H + 1.12), (W + 0.6, 0.1, 0.1), bevel=0.02))
    wood.paint(lambda p, n, fc, fi: shade(mix(C_WOOD0, C_WOOD1, hash01(fc.x * 2, fc.z * 5)), 1.0), per_face=True)
    # chimney
    chim = Geo()
    for k in range(4):
        chim.extend(stone_block((-W / 2 + 0.35, 0.35, H + 0.6 + k * 0.28), (0.42, 0.42, 0.26), 300 + k, bevel=False))
    paint_vertex_normals(chim, stone_paint(0.85))
    # moss mounds and flowers around
    deco = Geo()
    for k in range(4):
        a = rnd.uniform(0, 2 * math.pi)
        deco.extend(grass_clump((math.cos(a) * 1.5, math.sin(a) * 1.2, 0), n=5, h=0.3, seed=k, spread=0.15))
    for k in range(4):
        deco.extend(flower((rnd.uniform(-1.3, 1.3), -D / 2 - 0.35, 0), FLOWERS[k % 5], 0.06, stem=0.2))
    g = merge(g, wood, roof, chim, deco)
    part("Mesh", clamp_ground(g), root)
    finish(root, "Props", view=(250, 28))


# ------------------------------------------------------------------ battlefield pieces
def ground_tile(name, lo, hi, seed):
    """Soft lawn tile: gently domed 6x6 top, rounded rolled edge (<= 0.06 m at the rim), smooth per-vertex
    colour variation, a little clover and a couple of tiny flowers."""
    root = new_scene(name)
    rnd = random.Random(seed)
    g = Geo()
    N = 6
    H_EDGE, H_MID, INSET = 0.05, 0.062, 0.035
    idx = {}
    for i in range(N + 1):
        for j in range(N + 1):
            u = -0.5 + i / N; v = -0.5 + j / N
            x = u * (1 - 2 * INSET); y = v * (1 - 2 * INSET)
            d = max(abs(u), abs(v)) * 2      # 0 centre .. 1 rim
            z = H_MID - (H_MID - H_EDGE) * d ** 3 + 0.002 * math.sin(i * 1.7 + j * 2.3 + seed)
            idx[(i, j)] = len(g.v); g.v.append(Vector((x, y, z)))
    for i in range(N):
        for j in range(N):
            g.add_face([idx[(i, j)], idx[(i + 1, j)], idx[(i + 1, j + 1)], idx[(i, j + 1)]], True)
    ring = [idx[(i, 0)] for i in range(N + 1)] + [idx[(N, j)] for j in range(1, N + 1)] + \
           [idx[(i, N)] for i in range(N - 1, -1, -1)] + [idx[(0, j)] for j in range(N - 1, 0, -1)]
    # rolled edge: two rings stepping out and down (rounded profile)
    prev = ring
    for (grow, zf) in ((0.55, 0.62), (1.0, 0.0)):
        nxt = []
        for k in ring:
            p = g.v[k]
            sx = 0.5 if abs(p.x) >= 0.5 * (1 - 2 * INSET) - 1e-6 else abs(p.x)
            out = Vector((p.x, p.y, 0))
            ox = math.copysign(INSET * grow, p.x) if abs(abs(p.x) - 0.5 * (1 - 2 * INSET)) < 1e-6 else 0.0
            oy = math.copysign(INSET * grow, p.y) if abs(abs(p.y) - 0.5 * (1 - 2 * INSET)) < 1e-6 else 0.0
            nxt.append(len(g.v)); g.v.append(Vector((p.x + ox, p.y + oy, p.z * zf)))
        for k in range(len(ring)):
            a_, b_ = prev[k], prev[(k + 1) % len(ring)]
            c_, d_ = nxt[(k + 1) % len(ring)], nxt[k]
            g.add_face([a_, d_, c_, b_], True)
        prev = nxt
    def tp(p, n, fc, fi):
        base = mix(lo, hi, 0.5 + 0.45 * vnoise(p, 3.2, seed) + 0.15 * vnoise(p, 9, seed + 4))
        if n.z < 0.75:
            base = mix(base, shade(lo, 0.78), smoothstep(0.75, 0.2, n.z))
        return base
    paint_vertex_normals(g, tp)
    # clover patches (three round leaflets) and a couple of tiny flowers
    deco = Geo()
    for k in range(2):
        c = Vector((rnd.uniform(-0.33, 0.33), rnd.uniform(-0.33, 0.33), 0.058))
        for j in range(3):
            a = 2 * math.pi * j / 3 + k
            lf = leaf(length=0.045, width=0.045, segs=2, across=1, curl=0.2, fold=0.1, thick=0.006, tip_power=0.6)
            lf.transform(T(*(c + Vector((0, 0, 0.01)))) @ rot(0, 0, math.degrees(a)) @ rot(0, -12, 0))
            deco.extend(lf)
    deco.paint(lambda p, n, fc, fi: shade(mix(lo, hi, 0.3), 0.85))
    for k in range(1):
        c = (rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), 0.055)
        deco.extend(flower(c, FLOWERS[(seed + k * 2) % 4], size=0.028, petals=5, stem=0.035))
    g.extend(deco)
    part("Mesh", g, root)
    finish(root, "Battlefield", view=(30, 45))

@model
def GroundTileA(): ground_tile("GroundTileA", hexc("#6FB24A"), hexc("#8CCB57"), 1)
@model
def GroundTileB(): ground_tile("GroundTileB", hexc("#4F9440"), hexc("#68AD48"), 2)

@model
def LaneEdge():
    root = new_scene("LaneEdge")
    rnd = random.Random(61)
    g = Geo()
    x = -0.5
    k = 0
    while x < 0.48:
        w = rnd.uniform(0.22, 0.3)
        w = min(w, 0.5 - x)
        h = rnd.uniform(0.14, 0.24)
        b = stone_block((x + w / 2, rnd.uniform(-0.03, 0.03), h / 2), (w - 0.025, 0.3, h), 500 + k, 0.015)
        g.extend(b)
        x += w; k += 1
    paint_vertex_normals(g, stone_paint(0.8, (hexc("#7F7462"), hexc("#CDBF9F"))))
    g.extend(grass_clump((-0.3, 0.17, 0), n=4, h=0.16, seed=2, spread=0.05))
    g.extend(grass_clump((0.25, -0.18, 0), n=4, h=0.14, seed=3, spread=0.05))
    part("Mesh", clamp_ground(g), root)
    finish(root, "Battlefield")


# ------------------------------------------------------------------ FX
@model
def SeedBolt():
    root = new_scene("SeedBolt")
    body = lathe([(0.0, -0.11), (0.03, -0.09), (0.052, -0.03), (0.055, 0.02), (0.038, 0.08), (0.0, 0.13)], sides=8)
    body.transform(rot(0, 90, 0))
    body.paint(lambda p, n, fc, fi: mix(hexc("#FFB52E", 1.0), hexc("#FFF4A8", 1.0), smoothstep(-0.05, 0.12, p.x)))
    fins = Geo()
    for k in range(3):
        lf = leaf(length=0.09, width=0.05, segs=2, across=1, curl=0.0, fold=0.15, thick=0.008)
        lf.transform(rot(120 * k, 0, 0) @ T(-0.06, 0, 0.03) @ rot(0, 0, 180) @ rot(0, -25, 0))
        fins.extend(lf)
    fins.fill(hexc("#9BE04A", 0.6))
    g = merge(body, fins)
    g.transform(T(0, 0, 0.0))
    part("Mesh", g, root)
    finish(root, "FX", view=(60, 25))

@model
def Sunmote():
    root = new_scene("Sunmote")
    orb = ellipsoid((0, 0, 0), (0.1, 0.1, 0.1), sides=9, rings=6)
    orb.paint(lambda p, n, fc, fi: mix(hexc("#FFB42A", 1.0), hexc("#FFF8C8", 1.0), smoothstep(-0.1, 0.1, p.z + p.x * 0.4)))
    part("Orb", orb, root)
    rays = Geo()
    for k in range(8):
        a = 360 * k / 8
        lf = leaf(length=0.08, width=0.06, segs=2, across=1, curl=0.0, fold=0.2, thick=0.01)
        lf.transform(rot(0, 0, a) @ T(0.09, 0, 0) @ rot(0, -10, 0))
        rays.extend(lf)
    rays.fill(hexc("#FFD447", 1.0))
    part("Rays", rays, root)
    finish(root, "FX", view=(30, 50))


if __name__ == "__main__":
    names = argv() or list(MODELS)
    for n in names:
        MODELS[n]()
