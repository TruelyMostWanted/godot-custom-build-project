using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.InputIcons;

/// <summary>
/// Full API test of Input Icons: InputIconServer and InputIconSet. Same tests as scripts/gd/Test.gd.
/// The icons are made at runtime (solid colors, CC0): the size tells them apart.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://input-icons-test-cs";
    private const string SavePath = "user://input-icons-test-cs/icons.tres";
    private const string ExampleIcons = "res://input-icons/resources/example_icons.tres";

    protected override Task RunTests()
    {
        TestClasses();
        TestSetLookup();
        TestTextAndInspector();
        TestSaveLoad();
        TestServer();
        TestDeviceFamily();
        TestProjectSettings();
        TestExampleResource();
        ResetServer();
        foreach (string fileName in DirAccess.GetFilesAt(Dir))
        {
            DirAccess.RemoveAbsolute(Dir.PathJoin(fileName));
        }
        DirAccess.RemoveAbsolute(Dir);
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

    private void TestClasses()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        Expect(ClassDB.ClassExists("InputIconSet"), "InputIconSet class exists");
        Expect(ClassDB.IsParentClass("InputIconSet", "Resource"), "InputIconSet is a Resource");
        Expect(Engine.HasSingleton("InputIconServer"), "InputIconServer singleton exists");
        Expect(ProjectSettings.HasSetting("input_devices/icons/xbox"), "project setting input_devices/icons/xbox exists");
        Expect((int)InputIconServer.InputIconFamily.Auto == -1, "INPUT_ICON_FAMILY_AUTO is -1");
        Expect((int)InputIconServer.InputIconFamily.Max == 5, "five icon families");
    }

    private void TestSetLookup()
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

    private void TestTextAndInspector()
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

    private void TestSaveLoad()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
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

    private void TestServer()
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

    private void TestDeviceFamily()
    {
        InputIconServer.InputIconFamily family = InputIconServer.GetDeviceFamily(0);
        Expect(family >= InputIconServer.InputIconFamily.GenericGamepad, "GetDeviceFamily gives a gamepad family");
        Expect(family < InputIconServer.InputIconFamily.Max, "GetDeviceFamily is below Max");
        ExpectEqual(InputIconServer.GetEventFamily(JoyButtonEvent(JoyButton.A)), InputIconServer.GetDeviceFamily(-1), "GetEventFamily of a gamepad event follows the device");
        InputIconServer.GamepadFamily = InputIconServer.InputIconFamily.Nintendo;
        ExpectEqual(InputIconServer.GetEventFamily(JoyButtonEvent(JoyButton.A)), InputIconServer.InputIconFamily.Nintendo, "GamepadFamily forces the family of a gamepad event");
        ExpectEqual(InputIconServer.GamepadFamily, InputIconServer.InputIconFamily.Nintendo, "GamepadFamily property");
        InputIconServer.GamepadFamily = InputIconServer.InputIconFamily.Auto;
    }

    private void TestProjectSettings()
    {
        foreach (string familyName in new[] { "keyboard_mouse", "generic_gamepad", "xbox", "playstation", "nintendo" })
        {
            Expect(ProjectSettings.HasSetting("input_devices/icons/" + familyName), $"project setting input_devices/icons/{familyName} exists");
        }
        ProjectSettings.SetSetting("input_devices/icons/xbox", ExampleIcons);
        InputIconSet xbox = InputIconServer.GetIconSet(InputIconServer.InputIconFamily.Xbox);
        Expect(xbox != null, "the set of the project setting is loaded");
        Expect(!InputIconServer.HasIconSetOverride(InputIconServer.InputIconFamily.Xbox), "a project set is no override");
        Expect(xbox.GetKeyIcon(Key.Space) != null, "the set of the project setting has the example icons");
        ProjectSettings.SetSetting("input_devices/icons/xbox", "");
    }

    private void TestExampleResource()
    {
        var icons = GD.Load<InputIconSet>(ExampleIcons);
        Expect(icons.GetKeyIcon(Key.Escape) != null, "the example set has an Escape icon");
        Expect(icons.GetMouseButtonIcon(MouseButton.Left) != null, "the example set has a left mouse button icon");
        Expect(icons.GetJoyButtonIcon(JoyButton.A) != null, "the example set has an A button icon");
        Expect(icons.GetJoyAxisIcon(JoyAxis.LeftY, -1.0f) != null, "the example set has a left stick up icon");
        Expect(icons.FallbackKeyIcon != null, "the example set has a fallback key icon");
        Expect(icons.FallbackMouseButtonIcon == null, "the example set has no fallback mouse icon");
        Expect(icons.FallbackJoyButtonIcon == null, "the example set has no fallback joy button icon");
        Expect(icons.FallbackJoyAxisIcon == null, "the example set has no fallback joy axis icon");
        Expect(icons.GetFallbackIcon(KeyEvent(Key.A)) == icons.FallbackKeyIcon, "GetFallbackIcon of a key");
        Expect(icons.GetFallbackIcon(JoyButtonEvent(JoyButton.A)) == null, "GetFallbackIcon without a fallback is null");
        Expect(icons.KeyIcons.ContainsKey("Space"), "KeyIcons is keyed by names");
        Expect(icons.MouseButtonIcons.ContainsKey("Left"), "MouseButtonIcons is keyed by names");
        Expect(icons.JoyButtonIcons.ContainsKey("A"), "JoyButtonIcons is keyed by names");
        Expect(icons.JoyAxisIcons.ContainsKey("LeftStickUp"), "JoyAxisIcons is keyed by names");
    }
}
