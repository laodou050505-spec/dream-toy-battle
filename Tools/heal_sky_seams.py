"""天球全景接缝修补后处理：python3 Tools/heal_sky_seams.py
ArtSource/Indoor/seam/<名>_healed.png 是“把原图左右半边对调（接缝挪到正中）后让 nano banana 只重画中间一条”的结果：
它自身的左右边缘本来就是原图中间连续的两列 → 直接就是无缝全景（方位整体转了 180°，无所谓）。
检查：中间条带以外和对调图的差异（构图没被改动），通过的缩到 8192 宽写进 Assets/_Bang/Art/Textures/Cyclo_*.png。"""
import os, numpy as np
from PIL import Image
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SEAM = os.path.join(ROOT, 'ArtSource/Indoor/seam'); DST = os.path.join(ROOT, 'Assets/_Bang/Art/Textures')
for n, out in [('Cyclo_W1', 'Cyclo_W1'), ('Cyclo_W2', 'Cyclo_W2'), ('Cyclo_W3', 'Cyclo_W3'), ('Cyclo_W4', 'Cyclo_W4'), ('W6_NightSky', 'Cyclo_W6')]:
    hp = os.path.join(SEAM, n + '_healed.png')
    if not os.path.exists(hp): print('缺', n); continue
    roll = Image.open(os.path.join(SEAM, n + '_roll.png')).convert('RGB')
    heal = Image.open(hp).convert('RGB').resize(roll.size, Image.Resampling.LANCZOS)
    a = np.asarray(roll, np.float32); b = np.asarray(heal, np.float32)
    w = a.shape[1]; band = slice(int(w * 0.42), int(w * 0.58))
    mask = np.ones(w, bool); mask[band] = False
    diff_out = np.abs(a[:, mask] - b[:, mask]).mean()
    edge = np.abs(b[:, :8] - b[:, -8:][:, ::-1]).mean()
    print(f'{n}: 条带外平均差 {diff_out:.1f}（<12 视为构图未变）  左右边缘差 {edge:.1f}')
    if diff_out > 12: print('  → 构图改动太大，不采用'); continue
    heal.resize((8192, int(8192 * heal.size[1] / heal.size[0])), Image.Resampling.LANCZOS).save(os.path.join(DST, out + '.png'))
    print('  → 已写入', out)
