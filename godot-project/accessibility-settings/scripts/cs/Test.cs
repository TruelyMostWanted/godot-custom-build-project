using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.AccessibilitySettings;

using AccessibilitySettings = Godot.AccessibilitySettings;

/// <summary>
/// Full API test of Accessibility Settings (AccessibilitySettingsServer, AccessibilitySettings, AccessibilityColorScheme).
/// Same tests as scripts/gd/Test.gd. UI scale, font size, color scheme and focus outline are checked on the root window
/// and its overlay theme. The restart test starts the project again as its own process (desktop only).
/// </summary>
public partial class Test : FeatureTest
{
    private const string ProbeScene = "res://accessibility-settings/scenes/restart_probe.tscn";
    private const string AccessibilityFile = "user://settings/accessibility.ini";
    private const string SchemeFile = "user://accessibility_scheme.tres";
    private const AccessibilitySettingsServer.ColorScheme Dark = AccessibilitySettingsServer.ColorScheme.HighContrastDark;
    private static readonly Color CustomText = new(0.9f, 0.9f, 0.2f);

    private readonly Godot.Collections.Dictionary _received = [];

    protected override async Task RunTests()
    {
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
        TestDefaults();
        TestValues();
        TestValidation();
        TestUiScale();
        await TestTheme();
        TestHelpers();
        TestSpeech();
        TestSignals();
        TestResources();
        TestRestart();
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(AccessibilityFile);
    }

    private void TestDefaults()
    {
        ExpectEqual(AccessibilitySettingsServer.GetUiScale(), 1.0f, "the default UI scale is 1");
        ExpectEqual(AccessibilitySettingsServer.GetFontSizeFactor(), 1.0f, "the default font size factor is 1");
        ExpectEqual(AccessibilitySettingsServer.GetFocusOutlineWidth(), 0, "the default focus outline is the one of the theme");
        Expect(!AccessibilitySettingsServer.IsSpeakingFocusedControls(), "speaking focused controls is off by default");
        ExpectEqual(AccessibilitySettingsServer.GetColorblindMode(), AccessibilitySettingsServer.ColorblindMode.None, "the default colorblind mode is none");
        int reduceAnimation = (int)DisplayServer.AccessibilityShouldReduceAnimation();
        ExpectEqual(AccessibilitySettingsServer.IsReducingMotion(), reduceAnimation == 1, "reduced motion follows the OS");
        int increaseContrast = (int)DisplayServer.AccessibilityShouldIncreaseContrast();
        AccessibilitySettingsServer.ColorScheme expectedScheme = AccessibilitySettingsServer.ColorScheme.Default;
        if (increaseContrast == 1)
        {
            expectedScheme = Dark;
        }
        ExpectEqual(AccessibilitySettingsServer.GetColorScheme(), expectedScheme, "the color scheme follows the OS contrast");
        int reduceTransparency = (int)DisplayServer.AccessibilityShouldReduceTransparency();
        ExpectEqual(AccessibilitySettingsServer.IsReducingTransparency(), reduceTransparency == 1, "reduced transparency follows the OS");
        Expect(!AccessibilitySettingsServer.IsSubtitlesEnabled(), "subtitles are off by default");
        ExpectEqual(AccessibilitySettingsServer.GetSubtitleScale(), 1.0f, "the default subtitle scale is 1");
        ExpectEqual(AccessibilitySettingsServer.GetCustomColorSchemePath(), "", "no custom scheme by default");
        ExpectEqual(AccessibilitySettingsServer.GetSpeechPersona(), "", "no speech persona by default");
        bool screenReader = AccessibilitySettingsServer.IsScreenReaderActive();
        ExpectEqual(screenReader, (int)DisplayServer.AccessibilityScreenReaderActive() == 1, "IsScreenReaderActive follows the OS");
    }

    private void TestValues()
    {
        AccessibilitySettingsServer.SetUiScale(1.5f);
        ExpectEqual(AccessibilitySettingsServer.GetUiScale(), 1.5f, "ui_scale round trip");
        AccessibilitySettingsServer.SetFontSizeFactor(1.25f);
        ExpectEqual(AccessibilitySettingsServer.GetFontSizeFactor(), 1.25f, "font_size_factor round trip");
        AccessibilitySettingsServer.SetColorScheme(Dark);
        ExpectEqual(AccessibilitySettingsServer.GetColorScheme(), Dark, "color_scheme round trip");
        AccessibilitySettingsServer.SetCustomColorSchemePath("res://accessibility-settings/resources/yellow_on_dark_scheme.tres");
        ExpectEqual(AccessibilitySettingsServer.GetCustomColorSchemePath(), "res://accessibility-settings/resources/yellow_on_dark_scheme.tres", "custom_color_scheme_path round trip");
        AccessibilitySettingsServer.SetFocusOutlineWidth(4);
        ExpectEqual(AccessibilitySettingsServer.GetFocusOutlineWidth(), 4, "focus_outline_width round trip");
        AccessibilitySettingsServer.SetReduceMotion(true);
        Expect(AccessibilitySettingsServer.IsReducingMotion(), "reduce_motion round trip");
        AccessibilitySettingsServer.SetReduceTransparency(true);
        Expect(AccessibilitySettingsServer.IsReducingTransparency(), "reduce_transparency round trip");
        AccessibilitySettingsServer.SetColorblindMode(AccessibilitySettingsServer.ColorblindMode.Deuteranopia);
        ExpectEqual(AccessibilitySettingsServer.GetColorblindMode(), AccessibilitySettingsServer.ColorblindMode.Deuteranopia, "colorblind_mode round trip");
        AccessibilitySettingsServer.SetSubtitlesEnabled(true);
        Expect(AccessibilitySettingsServer.IsSubtitlesEnabled(), "subtitles_enabled round trip");
        AccessibilitySettingsServer.SetSubtitleScale(1.5f);
        ExpectEqual(AccessibilitySettingsServer.GetSubtitleScale(), 1.5f, "subtitle_scale round trip");
        AccessibilitySettingsServer.SetSpeechPersona("narrator");
        ExpectEqual(AccessibilitySettingsServer.GetSpeechPersona(), "narrator", "speech_persona round trip");
        AccessibilitySettingsServer.SetSpeakFocusedControls(true);
        Expect(AccessibilitySettingsServer.IsSpeakingFocusedControls(), "speak_focused_controls round trip");
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
    }

    private void TestValidation()
    {
        AccessibilitySettingsServer.SetUiScale(10.0f);
        ExpectEqual(AccessibilitySettingsServer.GetUiScale(), 4.0f, "the UI scale is clamped to 4");
        AccessibilitySettingsServer.SetFontSizeFactor(0.1f);
        ExpectEqual(AccessibilitySettingsServer.GetFontSizeFactor(), 0.5f, "the font size factor is clamped to 0.5");
        AccessibilitySettingsServer.SetFocusOutlineWidth(20);
        ExpectEqual(AccessibilitySettingsServer.GetFocusOutlineWidth(), 8, "the focus outline is clamped to 8");
        AccessibilitySettingsServer.Singleton.SetValue("color_scheme", 42);
        int defaultScheme = AccessibilitySettingsServer.Singleton.GetDefaultValue("color_scheme").AsInt32();
        ExpectEqual((int)AccessibilitySettingsServer.GetColorScheme(), defaultScheme, "an unknown color scheme keeps the current one");
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
    }

    private void TestUiScale()
    {
        Window root = GetTree().Root;
        float baseFactor = root.ContentScaleFactor;
        AccessibilitySettingsServer.SetUiScale(1.5f);
        Expect(Mathf.IsEqualApprox(root.ContentScaleFactor, baseFactor * 1.5f), "UI scale 150 % multiplies the root content scale factor");
        Expect(Mathf.IsEqualApprox(WindowSettingsServer.GetContentScaleFactor(), baseFactor), "the window factor stays separate");
        AccessibilitySettingsServer.SetUiScale(1.0f);
        Expect(Mathf.IsEqualApprox(root.ContentScaleFactor, baseFactor), "UI scale 100 % gives the factor back");
    }

    private async Task TestTheme()
    {
        Window root = GetTree().Root;
        Theme previousTheme = root.Theme;
        Expect(AccessibilitySettingsServer.GetOverlayTheme() == null, "no overlay theme with the defaults");
        AccessibilitySettingsServer.SetColorScheme(Dark);
        Theme overlay = AccessibilitySettingsServer.GetOverlayTheme();
        Expect(root.Theme == overlay, "a high-contrast scheme sets the overlay theme on the root window");
        AccessibilityColorScheme scheme = AccessibilitySettingsServer.GetActiveColorScheme();
        ExpectEqual(overlay.GetColor("font_color", "Button"), Colors.White, "button text is white");
        var focus = (StyleBoxFlat)overlay.GetStylebox("focus", "Button");
        ExpectEqual(focus.BorderColor, scheme.FocusColor, "the focus outline has the focus color of the scheme");
        var button = new Button();
        button.Text = "Play";
        AddChild(button);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(button.GetThemeColor("font_color"), Colors.White, "a button in the tree gets the text color of the scheme");
        ExpectEqual(AccessibilitySettingsServer.GetSchemeColor("problem", Colors.Magenta), scheme.ProblemColor, "GetSchemeColor returns the role color");
        int fontSizeBefore = button.GetThemeFontSize("font_size");
        AccessibilitySettingsServer.SetFontSizeFactor(1.5f);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(button.GetThemeFontSize("font_size"), Mathf.RoundToInt(fontSizeBefore * 1.5f), "a button in the tree gets the bigger font");
        AccessibilitySettingsServer.SetFocusOutlineWidth(4);
        var focusBox = (StyleBoxFlat)AccessibilitySettingsServer.GetOverlayTheme().GetStylebox("focus", "Button");
        ExpectEqual(focusBox.BorderWidthLeft, 4, "the focus outline width reaches the focus style box");
        button.QueueFree();
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
        Expect(AccessibilitySettingsServer.GetOverlayTheme() == null, "back at the defaults there is no overlay theme");
        Expect(root.Theme == previousTheme, "the root window gets its previous theme back");
    }

    private void TestHelpers()
    {
        AccessibilitySettingsServer.SetReduceMotion(true);
        ExpectEqual(AccessibilitySettingsServer.GetAnimationDuration(0.3f), 0.0f, "reduced motion: no animation duration");
        ExpectEqual(AccessibilitySettingsServer.GetMotionScale(), 0.0f, "reduced motion: motion scale 0");
        AccessibilitySettingsServer.SetReduceMotion(false);
        ExpectEqual(AccessibilitySettingsServer.GetAnimationDuration(0.3f), 0.3f, "no reduced motion: the duration stays");
        AccessibilitySettingsServer.SetFontSizeFactor(2.0f);
        ExpectEqual(AccessibilitySettingsServer.ScaleFontSize(16), 32, "ScaleFontSize multiplies by the factor");
        AccessibilitySettingsServer.SetSubtitleScale(1.5f);
        ExpectEqual(AccessibilitySettingsServer.ScaleSubtitleFontSize(10), 30, "ScaleSubtitleFontSize multiplies by the subtitle scale");
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSpeech()
    {
        var button = new Button();
        button.Text = "Play";
        ExpectEqual(AccessibilitySettingsServer.GetControlSpeechText(button), "Play", "the text of a button is spoken");
        button.AccessibilityName = "Start the game";
        ExpectEqual(AccessibilitySettingsServer.GetControlSpeechText(button), "Start the game", "accessibility_name wins over the text");
        var field = new LineEdit();
        field.PlaceholderText = "Player name";
        ExpectEqual(AccessibilitySettingsServer.GetControlSpeechText(field), "Player name", "an empty field speaks its placeholder");
        field.Free();
        Window root = GetTree().Root;
        int before = root.GetSignalConnectionList("gui_focus_changed").Count;
        AccessibilitySettingsServer.SetSpeakFocusedControls(true);
        ExpectEqual(root.GetSignalConnectionList("gui_focus_changed").Count, before + 1, "speaking focused controls connects to the root window");
        AccessibilitySettingsServer.SetSpeakFocusedControls(false);
        ExpectEqual(root.GetSignalConnectionList("gui_focus_changed").Count, before, "turning it off disconnects");
        if (TtsServer.IsAvailable() && !AccessibilitySettingsServer.IsScreenReaderActive())
        {
            var persona = new TtsPersona();
            persona.PersonaName = "test_narrator_cs";
            TtsServer.AddPersona(persona);
            AccessibilitySettingsServer.SetSpeechPersona("test_narrator_cs");
            long utterance = AccessibilitySettingsServer.SpeakControl(button);
            Expect(utterance >= 0, "SpeakControl returns an utterance id");
            TtsServer.Stop();
            TtsServer.RemovePersona("test_narrator_cs");
        }
        else
        {
            GD.Print("  SKIP  SpeakControl with a voice (no TTS voice or a screen reader is active)");
            ExpectEqual(AccessibilitySettingsServer.SpeakControl(button), -1L, "without a voice nothing is spoken");
        }
        button.Free();
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSignals()
    {
        AccessibilitySettingsServerInstance server = AccessibilitySettingsServer.Singleton;
        server.ColorSchemeChanged += scheme => _received["color_scheme"] = scheme;
        server.ColorblindModeChanged += mode => _received["colorblind_mode"] = (int)mode;
        server.FocusOutlineWidthChanged += width => _received["focus_outline_width"] = width;
        server.FontSizeFactorChanged += factor => _received["font_size_factor"] = factor;
        server.ReduceMotionChanged += enabled => _received["reduce_motion"] = enabled;
        server.ReduceTransparencyChanged += enabled => _received["reduce_transparency"] = enabled;
        server.SpeakFocusedControlsChanged += enabled => _received["speak_focused_controls"] = enabled;
        server.SubtitleScaleChanged += scale => _received["subtitle_scale"] = scale;
        server.SubtitlesEnabledChanged += enabled => _received["subtitles_enabled"] = enabled;
        server.UiScaleChanged += scale => _received["ui_scale"] = scale;
        server.ResetToDefaults();
        AccessibilitySettingsServer.SetColorScheme(Dark);
        AccessibilitySettingsServer.SetColorblindMode(AccessibilitySettingsServer.ColorblindMode.Tritanopia);
        AccessibilitySettingsServer.SetFocusOutlineWidth(3);
        AccessibilitySettingsServer.SetFontSizeFactor(1.5f);
        AccessibilitySettingsServer.SetReduceMotion(true);
        AccessibilitySettingsServer.SetReduceTransparency(true);
        AccessibilitySettingsServer.SetSpeakFocusedControls(true);
        AccessibilitySettingsServer.SetSubtitleScale(2.0f);
        AccessibilitySettingsServer.SetSubtitlesEnabled(true);
        AccessibilitySettingsServer.SetUiScale(1.5f);
        Expect(_received["color_scheme"].As<AccessibilityColorScheme>() != null, "ColorSchemeChanged passes the scheme");
        ExpectEqual((int)_received["colorblind_mode"], (int)AccessibilitySettingsServer.ColorblindMode.Tritanopia, "ColorblindModeChanged passes the mode");
        ExpectEqual((int)_received["focus_outline_width"], 3, "FocusOutlineWidthChanged passes the width");
        ExpectEqual((float)_received["font_size_factor"], 1.5f, "FontSizeFactorChanged passes the factor");
        Expect((bool)_received["reduce_motion"], "ReduceMotionChanged passes the flag");
        Expect((bool)_received["reduce_transparency"], "ReduceTransparencyChanged passes the flag");
        Expect((bool)_received["speak_focused_controls"], "SpeakFocusedControlsChanged passes the flag");
        ExpectEqual((float)_received["subtitle_scale"], 2.0f, "SubtitleScaleChanged passes the scale");
        Expect((bool)_received["subtitles_enabled"], "SubtitlesEnabledChanged passes the flag");
        ExpectEqual((float)_received["ui_scale"], 1.5f, "UiScaleChanged passes the scale");
        server.ResetToDefaults();
    }

    private void TestResources()
    {
        AccessibilityColorScheme dark = AccessibilityColorScheme.CreateHighContrastDark();
        AccessibilityColorScheme light = AccessibilityColorScheme.CreateHighContrastLight();
        ExpectEqual(dark.TextColor, Colors.White, "the dark scheme has white text");
        ExpectEqual(light.SurfaceColor, Colors.White, "the light scheme has white surfaces");
        ExpectEqual(dark.GetRoleColor("focus"), dark.FocusColor, "GetRoleColor returns the color of the role");
        ExpectEqual(dark.GetRoleColor("missing", Colors.Magenta), Colors.Magenta, "GetRoleColor returns the default for an unknown role");
        var scheme = new AccessibilityColorScheme();
        ExpectEqual(scheme.BorderWidth, 2, "AccessibilityColorScheme: default BorderWidth");
        ExpectEqual(scheme.FocusColor, new Color(1, 0.85f, 0, 1), "AccessibilityColorScheme: default FocusColor");
        scheme.TextColor = CustomText;
        ExpectEqual(ResourceSaver.Save(scheme, SchemeFile), Error.Ok, "a custom scheme can be saved");
        AccessibilitySettingsServer.SetCustomColorSchemePath(SchemeFile);
        AccessibilitySettingsServer.SetColorScheme(AccessibilitySettingsServer.ColorScheme.Custom);
        Theme overlay = AccessibilitySettingsServer.GetOverlayTheme();
        Expect(overlay.GetColor("font_color", "Label").IsEqualApprox(CustomText), "the custom scheme reaches the overlay theme");
        AccessibilitySettingsServer.Singleton.ResetToDefaults();
        DirAccess.RemoveAbsolute(SchemeFile);
        var settings = new AccessibilitySettings();
        ExpectEqual(settings.UiScale, 1.0f, "AccessibilitySettings: default UiScale");
        ExpectEqual(settings.ColorScheme, (int)AccessibilitySettingsServer.ColorScheme.Default, "AccessibilitySettings: default ColorScheme");
        Expect(!settings.ReduceMotion, "AccessibilitySettings: ReduceMotion is off by default");
        settings.UiScale = 2.0f;
        ExpectEqual(settings.UiScale, 2.0f, "AccessibilitySettings: UiScale is a plain property");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        AccessibilitySettingsServer.SetUiScale(1.5f);
        AccessibilitySettingsServer.SetColorScheme(Dark);
        AccessibilitySettingsServer.SetReduceMotion(true);
        ExpectEqual(AccessibilitySettingsServer.Singleton.Save(), Error.Ok, "Save writes the player file");
        string[] args = ["--headless", "--path", ProjectSettings.GlobalizePath("res://"), ProbeScene];
        var output = new Godot.Collections.Array();
        int code = OS.Execute(OS.GetExecutablePath(), args, output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        var probe = new Godot.Collections.Dictionary();
        foreach (string line in output[0].AsString().Split('\n'))
        {
            if (line.StartsWith("PROBE "))
            {
                probe = Json.ParseString(line["PROBE ".Length..]).AsGodotDictionary();
            }
        }
        ExpectEqual((int)probe["ready_frame"], 0, "restart: applied before the first frame");
        ExpectEqual((float)probe["ui_scale"], 1.5f, "restart: the saved UI scale is loaded");
        float expectedRoot = (float)probe["video_factor"] * 1.5f;
        Expect(Mathf.IsEqualApprox((float)probe["root_factor"], expectedRoot), "restart: the root window is scaled before the main scene is ready");
        ExpectEqual((int)probe["color_scheme"], (int)Dark, "restart: the saved scheme is loaded");
        Expect((bool)probe["overlay_on_root"], "restart: the overlay theme is on the root window before the main scene is ready");
        Expect((bool)probe["button_font_white"], "restart: a button of the main scene has the text color of the scheme");
        Expect((bool)probe["reduce_motion"], "restart: reduced motion is loaded");
    }
}
