using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.WindowSettings;

using WindowSettings = Godot.WindowSettings;

/// <summary>Full API test of Window Settings (WindowSettingsServer, WindowSettings). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string ProbeScene = "res://window-settings/scenes/restart_probe.tscn";
    private const string WindowFile = "user://settings/window.ini";
    private const string InventoryKey = "/root/RestartProbe/Inventory";
    private static readonly Vector2I PopupSize = new(320, 240);
    private static readonly Vector2I PopupPosition = new(50, 40);
    private static readonly Vector2I SavedSize = new(1920, 1080);
    private const int MissingScreen = 5;

    private readonly Godot.Collections.Dictionary _received = [];

    protected override Task RunTests()
    {
        WindowSettingsServer.Singleton.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        TestDefaults();
        TestValues();
        TestValidation();
        TestSignals();
        TestContentScale();
        TestWindowState();
        TestCommandLineOptions();
        TestResource();
        TestRestart();
        Reset();
        WindowSettingsServer.Singleton.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        DirAccess.RemoveAbsolute(WindowFile);
        return Task.CompletedTask;
    }

    private static void Reset()
    {
        WindowSettingsServer.Singleton.ResetToDefaults();
        WindowSettingsServer.Singleton.Apply();
        WindowSettingsServer.Singleton.ConfirmChanges();
    }

    private void TestDefaults()
    {
        Reset();
        int projectMode = (int)ProjectSettings.GetSetting("display/window/size/mode");
        int projectWidth = (int)ProjectSettings.GetSetting("display/window/size/viewport_width");
        int projectHeight = (int)ProjectSettings.GetSetting("display/window/size/viewport_height");
        int projectVsync = (int)ProjectSettings.GetSetting("display/window/vsync/vsync_mode");
        ExpectEqual((int)WindowSettingsServer.GetWindowMode(), projectMode, "the default mode is the project setting");
        ExpectEqual(WindowSettingsServer.GetWindowSize(), new Vector2I(projectWidth, projectHeight), "the default size is the project viewport size");
        ExpectEqual((int)WindowSettingsServer.GetVsyncMode(), projectVsync, "the default V-Sync mode is the project setting");
        ExpectEqual(WindowSettingsServer.GetScreen(), (int)DisplayServer.ScreenPrimary, "the default screen is the primary screen");
        Expect(WindowSettingsServer.IsWindowCentered(), "the window is centered by default");
        ExpectEqual(WindowSettingsServer.GetHdrReferenceLuminance(), -1.0f, "the HDR reference luminance is automatic by default");
        ExpectEqual(WindowSettingsServer.GetHdrMaxLuminance(), -1.0f, "the HDR maximum luminance is automatic by default");
    }

    private void TestValues()
    {
        WindowSettingsServer.SetWindowMode(DisplayServer.WindowMode.Maximized);
        ExpectEqual(WindowSettingsServer.GetWindowMode(), DisplayServer.WindowMode.Maximized, "window_mode round trip");
        WindowSettingsServer.SetWindowSize(new Vector2I(1024, 600));
        ExpectEqual(WindowSettingsServer.GetWindowSize(), new Vector2I(1024, 600), "window_size round trip");
        WindowSettingsServer.SetScreen(1);
        ExpectEqual(WindowSettingsServer.GetScreen(), 1, "screen round trip");
        WindowSettingsServer.SetWindowCentered(false);
        Expect(!WindowSettingsServer.IsWindowCentered(), "window_centered round trip");
        WindowSettingsServer.SetWindowPosition(new Vector2I(30, 20));
        ExpectEqual(WindowSettingsServer.GetWindowPosition(), new Vector2I(30, 20), "window_position round trip");
        WindowSettingsServer.SetBorderless(true);
        Expect(WindowSettingsServer.IsBorderless(), "borderless round trip");
        WindowSettingsServer.SetAlwaysOnTop(true);
        Expect(WindowSettingsServer.IsAlwaysOnTop(), "always_on_top round trip");
        WindowSettingsServer.SetUnresizable(true);
        Expect(WindowSettingsServer.IsUnresizable(), "unresizable round trip");
        WindowSettingsServer.SetTransparent(true);
        Expect(WindowSettingsServer.IsTransparent(), "transparent round trip");
        WindowSettingsServer.SetVsyncMode(DisplayServer.VSyncMode.Disabled);
        ExpectEqual(WindowSettingsServer.GetVsyncMode(), DisplayServer.VSyncMode.Disabled, "vsync_mode round trip");
        WindowSettingsServer.SetHdrOutputRequested(true);
        Expect(WindowSettingsServer.IsHdrOutputRequested(), "hdr_output_requested round trip");
        WindowSettingsServer.SetHdrReferenceLuminance(200.0f);
        ExpectEqual(WindowSettingsServer.GetHdrReferenceLuminance(), 200.0f, "hdr_reference_luminance round trip");
        WindowSettingsServer.SetHdrMaxLuminance(1000.0f);
        ExpectEqual(WindowSettingsServer.GetHdrMaxLuminance(), 1000.0f, "hdr_max_luminance round trip");
        WindowSettingsServer.Singleton.Revert();
    }

    private void TestValidation()
    {
        WindowSettingsServer.SetWindowSize(new Vector2I(0, -10));
        ExpectEqual(WindowSettingsServer.GetWindowSize(), new Vector2I(1, 1), "the window size is at least 1x1");
        WindowSettingsServer.SetHdrMaxLuminance(-50.0f);
        ExpectEqual(WindowSettingsServer.GetHdrMaxLuminance(), -1.0f, "a negative luminance means automatic");
        WindowSettingsServer.SetContentScaleFactor(0.0f);
        float factor = WindowSettingsServer.GetContentScaleFactor();
        Expect(Mathf.IsEqualApprox(factor, 0.1f), "the content scale factor is at least 0.1");
        WindowSettingsServer.Singleton.Revert();
    }

    private void TestSignals()
    {
        WindowSettingsServerInstance server = WindowSettingsServer.Singleton;
        server.WindowModeChanged += mode => _received["window_mode"] = (int)mode;
        server.WindowSizeChanged += size => _received["window_size"] = size;
        server.ScreenChanged += screen => _received["screen"] = screen;
        server.BorderlessChanged += enabled => _received["borderless"] = enabled;
        server.VsyncModeChanged += mode => _received["vsync_mode"] = (int)mode;
        server.HdrOutputRequestedChanged += requested => _received["hdr_output_requested"] = requested;
        server.ContentScaleModeChanged += mode => _received["content_scale_mode"] = (int)mode;
        server.ContentScaleAspectChanged += aspect => _received["content_scale_aspect"] = (int)aspect;
        server.ContentScaleFactorChanged += factor => _received["content_scale_factor"] = factor;
        WindowSettingsServer.SetWindowMode(DisplayServer.WindowMode.Fullscreen);
        WindowSettingsServer.SetWindowSize(SavedSize);
        WindowSettingsServer.SetScreen(0);
        WindowSettingsServer.SetBorderless(true);
        WindowSettingsServer.SetVsyncMode(DisplayServer.VSyncMode.Adaptive);
        WindowSettingsServer.SetHdrOutputRequested(true);
        WindowSettingsServer.SetContentScaleMode(Window.ContentScaleModeEnum.Viewport);
        WindowSettingsServer.SetContentScaleAspect(Window.ContentScaleAspectEnum.Expand);
        WindowSettingsServer.SetContentScaleFactor(2.0f);
        Expect(_received.Count == 0, "no typed signal fires for pending changes in manual mode");
        server.Apply();
        server.ConfirmChanges();
        ExpectEqual((int)_received["window_mode"], (int)DisplayServer.WindowMode.Fullscreen, "WindowModeChanged passes the mode");
        ExpectEqual((Vector2I)_received["window_size"], SavedSize, "WindowSizeChanged passes the size");
        ExpectEqual((int)_received["screen"], 0, "ScreenChanged passes the screen");
        Expect((bool)_received["borderless"], "BorderlessChanged passes the flag");
        ExpectEqual((int)_received["vsync_mode"], (int)DisplayServer.VSyncMode.Adaptive, "VsyncModeChanged passes the mode");
        Expect((bool)_received["hdr_output_requested"], "HdrOutputRequestedChanged passes the flag");
        ExpectEqual((int)_received["content_scale_mode"], (int)Window.ContentScaleModeEnum.Viewport, "ContentScaleModeChanged passes the mode");
        ExpectEqual((int)_received["content_scale_aspect"], (int)Window.ContentScaleAspectEnum.Expand, "ContentScaleAspectChanged passes the aspect");
        ExpectEqual((float)_received["content_scale_factor"], 2.0f, "ContentScaleFactorChanged passes the factor");
        Reset();
    }

    private void TestContentScale()
    {
        Window root = GetTree().Root;
        WindowSettingsServer.SetContentScaleMode(Window.ContentScaleModeEnum.CanvasItems);
        WindowSettingsServer.SetContentScaleAspect(Window.ContentScaleAspectEnum.Expand);
        WindowSettingsServer.SetContentScaleStretch(Window.ContentScaleStretchEnum.Integer);
        WindowSettingsServer.SetContentScaleFactor(2.0f);
        ExpectEqual(WindowSettingsServer.GetContentScaleStretch(), Window.ContentScaleStretchEnum.Integer, "content_scale_stretch round trip");
        WindowSettingsServer.Singleton.Apply();
        ExpectEqual(root.ContentScaleMode, Window.ContentScaleModeEnum.CanvasItems, "the root window gets the content scale mode");
        ExpectEqual(root.ContentScaleAspect, Window.ContentScaleAspectEnum.Expand, "the root window gets the content scale aspect");
        ExpectEqual(root.ContentScaleStretch, Window.ContentScaleStretchEnum.Integer, "the root window gets the content scale stretch");
        ExpectEqual(root.ContentScaleFactor, 2.0f, "the root window gets the content scale factor");
        Reset();
        ExpectEqual(root.ContentScaleFactor, 1.0f, "ResetToDefaults gives the project factor back");
    }

    private void TestWindowState()
    {
        var popup = new Window();
        popup.Name = "Popup";
        popup.Visible = false;
        AddChild(popup);
        popup.Size = PopupSize;
        popup.Position = PopupPosition;
        WindowSettingsServer.SaveWindowState(popup);
        string key = popup.GetPath();
        Expect(WindowSettingsServer.HasWindowState(key), "SaveWindowState stores the state under the node path");
        RemoveChild(popup);
        popup.Size = new Vector2I(100, 100);
        AddChild(popup);
        ExpectEqual(popup.Size, PopupSize, "the window gets its size back when it enters the tree");
        var other = new Window();
        bool restored = WindowSettingsServer.RestoreWindowState(other, key);
        Expect(restored, "RestoreWindowState with a key, before AddChild");
        ExpectEqual(other.Size, PopupSize, "RestoreWindowState restores the size");
        bool missing = WindowSettingsServer.RestoreWindowState(other, "missing");
        Expect(!missing, "RestoreWindowState returns false for an unknown key");
        WindowSettingsServer.ClearWindowState(key);
        Expect(!WindowSettingsServer.HasWindowState(key), "ClearWindowState removes the state");
        other.Free();
        popup.QueueFree();
    }

    private void TestCommandLineOptions()
    {
        Godot.Collections.Dictionary options = WindowSettingsServer.GetCommandLineOptions();
        Expect(options.Count == 0, "no window options on the command line of this run");
        // Does nothing without free windows; it must not fail.
        WindowSettingsServer.PlaceMainWindow();
    }

    private void TestResource()
    {
        var settings = new WindowSettings();
        ExpectEqual(settings.WindowMode, DisplayServer.WindowMode.Windowed, "WindowSettings: default WindowMode");
        ExpectEqual(settings.WindowSize, new Vector2I(1152, 648), "WindowSettings: default WindowSize");
        ExpectEqual(settings.Screen, (int)DisplayServer.ScreenPrimary, "WindowSettings: default Screen");
        Expect(settings.WindowCentered, "WindowSettings: WindowCentered by default");
        ExpectEqual(settings.VsyncMode, DisplayServer.VSyncMode.Enabled, "WindowSettings: default VsyncMode");
        ExpectEqual(settings.ContentScaleFactor, 1.0f, "WindowSettings: default ContentScaleFactor");
        Expect(settings.WindowStates.Count == 0, "WindowSettings: WindowStates is empty");
        settings.WindowMode = DisplayServer.WindowMode.Fullscreen;
        ExpectEqual(settings.WindowMode, DisplayServer.WindowMode.Fullscreen, "WindowSettings: WindowMode is a plain property");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        WindowSettingsServer.SetWindowMode(DisplayServer.WindowMode.Fullscreen);
        WindowSettingsServer.SetWindowSize(SavedSize);
        WindowSettingsServer.SetScreen(MissingScreen);
        WindowSettingsServer.SetBorderless(true);
        var inventory = new Window();
        inventory.Size = new Vector2I(400, 300);
        inventory.Position = new Vector2I(60, 70);
        WindowSettingsServer.SaveWindowState(inventory, InventoryKey);
        inventory.Free();
        WindowSettingsServer.Singleton.Apply();
        WindowSettingsServer.Singleton.ConfirmChanges();
        WindowSettingsServer.Singleton.Save();
        Godot.Collections.Dictionary probe = RunProbe([]);
        ExpectEqual((int)probe["ready_frame"], 0, "restart: the settings are loaded before the first frame");
        ExpectEqual((int)probe["window_mode"], (int)DisplayServer.WindowMode.Fullscreen, "restart: the saved mode is loaded");
        ExpectEqual((string)probe["window_size"], SavedSize.ToString(), "restart: the saved size is loaded");
        ExpectEqual((int)probe["screen"], MissingScreen, "restart: a saved screen that is missing stays stored");
        Expect((bool)probe["borderless"], "restart: the saved flag is loaded");
        ExpectEqual((string)probe["inventory_size"], new Vector2I(400, 300).ToString(), "restart: the second window gets its size back");
        ExpectEqual((string)probe["inventory_position"], new Vector2I(60, 70).ToString(), "restart: the second window gets its position back");
        probe = RunProbe(["--windowed", "--resolution", "800x600"]);
        var options = (Godot.Collections.Dictionary)probe["command_line_options"];
        ExpectEqual((int)options["window_mode"], (int)DisplayServer.WindowMode.Windowed, "restart: --windowed is read");
        Expect((bool)probe["window_size_overridden"], "restart: --resolution is a session override");
        ExpectEqual((string)probe["window_size"], SavedSize.ToString(), "restart: --resolution does not change the stored size");
    }

    private Godot.Collections.Dictionary RunProbe(string[] extraArgs)
    {
        var args = new System.Collections.Generic.List<string> { "--headless" };
        args.AddRange(extraArgs);
        args.AddRange(["--path", ProjectSettings.GlobalizePath("res://"), ProbeScene]);
        var output = new Godot.Collections.Array();
        int code = OS.Execute(OS.GetExecutablePath(), args.ToArray(), output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        foreach (string line in output[0].AsString().Split('\n'))
        {
            if (line.StartsWith("PROBE "))
            {
                return Json.ParseString(line["PROBE ".Length..]).AsGodotDictionary();
            }
        }
        Expect(false, "restart: the probe printed its result");
        return [];
    }
}
