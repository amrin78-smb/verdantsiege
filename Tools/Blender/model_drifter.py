"""Drifter: basic Blight husk walking toward -X. Hunched fungal body, bell-shaped hood with drooping glowing
tendrils, one big lime eye, long clawed arms, stubby legs. Facing -X: its LEFT side is -Y."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_BODY0, C_BODY1 = hexc("#2E1A36"), hexc("#6E4577")
C_BELLY = hexc("#A07A98")
C_HOOD0, C_HOOD1 = hexc("#3A1F47"), hexc("#9A5E9C")
C_FRILL0, C_FRILL1 = hexc("#B98AA6"), hexc("#F0D2B4")
C_GLOW = hexc("#C4FF4D", 1.0)
C_GLOWDIM = hexc("#86C43A", 0.6)
C_EYEWHITE = hexc("#F4FFD8", 1.0)
C_PUPIL = hexc("#1A0F12")
C_CLAW = hexc("#E4D3B8")
C_MAW = hexc("#140A12")

root = new_scene("Drifter")
HIP = Vector((0.04, 0, 0.34))
NECK = Vector((-0.17, 0, 0.86))

# ---------------- body: hunched, tilted forward (-X)
torso = ellipsoid((0, 0, 0), (0.2, 0.22, 0.3), sides=14, rings=9,
                  jitter=lambda pi, si, a: 1 + 0.05 * math.sin(a * 5 + pi * 1.3))
torso.transform(T(-0.03, 0, 0.6) @ rot(0, -28, 0))
def body_paint(p, n, fc, fi):
    c = mix(C_BODY0, C_BODY1, smoothstep(0.3, 0.85, p.z))
    if n.x < -0.35 and p.z < 0.72:                     # lighter belly on the front
        c = mix(c, C_BELLY, smoothstep(-0.35, -0.8, n.x) * 0.8)
    return shade(c, 1 + 0.08 * (hash01(fc.x, fc.y, fc.z) - 0.5))
torso.paint(body_paint)
pelvis = ellipsoid((HIP.x, 0, HIP.z + 0.02), (0.15, 0.19, 0.11), sides=12, rings=6).paint(body_paint)
# shelf-fungus brackets growing on the back (+X): flattened half-discs, pale peach tops, lilac undersides
frills = Geo()
for k, (x, y, z, s_) in enumerate(((0.17, -0.08, 0.5, 1.0), (0.14, 0.1, 0.62, 1.2), (0.1, -0.04, 0.76, 0.95),
                                    (0.16, 0.14, 0.44, 0.75), (0.04, 0.16, 0.84, 0.7))):
    br = ellipsoid((0, 0, 0), (0.085 * s_, 0.11 * s_, 0.032 * s_), sides=10, rings=4,
                   jitter=lambda pi, si, aa, k=k: 1 + 0.12 * math.sin(aa * 4 + k), smooth=False)
    br.transform(T(x, y, z) @ rot(0, -12, 0))
    frills.extend(br)
frills.paint(lambda p, n, fc, fi: shade(C_FRILL1 if n.z > 0.1 else C_FRILL0, 0.9 + 0.2 * hash01(fc.x, fc.y)), per_face=True)
# glowing spore nodules on the shoulders/back
nod = Geo()
for k, (x, y, z, r) in enumerate(((0.02, 0.17, 0.78, 0.035), (0.08, -0.15, 0.7, 0.03), (0.12, 0.02, 0.86, 0.028),
                                  (-0.04, -0.2, 0.62, 0.025), (0.14, 0.15, 0.58, 0.022))):
    nod.extend(sphere((x, y, z), r, sides=6, rings=4).fill(C_GLOW))
part("Body", merge(torso, pelvis, frills, nod), root, HIP)

# ---------------- head: bell-hood with tendrils, single great eye, jagged maw
hc = NECK + Vector((-0.06, 0, 0.09))
skull = ellipsoid(hc, (0.14, 0.15, 0.13), sides=12, rings=8).paint(body_paint)
hood_prof = [(0.0, 0.17), (0.08, 0.16), (0.14, 0.11), (0.18, 0.03), (0.2, -0.04), (0.17, -0.05)]
hood = lathe(hood_prof[::-1], sides=14, jitter=lambda pi, si, a: 1 + (0.14 * math.sin(a * 6) + 0.05 * math.sin(a * 11) if pi <= 1 else 0.02 * math.sin(a * 3)),
             cap_bottom=False, cap_top=False)
hood_in = lathe([(0.0, 0.06), (0.07, 0.05), (0.13, 0.02), (0.17, -0.05)], sides=14,
                jitter=lambda pi, si, a: 1 + (0.14 * math.sin(a * 6) + 0.05 * math.sin(a * 11) if pi == 3 else 0), cap_bottom=False, cap_top=False)
def hood_paint(p, n, fc, fi):
    return mix(C_HOOD1, C_HOOD0, smoothstep(-0.05, 0.18, p.z))
hood.paint(hood_paint)
hood_in.fill(shade(C_HOOD0, 0.6))
hd = merge(hood, hood_in)
hd.transform(T(hc.x + 0.06, 0, hc.z + 0.07) @ rot(0, 34, 0))  # hood tips back so the eye shows
# drooping tendrils from the hood rim with glowing tips
tend = Geo()
hood_m = T(hc.x + 0.06, 0, hc.z + 0.07) @ rot(0, 34, 0)
for k in range(8):
    a = math.radians(40 + 280 * k / 7)      # around the rim, leaving the very front (-X, 180 deg) open
    a = math.radians(180) + (a - math.radians(180)) if True else a
    ang = math.radians(-150 + 300 * k / 7)  # -150..150 around +X (0) -> back and sides, gap at the face
    d = Vector((math.cos(ang), math.sin(ang), 0))
    p0 = hood_m @ (d * 0.18 + Vector((0, 0, -0.035)))
    ln = 0.2 + 0.08 * hash01(k, 3)
    sw = Vector((d.x, d.y, 0)) * 0.05
    pts = bezier(p0, p0 + sw - Vector((0, 0, ln * 0.45)), p0 + sw * 0.6 - Vector((0, 0, ln)), n=4)
    tend.extend(tube(pts, taper(len(pts), 0.026, 0.012), sides=5, cap0=True, cap1=True).paint(
        lambda p, n, fc, fi: mix(C_HOOD1, C_HOOD0, 0.3)))
    tend.extend(sphere(pts[-1] - Vector((0, 0, 0.015)), 0.026, sides=6, rings=4).fill(C_GLOW))
# eye: big lime orb with a dark slit, set in a socket on the -X face
eye_c = hc + Vector((-0.13, 0, 0.0))
eye = Geo()
eye.extend(ellipsoid(eye_c, (0.06, 0.088, 0.088), sides=12, rings=7).fill(C_GLOW))
eye.extend(ellipsoid(eye_c + Vector((-0.055, 0, 0)), (0.012, 0.02, 0.06), sides=6, rings=4).fill(C_PUPIL))
socket = lathe([(0.1, -0.02), (0.115, 0.0), (0.105, 0.025), (0.088, 0.02)], sides=12, cap_bottom=False, cap_top=False)
socket.transform(T(*eye_c) @ rot(0, -90, 0))
eye.extend(socket.fill(C_BODY0))
# jagged maw under the eye
maw = ellipsoid(hc + Vector((-0.115, 0, -0.085)), (0.03, 0.08, 0.03), sides=8, rings=4).fill(C_MAW)
teeth = Geo()
for k in range(5):
    y = -0.06 + 0.03 * k
    b = hc + Vector((-0.13, y, -0.06))
    teeth.extend(cone(b, b + Vector((-0.005, 0, -0.035)), 0.012, sides=3).fill(C_CLAW))
head = merge(skull, hd, tend, eye, maw, teeth)
part("Head", head, root, NECK)

# ---------------- arms: long, hanging forward, three claws
def arm(side):
    sh = Vector((-0.08, 0.22 * side, 0.76))
    el = Vector((-0.2, 0.28 * side, 0.5))
    wr = Vector((-0.27, 0.25 * side, 0.27))
    pts = bezier(sh, el + Vector((0.03, 0, 0.04)), wr, n=6)
    g = tube(pts, taper(len(pts), 0.065, 0.04), sides=7, cap0=True, cap1=False)
    g.paint(lambda p, n, fc, fi: mix(C_BODY0, C_BODY1, smoothstep(0.3, 0.75, p.z)))
    hand = ellipsoid(wr + Vector((0, 0, -0.02)), (0.055, 0.05, 0.05), sides=8, rings=5).paint(body_paint)
    g.extend(hand)
    for k in range(3):
        a = math.radians(-35 + 35 * k)
        d = Vector((-math.cos(a) * 0.55, math.sin(a) * 0.5 * side, -0.85)).normalized()
        b = wr + Vector((-0.02, 0, -0.04)) + Vector((d.x, d.y, 0)) * 0.03
        g.extend(cone(b, b + d * 0.09, 0.016, sides=4).fill(C_CLAW))
    sp = sphere(sh + Vector((0.0, 0.02 * side, 0.03)), 0.075, sides=8, rings=5).paint(body_paint)
    g.extend(sp)
    return g, sh

for side, nm in ((-1, "ArmL"), (1, "ArmR")):
    g, sh = arm(side)
    part(nm, g, root, sh)

# ---------------- legs: stubby, wide feet pointing -X
def legg(side):
    hip = Vector((HIP.x, 0.1 * side, HIP.z))
    knee = Vector((HIP.x - 0.03, 0.12 * side, 0.17))
    ank = Vector((HIP.x + 0.0, 0.13 * side, 0.06))
    pts = bezier(hip, knee, ank, n=4)
    g = tube(pts, taper(len(pts), 0.08, 0.055), sides=7, cap0=True, cap1=False)
    foot = ellipsoid((ank.x - 0.05, ank.y, 0.04), (0.1, 0.065, 0.045), sides=8, rings=5)
    for p in foot.v:
        if p.z < 0: p.z = 0.0
    g.extend(foot)
    g.paint(lambda p, n, fc, fi: mix(shade(C_BODY0, 0.8), C_BODY1, smoothstep(0.0, 0.35, p.z)))
    for k in range(3):
        b = Vector((ank.x - 0.13, ank.y + (k - 1) * 0.035, 0.03))
        g.extend(cone(b, b + Vector((-0.04, (k - 1) * 0.01, -0.02)), 0.015, sides=4).fill(C_CLAW))
    return g, hip

for side, nm in ((-1, "LegL"), (1, "LegR")):
    g, hip = legg(side)
    part(nm, clamp_ground(g), root, hip)

finish(root, "Enemies", icon=True)
