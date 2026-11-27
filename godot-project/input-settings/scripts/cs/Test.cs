using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.InputSettingsGuide;

/// <summary>Full API test of Input Settings: InputSettingsServer and InputSettings. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string ProbeScene = "res://input-settings/scenes/restart_probe.tscn";
    private const string InputFile = "user://settings/input.ini";
    private const string Dir = "user://input-settings-test-cs";
    private const double MaxSensitivity = 10.0;
    private const double MaxDeadzone = 0.95;
    private const string TypedSignals = "mouse_sensitivity_changed,keyboard_sensitivity_changed,gamepad_sensitivity_changed,mouse_x_inverted_changed,mouse_y_inverted_changed,keyboard_x_inverted_changed,keyboard_y_inverted_changed,gamepad_x_inverted_changed,gamepad_y_inverted_changed,gamepad_deadzone_changed,mouse_smoothing_changed,vibration_enabled_changed,vibration_strength_changed,toggle_actions_changed,first_player_device_id_changed";

    private const InputSettingsServer.Device Keyboard = InputSettingsServer.Device.Keyboard;
    private const InputSettingsServer.Device Mouse = InputSettingsServer.Device.Mouse;
    private const InputSettingsServer.Device Gamepad = InputSettingsServer.Device.Gamepad;

    private readonly List<string> _events = [];

    private static InputSettingsServerInstance Server => InputSettingsServer.Singleton;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        TestRegistration();
        TestConstants();
        Server.ResetToDefaults();
        TestDefaults();
        TestValidation();
        TestDevices();
        TestHelpers();
        TestSmoothing();
        TestPlayerEvents();
        TestVibration();
        TestFloatPrecision();
        TestSignals();
        TestEngineOptions();
        await TestToggleActions();
        TestMouseCapture();
        TestResource();
        TestRestart();
        Server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(InputFile);
        RemoveDir(Dir);
    }

    private void Log(string text)
    {
        _events.Add(text);
    }

    private void TestRegistration()
    {
        Expect(Engine.HasSingleton("InputSettingsServer"), "InputSettingsServer is a registered singleton");
        Expect(Server is SettingsServer, "InputSettingsServer is a SettingsServer");
        Expect(SettingsServerManager.GetServer("input") == Server, "the domain is registered as input");
        Expect(Server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(Server.FilePath, InputFile, "file path");
        Expect(Server.GetSettingNames().Contains("mouse_sensitivity"), "setting names contain mouse_sensitivity");
        SettingsServer.SettingFlags flags = Server.GetSettingFlags("mouse_sensitivity");
        Expect((flags & SettingsServer.SettingFlags.NoEngineTarget) != 0, "mouse_sensitivity has no engine target");
        foreach (string signalName in TypedSignals.Split(','))
        {
            Expect(Server.HasSignal(signalName), $"InputSettingsServer has the signal {signalName}");
        }
    }

    private void TestConstants()
    {
        ExpectEqual((int)Keyboard, 0, "Device.Keyboard is 0");
        ExpectEqual((int)Mouse, 1, "Device.Mouse is 1");
        ExpectEqual((int)Gamepad, 2, "Device.Gamepad is 2");
        ExpectEqual((int)InputSettingsServer.Device.Max, 3, "Device.Max is 3");
    }

    private void TestDefaults()
    {
        foreach (InputSettingsServer.Device device in new[] { Keyboard, Mouse, Gamepad })
        {
            ExpectEqual(InputSettingsServer.GetSensitivity(device), 1.0f, $"default sensitivity of device {device}");
            Expect(!InputSettingsServer.IsXInverted(device), $"default: x not inverted, device {device}");
            Expect(!InputSettingsServer.IsYInverted(device), $"default: y not inverted, device {device}");
        }
        ExpectEqual(Server.GetValue("gamepad_deadzone").AsDouble(), 0.2, "default deadzone is 0.2, not 0.20000000298023224");
        Expect(InputSettingsServer.IsVibrationEnabled(), "default: vibration on");
        ExpectEqual(InputSettingsServer.GetVibrationStrength(), 1.0f, "default vibration strength");
        ExpectEqual(InputSettingsServer.GetMouseSmoothing(), 0.0f, "default: no smoothing");
        Expect(InputSettingsServer.GetToggleActions().Length == 0, "default: no toggle actions");
        ExpectEqual(InputSettingsServer.GetFirstPlayerDeviceId(), -1, "default: any device");
        Expect(InputSettingsServer.IsKeyboardAllowedForCurrentPlayer(), "default: keyboard allowed");
        Expect(!InputSettingsServer.IsMouseConfined(), "default: mouse not confined");
    }

    private void TestValidation()
    {
        InputSettingsServer.SetSensitivity(Mouse, 50.0f);
        ExpectEqual(Server.GetValue("mouse_sensitivity").AsDouble(), MaxSensitivity, "sensitivity is clamped to 10");
        InputSettingsServer.SetSensitivity(Mouse, 0.0f);
        ExpectEqual(Server.GetValue("mouse_sensitivity").AsDouble(), 0.01, "sensitivity is clamped to 0.01");
        InputSettingsServer.SetGamepadDeadzone(3.0f);
        ExpectEqual(Server.GetValue("gamepad_deadzone").AsDouble(), MaxDeadzone, "deadzone is clamped to 0.95");
        InputSettingsServer.SetMouseSmoothing(3.0f);
        ExpectEqual(Server.GetValue("mouse_smoothing").AsDouble(), MaxDeadzone, "smoothing is clamped to 0.95");
        InputSettingsServer.SetVibrationStrength(3.0f);
        ExpectEqual(InputSettingsServer.GetVibrationStrength(), 1.0f, "vibration strength is clamped to 1");
        InputSettingsServer.SetToggleActions(["crouch", "", "aim ", "crouch"]);
        Expect(InputSettingsServer.GetToggleActions().SequenceEqual(["crouch", "aim"]), "toggle actions without empty names and duplicates");
        Server.ResetToDefaults();
    }

    private void TestDevices()
    {
        foreach (InputSettingsServer.Device device in new[] { Keyboard, Mouse, Gamepad })
        {
            InputSettingsServer.SetSensitivity(device, 2.0f);
            InputSettingsServer.SetXInverted(device, true);
            InputSettingsServer.SetYInverted(device, true);
            ExpectEqual(InputSettingsServer.GetSensitivity(device), 2.0f, $"SetSensitivity, device {device}");
            Expect(InputSettingsServer.IsXInverted(device), $"SetXInverted, device {device}");
            Expect(InputSettingsServer.IsYInverted(device), $"SetYInverted, device {device}");
        }
        ExpectEqual(Server.GetValue("keyboard_sensitivity").AsDouble(), 2.0, "the keyboard values are the settings keyboard_*");
        ExpectEqual(Server.GetValue("gamepad_x_inverted").AsBool(), true, "the gamepad values are the settings gamepad_*");
        Server.ResetToDefaults();
    }

    private void TestHelpers()
    {
        InputSettingsServer.SetSensitivity(Mouse, 2.5f);
        InputSettingsServer.SetYInverted(Mouse, true);
        ExpectEqual(InputSettingsServer.ApplyToMouseMotion(new Vector2(2, 4)), new Vector2(5, -10), "ApplyToMouseMotion: sensitivity and Y inversion");
        var motion = new InputEventMouseMotion();
        motion.ScreenRelative = new Vector2(2, 4);
        ExpectEqual(InputSettingsServer.GetLookVector(motion), new Vector2(5, -10), "GetLookVector of a mouse motion");
        ExpectEqual(InputSettingsServer.GetLookVector(new InputEventKey()), Vector2.Zero, "GetLookVector of a key event is zero");
        InputSettingsServer.SetSensitivity(Keyboard, 2.0f);
        InputSettingsServer.SetXInverted(Keyboard, true);
        ExpectEqual(InputSettingsServer.ApplyToVector(Keyboard, new Vector2(1, 1)), new Vector2(-2, 2), "ApplyToVector for the keyboard");
        ExpectEqual(InputSettingsServer.ApplyToStick(new Vector2(0.1f, 0.1f)), Vector2.Zero, "ApplyToStick: inside the deadzone");
        Expect(InputSettingsServer.ApplyToStick(new Vector2(0, 0.6f)).IsEqualApprox(new Vector2(0, 0.5f)), "ApplyToStick: rescaled outside the deadzone");
        InputSettingsServer.SetYInverted(Gamepad, true);
        InputSettingsServer.SetSensitivity(Gamepad, 3.0f);
        Expect(InputSettingsServer.ApplyToStick(new Vector2(0, 0.6f)).IsEqualApprox(new Vector2(0, -1.5f)), "ApplyToStick: gamepad sensitivity and inversion");
        Server.ResetToDefaults();
    }

    private void TestSmoothing()
    {
        InputSettingsServer.SetMouseSmoothing(0.5f);
        InputSettingsServer.ResetMouseSmoothing();
        Expect(InputSettingsServer.ApplyToMouseMotion(new Vector2(4, 0)).IsEqualApprox(new Vector2(2, 0)), "smoothing: the first motion is blended with zero");
        Expect(InputSettingsServer.ApplyToMouseMotion(new Vector2(4, 0)).IsEqualApprox(new Vector2(3, 0)), "smoothing: the second motion is blended with the first");
        InputSettingsServer.ResetMouseSmoothing();
        Expect(InputSettingsServer.ApplyToMouseMotion(new Vector2(4, 0)).IsEqualApprox(new Vector2(2, 0)), "ResetMouseSmoothing forgets the previous result");
        Server.ResetToDefaults();
    }

    private void TestPlayerEvents()
    {
        var pad = new InputEventJoypadButton();
        pad.Device = 1;
        Expect(InputSettingsServer.IsPlayerEvent(pad), "any joypad belongs to the first player by default");
        InputSettingsServer.SetFirstPlayerDeviceId(0);
        ExpectEqual(InputSettingsServer.GetFirstPlayerDeviceId(), 0, "GetFirstPlayerDeviceId");
        Expect(!InputSettingsServer.IsPlayerEvent(pad), "another joypad is not the first player");
        InputSettingsServer.SetAllowKeyboardForCurrentPlayer(false);
        Expect(!InputSettingsServer.IsKeyboardAllowedForCurrentPlayer(), "IsKeyboardAllowedForCurrentPlayer");
        Expect(!InputSettingsServer.IsPlayerEvent(new InputEventKey()), "keyboard not allowed for the first player");
        Server.ResetToDefaults();
    }

    private void TestVibration()
    {
        InputSettingsServer.SetVibrationStrength(0.5f);
        InputSettingsServer.Vibrate(0, 1.0f, 0.8f, 0.2f);
        Expect(Input.GetJoyVibrationStrength(0).IsEqualApprox(new Vector2(0.5f, 0.4f)), "Vibrate scales by the strength");
        Input.StopJoyVibration(0);
        InputSettingsServer.SetVibrationEnabled(false);
        Expect(!InputSettingsServer.IsVibrationEnabled(), "IsVibrationEnabled");
        InputSettingsServer.Vibrate(0, 1.0f, 1.0f, 0.2f);
        ExpectEqual(Input.GetJoyVibrationStrength(0), Vector2.Zero, "Vibrate does nothing when vibration is off");
        Server.ResetToDefaults();
    }

    private void TestFloatPrecision()
    {
        _events.Clear();
        SettingsServer.ValueChangedEventHandler onChanged = (name, value) => Log($"value_changed {name} {value}");
        Server.ValueChanged += onChanged;
        InputSettingsServer.SetGamepadDeadzone(0.123456f);
        Server.ValueChanged -= onChanged;
        ExpectEqual(Server.GetValue("gamepad_deadzone").AsDouble(), 0.12, "SetGamepadDeadzone snaps to the 0.01 step");
        ExpectEqual(string.Join("; ", _events), "value_changed gamepad_deadzone 0.12", "ValueChanged carries 0.12");
        InputSettingsServer.SetSensitivity(Mouse, 1.23456f);
        ExpectEqual(Server.GetValue("mouse_sensitivity").AsDouble(), 1.23, "mouse_sensitivity snaps to 0.01");
        InputSettingsServer.SetGamepadDeadzone(0.3f);
        InputSettingsServer.SetSensitivity(Mouse, 1.7f);
        Server.Apply();
        foreach (string extension in new[] { "json", "ini" })
        {
            Server.FilePath = Dir.PathJoin("input." + extension);
            ExpectEqual(Server.Save(), Error.Ok, $"Save as {extension}");
            string text = FileAccess.GetFileAsString(Server.FilePath);
            Expect(text.Contains("0.3") && text.Contains("1.7"), $"{extension}: the file holds 0.3 and 1.7");
            Expect(!text.Contains("0000000") && !text.Contains("9999999"), $"{extension}: no float noise");
            Server.ResetToDefaults();
            ExpectEqual(Server.Load(), Error.Ok, $"Load as {extension}");
            ExpectEqual(Server.GetValue("gamepad_deadzone").AsDouble(), 0.3, $"{extension}: the deadzone is 0.3 again");
            InputSettingsServer.SetGamepadDeadzone(0.3f);
            InputSettingsServer.SetSensitivity(Mouse, 1.7f);
            Server.Apply();
        }
        Server.FilePath = "";
        Server.ResetToDefaults();
    }

    private void TestSignals()
    {
        _events.Clear();
        InputSettingsServer.MouseSensitivityChanged += value => Log($"mouse_sensitivity_changed {value}");
        InputSettingsServer.MouseYInvertedChanged += value => Log($"mouse_y_inverted_changed {value}");
        InputSettingsServer.GamepadDeadzoneChanged += value => Log($"gamepad_deadzone_changed {value}");
        InputSettingsServer.VibrationStrengthChanged += value => Log($"vibration_strength_changed {value}");
        InputSettingsServer.ToggleActionsChanged += value => Log($"toggle_actions_changed {string.Join(",", value)}");
        InputSettingsServer.FirstPlayerDeviceIdChanged += value => Log($"first_player_device_id_changed {value}");
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        InputSettingsServer.SetSensitivity(Mouse, 2.0f);
        InputSettingsServer.SetYInverted(Mouse, true);
        InputSettingsServer.SetGamepadDeadzone(0.4f);
        InputSettingsServer.SetVibrationStrength(0.5f);
        InputSettingsServer.SetToggleActions(["aim"]);
        InputSettingsServer.SetFirstPlayerDeviceId(2);
        Expect(_events.Count == 0, "the typed signals do not fire for pending changes in manual mode");
        Server.Apply();
        ExpectEqual(_events.Count, 6, "every typed signal fires once on Apply");
        Expect(_events.Contains("mouse_sensitivity_changed 2"), "MouseSensitivityChanged carries the value");
        Expect(_events.Contains("mouse_y_inverted_changed True"), "MouseYInvertedChanged carries the value");
        Expect(_events.Contains("gamepad_deadzone_changed 0.4"), "GamepadDeadzoneChanged carries the value");
        Expect(_events.Contains("vibration_strength_changed 0.5"), "VibrationStrengthChanged carries the value");
        Expect(_events.Contains("toggle_actions_changed aim"), "ToggleActionsChanged carries the actions");
        Expect(_events.Contains("first_player_device_id_changed 2"), "FirstPlayerDeviceIdChanged carries the device");
        Server.Apply();
        ExpectEqual(_events.Count, 6, "the typed signals do not fire when nothing changed");
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Server.ResetToDefaults();
        ExpectEqual(_events.Count, 12, "the typed signals fire again when the defaults are applied");
    }

    private void TestEngineOptions()
    {
        bool accumulated = Input.UseAccumulatedInput;
        bool touch = Input.EmulateTouchFromMouse;
        bool mouse = Input.EmulateMouseFromTouch;
        ExpectEqual(InputSettingsServer.IsUsingAccumulatedInput(), accumulated, "the default of use_accumulated_input comes from Input");
        InputSettingsServer.SetUseAccumulatedInput(!accumulated);
        ExpectEqual(Input.UseAccumulatedInput, !accumulated, "use_accumulated_input takes effect at once");
        InputSettingsServer.SetEmulateTouchFromMouse(!touch);
        ExpectEqual(Input.EmulateTouchFromMouse, !touch, "emulate_touch_from_mouse takes effect at once");
        ExpectEqual(InputSettingsServer.IsEmulatingTouchFromMouse(), !touch, "IsEmulatingTouchFromMouse");
        InputSettingsServer.SetEmulateMouseFromTouch(!mouse);
        ExpectEqual(Input.EmulateMouseFromTouch, !mouse, "emulate_mouse_from_touch takes effect at once");
        ExpectEqual(InputSettingsServer.IsEmulatingMouseFromTouch(), !mouse, "IsEmulatingMouseFromTouch");
        Server.ResetToDefaults();
        ExpectEqual(Input.UseAccumulatedInput, accumulated, "reset gives back use_accumulated_input");
        ExpectEqual(Input.EmulateTouchFromMouse, touch, "reset gives back emulate_touch_from_mouse");
        ExpectEqual(Input.EmulateMouseFromTouch, mouse, "reset gives back emulate_mouse_from_touch");
    }

    private async Task TestToggleActions()
    {
        InputMap.AddAction("test_crouch");
        InputMap.AddAction("test_sprint");
        InputSettingsServer.SetToggleActions(["test_crouch"]);
        Expect(InputSettingsServer.IsToggleAction("test_crouch"), "IsToggleAction for a toggle action");
        Expect(!InputSettingsServer.IsToggleAction("test_sprint"), "IsToggleAction for a hold action");
        Input.ActionPress("test_crouch");
        Input.ActionPress("test_sprint");
        Expect(InputSettingsServer.IsActionActive("test_crouch"), "toggle: active after the press");
        Expect(InputSettingsServer.IsActionActive("test_sprint"), "hold: active while pressed");
        Input.ActionRelease("test_crouch");
        Input.ActionRelease("test_sprint");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(InputSettingsServer.IsActionActive("test_crouch"), "toggle: still active after the release");
        Expect(!InputSettingsServer.IsActionActive("test_sprint"), "hold: inactive after the release");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ActionPress("test_crouch");
        Expect(!InputSettingsServer.IsActionActive("test_crouch"), "toggle: the second press switches it off");
        Input.ActionRelease("test_crouch");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ActionPress("test_crouch");
        Expect(InputSettingsServer.IsActionActive("test_crouch"), "toggle: the third press switches it on");
        Input.ActionRelease("test_crouch");
        InputSettingsServer.ResetActionStates();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(!InputSettingsServer.IsActionActive("test_crouch"), "ResetActionStates switches the toggle actions off");
        Server.ResetToDefaults();
        InputMap.EraseAction("test_crouch");
        InputMap.EraseAction("test_sprint");
    }

    private void TestMouseCapture()
    {
        if (DisplayServer.GetName() == "headless")
        {
            GD.Print("  SKIP  mouse capture test (the headless display server does not keep the mouse mode)");
            return;
        }
        InputSettingsServer.SetMouseConfined(true);
        Expect(InputSettingsServer.IsMouseConfined(), "IsMouseConfined");
        InputSettingsServer.SetMouseCaptured(false);
        ExpectEqual(Input.MouseMode, Input.MouseModeEnum.Confined, "SetMouseCaptured(false) with mouse_confined: confined");
        InputSettingsServer.SetMouseCaptured(true);
        ExpectEqual(Input.MouseMode, Input.MouseModeEnum.Captured, "SetMouseCaptured(true): captured");
        InputSettingsServer.SetMouseConfined(false);
        InputSettingsServer.SetMouseCaptured(false);
        ExpectEqual(Input.MouseMode, Input.MouseModeEnum.Visible, "SetMouseCaptured(false): visible");
        Server.ResetToDefaults();
    }

    private void TestResource()
    {
        var settings = new Godot.InputSettings();
        ExpectEqual(settings.MouseSensitivity, 1.0f, "InputSettings: default mouse sensitivity");
        ExpectEqual(settings.MouseYInverted, false, "InputSettings: default Y inversion");
        ExpectEqual(settings.FirstPlayerDeviceId, -1, "InputSettings: default first player device");
        settings.MouseYInverted = true;
        settings.ToggleActions = ["aim"];
        Expect(settings.MouseYInverted, "InputSettings: MouseYInverted is a plain property");
        Expect(settings.ToggleActions.SequenceEqual(["aim"]), "InputSettings: ToggleActions");
        var sample = GD.Load<Godot.InputSettings>("res://input-settings/resources/inverted_mouse_settings.tres");
        ExpectEqual(sample.MouseSensitivity, 1.5f, "the sample InputSettings loads");
        Expect(sample.MouseYInverted, "the sample inverts the mouse Y axis");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        bool accumulated = Input.UseAccumulatedInput;
        InputSettingsServer.SetYInverted(Mouse, true);
        InputSettingsServer.SetSensitivity(Mouse, 2.5f);
        InputSettingsServer.SetUseAccumulatedInput(!accumulated);
        ExpectEqual(Server.Save(), Error.Ok, "Save writes the player file");
        string text = FileAccess.GetFileAsString(InputFile);
        Expect(text.Contains("mouse_y_inverted"), "the INI file contains mouse_y_inverted");
        Expect(text.Contains("mouse_sensitivity"), "the INI file contains mouse_sensitivity");
        Godot.Collections.Dictionary probe = RunProbe();
        Expect(probe["started"].AsBool(), "restart: the domain was started");
        Expect(probe["y_inverted"].AsBool(), "restart: Y inversion is restored");
        ExpectEqual(probe["sensitivity"].AsDouble(), 2.5, "restart: mouse sensitivity is restored");
        ExpectEqual(new Vector2(probe["look_x"].AsSingle(), probe["look_y"].AsSingle()), new Vector2(5, -10), "restart: the helper returns the changed values");
        ExpectEqual(probe["use_accumulated_input"].AsBool(), !accumulated, "restart: use_accumulated_input is applied before the main scene");
        Server.ResetToDefaults();
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
