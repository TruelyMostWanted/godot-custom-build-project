using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue198FeatureInputIcons;

/// <summary>
/// C# checks of 198-feature-input-icons. Check the same things as scripts/gd/check.gd.
/// The icons are made at runtime (solid colors, CC0): the size tells them apart.
/// </summary>
public partial class Issue198FeatureInputIconsCheck : FeatureCheckCs
{
    private const string SavePath = "user://checks/198_input_icons_cs.tres";

    protected override string CheckName => "198-feature-input-icons";

    protected override Task RunChecks()
    {
        CheckClasses();
        CheckSetLookup();
        CheckTextAndInspector();
        CheckSaveLoad();
        CheckServer();
        ResetServer();
        return Task.CompletedTask;
    }

    private static Texture2D MakeIcon(int size)
    {
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color((size % 7) / 7.0f, 0.5f, 1.0f));
        return ImageTexture.CreateFromImage(image);
    }

    private static InputEventKey KeyEvent(Key keycode)
    {
        return new InputEventKey { PhysicalKeycode = keycode };
    }

    private static InputEventJoypadButton JoyButtonEvent(JoyButton button)
    {
        return new InputEventJoypadButton { ButtonIndex = button, Device = -1 };
    }

    private static InputEventJoypadMotion JoyAxisEvent(JoyAxis axis, float value)
    {
        return new InputEventJoypadMotion { Axis = axis, AxisValue = value, Device = -1 };
    }

    private void CheckClasses()
    {
        Expect(ClassDB.ClassExists("InputIconSet"), "InputIconSet class exists");
        Expect(ClassDB.IsParentClass("InputIconSet", "Resource"), "InputIconSet is a Resource");
        Expect(Engine.HasSingleton("InputIconServer"), "InputIconServer singleton exists");
        Expect(ProjectSettings.HasSetting("input_devices/icons/xbox"), "project setting input_devices/icons/xbox exists");
        Expect((int)InputIconServer.InputIconFamily.Auto == -1, "INPUT_ICON_FAMILY_AUTO is -1");
        Expect((int)InputIconServer.InputIconFamily.Max == 5, "five icon families");
    }

    private void CheckSetLookup()
    {
        var icons = new InputIconSet();
        Texture2D space = MakeIcon(10);
        Texture2D left = MakeIcon(11);
        Texture2D a = MakeIcon(12);
        Texture2D stickLeft = MakeIcon(13);
        Texture2D trigger = MakeIcon(14);
        Texture2D fallback = MakeIcon(15);
        icons.SetKeyIcon(Key.Space, space);
        icons.SetMouseButtonIcon(MouseButton.Left, left);
        icons.SetJoyButtonIcon(JoyButton.A, a);
        icons.SetJoyAxisIcon(JoyAxis.LeftX, -1.0f, stickLeft);
        icons.SetJoyAxisIcon(JoyAxis.TriggerRight, 1.0f, trigger);
        icons.FallbackJoyButtonIcon = fallback;

        Expect(icons.GetKeyIcon(Key.Space) == space, "key icon per Key");
        Expect(icons.GetMouseButtonIcon(MouseButton.Left) == left, "icon per MouseButton");
        Expect(icons.GetJoyButtonIcon(JoyButton.A) == a, "icon per JoyButton");
        Expect(icons.GetJoyAxisIcon(JoyAxis.LeftX, -0.4f) == stickLeft, "axis icon per direction (negative)");
        Expect(icons.GetJoyAxisIcon(JoyAxis.LeftX, 1.0f) == null, "other direction has no icon");
        Expect(icons.GetIcon(KeyEvent(Key.Space)) == space, "get_icon of a key event");
        InputEventKey ctrlSpace = KeyEvent(Key.Space);
        ctrlSpace.CtrlPressed = true;
        Expect(icons.GetIcon(ctrlSpace) == space, "modifiers are ignored");
        Expect(icons.GetIcon(JoyAxisEvent(JoyAxis.LeftX, -1.0f)) == stickLeft, "get_icon of an axis event");
        Expect(icons.GetIcon(JoyAxisEvent(JoyAxis.TriggerRight, 1.0f)) == trigger, "get_icon of a trigger");
        Expect(icons.GetIcon(JoyButtonEvent(JoyButton.X)) == fallback, "fallback icon of the event type");
        Expect(icons.GetIcon(JoyButtonEvent(JoyButton.X), false) == null, "no fallback with use_fallback = false");
        Expect(icons.HasIcon(JoyButtonEvent(JoyButton.A)), "has_icon for a set button");
        Expect(!icons.HasIcon(JoyButtonEvent(JoyButton.X)), "has_icon ignores fallbacks");
        icons.Clear();
        Expect(icons.GetIcon(KeyEvent(Key.Space)) == null, "clear removes all icons");
    }

    private void CheckTextAndInspector()
    {
        var icons = new InputIconSet();
        Texture2D press = MakeIcon(20);
        Texture2D up = MakeIcon(21);
        Texture2D wheel = MakeIcon(22);
        icons.JoyButtonIcons = new Godot.Collections.Dictionary { { "LeftStickPress", press }, { (int)JoyButton.B, up } };
        Expect(icons.GetJoyButtonIcon(JoyButton.LeftStick) == press, "dictionary keyed by text name");
        Expect(icons.GetJoyButtonIcon(JoyButton.B) == up, "dictionary keyed by enum value");
        Godot.Collections.Dictionary saved = icons.JoyButtonIcons;
        Expect(saved.ContainsKey("LeftStickPress") && saved.ContainsKey("B"), "dictionary is read back with text names");
        Expect(icons.GetIconForText("Gamepad:LeftStickPress") == press, "get_icon_for_text");
        Expect(icons.GetIconForText("no event") == null, "invalid text gives null");
        icons.Set("mouse/WheelUp", wheel);
        Expect(icons.GetMouseButtonIcon(MouseButton.WheelUp) == wheel, "Inspector property mouse/WheelUp");
        icons.Set("gamepad_axes/LeftStickUp", up);
        Expect(icons.GetJoyAxisIcon(JoyAxis.LeftY, -1.0f) == up, "Inspector property gamepad_axes/LeftStickUp");
        var names = new HashSet<string>();
        foreach (Godot.Collections.Dictionary property in icons.GetPropertyList())
        {
            names.Add((string)property["name"]);
        }
        Expect(names.Contains("keyboard/Space"), "Inspector lists keyboard/Space");
        Expect(names.Contains("gamepad_buttons/LeftStickPress"), "Inspector lists gamepad_buttons/LeftStickPress");
        Expect(names.Contains("gamepad_axes/RightTrigger"), "Inspector lists gamepad_axes/RightTrigger");
    }

    private void CheckSaveLoad()
    {
        DirAccess.MakeDirRecursiveAbsolute("user://checks");
        var icons = new InputIconSet();
        icons.SetKeyIcon(Key.Escape, MakeIcon(30));
        icons.SetJoyAxisIcon(JoyAxis.RightX, 1.0f, MakeIcon(31));
        Expect(ResourceSaver.Save(icons, SavePath) == Error.Ok, "InputIconSet saves as .tres");
        var loaded = ResourceLoader.Load(SavePath, "", ResourceLoader.CacheMode.Ignore) as InputIconSet;
        Expect(loaded != null, "InputIconSet loads from .tres");
        if (loaded == null)
        {
            return;
        }
        Texture2D escape = loaded.GetKeyIcon(Key.Escape);
        Expect(escape != null && escape.GetWidth() == 30, "loaded key icon");
        Texture2D stick = loaded.GetJoyAxisIcon(JoyAxis.RightX, 1.0f);
        Expect(stick != null && stick.GetWidth() == 31, "loaded axis icon");
    }

    private void CheckServer()
    {
        var keyboard = new InputIconSet();
        var generic = new InputIconSet();
        var playstation = new InputIconSet();
        Texture2D keyW = MakeIcon(40);
        Texture2D genericA = MakeIcon(41);
        Texture2D genericB = MakeIcon(42);
        Texture2D psA = MakeIcon(43);
        Texture2D psFallback = MakeIcon(44);
        keyboard.SetKeyIcon(Key.W, keyW);
        generic.SetJoyButtonIcon(JoyButton.A, genericA);
        generic.SetJoyButtonIcon(JoyButton.B, genericB);
        playstation.SetJoyButtonIcon(JoyButton.A, psA);
        playstation.FallbackJoyButtonIcon = psFallback;

        var changed = new List<InputIconServer.InputIconFamily>();
        InputIconServer.IconSetChangedEventHandler onChanged = family => changed.Add(family);
        InputIconServer.IconSetChanged += onChanged;
        InputIconServer.SetIconSet(InputIconServer.InputIconFamily.KeyboardMouse, keyboard);
        InputIconServer.SetIconSet(InputIconServer.InputIconFamily.GenericGamepad, generic);
        InputIconServer.SetIconSet(InputIconServer.InputIconFamily.Playstation, playstation);
        Expect(changed.Count == 3 && changed[2] == InputIconServer.InputIconFamily.Playstation, "icon_set_changed on set_icon_set");
        playstation.SetJoyButtonIcon(JoyButton.Y, MakeIcon(45));
        Expect(changed.Count == 4, "icon_set_changed when a set is edited");
        InputIconServer.IconSetChanged -= onChanged;

        const InputIconServer.InputIconFamily ps = InputIconServer.InputIconFamily.Playstation;
        Expect(InputIconServer.GetIconSet(ps) == playstation, "get_icon_set");
        Expect(InputIconServer.GetIcon(KeyEvent(Key.W)) == keyW, "keys use KEYBOARD_MOUSE");
        Expect(InputIconServer.GetEventFamily(KeyEvent(Key.W)) == InputIconServer.InputIconFamily.KeyboardMouse, "family of a key");
        Expect(InputIconServer.GetIcon(JoyButtonEvent(JoyButton.A), ps) == psA, "specific icon of the family");
        Expect(InputIconServer.GetIcon(JoyButtonEvent(JoyButton.B), ps) == genericB, "then the icon of GENERIC_GAMEPAD");
        Expect(InputIconServer.GetIcon(JoyButtonEvent(JoyButton.X), ps) == psFallback, "then the fallback icon");
        Expect(InputIconServer.GetIconForText("Gamepad:A", ps) == psA, "get_icon_for_text with family");

        InputIconServer.GamepadFamily = ps;
        Expect(InputIconServer.GetIcon(JoyButtonEvent(JoyButton.A)) == psA, "gamepad_family forces the family");
        InputIconServer.GamepadFamily = InputIconServer.InputIconFamily.Auto;

        Expect(InputIconServer.GetFamilyForJoyName("Xbox Wireless Controller") == InputIconServer.InputIconFamily.Xbox, "Xbox name");
        Expect(InputIconServer.GetFamilyForJoyName("DualSense Wireless Controller") == ps, "DualSense name");
        Expect(InputIconServer.GetFamilyForJoyName("Wireless Controller", 0x054C) == ps, "Sony vendor id");
        Expect(InputIconServer.GetFamilyForJoyName("057e-2009-Pro Controller") == InputIconServer.InputIconFamily.Nintendo, "browser Gamepad.id");
        Expect(InputIconServer.GetFamilyForJoyName("Some Pad") == InputIconServer.InputIconFamily.GenericGamepad, "unknown name");

        InputIconServer.ResetIconSet(ps);
        Expect(!InputIconServer.HasIconSetOverride(ps), "reset_icon_set drops the override");
        Expect(InputIconServer.GetIconSet(ps) == null, "no project setting: no set");
    }

    private static void ResetServer()
    {
        for (int i = 0; i < (int)InputIconServer.InputIconFamily.Max; i++)
        {
            InputIconServer.ResetIconSet((InputIconServer.InputIconFamily)i);
        }
        InputIconServer.GamepadFamily = InputIconServer.InputIconFamily.Auto;
    }
}
