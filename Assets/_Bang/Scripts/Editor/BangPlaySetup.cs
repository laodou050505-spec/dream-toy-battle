// 主场景的玩法装置：GameDriver（规则层 + 视图）、编辑器模拟输入、特效池、临时 HUD、中文字体。
// 由 BangSceneBuilder.Build 调用；也可以单独执行菜单“Bang/6 挂上玩法装置”给当前打开的主场景补上。
using System.Linq;
using Bang.Play;
using Bang.Play.Input;
using Bang.Portal;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Bang.EditorTools
{
    public static class BangPlaySetup
    {
        const string FontSrc = "Assets/_Bang/Art/Fonts/ZCOOLQingKeHuangYou-Regular.ttf";   // 站酷庆科黄油体，SIL OFL 1.1（粗圆卡通体；用户：UI 改欧美卡通风）
        const string FontAsset = "Assets/_Bang/Art/Fonts/ZCOOLQingKeHuangYou SDF.asset";
        const string Pf = "Assets/_Bang/Prefabs/Enemies";

        [MenuItem("Bang/6 挂上玩法装置（当前场景）")]
        public static void AddToOpenScene()
        {
            var world = GameObject.Find("BangWorld");
            if (!world) { Debug.LogError("[Bang] 当前场景里没有 BangWorld"); return; }
            var old = world.transform.Find("Play"); if (old) Object.DestroyImmediate(old.gameObject);
            Build(world.transform, Camera.main);
            EditorSceneManager.MarkSceneDirty(world.scene);
            EditorSceneManager.SaveScene(world.scene);
        }

        /// <summary>中文字体（动态 SDF：用到哪个字才往图集里加，不用预先烘一大张）。</summary>
        const string FontEnSrc = "Assets/_Bang/Art/Fonts/LilitaOne-Regular.ttf";     // 英文：Lilita One，SIL OFL 1.1（欧美卡通粗体，和标题同一套）
        const string FontEnAsset = "Assets/_Bang/Art/Fonts/LilitaOne SDF.asset";

        public static TMP_FontAsset FontEn()
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontEnAsset);
            if (fa) return fa;
            AssetDatabase.ImportAsset(FontEnSrc);
            var src = AssetDatabase.LoadAssetAtPath<Font>(FontEnSrc);
            if (!src) { Debug.LogWarning("[Bang] 缺少字体 " + FontEnSrc); return null; }
            fa = TMP_FontAsset.CreateFontAsset(src, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = "LilitaOne SDF";
            AssetDatabase.CreateAsset(fa, FontEnAsset);
            fa.atlasTextures[0].name = "LilitaOne Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = "LilitaOne Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz{|}~·×", out string _);
            EditorUtility.SetDirty(fa); AssetDatabase.SaveAssets();
            return fa;
        }

        public static TMP_FontAsset Font()
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);
            const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz{|}~·×";
            if (fa) { fa.TryAddCharacters(Ascii, out string _); EditorUtility.SetDirty(fa); return fa; }   // 英文版：常用英文字符预先烘进图集
            AssetDatabase.ImportAsset(FontSrc);
            var src = AssetDatabase.LoadAssetAtPath<Font>(FontSrc);
            if (!src) { Debug.LogWarning("[Bang] 缺少字体 " + FontSrc); return null; }
            fa = TMP_FontAsset.CreateFontAsset(src, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = "ZCOOLQingKeHuangYou SDF";
            AssetDatabase.CreateAsset(fa, FontAsset);
            fa.atlasTextures[0].name = "ZCOOLQingKeHuangYou Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = "ZCOOLQingKeHuangYou Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters("0123456789/%·：！…第章波共玩具化休息一下按空格跳过补给站继续房间被占领了重来这通关再次大来袭秒教学★");
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        public static void Build(Transform world, Camera cam)
        {
            var root = new GameObject("Play").transform; root.SetParent(world, false);
            var font = Font();

            // 特效池
            var fx = new GameObject("PlayFx").AddComponent<PlayFx>(); fx.transform.SetParent(root, false);
            fx.cam = cam;
            fx.wordMat = BangArt.Mat("M_FxWord", m => m.SetColor("_Color", Color.white), Shader.Find("Bang/FxSprite"));
            fx.tracerMat = BangArt.Mat("M_FxTracer", m => { m.SetColor("_Color", Color.white); m.SetTexture("_BaseMap", null); }, Shader.Find("Bang/FxSprite"));
            fx.confettiMat = BangArt.Mat("M_FxConfetti", null, Shader.Find("Bang/Confetti"));
            fx.puffMat = BangArt.Mat("M_FxDust", m => { m.SetColor("_Top", new Color(1f, 0.98f, 0.96f)); m.SetColor("_Bottom", new Color(0.88f, 0.84f, 0.92f)); }, Shader.Find("Bang/Puff"));
            fx.pengTex = Tex("Assets/_Bang/Art/Textures/FX/FX_Peng.png");
            fx.boTex = Tex("Assets/_Bang/Art/Textures/FX/FX_Bo.png");
            fx.hitTex = Tex("Assets/_Bang/Art/Textures/FX/FX_HitBurst.png");
            fx.crayonTex = Tex("Assets/_Bang/Art/Textures/FX/FX_CrayonMarks.png");
            fx.starTex = Tex(UiDir + "Icon_star.png", 256);
            fx.glowTex = Tex("Assets/_Bang/Art/Textures/FX/FX_BlobShadow.png");
            fx.muzzleTex = Tex("Assets/_Bang/Art/Textures/FX/FX_Muzzle.png");
            fx.wordTexs = new[] { "FX_Pa", "FX_PengB", "FX_Xiu" }.Select(n => Tex($"Assets/_Bang/Art/Textures/FX/{n}.png")).Where(t => t).ToArray();
            // 英文版拟声字：BANG / POOF / POP / POW / ZAP（用中文拟声字做参考图出的同风格英文字）
            fx.pengTexEn = Tex("Assets/_Bang/Art/Textures/FX/FX_Peng_EN.png");
            fx.boTexEn = Tex("Assets/_Bang/Art/Textures/FX/FX_Bo_EN.png");
            fx.wordTexsEn = new[] { "FX_Pa_EN", "FX_PengB_EN", "FX_Xiu_EN" }.Select(n => Tex($"Assets/_Bang/Art/Textures/FX/{n}.png")).Where(t => t).ToArray();

            // 音效（素材来自 ange-embed 素材库，清单见 Docs/音效来源.md）
            var au = new GameObject("BangAudio").AddComponent<BangAudio>(); au.transform.SetParent(root, false);
            var list = new System.Collections.Generic.List<BangAudio.Entry>();
            foreach (var (n, v) in Sfx)
            {
                var sp = $"Assets/_Bang/Audio/Sfx/{n}.mp3";
                if (AssetImporter.GetAtPath(sp) is AudioImporter si)                 // 短音效：加载场景时就解压进内存（不预载的话第一次播放会没声）
                {
                    var st = si.defaultSampleSettings;
                    if (!st.preloadAudioData || st.loadType != AudioClipLoadType.DecompressOnLoad) { st.preloadAudioData = true; st.loadType = AudioClipLoadType.DecompressOnLoad; si.defaultSampleSettings = st; si.SaveAndReimport(); }
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sp);
                if (clip) list.Add(new BangAudio.Entry { name = n, clip = clip, volume = v });
                else Debug.LogWarning("[Bang] 缺少音效 " + n);
            }
            au.clips = list.ToArray();
            var mu = new GameObject("BangMusic").AddComponent<BangMusic>(); mu.transform.SetParent(root, false);
            mu.tracks = new[]
            {   // 循环区间由音频分析得到（去掉开头静音、结尾的截断尾巴）
                MusicTrack("title", "bgm_title.mp3", 0.9f, 0f, 108.6f),
                MusicTrack("rest", "bgm_rest.mp3", 0.9f, 0.1f, 55.0f),
                MusicTrack("low", "bgm_battle_low.mp3", 1f, 0.45f, 50.2f),
                MusicTrack("high", "bgm_battle_high.mp3", 1f, 0.1f, 72.25f),
                MusicTrack("big", "bgm_bigwave.mp3", 0.95f, 0f, 40.65f),
                MusicTrack("ch1", "bgm_ch1.mp3", 0.8f, 0f, 0f),
                MusicTrack("ch2", "bgm_ch2.mp3", 0.8f, 0f, 0f),
                MusicTrack("ch3", "bgm_ch3.mp3", 0.8f, 0f, 0f),
                MusicTrack("ch4", "bgm_ch4.mp3", 0.8f, 0f, 0f),
                MusicTrack("ch5", "bgm_ch5.mp3", 0.8f, 0f, 0f),
                MusicTrack("ch6", "bgm_ch6.mp3", 0.8f, 0f, 0f),
                MusicTrack("boss", "bgm_boss.mp3", 0.42f, 0f, 0f),   // 快节奏 136 BPM（用户：Boss 音乐要紧张刺激）；原曲响度比其他曲目高约 10 dB，音量压到 0.42
            };
            mu.stingWin = Clip("sting_win.wav", false); mu.stingLose = Clip("sting_lose.mp3", false);

            // 输入（编辑器：鼠标键盘；真机输入在第三步接入）
            var desk = new GameObject("DesktopInput").AddComponent<DesktopInput>(); desk.transform.SetParent(root, false);
            desk.cam = cam;
            var pico = new GameObject("PicoInput").AddComponent<PicoInput>(); pico.transform.SetParent(root, false);   // 头显上自动切换到它
            pico.cam = cam; pico.trackingOrigin = cam ? cam.transform.parent : null;

            // 总调度
            var gd = new GameObject("GameDriver").AddComponent<GameDriver>(); gd.transform.SetParent(root, false);
            gd.input = desk; gd.xrInput = pico; gd.fx = fx; gd.font = font;
            gd.worlds = Object.FindObjectsByType<PortalWorld>().OrderBy(w => w.stencilRef).ToArray();
            gd.planterPrefab = P("Enemy_Planter"); gd.brutePrefab = P("Enemy_Brute"); gd.mousePrefab = P("Enemy_Mouse");
            gd.trooperPrefab = P("Enemy_Trooper"); gd.tinPrefab = P("Enemy_Tin"); gd.minionPrefab = P("Enemy_Minion"); gd.commanderPrefab = P("Enemy_Commander"); gd.ninjaPrefab = P("Enemy_Ninja");
            gd.jackPrefab = P("Enemy_Jack"); gd.planePrefab = P("Enemy_Plane"); gd.balloonPrefab = P("Enemy_Balloon"); gd.carPrefab = P("Enemy_Car"); gd.botPrefab = P("Enemy_Bot");
            gd.bossPrefabs = new GameObject[6]; for (int i = 0; i < 6; i++) gd.bossPrefabs[i] = P("Enemy_Boss" + (i + 1)); gd.brickPrefab = P("BlockBall");
            gd.flagSmallPrefab = P("Flag_Small"); gd.flagBigPrefab = P("Flag_Big"); gd.gunPrefab = P("ToyEagle"); gd.rifleModel = P("ToyRifle");
            gd.enemyGunMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Bang/Art/Models/Proc/Gun/Gun_Enemy.asset");   // 射手手里的低模枪（玩家那把 3.7 万面，敌人手上只有几厘米：用 2000 面的减面版）
            gd.targetPrefab = P("Target_Balloon"); gd.dartPrefab = P("Dart_Foam"); gd.shieldPrefab = P("ToyShield");
            gd.music = mu;
            gd.outlineMat = BangArt.Mat("M_ToyOutline", m => { m.SetColor("_OutlineColor", new Color(0.33f, 0.2f, 0.42f, 1)); m.SetFloat("_Width", 0.0035f); }, Shader.Find("Bang/ToyOutline"));
            gd.reticleMat = BangArt.Mat("M_FxLockReticle", m => { m.SetTexture("_BaseMap", Tex("Assets/_Bang/Art/Textures/FX/FX_LockReticle.png")); m.SetColor("_Color", Color.white); m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); m.renderQueue = 3060; }, Shader.Find("Bang/FxSprite"));
            gd.reticleGlowMat = BangArt.Mat("M_FxLockGlow", m => { m.SetColor("_Color", Color.white); m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); m.renderQueue = 3055; }, Shader.Find("Bang/FxSprite"));
            gd.reticleGlowTex = Tex("Assets/_Bang/Art/Textures/FX/FX_BlobShadow.png");
            var oh = new GameObject("OffscreenHints").AddComponent<OffscreenHints>(); oh.transform.SetParent(root, false);
            oh.mat = BangArt.Mat("M_FxHintArrow", m => { m.SetColor("_Color", Color.white); m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); m.renderQueue = 3070; }, Shader.Find("Bang/FxSprite"));
            gd.offscreen = oh;
            var bb = new GameObject("BuffBadges").AddComponent<BuffBadges>(); bb.transform.SetParent(root, false);
            bb.iconMat = BangArt.Mat("M_FxIcon", m => { m.SetColor("_Color", Color.white); m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); m.renderQueue = 3065; }, Shader.Find("Bang/FxSprite"));
            bb.ringMat = BangArt.Mat("M_FxRing", m => { m.SetFloat("_Inner", 0.62f); }, Shader.Find("Bang/FxRing"));
            bb.gold = Tex(UiDir + "Icon_buff_gold.png", 256); bb.freeze = Tex(UiDir + "Icon_buff_freeze.png", 256);
            bb.repair = Tex(UiDir + "Icon_buff_repair.png", 256); bb.unflag = Tex(UiDir + "Icon_buff_unflag.png", 256); bb.rapid = Tex(UiDir + "Icon_buff_rapid.png", 256);
            gd.badges = bb;
            gd.ringMat = BangArt.Mat("M_FxRing", m => { m.SetFloat("_Inner", 0.62f); }, Shader.Find("Bang/FxRing"));
            gd.lockMat = BangArt.Mat("M_FxLock", m => { m.SetFloat("_Inner", 0.86f); m.SetColor("_Back", new Color(1, 1, 1, 0)); }, Shader.Find("Bang/FxRing"));
            gd.blobMat = BangArt.Mat("M_BlobShadow", m => { m.SetTexture("_BaseMap", Tex("Assets/_Bang/Art/Textures/FX/FX_BlobShadow.png")); m.SetColor("_Color", Color.white); }, Shader.Find("Bang/FxSprite"));
            var er = new GameObject("Enemies").transform; er.SetParent(root, false); gd.enemyRoot = er;

            // 界面（封面 / 选关 / HUD / 暂停 / 结算……）
            var uiGo = new GameObject("UI"); uiGo.transform.SetParent(root, false);
            var th = uiGo.AddComponent<Bang.Play.UI.UiTheme>();
            th.font = font;
            th.fontEn = FontEn();
            if (th.fontEn && font && (th.fontEn.fallbackFontAssetTable == null || !th.fontEn.fallbackFontAssetTable.Contains(font)))
            { th.fontEn.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { font }; EditorUtility.SetDirty(th.fontEn); }   // 万一漏了中文也不显示方框
            th.panelMat = BangArt.Mat("M_UIPanel", m => m.renderQueue = 3100, Shader.Find("Bang/UIPanel"));
            th.panelFrontMat = BangArt.Mat("M_UIPanelFront", m => m.renderQueue = 3104, Shader.Find("Bang/UIPanel"));
            th.imageMat = BangArt.Mat("M_UIImage", m => { m.SetColor("_Color", Color.white); m.renderQueue = 3110; }, Shader.Find("Bang/FxSprite"));
            th.glow = Tex("Assets/_Bang/Art/Textures/FX/FX_BlobShadow.png");
            th.glowMat = BangArt.Mat("M_UIGlow", m => { m.SetColor("_Color", Color.white); m.renderQueue = 3085; }, Shader.Find("Bang/FxSprite"));
            th.logoMat = BangArt.Mat("M_LogoCandy", m => { m.SetTexture("_BaseMap", Tex(UiDir + "Logo_3D.png", 2048)); m.renderQueue = 3090; }, Shader.Find("Bang/LogoCandy"));
            th.sparkleMat = BangArt.Mat("M_UISparkle", m => m.renderQueue = 3115, Shader.Find("Bang/Sparkle"));
            // 手作风格（参考纸艺信件 / 贴纸手账）：菜单板 = 牛皮纸板 + 毛边奶油纸，按钮 = 贴纸；纹理和贴纸是 nano banana 出图（Tools/craft_process.py 后处理）
            var paperTex = Tex(UiDir + "Craft/paper_tile.png", 1024);
            th.craftMat = BangArt.Mat("M_UICraft", m => { m.SetTexture("_KraftTex", Tex(UiDir + "Craft/kraft_tile.png", 1024)); m.SetTexture("_PaperTex", paperTex); m.renderQueue = 3100; }, Shader.Find("Bang/UICraft"));
            th.toonMat = BangArt.Mat("M_UIToon", m => { }, Shader.Find("Bang/UIToon"));
            th.sunburst = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Bang/Art/Models/Proc/UI/Th_Sunburst.asset");
            th.stickerOutlineMat = BangArt.Mat("M_StickerOutline", m => { m.SetColor("_OutlineColor", new Color(0.27f, 0.11f, 0.29f)); m.SetFloat("_Width", 0.002f); m.SetFloat("_FarBoost", 1f); }, Shader.Find("Bang/ToyOutline"));   // 封面描边：FarBoost=1 时线宽随距离放大（视觉粗细不随远近变），每件物件的宽度由 StickerOutline 按体量算
            th.stickerMat = BangArt.Mat("M_UISticker", m => { m.SetTexture("_PaperTex", paperTex); m.renderQueue = 3104; }, Shader.Find("Bang/UISticker"));
            th.washiTex = Tex(UiDir + "Craft/washi_tape.png", 1024);
            th.stickers = new[] { Tex(UiDir + "Craft/sticker_star.png", 512), Tex(UiDir + "Craft/sticker_block.png", 512), Tex(UiDir + "Craft/sticker_candy.png", 512) };
            th.logoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Bang/Art/Models/Proc/UI/Logo3D.fbx");
            th.logoPrefabEn = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Bang/Art/Models/Proc/UI/Logo3D_EN.fbx");
            th.logoMatEn = BangArt.Mat("M_LogoCandy_EN", m => { m.SetTexture("_BaseMap", Tex(UiDir + "Logo_3D_EN.png", 2048)); m.renderQueue = 3090; }, Shader.Find("Bang/LogoCandy"));
            th.titleStage = BangTheater.Build(th.logoPrefab, th.logoMat, null);      // 封面：玩具小剧场（用户选定概念图 A）
            th.ribbon = Tex(UiDir + "Ribbon.png", 1024);
            var icons = new System.Collections.Generic.List<Bang.Play.UI.UiTheme.Named>();
            foreach (var n in new[] { "star", "star_empty", "lock", "coin", "flag", "wand", "play", "pause", "retry", "home", "settings", "heart", "sound", "music", "language", "armlen", "seated", "standing", "gesture", "splat", "slowmo", "role_shoot", "role_command", "role_mess" })
                icons.Add(new Bang.Play.UI.UiTheme.Named { name = n, tex = Tex(UiDir + $"Icon_{n}.png", 256) });
            th.icons = icons.ToArray();
            var ges = new System.Collections.Generic.List<Bang.Play.UI.UiTheme.Named>();
            foreach (var n in new[] { "gun", "flick", "fist", "palm", "binoc", "pinch" }) ges.Add(new Bang.Play.UI.UiTheme.Named { name = n, tex = Tex(UiDir + $"Gesture_{n}.png", 512) });
            th.gestures = ges.ToArray();
            th.chapterThumbs = new Texture2D[6];
            for (int i = 0; i < 6; i++) th.chapterThumbs[i] = Tex(UiDir + $"Chapter_{i + 1}.png", 512);
            var um = uiGo.AddComponent<Bang.Play.UI.UiManager>();
            um.theme = th; um.driver = gd; um.head = cam ? cam.transform : null;
            gd.ui = um;

            // 真机上的真实房间表面（地板保底 + 空间网格），玩具化画在上面
            var surf = BangArt.Mat("M_RoomCoverMR", m =>
            {
                m.SetFloat("_RoomMode", 0);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.Zero); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_SrcBlendA", (float)UnityEngine.Rendering.BlendMode.Zero); m.SetFloat("_DstBlendA", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_ZWrite", 0); m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);   // 现实表面不遮挡任何游戏内容
            }, Shader.Find("Bang/ToyCover"));
            var mask = BangArt.Mat("M_RoomCoverMask", m => m.renderQueue = 1980, Shader.Find("Bang/ToyCoverMask"));
            var mr = new GameObject("MrRoom").AddComponent<MrRoom>(); mr.transform.SetParent(root, false);
            mr.trackingOrigin = cam ? cam.transform.parent : null; mr.surfaceMat = surf; mr.maskMat = mask;
            mr.meshBlockPrefab = MeshBlockPrefab(surf, mask);

            // 门后机关 / 补给礼盒 / 门涌 的表现
            var mp = new GameObject("MechPresenter").AddComponent<MechPresenter>(); mp.transform.SetParent(root, false);
            mp.driver = gd; mp.ballPrefab = P("BlockBall"); mp.giftPrefab = P("GiftBox"); mp.mechPrefabs = new[] { P("Mech_Mortar"), P("Mech_Gate"), P("Mech_Popper"), P("Mech_Crane"), P("Mech_Spring") }; mp.ringMat = gd.ringMat; mp.glowMat = th.glowMat; mp.glowTex = th.glow;
            gd.mech = mp;
        }

        // 音效名 → 音量（素材响度不一，在这里统一调平）
        static readonly (string, float)[] Sfx =
        {
            ("shot_plop", 1f), ("shot_boom", 0.6f), ("dry", 0.9f), ("reload_out", 0.8f), ("reload_in", 0.9f), ("reload_gold", 0.7f), ("reload_jam", 0.6f),
            ("hit_plastic", 0.9f), ("hit_plop", 0.7f), ("piece_knock", 0.8f), ("kill_pop", 0.9f), ("kill_scatter", 0.6f),
            ("flag_plant", 0.8f), ("flag_hit", 0.7f), ("flag_down", 0.9f), ("step_tap", 0.6f), ("stomp", 0.8f),
            ("mouse_squeak", 0.5f), ("mouse_wind", 0.5f), ("emerge", 0.6f), ("toy_spread", 0.5f), ("portal_open", 0.7f), ("portal_chime", 0.5f),
            ("wave_start", 0.7f), ("wave_clear", 0.7f), ("chapter_clear", 0.8f), ("lost", 0.7f),
            ("ui_hover", 0.5f), ("ui_select", 0.8f), ("ui_confirm", 0.7f), ("ui_back", 0.6f), ("shield_block", 0.8f), ("parry", 0.8f),
            ("shield_up", 0.6f), ("shield_break", 0.9f), ("buff_gold", 0.8f), ("buff_freeze", 0.9f), ("buff_repair", 0.8f), ("gift_open", 0.9f), ("danger_tick", 0.7f),
            // 第三批：敌人动作 / 机关 / 望远镜 / 封面幕布（之前这些事件没有声音）
            ("enemy_tele", 0.8f), ("enemy_shot", 0.8f), ("plant_warn", 0.45f), ("jack_open", 0.8f), ("jack_close", 0.6f), ("summon", 0.55f), ("order_bell", 0.6f),
            // 第四批：材质命中音 / 小玩具叫声 / Boss（2026-10-31）
            ("hit_metal", 0.75f), ("hit_metal2", 0.75f), ("hit_paper", 0.8f), ("hit_rubber", 0.7f), ("voice_fall", 0.5f), ("voice_pop", 0.5f), ("voice_jump", 0.4f), ("boss_blast", 0.8f), ("boss_gong", 0.85f),
            ("commander_down", 0.9f), ("dive", 0.7f), ("blink", 0.8f), ("reflect_ping", 0.8f), ("mark", 0.6f), ("binoc_on", 0.7f), ("binoc_off", 0.6f), ("surge", 0.8f), ("curtain", 0.6f),
        };

        const string UiDir = "Assets/_Bang/Art/UI/";
        const string MusicDir = "Assets/_Bang/Audio/Music/";

        static AudioClip Clip(string file, bool stream)
        {
            var path = MusicDir + file;
            if (AssetImporter.GetAtPath(path) is AudioImporter imp)
            {
                var st = imp.defaultSampleSettings;
                var want = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (st.loadType != want || st.compressionFormat != AudioCompressionFormat.Vorbis || !st.preloadAudioData)
                {
                    st.loadType = want; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = stream ? 0.5f : 0.7f; st.preloadAudioData = true;
                    imp.defaultSampleSettings = st; imp.loadInBackground = stream; imp.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static BangMusic.Track MusicTrack(string name, string file, float vol, float loopStart, float loopEnd)
            => new BangMusic.Track { name = name, clip = Clip(file, true), volume = vol, loopStart = loopStart, loopEnd = loopEnd };

        /// <summary>空间网格块预制体：网格由 PICO 运行时填，材质 = [玩具化表面, 遮罩]，带碰撞体（蜡笔痕）。</summary>
        static GameObject MeshBlockPrefab(Material surf, Material mask)
        {
            const string path = "Assets/_Bang/Prefabs/MR/SpatialMeshBlock.prefab";
            System.IO.Directory.CreateDirectory("Assets/_Bang/Prefabs/MR");
            var g = new GameObject("SpatialMeshBlock");
            g.AddComponent<MeshFilter>();
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterials = new[] { surf, mask }; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.AddComponent<MeshCollider>();
            var pf = PrefabUtility.SaveAsPrefabAsset(g, path); Object.DestroyImmediate(g);
            return pf;
        }

        /// <summary>UI / 特效贴图导入设置：透明度、开 mipmap（VR 里缩小时不闪烁）、夹边、尺寸上限。</summary>
        static Texture2D Tex(string path, int max = 512)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!imp) { AssetDatabase.ImportAsset(path); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (imp && (!imp.alphaIsTransparency || !imp.mipmapEnabled || imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize != max))
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = max;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static GameObject P(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Pf}/{n}.prefab");
    }
}
