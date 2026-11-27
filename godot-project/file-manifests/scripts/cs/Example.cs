using Godot;

namespace TruelyInteractive.FileManifests;

/// <summary>Minimal example of File Manifests: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Data = "res://file-manifests/resources/data/";

    private FilesManifest _manifest;
    private string[] _paths;

    public override void _Ready()
    {
        // Load all textures of two folders (the sample data has one SVG icon instead of PNG files).
        _manifest = new FilesManifest
        {
            TypeHint = "Texture2D",
            Extensions = ["png", "webp", "svg"],
            FolderPaths = [Data + "a", Data + "b"],
            Recursive = true,
        };

        Godot.Collections.Dictionary textures = ResourceLoader.LoadManifest(_manifest);
        foreach (var (path, value) in textures)
        {
            GD.Print(path, " ", value.As<Texture2D>().GetSize());
        }

        // Order (the example creates a mod and a patch file first).
        DirAccess.MakeDirRecursiveAbsolute("user://mods/mod_a/translations");
        DirAccess.MakeDirRecursiveAbsolute("user://patch");
        ResourceSaver.Save(new Curve(), "user://mods/mod_a/translations/mod_a.tres");
        ResourceSaver.Save(new Curve(), "user://patch/fixes.tres");
        var translations = new FilesManifest
        {
            // Base game first, then every mod in name order: mods override the base game.
            FolderPaths = [Data + "b", "user://mods/*/translations"],
            // A patch file last: it overrides everything.
            FilePaths = ["user://patch/fixes.tres"],
        };
        foreach (string path in ResourceLoader.GetManifestPaths(translations))
        {
            GD.Print(path); // in load order
        }

        // Show a loading bar for a manifest (the example prints the progress instead of filling a ProgressBar).
        _paths = ResourceLoader.GetManifestPaths(_manifest);
        ResourceLoader.LoadThreadedRequestManifest(_manifest);
    }

    public override void _Process(double delta)
    {
        double total = 0.0;
        int done = 0;
        foreach (string path in _paths)
        {
            var progress = new Godot.Collections.Array();
            if (ResourceLoader.LoadThreadedGetStatus(path, progress) != ResourceLoader.ThreadLoadStatus.InProgress)
            {
                done++;
            }
            total += progress[0].AsDouble();
        }
        GD.Print("Loading: ", total / _paths.Length * 100.0, " %");
        if (done == _paths.Length)
        {
            SetProcess(false);
            AfterLoading();
        }
    }

    private void AfterLoading()
    {
        // List save slots and create the folder on the first start.
        var saves = new FilesManifest
        {
            FolderPaths = ["user://saves"],
            Extensions = ["tres"],
            CreateMissingFolders = true,
        };
        string[] slots = ResourceLoader.GetManifestPaths(saves); // empty on the first start
        GD.Print("Save slots: ", slots.Length);

        // React when a player drops a file into the mods folder.
        var mods = new FilesManifest
        {
            FolderPaths = ["user://mods"],
            Extensions = ["tres"],
            CreateMissingFolders = true,
        };
        FilesManifestServer.RegisterManifest("mods", mods);
        FilesManifestServer.Singleton.FileAdded += OnModFileAdded;
        FilesManifestServer.SetWatching("mods", true);
        ResourceSaver.Save(new Curve(), "user://mods/dropped.tres"); // the player drops a file

        FilesManifestValidation validation = FilesManifestServer.Validate("mods");
        GD.Print("Mods valid: ", validation.Valid);
    }

    private void OnModFileAdded(string manifestName, string path)
    {
        GD.Print("New mod file: ", path);
        FilesManifestServer.SetWatching("mods", false);
        DirAccess.RemoveAbsolute(path);
        GetTree().Quit();
    }
}
