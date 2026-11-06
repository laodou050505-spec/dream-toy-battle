"""商店封面 / 启动图合成（python3 -I Tools/make_cover.py）：nano banana 主视觉 + Logo（投影 + 外发光）+ 站酷快乐体副标题 + 暗角。
输出 Docs/Cover/BANG_Cover_16x9.png（2752×1536）、BANG_Cover_1920x1080.png、BANG_Icon_1024.png，并把图标拷进工程。"""
import pathlib
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'Docs' / 'Cover'; OUT.mkdir(parents=True, exist_ok=True)
base = Image.open(ROOT / 'ArtSource/UI/cover_keyart.png').convert('RGBA')
W, H = base.size

# 暗角 + 左上角一点柔光（给 Logo 托底）
yy, xx = np.mgrid[0:H, 0:W]
r = np.sqrt(((xx - W * 0.5) / (W * 0.62)) ** 2 + ((yy - H * 0.5) / (H * 0.62)) ** 2)
vig = np.clip((r - 0.62) / 0.6, 0, 1) ** 1.6 * 0.42
glow = np.exp(-(((xx - W * 0.27) / (W * 0.24)) ** 2 + ((yy - H * 0.3) / (H * 0.3)) ** 2)) * 0.35
arr = np.array(base).astype(np.float32)
arr[..., :3] = arr[..., :3] * (1 - vig[..., None]) + np.array([60, 30, 70]) * vig[..., None]
arr[..., :3] = arr[..., :3] + (255 - arr[..., :3]) * glow[..., None]
img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGBA')

logo = Image.open(ROOT / 'Assets/_Bang/Art/UI/Logo_Main.png').convert('RGBA')
lw = int(W * 0.36); logo = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)
lx, ly = int(W * 0.09), int(H * 0.07)
a = logo.split()[3]
shadow = Image.new('RGBA', logo.size, (90, 40, 110, 0)); shadow.putalpha(a.point(lambda v: int(v * 0.55)))
shadow = shadow.filter(ImageFilter.GaussianBlur(18))
halo = Image.new('RGBA', logo.size, (255, 240, 250, 0)); halo.putalpha(a.point(lambda v: int(v * 0.8)))
pad = 60
halo_big = Image.new('RGBA', (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0)); halo_big.alpha_composite(halo, (pad, pad))
halo_big = halo_big.filter(ImageFilter.GaussianBlur(34))
img.alpha_composite(halo_big, (lx - pad, ly - pad))
img.alpha_composite(shadow, (lx + 10, ly + 22))
img.alpha_composite(logo, (lx, ly))

# 副标题：站酷快乐体，白字 + 莓紫描边
font = ImageFont.truetype(str(ROOT / 'Assets/_Bang/Art/Fonts/ZCOOLKuaiLe-Regular.ttf'), int(H * 0.058))
d = ImageDraw.Draw(img)
text = 'DREAM TOY BATTLE'
tw = d.textlength(text, font=font)
tx, ty = lx + (lw - tw) / 2, ly + logo.height + int(H * 0.01)
d.text((tx + 4, ty + 6), text, font=font, fill=(90, 40, 110, 110))
d.text((tx, ty), text, font=font, fill=(255, 255, 255, 255), stroke_width=int(H * 0.006), stroke_fill=(140, 80, 160, 255))
sub = ImageFont.truetype(str(ROOT / 'Assets/_Bang/Art/Fonts/ZCOOLKuaiLe-Regular.ttf'), int(H * 0.028))
t2 = 'MR · 手势 + 眼动 · 比出手指枪，守住你的房间'
tw2 = d.textlength(t2, font=sub)
d.text((lx + (lw - tw2) / 2, ty + int(H * 0.075)), t2, font=sub, fill=(255, 246, 250, 235), stroke_width=int(H * 0.003), stroke_fill=(150, 90, 170, 220))

img = ImageEnhance.Contrast(img).enhance(1.03)
img.convert('RGB').save(OUT / 'DreamToyBattle_Cover_16x9.png')
img.convert('RGB').resize((1920, 1080), Image.LANCZOS).save(OUT / 'DreamToyBattle_Cover_1920x1080.png')

icon = Image.open(ROOT / 'ArtSource/UI/app_icon.png').convert('RGB').resize((1024, 1024), Image.LANCZOS)
icon.save(OUT / 'DreamToyBattle_Icon_1024.png')
icon.resize((512, 512), Image.LANCZOS).save(ROOT / 'Assets/_Bang/Art/UI/AppIcon.png')
print('ok', img.size)
