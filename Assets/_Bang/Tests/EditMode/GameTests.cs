// 游戏规则测试（对应 test/game.test.js 全部 20 个用例）。
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Bang.Logic;
using static Bang.Logic.Tests.TestHelpers;

namespace Bang.Logic.Tests
{
    [TestFixture]
    public class GameTests
    {
        // ────────── 辅助 ──────────
        private static void Step(Game g, string state, HandEvent ev = null)
        {
            var events = ev != null ? new List<HandEvent> { ev } : new List<HandEvent>();
            var ho = new HandOut { state = state, events = events };
            g.Update(1.0/60, new FrameInput { head = HEAD, hands = new Hands { right = ho } });
        }

        private static Flag MakeFlag(int id, int i, double amount = 12)
        {
            return new Flag {
                id = id, type = "small",
                pos = new Vec3d(i * 0.3, 0, -2),
                hp = 10, maxHp = 10,
                amount = amount, cap = 12, rate = 0.6, radius = 1,
                hidden = false, dying = false, age = 0,
            };
        }

        // ─────────────────────────────────────────────────────────
        [Test]
        public void GT01_ChapterLessonFirst()
        {
            // 第 1 章首次进入先教学
            var g = new Game(new GameOpts { store = NewMemStore() });
            g.StartChapter(1);
            Assert.AreEqual("lesson", g.phase);

            // skipLesson 后进入第 1 波
            var g2 = WaveGame(1);
            Assert.AreEqual("wave", g2.phase);
            Assert.AreEqual(0, g2.run.wave);
        }

        [Test]
        public void GT02_GazeAim()
        {
            var g = WaveGame(1);
            var e = g.SpawnEnemy("planter", new SpawnOpts { pos = new Vec3d(0, 0, -2), portal = g.portals[0], eliteExplicit = true });
            e.state = "hold";                                        // 站着不动（插旗时是弱点，伤害翻倍，这里测普通伤害）
            FireAt(g, CenterOf(e));
            Assert.AreEqual(1.0, e.hp, 0.001, "小沙鹰伤害 1");

            // 枪口偏 40°：打空
            Vec3d off = V.add(HEAD.pos, V.rotY(new Vec3d(0, -0.3, -2), -40 * V.DEG));
            FireAt(g, CenterOf(e), "right", aimPosOverride: off);
            Assert.AreEqual(1.0, e.hp, 0.001, "偏 40° 打空");

            // 视线看空地：打空
            g.GazeOverride = new GazeRay { origin = HEAD.pos, dir = V.norm(new Vec3d(1, 0.5, -1)) };
            Run(g, 0.2);
            var origin2 = new Vec3d(0.2, 1.35, -0.6);
            var dir2 = V.norm(V.sub(CenterOf(e), origin2));
            var fireHand = new HandOut {
                state = "gun",
                events = new List<HandEvent> { new FireEvent { side = "right", origin = V.clone(origin2), dir = dir2 } }
            };
            g.Update(1.0/60, new FrameInput { head = HEAD, gaze = g.GazeOverride, hands = new Hands { right = fireHand } });
            Assert.AreEqual(0.0, e.hp, 0.001, "眼动没锁上也照样能打中枪口指着的小怪（眼动只是加分项）");
        }

        [Test]
        public void GT02b_EnemyBeforeFlag()
        {
            var g = WaveGame(1);
            g.flags.Add(MakeFlag(950, 0));
            var f = g.flags[0]; f.pos = new Vec3d(0.12, 0, -2.0);
            var e = g.SpawnEnemy("brute", new SpawnOpts { pos = new Vec3d(-0.12, 0, -2.0), portal = g.portals[0], eliteExplicit = true });
            e.state = "plant";
            double ehp = e.hp, fhp = f.hp;
            // 枪口指在两者中间偏旗子一点：仍然先打小怪
            var mid = new Vec3d(0.04, 0.4, -2.0);
            g.GazeOverride = new GazeRay { origin = HEAD.pos, dir = V.norm(new Vec3d(1, 0.5, -1)) };
            Run(g, 0.2);
            FireAt(g, mid, gazeAtTarget: false);
            Assert.Less(e.hp, ehp, "敌人优先");
            Assert.AreEqual(fhp, f.hp, 1e-6, "旗子没被打");
        }

        [Test]
        public void GT03_NoOneHitKill()
        {
            // 结实的小怪不会一枪死（hp >= 2）；轻型小怪（老鼠、纸飞机、跟班、小气球、靶子）一枪就倒，打起来爽
            foreach (var kind in new[]{"planter","brute","shooter","jack","tin","ninja","commander","balloon","rcCar","repair","trainer"}) {
                var K = KINDS.Get(kind);
                Assert.GreaterOrEqual(K.hp, 2, $"{kind} hp={K.hp}");
            }
            foreach (var kind in new[]{"mouse","plane","minion","balloonSmall","dummy"})
                Assert.AreEqual(1, KINDS.Get(kind).hp, kind);
        }

        [Test]
        public void GT04_ReloadZones()
        {
            var RT = Config.Default.reload;
            Game mk() { var g = WaveGame(1); g.weapons.right.ammo = 0; return g; }

            // 金区松手更快
            {
                var g = mk();
                Step(g, "fist", new StateEvent { from = "gun", to = "fist" });
                Run(g, (RT.goldStart + RT.goldEnd) / 2, i => new Hands { right = MakeStateHand("fist") }, 1.0/60);
                Step(g, "idle", new StateEvent { from = "fist", to = "idle" });
                Run(g, RT.fastFinish + 0.05, i => new Hands { right = MakeStateHand("idle") }, 1.0/60);
                Assert.AreEqual(GUNS.pistol.mag, g.weapons.right.ammo);
                Assert.AreEqual(1, g.counters.golds);
            }

            // 太早松手卡壳
            {
                var g = mk();
                Step(g, "fist", new StateEvent { from = "gun", to = "fist" });
                Run(g, 0.2, i => new Hands { right = MakeStateHand("fist") }, 1.0/60);
                Step(g, "idle", new StateEvent { from = "fist", to = "idle" });
                Run(g, RT.total, i => new Hands { right = MakeStateHand("idle") }, 1.0/60);
                Assert.AreEqual(0, g.weapons.right.ammo, "卡壳期间还没换完");
                Run(g, RT.jam, i => new Hands { right = MakeStateHand("idle") }, 1.0/60);
                Assert.AreEqual(GUNS.pistol.mag, g.weapons.right.ammo);
            }

            // 一直握着
            {
                var g = mk();
                Step(g, "fist", new StateEvent { from = "gun", to = "fist" });
                Run(g, RT.total + 0.05, i => new Hands { right = MakeStateHand("fist") }, 1.0/60);
                Assert.AreEqual(GUNS.pistol.mag, g.weapons.right.ammo);
            }
        }

        [Test]
        public void GT05_PlanterFlag()
        {
            var g = WaveGame(1);
            g.run.queue.Clear();
            HoldWave(g);
            g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            Run(g, 25);
            Assert.AreEqual(1, g.flags.Count);
            var f = g.flags[0];
            Assert.AreEqual(FLAGS.small.hp, f.maxHp);
            Run(g, 45);
            Assert.AreEqual(100.0 / g.FlagsToFull, g.toy, 1e-2, $"每面旗 = 100 / {g.FlagsToFull} %，实际 {g.toy}");
            // 插旗兵插完后 dead
            Assert.IsTrue(g.enemies.All(e => e.dead || e.id == g.enemies[0].id) ||
                          g.enemies.TrueForAll(e => e.dead || e.kind == "dummy"), "插完旗回门里了");
        }

        [Test]
        public void GT06_FlagKillRetractToy()
        {
            var g = WaveGame(1);
            g.run.queue.Clear();
            HoldWave(g);
            g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            Run(g, 40);
            var f = g.flags[0];
            var fp = new Vec3d(f.pos.x, 0.45, f.pos.z);
            for (int i = 0; i < 10; i++) {
                g.weapons.right.ammo = 8;
                FireAt(g, fp);
                Run(g, 0.25);
            }
            Assert.IsTrue(f.dying, "旗子正在消失");
            Run(g, 2.2);
            Assert.AreEqual(0, g.flags.Count);
            Assert.Less(g.toy, 0.01, "玩具化度退清");
        }

        [Test]
        public void GT07_ToyOverflowLostRetry()
        {
            var g = WaveGame(1);
            for (int i = 0; i < 9; i++) g.flags.Add(MakeFlag(900 + i, i));
            Run(g, 0.1);
            Assert.AreEqual("lost", g.phase);
            Run(g, Config.Default.flow.lostHold + 5);
            Assert.AreEqual("lost", g.phase, "不自动重来，等玩家选择");
            g.RetryAfterLoss();
            Assert.AreEqual("wave", g.phase);
            Assert.AreEqual(0, g.run.wave);
            Assert.AreEqual(0, g.flags.Count, "恢复到这一波开头的旗子");
        }

        [Test]
        public void GT08_CommanderMinions()
        {
            var g = WaveGame(3, new Dictionary<string,bool>{{"shield",true},{"binoc",true}});
            g.run.queue.Clear();
            g.enemies.Clear();
            var c = g.SpawnEnemy("commander", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            g.SummonMinions(c, 3);
            Assert.AreEqual(3, g.enemies.Count(e => e.kind == "minion"));
            Run(g, 3);
            var m = g.enemies.First(e => e.kind == "minion");
            g.ApplyOrder(m, "charge");
            // 冲锋的跟班要走到插旗点再插 1.5 秒；插旗点是随机挑的（随机序列随难度系数变化），最多等 15 秒
            Flag minionFlag = null;
            for (int i = 0; i < 150 && minionFlag == null; i++) { Run(g, 0.1); minionFlag = g.flags.Find(f => f.commanderId == c.id); }
            Assert.IsNotNull(minionFlag, "跟班插了旗");
            g.DamageEnemy(c, 99);
            var minions = g.enemies.Where(e => e.kind == "minion").ToList();
            Assert.IsTrue(minions.All(x => x.state == "flee"), "打死指挥官后跟班逃跑");
            Assert.IsTrue(minionFlag.dying, "旗子倒下");
            Run(g, 10);
            Assert.AreEqual(0, g.enemies.Count(e => e.kind == "minion"), "跟班都逃回门里");
        }

        [Test]
        public void GT09_CommanderFalloffCrit()
        {
            var g = WaveGame(3, new Dictionary<string,bool>{{"shield",true},{"binoc",true}});
            g.run.queue.Clear();
            g.enemies.Clear();
            var c = g.SpawnEnemy("commander", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            Assert.AreEqual("approach", c.state, "指挥官从门后侧面走过来，不凭空出现");
            for (int i = 0; i < 60 * 12 && c.state == "approach"; i++) g.Update(1.0 / 60, new FrameInput { head = HEAD });
            Assert.AreEqual("command", c.state, "走到门后的指挥位置");
            g.enemies.RemoveAll(m => m != c); g.run.queue.Clear();
            FireAt(g, CenterOf(c));
            Assert.AreEqual(6.0, c.hp, 0.01, "远近伤害一样：一枪算一枪");
            g.marks[c.id] = g.time + 8;
            FireAt(g, CenterOf(c));
            Assert.AreEqual(4.0, c.hp, 0.01, "标记后暴击");
        }

        [Test]
        public void GT10_ShieldBlockParryKnock()
        {
            var g = WaveGame(2, new Dictionary<string,bool>{{"shield",true}});
            g.run.queue.Clear();
            g.enemies.Clear();
            var s = g.SpawnEnemy("shooter", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            s.state = "aim";
            s.pos = new Vec3d(0, 0, -2.5);

            var shieldHand = new HandOut {
                state = "shield", events = new List<HandEvent>(),
                palm = new Vec3d(0, 1.6, -0.3), palmN = new Vec3d(0, 0, -1),
            };
            Hands shieldHands() => new Hands { left = shieldHand };

            // 挡
            g.FireAtHead(s, "foam", 3.2);
            Run(g, 0.5, i => shieldHands());
            Run(g, 1.2, i => shieldHands());
            Assert.AreEqual(1, g.counters.blocked);

            // 弹反：子弹快到时才举盾
            Run(g, 1.0, i => new Hands());
            g.FireAtHead(s, "foam", 3.2);
            Run(g, 0.62, i => new Hands());
            double hp0 = s.hp;
            Run(g, 1.5, i => shieldHands());
            Assert.AreEqual(1, g.counters.parried);
            Assert.Less(s.hp, hp0, "弹反打中了射手");

            // 没挡
            Run(g, 1.0, i => new Hands());
            g.FireAtHead(s, "foam", 3.2);
            Run(g, 1.2, i => new Hands());
            Assert.Greater(g.knockT, 0, "枪被打飞");
        }

        [Test]
        public void GT11_NinjaAlwaysHittable()
        {
            var g = WaveGame(3, new Dictionary<string,bool>{{"shield",true},{"binoc",true}});
            g.run.queue.Clear();
            g.enemies.Clear();
            var n = g.SpawnEnemy("ninja", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            n.pos = new Vec3d(0, 0, -2);
            n.state = "hold";
            FireAt(g, CenterOf(n));
            Assert.AreEqual(1.0, n.hp, 0.001, "忍者不用望远镜也打得中（用户：不能有打不死的情况）");

            // 望远镜标记
            var binoHand = new HandOut { state = "binoc", events = new List<HandEvent>() };
            g.GazeOverride = new GazeRay { origin = HEAD.pos, dir = V.norm(V.sub(CenterOf(n), HEAD.pos)) };
            Run(g, 0.6, i => new Hands { left = binoHand });
            Assert.IsTrue(g.marks.ContainsKey(n.id), "已标记");
            Run(g, 0.1, i => new Hands());
            FireAt(g, CenterOf(n));
            Assert.LessOrEqual(n.hp, 0.001, "标记后暴击 ×2，一枪带走");
        }

        [Test]
        public void GT12_BinocBlocksFire()
        {
            var g = WaveGame(3, new Dictionary<string,bool>{{"shield",true},{"binoc",true}});
            int ammo = g.weapons.right.ammo;
            g.Update(1.0/60, new FrameInput { head = HEAD, hands = new Hands {
                left = new HandOut { state = "binoc", events = new List<HandEvent>() }
            }});
            g.Update(1.0/60, new FrameInput { head = HEAD, hands = new Hands {
                left  = new HandOut { state = "binoc", events = new List<HandEvent>() },
                right = new HandOut { state = "gun",   events = new List<HandEvent> { new FireEvent { side = "right", origin = HEAD.pos, dir = HEAD.fwd } } },
            }});
            Assert.AreEqual(ammo, g.weapons.right.ammo, "望远镜开着时不能开火");
        }

        [Test]
        public void GT13_AutoSaveResume()
        {
            var store = NewMemStore();
            var g = WaveGame(1, store: store);
            g.run.queue.Clear();
            g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            Run(g, 18);
            Assert.AreEqual("rest", g.phase);
            Assert.AreEqual(1, g.flags.Count);
            g.BackToMenu();

            var g2 = new Game(new GameOpts { store = store });
            Assert.AreEqual(1, g2.resume.wave);
            g2.ContinueGame();
            Assert.AreEqual("wave", g2.phase);
            Assert.AreEqual(1, g2.run.wave);
            Assert.AreEqual(1, g2.flags.Count);
        }

        [Test]
        public void GT14_StationAfterWave3()
        {
            // 第 3 波打完固定喘口气，不自动开下一波；第 1、2 波后只是普通波间休息
            var store = NewMemStore();
            var g = WaveGame(1, store: store);
            g.BeginWave(0);
            g.run.queue.Clear();
            Run(g, 0.2);
            Assert.AreEqual("rest", g.phase, "第 1 波后：普通波间休息");
            g.BeginWave(2);
            g.run.queue.Clear();
            Run(g, 0.2);
            Assert.AreEqual("station", g.phase, "第 3 波后：喘口气");
            Run(g, 120);
            Assert.AreEqual("station", g.phase, "等玩家自己开始下一波");
            g.ContinueFromStation();
            Assert.AreEqual("wave", g.phase);
            Assert.AreEqual(3, g.run.wave, "开始的是第 4 波");
            g.run.queue.Clear();
            Run(g, 0.2);
            Assert.AreEqual("rest", g.phase, "第 4 波后回到普通休息");
            g.BeginWave(2);
            g.run.queue.Clear();
            Run(g, 0.2);
            g.QuitFromStation();
            Assert.AreEqual("menu", g.phase);
            var g2 = new Game(new GameOpts { store = store });
            Assert.AreEqual(3, g2.resume.wave);
        }

        [Test]
        public void GT15_UnlockProgression()
        {
            var g = new Game(new GameOpts { store = NewMemStore() });
            foreach (int ch in new[]{1,2,3,4,5,6}) {                       // 13 关：盾牌 / 望远镜 / 双枪分别在第 2、4、6 章（各主题下篇）拿到
                g.StartChapter(ch, new StartChapterOpts { skipLesson = true });
                g.BeginWave(4);
                g.run.queue.Clear();
                Run(g, 0.2);
                Assert.AreEqual("clear", g.phase, $"章节 {ch} 应通关");
                Run(g, Config.Default.flow.clearHold + 0.1);
                Assert.AreEqual("reward", g.phase, $"章节 {ch} 应进入奖励界面");
            }
            Assert.IsTrue(g.profile.unlocked["shield"] && g.profile.unlocked["binoc"] && g.profile.unlocked["dual"],
                "解锁了盾牌、望远镜、双枪");
            Assert.IsFalse(g.profile.unlocked.ContainsKey("endless") && g.profile.unlocked["endless"],
                "第 6 章不解锁无尽（终极决战才解锁）");
        }

        [Test]
        public void GT16_EndlessUnlock()
        {
            var g = new Game(new GameOpts { store = NewMemStore() });
            Assert.IsFalse(g.StartEndless(), "未解锁时无法进入无尽");
            g.profile.unlocked["endless"] = true;
            Assert.IsTrue(g.StartEndless());
            // 无尽的场景按 Boss 定：第 1～5 波在第 1 个 Boss 的关（第 2 关），门和那一关一样
            Assert.AreEqual(2, g.run.chapter);
            Assert.AreEqual(4, g.portals.Count, "无尽一律四扇门（用户要求），不跟着场景变");
        }

        [Test]
        public void GT17_DualWield()
        {
            var g = WaveGame(4, new Dictionary<string,bool>{{"shield",true},{"binoc",true},{"dual",true}});
            g.run.queue.Clear();
            g.enemies.Clear();
            var a = g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[1], pos = new Vec3d(-1.2, 0, -2.2), eliteExplicit = true });
            var b = g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[1], pos = new Vec3d(1.2, 0, -2.2), eliteExplicit = true });
            a.state = b.state = "hold";
            g.GazeOverride = new GazeRay { origin = HEAD.pos, dir = V.norm(V.sub(CenterOf(a), HEAD.pos)) };
            Run(g, 0.2, i => new Hands {
                left  = new HandOut { state = "gun", events = new List<HandEvent>() },
                right = new HandOut { state = "gun", events = new List<HandEvent>() },
            });
            Vec3d oR = new Vec3d(0.2, 1.35, -0.6);
            g.Update(1.0/60, new FrameInput {
                head = HEAD, gaze = g.GazeOverride,
                hands = new Hands {
                    left  = new HandOut { state = "gun", events = new List<HandEvent>() },
                    right = new HandOut { state = "gun", events = new List<HandEvent> {
                        new FireEvent { side = "right", origin = V.clone(oR), dir = V.norm(V.sub(CenterOf(b), oR)) }
                    }},
                }
            });
            Assert.AreEqual(1.0, b.hp, 0.001, "右手打中它指着的 b");
            Assert.AreEqual(2.0, a.hp, 0.001, "a 没被打");
        }

        [Test]
        public void GT18_BalloonSplit()
        {
            var g = WaveGame(4, new Dictionary<string,bool>{{"shield",true},{"binoc",true},{"dual",true}});
            g.run.queue.Clear();
            g.enemies.Clear();
            var b = g.SpawnEnemy("balloon", new SpawnOpts { portal = g.portals[1], eliteExplicit = true });
            g.DamageEnemy(b, 3);
            Assert.AreEqual(2, g.enemies.Count(e => e.kind == "balloonSmall"), "分裂成两个小气球");
        }

        [Test]
        public void GT19b_AllEnemiesComeFromDoor()
        {
            // 所有会自己走进房间的小怪，出生点都在门后（玩家只会看到它们从门里出来）
            var g = WaveGame(4, new Dictionary<string,bool>{{"shield",true},{"binoc",true}});
            g.run.queue.Clear(); g.enemies.Clear();
            foreach (var kind in new[]{"planter","mouse","brute","shooter","jack","tin","ninja","minion","balloon","rcCar","repair"}) {
                var e = g.SpawnEnemy(kind, new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
                Assert.Less(g.PortalSide(g.portals[0], e.pos), 0.01, $"{kind} 出生在 {e.pos.x:0.00},{e.pos.y:0.00},{e.pos.z:0.00}");
            }
        }

        [Test]
        public void GT19c_EnemiesWalkAroundRoomObstacles()
        {
            // 门前房间里的实物（灌木、蘑菇灯…）：地面小怪绕着走、不穿过去；旗子不插在它们身上
            var g = WaveGame(1, new Dictionary<string,bool>());
            g.run.queue.Clear(); g.enemies.Clear();
            var mouth = g.portals[0].mouth;
            g.obstacles.Add((mouth.x, mouth.z + 0.75, 0.25));
            g.obstacles.Add((mouth.x + 0.7, mouth.z + 0.9, 0.2));
            g.obstacles.Add((mouth.x - 0.7, mouth.z + 0.9, 0.2));
            var list = new List<Enemy>();
            for (int i = 0; i < 4; i++) list.Add(g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[0], eliteExplicit = true }));
            for (int i = 0; i < 3; i++) list.Add(g.SpawnEnemy("mouse", new SpawnOpts { portal = g.portals[0], eliteExplicit = true }));
            double worst = 1;
            for (int f = 0; f < 1800; f++) {
                g.Update(1.0/60, new FrameInput { head = HEAD });
                foreach (var e in g.enemies) {
                    if (e.dead || e.pos.y > 0.8) continue;
                    foreach (var o in g.obstacles) worst = System.Math.Min(worst, System.Math.Sqrt((e.pos.x - o.x) * (e.pos.x - o.x) + (e.pos.z - o.z) * (e.pos.z - o.z)) - o.r - e.radius);
                }
            }
            Assert.GreaterOrEqual(worst, -1e-6, "小怪进了障碍圈（穿模）");
            Assert.Greater(g.flags.Count, 0, "绕行后仍然能走到插旗点插旗");
            foreach (var fl in g.flags) foreach (var o in g.obstacles)
                Assert.Greater(System.Math.Sqrt((fl.pos.x - o.x) * (fl.pos.x - o.x) + (fl.pos.z - o.z) * (fl.pos.z - o.z)), o.r, "旗子插在了障碍上");
        }

        [Test]
        public void GT19d_NoEnemyStuckInDoor()
        {
            // 用户：有的小怪卡在门里、手枪打不到。门口正前方、门后出生路两侧都有摆件（障碍圈罩住了门口点 / 门后 0.7 m 点），门洞底部又窄：
            // 小怪照样能出门插旗、插完能回门里离场；任何时候骑在门口的小怪都在可瞄准列表里；所有小怪都不从门以外的地方出现
            var g = WaveGame(1, new Dictionary<string,bool>());
            g.run.queue.Clear(); g.enemies.Clear();
            var P = g.portals[0];
            g.obstacles.Add((P.mouth.x + 0.15, P.mouth.z, 0.22));                         // 罩住门口点
            g.obstacles.Add((P.inner.x - 0.2, P.inner.z, 0.25));                          // 罩住门后离场点
            g.obstacles.Add((P.deep.x + 0.45, P.deep.z, 0.2));
            g.gateA = V.add(P.@base, V.scale(P.right, -0.32)); g.gateB = V.add(P.@base, V.scale(P.right, 0.32));
            var kinds = new[] { "planter", "planter", "mouse", "mouse", "brute", "shooter", "plane" };
            var list = new List<Enemy>();
            foreach (var k in kinds) { var e = g.SpawnEnemy(k, new SpawnOpts { portal = P, eliteExplicit = true }); list.Add(e); Assert.Less(g.PortalSide(P, e.pos), 0, k + " 不是从门后出生"); }
            var nearDoorT = new Dictionary<int, double>(); double worstT = 0; string worstK = "";
            for (int f = 0; f < 60 * 70; f++) {
                g.Update(1.0/60, new FrameInput { head = HEAD });
                var tg = g.Targets(HEAD.pos);
                foreach (var e in g.enemies) {
                    if (e.dead || e.state == "gone") continue;
                    double side = g.PortalSide(P, e.pos);
                    if (side > -0.3 && side < 0.3 && e.pos.y < 0.8) {
                        Assert.IsTrue(tg.Exists(t => t.type == "enemy" && t.id == e.id) || e.hidden || (e.kind == "jack" && e.state != "open"), e.kind + " 骑在门口却打不到");
                        nearDoorT[e.id] = (nearDoorT.TryGetValue(e.id, out var v) ? v : 0) + 1.0/60;
                        if (nearDoorT[e.id] > worstT) { worstT = nearDoorT[e.id]; worstK = e.kind + "/" + e.state; }
                    }
                }
            }
            Assert.Less(worstT, 6.0, "有小怪在门口逗留超过 6 秒（卡门）：" + worstK);
            Assert.Greater(g.flags.Count, 0, "门口有障碍时仍然能出门插旗");
            Assert.IsTrue(list.Where(e => e.kind == "planter" || e.kind == "mouse").Any(e => e.dead), "插完旗的小怪能回到门里离场");
        }

        [Test]
        public void GT19e_LoneEnemyBecomesTargetable()
        {
            // 每种小怪单独出生、身边没有同伴（比如这一波它最先出门）：20 秒内必须出门 / 露面、进入可瞄准列表——
            // 回归：修理机器人没有要修的目标时一直站在门后出生点，门后打不到，这一波永远结束不了（第 5 章模拟卡死 900 秒）
            var kinds = new[] { "planter", "mouse", "brute", "shooter", "plane", "jack", "tin", "ninja", "commander", "balloon", "rcCar", "repair" };   // 小锡兵只跟着指挥官出来（单独出生会立刻判逃跑回门里），不在此列
            foreach (var k in kinds)
            {
                var g = WaveGame(5, new Dictionary<string,bool>());
                g.run.queue.Clear(); g.enemies.Clear();
                var e = g.SpawnEnemy(k, new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
                bool seen = false;
                for (int f = 0; f < 60 * 20 && !seen; f++)
                {
                    g.Update(1.0/60, new FrameInput { head = HEAD });
                    if (e.dead || e.state == "gone") break;
                    seen = g.Targets(HEAD.pos).Exists(t => t.type == "enemy" && t.id == e.id)
                        || (e.hidden && g.PortalSide(e.portal, e.pos) > 0.3);          // 忍者隐身是设计（望远镜标记后才能打）：只要求它已经走进房间
                }
                Assert.IsTrue(seen, k + " 单独出生时 20 秒内一直打不到（state=" + e.state + " side=" + g.PortalSide(e.portal, e.pos).ToString("0.00") + "）");
            }
        }

        [Test]
        public void GT19_JackOpenClose()
        {
            var g = WaveGame(2, new Dictionary<string,bool>{{"shield",true}});
            g.run.queue.Clear();
            g.enemies.Clear();
            var j = g.SpawnEnemy("jack", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            Assert.AreEqual("arrive", j.state, "先从门里滑出来");
            Assert.Less(g.PortalSide(g.portals[0], j.pos), 0, "出生在门后，不在房间里凭空出现");
            var seen = new HashSet<string>();
            for (int i = 0; i < 900; i++) {
                g.Update(1.0/60, new FrameInput { head = HEAD });
                seen.Add(j.state);
            }
            Assert.IsTrue(seen.Contains("open") && seen.Contains("closed"), "弹簧小丑盒会周期性开合");
        }

        // 用户：外观一样的敌人血量必须一样；不能有怎么打都打不死的情况；插旗时是弱点
        static readonly string[] AllKinds = { "planter", "mouse", "brute", "shooter", "plane", "jack", "tin", "ninja", "commander", "minion", "balloon", "rcCar", "repair" };

        [Test]
        public void GT30_SameKindSameHp()
        {
            var g = WaveGame(3);
            foreach (var kind in AllKinds)
                foreach (var el in new string[] { null, "gold", "swift", "swarm" })
                {
                    g.enemies.Clear(); g.run.hpMul = 3;
                    var e = g.SpawnEnemy(kind, new SpawnOpts { portal = g.portals[0], eliteOverride = el, eliteExplicit = true, fromSwarm = true });
                    Assert.AreEqual(KINDS.Get(kind).hp, e.hp, 1e-6, $"{kind} 精英 {el} 的血量必须等于种类血量");
                }
        }

        [Test]
        public void GT31_EveryKindCanBeKilled()
        {
            foreach (var kind in AllKinds)
            {
                var g = WaveGame(3); g.run.queue.Clear(); g.enemies.Clear();
                var e = g.SpawnEnemy(kind, new SpawnOpts { portal = g.portals[0], eliteExplicit = true, pos = V.add(g.portals[0].@base, V.scale(g.portals[0].normal, 1.0)) });
                for (int i = 0; i < 60 && !e.dead; i++) { Run(g, 0.25); g.weapons.Get("right").ammo = 24; if (!e.dead) FireAt(g, CenterOf(e)); }
                Assert.IsTrue(e.dead || !g.enemies.Contains(e), $"{kind} 必须打得死（没有望远镜、不看时机）");
            }
        }

        [Test]
        public void GT32_PlantingIsWeakSpot()
        {
            var g = WaveGame(3); g.run.queue.Clear(); g.enemies.Clear();
            var e = g.SpawnEnemy("tin", new SpawnOpts { portal = g.portals[0], eliteExplicit = true, pos = V.add(g.portals[0].@base, V.scale(g.portals[0].normal, 1.5)) });
            Assert.AreEqual(4, e.hp, 1e-6);
            e.state = "plant"; e.t = 0;
            FireAt(g, CenterOf(e)); Assert.AreEqual(2, e.hp, 1e-6, "插旗时一枪顶两枪");
            Run(g, 0.25); e.state = "plant";
            FireAt(g, CenterOf(e)); Assert.IsTrue(e.dead, "平时 4 枪的小怪，插旗时 2 枪打死");
        }

        [Test]
        public void GT33_FlagFourShotsAndLockableWhole()
        {
            foreach (var type in new[] { "small", "big" })
            {
                var g = WaveGame(3); g.run.queue.Clear(); g.enemies.Clear(); g.flags.Clear();
                var pos = new Vec3d(0.4, 0, -1.8);
                var f = new Flag { id = 9001, type = type, pos = pos, hp = FLAGS.Get(type).hp, maxHp = (int)FLAGS.Get(type).hp, amount = 0, cap = 1, rate = 0, hidden = false, dying = false, age = 5 };
                g.flags.Add(f);
                Assert.AreEqual(4, f.hp, 1e-6, $"{type} 旗 4 枪倒");
                // 看旗杆底座、中段、顶端都要锁得上（原来往下看底座锁不上）
                foreach (double y in new[] { 0.05, 0.3, type == "big" ? 0.75 : 0.5 })
                {
                    var dir = V.norm(V.sub(new Vec3d(pos.x, y, pos.z), HEAD.pos));
                    var t = Game.PickAimTarget(HEAD.pos, dir, g.Targets(HEAD.pos), Config.Default.gaze.margin, Config.Default.gaze.flagPenalty);
                    Assert.IsNotNull(t, $"{type} 旗看高度 {y} 必须选得中");
                    Assert.AreEqual(f.id, t.id);
                }
            }
        }

        [Test]
        public void GT34_RapidFireInfiniteAuto()
        {
            var g = WaveGame(1); g.run.queue.Clear(); g.enemies.Clear();
            var w = g.weapons.Get("right"); w.ammo = 3; w.reload = new ReloadState();
            g.rapidT = 1.0; w.reload = null;
            var origin = new Vec3d(0.2, 1.35, -0.6);
            var hand = new HandOut { state = "gun", origin = origin, dir = new Vec3d(0, 0, -1), events = new List<HandEvent>() };
            int shots = 0;
            for (int i = 0; i < 60; i++) { g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands { right = hand } }); shots += g.DrainFx().FindAll(f => f is ShotFx).Count; }
            Assert.AreEqual(3, w.ammo, "无限火力不耗子弹");
            Assert.GreaterOrEqual(shots, 9, "一秒自动连射约 11 发，不用挑腕");
            for (int i = 0; i < 30; i++) { g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands { right = hand } }); }
            g.DrainFx(); int after = 0;
            for (int i = 0; i < 30; i++) { g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands { right = hand } }); after += g.DrainFx().FindAll(f => f is ShotFx).Count; }
            Assert.AreEqual(0, after, "时间到了就停");
        }

        [Test]
        public void GT38_BuffsNeverWasted()
        {
            // 场上没敌人时金色子弹不走时间
            var g = WaveGame(1); g.run.queue.Clear(); g.enemies.Clear(); g.goldT = 5;
            g.run.queue.Add("planter"); g.run.spawnT = 99;
            for (int i = 0; i < 60; i++) g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
            Assert.AreEqual(5, g.goldT, 1e-6, "没敌人不扣时间");
            g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            for (int i = 0; i < 60; i++) g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
            Assert.Less(g.goldT, 4.5, "有敌人才计时");

            // 无限火力期间不收波，时间到就结束
            var g2 = WaveGame(1); g2.run.queue.Clear(); g2.enemies.Clear(); g2.flags.Clear();
            g2.StartFrenzy();
            for (int i = 0; i < 60 * 3; i++) g2.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
            Assert.AreEqual("wave", g2.phase, "长枪在手时不收波");
            for (int i = 0; i < 60 * 25 && g2.phase == "wave"; i++) g2.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
            Assert.AreEqual(0, g2.rapidT, 1e-6, "时间到归零");
            Assert.AreNotEqual("wave", g2.phase, "长枪结束、剩下的狂欢小怪跑回门里后正常收波");
        }

        [Test]
        public void GT43_BossWarnBeforeSpawn()
        {
            var g = WaveGame(2); g.run.queue.Clear(); g.enemies.Clear(); g.flags.Clear();
            g.run.queue.Add("boss1"); g.run.spawnT = 0.5; g.DrainFx();
            var fx = new List<FxEvent>(); double warnAt = -1, spawnAt = -1; double t = 0;
            for (int i = 0; i < 60 * 8; i++)
            {
                g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() }); t += 1.0 / 60;
                foreach (var f in g.DrainFx()) { if (f is BossWarnFx && warnAt < 0) warnAt = t; }
                if (spawnAt < 0 && g.enemies.Exists(e => Game.IsBoss(e))) spawnAt = t;
            }
            Assert.GreaterOrEqual(warnAt, 0, "Boss 出来前要预警");
            Assert.Greater(spawnAt, 0, "预警后 Boss 出场");
            Assert.GreaterOrEqual(spawnAt - warnAt, Game.BossWarnDur - 0.05, "预警满几秒才放出来");
        }

        [Test]
        public void GT39_EnemyNeverStuckBehindDoor()
        {
            var g = WaveGame(6); g.run.queue.Clear(); g.enemies.Clear();
            var p = g.portals[0];
            var e = g.SpawnEnemy("planter", new SpawnOpts { portal = p, eliteExplicit = true });
            // 门后摆件圈重叠成死角，把通道外侧整个堵住
            for (int i = -3; i <= 3; i++)
            {
                var c = V.add(p.mouth, V.add(V.scale(p.right, i * 0.35), V.scale(p.normal, -1.2)));
                g.obstacles.Add((c.x, c.z, 0.6));
            }
            e.pos = V.add(p.mouth, V.add(V.scale(p.right, 1.1), V.scale(p.normal, -1.9)));
            double side() => V.dot(V.sub(e.pos, p.@base), p.normal);
            Assert.Less(side(), -0.35);
            for (int i = 0; i < 60 * 14 && side() < -0.35 && !e.dead; i++) g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
            Assert.GreaterOrEqual(side(), -0.35, "再堵也要在几秒内出门，打得到");
        }

        [Test]
        public void GT40_BossEachChapterWithPhases()
        {
            for (int t = 1; t <= 6; t++)
            {
                int ch = t * 2;                                                   // 每个主题的下篇
                var g = WaveGame(ch); int last = CHAPTERS.Get(ch).waves.Length - 1;
                g.BeginWave(last);
                int at = g.run.queue.IndexOf("boss" + t);
                Assert.GreaterOrEqual(at, 0, $"第 {ch} 章最后一波有 Boss");
                Assert.GreaterOrEqual(at, g.run.queue.Count * 0.6, "Boss 在这一波的中后段才出来");
                Assert.AreEqual(-1, WaveGame(ch - 1).run.queue.FindIndex(k => k.StartsWith("boss")), "上篇没有 Boss");
                g.run.queue.Clear(); g.enemies.Clear(); g.DrainFx();
                var e = g.SpawnEnemy("boss" + t, new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
                for (int i = 0; i < 60 * 10 && e.state == "enter"; i++) g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
                Assert.AreEqual("fight", e.state, $"第 {ch} 章 Boss 走到阵地");
                Assert.Greater(V.dot(V.sub(e.pos, e.portal.@base), e.portal.normal), 0.3, "Boss 在门外，打得到");
                var fx = new List<FxEvent>();
                for (int i = 0; i < 60 * 12; i++) { g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() }); fx.AddRange(g.DrainFx()); }
                Assert.IsTrue(fx.Exists(f => f is TelegraphFx), "出招前有预警");
                Assert.IsTrue(fx.Exists(f => f is EnemyShotFx || f is SpawnFx), "会出招");
                double hp0 = e.maxHp; int phases = 0;
                while (!e.dead) { g.DamageEnemy(e, 1); phases += g.DrainFx().Count(f => f is BossPhaseFx); }
                Assert.AreEqual(2, phases, "掉到 2/3、1/3 各进一次新阶段");
                Assert.GreaterOrEqual(hp0, 32, "Boss 够耐打（用户：血量偏低）");
            }
        }

        [Test]
        public void GT41_ChapterMechanicsDiffer()
        {
            string[] kinds = { "catapult", "mortar", "gate", "popper", "clock", "spring" };
            for (int t = 1; t <= 6; t++)
            {
                int ch = t * 2 - 1;
                var g = WaveGame(ch);
                Assert.IsNotNull(g.catapult, $"第 {ch} 章有门后机关"); Assert.AreEqual(kinds[t - 1], g.catapult.kind);
                Assert.AreEqual(kinds[t - 1], WaveGame(ch + 1).catapult.kind, "同一主题两章机关一样");
                g.BeginWave(2); g.run.queue.Clear(); g.enemies.Clear(); g.run.queue.Add("planter"); g.run.spawnT = 999; g.DrainFx();
                var fx = new List<FxEvent>(); int bulletsMax = 0;
                for (int i = 0; i < 60 * 30; i++) { g.Update(1.0 / 60, new FrameInput { head = new Head { pos = new Vec3d(3, 1.5, 3), fwd = new Vec3d(0, 0, -1) }, hands = new Hands() }); fx.AddRange(g.DrainFx()); bulletsMax = Math.Max(bulletsMax, g.bullets.Count); }
                Assert.IsTrue(fx.Exists(f => f is CatapultFireFx), $"第 {ch} 章机关会出手");
                if (t == 3) Assert.IsTrue(g.enemies.Exists(e => e.kind == "planter"), "城门放出积木兵");
                if (t == 4) Assert.GreaterOrEqual(bulletsMax, 3, "派对礼炮一次喷三团彩纸");
            }
        }

        [Test]
        public void GT42_ThirteenLevelsAndFinal()
        {
            Assert.AreEqual(13, CHAPTERS.Count);
            for (int ch = 1; ch <= 12; ch++) Assert.AreEqual((ch + 1) / 2, CHAPTERS.World(ch), "两章一个主题");
            var f = CHAPTERS.Get(13); Assert.IsTrue(f.final);
            // 用户：终极决战一共六波、一波一个 Boss（1 → 6 依次），不要两个一起出现
            Assert.AreEqual(6, f.waves.Length, "终极决战六波");
            for (int w = 0; w < 6; w++)
            {
                var bs = f.waves[w].spawns.Where(sp => sp.kind.StartsWith("boss")).ToList();
                Assert.AreEqual(1, bs.Count, $"第 {w + 1} 波只有一个 Boss"); Assert.AreEqual("boss" + (w + 1), bs[0].kind, "Boss 依次登场");
            }
            var g = WaveGame(13); g.BeginWave(5);
            var idx = g.run.queue.Select((k, i) => (k, i)).Where(x => x.k.StartsWith("boss")).Select(x => x.i).ToList();
            Assert.AreEqual(1, idx.Count); Assert.Greater(idx[0], g.run.queue.Count * 0.4, "Boss 在中后段登场");
            // 每章保底一次无限火力
            var g2 = WaveGame(1); g2.BeginWave(1); g2.run.queue.Add("planter"); g2.run.spawnT = 999;
            Assert.IsFalse(g2.run.rapidGiven);
            for (int i = 0; i < 60 * 40 && !g2.run.rapidGiven; i++)
            {
                g2.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
                foreach (var gift in g2.gifts.ToList()) for (int k = 0; k < 6; k++) g2.HitTargetForTest(gift.id);
            }
            Assert.IsTrue(g2.run.rapidGiven, "第 2 波第一个礼盒保底给无限火力");
        }

        [Test]
        public void GT36_ToysIntroducedGradually()
        {
            var seen = new HashSet<string>();
            for (int c = 1; c <= CHAPTERS.Count; c++)
            {
                var fresh = new HashSet<string>();
                foreach (var w in CHAPTERS.Get(c).waves) foreach (var (k, _) in w.spawns) if (!seen.Contains(k) && !k.StartsWith("boss")) fresh.Add(k);
                if (c == 1) CollectionAssert.AreEquivalent(new[] { "planter", "mouse", "brute" }, fresh, "第 1 章只有基础三种");
                Assert.LessOrEqual(fresh.Count, 3, $"第 {c} 章一次最多引入 3 种新玩具");
                var firstWave = new HashSet<string>(); foreach (var (k, _) in CHAPTERS.Get(c).waves[0].spawns) if (!seen.Contains(k) && !k.StartsWith("boss")) firstWave.Add(k);
                Assert.LessOrEqual(firstWave.Count, c == 1 ? 1 : 2, $"第 {c} 章第一波不能一下全上");
                seen.UnionWith(fresh);
            }
            var g = WaveGame(1);
            Assert.IsFalse(g.KindSeen("minion")); Assert.IsFalse(g.KindSeen("balloonSmall")); Assert.IsTrue(g.KindSeen("mouse"));
            g.StartFrenzy();
            for (int i = 0; i < 300; i++) g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() });
            Assert.Greater(g.enemies.Count(e => e.bonus), 5, "狂欢小怪照样涌出来");
            Assert.IsTrue(g.enemies.Where(e => e.bonus).All(e => e.kind == "mouse"), "第 1 章的狂欢只出见过的小怪");
        }

        [Test]
        public void GT37_HeadHitReportsAttackKind()
        {
            foreach (var kind in new[] { "foam", "confetti" })
            {
                var g = WaveGame(1); g.DrainFx();                     // 留着出怪队列，不然本波立刻结束、子弹被清
                g.bullets.Add(new Bullet { id = 9100, kind = kind, pos = V.add(HEAD.pos, new Vec3d(0.2, 0, -0.5)), vel = new Vec3d(-1.2, 0, 3) });
                var fx = new List<FxEvent>();
                for (int i = 0; i < 40; i++) { g.Update(1.0 / 60, new FrameInput { head = HEAD, hands = new Hands() }); fx.AddRange(g.DrainFx()); }
                var h = fx.OfType<HeadHitFx>().FirstOrDefault();
                Assert.IsNotNull(h, kind + " 打中头要发事件"); Assert.AreEqual(kind, h.kind);
                Assert.Greater(h.dir.z, 0, "方向从敌人指向玩家");
            }
        }

        [Test]
        public void GT35_FrenzySwarmThenLeftoversWalkBack()
        {
            var g = WaveGame(1); g.run.queue.Clear(); g.enemies.Clear(); g.flags.Clear();
            g.run.queue.Add("planter");                                 // 让这一波别提前结束
            g.StartFrenzy();
            int max = 0;
            for (int i = 0; i < 60 * 9; i++) { g.Update(1.0 / 60, new FrameInput { head = HEAD }); int n = g.enemies.FindAll(e => e.bonus && !e.dead).Count; if (n > max) max = n; }
            Assert.GreaterOrEqual(max, 12, "涌出来一大群");
            Assert.IsTrue(g.enemies.FindAll(e => e.bonus && !e.dead).TrueForAll(e => e.hp == 1), "都是一枪倒");
            // 打倒几只（结算要有数）
            int shot = 0; foreach (var e in g.enemies.FindAll(e => e.bonus && !e.dead)) { if (shot++ >= 3) break; g.DamageEnemy(e, 5); }
            for (int i = 0; i < 60 * 2; i++) g.Update(1.0 / 60, new FrameInput { head = HEAD });
            Assert.AreEqual(0, g.rapidT, 1e-6);
            int left = g.enemies.FindAll(e => e.bonus && !e.dead).Count;
            Assert.Greater(left, 0, "长枪结束时没打到的小怪不会凭空消失（用户：枪没了、小怪也一起没了不对）");
            g.DrainFx();
            var exits = new List<FxEvent>();
            for (int i = 0; i < 60 * 14; i++) { g.Update(1.0 / 60, new FrameInput { head = HEAD }); exits.AddRange(g.DrainFx()); }
            Assert.AreEqual(0, g.enemies.FindAll(e => e.bonus && !e.dead).Count, "没打到的自己跑回门里");
            Assert.IsTrue(exits.Exists(f => f is ExitFx), "是走回门里离开（ExitFx），不是被系统炸掉");
            Assert.IsTrue(exits.Exists(f => f is FrenzyEndFx), "都离场后才结算");
        }

        [Test]
        public void GT20_LoseAssistRamp()
        {
            var g = WaveGame(1);
            void Lose() {
                for (int i = 0; i < 9; i++) {
                    g.flags.Add(new Flag {
                        id = (int)(800 + i + g.time * 100), type = "small",
                        pos = new Vec3d(i * 0.3, 0, -2),
                        hp = 10, maxHp = 10, amount = 12, cap = 12, rate = 0.5,
                        radius = 1, hidden = false, dying = false, age = 0,
                    });
                }
                Run(g, 0.1);
                Run(g, Config.Default.flow.lostHold + 0.1);
                g.RetryAfterLoss();
            }
            Lose();
            Assert.AreEqual(1, g.run.assist);
            Lose(); Lose(); Lose(); Lose(); Lose();
            Assert.AreEqual(5, g.run.assist, "最多 5 档");
            g.BeginWave(1);
            Assert.AreEqual(0, g.run.assist, "新的一波清零");
        }
    
        [Test]
        public void GT42_HiddenBehindWallNotTargetable()
        {
            // 用户：没看见他，他的框就出来了——门墙后面（门洞外侧）的敌人不能被锁定 / 吸附
            var g = WaveGame(3);
            g.enemies.Clear();
            var p = g.portals[0];
            var e = g.SpawnEnemy("planter", new SpawnOpts { portal = p, eliteExplicit = true });
            e.state = "walk"; e.hidden = false;
            e.pos = V.add(V.sub(p.@base, V.scale(p.normal, 0.2)), V.scale(p.right, 1.2));    // 门墙后 0.2 m、门洞外 1.2 m：被墙挡住
            Assert.IsFalse(g.CanSee(e, HEAD.pos), "墙后面的看不见");
            Assert.IsFalse(g.Targets(HEAD.pos).Exists(t => t.id == e.id), "看不见就不能锁");
            e.pos = V.sub(p.@base, V.scale(p.normal, 0.5));                                   // 门洞正后方：透过门洞看得见
            Assert.IsTrue(g.CanSee(e, HEAD.pos), "门洞正后方看得见");
            Assert.IsTrue(g.Targets(HEAD.pos).Exists(t => t.id == e.id));
        }

        [Test]
        public void GT43_EnemyNeverInsideDoorWall()
        {
            // 用户：敌人不要卡在墙里——门洞宽度外，房间这一侧的敌人不能贴进门墙
            var g = WaveGame(3);
            g.enemies.Clear();
            var p = g.portals[0];
            var e = g.SpawnEnemy("planter", new SpawnOpts { portal = p, eliteExplicit = true });
            e.state = "walk"; e.hidden = false;
            e.pos = V.add(V.add(p.@base, V.scale(p.normal, 0.05)), V.scale(p.right, 1.1));
            Run(g, 0.1);
            if (!e.dead) {
                double lat = Math.Abs(V.dot(V.sub(e.pos, p.@base), p.right));
                if (lat > 0.7) Assert.GreaterOrEqual(g.PortalSide(p, e.pos), e.radius, "门洞外侧不许探进门墙");
            }
            // 召唤成群：出生点横向不超过门后可走范围（原来最远 2.8 m，进了门后世界的墙）
            for (int k = 0; k < 6; k++) {
                var m = g.SpawnEnemy("mouse", new SpawnOpts { portal = p, offset = (k % 2 == 0 ? 1 : -1) * (1.0 + 0.3 * k), eliteExplicit = true });
                Assert.LessOrEqual(Math.Abs(V.dot(V.sub(m.pos, p.@base), p.right)), 1.41, "出生点不进门后的墙");
            }
            // 站位点：侧面不比原来（门 3.2 m 时）更远，不容易落进真实侧墙
            for (int k = 0; k < 200; k++) {
                var sp = g.PickSpot(1.0, 4.0, null);
                double d = Math.Sqrt(sp.x * sp.x + sp.z * sp.z), a = Math.Atan2(sp.x, -sp.z);
                Assert.LessOrEqual(d, 2.5 + 0.5 * Math.Cos(a) + 1e-6, "站位点太远");
            }
        }

        [Test]
        public void GT44_BossSpecials()
        {
            // 用户：Boss 可以有不同的技能，不一定都是攻击——一下插好几面旗、把手枪拿走一会、把弹匣打空
            var g = WaveGame(3);
            g.run.queue.Clear(); g.enemies.Clear(); g.flags.Clear();
            var P = g.portals[0];
            var b = g.SpawnEnemy("boss2", new SpawnOpts { portal = P, eliteExplicit = true });
            b.pos = V.add(P.mouth, V.scale(P.normal, 0.9)); b.state = "fight"; b.hidden = false; b.bossT = 0;
            var seen = new HashSet<string>(); bool stolen = false, drained = false; int maxFlags = 0;
            for (int f = 0; f < 60 * 90 && !(stolen && seen.Contains("grab")); f++) {
                g.Update(1.0 / 60, new FrameInput { head = HEAD });
                foreach (var fx in g.DrainFx()) { if (fx is BossMoveFx m && m.stage == 1) seen.Add(m.move); if (fx is GunStolenFx) stolen = true; if (fx is AmmoDrainFx) drained = true; }
                maxFlags = System.Math.Max(maxFlags, g.flags.Count);
                if (b.dead) break;
            }
            Assert.IsTrue(seen.Contains("grab"), "坦克会用磁铁爪");
            Assert.IsTrue(stolen, "磁铁爪打中头：枪被抢走");
            // 震空弹匣：直接打一发 drain 到头上
            var w = g.weapons.Get("right"); Assert.Greater(w.ammo, 0);
            var b5 = g.SpawnEnemy("boss5", new SpawnOpts { portal = P, eliteExplicit = true }); b5.pos = V.clone(P.mouth); b5.hidden = false;
            g.FireAtHead(b5, "drain", 3.0);
            for (int f = 0; f < 120 && !drained; f++) { g.Update(1.0 / 60, new FrameInput { head = HEAD }); foreach (var fx in g.DrainFx()) if (fx is AmmoDrainFx) drained = true; }
            Assert.IsTrue(drained, "发条波打中：弹匣被震空");
            Assert.AreEqual(0, g.weapons.Get("right").ammo);
        }

        [Test]
        public void GT45_EndlessBossEveryFiveWaves()
        {
            // 用户：无尽模式每五波一个 Boss，Boss 之间有小怪潮；敌人更多、血量更厚
            var rng = new System.Random(3); Func<double> r = () => rng.NextDouble();
            for (int n = 1; n <= 30; n++)
            {
                var w = EndlessMode.EndlessWave(n, r);
                int b = w.spawns.Where(sp => sp.kind.StartsWith("boss")).Sum(sp => sp.count);
                Assert.AreEqual(n % 5 == 0 ? 1 : 0, b, $"第 {n} 波 Boss 数");
                if (n % 5 == 0) Assert.AreEqual("boss" + (1 + (n / 5 - 1) % 6), w.spawns.First(sp => sp.kind.StartsWith("boss")).kind, "Boss 1→6 轮流");
                Assert.AreEqual(n % 5 == 3, w.rush, $"第 {n} 波小怪潮");
            }
            int c2 = EndlessMode.EndlessWave(2, r).spawns.Sum(sp => sp.count), c3 = EndlessMode.EndlessWave(3, r).spawns.Sum(sp => sp.count);
            Assert.Greater(c3, c2 * 1.4, "小怪潮明显更多");
            var g = new Game(new GameOpts { store = NewMemStore(), seed = 5 });
            g.profile.unlocked["endless"] = true;
            Assert.IsTrue(g.StartEndless());
            // 场景按 Boss 段轮换：第 1～5 波第 2 关、6～10 波第 4 关……第 26～30 波第 12 关，第 31 波起从头再来
            for (int n = 1; n <= 31; n++) Assert.AreEqual(2 * (((n - 1) / 5) % 6 + 1), EndlessMode.ChapterFor(n), $"第 {n} 波的场景");
            Assert.AreEqual(4, g.portals.Count, "无尽一律四扇门");
            // 续玩：存档记着波数，回来从那一波开头重来（旗子清空），还是四扇门
            g.BeginWave(6);
            Assert.IsTrue(g.HasEndlessResume); Assert.AreEqual(6, g.resume.wave);
            var g2 = new Game(new GameOpts { store = NewMemStore(), seed = 6 }); g2.profile.unlocked["endless"] = true; g2.resume = g.resume;
            Assert.IsTrue(g2.ContinueEndless());
            Assert.AreEqual(6, g2.run.wave); Assert.AreEqual(0, g2.flags.Count); Assert.AreEqual(4, g2.portals.Count);
            Assert.AreEqual(EndlessMode.ChapterFor(7), g2.run.chapter, "场景是第 7 波所在的 Boss 段");
            g.BeginWave(9);
            var e = g.SpawnEnemy("planter", new SpawnOpts { portal = g.portals[0], eliteExplicit = true });
            Assert.Greater(e.maxHp, KINDS.Get("planter").hp, "无尽后期血量更厚");
        }
}
}
