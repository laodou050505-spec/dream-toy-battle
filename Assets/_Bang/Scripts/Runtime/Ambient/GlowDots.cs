// 静止的发光小点（彩灯泡的光晕）：启用时用粒子系统在固定位置发射永久粒子，并带一点呼吸闪烁。
// 粒子不会存进场景，所以每次启用（编辑器里也是）重新发射。
using UnityEngine;

namespace Bang.Ambient
{
    [ExecuteAlways, RequireComponent(typeof(ParticleSystem))]
    public class GlowDots : MonoBehaviour
    {
        public Vector3[] points;                 // 本地坐标
        public Color[] colors;
        public float size = 0.05f;
        [Tooltip("呼吸闪烁幅度")] public float twinkle = 0.35f;
        ParticleSystem _ps;
        ParticleSystem.Particle[] _buf;

        void OnEnable()
        {
            _ps = GetComponent<ParticleSystem>();
            Emit();
        }

        public void Emit()
        {
            if (!_ps) _ps = GetComponent<ParticleSystem>();
            if (points == null) return;
            var main = _ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = Mathf.Max(points.Length, 1);
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = 0; main.gravityModifier = 0;
            var em = _ps.emission; em.enabled = false;
            _ps.Clear();
            _buf = new ParticleSystem.Particle[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                _buf[i] = new ParticleSystem.Particle
                {
                    position = points[i], startSize = size, startLifetime = 1e6f, remainingLifetime = 1e6f,
                    startColor = colors != null && i < colors.Length ? colors[i] : Color.white, randomSeed = (uint)(i * 7919 + 13),
                };
            }
            _ps.SetParticles(_buf, _buf.Length);
            _ps.Pause();
        }

        void Update()
        {
            if (_buf == null || !_ps || twinkle <= 0 || !Application.isPlaying) return;
            float t = Time.time;
            for (int i = 0; i < _buf.Length; i++)
            {
                float k = 1f - twinkle * 0.5f + twinkle * 0.5f * Mathf.Sin(t * (1.3f + (i % 5) * 0.37f) + i * 1.7f);
                _buf[i].startSize = size * k;
            }
            _ps.SetParticles(_buf, _buf.Length);
        }
    }
}
