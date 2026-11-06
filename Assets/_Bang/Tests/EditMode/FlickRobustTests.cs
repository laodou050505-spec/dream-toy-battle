// 挑腕识别的鲁棒性矩阵（用户：任何情况抬手腕不能识别成开枪都要注意）：
// 模拟头显上真实会发生的情况——追踪抖动、挑腕瞬间丢帧、手指松开、手型倾斜、不同瞄准方向、不同追踪帧率。
// 正例要求命中率达标；反例（慢抬手、瞄准移动、收枪）要求基本不走火。命中率写到 /tmp/flick_rates.txt 方便对比。
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Bang.Logic;
using static Bang.Logic.Tests.TestHelpers;

namespace Bang.Logic.Tests
{
    [TestFixture]
    public class FlickRobustTests
    {
        const string Report = "/tmp/flick_rates.txt";

        class Trial
        {
            public double amp = 18, dur = 0.09, roll = 0, aimPitch = 0, aimYaw = 0, noise = 0.25, hz = 72;
            public int dropAt = -1, dropFrames = 0;      // 挑腕第几帧开始丢追踪、丢几帧
            public bool loosen;                          // 挑腕时无名指小指松开
        }

        // 手的朝向：先定指向（俯仰 + 偏航），再绕指向滚转；挑腕 = 沿拇指一侧转 flick 度
        static Joints Hand(Trial t, double flick, Random rng, bool loose)
        {
            double Noise() => (rng.NextDouble() * 2 - 1) * t.noise;
            double p = (t.aimPitch + Noise()) * V.DEG, y = (t.aimYaw + Noise()) * V.DEG, r = t.roll * V.DEG;
            var f0 = new Vec3d(Math.Sin(y) * Math.Cos(p), Math.Sin(p), -Math.Cos(y) * Math.Cos(p));
            var side = V.norm(V.cross(new Vec3d(0, 1, 0), f0));
            var up0 = V.norm(V.cross(f0, side));
            var thumb = V.norm(V.add(V.scale(up0, Math.Cos(r)), V.scale(side, Math.Sin(r))));
            double a = flick * V.DEG;
            var f = V.norm(V.add(V.scale(f0, Math.Cos(a)), V.scale(thumb, Math.Sin(a))));
            var fThumb = V.norm(V.sub(thumb, V.scale(f, V.dot(thumb, f))));
            var n = V.norm(V.cross(fThumb, f));                                   // 右手：MakeHand 里 t = f × n
            var w = V.add(RIGHT_STRAIGHT, new Vec3d(Noise() * 0.003, Noise() * 0.003, Noise() * 0.003));
            return MakeHand("right", loose ? "open" : "gun", w, f, n);
        }

        /// <summary>稳住 → 挑腕 → 收回，返回开了几枪。</summary>
        static int Run(Trial t, int seed)
        {
            var rng = new Random(seed); var g = new HandGesture("right"); int fires = 0; double dt = 1.0 / t.hz;
            void Step(Joints j) { foreach (var e in g.Update(dt, j, HEAD, 0.62).events) if (e.type == "fire") fires++; }
            for (int i = 0, n = (int)(0.5 * t.hz); i < n; i++) Step(Hand(t, 0, rng, false));
            int up = Math.Max(2, (int)Math.Round(t.dur * t.hz));
            for (int i = 1; i <= up; i++)
            {
                bool drop = t.dropAt >= 0 && i >= t.dropAt && i < t.dropAt + t.dropFrames;
                double u = i / (double)up; u = u * u * (3 - 2 * u);
                Step(drop ? null : Hand(t, t.amp * u, rng, t.loosen));
            }
            for (int i = 0, n = (int)(0.3 * t.hz); i < n; i++) Step(Hand(t, t.amp * (1 - i / (0.3 * t.hz)), rng, false));
            return fires;
        }

        static double Rate(string name, Trial t)
        {
            int ok = 0; const int N = 40;
            for (int s = 0; s < N; s++) if (Run(t, s * 7 + 1) == 1) ok++;
            double rate = ok / (double)N;
            File.AppendAllText(Report, $"{name,-22} {rate * 100,5:0}%\n");
            return rate;
        }

        static readonly (string name, Trial t)[] Cases =
        {
            ("干净", new Trial { noise = 0 }),
            ("常规抖动", new Trial()),
            ("较大抖动", new Trial { noise = 0.6 }),
            ("轻挑12度", new Trial { amp = 12 }),
            ("轻挑10度", new Trial { amp = 10, dur = 0.08 }),
            ("慢挑0.14秒", new Trial { dur = 0.14 }),
            ("慢挑0.18秒", new Trial { amp = 22, dur = 0.18 }),
            ("大挑30度", new Trial { amp = 30, dur = 0.12 }),
            ("30Hz追踪", new Trial { hz = 30 }),
            ("60Hz追踪", new Trial { hz = 60 }),
            ("丢1帧", new Trial { dropAt = 2, dropFrames = 1 }),
            ("丢2帧", new Trial { dropAt = 2, dropFrames = 2 }),
            ("丢4帧", new Trial { dropAt = 2, dropFrames = 4 }),
            ("手指松开", new Trial { loosen = true }),
            ("手歪30度", new Trial { roll = 30 }),
            ("手歪60度", new Trial { roll = 60 }),
            ("手歪80度", new Trial { roll = 80 }),
            ("瞄高40度", new Trial { aimPitch = 40 }),
            ("瞄高65度", new Trial { aimPitch = 65 }),
            ("瞄低30度", new Trial { aimPitch = -30 }),
            ("瞄左50度", new Trial { aimYaw = -50 }),
            ("瞄右50度", new Trial { aimYaw = 50 }),
            ("手歪+丢帧+抖动", new Trial { roll = 45, dropAt = 3, dropFrames = 1, noise = 0.5 }),
        };

        [Test]
        public void FlickRecognizedInAllSituations()
        {
            File.WriteAllText(Report, "");
            var bad = new List<string>();
            foreach (var (name, t) in Cases) { double r = Rate(name, t); if (r < 0.95) bad.Add($"{name} {r * 100:0}%"); }
            Assert.IsEmpty(bad, "命中率不足：" + string.Join("，", bad));
        }

        [Test]
        public void RapidFlicksEachFireOnce()
        {
            foreach (double amp in new[] { 14.0, 22.0, 30.0 })
            {
                var t = new Trial { amp = amp, dur = 0.08 }; var rng = new Random(5); var g = new HandGesture("right"); int fires = 0; double dt = 1.0 / 72;
                void Step(double a) { foreach (var e in g.Update(dt, Hand(t, a, rng, false), HEAD, 0.62).events) if (e.type == "fire") fires++; }
                for (int i = 0; i < 36; i++) Step(0);
                for (int k = 0; k < 5; k++)                                     // 一秒约三枪：挑 0.08 秒、收 0.22 秒
                {
                    for (int i = 1; i <= 6; i++) Step(amp * i / 6.0);
                    for (int i = 15; i >= 0; i--) Step(amp * i / 15.0);
                }
                File.AppendAllText(Report, $"连续快挑 {amp:0} 度：5 次开 {fires} 枪\n");
                Assert.AreEqual(5, fires, $"幅度 {amp} 度的连续挑腕应该一次一枪");
            }
        }

        // ────────── 反例：不该开枪 ──────────
        static int Motion(double sec, Func<double, double, Joints> pose, int seed)
        {
            var g = new HandGesture("right"); int fires = 0; const double dt = 1.0 / 72; var rng = new Random(seed);
            for (int i = 0; i < 36; i++) g.Update(dt, pose(0, 0.25), HEAD, 0.62);
            for (double t = 0; t < sec; t += dt) foreach (var e in g.Update(dt, pose(t / sec, 0.25), HEAD, 0.62).events) if (e.type == "fire") fires++;
            return fires;
        }

        [Test]
        public void NoFireWhileAiming()
        {
            var rng = new Random(3);
            int steady = Motion(3, (u, n) => Hand(new Trial { noise = 0.8 }, 0, rng, false), 1);
            int slow = Motion(0.7, (u, n) => Hand(new Trial(), 25 * u, rng, false), 2);
            int sweep = Motion(0.35, (u, n) => Hand(new Trial { aimYaw = -40 + 80 * u }, 0, rng, false), 3);
            int sweepRolled = Motion(0.35, (u, n) => Hand(new Trial { aimYaw = -40 + 80 * u, roll = 70 }, 0, rng, false), 4);
            int down = Motion(0.12, (u, n) => Hand(new Trial(), 25 * (1 - u), rng, false), 5);
            File.AppendAllText(Report, $"走火：静止抖动 {steady} 慢抬 {slow} 快速横扫 {sweep} 手歪横扫 {sweepRolled} 快速下压 {down}\n");
            Assert.AreEqual(0, steady + slow + sweep + down, "瞄准动作不能走火");
        }
    }
}
