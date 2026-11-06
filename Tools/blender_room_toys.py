"""房间玩具化道具（Blender 里运行）：Blender -b -P Tools/blender_room_toys.py -- <输出目录>
玩法里小怪插旗、房间玩具化度上升时，这些道具在真实地板 / 墙面上一块块“长”出来（MR 里贴着真实房间）。

  Toy_PuzzleMat_{A,B,C,D}  拼图地垫块（0.3 m 方块，四边凹凸拼接齿，顶面细小防滑纹，倒角）
  Toy_Fence                小木栅栏一段（5 根圆头尖桩 + 两条横档，粉彩漆）
  Toy_Flag                 玩具化旗子（木杆 + 圆珠顶 + 波浪三角绒布旗 + 积木底座）——小怪插的就是它
  Toy_BlockTower           歪歪的积木塔（5 块积木叠起来，略错位旋转）
  Toy_GrassTuft            塑料草丛（一簇锥形弯草叶，亮绿）
  Toy_Sticker_{Star,Heart,Cloud}  墙面贴纸（极薄鼓起薄片，贴墙用）
全部封闭流形，顶点色 sRGB；原点在底部中心（贴纸在背面中心），正面朝 Unity +Z。
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
rnd = random.Random(20261102)
PINK, MINT, LEMON, LAV, BLUE, PEACH, CREAM = (1.0, 0.66, 0.8), (0.6, 0.9, 0.78), (1.0, 0.88, 0.5), (0.78, 0.68, 1.0), (0.58, 0.76, 1.0), (1.0, 0.74, 0.6), (1.0, 0.96, 0.9)


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    return bm


def curve_mesh(splines, extrude, bevel, res=2):
    cu = bpy.data.curves.new('c', 'CURVE'); cu.dimensions = '2D'; cu.fill_mode = 'BOTH'
    for pts in splines:
        sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
        for p, (x, y) in zip(sp.points, pts): p.co = (x, y, 0, 1)
        sp.use_cyclic_u = True
    cu.extrude = extrude; cu.bevel_depth = bevel; cu.bevel_resolution = res
    ob = bpy.data.objects.new('g', cu); bpy.context.scene.collection.objects.link(ob)
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bm = bmesh.new(); bm.from_mesh(me)
    bpy.data.objects.remove(ob); bpy.data.curves.remove(cu); bpy.data.meshes.remove(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    return bm


def puzzle_outline(s=0.3, tabs=(1, -1, 1, -1)):
    """拼图地垫轮廓：方块四边各一个圆头拼接齿（+1 凸出 / -1 凹进），逆时针"""
    pts = []
    corners = [(-s / 2, -s / 2), (s / 2, -s / 2), (s / 2, s / 2), (-s / 2, s / 2)]
    for e in range(4):
        a, b = Vector(corners[e]), Vector(corners[(e + 1) % 4])
        d = (b - a); n = Vector((d.y, -d.x)).normalized()       # 向外法线
        sign = tabs[e]
        for i in range(24):
            t = i / 24
            p = a + d * t
            # 中间 30% 做一个圆头齿：颈部窄、头部圆
            u = (t - 0.5) / 0.17
            if abs(u) < 1:
                bump = math.cos(u * math.pi / 2) ** 0.8 * 0.055 * sign
                p = p + n * bump
            pts.append((p.x, p.y))
    return pts


def puzzle_mat(col, tabs):
    bm = curve_mesh([puzzle_outline(tabs=tabs)], 0.006, 0.004, 2)
    bmesh.ops.translate(bm, vec=Vector((0, 0, 0.01)), verts=bm.verts)
    # 顶面防滑纹：一排排小圆点
    for i in range(-3, 4):
        for j in range(-3, 4):
            if (i + j) % 2: continue
            dot = P.lathe([(0, 0), (0.007, 0), (0.007, 0.002), (0.005, 0.003), (0, 0.003)], 10)
            bmesh.ops.translate(dot, vec=Vector((i * 0.035, j * 0.035, 0.019)), verts=dot.verts)
            P.merge(bm, dot)
    return color_bm(bm, col)


def fence():
    bm = bmesh.new()
    cols = [PINK, MINT, LEMON, LAV, BLUE]
    for k in range(5):
        x = -0.24 + k * 0.12
        h = 0.28 + 0.02 * math.sin(k * 1.7)
        # 尖头木桩：扁长方体 + 车削尖顶
        post = P.ubox(x - 0.03, x + 0.03, 0.0, h, -0.012, 0.012, bevel=0.006)
        tip = bmesh.new()
        tv = [tip.verts.new(U(x + dx, h + dy, dz)) for dx, dy, dz in ((-0.03, 0, -0.012), (0.03, 0, -0.012), (0.03, 0, 0.012), (-0.03, 0, 0.012), (0, 0.045, -0.012), (0, 0.045, 0.012))]
        for f in ((0, 1, 4), (3, 5, 2), (0, 4, 5, 3), (1, 2, 5, 4), (0, 3, 2, 1)): tip.faces.new([tv[i] for i in f])
        bmesh.ops.recalc_face_normals(tip, faces=tip.faces)
        c = cols[k % len(cols)]
        P.merge(bm, color_bm(post, c)); P.merge(bm, color_bm(tip, c))
    for y in (0.08, 0.2):
        rail = P.ubox(-0.3, 0.3, y - 0.014, y + 0.014, -0.03, -0.012, bevel=0.005)
        P.merge(bm, color_bm(rail, CREAM))
    return bm


def flag(col=PINK):
    bm = bmesh.new()
    base = P.ubox(-0.06, 0.06, 0.0, 0.045, -0.06, 0.06, bevel=0.01)      # 积木底座 + 4 颗凸点
    P.merge(bm, color_bm(base, BLUE))
    for sx in (-1, 1):
        for sz in (-1, 1):
            st = P.lathe([(0, 0), (0.016, 0), (0.016, 0.014), (0.013, 0.017), (0, 0.017)], 14)
            for v in st.verts: v.co = U(sx * 0.03 + v.co.x, 0.045 + v.co.z, sz * 0.03 + v.co.y)
            bmesh.ops.recalc_face_normals(st, faces=st.faces)
            P.merge(bm, color_bm(st, BLUE))
    pole = P.tube([U(0, 0.04, 0), U(0, 0.5, 0)], 0.009, seg=12)
    P.merge(bm, color_bm(pole, (0.95, 0.8, 0.6)))
    ball = P.lathe([(0, -0.02), (0.014, -0.014), (0.02, 0), (0.014, 0.014), (0, 0.02)], 14)
    for v in ball.verts: v.co.z += 0.52
    P.merge(bm, color_bm(ball, LEMON))
    # 波浪三角旗（双面 + 厚度），沿 +X 伸出
    L, Hh, nx, ny, t = 0.2, 0.13, 14, 5, 0.006
    fb = bmesh.new()
    def pos(i, j, side):
        u = i / nx; v = j / ny
        half = Hh / 2 * (1 - u) + 0.003 * u
        zz = 0.43 + (v * 2 - 1) * half
        wave = 0.018 * math.sin(u * math.pi * 1.8) * u
        return U(0.008 + u * L, zz, wave + side * t / 2)
    top = [[fb.verts.new(pos(i, j, 1)) for j in range(ny + 1)] for i in range(nx + 1)]
    bot = [[fb.verts.new(pos(i, j, -1)) for j in range(ny + 1)] for i in range(nx + 1)]
    for i in range(nx):
        for j in range(ny):
            fb.faces.new((top[i][j], top[i + 1][j], top[i + 1][j + 1], top[i][j + 1]))
            fb.faces.new((bot[i][j + 1], bot[i + 1][j + 1], bot[i + 1][j], bot[i][j]))
    ring = [(i, 0) for i in range(nx + 1)] + [(nx, j) for j in range(1, ny + 1)] + [(i, ny) for i in range(nx - 1, -1, -1)] + [(0, j) for j in range(ny - 1, 0, -1)]
    for k in range(len(ring)):
        a, b = ring[k], ring[(k + 1) % len(ring)]
        fb.faces.new((top[a[0]][a[1]], bot[a[0]][a[1]], bot[b[0]][b[1]], top[b[0]][b[1]]))
    bmesh.ops.recalc_face_normals(fb, faces=fb.faces)
    P.merge(bm, color_bm(fb, col))
    return bm


def block_tower():
    bm = bmesh.new()
    y = 0.0
    cols = [PINK, MINT, LEMON, BLUE, LAV]
    for k in range(5):
        w = rnd.choice((0.16, 0.12)); d = 0.08; h = 0.06
        part = P.ubox(-w / 2, w / 2, 0, h, -d / 2, d / 2, bevel=0.008)
        color_bm(part, cols[k])
        nx = max(1, round(w / 0.06))
        for i in range(nx):
            for jz in (-1, 1):
                st = P.lathe([(0, 0), (0.014, 0), (0.014, 0.012), (0.011, 0.015), (0, 0.015)], 12)
                for v in st.verts: v.co = U(-w / 2 + (i + 0.5) * w / nx + v.co.x, h + v.co.z - 0.002, jz * 0.02 + v.co.y)
                bmesh.ops.recalc_face_normals(st, faces=st.faces)
                P.merge(part, color_bm(st, cols[k]))
        m = Matrix.Translation(U(rnd.uniform(-0.012, 0.012), y, rnd.uniform(-0.01, 0.01))) @ Matrix.Rotation(rnd.uniform(-0.35, 0.35), 4, 'Z')
        bmesh.ops.transform(part, matrix=m, verts=part.verts)
        P.merge(bm, part)
        y += h
    return bm


def grass_tuft():
    bm = bmesh.new()
    for i in range(9):
        a = i / 9 * math.tau + rnd.uniform(-0.25, 0.25); L = rnd.uniform(0.08, 0.14); lean = rnd.uniform(0.3, 0.8)
        pts = []
        for k in range(6):
            t = k / 5; r = math.sin(lean * t) * L * 0.5 + 0.008
            pts.append((math.cos(a) * r, math.sin(a) * r, t * L))
        P.merge(bm, color_bm(P.tube(pts, 0.007, seg=5, r_fn=lambda u: 1 - 0.8 * u), rnd.choice([(0.46, 0.86, 0.4), (0.56, 0.92, 0.46), (0.4, 0.78, 0.42)])))
    base = P.lathe([(0, 0), (0.035, 0), (0.03, 0.008), (0, 0.01)], 14)
    P.merge(bm, color_bm(base, (0.4, 0.74, 0.4)))
    return bm


def sticker(kind, col):
    r = 0.07
    if kind == 'Star':
        out = [((r if i % 2 == 0 else r * 0.46) * math.cos(math.pi / 2 + i * math.pi / 5), (r if i % 2 == 0 else r * 0.46) * math.sin(math.pi / 2 + i * math.pi / 5)) for i in range(10)]
    elif kind == 'Heart':
        out = []
        for i in range(48):
            t = i / 48 * math.tau
            out.append((16 * math.sin(t) ** 3 / 17 * r, (13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t) + 2) / 17 * r))
    else:
        out = []
        for i in range(60):
            t = i / 60 * math.tau
            rr = r * (0.75 + 0.18 * abs(math.sin(3 * t)))
            out.append((math.cos(t) * rr * 1.3, math.sin(t) * rr * 0.8))
    bm = P.slab(out, 0.004)
    # 贴纸平面在 XY、正面 +Z（Blender）→ Unity 正面 +Z
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(math.radians(90), 4, 'X'), verts=bm.verts)
    return color_bm(bm, col)


def export(name, bm):
    ob = P.to_obj(bm, name, smooth=True)
    try: ob.data.set_sharp_from_angle(angle=math.radians(40))
    except Exception: pass
    bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                             bake_space_transform=True, colors_type='SRGB')
    bpy.data.objects.remove(ob, do_unlink=True)


def main():
    P.reset()
    for n, (c, tabs) in {'A': (PINK, (1, -1, 1, -1)), 'B': (MINT, (-1, 1, -1, 1)), 'C': (LEMON, (1, 1, -1, -1)), 'D': (BLUE, (-1, -1, 1, 1))}.items():
        export('Toy_PuzzleMat_' + n, puzzle_mat(c, tabs))
    export('Toy_Fence', fence())
    export('Toy_Flag', flag())
    export('Toy_BlockTower', block_tower())
    export('Toy_GrassTuft', grass_tuft())
    for k, c in {'Star': LEMON, 'Heart': PINK, 'Cloud': (0.9, 0.94, 1.0)}.items():
        export('Toy_Sticker_' + k, sticker(k, c))
    print('ROOMTOYS', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
