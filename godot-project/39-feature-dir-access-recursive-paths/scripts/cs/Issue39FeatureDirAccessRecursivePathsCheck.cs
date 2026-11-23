using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue39FeatureDirAccessRecursivePaths;

/// <summary>C# checks of 39-feature-dir-access-recursive-paths. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue39FeatureDirAccessRecursivePathsCheck : FeatureCheckCs
{
    private const string Root = "user://dirtest_cs";
    private const string BenchRoot = "user://dirbench";
    private const string Feature = "res://39-feature-dir-access-recursive-paths";

    protected override string CheckName => "39-feature-dir-access-recursive-paths";

    protected override Task RunChecks()
    {
        CreateTree(Root);
        CheckFilePaths();
        CheckDirectoryPaths();
        CheckExtensions();
        CheckInstanceMethods();
        CheckResInEditor();
        CheckFolderLink();
        CheckPerformance();
        RemoveTree(Root);
        return Task.CompletedTask;
    }

    // A link to a folder (junction on Windows, which needs no extra privileges) is listed, but not followed.
    private void CheckFolderLink()
    {
        string target = ProjectSettings.GlobalizePath(Root + "/sub1");
        string link = ProjectSettings.GlobalizePath(Root + "/link");
        int code = OS.GetName() == "Windows"
            ? OS.Execute("cmd.exe", ["/c", "mklink", "/J", link.Replace("/", "\\"), target.Replace("/", "\\")])
            : OS.Execute("ln", ["-s", target, link]);
        if (!Expect(code == 0, $"folder link created (exit code {code})"))
        {
            return;
        }
        ExpectEqual(DirAccess.GetDirectoryPathsAt(Root, true), Paths(Root, "link", "sub1", "sub1/sub2", "sub3"), "a linked folder is listed, but not followed");
        ExpectEqual(DirAccess.GetFilePathsAt(Root, true, ["json"]), Paths(Root, "sub1/sub2/f.json"), "files behind the link are not listed");
        DirAccess.RemoveAbsolute(Root + "/link");
    }

    // 3 levels, mixed extensions in mixed case, a hidden file, an empty folder.
    private static void CreateTree(string root)
    {
        RemoveTree(root);
        DirAccess.MakeDirRecursiveAbsolute(root + "/sub1/sub2");
        DirAccess.MakeDirRecursiveAbsolute(root + "/sub3");
        foreach (string file in new[] { "a.PNG", "b.txt", ".hidden.png", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json" })
        {
            using FileAccess f = FileAccess.Open(root.PathJoin(file), FileAccess.ModeFlags.Write);
            f.StoreString("x");
        }
        // Hidden by name on Unix, by attribute on Windows.
        FileAccess.SetHiddenAttribute(root.PathJoin(".hidden.png"), true);
    }

    private static void RemoveTree(string root)
    {
        if (!DirAccess.DirExistsAbsolute(root))
        {
            return;
        }
        using DirAccess da = DirAccess.Open(root);
        da.IncludeHidden = true;
        string[] dirs = da.GetDirectoryPaths(true);
        foreach (string path in da.GetFilePaths(true))
        {
            DirAccess.RemoveAbsolute(path);
        }
        foreach (string dir in dirs.Reverse())
        {
            DirAccess.RemoveAbsolute(dir);
        }
        DirAccess.RemoveAbsolute(root);
    }

    private static string[] Paths(string baseDir, params string[] names) => names.Select(n => baseDir.PathJoin(n)).ToArray();

    private void ExpectEqual(string[] actual, string[] expected, string message)
    {
        Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");
    }

    private void CheckFilePaths()
    {
        ExpectEqual(DirAccess.GetFilePathsAt(Root), Paths(Root, "a.PNG", "b.txt"), "GetFilePathsAt() lists one folder, without hidden files");
        ExpectEqual(DirAccess.GetFilePathsAt(Root, true),
            Paths(Root, "a.PNG", "b.txt", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json"),
            "GetFilePathsAt(recursive) lists all levels, sorted");
        ExpectEqual(DirAccess.GetFilePathsAt(Root, true, ["png", "webp"]), Paths(Root, "a.PNG", "sub1/c.png", "sub1/d.WebP"),
            "extension filter is case-insensitive");
        ExpectEqual(DirAccess.GetFilePathsAt(Root, true, [".PNG"]), Paths(Root, "a.PNG", "sub1/c.png"), "a leading dot in the filter is ignored");
        ExpectEqual(DirAccess.GetFilePathsAt(Root + "/", true, ["json"]), Paths(Root, "sub1/sub2/f.json"), "a trailing slash in the input gives no double slash");
    }

    private void CheckDirectoryPaths()
    {
        ExpectEqual(DirAccess.GetDirectoryPathsAt(Root), Paths(Root, "sub1", "sub3"), "GetDirectoryPathsAt() lists one folder");
        ExpectEqual(DirAccess.GetDirectoryPathsAt(Root, true), Paths(Root, "sub1", "sub1/sub2", "sub3"), "GetDirectoryPathsAt(recursive) lists all levels");
        ExpectEqual(DirAccess.GetDirectoryPathsAt(Root + "/sub3", true), [], "empty folder gives an empty array");
    }

    private void CheckExtensions()
    {
        ExpectEqual(DirAccess.GetFileExtensionsAt(Root), ["png", "txt"], "GetFileExtensionsAt() lists one folder");
        ExpectEqual(DirAccess.GetFileExtensionsAt(Root, true), ["json", "png", "tres", "txt", "webp"],
            "GetFileExtensionsAt(recursive) gives distinct lower-case extensions, sorted");
    }

    private void CheckInstanceMethods()
    {
        using DirAccess da = DirAccess.Open(Root);
        string baseDir = da.GetCurrentDir();
        ExpectEqual(da.GetFilePaths(false, ["png"]), Paths(baseDir, "a.PNG"), "GetFilePaths() skips hidden files by default");
        da.IncludeHidden = true;
        ExpectEqual(da.GetFilePaths(true, ["png"]), Paths(baseDir, ".hidden.png", "a.PNG", "sub1/c.png"), "GetFilePaths() respects IncludeHidden");
        da.IncludeNavigational = true;
        ExpectEqual(da.GetDirectoryPaths(true), Paths(baseDir, "sub1", "sub1/sub2", "sub3"), "GetDirectoryPaths() never returns . and ..");
        ExpectEqual(da.GetFileExtensions(true), ["json", "png", "tres", "txt", "webp"], "GetFileExtensions() works on the instance");
    }

    private void CheckResInEditor()
    {
        ExpectEqual(DirAccess.GetFilePathsAt(Feature, true, ["gd"]), [Feature + "/scripts/gd/check.gd"], "res:// in the editor keeps the res:// prefix");
        ExpectEqual(DirAccess.GetFilePathsAt(Feature + "/scripts", true, ["CS"]),
            [Feature + "/scripts/cs/Issue39FeatureDirAccessRecursivePathsCheck.cs"], "res:// filter by extension");
    }

    // Same tree as the GDScript check: 10 000 files in 500 folders.
    private static void CreateBenchTree()
    {
        if (FileAccess.FileExists(BenchRoot + "/done"))
        {
            return;
        }
        for (int i = 0; i < 20; i++)
        {
            string top = $"{BenchRoot}/d{i:00}";
            for (int j = 0; j < 24; j++)
            {
                DirAccess.MakeDirRecursiveAbsolute($"{top}/s{j:00}");
            }
            for (int j = 0; j < 20; j++)
            {
                using FileAccess f = FileAccess.Open($"{top}/f{j:00}.txt", FileAccess.ModeFlags.Write);
                f.StoreString("x");
            }
            for (int j = 0; j < 24; j++)
            {
                for (int k = 0; k < 20; k++)
                {
                    using FileAccess f = FileAccess.Open($"{top}/s{j:00}/f{k:00}.{(k % 2 == 0 ? "png" : "txt")}", FileAccess.ModeFlags.Write);
                    f.StoreString("x");
                }
            }
        }
        using FileAccess done = FileAccess.Open(BenchRoot + "/done", FileAccess.ModeFlags.Write);
        done.StoreString("x");
    }

    private static void Walk(string path, List<string> output)
    {
        foreach (string file in DirAccess.GetFilesAt(path))
        {
            output.Add(path.PathJoin(file));
        }
        foreach (string dir in DirAccess.GetDirectoriesAt(path))
        {
            Walk(path.PathJoin(dir), output);
        }
    }

    private void CheckPerformance()
    {
        CreateBenchTree();
        double bestNative = double.MaxValue;
        double bestScript = double.MaxValue;
        string[] native = [];
        List<string> script = [];
        for (int run = 0; run < 3; run++)
        {
            var sw = Stopwatch.StartNew();
            native = DirAccess.GetFilePathsAt(BenchRoot, true);
            bestNative = Math.Min(bestNative, sw.Elapsed.TotalMilliseconds);
            sw.Restart();
            script = [];
            Walk(BenchRoot, script);
            bestScript = Math.Min(bestScript, sw.Elapsed.TotalMilliseconds);
        }
        Expect(native.Length == 10001, $"benchmark tree has 10 000 files (+ marker): {native.Length}");
        Expect(native.ToHashSet().SetEquals(script), "C++ result equals the C# loop");
        GD.Print($"  PERF  10k files/500 folders: GetFilePathsAt {bestNative:F1} ms, C# loop {bestScript:F1} ms");
    }
}
