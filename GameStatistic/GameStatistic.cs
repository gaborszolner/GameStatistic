using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
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
        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventMapShutdown>(OnMapShutdown);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            _playerStatFilePath = Path.Combine(ModuleDirectory, "playerStatistic.json");
            _mapStatFilePath = Path.Combine(ModuleDirectory, "mapStatistic.json");
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            string? mapName = Server.MapName?.Trim();
            if (string.IsNullOrWhiteSpace(mapName))
            {
                return HookResult.Continue;
            }

            var storedStats = CreateMapStatistic(_mapStatFilePath);
            _mapStatEntry = new KeyValuePair<string, MapStatEntry>(mapName, new MapStatEntry(mapName));
            _mapStatEntry.Value.StartedRound++;
            WriteToFile(storedStats, _mapStatEntry);
            _mapStatEntry.Value.StartedRound = 0;
            return HookResult.Continue;
        }

        private HookResult OnMapShutdown(EventMapShutdown @event, GameEventInfo info)
        {
            _mapStatEntry.Value.Rtv++;
            var storedStats = CreateMapStatistic(_mapStatFilePath);
            WriteToFile(storedStats, _mapStatEntry);

            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            if(_isWarmup)
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
            CreatePlayerStatistic();
            CreateMapStatisticRoundEnd(@event.Winner);
            return HookResult.Continue;
        }

        private static void CreateMapStatisticRoundEnd(int winnerTeam)
        {
            var storedStats = CreateMapStatistic(_mapStatFilePath);

            if (winnerTeam == 2)
            {
                _mapStatEntry.Value.TtWin++;
            }
            else if (winnerTeam == 3)
            {
                _mapStatEntry.Value.CTWin++;
            }

            WriteToFile(storedStats, _mapStatEntry);
        }

        private static void WriteToFile(Dictionary<string, MapStatEntry> storedStats, KeyValuePair<string, MapStatEntry> mapStatEntry)
        {
            if (string.IsNullOrWhiteSpace(mapStatEntry.Key))
            {
                return;
            }

            if (storedStats.ContainsKey(mapStatEntry.Key))
            {
                var existing = storedStats[mapStatEntry.Key];
                existing.TtWin += mapStatEntry.Value.TtWin;
                existing.CTWin += mapStatEntry.Value.CTWin;
                existing.PlayedRound += mapStatEntry.Value.PlayedRound;
                existing.Rtv += mapStatEntry.Value.Rtv;
            }
            else 
            {
                storedStats[mapStatEntry.Key] = mapStatEntry.Value;
            }

                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(_mapStatFilePath, JsonSerializer.Serialize(storedStats, options));
                }
                catch (Exception)
                { }
        }

        private void CreatePlayerStatistic()
        {
            var storedStats = CreatePlayerStatistic(_playerStatFilePath);

            foreach (var kvp in _playerStatEntries)
            {
                if (!storedStats.TryGetValue(kvp.Key, out var existing))
                {
                    storedStats[kvp.Key] = kvp.Value;
                }
                else
                {
                    existing.Kill += kvp.Value.Kill;
                    existing.Dead += kvp.Value.Dead;
                    existing.Assister += kvp.Value.Assister;
                    existing.SelfKill += kvp.Value.SelfKill;
                    existing.TeamKill += kvp.Value.TeamKill;
                }
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_playerStatFilePath, JsonSerializer.Serialize(storedStats, options));
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Failed to save stats file: {ex.Message}");
            }

            _playerStatEntries.Clear();
        }

        private static Dictionary<string, MapStatEntry> CreateMapStatistic(string filePath)
        {
            Dictionary<string, MapStatEntry> storedStats = new();

            if (File.Exists(_mapStatFilePath))
            {
                string json = File.ReadAllText(_mapStatFilePath);
                storedStats = JsonSerializer.Deserialize<Dictionary<string, MapStatEntry>>(json) ?? [];
            }
            else
            {
                File.WriteAllText(_mapStatFilePath, "{}");
            }

            return storedStats;
        }

        private static Dictionary<string, PlayerStatEntry> CreatePlayerStatistic(string filePath)
        {
            Dictionary<string, PlayerStatEntry> storedStats = new();

            if (File.Exists(_playerStatFilePath))
            {
                string json = File.ReadAllText(_playerStatFilePath);
                storedStats = JsonSerializer.Deserialize<Dictionary<string, PlayerStatEntry>>(json) ?? [];
            }
            else
            {
                File.WriteAllText(_playerStatFilePath, "{}");
            }

            return storedStats;
        }

    }
}
