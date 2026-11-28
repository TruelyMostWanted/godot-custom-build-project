using Godot;

namespace TruelyInteractive.SubtitleDisplay;

// The test project has a namespace TruelyInteractive.SubtitlePlayer too.
using SubtitlePlayer = Godot.SubtitlePlayer;

/// <summary>Minimal example of Subtitle Display: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // The player the label follows.
        var player = new SubtitlePlayer { Name = "SubtitlePlayer" };
        AddChild(player);

        // Adding a SubtitleLabel.
        var label = new SubtitleLabel();
        AddChild(label);
        label.Player = label.GetPathTo(GetNode("SubtitlePlayer"));
        label.FontSize = 36;
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide, Control.LayoutPresetMode.Minsize, 48);
        label.GrowVertical = Control.GrowDirection.Begin;

        // Speaker names and colors.
        label.SpeakerColors = new Godot.Collections.Dictionary<StringName, Color> { { "Anna", Colors.Gold }, { "Ben", Colors.SkyBlue } };
        label.ShowSpeakerNames = false;

        // Following the accessibility settings.
        GD.Print(label.GetDisplayFontSize());
        label.FollowAccessibilitySettings = false;

        // Captions.
        AccessibilitySettingsServer.SetCaptionsEnabled(false);
        SubtitleServer.CaptionsEnabled = false;
        GD.Print(label.GetSubtitleText());

        SubtitleServer.CaptionsEnabled = true;
        AccessibilitySettingsServer.SetCaptionsEnabled(true);
        GetTree().Quit();
    }
}
