"""P2 重建失败的几件场景摆件，改成 Blender 程序模型（用户：场景里有很丑的星星形模型，不要让玩家一眼看出是低质量游戏）。
  Blender -b -P Tools/blender_props_fix.py -- <输出目录> [名…]
  W6_GummyBear   软糖熊：元球（头、耳、口鼻、身、手、脚）融成一体，粉桃色，顶点色由深到浅（透光感）
  W4_HeartCloud  彩虹棉花云：扁平元球簇，底边较平，顶部一排圆鼓包，横向蓝→粉→黄渐变
  W4_PinataStar  星形皮纳塔：圆角五角星厚片 + 前后面和侧边一排排彩色流苏片 + 底部卷曲彩带 + 顶部吊环
  W4_BalloonBunch 气球束：9 只水滴形气球（带扎口）+ 各自一根真分开的细绳汇到底部的小礼盒压重
坐标：Blender（Z 向上）；正面朝 Blender +X（= Unity −X，和 P2 摆件一致，场景摆放角度不用改）。
每件导出 <名>_lod0.fbx 与 <名>_lod1.fbx（约 40% 面数），顶点色 sRGB。所有部件都是封闭流形。"""
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

argv = sys.argv[sys.argv.index('--') + 1:]
OUT = argv[0]; ONLY = argv[1:]
os.makedirs(OUT, exist_ok=True)


def vcol(bm, fn):
    """fn(co) -> rgb：逐顶点上色"""
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops:
            c = fn(l.vert.co); l[lay] = (c[0], c[1], c[2], 1.0)
    return bm


def solid(bm, rgb):
    return vcol(bm, lambda co: rgb)


def ellipsoid(c, r, sc=(1, 1, 1), seg=32, rings=20):
    prof = [(0, -r)] + [(r * math.sin(math.pi * k / rings), -r * math.cos(math.pi * k / rings)) for k in range(1, rings)] + [(0, r)]
    bm = P.lathe(prof, seg)
    for v in bm.verts: v.co = Vector((v.co.x * sc[0], v.co.y * sc[1], v.co.z * sc[2])) + Vector(c)
    return bm


def union_mesh(elems, voxel=0.005, smooth=6):
    """elems: [(中心, 半径, (sx,sy,sz))] 椭球 → 合成后体素重拓扑（并集、封闭）→ 平滑出小圆角。
    比元球好：各部件轮廓分明（元球的场互相吸，整体会糊成一团）。"""
    bm = bmesh.new()
    for c, r, sc in elems: P.merge(bm, ellipsoid(c, r, sc))
    me = bpy.data.meshes.new('u'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('u', me); bpy.context.scene.collection.objects.link(ob)
    md = ob.modifiers.new('r', 'REMESH'); md.mode = 'VOXEL'; md.voxel_size = voxel; md.adaptivity = 0.0
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier='r')
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bpy.data.objects.remove(ob, do_unlink=True)
    for _ in range(smooth): bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    return bm


def mix(a, b, t):
    t = max(0.0, min(1.0, t)); return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


# ───────── 软糖熊 ─────────
def gummy_bear():
    E = []
    E.append(((0, 0, 0.315), 0.1, (0.92, 1.0, 0.9)))                    # 头
    for sd in (-1, 1): E.append(((-0.005, sd * 0.072, 0.4), 0.036, (0.75, 1, 1)))   # 耳
    E.append(((0.072, 0, 0.29), 0.045, (0.85, 1.05, 0.78)))             # 口鼻
    E.append(((0, 0, 0.155), 0.1, (0.92, 1.12, 1.3)))                   # 身
    for sd in (-1, 1):
        E.append(((0.04, sd * 0.11, 0.205), 0.042, (1.0, 0.85, 1.35)))  # 手（短胖，略前伸）
        E.append(((0.035, sd * 0.068, 0.045), 0.055, (1.2, 0.95, 0.82)))   # 脚（脚尖朝前）
    bm = union_mesh(E, voxel=0.0045, smooth=8)
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    deep, light = (1.0, 0.46, 0.5), (1.0, 0.8, 0.72)
    def c(co):
        rim = max(0.0, min(1.0, (co.x + 0.12) / 0.24))                 # 正面亮一点（透光）
        return mix(deep, light, 0.35 + 0.5 * rim * (0.6 + 0.4 * (co.z / 0.42)))
    vcol(bm, c)
    # 眼睛 + 小鼻头（深莓色小扁珠，半嵌进脸）
    for s in (-1, 1):
        e = ellipsoid((0, 0, 0), 0.0085, (1, 1, 0.55), seg=16, rings=8)                 # 扁珠，半嵌进脸（头前表面在 x≈0.092）
        bmesh.ops.transform(e, matrix=Matrix.Translation((0.083, s * 0.038, 0.338)) @ Matrix.Rotation(math.pi / 2 - s * 0.35, 4, 'Y') @ Matrix.Rotation(s * 0.4, 4, 'X'), verts=e.verts)
        P.merge(bm, solid(e, (0.42, 0.14, 0.24)))
    n = ellipsoid((0, 0, 0), 0.012, (1.2, 1, 0.6), seg=16, rings=8)                    # 鼻头：口鼻前端（x≈0.11）
    bmesh.ops.transform(n, matrix=Matrix.Translation((0.103, 0, 0.3)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), verts=n.verts)
    P.merge(bm, solid(n, (0.5, 0.18, 0.28)))
    return bm


# ───────── 彩虹棉花云 ─────────
def heart_cloud():
    E = []
    # 底部一条扁圆（平底）+ 顶部 5 个圆鼓包（中间最高）+ 两头小圆，前后厚度只有宽度的 1/4
    E.append(((0, 0, 0.1), 0.1, (0.7, 3.4, 1.0)))
    for y, z, r in [(-0.27, 0.13, 0.1), (-0.13, 0.2, 0.13), (0.04, 0.23, 0.15), (0.2, 0.18, 0.12), (0.32, 0.12, 0.09)]:
        E.append(((0, y, z), r, (0.62, 1, 1)))
    for y in (-0.37, 0.4): E.append(((0, y, 0.085), 0.075, (0.65, 1, 0.95)))
    bm = union_mesh(E, voxel=0.006, smooth=6)
    # 平底：底下 2 cm 压平
    zs = [v.co.z for v in bm.verts]; zb = min(zs) + 0.03
    for v in bm.verts:
        if v.co.z < zb: v.co.z = zb - (zb - v.co.z) * 0.25
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    ys = [v.co.y for v in bm.verts]; y0, y1 = min(ys), max(ys)
    blue, pink, yel, mint = (0.66, 0.84, 1.0), (1.0, 0.76, 0.86), (1.0, 0.93, 0.66), (0.72, 0.95, 0.84)
    def c(co):
        t = (co.y - y0) / (y1 - y0)
        base = mix(blue, pink, t / 0.5) if t < 0.5 else mix(pink, yel, (t - 0.5) / 0.5)
        lo = max(0.0, 0.25 - co.z) / 0.25                                # 底部带一点薄荷
        return mix(base, mint, lo * 0.45)
    vcol(bm, c)
    return bm


# ───────── 星形皮纳塔 ─────────
STRIPE = [(1.0, 0.72, 0.84), (0.66, 0.92, 0.82), (1.0, 0.92, 0.6), (0.8, 0.72, 1.0), (0.66, 0.84, 1.0), (1.0, 0.8, 0.66)]


def star_outline(R, r, n=5, rot=math.pi / 2, round_k=4):
    """圆角五角星轮廓（YZ 平面坐标列表）：尖角处用小圆弧倒圆"""
    pts = []
    for i in range(n * 2):
        a = rot + i * math.pi / n; rr = R if i % 2 == 0 else r
        pts.append(Vector((math.cos(a) * rr, math.sin(a) * rr)))
    out = []
    m = len(pts)
    for i in range(m):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % m]
        rad = R * 0.14 if i % 2 == 0 else r * 0.13                      # 圆角按星星大小缩放（小吊牌星不会被倒成圆疙瘩）
        d0 = (p0 - p1).normalized(); d2 = (p2 - p1).normalized()
        a0 = p1 + d0 * rad * 1.4; a2 = p1 + d2 * rad * 1.4
        for k in range(round_k + 1):
            t = k / round_k
            q = (1 - t) ** 2 * a0 + 2 * (1 - t) * t * p1 + t ** 2 * a2      # 二次贝塞尔倒圆
            out.append(q)
    return out


def inside(poly, y, z):
    c = False; n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[i - 1]
        if (a.y > z) != (b.y > z) and y < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x: c = not c
    return c


def flap(cx, cy, cz, w, h, t, nx, tilt, rgb):
    """一片流苏：薄片（Blender 坐标：x 厚 t、y 宽 w、z 高 h），上沿在 cz、往下垂；下沿朝 nx（±1，X 方向）外翘 tilt 弧度"""
    bm = P.box(0, 0, -h / 2, t, w, h)
    m = Matrix.Translation((cx, cy, cz)) @ Matrix.Rotation(-nx * tilt, 4, 'Y')
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return solid(bm, rgb)


def pinata_star():
    rnd = random.Random(11)
    R, r, T = 0.2, 0.095, 0.11
    poly = star_outline(R, r)                                                   # (y, z) 平面
    body = bmesh.new()                                                          # 平面星柱：前后两面平（流苏贴得上），侧壁竖直
    fr = [body.verts.new((T / 2, p.x, p.y)) for p in poly]; bk = [body.verts.new((-T / 2, p.x, p.y)) for p in poly]
    cf = body.verts.new((T / 2, 0, 0)); cb = body.verts.new((-T / 2, 0, 0))
    n = len(poly)
    for i in range(n):
        j = (i + 1) % n
        body.faces.new((fr[i], fr[j], cf)); body.faces.new((bk[j], bk[i], cb)); body.faces.new((fr[j], fr[i], bk[i], bk[j]))
    bmesh.ops.recalc_face_normals(body, faces=body.faces)
    bm = solid(body, (0.98, 0.88, 0.92))
    # 前后两面：一排排横向流苏（每排一种颜色，从下往上叠，上排盖住下排的上沿）
    rows = 12; zlo = min(p.y for p in poly); zhi = max(p.y for p in poly)
    for side in (1, -1):
        for k in range(rows):
            z = zlo + (k + 1) * (zhi - zlo) / (rows + 0.6)
            col = STRIPE[k % len(STRIPE)]
            y = -R
            while y < R:
                y += 0.021
                if not (inside(poly, y, z) and inside(poly, y, z - 0.03)): continue
                P.merge(bm, flap(side * (T / 2 + 0.0018), y, z, 0.022, 0.034, 0.0026, side, rnd.uniform(0.05, 0.22), tuple(min(1, c + rnd.uniform(-0.03, 0.03)) for c in col)))
    # 侧边一圈：沿轮廓竖着的流苏（两排，前后错开）
    per = sum((poly[i] - poly[i - 1]).length for i in range(len(poly)))
    nflap = int(per / 0.02)
    def at(s):
        acc = 0
        for i in range(len(poly)):
            a, b = poly[i - 1], poly[i]; L = (b - a).length
            if acc + L >= s: t = (s - acc) / L; return a.lerp(b, t), (b - a).normalized()
            acc += L
        return poly[0], (poly[1] - poly[0]).normalized()
    for j in range(nflap):
        p, d = at(j / nflap * per)
        nrm = Vector((d.y, -d.x))                                               # 轮廓外法向（逆时针轮廓）
        if inside(poly, p.x + nrm.x * 0.01, p.y + nrm.y * 0.01): nrm = -nrm
        for xi, xoff in enumerate((-T * 0.25, T * 0.25)):
            col = STRIPE[(j + xi * 3) % len(STRIPE)]
            # 侧边流苏：贴着侧壁的薄片（宽沿 X、长沿轮廓切向、薄沿外法向），外翘一点
            f = P.box(0, 0, 0, 0.026, 0.0026, 0.02)                              # x 宽（沿厚度方向）、y 薄、z 长
            ang = math.atan2(nrm.y, nrm.x)                                       # 外法向在 YZ 平面的角度
            m = Matrix.Translation((xoff, p.x + nrm.x * 0.0035, p.y + nrm.y * 0.0035)) @ Matrix.Rotation(ang, 4, 'X')
            bmesh.ops.transform(f, matrix=m, verts=f.verts)
            P.merge(bm, solid(f, col))
    # 底部两个下尖角各垂 2 条卷曲彩带
    tips = sorted([Vector((math.cos(math.pi / 2 + i * 2 * math.pi / 5) * R, math.sin(math.pi / 2 + i * 2 * math.pi / 5) * R)) for i in range(5)], key=lambda q: q.y)[:2]
    for ti, tp in enumerate(tips):
        for s in (-1, 1):
            pts = []; L = 0.17 + 0.03 * s
            for k in range(26):
                t = k / 25
                a = t * 5.5 * math.pi + s
                pts.append((s * 0.012 + math.cos(a) * 0.012, tp.x * 0.88 + math.sin(a) * 0.012, tp.y * 0.86 - t * L))
            P.merge(bm, solid(P.tube(pts, 0.0032, seg=6), STRIPE[(ti * 2 + (s > 0)) % len(STRIPE)]))
    # 顶部吊环（细绳圈）
    loop = P.torus(0.024, 0.0028, seg=24, sides=6)
    bmesh.ops.transform(loop, matrix=Matrix.Translation((0, 0, zhi + 0.018)) @ Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Rotation(math.pi / 2, 4, 'Z'), verts=loop.verts)
    P.merge(bm, solid(loop, (0.82, 0.7, 0.98)))
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    return bm


# ───────── 气球束 ─────────
BAL = [(1.0, 0.7, 0.8), (0.68, 0.84, 1.0), (0.7, 0.94, 0.82), (1.0, 0.92, 0.6), (0.82, 0.72, 1.0), (1.0, 0.78, 0.64)]


def balloon(c, rgb, s=1.0, tilt=(0, 0)):
    """水滴形气球（上圆下收）+ 扎口小结；返回 (bm, 扎口下端点)"""
    prof = []
    for k in range(17):
        t = k / 16; a = math.pi * t
        rr = math.sin(a) * (1 - 0.32 * (1 - t) ** 2.2)
        z = -math.cos(a) * 1.18 - 0.12 * (1 - t) ** 3
        prof.append((max(0.0, rr) * 0.1 * s, z * 0.1 * s))
    prof[0] = (0, prof[0][1]); prof[-1] = (0, prof[-1][1])
    bm = solid(P.lathe(prof, 24), rgb)
    k = P.lathe([(0, -0.016 * s), (0.008 * s, -0.012 * s), (0.011 * s, -0.004 * s), (0.006 * s, 0.002 * s), (0, 0.004 * s)], 12)
    zt = prof[0][1]
    bmesh.ops.transform(k, matrix=Matrix.Translation((0, 0, zt - 0.0005)), verts=k.verts)
    P.merge(bm, solid(k, tuple(c2 * 0.86 for c2 in rgb)))
    m = Matrix.Translation(c) @ Matrix.Rotation(tilt[0], 4, 'X') @ Matrix.Rotation(tilt[1], 4, 'Y')
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm, m @ Vector((0, 0, zt - 0.016 * s))


def balloon_bunch():
    rnd = random.Random(23)
    bm = bmesh.new()
    hub = Vector((0, 0, 0.11))                                                  # 绳子汇合点（礼盒上方）
    spots = [(0, 0, 0.92), (0.04, -0.13, 0.86), (-0.03, 0.13, 0.87), (0.1, 0.07, 0.8), (-0.1, -0.07, 0.8),
             (0.0, -0.21, 0.72), (0.02, 0.21, 0.73), (0.11, -0.03, 0.68), (-0.12, 0.04, 0.7)]
    for i, (x, y, z) in enumerate(spots):
        c = Vector((x, y, z)) + Vector((rnd.uniform(-0.01, 0.01), rnd.uniform(-0.01, 0.01), rnd.uniform(-0.01, 0.01)))
        d = (c - hub).normalized()
        tilt = (-math.atan2(d.y, d.z) * 0.6, math.atan2(d.x, d.z) * 0.6)
        b, knot = balloon(c, BAL[i % len(BAL)], s=rnd.uniform(0.92, 1.05), tilt=tilt)
        P.merge(bm, b)
        # 绳子：扎口 → 汇合点，微微下垂（每根独立，不缠）
        pts = []
        for k in range(12):
            t = k / 11
            p = knot.lerp(hub, t)
            p.y += math.sin(t * math.pi) * 0.006 * (1 if i % 2 else -1)
            pts.append(tuple(p))
        P.merge(bm, solid(P.tube(pts, 0.0022, seg=6), (0.94, 0.9, 0.98)))
    # 汇合点到礼盒的一小段 + 礼盒压重（带十字丝带和小蝴蝶结）
    P.merge(bm, solid(P.tube([(0, 0, 0.115), (0, 0, 0.085)], 0.0032, seg=6), (0.94, 0.9, 0.98)))
    box = P.box(0, 0, 0.04, 0.08, 0.08, 0.08, bevel=0.008); P.merge(bm, solid(box, (0.82, 0.74, 1.0)))
    for rot in (0, 1):
        rb = P.box(0, 0, 0.04, 0.084 if rot == 0 else 0.018, 0.018 if rot == 0 else 0.084, 0.084, bevel=0.003); P.merge(bm, solid(rb, (1.0, 0.72, 0.82)))
    for s in (-1, 1):
        lp = P.torus(0.014, 0.0045, seg=16, sides=6)
        bmesh.ops.transform(lp, matrix=Matrix.Translation((0, s * 0.016, 0.088)) @ Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Scale(1.0, 4), verts=lp.verts)
        P.merge(bm, solid(lp, (1.0, 0.72, 0.82)))
    return bm


# ───────── 毛毡星星（挂饰）─────────
def star_pillow(R, r, H, M=240, N=14):
    """枕头形五角星主体（YZ 平面、前后沿 ±X 鼓起，中心在原点）：返回 (bmesh, 星形轮廓, 每个角度的轮廓半径, 厚度函数)"""
    poly = star_outline(R, r, round_k=8)
    def rho(th):
        d = Vector((math.cos(th), math.sin(th))); best = None
        for i in range(len(poly)):
            a, b = poly[i - 1], poly[i]; e = b - a
            den = d.x * e.y - d.y * e.x
            if abs(den) < 1e-9: continue
            t = (a.x * e.y - a.y * e.x) / den; u = (a.x * d.y - a.y * d.x) / den
            if t > 0 and -1e-6 <= u <= 1 + 1e-6 and (best is None or t < best): best = t
        return best or R
    ths = [j / M * math.tau for j in range(M)]; rr = [rho(t) for t in ths]
    h = lambda u: H * max(0.0, 1 - u ** 4) ** 0.5
    bm = bmesh.new()
    def P3(side, u, j):
        th = ths[j]; q = rr[j] * u
        return (side * h(u), math.cos(th) * q, math.sin(th) * q)
    rim = [bm.verts.new(P3(1, 1.0, j)) for j in range(M)]
    for side in (1, -1):
        c = bm.verts.new((side * H, 0, 0)); rows = []
        for i in range(1, N):
            rows.append([bm.verts.new(P3(side, (i / N) ** 0.85, j)) for j in range(M)])
        rows.append(rim)
        for j in range(M):
            k = (j + 1) % M
            bm.faces.new((c, rows[0][j], rows[0][k]) if side > 0 else (c, rows[0][k], rows[0][j]))
        for a, b in zip(rows[:-1], rows[1:]):
            for j in range(M):
                k = (j + 1) % M
                bm.faces.new((a[j], b[j], b[k], a[k]) if side > 0 else (a[j], a[k], b[k], b[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, poly, (ths, rr), h


def felt_star():
    """鼓鼓的枕头形五角星挂饰：枕头星主体 + 前后两圈白缝线贴在鼓面上 + 顶上吊绳圈。解析曲面，表面光滑、封闭流形。"""
    R, r, H = 0.2, 0.094, 0.034; M = 240
    bm, poly, (ths, rr), h = star_pillow(R, r, H, M=M)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    felt, felt2 = (1.0, 0.84, 0.44), (1.0, 0.93, 0.62)
    vcol(bm, lambda co: mix(felt, felt2, 0.5 + co.z / 0.42))
    # 缝线：u=0.84 一圈短针脚，贴着鼓面（略浮 1.2 mm）
    us = 0.84; per = sum(rr[j] * us * math.tau / M for j in range(M))
    acc = [0.0]
    for j in range(1, M + 1): acc.append(acc[-1] + rr[j % M] * us * math.tau / M)
    def at(sv, side):
        sv %= per
        for j in range(M):
            if acc[j + 1] >= sv:
                t = (sv - acc[j]) / max(1e-9, acc[j + 1] - acc[j]); th = ths[j] + t * math.tau / M; q = (rr[j] * (1 - t) + rr[(j + 1) % M] * t) * us
                return (side * (h(us) + 0.0012), math.cos(th) * q, math.sin(th) * q)
        return (side * h(us), 0, 0)
    ns = int(per / 0.017)
    for side in (1, -1):
        for k in range(ns):
            s0 = k / ns * per
            pts = [at(s0 + f * 0.009, side) for f in (0, 0.5, 1)]
            P.merge(bm, solid(P.tube(pts, 0.0016, seg=5), (1.0, 0.98, 0.94)))
    zt = max(v.co.z for v in bm.verts)
    loop = P.torus(0.022, 0.0026, seg=24, sides=6)
    bmesh.ops.transform(loop, matrix=Matrix.Translation((0, 0, zt + 0.016)) @ Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Rotation(math.pi / 2, 4, 'Z'), verts=loop.verts)
    P.merge(bm, solid(loop, (0.82, 0.7, 0.98)))
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    return bm


# ───────── 礼盒（补给礼盒 + W4 场景礼物盒）─────────
def sweep(path, width, thick, side_dir=Vector((0, 1, 0)), closed=False):
    """矩形截面（宽 width 沿“侧向”、厚 thick 沿“法向”）沿折线扫成缎带；closed=True 首尾相接（蝴蝶结的圈），否则两端封口。封闭流形。"""
    P_ = [Vector(p) for p in path]; n = len(P_); bm = bmesh.new(); rings = []
    for i in range(n):
        if closed: t = (P_[(i + 1) % n] - P_[i - 1]).normalized()
        else: t = (P_[min(i + 1, n - 1)] - P_[max(i - 1, 0)]).normalized()
        side = (side_dir - t * side_dir.dot(t)).normalized()          # 宽度方向固定朝 side_dir（去掉沿切线的分量）：缎带不会在拐弯处翻面
        nrm = side.cross(t).normalized()
        c = P_[i]
        rings.append([bm.verts.new(c + side * sx * width / 2 + nrm * sy * thick / 2) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    segs = range(n) if closed else range(n - 1)
    for i in segs:
        a, b = rings[i], rings[(i + 1) % n]
        for k in range(4): bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    if not closed:
        bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def gift_parts(body, ribbon, A=0.11, Hb=0.17, Hl=0.05, tag=True):
    """返回 (盒身, 盒盖)：盒身原点在底面中心；盒盖原点在盖底（= 盒身顶 Hb），打开时整块飞起"""
    rd = tuple(c * 0.9 for c in ribbon)
    box = vcol(P.box(0, 0, Hb / 2, 2 * A, 2 * A, Hb, bevel=0.016), lambda co: mix(tuple(c * 0.86 for c in body), tuple(min(1, c * 1.05) for c in body), co.z / Hb))   # 上亮下暗
    for sx, sy in ((2 * A + 0.004, 0.042), (0.042, 2 * A + 0.004)):                      # 十字缎带绕盒身一圈（侧面竖条）
        P.merge(box, solid(P.box(0, 0, Hb / 2 - 0.002, sx, sy, Hb - 0.008, bevel=0.002), ribbon))
    if tag:                                                                                # 星星吊牌：挂在正面（Unity −X = Blender +X）缎带上
        st, _, _, _ = star_pillow(0.034, 0.016, 0.006, M=120, N=8)
        bmesh.ops.transform(st, matrix=Matrix.Translation((A + 0.012, 0.03, Hb * 0.5)) @ Matrix.Rotation(0.18, 4, 'X'), verts=st.verts)
        P.merge(box, solid(st, (1.0, 0.84, 0.4)))
        p0 = Vector((A + 0.004, 0.012, Hb - 0.012)); p2 = Vector((A + 0.012, 0.03, Hb * 0.5 + 0.03)); p1 = (p0 + p2) / 2 + Vector((0.006, 0.004, -0.004))
        P.merge(box, solid(P.tube([tuple((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2) for t in [k / 9 for k in range(10)]], 0.0014, seg=6), (0.96, 0.92, 0.86)))   # 挂绳：平滑下垂
    L = A + 0.008
    lid = solid(P.box(0, 0, Hl / 2, 2 * L, 2 * L, Hl, bevel=0.01), tuple(min(1, c * 1.04) for c in body))
    for sx, sy in ((2 * L + 0.004, 0.044), (0.044, 2 * L + 0.004)):
        P.merge(lid, solid(P.box(0, 0, Hl / 2, sx, sy, Hl + 0.003, bevel=0.002), ribbon))
    # 蝴蝶结：两个缎带圈（往两侧斜立、朝前倾）+ 中间结 + 两条飘带
    top = Hl + 0.0015
    for s in (-1, 1):                                                                       # 两个圈往左右（±Y）伸，缎带宽度沿前后（X），圈面朝前倾、往外斜
        pts = []
        for k in range(36):
            a = k / 36 * math.tau
            y = 0.058 + 0.056 * math.cos(a); z = 0.04 + 0.038 * math.sin(a)
            pts.append(Vector((0, s * y, max(z, 0.002))))
        m = Matrix.Translation((0, 0, top)) @ Matrix.Rotation(0.55, 4, 'Y') @ Matrix.Rotation(-s * 0.3, 4, 'X')
        pts = [m @ p for p in pts]
        P.merge(lid, solid(sweep(pts, 0.042, 0.006, side_dir=Vector((1, 0, 0)), closed=True), ribbon))
    knot = ellipsoid((0, 0, top + 0.016), 0.024, (1.0, 1.0, 0.85), seg=20, rings=10)
    P.merge(lid, solid(knot, rd))
    for s in (-1, 1):                                                                       # 飘带：从结往正面（+X）斜铺过盖顶，搭下盖沿
        pts = [Vector((0.005, s * 0.005, top + 0.004)), Vector((0.04, s * 0.03, top + 0.003)), Vector((0.075, s * 0.05, top + 0.002)), Vector((L + 0.004, s * 0.06, top - 0.004)), Vector((L + 0.008, s * 0.062, top - 0.03))]
        P.merge(lid, solid(sweep(pts, 0.03, 0.0045, side_dir=Vector((0, 1, 0))), ribbon))
    return box, lid


def gift_box():
    return gift_parts((1.0, 0.86, 0.5), (1.0, 0.66, 0.76))


def gift_whole(body, ribbon, sc):
    def f():
        box, lid = gift_parts(body, ribbon, tag=False)
        bmesh.ops.translate(lid, verts=lid.verts, vec=(0, 0, 0.17))
        P.merge(box, lid)
        for v in box.verts: v.co *= sc
        return box
    return f


# ───────── 发条小熊（封面主角 + 第 5 章）─────────
def union_parts(parts, voxel=0.004, smooth=6):
    """任意封闭网格（椭球、管子）合成后体素重拓扑（并集、封闭）→ 平滑"""
    bm = bmesh.new()
    for b in parts: P.merge(bm, b)
    me = bpy.data.meshes.new('u'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('u', me); bpy.context.scene.collection.objects.link(ob)
    md = ob.modifiers.new('r', 'REMESH'); md.mode = 'VOXEL'; md.voxel_size = voxel; md.adaptivity = 0.0
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier='r')
    bm = bmesh.new(); bm.from_mesh(ob.data); bpy.data.objects.remove(ob, do_unlink=True)
    for _ in range(smooth): bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    return bm


def windup_bear():
    """经典泰迪熊（软胶玩具质感）：大头带腮帮、短胖身子、胶囊形手脚各有形状（不是糊在身上的团块）、
    脚底和掌心的浅色肉垫、贴着肚子的扁肚皮、浅色口鼻、内耳、带高光的珠眼、薰衣草领结、背后金色发条钥匙。
    用户：上一版拼色袖子 / 短裤像蘑菇盖和团块，看着不舒服 → 去掉拼色块，回到经典泰迪的比例。正面 +X（= Unity −X）。"""
    BROWN, MUZ, PAD = (0.6, 0.41, 0.3), (0.93, 0.76, 0.6), (0.96, 0.8, 0.66)
    LAV, GOLD, EYE = (0.8, 0.7, 1.0), (1.0, 0.82, 0.4), (0.16, 0.1, 0.1)
    def tube(pts, r, taper=1.0):
        return P.tube(pts, r, seg=20, r_fn=lambda u: 1.0 + (taper - 1.0) * u)
    parts = [
        ellipsoid((0, 0, 0.305), 0.1, (0.95, 1.06, 0.92)),                         # 头（略宽）
        ellipsoid((0.03, 0.05, 0.27), 0.05, (0.9, 1, 0.9)), ellipsoid((0.03, -0.05, 0.27), 0.05, (0.9, 1, 0.9)),   # 腮帮
        ellipsoid((-0.01, 0.083, 0.385), 0.036, (0.62, 1, 1)), ellipsoid((-0.01, -0.083, 0.385), 0.036, (0.62, 1, 1)),   # 耳
        ellipsoid((0, 0, 0.15), 0.085, (0.95, 1.0, 1.12)),                         # 身子（短胖，梨形）
        ellipsoid((0, 0, 0.11), 0.088, (0.98, 1.05, 0.8)),                         # 肚腩
    ]
    for sd in (-1, 1):
        parts.append(tube([(0.0, sd * 0.07, 0.2), (0.03, sd * 0.106, 0.172), (0.058, sd * 0.11, 0.15)], 0.031, 0.95))   # 手臂：短胖，往前抱着
        parts.append(ellipsoid((0.062, sd * 0.11, 0.146), 0.033, (1, 1, 1)))                                                # 手掌（圆头）
        parts.append(tube([(0.005, sd * 0.05, 0.07), (0.02, sd * 0.056, 0.035)], 0.04, 1.0))                                 # 腿
        parts.append(ellipsoid((0.04, sd * 0.057, 0.026), 0.04, (1.35, 0.95, 0.68)))                                         # 脚（朝前的圆脚）
    bm = solid(union_parts(parts, voxel=0.0038, smooth=6), BROWN)
    def shell(c, r, sc, rgb, rot=None):
        b = ellipsoid((0, 0, 0), r, sc, seg=32, rings=16)
        m = Matrix.Translation(c) @ (rot or Matrix.Identity(4)); bmesh.ops.transform(b, matrix=m, verts=b.verts)
        P.merge(bm, solid(b, rgb))
    shell((0.079, 0, 0.27), 0.043, (0.72, 1.12, 0.78), MUZ)                               # 口鼻：略鼓出的浅色椭圆
    shell((0.064, 0, 0.13), 0.06, (0.36, 0.85, 1.0), MUZ)                                 # 肚皮：贴着肚子的扁椭圆，只微微凸一点
    for sd in (-1, 1):
        shell((0.004, sd * 0.084, 0.386), 0.024, (0.32, 1, 1), MUZ)                       # 内耳
        shell((0.081, sd * 0.057, 0.022), 0.026, (0.35, 0.9, 0.62), PAD, Matrix.Rotation(-0.25, 4, 'Y'))   # 脚底肉垫（朝前）
        shell((0.088, sd * 0.11, 0.14), 0.019, (0.4, 0.85, 0.85), PAD)                                      # 掌心（朝前）
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    def put(b, m, rgb):
        bmesh.ops.transform(b, matrix=m, verts=b.verts); bmesh.ops.translate(b, verts=b.verts, vec=(0, 0, -zmin)); P.merge(bm, solid(b, rgb))
    put(ellipsoid((0, 0, 0), 0.017, (0.8, 1.25, 0.82), seg=20, rings=10), Matrix.Translation((0.112, 0, 0.286)), (0.3, 0.18, 0.16))   # 鼻头
    for sd in (-1, 1):
        put(ellipsoid((0, 0, 0), 0.012, (0.55, 0.9, 1.05), seg=18, rings=9), Matrix.Translation((0.088, sd * 0.042, 0.322)) @ Matrix.Rotation(-sd * 0.42, 4, 'Z'), EYE)   # 眼：稍稍鼓出脸面，正面看得清
        put(ellipsoid((0, 0, 0), 0.0035, (1, 1, 1), seg=10, rings=5), Matrix.Translation((0.094, sd * 0.039, 0.328)), (1, 1, 1))                                   # 眼睛高光
        put(ellipsoid((0, 0, 0), 0.028, (0.5, 1.0, 0.72), seg=18, rings=9), Matrix.Translation((0.068, sd * 0.032, 0.212)) @ Matrix.Rotation(sd * 0.25, 4, 'X'), LAV)   # 领结两翼
    put(ellipsoid((0, 0, 0), 0.014, (0.8, 1, 1), seg=14, rings=7), Matrix.Translation((0.081, 0, 0.212)), tuple(c * 0.88 for c in LAV))
    put(P.tube([(-0.075, 0, 0.15), (-0.12, 0, 0.15)], 0.0065, seg=10), Matrix.Identity(4), GOLD)   # 发条钥匙
    for sd in (-1, 1):
        put(P.torus(0.022, 0.007, seg=20, sides=8), Matrix.Translation((-0.13, sd * 0.021, 0.15)) @ Matrix.Rotation(math.pi / 2, 4, 'X'), GOLD)
    return bm


# ───────── 气球小怪（热气球 + 吊篮）─────────
def balloon_enemy():
    """热气球小怪：粉色球囊（顶部金色气门帽）+ 球囊上的立体五官（珠眼带高光、眉毛、微笑、腮红、圆鼻头）+ 四根吊绳 + 编织吊篮。
    用户：P2 版的眼睛那里贴图糊 → 五官全部做成几何、顶点色，近看也是清清楚楚的边。总高约 0.6 m，正面 +X（= Unity −X）。"""
    PINK, PINK_L, CORAL, ROPE, WICK, WICK_D, GOLD = (0.95, 0.5, 0.64), (1.0, 0.7, 0.8), (1.0, 0.56, 0.5), (0.98, 0.86, 0.62), (0.72, 0.48, 0.3), (0.55, 0.34, 0.2), (1.0, 0.8, 0.36)
    EYE, BROW = (0.13, 0.08, 0.1), (0.42, 0.2, 0.2)
    ZC, R = 0.385, 0.2                                                          # 球囊主体是一个球（脸都在球面上，法线好算）
    prof = [(0, ZC + R)]
    for k in range(1, 23):
        ph = k / 22 * math.radians(128); prof.append((R * math.sin(ph), ZC + R * math.cos(ph)))
    r0, z0 = prof[-1]
    for k in range(1, 9):                                                       # 收口：从球面平滑收到细颈
        t = k / 8; e = t * t * (3 - 2 * t)
        prof.append((r0 + (0.034 - r0) * e, z0 + (0.17 - z0) * t - 0.012 * math.sin(t * math.pi)))
    prof.append((0, 0.17))
    env = P.lathe(prof, 48)
    vcol(env, lambda co: mix(PINK, PINK_L, max(0.0, min(1.0, (co.z - 0.25) / 0.33)) ** 1.6 * 0.55))
    bm = env
    def add(b, m, rgb=None):
        if rgb is None: m, rgb = None, m                                        # add(b, rgb)：不变换
        if m is not None: bmesh.ops.transform(b, matrix=m, verts=b.verts)
        P.merge(bm, solid(b, rgb))
    # 气门帽：顶上一小圈金环 + 小圆钮
    add(P.torus(0.045, 0.009, seg=32, sides=10), Matrix.Translation((0, 0, ZC + R - 0.006)), GOLD)
    add(ellipsoid((0, 0, ZC + R + 0.006), 0.022, (1, 1, 0.7)), GOLD)
    # 颈口：小裙边
    add(P.torus(0.036, 0.008, seg=24, sides=8), Matrix.Translation((0, 0, 0.172)), CORAL)
    def face(y, z, out=0.0):
        """球面上 (y, z) 处的点和朝外的姿态（局部 X = 法线）"""
        x = math.sqrt(max(1e-6, R * R - y * y - (z - ZC) ** 2)); n = Vector((x, y, z - ZC)).normalized()
        side = Vector((0, 0, 1)).cross(n).normalized(); up = n.cross(side)
        rot = Matrix((n, side, up)).transposed().to_4x4()
        return Matrix.Translation(Vector((x, y, z)) + n * out) @ rot
    for sd in (-1, 1):
        add(ellipsoid((0, 0, 0), 0.026, (0.42, 0.9, 1.08), seg=24, rings=12), face(sd * 0.062, 0.405, -0.002), EYE)            # 珠眼：微微鼓出球面
        add(ellipsoid((0, 0, 0), 0.0075, (0.6, 1, 1), seg=12, rings=6), face(sd * 0.062 + 0.008, 0.414, 0.009), (1, 1, 1))     # 高光（同侧左上）
        add(ellipsoid((0, 0, 0), 0.0035, (0.6, 1, 1), seg=10, rings=5), face(sd * 0.062 - 0.009, 0.396, 0.008), (1, 1, 1))
        add(ellipsoid((0, 0, 0), 0.026, (0.18, 1.2, 0.85), seg=24, rings=10), face(sd * 0.105, 0.36, -0.001), CORAL)           # 腮红：薄片贴面
        brow = [face(sd * (0.04 + 0.046 * t), 0.452 + 0.012 * math.sin(t * math.pi) - 0.004 * t, 0.002).to_translation() for t in [i / 8 for i in range(9)]]
        add(P.tube(brow, 0.0042, seg=10, r_fn=lambda u: 0.55 + 0.45 * math.sin(u * math.pi)), BROW)                          # 眉毛：两头尖的细弧
    add(ellipsoid((0, 0, 0), 0.017, (0.75, 1, 0.9), seg=18, rings=9), face(0, 0.37, 0.004), CORAL)                              # 圆鼻头
    smile = [face(0.05 * (2 * t - 1), 0.338 - 0.022 * math.sin(t * math.pi), 0.0015).to_translation() for t in [i / 12 for i in range(13)]]
    add(P.tube(smile, 0.0045, seg=10, r_fn=lambda u: 0.6 + 0.4 * math.sin(u * math.pi)), BROW)                               # 微笑
    # 吊篮：带凹口的编织篮（横向编织色带）+ 篮沿
    bprof = [(0, 0), (0.048, 0), (0.056, 0.008), (0.064, 0.082), (0.054, 0.082), (0.051, 0.068), (0, 0.068)]
    bas = P.lathe(bprof, 40)
    vcol(bas, lambda co: WICK_D if co.z > 0.07 and co.xy.length < 0.0545 else (WICK if (int(co.z / 0.012) + int((math.atan2(co.y, co.x) + math.pi) / (math.tau / 20))) % 2 else mix(WICK, WICK_D, 0.45)))
    P.merge(bm, bas)
    add(P.torus(0.062, 0.0075, seg=40, sides=10), Matrix.Translation((0, 0, 0.083)), mix(WICK, ROPE, 0.4))
    # 四根吊绳：避开正面五官（±45°、±135°），从篮沿拉到球囊下半
    for k in range(4):
        a = math.radians(45 + 90 * k); ca, sa = math.cos(a), math.sin(a)
        top_r = 0.136; tz = ZC - math.sqrt(R * R - top_r * top_r) + 0.004
        pts = [(ca * 0.06, sa * 0.06, 0.086)] + [(ca * (0.06 + (top_r - 0.06) * t), sa * (0.06 + (top_r - 0.06) * t), 0.086 + (tz - 0.086) * t) for t in (0.33, 0.66)] + [(ca * top_r, sa * top_r, tz)]
        add(P.tube(pts, 0.0032, seg=8), ROPE)
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    return bm


# ───────── Boss 标识：小皇冠（头顶漂浮）+ 脚下光环 ─────────
def boss_crown(part='gold'):
    """Boss 头顶的糖果金皇冠（直径约 0.16 m，原点在冠底中心），分两件导出，Unity 里各用一种材质：
      gold — 金身：带凸起上下沿的冠带 + 两沿之间一圈小金珠 + 5 个饱满的弧面尖角（尖顶托着金珠座）+ 宝石的金爪托
      gems — 宝石：冠带正面心形大宝石 + 两侧切面圆宝石 + 尖角顶端的糖珠
    用户：王冠的质感要做好 → 金身走 ToyLit._Metal 金属反射，宝石亮面高光；几何上沿口、珠链、爪托都是实体，不靠贴图。"""
    GOLD, GOLD_D = (1.0, 0.78, 0.3), (0.86, 0.58, 0.2)
    PINK, MINT, LAV, SKY = (1.0, 0.42, 0.62), (0.4, 0.88, 0.72), (0.68, 0.5, 1.0), (0.45, 0.72, 1.0)
    R, H = 0.075, 0.046
    gold, gems = bmesh.new(), bmesh.new()
    # 冠带：外壁略向外张（上口 R+0.004），厚 0.007，倒圆
    prof = [(R - 0.0035, 0.0), (R + 0.0035, 0.0), (R + 0.0075, H), (R + 0.0005, H)]
    band = bmesh.new(); N = 72; rings = []
    for (rr, zz) in prof:
        rings.append([band.verts.new((math.cos(i / N * math.tau) * rr, math.sin(i / N * math.tau) * rr, zz)) for i in range(N)])
    for k in range(4):
        a0, a1 = rings[k], rings[(k + 1) % 4]
        for i in range(N):
            j = (i + 1) % N; band.faces.new((a0[i], a0[j], a1[j], a1[i]))
    bmesh.ops.recalc_face_normals(band, faces=band.faces)
    ring_edges = [e for e in band.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-6]
    bmesh.ops.bevel(band, geom=ring_edges, offset=0.0022, segments=3, affect='EDGES', profile=0.5, clamp_overlap=True)
    def rr_at(z): return R + 0.0035 + 0.004 * z / H
    P.merge(gold, vcol(band, lambda co: mix(GOLD_D, GOLD, 0.35 + 0.65 * co.z / H)))
    # 凸起的上下沿（圆管）
    for z, r in ((0.0025, 0.0042), (H - 0.002, 0.0038)):
        t = P.torus(rr_at(z) + 0.0008, r, seg=72, sides=12); bmesh.ops.translate(t, verts=t.verts, vec=(0, 0, z)); P.merge(gold, solid(t, GOLD))
    # 两沿之间一圈小金珠（避开正面宝石区 ±0.75 rad）
    for i in range(36):
        a = i / 36 * math.tau
        if abs(math.atan2(math.sin(a), math.cos(a))) < 2.0: continue                   # 正面 ±2 rad 是宝石区
        b = ellipsoid((0, 0, 0), 0.0026, seg=10, rings=6); zz = H * 0.5; rr = rr_at(zz) + 0.0012
        bmesh.ops.translate(b, verts=b.verts, vec=(math.cos(a) * rr, math.sin(a) * rr, zz)); P.merge(gold, solid(b, GOLD))
    # 5 个尖角：弧面三角片（沿冠带外壁弯曲的厚片），尖顶有金珠座
    for k in range(5):
        a = k / 5 * math.tau
        w, h = 0.044, 0.05 if k == 0 else 0.042                                  # 正面那根最高
        M, Hn = 20, 14; T2 = 0.0032
        sp = bmesh.new(); verts_o, verts_i = [], []
        for j in range(Hn + 1):
            v_ = j / Hn; half = max(0.0032, w * 0.5 * (1 - v_) ** 1.15 * (1 + 0.25 * math.sin(v_ * math.pi)))   # 两侧微鼓；顶端留 3 mm 宽（收成 0 会重合成退化面），金珠座盖住
            row_o, row_i = [], []
            for i in range(M + 1):
                u = (i / M - 0.5) * 2; ang = a + u * half / R
                lean = 0.006 * v_ * v_                                               # 尖角往外微微张开
                ro = rr_at(H) - 0.001 + lean + 0.0018 * (1 - u * u); ri = ro - T2
                z = H - 0.006 + v_ * h
                row_o.append(sp.verts.new((math.cos(ang) * ro, math.sin(ang) * ro, z))); row_i.append(sp.verts.new((math.cos(ang) * ri, math.sin(ang) * ri, z)))
            verts_o.append(row_o); verts_i.append(row_i)
        for j in range(Hn):
            for i in range(M):
                sp.faces.new((verts_o[j][i], verts_o[j][i + 1], verts_o[j + 1][i + 1], verts_o[j + 1][i]))
                sp.faces.new((verts_i[j][i], verts_i[j + 1][i], verts_i[j + 1][i + 1], verts_i[j][i + 1]))
        for j in range(Hn):                                                          # 两侧边封起来
            for i in (0, M):
                f = (verts_o[j][i], verts_o[j + 1][i], verts_i[j + 1][i], verts_i[j][i]); sp.faces.new(f if i == 0 else f[::-1])
        for i in range(M):                                                           # 底边、顶边
            sp.faces.new((verts_o[0][i], verts_i[0][i], verts_i[0][i + 1], verts_o[0][i + 1]))
            sp.faces.new((verts_o[Hn][i], verts_o[Hn][i + 1], verts_i[Hn][i + 1], verts_i[Hn][i]))
        bmesh.ops.recalc_face_normals(sp, faces=sp.faces)
        P.merge(gold, vcol(sp, lambda co: mix(GOLD, (1.0, 0.9, 0.55), max(0.0, min(1.0, (co.z - H) / 0.05)) * 0.5)))   # 尖角越往上越亮
        top_r = rr_at(H) - 0.001 + 0.006 - T2 * 0.5; tz = H - 0.006 + h
        seat = P.torus(0.0058, 0.0022, seg=20, sides=8); bmesh.ops.translate(seat, verts=seat.verts, vec=(math.cos(a) * top_r, math.sin(a) * top_r, tz + 0.002)); P.merge(gold, solid(seat, GOLD))
        tip = ellipsoid((math.cos(a) * top_r, math.sin(a) * top_r, tz + 0.0085), 0.0075 if k == 0 else 0.0065, seg=20, rings=10)
        P.merge(gems, solid(tip, [PINK, MINT, LAV, LAV, MINT][k]))
    # 冠带正面宝石：心形大宝石（两颗椭球并成心形）+ 两侧切面圆宝石；每颗外面一圈金爪托
    def facet_gem(r, flat=0.55, seg=10):
        """切面宝石：低段数的扁椭球，平直着色（Unity 里 sharp 角 35° 会让切面分明）"""
        return ellipsoid((0, 0, 0), r, (flat, 1, 1), seg=seg, rings=6)
    zc = H * 0.5
    def on_band(a, out):
        r_ = rr_at(zc) + out
        return Matrix.Translation((math.cos(a) * r_, math.sin(a) * r_, zc)) @ Matrix.Rotation(a, 4, 'Z')
    big = ellipsoid((0, 0, 0), 0.0095, (0.55, 1, 1.12), seg=12, rings=6)              # 正面大宝石：低段数切面椭圆，比两侧的大一圈
    bmesh.ops.transform(big, matrix=on_band(0, 0.0022), verts=big.verts); P.merge(gems, solid(big, PINK))
    ring = P.torus(0.0125, 0.0018, seg=28, sides=8); bmesh.ops.transform(ring, matrix=on_band(0, 0.0008) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), verts=ring.verts); P.merge(gold, solid(ring, GOLD))
    for k, col in ((1, MINT), (-1, MINT), (2, SKY), (-2, SKY), (3, LAV), (-3, LAV)):
        a = k * 0.6; rg = 0.0068 if abs(k) == 1 else 0.0055
        g = facet_gem(rg); bmesh.ops.transform(g, matrix=on_band(a, 0.0016), verts=g.verts); P.merge(gems, solid(g, col))
        cl = P.torus(rg + 0.0012, 0.0014, seg=20, sides=6); bmesh.ops.transform(cl, matrix=on_band(a, 0.0006) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), verts=cl.verts); P.merge(gold, solid(cl, GOLD))
    return gold if part == 'gold' else gems

BUILD = {'En_Balloon': (balloon_enemy, 50), 'BossCrown': (lambda: boss_crown('gold'), 40), 'BossCrownGems': (lambda: boss_crown('gems'), 30), 'W5_WindupBear': (windup_bear, 30), 'W4_GiftBox_A': (gift_whole((0.66, 0.9, 0.8), (1.0, 0.92, 0.6), 1.0), 30), 'W4_GiftBox_B': (gift_whole((0.82, 0.74, 1.0), (1.0, 0.7, 0.8), 1.0), 30),
         'W3_FeltStar': (felt_star, 30), 'W6_GummyBear': (gummy_bear, 30), 'W4_HeartCloud': (heart_cloud, 30), 'W4_PinataStar': (pinata_star, 30), 'W4_BalloonBunch': (balloon_bunch, 30)}


def export(name, bm, sharp):
    ob = P.to_obj(bm, name, smooth=True)
    if sharp:
        try: ob.data.set_sharp_from_angle(angle=math.radians(sharp))
        except Exception: pass
    for lod, ratio in ((0, 1.0), (1, 0.4)):
        o = ob.copy(); o.data = ob.data.copy(); bpy.context.scene.collection.objects.link(o)
        if ratio < 1:
            md = o.modifiers.new('dec', 'DECIMATE'); md.ratio = ratio; md.use_collapse_triangulate = True
            bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier='dec')
        bm2 = bmesh.new(); bm2.from_mesh(o.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(o.data); bm2.free()
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'{name}_lod{lod}.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True, colors_type='SRGB')
        print('EXPORT', name, lod, len(o.data.polygons))
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.objects.remove(ob, do_unlink=True)


def export_parts(dir_):
    """补给礼盒：GiftBox（盒身）/ GiftLid（盒盖）两件，导出到小怪部件目录（替换 blender_enemies_w1 的旧版，名字和枢轴不变）"""
    box, lid = gift_box()
    for name, bm in (('GiftBox', box), ('GiftLid', lid)):
        ob = P.to_obj(bm, name, smooth=True)
        try: ob.data.set_sharp_from_angle(angle=math.radians(35))
        except Exception: pass
        b2 = bmesh.new(); b2.from_mesh(ob.data); bmesh.ops.triangulate(b2, faces=b2.faces); b2.to_mesh(ob.data); b2.free()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(filepath=os.path.join(dir_, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True, colors_type='SRGB')
        bpy.data.objects.remove(ob, do_unlink=True)


def main():
    P.reset()
    if ONLY and ONLY[0] == '--gift-parts':
        export_parts(ONLY[1]); print('PROPSFIX', P.report); return
    for name, (fn, sharp) in BUILD.items():
        if ONLY and name not in ONLY: continue
        export(name, fn(), sharp)
    print('PROPSFIX', P.report)


if __name__ == '__main__':
    main()
