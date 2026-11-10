// 终极决战打完后的谢幕（用户：终极之战打完之后结束得有些草率，结尾要做得有意思一些）。
// 规则层在终章通关后多留 finaleHold 秒再进结算，这里按时间线演：
//   0.0 s  全场静一下（慢动作），玩具化的天空从房间里一圈圈退回门里
//   1.2 s  六个 Boss 从四扇门里一个接一个走出来，在玩家面前排成半圆（已经被打服了：举白旗、垂着头）
//   4.5 s  一起朝玩家鞠躬，头顶冒“对不起”的小彩纸；每鞠一个躬放一束礼花
//   7.5 s  所有门一起喷彩纸烟花，天空金光一闪，横幅“梦境守护者！”
//   10 s   Boss 们转身挥手、蹦回门里，门里传来欢呼
//   13 s   进结算（结算页标题是“梦境守卫成功”，并解锁无尽模式）
// 只用整体位移 + 绕竖直轴 / 横轴的小角度转动（不碰零件轴向），动作慢、不晃（舒适）。
using System.Collections.Generic;
using Bang.Logic;
using UnityEngine;
using Bang.Play.UI;

namespace Bang.Play
{
    public class FinaleShow : MonoBehaviour
    {
        GameDriver _d;
        readonly List<(Transform t, Vector3 from, Vector3 to, float yaw, float delay)> _cast = new();
        float _t = -1f;
        readonly List<Vector3> _scale0 = new(); readonly List<int> _slot = new();
        float _tBow = 4.5f, _tBurst = 7.5f, _tBack = 10.2f; int _maxSlot;
        bool _bowFx, _burstFx, _byeFx;

        public static FinaleShow Begin(GameDriver d)
        {
            var g = new GameObject("FinaleShow"); var f = g.AddComponent<FinaleShow>(); f._d = d; f.Build(); return f;
        }

        void Build()
        {
            _t = 0f;
            var cam = Camera.main ? Camera.main.transform : null;
            // 队形朝向：玩家 → 所有门的平均方向（Boss 们排在玩家和门之间，不受玩家此刻朝哪看影响）
            var sum = Vector3.zero; foreach (var pp in _d.Game.portals) sum += LogicSpace.ToUnity(pp.center);
            var fwd = Vector3.ProjectOnPlane(sum / Mathf.Max(1, _d.Game.portals.Count) - (cam ? cam.position : Vector3.zero), Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.back;
            var head = cam ? cam.position : new Vector3(0, 1.6f, 0);
            var portals = _d.Game.portals;
            var prefabs = _d.bossPrefabs;
            if (prefabs == null) return;
            int n = Mathf.Min(6, prefabs.Length);
            // 用户：最后的 Boss 要分别从离它们最近的门出来，不要重叠穿模在一起。
            // 先排好半圆站位（离玩家 2.4 米、±70°，相邻约 1.2 米，Boss 缩到 0.8 倍后不会挨着），再给每个站位挑最近的门；
            // 同一扇门出来的几个按顺序错开 1.5 秒（前一个走出 1.3 米以后后一个才冒头），回去时倒着来
            var slotOfDoor = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                if (!prefabs[i]) continue;
                float a = Mathf.Lerp(-78f, 78f, n > 1 ? i / (float)(n - 1) : 0.5f);   // 半圆 ±78°、半径 2.7 米：相邻约 1.47 米，Boss（缩到 0.8 倍后半宽 ≤ 0.52 米）之间留出空隙
                var dir = Quaternion.Euler(0, a, 0) * fwd;
                var to = new Vector3(head.x, 0, head.z) + dir * 2.7f;
                Bang.Logic.Portal p = portals[0]; float best = float.MaxValue;
                foreach (var pp in portals) { var m = LogicSpace.ToUnity(pp.mouth); m.y = 0; float d = (m - to).sqrMagnitude; if (d < best) { best = d; p = pp; } }
                var mouth = LogicSpace.ToUnity(p.mouth); mouth.y = 0;
                int slot = slotOfDoor.TryGetValue(p.id, out var k0) ? k0 : 0; slotOfDoor[p.id] = slot + 1;
                float delay = 1.2f + slot * 1.5f + p.id * 0.25f;
                var go = Instantiate(prefabs[i], transform);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var m in go.GetComponentsInChildren<MonoBehaviour>()) if (!(m is Motion.EnemyMotion)) m.enabled = false;
                EnemyView.AddBossSilhouette(go);                                          // 和打仗时一样的金色外轮廓描边
                go.transform.position = mouth; go.transform.localScale *= 0.8f;          // 小一号：六个排开不挤
                _scale0.Add(go.transform.localScale); go.transform.localScale = Vector3.zero;   // 轮到它才从门口冒出来（之前不显示，不和同门的叠在一起）
                var look = new Vector3(head.x, 0, head.z) - to; float yaw = Quaternion.LookRotation(look).eulerAngles.y;
                go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                _cast.Add((go.transform, mouth, to, yaw, delay));
                _slot.Add(slot);
            }
            float lastIn = 0; foreach (var c in _cast) lastIn = Mathf.Max(lastIn, c.delay + 2f);
            foreach (var k in _slot) _maxSlot = Mathf.Max(_maxSlot, k);
            _tBow = Mathf.Max(4.5f, lastIn + 0.4f); _tBurst = _tBow + 3f; _tBack = _tBurst + 2.7f;
            _d.finaleLength = _tBack + _maxSlot * 0.9f + 2.2f;                  // 规则层的谢幕时长跟着走（同门错开后会长一点）
            ToyTime.Slow(0.8f, 0.3f);
            _d.ui?.Banner(Loc.T("全部打倒了！", "All Bosses Down!"), Loc.T("等等……它们好像有话要说", "Wait... they have something to say"), 3f, true);
            BangAudio.Ui("boss_gong", 0.8f, 1.2f);
        }

        void Update()
        {
            if (_t < 0) return;
            float dt = Time.deltaTime; _t += dt;
            var fx = PlayFx.Instance; var cam = Camera.main ? Camera.main.transform : null;
            for (int i = 0; i < _cast.Count; i++)
            {
                var (t, from, to, yaw, delay) = _cast[i]; if (!t) continue;
                float pop = Mathf.Clamp01((_t - delay) / 0.3f);                    // 门口“啵”地冒出来
                float walk = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_t - delay) / 2.0f));
                float back = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_t - _tBack - (_maxSlot - _slot[i]) * 0.9f) / 1.6f));   // 回门：同一扇门后出来的先回，错开
                float shrink = Mathf.Clamp01((back - 0.85f) / 0.15f);               // 到门口缩回去，不叠在门口
                t.localScale = _scale0[i] * (pop < 1 ? UiKit.Back(pop) : 1f) * (1f - shrink);
                var pos = Vector3.Lerp(Vector3.Lerp(from, to, walk), from, back);
                float hop = walk > 0 && walk < 1 ? Mathf.Abs(Mathf.Sin((_t - delay) * 7f)) * 0.05f : back > 0 && back < 1 ? Mathf.Abs(Mathf.Sin(_t * 9f)) * 0.08f : 0;
                t.position = pos + Vector3.up * hop;
                // 鞠躬（4.5～6 秒，逐个错开）
                float u = (_t - _tBow - i * 0.12f) / 1.4f, bow = u > 0 && u < 1 ? Mathf.Sin(u * Mathf.PI) * 28f : 0;
                // 挥手转身（10 秒起往回走时转向门）
                float turn = back > 0 ? Mathf.LerpAngle(yaw, Quaternion.LookRotation(from - to).eulerAngles.y, Mathf.Clamp01(back * 3f)) : yaw;
                float sway = _t > _tBurst && _t < _tBack ? Mathf.Sin(_t * 3f + i) * 6f : 0;   // 欢呼时左右摇摆
                t.rotation = Quaternion.Euler(bow, turn + sway, 0);
                if (back >= 1f || pop <= 0f) { t.gameObject.SetActive(back < 1f && pop > 0f); continue; }
                t.gameObject.SetActive(true);
            }
            if (!_bowFx && _t >= _tBow + 0.1f)
            {
                _bowFx = true;
                foreach (var c in _cast) if (c.t) fx?.Confetti(c.t.position + Vector3.up * 1.0f, 18);
                _d.ui?.Banner(Loc.T("玩具之王们：“我们认输啦！”", "Toy Kings: \"We give up!\""), Loc.T("“我们只是太想有人陪我们玩了……”", "\"We just wanted someone to play with...\""), 3f, true);
                BangAudio.Ui("chapter_clear", 0.7f, 1.1f);
            }
            if (!_burstFx && _t >= _tBurst)
            {
                _burstFx = true;
                foreach (var p in _d.Game.portals)
                {
                    var c = LogicSpace.ToUnity(p.center);
                    for (int k = 0; k < 3; k++) fx?.Confetti(c + Vector3.up * (0.2f + k * 0.4f), 50);
                    fx?.Flash(c, new Color(1f, 0.88f, 0.5f), 2.6f, 0.9f);
                }
                if (cam) { var f = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 1.6f + Vector3.up * 0.7f; fx?.Confetti(f + cam.right * 0.6f, 60); fx?.Confetti(f - cam.right * 0.6f, 60); }
                _d.ui?.Banner(Loc.T("你是梦境守护者！", "You are the Dream Guardian!"), Loc.T("房间守住了，玩具们也玩得很开心", "The room is safe, and the toys had their fun"), 3.2f, true);
                BangAudio.Ui("wave_clear", 1f, 1f); _d.music?.Sting(true);
            }
            if (!_byeFx && _t >= _tBack)
            {
                _byeFx = true;
                foreach (var c in _cast) if (c.t) BangAudio.At("voice_jump", c.t.position, 0.6f, 1.2f);
                _d.ui?.Banner(Loc.T("“晚安！下次再一起玩！”", "\"Good night! Let's play again!\""), Loc.T("玩具们回到梦里，门一扇扇合上", "The toys head home and the doors close"), 3f, true);
            }
            if (_t > _tBack + _maxSlot * 0.9f + 2.2f) Destroy(gameObject);
        }
    }
}
