using Godot;

namespace TruelyInteractive.RecursiveDirectoryListing;

/// <summary>Minimal example of Recursive Directory Listing: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private static void CreateFile(string path)
    {
        DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString("x");
    }

    public override void _Ready()
    {
        // Setup: the files the guide talks about.
        CreateFile("user://saves/slot_1.tres");
        CreateFile("user://saves/backup/slot_1.tres");
        CreateFile("user://mods/my_mod/icon.png");
        CreateFile("user://mods/my_mod/art/banner.webp");
        CreateFile("user://mods/my_mod/mod.json");
        DirAccess.RemoveAbsolute("user://config/.secret"); // a hidden file on Windows cannot be overwritten
        CreateFile("user://config/settings.cfg");
        CreateFile("user://config/.secret");
        FileAccess.SetHiddenAttribute("user://config/.secret", true);

        // List all files below a folder.
        foreach (string path in DirAccess.GetFilePathsAt("user://saves", true))
        {
            GD.Print(path);
        }
        GD.Print(string.Join(", ", DirAccess.GetDirectoryPathsAt("user://saves", true)));

        // Find the images of a mod.
        string[] images = DirAccess.GetFilePathsAt("user://mods/my_mod", true, ["png", "webp"]);
        string[] kinds = DirAccess.GetFileExtensionsAt("user://mods/my_mod", true);
        GD.Print(string.Join(", ", images));
        GD.Print(string.Join(", ", kinds));

        // Include hidden files.
        using DirAccess dir = DirAccess.Open("user://config");
        dir.IncludeHidden = true;
        string[] allFiles = dir.GetFilePaths(true);
        GD.Print(string.Join(", ", allFiles));
        GetTree().Quit();
    }
}
