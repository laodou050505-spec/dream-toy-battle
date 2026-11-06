// 封面“玩具小剧场”的活气（BangTheater 搭的预制体）：
//   幕布：出现时合着，停一下缓缓拉开（露出台上的演员）；点“开始游戏”时合上，剧场收小消失，再开门（Close）
//   演员（站在幕布前的台口，幕布开合碰不到）：各有各的小表演——插旗兵 A 举手挥一挥、插旗兵 B 蹦两下双手欢呼、
//     胖墩跺脚拍肚子、发条老鼠一路小蹦（尾巴甩、钥匙转）；每 12 秒一起鞠躬谢幕；被玩家用手指枪打中会“哎哟”一跳（CoverPractice）
//   气球靶挂在幕后（幕布拉开才露出来），是封面练习的靶子
//   灯泡：跑马灯（一亮两暗往前走，偶尔整圈一起闪两下）；看向大按钮时圆顶发光
// 演员动作只用整体位移 + 绕竖直轴 / 左右横轴的小角度转动（不碰零件的未知轴向，避免“动作奇怪”）。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Play.UI
{
    public class TitleStageFx : MonoBehaviour
    {
        public UiButton start;
        Renderer[] _bulbs; Renderer[] _dome;
        Transform _curL, _curR, _theater, _tieL, _tieR; float _tie, _lastDt;
        float _t, _flashT = 4f, _closeT = -1f; Action _closed;
        MaterialPropertyBlock _mpb;
        static readonly int IdEm = Shader.PropertyToID("_EmissionColor");
        static readonly Color Warm = new(1f, 0.78f, 0.42f);
        const float OpenK = 0.42f;                                     // 幕布拉开后的宽度比例

        class Actor { public Transform t; public Vector3 p0, s0; public Quaternion r0; public float phase, ouchT = -1f; public Bang.Play.EnemyRig rig; public float keyAng; public bool floaty; public string role; public MeshFilter[] feet; public float floor; }
        readonly List<Actor> _cast = new();
        readonly List<(Transform t, Vector3 p0, Quaternion r0, float ph)> _float = new();

        public bool Closing => _closeT >= 0;

        public void Init(GameObject unused)
        {
            _mpb = new MaterialPropertyBlock();
            var bl = new List<Renderer>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bulbs") bl.AddRange(t.GetComponentsInChildren<Renderer>());
                if (t.name == "Curtain_L") _curL = t; else if (t.name == "Curtain_R") _curR = t; else if (t.name == "Theater") _theater = t;
                else if (t.name == "TieBack_L") _tieL = t; else if (t.name == "TieBack_R") _tieR = t;
            }
            _bulbs = bl.ToArray();
            var d = FindDeep("BtnStart/Dome"); _dome = d ? d.GetComponentsInChildren<Renderer>() : new Renderer[0];
            var cast = FindDeep("Cast");
            if (cast)
            {
                int i = 0;
                foreach (Transform c in cast)
                {
                    var a = new Actor { t = c, p0 = c.localPosition, s0 = c.localScale, r0 = c.localRotation, phase = i++ * 0.9f, rig = c.GetComponent<Bang.Play.EnemyRig>(), floaty = c.name.Contains("Balloon") };
                    a.role = c.name.StartsWith("Planter_A") ? "wave" : c.name.StartsWith("Planter_B") ? "jump" : c.name.StartsWith("Brute") ? "stomp" : c.name.StartsWith("Mouse") ? "hop" : a.floaty ? "float" : "idle";
                    if (a.rig && a.rig.pack) a.rig.pack.gameObject.SetActive(false);     // 背后的小旗从正面看像个“鸟嘴”（用户：玩具有点粗糙）：台上不背旗
                    a.feet = System.Array.FindAll(c.GetComponentsInChildren<MeshFilter>(true), m => m.name.Contains("Foot") && m.sharedMesh);
                    a.floor = FeetMin(a, cast);
                    _cast.Add(a);
                }
                _cast.Sort((a, b) => a.p0.x.CompareTo(b.p0.x));          // 鞠躬从左到右
            }
            // 标题（中 / 英两套）整体缓缓浮动；前景金星慢慢自转、上下漂
            var sign = FindDeep("Sign");
            if (sign) foreach (Transform c in sign) _float.Add((c, c.localPosition, c.localRotation, 0f));
            var fore = transform.Find("Foreground");
            if (fore) { int k = 0; foreach (Transform c in fore) _float.Add((c, c.localPosition, c.localRotation, 1f + k++ * 1.7f)); }
            SetCurtains(0f);
        }

        public Transform FindDeepPublic(string path) => FindDeep(path);

        /// <summary>脚底最低点（Cast 局部坐标）：鞠躬、落地压扁时脚会往下沉，低于站立时的高度就把整个演员抬回来（不踩进台面）。</summary>
        static float FeetMin(Actor a, Transform cast)
        {
            if (a.feet == null || a.feet.Length == 0) return float.NaN;
            float m = float.MaxValue;
            foreach (var f in a.feet)
            {
                var vs = f.sharedMesh.vertices; var l2w = f.transform.localToWorldMatrix; var w2c = cast.worldToLocalMatrix;
                for (int i = 0; i < vs.Length; i++) { float y = w2c.MultiplyPoint3x4(l2w.MultiplyPoint3x4(vs[i])).y; if (y < m) m = y; }
            }
            return m;
        }

        /// <summary>封面练习：被手指枪打中的演员“哎哟”一跳。</summary>
        public void Ouch(Transform t)
        {
            foreach (var a in _cast) if (a.t && (t == a.t || t.IsChildOf(a.t)))
            {
                a.ouchT = 0f;
                Bang.Play.BangAudio.At(a.role == "hop" ? "mouse_squeak" : "hit_plastic", a.t.position, 0.8f, a.role == "hop" ? 1.2f : 1.1f);
                PlayFx.Instance?.HitStar(a.t.position + Vector3.up * 0.12f);
            }
        }

        bool _hiStart;
        /// <summary>练习最后一步：大按钮一跳一跳地发光，提示“打它出发”。</summary>
        public void HighlightStart(bool on) => _hiStart = on;

        Transform FindDeep(string path)
        {
            var t = transform.Find(path); if (t) return t;
            if (_theater) { t = _theater.Find(path); if (t) return t; }
            foreach (Transform c in transform) { t = c.Find(path); if (t) return t; }
            return null;
        }

        /// <summary>开始游戏：幕布合上 → 剧场收小消失 → 回调（开门）。</summary>
        public void Close(Action done) { if (_closeT >= 0) return; _closed = done; _closeT = 0f; CurtainSfx(0.9f); }

        bool _openSfx;
        void CurtainSfx(float pitch) { var c = _curL ? _curL.position : transform.position; Bang.Play.BangAudio.At("curtain", c, 1f, pitch); if (_curR) Bang.Play.BangAudio.At("curtain", _curR.position, 0.8f, pitch * 1.06f); }

        void SetCurtains(float open)                                  // open 0 = 合上，1 = 拉开
        {
            // 束幕金绳：幕布开到 95% 才弹出来，合幕前先收起（金绳挂在开着的幕布前面，合上的幕布会穿过它）
            float want = open > 0.95f ? 1f : 0f;
            _tie = Mathf.MoveTowards(_tie, want, _lastDt * (want > _tie ? 2.2f : 6f));
            float ts = want > 0 ? UiKit.Back(_tie) : _tie;
            foreach (var tb in new[] { _tieL, _tieR }) if (tb) tb.localScale = Vector3.one * Mathf.Max(0.001f, ts);
            float k = Mathf.Lerp(1f, OpenK, open);
            if (_curL) { var s = _curL.localScale; _curL.localScale = new Vector3(k, s.y, s.z); }
            if (_curR) { var s = _curR.localScale; _curR.localScale = new Vector3(-k, s.y, s.z); }
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

        void Update() => Step(Time.unscaledDeltaTime);

        /// <summary>改漂动物件的基准位置（英文标题换了字形，老鼠换个地方站）。</summary>
        public void MoveFloat(Transform t, Vector3 pos)
        {
            for (int i = 0; i < _float.Count; i++) if (_float[i].t == t) _float[i] = (t, pos, _float[i].r0, _float[i].ph);
        }

        /// <summary>推进一帧（编辑器里的穿模巡检也用它把整段表演逐帧跑一遍：cs/stageclip.cs）。</summary>
        public void Step(float dt)
        {
            if (_bulbs == null) return;                 // 还没 Init（实例化当帧）
            _lastDt = dt; _t += dt; _flashT -= dt;

            // 幕布
            if (_closeT >= 0)
            {
                _closeT += dt;
                SetCurtains(1f - Smooth(_closeT / 0.7f));
                float s = 1f - Smooth((_closeT - 0.75f) / 0.4f);
                transform.localScale = Vector3.one * Mathf.Max(0.0001f, s);
                if (_closeT >= 1.2f) { var d = _closed; _closed = null; _closeT = -1f; d?.Invoke(); return; }
            }
            else { SetCurtains(Smooth((_t - 0.5f) / 1.3f)); if (!_openSfx && _t > 0.5f) { _openSfx = true; CurtainSfx(1f); } }   // 幕布拉开：唰的一声（左右两幅各一声）

            // 跑马灯
            int step = Mathf.FloorToInt(_t / 0.12f);
            bool flashAll = _flashT < 0.5f && Mathf.Repeat(_flashT, 0.25f) < 0.12f;
            if (_flashT < 0) _flashT = UnityEngine.Random.Range(5f, 8f);
            for (int i = 0; i < _bulbs.Length; i++)
            {
                if (!_bulbs[i]) continue;
                bool on = flashAll || (i + step) % 3 == 0;
                _mpb.Clear(); _mpb.SetColor(IdEm, on ? Warm * 1.15f : new Color(0.06f, 0.04f, 0.02f)); _bulbs[i].SetPropertyBlock(_mpb);
            }
            float glow = start && start.Hovered ? 0.35f + 0.15f * Mathf.Sin(_t * 6f) : _hiStart ? 0.2f + 0.2f * Mathf.Abs(Mathf.Sin(_t * 3.5f)) : 0.05f;
            foreach (var r in _dome) { if (!r) continue; _mpb.Clear(); _mpb.SetColor(IdEm, new Color(1f, 0.45f, 0.5f) * glow); r.SetPropertyBlock(_mpb); }

            foreach (var (t, p0, r0, ph) in _float)
            {
                if (!t) continue;
                if (ph == 0f) { t.localPosition = p0 + Vector3.up * Mathf.Sin(_t * 0.9f) * 0.008f; t.localRotation = r0 * Quaternion.Euler(0, 0, Mathf.Sin(_t * 0.6f) * 1.2f); }
                else { t.localPosition = p0 + Vector3.up * Mathf.Sin(_t * 0.7f + ph) * 0.02f; t.localRotation = r0 * Quaternion.Euler(0, _t * 25f + ph * 40f, 0); }
            }

            // 演员：各自的小表演（12 秒一轮，错开）；每轮最后一起鞠躬谢幕
            float cyc = Mathf.Repeat(_t - 2.2f, 12f);
            for (int i = 0; i < _cast.Count; i++)
            {
                var a = _cast[i]; if (!a.t) continue;
                var r = a.rig;
                float bob = 0, sway = Mathf.Sin(_t * 0.8f + a.phase) * 4f, bow = 0, sq = 0, spin = 0, roll = 0; Vector3 off = Vector3.zero;
                float armL = 0, armR = 0, armLz = 8f, armRz = -8f; bool arms = false;   // 左臂 +Z、右臂 −Z = 往外张（反了会插进身体）
                if (_t > 2.2f && cyc > 10.6f && a.role != "float" && a.role != "idle") { float u = (cyc - 10.6f - i * 0.12f) / 1.0f; if (u > 0 && u < 1) bow = Mathf.Sin(u * Mathf.PI) * 22f; }
                switch (a.role)
                {
                    case "float": bob = Mathf.Sin(_t * 1.1f + a.phase) * 0.012f; sway = Mathf.Sin(_t * 0.8f + a.phase) * 4f; break;
                    case "wave":                                                                  // 举起右手挥一挥（0.5 秒举起、1.8 秒挥、0.5 秒放下）
                    {
                        float u = Mathf.Repeat(_t - 1.0f, 6f); arms = true;
                        float up = u < 0.5f ? Mathf.SmoothStep(0, 1, u / 0.5f) : u < 2.3f ? 1f : u < 2.8f ? 1f - Mathf.SmoothStep(0, 1, (u - 2.3f) / 0.5f) : 0f;
                        armR = -155f * up; armRz = -8f - up * (10f + 12f * Mathf.Sin(_t * 11f));   // 举过头顶后往外侧左右挥（不往头那边挥）
                        armL = Mathf.Sin(_t * 1.6f) * 6f;
                        bob = Mathf.Abs(Mathf.Sin(_t * 2.2f + a.phase)) * 0.004f;
                        sway = up * 8f;                                                         // 挥手时身子朝观众微侧
                        break;
                    }
                    case "jump":                                                                  // 蹦两下、双手举过头欢呼
                    {
                        float u = Mathf.Repeat(_t - 3.0f, 6f); arms = true;
                        if (u < 1.2f)
                        {
                            float k = (u % 0.6f) / 0.6f;
                            bob = Mathf.Sin(k * Mathf.PI) * 0.045f;
                            sq = k < 0.12f ? (0.12f - k) * 1.2f : k > 0.9f ? (k - 0.9f) * 1.5f : -Mathf.Sin(k * Mathf.PI) * 0.06f;   // 起跳前蹲、空中拉长、落地压扁
                            armL = armR = -160f; armLz = 18f + Mathf.Sin(_t * 18f) * 10f; armRz = -18f - Mathf.Sin(_t * 18f) * 10f;
                        }
                        else bob = Mathf.Abs(Mathf.Sin(_t * 2.2f + a.phase)) * 0.004f;
                        break;
                    }
                    case "stomp":                                                                 // 胖墩：左右跺脚、两手拍肚子
                    {
                        float u = Mathf.Repeat(_t - 5.0f, 6f); arms = true;
                        if (u < 1.6f)
                        {
                            float st = Mathf.Sin(u * Mathf.PI * 2.5f);
                            sway = st * 9f; bob = Mathf.Max(0, st) * 0.012f; sq = Mathf.Max(0, -st) * 0.05f;
                            float pat = Mathf.Sin(_t * 9f);
                            armL = -40f - Mathf.Max(0, pat) * 30f; armR = -40f - Mathf.Max(0, -pat) * 30f; armLz = 20f; armRz = -20f;
                        }
                        else { bob = Mathf.Abs(Mathf.Sin(_t * 1.6f + a.phase)) * 0.004f; }
                        break;
                    }
                    case "hop":                                                                   // 发条老鼠：站在台上安静待机（用户：老鼠不要一直蹦）——慢慢左右张望、轻轻呼吸、尾巴慢摆、发条钥匙慢转
                    {
                        bob = 0f; sq = -Mathf.Sin(_t * 2.4f + a.phase) * 0.012f;                       // 呼吸：身体微微一胀一缩（不离地）
                        spin = Mathf.Sin(_t * 0.6f + a.phase) * 14f;                                  // 张望
                        if (r && r.tail != null) for (int ti = 0; ti < r.tail.Length; ti++) if (r.tail[ti]) r.tail[ti].localRotation = Quaternion.Euler(0, Mathf.Sin(_t * 1.8f - ti * 0.7f) * 12f, 0);
                        if (r && r.key) { a.keyAng += dt * 70f; r.key.localRotation = r.keyBase * Quaternion.Euler(0, a.keyAng, 0); }   // 发条钥匙：绕杆顺时针（从身后看），慢转
                        break;
                    }
                    default: bob = Mathf.Abs(Mathf.Sin(_t * 2.2f + a.phase)) * 0.006f; break;
                }
                // 被打中：“哎哟”——往旁边一歪、跳起来再落下（0.6 秒；不往后仰，后面就是幕布）
                if (a.ouchT >= 0)
                {
                    a.ouchT += dt; float u = a.ouchT / 0.6f;
                    if (u >= 1f) a.ouchT = -1f;
                    else { bob += Mathf.Sin(u * Mathf.PI) * 0.06f; roll = Mathf.Sin(u * Mathf.PI) * (a.phase % 1.8f < 0.9f ? 16f : -16f); sq += u > 0.85f ? (u - 0.85f) * 0.8f : 0f; arms = true; armL = armR = -120f * Mathf.Sin(u * Mathf.PI); }
                }
                a.t.localPosition = a.p0 + Vector3.up * bob + off;
                a.t.localRotation = a.r0 * Quaternion.Euler(bow, sway + spin * (a.role == "hop" ? 1f : 0f), roll);   // 演员已转成面朝观众：绕本地 X 正角 = 向观众鞠躬
                a.t.localScale = new Vector3(a.s0.x * (1f + sq * 0.6f), a.s0.y * (1f - sq), a.s0.z * (1f + sq * 0.6f));
                if (!float.IsNaN(a.floor)) { float fm = FeetMin(a, a.t.parent); if (fm < a.floor) a.t.localPosition += Vector3.up * (a.floor - fm); }
                if (r && arms)
                {
                    if (r.armL) r.armL.localRotation = Quaternion.Slerp(r.armL.localRotation, Quaternion.Euler(armL, 0, armLz), Mathf.Clamp01(dt * 14f));
                    if (r.armR) r.armR.localRotation = Quaternion.Slerp(r.armR.localRotation, Quaternion.Euler(armR, 0, armRz), Mathf.Clamp01(dt * 14f));
                }
            }
        }
    }
}
