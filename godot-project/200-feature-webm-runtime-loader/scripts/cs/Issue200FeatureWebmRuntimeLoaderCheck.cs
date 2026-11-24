using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue200FeatureWebmRuntimeLoader;

/// <summary>C# checks of 200-feature-webm-runtime-loader. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue200FeatureWebmRuntimeLoaderCheck : FeatureCheckCs
{
    private const string VideoPath = "res://200-feature-webm-runtime-loader/resources/testsrc.webm";
    private const string UserDir = "user://200-feature-webm-runtime-loader-cs";
    private const string UserPath = UserDir + "/testsrc.webm";
    private const string ThreadedPath = UserDir + "/threaded.webm";
    private const string BrokenPath = UserDir + "/broken.webm";

    protected override string CheckName => "200-feature-webm-runtime-loader";

    private static void Write(string path, byte[] bytes)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreBuffer(bytes);
    }

    protected override async Task RunChecks()
    {
        // res:// file (WebM has no importer): loaded by the same loader.
        var inProject = GD.Load<VideoStreamWebm>(VideoPath);
        Expect(inProject != null, "the res:// .webm loads as VideoStreamWebm");

        // Copy the file into user:// at runtime.
        byte[] bytes = FileAccess.GetFileAsBytes(VideoPath);
        Expect(bytes.Length > 0, "the source file can be read");
        DirAccess.MakeDirRecursiveAbsolute(UserDir);
        Write(UserPath, bytes);
        Write(ThreadedPath, bytes);

        Expect(ResourceLoader.Exists(UserPath), "ResourceLoader.exists() finds the user:// .webm");
        Expect(ResourceLoader.Exists(UserPath, "VideoStream"), "ResourceLoader.exists() with type hint VideoStream");
        Expect(ResourceLoader.Exists(UserPath, "Resource"), "ResourceLoader.exists() with type hint Resource");
        Expect(!ResourceLoader.Exists(UserDir + "/missing.webm"), "ResourceLoader.exists() is false for a missing file");

        var stream = ResourceLoader.Load(UserPath, "", ResourceLoader.CacheMode.Ignore) as VideoStreamWebm;
        Expect(stream != null, "ResourceLoader.load() of user:// returns a VideoStreamWebm");
        if (stream == null)
        {
            return;
        }
        Expect(stream.File == UserPath, "file is the user:// path");
        var asResource = ResourceLoader.Load(UserPath, "Resource", ResourceLoader.CacheMode.Ignore);
        Expect(asResource is VideoStreamWebm, "the type hint Resource works");

        // Resource cache.
        var first = GD.Load<VideoStreamWebm>(UserPath);
        var second = GD.Load<VideoStreamWebm>(UserPath);
        Expect(first != null && first == second, "a normal load is cached");
        Expect(ResourceLoader.HasCached(UserPath), "the path is in the resource cache");

        // Threaded loading.
        Error err = ResourceLoader.LoadThreadedRequest(ThreadedPath, "VideoStream");
        Expect(err == Error.Ok, "load_threaded_request() returns OK");
        var threaded = ResourceLoader.LoadThreadedGet(ThreadedPath) as VideoStreamWebm;
        Expect(threaded != null, "load_threaded_get() returns a VideoStreamWebm");

        // Invalid file.
        Write(BrokenPath, "This is not a WebM file."u8.ToArray());
        var broken = ResourceLoader.Load(BrokenPath, "", ResourceLoader.CacheMode.Ignore);
        Expect(broken == null, "an invalid .webm file does not load");

        // No saver: the engine has no WebM encoder.
        Expect(ResourceSaver.Save(stream, UserDir + "/copy.webm") != Error.Ok, "ResourceSaver.save() to .webm fails (no encoder)");

        // Playback from user://.
        var player = new VideoStreamPlayer { Size = new Vector2(64, 64), Stream = stream };
        AddChild(player);
        player.Play();
        Expect(player.IsPlaying(), "VideoStreamPlayer plays the user:// stream");
        Expect(Math.Abs(player.GetStreamLength() - 1.0) < 0.2, $"stream length is about 1 s (got {player.GetStreamLength()})");
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Expect(player.StreamPosition > 0.0, $"stream position advances ({player.StreamPosition})");
        Expect(player.GetVideoTexture() != null, "a video texture exists");
        player.Stop();
        player.QueueFree();

        foreach (string fileName in new[] { "testsrc.webm", "threaded.webm", "broken.webm" })
        {
            DirAccess.RemoveAbsolute(UserDir + "/" + fileName);
        }
    }
}
