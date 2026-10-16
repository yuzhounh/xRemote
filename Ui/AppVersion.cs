using static Shawn.Utils.VersionHelper;

namespace _1RM
{
    public static class AppVersion
    {
        public const uint Major = 1;
        public const uint Minor = 3;
        public const uint Patch = 0;
        public const uint Build = 0;
        // Increment only for an xRemote release; keep increasing across upstream upgrades.
        public const uint ForkRevision = 12;
        public const string BuildDate = "";
        public const string PreRelease = ""; // e.g. "alpha" "beta.2"

        public static readonly Version VersionData = new Version(Major, Minor, Patch, Build, PreRelease);
        public static string Version => VersionData.ToString();
        public static string DisplayVersion => $"{Version} Rev.{ForkRevision}";
        public static string ProductDisplayName => $"{Assert.APP_DISPLAY_NAME} {DisplayVersion}";


        public const string ReleaseRepository = "yuzhounh/xRemote";
        public static string ReleaseApiUrl => $"https://api.github.com/repos/{ReleaseRepository}/releases/latest";
        public static string ReleasesUrl => $"https://github.com/{ReleaseRepository}/releases";
        public static Version UpdateVersionData => new Version(Major, Minor, Patch, ForkRevision, PreRelease);
    }
}
