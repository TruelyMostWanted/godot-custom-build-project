using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ResourcePaths;

/// <summary>Full API test of Resource Paths: ResourcePath, cache modes, TakeOverPath, saver flags, loader and saver virtuals.
/// Same tests as scripts/gd/Test.gd, without the GDScript-only typed virtuals.
/// The step "resource_path of a taken path" prints an engine error on purpose.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://resource-paths/cs/";
    private const string NoteText = "Buy milk";

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestCacheModes();
        TestSaveLeavesThePath();
        TestTakeOverPath();
        TestResourcePathOfATakenPath();
        TestLoaderAndSaver();
        Cleanup();
        return Task.CompletedTask;
    }

    private static Resource Make(string resourceName)
    {
        return new Resource { ResourceName = resourceName };
    }

    private void TestCacheModes()
    {
        string path = Root + "stats.tres";
        ResourceSaver.Save(Make("v1"), path);
        Resource first = GD.Load(path);
        Resource second = GD.Load(path);
        ExpectEqual(first.ResourcePath, path, "REUSE sets the path");
        Expect(first == second, "REUSE returns the cached instance");
        Expect(ResourceLoader.HasCached(path), "REUSE caches the instance");

        Resource ignored = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(ignored != first, "IGNORE returns a new instance");
        ExpectEqual(ignored.ResourcePath, path, "IGNORE reports the path");
        Expect(GD.Load(path) == first, "IGNORE leaves the cached instance in the cache");

        ResourceSaver.Save(Make("v2"), path);
        Resource replaced = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Replace);
        Expect(replaced == first, "REPLACE returns the cached instance");
        ExpectEqual(first.ResourceName, "v2", "REPLACE updates the cached instance in place");
        ExpectEqual(first.ResourcePath, path, "REPLACE keeps the path");
    }

    private void TestSaveLeavesThePath()
    {
        string path = Root + "profile.tres";
        var profile = new Resource();
        ResourceSaver.Save(profile, path);
        ExpectEqual(profile.ResourcePath, "", "save does not set the path");
        ResourceSaver.Save(profile, path, ResourceSaver.SaverFlags.ChangePath);
        ExpectEqual(profile.ResourcePath, "", "ChangePath restores the previous path after saving");
        Resource loaded = GD.Load(path);
        Expect(loaded != profile, "a saved resource without a path is not the cached one");
    }

    private void TestTakeOverPath()
    {
        string path = Root + "taken.tres";
        var first = new Resource();
        ResourceSaver.Save(first, path);
        first.TakeOverPath(path);
        ExpectEqual(first.ResourcePath, path, "TakeOverPath sets the path");
        Expect(GD.Load(path) == first, "Load returns the resource that took over the path");
        var other = new Resource();
        other.TakeOverPath(path);
        ExpectEqual(other.ResourcePath, path, "another TakeOverPath gets the path");
        ExpectEqual(first.ResourcePath, "", "the previous holder loses the path");
        Expect(GD.Load(path) == other, "Load returns the new holder");
    }

    private void TestResourcePathOfATakenPath()
    {
        string path = Root + "held.tres";
        var holder = new Resource();
        holder.TakeOverPath(path);
        var latecomer = new Resource();
        latecomer.ResourcePath = path;
        ExpectEqual(latecomer.ResourcePath, "", "setting ResourcePath of a held path fails and leaves no path");
        ExpectEqual(holder.ResourcePath, path, "the holder keeps the path");
    }

    private void TestLoaderAndSaver()
    {
        string path = Root + "shopping.note";
        var loader = new NoteLoader();
        var saver = new NoteSaver();
        ResourceLoader.AddResourceFormatLoader(loader);
        ResourceSaver.AddResourceFormatSaver(saver);
        Resource note = Make(NoteText);

        ExpectEqual(ResourceSaver.Save(note, path), Error.Ok, "the saver writes the file");
        ExpectEqual(saver.LastFlags, ResourceSaver.SaverFlags.None, "the saver gets no flags by default");
        ResourceSaver.Save(note, path, ResourceSaver.SaverFlags.Compress);
        Expect(saver.LastFlags.HasFlag(ResourceSaver.SaverFlags.Compress), "the saver gets Compress");

        Resource loaded = GD.Load(path);
        ExpectEqual(loaded.ResourceName, NoteText, "the loader reads the file");
        ExpectEqual(loaded.ResourcePath, path, "ResourceLoader sets the path, not the loader");
        ExpectEqual(loader.LastCacheMode, ResourceFormatLoader.CacheMode.Reuse, "the loader gets CacheMode.Reuse by default");
        ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(loader.LastCacheMode, ResourceFormatLoader.CacheMode.Ignore, "the loader gets CacheMode.Ignore");

        ResourceLoader.RemoveResourceFormatLoader(loader);
        ResourceSaver.RemoveResourceFormatSaver(saver);
    }

    private static void Cleanup()
    {
        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
    }
}
