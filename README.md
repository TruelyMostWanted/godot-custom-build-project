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

## Folders

One folder per wiki guide. Before #384 (engine), the folders were named after the engine branches (`<nn>-feature-…`); the last column lists the issues whose old folders were merged into each one.

| Folder | Wiki guide | Engine issues (old folders `<nn>-…`) |
|---|---|---|
| `accessibility-settings/` | [Accessibility-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Accessibility-Settings) | #27 |
| `audio-bus-settings/` | [Audio-Bus-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Bus-Settings) | #16 |
| `audio-commands/` | [Audio-Commands](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Commands) | #75 |
| `audio-device-settings/` | [Audio-Device-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Device-Settings) | #16 |
| `audio-files/` | [Audio-Files](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Files) | #102, #103, #104, #199 |
| `audio-mixer/` | [Audio-Mixer](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Mixer) | #74, #76 |
| `audio-player-fades-and-playlists/` | [Audio-Player-Fades-And-Playlists](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Player-Fades-And-Playlists) | #362 |
| `audio-track-information/` | [Audio-Track-Information](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Audio-Track-Information) | #363 |
| `cached-resource-paths/` | [Cached-Resource-Paths](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Cached-Resource-Paths) | #1, #37 |
| `camera-3d-settings/` | [Camera-3D-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Camera-3D-Settings) | #28 |
| `children-of-a-type/` | [Children-of-a-Type](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Children-of-a-Type) | #239 |
| `csv/` | [CSV](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/CSV) | #73, #268, #277, #279, #281 |
| `data-file-inspector/` | [Data-File-Inspector](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Data-File-Inspector) | #361 |
| `data-schemas/` | [Data-Schemas](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Data-Schemas) | #277, #278 |
| `dialogue-graphs/` | [Dialogue-Graphs](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Dialogue-Graphs) | #259 |
| `engine-settings/` | [Engine-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Engine-Settings) | #26 |
| `file-manifests/` | [File-Manifests](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/File-Manifests) | #7, #8 |
| `flow-graph-debugger/` | [Flow-Graph-Debugger](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graph-Debugger) | #276 |
| `flow-graph-formats/` | [Flow-Graph-Formats](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graph-Formats) | #258 |
| `flow-graph-playback/` | [Flow-Graph-Playback](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graph-Playback) | #256 |
| `flow-graphs/` | [Flow-Graphs](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graphs) | #255 |
| `font-files/` | [Font-Files](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Font-Files) | #96, #365 |
| `graphics-presets/` | [Graphics-Presets](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Graphics-Presets) | #25 |
| `http-requests/` | [HTTP-Requests](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/HTTP-Requests) | #100 |
| `image-files/` | [Image-Files](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Image-Files) | #101 |
| `ini/` | [INI](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/INI) | #73, #268 |
| `input-icons/` | [Input-Icons](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Input-Icons) | #198 |
| `input-map-settings/` | [Input-Map-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Input-Map-Settings) | #18, #172, #191 |
| `input-settings/` | [Input-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Input-Settings) | #19 |
| `json/` | [JSON](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/JSON) | #5, #73 |
| `language-settings/` | [Language-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Language-Settings) | #17 |
| `load-results/` | [Load-Results](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Load-Results) | #70 |
| `loader-index/` | [Loader-Index](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Loader-Index) | #34, #53 |
| `locale-channels/` | [Locale-Channels](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Locale-Channels) | #344, #357 |
| `localization/` | [Localization](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localization) |  |
| `localization-catalogs/` | [Localization-Catalogs](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localization-Catalogs) | #340, #341, #343, #344 |
| `localization-editor/` | [Localization-Editor](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localization-Editor) | #346 |
| `localized-text/` | [Localized-Text](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localized-Text) | #345 |
| `opus-audio/` | [Opus-Audio](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Opus-Audio) | #195, #199 |
| `path-prefixes/` | [Path-Prefixes](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Path-Prefixes) | #3 |
| `pck-packages/` | [PCK-Packages](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/PCK-Packages) | #10, #85, #86 |
| `performance-recorder/` | [Performance-Recorder](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Performance-Recorder) | #78, #79, #80 |
| `process-memory/` | [Process-Memory](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Process-Memory) | #77 |
| `recursive-directory-listing/` | [Recursive-Directory-Listing](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Recursive-Directory-Listing) | #39 |
| `render-effect-settings/` | [Render-Effect-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Render-Effect-Settings) | #24 |
| `render-quality-settings/` | [Render-Quality-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Render-Quality-Settings) | #21 |
| `resource-database/` | [Resource-Database](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Resource-Database) | #9 |
| `resource-format-limits/` | [Resource-Format-Limits](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Resource-Format-Limits) | #2 |
| `resource-paths/` | [Resource-Paths](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Resource-Paths) | #367 |
| `scene-formats/` | [Scene-Formats](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Scene-Formats) | #95 |
| `screenshots/` | [Screenshots](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Screenshots) | #237 |
| `seeds/` | [Seeds](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Seeds) | #11 |
| `settings-servers/` | [Settings-Servers](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Settings-Servers) | #15, #189 |
| `shadow-settings/` | [Shadow-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Shadow-Settings) | #23 |
| `simple-resource-formats/` | [Simple-Resource-Formats](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Simple-Resource-Formats) | #35 |
| `sprite-sheet/` | [Sprite-Sheet](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Sprite-Sheet) | #50, #51 |
| `sqlite/` | [SQLite](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/SQLite) | #252 |
| `streaming-assets/` | [Streaming-Assets](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Streaming-Assets) | #88, #89, #91 |
| `subtitle-player/` | [Subtitle-Player](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Subtitle-Player) | #260, #322 |
| `subtitles/` | [Subtitles](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Subtitles) | #227, #233 |
| `text-insertion/` | [Text-Insertion](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-Insertion) | #342 |
| `text-localization/` | [Text-Localization](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-Localization) | #343 |
| `text-serialization/` | [Text-Serialization](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-Serialization) | #5, #68, #73, #268 |
| `text-to-speech/` | [Text-to-Speech](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-to-Speech) | #12, #76 |
| `texture-settings/` | [Texture-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Texture-Settings) | #22 |
| `texture-streaming-settings/` | [Texture-Streaming-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Texture-Streaming-Settings) | #22 |
| `tile-map-path-graph-debug/` | [Tile-Map-Path-Graph-Debug](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Tile-Map-Path-Graph-Debug) | #283 |
| `tile-map-pathfinding/` | [Tile-Map-Pathfinding](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Tile-Map-Pathfinding) | #282 |
| `tileset-coordinate-axes/` | [TileSet-Coordinate-Axes](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/TileSet-Coordinate-Axes) | #228 |
| `toml/` | [TOML](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/TOML) | #36 |
| `truely-version/` | Releases | #265 |
| `tts-persona-settings/` | [TTS-Persona-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/TTS-Persona-Settings) | #29 |
| `video-files/` | [Video-Files](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Video-Files) | #105, #200 |
| `viewport-registry/` | [Viewport-Registry](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Viewport-Registry) | #251 |
| `webm-video/` | [WebM-Video](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/WebM-Video) | #196, #201 |
| `window-settings/` | [Window-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Window-Settings) | #20 |
| `world-space-progress-bars/` | [World-Space-Progress-Bars](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/World-Space-Progress-Bars) | #285 |
| `xml/` | [XML](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/XML) | #68, #73, #268 |
| `xr-settings/` | [XR-Settings](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/XR-Settings) | #30 |
| `yaml/` | [YAML](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/YAML) | #155 |
