// Written before the engine build of #34; run after the merge.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue34FeatureLoaderExtensionIndex;

/// <summary>C# checks of 34-feature-loader-extension-index. Check the same things as scripts/gd/check.gd.</summary>
// The issue adds no API: the choice of the loader must stay as it is (registration order, first match wins).
// Assumption: .import files are not covered here, because a test needs an imported asset; the built-in formats .tres and .res stand for the binary and text loaders.
// Assumption: a loader reports its identity through ResourceName (load) and through the type name it returns (GetResourceType).
public partial class Issue34FeatureLoaderExtensionIndexCheck : FeatureCheckCs
{
    private const string Root = "user://34-feature-loader-extension-index/cs/";
    private const string Ext = "t34x";
    private const int PerfCount = 10000;
    private const int ExtraLoaders = 100;

    private const string One = Root + "one." + Ext;
    private const string Upper = Root + "UPPER.T34X";
    private const string Special = Root + "item_special.dat";
    private const string Other = Root + "other.dat";
    private const string PlainTres = Root + "plain.tres";
    private const string PlainRes = Root + "plain.res";

    protected override string CheckName => "34-feature-loader-extension-index";

    protected override Task RunChecks()
    {
        Cleanup();
        Prepare();
        CheckOrder();
        CheckAtFront();
        CheckPathBased();
        CheckMixedOrder();
        CheckBuiltin();
        CheckPerformance();
        Cleanup();
        return Task.CompletedTask;
    }

    private static Issue34ExtLoader ExtLoader(string tag, string extension = Ext)
        => new() { Tag = tag, Extension = extension, TypeName = "Type" + tag };

    private static Issue34PathLoader PathLoader(string tag, string ending)
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
        foreach (string path in new[] { One, Upper, Special, Other })
        {
            WriteFile(path);
        }
        using var gradient = new Gradient();
        using var curve = new Curve();
        ExpectEqual(ResourceSaver.Save(gradient, PlainTres), Error.Ok, "saved plain.tres");
        ExpectEqual(ResourceSaver.Save(curve, PlainRes), Error.Ok, "saved plain.res");
    }

    private void CheckOrder()
    {
        ExpectEqual(Winner(One), "<none>", "no loader for .t34x before a loader is registered");
        var a = ExtLoader("A");
        var b = ExtLoader("B");
        var c = ExtLoader("C");
        ResourceLoader.AddResourceFormatLoader(a);
        ResourceLoader.AddResourceFormatLoader(b);
        ExpectEqual(Winner(One), "A", "two loaders for one extension: the first registered loads");
        ExpectEqual(Type(One), "TypeA", "GetResourceType() answers from the first registered loader");
        ResourceLoader.AddResourceFormatLoader(c, true);
        ExpectEqual(Winner(One), "C", "a loader registered with atFront = true loads first");
        ExpectEqual(Type(One), "TypeC", "GetResourceType() answers from the atFront loader");
        ExpectEqual(Winner(Upper), "C", "the extension match is case-insensitive (UPPER.T34X)");
        ResourceLoader.RemoveResourceFormatLoader(c);
        ExpectEqual(Winner(One), "A", "after removing the front loader the next one loads again (A)");
        ResourceLoader.RemoveResourceFormatLoader(a);
        ExpectEqual(Winner(One), "B", "after removing A the next one loads (B)");
        ResourceLoader.RemoveResourceFormatLoader(b);
        ExpectEqual(Winner(One), "<none>", "after removing every loader no loader handles .t34x");
        ExpectEqual(Type(One), "", "GetResourceType() is empty without a loader");
    }

    private void CheckAtFront()
    {
        var a = ExtLoader("A");
        var d1 = ExtLoader("D1");
        var d2 = ExtLoader("D2");
        ResourceLoader.AddResourceFormatLoader(a);
        ResourceLoader.AddResourceFormatLoader(d1, true);
        ResourceLoader.AddResourceFormatLoader(d2, true);
        ExpectEqual(Winner(One), "D2", "of two atFront loaders the later one is first");
        ResourceLoader.RemoveResourceFormatLoader(d2);
        ExpectEqual(Winner(One), "D1", "after removing D2 the earlier atFront loader loads (D1)");
        ResourceLoader.RemoveResourceFormatLoader(d1);
        ExpectEqual(Winner(One), "A", "after removing D1 the normal loader loads (A)");
        ResourceLoader.RemoveResourceFormatLoader(a);
        ExpectEqual(Winner(One), "<none>", "no loader left");
    }

    private void CheckPathBased()
    {
        ExpectEqual(Winner(Special), "<none>", "no loader for item_special.dat before registration");
        var p = PathLoader("P", "_special.dat");
        ResourceLoader.AddResourceFormatLoader(p);
        ExpectEqual(Winner(Special), "P", "a loader that overrides _RecognizePath() loads a path without its extension");
        ExpectEqual(Type(Special), "TypeP", "GetResourceType() answers from the path-based loader");
        ExpectEqual(Winner(Other), "<none>", "the path-based loader ignores other paths with the same extension");
        ResourceLoader.RemoveResourceFormatLoader(p);
        ExpectEqual(Winner(Special), "<none>", "after removing the path-based loader nothing handles item_special.dat");
    }

    private void CheckMixedOrder()
    {
        // A and B go by extension, Q by path. The registration order A, Q, B decides, not the kind of loader.
        var a = ExtLoader("A");
        var q = PathLoader("Q", "one." + Ext);
        var b = ExtLoader("B");
        ResourceLoader.AddResourceFormatLoader(a);
        ResourceLoader.AddResourceFormatLoader(q);
        ResourceLoader.AddResourceFormatLoader(b);
        ExpectEqual(Winner(One), "A", "order A, Q, B: A loads");
        ResourceLoader.RemoveResourceFormatLoader(a);
        ExpectEqual(Winner(One), "Q", "after removing A the path-based loader Q loads, not the later extension loader B");
        ExpectEqual(Winner(Upper), "B", "Q does not recognize UPPER.T34X (case differs), so B loads it");
        ResourceLoader.RemoveResourceFormatLoader(q);
        ExpectEqual(Winner(One), "B", "after removing Q the extension loader B loads");
        ResourceLoader.RemoveResourceFormatLoader(b);

        var frontQ = PathLoader("FrontQ", "one." + Ext);
        var lateA = ExtLoader("LateA");
        ResourceLoader.AddResourceFormatLoader(lateA);
        ResourceLoader.AddResourceFormatLoader(frontQ, true);
        ExpectEqual(Winner(One), "FrontQ", "a path-based loader at the front loads before an extension loader");
        ResourceLoader.RemoveResourceFormatLoader(frontQ);
        ResourceLoader.RemoveResourceFormatLoader(lateA);
    }

    private void CheckBuiltin()
    {
        var tres = ResourceLoader.Load(PlainTres, "", ResourceLoader.CacheMode.Ignore);
        var res = ResourceLoader.Load(PlainRes, "", ResourceLoader.CacheMode.Ignore);
        Expect(tres is Gradient, "plain.tres loads as Gradient with the built-in text loader");
        Expect(res is Curve, "plain.res loads as Curve with the built-in binary loader");
        ExpectEqual(Type(PlainTres), "Gradient", "GetResourceType(plain.tres) is Gradient");
        ExpectEqual(Type(PlainRes), "Curve", "GetResourceType(plain.res) is Curve");
        Expect(ResourceLoader.Exists(PlainTres) && ResourceLoader.Exists(PlainRes), "Exists() is true for .tres and .res");
        Expect(!ResourceLoader.Exists(Root + "missing.unknownext"), "Exists() is false for an unknown extension");

        // Script loaders in front, for other extensions and for .t34x: the built-in formats still use their own loaders.
        var loaders = new List<ResourceFormatLoader> { ExtLoader("X1", "xa"), ExtLoader("X2", Ext), PathLoader("X3", ".nothing") };
        foreach (var loader in loaders)
        {
            ResourceLoader.AddResourceFormatLoader(loader, true);
        }
        var tres2 = ResourceLoader.Load(PlainTres, "", ResourceLoader.CacheMode.Ignore);
        var res2 = ResourceLoader.Load(PlainRes, "", ResourceLoader.CacheMode.Ignore);
        Expect(tres2 is Gradient && tres2.ResourceName == "", "with script loaders in front plain.tres still loads with the built-in loader");
        Expect(res2 is Curve && res2.ResourceName == "", "with script loaders in front plain.res still loads with the built-in loader");
        ExpectEqual(Winner(One), "X2", "the script loader for .t34x still loads .t34x");
        foreach (var loader in loaders)
        {
            ResourceLoader.RemoveResourceFormatLoader(loader);
        }
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

    private void CheckPerformance()
    {
        string[] paths = PerfPaths();
        Measure(paths); // warm-up
        var baseline = Measure(paths);

        var extra = new List<Issue34ExtLoader>(ExtraLoaders);
        for (int i = 0; i < ExtraLoaders; i++)
        {
            var loader = ExtLoader($"E{i}", $"x34_{i}");
            ResourceLoader.AddResourceFormatLoader(loader);
            extra.Add(loader);
        }
        Measure(paths); // warm-up
        var withExtra = Measure(paths);
        foreach (var loader in extra)
        {
            ResourceLoader.RemoveResourceFormatLoader(loader);
        }

        GD.Print($"  INFO  {paths.Length} paths, built-in loaders:  GetResourceType() {baseline.TypeUsec} usec, Exists() {baseline.ExistsUsec} usec");
        GD.Print($"  INFO  {paths.Length} paths, +{ExtraLoaders} script loaders: GetResourceType() {withExtra.TypeUsec} usec, Exists() {withExtra.ExistsUsec} usec");
        Expect(withExtra.Types.SequenceEqual(baseline.Types), "the 100 extra loaders do not change GetResourceType() of any of the 10,000 paths");
        ExpectEqual(withExtra.Found, baseline.Found, "the 100 extra loaders do not change Exists() of any of the 10,000 paths");
        ExpectEqual(baseline.Found, PerfCount / 10, "Exists() finds exactly the 1,000 paths of the two real files (every 20th and 21st path)");
        ExpectEqual(baseline.Types[0], "Gradient", "the first path is plain.tres: type Gradient");
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
