// PICO 工程设置：SDK 只在构建平台是 Android 时才把 PXR_ProjectSetting 存成资产，否则每次编译都在内存里新建一份，
// 于是“Portal 已初始化”永远记不住——每次脚本重新编译都会弹出 PICO Portal 窗口和 XR Project Settings 页面。
// 这里把设置存到 Assets/Resources/PXR_ProjectSetting.asset，并打开本游戏需要的功能：手部追踪、眼动追踪、视频透视。
// 缺这个资产时真机上读手部数据会空引用，打包也写不进手部 / 眼动权限。
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangPicoSetup
    {
        const string Path = "Assets/Resources/PXR_ProjectSetting.asset";

        [MenuItem("Bang/0 PICO 工程设置（手部 / 眼动 / 透视）")]
        public static string Ensure()
        {
            var c = AssetDatabase.LoadAssetAtPath<PXR_ProjectSetting>(Path);
            if (!c)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                c = ScriptableObject.CreateInstance<PXR_ProjectSetting>();
                c.handTrackingSupportType = HandTrackingSupport.ControllersAndHands;
                c.meshLod = PxrMeshLod.Low;
                c.lightEstimationTextureResolution = PxrEnvironmentTextureResolution.Size8x8;
                c.openMRC = true;
                AssetDatabase.CreateAsset(c, Path);
            }
            c.portalInited = true;          // 不再每次编译都弹 Portal
            c.handTracking = true;
            c.eyeTracking = true;
            c.videoSeeThrough = true;
            c.spatialMesh = true;           // 真实房间的空间网格（玩具化爬上墙和家具、蜡笔痕留在真实表面）
            c.meshLod = PxrMeshLod.Low;
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (w.GetType().Name == "PXR_PortalEditor") w.Close();
            return $"portalInited={c.portalInited} hand={c.handTracking} eye={c.eyeTracking} vst={c.videoSeeThrough}";
        }
    }
}
