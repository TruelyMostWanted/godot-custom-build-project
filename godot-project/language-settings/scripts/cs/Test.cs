using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.LanguageSettingsGuide;

/// <summary>
/// Full API test of Language Settings: LanguageSettingsServer and LanguageSettings. Same tests as scripts/gd/Test.gd.
/// The project has two translations (resources/hello_de.translation and hello_fr.translation) with the key CHECK17_HELLO.
/// </summary>
public partial class Test : FeatureTest
{
    private const string ProbeScene = "res://language-settings/scenes/restart_probe.tscn";
    private const string LanguageFile = "user://settings/language.ini";
    private const string HelloKey = "CHECK17_HELLO";
    private const string HelloDe = "Hallo";
    private const string HelloFr = "Bonjour";

    private readonly List<string> _locales = [];
    private readonly List<bool> _pseudo = [];
    private readonly List<Window.LayoutDirection> _directions = [];
    private readonly List<Node.AutoTranslateModeEnum> _modes = [];

    private static LanguageSettingsServerInstance Server => LanguageSettingsServer.Singleton;

    protected override async Task RunTests()
    {
        TestRegistration();
        TestDefaults();
        TestLocale();
        TestInvalidLocale();
        TestChannels();
        TestManualMode();
        TestPseudolocalization();
        TestLayoutDirection();
        TestAutoTranslateMode();
        await TestSignals();
        TestResource();
        TestRestart();
        Server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(LanguageFile);
    }

    private static string ProjectLocale()
    {
        return TranslationServer.StandardizeLocale(LanguageSettingsServer.GetProjectLocale());
    }

    private string Hello()
    {
        return Tr(HelloKey);
    }

    private void TestRegistration()
    {
        Expect(Engine.HasSingleton("LanguageSettingsServer"), "LanguageSettingsServer is a registered singleton");
        Expect(Server is SettingsServer, "LanguageSettingsServer is a SettingsServer");
        Expect(SettingsServerManager.GetServer("language") == Server, "the domain is registered as language");
        Expect(Server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(Server.FilePath, LanguageFile, "file path");
        string[] names = ["locale", "pseudolocalization_enabled", "layout_direction", "root_auto_translate_mode", "interface_locale", "audio_locale", "subtitles_locale"];
        Expect(Server.GetSettingNames().SequenceEqual(names), "setting names");
    }

    private void TestDefaults()
    {
        Server.ResetToDefaults();
        ExpectEqual(Server.GetDefaultValue("locale").AsString(), "", "default locale follows the OS");
        ExpectEqual(Server.GetDefaultValue("pseudolocalization_enabled").AsBool(), false, "default: no pseudolocalization");
        ExpectEqual((Window.LayoutDirection)Server.GetDefaultValue("layout_direction").AsInt32(), Window.LayoutDirection.Inherited, "default layout direction is inherited");
        ExpectEqual((Node.AutoTranslateModeEnum)Server.GetDefaultValue("root_auto_translate_mode").AsInt32(), Node.AutoTranslateModeEnum.Always, "default auto-translate mode is Always (project setting)");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "", "locale is empty after reset");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "an empty locale gives the project (test or OS) locale");
        Expect(LanguageSettingsServer.GetAvailableLocales().SequenceEqual(["de", "fr"]), "available locales: the two project translations, sorted");
        ExpectEqual(LanguageSettingsServer.GetCommandLineLocale(), "", "no --language argument in this run");
    }

    private void TestLocale()
    {
        LanguageSettingsServer.SetLocale("de");
        ExpectEqual(TranslationServer.GetLocale(), "de", "SetLocale reaches TranslationServer at once");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "de", "GetLocale");
        ExpectEqual(Hello(), HelloDe, "the German text is used");
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(Hello(), HelloFr, "the French text is used");
        LanguageSettingsServer.SetLocale("de-DE");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "de_DE", "the locale is standardized (de-DE becomes de_DE)");
        ExpectEqual(Hello(), HelloDe, "a regional variant uses the translation of its language");
        LanguageSettingsServer.SetLocale("");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "an empty locale follows the OS (or the test locale) again");
    }

    private void TestInvalidLocale()
    {
        LanguageSettingsServer.SetLocale("zz_ZZ");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "zz_ZZ", "a locale without translation is kept as the stored value");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "no translation for the locale: the project locale is used, without an error");
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(TranslationServer.GetLocale(), "fr", "a valid locale works again afterwards");
        LanguageSettingsServer.SetLocale("");
    }

    private void TestChannels()
    {
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("interface"), "", "a channel is the same as the language by default");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor(""), "fr", "an empty channel is the language");
        LanguageSettingsServer.SetLocaleFor("audio", "de");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("audio"), "de", "SetLocaleFor stores the audio locale");
        ExpectEqual(Server.GetValue("audio_locale").AsString(), "de", "the audio locale is the setting audio_locale");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "de", "the audio locale is in use");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "fr", "the interface follows the language");
        LanguageSettingsServer.SetLocaleFor("subtitles", "de-DE");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("subtitles"), "de_DE", "a channel locale is standardized");
        LanguageSettingsServer.SetLocaleFor("interface", "de");
        ExpectEqual(Server.GetValue("interface_locale").AsString(), "de", "the interface locale is the setting interface_locale");
        ExpectEqual(Hello(), HelloDe, "the interface channel changes the translated text");
        LanguageSettingsServer.SetLocaleFor("", "fr");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "fr", "an empty channel in SetLocaleFor is SetLocale");
        LanguageSettingsServer.SetLocaleFor("video", "de");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("video"), "", "an unknown channel is refused (an error is printed)");
        Expect(LanguageSettingsServer.GetAvailableLocalesFor("interface").SequenceEqual(["de", "fr"]), "GetAvailableLocalesFor: the loaded translations for interface");
        Expect(LanguageSettingsServer.GetAvailableLocalesFor("audio").SequenceEqual(LanguageSettingsServer.GetAvailableLocales()), "a channel without catalogs lists all locales");
        Expect(LanguageSettingsServer.GetAvailableLocalesFor("").SequenceEqual(LanguageSettingsServer.GetAvailableLocales()), "an empty channel lists all locales");
        Server.ResetToDefaults();
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("audio"), "", "ResetToDefaults clears the channels");
    }

    private void TestManualMode()
    {
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "manual mode: not applied yet");
        Expect(Server.IsDirty(), "manual mode: dirty");
        Server.Revert();
        ExpectEqual(LanguageSettingsServer.GetLocale(), "", "Revert gives back the applied locale");
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(Server.Apply(), Error.Ok, "Apply in manual mode");
        ExpectEqual(TranslationServer.GetLocale(), "fr", "Apply reaches TranslationServer");
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        Server.ResetToDefaults();
    }

    private void TestPseudolocalization()
    {
        LanguageSettingsServer.SetLocale("fr");
        Expect(!LanguageSettingsServer.IsPseudolocalizationEnabled(), "pseudolocalization is off by default");
        LanguageSettingsServer.SetPseudolocalizationEnabled(true);
        Expect(TranslationServer.PseudolocalizationEnabled, "SetPseudolocalizationEnabled reaches TranslationServer");
        Expect(LanguageSettingsServer.IsPseudolocalizationEnabled(), "IsPseudolocalizationEnabled");
        string text = Hello();
        Expect(text != HelloFr, "the text is pseudolocalized (changed)");
        Expect(text.Length >= HelloFr.Length, "the pseudolocalized text is not shorter");
        LanguageSettingsServer.SetPseudolocalizationEnabled(false);
        ExpectEqual(Hello(), HelloFr, "pseudolocalization off gives back the translation");
        Server.ResetToDefaults();
    }

    private void TestLayoutDirection()
    {
        Window root = GetTree().Root;
        LanguageSettingsServer.SetLayoutDirection(Window.LayoutDirection.Rtl);
        ExpectEqual(root.GetLayoutDirection(), Window.LayoutDirection.Rtl, "the root window gets the layout direction");
        ExpectEqual(LanguageSettingsServer.GetLayoutDirection(), Window.LayoutDirection.Rtl, "GetLayoutDirection");
        LanguageSettingsServer.SetLayoutDirection(Window.LayoutDirection.Ltr);
        ExpectEqual(root.GetLayoutDirection(), Window.LayoutDirection.Ltr, "left-to-right");
        Server.ResetToDefaults();
        ExpectEqual(root.GetLayoutDirection(), Window.LayoutDirection.Inherited, "ResetToDefaults gives back the inherited direction");
    }

    private void TestAutoTranslateMode()
    {
        Window root = GetTree().Root;
        LanguageSettingsServer.SetRootAutoTranslateMode(Node.AutoTranslateModeEnum.Disabled);
        ExpectEqual(root.AutoTranslateMode, Node.AutoTranslateModeEnum.Disabled, "the root window gets the auto-translate mode");
        ExpectEqual(LanguageSettingsServer.GetRootAutoTranslateMode(), Node.AutoTranslateModeEnum.Disabled, "GetRootAutoTranslateMode");
        Server.ResetToDefaults();
        ExpectEqual(root.AutoTranslateMode, Node.AutoTranslateModeEnum.Always, "ResetToDefaults gives back Always");
    }

    private async Task TestSignals()
    {
        TranslationServer.LocaleChangedEventHandler onLocale = (channel, locale, previous) => { if (channel == "") { _locales.Add(locale); } };
        LanguageSettingsServer.PseudolocalizationEnabledChangedEventHandler onPseudo = enabled => _pseudo.Add(enabled);
        LanguageSettingsServer.LayoutDirectionChangedEventHandler onDirection = direction => _directions.Add(direction);
        LanguageSettingsServer.RootAutoTranslateModeChangedEventHandler onMode = mode => _modes.Add(mode);
        TranslationServer.LocaleChanged += onLocale;
        LanguageSettingsServer.PseudolocalizationEnabledChanged += onPseudo;
        LanguageSettingsServer.LayoutDirectionChanged += onDirection;
        LanguageSettingsServer.RootAutoTranslateModeChanged += onMode;
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        LanguageSettingsServer.SetLocale("fr");
        LanguageSettingsServer.SetPseudolocalizationEnabled(true);
        LanguageSettingsServer.SetLayoutDirection(Window.LayoutDirection.Rtl);
        LanguageSettingsServer.SetRootAutoTranslateMode(Node.AutoTranslateModeEnum.Disabled);
        Expect(_locales.Count == 0 && _pseudo.Count == 0, "the typed signals do not fire for pending changes in manual mode");
        Server.Apply();
        Expect(_locales.SequenceEqual(["fr"]), "TranslationServer.LocaleChanged fires once on Apply with the locale");
        Expect(_pseudo.SequenceEqual([true]), "PseudolocalizationEnabledChanged fires once on Apply");
        Expect(_directions.SequenceEqual([Window.LayoutDirection.Rtl]), "LayoutDirectionChanged fires once on Apply");
        Expect(_modes.SequenceEqual([Node.AutoTranslateModeEnum.Disabled]), "RootAutoTranslateModeChanged fires once on Apply");
        Server.Apply();
        ExpectEqual(_locales.Count, 1, "LocaleChanged does not fire when nothing changed");
        Server.ResetToDefaults();
        Server.Apply();
        ExpectEqual(_locales.Count, 2, "LocaleChanged fires again when the default is applied");
        Server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        TranslationServer.LocaleChanged -= onLocale;
        LanguageSettingsServer.PseudolocalizationEnabledChanged -= onPseudo;
        LanguageSettingsServer.LayoutDirectionChanged -= onDirection;
        LanguageSettingsServer.RootAutoTranslateModeChanged -= onMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void TestResource()
    {
        var settings = new Godot.LanguageSettings();
        ExpectEqual(settings.Locale, "", "LanguageSettings: default locale");
        ExpectEqual(settings.PseudolocalizationEnabled, false, "LanguageSettings: default pseudolocalization");
        ExpectEqual(settings.LayoutDirection, (int)Window.LayoutDirection.Inherited, "LanguageSettings: default layout direction");
        ExpectEqual(settings.RootAutoTranslateMode, (int)Node.AutoTranslateModeEnum.Always, "LanguageSettings: default auto-translate mode");
        settings.Locale = "fr";
        settings.InterfaceLocale = "de";
        settings.AudioLocale = "de";
        settings.SubtitlesLocale = "fr";
        ExpectEqual(settings.Locale, "fr", "LanguageSettings.Locale");
        ExpectEqual(settings.InterfaceLocale, "de", "LanguageSettings.InterfaceLocale");
        ExpectEqual(settings.AudioLocale, "de", "LanguageSettings.AudioLocale");
        ExpectEqual(settings.SubtitlesLocale, "fr", "LanguageSettings.SubtitlesLocale");
        var sample = GD.Load<Godot.LanguageSettings>("res://language-settings/resources/german_settings.tres");
        ExpectEqual(sample.Locale, "de", "the sample LanguageSettings loads");
    }

    private void TestRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart test (needs a desktop platform to start a second process)");
            return;
        }
        LanguageSettingsServer.SetLocale("fr");
        LanguageSettingsServer.SetLayoutDirection(Window.LayoutDirection.Rtl);
        LanguageSettingsServer.SetRootAutoTranslateMode(Node.AutoTranslateModeEnum.Disabled);
        ExpectEqual(Server.Save(), Error.Ok, "Save writes the player file");
        Expect(FileAccess.FileExists(LanguageFile), "the player file is in user://settings/");
        ExpectEqual(ReadStoredLocale(), "fr", "the file contains the locale");
        Godot.Collections.Dictionary probe = RunProbe([]);
        Expect(probe["started"].AsBool(), "restart: the domain was started");
        ExpectEqual(probe["locale"].AsString(), "fr", "restart: the saved locale is active");
        ExpectEqual(probe["hello"].AsString(), HelloFr, "restart: the first text is already French");
        ExpectEqual(probe["layout_direction"].AsInt32(), (int)Window.LayoutDirection.Rtl, "restart: the saved layout direction is on the root window");
        ExpectEqual(probe["auto_translate_mode"].AsInt32(), (int)Node.AutoTranslateModeEnum.Disabled, "restart: the saved auto-translate mode is on the root window");
        ExpectEqual(probe["ready_frame"].AsInt32(), 0, "restart: applied before the first frame");
        probe = RunProbe(["--language", "de"]);
        ExpectEqual(probe["command_line_locale"].AsString(), "de", "--language: the argument is read");
        ExpectEqual(probe["locale"].AsString(), "de", "--language wins over the stored locale");
        ExpectEqual(probe["hello"].AsString(), HelloDe, "--language: the first text is German");
        ExpectEqual(ReadStoredLocale(), "fr", "--language is not saved: the file still has the stored locale");
        LanguageSettingsServer.SetPseudolocalizationEnabled(true);
        LanguageSettingsServer.SetLocale("zz_ZZ");
        ExpectEqual(Server.Save(), Error.Ok, "Save with an unknown locale");
        probe = RunProbe([]);
        Expect(probe["pseudolocalization"].AsBool(), "restart: the saved pseudolocalization is on");
        ExpectEqual(probe["locale"].AsString(), probe["project_locale"].AsString(), "restart: an unknown stored locale falls back to the project locale");
    }

    private static string ReadStoredLocale()
    {
        string text = FileAccess.GetFileAsString(LanguageFile);
        foreach (string line in text.Split('\n'))
        {
            string stripped = line.StripEdges();
            if (stripped.StartsWith("locale="))
            {
                return stripped["locale=".Length..].Trim('"');
            }
        }
        return "<no locale key>";
    }

    private Godot.Collections.Dictionary RunProbe(string[] extraArgs)
    {
        var output = new Godot.Collections.Array();
        string[] args = ["--headless", .. extraArgs, "--path", ProjectSettings.GlobalizePath("res://"), ProbeScene];
        int code = OS.Execute(OS.GetExecutablePath(), args, output, true);
        ExpectEqual(code, 0, "restart: the probe process ran");
        foreach (Variant chunk in output)
        {
            foreach (string line in chunk.AsString().Split('\n'))
            {
                if (line.StartsWith("PROBE "))
                {
                    return Json.ParseString(line["PROBE ".Length..]).AsGodotDictionary();
                }
            }
        }
        Expect(false, "restart: the probe printed its result");
        return [];
    }
}
