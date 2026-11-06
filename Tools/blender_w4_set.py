"""第 4 章「气球派对」精细程序化部件 + 彩纸绒布贴图（Blender 里运行）：
  Blender -b -P Tools/blender_w4_set.py -- <输出目录>

  T_ConfettiFelt.png  无缝：绒布纤维 + 散落的粉彩彩纸圆点/小长条（乘在顶点色上）
  W4_BalloonArch      门框：沿拱形每 0.16 m 一簇 4 只水滴形气球（车削轮廓 + 打结的气嘴 + 小环），十字交错排成气球拱，
                      两侧门脚垂下卷曲的缎带
  W4_Ground           薄荷绿绒布草坪（远处缓坡 + 外圈山丘），平面 UV
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
rnd = random.Random(20261030)
W, H = 1.3, 1.8
R_IN = W / 2
SPRING = H - R_IN
BAL = [(1.0, 0.66, 0.8), (0.6, 0.8, 1.0), (0.8, 0.7, 1.0), (0.6, 0.92, 0.8), (1.0, 0.92, 0.6), (1.0, 0.76, 0.62), (0.98, 0.96, 0.98)]


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops: l[lay] = (rgb[0], rgb[1], rgb[2], 1.0)
    return bm


def confetti_texture(path, size=512, seed=21):
    import numpy as np
    rng = np.random.default_rng(seed)
    img = np.full((size, size, 3), 0.9, np.float32)
    yy, xx = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    for k in range(6):
        fx, fy = rng.integers(1, 6, 2); ph = rng.uniform(0, 6.28)
        img += (0.012 * np.sin(fx * xx + fy * yy + ph))[..., None]
    for _ in range(7000):                               # 绒布纤维
        x, y = rng.uniform(0, size, 2); a = rng.uniform(0, 6.28); L = rng.uniform(5, 12); v = rng.uniform(0.82, 1.0)
        for t in np.linspace(0, 1, int(L * 1.5)):
            px = int(x + math.cos(a + t) * L * t) % size; py = int(y + math.sin(a + t) * L * t) % size
            img[py, px] = img[py, px] * 0.6 + v * 0.4
    pal = np.array([(1.0, 0.6, 0.76), (0.55, 0.78, 1.0), (1.0, 0.88, 0.45), (0.74, 0.62, 1.0), (1.0, 0.7, 0.52), (0.55, 0.9, 0.75)], np.float32)
    for _ in range(140):                                # 彩纸：小圆点和小长条
        cx, cy = rng.uniform(0, size, 2); c = pal[rng.integers(0, len(pal))]
        if rng.random() < 0.55:
            r = rng.uniform(2.2, 3.6)
            for y in range(int(cy - r - 1), int(cy + r + 2)):
                for x in range(int(cx - r - 1), int(cx + r + 2)):
                    a = np.clip(r + 0.5 - math.hypot(x - cx, y - cy), 0, 1)
                    if a > 0: img[y % size, x % size] = img[y % size, x % size] * (1 - a) + c * a
        else:
            ang = rng.uniform(0, math.pi); L = rng.uniform(5, 9); wdt = rng.uniform(1.4, 2.2)
            ca, sa = math.cos(ang), math.sin(ang)
            for y in range(int(cy - L - 2), int(cy + L + 3)):
                for x in range(int(cx - L - 2), int(cx + L + 3)):
                    u = (x - cx) * ca + (y - cy) * sa; v = -(x - cx) * sa + (y - cy) * ca
                    a = np.clip(L + 0.5 - abs(u), 0, 1) * np.clip(wdt + 0.5 - abs(v), 0, 1)
                    if a > 0: img[y % size, x % size] = img[y % size, x % size] * (1 - a) + c * a
    img = np.clip(img, 0, 1)
    im = bpy.data.images.new('confetti', size, size, alpha=False)
    rgba = np.concatenate([img[::-1], np.ones((size, size, 1), np.float32)], -1)
    im.pixels.foreach_set(rgba.ravel()); im.filepath_raw = path; im.file_format = 'PNG'; im.save()


def balloon(r):
    """沿 +Z 的水滴形气球（底部气嘴在 -Z），原点在球心；返回 bmesh（气球 + 打结的气嘴，独立封闭块）"""
    prof = []
    n = 11
    for i in range(n + 1):
        t = i / n                                         # 0 = 底（气嘴处），1 = 顶
        ang = math.pi * t
        rr = math.sin(ang) * r * (0.82 + 0.18 * t)       # 下窄上宽
        z = -math.cos(ang) * r * 1.18
        prof.append((max(rr, 0.0) if 0 < i < n else 0.0, z))
    prof[0] = (0.0, -r * 1.18)
    bm = P.lathe(prof, 14)
    # 气嘴：小锥 + 一圈打结的小环
    nz = P.lathe([(0, -r * 1.36), (r * 0.12, -r * 1.33), (r * 0.08, -r * 1.22), (r * 0.05, -r * 1.12), (0, -r * 1.1)], 10)
    P.merge(bm, nz)
    ring = P.torus(r * 0.075, r * 0.035, seg=14, sides=6)
    for v in ring.verts: v.co.z += -r * 1.24
    P.merge(bm, ring)
    return bm


def arch_path(off, n_arc=80, n_side=20):
    r = R_IN + off
    pts = [(r, SPRING * i / n_side) for i in range(n_side)]
    pts += [(math.cos(math.pi * i / n_arc) * r, SPRING + math.sin(math.pi * i / n_arc) * r) for i in range(n_arc + 1)]
    pts += [(-r, SPRING * (1 - i / n_side)) for i in range(1, n_side + 1)]
    return pts


def w4_balloon_arch():
    bm = bmesh.new()
    off = 0.15
    path = arch_path(off)
    L = [0.0]
    for i in range(1, len(path)): L.append(L[-1] + math.dist(path[i], path[i - 1]))
    def at(t):
        for i in range(1, len(path)):
            if L[i] >= t:
                f = (t - L[i - 1]) / max(L[i] - L[i - 1], 1e-9)
                p = (path[i - 1][0] + (path[i][0] - path[i - 1][0]) * f, path[i - 1][1] + (path[i][1] - path[i - 1][1]) * f)
                tx, ty = path[i][0] - path[i - 1][0], path[i][1] - path[i - 1][1]; l = math.hypot(tx, ty)
                return p, (tx / l, ty / l)
        return path[-1], (0, -1)
    step = 0.175
    t = 0.09; k = 0; ci = 0
    while t < L[-1] - 0.05:
        (x, y), (tx, ty) = at(t)
        nx, ny = ty, -tx                                   # 拱外侧方向（Unity XY 平面内）
        for j in range(4):
            a = j * math.pi / 2 + (math.pi / 4 if k % 2 else 0)
            # 簇内 4 只：在（外侧法线, 前后 z）平面上排成十字
            du, dz = math.cos(a) * 0.075, math.sin(a) * 0.075
            r = rnd.uniform(0.072, 0.085)
            b = balloon(r)
            # 气球轴：朝外偏前/后，气嘴朝簇中心
            axis = Vector((nx * math.cos(a), ny * math.cos(a), math.sin(a))).normalized() * 0.85 + Vector((nx, ny, 0)) * 0.35
            axis_b = U(axis.x, axis.y, axis.z) - U(0, 0, 0)
            q = Vector((0, 0, 1)).rotation_difference(axis_b.normalized())
            spin = Matrix.Rotation(rnd.uniform(0, math.tau), 4, 'Z')
            m = Matrix.Translation(U(x + nx * du, max(y + ny * du, r * 1.0), 0.03 + dz)) @ q.to_matrix().to_4x4() @ spin
            bmesh.ops.transform(b, matrix=m, verts=b.verts)
            P.merge(bm, color_bm(b, BAL[(ci + j) % len(BAL)]))
        ci += 1; k += 1; t += step
    # 门脚垂下的卷曲缎带：从两侧最低一簇向下、向外卷三圈
    for side in (1, -1):
        for s in range(3):
            x0 = side * (R_IN + off) + side * rnd.uniform(-0.04, 0.06); y0 = rnd.uniform(0.25, 0.45); z0 = 0.03 + rnd.uniform(-0.05, 0.05)
            pts = []
            for i in range(70):
                u = i / 69
                ang = u * math.tau * 3.2 + s
                rr = 0.018 + 0.01 * u
                pts.append(U(x0 + side * (math.cos(ang) * rr + u * 0.06), y0 - u * (y0 - 0.02), z0 + math.sin(ang) * rr))
            rb = P.tube(pts, 0.0035, seg=6)
            P.merge(bm, color_bm(rb, BAL[(s * 2 + (side > 0)) % len(BAL)]))
    return bm


def w4_carousel_pole():
    """旋转木马中心柱：粉白螺旋条纹圆柱（顶点色按螺旋角上色）+ 上下奶油色圆环箍；原点在底部中心，高 1 m（Unity 里按需缩放）"""
    bm = bmesh.new(); lay = bm.loops.layers.color.new('Col')
    r, Hh, seg, rings = 0.032, 1.0, 24, 80
    vs = [[bm.verts.new((math.cos(k / seg * math.tau) * r, math.sin(k / seg * math.tau) * r, Hh * j / rings)) for k in range(seg)] for j in range(rings + 1)]
    c0 = bm.verts.new((0, 0, 0)); c1 = bm.verts.new((0, 0, Hh))
    faces = []
    for j in range(rings):
        for k in range(seg):
            faces.append(bm.faces.new((vs[j][k], vs[j][(k + 1) % seg], vs[j + 1][(k + 1) % seg], vs[j + 1][k])))
    for k in range(seg):
        faces.append(bm.faces.new((vs[0][(k + 1) % seg], vs[0][k], c0))); faces.append(bm.faces.new((vs[rings][k], vs[rings][(k + 1) % seg], c1)))
    for f in faces:
        for l in f.loops:
            co = l.vert.co
            a = math.atan2(co.y, co.x) / math.tau + co.z * 3.2          # 螺旋
            pink = (math.sin(a * math.tau * 2) > 0)
            l[lay] = ((1.0, 0.62, 0.78) if pink else (1.0, 0.97, 0.95)) + (1.0,)
    for zc in (0.03, Hh - 0.03):
        ring = P.torus(r * 1.15, 0.012, seg=24, sides=8)
        for v in ring.verts: v.co.z += zc
        P.merge(bm, color_bm(ring, (1.0, 0.92, 0.72)))
    return bm


def ground_height(x, z):
    d = math.hypot(x - 0.3, z + 3.6)
    h = 0.25 * math.exp(-((x + 2.6) ** 2 + (z + 5.5) ** 2) / 2.5) + 0.35 * math.exp(-((x - 3.0) ** 2 + (z + 6.0) ** 2) / 3.0)
    a = math.atan2(z + 3.6, x - 0.3)
    h += max(0.0, math.exp(-((d - 8.0) ** 2) / 4.5) * (1.0 + 0.5 * math.sin(a * 4 + 0.9) + 0.3 * math.sin(a * 9 + 0.2)))
    h *= min(1.0, max(0.0, (d - 1.6) / 1.4))
    return h


def w4_ground():
    from mathutils import noise as mn
    bm = bmesh.new()
    lay = bm.loops.layers.color.new('Col'); uvl = bm.loops.layers.uv.new('UVMap')
    R, rings, segs, cx, cz = 11.0, 48, 96, 0.3, -3.6
    center = bm.verts.new(U(cx, ground_height(cx, cz), cz))
    vs = [[None] * segs for _ in range(rings + 1)]
    for r in range(1, rings + 1):
        rad = R * (r / rings) ** 1.4
        for s in range(segs):
            a = s / segs * math.tau; x, z = cx + math.cos(a) * rad, cz + math.sin(a) * rad
            vs[r][s] = bm.verts.new(U(x, ground_height(x, z), z))
    faces = [bm.faces.new((center, vs[1][s], vs[1][(s + 1) % segs])) for s in range(segs)]
    for r in range(1, rings):
        for s in range(segs):
            faces.append(bm.faces.new((vs[r][s], vs[r + 1][s], vs[r + 1][(s + 1) % segs], vs[r][(s + 1) % segs])))
    for f in faces:
        for l in f.loops:
            x, z = -l.vert.co.x, -l.vert.co.y
            n = 0.5 + 0.5 * mn.noise(Vector((x * 0.6, z * 0.6, 3.0)))
            c = tuple(a + (b - a) * n for a, b in zip((0.62, 0.86, 0.72), (0.76, 0.93, 0.8)))   # 薄荷 ~ 浅薄荷
            l[lay] = c + (1.0,); l[uvl].uv = (x * 1.4, z * 1.4)
    rim = vs[rings]
    bot = [bm.verts.new(Vector((v.co.x, v.co.y, -0.3))) for v in rim]
    for s in range(segs):
        f = bm.faces.new((rim[(s + 1) % segs], bot[(s + 1) % segs], bot[s], rim[s]))
        for l in f.loops: l[lay] = (0.62, 0.86, 0.72, 1)
    f = bm.faces.new(bot[::-1])
    for l in f.loops: l[lay] = (0.62, 0.86, 0.72, 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def main():
    P.reset()
    confetti_texture(os.path.join(TEX, 'T_ConfettiFelt.png'))
    for name, fn in {'W4_BalloonArch': w4_balloon_arch, 'W4_Ground': w4_ground, 'W4_CarouselPole': w4_carousel_pole}.items():
        bm = fn()
        ob = P.to_obj(bm, name, smooth=True)
        bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                 bake_space_transform=True, colors_type='SRGB')
        bpy.data.objects.remove(ob, do_unlink=True)
    print('W4SET', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
