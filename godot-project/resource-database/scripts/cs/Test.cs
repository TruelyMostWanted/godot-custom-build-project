using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using Database = Godot.ResourceDatabase;

namespace TruelyInteractive.ResourceDatabase;

/// <summary>Full API test of Resource Database (ResourceDatabase and PreloadList). Same tests as scripts/gd/Test.gd.
/// The alias Database avoids the clash between the namespace and the ResourceDatabase class.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://resource-database/cs/";
    private const string ItemType = "ItemCs";
    private const int ThreadCount = 4;
    private const int ItemsPerThread = 100;

    private readonly List<(PreloadList List, Error Error)> _finished = [];
    private string _swordIron;
    private string _shield;
    private string _swordGold;
    private string _potion;
    private string _arrow;

    protected override async Task RunTests()
    {
        Database.Clear();
        Prepare();
        Database.Singleton.PreloadFinished += OnPreloadFinished;
        PreloadList list = MakeList();
        TestPreloadList(list);
        await TestPreloadFromList(list);
        TestLookup();
        TestFind();
        TestAddAndRemoveErrors();
        await TestStaysInMemory();
        await TestThreadedPreload();
        Database.Singleton.PreloadFinished -= OnPreloadFinished;
        TestIfMissingMemoryAndRemove();
        await TestParallelAddAndFind();
        TestClear();
        Cleanup();
    }

    private static ItemCs Item(string itemName)
    {
        return new ItemCs { ResourceName = itemName };
    }

    private static string Save(string file)
    {
        string path = Root + file;
        ResourceSaver.Save(Item(file.GetFile().GetBaseName()), path);
        return path;
    }

    private void Prepare()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root + "manifest");
        _swordIron = Save("sword_iron.tres");
        _shield = Save("shield.tres");
        _swordGold = Save("manifest/sword_gold.tres");
        Save("manifest/helmet.tres");
        _potion = Save("potion.tres");
        _arrow = Save("arrow.tres");
    }

    // A list with 3 resources, 2 paths and 1 manifest of 2 files.
    private PreloadList MakeList()
    {
        var manifest = new FilesManifest { FolderPaths = [Root + "manifest"], Extensions = ["tres"] };
        return new PreloadList
        {
            Resources = [Item("sword_wood"), Item("bow"), Item("ring")],
            Paths = [_swordIron, _shield],
            Manifests = [manifest],
            Keys = new Godot.Collections.Dictionary<string, string> { { "hero_sword", _swordIron } },
        };
    }

    private void OnPreloadFinished(PreloadList list, Error error)
    {
        _finished.Add((list, error));
    }

    private void TestPreloadList(PreloadList list)
    {
        Expect(!list.IsEmpty(), "PreloadList.IsEmpty is false for a filled list");
        Expect(new PreloadList().IsEmpty(), "PreloadList.IsEmpty is true for a new list");
        ExpectEqual(list.Resources.Count, 3, "PreloadList.Resources holds the three resources");
        ExpectEqual(list.Paths.Length, 2, "PreloadList.Paths holds the two paths");
        ExpectEqual(list.Manifests.Count, 1, "PreloadList.Manifests holds the manifest");
        ExpectEqual(list.Keys.Count, 1, "PreloadList.Keys holds the key");
    }

    private async Task TestPreloadFromList(PreloadList list)
    {
        Expect(Engine.HasSingleton("ResourceDatabase"), "the ResourceDatabase singleton exists");
        ExpectEqual(Database.PreloadFromList(list, false), Error.Ok, "PreloadFromList without threads returns Ok");
        ExpectEqual(Database.GetEntryCount(), 7L, "GetEntryCount: 3 resources + 2 paths + 2 manifest files");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(_finished.Count, 1, "PreloadFinished is emitted once on the main thread");
        Expect(_finished[0].List == list, "PreloadFinished passes the list");
        ExpectEqual(_finished[0].Error, Error.Ok, "PreloadFinished passes Ok");
    }

    private void TestLookup()
    {
        Resource byPath = Database.GetResource(_swordIron);
        Expect(byPath != null, "GetResource finds a resource by path");
        Expect(Database.GetResource("hero_sword") == byPath, "GetResource finds a resource by key");
        Expect(Database.HasResource("sword_wood"), "HasResource finds a direct resource by name");
        Expect(Database.HasResource(_swordGold), "HasResource finds a manifest file by path");
        Expect(!Database.HasResource("no_such_key"), "HasResource is false for an unknown key");
        Expect(Database.GetResource("no_such_key") == null, "GetResource of an unknown key is null");
        Expect(GD.Load(_swordIron) == byPath, "ResourceLoader.Load returns the stored instance");
        Expect(GD.Load("hero_sword") == null, "a key that is not a path is not found by Load");
    }

    private void TestFind()
    {
        ExpectEqual(Database.FindResources("", ItemType).Count, 7, "FindResources with an empty pattern and the class name finds all 7");
        ExpectEqual(Database.FindResources("", "Resource").Count, 7, "FindResources with the base class finds all 7");
        Database.AddResource("sword_texture", new Resource());
        ExpectEqual(Database.FindResources("%sword%", ItemType).Count, 3, "FindResources %sword% with the type finds 3 swords, not the plain Resource");
        ExpectEqual(Database.FindResources("%sword%").Count, 4, "FindResources %sword% without a type finds 4");
        string[] keys = Database.FindKeys("%SWORD%", ItemType);
        Expect(keys.Contains("sword_wood"), "FindKeys is case-insensitive: sword_wood");
        Expect(keys.Contains(_swordIron), "FindKeys is case-insensitive: sword_iron path");
        Expect(keys.Contains(_swordGold), "FindKeys is case-insensitive: sword_gold path");
        Expect(!keys.Contains("sword_texture"), "FindKeys with the type skips the plain Resource");
    }

    private void TestAddAndRemoveErrors()
    {
        ExpectEqual(Database.AddResource("", new Resource()), Error.InvalidParameter, "AddResource with an empty key is rejected");
        ExpectEqual(Database.AddResource("null_item", null), Error.InvalidParameter, "AddResource with a null resource is rejected");
        ExpectEqual(Database.RemoveMatching("no_such_%"), 0L, "RemoveMatching of an unknown pattern removes nothing");
    }

    // Resources stay in memory after the scene that preloaded them is freed.
    private async Task TestStaysInMemory()
    {
        var scene = new Node();
        AddChild(scene);
        ExpectEqual(Database.PreloadResource(_potion, "potion_key"), Error.Ok, "PreloadResource from a child scene");
        scene.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(ResourceLoader.HasCached(_potion), "the resource stays cached after the scene is freed");
        Expect(Database.GetResource("potion_key") != null, "the resource is still found by key");
    }

    private async Task TestThreadedPreload()
    {
        var list = new PreloadList { Paths = [_arrow] };
        _finished.Clear();
        ExpectEqual(Database.PreloadFromList(list), Error.Ok, "PreloadFromList with threads returns Ok");
        ulong end = Time.GetTicksMsec() + 5000;
        while (_finished.Count == 0 && Time.GetTicksMsec() < end)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        ExpectEqual(_finished.Count, 1, "PreloadFinished is emitted for the threaded list");
        Expect(_finished[0].List == list, "PreloadFinished passes the threaded list");
        Expect(Database.HasResource(_arrow), "the threaded preload stored the resource");
        Expect(!Database.IsPreloading(), "IsPreloading is false afterwards");
    }

    private void TestIfMissingMemoryAndRemove()
    {
        Expect(!Database.AddResourceIfMissing("hero_sword", Item("other")), "AddResourceIfMissing keeps an existing key");
        Expect(Database.AddResourceIfMissing("new_key", Item("new")), "AddResourceIfMissing stores a new key");
        Expect(Database.GetMemoryUsage() > 0, "GetMemoryUsage is above 0");
        ExpectEqual(Database.RemoveMatching("%sword%"), 4L, "RemoveMatching %sword% removes 4 entries");
        Expect(!Database.HasResource("hero_sword"), "the key is removed with its entry");
    }

    private async Task TestParallelAddAndFind()
    {
        Database.Clear();
        var failures = new List<string>();
        Task[] workers = Enumerable.Range(0, ThreadCount).Select(index => Task.Run(() =>
        {
            for (int i = 0; i < ItemsPerThread; i++)
            {
                string key = $"thread{index}/item{i}";
                if (Database.AddResource(key, Item(key)) != Error.Ok)
                {
                    lock (failures)
                    {
                        failures.Add(key);
                    }
                }
                if (Database.FindResources($"thread{index}/%").Count != i + 1)
                {
                    lock (failures)
                    {
                        failures.Add("find " + key);
                    }
                }
            }
        })).ToArray();
        await Task.WhenAll(workers);
        Expect(failures.Count == 0, "4 threads: no failed add or find");
        ExpectEqual(Database.GetEntryCount(), (long)(ThreadCount * ItemsPerThread), "4 threads: no lost entry");
    }

    private void TestClear()
    {
        Database.Clear();
        ExpectEqual(Database.GetEntryCount(), 0L, "Clear removes every entry");
    }

    private void Cleanup()
    {
        foreach (string file in new[] { "sword_iron.tres", "shield.tres", "manifest/sword_gold.tres", "manifest/helmet.tres", "potion.tres", "arrow.tres" })
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
        DirAccess.RemoveAbsolute(Root + "manifest");
        DirAccess.RemoveAbsolute(Root);
    }
}
