using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ResourceFormatLimits;

/// <summary>Full API test of Resource Format Limits (ResourceLoader and ResourceSaver registration). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    // Upstream Godot stops at 64 loaders and 64 savers, engine formats included.
    private const int Count = 200;

    protected override Task RunTests()
    {
        TestManyFormats();
        TestOrder();
        return Task.CompletedTask;
    }

    private static int LoadedId(string path)
    {
        Resource resource = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        if (resource == null)
        {
            return -1;
        }
        return (int)resource.GetMeta("loader_id", -1);
    }

    private void TestManyFormats()
    {
        var loaders = new List<MyLoader>();
        var savers = new List<MySaver>();
        for (int i = 0; i < Count; i++)
        {
            var loader = new MyLoader { Extension = $"cslimit{i}", Id = i };
            ResourceLoader.AddResourceFormatLoader(loader);
            loaders.Add(loader);
            var saver = new MySaver { Extension = $"cslimit{i}" };
            ResourceSaver.AddResourceFormatSaver(saver);
            savers.Add(saver);
        }

        int loaded = 0;
        int saved = 0;
        var resource = new Resource();
        for (int i = 0; i < Count; i++)
        {
            string path = $"res://__format_limit.cslimit{i}";
            if (LoadedId(path) == i)
            {
                loaded++;
            }
            Error error = ResourceSaver.Save(resource, path);
            if (error == Error.Ok && savers[i].Saved == 1)
            {
                saved++;
            }
        }
        ExpectEqual(loaded, Count, "all 200 loaders are used");
        ExpectEqual(saved, Count, "all 200 savers are used");
        string[] extensions = ResourceLoader.GetRecognizedExtensionsForType("Resource");
        Expect(extensions.Contains("cslimit199"), "extension of the 200th loader is recognized");

        for (int i = 0; i < Count; i++)
        {
            ResourceLoader.RemoveResourceFormatLoader(loaders[i]);
            ResourceSaver.RemoveResourceFormatSaver(savers[i]);
        }
        extensions = ResourceLoader.GetRecognizedExtensionsForType("Resource");
        Expect(!extensions.Contains("cslimit0"), "removed loaders are gone");
    }

    private void TestOrder()
    {
        var older = new MyLoader { Extension = "csorder", Id = 1 };
        var front = new MyLoader { Extension = "csorder", Id = 2 };
        ResourceLoader.AddResourceFormatLoader(older);
        ExpectEqual(LoadedId("res://__order.csorder"), 1, "the first registered loader wins");
        ResourceLoader.AddResourceFormatLoader(front, true);
        ExpectEqual(LoadedId("res://__order.csorder"), 2, "a loader added with at_front wins over older loaders");
        ResourceLoader.RemoveResourceFormatLoader(front);
        ExpectEqual(LoadedId("res://__order.csorder"), 1, "after removing it, the older loader wins again");
        ResourceLoader.RemoveResourceFormatLoader(older);
    }
}
