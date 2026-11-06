"""六章门洞的不规则“虫洞”轮廓（Blender 门框脚本和 Unity 门洞遮罩共用同一份）。

坐标：门面局部 Unity (x, y)，底边贴地 y=0，门后世界在 -z。
轮廓 = 下宽上圆的基础形（下半超椭圆、上半椭圆）× 低频谐波起伏 + 一点高频毛边；贴地部分压平成一段平底（小怪要从这里走出来）。
  outline(ch)      闭合轮廓（逆时针，Unity x/y），首点在右下贴地处
  path(ch, off)    门框中心线：轮廓沿外法线外扩 off 米，从右下贴地处绕过顶部到左下贴地处（开放折线，等弧长重采样）
  python3 portal_outline.py <输出目录>   → 写 portal_outline_W1..W6.json（outline + path 采样）并画预览图
"""
import math, random, json, os, sys

W_HALF, H_TOP = 0.72, 1.98          # 名义半宽、顶高（与玩法规则 portal.width≈1.3–1.45 / height≈1.9–2.0 对齐）


def _raw(ch, n):
    rnd = random.Random(7300 + ch * 17)
    harm = [(k, rnd.uniform(0.028, 0.06) / k ** 0.35, rnd.uniform(0, math.tau)) for k in range(2, 7)]
    fine = [(k, rnd.uniform(0.004, 0.009), rnd.uniform(0, math.tau)) for k in range(9, 14)]
    lean = rnd.uniform(-0.06, 0.06)                     # 整体略微歪一点（像被撕开的洞）
    cy = H_TOP * 0.5
    pts = []
    for i in range(n):
        th = -math.pi / 2 + i / n * math.tau             # 从正下方开始逆时针
        c, s = math.cos(th), math.sin(th)
        if s < 0:                                        # 下半：超椭圆（更方，底更宽）
            p = 3.2
            cx_ = math.copysign(abs(c) ** (2 / p), c); sy_ = -abs(s) ** (2 / p)
        else:
            cx_, sy_ = c, s
        r = 1 + sum(a * math.sin(k * th + ph) for k, a, ph in harm) + sum(a * math.sin(k * th + ph) for k, a, ph in fine)
        x = cx_ * W_HALF * r + lean * max(sy_, 0) * 0.5
        y = cy + sy_ * (H_TOP * 0.5) * r
        pts.append((x, y))
    return pts


def _resample(pts, step, closed):
    seq = pts + ([pts[0]] if closed else [])
    L = [0.0]
    for a, b in zip(seq[:-1], seq[1:]): L.append(L[-1] + math.dist(a, b))
    m = max(2, int(round(L[-1] / step)))
    out = []
    j = 1
    for i in range(m + (0 if closed else 1)):
        t = L[-1] * i / m
        while j < len(L) - 1 and L[j] < t: j += 1
        f = (t - L[j - 1]) / max(L[j] - L[j - 1], 1e-9)
        a, b = seq[j - 1], seq[j]
        out.append((a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f))
    return out


def outline(ch, step=0.02):
    """闭合轮廓，逆时针（+x → +y），首点为右侧贴地点；贴地段 y 压为 0。"""
    raw = _raw(ch, 720)
    lo = min(y for _, y in raw)
    raw = [(x, max(0.0, y - lo - 0.04)) for x, y in raw]   # 底部往下沉 4cm 再截平 → 一段平底
    # 旋转起点到右侧最后一个 y==0 的点（逆时针走：平底 → 右侧上升 → 顶 → 左侧下降 → 平底）
    idx = max((i for i, (x, y) in enumerate(raw) if y == 0.0 and x > 0), key=lambda i: raw[i][0])
    raw = raw[idx:] + raw[:idx]
    pts = _resample(raw, step, True)
    pts[0] = (pts[0][0], 0.0)
    return pts


def normals(pts, closed=True):
    n = len(pts); out = []
    for i in range(n):
        a = pts[(i - 1) % n] if closed or i > 0 else pts[i]
        b = pts[(i + 1) % n] if closed or i < n - 1 else pts[i]
        tx, ty = b[0] - a[0], b[1] - a[1]; L = math.hypot(tx, ty) or 1
        out.append((ty / L, -tx / L))                     # 逆时针轮廓的外法线
    return out


def path(ch, off, step=0.01):
    """门框中心线：外扩 off 米，从右下贴地 → 顶 → 左下贴地。法线先平滑，凹处不打结。"""
    o = outline(ch, 0.01)
    nr = normals(o)
    # 法线平滑（窗口 ±6cm）
    k = 6; sm = []
    for i in range(len(o)):
        sx = sum(nr[(i + j) % len(o)][0] for j in range(-k, k + 1)); sy = sum(nr[(i + j) % len(o)][1] for j in range(-k, k + 1))
        L = math.hypot(sx, sy) or 1; sm.append((sx / L, sy / L))
    pts = [(x + nx * off, y + ny * off) for (x, y), (nx, ny) in zip(o, sm)]
    # 只取 y>0 的连续一段（离开平底），两端补到地面
    first = next(i for i in range(len(o)) if o[i][1] > 0.005)
    last = max(i for i in range(len(o)) if o[i][1] > 0.005)
    seg = pts[first:last + 1]
    seg = [(seg[0][0], 0.0)] + seg + [(seg[-1][0], 0.0)]
    seg = [(x, max(y, 0.0)) for x, y in seg]
    return _resample(seg, step, False)


def main():
    out = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for ch in range(1, 7):
        o = outline(ch, 0.025)
        d = {'outline': [[round(x, 5), round(y, 5)] for x, y in o]}
        for off in (0.12, 0.16, 0.2, 0.3):                 # 门框中心线（C# 摆 ABC 积木、挂饰用）
            d[f'path{int(off * 100)}'] = [[round(x, 5), round(y, 5)] for x, y in path(ch, off, 0.02)]
        json.dump(d, open(os.path.join(out, f'portal_outline_W{ch}.json'), 'w'))
    try:
        from PIL import Image, ImageDraw
        S = 200; img = Image.new('RGB', (S * 6, int(S * 1.25)), 'white'); d = ImageDraw.Draw(img)
        for ch in range(1, 7):
            ox = S * (ch - 1) + S // 2; oy = int(S * 1.15)
            o = outline(ch)
            d.polygon([(ox + x * 90, oy - y * 90) for x, y in o], fill=(230, 220, 250), outline=(120, 90, 180))
            p = path(ch, 0.16)
            d.line([(ox + x * 90, oy - y * 90) for x, y in p], fill=(240, 120, 150), width=2)
            d.text((ox - 10, 5), f'W{ch}', fill='black')
        img.save(os.path.join(out, 'portal_outlines.png'))
    except ImportError:
        pass


if __name__ == '__main__':
    main()
