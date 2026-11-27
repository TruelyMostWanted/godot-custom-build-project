using Godot;

namespace TruelyInteractive.StreamingAssets;

/// <summary>Minimal example of Streaming Assets: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Ship a folder next to the game: an editor script and an export (skipped, both need the editor).

        // Read a streaming file at runtime.
        using FileAccess file = FileAccess.Open("streaming://config/levels.json", FileAccess.ModeFlags.Read);
        if (file != null)
        {
            var levels = Json.ParseString(file.GetAsText());
            GD.Print("Levels: ", levels);
        }
        GD.Print("Location: ", (long)StreamingAssetsServer.GetLocation());

        // Give a native library the OS path (the example prints it instead of calling a native player).
        string videoPath = StreamingAssetsServer.GetOsPath("streaming://config/levels.json");
        if (string.IsNullOrEmpty(videoPath))
        {
            GD.Print("Play through FileAccess");
        }
        else
        {
            GD.Print("Native player opens ", videoPath);
        }

        // Ship the folder as a second pack (the guide loads a texture of an exported pack; the example prints the location).
        GD.Print(StreamingAssetsServer.GetLocation() == StreamingAssetsServer.Location.Pack);

        // Test the desktop layout without exporting: start the game with --streaming-assets-path=<folder> (command line, skipped).

        // See what an export will ship (editor builds only).
#if TOOLS
        string[] files = StreamingAssetsExportPlugin.CollectFiles("res://streaming-assets/resources/streaming_assets");
        string target = StreamingAssetsExportPlugin.ExpandTemplate("{exe_name}_Data/StreamingAssets", "MyGame");
        GD.Print($"{files.Length} files go to {target}");
#endif

        // Add your own data to a macOS or iOS bundle: an EditorExportPlugin that runs during an export (skipped).
        GetTree().Quit();
    }
}
