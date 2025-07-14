using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Text.Json;

namespace GameStatistic
{
    public class GameStatistic : BasePlugin
    {
        public override string ModuleName => "GameStatistic";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "GameStatistic";

        public string PluginPrefix = $"[GameStatistic]";

        private static string _playerStatFilePath = string.Empty;
        private static string _mapStatFilePath = string.Empty;

        private static Dictionary<string, PlayerStatEntry> _playerStatEntries = new();
        private static KeyValuePair<string, MapStatEntry> _mapStatEntry;
        private static bool _isWarmup = false;
        private static bool _isRoundEnded = false;
        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundAnnounceFinal>(OnRoundAnnounceFinal);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            _playerStatFilePath = Path.Combine(ModuleDirectory, "playerStatistic.json");
            _mapStatFilePath = Path.Combine(ModuleDirectory, "mapStatistic.json");
        }

        private HookResult OnRoundAnnounceFinal(EventRoundAnnounceFinal @event, GameEventInfo info)
        {
            _mapStatEntry.Value.MapFullPlayed++;
            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);
            storedStats = MergeStatToStored(storedStats, _mapStatEntry);
            FileWriteAll<Dictionary<string, MapStatEntry>>(_mapStatFilePath, storedStats);
            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _isRoundEnded = false;
            string? mapName = Server.MapName?.Trim();
            if (string.IsNullOrWhiteSpace(mapName))
            {
                return HookResult.Continue;
            }

            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);
            _mapStatEntry = new KeyValuePair<string, MapStatEntry>(mapName, new MapStatEntry(mapName));
            _mapStatEntry.Value.MapStarted++;
            storedStats = MergeStatToStored(storedStats, _mapStatEntry);
            FileWriteAll<Dictionary<string, MapStatEntry>>(_mapStatFilePath, storedStats);
            _mapStatEntry.Value.MapStarted = 0;
            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;

            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);
            if(storedStats is not null && storedStats.ContainsKey(Server.MapName.Trim())) 
            { 
                int ctWin = storedStats[Server.MapName.Trim()].CTWin;
                int tWin = storedStats[Server.MapName.Trim()].TWin;
                int fullRoundCount = ctWin + tWin;

                double tWinPercentage = (double)tWin / fullRoundCount * 100;
                double ctWinPercentage = (double)ctWin / fullRoundCount * 100;

                Server.PrintToChatAll($"{ChatColors.Yellow}{PluginPrefix} {ChatColors.Default}- On this map {ChatColors.Red} T win: {tWinPercentage:F2}%, {ChatColors.Blue}CT win: {ctWinPercentage:F2}%");
            }

            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            if(_isWarmup || _isRoundEnded)
            {
                return HookResult.Continue;
            }

            var attacker = @event.Attacker;
            var victim = @event.Userid;
            var assister = @event.Assister;

            if (victim is null || attacker is null || victim.AuthorizedSteamID is null || attacker.AuthorizedSteamID is null || attacker.IsBot || victim.IsBot)
            {
                return HookResult.Continue;
            }

            if (assister is not null && assister.AuthorizedSteamID != null)
            {
                var steamId = victim.AuthorizedSteamID.SteamId2;
                if (!_playerStatEntries.ContainsKey(steamId))
                {
                    _playerStatEntries[steamId] = new PlayerStatEntry(steamId, victim.PlayerName);
                }

                _playerStatEntries[steamId].Assister++;
            }

            if (victim.AuthorizedSteamID != null && attacker != victim)
            {
                var steamId = victim.AuthorizedSteamID.SteamId2;
                if (!_playerStatEntries.ContainsKey(steamId))
                {
                    _playerStatEntries[steamId] = new PlayerStatEntry(steamId, victim.PlayerName);
                }

                _playerStatEntries[steamId].Dead++;
            }

            if (attacker?.AuthorizedSteamID != null && attacker != victim)
            {
                var steamId = attacker.AuthorizedSteamID.SteamId2;
                if (!_playerStatEntries.ContainsKey(steamId))
                {
                    _playerStatEntries[steamId] = new PlayerStatEntry(steamId, attacker.PlayerName);
                }
                if (attacker.Team == victim.Team)
                {
                    _playerStatEntries[steamId].TeamKill++;
                }
                else
                {
                    _playerStatEntries[steamId].Kill++;
                }
            }

            if (attacker?.AuthorizedSteamID != null && attacker == victim && !attacker.JustBecameSpectator && !attacker.TeamChanged)
            {
                var steamId = attacker.AuthorizedSteamID.SteamId2;
                if (!_playerStatEntries.ContainsKey(steamId))
                {
                    _playerStatEntries[steamId] = new PlayerStatEntry(steamId, attacker.PlayerName, 0, 0, 0, 0);
                }
                _playerStatEntries[steamId].SelfKill++;
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _isRoundEnded = true;
            CreatePlayerStatistic();
            CreateMapStatisticRoundEnd(@event.Winner);
            return HookResult.Continue;
        }

        private static void CreateMapStatisticRoundEnd(int winnerTeam)
        {
            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);

            if (winnerTeam == 2)
            {
                _mapStatEntry.Value.TWin++;
            }
            else if (winnerTeam == 3)
            {
                _mapStatEntry.Value.CTWin++;
            }

            storedStats = MergeStatToStored(storedStats, _mapStatEntry);
            FileWriteAll<Dictionary<string, MapStatEntry>>(_mapStatFilePath, storedStats);
        }

        private static Dictionary<string, MapStatEntry>? MergeStatToStored(Dictionary<string, MapStatEntry>? storedStats, KeyValuePair<string, MapStatEntry> mapStatEntry)
        {
            if (string.IsNullOrWhiteSpace(mapStatEntry.Key))
            {
                return null;
            }

            storedStats ??= [];

            if (storedStats.ContainsKey(mapStatEntry.Key))
            {
                var existing = storedStats[mapStatEntry.Key];
                existing.TWin += mapStatEntry.Value.TWin;
                existing.CTWin += mapStatEntry.Value.CTWin;
                existing.MapFullPlayed += mapStatEntry.Value.MapFullPlayed;
            }
            else
            {
                storedStats[mapStatEntry.Key] = mapStatEntry.Value;
            }

            return storedStats;
        }

        private static void FileWriteAll<T>(string statFilePath, T? storedStats)
        {
            if (storedStats is null) 
            {
                return;
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(statFilePath, JsonSerializer.Serialize(storedStats, options));
            }
            catch (Exception)
            { }
        }

        private void CreatePlayerStatistic()
        {
            var storedStats =
                ReadStoredStat<Dictionary<string, PlayerStatEntry>>(_playerStatFilePath) 
                ?? [];

            foreach (var kvp in _playerStatEntries)
            {
                if (storedStats.ContainsKey(kvp.Key))
                {
                    var existing = storedStats[kvp.Key];
                    existing.Kill += kvp.Value.Kill;
                    existing.Dead += kvp.Value.Dead;
                    existing.Assister += kvp.Value.Assister;
                    existing.SelfKill += kvp.Value.SelfKill;
                    existing.TeamKill += kvp.Value.TeamKill;
                }
                else
                {
                    storedStats[kvp.Key] = kvp.Value;
                }
            }

            FileWriteAll(_playerStatFilePath, storedStats);

            _playerStatEntries.Clear();
        }

        private static T? ReadStoredStat<T>(string filePath)
        {
            T? storedStats = default;
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);

                var deserialized = JsonSerializer.Deserialize<T>(json);

                if (deserialized is not null)
                {
                    storedStats = deserialized;
                }
            }
            else
            {
                File.WriteAllText(filePath, "{}");
            }

            return storedStats;
        }


    }
}
