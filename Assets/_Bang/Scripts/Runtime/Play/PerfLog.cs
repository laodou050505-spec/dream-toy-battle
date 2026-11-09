// 真机性能记录（用户：终极决战小怪一多就明显卡顿）：开销极小，一直开着。
//   ① 每 2 秒往 logcat 写一行 [BangPerf]：这段时间的平均帧率、最慢一帧、超过 1.5 倍帧预算的“卡顿帧”数、CPU 主线程 / 渲染线程 / GPU 平均耗时（FrameTimingManager）、
//      场上敌人数、旗子数、GC 次数——adb logcat -s Unity | grep BangPerf 就能看出是 CPU 还是 GPU 跟不上、卡顿和什么同时出现。
//   ② 每波累计：平均帧率、最慢一帧、卡顿帧数、GPU 平均，写进 bang_telemetry.jsonl 的 waveEnd / clear / lost 行（WaveExtra）。
using UnityEngine;

namespace Bang.Play
{
    public class PerfLog : MonoBehaviour
    {
        public GameDriver driver;
        const float Window = 2f;
        readonly FrameTiming[] _ft = new FrameTiming[1];
        float _wT, _wMax; int _wN, _wHitch; double _wCpu, _wRt, _wGpu; int _wFt, _gc0;
        float _vT, _vMax; int _vN, _vHitch; double _vGpu; int _vFt;          // 本波累计

        float Budget => 1000f / Mathf.Max(30f, Screen.currentResolution.refreshRateRatio.value > 1 ? (float)Screen.currentResolution.refreshRateRatio.value : 72f);

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            FrameTimingManager.CaptureFrameTimings();
            bool ft = FrameTimingManager.GetLatestTimings(1, _ft) > 0;
            float hitchMs = Budget * 1.5f;
            _wT += ms * 0.001f; _wN++; if (ms > _wMax) _wMax = ms; if (ms > hitchMs) _wHitch++;
            _vT += ms * 0.001f; _vN++; if (ms > _vMax) _vMax = ms; if (ms > hitchMs) _vHitch++;
            if (ft) { _wCpu += _ft[0].cpuMainThreadFrameTime; _wRt += _ft[0].cpuRenderThreadFrameTime; _wGpu += _ft[0].gpuFrameTime; _wFt++; _vGpu += _ft[0].gpuFrameTime; _vFt++; }
            if (_wT < Window) return;
            var g = driver ? driver.Game : null;
            int alive = 0, flags = 0; string phase = "-"; int ch = 0, wave = 0;
            if (g != null) { foreach (var e in g.enemies) if (!e.dead) alive++; flags = g.flags.Count; phase = g.phase; ch = driver.chapter; wave = g.run != null ? g.run.wave + 1 : 0; }
            int gcN = System.GC.CollectionCount(0);
            Debug.Log($"[BangPerf] ch={ch} wave={wave} phase={phase} fps={_wN / _wT:F1} avg={_wT * 1000f / _wN:F1}ms max={_wMax:F1}ms hitch={_wHitch} " +
                      (_wFt > 0 ? $"cpu={_wCpu / _wFt:F1} rt={_wRt / _wFt:F1} gpu={_wGpu / _wFt:F1} " : "ft=off ") +
                      $"enemies={alive} flags={flags} gc={gcN - _gc0} mono={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576}MB");
            _gc0 = gcN; _wT = 0; _wMax = 0; _wN = 0; _wHitch = 0; _wCpu = _wRt = _wGpu = 0; _wFt = 0;
        }

        /// <summary>本波性能（写进游玩记录），取完清零。</summary>
        public string WaveExtra()
        {
            string s = _vN > 0 ? $",\"fps\":{_vN / Mathf.Max(0.001f, _vT):0.0},\"worstMs\":{_vMax:0.0},\"hitches\":{_vHitch}" + (_vFt > 0 ? $",\"gpuMs\":{_vGpu / _vFt:0.0}" : "") : "";
            _vT = 0; _vMax = 0; _vN = 0; _vHitch = 0; _vGpu = 0; _vFt = 0;
            return s;
        }
        public void ResetWave() { _vT = 0; _vMax = 0; _vN = 0; _vHitch = 0; _vGpu = 0; _vFt = 0; }
    }
}
