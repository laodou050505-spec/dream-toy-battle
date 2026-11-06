// 存档：规则层的 SaveData（进度、星级、金币、升级、臂长校准、续玩快照）序列化成 JSON 存进 PlayerPrefs。
using Bang.Logic;
using Newtonsoft.Json;
using UnityEngine;

namespace Bang.Play
{
    public class PrefsSaveStore : ISaveStore
    {
        const string Key = "bang.save.v1";

        public SaveData Load()
        {
            var s = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(s)) return null;
            try { return JsonConvert.DeserializeObject<SaveData>(s); }
            catch (System.Exception e) { Debug.LogWarning("[Bang] 存档读不出来，重新开始：" + e.Message); return null; }
        }

        public void Save(SaveData d)
        {
            PlayerPrefs.SetString(Key, JsonConvert.SerializeObject(d, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
            PlayerPrefs.Save();
        }
    }
}
