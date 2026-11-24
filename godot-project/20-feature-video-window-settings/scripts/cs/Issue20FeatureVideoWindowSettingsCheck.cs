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
    private const string VideoWindowFile = "user://settings/video_window.ini";
    private const string ProbeWindowKey = "/root/RestartProbe/Inventory";

    protected override string CheckName => "20-feature-video-window-settings";

    protected override async Task RunChecks()
    {
        var server = VideoWindowSettingsServer.Singleton;
        Expect(Engine.HasSingleton("VideoWindowSettingsServer"), "VideoWindowSettingsServer is a registered singleton");
        Expect(server is VideoWindowSettingsServerInstance, "VideoWindowSettingsServer.Singleton is the instance type");
        Expect(server is SettingsServer, "VideoWindowSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("video_window") == server, "the domain is registered as \"video_window\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.PreDisplay, "applied before the display server exists");
        ExpectEqual(server.GetFilePath(), VideoWindowFile, "file path");
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
        DirAccess.RemoveAbsolute(VideoWindowFile);
    }

    private static void Reset()
    {
        VideoWindowSettingsServer.Singleton.ResetToDefaults();
        VideoWindowSettingsServer.Singleton.ConfirmChanges();
    }

    private static bool IsHeadless() => DisplayServer.GetName() == "headless";

    private void CheckDefaults()
    {
        var server = VideoWindowSettingsServer.Singleton;
        Reset();
        ExpectEqual(server.GetDefaultValue("window_mode").AsInt32(), ProjectSettings.GetSetting("display/window/size/mode").AsInt32(), "default mode is the project setting");
        ExpectEqual(server.GetDefaultValue("vsync_mode").AsInt32(), ProjectSettings.GetSetting("display/window/vsync/vsync_mode").AsInt32(), "default V-Sync mode is the project setting");
        ExpectEqual(server.GetDefaultValue("screen").AsInt32(), (int)DisplayServer.ScreenPrimary, "default screen is the primary screen");
        ExpectEqual(VideoWindowSettingsServer.IsWindowCentered(), true, "the window is centered by default");
        ExpectEqual(VideoWindowSettingsServer.GetHdrReferenceLuminance(), -1.0f, "HDR luminance: automatic by default");
        ExpectEqual(VideoWindowSettingsServer.GetCommandLineOptions().Count, 0, "no window options on the command line of this run");
        Expect(server.IsValueSupported("content_scale_factor"), "content scale is supported everywhere");
        if (IsHeadless())
        {
            Expect(!server.IsValueSupported("window_size"), "headless: the window size is reported as unsupported");
        }
    }

    private void CheckConfirmation()
    {
        var server = VideoWindowSettingsServer.Singleton;
        Expect(server.GetSettingFlags("window_mode").HasFlag(SettingsServer.SettingFlags.Confirm), "window_mode needs a confirmation");
        Expect(server.GetSettingFlags("window_size").HasFlag(SettingsServer.SettingFlags.Confirm), "window_size needs a confirmation");
        Expect(!server.GetSettingFlags("vsync_mode").HasFlag(SettingsServer.SettingFlags.Confirm), "vsync_mode is applied without a confirmation");

        Vector2I oldSize = VideoWindowSettingsServer.GetWindowSize();
        VideoWindowSettingsServer.SetWindowSize(new Vector2I(1024, 600));
        Expect(server.IsConfirmationPending(), "a new window size waits for the confirmation");
        server.CancelConfirmation();
        ExpectEqual(VideoWindowSettingsServer.GetWindowSize(), oldSize, "CancelConfirmation() gives back the old size");

        VideoWindowSettingsServer.SetWindowSize(new Vector2I(1024, 600));
        server.ConfirmChanges();
        ExpectEqual(server.GetAppliedValue("window_size").AsVector2I(), new Vector2I(1024, 600), "ConfirmChanges() keeps the new size");
        Reset();
    }

    private void CheckVsync()
    {
        var server = VideoWindowSettingsServer.Singleton;
        VideoWindowSettingsServer.SetVsyncMode(DisplayServer.VSyncMode.Disabled);
        ExpectEqual(VideoWindowSettingsServer.GetVsyncMode(), DisplayServer.VSyncMode.Disabled, "GetVsyncMode()");
        ExpectEqual(server.GetAppliedValue("vsync_mode").AsInt32(), (int)DisplayServer.VSyncMode.Disabled, "V-Sync is applied at once");
        if (!IsHeadless() && server.IsValueSupported("vsync_mode"))
        {
            ExpectEqual(DisplayServer.WindowGetVsyncMode(), DisplayServer.VSyncMode.Disabled, "the display server has the V-Sync mode");
        }
        server.ReadFromEngine();
        ExpectEqual(VideoWindowSettingsServer.GetVsyncMode(), DisplayServer.VSyncMode.Disabled, "ReadFromEngine() reads the same V-Sync mode back");
        Reset();
    }

    private void CheckContentScale()
    {
        Window root = GetTree().Root;
        VideoWindowSettingsServer.SetContentScaleMode(Window.ContentScaleModeEnum.CanvasItems);
        VideoWindowSettingsServer.SetContentScaleAspect(Window.ContentScaleAspectEnum.Expand);
        VideoWindowSettingsServer.SetContentScaleFactor(1.5f);
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
        VideoWindowSettingsServer.SaveWindowState(popup);
        string key = popup.GetPath();
        Expect(VideoWindowSettingsServer.HasWindowState(key), "the state is stored under the node path");
        RemoveChild(popup);
        popup.Size = new Vector2I(100, 100);
        AddChild(popup);
        ExpectEqual(popup.Size, new Vector2I(320, 240), "the window gets its size back when it enters the tree");
        var other = new Window();
        Expect(VideoWindowSettingsServer.RestoreWindowState(other, key), "RestoreWindowState() with a key, before AddChild()");
        ExpectEqual(other.Size, new Vector2I(320, 240), "restored size");
        VideoWindowSettingsServer.ClearWindowState(key);
        Expect(!VideoWindowSettingsServer.HasWindowState(key), "ClearWindowState()");
        other.Free();
        popup.QueueFree();
        Reset();
    }

    private void CheckResource()
    {
        var settings = new VideoWindowSettings();
        ExpectEqual(settings.WindowMode, DisplayServer.WindowMode.Windowed, "VideoWindowSettings: default mode");
        ExpectEqual(settings.WindowSize, new Vector2I(1152, 648), "VideoWindowSettings: default size");
        ExpectEqual(settings.VsyncMode, DisplayServer.VSyncMode.Enabled, "VideoWindowSettings: default V-Sync mode");
        settings.WindowMode = DisplayServer.WindowMode.Fullscreen;
        ExpectEqual(settings.WindowMode, DisplayServer.WindowMode.Fullscreen, "VideoWindowSettings: WindowMode is a plain property");
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = VideoWindowSettingsServer.Singleton;

        // 1) Fullscreen and 1920x1080 are saved and loaded before the main scene; a second window gets its state back.
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        VideoWindowSettingsServer.SetWindowMode(DisplayServer.WindowMode.Fullscreen);
        VideoWindowSettingsServer.SetWindowSize(new Vector2I(1920, 1080));
        VideoWindowSettingsServer.SetScreen(5); // A screen that does not exist.
        ExpectEqual(server.Apply(), Error.Ok, "Apply() in manual mode");
        server.ConfirmChanges();
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        var inventory = new Window { Size = new Vector2I(400, 300), Position = new Vector2I(60, 70) };
        VideoWindowSettingsServer.SaveWindowState(inventory, ProbeWindowKey);
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
}
