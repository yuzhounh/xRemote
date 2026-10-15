using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using _1RM.Model.Protocol;
using _1RM.Service;
using Newtonsoft.Json;

namespace _1RM.Utils;

public static class LocalRdpPasswordStore
{
    private static readonly object Sync = new();
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("1Remote.local-rdp-password.v1");
    private static string FilePath => Path.Combine(AppPathHelper.Instance.LocalityDirPath, ".rdp-passwords.json");

    private sealed class Entry
    {
        public string Address { get; set; } = "";
        public string Port { get; set; } = "";
        public string UserName { get; set; } = "";
        public string ProtectedPassword { get; set; } = "";
    }

    private static Dictionary<string, Entry> Read()
    {
        return File.Exists(FilePath)
            ? JsonConvert.DeserializeObject<Dictionary<string, Entry>>(File.ReadAllText(FilePath)) ?? new()
            : new();
    }

    // Every RDP profile uses the application prompt until a matching local credential is saved.
    public static void PrepareForConnect(RDP server)
    {
        if (server.AskPasswordWhenConnect is not true && TryGet(server, out var password))
            server.Password = password;
        else
            server.AskPasswordWhenConnect = true;
    }

    public static bool TryGet(RDP server, out string password)
    {
        password = "";
        if (string.IsNullOrEmpty(server.Id)) return false;
        lock (Sync)
        {
            try
            {
                if (!Read().TryGetValue(server.Id, out var entry) || entry == null
                    || string.IsNullOrEmpty(entry.ProtectedPassword)
                    || !string.Equals(entry.Address, server.Address, StringComparison.OrdinalIgnoreCase)
                    || entry.Port != server.Port
                    || !string.Equals(entry.UserName, server.UserName, StringComparison.OrdinalIgnoreCase)) return false;
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(entry.ProtectedPassword), Entropy, DataProtectionScope.CurrentUser);
                try { password = Encoding.UTF8.GetString(bytes); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
                return password.Length > 0;
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or IOException or UnauthorizedAccessException or JsonException)
            {
                return false;
            }
        }
    }

    public static void Remember(RDP server, string password)
    {
        if (string.IsNullOrEmpty(server.Id) || string.IsNullOrEmpty(password)) return;
        lock (Sync)
        {
            var entries = Read();
            var bytes = Encoding.UTF8.GetBytes(password);
            byte[] encrypted;
            try { encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            entries[server.Id] = new Entry
            {
                Address = server.Address, Port = server.Port, UserName = server.UserName,
                ProtectedPassword = Convert.ToBase64String(encrypted)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(entries, Formatting.Indented));
            File.Move(temporary, FilePath, true);
        }
    }
}
