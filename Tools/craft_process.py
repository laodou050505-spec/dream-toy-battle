"""手作 UI 素材后处理（python3 -I Tools/craft_process.py）：
  纹理（纸 / 牛皮纸板）：缩到 1024，和自身错开半张的副本按“边缘 0、中心 1”的权重混合 → 四边无缝平铺；
  贴纸（浅灰底）：从四边泛洪与角落底色接近的像素为背景（白色模切边和底色能分开），羽化 1.5 像素，裁到内容并留边；
  和纸胶带（白底）：同上，背景色取白。输出 Assets/_Bang/Art/UI/Craft/。"""
import pathlib, sys
import numpy as np, cv2
from PIL import Image
ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / 'ArtSource' / 'UI' / 'Craft'
OUT = ROOT / 'Assets' / '_Bang' / 'Art' / 'UI' / 'Craft'
OUT.mkdir(parents=True, exist_ok=True)

def plain(src, dst, size=1024):
    """不平铺：一张直接铺满整张卡片（卡片不到 0.9 m，1024 像素足够清晰），只缩小"""
    cv2.imwrite(str(dst), cv2.resize(cv2.imread(str(src), cv2.IMREAD_COLOR), (size, size), interpolation=cv2.INTER_AREA))

def tileable(src, dst, size=1024, detail=None):
    """四份错位拷贝按权重混合成四边无缝：原图权重 wx·wy，横移半张的 (1-wx)·wy，竖移的 wx·(1-wy)，双移的 (1-wx)(1-wy)。
    每份拷贝自己的接缝正好落在它权重为 0 的中线上，四条外边由错位拷贝提供（内容来自原图中间，绕回去是连续的）。"""
    a = cv2.resize(cv2.imread(str(src), cv2.IMREAD_COLOR), (size, size), interpolation=cv2.INTER_AREA).astype(np.float32)
    h = size // 2
    sx = np.roll(a, h, 1); sy = np.roll(a, h, 0); sxy = np.roll(sx, h, 0)
    t = np.linspace(0, 1, size, dtype=np.float32)
    w1 = np.sin(np.pi * t) ** 2                                   # 边上 0、中间 1，平滑
    wx = w1[None, :, None]; wy = w1[:, None, None]
    out = a * wx * wy + sx * (1 - wx) * wy + sy * wx * (1 - wy) + sxy * (1 - wx) * (1 - wy)
    if detail:
        # 只留纹理细节的灰度图（均值 detail[0]、对比 detail[1]）：颜色由模型顶点色给，不和贴图颜色相乘变暗
        lum = out.mean(axis=2)
        g = detail[0] + (lum - lum.mean()) / max(1e-3, lum.std()) * detail[1] * 255
        out = np.repeat(g[:, :, None], 3, axis=2)
    cv2.imwrite(str(dst), np.clip(out, 0, 255).astype(np.uint8))

if __name__ == '__main__':
    for n, f in (('paper_tile', 'paper.png'), ('kraft_tile', 'kraft.png')):
        if (SRC / f).exists(): plain(SRC / f, OUT / f'{n}.png'); print('tex', n)
    for n, f, d in (('wood_tile', 'wood.png', (225, 0.07)), ('velvet_tile', 'velvet.png', (200, 0.17)), ('floor_tile', 'floor.png', (210, 0.15))):   # 剧场：木纹 / 丝绒要平铺，灰度细节
        if (SRC / f).exists(): tileable(SRC / f, OUT / f'{n}.png', detail=d); print('tile', n)
    for n in ('sticker_star', 'sticker_block', 'sticker_candy'):          # 小熊贴纸弃用（白边抠不干净、表情偏丧）
        cut(SRC / f'{n}.png', OUT / f'{n}.png'); print('cut', n)
    cut(SRC / 'washi_tape.png', OUT / 'washi_tape.png', white=256, max_side=1024); print('cut washi')
