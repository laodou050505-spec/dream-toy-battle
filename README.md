# 梦境玩具战 Dream Toy Battle

PICO 头显上的混合现实（MR）手势射击游戏。真实房间的墙上会打开通往玩具世界的传送门，玩具士兵、发条老鼠、纸飞机从门里涌出来，想在你的房间里插满小旗、把房间变成玩具国。你用手比出“手枪”的手势，食指一弹就开枪，把它们打回去。

## 下载安装包（直接玩）

不需要装 Unity。到 [Releases](https://github.com/laodou050505-spec/dream-toy-battle/releases/latest) 下载 `DreamToyBattle_v0.3.1110.apk`（410 MB），然后：

1. PICO 头显开启开发者模式，用 USB 线连接电脑。
2. 运行 `adb install -r DreamToyBattle_v0.3.1110.apk`。
3. 在头显的应用列表里打开「梦境玩具战」。

## 玩法

- **手指枪**：手比成手枪形状，食指往上一弹 = 开一枪；握拳再松开 = 换弹（在金色区间松开会快速换弹）。
- **盾牌**：手掌张开对着前方 = 举盾，挡住敌人的子弹。
- **玩具化**：敌人插下的旗子会让房间一点点“玩具化”，到 100% 就输。
- **13 关 + 无尽模式**：6 个主题世界，每个世界两关，第二关末尾有 Boss；第 13 关“终极决战”六波六个 Boss；通关后解锁无尽模式（每 5 波一个 Boss，场景跟着 Boss 换）。

## 工程

- Unity 6（6000.5.5f1），URP，包名 `com.toybox.bang`
- PICO Unity Integration SDK 6.0.0：`Packages/manifest.json` 里是本机路径引用（`file:/Users/ld/Library/PICO/...`），克隆后需要从 PICO 开发者网站下载 PICO Unity Integration SDK 6.0.0，解压后把这一行改成你本地 `PICO-Unity-SDK/XR` 文件夹的路径，否则工程打不开。
- 不在仓库里：`Library/`（Unity 缓存，打开工程会自动重建）、`Builds/`（APK）、`ArtSource/`（美术原始素材和 Tripo 生成过程文件）。

## 目录

| 路径 | 内容 |
|---|---|
| `Assets/_Bang/Scripts/Logic` | 规则层：纯 C#，不依赖 Unity，可单测。敌人 AI、波次、Boss、伤害、手势识别 |
| `Assets/_Bang/Scripts/Runtime` | 表现层：模型、动画、特效、音效、UI、传送门、MR 房间 |
| `Assets/_Bang/Scripts/Editor` | 编辑器工具：一键搭建 6 个世界场景、平衡模拟、穿模巡检、打包 |
| `Assets/_Bang/Shaders` | 自写着色器：糖果卡通光照、描边、传送门模板遮罩、玩具化覆盖 |
| `Assets/_Bang/Tests` | 单元测试（66 项） |
| `Tools/` | Blender / Python 脚本：程序化建模、Tripo 模型清理、LOD |
| `Docs/` | 平衡模拟结果、音效素材来源 |

## 打包

想从源码自己打包：先按上面「工程」一节装好 PICO SDK，再用 Unity 6000.5.5f1 打开工程（第一次打开要重新导入全部资源，比较慢），菜单 `Bang/打包 APK（PICO）`，输出在 `Builds/`。仓库里的贴图、模型都是原始分辨率，打出来的包和 Releases 里的一致。

## 素材

音乐、音效来自 ange-embed 素材库（清单与来源链接见 `Docs/音效来源.md`）；3D 模型由 Tripo 生成后用 Blender 清理；部分小道具用 Blender 脚本程序化生成。
