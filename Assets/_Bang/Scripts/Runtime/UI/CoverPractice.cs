// 封面上的“玩法介绍”（用户：封面上放一个玩法介绍按钮，和下面的按钮一样，点进去之后边玩边学；少大段文字）：
//   台口名牌“玩法介绍”（和开始游戏 / 选关 / 设置同一排）点了以后，台边弹出一张小卡：一张会动的手势图 + 一句短话 + 四个进度点，按顺序带玩家做一遍——
//     ① 比出手指枪（手上出现玩具枪）
//     ② 看着封面上任意一个气球（台上吊着的、台边漂着的都行）、手腕一挑（开枪：气球“啵”地炸开，过一会儿再鼓起来）
//     ③ 握拳换子弹（枪上出现换弹圈）
//     ④ 打一下台上的玩具（被打中会“哎哟”一跳）→ 星星 + “学会啦！”，几秒后小卡收起
//   封面上开枪一直可以当点击用：打中开始按钮 = 开始游戏、打中名牌 = 选关 / 设置（不进规则层）。
// 开枪表现复用 GameDriver.CosmeticShot（动画 + “砰！” + 声音 + 蜡笔弹道），弹药 / 换弹圈复用真实武器的数据显示。
using System.Collections.Generic;
using Bang.Logic;
using TMPro;
using UnityEngine;

namespace Bang.Play.UI
{
    public class CoverPractice : MonoBehaviour
    {
        GameDriver _driver; UiTheme _theme; TitleStageFx _stage;
        int _step;                                  // 0 比枪 · 1 打气球 · 2 换弹 · 3 打玩具 · 4 完成
        bool _active; float _doneT = -1f, _signPop = -1f; Transform _guide;
        public bool Active => _active;
        float _stepT, _reloadT = -1f; string _reloadSide;
        Transform _sign, _icon; TextMeshPro _text; readonly List<MeshRenderer> _dots = new();
        Vector3 _iconScale;
        class Balloon { public Transform t; public Vector3 s0; public float back = -1f; }
        readonly List<Balloon> _balloons = new();                                  // 封面上的气球（台上一个、台边一个）：打哪个都算
        Texture2D _doneIcon;
        readonly RaycastHit[] _hits = new RaycastHit[24];
        static readonly string[] Gest = { "gun", "flick", "fist", "flick" };

        public bool Done => _step >= 4;
        public void ShowSign(bool on) { if (_sign) _sign.gameObject.SetActive(on && _active); }

        public void Init(GameDriver driver, UiTheme theme, TitleStageFx stage)
        {
            _driver = driver; _theme = theme; _stage = stage;
            // 练习靶：气球、台上演员加上碰撞体（剧场预制体里删掉了，免得挡按钮射线；这里只给射线判定用）
            var cast = stage.FindDeepPublic("Cast");
            if (cast)
                foreach (Transform a in cast)
                {
                    if (a.GetComponent<Collider>()) continue;
                    var rs = a.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    var c = a.gameObject.AddComponent<BoxCollider>();
                    c.center = a.InverseTransformPoint(b.center); var ls = a.lossyScale;
                    c.size = new Vector3(b.size.x / Mathf.Abs(ls.x), b.size.y / Mathf.Abs(ls.y), b.size.z / Mathf.Abs(ls.z)) * 1.15f;
                    if (a.name.Contains("Balloon")) _balloons.Add(new Balloon { t = a, s0 = a.localScale });
                }
            var floaters = stage.FindDeepPublic("Floaters");
            if (floaters)
                foreach (Transform a in floaters)
                    if (a.name.Contains("Balloon")) _balloons.Add(new Balloon { t = a, s0 = a.localScale });
            _doneIcon = theme.Icon("star");
            BuildSign();
            ShowEntry();
        }

        /// <summary>小牌显示成“玩法介绍”入口。</summary>
        void ShowEntry() { _active = false; _doneT = -1f; if (_sign) _sign.gameObject.SetActive(false); _stage.HighlightStart(false); }   // 平时不显示（入口是台口名牌）

        /// <summary>进入玩法介绍：小牌翻成步骤引导，从第一步开始。</summary>
        public void Begin()
        {
            if (_active) return;
            _active = true;
            _sign.gameObject.SetActive(true); _sign.localScale = Vector3.one * 0.001f; _signPop = 0f;
            SetStep(0);
            BangAudio.Ui("ui_confirm", 0.8f); PlayFx.Instance?.Confetti(_sign.position + Vector3.up * 0.1f, 14);
        }

        void BuildSign()
        {
            _sign = UiKit.Node(transform, "PracticeSign", new Vector3(-0.86f, -0.02f, -0.5f));   // 比所有漂浮物都靠近玩家（最近的手枪在 -0.42）：小卡不会被挡
            _sign.localRotation = Quaternion.Euler(0, -18f, 0);                        // 略朝向观众中线
            _sign.localScale = Vector3.one * SignScale;
            UiKit.Board(_sign, new Vector2(0.42f, 0.44f));
            _guide = UiKit.Node(_sign, "Guide", Vector3.zero);
            var ic = UiKit.Image(_guide, _theme.Gesture("gun"), 0.18f); _icon = ic.transform; _icon.localPosition = new Vector3(0, 0.08f, -0.004f); _iconScale = _icon.localScale;
            _text = UiKit.Text(_guide, "", 0.046f, UiKit.BoardInk, 0.37f, TextAlignmentOptions.Top); _text.outlineWidth = 0;
            _text.rectTransform.pivot = new Vector2(0.5f, 1f); _text.rectTransform.sizeDelta = new Vector2(0.36f, 0.12f); _text.transform.localPosition = new Vector3(0, -0.035f, -0.004f); _text.enableAutoSizing = true; _text.fontSizeMin = _text.fontSize * 0.6f; _text.fontSizeMax = _text.fontSize;
            for (int i = 0; i < 4; i++)
            {
                var d = UiKit.Panel(_guide, new Vector2(0.03f, 0.03f), UiTheme.Ink, UiTheme.Ink, 0.015f, 0, null, 0f, 0.002f);
                d.transform.localPosition = new Vector3((i - 1.5f) * 0.055f, -0.17f, -0.004f); _dots.Add(d);
            }
        }

        const float SignScale = 0.85f;

        void SetIcon(Texture2D tex, float height)
        {
            var ir = _icon.GetComponent<MeshRenderer>();
            if (tex && ir) { var b = new MaterialPropertyBlock(); ir.GetPropertyBlock(b); b.SetTexture("_BaseMap", tex); ir.SetPropertyBlock(b); _iconScale = new Vector3(height * tex.width / tex.height, height, 1f); }
        }

        void SetStep(int s)
        {
            _step = s; _stepT = 0;
            if (_step >= 2) UiManager.MarkKnowsGun();                         // 已经比出枪、开过火：之后菜单里也可以开枪点按钮
            if (_step >= 4) { _text.text = Loc.T("学会啦！", "You've got it!"); _icon.gameObject.SetActive(true); SetIcon(_doneIcon, 0.17f); _doneT = 0f; BangAudio.Ui("chapter_clear", 0.6f); }
            else
            {
                _icon.gameObject.SetActive(true);
                SetIcon(_theme.Gesture(Gest[_step]), 0.18f);
                _text.text = _step switch
                {
                    0 => Loc.T("比出手指枪", "Make a finger gun"),
                    1 => Loc.T("看着任意一个气球，手腕一挑", "Look at either balloon, flick to fire"),
                    2 => Loc.T("握拳换子弹：圈转到金色那段时松开拳头，换得更快", "Make a fist to reload. Open it when the ring hits gold to reload faster"),   // 用户：金区换弹没在玩法里介绍
                    _ => Loc.T("打一下台上的玩具", "Shoot a toy on the stage"),
                };
            }
            for (int i = 0; i < _dots.Count; i++)
                UiKit.SetPanelColors(_dots[i], i < _step ? UiTheme.Mint : i == _step ? UiKit.Coral : new Color(0.85f, 0.8f, 0.85f), i < _step ? UiTheme.MintB : i == _step ? UiKit.CoralB : new Color(0.8f, 0.75f, 0.8f));
            _stage.HighlightStart(_step >= 4);                                     // 学会了：大按钮一闪一闪，提示可以出发
            if (s > 0) { BangAudio.Ui("ui_confirm", 0.7f); PlayFx.Instance?.Confetti(_sign.position + Vector3.up * 0.1f, 10); }
        }

        /// <summary>每帧（封面状态）由 UiManager 调用：处理开枪 / 握拳。返回 true 表示手势已用掉。</summary>
        public bool Tick(FrameInput frame, Ray gaze, float dt)
        {
            _stepT += dt;
            if (_doneT >= 0) { _doneT += dt; if (_doneT > 3.5f) ShowEntry(); }
            if (_signPop >= 0) { _signPop += dt; _sign.localScale = Vector3.one * SignScale * UiKit.Back(_signPop / 0.35f); if (_signPop > 0.4f) { _sign.localScale = Vector3.one * SignScale; _signPop = -1f; } }   // 小卡弹出来
            AnimateIcon();
            foreach (var b in _balloons)
            {
                if (!b.t) continue;
                if (b.back >= 0)
                {
                    b.back -= dt;
                    if (b.back < 0) { b.t.gameObject.SetActive(true); b.t.localScale = b.s0 * 0.01f; }
                }
                if (b.t.gameObject.activeSelf && b.t.localScale.x < b.s0.x * 0.999f)
                    b.t.localScale = Vector3.MoveTowards(b.t.localScale, b.s0, b.s0.x * dt * 2.5f);   // 重新鼓起来
            }

            var g = _driver.Game;
            bool consumed = false;
            foreach (var side in new[] { "right", "left" })
            {
                var h = frame.hands?.Get(side); if (h == null) continue;
                if (_active && _step == 0 && h.tracked && h.state == "gun" && _stepT > 0.4f) SetStep(1);
                if (h.events == null) continue;
                foreach (var ev in h.events)
                {
                    if (ev is FireEvent) { if (_active || _driver.ui.GunsInMenu) { consumed = true; Fire(side, gaze); } }   // 没学会手枪之前菜单里不变枪，也不开火
                    else if (ev is StateEvent se && se.to == "fist" && _reloadT < 0 && _active) StartReload(side);
                }
            }
            // 换弹（真实武器的数据：枪上出现换弹圈、套筒动作；不进规则层）
            if (_reloadT >= 0)
            {
                var w = g.weapons.Get(_reloadSide); var rt = g.ReloadTimes();
                _reloadT += dt;
                if (w != null) { if (w.reload == null) w.reload = new ReloadState(); w.reload.t = _reloadT; }
                if (_reloadT >= rt.total)
                {
                    if (w != null) { w.reload = null; w.ammo = g.MagOf(w.gun); }
                    _driver.Gun(_reloadSide)?.ReloadDone(); BangAudio.At("reload_in", _driver.GunTransform(_reloadSide) ? _driver.GunTransform(_reloadSide).position : transform.position);
                    _reloadT = -1f;
                    if (_step == 2) SetStep(3);
                }
            }
            return consumed;
        }

        void StartReload(string side)
        {
            var w = _driver.Game.weapons.Get(side); if (w == null) return;
            _reloadT = 0; _reloadSide = side;
            var gt = _driver.GunTransform(side); BangAudio.At("reload_out", gt ? gt.position : transform.position);
            if (_step == 1 && w.ammo < _driver.Game.MagOf(w.gun)) { }      // 第二步没打中气球也可以先学换弹，不跳步
        }

        void Fire(string side, Ray gaze)
        {
            var g = _driver.Game; var w = g.weapons.Get(side);
            if (_reloadT >= 0) { _driver.Gun(side)?.Dry(); return; }
            if (_active && w != null && w.ammo <= 0) { _driver.Gun(side)?.Dry(); var gt = _driver.GunTransform(side); BangAudio.At("dry", gt ? gt.position : transform.position); return; }
            if (_active && w != null) w.ammo--;                                   // 只有玩法介绍里才数子弹（平时开枪当点击，不会打空）
            // 瞄准：沿视线找最近的可打物（大按钮、选关 / 设置积木、气球、演员）
            int n = Physics.RaycastNonAlloc(gaze, _hits, 6f);
            RaycastHit best = default; float bd = 1e9f; bool any = false;
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider; if (!c.transform.IsChildOf(transform)) continue;
                if (_hits[i].distance < bd) { bd = _hits[i].distance; best = _hits[i]; any = true; }
            }
            _driver.CosmeticShot(side, any ? best.point : (Vector3?)null);
            if (!any) return;
            var btn = best.collider.GetComponentInParent<UiButton>();
            if (btn && btn.interactable) { btn.Click(); return; }                 // 打开始按钮 = 开始游戏；打积木 = 选关 / 设置
            var actor = best.collider.transform;
            var hitBalloon = _balloons.Find(b => b.t == actor);
            if (hitBalloon != null) { PopBalloon(hitBalloon); if (_active && _step == 1) SetStep(2); return; }
            _stage.Ouch(actor);
            var toy = best.collider.GetComponentInParent<TitleToyHit>();          // 封面上所有独立物件都打得中：弹开、转一下、再回位
            if (toy)
            {
                toy.Hit(gaze.direction);
                PlayFx.Instance?.HitStar(best.point); BangAudio.At("hit_plastic", best.point, 0.8f, 1.1f);
                if (_active && _step == 3) SetStep(4);
            }
        }

        void PopBalloon(Balloon b)
        {
            var p = b.t.position + Vector3.up * 0.08f;
            PlayFx.Instance?.Confetti(p, 24); PlayFx.Instance?.Bo(p);
            BangAudio.At("kill_pop", p, 1f, 1.2f); BangAudio.At("gift_open", p, 0.5f, 1.3f);
            b.t.gameObject.SetActive(false); b.back = 2.2f;
        }

        /// <summary>验收工具用：直接跳到第 s 步。</summary>
        public void JumpTo(int s) { Begin(); SetStep(s); }
        /// <summary>验收工具用：沿 gaze 开一枪。</summary>
        public void ShootAt(Ray gaze) => Fire("right", gaze);
        /// <summary>验收工具用：当前第几步、气球各自是否还在。</summary>
        public string State() => $"step={_step} 气球=" + string.Join(",", _balloons.ConvertAll(b => $"{b.t.name}:{b.t.gameObject.activeSelf}"));

        void AnimateIcon()
        {
            if (!_icon || !_icon.gameObject.activeSelf) return;
            float t = _stepT, ang = 0, sc = 1, dy = 0;
            switch (_step < 4 ? Gest[_step] : "")
            {
                case "flick": { float c = t % 1.4f; ang = c < 0.12f ? Mathf.Lerp(0, 28f, c / 0.12f) : c < 0.6f ? Mathf.Lerp(28f, 0, (c - 0.12f) / 0.48f) : 0; break; }
                case "": { sc = 1f + 0.25f * Mathf.Exp(-t * 5f) * Mathf.Cos(t * 18f); ang = Mathf.Sin(t * 2.2f) * 6f; break; }   // 星星：弹一下，再轻轻摇
                case "fist": { float c = t % 1.2f; sc = c < 0.15f ? 1f - 0.12f * (c / 0.15f) : 1f - 0.12f * Mathf.Exp(-(c - 0.15f) * 8f); break; }
                default: dy = Mathf.Sin(t * 2.4f) * 0.006f; break;
            }
            _icon.localRotation = Quaternion.Euler(0, 0, ang);
            _icon.localPosition = new Vector3(0, 0.08f + dy, -0.004f);
            _icon.localScale = _iconScale * sc;
        }
    }
}
