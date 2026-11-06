"""室内化贴图后处理：python3 Tools/make_indoor_textures.py
  ArtSource/Indoor/*.png（nano banana 出图）→ Assets/_Bang/Art/Textures/
  - 横向全景（Playroom_Mural、Cyclo_W1..W4、W6_NightSky）：左右两端各取 6% 交叉渐变成无缝，再缩到 4096 宽
  - 方形平铺图（Playroom_Ceiling、W5_Wallpaper）：上下左右各做一次偏移交叉渐变，缩到 1024
  - Playroom_Floor：程序生成的粉彩木地板（错缝长条木板 + 板缝 + 木纹），可平铺
"""
import os, random
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'ArtSource/Indoor')
DST = os.path.join(ROOT, 'Assets/_Bang/Art/Textures')


def seamless_h(im, frac=0.06):
    a = np.asarray(im.convert('RGB'), np.float32)
    h, w, _ = a.shape
    o = int(w * frac)
    t = np.linspace(0, 1, o)[None, :, None]
    t = t * t * (3 - 2 * t)
    head = a[:, :o]; tail = a[:, w - o:]
    out = a[:, :w - o].copy()
    out[:, :o] = tail * (1 - t) + head * t          # 末尾渐变进开头：拼接处连续
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def seamless_2d(im, frac=0.12):
    im = seamless_h(im, frac)
    im = seamless_h(im.transpose(Image.Transpose.ROTATE_90), frac).transpose(Image.Transpose.ROTATE_270)
    return im


def floor_planks(size=1024, seed=7):
    rnd = random.Random(seed)
    rows = 8; ph = size // rows
    base = [(0.96, 0.86, 0.74), (0.98, 0.9, 0.8), (0.95, 0.84, 0.76), (0.97, 0.88, 0.72), (0.99, 0.91, 0.84)]
    img = np.zeros((size, size, 3), np.float32)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    grain = np.sin(xx / size * np.pi * 2 * 3 + np.sin(yy / ph * np.pi * 2) * 1.5)          # 低频木纹
    noise = np.asarray(Image.effect_noise((size, size), 40).filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255.0
    for r in range(rows):
        y0 = r * ph
        x = -rnd.randint(0, size // 2)
        while x < size:
            L = rnd.choice([size // 2, size // 2 + size // 4, size // 4 * 3])
            c = np.array(rnd.choice(base)) * rnd.uniform(0.96, 1.03)
            x0, x1 = max(x, 0), min(x + L, size)
            if x1 > x0:
                img[y0:y0 + ph, x0:x1] = c
                img[y0:y0 + ph, x0:min(x0 + 3, size)] *= 0.86          # 端缝
            x += L
        img[y0:y0 + 3, :] *= 0.84                                         # 长缝
    img *= (0.97 + 0.03 * grain[..., None]) * (0.95 + 0.1 * noise[..., None])
    return Image.fromarray(np.clip(img * 255, 0, 255).astype(np.uint8))


def main():
    os.makedirs(DST, exist_ok=True)
    for n in ('Playroom_Mural', 'Cyclo_W1', 'Cyclo_W2', 'Cyclo_W3', 'Cyclo_W4', 'W6_NightSky'):
        p = os.path.join(SRC, n + '.png')
        if not os.path.exists(p): continue
        im = seamless_h(Image.open(p))
        W = 8192 if n.startswith('Cyclo') or n == 'W6_NightSky' else 4096       # 天球用原分辨率：近看也清楚
        im = im.resize((W, int(W * im.size[1] / im.size[0])), Image.Resampling.LANCZOS)
        im.save(os.path.join(DST, n + '.png')); print('全景', n, im.size)
    for n in ('Playroom_Ceiling', 'W5_Wallpaper'):
        p = os.path.join(SRC, n + '.png')
        if not os.path.exists(p): continue
        im = seamless_2d(Image.open(p)).resize((1024, 1024), Image.Resampling.LANCZOS)
        im.save(os.path.join(DST, n + '.png')); print('平铺', n)
    floor_planks().save(os.path.join(DST, 'Playroom_Floor.png')); print('地板')


if __name__ == '__main__':
    main()
