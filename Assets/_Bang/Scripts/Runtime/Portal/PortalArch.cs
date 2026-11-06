// 门洞：按不规则“虫洞”轮廓（Tools/portal_outline.py 生成，逆时针、底边贴地）生成三样东西：
//   Mask  模板遮罩（耳切三角化，PortalMask 写 _StencilRef，门后世界只在这里画）
//   Film  涡旋膜（从边缘向中心的同心环，uv.x = 绕一圈的弧长比例，uv.y = 0 边缘 → 1 中心；Bang/PortalVortex 画旋向中心的流光）
//   Rim   内沿发光管（沿轮廓扫圆管，顶点色是一圈缓慢渐变的粉彩）
// 局部坐标：门面 z=0，底边 y=0，门后世界在 -z。outline 为空时退回旧的拱形（width × height）。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Portal
{
    [ExecuteAlways]
    public class PortalArch : MonoBehaviour
    {
        [Tooltip("门洞轮廓（逆时针，局部 xy）")] public Vector2[] outline;
        public float width = 1.3f, height = 1.8f;
        [Range(1, 255)] public int stencilRef = 1;
        public Material maskMaterial, filmMaterial, rimMaterial;
        [Tooltip("遮罩略在门面后方，避免与门框内沿共面闪烁")] public float maskDepth = -0.03f;
        public float filmDepth = 0.012f;
        [Tooltip("发光内沿相对轮廓的外扩量：负数 = 贴在洞口里侧（门框积木 / 绒布边从轮廓外 1 cm 起，管子不能埋进去）")]
        public float rimRadius = 0.016f, rimOffset = -0.024f, rimZ = 0.02f;

        /// <summary>场景里所有启用中的门洞（多门章节每扇门一个；门后世界的视锥裁剪按它们算窗口）。</summary>
        public static readonly System.Collections.Generic.List<PortalArch> All = new();
        void OnEnable() { Build(); if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
        void OnValidate() { if (isActiveAndEnabled) Build(); }

        /// <summary>门洞轮廓（局部 xy，逆时针）：开门特效沿着它冒火花。</summary>
        public Vector2[] OutlinePoints => Shape();

        Vector2[] Shape()
        {
            if (outline != null && outline.Length >= 8) return outline;
            float r = width / 2, spring = height - r;
            var o = new List<Vector2> { new(r, 0), new(r, spring) };
            for (int i = 1; i < 24; i++) { float a = Mathf.PI * i / 24; o.Add(new Vector2(Mathf.Cos(a) * r, spring + Mathf.Sin(a) * r)); }
            o.Add(new Vector2(-r, spring)); o.Add(new Vector2(-r, 0));
            return o.ToArray();
        }

        public Bounds LocalBounds
        {
            get
            {
                var s = Shape(); var b = new Bounds(s[0], Vector3.zero);
                foreach (var p in s) b.Encapsulate(p);
                return b;
            }
        }

        public void Build()
        {
            var s = Shape();
            var lb = LocalBounds; width = lb.size.x; height = lb.size.y;
            Child("Mask", maskDepth, maskMaterial, MaskMesh(s));
            Child("Film", filmDepth, filmMaterial, FilmMesh(s));
            Child("Rim", 0, rimMaterial, RimMesh(s, rimOffset, rimZ, rimRadius));
            if (maskMaterial) maskMaterial.SetFloat("_StencilRef", stencilRef | 64);     // 64 位 = 门洞（房间表面据此跳过）
        }

        /// <summary>开门动画用：按给定轮廓（正在往外长的门洞）重建遮罩 / 膜 / 发光内沿；不改 outline 本身。旧网格销毁，不漏内存。</summary>
        public void BuildWith(Vector2[] s)
        {
            foreach (var n in new[] { "Mask", "Film", "Rim" })
            {
                var t = transform.Find(n); var mf = t ? t.GetComponent<MeshFilter>() : null;
                if (mf && mf.sharedMesh && Application.isPlaying && mf.sharedMesh.name == "__grow") Destroy(mf.sharedMesh);
            }
            Mesh M(Mesh m) { m.name = "__grow"; return m; }
            Child("Mask", maskDepth, maskMaterial, M(MaskMesh(s)));
            Child("Film", filmDepth, filmMaterial, M(FilmMesh(s)));
            Child("Rim", 0, rimMaterial, M(RimMesh(s, rimOffset, rimZ, rimRadius)));
        }

        void Child(string n, float z, Material mat, Mesh mesh)
        {
            var t = transform.Find(n);
            if (!t) { t = new GameObject(n).transform; t.SetParent(transform, false); t.gameObject.hideFlags = HideFlags.DontSave; }
            t.localPosition = new Vector3(0, 0, z);
            // 注意：Unity 组件缺失时返回“假 null”，不能用 ?? 判断
            var mf = t.GetComponent<MeshFilter>(); if (!mf) mf = t.gameObject.AddComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>(); if (!mr) mr = t.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = mat;
            mr.enabled = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static Vector2 Centroid(Vector2[] s)
        {
            float a = 0, cx = 0, cy = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var p = s[i]; var q = s[(i + 1) % s.Length];
                float c = p.x * q.y - q.x * p.y; a += c; cx += (p.x + q.x) * c; cy += (p.y + q.y) * c;
            }
            a *= 0.5f;
            return Mathf.Abs(a) < 1e-6f ? s[0] : new Vector2(cx / (6 * a), cy / (6 * a));
        }

        /// <summary>耳切三角化（轮廓不一定是凸的）。</summary>
        public static Mesh MaskMesh(Vector2[] s)
        {
            var idx = new List<int>(); for (int i = 0; i < s.Length; i++) idx.Add(i);
            var tris = new List<int>();
            bool Convex(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) > 1e-9f;
            bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                float d2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
                float d3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
                return d1 >= 0 && d2 >= 0 && d3 >= 0;
            }
            int guard = 0;
            while (idx.Count > 3 && guard++ < 100000)
            {
                bool cut = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int i0 = idx[(k + idx.Count - 1) % idx.Count], i1 = idx[k], i2 = idx[(k + 1) % idx.Count];
                    var a = s[i0]; var b = s[i1]; var c = s[i2];
                    if (!Convex(a, b, c)) continue;
                    bool any = false;
                    foreach (var j in idx) if (j != i0 && j != i1 && j != i2 && Inside(s[j], a, b, c)) { any = true; break; }
                    if (any) continue;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);          // Unity 左手系：顺时针为正面（从 +z 房间一侧看）
                    idx.RemoveAt(k); cut = true; break;
                }
                if (!cut) break;
            }
            if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[2]); tris.Add(idx[1]); }
            var v = new Vector3[s.Length]; for (int i = 0; i < s.Length; i++) v[i] = s[i];
            var m = new Mesh { name = "PortalMask", vertices = v, triangles = tris.ToArray() };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>同心环：从轮廓向质心收缩 rings 圈（轮廓对质心是星形的，环不会交叉）。</summary>
        public static Mesh FilmMesh(Vector2[] s, int rings = 10)
        {
            var c = Centroid(s);
            int n = s.Length;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            var arc = new float[n + 1];
            for (int i = 1; i <= n; i++) arc[i] = arc[i - 1] + Vector2.Distance(s[i - 1], s[i % n]);
            for (int r = 0; r <= rings; r++)
            {
                float k = r / (float)rings, shrink = 1 - Mathf.Pow(k, 0.8f) * 0.985f;
                for (int i = 0; i <= n; i++)
                {
                    var p = s[i % n];
                    v.Add(c + (p - c) * shrink);
                    uv.Add(new Vector2(arc[i] / arc[n], k));
                }
            }
            int row = n + 1;
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < n; i++)
                {
                    int a = r * row + i, b = a + 1, d = a + row, e = d + 1;
                    t.AddRange(new[] { a, d, b, b, d, e });
                }
            var m = new Mesh { name = "PortalFilm" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>沿轮廓外扩 off 的发光圆管，顶点色一圈粉彩渐变。
        /// 只铺离地 minY 以上的部分（门槛和两个下角有小路、地皮、门框底座，管子横在那里必然穿插），两端在最后几个点收细成尖。</summary>
        public static Mesh RimMesh(Vector2[] s, float off, float z, float radius, int seg = 10, float minY = 0.25f)
        {
            int n = s.Length;
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var col = new List<Color>(); var t = new List<int>();
            Color[] pal = { new(1f, 0.78f, 0.9f), new(0.8f, 0.86f, 1f), new(0.78f, 1f, 0.9f), new(1f, 0.95f, 0.75f) };
            // 要铺的点（下标可越过 n 回绕）：从低处之后的第一个高点一直走到下一个低点之前
            var idx = new List<int>();
            int start = -1;
            for (int i = 0; i < n; i++) if (s[i].y <= minY && s[(i + 1) % n].y > minY) { start = i + 1; break; }
            bool closed = start < 0;
            if (closed) for (int i = 0; i <= n; i++) idx.Add(i);
            else for (int k = 0; k < n; k++) { int i = start + k; if (s[i % n].y <= minY) break; idx.Add(i); }
            const int taper = 4;
            for (int q = 0; q < idx.Count; q++)
            {
                int ii = idx[q], i = ii % n;
                var p = s[i]; var a = s[(i + n - 1) % n]; var b = s[(i + 1) % n];
                var tg = (b - a).normalized; var outN = new Vector2(tg.y, -tg.x);
                var center = new Vector3(p.x + outN.x * off, p.y + outN.y * off, z);
                float rr = radius;
                if (!closed) rr *= Mathf.Clamp01(Mathf.Min(q, idx.Count - 1 - q) / (float)taper);   // 两端收尖
                float u = ii / (float)n * pal.Length;
                var cc = Color.Lerp(pal[(int)u % pal.Length], pal[((int)u + 1) % pal.Length], Mathf.SmoothStep(0, 1, u - (int)u));
                for (int k = 0; k < seg; k++)
                {
                    float ang = k / (float)seg * Mathf.PI * 2f;
                    var dir = new Vector3(outN.x, outN.y, 0) * Mathf.Cos(ang) + Vector3.forward * Mathf.Sin(ang);
                    v.Add(center + dir * rr); nr.Add(dir); col.Add(cc);
                }
            }
            for (int i = 0; i < idx.Count - 1; i++)
                for (int k = 0; k < seg; k++)
                {
                    int a = i * seg + k, b = i * seg + (k + 1) % seg, d = a + seg, e = b + seg;
                    t.AddRange(new[] { a, b, d, b, e, d });
                }
            var m = new Mesh { name = "PortalRim" }; m.SetVertices(v); m.SetNormals(nr); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }
    }
}
