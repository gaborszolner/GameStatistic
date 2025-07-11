using System.Text.Json.Serialization;

namespace GameStatistic
{
    internal class MapStatEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tWin")]
        public int TtWin { get; set; }

        [JsonPropertyName("ctWin")]
        public int CTWin { get; set; }

        [JsonPropertyName("startedRound")]
        public int StartedRound { get; set; }

        [JsonPropertyName("playedRound")]
        public int PlayedRound { get; set; }

        [JsonPropertyName("rtv")]
        public int Rtv { get; set; }

        public MapStatEntry(string name, int tWin = 0, int ctWin = 0, int startedRound = 0, int playedRound = 0, int rtv = 0)
        {
            Name = name;
            TtWin = tWin;
            CTWin = ctWin;
            StartedRound = startedRound;
            PlayedRound = playedRound;
            Rtv = rtv;
        }

        public MapStatEntry() { }
    }
}
