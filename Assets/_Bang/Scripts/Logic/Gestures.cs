// 单手手势识别（纯逻辑）。每帧喂入关节位置和头部姿态，输出手势状态与事件。
//
// 状态：idle / gun（持枪，必须手臂伸直）/ fist（换弹）/ binoc（望远镜）/ shield（举盾）
// 优先级：binoc > fist > shield > gun > idle；候选状态保持 holdSec 才切换（防抖）。
// 事件：FireEvent / StateEvent / PinchStartEvent
//
// joints 字段：wrist, thumbTip, indexTip, indexProx, middleTip, middleProx,
//              ringTip, ringProx, pinkyTip, pinkyProx（均为 Vec3d，世界坐标）
using System;
using System.Collections.Generic;
namespace Bang.Logic
{
    // ───────────────────────── 输入结构 ─────────────────────────
    public class Head
    {
        public Vec3d pos; // 与 gazeRay.origin 共享同一对象（引用相等）
        public Vec3d fwd;
    }

    public class Joints
    {
        public Vec3d wrist, thumbTip;
        public Vec3d indexTip, indexProx;
        public Vec3d middleTip, middleProx;
        public Vec3d ringTip, ringProx;
        public Vec3d pinkyTip, pinkyProx;
    }

    // ───────────────────────── 事件基类 ─────────────────────────
    public class HandEvent { public string type; public string side; }
    public class StateEvent : HandEvent { public string from, to; public StateEvent() { type = "state"; } }
    public class FireEvent : HandEvent { public Vec3d origin, dir; public FireEvent() { type = "fire"; } }
    public class PinchStartEvent : HandEvent { public PinchStartEvent() { type = "pinchStart"; } }

    // ───────────────────────── HandGesture 输出 ─────────────────────────
    public class HandOut
    {
        public string side;
        public bool tracked;
        public string state;
        public string raw;
        public bool gunPose;
        public bool straight;
        public double reach;
        public Vec3d origin, dir;
        public Vec3d palm, palmN;
        public bool pinching;
        public Dictionary<string, double> ratios;
        public List<HandEvent> events;
    }

    // ───────────────────────── 辅助函数 ─────────────────────────
    public static class GestureUtils
    {
        public static Vec3d ShoulderOf(Head head, string side, ArmConfig cfg = null)
        {
            if (cfg == null) cfg = Config.Default.arm;
            Vec3d f = V.norm(new Vec3d(head.fwd.x, 0, head.fwd.z));
            Vec3d right = new Vec3d(-f.z, 0, f.x);
            double s = side == "right" ? 1 : -1;
            return new Vec3d(
                head.pos.x + right.x * cfg.shoulderSide * s,
                head.pos.y - cfg.shoulderDown,
                head.pos.z + right.z * cfg.shoulderSide * s
            );
        }

        public static Vec3d PalmNormal(Joints j, string side)
        {
            Vec3d a = V.sub(j.middleProx, j.wrist);
            Vec3d b = V.sub(j.pinkyProx, j.indexProx);
            return V.norm(side == "right" ? V.cross(a, b) : V.cross(b, a));
        }

        public static Vec3d PalmCenter(Joints j)
        {
            return V.scale(V.add(V.add(j.wrist, j.indexProx), V.add(j.pinkyProx, j.middleProx)), 0.25);
        }

        public static double ExtRatio(Joints j, string f)
        {
            Vec3d tip = GetTip(j, f);
            Vec3d prox = GetProx(j, f);
            double baseD = V.dist(prox, j.wrist);
            if (baseD < 1e-3) baseD = 1e-3;
            return V.dist(tip, j.wrist) / baseD;
        }

        private static Vec3d GetTip(Joints j, string f)
        {
            switch(f) { case "index": return j.indexTip; case "middle": return j.middleTip; case "ring": return j.ringTip; default: return j.pinkyTip; }
        }
        private static Vec3d GetProx(Joints j, string f)
        {
            switch(f) { case "index": return j.indexProx; case "middle": return j.middleProx; case "ring": return j.ringProx; default: return j.pinkyProx; }
        }
    }

    // ───────────────────────── HandGesture ─────────────────────────
    public class HandGesture
    {
        private static readonly string[] FINGERS = { "index", "middle", "ring", "pinky" };

        public string side;
        public Config cfg;
        public double time;
        public string state;
        public string cand;
        public double candT;
        private Dictionary<string, bool> ext;
        private Dictionary<string, bool> curl;
        public bool straight;
        public bool pinching;
        private List<Sample> hist;
        private double lostT, rearmT = double.NegativeInfinity;
        private bool armed;
        private double lastFire;
        private double peakPitch;
        public HandOut lastOut;

        public HandGesture(string side, Config cfg = null)
        {
            this.side = side;
            this.cfg = cfg ?? Config.Default;
            time = 0;
            state = "idle";
            cand = "idle";
            candT = 0;
            ext = new Dictionary<string, bool> { {"index",false},{"middle",false},{"ring",false},{"pinky",false} };
            curl = new Dictionary<string, bool> { {"index",false},{"middle",false},{"ring",false},{"pinky",false} };
            straight = false;
            pinching = false;
            hist = new List<Sample>();
            armed = true;
            lastFire = -9;
            peakPitch = -90;
            lastOut = null;
        }

        public void Reset()
        {
            state = "idle"; cand = "idle"; candT = 0; hist.Clear(); pinching = false;
        }

        /// <summary>挑腕判定用的历史样本。</summary>
        private struct Sample
        {
            public double t, pitch; public Vec3d dir, origin, flick, thumb, wrist;
        }

        /// <summary>从样本 h 到现在抬了多少度：取世界“上”和手自身“拇指一侧”两种量法的较大值（手歪着时世界竖直分量很小，但手确实挑了）。</summary>
        private double RiseSince(in Sample h, double pitch, Vec3d flick, Vec3d wrist)
        {
            double rise = pitch - h.pitch;
            double dt = time - h.t;
            if (dt > 1e-4 && V.dist(wrist, h.wrist) / dt <= cfg.flick.maxWristSpeed)
                rise = Math.Max(rise, Math.Asin(V.clamp(V.dot(V.sub(flick, h.flick), h.thumb), -1, 1)) / V.DEG);
            return rise;
        }

        public HandOut Update(double dt, Joints j, Head head, double armLen = -1)
        {
            if (armLen < 0) armLen = cfg.arm.defaultLen;
            time += dt;
            var events = new List<HandEvent>();
            if (j == null) {
                lostT += dt;
                if (lastOut != null && lastOut.tracked && lostT < cfg.hand.lostGrace) { lastOut.events = events; return lastOut; }   // 短暂丢帧：维持原状，不收枪、不清历史
                if (state != "idle") events.Add(new StateEvent { side=this.side, from=state, to="idle" });
                Reset();
                lastOut = new HandOut { side=this.side, tracked=false, state="idle", events=events };
                return lastOut;
            }
            lostT = 0;
            var H = cfg.hand;
            var ratios = new Dictionary<string, double>();
            foreach (var f in FINGERS) {
                double r = GestureUtils.ExtRatio(j, f);
                ratios[f] = r;
                if (!ext[f] && r > H.extOn) ext[f] = true;
                else if (ext[f] && r < H.extOff) ext[f] = false;
                if (!curl[f] && r < H.curlOn) curl[f] = true;
                else if (curl[f] && r > H.curlOff) curl[f] = false;
            }

            // 手臂伸直
            Vec3d sh = GestureUtils.ShoulderOf(head, side, cfg.arm);
            double reach = V.dist(j.wrist, sh) / armLen;
            if (!straight && reach >= cfg.arm.straightRatio) straight = true;
            else if (straight && reach < cfg.arm.straightOff) straight = false;

            Vec3d palm = GestureUtils.PalmCenter(j);
            Vec3d palmN = GestureUtils.PalmNormal(j, side);
            Vec3d toPalm = V.sub(palm, head.pos);
            Vec3d fwdNorm = V.norm(head.fwd);
            double fwdDist = V.dot(toPalm, fwdNorm);
            double eyeD = V.dist(palm, head.pos);
            bool nearEye = eyeD < cfg.binoc.eyeDist && fwdDist > 0.0;

            bool strictGun = ext["index"] && curl["ring"] && curl["pinky"];
            bool aimForward = V.dot(V.norm(V.sub(j.indexTip, j.wrist)), fwdNorm) > Math.Cos(H.looseAim * V.DEG);
            bool looseGun = aimForward && ratios["index"] >= H.looseIndex && ratios["index"] - 0.5 * (ratios["ring"] + ratios["pinky"]) >= H.looseGap;
            bool gunPose = strictGun || looseGun;
            bool allCurl = true; foreach (var f in FINGERS) if (!curl[f]) { allCurl = false; break; }
            bool noneExt = true; foreach (var f in FINGERS) if (ext[f]) { noneExt = false; break; }
            bool allExt = true; foreach (var f in FINGERS) if (!ext[f]) { allExt = false; break; }
            bool palmOut = V.dot(palmN, fwdNorm) > Math.Cos(45 * V.DEG);
            bool shieldPose = allExt && palmOut && palm.y > sh.y && fwdDist > 0.12;

            string raw = "idle";
            if (nearEye && noneExt) raw = "binoc";
            else if (allCurl) raw = "fist";
            else if (shieldPose) raw = "shield";
            else if (gunPose && (straight || (aimForward && reach >= H.looseReach))) raw = "gun";

            if (raw != cand) { cand = raw; candT = 0; }
            else candT += dt;
            if (cand != state && candT >= (state == "gun" && cand == "idle" ? H.gunHold : H.holdSec)) {
                events.Add(new StateEvent { side=this.side, from=state, to=cand });
                state = cand;
                if (state == "gun") { hist.Clear(); armed = true; }
            }

            // 指向：指尖方向与手背方向混合，抵消单根手指的抖动
            Vec3d dirFinger = V.norm(V.sub(j.indexTip, j.indexProx));
            Vec3d dirHand = V.norm(V.sub(j.indexProx, j.wrist));
            Vec3d dir = V.norm(V.add(V.scale(dirFinger, 0.6), V.scale(dirHand, 0.4)));
            Vec3d origin = V.clone(j.indexTip);
            Vec3d flickDir = V.norm(V.sub(j.indexTip, j.wrist));
            double pitch = Math.Asin(V.clamp(flickDir.y, -1, 1)) / V.DEG;

            // 挑腕开火
            var F = cfg.flick;
            Vec3d across = V.sub(j.indexProx, j.pinkyProx);                         // 手掌横向：小指根 → 食指根 = 拇指一侧，去掉沿手指方向的分量
            Vec3d thumbSide = V.norm(V.sub(across, V.scale(flickDir, V.dot(across, flickDir))));
            hist.Add(new Sample { t = time, pitch = pitch, dir = dir, origin = origin, flick = flickDir, thumb = thumbSide, wrist = V.clone(j.wrist) });
            while (hist.Count > 0 && time - hist[0].t > 0.4) hist.RemoveAt(0);
            if (state == "gun") {
                if (!armed) {
                    peakPitch = Math.Max(peakPitch, pitch);
                    if (pitch < peakPitch - 4 || time - lastFire > 0.3) { armed = true; rearmT = time; }   // 上膛前的样本作废：收手时不能拿挑腕之前的低位当起点，算成第二枪
                } else {
                    double best = 0;
                    for (int i = hist.Count - 1; i >= 0 && time - hist[i].t <= F.window && hist[i].t >= rearmT; i--)
                        best = Math.Max(best, RiseSince(hist[i], pitch, flickDir, j.wrist));
                    if (best >= F.minRise && time - lastFire >= F.minInterval) {
                        // 起点：最靠近现在、且已经接近最低点的样本（不用最低点本身：稳住时的抖动会让最低点落在很久以前）
                        int start = hist.Count - 1;
                        for (int i = hist.Count - 1; i >= 0 && time - hist[i].t <= F.window && hist[i].t >= rearmT; i--)
                            if (RiseSince(hist[i], pitch, flickDir, j.wrist) >= best - F.onsetSlack) { start = i; break; }
                        var h0 = hist[start];
                        double vel = RiseSince(h0, pitch, flickDir, j.wrist) / Math.Max(time - h0.t, 1e-3);
                        if (vel >= F.minVel) {
                            // 瞄准取抬腕开始前一点点的方向（挑腕本身不带偏枪口）
                            var aim = h0;
                            for (int i = start; i >= 0; i--) {
                                if (h0.t - hist[i].t > 0.03) break;
                                aim = hist[i];
                            }
                            events.Add(new FireEvent { side=this.side, origin=V.clone(aim.origin), dir=V.clone(aim.dir) });
                            lastFire = time;
                            armed = false;
                            peakPitch = pitch;
                        }
                    }
                }
            }

            // 捏合（只在非握拳时识别，避免握拳时拇指碰到食指误触）
            double pd = V.dist(j.thumbTip, j.indexTip);
            if (!pinching && pd < H.pinchOn && raw != "fist" && state != "fist") {
                pinching = true;
                events.Add(new PinchStartEvent { side=this.side });
            } else if (pinching && pd > H.pinchOff) pinching = false;

            lastOut = new HandOut {
                side=this.side, tracked=true, state=state, raw=raw, gunPose=gunPose,
                straight=straight, reach=reach, origin=origin, dir=dir, palm=palm, palmN=palmN,
                pinching=pinching, ratios=ratios, events=events,
            };
            return lastOut;
        }
    }

    // ───────────────────────── ArmCalibrator ─────────────────────────
    public class ArmCalibrator
    {
        public double t = 0;
        private List<double> samples = new List<double>();
        public bool done = false;
        public double len;

        public ArmCalibrator() { len = Config.Default.arm.defaultLen; }

        public double Update(double dt, Head head, Joints L, Joints R)
        {
            if (done) return len;
            double ok(Joints jj, string s) {
                if (jj == null) return 0;
                Vec3d sh = GestureUtils.ShoulderOf(head, s);
                double d = V.dist(jj.wrist, sh);
                Vec3d hd = V.norm(head.fwd);
                double f = V.dot(V.sub(jj.wrist, head.pos), hd);
                return f > 0.3 ? d : 0;
            }
            double dl = ok(L, "left"), dr = ok(R, "right");
            if (dl > 0.4 && dr > 0.4) {
                t += dt;
                samples.Add((dl + dr) / 2);
                if (t >= 1.5) {
                    var s = new List<double>(samples);
                    s.Sort();
                    len = V.clamp(s[(int)(s.Count * 0.8)], 0.45, 0.85);
                    done = true;
                }
            } else { t = 0; samples.Clear(); }
            return len;
        }

        public double Progress() { return done ? 1 : Math.Min(1, t / 1.5); }
    }
}
