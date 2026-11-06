"""第 6 章「玩具箱」精细程序化部件（Blender 里运行）：
  Blender -b -P Tools/blender_w6_set.py -- <输出目录>

  W6_Crayon_{Yellow,Blue,Pink,Mint}  巨型蜡笔：12 棱柱身 + 削尖笔头 + 包装纸（两道深色环 + 浅色标签带），原点在底部
  W6_Top_{A,B}       陀螺：车削轮廓（尖底 + 宽肚 + 握柄）+ 横向彩条，原点在尖端（可旋转 + 摆动）
  W6_BoxWalls        玩具箱内壁：左右两面木板墙（一块块倒角木板 + 角柱 + 顶沿），高 2.6 m
  W6_Floor           木地板（一条条倒角长板）
  W6_CloudRug        云朵形蓝色绒布地毯（波浪边薄垫 + 一圈缝线）
  W6_GlowStars       夜光星星贴纸（鼓起的五角星薄片，自发光材质）
  W6_CandyArch       门框：两根螺旋条纹拐杖糖组成拱 + 背后薄荷色糖果拱条 + 门脚彩色果冻豆
  W6_Block_{T,O,Y,B,X}  字母积木（复用第 2 章积木生成器），拼 TOYBOX
全部封闭流形，顶点色 sRGB；坐标按 Unity 约定（门面 z=0，门后世界在 -z）。
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_w2_blocks as BK

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
rnd = random.Random(20261101)
W, H = 1.3, 1.8
R_IN = W / 2
SPRING = H - R_IN
PINK, MINT, LEMON, LAV, BLUE, PEACH, CREAM = (1.0, 0.64, 0.78), (0.6, 0.9, 0.78), (1.0, 0.88, 0.5), (0.78, 0.68, 1.0), (0.58, 0.76, 1.0), (1.0, 0.74, 0.6), (1.0, 0.96, 0.9)


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    return bm


def color_by(bm, fn):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = tuple(fn(l.vert.co)) + (1.0,)
    return bm


def to_unity_up(bm):
    """车削件（沿 Blender +Z）→ Unity 沿 +Y：Blender 的 +Z 本来就是 Unity +Y，无需旋转"""
    return bm


def crayon(wax, Hh=1.2, r=0.07):
    """24 段带细棱（蜡笔压制的槽）的柱身 + 削尖笔头（带削痕环）+ 包装纸套筒（两端卷边 + 两道凸起深色环 + 浅色标签带）"""
    tip0, tip1 = Hh * 0.82, Hh
    prof = [(0, 0), (r * 0.94, 0), (r, 0.012)]
    prof += [(r, Hh * (0.02 + 0.8 * i / 20)) for i in range(1, 21)]
    prof += [(r * 0.97, tip0 + 0.008), (r * 0.82, tip0 + 0.03), (r * 0.66, tip0 + 0.07), (r * 0.5, tip0 + 0.115), (r * 0.38, tip1 - 0.035), (r * 0.33, tip1 - 0.015), (r * 0.2, tip1 - 0.004), (0, tip1)]
    flute = lambda t, z: 1.0 + 0.025 * math.cos(t * 12) if z < tip0 else 1.0
    bm = P.lathe(prof, 24, flute)
    color_bm(bm, wax)
    lw = lambda c, k: tuple(min(1, x * k) for x in c)
    light = tuple(min(1, x * 0.5 + 0.5) for x in wax)
    wprof = [(0, 0.07 * Hh)] + [(r * 1.07, Hh * (0.07 + 0.68 * i / 24)) for i in range(25)] + [(0, 0.75 * Hh)]
    wrap = P.lathe(wprof, 24)
    def wc(co):
        z = co.z / Hh
        if 0.13 < z < 0.17 or 0.65 < z < 0.69: return lw(wax, 0.7)
        if 0.3 < z < 0.52: return light
        return lw(wax, 0.95)
    color_by(wrap, wc)
    P.merge(bm, wrap)
    for zc, col in ((0.072, lw(wax, 0.9)), (0.748, lw(wax, 0.9)), (0.15, lw(wax, 0.66)), (0.67, lw(wax, 0.66))):   # 卷边 / 凸起色环
        ring = P.torus(r * 1.07, 0.006, seg=24, sides=6)
        for v in ring.verts: v.co.z += zc * Hh
        P.merge(bm, color_bm(ring, col))
    return bm


def top(cols, Hh=0.42):
    prof = [(0, 0), (0.03, 0.03), (0.12, 0.1), (0.2, 0.17), (0.22, 0.2), (0.21, 0.23), (0.14, 0.27), (0.05, 0.29), (0.035, 0.3), (0.035, 0.4), (0.05, 0.41), (0.05, 0.43), (0, 0.44)]
    prof = [(r * Hh / 0.44, z * Hh / 0.44) for r, z in prof]
    # 加密轮廓：相邻点之间插值 3 次，车削 48 段
    dense = []
    for a, b in zip(prof[:-1], prof[1:]):
        for k in range(3): dense.append((a[0] + (b[0] - a[0]) * k / 3, a[1] + (b[1] - a[1]) * k / 3))
    dense.append(prof[-1])
    bm = P.lathe(dense, 48)
    def c(co):
        z = co.z / Hh
        band = int(z * 11)
        return cols[band % len(cols)]
    color_by(bm, c)
    # 色带交界处的凸起细环（陀螺上的车削纹）
    for i in range(2, 8):
        z = i / 11 * Hh
        rr = max(r for r, zz in [(pr, pz) for pr, pz in dense] if abs(zz - z) < Hh / 30) if any(abs(pz - z) < Hh / 30 for _, pz in dense) else 0
        if rr < 0.03: continue
        ring = P.torus(rr * 1.005, 0.004, seg=48, sides=6)
        for v in ring.verts: v.co.z += z
        P.merge(bm, color_bm(ring, (1.0, 0.97, 0.92)))
    return bm


def box_walls():
    bm = bmesh.new()
    for side in (-1, 1):
        x = side * 3.3
        for k in range(9):                                # 竖向长木板，一块块略有不同的木色
            z0 = -0.6 - k * 0.66
            pl = P.ubox(x - 0.05, x + 0.05, 0.0, 2.6, z0 - 0.64, z0, bevel=0.015)
            base = (0.95, 0.78, 0.58)
            c = tuple(min(1, v * rnd.uniform(0.94, 1.04)) for v in base)
            color_by(pl, lambda co, c=c: tuple(min(1, v * (1 + 0.035 * math.sin(-co.y * 40 + co.z * 3))) for v in c))
            P.merge(bm, pl)
        rim = P.ubox(x - 0.09, x + 0.09, 2.6, 2.72, -6.6, -0.5, bevel=0.03)        # 顶沿
        P.merge(bm, color_bm(rim, (0.86, 0.66, 0.46)))
        post = P.ubox(x - 0.1, x + 0.1, 0.0, 2.75, -0.62, -0.42, bevel=0.03)      # 前角柱
        P.merge(bm, color_bm(post, (0.86, 0.66, 0.46)))
    return bm


def floor():
    bm = bmesh.new()
    for i in range(-16, 17):
        x0 = 0.3 + i * 0.2
        z = -0.3
        while z > -7.0 + 1e-3:
            L = min(rnd.uniform(0.9, 1.8), z + 7.0)          # 最后一块截到 z=-7，地板远端齐平
            if L < 0.25: break
            pl = P.ubox(x0 + 0.003, x0 + 0.197, -0.03, 0.0, z - L + 0.003, z - 0.003, bevel=0.006)
            c = tuple(min(1, v * rnd.uniform(0.93, 1.05)) for v in (0.92, 0.74, 0.56))
            P.merge(bm, color_bm(pl, c))
            z -= L
    return bm


def cloud_rug():
    n = 120; R = 1.1
    out = []
    for i in range(n):
        t = i / n * math.tau
        rr = R * (1 + 0.12 * math.cos(6 * t) ** 2) * (1.0 if math.sin(t) > -0.2 else 0.85)
        out.append((math.cos(t) * rr * 1.25, math.sin(t) * rr * 0.75))
    bm = P.slab(out, 0.02)
    for v in bm.verts:
        v.co = U(-0.1 + v.co.x, 0.012 + v.co.z, -2.6 + v.co.y)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    color_bm(bm, (0.5, 0.66, 0.95))
    # 缝线
    for i in range(0, n, 2):
        t0 = i / n * math.tau; t1 = (i + 1.1) / n * math.tau
        pts = []
        for t in (t0, (t0 + t1) / 2, t1):
            rr = 1.1 * (1 + 0.12 * math.cos(6 * t) ** 2) * (1.0 if math.sin(t) > -0.2 else 0.85) * 0.9
            pts.append(U(-0.1 + math.cos(t) * rr * 1.25, 0.024, -2.6 + math.sin(t) * rr * 0.75))
        P.merge(bm, color_bm(P.tube(pts, 0.004, seg=5), (0.86, 0.92, 1.0)))
    return bm


def glow_stars():
    bm = bmesh.new()
    spots = [(-3.22, 1.9, -1.4), (-3.22, 1.4, -2.6), (-3.22, 2.2, -3.6), (-3.22, 1.1, -4.4), (-3.22, 1.8, -5.3),
             (3.22, 1.7, -1.2), (3.22, 2.3, -2.2), (3.22, 1.2, -3.3), (3.22, 2.0, -4.6), (3.22, 1.5, -5.6)]
    for x, y, z in spots:
        r = rnd.uniform(0.09, 0.16)
        st = P.slab([((r if i % 2 == 0 else r * 0.45) * math.cos(math.pi / 2 + i * math.pi / 5), (r if i % 2 == 0 else r * 0.45) * math.sin(math.pi / 2 + i * math.pi / 5)) for i in range(10)], 0.012)
        # 贴在侧墙内面：星星平面朝 ±X（墙内侧）
        side = -1 if x < 0 else 1
        m = Matrix.Translation(U(x - side * 0.06, y, z)) @ Matrix.Rotation(math.radians(90) * side, 4, 'Y') @ Matrix.Rotation(rnd.uniform(-0.4, 0.4), 4, 'Z')
        bmesh.ops.transform(st, matrix=m, verts=st.verts)
        P.merge(bm, color_bm(st, rnd.choice([(0.75, 1.0, 0.85), (1.0, 0.97, 0.7), (0.85, 0.9, 1.0)])))
    return bm


def arch_path(off, n_arc=60, n_side=24):
    r = R_IN + off
    pts = [(r, SPRING * i / n_side) for i in range(n_side)]
    pts += [(math.cos(math.pi * i / n_arc) * r, SPRING + math.sin(math.pi * i / n_arc) * r) for i in range(n_arc + 1)]
    pts += [(-r, SPRING * (1 - i / n_side)) for i in range(1, n_side + 1)]
    return pts


def candy_arch():
    bm = bmesh.new()
    off = 0.14
    # 两根拐杖糖：右边从地面升到拱顶略过中线，左边对称；螺旋红白条
    path = arch_path(off, 220, 80)          # 足够密的环，螺旋条纹的顶点色才不会出锯齿
    half = len(path) // 2
    for side, seg in ((1, path[:half + 4]), (-1, path[half - 4:])):
        pts = [U(x, y, 0.06 + 0.012 * side) for x, y in seg]
        tb = P.tube(pts, 0.055, seg=32)
        L = [0.0]
        for i in range(1, len(seg)): L.append(L[-1] + math.dist(seg[i], seg[i - 1]))
        col_a = PINK if side > 0 else MINT
        # P.tube 的顶点按环排列：第 i 环的第 k 个点 → 螺旋条纹 = 弧长 × 频率 + 绕管角度
        segn = 32; lay = tb.loops.layers.color.new('Col')
        tb.verts.index_update(); tb.verts.ensure_lookup_table()
        for f in tb.faces:
            for l in f.loops:
                idx = l.vert.index
                if idx >= len(seg) * segn: l[lay] = col_a + (1,); continue
                i, k = divmod(idx, segn)
                v = math.sin(L[i] * 20 + k / segn * math.tau * 2)
                l[lay] = (col_a if v > 0 else CREAM) + (1,)
        P.merge(bm, tb)
    # 背后的薄荷糖果拱条（扁圆管）
    back = P.tube([U(x, y, -0.03) for x, y in arch_path(off + 0.02, 40, 14)], 0.09, seg=14)
    color_bm(back, (0.72, 0.94, 0.86))
    P.merge(bm, back)
    # 门脚的果冻豆
    for side in (-1, 1):
        for k in range(9):
            x = side * rnd.uniform(0.7, 1.15); z = rnd.uniform(0.05, 0.45)
            bean = P.lathe([(0, -0.03), (0.016, -0.026), (0.02, -0.01), (0.02, 0.01), (0.016, 0.026), (0, 0.03)], 12)
            m = Matrix.Translation(U(x, 0.018, z)) @ Matrix.Rotation(math.radians(90), 4, 'Y') @ Matrix.Rotation(rnd.uniform(0, math.tau), 4, 'X')
            bmesh.ops.transform(bean, matrix=m, verts=bean.verts)
            P.merge(bm, color_bm(bean, rnd.choice([PINK, MINT, LEMON, LAV, BLUE, PEACH])))
    return bm


def export(name, bm, sharp=False):
    ob = P.to_obj(bm, name, smooth=True)
    if sharp:
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
    for n, c in {'Yellow': (1.0, 0.86, 0.36), 'Blue': (0.46, 0.66, 1.0), 'Pink': (1.0, 0.52, 0.68), 'Mint': (0.46, 0.86, 0.7)}.items():
        export('W6_Crayon_' + n, crayon(c))
    export('W6_Top_A', top([PINK, CREAM, BLUE, LEMON]))
    export('W6_Top_B', top([MINT, LAV, CREAM, PEACH]))
    export('W6_BoxWalls', box_walls(), sharp=True)
    export('W6_Floor', floor(), sharp=True)
    export('W6_CloudRug', cloud_rug())
    export('W6_GlowStars', glow_stars())
    export('W6_CandyArch', candy_arch())
    # TOYBOX 字母积木（正面字母，侧面符号）
    sym = {'T': (PINK, BLUE), 'O': (LEMON, PINK), 'Y': (MINT, LAV), 'B': (BLUE, PEACH), 'X': (LAV, LEMON)}
    for ch, (panel, letter) in sym.items():
        spec = {'+z': (panel, ch, letter), '+x': (CREAM, 'star', letter), '+y': (CREAM, 'heart', PINK), '-x': (CREAM, 'moon', LAV), '-z': (panel, ch, letter), '-y': (CREAM, None, None)}
        export('W6_Block_' + ch, BK.build('W6_Block_' + ch, spec), sharp=True)
    print('W6SET', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
