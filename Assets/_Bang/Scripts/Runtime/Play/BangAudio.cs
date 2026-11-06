// 音效：3D 定位（声音从发生的地方传来——枪在手上、小怪在门口、旗子在地上），UI 提示音是 2D。
// 音源对象池复用；同名音效每次随机微调音高（±6%），连续开枪不会像复读机；同名音效 40 毫秒内不重复叠加。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Play
{
    public class BangAudio : MonoBehaviour
    {
        public static BangAudio Instance { get; private set; }

        [Serializable] public struct Entry { public string name; public AudioClip clip; [Range(0, 1)] public float volume; }
        public Entry[] clips;
        [Range(0, 1)] public float master = 0.9f;

        readonly Dictionary<string, Entry> _map = new();
        readonly Dictionary<string, float> _last = new();
        readonly List<AudioSource> _pool = new();
        AudioSource _ui;

        void Awake()
        {
            Instance = this;
            master = PlayerPrefs.GetFloat("bang.sfx", master);
            // 保险：场景里没有“耳朵”就挂到主相机上（没有 AudioListener 时全游戏静音）
            if (!FindAnyObjectByType<AudioListener>()) { var cam = Camera.main; if (cam) cam.gameObject.AddComponent<AudioListener>(); }
            foreach (var e in clips) if (e.clip) _map[e.name] = e;
            for (int i = 0; i < 16; i++) _pool.Add(MakeSource(true));
            _ui = MakeSource(false);
        }

        AudioSource MakeSource(bool spatial)
        {
            var g = new GameObject(spatial ? "Sfx3D" : "SfxUI"); g.transform.SetParent(transform, false);
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.15f, 0.85f), new Keyframe(1, 0.25f)));
            s.minDistance = 0.6f; s.maxDistance = 10f; s.dopplerLevel = 0;
            return s;
        }

        bool Get(string name, out Entry e)
        {
            if (!_map.TryGetValue(name, out e)) return false;
            float now = Time.unscaledTime;
            if (_last.TryGetValue(name, out var t) && now - t < 0.04f) return false;
            _last[name] = now;
            return true;
        }

        /// <summary>在世界位置 pos 播放（3D）。</summary>
        public void Play(string name, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            if (!Get(name, out var e)) return;
            AudioSource s = null;
            foreach (var p in _pool) if (!p.isPlaying) { s = p; break; }
            if (!s) s = _pool[0];
            s.transform.position = pos;
            s.pitch = pitch * UnityEngine.Random.Range(0.94f, 1.06f);
            s.PlayOneShot(e.clip, e.volume * vol * master);
        }

        /// <summary>UI / 全局提示音（2D）。</summary>
        public void UI(string name, float vol = 1f, float pitch = 1f)
        {
            if (!Get(name, out var e)) return;
            _ui.pitch = pitch;
            _ui.PlayOneShot(e.clip, e.volume * vol * master);
        }

        public static void At(string name, Vector3 pos, float vol = 1f, float pitch = 1f) { if (Instance) Instance.Play(name, pos, vol, pitch); }
        public static void Ui(string name, float vol = 1f, float pitch = 1f) { if (Instance) Instance.UI(name, vol, pitch); }
    }
}
