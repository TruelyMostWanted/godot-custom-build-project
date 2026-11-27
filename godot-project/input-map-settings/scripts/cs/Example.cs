using Godot;

namespace TruelyInteractive.InputMapSettings;

/// <summary>Minimal example of Input Map Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override async void _Ready()
    {
        // Using the API
        // Set: binds an event to a slot of its device, applied to the InputMap at once.
        var wKey = new InputEventKey { PhysicalKeycode = Key.W };
        InputMapSettingsServer.Rebind("jump", wKey, 0); // Keyboard slot 0: W instead of Space.
        InputMapSettingsServer.Rebind("jump", InputAction.TextToEvent("Gamepad:B"), 0); // The text form of an event.
        InputMapSettingsServer.ClearEvent("interact", InputAction.InputDevice.Gamepad, 0);

        // Get: the event of one slot, and a label for the UI.
        InputEvent ev = InputMapSettingsServer.GetEvent("jump", InputAction.InputDevice.Keyboard, 0);
        GD.Print(InputMapSettingsServer.GetEventLabel(ev)); // W
        GD.Print(InputMap.ActionHasEvent("jump", ev)); // True

        // ResetAction() gives one action the bindings of the project back.
        InputMapSettingsServer.ResetAction("fire");

        // Methods of the base class SettingsServer go through Singleton.
        // In APPLY_MODE_MANUAL, changes wait for Apply().
        InputMapSettingsServer.Singleton.Apply();
        // Autosave writes the file at the end of the frame; Save() writes it now.
        InputMapSettingsServer.Singleton.Save();
        // Load() reads the file into the pending values; Apply() pushes them to the InputMap.
        InputMapSettingsServer.Singleton.Load();
        InputMapSettingsServer.Singleton.Apply();

        // A rebind button
        var button = new RebindButton();
        AddChild(button);
        GD.Print("Button: ", button.Text);
        button.EmitSignal(BaseButton.SignalName.Pressed);
        GD.Print("Button waiting: ", button.Text);
        var pressedKey = new InputEventKey { Keycode = Key.Q, PhysicalKeycode = Key.Q, Pressed = true };
        Input.ParseInputEvent(pressedKey);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print("Button after the key: ", button.Text);

        // Ask before taking a key away (the dialog is left out: it needs a window).

        InputMapSettingsServer.ResetAll();
        InputMapSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
