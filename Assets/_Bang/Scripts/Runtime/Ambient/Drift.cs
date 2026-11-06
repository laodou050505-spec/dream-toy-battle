using UnityEngine;

namespace Bang.Ambient
{
    // 云：顺着风向慢慢来回漂（周期几十秒、幅度几十厘米），高度几乎不变，只有极轻的起伏；
    // 不同云的周期、相位不同，不会整齐划一地动。
    public class Drift : MonoBehaviour
    {
        public Vector3 wind = new Vector3(1, 0, 0.3f);
        public float amplitude = 0.35f, period = 38f, phase;
        Vector3 _p0;
        void OnEnable() { _p0 = transform.localPosition; if (phase == 0) phase = Random.value * 10f; }
        void Update()
        {
            if (!Application.isPlaying) return;
            float t = (Time.time + phase * period) * Mathf.PI * 2f / period;
            transform.localPosition = _p0 + wind.normalized * Mathf.Sin(t) * amplitude + Vector3.up * Mathf.Sin(t * 1.7f + phase) * amplitude * 0.12f;
        }
    }
}
