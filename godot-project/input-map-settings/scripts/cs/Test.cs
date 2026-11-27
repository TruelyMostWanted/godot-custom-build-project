using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.InputMapSettings;

/// <summary>
/// Full API test of Input Map Settings: InputMapSettingsServer, InputActionRebinder, InputAction, InputActions, the InputMap layers
/// and the file records. Same tests as scripts/gd/Test.gd.
/// The project defines the actions jump (Space, pad A, deadzone 0.5), fire (left mouse button, right trigger), interact (E, pad X),
/// test_jump (G, Paddle1), test_fire (left mouse button) and test_attack (F, deadzone 0.3) in project.godot.
/// Keys and buttons of the rebinder tests are chosen so that no built-in ui_* action uses them.
/// </summary>
public partial class Test : FeatureTest
{
    private const string ProbeScene = "res://input-map-settings/scenes/restart_probe.tscn";
    private const string InputMapFile = "user://settings/input_map.ini";
    private const string Dir = "user://input-map-settings-test-cs";
    private const string SavePath = "user://input-map-settings-test-cs/input_map.ini";
    private static readonly StringName Jump = "jump";
    private static readonly StringName Fire = "fire";
    private static readonly StringName Attack = "test_attack";
    private static readonly StringName RebindJump = "test_jump";
    private static readonly StringName RebindFire = "test_fire";
    private const int BulkCount = 100;
    private const int BulkFirstKey = 0x4E00; // Unicode keycodes no other action uses.

    private readonly List<string> _log = [];

    private static InputMapSettingsServerInstance Server => InputMapSettingsServer.Singleton;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        InputMapSettingsServer.ResetAll();
        TestRegistration();
        TestTextForm();
        TestInputAction();
        TestInputActions();
        TestResourceFiles();
        TestLayers();
        TestSetInputAction();
        TestParseCost();
        TestSettingsServer();
        InputMapSettingsServer.ResetAll();
        TestServerApi();
        TestDefaults();
        TestRebind();
        TestConflicts();
        TestReset();
        TestFormats();
        TestFileLayout();
        TestRecords();
        TestClass();
        Setup();
        TestFiltering();
        Setup();
        TestReject();
        Setup();
        TestSwapOrder();
        Setup();
        TestAllow();
        Setup();
        TestAsk();
        Setup();
        TestTwoRebinders();
        Setup();
        TestBuiltinConflict();
        Setup();
        TestSaveLoad();
        TestRestart();
        InputMapSettingsServer.ResetAll();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(InputMapFile);
        DirAccess.RemoveAbsolute("user://demo_actions.tres");
        DirAccess.RemoveAbsolute("user://demo_actions.res");
        foreach (string fileName in DirAccess.GetFilesAt(Dir))
        {
            DirAccess.RemoveAbsolute(Dir.PathJoin(fileName));
        }
        DirAccess.RemoveAbsolute(Dir);
    }

    private void TestRegistration()
    {
        Expect(Engine.HasSingleton("InputMapSettingsServer"), "InputMapSettingsServer is a registered singleton");
        Expect(Server is ListSettingsServer, "InputMapSettingsServer is a ListSettingsServer");
        Expect(SettingsServerManager.GetServer("input_map") == Server, "the domain is registered as input_map");
        Expect(Server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(Server.FilePath, InputMapFile, "file path");
        Expect(Server.GetDefaultSettings() is InputActions, "the settings resource is an InputActions");
        ExpectEqual((int)InputMapSettingsServer.SaveModeEnum.CustomOnly, 0, "SaveModeEnum.CustomOnly is 0");
        ExpectEqual((int)InputMapSettingsServer.SaveModeEnum.All, 1, "SaveModeEnum.All is 1");
        ExpectEqual((int)InputAction.InputDevice.Keyboard, 0, "InputDevice.Keyboard is 0");
        ExpectEqual((int)InputAction.InputDevice.Mouse, 1, "InputDevice.Mouse is 1");
        ExpectEqual((int)InputAction.InputDevice.Gamepad, 2, "InputDevice.Gamepad is 2");
        ExpectEqual((int)InputAction.InputDevice.Max, 3, "InputDevice.Max is 3");
        ExpectEqual((int)InputMap.InputActionLayer.Builtin, 0, "InputActionLayer.Builtin is 0");
        ExpectEqual((int)InputMap.InputActionLayer.Custom, 1, "InputActionLayer.Custom is 1");
        ExpectEqual((int)InputActionRebinder.DeviceFilterEnum.Any, 0, "DeviceFilterEnum.Any is 0");
        ExpectEqual((int)InputActionRebinder.DeviceFilterEnum.KeyboardMouse, 1, "DeviceFilterEnum.KeyboardMouse is 1");
        ExpectEqual((int)InputActionRebinder.DeviceFilterEnum.Gamepad, 2, "DeviceFilterEnum.Gamepad is 2");
        ExpectEqual((int)InputActionRebinder.ConflictModeEnum.Ask, 0, "ConflictModeEnum.Ask is 0");
        ExpectEqual((int)InputActionRebinder.ConflictModeEnum.Reject, 1, "ConflictModeEnum.Reject is 1");
        ExpectEqual((int)InputActionRebinder.ConflictModeEnum.Swap, 2, "ConflictModeEnum.Swap is 2");
        ExpectEqual((int)InputActionRebinder.ConflictModeEnum.Allow, 3, "ConflictModeEnum.Allow is 3");
    }

    private void TestServerApi()
    {
        string[] names = InputMapSettingsServer.GetActionNames();
        Expect(names.Contains("jump") && names.Contains("fire") && names.Contains("interact"), "GetActionNames lists the project actions");
        Expect(InputMapSettingsServer.HasAction(Jump), "HasAction for a project action");
        Expect(!InputMapSettingsServer.HasAction("no_such_action"), "HasAction for an unknown action");
        InputEventKey added = PressedKey(Key.Z);
        ExpectEqual(InputMapSettingsServer.AddEvent(Jump, added), Error.Ok, "AddEvent adds a binding");
        ExpectEqual(InputMapSettingsServer.GetEvents(Jump).Count, 3, "jump has three events");
        Expect(IsAction(PressedKey(Key.Z), Jump), "the added key works at once");
        ExpectEqual(InputMapSettingsServer.RemoveEvent(Jump, added), Error.Ok, "RemoveEvent removes the binding");
        ExpectEqual(InputMapSettingsServer.GetEvents(Jump).Count, 2, "jump has two events again");
        Expect(!IsAction(PressedKey(Key.Z), Jump), "the removed key no longer works");
        ExpectEqual(InputMapSettingsServer.AddEvent("no_such_action", added), Error.DoesNotExist, "AddEvent to an unknown action fails");
        InputAction copy = InputMapSettingsServer.GetInputAction(Jump);
        ExpectEqual(copy.Action.ToString(), "jump", "GetInputAction returns the action");
        Expect(copy != InputMap.GetInputAction(Jump), "GetInputAction is a copy of the pending settings");
        Expect(InputMapSettingsServer.GetInputAction("no_such_action") == null, "GetInputAction of an unknown action is null");
        ExpectEqual(InputMapSettingsServer.GetSlotCount(Jump, InputAction.InputDevice.Mouse), 0, "jump has no mouse slot");
        InputMapSettingsServer.SetActionDeadzone(Jump, 0.8f);
        Expect(Mathf.IsEqualApprox(InputMapSettingsServer.GetActionDeadzone(Jump), 0.8f), "GetActionDeadzone");
        InputMapSettingsServer.ResetAll();
    }

    private void TestRecords()
    {
        var record = new InputMapSettingsEvent();
        ExpectEqual(record.Device, InputMapSettingsEvent.DeviceEnum.Keyboard, "InputMapSettingsEvent: default device");
        ExpectEqual(record.KeyMode, InputMapSettingsEvent.KeyModeEnum.Physical, "InputMapSettingsEvent: default key mode");
        ExpectEqual(record.DeviceId, "Any", "InputMapSettingsEvent: default DeviceId");
        ExpectEqual((int)InputMapSettingsEvent.DeviceEnum.Mouse, 1, "DeviceEnum.Mouse is 1");
        ExpectEqual((int)InputMapSettingsEvent.KeyModeEnum.Label, 1, "KeyModeEnum.Label is 1");
        ExpectEqual((int)InputMapSettingsEvent.KeyModeEnum.Printed, 2, "KeyModeEnum.Printed is 2");
        record.Input = "S";
        record.Modifiers = "Ctrl";
        record.KeyMode = InputMapSettingsEvent.KeyModeEnum.Label;
        ExpectEqual(record.ToText(), "KeyboardLabel:Ctrl+S", "ToText of a label key");
        InputEvent restored = record.ToEvent();
        Expect(restored is InputEventKey, "ToEvent gives a key event");
        var key = (InputEventKey)restored;
        ExpectEqual(key.Keycode, Key.S, "ToEvent: the keycode");
        Expect(key.CtrlPressed, "ToEvent: the modifier");
        Expect(new InputMapSettingsEvent().ToEvent() == null, "a record without input is not valid");
        var sample = GD.Load<InputMapSettingsEvent>("res://input-map-settings/resources/gamepad_axis_event.tres");
        ExpectEqual(sample.ToText(), "Device1:GamepadAxis:RightStickDown", "the sample InputMapSettingsEvent loads");
        var action = new InputMapSettingsAction();
        ExpectEqual(action.Layer, InputMapSettingsAction.LayerEnum.Custom, "InputMapSettingsAction: default layer");
        ExpectEqual((int)InputMapSettingsAction.LayerEnum.BuiltIn, 0, "LayerEnum.BuiltIn is 0");
        var sampleAction = GD.Load<InputMapSettingsAction>("res://input-map-settings/resources/settings_action.tres");
        ExpectEqual(sampleAction.Action.ToString(), "jump", "the sample InputMapSettingsAction loads");
        ExpectEqual(sampleAction.InputEvents.Count, 2, "the sample action has two event records");
        ExpectEqual(sampleAction.Layer, InputMapSettingsAction.LayerEnum.Custom, "the sample action is on the custom layer");
        var actions = new InputMapSettingsActions();
        actions.Actions.Add(sampleAction);
        ExpectEqual(actions.Actions.Count, 1, "InputMapSettingsActions holds actions");
        var row = GD.Load<InputMapSettingsEventRow>("res://input-map-settings/resources/settings_event_row.tres");
        ExpectEqual(row.Input, "B", "the sample InputMapSettingsEventRow loads");
        ExpectEqual(row.Device, InputMapSettingsEvent.DeviceEnum.Gamepad, "the event row has a device");
        ExpectEqual(row.Index, 0, "the event row has an index");
        ExpectEqual(row.KeyMode, InputMapSettingsEvent.KeyModeEnum.Physical, "the event row has a key mode");
        ExpectEqual(row.Modifiers, "", "the event row has modifiers");
        ExpectEqual(row.DeviceId, "Any", "the event row has a DeviceId");
        Expect(Mathf.IsEqualApprox(row.Deadzone, 0.2f), "the event row has a deadzone");
        var rows = new InputMapSettingsEventRows();
        rows.Actions.Add(row);
        ExpectEqual(rows.Actions.Count, 1, "InputMapSettingsEventRows holds rows");
        var textRow = GD.Load<InputMapSettingsRow>("res://input-map-settings/resources/settings_row.tres");
        ExpectEqual(textRow.Bindings, "Keyboard:W; Gamepad:B", "the sample InputMapSettingsRow loads");
        Expect(Mathf.IsEqualApprox(textRow.Deadzone, 0.2f), "the row has a deadzone");
        var textRows = new InputMapSettingsRows();
        textRows.Actions.Add(textRow);
        ExpectEqual(textRows.Actions.Count, 1, "InputMapSettingsRows holds rows");
        var player = GD.Load<InputActions>("res://input-map-settings/resources/player_actions.tres");
        Expect(player.GetActionCount() > 0, "the sample InputActions loads");
        var jumpAction = GD.Load<InputAction>("res://input-map-settings/resources/jump_action.tres");
        ExpectEqual(jumpAction.Action.ToString(), "jump", "the sample InputAction loads");
        var interact = GD.Load<InputAction>("res://input-map-settings/resources/interact_action.tres");
        ExpectEqual(interact.Action.ToString(), "interact", "the second sample InputAction loads");
        var defaults = GD.Load<InputActions>("res://input-map-settings/resources/default_bindings.tres");
        Expect(defaults.HasAction("move_left"), "the default bindings sample loads");
    }

    private static InputEventKey PressedKey(Key keycode, bool pressed = true)
    {
        return new InputEventKey { Keycode = keycode, PhysicalKeycode = keycode, KeyLabel = keycode, Pressed = pressed };
    }

    private static InputEventMouseButton MouseButtonEvent(MouseButton button, bool pressed = true)
    {
        return new InputEventMouseButton { ButtonIndex = button, Pressed = pressed };
    }

    private static InputEventJoypadButton JoyButtonEvent(JoyButton button, int device)
    {
        return new InputEventJoypadButton { ButtonIndex = button, Device = device, Pressed = true };
    }

    private static InputEventJoypadMotion JoyMotion(JoyAxis axis, float value, int device)
    {
        return new InputEventJoypadMotion { Axis = axis, AxisValue = value, Device = device };
    }

    private static string EventsText(StringName action)
    {
        return InputMapSettingsServer.EventsToText(InputMapSettingsServer.GetEvents(action));
    }

    private static bool IsAction(InputEvent @event, StringName action)
    {
        return InputMap.EventIsAction(@event, action);
    }

    private static string[] BuiltinActions()
    {
        var result = new List<string>();
        foreach (StringName action in InputMap.GetActions())
        {
            if (InputMap.IsBuiltinInputAction(action))
            {
                result.Add(action);
            }
        }
        return result.ToArray();
    }

    private void Setup()
    {
        InputMapSettingsServer.ResetAll();
        _log.Clear();
    }

    private string Last() => _log.Count > 0 ? _log[^1] : "";

    private InputActionRebinder MakeRebinder(InputActionRebinder.DeviceFilterEnum filter, InputActionRebinder.ConflictModeEnum mode)
    {
        var rebinder = new InputActionRebinder { DeviceFilter = filter, ConflictMode = mode, IgnoredActions = BuiltinActions() };
        rebinder.Started += (action, index) => _log.Add($"started:{action}:{index}");
        rebinder.Canceled += (action, index) => _log.Add($"canceled:{action}:{index}");
        rebinder.Conflict += (source, sourceIndex, dest, destIndex) => _log.Add($"conflict:{source}:{sourceIndex}:{dest}:{destIndex}");
        rebinder.Completed += (action, index, @event) => _log.Add($"completed:{action}:{index}:{InputAction.EventToText(@event)}");
        return rebinder;
    }

    private void OnActionEventsChanged(StringName action)
    {
        if (action == RebindJump || action == RebindFire)
        {
            _log.Add($"changed:{action}");
        }
    }

    private static bool Approx(float actual, float expected)
    {
        return Mathf.IsEqualApprox(actual, expected);
    }

    private static string Names(InputActions list)
    {
        return string.Join(",", list.GetActionNames().Select(name => name.ToString()));
    }

    private void TestTextForm()
    {
        string[] texts = ["KeyboardLabel:Ctrl+S", "Keyboard:W", "KeyboardPrinted:Shift+A", "KeyboardLabel:CmdOrCtrl+Z", "Mouse:Left", "Mouse:Ctrl+WheelUp",
            "Gamepad:A", "Gamepad:LeftShoulder", "GamepadAxis:LeftStickRight", "GamepadAxis:RightTrigger"];
        foreach (string text in texts)
        {
            InputEvent @event = InputAction.TextToEvent(text);
            Expect(@event != null, $"TextToEvent(\"{text}\") gives an event");
            if (@event != null)
            {
                ExpectEqual(InputAction.EventToText(@event), text, $"round trip of \"{text}\"");
            }
        }
        // The text form [Device<N>:]<Type>:[<Modifiers>+]<Input>: devices, special keys, modifiers, all ten axis names.
        string[] moreTexts = ["Device3:Gamepad:A", "Device3:GamepadAxis:LeftStickLeft", "Keyboard:Colon", "Keyboard:Ctrl+Plus", "Keyboard:Ctrl+Shift+S", "Keyboard:CmdOrCtrl+Z", "Mouse:Ctrl+Left", "Keyboard:Shift", "Gamepad:LeftStickPress", "GamepadAxis:LeftStickLeft", "GamepadAxis:LeftStickRight", "GamepadAxis:LeftStickUp", "GamepadAxis:LeftStickDown", "GamepadAxis:RightStickLeft", "GamepadAxis:RightStickRight", "GamepadAxis:RightStickUp", "GamepadAxis:RightStickDown", "GamepadAxis:LeftTrigger", "GamepadAxis:RightTrigger"];
        foreach (string text in moreTexts)
        {
            InputEvent @event = InputAction.TextToEvent(text);
            Expect(@event != null, $"TextToEvent(\"{text}\") gives an event");
            if (@event != null)
            {
                ExpectEqual(InputAction.EventToText(@event), text, $"round trip of \"{text}\"");
            }
        }
        ExpectEqual(InputAction.GetEventDevice(InputAction.TextToEvent("Device3:Gamepad:A")), InputAction.InputDevice.Gamepad, "Device3:Gamepad:A is a gamepad event");
        ExpectEqual(InputAction.TextToEvent("Device3:Gamepad:A").Device, 3, "Device3:Gamepad:A reacts to pad 3");
        ExpectEqual(InputAction.EventToText(InputAction.TextToEvent("Keyboard:Shift+Ctrl+S")), "Keyboard:Ctrl+Shift+S", "the modifiers are written in a fixed order");
        ExpectEqual(InputAction.EventToText(InputAction.TextToEvent("GamepadAxis:LeftX-")), "GamepadAxis:LeftStickLeft", "the old axis name is read and written new");
        ExpectEqual(InputAction.EventToText(InputAction.TextToEvent("JoypadMotion:TriggerRight+")), "GamepadAxis:RightTrigger", "the old type JoypadMotion is read");
        ExpectEqual(InputAction.EventToText(InputAction.TextToEvent("JoypadButton:LeftStick@2")), "Device2:Gamepad:LeftStickPress", "the old button and device names are read");
        string[] invalidTexts = ["Gamepad:Ctrl+A", "Device:Gamepad:A", "DeviceX:Gamepad:A", "Keyboard:", "Keyboard:Ctrl+", "GamepadAxis:LeftStickX-"];
        foreach (string text in invalidTexts)
        {
            Expect(InputAction.TextToEvent(text) == null, $"\"{text}\" is not valid");
        }
        Expect(InputAction.TextToEvent("KeyboardLabel:Hyper+S") == null, "an unknown modifier is not valid");
        Expect(InputAction.TextToEvent("Joypad:A") == null, "an unknown type is not valid");
        ExpectEqual(InputAction.EventToText(PressedKey(Key.W)), "Keyboard:W", "a pressed key is stored by its physical keycode");
        ExpectEqual(InputAction.EventToText(new InputEventScreenTouch()), "", "a touch can't be bound");
        ExpectEqual(InputAction.GetEventDevice(new InputEventScreenTouch()), InputAction.InputDevice.Max, "a touch has no device");
        ExpectEqual(InputAction.GetEventDevice(new InputEventJoypadMotion()), InputAction.InputDevice.Gamepad, "a joypad axis is a gamepad event");
        ExpectEqual(InputMapSettingsServer.GetEventLabel(InputAction.TextToEvent("KeyboardLabel:Ctrl+S")), "Ctrl+S", "label of Ctrl+S");
        ExpectEqual(InputMapSettingsServer.GetEventLabel(InputAction.TextToEvent("Mouse:Right")), "Right Mouse Button", "label of a mouse button");
        ExpectEqual(InputMapSettingsServer.GetEventLabel(InputAction.TextToEvent("GamepadAxis:LeftStickRight")), "Left Stick Right", "label of a stick direction");

        var entry = new InputAction { Events = InputMapSettingsServer.TextToEvents("Gamepad:B; KeyboardLabel:Space") };
        ExpectEqual(entry.GetEventCount(), 2, "the text gives one event per entry");
        ExpectEqual(entry.GetDeviceEvents(InputAction.InputDevice.Gamepad).Count, 1, "the gamepad event is found by device");
        Expect(entry.Events[0] is InputEventJoypadButton, "the first event is parsed from the text");
        ExpectEqual(InputMapSettingsServer.EventsToText(entry.Events), "Gamepad:B; KeyboardLabel:Space", "text round trip");
    }

    private void TestInputAction()
    {
        var action = new InputAction();
        ExpectEqual(action.Action.ToString(), "", "a new InputAction has no name");
        Expect(Approx((float)action.Deadzone, 0.2f), "the default deadzone is 0.2");
        ExpectEqual(action.Events.Count, 0, "a new InputAction has no events");
        action.Action = "demo_demo";
        action.Deadzone = 0.4f;
        ExpectEqual(action.Action.ToString(), "demo_demo", "action name");
        Expect(Approx((float)action.Deadzone, 0.4f), "deadzone");

        action.Events = InputMapSettingsServer.TextToEvents("KeyboardLabel:Ctrl+S; Gamepad:A");
        ExpectEqual(action.GetEventCount(), 2, "the text form gives two events");
        Expect(action.Events[0] is InputEventKey, "the first event is a key");
        Expect(action.Events[1] is InputEventJoypadButton, "the second event is a joypad button");
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "KeyboardLabel:Ctrl+S; Gamepad:A", "bindings round trip");
        ExpectEqual(action.GetDeviceEvents(InputAction.InputDevice.Keyboard).Count, 1, "one keyboard event");
        ExpectEqual(action.GetDeviceEvents(InputAction.InputDevice.Gamepad).Count, 1, "one gamepad event");
        ExpectEqual(action.GetDeviceEvents(InputAction.InputDevice.Mouse).Count, 0, "no mouse event");

        InputEvent mouse = InputAction.TextToEvent("Mouse:Left");
        action.AddEvent(mouse);
        ExpectEqual(action.GetEventCount(), 3, "AddEvent() adds an event");
        Expect(action.HasEvent(mouse), "HasEvent()");
        action.AddEvent(InputAction.TextToEvent("Mouse:Left"));
        ExpectEqual(action.GetEventCount(), 3, "AddEvent() ignores an equal event");
        Expect(action.RemoveEvent(mouse), "RemoveEvent() finds the event");
        Expect(!action.RemoveEvent(mouse), "RemoveEvent() of a missing event is false");
        action.Events = new Godot.Collections.Array<InputEvent> { PressedKey(Key.W) };
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "Keyboard:W", "setting events updates the bindings text");
        action.ClearEvents();
        ExpectEqual(action.GetEventCount(), 0, "ClearEvents()");
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "", "no events, no bindings text");

        // Touch has no text form: kept in events, left out of bindings.
        action.AddEvent(new InputEventScreenTouch());
        action.AddEvent(InputAction.TextToEvent("KeyboardLabel:Space"));
        ExpectEqual(action.GetEventCount(), 2, "touch and key are both events");
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "KeyboardLabel:Space", "touch is not part of the bindings text");

        string[] texts = ["KeyboardLabel:Ctrl+S", "Keyboard:W", "Mouse:Ctrl+WheelUp", "Gamepad:LeftShoulder", "GamepadAxis:LeftStickRight"];
        foreach (string text in texts)
        {
            InputEvent @event = InputAction.TextToEvent(text);
            Expect(@event != null, $"TextToEvent(\"{text}\") gives an event");
            if (@event != null)
            {
                ExpectEqual(InputAction.EventToText(@event), text, $"round trip of \"{text}\"");
            }
        }
        Expect(InputAction.TextToEvent("KeyboardLabel:Hyper+S") == null, "an unknown modifier is not valid");
        ExpectEqual(InputAction.EventToText(new InputEventScreenTouch()), "", "a touch has no text");
        ExpectEqual(InputAction.GetEventDevice(PressedKey(Key.A)), InputAction.InputDevice.Keyboard, "key device");
        ExpectEqual(InputAction.GetEventDevice(InputAction.TextToEvent("Mouse:Left")), InputAction.InputDevice.Mouse, "mouse device");
        ExpectEqual(InputAction.GetEventDevice(InputAction.TextToEvent("GamepadAxis:LeftStickRight")), InputAction.InputDevice.Gamepad, "joypad axis device");
        ExpectEqual(InputAction.GetEventDevice(new InputEventScreenTouch()), InputAction.InputDevice.Max, "touch has no device");
    }

    private void TestInputActions()
    {
        var list = new InputActions();
        ExpectEqual(list.GetActionCount(), 0, "a new InputActions is empty");
        var jump = new InputAction { Action = "demo_jump", Events = InputMapSettingsServer.TextToEvents("KeyboardLabel:Space") };
        var fire = new InputAction { Action = "demo_fire", Events = InputMapSettingsServer.TextToEvents("Mouse:Left") };
        list.AddAction(jump);
        list.AddAction(fire);
        ExpectEqual(list.GetActionCount(), 2, "two actions");
        Expect(list.HasAction("demo_jump") && list.HasAction("demo_fire"), "HasAction()");
        Expect(!list.HasAction("demo_none"), "HasAction() of a missing name is false");
        Expect(list.GetAction("demo_jump") == jump, "GetAction() returns the same resource");
        Expect(list.GetAction("demo_none") == null, "GetAction() of a missing name is null");
        ExpectEqual(Names(list), "demo_jump,demo_fire", "names in insertion order");

        var jump2 = new InputAction { Action = "demo_jump", Events = InputMapSettingsServer.TextToEvents("KeyboardLabel:W") };
        list.AddAction(jump2);
        ExpectEqual(list.GetActionCount(), 2, "adding a name again replaces the action");
        Expect(list.GetAction("demo_jump") == jump2, "the new action replaced the old one");
        ExpectEqual(Names(list), "demo_jump,demo_fire", "the replaced action keeps its place");

        // A rename of an action in the list is found by the new name.
        fire.Action = "demo_shoot";
        Expect(list.HasAction("demo_shoot") && !list.HasAction("demo_fire"), "renaming an action updates the lookup");

        Expect(list.RemoveAction("demo_shoot"), "RemoveAction()");
        Expect(!list.RemoveAction("demo_shoot"), "RemoveAction() of a missing name is false");
        ExpectEqual(list.GetActionCount(), 1, "one action left");
        list.Clear();
        ExpectEqual(list.GetActionCount(), 0, "Clear()");
        list.Actions = new Godot.Collections.Array<InputAction> { jump, jump2 };
        ExpectEqual(list.GetActionCount(), 1, "set Actions: the later action of a name wins");
        Expect(list.GetAction("demo_jump") == jump2, "set Actions: the later one is kept");
    }

    private void TestResourceFiles()
    {
        // Events is a stored property: .tres and .res keep the events as sub-resources.
        bool stored = false;
        foreach (Godot.Collections.Dictionary property in new InputAction().GetPropertyList())
        {
            string name = property["name"].AsString();
            long usage = property["usage"].AsInt64();
            if (name == "events")
            {
                stored = (usage & (long)PropertyUsageFlags.Storage) != 0 && (usage & (long)PropertyUsageFlags.Editor) != 0;
            }
            Expect(name != "bindings", "InputAction has no bindings property");
        }
        Expect(stored, "events is stored and shown in the Inspector");

        var list = new InputActions();
        var jump = new InputAction { Action = "demo_file_jump", Deadzone = 0.5f };
        jump.Events = InputMapSettingsServer.TextToEvents("KeyboardLabel:Space; Keyboard:Ctrl+W; Mouse:Right; Device1:Gamepad:A; GamepadAxis:LeftStickUp");
        jump.AddEvent(new InputEventScreenTouch { Index = 2 });
        list.AddAction(jump);
        list.AddAction(new InputAction { Action = "demo_file_empty" });
        foreach (string path in new[] { "user://demo_actions_cs.tres", "user://demo_actions_cs.res" })
        {
            ExpectEqual(ResourceSaver.Save(list, path), Error.Ok, $"save {path}");
            var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as InputActions;
            Expect(loaded != null && loaded != list, $"load {path}");
            if (loaded == null)
            {
                continue;
            }
            ExpectEqual(string.Join(",", loaded.GetActionNames()), "demo_file_jump,demo_file_empty", $"{path}: action names");
            InputAction read = loaded.GetAction("demo_file_jump");
            Expect(Approx((float)read.Deadzone, 0.5f), $"{path}: deadzone");
            ExpectEqual(read.GetEventCount(), 6, $"{path}: all events, touch too");
            ExpectEqual(InputMapSettingsServer.EventsToText(read.Events), InputMapSettingsServer.EventsToText(jump.Events), $"{path}: the same events");
            Expect(read.Events[5] is InputEventScreenTouch touch && touch.Index == 2, $"{path}: the touch event");
            ExpectEqual(loaded.GetAction("demo_file_empty").GetEventCount(), 0, $"{path}: an action without events");
        }
        string text = FileAccess.GetFileAsString("user://demo_actions_cs.tres");
        Expect(text.Contains("events = Array[InputEvent]([SubResource("), ".tres: events as sub-resources");
        Expect(text.Contains("deadzone = 0.5"), ".tres: the deadzone that is not the default");
        Expect(!text.Contains("bindings"), ".tres: no text form");

        // The text form of InputMapSettingsServer: one pad with a Device<N>: prefix, events without text left out.
        ExpectEqual(InputMapSettingsServer.EventsToText(jump.Events), "KeyboardLabel:Space; Keyboard:Ctrl+W; Mouse:Right; Device1:Gamepad:A; GamepadAxis:LeftStickUp", "EventsToText()");
        ExpectEqual(InputMapSettingsServer.TextToEvents("KeyboardLabel:Space; NotAType:X").Count, 1, "TextToEvents() skips invalid items");
        ExpectEqual(InputMapSettingsServer.TextToEvents("Device1:Gamepad:A")[0].Device, 1, "TextToEvents(): the pad");
    }

    private void TestLayers()
    {
        Expect(InputMap.IsBuiltinInputAction("ui_accept"), "ui_accept is built-in");
        Expect(!InputMap.IsCustomInputAction("ui_accept"), "ui_accept is not custom");
        Expect(!InputMap.IsCustomInputAction("ui_text_backspace_word.macos"), "a platform variant of a built-in action is never custom");
        Expect(InputMap.IsCustomInputAction(Attack), "a project action is custom");
        Expect(!InputMap.IsBuiltinInputAction(Attack), "a project action is not built-in");
        Expect(!InputMap.IsCustomInputAction("demo_not_there"), "a missing action is not custom");
        ExpectEqual(InputMap.GetInputActionLayer("ui_accept"), InputMap.InputActionLayer.Builtin, "layer of ui_accept");
        ExpectEqual(InputMap.GetInputActionLayer(Attack), InputMap.InputActionLayer.Custom, "layer of the project action");

        InputActions builtin = InputMap.GetInputActions(InputMap.InputActionLayer.Builtin);
        InputActions custom = InputMap.GetInputActions(InputMap.InputActionLayer.Custom);
        Expect(builtin.HasAction("ui_accept"), "the built-in layer has ui_accept");
        Expect(!builtin.HasAction(Attack), "the built-in layer has no custom action");
        Expect(custom.HasAction(Attack), "the custom layer has the project action");
        Expect(!custom.HasAction("ui_accept"), "the custom layer has no built-in action");
        ExpectEqual(builtin.GetActionCount() + custom.GetActionCount(), InputMap.GetActions().Count, "the layers together are all actions");
        ExpectEqual(InputMap.GetInputActions(InputMap.InputActionLayer.Builtin).GetInstanceId(), builtin.GetInstanceId(), "the built-in layer is the same object every time");
        ExpectEqual(InputMap.GetInputActions(InputMap.InputActionLayer.Custom).GetInstanceId(), custom.GetInstanceId(), "the custom layer is the same object every time");
        foreach (StringName name in builtin.GetActionNames())
        {
            if (!InputMap.IsBuiltinInputAction(name))
            {
                Expect(false, $"{name} is in the built-in layer but not built-in");
                break;
            }
        }

        InputAction attack = InputMap.GetInputAction(Attack);
        ExpectEqual(attack.Action.ToString(), "test_attack", "GetInputAction() name");
        Expect(Approx((float)attack.Deadzone, 0.3f), "GetInputAction() deadzone from the project");
        ExpectEqual(InputMapSettingsServer.EventsToText(attack.Events), "KeyboardLabel:F", "GetInputAction() events as text");
        ExpectEqual(attack.GetInstanceId(), custom.GetAction(Attack).GetInstanceId(), "GetInputAction() is the action of the custom layer");
        InputEvent keyH = InputAction.TextToEvent("KeyboardLabel:H");
        attack.AddEvent(keyH);
        Expect(IsAction(PressedKey(Key.H), Attack), "an event added to the returned action works");
        attack.RemoveEvent(keyH);
        Expect(!IsAction(PressedKey(Key.H), Attack), "an event removed from the returned action no longer works");
        ExpectEqual(InputMap.GetInputAction(Attack).GetEventCount(), 1, "one event again");

        // Direct edits of a layer reach the InputMap.
        custom.AddAction(new InputAction { Action = "demo_direct", Events = InputMapSettingsServer.TextToEvents("KeyboardLabel:F9") });
        Expect(InputMap.HasAction("demo_direct"), "an action added to the custom layer is in the InputMap");
        Expect(IsAction(PressedKey(Key.F9), "demo_direct"), "its key works");
        Expect(custom.RemoveAction("demo_direct"), "remove it from the layer");
        Expect(!InputMap.HasAction("demo_direct"), "an action removed from the layer is gone from the InputMap");
    }

    private void TestSetInputAction()
    {
        InputAction original = InputMap.GetInputAction(Attack);
        var changed = new InputAction { Action = Attack, Deadzone = 0.6f, Events = InputMapSettingsServer.TextToEvents("KeyboardLabel:G") };
        InputMap.SetInputAction(changed);
        Expect(IsAction(PressedKey(Key.G), Attack), "SetInputAction(): the new key works");
        Expect(!IsAction(PressedKey(Key.F), Attack), "SetInputAction(): the old key no longer works");
        Expect(Approx(InputMap.ActionGetDeadzone(Attack), 0.6f), "SetInputAction(): the deadzone is replaced");
        InputMap.SetInputAction(original);
        Expect(IsAction(PressedKey(Key.F), Attack), "restoring the action brings back the old key");
        Expect(!IsAction(PressedKey(Key.G), Attack), "restoring removes the new key");

        // A new name creates a custom action; SetInputActions() takes a list.
        var extra = new InputActions();
        for (int n = 0; n < 2; n++)
        {
            extra.AddAction(new InputAction { Action = $"demo_extra_{n}", Events = InputMapSettingsServer.TextToEvents($"KeyboardLabel:F{n + 5}") });
        }
        InputMap.SetInputActions(extra);
        Expect(InputMap.HasAction("demo_extra_0") && InputMap.HasAction("demo_extra_1"), "SetInputActions() adds new actions");
        Expect(InputMap.IsCustomInputAction("demo_extra_1"), "a new action is custom");
        Expect(IsAction(PressedKey(Key.F6), "demo_extra_1"), "the new action reacts to its key");
        InputMap.EraseAction("demo_extra_0");
        InputMap.EraseAction("demo_extra_1");
    }

    private void TestParseCost()
    {
        for (int i = 0; i < BulkCount; i++)
        {
            StringName name = $"demo_bulk_{i}";
            InputMap.AddAction(name);
            InputMap.ActionAddEvent(name, PressedKey((Key)(BulkFirstKey + i)));
        }
        Expect(InputMap.HasAction("demo_bulk_99"), "100 actions were added");

        foreach (int i in new[] { 0, 37, 99 })
        {
            StringName bulk = $"demo_bulk_{i}";
            Input.ParseInputEvent(PressedKey((Key)(BulkFirstKey + i)));
            Input.FlushBufferedEvents();
            Expect(Input.IsActionPressed(bulk), $"{bulk} is pressed after ParseInputEvent()");
            StringName other = $"demo_bulk_{(i + 1) % BulkCount}";
            Expect(!Input.IsActionPressed(other), $"{other} is not pressed by the key of {bulk}");
            Input.ParseInputEvent(PressedKey((Key)(BulkFirstKey + i), false));
            Input.FlushBufferedEvents();
            Expect(!Input.IsActionPressed(bulk), $"{bulk} is released again");
        }

        InputEventKey @event = PressedKey((Key)(BulkFirstKey + 50));
        ulong start = Time.GetTicksUsec();
        for (int i = 0; i < 1000; i++)
        {
            Input.ParseInputEvent(@event);
        }
        ulong elapsed = Time.GetTicksUsec() - start;
        Input.ParseInputEvent(PressedKey((Key)(BulkFirstKey + 50), false));
        Input.FlushBufferedEvents();
        GD.Print($"  INFO  1000 x ParseInputEvent() with 100 extra actions: {elapsed / 1000.0:F2} ms");
        Expect(!Input.IsActionPressed("demo_bulk_50"), "the bulk action is released at the end");

        for (int i = 0; i < BulkCount; i++)
        {
            InputMap.EraseAction($"demo_bulk_{i}");
        }
        Expect(!InputMap.HasAction("demo_bulk_0"), "the bulk actions were removed");
        Expect(!IsAction(PressedKey((Key)BulkFirstKey), "demo_bulk_0"), "a removed action no longer matches");
    }

    private void TestSettingsServer()
    {
        // The property save_mode of the singleton is read through Get() (0 = CUSTOM_ONLY, 1 = ALL).
        var server = InputMapSettingsServer.Singleton;
        ExpectEqual(server.Get("save_mode").AsInt32(), 0, "save_mode is CUSTOM_ONLY by default");
        Expect(server.GetDefaultSettings() is InputActions, "the settings resource is an InputActions");
        Expect(InputMapSettingsServer.HasAction(Attack), "the project action is managed");
        Expect(!InputMapSettingsServer.HasAction("ui_accept"), "built-in actions are not managed in CUSTOM_ONLY");
        ExpectEqual(InputMapSettingsServer.Rebind("ui_accept", PressedKey(Key.Q)), Error.DoesNotExist, "a built-in action can't be rebound in CUSTOM_ONLY");

        InputMapSettingsServer.ResetAll();
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Attack, InputAction.InputDevice.Keyboard, 0)), "KeyboardLabel:F", "slot 0 is F");
        ExpectEqual(InputMapSettingsServer.Rebind(Attack, PressedKey(Key.X)), Error.Ok, "Rebind() slot 0");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Attack, InputAction.InputDevice.Keyboard, 0)), "Keyboard:X", "GetEvent() gives the new key");
        Expect(IsAction(PressedKey(Key.X), Attack), "the new key works at once");
        Expect(!IsAction(PressedKey(Key.F), Attack), "the old key no longer works");
        ExpectEqual(InputMapSettingsServer.Rebind(Attack, PressedKey(Key.Y), 1), Error.Ok, "Rebind() slot 1");
        ExpectEqual(InputMapSettingsServer.GetSlotCount(Attack, InputAction.InputDevice.Keyboard), 2, "two keyboard slots");
        Expect(InputMapSettingsServer.EventsToText(InputMapSettingsServer.GetInputAction(Attack).Events).Contains("Keyboard:Y"), "GetInputAction() shows both keys");

        InputMapSettingsServer.ClearEvent(Attack, InputAction.InputDevice.Keyboard, 0);
        ExpectEqual(InputMapSettingsServer.GetSlotCount(Attack, InputAction.InputDevice.Keyboard), 1, "ClearEvent() removes the slot");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Attack, InputAction.InputDevice.Keyboard, 0)), "Keyboard:Y", "later slots move up");
        Expect(!IsAction(PressedKey(Key.X), Attack), "the cleared key no longer works");
        Expect(IsAction(PressedKey(Key.Y), Attack), "the moved key works");

        InputMapSettingsServer.ResetAction(Attack);
        Expect(IsAction(PressedKey(Key.F), Attack), "ResetAction(): F works again");
        Expect(!IsAction(PressedKey(Key.Y), Attack), "ResetAction(): Y no longer works");
        ExpectEqual(InputMapSettingsServer.EventsToText(InputMapSettingsServer.GetInputAction(Attack).Events), "KeyboardLabel:F", "ResetAction(): the project bindings");

        InputMapSettingsServer.Rebind(Attack, PressedKey(Key.X));
        InputMapSettingsServer.ResetAll();
        Expect(IsAction(PressedKey(Key.F), Attack), "ResetAll(): F works again");
        Expect(!IsAction(PressedKey(Key.X), Attack), "ResetAll(): X no longer works");

        // SAVE_MODE_ALL manages the built-in actions too.
        // A copy: the server changes the action of the InputMap itself.
        var savedAccept = (InputAction)InputMap.GetInputAction("ui_accept").Duplicate();
        server.Set("save_mode", 1);
        ExpectEqual(InputMapSettingsServer.Rebind("ui_accept", PressedKey(Key.Q)), Error.Ok, "SAVE_MODE_ALL: a built-in action can be rebound");
        Expect(InputMapSettingsServer.HasAction("ui_accept"), "SAVE_MODE_ALL: ui_accept is managed now");
        server.Set("save_mode", 0);
        InputMapSettingsServer.ResetAll();
        InputMap.SetInputAction(savedAccept);
        Expect(!IsAction(PressedKey(Key.Q), "ui_accept"), "ui_accept is restored");
    }

    private void TestDefaults()
    {
        string[] actions = InputMapSettingsServer.GetActionNames();
        Expect(actions.Contains("jump") && actions.Contains("fire"), "the project actions are managed");
        Expect(!actions.Contains("ui_accept"), "built-in ui_* actions are left out by default");
        ExpectEqual(InputMapSettingsServer.Singleton.Get("save_mode").AsInt32(), 0, "save_mode is CUSTOM_ONLY by default");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Jump, InputAction.InputDevice.Keyboard, 0)), "KeyboardLabel:Space", "jump: keyboard slot 0 is Space");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Jump, InputAction.InputDevice.Gamepad, 0)), "Gamepad:A", "jump: gamepad slot 0 is A");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Fire, InputAction.InputDevice.Gamepad, 0)), "GamepadAxis:RightTrigger", "fire: gamepad slot 0 is the right trigger");
        Expect(Mathf.IsEqualApprox(InputMapSettingsServer.GetActionDeadzone(Jump), 0.5f), "jump: deadzone 0.5 from the project");
        ExpectEqual(InputMapSettingsServer.GetEvents(Jump).Count, 2, "jump has two events");
    }

    private void TestRebind()
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
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Jump, InputAction.InputDevice.Keyboard, 0)), "Keyboard:Up", "slot 1 moved up to slot 0");
        Expect(IsAction(PressedKey(Key.Up), Jump), "the moved key still works");
        Expect(!IsAction(PressedKey(Key.W), Jump), "the cleared key no longer works");

        InputMapSettingsServer.SetActionDeadzone(Jump, 0.7f);
        Expect(Mathf.IsEqualApprox(InputMap.ActionGetDeadzone(Jump), 0.7f), "the deadzone reaches InputMap");
        ExpectEqual(InputMapSettingsServer.Rebind(Jump, new InputEventScreenTouch()), Error.InvalidParameter, "a touch can't be bound");
        ExpectEqual(InputMapSettingsServer.Rebind("no_such_action", PressedKey(Key.Q)), Error.DoesNotExist, "an unknown action can't be bound");
    }

    private void TestConflicts()
    {
        InputMapSettingsServer.ResetAll();
        string[] conflicts = InputMapSettingsServer.FindConflicts(PressedKey(Key.Space));
        Expect(conflicts.Length == 1 && conflicts[0] == "jump", "Space is used by jump");
        Expect(InputMapSettingsServer.FindConflicts(PressedKey(Key.Space), Jump).Length == 0, "the ignored action is not a conflict");
        Expect(InputMapSettingsServer.FindConflicts(PressedKey(Key.K)).Length == 0, "K is free");
        InputMapSettingsServer.Rebind(Fire, PressedKey(Key.Space));
        ExpectEqual(InputMapSettingsServer.FindConflicts(PressedKey(Key.Space)).Length, 2, "after rebinding fire, Space is used twice");
    }

    private void TestReset()
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

    private void TestFormats()
    {
        var server = InputMapSettingsServer.Singleton;
        foreach (string extension in new[] { "ini", "json", "csv" })
        {
            string path = $"user://input-map-settings-test-cs/map_bindings_cs.{extension}";
            server.SetFilePath(path);
            InputMapSettingsServer.Rebind(Jump, PressedKey(Key.W));
            InputMapSettingsServer.Rebind(Jump, InputAction.TextToEvent("GamepadAxis:LeftStickUp"));
            InputMapSettingsServer.Rebind(Fire, InputAction.TextToEvent("Mouse:Ctrl+Right"));
            ExpectEqual(server.Save(), Error.Ok, $"{extension}: Save()");
            string text = FileAccess.GetFileAsString(path);
            Expect(text.Contains("Keyboard") && text.Contains("LeftStickUp") && text.Contains("Mouse") && text.Contains("Ctrl"), $"{extension}: the file holds the event fields");
            Expect(!text.Contains("Keyboard:W") && !text.Contains("InputEvent"), $"{extension}: no event texts, no sub-resources");
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

    private void TestFileLayout()
    {
        var server = InputMapSettingsServer.Singleton;
        // The text form: physical (Keyboard) or label (KeyboardLabel), one pad as a Device<N> prefix, the axis name, the modifiers.
        const string text = "Keyboard:Ctrl+S; KeyboardLabel:E; Mouse:Left; Gamepad:A; GamepadAxis:LeftStickLeft; Device2:Gamepad:B";
        InputEvent[] events = InputMapSettingsServer.TextToEvents(text).ToArray();
        ExpectEqual(events.Length, 6, "TextToEvents(): six events");
        ExpectEqual(InputMapSettingsServer.EventsToText(new Godot.Collections.Array<InputEvent>(events)), text, "EventsToText() writes the same names");
        var ctrlS = (InputEventKey)events[0];
        ExpectEqual(ctrlS.PhysicalKeycode, Key.S, "Keyboard: the physical keycode");
        ExpectEqual(ctrlS.Keycode, Key.None, "Keyboard: no label keycode");
        Expect(ctrlS.CtrlPressed, "Keyboard:Ctrl+S has the modifier");
        var labelE = (InputEventKey)events[1];
        ExpectEqual(labelE.Keycode, Key.E, "KeyboardLabel: the keycode");
        ExpectEqual(labelE.PhysicalKeycode, Key.None, "KeyboardLabel: no physical keycode");
        ExpectEqual(events[3].Device, -1, "Gamepad:A reacts to all pads");
        ExpectEqual(events[5].Device, 2, "Device2:Gamepad:B reacts to pad 2");
        Expect(events[4] is InputEventJoypadMotion { Axis: JoyAxis.LeftX, AxisValue: < 0.0f }, "GamepadAxis:LeftStickLeft is the left stick to the left");
        // The names of version 2 files are still read.
        ExpectEqual(InputMapSettingsServer.EventsToText(InputMapSettingsServer.TextToEvents("Key:Ctrl+S; PhysicalKey:W; MouseButton:Left; JoypadButton:A@1; JoypadMotion:LeftY-")),
            "KeyboardLabel:Ctrl+S; Keyboard:W; Mouse:Left; Device1:Gamepad:A; GamepadAxis:LeftStickUp", "the names of version 2 are read");

        InputMapSettingsEvent record = InputMapSettingsEvent.FromText("Device1:GamepadAxis:RightStickDown");
        Expect(record != null, "FromText() gives a record");
        if (record != null)
        {
            ExpectEqual((int)record.Device, 2, "record: device Gamepad");
            ExpectEqual(record.Input, "RightStickDown", "record: input");
            ExpectEqual(record.DeviceId, "1", "record: DeviceId is the pad number");
            ExpectEqual(record.ToText(), "Device1:GamepadAxis:RightStickDown", "record: text");
        }
        InputMapSettingsEvent keyRecord = InputMapSettingsEvent.FromEvent(PressedKey(Key.W));
        ExpectEqual((int)keyRecord.KeyMode, 0, "a pressed key is a Physical key record");
        ExpectEqual(keyRecord.DeviceId, "Any", "a key has DeviceId Any");
        Expect(InputMapSettingsEvent.FromText("Keyboard:NoSuchKey") == null, "an unknown key is not valid");

        // The file layout: one record per event in the formats with nesting, one row per event in CSV.
        InputMapSettingsServer.ResetAll();
        InputMapSettingsServer.Rebind(Jump, PressedKey(Key.W));
        InputMapSettingsServer.Rebind(Jump, InputAction.TextToEvent("GamepadAxis:LeftStickLeft"));
        InputMapSettingsServer.Rebind(Fire, InputAction.TextToEvent("Mouse:Ctrl+Right"));
        foreach (string extension in new[] { "json", "ini", "csv", "xml", "toml", "yaml" })
        {
            if (!ResourceTextSerializer.IsFormatAvailable(ResourceTextSerializer.GetFormatFromExtension(extension)))
            {
                GD.Print($"  SKIP  layout .{extension} (module not in this build)");
                continue;
            }
            string path = $"user://input-map-settings-test-cs/map_layout_cs.{extension}";
            server.SetFilePath(path);
            ExpectEqual(server.Save(), Error.Ok, $"{extension}: Save()");
            string fileText = FileAccess.GetFileAsString(path);
            if (extension == "csv")
            {
                Expect(fileText.StartsWith("action,index,device,input,modifiers,key_mode,device_id,deadzone"), "csv: the header names the fields");
                Expect(fileText.Contains("jump,0,Keyboard,W,,Physical,Any,"), "csv: the physical key W is one row");
                Expect(fileText.Contains("jump,1,Gamepad,LeftStickLeft,,"), "csv: the axis direction is in the name");
                Expect(fileText.Contains("fire,0,Mouse,Right,Ctrl,Physical,Any,"), "csv: the mouse button has the modifier Ctrl");
            }
            else
            {
                Expect(fileText.Contains("input_events") && fileText.Contains("key_mode") && fileText.Contains("device_id"), $"{extension}: the events are records with fields");
            }
            InputMapSettingsServer.ResetAll();
            ExpectEqual(server.Load(), Error.Ok, $"{extension}: Load()");
            server.Apply();
            Expect(IsAction(PressedKey(Key.W), Jump), $"{extension}: Keyboard:W survives");
            ExpectEqual(InputMapSettingsServer.EventsToText(InputMapSettingsServer.GetEvents(Fire)), "Mouse:Ctrl+Right; GamepadAxis:RightTrigger", $"{extension}: the mouse event replaces the mouse slot, the trigger stays");
            DirAccess.RemoveAbsolute(path);
        }

        // A file of version 2: one text cell per action.
        const string v2Path = "user://input-map-settings-test-cs/map_v2_cs.json";
        using (var v2 = FileAccess.Open(v2Path, FileAccess.ModeFlags.Write))
        {
            v2.StoreString("{\"$type\": \"InputMapSettingsRows\", \"metadata/settings_version\": 2, \"actions\": [{\"action\": \"jump\", \"deadzone\": 0.4, \"bindings\": \"Key:K; JoypadButton:B@2\"}]}");
        }
        InputMapSettingsServer.ResetAll();
        server.SetFilePath(v2Path);
        ExpectEqual(server.Load(), Error.Ok, "a version 2 file loads");
        server.Apply();
        ExpectEqual(InputMapSettingsServer.EventsToText(InputMapSettingsServer.GetEvents(Jump)), "KeyboardLabel:K; Device2:Gamepad:B", "version 2: the bindings are read");
        Expect(Mathf.IsEqualApprox(InputMapSettingsServer.GetActionDeadzone(Jump), 0.4f), "version 2: the deadzone is read");
        DirAccess.RemoveAbsolute(v2Path);
        InputMapSettingsServer.ResetAll();
        server.SetFilePath("");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = InputMapSettingsServer.Singleton;
        InputMapSettingsServer.Rebind(Jump, PressedKey(Key.W));
        // A row of an action that a later game version no longer has.
        server.AddEntry(new InputAction { Action = "removed", Events = InputMapSettingsServer.TextToEvents("KeyboardLabel:G") });
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        string text = FileAccess.GetFileAsString(InputMapFile);
        Expect(text.Contains("Keyboard") && text.Contains("input_events"), "the INI file has the event fields (device Keyboard)");
        Expect(text.Contains("removed"), "the INI file contains the removed action");

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
                if (line.StartsWith("PROBE "))
                {
                    Variant parsed = Json.ParseString(line["PROBE ".Length..]);
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

    private void TestClass()
    {
        Expect(ClassDB.ClassExists("InputActionRebinder"), "InputActionRebinder is registered");
        Expect(ClassDB.IsParentClass("InputActionRebinder", "RefCounted"), "InputActionRebinder is a RefCounted");
        var rebinder = new InputActionRebinder();
        ExpectEqual(rebinder.DeviceFilter, InputActionRebinder.DeviceFilterEnum.Any, "DeviceFilter is Any by default");
        ExpectEqual(rebinder.ConflictMode, InputActionRebinder.ConflictModeEnum.Ask, "ConflictMode is Ask by default");
        Expect(Mathf.IsEqualApprox((float)rebinder.AxisThreshold, 0.5f), "AxisThreshold is 0.5 by default");
        ExpectEqual(rebinder.GamepadDevice, -1, "GamepadDevice is -1 by default");
        ExpectEqual(rebinder.IgnoredActions.Length, 0, "no ignored actions by default");
        Expect(!rebinder.IsActive(), "a new rebinder is not active");
        ExpectEqual(rebinder.GetEventIndex(), -1, "no event index without a session");
        Expect(!rebinder.Update(PressedKey(Key.G)), "Update() without a session uses nothing");
        foreach (var signalName in new[] { "started", "canceled", "conflict", "completed" })
        {
            Expect(rebinder.HasSignal(signalName), $"signal {signalName}");
        }
    }

    private void TestFiltering()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        ExpectEqual(rebinder.Begin(RebindJump, 0), Error.Ok, "Begin()");
        ExpectEqual(Last(), "started:test_jump:0", "Started is emitted");
        Expect(rebinder.IsActive(), "the session is active");

        Expect(!rebinder.Update(new InputEventMouseMotion { Relative = new Vector2(12, 0) }), "mouse motion is ignored");
        Expect(!rebinder.Update(PressedKey(Key.H, false)), "a released key is ignored");
        var echo = PressedKey(Key.H);
        echo.Echo = true;
        Expect(!rebinder.Update(echo), "an echo is ignored");
        Expect(!rebinder.Update(JoyButtonEvent(JoyButton.Paddle2, 0)), "a joypad button is outside KeyboardMouse");
        Expect(!rebinder.Update(new InputEventScreenTouch { Pressed = true }), "a touch is ignored");

        var ctrl = PressedKey(Key.Ctrl);
        ctrl.CtrlPressed = true;
        Expect(rebinder.Update(ctrl), "a modifier alone is used");
        Expect(rebinder.IsActive(), "a modifier alone is not bound yet");
        var ctrlH = PressedKey(Key.H);
        ctrlH.CtrlPressed = true;
        Expect(rebinder.Update(ctrlH), "Ctrl+H is used");
        Expect(!rebinder.IsActive(), "the session ends with the binding");
        ExpectEqual(Last(), "completed:test_jump:0:Keyboard:Ctrl+H", "Completed with Ctrl+H");
        ExpectEqual(EventsText(RebindJump), "Keyboard:Ctrl+H; Gamepad:Paddle1", "slot 0 has Ctrl+H");
        Expect(IsAction(ctrlH, RebindJump), "Ctrl+H works at once");

        ExpectEqual(rebinder.Begin(RebindJump, 1), Error.Ok, "Begin() a new slot");
        var shift = PressedKey(Key.Shift);
        shift.ShiftPressed = true;
        Expect(rebinder.Update(shift), "Shift pressed is used");
        Expect(rebinder.Update(PressedKey(Key.Shift, false)), "Shift released alone is used");
        ExpectEqual(Last(), "completed:test_jump:1:Keyboard:Shift", "a modifier released alone is bound");
        ExpectEqual(EventsText(RebindJump), "Keyboard:Ctrl+H; Gamepad:Paddle1; Keyboard:Shift", "slot 1 is added at the end");

        rebinder.DeviceFilter = InputActionRebinder.DeviceFilterEnum.Gamepad;
        rebinder.GamepadDevice = 1;
        ExpectEqual(rebinder.Begin(RebindJump, 0), Error.Ok, "Begin() the gamepad slot");
        Expect(!rebinder.Update(PressedKey(Key.J)), "a key is outside Gamepad");
        Expect(!rebinder.Update(JoyMotion(JoyAxis.RightX, 0.3f, 1)), "a weak stick move is ignored");
        Expect(!rebinder.Update(JoyMotion(JoyAxis.RightX, 0.9f, 0)), "another pad is ignored");
        Expect(rebinder.Update(JoyMotion(JoyAxis.RightX, 0.9f, 1)), "a strong move of the pad is used");
        ExpectEqual(Last(), "completed:test_jump:0:GamepadAxis:RightStickRight", "the direction is part of the binding");
        Expect(IsAction(JoyMotion(JoyAxis.RightX, 1.0f, 3), RebindJump), "the binding works for every pad");

        ExpectEqual(rebinder.Begin("no_such_action", 0), Error.DoesNotExist, "Begin() of an unknown action");
        ExpectEqual(rebinder.Begin(RebindJump, -1), Error.InvalidParameter, "Begin() with a negative index");
    }

    private void TestReject()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        rebinder.Begin(RebindFire, 0);
        Expect(rebinder.Update(PressedKey(Key.G)), "Reject: the conflicting key is used");
        ExpectEqual(Last(), "conflict:test_fire:0:test_jump:0", "Reject: Conflict names both slots");
        Expect(rebinder.IsActive(), "Reject: the session waits for another key");
        ExpectEqual(InputAction.EventToText(rebinder.GetConflictEvent()), "Keyboard:G", "GetConflictEvent()");
        ExpectEqual(EventsText(RebindFire), "Mouse:Left", "Reject: fire is unchanged");
        Expect(rebinder.Update(PressedKey(Key.K)), "Reject: another key");
        ExpectEqual(Last(), "completed:test_fire:0:Keyboard:K", "Reject: the free key is bound");
    }

    private void TestSwapOrder()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Swap);
        InputMapSettingsServer.ActionEventsChanged += OnActionEventsChanged;
        rebinder.Begin(RebindFire, 0);
        _log.Clear();
        Expect(rebinder.Update(PressedKey(Key.G)), "Swap: the key is used");
        InputMapSettingsServer.ActionEventsChanged -= OnActionEventsChanged;
        ExpectEqual(EventsText(RebindFire), "Keyboard:G", "Swap: fire has G");
        ExpectEqual(EventsText(RebindJump), "Mouse:Left; Gamepad:Paddle1", "Swap: jump has the old event of fire");
        Expect(IsAction(MouseButtonEvent(MouseButton.Left), RebindJump), "Swap: the left button jumps");
        Expect(!IsAction(PressedKey(Key.G), RebindJump), "Swap: G no longer jumps");
        ExpectEqual(_log.Count, 3, "Swap: two server signals and Completed");
        if (_log.Count == 3)
        {
            Expect(_log.Contains("changed:test_fire") && _log.Contains("changed:test_jump"), "Swap: the server announces both actions");
            ExpectEqual(_log[2], "completed:test_fire:0:Keyboard:G", "Swap: Completed comes after the server signals");
        }
    }

    private void TestAllow()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.Any, InputActionRebinder.ConflictModeEnum.Allow);
        rebinder.Begin(RebindFire, 5);
        Expect(rebinder.Update(PressedKey(Key.G)), "Allow: the key is used");
        ExpectEqual(Last(), "completed:test_fire:1:Keyboard:G", "Allow: added behind the last event");
        ExpectEqual(EventsText(RebindFire), "Mouse:Left; Keyboard:G", "Allow: fire has G");
        Expect(IsAction(PressedKey(Key.G), RebindJump) && IsAction(PressedKey(Key.G), RebindFire), "Allow: G works for both");
    }

    private void TestAsk()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Ask);
        ExpectEqual(rebinder.ResolveConflictAllow(), Error.Unconfigured, "Ask: nothing to resolve");
        rebinder.Begin(RebindFire, 0);
        Expect(rebinder.Update(PressedKey(Key.G)), "Ask: the key is used");
        Expect(rebinder.IsConflictPending(), "Ask: the conflict waits");
        Expect(!rebinder.Update(PressedKey(Key.K)), "Ask: no input while the menu asks");
        rebinder.Cancel();
        ExpectEqual(Last(), "canceled:test_fire:0", "Ask: Cancel() emits Canceled");
        ExpectEqual(EventsText(RebindFire), "Mouse:Left", "Ask: Cancel changes nothing");
        ExpectEqual(EventsText(RebindJump), "KeyboardLabel:G; Gamepad:Paddle1", "Ask: jump unchanged");

        rebinder.Begin(RebindFire, 0);
        rebinder.Update(PressedKey(Key.G));
        ExpectEqual(rebinder.ResolveConflictAllow(), Error.Ok, "Ask: ResolveConflictAllow()");
        ExpectEqual(EventsText(RebindFire), "Keyboard:G", "Ask allow: fire has G");
        Expect(IsAction(PressedKey(Key.G), RebindJump), "Ask allow: jump keeps G");

        InputMapSettingsServer.ResetAction(RebindFire);
        ExpectEqual(EventsText(RebindFire), "Mouse:Left", "fire is back on the left button");
        rebinder.Begin(RebindFire, 0);
        rebinder.Update(PressedKey(Key.G));
        ExpectEqual(rebinder.ResolveConflictSwap(), Error.Ok, "Ask: ResolveConflictSwap()");
        ExpectEqual(Last(), "completed:test_fire:0:Keyboard:G", "Ask swap: Completed");
        ExpectEqual(EventsText(RebindJump), "Mouse:Left; Gamepad:Paddle1", "Ask swap: jump has the left button");
    }

    private void TestTwoRebinders()
    {
        var keyboard = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        var pad = MakeRebinder(InputActionRebinder.DeviceFilterEnum.Gamepad, InputActionRebinder.ConflictModeEnum.Reject);
        keyboard.Begin(RebindJump, 0);
        keyboard.Cancel();
        ExpectEqual(Last(), "canceled:test_jump:0", "Cancel() before any input");
        ExpectEqual(EventsText(RebindJump), "KeyboardLabel:G; Gamepad:Paddle1", "Cancel() changes nothing");

        keyboard.Begin(RebindJump, 0);
        pad.Begin(RebindJump, 0);
        Expect(!keyboard.Update(JoyButtonEvent(JoyButton.Paddle3, 0)), "the keyboard rebinder skips the pad");
        Expect(pad.Update(JoyButtonEvent(JoyButton.Paddle3, 0)), "the pad rebinder takes the pad");
        Expect(keyboard.IsActive() && !pad.IsActive(), "only the pad session ended");
        Expect(!pad.Update(PressedKey(Key.L)), "the ended pad rebinder takes nothing");
        Expect(keyboard.Update(PressedKey(Key.L)), "the keyboard rebinder takes the key");
        ExpectEqual(EventsText(RebindJump), "Keyboard:L; Gamepad:Paddle3", "both sessions changed their own slot");
    }

    private void TestBuiltinConflict()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        rebinder.IgnoredActions = System.Array.Empty<string>();
        ExpectEqual(rebinder.Begin("ui_accept", 0), Error.Unavailable, "a built-in action can't be rebound in CUSTOM_ONLY");
        rebinder.Begin(RebindJump, 0);
        rebinder.Update(PressedKey(Key.Enter));
        Expect(_log.Exists(entry => entry.StartsWith("conflict:test_jump:0:ui_accept:")), "Enter conflicts with the built-in ui_accept");
        Expect(rebinder.IsActive(), "Reject keeps waiting");
        rebinder.ConflictMode = InputActionRebinder.ConflictModeEnum.Swap;
        _log.Clear();
        rebinder.Update(PressedKey(Key.Enter));
        Expect(rebinder.IsActive(), "Swap with a built-in action that is not stored is refused");
        Expect(IsAction(PressedKey(Key.Enter), "ui_accept"), "ui_accept keeps Enter");
        rebinder.IgnoredActions = BuiltinActions();
        rebinder.ConflictMode = InputActionRebinder.ConflictModeEnum.Reject;
        _log.Clear();
        rebinder.Update(PressedKey(Key.Enter));
        ExpectEqual(Last(), "completed:test_jump:0:Keyboard:Enter", "ignored actions are no conflict");
        rebinder.Cancel();
    }

    private void TestSaveLoad()
    {
        string oldPath = InputMapSettingsServer.Singleton.GetFilePath();
        DirAccess.MakeDirRecursiveAbsolute(SavePath.GetBaseDir());
        InputMapSettingsServer.Singleton.SetFilePath(SavePath);
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.Any, InputActionRebinder.ConflictModeEnum.Reject);
        rebinder.Begin(RebindJump, 0);
        var ctrlJ = PressedKey(Key.J);
        ctrlJ.CtrlPressed = true;
        rebinder.Update(ctrlJ);
        rebinder.Begin(RebindJump, 1);
        rebinder.Update(JoyMotion(JoyAxis.TriggerLeft, 0.8f, 2));
        string stored = EventsText(RebindJump);
        ExpectEqual(stored, "Keyboard:Ctrl+J; GamepadAxis:LeftTrigger", "the stored form of both events");
        ExpectEqual(InputMapSettingsServer.EventsToText(InputMapSettingsServer.TextToEvents(stored)), stored, "the stored form survives the text round trip");
        ExpectEqual(InputMapSettingsServer.Singleton.Save(), Error.Ok, "Save()");
        InputMapSettingsServer.Rebind(RebindJump, PressedKey(Key.M));
        ExpectEqual(InputMapSettingsServer.Singleton.Load(), Error.Ok, "Load()");
        InputMapSettingsServer.Singleton.Apply();
        ExpectEqual(EventsText(RebindJump), stored, "the committed events are what a save and load gives back");
        Expect(IsAction(ctrlJ, RebindJump), "Ctrl+J works after the load");
        InputMapSettingsServer.Singleton.SetFilePath(oldPath);
        DirAccess.RemoveAbsolute(SavePath);
    }
}
