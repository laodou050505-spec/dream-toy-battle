"""门后世界的小生命（Blender 里运行）：Blender -b -P Tools/blender_critters.py -- <输出目录>

  SH_Bird_Body_{Pink,Mint,Lemon}     折纸小鸟身体：多面体折纸造型（菱形身 + 尖头 + 翘尾），平面着色、边缘折痕略深
  SH_Bird_Wing_{Pink,Mint,Lemon}     折纸翅膀：带一道折痕的三角纸片（有厚度），原点在翅根、沿 +X 伸出（左翅在 Unity 里镜像）
  SH_Fly_Body_{Pink,Lav,Lemon,Blue}  毛毡蝴蝶身体：分节的软胶囊 + 两根卷头触角
  SH_Fly_Wing_{Pink,Lav,Lemon,Blue}  毛毡蝴蝶翅膀：上下两瓣的软薄片 + 一圈缝线 + 中间一个小圆斑，原点在翅根、沿 +X 伸出
全部封闭流形，顶点色 sRGB；正面（头）朝 Unity +Z。
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    return bm


def shade_faces(bm, rgb, k=0.12):
    """折纸：每个面按法线朝向略微明暗不同（纸的折面感）"""
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    bm.normal_update()
    for f in bm.faces:
        s = 1.0 - k + k * (0.5 + 0.5 * f.normal.dot(Vector((0.3, -0.4, 0.86)).normalized()))
        c = tuple(min(1, x * s) for x in rgb) + (1.0,)
        for l in f.loops: l[lay] = c
    return bm


def bird_body(col):
    # 关键点（Unity 坐标，头朝 +Z）：菱形截面的身体，前尖后翘
    pts = {
        'nose': (0, 0.0, 0.11), 'tail': (0, 0.05, -0.13),
        'top': (0, 0.035, 0.0), 'bot': (0, -0.03, 0.01), 'l': (-0.032, 0.0, 0.0), 'r': (0.032, 0.0, 0.0),
        'neck_t': (0, 0.03, 0.06), 'neck_l': (-0.018, 0.005, 0.06), 'neck_r': (0.018, 0.005, 0.06), 'neck_b': (0, -0.018, 0.06),
    }
    bm = bmesh.new()
    V = {k: bm.verts.new(U(*v)) for k, v in pts.items()}
    faces = [('nose', 'neck_t', 'neck_r'), ('nose', 'neck_r', 'neck_b'), ('nose', 'neck_b', 'neck_l'), ('nose', 'neck_l', 'neck_t'),
             ('neck_t', 'top', 'r', 'neck_r'), ('neck_r', 'r', 'bot', 'neck_b'), ('neck_b', 'bot', 'l', 'neck_l'), ('neck_l', 'l', 'top', 'neck_t'),
             ('top', 'tail', 'r'), ('r', 'tail', 'bot'), ('bot', 'tail', 'l'), ('l', 'tail', 'top')]
    for f in faces: bm.faces.new([V[k] for k in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    shade_faces(bm, col, 0.22)
    # 小眼睛：两颗深色小珠
    for sx in (-1, 1):
        eye = P.lathe([(0, -0.005), (0.004, -0.003), (0.005, 0), (0.004, 0.003), (0, 0.005)], 8)
        for v in eye.verts: v.co += U(sx * 0.016, 0.014, 0.075)
        P.merge(bm, color_bm(eye, (0.25, 0.22, 0.3)))
    return bm


def bird_wing(col):
    """三角纸翅：根部 (0, 0..±0.04)，尖端 (0.16, ...)；中间一道折痕抬高，形成浅 V 形；厚度 0.003"""
    t = 0.0015
    outline = [(0.0, -0.045), (0.16, -0.01), (0.12, 0.03), (0.0, 0.05)]
    crease = [(0.0, 0.0), (0.15, 0.005)]
    bm = bmesh.new()
    def V(x, z, side):
        lift = 0.012 * max(0.0, 1 - abs(z - 0.002) / 0.05)              # 折痕处抬起
        return bm.verts.new(U(x, lift + side * t, z))
    top = [V(x, z, 1) for x, z in outline]; bot = [V(x, z, -1) for x, z in outline]
    ct = [V(x, z, 1) for x, z in crease]; cb = [V(x, z, -1) for x, z in crease]
    # 上表面两片三角面 + 下表面 + 侧边
    for A, C in ((top, ct), ):
        bm.faces.new((A[0], A[1], C[1], C[0])); bm.faces.new((C[0], C[1], A[2], A[3]))
    bm.faces.new((cb[0], cb[1], bot[1], bot[0])); bm.faces.new((bot[3], bot[2], cb[1], cb[0]))
    ring_t = [top[0], top[1], ct[1], top[2], top[3], ct[0]]; ring_b = [bot[0], bot[1], cb[1], bot[2], bot[3], cb[0]]
    for k in range(6):
        j = (k + 1) % 6
        bm.faces.new((ring_t[j], ring_t[k], ring_b[k], ring_b[j]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return shade_faces(bm, col, 0.18)


def fly_body(col):
    bm = P.lathe([(0, -0.03), (0.006, -0.026), (0.008, -0.012), (0.0065, -0.004), (0.008, 0.004), (0.0065, 0.012), (0.009, 0.02), (0.008, 0.03), (0, 0.036)], 12)
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(math.radians(-90), 4, 'X'), verts=bm.verts)   # 沿 Blender -Y = Unity +Z（头朝前）
    color_bm(bm, tuple(x * 0.82 for x in col))
    for sx in (-1, 1):
        pts = []
        for i in range(12):
            t = i / 11
            curl = max(0.0, t - 0.75) / 0.25
            pts.append(U(sx * (0.004 + 0.012 * t + 0.004 * math.sin(curl * 3)), 0.006 + 0.022 * t - 0.004 * curl * curl, 0.03 + 0.014 * t))
        P.merge(bm, color_bm(P.tube(pts, 0.0011, seg=5), (0.35, 0.3, 0.4)))
        tip = P.lathe([(0, -0.0025), (0.0025, 0), (0, 0.0025)], 8)
        for v in tip.verts: v.co += pts[-1]
        P.merge(bm, color_bm(tip, col))
    return bm


def fly_wing(col):
    def lobe(cx, cz, rx, rz, n=18):
        return [(cx + math.cos(t) * rx, cz + math.sin(t) * rz) for t in [i / n * math.tau for i in range(n)]]
    bm = bmesh.new()
    for (cx, cz, rx, rz, k) in ((0.032, 0.018, 0.034, 0.026, 1.0), (0.026, -0.016, 0.024, 0.018, 0.9)):
        o = lobe(cx, cz, rx, rz)
        sl = P.slab([(x, z) for x, z in o], 0.003)
        bmesh.ops.transform(sl, matrix=Matrix.Rotation(math.radians(90), 4, 'X'), verts=sl.verts)   # XY 平面 → XZ 平面（翅膀水平）
        P.merge(bm, color_bm(sl, tuple(min(1, x * k) for x in col)))
        # 圆斑
        spot = P.slab([(cx + math.cos(t) * rx * 0.3, cz + math.sin(t) * rz * 0.3) for t in [i / 10 * math.tau for i in range(10)]], 0.0036)
        bmesh.ops.transform(spot, matrix=Matrix.Rotation(math.radians(90), 4, 'X'), verts=spot.verts)
        P.merge(bm, color_bm(spot, (1.0, 0.97, 0.92)))
        # 缝线：外圈一段段短线
        for i in range(0, 18, 2):                                   # 缝线 9 段（一片翅膀才几厘米，远看是一圈虚线就够）
            t0 = i / 18 * math.tau; t1 = (i + 1.1) / 18 * math.tau
            q = [(cx + math.cos(t) * rx * 0.82, cz + math.sin(t) * rz * 0.82) for t in (t0, (t0 + t1) / 2, t1)]
            st = P.tube([Vector((x, z, 0.0021)) for x, z in q], 0.0008, seg=3)
            bmesh.ops.transform(st, matrix=Matrix.Rotation(math.radians(90), 4, 'X'), verts=st.verts)
            P.merge(bm, color_bm(st, (1.0, 0.96, 0.9)))
    # 现在翅膀在 Blender XZ 平面（y 为厚度方向）；转成 Unity：沿 +X 伸出、水平（Unity XZ 平面）
    for v in bm.verts: v.co = U(v.co.x, v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def export(name, bm, sharp=False):
    ob = P.to_obj(bm, name, smooth=not sharp)
    bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                             bake_space_transform=True, colors_type='SRGB')
    bpy.data.objects.remove(ob, do_unlink=True)


def main():
    P.reset()
    for n, c in {'Pink': (1.0, 0.7, 0.82), 'Mint': (0.66, 0.92, 0.82), 'Lemon': (1.0, 0.92, 0.6)}.items():
        export('SH_Bird_Body_' + n, bird_body(c), sharp=True)
        export('SH_Bird_Wing_' + n, bird_wing(c), sharp=True)
    for n, c in {'Pink': (1.0, 0.66, 0.8), 'Lav': (0.8, 0.7, 1.0), 'Lemon': (1.0, 0.9, 0.55), 'Blue': (0.62, 0.8, 1.0)}.items():
        export('SH_Fly_Body_' + n, fly_body(c))
        export('SH_Fly_Wing_' + n, fly_wing(c))
    print('CRITTERS', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
