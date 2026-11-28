using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Subtitles;

/// <summary>
/// Full API test of Subtitles (Subtitle, SubtitleSegment, SubtitleValidation, SubtitleServer) and the five text formats.
/// Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Res = "res://subtitles/resources/";
    private static readonly string[] Extensions = ["srt", "vtt", "ttml", "dfxp", "ass", "ssa", "sbv"];
    private const string Note = "♪";

    private readonly List<string> _events = [];
    private double _clock;

    protected override async Task RunTests()
    {
        SubtitleServer.TrackStarted += (id, subtitle) => _events.Add("track_started");
        SubtitleServer.SegmentStarted += (id, segment, text) => _events.Add("segment_started " + text);
        SubtitleServer.SegmentEnded += (id, segment) => _events.Add("segment_ended " + segment.Text);
        SubtitleServer.TextChanged += (id, text) => _events.Add("text_changed " + text.Replace("\n", "|"));
        SubtitleServer.TrackPaused += id => _events.Add("track_paused");
        SubtitleServer.TrackResumed += id => _events.Add("track_resumed");
        SubtitleServer.TrackSeeked += (id, time) => _events.Add(FormattableString.Invariant($"track_seeked {time}"));
        SubtitleServer.TrackFinished += id => _events.Add("track_finished");
        SubtitleServer.TrackCanceled += id => _events.Add("track_canceled");
        TestConstants();
        TestSegment();
        TestSegmentList();
        TestValidation();
        TestSrt();
        TestSrtRoundTrip();
        TestWebVtt();
        TestTtml();
        TestAss();
        TestSbv();
        TestFormatRoundTrips();
        TestSampleResource();
        TestDisplayText();
        await TestSubtitlesChannel();
        TestTranslatableStrings();
        TestKeysSrt();
        TestOwnClock();
        TestSettings();
        await TestTimeSource();
    }

    private void TestConstants()
    {
        ExpectEqual((int)SubtitleSegment.KindEnum.Max, 7, "seven segment kinds");
        ExpectEqual((int)SubtitleSegment.PlacementEnum.Max, 3, "three placements");
        ExpectEqual((int)Subtitle.Format.Max, 5, "five formats");
        ExpectEqual(SubtitleServer.InvalidTrack, 0, "InvalidTrack is 0");
        string[] extensions = ResourceLoader.GetRecognizedExtensionsForType("Subtitle");
        foreach (string extension in Extensions)
        {
            Expect(extensions.Contains(extension), $"ResourceLoader knows .{extension}");
        }
    }

    private void TestSegment()
    {
        SubtitleSegment segment = Segment(1.0, 3.0, "Hello");
        ExpectEqual(segment.GetDuration(), 2.0, "GetDuration is EndTime - StartTime");
        Expect(segment.ContainsTime(1.0), "ContainsTime includes the start");
        Expect(!segment.ContainsTime(3.0), "ContainsTime excludes the end");
        Expect(!segment.IsCaption(), "dialog is not a caption");
        foreach (SubtitleSegment.KindEnum kind in new[] { SubtitleSegment.KindEnum.Sound, SubtitleSegment.KindEnum.Reaction, SubtitleSegment.KindEnum.Music, SubtitleSegment.KindEnum.Lyrics })
        {
            segment.Kind = kind;
            Expect(segment.IsCaption(), $"kind {(int)kind} is a caption");
        }
    }

    private void TestSegmentList()
    {
        Subtitle subtitle = ThreeSegments();
        SubtitleSegment first = Segment(-1.0, -0.5, "Z");
        subtitle.AddSegment(first);
        Expect(subtitle.GetSegment(0) == first, "AddSegment keeps the list sorted by start time");
        subtitle.RemoveSegment(0);
        ExpectEqual(subtitle.GetSegmentCount(), 3, "RemoveSegment removes a segment");
        ExpectEqual(subtitle.GetStartTime(), 0.0, "GetStartTime is the start of the first segment");
        ExpectEqual(subtitle.GetEndTime(), 5.0, "GetEndTime is the latest end");
        ExpectEqual(subtitle.FindSegmentAt(2.5).Text, "B", "FindSegmentAt finds the visible segment");
        Expect(subtitle.FindSegmentAt(1.5) == null, "FindSegmentAt returns null in a gap");
        ExpectEqual(subtitle.GetSegmentsAt(3.5).Count, 2, "GetSegmentsAt returns overlapping segments");
        subtitle.ShiftTimes(10.0);
        ExpectEqual(subtitle.GetStartTime(), 10.0, "ShiftTimes moves every segment");
        subtitle.Segments = [Segment(5.0, 6.0, "late"), Segment(1.0, 2.0, "early")];
        subtitle.SortSegments();
        ExpectEqual(subtitle.GetSegment(0).Text, "early", "SortSegments sorts by start time");
        subtitle.ClearSegments();
        ExpectEqual(subtitle.GetSegmentCount(), 0, "ClearSegments removes all segments");
    }

    private void TestValidation()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 2.0, "Good."));
        subtitle.AddSegment(Segment(3.0, 3.0, "No duration."));
        subtitle.AddSegment(Segment(4.0, 6.0, ""));
        subtitle.AddSegment(Segment(7.0, 9.0, "Overlaps."));
        subtitle.AddSegment(Segment(8.0, 8.5, "Far too much text for half a second."));
        SubtitleValidation validation = subtitle.Validate();
        Expect(!validation.Valid, "Validate finds the problems");
        Expect(validation.InvalidTimeSegments.SequenceEqual([1]), "InvalidTimeSegments");
        Expect(validation.EmptyTextSegments.SequenceEqual([2]), "EmptyTextSegments");
        Expect(validation.OverlappingSegments.SequenceEqual([3]), "OverlappingSegments");
        Expect(validation.FastReadingSegments.SequenceEqual([4]), "FastReadingSegments");
        ExpectEqual(validation.GetProblemCount(), 4, "GetProblemCount");
        ExpectEqual(subtitle.Validate(100.0).FastReadingSegments.Length, 0, "Validate takes the characters per second");
    }

    private void TestSrt()
    {
        var subtitle = GD.Load<Subtitle>(Res + "video_editor_export.srt");
        ExpectEqual(subtitle.GetSegmentCount(), 6, "SubRip: six numbered cues with CRLF line endings");
        ExpectEqual(subtitle.GetSegment(0).StartTime, 3600.5, "SubRip: 01:00:00,500 is 3600.5 s");
        ExpectEqual(subtitle.GetSegment(0).Text, "Willkommen bei den Untertiteln\nder Truely-Engine.", "SubRip: a two-line text");
        ExpectEqual(subtitle.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "SubRip: [Applaus] is a sound");
        ExpectEqual(subtitle.GetSegment(1).Text, "Applaus", "SubRip: the brackets are not part of the text");
        ExpectEqual(subtitle.GetSegment(2).Kind, SubtitleSegment.KindEnum.Music, "SubRip: music notes mark music");
        ExpectEqual(subtitle.GetSegment(2).Text, "ruhige Musik", "SubRip: the notes are not part of the text");
        ExpectEqual(subtitle.GetSegment(3).Speaker, (StringName)"WACHE", "SubRip: WACHE: is the speaker");
        ExpectEqual(subtitle.GetSegment(3).Text, "Wer ist da?", "SubRip: the speaker is not part of the text");
        Expect(subtitle.GetSegment(4).OffScreen, "SubRip: <i> around the whole text is off-screen");
        ExpectEqual(subtitle.GetSegment(5).Text, "Fett und oben", "SubRip: <b> and {\\an8} are removed");
        subtitle.TimeOffset = -3600.0;
        ExpectEqual(subtitle.GetStartTime(), 0.5, "TimeOffset moves the start");
        ExpectEqual(subtitle.FindSegmentAt(4.0).Text, "Applaus", "FindSegmentAt uses TimeOffset");
        subtitle.TimeOffset = 0.0;
    }

    private void TestSrtRoundTrip()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(1.0, 2.5, "Hallo"));
        subtitle.AddSegment(Segment(3.0, 4.0, "Applaus", SubtitleSegment.KindEnum.Sound));
        SubtitleSegment spoken = Segment(5.0, 6.0, "Wer ist da?");
        spoken.Speaker = "WACHE";
        spoken.OffScreen = true;
        subtitle.AddSegment(spoken);
        string text = subtitle.ToText(Subtitle.Format.Srt);
        Expect(text.StartsWith("1\n00:00:01,000 --> 00:00:02,500\nHallo\n"), "ToText writes SubRip");
        Expect(text.Contains("[Applaus]"), "ToText writes a sound in brackets");
        Expect(text.Contains("<i>WACHE: Wer ist da?</i>"), "ToText writes the speaker and italics");
        var copy = new Subtitle();
        ExpectEqual(copy.LoadFromText(text, Subtitle.Format.Srt), Error.Ok, "LoadFromText reads SubRip");
        ExpectEqual(copy.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "the kind survives the round trip");
        ExpectEqual(copy.GetSegment(2).Speaker, (StringName)"WACHE", "the speaker survives the round trip");
        Expect(copy.GetSegment(2).OffScreen, "OffScreen survives the round trip");
        ExpectEqual(new Subtitle().LoadFromText("only text", Subtitle.Format.Srt), Error.ParseError, "text without cues is a parse error");
        const string path = "user://subtitles_test.srt";
        ExpectEqual(subtitle.SaveToFile(path), Error.Ok, "SaveToFile writes the file");
        var fromFile = new Subtitle();
        ExpectEqual(fromFile.LoadFromFile(path), Error.Ok, "LoadFromFile reads the file");
        ExpectEqual(fromFile.GetSegment(0).EndTime, 2.5, "the times survive SaveToFile and LoadFromFile");
    }

    private void TestWebVtt()
    {
        var subtitle = GD.Load<Subtitle>(Res + "interview.vtt");
        ExpectEqual(subtitle.GetSegmentCount(), 4, "WebVTT: four cues, NOTE and STYLE skipped");
        ExpectEqual(subtitle.GetSegment(0).Speaker, (StringName)"Host", "WebVTT: <v Host> is the speaker");
        ExpectEqual(subtitle.GetSegment(0).Text, "Welcome to the show\n& thanks for coming.", "WebVTT: entities decoded, two lines");
        ExpectEqual(subtitle.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "WebVTT: [Applause] is a sound");
        ExpectEqual(subtitle.GetSegment(1).Placement, SubtitleSegment.PlacementEnum.Top, "WebVTT: line:0 is the top");
        Expect(subtitle.GetSegment(2).OffScreen, "WebVTT: <i> is off-screen");
        ExpectEqual(subtitle.GetSegment(2).Speaker, (StringName)"Guest", "WebVTT: the voice inside italics is the speaker");
        ExpectEqual(subtitle.GetSegment(3).Kind, SubtitleSegment.KindEnum.Music, "WebVTT: music notes mark music");
    }

    private void TestTtml()
    {
        var subtitle = GD.Load<Subtitle>(Res + "broadcast.ttml");
        ExpectEqual(subtitle.GetSegmentCount(), 4, "TTML: four paragraphs");
        ExpectEqual(subtitle.Language, "de", "TTML: xml:lang is the language");
        ExpectEqual(subtitle.GetSegment(0).Text, "Guten Abend\nund willkommen.", "TTML: <br/> is a line break, whitespace collapsed");
        ExpectEqual(subtitle.GetSegment(1).EndTime, 4.48, "TTML: frames at 25 fps");
        ExpectEqual(subtitle.GetSegment(2).EndTime, 6.5, "TTML: an offset with dur");
        Expect(subtitle.GetSegment(2).OffScreen, "TTML: an italic span is off-screen");
        ExpectEqual(subtitle.GetSegment(3).Speaker, (StringName)"WACHE", "TTML: a speaker in capitals");
        ExpectEqual(subtitle.GetSegment(3).Text, "Halt & stehen bleiben!", "TTML: entities decoded");
    }

    private void TestAss()
    {
        var subtitle = GD.Load<Subtitle>(Res + "fansub.ass");
        ExpectEqual(subtitle.GetSegmentCount(), 4, "ASS: four dialog lines, the comment skipped");
        ExpectEqual(subtitle.GetSegment(0).Speaker, (StringName)"Guard", "ASS: the Name field is the speaker");
        ExpectEqual(subtitle.GetSegment(0).Text, "Who is there,\nfriend?", "ASS: \\N is a line break, commas stay");
        ExpectEqual(subtitle.GetSegment(1).Placement, SubtitleSegment.PlacementEnum.Top, "ASS: {\\an8} is the top");
        Expect(subtitle.GetSegment(2).OffScreen, "ASS: {\\i1} is off-screen");
        ExpectEqual(subtitle.GetSegment(3).Text, "Styled text", "ASS: override tags dropped, \\h is a space");
    }

    private void TestSbv()
    {
        var subtitle = GD.Load<Subtitle>(Res + "youtube.sbv");
        ExpectEqual(subtitle.GetSegmentCount(), 3, "SBV: three cues");
        ExpectEqual(subtitle.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "SBV: [Laughs] is a sound");
        ExpectEqual(subtitle.GetSegment(2).Speaker, (StringName)"HOST", "SBV: HOST: is the speaker");
    }

    private void TestFormatRoundTrips()
    {
        var source = GD.Load<Subtitle>(Res + "interview.vtt");
        foreach (string extension in Extensions)
        {
            string path = "user://subtitles_test." + extension;
            ResourceSaver.Save(source, path);
            var loaded = ResourceLoader.Load<Subtitle>(path, "", ResourceLoader.CacheMode.Ignore);
            bool same = loaded.GetSegmentCount() == source.GetSegmentCount();
            for (int i = 0; i < source.GetSegmentCount(); i++)
            {
                SubtitleSegment a = source.GetSegment(i);
                SubtitleSegment b = loaded.GetSegment(i);
                same = same && Mathf.IsEqualApprox(a.StartTime, b.StartTime) && Mathf.IsEqualApprox(a.EndTime, b.EndTime) && a.Text == b.Text && a.Kind == b.Kind;
            }
            Expect(same, $"a round trip through .{extension} keeps times, texts and kinds");
        }
        var copy = new Subtitle();
        copy.LoadFromText(source.ToText(Subtitle.Format.Ass), Subtitle.Format.Ass);
        ExpectEqual(copy.GetSegment(0).Speaker, (StringName)"Host", "ASS keeps the speaker as written");
    }

    private void TestSampleResource()
    {
        var story = GD.Load<Subtitle>(Res + "village_story.tres");
        ExpectEqual(story.Language, "de", "village_story.tres: language");
        ExpectEqual(story.GetSegmentCount(), 8, "village_story.tres: eight segments");
        int kinds = story.Segments.Select(segment => segment.Kind).Distinct().Count();
        ExpectEqual(kinds, (int)SubtitleSegment.KindEnum.Max, "village_story.tres uses every kind");
        ExpectEqual(story.GetSegment(7).Placement, SubtitleSegment.PlacementEnum.Top, "village_story.tres: placement is stored");
    }

    private void TestDisplayText()
    {
        var subtitle = new Subtitle();
        SubtitleSegment sound = Segment(0.0, 1.0, "Applaus", SubtitleSegment.KindEnum.Sound);
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, sound), "[Applaus]", "GetDisplayText puts a sound in brackets");
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, sound, true), "[lb]Applaus[rb]", "GetDisplayText with bbcode escapes the brackets");
        SubtitleSegment music = Segment(0.0, 1.0, "Musik", SubtitleSegment.KindEnum.Music);
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, music), Note + " Musik " + Note, "GetDisplayText puts music between notes");
        SubtitleSegment narration = Segment(0.0, 1.0, "Es war einmal", SubtitleSegment.KindEnum.Narration);
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, narration, true), "[i]Es war einmal[/i]", "GetDisplayText with bbcode puts narration in italics");
        var translation = new Translation { Locale = TranslationServer.GetLocale() };
        translation.AddMessage("Hallo", "Hello", "greeting");
        TranslationDomain domain = TranslationServer.GetOrAddDomain("subtitles");
        domain.AddTranslation(translation);
        SubtitleSegment greeting = Segment(0.0, 1.0, "Hallo");
        greeting.TranslationContext = "greeting";
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, greeting), "Hallo", "without AutoTranslate the text stays");
        subtitle.AutoTranslate = true;
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, greeting), "Hello", "AutoTranslate translates with TranslationContext");
        domain.RemoveTranslation(translation);
    }

    private async Task TestSubtitlesChannel()
    {
        // Voices in English, subtitles in German: the subtitles channel has its own locale.
        var catalog = new TextLocalizationCatalog { Channel = "subtitles" };
        catalog.SetText("LINE_1", "en", "Where are we?");
        catalog.SetText("LINE_1", "de", "Wo sind wir?");
        catalog.SetText("LINE_1", "de", "Wo bin ich?", "alone");
        TextLocalizationServer.Singleton.AddCatalog(catalog);
        string locale = TranslationServer.GetLocale();
        TranslationServer.SetLocale("en");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var subtitle = new Subtitle { AutoTranslate = true };
        SubtitleSegment line = Segment(0.0, 1.0, "LINE_1");
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, line), "Where are we?", "subtitles follow the general locale");
        TranslationServer.SetLocaleFor("subtitles", "de");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "en", "the interface stays English");
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, line), "Wo sind wir?", "subtitles follow the subtitles channel");
        SubtitleSegment alone = Segment(0.0, 1.0, "LINE_1");
        alone.TranslationContext = "alone";
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, alone), "Wo bin ich?", "TranslationContext selects the context");
        ExpectEqual(Tr("LINE_1"), "LINE_1", "the key is not in the main domain");
        subtitle.AutoTranslate = false;
        ExpectEqual(SubtitleServer.GetDisplayText(subtitle, line), "LINE_1", "AutoTranslate off shows the key");
        TranslationServer.ClearLocaleFor("subtitles");
        TextLocalizationServer.Singleton.RemoveCatalog(catalog);
        TranslationServer.SetLocale(locale);
    }

    private void TestTranslatableStrings()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 1.0, "Hello"));
        SubtitleSegment spoken = Segment(0.0, 1.0, "Run!");
        spoken.TranslationContext = "chase";
        spoken.Speaker = "Anna";
        subtitle.AddSegment(spoken);
        subtitle.AddSegment(Segment(0.0, 1.0, ""));
        var strings = subtitle.GetTranslatableStrings();
        ExpectEqual(strings.Count, 2, "GetTranslatableStrings skips segments without text");
        ExpectEqual(string.Join("|", strings[0]), "Hello|||", "entry: text, empty context");
        ExpectEqual(string.Join("|", strings[1]), "Run!|chase||Speaker: Anna", "entry: text, context, speaker comment");
    }

    private void TestKeysSrt()
    {
        var subtitle = GD.Load<Subtitle>(Res + "line_keys.srt");
        Expect(subtitle != null, "line_keys.srt loads as Subtitle");
        Expect(!subtitle.AutoTranslate, "a loaded .srt starts with auto_translate off");
        var strings = subtitle.GetTranslatableStrings();
        ExpectEqual(strings.Count, 2, "line_keys.srt gives one entry per cue");
        ExpectEqual(strings[1][0], "LINE_2", "cue text of line_keys.srt");
    }

    private void TestOwnClock()
    {
        Subtitle subtitle = ThreeSegments();
        int id = SubtitleServer.Play(subtitle, default, 2.5);
        Expect(SubtitleServer.IsPlaying(id), "Play starts a track");
        ExpectEqual(Take(), "track_started, segment_started B, text_changed B", "Play emits TrackStarted and the visible segment");
        Expect(SubtitleServer.GetTrackIds().SequenceEqual([id]), "GetTrackIds lists the track");
        Expect(SubtitleServer.GetTrackSubtitle(id) == subtitle, "GetTrackSubtitle returns the subtitle");
        SubtitleServer.Seek(id, 3.5);
        ExpectEqual(Take(), "track_seeked 3.5, segment_started C, text_changed B|C", "Seek into an overlap");
        ExpectEqual(SubtitleServer.GetActiveSegments(id).Count, 2, "GetActiveSegments returns both segments");
        ExpectEqual(SubtitleServer.GetTrackText(id), "B\nC", "GetTrackText has one line per segment");
        SubtitleServer.Pause(id);
        Expect(SubtitleServer.IsPaused(id), "Pause pauses the track");
        SubtitleServer.Resume(id);
        ExpectEqual(Take(), "track_paused, track_resumed", "Pause and Resume emit their signals");
        SubtitleServer.Stop(id);
        ExpectEqual(Take(), "segment_ended B, segment_ended C, text_changed , track_canceled", "Stop ends the visible segments and cancels");
        Expect(!SubtitleServer.IsPlaying(id), "a stopped track is gone");
        SubtitleServer.Play(subtitle);
        SubtitleServer.Play(subtitle);
        SubtitleServer.StopAll();
        Expect(SubtitleServer.GetTrackIds().Length == 0, "StopAll stops every track");
        _events.Clear();
    }

    private void TestSettings()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 5.0, "Hello"));
        subtitle.AddSegment(Segment(0.0, 5.0, "Applause", SubtitleSegment.KindEnum.Sound));
        int id = SubtitleServer.Play(subtitle, default, 1.0);
        SubtitleServer.CaptionsEnabled = false;
        ExpectEqual(SubtitleServer.GetTrackText(id), "Hello", "CaptionsEnabled = false hides sounds and keeps speech");
        SubtitleServer.CaptionsEnabled = true;
        SubtitleServer.Enabled = false;
        ExpectEqual(SubtitleServer.GetTrackText(id), "", "Enabled = false hides every segment");
        SubtitleServer.Enabled = true;
        ExpectEqual(SubtitleServer.GetTrackText(id), "Hello\n[Applause]", "Enabled and CaptionsEnabled show everything again");
        SubtitleServer.Stop(id);
        _events.Clear();
    }

    private async Task TestTimeSource()
    {
        _clock = 0.5;
        int id = SubtitleServer.Play(ThreeSegments(), Callable.From(() => _clock));
        ExpectEqual(Take(), "track_started, segment_started A, text_changed A", "a time source starts at its time");
        _clock = 2.5;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(Take(), "segment_ended A, segment_started B, text_changed B", "the frame update follows the time source");
        ExpectEqual(SubtitleServer.GetTrackTime(id), 2.5, "GetTrackTime is the source time");
        _clock = 10.0;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(Take(), "segment_ended B, text_changed , track_finished", "past the end the track finishes");
        Expect(!SubtitleServer.IsPlaying(id), "a finished track is gone");
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

    /// <summary>Returns the logged signals as one line and clears the log.</summary>
    private string Take()
    {
        string result = string.Join(", ", _events);
        _events.Clear();
        return result;
    }
}
