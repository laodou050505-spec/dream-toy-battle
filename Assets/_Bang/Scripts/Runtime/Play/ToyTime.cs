// 慢动作只作用于“玩具”：规则层、敌人、碎片特效按 Scale 放慢；玩家的头、手、枪、界面始终是真实时间（不晕）。
using UnityEngine;

namespace Bang.Play
{
    public static class ToyTime
    {
        public static float Scale { get; private set; } = 1f;
        static float _until, _min = 1f;

        /// <summary>放慢 dur 秒（真实时间）到 scale 倍；结束前 0.15 秒平滑恢复。已在慢动作中时取更慢、更长的那个。</summary>
        public static void Slow(float dur, float scale)
        {
            if (PlayerPrefs.GetInt("bang.slowmo", 1) == 0) return;
            _until = Mathf.Max(_until, Time.unscaledTime + dur); _min = Mathf.Min(_min, scale);
        }

        public static void Tick()
        {
            float left = _until - Time.unscaledTime;
            if (left <= 0f) { Scale = 1f; _min = 1f; return; }
            Scale = left < 0.15f ? Mathf.Lerp(1f, _min, left / 0.15f) : _min;
        }

        public static void Reset() { _until = 0; Scale = 1f; _min = 1f; }
    }
}
