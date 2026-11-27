using Godot;

namespace TruelyInteractive.LoadResults;

/// <summary>Minimal example of Load Results: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
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
