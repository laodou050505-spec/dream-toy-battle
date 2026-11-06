"""封面“玩具小剧场”（用户选定概念图 ArtSource/Concept/Cover/cover_A_theater.png）。Blender 里运行：
  Blender -b -P Tools/blender_theater.py -- Assets/_Bang/Art/Models/Proc/UI
部件（标题坐标：+Y 上、-Z 朝玩家，单位米；原点见各部件说明），摆放写进 Theater_layout.json：
  Th_Stage      弧形木台：后方直边、前方椭圆弧台口，台身 + 外挑一圈唇边（木纹贴图，平面投影 UV，按米）
  Th_Column     木柱（车削：柱础 / 柱身 / 柱头 / 金箍），左右复用；原点在柱脚
  Th_Arch       弧形顶梁：两柱之间的扁拱门楣（木纹），外沿一圈小灯座；原点在台面中心
  Th_ArchTrim   顶梁外沿的金色描边条
  Th_Backdrop   背幕：拱顶板，顶点色从夜空蓝紫渐变到台面附近的暖粉；原点在台面中心
  Th_Stars      背幕上的小星星（奶油 / 金色五角星薄片）
  Th_Curtain    一片丝绒幕布（竖向褶皱，下摆微张；原点在外侧上角，向 +X 展开 → Unity 里按 X 缩放就能拉开 / 合上）
  Th_Valance    顶部波浪幕檐（丝绒）+ Th_Fringe 金色流苏珠
  Th_FootSocket 台口脚灯座（金色小碗），Unity 里沿台口弧线摆一排、灯泡放进碗里
全部封闭流形（proc_lib.check 报告），顶点色 sRGB；木纹 / 丝绒部件带 UV。
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)

WOOD, WOOD2, CREAM, GOLD = (0.97, 0.82, 0.64), (0.62, 0.34, 0.24), (1.0, 0.95, 0.87), (1.0, 0.8, 0.42)
CHERRY, PANEL, PLUM = (0.5, 0.24, 0.17), (0.56, 0.1, 0.22), (0.36, 0.13, 0.32)
VELVET, VELVET2 = (0.86, 0.24, 0.37), (0.3, 0.03, 0.1)
SKY_T, SKY_B = (0.33, 0.27, 0.55), (0.98, 0.72, 0.68)

# 版式
HALF_W = 0.62            # 台宽一半
BACK_Z = 0.30            # 台后沿
FRONT_B = 0.40           # 台口椭圆前凸
STAGE_H, LIP_H, LIP_O = 0.075, 0.02, 0.016
TOP_Y = STAGE_H + LIP_H  # 台面高度
COL_X, COL_Z, COL_H = 0.50, 0.16, 0.74
ARCH_Y0 = TOP_Y + COL_H  # 顶梁下沿（两端）
ARCH_RISE = 0.10         # 拱的矢高
ARCH_T, ARCH_D = 0.085, 0.10
L = {}


class B:
    """在 Unity 坐标里直接搭网格，最后统一转成 Blender 坐标；可带顶点色 / UV（按米平面投影）"""
    def __init__(self, uv_scale=None):
        self.bm = bmesh.new(); self.col = self.bm.loops.layers.color.new('Col')
        self.uv = self.bm.loops.layers.uv.new('UVMap') if uv_scale else None; self.uvs = uv_scale

    def face(self, vs, rgb):
        f = self.bm.faces.new(vs)
        for l in f.loops: l[self.col] = (rgb[0], rgb[1], rgb[2], 1.0)
        return f

    def prism(self, poly, y0, y1, rgb, rgb_side=None):
        """XZ 平面多边形（逆时针，从上往下看）沿 Y 挤出成封闭柱体"""
        n = len(poly)
        lo = [self.bm.verts.new((x, y0, z)) for x, z in poly]
        hi = [self.bm.verts.new((x, y1, z)) for x, z in poly]
        self.face(hi, rgb); self.face(lo[::-1], rgb_side or rgb)
        for i in range(n):
            j = (i + 1) % n
            self.face((lo[i], lo[j], hi[j], hi[i]), rgb_side or rgb)

    def slab_xy(self, poly, z0, z1, rgb, rgb_fn=None):
        """XY 平面多边形沿 Z 挤出（立在台上的板）；rgb_fn(y) 可按高度渐变"""
        n = len(poly)
        a = [self.bm.verts.new((x, y, z0)) for x, y in poly]
        b = [self.bm.verts.new((x, y, z1)) for x, y in poly]
        fa = self.bm.faces.new(a[::-1]); fb = self.bm.faces.new(b)
        for f in (fa, fb):
            for l in f.loops:
                c = rgb_fn(l.vert.co.y) if rgb_fn else rgb; l[self.col] = (c[0], c[1], c[2], 1.0)
        for i in range(n):
            j = (i + 1) % n
            f = self.bm.faces.new((a[i], a[j], b[j], b[i]))
            for l in f.loops:
                c = rgb_fn(l.vert.co.y) if rgb_fn else rgb; l[self.col] = (c[0], c[1], c[2], 1.0)

    def finish(self, name, smooth=True, sharp=None, bevel=0.0):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if bevel > 0:                                        # 木作边缘倒圆角（硬直角是“廉价感”的主要来源之一）
            sh = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(30)]
            bmesh.ops.bevel(bm, geom=sh, offset=bevel, segments=2, affect='EDGES', profile=0.5, clamp_overlap=True)
        if self.uv:
            for f in bm.faces:
                n = f.normal; ax = max(range(3), key=lambda k: abs(n[k]))
                for l in f.loops:
                    x, y, z = l.vert.co
                    l[self.uv].uv = ((x, z) if ax == 1 else (z, y) if ax == 0 else (x, y))
                    l[self.uv].uv = (l[self.uv].uv[0] * self.uvs, l[self.uv].uv[1] * self.uvs)
        for v in bm.verts: v.co = P.U(v.co.x, v.co.y, v.co.z)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        export(name, bm, sharp)


def export(name, bm, sharp=None):
    ob = P.to_obj(bm, name, smooth=True)
    if sharp:
        try: ob.data.set_sharp_from_angle(angle=math.radians(sharp))
        except Exception: pass
    bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True, colors_type='SRGB')
    bpy.data.objects.remove(ob, do_unlink=True)


def stage_outline(grow=0.0, n=48):
    """台面外轮廓（XZ，逆时针从上往下看 = Unity 里 +Y 朝上看）：后沿直边 + 前方半椭圆台口"""
    pts = []
    hw, bz, fb = HALF_W + grow, BACK_Z + grow, FRONT_B + grow
    for i in range(n + 1):                          # 前弧：从右 (x=+hw, z=0) 绕到左 (x=-hw)，经过 z=-fb
        a = i / n * math.pi
        pts.append((math.cos(a) * hw, -math.sin(a) * fb))
    pts.append((-hw, bz)); pts.append((hw, bz))
    # 逆时针（从 +Y 看）：上面的顺序是 右→前→左→左后→右后，在 Unity 左手系里从上往下看是顺时针 → 反过来
    return pts[::-1]


def ring_seg(b, a0, a1, g0, g1, y0, y1, rgb, n=8):
    """台口前弧上一段环形块（角度 a0..a1，外扩 g0..g1，高 y0..y1）：嵌板线脚 / 描边"""
    def pt(a, g): return (math.cos(a) * (HALF_W + g), -math.sin(a) * (FRONT_B + g))
    poly = [pt(a0 + (a1 - a0) * k / n, g1) for k in range(n + 1)] + [pt(a1 - (a1 - a0) * k / n, g0) for k in range(n + 1)]
    b.prism(poly[::-1], y0, y1, rgb)


def stage():
    # 台身（深樱桃木）+ 台面（浅色木地板，外挑唇边）+ 唇边下一圈金色描边 + 台口正面一排凸起嵌板
    b = B(uv_scale=1.6)
    b.prism(stage_outline(0.0), 0.0, STAGE_H, WOOD2)
    b.finish('Th_Stage', sharp=40, bevel=0.004)
    t = B(uv_scale=1.6)
    t.prism(stage_outline(LIP_O), STAGE_H, TOP_Y, WOOD)
    t.finish('Th_StageTop', sharp=40, bevel=0.005)
    g = B()
    g.prism(stage_outline(LIP_O + 0.004), STAGE_H - 0.008, STAGE_H, GOLD)
    for k in range(9):                                     # 台口嵌板：凸出 6 mm 的深色面板，四周留缝
        a0 = (k + 0.12) / 9 * math.pi; a1 = (k + 0.88) / 9 * math.pi
        ring_seg(g, a0, a1, 0.0, 0.006, 0.016, STAGE_H - 0.018, CHERRY)
    g.finish('Th_StageTrim', sharp=40, bevel=0.0015)
    # 脚灯：台面前沿一排 11 个小灯罩（Unity 里灯泡放进去）
    feet = []
    for i in range(11):
        a = (i + 0.5) / 11 * math.pi
        x, z = math.cos(a) * (HALF_W - 0.03), -math.sin(a) * (FRONT_B - 0.03)
        nx, nz = math.cos(a) / HALF_W, -math.sin(a) / FRONT_B; l = math.hypot(nx, nz)
        feet.append([round(x, 4), round(TOP_Y, 4), round(z, 4), round(math.degrees(math.atan2(nx / l, nz / l)), 2)])
    L['foot_lights'] = feet


def foot_socket():
    # 脚灯罩：金色小半圆罩子扣在台沿上、开口朝舞台里（挡住观众直视灯泡，像真的台口脚灯）；原点在罩底中心
    bm = bmesh.new(); lay = bm.loops.layers.color.new('Col')
    R, T, NA, NB = 0.022, 0.003, 12, 8
    def ring(r, k):
        return [bm.verts.new((math.cos(math.pi * i / NA) * r, math.sin(math.pi * i / NA) * r * 0.9, -math.sin(math.pi * 0.5 * k / NB) * r * 0.0 + 0.0)) for i in range(NA + 1)]
    # 用车削的四分之一球壳：外壳 + 内壳 + 边缘封口
    def shell(r):
        vs = []
        for k in range(NB + 1):
            ph = math.pi * 0.5 * k / NB            # 0 = 罩口（朝 +z 舞台里），pi/2 = 罩背（朝观众 -z）
            row = []
            for i in range(NA + 1):
                th = math.pi * i / NA              # 0..pi：上半圆
                x = math.cos(th) * r * math.cos(ph); y = math.sin(th) * r * math.cos(ph); z = -math.sin(ph) * r
                row.append(bm.verts.new((x, y * 0.85, z)))
            vs.append(row)
        return vs
    o = shell(R); n_ = shell(R - T)
    def f(vs, rgb):
        fc = bm.faces.new(vs)
        for l in fc.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    for k in range(NB):
        for i in range(NA):
            f((o[k][i], o[k][i + 1], o[k + 1][i + 1], o[k + 1][i]), GOLD)
            f((n_[k][i + 1], n_[k][i], n_[k + 1][i], n_[k + 1][i + 1]), (1.0, 0.92, 0.7))
    for i in range(NA):                            # 罩口边 / 底边封口
        f((o[0][i + 1], o[0][i], n_[0][i], n_[0][i + 1]), GOLD)
    for k in range(NB):
        for row_o, row_n in ((o, n_),):
            f((row_o[k][0], row_o[k + 1][0], row_n[k + 1][0], row_n[k][0]), GOLD)
            f((row_n[k][NA], row_n[k + 1][NA], row_o[k + 1][NA], row_o[k][NA]), GOLD)
    # 罩背顶点（k=NB）已收成一点附近的小环：补一圈封口
    f([o[NB][i] for i in range(NA + 1)] + [n_[NB][i] for i in range(NA, -1, -1)], GOLD)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for v in bm.verts: v.co = P.U(v.co.x, v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    export('Th_FootSocket', bm)


def column():
    prof = [(0, 0), (0.06, 0), (0.06, 0.03), (0.05, 0.04), (0.05, 0.05), (0.036, 0.06), (0.032, 0.09),
            (0.03, COL_H - 0.1), (0.034, COL_H - 0.085), (0.042, COL_H - 0.075), (0.042, COL_H - 0.06), (0.036, COL_H - 0.05),
            (0.05, COL_H - 0.035), (0.058, COL_H - 0.02), (0.058, COL_H), (0, COL_H)]
    def flute(th, z):                           # 柱身 16 道凹槽（只在柱身段）
        return 1.0 - (0.09 * max(0.0, math.cos(th * 16)) ** 3 if 0.1 < z < COL_H - 0.1 else 0.0)
    bm = P.lathe(prof, 64, flute)
    lay = bm.loops.layers.color.new('Col'); uv = bm.loops.layers.uv.new('UVMap')
    for f in bm.faces:
        for l in f.loops:
            h = l.vert.co.z                       # P.lathe：轴沿 Blender Z
            r = math.hypot(l.vert.co.x, l.vert.co.y)
            gold = (COL_H - 0.08 < h < COL_H - 0.055) or (0.035 < h < 0.055)
            c = GOLD if gold else (GOLD if (h < 0.035 or h > COL_H - 0.04) else CREAM)
            l[lay] = (c[0], c[1], c[2], 1.0)
            l[uv].uv = (math.atan2(l.vert.co.y, l.vert.co.x) / math.tau * 0.2, h * 1.6)
    for v in bm.verts: v.co = Vector((v.co.x, v.co.y, v.co.z))
    export('Th_Column', bm)
    L['columns'] = [[-COL_X, TOP_Y, COL_Z], [COL_X, TOP_Y, COL_Z]]


def arch_curve(y_off, n=40):
    # 顶梁的扁拱：两端在柱顶外侧，中间矢高 ARCH_RISE（正弦形）
    pts = []
    for i in range(n + 1):
        u = i / n; x = -COL_X - 0.07 + u * (2 * COL_X + 0.14)
        pts.append((x, ARCH_Y0 + y_off + math.sin(u * math.pi) * ARCH_RISE))
    return pts


def arch():
    inner = arch_curve(0.0); outer = arch_curve(ARCH_T)
    b = B()
    b.slab_xy(inner + outer[::-1], COL_Z - ARCH_D / 2, COL_Z + ARCH_D / 2, CREAM)
    b.finish('Th_Arch', sharp=40, bevel=0.006)
    t = B()
    o2 = arch_curve(ARCH_T + 0.012)
    t.slab_xy(outer + o2[::-1], COL_Z - ARCH_D / 2 - 0.004, COL_Z + ARCH_D / 2 - 0.02, GOLD)             # 外沿金边
    p0 = arch_curve(0.018); p1 = arch_curve(ARCH_T - 0.018)
    t.slab_xy(p0[2:-2] + p1[2:-2][::-1], COL_Z - ARCH_D / 2 - 0.006, COL_Z - ARCH_D / 2 + 0.002, PANEL)    # 深红嵌板
    t.finish('Th_ArchTrim', sharp=40, bevel=0.002)
    # 顶饰：拱顶正中一颗厚金星
    cx, cy = 0.0, ARCH_Y0 + ARCH_T + ARCH_RISE + 0.012
    star = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5; rr = 0.05 if k % 2 == 0 else 0.022
        star.append((cx + math.cos(a) * rr, cy + 0.045 + math.sin(a) * rr))
    c = B(); c.slab_xy(star[::-1], COL_Z - ARCH_D / 2 - 0.012, COL_Z - ARCH_D / 2 + 0.012, GOLD); c.finish('Th_Crest', bevel=0.004)
    # 灯泡：沿深红嵌板中线一排 15 个
    mid = arch_curve(ARCH_T * 0.5, 14)
    L['arch_bulbs'] = [[round(x, 4), round(y, 4), round(COL_Z - ARCH_D / 2 - 0.012, 4)] for x, y in mid]
    L['logo'] = [0.0, round(ARCH_Y0 + ARCH_T + ARCH_RISE + 0.135, 4), round(COL_Z - 0.06, 4)]


def drape(name, W, H, folds, amp, col_peak, col_valley, uvs=2.4, hem=False, seed=0.0):
    """有厚度的丝绒褶皱布（原点在左上角，向 +X 展开、向 -Y 垂下）。
    用户：幕布要有质感、不要廉价感——所以：褶子间距 / 深浅不规则（真布不会每道一样）、顶部收在挂杆上（褶浅、密）、往下自然放开；
    顶点色按每个顶点所在的褶峰 / 褶谷平滑过渡（原来按整列面片涂色，看着是一条条硬色带）；网格加密，圆润不出棱；
    hem=True：下摆一圈金色编带 + 细流苏"""
    NU, NV, T = int(folds * 26), 40, 0.006
    b = B(uv_scale=uvs)
    def phase(u):                       # 褶子间距不均：相位上叠一点慢变化
        return u * folds * math.tau + 0.55 * math.sin(u * 2.3 * math.tau + 1.1 + seed) + 0.25 * math.sin(u * 5.1 * math.tau + 0.4 + seed * 1.7)
    def amp_u(u):                       # 每道褶深浅不一
        return amp * (0.82 + 0.22 * math.sin(u * 3.7 * math.tau + 2.0 + seed) + 0.1 * math.sin(u * 7.3 * math.tau + seed * 0.6))
    def gather(v):                      # 顶上 16% 藏在幕檐后面：褶很浅（幕檐波浪最低垂到这里，鼓出来会碰到）；再往下 14% 慢慢放开
        k = min(1.0, max(0.0, (v - 0.16) / 0.14)); return 0.12 + 0.88 * k * k * (3 - 2 * k)
    def edge(u):                        # 左右两边各 12% 褶子收浅：外边贴着柱子、里边是合幕的中缝（不碰柱子）
        k = min(1.0, min(u, 1 - u) / 0.12); return 0.2 + 0.8 * k * k * (3 - 2 * k)
    def depth(u, v):
        sn = math.sin(phase(u))
        return amp_u(u) * (0.72 + 0.5 * v) * gather(v) * edge(u) * math.copysign(abs(sn) ** 0.6, sn)
    def shade(u, v):
        k = 0.5 + 0.5 * math.sin(phase(u) + math.pi * 0.5)
        k = k ** 1.3
        c = tuple(col_valley[i] + (col_peak[i] - col_valley[i]) * k for i in range(3))
        dim = 0.86 + 0.14 * min(1.0, v / 0.25)                 # 挂杆下面一小段略暗（被幕檐挡光）
        return tuple(x * dim for x in c)
    grid = {}; cols = {}
    for side in (-1, 1):
        for i in range(NU + 1):
            for j in range(NV + 1):
                u, v = i / NU, j / NV
                grid[(side, i, j)] = b.bm.verts.new((u * W, -v * H, depth(u, v) + side * T * 0.5))
                cols[(side, i, j)] = shade(u, v)
    def facev(keys, flip=False):
        ks = keys[::-1] if flip else keys
        f = b.bm.faces.new([grid[k] for k in ks])
        for l, k in zip(f.loops, ks): c = cols[k]; l[b.col] = (c[0], c[1], c[2], 1.0)
    for i in range(NU):
        for j in range(NV):
            facev([(1, i, j), (1, i + 1, j), (1, i + 1, j + 1), (1, i, j + 1)])
            facev([(-1, i, j + 1), (-1, i + 1, j + 1), (-1, i + 1, j), (-1, i, j)])
    for i in range(NU):
        for j in (0, NV):
            q = [(-1, i, j), (-1, i + 1, j), (1, i + 1, j), (1, i, j)]
            facev(q, flip=(j != 0))
    for j in range(NV):
        for i in (0, NU):
            q = [(-1, i, j), (1, i, j), (1, i, j + 1), (-1, i, j + 1)]
            facev(q, flip=(i != 0))
    if hem:
        # 金色编带：沿下摆一条扁圆管（跟着褶子走），下面一排细流苏
        y = -H - 0.004
        for i in range(0, NU):
            u0, u1 = i / NU, (i + 1) / NU
            x0, x1 = u0 * W, u1 * W; z0, z1 = depth(u0, 1.0), depth(u1, 1.0)
            ring0 = [(x0, y + math.sin(k / 8 * math.tau) * 0.006, z0 + math.cos(k / 8 * math.tau) * 0.0065) for k in range(8)]
            ring1 = [(x1, y + math.sin(k / 8 * math.tau) * 0.006, z1 + math.cos(k / 8 * math.tau) * 0.0065) for k in range(8)]
            vs0 = [b.bm.verts.new(p) for p in ring0]; vs1 = [b.bm.verts.new(p) for p in ring1]
            for k in range(8):
                kk = (k + 1) % 8
                b.face((vs0[k], vs0[kk], vs1[kk], vs1[k]), GOLD)
            if i == 0: b.face(vs0[::-1], GOLD)
            if i == NU - 1: b.face(vs1, GOLD)
        for i in range(0, NU + 1, 2):
            u = i / NU; x = u * W; z = depth(u, 1.0)
            L_ = 0.016 + 0.004 * math.sin(i * 1.7)
            strand = [(x - 0.0016, z - 0.0016), (x + 0.0016, z - 0.0016), (x + 0.0016, z + 0.0016), (x - 0.0016, z + 0.0016)]
            b.prism(strand, y - 0.006 - L_, y - 0.004, GOLD)
            knot = [(x + math.cos(k / 8 * math.tau) * 0.0034, z + math.sin(k / 8 * math.tau) * 0.0034) for k in range(8)]
            b.prism(knot, y - 0.006 - L_ - 0.004, y - 0.006 - L_, GOLD)
    b.finish(name)


def tieback():
    """束幕金绳：幕布拉开后挂在幕布正前方的一段垂弧金绳 + 中间一颗流苏（Unity 里幕布开到位才弹出来）。
    原点在弧的中点；弧宽 0.24、下垂 0.035，绳粗 0.007；流苏 0.07 长。整个在幕布前面（幕布最前 z≈0.043），不碰幕布"""
    b = B()
    Wd, sag, r = 0.24, 0.035, 0.0068
    N = 28
    pts = []
    for i in range(N + 1):
        t = i / N; x = (t - 0.5) * Wd; y = -sag * (1 - (2 * t - 1) ** 2)
        pts.append((x, y))
    prev = None
    for i, (x, y) in enumerate(pts):
        tw = i * 0.9                                         # 绳子是拧的：截面上的点绕着转，顶点色深浅交替出“麻花”
        ring = []
        for k in range(10):
            a = k / 10 * math.tau
            ring.append(b.bm.verts.new((x, y + math.sin(a) * r, math.cos(a) * r)))
        if prev:
            for k in range(10):
                kk = (k + 1) % 10
                c = GOLD if ((k + i) // 2) % 2 == 0 else (0.82, 0.6, 0.22)
                b.face((prev[k], prev[kk], ring[kk], ring[k]), c)
        else: b.face(ring[::-1], GOLD)
        prev = ring
    b.face(prev, GOLD)
    # 流苏：绳结（小球）+ 帽 + 一束细穗
    cy = -sag - 0.004
    knot = [(math.cos(k / 12 * math.tau) * 0.011, math.sin(k / 12 * math.tau) * 0.011) for k in range(12)]
    b.prism(knot, cy - 0.016, cy, GOLD)
    cap = [(math.cos(k / 12 * math.tau) * 0.014, math.sin(k / 12 * math.tau) * 0.014) for k in range(12)]
    b.prism(cap, cy - 0.026, cy - 0.016, (0.82, 0.6, 0.22))
    for k in range(12):
        a = k / 12 * math.tau; x = math.cos(a) * 0.01; z = math.sin(a) * 0.01
        L_ = 0.045 + 0.006 * math.sin(k * 2.1)
        strand = [(x - 0.0017, z - 0.0017), (x + 0.0017, z - 0.0017), (x + 0.0017, z + 0.0017), (x - 0.0017, z + 0.0017)]
        b.prism(strand, cy - 0.026 - L_, cy - 0.026, GOLD)
    b.finish('Th_TieBack', bevel=0.0)


def backdrop():
    # 后幕：深紫丝绒（整宽、静止），挂在背后
    W = 2 * (COL_X - 0.03); H = ARCH_Y0 + ARCH_RISE * 0.6 - TOP_Y
    drape('Th_Backdrop', W, H, 9.0, 0.022, (0.52, 0.24, 0.5), (0.18, 0.06, 0.2), uvs=2.0)
    L['backdrop'] = [-W / 2, TOP_Y + H, BACK_Z - 0.04]
    # 吊饰：几颗立体金星 + 一弯月亮，用细线吊在拱下
    s = B()
    hang = [(-0.3, 0.30, 0.03), (-0.12, 0.42, 0.022), (0.2, 0.36, 0.026), (0.36, 0.48, 0.02)]
    for x, drop, r0 in hang:
        y = ARCH_Y0 + math.sin((x + COL_X + 0.07) / (2 * COL_X + 0.14) * math.pi) * ARCH_RISE - drop
        star = []
        for k in range(10):
            a = math.pi / 2 + k * math.pi / 5; rr = r0 if k % 2 == 0 else r0 * 0.45
            star.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
        s.slab_xy(star[::-1], BACK_Z - 0.12, BACK_Z - 0.105, GOLD)
    s.finish('Th_Stars', bevel=0.002)
    # 月亮（用户：月亮形状有点奇怪）：标准月牙 = 大圆减去往右偏的小圆，两个交点就是两个尖；
    # 截面是鼓鼓的扁椭圆（像布偶 / 软陶月亮），中间最厚、往两个尖平滑收细，不再是一块平板
    moon = B()
    mx, my = 0.05, ARCH_Y0 + ARCH_RISE - 0.24
    R, cx, cy, rin = 0.058, 0.026, 0.01, 0.05
    zc, depth = BACK_Z - 0.112, 0.75
    def t_in(a):                                  # 从大圆圆心沿 a 方向，碰到小圆边界的距离
        ux, uy = math.cos(a), math.sin(a); d = ux * cx + uy * cy
        return d + math.sqrt(max(0.0, d * d - (cx * cx + cy * cy) + rin * rin))
    def tip(a_in, a_out):                         # 二分找月牙尖（t_in = R）
        for _ in range(60):
            m = (a_in + a_out) / 2
            if t_in(m) < R: a_in = m
            else: a_out = m
        return (a_in + a_out) / 2
    a_mid = math.atan2(-cy, -cx)                  # 最厚处：背对小圆的方向
    a0, a1 = tip(a_mid, a_mid - math.pi), tip(a_mid, a_mid + math.pi)
    N, K = 40, 12
    def ptxy(a, rho): return (mx + math.cos(a) * rho, my + math.sin(a) * rho)
    x0, y0 = ptxy(a0, R); x1, y1 = ptxy(a1, R)
    cap0 = moon.bm.verts.new((x0, y0, zc)); cap1 = moon.bm.verts.new((x1, y1, zc))
    rings = []
    for i in range(1, N):
        u = i / N; a = a0 + (a1 - a0) * u
        ti = t_in(a); rm, hw = (R + ti) / 2, (R - ti) / 2
        ring = []
        for k in range(K):
            ph = k / K * math.tau
            x, y = ptxy(a, rm + hw * math.cos(ph))
            ring.append(moon.bm.verts.new((x, y, zc + depth * hw * math.sin(ph))))
        rings.append(ring)
    MOON = (1.0, 0.88, 0.52)
    for ra, rb in zip(rings[:-1], rings[1:]):
        for k in range(K): moon.face((ra[k], ra[(k + 1) % K], rb[(k + 1) % K], rb[k]), MOON)
    for k in range(K):
        moon.face((cap0, rings[0][(k + 1) % K], rings[0][k]), MOON)
        moon.face((rings[-1][k], rings[-1][(k + 1) % K], cap1), MOON)
    moon.finish('Th_Moon')
    strings = []
    for x, drop, r0 in hang + [(mx - 0.01, 0.24 - 0.06, 0.0)]:
        top = ARCH_Y0 + math.sin((x + COL_X + 0.07) / (2 * COL_X + 0.14) * math.pi) * ARCH_RISE
        strings.append([x, top, top - drop + r0])
    L['strings'] = [[round(a, 4) for a in v] + [round(BACK_Z - 0.112, 4)] for v in strings]


def curtain():
    # 一片幕布：原点在外侧上角，向 +X 展开宽 W；4.5 道深而圆的大褶 + 下摆金流苏（Unity 里按 X 缩放拉开 / 合上）
    # 宽 0.44：两片合上时中缝只交叠 0.5 cm（右片比左片靠后 8 mm，前后叠着、不互相穿）；外边往里收到柱子外面；整片挂在幕檐后面
    W, H = COL_X - 0.06, ARCH_Y0 - TOP_Y - 0.046                # 下摆 + 流苏离台面 1 cm（原来流苏扎进台面）
    drape('Th_Curtain', W, H, 3.4, 0.033, VELVET, VELVET2, uvs=1.6, hem=True, seed=0.7)
    # 挂在幕檐后面（幕檐 z 0.088..0.104）、拱梁下沿之下（不插进拱梁）
    L['curtain_L'] = [-COL_X + 0.065, ARCH_Y0 - 0.006, COL_Z - 0.047]
    L['curtain_R'] = [COL_X - 0.065, ARCH_Y0 - 0.006, COL_Z - 0.039]
    L['curtain_width'] = W


def valance():
    # 波浪幕檐：沿顶梁内侧挂一圈 6 个弧形垂幔（丝绒）+ 下沿金色流苏珠
    N, D = 6, 0.07
    x0, x1 = -COL_X + 0.02, COL_X - 0.02
    b = B(uv_scale=2.4)
    NU = 120
    top, bot = [], []
    for i in range(NU + 1):
        u = i / NU; x = x0 + (x1 - x0) * u
        yt = ARCH_Y0 + math.sin((x + COL_X + 0.07) / (2 * COL_X + 0.14) * math.pi) * ARCH_RISE + 0.004
        sc = abs(math.sin(u * N * math.pi))
        yb = min(yt - 0.035, ARCH_Y0 + 0.012) - D * sc        # 拱中间高：幕檐跟着往下多垂一截，盖住幕布顶端（用户：幕布上面像缺了一块）
        top.append((x, yt)); bot.append((x, yb))
    poly = top + bot[::-1]
    b.slab_xy(poly[::-1], COL_Z - ARCH_D / 2 - 0.022, COL_Z - ARCH_D / 2 - 0.006, VELVET)
    b.finish('Th_Valance')
    f = B()
    for i in range(N * 6 + 1):
        u = i / (N * 6); x = x0 + (x1 - x0) * u
        yt = ARCH_Y0 + math.sin((x + COL_X + 0.07) / (2 * COL_X + 0.14) * math.pi) * ARCH_RISE + 0.004
        yb = min(yt - 0.035, ARCH_Y0 + 0.012) - D * abs(math.sin(u * N * math.pi))
        z = COL_Z - ARCH_D / 2 - 0.016
        bead = []
        for k in range(10):
            a = k / 10 * math.tau
            bead.append((x + math.cos(a) * 0.006, yb - 0.008 + math.sin(a) * 0.008))
        f.slab_xy(bead[::-1], z - 0.004, z + 0.004, GOLD)
    f.finish('Th_Fringe')


def main():
    P.reset()
    stage(); foot_socket(); column(); arch(); backdrop(); curtain(); valance(); tieback()
    L['top_y'] = TOP_Y; L['half_w'] = HALF_W; L['front_b'] = FRONT_B; L['back_z'] = BACK_Z
    json.dump(L, open(os.path.join(OUT, 'Theater_layout.json'), 'w'), indent=1)
    print('THEATER', json.dumps(P.report, ensure_ascii=False))


main()
