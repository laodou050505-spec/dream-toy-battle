using UnityEngine;

namespace Bang.Ambient
{
    public class Spin : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float degPerSec = 40f;
        [Tooltip("风力驱动的东西（风车）：转速随阵风起伏的比例，0 = 匀速（马达驱动）")] public float gust;
        float _speed = -1;
        void Update()
        {
            float target = degPerSec * (1 + gust * (Mathf.PerlinNoise(Time.time * 0.12f, 3.7f) * 2 - 1));
            _speed = _speed < 0 ? target : Mathf.Lerp(_speed, target, Time.deltaTime * 0.8f);   // 惯性：转速慢慢跟上风
            transform.Rotate(axis, _speed * Time.deltaTime, Space.Self);
        }
    }
}
