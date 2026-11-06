// 面向玩家的圆环进度（Bang/FxRing）：小怪 / 旗子的血量环（受伤后才出现，掉血时先闪白再缩到新值）、换弹环（带金区）。
// 出现 / 消失都用缩放弹入弹出，不突兀。
using UnityEngine;

namespace Bang.Play
{
    public class RingView : MonoBehaviour
    {
        public float size = 0.12f;
        MeshRenderer _r; MaterialPropertyBlock _mpb;
        float _shown, _target, _fill = 1, _fillShown = 1, _flash, _goldA, _goldB;
        Color _color = new Color(1f, 0.45f, 0.55f, 1);
        static readonly int IdFill = Shader.PropertyToID("_Fill"), IdColor = Shader.PropertyToID("_Color"), IdA = Shader.PropertyToID("_GoldA"), IdB = Shader.PropertyToID("_GoldB"), IdAlpha = Shader.PropertyToID("_Alpha"), IdBack = Shader.PropertyToID("_Back");

        public static RingView Create(Transform parent, Material mat, float size)
        {
            var g = new GameObject("Ring"); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var v = g.AddComponent<RingView>(); v.size = size; v._r = r; v._mpb = new MaterialPropertyBlock();
            g.transform.localScale = Vector3.zero;
            return v;
        }

        public void Show(bool on) => _target = on ? 1 : 0;
        public void SetColor(Color c) => _color = c;
        public void SetGold(float a, float b) { _goldA = a; _goldB = b; }
        public void SetFill(float f, bool flash = false)
        {
            if (flash && f < _fill) _flash = 1;
            _fill = Mathf.Clamp01(f);
        }
        public void Snap(float f) { _fill = _fillShown = Mathf.Clamp01(f); }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _shown = Mathf.MoveTowards(_shown, _target, dt * 6f);
            // 弹入有一点过冲，弹出直接缩没
            float x = _shown - 1f, s = _target > 0.5f ? 1f + 2.70158f * x * x * x + 1.70158f * x * x : _shown;   // easeOutBack
            transform.localScale = Vector3.one * size * Mathf.Max(0, s) * (_shown > 0 ? 1 : 0);
            if (_shown <= 0) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();       // 被 Instantiate 复制出来的环：运行时字段没带过来
            if (!_r) { _r = GetComponent<MeshRenderer>(); if (!_r) return; }
            _fillShown = Mathf.MoveTowards(_fillShown, _fill, dt * (_fill < _fillShown ? 1.6f : 4f));
            _flash = Mathf.MoveTowards(_flash, 0, dt * 5f);
            var cam = Camera.main;
            if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
            _mpb.Clear();
            _mpb.SetFloat(IdFill, _fillShown);
            _mpb.SetColor(IdColor, Color.Lerp(_color, Color.white, _flash));
            _mpb.SetColor(IdBack, new Color(1, 1, 1, 0.3f + _flash * 0.4f));
            _mpb.SetFloat(IdA, _goldA); _mpb.SetFloat(IdB, _goldB);
            _mpb.SetFloat(IdAlpha, Mathf.Clamp01(_shown * 1.5f));
            _r.SetPropertyBlock(_mpb);
        }
    }
}
