using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue18FeatureKeyBindings;

/// <summary>
/// C# checks of 18-feature-key-bindings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base classes through
/// <c>Singleton</c>. The project defines kb18_jump (Space, joypad A, deadzone 0.5) and kb18_fire (left mouse
/// button, right trigger). The restart check starts the project again as its own process (desktop only).
/// </summary>
public partial class Issue18FeatureKeyBindingsCheck : FeatureCheckCs
{
    private const string ProbeScene = "res://18-feature-key-bindings/scenes/restart_probe.tscn";
    private const string InputMapFile = "user://settings/input_map.ini";
    private static readonly StringName Jump = "kb18_jump";
    private static readonly StringName Fire = "kb18_fire";

    protected override string CheckName => "18-feature-key-bindings";

    protected override async Task RunChecks()
    {
        var server = InputMapSettingsServer.Singleton;
        Expect(Engine.HasSingleton("InputMapSettingsServer"), "InputMapSettingsServer is a registered singleton");
        Expect(server is InputMapSettingsServerInstance, "InputMapSettingsServer.Singleton is the instance type");
        Expect(server is ListSettingsServer, "InputMapSettingsServerInstance derives from ListSettingsServer");
        Expect(SettingsServerManager.GetServer("input_map") == server, "the domain is registered as \"input_map\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetFilePath(), InputMapFile, "file path");
        Expect(server.GetDefaultSettings() is InputActions, "the settings resource is an InputActions");

        InputMapSettingsServer.ResetAll();
        CheckTextForm();
        CheckDefaults();
        CheckRebind();
        CheckConflicts();
        CheckReset();
        CheckFormats();
        CheckRestart();

        // Leave no player files behind for later runs.
        InputMapSettingsServer.ResetAll();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(InputMapFile);
    }

    private static InputEventKey PressedKey(Key keycode)
    {
        return new InputEventKey { Keycode = keycode, PhysicalKeycode = keycode, KeyLabel = keycode, Pressed = true };
    }

    private static bool IsAction(InputEvent @event, StringName action)
    {
        return InputMap.EventIsAction(@event, action);
    }

    private void CheckTextForm()
    {
        string[] texts = ["Key:Ctrl+S", "PhysicalKey:W", "KeyLabel:Shift+A", "Key:CmdOrCtrl+Z", "MouseButton:Left", "MouseButton:Ctrl+WheelUp",
            "JoypadButton:A", "JoypadButton:LeftShoulder", "JoypadMotion:LeftX+", "JoypadMotion:TriggerRight+"];
        foreach (string text in texts)
        {
            InputEvent @event = InputAction.TextToEvent(text);
            Expect(@event != null, $"TextToEvent(\"{text}\") gives an event");
            if (@event != null)
            {
                ExpectEqual(InputAction.EventToText(@event), text, $"round trip of \"{text}\"");
            }
        }
        Expect(InputAction.TextToEvent("Key:Hyper+S") == null, "an unknown modifier is not valid");
        Expect(InputAction.TextToEvent("Joypad:A") == null, "an unknown type is not valid");
        ExpectEqual(InputAction.EventToText(PressedKey(Key.W)), "PhysicalKey:W", "a pressed key is stored by its physical keycode");
        ExpectEqual(InputAction.EventToText(new InputEventScreenTouch()), "", "a touch can't be bound");
        ExpectEqual(InputAction.GetEventDevice(new InputEventScreenTouch()), InputAction.InputDevice.Max, "a touch has no device");
        ExpectEqual(InputAction.GetEventDevice(new InputEventJoypadMotion()), InputAction.InputDevice.Gamepad, "a joypad axis is a gamepad event");
        ExpectEqual(InputMapSettingsServer.GetEventLabel(InputAction.TextToEvent("Key:Ctrl+S")), "Ctrl+S", "label of Ctrl+S");
        ExpectEqual(InputMapSettingsServer.GetEventLabel(InputAction.TextToEvent("MouseButton:Right")), "Right Mouse Button", "label of a mouse button");
        ExpectEqual(InputMapSettingsServer.GetEventLabel(InputAction.TextToEvent("JoypadMotion:LeftX+")), "Left Stick Right", "label of a stick direction");

        var entry = new InputAction { Events = InputMapSettingsServer.TextToEvents("JoypadButton:B; Key:Space") };
        ExpectEqual(entry.GetEventCount(), 2, "the text gives one event per entry");
        ExpectEqual(entry.GetDeviceEvents(InputAction.InputDevice.Gamepad).Count, 1, "the gamepad event is found by device");
        Expect(entry.Events[0] is InputEventJoypadButton, "the first event is parsed from the text");
        ExpectEqual(InputMapSettingsServer.EventsToText(entry.Events), "JoypadButton:B; Key:Space", "text round trip");
    }

    private void CheckDefaults()
    {
        string[] actions = InputMapSettingsServer.GetActionNames();
        Expect(actions.Contains("kb18_jump") && actions.Contains("kb18_fire"), "the project actions are managed");
        Expect(!actions.Contains("ui_accept"), "built-in ui_* actions are left out by default");
        ExpectEqual(InputMapSettingsServer.Singleton.Get("save_mode").AsInt32(), 0, "save_mode is CUSTOM_ONLY by default");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Jump, InputAction.InputDevice.Keyboard, 0)), "Key:Space", "jump: keyboard slot 0 is Space");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Jump, InputAction.InputDevice.Gamepad, 0)), "JoypadButton:A", "jump: gamepad slot 0 is A");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Fire, InputAction.InputDevice.Gamepad, 0)), "JoypadMotion:TriggerRight+", "fire: gamepad slot 0 is the right trigger");
        Expect(Mathf.IsEqualApprox(InputMapSettingsServer.GetActionDeadzone(Jump), 0.5f), "jump: deadzone 0.5 from the project");
        ExpectEqual(InputMapSettingsServer.GetEvents(Jump).Count, 2, "jump has two events");
    }

    private void CheckRebind()
    {
        ExpectEqual(InputMapSettingsServer.Rebind(Jump, PressedKey(Key.W)), Error.Ok, "Rebind() of keyboard slot 0");
        ExpectEqual(InputMapSettingsServer.Rebind(Jump, PressedKey(Key.Up), 1), Error.Ok, "Rebind() of keyboard slot 1");
        Expect(IsAction(PressedKey(Key.W), Jump), "the new key works at once");
        Expect(IsAction(PressedKey(Key.Up), Jump), "the second slot works");
        Expect(!IsAction(PressedKey(Key.Space), Jump), "the old key no longer works");
        ExpectEqual(InputMapSettingsServer.GetSlotCount(Jump, InputAction.InputDevice.Keyboard), 2, "two keyboard slots");

        InputMapSettingsServer.ClearEvent(Jump, InputAction.InputDevice.Keyboard, 1);
        Expect(!IsAction(PressedKey(Key.Up), Jump), "a cleared slot no longer works");
        Expect(InputMapSettingsServer.GetEvent(Jump, InputAction.InputDevice.Keyboard, 1) == null, "a cleared slot has no event");

        // Clearing a slot removes the event: the later slots of the device move up.
        InputMapSettingsServer.Rebind(Jump, PressedKey(Key.Up), 1);
        InputMapSettingsServer.ClearEvent(Jump, InputAction.InputDevice.Keyboard, 0);
        ExpectEqual(InputMapSettingsServer.GetSlotCount(Jump, InputAction.InputDevice.Keyboard), 1, "clearing slot 0 leaves one keyboard slot");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Jump, InputAction.InputDevice.Keyboard, 0)), "PhysicalKey:Up", "slot 1 moved up to slot 0");
        Expect(IsAction(PressedKey(Key.Up), Jump), "the moved key still works");
        Expect(!IsAction(PressedKey(Key.W), Jump), "the cleared key no longer works");

        InputMapSettingsServer.SetActionDeadzone(Jump, 0.7f);
        Expect(Mathf.IsEqualApprox(InputMap.ActionGetDeadzone(Jump), 0.7f), "the deadzone reaches InputMap");
        ExpectEqual(InputMapSettingsServer.Rebind(Jump, new InputEventScreenTouch()), Error.InvalidParameter, "a touch can't be bound");
        ExpectEqual(InputMapSettingsServer.Rebind("kb18_no_such_action", PressedKey(Key.Q)), Error.DoesNotExist, "an unknown action can't be bound");
    }

    private void CheckConflicts()
    {
        InputMapSettingsServer.ResetAll();
        string[] conflicts = InputMapSettingsServer.FindConflicts(PressedKey(Key.Space));
        Expect(conflicts.Length == 1 && conflicts[0] == "kb18_jump", "Space is used by jump");
        Expect(InputMapSettingsServer.FindConflicts(PressedKey(Key.Space), Jump).Length == 0, "the ignored action is not a conflict");
        Expect(InputMapSettingsServer.FindConflicts(PressedKey(Key.K)).Length == 0, "K is free");
        InputMapSettingsServer.Rebind(Fire, PressedKey(Key.Space));
        ExpectEqual(InputMapSettingsServer.FindConflicts(PressedKey(Key.Space)).Length, 2, "after rebinding fire, Space is used twice");
    }

    private void CheckReset()
    {
        InputMapSettingsServer.Rebind(Jump, PressedKey(Key.W));
        InputMapSettingsServer.ResetAction(Jump);
        Expect(IsAction(PressedKey(Key.Space), Jump), "ResetAction(): Space works again");
        Expect(!IsAction(PressedKey(Key.W), Jump), "ResetAction(): W no longer works");
        Expect(IsAction(PressedKey(Key.Space), Fire), "ResetAction() keeps the other actions");
        InputMapSettingsServer.ResetAll();
        Expect(!IsAction(PressedKey(Key.Space), Fire), "ResetAll(): fire is back at the project bindings");
        Expect(IsAction(new InputEventMouseButton { ButtonIndex = MouseButton.Left }, Fire), "ResetAll(): the left mouse button fires again");
    }

    private void CheckFormats()
    {
        var server = InputMapSettingsServer.Singleton;
        foreach (string extension in new[] { "ini", "json", "csv" })
        {
            string path = $"user://kb18_bindings_cs.{extension}";
            server.SetFilePath(path);
            InputMapSettingsServer.Rebind(Jump, PressedKey(Key.W));
            InputMapSettingsServer.Rebind(Jump, InputAction.TextToEvent("JoypadMotion:LeftY-"));
            InputMapSettingsServer.Rebind(Fire, InputAction.TextToEvent("MouseButton:Ctrl+Right"));
            ExpectEqual(server.Save(), Error.Ok, $"{extension}: Save()");
            string text = FileAccess.GetFileAsString(path);
            Expect(text.Contains("PhysicalKey:W") && text.Contains("JoypadMotion:LeftY-") && text.Contains("MouseButton:Ctrl+Right"), $"{extension}: the file holds the event texts");
            InputMapSettingsServer.ResetAll();
            ExpectEqual(server.Load(), Error.Ok, $"{extension}: Load()");
            server.Apply();
            Expect(IsAction(PressedKey(Key.W), Jump), $"{extension}: key survives the round trip");
            Expect(IsAction(new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1.0f }, Jump), $"{extension}: joypad axis survives the round trip");
            Expect(IsAction(new InputEventMouseButton { ButtonIndex = MouseButton.Right, CtrlPressed = true }, Fire), $"{extension}: mouse button survives the round trip");
            Expect(!IsAction(new InputEventJoypadButton { ButtonIndex = JoyButton.A }, Jump), $"{extension}: the replaced joypad button no longer works");
            InputMapSettingsServer.ResetAll();
            DirAccess.RemoveAbsolute(path);
        }
        server.SetFilePath("");
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = InputMapSettingsServer.Singleton;
        InputMapSettingsServer.Rebind(Jump, PressedKey(Key.W));
        // A row of an action that a later game version no longer has.
        server.AddEntry(new InputAction { Action = "kb18_removed", Events = InputMapSettingsServer.TextToEvents("Key:G") });
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        string text = FileAccess.GetFileAsString(InputMapFile);
        Expect(text.Contains("PhysicalKey:W"), "the INI file contains PhysicalKey:W");
        Expect(text.Contains("kb18_removed"), "the INI file contains the removed action");

        var probe = RunProbe();
        Expect(probe.ContainsKey("started") && probe["started"].AsBool(), "restart: the domain was started");
        Expect(probe.ContainsKey("w_jumps") && probe["w_jumps"].AsBool(), "restart: the new key works");
        Expect(probe.ContainsKey("space_jumps") && !probe["space_jumps"].AsBool(), "restart: the old key does not");
        Expect(probe.ContainsKey("has_removed") && !probe["has_removed"].AsBool(), "restart: the removed action is skipped");
        Expect(probe.ContainsKey("pad_a_jumps") && probe["pad_a_jumps"].AsBool(), "restart: the other slots keep their bindings");
        InputMapSettingsServer.ResetAll();
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
                if (line.StartsWith("CHECK18_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK18_PROBE ".Length..]);
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
