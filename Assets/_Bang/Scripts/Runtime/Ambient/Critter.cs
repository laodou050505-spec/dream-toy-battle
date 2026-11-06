using UnityEngine;

namespace Bang.Ambient
{
    // 门后世界的小生命（折纸小鸟 / 毛毡蝴蝶）：沿不规则的闭合航线飞，转弯时倾斜，翅膀扇动。
    // 小鸟：爬升时扇翅、下降时滑翔；蝴蝶：快速扑翅、上下颠簸。
    // 编辑态也摆在航线起点（不然都堆在父物体原点，摊在地上和别的东西穿插）。
    [ExecuteAlways]
    public class Critter : MonoBehaviour
    {
        public Transform wingL, wingR;
        public Vector3 center;                    // 父物体本地坐标
        public Vector2 radius = new Vector2(2f, 1.4f);
        public float height = 1.6f, bob = 0.25f, speed = 0.35f, phase;
        public bool butterfly;
        float _t, _flapMix = 1f;

        Vector3 P(float a)
        {
            // 两个频率叠加的闭合曲线：不是死板的椭圆
            float x = Mathf.Cos(a) * radius.x + Mathf.Cos(a * 2f + phase) * radius.x * 0.18f;
            float z = Mathf.Sin(a) * radius.y + Mathf.Sin(a * 3f + phase * 1.7f) * radius.y * 0.15f;
            float y = height + Mathf.Sin(a * 2f + phase * 0.6f) * bob + (butterfly ? Mathf.Sin(a * 13f + phase) * 0.06f : 0f);
            return center + new Vector3(x, y, z);
        }

        void OnEnable() { _t = phase * 3.1f; Pose(_t); }
        public void Snap() { _t = phase * 3.1f; Pose(_t); }
        void OnValidate() { if (isActiveAndEnabled) { _t = phase * 3.1f; Pose(_t); } }
        /// <summary>航线上第 k / n 个采样点（父物体本地坐标），构建时用来检查航线有没有穿过别的东西。</summary>
        public Vector3 PathPoint(int k, int n) => P(k / (float)n * Mathf.PI * 2f);
        void Update() { if (Application.isPlaying) { _t += Time.deltaTime; Pose(_t); } }

        void Pose(float t)
        {
            float perim = Mathf.PI * (radius.x + radius.y);
            float a = t * speed / perim * Mathf.PI * 2f + phase;
            var p = P(a); var q = P(a + 0.03f); var r = P(a + 0.06f);
            var fwd = (q - p).normalized;
            float turn = Vector3.SignedAngle(new Vector3(fwd.x, 0, fwd.z), new Vector3((r - q).x, 0, (r - q).z), Vector3.up);
            transform.localPosition = p;
            if (fwd.sqrMagnitude > 1e-6) transform.localRotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0, 0, -turn * (butterfly ? 4f : 9f));
            float flap;
            if (butterfly) flap = Mathf.Sin(t * 15f + phase) * 55f + 10f;
            else
            {
                // 爬升或平飞要扇翅，下降时张开翅膀滑翔（翅膀角度平滑过渡）
                float climb = (q.y - p.y) / 0.03f;
                _flapMix = Mathf.MoveTowards(_flapMix, climb > -0.02f ? 1f : 0f, Time.deltaTime * 2.5f);
                flap = Mathf.Lerp(8f + Mathf.Sin(t * 2f) * 3f, Mathf.Sin(t * 9f + phase) * 34f, _flapMix);
            }
            if (wingR) wingR.localRotation = Quaternion.Euler(0, 0, flap);
            if (wingL) wingL.localRotation = Quaternion.Euler(0, 0, -flap);
        }
    }
}
