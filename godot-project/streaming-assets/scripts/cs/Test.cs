using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.StreamingAssets;

/// <summary>
/// Full API test of Streaming Assets (StreamingAssetsServer, StreamingAssetsExportPlugin, StreamingAssetsPackBuilder).
/// Same tests as scripts/gd/Test.gd. The plugin and the pack builder are editor classes (TOOLS define).
/// </summary>
public partial class Test : FeatureTest
{
    private const string Source = "res://streaming-assets/resources/streaming_assets";
    private const string ExportSample = "res://streaming-assets/resources/export_sample";
    private const string PackSample = "res://streaming-assets/resources/pack_sample";
    private const string PrintScript = "res://streaming-assets/scripts/gd/print_location.gd";
    private const string Root = "user://streaming_assets_test/";

    private static readonly (string Setting, Variant Default)[] Settings =
    [
        ("filesystem/streaming_assets/enabled", false),
        ("filesystem/streaming_assets/source_folder", "res://streaming_assets"),
        ("filesystem/streaming_assets/mode", 0),
        ("filesystem/streaming_assets/target_folder", "{exe_name}_Data/StreamingAssets"),
        ("filesystem/streaming_assets/pack_file", "{exe_name}.streaming.pck"),
        ("filesystem/streaming_assets/fallback", 0),
    ];

    private static readonly string[] Windows = ["pc", "windows", "x86_64"];
    private static readonly string[] Linux = ["pc", "linux", "linuxbsd", "x86_64"];
    private static readonly string[] MacOs = ["pc", "macos", "arm64"];
    private static readonly string[] Android = ["mobile", "android", "arm64"];
    private static readonly string[] Web = ["web"];

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestEnums();
        TestSettings();
        TestState();
        TestPrefix();
        TestReading();
        TestOsPath();
        TestExpandTemplate();
        TestExternalFolder();
#if TOOLS
        TestTargetFolder();
        TestPathInFolder();
        TestFileExcluded();
        TestCollectFiles();
        TestPlacement();
        TestPackPath();
        TestPackDescription();
        TestPackRefusals();
        // Last: mounting a pack switches the res:// access of this run.
        TestMountPreparedPack();
#endif
        return Task.CompletedTask;
    }

    private void ExpectPaths(string[] actual, string[] expected, string message)
    {
        Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");
    }

    private void TestEnums()
    {
        ExpectEqual((long)StreamingAssetsServer.Mode.Raw, 0L, "Mode.Raw");
        ExpectEqual((long)StreamingAssetsServer.Mode.Pack, 1L, "Mode.Pack");
        ExpectEqual((long)StreamingAssetsServer.Fallback.Embed, 0L, "Fallback.Embed");
        ExpectEqual((long)StreamingAssetsServer.Fallback.None, 1L, "Fallback.None");
        ExpectEqual((long)StreamingAssetsServer.Location.SourceFolder, 0L, "Location.SourceFolder");
        ExpectEqual((long)StreamingAssetsServer.Location.ExternalFolder, 1L, "Location.ExternalFolder");
        ExpectEqual((long)StreamingAssetsServer.Location.Bundle, 2L, "Location.Bundle");
        ExpectEqual((long)StreamingAssetsServer.Location.Embedded, 3L, "Location.Embedded");
        ExpectEqual((long)StreamingAssetsServer.Location.Pack, 4L, "Location.Pack");
        ExpectEqual((long)StreamingAssetsServer.Location.None, 5L, "Location.None");
#if TOOLS
        ExpectEqual((long)StreamingAssetsExportPlugin.Mode.Raw, (long)StreamingAssetsServer.Mode.Raw, "the plugin Mode.Raw matches the server");
        ExpectEqual((long)StreamingAssetsExportPlugin.Mode.Pack, (long)StreamingAssetsServer.Mode.Pack, "the plugin Mode.Pack matches the server");
        ExpectEqual((long)StreamingAssetsExportPlugin.Fallback.Embed, (long)StreamingAssetsServer.Fallback.Embed, "the plugin Fallback.Embed matches the server");
        ExpectEqual((long)StreamingAssetsExportPlugin.Fallback.None, (long)StreamingAssetsServer.Fallback.None, "the plugin Fallback.None matches the server");
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.External, 0L, "Placement.External");
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.Embed, 1L, "Placement.Embed");
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.None, 2L, "Placement.None");
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.Pack, 4L, "Placement.Pack");
        ExpectEqual((string)ClassDB.GetParentClass("StreamingAssetsExportPlugin"), "EditorExportPlugin", "StreamingAssetsExportPlugin is an EditorExportPlugin");
        Expect(!ClassDB.CanInstantiate("StreamingAssetsExportPlugin"), "StreamingAssetsExportPlugin cannot be instantiated");
        Expect(!ClassDB.CanInstantiate("StreamingAssetsPackBuilder"), "StreamingAssetsPackBuilder cannot be instantiated");
#endif
    }

    private void TestSettings()
    {
        foreach (var (setting, defaultValue) in Settings)
        {
            Expect(ProjectSettings.HasSetting(setting), $"{setting} is registered");
            ExpectEqual(ProjectSettings.Singleton.PropertyGetRevert(setting).ToString(), defaultValue.ToString(), $"{setting} default");
        }
        ExpectEqual(GetPropertyHint("filesystem/streaming_assets/mode"), "Raw,Pack", "mode is an enum in the Inspector");
        ExpectEqual(GetPropertyHint("filesystem/streaming_assets/fallback"), "Embed,None", "fallback is an enum in the Inspector");
        Expect((bool)ProjectSettings.GetSetting("filesystem/streaming_assets/enabled"), "project.godot enables streaming assets");
    }

    private void TestState()
    {
        Expect(StreamingAssetsServer.IsEnabled(), "IsEnabled");
        ExpectEqual(StreamingAssetsServer.GetMode(), StreamingAssetsServer.Mode.Raw, "GetMode");
        ExpectEqual(StreamingAssetsServer.GetFallback(), StreamingAssetsServer.Fallback.Embed, "GetFallback");
        ExpectEqual(StreamingAssetsServer.GetLocation(), StreamingAssetsServer.Location.SourceFolder, "a game run from the editor reads the source folder");
        ExpectEqual(StreamingAssetsServer.GetSourceFolder(), Source, "GetSourceFolder");
        ExpectEqual(StreamingAssetsServer.GetRoot(), Source, "GetRoot is the source folder");
    }

    private void TestPrefix()
    {
        Expect(PathPrefixServer.HasPrefix("streaming://"), "streaming:// is registered");
        Expect(PathPrefixServer.IsPrefixLocked("streaming://"), "streaming:// is locked");
        Expect(PathPrefixServer.IsPrefixAvailable("streaming://"), "streaming:// is available");
        Expect(!PathPrefixServer.IsPrefixReadOnly("streaming://"), "the source folder is writable in the editor");
        ExpectEqual(PathPrefixServer.RegisterPrefixFolder("streaming://", "user://elsewhere"), Error.Locked, "streaming:// cannot be replaced");
        ExpectEqual(PathPrefixServer.UnregisterPrefix("streaming://"), Error.Locked, "streaming:// cannot be removed");
        ExpectEqual(PathPrefixServer.Globalize("streaming://readme.txt"), ProjectSettings.GlobalizePath(Source + "/readme.txt"), "Globalize resolves to the source folder");
    }

    private void TestReading()
    {
        using FileAccess file = FileAccess.Open("streaming://config/levels.json", FileAccess.ModeFlags.Read);
        var data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        ExpectEqual(data["levels"].AsGodotArray().Count, 3, "levels.json is read through streaming://");
        Expect(FileAccess.FileExists("streaming://readme.txt"), "FileAccess.FileExists finds streaming://readme.txt");
        Expect(!FileAccess.FileExists("streaming://missing.txt"), "a missing file does not exist");
        Expect(DirAccess.GetFilesAt("streaming://config").Contains("levels.json"), "DirAccess lists streaming://config");
    }

    private void TestOsPath()
    {
        string expected = ProjectSettings.GlobalizePath(Source + "/config/levels.json");
        ExpectEqual(StreamingAssetsServer.GetOsPath("streaming://config/levels.json"), expected, "GetOsPath of a streaming:// path");
        ExpectEqual(StreamingAssetsServer.GetOsPath("config/levels.json"), expected, "GetOsPath of a relative path");
        ExpectEqual(StreamingAssetsServer.GetOsPath("streaming://config/../config/levels.json"), expected, "GetOsPath normalizes ..");
        ExpectEqual(StreamingAssetsServer.GetOsPath("streaming://../project.godot"), "", "GetOsPath refuses paths that leave the folder");
        ExpectEqual(StreamingAssetsServer.GetOsPath("user://a.txt"), "", "GetOsPath refuses other prefixes");
        Expect(FileAccess.FileExists(expected), "the OS path exists");
    }

    private void TestExpandTemplate()
    {
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{exe_name}_Data/StreamingAssets", "MyGame"), "MyGame_Data/StreamingAssets", "{exe_name}");
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{exe_name}/{exe_name}.data", "a"), "a/a.data", "every {exe_name} is replaced");
        // The project name and version have no characters that a file name replaces.
        string appName = ((string)ProjectSettings.GetSetting("application/config/name")).StripEdges();
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{app_name}", "x"), appName, "{app_name} is the project name");
        string version = ((string)ProjectSettings.GetSetting("application/config/version", "")).StripEdges();
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("v{version}", "x"), "v" + version, "{version} is the project version");
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{unknown}/x", "x"), "{unknown}/x", "unknown placeholders stay");
#if TOOLS
        string expanded = StreamingAssetsServer.ExpandTemplate("{app_name}/{exe_name}", "MyGame");
        ExpectEqual(StreamingAssetsExportPlugin.ExpandTemplate("{app_name}/{exe_name}", "MyGame"), expanded, "the export plugin uses the same rules");
#endif
        ExpectEqual(StreamingAssetsServer.GetExeName(), OS.GetExecutablePath().GetFile().GetBaseName(), "GetExeName is the executable name");
    }

    private void TestExternalFolder()
    {
        string folder = ProjectSettings.GlobalizePath(Root + "external");
        DirAccess.MakeDirRecursiveAbsolute(folder);
        using (FileAccess file = FileAccess.Open(folder.PathJoin("hello.txt"), FileAccess.ModeFlags.Write))
        {
            file.StoreString("Hello from outside");
        }
        var output = new Godot.Collections.Array();
        string[] arguments = ["--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--streaming-assets-path=" + folder, "--script", PrintScript];
        int code = OS.Execute(OS.GetExecutablePath(), arguments, output, true);
        string text = string.Join("\n", output);
        ExpectEqual(code, 0, "the second process exits with 0");
        Expect(text.Contains($"STREAMING_LOCATION={(long)StreamingAssetsServer.Location.ExternalFolder}"), "--streaming-assets-path gives Location.ExternalFolder");
        Expect(text.Contains($"STREAMING_ROOT={folder.SimplifyPath()}"), "the root is the given folder");
        Expect(text.Contains("STREAMING_TEXT=Hello from outside"), "streaming:// reads the external folder");
        Expect(text.Contains($"STREAMING_OS_PATH={folder.SimplifyPath().PathJoin("hello.txt")}"), "GetOsPath points into the external folder");
    }

#if TOOLS
    private void TestTargetFolder()
    {
        foreach (string folder in new[] { "MyGame_Data/StreamingAssets", "StreamingAssets", "StreamingAssets/", "a\\b" })
        {
            Expect(StreamingAssetsExportPlugin.IsValidTargetFolder(folder), $"'{folder}' is a valid target folder");
        }
        foreach (string folder in new[] { "", ".", "./a", "..", "a/../b", "a//b", "/abs", "C:/abs", "res://a", "{app_name}/x" })
        {
            Expect(!StreamingAssetsExportPlugin.IsValidTargetFolder(folder), $"'{folder}' is not a valid target folder");
        }
    }

    private void TestPathInFolder()
    {
        const string folder = "res://streaming_assets";
        Expect(StreamingAssetsExportPlugin.IsPathInFolder("res://streaming_assets/a.png", folder), "a file in the folder matches");
        Expect(StreamingAssetsExportPlugin.IsPathInFolder("res://streaming_assets/sub/b.json", folder + "/"), "a file in a subfolder matches (trailing slash)");
        Expect(!StreamingAssetsExportPlugin.IsPathInFolder("res://streaming_assets_other/a.png", folder), "a sibling folder with the same prefix does not match");
        Expect(!StreamingAssetsExportPlugin.IsPathInFolder("res://other/streaming_assets/a.png", folder), "a nested folder with the same name does not match");
        Expect(!StreamingAssetsExportPlugin.IsPathInFolder("res://a.png", "res://"), "a bare prefix never matches");
    }

    private void TestFileExcluded()
    {
        foreach (string entry in new[] { ".gdignore", ".DS_Store", "icon.png.import", "script.gd.uid" })
        {
            Expect(StreamingAssetsExportPlugin.IsFileExcluded(entry), $"'{entry}' is not shipped");
        }
        foreach (string entry in new[] { "icon.png", "level.json", "importer.txt" })
        {
            Expect(!StreamingAssetsExportPlugin.IsFileExcluded(entry), $"'{entry}' is shipped");
        }
    }

    private void TestCollectFiles()
    {
        string[] files = StreamingAssetsExportPlugin.CollectFiles(ExportSample);
        ExpectPaths(files, ["config/levels.json", "readme.txt", "sub/deeper/blob.bin"], "CollectFiles skips dot entries, .import and .uid and sorts");
        ExpectPaths(StreamingAssetsExportPlugin.CollectFiles(ExportSample + "/"), files, "a trailing slash does not change the result");
        ExpectPaths(StreamingAssetsExportPlugin.CollectFiles(ProjectSettings.GlobalizePath(ExportSample)), files, "an absolute OS path gives the same result");
        Expect(StreamingAssetsExportPlugin.CollectFiles(ExportSample + "/missing").Length == 0, "a missing folder gives an empty list");
    }

    private void TestPlacement()
    {
        const StreamingAssetsExportPlugin.Fallback embed = StreamingAssetsExportPlugin.Fallback.Embed;
        const StreamingAssetsExportPlugin.Fallback none = StreamingAssetsExportPlugin.Fallback.None;
        const StreamingAssetsExportPlugin.Mode raw = StreamingAssetsExportPlugin.Mode.Raw;
        const StreamingAssetsExportPlugin.Mode pack = StreamingAssetsExportPlugin.Mode.Pack;
        const StreamingAssetsExportPlugin.Placement external = StreamingAssetsExportPlugin.Placement.External;
        const StreamingAssetsExportPlugin.Placement embedded = StreamingAssetsExportPlugin.Placement.Embed;
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Windows, "C:/build/Game.exe", embed), external, "raw, Windows executable: external");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Windows, "C:/build/Game.pck", none), external, "raw, Windows .pck only: external");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Linux, "/build/Game.x86_64", embed, raw), external, "raw, Linux executable: external");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Windows, "C:/build/Game.zip", embed), embedded, "raw, .zip export: the fallback embeds");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(MacOs, "/build/Game.app", embed), StreamingAssetsExportPlugin.Placement.Bundle, "raw, macOS: bundle resources");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Android, "/build/Game.apk", embed), embedded, "raw, Android: the fallback embeds");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Web, "/build/index.html", embed), embedded, "raw, Web: the fallback embeds");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Web, "/build/index.html", none), StreamingAssetsExportPlugin.Placement.None, "raw, Web: the fallback none");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Windows, "C:/build/Game.exe", embed, pack), StreamingAssetsExportPlugin.Placement.Pack, "pack, Windows: a pack next to the executable");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Linux, "/build/Game.pck", embed, pack), StreamingAssetsExportPlugin.Placement.Pack, "pack, Linux: a pack next to the main pack");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Windows, "C:/build/Game.zip", embed, pack), embedded, "pack, .zip: the fallback");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(MacOs, "/build/Game.app", embed, pack), embedded, "pack, macOS: the fallback");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Android, "/build/Game.apk", embed, pack), embedded, "pack, Android: the fallback");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(Web, "/build/index.html", none, pack), StreamingAssetsExportPlugin.Placement.None, "pack, Web: the fallback none");
    }

    private void TestPackPath()
    {
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("C:/build/Game.exe", "Game.streaming.pck"), "C:/build/Game.streaming.pck", "the pack is next to the executable");
        string packFile = StreamingAssetsServer.ExpandTemplate("{exe_name}.streaming.pck", "Game");
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("/build/Game.pck", packFile), "/build/Game.streaming.pck", "the default pack file");
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("/build/Game.pck", "sub/a.pck"), "", "a pack file with a folder is refused");
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("", "a.pck"), "", "an empty export path is refused");
    }

    private void TestPackDescription()
    {
        PckPackage description = StreamingAssetsPackBuilder.MakeDescription(PackSample);
        string[] paths = description.GetFilePaths().Order().ToArray();
        string[] expected = [PackSample + "/data/levels.json", PackSample + "/icon.svg"];
        ExpectPaths(paths, expected, "MakeDescription: one entry per file, at its res:// path; .import files are left out");
        PckBuildPlan plan = PckPackageExporter.PlanDescription(description);
        Expect(plan.Valid, "the streaming pack can be built");
        ExpectPaths(plan.ExportSources.Order().ToArray(), expected, "every file goes through the export pipeline");
        Expect(plan.RawFiles.Length == 0, "nothing is copied as it is");
    }

    private void TestPackRefusals()
    {
        Expect(StreamingAssetsPackBuilder.MakeDescription("user://streaming") == null, "a folder outside res:// is refused");
        ExpectEqual(StreamingAssetsPackBuilder.Build(null, PackSample, Root + "a.pck"), Error.InvalidParameter, "Build without a preset is refused");
    }

    private void TestMountPreparedPack()
    {
        // A prepared streaming pack: a file that exists only in the pack, below the sample folder.
        string pack = Root + "Game.streaming.pck";
        string source = Root + "pack_only.txt";
        using (FileAccess file = FileAccess.Open(source, FileAccess.ModeFlags.Write))
        {
            file.StoreString("Hello from the streaming pack");
        }
        var packer = new PckPacker();
        ExpectEqual(packer.PckStart(pack), Error.Ok, "PckStart");
        ExpectEqual(packer.AddFile(PackSample + "/pack_only.txt", source), Error.Ok, "AddFile");
        ExpectEqual(packer.Flush(), Error.Ok, "Flush");
        // StreamingAssetsServer mounts the streaming pack like this in Pack mode.
        string packPath = ProjectSettings.GlobalizePath(pack);
        ExpectEqual(PckPackageServer.Mount(packPath, true), Error.Ok, "the streaming pack mounts");
        ExpectEqual(FileAccess.GetFileAsString(PackSample + "/pack_only.txt"), "Hello from the streaming pack", "the file loads by its res:// path");
        ExpectEqual(PckPackageServer.Unmount(packPath), Error.Ok, "Unmount");
        Expect(!FileAccess.FileExists(PackSample + "/pack_only.txt"), "the file is gone after Unmount");
    }
#endif

    private static string GetPropertyHint(string setting)
    {
        foreach (Godot.Collections.Dictionary property in ProjectSettings.Singleton.GetPropertyList())
        {
            if (property["name"].AsString() == setting)
            {
                return property["hint_string"].AsString();
            }
        }
        return "";
    }
}
