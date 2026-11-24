using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue195FeatureOpusAudio;

/// <summary>C# checks of 195-feature-opus-audio. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue195FeatureOpusAudioCheck : FeatureCheckCs
{
    private const string OpusPath = "res://195-feature-opus-audio/resources/sine_440.opus";
    private const string CopyPath = "user://195_sine_440.opus";

    protected override string CheckName => "195-feature-opus-audio";

    protected override async Task RunChecks()
    {
        Expect(ClassDB.ClassExists("AudioStreamOpus"), "AudioStreamOpus is registered");
        Expect(ClassDB.ClassExists("AudioStreamPlaybackOpus"), "AudioStreamPlaybackOpus is registered");

        // Imported resource.
        var imported = GD.Load<AudioStreamOpus>(OpusPath);
        Expect(imported != null, "the imported .opus loads as AudioStreamOpus");
        if (imported == null)
        {
            return;
        }
        Expect(Math.Abs(imported.GetLength() - 1.0) < 0.1, $"imported length is about 1 s (got {imported.GetLength()})");
        Expect(imported.Data.Length > 0, "imported stream has data");
        Expect(!imported.Loop, "loop is off by default");
        imported.Loop = true;
        imported.LoopOffset = 0.25;
        imported.Bpm = 120.0;
        imported.BeatCount = 2;
        imported.BarBeats = 4;
        Expect(imported.Loop && Mathf.IsEqualApprox((float)imported.LoopOffset, 0.25f), "loop and loop_offset are settable");
        Expect(Mathf.IsEqualApprox((float)imported.Bpm, 120.0f) && imported.BeatCount == 2 && imported.BarBeats == 4, "bpm, beat_count, bar_beats are settable");
        Expect(imported.Tags != null, "tags is a Dictionary");

        // Runtime load from file.
        byte[] bytes = FileAccess.GetFileAsBytes(OpusPath);
        Expect(bytes.Length > 0, "the source file can be read");
        using (var file = FileAccess.Open(CopyPath, FileAccess.ModeFlags.Write))
        {
            file.StoreBuffer(bytes);
        }
        AudioStreamOpus fromFile = AudioStreamOpus.LoadFromFile(CopyPath);
        Expect(fromFile != null, "load_from_file returns a stream");
        if (fromFile != null)
        {
            Expect(Math.Abs(fromFile.GetLength() - 1.0) < 0.1, "load_from_file length is about 1 s");
        }
        Expect(AudioStreamOpus.LoadFromFile("user://does_not_exist.opus") == null, "load_from_file of a missing file gives null");

        // Runtime load from buffer.
        AudioStreamOpus fromBuffer = AudioStreamOpus.LoadFromBuffer(bytes);
        Expect(fromBuffer != null, "load_from_buffer returns a stream");
        if (fromBuffer != null)
        {
            Expect(Math.Abs(fromBuffer.GetLength() - 1.0) < 0.1, "load_from_buffer length is about 1 s");
            Expect(fromBuffer.Data.SequenceEqual(bytes), "load_from_buffer keeps the data");
        }
        // Upstream behavior: garbage gives null or an empty stream (length 0), never a playable one.
        AudioStreamOpus garbage = AudioStreamOpus.LoadFromBuffer([1, 2, 3, 4]);
        Expect(garbage == null || garbage.GetLength() == 0.0, "load_from_buffer of garbage gives no playable stream");

        // Playback.
        AudioStreamOpus stream = fromBuffer ?? imported;
        AudioStreamPlayback playback = stream.InstantiatePlayback();
        Expect(playback != null, "instantiate_playback returns a playback");
        Expect(playback is AudioStreamPlaybackOpus, "the playback is an AudioStreamPlaybackOpus");
        playback.Start(0.0);
        Expect(playback.IsPlaying(), "the playback is playing after start()");

        var player = new AudioStreamPlayer { Stream = stream };
        AddChild(player);
        player.Play();
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Expect(player.Playing, "AudioStreamPlayer is playing");
        Expect(player.GetPlaybackPosition() > 0.0, $"the playback position advances ({player.GetPlaybackPosition()})");
        player.Stop();
        player.QueueFree();
        DirAccess.RemoveAbsolute(CopyPath);
    }
}
