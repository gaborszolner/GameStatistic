using System.Text.Json.Serialization;

namespace GameStatistic
{
    internal class MapStatEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tWin")]
        public int TWin { get; set; }

        [JsonPropertyName("ctWin")]
        public int CTWin { get; set; }

        [JsonPropertyName("mapStarted")]
        public int MapStarted { get; set; }

        [JsonPropertyName("mapFullPlayed")]
        public int MapFullPlayed { get; set; }


        public MapStatEntry(string name, int tWin = 0, int ctWin = 0, int mapStarted = 0, int mapFullPlayed = 0)
        {
            Name = name;
            TWin = tWin;
            CTWin = ctWin;
            MapStarted = mapStarted;
            MapFullPlayed = mapFullPlayed;
        }

        public MapStatEntry() { }
    }
}
