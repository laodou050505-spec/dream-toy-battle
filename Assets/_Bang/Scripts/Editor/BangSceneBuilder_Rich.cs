// 门后世界“丰富度”加密：远景山坡一圈小房子和树（缩小、靠 LOD 省开销）、门洞两侧的近景大道具（画框式层次）、
// 中景补一排、各章新增的特色道具、以及会飞的折纸小鸟 / 毛毡蝴蝶。每个 BuildPortalWn 在 PortalWorld 绑定前调用 Enrich。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        public static readonly Dictionary<string, (float h, float gloss, float sheen)> RichSizes = new()
        {
            ["W1_ToyCar_A"] = (0.22f, 0.55f, 0), ["W1_ToyCar_B"] = (0.26f, 0.5f, 0), ["W1_House_Yellow"] = (0.8f, 0.45f, 0), ["W1_House_Blue"] = (1.0f, 0.45f, 0),
            ["W1_Bench"] = (0.22f, 0.45f, 0), ["W1_Bridge"] = (0.28f, 0.45f, 0),
            ["W2_DumpTruck"] = (0.36f, 0.5f, 0), ["W2_PalmTree"] = (1.2f, 0.35f, 0), ["W2_BeachBall"] = (0.24f, 0.7f, 0), ["W2_SpadeRake"] = (0.14f, 0.5f, 0),
            ["W3_Carriage"] = (0.5f, 0.35f, 0), ["W3_Fountain"] = (0.55f, 0.4f, 0), ["W3_BannerPole"] = (1.3f, 0.15f, 0.4f), ["W3_Hedge"] = (0.3f, 0.04f, 0.7f),
            ["W4_IceCreamCart"] = (0.62f, 0.45f, 0), ["W4_Teddy"] = (0.42f, 0.03f, 0.8f), ["W4_PartyHats"] = (0.2f, 0.4f, 0), ["W4_PinataStar"] = (0.45f, 0.05f, 0.6f),
            ["W5_ToyRocket"] = (0.7f, 0.55f, 0), ["W5_ToyCrane"] = (1.1f, 0.45f, 0), ["W5_PaintCans"] = (0.32f, 0.5f, 0),
            ["W6_StackRings"] = (0.55f, 0.65f, 0), ["W6_Xylophone"] = (0.2f, 0.55f, 0), ["W6_ToyTrainCar"] = (0.4f, 0.35f, 0),
        };

        struct Item { public string n; public float x, z, yaw, s; public Item(string n, float x, float z, float yaw = 0, float s = 1) { this.n = n; this.x = x; this.z = z; this.yaw = yaw; this.s = s; } }

        static void Enrich(int ch, Transform w, Transform props)
        {
            // 构建时给地面加临时碰撞体（取高度用），结束后删掉
            MeshCollider col = null;
            foreach (var mf in w.GetComponentsInChildren<MeshFilter>())
                if (mf.name.Contains("_Ground") || mf.name.Contains("_Floor")) { col = mf.gameObject.AddComponent<MeshCollider>(); break; }
            Physics.SyncTransforms();
            _groundRoot = col ? w : null;
            var rich = new GameObject("Rich").transform; rich.SetParent(props, false);
            void Put(IEnumerable<Item> items)
            {
                foreach (var it in items)
                {
                    var g = Place(it.n, rich, new Vector3(it.x, GroundY(it.x, it.z), it.z), it.yaw);
                    if (!g) continue;
                    if (it.s != 1) g.transform.localScale *= it.s;
                    SettleOnGround(g);
                }
            }
            switch (ch)
            {
                case 1:
                    Put(new[]
                    {
                        // 远景：左后小山、右后大山、远处一圈（缩小）
                        new Item("W1_House_Yellow", -3.3f, -6.6f, 25, 0.75f), new Item("W1_House_Blue", -2.2f, -7.4f, 10, 0.7f), new Item("W1_House_Pink", -0.9f, -7.9f, 0, 0.65f),
                        new Item("W1_House_Teal", 0.6f, -8.1f, -5, 0.6f), new Item("W1_House_Purple", 2.4f, -7.6f, -15, 0.7f), new Item("W1_House_Yellow", 3.9f, -6.9f, -30, 0.7f),
                        new Item("W1_FeltTree", -4.2f, -5.6f, 0, 0.8f), new Item("W1_FeltTree", -1.5f, -8.4f, 0, 0.7f), new Item("W1_FeltTree", 1.4f, -8.6f, 0, 0.7f), new Item("W1_FeltTree", 4.6f, -5.4f, 0, 0.85f),
                        new Item("W1_LollipopTree_A", -2.9f, -7.6f, 0, 0.7f), new Item("W1_LollipopTree_B", 3.3f, -7.9f, 0, 0.7f), new Item("W1_LollipopTree_B", -0.2f, -8.6f, 0, 0.6f),
                        new Item("W1_GumdropBushes", -3.6f, -6.0f, 0, 0.9f), new Item("W1_GumdropBushes", 3.0f, -6.8f, 40, 0.9f), new Item("W1_GumdropBushes", -1.2f, -7.6f, 10, 0.8f),
                        // 中景：轨道外再补一排房子 + 停着的小车 + 长椅
                        new Item("W1_House_Blue", -2.15f, -4.2f, 30), new Item("W1_House_Yellow", 2.35f, -3.95f, -35),
                        new Item("W1_ToyCar_A", -1.05f, -1.95f, 65), new Item("W1_ToyCar_B", 2.1f, -2.35f, -110), new Item("W1_Bench", 0.75f, -1.2f, -20),
                        new Item("W1_Mushroom_Lamp", -1.35f, -2.75f, 0), new Item("W1_Mushroom_Lamp", 1.95f, -3.0f, 0),
                        // 近景：门洞两侧的大树和棒棒糖树（画框层次）
                        new Item("W1_FeltTree", -1.45f, -0.75f, 0, 1.15f), new Item("W1_LollipopTree_A", 1.35f, -0.7f, 0, 1.1f), new Item("W1_GumdropBushes", 1.0f, -0.55f, 20, 1.1f),
                    });
                    BangCritters.Spawn(w, false, 5, new Vector3(0.3f, 0, -4.4f), new Vector2(3.2f, 2.0f), new Vector2(2.3f, 3.3f), 101);
                    BangCritters.Spawn(w, true, 6, new Vector3(0.2f, 0, -2.2f), new Vector2(1.6f, 1.0f), new Vector2(0.5f, 1.2f), 102);
                    break;
                case 2:
                    Put(new[]
                    {
                        new Item("W2_PalmTree", -2.7f, -2.9f, 0), new Item("W2_PalmTree", 2.9f, -4.4f, 40), new Item("W2_PalmTree", -3.2f, -5.9f, 70, 0.8f), new Item("W2_PalmTree", 1.2f, -6.6f, 10, 0.75f),
                        new Item("W2_DumpTruck", 1.35f, -1.6f, -60), new Item("W2_BeachBall", -0.6f, -1.15f, 0), new Item("W2_BeachBall", 2.2f, -3.6f, 0, 0.8f), new Item("W2_SpadeRake", 0.35f, -1.0f, 30),
                        new Item("W2_Tent_Blue", -2.3f, -4.6f, 35, 0.85f), new Item("W2_Tent_Pink", 2.6f, -5.6f, -30, 0.8f), new Item("W2_SandCastle_Small", -1.9f, -6.4f, 0, 0.9f),
                        new Item("W2_SandCastle_Big", 3.6f, -6.6f, -30, 0.75f), new Item("W2_SupplyCrates", -2.6f, -1.4f, 50), new Item("W2_Campfire", 1.6f, -5.0f, 0, 0.8f),
                        new Item("W2_Bucket", -1.35f, -0.8f, 20, 1.1f),
                    });
                    BangCritters.Spawn(w, false, 5, new Vector3(0.3f, 0, -4.5f), new Vector2(3.0f, 2.0f), new Vector2(2.4f, 3.4f), 201);
                    break;
                case 3:
                    Put(new[]
                    {
                        new Item("W3_Carriage", 1.3f, -2.2f, -50), new Item("W3_Fountain", -1.3f, -2.0f, 0), new Item("W3_BannerPole", -1.75f, -3.1f, 0), new Item("W3_BannerPole", 2.25f, -3.1f, 0),
                        new Item("W3_Hedge", -0.85f, -1.3f, 10), new Item("W3_Hedge", 1.5f, -1.4f, -10), new Item("W3_Hedge", -2.4f, -2.6f, 60), new Item("W3_Hedge", 2.9f, -2.9f, -60),
                        new Item("W3_FeltPine", -1.4f, -0.8f, 0, 1.1f), new Item("W3_FeltTree_Lights", 1.35f, -0.75f, 0, 1.05f),
                        new Item("W3_Cottage", -3.6f, -4.0f, 40, 0.85f), new Item("W3_Cottage", 3.7f, -4.2f, -40, 0.8f), new Item("W3_Cottage", -1.2f, -7.2f, 10, 0.7f), new Item("W3_Cottage", 2.1f, -7.0f, -15, 0.7f),
                        new Item("W3_FeltPine", -3.9f, -6.0f, 0, 0.8f), new Item("W3_FeltPine", 3.9f, -6.2f, 0, 0.85f), new Item("W3_FeltPine", -0.1f, -7.8f, 0, 0.75f), new Item("W3_FeltPine", 1.2f, -8.0f, 0, 0.7f),
                        new Item("W3_TinSoldier_Deco", -0.25f, -2.55f, 0), new Item("W3_TinSoldier_Deco", 0.85f, -2.55f, 0),
                    });
                    BangCritters.Spawn(w, true, 6, new Vector3(0.3f, 0, -2.3f), new Vector2(1.8f, 1.0f), new Vector2(0.5f, 1.3f), 301);
                    break;
                case 4:
                    Put(new[]
                    {
                        new Item("W4_IceCreamCart", -1.0f, -2.9f, 40), new Item("W4_Teddy", 0.95f, -1.5f, -30), new Item("W4_Teddy", -2.3f, -4.4f, 30, 0.9f),
                        new Item("W4_PartyHats", -0.3f, -1.15f, 0), new Item("W4_PartyHats", 1.6f, -2.8f, 40), new Item("W4_PinataStar", 0.9f, -4.6f, 0),
                        new Item("W4_BalloonBunch", -1.4f, -0.8f, 0, 1.1f), new Item("W4_BalloonBunch", 1.45f, -0.85f, 0, 1.05f),
                        new Item("W4_GiftBox_A", -3.0f, -5.4f, 20, 1.3f), new Item("W4_GiftBox_B", 3.2f, -5.6f, -20, 1.3f), new Item("W4_CircusTent", 3.3f, -6.6f, -30, 0.7f),
                        new Item("W4_CakeTower", -3.4f, -6.8f, 20, 0.6f), new Item("W4_BalloonBunch", 0.2f, -7.4f, 0, 0.9f),
                    });
                    BangCritters.Spawn(w, true, 7, new Vector3(0.3f, 0, -2.6f), new Vector2(2.0f, 1.2f), new Vector2(0.6f, 1.5f), 401);
                    break;
                case 5:
                    Put(new[]
                    {
                        new Item("W5_ToyRocket", 1.55f, -4.0f, -20, 1.15f), new Item("W5_ToyCrane", -2.9f, -3.3f, 40), new Item("W5_PaintCans", 0.75f, -1.75f, 10), new Item("W5_PaintCans", -2.2f, -1.2f, -30),
                        new Item("W5_GearMachine", 3.0f, -5.4f, -35, 0.8f), new Item("W5_Lamp", -0.6f, -5.0f, 0), new Item("W5_Lamp", 1.4f, -5.1f, 0),
                    });
                    // 地板上绕小圈走的发条玩具（工坊里的“活气”）
                    foreach (var (n, c, ab, ph) in new[] { ("W5_WindupBunny", new Vector2(-0.1f, -2.0f), new Vector2(0.45f, 0.28f), 0.1f), ("W5_WindupBear", new Vector2(-0.6f, -4.3f), new Vector2(0.5f, 0.3f), 0.6f) })
                    {
                        var loop = new GameObject("Walker_" + n).AddComponent<Bang.Ambient.TrainLoop>();
                        loop.transform.SetParent(rich, false);
                        loop.center = c; loop.a = ab.x; loop.b = ab.y; loop.y = 0; loop.speed = 0.1f; loop.startPhase = ph; loop.yawOffset = 90; loop.spacing = 0;
                        var g = Place(n, loop.transform, Vector3.zero, 0);
                        if (g) { g.transform.localScale *= 1.6f; loop.cars = new[] { g.transform }; loop.Step(0); }
                    }
                    break;
                case 6:
                    Put(new[]
                    {
                        new Item("W6_StackRings", -0.95f, -2.9f, 0), new Item("W6_StackRings", 2.6f, -2.2f, 0, 0.8f), new Item("W6_Xylophone", 1.35f, -3.4f, 20, 1.3f),
                        new Item("W6_ToyTrainCar", -1.6f, -1.8f, 60), new Item("W6_GummyBear", 1.25f, -2.8f, -30, 1.6f), new Item("W6_GummyBear", -2.6f, -2.4f, 30, 1.4f),
                        new Item("W6_WrappedCandy", 1.9f, -1.3f, 40, 2.2f), new Item("W6_WrappedCandy", -0.6f, -1.25f, -20, 2.0f), new Item("W6_YarnBall", 2.4f, -3.6f, 0, 1.2f),
                    });
                    BangCritters.Spawn(w, true, 5, new Vector3(0.3f, 0, -2.8f), new Vector2(2.0f, 1.2f), new Vector2(0.8f, 1.8f), 601);
                    break;
            }
            if (col) Object.DestroyImmediate(col);
        }
    }
}
