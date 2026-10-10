// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LibreLancer;
using LibreLancer.ContentEdit;
using LibreLancer.Dialogs;
using WattleScript.Interpreter;

namespace LancerEdit
{
	class Program
    {
        private const string PipeGuid = "62e369d8adb04159908c125e22e12b94";

        [STAThread]
        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        static void Main(string[] args)
        {
            if (!TryParseArguments(args, out var startupDataPath, out var openFiles, out var showHelp))
            {
                Environment.ExitCode = 2;
                return;
            }
            if (showHelp)
            {
                Console.WriteLine("Usage: LancerEdit [--data <Freelancer directory>] [file ...]");
                Console.WriteLine("  --data <path>  Load this Freelancer installation on startup.");
                return;
            }
            if (startupDataPath != null || !OpenOnOther(openFiles))
            {
                MainWindow mw = null;
                Task pipeServer = null;
                CancellationTokenSource cts = new CancellationTokenSource();
                AppHandler.Run(() =>
                {
                    var editorConfig = EditorConfiguration.Load(true);
                    mw = new MainWindow(editorConfig, startupDataPath: startupDataPath)
                    {
                        InitOpenFile = openFiles
                    };
                    pipeServer = Task.Run(async () => await PipeServer(cts.Token, x =>
                    {
                        mw.QueueUIThread(() =>
                        {
                            mw.OpenFile(x);
                            mw.BringToFront();
                        });
                    }));
                    mw.Run();
                    mw.Config.Save();
                    cts.Cancel();
                    pipeServer.Wait();
                }, () =>
                {
                    cts.Cancel();
                    if (pipeServer != null)
                        pipeServer.Wait();
                    mw.Crashed();
                });
            }
        }

        static bool TryParseArguments(string[] args, out string startupDataPath, out string[] openFiles,
            out bool showHelp)
        {
            startupDataPath = null;
            showHelp = false;
            var files = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--help" || arg == "-h")
                {
                    showHelp = true;
                    continue;
                }
                if (arg == "--data")
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine("Missing path after --data.");
                        openFiles = System.Array.Empty<string>();
                        return false;
                    }
                    if (startupDataPath != null)
                    {
                        Console.Error.WriteLine("Specify --data only once.");
                        openFiles = System.Array.Empty<string>();
                        return false;
                    }
                    startupDataPath = args[++i];
                    continue;
                }
                if (arg.StartsWith("--data=", StringComparison.Ordinal))
                {
                    if (startupDataPath != null || arg.Length == "--data=".Length)
                    {
                        Console.Error.WriteLine("Specify --data once with a non-empty path.");
                        openFiles = System.Array.Empty<string>();
                        return false;
                    }
                    startupDataPath = arg["--data=".Length..];
                    continue;
                }
                files.Add(arg);
            }
            openFiles = files.ToArray();
            return true;
        }

        static async Task PipeServer(CancellationToken token, Action<string> openFile)
        {
            var myPid = Process.GetCurrentProcess().Id;
            // Create pipe and start the async connection wait
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var pipeServer = new NamedPipeServerStream(
                        $"{PipeGuid}-{myPid}", PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.CurrentUserOnly | PipeOptions.WriteThrough | PipeOptions.Asynchronous);
                    await pipeServer.WaitForConnectionAsync(token);
                    using (var reader = new BinaryReader(pipeServer))
                    {
                        var count = reader.ReadInt32();
                        for (int i = 0; i < count; i++) {
                            openFile(reader.ReadString());
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        static bool OpenOnOther(string[] args)
        {
            if (args.Length < 1)
            {
                return false;
            }
            var myProcess = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(myProcess.ProcessName))
            {
                if (p.Id == myProcess.Id)
                    continue;
                try
                {
                    using var client = new NamedPipeClientStream(".", $"{PipeGuid}-{p.Id}", PipeDirection.Out);
                    client.Connect(TimeSpan.FromSeconds(1));
                    using var writer = new BinaryWriter(client);
                    writer.Write(args.Length);
                    foreach (var a in args)
                        writer.Write(a);
                    Console.WriteLine($"Opened in process pid={p.Id}");
                    return true;
                }
                catch
                {
                    // ignored
                }
            }
            return false;
        }
	}
}
