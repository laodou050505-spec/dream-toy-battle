// 穿模巡检：场景里两件不同的摆件互相插进去（一个的顶点落在另一个封闭网格内部、深度超过阈值）就报出来。
// 做法：每个候选渲染器临时挂一个非凸 MeshCollider；对包围盒相交的两件，取 A 的顶点（抽样）做射线奇偶测试判断是否在 B 里面，
// 在里面就沿 6 个方向找最近的表面估深度。同一件模型的零件（共同祖先在两级以内、且祖先下面渲染器不多）不互查；
// 地面 / 地皮 / 天幕 / 覆盖壳 / 门洞膜这类大平面不参与（物体贴地本来就会略微陷进去）。
// 菜单：Bang/穿模巡检（当前场景）；结果写 Docs/穿模巡检.md。
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangClipAudit
    {
        public class Hit { public Renderer a, b; public float depth; public Vector3 at; public int count; }

        static readonly string[] SkipNames = { "Ground", "SpillPatch", "Sky", "Cyclo", "Cover", "Mask", "Film", "Shadow", "Blob", "PreviewRoom", "Water", "Rainbow", "Cloud", "Fog", "Sparkle", "Confetti" };

        public static bool Skip(Renderer r)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) return true;
            if (!(r is MeshRenderer)) return true;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return true;
            for (var t = r.transform; t; t = t.parent)
                foreach (var n in SkipNames) if (t.name.Contains(n)) return true;
            if (r.gameObject.CompareTag("EditorOnly")) return true;
            var b = r.bounds;
            if (b.size.x > 6f || b.size.z > 6f || b.size.y > 6f) return true;                   // 大件（地形、天幕、远山）
            var m = r.sharedMaterial;
            if (m && m.renderQueue >= 3000) return true;                                         // 透明（膜、光晕、气泡）
            // LOD：只查 LOD0
            var lg = r.GetComponentInParent<LODGroup>();
            if (lg)
            {
                var lods = lg.GetLODs();
                if (lods.Length > 0 && !lods[0].renderers.Contains(r)) return true;
            }
            return false;
        }

        // 装配关系（真实玩具就是这么装的、插进去的部分本来就看不见），不算穿模：
        //   木马立柱插在底座里、穿过顶棚中心；齿轮套在轴上；吊饰的线系在横杆上；发条钥匙插在钥匙孔里
        static readonly (string a, string b)[] Joints =
        {
            ("CarouselPole", "Carousel_Base"), ("CarouselPole", "Carousel_Canopy"),
            ("Axle", "W5_PG_"), ("Thread", "Mobile_Arms"), ("WindKey", "GearFrame"), ("Catapult_Base", "HillMound"), ("Catapult_Base", "Catapult_Arm"),
            ("W3_Wall_Segment", "W3_Tower_Round"), ("W3_Wall_Segment", "W3_Castle_Gate"),   // 城墙两头砌进圆塔 / 城门（城堡本来就是这么连的）   // 投石车轮子压进山包顶的沙坑
        };

        public static bool Attached(Renderer x, Renderer y)
        {
            string px = Path(x.transform), py = Path(y.transform);
            foreach (var (a, b) in Joints) if ((px.Contains(a) && py.Contains(b)) || (px.Contains(b) && py.Contains(a))) return true;
            return false;
        }

        public static Transform Owner(Renderer r)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
            if (root) return root.transform;
            return r.transform;
        }

        public static bool SameObject(Renderer a, Renderer b)
        {
            var oa = Owner(a); var ob = Owner(b);
            if (oa == ob) return true;
            // 共同祖先在两级以内、且祖先下面渲染器不多 → 同一件东西的零件（小鸟的翅膀和身体、组装的蘑菇灯）
            var pa = a.transform.parent; var pb = b.transform.parent;
            for (int i = 0; i < 2 && pa; i++, pa = pa.parent)
            {
                var q = pb;
                for (int j = 0; j < 2 && q; j++, q = q.parent)
                    if (q == pa && pa.GetComponentsInChildren<Renderer>().Length <= 12) return true;
            }
            return false;
        }

        static readonly Vector3[] Dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        static readonly Vector3[] Odd = { new Vector3(0.31f, 0.92f, 0.23f).normalized, new Vector3(-0.53f, 0.17f, 0.83f).normalized, new Vector3(0.71f, -0.41f, -0.57f).normalized };

        static int Crossings(Collider c, Vector3 p, Vector3 d, float max)
        {
            int n = 0; var o = p; float left = max;
            for (int k = 0; k < 32 && left > 0; k++)
            {
                if (!c.Raycast(new Ray(o, d), out var h, left)) break;
                n++; float step = h.distance + 1e-4f; o += d * step; left -= step;
            }
            return n;
        }

        public static float Depth(Collider c, Vector3 p)
        {
            int inside = 0;
            foreach (var d in Odd) if ((Crossings(c, p, d, 20f) & 1) == 1) inside++;
            if (inside < 2) return 0;
            float best = float.MaxValue;
            foreach (var d in Dirs) if (c.Raycast(new Ray(p, d), out var h, 20f)) best = Mathf.Min(best, h.distance);
            return best == float.MaxValue ? 0 : best;
        }

        public static List<Hit> Run(Transform scope, float minDepth = 0.008f, int maxVerts = 1000000)
        {
            var rs = (scope ? scope.GetComponentsInChildren<Renderer>() : Object.FindObjectsByType<Renderer>()).Where(r => !Skip(r)).ToList();
            var cols = new Dictionary<Renderer, MeshCollider>();
            var tmp = new List<GameObject>();
            bool back = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            var hits = new List<Hit>();
            try
            {
                foreach (var r in rs)
                {
                    var go = new GameObject("__clip"); go.hideFlags = HideFlags.HideAndDontSave; tmp.Add(go);
                    go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation); go.transform.localScale = r.transform.lossyScale;
                    var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh; cols[r] = mc;
                }
                Physics.SyncTransforms();
                var verts = new Dictionary<Renderer, Vector3[]>();
                Vector3[] V(Renderer r)
                {
                    if (verts.TryGetValue(r, out var v)) return v;
                    var src = r.GetComponent<MeshFilter>().sharedMesh.vertices;
                    int step = Mathf.Max(1, src.Length / maxVerts);
                    var l = new List<Vector3>();
                    for (int i = 0; i < src.Length; i += step) l.Add(r.transform.TransformPoint(src[i]));
                    return verts[r] = l.ToArray();
                }
                for (int i = 0; i < rs.Count; i++)
                    for (int j = i + 1; j < rs.Count; j++)
                    {
                        var a = rs[i]; var b = rs[j];
                        if (!a.bounds.Intersects(b.bounds) || SameObject(a, b) || Attached(a, b)) continue;
                        foreach (var (x, y) in new[] { (a, b), (b, a) })
                        {
                            var bb = y.bounds; bb.Expand(0.001f);
                            Hit h = null;
                            foreach (var p in V(x))
                            {
                                if (!bb.Contains(p)) continue;
                                float d = Depth(cols[y], p);
                                if (d < minDepth) continue;
                                h ??= new Hit { a = x, b = y };
                                h.count++;
                                if (d > h.depth) { h.depth = d; h.at = p; }
                            }
                            if (h != null) hits.Add(h);
                        }
                    }
            }
            finally
            {
                Physics.queriesHitBackfaces = back;
                foreach (var g in tmp) Object.DestroyImmediate(g);
            }
            return hits.OrderByDescending(h => h.depth).ToList();
        }

        public static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

        public static string Report(List<Hit> hits, string title)
        {
            var sb = new StringBuilder($"## {title}：{hits.Count} 处\n\n");
            foreach (var h in hits.Take(200))
                sb.Append($"- 深 {h.depth * 100:0.0} cm ×{h.count} 点  {Path(h.a.transform)}  →插进→  {Path(h.b.transform)}  @({h.at.x:0.00},{h.at.y:0.00},{h.at.z:0.00})\n");
            return sb.ToString();
        }

        [MenuItem("Bang/穿模巡检（当前场景）")]
        static void Menu()
        {
            var hits = Run(null);
            var s = Report(hits, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            System.IO.File.WriteAllText("Docs/穿模巡检.md", s);
            Debug.Log(s);
        }
    }
}
