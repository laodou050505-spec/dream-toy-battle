"""六章门框：沿不规则“虫洞”门洞轮廓（Tools/portal_outline.py）生成，贴墙安装（背面 z≈-0.02，向房间一侧凸出）。
  Blender -b -P Tools/blender_portals.py -- <Proc 输出目录>

  Portal_Bricks          W1 糖果积木：两圈错缝积木沿洞口外沿砌一圈（凸点朝房间），偶尔一块翘出来，像墙里挤出来的积木
  W3_FeltArch            W3 鼓鼓的绒布边（复用 blender_w3_set 的扫掠，路径换成不规则轮廓）
  W4_BalloonArch         W4 气球簇（复用 blender_w4_set）
  W6_CandyArch           W6 拐杖糖 + 薄荷糖条（复用 blender_w6_set）
  W5_GearFrame           W5 齿轮门的静止部分：铜色安装板（沿轮廓的圆角扁带）+ 每个齿轮的轴销 + 铆钉 + 发条钥匙
  W5_PG_00..NN           W5 每个齿轮单独一个模型（原点在轴心，一个齿正对本地 +X，齿数全是偶数 → 导出镜像后齿位不变）
  W5_GearTrain.json      齿轮表：位置、齿数、节圆半径、啮合父齿轮（Unity 门面局部坐标）
  Portal/portal_outline_W1..W6.json   门洞轮廓 + 门框中心线（Unity 门洞遮罩 / 涡旋 / 摆件用）
全部封闭流形（proc_lib.check），顶点色 sRGB。
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import portal_outline as PO

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
C = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))          # Unity 向量 → Blender 向量（与 P.U 相同的线性部分）
CI = C.inverted()


def umat(rot3, t):
    """Unity 旋转（3x3，列 = 本地轴在 Unity 里的方向）+ 平移 → Blender 4x4"""
    m = (C @ rot3 @ CI).to_4x4()
    m.translation = U(*t)
    return m


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    return bm


def seat_on_wall(bm, zmin=-0.02):
    """整体沿 Unity z 平移，使最靠墙的点在 z=zmin（Unity z = Blender -y）"""
    zs = [-v.co.y for v in bm.verts]
    dz = zmin - min(zs)
    for v in bm.verts: v.co.y -= dz
    return bm


def path_frames(pts):
    """折线每点的 (位置, 切线, 外法线)"""
    out = []
    for i, p in enumerate(pts):
        a = pts[max(i - 1, 0)]; b = pts[min(i + 1, len(pts) - 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]; L = math.hypot(tx, ty) or 1
        tx, ty = tx / L, ty / L
        out.append((p, (tx, ty), (ty, -tx)))
    return out


def walker(pts):
    L = [0.0]
    for a, b in zip(pts[:-1], pts[1:]): L.append(L[-1] + math.dist(a, b))
    fr = path_frames(pts)
    def at(t):
        t = min(max(t, 0), L[-1])
        for i in range(1, len(L)):
            if L[i] >= t:
                f = (t - L[i - 1]) / max(L[i] - L[i - 1], 1e-9)
                p = (pts[i - 1][0] + (pts[i][0] - pts[i - 1][0]) * f, pts[i - 1][1] + (pts[i][1] - pts[i - 1][1]) * f)
                return p, fr[i][1], fr[i][2]
        return pts[-1], fr[-1][1], fr[-1][2]
    return at, L[-1]


# ───────────────────────── W1 糖果积木 ─────────────────────────
def portal_bricks():
    import blender_w1_set as W1
    rnd = random.Random(2026102701)
    bm = bmesh.new()
    ci = 0
    for course, (off, depth_ofs, hr) in enumerate(((0.075, 0.0, 0.13), (0.205, -0.03, 0.12))):
        at, total = walker(PO.path(1, off))
        t = 0.0 if course == 0 else 0.09                      # 第二圈错缝
        while t < total - 0.04:
            Lb = rnd.choice([0.16, 0.24, 0.24, 0.32])
            Lb = min(Lb, total - t)
            if Lb < 0.06: break
            (x, y), (tx, ty), (nx, ny) = at(t + Lb / 2)
            rgb = W1.jitter(W1.PASTEL[ci % 6]); ci += 1
            dep = 0.2 + rnd.uniform(-0.02, 0.05) + depth_ofs
            pop = rnd.random() < 0.12                         # 偶尔一块被挤出来：更凸、歪一点
            tilt = rnd.uniform(-6, 6) + (rnd.choice([-1, 1]) * rnd.uniform(10, 18) if pop else 0)
            # 本地积木：x = 沿切线长度，y = 进深（凸点朝 +y → 朝房间），z = 径向高度
            b = W1.brick_u(-Lb / 2 + 0.004, Lb / 2 - 0.004, -dep / 2, dep / 2, -hr / 2, hr / 2, rgb, studs=True)
            T = Vector((tx, ty, 0)); Z = Vector((0, 0, 1)); N = Vector((nx, ny, 0))
            rot = Matrix((T, Z, N)).transposed()              # 列 = 本地 x/y/z 在 Unity 的方向
            if rot.determinant() < 0: rot = Matrix((T, Z, -N)).transposed()
            rot = Matrix.Rotation(math.radians(tilt), 3, Vector((0, 0, 1))) @ rot
            # brick_u 用 Unity 坐标建在原点 → 先回到 Unity，再旋转平移
            # 墙面内歪斜 tilt 会让积木靠洞口那侧的角伸进门洞 ≈ (Lb/2)·sin|tilt|：整块沿外法线往外让开这么多（洞口里不能有东西挡住、也不能和发光内沿穿插）
            lift = (Lb / 2) * abs(math.sin(math.radians(tilt))) + 0.004
            m = umat(rot, (x + nx * lift, max(y + ny * lift, hr / 2), dep / 2 + (0.06 if pop else 0) + rnd.uniform(0, 0.025)))
            bmesh.ops.transform(b, matrix=m, verts=b.verts)          # brick_u 已经是 Blender 坐标：m = C·rot·C⁻¹ + 平移
            P.merge(bm, b)
            t += Lb
    # 门脚两侧各一摞积木
    for side in (-1, 1):
        x0 = side * 0.95
        for lv in range(3):
            rgb = W1.jitter(W1.PASTEL[(ci + lv) % 6])
            P.merge(bm, W1.brick_u(x0 - 0.12 + lv * 0.02 * side, x0 + 0.12 + lv * 0.02 * side, lv * 0.12, (lv + 1) * 0.12 - 0.004, 0.0, 0.24 - lv * 0.03, rgb, studs=(lv == 2)))
        ci += 3
    return seat_on_wall(bm)


# ───────────────────────── W1 积木小路（从房间里穿过门洞通到小镇广场）─────────────────────────
PATH_CTRL = [(0.0, 1.4), (0.04, 0.75), (-0.06, 0.05), (-0.14, -0.6), (0.02, -1.25), (0.22, -1.85), (0.14, -2.4)]
PLAZA = (0.12, -2.72, 0.42)                      # 小广场圆心 x, z、半径


def catmull(pts, n_per=40):
    out = []
    P_ = [pts[0]] + pts + [pts[-1]]
    for i in range(1, len(P_) - 2):
        p0, p1, p2, p3 = P_[i - 1], P_[i], P_[i + 1], P_[i + 2]
        for k in range(n_per):
            t = k / n_per; t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(2)))
    out.append(pts[-1])
    return out


def w1_ground_y(x, z):
    """与 Unity 里看到的地面一致：W1 地形 + 真实房间范围内压平（FlattenNearRoom 同一公式）；房间那一侧在溢出草皮上"""
    import blender_w1_set as W1
    if z > 0.02: return 0.012
    h = W1.ground_height(x, z)
    dx = max(0.0, abs(x) - 4.3); dz = max(0.0, max(-0.3 - z, z - 7.0))
    k = min(1.0, math.hypot(dx, dz) / 2.5); k = k * k * (3 - 2 * k)
    return min(h, 0.0) * (1 - k) + h * k


def on_track(x, z, yaw=None, hl=0.0, hw=0.0):
    """道口：砖块（中心 x,z，朝向 yaw，半长 hl 横跨路面、半宽 hw 沿路）只要有一个角 / 中心碰到铁轨（轨枕半宽 0.08 + 1 cm 余量）就不铺"""
    import blender_w1_set as W1
    a, b = W1.TRACK_A, W1.TRACK_B; cx, cz = W1.TRACK_C
    pts = [(x, z)]
    if yaw is not None:
        ca, sa = math.cos(yaw), math.sin(yaw)
        for u in (-hl, hl):
            for w in (-hw, hw):
                pts.append((x + u * ca + w * sa, z - u * sa + w * ca))
    for px, pz in pts:
        q = math.hypot((px - cx) / a, (pz - cz) / b)
        if abs(q - 1) * min(a, b) < 0.09: return True
    return False


def brick_path():
    """返回 (门后世界里的一段, 房间里的一段)。每排两块 2×1 平板错缝铺，砖跟着曲线转向；两侧粉彩路沿；按地面高度和坡度贴地。"""
    import blender_w1_set as W1
    # 小路的凸点用轻量版（10 段、顶面一段倒角）：几百块砖，16 段 + 两段倒角的凸点太费
    def light_stud(cx, cy, cz, r=0.034, h=0.022, up=Vector((0, 0, 1))):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=r, radius2=r, depth=h)
        top = [e for e in bm.edges if all(v.co.z > h / 2 - 1e-6 for v in e.verts)]
        bmesh.ops.bevel(bm, geom=top, offset=r * 0.18, segments=1, affect='EDGES', profile=0.5)
        q = Vector((0, 0, 1)).rotation_difference(up.normalized())
        for v in bm.verts: v.co = q @ (v.co + Vector((0, 0, h / 2 - 0.004))) + Vector((cx, cy, cz))
        return bm
    W1.stud = light_stud
    rnd = random.Random(20261027)
    cream = [(1.0, 0.9, 0.72), (1.0, 0.87, 0.7), (0.99, 0.92, 0.78)]
    curve = catmull(PATH_CTRL)
    at, total = walker(curve)
    world, room = bmesh.new(), bmesh.new()
    W_HALF, ROW = 0.24, 0.122                    # 路宽 0.48（两块 0.24 的砖），每排 0.122
    TH = 0.03

    def put(bm_local, x, z, yaw, tilt_from=None):
        y = w1_ground_y(x, z)
        # 坡度：前后左右取样拟合法线
        e = 0.06
        gx = (w1_ground_y(x + e, z) - w1_ground_y(x - e, z)) / (2 * e)
        gz = (w1_ground_y(x, z + e) - w1_ground_y(x, z - e)) / (2 * e)
        n = Vector((-gx, 1, -gz)).normalized()
        up = Vector((0, 1, 0))
        tilt = up.rotation_difference(n).to_matrix()
        rot = tilt @ Matrix.Rotation(yaw, 3, Vector((0, 1, 0)))
        bmesh.ops.transform(bm_local, matrix=umat(rot, (x, y - 0.006, z)), verts=bm_local.verts)
        P.merge(room if z > 0.02 else world, bm_local)

    def plate(L, Wd, rgb, h=TH):
        # 本地：x 横跨路面（L），z 沿路（Wd），底在 y=0
        return W1.brick_u(-L / 2 + 0.003, L / 2 - 0.003, 0, h, -Wd / 2 + 0.003, Wd / 2 - 0.003, rgb, studs=True, bevel=0.005)

    t = ROW / 2; row = 0
    room_end = None
    while t < total - ROW * 0.4:
        (x, z), (tx, tz), (nx, nz) = at(t)
        if math.hypot(x - PLAZA[0], z - PLAZA[1]) < PLAZA[2] - 0.02: break
        yaw = math.atan2(tx, tz)                 # 本地 +z 对齐路线切线（Unity 绕 y 的角度）
        side = Vector((nx, 0, nz))
        # 错缝：偶数排 [整, 整]，奇数排 [半, 整, 半]
        layout = [(-0.12, 0.24), (0.12, 0.24)] if row % 2 == 0 else [(-0.18, 0.12), (0.0, 0.24), (0.18, 0.12)]
        for off, L in layout:
            px, pz = x + nx * off, z + nz * off
            if on_track(px, pz, yaw, L / 2, ROW / 2): continue        # 道口：铁轨从路面穿过，碰到铁轨的砖不铺
            rgb = W1.jitter(rnd.choice(W1.PASTEL), 0.02) if rnd.random() < 0.09 else W1.jitter(cream[rnd.randrange(3)], 0.02)
            put(plate(L, ROW, rgb), px, pz, yaw)
        # 路沿：两侧 1×1 小方砖（略高），粉彩交替
        for sgn in (-1, 1):
            px, pz = x + nx * sgn * (W_HALF + 0.06), z + nz * sgn * (W_HALF + 0.06)
            if on_track(px, pz, yaw, 0.06, ROW / 2): continue
            if abs(pz) < 0.3: continue           # 门框前后：路沿收在墙洞两边，只让路面穿过门洞（门洞平底只有 0.76 m 宽）
            put(plate(0.12, ROW, W1.jitter(W1.PASTEL[(row + (sgn > 0) * 3) % 6], 0.02), TH + 0.014), px, pz, yaw)
        t += ROW; row += 1
    # 小广场：一圈圈沿切向的砖 + 中心一块圆形大凸点
    cx, cz, R = PLAZA
    r = 0.1
    ring = 0
    while r < R:
        n = max(6, int(2 * math.pi * r / 0.13))
        for i in range(n):
            a = (i + 0.5 * (ring % 2)) / n * math.tau
            px, pz = cx + math.cos(a) * r, cz + math.sin(a) * r
            rgb = W1.jitter(W1.PASTEL[(ring * 2 + (i % 2)) % 6] if ring == 0 or r + 0.13 >= R else cream[(i + ring) % 3], 0.02)
            put(plate(0.12, 2 * math.pi * r / n, rgb, TH + (0.014 if r + 0.13 >= R else 0)), px, pz, -a)
        r += 0.13; ring += 1
    hub = P.lathe([(0, 0), (0.09, 0), (0.09, 0.035), (0.08, 0.045), (0.05, 0.05), (0, 0.05)], 24)
    color_bm(hub, (1.0, 0.72, 0.82))
    hub2 = bmesh.new(); me = bpy.data.meshes.new('h'); hub.to_mesh(me); hub2.from_mesh(me); bpy.data.meshes.remove(me)
    bmesh.ops.transform(hub2, matrix=Matrix.Rotation(math.radians(0), 4, 'X'), verts=hub2.verts)
    for v in hub2.verts: v.co = Vector((v.co.x, v.co.y, v.co.z))
    bmesh.ops.translate(hub2, vec=U(cx, w1_ground_y(cx, cz) - 0.004, cz), verts=hub2.verts)
    P.merge(world, hub2)
    # 房间那一头：最后一排只铺了一块，旁边两块散砖还没铺（像路正往房间里铺）
    (x, z), (tx, tz), (nx, nz) = at(0.0)
    for (dx, dz, yaw, c) in ((0.32, 0.25, 0.6, 2), (-0.3, 0.32, -0.4, 4)):
        put(plate(0.24, ROW, W1.jitter(W1.PASTEL[c], 0.02)), x + dx, z + dz, yaw)
    return world, room


# ───────────────────────── W3 / W4 / W6：复用原脚本，换路径 ─────────────────────────
def patched_path(ch):
    def arch_path(off, n_arc=80, n_side=20):
        n = 2 * n_side + n_arc
        pts = PO.path(ch, off, 0.005)
        step = max(1, len(pts) // max(n, 60))
        out = pts[::step]
        if out[-1] != pts[-1]: out.append(pts[-1])
        return out
    return arch_path


def felt_arch():
    import blender_w3_set as M
    M.arch_path = patched_path(3)
    return seat_on_wall(M.w3_felt_arch())


def balloon_arch():
    import blender_w4_set as M
    M.arch_path = patched_path(4)
    return seat_on_wall(M.w4_balloon_arch(), -0.06)


def candy_arch():
    import blender_w6_set as M
    M.arch_path = patched_path(6)
    return seat_on_wall(M.candy_arch())


# ───────────────────────── W5 齿轮门 ─────────────────────────
MOD = 0.0125                                    # 模数：节圆直径 = 齿数 × 模数（所有齿轮同模数才啮合）
TEETH = [14, 18, 22, 28, 34]
CLEAR = 0.003
W5COL = [(0.6, 0.9, 0.78), (1.0, 0.76, 0.62), (0.95, 0.76, 0.42), (0.78, 0.68, 1.0), (1.0, 0.66, 0.78), (1.0, 0.9, 0.55), (0.6, 0.78, 1.0)]


def pitch_r(n): return n * MOD / 2


def gear_train():
    """沿门洞外沿排一条啮合齿轮链（中心在外扩 0.2 m 的中心线附近左右摆动），再在外侧挂几只分支惰轮。
    返回 [{x, y, n, parent}]（parent=-1 为主动轮）。"""
    rnd = random.Random(55)
    gears = []
    def ok(x, y, n, ignore):
        R = pitch_r(n) + MOD
        for j, g in enumerate(gears):
            if j in ignore: continue
            if math.hypot(g['x'] - x, g['y'] - y) < R + pitch_r(g['n']) + MOD + 0.012: return False
        return y - R > 0.015
    offs = {k: PO.path(5, k, 0.004) for k in (0.17, 0.2, 0.24, 0.28)}
    # 主动轮：右下角，最大
    p0 = offs[0.24][int(len(offs[0.24]) * 0.04)]
    gears.append(dict(x=p0[0], y=max(p0[1], pitch_r(34) + 0.03), n=34, parent=-1))
    cur = 0
    for _ in range(60):
        g = gears[cur]
        placed = False
        for n in rnd.sample(TEETH, len(TEETH)):
            d = pitch_r(g['n']) + pitch_r(n) + CLEAR
            for key in rnd.sample(list(offs), len(offs)):
                pts = offs[key]
                # 沿路线往前找与当前齿轮中心距离 = d 的第一个点（只往前走）
                start = min(range(len(pts)), key=lambda i: math.hypot(pts[i][0] - g['x'], pts[i][1] - g['y']))
                for i in range(start, len(pts)):
                    if math.hypot(pts[i][0] - g['x'], pts[i][1] - g['y']) >= d:
                        # 精确放到距离 d 的圆上
                        ax, ay = pts[i][0] - g['x'], pts[i][1] - g['y']; L = math.hypot(ax, ay)
                        x, y = g['x'] + ax / L * d, g['y'] + ay / L * d
                        if ok(x, y, n, {cur}):
                            gears.append(dict(x=x, y=y, n=n, parent=cur)); placed = True
                        break
                if placed: break
            if placed: break
        if not placed: break
        cur = len(gears) - 1
        # 左下角到地了就停
        if gears[-1]['x'] < 0 and gears[-1]['y'] < 0.35: break
    chain = len(gears)
    # 分支惰轮：挂在链上齿轮的外侧
    for k in range(1, chain - 1, 3):
        g = gears[k]
        ax, ay = g['x'], g['y'] - 0.95; L = math.hypot(ax, ay); ax, ay = ax / L, ay / L      # 大致朝外
        for n in rnd.sample([14, 18, 22], 3):
            d = pitch_r(g['n']) + pitch_r(n) + CLEAR
            for da in (0.0, 0.5, -0.5, 0.9, -0.9):
                c, s = math.cos(da), math.sin(da)
                ux, uy = ax * c - ay * s, ax * s + ay * c
                x, y = g['x'] + ux * d, g['y'] + uy * d
                if ok(x, y, n, {k}):
                    gears.append(dict(x=x, y=y, n=n, parent=k)); break
            else:
                continue
            break
    for i, g in enumerate(gears):
        g['r'] = pitch_r(g['n']); g['col'] = i % len(W5COL)
    return gears


def gear_mesh(n, col, thick):
    import blender_w5_set as W5
    R = pitch_r(n) + MOD
    depth = 2.25 * MOD
    splines = [W5.gear_outline(R, n, depth)]
    hub = max(0.022, R * 0.2)
    splines.append(W5.circle(0.009, 16, rev=True))                       # 轴孔（套在轴销上）
    if R > 0.13:
        rr = (R - depth) * 0.6; hr = (R - depth - hub) * 0.27
        for s in range(5):
            a = s / 5 * math.tau + math.pi / 5
            splines.append(W5.circle(hr, 20, math.cos(a) * rr, math.sin(a) * rr, rev=True))
    # 倒角会把齿形整体往外胖出一圈：模数只有 12.5 mm，倒角大了齿顶会伸进对方齿根、齿侧顶到对方的齿（啮合时互相穿插）→ 倒角只留 1.5 mm
    bm = W5.curve_mesh(splines, thick / 2, 0.0015, 1)
    boss = P.lathe([(0.009, -thick * 0.85), (hub, -thick * 0.85), (hub * 1.05, -thick * 0.6), (hub * 1.05, thick * 0.6), (hub, thick * 0.85), (0.009, thick * 0.85)], 24)
    # boss 带孔 → 车削轮廓首尾不在轴上，是一个环管：首尾相接成封闭环
    bmesh.ops.remove_doubles(boss, verts=boss.verts, dist=1e-6)
    ring_a = [v for v in boss.verts if abs(v.co.z + thick * 0.85) < 1e-6 and abs(math.hypot(v.co.x, v.co.y) - 0.009) < 1e-5]
    ring_b = [v for v in boss.verts if abs(v.co.z - thick * 0.85) < 1e-6 and abs(math.hypot(v.co.x, v.co.y) - 0.009) < 1e-5]
    ring_a.sort(key=lambda v: math.atan2(v.co.y, v.co.x)); ring_b.sort(key=lambda v: math.atan2(v.co.y, v.co.x))
    for i in range(len(ring_a)):
        j = (i + 1) % len(ring_a)
        boss.faces.new((ring_a[i], ring_b[i], ring_b[j], ring_a[j]))
    bmesh.ops.recalc_face_normals(boss, faces=boss.faces)
    P.merge(bm, boss)
    # 一个齿对准 +X：gear_outline 的齿中心在 0.4 个齿距处
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(-0.4 / n * math.tau, 4, 'Z'), verts=bm.verts)
    W5.to_unity_front(bm)
    return color_bm(bm, col)


GEAR_Z = 0.062                                   # 齿轮中心离墙
PLATE_T = 0.022


def gear_frame(gears):
    import blender_w5_set as W5
    bm = bmesh.new()
    BRASS, COPPER = (0.95, 0.76, 0.42), (0.93, 0.6, 0.45)
    # 安装板：圆角扁带沿外扩 0.22 的中心线，宽度随齿轮链覆盖范围
    pts = PO.path(5, 0.22, 0.02)
    # 用户：齿轮门框右下角有模型问题——中心线两端贴地那段水平往门里拐，0.3 米宽的扁带在急弯内侧截面互相穿插成一团破面。
    # 改法：只留离地 >0.08 米的一段，两端沿各自的切线直线延伸到地面（不拐弯）；最后 0.35 米内截面方向渐渐转成水平，脚底平贴地面
    i0 = next(i for i, q in enumerate(pts) if q[1] > 0.08); i1 = max(i for i, q in enumerate(pts) if q[1] > 0.08)
    seg = pts[i0:i1 + 1]
    def ext(a, b):                                                             # 从 a 沿 b→a 方向直线延伸到 y=0
        dx, dy = a[0] - b[0], a[1] - b[1]; L = math.hypot(dx, dy); dx, dy = dx / L, dy / L
        t = a[1] / max(-dy, 1e-3); n = max(2, int(t / 0.02))
        return [(a[0] + dx * t * k / n, a[1] + dy * t * k / n) for k in range(n, 0, -1)]
    pts = ext(seg[0], seg[3]) + seg + ext(seg[-1], seg[-4])[::-1]
    pts = [q for k, q in enumerate(pts) if k == 0 or math.dist(q, pts[k - 1]) > 1e-4]
    prof = []
    Wd, Dp, rc = 0.3, PLATE_T, 0.009
    for cxs, czs, a0 in ((Wd / 2 - rc, Dp / 2 - rc, 0), (-(Wd / 2 - rc), Dp / 2 - rc, 90), (-(Wd / 2 - rc), -(Dp / 2 - rc), 180), (Wd / 2 - rc, -(Dp / 2 - rc), 270)):
        for i in range(4):
            a = math.radians(a0 + 90 * i / 3)
            prof.append((cxs + math.cos(a) * rc, czs + math.sin(a) * rc))
    rings = []
    cum = [0.0]
    for a_, b_ in zip(pts[:-1], pts[1:]): cum.append(cum[-1] + math.dist(a_, b_))
    for k, ((x, y), (tx, ty), (nx, ny)) in enumerate(path_frames(pts)):
        d = min(cum[k], cum[-1] - cum[k]); w = max(0.0, 1 - d / 0.35); w = w * w * (3 - 2 * w)
        if w > 0:                                                              # 脚部：截面转成水平（沿 ±x），落地时整条底边贴地
            hx = 1.0 if nx >= 0 else -1.0; nx, ny = nx * (1 - w) + hx * w, ny * (1 - w); L = math.hypot(nx, ny); nx, ny = nx / L, ny / L
        wob = 1 + 0.12 * math.sin(x * 9 + y * 7)
        rings.append([bm.verts.new(U(x + nx * u * wob, max(y + ny * u * wob, 0.0), Dp / 2 + v)) for u, v in prof])
    m = len(prof)
    for a, b in zip(rings[:-1], rings[1:]):
        for k in range(m): bm.faces.new((a[k], b[k], b[(k + 1) % m], a[(k + 1) % m]))
    bm.faces.new(rings[0]); bm.faces.new(rings[-1][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    color_bm(bm, COPPER)
    # 铆钉：板边缘一圈
    at, total = walker(pts)
    t = 0.05
    while t < total - 0.05:
        (x, y), _, (nx, ny) = at(t)
        for side in (-1, 1):
            rv = P.lathe([(0, 0), (0.008, 0), (0.008, 0.004), (0.006, 0.008), (0, 0.0095)], 10)
            bmesh.ops.transform(rv, matrix=Matrix.Translation(U(x + nx * side * 0.125, max(y + ny * side * 0.125, 0.012), PLATE_T)) @ Matrix.Rotation(math.radians(90), 4, 'X'), verts=rv.verts)
            P.merge(bm, color_bm(rv, BRASS))
        t += 0.11
    # 轴销：每个齿轮一根，从板面伸到齿轮前面，顶端一个圆帽
    for g in gears:
        pin = P.lathe([(0, 0), (0.0088, 0), (0.0088, GEAR_Z + 0.03), (0.014, GEAR_Z + 0.032), (0.014, GEAR_Z + 0.04), (0.01, GEAR_Z + 0.046), (0, GEAR_Z + 0.047)], 14)
        bmesh.ops.transform(pin, matrix=Matrix.Translation(U(g['x'], g['y'], PLATE_T - 0.004)) @ Matrix.Rotation(math.radians(90), 4, 'X'), verts=pin.verts)
        P.merge(bm, color_bm(pin, BRASS))
    # 门脚：两只铜脚座
    for side in (-1, 1):
        p = pts[0] if side > 0 else pts[-1]
        ft = P.lathe([(0, 0), (0.09, 0), (0.095, 0.02), (0.07, 0.035), (0.05, 0.05), (0, 0.05)], 20)
        bmesh.ops.transform(ft, matrix=Matrix.Translation(U(p[0], 0, 0.06)), verts=ft.verts)
        P.merge(bm, color_bm(ft, BRASS))
    return bm


def key_mesh():
    """发条钥匙（装在主动轮轴上，跟主动轮一起转）：轴杆 + 两片心形把手，原点在轴心，杆沿 +Z（朝房间）"""
    bm = bmesh.new()
    GOLD = (1.0, 0.82, 0.45)
    shaft = P.lathe([(0, 0), (0.012, 0), (0.012, 0.12), (0.022, 0.125), (0.022, 0.14), (0, 0.142)], 14)
    bmesh.ops.transform(shaft, matrix=Matrix.Rotation(math.radians(90), 4, 'X'), verts=shaft.verts)   # 沿 Blender -Y = Unity +Z
    P.merge(bm, color_bm(shaft, GOLD))
    for side in (-1, 1):
        o = []
        for i in range(40):
            a = i / 40 * math.tau
            r = 0.055 * (1 - 0.18 * math.sin(a) ** 4) * (1 + 0.12 * math.cos(a))
            o.append((side * (0.02 + 0.055 + math.cos(a) * r * 1.1), math.sin(a) * r))
        sl = P.slab(o, 0.018)
        # slab 在 XY 平面 → 转成 Unity 的 xy 平面（面朝 +Z），放在杆的末端
        for v in sl.verts: v.co = U(v.co.x, v.co.y, 0.155 + v.co.z)
        bmesh.ops.recalc_face_normals(sl, faces=sl.faces)
        P.merge(bm, color_bm(sl, GOLD))
    return bm


def export(name, bm, sharp_deg=None):
    ob = P.to_obj(bm, name, smooth=True)
    if sharp_deg:
        try: ob.data.set_sharp_from_angle(angle=math.radians(sharp_deg))
        except Exception: pass
    bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                             bake_space_transform=True, colors_type='SRGB')
    bpy.data.objects.remove(ob, do_unlink=True)


def main():
    P.reset()
    only = sys.argv[sys.argv.index('--') + 2:] if len(sys.argv) > sys.argv.index('--') + 2 else []
    def want(n): return not only or n in only
    if want('W1'): export('Portal_Bricks', portal_bricks(), 35)
    if want('W1path'):
        pw, pr = brick_path()
        export('W1_BrickPath', pw, 35); export('W1_BrickPathSpill', pr, 35)
    if want('W3'): export('W3_FeltArch', felt_arch())
    if want('W4'): export('W4_BalloonArch', balloon_arch())
    if want('W6'): export('W6_CandyArch', candy_arch())
    if want('W5'):
        gears = gear_train()
        for i, g in enumerate(gears):
            export(f'W5_PG_{i:02d}', gear_mesh(g['n'], W5COL[g['col']], 0.026 + 0.006 * (i % 2)), 40)
        export('W5_GearFrame', gear_frame(gears), 50)
        export('W5_WindKey', key_mesh(), 50)
        json.dump(dict(gearZ=GEAR_Z, module=MOD, gears=gears), open(os.path.join(OUT, 'W5_GearTrain.json'), 'w'), indent=1)
        print('GEARS', len(gears))
    print('PORTALS', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
