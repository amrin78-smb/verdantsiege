"""Branding renders (Blender part). Usage: blender -b --factory-startup --python Tools/Blender/branding.py -- icon|logo
icon -> Art/Branding/_icon_fg_raw.png (+ _icon_meta.json with the heart-seed screen position)
logo -> Art/Branding/_logo_raw.png
Compositing (glow, background, legacy icon, final logo) is done by Tools/Blender/branding_compose.py (PIL)."""
import sys, os, math, json
sys.path.insert(0, os.path.dirname(__file__))
from vskit import *
from bpy_extras.object_utils import world_to_camera_view

OUT = os.path.join(ART, "Branding")
FONT = os.path.join(ROOT_DIR, "Assets", "_Game", "UI", "Fonts", "LilitaOne-Regular.ttf")
os.makedirs(OUT, exist_ok=True)


def append_model(category, name, loc=(0, 0, 0), rot_deg=(0, 0, 0), scale=1.0, only=None):
    """Append a generated model (root + children) from its .blend and place it."""
    path = os.path.join(ART, "Models", category, name + ".blend")
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = list(src.objects)
    objs = [o for o in dst.objects if o is not None]
    for o in objs:
        if o.name.startswith("VS") or o.type not in ('MESH', 'EMPTY'):
            continue
        bpy.context.scene.collection.objects.link(o)
    root = next(o for o in objs if o.name.split(".")[0] == name and o.parent is None)
    if only:
        keep = bpy.data.objects[only] if only in bpy.data.objects else next(o for o in objs if o.name.startswith(only))
        for o in objs:
            if o is not keep and o.type == 'MESH' and keep not in o.children_recursive and o not in keep.children_recursive:
                o.hide_render = True
    root.location = loc
    root.rotation_euler = [math.radians(a) for a in rot_deg]
    root.scale = (scale, scale, scale)
    return root, objs


def lights(sc, strength=1.0):
    w = bpy.data.worlds.new("BrandWorld"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.85, 0.9, 0.88, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.7 * strength
    sc.world = w
    key = bpy.data.lights.new("Key", 'SUN'); key.energy = 3.4 * strength; key.angle = math.radians(10); key.color = (1.0, 0.95, 0.85)
    ko = bpy.data.objects.new("Key", key); sc.collection.objects.link(ko)
    ko.rotation_euler = Euler((math.radians(50), math.radians(-20), math.radians(70)))
    rim = bpy.data.lights.new("Rim", 'AREA'); rim.energy = 300 * strength; rim.size = 3; rim.color = (1.0, 0.85, 0.55)
    ro = bpy.data.objects.new("Rim", rim); sc.collection.objects.link(ro)
    ro.location = (-3, 1.5, 3); ro.rotation_euler = (Vector((0, 0, 0.6)) - Vector(ro.location)).to_track_quat('-Z', 'Y').to_euler()


def setup(res_x, res_y, samples=64):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res_x, res_y
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'
    return sc


def camera(sc, loc, target, ortho=None, lens=50):
    cd = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cd); sc.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        cd.type = 'ORTHO'; cd.ortho_scale = ortho
    else:
        cd.lens = lens
    sc.camera = cam
    return cam


def icon():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = setup(1024, 1024, 96)
    # camera in front of the defenders (they face +X), slightly above
    import vskit as vk
    vk._MAT = vk.vertex_material()
    hroot = bpy.data.objects.new("HeartSeed", None); sc.collection.objects.link(hroot)
    g = Geo()
    seed = ellipsoid((0, 0, 0), (0.24, 0.24, 0.31), sides=16, rings=10, jitter=lambda pi, si, a: 1 + 0.04 * math.sin(a * 6))
    seed.paint(lambda p, n, fc, fi: mix(hexc("#F08A14", 0.3), hexc("#FFE680", 0.55), smoothstep(-0.2, 0.3, p.z + p.x * 0.5)))
    g.extend(seed)
    for k in range(12):
        lf = leaf(length=0.2, width=0.12, segs=4, across=1, curl=0.15, fold=0.12, thick=0.012)
        lf.transform(rot(0, 0, 0) @ Matrix.Rotation(math.radians(30 * k), 4, 'X') @ T(0, 0, 0.3) @ rot(0, -80, 0) @ rot(0, 0, 0))
        g.extend(lf.fill(hexc("#FFD44A", 0.8)))
    part("Seed", g, hroot, (0, 0, 0))
    hroot.location = (-0.45, 0.0, 0.66)
    core = bpy.data.objects["Seed"]
    ss, _ = append_model("Defenders", "Sunspore", loc=(0.0, 0.28, 0), rot_deg=(-15, 0, -40), scale=1.05)
    gc, _ = append_model("Defenders", "Glowcap", loc=(0.05, -0.3, 0), rot_deg=(14, 0, 30), scale=1.15)
    lights(sc)
    target = Vector((0, 0, 0.5))
    cam = camera(sc, (4.2, 0.0, 1.55), target, ortho=1.0)
    # fit: the subject's projected bbox must fit the central 62 % (inside the 66 % safe zone)
    bpy.context.view_layer.update()
    pts = []
    for o in sc.objects:
        if o.type == 'MESH' and not o.hide_render:
            for v in o.data.vertices:
                pts.append(o.matrix_world @ v.co)
    cd = cam.data
    for _ in range(3):
        bpy.context.view_layer.update()
        uv = [world_to_camera_view(sc, cam, p) for p in pts]
        xs = [u.x for u in uv]; ys = [u.y for u in uv]
        w = max(xs) - min(xs); h = max(ys) - min(ys)
        cx = (max(xs) + min(xs)) / 2 - 0.5; cy = (max(ys) + min(ys)) / 2 - 0.5
        cd.ortho_scale *= max(w, h) / 0.62
        right = cam.matrix_world.to_3x3() @ Vector((1, 0, 0)); up = cam.matrix_world.to_3x3() @ Vector((0, 1, 0))
        cam.location += (right * cx + up * cy) * cd.ortho_scale
    bpy.context.view_layer.update()
    hp = world_to_camera_view(sc, cam, core.matrix_world.translation)
    sc.render.filepath = os.path.join(OUT, "_icon_fg_raw.png")
    bpy.ops.render.render(write_still=True)
    json.dump({"heart": [hp.x, 1 - hp.y]}, open(os.path.join(OUT, "_icon_meta.json"), "w"))
    print("[VS] icon rendered, heart at", hp.x, 1 - hp.y)


def text_obj(sc, body, size, loc, mat, extrude, bevel, offset=0.0, name="T"):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.font = bpy.data.fonts.load(FONT)
    cu.size = size
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    cu.extrude = extrude; cu.bevel_depth = bevel; cu.bevel_resolution = 3; cu.offset = offset
    cu.resolution_u = 8
    ob = bpy.data.objects.new(name, cu); sc.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = (math.radians(90), 0, 0)      # text faces -Y (toward the camera)
    cu.materials.append(mat)
    return ob


def gold_material():
    m = bpy.data.materials.new("Gold"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.1; ramp.color_ramp.elements[0].color = (0.78, 0.22, 0.01, 1)
    ramp.color_ramp.elements[1].position = 0.8; ramp.color_ramp.elements[1].color = (1.0, 0.78, 0.12, 1)
    nt.links.new(tc.outputs["Generated"], sep.inputs[0]); nt.links.new(sep.outputs["Y"], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], b.inputs["Base Color"])
    nt.links.new(ramp.outputs[0], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 0.18
    b.inputs["Roughness"].default_value = 0.32
    b.inputs["Metallic"].default_value = 0.25
    return m


def flat_material(name, rgb, emit=0.0, rough=0.5):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*rgb, 1); b.inputs["Emission Strength"].default_value = emit
    return m


def logo():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    global _MAT
    sc = setup(2048, 1024, 96)
    import vskit as vk
    vk._MAT = vk.vertex_material()
    gold = gold_material()
    rim = flat_material("Outline", (0.006, 0.05, 0.018), rough=0.55)
    cream = flat_material("Cream", (1.0, 0.9, 0.55), emit=0.4, rough=0.4)
    # words: VERDANT (big) over SIEGE, gold face, cream thin rim, thick dark-green outline behind
    for word, z, size in (("VERDANT", 0.25, 1.15), ("SIEGE", -0.78, 0.95)):
        text_obj(sc, word, size, (0, 0, z), gold, 0.09, 0.022, name="Face_" + word)
        text_obj(sc, word, size, (0, 0.06, z), cream, 0.06, 0.0, offset=0.025, name="Rim_" + word)
        text_obj(sc, word, size, (0, 0.14, z - 0.025), rim, 0.08, 0.02, offset=0.075, name="Out_" + word)
    # sprout + heart-seed motif above the wordmark (built with the kit, vertex-coloured)
    root = bpy.data.objects.new("Motif", None); sc.collection.objects.link(root)
    g = Geo()
    stem = tube(bezier((0, 0, 0), (0.03, 0, 0.18), (-0.02, 0, 0.32), n=6), taper(7, 0.045, 0.03), sides=10)
    stem.paint(grad_z(hexc("#2F7A2E"), hexc("#8ED64E"), 0, 0.32))
    g.extend(stem)
    for side in (1, -1):
        lf = leaf(length=0.46, width=0.3, segs=8, across=2, curl=0.25, fold=0.15, thick=0.03, tip_power=0.7)
        lf.transform(rot(90, 0, 0))                       # broad face toward the camera (-Y)
        lf.transform(rot(0, -32 if side > 0 else 212, 0))  # up-right / up-left in the screen plane
        lf.transform(T(-0.02, 0, 0.3))
        lf.paint(lambda p, n, fc, fi: mix(hexc("#2E8A34"), hexc("#B8EC5E"), smoothstep(0.3, 0.62, p.z)))
        g.extend(lf)
    seed = ellipsoid((0, 0, 0.0), (0.17, 0.14, 0.2), sides=16, rings=10)
    seed.paint(lambda p, n, fc, fi: mix(hexc("#F59A1C", 0.45), hexc("#FFE680", 0.6), smoothstep(-0.14, 0.2, p.z - p.y * 0.4)))
    seed.transform(T(0, -0.02, -0.08))
    g.extend(seed)
    for k in range(10):
        lf = leaf(length=0.12, width=0.08, segs=3, across=1, curl=0.1, fold=0.1, thick=0.01)
        lf.transform(T(0, -0.02, -0.08) @ rot(0, 36 * k, 0) @ T(0.22, 0, 0) @ rot(90, 0, 0))
        g.extend(lf.fill(hexc("#FFD44A", 0.7)))
    part("Sprout", g, root, (0, 0, 0))
    root.location = (0, -0.05, 1.3); root.scale = (1.3, 1.3, 1.3)
    # Lighting + front orthographic camera
    lights(sc, 1.0)
    cam = camera(sc, (0, -10, 0.42), (0, 0, 0.42), ortho=8.0)
    sc.render.filepath = os.path.join(OUT, "_logo_raw.png")
    bpy.ops.render.render(write_still=True)
    print("[VS] logo rendered")


if __name__ == "__main__":
    what = argv() or ["icon", "logo"]
    if "icon" in what:
        icon()
    if "logo" in what:
        logo()
