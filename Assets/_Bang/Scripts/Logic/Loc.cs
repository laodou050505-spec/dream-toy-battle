// 中英双语：所有玩家看得到的文字都写成 Loc.T("中文", "English")。
// 一个游戏里切换：设置页“语言 / Language”按钮，选择存在 PlayerPrefs（bang.lang）；第一次启动按系统语言（中文系统默认中文，其余默认英文）。
namespace Bang.Logic
{
    public static class Loc
    {
        public static bool En;
        public static string T(string zh, string en) => En ? en : zh;
    }
}
