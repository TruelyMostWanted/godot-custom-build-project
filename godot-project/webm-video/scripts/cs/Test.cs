using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.WebmVideo;

/// <summary>Full API test of WebM Video (VideoStreamWebm: VP9, VP8, alpha, seek). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Res = "res://webm-video/resources";
    private const double Length = 1.0;
    private const double LengthTolerance = 0.2;
    private const double SeekPosition = 0.8;
    private const double MinPositionAfterSeek = 0.5;
    private static readonly Vector2 VideoSize = new(64, 64);

    // VP9, VP9 with alpha, VP8 with alpha, VP8 with Vorbis audio. All are 1 s long and 64x64.
    private static readonly string[] Files = ["testsrc.webm", "testsrc_alpha.webm", "vp8_alpha.webm", "vp8_vorbis.webm"];

    protected override async Task RunTests()
    {
        Expect(ClassDB.ClassExists("VideoStreamWebm"), "VideoStreamWebm is registered");
        TestFileProperty();
        foreach (string fileName in Files)
        {
            await TestVideo(fileName);
        }
    }

    private void TestFileProperty()
    {
        var stream = new VideoStreamWebm { File = Res + "/testsrc.webm" };
        ExpectEqual(stream.File, Res + "/testsrc.webm", "the File property can be set at runtime");
    }

    private async Task TestVideo(string fileName)
    {
        string path = $"{Res}/{fileName}";
        var stream = GD.Load<VideoStreamWebm>(path);
        Expect(stream != null, $"{fileName} loads as VideoStreamWebm");
        ExpectEqual(stream.File, path, $"{fileName}: File is the loaded path");
        var player = new VideoStreamPlayer { Size = VideoSize, Stream = stream };
        AddChild(player);
        player.Play();
        Expect(player.IsPlaying(), $"{fileName}: VideoStreamPlayer is playing");
        Expect(Math.Abs(player.GetStreamLength() - Length) < LengthTolerance, $"{fileName}: stream length is about 1 s");
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Expect(player.StreamPosition > 0.0, $"{fileName}: stream position advances");
        Texture2D texture = player.GetVideoTexture();
        ExpectEqual(texture.GetSize(), VideoSize, $"{fileName}: the video texture is 64x64");
        player.StreamPosition = SeekPosition;
        await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        Expect(player.StreamPosition >= MinPositionAfterSeek, $"{fileName}: seek moves the position forward");
        player.QueueFree();
    }
}
