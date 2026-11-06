// 传送门的出现：封面阶段门完全不出现（门洞、门框、门前溢出物都收起来），点“开始游戏”后才打开（用户要求）。
// 开门（约 2.8 秒）——用户：门的外边缘慢慢长出来，不要整个一起放大；地面也一样，慢慢长出来：
//   0    ~ 1.5 s  门洞从中心一小点开始，边缘一圈参差不齐地往外“长”（每个轮廓点各有快慢、带一点抖动），最后长成完整的不规则门洞；
//                 涡旋膜、发光内沿跟着轮廓一起长；门洞里透出的光开头最亮（像光从小洞里透出来），边缘沿途冒光点
//   0.35 ~ 1.8 s  门框从一侧门脚起步，沿门洞外沿翻过顶，一路长到另一侧门脚（用户：从一边一点点长出来，不能太慢，但要看得见生长）：
//                 能拆开的门框（积木、气球、齿轮）一块接一块从上一块里冒出来；连成一体的门框沿弧线连续挤出；门框上的小摆件按同一条线依次弹出，落定扬一点灰
//   1.2  ~ 2.8 s  门前的地皮像液体一样从门脚往房间里一圈圈漫开（边缘不规则），花草在前沿经过时从地上长起来，摆件在前沿到达时冒出来
//   1.5 s 门洞长全那一下：一记柔光闪 + 彩纸星光
// 门洞的遮罩、涡旋膜、发光内沿都按“正在长的轮廓”逐帧重建（PortalArch.BuildWith），门后世界只在门洞里可见，所以门洞没长开时看不到。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Portal
{
    public class PortalOpen : MonoBehaviour
    {
        public float duration = 2.8f;
        PortalArch _archC; Transform _arch, _spill; readonly List<Transform> _frame = new();
        readonly Dictionary<Transform, Vector3> _scale0 = new();
        Vector2 _center; Vector2[] _outline, _grow; float[] _lag;
        readonly List<float> _frameDelay = new(); readonly HashSet<int> _landed = new();
        const float FrameStart = 0.35f, FrameSpan = 1.2f, FrameEach = 0.26f;      // 门框：起步时刻、从一侧到另一侧用时、每一块弹出用时

        // 门框主体（整块网格）：逐帧改顶点，从一侧长到另一侧。block=true：网格能拆成互不相连的小块，一块接一块冒出来；否则沿弧线连续挤出
        class Sweep
        {
            public MeshFilter mf; public Mesh src, dyn; public Transform root; public Vector3 s0; public LODGroup lod; public bool block, done;
            public Vector3[] v0, v; public int[] island; public Vector3[] center, from; public float[] islandDelay;       // 分块
            public int[] stream; public bool[] islandBig;                                                                  // 走连续挤出的顶点；分块门框里特别大的块（门框主体）也走连续挤出
            public int[] islandStart, islandVerts, islandState;                                                            // 分块：每块的顶点表；只更新状态有变化或正在动的块
            public float[] delay, radial, ringLo, ringHi; public Vector3[] arch; public Matrix4x4 toMesh;                  // 连续挤出：顶点在截面上的位置、沿途每段截面的内外半径
            public (float d, Vector3 p)[] dust;                                                                            // 沿途扬灰的点（门洞局部）
            public int dustNext;
        }
        readonly List<Sweep> _sweeps = new();
        const int Rings = 64;
        static readonly Dictionary<Mesh, (Mesh dyn, float t)> Latest = new();      // 同一章的几扇门同时开、门框一样：这一帧算过就直接用，不重复改顶点
        float _sweepMin, _sweepRange = 1f;
        float _t = -1f, _fxT; bool _open = true, _inited, _burst, _shapeFull = true; Action _done;
        MeshRenderer _glow; MaterialPropertyBlock _gmpb;
        static readonly Color Spark = new(0.75f, 0.95f, 1f), Pink = new(1f, 0.75f, 0.9f);

        // 门前地面漫开
        class Flat { public MeshFilter mf; public Mesh src, dyn; public Vector3[] v0, sp0, v; public Matrix4x4 toMesh; public bool grow; }   // grow=true：花草（前沿经过时从地上长起来），否则：地皮 / 小路（超出前沿的顶点收到前沿上）
        readonly List<Flat> _flats = new();
        readonly List<(Transform t, float d, Vector3 s0)> _props = new();
        float _spillR = 1.5f;

        public bool IsOpen => _open && _t < 0;
        public bool Opening => _t >= 0;

        void Init()
        {
            if (_inited) return; _inited = true;
            _archC = GetComponentInChildren<PortalArch>(true);
            if (_archC)
            {
                _arch = _archC.transform; _outline = _archC.OutlinePoints;
                var b = _archC.LocalBounds; _center = new Vector2(b.center.x, b.center.y);
                _grow = new Vector2[_outline.Length]; _lag = new float[_outline.Length];
                for (int i = 0; i < _outline.Length; i++)
                {
                    var d = _outline[i] - _center; float a = Mathf.Atan2(d.y, d.x);
                    _lag[i] = 0.22f * (0.5f + 0.5f * Mathf.Sin(a * 3f + 1.3f)) * (0.6f + 0.4f * Mathf.Sin(a * 7f + 0.4f) * Mathf.Sin(a * 2f));   // 有的地方先长、有的后长
                }
            }
            foreach (Transform c in transform)
            {
                if (c == _arch || c.GetComponent<PortalWorld>() || c.GetComponentInChildren<PortalWorld>(true)) continue;
                if (c.name == "Spill") { _spill = c; continue; }
                if (c.name == "OpenGlow") continue;
                if (c.name.StartsWith("Frame_") && c.childCount > 1)
                    foreach (Transform piece in c) AddFramePiece(piece);
                else AddFramePiece(c);
            }
            // 门框的先后：从一侧门脚沿门洞外沿翻过顶到另一侧（0 = 起点一侧，1 = 终点一侧）
            _sweepMin = 1f; float sweepMax = 0f;
            void Cover(float u) { _sweepMin = Mathf.Min(_sweepMin, u); sweepMax = Mathf.Max(sweepMax, u); }
            var pieceU = new List<float>();
            foreach (var f in _frame) { float u = AroundDoor(ToArch(f.position)); pieceU.Add(u); Cover(u); }
            foreach (var s in _sweeps) foreach (var u in s.delay) Cover(u);
            _sweepRange = Mathf.Max(0.05f, sweepMax - _sweepMin);
            float Norm(float u) => Mathf.Clamp01((u - _sweepMin) / _sweepRange);
            _frameDelay.Clear();
            foreach (var u in pieceU) _frameDelay.Add(Norm(u));
            foreach (var s in _sweeps)
            {
                for (int i = 0; i < s.delay.Length; i++) s.delay[i] = Norm(s.delay[i]);
                if (s.block) { for (int i = 0; i < s.islandDelay.Length; i++) s.islandDelay[i] = Norm(s.islandDelay[i]); PrepareBlocks(s); }
                PrepareRings(s);
                var dust = new (float d, Vector3 p)[Mathf.Min(24, s.delay.Length)];
                for (int i = 0; i < dust.Length; i++) { int k = i * s.delay.Length / dust.Length; dust[i] = (s.delay[k], _arch.InverseTransformPoint(s.mf.transform.TransformPoint(s.v0[k]))); }
                System.Array.Sort(dust, (a, b) => a.d.CompareTo(b.d)); s.dust = dust;
            }
            CollectSpill();
        }

        /// <summary>收集门前地面（地皮 / 小路 / 花草）和摆件。多门章节换了地皮网格、剔了花草后要重新收集（RefreshSpill），否则开门动画结束会把旧网格换回来。</summary>
        void CollectSpill()
        {
            foreach (var f in _flats) if (f.dyn) Destroy(f.dyn);
            _flats.Clear(); _props.Clear(); _spillR = 1.5f;
            if (_spill)
            {
                foreach (var mf in _spill.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                    string path = mf.name + "/" + (mf.transform.parent ? mf.transform.parent.name : "") + "/" + (mf.transform.parent && mf.transform.parent.parent ? mf.transform.parent.parent.name : "");
                    bool grow = path.Contains("Flora");
                    bool flat = grow || path.Contains("SpillPatch") || path.Contains("BrickPath");
                    if (!flat) continue;
                    var src = mf.sharedMesh; var v0 = src.vertices;
                    var f = new Flat { mf = mf, src = src, v0 = v0, v = (Vector3[])v0.Clone(), grow = grow, sp0 = new Vector3[v0.Length] };
                    var toSpill = _spill.worldToLocalMatrix * mf.transform.localToWorldMatrix; f.toMesh = toSpill.inverse;   // 动画期间这些物体不动：矩阵算一次
                    for (int i = 0; i < v0.Length; i++) { var sp = toSpill.MultiplyPoint3x4(v0[i]); f.sp0[i] = sp; _spillR = Mathf.Max(_spillR, new Vector2(sp.x, sp.z).magnitude); }
                    _flats.Add(f);
                }
                foreach (Transform c in _spill)
                {
                    if (_flats.Exists(f => f.mf && f.mf.transform.IsChildOf(c))) continue;
                    var p = c.localPosition; _props.Add((c, new Vector2(p.x, p.z).magnitude, c.localScale));
                }
            }
        }

        public void RefreshSpill() { if (_inited) CollectSpill(); }

        Vector2 ToArch(Vector3 world) => _arch ? (Vector2)_arch.InverseTransformPoint(world) : (Vector2)transform.InverseTransformPoint(world);

        /// <summary>门洞外沿上的位置：从正下方经一侧、翻过顶到另一侧（0~1）。</summary>
        float AroundDoor(Vector2 p)
        {
            var d = p - _center; if (d.sqrMagnitude < 1e-6f) return 0.5f;
            return Mathf.Repeat(-Vector2.SignedAngle(Vector2.down, d), 360f) / 360f;
        }

        void AddFramePiece(Transform piece)
        {
            if (!piece.GetComponent<MeshFilter>() && !piece.GetComponent<LODGroup>() && piece.childCount >= 6)      // 一组零件（齿轮组）：拆开，每个零件按自己的位置依次出现
            {
                foreach (Transform c in piece) AddFramePiece(c);
                return;
            }
            if (TrySweep(piece)) return;
            _frame.Add(piece); _scale0[piece] = piece.localScale;
        }

        /// <summary>门框主体（包住门洞的大网格）走逐帧改顶点；其余小摆件照旧整体弹出。</summary>
        bool TrySweep(Transform piece)
        {
            var mf = piece.GetComponent<MeshFilter>();
            if (!mf) { var l0 = piece.Find("LOD0"); if (l0) mf = l0.GetComponent<MeshFilter>(); }
            if (!mf || !mf.sharedMesh || !mf.sharedMesh.isReadable || !_arch) return false;
            var size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            float[] dim = { Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z) }; Array.Sort(dim);
            if (dim[2] < 1.5f || dim[1] < 1.2f) return false;
            var src = mf.sharedMesh; var v0 = src.vertices;
            var s = new Sweep { mf = mf, src = src, root = piece, s0 = piece.localScale, lod = piece.GetComponent<LODGroup>(), v0 = v0, v = (Vector3[])v0.Clone(), delay = new float[v0.Length] };
            var toArch = _arch.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var parent = Islands(src, v0, out int count); int[] verts = new int[count];
            foreach (var i in parent) verts[i]++;
            int biggest = 0; foreach (var n in verts) biggest = Mathf.Max(biggest, n);
            s.block = count >= 6 && biggest < v0.Length / 2;
            s.arch = new Vector3[v0.Length]; s.radial = new float[v0.Length]; s.toMesh = toArch.inverse;
            for (int i = 0; i < v0.Length; i++)
            {
                var p = toArch.MultiplyPoint3x4(v0[i]); s.arch[i] = p;
                s.delay[i] = AroundDoor(p); s.radial[i] = ((Vector2)p - _center).magnitude;           // 先记半径，PrepareRings 里换成截面上的位置
            }
            var stream = new List<int>();
            if (s.block)
            {
                s.island = parent; s.center = new Vector3[count]; s.from = new Vector3[count]; s.islandDelay = new float[count];
                var lo = new Vector3[count]; var hi = new Vector3[count]; var seen = new bool[count];
                for (int i = 0; i < v0.Length; i++)
                {
                    int k = parent[i];
                    if (!seen[k]) { seen[k] = true; lo[k] = hi[k] = v0[i]; } else { lo[k] = Vector3.Min(lo[k], v0[i]); hi[k] = Vector3.Max(hi[k], v0[i]); }
                }
                s.islandBig = new bool[count];
                for (int k = 0; k < count; k++)
                {
                    s.center[k] = (lo[k] + hi[k]) * 0.5f; s.islandDelay[k] = AroundDoor(toArch.MultiplyPoint3x4(s.center[k]));
                    var d = Vector3.Scale(hi[k] - lo[k], mf.transform.lossyScale);
                    s.islandBig[k] = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z)) > 0.8f;
                }
                for (int i = 0; i < v0.Length; i++)
                    if (s.islandBig[parent[i]]) stream.Add(i); else s.delay[i] = s.islandDelay[parent[i]];
            }
            else for (int i = 0; i < v0.Length; i++) stream.Add(i);
            s.stream = stream.ToArray();
            s.dyn = Instantiate(src); s.dyn.name = src.name + "_grow"; s.dyn.MarkDynamic();   // 章节加载时就备好：开门第一帧不卡
            _sweeps.Add(s);
            return true;
        }

        /// <summary>连续挤出：把门框沿途分成若干段，记下每段截面的内外半径；前沿之外的顶点收到前沿的截面上，不会因为门脚比门顶离中心远而戳出尖刺。</summary>
        static void PrepareRings(Sweep s)
        {
            s.ringLo = new float[Rings]; s.ringHi = new float[Rings];
            for (int i = 0; i < Rings; i++) { s.ringLo[i] = float.MaxValue; s.ringHi[i] = 0f; }
            int Ring(float d) => Mathf.Min(Rings - 1, (int)(d * Rings));
            foreach (var i in s.stream)
            {
                int b = Ring(s.delay[i]); float r = s.radial[i];
                s.ringLo[b] = Mathf.Min(s.ringLo[b], r); s.ringHi[b] = Mathf.Max(s.ringHi[b], r);
            }
            for (int i = 1; i < Rings; i++) if (s.ringHi[i] <= 0f) { s.ringLo[i] = s.ringLo[i - 1]; s.ringHi[i] = s.ringHi[i - 1]; }
            for (int i = Rings - 2; i >= 0; i--) if (s.ringHi[i] <= 0f) { s.ringLo[i] = s.ringLo[i + 1]; s.ringHi[i] = s.ringHi[i + 1]; }
            foreach (var i in s.stream)
            {
                int b = Ring(s.delay[i]);
                s.radial[i] = Mathf.InverseLerp(s.ringLo[b], s.ringHi[b], s.radial[i]);
            }
        }

        /// <summary>每一块都从“前一块”的位置冒出来。</summary>
        static void PrepareBlocks(Sweep s)
        {
            var order = new int[s.center.Length]; for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => s.islandDelay[a].CompareTo(s.islandDelay[b]));
            int prev = -1;
            foreach (var k in order)
            {
                if (s.islandBig[k]) continue;                                          // 门框主体不参与：小块不从门洞中心飞出来
                s.from[k] = s.center[prev >= 0 ? prev : k]; prev = k;
            }
            int n = s.center.Length; s.islandStart = new int[n + 1]; s.islandVerts = new int[s.v0.Length]; s.islandState = new int[n];
            foreach (var k in s.island) s.islandStart[k + 1]++;
            for (int k = 0; k < n; k++) s.islandStart[k + 1] += s.islandStart[k];
            var fill = new int[n];
            for (int i = 0; i < s.island.Length; i++) { int k = s.island[i]; s.islandVerts[s.islandStart[k] + fill[k]++] = i; }
        }

        void OnDestroy()
        {
            foreach (var s in _sweeps) if (s.dyn) Destroy(s.dyn);
            foreach (var f in _flats) if (f.dyn) Destroy(f.dyn);
        }

        /// <summary>按共用顶点位置和三角形把网格分成互不相连的几块，返回每个顶点所属的块号（0~count-1）。</summary>
        static int[] Islands(Mesh m, Vector3[] v, out int count)
        {
            var parent = new int[v.Length]; for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
            var weld = new Dictionary<Vector3Int, int>(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 10000f), Mathf.RoundToInt(v[i].y * 10000f), Mathf.RoundToInt(v[i].z * 10000f));
                if (weld.TryGetValue(k, out var o)) parent[Find(i)] = Find(o); else weld[k] = i;
            }
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) { parent[Find(t[i + 1])] = Find(t[i]); parent[Find(t[i + 2])] = Find(t[i]); }
            var ids = new Dictionary<int, int>(); var result = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                int r = Find(i); if (!ids.TryGetValue(r, out var id)) ids[r] = id = ids.Count;
                result[i] = id;
            }
            count = ids.Count;
            return result;
        }

        /// <summary>验收工具用：把开门动画定格在第 seconds 秒。</summary>
        public void PoseAt(float seconds) => Pose(seconds / duration);

        /// <summary>验收工具用：门框主体走的是哪种生长方式。</summary>
        public string SweepInfo()
        {
            var sb = new System.Text.StringBuilder($"小摆件 {_frame.Count}");
            foreach (var s in _sweeps) sb.Append($"；{s.mf.name} 分块 {(s.block ? s.center.Length : 0)} 连续挤出顶点 {s.stream.Length}/{s.v0.Length}");
            return sb.ToString();
        }

        /// <summary>封面：门马上收起来（不出现）。</summary>
        public void CloseNow()
        {
            Init(); _t = -1f; _open = false; _done = null;
            Pose(0f);
        }

        /// <summary>开门；动画播完回调 done（开始游戏）。门已经开着就直接回调。</summary>
        public void Open(Action done)
        {
            Init();
            if (IsOpen) { done?.Invoke(); return; }
            _done = done; _t = 0f; _open = true; _burst = false; _fxT = 0; _landed.Clear();
            foreach (var s in _sweeps) { s.dustNext = 0; if (s.islandState != null) Array.Clear(s.islandState, 0, s.islandState.Length); }
            Pose(0f);
            if (_arch) Bang.Play.BangAudio.At("portal_open", _arch.TransformPoint(_center), 1f, 1f);
        }

        void Update()
        {
            if (_t < 0) return;
            float prev = _t;
            _t += Time.deltaTime;
            if (prev < 1.45f && _t >= 1.45f && _arch) Bang.Play.BangAudio.At("portal_chime", _arch.TransformPoint(_center), 0.8f, 1f);
            Effects(prev, _t);
            if (_t >= duration) { Pose(1f); _t = -1f; if (_glow) _glow.gameObject.SetActive(false); var d = _done; _done = null; d?.Invoke(); return; }
            Pose(_t / duration);
        }

        static float Back(float x) { x = Mathf.Clamp01(x) - 1f; return 1f + 2.4f * x * x * x + 1.4f * x * x; }
        static float Out(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
        static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

        void Pose(float k)
        {
            float t = k * duration;
            // ① 门洞：每个轮廓点从中心往外长（各有先后、带抖动），长全后换回完整门洞
            if (_archC && _outline != null)
            {
                bool full = _open && t >= 1.5f;
                if (full) { if (!_shapeFull) { _archC.BuildWith(_outline); _shapeFull = true; } }
                else
                {
                    float P = !_open ? 0f : Out(Seg(t, 0.02f, 1.5f));
                    for (int i = 0; i < _outline.Length; i++)
                    {
                        float q = Mathf.Clamp01((P - _lag[i]) / Mathf.Max(0.05f, 1f - _lag[i]));
                        q = q * q * (3f - 2f * q);
                        float wob = (1f - P) * 0.05f * Mathf.Sin(t * 13f + i * 0.9f);              // 边缘长的时候轻轻抖
                        float g = !_open ? 0.001f : Mathf.Max(0.02f, q + wob * q);
                        _grow[i] = _center + (_outline[i] - _center) * g;
                    }
                    _archC.BuildWith(_grow); _shapeFull = false;
                }
            }
            // ② 门框：沿门洞外沿依次长出来
            for (int i = 0; i < _frame.Count; i++)
            {
                var c = _frame[i]; if (!c) continue;
                float d = i < _frameDelay.Count ? _frameDelay[i] : 0f;
                float a = FrameStart + d * FrameSpan;
                float f = !_open ? 0f : Back(Seg(t, a, a + FrameEach));
                c.localScale = _scale0[c] * Mathf.Max(0.0001f, f);
            }
            foreach (var s in _sweeps) PoseSweep(s, t);
            // ③ 门前地面：从门脚往外漫开
            if (_spill)
            {
                float R = !_open ? 0f : Out(Seg(t, 1.2f, 2.8f)) * (_spillR + 0.25f);
                bool done = _open && t >= duration - 1e-3f;
                foreach (var f in _flats)
                {
                    if (!f.mf) continue;
                    if (done) { f.mf.sharedMesh = f.src; continue; }
                    if (!f.dyn) { f.dyn = Instantiate(f.src); f.dyn.name = f.src.name + "_grow"; f.dyn.MarkDynamic(); }
                    for (int i = 0; i < f.v0.Length; i++)
                    {
                        var sp = f.sp0[i];
                        var xz = new Vector2(sp.x, sp.z); float dd = xz.magnitude;
                        float a = Mathf.Atan2(xz.y, xz.x);
                        float lim = R * (0.86f + 0.14f * Mathf.Sin(a * 5f + 0.7f) * Mathf.Sin(a * 2f + 1.9f));   // 前沿不规则
                        if (f.grow)
                        {
                            float g = Mathf.Clamp01((lim - dd) / 0.18f);                                       // 前沿经过后 18 cm 内长到原高
                            sp.y = Mathf.Lerp(Mathf.Min(sp.y, 0.012f), sp.y, g);
                        }
                        if (dd > lim) { var dir = dd > 1e-5f ? xz / dd : Vector2.zero; xz = dir * lim; sp.x = xz.x; sp.z = xz.y; }   // 前沿外面的收到前沿上（还没漫到）
                        f.v[i] = f.toMesh.MultiplyPoint3x4(sp);
                    }
                    f.dyn.vertices = f.v; f.dyn.RecalculateBounds();
                    f.mf.sharedMesh = f.dyn;
                }
                foreach (var (pt, d, s0) in _props)
                {
                    if (!pt) continue;
                    float g = !_open ? 0f : Back(Mathf.Clamp01((R - d) / 0.35f));
                    pt.localScale = s0 * Mathf.Max(0.0001f, g);
                }
            }
        }

        /// <summary>门框主体：从一侧长到另一侧。长完换回原网格，平时不占 CPU。</summary>
        void PoseSweep(Sweep s, float t)
        {
            if (!s.mf) return;
            if (!_open || t < FrameStart)
            {
                s.done = false; s.root.localScale = s.s0 * 0.0001f;
                if (s.dyn) s.mf.sharedMesh = s.src;
                return;
            }
            s.root.localScale = s.s0;
            if (t >= FrameStart + FrameSpan + FrameEach)
            {
                if (!s.done) { s.done = true; s.mf.sharedMesh = s.src; if (s.lod) s.lod.ForceLOD(-1); }
                return;
            }
            if (s.lod) s.lod.ForceLOD(0);
            if (Latest.TryGetValue(s.src, out var shared) && shared.dyn && shared.dyn != s.dyn && shared.t == t) { s.mf.sharedMesh = shared.dyn; return; }
            if (s.block)
            {
                for (int k = 0; k < s.center.Length; k++)
                {
                    if (s.islandBig[k]) continue;
                    float a = FrameStart + s.islandDelay[k] * FrameSpan, seg = Seg(t, a, a + FrameEach);
                    int state = seg <= 0f ? 1 : seg >= 1f ? 3 : 2;                     // 1 还没长 · 2 正在长 · 3 长好了（0 = 还没写过）
                    if (state != 2 && s.islandState[k] == state) continue;
                    s.islandState[k] = state;
                    float g = Back(seg), size = Mathf.Max(0.0001f, g);
                    var move = Vector3.LerpUnclamped(s.from[k], s.center[k], g);
                    for (int j = s.islandStart[k]; j < s.islandStart[k + 1]; j++)
                    {
                        int i = s.islandVerts[j];
                        s.v[i] = move + (s.v0[i] - s.center[k]) * size;
                    }
                }
            }
            if (s.stream.Length > 0)
            {
                float front = Mathf.Clamp01((t - FrameStart) / (FrameSpan + FrameEach));                  // 前沿走到哪：前沿之外的顶点都收到前沿这条半径上
                front = front * front * (3f - 2f * front) * 0.35f + front * 0.65f;
                float u = _sweepMin + front * _sweepRange;
                int ring = Mathf.Min(Rings - 1, (int)(front * Rings)); float lo = s.ringLo[ring], hi = s.ringHi[ring];
                var dir = (Vector2)(Quaternion.Euler(0, 0, -u * 360f) * Vector2.down);
                foreach (var i in s.stream)
                {
                    if (s.delay[i] <= front) { s.v[i] = s.v0[i]; continue; }
                    var p = _center + dir * Mathf.Lerp(lo, hi, s.radial[i]);
                    s.v[i] = s.toMesh.MultiplyPoint3x4(new Vector3(p.x, p.y, s.arch[i].z));
                }
            }
            s.dyn.SetVertices(s.v, 0, s.v.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            s.mf.sharedMesh = s.dyn; Latest[s.src] = (s.dyn, t);
        }

        /// <summary>门洞光晕：一块跟着门洞大小的柔光（刚开始最亮——光从小洞里透出来；长大时减弱；长全那一下再亮一次后淡出）。</summary>
        void Glow(float t)
        {
            var fx = Bang.Play.PlayFx.Instance;
            if (!_glow)
            {
                if (!fx || !fx.wordMat || !fx.glowTex || !_arch) return;
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(g.GetComponent<Collider>()); g.name = "OpenGlow";
                g.transform.SetParent(transform, false);
                _glow = g.GetComponent<MeshRenderer>(); _glow.sharedMaterial = fx.wordMat; _glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _gmpb = new MaterialPropertyBlock();
            }
            float a = t < 0.45f ? Mathf.Lerp(0.7f, 1f, t / 0.45f)
                    : t < 1.5f ? Mathf.Lerp(0.6f, 0.3f, (t - 0.45f) / 1.05f)
                    : t < 1.62f ? Mathf.Lerp(0.3f, 1f, (t - 1.5f) / 0.12f)
                    : Mathf.Lerp(1f, 0f, Mathf.Clamp01((t - 1.62f) / 0.75f));
            _glow.gameObject.SetActive(t >= 0 && t < 2.4f && a > 0.01f);
            if (!_glow.gameObject.activeSelf) return;
            var b = _archC.LocalBounds; float P = Out(Seg(t, 0.02f, 1.5f));
            float burst = t >= 1.5f ? 1f + 0.35f * Mathf.Clamp01((t - 1.5f) / 0.5f) : 1f;
            var c = _arch.TransformPoint(new Vector3(_center.x, _center.y, 0.03f));                   // 门面往房间这边 3 cm
            _glow.transform.position = c; _glow.transform.rotation = _arch.rotation * Quaternion.Euler(0, 180f, 0);
            float gw = Mathf.Max(b.size.x * P * 1.5f + 0.12f, t < 0.6f ? 0.35f : 0f), gh = Mathf.Max(b.size.y * P * 1.25f + 0.1f, t < 0.6f ? 0.35f : 0f);
            _glow.transform.localScale = new Vector3(gw * burst, gh * burst, 1f);
            _gmpb.Clear(); _gmpb.SetTexture("_BaseMap", fx.glowTex); _gmpb.SetColor("_Color", new Color(0.92f, 0.97f, 1f, a)); _glow.SetPropertyBlock(_gmpb);
        }

        Vector3 W(float x, float y) => _arch ? _arch.TransformPoint(new Vector3(x, y, 0.02f)) : transform.position;   // 门洞局部点 → 世界（略往房间这边）

        void Effects(float prev, float t)
        {
            Glow(t);
            var fx = Bang.Play.PlayFx.Instance; if (!fx || !_arch) return;
            _fxT -= Time.deltaTime;
            // 门洞边缘（正在长的轮廓）沿途冒光点
            if (t < 1.55f && _fxT <= 0 && _grow != null && _grow.Length > 0)
            {
                _fxT = 0.035f;
                for (int k = 0; k < 3; k++)
                {
                    var p = _grow[UnityEngine.Random.Range(0, _grow.Length)];
                    if (p.y < 0.05f) continue;                                          // 贴地那一段不冒（地上冒火花不对劲）
                    var wp = _arch.TransformPoint(new Vector3(p.x, p.y, 0.02f));
                    fx.Flash(wp, k == 0 ? Pink : Spark, 0.12f, 0.4f);                          // 远处也看得清的柔光点
                    if (k == 1) fx.Glitter(wp, Spark, 2, 0.5f);
                }
            }
            // 门洞长全那一下：一记柔光闪 + 彩纸星光爆开
            if (!_burst && t >= 1.5f)
            {
                _burst = true;
                var h = _archC.LocalBounds.size.y;
                var c = W(_center.x, h * 0.5f);
                fx.Flash(c, new Color(1f, 0.93f, 0.98f), 1.6f, 0.55f);
                fx.Confetti(c + _arch.forward * 0.2f, 46);                    // 往房间这边撒
                fx.Glitter(c, Spark, 26, 1.6f); fx.Glitter(c, Pink, 18, 1.2f);
            }
            // 门框零件长到位：扬一点灰（隔一块扬一次）
            for (int i = 0; i < _frame.Count; i++)
            {
                if (_landed.Contains(i) || !_frame[i]) continue;
                float d = i < _frameDelay.Count ? _frameDelay[i] : 0f;
                if (t >= FrameStart + FrameEach + d * FrameSpan) { _landed.Add(i); if (i % 2 == 0) fx.Dust(_frame[i].position, 0.35f, 2); }
            }
            foreach (var s in _sweeps)                                                 // 门框主体：前沿走到哪，哪里扬一点灰
                while (s.dust != null && s.dustNext < s.dust.Length && t >= FrameStart + FrameEach + s.dust[s.dustNext].d * FrameSpan)
                {
                    if (s.dustNext % 2 == 0) fx.Dust(_arch.TransformPoint(s.dust[s.dustNext].p), 0.3f, 2);
                    s.dustNext++;
                }
        }
    }
}
