# LancerEdit Documentation

Welcome to the documentation for LancerEdit $(VERSION).

## Load a Freelancer installation on startup

Pass `--data` with the installation directory to load that data directly:

```sh
LancerEdit --data "/path/to/Freelancer"
```

The equivalent `--data="/path/to/Freelancer"` form is supported. The command-line
path overrides the saved auto-load directory for this run and does not change it.
Files can follow the option to open them after launch.

### Models
- [Model Importer](model-importer.md)
- [Model Tab](modeltab.md)
- [Model Exporter](modeltab.md#exporting-models)
- [Hardpoint Editor](modeltab.md#editing-hardpoints)

### Tools
- [FRC Compiler](frc.md)
- [Generating .3db icons](genicons.md)
- [Renderer-Roadmap](renderer-roadmap.md)

### Scripting

- [Librelancer Scripts](scripts.md)
- [Script API Reference](api/reference.md)

### Troubleshooting

- [Locating Log Files](logfiles.md)
