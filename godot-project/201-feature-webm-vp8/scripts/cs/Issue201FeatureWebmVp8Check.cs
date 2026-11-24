using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue201FeatureWebmVp8;

/// <summary>C# checks of 201-feature-webm-vp8. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue201FeatureWebmVp8Check : FeatureCheckCs
{
    private const string AlphaPath = "res://201-feature-webm-vp8/resources/vp8_alpha.webm";
    private const string VorbisPath = "res://201-feature-webm-vp8/resources/vp8_vorbis.webm";

    protected override string CheckName => "201-feature-webm-vp8";

    protected override async Task RunChecks()
    {
        Expect(ClassDB.ClassExists("VideoStreamWebm"), "VideoStreamWebm is registered");
        foreach (string path in new[] { AlphaPath, VorbisPath })
        {
            await CheckFile(path);
        }
    }

    private async Task CheckFile(string path)
    {
        string name = path.GetFile();
        var stream = GD.Load<VideoStreamWebm>(path);
        Expect(stream != null, $"{name} loads as VideoStreamWebm");
        if (stream == null)
        {
            return;
        }

        var player = new VideoStreamPlayer { Size = new Vector2(64, 64), Stream = stream };
        AddChild(player);
        player.Play();
        Expect(player.IsPlaying(), $"{name}: VideoStreamPlayer is playing");
        Expect(Math.Abs(player.GetStreamLength() - 1.0) < 0.2, $"{name}: stream length is about 1 s (got {player.GetStreamLength()})");
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Expect(player.StreamPosition > 0.0, $"{name}: stream position advances ({player.StreamPosition})");
        Texture2D texture = player.GetVideoTexture();
        Expect(texture != null, $"{name}: a video texture exists");
        if (texture != null)
        {
            Expect(texture.GetWidth() == 64 && texture.GetHeight() == 64, $"{name}: the video texture is 64x64");
            if (path == AlphaPath)
            {
                Image image = texture.GetImage();
                if (image != null && !image.IsEmpty())
                {
                    float left = image.GetPixel(8, 32).A;
                    float right = image.GetPixel(56, 32).A;
                    Expect(left < 0.5f, $"alpha: pixel in the transparent half has alpha < 0.5 ({left})");
                    Expect(right > 0.5f, $"alpha: pixel in the opaque half has alpha > 0.5 ({right})");
                }
                else
                {
                    Expect(false, "alpha: the video texture image is readable");
                }
            }
        }

        player.StreamPosition = 0.8;
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        Expect(player.StreamPosition >= 0.5, $"{name}: seek moves the position forward ({player.StreamPosition})");
        player.Stop();
        player.QueueFree();
    }
}
