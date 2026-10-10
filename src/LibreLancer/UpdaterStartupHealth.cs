using System;
using System.IO;
using System.Text;

namespace LibreLancer;

internal static class UpdaterStartupHealth
{
    public static void AcknowledgeReady()
    {
        var path = Environment.GetEnvironmentVariable("LANCER_NEXUS_HEALTH_ACK_PATH");
        var token = Environment.GetEnvironmentVariable("LANCER_NEXUS_HEALTH_ACK_TOKEN");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(token) ||
            token.Length != 64 || !IsHex(token))
            return;

        string? temporaryPath = null;
        try
        {
            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path);
            if (directory is null || !Directory.Exists(directory))
                return;
            temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 256, FileOptions.WriteThrough))
            {
                var bytes = Encoding.ASCII.GetBytes(token);
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path);
            temporaryPath = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            FLLog.Error("Updater", $"Could not acknowledge healthy startup: {error.Message}");
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
            if (!Uri.IsHexDigit(character))
                return false;
        return true;
    }
}
