using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue105FeatureTheoraLoader;

/// <summary>C# checks of 105-feature-theora-loader. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue105FeatureTheoraLoaderCheck : FeatureCheckCs
{
    private const string Dir = "user://105-feature-theora-loader-cs";
    private const string Res = "res://105-feature-theora-loader/resources";

    protected override string CheckName => "105-feature-theora-loader";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        string path = Dir.PathJoin("clip.ogv");
        // clip.ogv: 1 s of a 64x48 test pattern at 10 FPS, Ogg Theora without audio. The editor does not import .ogv files.
        byte[] bytes = FileAccess.GetFileAsBytes(Res.PathJoin("clip.ogv"));
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreBuffer(bytes);
        }

        Expect(ResourceLoader.Exists(path), "ResourceLoader.Exists() finds an .ogv in user://");
        Expect(ResourceLoader.Exists(path, "VideoStream"), "Exists() with type hint VideoStream");
        Expect(ResourceLoader.GetRecognizedExtensionsForType("VideoStreamTheora").Contains("ogv"), "ogv is a recognized extension");
        Expect(ResourceLoader.GetRecognizedExtensionsForType("Resource").Contains("ogv"), "ogv is recognized for the type Resource");

        var stream = ResourceLoader.Load<VideoStreamTheora>(path, "VideoStreamTheora", ResourceLoader.CacheMode.Ignore);
        if (Expect(stream != null, "ResourceLoader.Load() returns a VideoStreamTheora from user://"))
        {
            ExpectEqual(stream.File, path, "the stream points to the loaded file");
            Expect(ResourceLoader.Load(path, "Resource", ResourceLoader.CacheMode.Ignore) is VideoStreamTheora, "Load() with type hint Resource");

            // No Theora encoder at runtime.
            Expect(!ResourceSaver.GetRecognizedExtensions(stream).Contains("ogv"), "ResourceSaver offers no .ogv");
            Expect(ResourceSaver.Save(stream, Dir.PathJoin("copy.ogv")) != Error.Ok, "ResourceSaver.Save() cannot write .ogv");

            // Plays in a VideoStreamPlayer.
            var player = new VideoStreamPlayer();
            AddChild(player);
            player.Stream = stream;
            Expect(Math.Abs(player.GetStreamLength() - 1.0) < 0.15, $"length is about 1 s (got {player.GetStreamLength()})");
            player.Play();
            for (int i = 0; i < 5; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Expect(player.IsPlaying(), "the stream plays");
            Texture2D texture = player.GetVideoTexture();
            if (Expect(texture != null, "the player has a video texture"))
            {
                ExpectEqual(texture.GetSize(), new Vector2(64, 48), "video size is 64x48");
            }
            player.QueueFree();
        }

        Resource first = GD.Load(path);
        Expect(first != null && first == GD.Load(path), "Load() caches the stream");

        // Not an Ogg file.
        string broken = Dir.PathJoin("broken.ogv");
        using (FileAccess brokenFile = FileAccess.Open(broken, FileAccess.ModeFlags.Write))
        {
            brokenFile.StoreString("not a video");
        }
        Expect(ResourceLoader.Load(broken, "", ResourceLoader.CacheMode.Ignore) == null, "a file that is not Ogg does not load");

        // res:// without import and with import mode Keep File: both come from the runtime loader.
        Expect(GD.Load(Res.PathJoin("clip.ogv")) is VideoStreamTheora, "res:// .ogv without import loads");
        Expect(GD.Load(Res.PathJoin("kept_clip.ogv")) is VideoStreamTheora, ".ogv with import mode Keep File loads");

        // Threaded loading.
        string threaded = Dir.PathJoin("threaded.ogv");
        DirAccess.CopyAbsolute(path, threaded);
        ExpectEqual(ResourceLoader.LoadThreadedRequest(threaded, "VideoStreamTheora"), Error.Ok, "LoadThreadedRequest() accepts a user:// .ogv");
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(threaded);
        int frames = 0;
        while (status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            frames++;
            status = ResourceLoader.LoadThreadedGetStatus(threaded);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Loaded, "threaded load finishes");
        Expect(ResourceLoader.LoadThreadedGet(threaded) is VideoStreamTheora, "LoadThreadedGet() returns the VideoStreamTheora");
    }
}
