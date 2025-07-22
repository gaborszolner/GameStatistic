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
        private static string _mapName = string.Empty;
        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundAnnounceFinal>(OnRoundAnnounceFinal);
            RegisterEventHandler<EventRoundAnnounceMatchStart>(OnRoundAnnounceMatchStart);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventStartHalftime>(OnStartHalftime);
            _playerStatFilePath = Path.Combine(ModuleDirectory, "playerStatistic.json");
            _mapStatFilePath = Path.Combine(ModuleDirectory, "mapStatistic.json");
        }

        private HookResult OnStartHalftime(EventStartHalftime @event, GameEventInfo info)
        {
            PrintMapStat();
            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceMatchStart(EventRoundAnnounceMatchStart @event, GameEventInfo info)
        {
            _mapName = Server.MapName.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_mapName))
            {
                return HookResult.Continue;
            }

            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);
            _mapStatEntry = new KeyValuePair<string, MapStatEntry>(_mapName, new MapStatEntry(_mapName));
            _mapStatEntry.Value.MapStarted++;
            MergeStatToStored(storedStats, _mapStatEntry);
            _mapStatEntry.Value.MapStarted = 0;
            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceFinal(EventRoundAnnounceFinal @event, GameEventInfo info)
        {
            _mapStatEntry.Value.MapFullPlayed++;
            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);
            MergeStatToStored(storedStats, _mapStatEntry);
            _mapStatEntry.Value.MapFullPlayed = 0;
            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _isRoundEnded = false;
            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            PrintMapStat();

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

            var attackerSteamId = attacker.AuthorizedSteamID.SteamId2;
            var victimSteamId = victim.AuthorizedSteamID.SteamId2;
            var assisterSteamId = assister?.AuthorizedSteamID?.SteamId2;

            if (assister is not null && assister.AuthorizedSteamID is not null && assisterSteamId is not null)
            {
                if (!_playerStatEntries.ContainsKey(assisterSteamId))
                {
                    _playerStatEntries[assisterSteamId] = new PlayerStatEntry(assisterSteamId, assister.PlayerName);
                }
                _playerStatEntries[assisterSteamId].Assister++;
            }

            if (attackerSteamId is not null && victimSteamId is not null)
            {
                if (attackerSteamId != victimSteamId)
                {

                    if (!_playerStatEntries.ContainsKey(victimSteamId))
                    {
                        _playerStatEntries[victimSteamId] = new PlayerStatEntry(victimSteamId, victim.PlayerName);
                    }
                    _playerStatEntries[victimSteamId].Dead++;


                    if (!_playerStatEntries.ContainsKey(attackerSteamId))
                    {
                        _playerStatEntries[attackerSteamId] = new PlayerStatEntry(attackerSteamId, attacker.PlayerName);
                    }
                    if (attacker.Team == victim.Team)
                    {
                        _playerStatEntries[attackerSteamId].TeamKill++;
                    }
                    else
                    {
                        _playerStatEntries[attackerSteamId].Kill++;
                    }
                }
                else 
                {
                    if (!_playerStatEntries.ContainsKey(victimSteamId))
                    {
                        _playerStatEntries[victimSteamId] = new PlayerStatEntry(victimSteamId, victim.PlayerName);
                    }
                    _playerStatEntries[victimSteamId].SelfKill++;
                }
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

        private void PrintMapStat()
        {
            _mapName = Server.MapName.Trim() ?? string.Empty;
            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);
            if (storedStats is not null && storedStats.ContainsKey(_mapName))
            {
                int ctWin = storedStats[_mapName].CTWin;
                int tWin = storedStats[_mapName].TWin;
                int fullRoundCount = ctWin + tWin;

                double tWinPercentage = (double)tWin / fullRoundCount * 100;
                double ctWinPercentage = (double)ctWin / fullRoundCount * 100;

                int mapStarted = storedStats[_mapName].MapStarted;
                int mapFullPlayed = storedStats[_mapName].MapFullPlayed;
                double rtvPercentage = 100 - ((double)mapFullPlayed / mapStarted * 100);

                Server.PrintToChatAll($"{ChatColors.Yellow}{PluginPrefix} {ChatColors.Default}- On this map {ChatColors.Red} T win: {tWinPercentage:F2}%, {ChatColors.Blue}CT win: {ctWinPercentage:F2}%, {ChatColors.Green}RTV in {rtvPercentage:F2}%");
            }
        }

        private static void CreateMapStatisticRoundEnd(int winnerTeam)
        {
            var storedStats = ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath);
            _mapStatEntry = new KeyValuePair<string, MapStatEntry>(_mapName, new MapStatEntry(_mapName));
            if (winnerTeam == 2)
            {
                _mapStatEntry.Value.TWin++;
            }
            else if (winnerTeam == 3)
            {
                _mapStatEntry.Value.CTWin++;
            }

            MergeStatToStored(storedStats, _mapStatEntry);
            
        }

        private static void MergeStatToStored(Dictionary<string, MapStatEntry>? storedStats, KeyValuePair<string, MapStatEntry> mapStatEntry)
        {
            if (string.IsNullOrWhiteSpace(mapStatEntry.Key))
            {
                return;
            }

            storedStats ??= [];

            if (storedStats.ContainsKey(mapStatEntry.Key))
            {
                var existing = storedStats[mapStatEntry.Key];
                existing.TWin += mapStatEntry.Value.TWin;
                existing.CTWin += mapStatEntry.Value.CTWin;
                existing.MapStarted += mapStatEntry.Value.MapStarted;
                existing.MapFullPlayed += mapStatEntry.Value.MapFullPlayed;
            }
            else
            {
                storedStats[mapStatEntry.Key] = mapStatEntry.Value;
            }

            FileWriteAll<Dictionary<string, MapStatEntry>>(_mapStatFilePath, storedStats);
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
