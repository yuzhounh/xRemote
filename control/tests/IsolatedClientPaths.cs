namespace _1RM.Service;

internal static class UiText
{
    public static string Get(string key, params object[] values) => key;
}

// The real HTTP/pairing client is linked into this harness; user configuration is never opened.
public sealed class AppPathHelper
{
    public static AppPathHelper Instance { get; set; } = new();
    public string LocalityDirPath { get; set; } = "";
}
