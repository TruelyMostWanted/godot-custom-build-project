using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue260FeatureSubtitlePlayer;

/// <summary>C# checks of 260-feature-subtitle-player. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue260FeatureSubtitlePlayerCheck : FeatureCheckCs
{
    private const string VideoPath = "res://196-feature-webm-video/resources/testsrc.webm";

    private readonly List<string> _events = new();

    protected override string CheckName => "260-feature-subtitle-player";

    protected override async Task RunChecks()
    {
        CheckClasses();
        CheckPlayback();
        CheckPlaybackSkipAndSeek();
        await CheckOwnClock();
        await CheckAnimationSync();
        await CheckAudioSync();
        await CheckVideoSync();
    }

    private static SubtitleSegment Segment(double start, double end, string text)
    {
        return new SubtitleSegment { StartTime = start, EndTime = end, Text = text };
    }

    // 0-1 "A", 2-4 "B", 3-5 "C" (overlaps B).
    private static Subtitle ThreeSegments()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 1.0, "A"));
        subtitle.AddSegment(Segment(2.0, 4.0, "B"));
        subtitle.AddSegment(Segment(3.0, 5.0, "C"));
        return subtitle;
    }

    // Logs "+X" for SegmentStarted, "-X" for SegmentEnded and "=X" for SegmentChanged ("=" alone for none).
    private void OnChanged(SubtitleSegment segment) => _events.Add("=" + (segment?.Text ?? ""));
    private void OnStarted(SubtitleSegment segment) => _events.Add("+" + segment.Text);
    private void OnEnded(SubtitleSegment segment) => _events.Add("-" + segment.Text);

    private void LogSignals(SubtitlePlayback playback)
    {
        playback.SegmentChanged += OnChanged;
        playback.SegmentStarted += OnStarted;
        playback.SegmentEnded += OnEnded;
    }

    private void LogSignals(SubtitlePlayer player)
    {
        player.SegmentChanged += OnChanged;
        player.SegmentStarted += OnStarted;
        player.SegmentEnded += OnEnded;
    }

    private string Take()
    {
        string result = string.Join(" ", _events);
        _events.Clear();
        return result;
    }

    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void CheckClasses()
    {
        Expect(ClassDB.ClassExists("SubtitlePlayback"), "class SubtitlePlayback exists");
        Expect(ClassDB.ClassExists("SubtitlePlayer"), "class SubtitlePlayer exists");
        Expect(new SubtitlePlayback() is RefCounted, "SubtitlePlayback is a RefCounted");
        var player = new SubtitlePlayer();
        Expect(player is Node, "SubtitlePlayer is a Node");
        Expect(player.GetPlayback() is SubtitlePlayback, "GetPlayback() returns the SubtitlePlayback");
        Expect(!player.Autoplay && player.SyncNode.IsEmpty && player.TimeOffset == 0.0, "defaults: no autoplay, no sync node, no offset");
        player.Free();
    }

    private void CheckPlayback()
    {
        var playback = new SubtitlePlayback();
        LogSignals(playback);
        playback.Subtitle = ThreeSegments();

        playback.Update(0.5);
        ExpectEqual(Take(), "+A =A", "Update() starts A");
        playback.Update(1.0);
        ExpectEqual(Take(), "-A =", "end times are exclusive");
        playback.Update(3.5);
        ExpectEqual(Take(), "+B +C =C", "overlapping segments start in order, the current one is the latest");
        ExpectEqual(playback.GetActiveSegments().Count, 2, "two active segments");
        Expect(Mathf.IsEqualApprox(playback.GetTime(), 3.5), "GetTime() is the last time");
        playback.Update(4.5);
        ExpectEqual(Take(), "-B", "B ends, C stays current");
        ExpectEqual(playback.GetCurrentSegment().Text, "C", "GetCurrentSegment() is C");

        playback.Subtitle.TimeOffset = 10.0;
        playback.Update(10.5);
        ExpectEqual(Take(), "-C +A =A", "TimeOffset of the subtitle moves the segments");

        playback.Reset();
        ExpectEqual(Take(), "-A =", "Reset() ends the active segments");
        Expect(playback.GetCurrentSegment() == null, "nothing is active after Reset()");
    }

    private void CheckPlaybackSkipAndSeek()
    {
        var playback = new SubtitlePlayback();
        LogSignals(playback);
        playback.Subtitle = ThreeSegments();

        playback.Update(0.5);
        Take();
        playback.Update(10.0);
        ExpectEqual(Take(), "-A +B -B +C -C =", "Update() reports segments skipped by a long frame");
        playback.Seek(0.5);
        ExpectEqual(Take(), "+A =A", "Seek() backwards");
        playback.Seek(10.0);
        ExpectEqual(Take(), "-A =", "Seek() forward skips silently");

        var unsorted = new Subtitle
        {
            Segments = new Godot.Collections.Array<SubtitleSegment> { Segment(3.0, 4.0, "late"), Segment(0.0, 1.0, "early") },
        };
        playback.Subtitle = unsorted;
        playback.Update(0.5);
        ExpectEqual(playback.GetCurrentSegment().Text, "early", "unsorted segments are found");
        playback.Update(3.5);
        ExpectEqual(playback.GetCurrentSegment().Text, "late", "unsorted segments are found in time order");
        Take();
    }

    private async Task CheckOwnClock()
    {
        var player = new SubtitlePlayer { ProcessMode = ProcessModeEnum.Pausable };
        LogSignals(player);
        player.Subtitle = ThreeSegments();
        AddChild(player);
        await Frames();
        Expect(!player.IsPlaying(), "no autoplay: not playing");
        ExpectEqual(Take(), "", "nothing happens before Play()");

        player.Play(0.5);
        Expect(player.IsPlaying(), "Play() starts the own clock");
        ExpectEqual(Take(), "+A =A", "Play() emits the segment at the start position");
        player.Seek(2.5);
        ExpectEqual(Take(), "-A +B =B", "Seek() forward");
        await Frames();
        Expect(player.GetPlaybackPosition() > 2.5, $"the own clock advances ({player.GetPlaybackPosition()})");
        player.Seek(0.25);
        ExpectEqual(Take(), "-B +A =A", "Seek() backwards");

        GetTree().Paused = true;
        double pausedAt = player.GetPlaybackPosition();
        await Frames(3);
        ExpectEqual(player.GetPlaybackPosition(), pausedAt, "the own clock stands still while the tree is paused");
        GetTree().Paused = false;

        player.Stop();
        Expect(!player.IsPlaying(), "Stop() stops");
        ExpectEqual(Take(), "-A =", "Stop() ends the active segment");
        player.QueueFree();

        var autoplayer = new SubtitlePlayer { Subtitle = ThreeSegments(), Autoplay = true };
        AddChild(autoplayer);
        Expect(autoplayer.IsPlaying(), "Autoplay starts the own clock when ready");
        SubtitleSegment first = autoplayer.GetPlayback().GetCurrentSegment();
        Expect(first != null && first.Text == "A", "Autoplay starts at 0");
        autoplayer.QueueFree();
    }

    private async Task CheckAnimationSync()
    {
        var animation = new Animation { Length = 10.0f };
        var library = new AnimationLibrary();
        library.AddAnimation("talk", animation);
        var animationPlayer = new AnimationPlayer();
        animationPlayer.AddAnimationLibrary("", library);
        AddChild(animationPlayer);

        var player = new SubtitlePlayer();
        LogSignals(player);
        player.Subtitle = ThreeSegments();
        AddChild(player);
        player.SyncNode = player.GetPathTo(animationPlayer);

        animationPlayer.Play("talk");
        animationPlayer.Seek(2.5, true);
        await Frames();
        Expect(player.IsPlaying(), "follows a playing AnimationPlayer");
        ExpectEqual(Take(), "+B =B", "animation sync: B at 2.5 s");
        animationPlayer.Seek(0.5, true);
        await Frames();
        ExpectEqual(Take(), "-B +A =A", "animation sync: seeking backwards");
        animationPlayer.Stop();
        await Frames();
        Expect(!player.IsPlaying(), "a stopped AnimationPlayer stops the subtitles");
        ExpectEqual(Take(), "-A =", "animation sync: stop ends the segment");
        player.QueueFree();
        animationPlayer.QueueFree();
    }

    private static AudioStreamWav Silence(double seconds)
    {
        var data = new byte[(int)(seconds * 8000)];
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format8Bits, MixRate = 8000, Data = data };
    }

    private async Task CheckAudioSync()
    {
        var audio = new AudioStreamPlayer { Stream = Silence(6.0) };
        AddChild(audio);

        var player = new SubtitlePlayer();
        LogSignals(player);
        player.Subtitle = ThreeSegments();
        AddChild(player);
        player.SyncNode = player.GetPathTo(audio);

        audio.Play(2.5f);
        await Frames();
        Expect(player.IsPlaying(), "follows a playing AudioStreamPlayer");
        ExpectEqual(Take(), "+B =B", "audio sync: B at 2.5 s");
        audio.Seek(0.5f);
        await Frames();
        ExpectEqual(Take(), "-B +A =A", "audio sync: seeking backwards");
        audio.Stop();
        await Frames();
        Expect(!player.IsPlaying(), "a stopped AudioStreamPlayer stops the subtitles");
        ExpectEqual(Take(), "-A =", "audio sync: stop ends the segment");
        player.QueueFree();
        audio.QueueFree();
    }

    private async Task CheckVideoSync()
    {
        var video = new VideoStreamPlayer { Stream = GD.Load<VideoStream>(VideoPath) };
        AddChild(video);

        // The clip is about 1 s long: "early" until 0.5 s, then "late".
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 0.5, "early"));
        subtitle.AddSegment(Segment(0.5, 5.0, "late"));
        var player = new SubtitlePlayer();
        LogSignals(player);
        player.Subtitle = subtitle;
        AddChild(player);
        player.SyncNode = player.GetPathTo(video);

        video.Play();
        await Frames();
        Expect(player.IsPlaying(), "follows a playing VideoStreamPlayer");
        ExpectEqual(Take(), "+early =early", "video sync: early at the start");
        video.StreamPosition = 0.7;
        await Frames();
        ExpectEqual(Take(), "-early +late =late", "video sync: seeking forward");
        video.Stop();
        await Frames();
        Expect(!player.IsPlaying(), "a stopped VideoStreamPlayer stops the subtitles");
        ExpectEqual(Take(), "-late =", "video sync: stop ends the segment");
        player.QueueFree();
        video.QueueFree();
    }
}
