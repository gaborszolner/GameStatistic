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

        private static string _statisticFilePath = string.Empty;

        private static Dictionary<string, StatisticEntry> _statisticEntries = new();
        private static bool _isWarmup = false;
        public override void Load(bool hotReload)
        {
            Logger?.LogInformation($"Plugin: {ModuleName} - Version: {ModuleVersion} by {ModuleAuthor}");
            Logger?.LogInformation(ModuleDescription);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            _statisticFilePath = Path.Combine(ModuleDirectory, "statistic.json");
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

            if (victim is null || attacker is null || victim.AuthorizedSteamID is null || attacker.AuthorizedSteamID is null || attacker.IsBot || victim.IsBot)
            {
                return HookResult.Continue;
            }

            if (victim.AuthorizedSteamID != null && attacker != victim)
            {
                var steamId = victim.AuthorizedSteamID.SteamId2;
                if (!_statisticEntries.ContainsKey(steamId))
                {
                    _statisticEntries[steamId] = new StatisticEntry(steamId, victim.PlayerName, 0, 0, 0);
                }

                _statisticEntries[steamId].Dead++;
            }

            if (attacker?.AuthorizedSteamID != null && attacker != victim)
            {
                var steamId = attacker.AuthorizedSteamID.SteamId2;
                if (!_statisticEntries.ContainsKey(steamId))
                {
                    _statisticEntries[steamId] = new StatisticEntry(steamId, attacker.PlayerName, 0, 0, 0);
                }
                _statisticEntries[steamId].Kill++;
            }

            if (attacker?.AuthorizedSteamID != null && attacker == victim)
            {
                var steamId = attacker.AuthorizedSteamID.SteamId2;
                if (!_statisticEntries.ContainsKey(steamId))
                {
                    _statisticEntries[steamId] = new StatisticEntry(steamId, attacker.PlayerName, 0, 0, 0);
                }
                _statisticEntries[steamId].SelfKill++;
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            Dictionary<string, StatisticEntry> storedStats = new();

            if (File.Exists(_statisticFilePath))
            {
                string json = File.ReadAllText(_statisticFilePath);
                storedStats = JsonSerializer.Deserialize<Dictionary<string, StatisticEntry>>(json) ?? [];
            }
            else
            {
                File.Create(_statisticFilePath);
            }

            foreach (var kvp in _statisticEntries)
            {
                if (!storedStats.TryGetValue(kvp.Key, out var existing))
                {
                    storedStats[kvp.Key] = kvp.Value;
                }
                else
                {
                    existing.Kill += kvp.Value.Kill;
                    existing.Dead += kvp.Value.Dead;
                    existing.SelfKill += kvp.Value.SelfKill;
                }
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_statisticFilePath, JsonSerializer.Serialize(storedStats, options));
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Failed to save stats file: {ex.Message}");
            }

            _statisticEntries.Clear();

            return HookResult.Continue;
        }
    }
}
