using Godot;

namespace TruelyInteractive.ResourceDatabase;

/// <summary>Minimal example of Resource Database: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Replace with the first snippet of the guide.
        var version = Engine.GetVersionInfo();
        GD.Print("Engine version: ", version["string"]);
        GetTree().Quit();
    }
}
