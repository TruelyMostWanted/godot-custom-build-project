# godot-custom-build-project

Test project for the custom Godot engine [truelymostwanted-godot-engine](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine).
Every engine feature gets its own folder here and is tested in **GDScript and C#** with a **Mono build** of the engine.

| Meta | Value |
|---|---|
| **Engine** | custom build of `truely-godot-engine` (Godot 4.8 beta, .NET / Mono) |
| **C# SDK** | `Godot.NET.Sdk/4.8.0-beta`, built locally from the engine and served from a local NuGet folder |
| **.NET** | `net11.0` |
| **Feature docs** | [Wiki of the engine](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki) |
| **Workflow** | [`truely/FEATURE-WORKFLOW.md`](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/blob/truely-godot-engine/truely/FEATURE-WORKFLOW.md) in the engine repository |

## Layout

```
godot-custom-build-project/
├── .github/                        issue templates (same as the engine)
├── AGENTS.md / CLAUDE.md           how to work on this repository (people and AI agents)
├── documentation/                  notes for people
├── external-projects/              source files of other tools
├── godot-exports/                  export target (builds are not versioned)
└── godot-project/                  res://
    ├── project.godot
    ├── GodotCustomBuildProject.csproj
    ├── nuget.config                local NuGet folder with the custom C# SDK
    ├── checks/                     shared check base (GDScript + C#) and the scripts that run them
    ├── _feature-template/          copied for every new feature by checks/new-feature.ps1
    └── feature-<name>/             one folder per engine feature, named like its feature branch
        ├── scripts/gd/             checks in GDScript
        ├── scripts/cs/             checks in C#
        ├── scenes/                 check_gd.tscn, check_cs.tscn and other test scenes
        ├── resources/              test data (.tres, .json, .ini, .csv, …)
        └── packed-scenes/          reusable scenes of the feature
```

## Getting started

1. Build the engine with Mono, generate the glue and push the C# packages to the local NuGet folder
   (see `truely/FEATURE-WORKFLOW.md`, section 3). The local folder is `../nuget-local` next to this repository
   (`P:/Projects/GitHub/nuget-local`).
2. `dotnet build` in `godot-project/`.
3. Run all checks:
   ```powershell
   ./godot-project/checks/run-checks.ps1 -Godot P:/Projects/GitHub/truelymostwanted-godot-engine/bin/godot.windows.editor.x86_64.mono.exe
   ```
   Every check scene exits with code 0 (passed) or 1 (failed).

## A new feature

```powershell
./godot-project/checks/new-feature.ps1 -Name feature-resource-paths-server
```

This copies `_feature-template/` to `feature-resource-paths-server/`, renames the C# check class and namespace, and gives
two passing example checks (`check_gd.tscn`, `check_cs.tscn`) to start from.
