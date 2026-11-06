// 视线锁定准星：四片糖果角括号（nano banana 美术 FX_LockReticle，按四个象限切成四片）+ 一层淡淡的粉彩光晕。
//   锁上：四片从外面“咔哒”收拢到目标四角（缓出带过冲），光晕同时弹出；
//   锁住期间：四片轻轻呼吸（一张一合），整体慢慢左右摆一点，不抢目标的戏；
//   换目标：滑过去并按目标大小缩放，不瞬移；
//   打中：四片往外一弹再收回；目标被打死：四片向外飞散并淡出；
//   金色子弹期间：准星染成金色、光晕更亮——增益在准星上也看得见。
// 始终面向玩家，最后画（不被门框、小怪挡住）。
using UnityEngine;

namespace Bang.Play
{
    public class LockReticle : MonoBehaviour
    {
        public float closeTime = 0.22f;
        Transform[] _corner = new Transform[4];
        MeshRenderer[] _cr = new MeshRenderer[4];
        MeshRenderer _glow;
        MaterialPropertyBlock _mpb, _gmpb;
        Vector3 _pos; float _size, _sizeShown;
        float _show, _close, _pulse, _burst, _age;
        bool _on, _gold;
        static readonly int IdST = Shader.PropertyToID("_BaseMap_ST"), IdColor = Shader.PropertyToID("_Color");
        // 每片：象限 UV 偏移、在准星里的方向
        static readonly Vector2[] Uv = { new(0, 0.5f), new(0.5f, 0.5f), new(0, 0), new(0.5f, 0) };
        static readonly Vector2[] Dir = { new(-1, 1), new(1, 1), new(-1, -1), new(1, -1) };

        public static LockReticle Create(Transform parent, Material cornerMat, Material glowMat, Texture glowTex)
        {
            var g = new GameObject("LockReticle"); g.transform.SetParent(parent, false);
            var v = g.AddComponent<LockReticle>(); v._mpb = new MaterialPropertyBlock(); v._gmpb = new MaterialPropertyBlock();
            for (int i = 0; i < 4; i++)
            {
                var c = new GameObject("Corner" + i); c.transform.SetParent(g.transform, false);
                c.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
                var r = c.AddComponent<MeshRenderer>(); r.sharedMaterial = cornerMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                v._corner[i] = c.transform; v._cr[i] = r;
            }
            if (glowMat)
            {
                var gl = new GameObject("Glow"); gl.transform.SetParent(g.transform, false);
                gl.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
                v._glow = gl.AddComponent<MeshRenderer>(); v._glow.sharedMaterial = glowMat;
                v._glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (glowTex) v._gmpb.SetTexture("_BaseMap", glowTex);
            }
            g.SetActive(false);
            return v;
        }

        /// <summary>每帧调用：target 为 null 表示没锁定。size = 目标框边长（米）。</summary>
        /// <summary>big = 大目标（Boss）：不在身上盖光晕、四个角放到轮廓外面、更淡——框住 Boss 时不挡住 Boss（用户：选中框到 Boss 身上，Boss 被挡住，看着不舒服）。</summary>
        public bool big;
        public void Track(Vector3? target, float size, bool gold)
        {
            _gold = gold;
            if (target.HasValue)
            {
                if (!_on || _show < 0.05f) { _pos = target.Value; _sizeShown = size; _close = 0; Lock(); }
                _on = true; _pos = target.Value; _size = size;
            }
            else _on = false;
        }

        /// <summary>换了锁定目标：重新收拢一次（从稍微张开的位置）。</summary>
        public void Lock() { _close = Mathf.Min(_close, 0.35f); _burst = 0; if (!gameObject.activeSelf) gameObject.SetActive(true); }
        public void Pulse() => _pulse = 1;
        public void Burst() { if (_show > 0.1f) { _burst = 0.0001f; _on = false; } }

        void LateUpdate()
        {
            float dt = Time.deltaTime; _age += dt;
            _show = Mathf.MoveTowards(_show, _on ? 1 : 0, dt * (_on ? 8f : _burst > 0 ? 2.5f : 6f));
            if (_show <= 0 && !_on) { _burst = 0; gameObject.SetActive(false); return; }
            _close = Mathf.MoveTowards(_close, 1, dt / closeTime);
            _pulse = Mathf.MoveTowards(_pulse, 0, dt * 5f);
            if (_burst > 0) _burst += dt * 3f;
            _sizeShown = Mathf.Lerp(_sizeShown, _size, 1 - Mathf.Exp(-dt * 14f));
            transform.position = Vector3.Lerp(transform.position, _pos, _show < 0.99f && _close < 0.1f ? 1 : 1 - Mathf.Exp(-dt * 22f));

            var cam = Camera.main;
            if (cam)
            {
                var fwd = transform.position - cam.transform.position;
                float wob = Mathf.Sin(_age * 1.7f) * 5f;                  // 慢慢左右摆一点
                transform.rotation = Quaternion.LookRotation(fwd, cam.transform.up) * Quaternion.Euler(0, 0, wob);
            }

            // 收拢：easeOutBack（从 1.7 倍张开处落到 1，过冲一点）
            float x = _close - 1f, k = 1f + 2.70158f * x * x * x + 1.70158f * x * x;
            float open = Mathf.LerpUnclamped(0.7f, 0f, k);
            float breathe = Mathf.Sin(_age * 4.2f) * 0.035f * _close;
            float spread = 0.5f + open + breathe + _pulse * 0.16f + _burst * 0.9f + (big ? 0.35f : 0f);
            float piece = _sizeShown * (big ? 0.26f : 0.5f);
            Color c = _gold ? new Color(1f, 0.86f, 0.35f, 1) : Color.white;
            float a = _show * (_burst > 0 ? Mathf.Clamp01(1 - _burst) : 1) * (0.55f + 0.45f * Mathf.Clamp01(_close * 2)) * (big ? 0.8f : 1f);
            for (int i = 0; i < 4; i++)
            {
                var d = Dir[i];
                _corner[i].localPosition = new Vector3(d.x, d.y, 0) * (_sizeShown * spread * 0.5f);
                _corner[i].localScale = Vector3.one * piece * (1 + _pulse * 0.1f);
                _corner[i].localRotation = Quaternion.Euler(0, 0, _burst * 40f * -d.x * d.y);
                _mpb.Clear();
                _mpb.SetVector(IdST, new Vector4(0.5f, 0.5f, Uv[i].x, Uv[i].y));
                _mpb.SetColor(IdColor, new Color(c.r, c.g, c.b, a * 0.92f));
                _cr[i].SetPropertyBlock(_mpb);
            }
            if (_glow) _glow.enabled = !big;
            if (_glow && !big)
            {
                _glow.transform.localPosition = new Vector3(0, 0, 0.01f);
                _glow.transform.localScale = Vector3.one * _sizeShown * (1.5f + _pulse * 0.3f) * Mathf.Clamp01(k);
                var mp = _gmpb;
                var gc = _gold ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.72f, 0.86f);
                mp.SetColor(IdColor, new Color(gc.r, gc.g, gc.b, a * (_gold ? 0.5f : 0.28f) * (0.85f + 0.15f * Mathf.Sin(_age * 4.2f))));
                _glow.SetPropertyBlock(mp);
            }
        }
    }
}
