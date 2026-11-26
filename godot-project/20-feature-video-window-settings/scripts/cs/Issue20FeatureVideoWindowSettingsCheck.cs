using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue20FeatureVideoWindowSettings;

/// <summary>
/// C# checks of 20-feature-video-window-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. Window mode, size and screen go through the confirmation, so the checks confirm or cancel them.
/// The restart checks start the project again as their own processes (desktop only).
/// </summary>
public partial class Issue20FeatureVideoWindowSettingsCheck : FeatureCheckCs
{
    private const string ProbeScene = "res://20-feature-video-window-settings/scenes/restart_probe.tscn";
    private const string WindowFile = "user://settings/window.ini";
    private const string ProbeWindowKey = "/root/RestartProbe/Inventory";

    protected override string CheckName => "20-feature-video-window-settings";

    protected override async Task RunChecks()
    {
        await CheckTypedSignals();

        var server = WindowSettingsServer.Singleton;
        Expect(Engine.HasSingleton("WindowSettingsServer"), "WindowSettingsServer is a registered singleton");
        Expect(server is WindowSettingsServerInstance, "WindowSettingsServer.Singleton is the instance type");
        Expect(server is SettingsServer, "WindowSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("window") == server, "the domain is registered as \"window\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.PreDisplay, "applied before the display server exists");
        ExpectEqual(server.GetFilePath(), WindowFile, "file path");
        var names = server.GetSettingNames();
        foreach (string setting in new[] { "window_mode", "window_size", "screen", "window_centered", "window_position", "borderless", "always_on_top", "unresizable", "transparent", "vsync_mode", "content_scale_mode", "content_scale_aspect", "content_scale_stretch", "content_scale_factor", "hdr_output_requested", "hdr_reference_luminance", "hdr_max_luminance", "window_states" })
        {
            Expect(System.Array.IndexOf(names, setting) >= 0, $"setting \"{setting}\" exists");
        }

        CheckDefaults();
        CheckConfirmation();
        CheckVsync();
        CheckContentScale();
        CheckWindowState();
        CheckResource();
        CheckRestart();

        // Leave no player files behind for later runs.
        Reset();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(WindowFile);
    }

    private static void Reset()
    {
        WindowSettingsServer.Singleton.ResetToDefaults();
        WindowSettingsServer.Singleton.ConfirmChanges();
    }

    private static bool IsHeadless() => DisplayServer.GetName() == "headless";

    private void CheckDefaults()
    {
        var server = WindowSettingsServer.Singleton;
        Reset();
        ExpectEqual(server.GetDefaultValue("window_mode").AsInt32(), ProjectSettings.GetSetting("display/window/size/mode").AsInt32(), "default mode is the project setting");
        ExpectEqual(server.GetDefaultValue("vsync_mode").AsInt32(), ProjectSettings.GetSetting("display/window/vsync/vsync_mode").AsInt32(), "default V-Sync mode is the project setting");
        ExpectEqual(server.GetDefaultValue("screen").AsInt32(), (int)DisplayServer.ScreenPrimary, "default screen is the primary screen");
        ExpectEqual(WindowSettingsServer.IsWindowCentered(), true, "the window is centered by default");
        ExpectEqual(WindowSettingsServer.GetHdrReferenceLuminance(), -1.0f, "HDR luminance: automatic by default");
        ExpectEqual(WindowSettingsServer.GetCommandLineOptions().Count, 0, "no window options on the command line of this run");
        Expect(server.IsValueSupported("content_scale_factor"), "content scale is supported everywhere");
        if (IsHeadless())
        {
            Expect(!server.IsValueSupported("window_size"), "headless: the window size is reported as unsupported");
        }
    }

    private void CheckConfirmation()
    {
        var server = WindowSettingsServer.Singleton;
        Expect(server.GetSettingFlags("window_mode").HasFlag(SettingsServer.SettingFlags.Confirm), "window_mode needs a confirmation");
        Expect(server.GetSettingFlags("window_size").HasFlag(SettingsServer.SettingFlags.Confirm), "window_size needs a confirmation");
        Expect(!server.GetSettingFlags("vsync_mode").HasFlag(SettingsServer.SettingFlags.Confirm), "vsync_mode is applied without a confirmation");

        Vector2I oldSize = WindowSettingsServer.GetWindowSize();
        WindowSettingsServer.SetWindowSize(new Vector2I(1024, 600));
        Expect(server.IsConfirmationPending(), "a new window size waits for the confirmation");
        server.CancelConfirmation();
        ExpectEqual(WindowSettingsServer.GetWindowSize(), oldSize, "CancelConfirmation() gives back the old size");

        WindowSettingsServer.SetWindowSize(new Vector2I(1024, 600));
        server.ConfirmChanges();
        ExpectEqual(server.GetAppliedValue("window_size").AsVector2I(), new Vector2I(1024, 600), "ConfirmChanges() keeps the new size");
        Reset();
    }

    private void CheckVsync()
    {
        var server = WindowSettingsServer.Singleton;
        WindowSettingsServer.SetVsyncMode(DisplayServer.VSyncMode.Disabled);
        ExpectEqual(WindowSettingsServer.GetVsyncMode(), DisplayServer.VSyncMode.Disabled, "GetVsyncMode()");
        ExpectEqual(server.GetAppliedValue("vsync_mode").AsInt32(), (int)DisplayServer.VSyncMode.Disabled, "V-Sync is applied at once");
        if (!IsHeadless() && server.IsValueSupported("vsync_mode"))
        {
            ExpectEqual(DisplayServer.WindowGetVsyncMode(), DisplayServer.VSyncMode.Disabled, "the display server has the V-Sync mode");
        }
        server.ReadFromEngine();
        ExpectEqual(WindowSettingsServer.GetVsyncMode(), DisplayServer.VSyncMode.Disabled, "ReadFromEngine() reads the same V-Sync mode back");
        Reset();
    }

    private void CheckContentScale()
    {
        Window root = GetTree().Root;
        WindowSettingsServer.SetContentScaleMode(Window.ContentScaleModeEnum.CanvasItems);
        WindowSettingsServer.SetContentScaleAspect(Window.ContentScaleAspectEnum.Expand);
        WindowSettingsServer.SetContentScaleFactor(1.5f);
        ExpectEqual(root.ContentScaleMode, Window.ContentScaleModeEnum.CanvasItems, "the root window gets the content scale mode");
        ExpectEqual(root.ContentScaleAspect, Window.ContentScaleAspectEnum.Expand, "the root window gets the content scale aspect");
        ExpectEqual(root.ContentScaleFactor, 1.5f, "the root window gets the content scale factor");
        Reset();
    }

    private void CheckWindowState()
    {
        var popup = new Window { Name = "Check20PopupCs", Visible = false };
        AddChild(popup);
        popup.Size = new Vector2I(320, 240);
        popup.Position = new Vector2I(50, 40);
        WindowSettingsServer.SaveWindowState(popup);
        string key = popup.GetPath();
        Expect(WindowSettingsServer.HasWindowState(key), "the state is stored under the node path");
        RemoveChild(popup);
        popup.Size = new Vector2I(100, 100);
        AddChild(popup);
        ExpectEqual(popup.Size, new Vector2I(320, 240), "the window gets its size back when it enters the tree");
        var other = new Window();
        Expect(WindowSettingsServer.RestoreWindowState(other, key), "RestoreWindowState() with a key, before AddChild()");
        ExpectEqual(other.Size, new Vector2I(320, 240), "restored size");
        WindowSettingsServer.ClearWindowState(key);
        Expect(!WindowSettingsServer.HasWindowState(key), "ClearWindowState()");
        other.Free();
        popup.QueueFree();
        Reset();
    }

    private void CheckResource()
    {
        var settings = new WindowSettings();
        ExpectEqual(settings.WindowMode, DisplayServer.WindowMode.Windowed, "WindowSettings: default mode");
        ExpectEqual(settings.WindowSize, new Vector2I(1152, 648), "WindowSettings: default size");
        ExpectEqual(settings.VsyncMode, DisplayServer.VSyncMode.Enabled, "WindowSettings: default V-Sync mode");
        settings.WindowMode = DisplayServer.WindowMode.Fullscreen;
        ExpectEqual(settings.WindowMode, DisplayServer.WindowMode.Fullscreen, "WindowSettings: WindowMode is a plain property");
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = WindowSettingsServer.Singleton;

        // 1) Fullscreen and 1920x1080 are saved and loaded before the main scene; a second window gets its state back.
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        WindowSettingsServer.SetWindowMode(DisplayServer.WindowMode.Fullscreen);
        WindowSettingsServer.SetWindowSize(new Vector2I(1920, 1080));
        WindowSettingsServer.SetScreen(5); // A screen that does not exist.
        ExpectEqual(server.Apply(), Error.Ok, "Apply() in manual mode");
        server.ConfirmChanges();
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        var inventory = new Window { Size = new Vector2I(400, 300), Position = new Vector2I(60, 70) };
        WindowSettingsServer.SaveWindowState(inventory, ProbeWindowKey);
        inventory.Free();
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");

        var probe = RunProbe([]);
        Expect(ProbeBool(probe, "started"), "restart: the domain was started");
        ExpectEqual(ProbeInt(probe, "window_mode"), (int)DisplayServer.WindowMode.Fullscreen, "restart: the saved mode is loaded");
        ExpectEqual(ProbeString(probe, "window_size"), new Vector2I(1920, 1080).ToString(), "restart: the saved size is loaded");
        ExpectEqual(ProbeInt(probe, "screen"), 5, "restart: the saved screen stays stored even if it is missing");
        ExpectEqual(ProbeString(probe, "inventory_size"), new Vector2I(400, 300).ToString(), "restart: the second window gets its size back");
        ExpectEqual(ProbeInt(probe, "ready_frame"), 0, "restart: loaded before the first frame");

        // 2) Command line options are session overrides: active, but never saved.
        probe = RunProbe(["--windowed", "--resolution", "800x600"]);
        Expect(ProbeBool(probe, "window_size_overridden"), "--resolution is a session override");
        Expect(ProbeBool(probe, "window_mode_overridden"), "--windowed is a session override");
        ExpectEqual(ProbeString(probe, "window_size"), new Vector2I(1920, 1080).ToString(), "--resolution does not change the stored size");
    }

    private static string ProbeString(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) ? probe[key].AsString() : "<missing>";

    private static int ProbeInt(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) ? probe[key].AsInt32() : -1;

    private static bool ProbeBool(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) && probe[key].AsBool();

    private Godot.Collections.Dictionary RunProbe(string[] extraArgs)
    {
        var output = new Godot.Collections.Array();
        var args = new System.Collections.Generic.List<string> { "--headless" };
        args.AddRange(extraArgs);
        args.AddRange(["--path", ProjectSettings.GlobalizePath("res://"), ProbeScene]);
        int code = OS.Execute(OS.GetExecutablePath(), args.ToArray(), output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        foreach (Variant chunk in output)
        {
            foreach (string line in chunk.AsString().Split('\n'))
            {
                if (line.StartsWith("CHECK20_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK20_PROBE ".Length..]);
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

    /// <summary>Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.</summary>
    private async Task CheckTypedSignals()
    {
        if (!Engine.HasSingleton("WindowSettingsServer"))
        {
            return;
        }
        var s = (SettingsServer)Engine.GetSingleton("WindowSettingsServer");
        Expect(s.HasSignal("vsync_mode_changed"), "WindowSettingsServer has the typed signal vsync_mode_changed");
        string filePath = s.GetFilePath();
        bool hadFile = FileAccess.FileExists(filePath);
        var previousMode = s.ApplyMode;
        s.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        s.ResetValue("vsync_mode");
        long newValue = s.GetAppliedValue("vsync_mode").AsInt64() != (long)DisplayServer.VSyncMode.Disabled ? (long)DisplayServer.VSyncMode.Disabled : (long)DisplayServer.VSyncMode.Enabled;
        var received = new System.Collections.Generic.List<Variant>();
        var onChanged = Callable.From((Variant v) => received.Add(v));
        s.Connect("vsync_mode_changed", onChanged);

        s.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        s.SetValue("vsync_mode", newValue);
        ExpectEqual(received.Count, 0, "vsync_mode_changed does not fire for a pending change in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 1, "vsync_mode_changed fires once on Apply()");
        if (received.Count > 0)
        {
            ExpectEqual(received[0].VariantType, Variant.Type.Int, "vsync_mode_changed passes a typed value");
            ExpectEqual(received[0].AsInt64(), newValue, "vsync_mode_changed passes the applied value");
        }
        s.Apply();
        ExpectEqual(received.Count, 1, "vsync_mode_changed does not fire when nothing changed");
        s.ResetValue("vsync_mode");
        ExpectEqual(received.Count, 1, "vsync_mode_changed waits for Apply() after a reset in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 2, "vsync_mode_changed fires again when the default is applied");

        s.Disconnect("vsync_mode_changed", onChanged);
        s.ApplyMode = previousMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!hadFile)
        {
            DirAccess.RemoveAbsolute(filePath);
        }
    }
}
