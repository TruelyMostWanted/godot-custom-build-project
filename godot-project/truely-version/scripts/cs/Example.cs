using Godot;

namespace TruelyInteractive.TruelyVersion;

/// <summary>Minimal example of the Truely version: the snippet of the wiki page Releases. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Read the version in a game.
        Godot.Collections.Dictionary info = Engine.GetVersionInfo();
        GD.Print($"Truely v{(string)info["truely_version"]} on Godot {(string)info["string"]}");

        if ((int)info["truely_hex"] >= 0x000200)
        {
            GD.Print("This engine has the features of Truely 0.2.0.");
        }
        GetTree().Quit();
    }
}
