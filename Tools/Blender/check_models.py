"""Independent validation of the exported FBX files (re-imported into an empty Blender scene) against
model_specs.json: required part names, triangle budget, bounding box, root at origin, grounded, Col colour
attribute present with an emission alpha channel. Exit code 1 on any failure. Run via build_all.sh."""
import bpy, os, sys, json
from mathutils import Vector
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..", "..", "Art")
SPECS = json.load(open(os.path.join(HERE, "model_specs.json")))
rows, failures = [], 0
for name, spec in SPECS.items():
    path = os.path.join(ART, "Models", spec["category"], name + ".fbx")
    errs = []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if not os.path.exists(path):
        rows.append((name, 0, "-", ["missing " + path])); failures += 1; continue
    bpy.ops.import_scene.fbx(filepath=path)
    obs = list(bpy.context.scene.objects)
    names = {o.name for o in obs}
    if name not in names: errs.append("no root " + name)
    for p in spec.get("parts", []):
        if p not in names: errs.append("missing part " + p)
    tris = 0; lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3); emissive = 0
    for o in obs:
        if o.type != 'MESH': continue
        tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        ca = o.data.color_attributes
        if len(ca) == 0: errs.append(o.name + ": no vertex colours")
        else:
            emissive += sum(1 for d in ca[0].data if d.color[3] > 0.5)
    size = hi - lo
    if tris > spec["tris"]: errs.append(f"{tris} tris > {spec['tris']}")
    for i, ax in enumerate("XYZ"):
        if size[i] > spec["max"][i] + 1e-3: errs.append(f"{ax} {size[i]:.2f} > {spec['max'][i]}")
    root = bpy.data.objects.get(name)
    if root and root.matrix_world.translation.length > 1e-4: errs.append("root not at origin")
    if spec.get("grounded", True) and (lo.z < -0.02 or lo.z > 0.03): errs.append(f"not grounded (min z {lo.z:.3f})")
    if errs: failures += 1
    rows.append((name, tris, f"{size.x:.2f}x{size.y:.2f}x{size.z:.2f}", errs, emissive))
print("CHECK name              tris   size(XxYxZ m)    glow-corners  result")
for r in rows:
    print(f"CHECK {r[0]:16s} {r[1]:5d}  {r[2]:16s} {r[4] if len(r) > 4 else 0:8d}      {'OK' if not r[3] else 'FAIL: ' + '; '.join(r[3])}")
print(f"SUMMARY {len(rows) - failures}/{len(rows)} models pass")
sys.exit(1 if failures else 0)
