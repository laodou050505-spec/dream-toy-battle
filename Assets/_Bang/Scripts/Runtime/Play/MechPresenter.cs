// 门后机关 / 补给礼盒 / 门涌 的表现：
//   投石车：场景里的投臂改由规则层驱动（蓄力 1.6 秒 + 甩出 0.18 秒，正好在积木球出手的一刻甩到最高）；挨打晃一下、
//           血量环；散架时投臂耷拉、扬尘冒烟，20 秒后弹回。规则层的投石车位置按场景模型的投石勺校正。
//   积木球：在门后时只透过门洞看得到（模板材质），飞行中翻滚；被打掉彩纸四散，落地扬尘。
//   礼盒：飘进房间、轻轻转、周围星光；打开时盖子飞走、彩纸、横幅说明奖励。
//   门涌：门洞亮起红色脉冲光并震动 2 秒。  冰冻：小怪蒙一层蓝霜、动作停住。  金色子弹：枪发金光。
using System.Collections.Generic;
using Bang.Ambient;
using Bang.Logic;
using Bang.Portal;
using Bang.Play.UI;
using UnityEngine;

namespace Bang.Play
{
    public class MechPresenter : MonoBehaviour
    {
        public GameDriver driver;
        public GameObject ballPrefab, giftPrefab;
        [Tooltip("第 2～6 章的门后机关：迫击炮 / 城门 / 派对礼炮 / 发条钟楼 / 弹簧发射器")] public GameObject[] mechPrefabs;
        Transform _mechT; Vector3 _mechPos0; float _mechKick;
        public Material ringMat, glowMat;
        public Texture2D glowTex;

        CatapultArm _arm; Transform _armRoot; RingView _catRing; Vector3 _rootPos0; bool _catHurt;
        ParticleSystem _smoke;
        readonly Dictionary<int, (Transform t, Renderer[] rs, Material[][] normal, Material[][] portal, bool inPortal)> _balls = new();
        readonly Dictionary<int, (Transform t, RingView ring, float age)> _gifts = new();
        readonly HashSet<int> _seen = new(); readonly List<int> _gone = new();
        MeshRenderer _surgeGlow; float _surgeT = -1f; Transform _portalRoot; Vector3 _portalPos0;
        MaterialPropertyBlock _mpb;

        Game G => driver.Game;

        void Awake() { _mpb = new MaterialPropertyBlock(); }

        /// <summary>开章 / 续玩后：绑定场景里的投石车并校正规则层位置。</summary>
        public void Bind()
        {
            Clear();
            if (G.catapult == null) return;
            if (G.catapult.kind != "catapult") { BindMech(); return; }
            _arm = FindAnyObjectByType<CatapultArm>();
            if (!_arm) return;
            _arm.external = true; _arm.broken = false;
            _armRoot = _arm.transform.parent; _rootPos0 = _armRoot ? _armRoot.localPosition : Vector3.zero;
            var r = _arm.GetComponentInChildren<Renderer>();
            var cup = r ? r.bounds.center + Vector3.up * r.bounds.extents.y * 0.8f : _arm.transform.position;
            G.SetMechPos(LogicSpace.ToLogic(cup));
            _catRing = RingView.Create(transform, ringMat, 0.16f); _catRing.transform.position = cup + Vector3.up * 0.35f; _catRing.Snap(1);
            _catRing.SetColor(new Color(1f, 0.6f, 0.4f, 1));
        }

        static readonly string[] MechKinds = { "mortar", "gate", "popper", "clock", "spring" };

        /// <summary>第 2～6 章的机关：放在门后（门后摆件的空当里，不和摆件穿插），只透过门洞看得到；挨打晃、散架歪倒、修好弹回。</summary>
        void BindMech()
        {
            var c = G.catapult; int idx = System.Array.IndexOf(MechKinds, c.kind);
            var pf = mechPrefabs != null && idx >= 0 && idx < mechPrefabs.Length ? mechPrefabs[idx] : null;
            var p = c.portal;
            // 门后 3.0～3.9 米、左右 ±1.4 米里找一个离门后摆件都够远的位置
            Bang.Logic.Vec3d best = null; double bestClear = -1;
            for (int i = 0; i < 40; i++)
            {
                double back = 3.0 + (i % 4) * 0.3, side = ((i / 4) % 2 == 0 ? 1 : -1) * (0.5 + (i / 8) * 0.25);
                var q = Bang.Logic.V.add(Bang.Logic.V.sub(p.@base, Bang.Logic.V.scale(p.normal, back)), Bang.Logic.V.scale(p.right, side));
                double clear = 9; foreach (var o in G.obstacles) clear = System.Math.Min(clear, System.Math.Sqrt((q.x - o.x) * (q.x - o.x) + (q.z - o.z) * (q.z - o.z)) - o.r);
                if (clear > bestClear) { bestClear = clear; best = q; }
                if (clear > 0.6) break;
            }
            G.SetMechPos(new Bang.Logic.Vec3d(best.x, 0.45, best.z));
            var at = LogicSpace.ToUnity(best); at.y = 0;
            if (pf)
            {
                _mechT = Instantiate(pf, driver.enemyRoot).transform; _mechT.name = "Mech_" + c.kind;
                _mechT.position = at; _mechPos0 = at;
                var face = Camera.main ? Camera.main.transform.position - at : -LogicSpace.ToUnity(p.normal); face.y = 0;
                if (face.sqrMagnitude > 1e-4f) _mechT.rotation = Quaternion.LookRotation(face.normalized);
                var b = BoundsOf(_mechT); _mechT.position += Vector3.up * (0.01f - b.min.y); _mechPos0 = _mechT.position;   // 底面落地
                var world = driver.worlds != null && driver.worlds.Length > 0 ? driver.worlds[0] : null;
                if (world) foreach (var r in _mechT.GetComponentsInChildren<Renderer>(true)) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = world.VariantOf(ms[i]); r.sharedMaterials = ms; }
                foreach (var r in _mechT.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _catRing = RingView.Create(transform, ringMat, 0.16f); _catRing.transform.position = at + Vector3.up * 1.15f; _catRing.Snap(1);
            _catRing.SetColor(new Color(1f, 0.6f, 0.4f, 1));
        }

        static (string zh, string en) MechName(string k) => k switch
        {
            "mortar" => ("迫击炮", "Mortar"), "gate" => ("城门", "Gatehouse"), "popper" => ("派对礼炮", "Party Popper"),
            "clock" => ("发条钟楼", "Clock Tower"), "spring" => ("弹簧发射器", "Spring Launcher"), _ => ("投石车", "Catapult"),
        };

        static Bounds BoundsOf(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); var b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero); foreach (var r in rs) b.Encapsulate(r.bounds); return b; }

        /// <summary>机关的反馈（用户：场景要固定不动——带脸的门楼被“打倒”不对）：模型本身不晃、不缩、不歪，
        /// 蓄力时一闪一闪地发亮、出手和挨打时闪白，散架时原地冒烟、头顶转眩晕星；修好时扬一小团尘。</summary>
        void AnimateMech(float dt)
        {
            var host = _mechT ? _mechT : _armRoot; if (!host) return;
            _mechKick = Mathf.MoveTowards(_mechKick, 0, dt * 3f);
            _mechFlash = Mathf.MoveTowards(_mechFlash, 0, dt * 4f);
            bool broken = G.catapult != null && G.catapult.brokenT > 0;
            bool winding = G.catapult != null && G.catapult.winding && !broken;
            float f = Mathf.Max(_mechFlash * 0.45f, Mathf.Max(winding ? 0.12f + 0.12f * Mathf.Sin(Time.time * 14f) : 0f, _mechKick * 0.3f));
            if (Mathf.Abs(f - _flashShown) > 0.004f)
            {
                _flashShown = f;
                if (_flashRs == null) _flashRs = host.GetComponentsInChildren<Renderer>(true);
                foreach (var r in _flashRs)
                {
                    if (!r || r is ParticleSystemRenderer || !r.sharedMaterial || !r.sharedMaterial.HasProperty("_Flash")) continue;
                    r.GetPropertyBlock(_mpb); _mpb.SetFloat("_Flash", f); r.SetPropertyBlock(_mpb);
                }
            }
            if (broken)
            {
                _brokenFxT -= dt;
                if (_brokenFxT <= 0f)
                {
                    _brokenFxT = 0.55f;
                    var b = BoundsOf(host); var top = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
                    PlayFx.Instance?.Dust(top, 1.1f, 3);                                                  // 一缕缕烟从顶上冒
                    float a = Time.time * 4f;                                                              // 眩晕星：绕头顶转
                    PlayFx.Instance?.Glitter(top + new Vector3(Mathf.Cos(a), 0.06f, Mathf.Sin(a)) * Mathf.Max(0.12f, b.extents.x * 0.5f), new Color(1f, 0.88f, 0.4f), 2, 0.25f);
                }
            }
        }
        float _mechFlash, _flashShown = -1f, _brokenFxT; Renderer[] _flashRs;

        /// <summary>视线锁定准星用：机关类目标（积木球 / 礼盒 / 投石车）在场景里的位置和框大小。</summary>
        public bool TargetPos(int id, out Vector3 pos, out float size)
        {
            pos = default; size = 0.3f;
            if (_balls.TryGetValue(id, out var b) && b.t) { pos = b.t.position; size = 0.3f; return true; }
            if (_gifts.TryGetValue(id, out var g) && g.t) { pos = g.t.position; size = 0.42f; return true; }
            if (G.catapult != null && G.catapult.id == id && _mechT) { pos = BoundsOf(_mechT).center; size = 0.8f; return true; }
            if (G.catapult != null && G.catapult.id == id && _catRing) { pos = _catRing.transform.position - Vector3.up * 0.35f; size = 0.6f; return true; }
            return false;
        }

        public void Clear()
        {
            foreach (var b in _balls.Values) if (b.t) Destroy(b.t.gameObject);
            foreach (var g in _gifts.Values) { if (g.t) Destroy(g.t.gameObject); if (g.ring) Destroy(g.ring.gameObject); }
            _balls.Clear(); _gifts.Clear();
            if (_catRing) Destroy(_catRing.gameObject);
            if (_mechT) Destroy(_mechT.gameObject); _mechT = null; _flashRs = null; _flashShown = -1f;
            if (_arm) { _arm.broken = false; }
            if (_smoke) _smoke.Stop();
            _surgeT = -1f; if (_surgeGlow) _surgeGlow.enabled = false;
        }

        public void Sync(float dt)
        {
            SyncBalls();
            SyncGifts(dt);
            AnimateMech(dt);
            // 投石车挨打的晃动（阻尼正弦）
            // （投石车挨打不再晃底座：场景物件固定不动，挨打改成闪白，见 AnimateMech）
            // 门涌预警：红光脉冲 + 门框震动
            if (_surgeT >= 0)
            {
                _surgeT -= dt;
                float k = Mathf.Clamp01(_surgeT / 2f);
                if (_surgeGlow) { _surgeGlow.enabled = _surgeT > 0; _surgeGlow.GetPropertyBlock(_mpb); var gc = _glowCol; gc.a = (0.35f + 0.35f * Mathf.Sin(Time.time * _glowHz)) * Mathf.Min(1, _surgeT * 3f); _mpb.SetColor("_Color", gc); _surgeGlow.SetPropertyBlock(_mpb); }
                // （门框不再震动：场景固定不动，预警靠红光脉冲 + 声音）
            }
            if (_catRing && G.catapult != null)
            {
                _catRing.SetFill((float)(G.catapult.hp / G.catapult.maxHp));
                bool seen = G.CanSeePoint(G.catapult.portal, G.catapult.pos);   // 机关在门后：看不见（被门墙挡住）时血量环不画在墙上
                if (_catHurt) _catRing.Show(seen && G.catapult.brokenT <= 0);
            }
        }

        // ───────── 积木球 ─────────
        void SyncBalls()
        {
            _seen.Clear();
            var world = driver.worlds != null && driver.worlds.Length > 0 ? driver.worlds[0] : null;
            foreach (var b in G.bullets)
            {
                if (b.kind != "ball") continue;
                _seen.Add(b.id);
                if (!_balls.TryGetValue(b.id, out var v))
                {
                    if (!ballPrefab) continue;
                    var t = Instantiate(ballPrefab, driver.enemyRoot).transform; t.localScale = Vector3.one * 0.85f;
                    var rs = t.GetComponentsInChildren<Renderer>();
                    var nm = new Material[rs.Length][]; var pm = new Material[rs.Length][];
                    for (int i = 0; i < rs.Length; i++) { nm[i] = rs[i].sharedMaterials; pm[i] = new Material[nm[i].Length]; for (int j = 0; j < nm[i].Length; j++) pm[i][j] = world ? world.VariantOf(nm[i][j]) : nm[i][j]; rs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                    v = (t, rs, nm, pm, false);
                }
                v.t.position = LogicSpace.ToUnity(b.pos);
                v.t.rotation = Quaternion.Euler(Time.time * 260f, Time.time * 170f, 0);      // 抛出去的积木球翻滚
                // 还在墙后：只透过门洞可见
                var p = G.catapult?.portal;
                bool behind = p != null && G.PortalSide(p, b.pos) < 0.08;
                if (behind != v.inPortal) { for (int i = 0; i < v.rs.Length; i++) v.rs[i].sharedMaterials = behind ? v.portal[i] : v.normal[i]; v.inPortal = behind; }
                _balls[b.id] = v;
            }
            _gone.Clear();
            foreach (var kv in _balls) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone) { var t = _balls[id].t; _balls.Remove(id); if (t) Destroy(t.gameObject); }
        }

        // ───────── 礼盒 ─────────
        // ───────── 礼盒：金色描边 + 视野边缘的方向提示 ─────────
        static readonly int IdOutlineW = Shader.PropertyToID("_Width"), IdOutlineC = Shader.PropertyToID("_OutlineColor");
        static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap"), IdCol = Shader.PropertyToID("_Color");
        Material _giftGold;
        Transform _ptrRoot, _ptrArrow; MeshRenderer _ptrIconR, _ptrArrowR; MaterialPropertyBlock _ptrMpb; float _ptrShow;

        /// <summary>礼盒套一圈呼吸的金边：用通用描边材质换成金色，比普通敌人的描边粗。</summary>
        void GoldOutline(Transform gift)
        {
            if (!_giftGold && EnemyView.OutlineMat)
            {
                _giftGold = new Material(EnemyView.OutlineMat); _giftGold.SetColor(IdOutlineC, new Color(1f, 0.76f, 0.16f, 1f)); _giftGold.SetFloat("_DepthPush", 0f);
            }
            if (!_giftGold) return;
            foreach (var r in gift.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf) mf.sharedMesh = EnemyView.Smoothed(mf.sharedMesh);
                var mats = r.sharedMaterials; var nm = new Material[mats.Length + 1]; mats.CopyTo(nm, 0); nm[^1] = _giftGold; r.sharedMaterials = nm;
            }
        }

        MeshRenderer PtrQuad(string name, string tex, Transform parent, float size)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localScale = new Vector3(size, size, 1f);
            g.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = Resources.Load<Material>("Splat/M_HitSplat"); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ptrMpb.Clear(); var t = PtrTex(tex); if (t) _ptrMpb.SetTexture(IdBaseMap, t); r.SetPropertyBlock(_ptrMpb);
            _ptrTexOf[r] = tex;
            return r;
        }

        // 提示图标的贴图只挂在属性块上：换章时 Resources.UnloadUnusedAssets 认为没人用，把它卸掉 → 提示变成一块黑面板（用户：补给箱提醒的图标有时变成黑色面板）。
        // 这里用静态表持有贴图（强引用，不会被卸载），每帧上色时也重新带上贴图
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> PtrTexCache = new();
        readonly System.Collections.Generic.Dictionary<MeshRenderer, string> _ptrTexOf = new();
        static Texture2D PtrTex(string name)
        {
            if (!PtrTexCache.TryGetValue(name, out var t) || !t) { t = Resources.Load<Texture2D>("Splat/" + name); PtrTexCache[name] = t; }
            return t;
        }

        /// <summary>礼盒不在视野中央附近时，视野边缘出现礼盒图标 + 朝礼盒方向的金色箭头：不用回头找，顺着箭头转过去就看到了。</summary>
        void SyncGiftPointer(float dt)
        {
            var cam = Camera.main; if (!cam) return;
            Vector3 best = default; bool has = false; float bestAge = -1f;
            foreach (var g in G.gifts)
            {
                if (g.leaving || !_gifts.TryGetValue(g.id, out var v) || !v.t || v.age < bestAge) continue;
                best = v.t.position; bestAge = v.age; has = true;
            }
            var local = has ? cam.transform.InverseTransformPoint(best) : Vector3.forward;
            float off = Vector3.Angle(Vector3.forward, local);
            bool want = has && off > 26f;
            _ptrShow = Mathf.MoveTowards(_ptrShow, want ? 1f : 0f, dt * 6f);
            if (_ptrShow <= 0f) { if (_ptrRoot) _ptrRoot.gameObject.SetActive(false); return; }
            if (!_ptrRoot)
            {
                _ptrMpb = new MaterialPropertyBlock();
                _ptrRoot = new GameObject("GiftPointer").transform; _ptrRoot.SetParent(cam.transform, false);
                _ptrIconR = PtrQuad("Icon", "pointer_gift", _ptrRoot, 0.13f);
                _ptrArrowR = PtrQuad("Arrow", "pointer_arrow", _ptrRoot, 0.06f); _ptrArrow = _ptrArrowR.transform;
            }
            _ptrRoot.gameObject.SetActive(true);
            var d2 = new Vector2(local.x, local.y); if (local.z < 0 && d2.sqrMagnitude < 0.04f) d2 = new Vector2(local.x >= 0 ? 1f : -1f, 0f);
            d2 = d2.sqrMagnitude > 1e-4f ? d2.normalized : Vector2.right;
            const float dist = 0.8f;
            var p = new Vector3(d2.x * Mathf.Tan(24f * Mathf.Deg2Rad) * dist, d2.y * Mathf.Tan(16f * Mathf.Deg2Rad) * dist, dist);
            _ptrRoot.localPosition = p; _ptrRoot.localRotation = Quaternion.identity;
            float bob = 0.012f * (0.5f + 0.5f * Mathf.Sin(Time.time * 7f));
            _ptrArrow.localPosition = new Vector3(d2.x, d2.y, 0f) * (0.1f + bob);
            _ptrArrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d2.y, d2.x) * Mathf.Rad2Deg);
            foreach (var r in new[] { _ptrIconR, _ptrArrowR }) { r.GetPropertyBlock(_ptrMpb); var tx = _ptrTexOf.TryGetValue(r, out var tn) ? PtrTex(tn) : null; if (tx) _ptrMpb.SetTexture(IdBaseMap, tx); _ptrMpb.SetColor(IdCol, new Color(1f, 1f, 1f, _ptrShow)); r.SetPropertyBlock(_ptrMpb); }
        }

        // ───────── Boss 方向提示：Boss 不在视野中央附近时，视野边缘出一个皇冠图标 + 箭头（预警期间指向它要出来的那扇门）─────────
        Transform _bossPtr, _bossArrow; MeshRenderer _bossIconR, _bossArrowR; float _bossShow; Texture2D _bossTex;
        void SyncBossPointer(float dt)
        {
            var cam = Camera.main; if (!cam) return;
            Vector3 at = default; bool has = false;
            foreach (var e in G.enemies)
                if (!e.dead && e.state != "gone" && Game.IsBoss(e)) { at = LogicSpace.ToUnity(e.pos) + Vector3.up * (float)e.height * 0.5f; has = true; break; }
            if (!has && G.bossWarnT > 0 && G.run.bossPortal != null) { at = LogicSpace.ToUnity(G.run.bossPortal.center); has = true; }
            var local = has ? cam.transform.InverseTransformPoint(at) : Vector3.forward;
            bool want = has && Vector3.Angle(Vector3.forward, local) > 26f;
            _bossShow = Mathf.MoveTowards(_bossShow, want ? 1f : 0f, dt * 5f);
            if (_bossShow <= 0f) { if (_bossPtr) _bossPtr.gameObject.SetActive(false); return; }
            if (!_bossPtr)
            {
                _ptrMpb ??= new MaterialPropertyBlock();
                _bossPtr = new GameObject("BossPointer").transform; _bossPtr.SetParent(cam.transform, false);
                _bossIconR = PtrQuad("Icon", "pointer_boss", _bossPtr, 0.15f);
                _bossArrowR = PtrQuad("Arrow", "pointer_arrow", _bossPtr, 0.065f); _bossArrow = _bossArrowR.transform;
                _bossArrowR.GetPropertyBlock(_ptrMpb); _ptrMpb.SetColor(IdCol, new Color(0.9f, 0.62f, 1f, 1f)); _bossArrowR.SetPropertyBlock(_ptrMpb);
            }
            _bossPtr.gameObject.SetActive(true);
            var d2 = new Vector2(local.x, local.y); if (local.z < 0 && d2.sqrMagnitude < 0.04f) d2 = new Vector2(local.x >= 0 ? 1f : -1f, 0f);
            d2 = d2.sqrMagnitude > 1e-4f ? d2.normalized : Vector2.right;
            const float dist = 0.78f;                                     // 比礼盒提示近一点：两个同时出现时 Boss 在前
            _bossPtr.localPosition = new Vector3(d2.x * Mathf.Tan(22f * Mathf.Deg2Rad) * dist, d2.y * Mathf.Tan(14f * Mathf.Deg2Rad) * dist + 0.09f, dist); _bossPtr.localRotation = Quaternion.identity;   // 比礼盒提示高一格：两个同时出现时不叠在一起
            float bob = 0.012f * (0.5f + 0.5f * Mathf.Sin(Time.time * 5f));
            _bossArrow.localPosition = new Vector3(d2.x, d2.y, 0f) * (0.115f + bob);
            _bossArrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d2.y, d2.x) * Mathf.Rad2Deg);
            if (!_bossTex) _bossTex = PtrTex("pointer_boss");
            _bossIconR.GetPropertyBlock(_ptrMpb); if (_bossTex) _ptrMpb.SetTexture(IdBaseMap, _bossTex); _ptrMpb.SetColor(IdCol, new Color(1f, 1f, 1f, _bossShow)); _bossIconR.SetPropertyBlock(_ptrMpb);
            _bossArrowR.GetPropertyBlock(_ptrMpb); var arT = PtrTex("pointer_arrow"); if (arT) _ptrMpb.SetTexture(IdBaseMap, arT); _ptrMpb.SetColor(IdCol, new Color(0.9f, 0.62f, 1f, _bossShow)); _bossArrowR.SetPropertyBlock(_ptrMpb);
        }

        void SyncGifts(float dt)
        {
            _seen.Clear();
            foreach (var g in G.gifts)
            {
                _seen.Add(g.id);
                if (!_gifts.TryGetValue(g.id, out var v))
                {
                    if (!giftPrefab) continue;
                    var t = Instantiate(giftPrefab, driver.enemyRoot).transform; t.localScale = Vector3.zero;
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    GoldOutline(t);
                    var sp = UiSparkles.Create(t, driver.ui ? driver.ui.theme.sparkleMat : null, new Vector3(0.35f, 0.35f, 0.35f), 10);
                    sp.transform.localPosition = new Vector3(0, 0.1f, 0);
                    var ring = RingView.Create(transform, ringMat, 0.09f); ring.SetColor(new Color(1f, 0.85f, 0.35f, 1)); ring.Snap(1);
                    v = (t, ring, 0f);
                    // 礼盒冒出来：GiftSpawnFx 已经有“叮”的提示音，这里不再放出怪的“啵”
                }
                v.age += dt;
                var pos = LogicSpace.ToUnity(g.pos);
                v.t.position = pos + Vector3.down * 0.1f;
                v.t.rotation = Quaternion.Euler(Mathf.Sin(v.age * 1.7f) * 8f, v.age * 40f, Mathf.Cos(v.age * 1.3f) * 6f);
                float s = Mathf.Min(UiKit.Back(v.age / 0.4f), g.leaving ? Mathf.Clamp01((float)(1.4 * 2 + 6 - g.t) / 0.6f) : 1f);
                v.t.localScale = Vector3.one * 0.8f * s;
                v.ring.transform.position = pos + Vector3.up * 0.22f;
                if (g.hp < g.maxHp) v.ring.Show(G.CanSeePoint(g.portal, g.pos));   // 挨过打的礼盒：还在门后被墙挡住时不显示血量环
                _gifts[g.id] = v;
            }
            if (_giftGold) _giftGold.SetFloat(IdOutlineW, 0.006f + 0.0025f * Mathf.Sin(Time.time * 7f));   // 金边一呼一吸
            SyncGiftPointer(dt);
            SyncBossPointer(dt);
            _gone.Clear();
            foreach (var kv in _gifts) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone) { var v = _gifts[id]; _gifts.Remove(id); if (v.t) Destroy(v.t.gameObject); if (v.ring) Destroy(v.ring.gameObject); }
        }

        public static Color RewardColor(string rw) => rw switch
        {
            "gold" => new Color(1f, 0.82f, 0.3f), "freeze" => new Color(0.62f, 0.86f, 1f), "repair" => new Color(0.55f, 1f, 0.75f), _ => new Color(0.85f, 0.65f, 1f),
        };

        static readonly Dictionary<string, (string main, string sub)> GiftText = new()
        {
            ["rapid"] = (Loc.T("获得：无限火力！", "Got: Unlimited Fire!"), Loc.T("10 秒长枪自动连射、子弹不限，敌人涌出来了！", "10s of auto fire, no reloading. Here they come!")),
            ["gold"] = (Loc.T("获得：金色子弹！", "Got: Golden Bullets!"), Loc.T("10 秒内每一枪伤害翻倍", "Every shot does double damage for 10s")),
            ["freeze"] = (Loc.T("获得：冰冻！", "Got: Freeze!"), Loc.T("场上的玩具全部定住 4 秒", "Toys on the field freeze for 4s")),
            ["repair"] = (Loc.T("获得：修理！", "Got: Repair!"), Loc.T("盾牌修好了，两把枪的弹匣都装满了", "Shield fixed and both magazines refilled")),
            ["unflag"] = (Loc.T("获得：拔旗！", "Got: Flag Pulled!"), Loc.T("离你最近的那面旗子倒下了，玩具化退一点", "The nearest flag falls, the toy takeover shrinks")),
        };

        /// <summary>处理机关相关的 fx 事件；返回 true 表示已处理。</summary>
        public bool OnFx(FxEvent ev)
        {
            switch (ev)
            {
                case CatapultWindFx: if (_arm) _arm.Trigger(); if (_arm) BangAudio.At("mouse_wind", _arm.transform.position, 0.9f, 0.8f); return true;
                case CatapultFireFx: if (_arm) BangAudio.At("shield_block", _arm.transform.position, 0.7f, 0.8f); if (_mechT) { _mechKick = 1f; BangAudio.At(G.catapult.kind == "popper" ? "voice_pop" : "boss_blast", _mechT.position, 0.7f, 1.1f); } return true;
                case MechHitFx mh:
                    _mechFlash = 1f; _catHurt = true; _catRing?.Show(G.CanSeePoint(G.catapult?.portal, G.catapult?.pos ?? new Bang.Logic.Vec3d(0, 0, 0)));
                    BangAudio.At("hit_plastic", LogicSpace.ToUnity(mh.pos), 0.9f, 0.8f);
                    return true;
                case MechBrokenFx:
                    if (_arm) { _arm.broken = true; var p = _arm.transform.position; PlayFx.Instance?.Dust(p, 2.2f, 12); PlayFx.Instance?.Confetti(p + Vector3.up * 0.2f, 16); PlayFx.Instance?.Bo(p + Vector3.up * 0.45f); BangAudio.At("kill_scatter", p, 1f, 0.7f); BangAudio.At("kill_pop", p, 1f, 0.8f); }
                    _catHurt = false; _catRing?.Show(false);
                    if (_mechT) { PlayFx.Instance?.Dust(_mechT.position, 2.2f, 12); PlayFx.Instance?.Confetti(_mechT.position + Vector3.up * 0.5f, 30); }
                    var mn = MechName(G.catapult?.kind);
                    driver.ui?.Banner(Loc.T(mn.zh + "散架啦！", mn.en + " Wrecked!"), Loc.T("20 秒内不会再出招", "Out of action for 20 seconds"), 2.2f);
                    return true;
                case MechRepairFx:
                    if (_arm) { _arm.broken = false; PlayFx.Instance?.Dust(_arm.transform.position, 1.2f, 6); BangAudio.At("buff_repair", _arm.transform.position, 0.6f, 0.9f); }   // 机关修好：不用出怪的“啵”，免得以为又出来了敌人
                    _catRing?.Snap(1);
                    return true;
                case BallPopFx bp:
                {
                    var p = LogicSpace.ToUnity(bp.pos);
                    PlayFx.Instance?.Confetti(p, 14); PlayFx.Instance?.Dust(p, 0.8f, 5);
                    BangAudio.At("kill_pop", p, 0.8f, 1.15f); BangAudio.At("piece_knock", p, 0.6f);
                    return true;
                }
                case BallLandFx bl: { var p = LogicSpace.ToUnity(bl.pos); PlayFx.Instance?.Dust(p, 1.2f, 7); BangAudio.At("piece_knock", p, 1f, 0.8f); return true; }
                case GiftSpawnFx: driver.ui?.Banner(Loc.T("补给礼盒！", "Gift Box!"), Loc.T("打一枪打开它", "Shoot it to open"), 1.8f); BangAudio.Ui("mark", 0.9f, 1.35f); return true;
                case GiftHitFx gh:
                    if (_gifts.TryGetValue(gh.id, out var gv)) { gv.ring.Show(true); gv.ring.SetFill((float)(gh.hp / gh.maxHp), true); }
                    BangAudio.At("hit_plop", LogicSpace.ToUnity(gh.pos), 1f, 1.2f);
                    return true;
                case GiftOpenFx go:
                {
                    var p = LogicSpace.ToUnity(go.pos);
                    if (_gifts.TryGetValue(go.id, out var v) && v.t)
                    {
                        var lid = v.t.Find("Lid");
                        if (lid) PlayFx.Instance?.Throw(lid, Vector3.up * 2.2f + Random.insideUnitSphere * 0.6f, Random.insideUnitSphere * 600f, 1.4f);
                        else { v.t.localScale *= 0.01f; PlayFx.Instance?.ToyBits(p, Vector3.up, 8); }   // P2 礼盒是一整块：直接炸开
                    }
                    var col = RewardColor(go.reward);
                    PlayFx.Instance?.Confetti(p, 40);
                    PlayFx.Instance?.Flash(p, col, 0.9f, 0.45f);
                    PlayFx.Instance?.Glitter(p, col, 24, 1.4f);
                    BangAudio.At("gift_open", p, 1f); BangAudio.At("kill_pop", p, 0.8f, 1.3f); BangAudio.Ui("chapter_clear", 0.5f);
                    if (GiftText.TryGetValue(go.reward, out var tx)) driver.ui?.Banner(tx.main, tx.sub, 3.2f, false, driver.badges ? driver.badges.Icon(go.reward) : null, true, 2.6f);   // 眼前 + 图标，2.6 秒内不被别的横幅顶掉
                    // 奖励化作一串星星飞进枪里，到了才生效（徽章弹出、枪闪光、音效）
                    var gun = driver.GunTransform("right");
                    var cam = Camera.main ? Camera.main.transform : null;
                    System.Func<Vector3> to = () => gun && gun.gameObject.activeInHierarchy ? gun.position : cam ? cam.TransformPoint(new Vector3(0.1f, -0.15f, 0.4f)) : p;
                    string rw = go.reward;
                    const float fly = 0.8f;
                    if (driver.badges)
                    {
                        if (rw == "gold") driver.badges.Timed("gold", (float)G.goldT, (float)G.cfg.mech.giftGoldDur, fly);
                        else if (rw == "rapid") driver.badges.Timed("rapid", (float)G.rapidT, (float)G.cfg.mech.giftRapidDur, fly);
                        else if (rw == "freeze") driver.badges.Timed("freeze", (float)G.cfg.mech.giftFreezeDur, (float)G.cfg.mech.giftFreezeDur, 0.1f);
                        else driver.badges.Flash(rw, fly);
                    }
                    if (rw == "freeze") { BangAudio.Ui("buff_freeze", 1f); return true; }   // 冰冻：瞬间定住全场（每只小怪身上自己冒冰屑）
                    PlayFx.Instance?.Trail(p, to, col, 8, () =>
                    {
                        var at = to();
                        PlayFx.Instance?.Flash(at, col, 0.35f, 0.35f);
                        PlayFx.Instance?.Glitter(at, col, 14, 0.8f);
                        driver.Gun("right")?.Flash(col);
                        BangAudio.At(rw == "gold" ? "buff_gold" : rw == "repair" ? "buff_repair" : "reload_gold", at, 1f);
                    });
                    return true;
                }
                case GiftLeaveFx: return true;
                case FreezeFx: return true;
                case SurgeWarnFx sw:
                {
                    _surgeT = (float)sw.dur; _glowCol = new Color(1f, 0.25f, 0.3f); _glowHz = 14f;
                    EnsureSurgeGlow(sw.portal);
                    BangAudio.Ui("reload_jam", 1f); BangAudio.Ui("wave_start", 0.6f);
                    driver.ui?.Banner(Loc.T("门涌！", "Door Rush!"), Loc.T("门里要一下子冲出好几个", "A bunch of toys are about to burst out"), 2f);
                    return true;
                }
                case SurgeFx: if (_surgeGlow) BangAudio.At("surge", _surgeGlow.transform.position, 1f); else BangAudio.Ui("surge", 0.8f); return true;   // 门里一下子冲出好几个：一阵玩具哗啦声
            }
            return false;
        }

        Color _glowCol = new Color(1f, 0.25f, 0.3f); float _glowHz = 14f;
        /// <summary>Boss 即将从这扇门出来：门洞泛起慢慢起伏的紫金光（比门涌的红光慢、更沉）。</summary>
        public void BossGlow(int portalId, float dur)
        {
            if (G.portals == null || G.portals.Count == 0) return;
            _surgeT = dur; _glowCol = new Color(0.82f, 0.5f, 1f); _glowHz = 5f;
            EnsureSurgeGlow(portalId);
        }

        void EnsureSurgeGlow(int portalId)
        {
            var p = portalId < G.portals.Count ? G.portals[portalId] : G.portals[0];
            if (!_surgeGlow)
            {
                var go = new GameObject("SurgeGlow"); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
                _surgeGlow = go.AddComponent<MeshRenderer>(); _surgeGlow.sharedMaterial = glowMat; _surgeGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mpb.Clear(); if (glowTex) _mpb.SetTexture("_BaseMap", glowTex); _surgeGlow.SetPropertyBlock(_mpb);
            }
            var n = LogicSpace.ToUnity(p.normal).normalized;
            _surgeGlow.transform.SetPositionAndRotation(LogicSpace.ToUnity(p.center) + n * 0.06f, Quaternion.LookRotation(-n));
            _surgeGlow.transform.localScale = new Vector3(2.4f, 3f, 1);
            // 门涌的那扇门抖（多门章节按门号找；换章后世界和门都是新的，不能缓存旧的）
            var ws = ChapterWorlds.Instance; var dr = ws && p.id >= 0 && p.id < ws.Doors.Count ? ws.Doors[p.id] : null;
            if (!dr) { var arch = FindAnyObjectByType<PortalArch>(); if (arch) dr = arch.transform.parent; }
            if (dr && dr != _portalRoot) { if (_portalRoot) _portalRoot.localPosition = _portalPos0; _portalRoot = dr; _portalPos0 = dr.localPosition; }
        }
    }
}
