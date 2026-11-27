using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AudioPlayerFadesAndPlaylists;

/// <summary>
/// Full API test of Audio Player Fades And Playlists: the fade, track and signal API of AudioStreamPlayer (2D, 3D),
/// AudioStreamPlaylist and AudioStreamPlaybackPlaylist. Same tests as scripts/gd/Test.gd.
/// Runs headless with the dummy audio driver: it checks signals, indices and fade gains, not sound.
/// </summary>
public partial class Test : FeatureTest
{
    private const string TitlePath = "res://audio-player-fades-and-playlists/resources/title.tres";
    // Lengths of the three tracks of the time helper playlist, and its start times.
    private static readonly double[] Lengths = [1.0, 2.0, 0.5];
    private static readonly double[] StartTimes = [0.0, 1.0, 3.0];
    private const double TotalLength = 3.5;

    private int _fadeFinished;
    private readonly List<AudioStream> _streamChanged = [];
    private readonly List<long> _tracks = [];
    private readonly List<AudioStream> _trackStreams = [];
    private int _positions;

    protected override async Task RunTests()
    {
        TestPlaylistList();
        TestStreamNames();
        TestTimeHelpers();
        await TestFadeInOut();
        await TestCrossfade();
        await TestPlaylistOnPlayer();
        await TestSeekInPlaylist();
        await TestPositionSignal();
        await TestFade2D3D();
    }

    private void TestPlaylistList()
    {
        AudioStreamWav a = Silence(1.0);
        AudioStreamWav b = Silence(1.0);
        AudioStreamWav c = Silence(1.0);
        var playlist = new AudioStreamPlaylist();
        Expect(playlist.Streams.Count == 0, "a new playlist has no streams");
        playlist.AddStream(a);
        playlist.AddStream(c);
        playlist.InsertStream(1, b);
        Expect(playlist.Streams.SequenceEqual([a, b, c]), "AddStream appends, InsertStream inserts at the index");
        ExpectEqual(playlist.StreamCount, 3, "StreamCount is the size of Streams");
        ExpectEqual(playlist.FindStream(c), 2, "FindStream finds the stream");
        ExpectEqual(playlist.FindStream(Silence(0.1)), -1, "FindStream returns -1 for a missing stream");
        playlist.MoveStream(2, 0);
        Expect(playlist.Streams.SequenceEqual([c, a, b]), "MoveStream moves a stream");
        playlist.RemoveStream(0);
        Expect(playlist.Streams.SequenceEqual([a, b]), "RemoveStream removes the stream");
        playlist.SetListStream(1, c);
        Expect(playlist.GetListStream(1) == c, "SetListStream and GetListStream replace and read one entry");
        playlist.Streams = [b, c];
        Expect(playlist.Streams.SequenceEqual([b, c]), "the Streams setter replaces the list");
        for (int i = 0; i < AudioStreamPlaylist.MaxStreams; i++)
        {
            playlist.AddStream(a);
        }
        ExpectEqual(playlist.Streams.Count, (int)AudioStreamPlaylist.MaxStreams, "the list holds at most MaxStreams streams");
        var old = new AudioStreamPlaylist();
        old.Set("stream_count", 2);
        old.Set("stream_0", a);
        old.Set("stream_1", b);
        Expect(old.Streams.SequenceEqual([a, b]), "the old stream_count and stream_N properties load into Streams");
    }

    private void TestStreamNames()
    {
        AudioStreamWav named = Silence(1.0);
        named.ResourceName = "alpha";
        var playlist = new AudioStreamPlaylist();
        playlist.AddStream(named);
        playlist.AddStream(GD.Load<AudioStream>(TitlePath));
        playlist.AddStream(Silence(1.0));
        ExpectEqual(playlist.GetStreamName(0), "alpha", "GetStreamName returns the resource name");
        ExpectEqual(playlist.GetStreamName(1), "title", "GetStreamName falls back to the file name");
        ExpectEqual(playlist.GetStreamName(2), "", "GetStreamName is empty without name and path");
    }

    private void TestTimeHelpers()
    {
        var playlist = new AudioStreamPlaylist();
        foreach (double length in Lengths)
        {
            playlist.AddStream(Silence(length));
        }
        playlist.Loop = false;
        for (int i = 0; i < Lengths.Length; i++)
        {
            ExpectEqual(playlist.GetStreamLength(i), Lengths[i], $"GetStreamLength of track {i}");
            ExpectEqual(playlist.GetStreamStartTime(i), StartTimes[i], $"GetStreamStartTime of track {i}");
            ExpectEqual(playlist.GetStreamIndexAt(StartTimes[i] + 0.01), i, $"GetStreamIndexAt just after the start of track {i}");
        }
        ExpectEqual(playlist.GetLength(), TotalLength, "GetLength is the sum of the lengths");
        ExpectEqual(playlist.GetStreamIndexAt(0.99), 0, "GetStreamIndexAt just before a track change is the earlier track");
        ExpectEqual(playlist.GetStreamIndexAt(3.6), -1, "GetStreamIndexAt past the end is -1 without loop");
        ExpectEqual(playlist.GetStreamIndexAt(-0.5), -1, "GetStreamIndexAt of a negative time is -1");
        playlist.Loop = true;
        ExpectEqual(playlist.GetStreamIndexAt(4.7), 1, "GetStreamIndexAt wraps with loop (4.7 -> 1.2)");
        playlist.FadeTime = 0.3f;
        ExpectEqual(playlist.GetStreamStartTime(2), 3.0, "FadeTime does not change the start times");
    }

    private async Task TestFadeInOut()
    {
        AudioStreamPlayer player = AddPlayer();
        Expect(!player.IsFading(), "IsFading is false at the start");
        ExpectEqual(player.GetFadeGain(), 1.0f, "GetFadeGain is 1 at the start");
        player.FadeIn(0.4f);
        Expect(player.IsPlaying(), "FadeIn starts playback");
        Expect(player.IsFading(), "IsFading is true during FadeIn");
        Expect(player.GetFadeGain() < 0.2f, "FadeIn starts near silence");
        float last = player.GetFadeGain();
        bool rising = true;
        for (int i = 0; i < 10; i++)
        {
            await Wait(0.05);
            rising = rising && player.GetFadeGain() >= last - 0.001f;
            last = player.GetFadeGain();
        }
        Expect(rising, "FadeIn raises the gain step by step");
        await Wait(0.5);
        ExpectEqual(player.GetFadeGain(), 1.0f, "FadeIn ends at gain 1");
        ExpectEqual(_fadeFinished, 1, "FadeFinished is emitted after FadeIn");
        ExpectEqual(player.VolumeDb, 0.0f, "fades leave VolumeDb alone");
        player.FadeOut(0.3f);
        Expect(player.IsFading(), "IsFading is true during FadeOut");
        await Wait(0.6);
        Expect(!player.IsPlaying(), "FadeOut stops the player at the end");
        ExpectEqual(_fadeFinished, 2, "FadeFinished is emitted after FadeOut");
        player.Play();
        ExpectEqual(player.GetFadeGain(), 1.0f, "Play resets the gain to 1");
        player.FadeOut(0.2f, AudioMixerServer.FadeCurve.Linear, false);
        await Wait(0.5);
        Expect(player.IsPlaying(), "FadeOut with stopAtEnd = false keeps playing");
        player.QueueFree();
    }

    private async Task TestCrossfade()
    {
        AudioStreamPlayer player = AddPlayer();
        AudioStreamWav second = Silence(30.0);
        player.Play();
        _fadeFinished = 0;
        player.CrossfadeTo(second, 0.4f);
        Expect(_streamChanged.SequenceEqual([second]), "CrossfadeTo emits StreamChanged with the new stream");
        Expect(player.Stream == second, "CrossfadeTo sets the stream");
        Expect(player.IsFading(), "IsFading is true during the crossfade");
        await Wait(0.8);
        Expect(!player.IsFading(), "the crossfade ends");
        ExpectEqual(_fadeFinished, 1, "FadeFinished is emitted after the crossfade");
        Expect(player.IsPlaying(), "the player plays the new stream after the crossfade");
        player.QueueFree();
    }

    private async Task TestPlaylistOnPlayer()
    {
        AudioStreamWav first = Silence(0.3);
        AudioStreamWav second = Silence(0.3);
        var playlist = new AudioStreamPlaylist();
        playlist.AddStream(first);
        playlist.AddStream(second);
        playlist.Loop = false;
        AudioStreamPlayer player = AddPlayer();
        player.Stream = playlist;
        ExpectEqual(player.GetCurrentTrackIndex(), -1, "GetCurrentTrackIndex is -1 without playback");
        Expect(player.GetCurrentTrackStream() == null, "GetCurrentTrackStream is null without playback");
        player.Play();
        await Frames(2);
        Expect(_tracks.SequenceEqual([0L]), "TrackChanged(0) arrives after Play");
        Expect(_trackStreams.SequenceEqual([first]), "TrackChanged carries the stream of the track");
        ExpectEqual(player.GetCurrentTrackIndex(), 0, "GetCurrentTrackIndex is the playing track");
        Expect(player.GetCurrentTrackStream() == first, "GetCurrentTrackStream is the playing stream");
        var playback = (AudioStreamPlaybackPlaylist)player.GetStreamPlayback();
        ExpectEqual(playback.GetCurrentIndex(), 0, "AudioStreamPlaybackPlaylist.GetCurrentIndex is the playing track");
        Expect(playback.GetCurrentStream() == first, "AudioStreamPlaybackPlaylist.GetCurrentStream is the playing stream");
        await WaitForTrack(1);
        Expect(_tracks.SequenceEqual([0L, 1L]), "TrackChanged(1) arrives when the second track starts");
        Expect(player.GetPositionInPlaylist() > 0.3f, "GetPositionInPlaylist counts from the start of the playlist");
        Expect(player.GetTrackPosition() < 0.3f, "GetTrackPosition counts from the start of the track");
        ExpectEqual(playback.GetPositionInPlaylist(), (double)player.GetPositionInPlaylist(), "the playback reports the same playlist position");
        ExpectEqual(playback.GetPositionInStream(), (double)player.GetTrackPosition(), "the playback reports the same track position");
        player.QueueFree();
        AudioStreamPlayer single = AddPlayer();
        single.Play();
        await Frames(1);
        ExpectEqual(single.GetCurrentTrackIndex(), 0, "a single stream is track 0");
        Expect(single.GetCurrentTrackStream() == single.Stream, "a single stream is its own track stream");
        single.QueueFree();
    }

    private async Task TestSeekInPlaylist()
    {
        var playlist = new AudioStreamPlaylist();
        foreach (double length in Lengths)
        {
            playlist.AddStream(Silence(length));
        }
        AudioStreamPlayer player = AddPlayer();
        player.Stream = playlist;
        player.Play();
        player.Seek((float)playlist.GetStreamStartTime(2) + 0.1f);
        await Frames(2);
        ExpectEqual(player.GetCurrentTrackIndex(), 2, "Seek across the playlist starts the track at that time");
        player.QueueFree();
    }

    private async Task TestPositionSignal()
    {
        AudioStreamPlayer player = AddPlayer();
        ExpectEqual(player.PositionSignalInterval, 0.0f, "PositionSignalInterval is 0 by default");
        player.PositionSignalInterval = -1.0f;
        ExpectEqual(player.PositionSignalInterval, 0.0f, "a negative PositionSignalInterval is clamped to 0");
        player.Play();
        await Frames(6);
        ExpectEqual(_positions, 0, "PositionChanged is off by default");
        player.PositionSignalInterval = 0.1f;
        await Wait(0.5);
        Expect(_positions >= 1, "PositionChanged is emitted with an interval");
        player.QueueFree();
    }

    private async Task TestFade2D3D()
    {
        var player2D = new AudioStreamPlayer2D { Stream = Silence(30.0) };
        AddChild(player2D);
        player2D.FadeFinished += OnFadeFinished;
        _fadeFinished = 0;
        player2D.FadeIn(0.2f);
        Expect(player2D.IsPlaying(), "AudioStreamPlayer2D: FadeIn starts playback");
        await Wait(0.5);
        ExpectEqual(player2D.GetFadeGain(), 1.0f, "AudioStreamPlayer2D: FadeIn ends at gain 1");
        ExpectEqual(_fadeFinished, 1, "AudioStreamPlayer2D: FadeFinished is emitted");
        player2D.QueueFree();
        var player3D = new AudioStreamPlayer3D { Stream = Silence(30.0) };
        AddChild(player3D);
        player3D.FadeFinished += OnFadeFinished;
        _fadeFinished = 0;
        player3D.FadeIn(0.2f);
        Expect(player3D.IsPlaying(), "AudioStreamPlayer3D: FadeIn starts playback");
        await Wait(0.5);
        ExpectEqual(player3D.GetFadeGain(), 1.0f, "AudioStreamPlayer3D: FadeIn ends at gain 1");
        ExpectEqual(_fadeFinished, 1, "AudioStreamPlayer3D: FadeFinished is emitted");
        player3D.QueueFree();
    }

    /// <summary>Adds a player with 30 seconds of silence to the tree and connects its signals.</summary>
    private AudioStreamPlayer AddPlayer()
    {
        var player = new AudioStreamPlayer { Stream = Silence(30.0) };
        AddChild(player);
        player.FadeFinished += OnFadeFinished;
        player.StreamChanged += OnStreamChanged;
        player.PositionChanged += OnPositionChanged;
        player.TrackChanged += OnTrackChanged;
        return player;
    }

    private static AudioStreamWav Silence(double seconds)
    {
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44100,
            Data = new byte[(int)(seconds * 44100.0) * 2],
        };
    }

    private async Task Wait(double seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task WaitForTrack(long index)
    {
        for (int i = 0; i < 40 && !_tracks.Contains(index); i++) // at most 2 seconds
        {
            await Wait(0.05);
        }
    }

    private void OnFadeFinished()
    {
        _fadeFinished++;
    }

    private void OnStreamChanged(AudioStream stream)
    {
        _streamChanged.Add(stream);
    }

    private void OnTrackChanged(long index, AudioStream stream)
    {
        _tracks.Add(index);
        _trackStreams.Add(stream);
    }

    private void OnPositionChanged(double position)
    {
        _positions++;
    }
}
