// 平衡模拟（移植自原型 tools/sim.mjs）：三档机器人玩家（新手 / 普通 / 熟练）连打 6 章 + 无尽，
// 统计每章通关率、失败次数、峰值玩具化度、用时、星级、命中率。机器人也会应对 Unity 版新增的机关：
// 优先打掉 / 用盾挡飞来的积木球，有空就抢补给礼盒，偶尔打投石车。
// 菜单：Bang/平衡模拟（结果写到控制台和 Docs/平衡模拟.md）
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bang.Logic;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangBalanceSim
    {
        class Skill { public string name; public double acc, react, flickRate, gold, shield, parry; public bool binoc = true; }
        static readonly Skill[] Skills =
        {
            new Skill { name = "新手", acc = 0.75, react = 0.55, flickRate = 1.3, gold = 0.3, shield = 0.45, parry = 0.1 },
            new Skill { name = "普通", acc = 0.85, react = 0.4, flickRate = 1.8, gold = 0.5, shield = 0.65, parry = 0.2 },
            new Skill { name = "熟练", acc = 0.95, react = 0.25, flickRate = 2.4, gold = 0.75, shield = 0.85, parry = 0.35 },
        };
        static readonly Head HEAD = new Head { pos = new Vec3d(0, 1.6, 0), fwd = new Vec3d(0, 0, -1) };

        class MemStore : ISaveStore { SaveData d; public SaveData Load() => d; public void Save(SaveData x) { d = x; } }

        class Bot
        {
            readonly Game g; readonly Skill s; readonly Func<double> rng;
            int? target; double lookT, flickT, shieldT, binocT; int? binocTarget;
            readonly Dictionary<string, double?> reloadHold = new() { ["right"] = null, ["left"] = null };
            readonly HashSet<int> decided = new();
            public Bot(Game g, Skill s, Func<double> rng) { this.g = g; this.s = s; this.rng = rng; }

            double Priority(TargetInfo t)
            {
                switch (t.type)
                {
                    case "ball": return 97;                                   // 飞来的积木球：先打掉
                    case "gift": return 70;
                    case "mech": return 25;
                    case "flag": { var f = (Flag)t.@ref; return 30 + f.amount * 2 - f.hp; }
                }
                var r = (Enemy)t.@ref; var k = r.kind;
                if (r.state == "plant") return 100 - (r.k.plantTime - r.t) * 10;
                if (r.state == "dive") return 95;
                if (k == "commander") return g.marks.ContainsKey(r.id) ? 90 : 20;
                if (k == "rcCar") return 85;
                if (k == "repair") return 80;
                if (r.state == "walk" || r.state == "charge") return 60 + (r.spot != null ? 10 / (V.distXZ(r.pos, r.spot) + 0.3) : 0);
                if (k == "shooter" || k == "jack") return 55;
                if (r.state == "leave" || r.state == "flee" || r.state == "rise") return 5;
                return 40;
            }

            static HandOut H(string state) => new HandOut { state = state, events = new List<HandEvent>(), tracked = true };

            public void Frame(double dt)
            {
                bool dual = g.weapons.left != null;
                var hands = new Hands { right = H("gun") };
                hands.right.palm = new Vec3d(0.2, 1.35, -0.6); hands.right.palmN = new Vec3d(-1, 0, 0);
                if (dual) { hands.left = H("gun"); hands.left.palm = new Vec3d(-0.2, 1.35, -0.6); hands.left.palmN = new Vec3d(1, 0, 0); }
                var gaze = new GazeRay { origin = HEAD.pos, dir = HEAD.fwd };

                // 盾牌：子弹 / 积木球快打到时举盾
                bool canShield = g.profile.unlocked.TryGetValue("shield", out var us) && us;
                if (canShield && shieldT <= 0)
                {
                    foreach (var b in g.bullets)
                    {
                        if (b.reflected || decided.Contains(b.id)) continue;
                        double tti = V.dist(b.pos, HEAD.pos) / Math.Max(0.1, V.len(b.vel));
                        if (tti < 0.45) { decided.Add(b.id); if (rng() < s.shield) shieldT = rng() < s.parry ? tti + 0.05 : tti + 0.5; }
                    }
                    foreach (var e in g.enemies) if (e.state == "dive" && !decided.Contains(e.id) && V.dist(e.pos, HEAD.pos) < 1.2) { decided.Add(e.id); if (rng() < s.shield) shieldT = 0.8; }
                }
                if (shieldT > 0) { shieldT -= dt; hands.left = new HandOut { state = "shield", events = new List<HandEvent>(), tracked = true, palm = new Vec3d(0, 1.6, -0.3), palmN = new Vec3d(0, 0, -1) }; }

                // 望远镜：标记隐形 / 装甲 / 指挥官
                bool canBinoc = g.profile.unlocked.TryGetValue("binoc", out var ub) && ub;
                if (canBinoc && s.binoc)
                {
                    if (binocT > 0)
                    {
                        binocT -= dt;
                        var bl = H("binoc");
                        var bt = g.Targets(HEAD.pos, true).FirstOrDefault(x => x.id == binocTarget);
                        if (bt != null) gaze = new GazeRay { origin = HEAD.pos, dir = V.norm(V.sub(bt.pos, HEAD.pos)) };
                        if (bt == null || (binocTarget.HasValue && g.marks.ContainsKey(binocTarget.Value))) binocT = Math.Min(binocT, 0.15);
                        g.Update(dt, new FrameInput { head = HEAD, gaze = gaze, hands = new Hands { left = bl } });
                        return;
                    }
                    var need = g.Targets(HEAD.pos, true).FirstOrDefault(t => t.type == "enemy" && !g.marks.ContainsKey(t.id) &&
                        (((Enemy)t.@ref).hidden || ((Enemy)t.@ref).armor > 0 || ((Enemy)t.@ref).kind == "commander") && ((Enemy)t.@ref).state != "leave");
                    bool urgent = g.enemies.Any(e => e.state == "plant" && !e.hidden) || g.bullets.Count > 0;
                    if (need != null && !urgent && shieldT <= 0) { binocT = 0.75 + s.react; binocTarget = need.id; }
                }

                // 选目标
                var ts = g.Targets(HEAD.pos).Where(t => !(t.type == "enemy" && ((Enemy)t.@ref).kind == "dummy" && ((Enemy)t.@ref).maxHp >= 100)).ToList();
                var best = ts.OrderByDescending(Priority).FirstOrDefault();
                if (best != null && best.id != target) { target = best.id; lookT = 0; }
                if (best == null) target = null;
                if (best != null) gaze = new GazeRay { origin = HEAD.pos, dir = V.norm(V.sub(best.pos, HEAD.pos)) };
                lookT += dt; flickT -= dt;

                foreach (var side in dual ? new[] { "right", "left" } : new[] { "right" })
                {
                    var w = g.weapons.Get(side); if (w == null) continue;
                    var h = side == "right" ? hands.right : hands.left;
                    if (reloadHold[side] != null)
                    {
                        reloadHold[side] -= dt;
                        if (reloadHold[side] <= 0) { h = new HandOut { state = "idle", tracked = true, events = new List<HandEvent> { new StateEvent { side = side, from = "fist", to = "idle" } } }; reloadHold[side] = null; }
                        else h = H("fist");
                        if (side == "right") hands.right = h; else hands.left = h;
                        continue;
                    }
                    if (w.ammo == 0 && w.reload == null)
                    {
                        var RT = g.ReloadTimes();
                        reloadHold[side] = rng() < s.gold ? (RT.goldStart + RT.goldEnd) / 2 : RT.total + 0.05;
                        h = new HandOut { state = "fist", tracked = true, events = new List<HandEvent> { new StateEvent { side = side, from = "gun", to = "fist" } } };
                        if (side == "right") hands.right = h; else hands.left = h;
                        continue;
                    }
                    if (best != null && lookT > s.react && flickT <= 0 && h != null && h.state == "gun")
                    {
                        var o = new Vec3d(side == "right" ? 0.2 : -0.2, 1.35, -0.6);
                        var dir = V.norm(V.sub(best.pos, o));
                        if (rng() > s.acc) dir = V.rotY(dir, 40 * V.DEG);          // 眼动抖动 / 手势误差：偏出判定锥
                        h.events.Add(new FireEvent { side = side, origin = o, dir = dir });
                        flickT = 1 / s.flickRate / (dual ? 1.6 : 1);
                    }
                }
                g.Update(dt, new FrameInput { head = HEAD, gaze = gaze, hands = hands });
            }
        }

        class ChResult { public bool done; public int losses, stars; public double peak, time, acc, bossT; public int knocks, gifts; }
        public static readonly System.Collections.Generic.List<(int ch, string skill, string kind, double life)> BossLives = new();

        static readonly System.Collections.Generic.Dictionary<int, double> _bossBorn = new();
        static ChResult PlayChapter(Game g, int ch, Skill skill, Func<double> rng, double maxTime = 900)
        {
            g.StartChapter(ch, new StartChapterOpts { skipLesson = true });
            var bot = new Bot(g, skill, rng);
            double t = 0, peak = 0, dt = 1.0 / 30; int losses = 0, knocks = 0, gifts = 0;
            while (t < maxTime)
            {
                bot.Frame(dt); t += dt; peak = Math.Max(peak, g.toy);
                foreach (var e in g.DrainFx()) { if (e.type == "lost") losses++; if (e is KnockedFx) knocks++; if (e is GiftOpenFx) gifts++;
                    if (e is BossIntroFx bi) _bossBorn[bi.id] = t;
                    if (e is KillFx kf && _bossBorn.TryGetValue(kf.id, out var bt)) { BossLives.Add((ch, skill.name, kf.kind ?? "", t - bt)); _bossBorn.Remove(kf.id); } }
                if (g.phase == "station") g.ContinueFromStation();
                    if (g.phase == "lost" && g.phaseT > 1.5) g.RetryAfterLoss();      // 失败画面：玩家看一眼就点重试
                if (g.phase == "reward") break;
            }
            var st = g.reward?.stats;
            return new ChResult { done = g.phase == "reward", losses = losses, peak = peak, time = t, stars = g.reward?.stars ?? 0, acc = st != null ? st.hits * 100.0 / Math.Max(1, st.shots) : 0, knocks = knocks, gifts = gifts };
        }

        static int PlayEndless(Game g, Skill skill, Func<double> rng)
        {
            g.StartEndless();
            var bot = new Bot(g, skill, rng); double t = 0;
            while (t < 1800 && g.phase != "endlessOver") { bot.Frame(1.0 / 30); t += 1.0 / 30; g.DrainFx(); if (g.phase == "station") g.ContinueFromStation(); if (g.phase == "travel") g.EndlessArrive(g.endlessNext); if (g.phase == "lost" && g.phaseT > 1.5) g.RetryAfterLoss(); }   // travel：打完一段 Boss 换场景（真机是表现层换好世界再调）
            return g.phase == "endlessOver" ? g.profile.endlessBest : g.run.wave;   // 30 分钟还没输：按当前波数算
        }

        static string RunAll(bool proto, int N)
        {
            var sb = new StringBuilder();
            sb.AppendLine(proto ? "### 原型难度（难度系数全为 1、没有新机关，对照）" : "### 当前难度（按章校准的数量 / 出怪间隔 / 旗子增长系数 + 失败辅助 + 门后机关 / 礼盒 / 门涌，参数见 Config.diff / Config.mech）");
            foreach (var skill in Skills)
            {
                var agg = new Dictionary<int, List<ChResult>>(); var endless = new List<int>();
                for (int run = 0; run < N; run++)
                {
                    long seed = 1000 + run * 77;
                    Func<double> rng = () => { seed = seed * 16807 % 2147483647; return seed / 2147483647.0; };
                    var cfg = new Config();
                    if (proto) { cfg.diff.Flat(); cfg.mech.enabled = false; }
                    var g = new Game(new GameOpts { store = new MemStore(), seed = (uint)(500 + run), cfg = cfg });
                    for (int ch = 1; ch <= CHAPTERS.Count; ch++)
                    {
                        for (int k = 0; k < 6; k++) foreach (var u in new[] { "reload", "gold", "mag", "shield", "parry" }) g.BuyUpgrade(u);
                        var r = PlayChapter(g, ch, skill, rng);
                        if (!agg.ContainsKey(ch)) agg[ch] = new List<ChResult>();
                        agg[ch].Add(r);
                        if (!r.done) break;
                    }
                    endless.Add(PlayEndless(g, skill, rng));
                }
                sb.AppendLine($"\n**{skill.name}**（{N} 次）");
                sb.AppendLine("| 章 | 通关 | 失败次数 | 峰值玩具化 | 用时 | 星级 | 命中率 | 被打飞枪 | 开礼盒 |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
                foreach (var kv in agg)
                {
                    var rs = kv.Value;
                    string Avg(Func<ChResult, double> f) => (rs.Average(f)).ToString("0.0");
                    sb.AppendLine($"| {kv.Key} | {rs.Count(r => r.done)}/{rs.Count} | {Avg(r => r.losses)} | {Avg(r => r.peak)}% | {Avg(r => r.time):0}s | {string.Join("/", rs.Select(r => r.done ? r.stars.ToString() : "-"))} | {Avg(r => r.acc)}% | {Avg(r => r.knocks)} | {Avg(r => r.gifts)} |");
                }
                sb.AppendLine($"无尽：撑过 {string.Join(" / ", endless)} 波");
            }
            return sb.ToString();
        }

        /// <summary>Boss 存活时间：某档机器人把有 Boss 的关各打 n 次，统计每个 Boss 从登场到被打倒的秒数（用户：Boss 不要一会就被秒了）。</summary>
        public static string BossTime(string skillName, int n = 3)
        {
            var skill = Skills.First(x => x.name == skillName); BossLives.Clear();
            foreach (var ch in new[] { 2, 4, 6, 8, 10, 12, 13 })
                for (int run = 0; run < n; run++)
                {
                    long seed = 5000 + run * 97 + ch;
                    Func<double> rng = () => { seed = seed * 16807 % 2147483647; return seed / 2147483647.0; };
                    var g = new Game(new GameOpts { store = new MemStore(), seed = (uint)(700 + run * 11 + ch), cfg = new Config() });
                    for (int k = 0; k < 6; k++) foreach (var u in new[] { "reload", "gold", "mag", "shield", "parry" }) g.BuyUpgrade(u);
                    for (int c = 1; c < ch; c++) { g.profile.cleared = c; var rw = CHAPTERS.Get(c).reward; if (!string.IsNullOrEmpty(rw)) g.profile.unlocked[rw] = true; if (rw == "endless") g.profile.unlocked["bubble"] = true; }
                    PlayChapter(g, ch, skill, rng);
                }
            var sb = new StringBuilder($"{skillName} Boss 存活秒数（每关 {n} 次）\n");
            foreach (var grp in BossLives.GroupBy(x => (x.ch, x.kind)).OrderBy(x => x.Key.ch))
                sb.Append($"第{grp.Key.ch}关 {grp.Key.kind}: {string.Join(" / ", grp.Select(x => x.life.ToString("0")))} 秒\n");
            return sb.ToString();
        }

        /// <summary>三星可达性：某档机器人每章打 n 次，统计三星率、峰值 / 终值玩具化分位、失败次数（用户：每一关的三星要确保有人能打出来）。</summary>
        public static string StarCheck(string skillName, int n = 8)
        {
            var skill = Skills.First(x => x.name == skillName);
            var sb = new StringBuilder($"{skillName} 三星检查（每章 {n} 次）\n章 | 三星率 | 峰值玩具化 p25/p50 | 终值 p50 | 失败>0 次数\n");
            for (int ch = 1; ch <= CHAPTERS.Count; ch++)
            {
                var peaks = new List<double>(); var ends = new List<double>(); int three = 0, lost = 0;
                for (int run = 0; run < n; run++)
                {
                    long seed = 3000 + run * 131 + ch * 7;
                    Func<double> rng = () => { seed = seed * 16807 % 2147483647; return seed / 2147483647.0; };
                    var g = new Game(new GameOpts { store = new MemStore(), seed = (uint)(900 + run * 13 + ch), cfg = new Config() });
                    for (int k = 0; k < 6; k++) foreach (var u in new[] { "reload", "gold", "mag", "shield", "parry" }) g.BuyUpgrade(u);
                    for (int c = 1; c < ch; c++) { g.profile.cleared = c; var rw = CHAPTERS.Get(c).reward; if (!string.IsNullOrEmpty(rw)) g.profile.unlocked[rw] = true; if (rw == "endless") g.profile.unlocked["bubble"] = true; }   // 前面关卡奖励的装备（盾牌、双枪……）都已到手
                    var r = PlayChapter(g, ch, skill, rng);
                    peaks.Add(r.peak); ends.Add(g.toy); if (r.stars == 3) three++; if (r.losses > 0) lost++;
                }
                peaks.Sort(); ends.Sort();
                sb.Append($"{ch} | {three}/{n} | {peaks[n / 4]:0}/{peaks[n / 2]:0} | {ends[n / 2]:0} | {lost}\n");
            }
            return sb.ToString();
        }

        /// <summary>单项探针：某档机器人打某章 n 次，配置可调；返回通关率、平均失败、每波失败分布。</summary>
        public static string Probe(string skillName, int ch, string label, Action<Config> tweak, int n = 4)
        {
            var skill = Skills.First(x => x.name == skillName);
            int done = 0; double losses = 0; var perWave = new int[6];
            for (int run = 0; run < n; run++)
            {
                long seed = 1000 + run * 77;
                Func<double> rng = () => { seed = seed * 16807 % 2147483647; return seed / 2147483647.0; };
                var cfg = new Config(); tweak?.Invoke(cfg);
                var g = new Game(new GameOpts { store = new MemStore(), seed = (uint)(500 + run), cfg = cfg });
                for (int c = 1; c < ch; c++) { if (!string.IsNullOrEmpty(CHAPTERS.Get(c).reward)) g.profile.unlocked[CHAPTERS.Get(c).reward] = true; g.profile.cleared = c; }
                g.profile.coins += 45 * (ch - 1);                                   // 前几章攒下的玩具币（按完整流程的平均值估算）
                for (int k = 0; k < 6; k++) foreach (var u in new[] { "reload", "gold", "mag", "shield", "parry" }) g.BuyUpgrade(u);
                g.StartChapter(ch, new StartChapterOpts { skipLesson = true });
                var bot = new Bot(g, skill, rng); double t = 0;
                while (t < 900)
                {
                    bot.Frame(1.0 / 30); t += 1.0 / 30;
                    foreach (var e in g.DrainFx()) if (e.type == "lost") { losses++; perWave[Math.Min(5, g.run.wave)]++; }
                    if (g.phase == "station") g.ContinueFromStation();
                    if (g.phase == "lost" && g.phaseT > 1.5) g.RetryAfterLoss();      // 失败画面：玩家看一眼就点重试
                    if (g.phase == "reward") { done++; break; }
                }
            }
            var line = $"{skillName} 第{ch}章 [{label}] 通关 {done}/{n} 平均失败 {losses / n:0.0} 每波失败 {string.Join(",", perWave)}";
            File.AppendAllText("/tmp/bang_probe.txt", line + "\n");          // 长任务：每项跑完就落盘，调用方超时也不丢
            return line;
        }

        /// <summary>整章诊断：按 RunAll 的流程（同种子、从第 1 章一路打上来）打到第 ch 章；超时时报告卡在哪（阶段、波次、剩余小怪的状态和位置、出怪队列）。</summary>
        public static string DiagChapter(string skillName, int ch, int run)
        {
            var skill = Skills.First(x => x.name == skillName);
            long seed = 1000 + run * 77;
            Func<double> rng = () => { seed = seed * 16807 % 2147483647; return seed / 2147483647.0; };
            var g = new Game(new GameOpts { store = new MemStore(), seed = (uint)(500 + run), cfg = new Config() });
            ChResult r = null;
            for (int c = 1; c <= ch; c++)
            {
                for (int k = 0; k < 6; k++) foreach (var u in new[] { "reload", "gold", "mag", "shield", "parry" }) g.BuyUpgrade(u);
                r = PlayChapter(g, c, skill, rng);
                if (!r.done) break;
            }
            var sb = new StringBuilder($"{skillName} 第{ch}章 run{run}: done={r.done} phase={g.phase} wave={g.run.wave} queue={g.run.queue.Count} toy={g.toy:0} t={r.time:0}\n");
            foreach (var e in g.enemies) sb.Append($"  {e.kind} state={e.state} dead={e.dead} pos=({e.pos.x:0.00},{e.pos.y:0.00},{e.pos.z:0.00}) side={g.PortalSide(e.portal, e.pos):0.00} hp={e.hp}\n");
            return sb.ToString();
        }

        /// <summary>单波诊断：只打某章某一波一次，统计被打飞枪、各种小怪插旗 / 被击倒数、命中率、用时、结局。</summary>
        public static string DiagWave(string skillName, int ch, int wave, int seed = 0)
        {
            var skill = Skills.First(x => x.name == skillName);
            long sd = 1000 + seed * 77; Func<double> rng = () => { sd = sd * 16807 % 2147483647; return sd / 2147483647.0; };
            var g = new Game(new GameOpts { store = new MemStore(), seed = (uint)(500 + seed) });
            for (int c = 1; c < ch; c++) { if (!string.IsNullOrEmpty(CHAPTERS.Get(c).reward)) g.profile.unlocked[CHAPTERS.Get(c).reward] = true; g.profile.cleared = c; }
            g.profile.coins += 45 * (ch - 1);
            for (int k = 0; k < 6; k++) foreach (var u in new[] { "reload", "gold", "mag", "shield", "parry" }) g.BuyUpgrade(u);
            g.StartChapter(ch, new StartChapterOpts { skipLesson = true }); g.BeginWave(wave);
            var bot = new Bot(g, skill, rng); double t = 0; int knocks = 0; string end = "超时";
            var kindOf = new Dictionary<int, string>(); var planted = new Dictionary<string, int>(); var killed = new Dictionary<string, int>();
            while (t < 300)
            {
                foreach (var e in g.enemies) kindOf[e.id] = e.kind;
                bot.Frame(1.0 / 30); t += 1.0 / 30;
                foreach (var f in g.DrainFx())
                {
                    if (f is KnockedFx) knocks++;
                    if (f is PlantingFx pf && kindOf.TryGetValue(pf.id, out var k1)) planted[k1] = planted.TryGetValue(k1, out var v1) ? v1 + 1 : 1;
                    if (f is KillFx kf) killed[kf.kind] = killed.TryGetValue(kf.kind, out var v2) ? v2 + 1 : 1;
                    if (f.type == "lost") end = "失败";
                }
                if (end == "失败" || g.phase == "rest" || g.phase == "station" || g.phase == "clear") { if (end != "失败") end = "过关"; break; }
            }
            var st = g.run.stats;
            return $"{skillName} 第{ch}章第{wave + 1}波：{end} 用时 {t:0}s 玩具化 {g.toy:0}% 被打飞 {knocks} 次 命中 {st.hits}/{st.shots} 插旗 [{string.Join(" ", planted.Select(kv => kv.Key + "×" + kv.Value))}] 击倒 [{string.Join(" ", killed.Select(kv => kv.Key + "×" + kv.Value))}]";
        }

        [MenuItem("Bang/平衡模拟")]
        public static string Run() => Run(3);

        public static string Run(int n)
        {
            var t0 = DateTime.Now;
            var text = "# 平衡模拟结果\n\n" + $"三档机器人玩家（新手 / 普通 / 熟练）连打 6 章 + 无尽，每档 {n} 次。生成于 {DateTime.Now:yyyy-MM-dd HH:mm}。\n\n" + RunAll(true, n) + "\n" + RunAll(false, n);
            text += $"\n用时 {(DateTime.Now - t0).TotalSeconds:0} 秒。\n";
            File.WriteAllText(Path.Combine(Application.dataPath, "../Docs/平衡模拟.md"), text);
            Debug.Log(text);
            return text;
        }
    }
}
