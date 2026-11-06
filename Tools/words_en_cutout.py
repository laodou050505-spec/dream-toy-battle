"""英文版拟声字抠图（python3 Tools/words_en_cutout.py）：ArtSource/UI/EN/words/FX_*_EN.png → Assets/_Bang/Art/Textures/FX/（白底抠透明、裁边，和中文拟声字同一套处理）。"""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from ui_cutout import cutout, trim, save, ROOT, FX
import numpy as np
from PIL import Image
for p in sorted((ROOT / 'ArtSource/UI/EN/words').glob('FX_*_EN.png')):
    save(trim(cutout(np.array(Image.open(p).convert('RGB')))), FX / p.name, 512)
