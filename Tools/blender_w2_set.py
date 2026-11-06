"""第 2 章「沙盒兵营」精细程序化部件（Blender 里运行）：
  Blender -b -P Tools/blender_w2_set.py -- <输出目录>

  W2_Ground   粉彩沙丘：奶油粉沙地 + 每座沙丘一种粉彩色（薄荷/柠檬/粉/薰衣草）柔和过渡 + 细沙颗粒色斑 + 外圈沙丘挡边
  W2_Border   沙盒木框：圆角矩形一圈上漆木板（倒角板 + 木纹色带 + 角柱 + 圆钉头），半埋在沙里
  W2_Decor    牙签小纸旗（波浪纸面）、扇贝壳（放射棱）、彩色小石子，贴着沙丘摆放
全部封闭流形，顶点色 sRGB；坐标按 Unity 约定（门面 z=0，门后世界在 -z）。
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
rnd = random.Random(20261028)

SAND = (0.96, 0.8, 0.74)       # 暖粉沙（太白在日光下会过曝）
# 沙丘：(x, z, 高, 半径², 颜色)
DUNES = [(-1.6, -3.7, 0.34, 1.1, (0.5, 0.84, 0.7)),      # 薄荷（瞭望塔）
         (0.65, -3.85, 0.26, 0.9, (0.98, 0.84, 0.44)),   # 柠檬（吉普）
         (-0.85, -4.5, 0.42, 0.8, (0.98, 0.6, 0.7)),     # 粉（大炮）
         (2.1, -4.9, 0.62, 1.6, (0.7, 0.62, 0.95)),      # 薰衣草
         (-2.9, -5.6, 0.7, 2.2, (0.52, 0.72, 0.95)),     # 浅蓝
         (0.2, -6.6, 0.85, 3.0, (0.98, 0.7, 0.55))]      # 蜜桃（远）
CENTER = (0.2, -3.2)
PASTEL = [(0.98, 0.6, 0.76), (0.52, 0.87, 0.71), (1.0, 0.86, 0.45), (0.5, 0.74, 0.98), (0.71, 0.58, 0.98), (1.0, 0.68, 0.5)]


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    c = (rgb[0], rgb[1], rgb[2], 1.0)
    for f in bm.faces:
        for l in f.loops: l[lay] = c
    return bm


def jitter(rgb, k=0.04):
    return tuple(max(0, min(1, x + rnd.uniform(-k, k))) for x in rgb)


def ground_height(x, z):
    d = math.hypot(x - CENTER[0], z - CENTER[1])
    h = 0.0
    for dx, dz, hh, r2, _ in DUNES:
        h += hh * math.exp(-((x - dx) ** 2 + (z - dz) ** 2) / r2)
    a = math.atan2(z - CENTER[1], x - CENTER[0])
    ring = math.exp(-((d - 8.0) ** 2) / 4.5) * (1.1 + 0.5 * math.sin(a * 4 + 1.1) + 0.3 * math.sin(a * 9 + 0.4))
    h += max(0.0, ring)
    h += 0.025 * math.sin(x * 2.3 + z * 0.7) * math.sin(z * 1.9 - x * 0.4)   # 细小的沙纹
    h *= min(1.0, max(0.0, (d - 0.9) / 1.2)) * 0.85 + 0.15 * min(1.0, d / 0.9)
    return h


def ground_color(x, z):
    from mathutils import noise as mn
    wsum, c = 0.0, [0.0, 0.0, 0.0]
    for dx, dz, hh, r2, col in DUNES:
        w = math.exp(-((x - dx) ** 2 + (z - dz) ** 2) / (r2 * 0.55)) * 2.2
        wsum += w
        for i in range(3): c[i] += col[i] * w
    base = 1.0
    out = [(SAND[i] * base + c[i]) / (base + wsum) for i in range(3)]
    n = mn.noise(Vector((x * 9.0, z * 9.0, 0.3))) * 0.035 + mn.noise(Vector((x * 31.0, z * 31.0, 1.7))) * 0.02   # 沙粒色斑
    return tuple(max(0, min(1, v + n)) for v in out) + (1.0,)


GRAIN_TILE = 1.25      # 每米重复次数


def sand_grain_texture(path, size=512, seed=7):
    """无缝细沙贴图（乘在顶点色上）：整体略低于白（压亮度）+ 明暗沙粒 + 少量粉彩彩沙颗粒"""
    import numpy as np
    rng = np.random.default_rng(seed)
    img = np.full((size, size, 3), 0.86, np.float32)
    # 低频起伏（周期噪声：随机相位的正弦叠加，天然无缝）
    yy, xx = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    for k in range(6):
        fx, fy = rng.integers(1, 5, 2); ph = rng.uniform(0, 6.28)
        img += (0.018 * np.sin(fx * xx + fy * yy + ph))[..., None]
    def dots(n, rmin, rmax, colfn):
        for _ in range(n):
            cx, cy = rng.uniform(0, size, 2); r = rng.uniform(rmin, rmax); c = colfn()
            x0, x1 = int(cx - r - 1), int(cx + r + 2); y0, y1 = int(cy - r - 1), int(cy + r + 2)
            for y in range(y0, y1):
                for x in range(x0, x1):
                    d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
                    a = np.clip(r + 0.5 - d, 0, 1)
                    if a > 0:
                        img[y % size, x % size] = img[y % size, x % size] * (1 - a) + c * a
    dots(5200, 0.5, 1.3, lambda: np.full(3, rng.uniform(0.72, 0.8), np.float32))     # 暗沙粒
    dots(4200, 0.5, 1.2, lambda: np.full(3, rng.uniform(0.95, 1.0), np.float32))     # 亮沙粒
    pal = np.array([(1.0, 0.62, 0.78), (0.55, 0.88, 0.74), (1.0, 0.88, 0.5), (0.58, 0.76, 1.0), (0.76, 0.64, 1.0)], np.float32)
    dots(420, 0.9, 1.7, lambda: pal[rng.integers(0, len(pal))] * 0.95)               # 彩沙
    img = np.clip(img, 0, 1)
    im = bpy.data.images.new('grain', size, size, alpha=False)
    rgba = np.concatenate([img[::-1], np.ones((size, size, 1), np.float32)], -1)
    im.pixels.foreach_set(rgba.ravel()); im.filepath_raw = path; im.file_format = 'PNG'; im.save()


def w2_ground():
    bm = bmesh.new()
    lay = bm.loops.layers.color.new('Col')
    R, rings, segs = 11.0, 64, 128
    vs = [[None] * segs for _ in range(rings + 1)]
    cx, cz = CENTER
    center = bm.verts.new(U(cx, ground_height(cx, cz), cz))
    for r in range(1, rings + 1):
        rad = R * (r / rings) ** 1.4
        for s in range(segs):
            a = s / segs * math.tau
            x, z = cx + math.cos(a) * rad, cz + math.sin(a) * rad
            vs[r][s] = bm.verts.new(U(x, ground_height(x, z), z))
    faces = []
    for s in range(segs):
        faces.append(bm.faces.new((center, vs[1][s], vs[1][(s + 1) % segs])))
    for r in range(1, rings):
        for s in range(segs):
            faces.append(bm.faces.new((vs[r][s], vs[r + 1][s], vs[r + 1][(s + 1) % segs], vs[r][(s + 1) % segs])))
    uvl = bm.loops.layers.uv.new('UVMap')
    for f in faces:
        for l in f.loops:
            p = l.vert.co
            l[lay] = ground_color(-p.x, -p.y)
            l[uvl].uv = (-p.x * GRAIN_TILE, -p.y * GRAIN_TILE)   # 平面投影 UV：细沙颗粒贴图按世界尺寸平铺
    rim = vs[rings]
    bot = [bm.verts.new(Vector((v.co.x, v.co.y, -0.3))) for v in rim]
    for s in range(segs):
        f = bm.faces.new((rim[(s + 1) % segs], bot[(s + 1) % segs], bot[s], rim[s]))
        for l in f.loops: l[lay] = SAND + (1,)
    f = bm.faces.new(bot[::-1])
    for l in f.loops: l[lay] = SAND + (1,)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


# ───────────────────────── 沙盒木框 ─────────────────────────
def border_path(step=0.05, hw=3.3, hd=2.5, rc=0.9):
    """圆角矩形（中心 CENTER 偏后），按约 step 米均匀采样，返回 [(x,z)]（逆时针，从右边中点开始）"""
    cx, cz = CENTER[0], CENTER[1] - 0.6
    sx, sz = hw - rc, hd - rc
    pts = []
    def line(a, b):
        n = max(1, int(math.dist(a, b) / step))
        for i in range(n): pts.append((a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n))
    def arc(ox, oz, a0):
        n = max(2, int(rc * math.pi / 2 / step))
        for i in range(n):
            a = math.radians(a0 + 90 * i / n); pts.append((cx + ox + math.cos(a) * rc, cz + oz + math.sin(a) * rc))
    line((cx + hw, cz), (cx + hw, cz + sz)); arc(sx, sz, 0)
    line((cx + sx, cz + hd), (cx - sx, cz + hd)); arc(-sx, sz, 90)
    line((cx - hw, cz + sz), (cx - hw, cz - sz)); arc(-sx, -sz, 180)
    line((cx - sx, cz - hd), (cx + sx, cz - hd)); arc(sx, -sz, 270)
    line((cx + hw, cz - sz), (cx + hw, cz))
    return pts


def plank(p0, p1, y0, y1, thick, rgb):
    """沿 p0→p1 的倒角木板：长度方向、高度 y0..y1、厚 thick，带两道浅色木纹带"""
    (x0, z0), (x1, z1) = p0, p1
    L = math.hypot(x1 - x0, z1 - z0)
    bm = P.ubox(-L / 2, L / 2, y0, y1, -thick / 2, thick / 2, bevel=min(0.012, thick * 0.3))
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops:
            ux = -l.vert.co.x
            grain = 0.04 * math.sin(ux * 38 + rnd.random()) + 0.03 * math.sin(ux * 11)
            l[lay] = tuple(max(0, min(1, c + grain)) for c in rgb) + (1.0,)
    ang = math.atan2(z1 - z0, x1 - x0)
    m = Matrix.Translation(U((x0 + x1) / 2, 0, (z0 + z1) / 2)) @ Matrix.Rotation(ang, 4, 'Z')
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def w2_border():
    bm = bmesh.new()
    pts = border_path()
    # 每 ~0.42 m 一块板，颜色交替
    seg = []
    acc, last = 0.0, 0
    for i in range(1, len(pts) + 1):
        acc += math.dist(pts[i - 1], pts[i % len(pts)])
        if acc >= 0.42:
            seg.append((last, i % len(pts))); last = i % len(pts); acc = 0.0
    cols = [(0.62, 0.88, 0.8), (1.0, 0.8, 0.66), (1.0, 0.9, 0.62), (0.8, 0.76, 0.98)]
    for k, (a, b) in enumerate(seg):
        p0, p1 = pts[a], pts[b]
        mid = ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2)
        if mid[1] > -1.6:          # 前边敞开（门洞就是沙盒的前沿），不挡住看营地的视线
            continue
        g = ground_height(*mid)
        top = g + 0.11 + 0.012 * math.sin(k * 1.7)
        pl = plank(p0, p1, g - 0.12, top, 0.045, jitter(cols[k % 4], 0.02))
        P.merge(bm, pl)
        # 板接缝处的圆角小柱 + 顶上一颗圆钉头
        x, z = p1
        gy = ground_height(x, z)
        post = P.lathe([(0, gy - 0.12), (0.03, gy - 0.12), (0.03, top + 0.02), (0.024, top + 0.032), (0, top + 0.034)], 14)
        for v in post.verts: v.co = U(x + v.co.x, v.co.z, z + v.co.y)
        bmesh.ops.recalc_face_normals(post, faces=post.faces)
        P.merge(bm, color_bm(post, (1.0, 0.97, 0.92)))
    return bm


# ───────────────────────── 点缀 ─────────────────────────
def place(bm, x, z, yaw, sink=0.004, tilt=0.0):
    y = ground_height(x, z) - sink
    m = Matrix.Translation(U(x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tilt, 4, 'X')
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def paper_flag(x, z):
    bm = bmesh.new()
    h = rnd.uniform(0.16, 0.26)
    stick = P.tube([(0, 0, -0.03), (0, 0, h * 0.5), (0, 0, h)], 0.0032, seg=8)
    P.merge(bm, color_bm(stick, (0.96, 0.88, 0.7)))
    # 三角纸旗：沿 X 伸出，纸面带波浪（细分薄片，前后两面 + 侧边封闭）
    L, Hh, nx, ny, t = rnd.uniform(0.06, 0.085), rnd.uniform(0.04, 0.055), 10, 4, 0.0016
    col = rnd.choice(PASTEL)
    fb = bmesh.new()
    def pos(i, j, side):
        u = i / nx; v = j / ny
        x0 = u * L; half = Hh / 2 * (1 - u) + 0.0007 * u     # 尖端留一小段边，避免退化面
        zz = h - Hh / 2 - 0.004 + (v * 2 - 1) * half
        wave = 0.008 * math.sin(u * math.pi * 1.6) * u
        return (x0 + 0.003, wave + side * t / 2, zz)
    top = [[fb.verts.new(pos(i, j, 1)) for j in range(ny + 1)] for i in range(nx + 1)]
    bot = [[fb.verts.new(pos(i, j, -1)) for j in range(ny + 1)] for i in range(nx + 1)]
    for i in range(nx):
        for j in range(ny):
            fb.faces.new((top[i][j], top[i + 1][j], top[i + 1][j + 1], top[i][j + 1]))
            fb.faces.new((bot[i][j + 1], bot[i + 1][j + 1], bot[i + 1][j], bot[i][j]))
    ring = [(i, 0) for i in range(nx + 1)] + [(nx, j) for j in range(1, ny + 1)] + [(i, ny) for i in range(nx - 1, -1, -1)] + [(0, j) for j in range(ny - 1, 0, -1)]
    for k in range(len(ring)):
        a, b = ring[k], ring[(k + 1) % len(ring)]
        fb.faces.new((top[a[0]][a[1]], bot[a[0]][a[1]], bot[b[0]][b[1]], top[b[0]][b[1]]))
    bmesh.ops.recalc_face_normals(fb, faces=fb.faces)
    P.merge(bm, color_bm(fb, col))
    return place(bm, x, z, rnd.uniform(0, math.tau), sink=0.0)


def shell(x, z):
    """扇贝：极坐标网格（角度 ±68°、半径 0.18..1）的拱起薄壳 + 放射棱，挤出成封闭体，加铰链小耳朵"""
    S = rnd.uniform(0.035, 0.055)
    na, nr = 36, 7
    bm = bmesh.new()
    def p(i, j, top):
        a = math.radians(-68 + 136 * i / na); r = 0.18 + 0.82 * j / nr
        rib = 0.1 * (0.5 + 0.5 * math.cos(i / na * math.tau * 4.5)) * r   # 9 道放射棱
        hgt = (0.32 * (1 - r * r) + 0.06 + rib) * S if top else 0.0
        return (math.sin(a) * r * S, math.cos(a) * r * S, hgt)
    T = [[bm.verts.new(p(i, j, True)) for j in range(nr + 1)] for i in range(na + 1)]
    B = [[bm.verts.new(p(i, j, False)) for j in range(nr + 1)] for i in range(na + 1)]
    for i in range(na):
        for j in range(nr):
            bm.faces.new((T[i][j], T[i + 1][j], T[i + 1][j + 1], T[i][j + 1]))
            bm.faces.new((B[i][j + 1], B[i + 1][j + 1], B[i + 1][j], B[i][j]))
    ring = [(i, 0) for i in range(na + 1)] + [(na, j) for j in range(1, nr + 1)] + [(i, nr) for i in range(na - 1, -1, -1)] + [(0, j) for j in range(nr - 1, 0, -1)]
    for k in range(len(ring)):
        a, b = ring[k], ring[(k + 1) % len(ring)]
        bm.faces.new((T[a[0]][a[1]], B[a[0]][a[1]], B[b[0]][b[1]], T[b[0]][b[1]]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    col = rnd.choice([(1.0, 0.82, 0.78), (1.0, 0.92, 0.8), (0.9, 0.84, 1.0)])
    lay = bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops:
            k = 0.92 + 0.08 * (l.vert.co.z / (0.5 * S))
            l[lay] = tuple(min(1, c * k) for c in col) + (1,)
    ear = P.box(0, 0.1 * S, 0.05 * S, 0.5 * S, 0.22 * S, 0.1 * S, bevel=0.03 * S)
    P.merge(bm, color_bm(ear, col))
    return place(bm, x, z, rnd.uniform(0, math.tau), sink=0.002)


def pebble(x, z):
    r = rnd.uniform(0.014, 0.028)
    bm = P.blob(0, 0, r * 0.45, r * 2, r * 1.6, r * 0.9, amp=r * 0.12, freq=40, cuts=3, seed=rnd.random() * 10)
    return place(color_bm(bm, rnd.choice(PASTEL + [(0.92, 0.9, 0.95)])), x, z, rnd.uniform(0, math.tau), sink=r * 0.15)


AVOID = [(-0.9, -2.3, 0.45), (0.45, -2.0, 0.45), (-0.25, -1.6, 0.25), (-1.6, -3.6, 0.5), (0.6, -3.6, 0.5),
         (1.7, -2.9, 0.45), (2.3, -2.2, 0.3), (2.6, -1.4, 0.2), (-0.9, -4.3, 0.55), (-2.0, -2.2, 0.35)]


def free(x, z, pad):
    if z > -0.35: return False
    return all(math.hypot(x - ax, z - az) > r + pad for ax, az, r in AVOID)


def scatter(n, pad, fn, pts, zmin=-5.6):
    out = tries = 0
    while out < n and tries < n * 300:
        tries += 1
        z = -0.45 + (rnd.random() ** 1.3) * (zmin + 0.45)
        x = rnd.uniform(-3.0, 3.4)
        if not free(x, z, pad) or any(math.hypot(x - px, z - pz) < pad + pr for px, pz, pr in pts): continue
        pts.append((x, z, pad)); out += 1
        yield fn(x, z)


def w2_decor():
    bm = bmesh.new(); pts = []
    for part in scatter(9, 0.12, paper_flag, pts): P.merge(bm, part)
    for part in scatter(14, 0.06, shell, pts): P.merge(bm, part)
    for part in scatter(40, 0.03, pebble, pts): P.merge(bm, part)
    return bm


def main():
    P.reset()
    tex = os.path.join(os.path.dirname(os.path.dirname(OUT.rstrip('/'))), 'Textures', 'T_SandGrain.png')
    os.makedirs(os.path.dirname(tex), exist_ok=True)
    sand_grain_texture(tex)
    parts = {'W2_Ground': w2_ground, 'W2_Border': w2_border, 'W2_Decor': w2_decor}
    for name, fn in parts.items():
        bm = fn()
        ob = P.to_obj(bm, name, smooth=True)
        if name == 'W2_Border':
            try: ob.data.set_sharp_from_angle(angle=math.radians(40))
            except Exception: pass
        bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                 bake_space_transform=True, colors_type='SRGB')
        bpy.data.objects.remove(ob, do_unlink=True)
    print('W2SET', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
