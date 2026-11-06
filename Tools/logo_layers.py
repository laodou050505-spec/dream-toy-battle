"""错位标题：一张 nano banana 字标图 → 3D 用贴图 + 两层轮廓（python3 Tools/logo_layers.py <原图> <后缀>）。
  贴图   Assets/_Bang/Art/UI/Logo_3D<后缀>.png（白底抠透明、字内空白填奶油色，同 ui_cutout 的中文字标处理）
  后层   ArtSource/UI/logo_contours<后缀>b.json：整个字标剪影（含深紫“错位影子层”）
  前层   ArtSource/UI/logo_contours<后缀>f.json：剪影去掉深紫影子 = 字块本身（含奶油描边）
blender_logo3d.py -- <out> <后缀>b pieces / <后缀>f pieces 分别挤出；Unity 里前层在前、后层退后几厘米 → 真正的前后错位。"""
import sys, json, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np, cv2
from PIL import Image
from ui_cutout import cutout, trim, save, UI, ROOT

src, suf = sys.argv[1], sys.argv[2]
rgb = np.array(Image.open(src).convert('RGB'))
l3 = trim(cutout(rgb))
c = l3[:, :, :3].astype(np.int16); mx, mn = c.max(2), c.min(2)
hole = (mn > 244) & (mx - mn < 10) & (l3[:, :, 3] > 200)
n, lab, st, _ = cv2.connectedComponentsWithStats(hole.astype(np.uint8), connectivity=4)
for i in range(1, n):
    if st[i, cv2.CC_STAT_AREA] > l3.shape[0] * l3.shape[1] * 0.0015: l3[lab == i, :3] = np.array([252, 238, 222], np.uint8)
im = Image.fromarray(l3); s = 2048 / max(im.size)
if s < 1: im = im.resize((round(im.size[0] * s), round(im.size[1] * s)), Image.LANCZOS)
im.save(UI / f'Logo_3D{suf}.png')
a = np.array(im)
alpha = a[:, :, 3] > 110
r, g, b = a[:, :, 0].astype(int), a[:, :, 1].astype(int), a[:, :, 2].astype(int)
plum = alpha & (r < 150) & (g < 95) & (b < 150) & (r > g + 8) & (b > g + 8)
k = np.ones((5, 5), np.uint8)
plum = cv2.morphologyEx(plum.astype(np.uint8), cv2.MORPH_OPEN, k).astype(bool)
def polys(mask, min_area):
    m = cv2.morphologyEx(mask.astype(np.uint8) * 255, cv2.MORPH_CLOSE, k)
    cs, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    out = []
    for cc in cs:
        if cv2.contourArea(cc) < min_area: continue
        p = cv2.approxPolyDP(cc, 1.2, True)[:, 0, :].tolist()
        if len(p) >= 3: out.append(p)
    return out
H, W = a.shape[:2]
back = polys(alpha, 250); front = polys(alpha & ~plum, 250)
for nm, ps in (('b', back), ('f', front)):
    json.dump({'w': W, 'h': H, 'polys': ps}, open(ROOT / f'ArtSource/UI/logo_contours{suf}{nm}.json', 'w'))
print('size', W, H, 'back', len(back), [len(p) for p in back][:8], 'front', len(front), [len(p) for p in front][:12], 'plum px', int(plum.sum()))
