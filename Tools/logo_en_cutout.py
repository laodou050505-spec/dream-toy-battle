"""英文版标题字抠图（python3 Tools/logo_en_cutout.py）：ArtSource/UI/logo_en.png → Logo_Main_EN.png（2D）+ Logo_3D_EN.png（3D 贴图，字内空白填奶油色）。
流程与 ui_cutout.py 的中文标题字完全一致。"""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np, cv2
from ui_cutout import cutout, trim, save, load, UI
la = load('logo_en')
save(trim(cutout(la, holes=True)), UI / 'Logo_Main_EN.png', 2048)
l3 = trim(cutout(la))
rgb = l3[:, :, :3].astype(np.int16); mx, mn = rgb.max(2), rgb.min(2)
hole = (mn > 244) & (mx - mn < 10) & (l3[:, :, 3] > 200)
n, lab, st, _ = cv2.connectedComponentsWithStats(hole.astype(np.uint8), connectivity=4)
for i in range(1, n):
    if st[i, cv2.CC_STAT_AREA] > l3.shape[0] * l3.shape[1] * 0.0015: l3[lab == i, :3] = np.array([252, 238, 222], np.uint8)
save(l3, UI / 'Logo_3D_EN.png', 2048)
