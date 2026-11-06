"""第二批玩法美术抠图（python3 -I Tools/play2_cutout.py）：
  lock_reticle.png → FX_LockReticle.png：四个糖果角括号 + 小星星，去掉虚线圆点（小连通块），居中裁成正方形；
                    运行时按四个象限分成四片各自收拢 / 张开。
  buff_icons.png   → Icon_buff_{gold,freeze,repair,unflag}.png：2×2 网格切成四个增益徽章。"""
import pathlib, sys
import numpy as np, cv2
from PIL import Image
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from ui_cutout import cutout, trim, ROOT

SRC = ROOT / 'ArtSource' / 'Play2'
UI = ROOT / 'Assets' / '_Bang' / 'Art' / 'UI'
FX = ROOT / 'Assets' / '_Bang' / 'Art' / 'Textures' / 'FX'

def load(n): return np.array(Image.open(SRC / n).convert('RGB'))

# 准星
rgba = cutout(load('lock_reticle.png'))
fg = (rgba[:, :, 3] > 40).astype(np.uint8)
n, lab, st, _ = cv2.connectedComponentsWithStats(fg, connectivity=8)
h, w = fg.shape
for i in range(1, n):
    if st[i, cv2.CC_STAT_AREA] < h * w * 0.002: rgba[lab == i, 3] = 0        # 虚线圆点
ys, xs = np.where(rgba[:, :, 3] > 8)
cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
half = int(max(xs.max() - xs.min(), ys.max() - ys.min()) / 2) + 10
x0, y0 = int(cx - half), int(cy - half)
sq = np.zeros((2 * half, 2 * half, 4), np.uint8)
sx0, sy0 = max(0, x0), max(0, y0)
crop = rgba[sy0:y0 + 2 * half, sx0:x0 + 2 * half]
sq[sy0 - y0:sy0 - y0 + crop.shape[0], sx0 - x0:sx0 - x0 + crop.shape[1]] = crop
Image.fromarray(sq).resize((512, 512), Image.LANCZOS).save(FX / 'FX_LockReticle.png')
print('reticle', sq.shape)

# 增益徽章
img = load('buff_icons.png'); H, W = img.shape[:2]
for (r, c), name in zip([(0, 0), (0, 1), (1, 0), (1, 1)], ['gold', 'freeze', 'repair', 'unflag']):
    cell = img[r * H // 2:(r + 1) * H // 2, c * W // 2:(c + 1) * W // 2]
    t = trim(cutout(cell), 6)
    s = max(t.shape[:2]); pad = np.zeros((s, s, 4), np.uint8)
    pad[(s - t.shape[0]) // 2:(s - t.shape[0]) // 2 + t.shape[0], (s - t.shape[1]) // 2:(s - t.shape[1]) // 2 + t.shape[1]] = t
    Image.fromarray(pad).resize((256, 256), Image.LANCZOS).save(UI / f'Icon_buff_{name}.png')
    print(name, t.shape)
