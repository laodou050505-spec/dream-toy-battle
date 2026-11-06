// 门后机关 / 补给礼盒 / 门涌 / 难度系数 的规则测试（Unity 版新增，原型里没有）。
using System.Linq;
using NUnit.Framework;
using Bang.Logic;
using static Bang.Logic.Tests.TestHelpers;

namespace Bang.Logic.Tests
{
    [TestFixture]
    public class MechanicsTests
    {
        /// <summary>边瞄边打：视线每帧跟着目标的实时位置走（飞行中的积木球、飘着的礼盒），锁定后按当时的位置开枪。</summary>
        static void AimFire(Game g, System.Func<Vec3d> pos)
        {
            var origin = new Vec3d(0.2, 1.35, -0.6);
            var hold = new Hands { right = new HandOut { state = "gun", events = new System.Collections.Generic.List<HandEvent>() } };
            for (int i = 0; i < 14; i++)
            {
                g.GazeOverride = new GazeRay { origin = HEAD.pos, dir = V.norm(V.sub(pos(), HEAD.pos)) };
                g.Update(1.0 / 60, new FrameInput { head = HEAD, gaze = g.GazeOverride, hands = hold });
            }
            var fe = new FireEvent { side = "right", origin = V.clone(origin), dir = V.norm(V.sub(pos(), origin)) };
            g.Update(1.0 / 60, new FrameInput { head = HEAD, gaze = g.GazeOverride, hands = new Hands { right = new HandOut { state = "gun", events = new System.Collections.Generic.List<HandEvent> { fe } } } });
        }

        static Game QuietWave(int wave)
        {
            var g = WaveGame(1);
            g.BeginWave(wave);
            g.run.queue.Clear();          // 不出普通小怪，只看机关
            HoldWave(g);                  // 有一只打不到的靶，这一波不会结束
            g.DrainFx();
            return g;
        }

        [Test]
        public void MT01_CatapultOnlyFromWave3()
        {
            var g = QuietWave(1);
            Run(g, 20);
            Assert.IsFalse(g.DrainFx().Any(f => f is CatapultFireFx), "第 1 章前两波投石车不该开火（入门）");
            g = QuietWave(2);
            Run(g, 6 + 10 + 0.2);
            var fx = g.DrainFx();
            Assert.IsTrue(fx.Any(f => f is CatapultWindFx), "开火前要有蓄力预警");
            Assert.IsTrue(fx.Any(f => f is CatapultFireFx), "第 2 波起每 9 秒抛一颗积木球");
        }

        [Test]
        public void MT02_BallKnocksGun()
        {
            var g = QuietWave(2);
            Run(g, 6 + 10 + 0.1);
            Assert.IsTrue(g.bullets.Any(b => b.kind == "ball"));
            Run(g, 2.5);
            Assert.IsTrue(g.DrainFx().Any(f => f is KnockedFx), "积木球砸中头要打飞枪");
        }

        [Test]
        public void MT03_ShootBallInAir()
        {
            var g = QuietWave(2);
            Run(g, 6 + 10 + 0.4);
            var ball = g.bullets.First(b => b.kind == "ball");
            AimFire(g, () => ball.pos);
            Assert.IsTrue(g.DrainFx().Any(f => f is BallPopFx), "空中的积木球可以打掉");
            Assert.IsFalse(g.bullets.Any(b => b.id == ball.id));
        }

        [Test]
        public void MT04_CatapultBreaksAfter6Hits()
        {
            var g = QuietWave(2);
            var c = g.catapult;
            for (int i = 0; i < 6; i++) FireAt(g, c.pos);
            var fx = g.DrainFx();
            Assert.IsTrue(fx.Any(f => f is MechBrokenFx), "打 6 枪散架");
            Run(g, 15);
            Assert.IsFalse(g.DrainFx().Any(f => f is CatapultFireFx), "散架期间不开火");
            Run(g, 6);
            Assert.IsTrue(g.DrainFx().Any(f => f is MechRepairFx), "20 秒后修好");
        }

        [Test]
        public void MT05_GiftOpensWithOneShot()
        {
            var g = QuietWave(1);
            for (int i = 0; i < 3; i++) g.run.queue.Add("planter");                 // 快打完时不出补给，这里留着敌人
            g.run.spawnT = 999;
            for (int i = 0; i < 200 && g.gifts.Count == 0; i++) Run(g, 0.1);       // 等礼盒飘出来（第 8～16 秒之间）
            Assert.IsTrue(g.gifts.Count > 0, "每波都会飘出礼盒");
            Run(g, 1.6);                                                           // 等它飘进房间
            var gift = g.gifts.First();
            AimFire(g, () => gift.pos);
            var open = g.DrainFx().OfType<GiftOpenFx>().FirstOrDefault();
            Assert.IsNotNull(open, "空投礼盒一枪就开");
            Assert.Contains(open.reward, new[] { "gold", "freeze", "repair", "unflag", "rapid" });   // 第 2 波起第一个礼盒保底无限火力
        }

        [Test]
        public void MT06_DoorSurgeFromWave3()
        {
            var g = QuietWave(2);
            int before = g.enemies.Count;
            Run(g, 26);
            var fx = g.DrainFx();
            Assert.IsTrue(fx.Any(f => f is SurgeWarnFx), "门涌前要预警");
            Assert.AreEqual(2, fx.Count(f => f is SurgeFx), "第 1 章一次门涌 2 只（之后的章节 3 只）");
            Assert.GreaterOrEqual(g.enemies.Count + fx.Count(f => f is KillFx || f is ExitFx), before + 2);
        }

        [Test]
        public void MT07_DifficultyCountMul()
        {
            var g = WaveGame(1);
            g.BeginWave(0);
            Assert.AreEqual(3, g.run.queue.Count, "第 1 章是入门章，不加数量");
            var g3 = WaveGame(3);
            g3.BeginWave(0);
            double mul = g3.cfg.diff.CountMul(CHAPTERS.World(3));   // 难度系数按主题（世界）取，和 Game.BeginWave 一致
            Assert.Greater(mul, 1.0, "第 3 章要比原型多");
            int n = 0; foreach (var (k, c) in CHAPTERS.Get(3).waves[0].spawns) n += k == "commander" ? c : (int)System.Math.Round(c * mul);
            Assert.AreEqual(n, g3.run.queue.Count, "第 3 章数量按难度系数放大");
        }
    }
}
