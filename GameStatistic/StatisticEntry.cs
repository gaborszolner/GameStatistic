using System.Text.Json.Serialization;

namespace GameStatistic
{
    internal class StatisticEntry(string identity, string name, int kill, int dead, int selfKill)
    {
        [JsonPropertyName("identity")]
        public string Identity { get; set; } = identity;

        [JsonPropertyName("name")]
        public string Name { get; set; } = name;

        [JsonPropertyName("kill")]
        public int Kill { get; set; } = kill;

        [JsonPropertyName("dead")]
        public int Dead { get; set; } = dead;

        [JsonPropertyName("selfkill")]
        public int SelfKill { get; set; } = selfKill;
    }
}
