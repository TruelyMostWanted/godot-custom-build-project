using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue05FeatureResourceTextFormats;

/// <summary>C# checks of 05-feature-resource-text-formats. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue05FeatureResourceTextFormatsCheck : FeatureCheckCs
{
    private const string Root = "user://05-feature-resource-text-formats/cs/";
    private const string Dir = "res://05-feature-resource-text-formats/scripts/gd/";

    protected override string CheckName => "05-feature-resource-text-formats";

    private static Resource New(string script) => (Resource)GD.Load<GDScript>(Dir + script).New();

    private static Resource MakeBus(string name, float volume, int mode)
    {
        Resource bus = New("audio_bus.gd");
        bus.Set("name", name);
        bus.Set("volume_db", volume);
        bus.Set("muted", volume < -10.0f);
        bus.Set("mode", mode);
        bus.Set("pan", new Vector2(0.25f, -0.5f));
        return bus;
    }

    private static Resource MakeSettings()
    {
        Resource s = New("audio_settings.gd");
        s.Set("master_volume", 0.75f);
        s.Set("title", "Cleo, \"the\" 2nd = ok");
        s.Set("quality", 2);
        s.Set("tint", new Color(0.5f, 0.25f, 1.0f, 0.5f));
        s.Set("main_bus", MakeBus("Master", -3.5f, 7));
        var buses = (Array)s.Get("buses");
        buses.Add(MakeBus("Music", -12.0f, 1));
        buses.Add(MakeBus("Sfx", 0.0f, 0));
        s.Set("tags", new[] { "a", "b c" });
        s.Set("cell", new Vector3I(1, -2, 3));
        return s;
    }

    private static bool SameBus(Variant a, Variant b)
    {
        var x = a.As<Resource>();
        var y = b.As<Resource>();
        return x != null && y != null && (string)x.Get("name") == (string)y.Get("name")
            && Mathf.IsEqualApprox((float)x.Get("volume_db"), (float)y.Get("volume_db"))
            && (bool)x.Get("muted") == (bool)y.Get("muted") && (int)x.Get("mode") == (int)y.Get("mode")
            && ((Vector2)x.Get("pan")).IsEqualApprox((Vector2)y.Get("pan"));
    }

    private void CheckSettings(Resource loaded, string label)
    {
        Resource e = MakeSettings();
        bool ok = loaded != null && loaded.GetScript().As<Script>()?.ResourcePath == Dir + "audio_settings.gd";
        Expect(ok, $"{label}: loads as Issue05GdAudioSettings");
        if (!ok)
        {
            return;
        }
        Expect(Mathf.IsEqualApprox((float)loaded.Get("master_volume"), 0.75f) && (string)loaded.Get("title") == (string)e.Get("title") && (int)loaded.Get("quality") == 2, $"{label}: float, string with quotes and enum");
        Expect(((Color)loaded.Get("tint")).IsEqualApprox((Color)e.Get("tint")) && (Vector3I)loaded.Get("cell") == new Vector3I(1, -2, 3) && string.Join("|", (string[])loaded.Get("tags")) == "a|b c", $"{label}: Color, Vector3i, PackedStringArray");
        Expect(SameBus(loaded.Get("main_bus"), e.Get("main_bus")), $"{label}: nested resource");
        var lb = (Array)loaded.Get("buses");
        var eb = (Array)e.Get("buses");
        Expect(lb.Count == 2 && SameBus(lb[0], eb[0]) && SameBus(lb[1], eb[1]), $"{label}: typed array of resources");
    }

    private void CheckSerializerOptions()
    {
        Resource settings = MakeSettings();
        var json = ResourceTextSerializer.Format.Json;
        var defaults = new SerializerOptions();
        Expect(defaults.Indent == "  " && defaults.EnumsAsNames && defaults.Defaults == null && defaults.IncludeTypeMarker && !defaults.SortKeys && !defaults.FullPrecisionFloats, "SerializerOptions(): common defaults");
        Expect(defaults.CsvDelimiter == "," && defaults.CsvHeaders && !defaults.IniPlainValues && defaults.XmlDeclaration, "SerializerOptions(): format defaults");
        ExpectEqual(ResourceTextSerializer.Serialize(settings, json, defaults), ResourceTextSerializer.Serialize(settings, json), "new() gives the same text as null (project settings)");

        Expect(ResourceTextSerializer.Serialize(settings, json, new SerializerOptions { Indent = "\t" }).Contains("\n\t\""), "Indent");

        string numbers = ResourceTextSerializer.Serialize(settings, json, new SerializerOptions { EnumsAsNames = false });
        Expect(numbers.Contains("\"quality\": 2"), "EnumsAsNames = false writes numbers");
        CheckSettings(ResourceTextSerializer.Deserialize(numbers, json), "EnumsAsNames = false");

        string changed = ResourceTextSerializer.Serialize(settings, json, new SerializerOptions { Defaults = MakeSettings() });
        Expect(!changed.Contains("master_volume") && changed.Contains("$type"), "Defaults: unchanged values are not written");

        string untyped = ResourceTextSerializer.Serialize(settings, json, new SerializerOptions { IncludeTypeMarker = false });
        Expect(!untyped.Contains("\"$type\""), "IncludeTypeMarker = false");
        CheckSettings(ResourceTextSerializer.Deserialize(untyped, json, "Issue05GdAudioSettings"), "IncludeTypeMarker = false with type hint");

        string plain = ResourceTextSerializer.Serialize(settings, json);
        string sorted = ResourceTextSerializer.Serialize(settings, json, new SerializerOptions { SortKeys = true });
        Expect(plain.IndexOf("\"title\"") < plain.IndexOf("\"quality\"") && sorted.IndexOf("\"quality\"") < sorted.IndexOf("\"title\""), "SortKeys");
        CheckSettings(ResourceTextSerializer.Deserialize(sorted, json), "SortKeys");

        Resource bus = MakeBus("Pan", 0.0f, 0);
        bus.Set("pan", new Vector2(0.1f, 0.5f));
        Expect(ResourceTextSerializer.Serialize(bus, json, new SerializerOptions { FullPrecisionFloats = true }).Contains("0.10000000149011612") && !ResourceTextSerializer.Serialize(bus, json).Contains("0.10000000149011612"), "FullPrecisionFloats");

        Resource list = New("bus_list.gd");
        var entries = (Array)list.Get("entries");
        entries.Add(MakeBus("Music", -12.0f, 1));
        var csvOptions = new SerializerOptions { CsvDelimiter = ";" };
        Expect(ResourceTextSerializer.Serialize(list, ResourceTextSerializer.Format.Csv, csvOptions).Contains("Music;"), "CsvDelimiter");
        csvOptions.CsvHeaders = false;
        string rows = ResourceTextSerializer.Serialize(list, ResourceTextSerializer.Format.Csv, csvOptions);
        Expect(rows.StartsWith("Music;"), "CsvHeaders = false: no header row");
        Resource back = ResourceTextSerializer.Deserialize(rows, ResourceTextSerializer.Format.Csv, "Issue05GdBusList", csvOptions);
        var backEntries = back?.Get("entries").As<Array>();
        Expect(backEntries != null && backEntries.Count == 1 && SameBus(backEntries[0], entries[0]), "CsvHeaders = false round trip");

        string ini = ResourceTextSerializer.Serialize(settings, ResourceTextSerializer.Format.Ini, new SerializerOptions { IniPlainValues = true });
        Expect(ini.Contains("cell=1, -2, 3"), "IniPlainValues");
        CheckSettings(ResourceTextSerializer.Deserialize(ini, ResourceTextSerializer.Format.Ini), "IniPlainValues");

        string xml = ResourceTextSerializer.Serialize(settings, ResourceTextSerializer.Format.Xml, new SerializerOptions { XmlDeclaration = false });
        Expect(xml.StartsWith("<resource") && ResourceTextSerializer.Serialize(settings, ResourceTextSerializer.Format.Xml).StartsWith("<?xml"), "XmlDeclaration");
        CheckSettings(ResourceTextSerializer.Deserialize(xml, ResourceTextSerializer.Format.Xml), "XmlDeclaration = false");

        // A preset saved as .tres gives the same output.
        var options = new SerializerOptions { Indent = "\t", SortKeys = true, EnumsAsNames = false };
        string presetPath = Root + "options.tres";
        ExpectEqual(ResourceSaver.Save(options, presetPath), Error.Ok, "save SerializerOptions as .tres");
        var preset = ResourceLoader.Load(presetPath, "", ResourceLoader.CacheMode.Ignore) as SerializerOptions;
        Expect(preset != null && ResourceTextSerializer.Serialize(settings, json, preset) == ResourceTextSerializer.Serialize(settings, json, options), "SerializerOptions .tres gives the same output");
    }

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        Expect(Engine.HasSingleton("ResourceTextSerializer"), "ResourceTextSerializer singleton exists");
        Expect(ClassDB.ClassExists("XMLWriter"), "XMLWriter class exists");

        foreach (string ext in new[] { "json", "ini" })
        {
            string path = Root + "settings." + ext;
            ExpectEqual(ResourceSaver.Save(MakeSettings(), path), Error.Ok, $"save settings as .{ext}");
            Expect(FileAccess.GetFileAsString(path).Length > 0, $".{ext} file is not empty");
            CheckSettings(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore), "." + ext);
        }

        Resource list = New("bus_list.gd");
        var entries = (Array)list.Get("entries");
        entries.Add(MakeBus("Music", -12.0f, 1));
        entries.Add(MakeBus("Voice, \"main\"", 2.0f, 7));
        Expect(ResourceTextSerializer.CanSerialize(list, ResourceTextSerializer.Format.Csv), "a list of flat records can be saved as CSV");
        Expect(!ResourceTextSerializer.CanSerialize(MakeSettings(), ResourceTextSerializer.Format.Csv), "nested settings cannot be saved as CSV");
        ExpectEqual(ResourceSaver.Save(list, Root + "buses.csv"), Error.Ok, "save list as .csv");
        Resource loadedList = ResourceLoader.Load(Root + "buses.csv", "Issue05GdBusList", ResourceLoader.CacheMode.Ignore); // CSV: type hint needed
        var le = loadedList?.Get("entries").As<Array>();
        Expect(le != null && le.Count == 2 && SameBus(le[0], entries[0]) && SameBus(le[1], entries[1]), ".csv: rows with comma and quotes round-trip");

        string json = ResourceTextSerializer.Serialize(MakeSettings(), ResourceTextSerializer.Format.Json);
        Expect(json.Contains("Cleo"), "Serialize(Format.Json) returns text");
        CheckSettings(ResourceTextSerializer.Deserialize(json, ResourceTextSerializer.Format.Json), "Deserialize(Format.Json)");
        CheckSettings(ResourceTextSerializer.Deserialize(ResourceTextSerializer.Serialize(MakeSettings(), ResourceTextSerializer.Format.Ini), ResourceTextSerializer.Format.Ini), "Deserialize(Format.Ini)");
        Expect(ResourceTextSerializer.IsFormatAvailable(ResourceTextSerializer.Format.Json) && !ResourceTextSerializer.IsFormatAvailable(ResourceTextSerializer.Format.Yaml), "IsFormatAvailable(): JSON yes, YAML no");
        Expect(System.Array.IndexOf(ResourceTextSerializer.GetAvailableFormats(), (int)ResourceTextSerializer.Format.Csv) >= 0, "GetAvailableFormats() lists Format.Csv");
        ExpectEqual(ResourceTextSerializer.GetFormatFromExtension(".INI"), ResourceTextSerializer.Format.Ini, "GetFormatFromExtension(\".INI\")");
        ExpectEqual(ResourceTextSerializer.GetFormatExtension(ResourceTextSerializer.Format.Csv), "csv", "GetFormatExtension(Format.Csv)");
        ExpectEqual(ResourceTextSerializer.TextLayoutVersion, 1, "TextLayoutVersion is 1");
        Expect(!ClassDB.ClassHasMethod("ResourceTextSerializer", "get_formats") && !ClassDB.ClassHasMethod("ResourceTextSerializer", "can_save_as_csv"), "old string API is gone");
        CheckSerializerOptions();

        using (var f = FileAccess.Open(Root + "plain.json", FileAccess.ModeFlags.Write))
        {
            f.StoreString("{\"answer\": 42}");
        }
        var plain = ResourceLoader.Load(Root + "plain.json", "", ResourceLoader.CacheMode.Ignore) as Json;
        Expect(plain != null, "plain JSON without type marker still loads as JSON");
        Expect(plain != null && (int)plain.Data.AsGodotDictionary()["answer"] == 42, "plain JSON data is intact");

        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
        DirAccess.RemoveAbsolute(Root);
        return Task.CompletedTask;
    }
}
