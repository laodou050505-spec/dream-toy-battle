"""第 1 章小怪的分部件模型 + 玩法道具（Blender 里运行）：
  Blender -b -P Tools/blender_enemies_w1.py -- <输出目录>

每个部件单独导出一个 FBX，原点放在关节（脚、手臂在胯 / 肩，头在脖子，积木在底面中心），
Unity 里按 W1E_parts.json 的安装位置组装成带层级的预制体，零件各自做程序动作（不用骨骼）。
坐标按 Unity：小怪正面朝 +Z，脚底在 y=0。全部封闭流形（proc_lib.check），顶点色 sRGB。

  积木插旗兵  Planter_Foot / Planter_Brick_0..2 / Planter_Head / Planter_Arm / Planter_Pack（背旗）
  积木胖墩    Brute_Foot / Brute_Brick_0..7 / Brute_Head / Brute_Arm / Brute_Pack
  发条老鼠    Mouse_TailSeg（尾巴一节，Unity 里串成链）/ Mouse_Key（蝴蝶发条钥匙）/ Mouse_Foot（小圆脚）
  旗子        Flag_Base_S / Flag_Base_L（积木底座 + 旗杆 + 金顶球）、Flag_Cloth_S / Flag_Cloth_L（分段旗布，顶点动画飘）
  道具        BlockBall（投石车抛的积木球）、GiftBox / GiftLid（补给礼盒）
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_w1_set as W1
import blender_portals as BP

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
rnd = random.Random(20261028)
color_bm = BP.color_bm
PINK, CREAM, BLUE, YEL = (1.0, 0.66, 0.78), (1.0, 0.95, 0.86), (0.6, 0.78, 1.0), (1.0, 0.86, 0.42)
MINT, PEACH, LAV, DARK, WHITE = (0.6, 0.9, 0.78), (1.0, 0.76, 0.62), (0.78, 0.68, 1.0), (0.22, 0.2, 0.28), (1.0, 0.98, 0.95)
GOLD = (1.0, 0.82, 0.42)
PARTS = {}            # 名称 → 在小怪局部的安装位置（Unity）


def brick(w, h, d, rgb, studs=(2, 2), stud_r=None):
    """以底面中心为原点的积木：倒角方块 + 顶面凸点（按 studs 排）"""
    bm = W1.brick_u(-w / 2, w / 2, 0, h, -d / 2, d / 2, W1.jitter(rgb, 0.015), studs=False, bevel=min(0.012, h * 0.18))
    nx, nz = studs
    r = stud_r or min(w / nx, d / nz) * 0.3
    for i in range(nx):
        for k in range(nz):
            x = -w / 2 + (i + 0.5) * w / nx; z = -d / 2 + (k + 0.5) * d / nz
            s = W1.stud(*U(x, h, z), r=r, h=r * 0.62); color_bm(s, rgb); P.merge(bm, s)
            ring = P.torus(r * 0.62, max(0.0012, r * 0.07), seg=20, sides=6)          # 凸点顶面的压印环（细化：近看像真积木）
            for v in ring.verts: v.co = U(x + v.co.x, h + r * 0.62 - 0.0035 + v.co.z, z + v.co.y)   # 半埋在凸点顶面里，露出一圈细棱
            P.merge(bm, color_bm(ring, tuple(min(1.0, c * 1.06) for c in rgb)))
    # 下沿分模线：一圈比积木略深一档的细边（0.8 mm 凸出、4 mm 高）
    band = W1.brick_u(-w / 2 - 0.0008, w / 2 + 0.0008, 0.006, 0.010, -d / 2 - 0.0008, d / 2 + 0.0008, tuple(c * 0.84 for c in rgb), studs=False, bevel=0.0015)
    P.merge(bm, band)
    return bm


def bead(r, rgb, sz=1.0, seg=14, rings=8):
    prof = [(0, -r * sz)] + [(r * math.sin(math.pi * k / rings), -r * sz * math.cos(math.pi * k / rings)) for k in range(1, rings)] + [(0, r * sz)]
    return color_bm(P.lathe(prof, seg), rgb)


def place(bm, x, y, z, rx=0.0, ry=0.0, rz=0.0):
    """bm 用 Unity 坐标建在原点；绕 Unity 轴旋转后平移"""
    R = (Matrix.Rotation(math.radians(ry), 3, 'Y') @ Matrix.Rotation(math.radians(rx), 3, 'X') @ Matrix.Rotation(math.radians(rz), 3, 'Z'))
    bmesh.ops.transform(bm, matrix=BP.umat(R, (x, y, z)), verts=bm.verts)
    return bm


def tube_u(pts, r, rgb, seg=10):
    return color_bm(P.tube([U(*p) for p in pts], r, seg=seg), rgb)


def face(bm, y, zf, w, eye_r, brows=False, smile=True, cheeks=True, frown=None, sly=False):
    """印刷感的脸：两颗黑亮小珠眼 + 微笑弧线 + 腮红（贴在 z=zf 的前表面上）"""
    for s in (-1, 1):
        e = bead(eye_r, DARK, sz=0.7); place(e, s * w * 0.42, y, zf + eye_r * 0.35, rx=90); P.merge(bm, e)
        hl = bead(eye_r * 0.32, WHITE, sz=0.6); place(hl, s * w * 0.42 + eye_r * 0.3, y + eye_r * 0.35, zf + eye_r * 0.85, rx=90); P.merge(bm, hl)
        if cheeks:
            c = color_bm(P.slab([(math.cos(t) * eye_r * 1.3, math.sin(t) * eye_r * 0.8) for t in [i / 16 * math.tau for i in range(16)]], 0.003), (1.0, 0.62, 0.7))
            for v in c.verts: v.co = U(v.co.x + s * w * 0.62, v.co.y + y - eye_r * 2.0, zf + 0.002 + v.co.z)
            bmesh.ops.recalc_face_normals(c, faces=c.faces); P.merge(bm, c)
        if brows:
            yi, yo = (y + eye_r * 1.75, y + eye_r * 2.35) if sly else (y + eye_r * 2.4, y + eye_r * 1.7)   # sly：内低外高（调皮得意）；默认：内高外低（胖墩的闷闷不乐）
            b = tube_u([(s * w * 0.22, yi, zf + 0.004), (s * w * 0.6, yo, zf + 0.004)], eye_r * 0.32, DARK, seg=6); P.merge(bm, b)
    if frown is None: frown = brows
    if smile:
        pts = [(math.sin(a) * w * 0.26, y - eye_r * 2.1 - math.cos(a) * w * 0.07 * (1 if not frown else -1), zf + 0.003) for a in [(-0.9 + 1.8 * i / 10) for i in range(11)]]
        P.merge(bm, tube_u(pts, eye_r * 0.28, DARK, seg=6))
    return bm


# ───────────────────────── 积木插旗兵（高约 0.52 m）─────────────────────────
def planter_parts():
    out = {}
    # 脚：圆头小靴子，原点在顶面中心（胯）
    f = W1.brick_u(-0.036, 0.036, -0.05, 0, -0.045, 0.062, (0.62, 0.8, 1.0), studs=False, bevel=0.016)   # 粉蓝圆头靴
    sole = W1.brick_u(-0.038, 0.038, -0.052, -0.041, -0.047, 0.064, (0.3, 0.32, 0.5), studs=False, bevel=0.004); P.merge(f, sole)   # 深色鞋底（细化）
    out['Planter_Foot'] = f
    PARTS['Planter_FootL'] = (0.05, 0.05, 0.0); PARTS['Planter_FootR'] = (-0.05, 0.05, 0.0)
    # 三块身体积木：底 蓝 / 中 奶油（印白色 X 腰带）/ 顶 粉
    cols = [BLUE, CREAM, PINK]
    for i, c in enumerate(cols):
        b = brick(0.17, 0.085, 0.13, c, (2, 2))
        if i == 1:
            for sgn in (-1, 1):
                strap = W1.brick_u(-0.012, 0.012, -0.04, 0.04, -0.002, 0.004, WHITE, studs=False, bevel=0.002)
                place(strap, 0, 0.0425, 0.065, rz=sgn * 35); P.merge(b, strap)
        out[f'Planter_Brick_{i}'] = b
        PARTS[f'Planter_Brick_{i}'] = (0.0, 0.05 + i * 0.087, 0.0)
    # 头：圆柱积木头 + 顶钮 + 脖子，原点在脖子底
    head = P.lathe([(0, 0), (0.025, 0), (0.025, 0.012), (0.058, 0.014), (0.062, 0.02), (0.062, 0.085), (0.058, 0.092), (0.03, 0.094), (0.03, 0.112), (0.026, 0.115), (0, 0.115)], 28)
    color_bm(head, YEL)
    face(head, 0.055, 0.062, 0.06, 0.009, brows=True, frown=False, sly=True)   # 调皮得意的笑脸
    # 红色小军帽（插旗“兵”的身份）：帽顶微鼓 + 前檐 + 金色帽徽
    cap = P.lathe([(0, 0.084), (0.066, 0.084), (0.067, 0.1), (0.06, 0.122), (0.04, 0.134), (0, 0.137)], 28)
    color_bm(cap, (0.84, 0.24, 0.27)); P.merge(head, cap)
    visor = P.slab([(math.cos(a) * 0.068, math.sin(a) * 0.05) for a in [math.pi * (0.05 + 0.9 * i / 12) for i in range(13)]] + [(0, 0.012)], 0.006)
    for v in visor.verts: v.co = U(v.co.x, 0.088 + v.co.z, 0.034 + v.co.y)                              # 帽檐朝前伸出
    bmesh.ops.recalc_face_normals(visor, faces=visor.faces); P.merge(head, color_bm(visor, (0.62, 0.15, 0.2)))
    badge = []
    for k in range(10):
        a = k / 10 * math.tau + math.pi / 2; r = 0.011 if k % 2 == 0 else 0.0048
        badge.append((math.cos(a) * r, math.sin(a) * r))
    bd = P.slab(badge, 0.003)
    for v in bd.verts: v.co = U(v.co.x, 0.108 + v.co.y, 0.0635 + v.co.z)
    bmesh.ops.recalc_face_normals(bd, faces=bd.faces); P.merge(head, color_bm(bd, GOLD))
    out['Planter_Head'] = head
    PARTS['Planter_Head'] = (0.0, 0.05 + 3 * 0.087 + 0.012, 0.0)
    # 手臂：上臂圆柱 + C 形手，原点在肩，自然下垂（沿 -y）
    arm = P.lathe([(0, 0.012), (0.018, 0.01), (0.022, -0.01), (0.02, -0.07), (0.016, -0.085), (0, -0.088)], 14)
    color_bm(arm, PINK)
    sh = bead(0.023, tuple(c * 0.92 for c in PINK)); place(sh, 0, 0.004, 0); P.merge(arm, sh)              # 肩关节球
    cuff = P.torus(0.0205, 0.004, seg=18, sides=6)
    for v in cuff.verts: v.co = U(v.co.x, -0.072 + v.co.z, v.co.y)
    P.merge(arm, color_bm(cuff, WHITE))                                                                   # 手腕白袖口
    hand = P.torus(0.016, 0.008, seg=16, sides=8)
    bmesh.ops.delete(hand, geom=[v for v in hand.verts if v.co.x > 0.012 and abs(v.co.y) < 0.008], context='VERTS') if False else None
    for v in hand.verts: v.co = U(v.co.x, -0.1 + v.co.z, v.co.y * 1.0 + 0.0)
    color_bm(hand, PINK); P.merge(arm, hand)
    out['Planter_Arm'] = arm
    PARTS['Planter_ArmL'] = (0.1, 0.05 + 3 * 0.087 - 0.02, 0.0); PARTS['Planter_ArmR'] = (-0.1, 0.05 + 3 * 0.087 - 0.02, 0.0)
    # 背旗：细杆 + 三角绒布小旗，原点在杆底（背后绑带处）
    pack = tube_u([(0, 0, 0), (0, 0.22, 0)], 0.005, (0.9, 0.82, 0.7), seg=8)
    top = bead(0.01, GOLD); place(top, 0, 0.225, 0); P.merge(pack, top)
    # 彩虹条纹三角旗（5 条色带）+ 一颗白星
    stripes = [(1.0, 0.62, 0.72), (1.0, 0.82, 0.55), (1.0, 0.95, 0.6), (0.62, 0.9, 0.78), (0.6, 0.78, 1.0)]
    L, Hh = 0.12, 0.075
    for k, col in enumerate(stripes):
        y0, y1 = Hh * k / 5 + 0.0004, Hh * (k + 1) / 5 - 0.0004          # 色带之间留 0.8 mm 细缝：共用顶点会被合并成非流形边
        x0 = L * (1 - abs((y0 - Hh / 2) / (Hh / 2))); x1 = L * (1 - abs((y1 - Hh / 2) / (Hh / 2)))
        poly = [(0, y0), (x0, y0), (x1, y1), (0, y1)] if k < 2 else [(0, y0), (x0, y0), (x1, y1), (0, y1)]
        poly = [p for i, p in enumerate(poly) if i == 0 or p != poly[i - 1]]
        if len(poly) < 3: continue
        st = P.slab(poly, 0.004)
        for v in st.verts: v.co = U(v.co.x, 0.135 + v.co.y, v.co.z)
        bmesh.ops.recalc_face_normals(st, faces=st.faces); P.merge(pack, color_bm(st, col))
    star = []
    for k in range(10):
        a = k / 10 * math.tau + math.pi / 2; r = 0.014 if k % 2 == 0 else 0.006
        star.append((0.035 + math.cos(a) * r, Hh / 2 + math.sin(a) * r))
    for zs in (1, -1):
        sv = P.slab(star, 0.0015)
        for v in sv.verts: v.co = U(v.co.x, 0.135 + v.co.y, zs * (0.003 + v.co.z))
        bmesh.ops.recalc_face_normals(sv, faces=sv.faces); P.merge(pack, color_bm(sv, WHITE))
    out['Planter_Pack'] = pack
    PARTS['Planter_Pack'] = (0.0, 0.16, -0.075)
    return out


# ───────────────────────── 积木胖墩（高约 0.86 m）─────────────────────────
def brute_parts():
    out = {}
    f = W1.brick_u(-0.062, 0.062, -0.075, 0, -0.07, 0.07, MINT, studs=False, bevel=0.018)       # 两节积木腿（原点在胯）
    low2 = W1.brick_u(-0.066, 0.066, -0.155, -0.08, -0.075, 0.095, LAV, studs=False, bevel=0.018); P.merge(f, low2)
    knee = W1.brick_u(-0.068, 0.068, -0.083, -0.071, -0.074, 0.074, (0.98, 0.76, 0.25), studs=False, bevel=0.004); P.merge(f, knee)   # 芥末金膝箍
    out['Brute_Foot'] = f
    PARTS['Brute_FootL'] = (0.1, 0.155, 0.0); PARTS['Brute_FootR'] = (-0.1, 0.155, 0.0)
    cols = [BLUE, PEACH, LAV, MINT, PEACH, BLUE, MINT, LAV]
    for i in range(8):
        lvl, side = divmod(i, 2)
        b = brick(0.2, 0.12, 0.26, cols[i], (2, 3))
        if lvl == 3:                                                     # 最上一层前面压一枚金色星形胸章（左右各半，拼起来一颗星）
            star = []
            for k in range(10):
                a = k / 10 * math.tau + math.pi / 2; r = 0.05 if k % 2 == 0 else 0.022
                star.append((math.cos(a) * r, math.sin(a) * r))
            st = P.slab(star, 0.004)
            for v in st.verts: v.co = U(v.co.x - 0.1, 0.06 + v.co.y, 0.131 + v.co.z)       # 左块积木局部 x=-0.1 = 身体正中
            bmesh.ops.recalc_face_normals(st, faces=st.faces)
            if side == 0: P.merge(b, color_bm(st, (0.98, 0.76, 0.25)))
        out[f'Brute_Brick_{i}'] = b
        PARTS[f'Brute_Brick_{i}'] = ((0.1 if side == 0 else -0.1), 0.155 + lvl * 0.122, 0.0)
    # 方头：大积木头 + 两颗顶钮 + 皱眉的脸，原点在底面
    head = W1.brick_u(-0.11, 0.11, 0, 0.17, -0.1, 0.1, BLUE, studs=False, bevel=0.025)
    for sx in (-1, 1):
        s = W1.stud(*U(sx * 0.05, 0.17, 0), r=0.032, h=0.02); color_bm(s, BLUE); P.merge(head, s)
    face(head, 0.085, 0.1, 0.16, 0.014, brows=True)
    out['Brute_Head'] = head
    PARTS['Brute_Head'] = (0.0, 0.155 + 4 * 0.122 + 0.02, 0.0)
    # 粗手臂：两节积木 + 圆拳，原点在肩
    arm = bmesh.new()
    for k, (c, y0, y1) in enumerate(((BLUE, -0.07, 0.025), (LAV, -0.145, -0.072), (MINT, -0.22, -0.147))):
        seg = W1.brick_u(-0.045, 0.045, y0, y1, -0.05, 0.05, c, studs=False, bevel=0.016); P.merge(arm, seg)
    fist = bead(0.052, PEACH, sz=0.92); place(fist, 0, -0.27, 0.005); P.merge(arm, fist)
    for sx in (-1, 1):                                                   # 肘部两颗铆钉
        rv = bead(0.009, (0.98, 0.76, 0.25), sz=0.6); place(rv, sx * 0.032, -0.108, 0.052, rx=90); P.merge(arm, rv)
    for k in range(3):                                         # 拳头上三道指缝
        kn = bead(0.016, PEACH, sz=0.8); place(kn, -0.025 + k * 0.025, -0.255, 0.048); P.merge(arm, kn)
    out['Brute_Arm'] = arm
    PARTS['Brute_ArmL'] = (0.25, 0.155 + 4 * 0.122 - 0.04, 0.0); PARTS['Brute_ArmR'] = (-0.25, 0.155 + 4 * 0.122 - 0.04, 0.0)
    pack = tube_u([(0, 0, 0), (0, 0.42, 0)], 0.008, (0.9, 0.82, 0.7), seg=8)
    top = bead(0.016, GOLD); place(top, 0, 0.43, 0); P.merge(pack, top)
    cloth = P.slab([(0, 0), (0.2, 0.06), (0, 0.13)], 0.005)
    for v in cloth.verts: v.co = U(v.co.x, 0.27 + v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(cloth, faces=cloth.faces); color_bm(cloth, (1.0, 0.9, 0.55)); P.merge(pack, cloth)
    out['Brute_Pack'] = pack
    PARTS['Brute_Pack'] = (0.0, 0.38, -0.14)
    return out


# ───────────────────────── 发条老鼠的 Blender 部件 ─────────────────────────
def mouse_parts():
    out = {}
    # 尾巴一节：毛线小珠（略长的椭球），原点在前端，沿 -z 伸出；Unity 里 7 节串成链，逐节缩小、逐节滞后摆动
    seg = bead(0.011, (1.0, 0.66, 0.78), sz=1.0)
    for v in seg.verts: v.co = Vector((v.co.x, v.co.y, v.co.z)) ;
    bmesh.ops.scale(seg, vec=Vector((1, 1.7, 1)), verts=seg.verts)       # Blender y = Unity -z 方向拉长
    bmesh.ops.translate(seg, vec=U(0, 0, -0.016), verts=seg.verts)
    out['Mouse_TailSeg'] = seg
    # 蝴蝶发条钥匙：杆 + 两片心形翼，原点在杆底（插进背里），杆沿 +y
    key = bmesh.new()
    shaft = P.lathe([(0, 0), (0.005, 0), (0.005, 0.03), (0.009, 0.032), (0.009, 0.04), (0, 0.041)], 12)
    color_bm(shaft, GOLD); P.merge(key, shaft)
    for s in (-1, 1):
        o = []
        for i in range(32):
            a = i / 32 * math.tau
            r = 0.016 * (1 - 0.18 * math.sin(a) ** 4) * (1 + 0.12 * math.cos(a))
            o.append((s * (0.006 + 0.016 + math.cos(a) * r * 1.1), math.sin(a) * r))
        sl = P.slab(o, 0.005)
        for v in sl.verts: v.co = Vector((v.co.x, v.co.z, v.co.y + 0.052))   # 立在 xz……翼面朝 ±z（Unity：x 横、y 上）
        for v in sl.verts: v.co = U(-v.co.x, v.co.z, -v.co.y)
        bmesh.ops.recalc_face_normals(sl, faces=sl.faces)
        P.merge(key, color_bm(sl, GOLD))
    out['Mouse_Key'] = key
    foot = bead(0.012, (0.8, 0.74, 0.86), sz=0.7)
    out['Mouse_Foot'] = foot
    return out


# ───────────────────────── 旗子 ─────────────────────────
def flag_parts():
    """燕尾三角旗：粉彩横条纹 + 积木军团徽记（一块 2×2 小积木），刻意避开任何真实国旗 / 政治符号的配色与图案"""
    out = {}
    for tag, sc, ca, cb, emb in (('S', 1.0, (0.55, 0.88, 0.78), CREAM, YEL), ('L', 1.6, LAV, (1.0, 0.8, 0.9), (0.6, 0.86, 1.0))):
        base = brick(0.12 * sc, 0.05 * sc, 0.12 * sc, PINK if tag == 'S' else BLUE, (2, 2))
        pole = tube_u([(0, 0.05 * sc, 0), (0, 0.52 * sc, 0)], 0.007 * sc, (0.95, 0.9, 0.82), seg=10); P.merge(base, pole)
        ball = bead(0.016 * sc, GOLD); place(ball, 0, 0.535 * sc, 0); P.merge(base, ball)
        out[f'Flag_Base_{tag}'] = base
        # 旗布：分段网格（12×6），原点在旗杆上的挂点；尾端开燕尾口（中间行最短）；Unity 里用顶点动画（离旗杆越远摆得越大）
        nx, ny = 12, 6
        Wc, Hc = 0.2 * sc, 0.13 * sc
        cl = bmesh.new(); vs = {}
        for i in range(nx + 1):
            for j in range(ny + 1):
                t = abs(2 * j / ny - 1)
                x = i / nx * Wc * (1 - 0.3 * (1 - t))
                vs[(i, j, 0)] = cl.verts.new(U(x, -j / ny * Hc, 0.003))
                vs[(i, j, 1)] = cl.verts.new(U(x, -j / ny * Hc, -0.003))
        row = {}
        for i in range(nx):
            for j in range(ny):
                row[cl.faces.new((vs[(i, j, 0)], vs[(i + 1, j, 0)], vs[(i + 1, j + 1, 0)], vs[(i, j + 1, 0)]))] = j
                row[cl.faces.new((vs[(i, j, 1)], vs[(i, j + 1, 1)], vs[(i + 1, j + 1, 1)], vs[(i + 1, j, 1)]))] = j
        for i in range(nx):                       # 上下边
            for j, (a, b) in ((0, ((i, 0), (i + 1, 0))), (ny, ((i, ny), (i + 1, ny)))):
                row[cl.faces.new((vs[a + (0,)], vs[a + (1,)], vs[b + (1,)], vs[b + (0,)]))] = min(j, ny - 1)
        for j in range(ny):                       # 左右边
            for i in (0, nx):
                row[cl.faces.new((vs[(i, j, 0)], vs[(i, j + 1, 0)], vs[(i, j + 1, 1)], vs[(i, j, 1)]))] = j
        bmesh.ops.recalc_face_normals(cl, faces=cl.faces)
        lay = cl.loops.layers.color.new('Col')
        for f, j in row.items():                  # 三条横纹：上下 ca，中间 cb
            c = cb if j // 2 == 1 else ca
            for l in f.loops: l[lay] = (c[0], c[1], c[2], 1.0)
        # 徽记：两面各一块圆角小积木（2×2 凸点）
        ex, ey, ew, eh = Wc * 0.36, -Hc * 0.5, 0.05 * sc, 0.036 * sc
        rr = eh * 0.3; outline = []
        for cx, cy, a0 in ((ew / 2 - rr, eh / 2 - rr, 0), (-ew / 2 + rr, eh / 2 - rr, 90), (-ew / 2 + rr, -eh / 2 + rr, 180), (ew / 2 - rr, -eh / 2 + rr, 270)):
            for k in range(5):
                a = math.radians(a0 + k * 22.5); outline.append((ex + cx + math.cos(a) * rr, ey + cy + math.sin(a) * rr))
        for zs in (1, -1):
            s2 = P.slab(outline, 0.003)
            for v in s2.verts: v.co = U(v.co.x, v.co.y, zs * (0.0045 + v.co.z))
            bmesh.ops.recalc_face_normals(s2, faces=s2.faces); P.merge(cl, color_bm(s2, emb))
            for dx in (-1, 1):
                for dy in (-1, 1):
                    d = bead(eh * 0.16, WHITE, sz=0.55); place(d, ex + dx * ew * 0.24, ey + dy * eh * 0.24, zs * 0.0068, rx=90); P.merge(cl, d)
        out[f'Flag_Cloth_{tag}'] = cl
        PARTS[f'Flag_Cloth_{tag}'] = (0.008 * sc, 0.5 * sc, 0.0)
    return out


# ───────────────────────── 道具 ─────────────────────────
def prop_parts():
    out = {}
    # 积木球：圆球 + 一圈圈凸点（像积木拼成的球），直径 0.18
    ball = bead(0.085, PEACH, seg=24, rings=14)
    for k in range(5):
        lat = -60 + k * 30
        n = max(4, int(10 * math.cos(math.radians(lat))))
        for i in range(n):
            lon = i / n * 360 + k * 13
            d = Vector((math.cos(math.radians(lat)) * math.cos(math.radians(lon)), math.sin(math.radians(lat)), math.cos(math.radians(lat)) * math.sin(math.radians(lon))))
            s = W1.stud(*U(*(d * 0.08)), r=0.016, h=0.012, up=(U(*d) - U(0, 0, 0)).normalized())
            color_bm(s, [PINK, MINT, YEL, BLUE, LAV][(i + k) % 5]); P.merge(ball, s)
    out['BlockBall'] = ball
    # 补给礼盒：盒身（原点底面）+ 盒盖（原点在盖底，打开时翻起）+ 丝带蝴蝶结
    box = W1.brick_u(-0.11, 0.11, 0, 0.17, -0.11, 0.11, (1.0, 0.86, 0.5), studs=False, bevel=0.012)
    for rot in (0, 90):
        rb = W1.brick_u(-0.022, 0.022, 0, 0.172, -0.112, 0.112, PINK, studs=False, bevel=0.004); place(rb, 0, 0, 0, ry=rot); P.merge(box, rb)
    out['GiftBox'] = box
    lid = W1.brick_u(-0.12, 0.12, 0, 0.05, -0.12, 0.12, (1.0, 0.8, 0.45), studs=False, bevel=0.012)
    for rot in (0, 90):
        rb = W1.brick_u(-0.024, 0.024, -0.002, 0.052, -0.122, 0.122, PINK, studs=False, bevel=0.004); place(rb, 0, 0, 0, ry=rot); P.merge(lid, rb)
    for s in (-1, 1):
        loop = P.torus(0.035, 0.012, seg=18, sides=8)
        for v in loop.verts: v.co = U(v.co.x * 1.2 + s * 0.04, 0.085 + v.co.y * 0.9, v.co.z)
        P.merge(lid, color_bm(loop, PINK))
    knot = bead(0.018, (1.0, 0.55, 0.7)); place(knot, 0, 0.065, 0); P.merge(lid, knot)
    out['GiftLid'] = lid
    PARTS['GiftLid'] = (0.0, 0.17, 0.0)
    return out


def main():
    P.reset()
    allp = {}
    for fn in (planter_parts, brute_parts, mouse_parts, flag_parts, prop_parts):
        allp.update(fn())
    for name, bm in allp.items():
        BP.export(name, bm, 40)
    json.dump({k: list(v) for k, v in PARTS.items()}, open(os.path.join(OUT, 'W1E_parts.json'), 'w'), indent=1)
    print('W1E', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
