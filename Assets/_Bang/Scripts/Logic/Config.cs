// 全部可调参数集中在这里（与设计文档 v3 第 12 节对应）。单位：米、秒、度。
namespace Bang.Logic
{
    public class HandConfig
    {
        // 手指伸直程度 = |指尖-手腕| / |近节指骨根-手腕|；伸直约 1.9，弯曲约 1.1
        public double extOn = 1.62, extOff = 1.5;
        public double curlOn = 1.38, curlOff = 1.48;
        public double pinchOn = 0.02, pinchOff = 0.035;
        public double holdSec = 0.08; // 手势状态至少保持多久才切换（防抖）
        public double gunHold = 0.18; // 手型从持枪变成“什么都不是”要保持这么久才收枪：挑腕瞬间手指常会松开、追踪也会晃
        // 手型不用标准：枪口大致朝前时，食指明显比无名指、小指伸得开就算持枪（用户：差不多的手势就行，要结合枪的方向）
        public double looseIndex = 1.42;   // 食指伸直程度下限
        public double looseGap = 0.2;      // 食指比无名指、小指平均值多伸开这么多
        public double looseAim = 60;       // 手指方向和头朝向的夹角在这个角度内算“枪口朝前”
        public double looseReach = 0.42;   // 枪口朝前时，手到肩的距离只要到臂长的这个比例
        public double lostGrace = 0.15; // 追踪短暂丢失这么久以内不算手没了：手动得快时最容易丢帧，正好是挑腕的时候
    }
    public class ArmConfig
    {
        public double straightRatio = 0.6;  // 手到估算肩膀的距离 ≥ 臂长 × 0.6 就能持枪（真机试玩：手不用举直，放在胸前也能瞄，手臂不累）
        public double straightOff = 0.52;   // 退出阈值（垂在腰边的手不算持枪，防误触）
        public double defaultLen = 0.62;    // 未校准时的默认臂长
        public double shoulderDown = 0.22;  // 肩膀在眼睛下方
        public double shoulderSide = 0.17;  // 肩膀离中线
    }
    public class FlickConfig
    {
        public double minVel = 130;   // 手腕上挑角速度（°/s）（真机试玩：200 时轻挑经常不出枪）
        public double minRise = 7;    // 累计抬起角度（°）
        public double onsetSlack = 1.5; // 起点取“最靠近现在、且离最低点不超过这么多度”的样本：稳住时的追踪抖动不会把起点拖到很久以前，把一次快挑算成慢动作
        public double maxWristSpeed = 0.6; // 按手自身方向判定挑腕时，手腕移动不能超过这个速度（米/秒）：整条手臂横扫瞄准不算挑腕
        public double window = 0.2;   // 抬起要在这么短的时间内完成
        public double aimLookback = 0.1; // 瞄准方向取挑腕前这么久（注：原型中未实际生效）
        public double minInterval = 0.15;
    }
    public class BinocConfig
    {
        public double eyeDist = 0.14; public double zoom = 3;
        public double markDwell = 0.4; public int maxMarks = 3; public double markDur = 8;
    }
    public class ShieldConfig
    {
        public double durability = 5; public double regenDelay = 1.5; public double regenRate = 1;
        public double brokenCd = 4; public double cone = 50; public double parryWin = 0.2; public double radius = 0.32;
    }
    public class GazeConfig { public double margin = 4; public double dwell = 0.08; public double lostGrace = 0.35; public double flagPenalty = 1; }
    public class AimConfig { public double weakMul = 2; public double cone = 30; public double assist = 14; public double flagPenalty = 2.5; public double offhandSnap = 6; public double falloffDist = 6; public double falloffMul = 0.5; public double critMul = 2; }
    public class KnockConfig { public double dur = 1.3; public double grace = 1.6; public double stealDur = 3.0; }   // 11-04 用户：难度不高，被打掉枪是很好的设定 → 打飞更久、免疫期更短；Boss 抢枪 3 秒   // 被打飞后的免疫期：不会连续被打飞（真机试玩：挫败感最强的一项）
    public class HeadConfig { public double radius = 0.15; }
    public class ReloadConfig { public double total = 1.4; public double goldStart = 0.45; public double goldEnd = 0.65; public double fastFinish = 0.1; public double jam = 0.8; }
    public class ToyConfig { public double loseAt = 100; public double recede = 4; public int flagsToFullEndless = 12; }   // 每面立着的旗 = 100 / n %（n 按章：ChapterDef.flagsToFull；无尽模式 12）
    public class FlowConfig { public double finaleHold = 13; public double rest = 8; public double station = 30; public int stationAfterWave = 3; public double breakAfter = 8 * 60; public double lostHold = 3.2; public double clearHold = 4; public double longPlayRemind = 15 * 60; }
    public class ArenaConfig { public double flagMin = 1.1; public double flagMax = 2.3; public double flagGap = 0.9; public double frontHalfAngle = 75; public double detour = 0.35; }   // 出门后走一条轻微的弧线去插旗点（最多偏 0.35 米，不是明显的拐弯）
    public class PortalConfig { public double width = 1.3; public double height = 1.8; public double dist = 3.7; public double inside = 0.7; public double commanderBehind = 3.6; public double spawnDepth = 0.9; }   // 门后 0.7 + 0.9 = 1.6 m 出生：第 1 章小火车的轨道从门后约 2 m 处横穿，再深小怪就站在铁轨上；门的进出通道（BangDeclutter.Lane*）里不摆东西   // dist 和场景里的门（BangSceneBuilder.PortalDist）一致

    /// <summary>难度系数（迁移计划 3.3：数量 +25%、出怪间隔 −12%、旗子增长 +15%）。单测里可以设回 1 对照原型。</summary>
    public class DifficultyConfig
    {
        // 按章递增（下标 = 章节，0 号不用）：第 1 章是入门章不加难度，第 2 章加一半，第 3 章起全加
        // 2026-10-31 用户：玩家完全感受不到房间被玩具化 → 整体加难；11-02 再加一档（用户：在好玩的基础上加点挑战）
        public double[] countMulCh = { 1, 1.5, 1.95, 2.0, 1.85, 2.05, 1.95 };   // 第 1 世界（第 1、2 关）是入门，少加   // 11-05 用户：只有最后一关最后一波才有点挑战 → 前面各关整体再加一档   // 第 4 世界（第 7、8 关：三扇门）本身最难，少加   // 11-04 用户：难度稍微有些简单了 → 再加一档（数量 +12%、间隔 −10%、旗子 +10%、Boss 血量 +25%）   // 第 3 章后两波本身很密（指挥官召唤），不再多加      // 平衡模拟校准过：第 4、6 章大波最容易卡人，收一点
        public double[] gapMulCh = { 1, 0.66, 0.62, 0.6, 0.64, 0.6, 0.6 };
        public double[] flagRateMulCh = { 1, 1.95, 1.98, 1.98, 2.03, 2.03, 1.98 };   // 真机试玩：整体偏难，后期系数收回一截
        // 11-04 用户：难度不高、有些顺 → 每波更多敌人之外，敌人对玩家的攻击也加强：开火间隔按章缩短、子弹更快（举盾 / 弹反要更早反应）
        // 三星：本章一次没输 + 全程峰值玩具化不超过这个值（按章；用 BangBalanceSim.StarCheck 校准：熟练玩家约一半能拿、普通玩家偶尔能拿。
        // 用户：每一关的三星要确保有人能打出来）。下标 = 关卡号（1..13）
        public double[] starPeak = { 0, 40, 40, 15, 25, 15, 25, 30, 60, 25, 20, 25, 42, 35 };   // 11-05 加难后复查：第 8、12 关按熟练玩家峰值中位数放宽
        public double StarPeak(int ch) => ch >= 1 && ch < starPeak.Length ? starPeak[ch] : 25;
        // Boss 耐打度（按关，下标 = 关卡号）：伤害除以这个数。入门关（第 2 关）和终极决战（连打 6 个 Boss）低一些，其余关 1
        public double[] bossToughCh = { 1, 1, 0.6, 1, 1, 1, 1, 1, 0.85, 1, 1, 1, 0.95, 0.5 };
        public double BossTough(int ch) => ch >= 1 && ch < bossToughCh.Length ? bossToughCh[ch] : 1;
        public double bossAssistPer = 0.15;   // 失败辅助：每层 Boss 少耐打 15%
        public bool bossSpecials = true;      // Boss 特技（连插旗 / 抢枪 / 震空弹匣）
        public double multiFlagPer = 1;       // 连插旗：每阶段多插几面（1 + 阶段 × 这个）
        public double[] fireMulCh = { 1, 0.88, 0.78, 0.74, 0.78, 0.7, 0.68 };
        public double[] shotSpeedMulCh = { 1, 1.08, 1.15, 1.2, 1.18, 1.25, 1.28 };
        public double FireMul(int ch) => ch >= 1 && ch < fireMulCh.Length ? fireMulCh[ch] : 1;
        public double ShotSpeedMul(int ch) => ch >= 1 && ch < shotSpeedMulCh.Length ? shotSpeedMulCh[ch] : 1;
        public double CountMul(int ch) => ch >= 1 && ch < countMulCh.Length ? countMulCh[ch] : 1;
        public double GapMul(int ch) => ch >= 1 && ch < gapMulCh.Length ? gapMulCh[ch] : 1;
        public double FlagRateMul(int ch) => ch >= 1 && ch < flagRateMulCh.Length ? flagRateMulCh[ch] : 1;
        /// <summary>全部设回 1（对照原型）。</summary>
        // 失败辅助（同一波连续失败，每层）：出怪间隔放宽、旗子长得慢；最多 5 层。只帮正在卡关的玩家
        public double assistGapPer = 0.25, assistFlagPer = 0.12, assistCountPer = 0.1,   // 第 3 层起每层少出 10% 小怪
            assistFirePer = 0.2, assistKnockPer = 0.12, assistFlagRecede = 0.15;                                  // 每层：敌人开火间隔 +20%、枪被打飞的时间 −12%
        public void Flat() { for (int i = 0; i < countMulCh.Length; i++) { countMulCh[i] = 1; gapMulCh[i] = 1; flagRateMulCh[i] = 1; fireMulCh[i] = 1; shotSpeedMulCh[i] = 1; } }
    }

    /// <summary>门后机关 / 补给礼盒 / 门涌的参数。</summary>
    public class MechConfig
    {
        public bool enabled = true;                                // 关掉 = 没有门后机关 / 礼盒 / 门涌（对照原型）
        public double catapultPeriod = 10, catapultWind = 1.78, catapultDoorY = 1.35, catapultGravity = 3, catapultHp = 6, catapultBroken = 20, catapultFirstDelay = 6;
        public int catapultFromWave = 2;                         // 第 3 波起（第 1 章是入门章，前两波先熟悉开枪和插旗）
        public int frenzyCount = 24;                                // 无限火力期间涌出来给玩家扫的小怪数量
        public double giftRapidDur = 10, giftRapidInterval = 0.09;   // 无限火力：10 秒、每秒约 11 发
        public int giftExtraAt = 8, giftThirdAt = 12;               // 这一波小怪达到这么多：必出第 2 个礼盒 / 再加第 3 个
        public double giftLife = 6, giftHp = 1, giftEnter = 1.4, giftFirstMin = 8, giftFirstMax = 16, giftSecondChance = 0.5, giftGoldDur = 10, giftFreezeDur = 4;
        public double surgeWarn = 2, surgeGap = 0.35, surgeAtMin = 12, surgeAtMax = 22; public int surgeCount = 3, surgeFromWave = 2;
    }

    public class Config
    {
        public DifficultyConfig diff = new DifficultyConfig();
        public MechConfig mech = new MechConfig();
        public HandConfig hand = new HandConfig();
        public ArmConfig arm = new ArmConfig();
        public FlickConfig flick = new FlickConfig();
        public BinocConfig binoc = new BinocConfig();
        public ShieldConfig shield = new ShieldConfig();
        public GazeConfig gaze = new GazeConfig();
        public AimConfig aim = new AimConfig();
        public KnockConfig knock = new KnockConfig();
        public HeadConfig head = new HeadConfig();
        public ReloadConfig reload = new ReloadConfig();
        public ToyConfig toy = new ToyConfig();
        public FlowConfig flow = new FlowConfig();
        public ArenaConfig arena = new ArenaConfig();
        public PortalConfig portal = new PortalConfig();
        public int maxAlive = 12;   // 11-05：场上同时更多敌人

        // 单例，与 JS 的 CONFIG 对应
        public static readonly Config Default = new Config();

        /// <summary>手势灵敏度（设置里切换）：0 宽松 / 1 标准 / 2 严格。改的是持枪手型的宽容度和挑腕开火的门槛。</summary>
        public void SetSensitivity(int lvl)
        {
            var H = hand; var F = flick;
            switch (lvl)
            {
                case 0: H.looseIndex = 1.34; H.looseGap = 0.14; H.looseAim = 70; H.looseReach = 0.38; F.minVel = 110; F.minRise = 6; break;
                case 2: H.looseIndex = 1.55; H.looseGap = 0.3; H.looseAim = 45; H.looseReach = 0.5; F.minVel = 160; F.minRise = 9; break;
                default: H.looseIndex = 1.42; H.looseGap = 0.2; H.looseAim = 60; H.looseReach = 0.42; F.minVel = 130; F.minRise = 7; break;
            }
        }
    }

    // 枪械定义
    public class GunDef
    {
        public string id, name, en;
        public double dmg; public int mag; public double interval; public int perFlick;
        public double burstGap = 0;
        public bool armorBreak = false;
        public double flagMul = 1;
        public double knockback = 0;
        public double slow = 0;
    }

    public static class GUNS
    {
        public static readonly GunDef pistol = new GunDef { id = "pistol", name = "小沙鹰", en = "Little Eagle", dmg = 1, mag = 24, interval = 0.2, perFlick = 1 };          // 真机试玩：12 发仍要频繁换弹，打断节奏
        public static readonly GunDef burst = new GunDef { id = "burst", name = "泡沫连发枪", en = "Foam Burster", dmg = 0.5, mag = 24, interval = 0.35, perFlick = 3, burstGap = 0.06 };
        public static readonly GunDef cannon = new GunDef { id = "cannon", name = "软木塞手炮", en = "Cork Cannon", dmg = 4, mag = 3, interval = 0.6, perFlick = 1, armorBreak = true, flagMul = 2, knockback = 0.6 };
        public static readonly GunDef bubble = new GunDef { id = "bubble", name = "泡泡枪", en = "Bubble Pop", dmg = 1, mag = 9, interval = 0.3, perFlick = 1, slow = 3 };

        public static GunDef Get(string id) {
            switch(id) {
                case "pistol": return pistol;
                case "burst": return burst;
                case "cannon": return cannon;
                case "bubble": return bubble;
                default: return null;
            }
        }
    }

    // 旗子定义
    public class FlagDef { public double hp; public double rate; public double cap; public double radius; }
    public static class FLAGS
    {
        public static readonly FlagDef small = new FlagDef { hp = 4, rate = 0.4, cap = 12, radius = 1.0 };     // 真机试玩：10 血要打 10 枪太肉
        public static readonly FlagDef big = new FlagDef { hp = 4, rate = 0.8, cap = 30, radius = 1.6 };
        public static FlagDef Get(string type) { return type == "big" ? big : small; }
    }

    // 升级定义
    public class UpgradeDef { public string name; public int[] costs; public double[] values; }
    public static class UPGRADES
    {
        public static readonly UpgradeDef mag = new UpgradeDef { name = "弹匣扩容", costs = new[]{30,60,120}, values = new double[]{0,2,4,6} };
        public static readonly UpgradeDef reload = new UpgradeDef { name = "换弹更快", costs = new[]{30,70,140}, values = new double[]{1,0.9,0.8,0.7} };
        public static readonly UpgradeDef gold = new UpgradeDef { name = "金区加宽", costs = new[]{25,60,110}, values = new double[]{0,0.05,0.1,0.15} };
        public static readonly UpgradeDef shield = new UpgradeDef { name = "盾牌加厚", costs = new[]{40,90}, values = new double[]{0,1,2} };
        public static readonly UpgradeDef parry = new UpgradeDef { name = "弹反窗口", costs = new[]{40,90}, values = new double[]{0,0.05,0.1} };

        public static UpgradeDef Get(string key) {
            switch(key) {
                case "mag": return mag;
                case "reload": return reload;
                case "gold": return gold;
                case "shield": return shield;
                case "parry": return parry;
                default: return null;
            }
        }
    }
}
