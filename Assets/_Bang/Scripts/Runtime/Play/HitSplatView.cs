// 视野遮挡：敌人的攻击打中玩家时，眼前粘上这次攻击留下的东西，停一会儿淡掉——吸盘弹吸在眼前慢慢滑、小丑盒的彩纸屑飘落、
// 纸飞机的皱纸片贴在脸上、积木球砸出一片裂纹和塑料碎屑。
// 打到脸上就该挡一下视线：单片约 28～36°（用户：番茄酱反馈做大一些；脸上的涂鸦再大一些、再靠近眼睛），正中央准星周围 ±5° 仍露出来，最多同时 4 片。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Play
{
    public class HitSplatView : MonoBehaviour
    {
        public static HitSplatView Instance { get; private set; }
        const float Dist = 0.2f;                  // 贴片离眼睛的距离（用户：再靠近人眼睛；0.8 → 0.45 → 0.2 米，近裁剪面 0.02 米）。贴得近，双眼视差让它像糊在面罩上
        const int MaxSplats = 4;

        struct Style { public string tex; public float size, ecc, life, slide; public bool alongDir, upright; }
        // size = 贴图整幅的视角（度）；ecc = 离视野中心的角度；slide = 往下滑的速度（米/秒，头部坐标）；alongDir = 沿来袭方向摆放；upright = 不随机旋转
        static readonly Dictionary<string, Style> Styles = new()
        {
            { "foam", new Style { tex = "splat_dart", size = 28f, ecc = 17f, life = 2.4f, slide = 0.006f } },
            { "confetti", new Style { tex = "splat_confetti", size = 34f, ecc = 20f, life = 1.9f, slide = 0.05f } },
            { "plane", new Style { tex = "splat_paper", size = 29f, ecc = 17f, life = 2.5f, slide = 0.012f, alongDir = true } },
            { "ball", new Style { tex = "splat_crack", size = 36f, ecc = 21f, life = 2.6f } },
            { "brick", new Style { tex = "splat_crack", size = 32f, ecc = 19f, life = 2.4f } },
            { "shell", new Style { tex = "splat_crack", size = 36f, ecc = 21f, life = 2.6f } },
        };

        class Splat { public Transform t; public MeshRenderer r; public float age, life, roll, slide, alpha = 1f; public Vector3 pos; public float size; public bool live; }
        readonly List<Splat> _splats = new();
        readonly Dictionary<string, Texture2D> _tex = new();
        MaterialPropertyBlock _mpb;
        Material _mat;
        static readonly int IdBase = Shader.PropertyToID("_BaseMap"), IdColor = Shader.PropertyToID("_Color");

        void Awake() { Instance = this; _mpb = new MaterialPropertyBlock(); _mat = Resources.Load<Material>("Splat/M_HitSplat"); }

        /// <summary>dirWorld = 攻击飞行的方向（从敌人指向玩家）。</summary>
        public void Hit(string kind, Vector3 dirWorld)
        {
            float strength = PlayerPrefs.GetFloat("bang.splat", 1f); if (strength <= 0.01f) return;   // 设置：遮挡强度（关 / 弱 / 正常）
            var cam = Camera.main; if (!cam || !_mat) return;
            if (!Styles.TryGetValue(kind, out var st)) st = Styles["foam"];
            if (!_tex.TryGetValue(st.tex, out var tex)) _tex[st.tex] = tex = Resources.Load<Texture2D>("Splat/" + st.tex);
            if (!tex) return;
            var s = Take(cam.transform);
            // 粘在来袭的那一侧；正面飞来时方向不明确，随机取一边
            var from = cam.transform.InverseTransformDirection(-dirWorld);
            float a = new Vector2(from.x, from.y).sqrMagnitude > 0.01f ? Mathf.Atan2(from.y, from.x) : Random.value * Mathf.PI * 2f;
            a += Random.Range(-0.45f, 0.45f);
            float r = Mathf.Tan(st.ecc * Mathf.Deg2Rad) * Dist;
            s.pos = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f, Dist);   // 上下视野窄一点，纵向收一些
            s.size = 2f * Dist * Mathf.Tan(st.size * 0.5f * Mathf.Deg2Rad);
            s.roll = st.alongDir ? a * Mathf.Rad2Deg - 45f : st.upright ? Random.Range(-12f, 12f) : Random.Range(0f, 360f);   // 贴图里纸片是 45° 斜放
            s.age = 0; s.life = st.life; s.slide = st.slide; s.live = true; s.alpha = strength;
            if (strength < 0.99f) s.size *= 0.75f;
            s.t.gameObject.SetActive(true); s.r.sortingOrder = 100;
            _mpb.Clear(); _mpb.SetTexture(IdBase, tex); _mpb.SetColor(IdColor, Color.white); s.r.SetPropertyBlock(_mpb);
            Pose(s);
        }

        /// <summary>取一片空闲的；都在用时挤掉最旧的，最多同时 MaxSplats 片。</summary>
        Splat Take(Transform parent)
        {
            Splat oldest = null;
            foreach (var s in _splats) { if (!s.live) return s; if (oldest == null || s.age > oldest.age) oldest = s; }
            if (_splats.Count >= MaxSplats) return oldest;
            var g = new GameObject("HitSplat"); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = _mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var n = new Splat { t = g.transform, r = mr }; _splats.Add(n);
            return n;
        }

        void Pose(Splat s)
        {
            float pop = s.age < 0.14f ? Mathf.Lerp(0.55f, 1.1f, s.age / 0.14f) : Mathf.Lerp(1.1f, 1f, Mathf.Clamp01((s.age - 0.14f) / 0.12f));   // “啪”地粘上来
            float drip = Mathf.Max(0f, s.age - 0.5f);                                            // 停一下再往下滑
            s.t.localPosition = s.pos + Vector3.down * (drip * s.slide * (Dist / 0.45f));   // slide 是按 0.45 米调的：贴近以后按距离缩放，往下滑的视角速度不变
            s.t.localRotation = Quaternion.Euler(0, 0, s.roll);
            s.t.localScale = new Vector3(s.size * pop, s.size * pop, 1f);
            float fade = Mathf.Clamp01((s.life - s.age) / 0.7f);
            _mpb.SetColor(IdColor, new Color(1f, 1f, 1f, 0.94f * fade * s.alpha)); s.r.SetPropertyBlock(_mpb);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var s in _splats)
            {
                if (!s.live) continue;
                s.age += dt;
                if (s.age >= s.life) { s.live = false; s.t.gameObject.SetActive(false); continue; }
                s.r.GetPropertyBlock(_mpb); Pose(s);
            }
        }

        public void Clear() { foreach (var s in _splats) { s.live = false; s.t.gameObject.SetActive(false); } }
    }
}
