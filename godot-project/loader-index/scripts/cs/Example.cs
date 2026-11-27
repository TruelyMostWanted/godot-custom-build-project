using System.Linq;
using Godot;

namespace TruelyInteractive.LoaderIndex;

/// <summary>Minimal example of Loader Index: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Write a loader that the index finds
        var myLoader = new MyLoader();
        ResourceLoader.AddResourceFormatLoader(myLoader);
        GD.Print("mydata known: ", ResourceLoader.GetRecognizedExtensionsForType("Resource").Contains("mydata"));

        // Change the extensions of a loader at runtime
        myLoader.Extensions = ["mydata", "mydata2"];
        ResourceLoader.RebuildExtensionIndex();
        GD.Print(ResourceLoader.Exists("res://level.mydata2"));

        ResourceLoader.RemoveResourceFormatLoader(myLoader);
        GetTree().Quit();
    }
}
