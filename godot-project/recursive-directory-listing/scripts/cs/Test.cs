using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.RecursiveDirectoryListing;

/// <summary>Full API test of Recursive Directory Listing (DirAccess.GetFilePathsAt and its siblings). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://dir_listing_test";
    private const string PckRoot = "user://dir_listing_pack";
    private const string Feature = "res://recursive-directory-listing";
    // The files of the tree: 3 levels, mixed extensions in mixed case, a hidden file, an empty folder (sub3).
    private static readonly string[] Files = ["a.PNG", "b.txt", ".hidden.png", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json"];
    private static readonly string[] AllExtensions = ["json", "png", "tres", "txt", "webp"];
    // A project that prints the listings of its own res:// folder, run on a pack as the main pack.
    private const string PackScript = """
        extends SceneTree

        func _initialize() -> void:
        	print("PACK_FILES=", ",".join(DirAccess.get_file_paths_at("res://data", true)))
        	print("PACK_IMPORT=", ",".join(DirAccess.get_file_paths_at("res://data", true, ["IMPORT"])))
        	print("PACK_DIRS=", ",".join(DirAccess.get_directory_paths_at("res://data", true)))
        	print("PACK_EXTENSIONS=", ",".join(DirAccess.get_file_extensions_at("res://data", true)))
        	quit(0)
        """;

    protected override Task RunTests()
    {
        RemoveTree(Root);
        CreateTree();
        TestFilePaths();
        TestDirectoryPaths();
        TestExtensions();
        TestInstanceMethods();
        TestResFolder();
        TestMissingFolder();
        TestFolderLink();
        TestPack();
        RemoveTree(Root);
        return Task.CompletedTask;
    }

    private static void CreateTree()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root + "/sub1/sub2");
        DirAccess.MakeDirRecursiveAbsolute(Root + "/sub3");
        foreach (string relative in Files)
        {
            using FileAccess file = FileAccess.Open(Root.PathJoin(relative), FileAccess.ModeFlags.Write);
            file.StoreString("x");
        }
        // Hidden by name on Unix, by attribute on Windows.
        FileAccess.SetHiddenAttribute(Root + "/.hidden.png", true);
    }

    private static void RemoveTree(string root)
    {
        if (!DirAccess.DirExistsAbsolute(root))
        {
            return;
        }
        using DirAccess dir = DirAccess.Open(root);
        dir.IncludeHidden = true;
        string[] folders = dir.GetDirectoryPaths(true);
        foreach (string path in dir.GetFilePaths(true))
        {
            DirAccess.RemoveAbsolute(path);
        }
        foreach (string folder in folders.Reverse())
        {
            DirAccess.RemoveAbsolute(folder);
        }
        DirAccess.RemoveAbsolute(root);
    }

    private static string[] Paths(string basePath, params string[] entries)
    {
        return entries.Select(entry => basePath.PathJoin(entry)).ToArray();
    }

    private void ExpectPaths(string[] actual, string[] expected, string message)
    {
        Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");
    }

    private void TestFilePaths()
    {
        ExpectPaths(DirAccess.GetFilePathsAt(Root), Paths(Root, "a.PNG", "b.txt"), "GetFilePathsAt lists one folder, without hidden files");
        string[] all = Paths(Root, "a.PNG", "b.txt", "sub1/c.png", "sub1/d.WebP", "sub1/noext", "sub1/sub2/e.Tres", "sub1/sub2/f.json");
        ExpectPaths(DirAccess.GetFilePathsAt(Root, true), all, "GetFilePathsAt(recursive) lists all levels, sorted");
        string[] images = Paths(Root, "a.PNG", "sub1/c.png", "sub1/d.WebP");
        ExpectPaths(DirAccess.GetFilePathsAt(Root, true, ["png", "webp"]), images, "the extension filter ignores case");
        ExpectPaths(DirAccess.GetFilePathsAt(Root, true, [".PNG"]), Paths(Root, "a.PNG", "sub1/c.png"), "a leading dot in the filter is ignored");
        ExpectPaths(DirAccess.GetFilePathsAt(Root + "/", true, ["json"]), Paths(Root, "sub1/sub2/f.json"), "a trailing slash gives no double slash");
    }

    private void TestDirectoryPaths()
    {
        ExpectPaths(DirAccess.GetDirectoryPathsAt(Root), Paths(Root, "sub1", "sub3"), "GetDirectoryPathsAt lists one folder");
        ExpectPaths(DirAccess.GetDirectoryPathsAt(Root, true), Paths(Root, "sub1", "sub1/sub2", "sub3"), "GetDirectoryPathsAt(recursive) lists all levels");
        ExpectPaths(DirAccess.GetDirectoryPathsAt(Root + "/sub3", true), [], "an empty folder gives an empty array");
    }

    private void TestExtensions()
    {
        ExpectPaths(DirAccess.GetFileExtensionsAt(Root), ["png", "txt"], "GetFileExtensionsAt lists one folder");
        ExpectPaths(DirAccess.GetFileExtensionsAt(Root, true), AllExtensions, "GetFileExtensionsAt(recursive) gives distinct lower-case extensions, sorted");
    }

    private void TestInstanceMethods()
    {
        using DirAccess dir = DirAccess.Open(Root);
        string basePath = dir.GetCurrentDir();
        ExpectPaths(dir.GetFilePaths(false, ["png"]), Paths(basePath, "a.PNG"), "GetFilePaths skips hidden files by default");
        dir.IncludeHidden = true;
        ExpectPaths(dir.GetFilePaths(true, ["png"]), Paths(basePath, ".hidden.png", "a.PNG", "sub1/c.png"), "GetFilePaths honors IncludeHidden");
        dir.IncludeNavigational = true;
        ExpectPaths(dir.GetDirectoryPaths(true), Paths(basePath, "sub1", "sub1/sub2", "sub3"), "GetDirectoryPaths never returns . and ..");
        ExpectPaths(dir.GetFileExtensions(true), AllExtensions, "GetFileExtensions works on the instance");
    }

    private void TestResFolder()
    {
        ExpectPaths(DirAccess.GetFilePathsAt(Feature, true, ["gd"]), Paths(Feature, "scripts/gd/Example.gd", "scripts/gd/Test.gd"), "res:// keeps the res:// prefix");
        Expect(DirAccess.GetDirectoryPathsAt(Feature, true).Contains(Feature + "/scripts/gd"), "res:// directory paths include nested folders");
    }

    private void TestMissingFolder()
    {
        ExpectPaths(DirAccess.GetFilePathsAt(Root + "/missing", true), [], "a missing folder gives an empty array");
    }

    // A link to a folder (a junction on Windows, which needs no extra privileges) is listed, but not entered.
    private void TestFolderLink()
    {
        string target = ProjectSettings.GlobalizePath(Root + "/sub1");
        string link = ProjectSettings.GlobalizePath(Root + "/link");
        int code;
        if (OS.GetName() == "Windows")
        {
            code = OS.Execute("cmd.exe", ["/c", "mklink", "/J", link.Replace("/", "\\"), target.Replace("/", "\\")]);
        }
        else
        {
            code = OS.Execute("ln", ["-s", target, link]);
        }
        ExpectEqual(code, 0, "the folder link is created");
        ExpectPaths(DirAccess.GetDirectoryPathsAt(Root, true), Paths(Root, "link", "sub1", "sub1/sub2", "sub3"), "a linked folder is listed, but not entered");
        ExpectPaths(DirAccess.GetFilePathsAt(Root, true, ["json"]), Paths(Root, "sub1/sub2/f.json"), "the files behind the link are not listed");
        DirAccess.RemoveAbsolute(Root + "/link");
    }

    // Runs this engine with a pack as the main pack, so res:// is served by the pack as in an exported game.
    private void TestPack()
    {
        RemoveTree(PckRoot);
        DirAccess.MakeDirRecursiveAbsolute(PckRoot);
        var sources = new System.Collections.Generic.Dictionary<string, string>
        {
            ["project.godot"] = "config_version=5\n\n[application]\n\nconfig/name=\"pack-test\"\n",
            ["pack_test.gd"] = PackScript,
            ["data/icon.svg.import"] = "x",
            ["data/sub/x.tres.remap"] = "x",
            ["data/sub/deep/y.json"] = "{}",
        };
        var packer = new PckPacker();
        string pack = ProjectSettings.GlobalizePath(PckRoot + "/test.pck");
        ExpectEqual(packer.PckStart(pack), Error.Ok, "PCKPacker starts");
        foreach (var (target, content) in sources)
        {
            packer.AddFileFromBuffer(target, content.ToUtf8Buffer());
        }
        ExpectEqual(packer.Flush(), Error.Ok, "PCKPacker writes the pack");
        var output = new Godot.Collections.Array();
        int code = OS.Execute(OS.GetExecutablePath(), ["--headless", "--main-pack", pack, "--script", "res://pack_test.gd"], output, true);
        string text = string.Join("\n", output);
        ExpectEqual(code, 0, "the engine runs on the pack");
        Expect(text.Contains("PACK_FILES=res://data/icon.svg.import,res://data/sub/deep/y.json,res://data/sub/x.tres.remap"), "pack: GetFilePathsAt(recursive) returns the raw entries (.import, .remap)");
        Expect(text.Contains("PACK_IMPORT=res://data/icon.svg.import"), "pack: the extension filter works");
        Expect(text.Contains("PACK_DIRS=res://data/sub,res://data/sub/deep"), "pack: GetDirectoryPathsAt(recursive)");
        Expect(text.Contains("PACK_EXTENSIONS=import,json,remap"), "pack: GetFileExtensionsAt(recursive)");
        RemoveTree(PckRoot);
    }
}
