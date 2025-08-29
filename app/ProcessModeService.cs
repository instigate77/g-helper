using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GHelper
{
    // Lightweight background watcher that switches performance modes based on running processes
    // Patterns are taken from AppConfig keys populated via the Process Auto UI in Extra:
    //  - process_map_enabled (bool)
    //  - process_map_silent, process_map_performance, process_map_turbo (newline/comma/semicolon separated)
    // Matching is case-insensitive against Process.ProcessName (no .exe extension). Supports '*' wildcards.
    public static class ProcessModeService
    {
        private static CancellationTokenSource? _cts;
        private static Task? _loopTask;

        // Cached mappings
        private static List<Regex> _silent = new();
        private static List<Regex> _perf = new();
        private static List<Regex> _turbo = new();

        // Track last applied by this service to avoid spamming
        private static int? _lastAppliedModeIndex = null;

        public static void Start()
        {
            try
            {
                ReloadMappings();

                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                _loopTask = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            if (!AppConfig.Is("process_map_enabled"))
                            {
                                _lastAppliedModeIndex = null;
                                await Task.Delay(1500, token);
                                continue;
                            }

                            int? target = GetTargetModeIndex();

                            if (target.HasValue)
                            {
                                if (_lastAppliedModeIndex != target.Value)
                                {
                                    Program.modeControl.SetPerformanceMode(target.Value, true);
                                    _lastAppliedModeIndex = target.Value;
                                }
                            }

                            await Task.Delay(1500, token);
                        }
                        catch (TaskCanceledException) { }
                        catch (Exception ex)
                        {
                            Logger.WriteLine("ProcessModeService error: " + ex.Message);
                            await Task.Delay(3000, token);
                        }
                    }
                }, token);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to start ProcessModeService: " + ex.Message);
            }
        }

        public static void Stop()
        {
            try
            {
                _cts?.Cancel();
                _cts = null;
                _loopTask = null;
                _lastAppliedModeIndex = null;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to stop ProcessModeService: " + ex.Message);
            }
        }

        public static void ReloadMappings()
        {
            try
            {
                _silent = BuildRegexList(AppConfig.GetString("process_map_silent"));
                _perf = BuildRegexList(AppConfig.GetString("process_map_performance"));
                _turbo = BuildRegexList(AppConfig.GetString("process_map_turbo"));
                Logger.WriteLine($"ProcessModeService mappings: silent={_silent.Count} perf={_perf.Count} turbo={_turbo.Count}");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ProcessModeService ReloadMappings error: " + ex.Message);
            }
        }

        private static List<Regex> BuildRegexList(string? patterns)
        {
            var list = new List<Regex>();
            if (string.IsNullOrWhiteSpace(patterns)) return list;

            var tokens = patterns
                .Replace('\r', '\n')
                .Replace(';', '\n')
                .Replace(',', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var t in tokens)
            {
                // Normalize: accept names like "game.exe" or "game"; we match only ProcessName so strip .exe
                var name = t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? t[..^4] : t;
                // Convert simple wildcard * to regex .*
                var pattern = "^" + Regex.Escape(name).Replace("\\*", ".*") + "$";
                try { list.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)); }
                catch { }
            }
            return list;
        }

        // Returns target mode index if any match. Priority: Turbo > Performance > Silent
        // Mode indices follow Program.HandleModeCommand usage: performance=0, turbo=1, silent=2
        private static int? GetTargetModeIndex()
        {
            try
            {
                var procs = Process.GetProcesses();

                // Build a quick set of names to check (lowercase)
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in procs)
                {
                    try { if (!string.IsNullOrEmpty(p.ProcessName)) names.Add(p.ProcessName); }
                    catch { }
                }

                if (MatchesAny(names, _turbo)) return 1;       // Turbo
                if (MatchesAny(names, _perf)) return 0;        // Performance/Balanced
                if (MatchesAny(names, _silent)) return 2;      // Silent
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ProcessModeService scan error: " + ex.Message);
            }
            return null;
        }

        private static bool MatchesAny(HashSet<string> names, List<Regex> patterns)
        {
            if (patterns.Count == 0) return false;
            foreach (var name in names)
            {
                foreach (var rx in patterns)
                {
                    if (rx.IsMatch(name)) return true;
                }
            }
            return false;
        }
    }
}
