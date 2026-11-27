using Godot;

namespace TruelyInteractive.Subtitles;

/// <summary>Minimal example of Subtitles: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Res = "res://subtitles/resources/";

    public override async void _Ready()
    {
        // Loading a SubRip file (exported from a video editor: the timeline starts at 01:00:00).
        var subtitle = GD.Load<Subtitle>(Res + "video_editor_export.srt");
        subtitle.TimeOffset = -3600.0;
        foreach (SubtitleSegment segment in subtitle.Segments)
        {
            GD.Print(segment.StartTime, " ", segment.Text);
        }

        // Subtitle formats.
        var interview = GD.Load<Subtitle>(Res + "interview.vtt");
        ResourceSaver.Save(interview, "user://interview.ass");
        string ttml = interview.ToText(Subtitle.Format.Ttml);
        GD.Print("TTML: ", ttml.Length, " characters");

        // Creating subtitles in code.
        var door = new Subtitle();
        door.AddSegment(new SubtitleSegment { StartTime = 1.5, EndTime = 4.0, Text = "Who is there?", Speaker = "guard" });
        door.AddSegment(new SubtitleSegment { StartTime = 0.5, EndTime = 1.5, Text = "Knocking", Kind = SubtitleSegment.KindEnum.Sound });
        ResourceSaver.Save(door, "user://door.srt");
        GD.Print("door.srt: ", FileAccess.GetFileAsString("user://door.srt").Replace("\n", " | "));

        // Playing subtitles with a voice line.
        SubtitleServer.TextChanged += OnSubtitleTextChanged;
        var voice = GetNode<AudioStreamPlayer>("Voice");
        SubtitleServer.Play(subtitle, Callable.From(() => (double)voice.GetPlaybackPosition()));
        voice.Play();
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        GD.Print("Label: ", GetNode<Label>("SubtitleLabel").Text.Replace("\n", " "));
        SubtitleServer.StopAll();

        // Following the track.
        int trackId = SubtitleServer.Play(subtitle);
        SubtitleServer.Seek(trackId, 13.0);
        GD.Print("At 13 s: ", SubtitleServer.GetTrackText(trackId));
        SubtitleServer.Pause(trackId);
        SubtitleServer.Resume(trackId);
        SubtitleServer.Stop(trackId);

        // Following the player's settings.
        SubtitleServer.Enabled = AccessibilitySettingsServer.IsSubtitlesEnabled();
        AccessibilitySettingsServer.SubtitlesEnabledChanged += enabled => SubtitleServer.Enabled = enabled;
        GD.Print("Subtitles enabled: ", SubtitleServer.Enabled);

        // Checking a subtitle.
        SubtitleValidation check = subtitle.Validate(17.0);
        if (!check.Valid)
        {
            GD.Print("Too fast to read: ", string.Join(", ", check.FastReadingSegments));
        }

        SubtitleServer.TextChanged -= OnSubtitleTextChanged;
        GetTree().Quit();
    }

    private void OnSubtitleTextChanged(long trackId, string text)
    {
        GetNode<Label>("SubtitleLabel").Text = text;
    }
}
