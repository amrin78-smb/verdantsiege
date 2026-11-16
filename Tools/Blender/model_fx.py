"""Projectiles (fly along +X, origin at centre, <= 200 tris) and evolution accessories (origin at the attachment point,
<= 600 tris). Usage: blender -b --factory-startup --python Tools/Blender/model_fx.py -- [Name ...]"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *
import vskit; vskit.RES = 1.0          # tiny budgets: explicit segment counts only

MODELS = {}
def model(fn):
    MODELS[fn.__name__] = fn
    return fn


def x_axis(g):
    """Lathes are built along +Z; turn them to fly along +X."""
    return g.transform(rot(0, 90, 0))


# ================================================================== projectiles
@model
def Thorn():
    root = new_scene("Thorn")
    shaft = lathe([(0.0, -0.17), (0.028, -0.14), (0.034, -0.06), (0.026, 0.06), (0.012, 0.14), (0.0, 0.18)], sides=10)
    x_axis(shaft)
    shaft.paint(lambda p, n, fc, fi: mix(hexc("#1E4A22"), hexc("#7FC94A", 0.35), smoothstep(0.02, 0.18, p.x)))
    barbs = Geo()
    for k in range(3):
        a = math.radians(120 * k + 20)
        d = Vector((0, math.cos(a), math.sin(a)))
        for x0 in (-0.03, 0.06):
            b = Vector((x0, 0, 0)) + d * 0.022
            barbs.extend(cone(b, b + d * 0.05 + Vector((-0.06, 0, 0)), 0.012, sides=3))
    barbs.fill(hexc("#2E5E2A"))
    part("Mesh", merge(shaft, barbs), root)
    finish(root, "FX", view=(60, 25))


@model
def FrostShard():
    root = new_scene("FrostShard")
    sh = lathe([(0.0, -0.13), (0.035, -0.06), (0.04, 0.03), (0.0, 0.17)], sides=6, smooth=False)
    x_axis(sh)
    sh.transform(S(1, 1, 0.8))
    sh.paint(lambda p, n, fc, fi: mix(hexc("#4FAED0", 0.15), hexc("#CFF6FF", 0.5), smoothstep(-0.1, 0.15, p.x + n.z * 0.05)), per_face=True)
    side = Geo()
    for k, d_ in enumerate(((0.6, 0.3, 0.5), (0.5, -0.5, -0.3), (0.4, 0.5, -0.5))):
        sd = lathe([(0.0, 0.0), (0.016, 0.02), (0.0, 0.08 - 0.015 * k)], sides=5, smooth=False)
        sd.transform(T(-0.03 + 0.03 * k, 0, 0.0) @ align(d_))
        side.extend(sd)
    side.fill(hexc("#9FE0F5", 0.35))
    part("Mesh", merge(sh, side), root)
    finish(root, "FX", view=(60, 25))


@model
def EmberSeed():
    root = new_scene("EmberSeed")
    body = lathe([(0.0, -0.12), (0.035, -0.1), (0.055, -0.04), (0.058, 0.02), (0.042, 0.08), (0.0, 0.13)], sides=12)
    x_axis(body)
    def crack(p, n, fc, fi):
        a = math.atan2(p.z, p.y)
        k = smoothstep(0.4, 0.1, abs(math.sin(a * 3 + p.x * 45)))
        base = mix(hexc("#3A1A12"), hexc("#7A2E16", 0.2), smoothstep(-0.1, 0.12, p.x))
        return mix(base, hexc("#FFB030", 1.0), k)
    body.paint(crack)
    tail = Geo()
    for k in range(3):
        a = math.radians(120 * k)
        b = Vector((-0.1, math.cos(a) * 0.02, math.sin(a) * 0.02))
        tail.extend(cone(b, b + Vector((-0.08, math.cos(a) * 0.03, math.sin(a) * 0.03)), 0.014, sides=3))
    tail.fill(hexc("#FF6A1A", 1.0))
    part("Mesh", merge(body, tail), root)
    finish(root, "FX", view=(60, 25))


@model
def WhirlSeed():
    root = new_scene("WhirlSeed")
    seed = ellipsoid((0.03, 0, 0), (0.05, 0.035, 0.03), sides=8, rings=5)
    seed.paint(lambda p, n, fc, fi: mix(hexc("#7A4A22"), hexc("#D0A050", 0.2), smoothstep(-0.3, 0.8, n.z)))
    wings = Geo()
    for side in (1, -1):
        w = leaf(length=0.13, width=0.07, segs=3, across=1, curl=0.08, fold=0.04, thick=0.008, tip_power=0.6)
        w.transform(rot(0, 0, 90 * side - 20 * side) @ rot(10 * side, 0, 0))
        w.transform(T(0.0, 0.02 * side, 0.0))
        wings.extend(w)
    wings.paint(lambda p, n, fc, fi: mix(hexc("#C9A052"), hexc("#FFE08A", 0.45), smoothstep(0.03, 0.14, abs(p.y))))
    part("Mesh", merge(seed, wings), root)
    finish(root, "FX", view=(60, 45))


@model
def SapperSpit():
    root = new_scene("SapperSpit")
    vskit.RES = 1.0
    glob = soft_blob((0, 0, 0), (0.1, 0.075, 0.07), seed=3, lump=0.25, freq=3.0, sides=9, rings=6)
    glob.paint(lambda p, n, fc, fi: mix(hexc("#3E9A12", 0.75), hexc("#C8FF4A", 0.9), smoothstep(-0.4, 0.8, n.z + n.x * 0.5)))
    drops = Geo()
    for k, (x, y, z, r) in enumerate(((-0.11, 0.02, 0.01, 0.025), (-0.15, -0.02, -0.015, 0.018), (-0.06, 0.05, -0.04, 0.016))):
        drops.extend(sphere((x, y, z), r, sides=6, rings=4).fill(hexc("#C4FF4D", 1.0)))
    part("Mesh", merge(glob, drops), root)
    finish(root, "FX", view=(60, 25))


# ================================================================== evolution accessories
def petal_ring(n, length, width, radius, tilt, z, c0, c1, curl=0.2, phase=0.0, segs=3):
    g = Geo()
    for k in range(n):
        a = 360 * (k + phase) / n
        lf = leaf(length=length, width=width, segs=segs, across=1, curl=curl, fold=0.15, thick=0.008, tip_power=0.8)
        lf.transform(rot(0, 0, a) @ T(radius, 0, z) @ rot(0, -tilt, 0))
        g.extend(lf)
    g.paint(lambda p, n_, fc, fi: mix(c0, c1, smoothstep(radius, radius + length, Vector((p.x, p.y, 0)).length)))
    return g


@model
def EvoSolar():
    root = new_scene("EvoSolar")
    band = lathe([(0.075, 0.0), (0.085, 0.01), (0.085, 0.03), (0.075, 0.04), (0.068, 0.02), (0.075, 0.0)], sides=16, cap_bottom=False, cap_top=False)
    band.fill(hexc("#E0A82A", 0.3))
    rays = petal_ring(10, 0.125, 0.06, 0.075, 30, 0.03, hexc("#FFB42A", 0.5), hexc("#FFF0A0", 1.0), curl=0.1)
    gems = Geo()
    for k in range(5):
        a = 2 * math.pi * k / 5
        gems.extend(sphere((math.cos(a) * 0.082, math.sin(a) * 0.082, 0.02), 0.012, sides=6, rings=4).fill(hexc("#FFF6C0", 1.0)))
    part("Mesh", merge(band, rays, gems), root)
    finish(root, "FX", view=(30, 40))


@model
def EvoPrism():
    root = new_scene("EvoPrism")
    g = Geo()
    for k, (x, y, ln, r, tilt, col) in enumerate(((0.0, 0.0, 0.22, 0.035, (0, 0, 1), "#9AE8FF"), (0.05, 0.04, 0.15, 0.026, (0.5, 0.4, 1), "#C8A0FF"),
                                                  (-0.05, 0.035, 0.14, 0.024, (-0.5, 0.35, 1), "#9AE8FF"), (0.02, -0.055, 0.16, 0.026, (0.2, -0.6, 1), "#FFB0F0"),
                                                  (-0.04, -0.04, 0.11, 0.02, (-0.5, -0.5, 1), "#C8A0FF"))):
        c = lathe([(0.0, 0.0), (r, 0.0), (r * 1.05, ln * 0.72), (0.0, ln)], sides=6, smooth=False)
        c.transform(T(x, y, 0) @ align(tilt))
        c.paint(lambda p, n, fc, fi, col=col: mix(shade(hexc(col, 0.2), 0.75), hexc(col, 0.65), smoothstep(0.02, 0.2, p.z) * 0.7 + 0.3 * max(0.0, n.z)), per_face=True)
        g.extend(c)
    base = lathe([(0.0, 0.0), (0.075, 0.0), (0.06, 0.02), (0.0, 0.025)], sides=8).fill(hexc("#5A4A6A"))
    part("Mesh", merge(base, g), root)
    finish(root, "FX", view=(30, 30))


@model
def EvoRoot():
    root = new_scene("EvoRoot")
    g = Geo()
    for strand in range(1):
        pts = []
        for i in range(31):
            a = 2 * math.pi * i / 30
            r = 0.34 + 0.025 * math.sin(a * 5 + strand * 2)
            pts.append(Vector((math.cos(a) * r, math.sin(a) * r, 0.025 + 0.012 * math.sin(a * 7 + strand * 3))))
        g.extend(tube(pts, 0.022, sides=5))
    g.paint(lambda p, n, fc, fi: mix(hexc("#3E6A2A"), hexc("#8ACB4A", 0.5), smoothstep(0.02, 0.045, p.z)))
    lv = Geo()
    for k in range(8):
        a = 2 * math.pi * k / 8 + 0.15
        lf = leaf(length=0.07, width=0.045, segs=2, across=1, curl=0.3, fold=0.15, thick=0.006)
        lf.transform(rot(0, 0, math.degrees(a)) @ T(0.34, 0, 0.035) @ rot(0, 0, 70 * (1 if k % 2 else -1)) @ rot(0, -15, 0))
        lv.extend(lf)
    lv.fill(hexc("#6FBF3A"))
    glows = Geo()
    for k in range(8):
        a = 2 * math.pi * k / 8 + 0.4
        glows.extend(sphere((math.cos(a) * 0.35, math.sin(a) * 0.35, 0.05), 0.02, sides=5, rings=3).fill(hexc("#C4FF4D", 1.0)))
    part("Mesh", clamp_ground(merge(g, lv, glows)), root)
    finish(root, "FX", view=(30, 50))


@model
def EvoBloom():
    root = new_scene("EvoBloom")
    outer = petal_ring(9, 0.13, 0.095, 0.07, 25, 0.0, hexc("#FF8AB0"), hexc("#FFE3F0", 0.25), curl=0.35)
    inner = petal_ring(6, 0.09, 0.08, 0.05, 45, 0.015, hexc("#FFB0C8"), hexc("#FFF6FA", 0.35), curl=0.4, phase=0.5)
    ring = tube([Vector((math.cos(2 * math.pi * i / 16) * 0.045, math.sin(2 * math.pi * i / 16) * 0.045, 0.018)) for i in range(17)],
                0.012, sides=4, cap0=False, cap1=False)
    ring.fill(hexc("#FFD24A", 0.9))
    stam = Geo()
    for k in range(6):
        a = 2 * math.pi * k / 6
        b = Vector((math.cos(a) * 0.045, math.sin(a) * 0.045, 0.02))
        stam.extend(sphere(b + Vector((0, 0, 0.03)), 0.01, sides=5, rings=3).fill(hexc("#FFE06A", 1.0)))
    part("Mesh", clamp_ground(merge(outer, inner, ring, stam)), root)
    finish(root, "FX", view=(30, 45))


if __name__ == "__main__":
    for n in (argv() or list(MODELS)):
        MODELS[n]()
