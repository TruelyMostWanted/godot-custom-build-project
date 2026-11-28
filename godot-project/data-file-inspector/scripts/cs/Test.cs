using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

namespace TruelyInteractive.DataFileInspector;

/// <summary>
/// Test of Data File Inspector. The view is editor UI without an API of its own; this tests what it shows and promises,
/// from the data: the counts of the summary, the parse status, the schema issues, the types of the tree and that Data
/// (and the CSV Header) stay stored, editable properties. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Units = "res://csv/resources/units.csv";
    private const string UnitsSchema = "res://csv/resources/units_schema.tres";
    private const string Broken = "res://data-file-inspector/resources/broken.json";

    // The output of the wiki section "A data check before the game starts".
    private static readonly string[] Issues =
    [
        "row 3, column 1: \"class\": \"unknown\" is not one of: warrior,ranger,caster.",
        "row 3, column 2: \"hp\": 500 is outside the range 1.0 to 200.0.",
        "row 3, column 3: \"ranged\" must be of type bool, not \"maybe\".",
    ];

    protected override Task RunTests()
    {
        RunPerFormat();
        TestSummaries();
        TestParseStatus();
        TestSchemaStatus();
        TestTreeTypes();
        TestStoredProperties();
        return Task.CompletedTask;
    }

    private static string[] Stored(GodotObject instance)
    {
        return instance.GetPropertyList()
            .Where(property => ((PropertyUsageFlags)(int)property["usage"]).HasFlag(PropertyUsageFlags.Storage | PropertyUsageFlags.Editor))
            .Select(property => (string)property["name"])
            .ToArray();
    }

    private void TestSummaries()
    {
        var units = new Csv();
        units.Load(Units);
        ExpectEqual(units.GetRowCount(), 4, "CSV: 4 rows");
        ExpectEqual(units.GetColumnCount(), 4, "CSV: 4 columns");
        var settings = GD.Load<Ini>("res://ini/resources/settings.ini");
        ExpectEqual(settings.GetSections().Length, 2, "INI: 2 sections");
        int keys = settings.GetSectionKeys("video").Length + settings.GetSectionKeys("audio").Length;
        ExpectEqual(keys, 6, "INI: 6 keys");
        var level = GD.Load<Xml>("res://xml/resources/level.xml");
        ExpectEqual(level.GetRoot().Name, "level", "XML: root element <level>");
        ExpectEqual(level.GetRoot().GetChildren().Count, 3, "XML: 3 child nodes");
        var enemies = GD.Load<Json>("res://json/resources/enemies.json");
        ExpectEqual(enemies.Data.AsGodotDictionary().Count, 2, "JSON: Dictionary, 2 keys");
        var server = GD.Load<Toml>("res://toml/resources/server.toml");
        ExpectEqual(server.Data.AsGodotDictionary().Count, 3, "TOML: Dictionary, 3 keys");
        var yaml = GD.Load<Yaml>("res://yaml/resources/enemies.yaml");
        ExpectEqual(yaml.Data.AsGodotDictionary().Count, 2, "YAML: Dictionary, 2 keys");
    }

    private void TestParseStatus()
    {
        var json = new Json();
        ExpectEqual(json.Parse(FileAccess.GetFileAsString(Broken)), Error.ParseError, "broken.json does not parse");
        ExpectEqual(json.GetErrorLine() + 1, 4, "the parse error of broken.json is on line 4 (GetErrorLine() counts from 0)");
        ExpectEqual(json.Data.VariantType, Variant.Type.Nil, "broken.json has no data, so the tree stays empty");
    }

    private void TestSchemaStatus()
    {
        var units = new Csv();
        units.Load(Units);
        var schema = GD.Load<DataSchema>(UnitsSchema);
        Expect(Units.Match(schema.FilePatterns[0]), "units_schema.tres lists units.csv in FilePatterns");
        DataValidation result = units.Validate(schema);
        var lines = Enumerable.Range(0, result.GetIssueCount())
            .Select(i => $"row {result.GetIssueRow(i)}, column {result.GetIssueColumn(i)}: {result.GetIssueMessage(i)}");
        Expect(lines.SequenceEqual(Issues), "units.csv: the three issues of the wiki guide");
        var enemies = GD.Load<Json>("res://json/resources/enemies.json");
        Dictionary slime = enemies.Data.AsGodotDictionary()["enemies"].AsGodotArray()[0].AsGodotDictionary();
        ExpectEqual(slime["hp"].VariantType, Variant.Type.Float, "a JSON number is a float (Slime 12.0)");
    }

    private void TestTreeTypes()
    {
        var yaml = GD.Load<Yaml>("res://yaml/resources/enemies.yaml");
        ExpectEqual(yaml.GetValue("enemies/0/hp").VariantType, Variant.Type.Int, "YAML: hp is an int");
        var settings = GD.Load<Ini>("res://ini/resources/settings.ini");
        ExpectEqual(settings.GetValue("video", "width").VariantType, Variant.Type.Int, "INI: width is an int");
        ExpectEqual(settings.GetValue("video", "vsync").VariantType, Variant.Type.Bool, "INI: vsync is a bool");
        ExpectEqual(settings.GetValue("audio", "music").VariantType, Variant.Type.Float, "INI: music is a float");
        ExpectEqual(settings.GetValue("audio", "device").VariantType, Variant.Type.String, "INI: device is a String");
        var server = GD.Load<Toml>("res://toml/resources/server.toml");
        ExpectEqual(server.Data.AsGodotDictionary()["maps"].VariantType, Variant.Type.Array, "TOML: [[maps]] is an Array");
        var level = GD.Load<Xml>("res://xml/resources/level.xml");
        ExpectEqual(level.GetRoot().GetAttributes().Count, 2, "XML: <level> has 2 attributes (@name, @time)");
    }

    private void TestStoredProperties()
    {
        foreach (string formatClass in new[] { "CSV", "JSON", "YAML", "TOML", "INI", "XML" })
        {
            GodotObject instance = ClassDB.Instantiate(formatClass).AsGodotObject();
            Expect(Stored(instance).Contains("data"), $"{formatClass}.data is stored and in the editor (Raw Properties)");
        }
        Expect(Stored(new Csv()).Contains("header"), "CSV.header is stored and in the editor");
        var csv = new Csv
        {
            Data = new Array { new[] { "a", "b" } },
            Header = ["x", "y"],
        };
        ExpectEqual(csv.GetCell(0, 1), "b", "Data stays scriptable");
        ExpectEqual(csv.FindColumn("y"), 1, "Header stays scriptable");
    }

    private const string IniText = "version=2\n\n[player]\nname=\"knight\"\nhp=100\n\n[audio]\nvolume=0.8\n";
    private const string XmlText = "<units version=\"1\"><unit id=\"knight\" hp=\"100\"/><unit id=\"archer\" hp=\"80\"/></units>\n";
    private const string YamlText = "name: knight\n---\nname: archer\n---\n- 1\n- 2\n";
    private const string TomlText = "title = \"Game\"\n\n[server]\nport = 8080\n\n[[unit]]\nname = \"knight\"\n\n[[unit]]\nname = \"archer\"\n";
    private const string CsvText = "name,hp\nknight,100\narcher,80\n";

    private void RunPerFormat()
    {
        TestIni();
        TestXml();
        TestYaml();
        TestToml();
        TestCsv();
        TestTextDocument();
    }

    private void TestIni()
    {
        var ini = new Ini();
        ExpectEqual(ini.Parse(IniText), Error.Ok, "INI parses");
        GDict sections = ini.Data.AsGodotDictionary();
        Expect(sections.Count == 3, "INI: 3 sections (one without a name)");
        Expect(sections.ContainsKey(""), "INI: keys without a section are in section \"\"");
        Expect(ini.GetValue("", "version").AsInt32() == 2, "INI: version without a section");
        Expect(ini.GetValue("player", "hp").VariantType == Variant.Type.Int, "INI: hp is an int");
        Expect(ini.GetValue("audio", "volume").VariantType == Variant.Type.Float, "INI: volume is a float");
    }

    private void TestXml()
    {
        var xml = new Xml();
        ExpectEqual(xml.Parse(XmlText), Error.Ok, "XML parses");
        GDict root = xml.Data.AsGodotDictionary();
        Expect(root["name"].AsString() == "units", "XML: root element units");
        GArray children = root["children"].AsGodotArray();
        Expect(children.Count == 2, "XML: 2 child nodes");
        GDict archer = children[1].AsGodotDictionary();
        Expect(archer["attributes"].AsGodotDictionary()["id"].AsString() == "archer", "XML: attribute id of the second unit");
    }

    private void TestYaml()
    {
        var yaml = new Yaml();
        ExpectEqual(yaml.Parse(YamlText), Error.Ok, "YAML parses");
        Expect(yaml.GetDocumentCount() == 3, "YAML: 3 documents");
        Expect(yaml.Data.AsGodotDictionary()["name"].AsString() == "knight", "YAML: data is the first document");
        Expect(yaml.GetDocument(1).AsGodotDictionary()["name"].AsString() == "archer", "YAML: second document");
        Expect(yaml.GetDocument(2).AsGodotArray().Count == 2, "YAML: third document has 2 items");
    }

    private void TestToml()
    {
        var toml = new Toml();
        ExpectEqual(toml.Parse(TomlText), Error.Ok, "TOML parses");
        GDict root = toml.Data.AsGodotDictionary();
        Expect(root["server"].VariantType == Variant.Type.Dictionary, "TOML: [server] is a table");
        Expect(root["unit"].VariantType == Variant.Type.Array, "TOML: [[unit]] is an array");
        GArray units = root["unit"].AsGodotArray();
        Expect(units.Count == 2, "TOML: 2 units");
        Expect(units[1].VariantType == Variant.Type.Dictionary, "TOML: each unit is a table");
        Expect(root["title"].AsString() == "Game", "TOML: title is a key");
    }

    private void TestCsv()
    {
        var csv = new Csv();
        ExpectEqual(csv.Parse(CsvText), Error.Ok, "CSV parses");
        Expect(csv.Header.SequenceEqual(new[] { "name", "hp" }), "CSV: header stays stored");
        Expect(csv.GetRowCount() == 2, "CSV: 2 rows");
        Expect(csv.GetColumnCount() == 2, "CSV: 2 columns");
    }

    private void TestTextDocument()
    {
        var texts = new (string Class, string Text)[]
        {
            ("CSV", CsvText),
            ("INI", IniText),
            ("XML", XmlText),
            ("TOML", TomlText),
            ("YAML", YamlText),
        };
        foreach (var (cls, text) in texts)
        {
            var format = ClassDB.Instantiate(cls).As<TextDataFormat>();
            Expect(format != null, cls + " is a TextDataFormat");
            if (format == null)
            {
                continue;
            }
            ExpectEqual(format.Parse(text, true), Error.Ok, cls + " parses with kept text");
            Expect(format.GetParsedText() == text, cls + " keeps the parsed text");
            Expect(format.EncodeToText() != "", cls + " encodes its data");
            Expect(format.GetRecognizedExtensions().Length > 0, cls + " has file extensions");
        }

        var broken = new (string Class, string Text)[]
        {
            ("INI", "[broken\n"),
            ("XML", "<a>"),
            ("TOML", "[a\n"),
            ("YAML", "a: [1,\n"),
        };
        foreach (var (cls, text) in broken)
        {
            var format = ClassDB.Instantiate(cls).As<TextDataFormat>();
            Expect(format.Parse(text) != Error.Ok, cls + " reports a parse error");
            Expect(format.GetErrorMessage() != "", cls + " has an error message");
            Expect(format.GetErrorLine() > 0, cls + " has an error line");
        }
    }
}
