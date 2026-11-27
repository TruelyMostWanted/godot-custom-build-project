using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AudioBusSettingsGuide;

/// <summary>
/// Full API test of Audio Bus Settings: AudioBusSettingsServer, AudioBusSettings, AudioBusListSettings, AudioBusSettingsRow(s).
/// Same tests as scripts/gd/Test.gd. The project's bus layout (resources/bus_layout.tres) has the buses Master, Music (-6 dB) and Sfx.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://audio-bus-settings-test-cs";
    private const string ProbeScene = "res://audio-bus-settings/scenes/restart_probe.tscn";
    private const string NightPreset = "res://audio-bus-settings/resources/night_mode_preset.tres";
    private const string BusesFile = "user://settings/audio_bus.ini";
    private const float LayoutMusicDb = -6.0f;
    private const float NightMusicDb = -18.0f;
    private const float MaxDb = 24.0f;

    private readonly List<string> _events = [];
    private int _layoutChanges;

    private static AudioBusSettingsServerInstance Server => AudioBusSettingsServer.Singleton;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        TestRegistration();
        TestResources();
        TestBuses();
        TestManualMode();
        await TestSignals();
        TestCsv();
        TestMissingBus();
        TestStoredValues();
        TestLayoutChange();
        TestPresets();
        await TestRestart();
        Server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(BusesFile);
        RemoveDir(Dir);
    }

    private static float BusVolumeDb(string bus)
    {
        return AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(bus));
    }

    private static bool BusMute(string bus)
    {
        return AudioServer.IsBusMute(AudioServer.GetBusIndex(bus));
    }

    private static bool BusSolo(string bus)
    {
        return AudioServer.IsBusSolo(AudioServer.GetBusIndex(bus));
    }

    private void TestRegistration()
    {
        Expect(Engine.HasSingleton("AudioBusSettingsServer"), "AudioBusSettingsServer is a registered singleton");
        Expect(Server is ListSettingsServer, "AudioBusSettingsServer is a ListSettingsServer");
        Expect(SettingsServerManager.GetServer("audio_bus") == Server, "the domain is registered as audio_bus");
        Expect(Server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(Server.FilePath, BusesFile, "bus file path");
        Expect(ProjectSettings.HasSetting("settings/audio_bus/player_buses"), "project setting settings/audio_bus/player_buses");
        ExpectEqual(ProjectSettings.GetSetting("settings/audio_bus/store_volume").AsBool(), true, "store_volume is on by default");
        ExpectEqual(ProjectSettings.GetSetting("settings/audio_bus/store_mute").AsBool(), true, "store_mute is on by default");
        ExpectEqual(ProjectSettings.GetSetting("settings/audio_bus/store_solo").AsBool(), false, "store_solo is off by default");
        Expect(AudioBusSettingsServer.StoreVolume, "StoreVolume property");
        Expect(AudioBusSettingsServer.StoreMute, "StoreMute property");
        Expect(!AudioBusSettingsServer.StoreSolo, "StoreSolo property");
        Expect(AudioBusSettingsServer.IsStoringVolume(), "IsStoringVolume");
        Expect(AudioBusSettingsServer.IsStoringMute(), "IsStoringMute");
        Expect(!AudioBusSettingsServer.IsStoringSolo(), "IsStoringSolo");
        Expect(AudioBusSettingsServer.GetPlayerBuses().Length == 0, "player_buses is empty: the player controls every bus");
    }

    private void TestResources()
    {
        var bus = new Godot.AudioBusSettings();
        bus.BusName = "Voice";
        bus.VolumeDb = -9.0f;
        bus.Mute = true;
        bus.Solo = true;
        ExpectEqual(bus.BusName, "Voice", "AudioBusSettings.BusName");
        ExpectEqual(bus.VolumeDb, -9.0f, "AudioBusSettings.VolumeDb");
        Expect(bus.Mute && bus.Solo, "AudioBusSettings.Mute and Solo");
        bus.SetVolumeLinear(0.5f);
        Expect(Mathf.Abs(bus.GetVolumeLinear() - 0.5f) < 0.01f, "AudioBusSettings volume linear round trip");
        bus.SetVolumeLinear(0.0f);
        ExpectEqual(bus.VolumeDb, -80.0f, "linear 0.0 is -80 dB");
        var list = new AudioBusListSettings();
        list.Buses.Add(bus);
        ExpectEqual(list.Buses.Count, 1, "AudioBusListSettings.Buses holds AudioBusSettings");
        var row = new AudioBusSettingsRow();
        row.BusName = "Voice";
        row.VolumeDb = -9.0f;
        row.Mute = true;
        row.Solo = true;
        ExpectEqual(row.BusName, "Voice", "AudioBusSettingsRow.BusName");
        ExpectEqual(row.VolumeDb, -9.0f, "AudioBusSettingsRow.VolumeDb");
        Expect(row.Mute && row.Solo, "AudioBusSettingsRow.Mute and Solo");
        var rows = new AudioBusSettingsRows();
        rows.Buses.Add(row);
        ExpectEqual(rows.Buses.Count, 1, "AudioBusSettingsRows.Buses holds rows");
        var sample = GD.Load<AudioBusSettingsRows>("res://audio-bus-settings/resources/bus_rows.tres");
        ExpectEqual(sample.Buses[0].BusName, "Sfx", "the sample AudioBusSettingsRows loads");
        var sampleBus = GD.Load<Godot.AudioBusSettings>("res://audio-bus-settings/resources/sfx_bus.tres");
        ExpectEqual(sampleBus.VolumeDb, -3.0f, "the sample AudioBusSettings loads");
    }

    private void TestBuses()
    {
        Godot.Collections.Array defaults = Server.GetDefaultValue("buses").AsGodotArray();
        ExpectEqual(defaults.Count, 3, "defaults: one entry per bus of the layout");
        ExpectEqual(defaults[1].AsGodotObject().Get("volume_db").AsSingle(), LayoutMusicDb, "defaults come from the bus layout");
        Expect(AudioBusSettingsServer.GetBusNames().SequenceEqual(["Master", "Music", "Sfx"]), "buses in layout order");
        Expect(AudioBusSettingsServer.HasBus("Music"), "HasBus for a bus of the layout");
        Expect(!AudioBusSettingsServer.HasBus("Voice"), "HasBus for an unknown bus");
        Expect(AudioBusSettingsServer.IsPlayerBus("Music"), "IsPlayerBus: every bus with an empty player_buses");

        AudioBusSettingsServer.SetBusVolumeDb("Music", -12.0f);
        ExpectEqual(BusVolumeDb("Music"), -12.0f, "SetBusVolumeDb reaches AudioServer at once");
        ExpectEqual(AudioBusSettingsServer.GetBusVolumeDb("Music"), -12.0f, "GetBusVolumeDb");
        AudioBusSettingsServer.SetBusVolumeLinear("Sfx", 0.5f);
        ExpectEqual(BusVolumeDb("Sfx"), -6.0f, "linear 0.5 is stored as -6.0 dB, not -6.0206");
        Expect(Mathf.Abs(AudioBusSettingsServer.GetBusVolumeLinear("Sfx") - 0.5f) < 0.01f, "GetBusVolumeLinear (50 %)");
        AudioBusSettingsServer.SetBusVolumeLinear("Sfx", 1.0f);
        ExpectEqual(BusVolumeDb("Sfx").ToString(), "0", "linear 1.0 is 0 dB, not -0");
        AudioBusSettingsServer.SetBusMute("Music", true);
        Expect(BusMute("Music"), "SetBusMute reaches AudioServer");
        Expect(AudioBusSettingsServer.IsBusMute("Music"), "IsBusMute");
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        Expect(!BusSolo("Sfx"), "SetBusSolo does nothing while solo is not stored (a warning is printed)");
        AudioBusSettingsServer.StoreSolo = true;
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        Expect(BusSolo("Sfx"), "SetBusSolo reaches AudioServer with StoreSolo");
        Expect(AudioBusSettingsServer.IsBusSolo("Sfx"), "IsBusSolo");
        AudioBusSettingsServer.SetBusVolumeDb("Music", 100.0f);
        ExpectEqual(AudioBusSettingsServer.GetBusVolumeDb("Music"), MaxDb, "the volume is clamped to 24 dB");

        Server.ResetToDefaults();
        ExpectEqual(BusVolumeDb("Music"), LayoutMusicDb, "ResetToDefaults gives back the layout volume");
        Expect(!BusMute("Music"), "ResetToDefaults unmutes");
        Expect(!BusSolo("Sfx"), "ResetToDefaults clears solo");
        AudioBusSettingsServer.SetStoreSolo(false);
    }

    private void TestManualMode()
    {
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        AudioBusSettingsServer.SetBusVolumeDb("Music", -30.0f);
        ExpectEqual(BusVolumeDb("Music"), LayoutMusicDb, "manual mode: not applied yet");
        Expect(Server.IsDirty(), "manual mode: dirty");
        Server.Revert();
        ExpectEqual(AudioBusSettingsServer.GetBusVolumeDb("Music"), LayoutMusicDb, "Revert gives back the applied volume");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -30.0f);
        ExpectEqual(Server.Apply(), Error.Ok, "Apply in manual mode");
        ExpectEqual(BusVolumeDb("Music"), -30.0f, "Apply reaches AudioServer");
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Server.ResetToDefaults();
    }

    private async Task TestSignals()
    {
        var volumes = new List<string>();
        var mutes = new List<string>();
        var solos = new List<string>();
        AudioBusSettingsServer.BusVolumeChangedEventHandler onVolume = (bus, volumeDb) => volumes.Add($"{bus} {volumeDb}");
        AudioBusSettingsServer.BusMuteChangedEventHandler onMute = (bus, muted) => mutes.Add($"{bus} {muted}");
        AudioBusSettingsServer.BusSoloChangedEventHandler onSolo = (bus, solo) => solos.Add($"{bus} {solo}");
        System.Action onLayout = () => _layoutChanges++;
        AudioBusSettingsServer.BusVolumeChanged += onVolume;
        AudioBusSettingsServer.BusMuteChanged += onMute;
        AudioBusSettingsServer.BusSoloChanged += onSolo;
        AudioBusSettingsServer.BusLayoutChanged += onLayout;
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        AudioBusSettingsServer.SetBusVolumeDb("Master", -12.0f);
        Expect(volumes.Count == 0, "BusVolumeChanged does not fire for a pending change in manual mode");
        Server.Apply();
        Expect(volumes.SequenceEqual(["Master -12"]), "BusVolumeChanged fires once on Apply with the bus and the volume");
        Expect(mutes.Count == 0, "BusMuteChanged does not fire for an unchanged mute state");
        AudioBusSettingsServer.SetBusMute("Master", true);
        Server.Apply();
        Expect(mutes.SequenceEqual(["Master True"]), "BusMuteChanged fires once on Apply");
        ExpectEqual(volumes.Count, 1, "BusVolumeChanged does not fire again");
        AudioBusSettingsServer.StoreSolo = true;
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        Server.Apply();
        Expect(solos.SequenceEqual(["Sfx True"]), "BusSoloChanged fires once on Apply");
        AudioBusSettingsServer.StoreSolo = false;
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Server.ResetToDefaults();
        ExpectEqual(volumes.Count, 2, "BusVolumeChanged fires again when the default is applied");
        ExpectEqual(mutes.Count, 2, "BusMuteChanged fires again when the default is applied");
        AudioBusLayout layout = AudioServer.GenerateBusLayout();
        AudioServer.SetBusLayout(layout);
        Expect(_layoutChanges >= 1, "BusLayoutChanged fires after AudioServer got a new bus layout");
        AudioBusSettingsServer.BusVolumeChanged -= onVolume;
        AudioBusSettingsServer.BusMuteChanged -= onMute;
        AudioBusSettingsServer.BusSoloChanged -= onSolo;
        AudioBusSettingsServer.BusLayoutChanged -= onLayout;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void TestCsv()
    {
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Csv);
        ExpectEqual(Server.FilePath, "user://settings/audio_bus.csv", "the bus list can be stored as CSV");
        ProjectSettings.SetSetting("settings/general/default_format", (int)ResourceTextSerializer.Format.Ini);
        string path = Dir.PathJoin("buses.csv");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -20.0f);
        AudioBusSettingsServer.SetBusMute("Sfx", true);
        Server.FilePath = path;
        ExpectEqual(Server.Save(), Error.Ok, "Save as CSV");
        string[] lines = FileAccess.GetFileAsString(path).StripEdges().Split('\n');
        ExpectEqual(lines.Length, 4, "CSV: a header and one row per bus");
        Expect(lines[0].Contains("volume_db") && lines[0].Contains("mute"), "CSV: the columns are the stored values");
        Expect(!lines[0].Contains("solo"), "CSV: no solo column while solo is not stored");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -3.0f);
        AudioBusSettingsServer.SetBusMute("Sfx", false);
        ExpectEqual(Server.Load(), Error.Ok, "Load reads the CSV");
        ExpectEqual(AudioBusSettingsServer.GetBusVolumeDb("Music"), -20.0f, "CSV: volume loaded");
        Expect(AudioBusSettingsServer.IsBusMute("Sfx"), "CSV: mute loaded");
        Server.Apply();
        ExpectEqual(BusVolumeDb("Music"), -20.0f, "CSV: loaded volume applied");
        Server.FilePath = "";
        Server.ResetToDefaults();
    }

    private void TestMissingBus()
    {
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        var voice = new Godot.AudioBusSettings();
        voice.BusName = "Voice";
        voice.VolumeDb = -9.0f;
        Server.AddEntry(voice);
        Server.Apply();
        ExpectEqual(AudioServer.GetBusIndex("Voice"), -1, "a bus missing in the layout is skipped (a warning is printed)");
        int index = Server.FindEntry("Voice");
        Expect(index >= 0, "the entry of a missing bus is kept");
        Expect(AudioBusSettingsServer.HasBus("Voice"), "HasBus is true for an entry of a missing bus");
        ExpectEqual(AudioServer.BusCount, 3, "no bus is created: the layout builds the buses");
        Server.RemoveEntry(index);
        Server.Apply();
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
    }

    private void TestStoredValues()
    {
        string path = Dir.PathJoin("stored_values.ini");
        Server.FilePath = path;
        AudioBusSettingsServer.SetBusVolumeDb("Music", -14.0f);
        AudioBusSettingsServer.SetBusMute("Music", true);
        ExpectEqual(Server.Save(), Error.Ok, "Save with the default store properties");
        string text = FileAccess.GetFileAsString(path);
        Expect(text.Contains("volume_db") && text.Contains("mute"), "the file holds volume and mute");
        Expect(!text.Contains("solo"), "the file holds no solo");
        string csvPath = Dir.PathJoin("hand_edited.csv");
        using (var file = FileAccess.Open(csvPath, FileAccess.ModeFlags.Write))
        {
            file.StoreString("bus_name,volume_db,solo\nMusic,-17,true\n");
        }
        Server.FilePath = csvPath;
        ExpectEqual(Server.Load(), Error.Ok, "a file with an unstored value loads");
        ExpectEqual(AudioBusSettingsServer.GetBusVolumeDb("Music"), -17.0f, "the stored volume is read");
        Expect(!AudioBusSettingsServer.IsBusSolo("Music"), "the solo of the file is ignored");
        Expect(!AudioBusSettingsServer.IsBusMute("Music"), "the mute missing in the file comes from the layout");
        Expect(AudioBusSettingsServer.GetBusNames().SequenceEqual(["Master", "Music", "Sfx"]), "buses missing in the file come from the layout");
        Server.Apply();
        Server.FilePath = "";
        Server.ResetToDefaults();
    }

    private void TestLayoutChange()
    {
        AudioBusLayout gameLayout = AudioServer.GenerateBusLayout();
        AudioBusSettingsServer.SetBusVolumeDb("Music", -21.0f);
        AudioBusSettingsServer.SetBusMute("Music", true);
        AudioBusSettingsServer.StoreSolo = true;
        AudioBusSettingsServer.SetBusSolo("Sfx", true);
        AudioServer.SetBusLayout(gameLayout);
        ExpectEqual(BusVolumeDb("Music"), -21.0f, "SetBusLayout keeps the stored volume");
        Expect(BusMute("Music"), "SetBusLayout keeps the stored mute");
        Expect(BusSolo("Sfx"), "SetBusLayout keeps the stored solo");
        Expect(!Server.IsDirty(), "the re-apply changes no setting");

        AudioBusSettingsServer.PlayerBuses = ["Music"];
        Expect(AudioBusSettingsServer.IsPlayerBus("Music"), "IsPlayerBus: Music is listed");
        Expect(!AudioBusSettingsServer.IsPlayerBus("Sfx"), "IsPlayerBus: Sfx is not listed");
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Sfx"), -9.0f);
        AudioServer.SetBusSolo(AudioServer.GetBusIndex("Sfx"), false);
        AudioBusLayout quietLayout = AudioServer.GenerateBusLayout();
        AudioServer.SetBusLayout(quietLayout);
        ExpectEqual(BusVolumeDb("Music"), -21.0f, "the player bus gets the player value after SetBusLayout");
        ExpectEqual(BusVolumeDb("Sfx"), -9.0f, "a bus that is not a player bus keeps the value of the new layout");
        Expect(!BusSolo("Sfx"), "the solo of a non-player bus is not pushed again");
        AudioBusSettingsServer.SetBusVolumeDb("Sfx", -1.0f);
        ExpectEqual(BusVolumeDb("Sfx"), -9.0f, "SetBusVolumeDb on a non-player bus does nothing (a warning is printed)");
        AudioBusSettingsServer.PlayerBuses = [];
        AudioServer.SetBusLayout(gameLayout);

        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        AudioBusSettingsServer.SetBusVolumeDb("Music", -40.0f);
        AudioServer.SetBusLayout(gameLayout);
        ExpectEqual(BusVolumeDb("Music"), -21.0f, "manual mode: the applied volume comes back, the pending one waits");
        Server.Revert();
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Server.ResetToDefaults();
        ExpectEqual(BusVolumeDb("Music"), LayoutMusicDb, "ResetToDefaults after the layout change");
        Expect(!BusSolo("Sfx"), "ResetToDefaults clears the stored solo");
        AudioBusSettingsServer.StoreSolo = false;
    }

    private void TestPresets()
    {
        var preset = GD.Load<AudioBusListSettings>(NightPreset);
        ExpectEqual(preset.Buses.Count, 1, "the preset resource loads");
        AudioBusSettingsServer.SetBusVolumeDb("Sfx", -5.0f);
        Expect(AudioBusSettingsServer.IsCustom(), "no preset applied: custom");
        Expect(AudioBusSettingsServer.GetCurrentPreset() == null, "no current preset before ApplyPreset");
        ExpectEqual(AudioBusSettingsServer.ApplyPreset(preset), Error.Ok, "ApplyPreset");
        ExpectEqual(BusVolumeDb("Music"), NightMusicDb, "the preset volume reaches AudioServer");
        Expect(BusMute("Music"), "the preset mute reaches AudioServer");
        ExpectEqual(BusVolumeDb("Sfx"), -5.0f, "a bus that the preset does not list keeps its value");
        Expect(AudioBusSettingsServer.GetCurrentPreset() == preset, "GetCurrentPreset");
        Expect(!AudioBusSettingsServer.IsCustom(), "not custom right after ApplyPreset");
        AudioBusSettingsServer.SetBusVolumeDb("Music", -3.0f);
        Expect(AudioBusSettingsServer.IsCustom(), "a single change makes it custom");
        Expect(!AudioBusSettingsServer.MatchesPreset(preset), "MatchesPreset is false after the change");
        AudioBusSettingsServer.SetBusVolumeDb("Music", NightMusicDb);
        Expect(AudioBusSettingsServer.MatchesPreset(preset), "MatchesPreset is true again");
        Server.ResetToDefaults();
    }

    private async Task TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        float volume = -GD.RandRange(10, 30);
        AudioBusSettingsServer.SetBusVolumeDb("Music", volume);
        AudioBusSettingsServer.SetBusMute("Music", true);
        ExpectEqual(Server.Save(), Error.Ok, "Save writes the player file");
        Godot.Collections.Dictionary probe = RunProbe();
        Expect(probe["started"].AsBool(), "restart: the domain was started");
        ExpectEqual(probe["music_volume_db"].AsSingle(), volume, "restart: the saved Music volume was applied before the main scene");
        Expect(probe["music_mute"].AsBool(), "restart: the saved mute was applied");
        ExpectEqual(probe["ready_frame"].AsInt32(), 0, "restart: applied before the first frame, so before any sound");
        await Task.CompletedTask;
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
                if (line.StartsWith("PROBE "))
                {
                    return Json.ParseString(line["PROBE ".Length..]).AsGodotDictionary();
                }
            }
        }
        Expect(false, "restart: the probe printed its result");
        return [];
    }

    private static void RemoveDir(string path)
    {
        foreach (string fileName in DirAccess.GetFilesAt(path))
        {
            DirAccess.RemoveAbsolute(path.PathJoin(fileName));
        }
        DirAccess.RemoveAbsolute(path);
    }
}
