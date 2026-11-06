// 玩具手盾（Blender 精细建模 Tools/blender_shield.py，按 nano banana 参考图）：举手掌心朝外时出现在手前。
//   举起：从手心“啵”地弹出（缓出带过冲 0.2 秒）；放下：缩回手心。
//   挡住泡沫弹：盾面被压扁一下再弹回（阻尼弹簧），闪白 + 亮片；耐久越低颜色越偏粉红、晃得越厉害，提醒快碎了；
//   弹反：金光一闪 + 金色亮片；碎了：护边和星星飞散，盘面碎成彩纸，冷却结束后下次举起又是新的。
using Bang.Logic;
using UnityEngine;

namespace Bang.Play
{
    public class ShieldView : MonoBehaviour
    {
        Transform _model; Renderer[] _rs; MaterialPropertyBlock _mpb;
        float _show, _sq, _sqV, _flash; Color _flashC = Color.white;
        bool _up; Vector3 _pos; Quaternion _rot = Quaternion.identity; float _wob;
        static readonly int IdColor = Shader.PropertyToID("_Color");

        public static ShieldView Create(GameObject prefab, Transform parent)
        {
            var g = Instantiate(prefab, parent); g.name = "Shield";
            var v = g.AddComponent<ShieldView>();
            v._model = g.transform.childCount > 0 ? g.transform.GetChild(0) : g.transform;
            v._rs = g.GetComponentsInChildren<Renderer>(); v._mpb = new MaterialPropertyBlock();
            g.transform.localScale = Vector3.zero;
            return v;
        }

        public void Sync(ShieldState sh, double durMax, float dt)
        {
            bool up = sh.up && sh.pos != null && sh.normal != null;
            if (up && !_up) { _show = Mathf.Min(_show, 0.2f); }
            _up = up;
            if (up)
            {
                var p = LogicSpace.ToUnity(sh.pos); var n = LogicSpace.ToUnity(sh.normal).normalized;
                var cam = Camera.main;
                var upv = cam ? cam.transform.up : Vector3.up;
                var r = Quaternion.LookRotation(n, upv);
                float k = _show < 0.05f ? 1 : 1 - Mathf.Exp(-dt * 30f);
                _pos = Vector3.Lerp(_pos, p, k); _rot = Quaternion.Slerp(_rot, r, k);
            }
            _show = Mathf.MoveTowards(_show, up ? 1 : 0, dt * (up ? 5f : 8f));
            if (_show <= 0) { transform.localScale = Vector3.zero; return; }

            // 压扁弹簧
            _sqV += (-_sq * 420f - _sqV * 18f) * dt; _sq += _sqV * dt;
            float wear = durMax > 0 ? 1f - Mathf.Clamp01((float)(sh.dur / durMax)) : 0;
            _wob += dt * (6f + wear * 14f);
            float x = _show - 1f, pop = up ? 1f + 2.70158f * x * x * x + 1.70158f * x * x : _show;
            transform.SetPositionAndRotation(_pos, _rot * Quaternion.Euler(Mathf.Sin(_wob) * wear * 4f, Mathf.Cos(_wob * 1.3f) * wear * 4f, 0));
            transform.localScale = new Vector3(1 + _sq * 0.5f, 1 + _sq * 0.5f, 1 - _sq) * Mathf.Max(0, pop);

            _flash = Mathf.MoveTowards(_flash, 0, dt * 4f);
            var c = Color.Lerp(Color.white, new Color(1f, 0.7f, 0.75f), wear * 0.8f);
            c = Color.Lerp(c, _flashC, _flash);
            foreach (var rr in _rs) { if (!rr) continue; rr.GetPropertyBlock(_mpb); _mpb.SetColor(IdColor, c); rr.SetPropertyBlock(_mpb); }
        }

        public void Block()
        {
            _sqV -= 9f; _flash = 1; _flashC = new Color(1.4f, 1.4f, 1.4f);
            PlayFx.Instance?.Glitter(transform.position + transform.forward * 0.04f, new Color(0.7f, 0.95f, 0.9f), 10, 0.9f);
        }

        public void Parry()
        {
            _sqV -= 12f; _flash = 1; _flashC = new Color(1.5f, 1.25f, 0.6f);
            PlayFx.Instance?.Flash(transform.position + transform.forward * 0.05f, new Color(1f, 0.85f, 0.35f), 0.5f, 0.35f);
            PlayFx.Instance?.Glitter(transform.position + transform.forward * 0.05f, new Color(1f, 0.85f, 0.35f), 20, 1.4f);
        }

        public void Break()
        {
            var fx = PlayFx.Instance; if (!fx) return;
            var p = transform.position;
            // 护边、星星、握带各复制一份飞出去；盘面碎成彩纸
            foreach (Transform part in _model)
            {
                if (part.name.Contains("Disc")) continue;
                var cp = Instantiate(part.gameObject, part.position, part.rotation);
                cp.transform.localScale = part.lossyScale;
                fx.Throw(cp.transform, (part.position - p + transform.forward * 0.05f).normalized * 1.2f + Vector3.up * 1.5f + Random.insideUnitSphere * 0.5f, Random.insideUnitSphere * 720f, 1.2f);
            }
            fx.Confetti(p, 30); fx.Glitter(p, new Color(0.65f, 0.9f, 1f), 16, 1.5f);
            _show = 0; transform.localScale = Vector3.zero;
        }
    }
}
