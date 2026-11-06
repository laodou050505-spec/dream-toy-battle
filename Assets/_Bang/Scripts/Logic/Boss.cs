// 章末 Boss（每章最后一波登场，分三个阶段）：
//   1 积木巨人：扔积木砖，二阶段起跺脚震出老鼠          2 塑料坦克：连发炮弹，阶段越高连发越多
//   3 锡兵大将军：召唤小锡兵 + 下令冲锋，间隙开一枪        4 气球之王：撒彩纸，每过一个阶段放出一串小气球
//   5 发条巨偶：上发条（这时是弱点）然后连射三颗齿轮      6 玩具箱之王：轮流用前五章的招式
// 通用规则：从门里走出来站到离玩家 2 米多的位置左右踱步；每次出招前有预警（这时是弱点，伤害翻倍）；
// 血量掉到 2/3、1/3 时踉跄一下进下一阶段（出招更快）；二阶段起每隔一段时间插一面大旗。打倒 Boss 前这一波不会结束。
using System;
using System.Collections.Generic;
namespace Bang.Logic
{
    public class BossPhaseFx : FxEvent { public int id, phase; }
    public class BossIntroFx : FxEvent { public int id; public string kind; }
    public class BossMoveFx : FxEvent { public int id; public string move; public int stage; public double dur; }
    public class GunStolenFx : FxEvent { public int id; public double dur; }     // Boss 把玩家的枪抢走了（dur 秒后还回来）
    public class AmmoDrainFx : FxEvent { public int id; }                        // Boss 震空了玩家的弹匣

    public partial class Game
    {
        public static bool IsBoss(Enemy e) => e.k.ai == "boss";

        /// <summary>每章最后一波的 Boss 种类（无尽模式没有）。</summary>
        public static string BossOf(int ch) { int n = CHAPTERS.BossNo(ch); return n > 0 ? "boss" + n : null; }

        void InitBoss(Enemy e)
        {
            e.state = "enter"; e.bossPhase = 1; e.bossT = -2.5; e.bossFlagT = 0;
            Emit(new BossIntroFx { type = "bossIntro", id = e.id, kind = e.kind });
        }

        double BossPeriod(Enemy e) => e.k.fireEvery * (e.bossPhase == 1 ? 0.9 : e.bossPhase == 2 ? 0.72 : 0.58) * (1 + 0.12 * run.assist);   // 11-04：Boss 出招更勤

        private void AiBoss(Enemy e, double dt, double spd)
        {
            var p = e.portal; bool floaty = e.k.move == "float";
            var home = V.add(p.mouth, V.scale(p.normal, 0.9));
            if (e.state == "enter")
            {
                // 入场走快一点：Boss 从门后深处出来，不让玩家干等
                if (WalkVia(e, home, Math.Max(0.6, spd * 2), dt) || V.distXZ(e.pos, home) < 0.08 || (e.stuckN >= 1 && PortalSide(p, e.pos) > 0.5)) { e.state = "fight"; e.t = 0; }   // 出了门被摆件挡住：就地开打
                if (floaty) e.pos.y = Math.Min(0.75, e.pos.y + dt * 0.4);
                return;
            }
            if (e.bossGuardT > 0 && e.state != "stagger") e.bossGuardT -= dt;
            if (e.state == "stagger") { if (e.t >= 1.3) { e.state = "fight"; e.t = 0; e.bossStage = 0; e.bossMove = ""; } return; }
            // 在门口来回巡游，始终面向玩家（用户：Boss 不能傻站着）：横向 ±1.0 米，纵向只往门里退 0.15 米（不往玩家身上凑）；
            // 蓄力时放慢到 30%（看起来是停步蓄力），踉跄 / 插旗时停下
            double lat = Math.Sin(e.life * 0.3) * 1.0, back = -0.15 * (0.5 + 0.5 * Math.Sin(e.life * 0.21));
            var goal = V.add(V.add(home, V.scale(p.right, lat)), V.scale(p.normal, back));
            double pace = e.bossStage == 1 ? 0.3 : e.bossStage == 4 ? 0 : 1;
            if (e.stuckN < 2 && pace > 0) MoveTo(e, goal, Math.Max(spd, 0.3) * pace, dt);
            if (floaty) e.pos.y = 0.75 + Math.Sin(e.life * 1.3) * 0.08;
            FaceHead(e);
            // 二阶段起插大旗：给玩家压力，也逼玩家分心去拔旗
            if (e.bossPhase >= 2)
            {
                e.bossFlagT += dt;
                if (e.bossFlagT >= 20) { e.bossFlagT = 0; var s = PickSpot(cfg.arena.flagMin, cfg.arena.flagMax, p.angle);
                    for (int k = 0; k < 6 && V.distXZ(s, e.pos) < e.radius + 0.8; k++) s = PickSpot(cfg.arena.flagMin, cfg.arena.flagMax, null);   // 离 Boss 远一点
                    PlantFlagAt(e, s, "big"); if (e.bossStage == 0) SetBossStage(e, 4, "flag", 1.6); }
            }
            double per = BossPeriod(e), tele = e.k.telegraph;
            e.bossMoveT += dt;
            if ((e.bossStage == 2 && e.bossVolley.Count == 0 && e.bossMoveT >= 0.9) || (e.bossStage == 4 && e.bossMoveT >= 1.6)) { e.bossStage = 0; e.bossMove = ""; }   // 出招 / 插旗演完：回到巡游
            e.bossT += dt;
            if (!e.tele && e.bossT >= per - tele)
            {
                e.tele = true; Emit(new TelegraphFx { type = "telegraph", id = e.id });
                e.bossSrc = e.kind == "boss6" ? "boss" + (1 + (e.bossCycle % 5)) : e.kind;
                SetBossStage(e, 1, BossNextMove(e), tele);
            }
            if (e.bossT < per) return;
            e.bossT = 0; e.tele = false;
            if (BossSpecialNext(e)) { BossSpecial(e, e.bossMove); if (e.kind == "boss6") e.bossCycle++; }
            else BossAttack(e);
            e.bossSpecK++;
            SetBossStage(e, 2, e.bossMove, 0.9);
        }

        /// <summary>下一招是什么（只预读，不消耗计数）。用户：Boss 要干什么要明确清楚，不能傻站着——HUD 和动作都读这个。</summary>
        string BossNextMove(Enemy e)
        {
            if (BossSpecialNext(e)) return BossSpecialOf(e.bossSrc);
            switch (e.bossSrc)
            {
                case "boss1": return e.bossPhase >= 2 ? "brickStomp" : "brick";
                case "boss2": return "shells";
                case "boss3": return e.bossSub % 2 == 0 ? "summon" : "foam";
                case "boss4": return "confetti";
                case "boss5": return "gears";
            }
            return "";
        }

        /// <summary>这一招是不是特技：第 2 招开始每三招一次（一阶段也有一次，让玩家早点见识）。</summary>
        bool BossSpecialNext(Enemy e) => cfg.diff.bossSpecials && e.bossSpecK % (3 + Math.Min(2, run.assist)) == 1;   // 卡关时特技少一些
        /// <summary>每个 Boss 的特技（不是攻击）：积木巨人 / 气球之王一下插好几面旗；坦克、大将军用磁铁爪抢走玩家的枪；发条巨偶震空弹匣；玩具箱之王跟着学。</summary>
        static string BossSpecialOf(string src) => src switch
        {
            "boss1" => "multiFlag", "boss4" => "multiFlag",
            "boss2" => "grab", "boss3" => "grab",
            "boss5" => "drain",
            _ => "multiFlag",
        };

        void BossSpecial(Enemy e, string mv)
        {
            switch (mv)
            {
                case "multiFlag":                                                   // 一下插好几面小旗：逼玩家马上分心去拔
                {
                    bool intro = run.mode == "chapter" && run.chapter <= 2;          // 入门章（第 1、2 关）：一次只多插 1～2 面
                    int n = intro ? 1 : (int)Math.Round(e.bossPhase * cfg.diff.multiFlagPer);   // 一阶段 1 面、二阶段 2 面、三阶段 3 面（原来最多 4 面，熟练玩家也来不及拔）
                    n = Math.Max(1, n - run.assist);                                 // 这一波已经输过：少插几面（失败辅助）
                    for (int i = 0; i < n; i++)
                    {
                        var s = PickSpot(cfg.arena.flagMin, cfg.arena.flagMax, null);
                        for (int k = 0; k < 6 && V.distXZ(s, e.pos) < e.radius + 0.8; k++) s = PickSpot(cfg.arena.flagMin, cfg.arena.flagMax, null);
                        Reserve(s); PlantFlagAt(e, s, "small"); Release(s);
                    }
                    break;
                }
                case "grab": FireAtHead(e, "grab", 2.3); break;                    // 磁铁爪：举盾能挡住；打中就把枪抢走一会
                case "drain": FireAtHead(e, "drain", 2.6); break;                  // 发条震波：举盾能挡住；打中弹匣全空，得马上换弹
            }
        }

        void SetBossStage(Enemy e, int st, string mv, double dur)
        {
            e.bossStage = st; e.bossMove = mv; e.bossMoveT = 0;
            Emit(new BossMoveFx { type = "bossMove", id = e.id, move = mv, stage = st, dur = dur });
        }

        void BossAttack(Enemy e)
        {
            int ph = e.bossPhase;
            string k = e.kind;
            if (k == "boss6") k = "boss" + (1 + (e.bossCycle++ % 5));                  // 玩具箱之王：轮流用前五章的招式
            switch (k)
            {
                case "boss1":
                    FireAtHead(e, "brick", 2.4);
                    if (ph >= 2) for (int i = 0; i < 1; i++) SpawnEnemy("mouse", new SpawnOpts { portal = e.portal, offset = (e.bossCycle % 2 == 0 ? 1 : -1) * (e.radius + 0.6), eliteExplicit = true });
                    break;
                case "boss2":
                    for (int i = 0; i < ph; i++) e.bossVolley.Add(i * 0.35);               // 连发：一发一发打出去，看得清、躲得开
                    break;
                case "boss3":
                    if (e.bossSub++ % 2 == 0)                                        // 单独计数：原来和玩具箱之王的轮换共用 bossCycle，被加两次，玩具箱之王永远学不到气球之王
                    {
                        int alive = 0; foreach (var m in enemies) if (!m.dead && m.commanderId == e.id) alive++;
                        for (int i = 0; i < Math.Min(ph, 5 - alive); i++)
                        {
                            double side = (i % 2 == 0 ? 1 : -1) * (e.radius + 0.6 + 0.3 * (i / 2));   // 从 Boss 两侧外面出来（用户：Boss 身边不要有小怪，会穿模、看不清 Boss）
                            SpawnEnemy("minion", new SpawnOpts { portal = e.portal, commanderId = e.id, offset = side, eliteExplicit = true });
                        }
                        Emit(new SummonFx { type = "summon", id = e.id, n = 1 + ph });
                        foreach (var m in enemies) if (!m.dead && m.commanderId == e.id) ApplyOrder(m, "charge");
                    }
                    else FireAtHead(e, "foam", 3.0);
                    break;
                case "boss4":
                    FireAtHead(e, "confetti", 2.8);
                    break;
                case "boss5":
                    for (int i = 0; i < 3; i++) e.bossVolley.Add(i * 0.25);
                    break;
            }
        }

        /// <summary>连发队列：每帧推进，到点就打一发（坦克炮弹、发条齿轮）。</summary>
        void UpdateBossVolley(Enemy e, double dt)
        {
            if (e.bossVolley.Count == 0) return;
            for (int i = e.bossVolley.Count - 1; i >= 0; i--)
            {
                e.bossVolley[i] -= dt;
                if (e.bossVolley[i] > 0) continue;
                e.bossVolley.RemoveAt(i);
                FireAtHead(e, e.bossSrc == "boss5" ? "brick" : "shell", e.kind == "boss2" ? 3.2 : 2.8);
            }
        }

        /// <summary>挨打后检查阶段：掉到 2/3、1/3 血时踉跄、进下一阶段；气球之王此时放出一串小气球。</summary>
        void BossDamaged(Enemy e)
        {
            int want = e.hp <= e.maxHp / 3 ? 3 : e.hp <= e.maxHp * 2 / 3 ? 2 : 1;
            if (want <= e.bossPhase || e.dead) return;
            e.bossPhase = want; e.state = "stagger"; e.t = 0; e.tele = false; e.bossT = -0.5; e.bossVolley.Clear(); e.bossStage = 3; e.bossMove = "stagger";
            e.bossGuardT = 4 + 1.3;                                                    // 踉跄完立起护盾 4 秒（踉跄 1.3 秒本身是弱点，可以趁机打）
            Emit(new BossPhaseFx { type = "bossPhase", id = e.id, phase = want });
            if (e.kind == "boss4" || e.kind == "boss6")
                for (int i = 0; i < 3; i++) SpawnEnemy("balloonSmall", new SpawnOpts { portal = e.portal, offset = (i - 1) * (e.radius + 0.7), eliteExplicit = true });
        }

        Flag PlantFlagAt(Enemy e, Vec3d pos, string type)
        {
            var F = FLAGS.Get(type);
            var f = new Flag { id = nextId++, type = type, pos = new Vec3d(pos.x, 0, pos.z), hp = F.hp, maxHp = (int)F.hp, amount = 0, cap = F.cap, rate = F.rate, radius = F.radius };
            flags.Add(f);
            Emit(new FlagPlantedFx { type = "flagPlanted", id = f.id, flagType = type, pos = V.clone(f.pos), hidden = false });
            return f;
        }
    }
}
