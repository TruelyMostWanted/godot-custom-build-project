using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.Issue343FeatureTextLocalizationServer;

/// <summary>
/// Before/after benchmark of the translation path (#343), C# side: groups 1 and 2 (code paths and cases, including string literal vs
/// cached StringName vs key id), 4 and 5 (memory, references, load). Groups 3 (UI) and 6 (parity) are GDScript only (scripts/gd/bench.gd).
/// BEFORE = Godot translations of the #340 data sets in TranslationServer, AFTER = the same data as .lcat catalogs in TextLocalizationServer.
/// Quick: <c>-- --bench-quick</c>, full: <c>-- --bench-full</c>; options <c>--bench-groups=lookup,load</c>, <c>--bench-set=&lt;substring&gt;</c>.
/// Results: log lines "343 bench &lt;set&gt; &lt;before|after&gt; &lt;metric&gt; &lt;value&gt; &lt;unit&gt;" and user://bench_343/results_cs.csv.
/// </summary>
public partial class Issue343FeatureTextLocalizationServerBench : FeatureCheckCs
{
    private const string GeneratorPath = "res://340-task-translation-benchmark/scripts/gd/generator.gd";
    private const string DataPath = "res://343-feature-text-localization-server/scripts/gd/bench_data.gd";
    private const string OutDir = "user://bench_343";
    private const int ModPriority = 100;

    private readonly string[] _args = OS.GetCmdlineUserArgs();
    private readonly bool _full;
    private readonly int _calls;
    private readonly int _runs;
    private readonly List<string> _csv = ["language,set,keys,locales,files,translations,variant,path,metric,value,unit"];
    private string[] _groups = [];
    private string _setFilter = "";
    private GDScript _generator;
    private GDScript _data;

    protected override string CheckName => "343-text-localization-bench";

    public Issue343FeatureTextLocalizationServerBench()
    {
        _full = _args.Contains("--bench-full");
        _calls = _full ? 4000 : 1000;
        _runs = _full ? 5 : 3;
    }

    private sealed class DataSet(GodotDictionary d)
    {
        public readonly GodotDictionary Raw = d;
        public readonly string Name = (string)d["name"];
        public readonly int Keys = (int)d["keys"];
        public readonly int Locales = (int)d["locales"];
        public readonly int Files = (int)d["files"];
        public readonly string Variant = (string)d["variant"];
        public readonly string[] Paths = (string[])d["paths"];
        public readonly string[] NeededPaths = (string[])d["needed_paths"];
    }

    private sealed record Case(string Name, string Locale, StringName[] Keys, StringName Context, string[] Before, string[] After);

    private static string KeyName(int i) => $"UI_KEY_{i:D5}";
    private static string Text(string locale, int i) => $"{locale} text {i}: the quick brown fox jumps over the lazy dog";
    private static string ModText(int i) => $"MOD de text {i}";
    private static bool IsMod(int i) => i % 10 == 3;

    private static int HitIndex(int j, int keys)
    {
        var i = (j * 7919) % keys;
        return i % 10 == 9 ? i - 1 : i;
    }

    private string ArgValue(string prefix) => _args.Where(a => a.StartsWith(prefix)).Select(a => a.Substring(prefix.Length)).FirstOrDefault() ?? "";

    private bool InGroup(string name) => _groups.Length == 0 || _groups.Contains(name);

    protected override async Task RunChecks()
    {
        var start = Stopwatch.GetTimestamp();
        _setFilter = ArgValue("--bench-set=");
        var groupsArg = ArgValue("--bench-groups=");
        _groups = groupsArg == "" ? [] : groupsArg.Split(',');
        GD.Print($"  cpu: {OS.GetProcessorName()} ({OS.GetProcessorCount()} threads), debug build: {OS.IsDebugBuild()}, editor: {OS.HasFeature("editor")}");
        GD.Print($"  mode: {(_full ? "full" : "quick")}, {_calls} calls per run, median of {_runs} runs after 1 warm-up run");
        Reset();

        _generator = GD.Load<GDScript>(GeneratorPath);
        _data = GD.Load<GDScript>(DataPath);
        var sets = _generator.Call("get_sets", _full).AsGodotArray<GodotDictionary>()
            .Where(d => _setFilter == "" || ((string)d["name"]).Contains(_setFilter)).Select(d => new DataSet(d)).ToList();
        var raw = new Godot.Collections.Array<GodotDictionary>(sets.Select(s => s.Raw));
        _generator.Call("ensure_sets", raw);
        foreach (var set in sets)
        {
            ExpectEqual(_data.Call("ensure_lcat", set.Raw).AsInt32(), 0, $"{set.Name}: converted to catalogs");
        }
        foreach (var set in sets)
        {
            if (InGroup("lookup"))
            {
                await BenchLookup(set);
            }
            if (InGroup("load"))
            {
                await BenchLoad(set);
            }
            GD.Print($"  {set.Name} done");
        }

        Reset();
        DirAccess.MakeDirRecursiveAbsolute(OutDir);
        var csvPath = $"{OutDir}/results_cs.csv";
        using (var file = FileAccess.Open(csvPath, FileAccess.ModeFlags.Write))
        {
            file.StoreString(string.Join("\n", _csv) + "\n");
        }
        GD.Print($"  csv: {ProjectSettings.GlobalizePath(csvPath)}, total {Stopwatch.GetElapsedTime(start).TotalSeconds:F1} s");
    }

    // ---- Set up BEFORE (Godot translations) and AFTER (catalogs); only one path is active at a time ----

    private static void Reset()
    {
        TextLocalizationServer.Singleton.ClearCatalogs(); // removes the adapter translation from the main domain
        TranslationServer.Clear();
        TranslationServer.SetLocale("de");
    }

    private static List<Translation> LoadRes(string[] paths)
    {
        var result = new List<Translation>();
        foreach (var path in paths)
        {
            var translation = ResourceLoader.Load<Translation>(path, "", ResourceLoader.CacheMode.Ignore);
            TranslationServer.AddTranslation(translation);
            result.Add(translation);
        }
        return result;
    }

    private List<Translation> UseBefore(DataSet set)
    {
        Reset();
        return LoadRes(set.Paths);
    }

    private int UseAfter(DataSet set)
    {
        Reset();
        var list = new LocalizationCatalogList();
        foreach (var path in _data.Call("lcat_paths", set.Raw).AsStringArray())
        {
            list.AddEntry(path);
        }
        return TextLocalizationServer.Singleton.AddCatalogList(list);
    }

    private static void SetLocale(bool after, string locale)
    {
        if (after)
        {
            LocalizationServer.SetLocale(locale);
        }
        else
        {
            TranslationServer.SetLocale(locale);
        }
    }

    private void AddRow(DataSet set, string path, string metric, object value, string unit)
    {
        var text = value is double d ? d.ToString("F3", CultureInfo.InvariantCulture) : Format(value);
        _csv.Add($"csharp,{set.Name},{set.Keys},{set.Locales},{set.Files},{set.Paths.Length},{set.Variant},{path},{metric},{text},{unit}");
        GD.Print($"343 bench {set.Name} {path} {metric} {text} {unit}");
    }

    private static string Format(object value) => string.Format(CultureInfo.InvariantCulture, "{0}", value);

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    // ---- Groups 1 and 2: code paths and cases (ns per call, median of runs after one warm-up run) ----

    private async Task BenchLookup(DataSet set)
    {
        var keys = set.Keys;
        var hitStrings = new string[_calls];
        var hit = new StringName[_calls];
        var fallback = new StringName[_calls];
        var missing = new StringName[_calls];
        var plural = new StringName[_calls];
        for (var j = 0; j < _calls; j++)
        {
            hitStrings[j] = KeyName(HitIndex(j, keys));
            hit[j] = new StringName(hitStrings[j]);
            fallback[j] = new StringName(KeyName(((j * 7919) % (keys / 10)) * 10 + 9));
            missing[j] = new StringName($"NO_KEY_{j:D5}");
            plural[j] = new StringName($"PL_{((j * 7919) % (keys / 4)) * 4:D5}");
        }
        var ctxPlural = set.Variant == "ctx_plural";
        var ui = new StringName("ui");
        var none = new StringName("");
        var cases = new List<Case>
        {
            new("hit", "de", hit, none, ["tr", "tr_str", "atr", "ts"], ["tls", "tls_str", "tls_id", "tls_en", "tls_args"]),
            new("lang_fallback", "de_DE", hit, none, ["tr", "atr", "ts"], ["tls", "tls_id"]),
            new("fallback_locale", "de", fallback, none, ["tr", "atr", "ts"], ["tls", "tls_id"]),
            new("miss", "de", missing, none, ["tr", "atr", "ts"], ["tls"]),
        };
        if (ctxPlural)
        {
            cases.Add(new("context", "de", hit, ui, ["tr_ctx", "ts_ctx"], ["tls_ctx", "tls_id"]));
            cases.Add(new("plural", "de", plural, none, ["tr_n", "ts_plural"], ["tls_plural", "tls_id_n"]));
        }

        foreach (var mode in new[] { "before", "after" })
        {
            var after = mode == "after";
            List<Translation> loaded = null;
            if (after)
            {
                ExpectEqual(UseAfter(set), set.Files, $"{set.Name}: all catalogs added");
            }
            else
            {
                loaded = UseBefore(set);
            }
            var label = $"{set.Name} {mode}";
            SetLocale(after, "de");
            ExpectEqual(Tr(hit[1]), Text("de", HitIndex(1, keys)), label + ": hit returns the de text");
            ExpectEqual(Tr(fallback[1]), Text("en", (7919 % (keys / 10)) * 10 + 9), label + ": key missing in de returns the en text");
            ExpectEqual(Tr(missing[1]), "NO_KEY_00001", label + ": unknown key returns the key");
            if (ctxPlural)
            {
                ExpectEqual(Tr(hit[1], ui), Text("de", HitIndex(1, keys)) + " (ui)", label + ": context hit");
                ExpectEqual(TrN(plural[0], plural[0], 5), "de plural 0 form 1", label + ": tr_n(5) returns plural form 1");
            }
            SetLocale(after, "de_DE");
            ExpectEqual(Tr(hit[1]), Text("de", HitIndex(1, keys)), label + ": de_DE finds the de text");
            SetLocale(after, "de");

            // Cost of the call itself: main domain disabled (the adapter is in the main domain in the AFTER path).
            var mainDomain = TranslationServer.GetOrAddDomain("");
            mainDomain.Enabled = false;
            AddRow(set, mode, "call_overhead_ns", Measure("tr", hit, null, hitStrings), "ns");
            mainDomain.Enabled = true;

            foreach (var c in cases)
            {
                SetLocale(after, c.Locale);
                var kinds = after ? c.Before.Concat(c.After).ToArray() : c.Before;
                var ids = after ? Ids(c.Keys, c.Context) : null;
                foreach (var kind in kinds)
                {
                    AddRow(set, mode, $"{c.Name}_{kind}_ns", Measure(kind, c.Keys, ids, hitStrings), "ns");
                }
            }
            SetLocale(after, "de");

            if (after)
            {
                BenchModLayer(set, hit, hitStrings);
            }
            if (loaded != null)
            {
                Reset();
                foreach (var t in loaded)
                {
                    t.Dispose();
                }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Reset();
        foreach (var key in hit.Concat(fallback).Concat(missing).Concat(plural))
        {
            key.Dispose();
        }
    }

    private static int[] Ids(StringName[] keys, StringName context)
    {
        var ids = new int[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            ids[i] = TextLocalizationServer.GetKeyId(keys[i], context);
        }
        return ids;
    }

    // AFTER only: a mod layer (priority 100) overrides every 10th key in "de". Hit cost and correctness with it.
    private void BenchModLayer(DataSet set, StringName[] hit, string[] hitStrings)
    {
        var mod = new TextLocalizationCatalog();
        var modKeys = 0;
        for (var i = 0; i < set.Keys; i++)
        {
            if (IsMod(i))
            {
                mod.SetText(KeyName(i), "de", ModText(i));
                modKeys++;
            }
        }
        TextLocalizationServer.Singleton.AddCatalog(mod, ModPriority);
        LocalizationServer.SetLocale("de");
        int wrongTranslate = 0, wrongTr = 0, wrongEn = 0;
        for (var i = 0; i < set.Keys; i++)
        {
            StringName key = KeyName(i);
            var expected = IsMod(i) ? ModText(i) : i % 10 == 9 ? Text("en", i) : Text("de", i);
            wrongTranslate += TextLocalizationServer.Translate(key) == expected ? 0 : 1;
            wrongTr += Tr(key) == expected ? 0 : 1;
            wrongEn += TextLocalizationServer.TranslateForLocale("en", key) == Text("en", i) ? 0 : 1;
        }
        AddRow(set, "after", "mod_keys", modKeys, "count");
        AddRow(set, "after", "mod_wrong_translate", wrongTranslate, "count");
        AddRow(set, "after", "mod_wrong_tr", wrongTr, "count");
        AddRow(set, "after", "mod_wrong_en_untouched", wrongEn, "count");
        Expect(wrongTranslate == 0 && wrongTr == 0 && wrongEn == 0, $"{set.Name} after: mod layer overrides exactly its keys");
        var ids = Ids(hit, new StringName(""));
        AddRow(set, "after", "mod_hit_tr_ns", Measure("tr", hit, null, hitStrings), "ns");
        AddRow(set, "after", "mod_hit_tls_ns", Measure("tls", hit, null, hitStrings), "ns");
        AddRow(set, "after", "mod_hit_tls_id_ns", Measure("tls_id", hit, ids, hitStrings), "ns");
        TextLocalizationServer.Singleton.RemoveCatalog(mod);
    }

    // ns per call. Kinds as in scripts/gd/bench.gd; additionally tr_str / tls_str use C# string literals (a StringName conversion per call).
    private double Measure(string kind, StringName[] keys, int[] ids, string[] strings)
    {
        var ui = new StringName("ui");
        var none = new StringName("");
        var args = new GodotDictionary { { "count", 3 }, { "name", "x" } };
        var calls = keys.Length;
        var samples = new List<double>();
        for (var r = 0; r <= _runs; r++)
        {
            var t0 = Stopwatch.GetTimestamp();
            switch (kind)
            {
                case "tr":
                    foreach (var k in keys) Tr(k);
                    break;
                case "tr_str":
                    foreach (var s in strings) Tr(s);
                    break;
                case "atr":
                    foreach (var k in keys) Atr(k);
                    break;
                case "ts":
                    foreach (var k in keys) TranslationServer.Translate(k);
                    break;
                case "tr_ctx":
                    foreach (var k in keys) Tr(k, ui);
                    break;
                case "ts_ctx":
                    foreach (var k in keys) TranslationServer.Translate(k, ui);
                    break;
                case "tr_n":
                    foreach (var k in keys) TrN(k, k, 5);
                    break;
                case "ts_plural":
                    foreach (var k in keys) TranslationServer.TranslatePlural(k, k, 5);
                    break;
                case "tls":
                    foreach (var k in keys) TextLocalizationServer.Translate(k);
                    break;
                case "tls_str":
                    foreach (var s in strings) TextLocalizationServer.Translate(s);
                    break;
                case "tls_ctx":
                    foreach (var k in keys) TextLocalizationServer.Translate(k, ui);
                    break;
                case "tls_plural":
                    foreach (var k in keys) TextLocalizationServer.Translate(k, none, 5);
                    break;
                case "tls_args":
                    foreach (var k in keys) TextLocalizationServer.Translate(k, none, -1, args);
                    break;
                case "tls_en":
                    foreach (var k in keys) TextLocalizationServer.TranslateForLocale("en", k);
                    break;
                case "tls_id":
                    foreach (var id in ids) TextLocalizationServer.TranslateId(id);
                    break;
                case "tls_id_n":
                    foreach (var id in ids) TextLocalizationServer.TranslateId(id, 5);
                    break;
            }
            if (r > 0)
            {
                samples.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1e6 / calls);
            }
        }
        return Median(samples);
    }

    // ---- Groups 4 and 5: load time, memory, references, locales loaded ----

    private static double[] Snap() =>
    [
        Performance.GetMonitor(Performance.Monitor.MemoryStatic), (double)OS.GetStaticMemoryPeakUsage(),
        Performance.GetMonitor(Performance.Monitor.ObjectCount), Performance.GetMonitor(Performance.Monitor.ObjectResourceCount),
    ];

    // Rows for the delta since snapshot s0. tag "" = after load, else a suffix (_after_switch, _all_locales).
    private void EmitDelta(DataSet set, string mode, string tag, double[] s0, int messages)
    {
        var now = Snap();
        var deltaMem = now[0] - s0[0];
        AddRow(set, mode, "static_mb" + tag, deltaMem / 1048576.0, "MB");
        AddRow(set, mode, "bytes_per_message" + tag, deltaMem / messages, "B");
        AddRow(set, mode, "peak_mb" + tag, (now[1] - s0[1]) / 1048576.0, "MB");
        AddRow(set, mode, "objects" + tag, (long)(now[2] - s0[2]), "count");
        AddRow(set, mode, "resources" + tag, (long)(now[3] - s0[3]), "count");
        AddRow(set, mode, "translation_objects" + tag, TranslationServer.GetOrAddDomain("").GetTranslations().Count, "count");
        AddRow(set, mode, "catalog_objects" + tag, TextLocalizationServer.Singleton.GetCatalogs().Count, "count");
    }

    private static int LoadedLocales()
    {
        var count = 0;
        foreach (LocalizationCatalog catalog in TextLocalizationServer.Singleton.GetCatalogs())
        {
            foreach (var locale in catalog.Locales)
            {
                count += catalog.IsLocaleLoaded(locale) ? 1 : 0;
            }
        }
        return count;
    }

    private static int MessageCount(DataSet set)
    {
        var count = set.Keys + (set.Keys - set.Keys / 10) * (set.Locales - 1);
        return set.Variant == "ctx_plural" ? count * 2 + set.Keys / 4 * set.Locales : count;
    }

    private async Task BenchLoad(DataSet set)
    {
        var messages = MessageCount(set);
        var totalLocales = set.Files * set.Locales;

        // BEFORE: only the needed files (en + de), then all files (what load_project_translations() does).
        if (set.Locales > 2)
        {
            Reset();
            var sNeeded = Snap();
            var tNeeded = Stopwatch.GetTimestamp();
            LoadRes(set.NeededPaths);
            AddRow(set, "before", "load_needed_ms", Stopwatch.GetElapsedTime(tNeeded).TotalMilliseconds, "ms");
            AddRow(set, "before", "static_needed_mb", (Snap()[0] - sNeeded[0]) / 1048576.0, "MB");
        }
        Reset();
        var s0 = Snap();
        var t0 = Stopwatch.GetTimestamp();
        var loaded = LoadRes(set.Paths);
        AddRow(set, "before", "load_ms", Stopwatch.GetElapsedTime(t0).TotalMilliseconds, "ms");
        AddRow(set, "before", "messages", messages, "count");
        AddRow(set, "before", "locales_loaded", totalLocales, "count");
        AddRow(set, "before", "locales_total", totalLocales, "count");
        EmitDelta(set, "before", "", s0, messages);
        AddRow(set, "before", "stringname_est", set.Variant == "compress" ? 0 : set.Keys + messages, "count");
        var tSwitch = Stopwatch.GetTimestamp();
        TranslationServer.SetLocale("fr");
        AddRow(set, "before", "switch_fr_sync_ms", Stopwatch.GetElapsedTime(tSwitch).TotalMilliseconds, "ms");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        EmitDelta(set, "before", "_after_switch", s0, messages);
        Reset();
        foreach (var t in loaded)
        {
            t.Dispose();
        }

        // AFTER: add_catalog_list reads the directory of every catalog; only the chain locales (en + de) are loaded.
        Reset();
        var a0 = Snap();
        var ta = Stopwatch.GetTimestamp();
        var added = UseAfterNoReset(set);
        AddRow(set, "after", "load_ms", Stopwatch.GetElapsedTime(ta).TotalMilliseconds, "ms");
        ExpectEqual(added, set.Files, $"{set.Name}: add_catalog_list added all catalogs");
        AddRow(set, "after", "messages", messages, "count");
        AddRow(set, "after", "locales_loaded", LoadedLocales(), "count");
        AddRow(set, "after", "locales_total", totalLocales, "count");
        AddRow(set, "after", "key_count", TextLocalizationServer.Singleton.GetKeyCount(), "count");
        AddRow(set, "after", "stringname_est", TextLocalizationServer.Singleton.GetKeyCount(), "count");
        EmitDelta(set, "after", "", a0, messages);
        var ts = Stopwatch.GetTimestamp();
        LocalizationServer.SetLocale("fr");
        AddRow(set, "after", "switch_fr_sync_ms", Stopwatch.GetElapsedTime(ts).TotalMilliseconds, "ms");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        AddRow(set, "after", "locales_loaded_after_switch", LoadedLocales(), "count");
        EmitDelta(set, "after", "_after_switch", a0, messages);
        LocalizationServer.SetLocale("de");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // Force every locale (LoadLocale), the worst case, equal to the BEFORE path.
        var tf = Stopwatch.GetTimestamp();
        foreach (LocalizationCatalog catalog in TextLocalizationServer.Singleton.GetCatalogs())
        {
            foreach (var locale in catalog.Locales)
            {
                catalog.LoadLocale(locale);
            }
        }
        AddRow(set, "after", "load_all_locales_ms", Stopwatch.GetElapsedTime(tf).TotalMilliseconds, "ms");
        AddRow(set, "after", "locales_loaded_all", LoadedLocales(), "count");
        EmitDelta(set, "after", "_all_locales", a0, messages);
        Reset();
    }

    // Like UseAfter, without the Reset() (the caller took its memory snapshot after its own Reset()).
    private int UseAfterNoReset(DataSet set)
    {
        var list = new LocalizationCatalogList();
        foreach (var path in _data.Call("lcat_paths", set.Raw).AsStringArray())
        {
            list.AddEntry(path);
        }
        return TextLocalizationServer.Singleton.AddCatalogList(list);
    }
}
