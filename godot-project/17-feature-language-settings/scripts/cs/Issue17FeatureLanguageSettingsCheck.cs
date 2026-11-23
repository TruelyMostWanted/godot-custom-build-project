using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue17FeatureLanguageSettings;

/// <summary>
/// C# checks of 17-feature-language-settings. Check the same things as scripts/gd/check.gd.
/// The project has two translations (resources/hello_de.tres and hello_fr.tres) with the key CHECK17_HELLO.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The restart checks start the project again as their own processes (desktop only).
/// </summary>
public partial class Issue17FeatureLanguageSettingsCheck : FeatureCheckCs
{
    private const string ProbeScene = "res://17-feature-language-settings/scenes/restart_probe.tscn";
    private const string LanguageFile = "user://settings/language.ini";

    protected override string CheckName => "17-feature-language-settings";

    protected override async Task RunChecks()
    {
        var server = LanguageSettingsServer.Singleton;
        Expect(Engine.HasSingleton("LanguageSettingsServer"), "LanguageSettingsServer is a registered singleton");
        Expect(server is LanguageSettingsServerInstance, "LanguageSettingsServer.Singleton is the instance type");
        Expect(server is SettingsServer, "LanguageSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("language") == server, "the domain is registered as \"language\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetFilePath(), LanguageFile, "file path");
        ExpectEqual(string.Join(",", server.GetSettingNames()), "locale,pseudolocalization_enabled,layout_direction,root_auto_translate_mode", "setting names");

        CheckDefaults();
        CheckLocale();
        CheckInvalidLocale();
        CheckManualMode();
        CheckPseudolocalization();
        CheckLayoutDirection();
        CheckAutoTranslateMode();
        CheckResource();
        CheckRestart();

        // Leave no player files behind for later runs.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(LanguageFile);
    }

    private static string ProjectLocale() => TranslationServer.StandardizeLocale(LanguageSettingsServer.GetProjectLocale());

    private void CheckDefaults()
    {
        var server = LanguageSettingsServer.Singleton;
        server.ResetToDefaults();
        ExpectEqual(server.GetDefaultValue("locale").AsString(), "", "default locale follows the OS");
        ExpectEqual(server.GetDefaultValue("pseudolocalization_enabled").AsBool(), false, "default: no pseudolocalization");
        ExpectEqual(server.GetDefaultValue("layout_direction").AsInt32(), (int)Window.LayoutDirectionEnum.Inherited, "default layout direction is inherited");
        ExpectEqual(server.GetDefaultValue("root_auto_translate_mode").AsInt32(), (int)Node.AutoTranslateModeEnum.Always, "default auto-translate mode is ALWAYS (project setting)");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "", "locale is empty after reset");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "an empty locale gives the project (test or OS) locale");
        ExpectEqual(string.Join(",", LanguageSettingsServer.GetAvailableLocales()), "de,fr", "available locales: the two project translations, sorted");
        ExpectEqual(LanguageSettingsServer.GetCommandLineLocale(), "", "no --language argument in this run");
    }

    private void CheckLocale()
    {
        LanguageSettingsServer.SetLocale("de");
        ExpectEqual(TranslationServer.GetLocale(), "de", "SetLocale() reaches TranslationServer at once");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "de", "GetLocale()");
        ExpectEqual(Tr("CHECK17_HELLO"), "Hallo", "the German text is used");
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(Tr("CHECK17_HELLO"), "Bonjour", "the French text is used");
        LanguageSettingsServer.SetLocale("de-DE");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "de_DE", "the locale is standardized (de-DE becomes de_DE)");
        ExpectEqual(Tr("CHECK17_HELLO"), "Hallo", "a regional variant uses the translation of its language");
        LanguageSettingsServer.SetLocale("");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "an empty locale follows the OS (or the test locale) again");
    }

    private void CheckInvalidLocale()
    {
        LanguageSettingsServer.SetLocale("zz_ZZ");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "zz_ZZ", "a locale without translation is kept as the stored value");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "no translation for the locale: the project locale is used, without an error");
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(TranslationServer.GetLocale(), "fr", "a valid locale works again afterwards");
        LanguageSettingsServer.SetLocale("");
    }

    private void CheckManualMode()
    {
        var server = LanguageSettingsServer.Singleton;
        server.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(TranslationServer.GetLocale(), ProjectLocale(), "manual mode: not applied yet");
        Expect(server.IsDirty(), "manual mode: dirty");
        server.Revert();
        ExpectEqual(LanguageSettingsServer.GetLocale(), "", "Revert() gives back the applied locale");
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(server.Apply(), Error.Ok, "Apply() in manual mode");
        ExpectEqual(TranslationServer.GetLocale(), "fr", "Apply() reaches TranslationServer");
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        server.ResetToDefaults();
    }

    private void CheckPseudolocalization()
    {
        var server = LanguageSettingsServer.Singleton;
        LanguageSettingsServer.SetLocale("fr");
        Expect(!LanguageSettingsServer.IsPseudolocalizationEnabled(), "pseudolocalization is off by default");
        LanguageSettingsServer.SetPseudolocalizationEnabled(true);
        Expect(TranslationServer.PseudolocalizationEnabled, "SetPseudolocalizationEnabled() reaches TranslationServer");
        Expect(LanguageSettingsServer.IsPseudolocalizationEnabled(), "IsPseudolocalizationEnabled()");
        string text = Tr("CHECK17_HELLO");
        Expect(text != "Bonjour" && text.Length >= "Bonjour".Length, "the text is pseudolocalized (changed, not shorter)");
        LanguageSettingsServer.SetPseudolocalizationEnabled(false);
        ExpectEqual(Tr("CHECK17_HELLO"), "Bonjour", "pseudolocalization off gives back the translation");
        server.ResetToDefaults();
    }

    private void CheckLayoutDirection()
    {
        var root = GetTree().Root;
        LanguageSettingsServer.SetLayoutDirection(Window.LayoutDirectionEnum.Rtl);
        ExpectEqual((int)root.LayoutDirection, (int)Window.LayoutDirectionEnum.Rtl, "the root window gets the layout direction");
        ExpectEqual((int)LanguageSettingsServer.GetLayoutDirection(), (int)Window.LayoutDirectionEnum.Rtl, "GetLayoutDirection()");
        LanguageSettingsServer.SetLayoutDirection(Window.LayoutDirectionEnum.Ltr);
        ExpectEqual((int)root.LayoutDirection, (int)Window.LayoutDirectionEnum.Ltr, "left-to-right");
        LanguageSettingsServer.Singleton.ResetToDefaults();
        ExpectEqual((int)root.LayoutDirection, (int)Window.LayoutDirectionEnum.Inherited, "ResetToDefaults() gives back the inherited direction");
    }

    private void CheckAutoTranslateMode()
    {
        var root = GetTree().Root;
        LanguageSettingsServer.SetRootAutoTranslateMode(Node.AutoTranslateModeEnum.Disabled);
        ExpectEqual((int)root.AutoTranslateMode, (int)Node.AutoTranslateModeEnum.Disabled, "the root window gets the auto-translate mode");
        ExpectEqual((int)LanguageSettingsServer.GetRootAutoTranslateMode(), (int)Node.AutoTranslateModeEnum.Disabled, "GetRootAutoTranslateMode()");
        LanguageSettingsServer.Singleton.ResetToDefaults();
        ExpectEqual((int)root.AutoTranslateMode, (int)Node.AutoTranslateModeEnum.Always, "ResetToDefaults() gives back ALWAYS");
    }

    private void CheckResource()
    {
        var settings = new LanguageSettings();
        ExpectEqual(settings.Locale, "", "LanguageSettings: default locale");
        ExpectEqual(settings.PseudolocalizationEnabled, false, "LanguageSettings: default pseudolocalization");
        ExpectEqual((int)settings.LayoutDirection, (int)Window.LayoutDirectionEnum.Inherited, "LanguageSettings: default layout direction");
        settings.Locale = "fr";
        ExpectEqual(settings.Locale, "fr", "LanguageSettings: locale is a plain property");
    }

    private void CheckRestart()
    {
        if (!OS.HasFeature("pc"))
        {
            GD.Print("  SKIP  restart check (needs a desktop platform to start a second process)");
            return;
        }
        var server = LanguageSettingsServer.Singleton;

        // 1) The saved locale, layout direction and auto-translate mode are applied before the main scene.
        LanguageSettingsServer.SetLocale("fr");
        LanguageSettingsServer.SetLayoutDirection(Window.LayoutDirectionEnum.Rtl);
        LanguageSettingsServer.SetRootAutoTranslateMode(Node.AutoTranslateModeEnum.Disabled);
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the player file");
        Expect(FileAccess.FileExists(LanguageFile), "the player file is in user://settings/");
        ExpectEqual(ReadStoredLocale(), "fr", "the file contains the locale");
        var probe = RunProbe([]);
        Expect(probe.ContainsKey("started") && probe["started"].AsBool(), "restart: the domain was started");
        ExpectEqual(ProbeString(probe, "locale"), "fr", "restart: the saved locale is active");
        ExpectEqual(ProbeString(probe, "hello"), "Bonjour", "restart: the first text is already French");
        ExpectEqual(ProbeInt(probe, "layout_direction"), (int)Window.LayoutDirectionEnum.Rtl, "restart: the saved layout direction is on the root window");
        ExpectEqual(ProbeInt(probe, "auto_translate_mode"), (int)Node.AutoTranslateModeEnum.Disabled, "restart: the saved auto-translate mode is on the root window");
        ExpectEqual(ProbeInt(probe, "ready_frame"), 0, "restart: applied before the first frame");

        // 2) --language is a session override: active, but never saved.
        probe = RunProbe(["--language", "de"]);
        ExpectEqual(ProbeString(probe, "command_line_locale"), "de", "--language: the argument is read");
        ExpectEqual(ProbeString(probe, "locale"), "de", "--language wins over the stored locale");
        ExpectEqual(ProbeString(probe, "hello"), "Hallo", "--language: the first text is German");
        ExpectEqual(ReadStoredLocale(), "fr", "--language is not saved: the file still has the stored locale");

        // 3) Stored pseudolocalization is applied; an unknown stored locale falls back without an error.
        LanguageSettingsServer.SetPseudolocalizationEnabled(true);
        LanguageSettingsServer.SetLocale("zz_ZZ");
        ExpectEqual(server.Save(), Error.Ok, "Save() with an unknown locale");
        probe = RunProbe([]);
        Expect(probe.ContainsKey("pseudolocalization") && probe["pseudolocalization"].AsBool(), "restart: the saved pseudolocalization is on");
        ExpectEqual(ProbeString(probe, "locale"), ProbeString(probe, "project_locale"), "restart: an unknown stored locale falls back to the project locale");
    }

    private static string ProbeString(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) ? probe[key].AsString() : "<missing>";

    private static int ProbeInt(Godot.Collections.Dictionary probe, string key) => probe.ContainsKey(key) ? probe[key].AsInt32() : -1;

    private static string ReadStoredLocale()
    {
        var config = new ConfigFile();
        if (config.Load(LanguageFile) != Error.Ok)
        {
            return "<no file>";
        }
        foreach (string section in config.GetSections())
        {
            if (config.HasSectionKey(section, "locale"))
            {
                return config.GetValue(section, "locale").AsString();
            }
        }
        return "<no locale key>";
    }

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
                if (line.StartsWith("CHECK17_PROBE "))
                {
                    Variant parsed = Json.ParseString(line["CHECK17_PROBE ".Length..]);
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
