using UnityEngine;

namespace Bang.Ambient
{
    // 沿椭圆轨道行驶的小火车：车头 + 若干车厢，车厢按弧长落后于车头；车身随弯道转向，过接缝时轻微颠一下
    [ExecuteAlways]
    public class TrainLoop : MonoBehaviour
    {
        public Vector2 center = new Vector2(0.25f, -3.1f);
        public float a = 1.65f, b = 1.0f, y = 0.042f;
        public float speed = 0.35f;              // 米/秒
        public Transform[] cars;                 // [0] 车头
        public float spacing = 0.34f;
        public float yawOffset;                  // 模型正面方向修正（度）
        [Tooltip("起始位置（0..1 圈），0.25 = 轨道正前方")] public float startPhase = 0.3f;
        [Tooltip("可选：沿路线（0..1 圈）的地面高度，构建时烘焙；为空时用固定高度 y")] public AnimationCurve heightCurve;
        float _s = -1;

        Vector3 P(float u)
        {
            float h = y;
            if (heightCurve != null && heightCurve.length > 1) h += heightCurve.Evaluate(Mathf.Repeat(u / (Mathf.PI * 2f), 1f));
            return new Vector3(center.x + Mathf.Cos(u) * a, h, center.y + Mathf.Sin(u) * b);
        }

        void OnEnable() => Step(0);
        void Update() => Step(Application.isPlaying ? Time.deltaTime : 0);

        public void Step(float dt)
        {
            if (cars == null || cars.Length == 0) return;
            float perim = Mathf.PI * (3 * (a + b) - Mathf.Sqrt((3 * a + b) * (a + 3 * b)));
            if (_s < 0) _s = startPhase * perim;
            _s += speed * dt;
            for (int i = 0; i < cars.Length; i++)
            {
                if (!cars[i]) continue;
                float u = (_s - i * spacing) / perim * Mathf.PI * 2f;
                var p = P(u); var q = P(u + 0.04f);
                var fwd = (q - p).normalized;
                float bump = Mathf.Abs(Mathf.Sin((_s - i * spacing) / 0.32f * Mathf.PI)) * 0.004f;
                cars[i].localPosition = p + Vector3.up * bump;
                cars[i].localRotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0, yawOffset, 0);
            }
        }
    }
}
