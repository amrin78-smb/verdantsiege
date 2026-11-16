"""Heartroot: the objective (left edge of the field). Compact ancient tree: soft buttress roots, a hollow facing
the field (+X, angled toward the camera side -Y and slightly up) holding the glowing HeartCore, golden sap veins,
and a rounded lush canopy kept high (bottom >= 2.3 m) so a ~54 deg camera on the -Y side sees the core.
Limits: <= 1.8 m along X, <= 3.6 m along Y, 3.0-3.4 m tall."""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_BARK0, C_BARK1 = hexc("#3B2A1E"), hexc("#86643F")
C_BARKHI = hexc("#A5835C")
C_MOSS0, C_MOSS1 = hexc("#3C7230"), hexc("#8CC653")
C_LEAF0, C_LEAF1, C_LEAF2 = hexc("#1F5C30"), hexc("#3F9440"), hexc("#B4E06A")
C_HOLLOW = hexc("#1A0F08")
C_SAP = hexc("#FFD45C", 1.0)
C_CORE0, C_CORE1 = hexc("#FFB02A", 1.0), hexc("#FFF8C8", 1.0)
C_BLOSSOM = hexc("#FFE3F0", 0.7)
C_BLOSSOM_C = hexc("#FFC94D", 1.0)

root = new_scene("Heartroot")
rnd = random.Random(42)

HZ = 0.85                                     # heart height
HDIR = Vector((math.cos(math.radians(-30)), math.sin(math.radians(-30)), 0.18)).normalized()   # hollow faces +X, toward -Y

def bark(p, n, fc, fi):
    c = mix(C_BARK0, C_BARK1, smoothstep(0.0, 1.9, p.z) * 0.8 + 0.2 * (0.5 + 0.5 * vnoise(p, 5)))
    c = shade(c, 0.9 + 0.2 * vnoise(p, 11, 3))
    return mix(c, mix(C_MOSS0, C_MOSS1, 0.5 + 0.5 * vnoise(p, 8)), smoothstep(0.62, 0.9, n.z) * 0.85)

# ---------------- trunk: flared, gently twisted, pinched hollow on the heart side
def trunk_r(z):
    return 0.36 + 0.3 * math.exp(-z * 3.2) - 0.04 * z
prof = [(trunk_r(z), z) for z in (0.0, 0.12, 0.3, 0.55, 0.8, 1.05, 1.35, 1.7, 2.05)]
haz = math.atan2(HDIR.y, HDIR.x)
def tj(pi, si, a):
    z = prof[pi][1]
    k = 1.0 + 0.08 * math.sin(a * 5 + pi * 0.8) + 0.04 * math.sin(a * 9 - pi)
    front = max(0.0, math.cos(a - haz)) ** 6
    if 0.45 < z < 1.3:
        k -= 0.4 * front * math.sin(math.pi * (z - 0.45) / 0.85)
    return k
trunk = lathe(prof, sides=14, jitter=tj, twist=0.12, cap_top=False, cap_bottom=False, round_profile=1)
# lip + bowl around the hollow, oriented along HDIR
HM = T(*(Vector((0, 0, HZ)) + HDIR * 0.2)) @ align(HDIR)
lip = lathe([(0.25, -0.05), (0.32, 0.0), (0.31, 0.065), (0.25, 0.095), (0.235, 0.0)], sides=14,
            jitter=lambda pi, si, a: 1 + 0.1 * math.sin(a * 3), cap_bottom=False, cap_top=False)
lip.transform(HM @ S(0.85, 1.2, 1))
cavity = lathe([(0.0, -0.16), (0.12, -0.14), (0.2, -0.07), (0.245, 0.0)], sides=14,
               jitter=lambda pi, si, a: 1 + 0.06 * math.sin(a * 3), cap_bottom=False, cap_top=False)
for i in range(len(cavity.f)):
    cavity.f[i] = cavity.f[i][::-1]
cavity.transform(HM @ S(0.85, 1.2, 1))
wood = merge(trunk, lip)
# branches: short, thick, curving up into the canopy
LIMBS = []
for k, ang in enumerate((-55, 55, 165, -150, 15)):
    a = math.radians(ang)
    d = Vector((math.cos(a) * 0.45, math.sin(a), 0)).normalized()
    p0 = Vector((0, 0, 1.75)) + d * 0.08
    p2 = p0 + d * (0.7 if abs(math.sin(a)) > 0.5 else 0.45) + Vector((0, 0, 0.75))
    pts = bezier(p0, p0 + d * 0.25 + Vector((0, 0, 0.45)), p2, n=5)
    wood.extend(tube(pts, taper(len(pts), 0.17, 0.07), sides=7, cap0=False, round1=True))
    LIMBS.append(p2)
# roots: six soft, thick buttresses, arching and tucking into the ground
for k, (ang, ln) in enumerate(((90, 1.3), (-90, 0.9), (40, 0.75), (-40, 0.7), (140, 0.75), (-140, 0.65))):
    a = math.radians(ang)
    d = Vector((math.cos(a), math.sin(a), 0))
    p0 = d * 0.2 + Vector((0, 0, 0.55))
    pts = bezier(p0, d * (0.2 + ln * 0.35) + Vector((0, 0, 0.42)), d * (0.2 + ln * 0.75) + Vector((0, 0, 0.12)),
                 d * (0.2 + ln) + Vector((0, 0, -0.06)), n=6)
    pts = [Vector((max(-0.85, min(0.85, p.x)), max(-1.7, min(1.7, p.y)), p.z)) for p in pts]
    wood.extend(tube(pts, taper(len(pts), 0.28, 0.1, 0.8), sides=8, cap0=False, round1=True, flat=(1.0, 0.8)))
paint_vertex_normals(wood, bark)
lip_n = len(lip.f)
cavity.paint(lambda p, n, fc, fi: mix(glow(shade(C_SAP, 0.85), 0.65), C_HOLLOW, smoothstep(0.15, 0.02, (p - Vector((0, 0, HZ))).dot(HDIR))))
# golden sap veins radiating from the hollow
veins = Geo()
for k in range(6):
    ang = haz + math.radians(-60 + 24 * k)
    up = 1 if k % 2 == 0 else -1
    pts = []
    for j in range(6):
        z = HZ + up * (0.3 + j * 0.12)
        if z < 0.08 or z > 1.75:
            break
        a = ang * (1 + 0.06 * j) + 0.12 * math.sin(j + k)
        r = trunk_r(z) * (1.0 + 0.08 * math.sin(a * 5)) + 0.022
        pts.append(Vector((math.cos(a) * r, math.sin(a) * r, z)))
    if len(pts) >= 3:
        veins.extend(tube(pts, taper(len(pts), 0.02, 0.008), sides=4, smooth=False))
veins.fill(C_SAP)
# moss cushions and a few mushrooms at the base
moss = Geo()
for k in range(8):
    a = 2 * math.pi * k / 8 + 0.3
    if math.cos(a - haz) > 0.75:
        continue
    r = trunk_r(0.05) + 0.05
    moss.extend(soft_blob((math.cos(a) * r, math.sin(a) * r, 0.02), (0.2, 0.18, 0.1), seed=k, lump=0.2, sides=8, rings=5,
                          flatten_bottom=True))
paint_vertex_normals(moss, lambda p, n, fc, fi: shade(mix(C_MOSS0, C_MOSS1, smoothstep(0.0, 0.12, p.z)), 1 + 0.1 * vnoise(p, 9)))
shrooms = Geo()
for k, (x, y, s) in enumerate(((0.5, -0.55, 1.0), (0.6, -0.42, 0.7), (-0.35, 0.65, 0.9))):
    st = lathe([(0, 0), (0.03 * s, 0), (0.025 * s, 0.1 * s), (0, 0.1 * s)], sides=8).fill(hexc("#F2E6CC"))
    cp = lathe([(0.0, 0.0), (0.09 * s, 0.0), (0.085 * s, 0.03 * s), (0.05 * s, 0.06 * s), (0, 0.07 * s)], sides=10, round_profile=1)
    cp.paint(lambda p, n, fc, fi: hexc("#FFB347", 0.7) if n.z < -0.3 else hexc("#E8763A", 0.15))
    cp.transform(T(0, 0, 0.1 * s))
    m = merge(st, cp); m.transform(T(x, y, 0)); shrooms.extend(m)
TR = part("Trunk", clamp_ground(merge(wood, cavity, veins, moss, shrooms)), root, (0, 0, 0))

# ---------------- heart core: big glowing seed with a petal halo, facing out of the hollow
hc = Vector((0, 0, HZ)) + HDIR * 0.22
core = ellipsoid((0, 0, 0), (0.15, 0.17, 0.23), sides=12, rings=8,
                 jitter=lambda pi, si, a: 1 + 0.05 * math.sin(a * 6))
core.paint(lambda p, n, fc, fi: mix(C_CORE0, C_CORE1, smoothstep(-0.05, 0.2, p.z * 0.6 + n.dot(HDIR) * 0.15 + 0.05)))
halo = Geo()
for k in range(9):
    lf = leaf(length=0.13, width=0.08, segs=3, across=1, curl=0.25, fold=0.1, thick=0.01)
    lf.transform(rot(360 * k / 9, 0, 0) @ T(0.0, 0, 0.21) @ rot(0, -75, 0))
    halo.extend(lf)
halo.fill(hexc("#FFE98A", 1.0))
hcg = merge(core, halo)
hcg.transform(T(*hc) @ align(HDIR) @ rot(0, -90, 0))
part("HeartCore", hcg, root, hc, parent=TR)

# ---------------- canopy: rounded, lush, soft clusters (bottom >= ~2.3 m), hanging glow blossoms
CB = Vector((0, 0, 2.3))
canopy = Geo()
# Canopy mass sits toward +Y (away from the camera on -Y) so the sightline from the HeartCore up to a ~54 deg
# camera stays clear: it passes y ~ -1.2 at the canopy underside (z ~2.45).
clusters = [((0.0, 0.35, 2.95), (0.78, 0.95, 0.5)), ((0.05, 1.2, 2.72), (0.6, 0.55, 0.42)),
            ((0.0, -0.4, 2.98), (0.6, 0.55, 0.38)), ((-0.35, 0.7, 2.8), (0.5, 0.6, 0.45)),
            ((0.38, 0.6, 2.8), (0.5, 0.6, 0.45)), ((0.15, 0.75, 3.1), (0.5, 0.55, 0.3)),
            ((-0.2, -0.05, 3.1), (0.5, 0.5, 0.3))]
for k, (c, r) in enumerate(clusters):
    b = soft_blob((c[0], c[1], c[2] - 0.08), r, seed=10 + k, lump=0.16, freq=1.8, sides=11, rings=7)
    canopy.extend(b)
paint_vertex_normals(canopy, foliage_soft(C_LEAF0, C_LEAF1, C_LEAF2, var=0.14, seed=2.0))
vines = Geo()
for k in range(8):
    a = 2 * math.pi * k / 8 + 0.4
    top = Vector((math.cos(a) * 0.55, 0.35 + math.sin(a) * 0.9, 2.55))
    ln = rnd.uniform(0.3, 0.55)
    pts = bezier(top, top + Vector((0.03, 0.02, -ln * 0.5)), top + Vector((0.0, 0.04, -ln)), n=3)
    vines.extend(tube(pts, 0.016, sides=5).fill(C_LEAF1))
    bl = Geo()
    for j in range(5):
        pl = leaf(length=0.07, width=0.06, segs=2, across=1, curl=0.4, fold=0.1, thick=0.008)
        pl.transform(rot(0, 0, 72 * j) @ rot(0, -30, 0))
        bl.extend(pl)
    bl.fill(C_BLOSSOM)
    bl.extend(sphere((0, 0, 0.01), 0.025, sides=6, rings=4).fill(C_BLOSSOM_C))
    bl.transform(T(*(pts[-1] - Vector((0, 0, 0.02)))) @ rot(180, 0, 0))
    vines.extend(bl)
part("Canopy", merge(canopy, vines), root, CB, parent=TR)

finish(root, "Battlefield", view=(30.0, 22.0))
