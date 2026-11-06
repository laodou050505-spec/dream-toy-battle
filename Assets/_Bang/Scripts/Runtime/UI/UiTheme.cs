// UI 主题资源（编辑器里 BangPlaySetup 装配）：材质、字体、Logo、nano banana 生成的图标 / 手势插图 / 缎带、各章缩略图、配色。
using System;
using TMPro;
using UnityEngine;

namespace Bang.Play.UI
{
    public class UiTheme : MonoBehaviour
    {
        public Material panelMat, panelFrontMat, imageMat, glowMat, logoMat, sparkleMat;
        [Header("手作风格（参考：纸艺信件 / 贴纸）")] public Material craftMat, stickerMat;
        [Header("欧美卡通立体 UI（用户：UI AI 味太重 → 欧美卡通风格、XR 里要立体）")] public Material toonMat, stickerOutlineMat; public Mesh sunburst;
        public Texture2D washiTex; public Texture2D[] stickers;
        [Header("英文版")] public GameObject logoPrefabEn; public Material logoMatEn; public TMPro.TMP_FontAsset fontEn;
        public TMP_FontAsset font;
        public GameObject titleStage;            // 封面玩具小剧场（BangTitleStage 生成）
        public GameObject logoPrefab;
        public Texture2D ribbon, glow;
        [Serializable] public struct Named { public string name; public Texture2D tex; }
        public Named[] icons, gestures;
        public Texture2D[] chapterThumbs;                 // 第 1~6 章

        public Texture2D Icon(string n) { foreach (var i in icons) if (i.name == n) return i.tex; return null; }
        public Texture2D Gesture(string n) { foreach (var i in gestures) if (i.name == n) return i.tex; return null; }

        // 配色：奶油底 + 粉彩强调色（与场景的糖果玩具风格一致）
        public static readonly Color Cream = new(1f, 0.97f, 0.93f), CreamB = new(1f, 0.9f, 0.93f);
        public static readonly Color Peach = new(1f, 0.66f, 0.56f), PeachB = new(0.98f, 0.5f, 0.5f);
        public static readonly Color Mint = new(0.62f, 0.92f, 0.8f), MintB = new(0.42f, 0.8f, 0.7f);
        public static readonly Color Lav = new(0.8f, 0.72f, 1f), LavB = new(0.64f, 0.55f, 0.95f);
        public static readonly Color Sky = new(0.66f, 0.84f, 1f), SkyB = new(0.48f, 0.68f, 0.98f);
        public static readonly Color Ink = new(0.42f, 0.27f, 0.48f);          // 正文字色（深莓紫，在奶油卡片上清楚）
        public static readonly Color32 Outline = new(120, 72, 140, 255);
    }
}
