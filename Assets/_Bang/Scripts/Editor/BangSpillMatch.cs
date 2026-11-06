// 门外地皮接门里地面（用户：门外地面的材质要和门里一样，现在是一块平涂的纯色，显得简陋）：
// 门后世界的地面网格其实一直铺到房间底下（只是被门洞模板挡着看不见），门外那块地皮的每个顶点按自己的位置
// 到门里地面上取同一点的颜色 / UV，材质换成门里地面的原材质（不带门洞模板的那份）——门里门外是同一块地，无缝接上。
// 菜单：Bang/门外地皮接门里地面（6 个世界场景都处理并保存）
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangSpillMatch
    {
        static readonly List<Mesh> _restore = new();
        class Floor { public Vector3[] v; public Vector2[] uv; public Color[] c; public int[] t; public Material mat; public string name; }

        [MenuItem("Bang/门外地皮接门里地面")]
        public static string Run()
        {
            var log = new System.Text.StringBuilder();
            for (int w = 1; w <= 6; w++)
            {
                string path = $"Assets/_Bang/Scenes/World_W{w}.unity";
                if (!File.Exists(path)) continue;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var floors = new List<Floor>();
                var patches = new List<MeshRenderer>();
                foreach (var go in sc.GetRootGameObjects())
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.name.StartsWith("SpillPatch")) { patches.Add(r); continue; }
                        bool cand = r.name.Contains("_Ground") || r.name.Contains("Shell") || r.name.Contains("Floor");
                        if (!cand || r.GetComponentInParent<LODGroup>()) continue;
                        var f = Grab(r); if (f != null) floors.Add(f);
                    }
                int done = 0;
                foreach (var p in patches) if (Match(p, floors, log)) done++;
                foreach (var p in patches) BakeVariants(p, floors, w, log);
                foreach (var m in _restore) { var so = new SerializedObject(m); so.FindProperty("m_IsReadable").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(m); }
                _restore.Clear();
                EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); EditorSceneManager.CloseScene(sc, true);
                log.Append($"W{w}: 地面 {floors.Count} 块（{string.Join(",", floors.Select(f => f.name))}），地皮 {done}/{patches.Count}\n");
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>多门章节：每种“门角度 + 扇区”一份地皮网格（见 Bang.Portal.SpillVariants）。
        /// 顶点先按扇区裁（与相邻门的角平分线外收到线上，不再和隔壁门的地皮重叠），再按复制后真实的位置到门后地面取 UV / 顶点色。
        /// 门后世界只有 0° 那一份，复制门透过门洞看到的就是那一份里同一个世界坐标的地面——按真实位置取样，门里门外就对上了。</summary>
        static void BakeVariants(MeshRenderer patch, List<Floor> floors, int world, System.Text.StringBuilder log)
        {
            var mf = patch.GetComponent<MeshFilter>(); var src = mf ? mf.sharedMesh : null; if (!src || floors.Count == 0) return;
            var door = patch.transform.parent ? patch.transform.parent.parent : null; if (!door) return;   // SpillPatch → Spill → Portal_Wn
            var room = door.parent;                                                                           // ChapterWorld_Wn：复制门绕它转
            var layouts = new List<double[]>();
            for (int ch = 1; ch <= Bang.Logic.CHAPTERS.Count; ch++)
                if (Bang.Logic.CHAPTERS.World(ch) == world)
                    foreach (bool seated in new[] { false, true }) layouts.Add(Bang.Logic.CHAPTERS.PortalAngles(ch, seated));
            // 无尽模式每个世界都开四扇门（用户：无尽模式的门一直是四个门）：每个世界都要有四门布局的地皮
            foreach (bool seated in new[] { false, true }) layouts.Add(Bang.Logic.CHAPTERS.PortalAngles(Bang.Logic.CHAPTERS.Count, seated));
            var sv = patch.GetComponent<Bang.Portal.SpillVariants>(); if (!sv) sv = patch.gameObject.AddComponent<Bang.Portal.SpillVariants>();
            var A = new List<float>(); var L = new List<float>(); var H = new List<float>(); var M = new List<Mesh>(); var T = new List<Material>();
            string dir = "Assets/_Bang/Art/Models/Generated/SpillVariants"; if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Bang/Art/Models/Generated", "SpillVariants");
            var v0 = src.vertices; float topY = v0.Length > 0 ? v0.Max(q => q.y) : 0;
            foreach (var lay in layouts)
                for (int i = 0; i < lay.Length; i++)
                {
                    float a = (float)lay[i]; Bang.Portal.SpillVariants.Sector(lay, i, out float lo, out float hi);
                    bool dup = false; for (int k = 0; k < A.Count; k++) if (Mathf.Abs(A[k] - a) < 0.5f && Mathf.Abs(L[k] - lo) < 0.5f && Mathf.Abs(H[k] - hi) < 0.5f) dup = true;
                    if (dup) continue;
                    if (lo <= -179f && hi >= 179f && Mathf.Abs(a) < 0.5f) continue;                                // 单门：原网格就是
                    var q = Quaternion.Euler(0, a, 0);
                    var v = new Vector3[v0.Length]; var uv = new Vector2[v0.Length]; var col = new Color[v0.Length];
                    Floor used = null; int hits = 0;
                    for (int j = 0; j < v0.Length; j++)
                    {
                        // 0° 门下的局部点 → 房间坐标 → 转到该门 → 按扇区裁 → 转回 0° 门局部（复制门的子物体局部坐标和 0° 门一致）
                        var roomP = room.InverseTransformPoint(patch.transform.TransformPoint(v0[j]));
                        var rotP = Bang.Portal.SpillVariants.Clip(q * roomP, lo, hi);
                        v[j] = patch.transform.InverseTransformPoint(room.TransformPoint(Quaternion.Inverse(q) * rotP));
                        if (Sample(floors, room.TransformPoint(rotP), out var f, out var su, out var sc)) { hits++; used ??= f; uv[j] = su; col[j] = v0[j].y < topY * 0.5f ? sc * new Color(0.82f, 0.82f, 0.82f, 1f) : sc; }
                    }
                    if (used == null || hits < v0.Length * 0.6f) { log.Append($"  {patch.name} {a:0}°: 只取到 {hits}/{v0.Length}\n"); continue; }
                    var m = Object.Instantiate(src); m.name = $"{src.name}_{a:0}_{lo:0}_{hi:0}"; m.vertices = v; m.uv = uv; m.colors = col; m.RecalculateBounds(); m.RecalculateNormals();
                    string path = $"{dir}/{m.name}.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path);
                    A.Add(a); L.Add(lo); H.Add(hi); M.Add(m); T.Add(BaseOf(used.mat));
                }
            sv.angles = A.ToArray(); sv.lo = L.ToArray(); sv.hi = H.ToArray(); sv.meshes = M.ToArray(); sv.mats = T.ToArray();
            EditorUtility.SetDirty(sv);
            log.Append($"  {patch.name}: 多门版本 {A.Count} 份\n");
        }

        /// <summary>读出朝上的地面三角形（世界坐标）。FBX 不可读时临时打开读写。</summary>
        static Floor Grab(MeshRenderer r)
        {
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return null;
            var mesh = mf.sharedMesh;
            if (!mesh.isReadable)
            {
                var ap = AssetDatabase.GetAssetPath(mesh);
                if (AssetImporter.GetAtPath(ap) is ModelImporter mi) { mi.isReadable = true; mi.SaveAndReimport(); mesh = mf.sharedMesh; }
                else if (ap.EndsWith(".asset"))                                              // 脚本生成的网格资源：临时打开可读，取完样再关回去（不占运行时内存）
                {
                    var so = new SerializedObject(mesh); so.FindProperty("m_IsReadable").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
                    _restore.Add(mesh);
                }
                if (!mesh || !mesh.isReadable) return null;
            }
            var lv = mesh.vertices; var tri = mesh.triangles; var tr = mf.transform;
            var v = new Vector3[lv.Length]; for (int i = 0; i < lv.Length; i++) v[i] = tr.TransformPoint(lv[i]);
            var keep = new List<int>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = v[tri[i]], b = v[tri[i + 1]], c = v[tri[i + 2]];
                var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) continue;
                if (Mathf.Abs(n.normalized.y) < 0.75f) continue;                                  // 只要地面，不要墙
                if (Mathf.Max(a.y, b.y, c.y) > 0.3f || Mathf.Min(a.y, b.y, c.y) < -0.3f) continue;
                keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
            }
            if (keep.Count == 0) return null;
            var uv = mesh.uv; var col = mesh.colors;
            return new Floor { v = v, t = keep.ToArray(), uv = uv.Length == lv.Length ? uv : null, c = col.Length == lv.Length ? col : null, mat = r.sharedMaterial, name = r.name };
        }

        static bool Match(MeshRenderer patch, List<Floor> floors, System.Text.StringBuilder log)
        {
            var mf = patch.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null; if (!mesh || floors.Count == 0) return false;
            var v = mesh.vertices; var oldC = mesh.colors;
            var uv = new Vector2[v.Length]; var c = new Color[v.Length];
            float topY = v.Length > 0 ? v.Max(p => p.y) : 0;
            Floor used = null; int hits = 0;
            for (int i = 0; i < v.Length; i++)
            {
                var wp = patch.transform.TransformPoint(v[i]);
                if (!Sample(floors, wp, out var f, out var su, out var sc)) continue;
                hits++; used ??= f; uv[i] = su;
                bool rim = v[i].y < topY * 0.5f;                                                // 地皮外圈的薄边：压暗一点，看得出厚度
                c[i] = rim ? sc * new Color(0.82f, 0.82f, 0.82f, 1f) : sc;
            }
            if (used == null || hits < v.Length * 0.6f) { log.Append($"  {patch.name}: 只取到 {hits}/{v.Length} 个点，保留原样\n"); return false; }
            mesh.uv = uv; mesh.colors = c;
            EditorUtility.SetDirty(mesh);
            var baseMat = BaseOf(used.mat);
            if (baseMat) patch.sharedMaterial = baseMat;
            EditorUtility.SetDirty(patch);
            log.Append($"  {patch.name}: 接上 {used.name}（材质 {baseMat?.name}），{hits}/{v.Length}\n");
            return true;
        }

        /// <summary>水平面上找包含该点的地面三角形，按重心插值 UV 和颜色；找不到就用最近的三角形。</summary>
        static bool Sample(List<Floor> floors, Vector3 p, out Floor best, out Vector2 uv, out Color col)
        {
            best = null; uv = default; col = Color.white; float bestD = float.MaxValue; int bi = -1; Vector3 bw = default;
            foreach (var f in floors)
                for (int i = 0; i < f.t.Length; i += 3)
                {
                    Vector3 a = f.v[f.t[i]], b = f.v[f.t[i + 1]], c = f.v[f.t[i + 2]];
                    var w = Bary(new Vector2(p.x, p.z), new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z));
                    float outside = Mathf.Max(0, -w.x) + Mathf.Max(0, -w.y) + Mathf.Max(0, -w.z);
                    if (outside < bestD) { bestD = outside; best = f; bi = i; bw = w; if (outside <= 0) goto found; }
                }
            found:
            if (best == null || bestD > 0.5f) return false;
            bw = new Vector3(Mathf.Max(0, bw.x), Mathf.Max(0, bw.y), Mathf.Max(0, bw.z)); bw /= Mathf.Max(1e-5f, bw.x + bw.y + bw.z);
            int i0 = best.t[bi], i1 = best.t[bi + 1], i2 = best.t[bi + 2];
            if (best.uv != null) uv = best.uv[i0] * bw.x + best.uv[i1] * bw.y + best.uv[i2] * bw.z;
            if (best.c != null) col = best.c[i0] * bw.x + best.c[i1] * bw.y + best.c[i2] * bw.z;
            col.a = 1;
            return true;
        }

        static Vector3 Bary(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var v0 = b - a; var v1 = c - a; var v2 = p - a;
            float d = v0.x * v1.y - v1.x * v0.y; if (Mathf.Abs(d) < 1e-9f) return new Vector3(-9, -9, -9);
            float v = (v2.x * v1.y - v1.x * v2.y) / d, w = (v0.x * v2.y - v2.x * v0.y) / d;
            return new Vector3(1 - v - w, v, w);
        }

        /// <summary>门后世界的材质是带门洞模板的变体（名字后缀 _Portal1 之类），找回原材质。</summary>
        static Material BaseOf(Material m)
        {
            if (!m) return null;
            var n = m.name; int k = n.IndexOf("_Portal"); if (k < 0) return m;
            var bn = n.Substring(0, k);
            foreach (var g in AssetDatabase.FindAssets(bn + " t:Material"))
            {
                var mm = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (mm && mm.name == bn) return mm;
            }
            return null;
        }
    }
}
