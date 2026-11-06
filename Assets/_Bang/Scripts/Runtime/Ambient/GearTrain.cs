using UnityEngine;

namespace Bang.Ambient
{
    // 啮合齿轮组：所有齿轮同模数，啮合的一对反向转、角速度与齿数成反比（ω子 = -ω父 · N父 / N子）。
    // 初始角度按“父齿轮的齿正对子齿轮的齿槽”求出，转起来齿始终咬合。
    // 主动轮由发条驱动，按擒纵机构的节拍走：每拍快速转一小格、略过冲后停住（钟表的“嘀嗒”），而不是匀速空转。
    [ExecuteAlways]
    public class GearTrain : MonoBehaviour
    {
        [System.Serializable] public struct Gear { public Transform pivot; public int teeth; public int parent; }
        public Gear[] gears;
        [Tooltip("主动轮平均角速度（度/秒，正 = 逆时针，从房间看）")] public float driverDegPerSec = 14f;
        [Tooltip("擒纵节拍：每秒几拍；0 = 匀速")] public float ticksPerSec = 2f;
        [Tooltip("可选：跟主动轮同轴转的东西（发条钥匙）")] public Transform driverExtra;
        public float speedMul = 1f;
        float[] _ratio, _phase0; float _angle, _clock;
        Quaternion _extraRest;

        void OnEnable() { Solve(); if (driverExtra) _extraRest = driverExtra.localRotation; Pose(); }

        public void Solve()
        {
            if (gears == null || gears.Length == 0) return;
            int n = gears.Length;
            _ratio = new float[n]; _phase0 = new float[n];
            for (int i = 0; i < n; i++)
            {
                var g = gears[i];
                if (g.parent < 0 || g.parent >= i || !g.pivot) { _ratio[i] = 1; _phase0[i] = 0; continue; }
                var p = gears[g.parent];
                _ratio[i] = -_ratio[g.parent] * p.teeth / Mathf.Max(1, g.teeth);
                Vector2 d = g.pivot.localPosition - p.pivot.localPosition;
                float alpha = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float fp = Mathf.Repeat((alpha - _phase0[g.parent]) * p.teeth / 360f, 1f);      // 父齿轮在接触点的齿相位（0 = 齿正中）
                _phase0[i] = alpha + 180f - 360f / g.teeth * (0.5f - fp);
            }
        }

        void Update()
        {
            if (!Application.isPlaying || _ratio == null) return;
            float dt = Time.deltaTime * speedMul;
            if (ticksPerSec <= 0) _angle += driverDegPerSec * dt;
            else
            {
                // 每拍转 step 度：前 25% 时间快速转过去（缓出 + 3% 过冲），其余时间停住
                _clock += dt * ticksPerSec;
                float step = driverDegPerSec / ticksPerSec;
                float k = Mathf.Repeat(_clock, 1f), whole = Mathf.Floor(_clock);
                float e = k < 0.25f ? EaseBack(k / 0.25f) : 1f;
                _angle = (whole + e) * step;
            }
            Pose();
        }

        static float EaseBack(float x) { const float s = 1.2f; x -= 1; return 1 + x * x * ((s + 1) * x + s); }

        void Pose()
        {
            if (_ratio == null) return;
            for (int i = 0; i < gears.Length; i++)
                if (gears[i].pivot) gears[i].pivot.localRotation = Quaternion.Euler(0, 0, _phase0[i] + _angle * _ratio[i]);
            if (driverExtra) driverExtra.localRotation = _extraRest * Quaternion.Euler(0, 0, _angle);
        }
    }
}
