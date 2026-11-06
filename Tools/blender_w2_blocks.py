"""第 2 章门框：ABC 木头字母积木（Blender 里运行）：
  Blender -b -P Tools/blender_w2_blocks.py -- <输出目录>

每块 0.36 m 立方：圆角倒角木块 → 六个面各内嵌一块凹进去的彩色面板（inset + 下沉，同一封闭网格）
→ 面板上立体的字母 / 星星 / 爱心 / 月亮 / 小花（文字与曲线挤出 + 圆角倒角，独立封闭块，略嵌入面板）。
木纹：T_WoodGrain.png 图集（左半木纹、右半白色漆面微噪点），木头部分 UV 落在左半、彩漆部分落在右半，
顶点色只给彩漆着色（木头为白色，由贴图给出蜂蜜色）。导出 W2_Block_A/B/C.fbx（正面朝 Unity +Z = 朝玩家）。
"""
import bpy, bmesh, math, os, sys, json
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
TEX = os.path.join(os.path.dirname(os.path.dirname(OUT.rstrip('/'))), 'Textures')
S = 0.36
CREAM, PINK, MINT, LEMON, BLUE, LAV, PEACH = (1.0, 0.95, 0.86), (1.0, 0.72, 0.8), (0.62, 0.9, 0.78), (1.0, 0.9, 0.55), (0.58, 0.76, 1.0), (0.78, 0.68, 1.0), (1.0, 0.72, 0.56)
# 每块六个面（Unity 方向）：(面板色, 符号, 符号色)
BLOCKS = {
    'W2_Block_A': {'+z': (CREAM, 'A', BLUE), '+x': (PINK, 'star', LEMON), '+y': (CREAM, 'heart', MINT), '-x': (MINT, 'flower', PEACH), '-z': (LEMON, 'A', PINK), '-y': (BLUE, None, None)},
    'W2_Block_B': {'+z': (MINT, 'B', PEACH), '+x': (CREAM, 'moon', LAV), '+y': (BLUE, 'star', PINK), '-x': (LEMON, 'heart', PINK), '-z': (PINK, 'B', BLUE), '-y': (CREAM, None, None)},
    'W2_Block_C': {'+z': (BLUE, 'C', PINK), '+x': (LEMON, 'star', BLUE), '+y': (CREAM, 'flower', PEACH), '-x': (LAV, 'moon', LEMON), '-z': (PEACH, 'C', MINT), '-y': (MINT, None, None)},
}
DIRS = {'+x': Vector((1, 0, 0)), '-x': Vector((-1, 0, 0)), '+y': Vector((0, 1, 0)), '-y': Vector((0, -1, 0)), '+z': Vector((0, 0, 1)), '-z': Vector((0, 0, -1))}


def wood_texture(path, size=512):
    import numpy as np
    rng = np.random.default_rng(5)
    img = np.ones((size, size, 3), np.float32)
    h = size; w = size // 2
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    # 木纹：沿 y 方向的年轮条纹，带缓慢摆动和细纤维（左右、上下都周期，图集内无缝）
    wob = 6 * np.sin(yy / h * 2 * np.pi * 2 + 0.7) + 3 * np.sin(yy / h * 2 * np.pi * 5 + 2.1)
    ring = np.sin((xx + wob) / w * 2 * np.pi * 14) * 0.5 + 0.5
    fib = np.sin((xx + wob * 0.5) / w * 2 * np.pi * 61 + rng.uniform(0, 6)) * 0.5 + 0.5
    base = np.array([1.0, 0.9, 0.72], np.float32); dark = np.array([0.93, 0.76, 0.56], np.float32)   # 浅蜂蜜色，木纹淡淡的
    t = (ring ** 4 * 0.42 + fib * 0.12)[..., None]
    img[:, :w] = base * (1 - t) + dark * t
    # 右半：白色漆面 + 极轻微噪点
    img[:, w:] = 0.97 + rng.normal(0, 0.008, (h, size - w, 1)).astype(np.float32)
    img = np.clip(img, 0, 1)
    im = bpy.data.images.new('wood', size, size, alpha=False)
    rgba = np.concatenate([img[::-1], np.ones((size, size, 1), np.float32)], -1)
    im.pixels.foreach_set(rgba.ravel()); im.filepath_raw = path; im.file_format = 'PNG'; im.save()


def body(spec):
    """圆角木块 + 六面内嵌凹面板；返回 bmesh（Blender 坐标，原点在块中心）"""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=S)
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=0.026, segments=4, affect='EDGES', profile=0.5)
    bm.normal_update()
    big = sorted(bm.faces, key=lambda f: -f.calc_area())[:6]
    keys = {f: max(DIRS, key=lambda k: f.normal.dot(U_dir(DIRS[k]))) for f in big}
    bmesh.ops.inset_individual(bm, faces=big, thickness=0.03, depth=0.0, use_even_offset=True)          # 平的木边框
    walls = bmesh.ops.inset_individual(bm, faces=big, thickness=0.002, depth=-0.012, use_even_offset=True)['faces']   # 凹槽侧壁
    owner = {}
    for f in big:
        for e in f.edges:
            for lf in e.link_faces:
                if lf is not f: owner[lf] = keys[f]
    for wf in walls:
        if wf not in owner:
            # 侧壁与面板不共边的情况（角上的三角）：按法线最接近的面板归属
            owner[wf] = max(DIRS, key=lambda k: wf.calc_center_median().dot(U_dir(DIRS[k])))
    lay = bm.loops.layers.color.new('Col'); uvl = bm.loops.layers.uv.new('UVMap')
    bm.normal_update()
    for f in bm.faces:
        n = f.normal
        key = keys.get(f) or (owner.get(f) if f in walls else None)
        painted = key is not None
        col = spec[key][0] if painted else (1.0, 1.0, 1.0)
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [i for i in range(3) if i != ax]
        for l in f.loops:
            co = l.vert.co
            u, v = co[a] / S + 0.5, co[b] / S + 0.5
            l[uvl].uv = (0.52 + u * 0.46, v) if painted else (0.02 + u * 0.46, 0.05 + v * 0.9)
            l[lay] = (col[0], col[1], col[2], 1.0)
    return bm


def U_dir(v):
    """Unity 方向 → Blender 方向（同 P.U 的线性部分）"""
    return Vector((-v.x, -v.z, v.y))


def outline(kind, r):
    if kind == 'star':
        return [((r if i % 2 == 0 else r * 0.46) * math.cos(math.pi / 2 + i * math.pi / 5), (r if i % 2 == 0 else r * 0.46) * math.sin(math.pi / 2 + i * math.pi / 5)) for i in range(10)]
    if kind == 'heart':
        pts = []
        for i in range(48):
            t = i / 48 * math.tau
            x = 16 * math.sin(t) ** 3; y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((x / 17 * r, (y + 2) / 17 * r))
        return pts
    if kind == 'moon':
        pts = [(math.cos(math.radians(a)) * r, math.sin(math.radians(a)) * r) for a in range(60, 301, 8)]
        c2 = (r * 0.42, r * 0.12); r2 = r * 0.78
        inner = []
        for a in range(300, 59, -8):
            x, y = c2[0] + math.cos(math.radians(a)) * r2, c2[1] + math.sin(math.radians(a)) * r2
            inner.append((x, y))
        # 只保留外圆以内的内弧点
        inner = [p for p in inner if math.hypot(*p) < r * 0.97]
        return pts + inner
    if kind == 'flower':
        return [((r * (0.72 + 0.28 * math.cos(5 * t))) * math.cos(t), (r * (0.72 + 0.28 * math.cos(5 * t))) * math.sin(t)) for t in [i / 60 * math.tau for i in range(60)]]
    return None


def symbol_mesh(kind, size):
    """字母（Bfont）或曲线轮廓 → 挤出 + 圆角倒角 → 网格 bmesh（XY 平面、正面朝 +Z，底面在 z=0）"""
    if (len(kind) == 1 and kind.isalpha()):
        cu = bpy.data.curves.new('t', 'FONT'); cu.body = kind; cu.size = size * 1.25
        cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    else:
        cu = bpy.data.curves.new('c', 'CURVE'); cu.dimensions = '2D'; cu.fill_mode = 'BOTH'
        sp = cu.splines.new('POLY'); pts = outline(kind, size * 0.5)
        sp.points.add(len(pts) - 1)
        for p, (x, y) in zip(sp.points, pts): p.co = (x, y, 0, 1)
        sp.use_cyclic_u = True
    cu.extrude = 0.006; cu.bevel_depth = 0.0045; cu.bevel_resolution = 3; cu.offset = -0.0035 if (len(kind) == 1 and kind.isalpha()) else 0
    ob = bpy.data.objects.new('sym', cu); bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bm = bmesh.new(); bm.from_mesh(me)
    bpy.data.objects.remove(ob); bpy.data.curves.remove(cu); bpy.data.meshes.remove(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    if (len(kind) == 1 and kind.isalpha()):                      # 字母居中（Bfont 基线偏移）
        c = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
        xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]
        off = Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, 0))
        for v in bm.verts: v.co -= off
    return bm


def build(name, spec):
    bm = body(spec)
    lay = bm.loops.layers.color['Col']; uvl = bm.loops.layers.uv['UVMap']
    face_d = S / 2 - 0.012                                # 面板下沉后的平面
    for key, (pc, kind, sc) in spec.items():
        if not kind: continue
        sb = symbol_mesh(kind, S * (0.62 if (len(kind) == 1 and kind.isalpha()) else 0.56))
        n_u = DIRS[key]
        n = U_dir(n_u)
        # 符号局部 +Z → 面法线；局部 +Y → 面的“上”（侧面朝世界上方，顶/底面朝玩家方向）
        up = U_dir(Vector((0, 0, 1)) if key == '+y' else Vector((0, 1, 0)))   # 侧面：字头朝上；顶面：字头朝玩家
        xax = up.cross(n)
        m = Matrix((xax, up, n)).transposed().to_4x4()
        m.translation = n * (face_d - 0.0015)
        bmesh.ops.transform(sb, matrix=m, verts=sb.verts)
        l2 = sb.loops.layers.color.new('Col'); u2 = sb.loops.layers.uv.new('UVMap')
        for f in sb.faces:
            for l in f.loops: l[l2] = (sc[0], sc[1], sc[2], 1.0); l[u2].uv = (0.75, 0.5)
        P.merge(bm, sb)
    return bm


def main():
    P.reset()
    wood_texture(os.path.join(TEX, 'T_WoodGrain.png'))
    for name, spec in BLOCKS.items():
        bm = build(name, spec)
        ob = P.to_obj(bm, name, smooth=True)
        ob.data.shade_smooth()
        try: ob.data.set_sharp_from_angle(angle=math.radians(40))
        except Exception: pass
        bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                                 bake_space_transform=True, colors_type='SRGB')
        bpy.data.objects.remove(ob, do_unlink=True)
    print('BLOCKS', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
