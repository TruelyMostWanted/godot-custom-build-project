using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue03FeaturePathPrefixServer;

/// <summary>A prefix defined in C#: <c>csmods://</c> maps to <c>user://pp_cs_mods/</c>.</summary>
public partial class CsModsPrefix : PathPrefix
{
    private const string Root = "user://pp_cs_mods";

    public override string _Globalize(string path)
    {
        return ProjectSettings.GlobalizePath(Root + "/" + path.TrimPrefix("csmods://"));
    }

    public override string _Localize(string globalPath)
    {
        string root = ProjectSettings.GlobalizePath(Root);
        return globalPath.StartsWith(root + "/") ? "csmods://" + globalPath.Substring(root.Length + 1) : "";
    }
}

/// <summary>C# checks of 03-feature-path-prefix-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue03FeaturePathPrefixServerCheck : FeatureCheckCs
{
    private static readonly string[] EnginePrefixes = ["res://", "user://", "uid://", "pipe://", "exec://"];

    protected override string CheckName => "03-feature-path-prefix-server";

    protected override Task RunChecks()
    {
        CheckEnginePrefixes();
        CheckExecPrefix();
        CheckSubclassPrefix();
        CheckPlatforms();
        CheckDefaults();
        CheckResolve();
        CheckResolveSafe();
        CheckPerformance();
        // Last: mounting a pack switches the res:// directory access of this run.
        CheckEnginePathRedispatch();
        return Task.CompletedTask;
    }

    private void CheckEnginePrefixes()
    {
        Expect(Engine.HasSingleton("PathPrefixServer"), "PathPrefixServer is a singleton");
        foreach (string prefix in EnginePrefixes)
        {
            string before = PathPrefixServer.Globalize(prefix + "a/b.txt");
            Expect(PathPrefixServer.IsPrefixLocked(prefix), $"{prefix} is locked");
            Expect(PathPrefixServer.RegisterPrefix(prefix, new CsModsPrefix()) == Error.Locked, $"RegisterPrefix({prefix}) fails with Locked");
            Expect(PathPrefixServer.UnregisterPrefix(prefix) == Error.Locked, $"UnregisterPrefix({prefix}) fails with Locked");
            Expect(PathPrefixServer.Globalize(prefix + "a/b.txt") == before, $"{prefix} resolves as before");
        }
        foreach (string path in new[] { "res://folder/file.txt", "user://folder/file.txt" })
        {
            Expect(PathPrefixServer.Localize(PathPrefixServer.Globalize(path)) == path, $"Globalize() then Localize() gives {path}");
        }
    }

    private void CheckExecPrefix()
    {
        using (var file = FileAccess.Open("exec://pp_exec_test_cs.txt", FileAccess.ModeFlags.Write))
        {
            Expect(file != null, "FileAccess.Open(exec://..., Write) works");
            file?.StoreString("exec");
        }
        string real = ProjectSettings.GlobalizePath("res://pp_exec_test_cs.txt");
        Expect(FileAccess.FileExists(real), "exec:// writes into the project folder in the editor");
        DirAccess.RemoveAbsolute(real);
    }

    private void CheckSubclassPrefix()
    {
        Expect(PathPrefixServer.RegisterPrefix("csmods://", new CsModsPrefix()) == Error.Ok, "RegisterPrefix(csmods://, C# subclass) works");
        Expect(DirAccess.MakeDirRecursiveAbsolute("csmods://sub/dir") == Error.Ok, "DirAccess.MakeDirRecursiveAbsolute(csmods://sub/dir)");
        using (var file = FileAccess.Open("csmods://sub/a.txt", FileAccess.ModeFlags.Write))
        {
            Expect(file != null, "FileAccess.Open(csmods://sub/a.txt, Write)");
            file?.StoreString("hello");
        }
        Expect(FileAccess.GetFileAsString("csmods://sub/a.txt") == "hello", "FileAccess reads csmods://sub/a.txt");
        using var dir = DirAccess.Open("csmods://sub");
        Expect(dir != null && dir.FileExists("a.txt"), "DirAccess.Open(csmods://sub) lists a.txt");

        var res = new Resource { ResourceName = "from_csmods" };
        Expect(ResourceSaver.Save(res, "csmods://r.tres") == Error.Ok, "ResourceSaver.Save(csmods://r.tres)");
        var loaded = ResourceLoader.Load("csmods://r.tres", "", ResourceLoader.CacheMode.Ignore);
        Expect(loaded != null && loaded.ResourceName == "from_csmods", "ResourceLoader.Load(csmods://r.tres)");

        CheckResourceIo();

        Expect(PathPrefixServer.Localize(ProjectSettings.GlobalizePath("user://pp_cs_mods/sub/a.txt")) == "csmods://sub/a.txt", "Localize() uses _Localize of the C# subclass");
        Expect(PathPrefixServer.UnregisterPrefix("csmods://") == Error.Ok, "UnregisterPrefix(csmods://)");
        Expect(!PathPrefixServer.HasPrefix("csmods://"), "csmods:// is gone");
    }

    private void CheckResourceIo()
    {
        Expect(ResourceLoader.Exists("csmods://r.tres"), "ResourceLoader.Exists(csmods://r.tres) is true");
        Expect(!ResourceLoader.Exists("csmods://missing.tres"), "ResourceLoader.Exists(csmods://missing.tres) is false");
        var first = ResourceLoader.Load("csmods://r.tres");
        var second = ResourceLoader.Load("csmods://r.tres");
        Expect(first != null && ReferenceEquals(first, second), "Load(csmods://r.tres) twice gives the same object (cache)");
        Expect(first != null && first.ResourcePath == "csmods://r.tres", "ResourcePath is csmods://r.tres");
        Expect(ResourceLoader.HasCached("csmods://r.tres"), "ResourceLoader.HasCached(csmods://r.tres)");

        var res2 = new Resource { ResourceName = "threaded" };
        Expect(ResourceSaver.Save(res2, "csmods://r2.tres") == Error.Ok, "ResourceSaver.Save(csmods://r2.tres)");
        Expect(ResourceLoader.LoadThreadedRequest("csmods://r2.tres") == Error.Ok, "LoadThreadedRequest(csmods://r2.tres)");
        ulong deadline = Time.GetTicksMsec() + 10000;
        var status = ResourceLoader.LoadThreadedGetStatus("csmods://r2.tres");
        while (status == ResourceLoader.ThreadLoadStatus.InProgress && Time.GetTicksMsec() < deadline)
        {
            OS.DelayMsec(10);
            status = ResourceLoader.LoadThreadedGetStatus("csmods://r2.tres");
        }
        Expect(status == ResourceLoader.ThreadLoadStatus.Loaded, $"LoadThreadedGetStatus(csmods://r2.tres) is Loaded: {status}");
        var threaded = ResourceLoader.LoadThreadedGet("csmods://r2.tres");
        Expect(threaded != null && threaded.ResourceName == "threaded", "LoadThreadedGet(csmods://r2.tres) is not null");

        var b = new Gradient();
        b.SetColor(0, Colors.Red);
        b.TakeOverPath("csmods://b.tres");
        Expect(ResourceSaver.Save(b, "csmods://b.tres") == Error.Ok, "ResourceSaver.Save(csmods://b.tres)");
        Expect(b.ResourcePath == "csmods://b.tres", "TakeOverPath() sets ResourcePath to csmods://b.tres");
        var a = new GradientTexture1D { Gradient = b };
        Expect(ResourceSaver.Save(a, "csmods://a.tres") == Error.Ok, "ResourceSaver.Save(csmods://a.tres)");
        string text = FileAccess.GetFileAsString("csmods://a.tres");
        Expect(text.Contains("path=\"csmods://b.tres\""), "a.tres has an ext_resource with path csmods://b.tres");
        var aLoaded = ResourceLoader.Load("csmods://a.tres", "", ResourceLoader.CacheMode.IgnoreDeep) as GradientTexture1D;
        Expect(aLoaded?.Gradient != null, "a.tres loads with its gradient");
        if (aLoaded?.Gradient != null)
        {
            Expect(aLoaded.Gradient.ResourcePath == "csmods://b.tres", $"the loaded gradient has ResourcePath csmods://b.tres: {aLoaded.Gradient.ResourcePath}");
        }
        bool found = false;
        foreach (string dependency in ResourceLoader.GetDependencies("csmods://a.tres"))
        {
            found |= dependency.Contains("csmods://b.tres");
        }
        Expect(found, "GetDependencies(csmods://a.tres) contains csmods://b.tres");
        GD.Print($"INFO cs: uid of csmods://b.tres = {ResourceLoader.GetResourceUid("csmods://b.tres")}, .uid file exists = {FileAccess.FileExists("csmods://b.tres.uid")}");
    }

    private void CheckPlatforms()
    {
        PathPrefixServer.RegisterPrefixFolder("pp_win://", "user://pp_win", ["windows"]);
        PathPrefixServer.RegisterPrefixFolder("pp_linux://", "user://pp_linux", ["linux"]);
        PathPrefixServer.RegisterPrefixFolder("pp_web://", "user://pp_web", ["web"]);
        Expect(PathPrefixServer.IsPrefixAvailable("pp_win://") == OS.HasFeature("windows"), "a windows-only prefix is available only on Windows");
        Expect(PathPrefixServer.IsPrefixAvailable("pp_linux://") == OS.HasFeature("linux"), "a linux-only prefix is available only on Linux");
        Expect(PathPrefixServer.IsPrefixAvailable("pp_web://") == OS.HasFeature("web"), "a web-only prefix is available only on the Web");
        foreach (string prefix in new[] { "pp_win://", "pp_linux://", "pp_web://" })
        {
            PathPrefixServer.UnregisterPrefix(prefix);
        }
    }

    private void CheckDefaults()
    {
        Expect((bool)ProjectSettings.GetSetting("filesystem/path_prefixes/defaults", false), "filesystem/path_prefixes/defaults is on by default");
        foreach (string prefix in new[] { "appdata://", "config://", "cache://", "temp://" })
        {
            Expect(PathPrefixServer.HasPrefix(prefix) && !PathPrefixServer.IsPrefixLocked(prefix), $"{prefix} is registered and not locked");
        }
        Expect(PathPrefixServer.RegisterPrefixFolder("appdata://", "user://pp_appdata") == Error.Ok, "appdata:// can be replaced");
        Expect(PathPrefixServer.Globalize("appdata://a") == ProjectSettings.GlobalizePath("user://pp_appdata/a"), "the replacement is used");
        PathPrefixServer.RegisterPrefix("appdata://", PathPrefixServer.CreateOsPathLogic(PathPrefixServer.OSPath.Data));
    }

    private void CheckResolve()
    {
        PathInfo info = PathPrefixServer.Resolve("USER://saves\\a.json");
        Expect(info.Valid, "Resolve(USER://saves\\a.json) is valid");
        Expect(info.LocalPath == "user://saves/a.json", "LocalPath == user://saves/a.json");
        Expect(info.GlobalPath == ProjectSettings.GlobalizePath("user://saves/a.json"), "GlobalPath is the user folder");
        Expect(info.Prefix == "user://" && info.RelativePath == "saves/a.json", "Prefix and RelativePath");
        Expect(info.FileName == "a.json" && info.Extension == "json" && info.BaseDir == "user://saves", "FileName, Extension, BaseDir");
        Expect(PathPrefixServer.Resolve(OS.GetUserDataDir() + "/saves/a.json").LocalPath == "user://saves/a.json", "a global path gives the same LocalPath");
        PathInfo nope = PathPrefixServer.Resolve("nope://x");
        Expect(!nope.Valid && nope.Error == Error.FileBadPath && nope.ErrorMessage != "", "Resolve(nope://x) is invalid with an error");
    }

    private void CheckResolveSafe()
    {
        Expect(PathPrefixServer.ResolveSafe("user://saves/a.json", true).Valid, "ResolveSafe(user://saves/a.json) is valid");
        Expect(PathPrefixServer.ResolveSafe("user://saves/../../x", false).Error == Error.FileBadPath, "ResolveSafe rejects user://saves/../../x");
        Expect(PathPrefixServer.ResolveSafe("/etc/passwd", false).Error == Error.FileBadPath, "ResolveSafe rejects /etc/passwd");
        Expect(PathPrefixServer.ResolveSafe("user://other/a.json", true, ["user://saves/"]).Error == Error.FileBadPath, "ResolveSafe rejects paths outside allowed roots");
        Expect(PathPrefixServer.ResolveSafe("user://saves/a.json", true, ["user://saves/"]).Valid, "ResolveSafe accepts paths inside allowed roots");
    }

    private void CheckPerformance()
    {
        ulong start = Time.GetTicksUsec();
        for (int i = 0; i < 100000; i++)
        {
            PathPrefixServer.Resolve("user://saves/a.json");
        }
        double resolveMs = (Time.GetTicksUsec() - start) / 1000.0;
        GD.Print($"PERF cs: 100000 x Resolve() = {resolveMs:F1} ms");
        Expect(resolveMs < 10000.0, "100000 Resolve() calls take less than 10 s");
    }

    // 87: prefixes that resolve into res:// or user:// use the engine access, including the PCK.
    private void CheckEnginePathRedispatch()
    {
        Expect(PathPrefixServer.RegisterPrefixFolder("pp_cspack://", "res://pp87_cs_pack") == Error.Ok, "RegisterPrefixFolder(pp_cspack://, res://pp87_cs_pack)");
        Expect(PathPrefixServer.RegisterPrefixFolder("pp_cschain://", "pp_cspack://sub") == Error.Ok, "RegisterPrefixFolder(pp_cschain://, pp_cspack://sub)");
        Expect(PathPrefixServer.RegisterPrefixFolder("pp_csuser://", "user://pp87_cs_user") == Error.Ok, "RegisterPrefixFolder(pp_csuser://, user://pp87_cs_user)");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("pp_cspack://data.txt"), "res://pp87_cs_pack/data.txt", "ResolveEnginePath(pp_cspack://data.txt)");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("pp_cspack://x/../data.txt"), "res://pp87_cs_pack/data.txt", "ResolveEnginePath() resolves ..");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("pp_cschain://b.txt"), "res://pp87_cs_pack/sub/b.txt", "ResolveEnginePath() follows pp_cschain:// -> pp_cspack:// -> res://");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("pp_csuser://a.txt"), "user://pp87_cs_user/a.txt", "ResolveEnginePath(pp_csuser://a.txt)");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("temp://a.txt"), "temp://a.txt", "a prefix that ends in an OS path stays unchanged");
        ExpectEqual(PathPrefixServer.ResolveEnginePath("res://a.txt"), "res://a.txt", "engine paths stay unchanged");

        // user:// alias: files and folders go through the user:// access.
        Expect(DirAccess.MakeDirRecursiveAbsolute("pp_csuser://saves") == Error.Ok, "MakeDirRecursiveAbsolute(pp_csuser://saves)");
        using (FileAccess file = FileAccess.Open("pp_csuser://saves/a.txt", FileAccess.ModeFlags.Write))
        {
            Expect(file != null, "FileAccess.Open(pp_csuser://saves/a.txt, Write)");
            if (file != null)
            {
                ExpectEqual(file.GetPath(), "user://pp87_cs_user/saves/a.txt", "GetPath() is the user:// path");
                file.StoreString("saved");
            }
        }
        ExpectEqual(FileAccess.GetFileAsString("user://pp87_cs_user/saves/a.txt"), "saved", "the file is in user://pp87_cs_user");
        using (DirAccess dir = DirAccess.Open("pp_csuser://saves"))
        {
            Expect(dir != null && dir.GetCurrentDir().StartsWith("user://") && dir.FileExists("a.txt"), "DirAccess.Open(pp_csuser://saves) is a user:// access");
        }
        Expect(DirAccess.RemoveAbsolute("pp_csuser://saves/a.txt") == Error.Ok && DirAccess.RemoveAbsolute("pp_csuser://saves") == Error.Ok, "RemoveAbsolute() through pp_csuser://");
        Expect(!DirAccess.DirExistsAbsolute("pp_csuser://saves"), "DirExistsAbsolute(pp_csuser://saves) is false after removal");

        // res:// alias: the files exist only in a pack, as in an exported game.
        const string pack = "user://pp87_cs.pck";
        var packer = new PckPacker();
        packer.PckStart(pack);
        packer.AddFileFromBuffer("pp87_cs_pack/data.txt", "Packed".ToUtf8Buffer());
        packer.AddFileFromBuffer("pp87_cs_pack/sub/b.txt", "Nested".ToUtf8Buffer());
        packer.AddFileFromBuffer("pp87_cs_pack/res.tres", "[gd_resource type=\"Resource\" format=3]\n\n[resource]\nresource_name = \"packed\"\n".ToUtf8Buffer());
        Expect(packer.Flush() == Error.Ok, "PckPacker writes user://pp87_cs.pck");
        Expect(!FileAccess.FileExists(ProjectSettings.GlobalizePath("res://pp87_cs_pack/data.txt")), "the file is not on disk");
        Expect(ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath(pack)), "LoadResourcePack()");
        Expect(FileAccess.FileExists("pp_cspack://data.txt"), "FileAccess.FileExists(pp_cspack://data.txt) finds the packed file");
        Expect(!FileAccess.FileExists("pp_cspack://missing.txt"), "FileAccess.FileExists(pp_cspack://missing.txt) is false");
        ExpectEqual(FileAccess.GetFileAsString("pp_cspack://data.txt"), "Packed", "FileAccess reads pp_cspack://data.txt from the pack");
        ExpectEqual(FileAccess.GetFileAsString("pp_cschain://b.txt"), "Nested", "FileAccess reads pp_cschain://b.txt from the pack");
        using (FileAccess packed = FileAccess.Open("pp_cspack://data.txt", FileAccess.ModeFlags.Read))
        {
            Expect(packed != null && packed.GetPath() == "res://pp87_cs_pack/data.txt", "GetPath() of a packed file is the res:// path");
        }
        Expect(DirAccess.DirExistsAbsolute("pp_cspack://sub"), "DirAccess.DirExistsAbsolute(pp_cspack://sub) finds the packed folder");
        using (DirAccess packDir = DirAccess.Open("pp_cspack://"))
        {
            Expect(packDir != null && packDir.FileExists("data.txt"), "DirAccess.Open(pp_cspack://) lists the packed files");
        }
        Expect(ResourceLoader.Exists("pp_cspack://res.tres"), "ResourceLoader.Exists(pp_cspack://res.tres)");
        Resource byAlias = ResourceLoader.Load("pp_cspack://res.tres");
        Expect(byAlias != null && byAlias.ResourceName == "packed", "ResourceLoader.Load(pp_cspack://res.tres) from the pack");
        Expect(byAlias != null && byAlias.ResourcePath == "res://pp87_cs_pack/res.tres", "ResourcePath is the res:// path");
        Expect(byAlias != null && byAlias == ResourceLoader.Load("res://pp87_cs_pack/res.tres"), "the alias and the res:// path share the cache");

        foreach (string prefix in new[] { "pp_cspack://", "pp_cschain://", "pp_csuser://" })
        {
            PathPrefixServer.UnregisterPrefix(prefix);
        }
    }
}
