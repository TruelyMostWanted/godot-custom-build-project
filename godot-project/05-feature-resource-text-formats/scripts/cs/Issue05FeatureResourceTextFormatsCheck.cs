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
        Expect(ResourceTextSerializer.CanSaveAsCsv(list), "a list of flat records can be saved as CSV");
        Expect(!ResourceTextSerializer.CanSaveAsCsv(MakeSettings()), "nested settings cannot be saved as CSV");
        ExpectEqual(ResourceSaver.Save(list, Root + "buses.csv"), Error.Ok, "save list as .csv");
        Resource loadedList = ResourceLoader.Load(Root + "buses.csv", "Issue05GdBusList", ResourceLoader.CacheMode.Ignore); // CSV: type hint needed
        var le = loadedList?.Get("entries").As<Array>();
        Expect(le != null && le.Count == 2 && SameBus(le[0], entries[0]) && SameBus(le[1], entries[1]), ".csv: rows with comma and quotes round-trip");

        string json = ResourceTextSerializer.Serialize(MakeSettings(), "json");
        Expect(json.Contains("Cleo"), "Serialize(\"json\") returns text");
        CheckSettings(ResourceTextSerializer.Deserialize(json, "json"), "Deserialize(\"json\")");
        CheckSettings(ResourceTextSerializer.Deserialize(ResourceTextSerializer.Serialize(MakeSettings(), "ini"), "ini"), "Deserialize(\"ini\")");

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
