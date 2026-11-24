using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue189RefactoringSettingsFloatPrecision;

/// <summary>C# checks of 189-refactoring-settings-float-precision. Check the same things as scripts/gd/check.gd.
/// Settings float values have at most three decimals: in getters, signals and files.</summary>
public partial class Issue189RefactoringSettingsFloatPrecisionCheck : FeatureCheckCs
{
    private const string TempFolder = "user://checks/189-refactoring-settings-float-precision";

    // Text of a float with the noise of a 32-bit value or of a conversion.
    private static readonly string[] Noise = { "0000000", "9999999", "e-0", "e-1" };

    protected override string CheckName => "189-refactoring-settings-float-precision";

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(TempFolder);
        CheckSnapFloat();
        CheckValues();
        CheckLinearVolume();
        CheckFiles();
        return Task.CompletedTask;
    }

    private void CheckSnapFloat()
    {
        // Exact comparisons: snap_float() returns the double nearest to the decimal value.
        Expect(SettingsServer.SnapFloat(0.1 + 0.2) == 0.3, "SnapFloat(0.1 + 0.2) is 0.3");
        Expect(SettingsServer.SnapFloat(1.23456) == 1.235, "three decimals by default");
        Expect(SettingsServer.SnapFloat(-6.0206, 0.1) == -6.0, "snapped to the step");
        Expect(SettingsServer.SnapFloat(0.12, 0.1, 0.05) == 0.15, "snapped relative to the offset");
        Expect(SettingsServer.SnapFloat(0.0123, 0.0001) == 0.012, "a step below 0.001 counts as 0.001");
        Expect(!double.IsNegative(SettingsServer.SnapFloat(-0.0000014)), "-0.0 becomes 0.0");
    }

    private void CheckValues()
    {
        var server = InputSettingsServer.Singleton;
        server.ResetToDefaults();
        double deadzone = server.GetValue("gamepad_deadzone").AsDouble();
        Expect(deadzone == 0.2, "the default deadzone (a 32-bit member) is 0.2, not 0.20000000298023224");
        Expect(server.GetDefaultValue("gamepad_deadzone").AsDouble() == 0.2, "GetDefaultValue() is 0.2");

        var changes = new List<double>();
        void OnChanged(StringName settingName, Variant value) => changes.Add(value.AsDouble());
        server.ValueChanged += OnChanged;
        server.SetValue("gamepad_deadzone", 0.123456);
        Expect(server.GetValue("gamepad_deadzone").AsDouble() == 0.12, "SetValue() snaps to the 0.01 step of the range");
        Expect(server.GetAppliedValue("gamepad_deadzone").AsDouble() == 0.12, "the applied value is 0.12");
        Expect(changes.Count == 1 && changes[0] == 0.12, "ValueChanged carries 0.12");
        server.SetValue("gamepad_deadzone", 0.12);
        ExpectEqual(changes.Count, 1, "the same value again is no change");
        server.ValueChanged -= OnChanged;

        server.SetValue("mouse_sensitivity", 1.23456);
        Expect(server.GetValue("mouse_sensitivity").AsDouble() == 1.23, "mouse_sensitivity snaps to 0.01");
        server.ResetToDefaults();
    }

    private void CheckLinearVolume()
    {
        var server = AudioBusSettingsServer.Singleton;
        if (!Expect(server.HasBus("Sfx"), "the project has an Sfx bus"))
        {
            return;
        }
        server.SetBusVolumeLinear("Sfx", 0.5f);
        Expect(server.GetBusVolumeDb("Sfx") == -6.0f, "linear 0.5 is stored as -6.0 dB, not -6.0206");
        int index = server.FindEntry("Sfx");
        if (Expect(index >= 0, "Sfx has an entry"))
        {
            Expect(server.GetEntryValue(index, "volume_db").AsDouble() == -6.0, "GetEntryValue() is -6.0");
        }
        server.SetBusVolumeLinear("Sfx", 1.0f);
        float volume = server.GetBusVolumeDb("Sfx");
        Expect(volume == 0.0f && !float.IsNegative(volume), "linear 1.0 is 0.0 dB, not -0.0 or -0.000001");
        server.ResetToDefaults();
    }

    private void CheckFiles()
    {
        var input = InputSettingsServer.Singleton;
        string inputPath = input.GetFilePath();
        foreach (string extension in new[] { "json", "ini" })
        {
            string path = TempFolder.PathJoin("input." + extension);
            input.SetFilePath(path);
            input.SetValue("gamepad_deadzone", 0.3);
            input.SetValue("mouse_sensitivity", 1.7);
            input.Apply();
            ExpectEqual(input.Save(), Error.Ok, $"{extension}: Save()");
            string text = FileAccess.GetFileAsString(path);
            Expect(text.Contains("0.3") && text.Contains("1.7"), $"{extension}: the file holds 0.3 and 1.7");
            ExpectNoNoise(text, extension);
            input.ResetToDefaults();
            ExpectEqual(input.Load(), Error.Ok, $"{extension}: Load()");
            Expect(input.GetValue("gamepad_deadzone").AsDouble() == 0.3, $"{extension}: loaded deadzone is 0.3");
        }
        input.SetFilePath(inputPath);
        input.ResetToDefaults();

        var audio = AudioBusSettingsServer.Singleton;
        if (!audio.HasBus("Sfx"))
        {
            return;
        }
        string audioPath = audio.GetFilePath();
        string csvPath = TempFolder.PathJoin("audio_bus.csv");
        audio.SetFilePath(csvPath);
        audio.SetBusVolumeDb("Sfx", -12.3f);
        audio.Apply();
        ExpectEqual(audio.Save(), Error.Ok, "csv: Save()");
        string csv = FileAccess.GetFileAsString(csvPath);
        Expect(csv.Contains("-12.3"), "csv: the file holds -12.3");
        ExpectNoNoise(csv, "csv");
        audio.SetFilePath(audioPath);
        audio.ResetToDefaults();
    }

    private void ExpectNoNoise(string text, string format)
    {
        foreach (string noise in Noise)
        {
            Expect(!text.Contains(noise), $"{format}: no float noise (\"{noise}\")");
        }
    }
}
