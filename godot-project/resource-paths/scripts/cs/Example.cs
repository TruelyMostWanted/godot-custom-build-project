using Godot;

namespace TruelyInteractive.ResourcePaths;

/// <summary>Minimal example of Resource Paths: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute("user://balance");
        DirAccess.MakeDirRecursiveAbsolute("user://levels");

        // Try it
        TryIt();

        // Reload a resource that is already in use
        ReloadResource();

        // Load a private copy
        LoadPrivateCopy();

        // Save to a new path and keep using it
        SaveToNewPath();

        // Write a loader and a saver
        WriteLoaderAndSaver();
        GetTree().Quit();
    }

    private static void TryIt()
    {
        var stats = new Resource { ResourceName = "stats" };
        ResourceSaver.Save(stats, "user://stats.tres");

        Resource a = GD.Load("user://stats.tres");
        Resource b = GD.Load("user://stats.tres");
        GD.Print("load twice:   same instance = ", a == b, ", path = ", a.ResourcePath);

        Resource copy = ResourceLoader.Load("user://stats.tres", "", ResourceLoader.CacheMode.Ignore);
        GD.Print("IGNORE:       same instance = ", copy == a, ", path = ", copy.ResourcePath);
        GD.Print("              load() still returns the first: ", GD.Load("user://stats.tres") == a);

        Resource again = ResourceLoader.Load("user://stats.tres", "", ResourceLoader.CacheMode.Replace);
        GD.Print("REPLACE:      same instance = ", again == a);

        var profile = new Resource();
        ResourceSaver.Save(profile, "user://profile.tres");
        GD.Print("save:         path = '", profile.ResourcePath, "'");
        ResourceSaver.Save(profile, "user://profile.tres", ResourceSaver.SaverFlags.ChangePath);
        GD.Print("CHANGE_PATH:  path = '", profile.ResourcePath, "'");
        profile.TakeOverPath("user://profile.tres");
        GD.Print("take over:    path = '", profile.ResourcePath, "', load() returns it: ", GD.Load("user://profile.tres") == profile);

        var other = new Resource();
        other.TakeOverPath("user://profile.tres");
        GD.Print("another takes it: first path = '", profile.ResourcePath, "', other path = '", other.ResourcePath, "'");
    }

    private static void ReloadResource()
    {
        var first = new Resource { ResourceName = "old values" };
        ResourceSaver.Save(first, "user://balance/stats.tres");
        Resource stats = GD.Load("user://balance/stats.tres");
        // ... the file is rewritten ...
        var rewritten = new Resource { ResourceName = "new values" };
        ResourceSaver.Save(rewritten, "user://balance/stats.tres");
        Resource same = ResourceLoader.Load("user://balance/stats.tres", "", ResourceLoader.CacheMode.Replace);
        GD.Print("Same instance, new values: ", same == stats, ", ", stats.ResourceName);
    }

    private static void LoadPrivateCopy()
    {
        ResourceSaver.Save(new Resource(), "user://levels/level_1.tres");
        Resource running = GD.Load("user://levels/level_1.tres");
        Resource copy = ResourceLoader.Load("user://levels/level_1.tres", "", ResourceLoader.CacheMode.Ignore);
        GD.Print("Copy is another instance: ", copy != running);
        GD.Print("Same path: ", copy.ResourcePath == running.ResourcePath);
    }

    private static void SaveToNewPath()
    {
        // The guide saves a PlayerProfile; a plain Resource does the same here.
        var profile = new Resource();
        if (ResourceSaver.Save(profile, "user://new_profile.tres") == Error.Ok)
        {
            profile.TakeOverPath("user://new_profile.tres");
            // GD.Load("user://new_profile.tres") now returns this instance.
            GD.Print("Load returns the profile: ", GD.Load("user://new_profile.tres") == profile);
        }
    }

    private static void WriteLoaderAndSaver()
    {
        var loader = new NoteLoader();
        var saver = new NoteSaver();
        ResourceLoader.AddResourceFormatLoader(loader);
        ResourceSaver.AddResourceFormatSaver(saver);
        var note = new Resource { ResourceName = "Buy milk" };
        ResourceSaver.Save(note, "user://shopping.note");
        Resource loaded = ResourceLoader.Load("user://shopping.note", "", ResourceLoader.CacheMode.Ignore);
        GD.Print("Note: ", loaded.ResourceName);
        ResourceLoader.RemoveResourceFormatLoader(loader);
        ResourceSaver.RemoveResourceFormatSaver(saver);
    }
}
