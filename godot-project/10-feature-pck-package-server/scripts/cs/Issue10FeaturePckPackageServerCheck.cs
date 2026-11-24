using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue10FeaturePckPackageServer;

/// <summary>C# checks of 10-feature-pck-package-server. Check the same things as scripts/gd/check.gd.
/// The wrong-key checks print engine errors on purpose.</summary>
public partial class Issue10FeaturePckPackageServerCheck : FeatureCheckCs
{
    private const string Root = "user://10-feature-pck-package-server/cs/";
    private const string KeyA = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
    private const string KeyB = "ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100";
    private const string ZeroKey = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly List<string> _mounted = new();
    private readonly List<string> _unmounted = new();

    protected override string CheckName => "10-feature-pck-package-server";

    private static void WritePack(string path, Dictionary<string, string> files, string key = ZeroKey, bool encrypt = false)
    {
        var packer = new PckPacker();
        packer.PckStart(path, 32, key, encrypt);
        foreach (KeyValuePair<string, string> file in files)
        {
            packer.AddFileFromBuffer(file.Key, file.Value.ToUtf8Buffer(), encrypt);
        }
        packer.Flush();
    }

    private void OnMounted(string path) => _mounted.Add(path);

    private void OnUnmounted(string path) => _unmounted.Add(path);

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        Expect(Engine.HasSingleton("PckPackageServer"), "PckPackageServer singleton exists");
        PckPackageServer.PackMounted += OnMounted;
        PckPackageServer.PackUnmounted += OnUnmounted;

        // The main pack of an exported project opens and lists its files.
        foreach (string pack in PckPackageServer.GetMounted())
        {
            if (PckPackageServer.IsMainPack(pack))
            {
                PckPackage main = PckPackageServer.Open(pack);
                Expect(main != null && main.GetFileCount() > 0, $"main pack {pack} opens and lists files");
            }
        }

        // Open, list and read without mounting.
        string basePack = Root + "base.pck";
        WritePack(basePack, new Dictionary<string, string> { { "pck10cs/a.txt", "Alpha" }, { "pck10cs/b.txt", "Beta" } });
        PckPackage package = PckPackageServer.Open(basePack);
        Expect(package != null, "Open() returns a package");
        ExpectEqual(package.FormatVersion, 4, "FormatVersion is 4");
        Expect(!string.IsNullOrEmpty(package.EngineVersion), "EngineVersion is set");
        ExpectEqual(string.Join(",", package.GetPaths()), "res://pck10cs/a.txt,res://pck10cs/b.txt", "GetPaths() lists all files");
        Godot.Collections.Array<Godot.Collections.Dictionary> entries = package.GetEntries();
        Expect(entries.Count == 2 && (long)entries[0]["size"] == 5 && (string)entries[0]["md5"] == "Alpha".Md5Text(), "GetEntries() has size and md5");
        ExpectEqual(PckPackageServer.ReadFile(package, "res://pck10cs/a.txt").GetStringFromUtf8(), "Alpha", "ReadFile() without mounting");
        ExpectEqual(PckPackageServer.Verify(package), Error.Ok, "Verify()");
        Expect(!PckPackageServer.IsMounted(basePack), "Open() does not mount");
        Expect(!FileAccess.FileExists("res://pck10cs/a.txt"), "file is not in res:// before mounting");

        // Edit and build: the new pack mounts and shows the changes.
        package.AddFileFromBuffer("res://pck10cs/c.txt", "Gamma".ToUtf8Buffer());
        ExpectEqual(package.RemoveFile("res://pck10cs/a.txt"), Error.Ok, "RemoveFile()");
        ExpectEqual(package.RenameFile("res://pck10cs/b.txt", "res://pck10cs/b2.txt"), Error.Ok, "RenameFile()");
        string edited = Root + "edited.pck";
        ExpectEqual(PckPackageServer.Build(package, edited), Error.Ok, "Build()");
        ExpectEqual(PckPackageServer.Mount(edited), Error.Ok, "Mount()");
        Expect(PckPackageServer.IsMounted(edited) && PckPackageServer.GetMounted().Contains(edited), "IsMounted() and GetMounted()");
        Expect(_mounted.Contains(edited), "PackMounted emitted");
        ExpectEqual(FileAccess.GetFileAsString("res://pck10cs/c.txt"), "Gamma", "added file is in res://");
        ExpectEqual(FileAccess.GetFileAsString("res://pck10cs/b2.txt"), "Beta", "renamed file is in res://");
        Expect(!FileAccess.FileExists("res://pck10cs/a.txt"), "removed file is not in res://");

        // Two packs provide the same path: GetPackOf() names the one that is used.
        string overlay = Root + "overlay.pck";
        WritePack(overlay, new Dictionary<string, string> { { "pck10cs/c.txt", "Overlay" }, { "pck10cs/only_overlay.txt", "Only" } });
        Expect(ProjectSettings.LoadResourcePack(overlay), "LoadResourcePack() still works");
        Expect(PckPackageServer.IsMounted(overlay), "LoadResourcePack() goes through the server");
        ExpectEqual(PckPackageServer.GetPackOf("res://pck10cs/c.txt"), overlay, "GetPackOf() names the later pack");
        ExpectEqual(PckPackageServer.GetPackOf("res://pck10cs/b2.txt"), edited, "GetPackOf() names the only pack");
        ExpectEqual(FileAccess.GetFileAsString("res://pck10cs/c.txt"), "Overlay", "later pack replaces the file");

        // Unmount: files of the pack disappear, replaced files come back.
        ExpectEqual(PckPackageServer.Unmount(overlay), Error.Ok, "Unmount()");
        Expect(_unmounted.Contains(overlay), "PackUnmounted emitted");
        Expect(!PckPackageServer.IsMounted(overlay), "IsMounted() false after Unmount()");
        Expect(!FileAccess.FileExists("res://pck10cs/only_overlay.txt"), "file of the unmounted pack is gone");
        ExpectEqual(PckPackageServer.GetPackOf("res://pck10cs/c.txt"), edited, "GetPackOf() names the remaining pack");
        ExpectEqual(FileAccess.GetFileAsString("res://pck10cs/c.txt"), "Gamma", "replaced file is back");
        ExpectEqual(PckPackageServer.Unmount(overlay), Error.DoesNotExist, "Unmount() twice: DoesNotExist");
        ExpectEqual(PckPackageServer.Unmount(edited), Error.Ok, "Unmount() the edited pack");
        Expect(!FileAccess.FileExists("res://pck10cs/c.txt"), "all files of the packs are gone");
        ExpectEqual(PckPackageServer.Build(package, edited), Error.Ok, "Build() onto an unmounted pack");

        // Encrypted packs.
        string secret = Root + "secret.pck";
        WritePack(secret, new Dictionary<string, string> { { "pck10cs/secret.txt", "Hidden" } }, KeyA, true);
        PckPackage encrypted = PckPackageServer.Open(secret, KeyA);
        Expect(encrypted != null && encrypted.EncryptedDirectory, "Open() with the right key");
        ExpectEqual(PckPackageServer.ReadFile(encrypted, "res://pck10cs/secret.txt").GetStringFromUtf8(), "Hidden", "ReadFile() decrypts");
        Expect(PckPackageServer.Open(secret, KeyB) == null, "Open() with a wrong key returns null");

        CheckResource();
        CheckExtract();

        PckPackageServer.PackMounted -= OnMounted;
        PckPackageServer.PackUnmounted -= OnUnmounted;
        return Task.CompletedTask;
    }

    private static void WriteText(string path, string text)
    {
        FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f.StoreString(text);
        f.Close();
    }

    /// <summary>#83: PckPackage is a Resource. .pck files load and save through ResourceLoader and ResourceSaver,
    /// and a description is saved as .tres. The missing-source check prints an engine error on purpose.</summary>
    private void CheckResource()
    {
        string pack = Root + "resource.pck";
        WritePack(pack, new Dictionary<string, string> { { "pck83cs/a.txt", "Alpha" }, { "pck83cs/b.txt", "Beta" } });

        Expect(ClassDB.IsParentClass("PckPackage", "Resource"), "PckPackage is a Resource");
        Expect(ResourceLoader.Exists(pack, "PckPackage"), "ResourceLoader knows .pck files");
        PckPackage loaded = GD.Load<PckPackage>(pack);
        Expect(loaded != null, "Load() of a .pck returns a PckPackage");
        ExpectEqual(loaded.PackPath, pack, "PackPath is the loaded pack");
        Expect(!loaded.IsDescription(), "a loaded .pck is not a description");
        Expect(!PckPackageServer.IsMounted(pack), "Load() does not mount");
        Expect(ReferenceEquals(GD.Load<PckPackage>(pack), loaded), "Load() twice returns the same object");
        ExpectEqual(PckPackageServer.ReadFile(loaded, "res://pck83cs/b.txt").GetStringFromUtf8(), "Beta", "ReadFile() on a loaded pack");

        // CacheMode.Replace after a rebuild: the cached object shows the new entries.
        WritePack(pack, new Dictionary<string, string> { { "pck83cs/a.txt", "Alpha 2" }, { "pck83cs/c.txt", "Gamma" } });
        Resource replaced = ResourceLoader.Load(pack, "", ResourceLoader.CacheMode.Replace);
        Expect(ReferenceEquals(replaced, loaded), "CacheMode.Replace keeps the cached object");
        ExpectEqual(string.Join(",", loaded.GetPaths()), "res://pck83cs/a.txt,res://pck83cs/c.txt", "CacheMode.Replace sees the new entries");
        ExpectEqual(PckPackageServer.ReadFile(loaded, "res://pck83cs/a.txt").GetStringFromUtf8(), "Alpha 2", "CacheMode.Replace sees the new data");

        // Duplicate() keeps the entries.
        var copy = (PckPackage)loaded.Duplicate();
        Expect(!ReferenceEquals(copy, loaded) && string.Join(",", copy.GetPaths()) == string.Join(",", loaded.GetPaths()), "Duplicate() keeps the entries");
        ExpectEqual(PckPackageServer.ReadFile(copy, "res://pck83cs/c.txt").GetStringFromUtf8(), "Gamma", "Duplicate() can read the files");

        // A description saved as .tres: no key, and loaded again it is the same.
        string source = Root + "source.txt";
        WriteText(source, "Source");
        string later = Root + "later.txt";
        DirAccess.RemoveAbsolute(later);
        var description = new PckPackage();
        Expect(description.IsDescription(), "new PckPackage() is a description");
        ExpectEqual(description.SetKey(KeyA), Error.Ok, "SetKey()");
        description.BasePack = pack;
        description.Alignment = 64;
        ExpectEqual(description.AddFile("res://pck83cs/source.txt", source, true), Error.Ok, "AddFile()");
        ExpectEqual(description.AddFile("res://pck83cs/later.txt", later), Error.Ok, "AddFile() with a missing source");
        description.AddFileFromBuffer("res://pck83cs/data.txt", "Data".ToUtf8Buffer());
        Godot.Collections.Array files = description.Get("files").AsGodotArray();
        files.Add(new Godot.Collections.Dictionary { { "path", "pck83cs/from_base.txt" }, { "from_pack", "pck83cs/c.txt" } });
        description.Set("files", files);
        description.MarkRemoval("res://pck83cs/old.txt");
        Godot.Collections.Array<Godot.Collections.Dictionary> entries = description.GetEntries();
        Expect(entries.Count == 5
                && entries[0]["source_type"].AsInt32() == (int)PckPackage.SourceType.File
                && entries[2]["source_type"].AsInt32() == (int)PckPackage.SourceType.Data
                && entries[3]["source_type"].AsInt32() == (int)PckPackage.SourceType.Pack, "GetEntries() has source_type");
        ExpectEqual(entries[3]["source"].AsString(), "res://pck83cs/c.txt", "GetEntries() has source");

        string tres = Root + "description.tres";
        ExpectEqual(ResourceSaver.Save(description, tres), Error.Ok, "ResourceSaver.Save() as .tres");
        string text = FileAccess.GetFileAsString(tres);
        Expect(!text.Contains(KeyA), "the key is not in the .tres");
        Expect(text.Contains("base_pack") && text.Contains("from_pack") && text.Contains("removals"), "the .tres has base_pack, files and removals");
        var reloaded = ResourceLoader.Load(tres, "", ResourceLoader.CacheMode.Ignore) as PckPackage;
        Expect(reloaded != null && reloaded.IsDescription(), "the .tres loads as a description");
        ExpectEqual(GD.VarToStr(reloaded.Get("files")), GD.VarToStr(description.Get("files")), "files are identical after the round trip");
        ExpectEqual(string.Join(",", reloaded.Get("removals").AsStringArray()), "pck83cs/old.txt", "removals are identical after the round trip");
        ExpectEqual(reloaded.BasePack, pack, "BasePack after the round trip");
        ExpectEqual(reloaded.Alignment, 64, "Alignment after the round trip");
        ExpectEqual(PckPackageServer.ReadFile(reloaded, "res://pck83cs/from_base.txt").GetStringFromUtf8(), "Gamma", "from_pack reads the base pack");

        // Saving to .pck builds the pack; missing sources are reported by the build.
        string built = Root + "built.pck";
        ExpectEqual(ResourceSaver.Save(reloaded, built), Error.FileNotFound, "ResourceSaver.Save() as .pck fails for a missing source");
        WriteText(later, "Later");
        ExpectEqual(ResourceSaver.Save(reloaded, built), Error.Ok, "ResourceSaver.Save() as .pck builds the pack");
        PckPackage opened = PckPackageServer.Open(built);
        Expect(opened != null && string.Join(",", opened.GetPaths()) == "res://pck83cs/source.txt,res://pck83cs/later.txt,res://pck83cs/data.txt,res://pck83cs/from_base.txt", "the built pack has all files");
        Expect(opened != null && opened.HasFile("res://pck83cs/old.txt"), "the built pack has the removal entry");
        ExpectEqual(PckPackageServer.ReadFile(opened, "res://pck83cs/source.txt").GetStringFromUtf8(), "Source", "encrypted file reads back");
        ExpectEqual(PckPackageServer.Verify(opened), Error.Ok, "Verify() of the built pack");
    }

    private readonly List<string> _progress = new();

    private void OnExtractProgress(string path, long done, long total) => _progress.Add($"{path}|{done}|{total}");

    private static void RemoveDir(string path)
    {
        DirAccess dir = DirAccess.Open(path);
        if (dir == null)
        {
            return;
        }
        foreach (string sub in dir.GetDirectories())
        {
            RemoveDir(path.PathJoin(sub));
        }
        foreach (string file in dir.GetFiles())
        {
            dir.Remove(file);
        }
        DirAccess.RemoveAbsolute(path);
    }

    // #84: PckPackageServer.Extract() writes the files of a pack into a folder.
    // The null-package check prints an engine error on purpose.
    private void CheckExtract()
    {
        string pack = Root + "extract.pck";
        long uid = ResourceUid.CreateId();
        string uidText = ResourceUid.IdToText(uid);
        WritePack(pack, new Dictionary<string, string>
        {
            { "pck84cs/a.txt", "Alpha" },
            { "pck84cs/sub/b.txt", "Beta" },
            { "pck84cs/icon.png", "PNG" },
            { "pck84cs/icon.png.import", "[remap]\n" },
            { "pck84cs/orphan.png.import", "[remap]\n" },
            { ".godot/imported/pck84cs-icon.ctex", "CTEX" },
            { "pck84cs/level.tscn.remap", "[remap]\n" },
            { "pck84cs/tool.gd", "extends Node\n" },
            { "pck84cs/tool.gd.uid", uidText + "\n" },
        });
        string outDir = Root + "extract";
        RemoveDir(outDir);
        PckPackage package = PckPackageServer.Open(pack);
        Expect(package != null, "the pack to extract opens");

        // Extract everything: engine files are left out, sidecars follow their source.
        _progress.Clear();
        PckPackageServer.ExtractProgress += OnExtractProgress;
        PckExtractResult result = PckPackageServer.Extract(package, outDir);
        PckPackageServer.ExtractProgress -= OnExtractProgress;
        Expect(result != null, "Extract() returns a PckExtractResult");
        Expect(result.IsOk() && result.Error == Error.Ok && result.Errors.Length == 0, "Extract() without errors");
        string[] expectedWritten =
        {
            outDir.PathJoin("pck84cs/a.txt"), outDir.PathJoin("pck84cs/sub/b.txt"), outDir.PathJoin("pck84cs/icon.png"),
            outDir.PathJoin("pck84cs/icon.png.import"), outDir.PathJoin("pck84cs/tool.gd"), outDir.PathJoin("pck84cs/tool.gd.uid"),
        };
        ExpectEqual(string.Join(",", result.Written), string.Join(",", expectedWritten), "Written lists the extracted files in pack order");
        ExpectEqual(string.Join(",", result.Skipped), "res://pck84cs/orphan.png.import,res://.godot/imported/pck84cs-icon.ctex", "Skipped: .import without source and .godot/");
        ExpectEqual(string.Join(",", result.NotEditable), "res://pck84cs/level.tscn", "NotEditable lists remapped resources by their original path");
        Expect(result.Renamed.Count == 0 && result.DroppedUids.Length == 0, "nothing renamed, no UID dropped");
        ExpectEqual(FileAccess.GetFileAsString(outDir.PathJoin("pck84cs/sub/b.txt")), "Beta", "extracted file has the pack data");
        Expect(!DirAccess.DirExistsAbsolute(outDir.PathJoin(".godot")), ".godot/ is not extracted");
        ExpectEqual(_progress.Count, 9, "ExtractProgress once per selected file");
        Expect(_progress.Count == 9 && _progress[8] == "res://pck84cs/tool.gd.uid|9|9", "ExtractProgress reports path, done and total");
        Expect(ResourceUid.HasId(uid), "a free UID is registered at once");
        ExpectEqual(ResourceUid.HasId(uid) ? ResourceUid.GetIdPath(uid) : "", outDir.PathJoin("pck84cs/tool.gd"), "the UID points to the extracted file");

        // Conflict policies.
        WriteText(outDir.PathJoin("pck84cs/a.txt"), "Edited");
        PckExtractResult kept = PckPackageServer.Extract(package, outDir, new[] { "res://pck84cs/a.txt" });
        Expect(kept.Written.Length == 0 && string.Join(",", kept.Skipped) == "res://pck84cs/a.txt", "ExtractPolicy.Skip keeps existing files");
        ExpectEqual(FileAccess.GetFileAsString(outDir.PathJoin("pck84cs/a.txt")), "Edited", "the existing file is unchanged");
        PckExtractResult overwritten = PckPackageServer.Extract(package, outDir, new[] { "pck84cs/a.txt" }, PckPackageServer.ExtractPolicy.Overwrite);
        ExpectEqual(string.Join(",", overwritten.Written), outDir.PathJoin("pck84cs/a.txt"), "ExtractPolicy.Overwrite writes the file");
        ExpectEqual(FileAccess.GetFileAsString(outDir.PathJoin("pck84cs/a.txt")), "Alpha", "the existing file is overwritten");
        PckExtractResult renamed = PckPackageServer.Extract(package, outDir, new[] { "pck84cs/icon.png" }, PckPackageServer.ExtractPolicy.Rename);
        ExpectEqual(string.Join(",", renamed.Written), outDir.PathJoin("pck84cs/icon_2.png") + "," + outDir.PathJoin("pck84cs/icon_2.png.import"), "ExtractPolicy.Rename renames the file and its .import");
        ExpectEqual(renamed.Renamed.TryGetValue("res://pck84cs/icon.png", out Variant renamedTo) ? renamedTo.AsString() : "", outDir.PathJoin("pck84cs/icon_2.png"), "Renamed maps the pack path to the new path");

        // Filters: folders and patterns.
        PckExtractResult byFolder = PckPackageServer.Extract(package, outDir.PathJoin("folder"), new[] { "res://pck84cs/sub/" });
        ExpectEqual(string.Join(",", byFolder.Written), outDir.PathJoin("folder/pck84cs/sub/b.txt"), "a folder filter extracts the files below it");
        PckExtractResult byPattern = PckPackageServer.Extract(package, outDir.PathJoin("pattern"), new[] { "*.txt" });
        ExpectEqual(byPattern.Written.Length, 2, "a pattern filter extracts the matching files");

        // The UID of tool.gd now belongs to the first copy: a second copy drops it.
        PckExtractResult second = PckPackageServer.Extract(package, outDir.PathJoin("second"), new[] { "pck84cs/tool.gd" });
        ExpectEqual(string.Join(",", second.DroppedUids), uidText, "a colliding UID is dropped");
        Expect(FileAccess.FileExists(outDir.PathJoin("second/pck84cs/tool.gd")) && !FileAccess.FileExists(outDir.PathJoin("second/pck84cs/tool.gd.uid")), "the .uid file of a dropped UID is not written");

        ExpectEqual(PckPackageServer.Extract(null, outDir).Error, Error.InvalidParameter, "Extract() of null returns InvalidParameter");
        ResourceUid.RemoveId(uid);
    }
}
