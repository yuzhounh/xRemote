using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Shawn.Utils;

namespace _1RM.Service;

public static class XRemoteUpdateService
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("xRemote/" + AppVersion.Version);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<VersionHelper.CheckUpdateResult> CheckAsync()
    {
        using var response = await Client.GetAsync(AppVersion.ReleaseApiUrl).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            SimpleLogHelper.Warning("xRemote release source is unavailable or has no published release.");
            return VersionHelper.CheckUpdateResult.False();
        }
        response.EnsureSuccessStatusCode();
        return ParseRelease(await response.Content.ReadAsStringAsync().ConfigureAwait(false), AppVersion.UpdateVersionData);
    }

    public static VersionHelper.CheckUpdateResult ParseRelease(string json, VersionHelper.Version current)
    {
        var release = JObject.Parse(json);
        if (release.Value<bool>("draft") || release.Value<bool>("prerelease"))
            return VersionHelper.CheckUpdateResult.False();
        var tag = release.Value<string>("tag_name") ?? "";
        var match = Regex.Match(tag, @"^v?(\d+)\.(\d+)\.(\d+)-r(\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return VersionHelper.CheckUpdateResult.False();
        var parts = new uint[4];
        for (int i = 0; i < parts.Length; i++)
            if (!uint.TryParse(match.Groups[i + 1].Value, out parts[i])) return VersionHelper.CheckUpdateResult.False();
        var candidate = new VersionHelper.Version(parts[0], parts[1], parts[2], parts[3]);
        if (candidate <= current) return VersionHelper.CheckUpdateResult.False();
        return new VersionHelper.CheckUpdateResult(true,
            $"{parts[0]}.{parts[1]}.{parts[2]} Rev.{parts[3]}",
            AppVersion.ReleasesUrl + "/tag/" + Uri.EscapeDataString(tag), false);
    }
}
