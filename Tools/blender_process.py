"""Blender 清理导出（在 Blender 里运行）：
  Blender -b -P Tools/blender_process.py -- <model.glb> <输出目录> <名称> <目标高度米> [lod0面数上限]

做的事：
  1. 导入 GLB，合并为一个物体，应用变换
  2. 清理：合并重复顶点、删除面数 < 总面数 0.15% 的漂浮碎块（保留有意的独立部件）、统一法线朝外
  3. 摆正：底部落在 y=0、XZ 居中，统一缩放到目标高度（Unity 单位米）
  4. LOD：lod0（原始或限制面数）、lod1（50%）、lod2（20%），用 Decimate Collapse 保持 UV
  5. 贴图：导出时把贴图解出为 PNG，记录分辨率与锐度（拉普拉斯方差，太低说明糊）
  6. 导出 FBX（Unity 坐标：-Z forward, Y up，单位米，不带灯光相机）
  7. 输出 process.json 报告
"""
import bpy, bmesh, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from normals_util import orient_outward
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
src, out_dir, name, target_h = argv[0], argv[1], argv[2], float(argv[3])
lod0_cap = int(argv[4]) if len(argv) > 4 else 60000
os.makedirs(out_dir, exist_ok=True)


PARTS = []   # 分部件模型（blender_repair_parts.py 输出，物体名 PART_xxx）：不合并，逐个处理


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if any(o.name.startswith('PART_') for o in meshes):
        return load_parts(meshes)
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    # 去掉父级空物体，应用变换
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o != obj:
            bpy.data.objects.remove(o, do_unlink=True)
    obj.name = name
    return obj


def post_decimate_clean(o):
    """减面后：焊接、补小洞、三角化、按奇偶统一朝外"""
    b = bmesh.new(); b.from_mesh(o.data)
    sz = max(o.dimensions) or 1.0
    for _ in range(6):
        bmesh.ops.remove_doubles(b, verts=b.verts, dist=sz * 1e-5)
        bmesh.ops.triangulate(b, faces=b.faces)
        # 细长/零面积三角形：FBX 导出端会丢掉 → 先塌最短边
        sl = []
        for f in b.faces:
            if f.calc_area() < sz * sz * 1e-9: sl.append(f); continue
            vs = [v.co for v in f.verts]
            for i in range(3):
                a_ = vs[(i + 1) % 3] - vs[i]; c_ = vs[(i + 2) % 3] - vs[i]
                if a_.length < 1e-12 or c_.length < 1e-12 or a_.angle(c_) < 0.0087: sl.append(f); break
        if sl:
            es = list({min(f.edges, key=lambda e: e.calc_length()) for f in sl})
            bmesh.ops.collapse(b, edges=[e for e in es if e.is_valid], uvs=True)
        nm = [e for e in b.edges if len(e.link_faces) > 2]
        if nm:
            bmesh.ops.delete(b, geom=list({f for e in nm for v in e.verts for f in v.link_faces}), context='FACES')
        lv = [v for v in b.verts if not v.link_faces]
        if lv: bmesh.ops.delete(b, geom=lv, context='VERTS')
        bd = [e for e in b.edges if len(e.link_faces) == 1]
        if bd: bmesh.ops.holes_fill(b, edges=bd, sides=12)   # 只补小孔；大孔补出来是大片薄面（碎片），留给 crack_fix 处理
        bmesh.ops.triangulate(b, faces=b.faces)
        if not sl and not nm and not [e for e in b.edges if len(e.link_faces) != 2]: break
    orient_outward(b, sz)
    b.to_mesh(o.data); b.free()


def weld(o):
    b = bmesh.new(); b.from_mesh(o.data)
    bmesh.ops.remove_doubles(b, verts=b.verts, dist=(max(o.dimensions) or 1.0) * 1e-5)
    b.to_mesh(o.data); b.free()


def load_parts(meshes):
    # 应用变换、去父级，保持每个部件独立；统一缩放与摆正（按全体包围盒）
    for o in meshes:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH': bpy.data.objects.remove(o, do_unlink=True)
    vs = [v.co for o in meshes for v in o.data.vertices]
    mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    s = target_h / max(mx.z - mn.z, 1e-6); cx, cy = (mn.x + mx.x) / 2, (mn.y + mx.y) / 2
    for o in meshes:
        for v in o.data.vertices:
            v.co = Vector(((v.co.x - cx) * s, (v.co.y - cy) * s, (v.co.z - mn.z) * s))
        o.data.update()
        for p in o.data.polygons: p.use_smooth = True
    for o in meshes: weld(o)
    PARTS.extend(meshes)
    return None


obj = load()
if PARTS:
    report = {'name': name, 'parts': [o.name for o in PARTS]}
    report['faces_in'] = sum(len(o.data.polygons) for o in PARTS)
    report['islands'] = len(PARTS); report['debris_removed_islands'] = 0; report['debris_removed_faces'] = 0
    vs = [v.co for o in PARTS for v in o.data.vertices]
    report['size_m'] = [round(max(v.x for v in vs) - min(v.x for v in vs), 3), round(max(v.y for v in vs) - min(v.y for v in vs), 3), round(target_h, 3)]
    tex_info = []
    for img in bpy.data.images:
        if img.size[0] == 0: continue
        p = os.path.join(out_dir, f'{name}_albedo.png'); img.filepath_raw = p; img.file_format = 'PNG'; img.save(); img.filepath = p
        tex_info.append({'file': os.path.basename(p), 'size': list(img.size), 'laplacian_var': 0})
        break
    report['textures'] = tex_info
    base_faces = report['faces_in']
    lod_faces = []
    for li, ratio in enumerate([1.0, 0.5, 0.2]):
        dups = []
        for o in PARTS:
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.duplicate(); d = bpy.context.view_layer.objects.active
            r = min(1.0, min(base_faces, lod0_cap) * ratio / max(1, base_faces))
            # 小部件（火苗、糖果）面数本来就少：低 LOD 只减到一半，保住轮廓
            if len(d.data.polygons) < 800: r = max(r, 0.5)
            if r < 0.999:
                m = d.modifiers.new('dec', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = r; m.use_collapse_triangulate = True
                bpy.ops.object.modifier_apply(modifier=m.name)
                post_decimate_clean(d)
            d.name = o.name
            dups.append(d)
        bpy.ops.object.select_all(action='DESELECT')
        for d in dups: d.select_set(True)
        bpy.context.view_layer.objects.active = dups[0]
        fp = os.path.join(out_dir, f'{name}_LOD{li}.fbx')
        bpy.ops.export_scene.fbx(filepath=fp, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                 path_mode='STRIP', embed_textures=False, bake_space_transform=True)
        lod_faces.append(sum(len(d.data.polygons) for d in dups))
        for d in dups: bpy.data.objects.remove(d, do_unlink=True)
        for o in PARTS: o.name = o.name   # 复制后原物体名可能被改成 .001，保持
    report['lod_faces'] = lod_faces
    open(os.path.join(out_dir, f'{name}_process.json'), 'w').write(json.dumps(report, ensure_ascii=False, indent=2))
    print('PROCESS', json.dumps(report, ensure_ascii=False))
    sys.exit(0)

report = {'name': name}
bm = bmesh.new()
bm.from_mesh(obj.data)
report['faces_in'] = len(bm.faces)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)

# ---- 碎块清理 ----
bm.faces.ensure_lookup_table()
seen = set(); islands = []
for f in bm.faces:
    if f.index in seen:
        continue
    stack = [f]; seen.add(f.index); comp = []
    while stack:
        g = stack.pop(); comp.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen:
                    seen.add(h.index); stack.append(h)
    islands.append(comp)
nf = len(bm.faces)
thresh = max(12, int(nf * 0.0015))
# 只删“面数少且尺寸极小”的碎屑；面数少但尺寸不小的是有意的独立部件（窗框、栏杆、装饰），保留
_allv = [v.co for v in bm.verts]
_size = max(max(c[i] for c in _allv) - min(c[i] for c in _allv) for i in range(3))
def _tiny(c):
    vs = [v.co for f in c for v in f.verts]
    return max(max(p[i] for p in vs) - min(p[i] for p in vs) for i in range(3)) < _size * 0.03
small = [c for c in islands if len(c) < thresh and _tiny(c)]
kill = [f for c in small for f in c]
report['islands'] = len(islands)
report['debris_removed_islands'] = len(small)
report['debris_removed_faces'] = len(kill)
if kill:
    bmesh.ops.delete(bm, geom=kill, context='FACES')
orient_outward(bm, max(_size, 1e-6))
bm.to_mesh(obj.data)
bm.free()
obj.data.update()

# ---- 摆正 + 缩放 ----
vs = [v.co for v in obj.data.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
# glTF 导入后 Blender 是 Z 向上
h = mx.z - mn.z
s = target_h / max(h, 1e-6)
cx, cy = (mn.x + mx.x) / 2, (mn.y + mx.y) / 2
for v in obj.data.vertices:
    v.co = Vector(((v.co.x - cx) * s, (v.co.y - cy) * s, (v.co.z - mn.z) * s))
obj.data.update()
report['scale'] = round(s, 5)
report['size_m'] = [round((mx.x - mn.x) * s, 3), round((mx.y - mn.y) * s, 3), round(target_h, 3)]

# 平滑着色（P2 输出是硬边多），自动平滑 40°
for p in obj.data.polygons:
    p.use_smooth = True
try:
    bpy.ops.object.shade_auto_smooth(angle=math.radians(40))
except Exception:
    pass

# ---- 贴图解出 + 锐度 ----
tex_info = []
for img in bpy.data.images:
    if img.size[0] == 0:
        continue
    p = os.path.join(out_dir, f'{name}_albedo.png')
    img.filepath_raw = p
    img.file_format = 'PNG'
    img.save()
    w, hh = img.size
    px = list(img.pixels)
    # 拉普拉斯方差（灰度，采样中心 512×512 区域，避免整图太慢）
    def g(x, y):
        i = (y * w + x) * 4
        return 0.299 * px[i] + 0.587 * px[i + 1] + 0.114 * px[i + 2]
    vals = []
    step = max(1, min(w, hh) // 256)
    for y in range(2, hh - 2, step):
        for x in range(2, w - 2, step):
            lap = 4 * g(x, y) - g(x - 1, y) - g(x + 1, y) - g(x, y - 1) - g(x, y + 1)
            vals.append(lap)
    mean = sum(vals) / max(1, len(vals))
    var = sum((v - mean) ** 2 for v in vals) / max(1, len(vals))
    tex_info.append({'file': os.path.basename(p), 'size': [w, hh], 'laplacian_var': round(var * 1e4, 3)})
    # 让材质指向解出的 PNG（FBX 里记录相对路径）
    img.filepath = p
report['textures'] = tex_info

# ---- LOD 导出 ----
base_faces = len(obj.data.polygons)
lods = []
for li, ratio in enumerate([1.0, 0.5, 0.2]):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.duplicate()
    d = bpy.context.view_layer.objects.active
    target = min(base_faces, lod0_cap) * ratio
    r = target / max(1, base_faces)
    if r < 0.999:
        m = d.modifiers.new('dec', 'DECIMATE')
        m.decimate_type = 'COLLAPSE'
        m.ratio = r
        m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=m.name)
        post_decimate_clean(d)
    d.name = f'{name}_LOD{li}'
    lods.append(d)
for d in lods:
    bpy.ops.object.select_all(action='DESELECT')
    d.select_set(True)
    bpy.context.view_layer.objects.active = d
    fp = os.path.join(out_dir, f'{d.name}.fbx')
    bpy.ops.export_scene.fbx(filepath=fp, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                             path_mode='STRIP', embed_textures=False, bake_space_transform=True)
report['lod_faces'] = [len(d.data.polygons) for d in lods]

open(os.path.join(out_dir, f'{name}_process.json'), 'w').write(json.dumps(report, ensure_ascii=False, indent=2))
print('PROCESS', json.dumps(report, ensure_ascii=False))
