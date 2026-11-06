// MR 启动：头显上开启 PICO 视频透视（相机清屏为全透明，真实房间就是背景），追踪原点设为地面。
// 应用从后台恢复时 PICO 会关掉透视，需要重新打开。编辑器里不做任何事（用预览客厅代替真实房间）。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Bang
{
    public class MRBootstrap : MonoBehaviour
    {
        public static MRBootstrap Instance { get; private set; }
        /// <summary>对齐完成（界面锚点、HUD 要跟着重新摆）。</summary>
        public static event System.Action Aligned;
        public Camera cam;
        bool _aligned;
        void Awake() => Instance = this;

        /// <summary>
        /// 对齐游戏空间：规则层假定玩家站在原点、面朝 −Z，门在正前方 3.2 米。PICO 开机时追踪原点的朝向不一定是玩家面朝的方向，
        /// 所以把 XR Origin 旋转 / 平移，使玩家此刻的头正好在原点上方、面朝 −Z（地面高度不变）。开局和每次开章都对齐一次。
        /// </summary>
        public void AlignPlayArea()
        {
            if (!cam || !XRSettings.isDeviceActive) return;
            var origin = transform;
            var lp = origin.InverseTransformPoint(cam.transform.position);
            var lf = Quaternion.Inverse(origin.rotation) * cam.transform.forward; lf.y = 0;
            if (lf.sqrMagnitude < 1e-4f) return;
            float lyaw = Mathf.Atan2(lf.x, lf.z) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0, 180f - lyaw, 0);
            origin.rotation = rot;
            var p = new Vector3(0, lp.y, 0) - rot * lp;
            origin.position = new Vector3(p.x, 0, p.z);
            _aligned = true;
            Aligned?.Invoke();
        }
        public bool passthrough = true;
        float _retry;
        bool _floorSet;

        void Start() => Apply();

        void OnApplicationPause(bool paused) { if (!paused) Apply(); }

        void Apply()
        {
            if (Application.isEditor || !XRSettings.isDeviceActive) return;
            if (passthrough)
            {
                ByteDance.PICO.XR.PXR_Manager.EnableVideoSeeThrough = true;
                if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); }
            }
        }

        void Update()
        {
            // 头显追踪起来后（头不在原点了）自动对齐一次
            if (!_aligned && XRSettings.isDeviceActive && cam && cam.transform.localPosition.sqrMagnitude > 0.01f) AlignPlayArea();
            if (_floorSet || !XRSettings.isDeviceActive) return;
            _retry -= Time.unscaledDeltaTime;
            if (_retry > 0) return;
            _retry = 1f;
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            foreach (var s in subs)
                if ((s.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                    _floorSet |= s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }
    }
}
