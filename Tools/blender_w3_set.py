"""第 3 章「锡兵城堡」精细程序化部件 + 绒布纤维贴图（Blender 里运行）：
  Blender -b -P Tools/blender_w3_set.py -- <输出目录>

  T_FeltGrain.png   无缝绒布纤维贴图（短弯纤维 + 轻微起毛斑），乘在顶点色上；第 1 章草地也用
  W3_Ground         绒布草坪（橄榄绿/薄荷斑块）+ 蓝色绒布护城河（缎带般的椭圆环，边缘略翘）+ 城堡所在的绿绒布小岛（略高）
  W3_FeltArch       门框主体：鼓鼓的薰衣草紫绒布拱（圆角矩形截面沿拱形扫掠），正面两侧各一圈毛毯边缝线
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
rnd = random.Random(20261029)
TILE = 1.6                  # 绒布纤维贴图每米重复次数

MOAT_C, MOAT_A, MOAT_B, MOAT_W = (0.3, -4.15), 1.95, 1.35, 0.42
W, H = 1.3, 1.8
R_IN = W / 2
SPRING = H - R_IN


def felt_texture(path, size=512, seed=11):
    import numpy as np
    rng = np.random.default_rng(seed)
    img = np.full((size, size), 0.88, np.float32)
    yy, xx = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    for k in range(8):                                  # 起毛的大斑（周期正弦，无缝）
        fx, fy = rng.integers(1, 7, 2); ph = rng.uniform(0, 6.28)
        img += 0.012 * np.sin(fx * xx + fy * yy + ph)
    n = 9000
    for _ in range(n):                                  # 短弯纤维
        x, y = rng.uniform(0, size, 2); a = rng.uniform(0, math.pi * 2); L = rng.uniform(5, 13)
        curl = rng.uniform(-0.25, 0.25); v = rng.uniform(0.78, 1.0); w = rng.uniform(0.25, 0.6)
        for t in np.linspace(0, 1, int(L * 1.5)):
            aa = a + curl * t * 3
            px = int(x + math.cos(aa) * L * t) % size; py = int(y + math.sin(aa) * L * t) % size
            img[py, px] = img[py, px] * (1 - w) + v * w
    # 轻微模糊让纤维柔和（环绕）
    img = (img * 4 + np.roll(img, 1, 0) + np.roll(img, -1, 0) + np.roll(img, 1, 1) + np.roll(img, -1, 1)) / 8
    img = np.clip(img, 0, 1)
    im = bpy.data.images.new('felt', size, size, alpha=False)
    rgba = np.stack([img, img, img, np.ones_like(img)], -1)[::-1]
    im.pixels.foreach_set(rgba.ravel()); im.filepath_raw = path; im.file_format = 'PNG'; im.save()


def moat_q(x, z):
    return math.hypot((x - MOAT_C[0]) / MOAT_A, (z - MOAT_C[1]) / MOAT_B)


def ground_height(x, z):
    d = math.hypot(x - 0.3, z + 3.6)
    h = 0.0
    h += 0.5 * math.exp(-((x + 2.8) ** 2 + (z + 5.8) ** 2) / 2.5)
    h += 0.6 * math.exp(-((x - 3.3) ** 2 + (z + 6.2) ** 2) / 3.0)
    a = math.atan2(z + 3.6, x - 0.3)
    ring = math.exp(-((d - 8.0) ** 2) / 4.5) * (1.2 + 0.5 * math.sin(a * 3 + 0.4) + 0.3 * math.sin(a * 8 + 1.9))
    h += max(0.0, ring)
    q = moat_q(x, z)
    # 护城河：环带下凹 2cm（绒布缎带的厚度感）；岛上抬高 4cm
    band = MOAT_W / (2 * min(MOAT_A, MOAT_B))
    if abs(q - 1) < band: h -= 0.02 * (1 - (abs(q - 1) / band) ** 2)
    if q < 1 - band: h += 0.04 * min(1.0, (1 - band - q) / 0.05)
    h *= min(1.0, max(0.0, (d - 0.5) / 1.0)) if q > 1 + band else 1.0
    return h


def ground_color(x, z):
    from mathutils import noise as mn
    q = moat_q(x, z)
    band = MOAT_W / (2 * min(MOAT_A, MOAT_B))
    def sstep(a, b, t):
        t = max(0.0, min(1.0, (t - a) / (b - a))); return t * t * (3 - 2 * t)
    n = 0.5 + 0.5 * mn.noise(Vector((x * 0.7, z * 0.7, 2.0)))
    lawn = tuple(a + (b - a) * n for a, b in zip((0.5, 0.68, 0.36), (0.64, 0.8, 0.5)))      # 橄榄绿 ~ 薄荷
    island = (0.38, 0.66, 0.4)
    d = abs(q - 1) / band                                                                   # 0 = 河中线，1 = 河岸
    k = d
    water = (0.44 + 0.12 * k * k, 0.64 + 0.1 * k * k, 0.95)                                 # 蓝绒布，近岸略浅
    land = island if q < 1 else lawn
    w = 1.0 - sstep(0.86, 1.08, d)                                                          # 河岸柔和过渡（避免网格锯齿）
    c = tuple(wa * w + la * (1 - w) for wa, la in zip(water, land))
    j = mn.noise(Vector((x * 5, z * 5, 9.0))) * 0.02
    return tuple(max(0, min(1, v + j)) for v in c) + (1.0,)


def w3_ground():
    bm = bmesh.new()
    lay = bm.loops.layers.color.new('Col'); uvl = bm.loops.layers.uv.new('UVMap')
    # 规则网格（护城河的弧线需要均匀密度），外圈再接一圈放射状的远景地面
    N, half = 150, 4.2
    cx, cz = 0.3, -3.6
    grid = [[bm.verts.new(U(cx - half + 2 * half * i / N, 0, cz - half + 2 * half * j / N)) for j in range(N + 1)] for i in range(N + 1)]
    for row in grid:
        for v in row:
            x, z = -v.co.x, -v.co.y; v.co.z = ground_height(x, z)
    faces = []
    for i in range(N):
        for j in range(N):
            faces.append(bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1])))
    # 外圈：从网格边界向外到半径 11 的放射带
    border = [grid[i][0] for i in range(N + 1)] + [grid[N][j] for j in range(1, N + 1)] + [grid[i][N] for i in range(N - 1, -1, -1)] + [grid[0][j] for j in range(N - 1, 0, -1)]
    rings = [border]
    for r in range(1, 9):
        ring = []
        for v in border:
            x, z = -v.co.x, -v.co.y
            dx, dz = x - cx, z - cz; L = math.hypot(dx, dz)
            R = L + (11.0 - L) * (r / 8) ** 1.3
            nx, nz = cx + dx / L * R, cz + dz / L * R
            ring.append(bm.verts.new(U(nx, ground_height(nx, nz), nz)))
        rings.append(ring)
    m = len(border)
    for a, b in zip(rings[:-1], rings[1:]):
        for k in range(m):
            faces.append(bm.faces.new((a[k], a[(k + 1) % m], b[(k + 1) % m], b[k])))
    for f in faces:
        for l in f.loops:
            x, z = -l.vert.co.x, -l.vert.co.y
            l[lay] = ground_color(x, z); l[uvl].uv = (x * TILE, z * TILE)
    outer = rings[-1]
    bot = [bm.verts.new(Vector((v.co.x, v.co.y, -0.3))) for v in outer]
    for k in range(m):
        f = bm.faces.new((outer[(k + 1) % m], bot[(k + 1) % m], bot[k], outer[k]))
        for l in f.loops: l[lay] = (0.5, 0.66, 0.36, 1)
    f = bm.faces.new(bot[::-1])
    for l in f.loops: l[lay] = (0.5, 0.66, 0.36, 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


# ───────────────────────── 绒布拱门 ─────────────────────────
def arch_path(off, n_arc=40, n_side=10):
    """门洞外 off 米处的拱形中心线（从右下到左下），返回 Unity (x, y)"""
    r = R_IN + off
    pts = [(r, SPRING * i / n_side) for i in range(n_side)]
    pts += [(math.cos(math.pi * i / n_arc) * r, SPRING + math.sin(math.pi * i / n_arc) * r) for i in range(n_arc + 1)]
    pts += [(-r, SPRING * (1 - i / n_side)) for i in range(1, n_side + 1)]
    return pts


def w3_felt_arch():
    """圆角矩形截面（宽 0.24 深 0.14，四角圆 0.05）沿拱形扫掠，两端封平；正面两条毛毯边缝线"""
    bm = bmesh.new()
    lay = bm.loops.layers.color.new('Col')
    Wd, Dp, rc = 0.24, 0.14, 0.05
    prof = []
    for cxs, czs, a0 in ((Wd / 2 - rc, Dp / 2 - rc, 0), (-(Wd / 2 - rc), Dp / 2 - rc, 90), (-(Wd / 2 - rc), -(Dp / 2 - rc), 180), (Wd / 2 - rc, -(Dp / 2 - rc), 270)):
        for i in range(5):
            a = math.radians(a0 + 90 * i / 4)
            prof.append((cxs + math.cos(a) * rc, czs + math.sin(a) * rc))
    # 鼓胀：截面中部向外鼓 1.2cm（填充棉的感觉）
    prof = [(u * (1 + 0.06 * math.cos(v / Dp * math.pi)), v * (1 + 0.28 * math.cos(u / Wd * math.pi) ** 2)) for u, v in prof]   # 正面鼓起（填充棉）
    path = arch_path(Wd / 2 + 0.005)
    rings = []
    base = (0.72, 0.6, 0.94)
    for i, (x, y) in enumerate(path):
        x2, y2 = path[min(i + 1, len(path) - 1)]; x0, y0 = path[max(i - 1, 0)]
        tx, ty = x2 - x0, y2 - y0; L = math.hypot(tx, ty); tx, ty = tx / L, ty / L
        nx, ny = ty, -tx                        # 指向拱外侧
        ring = []
        for u, v in prof:
            px, py = x + nx * u, y + ny * u
            ring.append(bm.verts.new(U(px, max(py, 0.0) if i in (0, len(path) - 1) else py, 0.03 + v)))
        rings.append(ring)
    m = len(prof)
    uvl = bm.loops.layers.uv.new('UVMap')
    arc = [0.0]
    for i in range(1, len(path)): arc.append(arc[-1] + math.dist(path[i], path[i - 1]))
    per = [0.0]
    for k in range(1, m + 1): per.append(per[-1] + math.dist(prof[k % m], prof[k - 1]))
    side_faces = []
    for i, (a, b) in enumerate(zip(rings[:-1], rings[1:])):
        for k in range(m):
            f = bm.faces.new((a[k], b[k], b[(k + 1) % m], a[(k + 1) % m]))
            uvs = [(arc[i], per[k]), (arc[i + 1], per[k]), (arc[i + 1], per[k + 1]), (arc[i], per[k + 1])]
            for l, uv in zip(f.loops, uvs): l[uvl].uv = (uv[0] * 1.6, uv[1] * 1.6)
            side_faces.append(f)
    bm.faces.new(rings[0]); bm.faces.new(rings[-1][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        for l in f.loops:
            l[lay] = tuple(min(1, c * (0.97 + 0.06 * rnd.random())) for c in base) + (1,)
    # 平针缝线：正面两侧边缘内 2.2cm，沿边方向一段段的短线（长 1.6cm、间隔 1.1cm），奶油色
    zf = 0.03 + Dp / 2 * 1.0 + 0.004
    for side in (1, -1):
        off = Wd / 2 + 0.005 + side * (Wd / 2 - 0.024)
        sp = arch_path(off, n_arc=160, n_side=40)
        L = [0.0]
        for i in range(1, len(sp)): L.append(L[-1] + math.dist(sp[i], sp[i - 1]))
        def at(t):
            for i in range(1, len(sp)):
                if L[i] >= t:
                    f = (t - L[i - 1]) / max(L[i] - L[i - 1], 1e-9)
                    return (sp[i - 1][0] + (sp[i][0] - sp[i - 1][0]) * f, sp[i - 1][1] + (sp[i][1] - sp[i - 1][1]) * f)
            return sp[-1]
        t = 0.03
        while t + 0.016 < L[-1] - 0.03:
            pts = []
            for kk in range(4):
                x, y = at(t + 0.016 * kk / 3)
                # 正面是鼓的：缝线高度随离中线的距离降一点
                u = side * (Wd / 2 - 0.024)
                pts.append(U(x, y, 0.03 + Dp / 2 * (1 + 0.28 * math.cos(u / Wd * math.pi) ** 2) - 0.001))
            P.merge(bm, color_bm(P.tube(pts, 0.0026, seg=5), (1.0, 0.96, 0.86)))
            t += 0.027
    return bm


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    return bm


def main():
    P.reset()
    felt_texture(os.path.join(TEX, 'T_FeltGrain.png'))
    for name, fn in {'W3_Ground': w3_ground, 'W3_FeltArch': w3_felt_arch}.items():
        bm = fn()
        ob = P.to_obj(bm, name, smooth=True)
        bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                 bake_space_transform=True, colors_type='SRGB')
        bpy.data.objects.remove(ob, do_unlink=True)
    print('W3SET', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
