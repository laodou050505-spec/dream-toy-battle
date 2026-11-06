"""第 5 章「发条工厂」精细程序化部件（Blender 里运行）：
  Blender -b -P Tools/blender_w5_set.py -- <输出目录>

  W5_Gear_L/M/S      齿轮（齿形轮廓 + 辐条孔 + 轮毂孔，曲线挤出 + 圆角倒角；原点在轴心，轴朝 Unity +Z），可单独转动
  W5_Spring          螺旋弹簧（原点在底部，沿 +Y）
  W5_GearArch        门框：沿拱形交错排布大小齿轮 + 扁平发条盘簧 + 铆钉
  W5_Pipes           后墙前的铜管：带弯头、法兰圈、棒棒糖阀门手轮
  W5_ConveyorFrame   传送带支架：两侧轨道 + 8 根滚筒 + 4 条腿 + 底横撑（原点在中心地面，沿 X 方向长 1.7 m）
  W5_ConveyorBelt    传送带皮带环（UV 沿长度方向，材质偏移即可滚动）
  W5_Floor           粉彩棋盘地砖（每块倒角小方砖），T_BeltSlats.png 皮带板条贴图
全部封闭流形，顶点色 sRGB；坐标按 Unity 约定（门面 z=0，门后世界在 -z）。
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
TEX = os.path.join(os.path.dirname(os.path.dirname(OUT.rstrip('/'))), 'Textures')
os.makedirs(TEX, exist_ok=True)
U = P.U
rnd = random.Random(20261031)
W, H = 1.3, 1.8
R_IN = W / 2
SPRING = H - R_IN
MINT, PEACH, PINK, LEMON, LAV, BLUE = (0.6, 0.9, 0.78), (1.0, 0.76, 0.62), (1.0, 0.66, 0.78), (1.0, 0.9, 0.55), (0.78, 0.68, 1.0), (0.6, 0.78, 1.0)
BRASS, COPPER = (0.95, 0.76, 0.42), (0.93, 0.6, 0.45)


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    return bm


def curve_mesh(splines, extrude, bevel, res=2):
    """多条闭合多段线（XY 平面，第一条外轮廓，其余为孔）→ 填充 + 挤出 + 倒角 → bmesh（正面朝 +Z，居中于 z=0）"""
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


def circle(r, n=32, cx=0.0, cy=0.0, rev=False):
    pts = [(cx + math.cos(i / n * math.tau) * r, cy + math.sin(i / n * math.tau) * r) for i in range(n)]
    return pts[::-1] if rev else pts


def gear_outline(R, teeth, depth):
    pts = []
    for t in range(teeth):
        a0 = t / teeth * math.tau
        for k, (fa, rr) in enumerate([(0.0, R - depth), (0.18, R - depth), (0.3, R), (0.5, R), (0.62, R - depth), (1.0, R - depth)]):
            if k == 5: continue
            # 齿顶两侧加一个点做圆角
            a = a0 + fa / teeth * math.tau
            pts.append((math.cos(a) * rr, math.sin(a) * rr))
            if k == 2 or k == 3:
                a2 = a + (0.06 if k == 2 else -0.06) / teeth * math.tau
    return pts


def gear(R, teeth, thick=0.035, spokes=5, res=2):
    depth = min(0.045, R * 0.18)
    splines = [gear_outline(R, teeth, depth)]
    hub = R * 0.18
    splines.append(circle(hub * 0.55, 20, rev=True))                    # 轴孔
    if spokes and R > 0.1:
        rr = (R - depth) * 0.58; hr = (R - depth - hub) * 0.26
        for s in range(spokes):
            a = s / spokes * math.tau + math.pi / spokes
            splines.append(circle(hr, 20, math.cos(a) * rr, math.sin(a) * rr, rev=True))
    bm = curve_mesh(splines, thick / 2, min(0.008, thick * 0.25), res)
    # 轮毂凸台
    boss = P.lathe([(0, -thick * 0.9), (hub, -thick * 0.9), (hub * 1.05, -thick * 0.7), (hub * 1.05, thick * 0.7), (hub, thick * 0.9), (hub * 0.62, thick * 0.9), (hub * 0.58, thick * 0.5), (0, thick * 0.5)], 24)
    P.merge(bm, boss)
    return bm


def to_unity_front(bm):
    """把 XY 平面、正面 +Z 的部件转到 Unity：正面朝 +Z（门/玩家）→ Blender -Y"""
    m = Matrix.Rotation(math.radians(90), 4, 'X')            # Blender: +Z → -Y
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def w5_gear(R, teeth, col):
    return color_bm(to_unity_front(gear(R, teeth)), col)


def w5_spring(h=0.32, r=0.05, turns=8, wire=0.008):
    pts = []
    n = turns * 24
    for i in range(n + 1):
        t = i / n; a = t * turns * math.tau
        pts.append(U(math.cos(a) * r, 0.01 + t * (h - 0.02), math.sin(a) * r))
    return color_bm(P.tube(pts, wire, seg=8), BRASS)


def flat_spiral(r, turns=4, wire=0.007, thick=0.012):
    """发条盘簧：平面阿基米德螺线扫成扁管（XY 平面）"""
    pts = []
    n = int(turns * 30)
    for i in range(n + 1):
        t = i / n; a = t * turns * math.tau
        rr = r * (0.18 + 0.82 * t)
        pts.append((math.cos(a) * rr, math.sin(a) * rr, 0))
    bm = P.tube(pts, wire, seg=6)
    for v in bm.verts: v.co.z *= thick / (wire * 2)
    return bm


def arch_point(t_len, off):
    """沿拱形中心线（门洞外 off 米）走 t_len 米处的点 (x, y)"""
    r = R_IN + off
    side = SPRING
    arc = math.pi * r
    if t_len < side: return (r, t_len)
    if t_len < side + arc:
        a = (t_len - side) / r; return (math.cos(a) * r, SPRING + math.sin(a) * r)
    return (-r, SPRING - (t_len - side - arc))


def w5_gear_arch():
    bm = bmesh.new()
    off = 0.16
    total = 2 * SPRING + math.pi * (R_IN + off)
    t = 0.12; i = 0
    cols = [MINT, PEACH, BRASS, LAV, PINK, LEMON, BLUE]
    while t < total - 0.08:
        x, y = arch_point(t, off + (0.03 if i % 2 else -0.03))
        R = [0.16, 0.11, 0.13, 0.09][i % 4]
        g = gear(R, max(8, int(R * 70)), thick=0.03 + 0.01 * (i % 2), res=1)
        bmesh.ops.transform(g, matrix=Matrix.Rotation(rnd.uniform(0, math.tau), 4, 'Z'), verts=g.verts)
        to_unity_front(g)
        bmesh.ops.translate(g, vec=U(x, max(y, R * 0.95), 0.03 + (i % 3) * 0.02), verts=g.verts)
        P.merge(bm, color_bm(g, cols[i % len(cols)]))
        # 齿轮间隙里偶尔插一个发条盘簧
        if i % 3 == 1:
            sx, sy = arch_point(t + R * 0.9, off + 0.09)
            sp = flat_spiral(0.06, 3.5)
            to_unity_front(sp)
            bmesh.ops.translate(sp, vec=U(sx, max(sy, 0.07), 0.1), verts=sp.verts)
            P.merge(bm, color_bm(sp, COPPER))
        t += R * 1.55; i += 1
    return bm


def w5_pipes():
    """后墙前（z≈-6.2）的铜管：两条从地面升起、拐弯、横走的管路，法兰圈 + 两个棒棒糖阀门手轮"""
    bm = bmesh.new()
    def pipe(path, r=0.045):
        # 弯头处用圆角：在拐点前后各取点做二次贝塞尔
        pts = []
        for i, p in enumerate(path):
            p = Vector(p)
            if 0 < i < len(path) - 1:
                a, c = Vector(path[i - 1]), Vector(path[i + 1])
                p0 = p + (a - p).normalized() * 0.12; p2 = p + (c - p).normalized() * 0.12
                for k in range(9):
                    t = k / 8; pts.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p + t * t * p2)
            else:
                pts.append(p)
        dense = []
        for a, b in zip(pts[:-1], pts[1:]):
            n = max(1, int((b - a).length / 0.05))
            for k in range(n): dense.append(a.lerp(b, k / n))
        dense.append(pts[-1])
        P.merge(bm, color_bm(P.tube([U(*q) for q in dense], r, seg=14), COPPER))
        # 法兰圈：每段直管中点
        for a, b in zip(path[:-1], path[1:]):
            a, b = Vector(a), Vector(b); m = (a + b) / 2; d = (b - a).normalized()
            ring = P.torus(r * 1.15, r * 0.32, seg=18, sides=6)
            q = Vector((0, 0, 1)).rotation_difference((U(*d) - U(0, 0, 0)).normalized())
            bmesh.ops.transform(ring, matrix=Matrix.Translation(U(*m)) @ q.to_matrix().to_4x4(), verts=ring.verts)
            P.merge(bm, color_bm(ring, BRASS))
    pipe([(-3.4, 0.0, -6.0), (-3.4, 1.7, -6.0), (-1.2, 1.7, -6.0), (-1.2, 2.6, -6.0)])
    pipe([(3.6, 0.0, -5.6), (3.6, 1.2, -5.6), (1.6, 1.2, -5.6), (1.6, 0.0, -5.6)], r=0.04)
    # 棒棒糖阀门手轮：螺旋糖盘 + 短杆，装在管子正面
    for (x, y, z), col in [((-2.3, 1.7, -5.92), PINK), ((2.6, 1.2, -5.52), LAV)]:
        disc = curve_mesh([circle(0.09, 40)], 0.012, 0.008)
        to_unity_front(disc)
        lay = disc.loops.layers.color.new('Col')
        for f in disc.faces:
            for l in f.loops:
                c = l.vert.co; a = math.atan2(c.z, c.x); rr = math.hypot(c.x, c.z)
                stripe = math.sin(a * 1 + rr * 70) > 0
                l[lay] = (col if stripe else (1.0, 0.97, 0.95)) + (1.0,)
        bmesh.ops.translate(disc, vec=U(x, y, z + 0.09), verts=disc.verts)
        P.merge(bm, disc)
        stem = P.tube([U(x, y, z), U(x, y, z + 0.08)], 0.012, seg=10)
        P.merge(bm, color_bm(stem, BRASS))
    return bm


def w5_conveyor_frame(L=1.7, Wd=0.34, Hh=0.36):
    bm = bmesh.new()
    for side in (-1, 1):                                  # 两侧轨道（圆角长条，柠檬黄）
        rail = P.ubox(-L / 2 - 0.05, L / 2 + 0.05, Hh - 0.07, Hh + 0.035, side * Wd / 2 - 0.022, side * Wd / 2 + 0.022, bevel=0.012)
        P.merge(bm, color_bm(rail, LEMON))
        for lx in (-L / 2 + 0.08, L / 2 - 0.08):          # 腿
            leg = P.ubox(lx - 0.025, lx + 0.025, 0.0, Hh - 0.06, side * Wd / 2 - 0.02, side * Wd / 2 + 0.02, bevel=0.008)
            P.merge(bm, color_bm(leg, LAV))
            foot = P.lathe([(0, 0), (0.045, 0), (0.045, 0.02), (0.03, 0.03), (0, 0.03)], 16)
            for v in foot.verts: v.co = U(lx + v.co.x, v.co.z, side * Wd / 2 + v.co.y)
            bmesh.ops.recalc_face_normals(foot, faces=foot.faces)
            P.merge(bm, color_bm(foot, BRASS))
    for lx in (-L / 2 + 0.08, L / 2 - 0.08):              # 底横撑
        P.merge(bm, color_bm(P.tube([U(lx, 0.1, -Wd / 2), U(lx, 0.1, Wd / 2)], 0.012, seg=8), LAV))
    for k in range(8):                                    # 滚筒（两端带黄铜帽）
        x = -L / 2 + 0.04 + k * (L - 0.08) / 7
        P.merge(bm, color_bm(P.tube([U(x, Hh - 0.035, -Wd / 2 + 0.03), U(x, Hh - 0.035, Wd / 2 - 0.03)], 0.03, seg=14), (0.92, 0.92, 0.96)))
        for side in (-1, 1):
            cap = P.tube([U(x, Hh - 0.035, side * (Wd / 2 - 0.022)), U(x, Hh - 0.035, side * (Wd / 2 + 0.03))], 0.016, seg=10)
            P.merge(bm, color_bm(cap, BRASS))
    return bm


def w5_conveyor_belt(L=1.7, Wd=0.34, Hh=0.36):
    """皮带环：沿长度方向的圆角跑道截面（上下两层 + 两端半圆），带厚度；UV.u = 弧长（米）×2"""
    bm = bmesh.new(); lay = bm.loops.layers.color.new('Col'); uvl = bm.loops.layers.uv.new('UVMap')
    r = 0.035; th = 0.008; yc = Hh - 0.035
    path = []
    n_line, n_arc = 40, 12
    for i in range(n_line): path.append((-L / 2 + 0.04 + (L - 0.08) * i / n_line, yc + r, 0))
    for i in range(n_arc): a = math.pi / 2 - math.pi * i / n_arc; path.append((L / 2 - 0.04 + math.cos(a) * r, yc + math.sin(a) * r, 0))
    for i in range(n_line): path.append((L / 2 - 0.04 - (L - 0.08) * i / n_line, yc - r, 0))
    for i in range(n_arc): a = -math.pi / 2 - math.pi * i / n_arc; path.append((-L / 2 + 0.04 + math.cos(a) * r, yc + math.sin(a) * r, 0))
    m = len(path)
    arcl = [0.0]
    for i in range(1, m + 1): arcl.append(arcl[-1] + math.dist(path[i % m], path[i - 1]))
    rings = []
    for i, (x, y, _) in enumerate(path):
        nx_, ny_ = x - (L / 2 - 0.04 if x > L / 2 - 0.04 else (-L / 2 + 0.04 if x < -L / 2 + 0.04 else x)), y - yc
        l = math.hypot(nx_, ny_) or 1.0; nx_, ny_ = nx_ / l, ny_ / l
        ring = []
        for (dz, dn) in ((-Wd / 2 + 0.035, 0), (Wd / 2 - 0.035, 0), (Wd / 2 - 0.035, th), (-Wd / 2 + 0.035, th)):
            ring.append(bm.verts.new(U(x + nx_ * dn, y + ny_ * dn, dz)))
        rings.append(ring)
    for i in range(m):
        a, b = rings[i], rings[(i + 1) % m]
        for k in range(4):
            f = bm.faces.new((a[k], b[k], b[(k + 1) % 4], a[(k + 1) % 4]))
            u0, u1 = arcl[i] * 2.0, arcl[i + 1] * 2.0
            vv = [0, 1, 1, 0]
            for l, uv in zip(f.loops, [(u0, vv[k]), (u1, vv[k]), (u1, vv[(k + 1) % 4]), (u0, vv[(k + 1) % 4])]):
                l[uvl].uv = uv; l[lay] = (1, 1, 1, 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def belt_texture(path, size=256):
    import numpy as np
    img = np.ones((size, size, 3), np.float32)
    xx = np.arange(size)
    slat = ((xx // (size // 8)) % 2 == 0)
    a = np.array(MINT, np.float32); b = np.array((0.98, 0.95, 0.9), np.float32)
    img[:, :] = np.where(slat[None, :, None], a, b)
    edge = (xx % (size // 8) < 3)
    img[:, edge] *= 0.85
    im = bpy.data.images.new('belt', size, size, alpha=False)
    rgba = np.concatenate([img[::-1], np.ones((size, size, 1), np.float32)], -1)
    im.pixels.foreach_set(rgba.ravel()); im.filepath_raw = path; im.file_format = 'PNG'; im.save()


def w5_floor():
    """粉彩棋盘地砖：8.4 m × 7 m，每块 0.42 m 倒角方砖，远处（z<-6.6）一整块底板挡住缝"""
    bm = bmesh.new()
    s = 0.42
    cols = [(0.96, 0.76, 0.66), (0.93, 0.86, 0.8)]      # 压低亮度，室内灯光下不过曝
    for i in range(-10, 10):
        for j in range(0, 17):
            x0, z0 = 0.3 + i * s, -0.1 - j * s
            tile = P.ubox(x0 + 0.004, x0 + s - 0.004, -0.04, 0.0, z0 - s + 0.004, z0 - 0.004, bevel=0.008)
            c = cols[(i + j) % 2]
            P.merge(bm, color_bm(tile, tuple(min(1, v * (0.98 + 0.04 * rnd.random())) for v in c)))
    base = P.ubox(0.3 - 10 * s - 2, 0.3 + 10 * s + 2, -0.2, -0.03, -0.1 - 17 * s - 3, 0.2)
    P.merge(bm, color_bm(base, (0.9, 0.78, 0.7)))
    return bm


def w5_clutter():
    """地上散落的小零件：小齿轮（平放）、六角螺母、螺栓、小弹簧、彩色珠子；避开传送带、机器和小屋"""
    bm = bmesh.new()
    avoid = [(0.3, -3.0, 1.25), (1.4, -1.7, 0.35), (-1.0, -1.5, 0.35), (-0.6, -2.1, 0.25), (-1.45, -3.6, 0.6), (2.35, -2.7, 0.55), (-2.5, -4.7, 0.5), (-1.9, -1.9, 0.2), (1.95, -1.7, 0.2), (-2.45, -2.45, 0.45)]
    pts = []
    def ok(x, z, pad):
        if z > -0.4: return False
        if any(math.hypot(x - ax, z - az) < r + pad for ax, az, r in avoid): return False
        return all(math.hypot(x - px, z - pz) > pad + pr for px, pz, pr in pts)
    cols = [MINT, PEACH, PINK, LEMON, LAV, BLUE, BRASS, COPPER]
    kinds = ['gear'] * 16 + ['nut'] * 16 + ['bolt'] * 10 + ['spring'] * 8 + ['bead'] * 18
    rnd.shuffle(kinds)
    for kind in kinds:
        for _ in range(200):
            z = -0.5 - rnd.random() ** 1.3 * 4.5; x = rnd.uniform(-3.0, 3.4)
            if ok(x, z, 0.14): break
        else:
            continue
        pts.append((x, z, 0.1))
        c = rnd.choice(cols)
        if kind == 'gear':
            g = gear(rnd.uniform(0.07, 0.13), rnd.choice((8, 9, 10, 12)), thick=0.02, spokes=0, res=1)
            bmesh.ops.transform(g, matrix=Matrix.Translation(U(x, 0.008, z)) @ Matrix.Rotation(rnd.uniform(0, 6.28), 4, 'Z'), verts=g.verts)
        elif kind == 'nut':
            r = rnd.uniform(0.03, 0.05)
            g = curve_mesh([[(math.cos(i / 6 * math.tau) * r, math.sin(i / 6 * math.tau) * r) for i in range(6)], circle(r * 0.45, 16, rev=True)], r * 0.4, r * 0.12, 1)
            bmesh.ops.transform(g, matrix=Matrix.Translation(U(x, r * 0.5, z)) @ Matrix.Rotation(rnd.uniform(0, 6.28), 4, 'Z'), verts=g.verts)
        elif kind == 'bolt':
            L = rnd.uniform(0.1, 0.15)
            g = P.lathe([(0, 0), (0.022, 0), (0.022, L), (0.036, L), (0.036, L + 0.022), (0.03, L + 0.03), (0, L + 0.03)], 12)
            bmesh.ops.transform(g, matrix=Matrix.Translation(U(x, 0.036, z)) @ Matrix.Rotation(rnd.uniform(0, 6.28), 4, 'Z') @ Matrix.Rotation(math.radians(90), 4, 'X'), verts=g.verts)
        elif kind == 'spring':
            g = w5_spring(h=rnd.uniform(0.12, 0.2), r=0.035, turns=6, wire=0.007)
            bmesh.ops.translate(g, vec=U(x, 0, z), verts=g.verts)
        else:
            r = rnd.uniform(0.025, 0.04)
            g = P.lathe([(0, 0), (r * 0.7, r * 0.15), (r, r * 0.7), (r * 0.7, r * 1.3), (0, r * 1.4)], 12)
            bmesh.ops.translate(g, vec=U(x, 0, z), verts=g.verts)
        P.merge(bm, color_bm(g, c) if kind != 'spring' else g)
    return bm


def main():
    P.reset()
    belt_texture(os.path.join(TEX, 'T_BeltSlats.png'))
    parts = {
        'W5_Gear_L': lambda: w5_gear(0.34, 18, MINT), 'W5_Gear_M': lambda: w5_gear(0.22, 13, PINK), 'W5_Gear_S': lambda: w5_gear(0.13, 9, LEMON),
        'W5_Spring': w5_spring, 'W5_GearArch': w5_gear_arch, 'W5_Pipes': w5_pipes,
        'W5_ConveyorFrame': w5_conveyor_frame, 'W5_ConveyorBelt': w5_conveyor_belt, 'W5_Floor': w5_floor, 'W5_Clutter': w5_clutter,
    }
    for name, fn in parts.items():
        bm = fn()
        ob = P.to_obj(bm, name, smooth=True)
        if name in ('W5_Floor', 'W5_ConveyorFrame') or name.startswith('W5_Gear'):
            try: ob.data.set_sharp_from_angle(angle=math.radians(40))
            except Exception: pass
        bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                 bake_space_transform=True, colors_type='SRGB')
        bpy.data.objects.remove(ob, do_unlink=True)
    print('W5SET', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
