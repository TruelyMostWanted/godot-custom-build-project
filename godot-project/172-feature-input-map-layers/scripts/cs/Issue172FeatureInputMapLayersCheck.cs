using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue172FeatureInputMapLayers;

/// <summary>
/// C# checks of 172-feature-input-map-layers. Check the same things as scripts/gd/check.gd.
/// The project defines the action l172_attack (key F, deadzone 0.3) in project.godot. Other actions are
/// created at runtime and removed again. The wrappers of shared resources are not disposed.
/// </summary>
public partial class Issue172FeatureInputMapLayersCheck : FeatureCheckCs
{
    private static readonly StringName Attack = "l172_attack";
    private const int BulkCount = 100;
    private const int BulkFirstKey = 0x4E00; // Unicode keycodes no other action uses.

    protected override string CheckName => "172-feature-input-map-layers";

    protected override Task RunChecks()
    {
        CheckInputAction();
        CheckInputActions();
        CheckResourceFiles();
        CheckLayers();
        CheckSetInputAction();
        CheckParseCost();
        CheckSettingsServer();
        return Task.CompletedTask;
    }

    private static InputEventKey PressedKey(Key keycode, bool pressed = true)
    {
        return new InputEventKey { Keycode = keycode, PhysicalKeycode = keycode, KeyLabel = keycode, Pressed = pressed };
    }

    private static bool IsAction(InputEvent @event, StringName action)
    {
        return InputMap.EventIsAction(@event, action);
    }

    private static bool Approx(float actual, float expected)
    {
        return Mathf.IsEqualApprox(actual, expected);
    }

    private void CheckInputAction()
    {
        var action = new InputAction();
        ExpectEqual(action.Action.ToString(), "", "a new InputAction has no name");
        Expect(Approx((float)action.Deadzone, 0.2f), "the default deadzone is 0.2");
        ExpectEqual(action.Events.Count, 0, "a new InputAction has no events");
        action.Action = "l172_demo";
        action.Deadzone = 0.4f;
        ExpectEqual(action.Action.ToString(), "l172_demo", "action name");
        Expect(Approx((float)action.Deadzone, 0.4f), "deadzone");

        action.Events = InputMapSettingsServer.TextToEvents("Key:Ctrl+S; JoypadButton:A");
        ExpectEqual(action.GetEventCount(), 2, "the text form gives two events");
        Expect(action.Events[0] is InputEventKey, "the first event is a key");
        Expect(action.Events[1] is InputEventJoypadButton, "the second event is a joypad button");
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "Key:Ctrl+S; JoypadButton:A", "bindings round trip");
        ExpectEqual(action.GetDeviceEvents(InputAction.InputDevice.Keyboard).Count, 1, "one keyboard event");
        ExpectEqual(action.GetDeviceEvents(InputAction.InputDevice.Gamepad).Count, 1, "one gamepad event");
        ExpectEqual(action.GetDeviceEvents(InputAction.InputDevice.Mouse).Count, 0, "no mouse event");

        InputEvent mouse = InputAction.TextToEvent("MouseButton:Left");
        action.AddEvent(mouse);
        ExpectEqual(action.GetEventCount(), 3, "AddEvent() adds an event");
        Expect(action.HasEvent(mouse), "HasEvent()");
        action.AddEvent(InputAction.TextToEvent("MouseButton:Left"));
        ExpectEqual(action.GetEventCount(), 3, "AddEvent() ignores an equal event");
        Expect(action.RemoveEvent(mouse), "RemoveEvent() finds the event");
        Expect(!action.RemoveEvent(mouse), "RemoveEvent() of a missing event is false");
        action.Events = new Godot.Collections.Array<InputEvent> { PressedKey(Key.W) };
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "PhysicalKey:W", "setting events updates the bindings text");
        action.ClearEvents();
        ExpectEqual(action.GetEventCount(), 0, "ClearEvents()");
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "", "no events, no bindings text");

        // Touch has no text form: kept in events, left out of bindings.
        action.AddEvent(new InputEventScreenTouch());
        action.AddEvent(InputAction.TextToEvent("Key:Space"));
        ExpectEqual(action.GetEventCount(), 2, "touch and key are both events");
        ExpectEqual(InputMapSettingsServer.EventsToText(action.Events), "Key:Space", "touch is not part of the bindings text");

        string[] texts = ["Key:Ctrl+S", "PhysicalKey:W", "MouseButton:Ctrl+WheelUp", "JoypadButton:LeftShoulder", "JoypadMotion:LeftX+"];
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
        ExpectEqual(InputAction.EventToText(new InputEventScreenTouch()), "", "a touch has no text");
        ExpectEqual(InputAction.GetEventDevice(PressedKey(Key.A)), InputAction.InputDevice.Keyboard, "key device");
        ExpectEqual(InputAction.GetEventDevice(InputAction.TextToEvent("MouseButton:Left")), InputAction.InputDevice.Mouse, "mouse device");
        ExpectEqual(InputAction.GetEventDevice(InputAction.TextToEvent("JoypadMotion:LeftX+")), InputAction.InputDevice.Gamepad, "joypad axis device");
        ExpectEqual(InputAction.GetEventDevice(new InputEventScreenTouch()), InputAction.InputDevice.Max, "touch has no device");
    }

    private static string Names(InputActions list)
    {
        return string.Join(",", list.GetActionNames().Select(name => name.ToString()));
    }

    private void CheckInputActions()
    {
        var list = new InputActions();
        ExpectEqual(list.GetActionCount(), 0, "a new InputActions is empty");
        var jump = new InputAction { Action = "l172_jump", Events = InputMapSettingsServer.TextToEvents("Key:Space") };
        var fire = new InputAction { Action = "l172_fire", Events = InputMapSettingsServer.TextToEvents("MouseButton:Left") };
        list.AddAction(jump);
        list.AddAction(fire);
        ExpectEqual(list.GetActionCount(), 2, "two actions");
        Expect(list.HasAction("l172_jump") && list.HasAction("l172_fire"), "HasAction()");
        Expect(!list.HasAction("l172_none"), "HasAction() of a missing name is false");
        Expect(list.GetAction("l172_jump") == jump, "GetAction() returns the same resource");
        Expect(list.GetAction("l172_none") == null, "GetAction() of a missing name is null");
        ExpectEqual(Names(list), "l172_jump,l172_fire", "names in insertion order");

        var jump2 = new InputAction { Action = "l172_jump", Events = InputMapSettingsServer.TextToEvents("Key:W") };
        list.AddAction(jump2);
        ExpectEqual(list.GetActionCount(), 2, "adding a name again replaces the action");
        Expect(list.GetAction("l172_jump") == jump2, "the new action replaced the old one");
        ExpectEqual(Names(list), "l172_jump,l172_fire", "the replaced action keeps its place");

        // A rename of an action in the list is found by the new name.
        fire.Action = "l172_shoot";
        Expect(list.HasAction("l172_shoot") && !list.HasAction("l172_fire"), "renaming an action updates the lookup");

        Expect(list.RemoveAction("l172_shoot"), "RemoveAction()");
        Expect(!list.RemoveAction("l172_shoot"), "RemoveAction() of a missing name is false");
        ExpectEqual(list.GetActionCount(), 1, "one action left");
        list.Clear();
        ExpectEqual(list.GetActionCount(), 0, "Clear()");
        list.Actions = new Godot.Collections.Array<InputAction> { jump, jump2 };
        ExpectEqual(list.GetActionCount(), 1, "set Actions: the later action of a name wins");
        Expect(list.GetAction("l172_jump") == jump2, "set Actions: the later one is kept");
    }

    private void CheckResourceFiles()
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
        var jump = new InputAction { Action = "l172_file_jump", Deadzone = 0.5f };
        jump.Events = InputMapSettingsServer.TextToEvents("Key:Space; PhysicalKey:Ctrl+W; MouseButton:Right; JoypadButton:A@1; JoypadMotion:LeftY-");
        jump.AddEvent(new InputEventScreenTouch { Index = 2 });
        list.AddAction(jump);
        list.AddAction(new InputAction { Action = "l172_file_empty" });
        foreach (string path in new[] { "user://l172_actions_cs.tres", "user://l172_actions_cs.res" })
        {
            ExpectEqual(ResourceSaver.Save(list, path), Error.Ok, $"save {path}");
            var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as InputActions;
            Expect(loaded != null && loaded != list, $"load {path}");
            if (loaded == null)
            {
                continue;
            }
            ExpectEqual(string.Join(",", loaded.GetActionNames()), "l172_file_jump,l172_file_empty", $"{path}: action names");
            InputAction read = loaded.GetAction("l172_file_jump");
            Expect(Approx((float)read.Deadzone, 0.5f), $"{path}: deadzone");
            ExpectEqual(read.GetEventCount(), 6, $"{path}: all events, touch too");
            ExpectEqual(InputMapSettingsServer.EventsToText(read.Events), InputMapSettingsServer.EventsToText(jump.Events), $"{path}: the same events");
            Expect(read.Events[5] is InputEventScreenTouch touch && touch.Index == 2, $"{path}: the touch event");
            ExpectEqual(loaded.GetAction("l172_file_empty").GetEventCount(), 0, $"{path}: an action without events");
        }
        string text = FileAccess.GetFileAsString("user://l172_actions_cs.tres");
        Expect(text.Contains("events = Array[InputEvent]([SubResource("), ".tres: events as sub-resources");
        Expect(text.Contains("deadzone = 0.5"), ".tres: the deadzone that is not the default");
        Expect(!text.Contains("bindings"), ".tres: no text form");

        // The text form of InputMapSettingsServer: one pad with @<device>, events without text left out.
        ExpectEqual(InputMapSettingsServer.EventsToText(jump.Events), "Key:Space; PhysicalKey:Ctrl+W; MouseButton:Right; JoypadButton:A@1; JoypadMotion:LeftY-", "EventsToText()");
        ExpectEqual(InputMapSettingsServer.TextToEvents("Key:Space; NotAType:X").Count, 1, "TextToEvents() skips invalid items");
        ExpectEqual(InputMapSettingsServer.TextToEvents("JoypadButton:A@1")[0].Device, 1, "TextToEvents(): the pad");
    }

    private void CheckLayers()
    {
        Expect(InputMap.IsBuiltinInputAction("ui_accept"), "ui_accept is built-in");
        Expect(!InputMap.IsCustomInputAction("ui_accept"), "ui_accept is not custom");
        Expect(!InputMap.IsCustomInputAction("ui_text_backspace_word.macos"), "a platform variant of a built-in action is never custom");
        Expect(InputMap.IsCustomInputAction(Attack), "a project action is custom");
        Expect(!InputMap.IsBuiltinInputAction(Attack), "a project action is not built-in");
        Expect(!InputMap.IsCustomInputAction("l172_not_there"), "a missing action is not custom");
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
        ExpectEqual(attack.Action.ToString(), "l172_attack", "GetInputAction() name");
        Expect(Approx((float)attack.Deadzone, 0.3f), "GetInputAction() deadzone from the project");
        ExpectEqual(InputMapSettingsServer.EventsToText(attack.Events), "Key:F", "GetInputAction() events as text");
        ExpectEqual(attack.GetInstanceId(), custom.GetAction(Attack).GetInstanceId(), "GetInputAction() is the action of the custom layer");
        InputEvent keyH = InputAction.TextToEvent("Key:H");
        attack.AddEvent(keyH);
        Expect(IsAction(PressedKey(Key.H), Attack), "an event added to the returned action works");
        attack.RemoveEvent(keyH);
        Expect(!IsAction(PressedKey(Key.H), Attack), "an event removed from the returned action no longer works");
        ExpectEqual(InputMap.GetInputAction(Attack).GetEventCount(), 1, "one event again");

        // Direct edits of a layer reach the InputMap.
        custom.AddAction(new InputAction { Action = "l172_direct", Events = InputMapSettingsServer.TextToEvents("Key:F9") });
        Expect(InputMap.HasAction("l172_direct"), "an action added to the custom layer is in the InputMap");
        Expect(IsAction(PressedKey(Key.F9), "l172_direct"), "its key works");
        Expect(custom.RemoveAction("l172_direct"), "remove it from the layer");
        Expect(!InputMap.HasAction("l172_direct"), "an action removed from the layer is gone from the InputMap");
    }

    private void CheckSetInputAction()
    {
        InputAction original = InputMap.GetInputAction(Attack);
        var changed = new InputAction { Action = Attack, Deadzone = 0.6f, Events = InputMapSettingsServer.TextToEvents("Key:G") };
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
            extra.AddAction(new InputAction { Action = $"l172_extra_{n}", Events = InputMapSettingsServer.TextToEvents($"Key:F{n + 5}") });
        }
        InputMap.SetInputActions(extra);
        Expect(InputMap.HasAction("l172_extra_0") && InputMap.HasAction("l172_extra_1"), "SetInputActions() adds new actions");
        Expect(InputMap.IsCustomInputAction("l172_extra_1"), "a new action is custom");
        Expect(IsAction(PressedKey(Key.F6), "l172_extra_1"), "the new action reacts to its key");
        InputMap.EraseAction("l172_extra_0");
        InputMap.EraseAction("l172_extra_1");
    }

    private void CheckParseCost()
    {
        for (int i = 0; i < BulkCount; i++)
        {
            StringName name = $"l172_bulk_{i}";
            InputMap.AddAction(name);
            InputMap.ActionAddEvent(name, PressedKey((Key)(BulkFirstKey + i)));
        }
        Expect(InputMap.HasAction("l172_bulk_99"), "100 actions were added");

        foreach (int i in new[] { 0, 37, 99 })
        {
            StringName bulk = $"l172_bulk_{i}";
            Input.ParseInputEvent(PressedKey((Key)(BulkFirstKey + i)));
            Input.FlushBufferedEvents();
            Expect(Input.IsActionPressed(bulk), $"{bulk} is pressed after ParseInputEvent()");
            StringName other = $"l172_bulk_{(i + 1) % BulkCount}";
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
        Expect(!Input.IsActionPressed("l172_bulk_50"), "the bulk action is released at the end");

        for (int i = 0; i < BulkCount; i++)
        {
            InputMap.EraseAction($"l172_bulk_{i}");
        }
        Expect(!InputMap.HasAction("l172_bulk_0"), "the bulk actions were removed");
        Expect(!IsAction(PressedKey((Key)BulkFirstKey), "l172_bulk_0"), "a removed action no longer matches");
    }

    private void CheckSettingsServer()
    {
        // The property save_mode of the singleton is read through Get() (0 = CUSTOM_ONLY, 1 = ALL).
        var server = InputMapSettingsServer.Singleton;
        ExpectEqual(server.Get("save_mode").AsInt32(), 0, "save_mode is CUSTOM_ONLY by default");
        Expect(server.GetDefaultSettings() is InputActions, "the settings resource is an InputActions");
        Expect(InputMapSettingsServer.HasAction(Attack), "the project action is managed");
        Expect(!InputMapSettingsServer.HasAction("ui_accept"), "built-in actions are not managed in CUSTOM_ONLY");
        ExpectEqual(InputMapSettingsServer.Rebind("ui_accept", PressedKey(Key.Q)), Error.DoesNotExist, "a built-in action can't be rebound in CUSTOM_ONLY");

        InputMapSettingsServer.ResetAll();
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Attack, InputAction.InputDevice.Keyboard, 0)), "Key:F", "slot 0 is F");
        ExpectEqual(InputMapSettingsServer.Rebind(Attack, PressedKey(Key.X)), Error.Ok, "Rebind() slot 0");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Attack, InputAction.InputDevice.Keyboard, 0)), "PhysicalKey:X", "GetEvent() gives the new key");
        Expect(IsAction(PressedKey(Key.X), Attack), "the new key works at once");
        Expect(!IsAction(PressedKey(Key.F), Attack), "the old key no longer works");
        ExpectEqual(InputMapSettingsServer.Rebind(Attack, PressedKey(Key.Y), 1), Error.Ok, "Rebind() slot 1");
        ExpectEqual(InputMapSettingsServer.GetSlotCount(Attack, InputAction.InputDevice.Keyboard), 2, "two keyboard slots");
        Expect(InputMapSettingsServer.EventsToText(InputMapSettingsServer.GetInputAction(Attack).Events).Contains("PhysicalKey:Y"), "GetInputAction() shows both keys");

        InputMapSettingsServer.ClearEvent(Attack, InputAction.InputDevice.Keyboard, 0);
        ExpectEqual(InputMapSettingsServer.GetSlotCount(Attack, InputAction.InputDevice.Keyboard), 1, "ClearEvent() removes the slot");
        ExpectEqual(InputAction.EventToText(InputMapSettingsServer.GetEvent(Attack, InputAction.InputDevice.Keyboard, 0)), "PhysicalKey:Y", "later slots move up");
        Expect(!IsAction(PressedKey(Key.X), Attack), "the cleared key no longer works");
        Expect(IsAction(PressedKey(Key.Y), Attack), "the moved key works");

        InputMapSettingsServer.ResetAction(Attack);
        Expect(IsAction(PressedKey(Key.F), Attack), "ResetAction(): F works again");
        Expect(!IsAction(PressedKey(Key.Y), Attack), "ResetAction(): Y no longer works");
        ExpectEqual(InputMapSettingsServer.EventsToText(InputMapSettingsServer.GetInputAction(Attack).Events), "Key:F", "ResetAction(): the project bindings");

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
}
