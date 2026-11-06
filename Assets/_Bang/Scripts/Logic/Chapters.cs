// 6 章主线 + 无尽模式（设计文档 v3 第 7 节）。
using System;
using System.Collections.Generic;
namespace Bang.Logic
{
    public class WaveDef
    {
        public List<(string kind, int count)> spawns;
        public double gap;
        public double hpMul = 1;
        public double eliteChance = 0;
        public bool big = false;
        public bool rush = false;   // 无尽模式的小怪潮波
    }

    public class ChapterDef
    {
        public int id;
        public string theme, name, en;
        public double[] portals;
        public string startItem, reward, lesson;
        public double toyMul = 1;
        public double eliteChance = 0;
        public int flagsToFull = 10;        // 房间整个玩具化（判负）要立着几面旗：每面 = 100 / n %（用户：5 面只是举例，太少的话小怪一多就乱）
        public string mech;                 // 门后机关：catapult（第 1 章积木投石车）……
        public int world = 1;               // 门后世界 / 主题音乐（1..6）
        public bool final;                  // 终极决战：Boss 写在出怪表里
        public WaveDef[] waves;
    }

    public class RewardText { public string name, en, how, enHow; }

    public static class CHAPTERS
    {
        // 13 关：6 个主题各两章（上篇引入新玩具，下篇加难度、最后一波出 Boss），第 13 关终极决战（每波一个 Boss，最后一波两个）。
        // 索引 0 为 null，章节从 1 开始。world = 用哪个门后世界（World_W1..6）和主题音乐。
        static WaveDef W(double gap, params (string, int)[] sp) => new WaveDef { spawns = new List<(string, int)>(sp), gap = gap };
        static WaveDef Big(double gap, params (string, int)[] sp) => new WaveDef { spawns = new List<(string, int)>(sp), gap = gap, big = true };
        public static readonly ChapterDef[] All = new ChapterDef[]
        {
            null,
            new ChapterDef { id=1, world=1, flagsToFull=8, theme="blocks", name="积木小镇", en="Block Town", portals=new double[]{0},
                startItem="pistol", reward="", lesson="gun", toyMul=0.7, mech="catapult", waves=new[]{
                W(3.2, ("planter",3)), W(2.9, ("planter",4),("mouse",1)), W(2.9, ("planter",4),("mouse",2)),
                W(2.8, ("mouse",2),("planter",4),("brute",1)), Big(2.4, ("planter",5),("mouse",3),("brute",1)) } },
            new ChapterDef { id=2, world=1, flagsToFull=8, theme="blocks", name="积木小镇 · 巨人来了", en="Block Town · The Giant", portals=new double[]{0},
                startItem="pistol", reward="shield", lesson="", toyMul=0.75, mech="catapult", waves=new[]{
                W(2.8, ("planter",4),("mouse",2)), W(2.7, ("planter",5),("brute",1)), W(2.7, ("mouse",3),("planter",4),("brute",1)),
                W(2.5, ("planter",5),("mouse",3),("brute",2)), Big(2.2, ("planter",6),("mouse",4),("brute",1)) } },
            new ChapterDef { id=3, world=2, flagsToFull=8, theme="army", name="塑料兵营", en="Plastic Barracks", portals=new double[]{-35,35},
                startItem="shield", reward="", lesson="shield", toyMul=0.85, mech="mortar", waves=new[]{
                W(2.5, ("planter",4),("shooter",1)), W(2.4, ("shooter",2),("planter",3)), W(2.3, ("shooter",2),("plane",2),("planter",3)),
                W(2.2, ("shooter",2),("plane",2),("planter",4),("mouse",2)), Big(1.9, ("planter",5),("mouse",3),("shooter",2),("plane",2)) } },
            new ChapterDef { id=4, world=2, flagsToFull=8, theme="army", name="塑料兵营 · 坦克出动", en="Plastic Barracks · Tank Rolls", portals=new double[]{-35,35},
                startItem="shield", reward="binoc", lesson="", toyMul=0.85, mech="mortar", waves=new[]{
                W(2.3, ("shooter",2),("plane",2),("planter",3)), W(2.2, ("shooter",3),("planter",4),("mouse",2)), W(2.1, ("plane",3),("shooter",2),("brute",1),("planter",3)),
                W(2.0, ("shooter",3),("plane",2),("planter",4)), Big(1.8, ("planter",6),("mouse",4),("shooter",3),("plane",2),("brute",1)) } },
            new ChapterDef { id=5, world=3, flagsToFull=10, theme="castle", name="锡兵城堡", en="Tin Castle", portals=new double[]{-30,30},
                startItem="binoc", reward="", lesson="binoc", mech="gate", waves=new[]{
                W(2.3, ("tin",2),("planter",3)), W(2.2, ("tin",3),("planter",3),("shooter",1)), W(2.1, ("ninja",2),("planter",4),("shooter",1)),
                W(1.9, ("tin",2),("ninja",2),("shooter",2),("mouse",2)), Big(1.6, ("tin",2),("ninja",2),("planter",4),("shooter",2),("plane",2)) } },
            new ChapterDef { id=6, world=3, flagsToFull=10, theme="castle", name="锡兵城堡 · 大将军", en="Tin Castle · The General", portals=new double[]{-30,30},
                startItem="binoc", reward="dual", lesson="", mech="gate", waves=new[]{
                W(2.4, ("commander",1),("planter",2)), W(2.0, ("tin",3),("ninja",2),("shooter",1)), W(2.2, ("commander",1),("tin",2),("planter",3)),
                W(1.8, ("tin",2),("ninja",2),("shooter",2),("plane",2),("mouse",2)), Big(1.5, ("commander",1),("tin",2),("ninja",2),("planter",4),("shooter",2),("mouse",2)) } },
            new ChapterDef { id=7, world=4, flagsToFull=10, theme="party", name="气球派对", en="Balloon Party", portals=new double[]{-70,0,70},
                startItem="dual", reward="", lesson="dual", mech="popper", waves=new[]{
                W(1.9, ("balloon",3),("planter",4),("mouse",1)), W(1.75, ("rcCar",2),("planter",4),("mouse",2)), W(1.6, ("balloon",3),("rcCar",1),("planter",3),("shooter",1)),
                W(1.6, ("jack",2),("balloon",2),("planter",3)), Big(1.3, ("balloon",3),("rcCar",2),("jack",1),("planter",5),("mouse",2)) } },
            new ChapterDef { id=8, world=4, flagsToFull=10, theme="party", name="气球派对 · 气球之王", en="Balloon Party · The Balloon King", portals=new double[]{-70,0,70},
                startItem="dual", reward="burst", lesson="", mech="popper", waves=new[]{
                W(1.7, ("jack",2),("balloon",3),("planter",3),("shooter",1)), W(1.6, ("rcCar",2),("tin",2),("planter",4)), W(1.5, ("balloon",3),("shooter",2),("plane",2),("rcCar",1)),
                W(1.4, ("balloon",2),("rcCar",2),("tin",2),("jack",2),("planter",3)), Big(1.15, ("balloon",4),("rcCar",2),("planter",6),("mouse",4),("shooter",2),("commander",1)) } },
            new ChapterDef { id=9, world=5, flagsToFull=12, theme="factory", name="发条工厂", en="Wind-up Works", portals=new double[]{-80,0,80},
                startItem="burst", reward="", lesson="burst", mech="clock", waves=new[]{
                W(1.75, ("repair",1),("planter",5),("mouse",2)), W(1.6, ("repair",1),("tin",3),("shooter",2)), W(1.5, ("repair",1),("brute",1),("ninja",2),("mouse",3)),
                W(1.45, ("repair",1),("balloon",2),("rcCar",2),("planter",4)), Big(1.25, ("repair",2),("tin",2),("ninja",2),("shooter",2),("planter",5)) } },
            new ChapterDef { id=10, world=5, flagsToFull=12, theme="factory", name="发条工厂 · 发条巨偶", en="Wind-up Works · The Colossus", portals=new double[]{-80,0,80},
                startItem="burst", reward="cannon", lesson="", mech="clock", waves=new[]{
                W(1.55, ("repair",1),("tin",3),("shooter",2),("mouse",3)), W(1.45, ("repair",2),("ninja",2),("balloon",3),("planter",4),("plane",1)),
                W(1.4, ("repair",1),("commander",1),("rcCar",2),("plane",2),("jack",2),("planter",3)), W(1.4, ("repair",1),("brute",1),("tin",2),("shooter",2),("jack",1),("planter",4)),
                Big(1.15, ("repair",2),("brute",1),("tin",2),("ninja",2),("shooter",2),("plane",2),("planter",6),("rcCar",2)) } },
            new ChapterDef { id=11, world=6, flagsToFull=12, theme="toybox", name="玩具箱总动员", en="Toybox Uprising", portals=new double[]{-80,-25,25,80},
                startItem="cannon", reward="", lesson="cannon", eliteChance=0.04, mech="spring", waves=new[]{
                W(2.0, ("planter",5),("mouse",3),("shooter",2),("tin",2)), W(1.9, ("commander",1),("balloon",3),("rcCar",2),("repair",1)),
                W(1.8, ("ninja",3),("jack",2),("plane",3),("brute",1)), W(1.7, ("commander",1),("tin",3),("repair",1),("shooter",2),("mouse",4)),
                Big(1.5, ("brute",1),("tin",2),("ninja",2),("balloon",3),("rcCar",2),("shooter",2),("plane",2),("jack",2),("planter",6)) } },
            new ChapterDef { id=12, world=6, flagsToFull=12, theme="toybox", name="玩具箱总动员 · 玩具箱之王", en="Toybox Uprising · The Toybox King", portals=new double[]{-80,-25,25,80},
                startItem="cannon", reward="", lesson="", eliteChance=0.06, mech="spring", waves=new[]{
                W(1.9, ("planter",5),("mouse",3),("shooter",2),("tin",2),("ninja",1)), W(1.8, ("commander",1),("balloon",3),("rcCar",2),("repair",1),("jack",1)),
                W(1.7, ("ninja",3),("jack",2),("plane",3),("brute",2)), W(1.6, ("commander",1),("tin",3),("repair",1),("shooter",2),("mouse",4),("balloon",2)),
                Big(1.4, ("commander",1),("brute",1),("tin",2),("ninja",2),("balloon",3),("rcCar",2),("repair",1),("shooter",2),("plane",2),("jack",2),("planter",6)) } },
            new ChapterDef { id=13, world=6, final=true, flagsToFull=14, theme="toybox", name="终极决战", en="The Final Showdown", portals=new double[]{-80,-25,25,80},
                startItem="cannon", reward="endless", lesson="", eliteChance=0.04, mech="", waves=new[]{
                // 用户：一共六波、一波一个 Boss（1 → 6 依次登场），不要两个一起出现
                W(2.0, ("boss1",1),("planter",4),("mouse",3)), W(1.9, ("boss2",1),("shooter",2),("plane",2),("planter",3)),
                W(1.8, ("boss3",1),("tin",2),("planter",3),("mouse",2)), W(1.8, ("boss4",1),("balloon",2),("rcCar",2),("planter",3)),
                W(1.7, ("boss5",1),("jack",2),("brute",1),("planter",4)),
                Big(1.6, ("boss6",1),("planter",4),("mouse",3),("shooter",2),("ninja",2)) } },
        };

        public const int Count = 13;
        /// <summary>这一章用哪个主题世界（1..6）：两章共用一个世界，终极决战在玩具箱世界。</summary>
        public static int World(int ch) => Get(Math.Max(1, Math.Min(Count, ch)))?.world ?? 1;
        /// <summary>第几个主题的下篇（有章末 Boss）：返回 Boss 编号，没有返回 0。</summary>
        public static int BossNo(int ch) { var c = Get(ch); return c != null && !c.final && ch % 2 == 0 ? ch / 2 : 0; }

        /// <summary>坐姿模式：门的角度收进 ±45°，不用大幅转头（用户设置）。站姿原样。</summary>
        public static double[] PortalAngles(int ch, bool seated)
        {
            var src = Get(ch)?.portals ?? new double[] { 0 };
            if (!seated) return src;
            double max = 0; foreach (var a in src) max = Math.Max(max, Math.Abs(a));
            if (max <= 45) return src;
            var o = new double[src.Length]; for (int i = 0; i < src.Length; i++) o[i] = Math.Round(src[i] * 45 / max);
            return o;
        }

        public static ChapterDef Get(int ch) {
            if (ch < 1 || ch >= All.Length) return null;
            return All[ch];
        }
    }

    public static class REWARD_TEXT
    {
        public static readonly Dictionary<string, RewardText> All = new Dictionary<string, RewardText> {
            {"shield", new RewardText{name="盾牌",en="Shield",how="抬手到脸前，五指张开、掌心朝外",enHow="Raise a hand in front of your face, palm out"}},
            {"binoc", new RewardText{name="望远镜",en="Binoculars",how="空心拳举到眼前，盯住敌人标记它",enHow="Hold a loose fist to your eye and stare at a toy to mark it"}},
            {"dual", new RewardText{name="双枪",en="Dual Wield",how="两只手都比出手指枪，各打各的",enHow="Make a finger gun with each hand"}},
            {"burst", new RewardText{name="泡沫连发枪",en="Foam Burster",how="一挑三连发，专打成群的小怪",enHow="Three shots per flick, great against crowds"}},
            {"cannon", new RewardText{name="软木塞手炮",en="Cork Cannon",how="威力大，打旗子伤害加倍",enHow="Heavy hitter: double damage to flags"}},
            {"final", new RewardText{name="终极决战",en="Final Showdown",how="所有 Boss 轮番登场",enHow="Every boss, one after another"}},
            {"endless", new RewardText{name="无尽模式",en="Endless Mode",how="玩具越来越多，看你能撑几波",enHow="More and more toys. How many waves can you hold?"}},
        };
    }

    public static class EndlessMode
    {
        public static readonly double[] ENDLESS_PORTALS = new double[]{-80,-25,25,80};
        /// <summary>无尽第 n 波（从 1 开始）在哪一关的场景打（用户：无尽模式的场景按 Boss 定）：第 1～5 波 = 第 1 个 Boss 的关（第 2 关），
        /// 第 6～10 波 = 第 2 个 Boss 的关（第 4 关）……六个世界轮一圈再从头来。</summary>
        public static int ChapterFor(int n) => 2 * (((Math.Max(1, n) - 1) / 5) % 6 + 1);

        // 无尽模式小怪池（有序，与 JS 一致）
        private static readonly List<string> POOL = new List<string>{
            "planter","mouse","brute","shooter","plane","jack","tin","ninja","balloon","rcCar","repair"
        };
        private static readonly Dictionary<string, double> POOL_W = new Dictionary<string, double>{
            {"planter",5},{"mouse",3},{"brute",1},{"shooter",2},{"plane",2},
            {"jack",1.5},{"tin",2},{"ninja",1.5},{"balloon",1.5},{"rcCar",1.2},{"repair",0.8}
        };

        // 无尽第 n 波（从 1 开始），rng 是 mulberry32 委托
        public static WaveDef EndlessWave(int n, Func<double> rng)
        {
            // 11-05 用户：无尽模式整体难度提一层，敌人更多、血量更厚；每五波一个 Boss，两个 Boss 之间有一波小怪潮
            int count = Math.Min(60, (int)Math.Round(10 * Math.Pow(1.1, n - 1)));   // 起步 10 只，每波 +10%，封顶 60（第 26 波原来 108 只、一波要打 6 分钟，太拖；后期靠血量和攻击变难）
            bool bossWave = n % 5 == 0, rush = n % 5 == 3;                  // 第 5、10、15… 波 Boss；第 3、8、13… 波小怪潮（数量 ×1.6、间隔更密）
            if (rush) count = (int)Math.Round(count * 1.6);
            var pool = POOL.GetRange(0, Math.Min(POOL.Count, 4 + n));        // 无尽也循序渐进：开局只有基础几种，每波多一种，第 7 波全齐
            // 有序保存 counts
            var counts = new List<(string kind, int cnt)>();
            double tot = 0;
            foreach (var k in pool) tot += POOL_W[k];
            for (int i = 0; i < count; i++) {
                double r = rng() * tot;
                foreach (var k in pool) {
                    r -= POOL_W[k];
                    if (r <= 0) {
                        // 找到了，增加计数
                        int idx = counts.FindIndex(x => x.kind == k);
                        if (idx >= 0) counts[idx] = (k, counts[idx].cnt + 1);
                        else counts.Add((k, 1));
                        break;
                    }
                }
            }
            // Object.entries(counts) 的顺序：按插入顺序
            var spawns = new List<(string, int)>();
            foreach (var (kind, cnt) in counts) spawns.Add((kind, cnt));
            if (n % 4 == 0) spawns.Insert(0, ("commander", 1));
            if (bossWave) spawns.Add(("boss" + (1 + (n / 5 - 1) % 6), 1));     // 1→6 轮流，打完一轮从头再来（血量随波数继续涨）
            return new WaveDef {
                spawns = spawns,
                gap = Math.Max(0.55, (rush ? 0.9 : 1.3) - 0.03 * n),
                // 11-06 用户：无尽小怪血量别厚得离谱。原来按 1.3×1.06^n 指数涨，第 26 波已经 5.6 倍；
                // 改成每波 +4% 线性涨、封顶 2 倍（约第 21 波到顶），后期难度主要靠数量、精英和攻击
                hpMul = Math.Min(2.0, 1.2 + 0.04 * (n - 1)),
                eliteChance = Math.Min(0.08 + 0.03 * n, 0.55),
                big = bossWave || rush,
                rush = rush,
            };
        }
    }
}
