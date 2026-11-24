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
    private const string BusesFile = "user://settings/audio_buses.ini";
    private const string DevicesFile = "user://settings/audio.ini";

    protected override string CheckName => "16-feature-audio-settings";

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        Expect(Engine.HasSingleton("AudioBusSettingsServer"), "AudioBusSettingsServer is a registered singleton");
        Expect(Engine.HasSingleton("AudioSettingsServer"), "AudioSettingsServer is a registered singleton");
        // First C++ domain singleton: the generated static class and the instance type.
        Expect(AudioBusSettingsServer.Singleton is AudioBusSettingsServerInstance, "AudioBusSettingsServer.Singleton is the instance type");
        Expect(AudioBusSettingsServer.Singleton is ListSettingsServer, "AudioBusSettingsServerInstance derives from ListSettingsServer");
        Expect(AudioSettingsServer.Singleton is AudioSettingsServerInstance, "AudioSettingsServer.Singleton is the instance type");
        Expect(AudioSettingsServer.Singleton is SettingsServer, "AudioSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("audio_buses") == AudioBusSettingsServer.Singleton, "the bus domain is registered as \"audio_buses\"");
        Expect(SettingsServerManager.GetServer("audio") == AudioSettingsServer.Singleton, "the device domain is registered as \"audio\"");
        Expect(AudioBusSettingsServer.Singleton.IsStarted(), "the bus domain was started before the main scene");
        Expect(AudioSettingsServer.Singleton.IsStarted(), "the device domain was started before the main scene");
        ExpectEqual(AudioBusSettingsServer.Singleton.GetFilePath(), BusesFile, "bus file path");
        ExpectEqual(AudioSettingsServer.Singleton.GetFilePath(), DevicesFile, "device file path");

        CheckBuses();
        CheckManualMode();
        CheckCsv();
        CheckMissingBus();
        CheckDevices();
        CheckRestart();

        // Leave no player files behind for later runs.
        AudioBusSettingsServer.Singleton.ResetToDefaults();
        AudioSettingsServer.Singleton.ResetToDefaults();
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
        Expect(Math.Abs(AudioBusSettingsServer.GetBusVolumeLinear("Sfx") - 0.5f) < 0.001f, "linear volume (50 %)");
        Expect(Math.Abs(BusVolumeDb("Sfx") - Mathf.LinearToDb(0.5f)) < 0.01f, "linear volume is stored in dB");
        AudioBusSettingsServer.SetBusMute("Music", true);
        Expect(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "SetBusMute() reaches AudioServer");
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        Expect(AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "SetBusSolo() reaches AudioServer");
        AudioBusSettingsServer.SetBusVolumeDb("Music", 100.0f);
        Expect(Mathf.IsEqualApprox(AudioBusSettingsServer.GetBusVolumeDb("Music"), 24.0f), "volume is clamped to 24 dB");

        AudioBusSettingsServer.Singleton.ResetToDefaults();
        Expect(Mathf.IsEqualApprox(BusVolumeDb("Music"), -6.0f), "ResetToDefaults() gives back the layout volume");
        Expect(!AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")), "ResetToDefaults() unmutes");
        Expect(!AudioServer.IsBusSolo(AudioServer.GetBusIndex("Sfx")), "ResetToDefaults() clears solo");
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
        ProjectSettings.SetSetting("settings/storage/format", "csv");
        ExpectEqual(server.GetFilePath(), "user://settings/audio_buses.csv", "the bus list can be stored as CSV");
        ExpectEqual(AudioSettingsServer.Singleton.GetFilePath(), DevicesFile, "the devices stay INI (a warning is printed once)");
        ProjectSettings.SetSetting("settings/storage/format", "ini");

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

        AudioBusSettingsServer.CreateMissingBuses = true;
        server.SetEntryValue(index, "volume_db", -10.0f);
        server.Apply();
        int bus = AudioServer.GetBusIndex("Voice");
        Expect(bus >= 0, "CreateMissingBuses adds the bus");
        Expect(bus >= 0 && Mathf.IsEqualApprox(AudioServer.GetBusVolumeDb(bus), -10.0f), "the created bus gets the stored volume");

        AudioBusSettingsServer.CreateMissingBuses = false;
        server.RemoveEntry(server.FindEntry("Voice"));
        server.Apply();
        if (bus >= 0)
        {
            AudioServer.RemoveBus(bus);
        }
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
    }

    private void CheckDevices()
    {
        var server = AudioSettingsServer.Singleton;
        ExpectEqual(string.Join(",", server.GetSettingNames()), "output_device,input_device", "device setting names");
        ExpectEqual(server.IsValueSupported("output_device"), AudioSettingsServer.IsDeviceSelectionSupported(), "IsValueSupported() follows the driver");
        AudioSettingsServer.SetOutputDevice("Check16 device that does not exist");
        ExpectEqual(AudioSettingsServer.GetOutputDevice(), "Default", "an unknown output device falls back to \"Default\"");
        ExpectEqual(AudioServer.OutputDevice, "Default", "AudioServer keeps \"Default\"");
        AudioSettingsServer.SetInputDevice("");
        ExpectEqual(AudioSettingsServer.GetInputDevice(), "Default", "an empty input device is \"Default\"");

        string path = Dir.PathJoin("devices.json");
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("{ \"output_device\": \"Speakers (unplugged)\", \"input_device\": \"Microphone (unplugged)\" }");
        }
        server.FilePath = path;
        ExpectEqual(server.Load(), Error.Ok, "a file with devices of another machine loads without an error");
        ExpectEqual(AudioSettingsServer.GetOutputDevice(), "Default", "a stored output device that no longer exists becomes \"Default\"");
        ExpectEqual(AudioSettingsServer.GetInputDevice(), "Default", "a stored input device that no longer exists becomes \"Default\"");
        server.FilePath = "";
        server.Apply();

        string[] devices = AudioServer.GetOutputDeviceList();
        if (AudioSettingsServer.IsDeviceSelectionSupported() && devices.Length > 1)
        {
            AudioSettingsServer.SetOutputDevice(devices[1]);
            ExpectEqual(AudioSettingsServer.GetOutputDevice(), devices[1], "a listed output device is kept");
            AudioSettingsServer.SetOutputDevice("Default");
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
}
