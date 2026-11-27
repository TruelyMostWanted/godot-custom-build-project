using System.Linq;
using Godot;

namespace TruelyInteractive.ResourceFormatLimits;

/// <summary>Minimal example of Resource Format Limits: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Register many formats
        foreach (string extension in new[] { "foo", "bar", "baz" })
        {
            ResourceLoader.AddResourceFormatLoader(new MyLoader { Extension = extension });
            ResourceSaver.AddResourceFormatSaver(new MySaver { Extension = extension });
        }
        string[] extensions = ResourceLoader.GetRecognizedExtensionsForType("Resource");
        GD.Print("Recognized: ", extensions.Contains("foo"), " ", extensions.Contains("bar"), " ", extensions.Contains("baz"));
        GetTree().Quit();
    }
}
