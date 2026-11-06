"""第 2～6 章小怪的分部件模型（Blender 里运行）：
  Blender -b -P Tools/blender_enemies_more.py -- <输出目录>

原来除了插旗兵、胖墩、老鼠之外，其余种类都套用积木插旗兵；这里给每种做专属造型。部件约定和 blender_enemies_w1.py 相同：
每个部件单独导出，原点放在关节，全部封闭流形、顶点色；安装位置写进 W2E_parts.json，Unity 里组装成带层级的预制体。
  泡沫枪兵 Trooper_*   铁皮兵 Tin_*   跟班小锡兵 Minion_*   指挥官 Cmd_*   纸板忍者 Ninja_*
  弹簧小丑盒 Jack_*    纸飞机 Plane_*   气球兵 Balloon_*     遥控车 Car_*    修理机器人 Bot_*
"""
import bmesh, math, os, sys, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_w1_set as W1
import blender_portals as BP
import blender_enemies_w1 as E

OUT = sys.argv[sys.argv.index('--') + 1]
U, color_bm, brick, bead, place, tube_u, face = P.U, BP.color_bm, E.brick, E.bead, E.place, E.tube_u, E.face
PINK, CREAM, BLUE, YEL, MINT, PEACH, LAV, DARK, WHITE, GOLD = E.PINK, E.CREAM, E.BLUE, E.YEL, E.MINT, E.PEACH, E.LAV, E.DARK, E.WHITE, E.GOLD
SKIN = (1.0, 0.82, 0.68); RED = (0.9, 0.26, 0.3); NAVY = (0.2, 0.26, 0.5); BLACK = (0.13, 0.12, 0.16); SILVER = (0.78, 0.8, 0.86)
OLIVE = (0.56, 0.68, 0.4); SAND = (0.93, 0.84, 0.62); MOSS = (0.42, 0.54, 0.34); CARD = (0.8, 0.64, 0.44); CARD2 = (0.7, 0.54, 0.36); TAPE = (0.95, 0.86, 0.66)
PARTS = {}


def L(profile, rgb, seg=24):
    return color_bm(P.lathe(profile, seg), rgb)


def box(x0, x1, y0, y1, z0, z1, rgb, bevel=0.006):
    return W1.brick_u(x0, x1, y0, y1, z0, z1, rgb, studs=False, bevel=bevel)


def add(dst, *src):
    for s in src: P.merge(dst, s)
    return dst


def shade(c, k): return tuple(min(1.0, x * k) for x in c)


def scaled(bm, s):
    bmesh.ops.scale(bm, vec=(s, s, s), verts=bm.verts)
    return bm


def rivet(r, rgb):
    return bead(r, rgb, sz=0.7, seg=8, rings=4)


def rivet_ring(bm, R, y, n, r, rgb, a0=0.0):
    """一圈铆钉（绕 Y 轴半径 R）"""
    for k in range(n):
        a = a0 + k / n * math.tau
        add(bm, place(rivet(r, rgb), math.sin(a) * R, y, math.cos(a) * R))


def star(ro, ri, thick, rgb, n=5):
    pts = []
    for k in range(n * 2):
        a = k / (n * 2) * math.tau + math.pi / 2; r = ro if k % 2 == 0 else ri
        pts.append((math.cos(a) * r, math.sin(a) * r))
    bm = P.slab(pts, thick)
    for v in bm.verts: v.co = U(v.co.x, v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, rgb)


# ───────────────────────── 泡沫枪兵（高约 0.5）：积木兵的军绿版，钢盔、子弹带 ─────────────────────────
def trooper():
    o = {}
    f = box(-0.036, 0.036, -0.05, 0, -0.045, 0.062, MOSS, 0.016); add(f, box(-0.038, 0.038, -0.052, -0.041, -0.047, 0.064, BLACK, 0.004))
    for k in range(3): add(f, box(-0.024, 0.024, -0.03 + k * 0.009 - 0.01, -0.03 + k * 0.009 - 0.007, 0.05, 0.0635, CREAM, 0.001))   # 靴带
    o['Trooper_Foot'] = f
    PARTS['Trooper_FootL'] = (0.05, 0.05, 0.0); PARTS['Trooper_FootR'] = (-0.05, 0.05, 0.0)
    for i, c in enumerate([MOSS, SAND, OLIVE]):
        b = brick(0.17, 0.085, 0.13, c, (2, 2))
        if i == 1:                                                   # 腰带 + 弹药小包
            add(b, box(-0.087, 0.087, 0.03, 0.05, -0.067, 0.067, BLACK, 0.002))
            for x in (-0.05, 0.05):
                add(b, box(x - 0.022, x + 0.022, 0.012, 0.056, 0.06, 0.084, OLIVE, 0.004), box(x - 0.024, x + 0.024, 0.048, 0.06, 0.058, 0.086, MOSS, 0.003), place(rivet(0.0035, GOLD), x, 0.052, 0.087, rx=90))
            add(b, L([(0, 0.0), (0.024, 0.0), (0.03, 0.01), (0.03, 0.05), (0.022, 0.06), (0.01, 0.062), (0.01, 0.07), (0, 0.07)], SILVER, 14) if False else place(L([(0, -0.03), (0.02, -0.03), (0.026, -0.022), (0.026, 0.02), (0.018, 0.03), (0.008, 0.032), (0.008, 0.042), (0, 0.042)], SILVER, 14), 0.104, 0.03, 0.02))   # 水壶
            add(b, box(-0.012, 0.012, 0.03, 0.05, 0.064, 0.072, GOLD, 0.002))
        if i == 2:                                                   # 斜挎子弹带
            belt = box(-0.012, 0.012, -0.06, 0.06, -0.003, 0.003, BLACK, 0.002); place(belt, 0, 0.0425, 0.066, rz=-42); add(b, belt)
            for k in range(-2, 3):
                sh = L([(0, 0), (0.0045, 0), (0.0045, 0.012), (0, 0.017)], GOLD, 8)
                place(sh, k * 0.014 * math.cos(math.radians(48)), 0.0425 + k * 0.014 * math.sin(math.radians(48)) - 0.006, 0.07, rx=0); add(b, sh)
        o[f'Trooper_Brick_{i}'] = b
        PARTS[f'Trooper_Brick_{i}'] = (0.0, 0.05 + i * 0.087, 0.0)
    h = L([(0, 0), (0.025, 0), (0.025, 0.012), (0.058, 0.014), (0.062, 0.02), (0.062, 0.085), (0.058, 0.09), (0, 0.092)], YEL, 28)
    face(h, 0.05, 0.062, 0.06, 0.009, brows=True, frown=True)
    helmet = L([(0, 0.142), (0.03, 0.14), (0.052, 0.128), (0.066, 0.108), (0.072, 0.086), (0.08, 0.08), (0.08, 0.074), (0.066, 0.074), (0, 0.074)], OLIVE, 32)
    add(h, helmet)
    add(h, L([(0.0, 0.1), (0.0735, 0.1), (0.0735, 0.108), (0.0, 0.108)], MOSS, 32))    # 盔带
    badge = star(0.012, 0.005, 0.003, GOLD); place(badge, 0, 0.122, 0.064, rx=-22); add(h, badge)
    for sg in (-1, 1):                                               # 钢盔上的护目镜
        add(h, place(color_bm(P.torus(0.0135, 0.004, seg=16, sides=6), BLACK), sg * 0.021, 0.106, 0.069, rx=-8), place(bead(0.0115, (0.62, 0.84, 0.95), sz=0.45), sg * 0.021, 0.106, 0.069, rx=82))
    add(h, L([(0, 0.098), (0.0742, 0.098), (0.0742, 0.092), (0, 0.092)], BLACK, 32))
    rivet_ring(h, 0.0745, 0.0815, 12, 0.0026, GOLD)                  # 帽檐铆钉
    o['Trooper_Head'] = h
    PARTS['Trooper_Head'] = (0.0, 0.05 + 3 * 0.087 + 0.012, 0.0)
    a = L([(0, 0.012), (0.018, 0.01), (0.022, -0.01), (0.02, -0.07), (0.016, -0.085), (0, -0.088)], OLIVE, 14)
    add(a, place(bead(0.023, MOSS), 0, 0.004, 0))
    add(a, place(color_bm(P.torus(0.0205, 0.004, seg=18, sides=6), SAND), 0, -0.072, 0, rx=90))
    add(a, place(bead(0.02, YEL), 0, -0.1, 0))
    o['Trooper_Arm'] = a
    PARTS['Trooper_ArmL'] = (0.1, 0.05 + 3 * 0.087 - 0.02, 0.0); PARTS['Trooper_ArmR'] = (-0.1, 0.05 + 3 * 0.087 - 0.02, 0.0)
    return o


# ───────────────────────── 铁皮兵家族：车削铁皮玩具（铁皮兵 / 跟班小锡兵 / 指挥官）─────────────────────────
def tin_family(pre, s, coat, trim, trouser, hat):
    o = {}; S = lambda *p: tuple(c * s for c in p)
    leg = L([(0, -0.16), (0.036, -0.16), (0.038, -0.15), (0.036, -0.075), (0.03, -0.07), (0.03, 0), (0, 0)], trouser, 18)
    add(leg, L([(0, -0.16), (0.037, -0.16), (0.039, -0.15), (0.037, -0.08), (0, -0.08)], BLACK, 18))
    add(leg, box(-0.03, 0.03, -0.16, -0.125, 0.0, 0.066, BLACK, 0.012))
    add(leg, L([(0, -0.082), (0.0385, -0.082), (0.0385, -0.074), (0, -0.074)], trim, 18))
    o[pre + '_Foot'] = scaled(leg, s)
    PARTS[pre + '_FootL'] = S(0.046, 0.16, 0); PARTS[pre + '_FootR'] = S(-0.046, 0.16, 0)
    top = 0.385; h = 0.075
    for i in range(3):
        r0, r1 = 0.1 - i * 0.004, 0.096 - i * 0.004
        p = L([(0, 0), (r0, 0), (r1, h), (0, h)], coat, 32)
        add(p, L([(0, 0.0), (r0 + 0.0016, 0.0), (r0 + 0.0014, 0.009), (0, 0.009)], trim, 32))
        for y in ([0.04] if i != 1 else []): add(p, place(bead(0.009, GOLD, sz=0.8), 0, y, r1 + 0.002, rx=90))
        add(p, box(-0.006, 0.006, 0, h, -r1 - 0.004, -r1 + 0.008, shade(coat, 0.8), 0.002))    # 铁皮背缝
        for k in range(3): add(p, place(rivet(0.0035, shade(coat, 1.25)), 0, 0.016 + k * 0.021, -r1 - 0.0045, rx=90))
        add(p, box(-0.0035, 0.0035, 0.01, h, r1 - 0.004, r1 + 0.0025, trim, 0.001))             # 前襟金线
        rivet_ring(p, r0 - 0.0005, 0.004, 28, 0.0024, trim, a0=0.1)
        if i == 1:                                                   # 腰带 + 金扣
            add(p, L([(0, 0.022), (r0 + 0.002, 0.022), (r0 + 0.002, 0.05), (0, 0.05)], WHITE, 32))
            add(p, box(-0.02, 0.02, 0.02, 0.052, r0 - 0.002, r0 + 0.008, GOLD, 0.003))
        o[f'{pre}_Ring_{i}'] = scaled(p, s)
        PARTS[f'{pre}_Ring_{i}'] = S(0, 0.16 + i * h, 0)
    head = L([(0, 0), (0.03, 0), (0.03, 0.012), (0.05, 0.022), (0.064, 0.05), (0.064, 0.074), (0.052, 0.102), (0.03, 0.116), (0, 0.12)], SKIN, 28)
    add(head, L([(0, -0.002), (0.04, -0.002), (0.042, 0.012), (0.03, 0.016), (0, 0.016)], trim, 24))                  # 立领
    face(head, 0.06, 0.063, 0.058, 0.0085, brows=(pre != 'Minion'), frown=(pre == 'Cmd'), smile=(pre != 'Cmd'))
    if hat == 'shako':
        H = 0.13 if pre == 'Tin' else 0.085
        add(head, L([(0, 0.1), (0.066, 0.1), (0.071, 0.1 + H), (0.066, 0.104 + H), (0, 0.106 + H)], BLACK, 28))
        add(head, L([(0, 0.098), (0.0685, 0.098), (0.0695, 0.114), (0, 0.114)], trim, 28))
        pl = star(0.02, 0.009, 0.004, GOLD); place(pl, 0, 0.1 + H * 0.55, 0.07, rx=-6); add(head, pl)
        add(head, place(bead(0.022, RED, sz=1.5), 0, 0.106 + H + 0.03, 0))
        vis = box(-0.05, 0.05, 0.09, 0.102, 0.03, 0.094, BLACK, 0.005); add(head, vis)
    else:                                                            # 指挥官：横向双角帽 + 羽饰 + 小胡子
        arc = [(x, 0.086 + 0.05 * (1 - (x / 0.13) ** 2), 0) for x in [i * 0.026 - 0.13 for i in range(11)]]
        add(head, tube_u(arc, 0.034, NAVY, seg=14), tube_u([(x, y + 0.03, 0.012) for x, y, _ in arc], 0.008, GOLD, seg=8))
        add(head, place(bead(0.02, RED), 0, 0.142, 0.03), place(bead(0.018, WHITE, sz=1.7), 0, 0.178, 0.026))
        for sg in (-1, 1):
            add(head, tube_u([(sg * 0.006, 0.046, 0.064), (sg * 0.028, 0.044, 0.062), (sg * 0.042, 0.052, 0.054)], 0.0055, DARK, seg=8))
    if hat == 'shako':                                               # 下巴带
        add(head, tube_u([(-0.062, 0.1, 0.012), (-0.05, 0.05, 0.05), (-0.028, 0.018, 0.058), (0.028, 0.018, 0.058), (0.05, 0.05, 0.05), (0.062, 0.1, 0.012)], 0.0028, GOLD, seg=6))
        for sg in (-1, 1): add(head, place(rivet(0.0065, GOLD), sg * 0.0645, 0.1, 0.012, rz=sg * 90))
    o[pre + '_Head'] = scaled(head, s)
    PARTS[pre + '_Head'] = S(0, top + 0.002, 0)
    arm = L([(0, 0.014), (0.024, 0.01), (0.028, -0.012), (0.026, -0.1), (0.024, -0.112), (0, -0.114)], coat, 16)
    add(arm, place(bead(0.03, shade(coat, 0.9)), 0, 0.002, 0))
    add(arm, L([(0, -0.1), (0.029, -0.1), (0.029, -0.086), (0, -0.086)], trim, 16))
    add(arm, place(bead(0.024, WHITE), 0, -0.13, 0))
    for k in range(3): add(arm, place(rivet(0.0032, GOLD), 0.0, -0.093, 0.0, ry=0) if False else place(rivet(0.0034, trim if trim != WHITE else GOLD), -0.018 + k * 0.018, -0.094, 0.0255))   # 袖口小扣
    if pre == 'Cmd':                                                 # 金肩章 + 流苏
        add(arm, L([(0, 0.03), (0.04, 0.026), (0.044, 0.014), (0, 0.012)], GOLD, 18))
        for k in range(10):
            a = k / 10 * math.tau
            add(arm, tube_u([(math.cos(a) * 0.04, 0.014, math.sin(a) * 0.04), (math.cos(a) * 0.042, -0.012, math.sin(a) * 0.042)], 0.003, GOLD, seg=6))
    o[pre + '_Arm'] = scaled(arm, s)
    PARTS[pre + '_ArmL'] = S(0.122, top - 0.02, 0); PARTS[pre + '_ArmR'] = S(-0.122, top - 0.02, 0)
    return o


# ───────────────────────── 纸板忍者（高约 0.55）：瓦楞纸板剪出来的薄片、胶带 ─────────────────────────
def plate(x0, x1, y0, y1, rgb, t=0.016, z=0.0):
    return box(x0, x1, y0, y1, z - t / 2, z + t / 2, rgb, 0.003)


def ninja():
    o = {}
    f = add(plate(-0.04, 0.04, -0.09, 0, BLACK), box(-0.04, 0.04, -0.1, -0.075, -0.012, 0.06, BLACK, 0.005), plate(-0.042, 0.042, -0.05, -0.036, TAPE, 0.019))
    o['Ninja_Foot'] = f
    PARTS['Ninja_FootL'] = (0.05, 0.1, 0); PARTS['Ninja_FootR'] = (-0.05, 0.1, 0)
    for i, (c, h) in enumerate([(CARD, 0.09), (BLACK, 0.1)]):
        y = plate(-0.095, 0.095, 0, h, c, 0.022)
        if i == 0:
            add(y, plate(-0.1, 0.1, 0.03, 0.058, RED, 0.026))        # 腰带
            add(y, tube_u([(0.06, 0.04, 0.014), (0.1, 0.0, 0.016), (0.12, -0.05, 0.014)], 0.008, RED, seg=8))
            for x in (-0.078, 0.078): add(y, box(x - 0.011, x + 0.011, 0.002, 0.088, 0.0105, 0.0125, TAPE, 0.0006))   # 胶带
        else:
            add(y, plate(-0.018, 0.018, 0, h, CARD2, 0.025))         # 交领
            sh = star(0.03, 0.012, 0.005, SILVER, n=4); place(sh, 0.05, 0.055, 0.0145); add(y, sh, place(rivet(0.005, DARK), 0.05, 0.055, 0.017, rx=90))   # 胸前手里剑
            add(y, tube_u([(-0.09, 0.098, 0.0125), (0.09, 0.012, 0.0125)], 0.0035, CARD2, seg=6))   # 斜挎绳
            for k in range(-1, 2):                                    # 瓦楞侧边
                add(y, box(0.0955, 0.1, 0.03 + k * 0.024, 0.04 + k * 0.024, -0.01, 0.01, CARD2, 0.001))
        o[f'Ninja_Plate_{i}'] = y
        PARTS[f'Ninja_Plate_{i}'] = (0, 0.1 + sum([0.09, 0.1][:i]) + i * 0.002, 0)
    head = add(box(-0.075, 0.075, 0, 0.145, -0.013, 0.013, BLACK, 0.008), box(-0.066, 0.066, 0.058, 0.098, 0.011, 0.0165, SKIN, 0.003))
    for s in (-1, 1):
        add(head, box(s * 0.03 - 0.014, s * 0.03 + 0.014, 0.07, 0.086, 0.0155, 0.0185, WHITE, 0.002), box(s * 0.024 - 0.006, s * 0.024 + 0.006, 0.071, 0.085, 0.0175, 0.0205, DARK, 0.002))
        add(head, tube_u([(s * 0.03 - 0.016 * s, 0.098, 0.018), (s * 0.03 + 0.018 * s, 0.104, 0.018)], 0.0032, RED, seg=6))
    add(head, box(-0.08, 0.08, 0.116, 0.134, -0.016, 0.016, RED, 0.004))     # 红头带 + 飘带
    add(head, tube_u([(0.074, 0.124, -0.01), (0.11, 0.1, -0.02), (0.15, 0.116, -0.02)], 0.009, RED, seg=8))
    o['Ninja_Head'] = head
    PARTS['Ninja_Head'] = (0, 0.292, 0)
    a = add(box(-0.02, 0.02, -0.13, 0.016, -0.009, 0.009, BLACK, 0.004), box(-0.021, 0.021, -0.1, -0.082, -0.0105, 0.0105, TAPE, 0.002), box(-0.022, 0.022, -0.16, -0.128, -0.012, 0.012, SKIN, 0.008))
    o['Ninja_Arm'] = a
    PARTS['Ninja_ArmL'] = (0.116, 0.27, 0); PARTS['Ninja_ArmR'] = (-0.116, 0.27, 0)
    return o


# ───────────────────────── 弹簧小丑盒（盒高 0.26，弹出后 0.5）─────────────────────────
def jack():
    o = {}; W = 0.15; H = 0.26
    b = box(-W, W, 0, H, -W, W, PINK, 0.012)
    for sx in (-1, 1):
        add(b, box(sx * W - 0.014, sx * W + 0.014, 0, H, W - 0.014, W + 0.003, GOLD, 0.004), box(sx * W - 0.014, sx * W + 0.014, 0, H, -W - 0.003, -W + 0.014, GOLD, 0.004))
    add(b, box(-W - 0.003, W + 0.003, 0, 0.022, -W - 0.003, W + 0.003, GOLD, 0.004), box(-W - 0.003, W + 0.003, H - 0.022, H, -W - 0.003, W + 0.003, GOLD, 0.004))
    add(b, place(star(0.07, 0.03, 0.006, YEL), 0, H / 2, W + 0.003))
    add(b, place(star(0.07, 0.03, 0.006, MINT), W + 0.003, H / 2, 0, ry=90), place(star(0.07, 0.03, 0.006, LAV), -W - 0.003, H / 2, 0, ry=90))
    for sx in (-1, 1):
        for sy, y in ((1, H - 0.011), (-1, 0.011)):
            add(b, place(rivet(0.005, CREAM), sx * (W - 0.006), y, W + 0.003, rx=90), place(rivet(0.005, CREAM), sx * (W - 0.006), y, -W - 0.003, rx=90))
    add(b, place(color_bm(P.torus(0.082, 0.004, seg=30, sides=6), CREAM), 0, H / 2, W + 0.002, rx=90))     # 星星外的圆线
    for k in range(5):
        a = k / 5 * math.tau
        add(b, place(rivet(0.004, GOLD), math.sin(a) * 0.095, H / 2 + math.cos(a) * 0.095, W + 0.002, rx=90))
    o['Jack_Box'] = b
    lid = add(box(-W - 0.006, W + 0.006, 0, 0.022, 0, 2 * W + 0.012, BLUE, 0.008), place(bead(0.016, GOLD), 0, 0.03, W))
    o['Jack_Lid'] = lid
    PARTS['Jack_Lid'] = (0, H, -W - 0.006)
    crank = tube_u([(0, 0, 0), (0.05, 0, 0)], 0.008, SILVER, seg=8)
    add(crank, tube_u([(0.05, 0, 0), (0.05, 0.06, 0)], 0.007, SILVER, seg=8), tube_u([(0.05, 0.06, 0), (0.08, 0.06, 0)], 0.011, RED, seg=10))
    o['Jack_Crank'] = crank
    PARTS['Jack_Crank'] = (W, H * 0.55, 0)
    n = 56; coil = [(math.cos(i / n * math.tau * 6) * 0.05, i / n * 0.2, math.sin(i / n * math.tau * 6) * 0.05) for i in range(n + 1)]
    o['Jack_Spring'] = tube_u(coil, 0.008, SILVER, seg=8)
    PARTS['Jack_Spring'] = (0, 0.04, 0)
    c = L([(0, 0), (0.05, 0.002), (0.07, 0.03), (0.076, 0.07), (0.066, 0.105), (0.04, 0.13), (0, 0.138)], WHITE, 28)
    for i in range(14):                                              # 荷叶领
        a = i / 14 * math.tau
        add(c, place(bead(0.022, PINK if i % 2 else YEL, sz=0.55), math.cos(a) * 0.066, 0.006, math.sin(a) * 0.066))
    add(c, place(bead(0.017, RED), 0, 0.066, 0.076))                 # 红鼻子
    face(c, 0.086, 0.07, 0.056, 0.0095, brows=True, smile=True)
    for i, (dx, col) in enumerate([(-1, RED), (0, BLUE), (1, YEL)]):  # 三叉小丑帽 + 铃铛
        tip = (dx * 0.1, 0.216 if dx == 0 else 0.178, 0)
        add(c, tube_u([(dx * 0.03, 0.125, 0), (dx * 0.07, 0.175 if dx else 0.18, 0), tip], 0.03, col, seg=14) if False else color_bm(P.tube([U(dx * 0.03, 0.125, 0), U(dx * 0.066, 0.17, 0), U(*tip)], 0.03, seg=14, r_fn=lambda u: 1 - 0.8 * u), col))
        add(c, place(bead(0.013, GOLD), tip[0], tip[1] + 0.004, 0))
    for sg in (-1, 1):                                               # 两侧蓬蓬的头发
        for k in range(4): add(c, place(bead(0.02, (1.0, 0.5, 0.2)), sg * (0.07 + k * 0.004), 0.05 + k * 0.016, -0.006 - k * 0.002))
    o['Jack_Clown'] = c
    PARTS['Jack_Clown'] = (0, 0.24, 0)
    return o


# ───────────────────────── 纸飞机（翼展 0.36）─────────────────────────
def wing(sign):
    pts = [(0, 0.2), (0.018 * 0 + 0.17, -0.15), (0.02, -0.12), (0, -0.12)]
    if sign < 0: pts = [(-x, y) for x, y in reversed(pts)]
    bm = P.slab(pts, 0.004)
    for v in bm.verts: v.co = U(v.co.x, v.co.z, v.co.y)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, WHITE)


def plane():
    o = {}
    for nm, sg in (('L', 1), ('R', -1)):
        w = wing(sg)
        for k, c in enumerate((PINK, BLUE)):                        # 手画条纹
            t = -0.1 + k * 0.05
            add(w, box(min(0, sg * (0.03)), max(0, sg * 0.03) + (0.11 if sg > 0 else 0) - (0 if sg > 0 else 0), 0.0015, 0.0035, t, t + 0.022, c, 0.0005) if sg > 0 else box(-0.14, -0.03, 0.0015, 0.0035, t, t + 0.022, c, 0.0005))
        fold = box(-0.003 + sg * 0.05, 0.003 + sg * 0.05, 0.0015, 0.0028, -0.12, 0.06, shade(CREAM, 0.88), 0.0004)    # 折痕
        add(w, fold, box(-0.002 + sg * 0.11, 0.002 + sg * 0.11, 0.0015, 0.0028, -0.14, -0.06, shade(CREAM, 0.88), 0.0004))
        o['Plane_Wing' + nm] = w
    PARTS['Plane_WingL'] = (0.002, 0, 0); PARTS['Plane_WingR'] = (-0.002, 0, 0)
    keel = P.slab([(-0.06, 0.0), (0.0, -0.05), (-0.12, -0.05), (-0.12, 0.0)], 0.006)  # 中间折起的龙骨
    k = keel
    for v in k.verts: v.co = U(v.co.z, v.co.y + 0.0, -v.co.x * 0 + (v.co.x * -1 - 0.0) * 1.0 - 0.0 + 0.0)
    bmesh.ops.recalc_face_normals(k, faces=k.faces); color_bm(k, shade(CREAM, 0.95))
    nose = bead(0.016, RED, sz=0.8); place(nose, 0, -0.01, 0.2)
    add(k, nose)
    for s in (-1, 1): add(k, place(bead(0.011, DARK, sz=0.7), s * 0.022, 0.012, 0.13), place(bead(0.0035, WHITE, sz=0.7), s * 0.022 + 0.003, 0.02, 0.134))
    o['Plane_Keel'] = k
    return o


# ───────────────────────── 气球兵（总高 0.85）─────────────────────────
def balloon():
    o = {}
    prof = [(0, 0.0)] + [(0.205 * math.sqrt(1 - (2 * t - 1) ** 2) * (1.0 + 0.1 * (t - 0.5)), 0.42 * t) for t in [i / 24 for i in range(1, 24)]] + [(0, 0.42)]   # 上圆下略尖的气球
    b = L(prof, (1.0, 0.58, 0.72), 36)
    face(b, 0.25, 0.195, 0.1, 0.017, brows=False, smile=True)
    add(b, L([(0, -0.03), (0.018, -0.03), (0.024, -0.02), (0.008, -0.002), (0, 0.002)], shade((1.0, 0.58, 0.72), 0.84), 18))
    add(b, place(bead(0.016, WHITE, sz=1.4), 0.09, 0.33, 0.15))      # 高光贴片
    for sg in (-1, 1): add(b, place(bead(0.022, YEL, sz=0.6), sg * 0.024, -0.03, 0, rz=sg * 70))
    add(b, place(bead(0.009, shade(YEL, 0.9)), 0, -0.03, 0))
    for k in range(8):                                               # 气球表面的接缝细线
        a = k / 8 * math.tau
        arc = [(math.sin(a) * 0.205 * math.sqrt(1 - (2 * t - 1) ** 2) * (1.0 + 0.1 * (t - 0.5)) * 1.004, 0.42 * t, math.cos(a) * 0.205 * math.sqrt(1 - (2 * t - 1) ** 2) * (1.0 + 0.1 * (t - 0.5)) * 1.004) for t in [0.12 + i * 0.04 for i in range(20)]]
        if 0.9 < a < 5.4: add(b, tube_u(arc, 0.0016, shade((1.0, 0.58, 0.72), 0.9), seg=5))
    o['Balloon_Body'] = b
    PARTS['Balloon_Body'] = (0, 0.43, 0)
    s = tube_u([(0, 0, 0), (0.008, -0.1, 0.004), (-0.006, -0.2, -0.004), (0, -0.3, 0)], 0.0035, CREAM, seg=6)
    o['Balloon_String'] = s
    PARTS['Balloon_String'] = (0, 0.395, 0)
    k = L([(0, 0), (0.07, 0), (0.082, 0.012), (0.09, 0.075), (0.084, 0.08), (0.05, 0.08), (0, 0.08)], CARD, 20)
    for y in (0.02, 0.045): add(k, L([(0, y), (0.0875, y), (0.0885, y + 0.008), (0, y + 0.008)], CARD2, 20))
    add(k, L([(0, 0.08), (0.092, 0.08), (0.096, 0.09), (0.09, 0.098), (0, 0.098)], PEACH, 20))
    for j in range(3):                                               # 吊篮的三根绳
        a = j / 3 * math.tau + 0.5
        add(k, tube_u([(math.sin(a) * 0.08, 0.098, math.cos(a) * 0.08), (0, 0.11 + 0.02, 0)], 0.0028, CREAM, seg=6) if False else tube_u([(math.sin(a) * 0.08, 0.098, math.cos(a) * 0.08), (0, 0.15, 0)], 0.0028, CREAM, seg=6))
    rivet_ring(k, 0.0955, 0.089, 16, 0.0035, RED)
    o['Balloon_Basket'] = k
    PARTS['Balloon_Basket'] = (0, 0.0, 0)
    return o


# ───────────────────────── 颜料遥控车（长 0.34）─────────────────────────
def car():
    o = {}
    b = box(-0.085, 0.085, 0.03, 0.082, -0.16, 0.15, RED, 0.022)
    add(b, box(-0.07, 0.07, 0.076, 0.13, -0.09, 0.05, shade(RED, 1.1), 0.02))
    add(b, box(-0.062, 0.062, 0.09, 0.125, 0.04, 0.056, (0.72, 0.9, 1.0), 0.008), box(-0.072, -0.066, 0.088, 0.122, -0.08, 0.03, (0.72, 0.9, 1.0), 0.003), box(0.066, 0.072, 0.088, 0.122, -0.08, 0.03, (0.72, 0.9, 1.0), 0.003))
    add(b, box(-0.09, 0.09, 0.03, 0.05, 0.13, 0.17, BLACK, 0.008), box(-0.09, 0.09, 0.03, 0.05, -0.18, -0.145, BLACK, 0.008))
    for s in (-1, 1):
        add(b, place(bead(0.016, YEL, sz=0.8), s * 0.052, 0.06, 0.152, rx=90))
        add(b, tube_u([(s * 0.05, 0.08, -0.14), (s * 0.05, 0.135, -0.15)], 0.006, BLACK, seg=8))
    add(b, box(-0.09, 0.09, 0.13, 0.142, -0.178, -0.122, BLACK, 0.004))    # 尾翼
    add(b, L([(0, 0.13), (0.05, 0.13), (0.056, 0.142), (0.056, 0.185), (0.044, 0.198), (0.012, 0.2), (0.012, 0.215), (0, 0.215)], MINT, 22))   # 车顶颜料罐
    add(b, L([(0, 0.148), (0.0575, 0.148), (0.0575, 0.17), (0, 0.17)], PINK, 22))
    add(b, tube_u([(0.05, 0.13, -0.05), (0.06, 0.28, -0.075)], 0.0028, SILVER, seg=6), place(bead(0.01, RED), 0.06, 0.283, -0.075))
    add(b, place(star(0.022, 0.01, 0.004, YEL), 0, 0.09, 0.145, rx=-68))
    for s in (-1, 1):
        add(b, box(s * 0.0855 - 0.0012, s * 0.0855 + 0.0012, 0.036, 0.078, -0.03, 0.078, shade(RED, 0.72), 0.0005))    # 门缝
        add(b, box(s * 0.0862 - 0.0015, s * 0.0862 + 0.0015, 0.058, 0.064, 0.044, 0.066, SILVER, 0.0008))              # 门把手
        add(b, tube_u([(s * 0.074, 0.105, 0.048), (s * 0.1, 0.112, 0.056)], 0.003, BLACK, seg=6), place(bead(0.012, SILVER, sz=0.7), s * 0.106, 0.114, 0.056))   # 后视镜
    add(b, box(-0.03, 0.03, 0.05, 0.066, 0.1495, 0.1545, WHITE, 0.001), place(star(0.006, 0.0026, 0.002, RED), 0, 0.058, 0.155))   # 车牌
    rivet_ring(b, 0.0565, 0.16, 10, 0.0026, GOLD)
    o['Car_Body'] = b
    w = L([(0, -0.024), (0.03, -0.024), (0.042, -0.018), (0.044, 0), (0.042, 0.018), (0.03, 0.024), (0, 0.024)], BLACK, 20)
    add(w, L([(0, -0.0255), (0.022, -0.0255), (0.022, 0.0255), (0, 0.0255)], YEL, 16))
    for k in range(14):                                              # 轮胎花纹
        a = k / 14 * math.tau
        add(w, place(box(-0.0045, 0.0045, -0.003, 0.003, -0.02, 0.02, shade(BLACK, 1.9), 0.001), math.cos(a) * 0.0435, 0, math.sin(a) * 0.0435, ry=-math.degrees(a)) if False else color_bm(P.box(math.cos(a) * 0.0435, math.sin(a) * 0.0435, 0, 0.007, 0.007, 0.042, 0.001), shade(BLACK, 1.9)))
    for k in range(5):
        a = k / 5 * math.tau
        add(w, color_bm(P.box(math.cos(a) * 0.013, math.sin(a) * 0.013, 0.026, 0.007, 0.007, 0.003, 0.001), SILVER), color_bm(P.box(math.cos(a) * 0.013, math.sin(a) * 0.013, -0.026, 0.007, 0.007, 0.003, 0.001), SILVER))
    place(w, 0, 0, 0, rz=90)
    o['Car_Wheel'] = w
    for i, (x, z) in enumerate([(0.092, 0.095), (-0.092, 0.095), (0.092, -0.1), (-0.092, -0.1)]): PARTS[f'Car_Wheel_{i}'] = (x, 0.044, z)
    return o


# ───────────────────────── 修理机器人（高 0.45）─────────────────────────
def bot():
    o = {}
    tread = box(-0.03, 0.03, 0, 0.07, -0.08, 0.09, BLACK, 0.026)
    for k in range(7): add(tread, box(-0.031, 0.031, 0.001, 0.009, -0.07 + k * 0.026, -0.058 + k * 0.026, shade(BLACK, 1.8), 0.002))
    add(tread, place(L([(0, -0.032), (0.022, -0.032), (0.022, 0.032), (0, 0.032)], SILVER, 14), 0, 0.035, 0.0, rz=90))
    o['Bot_Tread'] = tread
    PARTS['Bot_TreadL'] = (0.075, 0.0, 0); PARTS['Bot_TreadR'] = (-0.075, 0.0, 0)
    for i, (c, h) in enumerate([(MINT, 0.09), (WHITE, 0.09)]):
        p = L([(0, 0), (0.1, 0), (0.112, 0.012), (0.112, h - 0.012), (0.1, h), (0, h)], c, 30)
        if i == 0:
            add(p, box(-0.05, 0.05, 0.02, 0.07, 0.1, 0.116, SILVER, 0.005))
            for x in (-0.03, 0, 0.03): add(p, place(bead(0.007, RED if x == 0 else YEL, sz=0.7), x, 0.045, 0.116, rx=90))
        else:
            add(p, place(star(0.022, 0.01, 0.004, PINK), 0, 0.045, 0.113))
            for s in (-1, 1): add(p, box(s * 0.06 - 0.012, s * 0.06 + 0.012, 0.02, 0.07, 0.09, 0.114, SILVER, 0.003))
        rivet_ring(p, 0.1125, 0.012, 16, 0.003, SILVER, a0=0.2); rivet_ring(p, 0.1125, h - 0.012, 16, 0.003, SILVER, a0=0.2)
        for k in range(4): add(p, place(box(-0.022, 0.022, -0.0016, 0.0016, -0.002, 0.002, shade(c, 0.7), 0.0005), 0, 0.03 + k * 0.01, -0.113))   # 背后散热格
        o[f'Bot_Plate_{i}'] = p
        PARTS[f'Bot_Plate_{i}'] = (0, 0.075 + i * 0.092, 0)
    h = L([(0, 0), (0.05, 0), (0.05, 0.014), (0.09, 0.016), (0.098, 0.03), (0.098, 0.06), (0.09, 0.066), (0, 0.066)], SILVER, 30)
    dome = L([(0, 0.15), (0.03, 0.146), (0.06, 0.13), (0.082, 0.1), (0.088, 0.066), (0, 0.066)], (0.72, 0.92, 1.0), 30)
    add(h, dome)
    for s in (-1, 1):
        add(h, place(bead(0.024, BLACK, sz=0.9), s * 0.036, 0.09, 0.074, rx=90), place(bead(0.008, WHITE, sz=0.8), s * 0.036 + 0.008, 0.098, 0.094, rx=90))
    add(h, tube_u([(0, 0.148, 0), (0, 0.205, 0)], 0.004, SILVER, seg=8), place(bead(0.016, RED), 0, 0.212, 0))
    for s in (-1, 1):
        add(h, place(L([(0, -0.012), (0.024, -0.012), (0.028, -0.006), (0.028, 0.006), (0.024, 0.012), (0, 0.012)], MINT, 16), s * 0.1, 0.042, 0, rz=90), place(rivet(0.007, GOLD), s * 0.114, 0.042, 0, rz=s * 90))   # 耳朵
    rivet_ring(h, 0.0985, 0.023, 18, 0.0028, GOLD)
    o['Bot_Head'] = h
    PARTS['Bot_Head'] = (0, 0.26, 0)
    arm = add(place(bead(0.024, SILVER), 0, 0, 0), tube_u([(0, 0, 0), (0, -0.1, 0.01)], 0.012, MINT, seg=10))
    o['Bot_ArmOil'] = add(arm, L([(0, -0.14), (0.026, -0.138), (0.03, -0.116), (0.018, -0.1), (0, -0.098)], GOLD, 14), tube_u([(0, -0.14, 0), (0, -0.175, 0.012)], 0.0045, GOLD, seg=8))
    wr = add(place(bead(0.024, SILVER), 0, 0, 0), tube_u([(0, 0, 0), (0, -0.09, 0.01)], 0.012, MINT, seg=10), box(-0.01, 0.01, -0.16, -0.085, 0.0, 0.016, SILVER, 0.003))
    add(wr, box(-0.028, -0.008, -0.195, -0.155, 0.0, 0.016, SILVER, 0.003), box(0.008, 0.028, -0.195, -0.155, 0.0, 0.016, SILVER, 0.003))
    o['Bot_ArmWrench'] = wr
    PARTS['Bot_ArmL'] = (0.128, 0.235, 0); PARTS['Bot_ArmR'] = (-0.128, 0.235, 0)
    return o


def main():
    P.reset(); allp = {}
    allp.update(trooper())
    allp.update(tin_family('Tin', 1.0, RED, GOLD, WHITE, 'shako'))
    allp.update(tin_family('Minion', 0.6, BLUE, WHITE, CREAM, 'shako'))
    allp.update(tin_family('Cmd', 1.5, NAVY, GOLD, WHITE, 'bicorne'))
    for fn in (ninja, jack, plane, balloon, car, bot): allp.update(fn())
    for name, bm in allp.items(): BP.export(name, bm, 40)
    json.dump({k: list(v) for k, v in PARTS.items()}, open(os.path.join(OUT, 'W2E_parts.json'), 'w'), indent=1)
    bad = {k: v for k, v in P.report.items() if v['boundary'] or v['nonmanifold']}
    print('W2E', len(allp), '总面数', sum(v['faces'] for v in P.report.values()), '有破面的部件', bad)


if __name__ == '__main__':
    main()
