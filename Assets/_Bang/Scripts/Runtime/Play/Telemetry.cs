// 真机试玩记录：每波结束 / 失败 / 通关写一行 JSON 到 persistentDataPath/bang_telemetry.jsonl，
// 之后用 adb pull 拿回来按真实数据调难度（每波用时、命中率、被打中次数、在哪儿输的）。
using System.IO;
using UnityEngine;

namespace Bang.Play
{
    public static class Telemetry
    {
        static string PathOf => System.IO.Path.Combine(Application.persistentDataPath, "bang_telemetry.jsonl");

        public static void Log(string evt, int chapter, int wave, float seconds, int shots, int hits, int kills, int headHits, float toy, string extra = "")
        {
            try
            {
                var line = $"{{\"t\":\"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"evt\":\"{evt}\",\"ch\":{chapter},\"wave\":{wave + 1},\"sec\":{seconds:0.0},\"shots\":{shots},\"hits\":{hits},\"kills\":{kills},\"headHits\":{headHits},\"toy\":{toy:0.0}{extra}}}";
                File.AppendAllText(PathOf, line + "\n");
            }
            catch { }
        }
    }
}
