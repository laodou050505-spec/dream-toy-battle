"""封面“玩具小剧场”（Blender 里运行）：Blender -b -P Tools/blender_title_stage.py -- Assets/_Bang/Art/Models/Proc/UI
封面不再是“浮空卡片 + 按钮”，而是摆在房间里的一个实物玩具：
  Title_Stage      圆鼓形小舞台：奶油木边（中段一圈粉色腰线）+ 顶沿一圈薄荷/粉/黄交替的圆钉
  Title_StageTop   舞台顶面薰衣草绒布（略低于木边）
  Title_SignBack   招牌底板：深莓色绒布圆角板（让粉彩 Logo 跳出来），下沿一条更深的带子印英文名
  Title_SignFrame  招牌外框：珊瑚粉圆管，四角圆角，外沿一圈小凹座（灯泡座）
  Title_Pole       招牌支柱：粉白螺旋糖果棍（左右各一根，Unity 里复用）
  Title_BtnBase    街机大按钮底座：奶油圆台 + 金色压圈
  Title_BtnDome    按钮帽：珊瑚粉糖果圆顶（单独物体，按下时下沉）
  Title_Block      字母积木：圆角立方体，五面奶油色、每面一圈彩色内框（正面印字在 Unity 里用文字）
  Title_Plaque     按钮名牌：小木牌（圆角薄板 + 两颗钉）
所有部件原点在各自的“摆放点”，摆放位置写进 Title_layout.json（标题坐标：+Y 上、-Z 朝玩家，单位米）。
全部封闭流形，顶点色 sRGB。
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_portals as BP

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
CREAM, CREAM2, PINK, MINT, YEL, LAV, LAV2 = (1.0, 0.94, 0.86), (0.96, 0.86, 0.76), (1.0, 0.62, 0.74), (0.6, 0.9, 0.8), (1.0, 0.86, 0.45), (0.78, 0.7, 0.96), (0.7, 0.6, 0.9)
PLUM, PLUM2, CORAL, GOLD, WHITE = (0.38, 0.22, 0.46), (0.28, 0.15, 0.36), (1.0, 0.55, 0.6), (1.0, 0.8, 0.42), (1.0, 0.97, 0.95)
L = {}

# 版式（标题坐标）
STAGE_C, STAGE_R, STAGE_H = (0.0, -0.46, 0.08), 0.5, 0.075
TOP_Y = STAGE_C[1] + STAGE_H
SIGN_C, SIGN_W, SIGN_H = (0.0, 0.2, 0.16), 1.04, 0.42
POLE_X = 0.44


def to_unity(bm):
    for v in bm.verts: v.co = U(v.co.x, v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def lathe_y(profile, seg, col_fn):
    """绕 Unity Y 轴车削：profile [(r, y)]，按 y 上色"""
    bm = P.lathe(profile, seg)
    lay = bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops:
            c = col_fn(l.vert.co.z, l.vert.co); l[lay] = (c[0], c[1], c[2], 1.0)
    for v in bm.verts: v.co = Vector((v.co.x, v.co.z, v.co.y))     # 先换成 (x, y=高, z)，再整体 to_unity
    return bm


def stage():
    R, H = STAGE_R, STAGE_H
    prof = [(0, 0), (R - 0.02, 0), (R, 0.012), (R + 0.004, 0.02), (R + 0.004, H - 0.016), (R, H - 0.006), (R - 0.012, H), (R - 0.03, H), (R - 0.03, H - 0.01), (0, H - 0.01)]
    def col(y, co):
        return PINK if 0.032 < y < 0.046 else CREAM
    bm = lathe_y(prof, 96, col)
    for i in range(16):                                       # 顶沿圆钉
        a = i / 16 * math.tau
        st = P.lathe([(0, 0), (0.014, 0), (0.016, 0.006), (0.012, 0.013), (0, 0.016)], 16)
        c = (MINT, PINK, YEL)[i % 3]
        BP.color_bm(st, c)
        for v in st.verts: v.co = Vector((v.co.x + math.cos(a) * (R - 0.015), v.co.z + H - 0.002, v.co.y + math.sin(a) * (R - 0.015)))
        P.merge(bm, st)
    L['Title_Stage'] = STAGE_C
    return to_unity(bm)


def stage_top():
    R = STAGE_R - 0.03
    prof = [(0, -0.012), (R - 0.004, -0.012), (R, -0.006), (R, 0), (0, 0.002)]
    bm = lathe_y(prof, 96, lambda y, co: LAV if (co.x * co.x + co.y * co.y) ** 0.5 < R - 0.03 else LAV2)
    L['Title_StageTop'] = (STAGE_C[0], TOP_Y - 0.008, STAGE_C[2])
    return to_unity(bm)


def rounded_rect(w, h, r, n=8):
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def slab_xy(pts, z0, z1, col, bev=0.004):
    """XY 平面轮廓沿 Unity z 挤出（z0 前、z1 后），折边倒圆"""
    bm = bmesh.new()
    a = [bm.verts.new((x, y, z0)) for x, y in pts]
    b = [bm.verts.new((x, y, z1)) for x, y in pts]
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for i in range(len(pts)):
        j = (i + 1) % len(pts); bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bev > 0:
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle(0)) > 40]
        bmesh.ops.bevel(bm, geom=edges, offset=bev, segments=3, profile=0.5, affect='EDGES', clamp_overlap=True)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return BP.color_bm(bm, col)


def sign_back():
    bm = slab_xy(rounded_rect(SIGN_W, SIGN_H, 0.07), -0.012, 0.012, PLUM, 0.006)
    band = slab_xy(rounded_rect(SIGN_W - 0.12, 0.06, 0.024), -0.016, -0.004, PLUM2, 0.004)   # 下沿印英文名的深色带
    for v in band.verts: v.co.y -= SIGN_H / 2 - 0.06
    P.merge(bm, band)
    L['Title_SignBack'] = SIGN_C
    return to_unity(bm)


def sign_frame():
    # 外框：沿圆角矩形扫一根圆管（闭合）——用环面式扫掠：每个轮廓点一圈截面
    pts = rounded_rect(SIGN_W + 0.03, SIGN_H + 0.03, 0.085, 10)
    n = len(pts); seg = 12; r = 0.02
    bm = bmesh.new(); rings = []
    for i in range(n):
        p0, p1 = Vector((*pts[i - 1], 0)), Vector((*pts[(i + 1) % n], 0))
        t = (p1 - p0).normalized(); nrm = Vector((t.y, -t.x, 0))      # 外法线（逆时针轮廓）
        c = Vector((*pts[i], 0))
        rings.append([bm.verts.new(c + (nrm * math.cos(k / seg * math.tau) + Vector((0, 0, 1)) * math.sin(k / seg * math.tau)) * r) for k in range(seg)])
    for i in range(n):
        a, b = rings[i], rings[(i + 1) % n]
        for k in range(seg): bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    BP.color_bm(bm, CORAL)
    # 灯泡座位置：沿外框均匀取点（Unity 里放发光小灯泡，做跑马灯）
    per = 0.0; segs = []
    for i in range(n):
        a, b = Vector(pts[i]), Vector(pts[(i + 1) % n]); segs.append((a, b, (b - a).length)); per += (b - a).length
    bulbs = []; step = per / 30; acc = 0.0; nxt = 0.0
    for a, b, l in segs:
        while nxt <= acc + l and len(bulbs) < 30:
            u = (nxt - acc) / l; p = a.lerp(b, u); bulbs.append((round(p.x, 4), round(p.y, 4), -0.02)); nxt += step
        acc += l
    L['Title_SignFrame'] = SIGN_C
    L['bulbs'] = bulbs
    return to_unity(bm)


def pole():
    h = SIGN_C[1] - SIGN_H / 2 - TOP_Y + 0.01
    bm = P.lathe([(0, 0), (0.016, 0), (0.016, h), (0.012, h + 0.008), (0, h + 0.01)], 20)
    lay = bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops:
            co = l.vert.co; a = math.atan2(co.y, co.x)
            s = math.sin(a * 2 + co.z * 70)                   # 螺旋糖果条纹
            c = PINK if s > 0.2 else WHITE
            l[lay] = (c[0], c[1], c[2], 1.0)
    for v in bm.verts: v.co = Vector((v.co.x, v.co.z, v.co.y))
    L['Title_Pole_L'] = (-POLE_X, TOP_Y, SIGN_C[2]); L['Title_Pole_R'] = (POLE_X, TOP_Y, SIGN_C[2])
    return to_unity(bm)


def btn_base():
    prof = [(0, 0), (0.1, 0), (0.104, 0.006), (0.1, 0.028), (0.09, 0.034), (0.082, 0.034), (0.082, 0.03), (0, 0.03)]
    bm = lathe_y(prof, 48, lambda y, co: CREAM)
    ring = P.torus(0.078, 0.009, seg=48, sides=10)
    BP.color_bm(ring, GOLD)
    for v in ring.verts: v.co = Vector((v.co.x, v.co.z + 0.036, v.co.y))
    P.merge(bm, ring)
    L['Title_BtnBase'] = (0.0, TOP_Y, -0.12)
    return to_unity(bm)


def btn_dome():
    prof = [(0, 0)]
    for k in range(1, 13):
        a = k / 12 * math.pi / 2
        prof.append((0.066 * math.cos(a * 0.92) if k < 12 else 0, 0.004 + 0.05 * math.sin(a)))
    prof = [(0, 0), (0.068, 0), (0.068, 0.006)] + [(0.066 * math.cos(k / 12 * math.pi / 2), 0.006 + 0.048 * math.sin(k / 12 * math.pi / 2)) for k in range(1, 12)] + [(0, 0.055)]
    bm = lathe_y(prof, 48, lambda y, co: CORAL)
    L['Title_BtnDome'] = (0.0, TOP_Y + 0.03, -0.12)
    return to_unity(bm)


def block():
    s = 0.12; h = s / 2
    bm = P.box(0, 0, 0, s, s, s, bevel=0.012)
    BP.color_bm(bm, CREAM)
    # 每个侧面一圈彩色内框（凸起 1.5 毫米的方环）
    cols = [PINK, MINT, YEL, LAV]
    for k, (nx, nz) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):
        outer = rounded_rect(s - 0.026, s - 0.026, 0.012, 4); inner = rounded_rect(s - 0.046, s - 0.046, 0.006, 4)
        ring = bmesh.new()
        o = [ring.verts.new((x, y, 0)) for x, y in outer]; i_ = [ring.verts.new((x, y, 0)) for x, y in inner]
        o2 = [ring.verts.new((x, y, 0.0016)) for x, y in outer]; i2 = [ring.verts.new((x, y, 0.0016)) for x, y in inner]
        m = len(outer)
        for q in range(m):
            r_ = (q + 1) % m
            ring.faces.new((o[q], o[r_], i_[r_], i_[q])); ring.faces.new((o2[q], i2[q], i2[r_], o2[r_]))
            ring.faces.new((o[q], o2[q], o2[r_], o[r_])); ring.faces.new((i_[q], i_[r_], i2[r_], i2[q]))
        bmesh.ops.recalc_face_normals(ring, faces=ring.faces)
        BP.color_bm(ring, cols[k])
        for v in ring.verts:                                    # 环本来朝 -Z（玩家），转到对应侧面
            x, y, z = v.co.x, v.co.y, -h - v.co.z
            if nx == 0 and nz == -1: v.co = Vector((x, y, z))
            elif nx == 1: v.co = Vector((-z, y, x))
            elif nz == 1: v.co = Vector((-x, y, -z))
            else: v.co = Vector((z, y, -x))
        P.merge(bm, ring)
    for i, x in enumerate((-0.27, 0.27)):
        L[f'Title_Block_{i}'] = (x, TOP_Y + h, -0.02)
    return to_unity(bm)


def plaque():
    bm = slab_xy(rounded_rect(0.3, 0.07, 0.02), -0.005, 0.005, CREAM2, 0.003)
    for x in (-0.13, 0.13):
        nail = P.lathe([(0, -0.009), (0.006, -0.009), (0.007, -0.006), (0.005, -0.004), (0, -0.004)], 12)
        BP.color_bm(nail, GOLD)
        for v in nail.verts: v.co = Vector((v.co.x + x, v.co.y, v.co.z))
        P.merge(bm, nail)
    L['Title_Plaque'] = (0.0, STAGE_C[1] + STAGE_H * 0.5, STAGE_C[2] - STAGE_R - 0.012)
    return to_unity(bm)


def main():
    P.reset()
    parts = (('Title_Stage', stage), ('Title_StageTop', stage_top), ('Title_SignBack', sign_back), ('Title_SignFrame', sign_frame),
             ('Title_Pole', pole), ('Title_BtnBase', btn_base), ('Title_BtnDome', btn_dome), ('Title_Block', block), ('Title_Plaque', plaque))
    for n, fn in parts:
        BP.export(n, fn(), 35)
    L['sign'] = {'w': SIGN_W, 'h': SIGN_H}
    json.dump(L, open(os.path.join(OUT, 'Title_layout.json'), 'w'), indent=1)
    print('TITLE', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
