"""去掉 Boss 模型自带的皇冠（用户：有头冠的 Boss 去掉自己的头冠，统一用 Boss 专属头冠）。
  Blender -b -P Tools/boss_decrown.py -- <lod.fbx> <comp|cut> <z>
  comp：删掉最低点高于 z 的独立部件（皇冠是单独一块）
  cut ：在 z 处水平切开，删上面，切口补成平面（UV 取切口一圈原有的 UV，颜色和头顶接上）
  z 是导入后的世界坐标（P2 模型归一化在 [-0.5, 0.5]）。原文件备份在 ArtSource/BossCrownBackup。"""
import bpy, bmesh, sys
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; fp, mode, zc = a[0], a[1], float(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=fp)
obs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in obs:
    mw = o.matrix_world; inv = mw.inverted()
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    n0 = len(bm.faces)
    if mode == 'comp':
        seen = set(); kill = []
        for v in bm.verts:
            if v.index in seen: continue
            st = [v]; seen.add(v.index); cur = []
            while st:
                x = st.pop(); cur.append(x)
                for e in x.link_edges:
                    y = e.other_vert(x)
                    if y.index not in seen: seen.add(y.index); st.append(y)
            if min((mw @ x.co).z for x in cur) > zc: kill += cur
        bmesh.ops.delete(bm, geom=kill, context='VERTS')
    else:
        p0 = inv @ Vector((0, 0, zc)); nz = (inv.to_3x3() @ Vector((0, 0, 1))).normalized()
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=p0, plane_no=nz, clear_outer=True)
        bnd = [e for e in bm.edges if len(e.link_faces) == 1]
        uv = bm.loops.layers.uv.active
        vuv = {}; bnd_verts_orig = set(v for e in bnd for v in e.verts)
        for e in bnd:
            for v in e.verts:
                for l in v.link_loops: vuv.setdefault(v, l[uv].uv.copy())
        # 切口补成圆顶（平盖在圆脑袋上会很假）：沿边界环向中心收 5 圈、逐圈抬高，UV 沿用边界点的（头顶颜色接上）
        loops = []; left = set(bnd)
        while left:
            e0 = left.pop(); ring = [e0.verts[0], e0.verts[1]]
            while True:
                nxt = [e for e in ring[-1].link_edges if e in left]
                if not nxt: break
                e = nxt[0]; left.discard(e); v = e.other_vert(ring[-1])
                if v == ring[0]: break
                ring.append(v)
            if len(ring) >= 6: loops.append(ring)
        for ring in loops:
            cen = sum((v.co for v in ring), Vector()) / len(ring)
            r = sum((v.co - cen).length for v in ring) / len(ring); h = r * float(a[3]) if len(a) > 3 else r * 0.35
            prev = ring; K = 5
            for k in range(1, K + 1):
                t = k / K
                if k == K:
                    c = bm.verts.new(cen + nz * h)
                    for i in range(len(prev)):
                        f = bm.faces.new((prev[i], prev[(i + 1) % len(prev)], c))
                        for l in f.loops: l[uv].uv = vuv.get(prev[i], vuv[ring[0]]) if l.vert == c else vuv.get(ring[ring.index(l.vert)] if l.vert in ring else l.vert, vuv[ring[0]])
                    break
                cur = [bm.verts.new(cen + (v.co - cen) * (1 - t) + nz * h * (1 - (1 - t) ** 2)) for v in ring]
                for i in range(len(ring)):
                    vuv[cur[i]] = vuv[ring[i]]
                    j = (i + 1) % len(ring); bm.faces.new((prev[i], prev[j], cur[j], cur[i]))
                prev = cur
        for f in bm.faces:
            for l in f.loops:
                if l.vert in vuv and l.vert not in bnd_verts_orig: l[uv].uv = vuv[l.vert]
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bnd2 = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    bm.to_mesh(o.data); bm.free(); o.data.update()
    print('DECROWN', fp.split('/')[-2], fp.split('/')[-1], o.name, 'faces', n0, '->', len(o.data.polygons), 'boundary', bnd2)
bpy.ops.object.select_all(action='DESELECT')
for o in obs: o.select_set(True)
bpy.context.view_layer.objects.active = obs[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # 导入 FBX 带的轴向旋转要烘进网格，否则 Unity 里会侧躺（原 P2 管线导出的就是单位变换）
bpy.ops.export_scene.fbx(filepath=fp, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                         path_mode='STRIP', embed_textures=False, bake_space_transform=True)
# 复查：重新导入看包围盒
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=fp)
for o in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
    ps = [o.matrix_world @ v.co for v in o.data.vertices]
    print('BBOX', fp.split('/')[-1], [round(min(p[i] for p in ps), 3) for i in range(3)], [round(max(p[i] for p in ps), 3) for i in range(3)])
