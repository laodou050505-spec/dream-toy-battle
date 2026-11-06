"""手势提示图标抠图（python3 Tools/gesture_cutout.py [binoc 源图名]）：ArtSource/Gestures2/g_*.png → Assets/_Bang/Art/UI/Gesture_*.png（512，透明底）。
每张图都按识别规则单独生成并人工核对过：手指枪 / 挑腕开火 / 握拳换弹 / 举盾（手背朝自己）/ 望远镜（空心拳，无伸出的手指）/ 捏合。"""
import pathlib, sys
import numpy as np
from PIL import Image
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from ui_cutout import cutout, trim, ROOT

SRC = ROOT / 'ArtSource' / 'Gestures2'
UI = ROOT / 'Assets' / '_Bang' / 'Art' / 'UI'
binoc = sys.argv[1] if len(sys.argv) > 1 else 'g_binoc'
for src, dst in (('g_gun', 'gun'), ('g_flick', 'flick'), ('g_fist', 'fist'), ('g_palm', 'palm'), (binoc, 'binoc'), ('g_pinch', 'pinch')):
    p = SRC / f'{src}.png'
    if not p.exists(): print('缺', src); continue
    img = Image.open(p).convert('RGB')
    if dst == 'pinch': img = img.crop((600, 500, 1500, 1560))          # 这张自带一个圆角边框：只取框内的手
    t = trim(cutout(np.array(img), tol=22, holes=dst in ('pinch',)), 10)       # 捏合：拇指和食指之间围出来的那块白底也抠掉
    im = Image.fromarray(t); im.thumbnail((512, 512), Image.LANCZOS); im.save(UI / f'Gesture_{dst}.png'); print(dst, im.size)
