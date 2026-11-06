// 程序网格小工具（门后世界的布景、室内外壳共用）：保存网格资产、朝内圆柱墙、圆盘、毛毡地毯、细线吊挂。
using System.Collections.Generic;
using Bang.Ambient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        /// <summary>从 ceilingY 高度用细线吊一个 P2 模型：吊点上挂 Pendulum（线长决定周期），模型挂在吊点下。</summary>
        static GameObject HangFromCeiling(string name, Transform parent, Vector3 pos, float scale, float ceilingY, float amp = 2.5f, float twist = 8f)
        {
            var g = Place(name, parent, pos, Random.Range(-30f, 30f));
            if (!g) return null;
            g.transform.localScale *= scale;
            var b = Bounds(g);
            var top = parent.InverseTransformPoint(new Vector3(b.center.x, b.max.y - 0.02f * scale, b.center.z));
            var pivot = new GameObject(name + "_Hang").transform; pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(top.x, ceilingY, top.z);
            g.transform.SetParent(pivot, true);
            BangFairyLights.Thread(name + "_Thread", pivot, Vector3.zero, top - pivot.localPosition, 0.004f);
            var pd = pivot.gameObject.AddComponent<Pendulum>(); pd.length = ceilingY - top.y; pd.amplitudeDeg = amp; pd.twistDeg = twist;
            return g;
        }

        /// <summary>房间四周放大过的道具提早切到低一级精度：切换阈值整体乘 k（放大后它们一直停在最高精度，4–10 m 外看不出区别）。</summary>
        static void LodPush(Transform root, float k)
        {
            return;   // 不再提前降精度（用户：头显里模型破面——低级 LOD 形状崩坏），保留调用点以便以后按性能需要再开
            foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lg.GetLODs();
                float prev = 1f;
                for (int i = 0; i < lods.Length; i++) { lods[i].screenRelativeTransitionHeight = Mathf.Min(prev * 0.98f, lods[i].screenRelativeTransitionHeight * k); prev = lods[i].screenRelativeTransitionHeight; }
                lg.SetLODs(lods);
            }
        }

        /// <summary>投影白名单：花草、小件（高 &lt; 0.45 m）、地面、轨道、小路、天空、粒子不投影——
        /// 它们的影子本来就看不清，却要把整个网格再画进阴影图。房子、树、帐篷这类有体量的照常投影。</summary>
        static void ShadowPolicy(Transform w)
        {
            string[] never = { "Flora", "Spill", "Clutter", "Ground", "Apron", "Sky", "Track", "Path", "Rug", "Floor", "Wall", "Ceiling", "Beam", "Shelf", "Thread", "Wire", "Bulb", "Flags", "Decal", "Porthole", "Rail", "Wain", "Base_" };
            foreach (var r in w.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; continue; }
                bool off = r.bounds.size.y < 0.45f;
                for (var t = r.transform; !off && t && t != w; t = t.parent) foreach (var k in never) if (t.name.Contains(k)) { off = true; break; }
                if (off) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>天上的云（或毛毡月亮星星）：放好后挂 Drift，顺着风向几十秒一个来回地漂。</summary>
        static GameObject FloatCloud(string name, Transform parent, Vector3 pos, float scale)
        {
            var g = Place(name, parent, pos, 0);
            if (!g) return null;
            g.transform.localScale *= scale;
            var d = g.AddComponent<Drift>(); d.amplitude = 0.3f + 0.1f * scale; d.period = Random.Range(32f, 52f);
            return g;
        }

        static GameObject MeshObj(string n, Transform parent, Mesh m, Material mat)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m, n);
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        // 本次构建里已经用过的网格名：同名的自动加序号（构建顺序固定 → 序号稳定），免得多个物体共用最后一份几何
        static readonly Dictionary<string, int> _meshNames = new();
        static void ResetMeshNames() => _meshNames.Clear();

        internal static Mesh SaveMesh(Mesh m, string name)
        {
            const string dir = "Assets/_Bang/Art/Models/Generated";
            System.IO.Directory.CreateDirectory(dir);
            if (_meshNames.TryGetValue(name, out int k)) { _meshNames[name] = k + 1; name = $"{name}_{k + 1}"; }
            else _meshNames[name] = 0;
            m.name = name;
            var path = $"{dir}/{name}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old) { old.Clear(); EditorUtility.CopySerialized(m, old); old.name = name; Object.DestroyImmediate(m); return old; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>朝内的圆柱墙：u 绕一圈 repeatU 次，v = 高度比例。</summary>
        static Mesh CylinderInner(float r, float h, int seg, float repeatU)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                v.Add(p); v.Add(p + Vector3.up * h);
                float u = i / (float)seg * repeatU;
                uv.Add(new Vector2(-u, 0)); uv.Add(new Vector2(-u, 1));            // 从里面看：u 反向，画面不左右镜像
                if (i < seg) { int k = i * 2; t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            }
            var m = new Mesh { name = "CylinderInner" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>水平圆盘，平面 UV（世界米 × uvPerMeter）；faceDown = 正面朝下（天花板）。</summary>
        static Mesh Disc(float r, int seg, float uvPerMeter, bool faceDown)
        {
            var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { Vector2.zero }; var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                v.Add(p); uv.Add(new Vector2(p.x, p.z) * uvPerMeter);
                if (i < seg) { if (faceDown) t.AddRange(new[] { 0, i + 1, i + 2 }); else t.AddRange(new[] { 0, i + 2, i + 1 }); }
            }
            var m = new Mesh { name = "Disc" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>毛毡地毯：扇贝边的薄圆饼（有厚度、边缘圆滑），顶面几圈同心色带 + 一圈缝线色。</summary>
        static Mesh RugMesh(float r, Color[] bands, int seg = 144)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            const float th = 0.025f;
            float R(float a) => r * (1 + 0.035f * Mathf.Cos(a * 18));                     // 扇贝边
            Color Band(float k)
            {
                if (Mathf.Abs(k - 0.9f) < 0.012f) return new Color(1f, 0.98f, 0.94f);        // 缝线
                int bi = k < 0.42f ? 0 : k < 0.72f ? 1 : 2;
                return bands[bi % bands.Length];
            }
            float[] rings = { 0f, 0.2f, 0.41f, 0.43f, 0.71f, 0.73f, 0.888f, 0.9f, 0.912f, 0.97f, 1f };
            int nr = rings.Length;
            for (int k = 0; k < nr; k++)
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2, rr = R(a) * rings[k];
                    float y = th * (k == nr - 1 ? 0.45f : 1f);                                // 最外圈下沉一点 → 圆润的边
                    v.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
                    c.Add(Band(Mathf.Clamp01(rings[k] - (k > 0 && rings[k] - rings[k - 1] < 0.02f && k % 2 == 1 ? 0.001f : 0))));
                }
            for (int k = 0; k < nr - 1; k++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = k * seg + i, a1 = k * seg + (i + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                    t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                }
            // 侧边：外圈到地面
            int baseIdx = v.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                v.Add(new Vector3(Mathf.Cos(a) * R(a) * 1.004f, 0, Mathf.Sin(a) * R(a) * 1.004f)); c.Add(bands[2 % bands.Length] * 0.92f);
            }
            int outer = (nr - 1) * seg;
            for (int i = 0; i < seg; i++)
            {
                int a0 = outer + i, a1 = outer + (i + 1) % seg, b0 = baseIdx + i, b1 = baseIdx + (i + 1) % seg;
                t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
            var m = new Mesh { name = "Rug" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
