// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.IO;
using LibreLancer;

namespace lancer;

internal static class MainClass
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Console.WriteLine("Usage: lancer [configuration-path]");
#if DEBUG
            Console.WriteLine("       lancer [configuration-path] --email=... --password=... [--character=...]");
            Console.WriteLine("       lancer [configuration-path] --credentials-file=...");
#endif
            return;
        }
#if DEBUG
        if (!TryParseArguments(args, out var configPath, out var email, out var password,
                out var character, out var error))
        {
            Console.Error.WriteLine(error);
            Environment.ExitCode = 2;
            return;
        }
#else
        foreach (var arg in args)
        {
            if (arg.StartsWith("--email", StringComparison.Ordinal) ||
                arg.StartsWith("--password", StringComparison.Ordinal) ||
                arg.StartsWith("--credentials-file", StringComparison.Ordinal) ||
                arg.StartsWith("--character", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Automated login options are available in Debug builds only.");
                Environment.ExitCode = 2;
                return;
            }
            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unknown option: {arg}. Use --help for usage.");
                Environment.ExitCode = 2;
                return;
            }
        }
        var configPath = args.Length > 0 ? args[0] : null;
#endif
        FreelancerGame? game = null;
        AppHandler.Run(() =>
        {
            Func<string>? filePath = configPath is null ? null : () => configPath;
            var cfg = GameConfig.Create(true, filePath);
#if DEBUG
            if (email is not null && string.IsNullOrWhiteSpace(cfg.ClusterGatewayUrl))
            {
                Console.Error.WriteLine("Automated login requires cluster_gateway_url in the client configuration.");
                return;
            }
#endif
            game = new FreelancerGame(cfg);
#if DEBUG
            game.DebugLoginEmail = email;
            game.DebugLoginPassword = password;
            game.DebugCharacterName = character;
#endif
            game.Run();
        }, () => game?.Crashed());
    }

#if DEBUG
    private static bool TryParseArguments(string[] args, out string? configPath, out string? email,
        out string? password, out string? character, out string? error)
    {
        configPath = email = password = character = error = null;
        string? credentialsFile = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--credentials-file=", StringComparison.Ordinal))
            {
                if (credentialsFile is not null) { error = "--credentials-file may be supplied only once."; return false; }
                credentialsFile = arg["--credentials-file=".Length..];
            }
            else if (arg.StartsWith("--email=", StringComparison.Ordinal))
            {
                if (email is not null) { error = "--email may be supplied only once."; return false; }
                email = arg[8..];
            }
            else if (arg.StartsWith("--password=", StringComparison.Ordinal))
            {
                if (password is not null) { error = "--password may be supplied only once."; return false; }
                password = arg[11..];
            }
            else if (arg.StartsWith("--character=", StringComparison.Ordinal))
            {
                if (character is not null) { error = "--character may be supplied only once."; return false; }
                character = arg[12..];
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = "Unknown option. Use --credentials-file=... or --email=... --password=..., and optionally --character=... .";
                return false;
            }
            else if (configPath is null)
            {
                configPath = arg;
            }
            else
            {
                error = "Only one client configuration path may be supplied.";
                return false;
            }
        }

        if (credentialsFile is not null)
        {
            if (email is not null || password is not null)
            {
                error = "Use either --credentials-file or --email/--password, not both.";
                return false;
            }
            try
            {
                if (string.IsNullOrWhiteSpace(credentialsFile))
                {
                    error = "Credentials file is missing or has an invalid size.";
                    return false;
                }
                var info = new FileInfo(credentialsFile);
                if (!info.Exists || info.Length is 0 or > 16_384)
                {
                    error = "Credentials file is missing or has an invalid size.";
                    return false;
                }
                foreach (var line in File.ReadLines(credentialsFile))
                {
                    var separator = line.IndexOf(": ", StringComparison.Ordinal);
                    if (separator < 0) continue;
                    var key = line[..separator];
                    var value = line[(separator + 2)..];
                    if (key == "Email" && email is null) email = value;
                    else if (key == "Password" && password is null) password = value;
                    else if (key is "Email" or "Password")
                    {
                        error = "Credentials file contains duplicate fields.";
                        return false;
                    }
                }
            }
            catch (IOException)
            {
                error = "Credentials file could not be read.";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                error = "Credentials file could not be read.";
                return false;
            }
            catch (ArgumentException)
            {
                error = "Credentials file could not be read.";
                return false;
            }
        }

        if ((email is null) != (password is null) ||
            (email is not null && string.IsNullOrWhiteSpace(email)) ||
            (password is not null && string.IsNullOrWhiteSpace(password)))
        {
            error = "--email and --password must both be supplied with non-empty values.";
            return false;
        }
        if ((character is not null && string.IsNullOrWhiteSpace(character)) ||
            (character is not null && email is null))
        {
            error = "--character requires a non-empty name and both login options.";
            return false;
        }
        return true;
    }
#endif
}
