using Godot;

namespace TruelyInteractive.InputIcons;

/// <summary>Minimal example of Input Icons: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private readonly TextureRect _prompt = new();

    public override void _Ready()
    {
        // Create an icon set (the example uses icon.svg for every button instead of the prompt images).
        var xbox = new InputIconSet();
        var promptIcon = GD.Load<Texture2D>("res://icon.svg");
        xbox.SetJoyButtonIcon(JoyButton.A, promptIcon);
        xbox.SetJoyAxisIcon(JoyAxis.LeftX, -1.0f, promptIcon);
        xbox.JoyButtonIcons = new Godot.Collections.Dictionary
        {
            { "B", promptIcon },
            { "LeftStickPress", promptIcon },
        };
        DirAccess.MakeDirRecursiveAbsolute("user://input-icons-example");
        ResourceSaver.Save(xbox, "user://input-icons-example/xbox.tres");
        GD.Print("Gamepad button icons: ", xbox.JoyButtonIcons.Keys.Count);

        // Register the sets in the project settings (here at runtime).
        InputIconServer.SetIconSet(InputIconServer.InputIconFamily.Playstation, xbox);
        GD.Print("Override: ", InputIconServer.HasIconSetOverride(InputIconServer.InputIconFamily.Playstation));
        InputIconServer.ResetIconSet(InputIconServer.InputIconFamily.Playstation);

        // Show the prompt of an action
        AddChild(_prompt);
        InputIconServer.IconSetChanged += family => UpdatePrompt();
        InputIconServer.SetIconSet(InputIconServer.InputIconFamily.KeyboardMouse, xbox);
        UpdatePrompt();
        GD.Print("Prompt: ", _prompt.Texture);

        // Let the player choose the gamepad icons
        InputIconServer.GamepadFamily = InputIconServer.InputIconFamily.Nintendo;
        GD.Print("Gamepad family: ", InputIconServer.GamepadFamily);

        // Fallback icons: a button without an icon gets the fallback of its type.
        xbox.FallbackJoyButtonIcon = promptIcon;
        var padX = new InputEventJoypadButton { ButtonIndex = JoyButton.X };
        GD.Print("Fallback icon: ", xbox.GetIcon(padX));

        InputIconServer.ResetIconSet(InputIconServer.InputIconFamily.KeyboardMouse);
        InputIconServer.GamepadFamily = InputIconServer.InputIconFamily.Auto;
        DirAccess.RemoveAbsolute("user://input-icons-example/xbox.tres");
        DirAccess.RemoveAbsolute("user://input-icons-example");
        GetTree().Quit();
    }

    private void UpdatePrompt()
    {
        var events = InputMap.ActionGetEvents("jump");
        _prompt.Texture = events.Count > 0 ? InputIconServer.GetIcon(events[0]) : null;
    }
}
