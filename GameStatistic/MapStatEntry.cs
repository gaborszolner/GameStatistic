using System.Text.Json.Serialization;

namespace GameStatistic
{
    internal class MapStatEntry(string name, int tWin = 0, int ctWin = 0, int mapStarted = 0, int mapFullPlayed = 0)
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = name;

        [JsonPropertyName("tWin")]
        public int TtWin { get; set; } = tWin;

        [JsonPropertyName("ctWin")]
        public int CTWin { get; set; } = ctWin;

        [JsonPropertyName("mapStarted")]
        public int MapStarted { get; set; } = mapStarted;

        [JsonPropertyName("mapFullPlayed")]
        public int MapFullPlayed { get; set; } = mapFullPlayed;
    }
}
