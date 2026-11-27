using Godot;

namespace TruelyInteractive.WindowSettings;

/// <summary>Minimal example of Window Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API: set the values, mode and size wait for ConfirmChanges().
        WindowSettingsServer.SetWindowMode(DisplayServer.WindowMode.Fullscreen);
        WindowSettingsServer.SetWindowSize(new Vector2I(1920, 1080));
        WindowSettingsServer.SetVsyncMode(DisplayServer.VSyncMode.Adaptive);
        WindowSettingsServer.SetContentScaleFactor(1.25f);

        // Using the API: get the pending values.
        GD.Print(WindowSettingsServer.GetWindowMode());
        GD.Print(WindowSettingsServer.GetWindowSize());

        // Using the API: keep, apply, save and load (methods of the base class go through Singleton).
        WindowSettingsServer.Singleton.ConfirmChanges();
        WindowSettingsServer.Singleton.Apply();
        WindowSettingsServer.Singleton.Save();
        WindowSettingsServer.Singleton.Load();
        WindowSettingsServer.Singleton.Apply();

        // A window mode button: needs a window and a player who clicks, so it is not run here.

        // Leave no player file behind.
        WindowSettingsServer.Singleton.ResetToDefaults();
        WindowSettingsServer.Singleton.ConfirmChanges();
        WindowSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
