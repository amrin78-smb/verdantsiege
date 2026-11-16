"""Heartroot: the objective. Ancient gnarled tree with roots spreading across the lanes (+-Y), a hollow facing +X
holding the glowing HeartCore seed, golden sap veins, layered canopy (sways) with hanging glow-blossoms."""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_BARK0, C_BARK1 = hexc("#3B2A1E"), hexc("#7B5A3E")
C_BARKHI = hexc("#9C7B57")
C_MOSS0, C_MOSS1 = hexc("#2F5F2A"), hexc("#7DB84A")
C_LEAF0, C_LEAF1, C_LEAF2 = hexc("#1F5A2E"), hexc("#3F8F3A"), hexc("#9AD25A")
C_HOLLOW = hexc("#1A0F08")
C_SAP = hexc("#FFD45C", 1.0)
C_CORE0, C_CORE1 = hexc("#FFB830", 1.0), hexc("#FFF6B0", 1.0)
C_BLOSSOM = hexc("#FFE3F0", 0.8)
C_BLOSSOM_C = hexc("#FFC94D", 1.0)

root = new_scene("Heartroot")
rnd = random.Random(42)

def bark(p, n, fc, fi):
    t = smoothstep(0.0, 1.8, p.z)
    c = mix(C_BARK0, C_BARK1, t)
    c = shade(c, 0.85 + 0.3 * hash01(fc.x * 3.7, fc.y * 2.1, fc.z * 1.3))
    if n.z > 0.8 and hash01(fc.x * 5.1, fc.y * 3.3) > 0.45:
        c = mix(c, C_MOSS1, 0.6)
    return c

# ---------------- trunk: twisted, flared base, hollow on the +X face
HZ = 0.95                         # hollow / heart height
def trunk_r(z):
    return 0.42 + 0.35 * math.exp(-z * 3.0) - 0.06 * z
prof = [(trunk_r(z), z) for z in (0.0, 0.15, 0.4, 0.7, 1.0, 1.3, 1.6, 1.85)]
def tj(pi, si, a):
    k = 1.0 + 0.1 * math.sin(a * 5 + pi * 0.8) + 0.05 * math.sin(a * 9 - pi)
    # hollow: pinch in the front between z 0.7 and 1.25
    z = prof[pi][1]
    front = max(0.0, math.cos(a)) ** 6
    if 0.6 < z < 1.35:
        k -= 0.45 * front * math.sin(math.pi * (z - 0.6) / 0.75)
    return k
trunk = lathe(prof, sides=16, jitter=tj, twist=0.18, cap_top=False, cap_bottom=False)
trunk.paint(bark, per_face=True)
# hollow rim (lip) and dark interior
lip = lathe([(0.25, -0.05), (0.33, 0.0), (0.32, 0.07), (0.25, 0.1), (0.235, 0.0)], sides=12,
            jitter=lambda pi, si, a: 1 + 0.12 * math.sin(a * 3), cap_bottom=False, cap_top=False)
lip.transform(T(0.27, 0, HZ) @ rot(0, 90, 0) @ S(1.25, 0.85, 1))
lip.paint(lambda p, n, fc, fi: shade(C_BARKHI, 0.85 + 0.3 * hash01(fc.x, fc.z)), per_face=True)
# concave bowl opening toward +X (faces reversed so the inside is front-facing)
cavity = lathe([(0.0, -0.16), (0.12, -0.14), (0.2, -0.07), (0.245, 0.0)], sides=12,
               jitter=lambda pi, si, a: 1 + 0.08 * math.sin(a * 3), cap_bottom=False, cap_top=False)
for i in range(len(cavity.f)):
    cavity.f[i] = cavity.f[i][::-1]
cavity.transform(T(0.27, 0, HZ) @ rot(0, 90, 0) @ S(1.25, 0.85, 1.0))
cavity.paint(lambda p, n, fc, fi: mix(glow(shade(C_SAP, 0.8), 0.6), C_HOLLOW, smoothstep(0.25, 0.14, p.x)))
# golden sap veins radiating from the hollow over the bark
veins = Geo()
for k in range(6):
    ang = math.radians(-70 + 28 * k)
    pts = []
    for j in range(6):
        z = HZ + (0.28 + j * 0.13) * (1 if k % 2 == 0 else -0.8)
        if z < 0.05: break
        a = ang * (1 + 0.1 * j) + 0.15 * math.sin(j + k)
        r = trunk_r(z) * (1.0 + 0.1 * math.sin(a * 5 + 0.0)) + 0.02
        pts.append(Vector((math.cos(a) * r, math.sin(a) * r, z)))
    if len(pts) >= 3:
        veins.extend(tube(pts, taper(len(pts), 0.022, 0.008), sides=4, smooth=False))
veins.fill(C_SAP)
# branches: 4 major limbs carrying the canopy
limbs = Geo()
LIMB_TIPS = []
for k, (ang, ln, up) in enumerate(((-60, 1.2, 0.7), (60, 1.25, 0.65), (170, 0.9, 0.85), (-150, 0.95, 0.8), (10, 0.7, 0.9))):
    a = math.radians(ang)
    d = Vector((math.cos(a) * 0.55, math.sin(a), 0)).normalized()
    p0 = Vector((0, 0, 1.6)) + d * 0.1
    p1 = p0 + d * ln * 0.4 + Vector((0, 0, up * 0.6))
    p2 = p0 + d * ln + Vector((0, 0, up * 0.85))
    pts = bezier(p0, p1, p2, n=5)
    limbs.extend(tube(pts, taper(len(pts), 0.22, 0.07), sides=8, cap0=False, cap1=True))
    LIMB_TIPS.append(p2)
limbs.paint(bark, per_face=True)
# roots: spread mostly sideways across the lanes, a few forward/back
roots = Geo()
for k, (ang, ln) in enumerate(((90, 2.2), (-90, 2.25), (55, 1.4), (-55, 1.35), (125, 1.5), (-125, 1.45), (20, 0.9),
                               (-20, 0.95), (160, 0.9), (200, 0.85))):
    a = math.radians(ang)
    d = Vector((math.cos(a), math.sin(a), 0))
    p0 = d * 0.25 + Vector((0, 0, 0.5))
    pts = bezier(p0, d * (0.25 + ln * 0.3) + Vector((0, 0, 0.45)), d * (0.25 + ln * 0.7) + Vector((0, 0, 0.12)),
                 d * (0.25 + ln) + Vector((0, 0, -0.05)), n=6)
    # keep the root extent inside the footprint (X <= +-1.2, Y <= +-2.45)
    pts = [Vector((max(-1.0, min(1.0, p.x)), max(-2.3, min(2.3, p.y)), p.z)) for p in pts]
    roots.extend(tube(pts, taper(len(pts), 0.3, 0.08, 0.6), sides=8, cap0=False, cap1=True, round1=True))
roots.paint(bark, per_face=True)
clamp_ground(roots)
# moss collar + base mushrooms
moss = Geo()
for k in range(14):
    a = 2 * math.pi * k / 14 + rnd.uniform(-0.2, 0.2)
    if math.cos(a) > 0.8:
        continue
    r = trunk_r(0.05) + rnd.uniform(-0.05, 0.08)
    moss.extend(ellipsoid((math.cos(a) * r, math.sin(a) * r, 0.04), (0.16, 0.14, 0.09), sides=7, rings=4,
                          jitter=lambda pi, si, aa, k=k: 1 + 0.2 * hash01(pi, si, k) - 0.1, smooth=False))
moss.paint(lambda p, n, fc, fi: shade(mix(C_MOSS0, C_MOSS1, smoothstep(0.0, 0.12, p.z)), 0.85 + 0.3 * hash01(fc.x, fc.y)), per_face=True)
shrooms = Geo()
for k, (x, y, s) in enumerate(((0.45, 0.55, 1.0), (0.5, 0.42, 0.7), (-0.3, -0.7, 0.9), (0.2, -0.75, 0.6))):
    st = lathe([(0, 0), (0.03 * s, 0), (0.025 * s, 0.1 * s), (0, 0.1 * s)], sides=6).fill(hexc("#F2E6CC"))
    cp = lathe([(0.0, 0.0), (0.09 * s, 0.0), (0.085 * s, 0.03 * s), (0.05 * s, 0.06 * s), (0, 0.07 * s)], sides=8)
    cp.paint(lambda p, n, fc, fi: hexc("#FFB347", 0.7) if n.z < -0.3 else hexc("#E8763A", 0.15))
    cp.transform(T(0, 0, 0.1 * s))
    m = merge(st, cp); m.transform(T(x, y, 0)); shrooms.extend(m)
trunk_all = clamp_ground(merge(trunk, lip, cavity, veins, limbs, roots, moss, shrooms))
TR = part("Trunk", trunk_all, root, (0, 0, 0))

# ---------------- heart core: faceted glowing seed with a halo of tiny petals
hc = Vector((0.25, 0, HZ))
core = ellipsoid((0, 0, 0), (0.14, 0.16, 0.22), sides=8, rings=6, smooth=False,
                 jitter=lambda pi, si, a: 1 + 0.08 * ((si + pi) % 2))
core.paint(lambda p, n, fc, fi: mix(C_CORE0, C_CORE1, smoothstep(-0.1, 0.12, p.z + 0.5 * p.x)), per_face=True)
halo = Geo()
for k in range(8):
    a = 360 * k / 8
    lf = leaf(length=0.12, width=0.07, segs=2, across=1, curl=0.2, fold=0.1, thick=0.01)
    lf.transform(rot(a, 0, 0) @ T(0.05, 0, 0.22) @ rot(0, -80, 0))
    halo.extend(lf)
halo.fill(hexc("#FFE98A", 1.0))
hcg = merge(core, halo)
hcg.transform(T(*hc))
part("HeartCore", hcg, root, hc, parent=TR)

# ---------------- canopy: layered faceted foliage clouds over the limbs, hanging glow blossoms
CB = Vector((0, 0, 2.05))           # canopy base / sway pivot
def leaf_paint(p, n, fc, fi):
    t = smoothstep(-0.3, 0.9, n.z)
    c = mix(C_LEAF0, C_LEAF1, t)
    c = mix(c, C_LEAF2, smoothstep(0.75, 1.0, n.z) * 0.8)
    if n.x > 0.4:
        c = shade(c, 1.08)
    return shade(c, 0.9 + 0.2 * hash01(fc.x * 1.3, fc.y * 2.1, fc.z))
canopy = Geo()
clusters = [(t + Vector((0, 0, 0.15)), 0.62) for t in LIMB_TIPS] + [(Vector((0.0, 0.0, 2.95)), 0.75),
            (Vector((0.15, 0.9, 2.65)), 0.6), (Vector((0.1, -0.95, 2.7)), 0.62), (Vector((-0.35, 0.0, 2.6)), 0.6)]
for k, (c, r) in enumerate(clusters):
    c = Vector((max(-0.45, min(0.45, c.x)), max(-1.7, min(1.7, c.y)), min(c.z, 3.5 - r * 0.62)))
    blob = icorock((c.x, c.y, c.z), (r * 0.95, r * 1.15, r * 0.62), subdiv=3, seed=100 + k, rough=0.22, flatten_bottom=False)
    tint = (0.92 + 0.16 * hash01(k, 1.7), k)
    blob.paint(lambda p, n, fc, fi, tint=tint: shade(leaf_paint(p, n, fc, fi), tint[0]), per_face=True)
    canopy.extend(blob)
    # a second, smaller highlight layer on top for depth
    top = icorock((c.x + 0.05, c.y - 0.05, c.z + r * 0.35), (r * 0.65, r * 0.7, r * 0.38), subdiv=2, seed=200 + k, rough=0.25,
                  flatten_bottom=False)
    top.paint(lambda p, n, fc, fi: shade(mix(leaf_paint(p, n, fc, fi), C_LEAF2, 0.35), 1.05), per_face=True)
    canopy.extend(top)
def _unused(p, n, fc, fi):
    t = smoothstep(-0.3, 0.9, n.z)
    c = mix(C_LEAF0, C_LEAF1, t)
    c = mix(c, C_LEAF2, smoothstep(0.75, 1.0, n.z) * 0.8)
    if n.x > 0.4:      # sun from the front-right
        c = shade(c, 1.08)
    return shade(c, 0.9 + 0.2 * hash01(fc.x * 1.3, fc.y * 2.1, fc.z))
vines = Geo()
for k in range(9):
    a = rnd.uniform(0, 2 * math.pi)
    rr = rnd.uniform(0.7, 1.2)
    top = Vector((math.cos(a) * rr * 0.6, math.sin(a) * rr * 1.2, rnd.uniform(2.3, 2.5)))
    ln = rnd.uniform(0.35, 0.7)
    pts = bezier(top, top + Vector((0.03, 0.02, -ln * 0.5)), top + Vector((0.0, 0.05, -ln)), n=3)
    vines.extend(tube(pts, 0.018, sides=4, smooth=False).fill(C_LEAF1))
    bl = Geo()
    for j in range(5):
        pl = leaf(length=0.07, width=0.06, segs=2, across=1, curl=0.4, fold=0.1, thick=0.008)
        pl.transform(rot(0, 0, 72 * j) @ rot(0, -30, 0))
        bl.extend(pl)
    bl.fill(C_BLOSSOM)
    bl.extend(sphere((0, 0, 0.01), 0.025, sides=5, rings=3).fill(C_BLOSSOM_C))
    bl.transform(T(*(pts[-1] - Vector((0, 0, 0.02)))) @ rot(180, 0, 0))
    vines.extend(bl)
part("Canopy", merge(canopy, vines), root, CB, parent=TR)

finish(root, "Battlefield", view=(30.0, 22.0))
