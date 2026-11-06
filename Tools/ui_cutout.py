"""把 nano banana 生成的白底 UI 美术抠成透明图并切图（python3 -I Tools/ui_cutout.py）。
  抠底：从图像边缘泛洪“接近白色、低饱和”的像素作为背景，再按到背景的距离羽化 1.5 像素（边缘不锯齿、不留白边）；
  icons.png 按 4×3 网格切成 12 个图标，gestures.png 按 5 列切成 5 个手势插图，各自裁到内容包围盒并留边。
输出到 Assets/_Bang/Art/UI/（UI 素材）和 Assets/_Bang/Art/Textures/FX/（特效贴图）。"""
import pathlib
import numpy as np
import cv2
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / 'ArtSource' / 'UI'
UI = ROOT / 'Assets' / '_Bang' / 'Art' / 'UI'
FX = ROOT / 'Assets' / '_Bang' / 'Art' / 'Textures' / 'FX'
UI.mkdir(parents=True, exist_ok=True)


def cutout(rgb, tol=26, holes=False, edge_frag=False):
    """rgb: HxWx3 uint8 → RGBA，背景 = 与四边连通的近白区域"""
    h, w = rgb.shape[:2]
    f = rgb.astype(np.int16)
    mx, mn = f.max(2), f.min(2)
    whiteish = ((mx > 255 - tol - 10) & (mx - mn < tol)).astype(np.uint8)
    # 从边缘泛洪：只把与边缘连通的近白区域当背景（图案内部的白色高光保留）
    n, lab = cv2.connectedComponents(whiteish, connectivity=4)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(border)) & (whiteish > 0)
    if holes:                                    # 图标内部封闭的纯白大块（锁环、齿轮中心、空心星）也是背景
        pure = ((mn > 244) & (mx - mn < 10)).astype(np.uint8)
        n2, lab2, st, _ = cv2.connectedComponentsWithStats(pure, connectivity=4)
        for i in range(1, n2):
            if st[i, cv2.CC_STAT_AREA] > h * w * 0.004: bg |= lab2 == i
    fg = (~bg).astype(np.uint8)
    if edge_frag:                                # 去掉贴着左右切线的碎片（相邻插图的箭头）
        n3, lab3, st3, _ = cv2.connectedComponentsWithStats(fg, connectivity=8)
        for i in range(1, n3):
            x, _, ww, _, area = st3[i]
            if (x <= 1 or x + ww >= w - 1) and area < h * w * 0.03: fg[lab3 == i] = 0
    # 羽化：前景内部到背景的距离 < 1.5 px 的地方渐变
    d = cv2.distanceTransform(fg, cv2.DIST_L2, 3)
    a = np.clip(d / 1.5, 0, 1)
    # 去白边：边缘像素把颜色往内部推（用腐蚀后的前景颜色填边）
    inner = cv2.erode(fg, np.ones((3, 3), np.uint8))
    out = rgb.copy()
    edge = (fg > 0) & (inner == 0)
    if edge.any():
        blur = cv2.inpaint(rgb, ((inner == 0)).astype(np.uint8) * 255, 2, cv2.INPAINT_TELEA)
        out[edge] = blur[edge]
    rgba = np.dstack([out, (a * 255).astype(np.uint8)])
    return rgba


def trim(rgba, pad=8):
    ys, xs = np.where(rgba[:, :, 3] > 8)
    if len(xs) == 0: return rgba
    y0, y1, x0, x1 = max(0, ys.min() - pad), min(rgba.shape[0], ys.max() + pad + 1), max(0, xs.min() - pad), min(rgba.shape[1], xs.max() + pad + 1)
    return rgba[y0:y1, x0:x1]


def save(rgba, path, max_side):
    im = Image.fromarray(rgba)
    im.thumbnail((max_side, max_side), Image.LANCZOS)
    im.save(path)
    print(path.relative_to(ROOT), im.size)


def load(name):
    return np.array(Image.open(SRC / f'{name}.png').convert('RGB'))


def main():
    la = load('logo_a')
    save(trim(cutout(la, holes=True)), UI / 'Logo_Main.png', 2048)            # 2D 用：字形内部的空白抠透明
    l3 = trim(cutout(la))                                                      # 3D 用：内部空白填成描边的奶油色（糖果贴纸底板）
    rgb = l3[:, :, :3].astype(np.int16); mx, mn = rgb.max(2), rgb.min(2)
    hole = (mn > 244) & (mx - mn < 10) & (l3[:, :, 3] > 200)
    n, lab, st, _ = cv2.connectedComponentsWithStats(hole.astype(np.uint8), connectivity=4)
    cream = np.array([252, 238, 222], np.uint8)
    for i in range(1, n):
        if st[i, cv2.CC_STAT_AREA] > l3.shape[0] * l3.shape[1] * 0.0015: l3[lab == i, :3] = cream
    save(l3, UI / 'Logo_3D.png', 2048)
    save(trim(cutout(load('logo_b'))), UI / 'Logo_Alt.png', 1024)
    save(trim(cutout(load('ribbon'))), UI / 'Ribbon.png', 1536)
    save(trim(cutout(load('hit_burst'))), FX / 'FX_HitBurst.png', 512)
    cr = cutout(load('crayon_marks'))
    save(cr, FX / 'FX_CrayonMarks.png', 1024)                       # 保持 2×2 排布（按 UV 取四分之一）
    ic = load('icons'); h, w = ic.shape[:2]
    names = ['star', 'star_empty', 'lock', 'coin', 'flag', 'wand', 'play', 'pause', 'retry', 'home', 'settings', 'heart']
    for k, n in enumerate(names):
        r, c = divmod(k, 4)
        cell = ic[int(r * h / 3) + 14:int((r + 1) * h / 3) - 14, int(c * w / 4) + 14:int((c + 1) * w / 4) - 14]
        save(trim(cutout(cell, tol=30, holes=True), pad=6), UI / f'Icon_{n}.png', 256)
    ge = load('gestures'); h, w = ge.shape[:2]
    for k, n in enumerate(['gun', 'flick', 'fist', 'palm', 'binoc']):
        cell = ge[:, int(k * w / 5):int((k + 1) * w / 5)]
        save(trim(cutout(cell, edge_frag=True), pad=10), UI / f'Gesture_{n}.png', 512)


if __name__ == '__main__':
    main()
