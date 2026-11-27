using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.SubtitlePlayer;

using SubtitlePlayer = Godot.SubtitlePlayer;

/// <summary>
/// Full API test of Subtitle Player: SubtitlePlayer, SubtitlePlayback, the SubtitlePath of streams and players,
/// and the PlayingChanged / StreamChanged signals of the players. Same tests as scripts/gd/Test.gd.
/// Signals are logged as "+X" (SegmentStarted), "-X" (SegmentEnded) and "=X" (SegmentChanged, "=" alone for none).
/// </summary>
public partial class Test : FeatureTest
{
    private const string Res = "res://subtitle-player/resources/";
    private const string LineA = Res + "line_a.srt";
    private const string LineB = Res + "line_b.srt";
    private const string SidecarStream = Res + "test/sidecar.tres";
    private const string Video = Res + "test/testsrc.webm";

    private readonly List<string> _events = [];

    protected override async Task RunTests()
    {
        TestDefaults();
        TestPlayback();
        TestPlaybackSkipAndSeek();
        await TestOwnClock();
        await TestAnimationSync();
        await TestAudioSync();
        await TestVideoSync();
        TestSubtitlePathProperties();
        await TestPlayingChanged();
        await TestSubtitleSources();
        await TestStreamSwitch();
        await TestHiddenWhenStopped();
    }

    private void TestDefaults()
    {
        var player = new SubtitlePlayer();
        Expect(player.Subtitle == null, "Subtitle is null by default");
        Expect(player.SyncNode.IsEmpty, "SyncNode is empty by default");
        ExpectEqual(player.TimeOffset, 0.0, "TimeOffset is 0 by default");
        Expect(!player.Autoplay, "Autoplay is off by default");
        Expect(player.GetPlayback() is SubtitlePlayback, "GetPlayback returns the SubtitlePlayback of the node");
        player.Free();
    }

    private void TestPlayback()
    {
        var playback = new SubtitlePlayback();
        Log(playback);
        playback.Subtitle = ThreeSegments();
        playback.Update(0.5);
        ExpectEqual(Take(), "+A =A", "Update starts A");
        playback.Update(1.0);
        ExpectEqual(Take(), "-A =", "the end time is exclusive");
        playback.Update(3.5);
        ExpectEqual(Take(), "+B +C =C", "overlapping segments start in order, the current one starts last");
        ExpectEqual(playback.GetActiveSegments().Count, 2, "GetActiveSegments returns both");
        ExpectEqual(playback.GetTime(), 3.5, "GetTime is the last time");
        playback.Update(4.5);
        ExpectEqual(Take(), "-B", "B ends, C stays current");
        ExpectEqual(playback.GetCurrentSegment().Text, "C", "GetCurrentSegment is C");
        playback.Subtitle.TimeOffset = 10.0;
        playback.Update(10.5);
        ExpectEqual(Take(), "-C +A =A", "the TimeOffset of the subtitle moves the segments");
        playback.Reset();
        ExpectEqual(Take(), "-A =", "Reset ends the active segments");
        Expect(playback.GetCurrentSegment() == null, "nothing is active after Reset");
    }

    private void TestPlaybackSkipAndSeek()
    {
        var playback = new SubtitlePlayback();
        Log(playback);
        playback.Subtitle = ThreeSegments();
        playback.Update(0.5);
        Take();
        playback.Update(10.0);
        ExpectEqual(Take(), "-A +B -B +C -C =", "Update reports segments skipped by a long frame");
        playback.Seek(0.5);
        ExpectEqual(Take(), "+A =A", "Seek backwards");
        playback.Seek(10.0);
        ExpectEqual(Take(), "-A =", "Seek forward skips silently");
        var unsorted = new Subtitle { Segments = [Segment(3.0, 4.0, "late"), Segment(0.0, 1.0, "early")] };
        playback.Subtitle = unsorted;
        playback.Update(0.5);
        ExpectEqual(playback.GetCurrentSegment().Text, "early", "unsorted segments are found");
        playback.Update(3.5);
        ExpectEqual(playback.GetCurrentSegment().Text, "late", "unsorted segments are found in time order");
        _events.Clear();
    }

    private async Task TestOwnClock()
    {
        var player = new SubtitlePlayer { ProcessMode = ProcessModeEnum.Pausable };
        Log(player);
        player.Subtitle = ThreeSegments();
        AddChild(player);
        await Frames(2);
        Expect(!player.IsPlaying(), "without Autoplay the player does not play");
        ExpectEqual(Take(), "", "nothing is emitted before Play");
        player.Play(0.5);
        Expect(player.IsPlaying(), "Play starts the own clock");
        ExpectEqual(Take(), "+A =A", "Play emits the segment at the start position");
        player.Seek(2.5);
        ExpectEqual(Take(), "-A +B =B", "Seek forward");
        await Frames(2);
        Expect(player.GetPlaybackPosition() > 2.5, "the own clock advances");
        player.Seek(0.25);
        ExpectEqual(Take(), "-B +A =A", "Seek backwards");
        GetTree().Paused = true;
        double pausedAt = player.GetPlaybackPosition();
        await Frames(3);
        ExpectEqual(player.GetPlaybackPosition(), pausedAt, "the own clock stands still while the tree is paused");
        GetTree().Paused = false;
        player.Stop();
        Expect(!player.IsPlaying(), "Stop stops the own clock");
        ExpectEqual(Take(), "-A =", "Stop ends the active segment");
        player.TimeOffset = 2.0;
        player.Play(0.5);
        ExpectEqual(Take(), "+B =B", "TimeOffset is added to the position");
        ExpectEqual(player.GetPlaybackPosition(), 0.5, "GetPlaybackPosition is without TimeOffset");
        player.QueueFree();
        var autoplayer = new SubtitlePlayer { Subtitle = ThreeSegments(), Autoplay = true };
        AddChild(autoplayer);
        Expect(autoplayer.IsPlaying(), "Autoplay starts the own clock when ready");
        ExpectEqual(autoplayer.GetPlayback().GetCurrentSegment().Text, "A", "Autoplay starts at 0");
        autoplayer.QueueFree();
    }

    private async Task TestAnimationSync()
    {
        var animation = new Animation { Length = 10.0f };
        var library = new AnimationLibrary();
        library.AddAnimation("talk", animation);
        var animationPlayer = new AnimationPlayer();
        animationPlayer.AddAnimationLibrary("", library);
        AddChild(animationPlayer);
        SubtitlePlayer player = SyncedPlayer(animationPlayer);
        player.Subtitle = ThreeSegments();
        animationPlayer.Play("talk");
        animationPlayer.Seek(2.5, true);
        await Frames(2);
        Expect(player.IsPlaying(), "the player follows a playing AnimationPlayer");
        ExpectEqual(Take(), "+B =B", "AnimationPlayer sync: B at 2.5 s");
        animationPlayer.Seek(0.5, true);
        await Frames(2);
        ExpectEqual(Take(), "-B +A =A", "AnimationPlayer sync: seeking backwards");
        animationPlayer.Stop();
        await Frames(2);
        Expect(!player.IsPlaying(), "a stopped AnimationPlayer stops the subtitles");
        ExpectEqual(Take(), "-A =", "AnimationPlayer sync: stop ends the segment");
        player.QueueFree();
        animationPlayer.QueueFree();
    }

    private async Task TestAudioSync()
    {
        AudioStreamPlayer audio = AudioPlayer("");
        SubtitlePlayer player = SyncedPlayer(audio);
        player.Subtitle = ThreeSegments();
        audio.Play(2.5f);
        await Frames(2);
        Expect(player.IsPlaying(), "the player follows a playing AudioStreamPlayer");
        ExpectEqual(Take(), "+B =B", "AudioStreamPlayer sync: B at 2.5 s");
        audio.Seek(0.5f);
        await Frames(2);
        ExpectEqual(Take(), "-B +A =A", "AudioStreamPlayer sync: seeking backwards");
        audio.Stop();
        await Frames(2);
        Expect(!player.IsPlaying(), "a stopped AudioStreamPlayer stops the subtitles");
        ExpectEqual(Take(), "-A =", "AudioStreamPlayer sync: stop ends the segment");
        player.QueueFree();
        audio.QueueFree();
    }

    private async Task TestVideoSync()
    {
        var video = new VideoStreamPlayer();
        var streams = new List<VideoStream>();
        video.StreamChanged += stream => streams.Add(stream);
        video.Stream = GD.Load<VideoStream>(Video);
        Expect(streams.SequenceEqual([video.Stream]), "VideoStreamPlayer emits StreamChanged");
        AddChild(video);
        // The clip is about 1 s long: "early" until 0.5 s, then "late".
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 0.5, "early"));
        subtitle.AddSegment(Segment(0.5, 5.0, "late"));
        SubtitlePlayer player = SyncedPlayer(video);
        player.Subtitle = subtitle;
        video.Play();
        await Frames(2);
        Expect(player.IsPlaying(), "the player follows a playing VideoStreamPlayer");
        ExpectEqual(Take(), "+early =early", "VideoStreamPlayer sync: early at the start");
        video.StreamPosition = 0.7;
        await Frames(2);
        ExpectEqual(Take(), "-early +late =late", "VideoStreamPlayer sync: seeking forward");
        video.Stop();
        await Frames(2);
        Expect(!player.IsPlaying(), "a stopped VideoStreamPlayer stops the subtitles");
        ExpectEqual(Take(), "-late =", "VideoStreamPlayer sync: stop ends the segment");
        player.QueueFree();
        video.QueueFree();
    }

    private void TestSubtitlePathProperties()
    {
        var stream = new AudioStreamWav();
        ExpectEqual(stream.SubtitlePath, "", "AudioStream.SubtitlePath is empty by default");
        stream.SubtitlePath = LineA;
        ExpectEqual(stream.SubtitlePath, LineA, "AudioStream.SubtitlePath can be set");
        var video = GD.Load<VideoStream>(Video);
        video.SubtitlePath = LineB;
        ExpectEqual(video.SubtitlePath, LineB, "VideoStream.SubtitlePath can be set");
        foreach (Node player in new Node[] { new AudioStreamPlayer(), new AudioStreamPlayer2D(), new AudioStreamPlayer3D(), new VideoStreamPlayer() })
        {
            ExpectEqual((string)player.Get("subtitle_path"), "", $"{player.GetClass()}.subtitle_path is empty by default");
            player.Set("subtitle_path", LineB);
            ExpectEqual((string)player.Get("subtitle_path"), LineB, $"{player.GetClass()}.subtitle_path can be set");
            Expect(player.HasSignal("playing_changed"), $"{player.GetClass()} has the signal playing_changed");
            player.Free();
        }
        video.SubtitlePath = "";
    }

    private async Task TestPlayingChanged()
    {
        AudioStreamPlayer audio = AudioPlayer("");
        var events = new List<bool>();
        audio.PlayingChanged += playing => events.Add(playing);
        audio.Play();
        await Frames(3);
        audio.StreamPaused = true;
        await Frames(3);
        audio.StreamPaused = false;
        await Frames(3);
        audio.Seek(1.0f);
        await Frames(3);
        audio.Stop();
        await Frames(3);
        Expect(events.SequenceEqual([true, false, true, false]), "PlayingChanged follows play, pause, resume and stop, but not seek");
        audio.QueueFree();
    }

    private async Task TestSubtitleSources()
    {
        AudioStreamPlayer audio = AudioPlayer(LineA);
        SubtitlePlayer player = SyncedPlayer(audio);
        audio.Play();
        await Frames(3);
        ExpectEqual(FirstText(player), "Track A", "the SubtitlePath of the stream is loaded");
        Expect(player.Subtitle == null, "Subtitle returns only an explicit subtitle");
        audio.Stop();
        audio.SubtitlePath = LineB;
        audio.Play();
        await Frames(3);
        ExpectEqual(FirstText(player), "Track B", "the SubtitlePath of the player wins over the stream's");
        var explicitSubtitle = new Subtitle();
        explicitSubtitle.AddSegment(Segment(0.0, 5.0, "Explicit"));
        player.Subtitle = explicitSubtitle;
        await Frames(3);
        ExpectEqual(FirstText(player), "Explicit", "an explicit subtitle wins over the paths");
        audio.Stream = GD.Load<AudioStream>(SidecarStream);
        audio.SubtitlePath = "";
        player.Subtitle = null;
        audio.Play();
        await Frames(3);
        ExpectEqual(FirstText(player), "Sidecar", "a sidecar file next to the stream file is found");
        audio.QueueFree();
        player.QueueFree();
        _events.Clear();
    }

    private async Task TestStreamSwitch()
    {
        AudioStreamPlayer audio = AudioPlayer(LineA);
        SubtitlePlayer player = SyncedPlayer(audio);
        audio.Play();
        await Frames(3);
        audio.Stream = Silence(LineB);
        audio.Play();
        await Frames(3);
        ExpectEqual(FirstText(player), "Track B", "a new stream shows its SubtitlePath");
        audio.CrossfadeTo(Silence(LineA), 0.1f);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        ExpectEqual(FirstText(player), "Track A", "CrossfadeTo shows the SubtitlePath of the new stream");
        audio.QueueFree();
        player.QueueFree();
        _events.Clear();
    }

    private async Task TestHiddenWhenStopped()
    {
        AudioStreamPlayer audio = AudioPlayer(LineA);
        SubtitlePlayer player = SyncedPlayer(audio);
        audio.Play();
        await Frames(3);
        ExpectEqual(_events.Last(), "=Track A", "the text is shown while playing");
        audio.Stop();
        await Frames(3);
        ExpectEqual(_events.Last(), "=", "the text is hidden after Stop");
        audio.QueueFree();
        player.QueueFree();
        _events.Clear();
    }

    /// <summary>An AudioStreamPlayer in the tree with 10 seconds of silence that has the given SubtitlePath.</summary>
    private AudioStreamPlayer AudioPlayer(string subtitlePath)
    {
        var audio = new AudioStreamPlayer { Stream = Silence(subtitlePath) };
        AddChild(audio);
        return audio;
    }

    /// <summary>A logged SubtitlePlayer in the tree that follows sync.</summary>
    private SubtitlePlayer SyncedPlayer(Node sync)
    {
        var player = new SubtitlePlayer();
        Log(player);
        AddChild(player);
        player.SyncNode = player.GetPathTo(sync);
        return player;
    }

    private static AudioStreamWav Silence(string subtitlePath)
    {
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format8Bits,
            MixRate = 8000,
            Data = new byte[80000],
            SubtitlePath = subtitlePath,
        };
    }

    private static SubtitleSegment Segment(double start, double end, string text)
    {
        return new SubtitleSegment { StartTime = start, EndTime = end, Text = text };
    }

    /// <summary>0-1 "A", 2-4 "B", 3-5 "C" (overlaps B).</summary>
    private static Subtitle ThreeSegments()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 1.0, "A"));
        subtitle.AddSegment(Segment(2.0, 4.0, "B"));
        subtitle.AddSegment(Segment(3.0, 5.0, "C"));
        return subtitle;
    }

    /// <summary>The text of the first segment of the subtitle in use.</summary>
    private static string FirstText(SubtitlePlayer player)
    {
        return player.GetPlayback().GetSubtitle().GetSegment(0).Text;
    }

    private void Log(GodotObject source)
    {
        source.Connect("segment_changed", Callable.From<SubtitleSegment>(OnSegmentChanged));
        source.Connect("segment_started", Callable.From<SubtitleSegment>(segment => _events.Add("+" + segment.Text)));
        source.Connect("segment_ended", Callable.From<SubtitleSegment>(segment => _events.Add("-" + segment.Text)));
    }

    private void OnSegmentChanged(SubtitleSegment segment)
    {
        if (segment == null)
        {
            _events.Add("=");
            return;
        }
        _events.Add("=" + segment.Text);
    }

    /// <summary>Returns the logged signals as one line and clears the log.</summary>
    private string Take()
    {
        string result = string.Join(" ", _events);
        _events.Clear();
        return result;
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
