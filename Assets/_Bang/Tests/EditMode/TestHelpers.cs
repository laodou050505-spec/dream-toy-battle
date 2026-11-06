// 测试工具：合成手部关节、内存存档、驱动游戏的假输入。
// 精确移植 test/helpers.js。
using System;
using System.Collections.Generic;
using Bang.Logic;

namespace Bang.Logic.Tests
{
    // ────────── makeHand ──────────
    public static class TestHelpers
    {
        public static readonly Head HEAD = new Head { pos = new Vec3d(0, 1.6, 0), fwd = new Vec3d(0, 0, -1) };
        // 右手向前伸直的手腕位置（臂长 0.62）
        public static readonly Vec3d RIGHT_STRAIGHT = new Vec3d(0.17, 1.38, -0.58);
        public static readonly Vec3d RIGHT_BENT = new Vec3d(0.2, 1.15, -0.25);

        // 生成一只手的关节。f=手指朝向，n=掌心朝向，pose: gun / fist / hollow / open / pinch / relaxed
        public static Joints MakeHand(string side, string pose, Vec3d W, Vec3d f, Vec3d n)
        {
            f = V.norm(f);
            n = V.norm(n);
            // 拇指一侧（叉积方向与 JS 一致）
            Vec3d t = side == "right" ? V.cross(f, n) : V.cross(n, f);

            // P(base, df, dt, dn)：在 base 沿 f/t/n 方向偏移
            Vec3d Pt(Vec3d @base, double df, double dt, double dn)
                => V.add(@base, V.add(V.scale(f, df), V.add(V.scale(t, dt), V.scale(n, dn))));

            var prox = new Dictionary<string, Vec3d> {
                {"index",  Pt(W,  0.09,   0.025, 0)},
                {"middle", Pt(W,  0.095,  0.005, 0)},
                {"ring",   Pt(W,  0.09,  -0.015, 0)},
                {"pinky",  Pt(W,  0.08,  -0.035, 0)},
            };

            Vec3d ext(Vec3d p)    => Pt(p,  0.085,  0,      0);
            Vec3d curl(Vec3d p)   => Pt(p, -0.01,   0,      0.03);
            Vec3d hollow(Vec3d p) => Pt(p,  0.012,  0,      0.034);
            Vec3d semi(Vec3d p)   => Pt(p,  0.04,  -0.005,  0.03);

            var shapes = new Dictionary<string, Dictionary<string, Func<Vec3d, Vec3d>>> {
                {"gun",     new Dictionary<string,Func<Vec3d,Vec3d>>{{"index",ext},   {"middle",ext},    {"ring",curl},   {"pinky",curl}}},
                {"fist",    new Dictionary<string,Func<Vec3d,Vec3d>>{{"index",curl},  {"middle",curl},   {"ring",curl},   {"pinky",curl}}},
                {"hollow",  new Dictionary<string,Func<Vec3d,Vec3d>>{{"index",hollow},{"middle",hollow}, {"ring",hollow}, {"pinky",hollow}}},
                {"open",    new Dictionary<string,Func<Vec3d,Vec3d>>{{"index",ext},   {"middle",ext},    {"ring",ext},    {"pinky",ext}}},
                {"pinch",   new Dictionary<string,Func<Vec3d,Vec3d>>{{"index",semi},  {"middle",ext},    {"ring",ext},    {"pinky",ext}}},
                {"relaxed", new Dictionary<string,Func<Vec3d,Vec3d>>{{"index",semi},  {"middle",semi},   {"ring",semi},   {"pinky",semi}}},
            };
            var shape = shapes[pose];

            var tip = new Dictionary<string, Vec3d>();
            foreach (var k in new[]{"index","middle","ring","pinky"})
                tip[k] = shape[k](prox[k]);

            Vec3d thumbTip = Pt(W, 0.05, 0.06, -0.03);
            if (pose == "pinch") thumbTip = V.clone(tip["index"]);
            if (pose == "fist")  thumbTip = Pt(W, 0.07, 0.0, 0.05);

            return new Joints {
                wrist       = V.clone(W),
                thumbTip    = thumbTip,
                indexTip    = tip["index"],  indexProx  = prox["index"],
                middleTip   = tip["middle"], middleProx = prox["middle"],
                ringTip     = tip["ring"],   ringProx   = prox["ring"],
                pinkyTip    = tip["pinky"],  pinkyProx  = prox["pinky"],
            };
        }

        // 内存存档（模拟 localStorage）
        public class MemStore : ISaveStore
        {
            private SaveData _data;
            public MemStore(SaveData init = null) { _data = init; }
            public SaveData Load() => _data;
            public void Save(SaveData d)
            {
                // 简单深拷贝（序列化语义），此处直接保存引用——测试中足够用
                _data = d;
            }
            public SaveData Peek() => _data;
        }

        public static MemStore NewMemStore(SaveData init = null) => new MemStore(init);

        // 让游戏跑若干秒，handsFn(i) → Hands；dt 默认 1/30s
        public static void Run(Game game, double sec, Func<int, Hands> handsFn = null, double dt = 1.0/30, Head head = null)
        {
            if (head == null) head = HEAD;
            int n = (int)Math.Round(sec / dt);
            for (int i = 0; i < n; i++) {
                var hands = handsFn?.Invoke(i) ?? new Hands();
                var gaze = game.GazeOverride ?? new GazeRay { origin = head.pos, dir = head.fwd };
                game.Update(dt, new FrameInput { head = head, gaze = gaze, hands = hands });
            }
        }

        // 直接对准某个点开一枪（绕过手势识别）
        public static void FireAt(Game game, Vec3d pos, string side = "right", Vec3d aimPosOverride = null, Vec3d originOverride = null, bool gazeAtTarget = true)
        {
            Vec3d origin = originOverride ?? new Vec3d(0.2, 1.35, -0.6);
            Vec3d dir = V.norm(V.sub(aimPosOverride ?? pos, origin));
            if (gazeAtTarget) game.GazeOverride = new GazeRay { origin = HEAD.pos, dir = V.norm(V.sub(pos, HEAD.pos)) };

            // 先让视线锁定（14 帧）
            var gunHands = MakeGunHands(side);
            for (int i = 0; i < 14; i++) {
                game.Update(1.0/60, new FrameInput { head = HEAD, gaze = game.GazeOverride, hands = gunHands });
            }
            // 开一枪
            var fireEvent = new FireEvent { side = side, origin = V.clone(origin), dir = V.clone(dir) };
            var fireHands = new Hands();
            var ho = new HandOut { state = "gun", events = new List<HandEvent> { fireEvent } };
            if (side == "right") fireHands.right = ho; else fireHands.left = ho;
            game.Update(1.0/60, new FrameInput { head = HEAD, gaze = game.GazeOverride, hands = fireHands });
        }

        private static Hands MakeGunHands(string side)
        {
            var ho = new HandOut { state = "gun", events = new List<HandEvent>() };
            var h = new Hands();
            if (side == "right") h.right = ho; else h.left = ho;
            return h;
        }

        public static Vec3d CenterOf(Enemy e) => new Vec3d(e.pos.x, e.pos.y + e.height * 0.5, e.pos.z);

        // 在玩具世界深处放一个打不到的靶，让这一波不结束
        public static Enemy HoldWave(Game game)
        {
            var p = game.portals[0];
            var e = game.SpawnEnemy("dummy", new SpawnOpts {
                pos = new Vec3d(p.far.x * 3, 0, p.far.z * 3),
                portal = p,
                eliteExplicit = true, eliteOverride = null,
            });
            e.hp = e.maxHp = 9999;
            return e;
        }

        // 创建已进入第 ch 章波次的游戏（跳过教学）
        public static Game WaveGame(int ch = 1, Dictionary<string,bool> unlockExtra = null, ISaveStore store = null)
        {
            var g = new Game(new GameOpts { store = store ?? NewMemStore(), seed = 7 });
            if (unlockExtra != null) foreach (var kv in unlockExtra) g.profile.unlocked[kv.Key] = kv.Value;
            g.StartChapter(ch, new StartChapterOpts { skipLesson = true });
            return g;
        }

        // 构造一个只有 state 没有事件的 HandOut
        public static HandOut MakeStateHand(string state) => new HandOut { state = state, events = new List<HandEvent>() };

        // 构造包含单个状态切换事件的 HandOut
        public static HandOut MakeStateEventHand(string state, string from, string to) => new HandOut {
            state = state,
            events = new List<HandEvent> { new StateEvent { from = from, to = to } },
        };
    }
}
