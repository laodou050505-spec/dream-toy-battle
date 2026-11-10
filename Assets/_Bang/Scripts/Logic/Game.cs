// 《砰！》核心规则（纯逻辑，不依赖 UnityEngine）。
// 坐标：玩家起点为原点，正前方 -Z，地面 y=0，单位米。
//
// 每帧 Update(dt, input)：
//   input = { head:{pos,fwd}, gaze:{origin,dir}, hands:{left, right} }
// 表现层读取 enemies/flags/paints/bullets/portals/weapons/shield 等状态，
// 并每帧取走 fx 事件队列（DrainFx）。
using System;
using System.Collections.Generic;
namespace Bang.Logic
{
    // ───────────────────────── mulberry32 RNG ─────────────────────────
    public static class Rng
    {
        /// <summary>
        /// 精确复刻 JS mulberry32：使用 uint 和 unchecked，复刻 Math.imul、>>>、|0。
        /// </summary>
        public static Func<double> Mulberry32(uint seed)
        {
            uint a = seed;
            return () => {
                unchecked {
                    a = a + 0x6d2b79f5u;
                    uint t = (a ^ (a >> 15)) * (1u | a);        // Math.imul(a^(a>>>15), 1|a)
                    t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t; // (t + Math.imul(t^(t>>>7), 61|t)) ^ t
                    return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296.0; // (t^(t>>>14))>>>0
                }
            };
        }
    }

    // ───────────────────────── 输入结构 ─────────────────────────
    public class GazeRay { public Vec3d origin; public Vec3d dir; }

    public class Hands
    {
        public HandOut left;
        public HandOut right;
        public HandOut Get(string side) { return side == "right" ? right : left; }
    }

    public class FrameInput
    {
        public Head head;
        public GazeRay gaze;
        public Hands hands;
    }

    // ───────────────────────── 游戏实体 ─────────────────────────
    public class Portal
    {
        public int id;
        public double angle;
        public Vec3d @base, normal, right;
        public double w, h;
        public Vec3d center, mouth, inner, far;
        public Vec3d deep;           // 出生点：门后小路上更深处（玩家透过门洞看得到它们一路走出来）
        public bool hasGate;         // 门洞底部可通过的横向范围（沿 right，相对 base）：每扇门各自一份（多门章节原来共用 0 号门的两个点，别的门上投影是错的 → 卡门）
        public double gateLo, gateHi;
        public double[] holeX, holeY;   // 门洞真实轮廓（门面局部：沿 right 的横向、离地高度，已往里收一点）：看得见门后的东西只认这个拱形，不认外接矩形
    }

    public class Enemy
    {
        public int id;
        public int bossPhase, bossCycle, bossSpecK; public double bossGuardT; public double bossT, bossFlagT; public List<double> bossVolley = new List<double>();   // Boss 专用；bossGuardT：换阶段后的护盾剩余秒数（期间伤害打折）
        public int bossSub, bossStage; public string bossMove = "", bossSrc = ""; public double bossMoveT;   // 锡兵招式轮换；当前在干什么：0 巡游 1 蓄力 2 出招 3 踉跄 4 插旗；bossSrc = 这一招是谁的（玩具箱之王学别人）
        public double okX, okZ, okT; public bool hasOk;
        public double longX, longZ, longT; public Vec3d detour; public double detourT;   // 3 秒挪不出 0.25 m（卡在两个摆件的夹角里来回滑）：先去侧面一个绕行点   // 上一帧不压任何障碍的位置：两个摆件夹缝太窄挤不过去时退回这里（不在夹缝里来回推、最后留在摆件里）
        public double hiddenT; public int stuckN;   // 连续卡住的次数、门后打不到的时长
        public double stuckWin, stuckX, stuckZ, ghostT;   // 卡住检测：想走却 0.8 秒没挪动 → ghostT 秒内不走绕行点、不卡门洞宽度，只贴着障碍边滑过去（用户：有的小怪卡在门里）
        public string kind;
        public KindDef k;
        public double hp, maxHp;
        public string elite;
        public double speedMul, armor;
        public double radius, height;
        public Vec3d pos;
        public Portal portal;
        public string state;
        public double t, life;
        public double yaw;
        public bool dead;
        public double slowT, flash;
        public int coins;
        public bool hidden;
        public double deadT;
        public bool bonus; public double bonusT;   // 无限火力期间涌出来的狂欢小怪：不插旗、不攻击，到处乱跑给玩家扫
        // AI 专用字段（全声明带默认值）
        public Vec3d spot;       // 预定点（引用，用于 reserve/release）
        public Vec3d post;       // shooter 的阵地
        public Vec3d orbitC;
        public double orbitA;
        public Vec3d climbTo;
        public bool shrunk;
        public double jt;
        public Vec3d guardPos;
        public Vec3d waitPos;
        public double summonT, orderT, orderAge;
        public string order;
        public List<int> minions = new List<int>();  // 已废弃，用commanderId反查
        public int commanderId;
        public bool planted;
        public double paintT;
        public Vec3d wp;
        public bool healing;
        public double fireT = 9999.0; // JS 中未初始化时为 undefined→NaN，永远不触发；C# 用大值模拟
        public bool tele;
        public int shots;
        public double volleyT;
        public Vec3d vel;
        public bool fired;
        public double blinkT;
        public bool paused;
        public double frozenT;   // 礼盒“冰冻”：定住的剩余时间
        public Vec3d arriveTo; public string arriveNext; public double arriveSpd;   // “先从门里出来”再进入本职状态（弹簧盒、气球兵、教学靶）
        public Vec3d arcFrom; public double arcK;                                   // 插旗兵出门后走的轻微弧线
    }

    public class Flag
    {
        public int id;
        public string type;
        public Vec3d pos;
        public double hp, maxHp;
        public double amount, cap, rate, radius;
        public bool hidden, dying;
        public int commanderId;
        public double age, flash;
        public double dyingFrom;
    }

    public class Paint
    {
        public int id;
        public Vec3d pos;
        public double amount, @base, life, maxLife;
    }

    public class Bullet
    {
        public int id;
        public string kind;
        public Vec3d pos, vel;
        public int from;
        public bool reflected;
        public double travelled;
        public int? target;
    }

    public class Weapon
    {
        public string gun;
        public int ammo;
        public ReloadState reload;
        public double fireCd;
        public List<BurstShot> burstQ = new List<BurstShot>();
        public double recoil;
    }

    public class ReloadState
    {
        public double t;
        public double? fastT;
        public bool jam;
        public bool released;
    }

    public class BurstShot { public double t; public Vec3d origin, dir; }

    public class ShieldState
    {
        public bool up;
        public string side;
        public double dur;
        public double broken;
        public double idle;
        public double upAge;
        public Vec3d pos;
        public Vec3d normal;
    }

    public class GazeState
    {
        public int? lock_;
        public int? cand;
        public double candT;
        public double lostT;
    }

    public class BinocState
    {
        public bool active;
        public int? cand;
        public double t;
    }

    public class LessonState
    {
        public string id;
        public LessonStep[] steps;
        public int i;
        public double t;
        public LessonData data;
    }

    public class LessonData
    {
        public double hold;
        public double t;
        public int[] ids;
        public bool trainerFire;
        public int trainer;
        public double b0, p0;
        public int e;
        // 通用附加字段（inside）
        public bool inside;
        public Enemy eRef;
    }

    public class LessonStep
    {
        public string text, sub, en, enSub;
        public string gesture = "gun";     // 教学卡上配的手势图：gun / flick / fist / palm / binoc / flag
        public Action<Game, LessonData> setup;
        public Func<Game, LessonData, double, bool> check;
    }

    public class RewardData
    {
        public int chapter;
        public string item;
        public bool fresh;
        public int stars;
        public double toy;
        public Stats stats;
    }

    public class StationData { public bool longPlay; public int minutes; }

    // ───────────────────────── 存档接口 ─────────────────────────
    public interface ISaveStore
    {
        SaveData Load();
        void Save(SaveData d);
    }

    public class SaveData
    {
        public int v;
        public Profile profile;
        public ResumeData resume;
    }

    public class ResumeData
    {
        public string mode;
        public int chapter, wave;
        public List<FlagSnapshot> flags;
    }

    public class FlagSnapshot
    {
        public string type;
        public Vec3d pos;
        public double hp, amount;
        public bool hidden;
    }

    public class Profile
    {
        public Dictionary<string, bool> unlocked;
        public int cleared;
        public Dictionary<int, int> stars;
        public int coins;
        public Dictionary<string, int> upgrades;
        public Dictionary<int, bool> lessonsDone;
        public Loadout loadout;
        public double armLen;
        public int endlessBest;
        public double playTime;
    }

    public class Loadout { public string right, left; }

    public class Stats
    {
        public int shots, hits, golds, parries, kills, coins;
    }

    // ───────────────────────── FX 事件 ─────────────────────────
    public class FxEvent { public string type; }
    public class SpawnFx : FxEvent { public int id; public string kind; }
    public class KillFx : FxEvent { public int id; public string kind; public Vec3d pos; public int coins; public string elite; }
    public class HitFx : FxEvent { public int id; public double dmg; public bool crit, weak; public Vec3d pos; public double hp, maxHp; }
    public class ShotFx : FxEvent { public string side, gun; public Vec3d from, dir; public bool hit; public Vec3d to; public int id; public bool crit; public double dmg; }
    public class FlagPlantedFx : FxEvent { public int id; public string flagType; public Vec3d pos; public bool hidden; }
    public class FlagHitFx : FxEvent { public int id; public double dmg, hp, maxHp; public Vec3d pos; }
    public class FrenzyEndFx : FxEvent { public int count; }
    /// <summary>Boss 即将登场（出场前 BossWarnDur 秒）：表现层出横幅、音效、场景变色、门口发光。</summary>
    public class BossWarnFx : FxEvent { public string kind; public double dur; public int portal; }
    public class FlagDownFx : FxEvent { public int id; public Vec3d pos; public bool byPlayer; }
    public class WaveStartFx : FxEvent { public int wave, total; public bool big; }
    public class PhaseFx : FxEvent { } // rest/station/lost/clear/reward/retry/quit 等
    public class RewardFx : PhaseFx { public RewardData reward; }
    public class LostFx : PhaseFx { public bool endless; }
    public class CommanderDownFx : FxEvent { public int id; }
    public class SummonFx : FxEvent { public int id; public int n; }
    public class OrderFx : FxEvent { public int id; public string order; }
    public class DiveFx : FxEvent { public int id; }
    public class EmergeFx : FxEvent { public int id; }
    public class TelegraphFx : FxEvent { public int id; }
    public class PlantingFx : FxEvent { public int id; }
    public class BlinkFx : FxEvent { public int id; }
    public class JackOpenFx : FxEvent { public int id; }
    public class JackCloseFx : FxEvent { public int id; }
    public class ExitFx : FxEvent { public int id; }
    public class EnemyShotFx : FxEvent { public int id; public int bullet; public string kind; }
    public class ShrinkFx : FxEvent { public int id; }
    public class ShieldFx : FxEvent { }
    public class ParryFx : FxEvent { public Vec3d pos; }
    public class BlockFx : FxEvent { public Vec3d pos; public double dur; }
    public class ReflectHitFx : FxEvent { public int id; }
    public class ReloadFx : FxEvent { public string side, how; }
    public class DryFx : FxEvent { public string side; }
    public class JamFx : FxEvent { public string side; }
    public class GoldHitFx : FxEvent { public string side; }
    public class MarkFx : FxEvent { public int id; }
    public class KnockedFx : FxEvent { }
    /// <summary>攻击打中了玩家的头：kind = foam / confetti / plane / ball，dir = 来袭方向（表现层据此把攻击留下的东西贴在那一侧）。</summary>
    public class HeadHitFx : FxEvent { public string kind; public Vec3d dir; }
    public class FireBlockedFx : FxEvent { public string side, why; }
    public class PinchFx : FxEvent { public string side; }
    public class LessonStepFx : FxEvent { public string text, sub, en, gesture; public int i, n; }
    public class LessonDoneFx : FxEvent { }
    public class EndlessOverFx : FxEvent { public int waves, best; }
    public class EndlessTravelFx : FxEvent { public int chapter, wave; }
    public class BinocFx : FxEvent { }

    // ───────────────────────── Counters ─────────────────────────
    public class Counters
    {
        public int blocked, parried, reloads, golds, marks, kills, shots;
    }

    public class RunState
    {
        public string mode;
        public int chapter, wave;
        public WaveDef waveDef;
        public List<string> queue = new List<string>();
        public double spawnT;
        public double hpMul;
        public double eliteChance;
        public double toyMul = 1;
        public int rapidCount, rapidWave = -1;        // 这一章给过几次无限火力、最近一次在第几波
        public bool bossWarned;                       // 下一个 Boss 已经预警过
        public Portal bossPortal;                     // 预警时就定好 Boss 从哪扇门出来（门口提示光打在那扇门上）
        public bool rapidGiven;                       // 这一章已经给过无限火力（用户：一直拿不到长枪 → 每章保底一次）
        public Stats stats = new Stats();
        public int assistWave = -1;
        public int waveLosses;
        public int assist;
        public int losses;          // 本章累计失败次数（星级用）
    }

    public class TargetInfo
    {
        public int id;
        public string type;
        public double y0, y1;   // 竖条判定的上下端（世界高度）；都是 0 = 按 pos 这一点算
        public Vec3d pos;
        public double radius;
        public object @ref; // Enemy or Flag
    }

    // ───────────────────────── Game ─────────────────────────
    public partial class Game
    {
        public Config cfg;
        public Func<double> rng;
        public ISaveStore store;
        public Profile profile;
        public ResumeData resume;

        public double time;
        public List<FxEvent> fx;
        public Head head;
        public GazeRay gazeRay;
        public string phase;
        public RunState run;
        public List<Portal> portals;
        public List<Enemy> enemies;
        public List<Flag> flags;
        public List<Paint> paints;
        public List<Bullet> bullets;
        public List<Vec3d> reserved;  // 预定点列表（引用相等）
        /// <summary>房间里的实物障碍（门前溢出的灌木、蘑菇灯、玩具车…，规则层坐标的圆：x, z, 半径）。
        /// 插旗点避开；地面上走的小怪碰到就沿边绕过去，不从里面穿过（场景表现层启动时登记）。</summary>
        public bool seated;                           // 坐姿模式（设置里切换）：门收窄到 ±45°
        public List<(double x, double z, double r)> obstacles = new List<(double x, double z, double r)>();
        /// <summary>门洞底部能走过去的横向范围（规则层坐标里门洞左右两个边缘点；null = 不限制）。门洞下宽上圆、底部平段比中段窄，
        /// 小怪穿门时横向位置要收在这里面（减去自身半径），不然身体会擦进门框。</summary>
        public Vec3d gateA, gateB;
        public double toy, toyPeak;
        public double phaseT;
        public Dictionary<int, double> marks;
        public GazeState gaze;
        public BinocState binoc;
        public ShieldState shield;
        public double knockT;
        public Counters counters;
        public LessonState lesson;
        public ResumeData snapshot; // 存为 ResumeData（含 wave + flags）
        public StationData station;
        public RewardData reward;
        public double sessionPlay;
        public double sinceBreak;          // 距上次中场休息（或开局）玩了多久
        public Weapons weapons;
        public Dictionary<string, string> lastHands; // side -> state
        /// <summary>测试用：覆盖每帧视线输入（null 则用帧输入中的 gaze 或头部朝向）</summary>
        public GazeRay GazeOverride;
        private int nextId;

        public class Weapons
        {
            public Weapon right, left;
            public Weapon Get(string side) { return side == "right" ? right : left; }
            public void Set(string side, Weapon w) { if (side == "right") right = w; else left = w; }
        }

        public Game(GameOpts opts = null)
        {
            if (opts == null) opts = new GameOpts();
            cfg = opts.cfg ?? Config.Default;
            rng = Rng.Mulberry32(opts.seed ?? 20261026u);
            store = opts.store;
            profile = DefaultProfile();
            resume = null;
            if (store != null) {
                var s = store.Load();
                if (s != null && s.v == 1) {
                    var def = DefaultProfile();
                    profile = MergeProfile(def, s.profile);
                    resume = s.resume;
                }
            }
            time = 0;
            fx = new List<FxEvent>();
            head = new Head { pos = new Vec3d(0, 1.6, 0), fwd = new Vec3d(0, 0, -1) };
            gazeRay = new GazeRay { origin = head.pos, dir = head.fwd };
            phase = "menu";
            ResetRun();
        }

        private Profile MergeProfile(Profile def, Profile src)
        {
            // 合并 unlocked
            var unlocked = new Dictionary<string, bool>(def.unlocked);
            if (src.unlocked != null) foreach (var kv in src.unlocked) unlocked[kv.Key] = kv.Value;
            // 合并 upgrades
            var upgrades = new Dictionary<string, int>(def.upgrades);
            if (src.upgrades != null) foreach (var kv in src.upgrades) upgrades[kv.Key] = kv.Value;
            return new Profile {
                unlocked = unlocked,
                cleared = src.cleared,
                stars = src.stars ?? new Dictionary<int, int>(),
                coins = src.coins,
                upgrades = upgrades,
                lessonsDone = src.lessonsDone ?? new Dictionary<int, bool>(),
                loadout = src.loadout ?? new Loadout { right = "pistol", left = "pistol" },
                armLen = src.armLen != 0 ? src.armLen : def.armLen,
                endlessBest = src.endlessBest,
                playTime = src.playTime,
            };
        }

        public static Profile DefaultProfile()
        {
            return new Profile {
                unlocked = new Dictionary<string, bool> {
                    {"pistol",true},{"shield",false},{"binoc",false},{"dual",false},
                    {"burst",false},{"cannon",false},{"bubble",false},{"endless",false}
                },
                cleared = 0,
                stars = new Dictionary<int, int>(),
                coins = 0,
                upgrades = new Dictionary<string, int> {
                    {"mag",0},{"reload",0},{"gold",0},{"shield",0},{"parry",0}
                },
                lessonsDone = new Dictionary<int, bool>(),
                loadout = new Loadout { right = "pistol", left = "pistol" },
                armLen = Config.Default.arm.defaultLen,
                endlessBest = 0,
                playTime = 0,
            };
        }

        // ───────────────────────── 存档 ─────────────────────────
        public void Persist()
        {
            if (store == null) return;
            store.Save(new SaveData { v = 1, profile = profile, resume = resume });
        }

        public void SaveNow()
        {
            if (run.mode == "chapter" && snapshot != null && (phase == "wave" || phase == "lost")) {
                resume = new ResumeData { mode = "chapter", chapter = run.chapter, wave = snapshot.wave, flags = snapshot.flags };
            }
            Persist();
        }

        // ───────────────────────── 局内状态 ─────────────────────────
        public void ResetRun()
        {
            nextId = 1;
            run = new RunState { mode = null };
            portals = new List<Portal>();
            enemies = new List<Enemy>();
            flags = new List<Flag>();
            paints = new List<Paint>();
            bullets = new List<Bullet>();
            reserved = new List<Vec3d>();
            toy = 0; toyPeak = 0;
            phaseT = 0;
            marks = new Dictionary<int, double>();
            gaze = new GazeState();
            binoc = new BinocState();
            shield = new ShieldState { dur = ShieldMax() };
            knockT = 0;
            gifts.Clear(); catapult = null; goldT = 0; rapidT = 0;
            counters = new Counters();
            lesson = null;
            snapshot = null;
            station = null;
            reward = null;
            sessionPlay = 0;
            SetupWeapons();
        }

        public double ShieldMax()
        {
            int lv = profile?.upgrades != null && profile.upgrades.ContainsKey("shield") ? profile.upgrades["shield"] : 0;
            return cfg.shield.durability + UPGRADES.shield.values[lv];
        }

        public int MagOf(string gunId)
        {
            var g = GUNS.Get(gunId);
            int lv = profile.upgrades.ContainsKey("mag") ? profile.upgrades["mag"] : 0;
            double bonus0 = UPGRADES.mag.values[lv];
            int bonus;
            if (gunId == "burst") bonus = (int)(bonus0 * 3);
            else if (gunId == "cannon") bonus = (bonus0 >= 2) ? 1 : 0;
            else bonus = (int)bonus0;
            return g.mag + bonus;
        }

        public (double total, double goldStart, double goldEnd, double fast, double jam) ReloadTimes()
        {
            var R = cfg.reload;
            int lv = profile.upgrades.ContainsKey("reload") ? profile.upgrades["reload"] : 0;
            double m = UPGRADES.reload.values[lv];
            int lvGold = profile.upgrades.ContainsKey("gold") ? profile.upgrades["gold"] : 0;
            double gw = UPGRADES.gold.values[lvGold];
            return (R.total * m, R.goldStart * m, R.goldEnd * m + gw, R.fastFinish, R.jam);
        }

        public void SetupWeapons()
        {
            var p = profile;
            Weapon mk(string gunId)
            {
                if (gunId == null) return null;
                return new Weapon { gun = gunId, ammo = MagOf(gunId) };
            }
            string right = (p.unlocked.ContainsKey(p.loadout.right) && p.unlocked[p.loadout.right]) ? p.loadout.right : "pistol";
            string left = (p.unlocked.ContainsKey("dual") && p.unlocked["dual"]) ?
                ((p.unlocked.ContainsKey(p.loadout.left) && p.unlocked[p.loadout.left]) ? p.loadout.left : "pistol") : null;
            weapons = new Weapons { right = mk(right), left = mk(left) };
        }

        public bool SetLoadout(string side, string gunId)
        {
            if (!profile.unlocked.ContainsKey(gunId) || !profile.unlocked[gunId] || GUNS.Get(gunId) == null) return false;
            if (side == "left" && !(profile.unlocked.ContainsKey("dual") && profile.unlocked["dual"])) return false;
            profile.loadout = new Loadout { right = profile.loadout.right, left = profile.loadout.left };
            if (side == "right") profile.loadout.right = gunId; else profile.loadout.left = gunId;
            SetupWeapons();
            Persist();
            return true;
        }

        /// <summary>改装共退还多少金币（每一级的花费加起来）。</summary>
        public int UpgradeRefund()
        {
            int sum = 0;
            if (profile?.upgrades == null) return 0;
            foreach (var kv in profile.upgrades)
            {
                var u = UPGRADES.Get(kv.Key); if (u == null) continue;
                for (int i = 0; i < kv.Value && i < u.costs.Length; i++) sum += u.costs[i];
            }
            return sum;
        }

        /// <summary>重置全部改装（用户：强化买了以后难度一直是降低的，要能还原）：等级清零、花掉的金币原数退还。返回退还的金币。</summary>
        public int ResetUpgrades()
        {
            int refund = UpgradeRefund();
            if (refund <= 0) return 0;
            profile.coins += refund;
            foreach (var k in new List<string>(profile.upgrades.Keys)) profile.upgrades[k] = 0;
            SetupWeapons();
            shield.dur = ShieldMax();
            Persist();
            return refund;
        }

        public bool BuyUpgrade(string key)
        {
            var u = UPGRADES.Get(key);
            if (u == null) return false;
            int lv = profile.upgrades.ContainsKey(key) ? profile.upgrades[key] : 0;
            if (lv >= u.costs.Length) return false;
            int cost = u.costs[lv];
            if (profile.coins < cost) return false;
            profile.coins -= cost;
            profile.upgrades[key] = lv + 1;
            SetupWeapons();
            shield.dur = ShieldMax();
            Persist();
            return true;
        }

        public void Emit(FxEvent e) { fx.Add(e); }
        public List<FxEvent> DrainFx() { var f = fx; fx = new List<FxEvent>(); return f; }

        // ───────────────────────── 传送门 ─────────────────────────
        public void MakePortals(double[] angles)
        {
            var P = cfg.portal;
            portals = new List<Portal>();
            for (int i = 0; i < angles.Length; i++) {
                double deg = angles[i];
                double a = deg * V.DEG;
                Vec3d @out = new Vec3d(Math.Sin(a), 0, -Math.Cos(a));
                Vec3d @base = V.scale(@out, P.dist);
                Vec3d n = V.scale(@out, -1);
                Vec3d right = new Vec3d(-n.z, 0, n.x);
                portals.Add(new Portal {
                    id = i, angle = deg, @base = @base, normal = n, right = right, w = P.width, h = P.height,
                    center = new Vec3d(@base.x, P.height / 2, @base.z),
                    mouth = V.add(@base, V.scale(n, 0.35)),
                    inner = V.sub(@base, V.scale(n, P.inside)),
                    deep = V.sub(@base, V.scale(n, P.inside + P.spawnDepth)),
                    far = V.sub(@base, V.scale(n, P.commanderBehind)),
                });
                if (_gateDefault is (double glo, double ghi)) SetGate(i, glo, ghi);
                if (_holeDefault is (double[] hx, double[] hy)) { portals[i].holeX = hx; portals[i].holeY = hy; }
            }
        }

        public void MovePortal(int i, Vec3d @base, Vec3d normal)
        {
            if (i < 0 || i >= portals.Count) return;
            var p = portals[i];
            var P = cfg.portal;
            Vec3d n = V.norm(new Vec3d(normal.x, 0, normal.z));
            p.@base = new Vec3d(@base.x, 0, @base.z);
            p.normal = n;
            p.right = new Vec3d(-n.z, 0, n.x);
            p.center = new Vec3d(@base.x, P.height / 2, @base.z);
            p.mouth = V.add(p.@base, V.scale(n, 0.35));
            p.inner = V.sub(p.@base, V.scale(n, P.inside));
            p.deep = V.sub(p.@base, V.scale(n, P.inside + P.spawnDepth));
            p.far = V.sub(p.@base, V.scale(n, P.commanderBehind));
        }

        // 性能：以下几个门相关判断每帧被“敌人 × 障碍 × 推开轮次”调用上千次，原来每次都 new 临时 Vec3d（满场每帧 50KB+ 垃圾，引发 GC 卡顿）。
        // 改成纯标量运算，运算顺序和原来 V.dot(V.sub(...)) 完全一致，结果逐位相同。
        public double PortalSide(Portal p, Vec3d pos) => PortalSide(p, pos.x, pos.y, pos.z);
        double PortalSide(Portal p, double x, double y, double z) { var b = p.@base; var n = p.normal; return (x - b.x) * n.x + (y - b.y) * n.y + (z - b.z) * n.z; }
        double PortalLat(Portal p, double x, double y, double z) { var b = p.@base; var r = p.right; return (x - b.x) * r.x + (y - b.y) * r.y + (z - b.z) * r.z; }
        /// <summary>origin→pos 这条线段和门面的交点（门面局部：横向 lx、世界高度 hy、世界 hx/hz）；不相交返回 false。</summary>
        bool HitWall(Portal p, Vec3d origin, Vec3d pos, out double hx, out double hy, out double hz)
        {
            hx = hy = hz = 0;
            var n = p.normal; var b = p.@base;
            double dx = pos.x - origin.x, dy = pos.y - origin.y, dz = pos.z - origin.z;
            double den = dx * n.x + dy * n.y + dz * n.z;
            if (Math.Abs(den) < 1e-6) return false;
            double t = ((b.x - origin.x) * n.x + (b.y - origin.y) * n.y + (b.z - origin.z) * n.z) / den;
            if (t < 0 || t > 1) return false;
            hx = origin.x + dx * t; hy = origin.y + dy * t; hz = origin.z + dz * t;
            return true;
        }

        /// <summary>门口已玩具化（墙面透出门后世界）的半径（米，表现层每帧给）：视线穿过门所在墙面、落在门口这一圈里，也算看得见。</summary>
        public double revealRadius;
        /// <summary>看得见门后的东西：穿过门洞，或者穿过门口已经玩具化、透出门后世界的那片墙（用户：看得见却打不到）。</summary>
        public bool SeenBehind(Portal p, Vec3d origin, Vec3d pos)
        {
            if (ThroughPortal(p, origin, pos)) return true;
            if (revealRadius < 0.3) return false;
            if (!HitWall(p, origin, pos, out double hx, out double hy, out double hz)) return false;
            var c = p.center; double ex = hx - c.x, ey = hy - c.y, ez = hz - c.z;
            return Math.Sqrt(ex * ex + ey * ey + ez * ez) <= revealRadius * 0.8;      // 覆盖边缘是软的、有舌头：只认里面八成
        }

        public bool ThroughPortal(Portal p, Vec3d origin, Vec3d pos)
        {
            if (!HitWall(p, origin, pos, out double hx, out double hy, out double hz)) return false;
            double lx = PortalLat(p, hx, hy, hz);
            if (p.holeX != null && p.holeX.Length >= 3) return InPoly(p.holeX, p.holeY, lx, hy);
            return Math.Abs(lx) <= p.w / 2 && hy >= 0 && hy <= p.h;
        }

        static bool InPoly(double[] xs, double[] ys, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
                if ((ys[i] > y) != (ys[j] > y) && x < (xs[j] - xs[i]) * (y - ys[i]) / (ys[j] - ys[i]) + xs[i]) inside = !inside;
            return inside;
        }

        /// <summary>按门洞真实轮廓设所有门的拱形（门面局部 横向 / 高度；表现层从 PortalArch 读出来给）。所有门是同一个门洞复制的。</summary>
        public void SetHoleAll(double[] xs, double[] ys)
        {
            _holeDefault = (xs, ys);
            if (portals != null) foreach (var p in portals) { p.holeX = xs; p.holeY = ys; }
        }
        (double[] xs, double[] ys)? _holeDefault;

        /// <summary>玩家看不看得见这个敌人（用户：没看见他，他的框就出来了）。整个身子到了房间这一侧 = 看得见；
        /// 还有一部分在门墙后面：身上取几个点（头、身子中间、左右两侧、脚），任意一点的视线穿过门洞（或门口已玩具化透出门后世界的那片墙）才算。
        /// 锁定、吸附、命中、血量环、锁定框、冰冻徽章、视野外提示都用这一个判定，不会出现“框到了、人没看到”。</summary>
        public bool CanSee(Enemy e, Vec3d eye)
        {
            if (e == null || e.portal == null) return true;
            var p = e.portal;
            double side = PortalSide(p, e.pos);
            if (side >= e.radius + 0.05 || e.pos.y > p.h + 0.3) return true;   // 已经整个出了门墙（飞在门顶以上的也在房间里）
            double y0 = e.pos.y, h = Math.Max(0.1, e.height);
            var r = p.right; double rr = e.radius * 0.8;
            var q = _visQ;
            foreach (var (dl, dy) in VisPts)
            {
                q.x = e.pos.x + r.x * dl * rr; q.y = y0 + h * dy; q.z = e.pos.z + r.z * dl * rr;
                if (PortalSide(p, q) >= 0 || SeenBehind(p, eye, q)) return true;   // 在门面这一侧的点：视线不穿门面，直接看得见
            }
            return false;
        }
        /// <summary>门后某个点（机关、礼盒、积木球）此刻看不看得见：在房间这一侧，或视线穿过门洞 / 门口玩具化的墙。</summary>
        public bool CanSeePoint(Portal p, Vec3d pos) => p == null || PortalSide(p, pos) >= 0 || SeenBehind(p, head.pos, pos);
        readonly Vec3d _visQ = new Vec3d();                                  // CanSee 的取样点（复用，不每次 new；只在本函数内用）
        static readonly (double dl, double dy)[] VisPts = { (0, 0.5), (0, 0.9), (0, 0.15), (-1, 0.5), (1, 0.5) };

        // ───────────────────────── 开局 ─────────────────────────
        public bool StartChapter(int ch, StartChapterOpts opts = null)
        {
            if (opts == null) opts = new StartChapterOpts();
            var C = CHAPTERS.Get(ch);
            if (C == null) return false;
            ResetRun();
            run.mode = "chapter";
            run.chapter = ch;
            run.eliteChance = C.eliteChance;
            run.toyMul = C.toyMul;
            MakePortals(CHAPTERS.PortalAngles(ch, seated));
            InitMechanics();
            var savedResume = opts.resume;
            if (savedResume != null) {
                RestoreSnapshot(savedResume);
                BeginWave(savedResume.wave);
            } else if (!string.IsNullOrEmpty(C.lesson) && !profile.lessonsDone.ContainsKey(ch) && LESSONS.ContainsKey(C.lesson) && !opts.skipLesson) {
                StartLesson(C.lesson);
            } else {
                BeginWave(0);
            }
            return true;
        }

        public bool ContinueGame()
        {
            if (resume == null) return false;
            return StartChapter(resume.chapter, new StartChapterOpts { resume = resume });
        }

        /// <summary>无尽模式一律四扇门（用户：无尽模式的门一直是四个门），场景按 Boss 段换，门不跟着那一关变。坐姿模式照样收窄。</summary>
        public double[] EndlessDoors() => CHAPTERS.PortalAngles(CHAPTERS.Count, seated);

        /// <summary>开始无尽；fromWave &gt; 0 = 续玩（用户：无尽可以中途暂停，回来还是那个波数，但从那一波开头重来）。</summary>
        public bool StartEndless(int fromWave = 0)
        {
            if (!profile.unlocked.ContainsKey("endless") || !profile.unlocked["endless"]) return false;
            ResetRun();
            run.mode = "endless";
            run.chapter = EndlessMode.ChapterFor(fromWave + 1);                 // 场景按这一波所在的 Boss 段
            run.toyMul = 1;
            MakePortals(EndlessDoors());
            InitMechanics();                                                     // 礼盒 / 无限火力 / 门涌照常有
            BeginWave(fromWave);                                                 // 从那一波开头重来：旗子清空、玩具化清零
            return true;
        }

        /// <summary>有没有可续玩的无尽进度（存档里的 resume.mode == "endless"）。</summary>
        public bool HasEndlessResume => resume != null && resume.mode == "endless";
        public bool ContinueEndless() => HasEndlessResume && StartEndless(resume.wave);

        public void BackToMenu()
        {
            if (phase == "wave" || phase == "rest" || phase == "station") SaveNow();
            phase = "menu";
            ResetRun();
        }

        // ───────────────────────── 教学 ─────────────────────────
        public void StartLesson(string id)
        {
            phase = "lesson";
            lesson = new LessonState { id = id, steps = LESSONS[id], i = -1, t = 0, data = new LessonData() };
            NextLessonStep();
        }

        public void NextLessonStep()
        {
            var L = lesson;
            L.i++;
            L.t = 0;
            L.data.hold = 0;
            L.data.t = 0;
            if (L.i >= L.steps.Length) {
                profile.lessonsDone[run.chapter] = true;
                Persist();
                enemies = new List<Enemy>();
                bullets = new List<Bullet>();
                lesson = null;
                Emit(new LessonDoneFx { type = "lessonDone" });
                BeginWave(0);
                return;
            }
            var s = L.steps[L.i];
            s.setup?.Invoke(this, L.data);
            Emit(new LessonStepFx { type = "lessonStep", text = Loc.T(s.text, s.en), sub = Loc.T(s.sub ?? "", s.enSub ?? ""), en = s.en, gesture = s.gesture, i = L.i, n = L.steps.Length });
        }

        public LessonStepFx LessonText()
        {
            if (lesson == null) return null;
            var s = lesson.steps[lesson.i];
            return s != null ? new LessonStepFx { text = Loc.T(s.text, s.en), sub = Loc.T(s.sub ?? "", s.enSub ?? ""), en = s.en, gesture = s.gesture, i = lesson.i, n = lesson.steps.Length } : null;
        }

        // ───────────────────────── 波次 ─────────────────────────
        public WaveDef WaveDefFor(int i)
        {
            if (run.mode == "endless") return EndlessMode.EndlessWave(i + 1, rng);
            return CHAPTERS.Get(run.chapter).waves[i];
        }

        public void BeginWave(int i)
        {
            var W = WaveDefFor(i);
            if (run.assistWave != i) { run.assistWave = i; run.waveLosses = 0; }
            run.assist = Math.Min(run.waveLosses, 5);
            run.wave = i;
            run.waveDef = W;
            run.bossWarned = false; bossWarnT = 0; run.bossPortal = null;
            run.hpMul = W.hpMul;
            if (run.mode == "endless") run.eliteChance = W.eliteChance;
            var q = new List<string>();
            double cm = run.mode == "chapter" && run.chapter > 1 ? cfg.diff.CountMul(CHAPTERS.World(run.chapter)) : 1;   // 第 1 章是入门章：数量不加（旗子长得快、出怪间隔照样加）
            cm *= 1 - cfg.diff.assistCountPer * Math.Max(0, run.assist - 2);          // 连续失败 3 次起，这一波少出几只
            foreach (var (k, n) in W.spawns) { int nn = k == "commander" || k.StartsWith("boss") ? n : (int)Math.Round(n * cm); for (int j = 0; j < nn; j++) q.Add(k); }
            // Fisher-Yates shuffle
            for (int j = q.Count - 1; j > 0; j--) {
                int r = (int)Math.Floor(rng() * (j + 1));
                var tmp = q[j]; q[j] = q[r]; q[r] = tmp;
            }
            // 章末 Boss：最后一波加一只，放在队列 40% 处登场（先清一批小怪热身，Boss 出来时场上不至于太挤）
            string boss = run.mode == "chapter" && i == CHAPTERS.Get(run.chapter).waves.Length - 1 ? BossOf(run.chapter) : null;
            if (boss != null) q.Add(boss);
            // Boss 不在一开场出来（用户：放到这一波的中后段）：先清一大半小怪；两只 Boss 时一只在 55%、一只在 85%
            var bosses = q.FindAll(k => k.StartsWith("boss")); q.RemoveAll(k => k.StartsWith("boss"));
            for (int bi = 0; bi < bosses.Count; bi++)
            {
                double at = bosses.Count == 1 ? 0.7 : (bi == 0 ? 0.55 : 0.85);
                q.Insert(Math.Min(q.Count, (int)Math.Round(q.Count * at)), bosses[bi]);
            }
            // 指挥官放在前三分之一出场
            int ci = q.IndexOf("commander");
            if (ci > 0) {
                q.RemoveAt(ci);
                q.Insert(Math.Min(2, q.Count), "commander");
            }
            run.queue = q;
            run.spawnT = 1.5;
            phase = "wave";
            phaseT = 0;
            ScheduleMechanics(i);
            snapshot = TakeSnapshot();
            if (run.mode == "chapter") {
                resume = new ResumeData { mode = "chapter", chapter = run.chapter, wave = run.wave, flags = snapshot.flags };
                Persist();
            } else if (run.mode == "endless") {                                   // 无尽：每波开头存一次进度（续玩时从这一波开头重来，不带旗子）
                resume = new ResumeData { mode = "endless", chapter = run.chapter, wave = run.wave };
                Persist();
            }
            Emit(new WaveStartFx { type = "waveStart", wave = i, total = run.mode == "endless" ? 0 : CHAPTERS.Get(run.chapter).waves.Length, big = W.big });   // 终极决战 6 波，其余 5 波
        }

        private ResumeData TakeSnapshot()
        {
            var snaps = new List<FlagSnapshot>();
            foreach (var f in flags) {
                if (!f.dying) snaps.Add(new FlagSnapshot { type = f.type, pos = V.clone(f.pos), hp = f.hp, amount = f.amount, hidden = f.hidden });
            }
            return new ResumeData { wave = run.wave, flags = snaps };
        }

        private void RestoreSnapshot(ResumeData s)
        {
            flags = new List<Flag>();
            foreach (var f in (s.flags ?? new List<FlagSnapshot>())) {
                var F = FLAGS.Get(f.type);
                flags.Add(new Flag { id = nextId++, type = f.type, pos = V.clone(f.pos), hp = f.hp, maxHp = (int)F.hp, amount = f.amount, cap = F.cap, rate = F.rate, radius = F.radius, hidden = f.hidden, dying = false, age = 99 });
            }
            enemies = new List<Enemy>();
            bullets = new List<Bullet>();
            paints = new List<Paint>();
            reserved = new List<Vec3d>();
            RecomputeToy();
        }

        private int AliveCount()
        {
            int c = 0; foreach (var e in enemies) if (!e.dead && !e.bonus) c++; return c;   // 狂欢小怪不拖住波次结束
        }

        public Enemy SpawnEnemy(string kind, SpawnOpts opts = null)
        {
            if (opts == null) opts = new SpawnOpts();
            var K = KINDS.Get(kind);
            var portal = opts.portal ?? portals[(int)Math.Floor(rng() * portals.Count)];
            if (opts.portal == null && portals.Count > 1)                // Boss 在场：小怪换一扇门出来，不挤在 Boss 身边；第二个 Boss 也不和第一个同门（用户：Boss 同一个门会穿模）
                portal = kind.StartsWith("boss") ? BossDoor(portal) : FreeDoor(portal);
            string elite = opts.eliteOverride; // null = 不强制
            bool eliteExplicit = opts.eliteExplicit;
            bool isBoss = kind.StartsWith("boss");
            // Boss 不参与精英词条（用户：坦克 Boss 身上有残影、血量各自独立，连本体一共三个——是抽到了“成群”，一次生成 3 只叠在一起）。
            // Boss 的血量 / 速度 / 招式是单独调的，金身、迅捷也不给。随机数照样消耗一次，后面的出怪顺序不变
            if (!eliteExplicit && run.eliteChance > 0 &&
                kind != "commander" && kind != "minion" && kind != "balloonSmall" &&
                rng() < run.eliteChance && !isBoss)
            {
                int idx = (int)Math.Floor(rng() * ELITES.Keys.Count);
                elite = ELITES.Keys[idx];
            }
            if (isBoss) elite = null;                                    // 保险：外部强制指定也不给 Boss 词条
            if (elite == "swarm" && !opts.fromSwarm) {
                var out0 = new List<Enemy>();
                for (int ii = 0; ii < ELITES.swarm.copies; ii++) {
                    out0.Add(SpawnEnemy(kind, new SpawnOpts {
                        portal = portal, eliteOverride = "swarm", eliteExplicit = true, fromSwarm = true,
                        offset = (ii - 1) * 0.35, commanderId = opts.commanderId,
                    }));
                }
                return out0[0];
            }
            var El = elite != null ? ELITES.Get(elite) : null;
            double hpCalc = K.hp;                                       // 血量只由种类决定（用户：外观一样的敌人血量必须一样）：精英不改血量
            if (run.mode == "endless") hpCalc = Math.Max(1, Math.Round(K.hp * run.hpMul));   // 11-05 用户：无尽模式血量更厚（同一波同种敌人血量一样，随波数变厚）
            var e = new Enemy {
                id = nextId++, kind = kind, k = K,
                hp = hpCalc, maxHp = hpCalc, elite = elite,
                speedMul = El?.speedMul ?? 1,
                armor = 0,
                radius = K.radius, height = K.height,
                pos = V.clone(opts.pos ?? HiddenSpawn(portal)),
                portal = portal,
                state = "enter", t = 0, life = 0,
                yaw = Math.Atan2(-portal.normal.x, -portal.normal.z),
                dead = false, slowT = 0, flash = 0,
                coins = (int)Math.Round(K.coins * (El?.coinMul ?? 1)),
            };
            if (opts.offset != 0)
            {
                // 召唤 / 成群：横向偏移不再叠在门后出生点（已在横向 1.2 m）上——叠上去最远 2.8 m，进了门后世界的墙（用户：敌人不要卡在墙里）。
                // 门后可走范围是门洞两侧各约 1.4 m：超出的部分改成往门后更深处错开（还是从门洞看不到的位置出生，不会叠在一起）
                double lat = V.dot(V.sub(e.pos, portal.@base), portal.right) + opts.offset;
                double max = 1.4 - e.radius, over = Math.Abs(lat) - max;
                if (over > 0) { double cl = Math.Sign(lat) * max; e.pos = V.add(e.pos, V.scale(portal.right, cl - (lat - opts.offset))); e.pos = V.sub(e.pos, V.scale(portal.normal, Math.Min(1.2, over))); }
                else e.pos = V.add(e.pos, V.scale(portal.right, opts.offset));
            }
            if (K.hidden) e.hidden = true;
            if (opts.pos == null) { var q = InBox(e.pos, BodyR(e) + 0.05); e.pos.x = q.x; e.pos.z = q.z; }   // 召唤 / 成群的横向偏移也不出门后世界的墙
            InitAI(e, opts);
            enemies.Add(e);
            Emit(new SpawnFx { type = "spawn", id = e.id, kind = kind });
            return e;
        }

        /// <summary>没有活着的 Boss 占着的门（从 want 开始往后找）；都被占了就用 want。</summary>
        Portal FreeDoor(Portal want)
        {
            for (int i = 0; i < portals.Count; i++)
            {
                var p = portals[(want.id + i) % portals.Count]; bool taken = false;
                foreach (var x in enemies) if (!x.dead && IsBoss(x) && x.portal == p) { taken = true; break; }
                if (!taken) return p;
            }
            return want;
        }
        /// <summary>Boss 选门：避开已有 Boss 的门，多门时尽量隔开一扇（两个 Boss 离得远，不互相挡）。</summary>
        Portal BossDoor(Portal want)
        {
            var taken = new List<Portal>(); foreach (var x in enemies) if (!x.dead && IsBoss(x)) taken.Add(x.portal);
            if (taken.Count == 0 || portals.Count < 2) return want;
            Portal best = want; double bestD = -1;
            foreach (var p in portals)
            {
                if (taken.Contains(p)) continue;
                double d = double.MaxValue; foreach (var t in taken) d = Math.Min(d, Math.Abs(p.angle - t.angle));
                if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>门后出生点：门后 1.6 m、往左或右偏 1.2 m（墙挡着，玩家透过门洞看不到出生那一下；原来生在门后正中、在视线里凭空冒出来——
        /// 用户：有的小怪不是从门里出来的）。小怪从旁边走进门洞的视野，再走出门。</summary>
        Vec3d HiddenSpawn(Portal p)
        {
            var d = p.deep ?? p.inner;
            double side = rng() < 0.5 ? -1 : 1;
            return InBox(V.add(d, V.scale(p.right, side * 1.2)), 0.45);
        }
        /// <summary>把点夹进门后世界的墙里（没有墙原样返回）。</summary>
        public Vec3d InBox(Vec3d q, double r)
        {
            if (q == null || !(worldBox is (double x0, double x1, double z0, double z1))) return q;
            return new Vec3d(Math.Max(x0 + r, Math.Min(x1 - r, q.x)), q.y, Math.Max(z0 + r, Math.Min(z1 - r, q.z)));
        }

        private void InitAI(Enemy e, SpawnOpts opts)
        {
            var K = e.k;
            var p = e.portal;
            switch (K.ai) {
                case "plane":
                {
                    // 从门洞上半部飞出来（原来直接出现在门外半空，用户：小怪只能从门里出来）
                    e.pos = new Vec3d(e.pos.x, 1.2, e.pos.z);
                    e.state = "arrive"; e.arriveTo = V.add(p.mouth, new Vec3d(0, 1.5, 0)); e.arriveNext = "circle"; e.arriveSpd = 0.9;
                    e.orbitC = new Vec3d(p.mouth.x * 0.6, 1.9, p.mouth.z * 0.6);
                    e.orbitA = rng() * Math.PI * 2;
                    break;
                }
                case "jack": {
                    var spot = PickSpot(2.0, 2.8, null);
                    if (opts.pos == null) { e.state = "arrive"; e.arriveTo = new Vec3d(spot.x, 0, spot.z); e.arriveNext = "closed"; e.arriveSpd = 0.7; }   // 从门里滑出来，不凭空冒出
                    else { e.pos = new Vec3d(spot.x, 0, spot.z); e.state = "closed"; }
                    e.jt = 1.0;
                    e.spot = spot;
                    Reserve(spot);
                    e.hidden = false;
                    break;
                }
                case "commander":
                {
                    // 指挥官站在门后远处（第 3 章望远镜玩法：远处伤害减半，标记后暴击），但不凭空出现：
                    // 从门后侧面（墙挡着、看不到的地方）走到指挥位置（用户：有的小怪不是从门里出来的）
                    double side = rng() < 0.5 ? -1 : 1;
                    e.pos = InBox(V.add(p.far, V.scale(p.right, side * 1.4)), 0.5); e.pos.y = 0;
                    e.state = "approach";
                    e.summonT = 0;
                    e.orderT = 4;
                    e.order = null;
                    break;
                }
                case "minion":
                    e.commanderId = opts.commanderId;
                    e.planted = false;
                    break;
                case "balloon": {
                    var spot = opts.spot ?? PickSpot(1.3, 2.4, null);
                    Reserve(spot);
                    e.spot = spot;
                    if (opts.pos != null) { e.pos = V.clone(opts.pos); e.state = "descend"; }
                    else
                    {
                        e.pos = new Vec3d(e.pos.x, 1.3, e.pos.z);                   // 门洞高 1.8：气球从门洞上半部飘出来（出生在门后墙挡着的一侧）
                        e.state = "arrive"; e.arriveTo = new Vec3d(spot.x, 1.3, spot.z); e.arriveNext = "descend"; e.arriveSpd = 0.85;   // 用户：插旗的不要在场上待好久才插：直飞插旗点
                    }
                    break;
                }
                case "boss": InitBoss(e); break;
                case "rcCar":
                    e.paintT = 0;
                    e.wp = null;
                    break;
                case "dummy": case "trainer":
                    e.pos = V.clone(opts.pos);
                    e.state = "idle";
                    if (opts.fromDoor)
                    {
                        var d0 = HiddenSpawn(p);
                        e.arriveTo = V.clone(opts.pos); e.pos = new Vec3d(d0.x, opts.pos.y, d0.z);
                        e.state = "arrive"; e.arriveNext = "idle"; e.arriveSpd = 1.0;
                    }
                    e.fireT = 1.5;
                    break;
                default: break;
            }
        }

        // 在活动区里找一个插旗点：离其他旗子和预定点至少 flagGap
        public Vec3d PickSpot(double dmin, double dmax, double? prefAngle)
        {
            var A = cfg.arena;
            for (int tries = 0; tries < 40; tries++) {
                double gap = tries < 25 ? A.flagGap : A.flagGap * 0.5;
                double a = prefAngle ?? 0;
                a += (rng() * 2 - 1) * (prefAngle == null ? A.frontHalfAngle : 45);
                a = V.clamp(a, -A.frontHalfAngle - 10, A.frontHalfAngle + 10) * V.DEG;
                double d = dmin + rng() * (dmax - dmin);
                double dCap = cfg.portal.dist - 0.7;                    // 离门至少 0.7 m：贴着门两侧墙根的点，门洞宽度约束会让小怪永远走不到（卡在门口）
                if (d > dCap) d = dCap - rng() * 0.2;
                double side = Math.Min(dCap, 2.5 + 0.5 * Math.Cos(a));                   // 正前方最远 3 m，往两侧收到 2.5 m（原来门 3.2 m 时的距离）
                if (d > side) d = side - rng() * 0.15;
                Vec3d s = new Vec3d(Math.Sin(a) * d, 0, -Math.Cos(a) * d);
                if (roomClamp != null && !InDoorCorridor(s)) { var q = roomClamp(s, 0.35); if (q != null) s = new Vec3d(q.x, 0, q.z); }   // 站位 / 插旗点不能在真实墙 / 家具里
                bool ok = true;
                foreach (var f in flags) if (!f.dying && V.distXZ(f.pos, s) < gap) { ok = false; break; }
                if (ok) foreach (var o in obstacles) if (Math.Sqrt((s.x - o.x) * (s.x - o.x) + (s.z - o.z) * (s.z - o.z)) < o.r + 0.35) { ok = false; break; }
                if (ok) foreach (var r in reserved) if (V.distXZ(r, s) < gap) { ok = false; break; }
                if (ok || tries == 39) return s;
            }
            return new Vec3d(0, 0, -1.8);
        }

        public void Reserve(Vec3d s) { reserved.Add(s); }
        public void Release(Vec3d s)
        {
            if (s == null) return;
            // 按引用相等找，与 JS indexOf 一致
            int i = -1;
            for (int j = 0; j < reserved.Count; j++) if (object.ReferenceEquals(reserved[j], s)) { i = j; break; }
            if (i >= 0) reserved.RemoveAt(i);
        }

        // ───────────────────────── 每帧 ─────────────────────────
        /// <summary>性能剖析钩子：Update 里每个子步骤结束时调一次（参数 = 步骤号）。平时为空，零开销；编辑器剖析脚本挂上去计时 / 统计分配。</summary>
        public static System.Action<int> Probe;
        public void Update(double dt, FrameInput input = null)
        {
            dt = Math.Min(dt, 0.1);
            time += dt;
            phaseT += dt;
            if (phase == "wave" || phase == "rest" || phase == "station" || phase == "lesson") {
                profile.playTime += dt;
                sessionPlay += dt;
                sinceBreak += dt;
            }
            var h = input?.head ?? new Head { pos = new Vec3d(0, 1.6, 0), fwd = new Vec3d(0, 0, -1) };
            head = h;
            var handInput = input?.hands ?? new Hands();
            var gazeInput = GazeOverride ?? input?.gaze ?? new GazeRay { origin = h.pos, dir = h.fwd };
            gazeRay = gazeInput;
            lastHands = new Dictionary<string, string> {
                { "left", handInput.left?.state ?? null },
                { "right", handInput.right?.state ?? null },
            };

            if (phase == "menu" || phase == "reward" || phase == "endlessOver" || phase == "explore" || phase == "travel") {   // 自由观赏：和菜单一样不出怪、不插旗
                HandleMenuHands(handInput);
                return;
            }
            Probe?.Invoke(0);
            UpdateShieldAndBinoc(dt, handInput); Probe?.Invoke(1);
            UpdateGaze(dt, gazeInput); Probe?.Invoke(2);
            UpdateWeapons(dt, handInput); Probe?.Invoke(3);
            if (phase == "lost" || phase == "clear") {
                UpdateFlags(dt);
                UpdatePhase(dt);
                return;
            }
            UpdateSpawns(dt); Probe?.Invoke(4);
            UpdateMechanics(dt); Probe?.Invoke(5);
            // 注意：用索引循环，AI 可能在迭代中添加新元素（如指挥官召唤跟班、气球分裂）
            for (int i = 0; i < enemies.Count; i++) {
                var e = enemies[i];
                if (!e.dead) { UpdateEnemy(e, dt); TrackStuck(e, dt); AvoidObstacles(e); }      // 不管哪种走法（滑出门、跳、冲、被击退），最后都不许留在房间实物里
            }
            Probe?.Invoke(6);
            SeparateEnemies(); Probe?.Invoke(7);
            foreach (var e in enemies) if (!e.dead && e.k.move != "fly") AvoidObstacles(e);
            if (worldBox is (double bx0, double bx1, double bz0, double bz1))   // 原地夹紧（不 new）
                foreach (var e in enemies) if (!e.dead) { double rr = BodyR(e) + 0.05; e.pos.x = Math.Max(bx0 + rr, Math.Min(bx1 - rr, e.pos.x)); e.pos.z = Math.Max(bz0 + rr, Math.Min(bz1 - rr, e.pos.z)); }   // 飞的也不出门后世界的墙   // 推开以后再避一次障：不许被同伴挤进房间实物里
            enemies.RemoveAll(e => e.dead); Probe?.Invoke(8);
            UpdateBullets(dt); Probe?.Invoke(9);
            UpdateFlags(dt); Probe?.Invoke(10);
            var keysToRemove = new List<int>();
            foreach (var kv in marks) if (time > kv.Value) keysToRemove.Add(kv.Key);
            foreach (var k in keysToRemove) marks.Remove(k);
            if (knockT > 0) knockT -= dt;
            if (_knockGrace > 0) _knockGrace -= dt;
            if (lesson != null) UpdateLesson(dt);
            UpdatePhase(dt); Probe?.Invoke(11);
        }

        private void HandleMenuHands(Hands hands)
        {
            foreach (var s in new[]{"left","right"}) {
                var h = hands.Get(s);
                foreach (var ev in h?.events ?? new List<HandEvent>()) {
                    if (ev.type == "pinchStart") Emit(new PinchFx { type = "pinch", side = s });
                }
            }
        }

        private void UpdatePhase(double dt)
        {
            var Fl = cfg.flow;
            if (phase == "wave") {
                if (toy >= cfg.toy.loseAt) {
                    phase = "lost"; phaseT = 0; run.losses++; _endlessReported = false;
                    Emit(new LostFx { type = "lost", endless = run.mode == "endless" });
                    return;
                }
                if (run.queue.Count == 0 && AliveCount() == 0 && !SurgeWarning && rapidT <= 0 && !_finale) EndWave();   // 无限火力期间不收波：长枪在手就一直有东西打
            } else if (phase == "rest") {
                if (phaseT >= Fl.rest) BeginWave(run.wave + 1);
            } else if (phase == "station") {
                // 中场休息不自动开下一波：玩家可能摘了头显在休息，等他点“继续”
            } else if (phase == "lost") {
                // 房间被占满：停在失败画面，等玩家自己点“重试”（RetryAfterLoss）或“退出”（真机试玩：自动重来太突然）
                if (run.mode == "endless" && phaseT >= Fl.lostHold && !_endlessReported) {
                    _endlessReported = true;
                    int reached = run.wave;
                    profile.endlessBest = Math.Max(profile.endlessBest, reached);
                    if (HasEndlessResume) resume = null;                          // 这一局结束了：不再显示“继续无尽”
                    Persist();
                    phase = "endlessOver";
                    Emit(new EndlessOverFx { type = "endlessOver", waves = reached, best = profile.endlessBest });
                }
            } else if (phase == "clear") {
                foreach (var f in flags) if (!f.dying) KillFlag(f, false);
                paints.Clear();
                bool finale = run.mode == "chapter" && run.chapter == CHAPTERS.Count;   // 终极决战：结算前先演一段谢幕（表现层 FinaleShow），不是打完立刻弹结算（用户：结尾有些草率）
                if (phaseT >= (finale ? Fl.finaleHold : Fl.clearHold)) {
                    phase = "reward";
                    Emit(new RewardFx { type = "reward", reward = this.reward });
                }
            }
        }

        bool _endlessReported;

        /// <summary>失败画面上玩家点了“重试”：从本波开头重来（连输两次起退掉一部分玩具化，越输越容易）。</summary>
        public void RetryAfterLoss()
        {
            if (phase != "lost" || run.mode == "endless" || snapshot == null) return;
            run.waveLosses = (run.waveLosses) + 1;
            RestoreSnapshot(snapshot);
            if (run.waveLosses >= 2)
            {
                double keepK = Math.Max(0.3, 1 - cfg.diff.assistFlagRecede * (run.waveLosses - 1));
                foreach (var f in flags) f.amount *= keepK;
                RecomputeToy();
            }
            BeginWave(snapshot.wave);
            Emit(new PhaseFx { type = "retry" });
        }

        private void EndWave()
        {
            int W = run.wave;
            if (run.mode == "endless") {
                profile.endlessBest = Math.Max(profile.endlessBest, W + 1);
                Persist();
                if ((W + 1) % 5 == 0) { EnterTravel(EndlessMode.ChapterFor(W + 2)); return; }
                if (sinceBreak >= cfg.flow.breakAfter) EnterStation(); else EnterRest();
                return;
            }
            if (W >= CHAPTERS.Get(run.chapter).waves.Length - 1) { ClearChapter(); return; }   // 波数按关（终极决战 6 波，其余 5 波）
            // 第 3 波打完固定喘口气，由玩家自己决定什么时候开始下一波（用户要求）；之后再按连续游玩时长提醒
            if (W + 1 == cfg.flow.stationAfterWave || sinceBreak >= cfg.flow.breakAfter) EnterStation(); else EnterRest();
        }

        /// <summary>无尽：打倒这一段的 Boss 后换到下一个 Boss 的世界。规则层停在 travel（不出怪、不插旗），表现层换好场景、开好门后调 EndlessArrive。</summary>
        public int endlessNext;
        void EnterTravel(int ch)
        {
            phase = "travel"; phaseT = 0; bullets.Clear(); endlessNext = ch;
            Emit(new EndlessTravelFx { type = "travel", chapter = ch, wave = run.wave + 1 });
        }
        public void EndlessArrive(int ch)
        {
            if (phase != "travel") return;
            run.chapter = ch;
            MakePortals(EndlessDoors());                                         // 换了场景，门还是四扇
            InitMechanics();
            BeginWave(run.wave + 1);
        }

        private void EnterRest()
        {
            phase = "rest"; phaseT = 0; bullets.Clear();
            if (run.mode == "chapter") {
                resume = new ResumeData { mode = "chapter", chapter = run.chapter, wave = run.wave + 1, flags = TakeSnapshot().flags };
                Persist();
            }
            Emit(new PhaseFx { type = "rest" });
        }

        private void EnterStation()
        {
            phase = "station"; phaseT = 0; bullets.Clear();
            bool longPlay = sessionPlay >= cfg.flow.longPlayRemind;
            sinceBreak = 0;
            station = new StationData { longPlay = longPlay, minutes = (int)Math.Floor(sessionPlay / 60) };
            if (run.mode == "chapter") {
                resume = new ResumeData { mode = "chapter", chapter = run.chapter, wave = run.wave + 1, flags = TakeSnapshot().flags };
                Persist();
            }
            Emit(new PhaseFx { type = "station" });
        }

        public void ContinueFromStation()
        {
            if (phase != "station") return;
            if (station?.longPlay == true) sessionPlay = 0;
            station = null;
            BeginWave(run.wave + 1);
        }

        public void QuitFromStation()
        {
            if (phase != "station") return;
            Persist();
            phase = "menu";
            ResetRun();
            Emit(new PhaseFx { type = "quit" });
        }

        /// <summary>终极决战通关后：自由观赏（不出怪、不插旗，玩家在玩具世界里随便走走看看），点“结束观赏”回到结算。</summary>
        public void Explore() { if (phase == "reward") { phase = "explore"; phaseT = 0; bullets.Clear(); } }
        public void EndExplore() { if (phase == "explore") { phase = "reward"; phaseT = 0; Emit(new RewardFx { type = "reward", reward = this.reward }); } }

        private void ClearChapter()
        {
            int ch = run.chapter;
            var C = CHAPTERS.Get(ch);
            // 三星：本章一次没输，且通关时 / 全程峰值玩具化都很低——前两章（入门）≤15% / ≤40%，第 3 章起 ≤6% / ≤10%
            // （平衡模拟：熟练玩家第 3 章起很难全三星）；二星：通关时 ≤45%
            bool intro = ch <= 2;                                       // 第一个主题（两章）是入门
            int stars = run.losses == 0 && toyPeak <= cfg.diff.StarPeak(ch) + 1e-6 ? 3 : toy <= 45 ? 2 : 1;   // 三星门槛按章（Config.diff.starPeak）
            if (!profile.stars.ContainsKey(ch) || profile.stars[ch] < stars) profile.stars[ch] = stars;
            bool fresh = !string.IsNullOrEmpty(C.reward) && (!profile.unlocked.ContainsKey(C.reward) || !profile.unlocked[C.reward]);
            if (!string.IsNullOrEmpty(C.reward)) profile.unlocked[C.reward] = true;
            if (C.reward == "endless") profile.unlocked["bubble"] = true;
            if (profile.cleared < ch) profile.cleared = ch;
            resume = null;
            reward = new RewardData { chapter = ch, item = C.reward, fresh = fresh, stars = stars, toy = Math.Round(toy), stats = run.stats };
            Persist();
            phase = "clear"; phaseT = 0; bullets.Clear(); goldT = 0;
            Emit(new PhaseFx { type = "clear" });
        }

        public const double BossWarnDur = 4.0;
        public double bossWarnT;                     // Boss 预警剩余时间（HUD 闪“Boss 来袭”）
        private void UpdateSpawns(double dt)
        {
            if (phase != "wave") return;
            run.spawnT -= dt;
            if (bossWarnT > 0) bossWarnT -= dt;
            // Boss 排到队首：先预警几秒再放出来（用户：Boss 出场前要有提示——横幅、音效、场景变色）
            if (run.queue.Count > 0 && run.queue[0].StartsWith("boss") && !run.bossWarned && run.spawnT <= BossWarnDur)
            {
                run.bossWarned = true; bossWarnT = BossWarnDur; run.spawnT = Math.Max(run.spawnT, BossWarnDur);
                run.bossPortal = BossDoor(portals[(int)Math.Floor(rng() * portals.Count)]);   // 和 SpawnEnemy 里随机选门是同一次随机数：不改变后面的随机序列
                Emit(new BossWarnFx { type = "bossWarn", kind = run.queue[0], dur = BossWarnDur, portal = run.bossPortal.id });
            }
            if (run.spawnT <= 0 && run.queue.Count > 0 && AliveCount() < cfg.maxAlive) {
                string k = run.queue[0]; run.queue.RemoveAt(0);
                Enemy e;
                if (k.StartsWith("boss") && run.bossPortal != null) { e = SpawnEnemy(k, new SpawnOpts { portal = run.bossPortal }); run.bossPortal = null; }
                else e = SpawnEnemy(k);
                if (k.StartsWith("boss")) run.bossWarned = false;                        // 终极决战最后一波两个 Boss：第二个也单独预警
                if (e != null && e.kind == "commander") SummonMinions(e, e.k.summonFirst);
                run.spawnT = run.waveDef.gap * (run.mode == "chapter" ? cfg.diff.GapMul(CHAPTERS.World(run.chapter)) : 1) * (1 + cfg.diff.assistGapPer * run.assist);
            }
        }

        // ───────────────────────── 盾牌 / 望远镜 / 视线 ─────────────────────────
        private void UpdateShieldAndBinoc(double dt, Hands hands)
        {
            var S = cfg.shield;
            var sh = shield;
            bool can = (profile.unlocked.ContainsKey("shield") && profile.unlocked["shield"]) || lesson?.id == "shield";
            string side = null;
            foreach (var s in new[]{"right","left"}) if (hands.Get(s)?.state == "shield") side = s;
            if (sh.broken > 0) { sh.broken -= dt; if (sh.broken <= 0) sh.dur = ShieldMax(); }
            bool up = can && side != null && sh.broken <= 0;
            if (up && !sh.up) { sh.upAge = 0; Emit(new ShieldFx { type = "shieldUp" }); }
            if (!up && sh.up) Emit(new ShieldFx { type = "shieldDown" });
            sh.up = up;
            if (sh.up) {
                sh.upAge += dt; sh.idle = 0;
                var hh = hands.Get(side);
                sh.side = side;
                sh.normal = hh.palmN;
                sh.pos = V.add(hh.palm, V.scale(hh.palmN, 0.06));
            } else {
                sh.idle += dt;
                if (sh.idle > S.regenDelay && sh.broken <= 0)
                    sh.dur = Math.Min(ShieldMax(), sh.dur + S.regenRate * dt);
            }

            bool canB = (profile.unlocked.ContainsKey("binoc") && profile.unlocked["binoc"]) || lesson?.id == "binoc";
            bool b = canB && (hands.left?.state == "binoc" || hands.right?.state == "binoc");
            if (b && !binoc.active) Emit(new BinocFx { type = "binocOn" });
            if (!b && binoc.active) Emit(new BinocFx { type = "binocOff" });
            binoc.active = b;
        }

        // 可被视线锁定的目标列表（binoc=true 时包含隐形目标）
        public List<TargetInfo> Targets(Vec3d origin, bool binocMode = false)
        {
            var out0 = new List<TargetInfo>();
            foreach (var e in enemies) {
                if (e.dead || e.state == "gone") continue;
                double aimY = double.IsNaN(e.k.aimY) ? e.height * 0.5 : e.k.aimY;
                Vec3d c = new Vec3d(e.pos.x, e.pos.y + aimY, e.pos.z);
                if (!CanSee(e, head?.pos ?? origin)) continue;            // 看不见的（还在门墙后面）不能锁、不能吸附、打不中（用户：没看见他，框就出来了）
                double r = double.IsNaN(e.k.aimY) ? Math.Max(e.radius, e.height * 0.45) : e.radius * 1.2;
                var ti = new TargetInfo { id = e.id, type = "enemy", pos = c, radius = r, @ref = e };
                if (double.IsNaN(e.k.aimY)) { ti.y0 = e.pos.y + e.height * 0.1; ti.y1 = e.pos.y + e.height * 0.92; ti.radius = e.radius; }
                out0.Add(ti);
            }
            foreach (var f in flags) {
                if (f.dying) continue;
                bool big = f.type == "big";                             // 旗子细高：判定是从底座到旗顶的竖条，看旗杆、底座、旗面都能锁上（原来是空中一个球，往下看底座时锁不上也打不中）
                out0.Add(new TargetInfo { id = f.id, type = "flag", pos = new Vec3d(f.pos.x, big ? 0.5 : 0.36, f.pos.z), radius = big ? 0.2 : 0.16, y0 = 0.03, y1 = big ? 0.8 : 0.54, @ref = f });
            }
            AddMechTargets(out0, origin);
            return out0;
        }

        private void UpdateGaze(double dt, GazeRay gazeIn)
        {
            var G = cfg.gaze;
            var B = cfg.binoc;
            if (binoc.active) {
                var t = PickTarget(gazeIn.origin, gazeIn.dir, Targets(gazeIn.origin, true), G.margin / B.zoom + 0.6);
                int? id = t?.id;
                if (id != binoc.cand) { binoc.cand = id; binoc.t = 0; }
                else if (id != null) {
                    binoc.t += dt;
                    if (binoc.t >= B.markDwell && !marks.ContainsKey(id.Value)) {
                        if (marks.Count >= B.maxMarks) {
                            int? oldest = null; double ot = double.PositiveInfinity;
                            foreach (var kv in marks) if (kv.Value < ot) { ot = kv.Value; oldest = kv.Key; }
                            if (oldest.HasValue) marks.Remove(oldest.Value);
                        }
                        marks[id.Value] = time + B.markDur;
                        counters.marks++;
                        Emit(new MarkFx { type = "mark", id = id.Value });
                    }
                }
                gaze.lock_ = null;
                return;
            }
            binoc.cand = null;
            // 眼动有抖动：视线容差 4°、敌人优先于旗子；视线短暂滑开 0.35 秒内仍保持锁定（不会一眨眼就丢）
            var tt = PickAimTarget(gazeIn.origin, gazeIn.dir, Targets(gazeIn.origin), G.margin, G.flagPenalty);
            int? idg = tt?.id;
            if (idg == null) {
                gaze.lostT += dt;
                if (gaze.lostT > G.lostGrace || !LockStillVisible()) gaze.lock_ = null;   // 视线滑开有 0.35 秒宽限；但目标走回门墙后面了就立刻丢（框不能画在墙上）
                gaze.cand = null;
                return;
            }
            gaze.lostT = 0;
            if (idg != gaze.cand) { gaze.cand = idg; gaze.candT = 0; }
            else gaze.candT += dt;
            if (gaze.candT >= G.dwell || gaze.lock_ == idg) gaze.lock_ = idg;
        }

        bool LockStillVisible()
        {
            if (gaze.lock_ == null) return true;
            foreach (var e in enemies) if (e.id == gaze.lock_.Value) return !e.dead && CanSee(e, head.pos);
            return true;                                                   // 旗子 / 机关：Targets 里已经按可见过滤
        }

        private TargetInfo FindTarget(int? id, Vec3d origin)
        {
            if (id == null) return null;
            var ts = Targets(origin, false);
            foreach (var t in ts) if (t.id == id.Value) return t;
            return null;
        }

        // ───────────────────────── 武器 ─────────────────────────
        private void UpdateWeapons(double dt, Hands hands)
        {
            var RT = ReloadTimes();
            foreach (var side in new[]{"right","left"}) {
                var w = weapons.Get(side);
                var h = hands.Get(side);
                if (w != null) {
                    if (w.fireCd > 0) w.fireCd -= dt;
                    if (w.recoil > 0) w.recoil = Math.Max(0, w.recoil - dt * 6);
                    if (w.reload != null) {
                        var r = w.reload;
                        r.t += dt;
                        if (r.fastT != null) { r.fastT -= dt; if (r.fastT <= 0) FinishReload(side, "gold"); }
                        else if (r.jam) { if (r.t >= RT.total + RT.jam) FinishReload(side, "jam"); }
                        else if (r.t >= RT.total) FinishReload(side, "normal");
                    }
                    // 无限火力：只要是持枪手势就自动连射
                    if (rapidT > 0 && h?.state == "gun" && h.origin != null && !binoc.active && knockT <= 0 && (phase == "wave" || phase == "rest")) {
                        w.reload = null;
                        if (w.fireCd <= 0) { w.fireCd = cfg.mech.giftRapidInterval; FireOne(side, h.origin, h.dir, hands); }
                    }
                    // 连发枪的后续子弹
                    if (w.burstQ.Count > 0) {
                        w.burstQ[0].t -= dt;
                        if (w.burstQ[0].t <= 0) {
                            var b = w.burstQ[0]; w.burstQ.RemoveAt(0);
                            FireOne(side, b.origin, b.dir, hands);
                        }
                    }
                }
                foreach (var ev in h?.events ?? new List<HandEvent>()) {
                    if (ev.type == "state" && w != null) {
                        var se = (StateEvent)ev;
                        if (se.to == "fist") StartReload(side);
                        else if (se.from == "fist") ReleaseReload(side);
                    } else if (ev.type == "fire") TryFire(side, (FireEvent)ev, hands);
                    else if (ev.type == "pinchStart") Emit(new PinchFx { type = "pinch", side = side });
                }
            }
        }

        private void StartReload(string side)
        {
            var w = weapons.Get(side);
            if (w == null || w.reload != null || rapidT > 0) return;
            if (w.ammo >= MagOf(w.gun)) { Emit(new ReloadFx { type = "reloadFull", side = side }); return; }
            w.reload = new ReloadState { t = 0, fastT = null, jam = false, released = false };
            w.burstQ.Clear();
            Emit(new ReloadFx { type = "reloadStart", side = side });
        }

        private void ReleaseReload(string side)
        {
            var w = weapons.Get(side);
            var r = w?.reload;
            if (r == null || r.released) return;
            r.released = true;
            var RT = ReloadTimes();
            if (r.t >= RT.goldStart && r.t <= RT.goldEnd) {
                r.fastT = RT.fast;
                Emit(new GoldHitFx { type = "reloadGoldHit", side = side });
            } else if (r.t < RT.goldStart) {
                r.jam = true;
                Emit(new JamFx { type = "jam", side = side });
            }
        }

        private void FinishReload(string side, string how)
        {
            var w = weapons.Get(side);
            w.ammo = MagOf(w.gun);
            w.reload = null;
            counters.reloads++;
            if (how == "gold") { counters.golds++; run.stats.golds++; }
            Emit(new ReloadFx { type = "reloadDone", side = side, how = how });
        }

        private void TryFire(string side, FireEvent ev, Hands hands)
        {
            var w = weapons.Get(side);
            if (w == null || rapidT > 0) return;                       // 无限火力期间自动连射，不看挑腕
            if (phase != "wave" && phase != "lesson" && phase != "rest") return;
            if (binoc.active) { Emit(new FireBlockedFx { type = "fireBlocked", side = side, why = "binoc" }); return; }
            if (knockT > 0) { Emit(new FireBlockedFx { type = "fireBlocked", side = side, why = "knocked" }); return; }
            if (w.reload != null) { Emit(new FireBlockedFx { type = "fireBlocked", side = side, why = "reload" }); return; }
            if (w.fireCd > 0) return;
            if (w.ammo <= 0) { Emit(new DryFx { type = "dry", side = side }); w.fireCd = 0.15; return; }
            var G = GUNS.Get(w.gun);
            w.fireCd = G.interval;
            FireOne(side, ev.origin, ev.dir, hands);
            for (int i = 1; i < G.perFlick; i++) w.burstQ.Add(new BurstShot { t = G.burstGap, origin = ev.origin, dir = ev.dir });
        }

        private void FireOne(string side, Vec3d origin, Vec3d dir, Hands hands)
        {
            var w = weapons.Get(side);
            bool rapid = rapidT > 0;                                    // 无限火力：不耗子弹，也不算进命中率
            if (w == null || (!rapid && w.ammo <= 0) || w.reload != null) return;
            if (!rapid) { w.ammo--; run.stats.shots++; counters.shots++; }
            w.recoil = 1;
            var G = GUNS.Get(w.gun);
            var A = cfg.aim;
            TargetInfo target = null;
            // 1) 眼动锁定的目标（加分项：枪口大致朝着它就算命中）
            var gazeT = FindTarget(gaze.lock_, origin);
            if (gazeT != null && V.angleTo(origin, dir, AimPoint(origin, dir, gazeT)) <= A.cone) target = gazeT;
            // 2) 没锁上 / 锁的不在枪口方向：沿枪口在宽松锥角里找，敌人优先于旗子（真机试玩：眼动不准时老打空）
            if (target == null) {
                string other = side == "right" ? "left" : "right";
                var ow = weapons.Get(other);
                bool dual = ow != null && hands?.Get(other)?.state == "gun";
                target = PickAimTarget(origin, dir, Targets(origin), dual ? A.offhandSnap : A.assist, A.flagPenalty);
            }
            // 指挥官前面的跟班会挡子弹
            if (target != null && target.type == "enemy") {
                var te = (Enemy)target.@ref;
                if (te.kind == "commander") {
                    Vec3d tp = target.pos;
                    foreach (var m in enemies) {
                        if (!m.dead && m.kind == "minion" && m.state == "guard" && m.commanderId == target.id) {
                            Vec3d mc = new Vec3d(m.pos.x, m.pos.y + m.height * 0.5, m.pos.z);
                            if (SegPointDist(origin, tp, mc) < 0.28) {
                                target = new TargetInfo { id = m.id, type = "enemy", pos = mc, @ref = m };
                                break;
                            }
                        }
                    }
                }
            }
            if (target == null) {
                Emit(new ShotFx { type = "shot", side = side, gun = w.gun, from = origin, dir = dir, hit = false });
                return;
            }
            bool marked = marks.ContainsKey(target.id);
            double dmg = G.dmg;
            bool crit = false, weak = false;
            if (marked) { dmg *= A.critMul; crit = true; }
            if (target.type == "flag" && G.flagMul != 1) dmg *= G.flagMul;
            if (goldT > 0) { dmg *= 2; crit = true; }
            if (target.type == "enemy") {
                var e = (Enemy)target.@ref;
                weak = IsWeak(e);
                if (weak) { dmg *= cfg.aim.weakMul; crit = true; }       // 弱点时机：伤害翻倍（用户：平时 4 枪，插旗时 2 枪）
            }
            run.stats.hits++;
            Emit(new ShotFx { type = "shot", side = side, gun = w.gun, from = origin, dir = dir, hit = true, to = target.pos, id = target.id, crit = crit, dmg = dmg });
            if (HitMechTarget(target, dmg)) return;
            if (target.type == "flag") DamageFlag((Flag)target.@ref, dmg);
            else DamageEnemy((Enemy)target.@ref, dmg, new DamageOpts { gun = G, crit = crit, weak = weak });
        }

        private class DamageOpts { public GunDef gun; public bool crit, weak; }

        /// <summary>弱点时机：小怪正在插旗、小丑盒打开着。</summary>
        public static bool IsWeak(Enemy e) => e.state == "plant" || (e.kind == "jack" && e.state == "open") || (e.k.ai == "boss" && (e.tele || e.state == "stagger"));

        /// <summary>测试/外部用：不带武器选项的伤害接口</summary>
        public void DamageEnemy(Enemy e, double dmg) => DamageEnemy(e, dmg, null);

        private void DamageEnemy(Enemy e, double dmg, DamageOpts opts)
        {
            if (e.dead || dmg <= 0) return;
            if (e.k.ai == "boss")
            {
                // Boss 要打得有分量：平时只吃 70% 伤害，换阶段后 4 秒护盾只吃 35%；弱点时机（出招前发光 / 踉跄）照常吃满并翻倍——逼玩家看准时机打，而不是乱射
                bool weakNow = opts?.weak ?? false;
                double mul = weakNow ? 1.0 : e.bossGuardT > 0 ? 0.35 : 0.7;
                if (run.mode == "chapter") mul /= cfg.diff.BossTough(run.chapter) * Math.Max(0.4, 1 - cfg.diff.bossAssistPer * run.assist);
                else if (run.mode == "endless") mul /= 0.6;                     // 无尽：Boss 本身随波数变厚，这里不再额外加（约 60% 的关卡 Boss 耐打度，否则十几波后打不动）   // 按关的耐打度；卡关时逐层放松
                dmg *= mul;
            }
            e.hp -= dmg;
            e.flash = 0.12;
            Emit(new HitFx { type = "hit", id = e.id, dmg = dmg, crit = opts?.crit ?? false, weak = opts?.weak ?? false, pos = new Vec3d(e.pos.x, e.pos.y + e.height * 0.6, e.pos.z), hp = e.hp, maxHp = e.maxHp });
            if (opts?.gun?.knockback > 0 && e.k.speed > 0 && e.kind != "plane" && e.k.ai != "boss") {
                Vec3d away = V.norm(new Vec3d(e.pos.x - head.pos.x, 0, e.pos.z - head.pos.z));
                double s0 = PortalSide(e.portal, e.pos);
                e.pos = V.add(e.pos, V.scale(away, opts.gun.knockback));
                if (s0 >= 0 && PortalSide(e.portal, e.pos) < 0 && !OnDoorLane(e))   // 原来在房间里、被推进门墙：停在墙前（门洞正前方的可以被推回门里）
                { double push = BodyR(e) + 0.06 - PortalSide(e.portal, e.pos); e.pos = V.add(e.pos, V.scale(e.portal.normal, push)); }
                ClampToWalls(e);
            }
            if (opts?.gun?.slow > 0) e.slowT = opts.gun.slow;
            if (e.kind == "brute" && !e.shrunk && e.hp <= e.k.shrinkAt && e.hp > 0) {
                e.shrunk = true; e.radius *= 0.75; e.height *= 0.75;
                Emit(new ShrinkFx { type = "shrink", id = e.id });
            }
            if (e.hp <= 0) KillEnemy(e, true);
            else if (e.k.ai == "boss") BossDamaged(e);
        }

        private void KillEnemy(Enemy e, bool byPlayer)
        {
            if (e.dead) return;
            e.dead = true; e.deadT = 0;
            Release(e.spot);
            if (byPlayer && e.bonus) CountFrenzyKill();
            if (byPlayer) {
                run.stats.kills++;
                counters.kills++;
                // 注：原型里 e.coins*(flee?1:1) 是原样的，两个分支一样——保持原型行为
                int c = (int)(e.coins * (e.state == "flee" ? 1 : 1));
                profile.coins += c;
                run.stats.coins += c;
            }
            Emit(new KillFx { type = "kill", id = e.id, kind = e.kind, pos = new Vec3d(e.pos.x, e.pos.y + e.height * 0.5, e.pos.z), coins = byPlayer ? e.coins : 0, elite = e.elite });
            if (e.k.splitInto != null && byPlayer) {
                for (int i = 0; i < e.k.splitN; i++) {
                    Vec3d off = V.scale(e.portal.right, (i == 0 ? -1 : 1) * 0.3);
                    Vec3d spot = PickSpot(1.2, 2.5, null);
                    SpawnEnemy(e.k.splitInto, new SpawnOpts { portal = e.portal, pos = V.add(e.pos, off), spot = spot, eliteOverride = null, eliteExplicit = true });
                }
            }
            if (e.kind == "commander") {
                for (int i = 0; i < enemies.Count; i++) {
                    var m = enemies[i];
                    if (!m.dead && m.commanderId == e.id) { m.state = "flee"; m.t = 0; }
                }
                foreach (var f in flags) if (f.commanderId == e.id && !f.dying) KillFlag(f, false);
                Emit(new CommanderDownFx { type = "commanderDown", id = e.id });
            }
        }

        private void DamageFlag(Flag f, double dmg)
        {
            if (f.dying) return;
            f.hp -= dmg;
            f.flash = 0.12;
            Emit(new FlagHitFx { type = "flagHit", id = f.id, dmg = dmg, hp = f.hp, maxHp = f.maxHp, pos = new Vec3d(f.pos.x, 0.5, f.pos.z) });
            if (f.hp <= 0) KillFlag(f, true);
        }

        private void KillFlag(Flag f, bool byPlayer)
        {
            f.dying = true;
            f.dyingFrom = f.amount;
            if (byPlayer) { profile.coins += 1; run.stats.coins += 1; }
            Emit(new FlagDownFx { type = "flagDown", id = f.id, pos = V.clone(f.pos), byPlayer = byPlayer });
        }

        private Flag PlantFlag(Enemy e)
        {
            string type = e.k.flag ?? "small";
            var F = FLAGS.Get(type);
            Vec3d pos = new Vec3d(e.pos.x, 0, e.pos.z);
            var f = new Flag { id = nextId++, type = type, pos = pos, hp = F.hp, maxHp = (int)F.hp, amount = 0, cap = F.cap, rate = F.rate, radius = F.radius, hidden = e.k.hiddenFlag, dying = false, commanderId = e.commanderId, age = 0, flash = 0 };
            flags.Add(f);
            Release(e.spot);
            e.spot = null;
            Emit(new FlagPlantedFx { type = "flagPlanted", id = f.id, flagType = type, pos = V.clone(pos), hidden = f.hidden });
            return f;
        }

        private void UpdateFlags(double dt)
        {
            double recede = cfg.toy.recede;
            foreach (var f in flags) {
                f.age += dt;
                if (f.flash > 0) f.flash -= dt;
                if (f.dying) f.amount = Math.Max(0, f.amount - (f.dyingFrom / recede) * dt);
                else if (phase == "wave" || phase == "lesson") f.amount = Math.Min(f.cap, f.amount + f.rate * (run.mode == "chapter" ? cfg.diff.FlagRateMul(CHAPTERS.World(run.chapter)) : 1) * (run.toyMul) * (1 - cfg.diff.assistFlagPer * run.assist) * dt);
            }
            flags.RemoveAll(f => f.dying && f.amount <= 0);
            foreach (var p in paints) { p.life -= dt; if (p.life < 1) p.amount = p.@base * Math.Max(0, p.life); }
            paints.RemoveAll(p => p.life <= 0);
            RecomputeToy();
        }

        /// <summary>房间整个玩具化要立着几面旗（按章；无尽模式固定）。HUD 的小旗格数也用它。</summary>
        public int FlagsToFull => run != null && run.mode == "endless" ? cfg.toy.flagsToFullEndless : CHAPTERS.Get(Math.Max(1, Math.Min(CHAPTERS.Count, run?.chapter ?? 1))).flagsToFull;

        private void RecomputeToy()
        {
            // 用户：每多插一面旗加一格，插满 n 面（按章）就是 100%（旗子插下立刻算、倒下立刻退回；旗子的 amount 只用来画旗子周围慢慢长开的玩具化）
            double s = 0;
            double per = 100.0 / Math.Max(1, FlagsToFull);
            foreach (var f in flags) if (!f.dying) s += per;
            foreach (var p in paints) s += p.amount;
            toy = Math.Min(100, s);
            toyPeak = Math.Max(toyPeak, toy);
        }

        // ───────────────────────── 小怪 AI ─────────────────────────
        private void UpdateEnemy(Enemy e, double dt)
        {
            if (e.frozenT > 0) { e.frozenT -= dt; if (e.flash > 0) e.flash -= dt; return; }
            e.t += dt;
            e.life += dt;
            if (e.flash > 0) e.flash -= dt;
            if (e.slowT > 0) e.slowT -= dt;
            double spd = e.k.speed * e.speedMul * (e.slowT > 0 ? 0.4 : 1) * (e.shrunk && e.k.shrinkSpeed > 0 ? e.k.shrinkSpeed / e.k.speed : 1);
            bool wasInside = PortalSide(e.portal, e.pos) < 0;
            if (e.state == "arrive" && e.arriveTo != null)
            {
                bool behind = PortalSide(e.portal, e.pos) < 0.25;
                var goal = behind ? new Vec3d(e.portal.mouth.x, Math.Min(e.arriveTo.y, 1.45), e.portal.mouth.z) : e.arriveTo;
                if (behind) goal = DoorApproach(e, goal);                 // 飞的也先对准门洞再出来，不从门墙里穿出来
                var d = V.sub(goal, e.pos); double L = V.len(d), step = e.arriveSpd * dt;
                if (!behind)
                {
                    double sx = d.x, sz = d.z, sl = Math.Sqrt(sx * sx + sz * sz);
                    if (sl > 1e-4 && SteerAround(e, ref sx, ref sz, ref sl))
                    {
                        double k = Math.Min(step, sl) / sl;
                        e.pos.x += sx * k; e.pos.z += sz * k; e.yaw = Math.Atan2(sx, sz);
                        return;
                    }
                }
                if (L > 1e-4) { e.pos = V.add(e.pos, V.scale(d, Math.Min(1, step / L))); if (d.x * d.x + d.z * d.z > 1e-6) e.yaw = Math.Atan2(d.x, d.z); }
                if (!behind && L <= step + 1e-3) { e.pos = V.clone(e.arriveTo); e.state = e.arriveNext; e.t = 0; }
                return;
            }
            if (e.bonus) { AiBonus(e, dt, spd); return; }
            switch (e.k.ai) {
                case "planter": AiPlanter(e, dt, spd); break;
                case "shooter": AiShooter(e, dt, spd); break;
                case "plane": AiPlane(e, dt, spd); break;
                case "jack": AiJack(e, dt); break;
                case "commander": AiCommander(e, dt); break;
                case "minion": AiMinion(e, dt, spd); break;
                case "balloon": AiBalloon(e, dt, spd); break;
                case "rcCar": AiCar(e, dt, spd); break;
                case "repair": AiRepair(e, dt, spd); break;
                case "boss": AiBoss(e, dt, spd); UpdateBossVolley(e, dt); break;
                case "trainer": AiTrainer(e, dt); break;
                default: break;
            }
            bool nowInside = PortalSide(e.portal, e.pos) < 0;
            if (wasInside && !nowInside) Emit(new EmergeFx { type = "emerge", id = e.id });
        }

        /// <summary>门后往房间走（或房间往门后走）：人还在门洞宽度外、离门墙不到 0.5 m 时，先去门洞正后方 / 正前方对准，再穿过门洞——
        /// 不再从门后侧面斜着穿过门墙（用户：敌人不要卡在墙里），也不会到门口被横向一把拽进门洞。</summary>
        Vec3d DoorApproach(Enemy e, Vec3d target)
        {
            var p = e.portal; if (p == null || target == null || e.ghostT > 0 || e.stuckN > 0) return target;   // 卡住过：按原来的贴边滑 / 穿过去，不再绕对准点
            double s0 = PortalSide(p, e.pos), s1 = PortalSide(p, target);
            if (Math.Sign(s0) == Math.Sign(s1) || Math.Abs(s0) < 0.02) return target;   // 不穿门
            LaneLat(p, out double la, out double lb);
            double lo = la + e.radius + 0.04, hi = lb - e.radius - 0.04; if (lo > hi) lo = hi = (la + lb) / 2;
            double lat = V.dot(V.sub(e.pos, p.@base), p.right);
            if (lat >= lo - 0.12 && lat <= hi + 0.12) return target;      // 已经大致对准门洞（剩下一点由门洞宽度夹紧）
            double cl = Math.Max(lo, Math.Min(hi, lat)), back = Math.Sign(s0) * Math.Max(0.45, BodyR(e) + 0.15);
            return new Vec3d(p.@base.x + p.right.x * cl + p.normal.x * back, target.y, p.@base.z + p.right.z * cl + p.normal.z * back);
        }

        private bool MoveTo(Enemy e, Vec3d target, double spd, double dt)
        {
            if (e.detour != null)
            {
                e.detourT -= dt;
                if (e.detourT <= 0 || MoveToRaw(e, e.detour, spd, dt)) e.detour = null;
                return false;
            }
            if (e.k.move != "fly" && e.pos.y <= 0.8) { var via = DoorApproach(e, target); if (!ReferenceEquals(via, target)) { MoveToRaw(e, via, spd, dt); return false; } }
            return MoveToRaw(e, target, spd, dt);
        }

        private bool MoveToRaw(Enemy e, Vec3d target, double spd, double dt)
        {
            // 目标点落在障碍圈里（门口 / 插旗点旁边有摆件）：改去圈边上离它最近的点，否则永远走不到、一直卡着
            if (e.pos.y <= 0.8 && e.k.move != "fly" && !PassThrough(e))
                foreach (var o in obstacles)
                {
                    if (OnDoorLane(e) && InDoorLane(e.portal, o)) continue;
                    double ox = target.x - o.x, oz = target.z - o.z, d = Math.Sqrt(ox * ox + oz * oz), min = o.r + e.radius + 0.02;
                    if (d >= min) continue;
                    if (d < 1e-4) { ox = target.x - e.pos.x; oz = target.z - e.pos.z; d = Math.Max(1e-4, Math.Sqrt(ox * ox + oz * oz)); ox = -ox; oz = -oz; }
                    target = new Vec3d(o.x + ox / d * min, target.y, o.z + oz / d * min);
                }
            double dx = target.x - e.pos.x, dz = target.z - e.pos.z;
            double l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-4) return true;
            double step = spd * dt;
            bool steered = SteerAround(e, ref dx, ref dz, ref l);
            e.yaw = Math.Atan2(dx, dz);
            if (!steered && step >= l) { e.pos.x = target.x; e.pos.z = target.z; return true; }
            step = Math.Min(step, l);
            e.pos.x += (dx / l) * step;
            e.pos.z += (dz / l) * step;
            AvoidObstacles(e);
            return false;
        }

        /// <summary>直线去目标会擦进障碍圈：改朝障碍侧边的绕行点走（离目标那一侧），走一段再重新判断——小怪绕着灌木走，不穿过去。</summary>
        private bool SteerAround(Enemy e, ref double dx, ref double dz, ref double l)
        {
            if (obstacles.Count == 0 || e.pos.y > 0.8 || e.k.move == "fly" || e.ghostT > 0) return false;
            double ux = dx / l, uz = dz / l, bestT = double.MaxValue; int hit = -1; double hcx = 0, hcz = 0, hmin = 0;
            bool nearGate = OnDoorLane(e);
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                if (nearGate && InDoorLane(e.portal, o)) continue;
                double min = o.r + e.radius + 0.04;
                double ox = o.x - e.pos.x, oz = o.z - e.pos.z;
                double t = ox * ux + oz * uz;                       // 障碍中心在前进方向上的投影
                if (t <= 0 || t > l + min) continue;
                double cx = ox - ux * t, cz = oz - uz * t;          // 前进直线到障碍中心的垂线
                if (cx * cx + cz * cz >= min * min || t >= bestT) continue;
                bestT = t; hit = i; hcx = cx; hcz = cz; hmin = min;
            }
            if (hit < 0) return false;
            var h = obstacles[hit];
            double pl = Math.Sqrt(hcx * hcx + hcz * hcz), sx, sz;
            if (pl > 1e-4) { sx = -hcx / pl; sz = -hcz / pl; } else { sx = -uz; sz = ux; }   // 往障碍中心的反侧绕
            dx = h.x + sx * hmin * 1.25 - e.pos.x; dz = h.z + sz * hmin * 1.25 - e.pos.z;
            l = Math.Max(1e-4, Math.Sqrt(dx * dx + dz * dz));
            return true;
        }

        /// <summary>地面上的小怪进了障碍圈就推回圈边（沿着圈边滑过去 = 绕开），飞的不管。</summary>
        /// <summary>门后连续卡住时才穿过摆件（玩家看不真切，而且不出来就打不到）；房间里的敌人打得到，障碍始终是实的。</summary>
        bool PassThrough(Enemy e) => e.stuckN >= 2 && PortalSide(e.portal, e.pos) < 0.2;

        private void TrackStuck(Enemy e, double dt)
        {
            if (e.ghostT > 0) e.ghostT -= dt;
            bool wants = e.frozenT <= 0 && !e.paused && (e.k.speed > 0 || e.state == "approach")
                && (e.state == "walk" || e.state == "arrive" || e.state == "enter" || e.state == "drive" || e.state == "charge" || e.state == "approach" || (e.state == "leave" && e.t >= CheerTime));
            e.longT += dt;
            if (e.longT >= 3)
            {
                double lx = e.pos.x - e.longX, lz = e.pos.z - e.longZ;
                if (wants && lx * lx + lz * lz < 0.25 * 0.25 && e.detour == null && e.pos.y <= 0.8 && e.k.move != "fly" && PortalSide(e.portal, e.pos) > 0.2) PickDetour(e);
                e.longT = 0; e.longX = e.pos.x; e.longZ = e.pos.z;
            }
            e.stuckWin += dt;
            if (e.stuckWin >= 0.8)
            {
                double mx = e.pos.x - e.stuckX, mz = e.pos.z - e.stuckZ;
                if (wants && mx * mx + mz * mz < 0.06 * 0.06) { e.ghostT = 1.6; e.stuckN++; }   // 第一次：贴着障碍滑；连续卡住（摆件圈重叠成死角）：直接穿过去
                else e.stuckN = 0;
                // 房间里连续卡住（摆件多的章节）：换一个目标点重新走，不在原地干耗
                if (e.stuckN >= 3 && PortalSide(e.portal, e.pos) > 0.2 && e.k.ai != "boss")
                {
                    if (e.spot != null) { Release(e.spot); e.spot = ClearSpot(e, cfg.arena.flagMin, cfg.arena.flagMax); Reserve(e.spot); e.arcFrom = null; }
                    if (e.post != null) e.post = ClearSpot(e, e.k.standoff != null ? e.k.standoff[0] : 1.2, e.k.standoff != null ? e.k.standoff[1] : 2.2);
                    e.stuckN = 0;
                }
                e.stuckWin = 0; e.stuckX = e.pos.x; e.stuckZ = e.pos.z;
            }
            // 最后一道保险：门后的敌人长时间打不到（比如走不出来），直接送到门口——不能出现打不到、这一波永远结束不了的情况
            bool behind = e.k.move != "fly" && e.pos.y <= 0.8 && PortalSide(e.portal, e.pos) < -0.35 && !e.bonus;
            e.hiddenT = behind && !CanSee(e, head.pos) ? e.hiddenT + dt : 0;
            if (e.hiddenT >= 8)
            {
                e.hiddenT = 0; e.ghostT = 1.6; e.stuckN = 2;
                e.pos.x = e.portal.mouth.x; e.pos.z = e.portal.mouth.z;
                if (e.state == "command" || e.state == "approach") e.state = "command";
            }
        }

        /// <summary>房间里卡住后重新选点：优先选从当前位置直线走得到的（连线不擦任何障碍），12 次都不行就用第一个。
        /// 原来随便选一个，新点常常还在同一堆摆件后面，小怪对着摆件原地踏步十几秒（第 12 章扫描）。</summary>
        private Vec3d ClearSpot(Enemy e, double dmin, double dmax)
        {
            Vec3d first = null;
            for (int i = 0; i < 12; i++)
            {
                var s = PickSpot(dmin, dmax, null); first ??= s;
                if (SegClear(e.pos, s, e.radius + 0.03)) return s;
            }
            return first;
        }

        /// <summary>卡在摆件夹角里：在身边 0.5～0.9 m 一圈找一个直线走得到、不压摆件的点，先走过去（2.5 秒内），再按原目标重新走。</summary>
        void PickDetour(Enemy e)
        {
            double best = double.MaxValue; Vec3d pick = null;
            for (int i = 0; i < 16; i++)
            {
                double a = i * Math.PI / 8, r = i % 2 == 0 ? 0.6 : 0.9;
                var c = new Vec3d(e.pos.x + Math.Cos(a) * r, 0, e.pos.z + Math.Sin(a) * r);
                if (e.portal != null && PortalSide(e.portal, c) < BodyR(e) + 0.1) continue;
                if (!SegClear(c, c, e.radius + 0.03) || !SegClear(e.pos, c, Math.Max(0.02, e.radius - 0.04))) continue;   // 终点要宽松地空；路上按略小的身子算（人本来就贴着摆件，按全身算每条路都“不通”）
                double back = e.longX - c.x, bz = e.longZ - c.z, score = -(back * back + bz * bz) + rng() * 0.05;   // 偏向离开卡住点
                if (score < best) { best = score; pick = c; }
            }
            if (pick != null) { e.detour = pick; e.detourT = 2.5; e.ghostT = 0; }
        }

        private bool SegClear(Vec3d a, Vec3d b, double rad)
        {
            double dx = b.x - a.x, dz = b.z - a.z, L2 = dx * dx + dz * dz;
            foreach (var o in obstacles)
            {
                double t = L2 > 1e-9 ? Math.Max(0, Math.Min(1, ((o.x - a.x) * dx + (o.z - a.z) * dz) / L2)) : 0;
                double px = a.x + t * dx - o.x, pz = a.z + t * dz - o.z, rr = o.r + rad;
                if (px * px + pz * pz < rr * rr) return false;
            }
            return true;
        }

        private void AvoidObstacles(Enemy e)
        {
            if (e.pos.y > 0.8 || e.k.move == "fly") return;
            bool inGate = e.portal != null && HasGate(e.portal) && Math.Abs(PortalSide(e.portal, e.pos)) < 0.45;
            bool lane = OnDoorLane(e);
            ClampToWalls(e);                                             // 先挡门墙 / 真实墙，再推开摆件（摆件圈是更硬的约束：门前墙根的摆件本来离墙有空）
            if (obstacles.Count > 0 && !PassThrough(e))
                for (int pass = 0; pass < 6; pass++)                     // 两个障碍挨得近时推出一个可能推进另一个：多推几轮
                {
                    bool any = false;
                    foreach (var o in obstacles)
                    {
                        if (lane && InDoorLane(e.portal, o)) continue;    // 门洞正前后的通道里不该有摆件（场景里已清空）；有也以门洞为准，不然两头推、卡在门里
                        double ox = e.pos.x - o.x, oz = e.pos.z - o.z, d = Math.Sqrt(ox * ox + oz * oz), min = o.r + e.radius;
                        if (d >= min) continue;
                        if (d < 1e-4) { ox = 1; oz = 0; d = 1; }
                        e.pos.x = o.x + ox / d * (min + 1e-4); e.pos.z = o.z + oz / d * (min + 1e-4); any = true;
                    }
                    if (!any) break;
                    if (pass == 5) ResolveGap(e, lane);                  // 推了 6 轮还压着：两个摆件的夹缝比身子窄，直接放到两圈外最近的点
                }
            if (PassThrough(e)) e.hasOk = false;                            // 卡住后正在穿过障碍：旧的合法位置作废（贴边滑时照样要退回，不许留在摆件里）
            else if (obstacles.Count > 0 && !Overlaps(e, lane)) { e.okX = e.pos.x; e.okZ = e.pos.z; e.okT = time; e.hasOk = true; }
            if (inGate)                                                  // 门洞底部的宽度是硬约束（门框是实的），最后夹
            {
                var r = e.portal.right; var b = e.portal.@base;
                LaneLat(e.portal, out double la, out double lb);
                double lo = la + e.radius + 0.02, hi = lb - e.radius - 0.02;
                if (lo > hi) lo = hi = (la + lb) / 2;
                double lat = V.dot(V.sub(e.pos, b), r), cl = Math.Max(lo, Math.Min(hi, lat));
                if (cl != lat) { e.pos.x += r.x * (cl - lat); e.pos.z += r.z * (cl - lat); }
            }
        }

        /// <summary>两个摆件夹缝太窄、来回推也推不出去：候选点 = 每个摆件圈外的投影点 + 两两扩张圈的交点，取不压任何摆件、离现在最近的那个。
        /// 都不行才退回半秒内记下的合法位置。（原来留在夹缝里压着摆件 → 穿模；直接退回又会把卡住后贴边滑出去的小怪一直拽回来 → 原地不动）</summary>
        void ResolveGap(Enemy e, bool lane)
        {
            var hits = new List<(double x, double z, double r)>();
            foreach (var o in obstacles) { if (lane && InDoorLane(e.portal, o)) continue; double ox = e.pos.x - o.x, oz = e.pos.z - o.z, R = o.r + e.radius + 1e-3; if (ox * ox + oz * oz < R * R) hits.Add((o.x, o.z, R)); }
            if (hits.Count == 0) return;
            var cand = new List<(double x, double z)>();
            foreach (var o in hits) { double ox = e.pos.x - o.x, oz = e.pos.z - o.z, d = Math.Max(1e-4, Math.Sqrt(ox * ox + oz * oz)); cand.Add((o.x + ox / d * o.r, o.z + oz / d * o.r)); }
            foreach (var a in hits) foreach (var o in obstacles)
            {
                if (lane && InDoorLane(e.portal, o)) continue;
                double R2 = o.r + e.radius + 1e-3, dx = o.x - a.x, dz = o.z - a.z, d = Math.Sqrt(dx * dx + dz * dz);
                if (d < 1e-4 || d > a.r + R2 || d < Math.Abs(a.r - R2)) continue;
                double k = (a.r * a.r - R2 * R2 + d * d) / (2 * d), hh = Math.Sqrt(Math.Max(0, a.r * a.r - k * k));
                double mx = a.x + dx * k / d, mz = a.z + dz * k / d;
                cand.Add((mx - dz * hh / d, mz + dx * hh / d)); cand.Add((mx + dz * hh / d, mz - dx * hh / d));
            }
            double best = double.MaxValue; (double x, double z)? pick = null;
            foreach (var c in cand)
            {
                bool clear = true;
                foreach (var o in obstacles) { if (lane && InDoorLane(e.portal, o)) continue; double ox = c.x - o.x, oz = c.z - o.z; if (ox * ox + oz * oz < (o.r + e.radius) * (o.r + e.radius) - 1e-9) { clear = false; break; } }
                if (!clear) continue;
                double dd = (c.x - e.pos.x) * (c.x - e.pos.x) + (c.z - e.pos.z) * (c.z - e.pos.z);
                if (dd < best) { best = dd; pick = c; }
            }
            if (pick.HasValue) { e.pos.x = pick.Value.x; e.pos.z = pick.Value.z; }
            else if (e.hasOk && time - e.okT < 0.5) { e.pos.x = e.okX; e.pos.z = e.okZ; }
        }

        bool Overlaps(Enemy e, bool lane)
        {
            foreach (var o in obstacles)
            {
                if (lane && InDoorLane(e.portal, o)) continue;
                double ox = e.pos.x - o.x, oz = e.pos.z - o.z;
                if (ox * ox + oz * oz < (o.r + e.radius) * (o.r + e.radius) - 1e-9) return true;
            }
            return false;
        }

        /// <summary>真实房间的可活动范围（逻辑坐标，玩家脚下为原点）：表现层按扫描到的房间 / 安全边界给；没给时用一个保守的圆。</summary>
        public Func<Vec3d, double, Vec3d> roomClamp;
        /// <summary>门后世界的墙（第 5 章工坊、第 6 章玩具箱是包住房间的室内壳）：逻辑坐标的轴对齐矩形，敌人不许走到墙外面（用户：玩具房关卡的模型卡在背景墙外面）。null = 开阔世界，没有墙。</summary>
        public (double x0, double x1, double z0, double z1)? worldBox;

        /// <summary>门所在的墙是实的（用户：敌人不要卡在墙里）：门洞宽度以外，身子不许探到门墙后面；房间这一侧也不许走出真实房间。
        /// 门后（整个还在门墙后面、正往门洞走）的不管——那是门后世界，由门后通道 / 门洞宽度管。</summary>
        void ClampToWalls(Enemy e)
        {
            var p = e.portal; if (p == null) return;
            double side = PortalSide(p, e.pos), keep = BodyR(e) + 0.06;
            if (e.k.move != "fly" && e.pos.y <= p.h && side > -keep && side < keep)
            {
                LaneLat(p, out double la, out double lb);
                double lat = V.dot(V.sub(e.pos, p.@base), p.right);
                bool inHole = lat > la + e.radius * 0.5 && lat < lb - e.radius * 0.5;
                if (e.k.ai == "boss" && e.state != "enter" && side >= 0) inHole = false;   // Boss 比门洞宽：打仗时正对门洞也要离墙一个身位（用户：Boss 会有一半在门后面）
                if (!inHole)
                {
                    if (side >= 0) { double push = keep - side; e.pos.x += p.normal.x * push; e.pos.z += p.normal.z * push; }   // 房间这一侧贴墙：推回墙前
                    else { double cl = Math.Max(la + e.radius + 0.02, Math.Min(lb - e.radius - 0.02, lat)); e.pos.x += p.right.x * (cl - lat); e.pos.z += p.right.z * (cl - lat); }   // 门后正要出门：先对准门洞
                }
            }
            if (worldBox is (double bx0, double bx1, double bz0, double bz1))   // 门后世界的墙：身子（含模型半宽）整个留在墙里面，飞的也一样
            {
                double r2 = BodyR(e) + 0.05;
                e.pos.x = Math.Max(bx0 + r2, Math.Min(bx1 - r2, e.pos.x));
                e.pos.z = Math.Max(bz0 + r2, Math.Min(bz1 - r2, e.pos.z));
            }
            if (roomClamp != null && e.k.move != "fly" && e.pos.y <= 0.8 && PortalSide(p, e.pos) > 0.3 && !InDoorCorridor(e.pos))
            {
                var q = roomClamp(e.pos, BodyR(e)); if (q != null) { e.pos.x = q.x; e.pos.z = q.z; }
            }
        }

        /// <summary>某扇门正前方的进出通道（横向 ±1 m）：真实墙离玩家可能比门还近，这里不按真实墙夹，不然小怪回不了门。</summary>
        bool InDoorCorridor(Vec3d pos)
        {
            if (portals == null) return false;
            foreach (var p in portals) if (PortalSide(p, pos) > -0.1 && Math.Abs(V.dot(V.sub(pos, p.@base), p.right)) < 1.0) return true;
            return false;
        }
        /// <summary>模型看得见的半宽：Boss、大块头的模型比规则半径宽（贴墙判定按模型算，不然模型半边进墙）。</summary>
        double BodyR(Enemy e) => e.k.ai == "boss" ? Math.Max(e.radius, 0.65) : e.kind == "brute" ? Math.Max(e.radius, 0.42) : e.radius;

        /// <summary>门的进出通道：门后出生点（deep）一直到门外门槛（障碍 0.45 m、小怪 0.9 m）、宽 = 门洞底部宽度（gateA..gateB，没有就按门宽）。小怪只从这条路进出。</summary>
        double LaneBack => cfg.portal.inside + cfg.portal.spawnDepth + 0.4;
        void LaneLat(Portal p, out double lo, out double hi)
        {
            if (p.hasGate) { lo = Math.Min(p.gateLo, p.gateHi); hi = Math.Max(p.gateLo, p.gateHi); }
            else if (gateA != null && gateB != null) { double la = V.dot(V.sub(gateA, p.@base), p.right), lb = V.dot(V.sub(gateB, p.@base), p.right); lo = Math.Min(la, lb); hi = Math.Max(la, lb); }
            else { lo = -p.w / 2; hi = p.w / 2; }
        }
        bool HasGate(Portal p) => p.hasGate || (gateA != null && gateB != null);
        /// <summary>按门洞真实轮廓设某扇门底部可通过的横向范围（沿 right、相对门的 base）。</summary>
        public void SetGate(int i, double lo, double hi)
        {
            if (i < 0 || i >= portals.Count) return;
            portals[i].hasGate = true; portals[i].gateLo = Math.Min(lo, hi); portals[i].gateHi = Math.Max(lo, hi);
        }
        /// <summary>所有门都用同一个门洞轮廓：记一份默认范围，之后每次建门（开章）都自动带上。</summary>
        public void SetGateAll(double lo, double hi)
        {
            _gateDefault = (Math.Min(lo, hi), Math.Max(lo, hi));
            if (portals != null) for (int i = 0; i < portals.Count; i++) SetGate(i, lo, hi);
        }
        (double lo, double hi)? _gateDefault;
        /// <summary>障碍圈压到门的进出通道上（场景里通道已清空；万一有，以通道为准，不让它把小怪堵在门里）。</summary>
        public bool InDoorLane(Portal p, (double x, double z, double r) o)
        {
            if (p == null) return false;
            double side = PortalSide(p, o.x, 0, o.z);
            if (side < -LaneBack - o.r || side > 0.45 + o.r) return false;   // 门外只算门槛这一小段（再往外是房间，正常绕开实物）
            LaneLat(p, out var lo, out var hi); double lat = PortalLat(p, o.x, 0, o.z);
            return lat > lo - o.r && lat < hi + o.r;
        }
        /// <summary>小怪正走在门的进出通道里。</summary>
        bool OnDoorLane(Enemy e)
        {
            var p = e.portal; if (p == null) return false;
            double side = PortalSide(p, e.pos);
            if (side < -LaneBack || side > 0.9) return false;
            LaneLat(p, out var lo, out var hi); double lat = PortalLat(p, e.pos.x, e.pos.y, e.pos.z);
            return lat > lo - 0.3 && lat < hi + 0.3;
        }

        // 先走到门口，再去目标
        private bool WalkVia(Enemy e, Vec3d target, double spd, double dt)
        {
            if (PortalSide(e.portal, e.pos) < 0.3) return MoveTo(e, e.portal.mouth, spd, dt) && false;
            return MoveTo(e, target, spd, dt);
        }

        /// <summary>小怪不要叠在一起、不和 Boss 穿模（用户：小怪不要叠在一起，减少穿模；Boss 身边不要有小怪，看不清 Boss）。
        /// 地面上的敌人两两按半径推开（各让一半）；Boss 不被推，只推小怪，并且 Boss 周围多留 0.35 米空地。
        /// 门后（还没出门）的不推（门洞窄，推了会卡墙）；飞的、气球、正在插旗 / 被冻住的只当障碍不被推。不用随机数。</summary>
        private void SeparateEnemies()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var a = enemies[i]; if (a.dead || a.state == "gone" || a.k.move == "fly") continue;
                bool aBoss = IsBoss(a);
                if (!aBoss && (PortalSide(a.portal, a.pos) < 1.0 || a.state == "leave" || a.state == "flee")) continue;
                for (int j = i + 1; j < enemies.Count; j++)
                {
                    var b = enemies[j]; if (b.dead || b.state == "gone" || b.k.move == "fly") continue;
                    bool bBoss = IsBoss(b);
                    if (!bBoss && (PortalSide(b.portal, b.pos) < 1.0 || b.state == "leave" || b.state == "flee")) continue;
                    if (aBoss && bBoss) continue;
                    if ((a.k.move == "float") != (b.k.move == "float") && !aBoss && !bBoss) continue;   // 空中的气球和地上的不互推
                    // Boss 的规则半径（0.42～0.48）比模型可见的半宽小一圈（实测 Boss 模型宽 1.0～1.4 米）：清场按 0.65 米半宽 + 0.35 米空地
                    double ra = aBoss ? Math.Max(a.radius, 0.65) : a.radius, rb = bBoss ? Math.Max(b.radius, 0.65) : b.radius;
                    double need = ra + rb + (aBoss || bBoss ? 0.35 : 0.04);
                    double dx = b.pos.x - a.pos.x, dz = b.pos.z - a.pos.z, d = Math.Sqrt(dx * dx + dz * dz);
                    if (d >= need) continue;
                    if (d < 1e-4) { dx = Math.Sin(a.id * 2.3 + b.id); dz = Math.Cos(a.id * 2.3 + b.id); d = 1; }
                    double push = need - Math.Max(d, 1e-4); dx /= d; dz /= d;
                    bool aFix = aBoss || a.state == "plant" || a.frozenT > 0, bFix = bBoss || b.state == "plant" || b.frozenT > 0;
                    if (aFix && bFix) continue;
                    double wa = aFix ? 0 : bFix ? 1 : 0.5, wb = 1 - wa;
                    a.pos.x -= dx * push * wa; a.pos.z -= dz * push * wa;
                    b.pos.x += dx * push * wb; b.pos.z += dz * push * wb;
                }
            }
        }

        private void FaceHead(Enemy e) { e.yaw = Math.Atan2(head.pos.x - e.pos.x, head.pos.z - e.pos.z); }

        /// <summary>待命时围着 anchor 沿 axis 慢慢来回踱步（±amp 米），相位按 phase 错开；不用 rng，不改随机序列。
        /// 用户：敌人不要站在那里不动，很假。</summary>
        private void Pace(Enemy e, Vec3d anchor, Vec3d axis, double amp, double spd, double dt, double phase)
        {
            double s = Math.Sin(e.life * 0.5 + phase) * amp;
            MoveTo(e, new Vec3d(anchor.x + axis.x * s, anchor.y, anchor.z + axis.z * s), spd, dt);
        }

        private void Leave(Enemy e, double spd, double dt)
        {
            // 门口在门面外 0.35 米：还在房间深处就先回门口，到门口后穿过门回玩具世界
            // goal === e.portal.inner 是引用相等判断，这里用对象引用
            Vec3d goal = PortalSide(e.portal, e.pos) > 0.36 ? e.portal.mouth : e.portal.inner;
            // 进门 0.3 米就算回去了（门后路边有摆件时，非要走到 inner 那一点会一直卡在门里）
            if ((MoveTo(e, goal, spd, dt) && object.ReferenceEquals(goal, e.portal.inner)) || PortalSide(e.portal, e.pos) < -0.3) {
                e.dead = true; e.deadT = 0;
                Emit(new ExitFx { type = "exit", id = e.id });
            }
        }

        private void AiPlanter(Enemy e, double dt, double spd)
        {
            var K = e.k;
            if (e.state == "enter") {
                e.spot = PickSpot(cfg.arena.flagMin, cfg.arena.flagMax, e.portal.angle);
                Reserve(e.spot);
                e.state = "walk"; e.t = 0; e.blinkT = 0;
                e.arcK = (rng() < 0.5 ? -1 : 1) * cfg.arena.detour * (0.6 + rng() * 0.4);
                e.arcFrom = null;
            }
            if (e.state == "walk") {
                double s = spd;
                if (K.move == "zigzag") {
                    double cyc = e.t % (K.windup + K.pause);
                    if (cyc > K.windup) { e.paused = true; return; }
                    e.paused = false;
                }
                if (K.move == "blink") {
                    e.blinkT -= dt;
                    if (e.blinkT <= 0 && PortalSide(e.portal, e.pos) > 0.3) {
                        e.blinkT = 1.6;
                        Vec3d d2 = V.sub(e.spot, e.pos);
                        double l2 = Math.Sqrt(d2.x * d2.x + d2.z * d2.z);
                        double hop = Math.Min(1.1, l2);
                        if (l2 > 1e-3) { e.pos.x += (d2.x / l2) * hop; e.pos.z += (d2.z / l2) * hop; }
                        Emit(new BlinkFx { type = "blink", id = e.id });
                    }
                    s = spd * 0.3;
                }
                Vec3d goal = e.spot;
                // 出了门以后：沿“门口 → 插旗点”这条线往前看 0.3 米取目标，再按 sin 曲线往一侧偏一点，走出一条自然的弧线
                if (K.move != "blink" && K.move != "zigzag" && PortalSide(e.portal, e.pos) > 0.3) {
                    if (e.arcFrom == null) e.arcFrom = V.clone(e.pos);
                    var dl = V.sub(e.spot, e.arcFrom); dl.y = 0; double L2 = dl.x * dl.x + dl.z * dl.z;
                    if (L2 > 0.04) {
                        double sProg = V.clamp(((e.pos.x - e.arcFrom.x) * dl.x + (e.pos.z - e.arcFrom.z) * dl.z) / L2, 0, 1);
                        double s2 = Math.Min(1, sProg + 0.3 / Math.Sqrt(L2));
                        var side = new Vec3d(-dl.z, 0, dl.x); double sl = Math.Sqrt(L2);
                        goal = new Vec3d(e.arcFrom.x + dl.x * s2 + side.x / sl * e.arcK * Math.Sin(Math.PI * s2), 0, e.arcFrom.z + dl.z * s2 + side.z / sl * e.arcK * Math.Sin(Math.PI * s2));
                        if (s2 >= 1) goal = e.spot;
                    }
                }
                if (K.move == "zigzag" && PortalSide(e.portal, e.pos) > 0.3) {
                    Vec3d dn = V.norm(V.sub(e.spot, e.pos));
                    Vec3d side = new Vec3d(-dn.z, 0, dn.x);
                    goal = V.add(e.spot, V.scale(side, Math.Sin(e.t * 5) * 0.6));
                    if (V.distXZ(e.pos, e.spot) < 0.35) goal = e.spot;
                }
                if (WalkVia(e, goal, s, dt) && V.distXZ(e.pos, e.spot) < 0.06) {
                    e.state = "plant"; e.t = 0;
                    Emit(new PlantingFx { type = "planting", id = e.id });
                }
            } else if (e.state == "plant") {
                FaceHead(e);
                if (e.t >= K.plantTime) { PlantFlag(e); e.state = "leave"; e.t = 0; }
            } else if (e.state == "leave") {
                if (e.t >= CheerTime) Leave(e, spd * 1.2, dt);   // 插完旗先原地转圈欢呼，再走回门里
            }
        }

        /// <summary>插旗后原地欢呼的时长（表现层的转圈动作按这个时长做）。</summary>
        public const double CheerTime = 0.8;

        private void AiShooter(Enemy e, double dt, double spd)
        {
            var K = e.k;
            if (e.state == "enter") {
                e.post = PickSpot(K.standoff[0], K.standoff[1], e.portal.angle);
                e.state = "walk"; e.shots = 0;
            }
            if (e.state == "walk") {
                if (WalkVia(e, e.post, spd, dt) && V.distXZ(e.pos, e.post) < 0.06) {
                    e.state = "aim"; e.t = 0; e.fireT = 1 + rng() * 1.2;
                }
            } else if (e.state == "aim") {
                if (e.post == null) e.post = V.clone(e.pos);
                if (e.fireT > K.telegraph + 0.3)   // 瞄准间隙：面朝玩家横向走位，预警前站定（用户：攻击型不要傻站）
                {
                    var tp = V.sub(head.pos, e.post); tp.y = 0;
                    if (V.len(tp) > 1e-3) Pace(e, e.post, V.norm(new Vec3d(-tp.z, 0, tp.x)), 0.25, 0.3, dt, e.id * 2.1);
                }
                FaceHead(e);
                e.fireT -= dt;
                if (e.fireT <= K.telegraph && !e.tele) { e.tele = true; Emit(new TelegraphFx { type = "telegraph", id = e.id }); }
                if (e.fireT <= 0) {
                    e.tele = false;
                    FireAtHead(e, "foam", K.bulletSpeed);
                    e.shots++;
                    e.fireT = K.fireEvery * ChFire * (1 + cfg.diff.assistFirePer * run.assist);
                    if (e.shots % 2 == 0) { e.post = PickSpot(K.standoff[0], K.standoff[1], e.portal.angle); e.state = "walk"; }
                }
            }
        }

        private void AiTrainer(Enemy e, double dt)
        {
            FaceHead(e);
            if (lesson?.data?.trainerFire != true) return;
            e.fireT -= dt;
            if (e.fireT <= e.k.telegraph && !e.tele) { e.tele = true; Emit(new TelegraphFx { type = "telegraph", id = e.id }); }
            if (e.fireT <= 0) { e.tele = false; FireAtHead(e, "foam", e.k.bulletSpeed); e.fireT = e.k.fireEvery; }
        }

        double ChFire => run == null ? 1 : run.mode == "chapter" ? 1 + (cfg.diff.FireMul(CHAPTERS.World(run.chapter)) - 1) * Math.Max(0, 1 - 0.3 * run.assist) : run.mode == "endless" ? cfg.diff.FireMul(6) : 1;   // 无尽：敌人攻击按最难一档
        public void FireAtHead(Enemy e, string kind, double speed)
        {
            if (run != null && run.mode == "chapter" && phase != "lesson") speed *= 1 + (cfg.diff.ShotSpeedMul(CHAPTERS.World(run.chapter)) - 1) * Math.Max(0, 1 - 0.3 * run.assist);
            else if (run != null && run.mode == "endless") speed *= cfg.diff.ShotSpeedMul(6);   // 卡关（失败辅助）时子弹提速逐层收回
            Vec3d from = new Vec3d(e.pos.x, e.pos.y + e.height * 0.7, e.pos.z);
            Vec3d to = head.pos;
            Vec3d vel = V.scale(V.norm(V.sub(to, from)), speed);
            var b = new Bullet { id = nextId++, kind = kind, pos = from, vel = vel, from = e.id, reflected = false, travelled = 0 };
            bullets.Add(b);
            Emit(new EnemyShotFx { type = "enemyShot", id = e.id, bullet = b.id, kind = kind });
        }

        private void AiPlane(Enemy e, double dt, double spd)
        {
            var K = e.k;
            if (e.state == "circle") {
                e.orbitA += dt * spd / 0.8;
                Vec3d goal = new Vec3d(
                    e.orbitC.x + Math.Cos(e.orbitA) * 0.8,
                    e.orbitC.y + Math.Sin(e.orbitA * 2) * 0.15,
                    e.orbitC.z + Math.Sin(e.orbitA) * 0.8
                );
                Vec3d d = V.sub(goal, e.pos);
                double l = V.len(d);
                double step = Math.Min(l, spd * 1.4 * dt);
                if (l > 1e-4) e.pos = V.add(e.pos, V.scale(d, step / l));
                e.vel = V.scale(V.norm(d), spd);
                e.yaw = Math.Atan2(e.vel.x, e.vel.z);
                if (e.t >= K.circleTime && PortalSide(e.portal, e.pos) > 0.5) {
                    e.state = "dive"; e.t = 0;
                    e.vel = V.scale(V.norm(V.sub(head.pos, e.pos)), K.diveSpeed);
                    Emit(new DiveFx { type = "dive", id = e.id });
                }
            } else if (e.state == "dive") {
                Vec3d want = V.scale(V.norm(V.sub(head.pos, e.pos)), K.diveSpeed);
                e.vel = V.lerp(e.vel, want, Math.Min(1, dt * 1.2));
                Vec3d prev = V.clone(e.pos);
                e.pos = V.add(e.pos, V.scale(e.vel, dt));
                e.yaw = Math.Atan2(e.vel.x, e.vel.z);
                string res = CollideIncoming(prev, e.pos, e.vel, e.radius * 0.6, null);
                if (res == "blocked" || res == "parried") {
                    DamageEnemy(e, res == "parried" ? 2 : 1);
                    if (!e.dead) PlaneClimb(e);
                } else if (res == "head") { HitHead("plane", e.pos); PlaneClimb(e); }
                else if (V.dot(V.sub(e.pos, head.pos), e.vel) > 0 && V.dist(e.pos, head.pos) > 0.6) PlaneClimb(e);
            } else if (e.state == "climb") {
                Vec3d dn = V.norm(V.sub(e.climbTo, e.pos));
                e.pos = V.add(e.pos, V.scale(dn, spd * dt));
                e.yaw = Math.Atan2(e.climbTo.x - e.pos.x, e.climbTo.z - e.pos.z);
                if (V.dist(e.pos, e.climbTo) < 0.15 || e.t > 2.5) { e.state = "circle"; e.t = 0; }
            }
        }

        private void PlaneClimb(Enemy e)
        {
            e.state = "climb"; e.t = 0;
            e.climbTo = new Vec3d(e.orbitC.x + Math.Cos(e.orbitA) * 0.8, e.orbitC.y, e.orbitC.z + Math.Sin(e.orbitA) * 0.8);
        }

        private void AiJack(Enemy e, double dt)
        {
            var K = e.k;
            e.jt -= dt;
            FaceHead(e);
            if (e.state == "closed" && e.jt <= 0) { e.state = "open"; e.jt = K.open; e.fired = false; Emit(new JackOpenFx { type = "jackOpen", id = e.id }); }
            else if (e.state == "open") {
                if (!e.fired && e.jt <= K.open - 0.4) { e.fired = true; FireAtHead(e, "confetti", K.bulletSpeed); }
                if (e.jt <= 0) { e.state = "closed"; e.jt = K.closed; Emit(new JackCloseFx { type = "jackClose", id = e.id }); }
            }
        }

        public void SummonMinions(Enemy c, int n)
        {
            int alive = 0;
            foreach (var m in enemies) if (!m.dead && m.commanderId == c.id) alive++;
            int k = Math.Min(n, c.k.maxMinions - alive);
            for (int i = 0; i < k; i++) {
                SpawnEnemy("minion", new SpawnOpts {
                    portal = c.portal, commanderId = c.id,
                    offset = (i - (k - 1) / 2.0) * 0.3, eliteOverride = null, eliteExplicit = true,
                });
            }
            if (k > 0) Emit(new SummonFx { type = "summon", id = c.id, n = k });
        }

        private void AiCommander(Enemy e, double dt)
        {
            var K = e.k;
            if (e.state == "approach")                                   // 先走到门后的指挥位置，到了才开始召唤 / 发令
            {
                if (MoveTo(e, InBox(e.portal.far, 0.5), 0.55, dt)) { e.state = "command"; e.post = V.clone(e.pos); }
                return;
            }
            if (e.post == null) e.post = V.clone(e.pos);
            if (e.order == null || e.orderAge > 1.2) Pace(e, e.post, e.portal.right, 0.35, 0.25, dt, e.id * 1.7);   // 发令那一下站定，其余时间在指挥位左右踱步
            FaceHead(e);
            e.summonT += dt;
            if (e.summonT >= K.summonEvery) { e.summonT = 0; SummonMinions(e, K.summonN); }
            e.orderT -= dt;
            if (e.orderT <= 0) {
                e.orderT = K.orderEvery;
                int out0 = 0;
                foreach (var m in enemies) if (!m.dead && m.commanderId == e.id && PortalSide(m.portal, m.pos) > 0.2) out0++;
                var opts = new List<string> { "charge", "guard" };
                if (out0 >= 2) { opts.Add("volley"); opts.Add("volley"); }
                e.order = opts[(int)Math.Floor(rng() * opts.Count)];
                e.orderAge = 0;
                for (int i = 0; i < enemies.Count; i++) {
                    var m = enemies[i];
                    if (!m.dead && m.commanderId == e.id && m.state != "flee") ApplyOrder(m, e.order);
                }
                Emit(new OrderFx { type = "order", id = e.id, order = e.order });
            }
            e.orderAge += dt;
        }

        public void ApplyOrder(Enemy m, string order)
        {
            if (m.state == "charge" || m.state == "plant" || m.state == "flee") { m.order = order; return; }   // 正在去插旗 / 插旗 / 回门：不被命令打断（职能单一）
            m.order = order; m.t = 0;
            if (order == "charge") {
                if (!m.planted) {
                    Release(m.spot);
                    m.spot = PickSpot(cfg.arena.flagMin, cfg.arena.flagMax, m.portal.angle);
                    Reserve(m.spot);
                    m.state = "charge";
                }
            } else if (order == "volley") {
                m.state = "volley"; m.volleyT = 0.6;
                Emit(new TelegraphFx { type = "telegraph", id = m.id });
            } else if (order == "guard") {
                m.state = "guard";
                var c = (Enemy)null;
                foreach (var x in enemies) if (x.id == m.commanderId) { c = x; break; }
                int idx = 0;
                foreach (var x in enemies) if (x.commanderId == m.commanderId && !x.dead) { if (x.id == m.id) break; idx++; }
                m.guardPos = V.add(m.portal.mouth, V.scale(m.portal.right, (idx - 2.5) * 0.25));
                if (c != null) m.guardPos = V.add(m.guardPos, V.scale(m.portal.normal, 0.15));
            }
        }

        private void AiMinion(Enemy e, double dt, double spd)
        {
            var c = (Enemy)null;
            foreach (var x in enemies) if (x.id == e.commanderId && !x.dead) { c = x; break; }
            if (c == null && e.state != "flee") { e.state = "flee"; e.t = 0; }
            switch (e.state) {
                case "enter":
                    // 用户：分工明确，插旗的就去插旗——跟班一出门就领一个插旗点直奔过去，不在门口排队等命令
                    if (MoveTo(e, e.portal.mouth, spd, dt)) {
                        Release(e.spot); e.spot = PickSpot(cfg.arena.flagMin, cfg.arena.flagMax, e.portal.angle); Reserve(e.spot);
                        e.state = "charge";
                        double ido = (e.id % 6) - 2.5;
                        e.waitPos = V.add(e.portal.mouth, V.add(V.scale(e.portal.right, ido * 0.3), V.scale(e.portal.normal, 0.3 + (e.id % 2) * 0.25)));
                    }
                    break;
                case "wait":
                    if (e.waitPos != null) Pace(e, e.waitPos, e.portal.right, 0.15, spd * 0.5, dt, e.commanderId * 1.3);   // 排队：原地小碎步左右挪
                    FaceHead(e);
                    break;
                case "charge":
                    if (WalkVia(e, e.spot, spd * 1.5, dt) && V.distXZ(e.pos, e.spot) < 0.06) {
                        e.state = "plant"; e.t = 0;
                        Emit(new PlantingFx { type = "planting", id = e.id });
                    }
                    break;
                case "plant":
                    FaceHead(e);
                    if (e.t >= e.k.plantTime) { PlantFlag(e); e.planted = true; e.state = "flee"; e.t = 0; }   // 插完就回门里（不再站着当护卫）
                    break;
                case "volley":
                    FaceHead(e);
                    e.volleyT -= dt;
                    if (e.volleyT <= 0) {
                        if (PortalSide(e.portal, e.pos) > 0) FireAtHead(e, "foam", e.k.bulletSpeed);
                        e.state = "wait";
                    }
                    break;
                case "guard":
                    Pace(e, e.guardPos, e.portal.right, 0.12, spd * 0.5, dt, e.commanderId * 1.3);   // 护卫：在门口来回巡逻
                    FaceHead(e);
                    break;
                case "flee":
                    Leave(e, spd * 1.8, dt);
                    break;
            }
        }

        private void AiBalloon(Enemy e, double dt, double spd)
        {
            if (e.state == "descend") {
                e.pos.y -= Math.Max(spd * 0.8, 0.4) * dt;
                e.pos.x += Math.Sin(e.life * 1.3 + e.id) * 0.08 * dt;
                e.pos.x += (e.spot.x - e.pos.x) * Math.Min(1, dt * 0.6);
                e.pos.z += (e.spot.z - e.pos.z) * Math.Min(1, dt * 0.6);
                if (e.pos.y <= 0) { e.pos.y = 0; e.state = "plant"; e.t = 0; Emit(new PlantingFx { type = "planting", id = e.id }); }
            } else if (e.state == "plant") {
                if (e.t >= e.k.plantTime) { PlantFlag(e); e.state = "rise"; e.t = 0; }
            } else if (e.state == "rise") {
                e.pos.y += 0.6 * dt;
                if (e.pos.y > 2.6) { e.dead = true; e.deadT = 0; Emit(new ExitFx { type = "exit", id = e.id }); }
            }
        }

        private void AiCar(Enemy e, double dt, double spd)
        {
            var K = e.k;
            if (e.state == "enter") { e.state = "drive"; e.t = 0; }
            if (e.state == "drive") {
                if (PortalSide(e.portal, e.pos) < 0.3) { MoveTo(e, e.portal.mouth, spd, dt); return; }
                if (e.wp == null || V.distXZ(e.pos, e.wp) < 0.1) e.wp = PickSpot(1.0, 2.6, null);
                MoveTo(e, e.wp, spd, dt);
                e.paintT -= dt;
                if (e.paintT <= 0) {
                    e.paintT = K.paintEvery;
                    paints.Add(new Paint { id = nextId++, pos = new Vec3d(e.pos.x, 0, e.pos.z), amount = K.paintAmount, @base = K.paintAmount, life = K.paintLife, maxLife = K.paintLife });
                }
                if (e.t >= K.driveTime) { e.state = "leave"; e.t = 0; }
            } else if (e.state == "leave") Leave(e, spd, dt);
        }

        private void AiRepair(Enemy e, double dt, double spd)
        {
            var K = e.k;
            if (e.state == "enter") e.state = "follow";
            Vec3d goal = null;
            double best = double.PositiveInfinity;
            foreach (var o in enemies) {
                if (o == e || o.dead || o.kind == "repair" || o.kind == "commander" || o.k.move == "fly") continue;
                if (PortalSide(o.portal, o.pos) < 0) continue;
                double d = V.distXZ(o.pos, e.pos) - (o.hp < o.maxHp ? 3 : 0);
                if (d < best) { best = d; goal = o.pos; }
            }
            foreach (var f in flags) {
                if (f.dying || f.hp >= f.maxHp) continue;
                double d = V.distXZ(f.pos, e.pos) - 2;
                if (d < best) { best = d; goal = f.pos; }
            }
            // 没有要修的（这一波它最先出门、同伴还没出来）：也先走出门，在门口里侧 1.2 米待命——
            // 原来没目标就一直站在门后出生点，门后打不到，这一波永远结束不了（模拟：第 5 章普通 / 熟练全部卡死 900 秒）
            if (goal == null) goal = V.add(e.portal.mouth, V.scale(e.portal.normal, 1.2));
            if (PortalSide(e.portal, e.pos) < 0.3) MoveTo(e, e.portal.mouth, spd, dt);
            else if (V.distXZ(e.pos, goal) > 0.7) MoveTo(e, goal, spd, dt);
            else {   // 到了：绕着要修的同伴 / 旗子慢慢转小圈（像围着它敲敲打打），半径 0.5 < 修理范围
                double a = e.life * 0.6 + e.id;
                MoveTo(e, new Vec3d(goal.x + Math.Cos(a) * 0.5, 0, goal.z + Math.Sin(a) * 0.5), spd * 0.5, dt);
            }
            e.healing = false;
            foreach (var o in enemies) {
                // 修理机器人不修同类：几只互相加血会变成打不死的僵局（模拟：新手第 5 章三只互修卡死）
                if (o == e || o.dead || o.kind == "repair" || o.hp >= o.maxHp) continue;
                if (V.distXZ(o.pos, e.pos) <= K.healRange) { o.hp = Math.Min(o.maxHp, o.hp + K.healRate * dt); e.healing = true; }
            }
            foreach (var f in flags) {
                if (f.dying || f.hp >= f.maxHp) continue;
                if (V.distXZ(f.pos, e.pos) <= K.healRange) { f.hp = Math.Min(f.maxHp, f.hp + K.healRate * dt); e.healing = true; }
            }
        }

        // ───────────────────────── 敌方子弹 ─────────────────────────
        double _knockGrace;
        void HitHead(string kind, Vec3d from)
        {
            Emit(new HeadHitFx { type = "headHit", kind = kind, dir = V.sub(head.pos, from) });
            if (kind == "grab") { Steal(); return; }
            if (kind == "drain") { DrainAmmo(); return; }
            Knock();
        }

        /// <summary>Boss 的磁铁爪：枪被抢走一会（比被打飞久，期间开不了枪），不受被打飞免疫期限制。</summary>
        void Steal()
        {
            double d = cfg.knock.stealDur * Math.Max(0.5, 1 - cfg.diff.assistKnockPer * run.assist);
            knockT = Math.Max(knockT, d); _knockGrace = knockT + cfg.knock.grace;
            foreach (var s in new[]{"left","right"}) { var w = weapons.Get(s); if (w != null) w.burstQ.Clear(); }
            Emit(new GunStolenFx { type = "gunStolen", dur = d });
        }

        /// <summary>发条震波：两把枪的弹匣都空了（正在换弹的也打断），逼玩家马上换弹。</summary>
        void DrainAmmo()
        {
            foreach (var s in new[]{"left","right"}) { var w = weapons.Get(s); if (w == null) continue; w.ammo = 0; w.reload = null; w.burstQ.Clear(); }
            Emit(new AmmoDrainFx { type = "ammoDrain" });
        }

        private void Knock()
        {
            if (knockT > 0 || _knockGrace > 0) return;
            knockT = cfg.knock.dur * Math.Max(0.4, 1 - cfg.diff.assistKnockPer * run.assist);
            _knockGrace = knockT + cfg.knock.grace;
            foreach (var s in new[]{"left","right"}) { var w = weapons.Get(s); if (w != null) w.burstQ.Clear(); }
            Emit(new KnockedFx { type = "knocked" });
        }

        // 来袭物体（子弹/纸飞机）与盾牌、头部的碰撞
        private string CollideIncoming(Vec3d prev, Vec3d pos, Vec3d vel, double radius, object ent)
        {
            var S = cfg.shield;
            var sh = shield;
            if (sh.up && sh.pos != null && sh.normal != null) {
                bool facing = V.angleDeg(sh.normal, V.scale(vel, -1)) <= S.cone + 20;
                double d = SegPointDist(prev, pos, sh.pos);
                if (facing && d < S.radius + radius) {
                    int lvParry = profile.upgrades.ContainsKey("parry") ? profile.upgrades["parry"] : 0;
                    double win = S.parryWin + UPGRADES.parry.values[lvParry];
                    if (sh.upAge <= win) {
                        counters.parried++;
                        run.stats.parries++;
                        Emit(new ParryFx { type = "parry", pos = V.clone(sh.pos) });
                        return "parried";
                    }
                    sh.dur -= 1;
                    counters.blocked++;
                    Emit(new BlockFx { type = "block", pos = V.clone(sh.pos), dur = sh.dur });
                    if (sh.dur <= 0) { sh.dur = 0; sh.broken = S.brokenCd; sh.up = false; Emit(new ShieldFx { type = "shieldBreak" }); }
                    return "blocked";
                }
            }
            if (SegPointDist(prev, pos, head.pos) < cfg.head.radius + radius) return "head";
            return null;
        }

        private void UpdateBullets(double dt)
        {
            var keep = new List<Bullet>();
            foreach (var b in bullets) {
                if (b.kind == "ball") { if (UpdateBall(b, dt)) keep.Add(b); continue; }
                Vec3d prev = V.clone(b.pos);
                if (b.reflected) {
                    var t = (Enemy)null;
                    foreach (var en in enemies) if (en.id == b.target && !en.dead) { t = en; break; }
                    if (t != null) {
                        Vec3d c = new Vec3d(t.pos.x, t.pos.y + t.height * 0.5, t.pos.z);
                        b.vel = V.scale(V.norm(V.sub(c, b.pos)), 6);
                        b.pos = V.add(b.pos, V.scale(b.vel, dt));
                        if (V.dist(b.pos, c) < t.radius + 0.1) {
                            DamageEnemy(t, 2, new DamageOpts { crit = true });
                            Emit(new ReflectHitFx { type = "reflectHit", id = t.id });
                            continue;
                        }
                    } else b.pos = V.add(b.pos, V.scale(b.vel, dt));
                    b.travelled += V.len(b.vel) * dt;
                    if (b.travelled < 10) keep.Add(b);
                    continue;
                }
                b.pos = V.add(b.pos, V.scale(b.vel, dt));
                b.travelled += V.len(b.vel) * dt;
                string res = CollideIncoming(prev, b.pos, b.vel, 0.05, b);
                if (res == "parried") {
                    // 弹反：打向正在看的敌人，否则打回射手
                    Enemy gazeE = null;
                    foreach (var en in enemies) if (en.id == gaze.lock_ && !en.dead) { gazeE = en; break; }
                    if (gazeE == null) {
                        var tgt2 = PickTarget(gazeRay.origin, gazeRay.dir, Targets(gazeRay.origin), 8);
                        if (tgt2 != null) foreach (var en in enemies) if (en.id == tgt2.id && !en.dead) { gazeE = en; break; }
                    }
                    Enemy src = null;
                    foreach (var en in enemies) if (en.id == b.from && !en.dead) { src = en; break; }
                    var tgtE = gazeE ?? src;
                    b.reflected = true;
                    b.target = tgtE?.id;
                    b.vel = V.scale(b.vel, -2);
                    b.travelled = 0;
                    keep.Add(b);
                    continue;
                }
                if (res == "blocked") continue;
                if (res == "head") { HitHead(b.kind, b.pos); continue; }
                Vec3d toHead = V.sub(head.pos, b.pos);
                if (b.travelled > 9 || (V.dot(toHead, b.vel) < 0 && V.len(toHead) > 1.2) || b.pos.y < 0) continue;
                keep.Add(b);
            }
            bullets = keep;
        }

        // ───────────────────────── 教学 ─────────────────────────
        private void UpdateLesson(double dt)
        {
            var L = lesson;
            var s = L.steps[L.i];
            if (s == null) return;
            L.t += dt;
            if (s.check(this, L.data, dt)) NextLessonStep();
        }

        // ───────────────────────── 辅助 ─────────────────────────
        /// <summary>目标上离射线最近的点：细高的目标（旗子、小怪）是一根竖条，不是空中一个点。</summary>
        public static Vec3d AimPoint(Vec3d origin, Vec3d dir, TargetInfo t)
        {
            if (t.y1 <= t.y0) return t.pos;
            double d1 = dir.x * (origin.x - t.pos.x) + dir.y * (origin.y - t.y0) + dir.z * (origin.z - t.pos.z);
            double denom = 1 - dir.y * dir.y;
            double u = denom > 1e-6 ? ((origin.y - t.y0) - dir.y * d1) / denom : t.pos.y - t.y0;
            return new Vec3d(t.pos.x, t.y0 + V.clamp(u, 0, t.y1 - t.y0), t.pos.z);
        }

        // 选目标的共同规则（用户：补给箱在 Boss 前面，怎么也打不到）：
        //   射线真的穿过了目标（偏离角 ≤ 目标角半径）= 打中它；同时穿过好几个时选离得最近的（前面的东西本来就挡住后面的），
        //   原来只比“偏离角 − 角半径”，大块头 Boss 的角半径大、分数更低或打平时排在前面，前面的补给箱 / 小怪永远轮不到。
        //   都没穿过：照旧按偏离角最小的吸附（辅助瞄准）。
        public static TargetInfo PickTarget(Vec3d origin, Vec3d dir, List<TargetInfo> targets, double marginDeg)
        {
            TargetInfo best = null, hit = null; double bestScore = double.PositiveInfinity, hitD = double.PositiveInfinity;
            foreach (var t in targets) {
                double d = V.dist(origin, t.pos);
                if (d < 0.05) continue;
                double ang = V.angleTo(origin, dir, AimPoint(origin, dir, t));
                double angR = Math.Atan(t.radius / d) / V.DEG;
                double score = ang - angR;
                if (score > marginDeg) continue;
                if (score <= 0) { if (d < hitD) { hit = t; hitD = d; } }
                else if (score < bestScore) { best = t; bestScore = score; }
            }
            return hit ?? best;
        }

        /// <summary>沿枪口找目标：射线穿过的非旗子目标里选最近的；否则按“偏离角 − 目标角半径”打分，旗子额外加罚分（同样偏离时先打敌人）。</summary>
        public static TargetInfo PickAimTarget(Vec3d origin, Vec3d dir, List<TargetInfo> targets, double marginDeg, double flagPenalty)
        {
            TargetInfo best = null, hit = null; double bestScore = double.PositiveInfinity, hitKey = double.PositiveInfinity;
            foreach (var t in targets) {
                double d = V.dist(origin, t.pos);
                if (d < 0.05) continue;
                double ang = V.angleTo(origin, dir, AimPoint(origin, dir, t));
                double angR = Math.Atan(t.radius / d) / V.DEG;
                double raw = ang - angR;
                if (raw <= 0 && t.type != "flag")                                 // 射线穿过的敌人 / 补给箱 / 机关：选最近的
                {
                    if (d < hitKey) { hit = t; hitKey = d; }
                    continue;
                }
                double score = Math.Max(0, raw);                                  // 旗子照旧：同样对准时加罚分，敌人优先（眼动不准时别老打到旗上）
                if (score > marginDeg) continue;
                if (t.type == "flag") score += flagPenalty;
                if (score < bestScore) { best = t; bestScore = score; }
            }
            return hit ?? best;
        }

        public static double SegPointDist(Vec3d a, Vec3d b, Vec3d p)
        {
            Vec3d ab = V.sub(b, a);
            double l2 = V.dot(ab, ab);
            double t = l2 > 1e-9 ? V.clamp(V.dot(V.sub(p, a), ab) / l2, 0, 1) : 0;
            return V.dist(V.add(a, V.scale(ab, t)), p);
        }

        // ───────────────────────── 教学步骤（闭包改成委托） ─────────────────────────
        private static bool HoldState(Game g, LessonData d, double dt, string state, double sec, bool both = false)
        {
            var hs = g.lastHands ?? new Dictionary<string, string>();
            string ls = hs.ContainsKey("left") ? hs["left"] : null;
            string rs = hs.ContainsKey("right") ? hs["right"] : null;
            bool ok = both ? ls == state && rs == state : ls == state || rs == state;
            d.hold = ok ? d.hold + dt : 0;
            return d.hold >= sec;
        }

        private static void SpawnDummies(Game g, LessonData d, Vec3d[] list)
        {
            var ids = new List<int>();
            foreach (var pos in list) ids.Add(g.SpawnEnemy("dummy", new SpawnOpts { pos = pos, portal = g.portals[0], eliteExplicit = true, fromDoor = true }).id);
            d.ids = ids.ToArray();
        }

        private static bool DummiesDead(Game g, LessonData d, double dt)
        {
            foreach (var id in (d.ids ?? new int[0])) {
                foreach (var e in g.enemies) if (e.id == id && !e.dead) return false;
            }
            return true;
        }

        public static readonly Dictionary<string, LessonStep[]> LESSONS = new Dictionary<string, LessonStep[]> {
            {"gun", new LessonStep[] {
                new LessonStep {
                    text="把手抬到身前，比出手指枪", sub="食指伸出，其余手指弯起来，不用举直手臂", en="Raise a hand in front of you and make a finger gun", enSub="Index finger out, other fingers curled. No need to straighten your arm.", gesture="gun",
                    check=(g,d,dt)=>HoldState(g,d,dt,"gun",0.4)
                },
                new LessonStep {
                    text="看着靶子，手腕往上一挑", sub="砰！看着哪个就打中哪个", en="Look at a target and flick your wrist up", enSub="Bang! Whatever you look at is what you hit.", gesture="flick",
                    setup=(g,d)=>SpawnDummies(g,d,new Vec3d[]{new Vec3d(-0.7,0.9,-2.4),new Vec3d(0,1.2,-2.6),new Vec3d(0.7,0.9,-2.4)}),
                    check=DummiesDead,
                },
                // 换弹不单独练：第一次打空时枪上会弹出“握拳换弹”提示卡，边玩边学（真机试玩：教学步骤太多太拖）
                new LessonStep {
                    text="玩具要来插旗了！", sub="趁它插旗前打掉它，插下的旗子也能打倒", en="Toys are coming to plant flags!", enSub="Stop them before they plant. Planted flags can be shot down too.", gesture="flag",
                    setup=(g,d)=>{ d.e=g.SpawnEnemy("planter",new SpawnOpts{portal=g.portals[0],eliteExplicit=true}).id; },
                    check=(g,d,dt)=>{
                        bool alive=false; foreach(var e in g.enemies) if(e.id==d.e&&!e.dead){alive=true;break;}
                        return !alive && g.flags.TrueForAll(f=>f.dying);
                    },
                },
            }},
            {"shield", new LessonStep[] {
                new LessonStep {
                    text="新装备：盾牌！", sub="抬手到脸前，五指张开、掌心朝外", en="New gear: Shield!", enSub="Raise a hand in front of your face, fingers spread, palm facing out.", gesture="palm",
                    check=(g,d,dt)=>HoldState(g,d,dt,"shield",0.5)
                },
                new LessonStep {
                    text="举盾挡住泡沫弹", sub="挡住 2 发", en="Block the foam darts with your shield", enSub="Block 2 of them", gesture="palm",
                    setup=(g,d)=>{
                        var e=g.SpawnEnemy("trainer",new SpawnOpts{pos=new Vec3d(0,0,-2.6),portal=g.portals[0],eliteExplicit=true,fromDoor=true});   // 从门里走出来（不凭空出现）
                        e.hp=999; e.maxHp=999; d.trainer=e.id; d.trainerFire=true; d.b0=g.counters.blocked; d.p0=g.counters.parried;
                    },
                    check=(g,d,dt)=>g.counters.blocked+g.counters.parried-d.b0-d.p0>=2,
                },
                new LessonStep {
                    text="弹反！", sub="子弹快到时才举盾，子弹会飞回去", en="Parry!", enSub="Raise the shield just before a dart hits and it flies right back.", gesture="palm",
                    setup=(g,d)=>{ d.trainerFire=true; d.p0=g.counters.parried; },
                    check=(g,d,dt)=>g.counters.parried-d.p0>=1,
                },
            }},
            {"binoc", new LessonStep[] {
                new LessonStep {
                    text="新装备：望远镜！", sub="空心拳举到眼前", en="New gear: Binoculars!", enSub="Hold a loose fist up to your eye.", gesture="binoc",
                    check=(g,d,dt)=>HoldState(g,d,dt,"binoc",0.5)
                },
                new LessonStep {
                    text="盯住远处的靶子", sub="透过传送门看过去，盯一会儿就标记上了", en="Stare at the far target", enSub="Look through the portal and hold your gaze to mark it.", gesture="binoc",
                    setup=(g,d)=>{ var p=g.portals[0]; d.ids=new int[]{g.SpawnEnemy("dummy",new SpawnOpts{pos=new Vec3d(p.far.x,0.4,p.far.z),portal=p,eliteExplicit=true}).id}; },
                    check=(g,d,dt)=>g.marks.ContainsKey(d.ids[0]),
                },
                new LessonStep {
                    text="放下望远镜，开枪！", sub="被标记的目标必定暴击，远了也不减伤害", en="Lower the binoculars and fire!", enSub="Marked targets always take critical hits, even far away.", gesture="flick",
                    check=DummiesDead,
                },
            }},
            {"dual", new LessonStep[] {
                new LessonStep {
                    text="新装备：双枪！", sub="两只手都比出手指枪", en="New gear: Dual Wield!", enSub="Make a finger gun with each hand.", gesture="gun",
                    check=(g,d,dt)=>HoldState(g,d,dt,"gun",0.4,true)
                },
                new LessonStep {
                    text="一眼一个，一手一个", sub="眼睛盯住一个，另一只手指着另一个", en="One by eye, one by hand", enSub="Look at one target and point your other hand at another.", gesture="flick",
                    setup=(g,d)=>SpawnDummies(g,d,new Vec3d[]{new Vec3d(-1.2,1.0,-2.2),new Vec3d(1.2,1.0,-2.2)}),
                    check=DummiesDead,
                },
            }},
            {"burst", new LessonStep[] {
                new LessonStep {
                    text="新枪：泡沫连发枪！", sub="一挑三连发，可以在主菜单的枪架上换枪", en="New gun: Foam Burster!", enSub="Three shots per flick. Swap guns on the rack in the main menu.", gesture="flick",
                    check=(g,d,dt)=>{ d.t+=dt; return d.t>3.5; }
                },
            }},
            {"cannon", new LessonStep[] {
                new LessonStep {
                    text="新枪：软木塞手炮！", sub="威力大，打旗子伤害加倍", en="New gun: Cork Cannon!", enSub="Deals double damage to flags.", gesture="flick",
                    check=(g,d,dt)=>{ d.t+=dt; return d.t>3.5; }
                },
            }},
        };
    }

    // ───────────────────────── 选项类 ─────────────────────────
    public class GameOpts
    {
        public Config cfg;
        public uint? seed;
        public ISaveStore store;
    }

    public class StartChapterOpts
    {
        public ResumeData resume;
        public bool skipLesson;
    }

    public class SpawnOpts
    {
        public Portal portal;
        public Vec3d pos;
        public string eliteOverride;     // null = 走随机逻辑；"null" 字符串不用，只用 null
        public bool eliteExplicit;       // true = 强制用 eliteOverride（包括 null）
        public bool fromSwarm;
        public bool fromDoor;            // 教学靶：从门里飘出来到 pos（不凭空出现）
        public double offset;
        public int commanderId;
        public Vec3d spot;               // balloon 用
    }
}
