"""无限火力的长枪（Blender 里运行）：Blender -b -P Tools/blender_rifle.py -- Assets/_Bang/Art/Models/Proc/Gun
按设计图 ArtSource/Refs/Gun_RifleP2/image.png 逐部件建模（P2 的硬表面重建后发融，枪又贴在眼前，必须边缘干净）。
侧面轮廓用设计图的像素坐标写（640 宽），PX() 换算到米：握把顶部在原点，枪管沿 +Z，枪口高度与手枪接近。顶点色，封闭流形。"""
import bmesh, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_w1_set as W1
import blender_portals as BP
import blender_enemies_w1 as E
import blender_enemies_more as M

color_bm, bead, place, add, star = BP.color_bm, E.bead, E.place, M.add, M.star
S = 0.00098; OX, OY = 205.0, 290.0
PEACH = (0.97, 0.66, 0.52); PEACH_D = (0.86, 0.53, 0.42); SLOT = (0.55, 0.32, 0.3); MINT = (0.56, 0.86, 0.72); MINT_D = (0.42, 0.72, 0.6)
PINK = (0.93, 0.5, 0.56); YEL = (0.98, 0.82, 0.3); LENS = (0.55, 0.82, 0.95); RED = (0.9, 0.2, 0.25)


def PX(px, py): return (px - OX) * S, -(py - OY) * S       # 返回 (z, y)


def prism(pts, th, rgb, bevel=0.0018, seg=2):
    """设计图侧面轮廓（像素）挤出成厚 th（米）的零件，边缘倒圆"""
    bm = bmesh.new(); top = []; bot = []
    for px, py in pts:
        z, y = PX(px, py)
        top.append(bm.verts.new(P.U(th / 2, y, z))); bot.append(bm.verts.new(P.U(-th / 2, y, z)))
    bm.faces.new(top); bm.faces.new(list(reversed(bot)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n; bm.faces.new((top[j], top[i], bot[i], bot[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type='OFFSET', segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return color_bm(bm, rgb)


def rect(x0, y0, x1, y1): return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def cyl(px0, px1, py, r, rgb, seg=20):
    """沿枪管方向的圆柱段，两端小倒角"""
    z0, y = PX(px0, py); z1, _ = PX(px1, py); e = min(0.0016, (z1 - z0) * 0.2)
    bm = M.L([(0, z1), (r * 0.9, z1), (r, z1 - e), (r, z0 + e), (r * 0.9, z0), (0, z0)], rgb, seg)     # 轮廓沿局部 Y
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return place(bm, 0, y, 0, rx=90)


def rifle():
    a = cyl(40, 176, 252, 0.0136, PEACH)                                                                   # 枪托管（圆管）
    for x in (60, 76, 92):
        add(a, cyl(x, x + 7, 252, 0.0146, PEACH_D))
    add(a, cyl(160, 180, 252, 0.0176, PEACH_D))                                                            # 缓冲管螺母
    add(a, prism([(15, 235), (45, 235), (46, 392), (20, 392), (15, 382)], 0.03, PEACH, 0.003))            # 托板
    add(a, prism([(44, 366), (166, 258), (174, 268), (48, 384)], 0.016, PEACH))                            # 斜撑
    add(a, prism(rect(21, 262, 30, 300), 0.0308, SLOT, 0.001), prism(rect(21, 320, 30, 358), 0.0308, SLOT, 0.001))   # 托板上的减重槽
    add(a, prism([(168, 222), (335, 218), (335, 282), (322, 296), (182, 296), (168, 280)], 0.038, PEACH, 0.0034, 3))   # 上机匣（中段最宽）
    add(a, prism([(172, 212), (335, 210), (335, 226), (172, 228)], 0.03, PEACH, 0.003, 3), prism([(186, 292), (322, 292), (318, 303), (190, 303)], 0.03, PEACH_D, 0.0024, 3))
    add(a, prism([(182, 296), (322, 296), (328, 330), (318, 362), (285, 362), (270, 330), (232, 322), (205, 326), (188, 318)], 0.03, PEACH, 0.0026))   # 下机匣
    add(a, prism(rect(330, 226, 502, 274), 0.04, PEACH, 0.004, 3))                                         # 护木：三层叠出八角截面
    add(a, prism(rect(330, 212, 502, 230), 0.03, PEACH, 0.0034, 3), prism(rect(330, 270, 502, 288), 0.03, PEACH, 0.0034, 3))
    for i in range(5):                                                                                     # 护木散热槽（两排）
        x = 370 + i * 26
        for y0 in (232, 262):
            add(a, prism([(x + 3, y0), (x + 18, y0), (x + 21, y0 + 4), (x + 18, y0 + 8), (x + 3, y0 + 8), (x, y0 + 4)], 0.0406, SLOT, 0.0))
    for x in list(range(176, 330, 13)) + list(range(338, 498, 13)):                                        # 顶部导轨齿
        add(a, prism(rect(x, 206, x + 8, 216), 0.021, PEACH_D, 0.0009, 1))
    add(a, prism(rect(170, 212, 498, 217), 0.022, PEACH_D, 0.0008, 1))
    add(a, cyl(498, 582, 253, 0.0115, PEACH), cyl(490, 512, 253, 0.0165, PEACH_D))                         # 枪管、导气座
    add(a, cyl(574, 626, 253, 0.0172, PINK))                                                               # 消焰器
    for x in (586, 600, 613):
        add(a, cyl(x, x + 6, 253, 0.0178, (0.66, 0.3, 0.36)))
    add(a, prism([(183, 326), (232, 324), (222, 382), (206, 456), (166, 434), (178, 380)], 0.03, PEACH, 0.0034))      # 握把
    add(a, prism([(193, 338), (222, 336), (212, 384), (199, 440), (174, 426), (186, 380)], 0.0312, MINT, 0.0012))     # 握把防滑片
    for i in range(7):
        for k in range(3):
            px, py = 196 + k * 8 - i * 2.2, 350 + i * 12
            z, y = PX(px, py)
            for sg in (-1, 1): add(a, place(bead(0.0013, MINT_D, sz=0.6, seg=6, rings=4), sg * 0.0156, y, z, rz=90))
    pts = [PX(250 + math.cos(t) * 26, 346 + math.sin(t) * 24) for t in [(-0.2 + i * 0.32) for i in range(11)]]
    add(a, M.tube_u([(0, y, z) for z, y in pts], 0.0032, PEACH, seg=8))                                    # 扳机护圈
    add(a, prism([(246, 320), (256, 320), (252, 352), (245, 350)], 0.009, MINT, 0.001))                    # 扳机
    # ── 弹匣（用户：长枪要精细，尤其弹匣）：沿一条往前弯的中心线生成香蕉形轮廓（不是折线拼的板），
    #    侧面三条顺着弧度的纵向加强棱、底部加厚的托底板（带防滑凹点）、顶上进弹口压进弹匣井；弹匣井外沿加一圈喇叭口
    c0 = (298.0, 352.0); Lm = 196.0; n = 24
    cl = []; x, y = c0; ph0, ph1 = 0.16, 0.66
    for i in range(n + 1):
        sv = i / n; ph = ph0 + (ph1 - ph0) * sv * sv
        cl.append((x, y, ph))
        x += math.sin(ph) * Lm / n; y += math.cos(ph) * Lm / n
    def edge(side, sv_lo=0.0, sv_hi=1.0, grow=0.0):
        out = []
        for i in range(n + 1):
            sv = i / n
            if sv < sv_lo - 1e-6 or sv > sv_hi + 1e-6: continue
            x, y, ph = cl[i]; w = 27.0 - 4.0 * sv + grow
            out.append((x - side * math.cos(ph) * w, y + side * math.sin(ph) * w))     # 法线：(cos, -sin) 指向后方
        return out
    back, front = edge(1), edge(-1)
    add(a, prism(back + list(reversed(front)), 0.026, PEACH, 0.0032, 3))                                  # 弹匣主体
    fb = edge(1, 0.9, 1.0, 3.5); ff = edge(-1, 0.9, 1.0, 3.5)
    xe, ye, phe = cl[-1]; tail = [(px + math.sin(phe) * 7, py + math.cos(phe) * 7) for px, py in (fb[-1], ff[-1])]
    add(a, prism(fb + [tail[0], tail[1]] + list(reversed(ff)), 0.0305, PEACH_D, 0.0028, 3))              # 托底板（比弹体宽一圈、更厚）
    for k in (-1, 0, 1):                                                                                   # 托底板侧面的防滑凹点
        x, y, ph = cl[-2]; bx, by = x + math.sin(ph) * 10 + math.cos(ph) * 12 * k, y + math.cos(ph) * 10 - math.sin(ph) * 12 * k
        z, yy = PX(bx, by)
        for sg in (-1, 1): add(a, place(bead(0.0017, SLOT, sz=0.5, seg=8, rings=4), sg * 0.0153, yy, z, rz=90))
    for off in (-14.0, 0.0, 14.0):                                                                         # 三条纵向加强棱：顺着弧度走
        pts = []
        for i in range(n + 1):
            sv = i / n
            if sv < 0.16 or sv > 0.86: continue
            x, y, ph = cl[i]; px, py = x - math.cos(ph) * off, y + math.sin(ph) * off
            pts.append(PX(px, py))
        for sg in (-1, 1):
            add(a, M.tube_u([(sg * 0.0131, yy, z) for z, yy in pts], 0.0021, PEACH_D, seg=8))
    add(a, prism([(262, 342), (334, 338), (338, 376), (264, 380)], 0.033, PEACH_D, 0.0026, 3))             # 弹匣井（喇叭口外沿）
    add(a, prism([(268, 346), (328, 343), (331, 372), (270, 375)], 0.0336, PEACH, 0.0018, 2))
    z, yy = PX(316, 318)                                                                                   # 弹匣释放钮（右侧，压在下机匣前沿内）
    add(a, place(M.L([(0, 0.0016), (0.0048, 0.0016), (0.0056, 0.0006), (0.0056, -0.0016), (0, -0.0016)], MINT, 16), -0.0166, yy, z, rz=90))
    add(a, prism([(268, 300), (296, 300), (296, 312), (268, 312)], 0.0322, PEACH_D, 0.0012, 2))            # 空仓挂机柄（左侧凸片）
    # 拉机柄（上机匣尾端的 T 形柄）+ 辅助推进器（右侧圆钮）
    add(a, prism([(150, 214), (176, 214), (176, 226), (150, 226)], 0.024, PEACH_D, 0.002, 2))
    add(a, prism([(142, 210), (156, 210), (156, 230), (142, 230)], 0.04, PEACH_D, 0.0024, 2))
    z, yy = PX(318, 246)
    add(a, place(M.L([(0, 0.006), (0.0062, 0.006), (0.0072, 0.004), (0.0072, -0.006), (0, -0.006)], PEACH_D, 18), -0.0215, yy, z, rz=90))
    # 枪托：托底减震垫（深一档、带横纹）+ 背带孔
    add(a, prism([(9, 238), (20, 238), (20, 390), (9, 390)], 0.032, PEACH_D, 0.0024, 2))
    for i in range(9):
        add(a, prism(rect(7, 246 + i * 16, 11, 252 + i * 16), 0.0326, SLOT, 0.0006, 1))
    for py in (250, 372):
        z, yy = PX(33, py)
        add(a, place(M.L([(0, 0.0164), (0.0042, 0.0164), (0.0042, -0.0164), (0, -0.0164)], SLOT, 16), 0, yy, z, rz=90))
    add(a, prism(rect(248, 200, 324, 214), 0.026, MINT_D, 0.0018, 3))                                      # 瞄具座
    zc, yc = PX(290, 172)                                                                                  # 红点镜：圆筒朝向玩家，第一人称能看到镜片和红点
    ring = place(color_bm(P.torus(0.0215, 0.0052, seg=28, sides=10), MINT), 0, yc, zc, rx=90)
    ring2 = place(color_bm(P.torus(0.0215, 0.0052, seg=28, sides=10), MINT), 0, yc, zc + 0.03, rx=90)
    add(a, ring, ring2, cyl(275, 305, 172, 0.0228, MINT, 28), prism([(262, 186), (318, 186), (322, 204), (258, 204)], 0.026, MINT, 0.003, 3))
    add(a, place(M.L([(0, 0.0009), (0.0205, 0.0009), (0.0205, -0.0009), (0, -0.0009)], LENS, 28), 0, yc, zc - 0.0155, rx=90))
    add(a, place(bead(0.0022, RED, sz=0.6, seg=10, rings=6), 0, yc, zc - 0.0168, rx=90))
    add(a, prism(rect(262, 234, 322, 252), 0.0386, SLOT, 0.0008, 1))                                       # 抛壳窗
    add(a, prism([(258, 260), (324, 258), (324, 290), (258, 292)], 0.0396, MINT, 0.0014, 3))                  # 防尘盖
    for i in range(10):
        for k in range(3):
            z, y = PX(265 + i * 6, 266 + k * 8)
            for sg in (-1, 1): add(a, place(bead(0.0012, MINT_D, sz=0.6, seg=6, rings=4), sg * 0.0199, y, z, rz=90))
    for px, py in ((186, 245), (186, 280), (345, 250), (490, 250), (300, 320), (240, 305), (200, 308)):    # 销钉和螺丝
        z, y = PX(px, py)
        for sg in (-1, 1):
            add(a, place(M.L([(0, 0.0012), (0.0044, 0.0012), (0.0052, 0), (0.0052, -0.0012), (0, -0.0012)], PEACH_D, 12), sg * 0.0202 if py < 300 else sg * 0.0158, y, z, rz=90))
    for sg in (-1, 1):                                                                                     # 机匣上的贴纸
        z, y = PX(310, 330)
        s1 = star(0.0075, 0.0034, 0.0012, YEL); add(a, place(s1, sg * 0.0156, y, z, ry=90))
        z, y = PX(290, 340)
        s2 = star(0.0062, 0.0028, 0.0012, YEL); add(a, place(s2, sg * 0.0156, y, z, ry=90))
        z, y = PX(203, 395)
        s3 = star(0.0066, 0.003, 0.0012, YEL); add(a, place(s3, sg * 0.0158, y, z, ry=90))
    return a


def main():
    P.reset()
    BP.export('Gun_Rifle', rifle(), 35)
    print('RIFLE', P.report)


if __name__ == '__main__':
    main()
