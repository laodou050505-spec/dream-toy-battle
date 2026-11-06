// UI 构件（全部是世界空间里的 3D 物体，XR 里就是浮在房间里的糖果卡片）：
//   卡片 = Bang/UIPanel 程序化圆角矩形；图片 = 带透明度的贴片；文字 = TextMeshPro（站酷快乐体）；按钮 = 底座 + 前面板（有真实厚度）。
// 绘制顺序靠渲染队列固定：卡片 3100 < 按钮底座 3102 < 按钮面板 3104 < 图片 3110 < 文字 3120（叠在一起时不会互相穿插）。
using System;
using TMPro;
using UnityEngine;

namespace Bang.Play.UI
{
    public static class UiKit
    {
        public static UiTheme T;
        static Material _textMat, _textMatEn;
        /// <summary>当前语言用的字体：英文 Lilita One，中文站酷庆科黄油体（都是 OFL 卡通粗体）。</summary>
        public static TMP_FontAsset Font => Bang.Logic.Loc.En && T.fontEn ? T.fontEn : T.font;
        static readonly int IdA = Shader.PropertyToID("_ColorA"), IdB = Shader.PropertyToID("_ColorB"), IdBorderC = Shader.PropertyToID("_BorderColor"),
            IdBorder = Shader.PropertyToID("_Border"), IdRadius = Shader.PropertyToID("_Radius"), IdSize = Shader.PropertyToID("_Size"), IdPad = Shader.PropertyToID("_Pad"),
            IdShadow = Shader.PropertyToID("_Shadow"), IdGlow = Shader.PropertyToID("_Glow"), IdGloss = Shader.PropertyToID("_Gloss"), IdAlpha = Shader.PropertyToID("_Alpha"),
            IdBase = Shader.PropertyToID("_BaseMap"), IdColor = Shader.PropertyToID("_Color"), IdInset = Shader.PropertyToID("_Inset");

        public static Transform Node(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t;
        }

        static MeshRenderer QuadR(Transform parent, string name, Material mat)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        /// <summary>糖果卡片。size 是卡片本身的宽高（米），四周额外留 pad 画投影。</summary>
        public static MeshRenderer Panel(Transform parent, Vector2 size, Color top, Color bottom, float radius = 0.04f, float border = 0.005f, Color? borderCol = null,
                                         float shadow = 0.35f, float pad = 0.04f, float glow = 0.35f, float gloss = 0.35f, bool front = false)
        {
            var r = QuadR(parent, "Panel", front ? T.panelFrontMat : T.panelMat);
            r.transform.localScale = new Vector3(size.x + pad * 2, size.y + pad * 2, 1);
            var b = new MaterialPropertyBlock();
            b.SetColor(IdA, top); b.SetColor(IdB, bottom); b.SetColor(IdBorderC, borderCol ?? Color.white);
            b.SetFloat(IdBorder, border); b.SetFloat(IdRadius, radius); b.SetVector(IdSize, size); b.SetFloat(IdPad, pad);
            b.SetFloat(IdShadow, shadow); b.SetFloat(IdGlow, glow); b.SetFloat(IdGloss, gloss); b.SetFloat(IdAlpha, 1);
            r.SetPropertyBlock(b);
            return r;
        }

        public static void SetPanelColors(MeshRenderer r, Color top, Color bottom)
        {
            var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b); b.SetColor(IdA, top); b.SetColor(IdB, bottom); r.SetPropertyBlock(b);
        }

        public static void SetPanelSize(MeshRenderer r, Vector2 size, float pad)
        {
            var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b); b.SetVector(IdSize, size); r.SetPropertyBlock(b);
            r.transform.localScale = new Vector3(size.x + pad * 2, size.y + pad * 2, 1);
        }

        public static void SetAlpha(Renderer r, float a)
        {
            var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b);
            if (r.sharedMaterial && r.sharedMaterial.HasProperty(IdAlpha) && r.sharedMaterial.shader.name.StartsWith("Bang/UI") && r.sharedMaterial.shader.name != "Bang/UIImage") b.SetFloat(IdAlpha, a);
            else { var c = b.GetColor(IdColor); if (c == default) c = Color.white; c.a = a; b.SetColor(IdColor, c); }
            r.SetPropertyBlock(b);
        }

        /// <summary>图片贴片（按贴图宽高比，高度 h 米）。</summary>
        public static MeshRenderer Image(Transform parent, Texture2D tex, float h, Color? tint = null, Material mat = null)
        {
            var r = QuadR(parent, tex ? tex.name : "Image", mat ? mat : T.imageMat);
            float aspect = tex ? tex.width / (float)tex.height : 1f;
            r.transform.localScale = new Vector3(h * aspect, h, 1);
            var b = new MaterialPropertyBlock(); if (tex) b.SetTexture(IdBase, tex); b.SetColor(IdColor, tint ?? Color.white); r.SetPropertyBlock(b);
            return r;
        }

        public static void SetTint(Renderer r, Color c) { var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b); b.SetColor(IdColor, c); r.SetPropertyBlock(b); }

        /// <summary>文字。size = 字高（米，约）。</summary>
        public static TextMeshPro Text(Transform parent, string s, float size, Color col, float width = 1f, TextAlignmentOptions align = TextAlignmentOptions.Center, float outline = 0.18f)
        {
            var g = new GameObject("Text"); g.transform.SetParent(parent, false);
            var t = g.AddComponent<TextMeshPro>();
            bool en = Bang.Logic.Loc.En && T.fontEn;
            t.font = Font;
            if (en) { if (!_textMatEn) _textMatEn = new Material(T.fontEn.material) { name = "UITextEn", renderQueue = 3120 }; t.fontSharedMaterial = _textMatEn; }
            else { if (!_textMat) _textMat = new Material(T.font.material) { name = "UIText", renderQueue = 3120 }; t.fontSharedMaterial = _textMat; }
            t.fontSize = size * 10f; t.color = col; t.alignment = align; t.enableWordWrapping = true;
            t.outlineWidth = outline; t.outlineColor = T && T.toonMat ? (Color32)ToonInk : UiTheme.Outline;   // 卡通风：文字描边统一深藏青
            t.rectTransform.sizeDelta = new Vector2(width, size * 3f);
            t.text = s;
            t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        // ───────── 欧美卡通立体 UI：克制的设计规则 ─────────
        // 只用四个主色 + 一个描边色，各有固定用途：红 = 主按钮、黄 = 次按钮、蓝 = 返回 / 中性、奶油 = 菜单板；
        // 平涂、深梅紫粗描边（底边加厚成“底唇”）、侧壁暗一档；不用渐变、柔光、高光、网点、闪光
        // 配色和封面放射条纹同一组：玫红 / 浅粉 / 淡紫 / 象牙白 + 深梅紫描边
        public static readonly Color ToonInk = new(0.27f, 0.11f, 0.29f);               // 和封面描边、标题底板同一个深梅紫
        public static readonly Color ToonRed = new(0.92f, 0.28f, 0.42f), ToonYellow = new(1f, 0.78f, 0.84f), ToonBlue = new(0.82f, 0.74f, 0.98f), ToonCream = new(0.99f, 0.95f, 0.87f);
        static Color Side(Color c) => new(c.r * 0.72f, c.g * 0.66f, c.b * 0.7f);
        static readonly System.Collections.Generic.Dictionary<string, Mesh> _slabs = new();

        /// <summary>有厚度的圆角板（正面在 z=0、朝 -z，往 +z 延伸 depth）：正面颜色 face、侧壁 side，烘进顶点色（线性空间）。</summary>
        public static Mesh Slab(Vector2 size, float radius, float depth, Color face, Color side)
        {
            string key = $"{size.x:0.###}_{size.y:0.###}_{radius:0.###}_{depth:0.###}_{ColorUtility.ToHtmlStringRGB(face)}_{ColorUtility.ToHtmlStringRGB(side)}";
            if (_slabs.TryGetValue(key, out var cached) && cached) return cached;
            radius = Mathf.Min(radius, Mathf.Min(size.x, size.y) * 0.5f - 1e-4f);
            const int seg = 8;
            var ring = new System.Collections.Generic.List<Vector2>();
            float hx = size.x * 0.5f - radius, hy = size.y * 0.5f - radius;
            var centers = new[] { new Vector2(hx, hy), new Vector2(-hx, hy), new Vector2(-hx, -hy), new Vector2(hx, -hy) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++) { float a = (c * 90f + i * 90f / seg) * Mathf.Deg2Rad; ring.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius); }
            var v = new System.Collections.Generic.List<Vector3>(); var col = new System.Collections.Generic.List<Color>(); var t = new System.Collections.Generic.List<int>();
            Color fl = face.linear, sl = side.linear;
            int n = ring.Count;
            // 正面（扇形，绕序朝 -z）
            int c0 = v.Count; v.Add(Vector3.zero); col.Add(fl);
            for (int i = 0; i < n; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, 0)); col.Add(fl); }
            for (int i = 0; i < n; i++) t.AddRange(new[] { c0, c0 + 1 + (i + 1) % n, c0 + 1 + i });
            // 侧壁
            int s0 = v.Count;
            for (int i = 0; i < n; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, 0)); v.Add(new Vector3(ring[i].x, ring[i].y, depth)); col.Add(sl); col.Add(sl); }
            for (int i = 0; i < n; i++) { int a = s0 + i * 2, b = s0 + ((i + 1) % n) * 2; t.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b }); }
            // 背面
            int b0 = v.Count; v.Add(new Vector3(0, 0, depth)); col.Add(sl);
            for (int i = 0; i < n; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, depth)); col.Add(sl); }
            for (int i = 0; i < n; i++) t.AddRange(new[] { b0, b0 + 1 + i, b0 + 1 + (i + 1) % n });
            var m = new Mesh { name = "Slab" }; m.SetVertices(v); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            // 绕序自检：正面三角形法线必须朝 -z（朝玩家），反了就整体翻
            var nn = m.normals; if (nn[1].z > 0) { var tt = m.triangles; for (int i = 0; i < tt.Length; i += 3) (tt[i + 1], tt[i + 2]) = (tt[i + 2], tt[i + 1]); m.triangles = tt; m.RecalculateNormals(); }
            _slabs[key] = m; return m;
        }

        static MeshRenderer SlabR(Transform parent, string name, Mesh mesh)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = T.toonMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        /// <summary>卡通立体板：正面 + 侧壁 + 后面垫一块略大的深藏青板当描边（底边更厚 = 底唇）。返回正面那块。</summary>
        public static MeshRenderer ToonSlab(Transform parent, Vector2 size, Color face, float depth = 0.02f, float outline = 0.007f, float lip = 0.007f)
        {
            float rad = Mathf.Min(0.045f, Mathf.Min(size.x, size.y) * 0.3f);
            var ink = SlabR(parent, "Outline", Slab(size + new Vector2(outline * 2, outline * 2 + lip), rad + outline, depth + 0.006f, ToonInk, ToonInk));
            ink.transform.localPosition = new Vector3(0, -lip * 0.5f, 0.003f);
            return SlabR(parent, "Face", Slab(size, rad, depth, face, Side(face)));
        }

        public enum BtnStyle { Primary, Secondary, Ghost }
        public static readonly Color Plum = new(0.45f, 0.29f, 0.6f), PlumB = new(0.4f, 0.25f, 0.55f), CreamText = new(1f, 0.97f, 0.92f);
        public static readonly Color Paper = new(1f, 0.975f, 0.95f, 0.97f), Ink = new(0.12f, 0.13f, 0.27f);
        public static readonly Color Butter = new(1f, 0.93f, 0.76f), ButterB = new(0.99f, 0.86f, 0.66f);
        public static readonly Color Coral = new(1f, 0.58f, 0.62f), CoralB = new(0.94f, 0.47f, 0.54f), BoardInk = new(0.27f, 0.11f, 0.29f), PlumDark = new(0.3f, 0.15f, 0.36f);

        /// <summary>菜单板：手作卡片——厚牛皮纸板上贴一张带毛边的奶油纸，顶上一截和纸胶带，角上一枚玩具贴纸（参考纸艺信件 / 贴纸手账的质感），
        /// 上面用墨色字。没配手作材质时退回旧的深莓绒布板。</summary>
        public static MeshRenderer Board(Transform parent, Vector2 size, bool tape = true)
        {
            if (T.toonMat)                                                // 欧美卡通：奶油色厚板 + 深梅紫粗描边（不要牛皮纸 / 胶带 / 贴纸）
            {
                var holder = Node(parent, "Board", new Vector3(0, 0, 0.012f));
                return ToonSlab(holder, size, ToonCream, 0.03f, 0.009f, 0.01f);
            }
            if (!T.craftMat)
                return Panel(parent, size, new Color(0.43f, 0.26f, 0.52f, 0.97f), new Color(0.34f, 0.19f, 0.42f, 0.97f), Mathf.Min(0.06f, size.y * 0.2f), 0.013f, Coral, 0.4f, 0.05f, 0.04f, 0.06f);
            const float pad = 0.05f;
            var r = QuadR(parent, "Board", T.craftMat);
            r.transform.localScale = new Vector3(size.x + pad * 2, size.y + pad * 2, 1);
            var b = new MaterialPropertyBlock();
            b.SetVector(IdSize, size); b.SetFloat(IdPad, pad); b.SetFloat(IdRadius, Mathf.Min(0.035f, size.y * 0.25f));
            b.SetFloat(IdInset, Mathf.Min(0.022f, size.y * 0.12f)); b.SetFloat(IdAlpha, 1);
            r.SetPropertyBlock(b);
            return r;
        }

        /// <summary>按钮（克制的层级，不用渐变糖果胶囊 + 彩色图标那一套）：
        ///   Primary：实心深莓紫，奶油色字，底座只比面板深一点点；一屏只放一个
        ///   Secondary：奶油纸色面板 + 细墨色描边，墨色字
        ///   Ghost：平时只有字，看向它时才浮出一层淡底（用于“设置”“返回”这类次要操作）</summary>
        public static UiButton Button(Transform parent, string label, Vector2 size, BtnStyle style, Action onClick, float textSize = 0.045f)
        {
            var root = Node(parent, "Btn_" + label, Vector3.zero);
            var btn = root.gameObject.AddComponent<UiButton>();
            Color face = style == BtnStyle.Primary ? Coral : Butter, faceB = style == BtnStyle.Primary ? CoralB : ButterB;
            float rad = Mathf.Min(size.y * 0.5f, 0.05f);
            MeshRenderer faceR; var face_ = Node(root, "Face", new Vector3(0, 0, -0.01f));
            if (T.toonMat)
            {
                // 欧美卡通立体按键：主 = 玫红、次 = 浅粉、幽灵（返回 / 主菜单）= 淡紫，和封面放射条纹同色；按键本体有 2.2 cm 厚，按下时整块往里压（UiButton.pressMove）
                face = style == BtnStyle.Primary ? ToonRed : style == BtnStyle.Ghost ? ToonBlue : ToonYellow; faceB = face;
                var baseT = Node(root, "Base", new Vector3(0, 0, 0.012f));
                var well = SlabR(baseT, "Well", Slab(size + new Vector2(0.016f, 0.02f), Mathf.Min(size.y * 0.5f, 0.05f) + 0.008f, 0.012f, ToonInk, ToonInk));
                well.transform.localPosition = new Vector3(0, -0.004f, 0);
                faceR = SlabR(face_, "Key", Slab(size, Mathf.Min(size.y * 0.5f, 0.05f), 0.022f, face, Side(face)));
                var tt = Text(face_, label, textSize * 1.06f, style == BtnStyle.Primary ? Color.white : ToonInk, size.x - 0.03f, TMPro.TextAlignmentOptions.Center, style == BtnStyle.Primary ? 0.28f : 0);
                tt.characterSpacing = 2; tt.fontStyle = TMPro.FontStyles.Bold;
                tt.transform.localPosition = new Vector3(0, -textSize * 0.06f, -0.003f);
                var bc = root.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(size.x, size.y, 0.04f); bc.center = new Vector3(0, 0, 0);
                btn.face = face_; btn.faceR = faceR; btn.onClick = onClick; btn.colA = face; btn.colB = faceB; btn.ghost = false; btn.label = tt;
                btn.hoverTint = Color.white; btn.hoverMove = new Vector3(0, 0, -0.006f); btn.pressMove = new Vector3(0, 0, 0.012f); btn.hoverScale = 0.03f;
                return btn;
            }
            if (T.stickerMat)
            {
                // 贴纸按钮：底色 + 白色模切边 + 纸纹 + 落影（一层就够，不再叠底座）
                faceR = QuadR(face_, "Sticker", T.stickerMat);
                const float pad = 0.022f;
                faceR.transform.localScale = new Vector3(size.x + pad * 2, size.y + pad * 2, 1);
                var b = new MaterialPropertyBlock();
                b.SetColor(IdA, face); b.SetColor(IdB, faceB); b.SetColor(IdBorderC, new Color(1f, 0.99f, 0.97f, 1f));
                b.SetFloat(IdBorder, Mathf.Clamp(size.y * 0.09f, 0.004f, 0.008f)); b.SetFloat(IdRadius, rad); b.SetVector(IdSize, size); b.SetFloat(IdPad, pad);
                b.SetFloat(IdShadow, style == BtnStyle.Ghost ? 0f : 0.3f); b.SetFloat(IdGloss, 0.35f); b.SetFloat(IdAlpha, 1);
                faceR.SetPropertyBlock(b);
            }
            else
            {
                if (style != BtnStyle.Ghost)
                {
                    var baseR = Panel(root, size, faceB * 0.86f + new Color(0, 0, 0, 0.14f), faceB * 0.8f + new Color(0, 0, 0, 0.2f), rad, 0, null, 0.28f, 0.03f, 0, 0);
                    baseR.transform.localPosition = new Vector3(0, -0.003f, 0.004f);
                }
                var border = style == BtnStyle.Secondary ? new Color(Ink.r, Ink.g, Ink.b, 0.35f) : new Color(1, 1, 1, 0.18f);
                faceR = Panel(face_, size, face, faceB, rad, style == BtnStyle.Secondary ? 0.0022f : 0.0016f, border, 0f, 0.01f, 0.08f, 0.14f, true);
            }
            var t = Text(face_, label, textSize, style == BtnStyle.Primary ? PlumDark : Ink, size.x - 0.02f, TMPro.TextAlignmentOptions.Center, 0);
            t.characterSpacing = style == BtnStyle.Primary ? 6 : 3;
            t.transform.localPosition = new Vector3(0, -textSize * 0.08f, -0.003f);
            var col = root.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(size.x, size.y, 0.03f); col.center = new Vector3(0, 0, -0.008f);
            btn.face = face_; btn.faceR = faceR; btn.onClick = onClick; btn.colA = face; btn.colB = faceB; btn.ghost = style == BtnStyle.Ghost; btn.label = t;
            btn.hoverTint = style == BtnStyle.Primary ? new Color(1f, 0.72f, 0.74f) : new Color(1f, 0.97f, 0.88f, 1f);
            return btn;
        }

        /// <summary>旧接口（彩色糖果按钮）：按颜色映射到新层级——桃色 = 主按钮，其余 = 次按钮；不再显示彩色图标。</summary>
        public static UiButton Button(Transform parent, string label, Vector2 size, Color face, Color faceB, Texture2D icon, Action onClick, float textSize = 0.05f)
            => Button(parent, label, size, face == UiTheme.Peach ? BtnStyle.Primary : face == UiTheme.Sky ? BtnStyle.Ghost : BtnStyle.Secondary, onClick, textSize * 0.92f);

        /// <summary>菜单背后的放射圆盘（和封面同一个网格）：所有菜单和封面是一套视觉。radius = 含外框的半径。</summary>
        public static void Burst(Transform parent, Vector3 center, float radius)
        {
            if (!T.sunburst || !T.toonMat) return;
            var g = new GameObject("Burst"); g.transform.SetParent(parent, false); g.transform.localPosition = center;
            g.transform.localScale = Vector3.one * (radius / T.sunburst.bounds.extents.x);
            g.AddComponent<MeshFilter>().sharedMesh = T.sunburst;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = T.toonMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var spin = g.AddComponent<Bang.Ambient.Spin>(); spin.axis = Vector3.forward; spin.degPerSec = 3.5f;
        }

        /// <summary>弹出动画用：easeOutBack。</summary>
        public static float Back(float t) { t = Mathf.Clamp01(t) - 1f; return 1f + 2.70158f * t * t * t + 1.70158f * t * t; }
    }
}
