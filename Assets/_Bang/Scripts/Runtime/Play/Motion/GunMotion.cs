// 半透明玩具沙鹰的零件动画（全是零件位移，不用骨骼）：
//   开火：枪身上挑 15° 再弹性回落；套筒 40 ms 后坐、80 ms 复位；击锤后仰；弹壳从抛壳口侧向弹出、翻滚下落后淡出。
//   换弹：枪横过来 30° → 空弹匣掉出（自由落体、弹一下后淡出）→ 新弹匣从下方推进握把 → 套筒“咔嚓”复位。
//          进度由规则层给（t / 总时长）；金区快速换弹时整套动作加速完成；卡壳时套筒卡在半开、枪身抖。
//   被打飞：枪向上翻飞再弹回手里（1.5 秒）。 空膛：扣一下只“咔”一声小抖。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Play.Motion
{
    public class GunMotion : MonoBehaviour
    {
        public Transform model;                       // 整把枪（挂在手的瞄准位姿下，做上挑 / 横过来 / 被打飞）
        public Transform slide, mag, hammer, trigger, muzzle, shellPoint;
        public GameObject shellPrefab, magPrefab;
        public float slideTravel = 0.028f;

        Vector3 _slideRest, _magRest; Quaternion _hammerRest, _trigRest;
        float _kick, _kickVel, _slideT = 1f, _knockT = -1f, _dryT = -1f, _jamShake;
        float _reloadT = -1f; bool _reloadJam, _magDropped, _newMagIn;
        readonly List<(Transform t, Vector3 v, Vector3 w, float life)> _debris = new();

        void Awake()
        {
            if (slide) _slideRest = slide.localPosition;
            if (mag) _magRest = mag.localPosition;
            if (hammer) _hammerRest = hammer.localRotation;
            if (trigger) _trigRest = trigger.localRotation;
        }

        public void Fire()
        {
            _kickVel += 260f;                         // 上挑角速度（度/秒）
            _slideT = 0f;
            if (shellPrefab && shellPoint)
            {
                var s = Instantiate(shellPrefab, shellPoint.position, shellPoint.rotation).transform;
                var right = transform.right; var up = transform.up;
                _debris.Add((s, right * Random.Range(0.9f, 1.3f) + up * Random.Range(0.8f, 1.2f), Random.onUnitSphere * 900f, 0.9f));
            }
        }

        public void Dry() => _dryT = 0f;
        public void Knocked() => _knockT = 0f;
        float _stealT = -1f, _stealDur;
        /// <summary>被 Boss 抢走：枪被扯向前方飞走、不见，dur 秒后从前方飞回手里。</summary>
        public void Stolen(float dur) { _stealT = 0f; _stealDur = Mathf.Max(1f, dur); _knockT = -1f; }

        /// <summary>换弹进度：t01 = 已用时间 / 总时长（金区成功时规则层会把总时长缩短，这里自然就加速了）。</summary>
        public void Reload(float t01, bool jam)
        {
            if (_reloadT < 0) { _magDropped = false; _newMagIn = false; }
            _reloadT = Mathf.Clamp01(t01); _reloadJam = jam;
        }

        public void ReloadDone()
        {
            _reloadT = -1f; _slideT = 0.5f;           // 套筒“咔嚓”复位（从半程回到原位）
            if (mag) { mag.localPosition = _magRest; mag.gameObject.SetActive(true); }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // 上挑：阻尼弹簧（快速抬起、带一点过冲回落）
            _kickVel += (-_kick * 180f - _kickVel * 18f) * dt;
            _kick += _kickVel * dt;
            // 套筒：0~0.25 后坐（快），0.25~1 复位（缓出）
            _slideT = Mathf.Min(1f, _slideT + dt / 0.12f);
            float back = _slideT < 0.33f ? _slideT / 0.33f : 1f - Mathf.SmoothStep(0, 1, (_slideT - 0.33f) / 0.67f);
            if (_reloadT >= 0 && _reloadJam) back = 0.5f + Mathf.Sin(Time.time * 40f) * 0.04f;     // 卡壳：套筒卡在半开
            if (slide) slide.localPosition = _slideRest + Vector3.back * slideTravel * back;
            if (hammer) hammer.localRotation = _hammerRest * Quaternion.Euler(-35f * back, 0, 0);
            if (trigger) trigger.localRotation = _trigRest * Quaternion.Euler(18f * Mathf.Clamp01(1f - _slideT * 2f), 0, 0);

            // 换弹姿态：前 15% 横过来 30°，最后 10% 回正
            float roll = 0f;
            if (_reloadT >= 0)
            {
                roll = _reloadT < 0.15f ? Mathf.SmoothStep(0, 30f, _reloadT / 0.15f) : _reloadT > 0.9f ? Mathf.SmoothStep(30f, 0, (_reloadT - 0.9f) / 0.1f) : 30f;
                if (mag)
                {
                    if (_reloadT > 0.18f && !_magDropped)
                    {
                        _magDropped = true;
                        if (magPrefab)                       // 空弹匣掉出去（复制一个掉落，原位的藏起来）
                        {
                            var d = Instantiate(magPrefab, mag.position, mag.rotation).transform;
                            _debris.Add((d, -transform.up * 0.6f + transform.right * 0.2f, new Vector3(0, 0, 200f), 1.2f));
                        }
                        mag.gameObject.SetActive(false);
                    }
                    if (_reloadT > 0.45f)
                    {
                        mag.gameObject.SetActive(true);      // 新弹匣从下面推进去
                        float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((_reloadT - 0.45f) / 0.35f));
                        mag.localPosition = _magRest + Vector3.down * 0.09f * (1f - k);
                    }
                }
                if (_reloadJam) _jamShake = 1f;
            }
            _jamShake = Mathf.MoveTowards(_jamShake, 0, dt * 2f);
            float shake = _jamShake * Mathf.Sin(Time.time * 55f) * 2.5f;
            if (_dryT >= 0) { _dryT += dt; shake += Mathf.Sin(_dryT * 60f) * 3f * Mathf.Max(0, 1f - _dryT * 6f); if (_dryT > 0.2f) _dryT = -1f; }

            // 被打飞：向上翻飞 0.6 秒，再弹回手里
            Vector3 knockPos = Vector3.zero; float knockSpin = 0f;
            if (_knockT >= 0)
            {
                _knockT += dt; float k = _knockT / 1.5f;
                float up = k < 0.45f ? Mathf.Sin(k / 0.45f * Mathf.PI * 0.5f) : 1f - Mathf.SmoothStep(0, 1, (k - 0.45f) / 0.55f);
                knockPos = new Vector3(0.05f, 0.25f, -0.05f) * up; knockSpin = 720f * Mathf.SmoothStep(0, 1, Mathf.Min(1f, k / 0.8f));
                if (k >= 1f) _knockT = -1f;
            }
            if (_stealT >= 0)
            {
                _stealT += dt; float t = _stealT, out1 = 0.45f, ret = 0.5f;
                float k = t < out1 ? Mathf.SmoothStep(0, 1, t / out1) : t < _stealDur - ret ? 1f : 1f - Mathf.SmoothStep(0, 1, (t - (_stealDur - ret)) / ret);
                knockPos = new Vector3(0f, 0.1f, 1.4f) * k; knockSpin = 360f * k;   // 往前被扯走（朝 Boss 方向），远处不见
                if (model) model.gameObject.SetActive(k < 0.97f);
                if (t >= _stealDur) { _stealT = -1f; if (model) model.gameObject.SetActive(true); }
            }
            if (model)
            {
                model.localPosition = knockPos;
                model.localRotation = Quaternion.Euler(-_kick - knockSpin + shake, 0, roll);
            }

            // 掉出去的弹壳 / 空弹匣：重力、落地弹一下、淡出
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                var (t, v, w, life) = _debris[i];
                if (!t) { _debris.RemoveAt(i); continue; }
                v += Physics.gravity * dt;
                var p = t.position + v * dt;
                if (PlayFx.RoomHit(t.position, p, out var hit) && Vector3.Dot(v, hit.normal) < 0)        // 先落到桌面上（不穿过桌子），在桌边才接着掉到地上
                { var n = hit.normal; p = hit.point + n * 0.01f; float vn = Vector3.Dot(v, n); v = (v - n * vn) * 0.5f - n * vn * 0.35f; w *= 0.5f; }
                else if (p.y < 0.01f && v.y < 0) { p.y = 0.01f; v = new Vector3(v.x * 0.5f, -v.y * 0.35f, v.z * 0.5f); w *= 0.5f; }
                t.position = p; t.rotation = Quaternion.Euler(w * dt) * t.rotation;
                life -= dt;
                if (life < 0.25f) t.localScale = Vector3.one * Mathf.Max(0, life / 0.25f);
                if (life <= 0) { Destroy(t.gameObject); _debris.RemoveAt(i); continue; }
                _debris[i] = (t, v, w, life);
            }
        }
    }
}
