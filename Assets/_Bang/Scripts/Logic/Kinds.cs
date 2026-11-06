// 小怪属性表（设计文档 v3 第 6 节）。行为由 Game.cs 里按 ai 字段分派。
// role: plant 插旗 / shoot 打你 / support 辅助
using System.Collections.Generic;
namespace Bang.Logic
{
    public class KindDef
    {
        public string name, en;
        public double hp, speed;
        public string ai, role;
        public double plantTime;
        public string flag;
        public double radius, height;
        public int coins;
        public string move;
        // 可选字段（各 AI 用到的）
        public double windup, pause;
        public double fireEvery, telegraph, bulletSpeed;
        public double[] standoff;
        public double circleTime, diveSpeed;
        public double closed, open;
        public double armor;
        public bool hiddenFlag, hidden;
        public int summonEvery, summonFirst, summonN, maxMinions, orderEvery;
        public double healRate, healRange;
        public string splitInto;
        public int splitN;
        public double driveTime, paintEvery, paintAmount, paintLife;
        public double shrinkAt, shrinkSpeed;
        public double aimY = double.NaN; // NaN 表示未设置
    }

    public static class KINDS
    {
        public static readonly KindDef planter = new KindDef {
            name="积木插旗兵", en="Block Planter", hp=2, speed=0.45, ai="planter", role="plant",
            plantTime=3.5, flag="small", radius=0.2, height=0.42, coins=1, move="hop",
        };
        public static readonly KindDef mouse = new KindDef {
            name="发条老鼠", en="Wind-up Mouse", hp=1, speed=1.4, ai="planter", role="plant",
            plantTime=2.2, flag="small", radius=0.16, height=0.18, coins=1, move="zigzag", windup=2, pause=0.5,
        };
        public static readonly KindDef brute = new KindDef {
            name="积木胖墩", en="Block Brute", hp=5, speed=0.25, ai="planter", role="plant",
            plantTime=4, flag="big", radius=0.34, height=0.7, coins=3, move="hop", shrinkAt=3, shrinkSpeed=0.5,
        };
        public static readonly KindDef shooter = new KindDef {
            name="泡沫枪兵", en="Foam Trooper", hp=3, speed=0.6, ai="shooter", role="shoot",
            radius=0.18, height=0.5, coins=2, move="hop", fireEvery=3.5, telegraph=0.6, bulletSpeed=3.0, standoff=new double[]{2.4,3.2},
        };
        public static readonly KindDef plane = new KindDef {
            name="纸飞机", en="Paper Plane", hp=1, speed=1.6, ai="plane", role="shoot",
            radius=0.18, height=0.08, coins=1, move="fly", circleTime=3, diveSpeed=2.8,
        };
        public static readonly KindDef jack = new KindDef {
            name="弹簧小丑盒", en="Jack-in-the-Box", hp=3, speed=0, ai="jack", role="shoot",
            radius=0.2, height=0.5, coins=2, move="static", closed=2.5, open=1.5, bulletSpeed=3,
        };
        public static readonly KindDef tin = new KindDef {
            name="铁皮兵", en="Tin Soldier", hp=4, speed=0.35, ai="planter", role="plant",
            plantTime=3, flag="small", radius=0.2, height=0.6, coins=3, move="slide",
        };
        public static readonly KindDef ninja = new KindDef {
            name="纸板忍者", en="Cardboard Ninja", hp=2, speed=0.7, ai="planter", role="plant",
            plantTime=2.5, flag="small", hiddenFlag=true, radius=0.18, height=0.55, coins=2, move="blink", hidden=true,
        };
        public static readonly KindDef commander = new KindDef {
            name="指挥官", en="Commander", hp=7, speed=0, ai="commander", role="support",
            radius=0.3, height=0.9, coins=5, move="static", summonEvery=10, summonFirst=3, summonN=2, maxMinions=6, orderEvery=6,
        };
        public static readonly KindDef minion = new KindDef {
            name="跟班小锡兵", en="Tin Minion", hp=1, speed=0.8, ai="minion", role="plant",
            plantTime=1.5, flag="small", radius=0.14, height=0.36, coins=1, move="slide", bulletSpeed=3.2,
        };
        public static readonly KindDef balloon = new KindDef {
            name="气球兵", en="Balloon Buddy", hp=2, speed=0.22, ai="balloon", role="plant",
            plantTime=1.5, flag="small", radius=0.2, height=0.85, aimY=0.7, coins=1, move="float", splitInto="balloonSmall", splitN=2,
        };
        public static readonly KindDef balloonSmall = new KindDef {
            name="小气球", en="Mini Balloon", hp=1, speed=0.3, ai="balloon", role="plant",
            plantTime=1.5, flag="small", radius=0.14, height=0.6, aimY=0.5, coins=0, move="float",
        };
        public static readonly KindDef rcCar = new KindDef {
            name="颜料遥控车", en="Paint Racer", hp=2, speed=2.0, ai="rcCar", role="mess",   // 捣乱型：到处洒颜料，不插旗
            radius=0.18, height=0.16, coins=2, move="drive", driveTime=12, paintEvery=0.5, paintAmount=0.5, paintLife=10,
        };
        public static readonly KindDef repair = new KindDef {
            name="修理机器人", en="Fix-it Bot", hp=3, speed=0.6, ai="repair", role="support",
            radius=0.18, height=0.45, coins=2, move="hop", healRate=1, healRange=1.5,
        };

        // 教学专用
        public static readonly KindDef dummy = new KindDef {
            name="积木靶", hp=1, speed=0, ai="dummy", role="none", radius=0.22, height=0.5, coins=0, move="static",
        };
        public static readonly KindDef trainer = new KindDef {
            name="陪练枪兵", hp=2, speed=0, ai="trainer", role="shoot", radius=0.18, height=0.5, coins=0, move="static", fireEvery=2.6, telegraph=0.6, bulletSpeed=2.2,
        };

        // ───── 章末 Boss（Logic/Boss.cs）：fireEvery = 出招间隔，telegraph = 出招前预警（弱点窗口）─────
        static KindDef Boss(string name, string en, double hp, double speed, double radius, double height, string move, double every, double tele) => new KindDef {
            name=name, en=en, hp=System.Math.Round(hp * 2.4), speed=speed, ai="boss", role="boss", radius=radius, height=height, coins=25, move=move, flag="big", fireEvery=every, telegraph=tele };
        public static readonly KindDef boss1 = Boss("积木巨人", "Brick Giant", 34, 0.32, 0.42, 1.25, "stomp", 4.4, 1.0);
        public static readonly KindDef boss2 = Boss("塑料坦克", "Plastic Tank", 44, 0.3, 0.45, 0.85, "drive", 4.2, 1.0);
        public static readonly KindDef boss3 = Boss("锡兵大将军", "Tin Grand General", 42, 0.25, 0.4, 1.35, "slide", 5.2, 1.1);
        public static readonly KindDef boss4 = Boss("气球之王", "Balloon King", 46, 0.3, 0.45, 1.05, "float", 3.4, 0.9);
        public static readonly KindDef boss5 = Boss("发条巨偶", "Wind-up Colossus", 52, 0.3, 0.4, 1.3, "slide", 4.4, 1.6);
        public static readonly KindDef boss6 = Boss("玩具箱之王", "Toybox King", 62, 0.28, 0.48, 1.05, "drive", 3.6, 1.0);

        public static KindDef Get(string kind) {
            switch(kind) {
                case "boss1": return boss1;
                case "boss2": return boss2;
                case "boss3": return boss3;
                case "boss4": return boss4;
                case "boss5": return boss5;
                case "boss6": return boss6;
                case "planter": return planter;
                case "mouse": return mouse;
                case "brute": return brute;
                case "shooter": return shooter;
                case "plane": return plane;
                case "jack": return jack;
                case "tin": return tin;
                case "ninja": return ninja;
                case "commander": return commander;
                case "minion": return minion;
                case "balloon": return balloon;
                case "balloonSmall": return balloonSmall;
                case "rcCar": return rcCar;
                case "repair": return repair;
                case "dummy": return dummy;
                case "trainer": return trainer;
                default: return null;
            }
        }
    }

    public class EliteDef
    {
        public string name;
        public double hpMul = 1, speedMul = 1, coinMul = 1;
        public double armor = -1; // -1 = 不覆盖
        public int copies = 1;
    }

    public static class ELITES
    {
        // 注意：使用有序列表保证插入顺序，与 JS Object.keys(ELITES) 一致
        public static readonly List<string> Keys = new List<string> { "gold", "swift", "swarm" };

        public static readonly EliteDef gold = new EliteDef { name="金身", coinMul=3 };
        public static readonly EliteDef swift = new EliteDef { name="迅捷", speedMul=1.5 };
        public static readonly EliteDef swarm = new EliteDef { name="成群", copies=3 };

        public static EliteDef Get(string key) {
            switch(key) {
                case "gold": return gold;
                case "swift": return swift;
                case "swarm": return swarm;
                default: return null;
            }
        }
    }
}
