using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue36FeatureTomlModule;

/// <summary>C# checks of 36-feature-toml-module. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue36FeatureTomlModuleCheck : FeatureCheckCs
{
    private const string Root = "user://36-feature-toml-module/cs/";
    private const string Dir = "res://36-feature-toml-module/scripts/gd/";
    private const string Title = "Main \"menu\"\nsecond line";

    protected override string CheckName => "36-feature-toml-module";

    private static Resource New(string script) => (Resource)GD.Load<GDScript>(Dir + script).New();

    private static Resource MakeEntry(string name, int level, int mode)
    {
        Resource entry = New("toml_entry.gd");
        entry.Set("name", name);
        entry.Set("level", level);
        entry.Set("speed", 0.1 * level);
        entry.Set("mode", mode);
        entry.Set("offset", new Vector2(0.1f, -2.5f));
        return entry;
    }

    private static Resource MakeSettings()
    {
        Resource s = New("toml_settings.gd");
        s.Set("volume", 0.8);
        s.Set("title", Title);
        Resource main = MakeEntry("Ann", 3, 5);
        main.Set("child", MakeEntry("Ann's child", 4, 6));
        s.Set("main", main);
        Resource cleo = MakeEntry("Cleo, the 2nd", 2, 6);
        cleo.Set("child", MakeEntry("Dan", 7, 0));
        var entries = (Array)s.Get("entries");
        entries.Add(MakeEntry("Bob", 1, 0));
        entries.Add(cleo);
        s.Set("extra", new Dictionary { { "difficulty", "hard" }, { "lives", 3 }, { "key with spaces", true } });
        s.Set("numbers", new[] { 4, -7 });
        s.Set("tint", new Color(1, 0, 0, 1));
        return s;
    }

    private static bool SameEntry(Variant a, Variant b)
    {
        var x = a.As<Resource>();
        var y = b.As<Resource>();
        return x != null && y != null && (string)x.Get("name") == (string)y.Get("name") && (int)x.Get("level") == (int)y.Get("level")
            && Mathf.IsEqualApprox((double)x.Get("speed"), (double)y.Get("speed")) && (int)x.Get("mode") == (int)y.Get("mode")
            && ((Vector2)x.Get("offset")).IsEqualApprox((Vector2)y.Get("offset"));
    }

    private void CheckSettings(Resource loaded, string label)
    {
        Resource e = MakeSettings();
        bool ok = loaded != null && loaded.GetScript().As<Script>()?.ResourcePath == Dir + "toml_settings.gd";
        Expect(ok, $"{label}: loads as Issue36GdTomlSettings");
        if (!ok)
        {
            return;
        }
        Expect(Mathf.IsEqualApprox((double)loaded.Get("volume"), 0.8) && (string)loaded.Get("title") == Title, $"{label}: float and multi-line string with quotes");
        var main = loaded.Get("main").As<Resource>();
        var expectedMain = e.Get("main").As<Resource>();
        Expect(SameEntry(main, expectedMain) && main != null && SameEntry(main.Get("child"), expectedMain.Get("child")), $"{label}: nested sub-resources (two levels)");
        Expect(loaded.Get("spare").As<Resource>() == null, $"{label}: null sub-resource stays null");
        var le = (Array)loaded.Get("entries");
        var ee = (Array)e.Get("entries");
        Expect(le.Count == 2 && SameEntry(le[0], ee[0]) && SameEntry(le[1], ee[1]), $"{label}: typed array of resources");
        Expect(le.Count == 2 && le[0].As<Resource>().Get("child").As<Resource>() == null
            && SameEntry(le[1].As<Resource>().Get("child"), ee[1].As<Resource>().Get("child")), $"{label}: sub-resource inside an array element");
        var extra = (Dictionary)loaded.Get("extra");
        Expect((string)extra["difficulty"] == "hard" && (int)extra["lives"] == 3 && (bool)extra["key with spaces"], $"{label}: Dictionary with a quoted key");
        int[] numbers = (int[])loaded.Get("numbers");
        Expect(numbers.Length == 2 && numbers[0] == 4 && numbers[1] == -7 && ((Color)loaded.Get("tint")).IsEqualApprox(new Color(1, 0, 0, 1)), $"{label}: PackedInt32Array and Color");
    }

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        Expect(ClassDB.ClassExists("TOML"), "TOML class exists (module enabled)");

        // File round trip.
        string path = Root + "settings.toml";
        ExpectEqual(ResourceSaver.Save(MakeSettings(), path), Error.Ok, "save settings as .toml");
        ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", "GetResourceType() reads \"$type\" (script class -> native base)");
        CheckSettings(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore), ".toml");
        CheckSettings(ResourceLoader.Load(path, "Issue36GdTomlSettings", ResourceLoader.CacheMode.Ignore), ".toml with type hint");

        // Key order.
        string text = Toml.ResourceToToml(MakeSettings());
        string[] order = { "\"$type\" = ", "\"$format_version\" = 1", "volume = ", "title = ", "spare = \"\"", "numbers = [4, -7]", "tint = ", "\n[main]\n", "\n[main.child]\n", "\n[[entries]]\n", "\n[entries.child]\n", "\n[extra]\n" };
        int last = -1;
        bool ordered = true;
        foreach (string key in order)
        {
            int at = text.Find(key);
            if (at <= last)
            {
                ordered = false;
                GD.Print("  out of order or missing: ", key);
            }
            last = at;
        }
        Expect(ordered, "keys in the file follow the property order (plain keys, then tables)");
        Expect(text.Split("\n[[entries]]\n").Length == 3, "array of resources is written as an array of tables");

        // String API.
        Expect(text.Contains("mode = \"Angry\""), "enums are written as names");
        CheckSettings(Toml.TomlToResource(text), "TomlToResource()");
        string numbersText = Toml.ResourceToToml(MakeSettings(), new Dictionary { { "enums_as_names", false } });
        Expect(numbersText.Contains("mode = 5"), "option enums_as_names = false writes numbers");
        CheckSettings(Toml.TomlToResource(numbersText), "TomlToResource() with enum numbers");
        string untyped = text.Replace("\"$type\" = \"Issue36GdTomlSettings\"\n", "");
        CheckSettings(Toml.TomlToResource(untyped, "Issue36GdTomlSettings"), "TomlToResource() with type hint");

        // Parse.
        const string source = "# Hand-written\n"
            + "title = \"TOML\"\n"
            + "literal = 'C:\\path'\n"
            + "hex = 0xff\n"
            + "big = 1_000_000\n"
            + "pi = 3.14\n"
            + "up = inf\n"
            + "when = 1979-05-27T07:32:00Z\n"
            + "list = [1, \"two\", { three = 3 }]\n"
            + "\n[window]\nwidth = 1280\nsize.height = 720\n"
            + "\n[[players]]\nname = \"Ann\"\n"
            + "\n[[players]]\nname = \"Bob\"\n";
        var toml = new Toml();
        ExpectEqual(toml.Parse(source), Error.Ok, "Parse() a hand-written document");
        Dictionary data = toml.GetData();
        Expect((string)data["title"] == "TOML" && (string)data["literal"] == "C:\\path", "basic and literal strings");
        Expect((long)data["hex"] == 255 && (long)data["big"] == 1000000 && Mathf.IsEqualApprox((double)data["pi"], 3.14) && double.IsPositiveInfinity((double)data["up"]), "integers and floats");
        Expect((string)data["when"] == "1979-05-27T07:32:00Z", "dates are read as strings");
        var list = (Array)data["list"];
        Expect(list.Count == 3 && (int)((Dictionary)list[2])["three"] == 3, "mixed array with inline table");
        var window = (Dictionary)data["window"];
        Expect((int)window["width"] == 1280 && (int)((Dictionary)window["size"])["height"] == 720, "tables and dotted keys");
        var players = (Array)data["players"];
        Expect(players.Count == 2 && (string)((Dictionary)players[1])["name"] == "Bob", "arrays of tables");
        Expect(string.Join(",", data.Keys) == "title,literal,hex,big,pi,up,when,list,window,players", "keys keep the file order");
        Expect((int)Toml.ParseString("a = 1")["a"] == 1, "ParseString()");

        // Errors.
        var bad = new Toml();
        ExpectEqual(bad.Parse("a = 1\nb = 2\na = 3\n"), Error.ParseError, "duplicate key is a parse error");
        ExpectEqual(bad.GetErrorLine(), 3, "error line of the duplicate key");
        Expect(bad.GetErrorColumn() > 0 && bad.GetErrorMessage().Length > 0, "error column and message");
        ExpectEqual(bad.Parse("x = { a = 1 }\n[x.b]\n"), Error.ParseError, "inline tables can't be extended");
        ExpectEqual(bad.Parse("x = \"open\n"), Error.ParseError, "unclosed string");

        // Stringify.
        string written = Toml.Stringify(new Dictionary
        {
            { "name", "Game" },
            { "window", new Dictionary { { "width", 1280 }, { "scale", 1.5 } } },
            { "volume", 0.1 },
            { "tags", new[] { "a", "b" } },
        });
        Expect(written.StartsWith("name = \"Game\"\nvolume = 0.1\ntags = [\"a\", \"b\"]\n"), "Stringify(): plain keys first, in order");
        Expect(written.Contains("\n[window]\nwidth = 1280\nscale = 1.5\n"), "Stringify(): nested Dictionary as table");
        Dictionary back = Toml.ParseString(written);
        Expect((int)((Dictionary)back["window"])["width"] == 1280 && Mathf.IsEqualApprox((double)back["volume"], 0.1), "Stringify() output reads back");

        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
        DirAccess.RemoveAbsolute(Root);
        return Task.CompletedTask;
    }
}
