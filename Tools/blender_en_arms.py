"""P2 人物的手臂（Blender 里运行）：
  Blender -b -P Tools/blender_en_arms.py -- Assets/_Bang/Art/Models/Proc/Enemies

手臂是球、管、袖口这类规整几何，P2 白模做不准（手套变成袖子侧面的小凸起），这里按 ArtSource/Refs/En_*Arm 的设计图精确建模。
原点在肩关节球心，手臂顺 -Y 垂下，手尖在 y = -总长；模型正面朝 +Z。左右分别导出（_L 在身体 +X 一侧）：拇指朝前偏内，纽扣朝外。
"""
import bmesh, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_enemies_more as M
import blender_portals as BP

bead, place, color_bm, add, box, star = M.bead, M.place, M.color_bm, M.add, M.box, M.star
BRASS = (0.8, 0.62, 0.36); COPPER = BRASS; SILVER = M.SILVER; WHITE = (0.96, 0.95, 0.93); GOLD = (0.95, 0.74, 0.3)
RED = (0.78, 0.2, 0.13); PINK = (0.93, 0.52, 0.63); YEL = (0.95, 0.82, 0.28); OLIVE = (0.45, 0.5, 0.3); MOSS = (0.34, 0.39, 0.23); SAND = (0.83, 0.71, 0.48)
BLUE = (0.33, 0.57, 0.8); MINT = (0.58, 0.85, 0.74); LAV = (0.77, 0.66, 0.9); PEACH = (0.98, 0.76, 0.62); BLACK = (0.12, 0.11, 0.14); TAPE = (0.74, 0.58, 0.38); LEATHER = (0.42, 0.26, 0.18)


def norm(bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def tube(r0, r1, y0, y1, rgb, seg=22, belly=0.0):
    """实心圆柱段（y0 在上），上下端面带圆角；belly = 中段鼓起"""
    e = min(0.02, abs(y0 - y1) * 0.25)
    prof = [(0, y0), (r0 * 0.72, y0), (r0 * 0.93, y0 - e * 0.3), (r0, y0 - e)]
    n = 6
    for i in range(1, n):
        t = i / n; prof.append(((r0 + (r1 - r0) * t) * (1 + belly * math.sin(t * math.pi)), y0 - e + (y1 - y0 + 2 * e) * t))
    prof += [(r1, y1 + e), (r1 * 0.93, y1 + e * 0.3), (r1 * 0.72, y1), (0, y1)]
    return norm(color_bm(P.lathe(prof, seg), rgb))


R = 0.15                                                             # 肩球半径（相对臂长）：要盖住身体模型自带的肩桩，按量出的桩子半径逐个定


def ball(r, y, rgb):
    return norm(place(bead(r, rgb, seg=22, rings=12), 0, y, 0))


def egg(rx, ry, rz, y, rgb, x=0.0, z=0.0, tilt=0.0):
    b = bead(1.0, rgb, seg=20, rings=12)
    bmesh.ops.scale(b, vec=(rx, rz, ry), verts=b.verts)                # Blender 坐标 (x, 深度, 高)
    return norm(place(b, x, y, z, rx=tilt))


def mitten(y, rgb, side):
    """手套：掌 + 朝前偏内的拇指"""
    return add(egg(0.1, 0.125, 0.082, y - 0.1, rgb), egg(0.04, 0.078, 0.04, y - 0.075, rgb, x=-side * 0.052, z=0.075, tilt=-24))


def fist(y, rgb, side):
    h = egg(0.15, 0.135, 0.13, y - 0.11, rgb)
    for i in range(4): add(h, egg(0.05, 0.042, 0.042, y - 0.185, rgb, x=(i - 1.5) * 0.077, z=0.09))
    return add(h, egg(0.05, 0.075, 0.05, y - 0.1, rgb, x=-side * 0.09, z=0.105, tilt=-40))


def buttons(bm, n, y, step, r, rgb, side, R):
    for i in range(n):
        yy = y - i * step
        add(bm, norm(place(bead(r, rgb, sz=0.7, seg=10, rings=6), side * R * 0.96, yy, 0, rz=-side * 90)))


def tin(s):
    a = add(ball(R, 0, BRASS), tube(0.085, 0.09, -0.07, -0.75, RED))
    add(a, tube(0.098, 0.098, -0.69, -0.725, GOLD)); buttons(a, 4, -0.45, 0.07, 0.015, GOLD, s, 0.09)
    return add(a, tube(0.07, 0.06, -0.73, -0.8, WHITE), mitten(-0.77, WHITE, s))


def planter(s):
    a = add(ball(R, 0, BRASS), tube(0.085, 0.092, -0.07, -0.68, PINK))
    return add(a, tube(0.112, 0.098, -0.66, -0.8, WHITE), ball(0.105, -0.895, YEL))


def trooper(s):
    a = add(ball(R, 0, BRASS), tube(0.082, 0.088, -0.07, -0.38, OLIVE), tube(0.095, 0.09, -0.35, -0.42, MOSS), tube(0.086, 0.09, -0.41, -0.72, OLIVE))
    add(a, norm(place(box(-0.04, 0.04, -0.06, 0.06, -0.012, 0.012, MOSS, 0.006), s * 0.09, -0.24, 0, ry=90 * s)))   # 袖口袋
    return add(a, tube(0.104, 0.096, -0.7, -0.8, SAND), mitten(-0.77, YEL, s))


def minion(s):
    a = add(ball(R, 0, SILVER), tube(0.082, 0.088, -0.07, -0.7, BLUE), tube(0.103, 0.1, -0.68, -0.78, SILVER))
    return add(a, tube(0.07, 0.06, -0.76, -0.8, WHITE), mitten(-0.77, WHITE, s))


def brute(s):
    a = add(ball(R, 0, PEACH), tube(0.15, 0.15, -0.1, -0.46, MINT, belly=0.1), tube(0.148, 0.142, -0.45, -0.78, LAV, belly=0.1))
    add(a, tube(0.158, 0.158, -0.445, -0.475, (0.98, 0.93, 0.85)))
    return add(a, tube(0.12, 0.11, -0.76, -0.82, PEACH), fist(-0.8, PEACH, s))


def ninja(s):
    a = add(ball(R, 0, COPPER), tube(0.08, 0.09, -0.07, -0.76, BLACK))
    for y, t in ((-0.38, 13), (-0.46, -11), (-0.54, 12)): add(a, norm(place(tube(0.098, 0.098, 0.022, -0.022, TAPE), 0, y, 0, rx=t)))
    add(a, mitten(-0.74, LEATHER, s))
    return add(a, norm(place(star(0.05, 0.022, 0.012, SILVER), 0, -0.84, 0.09)))


def bot(s):
    a = add(ball(R, 0, COPPER), tube(0.074, 0.074, -0.08, -0.4, MINT), tube(0.086, 0.086, -0.39, -0.43, SILVER))
    add(a, ball(0.082, -0.48, SILVER), tube(0.086, 0.086, -0.53, -0.56, SILVER), tube(0.074, 0.074, -0.55, -0.76, MINT), tube(0.05, 0.05, -0.75, -0.84, SILVER))
    add(a, norm(box(-0.022, 0.022, -0.93, -0.82, -0.016, 0.016, SILVER, 0.004)))                                       # 扳手
    return add(a, norm(box(-0.07, -0.018, -1.0, -0.91, -0.016, 0.016, SILVER, 0.004)), norm(box(0.018, 0.07, -1.0, -0.91, -0.016, 0.016, SILVER, 0.004)), norm(box(-0.07, 0.07, -0.935, -0.905, -0.016, 0.016, SILVER, 0.004)))


ARMS = {'Tin': (tin, 0.21, 0.155), 'Planter': (planter, 0.16, 0.19), 'Trooper': (trooper, 0.17, 0.17), 'Minion': (minion, 0.125, 0.165), 'Brute': (brute, 0.25, 0.26), 'Ninja': (ninja, 0.2, 0.175), 'Bot': (bot, 0.16, 0.19)}


def gear(R, teeth, thick, rgb):
    pts = []
    for i in range(teeth * 4):
        pts.append((lambda a, r: (math.cos(a) * r, math.sin(a) * r))(i / (teeth * 4) * math.tau, R if i % 4 in (0, 1) else R * 0.74))
    bm = P.slab(pts, thick)
    for v in bm.verts: v.co = M.U(v.co.x, v.co.y, v.co.z)
    return add(norm(color_bm(bm, rgb)), norm(place(M.L([(0, 0.008), (R * 0.3, 0.008), (R * 0.3, -0.008), (0, -0.008)], SILVER, 14), 0, 0, thick * 0.5, rx=90)))


def screw(rgb):
    a = M.L([(0, 0.02), (0.012, 0.02), (0.013, 0.012), (0.005, 0.01), (0.005, -0.024), (0, -0.03)], rgb, 12)
    for i in range(4): add(a, norm(place(color_bm(P.torus(0.0055, 0.0018, seg=12, sides=5), rgb), 0, 0.004 - i * 0.008, 0, rx=90)))
    return norm(a)


def spring(rgb):
    pts = [(math.cos(i * 0.75) * 0.014, 0.024 - i * 0.0052, math.sin(i * 0.75) * 0.014) for i in range(11)]
    return norm(M.tube_u(pts, 0.0028, rgb, seg=6))


# 敌人被打中 / 打死时迸出来的玩具小零件（放大一点，隔着 2～3 米也看得清）
P.reset()
BITS = {'Bit_Gear': gear(0.024, 8, 0.008, BRASS), 'Bit_Screw': screw(SILVER), 'Bit_Spring': spring(SILVER), 'Bit_Star': star(0.024, 0.011, 0.006, (0.98, 0.8, 0.3))}


def jack_lid():
    """小丑盒的盖子：宽 1、深 1 的单位尺寸，铰链在后沿（原点），向 +Z 伸出；Unity 里按盒子宽度缩放"""
    SKY = (0.42, 0.74, 0.9)
    return add(norm(box(-0.5, 0.5, 0.0, 0.07, 0.0, 1.0, SKY, 0.02)), norm(box(-0.52, 0.52, -0.02, 0.03, -0.02, 1.02, GOLD, 0.012)))


def jack_hole():
    """盒口：贴在盒顶的深色方板。P2 的盒顶是封死的，小丑穿过这块板时交线正好像盒口边缘"""
    return norm(box(-0.42, 0.42, 0.0, 0.012, -0.42, 0.42, (0.09, 0.07, 0.1), 0.004))


def bot_antenna():
    """机器人天线（P2 的弹簧丝太细，水密重建时丢了）：原点在头顶，弹簧 + 红球"""
    pts = [(math.cos(i * 0.7) * 0.012, 0.004 + i * 0.0016, math.sin(i * 0.7) * 0.012) for i in range(30)]
    return add(M.tube_u([(0, -0.01, 0), (0, 0.06, 0)], 0.0028, SILVER, seg=8), norm(M.tube_u(pts, 0.0022, SILVER, seg=6)), norm(place(bead(0.0115, (0.86, 0.14, 0.14), seg=16, rings=10), 0, 0.066, 0)))


def jack_box():
    """小丑盒盒身（原点在底面中心，真实尺寸：宽 0.27 深 0.2 高 0.285，正面 +Z）：P2 的盒身是碎薄板，重建后边缘发糊，这里精确建模"""
    W, D, H = 0.135, 0.1, 0.285; PINKB = (0.93, 0.5, 0.62); FRAME = (0.95, 0.76, 0.42); SKY = (0.42, 0.74, 0.9); PANEL = (0.97, 0.6, 0.7)
    a = norm(box(-W + 0.008, W - 0.008, 0.008, H - 0.04, -D + 0.008, D - 0.008, PINKB, 0.008))
    add(a, norm(box(-W, W, H - 0.05, H, -D, D, SKY, 0.01)), norm(box(-W, W, 0.0, 0.018, -D, D, FRAME, 0.005)), norm(box(-W, W, H - 0.056, H - 0.046, -D, D, FRAME, 0.003)))
    for sx in (-1, 1):
        for sz in (-1, 1):
            add(a, norm(box(sx * W - 0.012, sx * W + 0.012, 0.0, H - 0.05, sz * D - 0.012, sz * D + 0.012, FRAME, 0.005)))
            for i in range(9):
                y = 0.03 + i * 0.024
                add(a, norm(place(bead(0.0032, GOLD, sz=0.6, seg=8, rings=5), sx * (W - 0.004), y, sz * (D + 0.0125), rx=90)))
    add(a, norm(box(-W + 0.022, W - 0.022, 0.03, H - 0.07, D - 0.012, D + 0.003, PANEL, 0.005)), norm(box(-W + 0.022, W - 0.022, 0.03, H - 0.07, -D - 0.003, -D + 0.012, PANEL, 0.005)))
    # 圆润的大星星 + 脸
    pts = []
    for i in range(50):
        t = i / 50 * math.tau; r = 0.062 + 0.026 * math.cos(5 * (t - math.pi / 2))
        pts.append((math.cos(t) * r, math.sin(t) * r))
    st = P.slab(pts, 0.012)
    for v in st.verts: v.co = M.U(v.co.x, v.co.y + 0.125, v.co.z + D + 0.005)
    add(a, norm(color_bm(st, (0.99, 0.82, 0.26))))
    for sg in (-1, 1):
        add(a, norm(place(bead(0.0075, (0.1, 0.08, 0.1), sz=0.45, seg=12, rings=7), sg * 0.024, 0.135, D + 0.018, rx=90)), norm(place(bead(0.0025, WHITE, sz=0.5, seg=8, rings=5), sg * 0.024 + 0.0025, 0.138, D + 0.0212, rx=90)))
        add(a, norm(place(bead(0.008, (0.98, 0.62, 0.55), sz=0.2, seg=12, rings=5), sg * 0.038, 0.117, D + 0.0165, rx=90)))
    add(a, norm(M.tube_u([(math.sin(u) * 0.016, 0.116 - math.cos(u) * 0.009, D + 0.018) for u in [(-1.1 + 2.2 * i / 8) for i in range(9)]], 0.0022, (0.5, 0.2, 0.2), seg=6)))
    return a


def jack_crank():
    """摇柄：原点在盒身侧面的转轴，轴沿 -X 伸出（在盒子 -X 一侧）"""
    return add(norm(M.tube_u([(0.01, 0, 0), (-0.03, 0, 0)], 0.0055, SILVER, seg=10)), norm(M.tube_u([(-0.03, 0, 0), (-0.03, -0.042, 0)], 0.0045, SILVER, seg=10)), norm(M.tube_u([(-0.03, -0.042, 0), (-0.05, -0.042, 0)], 0.0045, SILVER, seg=10)), norm(place(bead(0.0115, (0.86, 0.14, 0.14), seg=16, rings=10), -0.058, -0.042, 0)))


BITS['Jack_BoxP'] = jack_box(); BITS['Jack_CrankP'] = jack_crank()
BITS['Jack_LidP'] = jack_lid(); BITS['Jack_HoleP'] = jack_hole()
BITS['Bot_Antenna'] = bot_antenna()


def main():
    global R
    for n, (fn, length, R) in ARMS.items():
        for sn, s in (('L', 1), ('R', -1)):
            bm = M.scaled(fn(s), length); BP.export(f'Arm_{n}_{sn}', bm, 40)
    for n, bm in BITS.items(): BP.export(n, bm, 40)
    bad = {k: v for k, v in P.report.items() if v['boundary'] or v['nonmanifold']}
    print('ARMS', len(P.report), '总面数', sum(v['faces'] for v in P.report.values()), '破面', bad)


if __name__ == '__main__':
    main()
