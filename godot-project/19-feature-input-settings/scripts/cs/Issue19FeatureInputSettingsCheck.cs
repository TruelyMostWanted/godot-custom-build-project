using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue19FeatureInputSettings;

/// <summary>
/// C# checks of 19-feature-input-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The restart check starts the project again as its own process (desktop only).
/// </summary>
public partial class Issue19FeatureInputSettingsCheck : FeatureCheckCs
{
    private const string ProbeScene = "res://19-feature-input-settings/scenes/restart_probe.tscn";
    private const string InputFile = "user://settings/input.ini";

    protected override string CheckName => "19-feature-input-settings";

    protected override async Task RunChecks()
    {
        var server = InputSettingsServer.Singleton;
        Expect(Engine.HasSingleton("InputSettingsServer"), "InputSettingsServer is a registered singleton");
        Expect(server is InputSettingsServerInstance, "InputSettingsServer.Singleton is the instance type");
        Expect(server is SettingsServer, "InputSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("input") == server, "the domain is registered as \"input\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetFilePath(), InputFile, "file path");
        Expect(server.GetSettingNames().Contains("mouse_sensitivity"), "setting names contain mouse_sensitivity");
        Expect(server.GetSettingFlags("mouse_sensitivity").HasFlag(SettingsServer.SettingFlags.NoEngineTarget), "mouse_sensitivity has no engine target");

        server.ResetToDefaults();
        CheckDefaults();
        CheckValidation();
        CheckHelpers();
        CheckSignal();
        CheckEngineOptions();
        await CheckToggleActions();
        CheckMouseCapture();
        CheckResource();
        CheckRestart();

        // Leave no player files behind for later runs.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(InputFile);
    }

    private void CheckDefaults()
    {
        foreach (var device in new[] { InputSettingsServer.Device.Keyboard, InputSettingsServer.Device.Mouse, InputSettingsServer.Device.Gamepad })
        {
            ExpectEqual(InputSettingsServer.GetSensitivity(device), 1.0f, $"default sensitivity of {device}");
            Expect(!InputSettingsServer.IsXInverted(device), $"default: x not inverted, {device}");
            Expect(!InputSettingsServer.IsYInverted(device), $"default: y not inverted, {device}");
        }
        Expect(Mathf.IsEqualApprox(InputSettingsServer.GetGamepadDeadzone(), 0.2f), "default deadzone 0.2");
        Expect(InputSettingsServer.IsVibrationEnabled(), "default: vibration on");
        ExpectEqual(InputSettingsServer.GetVibrationStrength(), 1.0f, "default vibration strength");
        ExpectEqual(InputSettingsServer.GetMouseSmoothing(), 0.0f, "default: no smoothing");
        ExpectEqual(InputSettingsServer.GetToggleActions().Length, 0, "default: no toggle actions");
        ExpectEqual(InputSettingsServer.GetFirstPlayerDeviceId(), -1, "default: any device");
        Expect(InputSettingsServer.IsKeyboardAllowedForCurrentPlayer(), "default: keyboard allowed");
    }

    private void CheckValidation()
    {
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Mouse, 50.0f);
        ExpectEqual(InputSettingsServer.GetSensitivity(InputSettingsServer.Device.Mouse), 10.0f, "sensitivity is clamped to 10");
        InputSettingsServer.SetGamepadDeadzone(3.0f);
        Expect(Mathf.IsEqualApprox(InputSettingsServer.GetGamepadDeadzone(), 0.95f), "deadzone is clamped to 0.95");
        InputSettingsServer.SetToggleActions(["crouch", "", "aim ", "crouch"]);
        ExpectEqual(string.Join(",", InputSettingsServer.GetToggleActions()), "crouch,aim", "toggle actions without empty names and duplicates");
        InputSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckHelpers()
    {
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Mouse, 2.5f);
        InputSettingsServer.SetYInverted(InputSettingsServer.Device.Mouse, true);
        ExpectEqual(InputSettingsServer.ApplyToMouseMotion(new Vector2(2, 4)), new Vector2(5, -10), "ApplyToMouseMotion(): sensitivity and Y inversion");
        var motion = new InputEventMouseMotion { ScreenRelative = new Vector2(2, 4) };
        ExpectEqual(InputSettingsServer.GetLookVector(motion), new Vector2(5, -10), "GetLookVector() of a mouse motion");
        ExpectEqual(InputSettingsServer.GetLookVector(new InputEventKey()), Vector2.Zero, "GetLookVector() of a key event is zero");

        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Keyboard, 2.0f);
        InputSettingsServer.SetXInverted(InputSettingsServer.Device.Keyboard, true);
        ExpectEqual(InputSettingsServer.ApplyToVector(InputSettingsServer.Device.Keyboard, new Vector2(1, 1)), new Vector2(-2, 2), "ApplyToVector() for the keyboard");

        ExpectEqual(InputSettingsServer.ApplyToStick(new Vector2(0.1f, 0.1f)), Vector2.Zero, "ApplyToStick(): inside the deadzone");
        Expect(InputSettingsServer.ApplyToStick(new Vector2(0, 0.6f)).IsEqualApprox(new Vector2(0, 0.5f)), "ApplyToStick(): rescaled outside the deadzone");
        InputSettingsServer.SetYInverted(InputSettingsServer.Device.Gamepad, true);
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Gamepad, 3.0f);
        Expect(InputSettingsServer.ApplyToStick(new Vector2(0, 0.6f)).IsEqualApprox(new Vector2(0, -1.5f)), "ApplyToStick(): gamepad sensitivity and inversion");

        InputSettingsServer.SetMouseSmoothing(0.5f);
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Mouse, 1.0f);
        InputSettingsServer.SetYInverted(InputSettingsServer.Device.Mouse, false);
        InputSettingsServer.ResetMouseSmoothing();
        Expect(InputSettingsServer.ApplyToMouseMotion(new Vector2(4, 0)).IsEqualApprox(new Vector2(2, 0)), "smoothing: first motion is blended with zero");
        Expect(InputSettingsServer.ApplyToMouseMotion(new Vector2(4, 0)).IsEqualApprox(new Vector2(3, 0)), "smoothing: second motion is blended with the first");

        var pad = new InputEventJoypadButton { Device = 1 };
        Expect(InputSettingsServer.IsPlayerEvent(pad), "any joypad belongs to the first player by default");
        InputSettingsServer.SetFirstPlayerDeviceId(0);
        Expect(!InputSettingsServer.IsPlayerEvent(pad), "another joypad is not the first player");
        InputSettingsServer.SetAllowKeyboardForCurrentPlayer(false);
        Expect(!InputSettingsServer.IsPlayerEvent(new InputEventKey()), "keyboard not allowed for the first player");

        InputSettingsServer.SetVibrationStrength(0.5f);
        InputSettingsServer.Vibrate(0, 1.0f, 0.8f, 0.2f);
        Expect(Input.GetJoyVibrationStrength(0).IsEqualApprox(new Vector2(0.5f, 0.4f)), "Vibrate() scales by the strength");
        Input.StopJoyVibration(0);
        InputSettingsServer.SetVibrationEnabled(false);
        InputSettingsServer.Vibrate(0, 1.0f, 1.0f, 0.2f);
        ExpectEqual(Input.GetJoyVibrationStrength(0), Vector2.Zero, "Vibrate() does nothing when vibration is off");
        InputSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckSignal()
    {
        var server = InputSettingsServer.Singleton;
        string changes = "";
        void OnChanged(StringName name, Variant value) => changes += $"{name}={value.AsBool()};";
        server.ValueChanged += OnChanged;
        InputSettingsServer.SetYInverted(InputSettingsServer.Device.Gamepad, true);
        server.ValueChanged -= OnChanged;
        ExpectEqual(changes, "gamepad_y_inverted=True;", "ValueChanged for a game-side value");
        server.ResetToDefaults();
    }

    private void CheckEngineOptions()
    {
        bool accumulated = Input.UseAccumulatedInput;
        bool touch = Input.EmulateTouchFromMouse;
        bool mouse = Input.EmulateMouseFromTouch;
        ExpectEqual(InputSettingsServer.IsUsingAccumulatedInput(), accumulated, "the default of use_accumulated_input comes from Input");
        InputSettingsServer.SetUseAccumulatedInput(!accumulated);
        ExpectEqual(Input.UseAccumulatedInput, !accumulated, "use_accumulated_input takes effect at once");
        InputSettingsServer.SetEmulateTouchFromMouse(!touch);
        ExpectEqual(Input.EmulateTouchFromMouse, !touch, "emulate_touch_from_mouse takes effect at once");
        InputSettingsServer.SetEmulateMouseFromTouch(!mouse);
        ExpectEqual(Input.EmulateMouseFromTouch, !mouse, "emulate_mouse_from_touch takes effect at once");
        InputSettingsServer.Singleton.ResetToDefaults();
        ExpectEqual(Input.UseAccumulatedInput, accumulated, "reset gives back use_accumulated_input");
        ExpectEqual(Input.EmulateTouchFromMouse, touch, "reset gives back emulate_touch_from_mouse");
        ExpectEqual(Input.EmulateMouseFromTouch, mouse, "reset gives back emulate_mouse_from_touch");
    }

    private async Task CheckToggleActions()
    {
        InputMap.AddAction("check19_crouch");
        InputMap.AddAction("check19_sprint");
        InputSettingsServer.SetToggleActions(["check19_crouch"]);
        Expect(InputSettingsServer.IsToggleAction("check19_crouch"), "IsToggleAction()");

        Input.ActionPress("check19_crouch");
        Input.ActionPress("check19_sprint");
        Expect(InputSettingsServer.IsActionActive("check19_crouch"), "toggle: active after the press");
        Expect(InputSettingsServer.IsActionActive("check19_sprint"), "hold: active while pressed");
        Input.ActionRelease("check19_crouch");
        Input.ActionRelease("check19_sprint");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(InputSettingsServer.IsActionActive("check19_crouch"), "toggle: still active after the release");
        Expect(!InputSettingsServer.IsActionActive("check19_sprint"), "hold: inactive after the release");
        // A new frame, as in a game: the press arrives before the frame is processed.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ActionPress("check19_crouch");
        Expect(!InputSettingsServer.IsActionActive("check19_crouch"), "toggle: the second press switches it off");
        Input.ActionRelease("check19_crouch");

        InputSettingsServer.Singleton.ResetToDefaults();
        InputMap.EraseAction("check19_crouch");
        InputMap.EraseAction("check19_sprint");
    }

    private void CheckMouseCapture()
    {
        if (DisplayServer.GetName() == "headless")
        {
            GD.Print("  SKIP  mouse capture check (the headless display server does not keep the mouse mode)");
            return;
        }
        InputSettingsServer.SetMouseConfined(true);
        InputSettingsServer.SetMouseCaptured(false);
        ExpectEqual(Input.MouseMode, Input.MouseModeEnum.Confined, "SetMouseCaptured(false) with mouse_confined: confined");
        InputSettingsServer.SetMouseConfined(false);
        InputSettingsServer.SetMouseCaptured(false);
        ExpectEqual(Input.MouseMode, Input.MouseModeEnum.Visible, "SetMouseCaptured(false): visible");
        InputSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckResource()
    {
        var settings = new InputSettings();
        ExpectEqual(settings.MouseSensitivity, 1.0f, "InputSettings: default mouse sensitivity");
        ExpectEqual(settings.MouseYInverted, false, "InputSettings: default Y inversion");
        settings.MouseYInverted = true;
        Expect(settings.MouseYInverted, "InputSettings: MouseYInverted is a plain property");
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = InputSettingsServer.Singleton;
        bool accumulated = Input.UseAccumulatedInput;
        InputSettingsServer.SetYInverted(InputSettingsServer.Device.Mouse, true);
        InputSettingsServer.SetSensitivity(InputSettingsServer.Device.Mouse, 2.5f);
        InputSettingsServer.SetUseAccumulatedInput(!accumulated);
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        string text = FileAccess.GetFileAsString(InputFile);
        Expect(text.Contains("mouse_y_inverted"), "the INI file contains mouse_y_inverted");
        Expect(text.Contains("mouse_sensitivity"), "the INI file contains mouse_sensitivity");

        var probe = RunProbe();
        Expect(probe.ContainsKey("started") && probe["started"].AsBool(), "restart: the domain was started");
        Expect(probe.ContainsKey("y_inverted") && probe["y_inverted"].AsBool(), "restart: Y inversion is restored");
        Expect(probe.ContainsKey("sensitivity") && Mathf.IsEqualApprox(probe["sensitivity"].AsSingle(), 2.5f), "restart: mouse sensitivity is restored");
        var look = new Vector2(probe.ContainsKey("look_x") ? probe["look_x"].AsSingle() : 0, probe.ContainsKey("look_y") ? probe["look_y"].AsSingle() : 0);
        ExpectEqual(look, new Vector2(5, -10), "restart: the helper returns the changed values");
        Expect(probe.ContainsKey("use_accumulated_input") && probe["use_accumulated_input"].AsBool() == !accumulated, "restart: use_accumulated_input is applied before the main scene");
        server.ResetToDefaults();
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
                if (line.StartsWith("CHECK19_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK19_PROBE ".Length..]);
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
