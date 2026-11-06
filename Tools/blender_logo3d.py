"""3D 糖果 Logo（Blender 里运行）：Blender -b -P Tools/blender_logo3d.py -- <输出目录>
读 ArtSource/UI/logo_contours.json（Tools/logo_contours.py 从 nano banana 的 Logo 图提取的外轮廓），每个连通块挤出成有厚度的糖果牌：
  宽度归一为 1（Unity 里再缩放），厚 0.07；前后边缘各倒 0.012 的圆角（3 段）；
  UV = 平面投影（正面就是原图；侧面取到轮廓边缘的像素颜色，拉伸成实心糖果厚边）；
  正面朝 Unity -Z（玩家看 Logo 时沿 +Z 方向看过去）。大平面用平整法线（三角化后的细长三角形不出条纹）。
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_a = sys.argv[sys.argv.index('--') + 1:]
SUF = _a[1] if len(_a) > 1 else ''          # '_EN' = 英文版标题字
D = json.load(open(os.path.join(ROOT, f'ArtSource/UI/logo_contours{SUF}.json')))
W, H = D['w'], D['h']
S = 1.0 / W
DEPTH, BEV = 0.07, 0.012


def piece(poly):
    pts = [((x - W / 2) * S, (H / 2 - y) * S) for x, y in poly]
    area = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    if area < 0: pts = pts[::-1]
    bm = bmesh.new()
    front = [bm.verts.new(U(x, y, 0)) for x, y in pts]           # 正面 z = 0
    back = [bm.verts.new(U(x, y, DEPTH)) for x, y in pts]        # 背面 z = +DEPTH
    bm.faces.new(front); bm.faces.new(back[::-1])
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n; bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rim = [e for e in bm.edges if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle(0)) > 60]
    bmesh.ops.bevel(bm, geom=rim, offset=min(BEV, math.sqrt(abs(area)) * 0.08), offset_type='OFFSET', segments=3, profile=0.5, affect='EDGES', clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def flat_normals(me, min_area=2e-4, smooth_deg=60):
    polys = me.polygons; groups = {}
    for p in polys:
        nn = p.normal; d = nn.dot(p.center)
        groups.setdefault((round(nn.x, 2), round(nn.y, 2), round(nn.z, 2), round(d / 0.001)), []).append(p.index)
    flat = [False] * len(polys)
    for idx in groups.values():
        if len(idx) > 2 and sum(polys[i].area for i in idx) > min_area:
            for i in idx: flat[i] = True
    vf = [[] for _ in me.vertices]
    for p in polys:
        if not flat[p.index]:
            for vi in p.vertices: vf[vi].append(p.index)
    cosl = math.cos(math.radians(smooth_deg)); out = [None] * len(me.loops)
    for p in polys:
        n0 = p.normal
        for li in p.loop_indices:
            if flat[p.index]: out[li] = n0.copy(); continue
            acc = Vector()
            for fi in vf[me.loops[li].vertex_index]:
                q = polys[fi]
                if q.normal.dot(n0) > cosl: acc += q.normal * q.area
            out[li] = acc.normalized() if acc.length > 0 else n0.copy()
    for p in polys: p.use_smooth = True
    me.normals_split_custom_set(out)


def main_pieces():
    """错位标题用：每个连通块（一个字块 / 一个词块 + 它自己的影子层，或者小星星）单独一个物体，按 x 从左到右编号 Piece_00..；
    原点都在整幅字标中心（Unity 里只需要沿 z 前后错开、各自浮动）。"""
    P.reset()
    pieces = sorted(D['polys'], key=lambda poly: sum(x for x, y in poly) / len(poly))
    obs = []
    for i, poly in enumerate(pieces):
        bm = piece(poly)
        uv = bm.loops.layers.uv.new('UVMap')
        for f in bm.faces:
            for l in f.loops:
                l[uv].uv = (-l.vert.co.x / (W * S) + 0.5, l.vert.co.z / (H * S) + 0.5)
        ob = P.to_obj(bm, f'Piece_{i:02d}', smooth=True)
        bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
        flat_normals(ob.data); obs.append(ob)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs: ob.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'Logo3D{SUF}.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True)
    print('LOGO', json.dumps(P.report), 'aspect', H / W, 'pieces', len(obs))


def main():
    if 'pieces' in sys.argv[sys.argv.index('--') + 1:]: return main_pieces()
    P.reset()
    bm = bmesh.new()
    for poly in D['polys']:
        P.merge(bm, piece(poly))
    # 平面投影 UV（Blender 坐标：x = -cindervale x，z = Unity y）
    uv = bm.loops.layers.uv.new('UVMap')
    for f in bm.faces:
        for l in f.loops:
            ux = -l.vert.co.x; uy = l.vert.co.z
            l[uv].uv = (ux / (W * S) + 0.5, uy / (H * S) + 0.5)
    ob = P.to_obj(bm, 'Logo3D' + SUF, smooth=True)
    bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
    flat_normals(ob.data)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'Logo3D{SUF}.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True)
    print('LOGO', json.dumps(P.report), 'aspect', H / W)


if __name__ == '__main__':
    main()
