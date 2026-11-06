"""第 1 章「积木小镇」精细程序化部件（Blender 里运行）：
  Blender -b -P Tools/blender_w1_set.py -- <输出目录>

每个部件由若干封闭流形块组成（倒角积木、凸点、扫掠管），全部写顶点色（sRGB），导出前逐块检查
边界边 / 非流形边为 0（proc_lib.check）。坐标按 Unity 约定建：门面在 z=0，房间一侧 +z，门后世界在 -z。
  Portal_Bricks   糖果积木拱门（立柱错缝 + 楔形拱石 + 顶面凸点 + 底座）
  Portal_Rim      门洞内沿发光边条（自发光材质用）
  Portal_Debris   门口散落的小积木
  W1_Track        木头火车轨道（带两条轮槽，每节木色略有不同）
  W1_BrickPath    奶油色积木小路
  W1_Rainbow      六色粉彩彩虹
  W1_Ground       绒布草地（起伏山丘 + 颜色斑块）
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P

OUT = sys.argv[sys.argv.index('--') + 1]
os.makedirs(OUT, exist_ok=True)
U = P.U
rnd = random.Random(20261026)

PASTEL = [(0.98, 0.6, 0.76), (0.52, 0.87, 0.71), (1.0, 0.86, 0.45), (0.5, 0.74, 0.98), (0.71, 0.58, 0.98), (1.0, 0.68, 0.5)]
W, H = 1.3, 1.8                 # 门洞宽高（与规则层 portal.width/height 一致）
R_IN = W / 2                    # 拱顶内半径
SPRING = H - R_IN               # 起拱高度


def color_bm(bm, rgb):
    lay = bm.loops.layers.color.get('Col') or bm.loops.layers.color.new('Col')
    c = (rgb[0], rgb[1], rgb[2], 1.0)
    for f in bm.faces:
        for l in f.loops: l[lay] = c
    return bm


def jitter(rgb, k=0.04):
    return tuple(max(0, min(1, x + rnd.uniform(-k, k))) for x in rgb)


def stud(cx, cy, cz, r=0.034, h=0.022, up=Vector((0, 0, 1))):
    """积木凸点：圆柱 + 顶面倒角，沿 up 方向立在 (cx,cy,cz)"""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=16, radius1=r, radius2=r, depth=h)
    top = [e for e in bm.edges if all(v.co.z > h / 2 - 1e-6 for v in e.verts)]
    bmesh.ops.bevel(bm, geom=top, offset=r * 0.18, segments=2, affect='EDGES', profile=0.5)
    q = Vector((0, 0, 1)).rotation_difference(up.normalized())
    for v in bm.verts:
        v.co = q @ (v.co + Vector((0, 0, h / 2 - 0.004))) + Vector((cx, cy, cz))
    return bm


def brick_u(x0, x1, y0, y1, z0, z1, rgb, studs=True, bevel=0.012):
    """Unity 坐标范围的倒角积木，顶面（+y）按 8cm 网格放凸点"""
    bm = P.ubox(x0, x1, y0, y1, z0, z1, bevel)
    color_bm(bm, rgb)
    if studs:
        nx = max(1, round(abs(x1 - x0) / 0.12)); nz = max(1, round(abs(z1 - z0) / 0.12))
        for i in range(nx):
            for k in range(nz):
                ux = x0 + (i + 0.5) * (x1 - x0) / nx; uz = z0 + (k + 0.5) * (z1 - z0) / nz
                s = stud(*U(ux, y1, uz)); color_bm(s, rgb); P.merge(bm, s)
    return bm


# ───────────────────────── 门框 ─────────────────────────
def portal_bricks():
    bm = bmesh.new()
    t = 0.34                    # 门框进深
    bw = 0.32                   # 立柱宽
    rows = 6
    rh = SPRING / rows
    back = 0.05                 # 奇数行往后错，露出下层前沿的一排凸点（手搭积木感）
    ci = 0
    for side in (-1, 1):
        xin = side * R_IN; xout = side * (R_IN + bw)
        for r in range(rows):
            y0, y1 = r * rh + 0.003, (r + 1) * rh - 0.003
            dz = -back if r % 2 else 0.0
            jx = rnd.uniform(-0.008, 0.008)
            z0, z1 = -t / 2 + dz, t / 2 + dz
            if r % 2 == 0:
                rgb = jitter(PASTEL[ci % 6]); ci += 1
                P.merge(bm, brick_u(min(xin, xout) + jx, max(xin, xout) + jx, y0, y1, z0, z1, rgb, studs=False))
                # 露出的前沿凸点（上一行往后错了 back）
                if r + 1 < rows:
                    for i in range(3):
                        ux = xin + side * (0.06 + i * 0.1)
                        st = stud(*U(ux, y1, t / 2 - back / 2), r=0.03, h=0.02); color_bm(st, rgb); P.merge(bm, st)
            else:                # 错缝：两块半砖
                xm = side * (R_IN + bw * 0.45)
                for a_, b_ in ((xin, xm), (xm, xout)):
                    rgb = jitter(PASTEL[ci % 6]); ci += 1
                    P.merge(bm, brick_u(min(a_, b_) + 0.003 + jx, max(a_, b_) - 0.003 + jx, y0, y1, z0, z1, rgb, studs=False))
        # 底座：向前伸出的宽积木，顶面有凸点
        xb0, xb1 = sorted((side * (R_IN - 0.04), side * (R_IN + bw + 0.22)))
        P.merge(bm, brick_u(xb0, xb1, -0.002, 0.11, -t / 2 - 0.06, t / 2 + 0.22, jitter(PASTEL[(ci + 2) % 6]), studs=True)); ci += 1
        # 立柱外侧再靠一摞小积木（2 层），丰富轮廓
        xs0, xs1 = sorted((side * (R_IN + bw + 0.02), side * (R_IN + bw + 0.2)))
        for lv in range(2):
            rgb = jitter(PASTEL[(ci + lv) % 6])
            P.merge(bm, brick_u(xs0, xs1, 0.11 + lv * 0.12, 0.11 + (lv + 1) * 0.12 - 0.004, -0.12, 0.08, rgb, studs=(lv == 1)))
        ci += 2
    # 拱石：9 块楔形积木，外弧上 2×3 的凸点阵（沿径向立起）
    n = 9
    r_out = R_IN + bw
    for k in range(n):
        a0 = math.pi - k * math.pi / n - 0.004
        a1 = math.pi - (k + 1) * math.pi / n + 0.004
        part = bmesh.new()
        steps = 5
        rr_out = r_out + rnd.uniform(-0.012, 0.012)
        verts = []
        for s_ in range(steps + 1):
            a = a0 + (a1 - a0) * s_ / steps
            for rr in (R_IN, rr_out):
                for z in (-t / 2, t / 2):
                    verts.append(U(math.cos(a) * rr, SPRING + math.sin(a) * rr, z))
        vs = [part.verts.new(v) for v in verts]
        def V(s_, ri, zi): return vs[s_ * 4 + ri * 2 + zi]
        for s_ in range(steps):
            part.faces.new((V(s_, 0, 0), V(s_ + 1, 0, 0), V(s_ + 1, 0, 1), V(s_, 0, 1)))
            part.faces.new((V(s_, 1, 0), V(s_, 1, 1), V(s_ + 1, 1, 1), V(s_ + 1, 1, 0)))
            part.faces.new((V(s_, 0, 1), V(s_ + 1, 0, 1), V(s_ + 1, 1, 1), V(s_, 1, 1)))
            part.faces.new((V(s_, 0, 0), V(s_, 1, 0), V(s_ + 1, 1, 0), V(s_ + 1, 0, 0)))
        part.faces.new((V(0, 0, 0), V(0, 0, 1), V(0, 1, 1), V(0, 1, 0)))
        part.faces.new((V(steps, 0, 0), V(steps, 1, 0), V(steps, 1, 1), V(steps, 0, 1)))
        bmesh.ops.recalc_face_normals(part, faces=part.faces)
        bmesh.ops.bevel(part, geom=part.edges[:], offset=0.012, segments=3, affect='EDGES', profile=0.5, clamp_overlap=True)
        rgb = jitter(PASTEL[(k + 3) % 6])
        color_bm(part, rgb)
        for f in (0.25, 0.75):
            a = a0 + (a1 - a0) * f
            for z in (-0.1, 0.0, 0.1):
                p = U(math.cos(a) * rr_out, SPRING + math.sin(a) * rr_out, z)
                up = U(math.cos(a), math.sin(a), 0)
                s2 = stud(p.x, p.y, p.z, r=0.042, h=0.026, up=up); color_bm(s2, rgb); P.merge(part, s2)
        P.merge(bm, part)
    return bm


def portal_rim():
    """门洞内沿的发光边条：沿门洞轮廓（两侧竖边 + 半圆拱）扫圆管，略在门面前方"""
    pts = []
    for i in range(10):
        pts.append((-R_IN + 0.02, i / 9 * SPRING))
    for i in range(1, 32):
        a = math.pi - i / 32 * math.pi
        pts.append((math.cos(a) * (R_IN - 0.02), SPRING + math.sin(a) * (R_IN - 0.02)))
    for i in range(10):
        pts.append((R_IN - 0.02, SPRING - i / 9 * SPRING))
    bm = P.tube([U(x, y, 0.16) for x, y in pts], 0.011, seg=10)
    return color_bm(bm, (1, 0.9, 0.72))


def portal_debris():
    bm = bmesh.new()
    spots = [(-0.55, 0.42, 0.4), (-0.25, 0.55, 1.2), (0.35, 0.48, 2.3), (0.62, 0.36, 0.7), (0.1, 0.7, 2.9), (-0.8, 0.62, 1.9)]
    for i, (x, z, rot) in enumerate(spots):
        sx = rnd.choice([0.08, 0.12, 0.16]); sz = 0.08
        part = brick_u(-sx / 2, sx / 2, 0, 0.05, -sz / 2, sz / 2, jitter(PASTEL[i % 6]), studs=True, bevel=0.006)
        m = Matrix.Translation(U(x, 0, z)) @ Matrix.Rotation(rot, 4, 'Z')
        bmesh.ops.transform(part, matrix=m, verts=part.verts)
        P.merge(bm, part)
    return bm


# ───────────────────────── 轨道 ─────────────────────────
TRACK_C = (0.25, -3.1)           # 门后世界里椭圆中心 (x, z)
TRACK_A, TRACK_B = 1.65, 1.0


def track_point(u):
    a = u * math.tau
    return (TRACK_C[0] + math.cos(a) * TRACK_A, TRACK_C[1] + math.sin(a) * TRACK_B)


def w1_track():
    prof = [(-0.08, 0), (0.08, 0), (0.08, 0.03), (0.074, 0.038), (0.06, 0.038), (0.06, 0.026), (0.036, 0.026), (0.036, 0.038),
            (-0.036, 0.038), (-0.036, 0.026), (-0.06, 0.026), (-0.06, 0.038), (-0.074, 0.038), (-0.08, 0.03)]
    N = 160
    bm = bmesh.new()
    lay = bm.loops.layers.color.new('Col')
    rings = []
    for i in range(N):
        u = i / N
        x, z = track_point(u)
        x2, z2 = track_point(u + 1e-3)
        tx, tz = x2 - x, z2 - z; l = math.hypot(tx, tz); tx, tz = tx / l, tz / l
        nx, nz = -tz, tx          # 侧向
        rings.append([bm.verts.new(U(x + nx * pu, 0.002 + pv, z + nz * pu)) for pu, pv in prof])
    seg_len = 0.32
    perim = sum(math.dist(track_point(i / N), track_point((i + 1) / N)) for i in range(N))
    acc = 0.0
    for i in range(N):
        a, b = rings[i], rings[(i + 1) % N]
        acc += math.dist(track_point(i / N), track_point((i + 1) / N))
        seg = int(acc / seg_len)
        base = (0.86, 0.68, 0.48) if seg % 2 == 0 else (0.80, 0.62, 0.42)
        for k in range(len(prof)):
            f = bm.faces.new((a[k], b[k], b[(k + 1) % len(prof)], a[(k + 1) % len(prof)]))
            groove = prof[k][1] < 0.03 and abs(prof[k][0]) < 0.07 and abs(prof[k][0]) > 0.03
            c = tuple(x * (0.78 if groove else 1.0) for x in base) + (1.0,)
            for l in f.loops: l[lay] = c
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


# ───────────────────────── 积木小路 ─────────────────────────
def w1_path():
    bm = bmesh.new()
    pts = [(0.0, -0.15), (0.12, -0.5), (0.05, -0.85), (-0.12, -1.2), (-0.05, -1.55), (0.1, -1.85)]
    creams = [(1.0, 0.89, 0.7), (1.0, 0.85, 0.68), (0.99, 0.9, 0.76)]   # 暖奶油色（太白在门洞里会发灰发飘）
    i = 0
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        for s in range(2):
            t = (s + 0.5) / 2
            x, z = x0 + (x1 - x0) * t, z0 + (z1 - z0) * t
            yaw = math.atan2(x1 - x0, z1 - z0) + rnd.uniform(-0.08, 0.08)
            for side in (-1, 1):
                if (i + side) % 5 == 0 and side == 1:
                    continue           # 偶尔缺一块，更像随手搭的
                part = brick_u(-0.11, 0.11, 0, 0.026, -0.075, 0.075, jitter(creams[i % 3], 0.02), studs=True, bevel=0.007)
                m = Matrix.Translation(U(x + side * 0.115 * math.cos(yaw), 0.0, z - side * 0.115 * math.sin(yaw))) @ Matrix.Rotation(-yaw, 4, 'Z')
                bmesh.ops.transform(part, matrix=m, verts=part.verts)
                P.merge(bm, part)
            i += 1
    return bm


# ───────────────────────── 彩虹 ─────────────────────────
def w1_rainbow():
    bm = bmesh.new()
    cx, cz, r0 = 0.6, -8.6, 3.0
    for k, rgb in enumerate(PASTEL[::-1]):
        r = r0 + k * 0.17
        pts = [U(cx + math.cos(math.pi * i / 48) * r, -0.35 + math.sin(math.pi * i / 48) * r, cz) for i in range(49)]
        part = P.tube(pts, 0.088, seg=12)
        color_bm(part, rgb)
        P.merge(bm, part)
    return bm


# ───────────────────────── 绒布草地 ─────────────────────────
def ground_height(x, z):
    """门后世界地面高度（Unity 局部坐标 x,z → y），W1_Ground 与散布植被共用"""
    d = math.hypot(x - 0.3, z + 3.0)
    h = 0.0
    h += 0.35 * math.exp(-((x + 2.6) ** 2 + (z + 5.4) ** 2) / 2.2)      # 左后小山
    h += 1.1 * math.exp(-((x - 3.6) ** 2 + (z + 7.2) ** 2) / 5.0)       # 右后大山
    h += 1.4 * math.exp(-((x + 1.0) ** 2 + (z + 10.0) ** 2) / 9.0)      # 远山
    # 外圈一环起伏的山丘，把小镇围起来、挡住地面边缘
    a = math.atan2(z + 3.0, x - 0.3)
    ring = math.exp(-((d - 8.5) ** 2) / 4.0) * (1.2 + 0.6 * math.sin(a * 3 + 0.7) + 0.4 * math.sin(a * 7 + 2.1))
    h += max(0.0, ring)
    h += 0.05 * math.sin(x * 1.7) * math.sin(z * 1.3)
    h *= min(1.0, max(0.0, (d - 1.2) / 1.5))                           # 小镇中心保持平坦
    return h

def w1_ground():
    bm = bmesh.new()
    lay = bm.loops.layers.color.new('Col')
    R, rings, segs = 11.0, 40, 72
    height = ground_height
    vs = [[None] * segs for _ in range(rings + 1)]
    center = bm.verts.new(U(0.3, height(0.3, -3.0), -3.0))
    for r in range(1, rings + 1):
        rad = R * (r / rings) ** 1.35
        for s in range(segs):
            a = s / segs * math.tau
            x, z = 0.3 + math.cos(a) * rad, -3.0 + math.sin(a) * rad
            vs[r][s] = bm.verts.new(U(x, height(x, z), z))
    def col(v):
        p = v.co
        n = 0.5 + 0.5 * math.sin(p.x * 0.9 + 1.3) * math.cos(p.y * 0.7)
        base = (0.42, 0.72, 0.36)
        light = (0.6, 0.84, 0.46)
        c = tuple(base[i] + (light[i] - base[i]) * n for i in range(3))
        return c + (1.0,)
    faces = []
    for s in range(segs):
        faces.append(bm.faces.new((center, vs[1][s], vs[1][(s + 1) % segs])))
    for r in range(1, rings):
        for s in range(segs):
            faces.append(bm.faces.new((vs[r][s], vs[r + 1][s], vs[r + 1][(s + 1) % segs], vs[r][(s + 1) % segs])))
    uvl = bm.loops.layers.uv.new('UVMap')
    for f in faces:
        for l in f.loops:
            l[lay] = col(l.vert)
            l[uvl].uv = (-l.vert.co.x * 1.6, -l.vert.co.y * 1.6)   # 平面投影：绒布纤维贴图（T_FeltGrain）按世界尺寸平铺
    # 底面封口 → 封闭体（地面块，从下面看不到）
    rim = vs[rings]
    bot = [bm.verts.new(Vector((v.co.x, v.co.y, -0.3))) for v in rim]
    for s in range(segs):
        f = bm.faces.new((rim[(s + 1) % segs], bot[(s + 1) % segs], bot[s], rim[s]))
        for l in f.loops: l[lay] = (0.5, 0.75, 0.45, 1)
    f = bm.faces.new(bot[::-1])
    for l in f.loops: l[lay] = (0.5, 0.75, 0.45, 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def main():
  P.reset()
  parts = {
      'Portal_Bricks': portal_bricks, 'Portal_Rim': portal_rim, 'Portal_Debris': portal_debris,
      'W1_Track': w1_track, 'W1_BrickPath': w1_path, 'W1_Rainbow': w1_rainbow, 'W1_Ground': w1_ground,
  }
  for name, fn in parts.items():
      bm = fn()
      ob = P.to_obj(bm, name, smooth=name not in ('Portal_Bricks', 'Portal_Debris', 'W1_BrickPath'))
      if name in ('Portal_Bricks', 'Portal_Debris', 'W1_BrickPath'):
          # 积木：按角度自动平滑（倒角圆润、平面硬朗）
          ob.data.shade_smooth()
          try:
              ob.data.set_sharp_from_angle(angle=math.radians(35))
          except Exception:
              pass
      fp = os.path.join(OUT, name + '.fbx')
      bm2 = bmesh.new(); bm2.from_mesh(ob.data); bmesh.ops.triangulate(bm2, faces=bm2.faces); bm2.to_mesh(ob.data); bm2.free()
      bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
      bpy.ops.export_scene.fbx(filepath=fp, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                               axis_forward='-Z', axis_up='Y', object_types={'MESH'}, mesh_smooth_type='FACE',
                               bake_space_transform=True, colors_type='SRGB')
      bpy.data.objects.remove(ob, do_unlink=True)
  print('W1SET', json.dumps(P.report, ensure_ascii=False))
  json.dump(P.report, open(os.path.join(OUT, 'w1set_report.json'), 'w'), ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
