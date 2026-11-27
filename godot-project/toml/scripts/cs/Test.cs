using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TomlFormat;

/// <summary>Full API test of TOML: every method of the Toml class, the parser, the writer and the TOML text of a resource. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Root = "user://toml/cs/";
    private const string Server = "res://toml/resources/server.toml";
    private const string Scripts = "res://text-serialization/scripts/gd/";
    private const string HandWritten = "# Hand-written\ntitle = \"TOML\"\nliteral = 'C:\\path'\nhex = 0xff\nbig = 1_000_000\npi = 3.14\nup = inf\nwhen = 1979-05-27T07:32:00Z\nlist = [1, \"two\", { three = 3 }]\n\n[window]\nwidth = 1280\nsize.height = 720\n\n[[players]]\nname = \"Ann\"\n\n[[players]]\nname = \"Bob\"\n";
    // The text of the wiki section "Save your own resource as TOML".
    private const string SettingsToml = "\"$type\" = \"GameSettings\"\n\"$format_version\" = 1\ntitle = \"Space Trader\"\nquality = \"High\"\nresolution = [1920, 1080]\nfullscreen = true\n\n[audio]\nmaster_volume = 1.0\nmusic_volume = 0.5\nmuted = false\n\n[[profiles]]\nname = \"Ann\"\nlevel = 12\n";
    private const int High = 2;

    protected override Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Root);
        TestDataFile();
        TestParse();
        TestParseErrors();
        TestTables();
        TestCopy();
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
        var config = GD.Load<Toml>(Server);
        Expect(config != null, "Load() of a plain .toml returns a Toml");
        Dictionary data = config.Data.AsGodotDictionary();
        Expect(data.Keys.Select(key => (string)key).SequenceEqual(["title", "server", "maps"]), "Data keeps the key order of the file");
        ExpectEqual((string)config.GetValue("", "title"), "Arena", "GetValue() of the root table \"\"");
        ExpectEqual((int)config.GetValue("server", "port"), 8080, "GetValue() of a table");
        ExpectEqual((int)config.GetValue("server.limits", "players"), 16, "GetValue() with a dotted table path");
        ExpectEqual(config.GetValue("server.limits", "tick_rate").VariantType, Variant.Type.Float, "a float keeps its type");
        ExpectEqual((int)config.GetValue("server", "missing", 7), 7, "GetValue() returns the default");
        Dictionary desert = data["maps"].AsGodotArray()[1].AsGodotDictionary();
        ExpectEqual((string)desert["name"], "Desert", "[[maps]] is an Array of Dictionaries");
        Expect(config.GetTables().SequenceEqual(["server", "server.limits"]), "GetTables() lists the tables in file order");
        Expect(config.GetTableKeys("server").SequenceEqual(["port", "public", "limits"]), "GetTableKeys()");
    }

    private void TestParse()
    {
        var toml = new Toml();
        ExpectEqual(toml.Parse(HandWritten), Error.Ok, "Parse() of a hand-written document");
        Dictionary data = toml.Data.AsGodotDictionary();
        ExpectEqual((string)data["title"], "TOML", "a basic string");
        ExpectEqual((string)data["literal"], "C:\\path", "a literal string");
        ExpectEqual((int)data["hex"], 255, "a hex integer");
        ExpectEqual((int)data["big"], 1000000, "an integer with underscores");
        Expect(Mathf.IsEqualApprox((double)data["pi"], 3.14), "a float");
        Expect(double.IsInfinity((double)data["up"]), "inf");
        ExpectEqual((string)data["when"], "1979-05-27T07:32:00Z", "a date stays a String");
        Dictionary inline = data["list"].AsGodotArray()[2].AsGodotDictionary();
        ExpectEqual((int)inline["three"], 3, "a mixed array with an inline table");
        Dictionary size = data["window"].AsGodotDictionary()["size"].AsGodotDictionary();
        ExpectEqual((int)size["height"], 720, "a dotted key");
        Dictionary bob = data["players"].AsGodotArray()[1].AsGodotDictionary();
        ExpectEqual((string)bob["name"], "Bob", "an array of tables");
        Expect(data.Keys.Select(key => (string)key).SequenceEqual(["title", "literal", "hex", "big", "pi", "up", "when", "list", "window", "players"]), "keys keep the file order");
        Dictionary parsed = Toml.ParseString("a = 1");
        Expect(parsed.Count == 1 && (int)parsed["a"] == 1, "ParseString()");
    }

    private void TestParseErrors()
    {
        var toml = new Toml();
        ExpectEqual(toml.Parse("a = 1\nb = 2\na = 3\n"), Error.ParseError, "a duplicate key fails");
        ExpectEqual(toml.GetErrorLine(), 3, "GetErrorLine()");
        ExpectEqual(toml.GetErrorColumn(), 6, "GetErrorColumn()");
        ExpectEqual(toml.GetErrorMessage(), "The key 'a' is defined twice.", "GetErrorMessage()");
        ExpectEqual(toml.Parse("x = { a = 1 }\n[x.b]\n"), Error.ParseError, "an inline table can't be extended");
        ExpectEqual(toml.Parse("x = \"open\n"), Error.ParseError, "an unclosed string fails");
    }

    private void TestTables()
    {
        var toml = new Toml();
        toml.SetValue("server.limits", "players", 16);
        Expect(toml.HasTable("server"), "SetValue() creates the parent table");
        Expect(toml.HasTable("server.limits"), "HasTable() with a dotted path");
        Expect(toml.HasTableKey("server.limits", "players"), "HasTableKey()");
        Expect(!toml.HasTableKey("server", "players"), "HasTableKey() of another table");
        toml.SetValue(@"a\.b", "k", 2);
        Dictionary data = toml.Data.AsGodotDictionary();
        ExpectEqual((int)data["a.b"].AsGodotDictionary()["k"], 2, "a\\.b is the one table a.b");
        toml.SetValue("", "title", "Arena");
        ExpectEqual((string)data["title"], "Arena", "the empty path is the root table");
        toml.EraseTableKey("server.limits", "players");
        Expect(!toml.HasTableKey("server.limits", "players"), "EraseTableKey()");
        toml.EraseTable("server");
        Expect(!toml.HasTable("server.limits"), "EraseTable() removes the sub-tables too");
    }

    private void TestCopy()
    {
        var config = GD.Load<Toml>(Server);
        var mine = new Toml();
        mine.Parse(config.EncodeToText());
        mine.SetValue("server.limits", "players", 32);
        ExpectEqual((int)config.GetValue("server.limits", "players"), 16, "a parsed copy leaves the cached resource unchanged");
        string path = Root + "server.toml";
        ExpectEqual(mine.Save(path), Error.Ok, "Save()");
        var again = new Toml();
        ExpectEqual(again.Load(path), Error.Ok, "Load()");
        ExpectEqual((int)again.GetValue("server.limits", "players"), 32, "Load() reads the saved value");
    }

    private void TestStringify()
    {
        var data = new Dictionary
        {
            ["name"] = "Game",
            ["window"] = new Dictionary { ["width"] = 1280, ["scale"] = 1.5 },
            ["volume"] = 0.1,
            ["tags"] = new[] { "a", "b" },
            ["nothing"] = default,
        };
        string text = Toml.Stringify(data);
        Expect(text.StartsWith("name = \"Game\"\nvolume = 0.1\ntags = [\"a\", \"b\"]\nnothing = \"\"\n"), "Stringify(): plain keys first, in order, null as \"\"");
        Expect(text.Contains("\n[window]\nwidth = 1280\nscale = 1.5\n"), "Stringify(): a nested Dictionary is a table");
        Dictionary window = Toml.ParseString(text)["window"].AsGodotDictionary();
        Expect((int)window["width"] == 1280 && (double)window["scale"] == 1.5, "Stringify() reads back");
    }

    private void TestSerialize()
    {
        Resource settings = MakeSettings();
        string text = Toml.Serialize(settings);
        ExpectEqual(text, SettingsToml, "Serialize() writes the text of the wiki guide");
        ExpectEqual(text, ResourceTextSerializer.Serialize(settings, ResourceTextSerializer.Format.Toml), "Serialize() is ResourceTextSerializer.Serialize(Toml)");
        var options = new SerializerOptions { EnumsAsNames = false };
        string numbers = Toml.Serialize(settings, options);
        Expect(numbers.Contains("quality = 2"), "EnumsAsNames = false writes numbers");
        options = new SerializerOptions { IncludeTypeMarker = false };
        string untyped = Toml.Serialize(settings, options);
        Expect(!untyped.Contains("$type"), "IncludeTypeMarker = false leaves out \"$type\"");
        var profiles = (Array)settings.Get("profiles");
        profiles.Add(New("player_profile.gd"));
        string two = Toml.Serialize(settings);
        ExpectEqual(two.Split("\n[[profiles]]\n").Length - 1, 2, "an array of resources is an array of tables");
    }

    private void TestDeserialize()
    {
        Resource copy = Toml.Deserialize(SettingsToml);
        ExpectEqual((int)copy.Get("quality"), High, "Deserialize(): enum by name");
        ExpectEqual((Vector2I)copy.Get("resolution"), new Vector2I(1920, 1080), "Deserialize(): Vector2i");
        ExpectEqual((double)copy.Get("audio").As<Resource>().Get("music_volume"), 0.5, "Deserialize(): nested resource");
        var profiles = (Array)copy.Get("profiles");
        ExpectEqual((int)profiles[0].As<Resource>().Get("level"), 12, "Deserialize(): array of tables");
        string handWritten = "title = \"Low\"\nquality = 0\n";
        Resource low = Toml.Deserialize(handWritten, "GameSettings");
        ExpectEqual((int)low.Get("quality"), 0, "Deserialize() with a type hint, enum by number");
        ExpectEqual((Vector2I)low.Get("resolution"), new Vector2I(1280, 720), "missing values keep the defaults");
        Resource empty = Toml.Deserialize("audio = \"\"\nfullscreen = \"\"\n", "GameSettings");
        ExpectEqual(empty.Get("audio").VariantType, Variant.Type.Nil, "an empty string sets an object property to null");
        ExpectEqual((bool)empty.Get("fullscreen"), false, "an empty string keeps the default of a bool property");
        var options = new SerializerOptions { SortKeys = true };
        string sorted = Toml.Serialize(MakeSettings(), options);
        Resource fromSorted = Toml.Deserialize(sorted, "", options);
        ExpectEqual((string)fromSorted.Get("title"), "Space Trader", "SortKeys reads back");
    }

    private void TestFiles()
    {
        string path = Root + "settings.toml";
        ExpectEqual(ResourceSaver.Save(MakeSettings(), path), Error.Ok, "ResourceSaver.Save() to .toml");
        ExpectEqual(ResourceLoader.GetResourceType(path), "Resource", "GetResourceType() reads \"$type\" (native base of GameSettings)");
        Resource loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual((string)loaded.Get("title"), "Space Trader", "a .toml with \"$type\" loads as its class");
        ExpectEqual(ResourceLoader.GetResourceType(Server), "TOML", "GetResourceType() of a plain .toml");
        Expect(ClassDB.ClassExists("TOML"), "the toml module is built");
    }
}
