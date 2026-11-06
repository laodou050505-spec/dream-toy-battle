// 一只手上的半透明玩具沙鹰：跟着输入源给的位姿（桌面版挂在相机右下，头显里挂在手的瞄准位姿上），
// 零件动作交给 GunMotion；旁边一圈换弹进度环（金区 = 快速换弹的松手时机），弹药数字浮在枪的上方。
using Bang.Logic;
using Bang.Play.Motion;
using TMPro;
using UnityEngine;

namespace Bang.Play
{
    public class GunView : MonoBehaviour
    {
        public string side = "right";
        GunMotion _motion;
        RingView _ring;
        TextMeshPro _ammo;
        float _goldFrom = -1f;        // 金区成功那一刻的进度（之后从这里快速走到 1）
        bool _reloading;
        Vector3 _pos; Quaternion _rot; bool _has;

        public Transform Muzzle => _rifle && _rifle.gameObject.activeSelf && _rifleMuzzle ? _rifleMuzzle : _motion && _motion.muzzle ? _motion.muzzle : transform;
        Transform _rifle, _rifleMuzzle; float _rifleShow;

        public void Init(string s, Material ringMat, TMP_FontAsset font, GameObject rifleModel = null)
        {
            side = s;
            if (rifleModel)
            {
                _rifle = Instantiate(rifleModel, transform).transform; _rifle.name = "Rifle"; _rifle.gameObject.SetActive(false);
                _rifleMuzzle = _rifle.Find("Muzzle");
            }
            _motion = GetComponent<GunMotion>();
            _ring = RingView.Create(transform, ringMat, 0.075f);
            _ring.transform.localPosition = new Vector3(side == "right" ? 0.07f : -0.07f, 0.09f, 0.02f);
            _ring.SetColor(new Color(0.55f, 0.85f, 1f, 1));
            if (font)
            {
                var g = new GameObject("Ammo"); g.transform.SetParent(transform, false);
                g.transform.localPosition = new Vector3(0, 0.075f, -0.02f);
                _ammo = g.AddComponent<TextMeshPro>();
                _ammo.font = font; _ammo.fontSize = 0.32f; _ammo.alignment = TextAlignmentOptions.Center;
                _ammo.color = new Color(1f, 0.98f, 0.9f); _ammo.outlineWidth = 0.25f; _ammo.outlineColor = new Color32(120, 80, 150, 255);
                _ammo.rectTransform.sizeDelta = new Vector2(0.2f, 0.06f);
            }
        }

        /// <summary>每帧摆位：位姿有轻微平滑（手部追踪抖动），但不拖泥带水。</summary>
        public void Pose(bool has, Vector3 pos, Quaternion rot, float dt)
        {
            if (!has) { gameObject.SetActive(false); _has = false; return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (!_has) { _pos = pos; _rot = rot; _has = true; }
            float k = 1f - Mathf.Exp(-dt * 40f);
            _pos = Vector3.Lerp(_pos, pos, k); _rot = Quaternion.Slerp(_rot, rot, k);
            transform.SetPositionAndRotation(_pos, _rot);
        }

        Renderer[] _rs; MaterialPropertyBlock _mpb; bool _gold; float _flashT, _glitT; Color _flashC;
        public void Flash(Color c, float t = 0.8f) { _flashC = c; _flashT = t; }
        static readonly int IdColor = Shader.PropertyToID("_Color");

        public void Sync(Weapon w, Game g)
        {
            if (w == null) return;
            SyncRifle(w, g);
            // 金色子弹：枪发金光（脉动），结束时恢复；拿到修理等增益时整把枪闪一下对应的颜色
            bool gold = g.goldT > 0;
            _flashT = Mathf.Max(0, _flashT - Time.deltaTime);
            if (gold || _gold || _flashT > 0)
            {
                if (_rs == null) { _rs = GetComponentsInChildren<Renderer>(); _mpb = new MaterialPropertyBlock(); }
                var c = gold ? Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.97f, 0.75f), 0.5f + 0.5f * Mathf.Sin(Time.time * 9f)) : Color.white;
                if (_flashT > 0) c = Color.Lerp(c, _flashC, Mathf.Clamp01(_flashT / 0.5f));
                foreach (var rr in _rs) { if (!rr || rr is ParticleSystemRenderer) continue; rr.GetPropertyBlock(_mpb); _mpb.SetColor(IdColor, c); rr.SetPropertyBlock(_mpb); }
                _gold = gold || _flashT > 0;
                // 金色子弹期间枪身周围不停冒金色亮片
                if (gold && Time.time - _glitT > 0.12f) { _glitT = Time.time; PlayFx.Instance?.Glitter(transform.position + transform.forward * 0.05f, new Color(1f, 0.85f, 0.35f), 2, 0.25f); }
            }
            var r = w.reload;
            if (r != null)
            {
                var rt = g.ReloadTimes();
                float total = (float)rt.total, p;
                if (r.fastT != null) { if (_goldFrom < 0) _goldFrom = Mathf.Clamp01((float)(r.t - (rt.fast - r.fastT.Value)) / total); p = Mathf.Lerp(_goldFrom, 1f, 1f - (float)(r.fastT.Value / rt.fast)); }
                else if (r.jam) p = (float)(r.t / (rt.total + rt.jam));
                else p = (float)(r.t / total);
                if (!_reloading) { _reloading = true; _ring.Snap(0); }
                _ring.Show(true);
                _ring.SetFill(p);
                _ring.SetGold(r.jam ? 0 : (float)(rt.goldStart / total), r.jam ? 0 : (float)(rt.goldEnd / total));
                _ring.SetColor(r.jam ? new Color(1f, 0.5f, 0.5f, 1) : r.fastT != null ? new Color(1f, 0.85f, 0.3f, 1) : new Color(0.55f, 0.85f, 1f, 1));
                _motion?.Reload(p, r.jam);
            }
            else if (_reloading) { _reloading = false; _goldFrom = -1f; _ring.Show(false); }
            SyncAmmo(w, g, r != null);
            SyncGoldTip(r != null, r != null && r.jam);
        }

        const float RifleScale = 1f;

        /// <summary>无限火力：手枪收起，长枪弹出来；连射时枪身后坐；结束后弹回手枪。</summary>
        void SyncRifle(Weapon w, Game g)
        {
            if (!_rifle || !_motion || !_motion.model) return;
            bool on = g.rapidT > 0;
            _rifleShow = Mathf.MoveTowards(_rifleShow, on ? 1f : 0f, Time.deltaTime * 6f);
            bool show = on || _rifleShow > 0f;
            if (_rifle.gameObject.activeSelf != show) { _rifle.gameObject.SetActive(show); _motion.model.gameObject.SetActive(!show); PlayFx.Instance?.Glitter(transform.position, new Color(1f, 0.8f, 0.5f), 10, 0.3f); }
            if (show)
            {
                _rifle.localScale = Vector3.one * (RifleScale * Mathf.Max(0.01f, UI.UiKit.Back(_rifleShow)));
                _rifle.localPosition = Vector3.back * (0.022f * (float)w.recoil);
            }
        }

        // ───────── 弹药牌：深莓色小药丸（和桃色糖果枪拉开明度），白色大数字；剩 5 发变琥珀色，打空变珊瑚红并弹出“握拳换弹”提示卡 ─────────
        /// <summary>切换语言后：弹药牌和“握拳换弹”卡下一帧按新语言（字体）重建。</summary>
        public void ResetLabels() { if (_badge) Destroy(_badge.gameObject); if (_tip) Destroy(_tip.gameObject); _badge = null; _tip = null; _lastAmmo = -1; }
        Transform _badge, _tip; MeshRenderer _badgeBg; TextMeshPro _badgeText; float _emptyT, _tipShow; int _lastAmmo = -1;
        static readonly Color BadgeInk = new(0.29f, 0.17f, 0.36f, 0.94f), BadgeLow = new(0.93f, 0.6f, 0.16f, 0.96f), BadgeEmpty = new(0.92f, 0.33f, 0.36f, 0.97f);

        void SyncAmmo(Weapon w, Game g, bool reloading)
        {
            if (_ammo) _ammo.gameObject.SetActive(false);            // 旧的浮字不用了
            if (UI.UiKit.T == null) return;
            if (!_badge)
            {
                _badge = UI.UiKit.Node(transform, "AmmoBadge", new Vector3(side == "right" ? 0.045f : -0.045f, 0.07f, -0.06f));
                _badgeBg = UI.UiKit.Panel(_badge, new Vector2(0.07f, 0.032f), BadgeInk, BadgeInk, 0.016f, 0.0015f, new Color(1, 1, 1, 0.85f), 0.25f, 0.008f, 0, 0.2f, true);
                _badgeText = UI.UiKit.Text(_badge, "", 0.026f, Color.white, 0.08f, TextAlignmentOptions.Center, 0);
                _badgeText.transform.localPosition = new Vector3(0, -0.002f, -0.002f);
                _tip = UI.UiKit.Node(transform, "EmptyTip", new Vector3(0, 0.15f, -0.02f));
                UI.UiKit.Panel(_tip, new Vector2(0.2f, 0.06f), BadgeEmpty, new Color(0.85f, 0.25f, 0.32f, 0.97f), 0.03f, 0.002f, Color.white, 0.3f, 0.012f, 0.3f, 0.3f, true);
                var gi = UI.UiKit.Image(_tip, UI.UiKit.T.Gesture("fist"), 0.05f); gi.transform.localPosition = new Vector3(-0.07f, 0, -0.003f);
                var tt = UI.UiKit.Text(_tip, Loc.T("握拳换弹", "Fist to reload"), 0.026f, Color.white, 0.14f, TextAlignmentOptions.Left, 0); tt.transform.localPosition = new Vector3(0.025f, -0.002f, -0.003f);
                _tip.localScale = Vector3.zero;
            }
            int mag = g.MagOf(w.gun);
            bool empty = w.ammo == 0 && !reloading;
            if (w.ammo == 0 && _lastAmmo > 0) { BangAudio.At("reload_jam", transform.position, 0.6f, 1.3f); }    // 打空的那一下：提示音
            _lastAmmo = w.ammo;
            _badge.gameObject.SetActive(!reloading);
            _badgeText.text = g.rapidT > 0 ? "<b>MAX</b>" : $"<b>{w.ammo}</b><size=60%><alpha=#B0>/{mag}";
            var bc = empty ? BadgeEmpty : w.ammo <= 5 ? BadgeLow : BadgeInk;
            UI.UiKit.SetPanelColors(_badgeBg, bc, bc);
            _emptyT = empty ? _emptyT + Time.deltaTime : 0;
            _badge.localScale = Vector3.one * (empty ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(_emptyT * 6f)) : 1);
            _tipShow = Mathf.MoveTowards(_tipShow, empty ? 1 : 0, Time.deltaTime * (empty ? 5f : 8f));
            float x = _tipShow - 1f, pop = empty ? 1f + 2.70158f * x * x * x + 1.70158f * x * x : _tipShow;
            _tip.localScale = Vector3.one * Mathf.Max(0, pop) * (1f + 0.04f * Mathf.Sin(_emptyT * 5f));
            var cam = Camera.main;
            if (cam)
            {
                _badge.rotation = Quaternion.LookRotation(_badge.position - cam.transform.position, cam.transform.up);
                _tip.rotation = Quaternion.LookRotation(_tip.position - cam.transform.position, cam.transform.up);
            }
        }

        public void Fire() => _motion?.Fire();
        public void Dry() => _motion?.Dry();
        public void Knocked() => _motion?.Knocked();
        public void Stolen(float dur) => _motion?.Stolen(dur);
        public void ReloadDone() { _motion?.ReloadDone(); _reloading = false; _goldFrom = -1f; _ring.Show(false); }

        // ───────── 金区换弹提示（用户：黄金时间的设定没在玩法中介绍，玩家都不知道）─────────
        // 换弹时圈旁边一张小卡：“转到金色时松拳！”；玩家成功 3 次金区换弹以后不再出现（本次启动内计数）。太早松开卡壳时卡片变成“太早啦”
        static int _goldsDone; Transform _goldTip; TextMeshPro _goldTipText; float _goldTipK, _jamT;
        public static void GoldHit() { _goldsDone++; }
        public void JamHint() { _jamT = 1.4f; }
        void SyncGoldTip(bool reloading, bool jam)
        {
            if (UI.UiKit.T == null) return;
            if (!_goldTip)
            {
                _goldTip = UI.UiKit.Node(transform, "GoldTip", new Vector3(0, 0.12f, -0.02f));
                UI.UiKit.Panel(_goldTip, new Vector2(0.21f, 0.05f), new Color(1f, 0.83f, 0.3f, 0.97f), new Color(1f, 0.72f, 0.22f, 0.97f), 0.025f, 0.002f, Color.white, 0.3f, 0.012f, 0.3f, 0.3f, true);
                _goldTipText = UI.UiKit.Text(_goldTip, "", 0.022f, new Color(0.45f, 0.25f, 0.1f), 0.2f, TextAlignmentOptions.Center, 0); _goldTipText.transform.localPosition = new Vector3(0, -0.002f, -0.003f);
                _goldTip.localScale = Vector3.zero;
            }
            _jamT -= Time.deltaTime;
            bool show = (reloading && _goldsDone < 3) || _jamT > 0;
            _goldTipText.text = _jamT > 0 ? Loc.T("松太早卡壳啦，等金色再松", "Too early! Wait for gold") : Loc.T("转到金色时松拳，换弹更快！", "Open your fist on gold: faster!");
            _goldTipK = Mathf.MoveTowards(_goldTipK, show ? 1 : 0, Time.deltaTime * 7f);
            _goldTip.localScale = Vector3.one * UI.UiKit.Back(_goldTipK) * (show ? 1f : _goldTipK);
            var cam = Camera.main; if (cam) _goldTip.rotation = Quaternion.LookRotation(_goldTip.position - cam.transform.position, cam.transform.up);
        }
    }
}
