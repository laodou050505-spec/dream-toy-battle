// 头显输入：PICO 手部追踪（每只手取 10 个关节喂给规则层的手势识别）+ 眼动（合成眼位姿，无效 / 闭眼时退回头部朝向）。
// 关节和眼位姿都在追踪空间，用 XR Origin 转到世界坐标，再转成规则坐标（x 取反）。
// 臂长：首次进入时双臂前伸 1.5 秒校准（ArmCalibrator），结果存进存档；没校准前用默认臂长。
using System.Collections.Generic;
using Bang.Logic;
using ByteDance.PICO.XR;
using UnityEngine;

namespace Bang.Play.Input
{
    public class PicoInput : PlayerInputSource
    {
        public Camera cam;
        [Tooltip("追踪空间原点（XR Origin）")] public Transform trackingOrigin;
        [Tooltip("眼动视线平滑（秒）：眼跳很快，稍微平滑一点准星不抖，又不至于拖")] public float gazeSmoothing = 0.035f;

        public double armLen = -1;                    // < 0 用默认臂长
        public ArmCalibrator calibrator;              // 非空时正在校准
        public bool HandsTracked { get; private set; }
        public bool EyeTracked { get; private set; }

        readonly HandGesture _left = new("left"), _right = new("right");
        readonly Dictionary<string, HandOut> _out = new();
        readonly Dictionary<string, Joints> _joints = new();
        readonly Dictionary<string, (Vector3 grip, Vector3 fwd, Vector3 up)> _pose = new();
        HandJointLocations _locs = new() { jointLocations = new HandJointLocation[(int)HandJoint.JointMax] };
        EyeTrackingData _eye = new() { eyeDatas = new PerEyeData[(int)PerEyeUsage.EyeCount] };
        bool _eyeStarted, _eyeTried;
        Ray _gaze; Vector3 _gazeDir;

        public override Ray GazeWorld => _gaze;

        void Start()
        {
            if (!cam) cam = Camera.main;
            if (!trackingOrigin && cam) trackingOrigin = cam.transform.parent;
        }

        void OnDisable()
        {
            if (_eyeStarted) { var stop = new EyeTrackingStopInfo(); PXR_MotionTracking.StopEyeTracking(ref stop); _eyeStarted = false; _eyeTried = false; }
        }

        Vector3 ToWorld(Vector3 trackingPos) => trackingOrigin ? trackingOrigin.TransformPoint(trackingPos) : trackingPos;

        Joints ReadHand(HandType type, string side)
        {
            if (!PXR_HandTracking.GetJointLocations(type, ref _locs) || _locs.isActive == 0 || _locs.jointLocations == null) return null;
            const ulong need = (ulong)HandLocationStatus.PositionValid;
            Vector3 P(HandJoint j, out bool ok)
            {
                var l = _locs.jointLocations[(int)j]; ok = ((ulong)l.locationStatus & need) != 0;
                return ToWorld(l.pose.Position.ToVector3());
            }
            bool a, b, c, d, e, f, g, h, i, k;
            var wrist = P(HandJoint.JointWrist, out a); var thumb = P(HandJoint.JointThumbTip, out b);
            var it = P(HandJoint.JointIndexTip, out c); var ip = P(HandJoint.JointIndexProximal, out d);
            var mt = P(HandJoint.JointMiddleTip, out e); var mp = P(HandJoint.JointMiddleProximal, out f);
            var rt = P(HandJoint.JointRingTip, out g); var rp = P(HandJoint.JointRingProximal, out h);
            var pt = P(HandJoint.JointLittleTip, out i); var pp = P(HandJoint.JointLittleProximal, out k);
            if (!(a && b && c && d && e && f && g && h && i && k)) return null;
            // 枪的位姿：握把顶部在食指根和手腕之间，枪口朝食指方向，“上”= 拇指一侧
            var fwd = (it - ip).normalized; if (fwd.sqrMagnitude < 0.5f) fwd = (ip - wrist).normalized;
            var up = Vector3.ProjectOnPlane(thumb - ip, fwd).normalized; if (up.sqrMagnitude < 0.5f) up = Vector3.up;
            _pose[side] = (Vector3.Lerp(wrist, ip, 0.75f) - up * 0.012f, fwd, up);
            return new Joints
            {
                wrist = LogicSpace.ToLogic(wrist), thumbTip = LogicSpace.ToLogic(thumb),
                indexTip = LogicSpace.ToLogic(it), indexProx = LogicSpace.ToLogic(ip),
                middleTip = LogicSpace.ToLogic(mt), middleProx = LogicSpace.ToLogic(mp),
                ringTip = LogicSpace.ToLogic(rt), ringProx = LogicSpace.ToLogic(rp),
                pinkyTip = LogicSpace.ToLogic(pt), pinkyProx = LogicSpace.ToLogic(pp),
            };
        }

        bool ReadEye(out Ray ray)
        {
            ray = default;
            if (!_eyeTried)
            {
                _eyeTried = true;
                var start = new EyeTrackingStartInfo { needCalibration = 0, mode = EyeTrackingMode.PXR_ETM_BOTH };
                _eyeStarted = PXR_MotionTracking.StartEyeTracking(ref start) == 0;
            }
            if (!_eyeStarted) return false;
            var info = new EyeTrackingDataGetInfo { displayTime = 0, flags = EyeTrackingDataGetFlags.PXR_EYE_POSITION | EyeTrackingDataGetFlags.PXR_EYE_ORIENTATION };
            if (PXR_MotionTracking.GetEyeTrackingData(ref info, ref _eye) != 0 || _eye.eyeDatas == null) return false;
            var cmb = _eye.eyeDatas[(int)PerEyeUsage.Combined];
            if (cmb.isPoseValid == 0) return false;
            if (cmb.isOpennessValid != 0 && cmb.openness < 0.2f) return false;            // 闭眼 / 眨眼：这一帧不用眼动
            var o = new Vector3(cmb.pose.position.x, cmb.pose.position.y, -cmb.pose.position.z);
            var q = new Quaternion(cmb.pose.orientation.x, cmb.pose.orientation.y, -cmb.pose.orientation.z, -cmb.pose.orientation.w);
            var origin = ToWorld(o);
            var dir = trackingOrigin ? trackingOrigin.TransformDirection(q * Vector3.forward) : q * Vector3.forward;
            ray = new Ray(origin, dir.normalized);
            return true;
        }

        public override FrameInput Poll(float dt)
        {
            if (!cam) cam = Camera.main;
            var ct = cam.transform;
            var head = new Head { pos = LogicSpace.ToLogic(ct.position), fwd = LogicSpace.ToLogic(ct.forward) };

            bool handsActive = PXR_HandTracking.GetActiveInputDevice() == ActiveInputDevice.HandTrackingActive;
            var jl = handsActive ? ReadHand(HandType.HandLeft, "left") : null;
            var jr = handsActive ? ReadHand(HandType.HandRight, "right") : null;
            HandsTracked = jl != null || jr != null;
            if (calibrator != null)
            {
                armLen = calibrator.Update(dt, head, jl, jr);
                if (calibrator.done) calibrator = null;
            }
            _joints["left"] = jl; _joints["right"] = jr;
            _out["left"] = _left.Update(dt, jl, head, armLen);
            _out["right"] = _right.Update(dt, jr, head, armLen);

            // 视线：眼动优先，平滑一点；无效时用头部朝向
            EyeTracked = ReadEye(out var eye);
            var target = EyeTracked ? eye : new Ray(ct.position, ct.forward);
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, gazeSmoothing));
            _gazeDir = _gazeDir == Vector3.zero ? target.direction : Vector3.Slerp(_gazeDir, target.direction, k);
            _gaze = new Ray(target.origin, _gazeDir.normalized);

            return new FrameInput
            {
                head = head,
                gaze = new GazeRay { origin = LogicSpace.ToLogic(_gaze.origin), dir = LogicSpace.ToLogic(_gaze.direction) },
                hands = new Hands { left = _out["left"], right = _out["right"] },
            };
        }

        // 枪的常驻：第一次比出手指枪确认后枪就一直在——手在视野里跟着手走；手离开视野（放下、背到身后）时
        // 停在相对头部的最后位置，不消失；开火仍然要手伸出来被识别到（规则层只认被追踪的手）。
        readonly Dictionary<string, bool> _gunSeen = new();
        readonly Dictionary<string, (Vector3 p, Quaternion r)> _gunLocal = new();

        public override bool GunPose(string side, out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            if (side == "left" && !dual) return false;
            var head = cam ? cam.transform : null;
            _out.TryGetValue(side, out var o);
            bool tracked = o != null && o.tracked && _pose.ContainsKey(side) && _joints.TryGetValue(side, out var jj) && jj != null;
            if (o != null && o.state == "gun") _gunSeen[side] = true;
            if (!_gunSeen.TryGetValue(side, out var seen) || !seen) return false;
            if (tracked && (o.state == "shield" || o.state == "binoc")) return false;   // 这只手在举盾 / 用望远镜：枪先收起来
            if (tracked)
            {
                var p = _pose[side];
                var fwd = o.dir != null && o.state == "gun" ? LogicSpace.ToUnity(o.dir).normalized : p.fwd;   // 持枪时枪口和规则层的瞄准方向一致
                rot = Quaternion.LookRotation(fwd, p.up);
                pos = p.grip;
                if (head) _gunLocal[side] = (head.InverseTransformPoint(pos), Quaternion.Inverse(head.rotation) * rot);
                return true;
            }
            if (head && _gunLocal.TryGetValue(side, out var l)) { pos = head.TransformPoint(l.p); rot = head.rotation * l.r; return true; }
            return false;
        }

        /// <summary>开始臂长校准（双臂向前伸直 1.5 秒）。</summary>
        public void BeginCalibration() => calibrator = new ArmCalibrator();
    }
}
