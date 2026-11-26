using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.Issue340TaskTranslationBenchmark;

/// <summary>
/// Benchmark of Godot's translation system (#340), C# side. Measures the same as scripts/gd/check.gd and uses the same
/// data sets (scripts/gd/generator.gd). Default: quick run with sanity checks. Full run: pass <c>-- --bench-full</c>.
/// Results: user://bench_340/csharp_(quick|full).csv and a table in the log.
/// </summary>
public partial class Issue340TaskTranslationBenchmarkCheck : FeatureCheckCs
{
    private const string GeneratorPath = "res://340-task-translation-benchmark/scripts/gd/generator.gd";
    private const string OutDir = "user://bench_340";
    private const int LabelCount = 1000;

    private readonly bool _full = OS.GetCmdlineUserArgs().Contains("--bench-full");
    private int Calls => _full ? 4000 : 1000;
    private int Runs => _full ? 5 : 3;
    private readonly List<string> _csv = ["language,set,keys,locales,files,translations,variant,metric,value,unit"];
    private readonly List<string> _table = [];

    protected override string CheckName => "340-task-translation-benchmark";

    protected override async Task RunChecks()
    {
        var start = Stopwatch.GetTimestamp();
        GD.Print($"  cpu: {OS.GetProcessorName()} ({OS.GetProcessorCount()} threads), debug build: {OS.IsDebugBuild()}, editor: {OS.HasFeature("editor")}");
        GD.Print($"  mode: {(_full ? "full" : "quick")}, {Calls} calls per run, median of {Runs} runs after 1 warm-up run");
        TranslationServer.Clear(); // the project's own translations (hello_de/hello_fr) would add to every loop

        var generator = GD.Load<GDScript>(GeneratorPath);
        var sets = generator.Call("get_sets", _full);
        generator.Call("ensure_sets", sets);
        BenchCompareLocales();
        CheckDuplicateKeys();
        _table.Add("| set | translations | load ms | static MB | B/msg | hit | hit nl (last) | lang fallback | miss→fallback | miss→key | context | tr_n | switch 1k Labels ms |");
        _table.Add("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var dataSet in sets.AsGodotArray<GodotDictionary>())
        {
            await BenchSet(new DataSet(dataSet));
        }

        DirAccess.MakeDirRecursiveAbsolute(OutDir);
        var csvPath = $"{OutDir}/csharp_{(_full ? "full" : "quick")}.csv";
        using (var file = FileAccess.Open(csvPath, FileAccess.ModeFlags.Write))
        {
            file.StoreString(string.Join("\n", _csv) + "\n");
        }
        GD.Print(string.Join("\n", _table));
        GD.Print($"  csv: {ProjectSettings.GlobalizePath(csvPath)}, total {Stopwatch.GetElapsedTime(start).TotalSeconds:F1} s");
    }

    private sealed class DataSet(GodotDictionary d)
    {
        public readonly string Name = (string)d["name"];
        public readonly int Keys = (int)d["keys"];
        public readonly int Locales = (int)d["locales"];
        public readonly int Files = (int)d["files"];
        public readonly string Variant = (string)d["variant"];
        public readonly string[] Paths = (string[])d["paths"];
        public readonly string[] NeededPaths = (string[])d["needed_paths"];
    }

    private static string KeyName(int i) => $"UI_KEY_{i:D5}";
    private static string Text(string locale, int i) => $"{locale} text {i}: the quick brown fox jumps over the lazy dog";

    private static int HitIndex(int j, int keys)
    {
        var i = (j * 7919) % keys;
        return i % 10 == 9 ? i - 1 : i;
    }

    // Bottleneck 2: compare_locales() builds an "a|b" String for every non-equal locale pair.
    private void BenchCompareLocales()
    {
        var n = Calls * 10;
        foreach (var (a, b) in new[] { ("de", "de"), ("de", "fr"), ("de_DE", "de") })
        {
            var samples = new List<double>();
            for (var r = 0; r <= Runs; r++)
            {
                var t0 = Stopwatch.GetTimestamp();
                for (var j = 0; j < n; j++)
                {
                    TranslationServer.CompareLocales(a, b);
                }
                if (r > 0)
                {
                    samples.Add(Ns(t0, n));
                }
            }
            var ns = Median(samples);
            AddRow(null, $"compare_locales_{a}_{b}_ns", ns, "ns");
            GD.Print($"  compare_locales({a}, {b}): {ns:F1} ns");
        }
    }

    // Bottleneck 8: which translation wins when two translations of one locale have the same key.
    private void CheckDuplicateKeys()
    {
        var domain = TranslationServer.GetOrAddDomain("bench_340_duplicates");
        Translation a = Single("A"), b = Single("B"), c = Single("C");
        var results = new Dictionary<string, string>();
        domain.SetLocaleOverride("de");
        domain.AddTranslation(a);
        domain.AddTranslation(b);
        results["exact_ab"] = domain.Translate("DUP");
        domain.Clear();
        domain.AddTranslation(b);
        domain.AddTranslation(a);
        results["exact_ba"] = domain.Translate("DUP");
        domain.SetLocaleOverride("de_DE"); // language match only: no early exit, the last match wins
        results["lang_ba"] = domain.Translate("DUP");
        domain.SetLocaleOverride("de");
        domain.Clear();
        domain.AddTranslation(a);
        domain.AddTranslation(b);
        domain.AddTranslation(c);
        domain.RemoveTranslation(a);
        domain.AddTranslation(a);
        results["exact_abc_remove_a_add_a"] = domain.Translate("DUP");
        TranslationServer.RemoveDomain("bench_340_duplicates");
        foreach (var (key, value) in results)
        {
            AddRow(null, "duplicate_" + key, value, "winner");
        }
        GD.Print($"  duplicate key winners: {string.Join(", ", results.Select(kv => $"{kv.Key}={kv.Value}"))}");
        ExpectEqual(results["exact_ab"], "A", "exact locale: the first added translation wins (insertion order, not random)");
        ExpectEqual(results["exact_ba"], "B", "exact locale, other order: the first added translation wins");
        ExpectEqual(results["lang_ba"], "A", "language match (de_DE -> de): the last added translation wins");
        ExpectEqual(results["exact_abc_remove_a_add_a"], "C", "remove_translation() reorders: the last one moves to the front");
    }

    private static Translation Single(string value)
    {
        var t = new Translation { Locale = "de" };
        t.AddMessage("DUP", value);
        return t;
    }

    private async Task BenchSet(DataSet set)
    {
        var keys = set.Keys;
        TranslationServer.SetLocale("de");

        // Bottleneck 5: load time and memory for all locales vs only the needed ones (active + fallback).
        if (set.Locales > 2)
        {
            var memNeeded = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
            var tNeeded = Stopwatch.GetTimestamp();
            var needed = Load(set.NeededPaths);
            AddRow(set, "load_needed_ms", Stopwatch.GetElapsedTime(tNeeded).TotalMilliseconds, "ms");
            AddRow(set, "static_needed_mb", (Performance.GetMonitor(Performance.Monitor.MemoryStatic) - memNeeded) / 1048576.0, "MB");
            Unload(needed);
        }
        var mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        var t0 = Stopwatch.GetTimestamp();
        var loaded = Load(set.Paths);
        var loadMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        var memMb = (Performance.GetMonitor(Performance.Monitor.MemoryStatic) - mem0) / 1048576.0;
        var messages = MessageCount(set);
        AddRow(set, "load_ms", loadMs, "ms");
        AddRow(set, "static_mb", memMb, "MB");
        AddRow(set, "messages", messages, "count");
        AddRow(set, "bytes_per_message", memMb * 1048576.0 / messages, "B");

        // Lookup keys spread over the whole key range, the same sequence as in GDScript.
        var hit = new StringName[Calls];
        var hitStrings = new string[Calls];
        var fallback = new StringName[Calls];
        var missing = new StringName[Calls];
        var plural = new StringName[Calls];
        for (var j = 0; j < Calls; j++)
        {
            hitStrings[j] = KeyName(HitIndex(j, keys));
            hit[j] = new StringName(hitStrings[j]);
            fallback[j] = new StringName(KeyName(((j * 7919) % (keys / 10)) * 10 + 9));
            missing[j] = new StringName($"NO_KEY_{j:D5}");
            plural[j] = new StringName($"PL_{((j * 7919) % (keys / 4)) * 4:D5}");
        }

        ExpectEqual(Tr(hit[1]), Text("de", HitIndex(1, keys)), $"{set.Name}: hit returns the de text");
        ExpectEqual(Tr(fallback[1]), Text("en", (7919 % (keys / 10)) * 10 + 9), $"{set.Name}: key missing in de returns the en text");
        ExpectEqual(Tr(missing[1]), "NO_KEY_00001", $"{set.Name}: unknown key returns the key");

        var r = new Dictionary<string, double>();
        var mainDomain = TranslationServer.GetOrAddDomain("");
        mainDomain.Enabled = false;
        r["baseline_disabled"] = Measure(Kind.Tr, hit);
        mainDomain.Enabled = true;
        r["hit_active"] = Measure(Kind.Tr, hit);
        r["hit_string_key"] = MeasureStrings(hitStrings);
        r["hit_server_translate"] = Measure(Kind.Server, hit);
        r["miss_fallback_locale"] = Measure(Kind.Tr, fallback);
        r["miss_key"] = Measure(Kind.Tr, missing);
        if (set.Variant == "ctx_plural")
        {
            ExpectEqual(Tr(hit[1], "ui"), Text("de", HitIndex(1, keys)) + " (ui)", $"{set.Name}: context hit");
            ExpectEqual(TrN(plural[0], plural[0], 5), "de plural 0 form 1", $"{set.Name}: tr_n(5) returns plural form 1");
            r["hit_context"] = Measure(Kind.TrContext, hit);
            r["tr_n"] = Measure(Kind.TrN, plural);
        }
        TranslationServer.SetLocale("de_DE");
        ExpectEqual(Tr(hit[1]), Text("de", HitIndex(1, keys)), $"{set.Name}: de_DE finds the de text");
        r["hit_lang_fallback"] = Measure(Kind.Tr, hit);
        if (set.Locales > 2)
        {
            // Bottleneck 1: the cost of a hit depends on where the locale's translations are in the domain's list ("nl" is added last).
            TranslationServer.SetLocale("nl");
            ExpectEqual(Tr(hit[1]), Text("nl", HitIndex(1, keys)), $"{set.Name}: nl (added last) finds the nl text");
            r["hit_last_locale"] = Measure(Kind.Tr, hit);
        }
        TranslationServer.SetLocale("de");
        foreach (var (metric, value) in r)
        {
            AddRow(set, metric + "_ns", value, "ns");
        }

        // Re-translation of 1k Labels (auto_translate) on a locale switch: the set_locale() call itself, and with the next frame.
        var box = new VBoxContainer();
        AddChild(box);
        for (var j = 0; j < LabelCount; j++)
        {
            box.AddChild(new Label { Text = hitStrings[j % hitStrings.Length] });
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var switchSamples = new List<double>();
        var frameSamples = new List<double>();
        for (var s = 0; s <= Runs; s++)
        {
            var ts = Stopwatch.GetTimestamp();
            TranslationServer.SetLocale(s % 2 == 0 ? "en" : "de");
            var switchMs = Stopwatch.GetElapsedTime(ts).TotalMilliseconds;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (s > 0)
            {
                switchSamples.Add(switchMs);
                frameSamples.Add(Stopwatch.GetElapsedTime(ts).TotalMilliseconds);
            }
        }
        box.Free();
        var switchMedian = Median(switchSamples);
        AddRow(set, "locale_switch_1k_labels_ms", switchMedian, "ms");
        AddRow(set, "locale_switch_1k_labels_frame_ms", Median(frameSamples), "ms");

        Unload(loaded);
        foreach (var key in hit.Concat(fallback).Concat(missing).Concat(plural))
        {
            key.Dispose();
        }

        string Opt(string metric) => r.TryGetValue(metric, out var v) ? v.ToString("F0", CultureInfo.InvariantCulture) : "-";
        _table.Add(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2:F1} | {3:F2} | {4:F0} | {5:F0} | {6} | {7:F0} | {8:F0} | {9:F0} | {10} | {11} | {12:F2} |",
            set.Name, set.Paths.Length, loadMs, memMb, memMb * 1048576.0 / messages, r["hit_active"], Opt("hit_last_locale"),
            r["hit_lang_fallback"], r["miss_fallback_locale"], r["miss_key"], Opt("hit_context"), Opt("tr_n"), switchMedian));
        GD.Print($"  {set.Name} done");
    }

    private static List<Translation> Load(string[] paths)
    {
        var result = new List<Translation>();
        foreach (var path in paths)
        {
            var t = ResourceLoader.Load<Translation>(path, "", ResourceLoader.CacheMode.Ignore);
            TranslationServer.AddTranslation(t);
            result.Add(t);
        }
        return result;
    }

    // Loaded with CacheMode.Ignore, so these objects are not shared: release them now instead of at the next GC,
    // so the memory of one data set does not linger into the next.
    private static void Unload(List<Translation> translations)
    {
        foreach (var t in translations)
        {
            TranslationServer.RemoveTranslation(t);
            t.Dispose();
        }
        translations.Clear();
    }

    private static int MessageCount(DataSet set)
    {
        var count = set.Keys + (set.Keys - set.Keys / 10) * (set.Locales - 1);
        return set.Variant == "ctx_plural" ? count * 2 + set.Keys / 4 * set.Locales : count;
    }

    private enum Kind { Tr, TrContext, TrN, Server }

    // ns per call: median over Runs runs after one warm-up run.
    private double Measure(Kind kind, StringName[] keys)
    {
        StringName context = "ui";
        var samples = new List<double>();
        for (var r = 0; r <= Runs; r++)
        {
            var t0 = Stopwatch.GetTimestamp();
            switch (kind)
            {
                case Kind.Tr:
                    foreach (var k in keys) Tr(k);
                    break;
                case Kind.TrContext:
                    foreach (var k in keys) Tr(k, context);
                    break;
                case Kind.TrN:
                    foreach (var k in keys) TrN(k, k, 5);
                    break;
                case Kind.Server:
                    foreach (var k in keys) TranslationServer.Translate(k);
                    break;
            }
            if (r > 0)
            {
                samples.Add(Ns(t0, keys.Length));
            }
        }
        return Median(samples);
    }

    // Keys as C# strings, the usual way to call Tr(): every call converts the string to a new StringName.
    private double MeasureStrings(string[] keys)
    {
        var samples = new List<double>();
        for (var r = 0; r <= Runs; r++)
        {
            var t0 = Stopwatch.GetTimestamp();
            foreach (var k in keys) Tr(k);
            if (r > 0)
            {
                samples.Add(Ns(t0, keys.Length));
            }
        }
        return Median(samples);
    }

    private static double Ns(long t0, int calls) => Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1e6 / calls;

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    private void AddRow(DataSet set, string metric, object value, string unit)
    {
        var text = value is double d ? d.ToString("F3", CultureInfo.InvariantCulture) : value.ToString();
        _csv.Add(set == null
            ? $"csharp,micro,,,,,,{metric},{text},{unit}"
            : $"csharp,{set.Name},{set.Keys},{set.Locales},{set.Files},{set.Paths.Length},{set.Variant},{metric},{text},{unit}");
    }
}
