"""Bulwark Bloom: guardian. A mossy bulb shielded by two layers of bark-stone petal plates, a coral bud on top,
two amber eyes peeking through the front slit (+X). PlatesOuter drop at 66 % health, PlatesInner at 33 %."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_MOSS0, C_MOSS1 = hexc("#2D5E2B"), hexc("#86BE52")
C_CORE0, C_CORE1 = hexc("#2F6A4A"), hexc("#5FA866")
C_BARK0, C_BARK1 = hexc("#3F2C20"), hexc("#7A5A3E")
C_STONE0, C_STONE1 = hexc("#5C4E40"), hexc("#9C8160")
C_INNER0, C_INNER1 = hexc("#9C7A50"), hexc("#E0C48E")
C_MOSSTOP = hexc("#5E9A34")
C_BUD0, C_BUD1 = hexc("#C2456A"), hexc("#FF9FA8")
C_EYE = hexc("#FFC23D", 1.0)
C_EYEDARK = hexc("#2A1A10")
C_ROOT = hexc("#4E3B28")

root = new_scene("BulwarkBloom")
CZ = 0.30           # bulb centre height
BR = (0.24, 0.24, 0.27)

# ---------------- core: mossy bulb + root skirt + closed bud on top
bulb = ellipsoid((0, 0, CZ), BR, sides=14, rings=9,
                 jitter=lambda pi, si, a: 1.0 + 0.04 * math.sin(a * 7 + pi))
bulb.paint(lambda p, n, fc, fi: shade(mix(C_CORE0, C_CORE1, smoothstep(0.05, 0.55, p.z)),
                                      1.0 + 0.1 * (hash01(fc.x, fc.y, fc.z) - 0.5)))
skirt = Geo()
for i in range(9):
    a = 2 * math.pi * i / 9 + 0.2
    d = Vector((math.cos(a), math.sin(a), 0))
    p0 = d * 0.16 + Vector((0, 0, 0.12))
    pts = bezier(p0, d * 0.28 + Vector((0, 0, 0.08)), d * 0.36 + Vector((0, 0, 0.0)), n=3)
    skirt.extend(tube(pts, taper(len(pts), 0.05, 0.016), sides=5, cap0=False, round1=True))
skirt.paint(lambda p, n, fc, fi: mix(shade(C_ROOT, 0.8), C_MOSS0, smoothstep(0.0, 0.12, p.z)))
moss = Geo()
for i in range(10):
    a = 2 * math.pi * i / 10
    r = 0.22 + 0.03 * math.sin(i * 2.3)
    moss.extend(ellipsoid((math.cos(a) * r, math.sin(a) * r, 0.02), (0.085, 0.08, 0.06), sides=7, rings=4,
                          jitter=lambda pi, si, aa, i=i: 1 + 0.18 * hash01(pi, si, i) - 0.09, smooth=False))
moss.paint(lambda p, n, fc, fi: shade(mix(C_MOSS0, C_MOSS1, smoothstep(0.0, 0.08, p.z)), 1 + 0.15 * (hash01(fc.x, fc.y) - 0.5)),
           per_face=True)
bud = Geo()
for i in range(6):
    lf = leaf(length=0.14, width=0.1, segs=3, across=1, curl=1.6, fold=0.35, thick=0.014)
    lf.transform(T(0, 0, CZ + BR[2] * 0.88) @ rot(0, 0, 60 * i + 15) @ T(0.015, 0, 0) @ rot(0, -55, 0))
    bud.extend(lf)
bud.extend(ellipsoid((0, 0, CZ + BR[2] + 0.05), (0.05, 0.05, 0.075), sides=8, rings=5))
bud.paint(lambda p, n, fc, fi: mix(C_BUD0, C_BUD1, smoothstep(CZ + BR[2], CZ + BR[2] + 0.13, p.z)))
core = clamp_ground(merge(bulb, skirt, moss, bud))
C = part("Core", core, root, (0, 0, 0))

# ---------------- eyes: two amber almonds with dark lids, peeking from the front (+X)
eyes = Geo()
for side in (1, -1):
    ex, ey, ez = 0.235, 0.085 * side, CZ + 0.07
    e = ellipsoid((0, 0, 0), (0.04, 0.062, 0.045), sides=8, rings=5).fill(C_EYE)
    pupil = ellipsoid((0.033, 0, 0), (0.014, 0.02, 0.03), sides=6, rings=3).fill(hexc("#3A2208", 0.0))
    lid = shell(0, 200, 15, 80, (0.046, 0.068, 0.05), thick=0.012, nu=3, nv=2, point=0.0).fill(C_EYEDARK)
    eg = merge(e, pupil, lid)
    eg.transform(T(ex, ey, ez) @ rot(0, -8, 20 * side))
    eyes.extend(eg)
part("Eyes", eyes, root, (0.235, 0, CZ + 0.07), parent=C)

# ---------------- inner plates: lighter sandstone scales hugging the bulb, front slit for the eyes
inner = Geo()
for i in range(7):
    az = 360 * i / 7 + 180 / 7 + 180  # starts behind; no plate centred on +X
    rel = ((az + 180) % 360) - 180
    if abs(rel) < 30:
        continue
    pl = shell(az, 62, 2, 78, (BR[0] + 0.035, BR[1] + 0.035, BR[2] + 0.04), thick=0.035, nu=3, nv=3,
               center=(0, 0, CZ - 0.06), point=1.4, bulge=0.06, lift=0.06, seed=i)
    inner.extend(pl)
def inner_paint(p, n, fc, fi):
    c = mix(C_INNER0, C_INNER1, smoothstep(0.1, 0.55, p.z))
    c = mix(c, C_MOSSTOP, smoothstep(0.65, 0.95, n.z) * 0.7)
    return shade(c, 1 + 0.12 * (hash01(fc.x, fc.y, fc.z) - 0.5))
inner.paint(inner_paint, per_face=True)
part("PlatesInner", inner, root, (0, 0, 0), parent=C)

# ---------------- outer plates: big chipped bark-stone shields, lower and wider, with moss caps
outer = Geo()
for i in range(8):
    az = 360 * i / 8 + 22.5
    rel = ((az + 180) % 360) - 180
    span = 40 if abs(rel) < 30 else 58
    e1 = 50 if abs(rel) < 30 else 66
    pl = shell(az, span, -14, e1, (BR[0] + 0.1, BR[1] + 0.1, BR[2] + 0.08), thick=0.05, nu=3, nv=4,
               center=(0, 0, CZ - 0.07), point=1.1, bulge=0.08, lift=0.1, jit=0.008, seed=10 + i)
    outer.extend(pl)
# a heavy brow plate over the eyes
brow = shell(0, 70, 30, 52, (BR[0] + 0.07, BR[1] + 0.07, BR[2] + 0.06), thick=0.05, nu=4, nv=2,
             center=(0, 0, CZ - 0.02), point=0.4, bulge=0.05, lift=0.04, seed=99)
outer.extend(brow)
for p in outer.v:
    if p.z < 0.0:
        p.z = 0.0
def outer_paint(p, n, fc, fi):
    t = smoothstep(0.0, 0.5, p.z)
    c = mix(C_BARK0, C_STONE1, t)
    c = mix(c, C_STONE0, 0.35 * hash01(fc.x * 3.1, fc.y * 2.7))
    c = mix(c, C_MOSSTOP, max(smoothstep(0.4, 0.52, p.z), smoothstep(0.55, 0.9, n.z)) * 0.9)
    return shade(c, 0.92 + 0.16 * hash01(fc.x, fc.y, fc.z))
outer.paint(outer_paint, per_face=True)
part("PlatesOuter", outer, root, (0, 0, 0), parent=C)

finish(root, "Defenders", icon=True)
