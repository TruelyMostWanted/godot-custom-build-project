using Godot;

namespace TruelyInteractive.PckPackages;

/// <summary>Minimal example of PCK Packages: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string BasePack = "res://pck-packages/resources/sample_pack.pck";
    private const string ZeroKey = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string SecretKey = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";

    private static void WritePack(string path, System.Collections.Generic.Dictionary<string, string> files, string key = ZeroKey, bool encrypt = false)
    {
        DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
        var packer = new PckPacker();
        packer.PckStart(path, 32, key, encrypt);
        foreach (var (target, content) in files)
        {
            packer.AddFileFromBuffer(target, content.ToUtf8Buffer(), encrypt);
        }
        packer.Flush();
    }

    public override void _Ready()
    {
        // Setup: the packs and files the guide talks about.
        WritePack("user://mods/level_pack.pck", new()
        {
            ["levels/readme.txt"] = "Ten levels.",
            ["levels/old.tscn"] = "old",
            ["levels/from_base_game.tscn"] = "base",
        });
        WritePack("user://dlc/forest.pck", new() { ["dlc/forest/level.tscn"] = "forest" });
        WritePack("user://downloads/mod.pck", new() { ["levels/a.tscn"] = "a", ["data/info.json"] = "{}", ["docs/notes.txt"] = "notes" });
        WritePack("user://secret.pck", new() { ["secret/hidden.txt"] = "hidden" }, SecretKey, true);
        using (FileAccess icon = FileAccess.Open("user://export_icon.png", FileAccess.ModeFlags.Write))
        {
            icon.StoreString("png");
        }

        // Describe a pack as a .tres file (the guide's res://art/a.png and res://base.pck are the sample files).
        var desc = new PckPackage { BasePack = BasePack };
        desc.AddFile("textures/a.png", "res://icon.svg");
        desc.AddFileFromBuffer("config.json", "{}".ToUtf8Buffer());
        desc.AddRemoval("old/c.png");
        ResourceSaver.Save(desc, "user://dlc.tres");
        ResourceSaver.Save(desc, "user://dlc.pck");
        GD.Print("Description: ", string.Join(", ", desc.GetFilePaths()));

        // List the files of a mod pack.
        PckPackage package = PckPackageServer.Open("user://mods/level_pack.pck");
        if (package == null)
        {
            return;
        }
        foreach (PckPackageEntry entry in package.GetEntries())
        {
            GD.Print(entry.Path, " ", entry.Size);
        }
        string readme = PckPackageServer.ReadFile(package, "res://levels/readme.txt").GetStringFromUtf8();
        bool intact = PckPackageServer.Verify(package) == Error.Ok;
        GD.Print(readme, " ", intact);

        // Mount a DLC and unmount it again (the guide changes to the scene of the DLC; the example prints the file).
        if (PckPackageServer.Mount("user://dlc/forest.pck") == Error.Ok)
        {
            GD.Print("Mounted: ", FileAccess.FileExists("res://dlc/forest/level.tscn"));
        }

        PckPackageServer.Unmount("user://dlc/forest.pck");

        // Add a file to a pack.
        package.AddFileFromBuffer("res://levels/extra.txt", "Hello".ToUtf8Buffer());
        package.AddFile("res://levels/icon.png", "user://export_icon.png");
        package.RemoveFile("res://levels/old.tscn");
        package.AddRemoval("res://levels/from_base_game.tscn");
        PckPackageServer.Build(package, "user://mods/level_pack_v2.pck");

        // Build a DLC pack with imported resources and Keep DLC content out of the main export:
        // both need the editor and an export preset (PCKPackageExporter, Project > Tools), so the example skips them.

        // Unpack a mod into user://.
        PckPackage mod = PckPackageServer.Open("user://downloads/mod.pck");
        PckPackageServer.ExtractProgress += (path, done, total) => GD.Print(100.0 * done / total, " % ", path);
        PckExtractResult result = PckPackageServer.Extract(mod, "user://mods/my_mod", ["res://levels/", "*.json"], PckPackageServer.ExtractPolicy.Rename);
        if (!result.IsOk())
        {
            GD.PushError(string.Join("\n", result.Errors));
        }
        GD.Print("Written: ", string.Join(", ", result.Written));

        // Open an encrypted pack.
        PckPackage secret = PckPackageServer.Open("user://secret.pck", SecretKey);
        if (secret == null)
        {
            GD.PushError("Wrong key or not a pack.");
        }
        GD.Print("Secret: ", string.Join(", ", secret.GetFilePaths()));
        GetTree().Quit();
    }
}
