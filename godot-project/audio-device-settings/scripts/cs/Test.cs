using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AudioDeviceSettingsGuide;

/// <summary>Full API test of Audio Device Settings: AudioDeviceSettingsServer and AudioDeviceSettings. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://audio-device-settings-test-cs";
    private const string ProbeScene = "res://audio-device-settings/scenes/restart_probe.tscn";
    private const string DevicesFile = "user://settings/audio_device.ini";
    private const string DefaultDevice = "Default";
    private const string UnknownDevice = "Test device that does not exist";

    private readonly List<string> _outputs = [];

    private static AudioDeviceSettingsServerInstance Server => AudioDeviceSettingsServer.Singleton;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        TestRegistration();
        TestResource();
        TestDevices();
        TestFile();
        TestManualMode();
        TestSignals();
        TestRestart();
        Server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(DevicesFile);
        RemoveDir(Dir);
    }

    private void TestRegistration()
    {
        Expect(Engine.HasSingleton("AudioDeviceSettingsServer"), "AudioDeviceSettingsServer is a registered singleton");
        Expect(Server is SettingsServer, "AudioDeviceSettingsServer is a SettingsServer");
        Expect(SettingsServerManager.GetServer("audio_device") == Server, "the domain is registered as audio_device");
        Expect(Server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(Server.FilePath, DevicesFile, "device file path");
        Expect(Server.GetSettingNames().SequenceEqual(["output_device", "input_device"]), "setting names");
        ExpectEqual(Server.IsValueSupported("output_device"), AudioDeviceSettingsServer.IsDeviceSelectionSupported(), "IsValueSupported follows the driver");
    }

    private void TestResource()
    {
        var settings = new Godot.AudioDeviceSettings();
        ExpectEqual(settings.OutputDevice, DefaultDevice, "AudioDeviceSettings.OutputDevice defaults to Default");
        ExpectEqual(settings.InputDevice, DefaultDevice, "AudioDeviceSettings.InputDevice defaults to Default");
        settings.OutputDevice = "Speakers";
        settings.InputDevice = "Microphone";
        ExpectEqual(settings.OutputDevice, "Speakers", "AudioDeviceSettings.OutputDevice can be set");
        ExpectEqual(settings.InputDevice, "Microphone", "AudioDeviceSettings.InputDevice can be set");
        var sample = GD.Load<Godot.AudioDeviceSettings>("res://audio-device-settings/resources/devices.tres");
        ExpectEqual(sample.OutputDevice, DefaultDevice, "the sample AudioDeviceSettings loads");
    }

    private void TestDevices()
    {
        AudioDeviceSettingsServer.SetOutputDevice(UnknownDevice);
        ExpectEqual(AudioDeviceSettingsServer.GetOutputDevice(), DefaultDevice, "an unknown output device falls back to Default");
        ExpectEqual(AudioServer.OutputDevice, DefaultDevice, "AudioServer keeps Default");
        AudioDeviceSettingsServer.SetInputDevice(UnknownDevice);
        ExpectEqual(AudioDeviceSettingsServer.GetInputDevice(), DefaultDevice, "an unknown input device falls back to Default");
        AudioDeviceSettingsServer.SetInputDevice("");
        ExpectEqual(AudioDeviceSettingsServer.GetInputDevice(), DefaultDevice, "an empty input device is Default");
        string[] devices = AudioServer.GetOutputDeviceList();
        if (AudioDeviceSettingsServer.IsDeviceSelectionSupported() && devices.Length > 1)
        {
            AudioDeviceSettingsServer.SetOutputDevice(devices[1]);
            ExpectEqual(AudioDeviceSettingsServer.GetOutputDevice(), devices[1], "a listed output device is kept");
            AudioDeviceSettingsServer.SetOutputDevice(DefaultDevice);
        }
        else
        {
            GD.Print("  SKIP  device switch (the driver has no device list or only one device)");
        }
    }

    private void TestFile()
    {
        string path = Dir.PathJoin("devices.json");
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("{\"output_device\": \"Speakers (unplugged)\", \"input_device\": \"Microphone (unplugged)\"}");
        }
        Server.FilePath = path;
        ExpectEqual(Server.Load(), Error.Ok, "a file with devices of another machine loads without an error");
        ExpectEqual(AudioDeviceSettingsServer.GetOutputDevice(), DefaultDevice, "a stored output device that no longer exists becomes Default");
        ExpectEqual(AudioDeviceSettingsServer.GetInputDevice(), DefaultDevice, "a stored input device that no longer exists becomes Default");
        ExpectEqual(Server.Save(), Error.Ok, "Save writes the file");
        Expect(FileAccess.FileExists(path), "the file exists");
        Server.FilePath = "";
        Server.Apply();
    }

    private void TestManualMode()
    {
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        AudioDeviceSettingsServer.SetInputDevice(UnknownDevice);
        Expect(!Server.IsDirty(), "a device that falls back to Default is no change");
        ExpectEqual(Server.Apply(), Error.Ok, "Apply in manual mode");
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Server.ResetToDefaults();
        ExpectEqual(AudioDeviceSettingsServer.GetOutputDevice(), DefaultDevice, "ResetToDefaults gives back Default");
    }

    private void TestSignals()
    {
        AudioDeviceSettingsServer.OutputDeviceChangedEventHandler onOutput = device => _outputs.Add(device);
        AudioDeviceSettingsServer.OutputDeviceChanged += onOutput;
        AudioDeviceSettingsServer.SetOutputDevice(UnknownDevice);
        Expect(_outputs.Count == 0, "OutputDeviceChanged does not fire when the device stays Default");
        string[] devices = AudioServer.GetOutputDeviceList();
        if (AudioDeviceSettingsServer.IsDeviceSelectionSupported() && devices.Length > 1)
        {
            AudioDeviceSettingsServer.SetOutputDevice(devices[1]);
            Expect(_outputs.SequenceEqual([devices[1]]), "OutputDeviceChanged fires once with the device");
            AudioDeviceSettingsServer.SetOutputDevice(DefaultDevice);
        }
        else
        {
            GD.Print("  SKIP  OutputDeviceChanged with a real device (the driver has no device list or only one device)");
        }
        AudioDeviceSettingsServer.OutputDeviceChanged -= onOutput;
        Expect(Server.HasSignal("input_device_changed"), "AudioDeviceSettingsServer has input_device_changed");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        ExpectEqual(Server.Save(), Error.Ok, "Save writes the player file");
        Godot.Collections.Dictionary probe = RunProbe();
        Expect(probe["started"].AsBool(), "restart: the domain was started");
        ExpectEqual(probe["output_device"].AsString(), DefaultDevice, "restart: the output device is Default");
        ExpectEqual(probe["input_device"].AsString(), DefaultDevice, "restart: the input device is Default");
        ExpectEqual(probe["ready_frame"].AsInt32(), 0, "restart: applied before the first frame");
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
