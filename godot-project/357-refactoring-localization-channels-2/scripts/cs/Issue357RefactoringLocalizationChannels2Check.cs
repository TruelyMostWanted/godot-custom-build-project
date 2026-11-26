using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue357RefactoringLocalizationChannels2;

/// <summary>
/// C# checks of 357-refactoring-localization-channels-2. Check the same things as scripts/gd/check.gd.
/// Part 2 of the issue: locale channels in LanguageSettingsServer and translator notes in localization catalogs.
/// The project has two translations (de, fr). Expected errors are printed: a custom channel in SetLocaleFor(),
/// the reserved note column "plural".
/// </summary>
public partial class Issue357RefactoringLocalizationChannels2Check : FeatureCheckCs
{
    private const string DataDir = "user://check_357_2_cs";
    private static readonly string[] FormatClasses = { "JSON", "INI", "CSV", "XML", "TOML", "YAML" };

    protected override string CheckName => "357-refactoring-localization-channels-2";

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(DataDir);
        var server = LanguageSettingsServer.Singleton;
        var textServer = TextLocalizationServer.Singleton;
        string previousLocale = TranslationServer.GetLocale();
        string previousPath = server.GetFilePath();
        var previousMode = server.ApplyMode;
        server.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        textServer.ClearCatalogs();

        CheckProperties(server);
        CheckChannels(server);
        CheckCustomChannel(server);
        CheckSaveAndLoad(server);
        CheckOldFile(server);
        CheckAvailability(server, textServer);
        CheckNotes();
        CheckNoteFormats();
        CheckNoteFiles();

        // Clean up.
        textServer.ClearCatalogs();
        server.SetFilePath(previousPath);
        server.ResetToDefaults();
        foreach (string channel in new[] { "interface", "audio", "subtitles" })
        {
            TranslationServer.ClearLocaleFor(channel);
        }
        TranslationServer.SetLocale(previousLocale);
        server.ApplyMode = previousMode;
        using (var dir = DirAccess.Open(DataDir))
        {
            if (dir != null)
            {
                foreach (string file in dir.GetFiles())
                {
                    dir.Remove(file);
                }
            }
        }
        DirAccess.RemoveAbsolute(DataDir);
        return Task.CompletedTask;
    }

    private void CheckProperties(LanguageSettingsServerInstance server)
    {
        server.ResetToDefaults();
        string[] names = server.GetSettingNames();
        foreach (string setting in new[] { "interface_locale", "audio_locale", "subtitles_locale" })
        {
            Expect(names.Contains(setting), "setting " + setting);
            ExpectEqual(server.GetDefaultValue(setting).AsString(), "", $"default of {setting} is empty (same as language)");
        }
        var settings = new LanguageSettings();
        ExpectEqual(settings.InterfaceLocale, "", "LanguageSettings: default InterfaceLocale");
        settings.AudioLocale = "de";
        settings.SubtitlesLocale = "fr";
        ExpectEqual(settings.AudioLocale, "de", "LanguageSettings: AudioLocale is a plain property");
        ExpectEqual(settings.SubtitlesLocale, "fr", "LanguageSettings: SubtitlesLocale is a plain property");
    }

    private void CheckChannels(LanguageSettingsServerInstance server)
    {
        server.ResetToDefaults();
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("audio"), "", "GetLocaleFor: empty means the language");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "fr", "an empty channel follows the language");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor(""), "fr", "channel \"\" is the language");
        LanguageSettingsServer.SetLocaleFor("interface", "de");
        LanguageSettingsServer.SetLocaleFor("audio", "de");
        LanguageSettingsServer.SetLocaleFor("subtitles", "fr");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("interface"), "de", "GetLocaleFor interface");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("audio"), "de", "GetLocaleFor audio");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("subtitles"), "fr", "GetLocaleFor subtitles");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "TranslationServer interface follows");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "de", "TranslationServer audio follows");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "fr", "TranslationServer subtitles follows");
        ExpectEqual(TranslationServer.GetLocale(), "fr", "the language is untouched");
        ExpectEqual(server.GetValue("audio_locale").AsString(), "de", "the setting audio_locale has the value");
        LanguageSettingsServer.SetLocaleFor("audio", "");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "fr", "an empty channel locale follows the language again");
        LanguageSettingsServer.SetLocale("de");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "de", "a language change moves the channels that follow it");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "fr", "a channel with its own locale stays");
        server.ResetToDefaults();
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), TranslationServer.GetLocale(), "ResetToDefaults() clears the channels");
    }

    private void CheckCustomChannel(LanguageSettingsServerInstance server)
    {
        // Expected error: only interface, audio and subtitles can be stored.
        LanguageSettingsServer.SetLocale("fr");
        LanguageSettingsServer.SetLocaleFor("custom", "de");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("custom"), "", "a custom channel is refused, nothing is stored");
        ExpectEqual(TranslationServer.GetLocaleFor("custom"), "fr", "a custom channel follows the language");
        server.ResetToDefaults();
    }

    private void CheckSaveAndLoad(LanguageSettingsServerInstance server)
    {
        string path = DataDir + "/language.ini";
        server.SetFilePath(path);
        server.ResetToDefaults();
        LanguageSettingsServer.SetLocale("fr");
        LanguageSettingsServer.SetLocaleFor("interface", "de");
        LanguageSettingsServer.SetLocaleFor("audio", "de");
        ExpectEqual(server.Save(), Error.Ok, "Save() writes the channels");
        string text = FileAccess.GetFileAsString(path);
        Expect(text.Contains("interface_locale") && text.Contains("audio_locale"), "the file has the channel keys");
        server.ResetToDefaults();
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("interface"), "", "after reset the channels are empty");
        ExpectEqual(server.Load(), Error.Ok, "Load() reads the file");
        ExpectEqual(server.Apply(), Error.Ok, "Apply() uses the loaded values");
        ExpectEqual(LanguageSettingsServer.GetLocale(), "fr", "the language is loaded");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("interface"), "de", "interface_locale is loaded");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("audio"), "de", "audio_locale is loaded");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("subtitles"), "", "subtitles_locale stays empty");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "the loaded interface locale is applied");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "fr", "subtitles follow the language");
        server.ResetToDefaults();
    }

    private void CheckOldFile(LanguageSettingsServerInstance server)
    {
        string path = DataDir + "/before_channels.json";
        var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString("{ \"locale\": \"fr\" }");
        file.Close();
        server.SetFilePath(path);
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "an old file without channel keys loads");
        server.Apply();
        ExpectEqual(LanguageSettingsServer.GetLocale(), "fr", "old file: the language is loaded");
        foreach (string channel in new[] { "interface", "audio", "subtitles" })
        {
            ExpectEqual(LanguageSettingsServer.GetLocaleFor(channel), "", $"old file: channel {channel} is empty");
            ExpectEqual(TranslationServer.GetLocaleFor(channel), "fr", $"old file: channel {channel} follows the language");
        }
        server.ResetToDefaults();
    }

    private void CheckAvailability(LanguageSettingsServerInstance server, TextLocalizationServerInstance textServer)
    {
        var interfaceTexts = new TextLocalizationCatalog();
        interfaceTexts.SetText("HELLO", "en", "Hello");
        interfaceTexts.SetText("HELLO", "de", "Hallo");
        var subtitles = new TextLocalizationCatalog { Channel = "subtitles" };
        subtitles.SetText("LINE_1", "en", "Hi");
        subtitles.SetText("LINE_1", "es", "Hola");
        textServer.AddCatalog(interfaceTexts);
        textServer.AddCatalog(subtitles);

        ExpectEqual(string.Join(",", textServer.GetAvailableLocalesFor("subtitles")), "en,es", "TextLocalizationServer.GetAvailableLocalesFor(subtitles)");
        Expect(textServer.GetAvailableLocalesFor("audio").Length == 0, "no catalog with audio keys: empty");
        ExpectEqual(string.Join(",", LanguageSettingsServer.GetAvailableLocalesFor("subtitles")), "en,es", "GetAvailableLocalesFor(subtitles) is sorted");
        string[] interfaceLocales = LanguageSettingsServer.GetAvailableLocalesFor("interface");
        Expect(interfaceLocales.Contains("en") && interfaceLocales.Contains("de"), "interface: the catalog locales");
        Expect(interfaceLocales.Contains("fr"), "interface: also the Godot translation locales");
        Expect(!interfaceLocales.Contains("es"), "interface: not the subtitle-only locale");
        ExpectEqual(string.Join(",", LanguageSettingsServer.GetAvailableLocalesFor("audio")), string.Join(",", LanguageSettingsServer.GetAvailableLocales()), "a channel no catalog declares: all locales");
        Expect(LanguageSettingsServer.GetAvailableLocales().Contains("es"), "GetAvailableLocales() has the locales of all catalogs");

        LanguageSettingsServer.SetLocale("en");
        LanguageSettingsServer.SetLocaleFor("subtitles", "es");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "es", "an available channel locale is used");
        LanguageSettingsServer.SetLocaleFor("subtitles", "de");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "en", "an unavailable channel locale: the channel follows the language");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("subtitles"), "de", "the stored value is kept");
        textServer.ClearCatalogs();
        server.ResetToDefaults();
    }

    private static TextLocalizationCatalog NotesCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "en", "Hello");
        catalog.SetText("HELLO", "de", "Hallo");
        catalog.SetText("HELLO", "en", "Hello (menu)", "menu");
        catalog.SetText("HELLO", "de", "Hallo (Menü)", "menu");
        catalog.SetText("APPLE", "en", "Apple");
        catalog.SetText("APPLE", "de", "Apfel");
        catalog.SetText("LINE", "en", "One");
        catalog.SetText("LINE", "de", "Eins");
        catalog.SetNote("HELLO", "greeting");
        catalog.SetNote("HELLO", "Main menu button", "menu");
        catalog.SetNote("APPLE", "12", "", "max_length");
        catalog.SetNote("LINE", "Two lines\nkeep it short");
        return catalog;
    }

    private void CheckNotes()
    {
        ExpectEqual(new TextLocalizationCatalog().GetNoteColumns().Length, 0, "a new catalog has no note columns");
        var catalog = NotesCatalog();
        ExpectEqual(string.Join(",", catalog.GetNoteColumns()), "notes,max_length", "GetNoteColumns() without the underscore");
        ExpectEqual(catalog.GetNote("HELLO"), "greeting", "GetNote()");
        ExpectEqual(catalog.GetNote("HELLO", "menu"), "Main menu button", "GetNote() with a context");
        ExpectEqual(catalog.GetNote("APPLE", "", "max_length"), "12", "GetNote() with a column");
        ExpectEqual(catalog.GetNote("MISSING"), "", "no note: empty string");
        ExpectEqual(catalog.GetText("HELLO", "de"), "Hallo", "notes do not change the texts");
        // Expected error: the reserved column.
        catalog.SetNote("HELLO", "x", "", "plural");
        ExpectEqual(catalog.GetNote("HELLO", "", "plural"), "", "the reserved column plural is refused");
        ExpectEqual(catalog.GetNoteColumns().Length, 2, "the refused column is not added");
    }

    private void CheckNoteFormats()
    {
        var catalog = NotesCatalog();
        for (int i = 0; i < (int)ResourceTextSerializer.Format.Max; i++)
        {
            var format = (ResourceTextSerializer.Format)i;
            string formatClass = FormatClasses[i];
            if (!ClassDB.ClassExists(formatClass))
            {
                GD.Print($"  SKIP  notes in {formatClass} (module disabled)");
                continue;
            }
            var copy = new TextLocalizationCatalog();
            ExpectEqual(copy.LoadFromText(catalog.ToText(format), format), Error.Ok, "notes round trip: LoadFromText " + formatClass);
            ExpectEqual(copy.GetNote("HELLO"), "greeting", $"{formatClass}: note");
            ExpectEqual(copy.GetNote("HELLO", "menu"), "Main menu button", $"{formatClass}: note with a context");
            ExpectEqual(copy.GetNote("APPLE", "", "max_length"), "12", $"{formatClass}: max_length column");
            ExpectEqual(copy.GetNote("LINE"), "Two lines\nkeep it short", $"{formatClass}: note with a line break");
            ExpectEqual(copy.GetText("HELLO", "de", "menu"), "Hallo (Menü)", $"{formatClass}: texts are kept");
        }
        string[] header = catalog.ToText(ResourceTextSerializer.Format.Csv).Split('\n')[0].Trim().Split(',');
        int notes = System.Array.IndexOf(header, "_notes");
        Expect(notes > System.Array.IndexOf(header, "de") && System.Array.IndexOf(header, "_max_length") > notes, "CSV header: note columns after the locales: " + string.Join(",", header));
    }

    private void CheckNoteFiles()
    {
        var catalog = NotesCatalog();
        string lcatPath = DataDir + "/notes.lcat";
        ExpectEqual(catalog.SaveToFile(lcatPath), Error.Ok, "save .lcat");
        var fromLcat = new TextLocalizationCatalog();
        ExpectEqual(fromLcat.LoadFromFile(lcatPath), Error.Ok, "load .lcat");
        Expect(fromLcat.GetNoteColumns().Length == 0, ".lcat has no notes");
        ExpectEqual(fromLcat.GetNote("HELLO"), "", ".lcat: GetNote() is empty");
        ExpectEqual(fromLcat.GetText("HELLO", "de"), "Hallo", ".lcat: texts are kept");

        string csvPath = DataDir + "/notes.csv";
        ExpectEqual(catalog.SaveToFile(csvPath), Error.Ok, "save .csv");
        var loaded = ResourceLoader.Load(csvPath, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore) as TextLocalizationCatalog;
        Expect(loaded != null, "ResourceLoader loads the csv catalog");
        if (loaded != null)
        {
            ExpectEqual(loaded.GetText("HELLO", "de", "menu"), "Hallo (Menü)", "ResourceLoader: texts are loaded");
            Expect(loaded.GetNoteColumns().Length == 0, "ResourceLoader outside the editor skips the notes");
        }
        var toolCatalog = new TextLocalizationCatalog();
        ExpectEqual(toolCatalog.LoadFromFile(csvPath), Error.Ok, "LoadFromFile(csv)");
        ExpectEqual(toolCatalog.GetNote("HELLO", "menu"), "Main menu button", "LoadFromFile() keeps the notes");
    }
}
