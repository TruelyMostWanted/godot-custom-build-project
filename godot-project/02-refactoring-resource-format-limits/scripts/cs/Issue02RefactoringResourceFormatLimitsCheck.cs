using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue02RefactoringResourceFormatLimits;

/// <summary>Test loader: recognizes one extension and returns a new Resource that records which loader created it.</summary>
public partial class LimitLoaderCs : ResourceFormatLoader
{
    public string Extension { get; set; } = "";
    public int Id { get; set; }

    public override string[] _GetRecognizedExtensions() => [Extension];

    public override bool _HandlesType(StringName type) => type == "Resource";

    public override string _GetResourceType(string path) => path.GetExtension() == Extension ? "Resource" : "";

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        var res = new Resource();
        res.SetMeta("loader_id", Id);
        return res;
    }
}

/// <summary>Test saver: recognizes one extension and counts its calls instead of writing a file.</summary>
public partial class LimitSaverCs : ResourceFormatSaver
{
    public string Extension { get; set; } = "";
    public int Saved { get; private set; }

    public override bool _Recognize(Resource resource) => true;

    public override string[] _GetRecognizedExtensions(Resource resource) => [Extension];

    public override bool _RecognizePath(Resource resource, string path) => path.GetExtension() == Extension;

    public override Error _Save(Resource resource, string path, uint flags)
    {
        Saved++;
        return Error.Ok;
    }
}

/// <summary>C# checks of 02-refactoring-resource-format-limits. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue02RefactoringResourceFormatLimitsCheck : FeatureCheckCs
{
    private const int Count = 200;

    protected override string CheckName => "02-refactoring-resource-format-limits";

    protected override Task RunChecks()
    {
        CheckManyFormats();
        CheckOrder();
        return Task.CompletedTask;
    }

    private static int LoadedId(string path)
    {
        using var res = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        return res == null ? -1 : (int)res.GetMeta("loader_id", -1);
    }

    private void CheckManyFormats()
    {
        var loaders = new List<LimitLoaderCs>();
        var savers = new List<LimitSaverCs>();
        for (int i = 0; i < Count; i++)
        {
            var loader = new LimitLoaderCs { Extension = $"cslimit{i}", Id = i };
            ResourceLoader.AddResourceFormatLoader(loader);
            loaders.Add(loader);
            var saver = new LimitSaverCs { Extension = $"cslimit{i}" };
            ResourceSaver.AddResourceFormatSaver(saver);
            savers.Add(saver);
        }

        int loaded = 0;
        int saved = 0;
        using var res = new Resource();
        for (int i = 0; i < Count; i++)
        {
            string path = $"res://__format_limit.cslimit{i}";
            if (LoadedId(path) == i)
            {
                loaded++;
            }
            if (ResourceSaver.Save(res, path) == Error.Ok && savers[i].Saved == 1)
            {
                saved++;
            }
        }
        ExpectEqual(loaded, Count, "all 200 C# loaders are used (old limit: 64 incl. engine loaders)");
        ExpectEqual(saved, Count, "all 200 C# savers are used (old limit: 64 incl. engine savers)");
        Expect(System.Array.IndexOf(ResourceLoader.GetRecognizedExtensionsForType("Resource"), "cslimit199") >= 0, "extension of the 200th loader is recognized");

        for (int i = 0; i < Count; i++)
        {
            ResourceLoader.RemoveResourceFormatLoader(loaders[i]);
            ResourceSaver.RemoveResourceFormatSaver(savers[i]);
        }
        Expect(System.Array.IndexOf(ResourceLoader.GetRecognizedExtensionsForType("Resource"), "cslimit0") < 0, "removed loaders are gone");
    }

    private void CheckOrder()
    {
        var older = new LimitLoaderCs { Extension = "csorder", Id = 1 };
        var front = new LimitLoaderCs { Extension = "csorder", Id = 2 };
        ResourceLoader.AddResourceFormatLoader(older);
        ExpectEqual(LoadedId("res://__order.csorder"), 1, "the first registered loader wins");
        ResourceLoader.AddResourceFormatLoader(front, true);
        ExpectEqual(LoadedId("res://__order.csorder"), 2, "a loader added with at_front wins over older loaders");
        ResourceLoader.RemoveResourceFormatLoader(front);
        ExpectEqual(LoadedId("res://__order.csorder"), 1, "after removing it, the older loader wins again");
        ResourceLoader.RemoveResourceFormatLoader(older);
    }
}
