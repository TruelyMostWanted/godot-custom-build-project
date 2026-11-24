using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue88FeatureStreamingAssetsServer;

/// <summary>
/// C# checks of 88-feature-streaming-assets-server. Check the same things as scripts/gd/check.gd.
/// project.godot enables streaming assets with the source folder below. The checks run from the editor
/// binary, so streaming:// points to the source folder (LocationSourceFolder). The external folder
/// layout is checked in a second process started with --streaming-assets-path=&lt;folder&gt;.
/// </summary>
public partial class Issue88FeatureStreamingAssetsServerCheck : FeatureCheckCs
{
    private const string ClassNameServer = "StreamingAssetsServer";
    private const string Source = "res://88-feature-streaming-assets-server/resources/streaming_assets";
    private const string PrintScript = "res://88-feature-streaming-assets-server/scripts/gd/print_location.gd";

    private static readonly string[] Methods =
    [
        "expand_template", "get_exe_name", "get_fallback", "get_location", "get_mode",
        "get_os_path", "get_root", "get_source_folder", "is_enabled",
    ];

    protected override string CheckName => "88-feature-streaming-assets-server";

    protected override Task RunChecks()
    {
        CheckBindings();
        CheckSettings();
        CheckState();
        CheckPrefix();
        CheckReading();
        CheckOsPath();
        CheckExpandTemplate();
        CheckExternalFolderOverride();
        return Task.CompletedTask;
    }

    private void CheckBindings()
    {
        Expect(Engine.HasSingleton(ClassNameServer), $"{ClassNameServer} is a singleton");
        foreach (string method in Methods)
        {
            Expect(ClassDB.ClassHasMethod(ClassNameServer, method, true), $"{ClassNameServer}.{method}() is bound");
        }
        ExpectEqual((int)StreamingAssetsServer.Mode.Raw, 0, "Mode.Raw");
        ExpectEqual((int)StreamingAssetsServer.Mode.Pack, 1, "Mode.Pack");
        ExpectEqual((int)StreamingAssetsServer.Fallback.Embed, 0, "Fallback.Embed");
        ExpectEqual((int)StreamingAssetsServer.Fallback.None, 1, "Fallback.None");
        ExpectEqual((int)StreamingAssetsServer.Location.SourceFolder, 0, "Location.SourceFolder");
        ExpectEqual((int)StreamingAssetsServer.Location.ExternalFolder, 1, "Location.ExternalFolder");
        ExpectEqual((int)StreamingAssetsServer.Location.Bundle, 2, "Location.Bundle");
        ExpectEqual((int)StreamingAssetsServer.Location.Embedded, 3, "Location.Embedded");
        ExpectEqual((int)StreamingAssetsServer.Location.Pack, 4, "Location.Pack");
        ExpectEqual((int)StreamingAssetsServer.Location.None, 5, "Location.None");
    }

    private void CheckSettings()
    {
        var defaults = new Godot.Collections.Dictionary
        {
            { "filesystem/streaming_assets/enabled", false },
            { "filesystem/streaming_assets/source_folder", "res://streaming_assets" },
            { "filesystem/streaming_assets/mode", 0 },
            { "filesystem/streaming_assets/target_folder", "{exe_name}_Data/StreamingAssets" },
            { "filesystem/streaming_assets/pack_file", "{exe_name}.streaming.pck" },
            { "filesystem/streaming_assets/fallback", 0 },
        };
        GodotObject settings = ProjectSettings.Singleton;
        foreach (Variant key in defaults.Keys)
        {
            string setting = key.AsString();
            Expect(ProjectSettings.HasSetting(setting), $"{setting} is registered");
            Variant revert = settings.PropertyGetRevert(setting);
            ExpectEqual(revert.ToString(), defaults[key].ToString(), $"{setting} default");
        }
        ExpectEqual(GetPropertyHint("filesystem/streaming_assets/mode"), "Raw,Pack", "mode is an enum in the Inspector");
        ExpectEqual(GetPropertyHint("filesystem/streaming_assets/fallback"), "Embed,None", "fallback is an enum in the Inspector");
        ExpectEqual(ProjectSettings.GetSetting("filesystem/streaming_assets/enabled").AsBool(), true, "project.godot enables streaming assets");
    }

    private void CheckState()
    {
        Expect(StreamingAssetsServer.IsEnabled(), "IsEnabled()");
        ExpectEqual(StreamingAssetsServer.GetMode(), StreamingAssetsServer.Mode.Raw, "GetMode()");
        ExpectEqual(StreamingAssetsServer.GetFallback(), StreamingAssetsServer.Fallback.Embed, "GetFallback()");
        ExpectEqual(StreamingAssetsServer.GetLocation(), StreamingAssetsServer.Location.SourceFolder, "a game run from the editor reads the source folder");
        ExpectEqual(StreamingAssetsServer.GetSourceFolder(), Source, "GetSourceFolder()");
        ExpectEqual(StreamingAssetsServer.GetRoot(), Source, "GetRoot() is the source folder");
    }

    private void CheckPrefix()
    {
        Expect(PathPrefixServer.HasPrefix("streaming://"), "streaming:// is registered");
        Expect(PathPrefixServer.IsPrefixLocked("streaming://"), "streaming:// is locked");
        Expect(PathPrefixServer.IsPrefixAvailable("streaming://"), "streaming:// is available");
        Expect(!PathPrefixServer.IsPrefixReadOnly("streaming://"), "the source folder is writable in the editor");
        ExpectEqual(PathPrefixServer.RegisterPrefixFolder("streaming://", "user://elsewhere"), Error.Locked, "streaming:// can't be replaced");
        ExpectEqual(PathPrefixServer.UnregisterPrefix("streaming://"), Error.Locked, "streaming:// can't be removed");
        ExpectEqual(PathPrefixServer.Globalize("streaming://readme.txt"), ProjectSettings.GlobalizePath(Source + "/readme.txt"), "Globalize() resolves to the source folder");
    }

    private void CheckReading()
    {
        FileAccess file = FileAccess.Open("streaming://config/levels.json", FileAccess.ModeFlags.Read);
        Expect(file != null, "FileAccess opens streaming://config/levels.json");
        if (file != null)
        {
            Variant data = Json.ParseString(file.GetAsText());
            file.Close();
            bool ok = data.VariantType == Variant.Type.Dictionary
                && data.AsGodotDictionary().ContainsKey("levels")
                && data.AsGodotDictionary()["levels"].AsGodotArray().Count == 3;
            Expect(ok, "levels.json is read through streaming://");
        }
        Expect(FileAccess.FileExists("streaming://readme.txt"), "FileAccess.FileExists(streaming://readme.txt)");
        Expect(!FileAccess.FileExists("streaming://missing.txt"), "a missing file does not exist");
        Expect(System.Array.IndexOf(DirAccess.GetFilesAt("streaming://config"), "levels.json") >= 0, "DirAccess lists streaming://config");
    }

    private void CheckOsPath()
    {
        string expected = ProjectSettings.GlobalizePath(Source + "/config/levels.json");
        ExpectEqual(StreamingAssetsServer.GetOsPath("streaming://config/levels.json"), expected, "GetOsPath() of a streaming:// path");
        ExpectEqual(StreamingAssetsServer.GetOsPath("config/levels.json"), expected, "GetOsPath() of a relative path");
        ExpectEqual(StreamingAssetsServer.GetOsPath("streaming://config/../config/levels.json"), expected, "GetOsPath() normalizes ..");
        ExpectEqual(StreamingAssetsServer.GetOsPath("streaming://../project.godot"), "", "GetOsPath() refuses paths that leave the folder");
        ExpectEqual(StreamingAssetsServer.GetOsPath("user://a.txt"), "", "GetOsPath() refuses other prefixes");
        Expect(FileAccess.FileExists(expected), "the OS path exists");
    }

    private void CheckExpandTemplate()
    {
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{exe_name}_Data/StreamingAssets", "MyGame"), "MyGame_Data/StreamingAssets", "{exe_name}");
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{exe_name}.streaming.pck", "MyGame"), "MyGame.streaming.pck", "pack file template");
        string appName = ValidateFilename(ProjectSettings.GetSetting("application/config/name").AsString());
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{app_name}", "x"), appName, "{app_name} is the project name");
        string version = ValidateFilename(ProjectSettings.GetSetting("application/config/version", "").AsString());
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("v{version}", "x"), "v" + version, "{version} is the project version");
        ExpectEqual(StreamingAssetsServer.ExpandTemplate("{unknown}", "x"), "{unknown}", "unknown placeholders stay");
        if (ClassDB.ClassExists("StreamingAssetsExportPlugin"))
        {
            Variant fromPlugin = ClassDB.ClassCallStatic("StreamingAssetsExportPlugin", "expand_template", "{app_name}/{exe_name}", "MyGame");
            ExpectEqual(fromPlugin.AsString(), StreamingAssetsServer.ExpandTemplate("{app_name}/{exe_name}", "MyGame"), "the export plugin uses the same helper");
        }
        string exeName = StreamingAssetsServer.GetExeName();
        GD.Print($"  exe_name: {exeName}");
        Expect(!string.IsNullOrEmpty(exeName), "GetExeName() is not empty");
        if (OS.GetName() == "Windows" || OS.GetName() == "Linux")
        {
            ExpectEqual(exeName, OS.GetExecutablePath().GetFile().GetBaseName(), "GetExeName() is the executable name");
        }
    }

    private void CheckExternalFolderOverride()
    {
        if (OS.HasFeature("web") || OS.HasFeature("android") || OS.HasFeature("ios"))
        {
            GD.Print("  skipped: no second process on this platform");
            return;
        }
        string folder = ProjectSettings.GlobalizePath("user://88_streaming_external_cs");
        DirAccess.MakeDirRecursiveAbsolute(folder);
        using (FileAccess file = FileAccess.Open(folder.PathJoin("hello.txt"), FileAccess.ModeFlags.Write))
        {
            file.StoreString("Hello from outside");
        }

        var output = new Godot.Collections.Array();
        string[] args = ["--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--streaming-assets-path=" + folder, "--script", PrintScript];
        int code = OS.Execute(OS.GetExecutablePath(), args, output, true);
        string text = string.Join("\n", output);
        ExpectEqual(code, 0, "the second process exits with 0");
        Expect(text.Contains($"STREAMING_LOCATION={(int)StreamingAssetsServer.Location.ExternalFolder}"), "--streaming-assets-path gives Location.ExternalFolder");
        Expect(text.Contains($"STREAMING_ROOT={folder.SimplifyPath()}"), "the root is the given folder");
        Expect(text.Contains("STREAMING_TEXT=Hello from outside"), "streaming:// reads the external folder");
        Expect(text.Contains($"STREAMING_OS_PATH={folder.SimplifyPath().PathJoin("hello.txt")}"), "GetOsPath() points into the external folder");
    }

    // The same as String.validate_filename() of the engine, which C# does not expose.
    private static string ValidateFilename(string name)
    {
        string result = name.StripEdges();
        foreach (string ch in new[] { ":", "/", "\\", "?", "*", "\"", "|", "%", "<", ">" })
        {
            result = result.Replace(ch, "_");
        }
        return result;
    }

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
