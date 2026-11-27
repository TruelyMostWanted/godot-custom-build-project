using Godot;

namespace TruelyInteractive.AudioDeviceSettingsGuide;

/// <summary>Minimal example of Audio Device Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        // Only some drivers can switch the device; elsewhere it stays "Default".
        if (AudioDeviceSettingsServer.IsDeviceSelectionSupported())
        {
            // Set: a name from AudioServer.GetOutputDeviceList(); "Default" follows the system.
            AudioDeviceSettingsServer.SetOutputDevice(AudioServer.GetOutputDeviceList()[0]);
            AudioDeviceSettingsServer.SetInputDevice("Default");
        }

        // Get: the pending values.
        GD.Print(AudioDeviceSettingsServer.GetOutputDevice()); // Default
        GD.Print(AudioDeviceSettingsServer.GetInputDevice()); // Default

        // Methods of the base class SettingsServer go through Singleton.
        // In APPLY_MODE_MANUAL, changes wait for Apply().
        AudioDeviceSettingsServer.Singleton.Apply();
        // Autosave writes the file at the end of the frame; Save() writes it now.
        AudioDeviceSettingsServer.Singleton.Save();
        // Load() reads the file into the pending values; Apply() pushes them to the AudioServer.
        AudioDeviceSettingsServer.Singleton.Load();
        AudioDeviceSettingsServer.Singleton.Apply();

        // An output device button
        var button = new OutputDeviceButton();
        AddChild(button);
        GD.Print("Items: ", button.ItemCount);
        GD.Print("Disabled: ", button.Disabled);

        GetTree().Quit();
    }
}
