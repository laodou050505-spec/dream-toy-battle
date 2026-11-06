// 一键搭建主场景：XR 装置 + 灯光后期 + 编辑器预览客厅 + 第 1 章传送门与门后世界「积木小镇」。
// 坐标约定（与规则层一致）：玩家站在原点面朝 -Z；传送门门面在 z = -3.7，门后世界在门的 -Z 一侧。
using System.Collections.Generic;
using System.IO;
using Bang.Ambient;
using Bang.Portal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        public const string ScenePath = "Assets/_Bang/Scenes/Main.unity";
        const string Proc = "Assets/_Bang/Art/Models/Proc";
        public const float PortalDist = 3.7f;   // 用户：门离远一点，让敌人多走一会（原 3.2）

        // P2 资产的目标高度（米）、光泽、绒布感（各章共用一张表，W2 的在 BangSceneBuilder_W2.cs 里补进来）
        public static readonly Dictionary<string, (float h, float gloss, float sheen)> Sizes = new()
        {
            ["W1_House_Pink"] = (0.85f, 0.45f, 0), ["W1_House_Purple"] = (0.8f, 0.45f, 0), ["W1_House_Teal"] = (1.05f, 0.45f, 0),
            ["W1_ClockTower"] = (1.85f, 0.45f, 0), ["W1_Windmill_Body"] = (1.35f, 0.3f, 0), ["W1_Windmill_Blades"] = (0.95f, 0.25f, 0),
            ["W1_Catapult_Base"] = (0.7f, 0.25f, 0), ["W1_Catapult_Arm"] = (0.2f, 0.25f, 0),      // 投石车是门后机关：要比小怪大、从门洞里一眼看得到
            ["W1_Train_Engine"] = (0.26f, 0.35f, 0), ["W1_Train_Car"] = (0.2f, 0.35f, 0),
            ["W1_LollipopTree_A"] = (0.85f, 0.6f, 0), ["W1_LollipopTree_B"] = (0.75f, 0.6f, 0),
            ["W1_FeltTree"] = (1.0f, 0.05f, 0.7f), ["W1_GumdropBushes"] = (0.3f, 0.05f, 0.7f),
            ["W1_Cloud_A"] = (0.9f, 0.3f, 0.4f), ["W1_Cloud_B"] = (0.7f, 0.3f, 0.4f),
            ["W1_HillMound"] = (0.55f, 0.05f, 0.7f), ["W1_Mushroom_Lamp"] = (0.38f, 0.5f, 0),
        };

        /// <summary>主场景里预览哪一章的门（玩法阶段改成按章节动态生成）。</summary>
        public static int PreviewChapter = 1;
        /// <summary>预览房间玩具化道具（只在编辑器截图时用）。</summary>
        public static bool PreviewRoomToys = false;

        /// <summary>六章各存一个预览场景 Assets/_Bang/Scenes/Preview_W1..W6.unity（主场景保持第 1 章）。</summary>
        [MenuItem("Bang/3 生成六章预览场景")]
        public static void BuildAllPreviews()
        {
            int keep = PreviewChapter;
            for (int ch = 1; ch <= 6; ch++)
            {
                PreviewChapter = ch;
                Build($"Assets/_Bang/Scenes/Preview_W{ch}.unity");
            }
            PreviewChapter = keep;
            Build();
        }

        [MenuItem("Bang/1 搭建主场景")]
        public static void Build() => Build(ScenePath);

        public static void Build(string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ResetMeshNames();
            var world = new GameObject("BangWorld");
            BuildLighting(world.transform);
            world.AddComponent<ToyLook>();                 // 全局画面调色（边缘光 / 环境光 / 饱和度 / 亮度）
            var cam = BuildXR(world.transform);
            BuildPreviewRoom(world.transform);
            // 主场景不带门后世界：每章的世界单独存成 World_Wn 场景，开章时按需加载（六章都进安装包，同一时间只占一章的内存）
            if (scenePath != ScenePath) BuildChapterPortal(world.transform, PreviewChapter);
            var toy = new GameObject("RoomToyification").AddComponent<RoomToyification>();
            toy.transform.SetParent(world.transform, false);
            var doorAnchor = new GameObject("ToyDoorAnchor").transform;                    // 玩具化从门洞开始蔓延
            doorAnchor.SetParent(world.transform, false); doorAnchor.localPosition = new Vector3(0, 0.95f, -PortalDist);
            toy.door = doorAnchor;
            if (PreviewRoomToys)
            {
                BangRoomToys.Preview(world.transform);
                toy.previewDoorRadius = 2.6f;                                                      // 从门口蔓延出的一大片
                toy.previewSeeds = new[]
                {
                    new RoomToyification.PreviewSeed { position = new Vector3(1.9f, 0.0f, -0.6f), radius = 0.7f },    // 地上一面旗子（还没连上门口那片）
                    new RoomToyification.PreviewSeed { position = new Vector3(-3.95f, 1.5f, -1.4f), radius = 0.45f }, // 左墙刚开始
                };
                toy.Apply();
            }
            if (scenePath == ScenePath) BangPlaySetup.Build(world.transform, cam);   // 玩法装置只挂在主场景
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
            if (scenePath == ScenePath) SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Bang] 场景搭建完成 → " + scenePath);
        }

        public static string WorldScenePath(int ch) => $"Assets/_Bang/Scenes/World_W{ch}.unity";

        /// <summary>某章的门 + 门后世界（门在正前方 0°；多门章节运行时按逻辑门的角度复制门框）+ 去穿模 + 登记门后材质。</summary>
        static GameObject BuildChapterPortal(Transform root, int ch)
        {
            int keep = PreviewChapter; PreviewChapter = ch; _missY = 0;
            var pos = new Vector3(0, 0, -PortalDist);
            GameObject portalGo;
            switch (ch)
            {
                case 2: portalGo = BuildPortalW2(root, pos, 1); break;
                case 3: portalGo = BuildPortalW3(root, pos, 1); break;
                case 4: portalGo = BuildPortalW4(root, pos, 1); break;
                case 5: portalGo = BuildPortalW5(root, pos, 1); break;
                case 6: portalGo = BuildPortalW6(root, pos, 1); break;
                default: portalGo = BuildPortalW1(root, pos, 1); break;
            }
            // 最后一道：统一去穿模（各套摆放逻辑互相不知道对方；通道按该章每扇门清），再重新登记门后世界的材质
            UnityEngine.Random.InitState(4100 + ch);
            Declutter(portalGo.transform, ch);
            foreach (var pw in portalGo.GetComponentsInChildren<PortalWorld>(true)) pw.Rebind();
            PreviewChapter = keep;
            return portalGo;
        }

        /// <summary>六章世界场景（打包用）：只有门 + 门后世界，运行时叠加加载到主场景。</summary>
        [MenuItem("Bang/4 生成六章世界场景（打包用）")]
        public static void BuildWorldScenes()
        {
            for (int ch = 1; ch <= 6; ch++)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ResetMeshNames();
                var root = new GameObject($"ChapterWorld_W{ch}");
                BuildChapterPortal(root.transform, ch);
                AddGroundCols(root.transform);
                EditorSceneManager.SaveScene(scene, WorldScenePath(ch));
                Debug.Log("[Bang] 世界场景 → " + WorldScenePath(ch));
            }
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);     // 门外地皮按门里地面重新取 UV / 顶点色（它自己逐个开世界场景）
            Debug.Log("[Bang] 门外地皮：\n" + BangSpillMatch.Run());
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>能走的地面件（地形、地板、小路、轨道、地毯…）挂一个隐形碰撞体子物体（Ignore Raycast 层）：小怪脚底贴地取样用。
        /// 地形叫 WorldGroundCol（门后），其余叫 GroundCol（房间里只认这个）。原来是重建后手动跑的脚本，重建一次就丢。</summary>
        static void AddGroundCols(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                string nm = r.name;
                bool walk = nm.Contains("BrickPath") || nm.Contains("Track") || nm.Contains("CloudRug") || nm.Contains("Ground") || nm.Contains("Floor") || nm.Contains("Terrain") || nm.Contains("Road") || nm.Contains("Hill") || nm.Contains("Rug");
                if (!walk || nm.EndsWith("GroundCol") || r.GetComponentInParent<LODGroup>() || r.bounds.max.y < 0.015f) continue;
                var g = new GameObject(nm.EndsWith("_Ground") || nm.Contains("Terrain") || nm.Contains("Hill") ? "WorldGroundCol" : "GroundCol");
                g.layer = 2; g.transform.SetParent(r.transform, false);
                g.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }

        /// <summary>打包场景：主场景 + 六章世界场景；世界场景缺任何一个都直接报错（不能打出一个门后空空的包）。</summary>
        public static string[] BuildScenePaths()
        {
            var list = new List<string> { ScenePath };
            for (int ch = 1; ch <= 6; ch++)
            {
                if (!File.Exists(WorldScenePath(ch))) throw new System.Exception("缺世界场景 " + WorldScenePath(ch) + "，先跑 Bang/4 生成六章世界场景");
                list.Add(WorldScenePath(ch));
            }
            return list.ToArray();
        }

        static void SetBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            for (int ch = 1; ch <= 6; ch++) if (File.Exists(WorldScenePath(ch))) list.Add(new EditorBuildSettingsScene(WorldScenePath(ch), true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ───────── 灯光与后期：温暖柔和、略带粉彩，泛光克制 ─────────
        static void BuildLighting(Transform root)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(48, 150, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.64f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.68f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.52f, 0.46f);
            RenderSettings.fog = false;

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.transform.SetParent(root, false);
            vol.isGlobal = true;
            var path = "Assets/_Bang/Art/Materials/PP_Dream.asset";
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!prof)
            {
                prof = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(prof, path);
                var bloom = prof.Add<Bloom>(true); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.25f); bloom.scatter.Override(0.65f); bloom.tint.Override(new Color(1f, 0.92f, 0.96f));
                var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
                var ca = prof.Add<ColorAdjustments>(true); ca.saturation.Override(0f); ca.postExposure.Override(0f); ca.contrast.Override(10f);
                var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.12f); vg.color.Override(new Color(0.45f, 0.3f, 0.5f));
                EditorUtility.SetDirty(prof);
            }
            vol.sharedProfile = prof;
        }

        // ───────── XR 装置：原点在地面，相机由头显驱动 ─────────
        static Camera BuildXR(Transform root)
        {
            var origin = new GameObject("XR Origin").transform;
            origin.SetParent(root, false);
            var camGO = new GameObject("XR Camera");
            camGO.transform.SetParent(origin, false);
            camGO.transform.localPosition = new Vector3(0, 1.6f, 0);
            camGO.transform.localRotation = Quaternion.Euler(4, 180, 0);    // 玩家面朝 -Z
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.93f, 0.9f, 0.96f, 1);
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 80; cam.fieldOfView = 70;
            var data = camGO.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;   // MR 透视下后处理会破坏 alpha 且耗性能，真机不用后处理，画面靠材质与光照
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();   // 耳朵在头上：没有它整个游戏没有任何声音（用户真机反馈“游戏没有声音”）
            var tpd = camGO.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(new InputAction("HeadPos", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("HeadRot", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            var pxr = origin.gameObject.AddComponent<ByteDance.PICO.XR.PXR_Manager>();
            pxr.eyeTracking = true;
            var pc = ByteDance.PICO.XR.PXR_ProjectSetting.GetProjectConfig();
            if (pc != null) { pc.handTracking = true; pc.eyeTracking = true; pc.videoSeeThrough = true; EditorUtility.SetDirty(pc); }
            origin.gameObject.AddComponent<MRBootstrap>().cam = cam;
            return cam;
        }

        // ───────── 编辑器预览客厅（MR 里是真实房间；带 EditorOnly 标签，打包时自动剔除）─────────
        static void BuildPreviewRoom(Transform root)
        {
            var room = new GameObject("PreviewRoom (EditorOnly)");
            room.tag = "EditorOnly";
            room.transform.SetParent(root, false);
            // 预览客厅只在模板值为 0 的地方绘制（门洞里写了模板值），否则会挡住门后世界
            // 客厅表面全部用 Bang/ToyCover：未覆盖处显示原色，被玩具化种子覆盖的地方化开成玩具化全景天空（MR 里未覆盖处透明露出真实房间）
            var pano = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/ToySky_Pano.png");
            Material M(string n, Color c, float smooth) => BangArt.Mat("M_RoomCover_" + n, m =>
            {
                m.SetColor("_BaseColor", c); m.SetFloat("_RoomMode", 1);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendA", (float)BlendMode.One); m.SetFloat("_DstBlendA", (float)BlendMode.Zero);
                m.renderQueue = 2050;                                      // 在外部玩具世界（2000）之后画，覆盖处淡出露出后面的 3D 模型
            }, Shader.Find("Bang/ToyCover"));
            var mask = BangArt.Mat("M_RoomCoverMask", m => m.renderQueue = 1980, Shader.Find("Bang/ToyCoverMask"));
            void Box(string n, Vector3 c, Vector3 s, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; g.transform.SetParent(room.transform, false);
                g.transform.localPosition = c; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterials = new[] { m, mask };
                Object.DestroyImmediate(g.GetComponent<Collider>());
            }
            var wall = M("Wall", new Color(0.93f, 0.88f, 0.83f), 0.1f);
            var floor = M("Floor", new Color(0.72f, 0.55f, 0.4f), 0.35f);
            // 门洞开在后墙上：后墙前表面 = 门面（z = -PortalDist）；房间 z 从 -PortalDist 到 3.45
            float zb = -PortalDist, zf = 3.45f, zc = (zb + zf) / 2, len = zf - zb;
            Box("Floor", new Vector3(0, -0.05f, zc), new Vector3(8, 0.1f, len), floor);
            Box("WallBack", new Vector3(0, 1.35f, zb - 0.05f), new Vector3(8, 2.7f, 0.1f), wall);
            Box("WallL", new Vector3(-4, 1.35f, zc), new Vector3(0.1f, 2.7f, len), wall);
            Box("WallR", new Vector3(4, 1.35f, zc), new Vector3(0.1f, 2.7f, len), wall);
            Box("WallFront", new Vector3(0, 1.35f, zf), new Vector3(8, 2.7f, 0.1f), wall);      // 真实房间四面都有墙（玩具化覆盖要有表面）
            Box("Ceiling", new Vector3(0, 2.75f, zc), new Vector3(8, 0.1f, len), M("Ceil", new Color(0.97f, 0.95f, 0.93f), 0));
            Box("Rug", new Vector3(0, 0.005f, -1.6f), new Vector3(3.4f, 0.01f, 2.6f), M("Rug", new Color(0.62f, 0.7f, 0.86f), 0));
            Box("Sofa", new Vector3(-3.3f, 0.35f, 0.8f), new Vector3(0.9f, 0.7f, 2f), M("Sofa", new Color(0.62f, 0.72f, 0.6f), 0.05f));
            Box("Shelf", new Vector3(3.6f, 0.95f, -1.8f), new Vector3(0.35f, 1.9f, 1.1f), M("Shelf", new Color(0.62f, 0.47f, 0.33f), 0.2f));
        }

        // ───────── 第 1 章：糖果积木门 + 积木小镇 ─────────
        public static GameObject BuildPortalW1(Transform root, Vector3 pos, int stencilRef)
        {
            var portal = new GameObject("Portal_W1");
            portal.transform.SetParent(root, false);
            portal.transform.localPosition = pos;
            Inst(Proc + "/Portal_Bricks.fbx", portal.transform, BangArt.ToyVC);      // 沿不规则洞口挤出墙面的糖果积木
            // （原来门口散着一层碎积木 Portal_Debris：是给旧小路摆的，新小路从门洞穿过后它们正压在路面和花草上，已去掉；路的房间那头另有“还没铺好的散砖”）
            PortalOpening(portal.transform, 1, stencilRef);                           // 不规则虫洞门洞（遮罩 + 涡旋膜 + 发光内沿）

            var w = new GameObject("World_BlockTown");
            w.transform.SetParent(portal.transform, false);
            SkyAndBackdrop(w.transform, "W1", new Color(0.66f, 0.82f, 1f), new Color(1f, 0.88f, 0.78f));
            var ground = Inst(Proc + "/W1_Ground.fbx", w.transform, BangArt.TurfGroundVC);
            FlattenNearRoom(ground, w.transform);   // 真实房间范围内压平地形（不然从房间里看会被山丘挡住门洞）
            // 临时碰撞体：构建时取地面高度，让道具贴合起伏的山丘（构建完删掉）
            var gcol = ground ? ground.GetComponentInChildren<MeshFilter>().gameObject.AddComponent<MeshCollider>() : null;
            Physics.SyncTransforms();
            _groundRoot = w.transform;
            Inst(Proc + "/W1_Track.fbx", w.transform, BangArt.WoodVC);
            Inst(Proc + "/W1_BrickPath.fbx", w.transform, BangArt.ToyVC);
            Inst(Proc + "/W1_Rainbow.fbx", w.transform, BangArt.RainbowVC);
            Inst(Proc + "/W1_Flora.fbx", w.transform, BangArt.FeltVC);   // 绒布小花、草丛、小珠、散落积木

            var props = new GameObject("Props").transform; props.SetParent(w.transform, false);
            // 名称, x, z, 朝向（度，0 = 正面朝向门/玩家）
            var layout = new (string n, float x, float z, float yaw)[]
            {
                ("W1_House_Pink", -0.75f, -2.75f, 15), ("W1_House_Purple", 1.0f, -3.2f, -20), ("W1_House_Teal", -0.1f, -3.65f, 5),
                ("W1_ClockTower", 0.75f, -4.55f, -10),
                ("W1_LollipopTree_A", -1.55f, -1.55f, 0), ("W1_LollipopTree_B", 1.5f, -1.75f, 0), ("W1_LollipopTree_A", -2.25f, -3.3f, 0),
                ("W1_LollipopTree_B", 2.6f, -2.9f, 0), ("W1_LollipopTree_A", -0.95f, -5.0f, 0),
                ("W1_FeltTree", -1.95f, -4.45f, 0), ("W1_FeltTree", 3.0f, -5.8f, 0), ("W1_FeltTree", -3.2f, -6.4f, 0), ("W1_FeltTree", 1.7f, -6.2f, 0),
                ("W1_GumdropBushes", 0.55f, -1.15f, 0), ("W1_GumdropBushes", -0.62f, -0.95f, 30), ("W1_GumdropBushes", 1.25f, -4.3f, 0),
                ("W1_Mushroom_Lamp", -0.42f, -1.5f, 0), ("W1_Mushroom_Lamp", 0.58f, -2.05f, 0),
                ("W1_HillMound", -0.3f, -5.7f, -10),               // 小路尽头正对门洞：投石车必须透过门洞看得到、打得到
            };
            foreach (var (n, x, z, yaw) in layout) Place(n, props, new Vector3(x, GroundY(x, z), z), yaw);
            // 风车：塔身立在左边的小山上，扇叶装在正面的轴上、一直慢慢转
            var mill = Place("W1_Windmill_Body", props, new Vector3(-2.65f, GroundY(-2.65f, -5.45f), -5.45f), 20);
            if (mill)
            {
                var mb = Bounds(mill);
                var blades = Place("W1_Windmill_Blades", mill.transform, Vector3.zero, 0);
                if (blades)
                {
                    blades.transform.SetParent(props, true);
                    blades.transform.position = new Vector3(mb.center.x, mb.min.y + mb.size.y * 0.8f, mb.center.z) + mill.transform.forward * 0f + (mill.transform.rotation * Vector3.left) * (mb.extents.x * 0.0f);
                    blades.transform.rotation = mill.transform.rotation;
                    var front = mill.transform.rotation * Vector3.left;      // Tripo 模型正面朝 -X
                    blades.transform.position += front * (Mathf.Max(mb.extents.x, mb.extents.z) * 0.85f);
                    var sp = blades.AddComponent<Spin>(); sp.axis = Vector3.right; sp.degPerSec = 28f; sp.gust = 0.45f;   // 跟云同一阵风：阵风时快、风小时慢
                }
            }
            // 投石车：小路尽头的山丘顶上（正对门洞），投臂装在横梁上，定时蓄力、扬起（玩法里朝玩家抛积木球）
            var mound = props.Find("W1_HillMound");
            if (mound)
            {
                var hb = Bounds(mound.gameObject);
                var cat = Place("W1_Catapult_Base", props, props.InverseTransformPoint(new Vector3(hb.center.x, hb.max.y - 0.05f, hb.center.z)), 0);   // Bounds 是世界坐标；轮子压进山包顶的沙坑里
                if (cat)
                {
                    var cb = Bounds(cat);
                    var arm = Place("W1_Catapult_Arm", props, props.InverseTransformPoint(new Vector3(cb.center.x, cb.min.y + cb.size.y * 0.78f, cb.center.z)), 90);
                    if (arm) arm.AddComponent<CatapultArm>().axis = Vector3.forward;
                }
            }
            if (gcol) Object.DestroyImmediate(gcol);
            // 漂浮的云（彩虹两端各一朵 + 天上两朵）
            var clouds = new (string n, Vector3 p, float s)[]
            {
                ("W1_Cloud_A", new Vector3(-2.7f, 0.15f, -8.6f), 2.2f), ("W1_Cloud_B", new Vector3(4.1f, 0.1f, -8.6f), 2.4f),
                ("W1_Cloud_A", new Vector3(-1.6f, 2.5f, -6.2f), 1.1f), ("W1_Cloud_B", new Vector3(2.0f, 2.9f, -7.4f), 1.4f),
            };
            foreach (var (n, p, s) in clouds)
            {
                if (p.y < 1f) { var c = Place(n, props, p, 0); if (c) c.transform.localScale *= s; }   // 彩虹脚下坐在地上的云：不动
                else FloatCloud(n, props, p, s);                                                     // 天上的云：随风慢慢漂
            }
            // 小火车
            var train = new GameObject("Train").AddComponent<TrainLoop>();
            train.transform.SetParent(w.transform, false);
            var cars = new List<Transform>();
            foreach (var n in new[] { "W1_Train_Engine", "W1_Train_Car", "W1_Train_Car" }) { var c = Place(n, train.transform, Vector3.zero, 0); if (c) cars.Add(c.transform); }
            train.cars = cars.ToArray(); train.yawOffset = 90;
            train.Step(0);   // AddComponent 时 OnEnable 还没有车厢，赋值后摆一次
            // 飘浮的小星光：小镇上空慢慢升起、闪烁
            Sparkles(w.transform, new Vector3(0.3f, 1.1f, -3.4f), new Vector3(6.5f, 1.8f, 5.5f), 12);
            Enrich(1, w.transform, props);   // 丰富度加密（远景 / 近景 / 中景 / 小生命）
            FloraRing(1, w.transform);   // 房间四周一整圈地面花草
            Vignettes(1, w.transform, props);   // 房间四周的主题小场景组团
            GiantRing(1, w.transform, props);   // 远处一圈巨型玩具（被玩具包围）
            Surround(1, w.transform, props);  // 房间四周和门洞两侧按主题摆满（玩具化露出哪个方向都不空）
            var pw = w.AddComponent<PortalWorld>();
            pw.stencilRef = stencilRef;
            pw.hazeStart = 9.0f; pw.hazeEnd = 24.0f; pw.hazeAmount = 0.054f;
            pw.Rebind();
            ShadowPolicy(w.transform);                                                         // 只让有体量的物体投影
            w.AddComponent<PortalCuller>().arch = portal.GetComponentInChildren<PortalArch>();   // 只渲染透过门洞 / 已玩具化区域看得到的物体
            Spill(1, portal.transform);   // 门前真实房间里的溢出物（门后世界从洞口淌出来一截）
            return portal;
        }

        /// <summary>门后世界里的星光粒子（Bang/Sparkle，PortalWorld 会套上模板）。</summary>
        static ParticleSystem Sparkles(Transform parent, Vector3 center, Vector3 size, float rate)
        {
            var go = new GameObject("Sparkles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.94f), new Color(0.82f, 0.94f, 1f));
            main.gravityModifier = -0.006f;
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0.85f, 0.75f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;   // 一闪一闪
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(0.18f, 1f), new Keyframe(0.38f, 0.45f), new Keyframe(0.58f, 1f), new Keyframe(0.78f, 0.5f), new Keyframe(1f, 0f)));
            var nz = ps.noise; nz.enabled = true; nz.strength = 0.06f; nz.frequency = 0.35f; nz.scrollSpeed = 0.15f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = BangArt.Mat("M_Sparkle", null, Shader.Find("Bang/Sparkle"));
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play(true);
            ps.Simulate(6f, true, true);   // 编辑器截图时也能看到
            return ps;
        }

        // 环形天幕把门后世界和真实房间整个围在里面：玩具化覆盖到房间哪面墙，就从那面墙看到同一个世界的那个方向
        public const float CycloR = 12.2f, CycloTop = 9.6f;
        static readonly Vector3 CycloCenter = new(0.2f, 0f, -1.0f);

        /// <summary>门后世界的天：一圈 360° 环形手绘天幕（Cyclo_Wn，横向一圈重复 2 次）+ 天顶（画面最上一行的颜色），
        /// 再在地形下面垫一张大圆地皮（地形边缘外、房间前方也有地面）。没有 Cyclo_Wn 时退回 Sky_Wn。</summary>
        static void SkyAndBackdrop(Transform w, string id, Color top, Color horizon, float radius = CycloR, float height = 10.2f)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Bang/Art/Textures/Cyclo_{id}.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Bang/Art/Textures/Sky_{id}.png");
            var set = new GameObject("SkySet").transform; set.SetParent(w, false); set.localPosition = CycloCenter;
            SkyTexImport(tex);
            // 一个完整的天球：整张横幅全景绕一圈（不重复），往上平滑过渡到天顶色——没有圆柱 + 顶盖的接缝
            var topCol = tex ? TopRowColor(tex) : top;
            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere); sky.name = "SkyDome_" + id;
            Object.DestroyImmediate(sky.GetComponent<Collider>());
            sky.transform.SetParent(set, false); sky.transform.localScale = Vector3.one * 120f;      // 半径 60 m：房间里走动时方向偏差可以忽略
            var mr = sky.GetComponent<MeshRenderer>();
            mr.sharedMaterial = BangArt.Mat("M_SkyDome_" + id, m =>
            {
                m.SetTexture("_MainTex", tex); m.SetFloat("_TexAmount", tex ? 1 : 0); m.SetFloat("_DirMode", 2); m.SetFloat("_Cull", 0);
                m.SetFloat("_BandMin", -16f); m.SetFloat("_BandMax", 72f); m.SetFloat("_ZenithBlend", 16f);   // 画里的远山大半沉到 3D 地形后面，只露山头（比例不和 3D 景物打架）
                m.SetColor("_Zenith", Color.Lerp(topCol, top, 0.25f)); m.SetColor("_Top", top); m.SetColor("_Horizon", horizon);
                m.SetTextureScale("_MainTex", Vector2.one); m.SetTextureOffset("_MainTex", new Vector2(0.5f, 0)); m.SetFloat("_Exposure", 1f);
            }, Shader.Find("Bang/PortalSky"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            if (id == "W6") return;                                                // 玩具箱自己有地板
            var (gtex, gcol) = id switch
            {
                "W2" => ("T_SandGrain", new Color(1f, 0.86f, 0.8f)),
                "W3" => ("T_TurfGrain", new Color(0.56f, 0.76f, 0.42f)),
                "W4" => ("T_ConfettiFelt", new Color(0.78f, 0.94f, 0.84f)),
                _ => ("T_TurfGrain", new Color(0.46f, 0.76f, 0.34f)),
            };
            var apron = MeshObj("GroundApron_" + id, set, Disc(CycloR + 0.1f, 96, 0.5f, false), BangArt.Mat("M_Apron_" + id, m =>
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Bang/Art/Textures/{gtex}.png")); m.SetColor("_BaseColor", gcol);
                m.SetFloat("_Gloss", 0.02f); m.SetFloat("_Sheen", 0.4f); m.SetFloat("_RimStrength", 0.04f); m.SetFloat("_ShadeSoft", 0.8f);
            }));
            apron.transform.localPosition = new Vector3(0, -0.035f, 0);
            _missY = -0.035f;
        }

        /// <summary>天球贴图：8192 宽、不要 mip（经度接缝不出细线）、横向循环纵向钳制。</summary>
        static void SkyTexImport(Texture2D tex)
        {
            if (!tex) return;
            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
            if (imp.maxTextureSize == 8192 && !imp.mipmapEnabled && imp.wrapModeU == TextureWrapMode.Repeat && imp.wrapModeV == TextureWrapMode.Clamp) return;
            imp.maxTextureSize = 8192; imp.mipmapEnabled = false; imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.SaveAndReimport();
        }

        static Color TopRowColor(Texture2D tex)
        {
            var path = AssetDatabase.GetAssetPath(tex);
            var tmp = new Texture2D(2, 2); tmp.LoadImage(System.IO.File.ReadAllBytes(path));
            Color sum = Color.black; int n = 0;
            for (int x = 0; x < tmp.width; x += 16) for (int y = tmp.height - 6; y < tmp.height; y += 2) { sum += tmp.GetPixel(x, y); n++; }
            Object.DestroyImmediate(tmp);
            var c = sum / Mathf.Max(n, 1); c.a = 1; return c;
        }

        static Transform _groundRoot;
        /// <summary>门后世界局部坐标 (x,z) 处的地面高度（向下打射线到绒布草地）。</summary>
        static float GroundY(float x, float z)
        {
            if (!_groundRoot) return 0;
            var o = _groundRoot.TransformPoint(new Vector3(x, 20, z));
            return Physics.Raycast(o, Vector3.down, out var hit, 60f) ? _groundRoot.InverseTransformPoint(hit.point).y : _missY;
        }
        /// <summary>主地面网格以外（远处一圈）的地面高度 = 外圈地裙的高度（没有地裙的室内章为 0）。原来一律按 0：放在地裙上的吉普车悬空 3.5 cm。</summary>
        static float _missY;

        /// <summary>这一点（_groundRoot 局部 xz）正下方有没有地面网格。</summary>
        static bool HasGround(float x, float z)
        {
            if (!_groundRoot) return true;
            var o = _groundRoot.TransformPoint(new Vector3(x, 20, z));
            foreach (var h in Physics.RaycastAll(o, Vector3.down, 60f)) if (h.collider.name.Contains("Ground")) return true;
            return false;
        }

        /// <summary>平的模型（轨道…）逐顶点贴到地面上：每个顶点放到“它下面的地面 + 它在原模型里离底面的高度”（整块平放在坡上会一边悬空，用户：悬空）。</summary>
        static void ConformToGround(GameObject g, string meshName)
        {
            if (!g || !_groundRoot) return;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                var src = mf.sharedMesh; if (!src) continue;
                var m = Object.Instantiate(src); var v = m.vertices;
                float minY = float.MaxValue; foreach (var x in v) minY = Mathf.Min(minY, mf.transform.TransformPoint(x).y);
                for (int i = 0; i < v.Length; i++)
                {
                    var wp = mf.transform.TransformPoint(v[i]); var lp = _groundRoot.InverseTransformPoint(wp);
                    var gw = _groundRoot.TransformPoint(new Vector3(lp.x, GroundY(lp.x, lp.z), lp.z));
                    v[i] = mf.transform.InverseTransformPoint(new Vector3(wp.x, gw.y + (wp.y - minY), wp.z));
                }
                m.vertices = v; m.RecalculateBounds();
                mf.sharedMesh = SaveMesh(m, meshName + "_" + mf.name);
            }
        }

        /// <summary>沿路线走、没有轨道的小车（吉普、巡逻锡兵）：量一下车底离地多少，把路线整体高度补上（车模原点不在车底，原来悬空 4 cm）。</summary>
        static void FitLoopToGround(Bang.Ambient.TrainLoop loop)
        {
            if (!loop || !_groundRoot || loop.cars == null || loop.cars.Length == 0 || !loop.cars[0]) return;
            // 沿整圈 12 个位置量“车身下最高的地面 − 车底”，取最小的那个（= 最需要往下压的：一圈下来哪儿都不悬空，坡上个别处轮子略压进沙里）
            float perim = Mathf.PI * (3 * (loop.a + loop.b) - Mathf.Sqrt((3 * loop.a + loop.b) * (loop.a + 3 * loop.b)));
            float best = float.PositiveInfinity;
            for (int k = 0; k < 12; k++)
            {
                loop.Step(perim / 12f / Mathf.Max(0.01f, loop.speed));
                foreach (var c in loop.cars)
                {
                    if (!c) continue;
                    var b = Bounds(c.gameObject); var lc = _groundRoot.InverseTransformPoint(b.center);
                    float bottom = _groundRoot.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
                    float g0 = GroundY(lc.x, lc.z);
                    foreach (var o in new[] { new Vector3(b.extents.x, 0, 0), new Vector3(-b.extents.x, 0, 0), new Vector3(0, 0, b.extents.z), new Vector3(0, 0, -b.extents.z) })
                    { var q = _groundRoot.InverseTransformPoint(b.center + o * 0.6f); float gq = GroundY(q.x, q.z); if (Mathf.Abs(gq - g0) < 0.3f) g0 = Mathf.Max(g0, gq); }
                    best = Mathf.Min(best, g0 - bottom);
                }
            }
            if (float.IsPositiveInfinity(best)) return;
            loop.y += best - 0.004f;
            loop.Step(0);
        }

        /// <summary>矮的道具顺着坡面倾斜（取中心与四周 5 个点的地面拟合法线），并下沉到最低接触点，免得一侧悬空。高的塔不倾斜。</summary>
        static void SettleOnGround(GameObject g, float maxTiltDeg = 18f)
        {
            if (!g || !_groundRoot) return;
            var b = Bounds(g);
            var c = b.center; float r = Mathf.Max(b.extents.x, b.extents.z) * 0.8f;
            Vector3 Hit(Vector3 p) => Physics.Raycast(new Vector3(p.x, p.y + 20, p.z), Vector3.down, out var h, 60f) ? h.point : new Vector3(p.x, b.min.y, p.z);
            if (b.size.y > 0.75f)
            {
                // 高的塔不倾斜，只调高度：底面不高出占地里最高的地面 2.5 cm（不悬空）、也尽量不低于最低的地面 5 cm（不深埋）
                // 只认中心附近 ±0.6 m 的地面命中（采样点落到地面边缘外会打到 58 m 下的天幕底，原来把整棵树沉下去了）
                bool G(Vector3 p, out float y)                          // 只认地面网格（不认天幕、远山）
                {
                    y = 0; float best = float.NegativeInfinity;
                    foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 20, p.z), Vector3.down, 60f))
                        if (h.collider.name.Contains("Ground") && h.point.y > best) best = h.point.y;
                    if (float.IsNegativeInfinity(best)) return false; y = best; return true;
                }
                if (!G(c, out var gc)) return;                         // 正下方没有地面（在地面网格边缘外）：不动
                float lo = gc, hi = gc;
                foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                { if (!G(c + d * r, out var y) || Mathf.Abs(y - gc) > 0.6f) continue; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
                float target = Mathf.Max(lo, hi - 0.025f);
                if (target > lo + 0.05f) target = (lo + 0.05f + hi - 0.025f) * 0.5f;
                g.transform.position += Vector3.up * (target - b.min.y);
                return;
            }
            var px = Hit(c + Vector3.right * r); var nx = Hit(c - Vector3.right * r);
            var pz = Hit(c + Vector3.forward * r); var nz = Hit(c - Vector3.forward * r);
            var n = Vector3.Cross(pz - nz, px - nx).normalized;
            if (n.y < 0) n = -n;
            n = Vector3.RotateTowards(Vector3.up, n, maxTiltDeg * Mathf.Deg2Rad, 0);
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * g.transform.rotation;
            float low = Mathf.Min(Mathf.Min(px.y, nx.y), Mathf.Min(pz.y, nz.y));
            float mid = Hit(c).y;
            g.transform.position += Vector3.up * ((low + mid) * 0.5f - mid) * 0.5f;   // 往下压一点，四角都着地
        }

        static Bounds Bounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject Inst(string fbx, Transform parent, Material mat)
        {
            var m = BangArt.Model(fbx);
            if (!m) { Debug.LogWarning("[Bang] 缺少模型 " + fbx); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(m, parent);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            return g;
        }

        /// <summary>放一个 P2 预制体：Tripo 模型正面朝 -X，yaw=0 时转成正面朝 +Z（朝向门 / 玩家）。</summary>
        static GameObject Place(string name, Transform parent, Vector3 pos, float yaw)
        {
            var path = $"{BangArt.PropDir}/{name}.prefab";
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!pf && (Sizes.TryGetValue(name, out var sz) || W2Sizes.TryGetValue(name, out sz) || W3Sizes.TryGetValue(name, out sz) || W4Sizes.TryGetValue(name, out sz) || W5Sizes.TryGetValue(name, out sz) || W6Sizes.TryGetValue(name, out sz) || RichSizes.TryGetValue(name, out sz))) pf = BangArt.ImportP2(name, sz.h, sz.gloss, sz.sheen);
            if (!pf) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0, 90 + yaw, 0);
            return g;
        }
    }
}
