using UnityEngine;

namespace Bang.Ambient
{
    // 吊挂物（云、星星、月亮、挂铃）：挂在吊点上的单摆。
    // 周期由吊线长度决定（T = 2π√(L/g)，线越长摆得越慢），室内只有很弱的气流：幅度很小、强弱缓慢起伏，两个方向略有相位差（轻微的椭圆摆）；
    // 再加一点绕吊线的扭转（扭摆周期比单摆长得多）。挂在本组件所在物体（吊点）上，被挂的东西是子物体。
    public class Pendulum : MonoBehaviour
    {
        [Tooltip("吊线长度（米），决定摆动周期")] public float length = 1.5f;
        [Tooltip("摆幅（度）")] public float amplitudeDeg = 2.5f;
        [Tooltip("绕吊线扭转的幅度（度）")] public float twistDeg = 8f;
        public float phase;
        Quaternion _rest;
        void OnEnable() { _rest = transform.localRotation; if (phase == 0) phase = Random.value * 10f; }
        void Update()
        {
            if (!Application.isPlaying) return;
            float t = Time.time;
            float w = Mathf.Sqrt(9.81f / Mathf.Max(0.05f, length));
            float env = 0.65f + 0.35f * Mathf.Sin(t * 0.11f + phase);              // 气流强弱慢慢变化
            float ax = Mathf.Sin(t * w + phase) * amplitudeDeg * env;
            float az = Mathf.Sin(t * w + phase + 1.4f) * amplitudeDeg * 0.4f * env;
            float tw = Mathf.Sin(t * 0.35f + phase * 0.7f) * twistDeg * env;        // 扭摆
            transform.localRotation = _rest * Quaternion.Euler(ax, tw, az);
        }
    }
}
