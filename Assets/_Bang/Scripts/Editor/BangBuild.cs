// 打包：切到 Android（PICO），只打主场景，输出 Builds/BANG_<日期>.apk。
// 菜单：Bang/打包 APK（PICO）。真机输入用 PicoInput（手部追踪 + 眼动），编辑器预览客厅带 EditorOnly 标签，打包时自动剔除。
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangBuild
    {
        /// <summary>一个包，游戏里设置页可切换中文 / English。</summary>
        [MenuItem("Bang/打包 APK（PICO）")]
        public static string BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                // 切平台后要重新编译，PICO SDK 的清单注入（PXR_Manifest，只在 UNITY_ANDROID 下编译）才会存在；
                // 同一次调用里直接打包会漏掉 VR 标记，装到头显上变成 2D 窗口。所以切完就停，等编译完再打。
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android)) return "切换 Android 平台失败";
                return "已切到 Android，等编译完成后再执行一次打包";
            }
            BangPicoSetup.Ensure();                                   // 手部追踪 / 眼动 / 视频透视权限写进清单
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;   // PICO SDK 校验：必须用 Activity，不能用 GameActivity
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.toybox.bang");       // 包名不变：头显上直接覆盖安装
            PlayerSettings.productName = "梦境玩具战";
            PlayerSettings.bundleVersion = "0.3." + DateTime.Now.ToString("MMdd");
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "../Builds"));
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, $"../Builds/DreamToyBattle_{DateTime.Now:MMdd_HHmm}.apk"));
            var opts = new BuildPlayerOptions
            {
                scenes = BangSceneBuilder.BuildScenePaths(),          // 主场景 + 六章世界场景（缺了世界场景，头显里门后什么都没有）
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var rep = BuildPipeline.BuildPlayer(opts);
            var s = rep.summary;
            string msg = $"{s.result} 用时 {s.totalTime.TotalMinutes:0.0} 分钟 大小 {s.totalSize / 1048576f:0.0} MB 错误 {s.totalErrors} → {path}";
            File.AppendAllText("/tmp/bang_build.txt", msg + "\n");
            Debug.Log("[Bang] 打包：" + msg);
            return msg;
        }
    }
}
