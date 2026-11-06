using UnityEngine;

namespace Bang.Ambient
{
    // 投石车投臂：静止 → 慢慢往后压（蓄力）→ 猛地扬起 → 回弹 → 静止。OnFire 在扬到最高点时触发（玩法里用来抛积木球）
    public class CatapultArm : MonoBehaviour
    {
        public Vector3 axis = Vector3.right;
        public float restDeg = 0f, windDeg = -35f, flingDeg = 70f;
        public float period = 9f, windTime = 1.6f, flingTime = 0.18f, settleTime = 0.9f;
        public float timeOffset;
        public System.Action OnFire;
        [Tooltip("由玩法驱动：不再自己循环，每次 Trigger() 才蓄力抛一次")] public bool external;
        [Tooltip("散架：投臂耷拉下来（玩法里打 6 枪后）")] public bool broken;
        Quaternion _r0; float _t; bool _fired; float _trig = -1f, _broken;
        void Start() { _r0 = transform.localRotation; _t = timeOffset; }

        /// <summary>玩法触发一次：蓄力 windTime → 甩出 flingTime（与规则层积木球出手同步）→ 回弹。</summary>
        public void Trigger() { _trig = 0; _fired = false; }

        void Update()
        {
            _broken = Mathf.MoveTowards(_broken, broken ? 1 : 0, Time.deltaTime * (broken ? 2.5f : 1.2f));
            if (external)
            {
                float a2 = restDeg;
                if (_trig >= 0)
                {
                    _trig += Time.deltaTime;
                    float tt = _trig;
                    if (tt < windTime) a2 = Mathf.Lerp(restDeg, windDeg, Mathf.SmoothStep(0, 1, tt / windTime));
                    else if (tt < windTime + flingTime) a2 = Mathf.Lerp(windDeg, flingDeg, (tt - windTime) / flingTime);
                    else if (tt < windTime + flingTime + settleTime)
                    {
                        if (!_fired) { _fired = true; OnFire?.Invoke(); }
                        float k = (tt - windTime - flingTime) / settleTime;
                        a2 = Mathf.Lerp(flingDeg, restDeg, 1 - Mathf.Pow(1 - k, 3)) + Mathf.Sin(k * 18f) * (1 - k) * 6f;
                    }
                    else _trig = -1f;
                }
                // 散架：投臂失去支撑耷拉下来（重力式加速），修好时带一点回弹抬起
                a2 = Mathf.Lerp(a2, windDeg * 1.9f, _broken * _broken);
                transform.localRotation = _r0 * Quaternion.AngleAxis(a2, axis);
                return;
            }
            _t += Time.deltaTime;
            float t = _t % period, a = restDeg;
            float t0 = period - windTime - flingTime - settleTime;
            if (t < t0) { a = restDeg; _fired = false; }
            else if (t < t0 + windTime) a = Mathf.Lerp(restDeg, windDeg, Mathf.SmoothStep(0, 1, (t - t0) / windTime));
            else if (t < t0 + windTime + flingTime) a = Mathf.Lerp(windDeg, flingDeg, (t - t0 - windTime) / flingTime);
            else
            {
                if (!_fired) { _fired = true; OnFire?.Invoke(); }
                float k = (t - t0 - windTime - flingTime) / settleTime;
                a = Mathf.Lerp(flingDeg, restDeg, 1 - Mathf.Pow(1 - k, 3)) + Mathf.Sin(k * 18f) * (1 - k) * 6f;
            }
            transform.localRotation = _r0 * Quaternion.AngleAxis(a, axis);
        }
    }
}
