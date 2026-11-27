using Godot;

namespace TruelyInteractive.PathPrefixes;

/// <summary>Minimal example of Path Prefixes: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Open the mods folder next to the executable (the example uses user://mods to run anywhere).
        DirAccess.MakeDirRecursiveAbsolute("user://mods/my_mod");
        using (FileAccess modFile = FileAccess.Open("user://mods/my_mod/mod.toml", FileAccess.ModeFlags.Write))
        {
            modFile.StoreString("name = \"my_mod\"");
        }
        PathPrefixServer.RegisterPrefixFolder("mods://", "user://mods");
        using DirAccess mods = DirAccess.Open("mods://");
        string manifest = FileAccess.GetFileAsString("mods://my_mod/mod.toml");
        GD.Print("Mod folders: ", string.Join(", ", mods.GetDirectories()));
        GD.Print("Manifest: ", manifest);

        // Use a different folder per platform.
        PathPrefixServer.RegisterPrefixFolder("saves://", "user://saves");
        PathPrefixServer.RegisterPrefixFolder("saves://", "exec://saves", ["windows", "linux"]);
        GD.Print("saves:// is ", PathPrefixServer.Globalize("saves://slot_1.json"));

        // Compute the folder with your own logic.
        PathPrefixServer.RegisterPrefix("levels://", new LevelsPrefix());
        GD.Print("levels:// is ", PathPrefixServer.Globalize("levels://a/b.tres"));

        // Add a prefix for a system folder (saving a screenshot needs a window, so only the path is shown).
        PathPrefixServer.RegisterPrefix("pictures://", PathPrefixServer.CreateSystemDirPrefix(OS.SystemDir.Pictures));
        GD.Print("pictures:// is ", PathPrefixServer.Globalize("pictures://screenshot.png"));

        // Turn a prefixed path into an OS path and back.
        PathInfo info = PathPrefixServer.Resolve("USER://saves\a.json");
        GD.Print(info.LocalPath);
        GD.Print(info.RelativePath);
        string osPath = PathPrefixServer.Globalize("appdata://saves/slot_1.json");
        GD.Print(PathPrefixServer.Localize(osPath));

        // Check paths from mods or players.
        string pathFromPlayer = "user://saves/slot_1.json";
        DirAccess.MakeDirRecursiveAbsolute("user://saves");
        PathInfo safe = PathPrefixServer.ResolveSafe(pathFromPlayer, PathPrefixServer.AccessMode.Write, ["user://saves/"]);
        if (safe.Valid)
        {
            using FileAccess file = FileAccess.Open(safe.LocalPath, FileAccess.ModeFlags.Write);
            GD.Print("Opened ", file.GetPath());
        }
        else
        {
            GD.PushError(safe.ErrorMessage);
        }

        // Point a prefix into res:// or user://.
        PathPrefixServer.RegisterPrefixFolder("levels://", "res://path-prefixes/resources");
        GD.Print(PathPrefixServer.ResolveEnginePath("levels://level.tres"));
        Resource level = GD.Load("levels://level.tres");
        GD.Print("Level: ", level.ResourceName);
        GetTree().Quit();
    }
}
