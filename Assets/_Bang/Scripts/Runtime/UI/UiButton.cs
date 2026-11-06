// 立体按钮：看向它（视线 / 鼠标）时前面板朝玩家浮起 1 厘米、略放大并变亮；捏合 / 用手指枪打它 / 点击时面板压回底座（按下），
// 0.14 秒后再执行动作（让按下的动作先被看到）。
using System;
using UnityEngine;

namespace Bang.Play.UI
{
    public class UiButton : MonoBehaviour
    {
        public Action onClick;
        public Transform face;
        public MeshRenderer faceR;
        public Color colA, colB;
        public bool interactable = true;
        [Tooltip("保护期（秒）：界面自动弹出时玩家可能还在连续开枪，这段时间按钮压暗、不响应，到点亮起并响一声")] public float armT;
        [Tooltip("不可逆操作（回主菜单、重来）：第一次按只把文字换成这句，3 秒内再按才执行")] public string confirmText;
        float _confirmT; string _label0;
        public bool ghost;                       // 幽灵按钮：平时只有字，看向时才浮出淡底
        public TMPro.TextMeshPro label;
        public Color hoverTint = Color.white;
        [Tooltip("整块卡片就是按钮时：悬停 / 按下作用在这个物体上（浮起、放大）")] public Transform hoverTarget;
        [Tooltip("悬停时 hoverTarget 的位移 / 按下时的位移 / 悬停放大比例（实物按钮：圆顶往下按、积木往上蹦）")] public Vector3 hoverMove = new(0, 0, -0.02f), pressMove = new(0, 0, 0.012f);
        public float hoverScale = 0.05f;
        Vector3 _ht0, _hs0; bool _htInit;
        float _hover, _press, _fire = -1f;
        bool _hovered;

        public bool Hovered => _hovered;

        MaterialPropertyBlock _mpb;
        public void SetHover(bool h)
        {
            if (h && !_hovered && interactable) BangAudio.Ui("ui_hover", 0.5f);
            _hovered = h && interactable && armT <= 0;
        }

        public void Click()
        {
            if (!interactable || armT > 0 || _fire >= 0) return;
            if (!string.IsNullOrEmpty(confirmText) && label && _confirmT <= 0)
            {
                _label0 = label.text; label.text = confirmText; _confirmT = 3f; _press = 1f;
                BangAudio.Ui("ui_hover", 0.9f);
                return;
            }
            _press = 1f; _fire = 0.14f;
            BangAudio.Ui("ui_select");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (armT > 0) { armT -= dt; if (armT <= 0) { armT = 0; BangAudio.Ui("ui_hover", 0.8f, 1.3f); } }
            if (_confirmT > 0) { _confirmT -= dt; if (_confirmT <= 0 && label) label.text = _label0; }
            _hover = Mathf.MoveTowards(_hover, _hovered ? 1 : 0, dt * 8f);
            _press = Mathf.MoveTowards(_press, 0, dt * 4f);
            float h = Mathf.SmoothStep(0, 1, _hover);
            if (hoverTarget)
            {
                if (!_htInit) { _ht0 = hoverTarget.localPosition; _htInit = true; }
                hoverTarget.localPosition = _ht0 + hoverMove * h + pressMove * Mathf.Sin(_press * Mathf.PI * 0.5f);
                if (_hs0 == Vector3.zero) _hs0 = hoverTarget.localScale;
                hoverTarget.localScale = _hs0 * (1f + hoverScale * h);
            }
            else if (face)
            {
                face.localPosition = new Vector3(0, 0, -0.012f - 0.01f * h + 0.011f * Mathf.Sin(_press * Mathf.PI * 0.5f));
                face.localScale = Vector3.one * (1f + 0.05f * h);
            }
            if (faceR && faceR.sharedMaterial && faceR.sharedMaterial.shader.name == "Bang/UIToon")
            {
                // 立体卡通按键：看着时整体提亮一点，不可用时压暗（颜色本身烘在顶点色里）
                var k = interactable && armT <= 0 ? 1f + 0.16f * h : 0.55f;
                _mpb ??= new MaterialPropertyBlock(); faceR.GetPropertyBlock(_mpb); _mpb.SetColor("_BaseColor", new Color(k, k, k, 1)); faceR.SetPropertyBlock(_mpb);
            }
            else if (faceR)
            {
                var a = Color.Lerp(colA, hoverTint, 0.6f * h); var b = Color.Lerp(colB, hoverTint, 0.35f * h);
                if (!interactable || armT > 0) { a *= 0.6f; b *= 0.6f; }
                UiKit.SetPanelColors(faceR, a, b);
                if (ghost) UiKit.SetAlpha(faceR, 0.85f * h);
            }
            if (_fire >= 0) { _fire -= dt; if (_fire < 0) onClick?.Invoke(); }
        }
    }
}
