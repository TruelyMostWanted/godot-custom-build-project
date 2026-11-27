using Godot;

namespace TruelyInteractive.AudioBusSettingsGuide;

/// <summary>Minimal example of Audio Bus Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        // Set: applied to the AudioServer at once (APPLY_MODE_IMMEDIATE, the default).
        AudioBusSettingsServer.SetBusVolumeDb("Music", -12.0f);
        AudioBusSettingsServer.SetBusVolumeLinear("Sfx", 0.5f);
        AudioBusSettingsServer.SetBusMute("Sfx", true);

        // Get: the pending values.
        GD.Print(AudioBusSettingsServer.GetBusVolumeDb("Music")); // -12
        GD.Print(AudioBusSettingsServer.IsBusMute("Sfx")); // True

        // Methods of the base class SettingsServer go through Singleton.
        // In APPLY_MODE_MANUAL, changes wait for Apply().
        AudioBusSettingsServer.Singleton.Apply();
        // Autosave writes the file at the end of the frame; Save() writes it now.
        AudioBusSettingsServer.Singleton.Save();
        // Load() reads the file into the pending values; Apply() pushes them to the AudioServer.
        AudioBusSettingsServer.Singleton.Load();
        AudioBusSettingsServer.Singleton.Apply();

        // Presets (the preset is a resource of this folder)
        var nightMode = GD.Load<AudioBusListSettings>("res://audio-bus-settings/resources/night_mode_preset.tres");
        AudioBusSettingsServer.ApplyPreset(nightMode);
        GD.Print(AudioBusSettingsServer.GetBusVolumeDb("Music")); // -18
        GD.Print(AudioBusSettingsServer.GetCurrentPreset() == nightMode); // True
        AudioBusSettingsServer.SetBusVolumeDb("Music", -10.0f);
        GD.Print(AudioBusSettingsServer.IsCustom()); // True
        GD.Print(AudioBusSettingsServer.MatchesPreset(nightMode)); // False

        // A volume slider
        var slider = new VolumeSlider();
        slider.Bus = "Music";
        AddChild(slider);
        GD.Print("Slider: ", slider.Value);
        AudioBusSettingsServer.SetBusVolumeLinear("Music", 0.5f);
        GD.Print("Slider after the change: ", slider.Value);

        AudioBusSettingsServer.Singleton.ResetToDefaults();
        AudioBusSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
