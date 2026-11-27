using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.LoaderIndex;

/// <summary>Full API test of Loader Index (ResourceLoader extension index). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://loader-index/cs/";
    private const string One = Root + "one.lix";
    private const string Upper = Root + "UPPER.LIX";
    private const string Old = Root + "data.lixold";
    private const string New = Root + "data.lixnew";
    private const string Dotted = Root + "archive.pack.lix";
    private const string PlainDotted = Root + "archive.lix";
    private const string Special = Root + "item_special.dat";
    private const string Other = Root + "other.dat";
    private const string PlainTres = Root + "plain.tres";
    private const string PlainRes = Root + "plain.res";
    private const string None = "<none>";
    private const int SamplePathCount = 1000;
    private const int ExtraLoaders = 100;

    protected override Task RunTests()
    {
        Prepare();
        TestBuiltinFormats();
        TestOrder();
        TestAtFront();
        TestDottedExtension();
        TestPathBasedLoader();
        TestRebuildExtensionIndex();
        TestSameResultsWithManyLoaders();
        Cleanup();
        return Task.CompletedTask;
    }

    private void Prepare()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        foreach (string path in new[] { One, Upper, Old, New, Dotted, PlainDotted, Special, Other })
        {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString("x");
        }
        ExpectEqual(ResourceSaver.Save(new Gradient(), PlainTres), Error.Ok, "saved plain.tres");
        ExpectEqual(ResourceSaver.Save(new Curve(), PlainRes), Error.Ok, "saved plain.res");
    }

    private static void Cleanup()
    {
        using DirAccess dir = DirAccess.Open(Root);
        foreach (string file in dir.GetFiles())
        {
            dir.Remove(file);
        }
        DirAccess.RemoveAbsolute(Root);
    }

    private static MyLoader ExtLoader(string tag, params string[] extensions)
    {
        return new MyLoader { Tag = tag, Extensions = extensions, TypeName = "Type" + tag };
    }

    private static PathLoader PathLoaderOf(string tag, string ending)
    {
        return new PathLoader { Tag = tag, PathEnding = ending, TypeName = "Type" + tag };
    }

    private static string Winner(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            return None;
        }
        Resource resource = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        return resource.ResourceName;
    }

    private static string TypeOf(string path)
    {
        return ResourceLoader.GetResourceType(path);
    }

    private void TestBuiltinFormats()
    {
        Resource tres = ResourceLoader.Load(PlainTres, "", ResourceLoader.CacheMode.Ignore);
        Resource res = ResourceLoader.Load(PlainRes, "", ResourceLoader.CacheMode.Ignore);
        Expect(tres is Gradient, "plain.tres loads as Gradient with the built-in text loader");
        Expect(res is Curve, "plain.res loads as Curve with the binary loader");
        ExpectEqual(TypeOf(PlainTres), "Gradient", "GetResourceType(plain.tres) is Gradient");
        ExpectEqual(TypeOf(PlainRes), "Curve", "GetResourceType(plain.res) is Curve");
        Expect(ResourceLoader.Exists(PlainRes, "Curve"), "Exists(plain.res, Curve) is true with a type hint");
        Expect(ResourceLoader.Exists("res://icon.svg"), "Exists() is true for the imported res://icon.svg");
        ExpectEqual(TypeOf("res://icon.svg"), "CompressedTexture2D", "GetResourceType(res://icon.svg) comes from the importer");
        Resource icon = ResourceLoader.Load("res://icon.svg");
        Expect(icon is Texture2D, "res://icon.svg loads as Texture2D");
        Expect(!ResourceLoader.Exists(Root + "missing.unknownext"), "Exists() is false for an unknown extension");
        ExpectEqual(TypeOf(Root + "missing.unknownext"), "", "GetResourceType() is empty for an unknown extension");
    }

    private void TestOrder()
    {
        ExpectEqual(Winner(One), None, "no loader for .lix before a loader is registered");
        MyLoader a = ExtLoader("A", "lix");
        MyLoader b = ExtLoader("B", "LIX");
        MyLoader c = ExtLoader("C", "lix");
        ResourceLoader.AddResourceFormatLoader(a);
        ResourceLoader.AddResourceFormatLoader(b);
        ExpectEqual(Winner(One), "A", "two loaders for one extension: the first registered loads");
        ExpectEqual(TypeOf(One), "TypeA", "GetResourceType() answers from the first registered loader");
        ResourceLoader.AddResourceFormatLoader(c, true);
        ExpectEqual(Winner(One), "C", "a loader registered with atFront = true loads first");
        ExpectEqual(Winner(Upper), "C", "the extension match is case-insensitive (UPPER.LIX)");
        ResourceLoader.RemoveResourceFormatLoader(c);
        ResourceLoader.RemoveResourceFormatLoader(a);
        ExpectEqual(Winner(One), "B", "after removing C and A the loader declaring LIX loads (B)");
        ExpectEqual(TypeOf(One), "TypeB", "GetResourceType() answers from B");
        ResourceLoader.RemoveResourceFormatLoader(b);
        ExpectEqual(Winner(One), None, "after removing every loader no loader handles .lix");
        ExpectEqual(TypeOf(One), "", "GetResourceType() is empty without a loader");
    }

    private void TestAtFront()
    {
        MyLoader a = ExtLoader("A", "lix");
        MyLoader d1 = ExtLoader("D1", "lix");
        MyLoader d2 = ExtLoader("D2", "lix");
        ResourceLoader.AddResourceFormatLoader(a);
        ResourceLoader.AddResourceFormatLoader(d1, true);
        ResourceLoader.AddResourceFormatLoader(d2, true);
        ExpectEqual(Winner(One), "D2", "of two atFront loaders the later one is first");
        ResourceLoader.RemoveResourceFormatLoader(d2);
        ExpectEqual(Winner(One), "D1", "after removing D2 the earlier atFront loader loads (D1)");
        ResourceLoader.RemoveResourceFormatLoader(d1);
        ExpectEqual(Winner(One), "A", "after removing D1 the normal loader loads (A)");
        ResourceLoader.RemoveResourceFormatLoader(a);
    }

    private void TestDottedExtension()
    {
        MyLoader d = ExtLoader("D", "pack.lix");
        ResourceLoader.AddResourceFormatLoader(d);
        ExpectEqual(Winner(Dotted), "D", "an extension with a dot (pack.lix) is found for archive.pack.lix");
        ExpectEqual(Winner(PlainDotted), None, "archive.lix does not match the extension pack.lix");
        ResourceLoader.RemoveResourceFormatLoader(d);
    }

    private void TestPathBasedLoader()
    {
        MyLoader a = ExtLoader("A", "dat");
        PathLoader p = PathLoaderOf("P", "_special.dat");
        MyLoader b = ExtLoader("B", "dat");
        ResourceLoader.AddResourceFormatLoader(p, true);
        ExpectEqual(Winner(Special), "P", "a loader that overrides _RecognizePath() is still asked for its paths");
        ExpectEqual(TypeOf(Special), "TypeP", "GetResourceType() still asks the path-based loader");
        ExpectEqual(Winner(Other), None, "the path-based loader ignores other paths with the same extension");
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

    private void TestRebuildExtensionIndex()
    {
        MyLoader loader = ExtLoader("L", "lixold");
        ResourceLoader.AddResourceFormatLoader(loader);
        ExpectEqual(Winner(Old), "L", "the loader is found for its first extension");
        loader.Extensions = ["lixnew"];
        ExpectEqual(Winner(New), None, "a changed list is not seen before RebuildExtensionIndex()");
        ExpectEqual(Winner(Old), None, "the old extension is no longer recognized");
        ResourceLoader.RebuildExtensionIndex();
        ExpectEqual(Winner(New), "L", "after RebuildExtensionIndex() the new extension is found");
        ExpectEqual(TypeOf(New), "TypeL", "after RebuildExtensionIndex() GetResourceType() asks the loader");
        ExpectEqual(Winner(Old), None, "after RebuildExtensionIndex() the old extension stays unrecognized");
        loader.Extensions = ["lixold", "lixnew"];
        ResourceLoader.RemoveResourceFormatLoader(loader);
        ResourceLoader.AddResourceFormatLoader(loader);
        ExpectEqual(Winner(Old), "L", "adding the loader again reads its list again (old)");
        ExpectEqual(Winner(New), "L", "adding the loader again reads its list again (new)");
        ResourceLoader.RemoveResourceFormatLoader(loader);
        ExpectEqual(Winner(New), None, "after removing the loader nothing handles .lixnew");
    }

    private static string[] SampleTypes()
    {
        string[] extensions = ["tres", "res", "scn", "tscn", "png", "gd", "wav", "ogg", "glb", "unknown"];
        var types = new List<string>();
        for (int i = 0; i < SamplePathCount; i++)
        {
            string path = $"res://__loader_index/{i}.{extensions[i % extensions.Length]}";
            types.Add(ResourceLoader.GetResourceType(path));
        }
        return types.ToArray();
    }

    private void TestSameResultsWithManyLoaders()
    {
        string[] before = SampleTypes();
        var loaders = new List<ResourceFormatLoader>();
        for (int i = 0; i < ExtraLoaders; i++)
        {
            loaders.Add(ExtLoader($"E{i}", $"lixext{i}"));
            loaders.Add(PathLoaderOf($"P{i}", $".lixpath{i}"));
        }
        foreach (ResourceFormatLoader loader in loaders)
        {
            ResourceLoader.AddResourceFormatLoader(loader);
        }
        string[] after = SampleTypes();
        foreach (ResourceFormatLoader loader in loaders)
        {
            ResourceLoader.RemoveResourceFormatLoader(loader);
        }
        Expect(after.SequenceEqual(before), "200 extra loaders do not change GetResourceType() of 1000 paths");
    }
}
