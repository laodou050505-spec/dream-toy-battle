// 手势识别测试（对应 test/gestures.test.js 全部 10 个用例）
// 加上参数化帧率测试（60Hz / 72Hz / 90Hz）验证开火手势。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Bang.Logic;
using static Bang.Logic.Tests.TestHelpers;

namespace Bang.Logic.Tests
{
    [TestFixture]
    public class GestureTests
    {
        static readonly Vec3d FWD      = new Vec3d(0, 0, -1);
        static readonly Vec3d LEFTWARD = new Vec3d(-1, 0, 0);

        // ────────── feed 辅助：向 HandGesture 连续投喂关节数据 ──────────
        private static (HandOut @out, List<HandEvent> evs) Feed(
            HandGesture g, double sec, Func<int, int, Joints> jFn, double dt = 1.0/60)
        {
            var evs = new List<HandEvent>();
            HandOut @out = null;
            int n = (int)Math.Round(sec / dt);
            for (int i = 0; i < n; i++) {
                @out = g.Update(dt, jFn(i, n), HEAD, 0.62);
                evs.AddRange(@out.events);
            }
            return (@out, evs);
        }

        // 常量 joints 版本（每帧相同）
        private static (HandOut @out, List<HandEvent> evs) Feed(
            HandGesture g, double sec, Joints joints, double dt = 1.0/60)
            => Feed(g, sec, (i, n) => joints, dt);

        // 不标准的持枪：无名指、小指只弯了一半
        private static Joints Sloppy(Vec3d wrist, Vec3d f, Vec3d n)
        {
            var j = MakeHand("right", "gun", wrist, f, n);
            var nf = V.norm(f); var nn = V.norm(n);
            Vec3d Half(Vec3d prox) => V.add(prox, V.add(V.scale(nf, 0.038), V.scale(nn, 0.024)));
            j.ringTip = Half(j.ringProx); j.pinkyTip = Half(j.pinkyProx);
            return j;
        }

        [Test]
        public void GL_LooseGunWhenAimingForward()
        {
            var chest = new Vec3d(0.16, 1.2, -0.34);
            var g = new HandGesture("right");
            Assert.AreEqual("gun", Feed(g, 0.3, Sloppy(chest, FWD, LEFTWARD)).@out.state, "手型差不多、枪口朝前：算持枪");

            var g2 = new HandGesture("right");
            var hip = new Vec3d(0.22, 0.95, -0.2);
            Assert.AreEqual("idle", Feed(g2, 0.3, Sloppy(hip, new Vec3d(0, -1, -0.3), LEFTWARD)).@out.state, "手垂着、枪口朝下：不算");

            var g3 = new HandGesture("right");
            Assert.AreEqual("idle", Feed(g3, 0.3, MakeHand("right", "relaxed", chest, FWD, LEFTWARD)).@out.state, "放松的手朝前：不算");

            var g4 = new HandGesture("right");
            Assert.AreNotEqual("gun", Feed(g4, 0.3, MakeHand("right", "open", chest, FWD, LEFTWARD)).@out.state, "五指张开：不算");
        }

        // ────────── 辅助：绕手腕把手往上转 deg 度 ──────────
        private static Joints Pitched(double deg)
        {
            double a = deg * V.DEG;
            Vec3d f = new Vec3d(0, Math.Sin(a), -Math.Cos(a));
            return MakeHand("right", "gun", RIGHT_STRAIGHT, f, LEFTWARD);
        }

        // ────────────────────────────────────────────────────────
        [Test]
        public void Test01_PalmNormalCorrect()
        {
            var r = MakeHand("right", "open", RIGHT_STRAIGHT, FWD, new Vec3d(0, -1, 0));
            Assert.Less(GestureUtils.PalmNormal(r, "right").y, -0.95, "右手掌心朝下");
            var l = MakeHand("left", "open", new Vec3d(-0.17, 1.38, -0.58), FWD, new Vec3d(0, -1, 0));
            Assert.Less(GestureUtils.PalmNormal(l, "left").y, -0.95, "左手掌心朝下");
        }

        [Test]
        public void Test02_ArmStraightGun()
        {
            // 手臂伸直 + 手指枪 = 持枪
            var g = new HandGesture("right");
            var (out1, _) = Feed(g, 0.3, MakeHand("right", "gun", RIGHT_STRAIGHT, FWD, LEFTWARD));
            Assert.AreEqual("gun", out1.state, "伸直持枪");

            // 手缩在肩膀跟前不持枪（枪口朝前时胸前位置也算持枪，见 GL_LooseGunWhenAimingForward）
            var g2 = new HandGesture("right");
            var (out2, _) = Feed(g2, 0.3, MakeHand("right", "gun", new Vec3d(0.2, 1.22, -0.08), FWD, LEFTWARD));
            Assert.AreEqual("idle", out2.state, "手臂弯曲不持枪");
            Assert.IsTrue(out2.gunPose, "手势本身识别到了，只是手臂没伸直");
        }

        [Test]
        public void Test03_FistAndBinoc()
        {
            // 握实拳 = 换弹
            var g = new HandGesture("right");
            var (ho, _) = Feed(g, 0.3, MakeHand("right", "fist", RIGHT_STRAIGHT, FWD, LEFTWARD));
            Assert.AreEqual("fist", ho.state);

            // 空心拳举到眼前 = 望远镜
            var g2 = new HandGesture("right");
            var nearEye = new Vec3d(0.03, 1.58, -0.06);
            var (out2, _) = Feed(g2, 0.3, MakeHand("right", "hollow", nearEye, FWD, LEFTWARD));
            Assert.AreEqual("binoc", out2.state);

            // 空心拳不在眼前：算握拳
            var g3 = new HandGesture("right");
            var (out3, _) = Feed(g3, 0.3, MakeHand("right", "hollow", RIGHT_STRAIGHT, FWD, LEFTWARD));
            Assert.AreEqual("fist", out3.state);
        }

        [Test]
        public void Test04_ShieldGesture()
        {
            // 抬手到脸前、掌心朝外 = 举盾
            var g = new HandGesture("left");
            var W = new Vec3d(-0.1, 1.5, -0.35);
            var up = new Vec3d(0, 1, 0);
            var (ho, _) = Feed(g, 0.3, MakeHand("left", "open", W, up, FWD));
            Assert.AreEqual("shield", ho.state);

            // 掌心朝自己不算
            var g2 = new HandGesture("left");
            var (out2, _) = Feed(g2, 0.3, MakeHand("left", "open", W, up, new Vec3d(0, 0, 1)));
            Assert.AreEqual("idle", out2.state);
        }

        [Test]
        public void Test05_Debounce()
        {
            var g = new HandGesture("right");
            var gun  = MakeHand("right", "gun",  RIGHT_STRAIGHT, FWD, LEFTWARD);
            var fist = MakeHand("right", "fist", RIGHT_STRAIGHT, FWD, LEFTWARD);
            Feed(g, 0.3, gun);
            var (r, _) = Feed(g, 0.05, fist);
            Assert.AreEqual("gun", r.state, "短于 80ms 的手势不切换");
        }

        [Test]
        public void Test06_FireGesture_60Hz()
        {
            FireGestureAtHz(60);
        }

        [Test]
        public void Test06_FireGesture_72Hz()
        {
            FireGestureAtHz(72);
        }

        [Test]
        public void Test06_FireGesture_90Hz()
        {
            FireGestureAtHz(90);
        }

        private void FireGestureAtHz(int hz)
        {
            double dt = 1.0 / hz;
            var g = new HandGesture("right");
            var (outBefore, _) = Feed(g, 0.3, (i, n) => Pitched(0), dt);
            Vec3d before = outBefore.dir;

            var (r, evs) = Feed(g, 0.07, (i, n) => Pitched(i / (double)(Math.Max(n - 1, 1)) * 18), dt);
            var fires = evs.FindAll(e => e.type == "fire");
            Assert.AreEqual(1, fires.Count, $"开一枪 ({hz}Hz)");

            double off = V.angleDeg(((FireEvent)fires[0]).dir, before);
            Assert.Less(off, 1.0, $"瞄准应等于挑腕前的指向，偏了 {off:F2}°（{hz}Hz）");
        }

        [Test]
        public void Test07_SlowLiftNoFire()
        {
            var g = new HandGesture("right");
            Feed(g, 0.3, (i, n) => Pitched(0));
            var (_, evs) = Feed(g, 0.6, (i, n) => Pitched(i / (double)(Math.Max(n - 1, 1)) * 20));
            Assert.AreEqual(0, evs.FindAll(e => e.type == "fire").Count, "慢慢抬手不会开火");
        }

        [Test]
        public void Test08_TwoFlicksTwoShots()
        {
            var g = new HandGesture("right");
            Feed(g, 0.3, (i, n) => Pitched(0));
            int fires = 0;
            for (int k = 0; k < 2; k++) {
                var (_, evs) = Feed(g, 0.07, (i, n) => Pitched(i / (double)(Math.Max(n - 1, 1)) * 18));
                fires += evs.FindAll(e => e.type == "fire").Count;
                Feed(g, 0.25, (i, n) => Pitched(18 - i / (double)(Math.Max(n - 1, 1)) * 18));
            }
            Assert.AreEqual(2, fires, "连续两次挑腕各开一枪");
        }

        [Test]
        public void Test09_PinchNoPseudoFist()
        {
            // 捏合触发 pinchStart
            var g = new HandGesture("right");
            var W = new Vec3d(0.15, 1.3, -0.4);
            var (_, evs) = Feed(g, 0.2, MakeHand("right", "pinch", W, FWD, new Vec3d(0, -1, 0)));
            Assert.AreEqual(1, evs.FindAll(e => e.type == "pinchStart").Count);

            // 握拳时不误触 pinchStart
            var g2 = new HandGesture("right");
            var (_, evs2) = Feed(g2, 0.3, MakeHand("right", "fist", W, FWD, new Vec3d(0, -1, 0)));
            Assert.AreEqual(0, evs2.FindAll(e => e.type == "pinchStart").Count);
        }

        [Test]
        public void Test10_ArmCalibration()
        {
            var c = new ArmCalibrator();
            var R = MakeHand("right", "open", RIGHT_STRAIGHT, FWD, new Vec3d(0, -1, 0));
            var L = MakeHand("left", "open", new Vec3d(-0.17, 1.38, -0.58), FWD, new Vec3d(0, -1, 0));
            for (int i = 0; i < 100; i++) c.Update(1.0/60, HEAD, L, R);
            Assert.IsTrue(c.done, "校准完成");
            Assert.Greater(c.len, 0.5, $"臂长 {c.len} > 0.5");
            Assert.Less(c.len, 0.7, $"臂长 {c.len} < 0.7");
        }
    }
}
