"""积木球（投石车抛的那颗）：按设计图 ArtSource/Refs/P_Ball 程序建模。
立方体投影成球面，每面 6×6 格，随机拼成 2×2 / 2×1 / 1×1 的弯曲积木块（块间留细缝、边缘倒角），
每格一个带压印环的凸点；里面一颗深色内芯，从缝里看不穿。每块都是独立封闭流形。
用法：Blender -b -P Tools/blender_blockball.py -- <输出目录>
"""
import bpy, bmesh, math, os, sys, random
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proc_lib as P
import blender_portals as BP

OUT = sys.argv[sys.argv.index('--') + 1]
BP.OUT = OUT
rnd = random.Random(20261031)
color_bm = BP.color_bm

R = 0.09                 # 积木顶面半径（直径 0.18，和旧球一致）
T = 0.016                # 积木厚度
N = 6                    # 每面格数
GAP = 0.05               # 块间缝（占一格的比例）
PAL = [(1.0, 0.66, 0.74), (0.62, 0.9, 0.78), (1.0, 0.88, 0.5), (0.8, 0.7, 1.0)]
CORE = (0.42, 0.36, 0.48)

# 立方体 6 个面：法向 n，面内两个轴 a、b
FACES = [(Vector(n), Vector(a), Vector(b)) for n, a, b in (
    ((1, 0, 0), (0, 1, 0), (0, 0, 1)), ((-1, 0, 0), (0, 0, 1), (0, 1, 0)),
    ((0, 1, 0), (0, 0, 1), (1, 0, 0)), ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
    ((0, 0, 1), (1, 0, 0), (0, 1, 0)), ((0, 0, -1), (0, 1, 0), (1, 0, 0)))]


def sph(face, u, v):
    """面内坐标 u,v ∈ [-1,1] → 单位球面方向（等角投影，格子大小更均匀）"""
    n, a, b = face
    p = n + a * math.tan(u * math.pi / 4) + b * math.tan(v * math.pi / 4)
    return p.normalized()


def curved_brick(face, u0, u1, v0, v1, rgb, k=4):
    """球面上的弯曲积木块：顶面半径 R、底面 R-T，(k+1)×(k+1) 采样，六面封闭"""
    bm = bmesh.new()
    def grid(rad):
        return [[bm.verts.new(sph(face, u0 + (u1 - u0) * i / k, v0 + (v1 - v0) * j / k) * rad) for j in range(k + 1)] for i in range(k + 1)]
    top, bot = grid(R), grid(R - T)
    for i in range(k):
        for j in range(k):
            bm.faces.new((top[i][j], top[i + 1][j], top[i + 1][j + 1], top[i][j + 1]))
            bm.faces.new((bot[i][j], bot[i][j + 1], bot[i + 1][j + 1], bot[i + 1][j]))
    ring = [(i, 0) for i in range(k)] + [(k, j) for j in range(k)] + [(i, k) for i in range(k, 0, -1)] + [(0, j) for j in range(k, 0, -1)]
    for s in range(len(ring)):
        (i0, j0), (i1, j1) = ring[s], ring[(s + 1) % len(ring)]
        bm.faces.new((top[i0][j0], bot[i0][j0], bot[i1][j1], top[i1][j1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    sharp = [e for e in bm.edges if e.calc_face_angle(0) > math.radians(40)]
    bmesh.ops.bevel(bm, geom=sharp, offset=0.0018, segments=1, affect='EDGES', profile=0.5)
    return color_bm(bm, rgb)


def stud(d, r, h, rgb):
    """沿方向 d 立在积木顶面上的凸点：圆柱（顶边倒角）+ 顶面压印细环"""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=r, radius2=r, depth=h + 0.002)
    top = [e for e in bm.edges if all(v.co.z > (h + 0.002) / 2 - 1e-6 for v in e.verts)]
    bmesh.ops.bevel(bm, geom=top, offset=r * 0.2, segments=2, affect='EDGES', profile=0.5)
    q = Vector((0, 0, 1)).rotation_difference(d)
    base = d * R
    for v in bm.verts:
        v.co = q @ (v.co + Vector((0, 0, (h + 0.002) / 2 - 0.002))) + base
    color_bm(bm, rgb)
    ring = P.torus(r * 0.6, r * 0.08, seg=10, sides=4)
    for v in ring.verts:
        v.co = q @ (v.co + Vector((0, 0, h - r * 0.02))) + base
    P.merge(bm, color_bm(ring, tuple(min(1.0, c * 1.07) for c in rgb)))
    return bm


def tile():
    """把 N×N 格随机拼成积木块：优先 2×1 / 1×2，夹几块 2×2，剩下 1×1"""
    used = [[False] * N for _ in range(N)]
    blocks = []
    cells = [(i, j) for i in range(N) for j in range(N)]
    for i, j in cells:
        if used[i][j]: continue
        opts = []
        if i + 1 < N and j + 1 < N and not (used[i + 1][j] or used[i][j + 1] or used[i + 1][j + 1]): opts += [(2, 2)] * 2
        if i + 1 < N and not used[i + 1][j]: opts += [(2, 1)] * 3
        if j + 1 < N and not used[i][j + 1]: opts += [(1, 2)] * 3
        opts += [(1, 1)]
        w, h = rnd.choice(opts)
        for a in range(w):
            for b in range(h): used[i + a][j + b] = True
        blocks.append((i, j, w, h))
    return blocks


def build():
    ball = color_bm(P.lathe([(0, -(R - T * 0.6))] + [((R - T * 0.6) * math.sin(math.pi * k / 16), -(R - T * 0.6) * math.cos(math.pi * k / 16)) for k in range(1, 16)] + [(0, R - T * 0.6)], 32), CORE)
    last = None
    for face in FACES:
        for i, j, w, h in tile():
            rgb = rnd.choice([c for c in PAL if c != last]); last = rgb
            rgb = tuple(max(0, min(1, c + rnd.uniform(-0.02, 0.02))) for c in rgb)
            g = GAP
            u0 = -1 + 2 * (i + g) / N; u1 = -1 + 2 * (i + w - g) / N
            v0 = -1 + 2 * (j + g) / N; v1 = -1 + 2 * (j + h - g) / N
            P.merge(ball, curved_brick(face, u0, u1, v0, v1, rgb, k=2 + max(w, h)))
            for a in range(w):
                for b in range(h):
                    d = sph(face, -1 + 2 * (i + a + 0.5) / N, -1 + 2 * (j + b + 0.5) / N)
                    P.merge(ball, stud(d, 0.0062, 0.0048, rgb))
    return ball


def main():
    P.reset()
    os.makedirs(OUT, exist_ok=True)
    BP.export('BlockBall', build(), 35)
    print('BLOCKBALL', P.report)


if __name__ == '__main__':
    main()
