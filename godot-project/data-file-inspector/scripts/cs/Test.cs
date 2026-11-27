using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

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
}
