"""顶点色模型预览（Blender 里运行）：Blender -b -P Tools/render_vc.py -- <输出.png> <方位角度> <俯角度> <a.fbx> [b.fbx ...]
材质：Color Attribute → 原理化 BSDF（略带光泽），柔和天光 + 主光，EEVEE，背面剔除（漏面会透出背景）。"""
import bpy, math, sys
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]
out, az, el, files = argv[0], float(argv[1]), float(argv[2]), argv[3:]
bpy.ops.wm.read_factory_settings(use_empty=True)
for f in files:
    (bpy.ops.import_scene.gltf if f.endswith('.glb') else bpy.ops.import_scene.fbx)(filepath=f)
mat = bpy.data.materials.new('vc'); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes['Principled BSDF']
ca = nt.nodes.new('ShaderNodeVertexColor')
nt.links.new(ca.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = 0.35
mat.use_backface_culling = True
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in objs:
    if o.data.color_attributes and not any(m and m.use_nodes and any(n.type == 'TEX_IMAGE' for n in m.node_tree.nodes) for m in o.data.materials):
        o.data.materials.clear(); o.data.materials.append(mat)
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in objs:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
ctr = (mn + mx) / 2; size = (mx - mn).length
cam = bpy.data.cameras.new('c'); cam.lens = 50
co = bpy.data.objects.new('c', cam); bpy.context.scene.collection.objects.link(co)
a, e = math.radians(az), math.radians(el)
co.location = ctr + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * size * 1.35
co.rotation_euler = (ctr - co.location).to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.camera = co
sun = bpy.data.lights.new('s', 'SUN'); sun.energy = 1.6; so = bpy.data.objects.new('s', sun); so.rotation_euler = (math.radians(50), 0, math.radians(30)); bpy.context.scene.collection.objects.link(so)
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.93, 0.9, 0.96, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.55
bpy.context.scene.world = w
r = bpy.context.scene.render; r.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [i.identifier for i in r.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
r.resolution_x, r.resolution_y = 1400, 900; r.filepath = out
bpy.context.scene.view_settings.view_transform = 'Standard'
bpy.ops.render.render(write_still=True)
