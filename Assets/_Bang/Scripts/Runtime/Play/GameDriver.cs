// 玩法总调度：持有规则层 Game，每帧 输入 → Game.Update → 同步视图 → 分发 fx 事件。
//   规则层坐标与场景坐标只差 x 取反（LogicSpace）；规则层的门 i 对应场景里的门后世界 worlds[i]（小怪在门后时套它的模板材质）。
//   小怪 / 旗子按 id 一一对应视图：规则层出现新 id 就生成，消失就交给视图收尾（散架、走回门里、倒下缩没）。
//   编辑器快捷键：Enter = 重来 / 继续，Space = 跳过休息。
using System.Collections.Generic;
using Bang.Logic;
using Bang.Play.Input;
using Bang.Portal;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Bang.Play.UI;

namespace Bang.Play
{
    public class GameDriver : MonoBehaviour
    {
        [Header("输入")]
        public PlayerInputSource input;
        [Tooltip("头显上使用的输入（PICO 手部追踪 + 眼动）；编辑器里用 input（鼠标键盘）")] public PlayerInputSource xrInput;
        [Header("章节")]
        public int chapter = 1;
        [Tooltip("跳过新手教学（教学关的靶子 / 陪练还没做表现）")] public bool skipLesson = false;
        public bool autoStart = true;
        [Header("场景")]
        [Tooltip("规则层第 i 道门 → 场景里的门后世界（小怪在门后时用它的模板材质）")] public PortalWorld[] worlds;
        public Transform enemyRoot;
        [Header("预制体")]
        public GameObject planterPrefab, brutePrefab, mousePrefab, trooperPrefab, tinPrefab, minionPrefab, commanderPrefab, ninjaPrefab, jackPrefab, planePrefab, balloonPrefab, carPrefab, botPrefab, flagSmallPrefab, flagBigPrefab, gunPrefab, rifleModel, targetPrefab, dartPrefab, shieldPrefab;
        [Header("表现")]
        [Tooltip("章末 Boss（下标 0 = 第 1 章）")] public GameObject[] bossPrefabs;
        [Tooltip("Boss 扔出的积木砖 / 炮弹")] public GameObject brickPrefab;
        public Material ringMat, lockMat, blobMat;
        [Tooltip("视线锁定准星（糖果角括号）和它的光晕")] public Material reticleMat, reticleGlowMat; public Texture reticleGlowTex;
        public TMP_FontAsset font;
        public UiManager ui;
        public PlayFx fx;
        public MechPresenter mech;
        public BuffBadges badges;
        public Material outlineMat;
        public OffscreenHints offscreen;
        public BangMusic music;
        [HideInInspector] public float finaleLength = 13f;   // 终极决战谢幕时长（FinaleShow 按出场顺序算好，同步给规则层）
        [Tooltip("开局门口自己渗出的固定玩具化半径（米），之后只随插旗扩张")] public float toyBaseRadius = 1.4f;
        [Tooltip("每过一波门口额外渗出的半径（米），最多算 4 波")] public float waveLeak = 0.3f;   // 用户：稍微大了，缩小一点范围（原 0.5）
        [Tooltip("门口玩具化半径：房间玩具化 0% → 100% 时从这里长到 doorRadiusMax（满了要包住玩家身边整圈：门到身后最远的覆盖壳约 11 米）")] public float doorRadiusMin = 0.35f, doorRadiusMax = 13f;

        public Game Game { get; private set; }
        readonly Dictionary<int, EnemyView> _enemies = new();
        readonly Dictionary<int, FlagView> _flags = new();
        readonly Dictionary<string, GunView> _guns = new();
        readonly Dictionary<int, Transform> _darts = new();
        readonly Dictionary<int, Vector3> _shotDir = new();
        readonly HashSet<int> _seen = new();
        readonly List<int> _gone = new();
        LockReticle _lock; int? _lockId;
        ShieldView _shield;
        static readonly Color[] Crayons = { new(1f, 0.42f, 0.5f), new(0.35f, 0.7f, 1f), new(1f, 0.78f, 0.25f), new(0.5f, 0.85f, 0.45f), new(0.75f, 0.5f, 1f) };
        int _crayon;

        /// <summary>语言：玩家在设置里选过就用选的（PlayerPrefs bang.lang），否则按系统语言（中文系统默认中文，其余默认英文）。</summary>
        void Awake()
        {
            Bang.Logic.Config.Default.SetSensitivity(PlayerPrefs.GetInt("bang.sens", 1));
            _splat = gameObject.AddComponent<HitSplatView>();
            var lang = PlayerPrefs.GetString("bang.lang", "");
            if (lang == "") { var sl = Application.systemLanguage; lang = sl == SystemLanguage.Chinese || sl == SystemLanguage.ChineseSimplified || sl == SystemLanguage.ChineseTraditional ? "zh" : "en"; }
            Loc.En = lang == "en";
        }

        /// <summary>设置里切换语言：记住选择，拟声字 / 枪上的标签 / 界面马上换。</summary>
        public void SetLanguage(bool en)
        {
            Loc.En = en; PlayerPrefs.SetString("bang.lang", en ? "en" : "zh"); PlayerPrefs.Save();
            if (fx) fx.ApplyLanguage();
            foreach (var g in _guns.Values) if (g) g.ResetLabels();
            if (badges) badges.ResetLabels();
        }

        void Start()
        {
            if (xrInput && !Application.isEditor && UnityEngine.XR.XRSettings.isDeviceActive) { if (input) input.enabled = false; input = xrInput; }
            else if (xrInput) xrInput.enabled = false;
            Game = new Game(new GameOpts { store = new PrefsSaveStore() });
            _worlds = GetComponent<ChapterWorlds>(); if (!_worlds) _worlds = gameObject.AddComponent<ChapterWorlds>();
            if (input is PicoInput pi && Game.profile.armLen > 0) pi.armLen = Game.profile.armLen;
            if (!fx) fx = FindAnyObjectByType<PlayFx>();
            if (fx) fx.ApplyLanguage();
            if (!enemyRoot) enemyRoot = new GameObject("Enemies").transform;
            if (reticleMat) _lock = LockReticle.Create(transform, reticleMat, reticleGlowMat, reticleGlowTex);
            if (shieldPrefab) _shield = ShieldView.Create(shieldPrefab, transform);
            if (badges) { EnemyView.FrostIcon = badges.freeze; EnemyView.IconMat = badges.iconMat; }
            EnemyView.OutlineMat = outlineMat;
            if (ui && ui.theme) EnemyView.SparkleMat = ui.theme.sparkleMat;
            RoomToyification.Instance?.ClearAll(); _doorSeeds.Clear(); _doorSeedKey = -1;
            EnsureGuns();
            if (ui)
            {
                bool needCal = input is PicoInput && Game.profile.armLen <= 0;          // 头显首次进入：先量手臂
                CloseDoors();                                 // 封面阶段门不出现
                if (needCal) ui.ShowCalibrate(); else ui.ShowTitle();
                _worlds.endless = Game.HasEndlessResume || (Game.resume == null && Game.profile.cleared >= CHAPTERS.Count);
                _worlds.Ensure(Game.resume != null ? Game.resume.chapter : _worlds.endless ? Bang.Logic.EndlessMode.ChapterFor(1) : Mathf.Clamp(Game.profile.cleared + 1, 1, CHAPTERS.Count), null);   // 封面阶段就把要玩的那章世界加载好（门收着）
            }
            else if (autoStart) StartChapter();
        }

        /// <summary>小怪会经过的地方的实物登记成规则层障碍圈：门前溢出（真实房间里）的立体摆件 + 门后出门路线两侧的摆件（蘑菇灯、灌木…）。
        /// 地皮、花草、积木小路、轨道这些贴地的不算（小怪本来就踩在上面走）；自己会走的（小火车、溜出门的发条兔）也不算。
        /// 另外按门洞真实轮廓算出底部能走过去的横向范围（门洞下宽上圆，平底段最窄）。</summary>
        void RegisterObstacles()
        {
            Game.obstacles.Clear();
            // 门后世界是室内壳（工坊 / 玩具箱）：墙围成的矩形交给规则层，敌人不许走到墙外（用户：玩具房关卡的模型卡在背景墙外面）
            Game.worldBox = null;
            {
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue; bool any = false;
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
                {
                    var n = r.name; if (!(n == "W6_WallL" || n == "W6_WallR" || n == "W6_BackWall" || n == "W6_FrontWall" || n.StartsWith("W5_Wall_"))) continue;
                    var b = r.bounds; any = true;
                    // 墙的内表面：薄的那个轴取靠里的面
                    if (b.size.x < b.size.z) { if (b.center.x < 0) x0 = Mathf.Min(x0, b.max.x); else x1 = Mathf.Max(x1, b.min.x); }
                    else { if (b.center.z < 0) z0 = Mathf.Min(z0, b.max.z); else z1 = Mathf.Max(z1, b.min.z); }
                }
                if (any && x0 < x1 && z0 < z1)
                {
                    var a2 = LogicSpace.ToLogic(new Vector3(x0, 0, z0)); var b2 = LogicSpace.ToLogic(new Vector3(x1, 0, z1));   // ToLogic 翻转 x
                    Game.worldBox = (Mathf.Min((float)a2.x, (float)b2.x), Mathf.Max((float)a2.x, (float)b2.x), Mathf.Min((float)a2.z, (float)b2.z), Mathf.Max((float)a2.z, (float)b2.z));
                }
            }
            void Add(Transform g)
            {
                if (g.GetComponentInChildren<Bang.Ambient.TrainLoop>() || g.GetComponentInParent<Bang.Ambient.TrainLoop>()) return;
                foreach (var n in new[] { "Patch", "Flora", "BrickPath", "Track", "Ground", "Bird", "Butterfl", "Sparkle", "Cloud", "Rainbow", "Sky", "Cyclo" }) if (g.name.Contains(n)) return;
                var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y < 0.06f || Mathf.Max(b.extents.x, b.extents.z) > 1.2f) return;
                var p = LogicSpace.ToLogic(b.center);
                Game.obstacles.Add((p.x, p.z, Mathf.Max(b.extents.x, b.extents.z) * 0.9));
            }
            var doors = _worlds ? _worlds.Doors : null; if (doors == null || doors.Count == 0) return;
            var seen = new HashSet<Transform>();
            for (int i = 0; i < doors.Count; i++)
            {
                var portal = doors[i]; if (!portal) continue;
                var spill = portal.Find("Spill");
                if (spill) foreach (Transform c in spill) Add(c);
                // 门后：每扇门出门路线（门面局部 |x| < 1.4、门后 0..2.4 m）两侧的门后世界摆件（世界只有一份，挂在 0° 门下面）
                foreach (var props in _worlds.WorldProps())
                    foreach (Transform c in props)
                    {
                        var lp = portal.InverseTransformPoint(c.position);
                        if (Mathf.Abs(lp.x) < 1.4f && lp.z < 0.05f && lp.z > -2.4f && seen.Add(c)) Add(c);
                    }
            }
            // 门洞底部可通过的横向范围：离地 0.02..0.65 m（小怪身高范围）各高度上轮廓宽度的交集。所有门是同一个门洞复制的，按 0 号门算一次、每扇门都用
            var arch = doors[0] ? doors[0].GetComponentInChildren<Bang.Portal.PortalArch>(true) : null;
            var o = arch ? arch.outline : null;
            if (o != null && o.Length > 8 && Game.portals != null && Game.portals.Count > 0)
            {
                float L = float.MinValue, R = float.MaxValue;
                foreach (var y in new[] { 0.02f, 0.12f, 0.25f, 0.4f, 0.55f, 0.65f })
                {
                    float l = float.MaxValue, r = float.MinValue;
                    for (int i = 0; i < o.Length; i++)
                    {
                        var p0 = o[i]; var p1 = o[(i + 1) % o.Length];
                        if ((p0.y - y) * (p1.y - y) > 0 || Mathf.Approximately(p0.y, p1.y)) continue;
                        float x = Mathf.Lerp(p0.x, p1.x, (y - p0.y) / (p1.y - p0.y));
                        l = Mathf.Min(l, x); r = Mathf.Max(r, x);
                    }
                    if (l < r) { L = Mathf.Max(L, l); R = Mathf.Min(R, r); }
                }
                var P = Game.portals[0];
                double Lat(Vector3 wp) { var q = LogicSpace.ToLogic(wp); return (q.x - P.@base.x) * P.right.x + (q.z - P.@base.z) * P.right.z; }
                if (L < R) Game.SetGateAll(Lat(arch.transform.TransformPoint(new Vector3(L, 0, 0))), Lat(arch.transform.TransformPoint(new Vector3(R, 0, 0))));
                // 门洞真实拱形（往里收 3 cm：门框内沿那一圈也挡视线）：规则层据此判断“看不看得见门后的敌人”
                var c2 = Vector2.zero; foreach (var q in o) c2 += q; c2 /= o.Length;
                var hx = new double[o.Length]; var hy = new double[o.Length];
                for (int i = 0; i < o.Length; i++)
                {
                    var q = o[i]; var dq = q - c2; q -= dq.normalized * 0.03f;
                    var wp = arch.transform.TransformPoint(new Vector3(q.x, q.y, 0));
                    hx[i] = Lat(wp); hy[i] = wp.y;
                }
                Game.SetHoleAll(hx, hy);
            }
        }

        public void StartChapter() => StartChapter(chapter);

        // 传送门：封面阶段不出现，开始游戏时先播开门动画再进游戏（Bang.Portal.PortalOpen）。多门章节每扇门一起开
        ChapterWorlds _worlds;
        IEnumerable<Bang.Portal.PortalOpen> DoorOpens()
        {
            if (!_worlds) yield break;
            foreach (var d in _worlds.Doors) { var po = d ? d.GetComponent<Bang.Portal.PortalOpen>() : null; if (po) yield return po; }
        }
        bool DoorsOpen { get { bool any = false; foreach (var d in DoorOpens()) { if (!d.IsOpen) return false; any = true; } return any; } }
        void CloseDoors() { foreach (var d in DoorOpens()) d.CloseNow(); }

        /// <summary>门还没开：先对准玩家、收起菜单、播开门动画，播完再执行 then；门已开（下一章 / 再玩一次）直接执行。</summary>
        bool GateThroughDoor(System.Action then, bool align = true)
        {
            var doors = new List<Bang.Portal.PortalOpen>(DoorOpens());
            if (doors.Count == 0 || DoorsOpen) return false;
            foreach (var d in doors) if (d.Opening) return true;          // 已经在开了，别重复触发
            if (align) MRBootstrap.Instance?.AlignPlayArea();
            ui?.ShowHud();
            BangAudio.Ui("ui_confirm");
            int left = 0; foreach (var d in doors) if (!d.IsOpen) left++;
            foreach (var d in doors) if (!d.IsOpen) d.Open(() => { if (--left == 0) then?.Invoke(); });   // 几扇门同时开，全开完再进游戏
            return true;
        }

        /// <summary>这一章的世界还没加载好：先加载（门收着），好了再回来。</summary>
        bool GateWorld(int ch, System.Action then)
        {
            if (!_worlds) return false;
            if (_worlds.ReadyFor(ch)) return false;
            _worlds.Ensure(ch, then);
            return true;
        }

        bool _epilogueShown;
        public void StartChapter(int ch)
        {
            _epilogueShown = false;
            if (_worlds) _worlds.endless = false;                 // 章节：门按这一章摆
            ui?.LessonDone();                                     // 上一局教学没做完就退出时，教学卡不残留到新的一章
            if (_splat) _splat.Clear();
            _toyAge = 0;                                          // 新的一章：门口的基础蔓延从头开始
            ch = Mathf.Clamp(ch, 1, CHAPTERS.Count);
            if (Game != null) Game.seated = PlayerPrefs.GetInt("bang.seated", 0) == 1;   // 坐姿模式：门收窄（开章时生效）
            _bestStreak = 0; ToyTime.Reset();
            if (GateWorld(ch, () => StartChapter(ch))) return;
            if (GateThroughDoor(() => StartChapter(ch))) return;
            chapter = ch;
            // 门只在出现之前对齐一次（GateThroughDoor 里，门还没长出来时）。原来开门动画播完这里又对齐一次，
            // 玩家开门时转头看了一眼门在哪，门就跟着转过去了（用户：门的位置不要跟着跑）；门已经开着（下一章 / 再玩一次）也不再挪
            ClearViews();
            Game.StartChapter(chapter, new StartChapterOpts { skipLesson = skipLesson });
            RegisterObstacles();                                  // 这一章的门 / 门后摆件 / 门洞宽度
            EnsureGuns();
            mech?.Bind();
            ui?.ShowHud();
            BangAudio.Ui("ui_confirm");
            StartCoroutine(PrewarmBosses(chapter));
        }

        /// <summary>无尽模式：玩具箱世界（和终极决战同一个世界、同样四扇门），开门后一波接一波，直到房间被玩具化满。</summary>
        public void StartEndless() => StartEndlessAt(0);
        /// <summary>继续上次的无尽进度：回到那一波的开头（用户：暂停回来还是那个波数，从那波开始重新开始）。</summary>
        public void ContinueEndless() => StartEndlessAt(Game.HasEndlessResume ? Game.resume.wave : 0);

        void StartEndlessAt(int fromWave)
        {
            _epilogueShown = true;                                         // 无尽不放结尾故事卡
            ui?.LessonDone(); if (_splat) _splat.Clear(); _toyAge = 0;
            if (Game != null) Game.seated = PlayerPrefs.GetInt("bang.seated", 0) == 1;
            _bestStreak = 0; ToyTime.Reset();
            int ch = Bang.Logic.EndlessMode.ChapterFor(fromWave + 1);     // 场景按这一波所在的 Boss 段（用户：无尽的场景按 Boss 定）
            if (_worlds) _worlds.endless = true;                          // 无尽一律四扇门
            if (GateWorld(ch, () => StartEndlessAt(fromWave))) return;
            if (GateThroughDoor(() => StartEndlessAt(fromWave))) return;
            chapter = ch;
            ClearViews();
            if (!Game.StartEndless(fromWave)) { ui?.ShowChapters(); return; }
            RegisterObstacles();
            EnsureGuns();
            mech?.Bind();
            ui?.ShowHud();
            BangAudio.Ui("ui_confirm");
            if (fromWave > 0) ui?.Banner(Loc.T($"继续无尽 · 第 {fromWave + 1} 波", $"Endless · Wave {fromWave + 1}"), Loc.T("从这一波开头重新开始", "Restarting from the start of this wave"), 3f, true);
            else ui?.Banner(Loc.T("无尽模式", "Endless Mode"), Loc.T("每 5 波一个 Boss，看你能撑几波", "A boss every 5 waves. How long can you last?"), 3f, true);
            StartCoroutine(PrewarmBosses(ch));
        }

        /// <summary>无尽：打倒一段的 Boss 后去下一个 Boss 的世界——横幅报下一站，门合上，加载那个世界，门重新长出来，开下一波。</summary>
        System.Collections.IEnumerator EndlessTravel(int ch, int nextWave)
        {
            var nb = Bang.Logic.KINDS.Get(Bang.Logic.Game.BossOf(ch) ?? "boss1");
            string world = CHAPTERS.Get(ch) != null ? Loc.T(CHAPTERS.Get(ch).name, CHAPTERS.Get(ch).en) : "";
            ui?.Banner(Loc.T("下一站：" + world, "Next: " + world), Loc.T($"第 {nextWave + 1} 波起，{nb.name} 在等你", $"From wave {nextWave + 1}, {nb.en} awaits"), 3f, true);
            yield return new WaitForSeconds(2.8f);
            CloseDoors();
            BangAudio.Ui("portal_chime", 0.8f, 0.8f);
            yield return new WaitForSeconds(0.6f);
            bool ready = false;
            if (_worlds && !_worlds.ReadyFor(ch)) _worlds.Ensure(ch, () => ready = true); else ready = true;
            float t = 0; while (!ready && t < 30f) { t += Time.deltaTime; yield return null; }
            bool opened = false;
            if (!GateThroughDoor(() => opened = true, false)) opened = true;
            t = 0; while (!opened && t < 15f) { t += Time.deltaTime; yield return null; }
            if (Game.phase != "travel") yield break;                       // 期间玩家回了主菜单
            chapter = ch;
            foreach (var v in _enemies.Values) if (v) Destroy(v.gameObject);
            _enemies.Clear(); _doorSeeds.Clear(); _doorSeedKey = -1;
            Game.EndlessArrive(ch);
            RegisterObstacles();
            mech?.Bind();
            ui?.ShowHud();
            StartCoroutine(PrewarmBosses(ch));
        }

        /// <summary>续玩上次的存档（自动存档在每波之间）。</summary>
        public void Continue()
        {
            int rc = Game.resume != null ? Game.resume.chapter : chapter;
            if (GateWorld(rc, Continue)) return;
            if (GateThroughDoor(Continue)) return;
            ClearViews();
            if (!Game.ContinueGame()) { StartChapter(chapter); return; }
            chapter = Game.run.chapter;
            RegisterObstacles();
            EnsureGuns();
            mech?.Bind();
            ui?.ShowHud();
            StartCoroutine(PrewarmBosses(chapter));
        }

        public void ToMenu()
        {
            ClearViews();
            mech?.Clear();
            Game.BackToMenu();
            ui?.LessonDone();
            CloseDoors();                                     // 回到封面：门收起来
            ui?.ShowTitle();
        }

        int _waveKnocks;

        // 摘下头显 / 按了系统菜单（应用失去焦点）：战斗中自动暂停，回来时暂停菜单就在眼前
        void OnApplicationPause(bool paused) { if (paused) AutoPause(); }
        void OnApplicationFocus(bool focus) { if (!focus) AutoPause(); }
        void AutoPause() { if (!Application.isEditor && Game != null && (Game.phase == "wave" || Game.phase == "lesson") && ui && !ui.Paused) ui.ShowPause(); }

        /// <summary>失败画面上点了“再试一次”。</summary>
        public void RetryAfterLoss()
        {
            ClearViews();
            ui?.LessonDone();
            Game.RetryAfterLoss();
            EnsureGuns();
            mech?.Bind();
            ui?.ShowHud();
            BangAudio.Ui("ui_confirm");
        }

        public void Pause() { if (ui && !ui.Paused && ui.MenuOpen == false) { ui.ShowPause(); BangAudio.Ui("ui_back"); } }

        /// <summary>菜单里用手指枪打按钮：枪照样开火（动画 + “砰！” + 声音 + 蜡笔弹道），但不进规则层。</summary>
        public void CosmeticShot(string side, Vector3? target)
        {
            if (!_guns.TryGetValue(side, out var gun)) return;
            gun.Fire();
            var mz = gun.Muzzle; var muzzle = mz.position;
            fx?.Peng(muzzle + mz.forward * 0.08f + mz.up * 0.035f);
            BangAudio.At("shot_plop", muzzle); BangAudio.At("shot_boom", muzzle, 0.35f, 1.25f);
            var to = target ?? muzzle + mz.forward * 4f;
            fx?.Shot(muzzle, to, Crayons[_crayon++ % Crayons.Length]);
            if (target.HasValue) fx?.HitStar(to);
        }

        void EnsureGuns()
        {
            foreach (var side in new[] { "right", "left" })
            {
                bool want = Game.weapons.Get(side) != null && gunPrefab;
                if (want && !_guns.ContainsKey(side))
                {
                    var g = Instantiate(gunPrefab, transform);
                    g.name = "Gun_" + side;
                    var v = g.AddComponent<GunView>(); v.Init(side, ringMat, font, rifleModel);
                    var gm = g.GetComponent<Motion.GunMotion>();
                    if (gm && gm.muzzle) Input.DesktopInput.GunMuzzleOffset = g.transform.InverseTransformPoint(gm.muzzle.position);   // 枪口位置以预制体为准
                    _guns[side] = v;
                }
                else if (!want && _guns.TryGetValue(side, out var old)) { Destroy(old.gameObject); _guns.Remove(side); }
            }
            if (input) input.dual = Game.weapons.left != null;
        }

        void ClearViews()
        {
            foreach (var v in _enemies.Values) if (v) Destroy(v.gameObject);
            foreach (var v in _flags.Values) if (v) Destroy(v.gameObject);
            foreach (var t in _darts.Values) if (t) Destroy(t.gameObject);
            _enemies.Clear(); _flags.Clear(); _darts.Clear();
            RoomToyification.Instance?.ClearAll(); _doorSeeds.Clear(); _doorSeedKey = -1;
        }

        void Update()
        {
            if (Game == null || !input) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            HandleKeys();
            var frame = input.Poll(dt);
            bool consumed = ui && ui.Tick(frame, input.GazeWorld, dt);
            if (consumed && frame.hands != null) { if (frame.hands.left != null) frame.hands.left.events = new List<HandEvent>(); if (frame.hands.right != null) frame.hands.right.events = new List<HandEvent>(); }
            if (!(ui && ui.Paused))
            {
                ToyTime.Tick();
                float tdt = dt * ToyTime.Scale;                                  // 慢动作只放慢玩具，枪和手照常
                Game.Update(tdt, frame);
                SyncEnemies(tdt);
                SyncFlags(tdt);
                SyncBullets();
                mech?.Sync(tdt);
                foreach (var e in Game.DrainFx()) Dispatch(e);   // 先分发事件（死亡要用到还在的视图），再清掉规则层已移除的
                Cleanup();
                SyncToy(dt);
                SyncMood(dt);
            }
            foreach (var kv in _guns)
            {
                bool has = input.GunPose(kv.Key, out var p, out var r);
                if (ui && ui.MenuOpen && !ui.GunsInMenu) has = false;   // 菜单里手就是手：不变成手枪，用“看 + 捏”选按钮（封面练习除外：比出手指枪就变出玩具枪）
                kv.Value.Pose(has, p, r, dt);
                kv.Value.Sync(Game.weapons.Get(kv.Key), Game);
            }
            SyncLock();
            if (_shield) _shield.Sync(Game.shield, Game.ShieldMax(), dt);
            SyncBuffs(dt);
            SyncMusic(dt);
            SyncOffscreen(dt);
        }

        // ───────── 增益徽章 / 自适应音乐 ─────────
        void SyncBuffs(float dt)
        {
            if (!badges) return;
            badges.Timed("gold", (float)Game.goldT, (float)Game.cfg.mech.giftGoldDur);
            bool anyAlive = false; foreach (var e in Game.enemies) if (!e.dead) { anyAlive = true; break; }
            badges.SetPaused("gold", Game.goldT > 0 && !anyAlive);
            badges.Timed("rapid", (float)Game.rapidT, (float)Game.cfg.mech.giftRapidDur);
            double fz = 0; foreach (var e in Game.enemies) if (!e.dead && e.frozenT > fz) fz = e.frozenT;
            badges.Timed("freeze", (float)fz, (float)Game.cfg.mech.giftFreezeDur);
            badges.Tick(GunTransform("right"), dt);
        }

        readonly List<(Vector3, bool)> _offTargets = new();
        void SyncOffscreen(float dt)
        {
            if (!offscreen) return;
            _offTargets.Clear();
            if (Game.phase == "wave" && !(ui && ui.MenuOpen))
                foreach (var kv in _enemies)
                {
                    var v = kv.Value; if (!v || v.data.dead || v.data.state == "leave" || v.data.state == "flee" || !v.visible) continue;
                    _offTargets.Add((v.transform.position + Vector3.up * 0.2f, v.data.state == "plant"));
                }
            _offTargets.Sort((a, b) => b.Item2.CompareTo(a.Item2));      // 正在插旗的优先
            offscreen.Tick(Camera.main ? Camera.main.transform : null, _offTargets, dt);
        }

        bool _hot; float _tickT;
        void SyncMusic(float dt)
        {
            if (!music) return;
            string ph = Game.phase;
            float toy = (float)(Game.toy / 100.0);
            int alive = 0; foreach (var e in Game.enemies) if (!e.dead && e.state != "leave" && e.state != "flee") alive++;
            float inten = Mathf.Clamp01(toy * 0.9f + alive * 0.06f + (Game.SurgeWarning ? 0.5f : 0f));
            _hot = _hot ? inten > 0.38f : inten > 0.58f;               // 回差：不会在两首之间来回抖
            bool big = Game.run?.waveDef != null && Game.run.waveDef.big;
            string want = ph switch
            {
                "wave" => BossAlive() || Game.bossWarnT > 0 ? "boss" : big ? "big" : _hot ? "high" : music.Has("ch" + CHAPTERS.World(chapter)) && Game.run.mode == "chapter" ? "ch" + CHAPTERS.World(chapter) : "low",
                "lesson" => "low",
                "rest" or "station" or "clear" => "rest",
                "lost" => null,
                "explore" or "reward" when FinaleBright => "rest",
                _ => "title",
            };
            music.Want(want);
            music.SetMuffle(ui && ui.Paused);
            // 房间快被占领：音乐微微升调 + 越来越快的滴答声
            float danger = ph == "wave" ? Mathf.Clamp01((toy - 0.7f) / 0.3f) : 0f;
            music.SetPitch(1f + danger * 0.04f);
            if (danger > 0 && !(ui && ui.Paused))
            {
                _tickT -= dt;
                if (_tickT <= 0) { _tickT = Mathf.Lerp(0.95f, 0.3f, danger); BangAudio.Ui("danger_tick", 0.3f + 0.45f * danger, 1.15f + 0.35f * danger); }
            }
            else _tickT = 0;
        }

        // 连击：1.2 秒内接连打中，命中声一声比一声高（最多升 8 级）
        int _combo; float _comboT;
        float ComboPitch()
        {
            _combo = Time.time - _comboT < 1.2f ? Mathf.Min(_combo + 1, 16) : 0; _comboT = Time.time;
            return 1f + _combo * 0.03f;
        }

        void HandleKeys()
        {
            var kb = Keyboard.current; if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { if (ui && ui.Paused) ui.Resume(); else Pause(); }
            if (kb.spaceKey.wasPressedThisFrame && Game.phase == "rest") Game.phaseT = 999;
        }

        // ───────── 小怪 ─────────
        void SyncEnemies(float dt)
        {
            for (int i = 0; i < Game.enemies.Count; i++)
            {
                var e = Game.enemies[i];
                if (!_enemies.TryGetValue(e.id, out var v) || !v) { v = Spawn(e); _enemies[e.id] = v; }
                if (v) { v.visible = Game.CanSee(e, Game.head.pos); v.Sync(dt); }
            }
        }

        /// <summary>规则层已移除、但没收到死亡 / 离开事件的视图（重来、换章）：直接移除。</summary>
        void Cleanup()
        {
            _seen.Clear();
            foreach (var e in Game.enemies) _seen.Add(e.id);
            _gone.Clear();
            foreach (var kv in _enemies) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone) { if (_enemies[id]) _enemies[id].Exit(); _enemies.Remove(id); }
            _seen.Clear();
            foreach (var f in Game.flags) _seen.Add(f.id);
            _gone.Clear();
            foreach (var kv in _flags) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone) { if (_flags[id]) Destroy(_flags[id].gameObject); _flags.Remove(id); }
        }

        const string WeakTipKey = "bang.weakTip";
        HitSplatView _splat;
        int _highlightNext = -1;
        bool BossAlive() { foreach (var e in Game.enemies) if (!e.dead && Bang.Logic.Game.IsBoss(e)) return true; return false; }

        // 连杀：1.6 秒内接着杀就继续计数；越连越高调，到里程碑放大特效（用户：提高整个游戏的爽感）
        int _streak; float _streakT;
        public int BestStreak => _bestStreak;
        int _bestStreak, _headHits, _waveShots0, _waveHits0, _waveKills0; float _waveStartT;

        bool AnyOtherAlive(Enemy e) { foreach (var x in Game.enemies) if (!x.dead && !x.bonus && x != e) return true; return false; }

        /// <summary>材质命中音：铁皮叮、纸板沙、气球啵，其余塑料。</summary>
        static string HitSound(string kind) => kind switch
        {
            "tin" or "minion" or "commander" or "repair" or "boss3" or "boss5" => Random.value < 0.5f ? "hit_metal" : "hit_metal2",
            "ninja" or "plane" => "hit_paper",
            "balloon" or "balloonSmall" or "boss4" => "hit_rubber",
            _ => "hit_plastic",
        };
        /// <summary>小玩具叫声的音高：个子越小越尖。</summary>
        static float VoicePitch(Enemy e) => Mathf.Clamp(1.5f - (float)e.height * 0.7f, 0.6f, 1.45f) * Random.Range(0.94f, 1.06f);

        void LogWave(string evt, int wave)
        {
            var st = Game.run.stats;
            Telemetry.Log(evt, chapter, wave, Time.time - _waveStartT, st.shots - _waveShots0, st.hits - _waveHits0, st.kills - _waveKills0, _headHits, (float)Game.toy,
                $",\"seated\":{(Game.seated ? 1 : 0)},\"sens\":{PlayerPrefs.GetInt("bang.sens", 1)}");
        }

        /// <summary>开章时说清楚三星的条件（原来只有结算才知道差在哪）。</summary>
        void StarGoalTip()
        {
            bool intro = chapter <= 2;
            ui?.Banner(Loc.T("三星目标", "3-Star Goal"), Loc.T($"不失败，房间玩具化始终不超过 {Game.cfg.diff.StarPeak(chapter):0}%", $"No defeats, keep the room under {Game.cfg.diff.StarPeak(chapter):0}% toy"), 3f);
        }
        void OnKillStreak(Vector3 p, Enemy e)
        {
            _streak = Time.time - _streakT < 1.6f ? _streak + 1 : 1; _streakT = Time.time;
            if (_streak > _bestStreak) _bestStreak = _streak;
            if (_streak >= 3) ui?.Streak(_streak);
            if (_streak >= 5) { fx?.Confetti(p, 10); fx?.Glitter(p, new Color(1f, 0.75f, 0.85f), 8, 1f); }
            if (_streak == 5 || _streak == 10 || _streak == 20 || _streak == 30 || _streak == 50)
            { ToyTime.Slow(0.3f, 0.35f);
                fx?.Flash(p, new Color(1f, 0.9f, 0.7f), 1.2f, 0.4f); fx?.Confetti(p + Vector3.up * 0.3f, 40);
                BangAudio.Ui("buff_gold", 0.8f, 1f + _streak * 0.006f);
            }
            if (e.kind == "brute" || e.kind == "commander")                     // 大块头倒下：一记重的
            {
                fx?.Flash(p, new Color(1f, 0.95f, 0.8f), 1.6f, 0.5f); fx?.Confetti(p, 50); fx?.Dust(e != null ? p + Vector3.down * 0.3f : p, 2f, 14);
                BangAudio.At("stomp", p, 1f, 0.8f);
            }
        }

        /// <summary>第一次遇到某种玩具时报个名、说一句怎么对付（只提示一次，存在 PlayerPrefs）。狂欢小怪不提示。</summary>
        void NewKindTip(Enemy e)
        {
            string id = e.kind switch { "balloonSmall" => "balloon", "trainer" => "shooter", _ => e.kind };
            if (e.bonus || id == "planter" || id == "dummy" || ui == null || Bang.Logic.Game.IsBoss(e)) return;   // Boss 有自己的预警 / 登场横幅
            string key = "bang.seenKind." + id;
            if (PlayerPrefs.GetInt(key, 0) != 0) return;
            PlayerPrefs.SetInt(key, 1);
            string tip = e.k.role == "shoot" ? Loc.T("它会攻击你，举盾能挡", "It attacks you. Block with your shield")
                : e.k.role == "support" ? Loc.T("先打它，它在帮别的玩具", "Take it out first. It helps the others")
                : e.k.role == "mess" ? Loc.T("它在捣乱，到处洒颜料挡你视线", "It sprays paint everywhere to block your view")
                : Loc.T("别让它把旗插下去", "Stop it before it plants a flag");
            ui.Banner(Loc.T("新玩具：", "New toy: ") + Loc.T(e.k.name, e.k.en), tip, 3f);
            _highlightNext = e.id;
        }

        EnemyView Spawn(Enemy e)
        {
            GameObject pf = e.kind switch
            {
                "brute" => brutePrefab, "mouse" => mousePrefab, "dummy" => targetPrefab,
                "shooter" or "trainer" => trooperPrefab, "tin" => tinPrefab, "minion" => minionPrefab, "commander" => commanderPrefab, "ninja" => ninjaPrefab,
                "boss1" or "boss2" or "boss3" or "boss4" or "boss5" or "boss6" when bossPrefabs != null && bossPrefabs.Length >= e.kind[4] - '0' && bossPrefabs[e.kind[4] - '1'] => bossPrefabs[e.kind[4] - '1'],
                "jack" => jackPrefab, "plane" => planePrefab, "balloon" or "balloonSmall" => balloonPrefab, "rcCar" => carPrefab, "repair" => botPrefab,
                _ => planterPrefab,
            };
            if (!pf) pf = planterPrefab;
            if (!pf) return null;
            var g = Instantiate(pf, enemyRoot);
            g.name = $"{e.kind}_{e.id}";
            if (e.kind == "mouse" && !mousePrefab) g.transform.localScale = Vector3.one * 0.55f;   // 老鼠模型还没好时的替身
            if (e.kind == "balloonSmall") g.transform.localScale = Vector3.one * 0.7f;
            if (e.k.ai == "shooter" || e.k.ai == "trainer") GiveToyGun(g);                      // 会开枪的拿一把小玩具枪（一眼看出它会打你）
            NewKindTip(e);
            var v = g.AddComponent<EnemyView>();
            int pi = e.portal != null ? e.portal.id : 0;
            v.Init(e, worlds != null && pi < worlds.Length ? worlds[pi] : null, ringMat, blobMat);
            if (_highlightNext == e.id) { v.Highlight(4f); _highlightNext = -1; }
            BangAudio.At("voice_jump", g.transform.position, 0.35f, VoicePitch(e));
            return v;
        }

        /// <summary>给射手右手塞一把小号糖果玩具枪（就是玩家那把，缩小）：枪口顺着手臂往外（手垂着时朝下，举枪瞄准时朝前）。在 EnemyView.Init 之前挂上，门后材质切换会一起处理。</summary>
        void GiveToyGun(GameObject enemy)
        {
            var rig = enemy.GetComponent<EnemyRig>(); if (!rig || !rig.armR || !gunPrefab) return;
            var rs = rig.armR.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var gun = Instantiate(gunPrefab, rig.armR); gun.name = "EnemyGun";
            foreach (var mb in gun.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var c in gun.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var armMat = rs[0].sharedMaterial;                                          // 玩家那把是半透明的，敌人的换成手臂同款不透明顶点色材质（描边也能套上）
            foreach (var r in gun.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = armMat;
            rig.holdAim = 0.45f;
            var down = -rig.armR.up;                                                    // 手臂顺着本地 −Y 往下
            var hand = b.center + down * b.extents.y * 0.85f;                           // 手的位置：手臂末端
            gun.transform.rotation = Quaternion.LookRotation(down, rig.armR.forward);
            var gb = gun.GetComponentsInChildren<Renderer>(); var gbb = gb[0].bounds; foreach (var r in gb) gbb.Encapsulate(r.bounds);
            float want = b.size.y * 0.75f;                                              // 枪长 ≈ 手臂长的 3/4
            gun.transform.localScale *= want / Mathf.Max(1e-3f, gbb.size.magnitude * 0.8f);
            gun.transform.position = hand + down * want * 0.25f;
        }

        // ───────── 敌方泡沫弹 ─────────
        void SyncBullets()
        {
            _seen.Clear();
            foreach (var b in Game.bullets)
            {
                if (b.kind == "ball") continue;             // 积木球由 MechPresenter 画
                _seen.Add(b.id);
                if (!_darts.TryGetValue(b.id, out var t) || !t)
                {
                    var bpf = (b.kind == "brick" || b.kind == "shell") && brickPrefab ? brickPrefab : dartPrefab;   // Boss 扔的积木砖 / 炮弹
                    if (!bpf) continue;
                    t = Instantiate(bpf, enemyRoot).transform; _darts[b.id] = t;
                    if (bpf == brickPrefab) t.localScale = Vector3.one * (b.kind == "shell" ? 0.5f : 0.75f);
                    t.localScale = Vector3.one * Random.Range(0.92f, 1.08f);
                    if (b.kind == "grab" || b.kind == "drain")                 // Boss 特技弹：大一号、染色（抢枪 = 紫色磁铁爪，震空弹匣 = 金色发条波），一眼和普通子弹区分开
                    {
                        t.localScale = Vector3.one * 1.8f;
                        var tint = b.kind == "grab" ? new Color(0.72f, 0.45f, 1f) : new Color(1f, 0.78f, 0.25f);
                        var mpb = new MaterialPropertyBlock();
                        foreach (var r in t.GetComponentsInChildren<Renderer>()) { r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", tint); r.SetPropertyBlock(mpb); }
                    }
                    t.position = LogicSpace.ToUnity(b.pos);
                    AddDartTrail(t);
                    BangAudio.At("shot_plop", LogicSpace.ToUnity(b.pos), 0.5f, 1.4f);
                }
                t.position = LogicSpace.ToUnity(b.pos);
                var v = LogicSpace.ToUnity(b.vel);
                // 迎面飞来时只看得到正面，所以让弹身绕飞行方向轻轻画圈摆（像真的软弹那样晃着飞）——尾翼和桃色弹身时隐时现；尾翼导旋慢转
                float ph = b.id * 1.7f, tt = Time.time;
                if (v.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(v) * Quaternion.Euler(9f * Mathf.Sin(tt * 9f + ph), 9f * Mathf.Cos(tt * 9f + ph), tt * 300f + ph * 40f);
            }
            _gone.Clear();
            foreach (var kv in _darts) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                var t = _darts[id]; _darts.Remove(id);
                if (t) { PlayFx.Instance?.Dust(t.position, 0.5f, 4); Destroy(t.gameObject); }
            }
        }

        /// <summary>软弹身后一道桃色蜡笔尾迹（很短，粗→细、渐隐），让飞行路线一眼看得出来。</summary>
        void AddDartTrail(Transform t)
        {
            var fx = PlayFx.Instance; if (!fx || !fx.tracerMat) return;
            var tr = t.gameObject.AddComponent<TrailRenderer>();
            tr.sharedMaterial = fx.tracerMat; tr.time = 0.16f; tr.minVertexDistance = 0.02f; tr.numCapVertices = 3;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, 0.016f), new Keyframe(1, 0.002f));
            tr.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.78f, 0.62f), 0), new GradientColorKey(new Color(1f, 0.62f, 0.74f), 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.85f, 0), new GradientAlphaKey(0f, 1) },
            };
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
            tr.Clear();
        }

        // ───────── 旗子 ─────────
        void SyncFlags(float dt)
        {
            foreach (var f in Game.flags)
            {
                if (!_flags.TryGetValue(f.id, out var v) || !v)
                {
                    var pf = f.type == "big" ? flagBigPrefab : flagSmallPrefab;
                    if (!pf) continue;
                    var g = Instantiate(pf, enemyRoot); g.name = $"Flag_{f.id}";
                    v = g.AddComponent<FlagView>(); v.Init(f, ringMat, blobMat);
                    _flags[f.id] = v;
                }
                v.Sync(dt);
            }
        }

        string _lastShotSide;                                     // 最近开枪的那只手：击杀后的小星星飞回这把枪
        // ───────── fx 事件 ─────────
        void Dispatch(FxEvent ev)
        {
            if (mech && mech.OnFx(ev)) return;
            switch (ev)
            {
                case ShotFx s:
                {
                    _lastShotSide = s.side ?? "right";
                    var dir = LogicSpace.ToUnity(s.dir).normalized;
                    if (_guns.TryGetValue(s.side ?? "right", out var gun))
                    {
                        gun.Fire();
                        var mz = gun.Muzzle;
                        var muzzle = mz.position;
                        bool gold = Game.goldT > 0;
                        fx?.Muzzle(muzzle, mz.forward, mz.up, gold);
                        BangAudio.At("shot_plop", muzzle, 1f);
                        BangAudio.At("shot_boom", muzzle, 0.35f, 1.25f);
                        var to = s.hit && s.to != null ? LogicSpace.ToUnity(s.to) : muzzle + dir * 6f;
                        bool wall = false; RaycastHit hitInfo = default;
                        if (!s.hit && Physics.Raycast(muzzle, dir, out hitInfo, 12f)) { to = hitInfo.point; wall = true; }
                        var col = gold ? new Color(1f, 0.8f, 0.25f) : Crayons[_crayon++ % Crayons.Length];
                        bool hit = s.hit; var hp = to; var hn = hitInfo.normal;
                        fx?.Shot(muzzle, to, col, gold ? 1.8f : 1f, () =>
                        {
                            if (hit) { fx.HitStar(hp); if (gold) { fx.Glitter(hp, col, 10, 1f); fx.Flash(hp, col, 0.25f, 0.25f); } }
                            else if (wall) fx.CrayonMark(hp, hn, col);   // 打空：子弹落到的表面留个蜡笔痕
                        });
                        var wpn = Game.weapons.Get(s.side ?? "right");
                        if (wpn != null && wpn.ammo > 0 && wpn.ammo <= 3) BangAudio.At("ui_hover", muzzle, 0.4f, 1.4f + (3 - wpn.ammo) * 0.18f);   // 快没子弹了：越来越尖的“嘀”
                    }
                    if (s.hit) _shotDir[s.id] = dir;
                    break;
                }
                case HitFx h:
                    if (_enemies.TryGetValue(h.id, out var ev2) && ev2)
                    {
                        ev2.OnHit(ShotDir(h.id), h.hp, h.maxHp);
                        bool guard = Bang.Logic.Game.IsBoss(ev2.data) && ev2.data.bossGuardT > 0 && !h.weak;
                        if (guard) BangAudio.At("hit_metal2", ev2.transform.position, 0.7f, 0.7f);   // 护盾期间：“当”一声闷响（打不疼）
                        else BangAudio.At(HitSound(ev2.data.kind), ev2.transform.position, h.crit ? 1f : 0.8f, (h.crit ? 0.85f : 1f) * ComboPitch());
                        if (h.crit) ToyTime.Slow(0.07f, 0.1f);                                       // 暴击：玩具顿一下（只停玩具，玩家的枪和界面照常）
                        if (h.weak) { var wp = LogicSpace.ToUnity(h.pos); PlayFx.Instance?.HitStar(wp); PlayFx.Instance?.Confetti(wp, 14); }    // 打中弱点：更大的反馈
                    }
                    break;
                case KillFx k:
                    if (_enemies.TryGetValue(k.id, out var ek) && ek)
                    {
                        var p = ek.transform.position;
                        BangAudio.At("kill_pop", p, 1f, ComboPitch()); BangAudio.At("kill_scatter", p, 0.7f);
                        OnKillStreak(p + Vector3.up * (float)ek.data.height * 0.5f, ek.data);
                        if (Game.goldT > 0) fx?.Glitter(p + Vector3.up * (float)ek.data.height * 0.5f, new Color(1f, 0.85f, 0.35f), 18, 1.3f);
                        {   // 击杀：地上一圈冲击环 + 几颗小星星沿弧线飞回开枪的那把枪（“收下了”），玩具顿一下
                            var mid = p + Vector3.up * (float)ek.data.height * 0.5f; float big = Bang.Logic.Game.IsBoss(ek.data) ? 2.4f : 1f;
                            fx?.Ring(p + Vector3.up * 0.04f, new Color(1f, 0.95f, 0.8f, 0.9f), 0.55f * big + (float)ek.data.height * 0.5f, 0.3f * Mathf.Sqrt(big));
                            var kg = _lastShotSide != null && _guns.TryGetValue(_lastShotSide, out var kgun) && kgun ? kgun : null;
                            if (kg) fx?.Trail(mid, () => kg ? kg.Muzzle.position : mid, Crayons[ek.data.id % Crayons.Length], Bang.Logic.Game.IsBoss(ek.data) ? 12 : 4);
                            bool lastOne = !ek.data.bonus && Game.phase == "wave" && Game.run.queue.Count == 0 && !AnyOtherAlive(ek.data);
                            if (!Bang.Logic.Game.IsBoss(ek.data) && !lastOne) ToyTime.Slow(0.05f, 0.15f);   // 最后一只 / Boss 有自己的慢动作，不叠（叠了会更慢）
                        }
                        BangAudio.At("voice_fall", p, 0.55f, VoicePitch(ek.data));
                        if (Bang.Logic.Game.IsBoss(ek.data))
                        {
                            ToyTime.Slow(0.9f, 0.25f);
                            for (int i = 0; i < 3; i++) fx?.Confetti(p + Vector3.up * (0.3f + i * 0.35f), 40);
                            fx?.Flash(p + Vector3.up * 0.6f, new Color(1f, 0.9f, 0.6f), 2.2f, 0.6f); BangAudio.Ui("chapter_clear", 0.9f); BangAudio.At("boss_blast", p, 1f, 0.8f);
                            ui?.Banner(Loc.T("打倒了" + ek.data.k.name + "！", ek.data.k.en + " Down!"), Loc.T("干得漂亮！", "Brilliant!"), 2.6f);
                        }
                        else if (!ek.data.bonus && Game.phase == "wave" && Game.run.queue.Count == 0 && !AnyOtherAlive(ek.data)) ToyTime.Slow(0.45f, 0.3f);   // 这一波最后一只
                        ek.Die(ShotDir(k.id)); _enemies.Remove(k.id);
                    }
                    break;
                case ExitFx x:
                    if (_enemies.TryGetValue(x.id, out var ex) && ex) { ex.Exit(); _enemies.Remove(x.id); }
                    break;
                case ShrinkFx sh:
                    if (_enemies.TryGetValue(sh.id, out var es) && es) { es.Shrink(); BangAudio.At("kill_scatter", es.transform.position, 0.8f, 0.8f); }
                    break;
                case FlagPlantedFx fp:
                {
                    var p = LogicSpace.ToUnity(fp.pos);
                    BangAudio.At("flag_plant", p, 0.9f); BangAudio.At("toy_spread", p, 0.45f);
                    break;
                }
                case FlagHitFx fh:
                    if (_flags.TryGetValue(fh.id, out var fv) && fv) { fv.OnHit(ShotDir(fh.id), fh.hp, fh.maxHp); BangAudio.At("flag_hit", fv.transform.position, 0.8f); }
                    break;
                case FrenzyEndFx fe:
                    ui?.Banner(Loc.T("狂欢结束！", "Frenzy Over!"), Loc.T($"一共扫倒 {fe.count} 只，长枪收起，换回手枪", $"You popped {fe.count}. Back to the pistol"), 2.2f);
                    BangAudio.Ui("chapter_clear", 0.7f);
                    break;
                case FlagDownFx fd:
                    if (fd.byPlayer) { var fp2 = LogicSpace.ToUnity(fd.pos) + Vector3.up * 0.35f; fx?.Flash(fp2, new Color(0.8f, 0.95f, 1f), 0.8f, 0.35f); fx?.Confetti(fp2, 30); fx?.Bo(fp2 + Vector3.up * 0.15f); }   // 打倒旗子：解气
                    if (_flags.TryGetValue(fd.id, out var fdv) && fdv) { fdv.Fall(ShotDir(fd.id)); BangAudio.At("flag_down", fdv.transform.position, 1f); }
                    break;
                case DryFx d: Gun(d.side)?.Dry(); GunSfx(d.side, "dry"); break;
                case JamFx j: GunSfx(j.side, "reload_jam", 0.7f); Gun(j.side)?.JamHint(); break;
                case GoldHitFx gh: GunSfx(gh.side, "reload_gold", 0.8f); GunView.GoldHit(); break;
                case HeadHitFx h:
                    _headHits++;
                    if (_splat)
                    {
                        _splat.Hit(h.kind, LogicSpace.ToUnity(h.dir));
                        if (h.kind == "ball") BangAudio.Ui("hit_plastic", 1f, 0.8f);
                        else if (h.kind == "plane") BangAudio.Ui("hit_plastic", 0.7f, 1.3f);
                        else BangAudio.Ui("hit_plop", 0.9f, h.kind == "confetti" ? 1.25f : 0.8f);
                    }
                    break;
                case KnockedFx: _waveKnocks++; foreach (var g in _guns.Values) { g.Knocked(); BangAudio.At("shield_block", g.transform.position, 0.8f, 1.2f); } break;
                case GunStolenFx gs:
                    _waveKnocks++;
                    foreach (var g in _guns.Values) { g.Stolen((float)gs.dur); BangAudio.At("binoc_on", g.transform.position, 1f, 0.7f); }
                    ui?.Banner(Loc.T("枪被抢走了！", "Gun Stolen!"), Loc.T($"{gs.dur:0} 秒后还回来，下次举盾挡住紫色磁铁爪", $"Back in {gs.dur:0}s. Block the purple claw next time"), 2.2f, true);
                    break;
                case AmmoDrainFx:
                    foreach (var g in _guns.Values) BangAudio.At("dry", g.transform.position, 1f, 0.8f);
                    ui?.Banner(Loc.T("弹匣被震空了！", "Ammo Drained!"), Loc.T("马上换弹；举盾能挡住金色发条波", "Reload now. Shield blocks the golden wave"), 2f, true);
                    break;
                case ReloadFx r when r.type == "reloadStart": GunSfx(r.side, "reload_out"); break;
                case ReloadFx r when r.type == "reloadDone": Gun(r.side)?.ReloadDone(); GunSfx(r.side, "reload_in"); break;
                case BlockFx bf: BangAudio.At("shield_block", LogicSpace.ToUnity(bf.pos), 1f, 1f + (float)(Game.ShieldMax() - bf.dur) * 0.06f); _shield?.Block(); break;
                case ParryFx pf: BangAudio.At("parry", LogicSpace.ToUnity(pf.pos)); BangAudio.At("buff_gold", LogicSpace.ToUnity(pf.pos), 0.5f, 1.3f); _shield?.Parry(); break;
                case ShieldFx sf:
                {
                    var sp = Game.shield.pos != null ? LogicSpace.ToUnity(Game.shield.pos) : (Camera.main ? Camera.main.transform.position : Vector3.zero);
                    if (sf.type == "shieldUp") BangAudio.At("shield_up", sp, 0.7f, 1.1f);
                    else if (sf.type == "shieldBreak") { _shield?.Break(); BangAudio.At("shield_break", sp, 1f); BangAudio.At("kill_scatter", sp, 0.8f, 1.2f); ui?.Banner(Loc.T("盾牌碎了！", "Shield Broken!"), Loc.T($"{Game.cfg.shield.brokenCd:0} 秒后自动修好", $"Repairs itself in {Game.cfg.shield.brokenCd:0} seconds"), 1.8f); }
                    break;
                }
                case LessonStepFx ls: ui?.LessonStep(ls); BangAudio.Ui("ui_confirm", 0.6f); break;
                case LessonDoneFx: UiManager.MarkKnowsGun(); ui?.LessonDone(); ui?.Banner(Loc.T("教学完成！", "Tutorial Complete!"), Loc.T("玩具们要来了", "Here come the toys"), 2.5f); BangAudio.Ui("wave_clear"); break;
                case WaveStartFx w:
                    if (w.wave > 0) LogWave("waveEnd", w.wave - 1);
                    _waveStartT = Time.time; _headHits = 0; _waveShots0 = Game.run.stats.shots; _waveHits0 = Game.run.stats.hits; _waveKills0 = Game.run.stats.kills;
                    if (w.wave == 0 && Game.run.mode == "chapter") StarGoalTip();
                    _waveKnocks = 0;
                    BangAudio.Ui("wave_start", 0.8f);
                    if (Game.run.mode == "endless")
                    {
                        int n = w.wave + 1; bool bw = n % 5 == 0, rw2 = n % 5 == 3;
                        ui?.Banner(Loc.T($"无尽 · 第 {n} 波", $"Endless · Wave {n}"), bw ? Loc.T("Boss 波！", "Boss wave!") : rw2 ? Loc.T("小怪潮！一大群冲过来了", "Toy rush! Here comes a crowd") : Loc.T($"下一个 Boss 在第 {(n / 5 + 1) * 5} 波", $"Next boss on wave {(n / 5 + 1) * 5}"));
                    }
                    else ui?.Banner(w.big ? Loc.T("大波来袭！", "Big Wave!") : Loc.T($"第 {w.wave + 1} 波", $"Wave {w.wave + 1}"), w.big ? Loc.T("最后一波，守住房间！", "Final wave. Hold the room!") : Loc.T($"共 {w.total} 波", $"of {w.total}"));
                    break;
                case EndlessTravelFx tr: StartCoroutine(EndlessTravel(tr.chapter, tr.wave)); break;
                case EndlessOverFx eo: music?.Sting(false); ui?.ShowEndlessOver(eo.waves, eo.best); break;
                case LostFx lf:
                    LogWave("lost", Game.run.wave);
                {
                    music?.Sting(false);
                    int flags = 0, bigs = 0; foreach (var f in Game.flags) if (!f.dying) { flags++; if (f.type == "big") bigs++; }
                    string reason = Loc.En
                        ? $"The toys planted {flags} flag{(flags == 1 ? "" : "s")}" + (bigs > 0 ? $", {bigs} of them big" : "") + (_waveKnocks > 0 ? $", and knocked your gun away {_waveKnocks} time{(_waveKnocks == 1 ? "" : "s")}" : "")
                        : $"玩具们插下了 {flags} 面旗子" + (bigs > 0 ? $"，其中 {bigs} 面是大旗" : "") + (_waveKnocks > 0 ? $"，手枪被打飞 {_waveKnocks} 次" : "");
                    string tip = bigs > 0 ? Loc.T("大旗长得特别快，看到积木胖墩先打它", "Big flags grow fast. Take out Block Brutes first.") :
                                 _waveKnocks >= 2 && Game.profile.unlocked.ContainsKey("shield") && Game.profile.unlocked["shield"] ? Loc.T("射手瞄准你时，左手掌心朝外就能举盾挡住", "When a shooter aims at you, raise your left palm to block.") :
                                 flags >= 4 ? Loc.T("小怪插旗前会停下来几秒，趁这时打它最稳", "Toys pause for a moment before planting. That is your best shot.") :
                                 Loc.T("旗子插下后会越长越大，看到旗子就顺手打掉", "Planted flags keep growing. Knock them down when you see them.");
                    if (!lf.endless) ui?.ShowLost(reason, tip, true);   // 无尽：等 EndlessOverFx 弹“撑过几波”
                    else ui?.Banner(Loc.T("房间被梦境吞没了", "Dreamed Away!"), reason, 3f, true);
                    break;
                }
                case RewardFx rw:
                    LogWave("clear", Game.run.wave);
                    // 终极决战：谢幕后先放结尾故事卡（交代结局、写明“游戏结束”），点继续再进结算；同一局只放一次
                    if (Game.run.mode == "chapter" && Game.run.chapter == CHAPTERS.Count && !_epilogueShown && ui) { _epilogueShown = true; var r0 = rw.reward; ui.ShowEpilogue(() => ui.ShowResult(r0)); }
                    else ui?.ShowResult(rw.reward);
                    break;
                // 敌人动作的声音：从小怪身上传来（3D），玩家不用盯着也能听出“要开枪了 / 要插旗了 / 有东西冲过来”
                case BossWarnFx bw:
                {
                    // Boss 即将登场（用户：要有提示——横幅、音效、场景变色）：缎带横幅 + 低沉的门涌声和倒数滴答 + 那扇门泛起紫金光 + 整个玩具世界慢慢转暗紫 + 提前换 Boss 音乐
                    var bk = Bang.Logic.KINDS.Get(bw.kind);
                    ui?.Banner(Loc.T("Boss 即将登场！", "Boss Incoming!"), Loc.T(bk.name + "要从门里出来了，准备好", bk.en + " is about to come through the door"), (float)bw.dur, true);
                    BangAudio.Ui("boss_gong", 1f, 0.85f); BangAudio.Ui("surge", 0.8f, 0.7f);   // Boss 专属：一声低沉的锣 + 门涌声，背景音乐同时切到 Boss 曲（用户：Boss 来了要有专属音效 BGM）
                    _warnTick = 0f; _warnTicks = 0;
                    mech?.BossGlow(bw.portal, (float)bw.dur);
                    break;
                }
                case BossIntroFx bi:
                    if (_enemies.TryGetValue(bi.id, out var bv) && bv) bv.Highlight(3f);
                    BangAudio.Ui("boss_gong", 0.9f, 1f); BangAudio.Ui("surge", 0.7f, 0.8f);
                    ui?.Banner(Loc.T("Boss 来袭：" + Bang.Logic.KINDS.Get(bi.kind).name, "Boss: " + Bang.Logic.KINDS.Get(bi.kind).en), Loc.T("它出招前会发光，这时打它伤害翻倍", "Hit it while it glows before attacking: double damage"), 3.2f, true);
                    break;
                case BossPhaseFx bp:
                    if (_enemies.TryGetValue(bp.id, out var bpv) && bpv)
                    {
                        var c = bpv.transform.position + Vector3.up * (float)bpv.data.height * 0.6f;
                        fx?.Confetti(c, 36); fx?.Dust(bpv.transform.position, 2.2f, 12); BangAudio.At("boss_blast", c, 1f, 1f); bpv.Highlight(1.2f);
                    }
                    ToyTime.Slow(0.35f, 0.35f);
                    ui?.Banner(Loc.T(bp.phase == 2 ? "它生气了！" : "最后的反扑！", bp.phase == 2 ? "It's angry!" : "Last stand!"), Loc.T("出招更快了。蓝光护盾期间打它不疼，等它发光再打", "Faster attacks. Blue shield: wait for the glow"), 2.4f);
                    break;
                case TelegraphFx tf: EnemySfx(tf.id, "enemy_tele", 0.9f, 1.1f); break;           // 开枪前的预警（举盾 / 先打它的提示）
                case EnemyShotFx esh: EnemySfx(esh.id, "enemy_shot", 0.9f); if (_enemies.TryGetValue(esh.id, out var shv) && shv) shv.Shoot(); break;
                case PlantingFx pl:                                                             // 停下来插旗：趁现在打
                    EnemySfx(pl.id, "plant_warn", 0.8f);
                    if (PlayerPrefs.GetInt(WeakTipKey, 0) == 0) { PlayerPrefs.SetInt(WeakTipKey, 1); ui?.Banner(Loc.T("插旗时是弱点！", "Planting = Weak Spot!"), Loc.T("这时打它，伤害翻倍", "Hit it now for double damage"), 3f); }
                    break;
                case JackOpenFx jo: EnemySfx(jo.id, "jack_open", 1f); break;
                case JackCloseFx jc: EnemySfx(jc.id, "jack_close", 0.8f); break;
                case SummonFx su: EnemySfx(su.id, "summon", 0.8f); break;
                case BossMoveFx bm: if (bm.stage == 1 && bm.move == "summon") EnemySfx(bm.id, "order_bell", 0.9f, 0.8f); break;   // 大将军摇铃点兵（跺地的砸地声由落地事件播）
                case OrderFx od: EnemySfx(od.id, "order_bell", 0.8f); break;
                case CommanderDownFx cd: EnemySfx(cd.id, "commander_down", 1f); break;
                case DiveFx dv: EnemySfx(dv.id, "dive", 0.9f); break;
                case BlinkFx bk: EnemySfx(bk.id, "blink", 0.9f); break;
                case ReflectHitFx rh: EnemySfx(rh.id, "reflect_ping", 1f); break;
                case MarkFx mk: EnemySfx(mk.id, "mark", 0.8f, 1.2f); break;
                case BinocFx bn: BangAudio.Ui(bn.type == "binocOn" ? "binoc_on" : "binoc_off", 0.8f); break;
                case FireBlockedFx fb when fb.why != "binoc": GunSfx(fb.side, "dry", 0.5f); break;   // 换弹中 / 枪被打飞时扣扳机：轻轻“咔”一声，告诉玩家这枪没打出去
                case PhaseFx p:
                    if (p.type == "rest" || p.type == "station")
                    {
                        BangAudio.Ui("wave_clear", 0.8f);
                        var cam = Camera.main ? Camera.main.transform : null;              // 一波清完：面前撒一大把彩纸
                        if (cam) { var cf = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 1.7f + Vector3.up * 0.5f; fx?.Confetti(cf + cam.right * 0.5f, 45); fx?.Confetti(cf - cam.right * 0.5f, 45); }
                    }
                    if (p.type == "clear") { BangAudio.Ui("chapter_clear"); music?.Sting(true); if (Game.run.mode == "chapter" && Game.run.chapter == CHAPTERS.Count) { FinaleShow.Begin(this); Game.cfg.flow.finaleHold = finaleLength; } }   // 终极决战：结算前先演谢幕
                    if (p.type == "rest") ui?.Banner(Loc.T("漂亮！", "Nice!"), Loc.T("下一波马上来", "Next wave coming up"), 2.2f);
                    else if (p.type == "station") ui?.ShowStation(Game.station);
                    else if (p.type == "clear") ui?.Banner(UI.UiManager.LevelLabel(chapter) + Loc.T(" 通关！", " Clear!"), Loc.T("房间保住啦", "The room is safe"), 3.5f);
                    else if (p.type == "retry") ClearViews();
                    break;
            }
        }

        Vector3 ShotDir(int id) => _shotDir.TryGetValue(id, out var d) ? d : (input ? input.GazeWorld.direction : Vector3.forward);
        void EnemySfx(int id, string name, float vol = 1f, float pitch = 1f) { if (_enemies.TryGetValue(id, out var v) && v) BangAudio.At(name, v.transform.position + Vector3.up * 0.2f, vol, pitch); }
        void GunSfx(string side, string name, float vol = 1f) { var g = Gun(side); if (g) BangAudio.At(name, g.transform.position, vol); }
        public Transform GunTransform(string side) => Gun(side) ? Gun(side).transform : null;
        public GunView Gun(string side) => side != null && _guns.TryGetValue(side, out var g) ? g : null;

        // ───────── Boss 氛围：预警时整个玩具世界转暗紫并轻轻起伏，Boss 在场时保持淡淡的紫，打倒后慢慢恢复 ─────────
        static readonly int IdMood = Shader.PropertyToID("_ToyMood");
        float _mood, _warnTick; int _warnTicks;
        float _bright;
        /// <summary>终极决战通关后的明亮时段：谢幕 + 结算 + 自由观赏都算。</summary>
        public bool FinaleBright => Game.run != null && Game.run.mode == "chapter" && Game.run.chapter == CHAPTERS.Count && (Game.phase == "clear" || Game.phase == "reward" || Game.phase == "explore");
        void SyncMood(float dt)
        {
            bool warn = Game.bossWarnT > 0, boss = BossAlive();
            if (FinaleBright)                                                 // 终极决战打完（用户：最后的场景不要再暗下去，明亮起来）：整个玩具世界提亮、偏暖金
            {
                _bright = Mathf.MoveTowards(_bright, 1f, dt * 0.4f); _mood = 0;
                var bc = Color.Lerp(Color.white, new Color(1.2f, 1.14f, 1.02f), _bright);
                Shader.SetGlobalVector(IdMood, new Vector4(bc.r, bc.g, bc.b, 1)); return;
            }
            _bright = 0;
            float target = warn ? 1f : boss ? 0.45f : 0f;
            _mood = Mathf.MoveTowards(_mood, target, dt * (target > _mood ? 0.9f : 0.35f));
            if (warn)
            {
                _warnTick -= dt;
                if (_warnTick <= 0 && _warnTicks < 4) { _warnTick = 1f; BangAudio.Ui("danger_tick", 0.8f, 0.85f + 0.12f * _warnTicks); _warnTicks++; }   // 倒数滴答，越来越高
            }
            float pulse = warn ? 0.06f * Mathf.Sin(Time.time * 4.5f) : 0f;
            float k = Mathf.Clamp01(_mood * 0.85f + pulse);
            if (k < 0.002f) { Shader.SetGlobalVector(IdMood, Vector4.zero); return; }
            var c = Color.Lerp(Color.white, new Color(0.66f, 0.56f, 0.9f), k);   // 暮紫：压暗、偏紫，玩具颜色还认得出
            Shader.SetGlobalVector(IdMood, new Vector4(c.r, c.g, c.b, 1));
        }

        /// <summary>开章时预热这一章会出的 Boss（用户：最后 Boss 出来时有点卡）：在视野里极小地实例化一帧，
        /// 让模型 / 贴图 / 着色器变体都提前上 GPU、描边网格提前算好；Boss 音乐提前解码。</summary>
        System.Collections.IEnumerator PrewarmBosses(int ch)
        {
            var kinds = new HashSet<string>();
            var C = CHAPTERS.Get(ch); if (C == null || bossPrefabs == null) yield break;
            foreach (var w in C.waves) foreach (var (k, _) in w.spawns) if (k.StartsWith("boss")) kinds.Add(k);
            var b0 = Bang.Logic.Game.BossOf(ch); if (b0 != null) kinds.Add(b0);
            var cam = Camera.main; if (!cam || kinds.Count == 0) yield break;
            var holder = new GameObject("BossPrewarm").transform;
            foreach (var k in kinds)
            {
                int idx = k[4] - '1'; if (idx < 0 || idx >= bossPrefabs.Length || !bossPrefabs[idx]) continue;
                var g = Instantiate(bossPrefabs[idx], holder);
                g.transform.position = cam.transform.position + cam.transform.forward * 0.5f; g.transform.localScale = Vector3.one * 0.0005f;
                foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                foreach (var lg in g.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = lg.GetLODs(); if (lods.Length == 0) continue;
                    var low = lods[Mathf.Min(2, lods.Length - 1)].renderers;
                    if (low.Length > 0 && low[0] && low[0].GetComponent<MeshFilter>()) EnemyView.Smoothed(low[0].GetComponent<MeshFilter>().sharedMesh);
                    lg.ForceLOD(0);
                }
                var w0 = worlds != null && worlds.Length > 0 ? worlds[0] : null;   // 门后材质变体也先建好
                if (w0) foreach (var r in g.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) w0.VariantOf(m);
            }
            music?.Prewarm("boss");
            yield return null; yield return null;                         // 渲染两帧
            Destroy(holder.gameObject);
        }

        // ───────── 玩具化、视线锁定、HUD ─────────
        float _toyAge;                                            // 本章门开着、在玩的累计时间（基础蔓延用）
        void SyncToy(float dt)
        {
            var toy = RoomToyification.Instance; if (!toy) return;
            // 用户：开始前天空从门口渐渐玩具化，到一个固定值就不再自己扩张；剩下的扩张只看插了几面旗（每面 100/n %，n 按章 8～12）
            bool playing = Game.phase == "wave" || Game.phase == "lesson" || Game.phase == "rest" || Game.phase == "station";
            bool open = DoorsOpen;
            if (playing && open) _toyAge += dt;
            float baseR = Mathf.Lerp(doorRadiusMin, toyBaseRadius, Mathf.SmoothStep(0, 1, Mathf.Clamp01(_toyAge / 8f)));   // 门开后约 8 秒渗到固定范围
            // 用户：要让玩家看得出天空盒在慢慢变——每过一波，门口那片再往外渗一圈（舌头 / 触须由 RoomToyification 自动长，边缘不规则），只在门口附近，不会铺满
            if (Game.run != null && Game.run.mode != "endless") baseR += waveLeak * Mathf.Min(Game.run.wave, 4);   // 每一关都要看得见（含终极决战），无尽模式没有波次概念
            baseR = Mathf.Min(baseR, toyBaseRadius + 1.2f);
            float k = Mathf.Pow(Mathf.Clamp01((float)(Game.toy / 100.0)), 0.8f);   // 前段长得快（用户：完全感受不到房间被玩具化）：20% 时门口约 4.6 米
            float target = Game.phase == "menu" || !open ? 0 : Mathf.Lerp(baseR, doorRadiusMax, k);   // 门没开（封面 / 开门动画中）不渗出
            if (FinaleBright) target = doorRadiusMax;                         // 终极决战通关：梦境世界从门口铺满整个房间，让玩家站在里面慢慢看
            float sp = target > toy.doorRadius ? toy.growSpeed * (1f + toy.doorRadius * 0.35f) : toy.shrinkSpeed * (1f + toy.doorRadius * 0.2f);   // 半径越大长得越快（边缘线速度不变感）
            toy.doorRadius = Mathf.MoveTowards(toy.doorRadius, target, dt * sp);
            Game.revealRadius = toy.doorRadius;                   // 门口透出门后世界的范围：里面的小怪看得见就打得到
            SyncDoorAnchors(toy);
        }

        // 主门口那片（不规则舌头）跟 0 号门走；每扇门（含 0 号）再各挂一个种子，半径跟门口那片一样：门框四周稳定地往外渗一圈
        readonly List<int> _doorSeeds = new(); int _doorSeedKey = -1;
        void SyncDoorAnchors(RoomToyification toy)
        {
            var doors = _worlds ? _worlds.Doors : null; if (doors == null || doors.Count == 0 || !doors[0]) return;
            if (toy.door) toy.door.position = doors[0].position + Vector3.up * 0.95f;
            int key = _worlds.Loaded * 10 + doors.Count;
            if (key != _doorSeedKey)
            {
                foreach (var id in _doorSeeds) toy.RemoveSeed(id);
                _doorSeeds.Clear();
                for (int i = 0; i < doors.Count; i++) if (doors[i]) _doorSeeds.Add(toy.AddSeed(doors[i].position + Vector3.up * 0.95f, 0));   // 0 号门也挂一个：门口那片的舌头有时全伸到地上，墙上看不出在渗（用户：要让玩家看得出天空盒在变）
                _doorSeedKey = key;
            }
            foreach (var id in _doorSeeds) toy.SetTarget(id, toy.doorRadius);
        }

        void SyncLock()
        {
            if (!_lock) return;
            int? id = Game.gaze?.lock_;
            bool gold = Game.goldT > 0;
            Vector3? at = null; float size = 0.3f;
            if (id != null && _enemies.TryGetValue(id.Value, out var v) && v && v.visible)
            {
                float h = (float)v.data.height * v.transform.lossyScale.y;
                at = v.transform.position + Vector3.up * h * 0.5f;
                bool boss = Game.IsBoss(v.data);
                size = boss ? Mathf.Max(h, (float)v.data.radius * 2.6f) * 1.05f : Mathf.Clamp(h * 1.15f, 0.22f, 0.75f);   // Boss：框按整个身子大小、角落在轮廓外
                _lock.big = boss;
            }
            else if (id != null && _flags.TryGetValue(id.Value, out var f) && f) { _lock.big = false; at = f.transform.position + Vector3.up * 0.3f; size = 0.45f; }
            else if (id != null && mech && mech.TargetPos(id.Value, out var mp, out var ms)) { at = mp; size = ms; }
            if (at.HasValue && id != _lockId) { _lock.Lock(); if (Time.unscaledTime - _lockSfxT > 0.12f) { BangAudio.Ui("ui_hover", 0.18f, 1.25f); _lockSfxT = Time.unscaledTime; } }
            _lockId = at.HasValue ? id : null;
            _lock.Track(at, size, gold);
        }
        float _lockSfxT;

    }
}
