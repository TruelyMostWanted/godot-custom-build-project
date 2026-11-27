using System.Diagnostics;
using Godot;

namespace TruelyInteractive.LocalizationCatalogs;

/// <summary>
/// Benchmark of the localization data and servers: a catalog against Godot's Translation, lookups through
/// TextLocalizationServer and Tr(), localized paths. Prints measurements only; not run by run-checks.ps1.
/// Same measurements as scripts/gd/Benchmark.gd. Bigger runs: raise KeyCount (the wiki uses 20 000).
/// Run: godot --headless --path godot-project res://localization-catalogs/scenes/benchmark_cs.tscn
/// </summary>
public partial class Benchmark : Node
{
    private static readonly string[] Locales = ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"];
    private const int KeyCount = 2000;
    private const int Calls = 20000;
    private const int LookupIndex = 1000;
    private const string Path = "user://localization_catalogs_benchmark_cs.lcat";

    private readonly StringName[] _keys = new StringName[KeyCount];

    public override void _Ready()
    {
        GD.Print($"cpu: {OS.GetProcessorName()}, debug build: {OS.IsDebugBuild()}");
        GD.Print($"data: {KeyCount} keys x {Locales.Length} locales");
        for (int i = 0; i < KeyCount; i++)
        {
            _keys[i] = $"UI_KEY_{i:D5}";
        }
        BenchCatalogFile();
        BenchMemory();
        BenchLookup();
        BenchLocalizedPaths();
        DirAccess.RemoveAbsolute(Path);
        GetTree().Quit();
    }

    private static string Text(string locale, int i) => $"{locale} text {i}: the quick brown fox jumps over the lazy dog";

    private static double Mb(double bytes) => bytes / 1048576.0;

    private static double StaticMemory() => Performance.GetMonitor(Performance.Monitor.MemoryStatic);

    private static double NsPerCall(Stopwatch watch) => watch.Elapsed.TotalNanoseconds / Calls;

    private void BenchCatalogFile()
    {
        var watch = Stopwatch.StartNew();
        var catalog = new TextLocalizationCatalog();
        foreach (string locale in Locales)
        {
            catalog.AddLocale(locale);
        }
        for (int i = 0; i < KeyCount; i++)
        {
            foreach (string locale in Locales)
            {
                catalog.SetText(_keys[i], locale, Text(locale, i));
            }
        }
        GD.Print($"generate catalog: {watch.Elapsed.TotalMilliseconds:F1} ms");
        watch.Restart();
        catalog.SaveToFile(Path);
        GD.Print($"save .lcat: {watch.Elapsed.TotalMilliseconds:F1} ms");
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        ulong size = file.GetLength();
        GD.Print($".lcat file: {Mb(size):F2} MB, {(double)size / (KeyCount * Locales.Length):F1} bytes per message");
    }

    /// <summary>Catalog (.lcat, all locales and only en + de) against one Godot Translation per locale.</summary>
    private void BenchMemory()
    {
        int messages = KeyCount * Locales.Length;
        double memory = StaticMemory();
        var watch = Stopwatch.StartNew();
        var catalog = ResourceLoader.Load<TextLocalizationCatalog>(Path, "", ResourceLoader.CacheMode.Ignore);
        foreach (string locale in Locales)
        {
            catalog.LoadLocale(locale);
        }
        double used = StaticMemory() - memory;
        GD.Print($"catalog, load all locales: {watch.Elapsed.TotalMilliseconds:F1} ms, {Mb(used):F2} MB, {used / messages:F1} bytes per message");
        catalog.Dispose();

        memory = StaticMemory();
        watch.Restart();
        var two = ResourceLoader.Load<TextLocalizationCatalog>(Path, "", ResourceLoader.CacheMode.Ignore);
        two.LoadLocale("en");
        two.LoadLocale("de");
        used = StaticMemory() - memory;
        GD.Print($"catalog, load en + de: {watch.Elapsed.TotalMilliseconds:F1} ms, {Mb(used):F2} MB");
        two.Dispose();

        memory = StaticMemory();
        watch.Restart();
        var translations = new Translation[Locales.Length];
        for (int l = 0; l < Locales.Length; l++)
        {
            var translation = new Translation();
            translation.Locale = Locales[l];
            for (int i = 0; i < KeyCount; i++)
            {
                translation.AddMessage(_keys[i], Text(Locales[l], i));
            }
            translations[l] = translation;
        }
        used = StaticMemory() - memory;
        GD.Print($"Godot Translation, build all locales: {watch.Elapsed.TotalMilliseconds:F1} ms, {Mb(used):F2} MB, {used / messages:F1} bytes per message");
    }

    /// <summary>ns per call, including the C# interop call.</summary>
    private void BenchLookup()
    {
        var catalog = ResourceLoader.Load<TextLocalizationCatalog>(Path, "", ResourceLoader.CacheMode.Ignore);
        var translation = new Translation();
        translation.Locale = "de";
        for (int i = 0; i < KeyCount; i++)
        {
            translation.AddMessage(_keys[i], Text("de", i));
        }
        TextLocalizationServer.Singleton.AddCatalog(catalog);
        string previousLocale = TranslationServer.GetLocale();
        TranslationServer.SetLocale("de");
        StringName key = _keys[LookupIndex];
        int id = TextLocalizationServer.GetKeyId(key);

        var watch = Stopwatch.StartNew();
        for (int i = 0; i < Calls; i++)
        {
            catalog.GetKeyCount();
        }
        GD.Print($"call overhead (GetKeyCount): {NsPerCall(watch):F0} ns");
        watch.Restart();
        for (int i = 0; i < Calls; i++)
        {
            catalog.GetText(key, "de");
        }
        GD.Print($"TextLocalizationCatalog.GetText: {NsPerCall(watch):F0} ns");
        watch.Restart();
        for (int i = 0; i < Calls; i++)
        {
            translation.GetMessage(key);
        }
        GD.Print($"Translation.GetMessage: {NsPerCall(watch):F0} ns");
        watch.Restart();
        for (int i = 0; i < Calls; i++)
        {
            TextLocalizationServer.Translate(key);
        }
        GD.Print($"TextLocalizationServer.Translate: {NsPerCall(watch):F0} ns");
        watch.Restart();
        for (int i = 0; i < Calls; i++)
        {
            TextLocalizationServer.TranslateId(id);
        }
        GD.Print($"TextLocalizationServer.TranslateId: {NsPerCall(watch):F0} ns");
        watch.Restart();
        for (int i = 0; i < Calls; i++)
        {
            Tr(key);
        }
        GD.Print($"Tr() through the adapter: {NsPerCall(watch):F0} ns");

        TextLocalizationServer.Singleton.ClearCatalogs();
        TranslationServer.SetLocale(previousLocale);
    }

    private static void BenchLocalizedPaths()
    {
        var catalog = new ResourceLocalizationCatalog();
        for (int i = 0; i < KeyCount; i++)
        {
            catalog.SetLocalizedPath($"res://voice/line_{i}.ogg", "de", $"res://voice/de/line_{i}.ogg");
        }
        ResourceLocalizationServer.Singleton.AddCatalog(catalog);
        string path = $"res://voice/line_{LookupIndex}.ogg";
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < Calls; i++)
        {
            ResourceLocalizationServer.GetLocalizedPath(path, "de");
        }
        GD.Print($"ResourceLocalizationServer.GetLocalizedPath: {NsPerCall(watch):F0} ns");
        ResourceLocalizationServer.Singleton.ClearCatalogs();
    }
}
