using System.Linq;
using Godot;

namespace TruelyInteractive.CachedResourcePaths;

/// <summary>Minimal example of Cached Resource Paths: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Resources = "res://cached-resource-paths/resources/";

    public override void _Ready()
    {
        // The guide counts the resources of a running game; here a few resources are loaded first.
        var loaded = new Godot.Collections.Array<Resource>();
        foreach (string file in new[] { "first.tres", "second.tres", "third.tres", "texture.tres", "widget_cs.tres" })
        {
            loaded.Add(GD.Load(Resources + file));
        }

        // Count the loaded resources
        int files = ResourceLoader.GetCachedPaths().Count(p => !p.Contains("::"));
        int scenes = ResourceLoader.GetCachedPathsWithExtension("tscn").Length;
        GD.Print($"{files} resources loaded, {scenes} scenes");

        // List the loaded textures of a level
        var textures = ResourceLoader.GetCachedPathsInDirectory(Resources)
            .Where(p => ResourceLoader.GetCachedRef(p) is Texture2D)
            .ToArray();
        GD.Print("Textures: ", string.Join(", ", textures));
        GD.Print("Widgets: ", string.Join(", ", ResourceLoader.GetCachedPathsForType("WidgetCs", true)));

        // Count and filter by kind
        int imported = ResourceLoader.GetCachedCount(ResourceLoader.CachedResourceKind.Imported);
        int ownFiles = ResourceLoader.GetCachedCount(ResourceLoader.CachedResourceKind.File);
        GD.Print($"Imported: {imported}, own files: {ownFiles}");

        // Scenes and resources of the level, without the imported textures:
        string[] level = ResourceLoader.GetCachedPathsInDirectory(Resources, true,
            ResourceLoader.CachedResourceKind.File | ResourceLoader.CachedResourceKind.BuiltIn);
        GD.Print("Level: ", string.Join(", ", level));

        foreach (var (type, count) in ResourceLoader.GetCachedTypeCounts())
        {
            GD.Print(type, ": ", count);
        }
        GetTree().Quit();
    }
}
