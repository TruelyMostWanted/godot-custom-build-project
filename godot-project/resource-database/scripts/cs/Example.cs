using Godot;
using Database = Godot.ResourceDatabase;

namespace TruelyInteractive.ResourceDatabase;

/// <summary>Minimal example of Resource Database: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.
/// The alias Database avoids the clash between the namespace and the ResourceDatabase class.</summary>
public partial class Example : Node
{
    private const string Items = "res://resource-database/resources/items/";

    public override void _Ready()
    {
        // Preload the items of the game
        var armor = new FilesManifest { FolderPaths = ["res://resource-database/resources/armor"], Extensions = ["tres"] };
        var list = new Preloads
        {
            Paths = [Items + "sword.tres", Items + "shield.tres"],
            Manifests = [armor],
            Keys = new Godot.Collections.Dictionary<string, string> { { "hero_sword", Items + "sword.tres" } },
        };
        Database.Singleton.PreloadFinished += OnPreloadFinished;
        Database.PreloadFrom(list);

        // Preload at startup
        // Nothing to run here: the Preloads is saved as resources/preloads.tres and added to the project setting
        // filesystem/resources/preloads. This example does not change the project settings.
    }

    private void OnPreloadFinished(Preloads list, Error error)
    {
        GD.Print("Preload finished: ", error);
        Resource sword = Database.GetResource("hero_sword");
        GD.Print("hero_sword: ", sword.ResourceName);

        // Find resources by pattern and type
        // (the guide finds DatabaseItem resources; the example items are plain Resources, so the type is Resource)
        var swords = Database.FindResources("%sword%", "Resource");
        string[] itemKeys = Database.FindKeys("%items/%");
        GD.Print("Swords: ", swords.Count, ", item keys: ", itemKeys.Length);
        if (Database.AddResourceIfMissing("player/stats", new Resource()))
        {
            GD.Print("stored");
        }

        // Free the resources of a level
        long removed = Database.RemoveMatching(Items + "%");
        GD.Print("Removed: ", removed);
        Database.Clear();
        GetTree().Quit();
    }
}
