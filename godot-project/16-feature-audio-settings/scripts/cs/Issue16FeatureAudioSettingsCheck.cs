using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue16FeatureAudioSettings;

/// <summary>
/// C# checks of 16-feature-audio-settings. Check the same things as scripts/gd/check.gd.
/// The project's bus layout (resources/bus_layout.tres) has the buses Master, Music (-6 dB) and Sfx.
/// Typed methods are called on the generated static classes, the methods of the base classes
/// through <c>Singleton</c>. The restart check starts the project a second time (desktop only).
/// </summary>
public partial class Issue16FeatureAudioSettingsCheck : FeatureCheckCs
{
    private const string Dir = "user://16-feature-audio-settings-cs";
    private const string ProbeScene = "res://16-feature-audio-settings/scenes/restart_probe.tscn";
    private const string BusesFile = "user://settings/audio_bus.ini";
    private const string DevicesFile = "user://settings/audio_device.ini";
    private const string NightPreset = "res://16-feature-audio-settings/resources/night_mode_preset.tres";

    protected override string CheckName => "16-feature-audio-settings";

    protected override async Task RunChecks()
    {
        await CheckTypedSignals();

        DirAccess.MakeDirRecursiveAbsolute(Dir);
        Expect(Engine.HasSingleton("AudioBusSettingsServer"), "AudioBusSettingsServer is a registered singleton");
        Expect(Engine.HasSingleton("AudioDeviceSettingsServer"), "AudioDeviceSettingsServer is a registered singleton");
        // First C++ domain singleton: the generated static class and the instance type.
        Expect(AudioBusSettingsServer.Singleton is AudioBusSettingsServerInstance, "AudioBusSettingsServer.Singleton is the instance type");
        Expect(AudioBusSettingsServer.Singleton is ListSettingsServer, "AudioBusSettingsServerInstance derives from ListSettingsServer");
        Expect(AudioDeviceSettingsServer.Singleton is AudioDeviceSettingsServerInstance, "AudioDeviceSettingsServer.Singleton is the instance type");
        Expect(AudioDeviceSettingsServer.Singleton is SettingsServer, "AudioDeviceSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("audio_bus") == AudioBusSettingsServer.Singleton, "#185: the bus domain is registered as \"audio_bus\"");
        Expect(SettingsServerManager.GetServer("audio_device") == AudioDeviceSettingsServer.Singleton, "#185: the device domain is registered as \"audio_device\"");
        Expect(SettingsServerManager.GetServer("audio_buses") == null && SettingsServerManager.GetServer("audio") == null, "#185: the old domain names are gone");
        Expect(ProjectSettings.HasSetting("settings/audio_bus/player_buses"), "#185: project setting settings/audio_bus/player_buses");
        Expect(ProjectSettings.GetSetting("settings/audio_bus/store_volume").AsBool() && ProjectSettings.GetSetting("settings/audio_bus/store_mute").AsBool() && !ProjectSettings.GetSetting("settings/audio_bus/store_solo").AsBool(), "#185: store_volume and store_mute on, store_solo off");
        Expect(!ProjectSettings.HasSetting("settings/audio/create_missing_buses"), "#185: create_missing_buses is gone");
        Expect(AudioBusSettingsServer.StoreVolume && AudioBusSettingsServer.StoreMute && !AudioBusSettingsServer.StoreSolo && AudioBusSettingsServer.PlayerBuses.Length == 0, "#185: the server properties follow the project settings");
        Expect(AudioBusSettingsServer.Singleton.IsStarted(), "the bus domain was started before the main scene");
        Expect(AudioDeviceSettingsServer.Singleton.IsStarted(), "the device domain was started before the main scene");
        ExpectEqual(AudioBusSettingsServer.Singleton.GetFilePath(), BusesFile, "bus file path");
        ExpectEqual(AudioDeviceSettingsServer.Singleton.GetFilePath(), DevicesFile, "device file path");

        CheckBuses();
        CheckManualMode();
        CheckCsv();
        CheckMissingBus();
        CheckStoredValues();
        CheckLayoutChange();
        CheckPresets();
        CheckDevices();
        CheckRestart();

        // Leave no player files behind for later runs.
        AudioBusSettingsServer.Singleton.ResetToDefaults();
        AudioDeviceSettingsServer.Singleton.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(BusesFile);
        DirAccess.RemoveAbsolute(DevicesFile);
        foreach (string file in DirAccess.GetFilesAt(Dir))
        {
            DirAccess.RemoveAbsolute(Dir.PathJoin(file));
        }
        DirAccess.RemoveAbsolute(Dir);
    }

    private static float BusVolumeDb(StringName bus) => AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(bus));

    private void CheckBuses()
    {
        var defaults = AudioBusSettingsServer.Singleton.GetDefaultValue("buses").AsGodotArray();
        ExpectEqual(defaults.Count, 3, "defaults: one entry per bus of the layout");
        Expect(defaults.Count == 3 && Mathf.IsEqualApprox(defaults[1].As<AudioBusSettings>().VolumeDb, -6.0f), "defaults come from the bus layout");
        ExpectEqual(string.Join(",", AudioBusSettingsServer.GetBusNames()), "Master,Music,Sfx", "buses in layout order");
        Expect(AudioBusSettingsServer.HasBus("Music"), "HasBus() for a bus of the layout");
        Expect(!AudioBusSettingsServer.HasBus("Voice"), "HasBus() for an unknown bus");

        AudioBusSettingsServer.SetBusVolumeDb("Music", -12.0f);
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -12.0f), "SetBusVolumeDb() reaches AudioServer at once");
        Expect(Mathf.IsEqualApprox(AudioBusSettingsServer.GetBusVolumeDb("Music"), -12.0f), "GetBusVolumeDb()");
        AudioBusSettingsServer.SetBusVolumeLinear("Sfx", 0.5f);
        // The dB value is snapped to 0.1 dB, so 50 % comes back as 0.501.
        Expect(Math.Abs(AudioBusSettingsServer.GetBusVolumeLinear("Sfx") - 0.5f) < 0.01f, "linear volume (50 %)");
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Sfx"), -6.0f), "linear volume is stored in dB, snapped to 0.1 dB (#189)");
        AudioBusSettingsServer.SetBusMute("Music", true);
        Expect(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "SetBusMute() reaches AudioServer");
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        Expect(!AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "#185: SetBusSolo() does nothing while solo is not stored (a warning is printed)");
        AudioBusSettingsServer.StoreSolo = true;
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        Expect(AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "SetBusSolo() reaches AudioServer with StoreSolo");
        AudioBusSettingsServer.SetBusVolumeDb("Music", 100.0f);
        Expect(Mathf.IsEqualApprox(AudioBusSettingsServer.GetBusVolumeDb("Music"), 24.0f), "volume is clamped to 24 dB");

        AudioBusSettingsServer.Singleton.ResetToDefaults();
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -6.0f), "ResetToDefaults() gives back the layout volume");
        Expect(!AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "ResetToDefaults() unmutes");
        Expect(!AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "ResetToDefaults() clears solo");
        AudioBusSettingsServer.StoreSolo = false;
    }

    private void CheckManualMode()
    {
        var server = AudioBusSettingsServer.Singleton;
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        AudioBusSettingsServer.SetBusVolumeDb("Music", -30.0f);
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -6.0f), "manual mode: not applied yet");
        Expect(server.IsDirty(), "manual mode: dirty");
        server.Revert();
        Expect(Mathf.IsEqualApprox(AudioBusSettingsServer.GetBusVolumeDb("Music"), -6.0f), "Revert() gives back the applied volume");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -30.0f);
        ExpectEqual(server.Apply(), Error.Ok, "Apply() in manual mode");
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -30.0f), "Apply() reaches AudioServer");
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        server.ResetToDefaults();
    }

    private void CheckCsv()
    {
        var server = AudioBusSettingsServer.Singleton;
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Csv);
        ExpectEqual(server.GetFilePath(), "user://settings/audio_bus.csv", "the bus list can be stored as CSV");
        ExpectEqual(AudioDeviceSettingsServer.Singleton.GetFilePath(), DevicesFile, "the devices stay INI (a warning is printed once)");
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Ini);

        string path = Dir.PathJoin("buses.csv");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -20.0f);
        AudioBusSettingsServer.SetBusMute("Sfx", true);
        server.FilePath = path;
        ExpectEqual(server.Save(), Error.Ok, "Save() as CSV");
        int master = 0, music = 0, sfx = 0;
        foreach (string line in FileAccess.GetFileAsString(path).Split('\n'))
        {
            master += line.Contains("Master") ? 1 : 0;
            music += line.Contains("Music") ? 1 : 0;
            sfx += line.Contains("Sfx") ? 1 : 0;
        }
        Expect(master == 1 && music == 1 && sfx == 1, "CSV: one row per bus");
        string header = FileAccess.GetFileAsString(path).Split('\n')[0];
        Expect(header.Contains("volume_db") && header.Contains("mute") && !header.Contains("solo"), "#185: CSV columns are the stored values only: " + header);

        AudioBusSettingsServer.SetBusVolumeDb("Music", -3.0f);
        AudioBusSettingsServer.SetBusMute("Sfx", false);
        ExpectEqual(server.Load(), Error.Ok, "Load() reads the CSV");
        Expect(Mathf.IsEqualApprox(AudioBusSettingsServer.GetBusVolumeDb("Music"), -20.0f), "CSV: volume loaded");
        Expect(AudioBusSettingsServer.IsBusMute("Sfx"), "CSV: mute loaded");
        server.Apply();
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -20.0f), "CSV: loaded volume applied");
        server.FilePath = "";
        server.ResetToDefaults();
    }

    private void CheckMissingBus()
    {
        var server = AudioBusSettingsServer.Singleton;
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        server.AddEntry(new AudioBusSettings { BusName = "Voice", VolumeDb = -9.0f });
        server.Apply();
        ExpectEqual(AudioServer.GetBusIndex("Voice"), -1, "a bus missing in the layout is skipped (a warning is printed)");
        int index = server.FindEntry("Voice");
        Expect(index >= 0, "the entry of a missing bus is kept");

        ExpectEqual(AudioServer.GetBusCount(), 3, "#185: no bus is created; the layout builds the buses");
        server.RemoveEntry(index);
        server.Apply();
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
    }

    // Issue #122: a new bus layout at runtime resets the bus values; the stored values are applied again.
    private void CheckLayoutChange()
    {
        var server = AudioBusSettingsServer.Singleton;
        AudioBusLayout gameLayout = AudioServer.GenerateBusLayout();
        AudioBusSettingsServer.SetBusVolumeDb("Music", -21.0f);
        AudioBusSettingsServer.SetBusMute("Music", true);
        AudioBusSettingsServer.StoreSolo = true;
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        AudioServer.SetBusLayout(gameLayout);
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -21.0f), "#122: SetBusLayout() keeps the stored volume");
        Expect(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "#122: SetBusLayout() keeps the stored mute");
        Expect(AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "#122: SetBusLayout() keeps the stored solo");
        Expect(!server.IsDirty(), "#122: the re-apply changes no setting");

        // #185: player buses. Sfx is not a player bus any more: a new layout keeps its own Sfx mix.
        AudioBusSettingsServer.PlayerBuses = ["Music"];
        int sfx = AudioServer.GetBusIndex("Sfx");
        AudioServer.SetBusVolumeDb(sfx, -9.0f);
        AudioServer.SetBusSolo(sfx, false);
        AudioBusLayout quietLayout = AudioServer.GenerateBusLayout();
        AudioServer.SetBusLayout(quietLayout);
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -21.0f), "#185: the player bus gets the player value after SetBusLayout()");
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Sfx"), -9.0f), "#185: a bus that is not a player bus keeps the value of the new layout");
        Expect(!AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "#185: the solo of a non-player bus is not pushed again");
        AudioBusSettingsServer.SetBusVolumeDb("Sfx", -1.0f);
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Sfx"), -9.0f), "#185: SetBusVolumeDb() on a non-player bus does nothing (a warning is printed)");
        AudioBusSettingsServer.PlayerBuses = [];
        AudioServer.SetBusLayout(gameLayout);

        // In manual mode, the applied values come back, not the pending ones.
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        AudioBusSettingsServer.SetBusVolumeDb("Music", -40.0f);
        AudioServer.SetBusLayout(gameLayout);
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -21.0f), "#122: manual mode: the applied volume comes back, the pending one waits");
        server.Revert();
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        server.ResetToDefaults();
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -6.0f), "#122: ResetToDefaults() after the layout change");
        Expect(!AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "#122: ResetToDefaults() clears the stored solo");
        AudioBusSettingsServer.StoreSolo = false;
    }

    // Issue #185: only the stored values are written; a file with values that are no longer stored is read without them,
    // stored values missing in the file come from the layout.
    private void CheckStoredValues()
    {
        var server = AudioBusSettingsServer.Singleton;
        string path = Dir.PathJoin("stored_values.ini");
        server.FilePath = path;
        AudioBusSettingsServer.SetBusVolumeDb("Music", -14.0f);
        AudioBusSettingsServer.SetBusMute("Music", true);
        ExpectEqual(server.Save(), Error.Ok, "#185: Save() with the default store settings");
        string text = FileAccess.GetFileAsString(path);
        Expect(text.Contains("volume_db") && text.Contains("mute") && !text.Contains("solo"), "#185: the file holds volume and mute, no solo");

        string csvPath = Dir.PathJoin("hand_edited.csv");
        using (var file = FileAccess.Open(csvPath, FileAccess.ModeFlags.Write))
        {
            file.StoreString("bus_name,volume_db,solo\nMusic,-17,true\n");
        }
        server.FilePath = csvPath;
        ExpectEqual(server.Load(), Error.Ok, "#185: a file with an unstored value loads");
        Expect(Mathf.IsEqualApprox(AudioBusSettingsServer.GetBusVolumeDb("Music"), -17.0f), "#185: the stored volume is read");
        Expect(!AudioBusSettingsServer.IsBusSolo("Music"), "#185: the solo of the file is ignored");
        Expect(!AudioBusSettingsServer.IsBusMute("Music"), "#185: the mute missing in the file comes from the layout");
        ExpectEqual(string.Join(",", AudioBusSettingsServer.GetBusNames()), "Master,Music,Sfx", "#185: buses missing in the file come from the layout");
        server.Apply();
        server.FilePath = "";
        server.ResetToDefaults();
    }

    // Issue #185: an AudioBusListSettings in res:// is a preset; buses it does not list stay unchanged.
    private void CheckPresets()
    {
        var preset = GD.Load<AudioBusListSettings>(NightPreset);
        Expect(preset != null && preset.Buses.Count == 1, "#185: the preset resource loads");
        if (preset == null)
        {
            return;
        }
        AudioBusSettingsServer.SetBusVolumeDb("Sfx", -5.0f);
        Expect(AudioBusSettingsServer.IsCustom(), "#185: no preset applied: Custom");
        ExpectEqual(AudioBusSettingsServer.ApplyPreset(preset), Error.Ok, "#185: ApplyPreset()");
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -18.0f), "#185: the preset volume reaches AudioServer");
        Expect(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "#185: the preset mute reaches AudioServer");
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Sfx"), -5.0f), "#185: a bus that the preset does not list keeps its value");
        Expect(AudioBusSettingsServer.GetCurrentPreset() == preset, "#185: GetCurrentPreset()");
        Expect(!AudioBusSettingsServer.IsCustom(), "#185: not Custom right after ApplyPreset()");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -3.0f);
        Expect(AudioBusSettingsServer.IsCustom(), "#185: a single change makes it Custom");
        Expect(!AudioBusSettingsServer.MatchesPreset(preset), "#185: MatchesPreset() is false after the change");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -18.0f);
        Expect(AudioBusSettingsServer.MatchesPreset(preset), "#185: MatchesPreset() is true again");
        AudioBusSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckDevices()
    {
        var server = AudioDeviceSettingsServer.Singleton;
        ExpectEqual(string.Join(",", server.GetSettingNames()), "output_device,input_device", "device setting names");
        ExpectEqual(server.IsValueSupported("output_device"), AudioDeviceSettingsServer.IsDeviceSelectionSupported(), "IsValueSupported() follows the driver");
        AudioDeviceSettingsServer.SetOutputDevice("Check16 device that does not exist");
        ExpectEqual(AudioDeviceSettingsServer.GetOutputDevice(), "Default", "an unknown output device falls back to \"Default\"");
        ExpectEqual(AudioServer.OutputDevice, "Default", "AudioServer keeps \"Default\"");
        AudioDeviceSettingsServer.SetInputDevice("");
        ExpectEqual(AudioDeviceSettingsServer.GetInputDevice(), "Default", "an empty input device is \"Default\"");

        string path = Dir.PathJoin("devices.json");
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("{ \"output_device\": \"Speakers (unplugged)\", \"input_device\": \"Microphone (unplugged)\" }");
        }
        server.FilePath = path;
        ExpectEqual(server.Load(), Error.Ok, "a file with devices of another machine loads without an error");
        ExpectEqual(AudioDeviceSettingsServer.GetOutputDevice(), "Default", "a stored output device that no longer exists becomes \"Default\"");
        ExpectEqual(AudioDeviceSettingsServer.GetInputDevice(), "Default", "a stored input device that no longer exists becomes \"Default\"");
        server.FilePath = "";
        server.Apply();

        string[] devices = AudioServer.GetOutputDeviceList();
        if (AudioDeviceSettingsServer.IsDeviceSelectionSupported() && devices.Length > 1)
        {
            AudioDeviceSettingsServer.SetOutputDevice(devices[1]);
            ExpectEqual(AudioDeviceSettingsServer.GetOutputDevice(), devices[1], "a listed output device is kept");
            AudioDeviceSettingsServer.SetOutputDevice("Default");
        }
        else
        {
            GD.Print("  SKIP  device switch (the driver has no device list or only one device)");
        }
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        float volume = (float)Math.Round(GD.RandRange(-30.0, -10.0), 1);
        AudioBusSettingsServer.SetBusVolumeDb("Music", volume);
        AudioBusSettingsServer.SetBusMute("Music", true);
        ExpectEqual(AudioBusSettingsServer.Singleton.Save(), Error.Ok, "Save() writes the player file");
        Expect(FileAccess.FileExists(BusesFile), "the player file is in user://settings/");

        Godot.Collections.Dictionary probe = RunProbe();
        Expect(probe.ContainsKey("buses_started") && probe["buses_started"].AsBool(), "restart: the bus domain was started");
        Expect(probe.ContainsKey("devices_started") && probe["devices_started"].AsBool(), "restart: the device domain was started");
        Expect(probe.ContainsKey("music_volume_db") && Mathf.IsEqualApprox(probe["music_volume_db"].AsSingle(), volume), "restart: the saved Music volume was applied before the main scene");
        Expect(probe.ContainsKey("music_mute") && probe["music_mute"].AsBool(), "restart: the saved mute was applied");
        ExpectEqual(probe.ContainsKey("ready_frame") ? probe["ready_frame"].AsInt32() : -1, 0, "restart: applied before the first frame, so before any sound");
    }

    private Godot.Collections.Dictionary RunProbe()
    {
        var output = new Godot.Collections.Array();
        string[] args = ["--headless", "--path", ProjectSettings.GlobalizePath("res://"), ProbeScene];
        int code = OS.Execute(OS.GetExecutablePath(), args, output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        foreach (Variant chunk in output)
        {
            foreach (string line in chunk.AsString().Split('\n'))
            {
                if (line.StartsWith("CHECK16_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK16_PROBE ".Length..]);
                    if (parsed.VariantType == Variant.Type.Dictionary)
                    {
                        return parsed.AsGodotDictionary();
                    }
                }
            }
        }
        Expect(false, "restart: the probe printed its result");
        return new Godot.Collections.Dictionary();
    }

    /// <summary>Issue #173: the typed bus signals fire once per applied change, not for pending changes in manual mode.</summary>
    private async Task CheckTypedSignals()
    {
        if (!Engine.HasSingleton("AudioBusSettingsServer") || !Engine.HasSingleton("AudioDeviceSettingsServer"))
        {
            return;
        }
        var devices = (SettingsServer)Engine.GetSingleton("AudioDeviceSettingsServer");
        Expect(devices.HasSignal("output_device_changed"), "AudioDeviceSettingsServer has output_device_changed");
        Expect(devices.HasSignal("input_device_changed"), "AudioDeviceSettingsServer has input_device_changed");
        var s = (SettingsServer)Engine.GetSingleton("AudioBusSettingsServer");
        foreach (string sig in new[] { "bus_volume_changed", "bus_mute_changed", "bus_solo_changed", "bus_layout_changed" })
        {
            Expect(s.HasSignal(sig), $"AudioBusSettingsServer has {sig}");
        }
        string filePath = s.GetFilePath();
        bool hadFile = FileAccess.FileExists(filePath);
        var previousMode = s.ApplyMode;
        s.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        s.ResetToDefaults();
        var volumes = new System.Collections.Generic.List<(StringName Bus, float VolumeDb)>();
        int mutes = 0;
        var onVolume = Callable.From((StringName busName, double volumeDb) => volumes.Add((busName, (float)volumeDb)));
        var onMute = Callable.From((StringName busName, bool muted) => mutes++);
        s.Connect("bus_volume_changed", onVolume);
        s.Connect("bus_mute_changed", onMute);

        s.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        float volume = (float)s.Call("get_bus_volume_db", "Master").AsDouble() != -6.0f ? -6.0f : -12.0f;
        s.Call("set_bus_volume_db", "Master", volume);
        ExpectEqual(volumes.Count, 0, "bus_volume_changed does not fire for a pending change in manual mode");
        s.Apply();
        ExpectEqual(volumes.Count, 1, "bus_volume_changed fires once on Apply()");
        if (volumes.Count > 0)
        {
            ExpectEqual(volumes[0].Bus.ToString(), "Master", "bus_volume_changed passes the bus name");
            ExpectEqual(volumes[0].VolumeDb, (float)volume, "bus_volume_changed passes the volume");
        }
        ExpectEqual(mutes, 0, "bus_mute_changed does not fire for an unchanged mute state");
        s.Call("set_bus_mute", "Master", !s.Call("is_bus_mute", "Master").AsBool());
        s.Apply();
        ExpectEqual(mutes, 1, "bus_mute_changed fires once on Apply()");
        s.ResetToDefaults();
        s.Apply();
        ExpectEqual(volumes.Count, 2, "bus_volume_changed fires again when the default is applied");
        ExpectEqual(mutes, 2, "bus_mute_changed fires again when the default is applied");

        s.Disconnect("bus_volume_changed", onVolume);
        s.Disconnect("bus_mute_changed", onMute);
        s.ApplyMode = previousMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!hadFile)
        {
            DirAccess.RemoveAbsolute(filePath);
        }
    }
}
