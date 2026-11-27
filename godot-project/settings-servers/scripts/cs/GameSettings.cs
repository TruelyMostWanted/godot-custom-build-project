using System;
using Godot;

namespace TruelyInteractive.SettingsServers;

/// <summary>The "game" domain of the wiki guide. Registered by Example.cs (a game lists it in settings/startup/custom_servers).</summary>
public partial class GameSettings : SettingsServer
{
    [Signal] public delegate void DifficultyChangedEventHandler(int difficulty);

    public GameSettings()
    {
        BindSettingSingletonProperty("MaxFps", "Engine", "max_fps");
        BindSettingCallbacks("MusicVolume", Callable.From<float>(SetMusicVolume), Callable.From(GetMusicVolume));
    }

    public override StringName _GetDomain() => "game";

    public override Resource _CreateSettings() => new GameSettingsResourceCs();

    public override void _ValueApplied(StringName name, Variant value)
    {
        if (name == "Difficulty")
        {
            EmitSignal(SignalName.DifficultyChanged, value);
        }
    }

    public override int _GetFormatVersion() => 2;

    public override Godot.Collections.Dictionary _Migrate(int fromVersion, Godot.Collections.Dictionary data)
    {
        if (fromVersion < 2 && data.ContainsKey("volume")) // Renamed in version 2.
        {
            data["MusicVolume"] = data["volume"];
            data.Remove("volume");
        }
        return data;
    }

    private void SetMusicVolume(float value)
    {
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(value));
    }

    private float GetMusicVolume()
    {
        return Mathf.DbToLinear(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")));
    }
}
