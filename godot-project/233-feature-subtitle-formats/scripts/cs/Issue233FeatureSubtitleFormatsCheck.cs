using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue233FeatureSubtitleFormats;

/// <summary>C# checks of 233-feature-subtitle-formats. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue233FeatureSubtitleFormatsCheck : FeatureCheckCs
{
    private const string Dir = "res://233-feature-subtitle-formats/resources/";
    private static readonly string[] Extensions = { "srt", "vtt", "ttml", "dfxp", "ass", "ssa", "sbv" };

    protected override string CheckName => "233-feature-subtitle-formats";

    protected override Task RunChecks()
    {
        CheckEnum();
        CheckWebVtt();
        CheckTtml();
        CheckAss();
        CheckSbv();
        CheckRoundTrips();
        return Task.CompletedTask;
    }

    private void CheckEnum()
    {
        ExpectEqual((long)Subtitle.Format.Max, 5L, "five formats");
        string[] extensions = ResourceLoader.GetRecognizedExtensionsForType("Subtitle");
        foreach (string extension in Extensions)
        {
            Expect(System.Array.IndexOf(extensions, extension) >= 0, $"ResourceLoader knows .{extension}");
        }
    }

    private void CheckWebVtt()
    {
        var subtitle = GD.Load<Subtitle>(Dir + "interview.vtt");
        Expect(subtitle != null && subtitle.GetSegmentCount() == 4, "interview.vtt: four cues (NOTE and STYLE skipped)");
        if (subtitle == null || subtitle.GetSegmentCount() != 4)
        {
            return;
        }
        ExpectEqual(subtitle.GetSegment(0).Speaker.ToString(), "Host", "<v Host> is the speaker");
        ExpectEqual(subtitle.GetSegment(0).Text, "Welcome to the show\n& thanks for coming.", "entities decoded, two lines");
        ExpectEqual(subtitle.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "[Applause] is a sound");
        ExpectEqual(subtitle.GetSegment(1).Placement, SubtitleSegment.PlacementEnum.Top, "line:0 is the top");
        Expect(subtitle.GetSegment(2).OffScreen && subtitle.GetSegment(2).Speaker == "Guest", "voice and italics");
        ExpectEqual(subtitle.GetSegment(3).Kind, SubtitleSegment.KindEnum.Music, "music notes");
    }

    private void CheckTtml()
    {
        var subtitle = GD.Load<Subtitle>(Dir + "broadcast.ttml");
        Expect(subtitle != null && subtitle.GetSegmentCount() == 4, "broadcast.ttml: four paragraphs");
        if (subtitle == null || subtitle.GetSegmentCount() != 4)
        {
            return;
        }
        ExpectEqual(subtitle.Language, "de", "xml:lang is the language");
        ExpectEqual(subtitle.GetSegment(0).Text, "Guten Abend\nund willkommen.", "<br/> and whitespace");
        Expect(Mathf.IsEqualApprox(subtitle.GetSegment(1).EndTime, 4.48), "frames at 25 fps");
        Expect(Mathf.IsEqualApprox(subtitle.GetSegment(2).EndTime, 6.5), "offset and dur");
        Expect(subtitle.GetSegment(2).OffScreen, "italic span is off-screen");
        ExpectEqual(subtitle.GetSegment(3).Speaker.ToString(), "WACHE", "speaker in capitals");
        ExpectEqual(subtitle.GetSegment(3).Text, "Halt & stehen bleiben!", "entity decoded");
    }

    private void CheckAss()
    {
        var subtitle = GD.Load<Subtitle>(Dir + "fansub.ass");
        Expect(subtitle != null && subtitle.GetSegmentCount() == 4, "fansub.ass: four dialog lines (comment skipped)");
        if (subtitle == null || subtitle.GetSegmentCount() != 4)
        {
            return;
        }
        ExpectEqual(subtitle.GetSegment(0).Speaker.ToString(), "Guard", "Name field is the speaker");
        ExpectEqual(subtitle.GetSegment(0).Text, "Who is there,\nfriend?", "\\N is a line break, commas stay");
        ExpectEqual(subtitle.GetSegment(1).Placement, SubtitleSegment.PlacementEnum.Top, "{\\an8} is the top");
        Expect(subtitle.GetSegment(2).OffScreen, "{\\i1} is italics");
        ExpectEqual(subtitle.GetSegment(3).Text, "Styled text", "override tags dropped, \\h is a space");
    }

    private void CheckSbv()
    {
        var subtitle = GD.Load<Subtitle>(Dir + "youtube.sbv");
        Expect(subtitle != null && subtitle.GetSegmentCount() == 3, "youtube.sbv: three cues");
        if (subtitle == null || subtitle.GetSegmentCount() != 3)
        {
            return;
        }
        ExpectEqual(subtitle.GetSegment(1).Kind, SubtitleSegment.KindEnum.Sound, "[Laughs] is a sound");
        ExpectEqual(subtitle.GetSegment(2).Speaker.ToString(), "HOST", "HOST: is the speaker");
    }

    private void CheckRoundTrips()
    {
        var source = GD.Load<Subtitle>(Dir + "interview.vtt");
        if (source == null)
        {
            return;
        }
        foreach (string extension in Extensions)
        {
            string path = "user://check_233_cs." + extension;
            bool saved = ResourceSaver.Save(source, path) == Error.Ok;
            var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as Subtitle;
            bool ok = saved && loaded != null && loaded.GetSegmentCount() == source.GetSegmentCount();
            if (ok)
            {
                for (int i = 0; i < source.GetSegmentCount(); i++)
                {
                    SubtitleSegment a = source.GetSegment(i);
                    SubtitleSegment b = loaded.GetSegment(i);
                    ok = ok && Mathf.IsEqualApprox(a.StartTime, b.StartTime) && Mathf.IsEqualApprox(a.EndTime, b.EndTime) && a.Text == b.Text && a.Kind == b.Kind;
                }
            }
            Expect(ok, $"round trip through .{extension} keeps times, texts and kinds");
        }
        var copy = new Subtitle();
        ExpectEqual(copy.LoadFromText(source.ToText(Subtitle.Format.Ass), Subtitle.Format.Ass), Error.Ok, "to_text() and load_from_text() with FORMAT_ASS");
        ExpectEqual(copy.GetSegment(0).Speaker.ToString(), "Host", "ASS keeps the speaker as written");
    }
}
