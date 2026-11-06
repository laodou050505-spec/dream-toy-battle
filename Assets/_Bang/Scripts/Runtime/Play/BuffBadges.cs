// 增益徽章（nano banana 美术 Icon_buff_*）：浮在枪上方一排，玩家低头看枪就能看到自己身上有什么增益。
//   持续型（金色子弹、冰冻）：徽章外一圈倒计时环；最后 2 秒闪烁并伴随“滴答”声，结束时缩没；
//   瞬时型（修理、拔旗）：弹出来停 1.6 秒作为确认，再缩没。
//   新增益出现时：先由飞星从礼盒飞进枪里（MechPresenter），星星到了徽章才弹出来——因果一目了然。
using System.Collections.Generic;
using Bang.Logic;
using UnityEngine;

namespace Bang.Play
{
    public class BuffBadges : MonoBehaviour
    {
        public Material iconMat, ringMat;
        public Texture2D gold, freeze, repair, unflag, rapid;
        public float size = 0.045f, gap = 0.06f, lift = 0.17f;

        class Badge { public string key; public Transform t; public MeshRenderer r; public RingView ring; public float show, life = -1, pending, lastTick; public bool on; public float remain, max; public TMPro.TextMeshPro label; public bool paused; }
        readonly List<Badge> _badges = new();
        MaterialPropertyBlock _mpb;
        Vector3 _anchor; bool _hasAnchor;

        void Awake() { _mpb = new MaterialPropertyBlock(); }

        public Texture2D Icon(string key) => key switch { "gold" => gold, "freeze" => freeze, "repair" => repair, "rapid" => rapid, _ => unflag };
        static string Name(string key) => key switch { "gold" => Loc.T("金色子弹", "Golden"), "freeze" => Loc.T("冰冻", "Freeze"), "repair" => Loc.T("修理", "Repair"), "rapid" => Loc.T("无限火力", "Unlimited"), _ => Loc.T("拔旗", "Unflag") };
        /// <summary>切换语言：徽章下的名称下次按新语言重建。</summary>
        public void ResetLabels() { foreach (var b in _badges) if (b.label) { Destroy(b.label.gameObject); b.label = null; } }

        Badge Get(string key)
        {
            foreach (var b in _badges) if (b.key == key) return b;
            var tex = Icon(key);
            var g = new GameObject("Buff_" + key); g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = iconMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var nb = new Badge { key = key, t = g.transform, r = r };
            _mpb.Clear(); if (tex) _mpb.SetTexture("_BaseMap", tex); r.SetPropertyBlock(_mpb);
            if (ringMat)
            {
                nb.ring = RingView.Create(g.transform, ringMat, 1.18f);
                nb.ring.SetColor(key == "gold" ? new Color(1f, 0.82f, 0.3f, 1) : new Color(0.55f, 0.85f, 1f, 1));
            }
            g.transform.localScale = Vector3.zero;
            MakeLabel(nb);
            _badges.Add(nb);
            return nb;
        }

        /// <summary>瞬时增益（修理 / 拔旗）：delay 秒后弹出（等飞星到达）。</summary>
        public void Flash(string key, float delay)
        {
            var b = Get(key); b.pending = delay; b.life = 1.6f + delay; b.on = true; b.remain = 0;
        }

        /// <summary>持续增益的倒计时（每帧）：remain ≤ 0 表示没有。delay：刚获得时等飞星到达再弹出。</summary>
        public void Timed(string key, float remain, float max, float delay = 0)
        {
            if (remain <= 0) { foreach (var b0 in _badges) if (b0.key == key && b0.life < 0) b0.on = false; return; }
            var b = Get(key);
            if (!b.on) { b.pending = delay; b.on = true; }
            b.life = -1; b.remain = remain; b.max = max;
        }

        // 徽章下面一行字：名称 + 剩余秒数（用户：收获的 buff 是什么要标清楚）。字挂在徽章下，跟着一起缩放
        void MakeLabel(Badge b)
        {
            if (UI.UiKit.T == null) return;
            b.label = UI.UiKit.Text(b.t, "", 0.28f, Color.white, 3.2f, TMPro.TextAlignmentOptions.Center, 0.25f);
            b.label.transform.localPosition = new Vector3(0, -0.78f, -0.01f); b.label.transform.localRotation = Quaternion.identity;
        }
        /// <summary>金色子弹在场上没有敌人时计时暂停：徽章上标“暂停”，不让玩家以为卡住了。</summary>
        public void SetPaused(string key, bool p) { foreach (var b in _badges) if (b.key == key) b.paused = p; }

        public void Tick(Transform gun, float dt)
        {
            var cam = Camera.main;
            if (gun && gun.gameObject.activeInHierarchy)
            {
                var p = gun.position + Vector3.up * lift;
                _anchor = _hasAnchor ? Vector3.Lerp(_anchor, p, 1 - Mathf.Exp(-dt * 12f)) : p; _hasAnchor = true;
            }
            else if (cam) { var p = cam.transform.TransformPoint(new Vector3(0.12f, -0.12f, 0.45f)); _anchor = _hasAnchor ? Vector3.Lerp(_anchor, p, 1 - Mathf.Exp(-dt * 12f)) : p; _hasAnchor = true; }

            int visible = 0; foreach (var b in _badges) if (b.show > 0.01f || (b.on && b.pending <= 0)) visible++;
            int idx = 0;
            var right = cam ? cam.transform.right : Vector3.right;
            for (int i = _badges.Count - 1; i >= 0; i--)
            {
                var b = _badges[i];
                if (b.pending > 0) { b.pending -= dt; continue; }
                if (b.life >= 0) { b.life -= dt; if (b.life <= 0) b.on = false; }
                bool was = b.show > 0;
                b.show = Mathf.MoveTowards(b.show, b.on ? 1 : 0, dt * (b.on ? 5f : 4f));
                if (!was && b.show > 0) { PlayFx.Instance?.Glitter(_anchor, b.key == "gold" ? new Color(1f, 0.85f, 0.35f) : b.key == "freeze" ? new Color(0.7f, 0.9f, 1f) : new Color(0.6f, 1f, 0.8f), 8, 0.6f); }
                if (b.show <= 0) { b.t.localScale = Vector3.zero; if (b.ring) b.ring.Show(false); continue; }
                float x = b.show - 1f, pop = b.on ? 1f + 2.70158f * x * x * x + 1.70158f * x * x : b.show;
                bool ending = b.life < 0 && b.remain < 2f;
                float blink = ending ? 0.8f + 0.2f * Mathf.Sign(Mathf.Sin(Time.time * 18f)) : 1f;
                if (ending && Time.time - b.lastTick > 0.5f) { b.lastTick = Time.time; BangAudio.Ui("danger_tick", 0.45f, 1.5f); }
                float off = (idx - (visible - 1) * 0.5f) * gap; idx++;
                b.t.position = _anchor + right * off + Vector3.up * Mathf.Sin(Time.time * 2.6f + i) * 0.006f;
                if (cam) b.t.rotation = Quaternion.LookRotation(b.t.position - cam.transform.position, cam.transform.up);
                b.t.localScale = Vector3.one * size * Mathf.Max(0, pop) * blink;
                if (!b.label) MakeLabel(b);
                if (b.label) b.label.text = b.life >= 0 ? Name(b.key) : b.paused ? Name(b.key) + Loc.T(" · 暂停", " · paused") : $"{Name(b.key)} {Mathf.CeilToInt(b.remain)}s";
                if (b.ring)
                {
                    b.ring.Show(b.life < 0 && b.on);
                    if (b.max > 0) b.ring.SetFill(b.remain / b.max);
                    b.ring.transform.localPosition = new Vector3(0, 0, 0.002f);
                }
            }
        }
    }
}
