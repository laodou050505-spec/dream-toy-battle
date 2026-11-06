// 视野外提示：小怪在视野外（偏离视线 > 40°）时，视野边缘出现一个珊瑚色小箭头指向它；正在插旗的那只箭头更大、会跳动。
// 箭头挂在头部前方 0.6 米的一个圆上（不贴脸、不挡中间），最多 4 个，渐显渐隐，转头看过去就消失。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Play
{
    public class OffscreenHints : MonoBehaviour
    {
        public Material mat;                    // Bang/FxSprite（白贴图 × 颜色）
        public float dist = 0.6f, radius = 0.2f, offAngle = 40f;
        class Arrow { public Transform t; public MeshRenderer r; public float a; }
        readonly List<Arrow> _arrows = new();
        MaterialPropertyBlock _mpb;
        static Mesh _chevron;
        static readonly int IdColor = Shader.PropertyToID("_Color");

        static Mesh Chevron()
        {
            if (_chevron) return _chevron;
            // 圆头的 V 形箭头（指向 +Y），厚 0.3
            var v = new List<Vector3>(); var t = new List<int>();
            Vector2[] o = { new(0, 0.5f), new(0.5f, -0.1f), new(0.32f, -0.3f), new(0, 0.1f), new(-0.32f, -0.3f), new(-0.5f, -0.1f) };
            foreach (var p in o) v.Add(p);
            t.AddRange(new[] { 0, 1, 3, 1, 2, 3, 0, 3, 5, 3, 4, 5 });
            _chevron = new Mesh { name = "Chevron", vertices = v.ToArray(), triangles = t.ToArray(), uv = new Vector2[v.Count] };
            _chevron.RecalculateNormals();
            return _chevron;
        }

        void Awake() { _mpb = new MaterialPropertyBlock(); }

        Arrow Get(int i)
        {
            while (_arrows.Count <= i)
            {
                var g = new GameObject("OffscreenArrow"); g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = Chevron();
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _arrows.Add(new Arrow { t = g.transform, r = r });
            }
            return _arrows[i];
        }

        /// <summary>targets：(位置, 是否紧急)。</summary>
        public void Tick(Transform head, List<(Vector3 pos, bool urgent)> targets, float dt)
        {
            if (!head || !mat) return;
            int n = 0;
            foreach (var (pos, urgent) in targets)
            {
                if (n >= 4) break;
                var d = pos - head.position;
                if (Vector3.Angle(head.forward, d) < offAngle) continue;
                var local = head.InverseTransformDirection(d);
                var dir2 = new Vector2(local.x, local.y);
                if (local.z < 0 && dir2.sqrMagnitude < 0.01f) dir2 = Vector2.right;      // 正后方：箭头放在一侧
                dir2 = dir2.sqrMagnitude > 1e-4f ? dir2.normalized : Vector2.right;
                var a = Get(n++);
                a.a = Mathf.MoveTowards(a.a, 1, dt * 4f);
                float bob = urgent ? Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.02f : 0;
                var lp = new Vector3(dir2.x * (radius + bob), dir2.y * (radius + bob) * 0.75f, dist);
                a.t.position = head.TransformPoint(lp);
                a.t.rotation = head.rotation * Quaternion.Euler(0, 0, Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg - 90f);
                a.t.localScale = Vector3.one * (urgent ? 0.045f : 0.032f);
                var c = urgent ? new Color(1f, 0.45f, 0.4f, 0.95f * a.a) : new Color(1f, 0.7f, 0.55f, 0.75f * a.a);
                _mpb.SetColor(IdColor, c); a.r.SetPropertyBlock(_mpb);
                a.r.enabled = true;
            }
            for (int i = n; i < _arrows.Count; i++) { _arrows[i].a = 0; _arrows[i].r.enabled = false; }
        }
    }
}
