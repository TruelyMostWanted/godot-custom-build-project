using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue155FeatureYamlModule;

/// <summary>C# checks of 155-feature-yaml-module. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue155FeatureYamlModuleCheck : FeatureCheckCs
{
    private const string Root = "user://155-feature-yaml-module/cs/";
    private const string Dir = "res://155-feature-yaml-module/scripts/gd/";
    private const string Title = "Main \"menu\": first line\nsecond line";

    protected override string CheckName => "155-feature-yaml-module";

    private static Resource New(string script) => (Resource)GD.Load<GDScript>(Dir + script).New();

    private static Resource MakeEntry(string name, int level, int mode)
    {
        Resource entry = New("yaml_entry.gd");
        entry.Set("name", name);
        entry.Set("level", level);
        entry.Set("speed", 0.1 * level);
        entry.Set("mode", mode);
        entry.Set("offset", new Vector2(0.1f, -2.5f));
        return entry;
    }

    private static Resource MakeSettings()
    {
        Resource s = New("yaml_settings.gd");
        s.Set("volume", 0.8);
        s.Set("title", Title);
        Resource main = MakeEntry("Ann", 3, 5);
        main.Set("child", MakeEntry("Ann's child", 4, 6));
        s.Set("main", main);
        Resource cleo = MakeEntry("- Cleo, the 2nd", 2, 6);
        cleo.Set("child", MakeEntry("Dan", 7, 0));
        var entries = (Array)s.Get("entries");
        entries.Add(MakeEntry("Bob", 1, 0));
        entries.Add(cleo);
        s.Set("extra", new Dictionary { { "difficulty", "hard" }, { "lives", 3 }, { "key with spaces", true }, { "looks like a number", "42" } });
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
        bool ok = loaded != null && loaded.GetScript().As<Script>()?.ResourcePath == Dir + "yaml_settings.gd";
        Expect(ok, $"{label}: loads as Issue155GdYamlSettings");
        if (!ok)
        {
            return;
        }
        Expect(Mathf.IsEqualApprox((double)loaded.Get("volume"), 0.8) && (string)loaded.Get("title") == Title, $"{label}: float and multi-line string with quotes and ': '");
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
        Expect((string)extra["difficulty"] == "hard" && (int)extra["lives"] == 3 && (bool)extra["key with spaces"], $"{label}: Dictionary with a key with spaces");
        Expect(extra["looks like a number"].VariantType == Variant.Type.String, $"{label}: a string that looks like a number stays a string");
        int[] numbers = (int[])loaded.Get("numbers");
        Expect(numbers.Length == 2 && numbers[0] == 4 && numbers[1] == -7 && ((Color)loaded.Get("tint")).IsEqualApprox(new Color(1, 0, 0, 1)), $"{label}: PackedInt32Array and Color");
    }

    protected override Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        Expect(ClassDB.ClassExists("YAML"), "YAML class exists (module enabled)");

        // File round trip, both extensions.
        foreach (string extension in new[] { "yaml", "yml" })
        {
            string path = Root + "settings." + extension;
            ExpectEqual(ResourceSaver.Save(MakeSettings(), path), Error.Ok, $"save settings as .{extension}");
            ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", $".{extension}: GetResourceType() reads \"$type\" (script class -> native base)");
            CheckSettings(ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore), "." + extension);
            CheckSettings(ResourceLoader.Load(path, "Issue155GdYamlSettings", ResourceLoader.CacheMode.Ignore), $".{extension} with type hint");
        }

        // String API.
        string text = Yaml.Serialize(MakeSettings());
        Expect(text.StartsWith("$type: Issue155GdYamlSettings\n$format_version: 1\n"), "type marker first");
        Expect(text.Find("volume: ") < text.Find("title: ") && text.Find("title: ") < text.Find("\nmain:\n"), "keys follow the property order");
        Expect(text.Contains("title: |-\n  Main \"menu\": first line\n  second line\n"), "multi-line string as literal block scalar");
        Expect(text.Contains("\nspare: null\n") && text.Contains("\nnumbers: [4, -7]\n"), "null and number arrays");
        Expect(text.Contains("mode: Angry"), "enums are written as names");
        Expect(text.Contains("\"- Cleo, the 2nd\"") && text.Contains("looks like a number: \"42\""), "strings that YAML would misread are quoted");
        CheckSettings(Yaml.Deserialize(text), "Deserialize()");
        // Yaml.Serialize() is ResourceTextSerializer.Serialize(r, Format.Yaml).
        Expect(ResourceTextSerializer.IsFormatAvailable(ResourceTextSerializer.Format.Yaml), "IsFormatAvailable(Format.Yaml)");
        ExpectEqual(ResourceTextSerializer.Serialize(MakeSettings(), ResourceTextSerializer.Format.Yaml), text, "Serialize(Format.Yaml) equals Yaml.Serialize()");
        CheckSettings(ResourceTextSerializer.Deserialize(text, ResourceTextSerializer.Format.Yaml), "ResourceTextSerializer.Deserialize(Format.Yaml)");
        ExpectEqual(ResourceTextSerializer.GetFormatFromExtension("yml"), ResourceTextSerializer.Format.Yaml, "GetFormatFromExtension(\"yml\")");
        string numbersText = Yaml.Serialize(MakeSettings(), new SerializerOptions { EnumsAsNames = false });
        Expect(numbersText.Contains("mode: 5"), "option EnumsAsNames = false writes numbers");
        CheckSettings(Yaml.Deserialize(numbersText), "Deserialize() with enum numbers");
        string untyped = text.Replace("$type: Issue155GdYamlSettings\n", "");
        CheckSettings(Yaml.Deserialize(untyped, "Issue155GdYamlSettings"), "Deserialize() with type hint");
        CheckSettings(Yaml.Deserialize(Yaml.Serialize(MakeSettings(), new SerializerOptions { SortKeys = true, Indent = "    " })), "options SortKeys and Indent round trip");

        // Parse.
        const string source = "# Hand-written\n"
            + "---\n"
            + "title: YAML # comment\n"
            + "quoted: \"tab\\tend\"\n"
            + "single: 'it''s'\n"
            + "hex: 0x1F\n"
            + "big: 1e3\n"
            + "up: .inf\n"
            + "yes: yes\n"
            + "none: ~\n"
            + "list:\n- one\n- {a: 1, b: [true, null]}\n"
            + "window:\n  width: 1280\n  size: [1280, 720]\n"
            + "text: |\n  line 1\n  line 2\n";
        var yaml = new Yaml();
        ExpectEqual(yaml.Parse(source), Error.Ok, "Parse() a hand-written document");
        Dictionary data = yaml.GetData().AsGodotDictionary();
        Expect((string)data["title"] == "YAML" && (string)data["quoted"] == "tab\tend" && (string)data["single"] == "it's", "plain, double- and single-quoted scalars");
        Expect((long)data["hex"] == 31 && data["big"].VariantType == Variant.Type.Float && double.IsPositiveInfinity((double)data["up"]), "integers and floats of the core schema");
        Expect((string)data["yes"] == "yes" && data.ContainsKey("none") && data["none"].VariantType == Variant.Type.Nil, "yes is a string, ~ is null");
        var list = (Array)data["list"];
        var flowMap = (Dictionary)list[1];
        var b = (Array)flowMap["b"];
        Expect(list.Count == 2 && b.Count == 2 && (bool)b[0] && b[1].VariantType == Variant.Type.Nil, "block sequence with a flow mapping");
        var size = (Array)((Dictionary)data["window"])["size"];
        Expect(size.Count == 2 && (int)size[0] == 1280 && (int)size[1] == 720, "nested mapping with a flow sequence");
        Expect((string)data["text"] == "line 1\nline 2\n", "literal block scalar");
        Expect(string.Join(",", data.Keys) == "title,quoted,single,hex,big,up,yes,none,list,window,text", "keys keep the file order");
        Expect((int)Yaml.ParseString("a: 1").AsGodotDictionary()["a"] == 1, "ParseString()");
        Expect(Yaml.ParseString("- 1\n- 2\n").AsGodotArray().Count == 2, "ParseString() of a sequence");

        // Errors.
        var bad = new Yaml();
        ExpectEqual(bad.Parse("root:\n  a: 1\n b: 2\n"), Error.ParseError, "broken indentation is a parse error");
        ExpectEqual(bad.GetErrorLine(), 3, "error line of the broken indentation");
        Expect(bad.GetErrorColumn() > 0 && bad.GetErrorMessage().Contains("indentation"), "error column and message of the broken indentation");
        ExpectEqual(bad.Parse("list: [1, 2\nnext: 3\n"), Error.ParseError, "unclosed flow sequence is a parse error");
        ExpectEqual(bad.GetErrorLine(), 1, "error line of the unclosed flow sequence (where it starts)");
        Expect(bad.GetErrorMessage().Contains("not closed"), "error message of the unclosed flow sequence");
        ExpectEqual(bad.Parse("a: &x 1\nb: *x\n"), Error.ParseError, "anchors and aliases are rejected");
        ExpectEqual(bad.Parse("a: 1\n---\nb: 2\n"), Error.Ok, "a second document is read (#268)");
        ExpectEqual(bad.GetDocumentCount(), 2, "both documents are kept");
        ExpectEqual(bad.GetDocument(1).AsGodotDictionary()["b"].AsInt32(), 2, "the second document");

        // Stringify.
        var input = new Dictionary
        {
            { "name", "Game" },
            { "window", new Dictionary { { "width", 1280 }, { "scale", 1.5 } } },
            { "tags", new Array { "a", "b: c" } },
            { "players", new Array { new Dictionary { { "name", "Ann" }, { "level", 3 } } } },
            { "nothing", default },
            { "flag", "true" },
        };
        string written = Yaml.Stringify(input);
        Expect(written.StartsWith("name: Game\nwindow:\n  width: 1280\n  scale: 1.5\ntags:\n  - a\n  - \"b: c\"\nplayers:\n  - name: Ann\n    level: 3\n"), "Stringify(): block style in key order");
        Expect(written.Contains("\nnothing: null\nflag: \"true\"\n"), "Stringify(): null and a quoted string that looks like a bool");
        Dictionary back = Yaml.ParseString(written).AsGodotDictionary();
        Expect((int)((Dictionary)back["window"])["width"] == 1280 && (string)((Array)back["tags"])[1] == "b: c" && back["nothing"].VariantType == Variant.Type.Nil
            && (string)back["flag"] == "true", "Stringify() output reads back as the same data");
        ExpectEqual(Yaml.Stringify(input["players"], new SerializerOptions { Indent = "" }), "[{name: Ann, level: 3}]\n", "Stringify() with an empty Indent writes flow style");

        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            DirAccess.RemoveAbsolute(Root + file);
        }
        DirAccess.RemoveAbsolute(Root);
        return Task.CompletedTask;
    }
}
