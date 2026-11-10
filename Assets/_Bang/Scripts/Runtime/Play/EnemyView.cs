// 一只小怪的表现：按规则层的 Enemy 同步位置 / 朝向，把状态翻译成 EnemyAnimInput 交给 EnemyMotion 驱动零件。
//   门后：还有一部分身体在墙后时用门后世界的模板材质（只透过门洞 / 玩具化区域看得到；真机上没有墙挡，不这样会“穿墙”）；
//         整个身体钻出门洞后切回普通材质。
//   朝向：规则层的 yaw 是瞬时转过去的，这里用缓入缓出追（转身有分量感）。
//   受击：闪白 + 朝子弹方向后仰；按剩余血量掉积木——最上面一块被打飞，上半身（头、手、背旗）随后落下一格并弹一下。
//   死亡：所有零件四散（各自翻滚、落地弹跳），喷彩纸，“啵！”。  血量环：受伤后才出现在头顶。
using System.Collections.Generic;
using Bang.Logic;
using Bang.Play.Motion;
using Bang.Portal;
using UnityEngine;

namespace Bang.Play
{
    public class EnemyView : MonoBehaviour
    {
        public int id;
        public Enemy data;
        EnemyRig _rig; EnemyMotion _motion; TargetSway _sway;
        Renderer[] _renderers; Material[][] _normal, _portal;
        bool _portalMat, _hasPortalMat;
        Vector3 _lastPos; float _yaw, _flash, _upperDrop, _upperVel, _upperOffset; bool _wasWeak;
        int _piecesLeft;
        RingView _ring, _plantRing; PortalWorld _world;
        /// <summary>玩家此刻看不看得见它（规则层 Game.CanSee，GameDriver 每帧给）：看不见时，血量环、插旗环、冰冻徽章一律不显示——不能“人没看见，框先出来”。</summary>
        public bool visible = true; bool _hpShown, _announced;
        bool _packGone;
        MaterialPropertyBlock _mpb;
        float _age; Vector3 _scaleBase = Vector3.one;
        static readonly int IdFlash = Shader.PropertyToID("_Flash"), IdBaseColor = Shader.PropertyToID("_BaseColor");
        bool _frozen;
        public static Texture2D FrostIcon; public static Material IconMat;   // 冰冻时头顶的雪花徽章
        public static Material OutlineMat;                                    // 深莓紫描边（Bang/ToyOutline）
        public static Material SparkleMat;                                    // Boss 皇冠周围的金色亮片
        static readonly System.Collections.Generic.Dictionary<Mesh, Mesh> SmoothCache = new();
        static readonly int IdRimC = Shader.PropertyToID("_RimColor"), IdRimS = Shader.PropertyToID("_RimStrength"), IdRimP = Shader.PropertyToID("_RimPower");

        /// <summary>给描边用的网格副本：UV3 里存“同一位置所有法线的平均”，硬边转角往外推时不会裂缝。按原网格缓存，所有小怪共用。</summary>
        internal static Mesh Smoothed(Mesh src)
        {
            if (!src || !src.isReadable) return src;
            if (SmoothCache.TryGetValue(src, out var m) && m) return m;
            m = Instantiate(src); m.name = src.name + "_ol";
            var v = m.vertices; var n = m.normals;
            var acc = new System.Collections.Generic.Dictionary<Vector3Int, Vector3>();
            Vector3Int K(Vector3 p) => new(Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
            for (int i = 0; i < v.Length; i++) { var k = K(v[i]); acc[k] = (acc.TryGetValue(k, out var a) ? a : Vector3.zero) + (i < n.Length ? n[i] : Vector3.up); }
            var sm = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) sm[i] = acc[K(v[i])].normalized;
            m.SetUVs(3, sm);
            SmoothCache[src] = m;
            return m;
        }

        const string OutlineHull = "OutlineHull";
        static Material _hullMat;
        /// <summary>低模外壳有些地方会鼓出高模表面（在眼睛、面板上露出碎线）：沿视线往后推 3 厘米，内部的线藏到模型后面，只留外轮廓。</summary>
        static Material HullMat
        {
            get
            {
                if (!_hullMat && OutlineMat) { _hullMat = new Material(OutlineMat); _hullMat.SetFloat("_DepthPush", 0.03f); }
                return _hullMat;
            }
        }

        /// <summary>描边 + 暖白边缘光：小怪从场景里跳出来。P2 模型的细节褶皱多，描边外壳直接套在高模上会从褶子里透出紫线、显得脏，
        /// 所以用最低一级 LOD（表面光顺）单独做外壳，挂在 LOD0 下面，飞出去时跟着走。</summary>
        void AddOutline()
        {
            var rig = GetComponent<EnemyRig>();
            foreach (var group in GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs(); if (lods.Length == 0 || lods[0].renderers.Length == 0 || !lods[0].renderers[0]) continue;
                var low = lods[Mathf.Min(2, lods.Length - 1)].renderers; if (low.Length == 0 || !low[0] || !OutlineMat) continue;
                var mf = low[0].GetComponent<MeshFilter>(); if (!mf) continue;
                var hull = new GameObject(OutlineHull); hull.transform.SetParent(lods[0].renderers[0].transform, false);
                hull.transform.SetPositionAndRotation(low[0].transform.position, low[0].transform.rotation);
                hull.transform.localScale = Vector3.one;
                hull.AddComponent<MeshFilter>().sharedMesh = Smoothed(mf.sharedMesh);
                var hr = hull.AddComponent<MeshRenderer>(); hr.sharedMaterial = HullMat; hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats.Length == 0 || !mats[0] || mats[0].shader == null || mats[0].shader.name != "Bang/ToyLit") continue;   // 只给身体零件加（不加给脚影、血量环）
                if (rig && rig.head && r.transform.IsChildOf(rig.head)) continue;                                            // 头 / 脸不描边（用户：脸上有描边太丑）
                if (r.name.Contains("Head") || r.name.Contains("Face")) continue;
                if (!r.GetComponentInParent<LODGroup>())
                {
                    var mf = r.GetComponent<MeshFilter>(); if (mf) mf.sharedMesh = Smoothed(mf.sharedMesh);
                    if (OutlineMat) { var nm = new Material[mats.Length + 1]; mats.CopyTo(nm, 0); nm[^1] = OutlineMat; r.sharedMaterials = nm; }
                }
                var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b);
                b.SetColor(IdRimC, new Color(1f, 0.96f, 0.86f, 1f)); b.SetFloat(IdRimS, 0.5f); b.SetFloat(IdRimP, 2.8f);
                r.SetPropertyBlock(b);
            }
        }
        static Material _goldHull, _bossHull;
        static Material BossHull
        {
            get
            {
                if (!_bossHull && OutlineMat) { _bossHull = new Material(OutlineMat); _bossHull.SetColor("_OutlineColor", new Color(1f, 0.7f, 0.12f, 1f)); _bossHull.SetFloat("_DepthPush", 0.16f); _bossHull.SetFloat("_Width", 0.011f); }
                return _bossHull;
            }
        }

        /// <summary>给没有 EnemyView 的 Boss 模型（终极决战谢幕里摆出来的 6 个）套上和打仗时一样的金色外轮廓描边（用户：最后 Boss 一起出来时要有个简单的描边，不然有点单调）。
        /// 做法同 AddOutline：最低一级 LOD 做外壳挂在 LOD0 下；没有 LOD 的零件直接追加描边材质。也顺手关掉投影（和打仗时一致）。</summary>
        public static void AddBossSilhouette(GameObject go)
        {
            var hullMat = BossHull; if (!hullMat) return;
            foreach (var group in go.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs(); if (lods.Length == 0 || lods[0].renderers.Length == 0 || !lods[0].renderers[0]) continue;
                var low = lods[Mathf.Min(2, lods.Length - 1)].renderers; if (low.Length == 0 || !low[0]) continue;
                var mf = low[0].GetComponent<MeshFilter>(); if (!mf) continue;
                var hull = new GameObject(OutlineHull); hull.transform.SetParent(lods[0].renderers[0].transform, false);
                hull.transform.SetPositionAndRotation(low[0].transform.position, low[0].transform.rotation);
                hull.transform.localScale = Vector3.one;
                hull.AddComponent<MeshFilter>().sharedMesh = Smoothed(mf.sharedMesh);
                var hr = hull.AddComponent<MeshRenderer>(); hr.sharedMaterial = hullMat; hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mats = r.sharedMaterials;
                if (r.name == OutlineHull || r.GetComponentInParent<LODGroup>() || mats.Length == 0 || !mats[0] || mats[0].shader == null || mats[0].shader.name != "Bang/ToyLit" || !OutlineMat) continue;
                var mf = r.GetComponent<MeshFilter>(); if (mf) mf.sharedMesh = Smoothed(mf.sharedMesh);
                var nm = new Material[mats.Length + 1]; mats.CopyTo(nm, 0); nm[^1] = OutlineMat; r.sharedMaterials = nm;
            }
        }
        Material _baseHull;                                                   // 平时的外壳描边：普通小怪 = 深莓紫，Boss = 一圈粗金边
        float _hlT;
        /// <summary>金色描边闪几秒：第一次见到的新玩具、Boss 登场 / 换阶段时，让玩家一眼认出“说的就是它”。</summary>
        public void Highlight(float sec)
        {
            if (!_goldHull && OutlineMat) { _goldHull = new Material(OutlineMat); _goldHull.SetColor("_OutlineColor", new Color(1f, 0.78f, 0.2f, 1f)); _goldHull.SetFloat("_DepthPush", 0.03f); _goldHull.SetFloat("_Width", 0.007f); }
            _hlT = sec; SetHullMat(_goldHull);
        }

        // ───────── Boss 标识（用户：要让玩家一眼知道这个是 Boss，跟小怪不一样）─────────
        // 头顶漂着一顶慢慢转的糖果金皇冠（带金色亮片）+ 常亮的粗金描边 + 脚下一圈缓慢呼吸的金色光圈。动作都慢，不闪不晃（舒适）。
        Transform _crownPivot, _crown, _halo; MeshRenderer _haloR; float _crownTop; static Texture2D _haloTex; static Material _crownMat, _gemMat;
        void AddBossDecor(Enemy e)
        {
            var bh = BossHull;                                                  // 粗金边（只留外轮廓）
            if (_bossHull) { _baseHull = _bossHull; SetHullMat(_bossHull); }
            var src = Resources.Load<GameObject>("BossFx/BossCrown");
            if (!_crownMat) _crownMat = Resources.Load<Material>("BossFx/M_CrownVC");     // 金身：ToyLit._Metal 金属反射
            if (!_gemMat) _gemMat = Resources.Load<Material>("BossFx/M_CrownGemVC");     // 宝石 / 糖珠：亮面高光
            var gems = Resources.Load<GameObject>("BossFx/BossCrownGems");
            if (src)
            {
                // 皇冠挂在头上（掉积木时跟着上半身往下落一格）；没有头的整块模型挂在根上，放在包围盒顶上
                var parent = _rig && _rig.head ? _rig.head : _rig && _rig.upper ? _rig.upper : transform;
                var b = new Bounds(transform.position + Vector3.up * (float)e.height * transform.lossyScale.y, Vector3.zero); bool any = false;
                foreach (var r in GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                _crownPivot = new GameObject("BossCrownPivot").transform; _crownPivot.SetParent(parent, false);
                _crownPivot.position = new Vector3(transform.position.x, b.max.y + 0.05f, transform.position.z);
                _crownTop = _crownPivot.localPosition.y;
                var c = Instantiate(src, _crownPivot); c.name = "BossCrown"; _crown = c.transform;
                if (gems) { var gi = Instantiate(gems, _crown); gi.name = "Gems"; foreach (var r in gi.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = _gemMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; } }
                foreach (var col in c.GetComponentsInChildren<Collider>()) Destroy(col);
                foreach (var r in c.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.sharedMaterial == _gemMat) continue;                                      // 宝石不描边
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mf = r.GetComponent<MeshFilter>(); if (mf) mf.sharedMesh = Smoothed(mf.sharedMesh);
                    var body = _crownMat ? _crownMat : r.sharedMaterial;
                    r.sharedMaterials = _bossHull ? new[] { body, _bossHull } : new[] { body };   // 皇冠和 Boss 身上同一圈金描边（用户：王冠也要有 Boss 一样的描边）
                }
                float ws = 1.9f;                                                     // 模型直径 0.16 m → 约 0.3 m，Boss 头顶上远远就看得见
                var ls = parent.lossyScale; _crown.localScale = new Vector3(ws / Mathf.Max(1e-3f, ls.x), ws / Mathf.Max(1e-3f, ls.y), ws / Mathf.Max(1e-3f, ls.z));
                if (SparkleMat) { var sp = Bang.Play.UI.UiSparkles.Create(_crownPivot, SparkleMat, new Vector3(0.4f, 0.25f, 0.4f) / Mathf.Max(1e-3f, ls.y), 7); sp.transform.localPosition = Vector3.up * 0.08f / Mathf.Max(1e-3f, ls.y); }
            }
            // 脚下金色光圈：不进门后材质系统，挂在敌人根的父节点上，整个身体出了门才显示
            if (!_haloTex)
            {
                const int N = 128; _haloTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BossHalo" };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float r = new Vector2(x - N / 2 + 0.5f, y - N / 2 + 0.5f).magnitude / (N / 2f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.09f); ring = ring * ring * (3 - 2 * ring);
                    float glow = Mathf.Clamp01(1f - r) * 0.35f;                         // 圈里一层淡淡的金
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ring + glow) * 255));
                }
                _haloTex.SetPixels32(px); _haloTex.Apply();
            }
            var hg = new GameObject("BossHalo"); _halo = hg.transform; _halo.SetParent(transform.parent, false);
            hg.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
            _haloR = hg.AddComponent<MeshRenderer>(); _haloR.sharedMaterial = Resources.Load<Material>("Splat/M_HitSplat"); _haloR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mp = new MaterialPropertyBlock(); mp.SetTexture("_BaseMap", _haloTex); _haloR.SetPropertyBlock(mp);
            float hs = Mathf.Max(0.7f, (float)e.radius * 3.2f); _halo.localScale = new Vector3(hs, hs, 1f);
            hg.SetActive(false);
        }

        // Boss 换阶段后的护盾（规则层 bossGuardT，期间只吃 35% 伤害）：全身边缘光转成亮蓝、一呼一吸，玩家看得出“现在打它不疼”
        // ───── Boss 摆臂（用户：Boss 不要笔直站着，有胳膊就稍微抬一抬）─────
        // 模型是一整块网格，没有手臂骨骼：着色器把身体两侧（模型局部 |z| > 半宽）的顶点绕肩转一个小角度（ToyLit / ToyOutline 的 ArmSwing）。
        // 巡游时左右手轮流慢慢摆（约 ±10°），蓄力时双手抬起（约 25°），出招那一下往外甩，踉跄时垂下。只给有手臂的 Boss
        static readonly int IdArm = Shader.PropertyToID("_ArmSwing");
        static readonly Dictionary<string, (float shoulder, float half)> ArmRig = new()   // 模型局部（归一化到 ±0.5）：肩高、身体半宽
        {
            ["boss1"] = (0.22f, 0.24f),     // 积木巨人：两只积木大拳头
            ["boss3"] = (0.18f, 0.12f),     // 锡兵大将军：权杖手 / 发条钥匙
            ["boss5"] = (0.2f, 0.2f),       // 发条巨偶：两只大手
        };
        float _armA, _armT; MaterialPropertyBlock _armMpb;
        void SyncArms(float dt)
        {
            if (!ArmRig.TryGetValue(data.kind, out var rig)) return;
            _armT += dt;
            // 只往外抬（正值 = 手臂绕肩往外、往上张开；实测负值是往身体里收，会把拳头压进身体穿模）
            float target = data.bossStage switch
            {
                1 => 0.32f,                                                    // 蓄力：双手往外抬起
                2 => 0.18f + 0.12f * Mathf.Abs(Mathf.Sin(_armT * 9f)),         // 出招：往外甩一下
                3 => 0.04f,                                                    // 踉跄：手垂下
                _ => 0.06f + 0.07f * (0.5f + 0.5f * Mathf.Sin(_armT * 1.8f)),  // 巡游：一张一合慢慢摆
            };
            _armA = Mathf.Lerp(_armA, target, 1f - Mathf.Exp(-dt * 5f));
            _armMpb ??= new MaterialPropertyBlock();
            var v = new Vector4(_armA, rig.shoulder, rig.half, 0.6f);
            foreach (var r in _renderers)
            {
                if (!r || !(r is MeshRenderer) || r.name.StartsWith("BossCrown")) continue;
                r.GetPropertyBlock(_armMpb); _armMpb.SetVector(IdArm, v); r.SetPropertyBlock(_armMpb);
            }
        }
        float _guardK = -1f;
        void SyncGuard(float dt)
        {
            float want = data.bossGuardT > 0 && data.state != "stagger" ? 1f : 0f;
            float k = Mathf.MoveTowards(Mathf.Max(0, _guardK), want, dt * 3f);
            if (Mathf.Approximately(k, _guardK)) return;
            _guardK = k;
            var pulse = 0.85f + 0.15f * Mathf.Sin(_age * 5f);
            var c = Color.Lerp(new Color(1f, 0.96f, 0.86f, 1f), new Color(0.45f, 0.8f, 1f, 1f), k);
            foreach (var r in _renderers)
            {
                if (!r || !(r is MeshRenderer)) continue;
                r.GetPropertyBlock(_mpb); _mpb.SetColor(IdRimC, c); _mpb.SetFloat(IdRimS, Mathf.Lerp(0.5f, 1.6f * pulse, k)); _mpb.SetFloat(IdRimP, Mathf.Lerp(2.8f, 1.6f, k)); r.SetPropertyBlock(_mpb);
            }
        }

        void SyncBossDecor(float dt)
        {
            if (_crownPivot)
            {
                _crownPivot.localPosition = new Vector3(_crownPivot.localPosition.x, _crownTop, _crownPivot.localPosition.z);
                if (_crown)
                {
                    _crown.position = _crownPivot.position + Vector3.up * (0.025f * Mathf.Sin(_age * 1.6f));
                    _crown.rotation = Quaternion.Euler(Mathf.Sin(_age * 1.1f) * 5f, _age * 35f, Mathf.Cos(_age * 0.9f) * 5f);   // 慢慢转、轻轻歪：一看就是“戴着皇冠的那个”
                }
            }
            if (_halo)
            {
                bool show = !_portalMat && !data.dead;
                _halo.gameObject.SetActive(show);
                if (show)
                {
                    _halo.position = new Vector3(transform.position.x, transform.position.y + 0.012f, transform.position.z);
                    _halo.rotation = Quaternion.Euler(90f, _age * 12f, 0f);
                    var mp = new MaterialPropertyBlock(); _haloR.GetPropertyBlock(mp);
                    mp.SetColor("_Color", new Color(1f, 0.78f, 0.28f, 0.45f + 0.2f * Mathf.Sin(_age * 2.2f))); _haloR.SetPropertyBlock(mp);
                }
            }
        }
        void SetHullMat(Material m)
        {
            if (!m) return;
            // 外壳描边换颜色（新玩具金边、Boss 金边）也要按门后 / 房间两套材质缓存来换：原来直接写 renderer，
            // 换上的是不带门洞模板的材质，门后的敌人整圈轮廓画到了墙上（用户：没看见他，他的框就出来了）
            if (_normal != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var r = _renderers[i]; if (!r) continue;
                    bool hull = r.name == OutlineHull, ch = false;
                    for (int j = 0; j < _normal[i].Length; j++)
                    {
                        var x = _normal[i][j];
                        if (!hull && !(x && (x == OutlineMat || x == _goldHull || x == HullMat || x == _bossHull))) continue;
                        var n = hull ? m : (m == HullMat ? OutlineMat : m);
                        _normal[i][j] = n; _portal[i][j] = _world ? _world.VariantOf(n) : n; ch = true;
                    }
                    if (ch) r.sharedMaterials = _portalMat ? _portal[i] : _normal[i];
                }
                return;
            }
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == OutlineHull) { r.sharedMaterial = m; continue; }
                var ms = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < ms.Length; i++) if (ms[i] && (ms[i] == OutlineMat || ms[i] == _goldHull || ms[i] == HullMat || ms[i] == _bossHull)) { ms[i] = m == HullMat ? OutlineMat : m; ch = true; }
                if (ch) r.sharedMaterials = ms;
            }
        }
        Transform _frost; MeshRenderer _frostR; float _frostShow;

        public void Init(Enemy e, PortalWorld world, Material ringMat, Material blobMat = null)
        {
            id = e.id; data = e; _world = world;
            _rig = GetComponent<EnemyRig>(); _motion = GetComponent<EnemyMotion>(); _sway = GetComponent<TargetSway>();
            if (_motion && Game.IsBoss(e)) _motion.calm = true;               // Boss 动作要稳，不抽搐
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 用脚下软影子代替
            AddOutline();
            if (Game.IsBoss(e)) AddBossDecor(e);                              // 要在收集 renderer 之前：皇冠和金描边一起生成门后材质
            if (blobMat && !_sway) BlobShadow.Attach(transform, blobMat, Mathf.Max(0.12f, (float)e.radius * 1.1f), _rig ? _rig.body : null);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _normal = new Material[_renderers.Length][]; _portal = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _normal[i] = _renderers[i].sharedMaterials;
                _portal[i] = new Material[_normal[i].Length];
                for (int j = 0; j < _normal[i].Length; j++) _portal[i][j] = world ? world.VariantOf(_normal[i][j]) : _normal[i][j];
            }
            _hasPortalMat = world;
            _piecesLeft = _rig && _rig.pieces != null ? _rig.pieces.Length : 0;
            transform.position = _lastPos = LogicSpace.ToUnity(e.pos);
            _yaw = LogicSpace.YawDeg(e.yaw);
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            float ringY = (float)e.height + 0.1f;
            _ring = RingView.Create(transform, ringMat, Mathf.Clamp((float)e.radius * 0.6f, 0.07f, 0.16f));
            _ring.transform.localPosition = new Vector3(0, ringY, 0);
            _ring.Snap(1);
            // 插旗进度环（琥珀色，在血量环上面一点）：小怪开始插旗时出现、按进度填满，快插好时闪——告诉玩家“还来得及打断”
            _plantRing = RingView.Create(transform, ringMat, Mathf.Clamp((float)e.radius * 0.75f, 0.09f, 0.2f));
            _plantRing.transform.localPosition = new Vector3(0, ringY + 0.12f, 0);
            _plantRing.SetColor(new Color(1f, 0.72f, 0.25f, 1)); _plantRing.Snap(0);
            _mpb = new MaterialPropertyBlock();
            if (_motion)
            {
                _motion.Landed += p =>
                {
                    bool heavy = _rig.style == MoveStyle.Stomp;
                    PlayFx.Instance?.Dust(p, heavy ? 1.5f : 0.6f, heavy ? 8 : 4);
                    BangAudio.At(heavy ? "stomp" : "step_tap", p, heavy ? 0.55f : 0.25f);
                };
                _motion.PlantStab += () => PlayFx.Instance?.Dust(transform.position + transform.forward * 0.06f, 0.8f, 6);
            }
            SetPortalMat(true);
            UpdatePortalMat();
            // 出场：从门后世界的小路上“蹦”出来——缩放 0 → 过冲 → 回正，脚下一圈星光尘
            _scaleBase = transform.localScale; transform.localScale = Vector3.zero;
            PlayFx.Instance?.Dust(transform.position, 0.9f, 6);
            // 出场的“啵”不在生成时响：小怪生在门墙后面、玩家看不见，这时响一声像是“有东西出来了却没看到 / 漏掉了”（用户：音效不要让玩家误会）。
            // 改成第一次进入玩家视野（Game.CanSee 变成真）时再响，见 Sync
        }

        float _groundY;
        /// <summary>贴着脚下的路面 / 门后地形走，不陷进地里（飞的、飘的不管）。平滑一下，走上积木路时不跳变。</summary>
        float GroundLift(Vector3 pos, float dt)
        {
            if (_rig && (_rig.style == MoveStyle.Fly || _rig.style == MoveStyle.Float) || pos.y > 0.3f) return 0f;
            var p = data.portal;
            bool behind = p != null && (data.pos.x - p.@base.x) * p.normal.x + (data.pos.z - p.@base.z) * p.normal.z < 0;
            float want = GroundProbe.Height(pos, behind, Mathf.Min(0.14f, (float)data.radius));
            _groundY = _age < 0.05f ? want : Mathf.MoveTowards(_groundY, want, dt * 1.2f);
            return _groundY;
        }

        void SetPortalMat(bool on)
        {
            if (!_hasPortalMat || _portalMat == on) return;
            _portalMat = on;
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i]) _renderers[i].sharedMaterials = on ? _portal[i] : _normal[i];
        }

        void UpdatePortalMat()
        {
            var p = data.portal; if (p == null) return;
            double side = (data.pos.x - p.@base.x) * p.normal.x + (data.pos.z - p.@base.z) * p.normal.z;
            // 整个身体（含半径）都到了墙这一侧才用普通材质；往回走时一碰到墙面就切回门后材质
            SetPortalMat(side < data.radius + 0.03);
            // 跨在门面上（身子一半在门里一半在门外）：原来整个身子用门后材质（只透过门洞画），伸进房间的那一半在门洞外就被裁掉
            //（用户：敌人经过门框时模型缺一块）。现在跨门期间多画一份：门后材质只画门面后面，房间材质只画门面前面，两半在门面处严丝合缝
            float r = Mathf.Max((float)data.radius, BodyHalf());
            SetStraddle(_hasPortalMat && side > -r - 0.05f && side < r + 0.08f, p);
            if (Game.IsBoss(data)) SqueezeThroughDoor(p, (float)side, r);
        }

        // ───── Boss 比门洞宽（积木巨人 1.5 米，门洞 1.3 米）：用户：Boss 会有一半在门后面 ─────
        // 跨门时横向压扁到门洞宽度以内、稍微拉高一点（卡通“挤出来”），离开门面 0.4 米内弹回原样；比门洞窄的 Boss 不变
        float _bossHalfW = -1f, _squeeze = 1f; Vector3 _scale0;
        float BossHalfWidth()
        {
            if (_bossHalfW >= 0) return _bossHalfW;
            float mx = 0f; var lg = GetComponentInChildren<LODGroup>();
            var parts = new List<Renderer>();
            if (lg && lg.GetLODs().Length > 0) parts.AddRange(lg.GetLODs()[0].renderers);
            foreach (var mr in GetComponentsInChildren<MeshRenderer>()) if (!mr.GetComponentInParent<LODGroup>() && mr.GetComponent<MeshFilter>() && !mr.name.StartsWith("Boss") && mr.name != "Ring" && mr.name != "BlobShadow" && !mr.name.EndsWith("_Twin") && mr.name != OutlineHull) parts.Add(mr);
            foreach (var pr in parts)
            {
                if (!pr) continue; var mf = pr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var bb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = transform.InverseTransformPoint(pr.transform.TransformPoint(c));
                    mx = Mathf.Max(mx, Mathf.Abs(lp.x));
                }
            }
            _bossHalfW = mx; _scale0 = transform.localScale; return _bossHalfW;
        }
        void SqueezeThroughDoor(Bang.Logic.Portal p, float side, float r)
        {
            float half = BossHalfWidth(); if (half <= 0.01f) return;
            float fit = Mathf.Clamp01(((float)p.w * 0.5f - 0.06f) / half);
            if (fit >= 0.999f && _squeeze >= 0.999f) return;                 // 比门洞窄：什么都不做
            float t = Mathf.Clamp01(1f - (Mathf.Abs(side) - r) / 0.4f);       // 跨门 = 1，离门面 r+0.4 米以外 = 0
            float want = Mathf.Lerp(1f, fit, t);
            _squeeze = Mathf.MoveTowards(_squeeze, want, Time.deltaTime * 2.5f);
            transform.localScale = new Vector3(_scale0.x * _squeeze, _scale0.y * (1f + (1f - _squeeze) * 0.3f), _scale0.z);
        }

        // ───── 跨门双份渲染 ─────
        Renderer[] _twin; bool _straddle; MaterialPropertyBlock _clipMpb; static readonly int IdClip = Shader.PropertyToID("_ClipPlane");
        float _bodyHalf = -1f;
        float BodyHalf()
        {
            if (_bodyHalf >= 0) return _bodyHalf;
            var b = new Bounds(transform.position, Vector3.zero); bool any = false;
            foreach (var r in _renderers) if (r && r is MeshRenderer) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            _bodyHalf = any ? Mathf.Max(b.extents.x, b.extents.z) : 0.2f; return _bodyHalf;
        }
        void SetStraddle(bool on, Bang.Logic.Portal p)
        {
            if (!on && !_straddle) return;                                    // 平时什么都不做（不改属性块、不耗 CPU）
            if (on && _twin == null) BuildTwin();
            if (_twin == null) return;
            _clipMpb ??= new MaterialPropertyBlock();
            var n = new Vector3(-(float)p.normal.x, 0, (float)p.normal.z).normalized;   // 门面法线（Unity 世界坐标，指向房间）
            var o = LogicSpace.ToUnity(p.@base);
            var room = new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, o)); var back = -room;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i]; var t = _twin[i];
                if (!r || !(r is MeshRenderer)) { if (t) t.enabled = false; continue; }
                r.GetPropertyBlock(_clipMpb); _clipMpb.SetVector(IdClip, on ? (_portalMat ? back : room) : Vector4.zero); r.SetPropertyBlock(_clipMpb);
                if (!t) continue;
                t.enabled = on && r.enabled && r.isVisible;                   // LOD 没选中这一级的零件，双份也不画
                if (!on) continue;
                t.sharedMaterials = _portalMat ? _normal[i] : _portal[i];     // 另一套材质（门后 ↔ 房间），裁到门面另一侧；闪白 / 冰冻颜色随本体同步
                _clipMpb.SetVector(IdClip, _portalMat ? room : back); t.SetPropertyBlock(_clipMpb);
            }
            _straddle = on;
        }
        void BuildTwin()
        {
            _twin = new Renderer[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i]; if (!r || !(r is MeshRenderer)) continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var g = new GameObject(r.name + "_Twin"); g.transform.SetParent(r.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var tr = g.AddComponent<MeshRenderer>(); tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.enabled = false;
                _twin[i] = tr;
            }
        }

        public void Sync(float dt)
        {
            var e = data;
            _age += dt;
            if (_hlT > 0) { _hlT -= Time.deltaTime; if (_hlT <= 0) SetHullMat(_baseHull ? _baseHull : HullMat); }
            if (_crownPivot || _halo) SyncBossDecor(dt);
            if (Game.IsBoss(e)) { SyncGuard(dt); SyncArms(dt); }
            if (_ring && _hpShown && !e.dead) _ring.Show(visible);
            if (visible && !_announced && !e.dead) { _announced = true; BangAudio.At("emerge", transform.position, 0.6f); if (e.kind == "mouse") BangAudio.At("mouse_squeak", transform.position, 0.5f, 1.15f); }        // 挨过打的血量环：走回门墙后面就收起，出来再弹出
            if (_age < 0.4f) { float x = Mathf.Clamp01(_age / 0.32f) - 1f; transform.localScale = _scaleBase * (1f + 2.70158f * x * x * x + 1.70158f * x * x); }
            else if (_age - dt < 0.4f) transform.localScale = _scaleBase;
            var pos = LogicSpace.ToUnity(e.pos);
            var vel = dt > 1e-5f ? (pos - _lastPos) / dt : Vector3.zero;
            _lastPos = pos;
            transform.position = pos + Vector3.up * GroundLift(pos, dt);
            float targetYaw = LogicSpace.YawDeg(e.yaw);
            bool facePlayer = _sway || e.k.ai == "balloon";                    // 用户：气球敌人要尽量看向玩家（飘着走、飞走时脸也朝着玩家，不拿后脑勺对人）
            if (facePlayer && Camera.main) { var d = Camera.main.transform.position - pos; d.y = 0; if (d.sqrMagnitude > 1e-4f) targetYaw = Quaternion.LookRotation(d).eulerAngles.y; }   // 靶子 / 气球始终正对玩家
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-dt * (_rig && _rig.style == MoveStyle.Scurry ? 14f : 8f)));
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            UpdatePortalMat();

            if (_motion)
            {
                var a = new EnemyAnimInput
                {
                    moving = vel.sqrMagnitude > 0.0004f,
                    planting = e.state == "plant",
                    plantT = e.state == "plant" && e.k.plantTime > 0 ? (float)(e.t / e.k.plantTime) : 0,
                    paused = e.paused,
                    frozen = e.frozenT > 0,
                    cheering = e.state == "leave" && e.t < 0.9 && !e.dead && _packGone,
                    aiming = e.tele || (e.state == "aim" && e.fireT < e.k.telegraph + 0.15),
                    popOpen = e.state == "open",
                    velocity = vel,
                    working = e.healing || (e.kind == "commander" && e.state == "command" && e.order != null && e.orderAge < 1.2),
                    role = e.k.role == "support" ? 2 : e.k.role == "shoot" ? 1 : 0,
                    bossStage = e.bossStage, bossMove = e.bossMove,
                    bossK = e.bossStage == 1 && e.k.telegraph > 0 ? Mathf.Clamp01((float)(e.bossMoveT / e.k.telegraph)) : 0f,
                };
                _motion.Tick(a, dt);
            }
            if (_plantRing)
            {
                bool planting = e.state == "plant" && !e.dead && e.k.plantTime > 0;
                float pt = planting ? Mathf.Clamp01((float)(e.t / e.k.plantTime)) : 0;
                _plantRing.Show(planting && visible);
                if (planting) { _plantRing.SetFill(pt); _plantRing.SetColor(Color.Lerp(new Color(1f, 0.72f, 0.25f, 1), new Color(1f, 0.4f, 0.35f, 1), Mathf.SmoothStep(0, 1, (pt - 0.5f) / 0.5f))); }   // 快插完时由琥珀渐变成红（原来 8Hz 红闪）
            }
            // 插完旗：背上的小旗没了（插到地上去了）
            if (!_packGone && e.state == "leave" && e.spot == null && _rig && _rig.pack)
            {
                _packGone = true;
                PlayFx.Instance?.Throw(Detach(_rig.pack), Vector3.zero, Vector3.zero, 0.25f);
            }

            // 掉积木后上半身落下一格：弹簧（带一点回弹）
            float targetDrop = _rig ? (_rig.pieces.Length - _piecesLeft) * _rig.pieceHeight : 0;
            _upperVel += ((targetDrop - _upperDrop) * 260f - _upperVel * 16f) * dt;
            _upperDrop += _upperVel * dt;
            if (_rig && _rig.upper) _rig.upper.localPosition = _rig.upperRest + Vector3.down * _upperDrop;

            // 冰冻：蒙一层蓝霜（动作由 EnemyAnimInput.frozen 停住）+ 冰蓝亮片 + 头顶转着的雪花徽章（剩 1 秒时闪烁）；解冻时“咔”地抖落冰屑
            bool fr = e.frozenT > 0;
            var top = transform.position + Vector3.up * ((float)e.height * transform.lossyScale.y + 0.12f);
            if (fr != _frozen)
            {
                _frozen = fr;
                foreach (var r in _renderers) { if (!r) continue; r.GetPropertyBlock(_mpb); _mpb.SetColor(IdBaseColor, fr ? new Color(0.62f, 0.82f, 1f, 1f) : Color.white); r.SetPropertyBlock(_mpb); }
                PlayFx.Instance?.Glitter(transform.position + Vector3.up * (float)e.height * 0.6f, new Color(0.7f, 0.9f, 1f), fr ? 14 : 10, fr ? 0.9f : 1.4f);
                if (!fr) BangAudio.At("piece_knock", transform.position, 0.5f, 1.6f);
            }
            _frostShow = Mathf.MoveTowards(_frostShow, fr && visible ? 1 : 0, dt * 5f);
            if (_frostShow > 0 && FrostIcon && IconMat)
            {
                if (!_frost)
                {
                    var g = new GameObject("FrostBadge"); _frost = g.transform; _frost.SetParent(transform.parent, false);
                    g.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
                    _frostR = g.AddComponent<MeshRenderer>(); _frostR.sharedMaterial = IconMat; _frostR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mp = new MaterialPropertyBlock(); mp.SetTexture("_BaseMap", FrostIcon); _frostR.SetPropertyBlock(mp);
                }
                _frost.gameObject.SetActive(true);
                float x = _frostShow - 1f, pop = 1f + 2.70158f * x * x * x + 1.70158f * x * x;
                bool blink = fr && e.frozenT < 1.0 && Mathf.Repeat(Time.time * 2f, 1f) < 0.4f;   // 快解冻：2Hz 慢闪（原 6Hz）
                _frost.position = top + Vector3.up * Mathf.Sin(_age * 3f) * 0.01f;
                var cam = Camera.main;
                if (cam) _frost.rotation = Quaternion.LookRotation(_frost.position - cam.transform.position, cam.transform.up) * Quaternion.Euler(0, 0, _age * 40f);
                _frost.localScale = Vector3.one * 0.075f * Mathf.Max(0, pop) * (blink ? 0.85f : 1f);
            }
            else if (_frost) _frost.gameObject.SetActive(false);
            // 闪白
            // 弱点时机（插旗、小丑盒打开）：身体一闪一闪，告诉玩家现在打伤害翻倍
            // 弱点：原来 10Hz 闪烁（违反“不闪”）→ Boss 随蓄力慢慢变亮，小怪 0.5Hz 慢呼吸
            float weak = e.dead || !Game.IsWeak(e) ? 0f : Game.IsBoss(e) ? 0.08f + 0.2f * (e.bossStage == 1 && e.k.telegraph > 0 ? Mathf.Clamp01((float)(e.bossMoveT / e.k.telegraph)) : 1f) : 0.2f + 0.08f * Mathf.Sin(Time.time * 3f);
            if (_flash > 0 || weak > 0 || _wasWeak)
            {
                _flash = Mathf.Max(0, _flash - dt * 7f); _wasWeak = weak > 0;
                float shown = Mathf.Max(_flash, weak);
                foreach (var r in _renderers) { if (!r) continue; r.GetPropertyBlock(_mpb); _mpb.SetFloat(IdFlash, shown); r.SetPropertyBlock(_mpb); }
            }
        }

        /// <summary>被打中：dirWorld = 子弹飞行方向。</summary>
        public void OnHit(Vector3 dirWorld, double hp, double maxHp)
        {
            _flash = 1f;
            _motion?.Hit(dirWorld, 1f);
            if (_sway) _sway.Hit(dirWorld);
            _hpShown = true; _ring.Show(visible);
            _ring.SetFill((float)(hp / maxHp), true);
            // 按剩余血量该剩几块积木（最后一块随死亡一起散）；整块的 P2 模型没有积木可掉，迸两个小零件
            if (_rig && _rig.pieces != null && _rig.pieces.Length > 0)
            {
                int keep = Mathf.Max(1, Mathf.CeilToInt((float)(hp / maxHp) * _rig.pieces.Length));
                while (_piecesLeft > keep) PopPiece(dirWorld);
            }
            else PlayFx.Instance?.ToyBits(transform.position + Vector3.up * (float)data.height * 0.6f, dirWorld, 3);
        }

        void PopPiece(Vector3 dir)
        {
            _piecesLeft--;
            var t = _rig.pieces[_piecesLeft];
            if (!t) return;
            dir.y = 0; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : -transform.forward;
            var side = Vector3.Cross(Vector3.up, dir) * Random.Range(-0.6f, 0.6f);
            BangAudio.At("piece_knock", t.position, 0.8f);
            PlayFx.Instance?.Throw(Detach(t), (dir + side) * Random.Range(0.7f, 1.0f) + Vector3.up * Random.Range(1.0f, 1.4f), Random.insideUnitSphere * 540f, 1.4f);
        }

        Transform Detach(Transform t)
        {
            // 零件的材质换回普通材质（甩出去后不再受门洞限制），并从 renderer 列表里摘掉
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                int i = System.Array.IndexOf(_renderers, r);
                if (i >= 0) { r.sharedMaterials = _normal[i]; _renderers[i] = null; }
                r.SetPropertyBlock(null);
            }
            return t;
        }

        /// <summary>LOD 的低精度层：死亡时只甩最高精度那份，其余随根物体一起销毁。</summary>
        bool LowLod(Transform t)
        {
            for (; t && t != transform; t = t.parent) if (t.name == "LOD1" || t.name == "LOD2" || t.name == "LOD3") return true;
            return false;
        }

        /// <summary>被打死：零件四散 + 彩纸 + “啵！”。</summary>
        public void Die(Vector3 dirWorld)
        {
            if (_frost) Destroy(_frost.gameObject);
            if (_halo) Destroy(_halo.gameObject);
            var center = transform.position + Vector3.up * (float)data.height * 0.5f;
            dirWorld.y = 0; dirWorld = dirWorld.sqrMagnitude > 1e-4f ? dirWorld.normalized : -transform.forward;
            if (_sway && _sway.balloon)                                   // 气球靶：气球“啵”地爆掉（直接消失），靶盘掉下去
            {
                PlayFx.Instance?.Confetti(_sway.balloon.position + Vector3.up * 0.3f, 18);
                Destroy(_sway.balloon.gameObject); _sway.balloon = null; _sway.enabled = false;
            }
            var parts = new List<Transform>();
            foreach (var mf in GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<BlobShadow>() && mf.name != "Ring" && mf.name != OutlineHull && !LowLod(mf.transform)) parts.Add(mf.transform);
            if (_rig && _rig.pieces != null && _rig.pieces.Length == 0) PlayFx.Instance?.ToyBits(center, dirWorld, 14);   // 整块的 P2 模型：身体翻滚飞出 + 迸零件
            foreach (var t in parts)
            {
                if (!t || t == transform) continue;
                var away = t.position - center; away.y = 0;
                var v = (away.normalized * 0.8f + dirWorld * 0.7f) * Random.Range(0.6f, 1.2f) + Vector3.up * Random.Range(1.2f, 2.0f);
                PlayFx.Instance?.Throw(Detach(t), v, Random.insideUnitSphere * 720f, Random.Range(1.1f, 1.6f));
            }
            PlayFx.Instance?.Confetti(center);
            PlayFx.Instance?.Bo(center + Vector3.up * 0.12f);
            Destroy(gameObject);
        }

        /// <summary>插完旗走回门里（或被召回）：已经在墙后看不到了，直接移除。</summary>
        public void Exit() => Destroy(gameObject);

        /// <summary>射手开了一枪：后坐动作 + 枪口一小团“噗”。</summary>
        public void Shoot()
        {
            _motion?.Shoot();
            var gun = transform.Find("EnemyGun") ?? FindDeep(transform, "EnemyGun");
            if (gun) { var tip = gun.position + gun.forward * 0.05f; PlayFx.Instance?.Flash(tip, new Color(1f, 0.9f, 0.7f), 0.12f, 0.2f); PlayFx.Instance?.Dust(tip, 0.25f, 2); }
        }
        static Transform FindDeep(Transform t, string n) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == n) return c; return null; }
        void OnDestroy() { if (_frost) Destroy(_frost.gameObject); if (_halo) Destroy(_halo.gameObject); }

        /// <summary>胖墩血掉一半：积木哗啦掉一地、扬一圈尘，个子变小（之后跳得更快由规则层的速度体现）。</summary>
        public void Shrink()
        {
            PlayFx.Instance?.Dust(transform.position, 1.8f, 10);
            _scaleBase *= 0.85f; transform.localScale = _scaleBase;
        }
    }
}
