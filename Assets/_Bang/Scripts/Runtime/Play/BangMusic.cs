// 自适应背景音乐（素材来自 ange-embed 素材库，清单见 Docs/音效来源.md）：
//   封面 / 选关：温柔的木吉他小曲；休息 / 补给站：暖暖的小镇曲；
//   战斗：木琴马林巴“玩具们偷偷摸摸来了”——平时是悠闲版，房间被占得多 / 场上玩具多 / 门涌时切到紧张版（两首同乐器，交叉淡入淡出），
//         大波来袭换成更燃的一首；房间快被占领时音乐微微升调，再叠一层越来越快的“滴答”钟声；
//   通关 / 失败：短小的胜利 / 失落旋律，期间压低背景音乐；暂停：音乐压低并闷住（低通），像隔了一扇门。
// 每首曲子双音源无缝循环：快到结尾时另一个音源从头淡入。
using System;
using UnityEngine;

namespace Bang.Play
{
    public class BangMusic : MonoBehaviour
    {
        public static BangMusic Instance { get; private set; }

        [Serializable] public class Track { public string name; public AudioClip clip; [Range(0, 1)] public float volume = 1; public float loopStart, loopEnd; }
        public Track[] tracks;
        public AudioClip stingWin, stingLose;
        [Range(0, 1)] public float volume = 0.38f;
        public float fade = 2.2f;

        class Deck { public Track t; public AudioSource a, b; public AudioLowPassFilter la; public float level, target; }
        readonly System.Collections.Generic.Dictionary<string, Deck> _decks = new();
        AudioSource _sting; float _duck = 1, _duckT, _muffle, _pitch = 1, _pitchTarget = 1;
        string _want = "title";

        void Awake()
        {
            Instance = this;
            volume = PlayerPrefs.GetFloat("bang.music", volume);
            foreach (var t in tracks) if (t.clip) _decks[t.name] = MakeDeck(t);
            _sting = gameObject.AddComponent<AudioSource>(); _sting.playOnAwake = false; _sting.spatialBlend = 0;
        }

        Deck MakeDeck(Track t)
        {
            var g = new GameObject("Music_" + t.name); g.transform.SetParent(transform, false);
            AudioSource Src() { var s = g.AddComponent<AudioSource>(); s.clip = t.clip; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0; s.priority = 0; return s; }
            var d = new Deck { t = t, a = Src(), b = Src() };
            d.la = g.AddComponent<AudioLowPassFilter>(); d.la.cutoffFrequency = 22000;     // 同一物体上的滤波器作用于两个音源
            if (t.loopEnd <= 0) t.loopEnd = t.clip.length;
            return d;
        }

        /// <summary>想听哪首（"title" / "rest" / "low" / "high" / "big" / null = 安静）。</summary>
        public void Want(string name) => _want = name;
        /// <summary>提前把这首曲子解码进内存（切过去的那一帧不再卡）。</summary>
        public void Prewarm(string name) { foreach (var t in tracks) if (t.name == name && t.clip && t.clip.loadState != AudioDataLoadState.Loaded) t.clip.LoadAudioData(); }
        public bool Has(string name) => _decks != null && _decks.ContainsKey(name);
        public void SetPitch(float p) => _pitchTarget = p;
        public void SetMuffle(bool on) => _muffle = on ? 1 : 0;
        public void SetVolume(float v) { volume = v; PlayerPrefs.SetFloat("bang.music", v); }

        /// <summary>胜利 / 失败旋律：期间背景音乐压到很低。</summary>
        public void Sting(bool win)
        {
            var c = win ? stingWin : stingLose; if (!c) return;
            _sting.PlayOneShot(c, (win ? 0.9f : 0.8f) * Mathf.Max(volume * 2f, 0.4f) * (BangAudio.Instance ? BangAudio.Instance.master / 0.9f : 1));
            _duckT = c.length + 0.3f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _duckT -= dt;
            _duck = Mathf.MoveTowards(_duck, _duckT > 0 ? 0.15f : 1f, dt * (_duckT > 0 ? 4f : 0.6f));
            _pitch = Mathf.MoveTowards(_pitch, _pitchTarget, dt * 0.02f);
            float cutoff = Mathf.Lerp(22000, 900, _muffle);
            foreach (var kv in _decks)
            {
                var d = kv.Value;
                d.target = kv.Key == _want ? 1 : 0;
                d.level = Mathf.MoveTowards(d.level, d.target, dt / fade);
                d.la.cutoffFrequency = Mathf.Lerp(d.la.cutoffFrequency, cutoff, 1 - Mathf.Exp(-dt * 6f));
                if (d.level <= 0) { if (d.a.isPlaying) d.a.Stop(); if (d.b.isPlaying) d.b.Stop(); continue; }
                Loop(d);
                float v = d.level * d.level * d.t.volume * volume * _duck * (1 - 0.45f * _muffle);
                bool battle = kv.Key == "low" || kv.Key == "high" || kv.Key == "big";
                foreach (var s in new[] { d.a, d.b })
                {
                    if (!s.isPlaying) continue;
                    // 每个音源自己的循环交叉淡化
                    float edge = Mathf.Min(Mathf.Clamp01((s.time - d.t.loopStart) / 0.25f), Mathf.Clamp01((d.t.loopEnd - s.time) / 0.25f));
                    s.volume = v * edge;
                    s.pitch = battle ? _pitch : 1f;
                    if (s.time >= d.t.loopEnd) s.Stop();
                }
            }
        }

        static void Loop(Deck d)
        {
            if (!d.a.isPlaying && !d.b.isPlaying) { d.a.time = d.t.loopStart; d.a.Play(); return; }
            // 正在播的那个快到循环终点：另一个从头开始，交叉 0.25 秒
            var cur = d.a.isPlaying && (!d.b.isPlaying || d.a.time > d.b.time) ? d.a : d.b;
            var other = cur == d.a ? d.b : d.a;
            if (!other.isPlaying && cur.time >= d.t.loopEnd - 0.25f) { other.time = d.t.loopStart; other.Play(); }
        }
    }
}
