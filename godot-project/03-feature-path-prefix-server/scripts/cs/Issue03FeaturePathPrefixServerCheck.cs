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

        Expect(PathPrefixServer.Localize(ProjectSettings.GlobalizePath("user://pp_cs_mods/sub/a.txt")) == "csmods://sub/a.txt", "Localize() uses _Localize of the C# subclass");
        Expect(PathPrefixServer.UnregisterPrefix("csmods://") == Error.Ok, "UnregisterPrefix(csmods://)");
        Expect(!PathPrefixServer.HasPrefix("csmods://"), "csmods:// is gone");
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
}
