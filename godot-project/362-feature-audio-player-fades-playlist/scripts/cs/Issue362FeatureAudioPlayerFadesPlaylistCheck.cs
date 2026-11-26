using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue362FeatureAudioPlayerFadesPlaylist;

/// <summary>C# checks of 362-feature-audio-player-fades-playlist. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>The checks run headless with the dummy audio driver: they check signals, indices and fade gains, not sound.</remarks>
public partial class Issue362FeatureAudioPlayerFadesPlaylistCheck : FeatureCheckCs
{
    private int _fadeFinished;
    private readonly List<AudioStream> _streamChanged = [];
    private readonly List<long> _tracks = [];
    private readonly List<AudioStream> _trackStreams = [];
    private int _positions;

    protected override string CheckName => "362-feature-audio-player-fades-playlist";

    /// <summary>A silent 16-bit mono WAV of the given length.</summary>
    private static AudioStreamWav MakeWav(double seconds)
    {
        var data = new byte[(int)(seconds * 44100.0) * 2];
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44100,
            Stereo = false,
            Data = data,
        };
    }

    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task NextFrames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static bool Approx(double a, double b, double tolerance = 0.001)
        => Mathf.Abs(a - b) <= tolerance;

    private void OnFadeFinished() => _fadeFinished++;

    private void OnStreamChanged(AudioStream stream) => _streamChanged.Add(stream);

    private void OnTrackChanged(long index, AudioStream stream)
    {
        _tracks.Add(index);
        _trackStreams.Add(stream);
    }

    private void OnPositionChanged(double position) => _positions++;

    protected override async Task RunChecks()
    {
        CheckListApi();
        CheckTimeHelpers();
        await CheckFadeInOut();
        await CheckCrossfade();
        await CheckPlaylistOnPlayer();
        await CheckPositionSignal();
        await CheckFade2dAnd3d();
    }

    private void CheckListApi()
    {
        var a = MakeWav(1.0);
        a.ResourceName = "alpha";
        var b = MakeWav(1.0);
        var c = MakeWav(1.0);
        var playlist = new AudioStreamPlaylist();
        Expect(playlist.Streams.Count == 0, "a new playlist has no streams");
        playlist.AddStream(a);
        playlist.AddStream(c);
        playlist.InsertStream(1, b);
        Expect(playlist.Streams.Count == 3, "add_stream/insert_stream give three streams");
        Expect(playlist.Streams[0] == a && playlist.Streams[1] == b && playlist.Streams[2] == c, "insert_stream inserts at the index");
        ExpectEqual((int)playlist.FindStream(c), 2, "find_stream finds the stream");
        ExpectEqual((int)playlist.FindStream(MakeWav(0.1)), -1, "find_stream returns -1 for a missing stream");
        playlist.MoveStream(2, 0);
        Expect(playlist.Streams[0] == c && playlist.Streams[1] == a && playlist.Streams[2] == b, "move_stream moves a stream");
        playlist.RemoveStream(0);
        Expect(playlist.Streams.Count == 2 && playlist.Streams[0] == a, "remove_stream removes the stream");
        ExpectEqual(playlist.GetStreamName(0), "alpha", "get_stream_name returns the resource name");
        ExpectEqual(playlist.GetStreamName(1), "", "get_stream_name is empty without name and path");

        playlist.Streams = new Godot.Collections.Array<AudioStream> { b, c };
        Expect(playlist.Streams.Count == 2 && playlist.Streams[0] == b, "the streams setter replaces the list");

        // Old files: stream_count + stream_N.
        var old = new AudioStreamPlaylist();
        old.Set("stream_count", 2);
        old.Set("stream_0", a);
        old.Set("stream_1", b);
        Expect(old.Streams.Count == 2 && old.Streams[0] == a && old.Streams[1] == b, "old stream_count/stream_N properties load into streams");
    }

    private void CheckTimeHelpers()
    {
        var playlist = new AudioStreamPlaylist();
        playlist.AddStream(MakeWav(1.0));
        playlist.AddStream(MakeWav(2.0));
        playlist.AddStream(MakeWav(0.5));
        playlist.Loop = false;
        Expect(Approx(playlist.GetStreamLength(1), 2.0), "get_stream_length of track 1 is 2.0");
        Expect(Approx(playlist.GetStreamStartTime(0), 0.0), "start time of track 0 is 0");
        Expect(Approx(playlist.GetStreamStartTime(1), 1.0), "start time of track 1 is 1");
        Expect(Approx(playlist.GetStreamStartTime(2), 3.0), "start time of track 2 is 3");
        Expect(Approx(playlist.GetLength(), 3.5), "get_length is the sum of the lengths");
        ExpectEqual((int)playlist.GetStreamIndexAt(0.0), 0, "index_at 0.0 is track 0");
        ExpectEqual((int)playlist.GetStreamIndexAt(0.99), 0, "index_at 0.99 is track 0");
        ExpectEqual((int)playlist.GetStreamIndexAt(1.0), 1, "index_at 1.0 is track 1");
        ExpectEqual((int)playlist.GetStreamIndexAt(2.9), 1, "index_at 2.9 is track 1");
        ExpectEqual((int)playlist.GetStreamIndexAt(3.2), 2, "index_at 3.2 is track 2");
        for (int i = 0; i < 3; i++)
        {
            ExpectEqual((int)playlist.GetStreamIndexAt(playlist.GetStreamStartTime(i) + 0.01), i, $"index_at(start time + 0.01) round trip for track {i}");
        }
        ExpectEqual((int)playlist.GetStreamIndexAt(3.6), -1, "index_at past the end is -1 without loop");
        ExpectEqual((int)playlist.GetStreamIndexAt(-0.5), -1, "index_at of a negative time is -1");
        playlist.Loop = true;
        ExpectEqual((int)playlist.GetStreamIndexAt(3.6), 0, "index_at wraps with loop (3.6 -> 0.1)");
        ExpectEqual((int)playlist.GetStreamIndexAt(4.7), 1, "index_at wraps with loop (4.7 -> 1.2)");
        playlist.Loop = false;
        playlist.FadeTime = 0.3f;
        Expect(Approx(playlist.GetStreamStartTime(2), 3.0), "fade_time does not change start times");
    }

    private async Task CheckFadeInOut()
    {
        var player = new AudioStreamPlayer { Stream = MakeWav(30.0) };
        AddChild(player);
        player.FadeFinished += OnFadeFinished;
        _fadeFinished = 0;
        Expect(!player.IsFading(), "not fading at the start");
        Expect(Approx(player.GetFadeGain(), 1.0), "gain is 1.0 at the start");
        player.FadeIn(0.4);
        Expect(player.Playing, "fade_in() starts playback");
        Expect(player.IsFading(), "is_fading() during fade_in");
        Expect((float)player.GetFadeGain() < 0.2f, $"gain starts near 0 (got {player.GetFadeGain()})");
        float last = player.GetFadeGain();
        bool monotonic = true;
        for (int i = 0; i < 10; i++)
        {
            await Wait(0.05);
            float g = player.GetFadeGain();
            if (g < last - 0.001f)
            {
                monotonic = false;
            }
            last = g;
        }
        Expect(monotonic, "the gain rises monotonically");
        await Wait(0.5);
        Expect(Approx(player.GetFadeGain(), 1.0), "gain is 1.0 after the fade");
        Expect(!player.IsFading(), "not fading after fade_in");
        Expect(_fadeFinished >= 1, "fade_finished is emitted after fade_in");
        Expect(Approx(player.VolumeDb, 0.0), "volume_db is untouched by fades");

        _fadeFinished = 0;
        player.FadeOut(0.3);
        Expect(player.IsFading(), "is_fading() during fade_out");
        await Wait(0.6);
        Expect(!player.Playing, "fade_out(stop_at_end) stops the player");
        Expect(_fadeFinished >= 1, "fade_finished is emitted after fade_out");

        player.Play();
        Expect(player.Playing, "play() starts playback");
        Expect(Approx(player.GetFadeGain(), 1.0), "play() resets the gain to 1.0");
        player.FadeOut(0.2, AudioMixerServer.FadeCurve.Linear, false);
        await Wait(0.5);
        Expect(player.Playing, "fade_out(stop_at_end = false) keeps playing");
        player.QueueFree();
    }

    private async Task CheckCrossfade()
    {
        var first = MakeWav(30.0);
        var second = MakeWav(30.0);
        var player = new AudioStreamPlayer { Stream = first };
        AddChild(player);
        player.Play();
        player.FadeFinished += OnFadeFinished;
        player.StreamChanged += OnStreamChanged;
        _fadeFinished = 0;
        _streamChanged.Clear();
        player.CrossfadeTo(second, 0.4);
        Expect(_streamChanged.Count == 1 && _streamChanged[0] == second, "crossfade_to emits stream_changed with the new stream");
        Expect(player.Stream == second, "crossfade_to swaps the stream");
        Expect(player.IsFading(), "is_fading() during the crossfade");
        Expect(player.Playing, "the player keeps playing during the crossfade");
        await Wait(0.8);
        Expect(!player.IsFading(), "not fading after the crossfade");
        Expect(_fadeFinished >= 1, "fade_finished is emitted after the crossfade");
        Expect(player.Playing, "the player plays the new stream after the crossfade");
        player.QueueFree();
    }

    private async Task CheckPlaylistOnPlayer()
    {
        var first = MakeWav(0.3);
        var second = MakeWav(0.3);
        var playlist = new AudioStreamPlaylist();
        playlist.AddStream(first);
        playlist.AddStream(second);
        playlist.Loop = false;
        var player = new AudioStreamPlayer { Stream = playlist };
        AddChild(player);
        ExpectEqual((int)player.GetCurrentTrackIndex(), -1, "no track index without playback");
        Expect(player.GetCurrentTrackStream() == null, "no track stream without playback");
        player.TrackChanged += OnTrackChanged;
        _tracks.Clear();
        _trackStreams.Clear();
        player.Play();
        await NextFrames(2);
        Expect(_tracks.Count >= 1 && _tracks[0] == 0 && _trackStreams[0] == first, "track_changed(0, first) arrives after play()");
        ExpectEqual((int)player.GetCurrentTrackIndex(), 0, "get_current_track_index is 0");
        Expect(player.GetCurrentTrackStream() == first, "get_current_track_stream is the first stream");
        float posA = player.GetPositionInPlaylist();
        await Wait(0.1);
        float posB = player.GetPositionInPlaylist();
        Expect(posB > posA, $"get_position_in_playlist increases ({posA} -> {posB})");
        for (int i = 0; i < 40 && !_tracks.Contains(1); i++) // At most 2 seconds.
        {
            await Wait(0.05);
        }
        Expect(_tracks.Contains(1), "track_changed(1, second) arrives later");
        if (_tracks.Contains(1))
        {
            Expect(_trackStreams[_tracks.IndexOf(1)] == second, "track_changed(1) carries the second stream");
        }
        ExpectEqual((int)player.GetCurrentTrackIndex(), 1, "get_current_track_index is 1");
        Expect(player.GetCurrentTrackStream() == second, "get_current_track_stream is the second stream");
        player.QueueFree();

        // Single streams: index 0 and the stream itself.
        var wav = MakeWav(5.0);
        var single = new AudioStreamPlayer { Stream = wav };
        AddChild(single);
        single.Play();
        await NextFrames(1);
        ExpectEqual((int)single.GetCurrentTrackIndex(), 0, "single stream: track index is 0");
        Expect(single.GetCurrentTrackStream() == wav, "single stream: track stream is the stream");
        single.QueueFree();
    }

    private async Task CheckPositionSignal()
    {
        var player = new AudioStreamPlayer { Stream = MakeWav(30.0) };
        AddChild(player);
        player.PositionChanged += OnPositionChanged;
        Expect(Approx(player.PositionSignalInterval, 0.0), "position_signal_interval is 0 by default");
        player.PositionSignalInterval = -1.0f;
        Expect(Approx(player.PositionSignalInterval, 0.0), "a negative interval clamps to 0");
        _positions = 0;
        player.Play();
        await NextFrames(6);
        ExpectEqual(_positions, 0, "position_changed is off by default");
        player.PositionSignalInterval = 0.1f;
        await Wait(0.5);
        Expect(_positions >= 1, $"position_changed is emitted with an interval ({_positions})");
        player.QueueFree();
    }

    private async Task CheckFade2dAnd3d()
    {
        var p2d = new AudioStreamPlayer2D { Stream = MakeWav(30.0) };
        AddChild(p2d);
        p2d.FadeFinished += OnFadeFinished;
        _fadeFinished = 0;
        p2d.FadeIn(0.2);
        Expect(p2d.Playing, "2D: fade_in() starts playback");
        Expect((float)p2d.GetFadeGain() < 0.3f, "2D: gain starts near 0");
        await Wait(0.5);
        Expect(Approx(p2d.GetFadeGain(), 1.0), "2D: gain is 1.0 after the fade");
        Expect(_fadeFinished >= 1, "2D: fade_finished is emitted");
        p2d.QueueFree();

        var p3d = new AudioStreamPlayer3D { Stream = MakeWav(30.0) };
        AddChild(p3d);
        p3d.FadeFinished += OnFadeFinished;
        _fadeFinished = 0;
        p3d.FadeIn(0.2);
        Expect(p3d.Playing, "3D: fade_in() starts playback");
        Expect((float)p3d.GetFadeGain() < 0.3f, "3D: gain starts near 0");
        await Wait(0.5);
        Expect(Approx(p3d.GetFadeGain(), 1.0), "3D: gain is 1.0 after the fade");
        Expect(_fadeFinished >= 1, "3D: fade_finished is emitted");
        p3d.QueueFree();
    }
}
