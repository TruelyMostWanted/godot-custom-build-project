using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue367RefactoringTypedLoaderSaverVirtuals;

// Note: the old signatures (int cacheMode / uint flags) no longer compile in C# (CS0115, no matching base method),
// so only the new enum typing is checked here. The GDScript check covers the old int typing.

/// <summary>Loader of the text format t367b. Records the cache mode it received.</summary>
public partial class Issue367LoaderB : ResourceFormatLoader
{
    public ResourceFormatLoader.CacheMode LastCacheMode = (ResourceFormatLoader.CacheMode)(-1);

    public override string[] _GetRecognizedExtensions() => new[] { "t367b" };

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path) => path.GetExtension() == "t367b" ? "Resource" : "";

    public override Variant _Load(string path, string originalPath, bool useSubThreads, ResourceFormatLoader.CacheMode cacheMode)
    {
        LastCacheMode = cacheMode;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return (long)Error.FileCantOpen;
        }
        var res = new Resource();
        res.ResourceName = file.GetLine();
        return res;
    }
}

/// <summary>Saver of the text format t367b. Records the flags it received.</summary>
public partial class Issue367SaverB : ResourceFormatSaver
{
    public ResourceSaver.SaverFlags LastFlags = (ResourceSaver.SaverFlags)(-1);

    public override bool _Recognize(Resource resource) => (string)resource.GetMeta("fmt", "") == "b";

    public override string[] _GetRecognizedExtensions(Resource resource) => new[] { "t367b" };

    public override Error _Save(Resource resource, string path, ResourceSaver.SaverFlags flags)
    {
        LastFlags = flags;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            return Error.FileCantOpen;
        }
        file.StoreLine(resource.ResourceName);
        return Error.Ok;
    }
}

/// <summary>C# checks of 367-refactoring-typed-loader-saver-virtuals. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue367RefactoringTypedLoaderSaverVirtualsCheck : FeatureCheckCs
{
    protected override string CheckName => "367-refactoring-typed-loader-saver-virtuals";

    private static Resource Make(string name, string fmt = "")
    {
        var res = new Resource();
        res.ResourceName = name;
        if (fmt != "")
        {
            res.SetMeta("fmt", fmt);
        }
        return res;
    }

    private static void Remove(string path)
    {
        if (FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(path);
        }
    }

    private static bool Same(GodotObject a, GodotObject b) => a != null && b != null && a.GetInstanceId() == b.GetInstanceId();

    protected override Task RunChecks()
    {
        CheckTypedFormat();
        CheckCacheModes();
        CheckPaths();
        return Task.CompletedTask;
    }

    private void CheckTypedFormat()
    {
        const string path = "user://t367_b_cs.t367b";
        var loader = new Issue367LoaderB();
        var saver = new Issue367SaverB();
        ResourceLoader.AddResourceFormatLoader(loader);
        ResourceSaver.AddResourceFormatSaver(saver);

        ExpectEqual(ResourceSaver.Save(Make("hello-b", "b"), path, ResourceSaver.SaverFlags.Compress), Error.Ok, "B: save with enum-typed _Save override");
        Expect((saver.LastFlags & ResourceSaver.SaverFlags.Compress) != 0, "B: saver received Compress");
        ExpectEqual(ResourceSaver.Save(Make("hello-b", "b"), path), Error.Ok, "B: save without flags");
        Expect((saver.LastFlags & ResourceSaver.SaverFlags.Compress) == 0, "B: saver received no Compress by default");

        var ignored = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(ignored != null, "B: load with Ignore returns a resource");
        if (ignored != null)
        {
            ExpectEqual(ignored.ResourceName, "hello-b", "B: value survives the round trip");
        }
        ExpectEqual(loader.LastCacheMode, ResourceFormatLoader.CacheMode.Ignore, "B: loader received CacheMode.Ignore");
        ExpectEqual((int)loader.LastCacheMode, 0, "B: Ignore is 0");

        var reused = ResourceLoader.Load(path);
        Expect(reused != null, "B: default load returns a resource");
        ExpectEqual(loader.LastCacheMode, ResourceFormatLoader.CacheMode.Reuse, "B: loader received CacheMode.Reuse by default");
        ExpectEqual((int)loader.LastCacheMode, 1, "B: Reuse is 1");

        ResourceLoader.RemoveResourceFormatLoader(loader);
        ResourceSaver.RemoveResourceFormatSaver(saver);
        Remove(path);
    }

    private void CheckCacheModes()
    {
        const string path = "user://t367_cache_cs.tres";
        ExpectEqual(ResourceSaver.Save(Make("v1"), path), Error.Ok, "cache: save .tres");

        var first = ResourceLoader.Load(path);
        Expect(first != null, "cache: REUSE load returns a resource");
        if (first == null)
        {
            Remove(path);
            return;
        }
        ExpectEqual(first.ResourcePath, path, "cache: loaded ResourcePath equals the path");
        Expect(Same(ResourceLoader.Load(path), first), "cache: second REUSE load returns the same instance");

        var ignored = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(ignored != null && !Same(ignored, first), "cache: IGNORE returns a different instance");
        if (ignored != null)
        {
            ExpectEqual(ignored.ResourcePath, path, "cache: IGNORE instance has the path too");
        }
        Expect(Same(ResourceLoader.Load(path), first), "cache: REUSE still returns the first instance after IGNORE");

        ExpectEqual(ResourceSaver.Save(Make("v2"), path), Error.Ok, "cache: re-save with new content");
        var replaced = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Replace);
        Expect(Same(replaced, first), "cache: REPLACE returns the first instance");
        ExpectEqual(first.ResourceName, "v2", "cache: REPLACE updated the instance in place");

        Remove(path);
    }

    private void CheckPaths()
    {
        const string path = "user://t367_paths_cs.tres";
        var fresh = Make("fresh");
        ExpectEqual(ResourceSaver.Save(fresh, path, ResourceSaver.SaverFlags.ChangePath), Error.Ok, "paths: save with ChangePath");
        ExpectEqual(fresh.ResourcePath, "", "paths: ResourcePath is restored to empty after saving");

        var first = ResourceLoader.Load(path);
        Expect(first != null && first.ResourcePath == path, "paths: first resource holds the path");
        var second = new Resource();
        second.TakeOverPath(path);
        ExpectEqual(second.ResourcePath, path, "paths: TakeOverPath gives the second resource the path");
        if (first != null)
        {
            ExpectEqual(first.ResourcePath, "", "paths: the first resource lost the path");
        }

        Remove(path);
    }
}
