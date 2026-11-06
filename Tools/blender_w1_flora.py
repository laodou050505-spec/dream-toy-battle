"""第 1 章「积木小镇」地面点缀（Blender 里运行）：
  Blender -b -P Tools/blender_w1_flora.py -- <输出目录>

  W1_Flora  绒布小花（弯曲花瓣 + 珠子花心 + 弯茎 + 两片叶）、绒布草丛（锥形弯草叶）、彩色小珠、散落的小积木
每朵花 / 每丛草 / 每颗珠子都是独立的封闭流形块，合成一个网格（一次绘制），顶点色 sRGB。
贴着 W1_Ground 的起伏摆放（共用 blender_w1_set.ground_height），避开房子、轨道、小路、树、山包和门口。
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_w1_set as S

OUT = S.OUT
U = P.U
_argv = sys.argv[sys.argv.index('--') + 1:]
WORLD = _argv[1] if len(_argv) > 1 else 'W1'      # W1 积木小镇 / W3 锡兵城堡；W1R..W4R = 房间四周一整圈（门后世界包住房间后）
RING = len(WORLD) == 3 and WORLD.endswith('R')
SPILL = len(WORLD) == 3 and WORLD.endswith('S')     # W1S..W4S = 门前溢出地皮上的花草（房间里，正常渲染）
ROOMF = len(WORLD) == 3 and WORLD.endswith('F')     # W1F..W4F = 真实房间地面范围内的低矮花草（属于门后世界，只在地面被玩具化覆盖处看得到）
BASE = WORLD[:2] if (RING or SPILL or ROOMF) else WORLD
if SPILL:
    GH = lambda x, z: 0.012 if BASE != 'W2' else 0.014
elif RING and BASE == 'W2':
    import blender_w2_set as G2
    GH = G2.ground_height
elif BASE == 'W3':
    import blender_w3_set as G
    GH = G.ground_height
elif BASE == 'W4':
    import blender_w4_set as G4
    GH = G4.ground_height
elif WORLD == 'OUT':
    import importlib.util as _iu
    _spec = _iu.spec_from_file_location('toy_outer_h', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'blender_toy_outer.py'))
    _src = open(_spec.origin).read().split('\nP.reset()')[0]          # 只取地形高度函数，不执行导出
    _ns = {'__file__': _spec.origin, '__name__': 'toy_outer_h'}
    exec(compile(_src.replace("OUT = sys.argv[sys.argv.index('--') + 1]", "OUT = ''").replace("os.makedirs(OUT, exist_ok=True)", ""), _spec.origin, 'exec'), _ns)
    GH = _ns['height']
else:
    GH = S.ground_height
if RING or ROOMF:
    _GH0 = GH
    def GH(x, z):                                   # 与 Unity 的 FlattenNearRoom 同一公式：真实房间范围内压平
        h = _GH0(x, z)
        dx = max(0.0, abs(x) - 4.3); dz = max(0.0, max(-0.3 - z, z - 7.0))
        k = min(1.0, math.hypot(dx, dz) / 2.5); k = k * k * (3 - 2 * k)
        return min(h, 0.0) * (1 - k) + h * k
import zlib
rnd = random.Random(20261027 + (zlib.crc32(WORLD.encode()) % 1000 if RING else 0))   # hash(str) 每次进程都随机化：环带花草每次生成位置都不同
GREENS = [(0.36, 0.66, 0.3), (0.44, 0.74, 0.36), (0.3, 0.6, 0.32), (0.5, 0.8, 0.5)]
# 草丛要和地面（0.42,0.72,0.36 ~ 0.6,0.84,0.46）拉开明度：嫩黄绿 / 深青绿 / 薄荷
TUFT = [(0.6, 0.86, 0.4), (0.4, 0.68, 0.34), (0.54, 0.84, 0.6), (0.34, 0.62, 0.3)]   # 原来有一档深墨绿，远看像一撮黑刺；数量保持 4（random.choice 的取数不变）
PATCH = [(0.74, 0.94, 0.66), (1.0, 0.92, 0.66), (1.0, 0.8, 0.84), (0.86, 0.8, 1.0)]
PETALS = [(1.0, 0.62, 0.78), (1.0, 0.92, 0.55), (0.62, 0.78, 1.0), (0.78, 0.64, 1.0), (1.0, 0.98, 0.96), (1.0, 0.7, 0.55)]
CENTERS = [(1.0, 0.86, 0.4), (1.0, 0.96, 0.84), (1.0, 0.72, 0.5)]

# 避让区：(x, z, 半径)，与 BangSceneBuilder 的布局一致
AVOID = [(-0.75, -2.75, 0.45), (1.0, -3.2, 0.45), (-0.1, -3.65, 0.45), (0.75, -4.55, 0.3),
         (-1.55, -1.55, 0.16), (1.5, -1.75, 0.16), (-2.25, -3.3, 0.16), (2.6, -2.9, 0.16), (-0.95, -5.0, 0.16),
         (-1.95, -4.45, 0.42), (3.0, -5.8, 0.42), (-3.2, -6.4, 0.42), (1.7, -6.2, 0.42),
         (0.55, -1.15, 0.32), (-0.62, -0.95, 0.32), (1.25, -4.3, 0.32),
         (-0.42, -1.5, 0.12), (0.58, -2.05, 0.12), (2.45, -4.85, 0.95), (-2.65, -5.45, 0.65)]
PATH = [(-0.06, 0.05), (-0.14, -0.6), (0.02, -1.25), (0.22, -1.85), (0.14, -2.4), (0.12, -2.72)]   # 新积木小路（blender_portals.PATH_CTRL）+ 小广场


def seg_dist(p, a, b):
    ax, az = a; bx, bz = b; px, pz = p
    dx, dz = bx - ax, bz - az
    t = max(0, min(1, ((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz)))
    return math.hypot(px - ax - t * dx, pz - az - t * dz)


AVOID_W3 = [(0.3, -4.15, 0.0)] + [(x, z, r) for x, z, r in [(-2.2, -3.0, 0.45), (2.65, -3.2, 0.45), (-2.8, -4.6, 0.4), (3.1, -4.9, 0.4), (-1.9, -6.3, 0.4),
            (2.2, -6.4, 0.4), (-3.4, -2.2, 0.4), (0.3, -6.9, 0.4), (-2.45, -5.3, 0.45), (3.0, -2.2, 0.45)]]


def free_w3(x, z, pad):
    if z > -0.35: return False
    q = G.moat_q(x, z)
    if q < 1.0 + G.MOAT_W / (2 * min(G.MOAT_A, G.MOAT_B)) + 0.08 + pad: return False   # 护城河与城堡小岛不长草
    return all(math.hypot(x - ax, z - az) > r + pad for ax, az, r in AVOID_W3[1:])


AVOID_W4 = [(0.3, -3.6, 0.85), (-1.55, -3.4, 0.7), (1.95, -3.45, 0.6), (-0.95, -1.75, 0.2), (-1.25, -2.05, 0.22), (1.45, -1.95, 0.22),
            (1.8, -2.25, 0.2), (2.45, -2.75, 0.2), (-2.1, -2.6, 0.22), (-0.45, -2.25, 0.35), (-2.45, -3.0, 0.3), (2.75, -3.3, 0.3), (-0.6, -5.4, 0.3)]


RING_C, RING_R = (0.2, -1.0), 10.6


SPILL_AVOID = {'W1': [(0.85, 0.45), (-1.05, 0.9), (-0.75, 0.35), (0.9, 1.25), (1.35, 0.25), (0.32, 1.65), (-0.3, 1.72)],
               'W2': [(-0.75, 0.85), (0.6, 1.05), (1.15, 1.6), (-1.15, 1.5), (1.4, 0.4)],
               'W3': [(-1.1, 0.45), (0.55, 0.85), (0.85, 1.05), (1.35, 0.35), (-0.7, 1.2)],
               'W4': [(-0.85, 0.8), (0.65, 1.15), (-1.2, 1.45), (1.25, 0.45)]}


def free(x, z, pad):
    if ROOMF:
        if not (abs(x) < 3.85 - pad and 0.1 + pad < z < 6.55 - pad): return False
        hw, dp = (1.9, 2.1) if BASE == 'W2' else (1.6, 1.7)
        if (x / (hw * 1.05)) ** 2 + ((z + 0.05) / (dp * 1.05)) ** 2 < 1: return False   # 门前溢出地皮那块已有
        return True
    if SPILL:
        hw, dp = (1.9, 2.1) if BASE == 'W2' else (1.6, 1.7)
        if z < 0.12 or (x / (hw * 0.82)) ** 2 + ((z + 0.05) / (dp * 0.82)) ** 2 > 1: return False
        if BASE == 'W1' and abs(x - 0.0) < 0.42 + pad: return False                 # 积木小路
        return all(math.hypot(x - ax, z - az) > 0.2 + pad for ax, az in SPILL_AVOID[BASE])
    if RING:
        if math.hypot(x - RING_C[0], z - RING_C[1]) > RING_R: return False
        if abs(x) < 4.3 + pad and -0.3 - pad < z < 7.0 + pad: return False          # 真实房间
        if -3.8 < x < 4.2 and -7.0 < z < -0.35: return False                        # 中间区域已有花草
        if BASE == 'W3' and G.moat_q(x, z) < 1.25: return False
        return True
    if WORLD == 'OUT':
        d = math.hypot(x, z + 1.0)
        return 4.6 < d < 9.5 and not (abs(x) < 4.3 and -5.7 < z < 3.8)      # 房间外的环带
    if WORLD == 'W3': return free_w3(x, z, pad)
    if WORLD == 'W4':
        return z < -0.35 and all(math.hypot(x - ax, z - az) > r + pad for ax, az, r in AVOID_W4)
    if z > -0.35:                                   # 门口留空
        return False
    for ax, az, r in AVOID:
        if math.hypot(x - ax, z - az) < r + pad:
            return False
    q = math.hypot((x - S.TRACK_C[0]) / S.TRACK_A, (z - S.TRACK_C[1]) / S.TRACK_B)
    if abs(q - 1) * 1.2 < 0.13 + pad:                # 轨道带
        return False
    if min(seg_dist((x, z), a, b) for a, b in zip(PATH, PATH[1:])) < 0.4 + pad or math.hypot(x - 0.12, z + 2.72) < 0.5 + pad:
        return False
    return True


def colored(bm, rgb):
    return S.color_bm(bm, rgb)


def place(bm, x, z, yaw, tilt=(0.0, 0.0), sink=0.004):
    """bm 在 Blender 局部坐标建（Z 向上、原点在根部），搬到 Unity (x, ground, z)"""
    y = GH(x, z) - sink
    sc = Matrix.Scale(1.9, 4) if WORLD == 'OUT' else Matrix.Scale(1.55, 4) if RING else Matrix.Identity(4)   # 外圈：放大一点（远处也看得清）
    m = Matrix.Translation(U(x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tilt[0], 4, 'X') @ Matrix.Rotation(tilt[1], 4, 'Y') @ sc
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def sphere(r, sz=1.0, seg=10, rings=6, z0=0.0):
    prof = [(0, -r * sz)] + [(r * math.sin(math.pi * k / rings), -r * sz * math.cos(math.pi * k / rings)) for k in range(1, rings)] + [(0, r * sz)]
    bm = P.lathe(prof, seg)
    for v in bm.verts: v.co.z += z0
    return bm


def petal_outline(L, W, n=16):
    """水滴形花瓣（根部窄、外端圆），沿 +X 伸出"""
    pts = []
    for i in range(n):
        t = i / n * math.tau
        x = (1 - math.cos(t)) / 2 * L                          # 0..L
        w = W * math.sin(t) * (0.35 + 0.65 * (x / L) ** 0.6)    # 根部收窄
        pts.append((x + L * 0.08, w / 2))
    return pts


def flower(x, z):
    h = rnd.uniform(0.07, 0.13)
    bm = bmesh.new()
    # 弯茎
    bend = rnd.uniform(-0.25, 0.25)
    stem = [(math.sin(bend * t) * h * 0.25, 0, t * h) for t in [i / 7 for i in range(8)]]
    top = Vector(stem[-1])
    P.merge(bm, colored(P.tube(stem, 0.0055, seg=8, r_fn=lambda u: 1.0 - 0.3 * u), rnd.choice(GREENS)))
    # 两片叶（贴在茎下部，向外斜伸、弯曲）
    for k, side in enumerate((1, -1)):
        lf = P.slab(petal_outline(rnd.uniform(0.028, 0.04), 0.018), 0.004, bend=1.6)
        hz = h * (0.25 + 0.18 * k)
        m = Matrix.Translation((math.sin(bend * hz / h) * h * 0.25, 0, hz)) @ Matrix.Rotation(math.pi * (0 if side > 0 else 1) + rnd.uniform(-0.4, 0.4), 4, 'Z') @ Matrix.Rotation(-0.55, 4, 'Y')
        bmesh.ops.transform(lf, matrix=m, verts=lf.verts)
        P.merge(bm, colored(lf, rnd.choice(GREENS)))
    # 花头：5~6 片花瓣朝上翘
    n = rnd.choice((5, 5, 6))
    pc = rnd.choice(PETALS)
    L = rnd.uniform(0.024, 0.034)
    face_tilt = rnd.uniform(0.15, 0.45)              # 花头略朝前（朝门）
    head = Matrix.Translation(top) @ Matrix.Rotation(face_tilt, 4, 'X')
    for i in range(n):
        pt = P.slab(petal_outline(L, L * 0.78), 0.0045, bend=2.2)
        m = head @ Matrix.Rotation(i / n * math.tau + rnd.uniform(-0.08, 0.08), 4, 'Z') @ Matrix.Rotation(-0.32, 4, 'Y')
        bmesh.ops.transform(pt, matrix=m, verts=pt.verts)
        P.merge(bm, colored(pt, S.jitter(pc, 0.03)))
    c = sphere(L * 0.42, sz=0.7, seg=16, rings=8)
    bmesh.ops.transform(c, matrix=head @ Matrix.Translation((0, 0, 0.004)), verts=c.verts)
    P.merge(bm, colored(c, rnd.choice(CENTERS)))
    # 面朝门（+Z Unity = -Y Blender）：yaw 让花头朝玩家，带随机
    return place(bm, x, z, rnd.uniform(-0.6, 0.6))


def blade(rng, base, ang, L, W, lean, twist, g):
    """一片毛绒草叶：菱形截面的扁带（根部宽、尖端收圆），沿 ang 方向向外弯；顶点色由根到尖从深到浅。封闭流形。"""
    bm = bmesh.new(); K = 5
    col = bm.loops.layers.color.new('Col')
    out = Vector((math.cos(ang), math.sin(ang), 0))
    rings = []
    for k in range(K + 1):
        t = k / K
        bend = lean * t * t                                   # 越往上越弯
        c = Vector(base) + out * (math.sin(bend) * L * 0.62) + Vector((0, 0, L * (t - 0.18 * bend * t)))
        tan = (out * math.cos(bend) * 0.62 + Vector((0, 0, 1)) * (1 - 0.18 * bend)).normalized() if k else Vector((0, 0, 1))
        side = Vector((-math.sin(ang + twist * t), math.cos(ang + twist * t), 0))
        nrm = side.cross(tan).normalized()
        w = W * (1 - t ** 2.4) * (0.75 + 0.25 * math.sin(math.pi * min(1, t * 1.8))) + 0.0008
        th = 0.0016 * (1 - 0.6 * t) + 0.0005
        if k == K:
            rings.append([bm.verts.new(c + tan * 0.002)]); break
        rings.append([bm.verts.new(c + side * w), bm.verts.new(c + nrm * th), bm.verts.new(c - side * w), bm.verts.new(c - nrm * th)])
    for a, b in zip(rings[:-1], rings[1:]):
        if len(b) == 1:
            for i in range(4): bm.faces.new((a[i], a[(i + 1) % 4], b[0]))
        else:
            for i in range(4): bm.faces.new((a[i], a[(i + 1) % 4], b[(i + 1) % 4], b[i]))
    bm.faces.new(tuple(reversed(rings[0])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    root = tuple(c * 0.74 for c in g); tip = tuple(min(1.0, c * 1.12 + 0.05) for c in g)
    z0, z1 = base[2], base[2] + L
    for f in bm.faces:
        for l in f.loops:
            u = max(0.0, min(1.0, (l.vert.co.z - z0) / max(1e-6, z1 - z0))) ** 0.8
            l[col] = (root[0] + (tip[0] - root[0]) * u, root[1] + (tip[1] - root[1]) * u, root[2] + (tip[2] - root[2]) * u, 1.0)
    return bm


def tuft(x, z):
    """毛绒草丛：1～3 个小簇，每簇 4～7 片扁宽弯曲的草叶（原来是几根细圆管，像一撮硬刺）。
    全局 rnd 的取数和旧版完全一样（数量、颜色、每根 3 个、朝向），花草位置 / 分块文件名不变；造型用由这些数播种的局部随机数。"""
    nb = rnd.randint(5, 8)
    g = rnd.choice(TUFT)
    seeds = [rnd.uniform(-0.3, 0.3) + rnd.uniform(0.045, 0.085) + rnd.uniform(0.35, 0.75) for _ in range(nb)]
    rng = random.Random(int(sum(seeds) * 1e6) ^ int((x * 1000) % 99991) * 7 ^ int((z * 1000) % 99991))
    for _ in range(nb): S.jitter(g, 0.05)                     # 旧版每根调一次 S.jitter：照样消耗，别的花草颜色不变
    jit = lambda c, k: tuple(max(0.0, min(1.0, v + rng.uniform(-k, k))) for v in c)
    bm = bmesh.new()
    nclump = rng.choice((1, 2, 2, 3))
    for ci in range(nclump):
        a0 = rng.uniform(0, math.tau); r0 = 0 if ci == 0 else rng.uniform(0.012, 0.022)
        cx, cy = math.cos(a0) * r0, math.sin(a0) * r0
        gc = jit(g, 0.04)
        sz = 1.0 if ci == 0 else rng.uniform(0.6, 0.85)
        n = rng.randint(4, 7) if ci == 0 else rng.randint(3, 5)
        for i in range(n):
            ang = i / n * math.tau + rng.uniform(-0.35, 0.35)
            L = rng.uniform(0.056, 0.096) * sz
            W = rng.uniform(0.0054, 0.0076) * (0.85 + 0.3 * sz)
            lean = rng.uniform(0.5, 1.15)
            base = (cx + math.cos(ang) * 0.003, cy + math.sin(ang) * 0.003, 0.0)
            P.merge(bm, blade(rng, base, ang, L, W, lean, rng.uniform(-0.6, 0.6), jit(gc, 0.03)))
    return place(bm, x, z, rnd.uniform(0, math.tau), sink=0.006)


def patch(x, z):
    """绒布贴片：波浪边的圆片 + 一圈缝线（短小的线段）"""
    R = rnd.uniform(0.13, 0.26); n = rnd.choice((7, 8, 9)); ph = rnd.uniform(0, math.tau)
    out = [(math.cos(t) * R * (1 + 0.1 * math.cos(n * t + ph)), math.sin(t) * R * (1 + 0.1 * math.cos(n * t + ph)))
           for t in [i / 40 * math.tau for i in range(40)]]
    bm = colored(P.slab(out, 0.006), rnd.choice(PATCH))
    for v in bm.verts: v.co.z += 0.003
    ns = int(R * 2 * math.pi / 0.035)
    sc = (1.0, 0.97, 0.9)
    for i in range(ns):
        t0 = i / ns * math.tau; t1 = t0 + 0.55 * math.tau / ns
        rr = R * 0.8
        pts = [(math.cos(t) * rr * (1 + 0.1 * math.cos(n * t + ph)), math.sin(t) * rr * (1 + 0.1 * math.cos(n * t + ph)), 0.0068) for t in (t0, (t0 + t1) / 2, t1)]
        P.merge(bm, colored(P.tube(pts, 0.0022, seg=4), sc))
    return place(bm, x, z, 0, sink=0.0)


def bead(x, z):
    r = rnd.uniform(0.012, 0.022)
    bm = sphere(r, sz=0.75, seg=12, rings=7, z0=r * 0.75)
    return place(colored(bm, rnd.choice(S.PASTEL)), x, z, 0, sink=r * 0.25)


SAND = [(1.0, 0.82, 0.8), (1.0, 0.93, 0.7), (0.82, 0.92, 1.0), (0.9, 0.82, 1.0), (0.8, 0.95, 0.85), (1.0, 0.98, 0.94)]
BEACHGRASS = [(0.62, 0.86, 0.6), (0.78, 0.9, 0.55), (0.55, 0.8, 0.7)]


def starfish(x, z):
    r1, r0 = rnd.uniform(0.04, 0.06), 0.018
    out = []
    for i in range(40):
        t = i / 40 * math.tau
        k = 0.5 + 0.5 * math.cos(t * 5)
        out.append((math.cos(t) * (r0 + (r1 - r0) * k ** 1.6), math.sin(t) * (r0 + (r1 - r0) * k ** 1.6)))
    bm = colored(P.slab(out, 0.012, bend=-6.0), rnd.choice(SAND[:2] + [(1.0, 0.72, 0.62)]))
    for v in bm.verts: v.co.z += 0.006
    return place(bm, x, z, rnd.uniform(0, math.tau), sink=0.0)


def shell(x, z):
    R = rnd.uniform(0.035, 0.05); out = [(0, -R * 0.15)]
    for i in range(25):
        t = math.pi * (0.1 + 0.8 * i / 24)
        rr = R * (1 + 0.06 * math.cos(t * 18))                  # 扇贝边
        out.append((math.cos(t) * rr, math.sin(t) * rr * 0.9))
    bm = colored(P.slab(out, 0.01, bend=-4.0), rnd.choice(SAND))
    for v in bm.verts: v.co.z += 0.008
    return place(bm, x, z, rnd.uniform(0, math.tau), tilt=(rnd.uniform(-0.2, 0.2), 0), sink=0.0)


def pebble(x, z):
    r = rnd.uniform(0.02, 0.04)
    bm = sphere(r, sz=0.55, seg=12, rings=6, z0=r * 0.5)
    for v in bm.verts: v.co.x *= 1.3
    return place(colored(bm, rnd.choice(SAND)), x, z, rnd.uniform(0, math.tau), sink=r * 0.15)


def beach_grass(x, z):
    global TUFT
    keep = TUFT; TUFT = BEACHGRASS
    try: return tuft(x, z)
    finally: TUFT = keep


def loose_brick(x, z):
    w = rnd.choice((0.11, 0.17))
    bm = S.brick_u(-w / 2, w / 2, 0, 0.05, -0.04, 0.04, rnd.choice(S.PASTEL), studs=True, bevel=0.008)
    # brick_u 用 Unity 坐标建在原点 → 已是 Blender 坐标，直接摆
    y = GH(x, z) - 0.006
    m = Matrix.Translation(U(x, y, z)) @ Matrix.Rotation(rnd.uniform(0, math.tau), 4, 'Z')
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def scatter(n, pad, near_bias, fn, pts):
    out = 0; tries = 0
    while out < n and tries < n * 400:
        tries += 1
        if ROOMF:
            x, z = rnd.uniform(-3.9, 3.9), rnd.uniform(0.1, 6.6)
        elif SPILL:
            x, z = rnd.uniform(-1.7, 1.7), rnd.uniform(0.1, 1.9)
        elif RING:
            a = rnd.uniform(0, math.tau); r = RING_R * math.sqrt(rnd.random())
            x, z = RING_C[0] + math.cos(a) * r, RING_C[1] + math.sin(a) * r
        elif WORLD == 'OUT':                                   # 外部世界：围绕房间的环带，靠近房间多
            a = rnd.uniform(0, math.tau); r = 4.6 + (rnd.random() ** near_bias) * 4.9
            x, z = math.cos(a) * r, -1.0 + math.sin(a) * r
        else:
            # 近处（门口到小镇）多、远处少：z 按偏向分布
            z = -0.4 - (rnd.random() ** near_bias) * 6.4
            x = rnd.uniform(-3.6, 4.0)
        if not free(x, z, pad): continue
        if any(math.hypot(x - px, z - pz) < pad + pr for px, pz, pr in pts): continue
        pts.append((x, z, pad)); yield fn(x, z); out += 1


def w1_flora():
    bm = bmesh.new()
    pts = []
    # 绒布贴片放在门洞里看得到的地方（小路两侧、房前、树下）
    for x, z in [(-0.55, -1.95), (0.72, -1.45), (-1.25, -2.15), (1.95, -1.35), (-1.6, -3.95), (2.0, -4.05), (-0.35, -4.75), (2.75, -2.15), (-2.55, -2.35)]:
        if free(x, z, 0.12):
            pts.append((x, z, 0.0)); P.merge(bm, patch(x, z))
    pts[:] = [(x, z, 0.0) for x, z, _ in pts]      # 贴片上面也可以长花草
    for part in scatter(65 if WORLD == 'OUT' else 40, 0.05, 1.6, flower, pts): P.merge(bm, part)
    # 花丛：几朵挨在一起
    clusters = {'W1': [(-1.2, -1.0), (0.95, -0.85), (-1.6, -2.4), (2.1, -2.2), (-0.55, -4.6), (2.2, -3.75)],
                'W3': [(-1.1, -1.1), (1.2, -1.0), (-1.9, -2.2), (2.2, -2.0), (-0.3, -1.6), (1.6, -1.6), (-2.6, -3.6)],
                'W4': [(-0.2, -1.2), (0.9, -1.1), (-1.6, -1.3), (2.2, -1.5), (0.4, -2.3), (-2.5, -4.2), (2.9, -4.3)],
                'OUT': [(math.cos(a) * 5.4, -1.0 + math.sin(a) * 5.4) for a in [i * 0.7 for i in range(9)]]}[WORLD]
    for cx, cz in clusters:
        for k in range(5):
            x, z = cx + rnd.uniform(-0.16, 0.16), cz + rnd.uniform(-0.16, 0.16)
            if free(x, z, 0.02) and not any(math.hypot(x - px, z - pz) < 0.05 for px, pz, _ in pts):
                pts.append((x, z, 0.03)); P.merge(bm, flower(x, z))
    for part in scatter(140 if WORLD == 'OUT' else 110, 0.045, 1.4, tuft, pts): P.merge(bm, part)
    for part in scatter(22, 0.03, 1.3, bead, pts): P.merge(bm, part)
    if WORLD == 'W1':
        for part in scatter(7, 0.08, 1.2, loose_brick, pts): P.merge(bm, part)
    return bm


def ring_flora():
    """房间四周一整圈：按相对房间中心的方位分 4 块网格（视锥剔除）"""
    # 按 4 m 网格切块：Unity 里每块挂 LODGroup 做距离剔除，门洞视锥裁剪也更细
    CELL = 4.0
    cells = {}
    def sector(x, z):
        key = (math.floor(x / CELL), math.floor(z / CELL))
        if key not in cells: cells[key] = len(cells)
        return cells[key]
    class Grow(list):
        def __getitem__(self, i):
            while i >= len(self): self.append(bmesh.new())
            return list.__getitem__(self, i)
    bms = Grow()
    pts = []
    def add(gen):
        for part in gen:
            x, z = pts[-1][0], pts[-1][1]
            P.merge(bms[sector(x, z)], part)
    if BASE == 'W2':
        add(scatter(90, 0.05, 1.0, starfish, pts)); add(scatter(110, 0.05, 1.0, shell, pts))
        add(scatter(120, 0.05, 1.0, pebble, pts)); add(scatter(150, 0.06, 1.0, beach_grass, pts))
    else:
        add(scatter(26, 0.3, 1.0, patch, pts))
        pts[:] = [(x, z, 0.0) for x, z, _ in pts]
        add(scatter(130, 0.06, 1.0, flower, pts))
        # 一簇簇花丛
        for _ in range(16):
            for _t in range(200):
                a = rnd.uniform(0, math.tau); r = RING_R * math.sqrt(rnd.random())
                cx, cz = RING_C[0] + math.cos(a) * r, RING_C[1] + math.sin(a) * r
                if free(cx, cz, 0.2): break
            for k in range(6):
                x, z = cx + rnd.uniform(-0.22, 0.22), cz + rnd.uniform(-0.22, 0.22)
                if free(x, z, 0.02) and not any(math.hypot(x - px, z - pz) < 0.08 for px, pz, _ in pts):
                    pts.append((x, z, 0.04)); P.merge(bms[sector(x, z)], flower(x, z))
        add(scatter(240, 0.06, 1.0, tuft, pts)); add(scatter(50, 0.04, 1.0, bead, pts))
        if BASE == 'W1': add(scatter(16, 0.1, 1.0, loose_brick, pts))
    return [(k, bms[i]) for k, i in cells.items()]


def room_flora():
    bm = bmesh.new(); pts = []
    if BASE == 'W2':
        for g, n in ((shell, 28), (starfish, 16), (pebble, 30), (beach_grass, 30)):
            for part in scatter(n, 0.08, 1.0, g, pts): P.merge(bm, part)
    else:
        for part in scatter(4, 0.32, 1.0, patch, pts): P.merge(bm, part)
        pts[:] = [(x, z, 0.0) for x, z, _ in pts]
        for g, n in ((flower, 42), (tuft, 120), (bead, 14)):
            for part in scatter(n, 0.07, 1.0, g, pts): P.merge(bm, part)
    return bm


def spill_flora():
    bm = bmesh.new(); pts = []
    if BASE == 'W2':
        for g, n in ((shell, 10), (starfish, 6), (pebble, 10), (beach_grass, 12)):
            for part in scatter(n, 0.05, 1.0, g, pts): P.merge(bm, part)
    else:
        for g, n in ((flower, 18), (tuft, 56), (bead, 6)):
            for part in scatter(n, 0.045, 1.0, g, pts): P.merge(bm, part)
    return bm


def main():
    P.reset()
    if SPILL or ROOMF:
        bm = spill_flora() if SPILL else room_flora(); name = f'{BASE}_SpillFlora' if SPILL else f'{BASE}_RoomFlora'
        ob = P.to_obj(bm, name, smooth=True)
        bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                 bake_space_transform=True, colors_type='SRGB')
        print('FLORA', json.dumps(P.report, ensure_ascii=False)); return
    if RING:
        import glob
        for old in glob.glob(os.path.join(OUT, f'{BASE}_FloraRing_*.fbx')): os.remove(old)
        for (cx, cz), bm in ring_flora():
            if len(bm.faces) == 0: continue
            name = f'{BASE}_FloraRing_{cx + 10}_{cz + 10}'
            ob = P.to_obj(bm, name, smooth=True)
            bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
            bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                     axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                     bake_space_transform=True, colors_type='SRGB')
            bpy.data.objects.remove(ob, do_unlink=True)
        print('FLORA', json.dumps(P.report, ensure_ascii=False))
        return
    bm = w1_flora()
    name = WORLD + '_Flora'
    ob = P.to_obj(bm, name, smooth=True)
    bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                             bake_space_transform=True, colors_type='SRGB')
    print('FLORA', json.dumps(P.report, ensure_ascii=False))


main()
