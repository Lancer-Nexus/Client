using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LibreLancer.Server;

/// <summary>Host-local emergency operators for isolated, non-Gateway LLServer instances.</summary>
public sealed class LocalOperatorStore
{
    private readonly object sync = new();
    private readonly string path;
    private readonly HashSet<Guid> accounts;

    private LocalOperatorStore(string path, IEnumerable<Guid> accounts)
    {
        this.path = path;
        this.accounts = accounts.Where(x => x != Guid.Empty).ToHashSet();
    }

    public static LocalOperatorStore Load(string path)
    {
        var accounts = File.Exists(path)
            ? JsonSerializer.Deserialize<Guid[]>(File.ReadAllText(path)) ?? []
            : [];
        return new LocalOperatorStore(path, accounts);
    }

    public bool Contains(Guid accountId)
    {
        lock (sync) return accounts.Contains(accountId);
    }

    public void Set(Guid accountId, bool enabled)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account UUID must not be empty.", nameof(accountId));
        lock (sync)
        {
            if (enabled) accounts.Add(accountId);
            else accounts.Remove(accountId);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(accounts.Order().ToArray()));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, true);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
