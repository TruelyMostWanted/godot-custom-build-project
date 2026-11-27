using Godot;

namespace TruelyInteractive.WebmVideo;

/// <summary>Minimal example of WebM Video: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Setup: a .webm file in user://videos and a VideoStreamPlayer (the guide assumes them).
        DirAccess.MakeDirRecursiveAbsolute("user://videos");
        DirAccess.CopyAbsolute("res://webm-video/resources/testsrc.webm", "user://videos/intro.webm");
        var videoPlayer = new VideoStreamPlayer { Name = "VideoStreamPlayer" };
        AddChild(videoPlayer);

        // Play a video
        var player = GetNode<VideoStreamPlayer>("VideoStreamPlayer");
        player.Stream = GD.Load<VideoStreamWebm>("user://videos/intro.webm");
        player.Play();
        var s = new VideoStreamWebm { File = "user://videos/intro.webm" };
        GD.Print("Playing: ", player.IsPlaying(), ", own stream: ", s.File);

        // Load at runtime
        if (ResourceLoader.Exists("user://videos/intro.webm", "VideoStream"))
        {
            ResourceLoader.LoadThreadedRequest("user://videos/intro.webm", "VideoStream");
            // ... later:
            player.Stream = ResourceLoader.LoadThreadedGet("user://videos/intro.webm") as VideoStream;
            player.Play();
        }
        GD.Print("Threaded stream: ", player.Stream.GetClass());

        // Seek (the sample video is 1 s long)
        GD.Print(player.GetStreamLength());
        player.StreamPosition = 0.5;
        GetTree().Quit();
    }
}
