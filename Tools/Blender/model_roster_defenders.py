"""Milestone 3 defenders (face +X, origin at ground centre, footprint <= 0.8 m, 0.6-1.0 m tall).
Usage: blender -b --factory-startup --python Tools/Blender/model_roster_defenders.py -- [Name ...]"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_STEM0, C_STEM1 = hexc("#2F6B2A"), hexc("#7CC243")
C_LEAF0, C_LEAF1 = hexc("#2E7A2E"), hexc("#A6DB4E")
C_BARK0, C_BARK1 = hexc("#4A3220"), hexc("#8A6440")
C_MOSS0, C_MOSS1 = hexc("#3C7230"), hexc("#8CC653")

MODELS = {}
def model(fn):
    MODELS[fn.__name__] = fn
    return fn


def base_leaves(side, c0=C_LEAF0, c1=C_LEAF1, length=0.3, width=0.22, spiky=False, yaws=(100, 150)):
    g = Geo()
    for k, yaw in enumerate(yaws):
        ln = length * (1.0 if k == 0 else 0.78)
        if spiky:
            lf = leaf(length=ln, width=width * 0.6, segs=6, across=1, curl=0.35, fold=0.3, thick=0.02, tip_power=1.4)
        else:
            lf = leaf(length=ln, width=width * (1.0 if k == 0 else 0.8), segs=6, across=2, curl=0.3, fold=0.18,
                      thick=0.02, droop=0.05, tip_power=0.75)
        lf.transform(rot(0, -16 - 8 * k, 0))
        lf.transform(rot(0, 0, yaw if side > 0 else -yaw))
        lf.transform(T(-0.03, 0.03 * side, 0.03))
        g.extend(lf)
    g.paint(lambda p, n, fc, fi: shade(mix(c0, c1, smoothstep(0.04, length, Vector((p.x + 0.03, p.y, 0)).length)),
                                       1.0 if n.z > 0 else 0.8))
    return g


def stem_curve(pts_ctrl, r0, r1, c0=C_STEM0, c1=C_STEM1, flare=0.12, sides=10):
    pts = bezier(*pts_ctrl, n=7)
    g = tube(pts, taper(len(pts), r0, r1, 0.7), sides=sides, cap0=True, cap1=True)
    fl = lathe([(0.0, 0.0), (flare, 0.0), (flare * 0.8, 0.025), (r0 * 1.05, 0.07), (0.0, 0.075)], sides=10)
    fl.transform(T(*pts[0]))
    g.extend(fl)
    top = Vector(pts_ctrl[-1]).z
    g.paint(lambda p, n, fc, fi: mix(c0, c1, smoothstep(0.0, top, p.z)))
    return g


def crystal(base, direction, length, r, col, sides=6):
    """Faceted ice/gem prism with a pointed tip (crisp)."""
    g = lathe([(0.0, 0.0), (r, 0.0), (r * 1.05, length * 0.7), (0.0, length)], sides=sides, smooth=False)
    g.transform(T(*base) @ align(direction))
    return g.fill(col)


# ------------------------------------------------------------------ Thorncaster
@model
def Thorncaster():
    root = new_scene("Thorncaster")
    neck = Vector((-0.02, 0, 0.5))
    st = stem_curve(((-0.03, 0, 0), (-0.1, 0, 0.18), (0.06, 0, 0.32), tuple(neck)), 0.085, 0.06,
                    c0=hexc("#3A4F22"), c1=hexc("#6E8C36"))
    # woody bark rings on the stem
    for z in (0.12, 0.26, 0.4):
        rg = lathe([(0.0, -0.012), (0.085, -0.012), (0.09, 0.0), (0.085, 0.012), (0.0, 0.012)], sides=10)
        rg.transform(T(-0.02 + 0.03 * math.sin(z * 9), 0, z)); st.extend(rg.fill(C_BARK0))
    part("Stem", clamp_ground(st), root)
    # head: armoured pod (crossbow stock) along X with bark plates, bow limbs, vine string, loaded thorn bolt
    hc = neck + Vector((0.02, 0, 0.13))
    pod = lathe([(0.0, -0.24), (0.08, -0.2), (0.14, -0.1), (0.155, 0.04), (0.13, 0.16), (0.08, 0.23), (0.0, 0.25)],
                sides=14, round_profile=1)
    pod.transform(T(*hc) @ rot(0, 90, 0) @ S(1, 0.9, 1))
    pod.paint(lambda p, n, fc, fi: mix(hexc("#4B6B2A"), hexc("#9CC24E"), smoothstep(hc.z - 0.1, hc.z + 0.12, p.z)))
    plates = Geo()
    for k in range(4):
        for az in (90, -90):
            sh = shell(az, 110, 5, 86, (0.16, 0.15, 0.155), thick=0.028, nu=4, nv=3, point=0.0, bulge=0.08)
            sh.transform(T(hc.x - 0.15 + k * 0.08, 0, hc.z - 0.005) @ S(0.5, 1, 1))
            plates.extend(sh)
    paint_vertex_normals(plates, lambda p, n, fc, fi: mix(C_BARK0, C_BARK1, smoothstep(0.3, 0.9, n.z)))
    limbs = Geo()
    for side in (1, -1):
        pts = bezier(hc + Vector((0.12, 0.08 * side, 0.06)), hc + Vector((0.16, 0.25 * side, 0.1)),
                     hc + Vector((0.0, 0.34 * side, 0.12)), n=6)
        limbs.extend(tube(pts, taper(len(pts), 0.04, 0.016), sides=7, round1=True))
    paint_vertex_normals(limbs, lambda p, n, fc, fi: mix(C_BARK0, C_BARK1, 0.5 + 0.4 * n.z))
    string = tube([hc + Vector((0.0, 0.34, 0.12)), hc + Vector((-0.14, 0.0, 0.14)), hc + Vector((0.0, -0.34, 0.12))],
                  0.006, sides=4, smooth=False).fill(hexc("#C9E39A"))
    bolt = tube([hc + Vector((-0.16, 0, 0.15)), hc + Vector((0.22, 0, 0.15))], 0.022, sides=6).fill(hexc("#EDE0C0"))
    tip = crystal(hc + Vector((0.22, 0, 0.15)), (1, 0, 0), 0.13, 0.04, hexc("#D2402F", 0.3), sides=5)
    fletch = Geo()
    for k in range(3):
        lf = leaf(length=0.07, width=0.04, segs=2, across=1, curl=0.0, fold=0.1, thick=0.006)
        lf.transform(T(*(hc + Vector((-0.1, 0, 0.15)))) @ rot(120 * k, 0, 0) @ rot(0, 0, 180) @ rot(0, -20, 0))
        fletch.extend(lf)
    fletch.fill(hexc("#9BE04A"))
    eyes = Geo()
    for side in (1, -1):
        eyes.extend(ellipsoid(hc + Vector((0.13, 0.075 * side, 0.03)), (0.022, 0.028, 0.022), sides=8, rings=5).fill(hexc("#FFE45C", 1.0)))
    H = part("Head", merge(pod, plates, limbs, string, bolt, tip, fletch, eyes), root, neck)
    marker("Muzzle", root, hc + Vector((0.36, 0, 0.15)), parent=H)
    part("LeafL", base_leaves(1, spiky=True, length=0.32), root, (-0.03, 0.03, 0.03))
    part("LeafR", base_leaves(-1, spiky=True, length=0.32), root, (-0.03, -0.03, 0.03))
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Snapjaw Vine
@model
def SnapjawVine():
    root = new_scene("SnapjawVine")
    # coiled vine: a helix rising to the hinge
    pts = []
    for i in range(40):
        t = i / 39
        a = t * 3.2 * math.pi
        rr = 0.09 * (1 - t * 0.6)
        pts.append(Vector((math.cos(a) * rr - 0.04, math.sin(a) * rr, 0.02 + t * 0.4)))
    st = tube(pts, taper(len(pts), 0.05, 0.035), sides=8, cap0=True, cap1=True)
    st.paint(lambda p, n, fc, fi: mix(C_STEM0, C_STEM1, smoothstep(0, 0.45, p.z)))
    fl = lathe([(0.0, 0.0), (0.15, 0.0), (0.11, 0.03), (0.06, 0.06), (0.0, 0.065)], sides=10).fill(C_STEM0)
    tendr = Geo()
    for side in (1, -1):
        tp = [Vector((0.0, 0.06 * side, 0.2)) + Vector((0.06 * math.cos(t * 6), side * (0.04 + 0.05 * t), 0.06 * math.sin(t * 6) + t * 0.1)) for t in [i / 10 for i in range(11)]]
        tendr.extend(tube(tp, 0.009, sides=4, smooth=False).fill(C_STEM1))
    part("Stem", clamp_ground(merge(st, fl, tendr)), root)
    hinge = Vector((-0.08, 0, 0.47))
    # jaws: two clam halves (upper opens up/forward), spiky teeth, glowing pink interior
    def jaw(upper):
        sgn = 1 if upper else -1
        def half(rx, ry, rz, flip):
            e = ellipsoid((0, 0, 0), (rx, ry, rz), sides=16, rings=10)
            h = Geo(); h.v = e.v
            for f in e.f:
                if all(e.v[i].z * sgn >= -1e-6 for i in f):
                    h.add_face(f[::-1] if flip else f, True)
            return h
        g = half(0.24, 0.18, 0.11, False)
        ig = half(0.215, 0.16, 0.085, True)
        def op(p, n, fc, fi):
            return mix(hexc("#2E7A2E"), hexc("#9BD84E"), smoothstep(0.0, 0.1, abs(p.z))) if True else None
        g.paint(op)
        ig.paint(lambda p, n, fc, fi: mix(hexc("#FF5C8A", 0.65), hexc("#B8235A", 0.2), smoothstep(0.0, 0.08, abs(p.z))))
        rim = Geo()
        for k in range(13):
            a = math.radians(-150 + 300 * k / 12)
            b = Vector((math.cos(a) * 0.225, math.sin(a) * 0.165, 0.0))
            rim.extend(cone(b, b + Vector((math.cos(a) * 0.02, math.sin(a) * 0.015, -0.06 * sgn)), 0.016, sides=4).fill(hexc("#F4EBD0")))
        spots = Geo()
        for k in range(5):
            a = math.radians(-60 + 30 * k)
            pp = Vector((math.cos(a) * 0.12, math.sin(a) * 0.1, 0.09 * sgn))
            spots.extend(sphere(pp, 0.016, sides=6, rings=4).fill(hexc("#D8FF6A", 0.8)))
        out = merge(g, ig, rim, spots if upper else None)
        # put the hinge at local origin: jaw extends forward (+X) from the hinge
        out.transform(T(0.22, 0, 0))
        return out
    up = jaw(True); up.transform(T(*hinge) @ rot(0, -28, 0))
    lo = jaw(False); lo.transform(T(*hinge) @ rot(0, 6, 0))
    part("Jaw", up, root, hinge)
    part("JawLower", lo, root, hinge)
    part("LeafL", base_leaves(1, length=0.3), root, (-0.03, 0.03, 0.03))
    part("LeafR", base_leaves(-1, length=0.3), root, (-0.03, -0.03, 0.03))
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Frost Fern
@model
def FrostFern():
    root = new_scene("FrostFern")
    C_F0, C_F1, C_TIP = hexc("#2F6E7A"), hexc("#8FD3E0"), hexc("#E6FCFF", 0.85)
    base = lathe([(0.0, 0.0), (0.16, 0.0), (0.14, 0.05), (0.08, 0.12), (0.04, 0.16), (0.0, 0.17)], sides=12, round_profile=1)
    base.paint(lambda p, n, fc, fi: mix(hexc("#3E7A86"), hexc("#BCEAF2"), smoothstep(0, 0.17, p.z)))
    frost = Geo()
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.3
        frost.extend(crystal((math.cos(a) * 0.13, math.sin(a) * 0.13, 0.0), (math.cos(a) * 0.4, math.sin(a) * 0.4, 1), 0.09, 0.022,
                             hexc("#D9F6FF", 0.35)))
    part("Stem", merge(base, frost), root)
    # fronds: arching rachis with paired leaflets, pale tips glow
    fr = Geo()
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.15
        d = Vector((math.cos(a), math.sin(a), 0))
        ln = 0.4 if abs(math.cos(a)) < 0.6 else 0.36
        p0 = Vector((0, 0, 0.14)) + d * 0.04
        pts = bezier(p0, p0 + d * ln * 0.25 + Vector((0, 0, ln * 1.1)), p0 + d * ln * 0.75 + Vector((0, 0, ln * 0.95)), n=6)
        fr.extend(tube(pts, taper(len(pts), 0.02, 0.006), sides=5))
        for j in range(1, len(pts) - 1, 1):
            t = j / (len(pts) - 1)
            tang = (pts[j + 1] - pts[j - 1]).normalized()
            side_dir = tang.cross(Vector((0, 0, 1))).normalized()
            for s in (1, -1):
                L = 0.13 * (1 - t * 0.65)
                lf = leaf(length=L, width=L * 0.55, segs=2, across=1, curl=-0.25, fold=0.2, thick=0.008)
                lf.transform(basis(side_dir * s + tang * 0.35, Vector((0, 0, 1)), pts[j]))
                fr.extend(lf)
    fr.paint(lambda p, n, fc, fi: mix(C_F0, C_F1, smoothstep(0.15, 0.45, p.z)) if p.z < 0.45 else mix(C_F1, C_TIP, smoothstep(0.45, 0.55, p.z)))
    # glowing frost tips
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.15
        d = Vector((math.cos(a), math.sin(a), 0))
        ln = 0.4 if abs(math.cos(a)) < 0.6 else 0.36
        tipp = Vector((0, 0, 0.14)) + d * (0.04 + ln * 0.75) + Vector((0, 0, ln * 0.95))
        fr.extend(sphere(tipp, 0.022, sides=6, rings=4).fill(C_TIP))
    part("Fronds", fr, root, (0, 0, 0.14))
    cr = Geo()
    for k, (dx, dy, ln, r) in enumerate(((0.0, 0.0, 0.32, 0.05), (0.06, 0.05, 0.2, 0.035), (0.05, -0.06, 0.22, 0.035),
                                          (-0.06, 0.02, 0.18, 0.03))):
        cr.extend(crystal((dx * 0.5, dy * 0.5, 0.15), (dx, dy, 1), ln, r, hexc("#A8ECFF", 0.55)))
    cr.extend(crystal((0.03, 0, 0.3), (1, 0, 0.4), 0.12, 0.03, hexc("#E6FCFF", 0.9)))
    C = part("Crystal", cr, root, (0, 0, 0.15))
    marker("Muzzle", root, (0.16, 0, 0.36), parent=C)
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Ember Lily
@model
def EmberLily():
    root = new_scene("EmberLily")
    st = stem_curve(((-0.02, 0, 0), (-0.08, 0, 0.2), (0.06, 0, 0.36), (0.02, 0, 0.5)), 0.04, 0.03,
                    c0=hexc("#2E5E2A"), c1=hexc("#6FA43C"))
    lv = Geo()
    for side in (1, -1):
        lf = leaf(length=0.34, width=0.11, segs=7, across=1, curl=0.5, fold=0.25, thick=0.015, tip_power=1.0)
        lf.transform(T(-0.02, 0.02 * side, 0.05) @ rot(0, 0, 80 * side + 20) @ rot(0, -45, 0))
        lv.extend(lf)
    lv.paint(lambda p, n, fc, fi: mix(hexc("#2E6E2E"), hexc("#8FCB4A"), smoothstep(0.05, 0.4, p.z)))
    part("Stem", merge(st, lv), root)
    bc = Vector((0.03, 0, 0.5))
    face = Vector((1, 0, 0.55)).normalized()          # bloom faces the field, tilted up
    bl = Geo()
    for k in range(6):
        a = 2 * math.pi * k / 6
        ln = 0.26 if k % 2 == 0 else 0.23
        pl = leaf(length=ln, width=0.11, segs=6, across=2, curl=-0.9, fold=0.22, thick=0.016, tip_power=1.1)
        # petals radiate around the bloom axis and curl back
        radial = Vector((0, math.cos(a), math.sin(a)))
        m = basis(Vector((0.55, 0, 0)) + radial, Vector((1, 0, 0)), (0, 0, 0))
        pl.transform(m)
        bl.extend(pl)
    def petal_paint(p, n, fc, fi):
        r = p.length
        c = mix(hexc("#FFB23A"), hexc("#E8461E"), smoothstep(0.03, 0.18, r))
        c = mix(c, hexc("#FF7A2A", 0.8), smoothstep(0.2, 0.26, r))
        return c
    bl.paint(petal_paint)
    m = T(*bc) @ basis(face, Vector((0, 0, 1)))
    bl.transform(m @ rot(0, 0, 0))
    blm = Geo().extend(bl)
    part("Bloom", blm, root, bc)
    core = Geo()
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.5
        tipp = Vector((0.14, math.cos(a) * 0.05, math.sin(a) * 0.05))
        core.extend(tube([Vector((0.0, 0, 0)), tipp * 0.6 + Vector((0, 0, 0.0)), tipp], [0.008, 0.006, 0.005], sides=4).fill(hexc("#FFD24A", 0.7)))
        core.extend(sphere(tipp, 0.017, sides=6, rings=4).fill(hexc("#FF6A1A", 1.0)))
    core.extend(ellipsoid((0.04, 0, 0), (0.05, 0.045, 0.045), sides=10, rings=6).fill(hexc("#FFE27A", 1.0)))
    core.transform(T(*bc) @ basis(face, Vector((0, 0, 1))))
    Cc = part("Core", core, root, bc)
    marker("Muzzle", root, bc + face * 0.2, parent=Cc)
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Whirlseed
@model
def Whirlseed():
    root = new_scene("Whirlseed")
    top = Vector((0, 0, 0.62))
    st = stem_curve(((0, 0, 0), (-0.05, 0, 0.25), (0.04, 0, 0.45), tuple(top)), 0.05, 0.035)
    lv = Geo()
    for side in (1, -1):
        lf = leaf(length=0.28, width=0.2, segs=6, across=2, curl=0.3, fold=0.2, thick=0.018, tip_power=0.8)
        lf.transform(T(0, 0.02 * side, 0.03) @ rot(0, 0, 100 * side) @ rot(0, -15, 0))
        lv.extend(lf)
    lv.paint(lambda p, n, fc, fi: mix(C_LEAF0, C_LEAF1, smoothstep(0.03, 0.25, Vector((p.x, p.y, 0)).length)))
    part("Stem", merge(st, lv), root)
    # pod: seed bulb mid-stem on the field side, the launcher
    pc = Vector((0.08, 0, 0.36))
    pod = lathe([(0.0, -0.08), (0.06, -0.06), (0.075, 0.0), (0.06, 0.06), (0.03, 0.09), (0.0, 0.095)], sides=12, round_profile=1)
    pod.transform(T(*pc) @ rot(0, 90, 0))
    pod.paint(lambda p, n, fc, fi: mix(hexc("#8A5A2E"), hexc("#E0B060"), smoothstep(pc.z - 0.06, pc.z + 0.07, p.z)))
    pod.extend(lathe([(0.04, 0.0), (0.05, 0.015), (0.035, 0.03), (0.0, 0.025)], sides=10).transform(T(*(pc + Vector((0.09, 0, 0)))) @ rot(0, 90, 0)).fill(hexc("#FFE08A", 0.8)))
    P = part("Pod", pod, root, pc)
    marker("Muzzle", root, pc + Vector((0.13, 0, 0)), parent=P)
    # rotor: three maple-seed wings around the vertical axis
    ro = Geo()
    hub = sphere((0, 0, 0), 0.05, sides=10, rings=6).fill(hexc("#8A5A2E"))
    ro.extend(hub)
    for k in range(3):
        a = 120 * k
        seed = ellipsoid((0.06, 0, 0), (0.05, 0.03, 0.028), sides=8, rings=5).fill(hexc("#A06A34"))
        wing = leaf(length=0.3, width=0.19, segs=7, across=2, curl=0.08, fold=0.05, thick=0.01, tip_power=0.6,
                    shape=lambda u: max(0.2, math.sin(math.pi * min(1, u) ** 0.55)) * (1 - 0.3 * u) if u < 1 else 0.0)
        wing.transform(T(0.08, 0, 0) @ rot(-8, 0, 0))
        wing.paint(lambda p, n, fc, fi: mix(hexc("#C9A052"), hexc("#F2DA8A"), smoothstep(0.1, 0.35, p.x)))
        vein = tube([Vector((0.08, 0, 0.004)), Vector((0.36, 0.02, 0.01))], 0.005, sides=3, smooth=False).fill(hexc("#8A5A2E"))
        g = merge(seed, wing, vein)
        g.transform(rot(0, 0, a) @ rot(0, -8, 0))
        ro.extend(g)
    ro.transform(T(*top))
    part("Rotor", ro, root, top)
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Mender Moss
@model
def MenderMoss():
    root = new_scene("MenderMoss")
    mo = Geo()
    for k, (c, r) in enumerate((((0, 0, 0.1), (0.3, 0.28, 0.2)), ((0.12, 0.14, 0.07), (0.18, 0.16, 0.12)),
                                ((-0.14, -0.12, 0.06), (0.18, 0.17, 0.11)), ((0.15, -0.14, 0.05), (0.14, 0.13, 0.09)),
                                ((-0.12, 0.16, 0.05), (0.14, 0.13, 0.09)))):
        mo.extend(soft_blob(c, r, seed=k, lump=0.16, freq=2.0, sides=9, rings=6, flatten_bottom=True))
    paint_vertex_normals(mo, foliage_soft(hexc("#2E5E2A"), hexc("#5FA040"), hexc("#B8E46A"), var=0.14, seed=3.0))
    sprigs = Geo()
    for k in range(10):
        a = 2 * math.pi * k / 10
        b = Vector((math.cos(a) * 0.22, math.sin(a) * 0.2, 0.12))
        sprigs.extend(blade(b, Vector((math.cos(a), math.sin(a), 0)), 0.08, 0.012, bend=0.5))
    sprigs.paint(grad_z(hexc("#3E7A30"), hexc("#B8E46A"), 0.1, 0.22))
    part("Mound", merge(mo, sprigs), root)
    orbs = Geo()
    for k, (x, y, z, r) in enumerate(((0.0, 0.0, 0.48, 0.07), (0.16, 0.12, 0.4, 0.05), (-0.14, -0.1, 0.42, 0.05), (0.1, -0.16, 0.55, 0.04))):
        orbs.extend(sphere((x, y, z), r, sides=10, rings=6).paint(lambda p, n, fc, fi: mix(hexc("#3FCF7A", 0.45), hexc("#D8FF9A", 0.6), smoothstep(-0.3, 0.8, n.z))))
        for j in range(4):
            a = 2 * math.pi * j / 4 + k
            lf = leaf(length=r * 1.2, width=r * 0.8, segs=2, across=1, curl=0.2, fold=0.1, thick=0.005)
            lf.transform(T(x, y, z) @ rot(0, 0, math.degrees(a)) @ T(r * 0.7, 0, 0) @ rot(0, -10, 0))
            orbs.extend(lf.fill(hexc("#8EE86A", 0.3)))
    part("Orbs", orbs, root, (0, 0, 0.45))
    bl = Geo()
    for k, (x, y, z, col) in enumerate(((0.05, 0.08, 0.29, "#FFF4F0"), (-0.1, 0.05, 0.25, "#FFC2D6"), (0.12, -0.06, 0.22, "#FFF4F0"),
                                        (-0.02, -0.14, 0.24, "#FFC2D6"), (0.2, 0.1, 0.18, "#FFE6A0"))):
        f = Geo()
        for j in range(5):
            pl = leaf(length=0.04, width=0.032, segs=2, across=1, curl=0.3, fold=0.1, thick=0.005)
            pl.transform(rot(0, 0, 72 * j) @ rot(0, -15, 0))
            f.extend(pl)
        f.fill(hexc(col))
        f.extend(sphere((0, 0, 0.006), 0.012, sides=6, rings=3).fill(hexc("#FFD24A", 0.5)))
        f.transform(T(x, y, z))
        bl.extend(f)
    part("Bloom", bl, root, (0, 0, 0.2))
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Burrowroot
@model
def Burrowroot():
    root = new_scene("Burrowroot")
    ring = Geo()
    dirt = lathe([(0.0, 0.0), (0.36, 0.0), (0.3, 0.035), (0.18, 0.06), (0.0, 0.065)], sides=16, round_profile=1,
                 jitter=lambda pi, si, a: 1 + 0.06 * math.sin(a * 5))
    dirt.paint(lambda p, n, fc, fi: shade(mix(hexc("#5A4028"), hexc("#8A6A44"), smoothstep(0, 0.06, p.z)), 1 + 0.1 * vnoise(p, 9)))
    ring.extend(dirt)
    for k in range(8):
        a = 2 * math.pi * k / 8
        d = Vector((math.cos(a), math.sin(a), 0))
        pts = bezier(d * 0.12 + Vector((0, 0, 0.05)), d * 0.25 + Vector((0, 0, 0.1)), d * 0.38 + Vector((0, 0, 0.0)), n=4)
        ring.extend(tube(pts, taper(len(pts), 0.035, 0.014), sides=6, round1=True))
    paint_vertex_normals(ring, lambda p, n, fc, fi: mix(hexc("#5A3E26"), hexc("#A07850"), smoothstep(0.0, 0.1, p.z))
                         if p.z > 0.02 or Vector((p.x, p.y, 0)).length > 0.33 else shade(mix(hexc("#5A4028"), hexc("#8A6A44"), smoothstep(0, 0.06, p.z)), 1 + 0.1 * vnoise(p, 9)))
    part("Ring", clamp_ground(ring), root)
    sp = Geo()
    sp.extend(tube(bezier((0, 0, 0.05), (0.01, 0, 0.12), (-0.01, 0, 0.18), n=3), 0.012, sides=5))
    for side in (1, -1):
        lf = leaf(length=0.1, width=0.07, segs=3, across=1, curl=0.3, fold=0.15, thick=0.008)
        lf.transform(T(0, 0, 0.17) @ rot(0, 0, 90 * side) @ rot(0, -30, 0))
        sp.extend(lf)
    sp.paint(lambda p, n, fc, fi: mix(hexc("#3E8A34"), hexc("#B8E46A"), smoothstep(0.05, 0.22, p.z)))
    part("Sprout", sp, root, (0, 0, 0.05))
    # spikes: modelled ERUPTED (full height); pivot at ground (z = 0) so the game can scale/slide them up from 0
    spk = Geo()
    rnd = random.Random(5)
    for k in range(9):
        a = 2 * math.pi * k / 9 + 0.2
        r = 0.2 + 0.06 * (k % 2)
        b = Vector((math.cos(a) * r, math.sin(a) * r, 0.0))
        h = rnd.uniform(0.45, 0.7)
        tip = b * 0.55 + Vector((0, 0, h))
        pts = bezier(b, b * 0.9 + Vector((0, 0, h * 0.5)), tip, n=4)
        spk.extend(tube(pts, taper(len(pts), 0.045, 0.004, 0.9), sides=6, cap0=True))
    paint_vertex_normals(spk, lambda p, n, fc, fi: mix(hexc("#4A321E"), hexc("#E8D8B0"), smoothstep(0.15, 0.6, p.z)))
    part("Spikes", spk, root, (0, 0, 0))
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Storm Orchid
@model
def StormOrchid():
    root = new_scene("StormOrchid")
    st = stem_curve(((-0.03, 0, 0), (-0.12, 0, 0.25), (0.08, 0, 0.42), (0.02, 0, 0.55)), 0.035, 0.025,
                    c0=hexc("#2A4A3A"), c1=hexc("#6A9A6A"))
    lv = Geo()
    for side in (1, -1):
        lf = leaf(length=0.3, width=0.15, segs=6, across=2, curl=0.25, fold=0.15, thick=0.016, tip_power=0.8)
        lf.transform(T(-0.02, 0.02 * side, 0.03) @ rot(0, 0, 95 * side) @ rot(0, -12, 0))
        lv.extend(lf)
    lv.paint(lambda p, n, fc, fi: mix(hexc("#2A5A3A"), hexc("#7EB86A"), smoothstep(0.03, 0.28, Vector((p.x, p.y, 0)).length)))
    part("Stem", merge(st, lv), root)
    fc_ = Vector((0.05, 0, 0.6))
    pet = Geo()
    # orchid: 3 sepals (long, narrow) + 2 broad petals + a frilled labellum, all facing +X
    spec = [(90, 0.2, 0.08), (210, 0.2, 0.08), (330, 0.2, 0.08), (150, 0.17, 0.13), (30, 0.17, 0.13)]
    for ang, ln, wd in spec:
        a = math.radians(ang)
        radial = Vector((0, math.cos(a), math.sin(a)))
        pl = leaf(length=ln, width=wd, segs=6, across=2, curl=-0.25, fold=0.12, thick=0.012, tip_power=0.9)
        pl.transform(basis(radial + Vector((0.25, 0, 0)), Vector((1, 0, 0))))
        pet.extend(pl)
    lab = leaf(length=0.13, width=0.14, segs=5, across=2, curl=0.6, fold=0.35, thick=0.012, tip_power=0.5)
    lab.transform(basis(Vector((0.6, 0, -1)), Vector((1, 0, 0))))
    pet.extend(lab)
    def pp(p, n, fc, fi):
        r = Vector((p.y, p.z, 0)).length
        c = mix(hexc("#3A1E7A"), hexc("#9A6AF0"), smoothstep(0.02, 0.15, r))
        return mix(c, hexc("#D8C0FF", 0.75), smoothstep(0.15, 0.2, r))
    pet.paint(pp)
    pet.transform(T(*fc_))
    part("Petals", pet, root, fc_)
    rod = Geo()
    rod.extend(tube([fc_ + Vector((-0.02, 0, 0)), fc_ + Vector((0.06, 0, 0.06)), fc_ + Vector((0.1, 0, 0.18))], [0.016, 0.012, 0.009], sides=6).fill(hexc("#C07A3A")))
    for k in range(3):
        rod.extend(lathe([(0.0, -0.006), (0.022, -0.006), (0.024, 0.0), (0.022, 0.006), (0.0, 0.006)], sides=8)
                   .transform(T(*(fc_ + Vector((0.03 + 0.025 * k, 0, 0.03 + 0.045 * k)))) @ rot(0, 30, 0)).fill(hexc("#E8B060")))
    tipp = fc_ + Vector((0.1, 0, 0.19))
    rod.extend(sphere(tipp, 0.03, sides=10, rings=6).fill(hexc("#9AD8FF", 0.8)))
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        zz = [tipp, tipp + Vector((0.02, math.cos(a) * 0.04, math.sin(a) * 0.04 + 0.01)),
              tipp + Vector((0.0, math.cos(a + 0.4) * 0.07, math.sin(a + 0.4) * 0.07))]
        rod.extend(tube(zz, 0.004, sides=3, smooth=False).fill(hexc("#E0D0FF", 1.0)))
    R = part("Rod", rod, root, fc_)
    marker("Muzzle", root, tipp + Vector((0.04, 0, 0)), parent=R)
    finish(root, "Defenders", icon=True)


# ------------------------------------------------------------------ Mirrorleaf
@model
def Mirrorleaf():
    root = new_scene("Mirrorleaf")
    st = stem_curve(((-0.05, 0, 0), (-0.08, 0, 0.12), (-0.04, 0, 0.2), (-0.03, 0, 0.26)), 0.05, 0.04)
    part("Stem", st, root)
    base = Vector((-0.02, 0, 0.18))
    # a broad shield leaf standing up, concave toward +X, silver-green mirror face, glowing rim
    m = leaf(length=0.62, width=0.5, segs=10, across=4, curl=-0.08, fold=-0.25, thick=0.03, tip_power=0.8, base_width=0.25)
    m.transform(rot(0, -90, 0))           # length along +Z, face toward +X
    m.transform(rot(0, 8, 0))
    def mirror(p, n, fc, fi):
        if n.x > 0.2:
            t = smoothstep(0.0, 0.6, p.z)
            c = mix(hexc("#6E9E88"), hexc("#D2EEE0"), 0.45 + 0.4 * vnoise(Vector((p.y, p.z, 0)), 2.5) + 0.25 * smoothstep(0.1, 0.5, p.z))
            c = mix(c, hexc("#E8FFF4", 0.6), smoothstep(0.56, 0.62, p.z))
            return c
        return mix(hexc("#2E6A3A"), hexc("#7CB85A"), smoothstep(0.0, 0.6, p.z))
    m.paint(mirror)
    vein = tube([Vector((0.02, 0, 0.0)), Vector((0.035, 0, 0.3)), Vector((0.02, 0, 0.58))], [0.012, 0.009, 0.004], sides=5).fill(hexc("#CFE8D8", 0.3))
    mm = merge(m, vein)
    mm.transform(T(*base))
    part("Mirror", mm, root, base)
    part("LeafL", base_leaves(1, length=0.28), root, (-0.03, 0.03, 0.03))
    part("LeafR", base_leaves(-1, length=0.28), root, (-0.03, -0.03, 0.03))
    finish(root, "Defenders", icon=True)


if __name__ == "__main__":
    for n in (argv() or list(MODELS)):
        MODELS[n]()
