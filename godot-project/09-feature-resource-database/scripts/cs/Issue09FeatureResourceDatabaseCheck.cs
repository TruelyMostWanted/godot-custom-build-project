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
    private readonly List<(PreloadList List, long Error)> _finished = new();

    protected override string CheckName => "09-feature-resource-database";

    private static Issue09CsItem Item(string name) => new() { ResourceName = name };

    private static string Save(string file)
    {
        string path = Root + file;
        ResourceSaver.Save(Item(file.GetFile().GetBaseName()), path);
        return path;
    }

    private void OnPreloadFinished(PreloadList list, long error) => _finished.Add((list, error));

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
        ExpectEqual(ResourceDatabase.PreloadList(list, false), Error.Ok, "PreloadList() synchronous");
        ExpectEqual(ResourceDatabase.Count(), 7, "Count(): 3 resources + 2 paths + 2 manifest files");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(_finished.Count == 1 && _finished[0].List == list && _finished[0].Error == (long)Error.Ok, "PreloadFinished(list, Ok) on the main thread");

        // Found by path, key, name and type.
        Expect(ResourceDatabase.GetResource(swordIron) != null, "found by path");
        Expect(ResourceDatabase.GetResource("hero_sword") == ResourceDatabase.GetResource(swordIron), "found by key");
        Expect(ResourceDatabase.HasResource("sword_wood"), "direct resource found by name");
        Expect(ResourceDatabase.HasResource(swordGold), "manifest file found by path");
        ExpectEqual(ResourceDatabase.Select("", TypeName).Count, 7, "Select('', Issue09CsItem): all 7");
        ExpectEqual(ResourceDatabase.Select("", "Resource").Count, 7, "Select('', Resource): inherited classes match");

        // Load() returns the stored instance.
        Expect(ResourceLoader.Load(swordIron) == ResourceDatabase.GetResource(swordIron), "ResourceLoader.Load() returns the stored instance");

        // Select("%sword%", type) returns the matching resources only.
        ResourceDatabase.Insert("sword_texture", new Resource());
        ExpectEqual(ResourceDatabase.Select("%sword%", TypeName).Count, 3, "Select('%sword%', Issue09CsItem): 3 swords, not the plain Resource");
        string[] keys = ResourceDatabase.SelectKeys("%SWORD%", TypeName);
        Expect(keys.Contains("sword_wood") && keys.Contains(swordIron) && keys.Contains(swordGold), "SelectKeys() is case-insensitive");
        ExpectEqual(ResourceDatabase.Select("%sword%").Count, 4, "Select('%sword%') without type: 4");

        // Resources stay in memory after the scene that preloaded them is freed.
        var scene = new Node();
        AddChild(scene);
        ExpectEqual(ResourceDatabase.PreloadPath(potion, "potion_key"), Error.Ok, "PreloadPath() from a child scene");
        scene.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(ResourceLoader.HasCached(potion), "resource stays cached after the scene is freed");
        Expect(ResourceDatabase.GetResource("potion_key") != null, "resource still found by key");

        // Threaded preload.
        var list2 = new PreloadList { Paths = [arrow] };
        _finished.Clear();
        ExpectEqual(ResourceDatabase.PreloadList(list2), Error.Ok, "PreloadList() with threads");
        ulong end = Time.GetTicksMsec() + 5000;
        while (_finished.Count == 0 && Time.GetTicksMsec() < end)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(_finished.Count == 1 && _finished[0].List == list2, "PreloadFinished for the threaded list");
        Expect(ResourceDatabase.HasResource(arrow), "threaded preload stored the resource");
        Expect(!ResourceDatabase.IsPreloading(), "IsPreloading() false afterwards");
        ResourceDatabase.Singleton.PreloadFinished -= OnPreloadFinished;

        // InsertIfAbsent, memory, remove.
        Expect(!ResourceDatabase.InsertIfAbsent("hero_sword", Item("other")), "InsertIfAbsent() keeps an existing key");
        Expect(ResourceDatabase.GetMemoryUsage() > 0, "GetMemoryUsage() > 0");
        ExpectEqual(ResourceDatabase.Remove("%sword%"), 4, "Remove('%sword%') removes 4 entries");
        Expect(!ResourceDatabase.HasResource("hero_sword"), "key removed with its entry");

        // Parallel insert and select from 4 threads.
        ResourceDatabase.Clear();
        int failures = 0;
        var tasks = Enumerable.Range(0, 4).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                string key = $"thread{t}/item{i}";
                if (ResourceDatabase.Insert(key, Item(key)) != Error.Ok)
                {
                    Interlocked.Increment(ref failures);
                }
                if (ResourceDatabase.Select($"thread{t}/%").Count != i + 1)
                {
                    Interlocked.Increment(ref failures);
                }
            }
        })).ToArray();
        await Task.WhenAll(tasks);
        ExpectEqual(failures, 0, "4 threads: no failed insert or select");
        ExpectEqual(ResourceDatabase.Count(), 400, "4 threads: no lost entry");

        ResourceDatabase.Clear();
        ExpectEqual(ResourceDatabase.Count(), 0, "Clear()");
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
