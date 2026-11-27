# godot-custom-build-project

Test project for the custom Godot engine [truelymostwanted-godot-engine](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine).
Every wiki guide of the engine has a folder here with two parts, each in **GDScript and C#**, run with a **Mono build** of the engine:

- **Example**: the hands-on snippets of the guide, in its order. Start here to see how a feature is used.
- **Test**: the full API of the feature (every method, property, signal and enum), as unit tests.

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
    ├── checks/                     FeatureTest base (GDScript + C#) and the scripts that run tests and examples
    ├── _feature-template/          copied for every new guide folder by checks/new-feature.ps1
    └── <guide>/                    one folder per wiki guide, e.g. seeds/ for the guide "Seeds"
        ├── scripts/gd/             Example.gd (hands-on), Test.gd (full API)
        ├── scripts/cs/             Example.cs, Test.cs (the same in C#)
        ├── scenes/                 example_gd.tscn, example_cs.tscn, test_gd.tscn, test_cs.tscn
        └── resources/              sample resources for the inspector and test data
```

## Getting started

1. Build the engine with Mono, generate the glue and push the C# packages to the local NuGet folder
   (see `truely/FEATURE-WORKFLOW.md`, section 3). The local folder is `../nuget-local` next to this repository
   (`P:/Projects/GitHub/nuget-local`).
   For a release instead of a local build, `checks/use-engine.ps1 -Godot <editor exe>` copies the packages of the
   release (`GodotSharp/Tools/nupkgs`) to the local folder and sets the SDK version in the csproj (e.g. `4.8.0-beta.5`).
2. `dotnet build` in `godot-project/`.
3. Run all tests and examples (`-Feature seeds` for one folder, `-Only test` or `-Only example`):
   ```powershell
   ./godot-project/checks/run-checks.ps1 -Godot P:/Projects/GitHub/truelymostwanted-godot-engine/bin/godot.windows.editor.x86_64.mono.exe
   ```
   Every scene exits with code 0 (passed) or 1 (failed); an example also fails with `ERROR` in its output. `run-checks.ps1` calls `use-engine.ps1` first
   (not with `-NoBuild`), so the project always builds against the packages of the engine it tests.

## A new feature

```powershell
./godot-project/checks/new-feature.ps1 -Name path-prefixes
```

The name is the wiki guide in kebab case (`Path-Prefixes` → `path-prefixes`). This copies `_feature-template/` and sets the
C# namespace (`TruelyInteractive.PathPrefixes`). A change to an existing feature goes into its existing guide folder.

Code style of examples and tests: less code is more, every line has a reason, keep it short and simple, and
**one statement per line**, so a reader can follow what happens line by line. Reference: `seeds/`.
