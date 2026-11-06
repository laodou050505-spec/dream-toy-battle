"""Logo 外轮廓提取（python3 -I Tools/logo_contours.py）：Logo_Main.png 的透明通道 → 各连通块外轮廓（简化到 1.2 像素误差）→ ArtSource/UI/logo_contours.json，
给 Tools/blender_logo3d.py 挤出成 3D 糖果 Logo。"""
import json, pathlib, sys
import numpy as np, cv2
from PIL import Image
ROOT = pathlib.Path(__file__).resolve().parents[1]
SUF = sys.argv[1] if len(sys.argv) > 1 else ''          # '_EN' = 英文版标题字
im = np.array(Image.open(ROOT / f'Assets/_Bang/Art/UI/Logo_Main{SUF}.png'))
a = (im[:, :, 3] > 110).astype(np.uint8) * 255
a = cv2.morphologyEx(a, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
cs, _ = cv2.findContours(a, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
polys = []
for c in cs:
    if cv2.contourArea(c) < 250: continue
    p = cv2.approxPolyDP(c, 1.2, True)[:, 0, :].tolist()
    if len(p) >= 3: polys.append(p)
json.dump({'w': im.shape[1], 'h': im.shape[0], 'polys': polys}, open(ROOT / f'ArtSource/UI/logo_contours{SUF}.json', 'w'))
print(len(polys), [len(p) for p in polys][:12])
