// 彩灯串：在若干挂点之间生成下垂的电线（细管）+ 一颗颗小灯泡（低面数球）+ 灯泡光晕（GlowDots 粒子，Bang/Sparkle）。
// 电线用 ToyVC（顶点色深绿），灯泡用自发光材质；生成的网格存成资产，方便场景保存。
using System.Collections.Generic;
using System.IO;
using Bang.Ambient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bang.EditorTools
{
    public static class BangFairyLights
    {
        const string MeshDir = "Assets/_Bang/Art/Models/Generated";
        static readonly Color[] Bulb = { new(1f, 0.85f, 0.55f), new(1f, 0.7f, 0.8f), new(0.75f, 0.9f, 1f), new(1f, 0.95f, 0.75f), new(0.8f, 1f, 0.8f) };

        public static Material BulbMat => BangArt.Mat("M_FairyBulb", m => { m.SetFloat("_UseVertexColor", 1); m.SetColor("_EmissionColor", new Color(0.9f, 0.75f, 0.55f)); m.SetFloat("_Gloss", 0.8f); m.SetFloat("_Ambient", 0.6f); });

        /// <summary>parent 本地坐标的挂点 anchors，相邻两点之间按 sag（米）下垂；spacing = 灯泡间距。</summary>
        public static GameObject Build(string name, Transform parent, IList<Vector3> anchors, float sag = 0.12f, float spacing = 0.07f, float bulbR = 0.012f)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i + 1 < anchors.Count; i++)
            {
                var a = anchors[i]; var b = anchors[i + 1];
                int n = Mathf.Max(6, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.03f));
                for (int k = (i == 0 ? 0 : 1); k <= n; k++)
                {
                    float t = k / (float)n;
                    pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * sag * 4 * t * (1 - t));
                }
            }
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            // 电线
            var wire = TubeMesh(pts, 0.0028f, 6, new Color(0.22f, 0.4f, 0.3f));
            var bulbs = new List<Vector3>();
            float acc = spacing * 0.5f;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += Vector3.Distance(pts[i - 1], pts[i]);
                if (acc >= spacing) { acc = 0; bulbs.Add(pts[i] + Vector3.down * bulbR * 1.1f); }
            }
            var bulbMesh = BulbsMesh(bulbs, bulbR);
            Child(root.transform, "Wire", Save(wire, name + "_Wire"), BangArt.ToyVC);
            Child(root.transform, "Bulbs", Save(bulbMesh, name + "_Bulbs"), BulbMat);
            // 光晕
            var glow = new GameObject("Glow");
            glow.transform.SetParent(root.transform, false);
            var ps = glow.AddComponent<ParticleSystem>();
            var r = glow.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = BangArt.Mat("M_Sparkle", null, Shader.Find("Bang/Sparkle"));
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var gd = glow.AddComponent<GlowDots>();
            gd.points = bulbs.ToArray();
            gd.colors = new Color[bulbs.Count];
            for (int i = 0; i < bulbs.Count; i++) { var c = Bulb[i % Bulb.Length]; c.a = 0.75f; gd.colors[i] = c; }
            gd.size = bulbR * 6f;
            gd.Emit();
            return root;
        }

        static readonly Color[] Flag = { new(1f, 0.66f, 0.8f), new(0.6f, 0.8f, 1f), new(1f, 0.9f, 0.55f), new(0.78f, 0.68f, 1f), new(0.6f, 0.9f, 0.76f), new(1f, 0.74f, 0.6f) };

        /// <summary>三角旗串：挂点之间下垂的细绳 + 一面面有厚度的绒布三角旗（双面、带小折边）。</summary>
        public static GameObject Bunting(string name, Transform parent, IList<Vector3> anchors, float sag = 0.15f, float spacing = 0.11f, float flag = 0.09f)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i + 1 < anchors.Count; i++)
            {
                var a = anchors[i]; var b = anchors[i + 1];
                int n = Mathf.Max(6, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.03f));
                for (int k = (i == 0 ? 0 : 1); k <= n; k++) { float t = k / (float)n; pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * sag * 4 * t * (1 - t)); }
            }
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            Child(root.transform, "Cord", Save(TubeMesh(pts, 0.0025f, 5, new Color(0.98f, 0.94f, 0.88f)), name + "_Cord"), BangArt.ToyVC);
            var v = new List<Vector3>(); var c = new List<Color>(); var t2 = new List<int>();
            float acc = spacing * 0.5f; int fi = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += Vector3.Distance(pts[i - 1], pts[i]);
                if (acc < spacing) continue;
                acc = 0;
                var dir = (pts[i] - pts[i - 1]).normalized;
                var side = Vector3.Cross(dir, Vector3.up).normalized * 0.004f;      // 旗面厚度
                var p0 = pts[i] - dir * flag * 0.5f; var p1 = pts[i] + dir * flag * 0.5f;
                var tip = pts[i] + Vector3.down * flag * 1.15f + side * 2f * Mathf.Sin(fi * 1.7f);   // 旗尖略微飘
                var col = Flag[fi++ % Flag.Length];
                int b0 = v.Count;
                foreach (var s in new[] { side, -side }) { v.Add(p0 + s); v.Add(p1 + s); v.Add(tip + s * 0.3f); }
                for (int k = 0; k < 6; k++) c.Add(col);
                t2.AddRange(new[] { b0, b0 + 1, b0 + 2, b0 + 3, b0 + 5, b0 + 4 });                           // 正反两面
                t2.AddRange(new[] { b0, b0 + 3, b0 + 4, b0, b0 + 4, b0 + 1, b0 + 1, b0 + 4, b0 + 5, b0 + 1, b0 + 5, b0 + 2, b0 + 2, b0 + 5, b0 + 3, b0 + 2, b0 + 3, b0 });   // 三条边
            }
            for (int k = 0; k < t2.Count; k += 3) (t2[k + 1], t2[k + 2]) = (t2[k + 2], t2[k + 1]);   // 上面按逆时针写的，翻成 Unity 的正面朝外（原来法线朝里 → 看着像破面）
            var m = new Mesh { name = "BuntingFlags" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t2, 0); m.RecalculateNormals(); m.RecalculateBounds();
            Child(root.transform, "Flags", Save(m, name + "_Flags"), BangArt.FeltVC);
            return root;
        }

        /// <summary>挂饰的吊线：从 a 到 b（parent 本地坐标）的一根细线。</summary>
        public static GameObject Thread(string name, Transform parent, Vector3 a, Vector3 b, float r = 0.002f)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 8; i++) pts.Add(Vector3.Lerp(a, b, i / 8f));
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            // 父物体可能被缩放：线的粗细按世界尺寸换算
            float sc = Mathf.Max(parent.lossyScale.x, 1e-4f);
            Child(root.transform, "Thread", Save(TubeMesh(pts, r / sc, 5, new Color(0.96f, 0.92f, 0.86f)), name), BangArt.ToyVC);
            return root;
        }

        static void Child(Transform p, string n, Mesh m, Material mat)
        {
            var g = new GameObject(n); g.transform.SetParent(p, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Mesh Save(Mesh m, string name) => BangSceneBuilder.SaveMesh(m, name);   // 同名自动加序号，免得几根不同长度的线共用一份网格

        static Mesh TubeMesh(List<Vector3> pts, float r, int seg, Color col)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
            Vector3 up = Vector3.up;
            for (int i = 0; i < pts.Count; i++)
            {
                var d = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                var side = Vector3.Cross(d, Mathf.Abs(Vector3.Dot(d, up)) > 0.95f ? Vector3.right : up).normalized;
                var up2 = Vector3.Cross(side, d);
                for (int k = 0; k < seg; k++)
                {
                    float a = k / (float)seg * Mathf.PI * 2;
                    var o = side * Mathf.Cos(a) + up2 * Mathf.Sin(a);
                    v.Add(pts[i] + o * r); n.Add(o); c.Add(col);
                }
                if (i > 0)
                    for (int k = 0; k < seg; k++)
                    {
                        int a0 = (i - 1) * seg + k, a1 = (i - 1) * seg + (k + 1) % seg, b0 = i * seg + k, b1 = i * seg + (k + 1) % seg;
                        t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                    }
            }
            var m = new Mesh { name = "FairyWire" }; m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        static Mesh BulbsMesh(List<Vector3> centers, float r)
        {
            // 小灯泡：略长的低面数球（8 段 × 6 环）+ 顶上一圈深色灯座
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
            const int S = 8, R = 6;
            for (int b = 0; b < centers.Count; b++)
            {
                var col = Bulb[b % Bulb.Length];
                int baseIdx = v.Count;
                for (int j = 0; j <= R; j++)
                {
                    float phi = Mathf.PI * j / R;
                    for (int k = 0; k <= S; k++)
                    {
                        float th = Mathf.PI * 2 * k / S;
                        var o = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        v.Add(centers[b] + Vector3.Scale(o, new Vector3(r, r * 1.35f, r))); n.Add(o);
                        c.Add(j <= 1 ? new Color(0.3f, 0.45f, 0.35f) : col);
                    }
                }
                for (int j = 0; j < R; j++)
                    for (int k = 0; k < S; k++)
                    {
                        int a0 = baseIdx + j * (S + 1) + k, a1 = a0 + 1, b0 = a0 + S + 1, b1 = b0 + 1;
                        t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                    }
            }
            var m = new Mesh { name = "FairyBulbs", indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }
    }
}
