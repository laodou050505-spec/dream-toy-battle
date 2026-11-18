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
        /// <summary>启动画面（11-17 用户：去掉 Made with Unity，换成排版一样的 MADE WITH TRIPO —— 左边两行小号大写字 MADE / WITH，右边官方 TRIPO 标志）。
        /// 仍然用 Unity 自带的启动画面（位置、大小、淡入淡出、深色背景都是同一套），只是关掉 Unity 标志、换成公司给的现成图（Art/Brand，原样使用）。
        /// 头显（XR）里 Unity 不画“标志列表”，只认 Virtual Reality Splash Image 这一张贴图，所以两处都要填。每次打包自动执行；图不在就不改</summary>
        public static string ApplySplash()
        {
            const string path = "Assets/_Bang/Art/Brand/made_with_tripo.png";          // 普通启动画面用：透明底
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[Bang] 没找到启动画面图 " + path + "，启动画面保持原样"); return "缺图 " + path; }
            if (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Uncompressed)
            { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sp) { Debug.LogWarning("[Bang] 启动画面图导入失败 " + path); return "导入失败 " + path; }
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, sp) };
            const string vrPath = "Assets/_Bang/Art/Brand/made_with_tripo_vr.png";     // 头显用：不透明深色底、内容居中
            var vi = AssetImporter.GetAtPath(vrPath) as TextureImporter;
            if (vi == null) { Debug.LogWarning("[Bang] 没找到头显启动图 " + vrPath); return "缺图 " + vrPath; }
            if (vi.textureType != TextureImporterType.Default || vi.mipmapEnabled || vi.textureCompression != TextureImporterCompression.Uncompressed || vi.npotScale != TextureImporterNPOTScale.None)
            { vi.textureType = TextureImporterType.Default; vi.mipmapEnabled = false; vi.textureCompression = TextureImporterCompression.Uncompressed; vi.npotScale = TextureImporterNPOTScale.None; vi.wrapMode = TextureWrapMode.Clamp; vi.sRGBTexture = true; vi.SaveAndReimport(); }
            var vt = AssetDatabase.LoadAssetAtPath<Texture2D>(vrPath);
            var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (!vt || all == null || all.Length == 0) { Debug.LogWarning("[Bang] 头显启动图没设上"); return "头显启动图没设上"; }
            var so = new SerializedObject(all[0]); var prop = so.FindProperty("m_VirtualRealitySplashScreen");
            if (prop == null) { Debug.LogWarning("[Bang] 这个版本的 Unity 没有 m_VirtualRealitySplashScreen"); return "没有 m_VirtualRealitySplashScreen"; }
            if (prop.objectReferenceValue != vt) { prop.objectReferenceValue = vt; so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
            return $"OK show={PlayerSettings.SplashScreen.show} unityLogo={PlayerSettings.SplashScreen.showUnityLogo} logos={PlayerSettings.SplashScreen.logos.Length}:{PlayerSettings.SplashScreen.logos[0].logo?.name} vr={(prop.objectReferenceValue ? prop.objectReferenceValue.name : "无")} {vt.width}x{vt.height}";
        }

        /// <summary>一个包，游戏里设置页可切换中文 / English。</summary>
        [MenuItem("Bang/打包 APK（PICO）")]
        public static string BuildApk()
        {
            ApplySplash();                                            // 启动画面：MADE WITH TRIPO（普通 + 头显两处）
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
