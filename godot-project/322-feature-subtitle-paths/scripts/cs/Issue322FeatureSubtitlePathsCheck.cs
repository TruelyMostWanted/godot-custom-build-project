using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue322FeatureSubtitlePaths;

/// <summary>C# checks of 322-feature-subtitle-paths. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue322FeatureSubtitlePathsCheck : FeatureCheckCs
{
    private const string TrackA = "res://322-feature-subtitle-paths/track_a.srt";
    private const string TrackB = "res://322-feature-subtitle-paths/track_b.srt";

    private readonly List<bool> _events = new();

    protected override string CheckName => "322-feature-subtitle-paths";

    protected override async Task RunChecks()
    {
        CheckProperties();
        await CheckPlayingChanged();
        await CheckStreamPath();
        await CheckPlayerOverride();
        await CheckExplicitWins();
        await CheckStreamSwitch();
        await CheckCrossfade();
        await CheckHiddenWhenStopped();
    }

    private static AudioStreamWav Silence(double seconds)
    {
        var data = new byte[(int)(seconds * 8000) * 2];
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 8000, Data = data };
    }

    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static string Text(SubtitlePlayer player)
    {
        Subtitle subtitle = player.GetPlayback().GetSubtitle();
        if (subtitle == null || subtitle.GetSegmentCount() == 0)
        {
            return "";
        }
        return subtitle.GetSegment(0).Text;
    }

    private (AudioStreamPlayer Audio, SubtitlePlayer Player) MakePair(string path)
    {
        var stream = Silence(10.0);
        stream.SubtitlePath = path;
        var audio = new AudioStreamPlayer { Stream = stream };
        AddChild(audio);
        var player = new SubtitlePlayer();
        AddChild(player);
        player.SyncNode = player.GetPathTo(audio);
        return (audio, player);
    }

    private void CheckProperties()
    {
        var stream = new AudioStreamWav();
        ExpectEqual(stream.SubtitlePath, "", "AudioStream.SubtitlePath is empty by default");
        stream.SubtitlePath = TrackA;
        ExpectEqual(stream.SubtitlePath, TrackA, "AudioStream.SubtitlePath round trip");
        Expect(ClassDB.ClassHasMethod("VideoStream", "get_subtitle_path"), "VideoStream has get_subtitle_path()");
        Expect(ClassDB.ClassHasMethod("VideoStream", "set_subtitle_path"), "VideoStream has set_subtitle_path()");
        bool found = false;
        foreach (var property in ClassDB.ClassGetPropertyList("VideoStream"))
        {
            if ((string)property["name"] == "subtitle_path")
            {
                found = true;
            }
        }
        Expect(found, "VideoStream has the property subtitle_path");
        if (ClassDB.CanInstantiate("VideoStream"))
        {
            var video = (VideoStream)ClassDB.Instantiate("VideoStream");
            video.SubtitlePath = TrackB;
            ExpectEqual(video.SubtitlePath, TrackB, "VideoStream.SubtitlePath round trip");
        }
        var audio = new AudioStreamPlayer();
        var audio2D = new AudioStreamPlayer2D();
        var audio3D = new AudioStreamPlayer3D();
        var videoPlayer = new VideoStreamPlayer();
        ExpectEqual(audio.SubtitlePath, "", "AudioStreamPlayer.SubtitlePath is empty by default");
        audio.SubtitlePath = TrackB;
        ExpectEqual(audio.SubtitlePath, TrackB, "AudioStreamPlayer.SubtitlePath round trip");
        audio2D.SubtitlePath = TrackB;
        ExpectEqual(audio2D.SubtitlePath, TrackB, "AudioStreamPlayer2D.SubtitlePath round trip");
        audio3D.SubtitlePath = TrackB;
        ExpectEqual(audio3D.SubtitlePath, TrackB, "AudioStreamPlayer3D.SubtitlePath round trip");
        videoPlayer.SubtitlePath = TrackB;
        ExpectEqual(videoPlayer.SubtitlePath, TrackB, "VideoStreamPlayer.SubtitlePath round trip");
        Expect(audio.HasSignal("playing_changed"), "AudioStreamPlayer has the signal playing_changed");
        Expect(audio2D.HasSignal("playing_changed"), "AudioStreamPlayer2D has the signal playing_changed");
        Expect(audio3D.HasSignal("playing_changed"), "AudioStreamPlayer3D has the signal playing_changed");
        Expect(videoPlayer.HasSignal("playing_changed"), "VideoStreamPlayer has the signal playing_changed");
        Expect(videoPlayer.HasSignal("stream_changed"), "VideoStreamPlayer has the signal stream_changed");
        audio.Free();
        audio2D.Free();
        audio3D.Free();
        videoPlayer.Free();
    }

    private async Task CheckPlayingChanged()
    {
        var audio = new AudioStreamPlayer { Stream = Silence(10.0) };
        AddChild(audio);
        audio.PlayingChanged += playing => _events.Add(playing);
        audio.Play();
        await Frames();
        ExpectEqual(string.Join(",", _events), "True", "PlayingChanged(true) on Play()");
        audio.StreamPaused = true;
        await Frames();
        ExpectEqual(string.Join(",", _events), "True,False", "PlayingChanged(false) on StreamPaused = true");
        audio.StreamPaused = false;
        await Frames();
        ExpectEqual(string.Join(",", _events), "True,False,True", "PlayingChanged(true) on resume");
        audio.Stop();
        await Frames();
        ExpectEqual(string.Join(",", _events), "True,False,True,False", "PlayingChanged(false) on Stop()");
        audio.QueueFree();
        _events.Clear();
    }

    private async Task CheckStreamPath()
    {
        var (audio, player) = MakePair(TrackA);
        Expect(player.Subtitle == null, "the explicit subtitle stays empty");
        audio.Play();
        await Frames();
        Expect(player.GetPlayback().GetSubtitle() != null, "the subtitle of the stream path is loaded after Play()");
        ExpectEqual(Text(player), "Track A", "the first segment is Track A");
        Expect(player.Subtitle == null, "Subtitle returns only the explicit subtitle");
        audio.QueueFree();
        player.QueueFree();
    }

    private async Task CheckPlayerOverride()
    {
        var (audio, player) = MakePair(TrackA);
        audio.SubtitlePath = TrackB;
        audio.Play();
        await Frames();
        ExpectEqual(Text(player), "Track B", "the SubtitlePath of the player wins over the stream's");
        audio.QueueFree();
        player.QueueFree();
    }

    private async Task CheckExplicitWins()
    {
        var (audio, player) = MakePair(TrackA);
        var explicitSubtitle = new Subtitle();
        explicitSubtitle.AddSegment(new SubtitleSegment { StartTime = 0.0, EndTime = 5.0, Text = "Explicit" });
        player.Subtitle = explicitSubtitle;
        audio.Play();
        await Frames();
        Expect(player.Subtitle == explicitSubtitle, "Subtitle returns the explicit subtitle");
        ExpectEqual(Text(player), "Explicit", "the explicit subtitle wins over the paths");
        audio.QueueFree();
        player.QueueFree();
    }

    private async Task CheckStreamSwitch()
    {
        var (audio, player) = MakePair(TrackA);
        audio.Play();
        await Frames();
        ExpectEqual(Text(player), "Track A", "before the switch: Track A");
        var other = Silence(10.0);
        other.SubtitlePath = TrackB;
        audio.Stream = other;
        audio.Play();
        await Frames();
        ExpectEqual(Text(player), "Track B", "after switching the stream: Track B");
        audio.QueueFree();
        player.QueueFree();
    }

    private async Task CheckCrossfade()
    {
        var (audio, player) = MakePair(TrackA);
        audio.Play();
        await Frames();
        var other = Silence(10.0);
        other.SubtitlePath = TrackB;
        audio.CrossfadeTo(other, 0.1f);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        await Frames();
        ExpectEqual(Text(player), "Track B", "after CrossfadeTo(): Track B");
        audio.QueueFree();
        player.QueueFree();
    }

    private async Task CheckHiddenWhenStopped()
    {
        var (audio, player) = MakePair(TrackA);
        var texts = new List<string>();
        player.SegmentChanged += segment => texts.Add(segment?.Text ?? "");
        audio.Play();
        await Frames();
        ExpectEqual(texts.Count > 0 ? texts[^1] : "<none>", "Track A", "text shown while playing");
        audio.Stop();
        await Frames();
        ExpectEqual(texts.Count > 0 ? texts[^1] : "<none>", "", "text hidden after Stop()");
        audio.QueueFree();
        player.QueueFree();
    }
}
