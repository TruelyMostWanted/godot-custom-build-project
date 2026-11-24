using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue199FeatureOpusRuntimeLoader;

/// <summary>C# checks of 199-feature-opus-runtime-loader. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue199FeatureOpusRuntimeLoaderCheck : FeatureCheckCs
{
    private const string OpusPath = "res://199-feature-opus-runtime-loader/resources/sine_440.opus";
    private const string UserDir = "user://199-feature-opus-runtime-loader-cs";
    private const string UserPath = UserDir + "/sine_440.opus";
    private const string ThreadedPath = UserDir + "/threaded.opus";
    private const string BrokenPath = UserDir + "/broken.opus";

    protected override string CheckName => "199-feature-opus-runtime-loader";

    private static void Write(string path, byte[] bytes)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreBuffer(bytes);
    }

    protected override async Task RunChecks()
    {
        // Imported res:// file: still loaded through the importer.
        var imported = GD.Load<AudioStreamOpus>(OpusPath);
        Expect(imported != null, "the imported res:// .opus loads as AudioStreamOpus");

        // Copy the file into user:// at runtime (no .import file there).
        byte[] bytes = FileAccess.GetFileAsBytes(OpusPath);
        Expect(bytes.Length > 0, "the source file can be read");
        DirAccess.MakeDirRecursiveAbsolute(UserDir);
        Write(UserPath, bytes);
        Write(ThreadedPath, bytes);

        Expect(ResourceLoader.Exists(UserPath), "ResourceLoader.exists() finds the user:// .opus");
        Expect(ResourceLoader.Exists(UserPath, "AudioStream"), "ResourceLoader.exists() with type hint AudioStream");
        Expect(!ResourceLoader.Exists(UserDir + "/missing.opus"), "ResourceLoader.exists() is false for a missing file");

        var stream = ResourceLoader.Load(UserPath, "", ResourceLoader.CacheMode.Ignore) as AudioStreamOpus;
        Expect(stream != null, "ResourceLoader.load() of user:// returns an AudioStreamOpus");
        if (stream == null)
        {
            return;
        }
        Expect(Math.Abs(stream.GetLength() - 1.0) < 0.1, $"the length is about 1 s (got {stream.GetLength()})");
        Expect(stream.Data.SequenceEqual(bytes), "the stream keeps the compressed data");
        Expect(!stream.Loop && stream.Bpm == 0.0 && stream.BarBeats == 4, "the importer defaults are used");

        // Resource cache.
        var first = GD.Load<AudioStreamOpus>(UserPath);
        var second = GD.Load<AudioStreamOpus>(UserPath);
        Expect(first != null && first == second, "a normal load is cached");
        Expect(ResourceLoader.HasCached(UserPath), "the path is in the resource cache");

        // Threaded loading.
        Error err = ResourceLoader.LoadThreadedRequest(ThreadedPath, "AudioStream");
        Expect(err == Error.Ok, "load_threaded_request() returns OK");
        var threaded = ResourceLoader.LoadThreadedGet(ThreadedPath) as AudioStreamOpus;
        Expect(threaded != null, "load_threaded_get() returns an AudioStreamOpus");

        // Invalid file.
        Write(BrokenPath, "This is not an Ogg Opus file."u8.ToArray());
        var broken = ResourceLoader.Load(BrokenPath, "", ResourceLoader.CacheMode.Ignore);
        Expect(broken == null, "an invalid .opus file does not load");

        // No saver: the engine has no Opus encoder.
        Expect(ResourceSaver.Save(stream, UserDir + "/copy.opus") != Error.Ok, "ResourceSaver.save() to .opus fails (no encoder)");
        Expect(ResourceSaver.Save(stream, UserDir + "/copy.tres") == Error.Ok, "ResourceSaver.save() to .tres works");
        var copy = ResourceLoader.Load(UserDir + "/copy.tres", "", ResourceLoader.CacheMode.Ignore) as AudioStreamOpus;
        Expect(copy != null && copy.Data.SequenceEqual(bytes), "the saved .tres loads again with the same data");

        // Playback.
        var player = new AudioStreamPlayer { Stream = stream };
        AddChild(player);
        player.Play();
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Expect(player.Playing, "AudioStreamPlayer plays the runtime-loaded stream");
        Expect(player.GetPlaybackPosition() > 0.0, $"the playback position advances ({player.GetPlaybackPosition()})");
        player.Stop();
        player.QueueFree();

        foreach (string fileName in new[] { "sine_440.opus", "threaded.opus", "broken.opus", "copy.tres" })
        {
            DirAccess.RemoveAbsolute(UserDir + "/" + fileName);
        }
    }
}
