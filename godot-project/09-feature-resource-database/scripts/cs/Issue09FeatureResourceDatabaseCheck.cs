using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue09FeatureResourceDatabase;

/// <summary>C# checks of 09-feature-resource-database. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue09FeatureResourceDatabaseCheck : FeatureCheckCs
{
    private const string Root = "user://09-feature-resource-database/cs/";
    private const string TypeName = "Issue09CsItem";
    private readonly List<(PreloadList List, Error Error)> _finished = new();

    protected override string CheckName => "09-feature-resource-database";

    private static Issue09CsItem Item(string name) => new() { ResourceName = name };

    private static string Save(string file)
    {
        string path = Root + file;
        ResourceSaver.Save(Item(file.GetFile().GetBaseName()), path);
        return path;
    }

    private void OnPreloadFinished(PreloadList list, Error error) => _finished.Add((list, error));

    protected override async Task RunChecks()
    {
        Expect(Engine.HasSingleton("ResourceDatabase"), "ResourceDatabase singleton exists");
        ResourceDatabase.Clear();
        DirAccess.MakeDirRecursiveAbsolute(Root + "manifest");
        string swordIron = Save("sword_iron.tres");
        string shield = Save("shield.tres");
        string swordGold = Save("manifest/sword_gold.tres");
        Save("manifest/helmet.tres");
        string potion = Save("potion.tres");
        string arrow = Save("arrow.tres");

        // A list with 3 resources, 2 paths and 1 manifest.
        var manifest = new FilesManifest
        {
            FolderPaths = [Root + "manifest"],
            Extensions = ["tres"],
        };
        var list = new PreloadList
        {
            Resources = [Item("sword_wood"), Item("bow"), Item("ring")],
            Paths = [swordIron, shield],
            Manifests = [manifest],
            Keys = new Godot.Collections.Dictionary<string, string> { { "hero_sword", swordIron } },
        };
        Expect(!list.IsEmpty(), "PreloadList.IsEmpty() is false");

        ResourceDatabase.Singleton.PreloadFinished += OnPreloadFinished;
        ExpectEqual(ResourceDatabase.PreloadFromList(list, false), Error.Ok, "PreloadFromList() synchronous");
        ExpectEqual(ResourceDatabase.GetEntryCount(), 7, "GetEntryCount(): 3 resources + 2 paths + 2 manifest files");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(_finished.Count == 1 && _finished[0].List == list && _finished[0].Error == Error.Ok, "PreloadFinished(list, Ok) on the main thread");

        // Found by path, key, name and type.
        Expect(ResourceDatabase.GetResource(swordIron) != null, "found by path");
        Expect(ResourceDatabase.GetResource("hero_sword") == ResourceDatabase.GetResource(swordIron), "found by key");
        Expect(ResourceDatabase.HasResource("sword_wood"), "direct resource found by name");
        Expect(ResourceDatabase.HasResource(swordGold), "manifest file found by path");
        ExpectEqual(ResourceDatabase.FindResources("", TypeName).Count, 7, "FindResources('', Issue09CsItem): all 7");
        ExpectEqual(ResourceDatabase.FindResources("", "Resource").Count, 7, "FindResources('', Resource): inherited classes match");

        // Load() returns the stored instance.
        Expect(ResourceLoader.Load(swordIron) == ResourceDatabase.GetResource(swordIron), "ResourceLoader.Load() returns the stored instance");

        // FindResources("%sword%", type) returns the matching resources only.
        ResourceDatabase.AddResource("sword_texture", new Resource());
        ExpectEqual(ResourceDatabase.FindResources("%sword%", TypeName).Count, 3, "FindResources('%sword%', Issue09CsItem): 3 swords, not the plain Resource");
        string[] keys = ResourceDatabase.FindKeys("%SWORD%", TypeName);
        Expect(keys.Contains("sword_wood") && keys.Contains(swordIron) && keys.Contains(swordGold), "FindKeys() is case-insensitive");
        ExpectEqual(ResourceDatabase.FindResources("%sword%").Count, 4, "FindResources('%sword%') without type: 4");

        // Resources stay in memory after the scene that preloaded them is freed.
        var scene = new Node();
        AddChild(scene);
        ExpectEqual(ResourceDatabase.PreloadResource(potion, "potion_key"), Error.Ok, "PreloadResource() from a child scene");
        scene.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(ResourceLoader.HasCached(potion), "resource stays cached after the scene is freed");
        Expect(ResourceDatabase.GetResource("potion_key") != null, "resource still found by key");

        // Threaded preload.
        var list2 = new PreloadList { Paths = [arrow] };
        _finished.Clear();
        ExpectEqual(ResourceDatabase.PreloadFromList(list2), Error.Ok, "PreloadFromList() with threads");
        ulong end = Time.GetTicksMsec() + 5000;
        while (_finished.Count == 0 && Time.GetTicksMsec() < end)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(_finished.Count == 1 && _finished[0].List == list2, "PreloadFinished for the threaded list");
        Expect(ResourceDatabase.HasResource(arrow), "threaded preload stored the resource");
        Expect(!ResourceDatabase.IsPreloading(), "IsPreloading() false afterwards");
        ResourceDatabase.Singleton.PreloadFinished -= OnPreloadFinished;

        // AddResourceIfMissing, memory, RemoveMatching.
        Expect(!ResourceDatabase.AddResourceIfMissing("hero_sword", Item("other")), "AddResourceIfMissing() keeps an existing key");
        Expect(ResourceDatabase.GetMemoryUsage() > 0, "GetMemoryUsage() > 0");
        ExpectEqual(ResourceDatabase.RemoveMatching("%sword%"), 4, "RemoveMatching('%sword%') removes 4 entries");
        Expect(!ResourceDatabase.HasResource("hero_sword"), "key removed with its entry");

        // Parallel add and find from 4 threads.
        ResourceDatabase.Clear();
        int failures = 0;
        var tasks = Enumerable.Range(0, 4).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                string key = $"thread{t}/item{i}";
                if (ResourceDatabase.AddResource(key, Item(key)) != Error.Ok)
                {
                    Interlocked.Increment(ref failures);
                }
                if (ResourceDatabase.FindResources($"thread{t}/%").Count != i + 1)
                {
                    Interlocked.Increment(ref failures);
                }
            }
        })).ToArray();
        await Task.WhenAll(tasks);
        ExpectEqual(failures, 0, "4 threads: no failed add or find");
        ExpectEqual(ResourceDatabase.GetEntryCount(), 400, "4 threads: no lost entry");

        ResourceDatabase.Clear();
        ExpectEqual(ResourceDatabase.GetEntryCount(), 0, "Clear()");
        foreach (string f in new[] { "sword_iron.tres", "shield.tres", "manifest/sword_gold.tres", "manifest/helmet.tres", "potion.tres", "arrow.tres" })
        {
            DirAccess.RemoveAbsolute(Root + f);
        }
        foreach (string d in new[] { "manifest", "" })
        {
            DirAccess.RemoveAbsolute(Root + d);
        }
    }
}
