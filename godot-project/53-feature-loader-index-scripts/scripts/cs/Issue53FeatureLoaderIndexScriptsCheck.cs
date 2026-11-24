// Written before the engine build of #53; run after the merge.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue53FeatureLoaderIndexScripts;

/// <summary>C# checks of 53-feature-loader-index-scripts. Check the same things as scripts/gd/check.gd.</summary>
// Script loaders that override only _GetRecognizedExtensions() are now found through the extension index,
// and the binary loader too. The choice of the loader must stay as it was (registration order, first match wins).
// A loader that changes its list calls ResourceLoader.RefreshFormatIndex().
// Assumption: a loader reports its identity through ResourceName (load) and through the type name it returns (GetResourceType).
// Assumption: res://icon.svg is an imported asset of this project (icon.svg.import), so it stands for the .import path.
public partial class Issue53FeatureLoaderIndexScriptsCheck : FeatureCheckCs
{
    private const string Root = "user://53-feature-loader-index-scripts/cs/";
    private const int PerfCount = 10000;
    private const int ExtraLoaders = 100;

    private const string One = Root + "one.t53x";
    private const string Upper = Root + "UPPER.T53X";
    private const string Old = Root + "data.t53old";
    private const string New = Root + "data.t53new";
    private const string Dotted = Root + "archive.pack.t53";
    private const string PlainDotted = Root + "archive.t53";
    private const string Special = Root + "item_special.dat";
    private const string PlainTres = Root + "plain.tres";
    private const string PlainRes = Root + "plain.res";

    protected override string CheckName => "53-feature-loader-index-scripts";

    protected override Task RunChecks()
    {
        Cleanup();
        Prepare();
        CheckBuiltin();
        CheckOrder();
        CheckDotted();
        CheckPathBased();
        CheckRefresh();
        CheckPerformance();
        Cleanup();
        return Task.CompletedTask;
    }

    private static Issue53ExtLoader ExtLoader(string tag, params string[] extensions)
        => new() { Tag = tag, Extensions = extensions, TypeName = "Type" + tag };

    private static Issue53PathLoader PathLoader(string tag, string ending)
        => new() { Tag = tag, PathEnding = ending, TypeName = "Type" + tag };

    private static string Winner(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            return "<none>";
        }
        var res = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        return res == null ? "<null>" : res.ResourceName;
    }

    private static string Type(string path) => ResourceLoader.GetResourceType(path);

    private static void WriteFile(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString("x");
    }

    private void Prepare()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        foreach (string path in new[] { One, Upper, Old, New, Dotted, PlainDotted, Special })
        {
            WriteFile(path);
        }
        using var gradient = new Gradient();
        using var curve = new Curve();
        ExpectEqual(ResourceSaver.Save(gradient, PlainTres), Error.Ok, "saved plain.tres");
        ExpectEqual(ResourceSaver.Save(curve, PlainRes), Error.Ok, "saved plain.res");
    }

    private void CheckBuiltin()
    {
        var tres = ResourceLoader.Load(PlainTres, "", ResourceLoader.CacheMode.Ignore);
        var res = ResourceLoader.Load(PlainRes, "", ResourceLoader.CacheMode.Ignore);
        Expect(tres is Gradient, "plain.tres loads as Gradient with the built-in text loader");
        Expect(res is Curve, "plain.res loads as Curve with the binary loader, which is now indexed");
        ExpectEqual(Type(PlainTres), "Gradient", "GetResourceType(plain.tres) is Gradient");
        ExpectEqual(Type(PlainRes), "Curve", "GetResourceType(plain.res) is Curve");
        Expect(ResourceLoader.Exists(PlainRes, "Curve"), "Exists(plain.res, \"Curve\") is true with a type hint");
        Expect(ResourceLoader.Exists("res://icon.svg"), "Exists() is true for the imported res://icon.svg (.import file)");
        ExpectEqual(Type("res://icon.svg"), "CompressedTexture2D", "GetResourceType(res://icon.svg) comes from the importer");
        Expect(ResourceLoader.Load("res://icon.svg") is Texture2D, "res://icon.svg loads as Texture2D");
        Expect(!ResourceLoader.Exists(Root + "missing.unknownext"), "Exists() is false for an unknown extension");
        ExpectEqual(Type(Root + "missing.unknownext"), "", "GetResourceType() is empty for an unknown extension");
    }

    private void CheckOrder()
    {
        ExpectEqual(Winner(One), "<none>", "no loader for .t53x before a loader is registered");
        var a = ExtLoader("A", "t53x");
        var b = ExtLoader("B", "T53X");
        var c = ExtLoader("C", "t53x");
        ResourceLoader.AddResourceFormatLoader(a);
        ResourceLoader.AddResourceFormatLoader(b);
        ExpectEqual(Winner(One), "A", "two indexed C# loaders for one extension: the first registered loads");
        ExpectEqual(Type(One), "TypeA", "GetResourceType() answers from the first registered loader");
        ResourceLoader.AddResourceFormatLoader(c, true);
        ExpectEqual(Winner(One), "C", "a C# loader registered with atFront = true loads first");
        ExpectEqual(Winner(Upper), "C", "the extension match is case-insensitive (UPPER.T53X)");
        ResourceLoader.RemoveResourceFormatLoader(c);
        ResourceLoader.RemoveResourceFormatLoader(a);
        ExpectEqual(Winner(One), "B", "after removing C and A the loader declaring T53X loads (B)");
        ExpectEqual(Type(One), "TypeB", "GetResourceType() answers from B");
        ResourceLoader.RemoveResourceFormatLoader(b);
        ExpectEqual(Winner(One), "<none>", "after removing every loader no loader handles .t53x");
        ExpectEqual(Type(One), "", "GetResourceType() is empty without a loader");
    }

    private void CheckDotted()
    {
        var d = ExtLoader("D", "pack.t53");
        ResourceLoader.AddResourceFormatLoader(d);
        ExpectEqual(Winner(Dotted), "D", "an extension with a dot (pack.t53) is found for archive.pack.t53");
        ExpectEqual(Winner(PlainDotted), "<none>", "archive.t53 does not match the extension pack.t53");
        ResourceLoader.RemoveResourceFormatLoader(d);
    }

    private void CheckPathBased()
    {
        // A (index), P (path based), B (index): registration order decides, not the kind of loader.
        var a = ExtLoader("A", "dat");
        var p = PathLoader("P", "_special.dat");
        var b = ExtLoader("B", "dat");
        ResourceLoader.AddResourceFormatLoader(p, true);
        ExpectEqual(Winner(Special), "P", "a loader that overrides _RecognizePath() is still asked for its paths");
        ExpectEqual(Type(Special), "TypeP", "GetResourceType() still asks the path-based loader");
        ResourceLoader.RemoveResourceFormatLoader(p);
        ResourceLoader.AddResourceFormatLoader(a);
        ResourceLoader.AddResourceFormatLoader(p);
        ResourceLoader.AddResourceFormatLoader(b);
        ExpectEqual(Winner(Special), "A", "order A, P, B: A loads");
        ResourceLoader.RemoveResourceFormatLoader(a);
        ExpectEqual(Winner(Special), "P", "after removing A the path-based loader P loads, not the later indexed loader B");
        ResourceLoader.RemoveResourceFormatLoader(p);
        ExpectEqual(Winner(Special), "B", "after removing P the indexed loader B loads");
        ResourceLoader.RemoveResourceFormatLoader(b);
    }

    private void CheckRefresh()
    {
        var l = ExtLoader("L", "t53old");
        ResourceLoader.AddResourceFormatLoader(l);
        ExpectEqual(Winner(Old), "L", "the loader is found for its first extension");
        l.Extensions = ["t53new"];
        ExpectEqual(Winner(New), "<none>", "a changed list is not seen before RefreshFormatIndex() (documented)");
        ExpectEqual(Winner(Old), "<none>", "the old extension is no longer recognized: recognize_path() reads the current list");
        ResourceLoader.RefreshFormatIndex();
        ExpectEqual(Winner(New), "L", "after RefreshFormatIndex() the new extension is found");
        ExpectEqual(Type(New), "TypeL", "after RefreshFormatIndex() GetResourceType() asks the loader for the new extension");
        ExpectEqual(Winner(Old), "<none>", "after RefreshFormatIndex() the old extension stays unrecognized");
        l.Extensions = ["t53old", "t53new"];
        ResourceLoader.RemoveResourceFormatLoader(l);
        ResourceLoader.AddResourceFormatLoader(l);
        Expect(Winner(Old) == "L" && Winner(New) == "L", "removing and adding the loader again reads its list again");
        ResourceLoader.RemoveResourceFormatLoader(l);
        ExpectEqual(Winner(New), "<none>", "after removing the loader nothing handles .t53new");
    }

    private static string[] PerfPaths()
    {
        string[] exts = ["tres", "res", "scn", "tscn", "png", "gd", "wav", "ogg", "glb", "unknown"];
        var paths = new string[PerfCount];
        for (int i = 0; i < PerfCount; i++)
        {
            paths[i] = (i % 20) switch
            {
                0 => PlainTres,
                1 => PlainRes,
                _ => $"res://__cs_loader_perf/{i}.{exts[i % exts.Length]}",
            };
        }
        return paths;
    }

    private static (string[] Types, int Found, ulong TypeUsec, ulong ExistsUsec) Measure(string[] paths)
    {
        var types = new string[paths.Length];
        ulong start = Time.GetTicksUsec();
        for (int i = 0; i < paths.Length; i++)
        {
            types[i] = ResourceLoader.GetResourceType(paths[i]);
        }
        ulong typeUsec = Time.GetTicksUsec() - start;
        int found = 0;
        start = Time.GetTicksUsec();
        foreach (string path in paths)
        {
            if (ResourceLoader.Exists(path))
            {
                found++;
            }
        }
        ulong existsUsec = Time.GetTicksUsec() - start;
        return (types, found, typeUsec, existsUsec);
    }

    private static (string[] Types, int Found, ulong TypeUsec, ulong ExistsUsec) MeasureWith(string[] paths, IEnumerable<ResourceFormatLoader> loaders)
    {
        var list = loaders.ToList();
        foreach (var loader in list)
        {
            ResourceLoader.AddResourceFormatLoader(loader);
        }
        Measure(paths); // warm-up
        var result = Measure(paths);
        foreach (var loader in list)
        {
            ResourceLoader.RemoveResourceFormatLoader(loader);
        }
        return result;
    }

    private static string PerPath(ulong usec, int count) => $"{(double)usec / count:F1} usec/path";

    private void CheckPerformance()
    {
        string[] paths = PerfPaths();
        Measure(paths); // warm-up
        var baseline = Measure(paths);

        // Indexed: override only _GetRecognizedExtensions() (new in #53).
        var withIndexed = MeasureWith(paths, Enumerable.Range(0, ExtraLoaders).Select(i => (ResourceFormatLoader)ExtLoader($"E{i}", $"x53_{i}")));
        // Asked for every path: override _RecognizePath(), the way every script loader was asked before #53.
        var withAsked = MeasureWith(paths, Enumerable.Range(0, ExtraLoaders).Select(i => (ResourceFormatLoader)PathLoader($"P{i}", $".x53p_{i}")));

        int n = paths.Length;
        GD.Print($"  INFO  {n} paths, built-in loaders only:         GetResourceType() {PerPath(baseline.TypeUsec, n)}, Exists() {PerPath(baseline.ExistsUsec, n)}");
        GD.Print($"  INFO  {n} paths, +{ExtraLoaders} indexed C# loaders:       GetResourceType() {PerPath(withIndexed.TypeUsec, n)}, Exists() {PerPath(withIndexed.ExistsUsec, n)}");
        GD.Print($"  INFO  {n} paths, +{ExtraLoaders} always-asked C# loaders:  GetResourceType() {PerPath(withAsked.TypeUsec, n)}, Exists() {PerPath(withAsked.ExistsUsec, n)}");
        Expect(withIndexed.Types.SequenceEqual(baseline.Types), "100 indexed C# loaders do not change GetResourceType() of any of the 10,000 paths");
        ExpectEqual(withIndexed.Found, baseline.Found, "100 indexed C# loaders do not change Exists() of any of the 10,000 paths");
        Expect(withAsked.Types.SequenceEqual(baseline.Types), "100 path-based C# loaders do not change GetResourceType() of any path");
        ExpectEqual(baseline.Found, PerfCount / 10, "Exists() finds exactly the 1,000 paths of the two real files (every 20th and 21st path)");
        ExpectEqual(baseline.Types[1], "Curve", "the second path is plain.res: type Curve");
    }

    private static void Cleanup() => RemoveRecursive(ProjectSettings.GlobalizePath(Root));

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
