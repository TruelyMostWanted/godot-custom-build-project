using Godot;

namespace TruelyInteractive.LoadResults;

/// <summary>Minimal example of Load Results: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Level = "user://levels/custom.tscn";

    public override void _Ready()
    {
        // Tell the player why a save game didn't load
        // (the guide loads a SaveGame; this example saves and loads a plain Resource, and prints instead of showing a dialog)
        DirAccess.MakeDirRecursiveAbsolute("user://saves");
        ResourceSaver.Save(new Resource(), "user://saves/slot_1.tres");
        if (ResourceLoader.TryLoad<Resource>("user://saves/slot_1.tres", out Resource save, out Error error))
        {
            GD.Print("Apply save: ", save);
        }
        else if (error == Error.FileNotFound)
        {
            GD.Print("Start new game");
        }
        else
        {
            ResourceLoadResult result = ResourceLoader.LoadResult("user://saves/slot_1.tres", "Resource");
            GD.Print("The save game is broken:\n", result.ErrorMessage);
        }

        // Check that a file was saved
        var settings = new Resource();
        if (!ResourceSaver.TrySave(settings, "user://settings.tres", out Error saveError))
        {
            GD.PushError($"Settings not saved: {saveError}");
        }
        GD.Print("Settings saved: ", saveError == Error.Ok);

        // Get the error of a background load
        // (the example saves the level first)
        var level = new Node();
        var scene = new PackedScene();
        scene.Pack(level);
        level.Free();
        DirAccess.MakeDirRecursiveAbsolute("user://levels");
        ResourceSaver.Save(scene, Level);
        ResourceLoader.LoadThreadedRequest(Level);
    }

    public override void _Process(double delta)
    {
        if (ResourceLoader.LoadThreadedGetStatus(Level) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            return;
        }
        SetProcess(false);
        ResourceLoadResult result = ResourceLoader.LoadThreadedGetResult(Level);
        if (result.Ok)
        {
            AddChild(((PackedScene)result.Resource).Instantiate());
            GD.Print("Level loaded");
        }
        else
        {
            GD.Print(result.ErrorMessage);
        }

        // Give callers a message from your own loader
        // (the example loads a quiz file with text; an empty file would give the message of the guide)
        using (FileAccess file = FileAccess.Open("user://questions.lrquiz", FileAccess.ModeFlags.Write))
        {
            file.StoreString("What is 6 * 7?");
        }
        var loader = new QuizLoader();
        ResourceLoader.AddResourceFormatLoader(loader);
        ResourceLoadResult quiz = ResourceLoader.LoadResult("user://questions.lrquiz");
        GD.Print("Quiz loaded: ", quiz.Ok);
        ResourceLoader.RemoveResourceFormatLoader(loader);
        GetTree().Quit();
    }
}
