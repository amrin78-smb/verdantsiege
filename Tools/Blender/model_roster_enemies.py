"""Milestone 3 enemies (the Blight). Face -X (their LEFT is -Y). Origin at ground centre.
Usage: blender -b --factory-startup --python Tools/Blender/model_roster_enemies.py -- [Name ...]"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

LIME = hexc("#C4FF4D", 1.0)
MAGENTA = hexc("#FF4FD2", 1.0)
BONE0, BONE1 = hexc("#8A7558"), hexc("#EADFC4")
CLAW = hexc("#E4D3B8")

MODELS = {}
def model(fn):
    MODELS[fn.__name__] = fn
    return fn


def skin(c0, c1, z0, z1, belly=None, var=0.06):
    def fn(p, n, fc, fi):
        c = mix(c0, c1, smoothstep(z0, z1, p.z))
        if belly is not None and n.x < -0.35:
            c = mix(c, belly, smoothstep(-0.35, -0.85, n.x) * 0.75)
        return shade(c, 1 + var * vnoise(p, 7))
    return fn


def limb(pts, r0, r1, sides=8):
    pts = [Vector(p) for p in pts]
    if len(pts) == 3:
        pts = bezier(pts[0], pts[1], pts[2], n=4)
    return tube(pts, taper(len(pts), r0, r1), sides=sides, cap0=True, cap1=True)


def claws(at, direction, n=3, length=0.07, r=0.014, spread=0.03, side_axis=Vector((0, 1, 0))):
    g = Geo()
    d = Vector(direction).normalized()
    for k in range(n):
        b = Vector(at) + side_axis * (k - (n - 1) / 2) * spread
        g.extend(cone(b, b + d * length, r, sides=4))
    return g.fill(CLAW)


def glow_eyes(center, sep, r, col=LIME, n=2, forward=Vector((-1, 0, 0))):
    g = Geo()
    for k in range(n):
        y = (k - (n - 1) / 2) * sep
        g.extend(ellipsoid(Vector(center) + Vector((0, y, 0)), (r * 0.7, r, r * 0.8), sides=8, rings=5).fill(col))
    return g


# ------------------------------------------------------------------ Vaultling
@model
def Vaultling():
    root = new_scene("Vaultling")
    C0, C1, BELLY = hexc("#2A1E3E"), hexc("#7A5AA8"), hexc("#86A84A")
    sk = skin(C0, C1, 0.3, 0.85, BELLY)
    HIP = Vector((0.06, 0, 0.5))
    body = ellipsoid((0, 0, 0), (0.17, 0.15, 0.24), sides=12, rings=8)
    body.transform(T(-0.02, 0, 0.6) @ rot(0, -35, 0))
    body.vpaint(sk)
    ridge = Geo()
    for k in range(4):
        b = Vector((0.1 - k * 0.06, 0, 0.72 + k * 0.05))
        ridge.extend(cone(b, b + Vector((0.07, 0, 0.06)), 0.025, sides=5))
    ridge.fill(hexc("#D8FF6A", 0.5))
    B = part("Body", merge(body, ridge), root, HIP)
    neck = Vector((-0.14, 0, 0.8))
    hc = neck + Vector((-0.06, 0, 0.04))
    head = ellipsoid(hc, (0.11, 0.1, 0.085), sides=12, rings=7).vpaint(sk)
    horns = Geo()
    for side in (1, -1):
        pts = bezier(hc + Vector((0.02, 0.05 * side, 0.06)), hc + Vector((0.1, 0.1 * side, 0.14)), hc + Vector((0.2, 0.12 * side, 0.12)), n=4)
        horns.extend(tube(pts, taper(len(pts), 0.022, 0.004), sides=5, cap0=True).fill(BONE1))
    eyes = glow_eyes(hc + Vector((-0.085, 0, 0.02)), 0.08, 0.026)
    mouth = ellipsoid(hc + Vector((-0.09, 0, -0.04)), (0.02, 0.05, 0.012), sides=8, rings=4).fill(hexc("#140A12"))
    part("Head", merge(head, horns, eyes, mouth), root, neck, parent=B)
    for side, nm in ((-1, "LegL"), (1, "LegR")):
        hip = HIP + Vector((0, 0.13 * side, 0))
        knee = Vector((0.27, 0.19 * side, 0.74))
        ank = Vector((0.2, 0.2 * side, 0.08))
        thigh = limb([hip, hip + Vector((0.12, 0.04 * side, 0.18)), knee], 0.085, 0.05)
        shin = limb([knee, knee + Vector((0.02, 0, -0.3)), ank], 0.045, 0.03)
        foot = ellipsoid((ank.x - 0.1, ank.y, 0.03), (0.14, 0.05, 0.03), sides=8, rings=5)
        coil = Geo()
        for j in range(3):
            coil.extend(lathe([(0.0, -0.008), (0.06, -0.008), (0.065, 0.0), (0.06, 0.008), (0.0, 0.008)], sides=10)
                        .transform(T(*(knee + (ank - knee) * (0.2 + 0.18 * j))) @ align(ank - knee)))
        coil.fill(LIME)
        g = merge(thigh, shin, foot)
        g.vpaint(sk)
        g.extend(coil); g.extend(claws((ank.x - 0.22, ank.y, 0.03), (-1, 0, -0.3), 3, 0.05, 0.012, 0.03))
        part(nm, clamp_ground(g), root, hip)
    for side, nm in ((-1, "ArmL"), (1, "ArmR")):
        sh = Vector((-0.12, 0.13 * side, 0.68))
        hand = Vector((-0.22, 0.15 * side, 0.36))
        g = limb([sh, sh + Vector((-0.08, 0.03 * side, -0.12)), hand], 0.04, 0.028).vpaint(sk)
        g.extend(claws(hand, (-0.4, 0, -1), 3, 0.05, 0.01, 0.025))
        part(nm, g, root, sh, parent=B)
    finish(root, "Enemies", icon=True)


# ------------------------------------------------------------------ Spore Carrier
@model
def SporeCarrier():
    root = new_scene("SporeCarrier")
    C0, C1 = hexc("#2C1A2E"), hexc("#6A4064")
    sk = skin(C0, C1, 0.15, 0.7, hexc("#9A7090"))
    HIP = Vector((0.0, 0, 0.32))
    body = ellipsoid((0.0, 0, 0.45), (0.24, 0.24, 0.2), sides=14, rings=8).vpaint(sk)
    straps = Geo()
    for side in (1, -1):
        pts = bezier((-0.15, 0.12 * side, 0.55), (-0.05, 0.25 * side, 0.85), (0.15, 0.2 * side, 0.95), n=5)
        straps.extend(tube(pts, 0.03, sides=6).vpaint(sk))
    B = part("Body", merge(body, straps), root, HIP)
    sc = Vector((0.12, 0, 0.88))
    sac = ellipsoid(sc, (0.28, 0.3, 0.3), sides=16, rings=10, jitter=lambda pi, si, a: 1 + 0.05 * math.sin(a * 5 + pi))
    def sac_paint(p, n, fc, fi):
        t = 0.5 + 0.5 * vnoise(p, 6.0, 2.0)
        c = mix(hexc("#6FB82A", 0.45), hexc("#E6FF7A", 0.9), smoothstep(0.35, 0.75, t))
        return c
    sac.paint(sac_paint)
    veins = Geo()
    for k in range(6):
        a = 2 * math.pi * k / 6
        pts = [sc + Vector((math.cos(a) * 0.29 * math.cos(e), math.sin(a) * 0.31 * math.cos(e), 0.31 * math.sin(e)))
               for e in [math.radians(x) for x in (-50, -20, 10, 40, 70)]]
        veins.extend(tube(pts, 0.012, sides=4, smooth=False).fill(hexc("#3A2040")))
    nubs = Geo()
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.5
        nubs.extend(sphere(sc + Vector((math.cos(a) * 0.2, math.sin(a) * 0.22, 0.22)), 0.03, sides=6, rings=4).fill(hexc("#F4FFB0", 1.0)))
    part("Sac", merge(sac, veins, nubs), root, sc, parent=B)
    neck = Vector((-0.2, 0, 0.5))
    hc = neck + Vector((-0.07, 0, 0.0))
    head = ellipsoid(hc, (0.1, 0.11, 0.09), sides=12, rings=7).vpaint(sk)
    eyes = glow_eyes(hc + Vector((-0.08, 0, 0.03)), 0.07, 0.02, col=hexc("#FFA22E", 1.0))
    drool = Geo()
    for k in range(3):
        b = hc + Vector((-0.08, (k - 1) * 0.035, -0.06))
        drool.extend(tube([b, b + Vector((0, 0, -0.05)), b + Vector((0.0, 0, -0.08))], [0.012, 0.009, 0.006], sides=4).fill(hexc("#C4FF4D", 0.7)))
    part("Head", merge(head, eyes, drool), root, neck, parent=B)
    for side, nm in ((-1, "LegL"), (1, "LegR")):
        hip = Vector((0.0, 0.14 * side, 0.36))
        g = limb([hip, hip + Vector((-0.04, 0.03 * side, -0.16)), Vector((-0.02, 0.17 * side, 0.06))], 0.08, 0.06).vpaint(sk)
        foot = ellipsoid((-0.06, 0.17 * side, 0.04), (0.11, 0.08, 0.045), sides=8, rings=5).vpaint(sk)
        g.extend(foot); g.extend(claws((-0.16, 0.17 * side, 0.03), (-1, 0, -0.3), 3, 0.04, 0.012, 0.035))
        part(nm, clamp_ground(g), root, hip)
    finish(root, "Enemies", icon=True)


# ------------------------------------------------------------------ Burrower
@model
def Burrower():
    root = new_scene("Burrower")
    C0, C1 = hexc("#3A2230"), hexc("#8A5A6A")
    sk = skin(C0, C1, 0.0, 0.5, hexc("#B48A8A"))
    # body: segmented arch emerging from a dirt mound at the back, rising to the head at the front
    allp = bezier((0.24, 0, -0.05), (0.2, 0, 0.42), (-0.06, 0, 0.52), (-0.12, 0, 0.4), n=6)
    pts = [allp[int(round(i * (len(allp) - 1) / 6))] for i in range(7)]
    segs = Geo()
    for k, p in enumerate(pts[:-1]):
        r = 0.15 - 0.02 * abs(k - 3) / 3
        segs.extend(ellipsoid(p, (r * 0.95, r, r), sides=9, rings=6))
    segs.vpaint(sk)
    rings_ = Geo()
    for k in range(1, len(pts) - 2):
        d = (pts[k + 1] - pts[k - 1]).normalized()
        rg = lathe([(0.0, -0.012), (0.15, -0.012), (0.158, 0.0), (0.15, 0.012), (0.0, 0.012)], sides=10)
        rg.transform(T(*((pts[k] + pts[k + 1]) / 2)) @ align(d))
        rings_.extend(rg.fill(hexc("#C24A9A", 0.4)))
    mound = lathe([(0.0, 0.0), (0.24, 0.0), (0.18, 0.07), (0.09, 0.12), (0.0, 0.13)], sides=12, round_profile=1,
                  jitter=lambda pi, si, a: 1 + 0.1 * math.sin(a * 4))
    mound.transform(T(0.22, 0, 0))
    mound.paint(lambda p, n, fc, fi: shade(mix(hexc("#5A4028"), hexc("#8A6A44"), smoothstep(0, 0.12, p.z)), 1 + 0.1 * vnoise(p, 9)))
    clods = Geo()
    for k in range(5):
        a = 2 * math.pi * k / 5
        clods.extend(pebble((0.22 + math.cos(a) * 0.2, math.sin(a) * 0.26, 0.03), (0.05, 0.045, 0.035), seed=k).fill(hexc("#6A4A30")))
    B = part("Body", clamp_ground(merge(segs, rings_, mound, clods)), root, (0.22, 0, 0))
    neck = pts[-2]
    hc = pts[-1]
    head = ellipsoid(hc, (0.13, 0.13, 0.12), sides=10, rings=7).vpaint(sk)
    drill = lathe([(0.1, 0.0), (0.08, 0.07), (0.055, 0.13), (0.028, 0.18), (0.0, 0.22)], sides=10,
                  jitter=lambda pi, si, a: 1 + 0.12 * math.sin(a * 3 - pi * 1.4), smooth=True)
    drill.transform(T(*(hc + Vector((-0.08, 0, -0.02)))) @ align((-1, 0, -0.15)))
    drill.paint(lambda p, n, fc, fi: mix(BONE0, BONE1, 0.5 + 0.5 * math.sin(math.atan2(p.z - hc.z, p.y) * 3 + p.x * 40)))
    eyes = glow_eyes(hc + Vector((-0.06, 0, 0.09)), 0.12, 0.018, col=hexc("#FF5A3C", 1.0))
    part("Head", merge(head, drill, eyes), root, neck, parent=B)
    for side, nm in ((-1, "ArmL"), (1, "ArmR")):
        sh = hc + Vector((0.08, 0.1 * side, -0.06))
        hand = Vector((hc.x - 0.12, 0.2 * side, 0.08))
        g = limb([sh, sh + Vector((-0.04, 0.08 * side, -0.12)), hand], 0.045, 0.035).vpaint(sk)
        g.extend(ellipsoid(hand, (0.06, 0.05, 0.03), sides=8, rings=5).vpaint(sk))
        g.extend(claws(hand + Vector((-0.05, 0, -0.01)), (-1, 0, -0.4), 3, 0.07, 0.014, 0.03))
        part(nm, clamp_ground(g), root, sh, parent=B)
    finish(root, "Enemies", icon=True)


# ------------------------------------------------------------------ Howler
@model
def Howler():
    root = new_scene("Howler")
    C0, C1 = hexc("#1E1828"), hexc("#5A4A78")
    sk = skin(C0, C1, 0.3, 1.2, hexc("#8A7AA0"))
    HIP = Vector((0.04, 0, 0.62))
    torso = ellipsoid((0, 0, 0), (0.12, 0.16, 0.26), sides=12, rings=8)
    torso.transform(T(0.0, 0, 0.9) @ rot(0, -15, 0))
    torso.vpaint(sk)
    ribs = Geo()
    for k in range(3):
        z = 0.8 + k * 0.08
        rg = shell(180, 140, -8, 8, (0.125, 0.165, 0.05), thick=0.012, nu=5, nv=1, center=(0.0 - (z - 0.9) * 0.27, 0, z), point=0.0)
        ribs.extend(rg)
    ribs.fill(BONE1)
    spines = Geo()
    for k in range(5):
        b = Vector((0.1 - 0.02 * k, 0, 0.75 + 0.09 * k))
        spines.extend(cone(b, b + Vector((0.12, 0, 0.04)), 0.02, sides=4))
    spines.fill(BONE1)
    B = part("Body", merge(torso, ribs, spines), root, HIP)
    neck = Vector((-0.1, 0, 1.15))
    hc = neck + Vector((-0.08, 0, 0.06))
    skull = ellipsoid(hc, (0.12, 0.09, 0.08), sides=12, rings=7).vpaint(sk)
    jaw = ellipsoid(hc + Vector((-0.1, 0, -0.05)), (0.1, 0.065, 0.035), sides=10, rings=5).vpaint(sk)
    jaw.transform(Matrix.Translation((0, 0, 0)))
    eyes = glow_eyes(hc + Vector((-0.06, 0, 0.04)), 0.09, 0.016, col=MAGENTA)
    ears = Geo()
    for side in (1, -1):
        e = leaf(length=0.16, width=0.07, segs=4, across=1, curl=0.3, fold=0.3, thick=0.012)
        e.transform(T(*(hc + Vector((0.04, 0.06 * side, 0.05)))) @ rot(0, 0, 160 * side) @ rot(0, -35, 0))
        ears.extend(e.vpaint(sk))
    part("Head", merge(skull, jaw, eyes, ears), root, neck, parent=B)
    sc = hc + Vector((-0.05, 0, -0.13))
    sac = ellipsoid(sc, (0.1, 0.11, 0.1), sides=14, rings=9, jitter=lambda pi, si, a: 1 + 0.04 * math.sin(a * 4))
    sac.paint(lambda p, n, fc, fi: mix(hexc("#8A1E6A", 0.25), hexc("#FF6AD8", 0.6), smoothstep(-0.6, 0.6, -n.x + n.z * 0.3)))
    part("Sac", sac, root, sc, parent=B)
    for side, nm in ((-1, "ArmL"), (1, "ArmR")):
        sh = Vector((-0.03, 0.17 * side, 1.08))
        el = Vector((-0.12, 0.25 * side, 0.75))
        hand = Vector((-0.2, 0.24 * side, 0.42))
        g = merge(limb([sh, (sh + el) / 2 + Vector((0.02, 0, 0)), el], 0.04, 0.03), limb([el, (el + hand) / 2, hand], 0.03, 0.025)).vpaint(sk)
        g.extend(claws(hand, (-0.3, 0, -1), 3, 0.09, 0.012, 0.025))
        part(nm, g, root, sh, parent=B)
    for side, nm in ((-1, "LegL"), (1, "LegR")):
        hip = HIP + Vector((0, 0.09 * side, 0))
        knee = Vector((-0.06, 0.11 * side, 0.33)); ank = Vector((0.06, 0.12 * side, 0.08))
        g = merge(limb([hip, (hip + knee) / 2, knee], 0.05, 0.035), limb([knee, (knee + ank) / 2, ank], 0.035, 0.025)).vpaint(sk)
        g.extend(ellipsoid((ank.x - 0.06, ank.y, 0.03), (0.1, 0.045, 0.03), sides=8, rings=5).vpaint(sk))
        g.extend(claws((ank.x - 0.15, ank.y, 0.02), (-1, 0, -0.2), 3, 0.04, 0.01, 0.025))
        part(nm, clamp_ground(g), root, hip)
    finish(root, "Enemies", icon=True)


# ------------------------------------------------------------------ Sapper
@model
def Sapper():
    root = new_scene("Sapper")
    C0, C1 = hexc("#2A1C22"), hexc("#6E4A50")
    sk = skin(C0, C1, 0.2, 0.9, hexc("#9A7070"))
    HIP = Vector((0.05, 0, 0.42))
    torso = ellipsoid((0, 0, 0), (0.2, 0.22, 0.26), sides=12, rings=8)
    torso.transform(T(-0.02, 0, 0.66) @ rot(0, -40, 0))
    torso.vpaint(sk)
    pelvis = ellipsoid((HIP.x, 0, HIP.z + 0.02), (0.15, 0.18, 0.1), sides=10, rings=6).vpaint(sk)
    harness = Geo()
    for side in (1, -1):
        pts = bezier((-0.12, 0.16 * side, 0.8), (0.0, 0.22 * side, 0.6), (0.2, 0.16 * side, 0.55), n=5)
        harness.extend(tube(pts, 0.018, sides=5).fill(hexc("#4A3424")))
    B = part("Body", merge(torso, pelvis, harness), root, HIP)
    neck = Vector((-0.2, 0, 0.78))
    hc = neck + Vector((-0.06, 0, -0.02))
    head = ellipsoid(hc, (0.11, 0.11, 0.09), sides=12, rings=7).vpaint(sk)
    mask = shell(180, 120, -30, 30, (0.115, 0.115, 0.095), thick=0.015, nu=5, nv=3, center=hc, point=0.0)
    mask.fill(hexc("#8A8070"))
    lenses = glow_eyes(hc + Vector((-0.11, 0, 0.02)), 0.08, 0.025, col=LIME)
    part("Head", merge(head, mask, lenses), root, neck, parent=B)
    cc = Vector((0.2, 0, 0.82))
    can = lathe([(0.0, -0.24), (0.11, -0.24), (0.13, -0.2), (0.13, 0.18), (0.11, 0.23), (0.05, 0.25), (0.0, 0.26)], sides=14)
    can.transform(T(*cc) @ rot(0, -25, 0))
    can.paint(lambda p, n, fc, fi: mix(hexc("#5A5048"), hexc("#A09080"), smoothstep(0.3, 1.0, n.z * 0.5 + 0.5)))
    window = lathe([(0.0, -0.13), (0.133, -0.13), (0.133, 0.1), (0.0, 0.1)], sides=14, cap_bottom=False, cap_top=False)
    window.f = [f for i, f in enumerate(window.f) if True]
    win = Geo(); win.v = window.v
    for fi_, f in enumerate(window.f):
        c = window.face_center(fi_)
        if c.y < -0.04:          # only the -Y facing band (camera side)
            win.add_face(f, True)
    win.transform(T(*cc) @ rot(0, -25, 0) @ S(1.02, 1.02, 1))
    win.fill(hexc("#9CFF3A", 1.0))
    bands = Geo()
    for z in (-0.18, 0.14):
        rg = lathe([(0.0, -0.02), (0.14, -0.02), (0.145, 0.0), (0.14, 0.02), (0.0, 0.02)], sides=14)
        rg.transform(T(*cc) @ rot(0, -25, 0) @ T(0, 0, z))
        bands.extend(rg.fill(BONE1))
    valve = tube([cc + Vector((-0.08, 0, 0.24)), cc + Vector((-0.2, 0, 0.3)), cc + Vector((-0.28, 0.12, 0.05))], 0.02, sides=6).fill(hexc("#3A3A30"))
    drip = sphere(cc + Vector((0.12, -0.06, -0.26)), 0.03, sides=6, rings=4).fill(LIME)
    part("Canister", merge(can, win, bands, valve, drip), root, cc, parent=B)
    for side, nm in ((-1, "ArmL"), (1, "ArmR")):
        sh = Vector((-0.12, 0.2 * side, 0.74))
        hand = Vector((-0.28, 0.22 * side, 0.3))
        g = limb([sh, sh + Vector((-0.1, 0.05 * side, -0.2)), hand], 0.055, 0.04).vpaint(sk)
        g.extend(ellipsoid(hand, (0.06, 0.05, 0.05), sides=8, rings=5).vpaint(sk))
        g.extend(claws(hand + Vector((-0.03, 0, -0.03)), (-0.5, 0, -1), 3, 0.07, 0.013, 0.03))
        part(nm, g, root, sh, parent=B)
    for side, nm in ((-1, "LegL"), (1, "LegR")):
        hip = HIP + Vector((0, 0.11 * side, 0))
        g = limb([hip, hip + Vector((-0.06, 0.02 * side, -0.2)), Vector((HIP.x, 0.13 * side, 0.06))], 0.07, 0.05).vpaint(sk)
        g.extend(ellipsoid((HIP.x - 0.06, 0.13 * side, 0.04), (0.11, 0.07, 0.045), sides=8, rings=5).vpaint(sk))
        g.extend(claws((HIP.x - 0.16, 0.13 * side, 0.03), (-1, 0, -0.3), 3, 0.04, 0.012, 0.03))
        part(nm, clamp_ground(g), root, hip)
    finish(root, "Enemies", icon=True)


# ------------------------------------------------------------------ Glider
@model
def Glider():
    root = new_scene("Glider")
    C0, C1 = hexc("#24162E"), hexc("#6A4A8A")
    sk = skin(C0, C1, 0.85, 1.15)
    BC = Vector((0.02, 0, 1.0))
    thorax = ellipsoid(BC, (0.12, 0.09, 0.09), sides=12, rings=7).vpaint(sk)
    abd = ellipsoid(BC + Vector((0.2, 0, -0.04)), (0.17, 0.08, 0.075), sides=12, rings=7,
                    jitter=lambda pi, si, a: 1 + 0.06 * math.sin(pi * 2.5)).paint(skin(C0, hexc("#B05AA0"), 0.9, 1.05))
    fur = Geo()
    for k in range(8):
        a = 2 * math.pi * k / 8
        b = BC + Vector((0.03, math.cos(a) * 0.08, math.sin(a) * 0.08))
        fur.extend(cone(b, b + Vector((0.04, math.cos(a) * 0.04, math.sin(a) * 0.04)), 0.02, sides=4))
    fur.fill(hexc("#C9B0D8"))
    legs = Geo()
    for k in range(3):
        for side in (1, -1):
            b = BC + Vector((-0.04 + 0.05 * k, 0.05 * side, -0.06))
            legs.extend(tube([b, b + Vector((-0.02, 0.06 * side, -0.12)), b + Vector((0.02, 0.07 * side, -0.25))], [0.012, 0.009, 0.005], sides=4).fill(C0))
    B = part("Body", merge(thorax, abd, fur, legs), root, BC)
    hc = BC + Vector((-0.13, 0, 0.02))
    head = ellipsoid(hc, (0.065, 0.07, 0.06), sides=10, rings=6).vpaint(sk)
    eyes = glow_eyes(hc + Vector((-0.04, 0, 0.0)), 0.08, 0.032, col=MAGENTA)
    ant = Geo()
    for side in (1, -1):
        pts = bezier(hc + Vector((-0.02, 0.03 * side, 0.05)), hc + Vector((-0.08, 0.08 * side, 0.18)), hc + Vector((0.02, 0.14 * side, 0.24)), n=5)
        ant.extend(tube(pts, 0.006, sides=3, smooth=False).fill(C1))
        for j in range(1, len(pts)):
            fd = leaf(length=0.035, width=0.02, segs=2, across=1, curl=0, fold=0.1, thick=0.004)
            fd.transform(basis(Vector((0.3, side, 0.2)), Vector((0, 0, 1)), pts[j]))
            ant.extend(fd.fill(hexc("#C9B0D8")))
    part("Head", merge(head, eyes, ant), root, hc, parent=B)
    for side, nm in ((-1, "WingL"), (1, "WingR")):
        shp = BC + Vector((0.0, 0.07 * side, 0.04))
        fore = leaf(length=0.4, width=0.3, segs=8, across=3, curl=0.05, fold=-0.04, thick=0.01, tip_power=0.6,
                    shape=lambda u: max(0.15, math.sin(math.pi * min(1, u) ** 0.6)) if u < 1 else 0.0)
        fore.transform(rot(0, 0, 0))
        fore.transform(basis(Vector((-0.35, side, 0.25)), Vector((0, 0, 1))))
        hind = leaf(length=0.3, width=0.26, segs=7, across=3, curl=0.05, fold=-0.04, thick=0.01, tip_power=0.7)
        hind.transform(basis(Vector((0.55, side, 0.0)), Vector((0, 0, 1))))
        w = merge(fore, hind)
        def wp(p, n, fc, fi):
            d = Vector((p.x, p.y, 0)).length
            c = mix(hexc("#3A2050"), hexc("#8A5AB0"), smoothstep(0.05, 0.3, d))
            spot = (Vector((p.x, p.y, 0)) - Vector((-0.1, 0.25 * (1 if p.y > 0 else -1), 0))).length
            c = mix(c, MAGENTA, smoothstep(0.07, 0.04, spot))
            return mix(c, hexc("#E0C8F0"), smoothstep(0.33, 0.4, d) * 0.8)
        w.paint(wp)
        w.transform(T(*shp))
        part(nm, w, root, shp, parent=B)
    finish(root, "Enemies", icon=True, view=(140, 22))


# ------------------------------------------------------------------ Warden
@model
def Warden():
    import vskit; vskit.RES = 1.45          # keeps the elite inside the 4500 budget
    root = new_scene("Warden")
    C0, C1 = hexc("#1E1626"), hexc("#4A3A5E")
    sk = skin(C0, C1, 0.3, 1.3)
    def plate(p, n, fc, fi):
        return mix(hexc("#4A3E5A"), hexc("#B8AAC8"), smoothstep(-0.2, 0.9, n.z * 0.6 + 0.4 - n.x * 0.3))
    HIP = Vector((0.03, 0, 0.62))
    torso = ellipsoid((0, 0, 0.97), (0.18, 0.22, 0.3), sides=12, rings=8).vpaint(sk)
    cuirass = shell(180, 200, -40, 55, (0.2, 0.24, 0.32), thick=0.03, nu=7, nv=5, center=(0, 0, 0.97), point=3.0, bulge=0.04)
    paint_vertex_normals(cuirass, plate)
    tasset = shell(180, 220, -60, -25, (0.2, 0.25, 0.32), thick=0.025, nu=7, nv=2, center=(0.02, 0, 0.95), point=0.0)
    paint_vertex_normals(tasset, plate)
    gem = ellipsoid((-0.2, 0, 1.0), (0.025, 0.05, 0.07), sides=6, rings=4, smooth=False).fill(hexc("#7AF0FF", 1.0))
    cape = leaf(length=0.75, width=0.42, segs=8, across=3, curl=-0.05, fold=-0.1, thick=0.015, tip_power=0.4, base_width=0.6)
    cape.transform(T(0.16, 0, 1.22) @ rot(0, 100, 0))
    cape.paint(lambda p, n, fc, fi: mix(hexc("#3A0E3A"), hexc("#9A2A7A"), smoothstep(0.4, 1.2, p.z)))
    B = part("Body", merge(torso, cuirass, tasset, gem, cape), root, HIP)
    neck = Vector((-0.02, 0, 1.25))
    hc = neck + Vector((0, 0, 0.1))
    helm = ellipsoid(hc, (0.12, 0.12, 0.14), sides=12, rings=8)
    paint_vertex_normals(helm, plate)
    slit = ellipsoid(hc + Vector((-0.11, 0, 0.01)), (0.02, 0.08, 0.014), sides=8, rings=4).fill(hexc("#7AF0FF", 1.0))
    crest = Geo()
    for k in range(4):
        b = hc + Vector((0.02 + 0.04 * k, 0, 0.13 - 0.02 * k))
        crest.extend(cone(b, b + Vector((0.06, 0, 0.1)), 0.025, sides=4))
    crest.fill(BONE1)
    part("Head", merge(helm, slit, crest), root, neck, parent=B)
    for side, nm in ((-1, "ArmL"), (1, "ArmR")):
        sh = Vector((0.0, 0.25 * side, 1.15))
        hand = Vector((-0.2, 0.28 * side, 0.68))
        g = limb([sh, sh + Vector((-0.08, 0.05 * side, -0.25)), hand], 0.06, 0.045).vpaint(sk)
        pauld = shell(90 * side, 160, -10, 80, (0.11, 0.11, 0.1), thick=0.025, nu=5, nv=3, center=sh, point=0.0)
        paint_vertex_normals(pauld, plate)
        g.extend(pauld)
        g.extend(ellipsoid(hand, (0.06, 0.05, 0.06), sides=8, rings=5).vpaint(sk))
        g.extend(claws(hand + Vector((-0.04, 0, -0.04)), (-0.4, 0, -1), 3, 0.07, 0.013, 0.03))
        part(nm, g, root, sh, parent=B)
    for side, nm in ((-1, "LegL"), (1, "LegR")):
        hip = HIP + Vector((0, 0.11 * side, 0))
        ank = Vector((0.02, 0.13 * side, 0.08))
        g = limb([hip, hip + Vector((-0.05, 0.02 * side, -0.28)), ank], 0.075, 0.055).vpaint(sk)
        greave = tube([ank + Vector((0, 0, 0.06)), ank + Vector((-0.02, 0, 0.32))], [0.07, 0.08], sides=8)
        paint_vertex_normals(greave, plate)
        g.extend(greave)
        g.extend(ellipsoid((ank.x - 0.06, ank.y, 0.04), (0.12, 0.07, 0.045), sides=8, rings=5).vpaint(sk))
        part(nm, clamp_ground(g), root, hip)
    # tower shield on its left (-Y) front, held by the left hand
    grip = Vector((-0.22, -0.3, 0.68))
    sh_ = leaf(length=0.9, width=0.48, segs=8, across=3, curl=0.0, fold=-0.12, thick=0.04, tip_power=0.35, base_width=0.75)
    sh_.transform(rot(0, -90, 0))   # length up, face +X
    sh_.transform(rot(0, 0, 180))   # face -X (forward)
    sh_.transform(T(-0.3, -0.3, 0.25))
    def shield_paint(p, n, fc, fi):
        if n.x < -0.3:
            c = mix(hexc("#4A3E5A"), hexc("#A898B8"), smoothstep(0.3, 1.1, p.z))
            boss = (Vector((p.y, p.z, 0)) - Vector((-0.3, 0.72, 0))).length
            c = mix(c, hexc("#7AF0FF", 1.0), smoothstep(0.06, 0.035, boss))
            return mix(c, hexc("#9A2A7A"), smoothstep(0.17, 0.2, abs(p.y + 0.3)) * 0.8)
        return hexc("#3A3040")
    sh_.paint(shield_paint)
    rim = tube([Vector((-0.31, -0.3, 0.25)), Vector((-0.31, -0.3, 1.15))], 0.02, sides=6).fill(BONE1)
    part("Shield", merge(sh_, rim), root, grip)
    aura = lathe([(0.38, -0.004), (0.46, -0.004), (0.48, 0.006), (0.46, 0.016), (0.38, 0.016), (0.36, 0.006)], sides=28, boost=False)
    aura.transform(T(0, 0, 0.004))
    aura.paint(lambda p, n, fc, fi: mix(hexc("#7AF0FF", 1.0), hexc("#FF4FD2", 1.0), 0.5 + 0.5 * math.sin(math.atan2(p.y, p.x) * 3)))
    runes = Geo()
    for k in range(6):
        a = 2 * math.pi * k / 6
        runes.extend(box((math.cos(a) * 0.42, math.sin(a) * 0.42, 0.02), (0.05, 0.02, 0.008), rotm=rot(0, 0, math.degrees(a))))
    runes.fill(hexc("#FFFFFF", 1.0))
    part("Aura", merge(aura, runes), root, (0, 0, 0))
    finish(root, "Enemies", icon=True)
    vskit.RES = 1.6


# ------------------------------------------------------------------ Blight Herald
@model
def BlightHerald():
    root = new_scene("BlightHerald")
    C0, C1 = hexc("#1C1220"), hexc("#5A3060")
    sk = skin(C0, C1, 0.4, 1.3)
    HIP = Vector((0.03, 0, 0.6))
    robe = lathe([(0.0, 0.0), (0.33, 0.0), (0.3, 0.12), (0.24, 0.45), (0.2, 0.75), (0.18, 0.95), (0.12, 1.12), (0.0, 1.16)], sides=16,
                 jitter=lambda pi, si, a: 1 + (0.08 * math.sin(a * 7) if pi <= 1 else 0.02 * math.sin(a * 3)), round_profile=1)
    robe.paint(lambda p, n, fc, fi: mix(hexc("#3A0E3A"), hexc("#8A2A7A"), smoothstep(0.0, 1.0, p.z)) if p.z > 0.12 else hexc("#2A0A2A"))
    trim = Geo()
    for z in (0.12, 0.75):
        r = 0.3 if z < 0.5 else 0.2
        rg = lathe([(0.0, -0.015), (r + 0.01, -0.015), (r + 0.02, 0.0), (r + 0.01, 0.015), (0.0, 0.015)], sides=16)
        trim.extend(rg.transform(T(0, 0, z)).fill(hexc("#E0B84A")))
    sigil = ellipsoid((-0.2, 0, 0.6), (0.02, 0.07, 0.1), sides=6, rings=4, smooth=False).fill(LIME)
    collar = Geo()
    for k in range(7):
        a = math.radians(-90 + 180 * k / 6) + math.pi
        b = Vector((math.cos(a) * 0.14, math.sin(a) * 0.14, 1.1))
        collar.extend(cone(b, b + Vector((math.cos(a) * 0.06, math.sin(a) * 0.06, 0.12)), 0.03, sides=4))
    collar.fill(BONE1)
    B = part("Body", merge(robe, trim, sigil, collar), root, HIP)
    neck = Vector((-0.02, 0, 1.14))
    hc = neck + Vector((-0.02, 0, 0.1))
    head = ellipsoid(hc, (0.1, 0.1, 0.12), sides=12, rings=7).vpaint(skin(hexc("#8A7A6A"), hexc("#C8B8A0"), 1.1, 1.35))
    sockets = glow_eyes(hc + Vector((-0.085, 0, 0.02)), 0.07, 0.02, col=LIME)
    crown = Geo()
    band = lathe([(0.0, -0.025), (0.105, -0.025), (0.11, 0.0), (0.105, 0.025), (0.0, 0.025)], sides=14)
    crown.extend(band.transform(T(*(hc + Vector((0, 0, 0.09))))))
    for k in range(7):
        a = 2 * math.pi * k / 7
        b = hc + Vector((math.cos(a) * 0.1, math.sin(a) * 0.1, 0.1))
        crown.extend(cone(b, b + Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, 0.12 + 0.04 * (k % 2))), 0.025, sides=4))
    crown.fill(hexc("#E0B84A"))
    jewel = ellipsoid(hc + Vector((-0.1, 0, 0.1)), (0.015, 0.025, 0.03), sides=6, rings=4, smooth=False).fill(MAGENTA)
    part("Head", merge(head, sockets, crown, jewel), root, neck, parent=B)
    for side, nm in ((-1, "ArmL"), (1, "ArmR")):
        sh = Vector((0.0, 0.2 * side, 1.02))
        hand = Vector((-0.2, 0.26 * side, 0.7)) if side > 0 else Vector((-0.16, 0.24 * side, 0.62))
        sleeve = limb([sh, sh + Vector((-0.08, 0.05 * side, -0.15)), hand], 0.07, 0.09).paint(lambda p, n, fc, fi: mix(hexc("#3A0E3A"), hexc("#8A2A7A"), smoothstep(0.5, 1.0, p.z)))
        g = merge(sleeve, ellipsoid(hand + Vector((-0.03, 0, -0.03)), (0.045, 0.04, 0.045), sides=8, rings=5).paint(skin(hexc("#C8B8A8"), hexc("#E8DCCC"), 0.5, 0.8)))
        part(nm, g, root, sh, parent=B)
    for side, nm in ((-1, "LegL"), (1, "LegR")):
        hip = HIP + Vector((0, 0.1 * side, 0))
        g = limb([hip, hip + Vector((-0.02, 0.02 * side, -0.3)), Vector((-0.02, 0.12 * side, 0.05))], 0.06, 0.05).vpaint(sk)
        g.extend(ellipsoid((-0.08, 0.12 * side, 0.035), (0.1, 0.06, 0.035), sides=8, rings=5).vpaint(sk))
        part(nm, clamp_ground(g), root, hip)
    grip = Vector((-0.23, 0.26, 0.67))
    pole = tube([Vector((-0.24, 0.26, 0.02)), Vector((-0.24, 0.26, 1.85))], 0.022, sides=7).fill(hexc("#3A2A1E"))
    top = cone((-0.24, 0.26, 1.85), (-0.24, 0.26, 1.95), 0.04, sides=5).fill(hexc("#E0B84A"))
    orb = sphere((-0.24, 0.26, 1.83), 0.045, sides=10, rings=6).fill(LIME)
    cross = tube([Vector((-0.24, 0.26, 1.72)), Vector((0.1, 0.26, 1.72))], 0.016, sides=6).fill(hexc("#3A2A1E"))
    flag = leaf(length=0.55, width=0.3, segs=8, across=2, curl=0.0, fold=0.0, thick=0.01, tip_power=0.2, base_width=0.9,
                shape=lambda u: (1.0 - 0.35 * max(0.0, u - 0.7) / 0.3) if u < 1 else 0.6)
    flag.transform(rot(90, 0, 0))     # hang in the XZ plane
    flag.transform(rot(0, 90, 0))     # length downward
    flag.transform(T(-0.08, 0.26, 1.71))
    flag.displace(lambda p: Vector((0, 0.03 * math.sin(p.z * 9 + p.x * 6), 0)))
    def fp(p, n, fc, fi):
        c = mix(hexc("#5A0E4A"), hexc("#C02A8A"), smoothstep(1.1, 1.7, p.z))
        d = (Vector((p.x, p.z, 0)) - Vector((-0.08, 1.45, 0))).length
        return mix(c, LIME, smoothstep(0.07, 0.05, d) + smoothstep(0.012, 0.004, abs(d - 0.1)))
    flag.paint(fp)
    part("Banner", merge(pole, top, orb, cross, flag), root, grip)
    finish(root, "Enemies", icon=True)


# ------------------------------------------------------------------ Mimic
@model
def Mimic():
    root = new_scene("Mimic")
    body = soft_blob((0, 0, 0.38), (0.36, 0.34, 0.38), seed=4, lump=0.18, freq=2.2, sides=16, rings=10)
    for p in body.v:
        if p.z < 0.02:
            p.z = 0.02 + (p.z - 0.02) * 0.1
    def iri(p, n, fc, fi):
        t = 0.5 + 0.5 * math.sin(n.x * 3 + n.y * 2 + p.z * 6)
        c = mix(hexc("#3A1A5A"), hexc("#2A8A9A"), t)
        c = mix(c, hexc("#E0A0FF", 0.35), smoothstep(0.6, 0.95, n.z) * 0.6)
        return mix(c, hexc("#1A0E2A"), smoothstep(0.2, 0.0, p.z))
    paint_vertex_normals(body, iri)
    B = part("Body", body, root, (0, 0, 0.38))
    eyes = Geo()
    for k, (y, z, s, col) in enumerate(((-0.12, 0.5, 1.0, "#7AF0FF"), (0.1, 0.56, 1.25, "#FF4FD2"), (0.0, 0.36, 0.7, "#C4FF4D"), (0.17, 0.38, 0.6, "#7AF0FF"))):
        c = Vector((-0.31 + 0.03 * abs(y), y, z))
        gem = lathe([(0.0, -0.06 * s), (0.045 * s, -0.02 * s), (0.05 * s, 0.01 * s), (0.03 * s, 0.04 * s), (0.0, 0.05 * s)], sides=6, smooth=False)
        gem.transform(T(*c) @ align((-1, 0, 0.1)))
        eyes.extend(gem.fill(hexc(col, 1.0)))
        socket = lathe([(0.055 * s, -0.01), (0.065 * s, 0.0), (0.055 * s, 0.012)], sides=10, cap_bottom=False, cap_top=False)
        socket.transform(T(*(c + Vector((0.01, 0, 0)))) @ align((-1, 0, 0.1)))
        eyes.extend(socket.fill(hexc("#1A0E2A")))
    part("Eyes", eyes, root, (-0.3, 0, 0.45), parent=B)
    ten = Geo()
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.3
        d = Vector((math.cos(a), math.sin(a), 0))
        p0 = d * 0.28 + Vector((0, 0, 0.08))
        pts = bezier(p0, p0 + d * 0.12 + Vector((0, 0, 0.05)), p0 + d * 0.2 + Vector((0, 0, -0.06)),
                     p0 + d * 0.16 + Vector((0.02, 0.04, 0.0)), n=5)
        ten.extend(tube(pts, taper(len(pts), 0.04, 0.01), sides=6, round1=True))
    ten.paint(lambda p, n, fc, fi: mix(hexc("#3A1A5A"), hexc("#FF4FD2", 0.6), smoothstep(0.32, 0.48, Vector((p.x, p.y, 0)).length)))
    part("Tendrils", clamp_ground(ten), root, (0, 0, 0.05), parent=B)
    finish(root, "Enemies", icon=True)


if __name__ == "__main__":
    for n in (argv() or list(MODELS)):
        MODELS[n]()
