"""Verdant Siege Blender toolkit (Blender 5.2, bpy + bmesh). Original, project-owned art pipeline.

Conventions (contract with the Unity side, see Art/ART_BIBLE.md):
  * metres, Z up, every model root is an EMPTY at (0,0,0) = ground centre of the footprint.
  * defenders face +X, enemies face -X, projectiles fly along +X.
  * one face-corner BYTE colour attribute "Col" (sRGB); ALPHA = emission mask (1 = self-lit).
  * one material "VS_Vertex" on every mesh. Animatable parts have their origin at the joint.

Geometry is authored in model space as `Geo` (python lists: verts, faces, per-corner colours, smooth flags),
then turned into Blender objects with `part()`. Colour is painted with functions of position/normal so
gradients (dark base -> light tips) and subtle per-face variation come for free.
"""
import bpy, bmesh, math, os, sys, random, json
from mathutils import Vector, Matrix, Euler, Quaternion, noise

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT_DIR, "Art")


# ---------------------------------------------------------------- colour helpers
def hexc(h, a=0.0):
    """'#RRGGBB' -> (r, g, b, a) sRGB floats. a = emission mask."""
    h = h.lstrip("#")
    return (int(h[0:2], 16) / 255.0, int(h[2:4], 16) / 255.0, int(h[4:6], 16) / 255.0, a)


def mix(c0, c1, t):
    t = max(0.0, min(1.0, t))
    return tuple(c0[i] + (c1[i] - c0[i]) * t for i in range(4))


def shade(c, k):
    """Multiply rgb by k (keeps alpha)."""
    return (min(1, c[0] * k), min(1, c[1] * k), min(1, c[2] * k), c[3])


def glow(c, a=1.0):
    return (c[0], c[1], c[2], a)


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a) if b != a else 0.0))
    return t * t * (3 - 2 * t)


def hash01(*v):
    """Deterministic pseudo random in [0,1) from numbers."""
    s = 0.0
    for i, x in enumerate(v):
        s += x * (12.9898 + 31.7 * i) + 78.233 * i
    return (math.sin(s) * 43758.5453) % 1.0


# ---------------------------------------------------------------- matrices
def rot(x=0.0, y=0.0, z=0.0):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_matrix().to_4x4()


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation(Vector((x, y, z)))


def S(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal(Vector((x, y, z, 1.0)))


def align(d, up=Vector((0, 0, 1))):
    """Rotation taking +Z to direction d."""
    return Vector((0, 0, 1)).rotation_difference(Vector(d).normalized()).to_matrix().to_4x4()


# ---------------------------------------------------------------- geometry container
class Geo:
    def __init__(self):
        self.v = []        # Vector
        self.f = []        # [idx, ...]
        self.c = []        # per face: [rgba per corner] or None (unpainted)
        self.s = []        # per face smooth flag

    def add_face(self, idx, smooth=True, col=None):
        self.f.append(list(idx))
        self.c.append(None if col is None else [col] * len(idx))
        self.s.append(smooth)

    def extend(self, other):
        o = len(self.v)
        self.v += [p.copy() for p in other.v]
        for f, c, s in zip(other.f, other.c, other.s):
            self.f.append([i + o for i in f])
            self.c.append(None if c is None else list(c))
            self.s.append(s)
        return self

    def copy(self):
        return Geo().extend(self)

    def transform(self, m):
        m = Matrix(m)
        self.v = [m @ p for p in self.v]
        if m.determinant() < 0:
            for i, f in enumerate(self.f):
                self.f[i] = f[::-1]
                if self.c[i] is not None:
                    self.c[i] = self.c[i][::-1]
        return self

    def face_normal(self, fi):
        f = self.f[fi]
        n = Vector((0, 0, 0))
        for i in range(len(f)):
            a, b = self.v[f[i]], self.v[f[(i + 1) % len(f)]]
            n.x += (a.y - b.y) * (a.z + b.z)
            n.y += (a.z - b.z) * (a.x + b.x)
            n.z += (a.x - b.x) * (a.y + b.y)
        return n.normalized() if n.length > 1e-12 else Vector((0, 0, 1))

    def face_center(self, fi):
        f = self.f[fi]
        c = Vector((0, 0, 0))
        for i in f:
            c += self.v[i]
        return c / len(f)

    def paint(self, fn, only_unpainted=False, per_face=False):
        """fn(p, n, fc, fi) -> rgba. per_face=True evaluates once at the face centre (faceted colour)."""
        for fi, f in enumerate(self.f):
            if only_unpainted and self.c[fi] is not None:
                continue
            n = self.face_normal(fi)
            fc = self.face_center(fi)
            if per_face:
                col = fn(fc, n, fc, fi)
                self.c[fi] = [col] * len(f)
            else:
                self.c[fi] = [fn(self.v[i], n, fc, fi) for i in f]
        return self

    def fill(self, col):
        return self.paint(lambda p, n, fc, fi: col)

    def set_smooth(self, s):
        self.s = [s] * len(self.f)
        return self

    def tris(self):
        return sum(len(f) - 2 for f in self.f)

    def bbox(self):
        xs = [p.x for p in self.v]; ys = [p.y for p in self.v]; zs = [p.z for p in self.v]
        return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))

    def displace(self, fn):
        """fn(p) -> offset Vector."""
        self.v = [p + fn(p) for p in self.v]
        return self


def merge(*gs):
    out = Geo()
    for g in gs:
        if g is not None:
            out.extend(g)
    return out


def from_bm(bm, smooth=True):
    g = Geo()
    bm.verts.index_update()
    g.v = [v.co.copy() for v in bm.verts]
    for f in bm.faces:
        g.add_face([v.index for v in f.verts], smooth)
    bm.free()
    return g


def to_bm(g):
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in g.v]
    for f in g.f:
        try:
            bm.faces.new([vs[i] for i in f])
        except ValueError:
            pass
    return bm


# ---------------------------------------------------------------- primitives
def lathe(profile, sides=12, jitter=None, twist=0.0, smooth=True, cap_bottom=True, cap_top=True):
    """Revolve [(r, z), ...] (bottom to top) around Z. r == 0 -> pole. jitter(pi, si, angle) -> radius scale."""
    g = Geo()
    rings = []
    for pi, (r, z) in enumerate(profile):
        if r <= 1e-6:
            rings.append([len(g.v)]); g.v.append(Vector((0, 0, z)))
            continue
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides + twist * pi
            rr = r * (jitter(pi, s, a) if jitter else 1.0)
            ring.append(len(g.v)); g.v.append(Vector((rr * math.cos(a), rr * math.sin(a), z)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        A, B = rings[i], rings[i + 1]
        for s in range(sides):
            t = (s + 1) % sides
            if len(A) == 1:
                g.add_face([A[0], B[t], B[s]], smooth)
            elif len(B) == 1:
                g.add_face([A[s], A[t], B[0]], smooth)
            else:
                g.add_face([A[s], A[t], B[t], B[s]], smooth)
    if cap_bottom and len(rings[0]) > 1:
        g.add_face(rings[0][::-1], smooth)
    if cap_top and len(rings[-1]) > 1:
        g.add_face(rings[-1], smooth)
    return g


def ellipsoid(center=(0, 0, 0), radii=(1, 1, 1), sides=12, rings=8, jitter=None, smooth=True):
    prof = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        prof.append((math.cos(a) if 0 < i < rings else 0.0, math.sin(a)))
    g = lathe(prof, sides, jitter=jitter, smooth=smooth)
    g.transform(T(*center) @ S(*radii))
    return g


def sphere(center, r, sides=12, rings=8, **kw):
    return ellipsoid(center, (r, r, r), sides, rings, **kw)


def icorock(center, radii, subdiv=1, seed=0, rough=0.25, smooth=False, flatten_bottom=True):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    g = from_bm(bm, smooth)
    rnd = random.Random(seed)
    off = Vector((rnd.random() * 10, rnd.random() * 10, rnd.random() * 10))
    def d(p):
        k = 1.0 + rough * noise.noise(p * 1.7 + off) + rough * 0.4 * (rnd.random() - 0.5)
        return p * (k - 1.0)
    g.displace(d)
    g.transform(T(*center) @ S(*radii))
    if flatten_bottom:
        cz = center[2]
        for p in g.v:
            if p.z < 0:
                p.z = 0.0
    return g


def box(center, size, bevel=0.0, segments=1, rotm=None, smooth=False):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=bevel, offset_type='OFFSET',
                        segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)
    g = from_bm(bm, smooth)
    m = T(*center)
    if rotm is not None:
        m = m @ rotm
    return g.transform(m)


def _frames(pts):
    """Parallel-transport frames along a polyline. Returns list of (tangent, normal, binormal)."""
    n = len(pts)
    tans = []
    for i in range(n):
        if i == 0:
            t = pts[1] - pts[0]
        elif i == n - 1:
            t = pts[-1] - pts[-2]
        else:
            t = (pts[i + 1] - pts[i - 1])
        tans.append(t.normalized())
    ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    nrm = tans[0].cross(ref).normalized()
    out = []
    for i in range(n):
        if i > 0:
            q = tans[i - 1].rotation_difference(tans[i])
            nrm = (q @ nrm).normalized()
        b = tans[i].cross(nrm).normalized()
        out.append((tans[i], nrm, b))
    return out


def tube(pts, radii, sides=8, cap0=True, cap1=True, round1=False, smooth=True, flat=(1.0, 1.0), twist=0.0):
    """Swept tube along points. radii: list or float. flat=(sx, sy) ellipse scale of the cross section.
    round1: dome the end instead of a flat cap."""
    pts = [Vector(p) for p in pts]
    if not isinstance(radii, (list, tuple)):
        radii = [radii] * len(pts)
    fr = _frames(pts)
    g = Geo()
    rings = []
    for i, p in enumerate(pts):
        t, n, b = fr[i]
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides + twist * i
            off = n * (math.cos(a) * radii[i] * flat[0]) + b * (math.sin(a) * radii[i] * flat[1])
            ring.append(len(g.v)); g.v.append(p + off)
        rings.append(ring)
    if round1:
        t = fr[-1][0]
        tip = len(g.v); g.v.append(pts[-1] + t * radii[-1] * 0.9)
    for i in range(len(rings) - 1):
        A, B = rings[i], rings[i + 1]
        for s in range(sides):
            u = (s + 1) % sides
            g.add_face([A[s], A[u], B[u], B[s]], smooth)
    if cap0:
        g.add_face(rings[0][::-1], smooth)
    if round1:
        R = rings[-1]
        for s in range(sides):
            g.add_face([R[s], R[(s + 1) % sides], tip], smooth)
    elif cap1:
        g.add_face(rings[-1], smooth)
    return g


def bezier(p0, p1, p2, p3=None, n=8):
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)
    out = []
    for i in range(n + 1):
        t = i / n
        if p3 is None:
            out.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2)
        else:
            p3v = Vector(p3)
            out.append((1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t * t * p2 + t ** 3 * p3v)
    return out


def taper(n, r0, r1, power=1.0):
    return [r0 + (r1 - r0) * ((i / (n - 1)) ** power) for i in range(n)]


def leaf(length=0.5, width=0.2, segs=6, across=2, curl=0.15, fold=0.25, thick=0.012,
         tip_power=0.8, droop=0.0, twist=0.0, base_width=0.15, shape=None, smooth=True):
    """A thick leaf/petal blade along +X from the origin, broad face up (+Z). Top and bottom surfaces share
    their rim, the tip is a single vertex, so it is cheap: 4*across*(segs-1) + 4*across + 2*across tris.
    curl: upward bend of the tip; droop: downward arc; fold: V-fold (edge lift); twist: radians at the tip."""
    if shape is None:
        def shape(u):
            return max(base_width, math.sin(math.pi * min(1.0, u) ** tip_power)) if u < 1 else 0.0
    g = Geo()
    cols = 2 * across + 1
    def pos(u, v):
        w = width * 0.5 * shape(u)
        x = u * length
        y = v * w
        z = curl * u * u * length - droop * math.sin(math.pi * u) * length + fold * abs(v) * w
        if twist:
            a = twist * u
            y, z = y * math.cos(a), z + y * math.sin(a)
        return Vector((x, y, z))
    top, bot = [], []
    for i in range(segs):
        u = i / segs
        rt, rb = [], []
        for j in range(cols):
            v = (j - across) / across
            p = pos(u, v)
            tth = thick * (1 - abs(v) ** 2) * (1 - u * 0.7)
            if abs(v) >= 0.999:
                k = len(g.v); g.v.append(p); rt.append(k); rb.append(k)
            else:
                rt.append(len(g.v)); g.v.append(p + Vector((0, 0, tth * 0.5)))
                rb.append(len(g.v)); g.v.append(p - Vector((0, 0, tth * 0.5)))
        top.append(rt); bot.append(rb)
    tip = len(g.v); g.v.append(pos(1.0, 0.0))
    for i in range(segs - 1):
        for j in range(cols - 1):
            g.add_face([top[i][j], top[i + 1][j], top[i + 1][j + 1], top[i][j + 1]], smooth)
            g.add_face([bot[i][j], bot[i][j + 1], bot[i + 1][j + 1], bot[i + 1][j]], smooth)
    L = segs - 1
    for j in range(cols - 1):
        g.add_face([top[L][j], tip, top[L][j + 1]], smooth)
        g.add_face([bot[L][j], bot[L][j + 1], tip], smooth)
        # base cap (only where top/bot differ)
        if top[0][j] != bot[0][j] or top[0][j + 1] != bot[0][j + 1]:
            vs = [top[0][j], top[0][j + 1], bot[0][j + 1], bot[0][j]]
            uniq = []
            for q in vs:
                if q not in uniq:
                    uniq.append(q)
            if len(uniq) >= 3:
                g.add_face(uniq, smooth)
    return g


def cone(base, tip, r, sides=6, smooth=False):
    base, tip = Vector(base), Vector(tip)
    L = (tip - base).length
    g = lathe([(0, 0), (r, 0), (0, L)], sides, smooth=smooth, cap_bottom=False, cap_top=False)
    return g.transform(T(*base) @ align(tip - base))


def blade(base, direction, length, width, bend=0.3, sides=3):
    """Grass blade: thin curved 3-sided spike."""
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    p0 = Vector(base)
    pts = [p0, p0 + Vector((0, 0, length * 0.5)) + d * bend * length * 0.3, p0 + Vector((0, 0, length)) + d * bend * length]
    pts = bezier(pts[0], pts[1], pts[2], n=3)
    return tube(pts, taper(len(pts), width, width * 0.15), sides=sides, cap0=False, cap1=True, smooth=False)


def disc(center, r, sides=10, z_thick=0.02, smooth=False):
    return lathe([(0, -z_thick), (r, -z_thick), (r, 0), (0, 0)], sides, smooth=smooth).transform(T(*center))


# ---------------------------------------------------------------- paint presets
def grad_z(c0, c1, z0, z1, var=0.0, power=1.0):
    def fn(p, n, fc, fi):
        t = smoothstep(z0, z1, p.z) ** power
        c = mix(c0, c1, t)
        if var:
            k = 1.0 + var * (hash01(fc.x, fc.y, fc.z) - 0.5)
            c = shade(c, k)
        return c
    return fn


def grad_axis(c0, c1, a, b, axis=Vector((0, 0, 1)), var=0.0):
    a, b = float(a), float(b)
    axis = Vector(axis).normalized()
    def fn(p, n, fc, fi):
        t = smoothstep(a, b, p.dot(axis))
        c = mix(c0, c1, t)
        if var:
            c = shade(c, 1.0 + var * (hash01(fc.x, fc.y, fc.z) - 0.5))
        return c
    return fn


def flat_var(c, var=0.12, seed=0.0):
    def fn(p, n, fc, fi):
        return shade(c, 1.0 + var * (hash01(fc.x + seed, fc.y, fc.z) - 0.5))
    return fn


def top_tint(base, top, var=0.1, k=0.6):
    """Colour faces pointing up toward `top` (moss, sunlit), others base."""
    def fn(p, n, fc, fi):
        t = smoothstep(k - 0.3, k + 0.2, n.z)
        return shade(mix(base, top, t), 1.0 + var * (hash01(fc.x, fc.y, fc.z) - 0.5))
    return fn


def ao_ground(fn, z0=0.0, z1=0.12, k=0.55):
    """Darken near the ground (baked contact occlusion)."""
    def f(p, n, fc, fi):
        c = fn(p, n, fc, fi)
        t = smoothstep(z0, z1, p.z)
        return shade(c, k + (1 - k) * t) if c[3] < 0.5 else c
    return f


# ---------------------------------------------------------------- scene + objects
_MAT = None


def new_scene(name):
    global _MAT
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    _MAT = vertex_material()
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 0.3
    sc.collection.objects.link(root)
    return root


def vertex_material():
    """VS_Vertex: vertex colour base, alpha -> emission (preview only; Unity replaces it)."""
    m = bpy.data.materials.new("VS_Vertex")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "Col"
    attr.attribute_type = 'GEOMETRY'
    em = nt.nodes.new("ShaderNodeMath"); em.operation = 'MULTIPLY'; em.inputs[1].default_value = 3.0
    nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(attr.outputs["Color"], bsdf.inputs["Emission Color"])
    nt.links.new(attr.outputs["Alpha"], em.inputs[0])
    nt.links.new(em.outputs[0], bsdf.inputs["Emission Strength"])
    bsdf.inputs["Roughness"].default_value = 0.62
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25
    # Debug: back faces render loud magenta so flipped winding is visible in every preview (Unity culls them).
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    bad = nt.nodes.new("ShaderNodeEmission"); bad.inputs[0].default_value = (1, 0, 1, 1); bad.inputs[1].default_value = 2.0
    mixs = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mixs.inputs[0])
    nt.links.new(bsdf.outputs[0], mixs.inputs[1])
    nt.links.new(bad.outputs[0], mixs.inputs[2])
    nt.links.new(mixs.outputs[0], out.inputs[0])
    return m


def part(name, geo, root, pivot=(0, 0, 0), parent=None, sharp_angle=35.0):
    """Create a mesh object `name` from model-space geo, origin at `pivot` (model space)."""
    pv = Vector(pivot)
    g = geo.copy().transform(T(*(-pv)))
    for i, c in enumerate(g.c):
        if c is None:
            g.c[i] = [(1, 0, 1, 0)] * len(g.f[i])   # loud magenta = unpainted (caught by checks)
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(p) for p in g.v], [], g.f)
    me.update()
    if len(me.polygons) != len(g.f):
        raise RuntimeError(f"{name}: {len(g.f) - len(me.polygons)} faces lost in from_pydata")
    attr = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    li = 0
    for pi, poly in enumerate(me.polygons):
        cols = g.c[pi]
        for k in range(poly.loop_total):
            attr.data[poly.loop_start + k].color_srgb = cols[k]
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = 0
    me.polygons.foreach_set("use_smooth", [bool(s) for s in g.s])
    if sharp_angle:
        me.set_sharp_from_angle(angle=math.radians(sharp_angle))
    me.materials.append(_MAT)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    par = parent if parent is not None else root
    ob.parent = par
    pw = par.matrix_world.translation if par is not None else Vector((0, 0, 0))
    ob.location = pv - _model_pivot(par)
    return ob


def _model_pivot(ob):
    """Model-space pivot of an object = sum of locations up the chain (we never rotate/scale)."""
    p = Vector((0, 0, 0))
    while ob is not None:
        p += ob.location
        ob = ob.parent
    return p


def marker(name, root, pos, parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'SPHERE'
    e.empty_display_size = 0.04
    bpy.context.scene.collection.objects.link(e)
    par = parent if parent is not None else root
    e.parent = par
    e.location = Vector(pos) - _model_pivot(par)
    return e


# ---------------------------------------------------------------- specs / checks
SPECS = json.load(open(os.path.join(os.path.dirname(__file__), "model_specs.json")))


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def model_objects(root):
    out = []
    def rec(o):
        out.append(o)
        for c in o.children:
            rec(c)
    rec(root)
    return out


def world_bbox(obs):
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for ob in obs:
        if ob.type != 'MESH':
            continue
        for v in ob.data.vertices:
            w = ob.matrix_world @ v.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
    return lo, hi


def check(root, spec):
    bpy.context.view_layer.update()
    errors = []
    obs = model_objects(root)
    names = {o.name for o in obs}
    for n in spec.get("parts", []):
        if n not in names:
            errors.append("missing part " + n)
    tris = sum(tri_count(o) for o in obs if o.type == 'MESH')
    if tris > spec["tris"]:
        errors.append(f"{tris} tris > budget {spec['tris']}")
    lo, hi = world_bbox(obs)
    size = hi - lo
    mx = spec.get("max", [99, 99, 99])
    for i, ax in enumerate("XYZ"):
        if size[i] > mx[i] + 1e-4:
            errors.append(f"size {ax} {size[i]:.3f} > {mx[i]}")
    if root.location.length > 1e-6:
        errors.append("root not at origin")
    if spec.get("grounded", True) and lo.z < -0.02:
        errors.append(f"geometry below ground ({lo.z:.3f})")
    if spec.get("grounded", True) and lo.z > 0.03:
        errors.append(f"model floats above ground ({lo.z:.3f})")
    for o in obs:
        if o.type == 'MESH':
            if tuple(o.scale) != (1, 1, 1) or tuple(o.rotation_euler) != (0, 0, 0):
                errors.append(o.name + " has rotation/scale")
            a = o.data.color_attributes.get("Col")
            if a is None:
                errors.append(o.name + " has no Col attribute")
            else:
                for d in a.data:
                    c = d.color_srgb
                    if c[0] > 0.99 and c[1] < 0.01 and c[2] > 0.99:
                        errors.append(o.name + " has unpainted faces"); break
            if len(o.data.materials) != 1 or o.data.materials[0].name != "VS_Vertex":
                errors.append(o.name + " material must be VS_Vertex")
    facing = spec.get("facing")
    if facing:
        # front (glow / muzzle) features should sit on the facing side: compare centroid of emissive verts.
        pass
    return errors, tris, size


def finish(root, category, previews=True, icon=False, view=None):
    name = root.name
    spec = SPECS[name]
    errors, tris, size = check(root, spec)
    print(f"[VS] {name:16s} {tris:5d} tris  size X{size.x:.2f} Y{size.y:.2f} Z{size.z:.2f}")
    for o in model_objects(root)[1:]:
        print(f"      - {o.name:14s} {o.type:5s} {tri_count(o) if o.type == 'MESH' else 0:5d}  origin {tuple(round(x, 3) for x in _model_pivot(o))}")
    if errors:
        raise RuntimeError(f"[VS] {name} self-check failed:\n  " + "\n  ".join(errors))
    out = os.path.join(ART, "Models", category)
    os.makedirs(out, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in model_objects(root):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out, name + ".fbx"), use_selection=True, object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=True, mesh_smooth_type='FACE', colors_type='SRGB', prioritize_active_color=True,
        use_triangles=True, add_leaf_bones=False, bake_anim=False, use_custom_props=False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, name + ".blend"), compress=True)
    if previews and not os.environ.get("VS_NO_RENDER"):
        render(root, spec, os.path.join(ART, "Previews", name + ".png"), 512, transparent=False, view=view)
        if os.environ.get("VS_GAMEVIEW"):
            # roughly the battle camera: elevated ~50 deg, looking across the lanes (from -Y)
            render(root, spec, os.path.join(ART, "Previews", "_game", name + "_game.png"), 384, view=(90.0, 48.0))
        if icon:
            render(root, spec, os.path.join(ART, "Icons", name + "_icon.png"), 256, transparent=True, view=view, tight=True)
    return tris


# ---------------------------------------------------------------- rendering
def _setup_render(res, transparent):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = int(os.environ.get("VS_SAMPLES", "32"))
    sc.cycles.use_denoising = True
    try:
        sc.cycles.denoiser = 'OPENIMAGEDENOISE'
    except Exception:
        pass
    sc.cycles.max_bounces = 4
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA' if transparent else 'RGB'
    sc.view_settings.view_transform = 'Standard'   # closest to the game's stylised shader
    w = bpy.data.worlds.get("VSWorld") or bpy.data.worlds.new("VSWorld")
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.78, 0.84, 0.86, 1)
    bg.inputs[1].default_value = 0.65
    sc.world = w
    if "VSSun" not in bpy.data.objects:
        sun = bpy.data.lights.new("VSSun", 'SUN'); sun.energy = 3.2; sun.angle = math.radians(8)
        sun.color = (1.0, 0.95, 0.86)
        so = bpy.data.objects.new("VSSun", sun); sc.collection.objects.link(so)
        so.rotation_euler = Euler((math.radians(48), math.radians(12), math.radians(-35)))
        fill = bpy.data.lights.new("VSFill", 'AREA'); fill.energy = 120; fill.size = 4; fill.color = (0.8, 0.88, 1.0)
        fo = bpy.data.objects.new("VSFill", fill); sc.collection.objects.link(fo)
        fo.location = (-3, -4, 3); fo.rotation_euler = (Vector((0, 0, 0.6)) - Vector(fo.location)).to_track_quat('-Z', 'Y').to_euler()
    if not transparent and "VSFloor" not in bpy.data.objects:
        me = bpy.data.meshes.new("VSFloor")
        r = 40
        me.from_pydata([(-r, -r, 0), (r, -r, 0), (r, r, 0), (-r, r, 0)], [], [(0, 1, 2, 3)])
        fl = bpy.data.objects.new("VSFloor", me); sc.collection.objects.link(fl)
        fm = bpy.data.materials.new("VSFloorMat"); fm.use_nodes = True
        fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.62, 0.68, 0.62, 1)
        fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
        me.materials.append(fm)
    fl = bpy.data.objects.get("VSFloor")
    if fl:
        fl.hide_render = transparent
    return sc


def render(root, spec, path, res, transparent=False, view=None, tight=False):
    sc = _setup_render(res, transparent)
    obs = model_objects(root)
    lo, hi = world_bbox(obs)
    ctr = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.1)
    facing = spec.get("facing", "+X")
    if view is None:
        az = {"+X": 35.0, "-X": 145.0}.get(facing, 35.0)   # degrees around Z, camera toward the model front
        el = 24.0
    else:
        az, el = view
    camd = bpy.data.cameras.get("VSCam") or bpy.data.cameras.new("VSCam")
    cam = bpy.data.objects.get("VSCam") or bpy.data.objects.new("VSCam", camd)
    if cam.name not in sc.collection.objects:
        sc.collection.objects.link(cam)
    camd.lens = 60
    fov = 2 * math.atan(18 / camd.lens)
    dist = radius / math.sin(fov / 2) * (0.92 if tight else 1.12)
    # az measured from +X toward -Y (camera in front-left for +X facing like a 3/4 view)
    d = Vector((math.cos(math.radians(el)) * math.cos(math.radians(az)),
                -math.cos(math.radians(el)) * math.sin(math.radians(az)),
                math.sin(math.radians(el))))
    cam.location = ctr + d * dist
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    camd.clip_end = 200
    sc.camera = cam
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("[VS] rendered", path)


def argv():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def clamp_ground(g, z=0.0):
    for p in g.v:
        if p.z < z:
            p.z = z
    return g


def basis(xdir, zdir, origin=(0, 0, 0)):
    """Matrix mapping local +X to xdir and local +Z as close as possible to zdir (orthonormalised)."""
    x = Vector(xdir).normalized()
    z = Vector(zdir)
    z = (z - x * z.dot(x)).normalized()
    y = z.cross(x)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def shell(az, span, e0, e1, radii, thick=0.04, nu=4, nv=4, center=(0, 0, 0), point=1.6, bulge=0.0,
          lift=0.0, smooth=False, jit=0.0, seed=0):
    """A thick curved scale/plate lying on an ellipsoid of `radii` around `center`.
    az: centre azimuth (deg, 0 = +X); span: angular width (deg) at the base; e0..e1 elevation (deg).
    The width narrows toward the top with exponent `point` (petal-shaped). bulge pushes the middle outward;
    lift tilts the plate tip away from the body (in metres at the top)."""
    rnd = random.Random(seed)
    cx = Vector(center)
    rx, ry, rz = radii
    def surf(u, v, off):
        # u 0..1 across, v 0..1 bottom->top
        w = (1 - v ** point) if point > 0 else 1.0
        w = max(w, 0.04)
        a = math.radians(az + (u - 0.5) * span * w)
        e = math.radians(e0 + (e1 - e0) * v)
        k = 1.0 + bulge * math.sin(math.pi * u) * math.sin(math.pi * min(1.0, v * 1.1)) + lift * v * v + off
        n = Vector((math.cos(e) * math.cos(a), math.cos(e) * math.sin(a), math.sin(e)))
        p = Vector((n.x * rx * k, n.y * ry * k, n.z * rz * k))
        if jit:
            p += Vector((rnd.uniform(-jit, jit), rnd.uniform(-jit, jit), rnd.uniform(-jit, jit)))
        return cx + p
    g = Geo()
    outer = [[None] * (nu + 1) for _ in range(nv + 1)]
    inner = [[None] * (nu + 1) for _ in range(nv + 1)]
    rmean = (rx + ry + rz) / 3
    for j in range(nv + 1):
        for i in range(nu + 1):
            outer[j][i] = len(g.v); g.v.append(surf(i / nu, j / nv, 0.0))
            inner[j][i] = len(g.v); g.v.append(surf(i / nu, j / nv, -thick / rmean))
    for j in range(nv):
        for i in range(nu):
            g.add_face([outer[j][i], outer[j][i + 1], outer[j + 1][i + 1], outer[j + 1][i]], smooth)
            g.add_face([inner[j][i], inner[j + 1][i], inner[j + 1][i + 1], inner[j][i + 1]], smooth)
    for j in range(nv):
        g.add_face([outer[j][0], outer[j + 1][0], inner[j + 1][0], inner[j][0]], smooth)
        g.add_face([outer[j][nu], inner[j][nu], inner[j + 1][nu], outer[j + 1][nu]], smooth)
    for i in range(nu):
        g.add_face([outer[0][i], inner[0][i], inner[0][i + 1], outer[0][i + 1]], smooth)
        g.add_face([outer[nv][i], outer[nv][i + 1], inner[nv][i + 1], inner[nv][i]], smooth)
    return g
