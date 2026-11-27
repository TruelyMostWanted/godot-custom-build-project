using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.FileManifests;

/// <summary>Full API test of File Manifests (FilesManifest, FilesManifestServer, FilesManifestValidation, ResourceLoader manifest calls). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Feature = "res://file-manifests/resources/";
    private const string Data = Feature + "data/";
    private const string Root = "user://file_manifests_test/";
    private const string ManifestName = "fm_test";
    private const string Prefix = "fmtest://";
    private const ulong ThreadTimeoutMsec = 20000;
    private const ulong WatchTimeoutMsec = 3000;

    private readonly List<string> _added = [];
    private readonly List<string> _removed = [];
    private readonly List<string> _changed = [];

    protected override async Task RunTests()
    {
        RemoveFolder(ProjectSettings.GlobalizePath(Root));
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestDefaults();
        TestExtensions();
        TestSampleManifest();
        TestSaveAndLoad();
        TestFilters();
        TestSingleFiles();
        TestMissingFolders();
        TestPrefixes();
        TestOrder();
        TestWildcards();
        await TestThreaded();
        TestNull();
        TestServerEdit();
        TestServerSave();
        TestServerValidate();
        await TestServerWatch();
        RemoveFolder(ProjectSettings.GlobalizePath(Root));
    }

    private static string D(string relative) => Data + relative;

    private static FilesManifest Sample()
    {
        return (FilesManifest)ResourceLoader.Load(Feature + "manifest.tres", "", ResourceLoader.CacheMode.Ignore);
    }

    private static void WriteCurves(params string[] paths)
    {
        foreach (string path in paths)
        {
            DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
            ResourceSaver.Save(new Curve(), path);
        }
    }

    private void ExpectPaths(string[] actual, string[] expected, string message)
    {
        Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");
    }

    private void TestDefaults()
    {
        var manifest = new FilesManifest();
        ExpectEqual((string)manifest.TypeHint, "", "TypeHint is empty by default");
        ExpectPaths(manifest.Extensions, [], "Extensions are empty by default");
        ExpectPaths(manifest.FolderPaths, [], "FolderPaths are empty by default");
        ExpectPaths(manifest.FilePaths, [], "FilePaths are empty by default");
        Expect(manifest.Recursive, "Recursive is true by default");
        Expect(!manifest.CreateMissingFolders, "CreateMissingFolders is false by default");
    }

    private void TestExtensions()
    {
        ExpectEqual(FilesManifest.NormalizeExtension("png"), ".png", "NormalizeExtension adds the dot");
        ExpectEqual(FilesManifest.NormalizeExtension("*.PNG"), ".png", "NormalizeExtension removes * and lowers the case");
        ExpectEqual(FilesManifest.NormalizeExtension(" .Tres "), ".tres", "NormalizeExtension trims spaces");
        ExpectEqual(FilesManifest.NormalizeExtension("tres.json"), ".tres.json", "NormalizeExtension keeps a double extension");
        ExpectEqual(FilesManifest.NormalizeExtension(""), "", "NormalizeExtension of an empty string");
        var manifest = new FilesManifest { Extensions = ["png", ".PNG", "", "*.webp"] };
        ExpectPaths(manifest.Extensions, [".png", ".webp"], "Extensions are normalized and deduplicated");
        Expect(manifest.MatchesExtension("res://x/B.PNG"), "MatchesExtension ignores case");
        Expect(!manifest.MatchesExtension("res://x/b.png.txt"), "MatchesExtension checks the end of the name");
        manifest.Extensions = [];
        Expect(manifest.MatchesExtension("res://x/b.anything"), "no extensions match every file");
    }

    private void TestSampleManifest()
    {
        FilesManifest manifest = Sample();
        ExpectPaths(manifest.Extensions, [".tres", ".svg"], "manifest.tres has two extensions");
        string[] expected = [D("a/curve.tres"), D("a/icon.svg"), D("b/gradient.tres"), D("b/sub/deep.tres")];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "GetManifestPaths: both folders, recursive, info.json left out");
        Godot.Collections.Dictionary loaded = ResourceLoader.LoadManifest(manifest);
        ExpectEqual(loaded.Count, 4, "LoadManifest loads every matching file");
        Expect(loaded[D("a/curve.tres")].As<Resource>() is Curve, "a/curve.tres is a Curve");
        Expect(loaded[D("a/icon.svg")].As<Resource>() is Texture2D, "a/icon.svg (imported) is a Texture2D");
        Expect(loaded[D("b/gradient.tres")].As<Resource>() is Gradient, "b/gradient.tres is a Gradient");
        Expect(!loaded.ContainsKey(D("b/info.json")), "b/info.json is not loaded");
        Godot.Collections.Dictionary uncached = ResourceLoader.LoadManifest(manifest, ResourceLoader.CacheMode.Ignore);
        Expect(uncached[D("a/curve.tres")].As<Resource>() != loaded[D("a/curve.tres")].As<Resource>(), "LoadManifest takes the cache mode");
    }

    private void TestSaveAndLoad()
    {
        FilesManifest manifest = Sample();
        manifest.TypeHint = "Curve";
        manifest.Recursive = false;
        manifest.FilePaths = [D("b/sub/deep.tres")];
        manifest.CreateMissingFolders = true;
        string path = Root + "copy.tres";
        ExpectEqual(ResourceSaver.Save(manifest, path), Error.Ok, "the manifest saves as .tres");
        var copy = (FilesManifest)ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((string)copy.TypeHint, "Curve", "TypeHint survives saving");
        ExpectPaths(copy.Extensions, manifest.Extensions, "Extensions survive saving");
        ExpectPaths(copy.FolderPaths, manifest.FolderPaths, "FolderPaths survive saving");
        ExpectPaths(copy.FilePaths, manifest.FilePaths, "FilePaths survive saving");
        Expect(!copy.Recursive, "Recursive survives saving");
        Expect(copy.CreateMissingFolders, "CreateMissingFolders survives saving");
    }

    private void TestFilters()
    {
        FilesManifest manifest = Sample();
        manifest.Recursive = false;
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres"), D("a/icon.svg"), D("b/gradient.tres")], "Recursive = false skips b/sub");
        manifest.Recursive = true;
        manifest.TypeHint = "Curve";
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres"), D("b/sub/deep.tres")], "TypeHint Curve keeps only curves");
        manifest.TypeHint = "Texture2D";
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/icon.svg")], "TypeHint Texture2D keeps the imported texture");
        manifest.TypeHint = "";
        manifest.Extensions = [];
        string[] all = ResourceLoader.GetManifestPaths(manifest);
        Expect(all.Contains(D("b/info.json")), "no extensions: every recognized file, also b/info.json");
        ExpectEqual(all.Length, 5, "no extensions: five files");
    }

    private void TestSingleFiles()
    {
        var manifest = new FilesManifest
        {
            Extensions = ["svg"],
            FolderPaths = [D("a")],
            FilePaths = [D("b/info.json"), D("a/icon.svg"), D("b/sub/../gradient.tres")],
        };
        string[] expected = [D("a/icon.svg"), D("b/info.json"), D("b/gradient.tres")];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "single files are added once, normalized, regardless of the extensions");
        manifest.FilePaths = [D("missing.tres"), D("a/curve.tres")];
        expected = [D("a/icon.svg"), D("a/curve.tres")];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "a missing single file is skipped");
        var relative = new FilesManifest { FolderPaths = ["file-manifests/resources/data/a/"], Extensions = ["tres"] };
        ExpectPaths(ResourceLoader.GetManifestPaths(relative), [D("a/curve.tres")], "relative folders are relative to res://");
    }

    private void TestMissingFolders()
    {
        string missing = Root + "missing/deeper";
        var manifest = new FilesManifest { FolderPaths = [missing, D("a")], Extensions = ["tres"] };
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres")], "a missing folder is skipped");
        Expect(!DirAccess.DirExistsAbsolute(missing), "the missing folder is not created by default");
        manifest.CreateMissingFolders = true;
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [D("a/curve.tres")], "with CreateMissingFolders the other folders are still listed");
        Expect(DirAccess.DirExistsAbsolute(missing), "CreateMissingFolders creates the folder in user://");
    }

    private void TestPrefixes()
    {
        WriteCurves(Root + "prefixed/data/c0.tres", Root + "prefixed/data/c1.tres");
        PathPrefixServer.RegisterPrefixFolder(Prefix, Root + "prefixed");
        var manifest = new FilesManifest { FolderPaths = [Prefix + "data"] };
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), [Prefix + "data/c0.tres", Prefix + "data/c1.tres"], "folders with a custom prefix");
        Godot.Collections.Dictionary loaded = ResourceLoader.LoadManifest(manifest);
        Expect(loaded[Prefix + "data/c1.tres"].As<Resource>() is Curve, "the prefixed path is the key");
        manifest.FolderPaths = [Root + "prefixed/data/"];
        ExpectEqual(ResourceLoader.GetManifestPaths(manifest).Length, 2, "user:// folders work, a trailing slash is accepted");
        PathPrefixServer.UnregisterPrefix(Prefix);
    }

    private void TestOrder()
    {
        var manifest = new FilesManifest
        {
            Extensions = ["tres", "svg"],
            FolderPaths = [D("b"), D("a")],
            FilePaths = [D("b/info.json"), D("a/curve.tres")],
        };
        string[] expected = [D("b/gradient.tres"), D("b/sub/deep.tres"), D("a/curve.tres"), D("a/icon.svg"), D("b/info.json")];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "folders in list order, then files in list order, duplicates keep their first position");
        string[] loadedKeys = ResourceLoader.LoadManifest(manifest).Keys.Select(key => key.AsString()).ToArray();
        ExpectPaths(loadedKeys, expected, "LoadManifest keeps the load order");
        manifest.FolderPaths = [D("a"), D("b")];
        ExpectEqual(ResourceLoader.GetManifestPaths(manifest)[0], D("a/curve.tres"), "reordering FolderPaths changes the load order");
        WriteCurves(Root + "order/z.tres", Root + "order/m/x.tres");
        var sorted = new FilesManifest { FolderPaths = [Root + "order"] };
        ExpectPaths(ResourceLoader.GetManifestPaths(sorted), [Root + "order/m/x.tres", Root + "order/z.tres"], "files of a folder are sorted by path, subfolders included");
    }

    private void TestWildcards()
    {
        WriteCurves(Root + "mods/b/tr/x.tres", Root + "mods/a/tr/y.tres", Root + "mods/a_extra/other/z.tres", Root + "mods/c/tr/sub/w.tres");
        var manifest = new FilesManifest { FolderPaths = [Root + "mods/*/tr"] };
        string[] expected = [Root + "mods/a/tr/y.tres", Root + "mods/b/tr/x.tres", Root + "mods/c/tr/sub/w.tres"];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "mods/*/tr: one level, sorted by name, mods without tr skipped");
        manifest.FolderPaths = [Root + "mods/a*"];
        expected = [Root + "mods/a/tr/y.tres", Root + "mods/a_extra/other/z.tres"];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "a pattern inside a segment (a*)");
        manifest.FolderPaths = [Root + "mods/*/nothing"];
        ExpectEqual(ResourceLoader.GetManifestPaths(manifest).Length, 0, "no match is not an error");
        PathPrefixServer.RegisterPrefixFolder(Prefix, Root + "mods");
        manifest.FolderPaths = [Prefix + "*/tr"];
        expected = [Prefix + "a/tr/y.tres", Prefix + "b/tr/x.tres", Prefix + "c/tr/sub/w.tres"];
        ExpectPaths(ResourceLoader.GetManifestPaths(manifest), expected, "wildcards behind a custom prefix");
        PathPrefixServer.UnregisterPrefix(Prefix);
    }

    private async Task TestThreaded()
    {
        FilesManifest manifest = Sample();
        string[] paths = ResourceLoader.GetManifestPaths(manifest);
        ExpectEqual(ResourceLoader.LoadThreadedRequestManifest(manifest, false, ResourceLoader.CacheMode.Ignore), Error.Ok, "LoadThreadedRequestManifest starts every request");
        var pending = new List<string>(paths);
        ulong start = Time.GetTicksMsec();
        while (pending.Count > 0 && Time.GetTicksMsec() - start < ThreadTimeoutMsec)
        {
            foreach (string path in pending.ToArray())
            {
                var progress = new Godot.Collections.Array();
                ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path, progress);
                if (status != ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    pending.Remove(path);
                }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(pending.Count == 0, "every threaded request finishes");
        int loaded = 0;
        foreach (string path in paths)
        {
            if (ResourceLoader.LoadThreadedGet(path) != null)
            {
                loaded++;
            }
        }
        ExpectEqual(loaded, paths.Length, "LoadThreadedGet returns every resource");
    }

    private void TestNull()
    {
        ExpectPaths(ResourceLoader.GetManifestPaths(null), [], "GetManifestPaths(null) is empty");
        ExpectEqual(ResourceLoader.LoadManifest(null).Count, 0, "LoadManifest(null) is empty");
        ExpectEqual(ResourceLoader.LoadThreadedRequestManifest(null), Error.InvalidParameter, "LoadThreadedRequestManifest(null) fails");
    }

    private void TestServerEdit()
    {
        FilesManifestServer.Singleton.ManifestChanged += OnManifestChanged;
        ExpectEqual(FilesManifestServer.RegisterManifest(ManifestName, new FilesManifest()), Error.Ok, "RegisterManifest");
        Expect(_changed.SequenceEqual([ManifestName]), "ManifestChanged is emitted when a manifest is registered");
        Expect(FilesManifestServer.HasManifest(ManifestName), "HasManifest finds the manifest");
        Expect(FilesManifestServer.GetManifestNames().Contains(ManifestName), "GetManifestNames lists the manifest");
        FilesManifest manifest = FilesManifestServer.GetManifest(ManifestName);
        Expect(manifest != null, "GetManifest returns the manifest");
        ExpectEqual(FilesManifestServer.AddFolder(ManifestName, D("a")), Error.Ok, "AddFolder");
        ExpectEqual(FilesManifestServer.AddFolder(ManifestName, D("a")), Error.AlreadyExists, "AddFolder twice: AlreadyExists");
        ExpectEqual(FilesManifestServer.AddExtension(ManifestName, "TRES"), Error.Ok, "AddExtension");
        ExpectEqual(FilesManifestServer.AddExtension(ManifestName, ".tres"), Error.AlreadyExists, "AddExtension twice: AlreadyExists");
        ExpectEqual(FilesManifestServer.AddFile(ManifestName, D("b/gradient.tres")), Error.Ok, "AddFile");
        ExpectEqual(FilesManifestServer.AddFile(ManifestName, D("b/gradient.tres")), Error.AlreadyExists, "AddFile twice: AlreadyExists");
        ExpectPaths(manifest.Extensions, [".tres"], "the edits change the manifest");
        ExpectPaths(FilesManifestServer.GetFilePaths(ManifestName), [D("a/curve.tres"), D("b/gradient.tres")], "GetFilePaths lists folders, then files");
        Expect(_changed.Count > 1, "ManifestChanged is emitted for every edit");
        FilesManifestServer.Singleton.ManifestChanged -= OnManifestChanged;
        ExpectEqual(FilesManifestServer.RemoveFile(ManifestName, D("b/gradient.tres")), Error.Ok, "RemoveFile");
        ExpectEqual(FilesManifestServer.RemoveFile(ManifestName, D("b/gradient.tres")), Error.DoesNotExist, "RemoveFile of a missing file fails");
        ExpectEqual(FilesManifestServer.RemoveExtension(ManifestName, "tres"), Error.Ok, "RemoveExtension");
        ExpectEqual(FilesManifestServer.RemoveExtension(ManifestName, "tres"), Error.DoesNotExist, "RemoveExtension of a missing extension fails");
        ExpectEqual(FilesManifestServer.RemoveFolder(ManifestName, D("a")), Error.Ok, "RemoveFolder");
        ExpectEqual(FilesManifestServer.RemoveFolder(ManifestName, D("a")), Error.DoesNotExist, "RemoveFolder of a missing folder fails");
        ExpectEqual(FilesManifestServer.GetFilePaths(ManifestName).Length, 0, "the manifest is empty again");
        Expect(FilesManifestServer.GetManifest("fm_unknown") == null, "GetManifest of an unknown name is null");
        Expect(!FilesManifestServer.HasManifest("fm_unknown"), "HasManifest of an unknown name is false");
    }

    private void TestServerSave()
    {
        FilesManifestServer.AddFolder(ManifestName, D("a"));
        FilesManifestServer.AddExtension(ManifestName, "tres");
        string path = Root + "server.tres";
        ExpectEqual(FilesManifestServer.SaveManifest(ManifestName, path), Error.Ok, "SaveManifest with a path");
        var loaded = (FilesManifest)ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(loaded.FolderPaths.Length, 1, "the saved manifest has the folder");
        FilesManifestServer.AddExtension(ManifestName, "svg");
        ExpectEqual(FilesManifestServer.SaveManifest(ManifestName), Error.Ok, "SaveManifest without a path writes back to the last file");
        ExpectEqual(FilesManifestServer.UnregisterManifest(ManifestName), Error.Ok, "UnregisterManifest");
        Expect(!FilesManifestServer.HasManifest(ManifestName), "the manifest is gone");
        ExpectEqual(FilesManifestServer.UnregisterManifest(ManifestName), Error.DoesNotExist, "UnregisterManifest of an unknown name fails");
        ExpectEqual(FilesManifestServer.RegisterManifestFile(ManifestName, path), Error.Ok, "RegisterManifestFile");
        FilesManifest fromFile = FilesManifestServer.GetManifest(ManifestName);
        ExpectPaths(fromFile.Extensions, [".tres", ".svg"], "RegisterManifestFile loads the manifest");
        FilesManifestServer.UnregisterManifest(ManifestName);
    }

    private void TestServerValidate()
    {
        var manifest = new FilesManifest
        {
            FolderPaths = [D("a"), Root + "absent/folder", Root + "absent/*/tr", Root + "mods/*/tr"],
            FilePaths = [D("missing.tres")],
            Extensions = ["tres", "nosuchext"],
        };
        FilesManifestServer.RegisterManifest(ManifestName, manifest);
        FilesManifestValidation result = FilesManifestServer.Validate(ManifestName);
        Expect(!result.Valid, "Validate: the manifest is not valid");
        ExpectEqual(result.Error, Error.FileNotFound, "Validate: the error is FileNotFound");
        ExpectPaths(result.MissingFolders, [Root + "absent/folder", Root + "absent/*/tr"], "Validate: a wildcard folder is missing only if its base is");
        ExpectPaths(result.MissingFiles, [D("missing.tres")], "Validate: MissingFiles");
        ExpectPaths(result.UnknownExtensions, [".nosuchext"], "Validate: UnknownExtensions");
        ExpectEqual(FilesManifestServer.CreateMissingFolders(ManifestName), Error.Ok, "CreateMissingFolders");
        Expect(DirAccess.DirExistsAbsolute(Root + "absent/folder"), "CreateMissingFolders creates a folder");
        Expect(DirAccess.DirExistsAbsolute(Root + "absent"), "CreateMissingFolders creates the base of a wildcard folder");
        FilesManifestServer.RemoveFile(ManifestName, D("missing.tres"));
        FilesManifestServer.RemoveExtension(ManifestName, "nosuchext");
        Expect(FilesManifestServer.Validate(ManifestName).Valid, "Validate: the manifest is valid after the fixes");
        FilesManifestServer.UnregisterManifest(ManifestName);
    }

    private async Task TestServerWatch()
    {
        string watched = Root + "watched/";
        WriteCurves(watched + "a.tres");
        FilesManifestServer.RegisterManifest(ManifestName, new FilesManifest { FolderPaths = [watched] });
        FilesManifestServer.Singleton.FileAdded += OnFileAdded;
        FilesManifestServer.Singleton.FileRemoved += OnFileRemoved;
        FilesManifestServer.WatchInterval = 0.1f;
        ExpectEqual(FilesManifestServer.WatchInterval, 0.1f, "WatchInterval is set");
        Expect(!FilesManifestServer.IsWatching(ManifestName), "IsWatching is false at first");
        ExpectEqual(FilesManifestServer.SetWatching(ManifestName, true), Error.Ok, "SetWatching");
        Expect(FilesManifestServer.IsWatching(ManifestName), "IsWatching is true");
        WriteCurves(watched + "b.tres");
        ulong start = Time.GetTicksMsec();
        while (_added.Count == 0 && Time.GetTicksMsec() - start < WatchTimeoutMsec)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(_added.SequenceEqual([watched + "b.tres"]), "FileAdded is emitted by the automatic poll");
        DirAccess.RemoveAbsolute(watched + "b.tres");
        FilesManifestServer.Poll();
        Expect(_removed.SequenceEqual([watched + "b.tres"]), "Poll emits FileRemoved");
        WriteCurves(watched + "c.tres");
        ExpectEqual(FilesManifestServer.Refresh(ManifestName), Error.Ok, "Refresh");
        Expect(_added.SequenceEqual([watched + "b.tres", watched + "c.tres"]), "Refresh reports the differences of a watched manifest");
        ExpectEqual(FilesManifestServer.GetFilePaths(ManifestName).Length, 2, "Refresh updates the cached file list");
        ExpectEqual(FilesManifestServer.SetWatching(ManifestName, false), Error.Ok, "SetWatching(false)");
        Expect(!FilesManifestServer.IsWatching(ManifestName), "IsWatching is false again");
        FilesManifestServer.Singleton.FileAdded -= OnFileAdded;
        FilesManifestServer.Singleton.FileRemoved -= OnFileRemoved;
        FilesManifestServer.UnregisterManifest(ManifestName);
    }

    private void OnManifestChanged(string manifestName) => _changed.Add(manifestName);

    private void OnFileAdded(string manifestName, string path) => _added.Add(path);

    private void OnFileRemoved(string manifestName, string path) => _removed.Add(path);

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
