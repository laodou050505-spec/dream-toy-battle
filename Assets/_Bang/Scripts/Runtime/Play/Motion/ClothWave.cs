// 旗布飘动（顶点动画，每面旗一份网格副本）：室内没有风，旗子不该一直猎猎作响——
// 只有旗杆被震动（插下去、挨打）时起一串波，从旗杆往外传、越往外摆得越大，然后阻尼衰减；平时只剩极轻的起伏。
using UnityEngine;

namespace Bang.Play.Motion
{
    [RequireComponent(typeof(MeshFilter))]
    public class ClothWave : MonoBehaviour
    {
        public float idleAmp = 0.003f, kickAmp = 0.03f, waveLength = 0.12f, speed = 2.2f, damping = 1.6f;
        Mesh _mesh; Vector3[] _base, _work; float _width = 0.2f, _energy, _t;

        void Awake()
        {
            var mf = GetComponent<MeshFilter>();
            _mesh = Instantiate(mf.sharedMesh); _mesh.name = mf.sharedMesh.name + "_Wave"; mf.sharedMesh = _mesh;
            _base = _mesh.vertices; _work = new Vector3[_base.Length];
            float mx = 0; foreach (var v in _base) mx = Mathf.Max(mx, Mathf.Abs(v.x)); _width = Mathf.Max(0.01f, mx);
            _t = Random.value * 10f;
        }

        /// <summary>旗杆被震一下（插下、挨打）：strength 0..1。</summary>
        public void Kick(float strength = 1f) => _energy = Mathf.Min(1.5f, _energy + strength);

        void Update()
        {
            if (!Application.isPlaying || _base == null) return;
            float dt = Time.deltaTime; _t += dt;
            _energy *= Mathf.Exp(-damping * dt);
            float amp = idleAmp + kickAmp * _energy;
            for (int i = 0; i < _base.Length; i++)
            {
                var v = _base[i];
                float u = Mathf.Abs(v.x) / _width;                         // 0 = 旗杆处，1 = 旗尾
                float w = Mathf.Sin((Mathf.Abs(v.x) / waveLength - _t * speed) * Mathf.PI * 2f) * amp * u * u;
                _work[i] = new Vector3(v.x, v.y - Mathf.Abs(w) * 0.25f * u, v.z + w);
            }
            _mesh.vertices = _work;
            _mesh.RecalculateNormals();
        }
    }
}
