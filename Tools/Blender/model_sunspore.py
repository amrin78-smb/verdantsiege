"""Sunspore: basic lane artillery. Golden seed-pod head on a curved stem, petal-fringed barrel facing +X."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *
import vskit; vskit.RES = 1.5  # per-model smoothness within the 4000 budget

C_STEM0, C_STEM1 = hexc("#2F6B2A"), hexc("#7CC243")
C_LEAF0, C_LEAF1 = hexc("#2E7A2E"), hexc("#A6DB4E")
C_POD_LO, C_POD, C_POD_HI = hexc("#C8502A"), hexc("#F09A32"), hexc("#FFD567")
C_COLLAR = hexc("#F7E2B0")
C_PETAL0, C_PETAL1 = hexc("#F2683A"), hexc("#FFD447")
C_INNER = hexc("#3B1D0E")
C_GLOW = hexc("#FFE98A", 1.0)
C_FRECKLE = hexc("#FFE45C", 0.9)
C_ROOT = hexc("#5A4A2C")

root = new_scene("Sunspore")

# ---------------- stem: S-curve, thick base flare, root nubs
neck = Vector((0.0, 0.0, 0.47))
pts = bezier((-0.02, 0, 0.0), (-0.12, 0, 0.20), (0.10, 0, 0.28), (0.0, 0, 0.47), n=8)
stem = tube(pts, taper(len(pts), 0.075, 0.048, 0.7), sides=10, cap0=True, cap1=True)
flare = lathe([(0.0, 0.0), (0.13, 0.0), (0.11, 0.025), (0.075, 0.06), (0.0, 0.07)], sides=10)
flare.transform(T(-0.02, 0, 0))
stem = merge(stem, flare)
stem.paint(grad_z(C_STEM0, C_STEM1, 0.0, 0.5, var=0.06))
roots = Geo()
for i in range(5):
    a = math.radians(30 + i * 72)
    d = Vector((math.cos(a), math.sin(a), 0))
    p0 = Vector((-0.02, 0, 0.03)) + d * 0.07
    rp = bezier(p0, p0 + d * 0.07 + Vector((0, 0, 0.01)), p0 + d * 0.14 - Vector((0, 0, 0.03)), n=3)
    roots.extend(tube(rp, taper(len(rp), 0.028, 0.008), sides=5, cap0=False, round1=True))
roots.paint(grad_z(shade(C_ROOT, 0.8), C_STEM0, 0.0, 0.05, var=0.1))
stem = clamp_ground(merge(stem, roots))
part("Stem", stem, root, (0, 0, 0))

# ---------------- head: ridged teardrop seed-capsule, sun-petal corolla around a glowing barrel
hc = neck + Vector((0.02, 0, 0.16))          # capsule centre
L_BACK, L_FRONT, RAD = 0.25, 0.17, 0.165
# teardrop profile along X (r, x): pointed tail at the back (-X), fat front
prof = [(0.0, -L_BACK), (0.035, -L_BACK + 0.03), (0.09, -0.15), (0.14, -0.07), (RAD, 0.0), (0.158, 0.08),
        (0.13, 0.135), (0.095, L_FRONT)]
def ridges(pi, si, a):
    return 1.0 + 0.09 * max(0.0, math.cos(a * 6)) ** 2 - 0.03
pod = lathe(prof, sides=18, jitter=ridges, cap_bottom=False, cap_top=False, round_profile=1)
pod.transform(T(*hc) @ rot(0, 90, 0) @ S(1, 1, 1))
# lathe axis is Z -> after rot(0,90,0) local z maps to +X? (rot Y +90: z -> +x)
def pod_paint(p, n, fc, fi):
    t = (p.z - (hc.z - RAD)) / (2 * RAD)
    c = mix(C_POD_LO, C_POD, smoothstep(0.0, 0.5, t))
    c = mix(c, C_POD_HI, smoothstep(0.55, 1.05, t))
    ang = math.atan2(p.z - hc.z, p.y - hc.y)
    ridge = max(0.0, math.cos(ang * 6)) ** 2
    c = shade(c, 0.86 + 0.2 * ridge)
    back = smoothstep(hc.x - 0.05, hc.x - L_BACK, p.x)
    return mix(c, shade(C_POD_LO, 0.85), back * 0.6)
pod.paint(pod_paint)
# tail curl at the back
tail_pts = bezier(hc + Vector((-L_BACK + 0.02, 0, 0)), hc + Vector((-L_BACK - 0.08, 0, 0.02)),
                  hc + Vector((-L_BACK - 0.08, 0, 0.11)), hc + Vector((-L_BACK - 0.01, 0, 0.1)), n=6)
tail = tube(tail_pts, taper(len(tail_pts), 0.03, 0.012), sides=6, cap0=False, round1=True)
tail.paint(grad_axis(C_POD_LO, C_STEM1, 0, 1, axis=(0, 0, 1)))
tail.paint(lambda p, n, fc, fi: mix(shade(C_POD_LO, 0.9), C_LEAF1, smoothstep(hc.z, hc.z + 0.1, p.z)))

front_x = hc.x + L_FRONT
# collar lip
collar = lathe([(0.088, -0.02), (0.112, 0.0), (0.118, 0.03), (0.1, 0.055), (0.078, 0.05), (0.07, 0.025)],
               sides=18, cap_bottom=False, cap_top=False)
collar.transform(T(front_x, 0, hc.z) @ rot(0, 90, 0))
collar.paint(grad_axis(shade(C_COLLAR, 0.8), C_COLLAR, front_x - 0.02, front_x + 0.05, axis=(1, 0, 0)))
throat = lathe([(0.07, 0.025), (0.066, -0.02), (0.05, -0.07), (0.0, -0.08)], sides=12, cap_bottom=False, cap_top=False)
throat.transform(T(front_x, 0, hc.z) @ rot(0, 90, 0))
throat.paint(lambda p, n, fc, fi: mix(C_GLOW, C_INNER, smoothstep(-0.075, 0.0, p.x - front_x) ** 0.7))
core = sphere((front_x - 0.03, 0, hc.z), 0.042, sides=10, rings=6).fill(C_GLOW)

# corolla: two rings of sun petals radiating around the barrel, cupped forward
petals = Geo()
for ring, (n_p, ln, wd, tilt, off) in enumerate(((11, 0.15, 0.085, 0.55, 0.0), (9, 0.11, 0.07, 0.95, 0.5))):
    for i in range(n_p):
        a = 2 * math.pi * (i + off) / n_p
        r = Vector((0, math.cos(a), math.sin(a)))
        xd = (r + Vector((tilt * 0.45, 0, 0))).normalized()
        pl = leaf(length=ln, width=wd, segs=3, across=1, curl=0.25, fold=0.12, thick=0.012, tip_power=0.7)
        org = Vector((front_x - 0.01 - ring * 0.012, 0, hc.z)) + r * (0.1 - ring * 0.008)
        pl.transform(basis(xd, Vector((1, 0, 0)), org))
        petals.extend(pl)
def petal_paint(p, n, fc, fi):
    d = Vector((0, p.y, p.z - hc.z)).length
    c = mix(C_PETAL0, C_PETAL1, smoothstep(0.11, 0.24, d))
    return shade(c, 1.05 if n.x > -0.2 else 0.9)
petals.paint(petal_paint)

# leafy crest on top, swept back
crest = Geo()
for yaw, ln in ((0, 0.17), (32, 0.12), (-32, 0.12)):
    lf = leaf(length=ln, width=0.08, segs=4, across=1, curl=0.6, fold=0.3, thick=0.012)
    lf.transform(T(hc.x - 0.06, 0, hc.z + RAD * 0.9) @ rot(0, 0, 180 + yaw) @ rot(0, -40, 0))
    crest.extend(lf)
crest.paint(lambda p, n, fc, fi: mix(C_LEAF0, C_LEAF1, smoothstep(hc.z + 0.12, hc.z + 0.3, p.z)))
# calyx cradling the capsule
calyx = Geo()
for i in range(5):
    lf = leaf(length=0.12, width=0.08, segs=2, across=1, curl=0.7, fold=0.25, thick=0.012)
    lf.transform(T(neck.x, 0, neck.z + 0.02) @ rot(0, 0, 360 * i / 5 + 20) @ rot(0, -50, 0))
    calyx.extend(lf)
calyx.paint(grad_z(C_STEM0, C_LEAF1, neck.z, neck.z + 0.1))

# glowing spore freckles in the ridge valleys of the upper capsule
freck = Geo()
for k, (u, ang) in enumerate(((-0.12, 75), (-0.04, 105), (0.04, 70), (-0.17, 110), (0.0, 135), (0.02, 45),
                              (-0.1, 140), (-0.08, 40))):
    a = math.radians(ang + 15)
    rr = 0.0
    x = hc.x + u
    # radius of the teardrop at x (approx from profile)
    import bisect
    xs = [q[1] for q in prof]; rs = [q[0] for q in prof]
    lx = x - hc.x
    for j in range(len(xs) - 1):
        if xs[j] <= lx <= xs[j + 1]:
            t = (lx - xs[j]) / (xs[j + 1] - xs[j]); rr = rs[j] + (rs[j + 1] - rs[j]) * t
    p = Vector((x, math.cos(a) * rr, hc.z + math.sin(a) * rr))
    freck.extend(sphere(p, 0.016 + 0.004 * (k % 3), sides=5, rings=3))
freck.fill(C_FRECKLE)

head = merge(pod, tail, collar, throat, core, petals, crest, calyx, freck)
H = part("Head", head, root, neck)
marker("Muzzle", root, (front_x + 0.06, 0, hc.z), parent=H)

# ---------------- base leaves: big, broad, curling up at the tips
def base_leaf(side):
    g = Geo()
    for yaw, ln, wd, pitch in ((100, 0.34, 0.26, -16), (148, 0.26, 0.2, -26)):
        lf = leaf(length=ln, width=wd, segs=6, across=2, curl=0.32, fold=0.18, thick=0.022, droop=0.05, tip_power=0.75)
        lf.transform(rot(0, pitch, 0))
        lf.transform(rot(0, 0, yaw if side > 0 else -yaw))
        lf.transform(T(-0.03, 0.03 * side, 0.035))
        g.extend(lf)
    g.paint(lambda p, n, fc, fi: shade(mix(C_LEAF0, C_LEAF1, smoothstep(0.05, 0.32, Vector((p.x + 0.03, p.y, 0)).length)),
                                       1.0 if n.z > 0 else 0.78))
    return g

part("LeafL", base_leaf(1), root, (-0.03, 0.03, 0.035))
part("LeafR", base_leaf(-1), root, (-0.03, -0.03, 0.035))

finish(root, "Defenders", icon=True)
