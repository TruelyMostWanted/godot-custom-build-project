using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue103FeatureOggVorbisLoader;

/// <summary>C# checks of 103-feature-ogg-vorbis-loader. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue103FeatureOggVorbisLoaderCheck : FeatureCheckCs
{
    private const string Dir = "user://103-feature-ogg-vorbis-loader-cs";
    private const string Res = "res://103-feature-ogg-vorbis-loader/resources";

    protected override string CheckName => "103-feature-ogg-vorbis-loader";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        string path = Dir.PathJoin("tone.ogg");
        // kept_tone.ogg uses import mode "Keep File": its bytes are a raw Ogg Vorbis file (0.5 s, 440 Hz, 22050 Hz mono).
        byte[] bytes = FileAccess.GetFileAsBytes(Res.PathJoin("kept_tone.ogg"));
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreBuffer(bytes);
        }

        Expect(ResourceLoader.Exists(path), "ResourceLoader.Exists() finds an .ogg in user://");
        Expect(ResourceLoader.Exists(path, "AudioStream"), "Exists() with type hint AudioStream");
        Expect(ResourceLoader.GetRecognizedExtensionsForType("AudioStreamOggVorbis").Contains("ogg"), "ogg is a recognized extension");

        var stream = ResourceLoader.Load<AudioStreamOggVorbis>(path, "AudioStreamOggVorbis", ResourceLoader.CacheMode.Ignore);
        if (Expect(stream != null, "ResourceLoader.Load() returns an AudioStreamOggVorbis from user://"))
        {
            Expect(Math.Abs(stream.GetLength() - 0.5) < 0.05, $"length is about 0.5 s (got {stream.GetLength()})");
            ExpectEqual(stream.Loop, false, "default import options: no loop");
            Expect(stream.InstantiatePlayback() != null, "the stream can be played");

            // No Ogg Vorbis encoder in the engine.
            Expect(!ResourceSaver.GetRecognizedExtensions(stream).Contains("ogg"), "ResourceSaver offers no .ogg");
            Expect(ResourceSaver.Save(stream, Dir.PathJoin("copy.ogg")) != Error.Ok, "ResourceSaver.Save() cannot write .ogg");
        }

        Resource first = GD.Load(path);
        Expect(first != null && first == GD.Load(path), "Load() caches the stream");

        // Importer precedence: imported_tone.ogg.import sets loop=true, the runtime loader never loops.
        var imported = GD.Load<AudioStreamOggVorbis>(Res.PathJoin("imported_tone.ogg"));
        if (Expect(imported != null, "imported res:// Ogg Vorbis loads"))
        {
            ExpectEqual(imported.Loop, true, "imported res:// file comes from the importer (loop=true)");
        }
        var kept = GD.Load<AudioStreamOggVorbis>(Res.PathJoin("kept_tone.ogg"));
        if (Expect(kept != null, "Ogg Vorbis with import mode Keep File loads"))
        {
            ExpectEqual(kept.Loop, false, "Keep File comes from the runtime loader (loop=false)");
        }

        // Threaded loading.
        string threaded = Dir.PathJoin("threaded.ogg");
        DirAccess.CopyAbsolute(path, threaded);
        ExpectEqual(ResourceLoader.LoadThreadedRequest(threaded, "AudioStreamOggVorbis"), Error.Ok, "LoadThreadedRequest() accepts a user:// Ogg Vorbis file");
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(threaded);
        int frames = 0;
        while (status == ResourceLoader.ThreadLoadStatus.InProgress && frames < 600)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            frames++;
            status = ResourceLoader.LoadThreadedGetStatus(threaded);
        }
        ExpectEqual(status, ResourceLoader.ThreadLoadStatus.Loaded, "threaded load finishes");
        Expect(ResourceLoader.LoadThreadedGet(threaded) is AudioStreamOggVorbis, "LoadThreadedGet() returns the AudioStreamOggVorbis");
    }
}
