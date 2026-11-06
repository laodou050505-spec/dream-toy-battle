"""程序化部件公共库（Blender 里 import）：保证每个部件都是封闭流形的几何生成器 + 简单的 numpy 贴图。
所有生成器返回 bmesh；坐标 Blender（Z 向上）、单位米。"""
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix

report = {}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def check(bm, name):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bd = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    nm = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    report[name] = dict(faces=len(bm.faces), boundary=bd, nonmanifold=nm)
    if bd or nm:
        raise SystemExit(f'{name} 拓扑不合格: boundary={bd} nonmanifold={nm}')


def merge(dst, src):
    """把 src bmesh 并入 dst（各自是独立封闭块）"""
    me = bpy.data.meshes.new('_t'); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)
    return dst


def to_obj(bm, name, smooth=True):
    check(bm, name)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons: p.use_smooth = smooth
    return ob


def lathe(profile, seg, r_fn=None):
    """绕 Z 轴车削（profile: [(r, z)]，首尾 r=0 → 封闭）；r_fn(theta, z) 可按角度调制半径（南瓜瓣）"""
    bm = bmesh.new(); rings = []
    for r, z in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            ring = []
            for i in range(seg):
                t = i / seg * math.tau; rr = r * (r_fn(t, z) if r_fn else 1.0)
                ring.append(bm.verts.new((math.cos(t) * rr, math.sin(t) * rr, z)))
            rings.append(ring)
    for a, b in zip(rings[:-1], rings[1:]):
        if len(a) == 1 and len(b) == 1: continue
        if len(a) == 1:
            for i in range(seg): bm.faces.new((a[0], b[(i + 1) % seg], b[i]))
        elif len(b) == 1:
            for i in range(seg): bm.faces.new((a[i], a[(i + 1) % seg], b[0]))
        else:
            for i in range(seg): bm.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]))
    return bm


def box(cx, cy, cz, sx, sy, sz, bevel=0.0):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * sx + cx, v.co.y * sy + cy, v.co.z * sz + cz))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=2, affect='EDGES', profile=0.5)
    return bm


def tube(points, radius, seg=10, r_fn=None):
    """沿折线扫圆截面，两端封顶 → 封闭管；r_fn(u) 给出沿程半径比例（u ∈ [0,1]）"""
    bm = bmesh.new(); P = [Vector(p) for p in points]; n = len(P)
    T = [(P[min(i + 1, n - 1)] - P[max(i - 1, 0)]).normalized() for i in range(n)]
    up = Vector((0, 0, 1)) if abs(T[0].z) < 0.9 else Vector((1, 0, 0))
    N = (up - T[0] * up.dot(T[0])).normalized(); rings = []
    for i in range(n):
        if i > 0:   # 平行移动标架
            N = (N - T[i] * N.dot(T[i])).normalized()
        B = T[i].cross(N)
        r = radius * (r_fn(i / (n - 1)) if r_fn else 1.0)
        rings.append([bm.verts.new(P[i] + (N * math.cos(k / seg * math.tau) + B * math.sin(k / seg * math.tau)) * r) for k in range(seg)])
    for a, b in zip(rings[:-1], rings[1:]):
        for k in range(seg): bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    c0 = bm.verts.new(P[0] - T[0] * radius * 0.3); c1 = bm.verts.new(P[-1] + T[-1] * radius * 0.3)
    for k in range(seg):
        bm.faces.new((rings[0][(k + 1) % seg], rings[0][k], c0))
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % seg], c1))
    return bm


def slab(outline, thick, bend=0.0):
    """平面轮廓（XY，逆时针）挤出成有厚度的封闭薄片（叶子等）；bend 让叶面沿 X 方向弯曲"""
    bm = bmesh.new(); n = len(outline)
    cx = sum(p[0] for p in outline) / n; cy = sum(p[1] for p in outline) / n
    def z(x): return bend * (x - cx) ** 2
    top = [bm.verts.new((x, y, z(x) + thick / 2)) for x, y in outline]
    bot = [bm.verts.new((x, y, z(x) - thick / 2)) for x, y in outline]
    ct = bm.verts.new((cx, cy, z(cx) + thick * 0.8)); cb = bm.verts.new((cx, cy, z(cx) - thick * 0.8))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((top[i], top[j], ct)); bm.faces.new((bot[j], bot[i], cb))
        bm.faces.new((top[j], top[i], bot[i], bot[j]))
    return bm


def arc_block(center, r_in, r_out, a0, a1, depth, steps=3):
    """拱石：在 YZ 平面（中心 center=(y,z)）上 a0→a1 角度间的环形块，沿 X 方向厚 depth → 封闭"""
    bm = bmesh.new(); secs = []
    for k in range(steps + 1):
        a = a0 + (a1 - a0) * k / steps; c, s = math.cos(a), math.sin(a)
        secs.append([bm.verts.new((x, center[0] + c * r, center[1] + s * r)) for r, x in
                     ((r_in, -depth / 2), (r_out, -depth / 2), (r_out, depth / 2), (r_in, depth / 2))])
    for a, b in zip(secs[:-1], secs[1:]):
        for i in range(4): bm.faces.new((a[i], a[(i + 1) % 4], b[(i + 1) % 4], b[i]))
    bm.faces.new(secs[0][::-1]); bm.faces.new(secs[-1])
    return bm


def uv_smart(ob, margin=0.01):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')


def noise_tex(path, base, var=0.12, dark=None, size=512, seed=1, cells=10, streak=0.0):
    """卡通石/木/叶用的平滑噪声贴图：base 颜色 × (1 ± var 的分形噪声)；dark 给出暗斑色（苔藓、裂缝）"""
    rng = np.random.default_rng(seed)
    acc = np.zeros((size, size), np.float32); amp = 1.0; tot = 0
    for o in range(4):
        g = cells * 2 ** o
        grid = rng.random((g + 1, g + 1)).astype(np.float32)
        grid[-1, :] = grid[0, :]; grid[:, -1] = grid[:, 0]      # 首尾相同 → 贴图可无缝平铺
        x = np.linspace(0, g, size, endpoint=False); i0 = x.astype(int); f = x - i0; f = f * f * (3 - 2 * f)
        a = grid[i0][:, i0] * (1 - f)[None, :] + grid[i0][:, i0 + 1] * f[None, :]
        b = grid[i0 + 1][:, i0] * (1 - f)[None, :] + grid[i0 + 1][:, i0 + 1] * f[None, :]
        acc += amp * (a * (1 - f)[:, None] + b * f[:, None]); tot += amp; amp *= 0.5
    n = acc / tot
    if streak: n = n * (1 - streak) + streak * (0.5 + 0.5 * np.sin(np.linspace(0, 20 * np.pi, size, endpoint=False))[None, :])
    col = np.array(base, np.float32)[None, None] * (1 + var * (n[..., None] - 0.5) * 2)
    if dark is not None:
        m = np.clip((n - 0.62) * 6, 0, 1)[..., None]
        col = col * (1 - m) + np.array(dark, np.float32)[None, None] * m
    col = np.clip(col, 0, 1)
    rgba = np.concatenate([col, np.ones((size, size, 1), np.float32)], -1)
    im = bpy.data.images.new(os.path.basename(path), size, size, alpha=False); im.pixels = rgba[::-1].ravel()
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()


def export(ob, path):
    bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.triangulate(bm, faces=bm.faces); bm.to_mesh(ob.data); bm.free()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True)


def torus(R, r, seg=32, sides=10):
    """XY 平面上的封闭圆环（中心原点）"""
    bm = bmesh.new(); rings = []
    for i in range(seg):
        a = i / seg * math.tau; c = Vector((math.cos(a), math.sin(a), 0))
        rings.append([bm.verts.new(c * (R + r * math.cos(k / sides * math.tau)) + Vector((0, 0, r * math.sin(k / sides * math.tau)))) for k in range(sides)])
    for i in range(seg):
        a, b = rings[i], rings[(i + 1) % seg]
        for k in range(sides): bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))
    return bm


def U(x, y, z):
    """Unity 世界坐标 → Blender 坐标（FBX 导出 -Z forward / Y up 的逆映射）"""
    return Vector((-x, -z, y))


def ubox(x0, x1, y0, y1, z0, z1, bevel=0.0):
    """用 Unity 坐标的轴对齐范围建盒子"""
    a, b = U(x0, y0, z0), U(x1, y1, z1)
    c = (a + b) / 2; s = Vector((abs(a.x - b.x), abs(a.y - b.y), abs(a.z - b.z)))
    return box(c.x, c.y, c.z, s.x, s.y, s.z, bevel)


def blob(cx, cy, cz, sx, sy, sz, amp=0.08, freq=3.0, cuts=8, seed=0):
    """凹凸的圆角块（树篱、灌木）：细分立方体 → 投向圆角形 → 沿法线加噪声；始终封闭"""
    from mathutils import noise as mn
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    for v in bm.verts:
        p = v.co.copy()
        q = Vector((p.x * (1 - p.y ** 2 / 2 - p.z ** 2 / 2 + p.y ** 2 * p.z ** 2 / 3) ** 0.5,
                    p.y * (1 - p.z ** 2 / 2 - p.x ** 2 / 2 + p.z ** 2 * p.x ** 2 / 3) ** 0.5,
                    p.z * (1 - p.x ** 2 / 2 - p.y ** 2 / 2 + p.x ** 2 * p.y ** 2 / 3) ** 0.5))   # 立方体→球
        r = Vector((p.x * 0.75 + q.x * 0.25, p.y * 0.75 + q.y * 0.25, p.z * 0.75 + q.z * 0.25))   # 圆角方块
        w = Vector((r.x * sx / 2, r.y * sy / 2, r.z * sz / 2))
        n = mn.noise(Vector((w.x * freq + seed, w.y * freq, w.z * freq)))
        v.co = w + r.normalized() * n * amp + Vector((cx, cy, cz))
    return bm


def catenary(p0, p1, sag, n=24):
    """两点间下垂的曲线（彩灯线、藤）"""
    pts = []
    for i in range(n + 1):
        t = i / n; p = p0.lerp(p1, t); p = p - Vector((0, 0, sag * 4 * t * (1 - t)))
        pts.append(p)
    return pts
