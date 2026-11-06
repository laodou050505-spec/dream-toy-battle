// 玩法特效（全部对象池复用，不在战斗中分配）：
//   拟声字（“砰！”枪口、“啵！”小怪散架）：先弹出过冲再回落（0.12 秒），停一下，向上飘并淡出；始终面向玩家。
//   弹道：从枪口到目标的一道蜡笔线，微微上拱（抛物线感），0.05 秒画到头，随后从尾部往前收掉。
//   彩纸：小怪散架时向上喷一把，受重力翻转飘落。  扬尘：落地扬起的棉花团，向外扩散、变大、变淡。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Play
{
    public class PlayFx : MonoBehaviour
    {
        public static PlayFx Instance { get; private set; }

        public Material wordMat, tracerMat, confettiMat, puffMat;
        public Texture2D pengTex, boTex, hitTex, crayonTex;   // crayonTex = 2×2 四种蜡笔痕
        public Texture2D starTex, glowTex;                   // 增益飞星 / 闪光
        [Header("英文版拟声字（BANG / POOF / POP / POW / ZAP）")] public Texture2D pengTexEn, boTexEn; public Texture2D[] wordTexsEn;
        public Texture2D muzzleTex; public Texture2D[] wordTexs;   // 枪口火光、更多拟声字（啪 / 嘭 / 咻）
        public Camera cam;

        class Word { public Transform t; public Renderer r; public float age, life, size; public Vector3 basePos; }
        class Tracer { public LineRenderer lr; public Vector3 a, b; public float age, T; public Color color; public Transform pellet; public Renderer pr; public System.Action arrive; public bool arrived; }
        class Star { public Transform t; public Renderer r; public Vector3 from, ctrl; public System.Func<Vector3> to; public float age, life, size, spin; public Color color; public System.Action done; }
        readonly List<Star> _stars = new(), _starPool = new();
        readonly List<Word> _words = new(), _wordPool = new();
        readonly List<Tracer> _tracers = new(), _tracerPool = new();
        class Debris { public Transform t; public Vector3 v, w, scale; public float life, max; public bool rest; }
        readonly List<Debris> _debris = new();
        ParticleSystem _confetti, _puff;
        MaterialPropertyBlock _mpb;
        static Mesh _quad;
        static readonly int IdBase = Shader.PropertyToID("_BaseMap"), IdColor = Shader.PropertyToID("_Color");
        readonly Vector3[] _pts = new Vector3[12];

        void Awake()
        {
            Instance = this; _mpb = new MaterialPropertyBlock();
            if (!cam) cam = Camera.main;
            _confetti = MakeConfetti(); _puff = MakePuff();
        }

        public static Mesh Quad
        {
            get
            {
                if (_quad) return _quad;
                _quad = new Mesh { name = "FxQuad" };
                _quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
                _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        // ───────── 拟声字 ─────────
        /// <summary>按当前语言换拟声字（中文：砰 / 啵 / 啪 / 嘭 / 咻；英文：BANG / POOF / POP / POW / ZAP），启动和切换语言时调用。</summary>
        Texture2D _pengZh, _boZh; Texture2D[] _wordsZh; bool _saved;
        public void ApplyLanguage()
        {
            if (!_saved) { _pengZh = pengTex; _boZh = boTex; _wordsZh = wordTexs; _saved = true; }
            bool en = Bang.Logic.Loc.En;
            pengTex = en && pengTexEn ? pengTexEn : _pengZh;
            boTex = en && boTexEn ? boTexEn : _boZh;
            wordTexs = en && wordTexsEn != null && wordTexsEn.Length > 0 ? wordTexsEn : _wordsZh;
        }

        public void Peng(Vector3 pos) => SpawnWord(pengTex, pos, 0.075f * ShotJitter(), 0.42f);       // 枪口前上方一个小“砰！”，不挡视线
        /// <summary>每一枪的大小在基准值上下随机浮动（用户：开枪特效大小要随机，但有个固定值，不要每次一样大）。偏向基准、偶尔大一点。</summary>
        public static float ShotJitter() { float u = Random.value; return 0.82f + 0.36f * u * u * (3f - 2f * u) * (Random.value < 0.15f ? 1.25f : 1f); }
        /// <summary>命中点：星爆一闪（0.25 秒）+ 几颗彩纸。</summary>
        public void HitStar(Vector3 pos) { SpawnWord(hitTex, pos, 0.13f, 0.26f); Confetti(pos, 5); }
        public void Bo(Vector3 pos) => SpawnWord(boTex, pos, 0.32f, 0.75f);

        void SpawnWord(Texture2D tex, Vector3 pos, float size, float life)
        {
            if (!tex || !wordMat) return;
            Word w;
            if (_wordPool.Count > 0) { w = _wordPool[^1]; _wordPool.RemoveAt(_wordPool.Count - 1); w.t.gameObject.SetActive(true); }
            else
            {
                var g = new GameObject("FxWord"); g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = Quad;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = wordMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                w = new Word { t = g.transform, r = mr };
            }
            w.age = 0; w.life = life; w.size = size; w.basePos = pos;
            _mpb.Clear(); _mpb.SetTexture(IdBase, tex); _mpb.SetColor(IdColor, Color.white); w.r.SetPropertyBlock(_mpb);
            _words.Add(w);
            UpdateWord(w, 0);
        }

        void UpdateWord(Word w, float dt)
        {
            w.age += dt;
            float k = w.age / w.life;
            // 弹出：0~0.12 秒从 0 冲到 1.18 再回到 1（过冲回落），最后 35% 时间淡出、继续上飘
            float pop = w.age < 0.08f ? Mathf.SmoothStep(0, 1.18f, w.age / 0.08f) : w.age < 0.16f ? Mathf.Lerp(1.18f, 1f, (w.age - 0.08f) / 0.08f) : 1f;
            float fade = k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f;
            w.t.position = w.basePos + Vector3.up * (0.06f * Mathf.Sqrt(Mathf.Max(0, k)));
            if (cam) w.t.rotation = Quaternion.LookRotation(w.t.position - cam.transform.position, cam.transform.up);
            w.t.localScale = Vector3.one * w.size * pop;
            _mpb.Clear(); w.r.GetPropertyBlock(_mpb); _mpb.SetColor(IdColor, new Color(1, 1, 1, Mathf.Clamp01(fade))); w.r.SetPropertyBlock(_mpb);
        }

        // ───────── 蜡笔痕（打空时留在打到的表面上，8 秒后淡出）─────────
        class Mark { public Transform t; public Renderer r; public float age; }
        readonly List<Mark> _marks = new();
        static readonly int IdST = Shader.PropertyToID("_BaseMap_ST");
        public void CrayonMark(Vector3 pos, Vector3 normal, Color tint)
        {
            if (!crayonTex || !wordMat) return;
            Mark m;
            if (_marks.Count >= 24) { m = _marks[0]; _marks.RemoveAt(0); }
            else
            {
                var g = new GameObject("CrayonMark"); g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = Quad;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = wordMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m = new Mark { t = g.transform, r = mr };
            }
            m.age = 0;
            m.t.SetPositionAndRotation(pos + normal * 0.003f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
            m.t.localScale = Vector3.one * Random.Range(0.1f, 0.14f);
            int k = Random.Range(0, 4);
            _mpb.Clear(); _mpb.SetTexture(IdBase, crayonTex); _mpb.SetVector(IdST, new Vector4(0.5f, 0.5f, (k % 2) * 0.5f, (k / 2) * 0.5f)); _mpb.SetColor(IdColor, Color.white);
            m.r.SetPropertyBlock(_mpb);
            _marks.Add(m);
        }

        void UpdateMarks(float dt)
        {
            foreach (var m in _marks)
            {
                m.age += dt;
                float a = m.age < 0.08f ? m.age / 0.08f : Mathf.Clamp01((8f - m.age) / 1.5f);
                m.r.GetPropertyBlock(_mpb); _mpb.SetColor(IdColor, new Color(1, 1, 1, a)); m.r.SetPropertyBlock(_mpb);
                m.r.enabled = a > 0;
            }
        }

        // ───────── 弹道：一颗发光的糖果弹从枪口飞向目标（约 30 米/秒，0.05~0.16 秒），后面拖一段蜡笔尾迹；到达时回调（命中特效在这时才出现）─────────
        public void Shot(Vector3 from, Vector3 to, Color color, float width = 1f, System.Action onArrive = null)
        {
            if (!tracerMat) { onArrive?.Invoke(); return; }
            Tracer tr;
            if (_tracerPool.Count > 0) { tr = _tracerPool[^1]; _tracerPool.RemoveAt(_tracerPool.Count - 1); tr.lr.gameObject.SetActive(true); }
            else
            {
                var g = new GameObject("FxTracer"); g.transform.SetParent(transform, false);
                var lr = g.AddComponent<LineRenderer>();
                lr.sharedMaterial = tracerMat; lr.positionCount = _pts.Length; lr.useWorldSpace = true;
                lr.numCapVertices = 3; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                lr.widthCurve = new AnimationCurve(new Keyframe(0, 0.002f), new Keyframe(0.75f, 0.011f), new Keyframe(1, 0.014f));   // 尾细头粗
                tr = new Tracer { lr = lr };
                if (wordMat)
                {
                    var pg = new GameObject("Pellet"); pg.transform.SetParent(g.transform, false);
                    pg.AddComponent<MeshFilter>().sharedMesh = Quad;
                    var pr = pg.AddComponent<MeshRenderer>(); pr.sharedMaterial = wordMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    tr.pellet = pg.transform; tr.pr = pr;
                }
            }
            float dist = Vector3.Distance(from, to);
            width *= Random.Range(0.85f, 1.2f);                                   // 弹道粗细也每枪略有不同
            tr.a = from; tr.b = to; tr.age = 0; tr.color = color; tr.lr.widthMultiplier = width; tr.arrive = onArrive; tr.arrived = false;
            tr.T = Mathf.Clamp(dist / 30f, 0.05f, 0.16f);
            if (tr.pr)
            {
                _mpb.Clear(); _mpb.SetTexture(IdBase, glowTex ? glowTex : hitTex); _mpb.SetColor(IdColor, Color.Lerp(color, Color.white, 0.45f)); tr.pr.SetPropertyBlock(_mpb);
                tr.pellet.localScale = Vector3.one * 0.045f * width;
            }
            _tracers.Add(tr);
            UpdateTracer(tr, 0);
        }

        static Vector3 Arc(Vector3 a, Vector3 b, float u, float arc) => Vector3.Lerp(a, b, u) + Vector3.up * (4f * u * (1f - u) * arc);

        void UpdateTracer(Tracer tr, float dt)
        {
            tr.age += dt;
            float head = Mathf.Clamp01(tr.age / tr.T), tail = Mathf.Clamp01((tr.age - 0.07f) / tr.T);
            float len = Vector3.Distance(tr.a, tr.b), arc = Mathf.Min(0.06f, len * 0.02f);
            for (int i = 0; i < _pts.Length; i++)
            {
                float u = Mathf.Lerp(tail, head, i / (float)(_pts.Length - 1));
                _pts[i] = Arc(tr.a, tr.b, u, arc);
            }
            tr.lr.SetPositions(_pts);
            var c = tr.color; c.a = 1f - tail * tail;
            tr.lr.startColor = new Color(c.r, c.g, c.b, c.a * 0.2f); tr.lr.endColor = c;
            if (tr.pellet)
            {
                tr.pellet.gameObject.SetActive(head < 1f);
                tr.pellet.position = Arc(tr.a, tr.b, head, arc);
                if (cam) tr.pellet.rotation = Quaternion.LookRotation(tr.pellet.position - cam.transform.position, cam.transform.up);
            }
            if (head >= 1f && !tr.arrived) { tr.arrived = true; tr.arrive?.Invoke(); }
        }

        bool TracerDone(Tracer tr) => tr.age >= tr.T + 0.07f;

        /// <summary>枪口：火光一闪（花瓣状）+ 一小团烟 + 拟声字（砰 / 啪 / 嘭 / 咻 轮换，不总是同一个）。</summary>
        public void Muzzle(Vector3 pos, Vector3 fwd, Vector3 up, bool gold)
        {
            var flashT = muzzleTex ? muzzleTex : hitTex;
            var c = gold ? new Color(1f, 0.85f, 0.4f) : Color.white;
            float k = ShotJitter();
            var w = SpawnStar(flashT, pos, pos, null, c, 0.075f * k, 0.09f * Mathf.Lerp(1f, k, 0.5f)); if (w != null) { w.spin = -1; }
            Dust(pos + fwd * 0.02f, 0.25f * k, k > 1.05f ? 4 : 3);
            Texture2D word = pengTex;
            if (wordTexs != null && wordTexs.Length > 0 && Random.value < 0.55f) { var t = wordTexs[Random.Range(0, wordTexs.Length)]; if (t) word = t; }
            SpawnWord(word, pos + fwd * 0.05f + up * 0.03f, 0.07f * ShotJitter(), 0.4f);
        }

        // ───────── 碎块（掉下来的积木、散架的零件）─────────
        /// <summary>把零件甩出去：受重力、落地弹跳（每次损失大半速度）、滚动减速，最后缩没并销毁。</summary>
        public void Throw(Transform t, Vector3 vel, Vector3 angVelDeg, float life = 1.6f)
        {
            if (!t) return;
            t.SetParent(transform, true);
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _debris.Add(new Debris { t = t, v = vel, w = angVelDeg, life = life, max = life, scale = t.localScale });
        }

        void UpdateDebris(float dt)
        {
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                var d = _debris[i];
                if (!d.t) { _debris.RemoveAt(i); continue; }
                d.life -= dt;
                if (!d.rest)
                {
                    d.v += Physics.gravity * dt;
                    var p0 = d.t.position; var p = p0 + d.v * dt;
                    // 真实房间（桌面、墙、柜子）：先撞到哪儿就在哪儿弹 / 停（用户：击飞要先从桌上飞起来再落到地上，不能直接穿过桌子）
                    if (RoomHit(p0, p, out var hit) && Vector3.Dot(d.v, hit.normal) < 0)
                    {
                        var n = hit.normal; p = hit.point + n * 0.015f;
                        float vn = Vector3.Dot(d.v, n); var vt = d.v - n * vn;
                        if (n.y > 0.7f && -vn < 0.25f) { d.rest = true; d.v = Vector3.zero; }   // 落在桌面 / 地上：停住
                        else { d.v = vt * 0.55f - n * vn * 0.38f; d.w *= 0.5f; }              // 弹开（撞到侧面就反弹回来，在桌边就滑下去接着落）
                    }
                    else if (p.y < 0.015f && d.v.y < 0)
                    {
                        p.y = 0.015f;
                        if (-d.v.y < 0.25f) { d.rest = true; d.v = Vector3.zero; }
                        else { d.v = new Vector3(d.v.x * 0.55f, -d.v.y * 0.38f, d.v.z * 0.55f); d.w *= 0.5f; }
                    }
                    d.t.position = p;
                    d.t.rotation = Quaternion.Euler(d.w * dt) * d.t.rotation;
                }
                if (d.life < 0.3f) d.t.localScale = d.scale * Mathf.SmoothStep(0, 1, d.life / 0.3f);
                if (d.life <= 0) { Destroy(d.t.gameObject); _debris.RemoveAt(i); }
            }
        }

        static readonly RaycastHit[] _rh = new RaycastHit[8];
        /// <summary>p0 → p1 这一步有没有撞到真实房间（MrRoom 登记的扫描网格 / 地板碰撞体），取最近的一处。</summary>
        public static bool RoomHit(Vector3 p0, Vector3 p1, out RaycastHit hit)
        {
            hit = default;
            var set = Bang.MrRoom.RoomColliders; if (set.Count == 0) return false;
            var d = p1 - p0; float len = d.magnitude; if (len < 1e-5f) return false;
            int n = Physics.RaycastNonAlloc(p0, d / len, _rh, len + 0.015f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool any = false;
            for (int i = 0; i < n; i++) if (_rh[i].distance < best && set.Contains(_rh[i].collider)) { best = _rh[i].distance; hit = _rh[i]; any = true; }
            return any;
        }

        // ───────── 彩纸 / 扬尘 ─────────
        static GameObject[] _bits;
        /// <summary>玩具小零件（齿轮 / 螺丝 / 弹簧 / 星星）迸出来、落地弹两下：P2 敌人是一整块模型，打中时靠它给“打碎了”的手感。场上碎片太多时自动减量。</summary>
        public void ToyBits(Vector3 pos, Vector3 dir, int n)
        {
            _bits ??= Resources.LoadAll<GameObject>("ToyBits");
            if (_bits.Length == 0) return;
            n = Mathf.Min(n, Mathf.Max(0, 90 - _debris.Count));
            for (int i = 0; i < n; i++)
            {
                var t = Instantiate(_bits[Random.Range(0, _bits.Length)], pos + Random.insideUnitSphere * 0.05f, Random.rotation).transform;
                t.localScale *= Random.Range(0.8f, 1.25f);
                var u = Random.insideUnitSphere; u.y = Mathf.Abs(u.y);
                Throw(t, (dir * 0.6f + u) * Random.Range(0.7f, 1.5f) + Vector3.up * Random.Range(0.8f, 1.5f), Random.insideUnitSphere * 900f, Random.Range(0.9f, 1.5f));
            }
        }

        public void Confetti(Vector3 pos, int n = 26)
        {
            if (!_confetti) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.05f;
                var v = Random.insideUnitSphere; v.y = Mathf.Abs(v.y) * 1.6f + 0.6f;
                ep.velocity = v * Random.Range(0.8f, 1.6f);
                ep.startColor = Color.HSVToRGB(Random.value, Random.Range(0.35f, 0.6f), 1f);
                ep.startSize = Random.Range(0.018f, 0.03f);
                ep.rotation3D = new Vector3(Random.value * 360, Random.value * 360, Random.value * 360);
                ep.angularVelocity3D = Random.insideUnitSphere * 720f;
                _confetti.Emit(ep, 1);
            }
        }

        /// <summary>单色亮片：金色子弹的命中、增益生效时的闪亮碎片（比彩纸小、更亮、飞得更开）。</summary>
        public void Glitter(Vector3 pos, Color color, int n = 12, float speed = 1.2f)
        {
            if (!_confetti) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.03f;
                var v = Random.insideUnitSphere; v.y = Mathf.Abs(v.y) + 0.3f;
                ep.velocity = v.normalized * Random.Range(0.5f, 1f) * speed;
                Color.RGBToHSV(color, out var h, out var sa, out var va);
                ep.startColor = Color.HSVToRGB(Mathf.Repeat(h + Random.Range(-0.03f, 0.03f), 1), sa * Random.Range(0.6f, 1f), 1f);
                ep.startSize = Random.Range(0.012f, 0.022f);
                ep.rotation3D = new Vector3(Random.value * 360, Random.value * 360, Random.value * 360);
                ep.angularVelocity3D = Random.insideUnitSphere * 900f;
                _confetti.Emit(ep, 1);
            }
        }

        /// <summary>一团柔光“噗”地亮一下再扩散淡出（增益生效、门涌、弹反）。</summary>
        public void Flash(Vector3 pos, Color color, float size = 0.6f, float life = 0.4f)
        {
            var w = SpawnStar(glowTex, pos, pos, null, color, size, life);
            if (w != null) w.spin = -1;             // -1：原地扩散
        }

        /// <summary>击杀冲击环：一圈柔光圆环从击杀点“嘭”地扩开淡出（贴图运行时生成，不占资源）。</summary>
        public void Ring(Vector3 pos, Color color, float size = 0.7f, float life = 0.32f)
        {
            if (!_ringTex)
            {
                const int N = 128; _ringTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxRing" };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float r = new Vector2(x - N / 2 + 0.5f, y - N / 2 + 0.5f).magnitude / (N / 2f);
                        float a2 = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.12f); a2 = a2 * a2 * (3 - 2 * a2);
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(a2 * 255));
                    }
                _ringTex.SetPixels32(px); _ringTex.Apply(false, true);
            }
            var w = SpawnStar(_ringTex, pos, pos, null, color, size, life);
            if (w != null) w.spin = -1;
        }
        Texture2D _ringTex;

        /// <summary>一串小星星从 from 沿弧线飞向 to（每帧取最新位置，跟着手走），到达时回调——增益“飞进”枪里。</summary>
        public void Trail(Vector3 from, System.Func<Vector3> to, Color color, int n = 7, System.Action arrived = null)
        {
            for (int i = 0; i < n; i++)
            {
                var side = Random.onUnitSphere * 0.35f; side.y = Mathf.Abs(side.y) + 0.25f;
                var s = SpawnStar(starTex, from, from + side, to, color, Random.Range(0.035f, 0.055f), 0.55f + i * 0.05f);
                if (s != null && i == n - 1) s.done = arrived;
            }
        }

        Star SpawnStar(Texture2D tex, Vector3 from, Vector3 ctrl, System.Func<Vector3> to, Color color, float size, float life)
        {
            if (!tex || !wordMat) return null;
            Star w;
            if (_starPool.Count > 0) { w = _starPool[^1]; _starPool.RemoveAt(_starPool.Count - 1); w.t.gameObject.SetActive(true); }
            else
            {
                var g = new GameObject("FxStar"); g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = Quad;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = wordMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                w = new Star { t = g.transform, r = mr };
            }
            w.from = from; w.ctrl = ctrl; w.to = to; w.age = 0; w.life = life; w.size = size; w.color = color; w.spin = Random.Range(-400f, 400f); w.done = null;
            _mpb.Clear(); _mpb.SetTexture(IdBase, tex); _mpb.SetColor(IdColor, color); w.r.SetPropertyBlock(_mpb);
            _stars.Add(w);
            return w;
        }

        void UpdateStar(Star w, float dt)
        {
            w.age += dt;
            float k = Mathf.Clamp01(w.age / w.life);
            Vector3 p; float sc, a;
            if (w.spin == -1) { p = w.from; sc = w.size * (0.4f + 1.1f * Mathf.Sqrt(k)); a = (1 - k) * (1 - k); }
            else
            {
                var end = w.to != null ? w.to() : w.from;
                float u = k * k * (3 - 2 * k);                  // 先慢后快再落稳
                p = Vector3.Lerp(Vector3.Lerp(w.from, w.ctrl, u), Vector3.Lerp(w.ctrl, end, u), u);
                sc = w.size * (k < 0.15f ? k / 0.15f : k > 0.85f ? Mathf.Lerp(1, 0.3f, (k - 0.85f) / 0.15f) : 1f);
                a = 1;
            }
            w.t.position = p;
            if (cam) w.t.rotation = Quaternion.LookRotation(p - cam.transform.position, cam.transform.up) * Quaternion.Euler(0, 0, w.spin == -1 ? 0 : w.age * w.spin);
            w.t.localScale = Vector3.one * sc;
            _mpb.Clear(); w.r.GetPropertyBlock(_mpb); var c = w.color; c.a *= a; _mpb.SetColor(IdColor, c); w.r.SetPropertyBlock(_mpb);
        }

        public void Dust(Vector3 pos, float size = 1f, int n = 5)
        {
            if (!_puff) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                float a = (i + Random.value * 0.5f) / n * Mathf.PI * 2;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                ep.position = pos + d * 0.03f * size + Vector3.up * 0.01f;
                ep.velocity = (d * Random.Range(0.18f, 0.3f) + Vector3.up * 0.06f) * size;
                ep.startSize = Random.Range(0.035f, 0.05f) * size;
                ep.startColor = new Color(1, 1, 1, 0.85f);
                _puff.Emit(ep, 1);
            }
        }

        ParticleSystem MakeConfetti()
        {
            if (!confettiMat) return null;
            var g = new GameObject("FxConfetti"); g.transform.SetParent(transform, false);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.startLifetime = 1.6f; main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 400; main.startRotation3D = true;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.9f; lim.dampen = 0.08f;    // 纸片空气阻力大：很快减速飘落
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.8f, 1), new Keyframe(1, 0)));
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = confettiMat; r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = Quad;
            r.alignment = ParticleSystemRenderSpace.World; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        ParticleSystem MakePuff()
        {
            if (!puffMat) return null;
            var g = new GameObject("FxDust"); g.transform.SetParent(transform, false);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.startLifetime = 0.55f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.05f; lim.dampen = 0.12f;  // 先冲出去再很快停住（缓出）
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1.8f)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = gr;
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = puffMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Update()
        {
            float dt = Time.deltaTime * ToyTime.Scale;                     // 慢动作时碎片也一起放慢
            UpdateDebris(dt);
            UpdateMarks(dt);
            for (int i = _words.Count - 1; i >= 0; i--)
            {
                var w = _words[i]; UpdateWord(w, dt);
                if (w.age >= w.life) { w.t.gameObject.SetActive(false); _words.RemoveAt(i); _wordPool.Add(w); }
            }
            for (int i = _stars.Count - 1; i >= 0; i--)
            {
                var w = _stars[i]; UpdateStar(w, dt);
                if (w.age >= w.life) { w.t.gameObject.SetActive(false); _stars.RemoveAt(i); _starPool.Add(w); w.done?.Invoke(); }
            }
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var tr = _tracers[i]; UpdateTracer(tr, dt);
                if (TracerDone(tr)) { tr.lr.gameObject.SetActive(false); _tracers.RemoveAt(i); _tracerPool.Add(tr); }
            }
        }
    }
}
