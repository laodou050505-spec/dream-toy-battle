// 编辑器试玩：鼠标 = 视线；按键模拟手势（跳过手势识别，直接合成手势输出，格式与 HandGesture 一致）。
//   鼠标左键 = 右手挑腕开火     R 按住 / 松开 = 握拳换弹（松开时机决定金区快速换弹）
//   Q 按住 = 左手举盾（快到时才按 = 弹反）   E 按住 = 左手望远镜
//   F = 左手开火（双枪）   右键拖动 / 方向键 = 转头
using System.Collections.Generic;
using Bang.Logic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bang.Play.Input
{
    public class DesktopInput : PlayerInputSource
    {
        public Camera cam;
        public float eyeHeight = 1.6f;
        [Tooltip("枪口在相机坐标里的位置（右手；左手 x 取反）")] public Vector3 muzzleLocal = new Vector3(0.2f, -0.21f, 0.62f);
        [Tooltip("瞄准距离：枪口指向视线上这么远的点")] public float aimDistance = 4f;

        [Header("调试")]
        [Tooltip("调试：一直举着盾（截图验收用）")] public bool holdShield;
        [Tooltip("自动驾驶：自动瞄准最近的小怪 / 旗子开火、按金区时机换弹（截图验收、压测用）")] public bool autoPilot;
        public float autoFireInterval = 0.45f;

        float _yaw = 180f, _pitch = 7f;
        float _apFire, _apHold = -1f, _apScan; Transform _apTarget;
        readonly Dictionary<string, string> _state = new() { ["left"] = "idle", ["right"] = "gun" };
        readonly Dictionary<string, List<HandEvent>> _events = new() { ["left"] = new(), ["right"] = new() };
        Ray _gaze;

        void Start()
        {
            if (!cam) cam = Camera.main;
            if (cam) { var e = cam.transform.eulerAngles; _yaw = e.y; _pitch = e.x > 180 ? e.x - 360 : e.x; }
        }

        public override Ray GazeWorld => _gaze;

        Vector3 Muzzle(string side)
        {
            var m = muzzleLocal; if (side == "left") m.x = -m.x;
            return cam.transform.TransformPoint(m);
        }

        Vector3 AimDir(string side)
        {
            var target = _gaze.GetPoint(aimDistance);
            return side == "right" || dual ? (target - Muzzle(side)).normalized : cam.transform.forward;
        }

        void Fire(string side)
        {
            if (_state[side] != "gun") return;
            _events[side].Add(new FireEvent { side = side, origin = LogicSpace.ToLogic(Muzzle(side)), dir = LogicSpace.ToLogic(AimDir(side)) });
        }

        void SetState(string side, string s)
        {
            if (_state[side] == s) return;
            _events[side].Add(new StateEvent { side = side, from = _state[side], to = s });
            _state[side] = s;
        }

        public override FrameInput Poll(float dt)
        {
            if (!cam) cam = Camera.main;
            var kb = Keyboard.current; var mouse = Mouse.current;
            // 转头：右键拖动或方向键
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                _yaw += d.x * 0.15f; _pitch = Mathf.Clamp(_pitch - d.y * 0.15f, -70f, 70f);
            }
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) _yaw -= dt * 85f;
                if (kb.rightArrowKey.isPressed) _yaw += dt * 85f;
                if (kb.upArrowKey.isPressed) _pitch = Mathf.Max(-70f, _pitch - dt * 60f);
                if (kb.downArrowKey.isPressed) _pitch = Mathf.Min(70f, _pitch + dt * 60f);
            }
            var ct = cam.transform;
            ct.SetPositionAndRotation(new Vector3(ct.position.x, eyeHeight, ct.position.z), Quaternion.Euler(_pitch, _yaw, 0));
            var mp = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
            _gaze = cam.ScreenPointToRay(mp);
            bool apReload = false, apFire = false;
            if (autoPilot) AutoPilot(dt, ref apReload, ref apFire);

            // 手势
            bool reload = kb != null && kb.rKey.isPressed || apReload;
            SetState("right", reload ? "fist" : "gun");
            string left = dual ? "gun" : "idle";
            if (kb != null && kb.qKey.isPressed || holdShield) left = "shield";
            if (kb != null && kb.eKey.isPressed) left = "binoc";
            SetState("left", left);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame || apFire) Fire("right");
            if (kb != null && kb.fKey.wasPressedThisFrame) Fire("left");

            var fwd = ct.forward;
            var palm = ct.TransformPoint(new Vector3(-0.16f, -0.08f, 0.5f));   // 桌面版的盾举在左前方（头显里由真实的手决定）
            HandOut Make(string side)
            {
                var o = new HandOut
                {
                    side = side, tracked = true, state = _state[side], gunPose = _state[side] == "gun", straight = true, reach = 0.6,
                    origin = LogicSpace.ToLogic(Muzzle(side)), dir = LogicSpace.ToLogic(AimDir(side)),
                    palm = LogicSpace.ToLogic(palm), palmN = LogicSpace.ToLogic(fwd),
                    events = new List<HandEvent>(_events[side]),
                };
                _events[side].Clear();
                return o;
            }
            return new FrameInput
            {
                head = new Head { pos = LogicSpace.ToLogic(ct.position), fwd = LogicSpace.ToLogic(fwd) },
                gaze = new GazeRay { origin = LogicSpace.ToLogic(_gaze.origin), dir = LogicSpace.ToLogic(_gaze.direction) },
                hands = new Hands { right = Make("right"), left = Make("left") },
            };
        }

        void AutoPilot(float dt, ref bool reload, ref bool fire)
        {
            var gd = FindAnyObjectByType<GameDriver>();
            var g = gd ? gd.Game : null; if (g == null) return;
            _apScan -= dt;
            if (_apScan <= 0 || !_apTarget)
            {
                _apScan = 0.3f; _apTarget = null; float best = 1e9f;
                foreach (var e in FindObjectsByType<EnemyView>()) { float d = Vector3.Distance(e.transform.position, cam.transform.position); if (d < best) { best = d; _apTarget = e.transform; } }
                if (!_apTarget) foreach (var f in FindObjectsByType<FlagView>()) { if (f.data.dying) continue; float d = Vector3.Distance(f.transform.position, cam.transform.position); if (d < best) { best = d; _apTarget = f.transform; } }
            }
            var ct = cam.transform;
            if (!_apTarget) _gaze = new Ray(ct.position, ct.forward);          // 没目标时看正前方
            if (_apTarget)
            {
                var aim = _apTarget.position + Vector3.up * 0.2f;
                var d = (aim - ct.position).normalized;
                _gaze = new Ray(ct.position, d);
                // 头也慢慢转过去（截图 / 录屏看得到）
                var look = Quaternion.LookRotation(d).eulerAngles;
                _yaw = Mathf.LerpAngle(_yaw, look.y, 1f - Mathf.Exp(-dt * 3f));
                float lp = look.x > 180 ? look.x - 360 : look.x;
                _pitch = Mathf.Lerp(_pitch, lp, 1f - Mathf.Exp(-dt * 3f));
            }
            var w = g.weapons.right; if (w == null) return;
            var rt = g.ReloadTimes();
            if (_apHold >= 0)
            {
                _apHold += dt; reload = _apHold < (rt.goldStart + rt.goldEnd) * 0.5;   // 在金区中间松手
                if (!reload) _apHold = -1f;
                return;
            }
            if (w.ammo <= 0 && w.reload == null) { _apHold = 0; reload = true; return; }
            _apFire -= dt;
            if (_apTarget && _apFire <= 0 && w.reload == null) { _apFire = autoFireInterval; fire = true; }
        }

        public override bool GunPose(string side, out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            if (!cam || (side == "left" && !dual)) return false;
            var o = Muzzle(side);
            rot = Quaternion.LookRotation(AimDir(side), cam.transform.up);
            pos = o - rot * GunMuzzleOffset;
            return true;
        }

        /// <summary>枪口相对枪原点（握把顶部）的位置，来自 Gun_parts.json。</summary>
        public static Vector3 GunMuzzleOffset = new Vector3(0, 0.033f, 0.142f);
    }
}
