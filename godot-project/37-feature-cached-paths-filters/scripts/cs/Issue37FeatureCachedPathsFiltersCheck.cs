// Written before the engine build of #37; run after the merge.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue37FeatureCachedPathsFilters;

/// <summary>C# checks of 37-feature-cached-paths-filters. Check the same things as scripts/gd/check.gd.</summary>
// Assumption: directories are passed with a trailing slash; a directory result includes sub-resource paths (file::id) of files in it.
// Assumption: the exact flag is not checked for "Resource" itself, because the class of a script-class resource is its script class.
public partial class Issue37FeatureCachedPathsFiltersCheck : FeatureCheckCs
{
    private const string Root = "user://37-feature-cached-paths-filters/cs/";
    private const int PerfCount = 10000;
    private const int PerfHits = 100;
    private const int ThreadCount = 4;
    private const int ThreadIterations = 1500;

    private static readonly Regex ThreadPathRegex = new(@"^res://__cs_filter_thread_\d+/\d+\.curvex$");

    private readonly List<Resource> _held = [];
    private string _texSubPath = "";

    protected override string CheckName => "37-feature-cached-paths-filters";

    protected override async Task RunChecks()
    {
        Cleanup();
        Prepare();
        CheckApi();
        CheckType();
        CheckClassName();
        CheckExtension();
        CheckDirectory();
        CheckFreed();
        await CheckThreads();
        CheckPerformance();
        Cleanup();
    }

    private static string P(string relative) => Root + relative;

    private static string[] Under(string[] paths)
        => paths.Where(p => p.StartsWith(Root, StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal).ToArray();

    private static string[] Sorted(params string[] paths) => paths.OrderBy(p => p, StringComparer.Ordinal).ToArray();

    private bool ExpectPaths(string[] actual, string[] expected, string message)
        => Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");

    private void Save(Resource res, string relative)
    {
        Error err = ResourceSaver.Save(res, P(relative));
        ExpectEqual(err, Error.Ok, $"saved {relative}");
        res.Dispose();
    }

    private void Prepare()
    {
        DirAccess.MakeDirRecursiveAbsolute(P("a/b"));
        Save(new Gradient(), "a/grad.tres");
        Save(new Curve(), "a/curve.res");
        Save(new StyleBoxFlat(), "a/flat.tres");
        Save(new GradientTexture1D { Gradient = new Gradient() }, "a/tex.tres");
        Save(new Issue37CsWidget(), "a/b/widget.tres");
        Save(new Curve(), "a/b/curve2.res");

        foreach (string relative in new[] { "a/grad.tres", "a/curve.res", "a/flat.tres", "a/tex.tres", "a/b/widget.tres", "a/b/curve2.res" })
        {
            _held.Add(GD.Load<Resource>(P(relative)));
        }
        foreach (string path in ResourceLoader.GetCachedPaths())
        {
            if (path.StartsWith(P("a/tex.tres::"), StringComparison.Ordinal))
            {
                _texSubPath = path;
            }
        }
        Expect(_texSubPath != "", "the loaded texture has a built-in sub-resource path (a/tex.tres::<id>)");
    }

    private void CheckApi()
    {
        foreach (string method in new[] { "get_cached_paths_for_type", "get_cached_paths_with_extension", "get_cached_paths_in_directory" })
        {
            Expect(ClassDB.ClassHasMethod("ResourceLoader", method), $"ResourceLoader has {method}()");
        }
        Expect(ResourceLoader.GetCachedPathsForType("Curve") is string[], "GetCachedPathsForType() returns string[]");
    }

    private void CheckType()
    {
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Gradient")), Sorted(P("a/grad.tres"), _texSubPath), "type Gradient: the file and the sub-resource, not the texture");
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
        string[] all = Sorted(P("a/grad.tres"), P("a/curve.res"), P("a/flat.tres"), P("a/tex.tres"), _texSubPath, P("a/b/widget.tres"), P("a/b/curve2.res"));
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Resource")), all, "type Resource: every loaded resource, including sub-resources and script classes");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Node")), [], "type Node: no resource");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("NoSuchClass")), [], "unknown type: nothing, no error");
    }

    private void CheckClassName()
    {
        string[] widget = [P("a/b/widget.tres")];
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Issue37CsWidget")), widget, "[GlobalClass]: the script class matches");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsForType("Issue37CsWidget", true)), widget, "[GlobalClass], exact: the script class matches itself");
        Expect(ResourceLoader.GetCachedPathsForType("Resource").Contains(P("a/b/widget.tres")), "[GlobalClass] resource is also found by its native base class");
    }

    private void CheckExtension()
    {
        string[] tres = Sorted(P("a/grad.tres"), P("a/flat.tres"), P("a/tex.tres"), _texSubPath, P("a/b/widget.tres"));
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("tres")), tres, "extension tres: files and the sub-resource of a tres file");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("TRES")), tres, "extension TRES: case-insensitive");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("res")), Sorted(P("a/curve.res"), P("a/b/curve2.res")), "extension res: does not match tres");
        Expect(ResourceLoader.GetCachedPathsWithExtension("tres").Contains(_texSubPath), "a sub-resource counts as the extension of its file");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("xyz")), [], "unknown extension: nothing");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsWithExtension("")), [], "empty extension: nothing under the test directory");
    }

    private void CheckDirectory()
    {
        string[] inB = Sorted(P("a/b/widget.tres"), P("a/b/curve2.res"));
        string[] directA = Sorted(P("a/grad.tres"), P("a/curve.res"), P("a/flat.tres"), P("a/tex.tres"), _texSubPath);
        string[] recursiveA = Sorted(directA.Concat(inB).ToArray());
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/b/"))), inB, "directory a/b/: its two resources");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/"))), recursiveA, "directory a/ (recursive by default): includes a/b/");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/"), true)), recursiveA, "directory a/, recursive = true: includes a/b/");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/"), false)), directA, "directory a/, recursive = false: direct children only");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("a/b/"), false)), inB, "directory a/b/, recursive = false: its two resources");
        ExpectPaths(Under(ResourceLoader.GetCachedPathsInDirectory(P("none/"))), [], "unknown directory: nothing");
        Expect(!ResourceLoader.GetCachedPathsInDirectory("res://").Contains(P("a/grad.tres")), "directory res:// does not return user:// paths");
    }

    private void CheckFreed()
    {
        string curve2 = P("a/b/curve2.res");
        // C# holds a managed wrapper; Dispose() drops the native reference right away instead of waiting for the GC.
        _held[5].Dispose();
        Expect(!ResourceLoader.GetCachedPathsForType("Curve").Contains(curve2), "freed resource is gone from the type filter");
        Expect(!ResourceLoader.GetCachedPathsWithExtension("res").Contains(curve2), "freed resource is gone from the extension filter");
        Expect(!ResourceLoader.GetCachedPathsInDirectory(P("a/b/")).Contains(curve2), "freed resource is gone from the directory filter");
        Expect(ResourceLoader.GetCachedPathsInDirectory(P("a/b/")).Contains(P("a/b/widget.tres")), "the other resource in the directory stays");
    }

    private async Task CheckThreads()
    {
        var workers = Enumerable.Range(0, ThreadCount).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < ThreadIterations; i++)
            {
                using var res = new Curve();
                res.TakeOverPath($"res://__cs_filter_thread_{t}/{i}.curvex");
            }
        })).ToArray();

        int calls = 0;
        int bad = 0;
        bool running = true;
        while (running)
        {
            running = workers.Any(w => !w.IsCompleted);
            string[][] lists =
            [
                ResourceLoader.GetCachedPathsForType("Curve", true),
                ResourceLoader.GetCachedPathsWithExtension("curvex"),
                ResourceLoader.GetCachedPathsInDirectory("res://__cs_filter_thread_0/"),
            ];
            foreach (string[] list in lists)
            {
                foreach (string path in list)
                {
                    if (path.StartsWith("res://__cs_filter_thread_", StringComparison.Ordinal) && !ThreadPathRegex.IsMatch(path))
                    {
                        bad++;
                    }
                }
            }
            calls++;
        }
        await Task.WhenAll(workers);

        GD.Print($"  INFO  {calls} rounds of the three filters while {ThreadCount} threads changed the cache");
        Expect(calls > 0, "the filters were called while threads were running");
        ExpectEqual(bad, 0, "no partial or malformed path while threads create and free resources");
        ExpectEqual(ResourceLoader.GetCachedPathsWithExtension("curvex").Length, 0, "no thread path left after the threads freed their resources");
    }

    private void CheckPerformance()
    {
        var held = new List<Resource>(PerfCount);
        int step = PerfCount / PerfHits;
        for (int i = 0; i < PerfCount; i++)
        {
            Resource res;
            string path;
            if (i % step == 0)
            {
                res = new Curve();
                path = $"res://__cs_filter_perf/hit/{i}.perfx";
            }
            else
            {
                res = new Resource();
                path = $"res://__cs_filter_perf/miss/{i}.tres";
            }
            res.TakeOverPath(path);
            held.Add(res);
        }

        const int runs = 10;
        // By type: the script alternative has to fetch every resource.
        ulong start = Time.GetTicksUsec();
        string[] byType = [];
        for (int i = 0; i < runs; i++)
        {
            byType = ResourceLoader.GetCachedPathsForType("Curve", true);
        }
        ulong filteredType = (Time.GetTicksUsec() - start) / runs;
        start = Time.GetTicksUsec();
        int scriptType = 0;
        foreach (string path in ResourceLoader.GetCachedPaths())
        {
            using var cached = ResourceLoader.GetCachedRef(path);
            if (cached != null && cached.GetClass() == "Curve")
            {
                scriptType++;
            }
        }
        ulong scriptTypeUsec = Time.GetTicksUsec() - start;

        start = Time.GetTicksUsec();
        string[] byExt = [];
        for (int i = 0; i < runs; i++)
        {
            byExt = ResourceLoader.GetCachedPathsWithExtension("perfx");
        }
        ulong filteredExt = (Time.GetTicksUsec() - start) / runs;
        start = Time.GetTicksUsec();
        int scriptExt = 0;
        for (int i = 0; i < runs; i++)
        {
            scriptExt = ResourceLoader.GetCachedPaths().Count(p => p.GetExtension().ToLowerInvariant() == "perfx");
        }
        ulong scriptExtUsec = (Time.GetTicksUsec() - start) / runs;

        start = Time.GetTicksUsec();
        string[] byDir = [];
        for (int i = 0; i < runs; i++)
        {
            byDir = ResourceLoader.GetCachedPathsInDirectory("res://__cs_filter_perf/hit/");
        }
        ulong filteredDir = (Time.GetTicksUsec() - start) / runs;
        start = Time.GetTicksUsec();
        int scriptDir = 0;
        for (int i = 0; i < runs; i++)
        {
            scriptDir = ResourceLoader.GetCachedPaths().Count(p => p.StartsWith("res://__cs_filter_perf/hit/", StringComparison.Ordinal));
        }
        ulong scriptDirUsec = (Time.GetTicksUsec() - start) / runs;

        GD.Print($"  INFO  {ResourceLoader.GetCachedPaths().Length} cached paths, {PerfHits} matches");
        GD.Print($"  INFO  by type:      filtered {filteredType} usec, GetCachedPaths() + GetCachedRef() in C# {scriptTypeUsec} usec (one run)");
        GD.Print($"  INFO  by extension: filtered {filteredExt} usec, GetCachedPaths() + C# filter {scriptExtUsec} usec");
        GD.Print($"  INFO  by directory: filtered {filteredDir} usec, GetCachedPaths() + C# filter {scriptDirUsec} usec");
        ExpectEqual(byType.Length, PerfHits, "type filter returns the 100 matches of 10,000 paths");
        ExpectEqual(byExt.Length, PerfHits, "extension filter returns the 100 matches of 10,000 paths");
        ExpectEqual(byDir.Length, PerfHits, "directory filter returns the 100 matches of 10,000 paths");
        Expect(scriptType == PerfHits && scriptExt == PerfHits && scriptDir == PerfHits, "the C# filters find the same 100 matches");
        foreach (var res in held)
        {
            res.Dispose();
        }
        held.Clear();
        ExpectEqual(ResourceLoader.GetCachedPathsInDirectory("res://__cs_filter_perf/").Length, 0, "10,000 paths are gone after release");
    }

    private void Cleanup()
    {
        foreach (var res in _held)
        {
            res?.Dispose();
        }
        _held.Clear();
        RemoveRecursive(ProjectSettings.GlobalizePath(Root));
    }

    private static void RemoveRecursive(string absolute)
    {
        using var dir = DirAccess.Open(absolute);
        if (dir == null)
        {
            return;
        }
        foreach (string sub in dir.GetDirectories())
        {
            RemoveRecursive(absolute.PathJoin(sub));
        }
        foreach (string file in dir.GetFiles())
        {
            dir.Remove(file);
        }
        DirAccess.RemoveAbsolute(absolute);
    }
}
