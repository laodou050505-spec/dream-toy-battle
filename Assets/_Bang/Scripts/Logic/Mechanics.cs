// 门后机关 / 补给礼盒 / 门涌（迁移计划 3.1、3.2，原型里没有，Unity 版新增）。
//   投石车（第 1 章）：门后小山上，每 9 秒蓄力 1.78 秒后抛一颗积木球（抛物线，落点 = 你的头）；
//     积木球可以在空中打掉、举盾挡住、弹反打回投石车；砸中头就打飞你的枪。打投石车 6 枪散架 20 秒。
//   补给礼盒：每波 1～2 次，从门里飘进房间，飘 6 秒后飘回去；打 3 枪打开，随机得到
//     金色子弹（8 秒伤害 ×2）/ 冰冻（全场小怪定住 2.5 秒）/ 修理（盾和弹匣回满）/ 拔旗（最近的一面旗子倒下）。
//   门涌：第 3 波起每波一次，门发红预警 2 秒后连续涌出 3 只小怪。
using System;
using System.Collections.Generic;

namespace Bang.Logic
{
    public class Catapult
    {
        public int id;
        public Portal portal;
        public Vec3d pos;                 // 投石勺的位置（规则坐标）；表现层可以按场景里的模型改
        public double t, hp, maxHp, brokenT;
        public bool winding;
        public string kind = "catapult";   // catapult 投石车 / mortar 迫击炮 / gate 城门 / popper 派对礼炮 / clock 发条钟楼 / spring 弹簧发射器
    }

    public class GiftBox
    {
        public int id;
        public Portal portal;
        public Vec3d pos, from, spot;
        public double t, hp, maxHp;
        public bool leaving;
    }

    public class CatapultWindFx : FxEvent { public int id; }
    public class CatapultFireFx : FxEvent { public int id; public int bullet; }
    public class MechHitFx : FxEvent { public int id; public double hp, maxHp; public Vec3d pos; }
    public class MechBrokenFx : FxEvent { public int id; }
    public class MechRepairFx : FxEvent { public int id; }
    public class BallPopFx : FxEvent { public int id; public Vec3d pos; }
    public class BallLandFx : FxEvent { public int id; public Vec3d pos; }
    public class GiftSpawnFx : FxEvent { public int id; }
    public class GiftHitFx : FxEvent { public int id; public double hp, maxHp; public Vec3d pos; }
    public class GiftOpenFx : FxEvent { public int id; public string reward; public Vec3d pos; }
    public class GiftLeaveFx : FxEvent { public int id; }
    public class SurgeWarnFx : FxEvent { public int portal; public double dur; }
    public class SurgeFx : FxEvent { public int portal; }
    public class FreezeFx : FxEvent { public double dur; }

    public partial class Game
    {
        public Catapult catapult;
        public List<GiftBox> gifts = new List<GiftBox>();
        public double goldT;                          // 金色子弹剩余时间
        public double rapidT;                         // 无限火力剩余时间：子弹无限、持枪手势自动连射，手枪变长枪
        public int frenzyLeft;                        // 狂欢小怪还要出多少只
        double _frenzyT; bool _finale;
        List<double> _giftAt = new List<double>();
        double _surgeAt = -1, _surgeWarn = -1, _surgeGap; int _surgeLeft; Portal _surgePortal; bool _surgeActive;

        /// <summary>表现层用：按场景里投石车模型的位置校正（规则坐标）。</summary>
        public void SetMechPos(Vec3d p) { if (catapult != null) catapult.pos = V.clone(p); }

        public bool SurgeWarning => _surgeActive;

        void InitMechanics()
        {
            gifts.Clear(); _giftAt.Clear(); _surgeAt = -1; _surgeWarn = -1; _surgeLeft = 0; _surgeActive = false; goldT = 0; rapidT = 0;
            catapult = null;
            var C = run.mode == "chapter" ? CHAPTERS.Get(run.chapter) : null;
            if (!string.IsNullOrEmpty(C?.mech) && portals != null && portals.Count > 0)
            {
                var p = portals[0];
                catapult = new Catapult
                {
                    id = nextId++, portal = p, hp = cfg.mech.catapultHp, maxHp = cfg.mech.catapultHp, kind = C.mech,
                    pos = V.add(V.sub(p.@base, V.scale(p.normal, 3.6)), new Vec3d(0.6 * p.right.x, 0.55, 0.6 * p.right.z)),
                };
            }
        }

        /// <summary>每波开始时排好礼盒和门涌的时间。</summary>
        void ScheduleMechanics(int wave)
        {
            var M = cfg.mech;
            gifts.Clear(); _giftAt.Clear(); _surgeWarn = -1; _surgeLeft = 0; _surgeActive = false;
            if (run.mode == "lesson" || phase == "lesson") return;
            double t1 = M.giftFirstMin + rng() * (M.giftFirstMax - M.giftFirstMin);
            _giftAt.Add(t1);
            int count = run.queue.Count;                                // 小怪越多礼盒越多（用户：敌人多了补给也要多）
            if (count >= M.giftExtraAt || rng() < M.giftSecondChance) _giftAt.Add(t1 + 14 + rng() * 8);
            if (count >= M.giftThirdAt) _giftAt.Add(t1 + 30 + rng() * 8);
            // 门涌：大波本身已经很满，不再门涌（模拟：新手在第 1 章大波被门涌压垮）
            _surgeAt = wave >= M.surgeFromWave && !run.waveDef.big ? M.surgeAtMin + rng() * (M.surgeAtMax - M.surgeAtMin) : -1;
            if (catapult != null) { catapult.t = -M.catapultFirstDelay; catapult.winding = false; }
        }

        void UpdateMechanics(double dt)
        {
            if (!cfg.mech.enabled) return;
            if (goldT > 0 && enemies.Exists(e => !e.dead)) goldT -= dt;     // 场上没敌人时金色子弹不走时间，留到下一只出来（用户：不能有增益却没东西打）
            if (rapidT > 0)
            {
                rapidT -= dt;
                if (rapidT <= 0)
                {
                    // 用户：增益没了、枪变回去了，没打到的小怪也跟着凭空消失——不对。现在：剩下的狂欢小怪还在场上乱跑，手枪一枪一个，
                    // 再过几秒没打到的就自己跑回门里（看得见地离开），全部离场 / 打完才结算
                    rapidT = 0; frenzyLeft = 0; _finale = true;
                    foreach (var e in enemies) if (!e.dead && e.bonus) e.bonusT = Math.Max(e.bonusT, BonusStay - M_LeftoverStay);
                }
            }
            if (_finale) UpdateFinale(dt);
            if (phase != "wave") { gifts.Clear(); frenzyLeft = 0; return; }
            if (frenzyLeft > 0)
            {
                _frenzyT -= dt;
                if (_frenzyT <= 0) { _frenzyT = cfg.mech.giftRapidDur * 0.7 / cfg.mech.frenzyCount; frenzyLeft--; SpawnBonus(); }   // 前 7 成时间涌完：长枪结束前大部分已经打得掉
            }
            UpdateCatapult(dt);
            UpdateGifts(dt);
            UpdateSurge(dt);
        }

        // ───────── 无限火力：长枪连射 + 小怪成群涌出来给玩家扫，时间到时剩下的连环爆开 ─────────
        public void StartFrenzy()
        {
            var M = cfg.mech;
            rapidT = M.giftRapidDur; frenzyLeft = M.frenzyCount; _frenzyT = 0.15; _finale = false; _frenzyKills = 0;
            foreach (var s in new[] { "left", "right" }) { var w = weapons.Get(s); if (w != null) { w.ammo = MagOf(w.gun); w.reload = null; } }
        }

        static readonly string[] BonusKinds = { "mouse", "minion", "balloonSmall", "mouse" };   // 都是 1 枪倒

        /// <summary>这种玩具玩家已经见过了吗：按章节出怪表算到当前波。狂欢小怪只出见过的种类，第 1 章拿到长枪不会冒出后面章节的敌人。</summary>
        public bool KindSeen(string kind)
        {
            if (run.mode != "chapter" || kind == "mouse") return true;
            string src = kind == "minion" ? "commander" : kind == "balloonSmall" ? "balloon" : kind;   // 小锡兵跟着指挥官、小气球由气球兵分裂
            for (int c = 1; c <= run.chapter; c++)
            {
                var waves = CHAPTERS.Get(c).waves; int last = c == run.chapter ? Math.Min(run.wave, waves.Length - 1) : waves.Length - 1;
                for (int i = 0; i <= last; i++) foreach (var (k, _) in waves[i].spawns) if (k == src) return true;
            }
            return false;
        }

        void SpawnBonus()
        {
            var pool = new List<string>(); foreach (var k in BonusKinds) if (KindSeen(k)) pool.Add(k);
            var portal = portals[(int)Math.Floor(rng() * portals.Count)];
            var e = SpawnEnemy(pool[(int)Math.Floor(rng() * pool.Count)], new SpawnOpts { portal = portal, eliteExplicit = true });
            if (e == null) return;
            if (e.spot != null) { Release(e.spot); e.spot = null; }
            e.bonus = true; e.bonusT = 0; e.state = "bonus"; e.paused = false; e.hidden = false; e.orbitC = null;
        }

        void AiBonus(Enemy e, double dt, double spd)
        {
            e.bonusT += dt;
            if (e.bonusT > BonusStay) { Leave(e, spd * 1.4, dt); return; }
            if (PortalSide(e.portal, e.pos) < 0.35) { MoveTo(e, e.portal.mouth, spd * 1.3, dt); return; }
            if (e.orbitC == null || MoveTo(e, e.orbitC, spd * 1.3, dt))
            {
                var t = V.add(e.portal.mouth, V.add(V.scale(e.portal.normal, 0.3 + rng() * 1.5), V.scale(e.portal.right, (rng() - 0.5) * 2.4)));
                t.y = 0; e.orbitC = t;
            }
        }

        const double BonusStay = 9, M_LeftoverStay = 5;      // 狂欢小怪最多乱跑 9 秒；长枪结束时还在的，再给 5 秒用手枪补
        int _frenzyKills;                                       // 狂欢期间（含长枪结束后补枪）打倒的狂欢小怪
        internal void CountFrenzyKill() => _frenzyKills++;

        /// <summary>长枪结束后：等剩下的狂欢小怪全部打倒 / 跑回门里，再出结算横幅。</summary>
        void UpdateFinale(double dt)
        {
            foreach (var e in enemies) if (!e.dead && e.bonus) return;
            _finale = false;
            if (_frenzyKills > 0) Emit(new FrenzyEndFx { type = "frenzyEnd", count = _frenzyKills });
        }

        // ───────── 投石车 ─────────
        void UpdateCatapult(double dt)
        {
            var c = catapult; var M = cfg.mech;
            if (c == null || run.wave < M.catapultFromWave) return;
            if (c.brokenT > 0)
            {
                c.brokenT -= dt;
                if (c.brokenT <= 0) { c.hp = c.maxHp; c.t = -2; Emit(new MechRepairFx { type = "mechRepair", id = c.id }); }
                return;
            }
            c.t += dt;
            double windAt = M.catapultPeriod - M.catapultWind;
            double period = MechPeriod(c);
            windAt = period - M.catapultWind;
            if (!c.winding && c.t >= windAt) { c.winding = true; Emit(new CatapultWindFx { type = "catapultWind", id = c.id }); }
            if (c.t >= period)
            {
                c.t = 0; c.winding = false;
                if (MechFireSpecial(c)) return;
                // 抛物线必须从门洞里穿出来（不能从门上方的墙里冒出来）：先定穿过门洞的高度，反推弧高 h，
                // 再由 h = g·T²/8 得到飞行时间 T（重力固定，整条轨迹物理自洽）。
                double g = M.catapultGravity;
                var p0 = V.clone(c.pos); var target = V.clone(head.pos);
                var pl = c.portal;
                double d0 = V.dot(V.sub(p0, pl.@base), pl.normal), d1 = V.dot(V.sub(target, pl.@base), pl.normal);
                double sd = Math.Abs(d1 - d0) > 1e-4 ? Math.Max(0.1, Math.Min(0.9, -d0 / (d1 - d0))) : 0.5;   // 轨迹上穿过门面的位置（0..1）
                double lin = p0.y + (target.y - p0.y) * sd;
                double h = Math.Max(0.15, Math.Min(0.9, (M.catapultDoorY - lin) / (4 * sd * (1 - sd))));
                double T = Math.Sqrt(8 * h / g);
                var v = new Vec3d((target.x - p0.x) / T, (target.y - p0.y) / T + 0.5 * g * T, (target.z - p0.z) / T);
                var b = new Bullet { id = nextId++, kind = "ball", pos = p0, vel = v, from = c.id };
                bullets.Add(b);
                Emit(new CatapultFireFx { type = "catapultFire", id = c.id, bullet = b.id });
            }
        }

        double MechPeriod(Catapult c) => c.kind switch { "gate" => 18, "popper" => 7.5, "clock" => 8.5, "spring" => 11, _ => cfg.mech.catapultPeriod };

        /// <summary>各章机关的出手方式：投石车 / 迫击炮 / 钟楼 / 弹簧都抛弧线（走默认流程，返回 false）；城门放两个积木兵冲出来；派对礼炮扇形喷三团彩纸。</summary>
        bool MechFireSpecial(Catapult c)
        {
            switch (c.kind)
            {
                case "gate":
                    SpawnEnemy("planter", new SpawnOpts { portal = c.portal, eliteExplicit = true });   // 平衡模拟：一次放两个时新手第 3 章顶不住
                    Emit(new CatapultFireFx { type = "catapultFire", id = c.id, bullet = -1 });
                    return true;
                case "popper":
                    var from = V.clone(c.pos); var to = head.pos;
                    for (int i = -1; i <= 1; i++)
                    {
                        var aim = V.add(to, V.scale(new Vec3d(-(to.z - from.z), 0, to.x - from.x), i * 0.09));
                        var b = new Bullet { id = nextId++, kind = "confetti", pos = V.clone(from), vel = V.scale(V.norm(V.sub(aim, from)), 2.6), from = c.id };
                        bullets.Add(b);
                        if (i == 0) Emit(new CatapultFireFx { type = "catapultFire", id = c.id, bullet = b.id });
                    }
                    return true;
            }
            return false;
        }

        void DamageMech(Catapult c, double dmg)
        {
            if (c.brokenT > 0) return;
            c.hp -= dmg >= 2 ? 2 : 1;                    // 按枪数算（打 6 枪散架），暴击 / 金色子弹 / 弹反算 2 枪
            Emit(new MechHitFx { type = "mechHit", id = c.id, hp = Math.Max(0, c.hp), maxHp = c.maxHp, pos = V.clone(c.pos) });
            if (c.hp <= 0) { c.brokenT = cfg.mech.catapultBroken; c.winding = false; Emit(new MechBrokenFx { type = "mechBroken", id = c.id }); }
        }

        /// <summary>积木球（kind = "ball"）每帧：受重力；被弹反时飞回投石车。返回 false 表示移除。</summary>
        bool UpdateBall(Bullet b, double dt)
        {
            var prev = V.clone(b.pos);
            if (b.reflected)
            {
                var c = catapult;
                if (c == null) return false;
                var to = V.sub(c.pos, b.pos);
                b.vel = V.scale(V.norm(to), 7);
                b.pos = V.add(b.pos, V.scale(b.vel, dt));
                if (V.len(to) < 0.3) { DamageMech(c, 2); Emit(new BallPopFx { type = "ballPop", id = b.id, pos = V.clone(b.pos) }); return false; }
                b.travelled += 7 * dt;
                return b.travelled < 12;
            }
            b.vel = new Vec3d(b.vel.x, b.vel.y - cfg.mech.catapultGravity * dt, b.vel.z);
            b.pos = V.add(b.pos, V.scale(b.vel, dt));
            b.travelled += V.len(b.vel) * dt;
            string res = CollideIncoming(prev, b.pos, b.vel, 0.1, b);
            if (res == "parried") { b.reflected = true; b.travelled = 0; return true; }
            if (res == "blocked") { Emit(new BallPopFx { type = "ballPop", id = b.id, pos = V.clone(b.pos) }); return false; }
            if (res == "head") { HitHead("ball", b.pos); Emit(new BallPopFx { type = "ballPop", id = b.id, pos = V.clone(b.pos) }); return false; }
            if (b.pos.y < 0 || b.travelled > 16)
            {
                Emit(new BallLandFx { type = "ballLand", id = b.id, pos = new Vec3d(b.pos.x, 0, b.pos.z) });
                if (catapult != null && catapult.kind == "spring" && b.pos.y < 0.1 && PortalSide(catapult.portal, b.pos) > 0.5 && phase == "wave")   // 弹簧发射器：弹出来的是一只发条老鼠
                    SpawnEnemy("mouse", new SpawnOpts { portal = catapult.portal, pos = new Vec3d(b.pos.x, 0, b.pos.z), eliteExplicit = true });
                return false;
            }
            return true;
        }

        // ───────── 补给礼盒 ─────────
        void UpdateGifts(double dt)
        {
            var M = cfg.mech;
            for (int i = _giftAt.Count - 1; i >= 0; i--)
            {
                if (phaseT < _giftAt[i]) continue;
                _giftAt.RemoveAt(i);
                if (RemainingEnemies() < 2) continue;                  // 快打完了就别出补给
                var p = portals[(int)Math.Floor(rng() * portals.Count)];
                // 飘到房间里玩家前方 1.4～2.2 米、视线高度附近的一点
                double ang = (rng() - 0.5) * 1.6, dist = 1.4 + rng() * 0.8;
                var f = V.norm(new Vec3d(head.fwd.x, 0, head.fwd.z));
                var r = new Vec3d(-f.z, 0, f.x);
                var spot = V.add(new Vec3d(head.pos.x, 1.25 + rng() * 0.35, head.pos.z), V.add(V.scale(f, dist * Math.Cos(ang)), V.scale(r, dist * Math.Sin(ang))));
                var from = new Vec3d(p.mouth.x, 1.1, p.mouth.z);
                var g = new GiftBox { id = nextId++, portal = p, pos = V.clone(from), from = from, spot = spot, hp = M.giftHp, maxHp = M.giftHp };
                gifts.Add(g);
                Emit(new GiftSpawnFx { type = "giftSpawn", id = g.id });
            }
            for (int i = gifts.Count - 1; i >= 0; i--)
            {
                var g = gifts[i];
                g.t += dt;
                double E = M.giftEnter, L = M.giftLife;
                Vec3d a, b; double k;
                if (g.t < E) { a = g.from; b = g.spot; k = Smooth(g.t / E); }
                else if (g.t < E + L) { a = g.spot; b = g.spot; k = 0; }
                else { a = g.spot; b = g.from; k = Smooth((g.t - E - L) / E); g.leaving = true; }
                var p = V.add(a, V.scale(V.sub(b, a), k));
                // 悬停时慢慢画小圈 + 上下浮动（像被气球吊着）
                double h = Math.Min(1, Math.Min(g.t / E, Math.Max(0, (E * 2 + L - g.t) / E)));
                g.pos = new Vec3d(p.x + Math.Sin(g.t * 1.3) * 0.12 * h, p.y + Math.Sin(g.t * 2.1) * 0.05 * h, p.z + Math.Cos(g.t * 1.1) * 0.08 * h);
                if (g.t >= E * 2 + L) { gifts.RemoveAt(i); Emit(new GiftLeaveFx { type = "giftLeave", id = g.id }); }
            }
        }

        static double Smooth(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }

        /// <summary>这一波还剩多少只要打（在场的 + 待出的，狂欢小怪不算）。</summary>
        int RemainingEnemies()
        {
            int n = run.queue.Count;
            foreach (var e in enemies) if (!e.dead && !e.bonus) n++;
            return n;
        }

        void DamageGift(GiftBox g, double dmg)
        {
            g.hp -= 1;                                   // 礼盒按枪数算（3 枪），不吃伤害倍率
            Emit(new GiftHitFx { type = "giftHit", id = g.id, hp = Math.Max(0, g.hp), maxHp = g.maxHp, pos = V.clone(g.pos) });
            if (g.hp > 0) return;
            gifts.Remove(g);
            var pool = new List<string> { "repair", "rapid" };
            if (RemainingEnemies() >= 3) { pool.Add("gold"); pool.Add("freeze"); }   // 快打完了不给金色子弹和冰冻，给了也没东西打
            bool anyFlag = false; foreach (var f in flags) if (!f.dying) { anyFlag = true; break; }
            if (anyFlag) pool.Add("unflag");
            string rw = pool[(int)Math.Floor(rng() * pool.Count)];
            if (!run.rapidGiven && run.wave >= 1 && run.mode == "chapter") rw = "rapid";      // 每章保底：第 2 波起第一个礼盒一定是无限火力
            else if (rw == "rapid" && (run.rapidWave == run.wave || run.rapidCount >= 2))     // 用户：礼盒别总是机枪 → 同一波最多一次、一章最多两次，超了换别的
            {
                pool.RemoveAll(x => x == "rapid"); rw = pool[(int)Math.Floor(rng() * pool.Count)];
            }
            if (rw == "rapid") { run.rapidGiven = true; run.rapidCount++; run.rapidWave = run.wave; }
            var M = cfg.mech;
            switch (rw)
            {
                case "gold": goldT = M.giftGoldDur; break;
                case "rapid": StartFrenzy(); break;
                case "freeze": foreach (var e in enemies) if (!e.dead) e.frozenT = M.giftFreezeDur; Emit(new FreezeFx { type = "freeze", dur = M.giftFreezeDur }); break;
                case "repair":
                    shield.broken = 0; shield.dur = ShieldMax();
                    foreach (var s in new[] { "left", "right" }) { var w = weapons.Get(s); if (w != null) { w.ammo = MagOf(w.gun); w.reload = null; } }
                    break;
                case "unflag":
                {
                    Flag best = null; double bd = 1e9;
                    foreach (var f in flags) { if (f.dying) continue; double d = V.distXZ(f.pos, head.pos); if (d < bd) { bd = d; best = f; } }
                    if (best != null) KillFlag(best, true);
                    break;
                }
            }
            Emit(new GiftOpenFx { type = "giftOpen", id = g.id, reward = rw, pos = V.clone(g.pos) });
        }

        // ───────── 门涌 ─────────
        void UpdateSurge(double dt)
        {
            var M = cfg.mech;
            if (_surgeAt >= 0 && phaseT >= _surgeAt)
            {
                _surgeAt = -1;
                _surgePortal = portals[(int)Math.Floor(rng() * portals.Count)];
                _surgeWarn = M.surgeWarn; _surgeLeft = run.mode == "chapter" && run.chapter == 1 ? Math.Min(2, M.surgeCount) : M.surgeCount; _surgeGap = 0; _surgeActive = true;
                Emit(new SurgeWarnFx { type = "surgeWarn", portal = _surgePortal.id, dur = M.surgeWarn });
            }
            if (_surgeActive)
            {
                _surgeWarn -= dt;
                if (_surgeWarn > 0) return;
                _surgeGap -= dt;
                if (_surgeGap <= 0 && _surgeLeft > 0)
                {
                    // 门涌的小怪从这一波的轻型出怪种类里挑（胖墩 / 铁皮兵 / 指挥官这类重型不涌，一次涌三只重型就是死局）
                    var kinds = new List<string>();
                    foreach (var (k, n) in run.waveDef.spawns) if (KINDS.Get(k).hp <= 4 && k != "commander") kinds.Add(k);
                    string kind = kinds.Count > 0 ? kinds[(int)Math.Floor(rng() * kinds.Count)] : "planter";
                    SpawnEnemy(kind, new SpawnOpts { portal = _surgePortal, offset = (_surgeLeft - 2) * 0.3 });
                    Emit(new SurgeFx { type = "surge", portal = _surgePortal.id });
                    _surgeLeft--; _surgeGap = M.surgeGap;
                }
                if (_surgeLeft <= 0) _surgeActive = false;
            }
        }

        /// <summary>机关类目标（积木球、投石车、礼盒）加进瞄准目标列表。</summary>
        void AddMechTargets(List<TargetInfo> out0, Vec3d origin)
        {
            origin = head?.pos ?? origin;                                 // 看不看得见按眼睛算（不是枪口）
            foreach (var b in bullets)
            {
                if (b.kind != "ball" || b.reflected) continue;
                if (catapult != null && PortalSide(catapult.portal, b.pos) < -0.05 && !SeenBehind(catapult.portal, origin, b.pos)) continue;
                out0.Add(new TargetInfo { id = b.id, type = "ball", pos = V.clone(b.pos), radius = 0.16, @ref = b });
            }
            var c = catapult;
            if (c != null && c.brokenT <= 0 && run.wave >= cfg.mech.catapultFromWave && phase == "wave" && SeenBehind(c.portal, origin, c.pos))
                out0.Add(new TargetInfo { id = c.id, type = "mech", pos = V.clone(c.pos), radius = 0.32, @ref = c });
            foreach (var g in gifts)
            {
                if (PortalSide(g.portal, g.pos) < -0.05 && !SeenBehind(g.portal, origin, g.pos)) continue;
                out0.Add(new TargetInfo { id = g.id, type = "gift", pos = V.clone(g.pos), radius = 0.2, @ref = g });
            }
        }

        /// <summary>命中机关类目标；返回 true 表示已处理。</summary>
        /// <summary>单测用：直接打一枪某个机关类目标（礼盒、积木球、机关）。</summary>
        public bool HitTargetForTest(int id)
        {
            var list = new List<TargetInfo>(); AddMechTargets(list, head.pos);
            foreach (var t in list) if (t.id == id) return HitMechTarget(t, 1);
            return false;
        }

        bool HitMechTarget(TargetInfo t, double dmg)
        {
            switch (t.type)
            {
                case "ball":
                    bullets.Remove((Bullet)t.@ref);
                    Emit(new BallPopFx { type = "ballPop", id = t.id, pos = V.clone(t.pos) });
                    return true;
                case "mech": DamageMech((Catapult)t.@ref, dmg); return true;
                case "gift": DamageGift((GiftBox)t.@ref, dmg); return true;
            }
            return false;
        }
    }
}
