using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.PathPrefixes;

/// <summary>Full API test of Path Prefixes (PathPrefixServer, PathPrefix, PathInfo). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private static readonly string[] EnginePrefixes = ["res://", "user://", "uid://", "pipe://", "exec://"];
    private static readonly string[] DefaultPrefixes = ["appdata://", "config://", "cache://", "temp://"];
    private const string PackPath = "user://pp_test.pck";
    private const string PackFolder = "res://pp_test_pack";
    // A feature tag that is never true.
    private const string MissingTag = "pp_no_such_feature";

    private readonly List<string> _registered = [];
    private readonly List<string> _unregistered = [];

    protected override Task RunTests()
    {
        TestEnginePrefixes();
        TestDefaultPrefixes();
        TestOsPathPrefixes();
        TestSignals();
        TestFolderPrefix();
        TestCallablePrefix();
        TestSubclassPrefix();
        TestPlatforms();
        TestSystemDirPrefix();
        TestResolve();
        TestResolveSafe();
        TestResourceReferences();
        TestEnginePath();
        // Last: mounting a pack switches the res:// access of this run.
        TestPack();
        return Task.CompletedTask;
    }

    private void TestEnginePrefixes()
    {
        string[] prefixes = PathPrefixServer.GetPrefixes();
        foreach (string prefix in EnginePrefixes)
        {
            Expect(prefixes.Contains(prefix), $"GetPrefixes lists {prefix}");
            Expect(PathPrefixServer.HasPrefix(prefix), $"HasPrefix finds {prefix}");
            Expect(PathPrefixServer.IsPrefixLocked(prefix), $"IsPrefixLocked: {prefix} is locked");
            ExpectEqual(PathPrefixServer.RegisterPrefixFolder(prefix, "user://x"), Error.Locked, $"RegisterPrefixFolder({prefix}) fails");
            ExpectEqual(PathPrefixServer.UnregisterPrefix(prefix), Error.Locked, $"UnregisterPrefix({prefix}) fails");
        }
        ExpectEqual(PathPrefixServer.Globalize("user://a.txt"), ProjectSettings.GlobalizePath("user://a.txt"), "Globalize(user://) is the user folder");
        string globalPath = PathPrefixServer.Globalize("user://folder/file.txt");
        ExpectEqual(PathPrefixServer.Localize(globalPath), "user://folder/file.txt", "Localize reverses Globalize");
        ExpectEqual(PathPrefixServer.Globalize("no_prefix/a.txt"), "no_prefix/a.txt", "Globalize keeps a path without prefix");
        ExpectEqual(PathPrefixServer.Localize("/nowhere/a.txt"), "", "Localize returns an empty string outside all prefixes");
        Expect(!PathPrefixServer.HasPrefix("nope://"), "HasPrefix is false for an unknown prefix");
        Expect(!PathPrefixServer.IsPrefixAvailable("nope://"), "IsPrefixAvailable is false for an unknown prefix");
        Expect(!PathPrefixServer.IsPrefixReadOnly("user://"), "IsPrefixReadOnly is false for user://");
        ExpectEqual(ProjectSettings.GlobalizePath("exec://a.txt"), PathPrefixServer.Globalize("exec://a.txt"), "ProjectSettings.GlobalizePath knows exec://");
    }

    private void TestDefaultPrefixes()
    {
        Expect((bool)ProjectSettings.GetSetting("filesystem/path_prefixes/defaults", false), "filesystem/path_prefixes/defaults is on");
        foreach (string prefix in DefaultPrefixes)
        {
            Expect(PathPrefixServer.HasPrefix(prefix), $"{prefix} is registered");
            Expect(!PathPrefixServer.IsPrefixLocked(prefix), $"{prefix} is not locked");
        }
        ExpectEqual(PathPrefixServer.RegisterPrefixFolder("appdata://", "user://pp_appdata"), Error.Ok, "a default prefix can be replaced");
        ExpectEqual(PathPrefixServer.Globalize("appdata://a"), ProjectSettings.GlobalizePath("user://pp_appdata/a"), "the replacement is used");
    }

    private void TestOsPathPrefixes()
    {
        var folders = new Dictionary<PathPrefixServer.OSPath, string>
        {
            [PathPrefixServer.OSPath.Data] = OS.GetDataDir(),
            [PathPrefixServer.OSPath.Config] = OS.GetConfigDir(),
            [PathPrefixServer.OSPath.Cache] = OS.GetCacheDir(),
            [PathPrefixServer.OSPath.Temp] = OS.GetTempDir(),
        };
        foreach (KeyValuePair<PathPrefixServer.OSPath, string> entry in folders)
        {
            PathPrefix prefix = PathPrefixServer.CreateOsPathPrefix(entry.Key);
            ExpectEqual(PathPrefixServer.RegisterPrefix("ppos://", prefix), Error.Ok, $"RegisterPrefix with CreateOsPathPrefix({(int)entry.Key})");
            string globalPath = PathPrefixServer.Globalize("ppos://x");
            ExpectEqual(globalPath, entry.Value.Replace("\\", "/").PathJoin("x"), $"OS path {(int)entry.Key} maps to its OS folder");
        }
        PathPrefixServer.UnregisterPrefix("ppos://");
    }

    private void TestSignals()
    {
        PathPrefixServer.PrefixRegistered += OnPrefixRegistered;
        PathPrefixServer.PrefixUnregistered += OnPrefixUnregistered;
        PathPrefixServer.RegisterPrefixFolder("ppsig://", "user://pp_sig");
        PathPrefixServer.UnregisterPrefix("ppsig://");
        PathPrefixServer.PrefixRegistered -= OnPrefixRegistered;
        PathPrefixServer.PrefixUnregistered -= OnPrefixUnregistered;
        Expect(_registered.SequenceEqual(["ppsig://"]), "PrefixRegistered is emitted with the prefix");
        Expect(_unregistered.SequenceEqual(["ppsig://"]), "PrefixUnregistered is emitted with the prefix");
        ExpectEqual(PathPrefixServer.UnregisterPrefix("ppsig://"), Error.DoesNotExist, "UnregisterPrefix of an unknown prefix fails");
        Expect(PathPrefixServer.RegisterPrefixFolder("Bad://", "user://x") != Error.Ok, "RegisterPrefixFolder rejects uppercase letters");
        Expect(PathPrefixServer.RegisterPrefixFolder("http://", "user://x") != Error.Ok, "RegisterPrefixFolder rejects URL schemes");
    }

    private void TestFolderPrefix()
    {
        PathPrefix folderPrefix = PathPrefixServer.CreateFolderPrefix("user://pp_folder");
        ExpectEqual(PathPrefixServer.RegisterPrefix("ppfolder://", folderPrefix), Error.Ok, "RegisterPrefix with CreateFolderPrefix");
        ExpectEqual(DirAccess.MakeDirRecursiveAbsolute("ppfolder://sub/dir"), Error.Ok, "DirAccess creates folders through the prefix");
        using (FileAccess file = FileAccess.Open("ppfolder://sub/a.txt", FileAccess.ModeFlags.Write))
        {
            file.StoreString("hello");
        }
        ExpectEqual(FileAccess.GetFileAsString("ppfolder://sub/a.txt"), "hello", "FileAccess writes and reads through the prefix");
        using DirAccess dir = DirAccess.Open("ppfolder://sub");
        Expect(dir.FileExists("a.txt"), "DirAccess.Open lists the file");
        var resource = new Resource { ResourceName = "from_prefix" };
        ExpectEqual(ResourceSaver.Save(resource, "ppfolder://r.tres"), Error.Ok, "ResourceSaver saves through the prefix");
        Resource loaded = ResourceLoader.Load("ppfolder://r.tres");
        ExpectEqual(loaded.ResourceName, "from_prefix", "ResourceLoader loads through the prefix");
        ExpectEqual(loaded.ResourcePath, "ppfolder://r.tres", "ResourcePath keeps the prefixed path");
        Expect(ResourceLoader.Exists("ppfolder://r.tres"), "ResourceLoader.Exists finds the resource");
        Expect(!ResourceLoader.Exists("ppfolder://missing.tres"), "ResourceLoader.Exists is false for a missing resource");
        Expect(ResourceLoader.Load("ppfolder://r.tres") == loaded, "a second load returns the cached resource");
        PathPrefixServer.RegisterPrefixFolder("pp_under://", "user://pp_folder");
        Resource underscore = ResourceLoader.Load("pp_under://r.tres");
        Expect(underscore != null, "ResourceLoader loads through a prefix with an underscore");
        PathPrefixServer.UnregisterPrefix("pp_under://");
        PathPrefixServer.UnregisterPrefix("ppfolder://");
    }

    private void TestCallablePrefix()
    {
        Callable globalize = Callable.From((string path) => ProjectSettings.GlobalizePath("user://pp_call/" + path.TrimPrefix("ppcall://")));
        Callable localize = Callable.From((string globalPath) => globalPath.EndsWith("/pp_call/found") ? "ppcall://found" : "");
        Callable isAvailable = Callable.From(() => false);
        ExpectEqual(PathPrefixServer.RegisterPrefixCallable("ppcall://", globalize, [], localize, isAvailable), Error.Ok, "RegisterPrefixCallable with all callables");
        Expect(!PathPrefixServer.IsPrefixAvailable("ppcall://"), "IsPrefixAvailable uses the isAvailable callable");
        ExpectEqual(PathPrefixServer.Globalize("ppcall://a"), "ppcall://a", "Globalize keeps the path of an unavailable prefix");
        ExpectEqual(PathPrefixServer.Resolve("ppcall://a").Error, Error.Unavailable, "Resolve of an unavailable prefix gives Unavailable");
        PathPrefixServer.RegisterPrefixCallable("ppcall://", globalize, [], localize);
        Expect(PathPrefixServer.IsPrefixAvailable("ppcall://"), "without isAvailable the prefix is available");
        ExpectEqual(PathPrefixServer.Globalize("ppcall://a"), ProjectSettings.GlobalizePath("user://pp_call/a"), "Globalize calls the globalize callable");
        string found = PathPrefixServer.Localize(ProjectSettings.GlobalizePath("user://pp_call/found"));
        ExpectEqual(found, "ppcall://found", "Localize calls the localize callable");
        PathPrefixServer.UnregisterPrefix("ppcall://");
    }

    private void TestSubclassPrefix()
    {
        var prefix = new TestPrefix();
        ExpectEqual(PathPrefixServer.RegisterPrefix("test://", prefix), Error.Ok, "RegisterPrefix with a PathPrefix subclass");
        ExpectEqual(PathPrefixServer.Globalize("test://a/b.txt"), ProjectSettings.GlobalizePath("user://pp_test/a/b.txt"), "_Globalize is called");
        string localPath = PathPrefixServer.Localize(ProjectSettings.GlobalizePath("user://pp_test/a/b.txt"));
        ExpectEqual(localPath, "test://a/b.txt", "_Localize is called");
        Expect(!PathPrefixServer.IsPrefixReadOnly("test://"), "_IsReadOnly is false");
        prefix.ReadOnly = true;
        Expect(PathPrefixServer.IsPrefixReadOnly("test://"), "_IsReadOnly is true");
        PathInfo write = PathPrefixServer.ResolveSafe("test://a.txt", PathPrefixServer.AccessMode.Write);
        ExpectEqual(write.Error, Error.FileNoPermission, "ResolveSafe rejects writing to a read-only prefix");
        Expect(write.ReadOnly, "PathInfo.ReadOnly is true");
        PathInfo read = PathPrefixServer.ResolveSafe("test://a.txt", PathPrefixServer.AccessMode.Read);
        Expect(read.Valid, "ResolveSafe accepts reading from a read-only prefix");
        prefix.Available = false;
        Expect(!PathPrefixServer.IsPrefixAvailable("test://"), "_IsAvailable is false");
        Expect(!PathPrefixServer.Resolve("test://a.txt").Available, "PathInfo.Available is false");
        PathPrefixServer.UnregisterPrefix("test://");
    }

    private void TestPlatforms()
    {
        string platform = OS.GetName().ToLower();
        PathPrefixServer.RegisterPrefixFolder("ppplat://", "user://pp_all");
        PathPrefixServer.RegisterPrefixFolder("ppplat://", "user://pp_named", [MissingTag]);
        string globalPath = PathPrefixServer.Globalize("ppplat://a");
        ExpectEqual(globalPath, ProjectSettings.GlobalizePath("user://pp_all/a"), "a registration for a feature that does not match is ignored");
        PathPrefixServer.RegisterPrefixFolder("ppplat://", "user://pp_named", [platform]);
        globalPath = PathPrefixServer.Globalize("ppplat://a");
        ExpectEqual(globalPath, ProjectSettings.GlobalizePath("user://pp_named/a"), "a registration for this platform wins over the one for all");
        ExpectEqual(PathPrefixServer.UnregisterPrefix("ppplat://", [platform]), Error.Ok, "UnregisterPrefix removes one platform registration");
        globalPath = PathPrefixServer.Globalize("ppplat://a");
        ExpectEqual(globalPath, ProjectSettings.GlobalizePath("user://pp_all/a"), "the registration for all platforms is used again");
        PathPrefixServer.UnregisterPrefix("ppplat://");
        Expect(!PathPrefixServer.HasPrefix("ppplat://"), "UnregisterPrefix without platforms removes all registrations");
        PathPrefixServer.RegisterPrefixFolder("pponly://", "user://pp_only", [MissingTag]);
        Expect(PathPrefixServer.HasPrefix("pponly://"), "HasPrefix is true for any platform");
        Expect(!PathPrefixServer.IsPrefixAvailable("pponly://"), "IsPrefixAvailable is false on other platforms");
        PathPrefixServer.UnregisterPrefix("pponly://");
    }

    private void TestSystemDirPrefix()
    {
        PathPrefix prefix = PathPrefixServer.CreateSystemDirPrefix(OS.SystemDir.Pictures);
        ExpectEqual(PathPrefixServer.RegisterPrefix("pppictures://", prefix), Error.Ok, "RegisterPrefix with CreateSystemDirPrefix");
        string expected = OS.GetSystemDir(OS.SystemDir.Pictures).PathJoin("a.png");
        ExpectEqual(PathPrefixServer.Globalize("pppictures://a.png"), expected, "the prefix maps to the system folder");
        PathPrefix privatePrefix = PathPrefixServer.CreateSystemDirPrefix(OS.SystemDir.Pictures, false);
        Expect(privatePrefix != null, "CreateSystemDirPrefix accepts sharedStorage = false");
        PathPrefixServer.UnregisterPrefix("pppictures://");
    }

    private void TestResolve()
    {
        PathInfo info = PathPrefixServer.Resolve("USER://saves\\a.json");
        Expect(info.Valid, "Resolve: the path is valid");
        ExpectEqual(info.Error, Error.Ok, "PathInfo.Error is Ok");
        ExpectEqual(info.ErrorMessage, "", "PathInfo.ErrorMessage is empty");
        ExpectEqual(info.SourcePath, "USER://saves\\a.json", "SourcePath is the given path");
        ExpectEqual(info.LocalPath, "user://saves/a.json", "LocalPath is canonical");
        ExpectEqual(info.GlobalPath, ProjectSettings.GlobalizePath("user://saves/a.json"), "GlobalPath is the OS path");
        ExpectEqual(info.Prefix, "user://", "Prefix is canonical");
        ExpectEqual(info.RelativePath, "saves/a.json", "RelativePath is the part after the prefix");
        ExpectEqual(info.FileName, "a.json", "FileName");
        ExpectEqual(info.Extension, "json", "Extension");
        ExpectEqual(info.BaseDir, "user://saves", "BaseDir");
        Expect(info.Available, "PathInfo.Available is true");
        Expect(info.Locked, "PathInfo.Locked is true for user://");
        Expect(!info.ReadOnly, "PathInfo.ReadOnly is false for user://");
        PathInfo dots = PathPrefixServer.Resolve("user://a/./b/../c.txt");
        ExpectEqual(dots.LocalPath, "user://a/c.txt", "Resolve normalizes . and ..");
        PathInfo fromGlobal = PathPrefixServer.Resolve(OS.GetUserDataDir() + "/saves/a.json");
        ExpectEqual(fromGlobal.LocalPath, "user://saves/a.json", "Resolve accepts an OS path below a prefix");
        PathInfo relative = PathPrefixServer.Resolve("project.godot");
        ExpectEqual(relative.LocalPath, "res://project.godot", "Resolve treats a relative path as res://");
        Expect(relative.Exists(), "PathInfo.Exists is true for an existing file");
        Expect(!relative.IsDir(), "PathInfo.IsDir is false for a file");
        Expect(PathPrefixServer.Resolve("res://").IsDir(), "PathInfo.IsDir is true for res://");
        PathInfo unknown = PathPrefixServer.Resolve("nope://x");
        Expect(!unknown.Valid, "Resolve of an unknown prefix is invalid");
        ExpectEqual(unknown.Error, Error.FileBadPath, "the error is FileBadPath");
        Expect(unknown.ErrorMessage != "", "ErrorMessage describes the error");
        Expect(!PathPrefixServer.Resolve("temp://x").Locked, "PathInfo.Locked is false for temp://");
    }

    private void TestResolveSafe()
    {
        PathInfo good = PathPrefixServer.ResolveSafe("user://saves/a.json", PathPrefixServer.AccessMode.Write);
        Expect(good.Valid, "ResolveSafe accepts a prefixed path");
        string[] rejected = ["user://saves/../../x", "/etc/passwd", "C:\\Windows\\win.ini", "nope://x", "relative.txt"];
        foreach (string path in rejected)
        {
            PathInfo info = PathPrefixServer.ResolveSafe(path, PathPrefixServer.AccessMode.Read);
            ExpectEqual(info.Error, Error.FileBadPath, $"ResolveSafe rejects {path}");
        }
        string[] roots = ["user://saves/"];
        PathInfo outside = PathPrefixServer.ResolveSafe("user://other/a.json", PathPrefixServer.AccessMode.Write, roots);
        ExpectEqual(outside.Error, Error.FileBadPath, "ResolveSafe rejects a path outside allowedRoots");
        PathInfo inside = PathPrefixServer.ResolveSafe("user://saves/a.json", PathPrefixServer.AccessMode.Write, roots);
        Expect(inside.Valid, "ResolveSafe accepts a path inside allowedRoots");
    }

    private void TestResourceReferences()
    {
        PathPrefixServer.RegisterPrefixFolder("ppref://", "user://pp_ref");
        DirAccess.MakeDirRecursiveAbsolute("ppref://");
        var gradient = new Gradient();
        gradient.TakeOverPath("ppref://b.tres");
        ResourceSaver.Save(gradient, "ppref://b.tres");
        var texture = new GradientTexture1D { Gradient = gradient };
        ResourceSaver.Save(texture, "ppref://a.tres");
        string text = FileAccess.GetFileAsString("ppref://a.tres");
        Expect(text.Contains("path=\"ppref://b.tres\""), "a saved resource stores the prefixed path of its reference");
        var loaded = (GradientTexture1D)ResourceLoader.Load("ppref://a.tres", "", ResourceLoader.CacheMode.IgnoreDeep);
        ExpectEqual(loaded.Gradient.ResourcePath, "ppref://b.tres", "the loaded reference keeps the prefixed path");
        string[] dependencies = ResourceLoader.GetDependencies("ppref://a.tres");
        Expect(dependencies[0].Contains("ppref://b.tres"), "GetDependencies lists the prefixed path");
        PathPrefixServer.UnregisterPrefix("ppref://");
    }

    private void TestEnginePath()
    {
        PathPrefixServer.RegisterPrefixFolder("pppack://", PackFolder);
        PathPrefixServer.RegisterPrefixFolder("ppchain://", "pppack://sub");
        PathPrefixServer.RegisterPrefixFolder("ppuser://", "user://pp_user");
        Callable toPack = Callable.From((string path) => PackFolder + "/" + path.TrimPrefix("ppcall://"));
        PathPrefixServer.RegisterPrefixCallable("ppcall://", toPack);
        ExpectEqual(PathPrefixServer.ResolveEnginePath("pppack://data.txt"), PackFolder + "/data.txt", "ResolveEnginePath of a prefix into res://");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("pppack://x/../data.txt"), PackFolder + "/data.txt", "ResolveEnginePath resolves ..");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("ppchain://b.txt"), PackFolder + "/sub/b.txt", "ResolveEnginePath follows a chain of prefixes");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("ppcall://data.txt"), PackFolder + "/data.txt", "ResolveEnginePath of a callable prefix");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("ppuser://a.txt"), "user://pp_user/a.txt", "ResolveEnginePath of a prefix into user://");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("temp://a.txt"), "temp://a.txt", "a prefix that ends in an OS path stays unchanged");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("res://a.txt"), "res://a.txt", "an engine path stays unchanged");
        DirAccess.MakeDirRecursiveAbsolute("ppuser://saves");
        using (FileAccess file = FileAccess.Open("ppuser://saves/a.txt", FileAccess.ModeFlags.Write))
        {
            ExpectEqual(file.GetPath(), "user://pp_user/saves/a.txt", "FileAccess.GetPath is the user:// path");
        }
        using DirAccess dir = DirAccess.Open("ppuser://saves");
        Expect(dir.GetCurrentDir().StartsWith("user://"), "DirAccess.Open is a user:// access");
        PathPrefixServer.UnregisterPrefix("ppuser://");
    }

    private void TestPack()
    {
        var packer = new PckPacker();
        packer.PckStart(PackPath);
        packer.AddFileFromBuffer("pp_test_pack/data.txt", "Packed".ToUtf8Buffer());
        packer.AddFileFromBuffer("pp_test_pack/sub/b.txt", "Nested".ToUtf8Buffer());
        string tres = "[gd_resource type=\"Resource\" format=3]\n\n[resource]\nresource_name = \"packed\"\n";
        packer.AddFileFromBuffer("pp_test_pack/res.tres", tres.ToUtf8Buffer());
        ExpectEqual(packer.Flush(), Error.Ok, "PCKPacker writes the pack");
        Expect(!FileAccess.FileExists(PackFolder + "/data.txt"), "the file is not on disk before the pack is loaded");
        Expect(ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath(PackPath)), "LoadResourcePack mounts the pack");
        Expect(FileAccess.FileExists("pppack://data.txt"), "FileAccess.FileExists finds the packed file");
        Expect(!FileAccess.FileExists("pppack://missing.txt"), "FileAccess.FileExists is false for a missing file");
        ExpectEqual(FileAccess.GetFileAsString("pppack://data.txt"), "Packed", "FileAccess reads a packed file through the prefix");
        ExpectEqual(FileAccess.GetFileAsString("ppchain://b.txt"), "Nested", "FileAccess reads through a chain of prefixes");
        ExpectEqual(FileAccess.GetFileAsString("ppcall://data.txt"), "Packed", "FileAccess reads through a callable prefix");
        Expect(DirAccess.DirExistsAbsolute("pppack://sub"), "DirAccess finds a packed folder");
        using DirAccess dir = DirAccess.Open("pppack://");
        Expect(dir.FileExists("data.txt"), "DirAccess.Open lists the packed files");
        Resource byPrefix = ResourceLoader.Load("pppack://res.tres");
        ExpectEqual(byPrefix.ResourceName, "packed", "ResourceLoader loads a packed resource through the prefix");
        ExpectEqual(byPrefix.ResourcePath, PackFolder + "/res.tres", "ResourcePath is the res:// path");
        Expect(ResourceLoader.Load(PackFolder + "/res.tres") == byPrefix, "the prefix and the res:// path share the cache");
        foreach (string prefix in new[] { "pppack://", "ppchain://", "ppcall://" })
        {
            PathPrefixServer.UnregisterPrefix(prefix);
        }
    }

    private void OnPrefixRegistered(string prefix) => _registered.Add(prefix);

    private void OnPrefixUnregistered(string prefix) => _unregistered.Add(prefix);
}
