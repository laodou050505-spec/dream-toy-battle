// 不规则门洞：读 Tools/portal_outline.py 写出的 Assets/_Bang/Art/Models/Proc/Portal/portal_outline_Wn.json
// （outline = 门洞轮廓；path12/16/20/30 = 门框中心线，外扩 0.12/0.16/0.2/0.3 米，从右下贴地 → 顶 → 左下贴地），
// 生成门洞（遮罩 + 涡旋膜 + 发光内沿），以及沿门框中心线取点（摆 ABC 积木、挂饰）。
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Bang.Portal;
using UnityEngine;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        static readonly Dictionary<string, Vector2[]> _shapes = new();

        public static Vector2[] PortalPts(int ch, string key = "outline")
        {
            string id = ch + ":" + key;
            if (_shapes.TryGetValue(id, out var cached)) return cached;
            var path = $"Assets/_Bang/Art/Models/Proc/Portal/portal_outline_W{ch}.json";
            if (!File.Exists(path)) { Debug.LogWarning("[Bang] 缺少门洞轮廓 " + path); return null; }
            var txt = File.ReadAllText(path);
            int k = txt.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
            if (k < 0) return null;
            int a = txt.IndexOf('[', k), depth = 0, b = a;
            for (; b < txt.Length; b++) { if (txt[b] == '[') depth++; else if (txt[b] == ']' && --depth == 0) break; }
            var nums = new List<float>();
            foreach (var tok in txt.Substring(a, b - a + 1).Split(new[] { '[', ']', ',', ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries))
                nums.Add(float.Parse(tok, CultureInfo.InvariantCulture));
            var pts = new Vector2[nums.Count / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(nums[i * 2], nums[i * 2 + 1]);
            _shapes[id] = pts;
            return pts;
        }

        static Material PortalVortex => BangArt.Mat("M_PortalVortex", null, Shader.Find("Bang/PortalVortex"));

        /// <summary>不规则门洞（遮罩 + 涡旋膜 + 发光内沿），挂在 portal 下。</summary>
        static PortalArch PortalOpening(Transform portal, int ch, int stencilRef)
        {
            var arch = new GameObject("Arch").AddComponent<PortalArch>();
            arch.transform.SetParent(portal, false);
            arch.outline = PortalPts(ch);
            arch.stencilRef = stencilRef;
            arch.maskMaterial = BangArt.PortalMask(stencilRef);
            arch.filmMaterial = PortalVortex;
            arch.rimMaterial = BangArt.PortalRim;
            arch.Build();   // 组件添加时材质还是空的，赋值后要重建一次
            return arch;
        }

        /// <summary>沿门框中心线（key = path12/16/20/30）按弧长比例 f（0 = 右下，1 = 左下）取点、切线、外法线。</summary>
        static (Vector2 p, Vector2 t, Vector2 n) PathAt(int ch, string key, float f)
        {
            var pts = PortalPts(ch, key);
            var L = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++) L[i] = L[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            float t = Mathf.Clamp01(f) * L[^1];
            int j = 1; while (j < pts.Length - 1 && L[j] < t) j++;
            float u = Mathf.InverseLerp(L[j - 1], L[j], t);
            var p = Vector2.Lerp(pts[j - 1], pts[j], u);
            var tg = (pts[j] - pts[j - 1]).normalized;
            return (p, tg, new Vector2(tg.y, -tg.x));
        }

        static float PathLength(int ch, string key)
        {
            var pts = PortalPts(ch, key); float L = 0;
            for (int i = 1; i < pts.Length; i++) L += Vector2.Distance(pts[i - 1], pts[i]);
            return L;
        }
    }
}
