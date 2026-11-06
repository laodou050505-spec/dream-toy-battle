"""玩具手盾（Blender 里运行）：Blender -b -P Tools/blender_shield.py -- Assets/_Bang/Art/Models/Proc/Play
按 nano banana 参考图 ArtSource/Play2/shield_ref.png：
  Shield_Disc    微鼓的圆盘，像一大颗半透明糖果棒棒糖：薄荷 / 天蓝两色螺旋（顶点色按螺旋角分带，过渡柔和）
  Shield_Rim     奶油色圆润护边（圆环）+ 10 颗小圆钉
  Shield_Star    正中凸起的黄油色五角星（圆角、有厚度）+ 星心粉色小爱心
  Shield_Handle  背面桃色拱形握带 + 两个固定座
Unity 坐标：盾面朝 +Z（朝敌人），握带在 -Z（朝玩家），原点在盘心背面；直径 0.29 m。全部封闭流形，顶点色 sRGB。
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_portals as BP

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
MINT, BLUE, CREAM, YEL, PINK, PEACH = (0.62, 0.93, 0.83), (0.64, 0.82, 1.0), (1.0, 0.95, 0.86), (1.0, 0.86, 0.42), (1.0, 0.56, 0.7), (1.0, 0.63, 0.5)
R = 0.13


def to_unity(bm):
    """部件按“Unity 空间”建（z = 盾面法线），最后统一换到 Blender 坐标"""
    for v in bm.verts: v.co = U(v.co.x, v.co.y, v.co.z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def mix(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def disc():
    # 前面微鼓（中心高 3 cm），背面平；前面多分几圈，螺旋色带过渡才顺
    prof = [(0, -0.012), (R - 0.006, -0.012), (R, -0.007), (R + 0.002, 0.0), (R - 0.002, 0.007)]
    for i in range(1, 15):
        t = i / 15; r = (R - 0.006) * (1 - t)
        prof.append((r, 0.009 + 0.021 * math.sin(t * math.pi / 2)))
    prof.append((0, 0.031))
    bm = P.lathe(prof, 72)
    lay = bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops:
            x, y, z = l.vert.co
            r = math.hypot(x, y); a = math.atan2(y, x)
            s = 0.5 + 0.5 * math.sin(2 * a + r * 55.0)                 # 两臂螺旋
            s = s * s * (3 - 2 * s)
            c = mix(MINT, BLUE, s)
            c = mix(c, (1, 1, 1), 0.25 * max(0, 1 - r / 0.05))         # 中心偏亮（糖心）
            if z < -0.005: c = mix(c, CREAM, 0.3)                       # 背面略浅
            l[lay] = (c[0], c[1], c[2], 1.0)
    return to_unity(bm)


def rim():
    bm = P.torus(R + 0.006, 0.0135, seg=72, sides=14)
    BP.color_bm(bm, CREAM)
    for i in range(10):                                                 # 小圆钉：护边正面一圈
        a = i / 10 * math.tau + math.pi / 10
        st = P.lathe([(0, -0.006), (0.0045, -0.0045), (0.006, 0), (0.0045, 0.0045), (0, 0.006)], 14)
        for v in st.verts: v.co = Vector((v.co.x + math.cos(a) * (R + 0.006), v.co.y + math.sin(a) * (R + 0.006), v.co.z + 0.011))
        P.merge(bm, BP.color_bm(st, CREAM))
    return to_unity(bm)


def star_outline(ro, ri, n=5, round_k=7):
    """圆角五角星（逆时针）：每个尖角 / 凹角用几个点倒圆"""
    pts = []
    for i in range(n * 2):
        a = math.pi / 2 + i * math.pi / n
        r = ro if i % 2 == 0 else ri
        pts.append((math.cos(a) * r, math.sin(a) * r))
    out = []
    m = len(pts)
    for i in range(m):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % m]
        k = 0.4 if i % 2 == 0 else 0.22
        for j in range(round_k):
            t = j / (round_k - 1)
            a = (p1[0] + (p0[0] - p1[0]) * k, p1[1] + (p0[1] - p1[1]) * k)
            b = (p1[0] + (p2[0] - p1[0]) * k, p1[1] + (p2[1] - p1[1]) * k)
            q = ((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * p1[0] + t * t * b[0], (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * p1[1] + t * t * b[1])
            out.append(q)
    return out


def puffy(outline, thick, z0, puff):
    """平面轮廓挤成鼓鼓的软糖块：底面平，顶面向中心鼓起（分 3 圈）"""
    bm = bmesh.new(); n = len(outline)
    cx = sum(p[0] for p in outline) / n; cy = sum(p[1] for p in outline) / n
    bot = [bm.verts.new((x, y, z0)) for x, y in outline]
    rings = [bot]
    for k, (s, h) in enumerate([(1.0, 0.55), (0.9, 0.85), (0.62, 1.0), (0.3, 1.08)]):
        rings.append([bm.verts.new((cx + (x - cx) * s, cy + (y - cy) * s, z0 + thick * h + (puff if k else 0) * (1 - s))) for x, y in outline])
    top = bm.verts.new((cx, cy, z0 + thick * 1.1 + puff))
    cb = bm.verts.new((cx, cy, z0))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[j], bot[i], cb))
        for a, b in zip(rings[:-1], rings[1:]): bm.faces.new((a[i], a[j], b[j], b[i]))
        bm.faces.new((rings[-1][i], rings[-1][j], top))
    return bm


def star():
    bm = BP.color_bm(puffy(star_outline(0.052, 0.028), 0.012, 0.026, 0.009), YEL)
    # 星心粉色爱心
    hp = []
    for i in range(28):
        t = i / 28 * math.tau
        x = 16 * math.sin(t) ** 3; y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        hp.append((x * 0.00085, y * 0.00085 + 0.001))
    P.merge(bm, BP.color_bm(puffy(hp, 0.004, 0.044, 0.002), PINK))
    return to_unity(bm)


def handle():
    # 拱形握带：左右两端固定在盘背，中间离开 4 cm 让手穿过
    pts = []
    for i in range(13):
        t = i / 12
        pts.append(Vector((-0.055 + 0.11 * t, 0.0, -0.014 - 0.036 * math.sin(t * math.pi))))
    bm = BP.color_bm(P.tube(pts, 0.0085, seg=12), PEACH)
    for sx in (-1, 1):
        b = P.box(sx * 0.058, 0, -0.016, 0.028, 0.032, 0.012, bevel=0.003)
        P.merge(bm, BP.color_bm(b, PEACH))
    return to_unity(bm)


def main():
    P.reset()
    for n, fn in (('Shield_Disc', disc), ('Shield_Rim', rim), ('Shield_Star', star), ('Shield_Handle', handle)):
        BP.export(n, fn(), 40)
    print('SHIELD', P.report)


if __name__ == '__main__':
    main()
