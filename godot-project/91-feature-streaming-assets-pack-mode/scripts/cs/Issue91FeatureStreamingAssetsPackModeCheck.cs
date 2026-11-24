using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue91FeatureStreamingAssetsPackMode;

/// <summary>C# checks of 91-feature-streaming-assets-pack-mode. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>
/// An export can't run here: these checks cover the bound rules of the pack mode (placement, pack path, the description
/// and plan of the streaming pack) and the runtime side: a prepared pack with files of a test folder is mounted the way
/// StreamingAssetsServer mounts the streaming pack in MODE_PACK, and the files load by res://.
/// StreamingAssetsPackBuilder is an editor class (GodotSharpEditor), available with the TOOLS define of the Debug configuration.
/// </remarks>
public partial class Issue91FeatureStreamingAssetsPackModeCheck : FeatureCheckCs
{
    private const string Class = "StreamingAssetsPackBuilder";
    private const string Sample = "res://91-feature-streaming-assets-pack-mode/resources/streaming_sample";
    private const string Pack = "user://91_streaming_pack_cs/Game.streaming.pck";

    private static readonly string[] Methods = ["build", "get_pack_path", "make_description"];

    protected override string CheckName => "91-feature-streaming-assets-pack-mode";

    protected override Task RunChecks()
    {
        CheckBindings();
#if TOOLS
        CheckPlacement();
        CheckPackPath();
        CheckDescriptionAndPlan();
        CheckRefusals();
#else
        Expect(false, "the C# checks need the TOOLS define (Debug configuration with GodotSharpEditor)");
#endif
        CheckMountPreparedPack();
        return Task.CompletedTask;
    }

    private void CheckBindings()
    {
        Expect(ClassDB.ClassExists(Class), $"{Class} exists in the editor build");
        ExpectEqual(ClassDB.GetParentClass(Class).ToString(), "Object", $"{Class} extends Object");
        Expect(!ClassDB.CanInstantiate(Class), $"{Class} can't be instantiated from scripts");
        foreach (string method in Methods)
        {
            Expect(ClassDB.ClassHasMethod(Class, method, true), $"{Class}.{method}() is bound");
        }
        Expect(ProjectSettings.HasSetting("filesystem/streaming_assets/pack_file"), "the pack_file setting is registered");
    }

#if TOOLS
    private void CheckPlacement()
    {
        ExpectEqual((long)StreamingAssetsExportPlugin.Placement.Pack, 4L, "Placement.Pack");
        string[] windows = ["pc", "windows", "x86_64"];
        string[] linux = ["pc", "linux", "linuxbsd", "x86_64"];
        string[] macos = ["pc", "macos", "arm64"];
        string[] android = ["mobile", "android", "arm64"];
        string[] web = ["web"];
        var embed = StreamingAssetsExportPlugin.Fallback.Embed;
        var pack = StreamingAssetsExportPlugin.Mode.Pack;
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(windows, "C:/build/Game.exe", embed, pack), StreamingAssetsExportPlugin.Placement.Pack, "Windows: a pack next to the executable");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(linux, "/build/Game.pck", embed, pack), StreamingAssetsExportPlugin.Placement.Pack, "Linux: a pack next to the main pack");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(windows, "C:/build/Game.zip", embed, pack), StreamingAssetsExportPlugin.Placement.Embed, ".zip: the fallback");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(macos, "/build/Game.app", embed, pack), StreamingAssetsExportPlugin.Placement.Embed, "macOS: the fallback");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(android, "/build/Game.apk", embed, pack), StreamingAssetsExportPlugin.Placement.Embed, "Android: the fallback");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(web, "/build/index.html", StreamingAssetsExportPlugin.Fallback.None, pack), StreamingAssetsExportPlugin.Placement.None, "Web: the fallback none");
        ExpectEqual(StreamingAssetsExportPlugin.ResolvePlacement(windows, "C:/build/Game.exe", embed), StreamingAssetsExportPlugin.Placement.External, "the default mode is raw");
    }

    private void CheckPackPath()
    {
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("C:/build/Game.exe", "Game.streaming.pck"), "C:/build/Game.streaming.pck", "the pack is next to the executable");
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("/build/Game.pck", StreamingAssetsServer.ExpandTemplate("{exe_name}.streaming.pck", "Game")), "/build/Game.streaming.pck", "the default pack file");
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("/build/Game.pck", "sub/a.pck"), "", "a pack file with a folder is refused");
        ExpectEqual(StreamingAssetsPackBuilder.GetPackPath("", "a.pck"), "", "an empty export path is refused");
    }

    private void CheckDescriptionAndPlan()
    {
        PckPackage description = StreamingAssetsPackBuilder.MakeDescription(Sample);
        Expect(description != null, "MakeDescription() returns a PckPackage");
        if (description == null)
        {
            return;
        }
        string[] expected =
        [
            "res://91-feature-streaming-assets-pack-mode/resources/streaming_sample/data/levels.json",
            "res://91-feature-streaming-assets-pack-mode/resources/streaming_sample/icon.svg",
        ];
        Expect(description.GetFilePaths().OrderBy(p => p, System.StringComparer.Ordinal).SequenceEqual(expected), "one entry per file, at its res:// path; .import files are left out");
        PckBuildPlan plan = PckPackageExporter.PlanDescription(description);
        Expect(plan.Valid, "the streaming pack can be built");
        Expect(plan.ExportSources.OrderBy(p => p, System.StringComparer.Ordinal).SequenceEqual(new[] { Sample + "/data/levels.json", Sample + "/icon.svg" }), "every file goes through the export pipeline");
        Expect(plan.RawFiles.Length == 0, "nothing is copied as it is");
    }

    private void CheckRefusals()
    {
        Expect(StreamingAssetsPackBuilder.MakeDescription("user://streaming") == null, "a folder outside res:// is refused");
        ExpectEqual(StreamingAssetsPackBuilder.Build(null, Sample, "user://a.pck"), Error.InvalidParameter, "Build() without a preset");
    }
#endif

    private void CheckMountPreparedPack()
    {
        // A prepared streaming pack: a file that exists only in the pack, below the sample folder.
        DirAccess.MakeDirRecursiveAbsolute(Pack.GetBaseDir());
        string source = "user://91_streaming_pack_cs/pack_only.txt";
        using (var file = FileAccess.Open(source, FileAccess.ModeFlags.Write))
        {
            file.StoreString("Hello from the streaming pack");
        }
        var packer = new PckPacker();
        ExpectEqual(packer.PckStart(Pack), Error.Ok, "PckStart()");
        ExpectEqual(packer.AddFile(Sample + "/pack_only.txt", source), Error.Ok, "AddFile()");
        ExpectEqual(packer.Flush(), Error.Ok, "Flush()");

        // StreamingAssetsServer mounts the streaming pack like this in MODE_PACK.
        string packPath = ProjectSettings.GlobalizePath(Pack);
        ExpectEqual(PckPackageServer.Mount(packPath, true), Error.Ok, "the streaming pack mounts");
        Expect(PckPackageServer.IsMounted(packPath), "IsMounted()");
        ExpectEqual(FileAccess.GetFileAsString(Sample + "/pack_only.txt"), "Hello from the streaming pack", "the file loads by its res:// path");
        ExpectEqual(PckPackageServer.Unmount(packPath), Error.Ok, "Unmount()");
        Expect(!FileAccess.FileExists(Sample + "/pack_only.txt"), "the file is gone after Unmount()");
    }
}
