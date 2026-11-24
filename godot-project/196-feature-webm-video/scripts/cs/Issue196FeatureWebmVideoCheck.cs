using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue196FeatureWebmVideo;

/// <summary>C# checks of 196-feature-webm-video. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue196FeatureWebmVideoCheck : FeatureCheckCs
{
    private const string VideoPath = "res://196-feature-webm-video/resources/testsrc.webm";
    private const string AlphaPath = "res://196-feature-webm-video/resources/testsrc_alpha.webm";

    protected override string CheckName => "196-feature-webm-video";

    protected override async Task RunChecks()
    {
        Expect(ClassDB.ClassExists("VideoStreamWebm"), "VideoStreamWebm is registered");

        // Resource loading.
        var stream = GD.Load<VideoStreamWebm>(VideoPath);
        Expect(stream != null, "the .webm loads as VideoStreamWebm");
        if (stream == null)
        {
            return;
        }
        Expect(stream.File == VideoPath, "file is the loaded path");
        var alpha = GD.Load<VideoStreamWebm>(AlphaPath);
        Expect(alpha != null, "the alpha .webm loads as VideoStreamWebm");

        // Runtime: a new stream with the file set.
        var runtime = new VideoStreamWebm { File = VideoPath };
        Expect(runtime.File == VideoPath, "the file property can be set at runtime");

        // Playback.
        var player = new VideoStreamPlayer { Size = new Vector2(64, 64), Stream = stream };
        AddChild(player);
        player.Play();
        Expect(player.IsPlaying(), "VideoStreamPlayer is playing");
        Expect(Math.Abs(player.GetStreamLength() - 1.0) < 0.2, $"stream length is about 1 s (got {player.GetStreamLength()})");
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Expect(player.StreamPosition > 0.0, $"stream position advances ({player.StreamPosition})");
        Texture2D texture = player.GetVideoTexture();
        Expect(texture != null, "a video texture exists");
        if (texture != null)
        {
            Expect(texture.GetWidth() == 64 && texture.GetHeight() == 64, "the video texture is 64x64");
        }

        // Seek.
        player.StreamPosition = 0.8;
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        Expect(player.StreamPosition >= 0.5, $"seek moves the position forward ({player.StreamPosition})");
        player.Stop();
        player.QueueFree();

        // Alpha file plays too.
        var alphaPlayer = new VideoStreamPlayer { Stream = alpha };
        AddChild(alphaPlayer);
        alphaPlayer.Play();
        Expect(Math.Abs(alphaPlayer.GetStreamLength() - 1.0) < 0.2, $"alpha stream length is about 1 s (got {alphaPlayer.GetStreamLength()})");
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        Expect(alphaPlayer.GetVideoTexture() != null, "the alpha video has a texture");
        alphaPlayer.Stop();
        alphaPlayer.QueueFree();
    }
}
