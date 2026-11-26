using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue341FeatureLocalizationCatalog;

/// <summary>C# checks of 341-feature-localization-catalog. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue341FeatureLocalizationCatalogCheck : FeatureCheckCs
{
    private const string PluralRule = "nplurals=2; plural=(n != 1);";
    private static readonly string[] FormatClasses = ["JSON", "INI", "CSV", "XML", "TOML", "YAML"];
    private static readonly string[] Locales = ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"];
    private const int KeyCount = 20000;
    private const int CallsPerRun = 4000;
    private const int Runs = 5;

    private readonly List<string> _loadedLog = [];

    protected override string CheckName => "341-feature-localization-catalog";

    protected override Task RunChecks()
    {
        Expect(ClassDB.ClassExists("LocalizationCatalog"), "ClassDB knows LocalizationCatalog");
        Expect(ClassDB.ClassExists("TextLocalizationCatalog"), "ClassDB knows TextLocalizationCatalog");
        Expect(ClassDB.ClassExists("ResourceLocalizationCatalog"), "ClassDB knows ResourceLocalizationCatalog");
        Expect(!ClassDB.ClassExists("LocalizationCatalogList"), "LocalizationCatalogList is removed (#357): catalog sources are FilesManifest lists");
        if (!ClassDB.ClassExists("TextLocalizationCatalog"))
        {
            return Task.CompletedTask;
        }
        CheckCsvRules();
        CheckTextRoundTrips();
        CheckFileRoundTrips();
        CheckBinary();
        CheckTranslations();
        CheckResourceCatalog();
        Measure();
        return Task.CompletedTask;
    }

    private static bool SameData(GodotObject a, GodotObject b)
        => a.Get("data").AsGodotDictionary().RecursiveEqual(b.Get("data").AsGodotDictionary());

    /// <summary>The rules of the CSV translation importer: ?context, ?plural + continuation rows, _ columns ignored,
    /// ?pluralrule row, translations unescaped.</summary>
    private TextLocalizationCatalog MakeTextCatalog()
    {
        string csv = "keys,?context,?plural,_notes,en,de\n"
            + "HELLO,,,greeting,Hello,Hallo\n"
            + "HELLO,menu,,,Hello (menu),Hallo (Menü)\n"
            + "?pluralrule,,,,,nplurals=2; plural=(n != 1);\n"
            + "APPLE,,APPLES,,%d apple,%d Apfel\n"
            + ",,,,%d apples,%d Äpfel\n"
            + "LINE,,,,One\\nTwo,Eins\\nZwei\n"
            + ",,,,ignored row,\n"
            + "BYE,,,,Bye,\n";
        var catalog = new TextLocalizationCatalog();
        Error err = catalog.LoadFromText(csv, ResourceTextSerializer.Format.Csv);
        Expect(err == Error.Ok, "LoadFromText(CSV) returns OK");
        return catalog;
    }

    private void CheckValues(TextLocalizationCatalog catalog, string label)
    {
        Expect(catalog.GetText("HELLO", "de") == "Hallo", $"{label}: get_text");
        Expect(catalog.GetText("HELLO", "de", "menu") == "Hallo (Menü)", $"{label}: get_text with context");
        Expect(catalog.GetText("LINE", "en") == "One\nTwo", $"{label}: \\n escape is a newline");
        Expect(catalog.GetPluralText("APPLE", "de", 1) == "%d Apfel", $"{label}: get_plural_text n=1");
        Expect(catalog.GetPluralText("APPLE", "de", 5) == "%d Äpfel", $"{label}: get_plural_text n=5");
        Expect(catalog.GetPluralRule("de") == PluralRule, $"{label}: plural rule");
    }

    private void CheckCsvRules()
    {
        var catalog = MakeTextCatalog();
        ExpectEqual(catalog.GetKeyCount(), 5, "key_count");
        Expect(catalog.GetLocales().SequenceEqual(["en", "de"]), "locales");
        CheckValues(catalog, "csv");
        ExpectEqual(catalog.GetPluralKey("APPLE").ToString(), "APPLES", "plural key");
        Expect(catalog.GetPluralTexts("APPLE", "de").SequenceEqual(["%d Apfel", "%d Äpfel"]), "plural texts");
        Expect(catalog.GetText("BYE", "de").Length == 0, "missing translation is empty");
        ExpectEqual(catalog.GetTranslatedCount("de"), 4, "translated count de");
        Expect(catalog.GetMissingRows("de").SequenceEqual([4]), "missing rows de");
        ExpectEqual(catalog.GetKey(1).ToString(), "HELLO", "key of row 1");
        ExpectEqual(catalog.GetKeyContext(1).ToString(), "menu", "context of row 1");
        ExpectEqual(catalog.FindKey("HELLO", "menu"), 1, "find_key with context");
        Expect(!catalog.HasKey("HELLO", "other"), "has_key with other context is false");
        ExpectEqual(catalog.GetPluralForm("de", 1), 0, "plural form n=1");
        ExpectEqual(catalog.GetPluralForm("de", 5), 1, "plural form n=5");

        // Keys, locales and cells.
        var small = new TextLocalizationCatalog();
        small.SetText("A", "en", "a");
        small.SetText("B", "en", "b");
        small.SetText("C", "de", "c");
        small.SetText("B", "en", "a much longer text");
        Expect(small.GetText("A", "en") == "a" && small.GetText("B", "en") == "a much longer text" && small.GetText("C", "de") == "c", "cells grow in the middle");
        small.RemoveKey("A");
        ExpectEqual(small.GetKeyCount(), 2, "remove_key");
        ExpectEqual(small.FindKey("B"), 0, "rows move up");
        small.RemoveLocale("en");
        Expect(small.GetLocales().SequenceEqual(["de"]), "remove_locale");
        for (int i = 0; i < 1000; i++)
        {
            small.SetText($"KEY_{i}", "de", i.ToString());
        }
        Expect(small.GetText("KEY_777", "de") == "777" && small.GetText("C", "de") == "c", "index grows with many keys");
    }

    private void CheckTextRoundTrips()
    {
        var catalog = MakeTextCatalog();
        Expect(catalog.Get("data").VariantType == Variant.Type.Dictionary, "data property is a Dictionary");
        for (int f = 0; f < FormatClasses.Length; f++)
        {
            string formatName = FormatClasses[f];
            if (!ClassDB.ClassExists(formatName))
            {
                GD.Print($"  NOTE  {formatName} module is not in this build: format skipped");
                continue;
            }
            var format = (ResourceTextSerializer.Format)f;
            string text = catalog.ToText(format);
            Expect(text.Length > 0, $"{formatName}: to_text is not empty");
            var copy = new TextLocalizationCatalog();
            Error err = copy.LoadFromText(text, format);
            Expect(err == Error.Ok, $"{formatName}: load_from_text returns OK");
            Expect(SameData(copy, catalog), $"{formatName}: data round trip is equal");
            CheckValues(copy, formatName);
            ExpectEqual(copy.GetPluralTexts("APPLE", "en").Length, 2, $"{formatName}: plural texts of en");
        }

        var duplicate = catalog.Duplicate() as TextLocalizationCatalog;
        Expect(duplicate != null && duplicate.GetText("HELLO", "de", "menu") == "Hallo (Menü)", "duplicate keeps the data");
    }

    private void CheckFileRoundTrips()
    {
        var catalog = MakeTextCatalog();
        var extensions = new List<string> { "lcat" };
        for (int f = 0; f < FormatClasses.Length; f++)
        {
            if (ClassDB.ClassExists(FormatClasses[f]))
            {
                extensions.Add(FormatClasses[f].ToLowerInvariant());
            }
        }
        foreach (string extension in extensions)
        {
            string path = "user://catalog_341_round_trip_cs." + extension;
            Expect(catalog.SaveToFile(path) == Error.Ok, $"save_to_file .{extension}");
            var copy = new TextLocalizationCatalog();
            Expect(copy.LoadFromFile(path) == Error.Ok, $"load_from_file .{extension}");
            Expect(SameData(copy, catalog), $"file .{extension}: data is equal");
            Expect(copy.GetText("HELLO", "de", "menu") == "Hallo (Menü)", $"file .{extension}: get_text");
            DirAccess.RemoveAbsolute(path);
        }
    }

    private void OnLocaleLoaded(string locale) => _loadedLog.Add(locale);

    private void CheckBinary()
    {
        var catalog = MakeTextCatalog();
        const string path = "user://catalog_341_binary_cs.lcat";
        Expect(catalog.SaveToFile(path) == Error.Ok, "save .lcat");
        var loaded = ResourceLoader.Load<TextLocalizationCatalog>(path, "", ResourceLoader.CacheMode.Ignore);
        Expect(loaded != null, "load() returns a TextLocalizationCatalog");
        if (loaded == null)
        {
            return;
        }
        ExpectEqual(loaded.GetKeyCount(), 5, "binary: key_count");
        Expect(loaded.GetLocales().SequenceEqual(["en", "de"]), "binary: locales");
        Expect(!loaded.IsLocaleLoaded("en") && !loaded.IsLocaleLoaded("de"), "binary: no locale loaded after load()");
        ExpectEqual(loaded.GetTranslatedCount("de"), 4, "binary: translated count without loading");
        Expect(!loaded.IsLocaleLoaded("de"), "binary: get_translated_count does not load");

        _loadedLog.Clear();
        loaded.LocaleLoaded += OnLocaleLoaded;
        Expect(loaded.GetText("HELLO", "de", "menu") == "Hallo (Menü)", "binary: get_text loads on first use");
        Expect(_loadedLog.SequenceEqual(["de"]), "binary: locale_loaded emitted once with de");
        Expect(loaded.IsLocaleLoaded("de") && !loaded.IsLocaleLoaded("en"), "binary: only de is loaded");
        ExpectEqual(loaded.GetPluralTexts("APPLE", "de").Length, 2, "binary: plural forms of de");
        loaded.GetText("HELLO", "de");
        ExpectEqual(_loadedLog.Count, 1, "binary: no second signal for a loaded locale");

        loaded.UnloadLocale("de");
        Expect(!loaded.IsLocaleLoaded("de"), "binary: unload_locale");
        Expect(loaded.LoadLocale("de") == Error.Ok, "binary: load_locale returns OK");
        Expect(loaded.IsLocaleLoaded("de"), "binary: load_locale loads");
        Expect(SameData(loaded, catalog), "binary: data equals the source");
        loaded.LocaleLoaded -= OnLocaleLoaded;

        // A changed catalog no longer reads from the file.
        loaded.SetText("NEW", "en", "new");
        Expect(loaded.IsLocaleLoaded("en"), "binary: a change loads every locale");
        Expect(loaded.GetText("BYE", "en") == "Bye", "binary: other cells are kept");

        // The wrong kind is refused (the engine prints an error).
        var wrong = new ResourceLocalizationCatalog();
        Expect(wrong.LoadFromFile(path) != Error.Ok, "binary: ResourceLocalizationCatalog refuses a text catalog");
        DirAccess.RemoveAbsolute(path);
    }

    private void CheckTranslations()
    {
        var catalog = MakeTextCatalog();
        var translations = catalog.ToTranslations();
        ExpectEqual(translations.Count, 2, "to_translations: one per locale");
        if (translations.Count != 2)
        {
            return;
        }
        Translation german = translations[1];
        ExpectEqual(german.Locale, "de", "translation locale");
        ExpectEqual(german.GetMessage("HELLO").ToString(), "Hallo", "translation message");
        ExpectEqual(german.GetMessage("HELLO", "menu").ToString(), "Hallo (Menü)", "translation message with context");
        ExpectEqual(german.GetPluralMessage("APPLE", "APPLES", 5).ToString(), "%d Äpfel", "translation plural message");
        ExpectEqual(german.PluralRulesOverride, PluralRule, "translation plural rule override");

        var back = new TextLocalizationCatalog();
        foreach (Translation translation in translations)
        {
            back.AddTranslation(translation);
        }
        Expect(back.GetText("HELLO", "de", "menu") == "Hallo (Menü)", "add_translation: context");
        ExpectEqual(back.GetPluralTexts("APPLE", "de").Length, 2, "add_translation: plural forms");
        ExpectEqual(back.GetPluralRule("de"), PluralRule, "add_translation: plural rule");
        Expect(back.GetText("LINE", "en") == "One\nTwo", "add_translation: newline");
    }

    private void CheckResourceCatalog()
    {
        var remaps = new Godot.Collections.Dictionary
        {
            ["res://icon.png"] = new[] { "res://icon_de.png:de", "res://icon_fr.png:fr" },
            ["res://music.ogg"] = new[] { "res://music_de.ogg:de" },
        };
        var catalog = new ResourceLocalizationCatalog();
        catalog.AddTranslationRemaps(remaps);
        ExpectEqual(catalog.GetLocalizedPath("res://icon.png", "fr"), "res://icon_fr.png", "localized path");
        Expect(catalog.GetLocalizedPath("res://music.ogg", "fr").Length == 0, "missing localized path is empty");
        Expect(catalog.ToTranslationRemaps().RecursiveEqual(remaps), "to_translation_remaps equals the added remaps");

        catalog.SetExpectedType("res://icon.png", "Texture2D");
        ExpectEqual(catalog.GetExpectedType("res://icon.png").ToString(), "Texture2D", "expected type");
        var formats = new[] { ResourceTextSerializer.Format.Csv, ResourceTextSerializer.Format.Json, ResourceTextSerializer.Format.Xml };
        foreach (var format in formats)
        {
            var copy = new ResourceLocalizationCatalog();
            Error err = copy.LoadFromText(catalog.ToText(format), format);
            Expect(err == Error.Ok, $"resource catalog: load_from_text format {format}");
            Expect(SameData(copy, catalog), $"resource catalog: data round trip format {format}");
            ExpectEqual(copy.GetExpectedType("res://icon.png").ToString(), "Texture2D", $"resource catalog: expected type format {format}");
        }
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    private static double BenchGetText(TextLocalizationCatalog catalog, StringName[] keys, string locale)
    {
        var samples = new List<double>();
        for (int run = 0; run <= Runs; run++)
        {
            long t0 = Stopwatch.GetTimestamp();
            foreach (StringName key in keys)
            {
                if (catalog.GetText(key, locale).Length == 0)
                {
                    return -1.0;
                }
            }
            double elapsedNs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1_000_000.0;
            if (run > 0) // Run 0 is the warm-up.
            {
                samples.Add(elapsedNs / keys.Length);
            }
        }
        return Median(samples);
    }

    /// <summary>The cost of the interop call itself: a cheap bound method.</summary>
    private static double BenchOverhead(TextLocalizationCatalog catalog, StringName[] keys)
    {
        var samples = new List<double>();
        for (int run = 0; run <= Runs; run++)
        {
            long t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < keys.Length; i++)
            {
                if (catalog.GetKeyCount() < 0)
                {
                    return -1.0;
                }
            }
            double elapsedNs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1_000_000.0;
            if (run > 0)
            {
                samples.Add(elapsedNs / keys.Length);
            }
        }
        return Median(samples);
    }

    private static double StaticMemory() => Performance.GetMonitor(Performance.Monitor.MemoryStatic);

    private static double ElapsedMs(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    private void Measure()
    {
        GD.Print($"  -- measurement: {KeyCount} keys x {Locales.Length} locales");
        const string path = "user://catalog_341_bench_cs.lcat";
        int messageCount = KeyCount * Locales.Length;

        long tGen = Stopwatch.GetTimestamp();
        var source = new TextLocalizationCatalog();
        foreach (string locale in Locales)
        {
            source.AddLocale(locale);
        }
        for (int i = 0; i < KeyCount; i++)
        {
            string key = $"UI_KEY_{i:D5}";
            foreach (string locale in Locales)
            {
                source.SetText(key, locale, $"{locale} text {i}: the quick brown fox jumps over the lazy dog");
            }
        }
        double genMs = ElapsedMs(tGen);
        long tSave = Stopwatch.GetTimestamp();
        Error saveErr = source.SaveToFile(path);
        double saveMs = ElapsedMs(tSave);
        Expect(saveErr == Error.Ok, $"bench: save {messageCount} messages as .lcat");
        long fileSize = 0;
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Read))
        {
            if (file != null)
            {
                fileSize = (long)file.GetLength();
            }
        }
        GD.Print($"  INFO  generated in {genMs:F0} ms, saved in {saveMs:F0} ms, file {fileSize / 1048576.0:F2} MB ({(double)fileSize / messageCount:F1} bytes per message)");

        // (a) load() + every locale.
        double mem0 = StaticMemory();
        long t0 = Stopwatch.GetTimestamp();
        var allLoaded = ResourceLoader.Load<TextLocalizationCatalog>(path, "", ResourceLoader.CacheMode.Ignore);
        if (!Expect(allLoaded != null, "bench: load() the .lcat"))
        {
            return;
        }
        double loadOnlyMs = ElapsedMs(t0);
        double memKeys = StaticMemory() - mem0;
        foreach (string locale in Locales)
        {
            allLoaded.LoadLocale(locale);
        }
        double allMs = ElapsedMs(t0);
        double allMem = StaticMemory() - mem0;
        ExpectEqual(allLoaded.GetKeyCount(), KeyCount, "bench: key_count after load");
        Expect(allLoaded.GetText("UI_KEY_19999", "nl") == "nl text 19999: the quick brown fox jumps over the lazy dog", "bench: last cell is correct");

        // (b) en + de only.
        double mem1 = StaticMemory();
        long t1 = Stopwatch.GetTimestamp();
        var twoLoaded = ResourceLoader.Load<TextLocalizationCatalog>(path, "", ResourceLoader.CacheMode.Ignore);
        if (!Expect(twoLoaded != null, "bench: load() a second time"))
        {
            return;
        }
        twoLoaded.LoadLocale("en");
        twoLoaded.LoadLocale("de");
        double twoMs = ElapsedMs(t1);
        double twoMem = StaticMemory() - mem1;
        Expect(!twoLoaded.IsLocaleLoaded("nl"), "bench: nl stays unloaded");

        GD.Print($"  INFO  load() only: {loadOnlyMs:F0} ms, {memKeys / 1048576.0:F2} MB (keys, locale table)");
        GD.Print($"  INFO  load() + 12 locales: {allMs:F0} ms, {allMem / 1048576.0:F2} MB, {allMem / messageCount:F1} bytes per message");
        GD.Print($"  INFO  load() + en + de: {twoMs:F0} ms, {twoMem / 1048576.0:F2} MB, {twoMem / (KeyCount * 2):F1} bytes per message");

        // (d) ns per call.
        var keys = new StringName[CallsPerRun];
        for (int i = 0; i < CallsPerRun; i++)
        {
            keys[i] = new StringName($"UI_KEY_{(i * 5) % KeyCount:D5}");
        }
        double nsDe = BenchGetText(allLoaded, keys, "de");
        double nsNl = BenchGetText(allLoaded, keys, "nl");
        double overhead = BenchOverhead(allLoaded, keys);
        GD.Print($"  INFO  GetText: de {nsDe:F0} ns, nl {nsNl:F0} ns per call (interop call overhead, GetKeyCount: {overhead:F0} ns)");
        GD.Print("  INFO  vs #340 Translation (12 locales): 2484 ms / 80 MB / ~380 B per message; en+de only: 424 ms / 13.9 MB; OptimizedTranslation ~72 B per message");
        GD.Print($"  INFO  now: 12 locales {allMs:F0} ms / {allMem / 1048576.0:F1} MB / {allMem / messageCount:F0} B per message; en+de {twoMs:F0} ms / {twoMem / 1048576.0:F1} MB");

        Expect(nsDe > 0.0 && nsDe < 20000.0, "bench: GetText takes less than 20 us per call (sanity)");
        Expect(allMs < 30000.0, "bench: load of 12 locales takes less than 30 s (sanity)");
        if (allMem > 0.0) // MEMORY_STATIC is 0 in release builds.
        {
            double bytesPerMessage = allMem / messageCount;
            Expect(bytesPerMessage < 120.0, $"bench: less than 120 bytes per message ({bytesPerMessage:F1})");
        }
        else
        {
            GD.Print("  NOTE  MEMORY_STATIC is 0 (release build): memory check skipped");
        }
        DirAccess.RemoveAbsolute(path);
    }
}
