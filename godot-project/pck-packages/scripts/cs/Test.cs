using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.PckPackages;

/// <summary>
/// Full API test of PCK Packages (PckPackageServer, PckPackage, PckPackageEntry, PckExtractResult, PckPackageExporter, PckBuildPlan).
/// Same tests as scripts/gd/Test.gd. The wrong-key and missing-source tests print engine errors on purpose.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://pck_packages_test/";
    private const string Resources = "res://pck-packages/resources/";
    private const string KeyA = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
    private const string KeyB = "ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100";
    private const string ZeroKey = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string MissingPreset = "No Such Preset";

    private readonly List<string> _mounted = [];
    private readonly List<string> _unmounted = [];

    protected override Task RunTests()
    {
        RemoveFolder(ProjectSettings.GlobalizePath(Root));
        DirAccess.MakeDirRecursiveAbsolute(Root);
        PckPackageServer.PackMounted += OnPackMounted;
        PckPackageServer.PackUnmounted += OnPackUnmounted;
        TestOpenAndRead();
        TestEditBuildMount();
        TestProvidingPackAndUnmount();
        TestEncryption();
        TestResourceLoading();
        TestDescription();
        TestInspectorData();
        TestSampleFiles();
        TestExtract();
#if TOOLS
        TestExporterBindings();
        TestExporterPlan();
        TestExporterHelpers();
#endif
        RemoveFolder(ProjectSettings.GlobalizePath(Root));
        return Task.CompletedTask;
    }

    private static void WritePack(string path, Dictionary<string, string> files, string key = ZeroKey, bool encrypt = false)
    {
        var packer = new PckPacker();
        packer.PckStart(path, 32, key, encrypt);
        foreach (var (target, content) in files)
        {
            packer.AddFileFromBuffer(target, content.ToUtf8Buffer(), encrypt);
        }
        packer.Flush();
    }

    private static void WriteFile(string path, string content)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(content);
    }

    private static string Read(PckPackage package, string path)
    {
        return PckPackageServer.ReadFile(package, path).GetStringFromUtf8();
    }

    private void ExpectPaths(string[] actual, string[] expected, string message)
    {
        Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");
    }

    private void TestOpenAndRead()
    {
        string basePack = Root + "base.pck";
        WritePack(basePack, new() { ["pck/a.txt"] = "Alpha", ["pck/b.txt"] = "Beta" });
        PckPackage package = PckPackageServer.Open(basePack);
        Expect(package != null, "Open returns a package");
        ExpectEqual(package.FormatVersion, 4, "FormatVersion is 4");
        Expect(package.EngineVersion != "", "EngineVersion is set");
        ExpectEqual(package.PackPath, basePack, "PackPath is the opened pack");
        ExpectEqual(package.Alignment, 32, "Alignment is read from the pack");
        Expect(!package.EncryptedDirectory, "the directory is not encrypted");
        Expect(!package.IsDescription(), "an opened pack is not a description");
        ExpectEqual(package.GetFileCount(), 2, "GetFileCount");
        ExpectPaths(package.GetFilePaths(), ["res://pck/a.txt", "res://pck/b.txt"], "GetFilePaths lists all files");
        Expect(package.HasFile("res://pck/a.txt"), "HasFile finds a file");
        Expect(!package.HasFile("res://pck/none.txt"), "HasFile is false for a missing file");
        PckPackageEntry entry = package.GetEntries()[0];
        ExpectEqual(entry.Path, "res://pck/a.txt", "PckPackageEntry.Path");
        ExpectEqual(entry.Size, 5L, "PckPackageEntry.Size");
        ExpectEqual(entry.Md5, "Alpha".Md5Text(), "PckPackageEntry.Md5");
        Expect(entry.Offset > 0, "PckPackageEntry.Offset");
        Expect(!entry.Removal, "PckPackageEntry.Removal is false");
        Expect(!entry.Encrypted, "PckPackageEntry.Encrypted is false");
        Expect(!entry.Delta, "PckPackageEntry.Delta is false");
        Expect(!entry.Pending, "PckPackageEntry.Pending is false for a file of the pack");
        ExpectEqual(entry.SourceType, PckPackage.SourceType.Pack, "PckPackageEntry.SourceType is Pack");
        ExpectEqual(Read(package, "res://pck/a.txt"), "Alpha", "ReadFile without mounting");
        ExpectEqual(PckPackageServer.Verify(package), Error.Ok, "Verify");
        Expect(!PckPackageServer.IsMounted(basePack), "Open does not mount");
        Expect(!PckPackageServer.IsMainPack(basePack), "IsMainPack is false for an ordinary pack");
        Expect(!FileAccess.FileExists("res://pck/a.txt"), "the file is not in res:// before mounting");
        Expect(PckPackageServer.Open(Root + "missing.pck") == null, "Open of a missing file returns null");
        ExpectEqual(PckPackageServer.Mount(Root + "missing.pck"), Error.FileNotFound, "Mount of a missing file returns FileNotFound");
        ExpectEqual(PckPackageServer.GetMountedPacks().Length, 0, "no pack is mounted in the editor");
    }

    private void TestEditBuildMount()
    {
        PckPackage package = PckPackageServer.Open(Root + "base.pck");
        package.AddFileFromBuffer("res://pck/c.txt", "Gamma".ToUtf8Buffer());
        Expect(package.GetEntries()[2].Pending, "an added file is pending");
        ExpectEqual(package.RemoveFile("res://pck/a.txt"), Error.Ok, "RemoveFile");
        ExpectEqual(package.RenameFile("res://pck/b.txt", "res://pck/b2.txt"), Error.Ok, "RenameFile");
        ExpectEqual(package.RemoveFile("res://pck/a.txt"), Error.DoesNotExist, "RemoveFile of a missing file fails");
        string edited = Root + "edited.pck";
        ExpectEqual(PckPackageServer.Build(package, edited), Error.Ok, "Build");
        ExpectEqual(PckPackageServer.Mount(edited), Error.Ok, "Mount");
        Expect(PckPackageServer.IsMounted(edited), "IsMounted");
        Expect(PckPackageServer.GetMountedPacks().Contains(edited), "GetMountedPacks lists the pack");
        Expect(_mounted.SequenceEqual([edited]), "PackMounted is emitted");
        ExpectEqual(FileAccess.GetFileAsString("res://pck/c.txt"), "Gamma", "the added file is in res://");
        ExpectEqual(FileAccess.GetFileAsString("res://pck/b2.txt"), "Beta", "the renamed file is in res://");
        Expect(!FileAccess.FileExists("res://pck/a.txt"), "the removed file is not in res://");
        ExpectEqual(PckPackageServer.Build(package, edited), Error.Busy, "Build onto a mounted pack returns Busy");
        var missingSource = new PckPackage();
        missingSource.AddFile("res://pck/x.txt", Root + "no_such_source.txt");
        ExpectEqual(PckPackageServer.Build(missingSource, Root + "never.pck"), Error.FileNotFound, "Build with a missing source returns FileNotFound");
        Expect(!FileAccess.FileExists(Root + "never.pck"), "Build writes nothing when a source is missing");
    }

    private void TestProvidingPackAndUnmount()
    {
        string edited = Root + "edited.pck";
        string overlay = Root + "overlay.pck";
        WritePack(overlay, new() { ["pck/c.txt"] = "Overlay", ["pck/only_overlay.txt"] = "Only" });
        Expect(ProjectSettings.LoadResourcePack(overlay), "LoadResourcePack still works");
        Expect(PckPackageServer.IsMounted(overlay), "LoadResourcePack goes through the server");
        ExpectEqual(PckPackageServer.GetProvidingPack("res://pck/c.txt"), overlay, "GetProvidingPack names the later pack");
        ExpectEqual(PckPackageServer.GetProvidingPack("res://pck/b2.txt"), edited, "GetProvidingPack names the only pack");
        ExpectEqual(PckPackageServer.GetProvidingPack("res://project.godot"), "", "GetProvidingPack is empty for a project file");
        ExpectEqual(FileAccess.GetFileAsString("res://pck/c.txt"), "Overlay", "the later pack replaces the file");
        ExpectEqual(PckPackageServer.Unmount(overlay), Error.Ok, "Unmount");
        Expect(_unmounted.SequenceEqual([overlay]), "PackUnmounted is emitted");
        Expect(!PckPackageServer.IsMounted(overlay), "IsMounted is false after Unmount");
        Expect(!FileAccess.FileExists("res://pck/only_overlay.txt"), "the file of the unmounted pack is gone");
        ExpectEqual(PckPackageServer.GetProvidingPack("res://pck/c.txt"), edited, "GetProvidingPack names the remaining pack");
        ExpectEqual(FileAccess.GetFileAsString("res://pck/c.txt"), "Gamma", "the replaced file is back");
        ExpectEqual(PckPackageServer.Unmount(overlay), Error.DoesNotExist, "Unmount twice returns DoesNotExist");
        ExpectEqual(PckPackageServer.Unmount(edited), Error.Ok, "Unmount the edited pack");
        Expect(!FileAccess.FileExists("res://pck/c.txt"), "all files of the packs are gone");
        PckPackageServer.PackMounted -= OnPackMounted;
        PckPackageServer.PackUnmounted -= OnPackUnmounted;
    }

    private void TestEncryption()
    {
        string secret = Root + "secret.pck";
        WritePack(secret, new() { ["pck/secret.txt"] = "Hidden" }, KeyA, true);
        PckPackage package = PckPackageServer.Open(secret, KeyA);
        Expect(package.EncryptedDirectory, "open with the right key: the directory is encrypted");
        Expect(package.GetEntries()[0].Encrypted, "PckPackageEntry.Encrypted is true");
        ExpectEqual(Read(package, "res://pck/secret.txt"), "Hidden", "ReadFile decrypts");
        Expect(PckPackageServer.Open(secret, KeyB) == null, "Open with a wrong key returns null");
        string rebuilt = Root + "secret_b.pck";
        ExpectEqual(PckPackageServer.Build(package, rebuilt, KeyB), Error.Ok, "Build encrypts again with another key");
        PckPackage again = PckPackageServer.Open(rebuilt, KeyB);
        ExpectEqual(Read(again, "res://pck/secret.txt"), "Hidden", "the rebuilt pack opens with the new key");
    }

    // PckPackage is a Resource: .pck files load and save through ResourceLoader and ResourceSaver.
    private void TestResourceLoading()
    {
        string pack = Root + "resource.pck";
        WritePack(pack, new() { ["pck/a.txt"] = "Alpha", ["pck/b.txt"] = "Beta" });
        Expect(ClassDB.IsParentClass("PCKPackage", "Resource"), "PCKPackage is a Resource");
        Expect(ResourceLoader.Exists(pack, "PCKPackage"), "ResourceLoader knows .pck files");
        var loaded = (PckPackage)ResourceLoader.Load(pack);
        ExpectEqual(loaded.GetPackPath(), pack, "GetPackPath is the loaded pack");
        Expect(!loaded.IsDescription(), "a loaded .pck is not a description");
        Expect(!PckPackageServer.IsMounted(pack), "Load does not mount");
        Expect(ResourceLoader.Load(pack) == loaded, "Load twice returns the same object");
        ExpectEqual(Read(loaded, "res://pck/b.txt"), "Beta", "ReadFile on a loaded pack");
        WritePack(pack, new() { ["pck/a.txt"] = "Alpha 2", ["pck/c.txt"] = "Gamma" });
        Resource replaced = ResourceLoader.Load(pack, "", ResourceLoader.CacheMode.Replace);
        Expect(replaced == loaded, "CacheMode.Replace keeps the cached object");
        ExpectPaths(loaded.GetFilePaths(), ["res://pck/a.txt", "res://pck/c.txt"], "CacheMode.Replace shows the new entries");
        ExpectEqual(Read(loaded, "res://pck/a.txt"), "Alpha 2", "CacheMode.Replace shows the new data");
        var copy = (PckPackage)loaded.Duplicate();
        Expect(copy != loaded, "Duplicate returns another object");
        ExpectPaths(copy.GetFilePaths(), loaded.GetFilePaths(), "Duplicate keeps the entries");
        ExpectEqual(Read(copy, "res://pck/c.txt"), "Gamma", "a duplicate can read the files");
    }

    private void TestDescription()
    {
        string basePack = Root + "resource.pck";
        string source = Root + "source.txt";
        string later = Root + "later.txt";
        WriteFile(source, "Source");
        DirAccess.RemoveAbsolute(later);
        var description = new PckPackage();
        Expect(description.IsDescription(), "new PckPackage is a description");
        ExpectEqual(description.SetKey(KeyA), Error.Ok, "SetKey");
        description.BasePack = basePack;
        description.Alignment = 64;
        ExpectEqual(description.AddFile("res://pck/source.txt", source, true), Error.Ok, "AddFile");
        ExpectEqual(description.AddFile("res://pck/later.txt", later), Error.Ok, "AddFile with a missing source");
        description.AddFileFromBuffer("res://pck/data.txt", "Data".ToUtf8Buffer());
        Godot.Collections.Array files = description.Get("files").AsGodotArray();
        files.Add(new Godot.Collections.Dictionary { { "path", "pck/from_base.txt" }, { "from_pack", "pck/c.txt" } });
        description.Set("files", files);
        description.AddRemoval("res://pck/old.txt");
        Godot.Collections.Array<PckPackageEntry> entries = description.GetEntries();
        ExpectEqual(entries.Count, 5, "GetEntries lists files and removals");
        ExpectEqual(entries[0].SourceType, PckPackage.SourceType.File, "SourceType.File");
        ExpectEqual(entries[0].Source, source, "PckPackageEntry.Source of a file");
        Expect(entries[0].Encrypted, "PckPackageEntry.Encrypted for AddFile(encrypt = true)");
        ExpectEqual(entries[2].SourceType, PckPackage.SourceType.Data, "SourceType.Data");
        ExpectEqual(entries[3].SourceType, PckPackage.SourceType.Pack, "SourceType.Pack");
        ExpectEqual(entries[3].Source, "res://pck/c.txt", "PckPackageEntry.Source of a pack file");
        Expect(entries[4].Removal, "AddRemoval adds a removal entry");
        ExpectEqual(entries[4].Path, "res://pck/old.txt", "the removal path");
        string tres = Root + "description.tres";
        ExpectEqual(ResourceSaver.Save(description, tres), Error.Ok, "ResourceSaver saves a description as .tres");
        string text = FileAccess.GetFileAsString(tres);
        Expect(!text.Contains(KeyA), "the key is not in the .tres");
        Expect(text.Contains("base_pack") && text.Contains("from_pack") && text.Contains("removals"), "the .tres has base_pack, files and removals");
        var reloaded = (PckPackage)ResourceLoader.Load(tres, "", ResourceLoader.CacheMode.Ignore);
        Expect(reloaded.IsDescription(), "the .tres loads as a description");
        ExpectEqual(GD.VarToStr(reloaded.Get("files")), GD.VarToStr(description.Get("files")), "files are identical after the round trip");
        ExpectPaths(reloaded.Get("removals").AsStringArray(), ["pck/old.txt"], "removals are identical after the round trip");
        ExpectEqual(reloaded.BasePack, basePack, "BasePack after the round trip");
        ExpectEqual(reloaded.Alignment, 64, "Alignment after the round trip");
        ExpectEqual(Read(reloaded, "res://pck/from_base.txt"), "Gamma", "from_pack reads the base pack");
        string built = Root + "built.pck";
        ExpectEqual(ResourceSaver.Save(reloaded, built), Error.FileNotFound, "ResourceSaver saves .pck: a missing source fails");
        WriteFile(later, "Later");
        ExpectEqual(ResourceSaver.Save(reloaded, built), Error.Ok, "ResourceSaver saves .pck: the pack is built");
        PckPackage opened = PckPackageServer.Open(built);
        string[] expectedPaths = ["res://pck/source.txt", "res://pck/later.txt", "res://pck/data.txt", "res://pck/from_base.txt"];
        ExpectPaths(opened.GetFilePaths(), expectedPaths, "the built pack has all files");
        Expect(opened.HasFile("res://pck/old.txt"), "the built pack has the removal entry");
        ExpectEqual(Read(opened, "res://pck/source.txt"), "Source", "a file source is built");
        ExpectEqual(PckPackageServer.Verify(opened), Error.Ok, "Verify of the built pack");
    }

    // The Inspector edits the storage properties `files` and `removals` as snapshots.
    private void TestInspectorData()
    {
        WriteFile(Root + "inspector_source.txt", "Alpha");
        var description = new PckPackage();
        description.Set("files", new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary { { "path", "pck/a.txt" }, { "source", Root + "inspector_source.txt" } },
            new Godot.Collections.Dictionary { { "path", "pck/data.txt" }, { "data", "Data".ToUtf8Buffer() } },
        });
        description.Set("removals", new[] { "pck/old.txt" });
        ExpectPaths(description.GetFilePaths(), ["res://pck/a.txt", "res://pck/data.txt"], "files set as a property are listed in order");
        Godot.Collections.Array before = description.Get("files").AsGodotArray().Duplicate(true);
        Godot.Collections.Array files = description.Get("files").AsGodotArray();
        var first = files[0].AsGodotDictionary();
        first["path"] = "pck/renamed/a.txt";
        description.Set("files", files);
        Expect(description.HasFile("res://pck/renamed/a.txt"), "a rename through the snapshot");
        description.Set("files", before);
        Expect(description.HasFile("res://pck/a.txt"), "undo: the old snapshot restores the entry");
        string pack = Root + "inspector.pck";
        ExpectEqual(PckPackageServer.Build(description, pack), Error.Ok, "Build Pack: the description is built");
        PckPackage opened = PckPackageServer.Open(pack);
        var copy = new PckPackage { BasePack = pack, Alignment = opened.Alignment };
        copy.Set("files", opened.Get("files"));
        copy.Set("removals", opened.Get("removals"));
        Godot.Collections.Array fromPack = copy.Get("files").AsGodotArray();
        ExpectEqual(fromPack.Count, 2, "Create Description: one entry per file");
        Expect(fromPack.All(entry => entry.AsGodotDictionary().ContainsKey("from_pack")), "Create Description: every file is a from_pack entry");
        string rebuilt = Root + "inspector_rebuilt.pck";
        ExpectEqual(PckPackageServer.Build(copy, rebuilt), Error.Ok, "the created description builds");
        ExpectEqual(Read(PckPackageServer.Open(rebuilt), "res://pck/data.txt"), "Data", "a from_pack file is copied from the base pack");
    }

    private void TestSampleFiles()
    {
        var sample = GD.Load<PckPackage>(Resources + "sample_pack.tres");
        Expect(sample.IsDescription(), "sample_pack.tres is a description");
        ExpectEqual(sample.GetFileCount(), 3, "sample_pack.tres has three files");
        var pack = GD.Load<PckPackage>(Resources + "sample_pack.pck");
        Expect(!pack.IsDescription(), "sample_pack.pck is a pack");
        ExpectEqual(pack.GetFileCount(), 3, "sample_pack.pck has three files");
        ExpectEqual(PckPackageServer.Verify(pack), Error.Ok, "sample_pack.pck verifies");
    }

    private void TestExtract()
    {
        string pack = Root + "extract.pck";
        long uid = ResourceUid.CreateId();
        string uidText = ResourceUid.IdToText(uid);
        WritePack(pack, new()
        {
            ["x/a.txt"] = "Alpha",
            ["x/sub/b.txt"] = "Beta",
            ["x/icon.png"] = "PNG",
            ["x/icon.png.import"] = "[remap]\n",
            ["x/orphan.png.import"] = "[remap]\n",
            [".godot/imported/x-icon.ctex"] = "CTEX",
            ["x/level.tscn.remap"] = "[remap]\n",
            ["x/tool.gd"] = "extends Node\n",
            ["x/tool.gd.uid"] = uidText + "\n",
        });
        string output = Root + "extract";
        PckPackage package = PckPackageServer.Open(pack);
        var progress = new List<(string Path, long Done, long Total)>();
        PckPackageServer.ExtractProgressEventHandler onProgress = (path, done, total) => progress.Add((path, done, total));
        PckPackageServer.ExtractProgress += onProgress;
        PckExtractResult result = PckPackageServer.Extract(package, output);
        PckPackageServer.ExtractProgress -= onProgress;
        Expect(result.IsOk(), "Extract: IsOk");
        ExpectEqual(result.Error, Error.Ok, "PckExtractResult.Error is Ok");
        ExpectEqual(result.Errors.Length, 0, "PckExtractResult.Errors is empty");
        string[] written = ["x/a.txt", "x/sub/b.txt", "x/icon.png", "x/icon.png.import", "x/tool.gd", "x/tool.gd.uid"];
        ExpectPaths(result.Written, written.Select(name => output.PathJoin(name)).ToArray(), "Written lists the extracted files in pack order");
        ExpectPaths(result.Skipped, ["res://x/orphan.png.import", "res://.godot/imported/x-icon.ctex"], "Skipped: .import without a source and .godot/");
        ExpectPaths(result.NotEditable, ["res://x/level.tscn"], "NotEditable lists remapped resources by their original path");
        ExpectEqual(result.Renamed.Count, 0, "nothing is renamed");
        ExpectEqual(result.DroppedUids.Length, 0, "no UID is dropped");
        ExpectEqual(FileAccess.GetFileAsString(output.PathJoin("x/sub/b.txt")), "Beta", "an extracted file has the pack data");
        ExpectEqual(progress.Count, 9, "ExtractProgress once per selected file");
        ExpectEqual(progress[8], ("res://x/tool.gd.uid", 9L, 9L), "ExtractProgress reports path, done and total");
        Expect(ResourceUid.HasId(uid), "a free UID is registered at once");
        ExpectEqual(ResourceUid.GetIdPath(uid), output.PathJoin("x/tool.gd"), "the UID points to the extracted file");
        WriteFile(output.PathJoin("x/a.txt"), "Edited");
        PckExtractResult kept = PckPackageServer.Extract(package, output, ["res://x/a.txt"]);
        Expect(kept.Written.Length == 0, "Skip writes nothing");
        ExpectPaths(kept.Skipped, ["res://x/a.txt"], "Skip lists the file as skipped");
        ExpectEqual(FileAccess.GetFileAsString(output.PathJoin("x/a.txt")), "Edited", "Skip keeps the existing file");
        PckExtractResult overwritten = PckPackageServer.Extract(package, output, ["x/a.txt"], PckPackageServer.ExtractPolicy.Overwrite);
        ExpectPaths(overwritten.Written, [output.PathJoin("x/a.txt")], "Overwrite writes the file");
        ExpectEqual(FileAccess.GetFileAsString(output.PathJoin("x/a.txt")), "Alpha", "Overwrite replaces the content");
        PckExtractResult renamed = PckPackageServer.Extract(package, output, ["x/icon.png"], PckPackageServer.ExtractPolicy.Rename);
        ExpectPaths(renamed.Written, [output.PathJoin("x/icon_2.png"), output.PathJoin("x/icon_2.png.import")], "Rename renames the file and its .import");
        ExpectEqual(renamed.Renamed["res://x/icon.png"].AsString(), output.PathJoin("x/icon_2.png"), "Renamed maps the pack path to the new path");
        PckExtractResult byFolder = PckPackageServer.Extract(package, output.PathJoin("folder"), ["res://x/sub/"]);
        ExpectPaths(byFolder.Written, [output.PathJoin("folder/x/sub/b.txt")], "a folder filter extracts the files below it");
        PckExtractResult byPattern = PckPackageServer.Extract(package, output.PathJoin("pattern"), ["*.txt"]);
        ExpectEqual(byPattern.Written.Length, 2, "a pattern filter extracts the matching files");
        PckExtractResult withEngine = PckPackageServer.Extract(package, output.PathJoin("engine"), [], PckPackageServer.ExtractPolicy.Skip, false);
        Expect(DirAccess.DirExistsAbsolute(output.PathJoin("engine/.godot")), "skipEngineFiles = false extracts engine files");
        Expect(!withEngine.Skipped.Contains("res://.godot/imported/x-icon.ctex"), "skipEngineFiles = false does not skip .godot/");
        PckExtractResult second = PckPackageServer.Extract(package, output.PathJoin("second"), ["x/tool.gd"]);
        ExpectPaths(second.DroppedUids, [uidText], "a colliding UID is dropped");
        Expect(!FileAccess.FileExists(output.PathJoin("second/x/tool.gd.uid")), "the .uid file of a dropped UID is not written");
        ExpectEqual(PckPackageServer.Extract(null, output).Error, Error.InvalidParameter, "Extract of null returns InvalidParameter");
        ResourceUid.RemoveId(uid);
    }

#if TOOLS
    private static readonly string[] ExporterMethods =
    [
        "export_description", "export_description_with_preset", "export_target_to_res_path", "find_preset",
        "get_export_target_paths", "get_preset_names", "is_building", "is_export_target", "is_exporting",
        "matches_encryption_filters", "plan_description", "should_encrypt",
    ];

    private void TestExporterBindings()
    {
        Expect(ClassDB.ClassExists("PCKPackageExporter"), "PCKPackageExporter exists in the editor build");
        Expect(!ClassDB.CanInstantiate("PCKPackageExporter"), "PCKPackageExporter cannot be instantiated");
        foreach (string method in ExporterMethods)
        {
            Expect(ClassDB.ClassHasMethod("PCKPackageExporter", method, true), $"PCKPackageExporter.{method} is bound");
        }
        ExpectEqual((string)ClassDB.GetParentClass("PCKBuildPlan"), "RefCounted", "PCKBuildPlan is a RefCounted");
        foreach (string property in new[] { "valid", "export_sources", "raw_files", "removals", "skipped", "errors" })
        {
            ExpectEqual((string)ClassDB.ClassGetPropertySetter("PCKBuildPlan", property), "", $"PCKBuildPlan.{property} is read-only");
        }
        Expect(!PckPackageExporter.IsExporting(), "no export runs during the test");
        Expect(!PckPackageExporter.IsBuilding(), "no description is built during the test");
    }

    private void TestExporterPlan()
    {
        var description = GD.Load<PckPackage>(Resources + "dlc_description.tres");
        PckBuildPlan plan = PckPackageExporter.PlanDescription(description);
        Expect(plan.Valid, "the saved description can be built");
        ExpectPaths(plan.ExportSources, ["res://icon.svg"], "res:// sources go through the export pipeline");
        ExpectPaths(plan.RawFiles, ["dlc/readme.txt", "dlc/settings.cfg"], "data and user:// files are copied as they are");
        ExpectPaths(plan.Removals, ["old/unused.png"], "removals stay removals");
        Expect(plan.Errors.Length == 0, "the plan has no errors");
        PckBuildPlan nullPlan = PckPackageExporter.PlanDescription(null);
        Expect(!nullPlan.Valid, "a null description gives an invalid plan");
        ExpectEqual(nullPlan.Errors.Length, 1, "a null description gives one error");
        var refused = new PckPackage();
        refused.AddFile("textures/icon.svg", "res://icon.svg");
        refused.AddFile("icon.svg.import", "res://icon.svg.import");
        refused.AddFile("other.png.import", "res://other.png.import");
        PckBuildPlan refusedPlan = PckPackageExporter.PlanDescription(refused);
        Expect(!refusedPlan.Valid, "a plan with errors is not valid");
        ExpectEqual(refusedPlan.Errors.Length, 3, "a renamed res:// target and both .import files are refused");
        Expect(refusedPlan.Errors[0].Contains("textures/icon.svg"), "the error names the renamed entry");
        var accepted = new PckPackage();
        accepted.AddFile("icon.svg", "res://icon.svg");
        accepted.AddFile("icon.svg.import", "res://icon.svg.import");
        PckBuildPlan acceptedPlan = PckPackageExporter.PlanDescription(accepted);
        ExpectPaths(acceptedPlan.Skipped, ["icon.svg.import"], "the .import of an exported source is skipped");
        Expect(acceptedPlan.Errors.Length == 0, "a source with its .import can be built");
    }

    private void TestExporterHelpers()
    {
        Expect(PckPackageExporter.MatchesEncryptionFilters("res://scripts/a.gd", ["*.gd"], []), "*.gd matches a script");
        Expect(PckPackageExporter.MatchesEncryptionFilters("scripts/a.gd", ["*.gd"], []), "filters match without res://");
        Expect(PckPackageExporter.MatchesEncryptionFilters("res://SECRET/a.png", ["secret/*"], []), "filters ignore case");
        Expect(!PckPackageExporter.MatchesEncryptionFilters("res://dlc/a.pck", ["*"], ["*.pck"]), "an exclude filter wins");
        Expect(!PckPackageExporter.MatchesEncryptionFilters("res://a.png", [], []), "no filter, no encryption");
        Expect(!PckPackageExporter.ShouldEncrypt(null, "res://a.gd"), "no preset, no encryption");
        const string project = "C:/Projects/Game";
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("build/game.pck", project), "res://build/game.pck", "a relative target is inside the project");
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("C:/Projects/Game/build/game.exe", project), "res://build/game.exe", "an absolute target inside the project");
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("../builds/game.pck", project), "", "a target above the project is outside");
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("C:/Projects/GameOther/game.pck", project), "", "a sibling folder with the same prefix is outside");
        string[] names = PckPackageExporter.GetPresetNames();
        Expect(!names.Contains(MissingPreset), "GetPresetNames does not list an unknown preset");
        Expect(PckPackageExporter.FindPreset(MissingPreset) == null, "FindPreset returns null for an unknown preset");
        Expect(!PckPackageExporter.IsExportTarget("res://pck-packages/none.pck"), "IsExportTarget is false for an ordinary file");
        string[] targets = PckPackageExporter.GetExportTargetPaths();
        Expect(!targets.Contains("res://pck-packages/none.pck"), "GetExportTargetPaths does not list an ordinary file");
        var description = new PckPackage();
        string target = Root + "refused.pck";
        ExpectEqual(PckPackageExporter.ExportDescription(description, MissingPreset, target), Error.DoesNotExist, "an unknown preset is refused");
        ExpectEqual(PckPackageExporter.ExportDescriptionWithPreset(description, null, target), Error.InvalidParameter, "a null preset is refused");
        Expect(!FileAccess.FileExists(target), "nothing is written when the build is refused");
    }
#endif

    private void OnPackMounted(string path) => _mounted.Add(path);

    private void OnPackUnmounted(string path) => _unmounted.Add(path);

    private static void RemoveFolder(string absolute)
    {
        using DirAccess dir = DirAccess.Open(absolute);
        if (dir == null)
        {
            return;
        }
        foreach (string sub in dir.GetDirectories())
        {
            RemoveFolder(absolute.PathJoin(sub));
        }
        foreach (string file in dir.GetFiles())
        {
            dir.Remove(file);
        }
        DirAccess.RemoveAbsolute(absolute);
    }
}
