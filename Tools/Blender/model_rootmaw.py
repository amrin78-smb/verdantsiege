"""Boss: Root Maw. A huge blighted tree-beast that emerges at the right edge of the battlefield, facing -X.
~3 m tall, <= 3 m along X, <= 5 m along Y. Parts: Body, Jaw (lower jaw, pivot at the hinge), RootL/RootR (grasping
root arms, pivot at the shoulders; L = -Y), Eye (magenta weak point), Core (throat core, visible when the jaw opens)."""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_BARK0, C_BARK1 = hexc("#2E1E22"), hexc("#8A6A5E")
C_BARKHI = hexc("#A88A74")
C_FUNGUS0, C_FUNGUS1 = hexc("#5A7A2A"), hexc("#C4F04A")
C_VEIN = hexc("#FF4FD2", 1.0)
C_TOOTH0, C_TOOTH1 = hexc("#8A7A60"), hexc("#EADFC4")
C_MAW = hexc("#1A060E")

root = new_scene("RootMaw")
rnd = random.Random(13)

def bark(p, n, fc, fi):
    c = mix(C_BARK0, C_BARK1, smoothstep(0.0, 2.6, p.z) * 0.7 + 0.3 * (0.5 + 0.5 * vnoise(p, 2.5)))
    c = shade(c, 0.9 + 0.2 * vnoise(p, 7, 3))
    c = mix(c, C_BARKHI, smoothstep(0.55, 0.8, n.z) * 0.35)
    moss = smoothstep(0.72, 0.95, n.z) * smoothstep(-0.1, 0.4, vnoise(p, 3.0, 7.0))
    return mix(c, mix(hexc("#4E6A2A"), hexc("#9AC24A"), 0.5 + 0.5 * vnoise(p, 9)), moss * 0.85)

# ---------------- body: hunched trunk mass (lathe along a bent spine) + brow + crown + ground roots
spine = bezier((0.35, 0, 0.0), (0.4, 0, 1.0), (0.05, 0, 1.9), (-0.45, 0, 2.3), n=8)
radii = taper(len(spine), 1.0, 0.55, 0.8)
body = tube(spine, radii, sides=18, cap0=False, cap1=True, round1=True, flat=(0.85, 1.0))
# gnarl: displace by low-frequency noise
body.displace(lambda p: Vector((noise.noise(p * 1.4) * 0.12, noise.noise(p * 1.4 + Vector((5, 1, 2))) * 0.14, 0.0)))
# carve the maw recess at the front: push vertices in front-centre between z 0.9..1.7 back
def maw_carve(p):
    if p.x < -0.2 and 0.85 < p.z < 1.75 and abs(p.y) < 0.6:
        k = math.sin(math.pi * (p.z - 0.85) / 0.9) * (1 - (abs(p.y) / 0.6) ** 2)
        return Vector((0.38 * k, 0, 0))
    return Vector((0, 0, 0))
body.displace(maw_carve)
paint_vertex_normals(body, lambda p, n, fc, fi: C_MAW if (p.x < -0.15 and 0.95 < p.z < 1.65 and abs(p.y) < 0.45 and n.x > -0.5) else bark(p, n, fc, fi))
brow = Geo()
for side in (1, -1):
    pts = bezier((-0.15, 0.55 * side, 1.95), (-0.65, 0.35 * side, 2.05), (-0.85, 0.05 * side, 1.85), n=6)
    brow.extend(tube(pts, taper(len(pts), 0.22, 0.12), sides=10, round1=True))
paint_vertex_normals(brow, bark)
upper_teeth = Geo()
for k in range(7):
    y = -0.42 + 0.14 * k
    x = -0.7 + 0.18 * (abs(y) / 0.42) ** 2
    b = Vector((x, y, 1.72))
    upper_teeth.extend(cone(b, b + Vector((-0.04, 0, -0.26 + 0.08 * abs(y))), 0.07, sides=6, smooth=True))
paint_vertex_normals(upper_teeth, lambda p, n, fc, fi: mix(C_TOOTH0, C_TOOTH1, smoothstep(1.45, 1.7, p.z)))
crown = Geo()
for k, (ang, ln) in enumerate(((-60, 0.9), (60, 0.95), (-20, 0.7), (25, 0.75), (180, 0.6), (-130, 0.65), (130, 0.6))):
    a = math.radians(ang)
    d = Vector((math.cos(a) * 0.5, math.sin(a), 0)).normalized()
    p0 = Vector((-0.1, 0, 2.3)) + d * 0.25
    pts = bezier(p0, p0 + d * ln * 0.4 + Vector((0, 0, ln * 0.6)), p0 + d * ln + Vector((0.15, 0, ln * 0.55)), n=5)
    pts = [Vector((p.x, max(-2.4, min(2.4, p.y)), min(p.z, 3.3))) for p in pts]
    crown.extend(tube(pts, taper(len(pts), 0.12, 0.025), sides=7, round1=True))
paint_vertex_normals(crown, bark)
for k, (ang, ln) in enumerate(((-60, 0.9), (60, 0.95), (-20, 0.7), (25, 0.75), (180, 0.6), (-130, 0.65), (130, 0.6))):
    a = math.radians(ang)
    d = Vector((math.cos(a) * 0.5, math.sin(a), 0)).normalized()
    p0 = Vector((-0.1, 0, 2.3)) + d * 0.25
    mid = p0 + d * ln * 0.55 + Vector((0.05, 0, ln * 0.55))
    crown.extend(sphere((mid.x, max(-2.3, min(2.3, mid.y)), min(mid.z, 3.2)), 0.06, sides=8, rings=5).fill(hexc("#D4FF5A", 1.0)))
fungus = Geo()
for k in range(9):
    a = rnd.uniform(-2.4, 2.4) + math.pi * 0.0
    z = rnd.uniform(0.5, 2.2)
    sp = spine[min(len(spine) - 1, int(z / 2.6 * (len(spine) - 1)))]
    r = 0.9 - 0.3 * (z / 2.4)
    c = Vector((sp.x + math.cos(a) * r * 0.85 + 0.15, math.sin(a) * r, z))
    if c.x < -0.2:
        continue
    br = ellipsoid((0, 0, 0), (0.16, 0.22, 0.05), sides=10, rings=5, jitter=lambda pi, si, aa, k=k: 1 + 0.12 * math.sin(aa * 4 + k))
    br.transform(T(*c) @ rot(0, 0, math.degrees(a)) @ rot(0, -10, 0))
    fungus.extend(br)
paint_vertex_normals(fungus, lambda p, n, fc, fi: mix(C_FUNGUS0, glow(C_FUNGUS1, 0.35), smoothstep(-0.2, 0.7, n.z)))
veins = Geo()
for k in range(8):
    a = math.radians(-160 + 40 * k)
    pts = []
    for j in range(6):
        z = 0.15 + j * 0.32
        sp = spine[min(len(spine) - 1, int(z / 2.6 * (len(spine) - 1)))]
        r = (1.0 - 0.45 * z / 2.4) * 0.92 + 0.03
        aa = a + 0.15 * math.sin(j * 1.3 + k)
        pts.append(Vector((sp.x + math.cos(aa) * r * 0.85, math.sin(aa) * r, z)))
    veins.extend(tube(pts, taper(len(pts), 0.03, 0.012), sides=4, smooth=False))
veins.fill(C_VEIN)
groots = Geo()
for k, (ang, ln) in enumerate(((90, 1.6), (-90, 1.65), (50, 1.1), (-50, 1.1), (140, 1.0), (-140, 1.0), (0, 0.9), (180, 0.6))):
    a = math.radians(ang)
    d = Vector((math.cos(a), math.sin(a), 0))
    p0 = Vector((0.35, 0, 0)) + d * 0.7 + Vector((0, 0, 0.35))
    pts = bezier(p0, p0 + d * ln * 0.4 + Vector((0, 0, 0.15)), p0 + d * ln + Vector((0, 0, -0.08)), n=5)
    pts = [Vector((max(-1.3, min(1.25, p.x)), max(-2.3, min(2.3, p.y)), p.z)) for p in pts]
    groots.extend(tube(pts, taper(len(pts), 0.26, 0.06), sides=9, round1=True))
paint_vertex_normals(groots, bark)
B = part("Body", clamp_ground(merge(body, brow, upper_teeth, crown, fungus, veins, groots)), root, (0.35, 0, 0))

# ---------------- jaw: heavy lower mandible hinged at the back of the maw
hinge = Vector((-0.05, 0, 1.0))
jaw = lathe([(0.0, -0.75), (0.25, -0.7), (0.42, -0.45), (0.5, -0.1), (0.48, 0.15), (0.0, 0.2)], sides=14, round_profile=1)
jaw.transform(rot(0, -90, 0))                 # lathe axis -> -X
jaw.transform(S(1, 1.05, 0.38))
jaw.displace(lambda p: Vector((0, 0, 0.12 * (p.x / 0.75) ** 2)))   # chin curls up
paint_vertex_normals(jaw, lambda p, n, fc, fi: C_MAW if n.z > 0.6 and abs(p.y) < 0.38 and p.x < -0.1 else bark(p + Vector((0, 0, 1.0)), n, fc, fi))
lower_teeth = Geo()
for k in range(6):
    y = -0.36 + 0.144 * k
    x = -0.62 + 0.2 * (abs(y) / 0.4) ** 2
    b = Vector((x, y, 0.1 + 0.08 * ((x / 0.75) ** 2)))
    lower_teeth.extend(cone(b, b + Vector((-0.02, 0, 0.24 - 0.06 * abs(y))), 0.06, sides=6, smooth=True))
paint_vertex_normals(lower_teeth, lambda p, n, fc, fi: mix(C_TOOTH0, C_TOOTH1, smoothstep(0.1, 0.3, p.z)))
jg = merge(jaw, lower_teeth)
jg.transform(T(*hinge) @ rot(0, 12, 0))       # slightly open at rest
part("Jaw", jg, root, hinge, parent=B)

# ---------------- core: throat weak point (visible when the jaw opens)
cc = Vector((-0.35, 0, 1.3))
core = ellipsoid(cc, (0.2, 0.22, 0.22), sides=14, rings=9, jitter=lambda pi, si, a: 1 + 0.05 * math.sin(a * 5))
core.paint(lambda p, n, fc, fi: mix(hexc("#C4FF4D", 0.8), hexc("#FFF8C0", 1.0), smoothstep(-0.3, 0.8, -n.x + n.z * 0.3)))
ribs = Geo()
for k in range(4):
    a = math.radians(-60 + 40 * k)
    rg = shell(180, 30, -70, 70, (0.25, 0.27, 0.27), thick=0.03, nu=1, nv=5, center=cc, point=0.0)
    rg.transform(T(*cc) @ rot(0, 0, (k - 1.5) * 28) @ T(*(-cc)))
    ribs.extend(rg)
ribs.fill(hexc("#3A1A2A"))
part("Core", merge(core, ribs), root, cc, parent=B)

# ---------------- eye: single great magenta eye above the maw
ec = Vector((-0.8, 0, 2.12))
eye = ellipsoid(ec, (0.15, 0.27, 0.22), sides=16, rings=10)
eye.paint(lambda p, n, fc, fi: mix(hexc("#8A1060", 0.6), hexc("#FF4FD2", 1.0), smoothstep(-0.2, 0.9, -n.x)))
pupil = ellipsoid(ec + Vector((-0.14, 0, 0)), (0.02, 0.045, 0.16), sides=8, rings=5).fill(hexc("#14040C"))
lids = shell(180, 120, 15, 62, (0.18, 0.31, 0.26), thick=0.045, nu=5, nv=3, center=ec, point=0.0)
paint_vertex_normals(lids, bark)
part("Eye", merge(eye, pupil, lids), root, ec, parent=B)

# ---------------- grasping root arms
for side, nm in ((-1, "RootL"), (1, "RootR")):
    sh = Vector((0.1, 0.78 * side, 1.75))
    pts = bezier(sh, sh + Vector((-0.4, 0.75 * side, 0.45)), Vector((-0.85, 1.55 * side, 0.7)), Vector((-1.0, 1.45 * side, 0.12)), n=9)
    arm = tube(pts, taper(len(pts), 0.3, 0.13, 0.8), sides=11, cap0=True, cap1=True)
    arm.displace(lambda p: Vector((0, 0, noise.noise(p * 2.2) * 0.04)))
    hand = pts[-1]
    fingers = Geo()
    for k in range(4):
        a = math.radians(-50 + 33 * k)
        d = Vector((-math.cos(a), math.sin(a) * side * 0.9, 0)).normalized()
        f = bezier(hand, hand + d * 0.2 + Vector((0, 0, 0.06)), hand + d * 0.36 + Vector((0, 0, -0.1)), n=4)
        fingers.extend(tube(f, taper(len(f), 0.08, 0.02), sides=7, round1=True))
    thorns = Geo()
    for k in range(5):
        j = 2 + k * (len(pts) - 4) // 5
        p = pts[j]
        thorns.extend(cone(p + Vector((0, 0, 0.18 - 0.02 * k)), p + Vector((0.05, 0.05 * side, 0.36 - 0.03 * k)), 0.05, sides=5))
    g = merge(arm, fingers, thorns)
    paint_vertex_normals(g, bark)
    vein = tube([p + Vector((0, 0, 0.16 - 0.015 * i)) for i, p in enumerate(pts[1:-1])], 0.022, sides=4, smooth=False).fill(C_VEIN)
    g.extend(vein)
    part(nm, clamp_ground(g), root, sh, parent=B)

finish(root, "Bosses", icon=True, view=(150, 20))
