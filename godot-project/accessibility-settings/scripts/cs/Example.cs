using Godot;

namespace TruelyInteractive.AccessibilitySettings;

/// <summary>Minimal example of Accessibility Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set, the engine applies these to the root window at once.
        AccessibilitySettingsServer.SetUiScale(1.25f);
        AccessibilitySettingsServer.SetFontSizeFactor(1.5f);
        AccessibilitySettingsServer.SetColorScheme(AccessibilitySettingsServer.ColorScheme.HighContrastDark);
        // Using the API: your game reads this one.
        AccessibilitySettingsServer.SetReduceMotion(true);

        // Using the API: get the pending values, and the root window that uses them.
        GD.Print(AccessibilitySettingsServer.GetUiScale());
        GD.Print(GetTree().Root.ContentScaleFactor);
        GD.Print(AccessibilitySettingsServer.GetAnimationDuration(0.4f));

        // Using the API: apply, save and load (methods of the base class go through Singleton).
        AccessibilitySettingsServer.Singleton.Apply();
        AccessibilitySettingsServer.Singleton.Save();
        AccessibilitySettingsServer.Singleton.Load();
        AccessibilitySettingsServer.Singleton.Apply();

        // A UI scale slider: needs a player who drags it, so it is not run here.

        // Leave no player file behind.
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
        AccessibilitySettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
