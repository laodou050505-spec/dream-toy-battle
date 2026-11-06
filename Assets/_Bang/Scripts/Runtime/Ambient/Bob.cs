using UnityEngine;

namespace Bang.Ambient
{
    public class Bob : MonoBehaviour
    {
        public float amplitude = 0.06f, period = 4f, swayDeg = 2f;
        public float phase;
        Vector3 _p0; Quaternion _r0;
        void Start() { _p0 = transform.localPosition; _r0 = transform.localRotation; if (phase == 0) phase = Random.value * 10f; }
        void Update()
        {
            float t = (Time.time + phase) * Mathf.PI * 2f / period;
            transform.localPosition = _p0 + Vector3.up * Mathf.Sin(t) * amplitude;
            transform.localRotation = _r0 * Quaternion.Euler(Mathf.Sin(t * 0.7f) * swayDeg, 0, Mathf.Cos(t * 0.5f) * swayDeg);
        }
    }
}
