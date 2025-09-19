using GHelper.Helpers;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GHelper.AutoUpdate
{
    public class AutoUpdateControl
    {

        SettingsForm settings;
        private readonly string displayVersion;

        // Development/build suffix for display version. Example values: ".d1", ".d2", "" (empty to disable)
        // Bump this when making iterative local changes you want reflected in the UI.
        private const string DevSuffix = ".d6";

    public string versionUrl = "http://github.com/seerge/g-helper/releases";
    static long lastUpdate;

        public AutoUpdateControl(SettingsForm settingsForm)
        {
            settings = settingsForm;
            var asmVersion = Assembly.GetExecutingAssembly()?.GetName()?.Version ?? new Version(0, 0);
            displayVersion = $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}{DevSuffix}";
            // Always show the current version; auto-update is disabled.
            settings.SetVersionLabel(Properties.Strings.VersionLabel + $": {displayVersion}");
        }

        public void CheckForUpdates()
        {
            // Auto-update checks are disabled intentionally. Keep label static.
            settings.SetVersionLabel(Properties.Strings.VersionLabel + $": {displayVersion}");
        }

        public void LoadReleases()
        {
            // Opening releases page is disabled as update downloads are not supported.
        }

        // No network calls: updates fully disabled.
        private static string EscapeString(string input)
        {
            return Regex.Replace(Regex.Replace(input, @"\[|\]", "`$0"), @"\'", "''");
        }

    }
}
