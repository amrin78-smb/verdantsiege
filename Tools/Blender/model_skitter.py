"""Skitter: fast, fragile rot-beetle running toward -X. Glossy split carapace with magenta glow seams, swollen
spore-sac abdomen, twin orange eyes, bone mandibles, six high-kneed legs (Leg0-2 left/-Y front->back, Leg3-5 right)."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *

C_SHELL0, C_SHELL1 = hexc("#141A2E"), hexc("#2F6A78")
C_SEAM = hexc("#FF4FD2", 1.0)
C_UNDER = hexc("#3B2A3C")
C_SAC0, C_SAC1 = hexc("#8C2E78", 0.2), hexc("#FF7AE0", 0.9)
C_EYE = hexc("#FFA22E", 1.0)
C_BONE = hexc("#E6D5B5")
C_LEG0, C_LEG1 = hexc("#24182A"), hexc("#5A3F5E")

root = new_scene("Skitter")
BC = Vector((0.0, 0, 0.34))     # body centre / origin

# ---------------- body: thorax + split carapace + abdomen + head
thorax = ellipsoid(BC + Vector((-0.04, 0, -0.02)), (0.2, 0.15, 0.1), sides=12, rings=6).fill(C_UNDER)
car = Geo()
for side in (1, -1):
    # each wing case is a shell over the back, slightly lifted, split along the middle
    sh = shell(90 * side, 150, 5, 86, (0.27, 0.2, 0.17), thick=0.025, nu=6, nv=4, center=BC + Vector((0.04, 0, 0.0)),
               point=0.0, bulge=0.04, lift=0.0)
    for p in sh.v:   # squash the azimuth towards the back so the cases cover the length
        pass
    car.extend(sh)
def car_paint(p, n, fc, fi):
    c = mix(C_SHELL0, C_SHELL1, smoothstep(0.3, 0.52, p.z))
    # glossy highlight band
    c = shade(c, 1.0 + 0.35 * smoothstep(0.8, 0.98, n.z))
    return c
car.paint(car_paint, per_face=True)
# glow seams: ribs across the carapace + the central split
seams = Geo()
for k, xo in enumerate((-0.06, 0.12)):
    pts = []
    for i in range(9):
        a = math.radians(-80 + 160 * i / 8)
        r_y, r_z = 0.2 * (1 - (xo / 0.33) ** 2) ** 0.5, 0.17 * (1 - (xo / 0.33) ** 2) ** 0.5
        pts.append(BC + Vector((0.04 + xo, math.sin(a) * (r_y + 0.008), math.cos(a) * (r_z + 0.008))))
    seams.extend(tube(pts, 0.007, sides=4, smooth=False).fill(C_SEAM))
ridge = [BC + Vector((0.04 + x, 0, 0.17 * (1 - (x / 0.28) ** 2) ** 0.5 + 0.012)) for x in [-0.22 + 0.055 * i for i in range(9)]]
seams.extend(tube(ridge, 0.009, sides=4, smooth=False).fill(C_SEAM))
# abdomen: swollen spore sacs at the back (+X)
abd = ellipsoid(BC + Vector((0.25, 0, 0.0)), (0.12, 0.13, 0.11), sides=10, rings=6,
                jitter=lambda pi, si, a: 1 + 0.12 * max(0, math.sin(a * 4)) * (1 if 1 < pi < 5 else 0))
abd.paint(lambda p, n, fc, fi: mix(C_SAC0, C_SAC1, smoothstep(0.0, 1.0, max(0.0, math.sin(math.atan2(p.z - BC.z, p.y) * 4)))))
# head
hc = BC + Vector((-0.24, 0, 0.0))
head = ellipsoid(hc, (0.09, 0.1, 0.075), sides=10, rings=6).paint(lambda p, n, fc, fi: mix(C_SHELL0, C_SHELL1, smoothstep(0.3, 0.42, p.z)))
eyes = Geo()
for side in (1, -1):
    eyes.extend(ellipsoid(hc + Vector((-0.06, 0.05 * side, 0.03)), (0.03, 0.032, 0.028), sides=8, rings=5).fill(C_EYE))
mand = Geo()
for side in (1, -1):
    b = hc + Vector((-0.07, 0.04 * side, -0.035))
    pts = bezier(b, b + Vector((-0.07, 0.03 * side, 0.0)), b + Vector((-0.1, -0.02 * side, -0.01)), n=4)
    mand.extend(tube(pts, taper(len(pts), 0.018, 0.004), sides=4, smooth=False).fill(C_BONE))
# antennae
ant = Geo()
for side in (1, -1):
    b = hc + Vector((-0.05, 0.035 * side, 0.06))
    pts = bezier(b, b + Vector((-0.08, 0.04 * side, 0.12)), b + Vector((-0.04, 0.1 * side, 0.18)), n=4)
    ant.extend(tube(pts, taper(len(pts), 0.009, 0.005), sides=3, smooth=False).fill(C_LEG1))
    ant.extend(sphere(pts[-1], 0.016, sides=5, rings=3).fill(C_SEAM))
part("Body", merge(thorax, car, seams, abd, head, eyes, mand, ant), root, BC)

# ---------------- legs: hip on the body side, knee high, foot on the ground; spiked joints
def legg(i):
    side = -1 if i < 3 else 1
    k = i % 3
    x = (-0.14, -0.02, 0.1)[k]
    hip = BC + Vector((x, 0.12 * side, -0.04))
    knee = Vector((x + (-0.08, 0.0, 0.08)[k], 0.33 * side, 0.5))
    foot = Vector((x + (-0.14, 0.0, 0.14)[k], 0.4 * side, 0.0))
    up = tube([hip, knee], [0.036, 0.026], sides=5, smooth=False)
    dn = tube([knee, foot], [0.026, 0.008], sides=5, smooth=False, round1=False)
    spike = cone(knee, knee + Vector((0, 0.03 * side, 0.07)), 0.018, sides=4)
    g = merge(up, dn, spike)
    g.paint(lambda p, n, fc, fi: mix(C_LEG0, C_LEG1, smoothstep(0.2, 0.5, p.z)))
    tip = cone(foot + Vector((0, 0, 0.05)), foot, 0.012, sides=4).fill(C_BONE)
    knob = sphere(knee, 0.03, sides=6, rings=4).fill(C_SHELL1)
    g = merge(g, tip, knob)
    return g, hip

for i in range(6):
    g, hip = legg(i)
    part(f"Leg{i}", clamp_ground(g), root, hip)

finish(root, "Enemies", icon=True)
