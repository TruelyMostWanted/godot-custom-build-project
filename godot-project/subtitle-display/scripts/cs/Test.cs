using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.SubtitleDisplay;

// The test project has a namespace TruelyInteractive.SubtitlePlayer too.
using SubtitlePlayer = Godot.SubtitlePlayer;

/// <summary>Full API test of Subtitle Display (SubtitleLabel): defaults, speakers, accessibility, captions, reparenting. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    protected override async Task RunTests()
    {
        TestDefaults();
        TestPlayerAndSpeakers();
        TestAccessibility();
        await TestChanges();
    }

    private static SubtitleSegment Segment(double start, double end, string text, string speaker = "", SubtitleSegment.KindEnum kind = SubtitleSegment.KindEnum.Dialog)
    {
        return new SubtitleSegment { StartTime = start, EndTime = end, Text = text, Speaker = speaker, Kind = kind };
    }

    // 0-1 "Hello" (Anna), 2-4 "Applause" (sound), 3-4 "Run!" (Ben).
    private (SubtitlePlayer Player, SubtitleLabel Label) MakePair()
    {
        var subtitle = new Subtitle();
        subtitle.AddSegment(Segment(0.0, 1.0, "Hello", "Anna"));
        subtitle.AddSegment(Segment(2.0, 4.0, "Applause", "", SubtitleSegment.KindEnum.Sound));
        subtitle.AddSegment(Segment(3.0, 4.0, "Run!", "Ben"));
        var player = new SubtitlePlayer { Subtitle = subtitle };
        AddChild(player);
        var label = new SubtitleLabel();
        AddChild(label);
        label.Player = label.GetPathTo(player);
        return (player, label);
    }

    private static string Sized(SubtitleLabel label, string text)
    {
        return $"[font_size={label.GetDisplayFontSize()}]{text}[/font_size]";
    }

    private void TestDefaults()
    {
        var label = new SubtitleLabel();
        Expect(label.IsClass("RichTextLabel"), "SubtitleLabel is a RichTextLabel");
        Expect(label.BbcodeEnabled, "bbcode_enabled is on");
        Expect(label.FitContent, "fit_content is on");
        ExpectEqual(label.HorizontalAlignment, HorizontalAlignment.Center, "centered");
        ExpectEqual(label.MouseFilter, Control.MouseFilterEnum.Ignore, "ignores the mouse");
        ExpectEqual(label.FontSize, 32, "font_size is 32");
        Expect(label.ShowSpeakerNames, "show_speaker_names is on");
        Expect(label.FollowAccessibilitySettings, "follow_accessibility_settings is on");
        ExpectEqual(label.SpeakerColors.Count, 0, "speaker_colors is empty");
        ExpectEqual(label.AutoTranslateMode, Node.AutoTranslateModeEnum.Disabled, "translated in the subtitles domain only");
        ExpectEqual(label.GetSubtitleText(), "", "no player: no text");
        label.Free();
    }

    private void TestPlayerAndSpeakers()
    {
        var (player, label) = MakePair();
        label.FollowAccessibilitySettings = false;
        player.Play(0.5);
        ExpectEqual(label.Text, Sized(label, "Anna: Hello"), "shows the line with the speaker");
        Expect(label.Visible, "visible while a line is shown");
        label.SpeakerColors = new Godot.Collections.Dictionary<StringName, Color> { { "Anna", new Color(1, 0, 0) } };
        ExpectEqual(label.Text, Sized(label, "[color=#ff0000ff]Anna: Hello[/color]"), "colors the speaker");
        label.ShowSpeakerNames = false;
        ExpectEqual(label.Text, Sized(label, "[color=#ff0000ff]Hello[/color]"), "without the speaker name");
        label.ShowSpeakerNames = true;
        player.Seek(1.5);
        ExpectEqual(label.Text, "", "no line between segments");
        Expect(!label.Visible, "hidden while no line is shown");
        player.Seek(3.5);
        ExpectEqual(label.Text, Sized(label, "[lb]Applause[rb]\nBen: Run!"), "two lines, the caption without speaker");
        SubtitleServer.CaptionsEnabled = false;
        ExpectEqual(label.Text, Sized(label, "Ben: Run!"), "captions_enabled off drops the caption at once");
        SubtitleServer.CaptionsEnabled = true;
        SubtitleServer.Enabled = false;
        ExpectEqual(label.Text, "", "SubtitleServer.enabled off hides the line at once");
        SubtitleServer.Enabled = true;
        ExpectEqual(label.Text, Sized(label, "[lb]Applause[rb]\nBen: Run!"), "SubtitleServer.enabled on shows it again");
        label.FontSize = 20;
        ExpectEqual(label.GetDisplayFontSize(), 20, "font_size without the accessibility scale");
        player.Stop();
        ExpectEqual(label.Text, "", "stop clears the text");
        label.QueueFree();
        player.QueueFree();
    }

    private void TestAccessibility()
    {
        bool enabled = AccessibilitySettingsServer.IsSubtitlesEnabled();
        float scale = AccessibilitySettingsServer.GetSubtitleScale();
        AccessibilitySettingsServer.SetSubtitlesEnabled(true);
        AccessibilitySettingsServer.SetSubtitleScale(1.0f);
        var (player, label) = MakePair();
        player.Play(0.5);
        AccessibilitySettingsServer.SetSubtitleScale(2.0f);
        ExpectEqual(label.GetDisplayFontSize(), AccessibilitySettingsServer.ScaleSubtitleFontSize(32), "font size follows subtitle_scale");
        ExpectEqual(label.Text, Sized(label, "Anna: Hello"), "the text updates on subtitle_scale_changed");
        AccessibilitySettingsServer.SetSubtitlesEnabled(false);
        ExpectEqual(label.Text, "", "subtitles_enabled off hides the text");
        label.FollowAccessibilitySettings = false;
        ExpectEqual(label.Text, "[font_size=32]Anna: Hello[/font_size]", "not following: shown, unscaled");
        label.FollowAccessibilitySettings = true;
        AccessibilitySettingsServer.SetSubtitlesEnabled(true);
        ExpectEqual(label.Text, Sized(label, "Anna: Hello"), "subtitles_enabled on shows it again");
        label.QueueFree();
        player.QueueFree();
        AccessibilitySettingsServer.SetSubtitlesEnabled(enabled);
        AccessibilitySettingsServer.SetSubtitleScale(scale);
    }

    private async Task TestChanges()
    {
        bool enabled = AccessibilitySettingsServer.IsSubtitlesEnabled();
        bool captions = AccessibilitySettingsServer.IsCaptionsEnabled();
        AccessibilitySettingsServer.SetSubtitlesEnabled(true);
        AccessibilitySettingsServer.SetCaptionsEnabled(true);
        var (player, label) = MakePair();
        player.Play(3.5);
        ExpectEqual(label.Text, Sized(label, "[lb]Applause[rb]\nBen: Run!"), "caption and speech");
        AccessibilitySettingsServer.SetCaptionsEnabled(false);
        ExpectEqual(label.Text, Sized(label, "Ben: Run!"), "AccessibilitySettings.captions_enabled off drops the caption");
        AccessibilitySettingsServer.SetCaptionsEnabled(true);
        ExpectEqual(label.Text, Sized(label, "[lb]Applause[rb]\nBen: Run!"), "captions_enabled on shows it again");
        int readies = 0;
        label.Ready += () => readies++;
        RemoveChild(label);
        AddChild(label);
        ExpectEqual(readies, 0, "reparenting does not run _ready() again");
        ExpectEqual(label.Text, Sized(label, "[lb]Applause[rb]\nBen: Run!"), "still follows the player after reparenting");
        player.Free();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(label.Text, "", "a freed player clears the line");
        Expect(!label.Visible, "hidden after the player is freed");
        label.QueueFree();
        AccessibilitySettingsServer.SetSubtitlesEnabled(enabled);
        AccessibilitySettingsServer.SetCaptionsEnabled(captions);
    }
}
