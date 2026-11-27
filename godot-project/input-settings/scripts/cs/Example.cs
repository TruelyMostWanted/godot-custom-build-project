using Godot;

namespace TruelyInteractive.InputSettingsGuide;

/// <summary>Minimal example of Input Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        // Set: sensitivity and inversion per device, and actions the player toggles instead of holding.
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Mouse, 1.5f);
        InputSettingsServer.SetYInverted(InputSettingsServer.Device.Mouse, true);
        InputSettingsServer.SetToggleActions(["aim"]);
        InputSettingsServer.SetVibrationStrength(0.5f);

        // Get: the pending values.
        GD.Print(InputSettingsServer.GetSensitivity(InputSettingsServer.Device.Mouse)); // 1.5
        GD.Print(InputSettingsServer.IsYInverted(InputSettingsServer.Device.Mouse)); // True

        // Use: your input code passes its input through the helpers.
        GD.Print(InputSettingsServer.ApplyToMouseMotion(new Vector2(10, 4))); // (15, -6)

        // Methods of the base class SettingsServer go through Singleton.
        // In APPLY_MODE_MANUAL, changes wait for Apply().
        InputSettingsServer.Singleton.Apply();
        // Autosave writes the file at the end of the frame; Save() writes it now.
        InputSettingsServer.Singleton.Save();
        // Load() reads the file into the pending values; Apply() makes them the values in use.
        InputSettingsServer.Singleton.Load();
        InputSettingsServer.Singleton.Apply();

        // A mouse sensitivity slider
        var slider = new SensitivitySlider();
        AddChild(slider);
        GD.Print("Slider: ", slider.Value);
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Mouse, 2.0f);
        InputSettingsServer.Singleton.ResetToDefaults();
        GD.Print("Slider after the reset: ", slider.Value);

        InputSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
