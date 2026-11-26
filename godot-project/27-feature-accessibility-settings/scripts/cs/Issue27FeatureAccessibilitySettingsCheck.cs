using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue27FeatureAccessibilitySettings;

/// <summary>
/// C# checks of 27-feature-accessibility-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The restart check starts the project again as its own process (desktop only).
/// </summary>
public partial class Issue27FeatureAccessibilitySettingsCheck : FeatureCheckCs
{
    private const string ProbeScene = "res://27-feature-accessibility-settings/scenes/restart_probe.tscn";
    private const string AccessibilityFile = "user://settings/accessibility.ini";

    protected override string CheckName => "27-feature-accessibility-settings";

    protected override async Task RunChecks()
    {
        await CheckTypedSignals();

        var server = AccessibilitySettingsServer.Singleton;
        Expect(Engine.HasSingleton("AccessibilitySettingsServer"), "AccessibilitySettingsServer is a registered singleton");
        Expect(server is AccessibilitySettingsServerInstance, "AccessibilitySettingsServer.Singleton is the instance type");
        Expect(server is SettingsServer, "AccessibilitySettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("accessibility") == server, "the domain is registered as \"accessibility\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the root window exists, before the main scene");
        ExpectEqual(server.GetFilePath(), AccessibilityFile, "file path");
        foreach (string setting in new[] { "ui_scale", "font_size_factor", "color_scheme", "custom_color_scheme_path", "focus_outline_width", "speak_focused_controls", "speech_persona" })
        {
            Expect(server.IsSettingBound(setting), $"\"{setting}\" has an engine target");
        }
        foreach (string setting in new[] { "reduce_motion", "reduce_transparency", "colorblind_mode", "subtitles_enabled", "subtitle_scale" })
        {
            Expect(server.GetSettingFlags(setting).HasFlag(SettingsServer.SettingFlags.NoEngineTarget), $"\"{setting}\" is game-side");
        }

        server.ResetToDefaults();
        CheckDefaults();
        CheckValidation();
        CheckUiScale();
        await CheckTheme();
        CheckHelpers();
        CheckSpeech();
        CheckResources();
        CheckRestart();

        // Leave no player files behind for later runs.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(AccessibilityFile);
    }

    private void CheckDefaults()
    {
        var server = AccessibilitySettingsServer.Singleton;
        ExpectEqual(AccessibilitySettingsServer.GetUiScale(), 1.0f, "default UI scale");
        ExpectEqual(AccessibilitySettingsServer.GetFontSizeFactor(), 1.0f, "default font size factor");
        ExpectEqual(AccessibilitySettingsServer.GetFocusOutlineWidth(), 0, "default focus outline: the theme's own");
        Expect(!AccessibilitySettingsServer.IsSpeakingFocusedControls(), "speaking focused controls is off by default");
        ExpectEqual(AccessibilitySettingsServer.GetColorblindMode(), AccessibilitySettingsServer.ColorblindMode.None, "default colorblind mode");
        int reduceAnimation = DisplayServer.AccessibilityShouldReduceAnimation();
        ExpectEqual(server.GetDefaultValue("reduce_motion").AsBool(), reduceAnimation == 1, $"default reduced motion follows the OS ({reduceAnimation})");
        int increaseContrast = DisplayServer.AccessibilityShouldIncreaseContrast();
        var expectedScheme = increaseContrast == 1 ? AccessibilitySettingsServer.ColorScheme.HighContrastDark : AccessibilitySettingsServer.ColorScheme.Default;
        ExpectEqual(server.GetDefaultValue("color_scheme").AsInt32(), (int)expectedScheme, $"default color scheme follows the OS contrast ({increaseContrast})");
    }

    private void CheckValidation()
    {
        var server = AccessibilitySettingsServer.Singleton;
        AccessibilitySettingsServer.SetUiScale(10.0f);
        ExpectEqual(AccessibilitySettingsServer.GetUiScale(), 4.0f, "UI scale is clamped to 4");
        AccessibilitySettingsServer.SetFontSizeFactor(0.1f);
        ExpectEqual(AccessibilitySettingsServer.GetFontSizeFactor(), 0.5f, "font size factor is clamped to 0.5");
        AccessibilitySettingsServer.SetFocusOutlineWidth(20);
        ExpectEqual(AccessibilitySettingsServer.GetFocusOutlineWidth(), 8, "focus outline is clamped to 8");
        server.SetValue("color_scheme", 42);
        ExpectEqual((int)AccessibilitySettingsServer.GetColorScheme(), server.GetDefaultValue("color_scheme").AsInt32(), "an unknown color scheme keeps the current one");
        server.ResetToDefaults();
    }

    private void CheckUiScale()
    {
        Window root = GetTree().Root;
        float baseFactor = root.ContentScaleFactor;
        AccessibilitySettingsServer.SetUiScale(1.5f);
        Expect(Mathf.IsEqualApprox(root.ContentScaleFactor, baseFactor * 1.5f), $"UI scale 150 % multiplies the root content scale factor ({root.ContentScaleFactor})");
        Expect(Mathf.IsEqualApprox(WindowSettingsServer.GetContentScaleFactor(), baseFactor), "the window factor stays separate");
        AccessibilitySettingsServer.SetUiScale(1.0f);
        Expect(Mathf.IsEqualApprox(root.ContentScaleFactor, baseFactor), "UI scale 100 % gives the factor back");
    }

    private async Task CheckTheme()
    {
        var server = AccessibilitySettingsServer.Singleton;
        Window root = GetTree().Root;
        Theme previousTheme = root.Theme;
        Expect(AccessibilitySettingsServer.GetOverlayTheme() == null, "no overlay theme with the defaults");

        int changes = 0;
        var callable = Callable.From<AccessibilityColorScheme>(_ => changes++);
        server.Connect("color_scheme_changed", callable);
        AccessibilitySettingsServer.SetColorScheme(AccessibilitySettingsServer.ColorScheme.HighContrastDark);
        Theme overlay = AccessibilitySettingsServer.GetOverlayTheme();
        Expect(overlay != null && root.Theme == overlay, "a high-contrast scheme sets the overlay theme on the root window");
        ExpectEqual(changes, 1, "color_scheme_changed is emitted once");
        AccessibilityColorScheme scheme = AccessibilitySettingsServer.GetActiveColorScheme();
        Expect(scheme != null, "the active scheme is an AccessibilityColorScheme");
        if (overlay != null && scheme != null)
        {
            ExpectEqual(overlay.GetColor("font_color", "Button"), Colors.White, "button text is white");
            var focus = overlay.GetStylebox("focus", "Button") as StyleBoxFlat;
            Expect(focus != null && focus.BorderColor == scheme.FocusColor, "the focus outline has the scheme's focus color");
            ExpectEqual(AccessibilitySettingsServer.GetSchemeColor("problem", Colors.Magenta), scheme.ProblemColor, "GetSchemeColor() returns the role color");
        }
        var button = new Button { Text = "Play" };
        AddChild(button);
        // Controls see a changed root theme from the next frame (before the first draw).
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(button.GetThemeColor("font_color"), Colors.White, "a button in the tree gets the scheme's text color");

        int fontSizeBefore = button.GetThemeFontSize("font_size");
        AccessibilitySettingsServer.SetFontSizeFactor(1.5f);
        Expect(AccessibilitySettingsServer.GetOverlayTheme().HasDefaultFontSize(), "the font size factor sets the default font size of the overlay");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(button.GetThemeFontSize("font_size"), Mathf.RoundToInt(fontSizeBefore * 1.5f), "a button in the tree gets the bigger font");
        AccessibilitySettingsServer.SetFocusOutlineWidth(4);
        var focusBox = AccessibilitySettingsServer.GetOverlayTheme().GetStylebox("focus", "Button") as StyleBoxFlat;
        Expect(focusBox != null && focusBox.BorderWidthLeft == 4, "the focus outline width reaches the focus style box");
        button.QueueFree();

        server.ResetToDefaults();
        Expect(AccessibilitySettingsServer.GetOverlayTheme() == null, "back at the defaults: no overlay theme");
        Expect(root.Theme == previousTheme, "the root window gets its previous theme back");
        ExpectEqual(changes, 2, "color_scheme_changed is emitted when the scheme goes away");
        server.Disconnect("color_scheme_changed", callable);
    }

    private void CheckHelpers()
    {
        var server = AccessibilitySettingsServer.Singleton;
        AccessibilitySettingsServer.SetReduceMotion(true);
        ExpectEqual(AccessibilitySettingsServer.GetAnimationDuration(0.3), 0.0, "reduced motion: no animation duration");
        ExpectEqual(AccessibilitySettingsServer.GetMotionScale(), 0.0f, "reduced motion: motion scale 0");
        AccessibilitySettingsServer.SetReduceMotion(false);
        ExpectEqual(AccessibilitySettingsServer.GetAnimationDuration(0.3), 0.3, "no reduced motion: the duration stays");
        AccessibilitySettingsServer.SetFontSizeFactor(2.0f);
        ExpectEqual(AccessibilitySettingsServer.ScaleFontSize(16), 32, "ScaleFontSize()");
        AccessibilitySettingsServer.SetSubtitleScale(1.5f);
        ExpectEqual(AccessibilitySettingsServer.ScaleSubtitleFontSize(10), 30, "ScaleSubtitleFontSize()");
        AccessibilitySettingsServer.SetColorblindMode(AccessibilitySettingsServer.ColorblindMode.Deuteranopia);
        ExpectEqual(AccessibilitySettingsServer.GetColorblindMode(), AccessibilitySettingsServer.ColorblindMode.Deuteranopia, "colorblind mode is stored");
        server.ResetToDefaults();
    }

    private void CheckSpeech()
    {
        var server = AccessibilitySettingsServer.Singleton;
        var button = new Button { Text = "Play" };
        ExpectEqual(AccessibilitySettingsServer.GetControlSpeechText(button), "Play", "the text of a button is spoken");
        button.AccessibilityName = "Start the game";
        ExpectEqual(AccessibilitySettingsServer.GetControlSpeechText(button), "Start the game", "AccessibilityName wins over the text");
        var field = new LineEdit { PlaceholderText = "Player name" };
        ExpectEqual(AccessibilitySettingsServer.GetControlSpeechText(field), "Player name", "an empty field speaks its placeholder");
        field.Free();

        Window root = GetTree().Root;
        int before = root.GetSignalConnectionList("gui_focus_changed").Count;
        AccessibilitySettingsServer.SetSpeakFocusedControls(true);
        ExpectEqual(root.GetSignalConnectionList("gui_focus_changed").Count, before + 1, "speaking focused controls connects to the root window");
        if (TtsServer.IsAvailable() && !AccessibilitySettingsServer.IsScreenReaderActive())
        {
            TtsServer.AddPersona(new TtsPersona { PersonaName = "check_narrator_cs" });
            AccessibilitySettingsServer.SetSpeechPersona("check_narrator_cs");
            AddChild(button);
            button.GrabFocus();
            Expect(TtsServer.GetPendingUtterances().Length > 0 || TtsServer.IsSpeaking(), "moving the focus speaks the control");
            TtsServer.Stop();
            TtsServer.RemovePersona("check_narrator_cs");
            button.QueueFree();
        }
        else
        {
            GD.Print("  SKIP  spoken focus (no TTS voice or a screen reader is active)");
            ExpectEqual(AccessibilitySettingsServer.SpeakControl(button), -1L, "without a voice nothing is spoken");
            button.Free();
        }
        AccessibilitySettingsServer.SetSpeakFocusedControls(false);
        ExpectEqual(root.GetSignalConnectionList("gui_focus_changed").Count, before, "turning it off disconnects");
        server.ResetToDefaults();
    }

    private void CheckResources()
    {
        var server = AccessibilitySettingsServer.Singleton;
        AccessibilityColorScheme dark = AccessibilityColorScheme.CreateHighContrastDark();
        AccessibilityColorScheme light = AccessibilityColorScheme.CreateHighContrastLight();
        ExpectEqual(dark.TextColor, Colors.White, "dark scheme: white text");
        ExpectEqual(light.SurfaceColor, Colors.White, "light scheme: white surfaces");
        ExpectEqual(dark.GetRoleColor("focus"), dark.FocusColor, "GetRoleColor()");
        Expect(server.GetSettings() is AccessibilitySettings, "the settings resource is an AccessibilitySettings");

        // A custom scheme from a file.
        var custom = new AccessibilityColorScheme { TextColor = new Color(0.9f, 0.9f, 0.2f) };
        const string path = "user://check_27_scheme_cs.tres";
        ExpectEqual(ResourceSaver.Save(custom, path), Error.Ok, "a custom scheme can be saved");
        AccessibilitySettingsServer.SetCustomColorSchemePath(path);
        AccessibilitySettingsServer.SetColorScheme(AccessibilitySettingsServer.ColorScheme.Custom);
        Theme overlay = AccessibilitySettingsServer.GetOverlayTheme();
        Expect(overlay != null && overlay.GetColor("font_color", "Label").IsEqualApprox(new Color(0.9f, 0.9f, 0.2f)), "the custom scheme reaches the overlay theme");
        server.ResetToDefaults();
        DirAccess.RemoveAbsolute(path);
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = AccessibilitySettingsServer.Singleton;
        AccessibilitySettingsServer.SetUiScale(1.5f);
        AccessibilitySettingsServer.SetColorScheme(AccessibilitySettingsServer.ColorScheme.HighContrastDark);
        AccessibilitySettingsServer.SetReduceMotion(true);
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        Expect(FileAccess.GetFileAsString(AccessibilityFile).Contains("ui_scale"), "the player file is plain INI");

        var probe = RunProbe();
        Expect(probe.ContainsKey("started") && probe["started"].AsBool(), "restart: the domain was started");
        Expect(probe.ContainsKey("ready_frame") && probe["ready_frame"].AsInt32() == 0, "restart: applied before the first frame");
        Expect(probe.ContainsKey("ui_scale") && Mathf.IsEqualApprox(probe["ui_scale"].AsSingle(), 1.5f), "restart: the saved UI scale is loaded");
        Expect(probe.ContainsKey("root_factor") && probe.ContainsKey("video_factor") && Mathf.IsEqualApprox(probe["root_factor"].AsSingle(), probe["video_factor"].AsSingle() * 1.5f), "restart: the root window is scaled before the main scene is ready");
        Expect(probe.ContainsKey("color_scheme") && probe["color_scheme"].AsInt32() == (int)AccessibilitySettingsServer.ColorScheme.HighContrastDark, "restart: the saved scheme is loaded");
        Expect(probe.ContainsKey("overlay_on_root") && probe["overlay_on_root"].AsBool(), "restart: the overlay theme is on the root window before the main scene is ready");
        Expect(probe.ContainsKey("button_font_white") && probe["button_font_white"].AsBool(), "restart: a button of the main scene has the scheme's text color");
        Expect(probe.ContainsKey("reduce_motion") && probe["reduce_motion"].AsBool(), "restart: reduced motion is loaded");
        server.ResetToDefaults();
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
                if (line.StartsWith("CHECK27_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK27_PROBE ".Length..]);
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
        if (!Engine.HasSingleton("AccessibilitySettingsServer"))
        {
            return;
        }
        var s = (SettingsServer)Engine.GetSingleton("AccessibilitySettingsServer");
        Expect(s.HasSignal("ui_scale_changed"), "AccessibilitySettingsServer has the typed signal ui_scale_changed");
        string filePath = s.GetFilePath();
        bool hadFile = FileAccess.FileExists(filePath);
        var previousMode = s.ApplyMode;
        s.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        s.ResetValue("ui_scale");
        double newValue = s.GetAppliedValue("ui_scale").AsDouble() != 1.5 ? 1.5 : 2.0;
        var received = new System.Collections.Generic.List<Variant>();
        var onChanged = Callable.From((Variant v) => received.Add(v));
        s.Connect("ui_scale_changed", onChanged);

        s.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        s.SetValue("ui_scale", newValue);
        ExpectEqual(received.Count, 0, "ui_scale_changed does not fire for a pending change in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 1, "ui_scale_changed fires once on Apply()");
        if (received.Count > 0)
        {
            ExpectEqual(received[0].VariantType, Variant.Type.Float, "ui_scale_changed passes a typed value");
            ExpectEqual((float)received[0].AsDouble(), (float)newValue, "ui_scale_changed passes the applied value");
        }
        s.Apply();
        ExpectEqual(received.Count, 1, "ui_scale_changed does not fire when nothing changed");
        s.ResetValue("ui_scale");
        ExpectEqual(received.Count, 1, "ui_scale_changed waits for Apply() after a reset in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 2, "ui_scale_changed fires again when the default is applied");

        s.Disconnect("ui_scale_changed", onChanged);
        s.ApplyMode = previousMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!hadFile)
        {
            DirAccess.RemoveAbsolute(filePath);
        }
    }
}
