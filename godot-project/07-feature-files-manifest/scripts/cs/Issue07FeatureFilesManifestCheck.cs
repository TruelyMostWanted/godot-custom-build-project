// Written before the engine build of #7; run after the merge.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GDict = Godot.Collections.Dictionary;

namespace TruelyInteractive.Issue07FeatureFilesManifest;

/// <summary>C# checks of 07-feature-files-manifest. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue07FeatureFilesManifestCheck : FeatureCheckCs
{
    private const string Feature = "res://07-feature-files-manifest/resources/";
    private const string Data = Feature + "data/";
    private const string Root = "user://07-feature-files-manifest/cs/";
    private const string Prefix = "fm07cs://";
    private const int PerfCount = 1000;
    private const ulong ThreadTimeoutMsec = 20000;

    protected override string CheckName => "07-feature-files-manifest";

    protected override async Task RunChecks()
    {
        Cleanup();
        DirAccess.MakeDirRecursiveAbsolute(Root);
        CheckApi();
        CheckNormalization();
        CheckManifestFile();
        CheckFilters();
        CheckSingleFiles();
        CheckMissingFolders();
        CheckPrefixes();
        await CheckThreaded();
        CheckNull();
        await CheckPerformance();
        Cleanup();
    }

    private static string D(string relative) => Data + relative;

    private static FilesManifest LoadManifestFile()
        => ResourceLoader.Load(Feature + "manifest.tres", "", ResourceLoader.CacheMode.Ignore) as FilesManifest;

    private bool ExpectPaths(string[] actual, string[] expected, string message)
        => Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");

    private static Variant Get(GDict dict, string key) => dict.TryGetValue(key, out Variant value) ? value : default;

    private void CheckApi()
    {
        Expect(ClassDB.ClassExists("FilesManifest"), "ClassDB knows FilesManifest");
        Expect(ClassDB.IsParentClass("FilesManifest", "Resource"), "FilesManifest is a Resource");
        foreach (string method in new[] { "get_manifest_paths", "load_manifest", "load_threaded_request_manifest" })
        {
            Expect(ClassDB.ClassHasMethod("ResourceLoader", method), $"ResourceLoader has {method}()");
        }
        var manifest = new FilesManifest();
        ExpectEqual(manifest.TypeHint.ToString(), "", "default TypeHint is empty");
        ExpectEqual(manifest.Extensions.Length, 0, "default Extensions are empty");
        ExpectEqual(manifest.Recursive, true, "default Recursive is true");
        ExpectEqual(manifest.CreateNonExistingFolders, false, "default CreateNonExistingFolders is false");
        var stored = new HashSet<string>();
        foreach (GDict property in manifest.GetPropertyList())
        {
            if (((int)property["usage"] & (int)PropertyUsageFlags.Storage) != 0)
            {
                stored.Add((string)property["name"]);
            }
        }
        foreach (string name in new[] { "type_hint", "extensions", "folder_paths", "file_paths", "recursive", "create_non_existing_folders" })
        {
            Expect(stored.Contains(name), $"{name} is stored (PROPERTY_USAGE_STORAGE)");
        }
    }

    private void CheckNormalization()
    {
        ExpectEqual(FilesManifest.NormalizeExtension("png"), ".png", "NormalizeExtension(png)");
        ExpectEqual(FilesManifest.NormalizeExtension("*.PNG"), ".png", "NormalizeExtension(*.PNG)");
        ExpectEqual(FilesManifest.NormalizeExtension(" .Tres "), ".tres", "NormalizeExtension( .Tres )");
        ExpectEqual(FilesManifest.NormalizeExtension("tres.json"), ".tres.json", "NormalizeExtension(tres.json)");
        ExpectEqual(FilesManifest.NormalizeExtension(""), "", "NormalizeExtension() of an empty string");

        var manifest = new FilesManifest { Extensions = ["png", ".PNG", "", "*.webp"] };
        ExpectPaths(manifest.Extensions, [".png", ".webp"], "Extensions are normalized and deduplicated");
        Expect(manifest.MatchesExtension("res://x/B.PNG"), "MatchesExtension() ignores case");
        Expect(!manifest.MatchesExtension("res://x/b.png.txt"), "MatchesExtension() checks the end of the name");
        manifest.Extensions = [];
        Expect(manifest.MatchesExtension("res://x/b.anything"), "no extensions match every file");
    }

    private void CheckManifestFile()
    {
        FilesManifest manifest = LoadManifestFile();
        if (!Expect(manifest != null, "manifest.tres loads as FilesManifest"))
        {
            return;
        }
        ExpectPaths(manifest.Extensions, [".tres", ".svg"], "manifest.tres has two extensions");
        ExpectEqual(manifest.FolderPaths.Length, 2, "manifest.tres has two folders");

        string[] expected = [D("a/curve.tres"), D("a/icon.svg"), D("b/gradient.tres"), D("b/sub/deep.tres")];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "GetManifestPaths(): both folders, both extensions, recursive, info.json left out");

        GDict loaded = ResourceLoader.LoadManifest(manifest);
        ExpectEqual(loaded.Count, 4, "LoadManifest() loads every matching file");
        Expect(Get(loaded, D("a/curve.tres")).Obj is Curve, "a/curve.tres is a Curve");
        Expect(Get(loaded, D("a/icon.svg")).Obj is Texture2D, "a/icon.svg (imported) is a Texture2D");
        Expect(Get(loaded, D("b/gradient.tres")).Obj is Gradient, "b/gradient.tres is a Gradient");
        Expect(Get(loaded, D("b/sub/deep.tres")).Obj is Curve, "b/sub/deep.tres is a Curve");
        Expect(!loaded.ContainsKey(D("b/info.json")), "b/info.json is not loaded");

        // Saved and loaded again, the manifest keeps all properties.
        manifest.TypeHint = "Curve";
        manifest.Recursive = false;
        manifest.FilePaths = [D("b/sub/deep.tres")];
        manifest.CreateNonExistingFolders = true;
        string copyPath = Root + "copy.tres";
        ExpectEqual(ResourceSaver.Save(manifest, copyPath), Error.Ok, "the manifest saves as .tres");
        var copy = ResourceLoader.Load(copyPath, "", ResourceLoader.CacheMode.Ignore) as FilesManifest;
        if (Expect(copy != null, "the saved manifest loads"))
        {
            ExpectEqual(copy.TypeHint.ToString(), "Curve", "TypeHint survives saving");
            ExpectPaths(copy.Extensions, manifest.Extensions, "Extensions survive saving");
            ExpectPaths(copy.FolderPaths, manifest.FolderPaths, "FolderPaths survive saving");
            ExpectPaths(copy.FilePaths, manifest.FilePaths, "FilePaths survive saving");
            ExpectEqual(copy.Recursive, false, "Recursive survives saving");
            ExpectEqual(copy.CreateNonExistingFolders, true, "CreateNonExistingFolders survives saving");
        }
    }

    private void CheckFilters()
    {
        FilesManifest manifest = LoadManifestFile();
        manifest.Recursive = false;
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres"), D("a/icon.svg"), D("b/gradient.tres")], "Recursive = false skips b/sub");

        manifest.Recursive = true;
        manifest.TypeHint = "Curve";
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres"), D("b/sub/deep.tres")], "TypeHint Curve keeps only curves");
        ExpectEqual(ResourceLoader.LoadManifest(manifest).Count, 2, "LoadManifest() with TypeHint Curve loads two curves");

        manifest.TypeHint = "Texture2D";
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/icon.svg")], "TypeHint Texture2D keeps the imported texture");

        manifest.TypeHint = "";
        manifest.Extensions = [];
        string[] all = ResourceLoader.GetManifestPaths(manifest);
        Expect(all.Contains(D("b/info.json")), "no extensions: every recognized file, also b/info.json");
        ExpectEqual(all.Length, 5, "no extensions: five files");
    }

    private void CheckSingleFiles()
    {
        var manifest = new FilesManifest
        {
            Extensions = ["svg"],
            FolderPaths = [D("a")],
            FilePaths = [D("b/info.json"), D("a/icon.svg"), D("b/sub/../gradient.tres")],
        };
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/icon.svg"), D("b/info.json"), D("b/gradient.tres")], "single files are added once, normalized, regardless of the extensions");

        manifest.FilePaths = [D("missing.tres"), D("a/curve.tres")];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/icon.svg"), D("a/curve.tres")], "a missing single file is skipped with an error");

        var relative = new FilesManifest
        {
            FolderPaths = ["07-feature-files-manifest/resources/data/a/"],
            Extensions = ["tres"],
        };
        ExpectPaths(ResourceLoader.GetManifestPaths(relative), [D("a/curve.tres")], "relative folders are relative to res://");
    }

    private void CheckMissingFolders()
    {
        string missing = Root + "missing/deeper";
        var manifest = new FilesManifest
        {
            FolderPaths = [missing, D("a")],
            Extensions = ["tres"],
        };
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres")], "a missing folder is skipped with an error");
        Expect(!DirAccess.DirExistsAbsolute(missing), "the missing folder is not created by default");

        manifest.CreateNonExistingFolders = true;
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres")], "with CreateNonExistingFolders the other folders are still listed");
        Expect(DirAccess.DirExistsAbsolute(missing), "CreateNonExistingFolders creates the folder in user://");
        ExpectEqual(ResourceLoader.LoadManifest(manifest).Count, 1, "the new folder is empty");

        if (OS.HasFeature("template"))
        {
            // Exported build: res:// is read-only and cannot get new folders.
            string resMissing = Feature + "not_exported_folder";
            var resManifest = new FilesManifest { FolderPaths = [resMissing], CreateNonExistingFolders = true };
            ExpectEqual(ResourceLoader.GetManifestPaths(resManifest).Length, 0, "exported: a missing res:// folder gives no paths");
            Expect(!DirAccess.DirExistsAbsolute(resMissing), "exported: res:// folders are not created");
        }
    }

    private void CheckPrefixes()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root + "prefixed/data");
        for (int i = 0; i < 3; i++)
        {
            ResourceSaver.Save(new Curve(), Root + $"prefixed/data/c{i}.tres");
        }
        ExpectEqual(PathPrefixServer.RegisterPrefixFolder(Prefix, Root + "prefixed"), Error.Ok, $"RegisterPrefixFolder({Prefix})");

        var manifest = new FilesManifest { FolderPaths = [Prefix + "data"] };
        string[] expected = [Prefix + "data/c0.tres", Prefix + "data/c1.tres", Prefix + "data/c2.tres"];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "folders with a custom prefix");
        GDict loaded = ResourceLoader.LoadManifest(manifest, ResourceLoader.CacheMode.Ignore);
        ExpectEqual(loaded.Count, 3, "LoadManifest() with a custom prefix");
        Expect(Get(loaded, Prefix + "data/c1.tres").Obj is Curve, "the prefixed path is the key");

        // user:// itself works too.
        var userManifest = new FilesManifest { FolderPaths = [Root + "prefixed/data/"] };
        ExpectEqual(ResourceLoader.GetManifestPaths(userManifest).Length, 3, "user:// folders (trailing slash is accepted)");

        if (PathPrefixServer.HasPrefix("exec://") && PathPrefixServer.IsPrefixAvailable("exec://"))
        {
            var execManifest = new FilesManifest { FolderPaths = ["exec://"], Recursive = false, Extensions = ["fm07_no_such_extension"] };
            ExpectEqual(ResourceLoader.GetManifestPaths(execManifest).Length, 0, "exec:// folders resolve (no matching files)");
        }

        PathPrefixServer.UnregisterPrefix(Prefix);
    }

    private async Task CheckThreaded()
    {
        FilesManifest manifest = LoadManifestFile();
        string[] paths = ResourceLoader.GetManifestPaths(manifest);
        ExpectEqual(ResourceLoader.LoadThreadedRequestManifest(manifest, false, ResourceLoader.CacheMode.Ignore), Error.Ok, "LoadThreadedRequestManifest() starts every request");

        bool known = true;
        foreach (string path in paths)
        {
            ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path);
            if (status == ResourceLoader.ThreadLoadStatus.InvalidResource || status == ResourceLoader.ThreadLoadStatus.Failed)
            {
                known = false;
            }
        }
        Expect(known, "LoadThreadedGetStatus() knows every path right after the request");

        ulong start = Time.GetTicksMsec();
        var pending = new List<string>(paths);
        bool progressSeen = true;
        while (pending.Count > 0 && Time.GetTicksMsec() - start < ThreadTimeoutMsec)
        {
            foreach (string path in pending.ToArray())
            {
                var progress = new Godot.Collections.Array();
                ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path, progress);
                if (progress.Count != 1)
                {
                    progressSeen = false;
                }
                if (status != ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    pending.Remove(path);
                }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(pending.Count == 0, "every threaded request finishes");
        Expect(progressSeen, "LoadThreadedGetStatus() reports progress for every path");

        int valid = paths.Count(path => ResourceLoader.LoadThreadedGet(path) != null);
        ExpectEqual(valid, paths.Length, "LoadThreadedGet() returns every resource");
    }

    private void CheckNull()
    {
        ExpectEqual(ResourceLoader.GetManifestPaths(null).Length, 0, "GetManifestPaths(null) is empty");
        ExpectEqual(ResourceLoader.LoadManifest(null).Count, 0, "LoadManifest(null) is empty");
        ExpectEqual(ResourceLoader.LoadThreadedRequestManifest(null), Error.InvalidParameter, "LoadThreadedRequestManifest(null) fails");
    }

    private async Task CheckPerformance()
    {
        string folder = Root + "perf";
        DirAccess.MakeDirRecursiveAbsolute(folder);
        var curve = new Curve();
        for (int i = 0; i < PerfCount; i++)
        {
            ResourceSaver.Save(curve, folder.PathJoin($"c{i:D4}.tres"));
        }

        var manifest = new FilesManifest { FolderPaths = [folder], Extensions = ["tres"] };

        ulong t0 = Time.GetTicksUsec();
        string[] paths = ResourceLoader.GetManifestPaths(manifest);
        ulong t1 = Time.GetTicksUsec();
        GDict loaded = ResourceLoader.LoadManifest(manifest, ResourceLoader.CacheMode.Ignore);
        ulong t2 = Time.GetTicksUsec();
        ExpectEqual(paths.Length, PerfCount, $"{PerfCount} files listed");
        ExpectEqual(loaded.Count, PerfCount, $"{PerfCount} files loaded blocking");

        ResourceLoader.LoadThreadedRequestManifest(manifest, false, ResourceLoader.CacheMode.Ignore);
        int done = paths.Count(path => ResourceLoader.LoadThreadedGet(path) != null);
        ulong t3 = Time.GetTicksUsec();
        ExpectEqual(done, PerfCount, $"{PerfCount} files loaded threaded");
        GD.Print($"  INFO  {PerfCount} files: list {(t1 - t0) / 1000.0:F1} ms, LoadManifest {(t2 - t1) / 1000.0:F1} ms, threaded (one request per file) {(t3 - t2) / 1000.0:F1} ms");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Cleanup()
    {
        if (PathPrefixServer.HasPrefix(Prefix))
        {
            PathPrefixServer.UnregisterPrefix(Prefix);
        }
        RemoveRecursive(ProjectSettings.GlobalizePath(Root));
    }

    private static void RemoveRecursive(string absolute)
    {
        using DirAccess dir = DirAccess.Open(absolute);
        if (dir == null)
        {
            return;
        }
        foreach (string sub in dir.GetDirectories())
        {
            RemoveRecursive(absolute.PathJoin(sub));
        }
        foreach (string file in dir.GetFiles())
        {
            dir.Remove(file);
        }
        DirAccess.RemoveAbsolute(absolute);
    }
}
