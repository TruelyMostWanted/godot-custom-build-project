using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue227FeatureSubtitles;

/// <summary>C# checks of 227-feature-subtitles. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue227FeatureSubtitlesCheck : FeatureCheckCs
{
    private const string Dir = "res://227-feature-subtitles/resources/";
    private const string Note = "♪";

    private readonly List<string> _events = new();
    private double _clock;

    protected override string CheckName => "227-feature-subtitles";

    protected override async Task RunChecks()
    {
        CheckClasses();
        CheckSrtFromVideoEditor();
        CheckRoundTrip();
        CheckExampleResource();
        CheckValidation();
        CheckDisplayText();
        CheckOwnClock();
        await CheckTimeSource();
    }

    private static SubtitleSegment Segment(double start, double end, string text, SubtitleSegment.KindEnum kind = SubtitleSegment.KindEnum.Dialog)
    {
        return new SubtitleSegment { StartTime = start, EndTime = end, Text = text, Kind = kind };
    }

    private static Subtitle ThreeSegments()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 1.0, "A"));
        subtitle.AddSegment(Segment(2.0, 4.0, "B"));
        subtitle.AddSegment(Segment(3.0, 5.0, "C"));
        return subtitle;
    }

    private void CheckClasses()
    {
        foreach (string name in new[] { "Subtitle", "SubtitleSegment", "SubtitleValidation", "SubtitleServer" })
        {
            Expect(ClassDB.ClassExists(name), $"class {name} exists");
        }
        Expect(Engine.HasSingleton("SubtitleServer"), "SubtitleServer is a singleton");
        ExpectEqual((long)SubtitleSegment.KindEnum.Max, 7L, "seven segment kinds");
        ExpectEqual(SubtitleServer.InvalidTrack, 0L, "INVALID_TRACK is 0");
        Expect(ResourceLoader.GetRecognizedExtensionsForType("Subtitle").Contains("srt"), "ResourceLoader knows .srt for Subtitle");
    }

    private void CheckSrtFromVideoEditor()
    {
        var subtitle = GD.Load<Subtitle>(Dir + "video_editor_export.srt");
        Expect(subtitle != null, "load() returns a Subtitle for .srt");
        if (subtitle == null)
        {
            return;
        }
        ExpectEqual(subtitle.GetSegmentCount(), 6, "six cues (CRLF, numbered)");
        SubtitleSegment first = subtitle.GetSegment(0);
        Expect(Mathf.IsEqualApprox(first.StartTime, 3600.5), "first cue starts at 01:00:00,500");
        ExpectEqual(first.Text, "Willkommen bei den Untertiteln\nder Truely-Engine.", "two-line text");
        ExpectEqual(subtitle.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "[Applaus] is a sound");
        ExpectEqual(subtitle.GetSegment(1).Text, "Applaus", "the brackets are not part of the text");
        ExpectEqual(subtitle.GetSegment(2).Kind, SubtitleSegment.KindEnum.Music, "music notes mark music");
        ExpectEqual(subtitle.GetSegment(2).Text, "ruhige Musik", "the notes are not part of the text");
        ExpectEqual(subtitle.GetSegment(3).Speaker.ToString(), "WACHE", "WACHE: is the speaker");
        ExpectEqual(subtitle.GetSegment(3).Text, "Wer ist da?", "the speaker is not part of the text");
        Expect(subtitle.GetSegment(4).OffScreen, "<i> around the whole text is off-screen");
        ExpectEqual(subtitle.GetSegment(5).Text, "Fett und oben", "<b> and {\\an8} are removed");

        subtitle.TimeOffset = -3600.0;
        Expect(Mathf.IsEqualApprox(subtitle.GetStartTime(), 0.5), "time_offset moves the start to 0.5 s");
        SubtitleSegment atFour = subtitle.FindSegmentAt(4.0);
        Expect(atFour != null && atFour.Text == "Applaus", "find_segment_at() uses the offset");
        subtitle.TimeOffset = 0.0;
    }

    private void CheckRoundTrip()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(1.0, 2.5, "Hallo"));
        subtitle.AddSegment(Segment(3.0, 4.0, "Applaus", SubtitleSegment.KindEnum.Sound));
        SubtitleSegment spoken = Segment(5.0, 6.0, "Wer ist da?");
        spoken.Speaker = "WACHE";
        spoken.OffScreen = true;
        subtitle.AddSegment(spoken);

        string text = subtitle.ToText(Subtitle.Format.Srt);
        Expect(text.StartsWith("1\n00:00:01,000 --> 00:00:02,500\nHallo\n"), "to_text() writes SubRip");
        Expect(text.Contains("[Applaus]") && text.Contains("<i>WACHE: Wer ist da?</i>"), "to_text() writes decorations back");

        var copy = new Subtitle();
        ExpectEqual(copy.LoadFromText(text, Subtitle.Format.Srt), Error.Ok, "load_from_text()");
        ExpectEqual(copy.GetSegmentCount(), 3, "three segments again");
        ExpectEqual(copy.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "kind survives the round trip");
        Expect(copy.GetSegment(2).OffScreen && copy.GetSegment(2).Speaker == "WACHE", "speaker and off_screen survive");
        ExpectEqual(new Subtitle().LoadFromText("only text", Subtitle.Format.Srt), Error.ParseError, "text without cues is a parse error");

        const string path = "user://check_227_cs.srt";
        ExpectEqual(ResourceSaver.Save(subtitle, path), Error.Ok, "ResourceSaver.save() writes .srt");
        var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as Subtitle;
        Expect(loaded != null && loaded.GetSegmentCount() == 3, "the saved .srt loads again");
        if (loaded != null)
        {
            Expect(Mathf.IsEqualApprox(loaded.GetSegment(0).EndTime, 2.5), "times survive");
        }
    }

    private void CheckExampleResource()
    {
        var story = GD.Load<Subtitle>(Dir + "village_story.tres");
        Expect(story != null, "village_story.tres loads");
        if (story == null)
        {
            return;
        }
        ExpectEqual(story.Language, "de", "language");
        ExpectEqual(story.GetSegmentCount(), 8, "eight segments");
        int kinds = story.Segments.Select(segment => segment.Kind).Distinct().Count();
        ExpectEqual(kinds, (int)SubtitleSegment.KindEnum.Max, "every kind is used");
        ExpectEqual(story.GetSegment(7).Placement, SubtitleSegment.PlacementEnum.Top, "placement is stored");
        ExpectEqual(story.GetSegmentsAt(6.5).Count, 2, "overlapping sound and reaction");
    }

    private void CheckValidation()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 2.0, "Gut."));
        subtitle.AddSegment(Segment(3.0, 3.0, "Ohne Dauer."));
        subtitle.AddSegment(Segment(4.0, 6.0, ""));
        subtitle.AddSegment(Segment(7.0, 9.0, "Überlappt."));
        subtitle.AddSegment(Segment(8.0, 8.5, "Viel zu viel Text für eine halbe Sekunde."));
        SubtitleValidation validation = subtitle.Validate();
        Expect(!validation.Valid, "problems are found");
        Expect(validation.InvalidTimeSegments.SequenceEqual(new[] { 1 }), "invalid time");
        Expect(validation.EmptyTextSegments.SequenceEqual(new[] { 2 }), "empty text");
        Expect(validation.OverlappingSegments.SequenceEqual(new[] { 3 }), "overlap");
        Expect(validation.FastReadingSegments.SequenceEqual(new[] { 4 }), "fast reading");
        ExpectEqual(validation.GetProblemCount(), 4, "four problems");
    }

    private void CheckDisplayText()
    {
        var subtitle = new Subtitle();
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, Segment(0, 1, "Applaus", SubtitleSegment.KindEnum.Sound)), "[Applaus]", "sound in brackets");
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, Segment(0, 1, "Musik", SubtitleSegment.KindEnum.Music)), Note + " Musik " + Note, "music between notes");
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, Segment(0, 1, "Applaus", SubtitleSegment.KindEnum.Sound), true), "[lb]Applaus[rb]", "BBCode escapes brackets");
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, Segment(0, 1, "Es war einmal", SubtitleSegment.KindEnum.Narration), true), "[i]Es war einmal[/i]", "narration in italics");
    }

    private void OnTrackStarted(long id, Subtitle subtitle) => _events.Add("track_started");
    private void OnSegmentStarted(long id, SubtitleSegment segment, string text) => _events.Add("segment_started " + text);
    private void OnSegmentEnded(long id, SubtitleSegment segment) => _events.Add("segment_ended " + segment.Text);
    private void OnTextChanged(long id, string text) => _events.Add("text_changed " + text.Replace("\n", "|"));
    private void OnTrackPaused(long id) => _events.Add("track_paused");
    private void OnTrackResumed(long id) => _events.Add("track_resumed");
    private void OnTrackSeeked(long id, double time) => _events.Add($"track_seeked {time.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    private void OnTrackFinished(long id) => _events.Add("track_finished");
    private void OnTrackCanceled(long id) => _events.Add("track_canceled");

    private void ConnectLog()
    {
        SubtitleServer.TrackStarted += OnTrackStarted;
        SubtitleServer.SegmentStarted += OnSegmentStarted;
        SubtitleServer.SegmentEnded += OnSegmentEnded;
        SubtitleServer.TextChanged += OnTextChanged;
        SubtitleServer.TrackPaused += OnTrackPaused;
        SubtitleServer.TrackResumed += OnTrackResumed;
        SubtitleServer.TrackSeeked += OnTrackSeeked;
        SubtitleServer.TrackFinished += OnTrackFinished;
        SubtitleServer.TrackCanceled += OnTrackCanceled;
    }

    private void DisconnectLog()
    {
        SubtitleServer.TrackStarted -= OnTrackStarted;
        SubtitleServer.SegmentStarted -= OnSegmentStarted;
        SubtitleServer.SegmentEnded -= OnSegmentEnded;
        SubtitleServer.TextChanged -= OnTextChanged;
        SubtitleServer.TrackPaused -= OnTrackPaused;
        SubtitleServer.TrackResumed -= OnTrackResumed;
        SubtitleServer.TrackSeeked -= OnTrackSeeked;
        SubtitleServer.TrackFinished -= OnTrackFinished;
        SubtitleServer.TrackCanceled -= OnTrackCanceled;
    }

    private string Take()
    {
        string result = string.Join(", ", _events);
        _events.Clear();
        return result;
    }

    private void CheckOwnClock()
    {
        ConnectLog();
        int id = SubtitleServer.Play(ThreeSegments(), default, 2.5);
        Expect(SubtitleServer.IsPlaying(id), "play() starts a track");
        ExpectEqual(Take(), "track_started, segment_started B, text_changed B", "play() emits track_started and the visible segment");

        SubtitleServer.Seek(id, 3.5);
        ExpectEqual(Take(), "track_seeked 3.5, segment_started C, text_changed B|C", "seek() into an overlap");
        ExpectEqual(SubtitleServer.GetActiveSegments(id).Count, 2, "two active segments");
        ExpectEqual(SubtitleServer.GetTrackText(id), "B\nC", "track text has both lines");

        SubtitleServer.Pause(id);
        SubtitleServer.Resume(id);
        ExpectEqual(Take(), "track_paused, track_resumed", "pause() and resume()");

        SubtitleServer.CaptionsEnabled = false;
        SubtitleServer.CaptionsEnabled = true;
        ExpectEqual(Take(), "", "captions_enabled does not touch dialog");

        SubtitleServer.Stop(id);
        ExpectEqual(Take(), "segment_ended B, segment_ended C, text_changed , track_canceled", "stop() ends the visible segments and cancels");
        Expect(!SubtitleServer.IsPlaying(id), "the track is gone");
        DisconnectLog();
    }

    private async Task CheckTimeSource()
    {
        ConnectLog();
        _clock = 0.5;
        int id = SubtitleServer.Play(ThreeSegments(), Callable.From(() => _clock));
        ExpectEqual(Take(), "track_started, segment_started A, text_changed A", "a time source starts at its time");

        _clock = 2.5;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(Take(), "segment_ended A, segment_started B, text_changed B", "the frame update follows the time source");
        Expect(Mathf.IsEqualApprox(SubtitleServer.GetTrackTime(id), 2.5), "track time is the source time");

        _clock = 10.0;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(Take(), "segment_ended B, text_changed , track_finished", "past the end the track finishes");
        Expect(!SubtitleServer.IsPlaying(id), "the finished track is gone");
        DisconnectLog();
    }
}
