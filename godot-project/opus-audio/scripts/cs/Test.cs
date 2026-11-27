using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.OpusAudio;

/// <summary>Full API test of Opus Audio (AudioStreamOpus, AudioStreamPlaybackOpus). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Opus = "res://opus-audio/resources/sine_440.opus";
    private const string Dir = "user://opus-audio-test";
    private const double Length = 1.0;
    private const double LengthTolerance = 0.1;
    private const int DefaultBarBeats = 4;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        Expect(ClassDB.ClassExists("AudioStreamOpus"), "AudioStreamOpus is registered");
        Expect(ClassDB.ClassExists("AudioStreamPlaybackOpus"), "AudioStreamPlaybackOpus is registered");
        TestImported();
        TestProperties();
        TestLoadFromFile();
        TestLoadFromBuffer();
        TestSaveAsTres();
        await TestPlayback();
    }

    private void TestImported()
    {
        var stream = GD.Load<AudioStreamOpus>(Opus);
        Expect(stream != null, "the imported .opus loads as AudioStreamOpus");
        Expect(Math.Abs(stream.GetLength() - Length) < LengthTolerance, "length is about 1 s");
        Expect(stream.Data.Length > 0, "the stream keeps the compressed data");
        Godot.Collections.Dictionary tags = stream.Tags;
        Expect(tags != null, "Tags is a Dictionary");
    }

    private void TestProperties()
    {
        var stream = new AudioStreamOpus();
        Expect(!stream.Loop, "Loop is off by default");
        ExpectEqual(stream.LoopOffset, 0.0, "LoopOffset defaults to 0");
        ExpectEqual(stream.Bpm, 0.0, "Bpm defaults to 0");
        ExpectEqual(stream.BeatCount, 0, "BeatCount defaults to 0");
        ExpectEqual(stream.BarBeats, DefaultBarBeats, "BarBeats defaults to 4");
        stream.Loop = true;
        stream.LoopOffset = 0.25;
        stream.Bpm = 120.0;
        stream.BeatCount = 2;
        stream.BarBeats = 3;
        Expect(stream.Loop, "Loop is settable");
        ExpectEqual(stream.LoopOffset, 0.25, "LoopOffset is settable");
        ExpectEqual(stream.Bpm, 120.0, "Bpm is settable");
        ExpectEqual(stream.BeatCount, 2, "BeatCount is settable");
        ExpectEqual(stream.BarBeats, 3, "BarBeats is settable");
    }

    private void TestLoadFromFile()
    {
        string path = Dir + "/voice.opus";
        DirAccess.CopyAbsolute(Opus, path);
        AudioStreamOpus stream = AudioStreamOpus.LoadFromFile(path);
        Expect(Math.Abs(stream.GetLength() - Length) < LengthTolerance, "LoadFromFile returns a stream of about 1 s");
        Expect(AudioStreamOpus.LoadFromFile(Dir + "/missing.opus") == null, "LoadFromFile of a missing file gives null");
    }

    private void TestLoadFromBuffer()
    {
        byte[] bytes = FileAccess.GetFileAsBytes(Opus);
        AudioStreamOpus stream = AudioStreamOpus.LoadFromBuffer(bytes);
        Expect(Math.Abs(stream.GetLength() - Length) < LengthTolerance, "LoadFromBuffer returns a stream of about 1 s");
        Expect(stream.Data.AsSpan().SequenceEqual(bytes), "LoadFromBuffer keeps the data");
        AudioStreamOpus garbage = AudioStreamOpus.LoadFromBuffer([1, 2, 3, 4]);
        Expect(garbage.Data.Length == 0, "LoadFromBuffer of invalid bytes gives a stream with empty data");
    }

    private void TestSaveAsTres()
    {
        var stream = GD.Load<AudioStreamOpus>(Opus);
        ExpectEqual(ResourceSaver.Save(stream, Dir + "/copy.tres"), Error.Ok, "the stream saves as .tres");
        var copy = ResourceLoader.Load(Dir + "/copy.tres", "", ResourceLoader.CacheMode.Ignore) as AudioStreamOpus;
        Expect(copy.Data.AsSpan().SequenceEqual(stream.Data), "the saved .tres loads again with the same data");
    }

    private async Task TestPlayback()
    {
        var stream = GD.Load<AudioStreamOpus>(Opus);
        AudioStreamPlayback playback = stream.InstantiatePlayback();
        Expect(playback.GetClass() == "AudioStreamPlaybackOpus", "InstantiatePlayback returns an AudioStreamPlaybackOpus");
        playback.Start(0.0);
        Expect(playback.IsPlaying(), "the playback is playing after Start");
        var player = new AudioStreamPlayer { Stream = stream };
        AddChild(player);
        player.Play();
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Expect(player.Playing, "AudioStreamPlayer is playing");
        Expect(player.GetPlaybackPosition() > 0.0, "the playback position advances");
        player.QueueFree();
    }
}
