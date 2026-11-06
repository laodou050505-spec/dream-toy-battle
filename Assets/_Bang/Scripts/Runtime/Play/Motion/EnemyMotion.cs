// 小怪零件动作（程序驱动，符合运动规律）：EnemyView 每帧填 EnemyAnimInput，这里驱动 EnemyRig 的各个零件。
//   跳（插旗兵）：跳跃节奏跟实际移动距离走（脚不打滑）；起跳前下蹲预备、腾空拉长、落地压扁并扬尘；手臂与脚交替摆。
//   跺（胖墩）：更慢更重，落地大幅压扁、扬一圈尘；插旗时两手轮流拍肚子。
//   碎步（发条老鼠）：高频小颠；发条钥匙只在走动时倒转（发条在松），停下时全身抖一抖；转弯身体内倾；尾巴逐节滞后（次级运动）。
//   插旗：举旗过头（预备）→ 用力往下扎、身体顿一下压扁 → 旗插稳后晃两下。
//   受击：朝受力方向后仰、压扁，弹性回正。冰冻：所有动作停住。
using System;
using UnityEngine;

namespace Bang.Play.Motion
{
    public struct EnemyAnimInput
    {
        public bool moving, planting, paused, frozen, cheering;
        public bool working;           // 修理机器人在修、指挥官刚发令
        public int role;               // 0 插旗 / 捣乱 1 攻击 2 辅助
        public bool aiming;            // 射手：开枪前的预警（举枪瞄准）
        public bool popOpen;           // 弹簧小丑盒：盒子打开
        public float plantT;           // 0..1 插旗进度
        public Vector3 velocity;       // 世界速度（米/秒）
        public int bossStage;          // Boss：0 巡游 1 蓄力 2 出招 3 踉跄 4 插旗
        public string bossMove;        // Boss 当前招式
        public float bossK;            // Boss 蓄力进度 0..1
    }

    [RequireComponent(typeof(EnemyRig))]
    public class EnemyMotion : MonoBehaviour
    {
        public event Action<Vector3> Landed;            // 落地（扬尘、音效）
        public event Action PlantStab;                  // 插旗下扎的一瞬间

        float _aim, _kick;
        /// <summary>开了一枪：持枪手臂往上一弹（后坐）、身子一缩、往后一仰（用户：敌人攻击要有动作）。</summary>
        public void Shoot() { _kick = 1f; _squash = Mathf.Max(_squash, calm ? 0.035f : 0.12f); }
        /// <summary>Boss：动作要稳（用户：Boss 抖动很明显，不要那么鬼畜）。挨打、开炮都只做小幅、带过渡的反应，不在一帧里猛跳；
        /// Boss 每秒挨好几枪，小怪那种“每枪后仰 22° + 压扁 22%”叠在一起就是一直抽搐。</summary>
        public bool calm;
        float _tiltS, _squashS, _kickS;

        EnemyRig _r;
        float _phase, _lookT, _shakeT, _keyAngle, _hitT, _lastYaw, _lean, _cheerT, _cheerSpin;
        Vector3 _hitAxis;
        float _squash;                                  // >0 压扁，<0 拉长
        bool _wasAir, _stabbed;
        Quaternion[] _tailRest;
        float[] _tailYaw;

        void Awake()
        {
            _r = GetComponent<EnemyRig>();
            if (_r.tail != null)
            {
                _tailRest = new Quaternion[_r.tail.Length]; _tailYaw = new float[_r.tail.Length];
                for (int i = 0; i < _r.tail.Length; i++) _tailRest[i] = _r.tail[i].localRotation;
            }
            _lastYaw = transform.eulerAngles.y;
        }

        /// <summary>被打中：dirWorld 是子弹飞行方向（小怪朝这个方向后仰）。</summary>
        public void Hit(Vector3 dirWorld, float strength = 1f)
        {
            var local = transform.InverseTransformDirection(dirWorld); local.y = 0;
            _hitAxis = Vector3.Cross(Vector3.up, local.sqrMagnitude > 1e-4f ? local.normalized : Vector3.back);
            if (calm) { _hitT = Mathf.Max(_hitT, 0.6f * Mathf.Clamp01(strength)); _squash = Mathf.Max(_squash, 0.04f * strength); return; }   // 连续挨打不重新起跳，只维持一个小幅后仰
            _hitT = Mathf.Clamp01(strength);
            _squash = Mathf.Max(_squash, 0.22f * strength);
        }

        public void Tick(in EnemyAnimInput a, float dt)
        {
            if (a.frozen) return;                                               // 冰冻：动作停住
            float speed = new Vector2(a.velocity.x, a.velocity.z).magnitude;
            switch (_r.style)
            {
                case MoveStyle.Scurry: Scurry(a, speed, dt); break;
                case MoveStyle.Slide: Slide(a, speed, dt); break;
                case MoveStyle.Fly: Fly(a, dt); break;
                case MoveStyle.Float: Float(dt); break;
                case MoveStyle.Drive: Drive(speed, dt); break;
                case MoveStyle.Pop: Pop(a, dt); break;
                default: Hop(a, speed, dt, _r.style == MoveStyle.Stomp); break;
            }
            Plant(a, dt);
            Cheer(a, dt);
            Idle(a, speed, dt);
            // 受击后仰：阻尼弹簧式回正
            if (_hitT > 0) _hitT = Mathf.Max(0, _hitT - dt * 3.2f);
            float tilt = Mathf.Sin(_hitT * Mathf.PI) * 22f;
            // 压扁 / 拉长：体积守恒（y 缩 xz 胀）
            _squash = Mathf.MoveTowards(_squash, 0, dt * 2.6f);
            float sq = _squash;
            if (calm)
            {   // Boss：后仰最多 5°、压扁最多 4%，都用指数平滑跟随（起、落都有过渡，没有单帧跳变）
                float kf = 1f - Mathf.Exp(-dt * 9f);
                _tiltS = Mathf.Lerp(_tiltS, _hitT * 5f, kf); tilt = _tiltS;
                _squashS = Mathf.Lerp(_squashS, sq, kf); sq = _squashS;
            }
            float sy = 1f - sq, sxz = 1f / Mathf.Sqrt(Mathf.Max(0.3f, sy));
            if (_r.body)
            {
                _r.body.localScale = new Vector3(sxz, sy, sxz);
                _r.body.localRotation = Quaternion.Euler(_pitch, _cheerSpin, 0) * Quaternion.AngleAxis(tilt, _hitAxis == Vector3.zero ? Vector3.right : _hitAxis) * Quaternion.Euler(0, 0, _lean + _rock);
            }
            BossPose(a, dt);
            Aim(a, dt);
        }

        // ───────── 职能待机（用户：敌人不要站在那里不动，很假）─────────
        // 停下 0.6 秒后按职能做小动作；修理 / 发令时右臂一下下敲或挥。幅度小、频率 ≤ 2Hz。飞的、开车的、弹簧盒不做（它们没有手脚）
        float _idleT;
        void Idle(in EnemyAnimInput a, float speed, float dt)
        {
            var r = _r;
            if (r.style == MoveStyle.Fly || r.style == MoveStyle.Drive || r.style == MoveStyle.Pop || r.style == MoveStyle.Float) return;
            if (a.working)
            {   // 修理机器人敲扳手 / 指挥官挥手：每 0.6 秒一下，落下时身子轻轻一顿
                float k = Mathf.Repeat(Time.time / 0.6f, 1f), hit = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f;
                if (r.armR) r.armR.localRotation = Quaternion.Euler(-30f - 70f * hit, 0, -ArmOut);
                if (k < 0.05f) _squash = Mathf.Max(_squash, 0.05f);
                _idleT = 0; return;
            }
            bool idle = !a.planting && !a.cheering && !a.aiming && !a.paused && speed < 0.03f;
            _idleT = idle ? _idleT + dt : 0f;
            if (_idleT < 0.6f) return;
            float w = Mathf.Clamp01(_idleT - 0.6f);
            switch (a.role)
            {
                case 0:   // 插旗 / 捣乱：原地踮脚、手臂小幅前后摆（急着要去插旗）
                {
                    float s = Mathf.Sin(_idleT * 5f);
                    if (r.body) r.body.localPosition += Vector3.up * Mathf.Abs(s) * 0.006f * w;
                    if (r.armL) r.armL.localRotation = Quaternion.Euler(s * 12f * w, 0, ArmOut);
                    if (r.armR) r.armR.localRotation = Quaternion.Euler(-s * 12f * w, 0, -ArmOut);
                    break;
                }
                case 2:   // 辅助：慢慢左右张望，右臂平举缓缓扫（在找要修的 / 在指挥）
                {
                    float s = Mathf.Sin(_idleT * 0.9f);
                    if (r.head) r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(0, s * 35f * w, 0), dt * 4f);
                    if (r.armR) r.armR.localRotation = Quaternion.Euler(-60f * w, s * 25f, -ArmOut);
                    break;
                }
            }
        }

        // ───────── Boss 招式姿势（用户：Boss 要干什么要明确，不能傻站着）─────────
        // Boss 预制体只有 body（没有手臂 / 头），所以全靠 body 的俯仰、缩放、上下：蓄力时慢慢做出“要出招”的姿势，出招那一下放出去，
        // 演完缓缓回位。所有量都用 MoveTowards 平滑，频率 ≤ 2Hz，不闪不抖（舒适）。
        float _bp, _bs, _by, _bl, _bTick, _bIdle;
        void BossPose(in EnemyAnimInput a, float dt)
        {
            var b = _r.body; if (!b) return;
            float tp = 0, ts = 0, ty = 0, tl = 0;
            float k = Mathf.SmoothStep(0, 1, a.bossK); bool wind = a.bossStage == 1, act = a.bossStage == 2;
            // 巡游时的待机小动作（用户：Boss 需要简单的动作，不要站着不动）：慢呼吸（缩放）、左右晃身、偶尔点头 / 张望，
            // 每个 Boss 按走法不同：跺脚型重心左右换、开车型车身前后轻晃、飘浮型慢慢转身、滑行型左右扭身。频率 ≤ 1 Hz、幅度小，不抖
            if (a.bossStage == 0 && calm)
            {
                _bIdle += dt; float t = _bIdle;
                float breathe = Mathf.Sin(t * 1.6f) * 0.018f;                                    // 呼吸：约 0.25 Hz
                float look = Mathf.Sin(t * 0.55f) * 12f + Mathf.Sin(t * 1.3f) * 3f;              // 慢慢左右张望
                float nod = Mathf.Max(0, Mathf.Sin(t * 0.9f)) * 4f;                               // 点头
                switch (_r.style)
                {
                    case MoveStyle.Stomp: ts = breathe; ty = look * 0.6f; tp = nod; _rock = Mathf.Sin(t * 1.4f) * 4f; break;   // 积木巨人：重心左右换
                    case MoveStyle.Drive: ts = breathe * 0.6f; ty = look; tp = Mathf.Sin(t * 1.8f) * 2.5f; break;              // 坦克 / 玩具箱：车身前后轻晃、炮塔张望
                    case MoveStyle.Float: ts = breathe * 1.5f; ty = Mathf.Sin(t * 0.4f) * 18f; break;                          // 气球之王：鼓肚子、慢慢转身
                    default: ts = breathe; ty = look; tp = nod; break;                                                            // 大将军 / 发条巨偶：扭身、点头
                }
            }
            else _bIdle = 0;
            switch (a.bossStage == 3 ? "stagger" : a.bossStage == 4 ? "flag" : a.bossMove)
            {
                case "brick": case "foam": tp = wind ? -12f * k : act ? 8f : 0; break;
                case "multiFlag": tp = wind ? 8f * k : act ? 10f : 0; tl = wind ? 0.03f * k : 0; break;              // 俯身抱旗 → 往前一撒
                case "grab": case "drain": tp = wind ? -9f * k : act ? 6f : 0; ts = wind ? 0.05f * k : 0; break;     // 蓄力鼓起 → 往前一甩                     // 往后仰蓄力 → 往前一甩
                case "brickStomp": tl = wind ? 0.07f * k : 0; tp = wind ? -6f * k : 0;                      // 身子慢慢抬高 → 砸下（落地压扁 + 扬尘）
                    if (act && _bl > 0.035f) { _squash = Mathf.Max(_squash, 0.22f); Landed?.Invoke(transform.position); _bl = 0.02f; }
                    break;
                case "shells": tp = wind ? -7f * k : act ? -4f : 0; break;                                  // 坦克：车头抬起对准
                case "summon": ty = Mathf.Sin(Time.time * 3f) * 6f * (wind ? k : act ? 1f : 0); ts = wind ? 0.04f * k : 0; break;   // 大将军：左右慢慢扭身点兵
                case "confetti": ts = wind ? 0.12f * k : act ? -0.04f : 0; break;                           // 气球之王：鼓起肚子 → 泄气
                case "gears": ty = wind ? 30f * k : 0; ts = wind ? 0.03f * k : 0;                           // 发条巨偶：拧发条一样扭身 → 每 0.6 秒一下顿
                    if (act) { _bTick += dt; if (_bTick >= 0.6f) { _bTick = 0; _squash = Mathf.Max(_squash, 0.08f); } } else _bTick = 0;
                    break;
                case "stagger": tp = 10f; ts = -0.05f; break;                                                // 踉跄：往后一仰、缩一下
                case "flag": tp = 6f; break;                                                                 // 插旗：往前一俯
            }
            _bp = Mathf.Lerp(_bp, tp, 1f - Mathf.Exp(-dt * 6f)); _bs = Mathf.MoveTowards(_bs, ts, dt * 0.5f); _by = Mathf.MoveTowards(_by, ty, dt * 80f); _bl = Mathf.MoveTowards(_bl, tl, dt * (tl > _bl ? 0.1f : 0.5f));
            if (Mathf.Abs(_bp) + Mathf.Abs(_bs) + Mathf.Abs(_by) + _bl < 1e-4f) return;
            b.localRotation *= Quaternion.Euler(_bp, _by, 0);
            b.localScale *= 1f + _bs;
            b.localPosition += Vector3.up * _bl;
        }

        float _rock, _pitch, _clock, _spin, _popAmt, _popVel;

        /// <summary>铁皮兵：底部有轮子平移，走起来左右摇摆，背后发条跟着转。</summary>
        void Slide(in EnemyAnimInput a, float speed, float dt)
        {
            var r = _r; bool move = a.moving && !a.paused && !a.planting && speed > 0.01f;
            if (move) { _phase += speed * dt / 0.09f; _keyAngle += speed * dt * 500f; }
            float w = move ? Mathf.Sin(_phase * Mathf.PI) : 0f;
            _rock = Mathf.MoveTowards(_rock, w * 5f, dt * 60f);
            if (r.armL) r.armL.localRotation = Quaternion.Euler(w * 16f, 0, ArmOut);
            if (r.armR) r.armR.localRotation = Quaternion.Euler(-w * 16f, 0, -ArmOut);
            if (r.key) r.key.localRotation = r.keyBase * Quaternion.Euler(0, _keyAngle, 0);
            TurnLean(dt, 4f);
            float tilt = Mathf.Abs(_rock + _lean) * Mathf.Deg2Rad;                  // 底座平移、腿不摆；身体倾斜时抬高一点，脚始终贴地不陷进地面
            if (r.body) r.body.localPosition = new Vector3(0, tilt * r.hopLength, 0);
        }

        /// <summary>纸飞机：转弯时大幅压坡度，升降时机头跟着抬，翅膀轻轻扇。</summary>
        void Fly(in EnemyAnimInput a, float dt)
        {
            var r = _r; _clock += dt;
            float flap = 14f + Mathf.Sin(_clock * 7f) * 9f;
            if (r.armL) r.armL.localRotation = Quaternion.Euler(0, 0, flap);
            if (r.armR) r.armR.localRotation = Quaternion.Euler(0, 0, -flap);
            _pitch = Mathf.Lerp(_pitch, Mathf.Clamp(-a.velocity.y * 26f, -32f, 32f), 1f - Mathf.Exp(-dt * 6f));
            if (r.body) r.body.localPosition = new Vector3(0, Mathf.Sin(_clock * 2.6f) * 0.012f, 0);
            TurnLean(dt, 22f);
        }

        /// <summary>气球兵：上下漂，身子慢慢晃。</summary>
        void Float(float dt)
        {
            var r = _r; _clock += dt;
            _rock = Mathf.Sin(_clock * 1.3f) * 5f;
            if (r.body) r.body.localPosition = new Vector3(0, Mathf.Sin(_clock * 2.1f) * 0.018f, 0);
        }

        /// <summary>遥控车：轮子按实际速度转，车身随路面轻颠，转弯内倾。</summary>
        void Drive(float speed, float dt)
        {
            var r = _r; _clock += dt;
            _spin += speed * dt / 0.044f * Mathf.Rad2Deg;
            if (r.feet != null) foreach (var w in r.feet) if (w) w.localRotation = Quaternion.Euler(_spin, 0, 0);
            TurnLean(dt, 10f);
            float bump = speed > 0.05f ? Mathf.Abs(Mathf.Sin(_clock * 19f)) * 0.004f : 0f;
            if (r.body) r.body.localPosition = new Vector3(0, bump + Mathf.Abs(_lean) * Mathf.Deg2Rad * r.hopLength, 0);   // 转弯内倾时抬高一点，外侧轮子不陷进地面
        }

        /// <summary>弹簧小丑盒：合着时摇柄慢慢转、盒子蓄力抖动；打开时盖子翻开，小丑顺着弹簧弹出来，带过冲回弹。</summary>
        void Pop(in EnemyAnimInput a, float dt)
        {
            var r = _r; _clock += dt;
            float target = a.popOpen ? 1f : 0f;
            _popVel += ((target - _popAmt) * 180f - _popVel * 11f) * dt;
            _popAmt = Mathf.Max(0f, _popAmt + _popVel * dt);
            if (r.pop) r.pop.localPosition = r.popRest + Vector3.up * (0.3f * _popAmt);   // 小丑整个弹出盒口
            if (r.spring) r.spring.localScale = new Vector3(1f, Mathf.Max(0.12f, 0.12f + 1.38f * _popAmt), 1f);
            if (r.pack) r.pack.localRotation = Quaternion.Euler(-118f * Mathf.Clamp01(_popAmt * 2.4f), 0, 0);
            if (!a.popOpen) { _spin += dt * 200f; _rock = Mathf.Sin(_clock * 24f) * 0.8f; } else _rock = 0f;
            if (r.key) r.key.localRotation = Quaternion.Euler(_spin, 0, 0);
            if (r.body) r.body.localPosition = Vector3.zero;
        }

        void Hop(in EnemyAnimInput a, float speed, float dt, bool heavy)
        {
            var r = _r;
            bool move = a.moving && !a.paused && !a.planting;
            float y = 0;
            if (move && speed > 0.01f)
            {
                _phase += speed * dt / Mathf.Max(0.02f, r.hopLength);           // 一跳 = hopLength 米
                float p = _phase % 1f;
                // 0~0.12 下蹲预备（压扁）→ 0.12~0.92 腾空（拉长后回正）→ 落地压扁
                if (p < 0.12f) { _squash = Mathf.Max(_squash, (heavy ? 0.16f : 0.1f) * (p / 0.12f)); y = 0; }
                else
                {
                    float q = (p - 0.12f) / 0.8f;
                    y = Mathf.Sin(Mathf.Clamp01(q) * Mathf.PI) * r.hopHeight * (heavy ? 0.7f : 1f);
                    if (q < 0.5f) _squash = Mathf.Min(_squash, -0.06f * Mathf.Sin(q * Mathf.PI * 2));   // 上升时略拉长
                }
                bool air = p >= 0.12f && p < 0.92f;
                if (_wasAir && !air) { _squash = heavy ? 0.24f : 0.14f; Landed?.Invoke(transform.position); }
                _wasAir = air;
                // 手臂、脚交替摆（随步伐）
                float swing = Mathf.Sin(_phase * Mathf.PI * 2f) * (heavy ? 18f : 28f);
                if (r.armL) r.armL.localRotation = Quaternion.Euler(swing, 0, ArmOut);
                if (r.armR) r.armR.localRotation = Quaternion.Euler(-swing, 0, -ArmOut);
                if (r.footL) r.footL.localRotation = Quaternion.Euler(-swing * r.footSwing, 0, 0);
                if (r.footR) r.footR.localRotation = Quaternion.Euler(swing * r.footSwing, 0, 0);
                _lookT = 0;
            }
            else
            {
                _wasAir = false;
                // 停下：手臂慢慢垂回，头左右张望（停得越久越明显）
                _lookT += dt;
                if (!a.planting)
                {
                    Ease(r.armL, Quaternion.Euler(0, 0, ArmOut), dt * 6); Ease(r.armR, Quaternion.Euler(0, 0, -ArmOut), dt * 6);
                    Ease(r.footL, Quaternion.identity, dt * 8); Ease(r.footR, Quaternion.identity, dt * 8);
                }
                if (r.head && !a.planting)
                {
                    float look = Mathf.Sin(_lookT * 1.3f) * 30f * Mathf.Clamp01(_lookT - 0.4f);
                    r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(0, look, 0), dt * 5);
                }
                if (a.paused) { _shakeT += dt; y = Mathf.Abs(Mathf.Sin(_shakeT * 40f)) * 0.004f; }
            }
            if (r.body) r.body.localPosition = new Vector3(0, y, 0);
            TurnLean(dt, 6f);
        }

        void Scurry(in EnemyAnimInput a, float speed, float dt)
        {
            var r = _r;
            bool move = a.moving && !a.paused && !a.planting && speed > 0.01f;
            float y = 0;
            if (move)
            {
                _phase += speed * dt / 0.05f;                                   // 小碎步：每 5 cm 一颠
                y = Mathf.Abs(Mathf.Sin(_phase * Mathf.PI)) * 0.006f;
                _keyAngle += speed * dt * 900f;                                 // 走动时发条在松：钥匙绕杆转，从身后看顺时针
                if (r.feet != null) for (int i = 0; i < r.feet.Length; i++) if (r.feet[i]) r.feet[i].localRotation = Quaternion.Euler(Mathf.Sin(_phase * Mathf.PI + i * Mathf.PI * 0.5f) * 35f, 0, 0);
                _shakeT = 0;
            }
            else if (a.paused)
            {
                _shakeT += dt;                                                   // 发条走完：全身抖一抖
                y = Mathf.Abs(Mathf.Sin(_shakeT * 46f)) * 0.004f * Mathf.Exp(-_shakeT * 3f);
            }
            if (r.key) r.key.localRotation = r.keyBase * Quaternion.Euler(0, _keyAngle, 0);
            if (r.body) r.body.localPosition = new Vector3(0, y, 0);
            TurnLean(dt, 14f);
            // 尾巴：每节朝前一节的朝向追，越往尖越滞后；走动时叠一点甩动
            if (r.tail != null && _tailRest != null)
            {
                float yawRate = _yawRate;
                for (int i = 0; i < r.tail.Length; i++)
                {
                    if (!r.tail[i]) continue;
                    float target = -yawRate * 0.12f * (i + 1) + (move ? Mathf.Sin(_phase * 0.9f - i * 0.7f) * 12f : Mathf.Sin(Time.time * 2f - i * 0.6f) * 4f);
                    _tailYaw[i] = Mathf.Lerp(_tailYaw[i], target, 1f - Mathf.Exp(-dt * (10f - i * 1.1f)));
                    r.tail[i].localRotation = _tailRest[i] * Quaternion.Euler(0, _tailYaw[i], 0);
                }
            }
        }

        float _yawRate;
        void TurnLean(float dt, float leanPerRad)
        {
            float yaw = transform.eulerAngles.y;
            float d = Mathf.DeltaAngle(_lastYaw, yaw); _lastYaw = yaw;
            _yawRate = Mathf.Lerp(_yawRate, d / Mathf.Max(dt, 1e-4f), 1f - Mathf.Exp(-dt * 8f));
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(-_yawRate * Mathf.Deg2Rad * leanPerRad, -16f, 16f), 1f - Mathf.Exp(-dt * 6f));   // 转弯身体内倾
        }

        void Plant(in EnemyAnimInput a, float dt)
        {
            var r = _r;
            if (!a.planting) { _stabbed = false; return; }
            float t = Mathf.Clamp01(a.plantT);
            bool brute = r.style == MoveStyle.Stomp;
            if (brute)
            {
                // 胖墩：两手轮流拍肚子
                float pat = Mathf.Sin(Time.time * 9f);
                if (r.armL) r.armL.localRotation = Quaternion.Euler(-40f - Mathf.Max(0, pat) * 30f, 0, 20f);
                if (r.armR) r.armR.localRotation = Quaternion.Euler(-40f - Mathf.Max(0, -pat) * 30f, 0, -20f);
                if (pat > 0.97f || pat < -0.97f) _squash = Mathf.Max(_squash, 0.04f);
            }
            else
            {
                // 插旗兵：0~0.55 双臂举过头（预备，缓入），0.55~0.68 猛地扎下（快），之后旗插稳、手扶着晃两下
                float raise = t < 0.55f ? Mathf.SmoothStep(0, 1, t / 0.55f) : t < 0.68f ? 1f - (t - 0.55f) / 0.13f : 0f;
                float hold = t >= 0.68f ? Mathf.Sin((t - 0.68f) * 40f) * 8f * (1f - t) : 0f;
                float ang = -165f * raise - 30f * (t >= 0.68f ? 1 : 0) + hold;
                if (r.armL) r.armL.localRotation = Quaternion.Euler(ang, 0, ArmOut);
                if (r.armR) r.armR.localRotation = Quaternion.Euler(ang, 0, -ArmOut);
                if (t >= 0.62f && !_stabbed) { _stabbed = true; _squash = 0.2f; PlantStab?.Invoke(); }
                if (r.head) r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(raise * -15f, 0, 0), dt * 8);
            }
        }

        /// <summary>举枪瞄准 → 开枪后坐：右手（持枪）平举指向玩家，左手扶一下，身子微微后仰蓄力；开枪时手臂上弹、身子后仰再回正。</summary>
        void Aim(in EnemyAnimInput a, float dt)
        {
            _aim = Mathf.MoveTowards(_aim, a.aiming && !a.frozen ? 1f : _r.holdAim, dt * (a.aiming ? 5f : 2.5f));
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 3.5f);
            if (_aim < 0.001f && _kick < 0.001f) return;
            var r = _r;
            float raise = Mathf.SmoothStep(0, 1, _aim);
            float kick = Mathf.Sin(_kick * Mathf.PI * 0.5f) * 38f;                                 // 后坐：手臂往上弹（快起、慢回）
            float tremble = a.aiming && !calm ? Mathf.Sin(Time.time * 31f) * 1.2f * raise : 0;   // 瞄准时手微微发抖（蓄力感；Boss 不抖）
            if (r.armR) r.armR.localRotation = Quaternion.Euler(Mathf.Lerp(0, -88f, raise) - kick + tremble, 0, -ArmOut);
            if (r.armL) r.armL.localRotation = Quaternion.Euler(Mathf.Lerp(0, -50f, raise) - kick * 0.4f, 0, ArmOut + 6f * raise);
            float bodyKick = -5f * raise - kick * 0.35f;
            if (calm) { _kickS = Mathf.Lerp(_kickS, -2f * raise - kick * 0.1f, 1f - Mathf.Exp(-dt * 10f)); bodyKick = _kickS; }   // Boss 开炮：机身只顿 ~4°，带过渡（原来一帧仰 13°）
            if (r.body) r.body.localRotation *= Quaternion.Euler(bodyKick, 0, 0);   // 负 = 往后仰
            if (r.head) r.head.localRotation = Quaternion.Slerp(r.head.localRotation, Quaternion.Euler(4f * raise, 0, 0), dt * 10f);
        }

        void Cheer(in EnemyAnimInput a, float dt)
        {
            if (!a.cheering) { _cheerT = 0; _cheerSpin = 0; return; }
            _cheerT += dt;                                                       // 插完旗原地转圈欢呼（转一圈约 0.8 秒）
            _cheerSpin = Mathf.SmoothStep(0, 360f, Mathf.Clamp01(_cheerT / 0.8f));
            float wave = Mathf.Sin(_cheerT * 14f) * 20f;
            if (_r.armL) _r.armL.localRotation = Quaternion.Euler(-150f + wave, 0, 10f);
            if (_r.armR) _r.armR.localRotation = Quaternion.Euler(-150f - wave, 0, -10f);
        }

        // 手臂往外张的角度：左臂绕本地 Z 正转、右臂负转才是往外（原来写反了，手臂插进身体 / 举手时插进头，用户：挥手穿模）。
        // 全部姿势用 cs/armclip.cs 量过：不穿模
        const float ArmOut = 8f;

        static void Ease(Transform t, Quaternion target, float k) { if (t) t.localRotation = Quaternion.Slerp(t.localRotation, target, Mathf.Clamp01(k)); }
    }
}
