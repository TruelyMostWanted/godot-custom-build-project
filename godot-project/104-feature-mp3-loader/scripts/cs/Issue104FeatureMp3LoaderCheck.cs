using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue104FeatureMp3Loader;

/// <summary>C# checks of 104-feature-mp3-loader. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue104FeatureMp3LoaderCheck : FeatureCheckCs
{
    private const string Dir = "user://104-feature-mp3-loader-cs";
    private const string Res = "res://104-feature-mp3-loader/resources";

    protected override string CheckName => "104-feature-mp3-loader";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        string path = Dir.PathJoin("tone.mp3");
        // kept_tone.mp3 uses import mode "Keep File": its bytes are a raw MP3 file (0.5 s, 440 Hz, 22050 Hz mono).
        byte[] bytes = FileAccess.GetFileAsBytes(Res.PathJoin("kept_tone.mp3"));
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreBuffer(bytes);
        }

        Expect(ResourceLoader.Exists(path), "ResourceLoader.Exists() finds an .mp3 in user://");
        Expect(ResourceLoader.Exists(path, "AudioStream"), "Exists() with type hint AudioStream");
        Expect(ResourceLoader.GetRecognizedExtensionsForType("AudioStreamMP3").Contains("mp3"), "mp3 is a recognized extension");

        var stream = ResourceLoader.Load<AudioStreamMP3>(path, "AudioStreamMP3", ResourceLoader.CacheMode.Ignore);
        if (Expect(stream != null, "ResourceLoader.Load() returns an AudioStreamMP3 from user://"))
        {
            // The decoded length can include the encoder delay and padding.
            Expect(Math.Abs(stream.GetLength() - 0.5) < 0.1, $"length is about 0.5 s (got {stream.GetLength()})");
            ExpectEqual(stream.Loop, false, "default import options: no loop");
            Expect(stream.InstantiatePlayback() != null, "the stream can be played");

            // No MP3 encoder in the engine.
            Expect(!ResourceSaver.GetRecognizedExtensions(stream).Contains("mp3"), "ResourceSaver offers no .mp3");
            Expect(ResourceSaver.Save(stream, Dir.PathJoin("copy.mp3")) != Error.Ok, "ResourceSaver.Save() cannot write .mp3");
        }

        Resource first = GD.Load(path);
        Expect(first != null && first == GD.Load(path), "Load() caches the stream");

        // Importer precedence: imported_tone.mp3.import sets loop=true, the runtime loader never loops.
        var imported = GD.Load<AudioStreamMP3>(Res.PathJoin("imported_tone.mp3"));
        if (Expect(imported != null, "imported res:// MP3 loads"))
        {
            ExpectEqual(imported.Loop, true, "imported res:// file comes from the importer (loop=true)");
        }
        var kept = GD.Load<AudioStreamMP3>(Res.PathJoin("kept_tone.mp3"));
        if (Expect(kept != null, "MP3 with import mode Keep File loads"))
        {
            ExpectEqual(kept.Loop, false, "Keep File comes from the runtime loader (loop=false)");
        }

        // Threaded loading.
        string threaded = Dir.PathJoin("threaded.mp3");
        DirAccess.CopyAbsolute(path, threaded);
        ExpectEqual(ResourceLoader.LoadThreadedRequest(threaded, "AudioStreamMP3"), Error.Ok, "LoadThreadedRequest() accepts a user:// MP3 file");
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(threaded);
        int frames = 0;
        while (status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            frames++;
            status = ResourceLoader.LoadThreadedGetStatus(threaded);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Loaded, "threaded load finishes");
        Expect(ResourceLoader.LoadThreadedGet(threaded) is AudioStreamMP3, "LoadThreadedGet() returns the AudioStreamMP3");
    }
}
