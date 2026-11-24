using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue89FeatureStreamingAssetsExportPlugin;

/// <summary>C# checks of 89-feature-streaming-assets-export-plugin. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>
/// Exports can't run here: these checks cover the bound rules of the plugin (file selection, target folder, placement).
/// StreamingAssetsExportPlugin is an editor class (GodotSharpEditor), available with the TOOLS define of the Debug configuration.
/// </remarks>
public partial class Issue89FeatureStreamingAssetsExportPluginCheck : FeatureCheckCs
{
    private const string Class = "StreamingAssetsExportPlugin";
    private const string Sample = "res://89-feature-streaming-assets-export-plugin/resources/streaming_sample";

    private static readonly string[] Methods =
    [
        "collect_files", "expand_template", "is_file_excluded",
        "is_path_in_folder", "is_valid_target_folder", "resolve_placement",
    ];

    protected override string CheckName => "89-feature-streaming-assets-export-plugin";

    protected override Task RunChecks()
    {
        CheckBindings();
#if TOOLS
        CheckEnums();
        CheckExpandTemplate();
        CheckTargetFolder();
        CheckPathInFolder();
        CheckFileExcluded();
        CheckCollectFiles();
        CheckPlacement();
#else
        Expect(false, "the C# checks need the TOOLS define (Debug configuration with GodotSharpEditor)");
#endif
        return Task.CompletedTask;
    }

    private void CheckBindings()
    {
        Expect(ClassDB.ClassExists(Class), $"{Class} exists in the editor build");
        ExpectEqual(ClassDB.GetParentClass(Class).ToString(), "EditorExportPlugin", $"{Class} extends EditorExportPlugin");
        Expect(!ClassDB.CanInstantiate(Class), $"{Class} can't be instantiated from scripts");
        foreach (string method in Methods)
        {
            Expect(ClassDB.ClassHasMethod(Class, method, true), $"{Class}.{method}() is bound");
        }
    }

#if TOOLS
    private void CheckEnums()
    {
        ExpectEqual((long)StreamingAssetsExportPlugin.Mode.Raw, 0L, "Mode.Raw");
        ExpectEqual((long)StreamingAssetsExportPlugin.Mode.Pack, 1L, "Mode.Pack");
        ExpectEqual((long)StreamingAssetsExportPlugin.Fallback.Embed, 0L, "Fallback.Embed");
        ExpectEqual((long)StreamingAssetsExportPlugin.Fallback.None, 1L, "Fallback.None");
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.External, 0L, "Placement.External");
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.Embed, 1L, "Placement.Embed");
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.None, 2L, "Placement.None");
    }

    private void CheckExpandTemplate()
    {
        ExpectEqual(StreamingAssetsExportPlugin.ExpandTemplate("{exe_name}_Data/StreamingAssets", "MyGame"), "MyGame_Data/StreamingAssets", "default target expands {exe_name}");
        ExpectEqual(StreamingAssetsExportPlugin.ExpandTemplate("{exe_name}/{exe_name}.data", "a"), "a/a.data", "every {exe_name} is replaced");
        ExpectEqual(StreamingAssetsExportPlugin.ExpandTemplate("{app_name}/x", "MyGame"), "{app_name}/x", "unknown placeholders stay");
    }

    private void CheckTargetFolder()
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

    private void CheckPathInFolder()
    {
        const string folder = "res://streaming_assets";
        Expect(StreamingAssetsExportPlugin.IsPathInFolder("res://streaming_assets/a.png", folder), "a file in the folder matches");
        Expect(StreamingAssetsExportPlugin.IsPathInFolder("res://streaming_assets/sub/b.json", folder + "/"), "a file in a subfolder matches (trailing slash)");
        Expect(!StreamingAssetsExportPlugin.IsPathInFolder("res://streaming_assets_other/a.png", folder), "a sibling folder with the same prefix does not match");
        Expect(!StreamingAssetsExportPlugin.IsPathInFolder("res://other/streaming_assets/a.png", folder), "a nested folder with the same name does not match");
        Expect(!StreamingAssetsExportPlugin.IsPathInFolder("res://a.png", "res://"), "a bare prefix never matches");
    }

    private void CheckFileExcluded()
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

    private void CheckCollectFiles()
    {
        string[] files = StreamingAssetsExportPlugin.CollectFiles(Sample);
        GD.Print($"  files in the sample folder: [{string.Join(", ", files)}]");
        string[] expected = ["config/levels.json", "readme.txt", "sub/deeper/blob.bin"];
        Expect(files.SequenceEqual(expected), $"CollectFiles() skips dot entries, .import and .uid and sorts (got [{string.Join(", ", files)}])");
        Expect(StreamingAssetsExportPlugin.CollectFiles(Sample + "/").SequenceEqual(files), "a trailing slash does not change the result");
        Expect(StreamingAssetsExportPlugin.CollectFiles(ProjectSettings.GlobalizePath(Sample)).SequenceEqual(files), "an absolute OS path gives the same result");
        Expect(StreamingAssetsExportPlugin.CollectFiles(Sample + "/missing").Length == 0, "a missing folder gives an empty list");
    }

    private void CheckPlacement()
    {
        string[] windows = ["pc", "windows", "x86_64"];
        string[] linux = ["pc", "linux", "linuxbsd", "x86_64"];
        string[] macos = ["pc", "macos", "arm64"];
        string[] android = ["mobile", "android", "arm64"];
        string[] web = ["web"];
        var embed = StreamingAssetsExportPlugin.Fallback.Embed;
        var none = StreamingAssetsExportPlugin.Fallback.None;
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(windows, "C:/build/Game.exe", embed), StreamingAssetsExportPlugin.Placement.External, "Windows executable: external");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(windows, "C:/build/Game.pck", none), StreamingAssetsExportPlugin.Placement.External, "Windows .pck only: external");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(linux, "/build/Game.x86_64", embed), StreamingAssetsExportPlugin.Placement.External, "Linux executable: external");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(windows, "C:/build/Game.zip", embed), StreamingAssetsExportPlugin.Placement.Embed, ".zip export: fallback embed");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(macos, "/build/Game.app", embed), StreamingAssetsExportPlugin.Placement.Embed, "macOS: fallback until #90");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(android, "/build/Game.apk", embed), StreamingAssetsExportPlugin.Placement.Embed, "Android: fallback embed");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(web, "/build/index.html", embed), StreamingAssetsExportPlugin.Placement.Embed, "Web: fallback embed");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(web, "/build/index.html", none), StreamingAssetsExportPlugin.Placement.None, "Web: fallback none");
    }
#endif
}
