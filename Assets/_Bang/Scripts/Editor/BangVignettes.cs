// 房间四周的主题小场景（组团）：门后世界包住真实房间以后，四周不能只是随机撒道具——
// 每章在房间左、右、左后、右后、门洞两侧各布置一组有主次、有故事、有“有原因的动态”的小场景；
// 组团之间再由 Surround 的随机散件和 FloraRing 地面花草填满。每组整体朝向房间中心。
using System.Collections.Generic;
using Bang.Ambient;
using UnityEngine;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        struct V { public string n; public float x, z, yaw, s; public V(string n, float x, float z, float yaw = 0, float s = 1) { this.n = n; this.x = x; this.z = z; this.yaw = yaw; this.s = s; } }

        // 六个组团中心（门面局部坐标）：左前 / 右前（门墙两侧后方）、左 / 右、左后 / 右后（玩家身后）
        static readonly Vector2[] VigSpots = { new(-7.3f, -3.4f), new(7.5f, -3.6f), new(-7.2f, 1.4f), new(7.3f, 2.2f), new(-4.6f, 8.2f), new(5.0f, 8.0f) };
        // 房间左右两侧再加四组（正左 / 正右、更远的左 / 右）：从房间中央往两边看，中景不能是空草地
        static readonly Vector2[] SideSpots = { new(-6.0f, 4.8f), new(6.2f, 5.2f), new(-9.4f, -0.2f), new(9.5f, 0.4f), new(0.3f, 9.2f) };   // 最后一个：身后正中
        const float VigScale = 1.35f;                                   // 房间四周的组团整体放大（离得近也要有体量）

        static readonly Dictionary<int, (string hero, string[] mid, string[] small, string[] tall)> SideSets = new()
        {
            [1] = ("W1_ClockTower", new[] { "W1_House_Pink", "W1_House_Blue", "W1_House_Yellow", "W1_House_Teal", "W1_House_Purple" }, new[] { "W1_Mushroom_Lamp", "W1_GumdropBushes", "W1_ToyCar_A", "W1_Bench", "W1_ToyCar_B" }, new[] { "W1_FeltTree", "W1_LollipopTree_A", "W1_LollipopTree_B" }),
            [2] = ("W2_Watchtower", new[] { "W2_Tent_Blue", "W2_Tent_Pink", "W2_SandCastle_Big", "W2_DumpTruck" }, new[] { "W2_SupplyCrates", "W2_Bucket", "W2_BeachBall", "W2_SandbagWall", "W2_Campfire" }, new[] { "W2_PalmTree", "W2_PalmTree" }),
            [3] = ("W3_BannerPole", new[] { "W3_Cottage", "W3_Cottage", "W3_Fountain", "W3_Carriage" }, new[] { "W3_Hedge", "W3_TinSoldier_Deco", "W3_FeltPets", "W3_Hedge" }, new[] { "W3_FeltPine", "W3_FeltTree_Lights", "W3_FeltPine" }),
            [4] = ("W4_CircusTent", new[] { "W4_CakeTower", "W4_IceCreamCart", "W4_CircusTent" }, new[] { "W4_GiftBox_A", "W4_GiftBox_B", "W4_PartyHats", "W4_Teddy", "W4_PinataStar" }, new[] { "W4_BalloonBunch", "W4_BalloonBunch" }),
        };

        static void Vignettes(int ch, Transform w, Transform props)
        {
            if (ch >= 5) return;
            MeshCollider col = null;
            foreach (var mf in w.GetComponentsInChildren<MeshFilter>())
                if (mf.name.Contains("_Ground")) { col = mf.gameObject.AddComponent<MeshCollider>(); break; }
            Physics.SyncTransforms();
            _groundRoot = col ? w : null;
            var root = new GameObject("Vignettes").transform; root.SetParent(props, false);
            var roomC = new Vector3(0, 0, 3.3f);

            // 摆一组：局部 (x, z) 以“面朝房间”为 -z（组团正面朝向房间）
            Transform Group(string name, Vector2 c, V[] items)
            {
                var g = new GameObject(name).transform; g.SetParent(root, false);
                var to = roomC - new Vector3(c.x, 0, c.y); to.y = 0;
                var rot = Quaternion.LookRotation(-to.normalized);                       // 组团 +z 背离房间
                g.localPosition = new Vector3(c.x, 0, c.y); g.localRotation = rot;
                foreach (var it in items)
                {
                    var wp = g.localPosition + rot * new Vector3(it.x, 0, it.z) * VigScale;
                    // 脚下必须有地面：组团最外侧的东西会落到地面边缘外（站在半空，用户：悬空）→ 朝房间方向往里挪，挪 1.5 m 还没有地面就不放
                    int tries = 0; var inward = to.normalized;
                    while (!HasGround(wp.x, wp.z) && tries < 10) { wp += inward * 0.15f; tries++; }
                    if (!HasGround(wp.x, wp.z)) continue;
                    var o = Place(it.n, g, Vector3.zero, 0);
                    if (!o) continue;
                    o.transform.SetParent(root, false);
                    o.transform.localPosition = new Vector3(wp.x, GroundY(wp.x, wp.z), wp.z);
                    // 正面朝房间（Place 的 yaw=0 = 正面朝 +z），再加组内偏转
                    o.transform.localRotation = Quaternion.LookRotation(to.normalized) * Quaternion.Euler(0, 90 + it.yaw, 0);
                    o.transform.localScale *= it.s * VigScale;
                    o.transform.SetParent(g, true);
                    SettleOnGround(o);
                }
                return g;
            }
            Vector3 WP(Vector2 c, float x, float z)
            {
                var to = roomC - new Vector3(c.x, 0, c.y); to.y = 0;
                var p = new Vector3(c.x, 0, c.y) + Quaternion.LookRotation(-to.normalized) * new Vector3(x, 0, z) * VigScale;
                p.y = GroundY(p.x, p.z); return p;
            }
            Vector3 TopOf(Transform g, string n, float k = 0.95f)
            {
                foreach (Transform t in g) if (t.name.StartsWith(n)) { var b = Bounds(t.gameObject); return w.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * k, b.center.z)); }
                return Vector3.zero;
            }
            // 沿小椭圆走的东西（小火车、吉普、巡逻锡兵）：高度沿路线烘焙
            TrainLoop Loop(string name, Vector3 center, float a, float b, float speed, float y, string[] cars, float spacing, float scale)
            {
                var loop = new GameObject(name).AddComponent<TrainLoop>(); loop.transform.SetParent(root, false);
                loop.center = new Vector2(center.x, center.z); loop.a = a; loop.b = b; loop.y = y; loop.speed = speed; loop.yawOffset = 90; loop.spacing = spacing;
                var hc = new AnimationCurve();
                for (int i = 0; i <= 48; i++) { float u = i / 48f, ang = u * Mathf.PI * 2; hc.AddKey(new Keyframe(u, GroundY(center.x + Mathf.Cos(ang) * a, center.z + Mathf.Sin(ang) * b))); }
                for (int i = 0; i < hc.length; i++) hc.SmoothTangents(i, 0);
                loop.heightCurve = hc;
                var list = new List<Transform>();
                foreach (var n in cars) { var c = Place(n, loop.transform, Vector3.zero, 0); if (c) { c.transform.localScale *= scale; list.Add(c.transform); } }
                loop.cars = list.ToArray(); loop.Step(0);
                if (y <= 0f) FitLoopToGround(loop);                       // 没有轨道的（巡逻锡兵…）贴地；小火车在轨道上（y = 轨面高）不动
                return loop;
            }

            var S = VigSpots;
            switch (ch)
            {
                case 1:
                {
                    // 左后：小积木村 + 绕村小火车（轨道复用第 1 章的椭圆轨道，缩小）
                    var v = Group("Vig_Village", S[4], new[] { new V("W1_House_Pink", -0.6f, 0.3f, 10), new V("W1_House_Yellow", 0.55f, 0.45f, -15), new V("W1_ClockTower", 0.05f, 1.0f, 0, 0.8f),
                        new V("W1_LollipopTree_A", -1.15f, 0.9f), new V("W1_LollipopTree_B", 1.2f, 1.0f), new V("W1_Mushroom_Lamp", 0.0f, -0.35f), new V("W1_GumdropBushes", -0.9f, -0.2f), new V("W1_GumdropBushes", 0.95f, -0.15f, 40) });
                    var tc = WP(S[4], 0.0f, 0.45f);
                    var trk = Inst(Proc + "/W1_Track.fbx", root, BangArt.WoodVC);
                    if (trk) { var tp = new GameObject("MiniTrack").transform; tp.SetParent(root, false); tp.localPosition = tc; tp.localScale = new Vector3(1.45f, 1, 1.45f); trk.transform.SetParent(tp, false); trk.transform.localPosition = new Vector3(-0.25f, 0, 3.1f); ConformToGround(trk, "W1_MiniTrack"); }   // 轨道顺着山坡贴地（小火车的高度曲线也是按地面烘焙的）
                    Loop("MiniTrain", tc, 1.65f * 1.45f, 1.0f * 1.45f, 0.35f, 0.042f, new[] { "W1_Train_Engine", "W1_Train_Car", "W1_Train_Car" }, 0.42f, 1.3f);   // 轨道和组团一起放大
                    // 左：风车山坡（风车随阵风转）
                    var m = Group("Vig_MillHill", S[2], new[] { new V("W1_HillMound", 0.6f, 0.6f, 20, 1.3f), new V("W1_FeltTree", -0.9f, 0.7f), new V("W1_FeltTree", 1.6f, 0.2f, 0, 0.9f), new V("W1_LollipopTree_A", -0.5f, -0.3f, 0, 0.9f), new V("W1_Bench", 0.3f, -0.55f, 10) });
                    var millPos = WP(S[2], -0.2f, 0.9f);
                    var mill = Place("W1_Windmill_Body", m, m.InverseTransformPoint(w.TransformPoint(millPos)), 0);
                    if (mill)
                    {
                        mill.transform.rotation = Quaternion.LookRotation((roomC - millPos).normalized) * Quaternion.Euler(0, 90, 0);
                        var mb = Bounds(mill);
                        var blades = Place("W1_Windmill_Blades", m, Vector3.zero, 0);
                        if (blades)
                        {
                            blades.transform.rotation = mill.transform.rotation;
                            blades.transform.position = new Vector3(mb.center.x, mb.min.y + mb.size.y * 0.8f, mb.center.z) + mill.transform.rotation * Vector3.left * Mathf.Max(mb.extents.x, mb.extents.z) * 0.85f;
                            var sp = blades.AddComponent<Spin>(); sp.axis = Vector3.right; sp.degPerSec = 26f; sp.gust = 0.45f;
                        }
                    }
                    // 右：游乐角（长椅、小桥、小车）
                    Group("Vig_Park", S[3], new[] { new V("W1_Bridge", 0, 0.2f, 0, 1.4f), new V("W1_Bench", -0.9f, -0.3f, 25), new V("W1_ToyCar_A", 0.8f, -0.4f, -60), new V("W1_ToyCar_B", 1.2f, 0.5f, 120),
                        new V("W1_FeltTree", -1.3f, 0.9f), new V("W1_LollipopTree_B", 1.5f, 1.2f), new V("W1_Mushroom_Lamp", -0.3f, -0.7f), new V("W1_GumdropBushes", 0.4f, 1.0f) });
                    // 右后：房子街
                    Group("Vig_Street", S[5], new[] { new V("W1_House_Blue", -0.9f, 0.4f, 15), new V("W1_House_Teal", 0.3f, 0.7f, 0), new V("W1_House_Purple", 1.4f, 0.4f, -20), new V("W1_FeltTree", -1.8f, 1.0f),
                        new V("W1_Mushroom_Lamp", -0.3f, -0.4f), new V("W1_Mushroom_Lamp", 0.9f, -0.35f), new V("W1_ToyCar_B", 0.3f, -0.6f, 90) });
                    // 门墙两侧：树林
                    Group("Vig_GroveL", S[0], new[] { new V("W1_FeltTree", 0, 0, 0, 1.2f), new V("W1_FeltTree", -1.0f, 0.6f), new V("W1_FeltTree", 0.9f, 0.8f, 0, 0.9f), new V("W1_LollipopTree_A", -0.4f, -0.8f), new V("W1_GumdropBushes", 0.6f, -0.6f), new V("W1_House_Yellow", 1.6f, -0.1f, -30, 0.9f) });
                    Group("Vig_GroveR", S[1], new[] { new V("W1_FeltTree", 0, 0.2f, 0, 1.1f), new V("W1_FeltTree", 1.0f, 0.7f), new V("W1_LollipopTree_B", -0.8f, -0.5f), new V("W1_House_Pink", -1.5f, 0.3f, 30, 0.9f), new V("W1_GumdropBushes", 0.5f, -0.7f, 20) });
                    break;
                }
                case 2:
                {
                    var camp = Group("Vig_Camp2", S[2], new[] { new V("W2_Tent_Blue", -0.6f, 0.4f, 20), new V("W2_Tent_Pink", 0.7f, 0.5f, -20), new V("W2_Campfire", 0, -0.3f), new V("W2_SupplyCrates", -1.3f, -0.1f, 30), new V("W2_SandbagWall", 0.1f, 1.3f), new V("W2_PalmTree", 1.6f, 1.1f) });
                    var fire = TopOf(camp, "W2_Campfire", 0.7f); if (fire != Vector3.zero) Smoke(w, fire);       // 篝火冒烟
                    Group("Vig_Oasis", S[3], new[] { new V("W2_PalmTree", -0.5f, 0.4f, 0, 1.2f), new V("W2_PalmTree", 0.6f, 0.8f, 40), new V("W2_PalmTree", 1.3f, -0.1f, 80, 0.9f), new V("W2_BeachBall", -0.2f, -0.6f), new V("W2_Bucket", 0.6f, -0.5f, 20), new V("W2_SpadeRake", -0.8f, -0.3f, -30), new V("W2_SandCastle_Small", 0.1f, 0.0f) });
                    Group("Vig_Fort", S[4], new[] { new V("W2_SandCastle_Big", 0, 0.5f), new V("W2_SandCastle_Small", -1.1f, 0.1f), new V("W2_SandCastle_Small", 1.1f, 0.2f, 30), new V("W2_Watchtower", 1.7f, 1.0f, -10, 0.9f), new V("W2_Bucket", -0.5f, -0.6f), new V("W2_SpadeRake", 0.4f, -0.7f) });
                    var mp = Group("Vig_Motor", S[5], new[] { new V("W2_DumpTruck", -0.7f, 0.3f, 30), new V("W2_SupplyCrates", 0.9f, 0.6f), new V("W2_SupplyCrates", 1.3f, 0.1f, 40), new V("W2_SandbagWall", -0.2f, 1.2f), new V("W2_PalmTree", -1.6f, 1.0f) });
                    Loop("Vig_JeepLoop", WP(S[5], 0.2f, -0.1f), 0.75f, 0.45f, 0.22f, 0f, new[] { "W2_Jeep" }, 0, 1f);   // 吉普车绕着车场巡逻
                    Group("Vig_DunesL", S[0], new[] { new V("W2_PalmTree", 0, 0, 0, 1.2f), new V("W2_PalmTree", -0.9f, 0.7f, 50), new V("W2_Tent_Blue", 1.0f, 0.4f, -20, 0.9f), new V("W2_BeachBall", 0.3f, -0.7f) });
                    Group("Vig_DunesR", S[1], new[] { new V("W2_PalmTree", 0.2f, 0.1f, 20, 1.1f), new V("W2_SandCastle_Big", -1.0f, 0.4f, 20, 0.8f), new V("W2_Campfire", 0.8f, -0.5f), new V("W2_SupplyCrates", 1.2f, 0.5f, -20) });
                    break;
                }
                case 3:
                {
                    var ham = Group("Vig_Hamlet", S[4], new[] { new V("W3_Cottage", -0.9f, 0.3f, 20), new V("W3_Cottage", 0.4f, 0.7f, 0), new V("W3_Cottage", 1.5f, 0.2f, -25), new V("W3_FeltPine", -1.8f, 0.9f), new V("W3_FeltPine", 2.2f, 1.0f), new V("W3_FeltTree_Lights", -0.2f, -0.4f, 0, 0.8f) });
                    var lc = new List<Vector3>();
                    foreach (Transform t in ham) if (t.name.StartsWith("W3_Cottage")) { var b = Bounds(t.gameObject); lc.Add(w.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.72f, b.center.z))); }
                    if (lc.Count >= 2) { lc.Sort((a, b) => a.x.CompareTo(b.x)); BangFairyLights.Build("Vig_HamletLights", root, lc, 0.12f); }   // 小屋之间拉一串彩灯（黄昏亮着）
                    Group("Vig_Garden", S[2], new[] { new V("W3_Fountain", 0, 0.2f, 0, 1.2f), new V("W3_Hedge", -1.0f, -0.2f, 10), new V("W3_Hedge", 1.0f, -0.1f, -10), new V("W3_Hedge", -0.9f, 0.9f, -20), new V("W3_Hedge", 0.9f, 1.0f, 20),
                        new V("W3_BannerPole", -1.6f, 0.4f), new V("W3_BannerPole", 1.6f, 0.5f), new V("W3_FeltPets", 0.2f, -0.8f) });
                    var road = Group("Vig_Road", S[3], new[] { new V("W3_Carriage", 0.4f, 0.3f, -40), new V("W3_BannerPole", -1.4f, 0.8f), new V("W3_FeltPine", 1.6f, 1.0f), new V("W3_Hedge", -0.6f, -0.6f) });
                    Loop("Vig_SoldierPatrol", WP(S[3], -0.2f, 0.1f), 0.9f, 0.5f, 0.12f, 0f, new[] { "W3_TinSoldier_Deco", "W3_TinSoldier_Deco", "W3_TinSoldier_Deco" }, 0.32f, 1.1f);   // 三个锡兵排队巡逻
                    Group("Vig_Forest", S[5], new[] { new V("W3_FeltPine", 0, 0.3f, 0, 1.3f), new V("W3_FeltPine", -0.9f, 0.8f), new V("W3_FeltPine", 0.9f, 0.9f, 0, 1.1f), new V("W3_FeltTree_Lights", -0.4f, -0.5f), new V("W3_Cottage", 1.6f, 0.0f, -30, 0.85f), new V("W3_FeltPets", 0.5f, -0.7f, 20) });
                    Group("Vig_PinesL", S[0], new[] { new V("W3_FeltPine", 0, 0, 0, 1.2f), new V("W3_FeltPine", -0.9f, 0.6f), new V("W3_FeltTree_Lights", 0.9f, 0.4f), new V("W3_Hedge", 0.2f, -0.7f) });
                    Group("Vig_PinesR", S[1], new[] { new V("W3_FeltPine", 0.1f, 0.1f, 0, 1.1f), new V("W3_FeltPine", 1.0f, 0.7f), new V("W3_Cottage", -1.2f, 0.3f, 30, 0.85f), new V("W3_TinSoldier_Deco", 0.4f, -0.6f), new V("W3_TinSoldier_Deco", 0.7f, -0.55f) });
                    break;
                }
                case 4:
                {
                    var circus = Group("Vig_Circus", S[4], new[] { new V("W4_CircusTent", 0, 0.5f, 0, 1.15f), new V("W4_BalloonBunch", -1.3f, 0.1f), new V("W4_BalloonBunch", 1.4f, 0.2f), new V("W4_GiftBox_A", -0.6f, -0.6f), new V("W4_PinataStar", 0.7f, -0.5f), new V("W4_Teddy", 0.1f, -0.8f, 10) });
                    var picnic = Group("Vig_Picnic", S[2], new[] { new V("W4_CakeTower", 0, 0.3f), new V("W4_GiftBox_A", -0.8f, -0.1f, 20), new V("W4_GiftBox_B", 0.8f, 0, -15), new V("W4_PartyHats", -0.3f, -0.6f), new V("W4_PartyHats", 0.4f, -0.65f, 40), new V("W4_Teddy", 1.3f, 0.6f, -20), new V("W4_BalloonBunch", -1.4f, 0.8f) });
                    Group("Vig_IceCream", S[3], new[] { new V("W4_IceCreamCart", 0, 0.2f, -20), new V("W4_BalloonBunch", 1.1f, 0.6f), new V("W4_PinataStar", -0.9f, 0.4f), new V("W4_GiftBox_B", -0.4f, -0.6f), new V("W4_Teddy", 0.7f, -0.6f, -30) });
                    Group("Vig_Balloons", S[5], new[] { new V("W4_BalloonBunch", 0, 0.3f, 0, 1.25f), new V("W4_BalloonBunch", -0.9f, 0.8f), new V("W4_BalloonBunch", 0.9f, 0.9f, 0, 1.1f), new V("W4_GiftBox_A", -0.3f, -0.5f), new V("W4_GiftBox_B", 0.4f, -0.4f), new V("W4_CircusTent", 1.8f, 0.6f, -30, 0.8f) });
                    Group("Vig_PartyL", S[0], new[] { new V("W4_CircusTent", 0, 0.3f, 0, 0.9f), new V("W4_BalloonBunch", -1.0f, 0), new V("W4_CakeTower", 1.0f, 0.4f, 0, 0.8f), new V("W4_GiftBox_A", 0.2f, -0.7f) });
                    Group("Vig_PartyR", S[1], new[] { new V("W4_BalloonBunch", 0, 0.2f, 0, 1.1f), new V("W4_IceCreamCart", -1.1f, 0.3f, 30), new V("W4_GiftBox_B", 0.8f, -0.4f), new V("W4_PartyHats", 0.2f, -0.7f) });
                    // 马戏团前一串彩旗；气球束随风绕系绳点摆（倒立单摆）
                    var top = TopOf(circus, "W4_CircusTent", 0.9f);
                    var b1 = TopOf(circus, "W4_BalloonBunch", 0.3f);
                    if (top != Vector3.zero && b1 != Vector3.zero) BangFairyLights.Bunting("Vig_CircusBunting", root, new[] { b1, top, TopOf(circus, "W4_PinataStar", 0.9f) }, 0.15f, 0.13f, 0.1f);
                    foreach (var pd in root.GetComponentsInChildren<Transform>())
                        if (pd.name.StartsWith("W4_BalloonBunch") && !pd.GetComponent<Pendulum>()) { var p = pd.gameObject.AddComponent<Pendulum>(); p.length = 1.1f; p.amplitudeDeg = 3.5f; p.twistDeg = 6f; }
                    break;
                }
            }
            // 两侧四组：主景（高大）+ 中件围一圈 + 小件铺前面 + 高树衬在后面
            if (SideSets.TryGetValue(ch, out var ss))
            {
                var rr = new System.Random(4400 + ch);
                for (int k = 0; k < SideSpots.Length; k++)
                {
                    var items = new List<V> { new V(ss.hero, 0, 0.5f, 0, 1.25f) };
                    for (int i = 0; i < 4; i++) { float a = -1.2f + i * 0.8f; items.Add(new V(ss.mid[(i + k) % ss.mid.Length], Mathf.Sin(a) * 1.35f, Mathf.Cos(a) * 0.9f - 0.1f, (float)rr.NextDouble() * 50 - 25, 1.05f)); }
                    for (int i = 0; i < 6; i++) items.Add(new V(ss.small[(i + k * 2) % ss.small.Length], -1.5f + i * 0.6f + (float)rr.NextDouble() * 0.2f, -1.0f - (float)rr.NextDouble() * 0.4f, (float)rr.NextDouble() * 80 - 40));
                    for (int i = 0; i < 5; i++) items.Add(new V(ss.tall[(i + k) % ss.tall.Length], -2.2f + i * 1.1f, 1.6f + (float)rr.NextDouble() * 0.6f, 0, 1.1f + (float)rr.NextDouble() * 0.3f));
                    var g = Group("Vig_Side" + k, SideSpots[k], items.ToArray());
                    if (ch == 2 && k < 2) { var f2 = TopOf(g, "W2_Campfire", 0.7f); if (f2 != Vector3.zero) Smoke(w, f2); }
                    if (ch == 4) foreach (var t in g.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("W4_BalloonBunch") && !t.GetComponent<Pendulum>()) { var p = t.gameObject.AddComponent<Pendulum>(); p.length = 1.1f; p.amplitudeDeg = 3.5f; p.twistDeg = 6f; }
                }
            }
            LodPush(root, 1.7f);
            if (col) Object.DestroyImmediate(col);
        }

        // 巨型玩具地平线：远处一圈比人还高的大玩具（巨型泰迪熊、叠叠圈、橡皮鸭、积木塔……），往任何方向看都被玩具包围，
        // 像人缩小后站在玩具堆中间。每章挑和主题搭的玩具；第 5、6 章是室内，靠墙摆在墙角和身后。
        static readonly Dictionary<int, (string n, float h)[]> GiantSets = new()
        {
            [1] = new[] { ("W4_Teddy", 3.6f), ("W6_StackRings", 3.2f), ("W4_RubberDuck", 2.6f), ("W5_ToyRocket", 3.8f), ("BLOCKS", 4.2f), ("W2_BeachBall", 2.2f), ("W6_ToyTrainCar", 2.0f), ("W1_ClockTower", 4.0f) },
            [2] = new[] { ("W2_Bucket", 2.8f), ("W2_SpadeRake", 2.2f), ("W2_BeachBall", 2.4f), ("W4_Teddy", 3.4f), ("W2_DumpTruck", 2.2f), ("W2_SandCastle_Big", 3.6f), ("W4_RubberDuck", 2.4f), ("BLOCKS", 3.8f) },
            [3] = new[] { ("W3_TinSoldier_Deco", 3.8f), ("W3_FeltPine", 4.4f), ("W4_Teddy", 3.4f), ("W3_TinSoldier_Deco", 3.6f), ("W6_StackRings", 3.0f), ("W3_Tower_Stripe", 4.6f), ("W3_FeltTree_Lights", 4.0f), ("W6_NightLight", 2.6f) },
            [4] = new[] { ("W4_GiftBox_A", 2.6f), ("W4_BalloonBunch", 4.2f), ("W4_CakeTower", 3.4f), ("W4_Teddy", 3.6f), ("W4_GiftBox_B", 2.8f), ("W4_PinataStar", 2.8f), ("W4_RubberDuck", 2.4f), ("W4_PartyHats", 1.8f) },
            [5] = new[] { ("W4_Teddy", 2.0f), ("W5_ToyRocket", 2.4f), ("W5_BigKey", 1.7f), ("W6_StackRings", 1.6f), ("W5_ToyCrane", 2.2f), ("W4_RockingHorse", 1.4f), ("W5_GearMachine", 1.8f), ("W5_WindupBear", 1.3f) },
            [6] = new[] { ("W4_Teddy", 1.4f), ("W6_DuckBeanie", 1.3f), ("W6_StackRings", 1.3f), ("W4_RockingHorse", 1.2f), ("W6_ToyTrainCar", 1.0f), ("W4_RubberDuck", 1.1f), ("W6_NightLight", 1.3f), ("W5_WindupBunny", 1.0f) },   // 箱壁搁板在 1.55 m，大玩具要矮于它
        };

        static void GiantRing(int ch, Transform w, Transform props)
        {
            if (!GiantSets.TryGetValue(ch, out var set)) return;
            MeshCollider col = null;
            foreach (var mf in w.GetComponentsInChildren<MeshFilter>())
                if (mf.name.Contains("_Ground")) { col = mf.gameObject.AddComponent<MeshCollider>(); break; }
            Physics.SyncTransforms();
            _groundRoot = col ? w : null;
            var root = new GameObject("GiantToys").transform; root.SetParent(props, false);
            var roomC = new Vector3(0, 0, 3.3f);
            var rnd = new System.Random(7700 + ch);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            // 位置：室外一圈（环绕天幕中心，半径 9.6–10.6）；室内沿墙（墙角、两侧、身后）
            var spots = new List<Vector3>();
            if (ch <= 4)
            {
                int n = 16;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f) / n * Mathf.PI * 2 + Rf(-0.08f, 0.08f), r = Rf(9.6f, 10.6f);
                    spots.Add(new Vector3(CycloCenter.x + Mathf.Cos(a) * r, 0, CycloCenter.z + Mathf.Sin(a) * r));
                }
            }
            else if (ch == 5)
                foreach (var (x, z) in new[] { (-5.7f, 7.0f), (6.0f, 7.0f), (-5.6f, 3.2f), (5.9f, 3.0f), (-5.7f, -0.6f), (6.0f, -0.8f), (-5.6f, -4.6f), (5.9f, -4.8f) }) spots.Add(new Vector3(x, 0, z));   // 离侧墙搁板远一点
            else
                foreach (var (x, z) in new[] { (-4.75f, 7.5f), (4.75f, 7.5f), (-4.8f, 4.6f), (4.8f, 4.4f), (-4.8f, 0.6f), (4.8f, 0.8f), (-4.7f, -5.9f), (4.7f, -5.9f), (-1.7f, 7.55f), (1.6f, 7.55f) }) spots.Add(new Vector3(x, 0, z));   // 最后两个：身后正中
            for (int i = 0; i < spots.Count; i++)
            {
                var p = spots[i]; p.y = GroundY(p.x, p.z);
                var (n, h) = set[i % set.Length];
                float hh = h * Rf(0.9f, 1.1f) * (ch <= 4 ? 0.62f : 0.9f);   // 用户：身后别太大，整体小一些（室外约 1.4–2.8 m）
                var to = roomC - p; to.y = 0; var face = Quaternion.LookRotation(to.normalized);
                if (n == "BLOCKS")
                {
                    // 巨型字母积木塔：两三块叠起来，每块略转一点（像小孩随手垒的）
                    string[] abc = { "W2_Block_A", "W2_Block_B", "W2_Block_C" };
                    float s = hh / 2f / 0.36f; int lv = hh > 4f ? 3 : 2; s = hh / lv / 0.36f;
                    // 叠成一座的积木算一个整体（去穿模 / 落地整座一起挪，不会抽走底下那块让上面悬空）
                    var tower = new GameObject($"BlockTower_{i:00}").transform; tower.SetParent(root, false);
                    for (int k = 0; k < lv; k++)
                    {
                        var b = Inst($"{Proc}/{abc[(k + i) % 3]}.fbx", tower, BangArt.BlockWood);
                        if (!b) continue;
                        b.transform.localPosition = p + Vector3.up * (0.18f * s + k * 0.36f * s);
                        b.transform.localRotation = face * Quaternion.Euler(0, Rf(-14, 14) + k * 9f, 0);
                        b.transform.localScale = Vector3.one * s;
                    }
                    continue;
                }
                var g = Place(n, root, p, 0);
                if (!g) continue;
                g.transform.localRotation = face * Quaternion.Euler(0, 90 + Rf(-25, 25), 0);
                var bb = Bounds(g);
                if (bb.size.y > 1e-3f) g.transform.localScale *= hh / bb.size.y;
                SettleOnGround(g, 6f);
                if (n == "W4_BalloonBunch") { var pd = g.AddComponent<Pendulum>(); pd.length = 2.5f; pd.amplitudeDeg = 2.5f; pd.twistDeg = 5f; }
            }
            LodPush(root, 2.2f);                                  // 远处的大家伙用低一级精度
            foreach (var r in root.GetComponentsInChildren<Renderer>())          // 离房间中心 9 m 外的不投影（影子落在阴影距离外、也看不清）
                if (Vector3.Distance(w.InverseTransformPoint(r.bounds.center), roomC) > 9f) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (col) Object.DestroyImmediate(col);
        }

        /// <summary>房间四周一整圈的地面花草（Tools/blender_w1_flora.py -- out WnR，分 4 块）。</summary>
        static void FloraRing(int ch, Transform w)
        {
            if (ch >= 5) return;
            // 4 m 网格块：每块一个 LODGroup，屏幕占比低于 0.18（约 9–11 m 外）就不画——那么远的小花小草本来只剩一两个像素
            var ringRoot = new GameObject("FloraRing").transform; ringRoot.SetParent(w, false);
            foreach (var path in System.IO.Directory.GetFiles(Proc, $"W{ch}_FloraRing_*.fbx"))
            {
                var g = Inst(path.Replace('\\', '/'), ringRoot, BangArt.FeltVC);
                if (!g) continue;
                var lg = g.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(0.18f, g.GetComponentsInChildren<Renderer>()) });
                lg.RecalculateBounds();
            }
            Inst($"{Proc}/W{ch}_RoomFlora.fbx", w, BangArt.FeltVC);      // 真实房间地面范围内（地面被玩具化后脚下也有花草）
        }
    }
}
