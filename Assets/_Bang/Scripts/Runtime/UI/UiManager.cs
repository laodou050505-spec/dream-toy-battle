// 全部界面（XR 世界空间）：
//   封面：3D 糖果 Logo（正面 nano banana 原图、侧面实心糖果边、光泽扫过）+ 背后柔光 + 环绕星光 + 副标题 + 立体按钮；
//   选关：6 扇“门”卡片（章节缩略图、星级、锁）；设置；臂长校准（头显）；
//   战斗 HUD：钉在门洞上方的玩具化度蜡笔条 + 波次小旗、缎带大字提示、门左侧的手势教学卡；
//   暂停（Esc / 左手掌心朝向自己停一会儿）、休息站、结算（星星逐个弹出）、失败横幅。
// 菜单类界面挂在懒跟随锚点上（不跟头动，转开超过 40° 才慢慢回到正前方）；HUD 固定在门洞上，属于场景的一部分。
// 选择方式：看着按钮（眼动 / 鼠标）+ 捏合，或者直接用手指枪打它（会真的开一枪）。
using System;
using System.Collections.Generic;
using Bang.Logic;
using TMPro;
using UnityEngine;

namespace Bang.Play.UI
{
    public class UiManager : MonoBehaviour
    {
        public UiTheme theme;
        public GameDriver driver;
        public Transform head;

        LazyFollow _menuAnchor, _eyeAnchor;     // _eyeAnchor：Boss 横幅挂在玩家眼前（用户：好几扇门时玩家不一定在看中间那扇）
        Vector3 _bannerHome; bool _bannerAtEye;
        Transform _screen, _hud, _lessonCard, _banner;
        readonly List<UiButton> _buttons = new();
        UiButton _hoverBtn;
        string _state = "";
        float _palmT, _screenT;
        public bool Paused { get; private set; }
        CoverPractice _practice;
        /// <summary>封面练习时手上要变出玩具枪（其他菜单里手就是手，用“看 + 捏”）。</summary>
        // 菜单里比出手指枪才变出玩具枪、开枪 = 点按钮；但玩家还没学会手指枪之前（没练过 / 没打过第 1 章教学），菜单里手就是手，只用“看 + 捏”
        //（用户：主菜单要能用手指捏合选择；玩家还不知道手枪交互时不要只有手枪）。量手臂时手要伸直，不变枪
        public bool GunsInMenu => MenuOpen && _state != "calibrate" && (KnowsGun || (_practice && _practice.Active));   // 校准页没有按钮，手保持原样好量臂长
        float _pauseGazeT;
        public bool KnowsGun => driver.Game.profile.cleared > 0 || PlayerPrefs.GetInt(KnowsGunKey, 0) == 1;
        public const string KnowsGunKey = "bang.knowsGun";
        public static void MarkKnowsGun() { if (PlayerPrefs.GetInt(KnowsGunKey, 0) == 1) return; PlayerPrefs.SetInt(KnowsGunKey, 1); PlayerPrefs.Save(); }
        UiButton _pauseBtn; int _hudWave = -1, _waveTotal; float _flagShown; Vector3 _pipScale = Vector3.one * 0.085f;
        public bool MenuOpen => _state != "hud" && _state != "";

        // HUD 元素
        readonly List<MeshRenderer> _bossTicks = new(); bool _bossBarOn;
        MeshRenderer _meterFill, _meterTrack; TextMeshPro _meterText, _bossMoveText; readonly List<MeshRenderer> _wavePips = new();
        float _meterShown;
        TextMeshPro _bannerMain, _bannerSub; float _bannerT = -1f, _bannerLife; MeshRenderer _bannerIcon;
        MeshRenderer _gestureImg; Vector3 _gestureScale = Vector3.one * 0.2f; string _gestureKind; TextMeshPro _lessonText, _lessonSub; float _lessonT;

        /// <summary>关卡称呼：第 1～12 章写“第 N 章”，终极决战只写名字（用户：最后一关不要写成第 13 关）。</summary>
        /// <summary>终极决战：Boss 血条标签前面带上“n/6 ·”，玩家知道打到第几个 Boss 了（六波一波一个）。</summary>
        string FinalTag(int wave) { var g = driver.Game; return g?.run != null && g.run.mode == "chapter" && g.run.chapter == Bang.Logic.CHAPTERS.Count ? $"{wave + 1}/{WaveCount()} · " : ""; }
        static string EndlessLabel(int best) => best > 0 ? Loc.T($"无尽模式 · 最佳第 {best} 波", $"Endless · Best {best}") : Loc.T("无尽模式", "Endless Mode");
        int WaveCount() { var g = driver.Game; var c = g?.run != null ? Bang.Logic.CHAPTERS.Get(g.run.chapter) : null; return c != null ? c.waves.Length : 5; }
        public static string LevelLabel(int ch) { var c = Bang.Logic.CHAPTERS.Get(ch); return c != null && c.final ? Loc.T(c.name, c.en) : Loc.T($"第 {ch} 章", $"Chapter {ch}"); }
        static string ChapterName(int ch) { var c = Bang.Logic.CHAPTERS.Get(ch); return c == null ? "" : Loc.T(c.name, c.en); }
        static string[] ThemeNames => Loc.En ? new[] { "", "Block Town", "Plastic Barracks", "Tin Castle", "Balloon Party", "Wind-up Works", "Toybox Uprising" }
                                              : new[] { "", "积木小镇", "塑料兵营", "锡兵城堡", "气球派对", "发条工厂", "玩具箱总动员" };
        static Dictionary<string, string> ItemNames => Loc.En
            ? new() { ["pistol"] = "Toy Eagle", ["shield"] = "Candy Shield", ["binoc"] = "Toy Binoculars", ["dual"] = "Dual Wield", ["burst"] = "Foam Burster", ["cork"] = "Cork Cannon" }
            : new() { ["pistol"] = "玩具沙鹰", ["shield"] = "糖果盾牌", ["binoc"] = "玩具望远镜", ["dual"] = "双枪", ["burst"] = "泡沫连发枪", ["cork"] = "软木塞手炮" };

        void Start()
        {
            EnsureInit();
            MRBootstrap.Aligned += OnAligned;
        }

        // GameDriver.Start 可能先于这里执行（直接要求显示封面），所以菜单锚点和 HUD 在第一次用到时就建好
        bool _inited;
        void EnsureInit()
        {
            if (_inited) return; _inited = true;
            UiKit.T = theme;
            if (!head && Camera.main) head = Camera.main.transform;
            _menuAnchor = new GameObject("MenuAnchor").AddComponent<LazyFollow>();
            _menuAnchor.transform.SetParent(transform, false); _menuAnchor.head = head;
            _eyeAnchor = new GameObject("EyeBannerAnchor").AddComponent<LazyFollow>();
            _eyeAnchor.transform.SetParent(transform, false); _eyeAnchor.head = head;
            _eyeAnchor.distance = 1.6f; _eyeAnchor.heightOffset = 0.2f; _eyeAnchor.angle = 22f; _eyeAnchor.speed = 1.6f;   // 视线稍上方，不挡准星和小怪；转头超过 22° 才慢慢跟过去
            BuildHud();
        }

        void OnDestroy() => MRBootstrap.Aligned -= OnAligned;
        void OnAligned() { if (_menuAnchor) _menuAnchor.Snap(); FitStage(); if (_state == "hud") PlaceHud(); }

        // ───────────────────────── 屏幕切换 ─────────────────────────
        GameObject _stageGo; readonly List<UiButton> _stageBtns = new();
        const float TitleDistance = 2.1f, TitleHeight = -0.05f;
        static readonly HashSet<string> StageStates = new() { "title", "settings", "calibrate" };
        static readonly Dictionary<string, (float r, float y)> Bursts = new()
        {
            ["chapters"] = (0.92f, -0.04f), ["settings"] = (0.8f, 0f), ["shop"] = (0.78f, 0f), ["pause"] = (0.64f, 0f), ["lost"] = (0.62f, -0.02f), ["station"] = (0.62f, -0.02f), ["result"] = (0.72f, 0.03f), ["epilogue"] = (0.76f, 0.02f),
        };

        Transform NewScreen(string state, float distance = 1.9f, float height = -0.05f)
        {
            EnsureInit();
            // 封面小剧场在“设置 / 校准”时留着当背景（菜单板浮在它前面），只是台上的按钮先不响应；进游戏、选关时收起
            if (_stageGo && !StageStates.Contains(state)) { Destroy(_stageGo); _stageGo = null; _stageBtns.Clear(); }
            foreach (var b in _stageBtns) if (b) b.interactable = state == "title";
            if (_practice) _practice.ShowSign(state == "title");             // 设置 / 校准板浮在剧场前面时，练习小牌先收起来
            if (_screen) Destroy(_screen.gameObject);
            _buttons.Clear(); _hoverBtn = null;
            _state = state; _screenT = 0;
            // 剧场还在（设置 / 校准当背景）：锚点留在封面的位置（剧场不跟着拉近），菜单板单独往玩家这边提前到台口前沿外面、再抬一点
            //（用户：设置界面被演员挡住——原来锚点一拉近，整座剧场连同台口的演员一起冲到了板子前面）
            var local = Vector3.zero;
            if (_stageGo && state != "title" && StageStates.Contains(state)) { local = new Vector3(0, 0.06f, -0.5f); distance = TitleDistance; height = TitleHeight; }
            _menuAnchor.distance = distance; _menuAnchor.heightOffset = height; _menuAnchor.Snap();
            _screen = UiKit.Node(_menuAnchor.transform, "Screen_" + state, local);
            _screen.localRotation = Quaternion.identity;
            _screen.localScale = Vector3.one * 0.001f;
            if (_hud) _hud.gameObject.SetActive(state == "hud");
            if (!_stageGo && Bursts.TryGetValue(state, out var burst)) UiKit.Burst(_screen, new Vector3(0, burst.y, 0.14f), burst.r);   // 没有剧场当背景的菜单：背后放同款放射圆盘
            return _screen;
        }

        /// <summary>界面是打着打着自动弹出来的（喘口气、结算、失败）：按钮先保护一段时间，防止玩家一枪误打到。</summary>
        void GuardButtons(float t = 1.6f) { foreach (var b in _buttons) if (b) b.armT = t; }
        /// <summary>不可逆的按钮：要按两下。</summary>
        static UiButton Confirm(UiButton b) { b.confirmText = Loc.T("再按一次确认", "Tap again"); return b; }

        UiButton AddButton(Transform parent, string label, Vector3 pos, Vector2 size, Color a, Color b, Texture2D icon, Action click, float ts = 0.05f)
        {
            var btn = UiKit.Button(parent, label, size, a, b, icon, click, ts);
            btn.transform.localPosition = pos;
            _buttons.Add(btn);
            return btn;
        }

        UiButton AddBtn(Transform parent, string label, Vector3 pos, Vector2 size, UiKit.BtnStyle style, Action click, float ts = 0.045f)
        {
            var btn = UiKit.Button(parent, label, size, style, click, ts);
            btn.transform.localPosition = pos;
            _buttons.Add(btn);
            return btn;
        }

        public void ShowTitle()
        {
            var g = driver.Game;
            var s = NewScreen("title", TitleDistance, TitleHeight);
            // 终极决战通关后（用户：标题外面应该是“继续无尽模式”，不要再是“继续终极决战”）：主按钮一律进无尽——
            // 有无尽进度就“继续无尽 · 第 N 波”（从那一波开头重来），没有就“无尽模式 · 最佳第 N 波”。旧的章节进度不再占主按钮（选关里照样能玩）
            bool endlessMain = g.profile.cleared >= Bang.Logic.CHAPTERS.Count && g.profile.unlocked.TryGetValue("endless", out var eu0) && eu0;
            bool endlessResume = endlessMain && g.HasEndlessResume;
            bool hasResume = !endlessMain && g.resume != null && g.resume.mode == "chapter";
            int next = Mathf.Clamp(g.profile.cleared + 1, 1, Bang.Logic.CHAPTERS.Count);
            bool firstTime = g.profile.cleared == 0 && !hasResume;          // 第一次玩：只有开始按钮和设置积木，不让新玩家先面对选关
            System.Action start = () => { if (endlessResume) driver.ContinueEndless(); else if (endlessMain) driver.StartEndless(); else if (hasResume) driver.Continue(); else driver.StartChapter(next); };
            if (theme.titleStage)
            {
                // 封面 = 房间里的一座玩具小剧场：跑马灯招牌 + 圆舞台 + 街机大按钮 + 字母积木（不是浮空卡片）
                bool fresh = !_stageGo;
                if (fresh)
                {
                    _stageGo = Instantiate(theme.titleStage, _menuAnchor.transform); _stageGo.transform.localPosition = new Vector3(0, 0.02f, 0); _stageGo.transform.localRotation = Quaternion.identity;
                    _stageGo.transform.localScale = Vector3.one * 0.001f;
                    _stageGo.AddComponent<UiPopIn>();
                }
                var st = _stageGo;
                FitStage();
                // 封面所有独立物件一视同仁加描边（深梅紫，和标题底板同色）：挂在根节点，由 StickerOutline 的跳过表排除自带轮廓的部分
                if (theme.stickerOutlineMat && !st.GetComponent<StickerOutline>()) st.AddComponent<StickerOutline>().mat = theme.stickerOutlineMat;
                var fx = st.GetComponent<TitleStageFx>(); if (!fx) fx = st.AddComponent<TitleStageFx>();          // 不能用 ??：编辑器里取不到组件会返回“假 null”
                _practice = st.GetComponent<CoverPractice>();
                _stageBtns.Clear();
                var bs = Deep(st.transform, "BtnStart");
                var btn = bs.GetComponent<UiButton>(); if (!btn) btn = bs.gameObject.AddComponent<UiButton>();
                btn.onClick = () => { foreach (var b in _stageBtns) if (b) b.interactable = false; fx.Close(start); };   // 先合幕、剧场收起，再开门
                btn.hoverTarget = bs.Find("Dome"); btn.hoverMove = new Vector3(0, 0, -0.005f); btn.pressMove = new Vector3(0, 0, 0.016f); btn.hoverScale = 0.03f; btn.interactable = true;
                _stageBtns.Add(btn); fx.start = btn;
                var lab = Deep(st.transform, "Plaque/Label")?.GetComponent<TextMeshPro>();
                if (lab) { lab.text = endlessResume ? Loc.T($"继续无尽 · 第 {g.resume.wave + 1} 波", $"Endless · Wave {g.resume.wave + 1}") : endlessMain ? EndlessLabel(g.profile.endlessBest) : hasResume ? (Loc.En ? "Continue" : "继续 " + LevelLabel(g.resume.chapter)) : g.profile.cleared == 0 ? Loc.T("开始游戏", "Start") : LevelLabel(next); FitLabel(lab); }
                // 台口名牌：Block_2 玩法介绍、Block_0 选关 | 开始游戏 | Block_1 设置、Block_3 退出游戏（语言切换只在设置里）
                for (int i = 0; i < 4; i++)
                {
                    var blk = Deep(st.transform, "Block_" + i); if (!blk) continue;
                    blk.gameObject.SetActive(!(i == 0 && firstTime));
                    if (!blk.gameObject.activeSelf) continue;
                    if (i == 2 && firstTime && fresh)                       // 第一次玩没有“选关”：玩法介绍挪到选关的位置（紧挨开始按钮，左右仍然对称）
                    {
                        var b0 = Deep(st.transform, "Block_0"); var b3 = Deep(st.transform, "Block_3");
                        if (b0) { blk.localPosition = b0.localPosition; blk.localRotation = b0.localRotation; }
                        var b1 = Deep(st.transform, "Block_1"); if (b3 && b1) { b3.gameObject.SetActive(false); }   // 右边只留“设置”（和左边一块对称）
                    }
                    if (i == 3 && firstTime) { blk.gameObject.SetActive(false); continue; }
                    var bb = blk.GetComponent<UiButton>(); if (!bb) bb = blk.gameObject.AddComponent<UiButton>();
                    bb.onClick = i switch
                    {
                        0 => ShowChapters,
                        1 => ShowSettings,
                        2 => () => { if (_practice) _practice.Begin(); },
                        _ => () => QuitPressed(Deep(st.transform, "Block_3/Label")?.GetComponent<TextMeshPro>()),   // 退出：要连打两下（菜单里开枪就是点按钮，防止一枪误退）
                    };
                    bb.hoverTarget = blk; bb.hoverMove = new Vector3(0, 0, -0.012f); bb.pressMove = new Vector3(0, 0, 0.008f); bb.hoverScale = 0.08f; bb.interactable = true;   // 台口名牌：看着时往外浮一点、按下压回去
                    _stageBtns.Add(bb);
                }
                if (fresh) { fx.Init(driver.mousePrefab); LocalizeStage(st.transform); _practice = st.AddComponent<CoverPractice>(); _practice.Init(driver, theme, fx); }
                if (_practice) _practice.ShowSign(true);
            }
            else AddBtn(s, Loc.T("开始游戏", "Start"), new Vector3(0, -0.1f, -0.02f), new Vector2(0.5f, 0.115f), UiKit.BtnStyle.Primary, start, 0.046f);
            Hint(s, new Vector3(0, -0.62f, -0.2f));
            BangAudio.Ui("chapter_clear", 0.35f);
        }

        /// <summary>英文版：封面舞台上预制体里印的中文换成英文（积木块上的“选关 / 设置”、招牌标题字换英文 3D 字、招牌下的英文小字去掉）。</summary>
        void LocalizeStage(Transform st)
        {
            if (!Loc.En) return;
            var plaque = Deep(st, "Plaque/Label")?.GetComponent<TextMeshPro>();
            if (plaque && theme.fontEn) plaque.font = theme.fontEn;
            string[] blocks = { "Levels", "Options", "How to Play", "Quit" };
            for (int i = 0; i < blocks.Length; i++)
            {
                var l = Deep(st, $"Block_{i}/Label")?.GetComponent<TextMeshPro>();
                if (l) { if (theme.fontEn) l.font = theme.fontEn; l.text = blocks[i]; FitLabel(l); }
            }
            var sign = Deep(st, "Sign");
            if (sign)
            {
                foreach (Transform c in sign) if (c.name.StartsWith("Text_")) c.gameObject.SetActive(false);
                var logo = sign.Find("Logo"); var logoEn = sign.Find("Logo_EN");
                if (logo && logoEn)
                {
                    logo.gameObject.SetActive(false); logoEn.gameObject.SetActive(true);                                  // 剧场：中英文错位标题都搭好了，切换显示
                    var ms = sign.Find("LogoMouse");                                                                       // 老鼠站到英文标题最高的字母 A 上
                    if (ms) { var on = new Vector3(-0.1f, 0.285f, 0.05f); ms.localPosition = on; st.GetComponent<TitleStageFx>()?.MoveFloat(ms, on); }
                }
                else if (logo && theme.logoPrefabEn)
                {
                    var en = Instantiate(theme.logoPrefabEn, sign);
                    en.name = "Logo_EN"; en.transform.localRotation = logo.localRotation; en.transform.localScale = logo.localScale;
                    // 英文字标两行、比中文高：下面那行副标题去掉后，让字标往下盖住副标题那条底带（不留一条空带），高度到中文字标的 1.62 倍、宽度不超过中文字标
                    Bounds B(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
                    var bc = B(logo); en.transform.localPosition = logo.localPosition; var be = B(en.transform);
                    float k = Mathf.Min(bc.size.y * 1.62f / Mathf.Max(1e-4f, be.size.y), bc.size.x * 1.04f / Mathf.Max(1e-4f, be.size.x));
                    en.transform.localScale = logo.localScale * k;
                    en.transform.position += (bc.center + Vector3.down * bc.size.y * 0.36f) - B(en.transform).center;
                    foreach (var r in en.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = theme.logoMatEn ? theme.logoMatEn : theme.logoMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                    logo.gameObject.SetActive(false);
                }
            }
        }

        float _quitArmedAt = -99f; Coroutine _quitReset;
        /// <summary>“退出游戏”：第一下牌子变成“再打一下退出”，3 秒内再打一下才退出；3 秒没打就恢复原样。</summary>
        void QuitPressed(TextMeshPro lab)
        {
            if (Time.unscaledTime - _quitArmedAt < 3f) { Application.Quit(); return; }
            _quitArmedAt = Time.unscaledTime;
            if (!lab) return;
            lab.text = Loc.T("再打一下退出", "Shoot again to quit"); FitLabel(lab);
            if (_quitReset != null) StopCoroutine(_quitReset);
            _quitReset = StartCoroutine(QuitReset(lab));
        }

        System.Collections.IEnumerator QuitReset(TextMeshPro lab)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (lab) { lab.text = Loc.T("退出游戏", "Quit"); FitLabel(lab); }
            _quitArmedAt = -99f; _quitReset = null;
        }

        /// <summary>封面预制体里按路径找部件（剧场的部件挂在 Theater 下面）。</summary>
        static Transform Deep(Transform root, string path) { var t = root.Find(path); if (t) return t; t = root.Find("Theater/" + path); return t; }

        /// <summary>英文比中文长：标签自动缩小到放得下（不超过原字号）。</summary>
        static void FitLabel(TextMeshPro l)
        {
            if (!l.enableAutoSizing) { l.fontSizeMax = l.fontSize; l.fontSizeMin = l.fontSize * 0.45f; l.enableAutoSizing = true; }
            l.enableWordWrapping = false;
            float m = l.rectTransform.rect.width * 0.12f; l.margin = new Vector4(m, 0, m, 0);       // 两头留白：木牌两侧有装饰圆点
        }

        /// <summary>切换语言后：封面小剧场（中文 / 英文字标）、HUD、教学卡全部按新语言重建，回到设置页。</summary>
        void RebuildForLanguage(bool backToSettings = true)
        {
            if (_stageGo) { Destroy(_stageGo); _stageGo = null; _stageBtns.Clear(); }
            if (_hud) Destroy(_hud.gameObject);
            if (_lessonCard) Destroy(_lessonCard.gameObject);
            BuildHud();
            ShowTitle();
            if (backToSettings) ShowSettings();
            if (_stageGo) { var pop = _stageGo.GetComponent<UiPopIn>(); if (pop) Destroy(pop); _stageGo.transform.localScale = Vector3.one; }      // 重建时不再播弹出动画
        }

        void Hint(Transform s, Vector3 p)
        {
            var h = UiKit.Node(s, "Hint", p);
            bool xr = driver.input is Input.PicoInput;
            string tip = !xr ? Loc.T("鼠标移到按钮上点击", "Hover a button and click")
                : KnowsGun ? Loc.T("看着按钮捏一下手指，或者用手指枪打它", "Look at a button and pinch — or shoot it with your finger gun")
                : Loc.T("看着按钮，拇指和食指捏一下", "Look at a button and pinch your thumb and index finger");
            var t = UiKit.Text(h, tip, 0.026f, new Color(0.36f, 0.23f, 0.44f, 0.6f), 0.56f, TextAlignmentOptions.Center, 0);
            var pin = theme.Gesture("pinch");
            if (xr && pin) { var gi = UiKit.Image(h, pin, 0.05f); gi.transform.localPosition = new Vector3(-0.2f, 0.004f, 0); t.transform.localPosition = new Vector3(0.03f, 0, 0); }
        }

        /// <summary>选关第一层：6 个主题（每个两章）+ 终极决战。卡片右下角是这个主题拿到的星星（满 6 颗）。</summary>
        public void ShowChapters()
        {
            var g = driver.Game;
            var s = NewScreen("chapters", 1.8f, -0.08f);
            var tp = UiKit.Board(s, new Vector2(0.44f, 0.11f)); tp.transform.parent.localPosition = new Vector3(0, 0.46f, 0.012f);   // 移动整块（面板 + 描边）
            var title = UiKit.Text(s, Loc.T("选择章节", "Chapters"), 0.056f, UiKit.BoardInk, 1f, TextAlignmentOptions.Center, 0); title.transform.localPosition = new Vector3(0, 0.455f, -0.004f);
            for (int t = 1; t <= 6; t++)
            {
                int tc = t, first = t * 2 - 1;
                bool open = first == 1 || g.profile.cleared >= first - 1;
                int stars = 0; for (int c = first; c <= first + 1; c++) stars += g.profile.stars.TryGetValue(c, out var sv) ? sv : 0;
                var cell = UiKit.Node(s, "Theme" + t, new Vector3(((t - 1) % 3 - 1) * 0.42f, (t <= 3 ? 0.22f : -0.15f), 0));
                UiKit.Board(cell, new Vector2(0.38f, 0.34f));
                var thumb = t - 1 < theme.chapterThumbs.Length ? theme.chapterThumbs[t - 1] : null;
                if (thumb)
                {
                    var im = UiKit.Image(cell, thumb, 0.2f, open ? Color.white : new Color(0.55f, 0.5f, 0.62f, 1f)); im.transform.localPosition = new Vector3(0, 0.05f, -0.002f);
                    float w = 0.34f; im.transform.localScale = new Vector3(w, w * 0.5f, 1);
                }
                var name = UiKit.Text(cell, ThemeNames[t], 0.038f, UiKit.BoardInk, 0.36f); name.transform.localPosition = new Vector3(0, -0.085f, -0.003f); name.outlineWidth = 0;
                var st = UiKit.Text(cell, $"★ {stars}/6", 0.03f, new Color(0.85f, 0.6f, 0.15f), 0.3f); st.transform.localPosition = new Vector3(0, -0.13f, -0.003f); st.outlineWidth = 0;
                if (open)
                {
                    var cb = AddButton(s, "", cell.localPosition + new Vector3(0, 0, -0.01f), new Vector2(0.38f, 0.34f), Color.white, Color.white, null, () => ShowThemeChapters(tc));
                    foreach (var r in cb.GetComponentsInChildren<Renderer>()) r.enabled = false;           // 整张卡片就是按钮（看不见的碰撞体）
                    cb.hoverTarget = cell; cb.faceR = null;
                }
                else { var lk = UiKit.Image(cell, theme.Icon("lock"), 0.1f); lk.transform.localPosition = new Vector3(0, 0.05f, -0.006f); }
            }
            // 终极决战：12 章都打完才开
            {
                bool open = g.profile.cleared >= 12;
                int stars = g.profile.stars.TryGetValue(13, out var sv) ? sv : 0;
                var cell = UiKit.Node(s, "Final", new Vector3(-0.08f, -0.43f, 0));
                UiKit.Board(cell, new Vector2(0.5f, 0.14f));
                var name = UiKit.Text(cell, LevelLabel(Bang.Logic.CHAPTERS.Count), 0.042f, UiKit.BoardInk, 0.6f); name.transform.localPosition = new Vector3(0, 0.012f, -0.003f); name.outlineWidth = 0;
                if (!open) { var lk = UiKit.Image(cell, theme.Icon("lock"), 0.05f); lk.transform.localPosition = new Vector3(-0.21f, 0, -0.006f); }
                var sub = UiKit.Text(cell, open ? Loc.T($"六波，六个 Boss   ★ {stars}/3", $"6 waves, 6 bosses   ★ {stars}/3") : Loc.T("打完前 12 章解锁", "Clear all 12 chapters to unlock"), 0.026f, UiKit.BoardInk, 0.46f); sub.transform.localPosition = new Vector3(0, -0.035f, -0.003f); sub.outlineWidth = 0;
                if (open)
                {
                    var cb = AddButton(s, "", cell.localPosition + new Vector3(0, 0, -0.01f), new Vector2(0.5f, 0.14f), Color.white, Color.white, null, () => driver.StartChapter(13));
                    foreach (var r in cb.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    cb.hoverTarget = cell; cb.faceR = null;
                }
            }
            // 无尽模式：终极决战右边，通关终极决战后才开，卡上写最佳纪录
            {
                bool open = g.profile.unlocked.TryGetValue("endless", out var eu) && eu;
                var cell = UiKit.Node(s, "Endless", new Vector3(0.4f, -0.43f, 0));
                UiKit.Board(cell, new Vector2(0.38f, 0.14f));
                var name = UiKit.Text(cell, Loc.T("无尽模式", "Endless"), 0.042f, UiKit.BoardInk, 0.34f); name.transform.localPosition = new Vector3(0, 0.012f, -0.003f); name.outlineWidth = 0;
                if (!open) { var lk = UiKit.Image(cell, theme.Icon("lock"), 0.05f); lk.transform.localPosition = new Vector3(-0.15f, 0, -0.006f); }
                string subT = !open ? Loc.T("通关终极决战解锁", "Beat the Finale to unlock")
                    : g.HasEndlessResume ? Loc.T($"继续：第 {g.resume.wave + 1} 波", $"Continue: wave {g.resume.wave + 1}")
                    : g.profile.endlessBest > 0 ? Loc.T($"最佳：第 {g.profile.endlessBest} 波", $"Best: wave {g.profile.endlessBest}") : Loc.T("每 5 波一个 Boss", "A boss every 5 waves");
                var sub = UiKit.Text(cell, subT, 0.026f, UiKit.BoardInk, 0.34f); sub.transform.localPosition = new Vector3(0, -0.035f, -0.003f); sub.outlineWidth = 0;
                if (open)
                {
                    var cb = AddButton(s, "", cell.localPosition + new Vector3(0, 0, -0.01f), new Vector2(0.38f, 0.14f), Color.white, Color.white, null, () => { if (g.HasEndlessResume) driver.ContinueEndless(); else driver.StartEndless(); });
                    foreach (var r in cb.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    cb.hoverTarget = cell; cb.faceR = null;
                }
            }
            AddButton(s, Loc.T("返回", "Back"), new Vector3(-0.56f, -0.43f, -0.1f), new Vector2(0.2f, 0.1f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), ShowTitle, 0.044f);
        }

        /// <summary>选关第二层：一个主题的上、下两章。</summary>
        public void ShowThemeChapters(int t)
        {
            var g = driver.Game;
            var s = NewScreen("chapters", 1.8f, -0.08f);
            var tp = UiKit.Board(s, new Vector2(0.6f, 0.11f)); tp.transform.parent.localPosition = new Vector3(0, 0.36f, 0.012f);
            var title = UiKit.Text(s, ThemeNames[t], 0.056f, UiKit.BoardInk, 1f, TextAlignmentOptions.Center, 0); title.transform.localPosition = new Vector3(0, 0.355f, -0.004f);
            for (int k = 0; k < 2; k++)
            {
                int ch = t * 2 - 1 + k;
                bool open = ch == 1 || g.profile.cleared >= ch - 1;
                int stars = g.profile.stars.TryGetValue(ch, out var sv) ? sv : 0;
                var cell = UiKit.Node(s, "Ch" + ch, new Vector3((k - 0.5f) * 0.5f, 0.02f, 0));
                UiKit.Board(cell, new Vector2(0.46f, 0.46f));
                var thumb = t - 1 < theme.chapterThumbs.Length ? theme.chapterThumbs[t - 1] : null;
                if (thumb)
                {
                    var im = UiKit.Image(cell, thumb, 0.2f, open ? Color.white : new Color(0.55f, 0.5f, 0.62f, 1f)); im.transform.localPosition = new Vector3(0, 0.08f, -0.002f);
                    float w = 0.42f; im.transform.localScale = new Vector3(w, w * 0.56f, 1);
                }
                var name = UiKit.Text(cell, LevelLabel(ch), 0.04f, UiKit.BoardInk, 0.42f); name.transform.localPosition = new Vector3(0, -0.09f, -0.003f); name.outlineWidth = 0;
                var sub = UiKit.Text(cell, k == 1 ? Loc.T("章末 Boss：" + Bang.Logic.KINDS.Get("boss" + t).name, "Boss: " + Bang.Logic.KINDS.Get("boss" + t).en) : Loc.T("新玩具登场", "New toys"), 0.028f, k == 1 ? new Color(0.78f, 0.28f, 0.36f) : UiKit.BoardInk, 0.42f);
                sub.transform.localPosition = new Vector3(0, -0.135f, -0.003f); sub.outlineWidth = 0;
                for (int q = 0; q < 3; q++) { var star = UiKit.Image(cell, theme.Icon(q < stars ? "star" : "star_empty"), 0.04f); star.transform.localPosition = new Vector3((q - 1) * 0.05f, -0.185f, -0.003f); }
                if (open)
                {
                    int c2 = ch;
                    var cb = AddButton(s, "", cell.localPosition + new Vector3(0, 0, -0.01f), new Vector2(0.46f, 0.46f), Color.white, Color.white, null, () => driver.StartChapter(c2));
                    foreach (var r in cb.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    cb.hoverTarget = cell; cb.faceR = null;
                }
                else { var lk = UiKit.Image(cell, theme.Icon("lock"), 0.1f); lk.transform.localPosition = new Vector3(0, 0.08f, -0.006f); }
            }
            AddButton(s, Loc.T("返回", "Back"), new Vector3(0, -0.36f, -0.1f), new Vector2(0.3f, 0.1f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), ShowChapters, 0.05f);
        }

        public void ShowSettings()
        {
            var s = NewScreen("settings", 1.7f, -0.1f);
            UiKit.Board(s, new Vector2(1.08f, 0.74f));
            var t = UiKit.Text(s, Loc.T("设置", "Settings"), 0.065f, UiKit.BoardInk, 0.6f); t.transform.localPosition = new Vector3(0, 0.29f, -0.003f); t.outlineWidth = 0;
            var au = BangAudio.Instance; var mu = BangMusic.Instance;
            string Vol() => au ? (au.master > 0.8f ? Loc.T("音效：大", "Sound: High") : au.master > 0.3f ? Loc.T("音效：小", "Sound: Low") : Loc.T("音效：关", "Sound: Off")) : Loc.T("音效", "Sound");
            string Mus() => mu ? (mu.volume > 0.3f ? Loc.T("音乐：大", "Music: High") : mu.volume > 0.05f ? Loc.T("音乐：小", "Music: Low") : Loc.T("音乐：关", "Music: Off")) : Loc.T("音乐", "Music");
            var sz = new Vector2(0.46f, 0.085f); float lx = -0.25f, rx = 0.25f;
            // 左列：声音、语言、校准
            AddButton(s, Vol(), new Vector3(lx, 0.19f, -0.01f), sz, UiTheme.Mint, UiTheme.MintB, theme.Icon("sound"), () =>
            {
                if (au) { au.master = au.master > 0.8f ? 0.45f : au.master > 0.3f ? 0f : 0.9f; PlayerPrefs.SetFloat("bang.sfx", au.master); }
                ShowSettings();
            }, 0.038f);
            AddButton(s, Mus(), new Vector3(lx, 0.09f, -0.01f), sz, UiTheme.Mint, UiTheme.MintB, theme.Icon("music"), () =>
            {
                if (mu) mu.SetVolume(mu.volume > 0.3f ? 0.18f : mu.volume > 0.05f ? 0f : 0.38f);
                ShowSettings();
            }, 0.038f);
            // 语言：中文 ⇄ English（两种语言都写出来，看不懂当前语言的人也认得出这个按钮）
            AddButton(s, Loc.En ? "Language: English  ·  语言" : "语言：中文  ·  Language", new Vector3(lx, -0.01f, -0.01f), sz, UiTheme.Mint, UiTheme.MintB, theme.Icon("language"), () =>
            {
                driver.SetLanguage(!Loc.En);
                RebuildForLanguage();
            }, 0.034f);
            bool xr = driver.input is Input.PicoInput;
            AddButton(s, xr ? Loc.T("重新校准手臂长度", "Recalibrate Arm Length") : Loc.T("手臂校准（头显）", "Arm Length (VR)"), new Vector3(lx, -0.11f, -0.01f), sz, UiTheme.Lav, UiTheme.LavB, theme.Icon("armlen"), () => { if (xr) ShowCalibrate(); }, 0.034f);
            // 右列：玩法舒适度
            int seated = PlayerPrefs.GetInt("bang.seated", 0), sens = PlayerPrefs.GetInt("bang.sens", 1), slow = PlayerPrefs.GetInt("bang.slowmo", 1);
            float splat = PlayerPrefs.GetFloat("bang.splat", 1f);
            AddButton(s, seated == 1 ? Loc.T("姿势：坐着玩", "Play: Seated") : Loc.T("姿势：站着玩", "Play: Standing"), new Vector3(rx, 0.19f, -0.01f), sz, UiTheme.Peach, UiTheme.PeachB, theme.Icon(seated == 1 ? "seated" : "standing"), () =>
            {
                PlayerPrefs.SetInt("bang.seated", 1 - seated); ShowSettings();
            }, 0.036f);
            AddButton(s, sens == 0 ? Loc.T("手势：宽松", "Gestures: Relaxed") : sens == 2 ? Loc.T("手势：严格", "Gestures: Strict") : Loc.T("手势：标准", "Gestures: Normal"), new Vector3(rx, 0.09f, -0.01f), sz, UiTheme.Peach, UiTheme.PeachB, theme.Icon("gesture"), () =>
            {
                int n = (sens + 1) % 3; PlayerPrefs.SetInt("bang.sens", n); Bang.Logic.Config.Default.SetSensitivity(n); ShowSettings();
            }, 0.036f);
            AddButton(s, splat > 0.9f ? Loc.T("被打中遮挡：正常", "Hit Splats: Normal") : splat > 0.1f ? Loc.T("被打中遮挡：弱", "Hit Splats: Light") : Loc.T("被打中遮挡：关", "Hit Splats: Off"), new Vector3(rx, -0.01f, -0.01f), sz, UiTheme.Peach, UiTheme.PeachB, theme.Icon("splat"), () =>
            {
                PlayerPrefs.SetFloat("bang.splat", splat > 0.9f ? 0.5f : splat > 0.1f ? 0f : 1f); ShowSettings();
            }, 0.034f);
            AddButton(s, slow == 1 ? Loc.T("慢动作：开", "Slow-mo: On") : Loc.T("慢动作：关", "Slow-mo: Off"), new Vector3(rx, -0.11f, -0.01f), sz, UiTheme.Peach, UiTheme.PeachB, theme.Icon("slowmo"), () =>
            {
                PlayerPrefs.SetInt("bang.slowmo", 1 - slow); ShowSettings();
            }, 0.036f);
            var note = UiKit.Text(s, Loc.T("坐着玩时门会集中在正前方，不用大幅转头（下一章开始生效）", "Seated mode keeps doors in front of you (applies from the next chapter)"), 0.024f, UiKit.BoardInk, 0.96f);
            note.transform.localPosition = new Vector3(0, -0.185f, -0.003f); note.outlineWidth = 0;
            // 重置强化：改装等级清零、金币原数退还（用户：买了强化以后难度一直是降低的）；不可逆，按两下确认
            int refund = driver.Game.UpgradeRefund();
            var rb = AddButton(s, refund > 0 ? Loc.T($"重置强化（退 {refund} 金币）", $"Reset Upgrades (+{refund} coins)") : Loc.T("重置强化（未购买）", "Reset Upgrades (none)"),
                new Vector3(-0.2f, -0.27f, -0.01f), new Vector2(0.4f, 0.075f), UiTheme.Lav, UiTheme.LavB, theme.Icon("retry"), () =>
                {
                    int r = driver.Game.ResetUpgrades();
                    if (r > 0) { BangAudio.Ui("ui_confirm"); PlayFx.Instance?.Confetti(s.position, 16); }
                    ShowSettings();
                }, 0.032f);
            rb.interactable = refund > 0;
            if (refund > 0) Confirm(rb);
            AddButton(s, Loc.T("返回", "Back"), new Vector3(0.25f, -0.27f, -0.01f), new Vector2(0.26f, 0.075f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), ShowTitle, 0.04f);
        }

        // ───────── 改装台：用打玩具攒的金币升级枪和盾（结算页、喘口气时都能进）─────────
        static readonly (string key, string zh, string en, string[] effZh, string[] effEn)[] Upgrades =
        {
            ("mag", "弹匣扩容", "Bigger Mag", new[] { "+2 发", "+4 发", "+6 发" }, new[] { "+2 shots", "+4 shots", "+6 shots" }),
            ("reload", "换弹更快", "Quick Reload", new[] { "快 10%", "快 20%", "快 30%" }, new[] { "10% faster", "20% faster", "30% faster" }),
            ("gold", "金区加宽", "Wider Gold Zone", new[] { "金区 +5%", "金区 +10%", "金区 +15%" }, new[] { "+5% zone", "+10% zone", "+15% zone" }),
            ("shield", "盾牌加厚", "Tougher Shield", new[] { "多挡 1 下", "多挡 2 下" }, new[] { "+1 block", "+2 blocks" }),
            ("parry", "弹反窗口", "Parry Window", new[] { "+0.05 秒", "+0.1 秒" }, new[] { "+0.05 s", "+0.1 s" }),
        };
        System.Action _shopBack;

        public void ShowShop(System.Action back)
        {
            _shopBack = back;
            var g = driver.Game; var pf = g.profile;
            var s = NewScreen("shop", 1.7f, -0.08f);
            UiKit.Board(s, new Vector2(0.98f, 0.78f));
            var t = UiKit.Text(s, Loc.T("改装台", "Workshop"), 0.065f, UiKit.BoardInk, 0.6f); t.transform.localPosition = new Vector3(0, 0.31f, -0.003f); t.outlineWidth = 0;
            var coins = UiKit.Text(s, Loc.T($"金币 {pf.coins}", $"Coins {pf.coins}"), 0.04f, new Color(0.75f, 0.5f, 0.1f), 0.5f); coins.transform.localPosition = new Vector3(0, 0.245f, -0.003f); coins.outlineWidth = 0;
            for (int i = 0; i < Upgrades.Length; i++)
            {
                var u = Upgrades[i]; var def = Bang.Logic.UPGRADES.Get(u.key);
                int lv = pf.upgrades.ContainsKey(u.key) ? pf.upgrades[u.key] : 0, max = def.costs.Length;
                float y = 0.15f - i * 0.085f;
                var name = UiKit.Text(s, Loc.T(u.zh, u.en), 0.038f, UiKit.BoardInk, 0.24f, TextAlignmentOptions.Left); name.transform.localPosition = new Vector3(-0.27f, y, -0.003f); name.outlineWidth = 0;
                var pp = UiKit.Text(s, Loc.T($"{lv}/{max} 级", $"Lv {lv}/{max}"), 0.03f, new Color(0.85f, 0.42f, 0.52f), 0.12f); pp.transform.localPosition = new Vector3(-0.06f, y, -0.003f); pp.outlineWidth = 0;
                string eff = lv < max ? Loc.T("下一级 " + u.effZh[lv], "Next " + u.effEn[lv]) : Loc.T("已满级", "Maxed");
                var ef = UiKit.Text(s, eff, 0.031f, UiKit.BoardInk, 0.24f, TextAlignmentOptions.Left); ef.transform.localPosition = new Vector3(0.13f, y, -0.003f); ef.outlineWidth = 0;
                if (lv < max)
                {
                    int cost = def.costs[lv]; bool can = pf.coins >= cost; string key = u.key;
                    var b = AddButton(s, Loc.T($"{cost} 金币", $"{cost} coins"), new Vector3(0.36f, y, -0.01f), new Vector2(0.17f, 0.07f), can ? UiTheme.Peach : UiTheme.Cream, can ? UiTheme.PeachB : UiTheme.CreamB, theme.Icon("coin"), () =>
                    {
                        if (g.BuyUpgrade(key)) { BangAudio.Ui("buff_gold", 0.9f); PlayFx.Instance?.Glitter(_screen.position, new Color(1f, 0.85f, 0.35f), 16, 0.6f); }
                        ShowShop(_shopBack);
                    }, 0.03f);
                    b.interactable = can;
                }
            }
            AddButton(s, Loc.T("返回", "Back"), new Vector3(0, -0.3f, -0.01f), new Vector2(0.26f, 0.075f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), () => _shopBack?.Invoke(), 0.04f);
        }

        // 臂长校准（头显首次进入 / 设置里）：双臂前伸保持 1.5 秒
        RingView _calRing; TextMeshPro _calText;
        public void ShowCalibrate()
        {
            var s = NewScreen("calibrate", 1.6f, -0.05f);
            UiKit.Board(s, new Vector2(0.72f, 0.5f));
            var t = UiKit.Text(s, Loc.T("把两只手臂向前伸直", "Stretch both arms straight out"), 0.055f, UiKit.BoardInk, 0.66f); t.transform.localPosition = new Vector3(0, 0.15f, -0.003f); t.outlineWidth = 0;
            _calText = UiKit.Text(s, Loc.T("保持一会儿，帮你量一下手臂长度", "Hold still for a moment while we measure your reach"), 0.034f, UiKit.BoardInk, 0.66f); _calText.transform.localPosition = new Vector3(0, 0.07f, -0.003f); _calText.outlineWidth = 0;
            // 图文一致（用户：所有图标和文字要匹配，比如测量手臂长度）：伸直的手臂 + 卷尺，左右各一只（右边镜像）
            var arm = theme.Icon("armlen") ? theme.Icon("armlen") : theme.Gesture("gun");
            var gl = UiKit.Image(s, arm, 0.15f); gl.transform.localPosition = new Vector3(-0.17f, -0.08f, -0.004f); gl.transform.localScale = new Vector3(-gl.transform.localScale.x, gl.transform.localScale.y, 1);
            var gr = UiKit.Image(s, arm, 0.15f); gr.transform.localPosition = new Vector3(0.17f, -0.08f, -0.004f);
            _calRing = RingView.Create(s, driver.ringMat, 0.12f); _calRing.transform.localPosition = new Vector3(0, -0.08f, -0.006f); _calRing.Show(true); _calRing.Snap(0);
            if (driver.input is Input.PicoInput pi) pi.BeginCalibration();
        }

        void TickCalibrate()
        {
            if (!(driver.input is Input.PicoInput pi)) { ShowTitle(); return; }
            if (pi.calibrator != null) { _calRing.SetFill((float)pi.calibrator.Progress()); return; }
            driver.Game.profile.armLen = pi.armLen; driver.Game.Persist();
            BangAudio.Ui("ui_confirm");
            ShowTitle();
        }

        public void ShowPause()
        {
            Paused = true;
            var s = NewScreen("pause", 1.35f, -0.12f);
            // 用户：暂停面板别太挤——板子加大、按钮之间留足空隙
            UiKit.Board(s, new Vector2(0.68f, 0.78f));
            var t = UiKit.Text(s, Loc.T("暂停一下", "Paused"), 0.064f, UiKit.BoardInk, 0.6f); t.transform.localPosition = new Vector3(0, 0.27f, -0.003f); t.outlineWidth = 0;
            AddButton(s, Loc.T("继续", "Resume"), new Vector3(0, 0.1f, -0.01f), new Vector2(0.46f, 0.11f), UiTheme.Peach, UiTheme.PeachB, theme.Icon("play"), Resume, 0.046f);
            bool endl = driver.Game.run?.mode == "endless";                       // 无尽：“重来这一波”（回到这一波开头），不是重开整局
            Confirm(AddButton(s, endl ? Loc.T("重来这一波", "Restart Wave") : Loc.T("重来本章", "Restart Chapter"), new Vector3(0, -0.07f, -0.01f), new Vector2(0.46f, 0.11f), UiTheme.Mint, UiTheme.MintB, theme.Icon("retry"),
                () => { Paused = false; if (endl) driver.ContinueEndless(); else driver.StartChapter(driver.chapter); }, 0.042f));
            Confirm(AddButton(s, Loc.T("回到主菜单", "Main Menu"), new Vector3(0, -0.24f, -0.01f), new Vector2(0.46f, 0.11f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), () => { Paused = false; driver.ToMenu(); }, 0.042f));
        }

        public void Resume() { Paused = false; ShowHud(); }

        /// <summary>房间被占满：停下来告诉玩家输在哪、给一条小建议，由玩家自己选“再试一次”或“回到主菜单”（不自动重来）。</summary>
        public void ShowLost(string reason, string tip, bool canRetry)
        {
            var s = NewScreen("lost", 1.45f, -0.1f);
            UiKit.Board(s, new Vector2(0.7f, 0.62f));
            var t = UiKit.Text(s, Loc.T("房间被梦境吞没了", "Dreamed Away!"), 0.058f, UiKit.BoardInk, 0.64f); t.transform.localPosition = new Vector3(0, 0.235f, -0.003f); t.outlineWidth = 0;
            var r = UiKit.Text(s, reason, 0.036f, UiKit.BoardInk, 0.62f); r.transform.localPosition = new Vector3(0, 0.15f, -0.003f); r.outlineWidth = 0;
            var tipRow = UiKit.Node(s, "Tip", new Vector3(0, 0.07f, -0.003f));
            var tipBg = UiKit.Panel(tipRow, new Vector2(0.6f, 0.085f), new Color(1f, 0.93f, 0.8f, 0.95f), new Color(1f, 0.9f, 0.78f, 0.95f), 0.03f, 0, null, 0f, 0.01f, 0, 0, true);
            var ti = UiKit.Image(tipRow, theme.Icon("star"), 0.045f); ti.transform.localPosition = new Vector3(-0.26f, 0, -0.004f);
            var tt = UiKit.Text(tipRow, tip, 0.027f, new Color(0.5f, 0.3f, 0.22f), 0.53f, TextAlignmentOptions.Left, 0); tt.transform.localPosition = new Vector3(0.035f, -0.003f, -0.004f);
            if (canRetry) AddButton(s, Loc.T("再试一次", "Try Again"), new Vector3(0, -0.05f, -0.01f), new Vector2(0.4f, 0.1f), UiTheme.Peach, UiTheme.PeachB, theme.Icon("retry"), () => { driver.RetryAfterLoss(); }, 0.044f);
            Confirm(AddButton(s, Loc.T("回到主菜单", "Main Menu"), new Vector3(0, canRetry ? -0.17f : -0.06f, -0.01f), new Vector2(0.4f, 0.09f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), driver.ToMenu, 0.04f));
            GuardButtons();
        }

        public void ShowStation(StationData st)
        {
            var s = NewScreen("station", 1.6f, -0.1f);
            UiKit.Board(s, new Vector2(0.72f, 0.62f));
            var t = UiKit.Text(s, Loc.T("喘口气", "Take a Breather"), 0.06f, UiKit.BoardInk, 0.6f); t.transform.localPosition = new Vector3(0, 0.22f, -0.003f); t.outlineWidth = 0;
            string body = st != null && st.longPlay
                ? Loc.T($"已经玩了 {st.minutes} 分钟啦，摘下头显让眼睛歇一歇吧，准备好了再开始下一波", $"You have played for {st.minutes} minutes. Take off the headset and rest your eyes. Start the next wave when you are ready.")
                : Loc.T("打得漂亮！放下手臂甩一甩，准备好了再开始下一波", "Nice work! Lower your arms and shake them out. Start the next wave when you are ready.");
            var b = UiKit.Text(s, body, 0.036f, UiKit.BoardInk, 0.62f); b.transform.localPosition = new Vector3(0, 0.09f, -0.003f); b.outlineWidth = 0;
            var ic = UiKit.Image(s, theme.Icon("heart"), 0.07f); ic.transform.localPosition = new Vector3(-0.29f, 0.22f, -0.004f);
            AddButton(s, Loc.T("开始下一波", "Start Next Wave"), new Vector3(0, -0.07f, -0.01f), new Vector2(0.44f, 0.1f), UiTheme.Peach, UiTheme.PeachB, theme.Icon("play"), () => { driver.Game.ContinueFromStation(); ShowHud(); }, 0.044f);
            AddButton(s, Loc.T("改装台", "Workshop"), new Vector3(0.145f, -0.2f, -0.01f), new Vector2(0.27f, 0.09f), UiTheme.Lav, UiTheme.LavB, theme.Icon("wand"), () => ShowShop(() => ShowStation(driver.Game.station)), 0.032f);
            Confirm(AddButton(s, Loc.T("回到主菜单", "Main Menu"), new Vector3(-0.145f, -0.2f, -0.01f), new Vector2(0.27f, 0.09f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), () => { driver.Game.QuitFromStation(); driver.ToMenu(); }, 0.032f));
            GuardButtons();
        }

        // 结算：缎带标题 → 星星一颗颗弹出（每颗一声）→ 数据 → 新装备 → 按钮
        readonly List<(Transform t, float at, Vector3 s)> _pop = new();
        public void ShowResult(RewardData rw)
        {
            var g = driver.Game;
            var s = NewScreen("result", 1.75f, -0.05f);
            _pop.Clear();
            UiKit.Board(s, new Vector2(0.86f, 0.74f), tape: false);     // 顶上已有丝带标题，不再贴和纸胶带
            var rb = UiKit.Image(s, theme.ribbon, 0.2f); rb.transform.localPosition = new Vector3(0, 0.36f, -0.006f);
            var rt = UiKit.Text(s, (rw?.chapter ?? driver.chapter) == Bang.Logic.CHAPTERS.Count ? Loc.T("梦境守护成功！", "Dream Saved!") : LevelLabel(rw?.chapter ?? driver.chapter) + Loc.T(" 通关！", " Clear!"), 0.06f, Color.white, 0.6f, TextAlignmentOptions.Center, 0.3f); rt.transform.localPosition = new Vector3(0, 0.37f, -0.012f);
            int n = rw?.stars ?? 0;
            for (int k = 0; k < 3; k++)
            {
                var st = UiKit.Image(s, theme.Icon(k < n ? "star" : "star_empty"), k == 1 ? 0.15f : 0.12f);
                st.transform.localPosition = new Vector3((k - 1) * 0.17f, k == 1 ? 0.18f : 0.15f, -0.006f);
                if (k < n) { _pop.Add((st.transform, 0.5f + k * 0.35f, st.transform.localScale)); st.transform.localScale *= 0.001f; }
            }
            var stats = rw?.stats;
            if (stats != null)
            {
                float acc = stats.shots > 0 ? stats.hits * 100f / stats.shots : 0;
                var tx = UiKit.Text(s, Loc.T($"命中率 {acc:0}%   击倒 {stats.kills}   金区换弹 {stats.golds}", $"Accuracy {acc:0}%   Toys Down {stats.kills}   Golden Reloads {stats.golds}"), 0.04f, UiKit.BoardInk, 0.82f);
                tx.transform.localPosition = new Vector3(0, 0.035f, -0.004f); tx.outlineWidth = 0;
                var tx2 = UiKit.Text(s, Loc.T($"最高连杀 {driver.BestStreak}   弹反 {stats.parries}   金币 +{stats.coins}", $"Best Streak {driver.BestStreak}   Parries {stats.parries}   Coins +{stats.coins}"), 0.034f, UiKit.BoardInk, 0.8f);
                tx2.transform.localPosition = new Vector3(0, -0.01f, -0.004f); tx2.outlineWidth = 0;
            }
            if (n < 3)                                                           // 差在哪：玩家知道下次怎么拿满三星
            {
                bool intro = (rw?.chapter ?? driver.chapter) <= 2; var gg = driver.Game;
                string why = gg.run.losses > 0 ? Loc.T("这一章失败过，下次一口气打完就能拿三星", "You lost once this chapter. Clear it in one go for 3 stars")
                    : Loc.T($"玩具化最高到了 {gg.toyPeak:0}%，三星要求始终不超过 {gg.cfg.diff.StarPeak(rw?.chapter ?? driver.chapter):0}%", $"Toy peaked at {gg.toyPeak:0}%. 3 stars needs it to stay under {gg.cfg.diff.StarPeak(rw?.chapter ?? driver.chapter):0}%");
                var wt = UiKit.Text(s, why, 0.028f, new Color(0.62f, 0.32f, 0.36f), 0.8f); wt.transform.localPosition = new Vector3(0, -0.16f, -0.004f); wt.outlineWidth = 0;
            }
            if (rw != null && !string.IsNullOrEmpty(rw.item) && rw.fresh)
            {
                var it = UiKit.Node(s, "Item", new Vector3(0, -0.08f, -0.006f));
                var ic = UiKit.Image(it, theme.Icon("wand"), 0.07f); ic.transform.localPosition = new Vector3(-0.17f, 0, 0);
                var itx = UiKit.Text(it, Loc.T("获得新装备：", "New gear: ") + (ItemNames.TryGetValue(rw.item, out var nm) ? nm : rw.item), 0.042f, UiTheme.PeachB, 0.5f, TextAlignmentOptions.Left);
                itx.transform.localPosition = new Vector3(0.08f, -0.004f, 0); itx.outlineWidth = 0;
            }
            int ch = rw?.chapter ?? driver.chapter;
            bool hasNext = ch < Bang.Logic.CHAPTERS.Count;
            // 主次（用户：一关完成后下一关放中间、适当放大）：下一章 = 正中的大按钮；再玩一次 / 主菜单 = 两侧小一号的次要按钮
            if (hasNext)
            {
                AddButton(s, Loc.T("下一章", "Next"), new Vector3(0, -0.245f, -0.014f), new Vector2(0.32f, 0.125f), UiTheme.Peach, UiTheme.PeachB, theme.Icon("play"), () => driver.StartChapter(ch + 1), 0.05f);
                AddButton(s, Loc.T("再玩一次", "Replay"), new Vector3(-0.3f, -0.25f, -0.012f), new Vector2(0.2f, 0.085f), UiTheme.Mint, UiTheme.MintB, theme.Icon("retry"), () => driver.StartChapter(ch), 0.032f);
                AddButton(s, Loc.T("主菜单", "Menu"), new Vector3(0.3f, -0.25f, -0.012f), new Vector2(0.2f, 0.085f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), driver.ToMenu, 0.032f);
            }
            else
            {
                // 终极决战：正中一个大按钮“到处看看”——收起面板，玩家在铺满房间、变亮了的玩具世界里自由走动观赏（用户：让玩家自主地看一看游戏场景）
                AddButton(s, Loc.T("到处看看", "Look Around"), new Vector3(0, -0.245f, -0.014f), new Vector2(0.32f, 0.125f), UiTheme.Peach, UiTheme.PeachB, theme.Icon("play"), () => { driver.Game.Explore(); ShowExplore(); }, 0.05f);
                AddButton(s, Loc.T("再玩一次", "Replay"), new Vector3(-0.3f, -0.25f, -0.012f), new Vector2(0.2f, 0.085f), UiTheme.Mint, UiTheme.MintB, theme.Icon("retry"), () => driver.StartChapter(ch), 0.032f);
                AddButton(s, Loc.T("主菜单", "Menu"), new Vector3(0.3f, -0.25f, -0.012f), new Vector2(0.2f, 0.085f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), driver.ToMenu, 0.032f);
            }
            { var rwc = rw; AddButton(s, Loc.T("改装台", "Workshop"), new Vector3(0.3f, 0.24f, -0.012f), new Vector2(0.22f, 0.065f), UiTheme.Lav, UiTheme.LavB, theme.Icon("wand"), () => ShowShop(() => ShowResult(rwc)), 0.032f); }
            GuardButtons();
        }

        /// <summary>用户：Boss 要干什么要明确清楚，不能傻站着。蓄力时说“它要干什么 + 现在打它伤害翻倍”，出招时说“怎么应对”，巡游时说它的套路。</summary>
        static string BossMoveLine(Bang.Logic.Enemy e)
        {
            string pre = "", preEn = "";
            if (e.kind == "boss6" && !string.IsNullOrEmpty(e.bossSrc) && e.bossSrc != "boss6") { var sk = Bang.Logic.KINDS.Get(e.bossSrc); pre = $"学{sk.name}："; preEn = $"{sk.en}'s move: "; }
            if (e.bossStage == 3) return Loc.T("踉跄了！趁现在猛打", "Staggered! Hit it now");
            if (e.bossStage == 4) return Loc.T("插下了一面大旗！快去打倒它", "Planted a big flag! Knock it down");
            bool w = e.bossStage == 1; int n = Mathf.Max(1, e.bossPhase);
            if (e.bossStage == 0)
                return e.kind switch
                {
                    "boss1" => Loc.T("在门口踱步，准备扔积木砖", "Pacing, about to throw bricks"),
                    "boss2" => Loc.T("在门口转悠，准备开炮", "Rolling around, about to fire"),
                    "boss3" => Loc.T("在门口巡视，会摇铃召集小锡兵", "Patrolling, will ring for troops"),
                    "boss4" => Loc.T("飘来飘去，准备撒彩纸", "Floating, about to throw confetti"),
                    "boss5" => Loc.T("在门口踱步，准备上发条", "Pacing, about to wind up"),
                    _ => Loc.T("在门口转悠，会学前面 Boss 的招", "Copies the other bosses' moves"),
                };
            return e.bossMove switch
            {
                "brick" => w ? Loc.T(pre + "举起积木砖……现在打它伤害翻倍", preEn + "Lifting a brick... double damage now") : Loc.T(pre + "扔积木砖！举盾挡住", preEn + "Brick incoming! Shield up"),
                "brickStomp" => w ? Loc.T(pre + "抬起身子蓄力……现在打它伤害翻倍", preEn + "Winding up a stomp... double damage now") : Loc.T(pre + "跺地！扔砖，还震出一只老鼠", preEn + "Stomp! A brick and a mouse"),
                "shells" => w ? Loc.T(pre + $"炮口对准你……要连发 {n} 炮", preEn + $"Aiming {n} shells...") : Loc.T(pre + "开炮！举盾一发一发挡", preEn + "Firing! Block them one by one"),
                "summon" => w ? Loc.T(pre + "摇铃召集小锡兵……现在打它伤害翻倍", preEn + "Ringing for troops... double damage now") : Loc.T(pre + "小锡兵冲过来了！先打小兵", preEn + "Troops charging! Shoot them first"),
                "foam" => w ? Loc.T(pre + "举枪瞄准你……", preEn + "Aiming at you...") : Loc.T(pre + "开枪！举盾挡住", preEn + "Shot fired! Shield up"),
                "confetti" => w ? Loc.T(pre + "鼓起肚子……要撒彩纸了", preEn + "Puffing up... confetti coming") : Loc.T(pre + "撒彩纸！举盾挡住别被糊脸", preEn + "Confetti! Shield up"),
                "gears" => w ? Loc.T(pre + "拧发条……现在打它伤害翻倍", preEn + "Winding up... double damage now") : Loc.T(pre + "连射三颗齿轮！举盾挡住", preEn + "Three gears! Shield up"),
                "multiFlag" => w ? Loc.T(pre + "抱起一把小旗……要一下插好几面", preEn + "Grabbing flags... planting several at once") : Loc.T(pre + $"一下插了 {n + 1} 面旗！快去打掉", preEn + $"{n + 1} flags planted! Shoot them down"),
                "grab" => w ? Loc.T(pre + "伸出磁铁爪……举盾，不然枪会被抢走", preEn + "Magnet claw... shield up or lose your gun") : Loc.T(pre + "磁铁爪飞过来了！举盾挡住", preEn + "Claw incoming! Shield up"),
                "drain" => w ? Loc.T(pre + "发条越拧越紧……举盾，不然弹匣会被震空", preEn + "Winding tight... shield up or lose your ammo") : Loc.T(pre + "发条震波！举盾挡住", preEn + "Shock wave! Shield up"),
                _ => "",
            };
        }

        // ───────── 终极决战的结尾（用户：不要玩具鞠躬就没话了，要说明一下结尾——游戏结束了）─────────
        // 一张故事卡，四句话一句一句浮出来：玩具们为什么闹、现在怎么样了、你做到了什么，最后一行大字“游戏结束 · 谢谢游玩”。
        // 看完点“继续”才进结算（结算页再选到处看看 / 回标题）
        public void ShowEpilogue(Action next)
        {
            var s = NewScreen("epilogue", 1.8f, -0.02f);
            UiKit.Board(s, new Vector2(0.92f, 0.74f), tape: false);
            var rb = UiKit.Image(s, theme.ribbon, 0.2f); rb.transform.localPosition = new Vector3(0, 0.36f, -0.006f);
            var rt = UiKit.Text(s, Loc.T("梦醒时分", "When the Dream Ends"), 0.058f, Color.white, 0.6f, TextAlignmentOptions.Center, 0.3f); rt.transform.localPosition = new Vector3(0, 0.37f, -0.012f);
            string[] lines =
            {
                Loc.T("原来，玩具们只是太久没人陪了。", "The toys had simply gone too long without anyone to play with."),
                Loc.T("它们打开梦境之门，想把房间变成玩具国。", "So they opened the dream doors to turn your whole room into Toyland."),
                Loc.T("你守住了房间，也陪它们好好玩了一场。", "Shot by shot you held the room, and gave them the game they wanted."),
                Loc.T("六位玩具之王心服口服，回到玩具箱里睡着了。", "All six Toy Kings bowed out and went back to sleep in the toybox."),
            };
            _epi.Clear();
            for (int i = 0; i < lines.Length; i++)
            {
                var t = UiKit.Text(s, lines[i], Loc.En ? 0.034f : 0.042f, UiKit.BoardInk, 0.84f); t.transform.localPosition = new Vector3(0, 0.235f - i * 0.092f, -0.004f); t.outlineWidth = 0;
                t.alpha = 0; _epi.Add((t, 0.5f + i * 1.6f));
            }
            var end = UiKit.Text(s, Loc.T("游戏结束 · 谢谢游玩！", "The End · Thanks for Playing!"), 0.062f, UiTheme.PeachB, 0.84f); end.transform.localPosition = new Vector3(0, -0.15f, -0.004f); end.outlineWidth = 0;
            end.alpha = 0; _epi.Add((end, 0.5f + lines.Length * 1.6f + 0.4f));
            var sub = UiKit.Text(s, Loc.T("无尽模式已解锁，想玩随时回来", "Endless mode unlocked. Come back anytime"), 0.03f, UiKit.BoardInk, 0.84f); sub.transform.localPosition = new Vector3(0, -0.215f, -0.004f); sub.outlineWidth = 0;
            sub.alpha = 0; _epi.Add((sub, 0.5f + lines.Length * 1.6f + 0.9f));
            var btn = AddButton(s, Loc.T("继续", "Continue"), new Vector3(0, -0.3f, -0.012f), new Vector2(0.28f, 0.1f), UiTheme.Peach, UiTheme.PeachB, theme.Icon("play"), () => next?.Invoke(), 0.045f);
            GuardButtons(0.5f + lines.Length * 1.6f + 1.2f);                  // 文字放完才能点（不会一枪误跳过）
            _epiT = 0;
        }
        readonly List<(TextMeshPro t, float at)> _epi = new(); float _epiT = -1;
        void TickEpilogue(float dt)
        {
            if (_state != "epilogue" || _epiT < 0) return;
            _epiT += dt;
            foreach (var (t, at) in _epi) if (t) t.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_epiT - at) / 0.8f));
        }

        /// <summary>无尽模式结束：撑过几波、最佳纪录；再来一局 / 主菜单。</summary>
        public void ShowEndlessOver(int waves, int best)
        {
            var s = NewScreen("lost", 1.45f, -0.1f);
            UiKit.Board(s, new Vector2(0.7f, 0.56f));
            var t = UiKit.Text(s, Loc.T("无尽模式结束", "Endless Over"), 0.058f, UiKit.BoardInk, 0.64f); t.transform.localPosition = new Vector3(0, 0.2f, -0.003f); t.outlineWidth = 0;
            var r = UiKit.Text(s, Loc.T($"撑过了 {waves} 波", $"You held {waves} waves"), 0.05f, UiTheme.PeachB, 0.62f); r.transform.localPosition = new Vector3(0, 0.1f, -0.003f); r.outlineWidth = 0;
            var b = UiKit.Text(s, waves >= best ? Loc.T("新纪录！", "New best!") : Loc.T($"最佳纪录：第 {best} 波", $"Best: wave {best}"), 0.034f, UiKit.BoardInk, 0.62f); b.transform.localPosition = new Vector3(0, 0.03f, -0.003f); b.outlineWidth = 0;
            AddButton(s, Loc.T("再来一局", "Play Again"), new Vector3(0, -0.08f, -0.01f), new Vector2(0.4f, 0.1f), UiTheme.Peach, UiTheme.PeachB, theme.Icon("retry"), () => driver.StartEndless(), 0.044f);
            AddButton(s, Loc.T("回到主菜单", "Main Menu"), new Vector3(0, -0.19f, -0.01f), new Vector2(0.4f, 0.09f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), driver.ToMenu, 0.04f);
            GuardButtons();
        }

        /// <summary>自由观赏：只在视野下方留一块小牌“看够了就回来”（看 + 捏 / 打一枪），不挡风景。</summary>
        public void ShowExplore()
        {
            // 用户：最后的小牌子就写“返回标题”，射两次就回到标题（第一次变成“再打一次确认”，3 秒内再打一次）
            var s = NewScreen("explore", 1.3f, -0.42f);
            Confirm(AddButton(s, Loc.T("返回标题", "Back to Title"), Vector3.zero, new Vector2(0.3f, 0.085f), UiTheme.Sky, UiTheme.SkyB, theme.Icon("home"), driver.ToMenu, 0.036f));
            GuardButtons(0.8f);
        }

        public void ShowHud()
        {
            if (_screen) Destroy(_screen.gameObject);
            if (_stageGo) { Destroy(_stageGo); _stageGo = null; _stageBtns.Clear(); }
            _buttons.Clear(); _hoverBtn = null; _state = "hud";
            bool placed = PlaceHud();
            if (_hud) _hud.gameObject.SetActive(placed);   // 用户：第一波的 UI 有露面——开门动画期间门还没生成，HUD 摆不上去，先不露；门开完 StartChapter 会再调一次
        }

        // ───────────────────────── 战斗 HUD（钉在门洞上）─────────────────────────
        void BuildHud()
        {
            _hud = UiKit.Node(transform, "HUD", Vector3.zero);
            // 门上方（正对门的中线），用户要求：
            //   ① 上面的进度 = 这一波的进度（顶上凸出一个圆角小标签写“第 n / 5 波”，面板两头全圆——不再是“被砍了一刀、头顶太平”）
            //   ② 小旗那一排 = 已经插下的旗子（按章 8～12 面插满整个房间），不再表示波次，免得误导
            //   ③ 暂停按钮在面板里，图标和字在按钮正中
            var meter = UiKit.Node(_hud, "WaveMeter", new Vector3(0, 0.3f, 0));
            UiKit.Board(meter, new Vector2(1.24f, 0.2f));                     // 和菜单同一种厚板 + 深梅紫描边
            var tab = UiKit.Node(meter, "Tab", new Vector3(-0.1f, 0.115f, -0.022f));
            UiKit.ToonSlab(UiKit.Node(tab, "Board", new Vector3(0, 0, 0.012f)), new Vector2(0.4f, 0.1f), UiKit.ToonYellow, 0.03f, 0.009f, 0.01f);   // 压在主面板上沿之上
            _meterText = UiKit.Text(tab, "", 0.044f, UiKit.BoardInk, 0.36f, TextAlignmentOptions.Center, 0); _meterText.transform.localPosition = new Vector3(0, -0.004f, -0.004f);
            _meterText.enableAutoSizing = true; _meterText.fontSizeMin = _meterText.fontSize * 0.6f; _meterText.fontSizeMax = _meterText.fontSize; _meterText.renderer.sortingOrder = 2;
            _meterTrack = UiKit.Panel(meter, new Vector2(0.78f, 0.06f), UiKit.ToonBlue, UiKit.ToonBlue, 0.03f, 0.006f, UiKit.BoardInk, 0f, 0.01f, 0f, 0f, true);
            _meterTrack.transform.localPosition = new Vector3(-0.1f, -0.01f, -0.005f);
            _meterFill = UiKit.Panel(meter, new Vector2(0.02f, 0.048f), UiKit.ToonRed, UiKit.ToonRed, 0.024f, 0, null, 0f, 0.005f, 0f, 0f, true);
            _meterFill.transform.localPosition = new Vector3(-0.48f, -0.01f, -0.007f);
            _bossMoveText = UiKit.Text(meter, "", 0.03f, UiKit.BoardInk, 0.9f, TextAlignmentOptions.Center, 0);   // Boss 在场：血条下面一行“它正在 / 马上要干什么”
            _bossMoveText.transform.localPosition = new Vector3(-0.1f, -0.068f, -0.01f); _bossMoveText.renderer.sortingOrder = 2;
            _bossMoveText.enableAutoSizing = true; _bossMoveText.fontSizeMin = _bossMoveText.fontSize * 0.6f; _bossMoveText.fontSizeMax = _bossMoveText.fontSize;
            _meterFill.sortingOrder = 1;   // 和底槽同一队列、按中心远近排序：填充条中心偏左被判成“更远”先画，整条被底槽盖住（进度一直看不见）
            _bossTicks.Clear();
            foreach (var f in new[] { 1f / 3f, 2f / 3f })                  // Boss 血条的换阶段刻度（平时藏着）
            {
                var tk = UiKit.Panel(meter, new Vector2(0.008f, 0.07f), UiKit.BoardInk, UiKit.BoardInk, 0.004f, 0, null, 0f, 0.002f);
                tk.transform.localPosition = new Vector3(-0.48f + 0.76f * f, -0.01f, -0.009f); tk.sortingOrder = 2; tk.gameObject.SetActive(false); _bossTicks.Add(tk);
            }
            _pauseBtn = UiKit.Button(meter, "", new Vector2(0.17f, 0.14f), UiKit.BtnStyle.Secondary, () => driver.Pause(), 0.03f);
            _pauseBtn.transform.localPosition = new Vector3(0.47f, 0, -0.012f);
            var pf = _pauseBtn.face ? _pauseBtn.face : _pauseBtn.transform;
            foreach (var x in new[] { -0.013f, 0.013f })                 // “❚❚” + “暂停”作为一组，整组在按钮正中（图标在上、字在下）
            {
                var bar = UiKit.Panel(pf, new Vector2(0.014f, 0.04f), UiKit.BoardInk, UiKit.BoardInk, 0.007f, 0, null, 0f, 0.002f);
                bar.transform.localPosition = new Vector3(x, 0.02f, -0.004f);
                bar.sortingOrder = 2;                                    // 贴面的队列比面板晚：不提前会被按钮贴面整个盖住
            }
            var pl = UiKit.Text(pf, Loc.T("暂停", "Pause"), 0.034f, UiKit.BoardInk, 0.16f, TextAlignmentOptions.Center, 0); pl.transform.localPosition = new Vector3(0, -0.028f, -0.004f);
            pl.enableAutoSizing = true; pl.fontSizeMin = pl.fontSize * 0.6f; pl.fontSizeMax = pl.fontSize; pl.renderer.sortingOrder = 2;
            if (_pauseBtn.label) _pauseBtn.label.gameObject.SetActive(false);
            // 插旗：一条小面板，“玩具化” + n 面小旗（n 按章，插下一面亮一面；颜料弹的零头让下一面亮一部分）
            _flagRow = UiKit.Node(_hud, "Flags", new Vector3(0, 0.1f, 0));
            _flagLabel = UiKit.Text(_flagRow, Loc.T("玩具化", "Toyified"), 0.036f, UiKit.BoardInk, 0.22f, TextAlignmentOptions.Left, 0);
            _streakRoot = UiKit.Node(_hud, "Streak", new Vector3(-0.76f, 0.1f, -0.02f));
            UiKit.Board(_streakRoot, new Vector2(0.34f, 0.12f));
            _streakText = UiKit.Text(_streakRoot, "", 0.056f, UiKit.ToonRed, 0.3f, TextAlignmentOptions.Center, 0); _streakText.transform.localPosition = new Vector3(0, -0.004f, -0.004f);
            _streakRoot.gameObject.SetActive(false);
            _flagLabel.enableAutoSizing = true; _flagLabel.fontSizeMin = _flagLabel.fontSize * 0.6f; _flagLabel.fontSizeMax = _flagLabel.fontSize;
            LayoutFlags(5);
            // 缎带提示（门洞前、比门高一点）
            // 用户：横幅 UI 和面板“露面”（叠在一起）——原来横幅在门洞上缘、往玩家这边凸 0.6，透视后压在“玩具化”面板上，副标题白字没底板悬在墙上。
            // 现在：横幅整块放到 HUD 面板下方（不重叠），副标题收进缎带里，主副标题都按缎带中段宽度自动缩字（英文长句也不溢出）
            _banner = UiKit.Node(_hud, "Banner", new Vector3(0, -0.34f, -0.2f));
            var rb = UiKit.Image(_banner, theme.ribbon, 0.36f);
            _bannerMain = UiKit.Text(_banner, "", 0.07f, Color.white, 0.72f); _bannerMain.transform.localPosition = new Vector3(0, 0.072f, -0.01f);
            _bannerSub = UiKit.Text(_banner, "", 0.036f, Color.white, 0.74f, TextAlignmentOptions.Center, 0.32f); _bannerSub.transform.localPosition = new Vector3(0, 0.008f, -0.01f);
            foreach (var bt in new[] { _bannerMain, _bannerSub }) { bt.enableAutoSizing = true; bt.fontSizeMin = bt.fontSize * 0.55f; bt.fontSizeMax = bt.fontSize; bt.enableWordWrapping = false; bt.overflowMode = TextOverflowModes.Ellipsis; }
            _bannerIcon = UiKit.Image(_banner, theme.ribbon, 0.14f); _bannerIcon.transform.localPosition = new Vector3(-0.3f, 0.045f, -0.014f); _bannerIcon.gameObject.SetActive(false);   // 获得增益：缎带左边放该增益的图标
            _banner.gameObject.SetActive(false); _bannerHome = _banner.localPosition;
            // 教学卡（门洞左侧、胸口高度）
            _lessonCard = UiKit.Node(transform, "LessonCard", Vector3.zero);
            UiKit.Board(_lessonCard, new Vector2(0.5f, 0.5f));          // 留出两行标题 + 两行副标题的位置
            _gestureImg = UiKit.Image(_lessonCard, theme.Gesture("gun"), 0.2f); _gestureImg.transform.localPosition = new Vector3(0, 0.1f, -0.004f);
            // 标题从上往下排（英文常折成两行），副标题跟在标题下面（LessonStep 里按标题实际高度摆）；纸卡上用墨色字
            _lessonText = UiKit.Text(_lessonCard, "", 0.04f, UiKit.BoardInk, 0.46f, TextAlignmentOptions.Top); _lessonText.transform.localPosition = new Vector3(0, -0.04f, -0.004f); _lessonText.outlineWidth = 0;
            _lessonSub = UiKit.Text(_lessonCard, "", 0.026f, new Color(UiKit.BoardInk.r, UiKit.BoardInk.g, UiKit.BoardInk.b, 0.72f), 0.46f, TextAlignmentOptions.Top); _lessonSub.transform.localPosition = new Vector3(0, -0.13f, -0.004f); _lessonSub.outlineWidth = 0;
            foreach (var t in new[] { _lessonText, _lessonSub }) { t.rectTransform.pivot = new Vector2(0.5f, 1f); t.rectTransform.sizeDelta = new Vector2(0.42f, 0.12f); }   // 左右离纸边留白
            _lessonCard.gameObject.SetActive(false);
            _hud.gameObject.SetActive(false);
        }

        /// <summary>HUD 钉在第一道门的上方（门在场景里的位置由规则层的门换算）。</summary>
        bool PlaceHud()
        {
            var g = driver.Game; if (g?.portals == null || g.portals.Count == 0) return false;
            var p = g.portals[0];
            var b = LogicSpace.ToUnity(p.@base); var n = LogicSpace.ToUnity(p.normal).normalized;
            if (g.portals.Count > 1)                                       // 多门章节：放在所有门的正中方向（两门之间的墙上方），不压某一扇门的门框
            {
                var ns = Vector3.zero; foreach (var q in g.portals) ns += LogicSpace.ToUnity(q.normal);
                if (ns.sqrMagnitude > 1e-4f) { var dist = new Vector2(b.x, b.z).magnitude; n = ns.normalized; b = new Vector3(-n.x * dist, b.y, -n.z * dist); }
            }
            var face = Quaternion.LookRotation(-n);                     // 面朝房间（玩家沿门的反法线方向看过去）
            // 高度：底边（小旗那条）紧贴门框顶，顶端（波次小标签）压在天花板下面——
            // 原来固定在门底 + 2.2 米、整体 ×1.35，顶上的标签到了 2.8 米，插进天花板被切成平头（用户：UI 被砍了一刀、头顶太平），还得抬头看
            const float S = 1.2f, LocalBottom = 0.04f, LocalTop = 0.465f;
            var at = b + n * 0.3f;                                        // 离墙 0.3 米：墙面网格不插进面板
            float doorTop = b.y + (float)p.h + 0.22f;                     // 门框积木的顶
            float ceil = CeilingY(at, b.y);
            float y = doorTop - LocalBottom * S;
            y = Mathf.Min(y, ceil - 0.06f - LocalTop * S);
            _hud.SetPositionAndRotation(new Vector3(at.x, y, at.z), face);
            _hud.localScale = Vector3.one * S;                           // 3 米外：字高约 1°，头显里看得清
            var right = Vector3.Cross(Vector3.up, -n).normalized;       // 从玩家看过去的右边
            var cardPos = b + n * 1.25f - right * 1.1f + Vector3.up * 1.32f;   // 门左侧、离玩家约 2 米、胸口高度
            _lessonCard.localScale = Vector3.one * 1.15f;
            var toHead = head ? head.position - cardPos : n; toHead.y = 0;
            _lessonCard.SetPositionAndRotation(cardPos, Quaternion.LookRotation(-toHead.normalized));
            return true;
        }

        /// <summary>封面按房间高度完整摆下（用户：上面不能被截）：天花板太低时整座封面最多下移 15 厘米，放射条纹再按剩余高度缩小。
        /// 往上打到的第一个房间碰撞体算天花板（封面自己的碰撞体不算），打不到按 2.6 米。</summary>
        void FitStage()
        {
            if (!_stageGo) return;
            const float StageY = 0.02f, StageTop = 0.98f, Margin = 0.03f;
            var st = _stageGo.transform; var sun = st.Find("Sunburst"); if (!sun) return;
            float ay = st.parent.position.y, ceil = 2.6f;
            var at = st.parent.TransformPoint(sun.localPosition.x, 0f, sun.localPosition.z);
            foreach (var h in Physics.RaycastAll(new Vector3(at.x, ay, at.z), Vector3.up, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(st) && Vector3.Dot(h.normal, Vector3.down) > 0.5f) ceil = Mathf.Min(ceil, h.point.y);
            float down = Mathf.Clamp(ay + StageY + StageTop - (ceil - Margin), 0f, 0.15f);
            st.localPosition = new Vector3(0, StageY - down, 0);
            float radius = sun.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x;
            float room = ceil - Margin - (ay + StageY - down + sun.localPosition.y);
            sun.localScale = Vector3.one * Mathf.Clamp(room / radius, 0.6f, 1f);
        }

        /// <summary>门前上方的天花板高度：往上打房间碰撞体（头显的空间网格 / 编辑器预览房间）；打不到按地面 + 2.45 米。</summary>
        static float CeilingY(Vector3 at, float floorY)
        {
            float best = floorY + 2.45f;
            var from = new Vector3(at.x, floorY + 1.6f, at.z);
            foreach (var h in Physics.RaycastAll(from, Vector3.up, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                if (Vector3.Dot(h.normal, Vector3.down) > 0.5f && h.point.y < best) best = h.point.y;
            return Mathf.Max(best, floorY + 2.1f);
        }

        Transform _flagRow, _flagPanel; TextMeshPro _flagLabel;
        /// <summary>小旗那一排按格数重排：格距随格数收紧（8 格 7.5 cm、12 格 6 cm），面板宽度跟着伸缩，“玩具化”字贴左边。</summary>
        void LayoutFlags(int n)
        {
            foreach (var r in _wavePips) if (r) Destroy(r.gameObject);
            _wavePips.Clear();
            float step = n <= 6 ? 0.085f : n <= 8 ? 0.075f : n <= 10 ? 0.067f : 0.06f, ih = Mathf.Min(0.085f, step * 1.05f);
            float label = 0.2f, w = label + n * step + 0.08f;
            if (_flagPanel) Destroy(_flagPanel.gameObject);                // 厚板尺寸烘在网格里：格数变了就换一块
            _flagPanel = UiKit.Board(_flagRow, new Vector2(w, 0.12f)).transform.parent;
            _flagLabel.transform.localPosition = new Vector3(-w / 2 + 0.05f + label / 2, -0.004f, -0.004f);
            float x0 = -w / 2 + 0.05f + label + step / 2;
            for (int i = 0; i < n; i++)
            {
                var f = UiKit.Image(_flagRow, theme.Icon("flag"), ih); f.transform.localPosition = new Vector3(x0 + i * step, 0, -0.004f);
                _wavePips.Add(f); _pipScale = f.transform.localScale;
            }
            _flagShown = 0;
        }

        /// <summary>缎带横幅。important（Boss 预警 / 登场）：播完之前普通横幅（礼盒、门涌…）先排队，之后再出，不会把 Boss 提示顶掉。</summary>
        /// <summary>atEye：出现在玩家眼前（Boss、获得增益）；icon：缎带左边的图标。
        /// 用户：收获的 buff 是什么要标清楚——增益横幅会锁住一小会儿，不被紧跟着的普通横幅顶掉；被锁时普通横幅排队（不再只留最后一条）。</summary>
        public void Banner(string main, string sub = "", float life = 2.4f, bool important = false, Texture2D icon = null, bool atEye = false, float lockFor = 0f)
        {
            if (!important && Time.time < _bannerLock) { if (_pendingBanners.Count < 4) _pendingBanners.Add((main, sub, life, icon, atEye, lockFor)); return; }
            if (important) _bannerLock = Time.time + life;
            else if (lockFor > 0) _bannerLock = Time.time + lockFor;
            BannerAtEye(important || atEye);
            _banner.gameObject.SetActive(true);
            _banner.localScale = Vector3.zero;                                   // 弹出动画从 0 开始：第一条横幅首帧不再满尺寸闪一下
            _bannerMain.text = main; _bannerSub.text = sub; _bannerT = 0; _bannerLife = life;
            float tx = icon ? 0.08f : 0f;
            _bannerMain.transform.localPosition = new Vector3(tx, 0.072f, -0.01f); _bannerSub.transform.localPosition = new Vector3(tx, 0.008f, -0.01f);
            _bannerMain.rectTransform.sizeDelta = new Vector2(icon ? 0.56f : 0.72f, _bannerMain.rectTransform.sizeDelta.y); _bannerSub.rectTransform.sizeDelta = new Vector2(icon ? 0.58f : 0.74f, _bannerSub.rectTransform.sizeDelta.y);
            if (_bannerIcon)
            {
                _bannerIcon.gameObject.SetActive(icon);
                if (icon)
                {
                    var mpb = new MaterialPropertyBlock(); _bannerIcon.GetPropertyBlock(mpb); mpb.SetTexture("_BaseMap", icon); _bannerIcon.SetPropertyBlock(mpb);
                    _bannerIcon.transform.localScale = new Vector3(0.14f * icon.width / (float)icon.height, 0.14f, 1f);   // 按图标自己的宽高比（用户：补给箱图标被拉伸变形——占位图是 4:1 的缎带）
                }
            }
        }
        readonly List<(string main, string sub, float life, Texture2D icon, bool atEye, float lockFor)> _pendingBanners = new();
        float _bannerLock;
        float BannerScale => _bannerAtEye ? 0.62f : 1f;                   // 1.6 米处 ×0.62 ≈ HUD 上（3 米 ×1.2）同样的视角大小

        /// <summary>重要横幅（Boss 预警 / 登场）出现在玩家正前方、视线稍上；普通横幅还在门洞上缘。</summary>
        void BannerAtEye(bool eye)
        {
            if (!_eyeAnchor || eye == _bannerAtEye && (!eye || _banner.parent == _eyeAnchor.transform)) { if (eye && _eyeAnchor) _eyeAnchor.Snap(); return; }
            _bannerAtEye = eye;
            if (eye) { _eyeAnchor.Snap(); _banner.SetParent(_eyeAnchor.transform, false); _banner.localPosition = Vector3.zero; _banner.localRotation = Quaternion.identity; }
            else { _banner.SetParent(_hud, false); _banner.localPosition = _bannerHome; _banner.localRotation = Quaternion.identity; }
        }

        public void LessonStep(LessonStepFx ls)
        {
            _lessonCard.gameObject.SetActive(true);
            PlaceHud();
            _lessonText.text = ls.text; _lessonSub.text = ls.sub;
            _lessonText.ForceMeshUpdate();
            float th = Mathf.Max(0.045f, _lessonText.textBounds.size.y);
            _lessonSub.transform.localPosition = new Vector3(0, -0.04f - th - 0.014f, -0.004f);
            _gestureKind = string.IsNullOrEmpty(ls.gesture) ? "gun" : ls.gesture;          // 规则层直接给手势（不靠解析文案，英文版也对）
            var tex = _gestureKind == "flag" ? theme.Icon("flag") : theme.Gesture(_gestureKind == "flick" ? "flick" : _gestureKind);
            if (tex) { var b = new MaterialPropertyBlock(); _gestureImg.GetPropertyBlock(b); b.SetTexture("_BaseMap", tex); _gestureImg.SetPropertyBlock(b); _gestureScale = new Vector3(0.2f * tex.width / tex.height, 0.2f, 1); }
            _lessonT = 0;
        }

        public void LessonDone() { _lessonCard.gameObject.SetActive(false); }

        Transform _streakRoot; TextMeshPro _streakText; float _streakHide, _streakPop;
        /// <summary>连杀牌：门上方 HUD 左侧，每杀一只弹一下。</summary>
        public void Streak(int n)
        {
            if (!_streakRoot) return;
            _streakRoot.gameObject.SetActive(true); _streakHide = Time.time + 1.8f; _streakPop = 1f;
            _streakText.text = Loc.T($"{n} 连杀", $"{n} KILLS");
        }

        void TickHud(float dt)
        {
            if (_streakRoot && _streakRoot.gameObject.activeSelf)
            {
                _streakPop = Mathf.MoveTowards(_streakPop, 0f, dt * 7f);
                _streakRoot.localScale = Vector3.one * (1f + 0.22f * _streakPop);
                if (Time.time > _streakHide) _streakRoot.gameObject.SetActive(false);
            }
            var g = driver.Game;
            if (_hud) { bool finale = driver.FinaleBright; if (_hud.gameObject.activeSelf == finale && _state == "hud") _hud.gameObject.SetActive(!finale); if (finale) return; }   // 终极决战谢幕：收起波次面板，不挡 Boss 们谢幕
            // 本波进度：这一波要出的小怪（队列 + 场上）打掉了多少
            int alive = 0; foreach (var e in g.enemies) if (!e.dead && e.state != "gone") alive++;
            int left = (g.run?.queue?.Count ?? 0) + alive; int wave = g.run?.wave ?? 0; bool lesson = g.phase == "lesson";
            if (wave != _hudWave || g.phase != "wave") { _hudWave = g.phase == "wave" ? wave : -1; _waveTotal = left; }
            _waveTotal = Mathf.Max(_waveTotal, left);
            float prog = g.phase == "wave" ? (_waveTotal > 0 ? 1f - left / (float)_waveTotal : 0f) : (g.phase == "rest" || g.phase == "station" || g.phase == "clear" ? 1f : 0f);
            // Boss 在场：这条改成 Boss 血条（终极决战两个 Boss 时合计），1/3、2/3 处两道刻度 = 换阶段的位置；Boss 倒下后回到波次进度
            double bhp = 0, bmax = 0; int bn = 0; Bang.Logic.Enemy b0 = null;
            foreach (var e in g.enemies) if (!e.dead && e.state != "gone" && Bang.Logic.Game.IsBoss(e)) { bhp += Mathf.Max(0f, (float)e.hp); bmax += e.maxHp; bn++; b0 ??= e; }
            bool bossBar = bn > 0 && bmax > 0;
            if (bossBar != _bossBarOn)
            {
                _bossBarOn = bossBar; _meterShown = bossBar ? 1f : prog;
                var fc = bossBar ? new Color(0.58f, 0.36f, 0.92f) : UiKit.ToonRed; UiKit.SetPanelColors(_meterFill, fc, fc);
                foreach (var t in _bossTicks) if (t) t.gameObject.SetActive(bossBar);
            }
            if (bossBar) prog = (float)(bhp / bmax);
            _meterShown = bossBar ? Mathf.MoveTowards(_meterShown, prog, dt * 1.6f) : Mathf.MoveTowards(_meterShown, prog, dt * 0.8f);
            float w = Mathf.Max(0.052f, 0.76f * _meterShown);
            UiKit.SetPanelSize(_meterFill, new Vector2(w, 0.048f), 0.005f);
            _meterFill.transform.localPosition = new Vector3(-0.48f + w * 0.5f, -0.01f, -0.007f);
            var tabT = _meterText.transform.parent;
            if (tabT) tabT.localScale = Vector3.one * (g.bossWarnT > 0 ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 4.5f)) : 1f);   // 预警时标签一跳一跳
            _meterText.text = g.bossWarnT > 0 ? Loc.T("Boss 来袭！", "Boss Incoming!") : bossBar ? (bn > 1 ? Loc.T($"Boss ×{bn}", $"Boss ×{bn}") : FinalTag(wave) + Loc.T(b0.k.name, b0.k.en))
                : lesson ? Loc.T("热身", "Warm-up") : g.run?.mode == "endless" ? Loc.T($"无尽 · 第 {wave + 1} 波", $"Endless · Wave {wave + 1}") : Loc.T($"第 {wave + 1} / {WaveCount()} 波", $"Wave {wave + 1} / {WaveCount()}");
            if (_bossMoveText)
            {
                Bang.Logic.Enemy bs = null;
                foreach (var e in g.enemies) if (!e.dead && e.state != "gone" && Bang.Logic.Game.IsBoss(e) && (bs == null || e.bossStage > bs.bossStage)) bs = e;
                _bossMoveText.text = bossBar && bs != null && g.bossWarnT <= 0 ? BossMoveLine(bs) : "";
            }
            // 插旗：格数 = 这一章插满要几面（按章 8～12），亮几面就是插了几面；只差最后一面时最后一格闪
            int need = Mathf.Max(1, g.FlagsToFull);
            if (need != _wavePips.Count) LayoutFlags(need);
            float toyFlags = (float)(g.toy / (100.0 / need));
            _flagShown = Mathf.MoveTowards(_flagShown, toyFlags, dt * 3f);
            for (int i = 0; i < _wavePips.Count; i++)
            {
                float fill = Mathf.Clamp01(_flagShown - i);
                bool danger = toyFlags >= need - 1.01f && i == need - 1;
                float pulse = danger ? 0.5f + 0.5f * Mathf.Sin(Time.time * 8f) : 0f;
                var c = fill >= 0.999f ? UiKit.ToonRed : Color.Lerp(new Color(UiKit.ToonInk.r, UiKit.ToonInk.g, UiKit.ToonInk.b, 0.28f), UiKit.ToonRed, fill);   // 没插的旗：奶油底上的淡紫剪影
                if (danger) c = Color.Lerp(c, new Color(1f, 0.35f, 0.4f, 1f), pulse);
                UiKit.SetTint(_wavePips[i], c);
                float pop = fill >= 0.999f ? 1.08f : 1f;
                _wavePips[i].transform.localScale = _pipScale * pop * (danger ? 1f + 0.12f * pulse : 1f);
            }
            if (_pendingBanners.Count > 0 && Time.time >= _bannerLock) { var pb = _pendingBanners[0]; _pendingBanners.RemoveAt(0); Banner(pb.main, pb.sub, pb.life, false, pb.icon, pb.atEye, pb.lockFor > 0 ? pb.lockFor : (_pendingBanners.Count > 0 ? 1.4f : 0f)); }
            if (_bannerT >= 0)
            {
                _bannerT += dt;
                float s = UiKit.Back(_bannerT / 0.3f);
                float a = _bannerLife > 0 ? Mathf.Clamp01((_bannerLife - _bannerT) / 0.35f) : 1f;
                _banner.localScale = Vector3.one * BannerScale * s * Mathf.Lerp(0.85f, 1f, a);
                foreach (var r in _banner.GetComponentsInChildren<Renderer>()) if (!(r is MeshRenderer mr && mr.GetComponent<TextMeshPro>())) UiKit.SetAlpha(r, a);
                _bannerMain.alpha = a; _bannerSub.alpha = a;
                if (_bannerLife > 0 && _bannerT >= _bannerLife) { _banner.gameObject.SetActive(false); _bannerT = -1f; BannerAtEye(false); }
            }
            if (_lessonCard.gameObject.activeSelf)
            {
                _lessonT += dt;
                var tr = _gestureImg.transform; float t = _lessonT;
                // 手势插图做出动作本身：挑腕 = 往上一挑再回落；握拳 = 用力一握；举盾 = 往前一推；其余轻轻浮动
                float ang = 0, sc = 1, dy = 0;
                switch (_gestureKind)
                {
                    case "flick": { float c = t % 1.4f; ang = c < 0.12f ? Mathf.Lerp(0, 28f, c / 0.12f) : c < 0.6f ? Mathf.Lerp(28f, 0, (c - 0.12f) / 0.48f) : 0; break; }
                    case "fist": { float c = t % 1.2f; sc = c < 0.15f ? 1f - 0.12f * (c / 0.15f) : 1f - 0.12f * Mathf.Exp(-(c - 0.15f) * 8f); break; }
                    case "palm": { float c = t % 1.6f; dy = Mathf.Sin(Mathf.Clamp01(c / 0.5f) * Mathf.PI) * 0.015f; sc = 1f + Mathf.Sin(Mathf.Clamp01(c / 0.5f) * Mathf.PI) * 0.06f; break; }
                    default: dy = Mathf.Sin(t * 2.4f) * 0.006f; break;
                }
                tr.localRotation = Quaternion.Euler(0, 0, ang);
                tr.localPosition = new Vector3(0, 0.1f + dy, -0.004f);
                tr.localScale = _gestureScale * sc;
            }
        }

        // ───────────────────────── 指针（视线 + 捏合 / 开枪 / 点击）─────────────────────────
        readonly RaycastHit[] _hits = new RaycastHit[16];
        Vector3? _pickPoint;
        UiButton Pick(Ray r)
        {
            int n = Physics.RaycastNonAlloc(r, _hits, 6f);
            UiButton best = null; float bd = 1e9f; _pickPoint = null;
            for (int i = 0; i < n; i++) { var b = _hits[i].collider.GetComponentInParent<UiButton>(); if (b && b.interactable && _hits[i].distance < bd) { bd = _hits[i].distance; best = b; _pickPoint = _hits[i].point; } }
            return best;
        }

        /// <summary>每帧由 GameDriver 调用（在规则层之前）：返回 true 表示这一帧的手势已被界面吃掉（不再交给规则层）。</summary>
        public bool Tick(FrameInput frame, Ray gaze, float dt)
        {
            _screenT += dt;
            if (_screen) _screen.localScale = Vector3.one * UiKit.Back(_screenT / 0.35f);
            foreach (var (t, at, s0) in _pop)
            {
                if (!t) continue;
                float k = (_screenT - at) / 0.3f;
                if (k > 0 && k - dt / 0.3f <= 0) { BangAudio.Ui("reload_gold", 0.8f); PlayFx.Instance?.Confetti(t.position, 10); }
                t.localScale = s0 * (k <= 0 ? 0.001f : UiKit.Back(k));
            }
            TickEpilogue(dt);
            if (_state == "hud")
            {
                TickHud(dt); CheckPalmPause(frame, dt);
                // HUD 上只有暂停按钮可点：看着它时捏合，或开枪打它（用户：所有按钮都要能用开枪确认）。
                // 开枪要先盯着它 0.3 秒——打门口小怪时视线扫过按钮的那一下不算，免得一枪误点暂停
                var hp = _pauseBtn && _pauseBtn.gameObject.activeInHierarchy ? Pick(gaze) : null; if (hp != _pauseBtn) hp = null;
                if (hp != _hoverBtn) { if (_hoverBtn) _hoverBtn.SetHover(false); _hoverBtn = hp; if (hp) hp.SetHover(true); }
                _pauseGazeT = _hoverBtn == _pauseBtn && _pauseBtn ? _pauseGazeT + dt : 0f;
                if (_hoverBtn == _pauseBtn && _pauseBtn)
                    foreach (var side in new[] { "left", "right" })
                    {
                        var h = frame.hands?.Get(side); if (h?.events == null) continue;
                        foreach (var ev in h.events)
                        {
                            if (ev is PinchStartEvent) { _pauseBtn.Click(); return true; }
                            if (ev is FireEvent && _pauseGazeT >= 0.3f) { driver.CosmeticShot(side, _pickPoint); _pauseBtn.Click(); return true; }
                        }
                    }
                return false;
            }
            if (_state == "calibrate") TickCalibrate();
            if (_state == "title" && _practice)
            {
                // 封面：手指枪练习（开枪 / 握拳交给 CoverPractice；打中大按钮、积木就等于点了它），捏合照样点按钮
                _practice.Tick(frame, gaze, dt);
                var hv = Pick(gaze);
                if (hv != _hoverBtn) { if (_hoverBtn) _hoverBtn.SetHover(false); _hoverBtn = hv; if (hv) hv.SetHover(true); }
                foreach (var side in new[] { "left", "right" })
                {
                    var h = frame.hands?.Get(side); if (h?.events == null) continue;
                    foreach (var ev in h.events) if (ev is PinchStartEvent && _hoverBtn) _hoverBtn.Click();
                }
                return true;                                                        // 封面上的手势都不交给规则层
            }
            if (_buttons.Count == 0 && !(_state == "title" && _stageBtns.Count > 0)) return false;

            var hover = Pick(gaze);
            if (hover != _hoverBtn) { if (_hoverBtn) _hoverBtn.SetHover(false); _hoverBtn = hover; if (hover) hover.SetHover(true); }
            bool consumed = false;
            foreach (var side in new[] { "left", "right" })
            {
                var h = frame.hands?.Get(side); if (h?.events == null) continue;
                foreach (var ev in h.events)
                {
                    if (ev is PinchStartEvent && _hoverBtn) { _hoverBtn.Click(); consumed = true; }
                    else if (ev is FireEvent && GunsInMenu)
                    {
                        // 所有菜单都能“开枪点按钮”（用户：一直都可以用开枪当点击）：枪照样开火（砰 + 蜡笔弹道），打中哪个按钮就点哪个
                        if (GunsInMenu) driver.CosmeticShot(side, _hoverBtn ? _pickPoint : null);
                        if (_hoverBtn) _hoverBtn.Click();
                        consumed = true;
                    }
                }
            }
            return consumed;
        }

        void CheckPalmPause(FrameInput frame, float dt)
        {
            if (Paused) return;
            var l = frame.hands?.left;
            if (l != null && l.tracked && l.state == "idle" && l.palm != null && l.palmN != null && frame.head != null)
            {
                var palm = LogicSpace.ToUnity(l.palm); var pn = LogicSpace.ToUnity(l.palmN).normalized; var hp = LogicSpace.ToUnity(frame.head.pos);
                var toHead = (hp - palm); float d = toHead.magnitude;
                bool facing = d < 0.55f && Vector3.Dot(pn, toHead / d) > 0.75f;
                _palmT = facing ? _palmT + dt : 0;
                if (_palmT > 0.9f) { _palmT = 0; driver.Pause(); }
            }
            else _palmT = 0;
        }
    }

    /// <summary>出现时弹一下（缓出带过冲 0.45 秒）。</summary>
    public class UiPopIn : MonoBehaviour
    {
        float _t;
        void Update() { _t += Time.unscaledDeltaTime; transform.localScale = Vector3.one * UiKit.Back(_t / 0.45f); if (_t > 0.5f) { transform.localScale = Vector3.one; Destroy(this); } }
    }

    /// <summary>Logo 轻轻浮动（周期 4.5 秒，上下 1 厘米）+ 微微左右摆（±3°），像悬在空中的糖果。</summary>
    public class UiFloat : MonoBehaviour
    {
        Vector3 _p0; float _t;
        void Start() { _p0 = transform.localPosition; }
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            transform.localPosition = _p0 + Vector3.up * Mathf.Sin(_t * Mathf.PI * 2f / 4.5f) * 0.012f;
            transform.localRotation = Quaternion.Euler(Mathf.Sin(_t * Mathf.PI * 2f / 6.2f) * 1.5f, Mathf.Sin(_t * Mathf.PI * 2f / 5.3f) * 3f, 0);
        }
    }

    /// <summary>柔光呼吸：大小 ±4%、亮度跟着起伏。</summary>
    public class UiPulse : MonoBehaviour
    {
        public Transform target; Vector3 _s0; float _t;
        void Start() { if (target) _s0 = target.localScale; }
        void Update() { _t += Time.unscaledDeltaTime; if (target) target.localScale = _s0 * (1f + Mathf.Sin(_t * 1.6f) * 0.04f); }
    }

    /// <summary>Logo 周围慢慢升起、闪烁的星光（Bang/Sparkle 粒子）。</summary>
    public static class UiSparkles
    {
        public static ParticleSystem Create(Transform parent, Material mat, Vector3 box, float rate)
        {
            var g = new GameObject("Sparkles"); g.transform.SetParent(parent, false);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f); main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.7f), new Color(1f, 0.8f, 0.95f)); main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.useUnscaledTime = true;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.25f, 1), new Keyframe(0.75f, 0.8f), new Keyframe(1, 0)));
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }
    }
}
