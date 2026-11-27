using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.CachedResourcePaths;

/// <summary>Full API test of Cached Resource Paths (the GetCached* methods of ResourceLoader). Same tests as scripts/gd/Test.gd.
/// A resource wrapper is disposed to free the native resource at once; these wrappers are the ones this test created.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://cached-resource-paths/cs/";
    private const string Resources = "res://cached-resource-paths/resources/";
    private const string First = Resources + "first.tres";
    private const string Second = Resources + "second.tres";
    private const string Third = Resources + "third.tres";
    private const string ThirdChild = Third + "::Resource_child";
    private const int ThreadCount = 4;
    private const int ThreadIterations = 500;

    // Resources of the type, extension and directory tests, kept alive so that they stay in the cache.
    private readonly List<Resource> _held = [];
    private Resource _curve2;
    private string _textureSubPath = "";

    protected override async Task RunTests()
    {
        Prepare();
        TestCachedPaths();
        TestFreedPaths();
        TestType();
        TestClassName();
        TestExtension();
        TestDirectory();
        TestCounts();
        TestKinds();
        TestResourcesAndTypeCounts();
        TestFreedFilters();
        await TestThreads();
        Cleanup();
    }

    private static string P(string relative)
    {
        return Root + relative;
    }

    private static string[] Under(string[] paths)
    {
        return paths.Where(path => path.StartsWith(Root, StringComparison.Ordinal)).Order().ToArray();
    }

    private static string[] Sorted(params string[] paths)
    {
        return paths.Order().ToArray();
    }

    private void ExpectPaths(string[] actual, string[] expected, string message)
    {
        Expect(actual.SequenceEqual(expected), $"{message} (expected {string.Join(", ", expected)}; got {string.Join(", ", actual)})");
    }

    private Resource Save(Resource resource, string relative)
    {
        resource.ResourceName = relative;
        ResourceSaver.Save(resource, P(relative));
        return GD.Load(P(relative));
    }

    private void Prepare()
    {
        DirAccess.MakeDirRecursiveAbsolute(P("a/b"));
        _held.Add(Save(new Gradient(), "a/grad.tres"));
        _held.Add(Save(new Curve(), "a/curve.res"));
        _held.Add(Save(new StyleBoxFlat(), "a/flat.tres"));
        var texture = new GradientTexture1D { Gradient = new Gradient() };
        _held.Add(Save(texture, "a/tex.tres"));
        _held.Add(Save(new WidgetCs(), "a/b/widget.tres"));
        _curve2 = Save(new Curve(), "a/b/curve2.res");
        foreach (string path in ResourceLoader.GetCachedPaths())
        {
            if (path.StartsWith(P("a/tex.tres::"), StringComparison.Ordinal))
            {
                _textureSubPath = path;
            }
        }
        Expect(_textureSubPath.Length > 0, "the loaded texture has a built-in sub-resource path (a/tex.tres::id)");
    }

    private void TestCachedPaths()
    {
        Resource first = GD.Load(First);
        Resource second = GD.Load(Second);
        Resource third = GD.Load(Third);
        string[] paths = ResourceLoader.GetCachedPaths();
        Expect(paths.Contains(First), "first.tres is in the cached paths");
        Expect(paths.Contains(Second), "second.tres is in the cached paths");
        Expect(paths.Contains(Third), "third.tres is in the cached paths");
        Expect(paths.Contains(ThirdChild), "the built-in sub-resource of third.tres is in the cached paths");
        Expect(!paths.Contains(""), "no empty path in the cached paths");
        ExpectEqual(ResourceLoader.GetCachedPaths().Length, paths.Length, "two calls without changes give the same count");
        foreach (string path in new[] { First, Second, Third })
        {
            Resource cached = ResourceLoader.GetCachedRef(path);
            Expect(cached != null, "GetCachedRef resolves " + path);
        }
        Expect(ResourceLoader.GetCachedRef("res://no/such/file.tres") == null, "GetCachedRef of an unknown path is null");
        Expect(first != null && second != null && third != null, "all three resources loaded");
    }

    private void TestFreedPaths()
    {
        Resource resource = GD.Load(First);
        Expect(ResourceLoader.GetCachedPaths().Contains(First), "first.tres is cached while referenced");
        resource.Dispose();
        Expect(!ResourceLoader.GetCachedPaths().Contains(First), "first.tres is gone after its last reference is freed");

        var scratch = new Resource();
        scratch.TakeOverPath(P("scratch.tres"));
        Expect(ResourceLoader.GetCachedPaths().Contains(P("scratch.tres")), "TakeOverPath adds the path");
        scratch.Dispose();
        Expect(!ResourceLoader.GetCachedPaths().Contains(P("scratch.tres")), "a freed resource with TakeOverPath is gone");
    }

    private void TestType()
    {
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Gradient")), Sorted(P("a/grad.tres"), _textureSubPath), "type Gradient: the file and the sub-resource, not the texture");
        string[] curves = Sorted(P("a/curve.res"), P("a/b/curve2.res"));
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Curve")), curves, "type Curve: both curves");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Curve", true)), curves, "type Curve, exact: both curves");
        // StyleBoxFlat inherits StyleBox.
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("StyleBox")), [P("a/flat.tres")], "type StyleBox includes the subclass StyleBoxFlat");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("StyleBox", true)), [], "type StyleBox, exact: not the subclass StyleBoxFlat");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("StyleBoxFlat", true)), [P("a/flat.tres")], "type StyleBoxFlat, exact: the class itself");
        // GradientTexture1D inherits Texture2D.
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Texture2D")), [P("a/tex.tres")], "type Texture2D includes GradientTexture1D");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Texture2D", true)), [], "type Texture2D, exact: nothing");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("GradientTexture1D", true)), [P("a/tex.tres")], "type GradientTexture1D, exact: the texture");
        string[] everything = Sorted(P("a/grad.tres"), P("a/curve.res"), P("a/flat.tres"), P("a/tex.tres"), _textureSubPath, P("a/b/widget.tres"), P("a/b/curve2.res"));
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Resource")), everything, "type Resource: every loaded resource, including sub-resources and script classes");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Node")), [], "type Node: no resource");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("NoSuchClass")), [], "unknown type: nothing, no error");
    }

    private void TestClassName()
    {
        string[] widget = [P("a/b/widget.tres")];
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("WidgetCs")), widget, "GlobalClass: the script class matches");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("WidgetCs", true)), widget, "GlobalClass, exact: the script class matches itself");
        Expect(ResourceLoader.GetCachedPathsForType("Resource").Contains(P("a/b/widget.tres")), "a GlobalClass resource is also found by its native base class");
    }

    private void TestExtension()
    {
        string[] tres = Sorted(P("a/grad.tres"), P("a/flat.tres"), P("a/tex.tres"), _textureSubPath, P("a/b/widget.tres"));
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("tres")), tres, "extension tres: files and the sub-resource of a tres file");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("TRES")), tres, "extension TRES: case-insensitive");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("res")), Sorted(P("a/curve.res"), P("a/b/curve2.res")), "extension res: does not match tres");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("xyz")), [], "unknown extension: nothing");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("")), [], "empty extension: nothing under the test directory");
    }

    private void TestDirectory()
    {
        string[] inB = Sorted(P("a/b/widget.tres"), P("a/b/curve2.res"));
        string[] directA = Sorted(P("a/grad.tres"), P("a/curve.res"), P("a/flat.tres"), P("a/tex.tres"), _textureSubPath);
        string[] recursiveA = Sorted(directA.Concat(inB).ToArray());
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/b/"))), inB, "directory a/b/: its two resources");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/"))), recursiveA, "directory a/ is recursive by default");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/"), true)), recursiveA, "directory a/, recursive = true: includes a/b/");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/"), false)), directA, "directory a/, recursive = false: direct children only");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/b/"), false)), inB, "directory a/b/, recursive = false: its two resources");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("none/"))), [], "unknown directory: nothing");
        Expect(!ResourceLoader.GetCachedPathsInDirectory("res://").Contains(P("a/grad.tres")), "directory res:// does not return user:// paths");
    }

    private void TestCounts()
    {
        string directory = P("a/");
        var file = ResourceLoader.CachedResourceKind.File;
        var builtIn = ResourceLoader.CachedResourceKind.BuiltIn;
        foreach (ResourceLoader.CachedResourceKind kinds in new[] { file, builtIn, ResourceLoader.CachedResourceKind.All })
        {
            string message = " equals the size of its path list, kinds " + kinds;
            ExpectEqual(ResourceLoader.GetCachedCount(kinds), ResourceLoader.GetCachedPaths(kinds).Length, "GetCachedCount" + message);
            ExpectEqual(ResourceLoader.GetCachedCountForType("Curve", false, kinds), ResourceLoader.GetCachedPathsForType("Curve", false, kinds).Length, "GetCachedCountForType" + message);
            ExpectEqual(ResourceLoader.GetCachedCountWithExtension("tres", kinds), ResourceLoader.GetCachedPathsWithExtension("tres", kinds).Length, "GetCachedCountWithExtension" + message);
            ExpectEqual(ResourceLoader.GetCachedCountInDirectory(directory, true, kinds), ResourceLoader.GetCachedPathsInDirectory(directory, true, kinds).Length, "GetCachedCountInDirectory" + message);
        }
        ExpectEqual(ResourceLoader.GetCachedCountInDirectory(directory, true, file), 6, "six resources in their own files under a/");
    }

    private void TestKinds()
    {
        string directory = P("a/");
        var file = ResourceLoader.CachedResourceKind.File;
        var builtIn = ResourceLoader.CachedResourceKind.BuiltIn;
        var imported = ResourceLoader.CachedResourceKind.Imported;
        Expect(ResourceLoader.GetCachedPathsInDirectory(directory, true, builtIn).Contains(_textureSubPath), "the built-in sub-resource of tex.tres has the kind BuiltIn");
        Expect(!ResourceLoader.GetCachedPathsInDirectory(directory, true, file).Contains(_textureSubPath), "the built-in sub-resource does not have the kind File");
        GD.Load("res://icon.svg");
        Expect(ResourceLoader.GetCachedPaths(imported).Contains("res://icon.svg"), "res://icon.svg has the kind Imported");
        Expect(!ResourceLoader.GetCachedPaths(file).Contains("res://icon.svg"), "res://icon.svg does not have the kind File");
    }

    private void TestResourcesAndTypeCounts()
    {
        var file = ResourceLoader.CachedResourceKind.File;
        // The array keeps the resources in the cache while it exists: clear it after use.
        var cachedCurves = ResourceLoader.GetCachedResourcesForType("Curve", true, file);
        int curves = 0;
        foreach (Resource resource in cachedCurves)
        {
            if (resource.ResourcePath.StartsWith(Root, StringComparison.Ordinal))
            {
                curves++;
            }
        }
        cachedCurves.Clear();
        ExpectEqual(curves, 2, "GetCachedResourcesForType returns the two curves themselves");
        var typeCounts = ResourceLoader.GetCachedTypeCounts(file);
        Expect(typeCounts.ContainsKey(new StringName("Curve")) && (int)typeCounts[new StringName("Curve")] >= 2, "GetCachedTypeCounts counts the curves");
        Expect(typeCounts.ContainsKey(new StringName("WidgetCs")) && (int)typeCounts[new StringName("WidgetCs")] >= 1, "GetCachedTypeCounts uses the script class name");
    }

    private void TestFreedFilters()
    {
        string curve2 = P("a/b/curve2.res");
        _curve2.Dispose();
        Expect(!ResourceLoader.GetCachedPathsForType("Curve").Contains(curve2), "a freed resource is gone from the type filter");
        Expect(!ResourceLoader.GetCachedPathsWithExtension("res").Contains(curve2), "a freed resource is gone from the extension filter");
        Expect(!ResourceLoader.GetCachedPathsInDirectory(P("a/b/")).Contains(curve2), "a freed resource is gone from the directory filter");
        Expect(ResourceLoader.GetCachedPathsInDirectory(P("a/b/")).Contains(P("a/b/widget.tres")), "the other resource in the directory stays");
    }

    private async Task TestThreads()
    {
        Task[] workers = Enumerable.Range(0, ThreadCount).Select(index => Task.Run(() =>
        {
            for (int i = 0; i < ThreadIterations; i++)
            {
                var resource = new Resource();
                resource.TakeOverPath($"res://__cached_paths_thread_{index}/{i}.tres");
                resource.Dispose();
            }
        })).ToArray();
        var regex = new Regex(@"^res://__cached_paths_thread_\d+/\d+\.tres$");
        int calls = 0;
        int badPaths = 0;
        bool running = true;
        while (running)
        {
            running = workers.Any(worker => !worker.IsCompleted);
            foreach (string path in ResourceLoader.GetCachedPaths())
            {
                if (path.StartsWith("res://__cached_paths_thread_", StringComparison.Ordinal) && !regex.IsMatch(path))
                {
                    badPaths++;
                }
            }
            calls++;
        }
        await Task.WhenAll(workers);
        Expect(calls > 0, "GetCachedPaths was called while threads were running");
        ExpectEqual(badPaths, 0, "no partial or malformed path while threads create and free resources");
        ExpectEqual(ResourceLoader.GetCachedPathsInDirectory("res://__cached_paths_thread_0/").Length, 0, "no thread path is left after the threads freed their resources");
    }

    private void Cleanup()
    {
        foreach (Resource resource in _held)
        {
            resource.Dispose();
        }
        foreach (string directory in new[] { "a/b/", "a/", "" })
        {
            foreach (string file in DirAccess.GetFilesAt(P(directory)))
            {
                DirAccess.RemoveAbsolute(P(directory + file));
            }
        }
        DirAccess.RemoveAbsolute(P("a/b"));
        DirAccess.RemoveAbsolute(P("a"));
    }
}
