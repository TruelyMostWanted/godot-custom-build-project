using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.LocalizationCatalogs;

/// <summary>
/// Full API test of Localization Catalogs (LocalizationCatalog, TextLocalizationCatalog, ResourceLocalizationCatalog).
/// Same tests as scripts/gd/Test.gd. Notes, entries, Validate() and ToPo() are tested in localization-editor,
/// the channel in locale-channels.
/// </summary>
public partial class Test : FeatureTest
{
    private const string PluralRule = "nplurals=2; plural=(n != 1);";
    // The rules of Godot's CSV translation importer: ?context, ?plural with a continuation row, ?pluralrule row,
    // \n unescaped, a continuation row without a plural key is ignored.
    private const string Csv = """
        keys,?context,?plural,en,de
        HELLO,,,Hello,Hallo
        HELLO,menu,,Hello (menu),Hallo (Menü)
        ?pluralrule,,,,nplurals=2; plural=(n != 1);
        APPLE,,APPLES,%d apple,%d Apfel
        ,,,%d apples,%d Äpfel
        LINE,,,One\nTwo,Eins\nZwei
        ,,,ignored row,
        BYE,,,Bye,

        """;
    private static readonly ResourceTextSerializer.Format[] Formats =
    [
        ResourceTextSerializer.Format.Json,
        ResourceTextSerializer.Format.Ini,
        ResourceTextSerializer.Format.Csv,
        ResourceTextSerializer.Format.Xml,
        ResourceTextSerializer.Format.Toml,
        ResourceTextSerializer.Format.Yaml,
    ];
    private static readonly string[] Extensions = ["lcat", "csv", "json", "yaml", "yml", "toml", "ini", "xml"];
    private const string LazyPath = "user://localization_catalogs_lazy_cs.lcat";

    private readonly List<string> _loadedLocales = [];

    protected override Task RunTests()
    {
        TestCsvRules();
        TestEditing();
        TestTextFormats();
        TestFiles();
        TestLazyLoading();
        TestTranslations();
        TestResourceCatalog();
        TestSampleResources();
        return Task.CompletedTask;
    }

    private static TextLocalizationCatalog MakeCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromText(Csv, ResourceTextSerializer.Format.Csv);
        return catalog;
    }

    private static Godot.Collections.Dictionary DataOf(GodotObject catalog) => catalog.Get("data").AsGodotDictionary();

    private void ExpectTexts(TextLocalizationCatalog catalog, string label)
    {
        ExpectEqual(catalog.GetText("HELLO", "de"), "Hallo", $"{label}: GetText");
        ExpectEqual(catalog.GetText("HELLO", "de", "menu"), "Hallo (Menü)", $"{label}: GetText with a context");
        ExpectEqual(catalog.GetText("LINE", "en"), "One\nTwo", $"{label}: \\n is a line break");
        ExpectEqual(catalog.GetPluralText("APPLE", "de", 1), "%d Apfel", $"{label}: GetPluralText for 1");
        ExpectEqual(catalog.GetPluralText("APPLE", "de", 5), "%d Äpfel", $"{label}: GetPluralText for 5");
        ExpectEqual(catalog.GetPluralRule("de"), PluralRule, $"{label}: GetPluralRule");
    }

    private void TestCsvRules()
    {
        var catalog = new TextLocalizationCatalog();
        Error error = catalog.LoadFromText(Csv, ResourceTextSerializer.Format.Csv);
        ExpectEqual(error, Error.Ok, "LoadFromText(CSV) returns OK");
        ExpectEqual(catalog.KeyCount, 5, "KeyCount counts every key and context");
        Expect(catalog.Locales.SequenceEqual(["en", "de"]), "Locales keep the column order");
        ExpectTexts(catalog, "CSV");
        ExpectEqual(catalog.GetPluralKey("APPLE"), "APPLES", "GetPluralKey");
        Expect(catalog.GetPluralTexts("APPLE", "de").SequenceEqual(["%d Apfel", "%d Äpfel"]), "GetPluralTexts");
        ExpectEqual(catalog.GetPluralForm("de", 1), 0, "GetPluralForm for 1 is form 0");
        ExpectEqual(catalog.GetPluralForm("de", 5), 1, "GetPluralForm for 5 is form 1");
        ExpectEqual(catalog.GetText("BYE", "de"), "", "a missing translation is empty");
        ExpectEqual(catalog.GetTranslatedCount("de"), 4, "GetTranslatedCount");
        Expect(catalog.GetMissingRows("de").SequenceEqual([4]), "GetMissingRows");
        ExpectEqual(catalog.GetKey(1).ToString(), "HELLO", "GetKey of row 1");
        ExpectEqual(catalog.GetKeyContext(1).ToString(), "menu", "GetKeyContext of row 1");
        ExpectEqual(catalog.FindKey("HELLO", "menu"), 1, "FindKey with a context");
        ExpectEqual(catalog.FindKey("NOBODY"), -1, "FindKey of an unknown key is -1");
        Expect(catalog.HasKey("HELLO", "menu"), "HasKey with a context");
        Expect(!catalog.HasKey("HELLO", "other"), "HasKey with another context is false");
        Expect(catalog.HasLocale("de"), "HasLocale finds de");
        Expect(!catalog.HasLocale("fr"), "HasLocale does not find fr");
        ExpectEqual(catalog.GetText("HELLO", "de_DE"), "", "a catalog does not fall back from de_DE to de");
    }

    private void TestEditing()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.AddLocale("en");
        Expect(catalog.Locales.SequenceEqual(["en"]), "AddLocale adds a column");
        int row = catalog.AddKey("A");
        ExpectEqual(row, 0, "AddKey returns the new row");
        ExpectEqual(catalog.AddKey("A"), 0, "AddKey of an existing key returns its row");
        catalog.SetText("A", "en", "a");
        catalog.SetText("B", "en", "b");
        catalog.SetText("C", "de", "c");
        catalog.SetText("B", "en", "a much longer text");
        ExpectEqual(catalog.GetText("A", "en"), "a", "SetText: the cell before a grown cell is kept");
        ExpectEqual(catalog.GetText("B", "en"), "a much longer text", "SetText replaces a cell");
        ExpectEqual(catalog.GetText("C", "de"), "c", "SetText adds the locale de");
        catalog.RemoveKey("A");
        ExpectEqual(catalog.KeyCount, 2, "RemoveKey removes a key");
        ExpectEqual(catalog.FindKey("B"), 0, "RemoveKey: the rows below move up");
        catalog.RemoveLocale("en");
        Expect(catalog.Locales.SequenceEqual(["de"]), "RemoveLocale removes a column");
        for (int i = 0; i < 1000; i++)
        {
            catalog.SetText($"KEY_{i}", "de", i.ToString());
        }
        ExpectEqual(catalog.GetText("KEY_777", "de"), "777", "the key index grows with many keys");
        ExpectEqual(catalog.GetText("C", "de"), "c", "the key index keeps the old keys");

        catalog.SetPluralKey("ENEMY", "ENEMIES");
        catalog.SetPluralRule("de", PluralRule);
        catalog.SetPluralTexts("ENEMY", "de", ["%d Gegner", "%d Gegner (viele)"]);
        ExpectEqual(catalog.GetPluralKey("ENEMY"), "ENEMIES", "SetPluralKey");
        ExpectEqual(catalog.GetPluralRule("de"), PluralRule, "SetPluralRule");
        ExpectEqual(catalog.GetPluralText("ENEMY", "de", 2), "%d Gegner (viele)", "SetPluralTexts");

        catalog.Clear();
        ExpectEqual(catalog.KeyCount, 0, "Clear removes every key");
        Expect(catalog.Locales.Length == 0, "Clear removes every locale");
    }

    private void TestTextFormats()
    {
        TextLocalizationCatalog catalog = MakeCatalog();
        Godot.Collections.Dictionary data = DataOf(catalog);
        foreach (ResourceTextSerializer.Format format in Formats)
        {
            string text = catalog.ToText(format);
            var copy = new TextLocalizationCatalog();
            Error error = copy.LoadFromText(text, format);
            ExpectEqual(error, Error.Ok, $"{format}: LoadFromText of ToText returns OK");
            Expect(DataOf(copy).RecursiveEqual(data), $"{format}: the data round-trips");
            ExpectTexts(copy, format.ToString());
        }
        var duplicate = (TextLocalizationCatalog)catalog.Duplicate();
        ExpectEqual(duplicate.GetText("HELLO", "de", "menu"), "Hallo (Menü)", "Duplicate keeps the data");
    }

    private void TestFiles()
    {
        Expect(LocalizationCatalog.GetFileExtensions().SequenceEqual(Extensions), "GetFileExtensions");
        TextLocalizationCatalog catalog = MakeCatalog();
        Godot.Collections.Dictionary data = DataOf(catalog);
        foreach (string extension in Extensions)
        {
            string path = "user://localization_catalogs_round_trip_cs." + extension;
            Error saved = catalog.SaveToFile(path);
            ExpectEqual(saved, Error.Ok, $".{extension}: SaveToFile returns OK");
            var copy = new TextLocalizationCatalog();
            Error loaded = copy.LoadFromFile(path);
            ExpectEqual(loaded, Error.Ok, $".{extension}: LoadFromFile returns OK");
            Expect(DataOf(copy).RecursiveEqual(data), $".{extension}: the data round-trips");
            DirAccess.RemoveAbsolute(path);
        }
        const string jsonPath = "user://localization_catalogs_hint_cs.json";
        catalog.SaveToFile(jsonPath);
        var hinted = ResourceLoader.Load<TextLocalizationCatalog>(jsonPath, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(hinted.GetText("HELLO", "de"), "Hallo", "Load() of a text format with the catalog type hint");
        DirAccess.RemoveAbsolute(jsonPath);
    }

    private void TestLazyLoading()
    {
        TextLocalizationCatalog source = MakeCatalog();
        source.SaveToFile(LazyPath);
        var catalog = ResourceLoader.Load<TextLocalizationCatalog>(LazyPath, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(catalog.KeyCount, 5, ".lcat: Load() reads the keys");
        Expect(catalog.Locales.SequenceEqual(["en", "de"]), ".lcat: Load() reads the locale table");
        Expect(!catalog.IsLocaleLoaded("en"), ".lcat: en is not loaded after Load()");
        Expect(!catalog.IsLocaleLoaded("de"), ".lcat: de is not loaded after Load()");
        ExpectEqual(catalog.GetTranslatedCount("de"), 4, ".lcat: GetTranslatedCount comes from the locale table");
        Expect(!catalog.IsLocaleLoaded("de"), ".lcat: GetTranslatedCount does not load de");

        catalog.LocaleLoaded += OnLocaleLoaded;
        ExpectEqual(catalog.GetText("HELLO", "de", "menu"), "Hallo (Menü)", ".lcat: GetText loads the locale on first use");
        Expect(_loadedLocales.SequenceEqual(["de"]), ".lcat: LocaleLoaded is emitted with de");
        Expect(!catalog.IsLocaleLoaded("en"), ".lcat: en stays unloaded");
        catalog.GetText("HELLO", "de");
        ExpectEqual(_loadedLocales.Count, 1, ".lcat: no second LocaleLoaded for a loaded locale");
        catalog.UnloadLocale("de");
        Expect(!catalog.IsLocaleLoaded("de"), "UnloadLocale frees the locale");
        Error error = catalog.LoadLocale("de");
        ExpectEqual(error, Error.Ok, "LoadLocale returns OK");
        Expect(catalog.IsLocaleLoaded("de"), "LoadLocale loads the locale");
        Expect(_loadedLocales.SequenceEqual(["de", "de"]), "LoadLocale emits LocaleLoaded");
        catalog.LocaleLoaded -= OnLocaleLoaded;

        catalog.SetText("NEW", "en", "new");
        Expect(catalog.IsLocaleLoaded("en"), ".lcat: a change loads every locale");
        ExpectEqual(catalog.GetText("BYE", "en"), "Bye", ".lcat: a change keeps the other cells");

        // The engine prints an error: the file holds a text catalog.
        var wrong = new ResourceLocalizationCatalog();
        Error refused = wrong.LoadFromFile(LazyPath);
        Expect(refused != Error.Ok, "a ResourceLocalizationCatalog refuses a text catalog file");
        DirAccess.RemoveAbsolute(LazyPath);
    }

    private void TestTranslations()
    {
        TextLocalizationCatalog catalog = MakeCatalog();
        Godot.Collections.Array<Translation> translations = catalog.ToTranslations();
        ExpectEqual(translations.Count, 2, "ToTranslations returns one Translation per locale");
        Translation german = translations[1];
        ExpectEqual(german.Locale, "de", "ToTranslations: the locale");
        ExpectEqual(german.GetMessage("HELLO").ToString(), "Hallo", "ToTranslations: a message");
        ExpectEqual(german.GetMessage("HELLO", "menu").ToString(), "Hallo (Menü)", "ToTranslations: a message with a context");
        ExpectEqual(german.GetPluralMessage("APPLE", "APPLES", 5).ToString(), "%d Äpfel", "ToTranslations: a plural message");
        ExpectEqual(german.PluralRulesOverride, PluralRule, "ToTranslations: the plural rule");

        var back = new TextLocalizationCatalog();
        foreach (Translation translation in translations)
        {
            back.AddTranslation(translation);
        }
        ExpectEqual(back.GetText("HELLO", "de", "menu"), "Hallo (Menü)", "AddTranslation keeps contexts");
        ExpectEqual(back.GetPluralTexts("APPLE", "de").Length, 2, "AddTranslation keeps the plural forms");
        ExpectEqual(back.GetPluralRule("de"), PluralRule, "AddTranslation keeps the plural rule");
        ExpectEqual(back.GetText("LINE", "en"), "One\nTwo", "AddTranslation keeps line breaks");
    }

    private void TestResourceCatalog()
    {
        var remaps = new Godot.Collections.Dictionary
        {
            ["res://icon.png"] = new[] { "res://icon_de.png:de", "res://icon_fr.png:fr" },
            ["res://music.ogg"] = new[] { "res://music_de.ogg:de" },
        };
        var catalog = new ResourceLocalizationCatalog();
        catalog.AddTranslationRemaps(remaps);
        ExpectEqual(catalog.GetLocalizedPath("res://icon.png", "fr"), "res://icon_fr.png", "AddTranslationRemaps: a localized path");
        ExpectEqual(catalog.GetLocalizedPath("res://music.ogg", "fr"), "", "a missing localized path is empty");
        Expect(catalog.ToTranslationRemaps().RecursiveEqual(remaps), "ToTranslationRemaps returns the added remaps");
        catalog.SetLocalizedPath("res://music.ogg", "fr", "res://music_fr.ogg");
        ExpectEqual(catalog.GetLocalizedPath("res://music.ogg", "fr"), "res://music_fr.ogg", "SetLocalizedPath");
        catalog.SetExpectedType("res://icon.png", "Texture2D");
        ExpectEqual(catalog.GetExpectedType("res://icon.png").ToString(), "Texture2D", "SetExpectedType");

        Godot.Collections.Dictionary data = DataOf(catalog);
        foreach (ResourceTextSerializer.Format format in Formats)
        {
            var copy = new ResourceLocalizationCatalog();
            Error error = copy.LoadFromText(catalog.ToText(format), format);
            ExpectEqual(error, Error.Ok, $"resource catalog {format}: LoadFromText returns OK");
            Expect(DataOf(copy).RecursiveEqual(data), $"resource catalog {format}: the data round-trips");
            ExpectEqual(copy.GetExpectedType("res://icon.png").ToString(), "Texture2D", $"resource catalog {format}: the expected type");
        }
    }

    private void TestSampleResources()
    {
        var texts = GD.Load<TextLocalizationCatalog>("res://localization-catalogs/resources/ui_texts.tres");
        ExpectEqual(texts.GetText("menu.play", "de"), "Spielen", "the sample ui_texts.tres loads");
        var files = GD.Load<ResourceLocalizationCatalog>("res://localization-catalogs/resources/voice_files.tres");
        ExpectEqual(files.GetLocalizedPath("res://voice/intro.ogg", "de"), "res://voice/de/intro.ogg", "the sample voice_files.tres loads");
    }

    private void OnLocaleLoaded(string locale)
    {
        _loadedLocales.Add(locale);
    }
}
