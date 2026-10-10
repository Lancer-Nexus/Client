# Local DATA to NAP migration

This is a developer-only conversion path. It packages files already present in a local installation and does not grant distribution rights.

```bash
dotnet run --project src/Nexus.Packaging.Cli/Nexus.Packaging.Cli.csproj -- pack /path/to/Freelancer/DATA /path/to/output/local-data.nap
dotnet run --project src/Nexus.Packaging.Cli/Nexus.Packaging.Cli.csproj -- inspect /path/to/output/local-data.nap
dotnet run --project src/Nexus.Packaging.Cli/Nexus.Packaging.Cli.csproj -- verify /path/to/output/local-data.nap
```

V1 currently provides an archive reader and CLI verification only. The game does not mount the package; keep the original installation configured until the later VFS and updater phases are implemented.
