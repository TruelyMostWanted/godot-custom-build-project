using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue01FeatureResourcePathsRetrieval;

/// <summary>C# checks of 01-feature-resource-paths-retrieval. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue01FeatureResourcePathsRetrievalCheck : FeatureCheckCs
{
    private const string Dir = "res://01-feature-resource-paths-retrieval/resources/";
    private const string First = Dir + "first.tres";
    private const string Second = Dir + "second.tres";
    private const string Third = Dir + "third.tres";
    private const int ThreadCount = 4;
    private const int ThreadIterations = 2000;
    private const int PerfCount = 10000;

    private static readonly Regex ThreadPathRegex = new(@"^res://__cs_thread_\d+/\d+\.tres$");

    protected override string CheckName => "01-feature-resource-paths-retrieval";

    protected override async Task RunChecks()
    {
        CheckLoadedPaths();
        CheckFreedPaths();
        await CheckThreads();
        CheckPerformance();
    }

    private void CheckLoadedPaths()
    {
        Expect(ClassDB.ClassHasMethod("ResourceLoader", "get_cached_paths"), "ResourceLoader has get_cached_paths()");

        using var first = GD.Load<Resource>(First);
        using var second = GD.Load<Resource>(Second);
        using var third = GD.Load<Resource>(Third);
        string[] paths = ResourceLoader.GetCachedPaths();
        Expect(paths.Contains(First), "first.tres is in the cached paths");
        Expect(paths.Contains(Second), "second.tres is in the cached paths");
        Expect(paths.Contains(Third), "third.tres is in the cached paths");
        Expect(paths.Contains(Third + "::Resource_child"), "built-in sub-resource third.tres::Resource_child is in the cached paths");
        Expect(!paths.Contains(""), "no empty path in the cached paths");
        ExpectEqual(ResourceLoader.GetCachedPaths().Length, paths.Length, "two calls without changes give the same count");
        foreach (string path in new[] { First, Second, Third })
        {
            var cached = ResourceLoader.GetCachedRef(path);
            Expect(cached != null, $"GetCachedRef() resolves {path}");
            cached?.Dispose();
        }
        Expect(first != null && second != null && third != null, "all three resources loaded");
    }

    private void CheckFreedPaths()
    {
        // C# holds a managed wrapper; Dispose() drops the native reference right away instead of waiting for the GC.
        var res = GD.Load<Resource>(First);
        Expect(ResourceLoader.GetCachedPaths().Contains(First), "first.tres is cached while referenced");
        res.Dispose();
        Expect(!ResourceLoader.GetCachedPaths().Contains(First), "first.tres is gone after its last reference is freed");

        var scratch = new Resource();
        scratch.TakeOverPath("res://__cs_scratch.tres");
        Expect(ResourceLoader.GetCachedPaths().Contains("res://__cs_scratch.tres"), "TakeOverPath() adds the path");
        scratch.Dispose();
        Expect(!ResourceLoader.GetCachedPaths().Contains("res://__cs_scratch.tres"), "freed resource with TakeOverPath() is gone");
    }

    private async Task CheckThreads()
    {
        var workers = Enumerable.Range(0, ThreadCount).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < ThreadIterations; i++)
            {
                using var res = new Resource();
                res.TakeOverPath($"res://__cs_thread_{t}/{i}.tres");
            }
        })).ToArray();
        ResourceLoader.LoadThreadedRequest(Second);
        ResourceLoader.LoadThreadedRequest(Third);

        int calls = 0;
        int badPaths = 0;
        int seenThreadPaths = 0;
        bool running = true;
        while (running)
        {
            running = workers.Any(w => !w.IsCompleted);
            foreach (string path in ResourceLoader.GetCachedPaths())
            {
                if (!path.StartsWith("res://"))
                {
                    badPaths++;
                }
                else if (path.StartsWith("res://__cs_thread_"))
                {
                    seenThreadPaths++;
                    if (!ThreadPathRegex.IsMatch(path))
                    {
                        badPaths++;
                    }
                }
            }
            calls++;
        }
        await Task.WhenAll(workers);
        ResourceLoader.LoadThreadedGet(Second)?.Dispose();
        ResourceLoader.LoadThreadedGet(Third)?.Dispose();

        GD.Print($"  INFO  {calls} calls while {ThreadCount} threads changed the cache, {seenThreadPaths} thread paths seen");
        Expect(calls > 0, "GetCachedPaths() was called while threads were running");
        ExpectEqual(badPaths, 0, "no partial or malformed path while threads load");
        int leftovers = ResourceLoader.GetCachedPaths().Count(p => p.StartsWith("res://__cs_thread_"));
        ExpectEqual(leftovers, 0, "no thread path left after the threads freed their resources");
    }

    private void CheckPerformance()
    {
        var held = new List<Resource>(PerfCount);
        for (int i = 0; i < PerfCount; i++)
        {
            var res = new Resource();
            res.TakeOverPath($"res://__cs_perf/{i}.tres");
            held.Add(res);
        }

        const int runs = 10;
        ulong start = Time.GetTicksUsec();
        string[] paths = [];
        for (int i = 0; i < runs; i++)
        {
            paths = ResourceLoader.GetCachedPaths();
        }
        ulong avg = (Time.GetTicksUsec() - start) / runs;
        GD.Print($"  INFO  GetCachedPaths() with {paths.Length} cached paths: {avg} usec per call (average of {runs})");

        ExpectEqual(paths.Count(p => p.StartsWith("res://__cs_perf/")), PerfCount, "all 10,000 paths are returned");
        foreach (var res in held)
        {
            res.Dispose();
        }
        held.Clear();
        Expect(!ResourceLoader.GetCachedPaths().Contains("res://__cs_perf/0.tres"), "10,000 paths are gone after release");
    }
}
