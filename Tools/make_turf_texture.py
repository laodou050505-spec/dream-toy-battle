"""草地绒草贴图 T_TurfGrain.png（乘在地面顶点色上）：python3 Tools/make_turf_texture.py <输出.png>
旧的 T_FeltGrain 是绒布纤维 + 斜向软条纹，铺在大片草地上条纹按 0.6 m 重复、整片又平又素（用户：草地有点粗糙）。
这里：中尺度柔和斑块（无缝噪声，多个周期叠加）+ 上万片短弯小草叶笔触（根深尖浅，随机朝向）+ 少量亮草尖，四边无缝。
平均亮度对齐旧贴图（≈0.9），地面整体颜色不变。"""
import sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
S = 1024; SS = 2                       # 2 倍超采样画笔触再缩小
N = S * SS
rnd = random.Random(1031)
rs = np.random.RandomState(1031)


def tile_noise(period, amp):
    """周期整除 S 的无缝值噪声（双三次插值）"""
    g = rs.rand(period, period)
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC)
    # resize 不保证首尾连续：用 3×3 平铺后取中间一块，接缝处也是插值过的
    big = np.tile(g, (3, 3))
    img = Image.fromarray((big * 255).astype(np.uint8)).resize((S * 3, S * 3), Image.BICUBIC)
    a = np.asarray(img, np.float32)[S:2 * S, S:2 * S] / 255.0
    return (a - 0.5) * 2 * amp


base = 0.86 + tile_noise(4, 0.035) + tile_noise(8, 0.03) + tile_noise(16, 0.018)

canvas = Image.new('L', (N, N), 0)
alpha = Image.new('L', (N, N), 0)
dc = ImageDraw.Draw(canvas); da = ImageDraw.Draw(alpha)


def blade(x, y, ang, L, W, lo, hi):
    """一片小草叶：沿弧线的锥形条，分 6 段从根（lo）到尖（hi）提亮"""
    bend = rnd.uniform(-0.5, 0.5)
    pts = []
    for k in range(7):
        t = k / 6
        a = ang + bend * t
        pts.append((x + math.cos(a) * L * t, y + math.sin(a) * L * t, a))
    for k in range(6):
        (x0, y0, a0), (x1, y1, a1) = pts[k], pts[k + 1]
        w0 = W * (1 - k / 6) ** 0.9; w1 = W * (1 - (k + 1) / 6) ** 0.9
        n0 = (-math.sin(a0) * w0, math.cos(a0) * w0); n1 = (-math.sin(a1) * w1, math.cos(a1) * w1)
        poly = [(x0 + n0[0], y0 + n0[1]), (x1 + n1[0], y1 + n1[1]), (x1 - n1[0], y1 - n1[1]), (x0 - n0[0], y0 - n0[1])]
        v = int(255 * (lo + (hi - lo) * (k + 0.5) / 6))
        for ox in (-N, 0, N):                                   # 跨边的笔触在对边补画：无缝
            for oy in (-N, 0, N):
                if not (-80 < x + ox < N + 80 and -80 < y + oy < N + 80): continue
                pp = [(px + ox, py + oy) for px, py in poly]
                dc.polygon(pp, fill=v); da.polygon(pp, fill=255)


for i in range(26000):
    x, y = rnd.uniform(0, N), rnd.uniform(0, N)
    L = rnd.uniform(14, 34) * SS
    W = rnd.uniform(1.4, 2.6) * SS
    lo = rnd.uniform(0.64, 0.76); hi = rnd.uniform(0.94, 1.04)
    blade(x, y, rnd.uniform(0, math.tau), L, W, lo, min(hi, 1.0))

canvas = canvas.resize((S, S), Image.LANCZOS); alpha = alpha.resize((S, S), Image.LANCZOS)
c = np.asarray(canvas, np.float32) / 255.0; a = np.asarray(alpha, np.float32) / 255.0 * 0.8
img = base * (1 - a) + (c + (base - 0.86)) * a
img = img * (0.9 / img.mean())
img = np.clip(img, 0, 1)
rgb = np.stack([img * 0.995, img, img * 0.985], -1)       # 极轻的偏暖绿，不影响顶点色主调
Image.fromarray((rgb * 255).astype(np.uint8)).save(OUT)
print('TURF', OUT, float(img.mean()), float(img.std()))
