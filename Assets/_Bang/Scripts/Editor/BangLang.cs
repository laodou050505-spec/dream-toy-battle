// 编辑器里预设语言（菜单 Bang/语言）：写进和游戏里设置页同一个 PlayerPrefs 键 bang.lang，下次进播放模式生效。
// 游戏里玩家在设置页“语言 / Language”按钮切换；只有一个包。
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangLang
    {
        const string Key = "bang.lang";
        [MenuItem("Bang/语言/中文")] static void Zh() => Set(false);
        [MenuItem("Bang/语言/English")] static void En() => Set(true);
        [MenuItem("Bang/语言/中文", true)] static bool ZhV() { Menu.SetChecked("Bang/语言/中文", PlayerPrefs.GetString(Key, "zh") != "en"); return true; }
        [MenuItem("Bang/语言/English", true)] static bool EnV() { Menu.SetChecked("Bang/语言/English", PlayerPrefs.GetString(Key, "zh") == "en"); return true; }
        public static void Set(bool en) { PlayerPrefs.SetString(Key, en ? "en" : "zh"); PlayerPrefs.Save(); }
    }
}
