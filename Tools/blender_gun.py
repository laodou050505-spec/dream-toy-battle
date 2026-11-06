"""玩具沙鹰（糖果玻璃半透明）分部件模型（Blender 里运行）：
  Blender -b -P Tools/blender_gun.py -- <输出目录>

造型完全按参考图 ArtSource/Refs/Enemies_W1/Gun_ref.png 描出来（不是方块圆柱拼凑）：
  每个零件先在参考图上取侧面轮廓（像素坐标，按 1932 显示尺寸），曲线部位用 Chaikin 细分磨圆，
  再沿枪身宽度挤出成实体，只给折角边倒圆角（曲面上不重复倒角，避免自穿插）——轮廓就是真正的沙鹰：
  套筒前低后高的阶梯 + 上导轨齿 + 前后准星 + 两组防滑纹 + 侧面长凹槽 + 保险钮 + 抛壳窗；
  枪身的护尘盖、弧形扳机护圈、海狸尾、后倾握把 + 薄荷握片和螺丝；弹匣（透过糖果玻璃看得见）+ 薄荷底板。
Unity 坐标：枪口朝 +Z，握把朝 -Y，原点在“手握的位置”（握把顶部中心，参考图像素 (1400, 830)）。
会动的零件单独导出，原点在各自的运动轴上，Unity 里按 Gun_parts.json 组装：
  Gun_Frame（静止） Gun_Slide（开火沿 -Z 后坐） Gun_Mag（换弹沿 -Y 掉出 / 推入） Gun_Hammer / Gun_Trigger（绕 X 轴转） Gun_Shell（弹壳）
全部封闭流形，顶点色 sRGB（Unity 里用 Bang/CandyGlass 半透明材质，顶点色区分零件色块）。
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_portals as BP

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
color_bm = BP.color_bm
PEACH, PEACH2, LIGHT = (1.0, 0.72, 0.6), (1.0, 0.63, 0.5), (1.0, 0.82, 0.7)
MINT, MINT2, SILVER, CREAM, GOLD, PINK = (0.62, 0.9, 0.78), (0.52, 0.84, 0.72), (0.9, 0.92, 0.95), (1.0, 0.92, 0.84), (1.0, 0.84, 0.5), (1.0, 0.62, 0.74)
PARTS = {}

S = 0.215 / 1585.0          # 米 / 像素（参考图枪长 1585 像素 → 21.5 cm 的玩具枪）
OX, OY = 1400.0, 830.0      # 原点（握把顶部中心）在参考图上的像素位置
WS, WF, WG = 0.017, 0.0145, 0.0155     # 半宽：套筒 / 枪身 / 握把


def zy(p):
    """参考图像素 → (Unity z, y)"""
    return ((OX - p[0]) * S, (OY - p[1]) * S)


def chaikin(pts, it):
    for _ in range(it):
        n = len(pts); out = []
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            out += [(a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25), (a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75)]
        pts = out
    return pts


def prism(pts, x0, x1, rgb, bev=0.0022, smooth=0, seg=3, sharp=28):
    """侧面轮廓（参考图像素）沿 Unity x 从 x0 挤出到 x1；smooth = Chaikin 次数；只给折角 > sharp 度的边倒圆角"""
    q = [zy(p) for p in chaikin(list(pts), smooth)]
    area = sum(q[i][0] * q[(i + 1) % len(q)][1] - q[(i + 1) % len(q)][0] * q[i][1] for i in range(len(q)))
    if area < 0: q = q[::-1]
    bm = bmesh.new()
    a = [bm.verts.new(U(x0, y, z)) for z, y in q]
    b = [bm.verts.new(U(x1, y, z)) for z, y in q]
    bm.faces.new(a); bm.faces.new(b[::-1])
    for i in range(len(q)):
        j = (i + 1) % len(q); bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bev > 0:
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle(0)) > sharp]
        bmesh.ops.bevel(bm, geom=edges, offset=bev, offset_type='OFFSET', segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, rgb)


def both(pts, x_in, x_out, rgb, **kw):
    """两侧对称的贴面细节（x_in..x_out 是外侧那面的范围）"""
    bm = prism(pts, x_in, x_out, rgb, **kw)
    P.merge(bm, prism(pts, -x_out, -x_in, rgb, **kw))
    return bm


def cyl_x(p, r_px, x0, x1, rgb, seg=20, bev=0.25):
    """沿 Unity x 的圆钮（螺丝、销钉、保险钮），p 为参考图像素中心"""
    z, y = zy(p); r = r_px * S; e = r * bev
    bm = P.lathe([(0, x0), (r - e, x0), (r, x0 + e), (r, x1 - e), (r - e, x1), (0, x1)], seg)
    for v in bm.verts: v.co = U(v.co.z, y + v.co.y, z + v.co.x)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, rgb)


def cyl_z(p, r_px, px0, px1, rgb, seg=24):
    """沿 Unity z 的圆柱（枪口），px0/px1 是参考图上的前后像素 x"""
    _, y = zy(p); r = r_px * S; z0, z1 = zy((px1, 0))[0], zy((px0, 0))[0]; e = r * 0.18
    bm = P.lathe([(0, z0), (r - e, z0), (r, z0 + e), (r, z1 - e), (r - e, z1), (0, z1)], seg)
    for v in bm.verts: v.co = U(v.co.x, y + v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, rgb)


def cyl_zx(x, p, r_px, px0, px1, rgb, seg=20, cap=0.18):
    """沿 Unity z 的小圆柱，横向偏到 Unity x 处（准星白点、抛壳窗里露出的弹壳）"""
    _, y = zy(p); r = r_px * S; z0, z1 = zy((px1, 0))[0], zy((px0, 0))[0]; e = r * cap
    bm = P.lathe([(0, z0), (r - e, z0), (r, z0 + e), (r, z1 - e), (r - e, z1), (0, z1)], seg)
    for v in bm.verts: v.co = U(x + v.co.x, y + v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, rgb)


def rebase(bm, name, p):
    """把零件原点移到它的运动轴（参考图像素 p），并记进 Gun_parts.json"""
    z, y = zy(p)
    for v in bm.verts: v.co -= U(0, y, z)
    PARTS[name] = (0.0, round(y, 5), round(z, 5))
    return bm


# ───────────────────────── 精修小件：鼓鼓的软糖贴花 / 防滑圆点 / 挂绳环 ─────────────────────────
def star_pts(ro, ri, n=5, round_k=6):
    pts = []
    for i in range(n * 2):
        a = math.pi / 2 + i * math.pi / n
        r = ro if i % 2 == 0 else ri
        pts.append((math.cos(a) * r, math.sin(a) * r))
    out = []
    for i in range(len(pts)):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % len(pts)]
        k = 0.4 if i % 2 == 0 else 0.22
        a = (p1[0] + (p0[0] - p1[0]) * k, p1[1] + (p0[1] - p1[1]) * k); b = (p1[0] + (p2[0] - p1[0]) * k, p1[1] + (p2[1] - p1[1]) * k)
        for j in range(round_k):
            t = j / (round_k - 1)
            out.append(((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * p1[0] + t * t * b[0], (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * p1[1] + t * t * b[1]))
    return out


def heart_pts(sz, n=26):
    out = []
    for i in range(n):
        t = i / n * math.tau
        x = 16 * math.sin(t) ** 3; y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        out.append((x * sz / 17, y * sz / 17 + sz * 0.1))
    return out


def decal(center_px, shape, x_face, side, thick, puff, rgb):
    """侧面上的鼓鼓软糖贴花：shape 是以 center 为原点的 (横向=枪长方向 z, 纵向 y) 米制轮廓；从 x_face 向外鼓出"""
    zc, yc = zy(center_px)
    bm = bmesh.new(); n = len(shape)
    def V(u, v, h): return bm.verts.new(U(side * (x_face + h), yc + v, zc - u))
    base = [V(u, v, -0.0006) for u, v in shape]
    rings = [base]
    for s_, h_ in ((1.0, 0.55), (0.88, 0.85), (0.6, 1.0), (0.28, 1.06)):
        rings.append([V(u * s_, v * s_, thick * h_ + puff * (1 - s_)) for u, v in shape])
    top = V(0, 0, thick * 1.08 + puff); cb = V(0, 0, -0.0006)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((base[j], base[i], cb))
        for a, b in zip(rings[:-1], rings[1:]): bm.faces.new((a[i], a[j], b[j], b[i]))
        bm.faces.new((rings[-1][i], rings[-1][j], top))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, rgb)


def inside(pt, poly):
    x, y = pt; c = False
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[(i + 1) % len(poly)]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1: c = not c
    return c


def dist_poly(pt, poly):
    best = 1e9
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[(i + 1) % len(poly)]
        dx, dy = x2 - x1, y2 - y1; L = dx * dx + dy * dy or 1
        t = max(0, min(1, ((pt[0] - x1) * dx + (pt[1] - y1) * dy) / L))
        best = min(best, math.hypot(pt[0] - x1 - dx * t, pt[1] - y1 - dy * t))
    return best


# ───────────────────────── 枪身（静止）─────────────────────────
def frame():
    # 护尘盖 + 握把 + 海狸尾：一整块轮廓（握把后倾，底部略外扩）
    outline = [(330, 800), (1650, 790), (1702, 800), (1756, 826), (1742, 846), (1666, 878), (1680, 1000), (1693, 1380), (1666, 1468),
               (1630, 1480), (1228, 1480), (1203, 1455), (1192, 1150), (1182, 895), (1160, 872), (850, 868), (360, 862), (335, 850)]
    bm = prism(outline, -WF, WF, PEACH, bev=0.0024, smooth=2)
    # 弧形扳机护圈：沿中线扫管，再把截面压成扁椭圆（宽 1.3 cm）
    path = [(1185, 880), (1182, 960), (1165, 1050), (1120, 1110), (1040, 1136), (950, 1126), (880, 1080), (851, 1000), (848, 930), (852, 866)]
    pts = []
    for p in chaikin_open(path, 2):
        z, y = zy(p); pts.append(U(0, y, z))
    guard = P.tube(pts, 21 * S, seg=12)
    for v in guard.verts: v.co.x *= 0.0066 / (21 * S)
    P.merge(bm, color_bm(guard, PEACH))
    # 两颗前销钉、握把前销、空仓挂机杆（左侧）
    for p, r in (((862, 856), 24), ((1112, 834), 24), ((1180, 1068), 21)):
        P.merge(bm, cyl_x(p, r, -WF - 0.0022, WF + 0.0022, PEACH2, seg=18))
    P.merge(bm, prism([(1190, 797), (1365, 797), (1374, 814), (1362, 833), (1196, 833)], -WF - 0.002, -WF + 0.001, PEACH2, bev=0.0007, smooth=1))
    # 薄荷握片（两侧）+ 两颗银色一字螺丝
    panel = [(1212, 890), (1236, 832), (1385, 814), (1402, 872), (1642, 906), (1662, 1000), (1675, 1378), (1650, 1440), (1262, 1446), (1229, 1420)]
    P.merge(bm, both(panel, WG - 0.0015, WG + 0.0032, MINT, bev=0.0016, smooth=2))
    # 握片：错位排列的小圆点防滑纹（避开螺丝和星星徽章），中间一颗金色软糖星星
    screws = ((1366, 966), (1460, 1324)); medal = (1535, 1135)
    for row, yy in enumerate(range(905, 1430, 40)):
        for xx in range(1240 + (20 if row % 2 else 0), 1660, 40):
            q = (xx, yy)
            if not inside(q, panel) or dist_poly(q, panel) < 26: continue
            if any(math.hypot(q[0] - c[0], q[1] - c[1]) < 62 for c in screws) or math.hypot(q[0] - medal[0], q[1] - medal[1]) < 105: continue
            for sd in (-1, 1):
                z, y = zy(q); r = 10.5 * S
                dot = P.lathe([(0, 0), (r, 0), (r * 0.85, r * 0.45), (r * 0.5, r * 0.8), (0, r * 0.95)], 8)
                for v in dot.verts: v.co = U(sd * (WG + 0.0030 + v.co.z), y + v.co.y, z + v.co.x)
                bmesh.ops.recalc_face_normals(dot, faces=dot.faces)
                P.merge(bm, color_bm(dot, MINT2))
    for sd in (-1, 1):
        P.merge(bm, decal(medal, star_pts(0.0095, 0.0051), WG + 0.0030, sd, 0.0018, 0.0014, GOLD))
    # 握把底部挂绳环（玩具枪的小细节）
    z, y = zy((1600, 1492))
    ring = P.torus(0.0056, 0.0016, seg=20, sides=8)
    for v in ring.verts: v.co = U(v.co.z, y - 0.0045 + v.co.y, z + v.co.x)
    P.merge(bm, color_bm(ring, CREAM))
    # 护尘盖下的战术导轨：一条凸起的导轨 + 4 个横槽（玩具枪常见的配件导轨）
    P.merge(bm, prism([(372, 866), (835, 866), (842, 874), (842, 894), (372, 894), (365, 886)], -WF * 0.72, WF * 0.72, PEACH2, bev=0.0012, smooth=0))
    for k in range(5):
        x = 400 + k * 86
        P.merge(bm, prism([(x, 890), (x + 40, 890), (x + 40, 899), (x, 899)], -WF * 0.74, WF * 0.74, LIGHT, bev=0.0005))
    # 护尘盖下沿三道防滑槽（凸棱）
    for k in range(3):
        x = 470 + k * 90
        P.merge(bm, both([(x, 846), (x + 46, 846), (x + 46, 858), (x, 858)], WF - 0.001, WF + 0.0009, PEACH2, bev=0.0004))
    for p in screws:
        for s in (-1, 1):
            x0, x1 = (WG + 0.0028, WG + 0.0048) if s > 0 else (-WG - 0.0048, -WG - 0.0028)
            P.merge(bm, cyl_x(p, 33, x0, x1, SILVER, seg=20, bev=0.35))
            slot = prism([(p[0] - 26, p[1] - 6), (p[0] + 26, p[1] - 6), (p[0] + 26, p[1] + 6), (p[0] - 26, p[1] + 6)],
                         x1 - 0.0004 if s > 0 else x0 - 0.0003, x1 + 0.0003 if s > 0 else x0 + 0.0004, (0.7, 0.72, 0.78), bev=0)
            ang = math.radians(-40)
            c = U(0, *reversed(zy(p)))
            bmesh.ops.rotate(slot, verts=slot.verts, cent=c, matrix=Matrix.Rotation(ang, 3, 'X'))
            P.merge(bm, slot)
    return bm


def chaikin_open(pts, it):
    for _ in range(it):
        out = [pts[0]]
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            out += [(a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25), (a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75)]
        out.append(pts[-1]); pts = out
    return pts


# ───────────────────────── 套筒（开火后坐）─────────────────────────
def slide():
    outline = [(215, 522), (232, 505), (1065, 505), (1082, 487), (1515, 487), (1532, 507), (1646, 770), (1636, 800), (240, 800), (215, 784)]
    bm = prism(outline, -WS, WS, PEACH, bev=0.0042, smooth=0, seg=4)
    # 上导轨齿（前段）、准星、照门
    for x in (445, 525, 605, 685, 765):
        P.merge(bm, prism([(x, 507), (x, 490), (x + 36, 490), (x + 36, 507)], -0.0105, 0.0105, PEACH, bev=0.0011))
    P.merge(bm, prism([(266, 507), (276, 470), (302, 455), (333, 461), (378, 507)], -0.004, 0.004, PEACH2, bev=0.0012, smooth=1))
    P.merge(bm, prism([(1420, 490), (1430, 456), (1456, 445), (1506, 448), (1516, 490)], -0.011, 0.011, PEACH2, bev=0.0012, smooth=1))
    # 侧面长凹槽（亮一点的糖果片，像参考图里那条透亮的槽）
    P.merge(bm, both([(452, 648), (1052, 648), (1072, 668), (1072, 756), (1052, 776), (452, 776), (434, 756), (434, 668)], WS - 0.001, WS + 0.0022, LIGHT, bev=0.0016, smooth=2, seg=4))
    # 凹槽里的糖果贴花：两颗小星星夹一颗粉色爱心（两侧）
    for sd in (-1, 1):
        P.merge(bm, decal((752, 712), heart_pts(0.0082), WS + 0.0022, sd, 0.0014, 0.0010, PINK))
        for cx in (580, 924):
            P.merge(bm, decal((cx, 712), star_pts(0.0068, 0.0036), WS + 0.0022, sd, 0.0012, 0.0008, GOLD))
    # 前段上方的分模线（细凸棱）
    P.merge(bm, both([(232, 628), (440, 628), (440, 640), (232, 640)], WS - 0.0008, WS + 0.0006, PEACH2, bev=0.0003))
    # 防滑纹：后部六道长棱 + 下排五道短棱
    for k in range(6):
        x = 1105 + k * 47
        P.merge(bm, both([(x, 592), (x + 20, 588), (x + 22, 778), (x + 2, 780)], WS - 0.001, WS + 0.0013, PEACH2, bev=0.0006))
    for k in range(5):
        x = 1378 + k * 45
        P.merge(bm, both([(x, 692), (x + 18, 690), (x + 20, 778), (x + 2, 780)], WS - 0.001, WS + 0.0011, PEACH2, bev=0.0005))
    # 前端防滑纹（套筒前部下半，五道细棱）
    for k in range(5):
        x = 262 + k * 34
        P.merge(bm, both([(x, 668), (x + 14, 666), (x + 15, 780), (x + 1, 782)], WS - 0.001, WS + 0.0011, PEACH2, bev=0.0005))
    # 照门两颗白点（朝玩家的一面）、准星一颗粉点：瞄准时一眼看到“三点一线”
    for sx in (-0.0062, 0.0062):
        P.merge(bm, cyl_zx(sx, (1516, 468), 9, 1512, 1522, CREAM, seg=14))
    P.merge(bm, cyl_zx(0.0, (300, 474), 10, 296, 308, PINK, seg=14))
    # 抛壳窗里露出半颗金色弹壳（右侧）
    P.merge(bm, cyl_zx(WS - 0.004, (1205, 552), 22, 1135, 1265, GOLD, seg=18, cap=0.3))
    # 保险杆 + 圆钮（两侧）
    P.merge(bm, both([(1345, 568), (1452, 560), (1500, 580), (1498, 640), (1440, 652), (1352, 608)], WS - 0.001, WS + 0.0024, PEACH2, bev=0.0009, smooth=2))
    P.merge(bm, cyl_x((1490, 606), 34, WS + 0.0016, WS + 0.0052, PEACH, seg=22, bev=0.4))
    P.merge(bm, cyl_x((1490, 606), 34, -WS - 0.0052, -WS - 0.0016, PEACH, seg=22, bev=0.4))
    # 抛壳窗（右侧，奶油色）
    P.merge(bm, prism([(1108, 516), (1300, 516), (1312, 530), (1312, 588), (1108, 588)], WS - 0.001, WS + 0.0008, CREAM, bev=0.0006))
    # 枪口：伸出套筒前面的枪管头 + 深色枪口
    P.merge(bm, cyl_z((0, 612), 66, 190, 224, PEACH, seg=32))          # 枪管头：粗的一段
    P.merge(bm, cyl_z((0, 612), 72, 150, 190, (1.0, 0.52, 0.6), seg=36))   # 玩具枪标志性的“安全帽”：粉色枪口套
    P.merge(bm, cyl_z((0, 612), 78, 176, 186, (1.0, 0.7, 0.76), seg=36))   # 安全帽后沿一圈浅色卷边
    P.merge(bm, cyl_z((0, 612), 34, 144, 152, (0.6, 0.3, 0.35), seg=24))   # 深色枪口
    return bm


# ───────────────────────── 弹匣 / 击锤 / 扳机 / 弹壳 ─────────────────────────
def mag():
    bm = prism([(1255, 862), (1590, 862), (1610, 1478), (1265, 1478)], -0.0105, 0.0105, CREAM, bev=0.0012)
    for k, c in enumerate((PINK, GOLD)):                     # 顶上露出的糖果子弹头（透过枪身看得见）
        z, y = zy((1330 + k * 120, 862))
        b = P.lathe([(0, 0), (0.0052, 0), (0.0052, 0.007), (0.0032, 0.0115), (0, 0.0125)], 14)
        bmesh.ops.rotate(b, verts=b.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))   # 车削轴转成朝枪口（+Z）
        for v in b.verts: v.co += U(0, y - 0.006, z - 0.008)
        P.merge(bm, color_bm(b, c))
    P.merge(bm, prism([(1240, 1478), (1600, 1478), (1613, 1496), (1602, 1546), (1252, 1546), (1236, 1528)], -0.017, 0.017, MINT2, bev=0.0022, smooth=1))
    return rebase(bm, 'Gun_Mag', (1430, 862))


def hammer():
    bm = prism([(1612, 735), (1622, 650), (1648, 576), (1672, 561), (1700, 576), (1703, 612), (1681, 652), (1662, 735)], -0.0058, 0.0058, PEACH2, bev=0.0016, smooth=2)
    for k in range(3):                                      # 击锤顶上三道防滑棱
        y = 572 + k * 16
        P.merge(bm, prism([(1676 + k * 6, y), (1706 - k * 2, y + 4), (1704 - k * 2, y + 11), (1674 + k * 6, y + 7)], -0.0062, 0.0062, PEACH, bev=0.0005))
    return rebase(bm, 'Gun_Hammer', (1636, 712))


def trigger():
    bm = prism([(1040, 872), (1082, 876), (1078, 925), (1062, 975), (1060, 1020), (1046, 1062), (1018, 1058), (1012, 1010), (1022, 960), (1035, 915)],
               -0.0038, 0.0038, PEACH2, bev=0.0014, smooth=2)
    for k in range(4):                                      # 扳机正面横纹
        y = 930 + k * 26
        P.merge(bm, prism([(1012 + k * 2, y), (1030, y + 2), (1030, y + 9), (1011 + k * 2, y + 8)], -0.0041, 0.0041, LIGHT, bev=0.0004))
    return rebase(bm, 'Gun_Trigger', (1060, 878))


def shell():
    bm = P.lathe([(0, -0.011), (0.0055, -0.011), (0.006, -0.009), (0.006, 0.006), (0.0045, 0.01), (0.002, 0.012), (0, 0.0125)], 14)
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(math.radians(90), 4, 'X'), verts=bm.verts)
    return color_bm(bm, GOLD)


def flat_normals(me, min_area=2e-5, smooth_deg=60):
    """自定义法线：共面的大平面（侧面、端面，三角化后是一堆细长三角形）整块用面法线，绝对平整；
    其余面（倒角、曲面）按面积加权、只和夹角 < smooth_deg 的邻面平滑。"""
    polys = me.polygons
    groups = {}
    for p in polys:
        n = p.normal; d = n.dot(p.center)
        key = (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(d / 0.0003))
        groups.setdefault(key, []).append(p.index)
    flat = [False] * len(polys)
    for idx in groups.values():
        if len(idx) > 2 and sum(polys[i].area for i in idx) > min_area:
            for i in idx: flat[i] = True
    vf = [[] for _ in me.vertices]
    for p in polys:
        if not flat[p.index]:
            for vi in p.vertices: vf[vi].append(p.index)
    cosl = math.cos(math.radians(smooth_deg))
    out = [None] * len(me.loops)
    for p in polys:
        n0 = p.normal
        for li in p.loop_indices:
            if flat[p.index]: out[li] = n0.copy(); continue
            acc = Vector()
            for fi in vf[me.loops[li].vertex_index]:
                q = polys[fi]
                if q.normal.dot(n0) > cosl: acc += q.normal * q.area
            out[li] = acc.normalized() if acc.length > 0 else n0.copy()
    for p in polys: p.use_smooth = True
    me.normals_split_custom_set(out)


def export(name, bm):
    """导出：三角化后按面积加权法线（大平面保持平整，倒角圆滑过渡，消除长三角形插值出来的斜条纹）"""
    ob = P.to_obj(bm, name, smooth=True)
    bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
    flat_normals(ob.data)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                             bake_space_transform=True, colors_type='SRGB')
    bpy.data.objects.remove(ob, do_unlink=True)


def main():
    P.reset()
    parts = {'Gun_Frame': frame, 'Gun_Slide': slide, 'Gun_Mag': mag, 'Gun_Hammer': hammer, 'Gun_Trigger': trigger, 'Gun_Shell': shell}
    for n, fn in parts.items():
        export(n, fn())
    z, y = zy((1205, 552)); PARTS['Gun_Shell'] = (WS + 0.006, round(y, 5), round(z, 5))       # 抛壳窗外侧
    z, y = zy((140, 612)); PARTS['Muzzle'] = (0.0, round(y, 5), round(z, 5))
    json.dump({k: list(v) for k, v in PARTS.items()}, open(os.path.join(OUT, 'Gun_parts.json'), 'w'), indent=1)
    print('GUN', json.dumps(P.report, ensure_ascii=False))
    print('PARTS', json.dumps(PARTS))


if __name__ == '__main__':
    main()
