"""房间玩具化后露出的“外部 3D 玩具世界”地形（Blender 里运行）：Blender -b -P Tools/blender_toy_outer.py -- <输出目录>

  TOY_OuterGround  以房间中心 (0, -1) 为圆心、半径 30 m 的绒布地面：房间及附近平坦（略低于真实地板 1cm），
                   从 7 m 起一圈连绵起伏的粉彩绒布山丘（薄荷 / 薰衣草 / 柠檬 / 蜜桃斑块），远处更高；平面 UV（贴玩具草地平铺图）
全部封闭流形，顶点色 sRGB。
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector, noise as mn
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
CX, CZ = 0.0, -1.0


def height(x, z):
    d = math.hypot(x - CX, z - CZ); a = math.atan2(z - CZ, x - CX)
    ring = max(0.0, (d - 9.0) / 7.0)                                       # 9 m 外才起坡，坡度平缓
    h = (ring ** 1.7) * (0.8 + 0.45 * math.sin(a * 3 + 0.6) + 0.3 * math.sin(a * 7 + 2.2) + 0.2 * math.sin(a * 13 + 1.1))
    h += 0.5 * max(0.0, (d - 17) / 8.0) ** 1.3 * (1.3 + math.sin(a * 5 + 0.3))   # 远处的山再高一些，挡住地面边缘
    h += 0.04 * math.sin(x * 1.3) * math.sin(z * 1.1) * min(1.0, max(0.0, d - 6.0))
    return h - 0.012


def color(x, z):
    d = math.hypot(x - CX, z - CZ)
    n = 0.5 + 0.5 * mn.noise(Vector((x * 0.25, z * 0.25, 1.7)))
    m = 0.5 + 0.5 * mn.noise(Vector((x * 0.12 + 5, z * 0.12, 3.1)))
    mint, lav, lem, pch = (0.66, 0.9, 0.76), (0.8, 0.76, 0.96), (0.98, 0.94, 0.7), (1.0, 0.84, 0.76)
    hill = min(1.0, max(0.0, (d - 9.5) / 4.0))
    a = tuple(mint[i] * (1 - m) + lav[i] * m for i in range(3)) if n > 0.5 else tuple(mint[i] * (1 - m) + (lem if n > 0.3 else pch)[i] * m for i in range(3))
    # 地面贴图（ToyGround_Tile）自带薄荷底色和毛毡小花：平地顶点色用白色，山坡只轻轻染一点粉彩
    c = tuple(1.0 * (1 - hill * 0.55) + a[i] * hill * 0.55 for i in range(3))
    return c + (1.0,)


def ground():
    bm = bmesh.new(); lay = bm.loops.layers.color.new('Col'); uvl = bm.loops.layers.uv.new('UVMap')
    R, rings, segs = 30.0, 64, 128
    c0 = bm.verts.new(U(CX, height(CX, CZ), CZ))
    vs = []
    for r in range(1, rings + 1):
        rad = R * (r / rings) ** 1.5
        vs.append([bm.verts.new(U(CX + math.cos(s / segs * math.tau) * rad, height(CX + math.cos(s / segs * math.tau) * rad, CZ + math.sin(s / segs * math.tau) * rad), CZ + math.sin(s / segs * math.tau) * rad)) for s in range(segs)])
    faces = [bm.faces.new((c0, vs[0][s], vs[0][(s + 1) % segs])) for s in range(segs)]
    for r in range(rings - 1):
        for s in range(segs):
            faces.append(bm.faces.new((vs[r][s], vs[r + 1][s], vs[r + 1][(s + 1) % segs], vs[r][(s + 1) % segs])))
    for f in faces:
        for l in f.loops:
            x, z = -l.vert.co.x, -l.vert.co.y
            l[lay] = color(x, z); l[uvl].uv = (x * 0.55, z * 0.55)
    rim = vs[-1]; bot = [bm.verts.new(Vector((v.co.x, v.co.y, -1.0))) for v in rim]
    for s in range(segs):
        f = bm.faces.new((rim[(s + 1) % segs], bot[(s + 1) % segs], bot[s], rim[s]))
        for l in f.loops: l[lay] = (0.66, 0.9, 0.76, 1)
    f = bm.faces.new(bot[::-1])
    for l in f.loops: l[lay] = (0.66, 0.9, 0.76, 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


P.reset()
bm = ground()
ob = P.to_obj(bm, 'TOY_OuterGround', smooth=True)
bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'TOY_OuterGround.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True, colors_type='SRGB')
print('OUTER', json.dumps(P.report))
