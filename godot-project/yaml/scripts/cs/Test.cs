using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.YamlFormat;

/// <summary>
/// Full API test of YAML: every method of the Yaml class, paths, documents, the parser, the writer and the YAML text of
/// a resource. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://yaml/cs/";
    private const string Enemies = "res://yaml/resources/enemies.yaml";
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private const string HandWritten = "# Hand-written\n---\ntitle: YAML # comment\nquoted: \"tab\\tend\"\nsingle: 'it''s'\nhex: 0x1F\nbig: 1e3\nup: .inf\nyes: yes\nnone: ~\nlist:\n- one\n- {a: 1, b: [true, null]}\nwindow:\n  width: 1280\n  size: [1280, 720]\ntext: |\n  line 1\n  line 2\n";
    // The text of the wiki section "Save your own resource as YAML".
    private const string SettingsYaml = "$type: GameSettings\n$format_version: 1\ntitle: Space Trader\nquality: High\nresolution: [1920, 1080]\nfullscreen: true\naudio:\n  master_volume: 1.0\n  music_volume: 0.5\n  muted: false\nprofiles:\n  - name: Ann\n    level: 12\n";
    private const int High = 2;

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestDataFile();
        TestPaths();
        TestParse();
        TestParseErrors();
        TestDocuments();
        TestStringify();
        TestSerialize();
        TestDeserialize();
        TestFiles();
        return Task.CompletedTask;
    }

    private static Resource New(string script)
    {
        var gdScript = GD.Load<GDScript>(Scripts + script);
        return (Resource)gdScript.New();
    }

    private static Resource MakeSettings()
    {
        Resource settings = New("game_settings.gd");
        settings.Set("title", "Space Trader");
        settings.Set("quality", High);
        settings.Set("resolution", new Vector2I(1920, 1080));
        settings.Set("fullscreen", true);
        settings.Set("audio", New("audio_settings.gd"));
        Resource ann = New("player_profile.gd");
        ann.Set("name", "Ann");
        ann.Set("level", 12);
        var profiles = (Array)settings.Get("profiles");
        profiles.Add(ann);
        return settings;
    }

    private void TestDataFile()
    {
        var enemies = GD.Load<Yaml>(Enemies);
        Expect(enemies != null, "Load() of a plain .yaml returns a Yaml");
        Dictionary data = enemies.Data.AsGodotDictionary();
        Expect(data.Keys.Select(key => (string)key).SequenceEqual(["enemies", "boss"]), "Data keeps the key order of the file");
        ExpectEqual(enemies.GetValue("enemies/0/hp").VariantType, Variant.Type.Int, "hp: 12 is an int");
        ExpectEqual(enemies.GetValue("enemies/0/speed").VariantType, Variant.Type.Float, "speed: 1.5 is a float");
        ExpectEqual((bool)enemies.GetValue("enemies/1/flying"), true, "flying: true is a bool");
        ExpectEqual((string)enemies.GetValue("enemies/0/drops").AsGodotArray()[0], "gel", "a flow sequence");
        Expect(enemies.HasValue("boss"), "boss: null has a key");
        ExpectEqual(enemies.GetValue("boss", 1).VariantType, Variant.Type.Nil, "boss: null is null");
        ExpectEqual(ResourceLoader.GetResourceType(Enemies), "YAML", "GetResourceType() of a plain .yaml");
    }

    private void TestPaths()
    {
        var yaml = new Yaml();
        yaml.Parse("enemies:\n  - name: Slime\n\"a/b\": 2\n\"c~d\": 3\n");
        ExpectEqual((string)yaml.GetValue("enemies/0/name"), "Slime", "GetValue() with a path of keys and indices");
        ExpectEqual((string)yaml.GetValue("enemies/5/name", "none"), "none", "GetValue() returns the default");
        ExpectEqual((int)yaml.GetValue("a~1b"), 2, "~1 is a / in a key");
        ExpectEqual((int)yaml.GetValue("c~0d"), 3, "~0 is a ~ in a key");
        Expect(yaml.HasValue("enemies/0"), "HasValue()");
        Expect(!yaml.HasValue("enemies/1"), "HasValue() of a missing index");
        yaml.SetValue("enemies/0/hp", 12);
        ExpectEqual((int)yaml.GetValue("enemies/0/hp"), 12, "SetValue() adds a key");
        yaml.SetValue("enemies/1", new Dictionary { ["name"] = "Ghost", ["hp"] = 20 });
        ExpectEqual((string)yaml.GetValue("enemies/1/name"), "Ghost", "SetValue() with an index equal to the size appends");
        yaml.EraseValue("a~1b");
        Expect(!yaml.HasValue("a~1b"), "EraseValue()");
    }

    private void TestParse()
    {
        var yaml = new Yaml();
        ExpectEqual(yaml.Parse(HandWritten), Error.Ok, "Parse() of a hand-written document");
        Dictionary data = yaml.Data.AsGodotDictionary();
        ExpectEqual((string)data["title"], "YAML", "a plain scalar, the comment is dropped");
        ExpectEqual((string)data["quoted"], "tab\tend", "a double-quoted scalar with an escape");
        ExpectEqual((string)data["single"], "it's", "a single-quoted scalar");
        ExpectEqual((int)data["hex"], 31, "a hex integer");
        ExpectEqual(data["big"].VariantType, Variant.Type.Float, "1e3 is a float");
        Expect(double.IsInfinity((double)data["up"]), ".inf");
        ExpectEqual((string)data["yes"], "yes", "yes is a String (YAML 1.2)");
        Expect(data.ContainsKey("none"), "~ has a key");
        ExpectEqual(data["none"].VariantType, Variant.Type.Nil, "~ is null");
        Array flow = data["list"].AsGodotArray()[1].AsGodotDictionary()["b"].AsGodotArray();
        Expect(flow.Count == 2 && (bool)flow[0] && flow[1].VariantType == Variant.Type.Nil, "a block sequence with a flow mapping");
        Array size = data["window"].AsGodotDictionary()["size"].AsGodotArray();
        Expect(size.Select(value => (int)value).SequenceEqual([1280, 720]), "a nested mapping with a flow sequence");
        ExpectEqual((string)data["text"], "line 1\nline 2\n", "a literal block scalar");
        Expect(data.Keys.Select(key => (string)key).SequenceEqual(["title", "quoted", "single", "hex", "big", "up", "yes", "none", "list", "window", "text"]), "keys keep the file order");
        Array parsed = Yaml.ParseString("- 1\n- 2\n").AsGodotArray();
        Expect(parsed.Select(value => (int)value).SequenceEqual([1, 2]), "ParseString() of a sequence");
    }

    private void TestParseErrors()
    {
        var yaml = new Yaml();
        ExpectEqual(yaml.Parse("root:\n  a: 1\n b: 2\n"), Error.ParseError, "broken indentation fails");
        ExpectEqual(yaml.GetErrorLine(), 3, "GetErrorLine()");
        ExpectEqual(yaml.GetErrorColumn(), 2, "GetErrorColumn()");
        Expect(yaml.GetErrorMessage().Contains("indentation"), "GetErrorMessage()");
        ExpectEqual(yaml.Parse("a:\n\t- 1\n"), Error.ParseError, "a tab in the indentation fails");
        ExpectEqual(yaml.GetErrorMessage(), "Tabs can't be used for indentation.", "error message of the tab");
        ExpectEqual(yaml.Parse("list: [1, 2\nnext: 3\n"), Error.ParseError, "an unclosed flow sequence fails");
        ExpectEqual(yaml.GetErrorLine(), 1, "error line where the flow sequence starts");
        Expect(yaml.GetErrorMessage().Contains("not closed"), "error message of the unclosed flow sequence");
        ExpectEqual(yaml.Parse("a: &x 1\nb: *x\n"), Error.ParseError, "anchors and aliases are rejected");
    }

    private void TestDocuments()
    {
        ExpectEqual(new Yaml().GetDocumentCount(), 1, "a new Yaml has one document");
        var yaml = new Yaml();
        ExpectEqual(yaml.Parse("a: 1\n---\n- x\n...\n---\nb: 2\n"), Error.Ok, "Parse() of several documents");
        ExpectEqual(yaml.GetDocumentCount(), 3, "GetDocumentCount()");
        ExpectEqual((int)yaml.GetValue("a"), 1, "Data is the first document");
        ExpectEqual((string)yaml.GetDocument(1).AsGodotArray()[0], "x", "GetDocument()");
        yaml.AddDocument(new Dictionary { ["c"] = 3 });
        ExpectEqual(yaml.GetDocumentCount(), 4, "AddDocument()");
        yaml.RemoveDocument(1);
        ExpectEqual(yaml.EncodeToText(), "a: 1\n---\nb: 2\n---\nc: 3\n", "documents are written with ---");
        string path = Root + "documents.yaml";
        ExpectEqual(yaml.Save(path), Error.Ok, "Save() of several documents");
        var loaded = new Yaml();
        ExpectEqual(loaded.Load(path), Error.Ok, "Load() of several documents");
        ExpectEqual(loaded.GetDocumentCount(), 3, "the documents survive Save() and Load()");
        yaml.RemoveDocument(0);
        ExpectEqual((int)yaml.GetValue("b"), 2, "RemoveDocument(0) makes the next document Data");
        Dictionary first = Yaml.ParseString("a: 1\n---\nb: 2\n").AsGodotDictionary();
        Expect(first.Count == 1 && (int)first["a"] == 1, "ParseString() reads the first document only");
    }

    private void TestStringify()
    {
        var data = new Dictionary
        {
            ["name"] = "Game",
            ["window"] = new Dictionary { ["width"] = 1280, ["scale"] = 1.5 },
            ["tags"] = new Array { "a", "b: c" },
            ["players"] = new Array { new Dictionary { ["name"] = "Ann", ["level"] = 3 } },
            ["nothing"] = default,
            ["flag"] = "true",
        };
        string text = Yaml.Stringify(data);
        Expect(text.StartsWith("name: Game\nwindow:\n  width: 1280\n  scale: 1.5\ntags:\n  - a\n  - \"b: c\"\nplayers:\n  - name: Ann\n    level: 3\n"), "Stringify(): block style in key order");
        Expect(text.Contains("\nnothing: null\nflag: \"true\"\n"), "Stringify(): null, and quotes for a String that looks like a bool");
        ExpectEqual(Yaml.Stringify(Yaml.ParseString(text)), text, "Stringify() reads back");
        var options = new SerializerOptions { Indent = "" };
        ExpectEqual(Yaml.Stringify(data["players"], options), "[{name: Ann, level: 3}]\n", "Stringify() with an empty Indent writes flow style");
    }

    private void TestSerialize()
    {
        Resource settings = MakeSettings();
        string text = Yaml.Serialize(settings);
        ExpectEqual(text, SettingsYaml, "Serialize() writes the text of the wiki guide");
        ExpectEqual(text, ResourceTextSerializer.Serialize(settings, ResourceTextSerializer.Format.Yaml), "Serialize() is ResourceTextSerializer.Serialize(Yaml)");
        settings.Set("title", "Main \"menu\": first line\nsecond line");
        string block = Yaml.Serialize(settings);
        Expect(block.Contains("title: |-\n  Main \"menu\": first line\n  second line\n"), "a String with a line break is a | block");
        settings.Set("title", "- Cleo");
        string dash = Yaml.Serialize(settings);
        Expect(dash.Contains("title: \"- Cleo\""), "a String that YAML would misread is quoted");
        var options = new SerializerOptions { EnumsAsNames = false };
        string numbers = Yaml.Serialize(settings, options);
        Expect(numbers.Contains("quality: 2"), "EnumsAsNames = false writes numbers");
        options = new SerializerOptions { Indent = "    " };
        string wide = Yaml.Serialize(MakeSettings(), options);
        Expect(wide.Contains("\n    music_volume: 0.5\n"), "Indent sets the spaces of a nesting level");
    }

    private void TestDeserialize()
    {
        Resource copy = Yaml.Deserialize(SettingsYaml);
        ExpectEqual((int)copy.Get("quality"), High, "Deserialize(): enum by name");
        ExpectEqual((Vector2I)copy.Get("resolution"), new Vector2I(1920, 1080), "Deserialize(): Vector2i");
        ExpectEqual((double)copy.Get("audio").As<Resource>().Get("music_volume"), 0.5, "Deserialize(): nested resource");
        var profiles = (Array)copy.Get("profiles");
        ExpectEqual((int)profiles[0].As<Resource>().Get("level"), 12, "Deserialize(): a sequence of mappings");
        Resource low = Yaml.Deserialize("title: Low\nquality: 0\n", "GameSettings");
        ExpectEqual((int)low.Get("quality"), 0, "Deserialize() with a type hint, enum by number");
        ExpectEqual((Vector2I)low.Get("resolution"), new Vector2I(1280, 720), "missing values keep the defaults");
        var options = new SerializerOptions { SortKeys = true };
        string sorted = Yaml.Serialize(MakeSettings(), options);
        Resource fromSorted = Yaml.Deserialize(sorted, "", options);
        ExpectEqual((string)fromSorted.Get("title"), "Space Trader", "SortKeys reads back");
    }

    private void TestFiles()
    {
        foreach (string extension in new[] { "yaml", "yml" })
        {
            string path = Root + "settings." + extension;
            ExpectEqual(ResourceSaver.Save(MakeSettings(), path), Error.Ok, $"ResourceSaver.Save() to .{extension}");
            ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", $".{extension}: GetResourceType() reads $type");
            Resource loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
            ExpectEqual((string)loaded.Get("title"), "Space Trader", $".{extension} with $type loads as its class");
        }
        string plainPath = Root + "plain.yml";
        var plain = new Yaml();
        plain.SetValue("name", "Short");
        ExpectEqual(plain.Save(plainPath), Error.Ok, "Save() of a plain .yml");
        var again = (Yaml)ResourceLoader.Load(plainPath, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((string)again.GetValue("name"), "Short", "a plain .yml loads as Yaml");
        Expect(ClassDB.ClassExists("YAML"), "the yaml module is built");
    }
}
