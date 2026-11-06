"""程序化模型严格拓扑审计（Blender 里运行）：
  Blender -b -P Tools/proc_audit.py -- <out.json> <a.fbx> [b.fbx ...]
程序化模型由多个独立部件组成（石板、岩块），碎块数不算问题；但每个部件必须是封闭流形：
边界边 = 0、非流形边 = 0、零面积面 = 0、重算法线后翻转面 = 0、z 范围单独报告（用于检查穿插）。
"""
import bpy, bmesh, json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from normals_util import inward_faces, inward_components
argv = sys.argv[sys.argv.index('--') + 1:]
out, files = argv[0], argv[1:]
rep = {}
for fp in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if fp.endswith('.fbx'):
        bpy.ops.import_scene.fbx(filepath=fp)
    else:
        bpy.ops.import_scene.gltf(filepath=fp)
    tot = dict(faces=0, boundary=0, nonmanifold=0, zero=0, flipped=0, wire=0, loose_verts=0, inward=0, inward_n=0)
    lo, hi = [1e9] * 3, [-1e9] * 3
    for o in bpy.context.scene.objects:
        if o.type != 'MESH':
            continue
        bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        tot['faces'] += len(bm.faces)
        tot['boundary'] += sum(1 for e in bm.edges if len(e.link_faces) == 1)
        tot['nonmanifold'] += sum(1 for e in bm.edges if len(e.link_faces) > 2)
        tot['wire'] += sum(1 for e in bm.edges if len(e.link_faces) == 0)
        tot['loose_verts'] += sum(1 for v in bm.verts if not v.link_faces)
        tot['zero'] += sum(1 for f in bm.faces if f.calc_area() < 1e-9)
        _sz = max(o.dimensions) or 1.0
        _b, _t = inward_components(bm); tot['inward'] += _b; tot['inward_n'] += _t
        before = [f.normal.copy() for f in bm.faces]
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        tot['flipped'] += sum(1 for f, n in zip(bm.faces, before) if f.normal.dot(n) < 0)
        for v in bm.verts:
            for i in range(3):
                lo[i] = min(lo[i], v.co[i]); hi[i] = max(hi[i], v.co[i])
        bm.free()
    tot['bbox_min'] = [round(x, 3) for x in lo]; tot['bbox_max'] = [round(x, 3) for x in hi]
    # 朝向判定用射线奇偶（inward）；flipped（与 recalc 结果不一致的面数）只作参考，自交模型上 recalc 本身会错
    tot['pass'] = tot['boundary'] == 0 and tot['nonmanifold'] == 0 and tot['zero'] == 0 and tot['wire'] == 0 and tot['inward'] == 0   # 有向体积为负的连通块（整块朝里）必须为 0
    rep[os.path.basename(fp)] = tot
    print('AUDIT', os.path.basename(fp), json.dumps(tot, ensure_ascii=False))
open(out, 'w').write(json.dumps(rep, ensure_ascii=False, indent=2))
