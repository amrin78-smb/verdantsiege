"""Emberwild look-dev diorama: imports the exported FBX kit (plus a few existing characters for scale/consistency),
lays out a small 3-lane battlefield and renders it from roughly the battle camera.
Usage: blender -b --factory-startup --python Tools/Blender/diorama_emberwild.py -- [out.png]   (default Art/emberwild_diorama.png)
Preview only: nothing here is exported or used by the game."""
import sys, os, math
import bpy
sys.path.insert(0, os.path.dirname(__file__))
import vskit
from vskit import *

OUT = (argv() or [os.path.join(ART, "emberwild_diorama.png")])[0]
root = new_scene("Diorama")
mat = vertex_material()


def place(category, name, x, y, z=0.0, rz=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, "Models", category, name + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type == 'MESH':
            o.data.materials.clear()
            o.data.materials.append(mat)
    top = [o for o in new if o.parent is None]
    holder = bpy.data.objects.new(name + "_at", None)
    bpy.context.scene.collection.objects.link(holder)
    holder.parent = root
    holder.location = (x, y, z)
    holder.rotation_euler = (0, 0, math.radians(rz))
    for o in top:
        o.parent = holder


for i in range(8):
    for j, y in enumerate((-1, 0, 1)):
        place("Battlefield", "EmberTileA" if (i + j) % 2 == 0 else "EmberTileB", i, y)
place("Battlefield", "EmberVentCell", 4, 0, 0.06)
for i in range(8):
    place("Battlefield", "EmberLaneEdge", i, -1.68)
for i in range(-1, 9):
    place("Battlefield", "EmberLavaChannel", i, 2.0)
place("Battlefield", "Heartroot", -1.5, 0)
place("Defenders", "Sunspore", 0, -1)
place("Defenders", "EmberLily", 1, 0)
place("Defenders", "BulwarkBloom", 2, 1)
place("Enemies", "Drifter", 5.6, -1)
place("Enemies", "PlatedBrute", 6.4, 1)
place("Props", "EmberForge", 2.2, 4.4)
place("Props", "EmberTreeA", -0.6, 3.6)
place("Props", "EmberTreeB", 6.0, 4.2)
place("Props", "EmberTreeA", 8.3, 3.2, 0, 70)
place("Props", "EmberRockB", 4.4, 3.2)
place("Props", "EmberRockA", 7.3, 2.95, 0, 30)
place("Props", "EmberRockC", 0.6, 3.0)
place("Props", "EmberRoots", 4.9, 3.0)
place("Props", "EmberFlowerCluster", 3.4, 3.0)
place("Props", "EmberGrassTuft", 1.5, 2.95)
place("Props", "EmberLavaPool", 1.0, -2.9)
place("Props", "EmberRoots", 5.2, -2.7, 0, 40)
place("Props", "EmberFlowerCluster", 3.0, -2.5)
place("Props", "EmberGrassTuft", 6.8, -2.4)
place("Props", "EmberRockA", 7.8, -2.9, 0, 200)
bpy.context.view_layer.update()
# frame the battlefield (a hidden 2-vertex mesh defines the framing box for vskit.render)
me = bpy.data.meshes.new("Frame"); me.from_pydata([(-1.2, -2.6, 0.0), (8.2, 4.2, 1.2)], [], [])
fr = bpy.data.objects.new("Frame", me); bpy.context.scene.collection.objects.link(fr); fr.hide_render = True
fr_root = bpy.data.objects.new("FrameRoot", None); bpy.context.scene.collection.objects.link(fr_root); fr.parent = fr_root
render(fr_root, {}, OUT, 1280, view=(90.0, 52.0), tight=True)
