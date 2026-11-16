"""Plated Brute: slow armoured front-liner walking toward -X. Hulking maroon blight beast with a fungal hump,
huge fists and small glowing eyes. ALL armour (shoulder pads, chest plate, brow guard, bracers) is one object
`Armor` (origin at the hips, parented to Body so it follows the body) that can be removed when broken; the body
under it is fully modelled."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *
import vskit; vskit.RES = 1.35  # per-model smoothness within the 6000 budget

C_SKIN0, C_SKIN1 = hexc("#24121F"), hexc("#64304C")
C_BELLY = hexc("#86506A")
C_HUMP0, C_HUMP1 = hexc("#4F6A2A"), hexc("#A8D94A")
C_PUS = hexc("#D4FF5A", 1.0)
C_EYE = hexc("#FF5A3C", 1.0)
C_BONE0, C_BONE1 = hexc("#8A7558"), hexc("#E8DCC0")
C_BARK = hexc("#4A3424")
C_RUNE = hexc("#C4FF4D", 1.0)
C_TUSK = hexc("#F0E6CF")
C_NAIL = hexc("#2A1A14")

root = new_scene("PlatedBrute")
HIP = Vector((0.06, 0, 0.5))
NECK = Vector((-0.2, 0, 1.12))

def skin(p, n, fc, fi):
    c = mix(C_SKIN0, C_SKIN1, smoothstep(0.2, 1.2, p.z))
    if n.x < -0.4 and 0.55 < p.z < 1.0:
        c = mix(c, C_BELLY, smoothstep(-0.4, -0.85, n.x) * 0.7)
    return shade(c, 1 + 0.08 * (hash01(fc.x, fc.y, fc.z) - 0.5))

# ---------------- body: pelvis + barrel torso + fungal hump
torso = ellipsoid((0, 0, 0), (0.27, 0.32, 0.36), sides=16, rings=10,
                  jitter=lambda pi, si, a: 1 + 0.04 * math.sin(a * 4 + pi))
torso.transform(T(0.0, 0, 0.92) @ rot(0, -22, 0))
torso.vpaint(skin)
pelvis = ellipsoid((HIP.x, 0, HIP.z + 0.04), (0.2, 0.26, 0.16), sides=12, rings=6).vpaint(skin)
hump = Geo()
for k, (x, y, z, r) in enumerate(((0.16, 0.0, 1.12, 0.17), (0.2, 0.14, 1.0, 0.12), (0.2, -0.13, 1.02, 0.13), (0.12, 0.08, 1.24, 0.1))):
    hump.extend(soft_blob((x, y, z), (r, r, r * 0.85), seed=k, lump=0.18, sides=10, rings=7))
paint_vertex_normals(hump, lambda p, n, fc, fi: shade(mix(C_HUMP0, C_HUMP1, smoothstep(0.95, 1.3, p.z)), 1 + 0.12 * vnoise(p, 6)))
pus = Geo()
for k, (x, y, z) in enumerate(((0.26, 0.05, 1.18), (0.3, -0.12, 1.05), (0.28, 0.17, 1.06), (0.18, -0.05, 1.3), (0.22, 0.14, 1.24))):
    pus.extend(sphere((x, y, z), 0.032, sides=6, rings=4).fill(C_PUS))
part_body = merge(torso, pelvis, hump, pus)
B = part("Body", part_body, root, HIP)

# ---------------- head: low-slung, tusked, small glowing eyes
hc = NECK + Vector((-0.08, 0, 0.0))
skull = ellipsoid(hc, (0.14, 0.15, 0.12), sides=12, rings=7).vpaint(skin)
jaw = ellipsoid(hc + Vector((-0.05, 0, -0.08)), (0.12, 0.15, 0.07), sides=10, rings=5).vpaint(skin)
eyes = Geo()
for side in (1, -1):
    eyes.extend(ellipsoid(hc + Vector((-0.125, 0.06 * side, 0.03)), (0.02, 0.028, 0.018), sides=6, rings=4).fill(C_EYE))
tusks = Geo()
for side in (1, -1):
    b = hc + Vector((-0.13, 0.09 * side, -0.08))
    pts = bezier(b, b + Vector((-0.05, 0.02 * side, 0.05)), b + Vector((-0.03, 0.05 * side, 0.12)), n=4)
    tusks.extend(tube(pts, taper(len(pts), 0.026, 0.006), sides=5, cap0=True).fill(C_TUSK))
part("Head", merge(skull, jaw, eyes, tusks), root, NECK, parent=B)

# ---------------- arms: massive, knuckles near the ground
def arm(side):
    sh = Vector((-0.04, 0.31 * side, 1.08))
    el = Vector((-0.1, 0.37 * side, 0.7))
    wr = Vector((-0.17, 0.34 * side, 0.36))
    up = tube(bezier(sh, sh + Vector((-0.03, 0.08 * side, -0.18)), el, n=4), taper(5, 0.13, 0.1), sides=8, cap0=True, cap1=True)
    fo = tube(bezier(el, el + Vector((-0.05, 0.01 * side, -0.16)), wr, n=4), taper(5, 0.1, 0.11), sides=8, cap0=True, cap1=True)
    g = merge(up, fo).vpaint(skin)
    deltoid = sphere(sh, 0.15, sides=9, rings=5).vpaint(skin)
    fist = ellipsoid(wr + Vector((-0.04, 0, -0.1)), (0.14, 0.13, 0.13), sides=9, rings=5).vpaint(skin)
    g.extend(deltoid); g.extend(fist)
    for k in range(4):
        y = (k - 1.5) * 0.055
        b = wr + Vector((-0.14, y, -0.13))
        g.extend(ellipsoid(b, (0.04, 0.03, 0.035), sides=6, rings=4).vpaint(skin))
        g.extend(cone(b + Vector((-0.03, 0, 0)), b + Vector((-0.06, 0, -0.02)), 0.016, sides=4).fill(C_NAIL))
    return g, sh

for side, nm in ((-1, "ArmL"), (1, "ArmR")):
    g, sh = arm(side)
    part(nm, g, root, sh, parent=B)

# ---------------- legs: short, thick, hoof-like feet
def legg(side):
    hip = Vector((HIP.x, 0.15 * side, HIP.z))
    knee = Vector((HIP.x - 0.06, 0.18 * side, 0.27))
    ank = Vector((HIP.x - 0.02, 0.19 * side, 0.08))
    g = tube(bezier(hip, knee, ank, n=4), taper(5, 0.13, 0.09), sides=9, cap0=True, cap1=True).vpaint(skin)
    foot = ellipsoid((ank.x - 0.06, ank.y, 0.06), (0.15, 0.11, 0.07), sides=10, rings=5)
    foot.vpaint(skin)
    g.extend(foot)
    for k in range(3):
        b = Vector((ank.x - 0.19, ank.y + (k - 1) * 0.05, 0.035))
        g.extend(cone(b, b + Vector((-0.05, 0, -0.03)), 0.022, sides=4).fill(C_NAIL))
    return g, hip

for side, nm in ((-1, "LegL"), (1, "LegR")):
    g, hip = legg(side)
    part(nm, clamp_ground(g), root, hip)

# ---------------- ARMOR (one object): bone-and-bark plates with glowing lime rune cracks
arm_g = Geo()
def plate_paint(p, n, fc, fi):
    t = 0.5 + 0.5 * n.dot(Vector((-0.5, 0, 0.85)).normalized())
    c = mix(C_BONE0, C_BONE1, smoothstep(0.2, 0.9, t))
    return shade(c, 0.9 + 0.2 * hash01(fc.x * 2.3, fc.y * 1.7, fc.z))
# chest plate: big shell on the front, tilted with the torso
CHR = (0.33, 0.37, 0.41)
chest = shell(180, 130, -40, 58, CHR, thick=0.065, nu=6, nv=5, center=(0, 0, 0), point=3.0, bulge=0.05, jit=0.006, seed=3)
CHM = T(0.0, 0, 0.92) @ rot(0, -22, 0)
chest.transform(CHM)
def chest_paint(p, n, fc, fi):
    c = plate_paint(p, n, fc, fi)
    # dark bark trim on the plate's thick edges (faces not facing outward from the torso)
    radial = (p - Vector((0.0, 0, 0.92))).normalized()
    if abs(n.dot(radial)) < 0.55:
        c = shade(C_BARK, 0.9 + 0.2 * hash01(fc.x, fc.z))
    return c
chest.paint(chest_paint)
arm_g.extend(chest)
# glowing rune cracks lying ON the chest plate surface
def on_chest(az, el, lift=1.0):
    a, e = math.radians(az), math.radians(el)
    k = 1.0 + 0.05 * 0.5 + 0.02
    v = Vector((math.cos(e) * math.cos(a) * CHR[0] * k, math.cos(e) * math.sin(a) * CHR[1] * k, math.sin(e) * CHR[2] * k)) * lift
    return CHM @ v
for k, (el0, az0) in enumerate(((-15, 160), (8, 150), (28, 165))):
    pts = [on_chest(az0 + 12 * j + 6 * math.sin(j * 2.3 + k), el0 + 5 * math.sin(j * 1.7 + k), 1.035) for j in range(5)]
    arm_g.extend(tube(pts, 0.012, sides=4, smooth=False).fill(C_RUNE))
# shoulder pads: layered, spiked
for side in (1, -1):
    for layer, (rr, e0, e1, sp) in enumerate(((0.18, -10, 75, 170), (0.195, 20, 85, 120))):
        pad = shell(90 * side, sp, e0, e1, (rr, rr, rr * 0.9), thick=0.045, nu=5, nv=3,
                    center=(-0.04, 0.31 * side, 1.08 - layer * 0.02), point=0.0, bulge=0.05, jit=0.006, seed=10 + layer)
        pad.paint(plate_paint)
        arm_g.extend(pad)
    base = Vector((-0.04, 0.38 * side, 1.24))
    arm_g.extend(cone(base, base + Vector((0.02, 0.05 * side, 0.15)), 0.045, sides=5).fill(C_BONE1))
# brow guard: a heavy visor over the head, fixed to the upper chest
brow = shell(180, 110, 5, 55, (0.2, 0.22, 0.18), thick=0.045, nu=4, nv=3, center=hc + Vector((0.03, 0, 0.02)),
             point=0.3, bulge=0.05, jit=0.005, seed=21)
brow.paint(plate_paint)
arm_g.extend(brow)
for side in (1, -1):
    b = hc + Vector((-0.02, 0.15 * side, 0.12))
    pts = bezier(b, b + Vector((0.05, 0.08 * side, 0.08)), b + Vector((0.14, 0.1 * side, 0.12)), n=4)
    arm_g.extend(tube(pts, taper(len(pts), 0.035, 0.008), sides=5, cap0=True).fill(C_BONE1))
# bracers on the forearms
for side in (1, -1):
    el = Vector((-0.1, 0.37 * side, 0.7)); wr = Vector((-0.17, 0.34 * side, 0.36))
    br = tube([el + (wr - el) * 0.25, el + (wr - el) * 0.8], [0.13, 0.135], sides=8, smooth=False).fill(C_BARK)
    arm_g.extend(br.paint(lambda p, n, fc, fi: shade(C_BARK, 0.85 + 0.3 * hash01(fc.x, fc.z))))
part("Armor", arm_g, root, HIP, parent=B)

finish(root, "Enemies", icon=True)
