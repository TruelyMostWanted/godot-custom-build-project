using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue191FeatureInputActionRebinder;

/// <summary>
/// C# checks of 191-feature-input-action-rebinder. Check the same things as scripts/gd/check.gd.
/// The project defines r191_jump (key G, joypad Paddle1) and r191_fire (left mouse button) in project.godot;
/// every check starts from these bindings (ResetAll()). Keys and buttons are chosen so that no built-in ui_*
/// action uses them; the built-in actions are ignored except in the check of the built-in conflict.
/// </summary>
public partial class Issue191FeatureInputActionRebinderCheck : FeatureCheckCs
{
    private static readonly StringName Jump = "r191_jump";
    private static readonly StringName Fire = "r191_fire";
    private const string SavePath = "user://checks/191_input_map_cs.ini";

    private readonly List<string> _log = new();

    protected override string CheckName => "191-feature-input-action-rebinder";

    protected override Task RunChecks()
    {
        CheckClass();
        Setup();
        CheckFiltering();
        Setup();
        CheckReject();
        Setup();
        CheckSwapOrder();
        Setup();
        CheckAllow();
        Setup();
        CheckAsk();
        Setup();
        CheckTwoRebinders();
        Setup();
        CheckBuiltinConflict();
        Setup();
        CheckSaveLoad();
        InputMapSettingsServer.ResetAll();
        return Task.CompletedTask;
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
        if (action == Jump || action == Fire)
        {
            _log.Add($"changed:{action}");
        }
    }

    private void CheckClass()
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

    private void CheckFiltering()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        ExpectEqual(rebinder.Begin(Jump, 0), Error.Ok, "Begin()");
        ExpectEqual(Last(), "started:r191_jump:0", "Started is emitted");
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
        ExpectEqual(Last(), "completed:r191_jump:0:PhysicalKey:Ctrl+H", "Completed with Ctrl+H");
        ExpectEqual(EventsText(Jump), "PhysicalKey:Ctrl+H; JoypadButton:Paddle1", "slot 0 has Ctrl+H");
        Expect(IsAction(ctrlH, Jump), "Ctrl+H works at once");

        ExpectEqual(rebinder.Begin(Jump, 1), Error.Ok, "Begin() a new slot");
        var shift = PressedKey(Key.Shift);
        shift.ShiftPressed = true;
        Expect(rebinder.Update(shift), "Shift pressed is used");
        Expect(rebinder.Update(PressedKey(Key.Shift, false)), "Shift released alone is used");
        ExpectEqual(Last(), "completed:r191_jump:1:PhysicalKey:Shift", "a modifier released alone is bound");
        ExpectEqual(EventsText(Jump), "PhysicalKey:Ctrl+H; JoypadButton:Paddle1; PhysicalKey:Shift", "slot 1 is added at the end");

        rebinder.DeviceFilter = InputActionRebinder.DeviceFilterEnum.Gamepad;
        rebinder.GamepadDevice = 1;
        ExpectEqual(rebinder.Begin(Jump, 0), Error.Ok, "Begin() the gamepad slot");
        Expect(!rebinder.Update(PressedKey(Key.J)), "a key is outside Gamepad");
        Expect(!rebinder.Update(JoyMotion(JoyAxis.RightX, 0.3f, 1)), "a weak stick move is ignored");
        Expect(!rebinder.Update(JoyMotion(JoyAxis.RightX, 0.9f, 0)), "another pad is ignored");
        Expect(rebinder.Update(JoyMotion(JoyAxis.RightX, 0.9f, 1)), "a strong move of the pad is used");
        ExpectEqual(Last(), "completed:r191_jump:0:JoypadMotion:RightX+", "the direction is part of the binding");
        Expect(IsAction(JoyMotion(JoyAxis.RightX, 1.0f, 3), Jump), "the binding works for every pad");

        ExpectEqual(rebinder.Begin("r191_no_such_action", 0), Error.DoesNotExist, "Begin() of an unknown action");
        ExpectEqual(rebinder.Begin(Jump, -1), Error.InvalidParameter, "Begin() with a negative index");
    }

    private void CheckReject()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        rebinder.Begin(Fire, 0);
        Expect(rebinder.Update(PressedKey(Key.G)), "Reject: the conflicting key is used");
        ExpectEqual(Last(), "conflict:r191_fire:0:r191_jump:0", "Reject: Conflict names both slots");
        Expect(rebinder.IsActive(), "Reject: the session waits for another key");
        ExpectEqual(InputAction.EventToText(rebinder.GetConflictEvent()), "PhysicalKey:G", "GetConflictEvent()");
        ExpectEqual(EventsText(Fire), "MouseButton:Left", "Reject: fire is unchanged");
        Expect(rebinder.Update(PressedKey(Key.K)), "Reject: another key");
        ExpectEqual(Last(), "completed:r191_fire:0:PhysicalKey:K", "Reject: the free key is bound");
    }

    private void CheckSwapOrder()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Swap);
        InputMapSettingsServer.ActionEventsChanged += OnActionEventsChanged;
        rebinder.Begin(Fire, 0);
        _log.Clear();
        Expect(rebinder.Update(PressedKey(Key.G)), "Swap: the key is used");
        InputMapSettingsServer.ActionEventsChanged -= OnActionEventsChanged;
        ExpectEqual(EventsText(Fire), "PhysicalKey:G", "Swap: fire has G");
        ExpectEqual(EventsText(Jump), "MouseButton:Left; JoypadButton:Paddle1", "Swap: jump has the old event of fire");
        Expect(IsAction(MouseButtonEvent(MouseButton.Left), Jump), "Swap: the left button jumps");
        Expect(!IsAction(PressedKey(Key.G), Jump), "Swap: G no longer jumps");
        ExpectEqual(_log.Count, 3, "Swap: two server signals and Completed");
        if (_log.Count == 3)
        {
            Expect(_log.Contains("changed:r191_fire") && _log.Contains("changed:r191_jump"), "Swap: the server announces both actions");
            ExpectEqual(_log[2], "completed:r191_fire:0:PhysicalKey:G", "Swap: Completed comes after the server signals");
        }
    }

    private void CheckAllow()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.Any, InputActionRebinder.ConflictModeEnum.Allow);
        rebinder.Begin(Fire, 5);
        Expect(rebinder.Update(PressedKey(Key.G)), "Allow: the key is used");
        ExpectEqual(Last(), "completed:r191_fire:1:PhysicalKey:G", "Allow: added behind the last event");
        ExpectEqual(EventsText(Fire), "MouseButton:Left; PhysicalKey:G", "Allow: fire has G");
        Expect(IsAction(PressedKey(Key.G), Jump) && IsAction(PressedKey(Key.G), Fire), "Allow: G works for both");
    }

    private void CheckAsk()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Ask);
        ExpectEqual(rebinder.ResolveConflictAllow(), Error.Unconfigured, "Ask: nothing to resolve");
        rebinder.Begin(Fire, 0);
        Expect(rebinder.Update(PressedKey(Key.G)), "Ask: the key is used");
        Expect(rebinder.IsConflictPending(), "Ask: the conflict waits");
        Expect(!rebinder.Update(PressedKey(Key.K)), "Ask: no input while the menu asks");
        rebinder.Cancel();
        ExpectEqual(Last(), "canceled:r191_fire:0", "Ask: Cancel() emits Canceled");
        ExpectEqual(EventsText(Fire), "MouseButton:Left", "Ask: Cancel changes nothing");
        ExpectEqual(EventsText(Jump), "Key:G; JoypadButton:Paddle1", "Ask: jump unchanged");

        rebinder.Begin(Fire, 0);
        rebinder.Update(PressedKey(Key.G));
        ExpectEqual(rebinder.ResolveConflictAllow(), Error.Ok, "Ask: ResolveConflictAllow()");
        ExpectEqual(EventsText(Fire), "PhysicalKey:G", "Ask allow: fire has G");
        Expect(IsAction(PressedKey(Key.G), Jump), "Ask allow: jump keeps G");

        InputMapSettingsServer.ResetAction(Fire);
        ExpectEqual(EventsText(Fire), "MouseButton:Left", "fire is back on the left button");
        rebinder.Begin(Fire, 0);
        rebinder.Update(PressedKey(Key.G));
        ExpectEqual(rebinder.ResolveConflictSwap(), Error.Ok, "Ask: ResolveConflictSwap()");
        ExpectEqual(Last(), "completed:r191_fire:0:PhysicalKey:G", "Ask swap: Completed");
        ExpectEqual(EventsText(Jump), "MouseButton:Left; JoypadButton:Paddle1", "Ask swap: jump has the left button");
    }

    private void CheckTwoRebinders()
    {
        var keyboard = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        var pad = MakeRebinder(InputActionRebinder.DeviceFilterEnum.Gamepad, InputActionRebinder.ConflictModeEnum.Reject);
        keyboard.Begin(Jump, 0);
        keyboard.Cancel();
        ExpectEqual(Last(), "canceled:r191_jump:0", "Cancel() before any input");
        ExpectEqual(EventsText(Jump), "Key:G; JoypadButton:Paddle1", "Cancel() changes nothing");

        keyboard.Begin(Jump, 0);
        pad.Begin(Jump, 0);
        Expect(!keyboard.Update(JoyButtonEvent(JoyButton.Paddle3, 0)), "the keyboard rebinder skips the pad");
        Expect(pad.Update(JoyButtonEvent(JoyButton.Paddle3, 0)), "the pad rebinder takes the pad");
        Expect(keyboard.IsActive() && !pad.IsActive(), "only the pad session ended");
        Expect(!pad.Update(PressedKey(Key.L)), "the ended pad rebinder takes nothing");
        Expect(keyboard.Update(PressedKey(Key.L)), "the keyboard rebinder takes the key");
        ExpectEqual(EventsText(Jump), "PhysicalKey:L; JoypadButton:Paddle3", "both sessions changed their own slot");
    }

    private void CheckBuiltinConflict()
    {
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.KeyboardMouse, InputActionRebinder.ConflictModeEnum.Reject);
        rebinder.IgnoredActions = System.Array.Empty<string>();
        ExpectEqual(rebinder.Begin("ui_accept", 0), Error.Unavailable, "a built-in action can't be rebound in CUSTOM_ONLY");
        rebinder.Begin(Jump, 0);
        rebinder.Update(PressedKey(Key.Enter));
        Expect(_log.Exists(entry => entry.StartsWith("conflict:r191_jump:0:ui_accept:")), "Enter conflicts with the built-in ui_accept");
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
        ExpectEqual(Last(), "completed:r191_jump:0:PhysicalKey:Enter", "ignored actions are no conflict");
        rebinder.Cancel();
    }

    private void CheckSaveLoad()
    {
        string oldPath = InputMapSettingsServer.Singleton.GetFilePath();
        DirAccess.MakeDirRecursiveAbsolute(SavePath.GetBaseDir());
        InputMapSettingsServer.Singleton.SetFilePath(SavePath);
        var rebinder = MakeRebinder(InputActionRebinder.DeviceFilterEnum.Any, InputActionRebinder.ConflictModeEnum.Reject);
        rebinder.Begin(Jump, 0);
        var ctrlJ = PressedKey(Key.J);
        ctrlJ.CtrlPressed = true;
        rebinder.Update(ctrlJ);
        rebinder.Begin(Jump, 1);
        rebinder.Update(JoyMotion(JoyAxis.TriggerLeft, 0.8f, 2));
        string stored = EventsText(Jump);
        ExpectEqual(stored, "PhysicalKey:Ctrl+J; JoypadMotion:TriggerLeft+", "the stored form of both events");
        ExpectEqual(InputMapSettingsServer.EventsToText(InputMapSettingsServer.TextToEvents(stored)), stored, "the stored form survives the text round trip");
        ExpectEqual(InputMapSettingsServer.Singleton.Save(), Error.Ok, "Save()");
        InputMapSettingsServer.Rebind(Jump, PressedKey(Key.M));
        ExpectEqual(InputMapSettingsServer.Singleton.Load(), Error.Ok, "Load()");
        InputMapSettingsServer.Singleton.Apply();
        ExpectEqual(EventsText(Jump), stored, "the committed events are what a save and load gives back");
        Expect(IsAction(ctrlJ, Jump), "Ctrl+J works after the load");
        InputMapSettingsServer.Singleton.SetFilePath(oldPath);
        DirAccess.RemoveAbsolute(SavePath);
    }
}
