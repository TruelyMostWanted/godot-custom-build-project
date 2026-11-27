using System.Threading.Tasks;
using Godot;

namespace TruelyInteractive.VideoFiles;

/// <summary>Minimal example of Video Files: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override async void _Ready()
    {
        // Setup: an .ogv file in user://videos and a VideoStreamPlayer (the guide assumes them).
        DirAccess.MakeDirRecursiveAbsolute("user://videos");
        DirAccess.CopyAbsolute("res://video-files/resources/clip.ogv", "user://videos/intro.ogv");
        var videoPlayer = new VideoStreamPlayer { Name = "VideoStreamPlayer" };
        AddChild(videoPlayer);

        // Play a video from any folder
        var player = GetNode<VideoStreamPlayer>("VideoStreamPlayer");
        player.Stream = GD.Load<VideoStreamTheora>("user://videos/intro.ogv");
        player.Play();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print("Playing: ", player.IsPlaying());
        GetTree().Quit();
    }
}
