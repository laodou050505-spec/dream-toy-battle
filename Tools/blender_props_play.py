"""玩法道具（Blender 里运行）：Blender -b -P Tools/blender_props_play.py -- <输出目录>
  Dart_Foam      泡沫软弹：桃色弹身（两道浅槽）+ 内凹吸盘碗口（天蓝唇边 / 黄碗心）+ 4 片尾翼；原点在弹身中心，+Z 朝前
  Target_Board   气球靶的靶盘：同心色环（粉 / 奶油 / 薄荷 / 黄心）+ 奶油镶边环 + 顶部小挂环；原点在靶心（规则层的靶子位置）
  Target_Balloon 吊着靶盘的气球 + 绳子：原点在绳子下端（挂环处），气球在上方——靶子浮空的理由（风吹 / 被打时绳子摆）
Unity 坐标，全部封闭流形，顶点色 sRGB。
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
PEACH, CREAM, BLUE, PINK, MINT, YEL, LAV = (1.0, 0.68, 0.52), (1.0, 0.95, 0.86), (0.55, 0.78, 1.0), (1.0, 0.62, 0.74), (0.6, 0.9, 0.78), (1.0, 0.86, 0.42), (0.78, 0.68, 1.0)
PARTS = {}


def lathe_z(profile, seg, rgb_fn):
    """绕 Unity z 轴车削（profile: [(r, z)]），按 z 给顶点色"""
    bm = P.lathe(profile, seg)
    lay = bm.loops.layers.color.new('Col')
    for f in bm.faces:
        for l in f.loops:
            c = rgb_fn(l.vert.co.z); l[lay] = (c[0], c[1], c[2], 1.0)
    for v in bm.verts: v.co = U(v.co.x, v.co.y, v.co.z)        # 车削轴（Blender z）→ Unity z
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def dart():
    """泡沫软弹（用户：敌人的子弹要好看，不要一个圆球）。迎面飞来时玩家看到的主要是正面——
    所以正面要能一眼认出“软弹”：内凹吸盘碗口（天蓝唇边 + 柠檬黄碗心 + 奶油小圆点）+ 尾部 4 片张开的尾翼（正面看是十字星），
    侧面看是桃色泡沫弹身（两道压痕）。比旧版大一号（长 8.6 cm）。"""
    L, r = 0.086, 0.0115
    h = L / 2
    prof = [(0, -h - 0.002), (r * 0.6, -h - 0.002), (r * 0.92, -h), (r, -h + 0.003)]
    for z0 in (-0.016, 0.01):                                   # 两道浅槽（泡沫弹的压痕）
        prof += [(r, z0 - 0.0016), (r * 0.9, z0), (r, z0 + 0.0016)]
    prof += [(r, h - 0.002), (r * 0.96, h), (r * 1.05, h + 0.003), (r * 1.45, h + 0.009), (r * 1.72, h + 0.0135),   # 吸盘外壁往外张
             (r * 1.74, h + 0.0158), (r * 1.6, h + 0.0168), (r * 1.4, h + 0.0158),                                  # 圆润的唇边
             (r * 1.0, h + 0.0128), (r * 0.55, h + 0.0108), (r * 0.25, h + 0.0118), (0, h + 0.0124)]             # 内凹碗心 + 中间一颗小圆点
    def col(rr, z, nz):
        if z > h + 0.0005:
            if nz < 0.25 or rr > r * 1.38: return BLUE              # 唇边和外壁（法线不朝前的都是外壁）
            if rr < r * 0.32: return CREAM                         # 碗心小圆点
            return YEL                                              # 碗心
        if z < -h + 0.004: return CREAM                            # 尾端奶油环
        return PEACH
    bm = P.lathe(prof, 28)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    lay = bm.loops.layers.color.new('Col')
    for f in bm.faces:
        cen = f.calc_center_median(); c = col(math.hypot(cen.x, cen.y), cen.z, f.normal.z)
        for l in f.loops: l[lay] = (c[0], c[1], c[2], 1.0)
    # 尾翼 ×4：薄片（径向 0.6r → 2.5r，尾端最宽、往前收成弧形），珊瑚粉 / 奶油间隔
    outline = [(r * 0.6, -h + 0.034), (r * 0.6, -h - 0.001)]
    for i in range(7):                                            # 外缘：从尾端外角弯回前端
        t = i / 6
        outline.append((r * (2.5 - 1.6 * t * t), -h - 0.001 + 0.035 * t))
    # 顺序：(内前) → (内后) → 外缘(后→前)；法线最后统一重算
    for k in range(4):
        th = k / 4 * math.tau + math.pi / 4
        fin = P.slab(outline, 0.0022)
        cs, sn = math.cos(th), math.sin(th)
        for v in fin.verts:
            a, b_, c = v.co.x, v.co.y, v.co.z
            v.co = Vector((a * cs - c * sn, a * sn + c * cs, b_))
        bmesh.ops.recalc_face_normals(fin, faces=fin.faces)
        P.merge(bm, color_bm(fin, PINK if k % 2 == 0 else CREAM))
    for v in bm.verts: v.co = U(v.co.x, v.co.y, v.co.z)        # 车削轴（Blender z）→ Unity z（+Z 朝前）
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def board():
    R, T = 0.15, 0.024
    # 靶盘：圆饼（前后两面微鼓），按半径分环上色
    rings = [(0.035, YEL), (0.07, MINT), (0.105, CREAM), (R, PINK)]
    prof = [(0, -T / 2 - 0.002)]
    rs = [0.035, 0.07, 0.105, R - 0.012]
    for rr in rs: prof.append((rr, -T / 2 - 0.002 * (1 - rr / R)))
    prof += [(R - 0.004, -T / 2 + 0.001), (R, -T / 2 + 0.005), (R, T / 2 - 0.005), (R - 0.004, T / 2 - 0.001)]
    for rr in reversed(rs): prof.append((rr, T / 2 + 0.002 * (1 - rr / R)))
    prof.append((0, T / 2 + 0.002))
    bm = P.lathe(prof, 48)
    lay = bm.loops.layers.color.new('Col')
    for f in bm.faces:
        c = f.calc_center_median(); rr = math.hypot(c.x, c.y)
        col = next(cc for lim, cc in rings if rr <= lim + 1e-4) if rr <= R else PINK
        for l in f.loops: l[lay] = (col[0], col[1], col[2], 1.0)
    for v in bm.verts: v.co = U(v.co.x, v.co.y, v.co.z)        # 靶面朝 +Z（朝玩家时由 Unity 转向）
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # 奶油镶边环
    ring = P.torus(R, 0.0075, seg=56, sides=10)
    for v in ring.verts: v.co = U(v.co.x, v.co.y, v.co.z)
    P.merge(bm, color_bm(ring, CREAM))
    # 顶部小挂环（绳子拴在这里）
    hook = P.torus(0.01, 0.003, seg=16, sides=8)
    for v in hook.verts: v.co = U(v.co.x, R + 0.012 + v.co.y, v.co.z)
    P.merge(bm, color_bm(hook, CREAM))
    PARTS['Target_Hook'] = (0.0, R + 0.02, 0.0)
    return bm


def balloon():
    # 绳子：从原点（挂环）往上 0.22 m，略微弯一点（不是笔直的棍）
    pts = [U(0.004 * math.sin(t * math.pi), 0.22 * t, 0.003 * math.sin(t * 2.2)) for t in [i / 10 for i in range(11)]]
    bm = color_bm(P.tube(pts, 0.0012, seg=6), CREAM)
    # 气球：水滴形车削（下尖上圆）+ 底部打结
    H, R = 0.2, 0.085
    prof = [(0, 0)]
    for i in range(1, 16):
        t = i / 16; ang = t * math.pi
        rr = R * math.sin(ang) * (0.82 + 0.18 * t)
        prof.append((rr, H * (1 - math.cos(ang)) / 2))
    prof.append((0, H))
    b = P.lathe(prof, 28)
    for v in b.verts: v.co = Vector((v.co.x, v.co.y, v.co.z + 0.232))   # Blender z = Unity y（向上）
    P.merge(bm, color_bm(b, LAV))
    knot = P.lathe([(0, 0.214), (0.006, 0.218), (0.007, 0.226), (0.004, 0.233), (0, 0.234)], 12)
    P.merge(bm, color_bm(knot, LAV))
    return bm


def main():
    P.reset()
    for n, fn in (('Dart_Foam', dart), ('Target_Board', board), ('Target_Balloon', balloon)):
        BP.export(n, fn(), 40)
    json.dump({k: list(v) for k, v in PARTS.items()}, open(os.path.join(OUT, 'Play_parts.json'), 'w'), indent=1)
    print('PLAY', json.dumps(P.report, ensure_ascii=False))


if __name__ == '__main__':
    main()
