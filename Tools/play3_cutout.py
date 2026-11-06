"""第三批玩法美术抠图（python3 Tools/play3_cutout.py）：枪口火光 + 更多拟声字（啪 / 嘭 / 咻），白底抠透明、裁到内容、512 方图。"""
import pathlib, sys
import numpy as np
from PIL import Image
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from ui_cutout import cutout, trim, ROOT

SRC = ROOT / 'ArtSource' / 'Play3'
FX = ROOT / 'Assets' / '_Bang' / 'Art' / 'Textures' / 'FX'

def square(rgba):
    t = trim(rgba, 8); s = max(t.shape[:2]); out = np.zeros((s, s, 4), np.uint8)
    y0, x0 = (s - t.shape[0]) // 2, (s - t.shape[1]) // 2
    out[y0:y0 + t.shape[0], x0:x0 + t.shape[1]] = t
    return out

for src, dst in (('word_pa', 'FX_Pa'), ('word_peng', 'FX_PengB'), ('word_xiu', 'FX_Xiu'), ('muzzle_flash', 'FX_Muzzle')):
    rgba = square(cutout(np.array(Image.open(SRC / f'{src}.png').convert('RGB'))))
    Image.fromarray(rgba).resize((512, 512), Image.LANCZOS).save(FX / f'{dst}.png')
    print(dst, rgba.shape)
