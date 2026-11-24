using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue85FeaturePckPackageInspector;

/// <summary>C# checks of 85-feature-pck-package-inspector. Check the same things as scripts/gd/check.gd.
/// The Inspector plugin is editor-only: these checks cover the data it edits (the storage properties <c>files</c> and
/// <c>removals</c> of a pack description), saving the description as <c>.tres</c>, "Build Pack..." and "Create Description...".
/// The UI itself is checked by hand (see the issue).</summary>
public partial class Issue85FeaturePckPackageInspectorCheck : FeatureCheckCs
{
    private const string Root = "user://85-feature-pck-package-inspector/cs/";

    protected override string CheckName => "85-feature-pck-package-inspector";

    private static void Write(string path, string text)
    {
        DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
        using FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f.StoreString(text);
    }

    protected override Task RunChecks()
    {
        Write(Root + "src/assets/a.txt", "Alpha");
        Write(Root + "src/assets/sub/c.txt", "Gamma");

        // A description as the Inspector edits it: a dropped folder keeps the relative paths, a removal is listed apart.
        var description = new PckPackage();
        Expect(description.IsDescription(), "a new PckPackage is a description (editable in the Inspector)");
        description.Set("files", new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary { { "path", "pck85cs/assets/a.txt" }, { "source", Root + "src/assets/a.txt" } },
            new Godot.Collections.Dictionary { { "path", "pck85cs/assets/sub/c.txt" }, { "source", Root + "src/assets/sub/c.txt" }, { "encrypt", false } },
            new Godot.Collections.Dictionary { { "path", "pck85cs/data.txt" }, { "data", "Data".ToUtf8Buffer() } },
        });
        description.Set("removals", new string[] { "pck85cs/old.txt" });
        ExpectEqual(string.Join(",", description.GetFilePaths()), "res://pck85cs/assets/a.txt,res://pck85cs/assets/sub/c.txt,res://pck85cs/data.txt", "files set as storage property are listed in order");
        ExpectEqual(string.Join(",", description.Get("removals").AsStringArray()), "pck85cs/old.txt", "removals set as storage property");

        // Rename a target path as the Inspector does: edit the snapshot and set it back (undo sets the old snapshot).
        Godot.Collections.Array before = description.Get("files").AsGodotArray();
        Godot.Collections.Array files = description.Get("files").AsGodotArray();
        Godot.Collections.Dictionary first = files[0].AsGodotDictionary();
        first["path"] = "pck85cs/renamed/a.txt";
        files[0] = first;
        description.Set("files", files);
        Expect(description.HasFile("res://pck85cs/renamed/a.txt") && !description.HasFile("res://pck85cs/assets/a.txt"), "rename through the files snapshot");
        description.Set("files", before);
        Expect(description.HasFile("res://pck85cs/assets/a.txt") && !description.HasFile("res://pck85cs/renamed/a.txt"), "undo: the old snapshot restores the entry");
        description.Set("files", files);

        // Save the description as text and open it again.
        string tres = Root + "dlc.tres";
        ExpectEqual(ResourceSaver.Save(description, tres), Error.Ok, "save the description as .tres");
        var loaded = ResourceLoader.Load<PckPackage>(tres, null, ResourceLoader.CacheMode.Ignore);
        Expect(loaded != null && loaded.IsDescription(), "the .tres loads as a description");
        if (loaded == null)
        {
            return Task.CompletedTask;
        }
        ExpectEqual(string.Join(",", loaded.GetFilePaths()), string.Join(",", description.GetFilePaths()), "the loaded description lists the same files");
        ExpectEqual(string.Join(",", loaded.Get("removals").AsStringArray()), "pck85cs/old.txt", "the loaded description keeps the removals");

        // "Build Pack...": build the description, the pack opens read-only.
        string pck = Root + "dlc.pck";
        ExpectEqual(PckPackageServer.Build(loaded, pck), Error.Ok, "build the description");
        PckPackage opened = PckPackageServer.Open(pck);
        Expect(opened != null && !opened.IsDescription(), "a built pack opens as a pack, not a description (read-only in the Inspector)");
        if (opened == null)
        {
            return Task.CompletedTask;
        }
        ExpectEqual(string.Join(",", opened.GetFilePaths()), string.Join(",", loaded.GetFilePaths()), "the built pack has the files of the description");
        ExpectEqual(PckPackageServer.ReadFile(opened, "res://pck85cs/renamed/a.txt").GetStringFromUtf8(), "Alpha", "a renamed file is built from its source");
        ExpectEqual(PckPackageServer.ReadFile(opened, "res://pck85cs/data.txt").GetStringFromUtf8(), "Data", "a data entry is built");

        // "Create Description...": every file of the pack becomes a from_pack entry of the base pack.
        var copy = new PckPackage();
        copy.BasePack = pck;
        copy.Alignment = opened.Alignment;
        copy.EncryptedDirectory = opened.EncryptedDirectory;
        copy.Set("files", opened.Get("files"));
        copy.Set("removals", opened.Get("removals"));
        Godot.Collections.Array fromPack = copy.Get("files").AsGodotArray();
        Expect(fromPack.Count == 3 && fromPack.All(f => f.AsGodotDictionary().ContainsKey("from_pack")), "Create Description: every file is a from_pack entry");
        string copyTres = Root + "from_pack.tres";
        ExpectEqual(ResourceSaver.Save(copy, copyTres), Error.Ok, "save the created description");
        var copyLoaded = ResourceLoader.Load<PckPackage>(copyTres, null, ResourceLoader.CacheMode.Ignore);
        Expect(copyLoaded != null && copyLoaded.BasePack == pck, "the created description keeps base_pack");
        if (copyLoaded == null)
        {
            return Task.CompletedTask;
        }
        string rebuilt = Root + "rebuilt.pck";
        ExpectEqual(PckPackageServer.Build(copyLoaded, rebuilt), Error.Ok, "build the created description");
        PckPackage rebuiltPack = PckPackageServer.Open(rebuilt);
        Expect(rebuiltPack != null && string.Join(",", rebuiltPack.GetFilePaths()) == string.Join(",", opened.GetFilePaths()), "the rebuilt pack has the same files");
        if (rebuiltPack != null)
        {
            ExpectEqual(PckPackageServer.ReadFile(rebuiltPack, "res://pck85cs/assets/sub/c.txt").GetStringFromUtf8(), "Gamma", "a from_pack file is copied from the base pack");
        }
        return Task.CompletedTask;
    }
}
