"""Glowcap: Vitality generator. Plump cream stem with sleepy eyes, luminous teal cap with glowing spots and gills,
moss collar and two baby caps. Cap origin at the top of the stem (it pulses)."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_STEM0, C_STEM1 = hexc("#CDB894"), hexc("#FFF4DC")
C_CAP0, C_CAP1 = hexc("#0E5F78"), hexc("#2CC2C4")
C_CAPGLOW = hexc("#8AFFF0", 0.55)
C_SPOT = hexc("#E6FFF8", 1.0)
C_GILL0, C_GILL1 = hexc("#2FB7B4", 0.85), hexc("#B6FFF4", 1.0)
C_MOSS0, C_MOSS1 = hexc("#2D5E2B"), hexc("#8AC456")
C_LID = hexc("#5A3D2A")
C_CHEEK = hexc("#F5A58C")

root = new_scene("Glowcap")
TOP = 0.36
# ---------------- stem: plump, slightly leaning back, wider base
prof = [(0.0, 0.0), (0.12, 0.0), (0.135, 0.04), (0.125, 0.12), (0.112, 0.22), (0.1, 0.3), (0.085, TOP), (0.0, TOP + 0.01)]
stem = lathe(prof, sides=14, jitter=lambda pi, si, a: 1 + 0.03 * math.sin(a * 3 + pi))
stem.transform(Matrix.Shear('XY', 4, (0, 0)) if False else Matrix())
stem.displace(lambda p: Vector((-0.06 * (p.z / TOP) ** 2, 0, 0)))
stem.paint(lambda p, n, fc, fi: mix(C_STEM0, C_STEM1, smoothstep(0.0, 0.3, p.z)))
# sleepy face on the front (+X): closed-eye arcs + blush
face = Geo()
for side in (1, -1):
    y0 = 0.045 * side
    zc = 0.22
    xs = lambda z: 0.118 - 0.06 * ((z / TOP) ** 2) + 0.004
    arc = [Vector((xs(zc + 0.012 * math.sin(math.pi * t)), y0 + (t - 0.5) * 0.05, zc + 0.012 * math.sin(math.pi * t)))
           for t in [i / 4 for i in range(5)]]
    face.extend(tube(arc, 0.008, sides=4, cap0=True, cap1=True).fill(C_LID))
    face.extend(ellipsoid((xs(0.19) - 0.006, y0 * 1.45, 0.19), (0.008, 0.022, 0.014), sides=6, rings=3).fill(C_CHEEK))
stem = merge(stem, face)
# moss collar
moss = Geo()
for i in range(9):
    a = 2 * math.pi * i / 9 + 0.3
    r = 0.14 + 0.02 * math.sin(i * 1.7)
    moss.extend(ellipsoid((math.cos(a) * r, math.sin(a) * r, 0.025), (0.07, 0.065, 0.05), sides=7, rings=4,
                          jitter=lambda pi, si, aa, i=i: 1 + 0.2 * hash01(pi, si, i) - 0.1, smooth=False))
moss.paint(lambda p, n, fc, fi: shade(mix(C_MOSS0, C_MOSS1, smoothstep(0.0, 0.07, p.z)), 1 + 0.15 * (hash01(fc.x, fc.y) - 0.5)),
           per_face=True)
sprigs = Geo()
for i in range(4):
    a = 2 * math.pi * i / 4 + 0.9
    b = Vector((math.cos(a) * 0.17, math.sin(a) * 0.17, 0.03))
    sprigs.extend(blade(b, Vector((math.cos(a), math.sin(a), 0)), 0.11, 0.014, bend=0.5))
sprigs.paint(grad_z(C_MOSS0, C_MOSS1, 0.02, 0.13))
part("Stem", clamp_ground(merge(stem, moss, sprigs)), root, (0, 0, 0))

# ---------------- cap: wavy-rim dome, tilted slightly forward, glowing spots and gills
cap_c = Vector((-0.06, 0, TOP - 0.01))
R = 0.255
cprof = [(0.0, 0.25), (0.09, 0.235), (0.17, 0.19), (0.225, 0.12), (R, 0.04), (R - 0.008, 0.0), (0.215, -0.015)]
def wave(pi, si, a):
    return 1.0 + (0.05 * math.sin(a * 7) if pi >= 4 else 0.0)
cap = lathe(cprof, sides=20, jitter=wave, cap_bottom=False, cap_top=False)
def cap_paint(p, n, fc, fi):
    t = smoothstep(0.03, 0.24, p.z)
    c = mix(C_CAP1, C_CAP0, t ** 0.7)         # deep teal crown, lighter toward the rim
    if n.z < -0.2:
        return C_GILL0
    rim = 1.0 - smoothstep(0.0, 0.035, p.z)  # thin glowing rim band
    return glow(mix(c, hexc("#7DF7EA"), rim * 0.8), 0.0 + 0.7 * rim)
cap.paint(cap_paint)
gills = Geo()
under = lathe([(0.215, -0.015), (0.15, -0.035), (0.09, -0.045), (0.06, -0.04)], sides=20, jitter=wave,
              cap_bottom=False, cap_top=False)
for i in range(len(under.f)):
    under.f[i] = under.f[i][::-1]
under.paint(lambda p, n, fc, fi: mix(C_GILL1, C_GILL0, smoothstep(0.05, 0.24, Vector((p.x, p.y, 0)).length)))
gills.extend(under)
for i in range(14):
    a = 2 * math.pi * i / 14
    d = Vector((math.cos(a), math.sin(a), 0))
    g = tube([d * 0.08 + Vector((0, 0, -0.04)), d * 0.21 + Vector((0, 0, -0.012))], [0.012, 0.006], sides=3, smooth=False)
    gills.extend(g.fill(C_GILL1))
spots = Geo()
for k, (rr, ang, sz) in enumerate(((0.02, 0, 0.04), (0.11, 30, 0.032), (0.13, 150, 0.036), (0.12, 260, 0.03),
                                   (0.19, 90, 0.027), (0.19, 200, 0.026), (0.18, 320, 0.03), (0.07, 210, 0.024))):
    a = math.radians(ang)
    # height on the dome profile at radius rr
    zs = cprof[0][1]
    for j in range(len(cprof) - 1):
        r0, z0 = cprof[j]; r1, z1 = cprof[j + 1]
        if r0 <= rr <= r1 and r1 > r0:
            zs = z0 + (z1 - z0) * (rr - r0) / (r1 - r0)
    zs += 0.004
    pos = Vector((math.cos(a) * rr, math.sin(a) * rr, zs))
    nrm = Vector((pos.x * 1.6, pos.y * 1.6, 0.25)).normalized()
    s = ellipsoid((0, 0, 0), (sz, sz, sz * 0.35), sides=8, rings=3).fill(C_SPOT)
    s.transform(T(*pos) @ align(nrm))
    spots.extend(s)
capg = merge(cap, gills, spots)
capg.transform(T(*cap_c) @ rot(0, 8, 0))
part("Cap", capg, root, cap_c)

# ---------------- babies: two tiny glowcaps at the base
babies = Geo()
for (bx, by, h, r, tilt) in ((-0.2, 0.17, 0.13, 0.075, -12), (0.13, -0.21, 0.1, 0.06, 15)):
    st = lathe([(0, 0), (0.028, 0), (0.024, h * 0.6), (0.018, h), (0, h)], sides=8)
    st.paint(lambda p, n, fc, fi: mix(C_STEM0, C_STEM1, smoothstep(0.0, 0.08, p.z)))
    cp = lathe([(0, r * 0.75), (r * 0.6, r * 0.62), (r, r * 0.2), (r * 0.9, 0.0), (0.0, 0.0)], sides=10)
    cp.paint(lambda p, n, fc, fi: C_GILL1 if n.z < -0.3 else glow(mix(C_CAP0, C_CAP1, smoothstep(0, r * 0.7, p.z)), 0.45))
    cp.transform(T(0, 0, h - 0.01))
    b = merge(st, cp)
    b.transform(T(bx, by, 0.0) @ rot(tilt, 0, 0))
    babies.extend(b)
part("Babies", clamp_ground(babies), root, (0, 0, 0))

finish(root, "Defenders", icon=True)
