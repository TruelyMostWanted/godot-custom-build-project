using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.DatasetDock;

/// <summary>Test of the runtime API the Dataset dock edits through (#436, #438). Same tests as scripts/gd/Test.gd.
/// The dock itself is editor UI (editor playtest scenario).</summary>
public partial class Test : FeatureTest
{
    protected override Task RunTests()
    {
        TestCsv();
        TestRecords();
        TestSectionsAndElements();
        return Task.CompletedTask;
    }

    private void TestCsv()
    {
        var csv = new Csv();
        ExpectEqual(csv.Parse("name,hp\nknight,100\narcher,80\n"), Error.Ok, "Csv.Parse()");
        ExpectEqual(csv.GetRowCount(), 2, "two rows");
        csv.InsertRow(1, new string[] { "mage", "60" });
        ExpectEqual(string.Join(",", csv.GetRow(1)), "mage,60", "InsertRow() (Insert Row Above)");
        csv.RemoveRow(1);
        ExpectEqual(csv.GetRowCount(), 2, "RemoveRow() (Delete Rows)");
        Array before = csv.GetCells(0, 1, 2, 1);
        csv.RemoveColumn(1);
        ExpectEqual(string.Join(",", csv.GetHeader()), "name", "RemoveColumn() (Remove Column)");
        csv.InsertColumn(1, "hp");
        csv.SetCells(0, 1, before);
        ExpectEqual(csv.GetCell(1, 1), "80", "undo of Remove Column: InsertColumn() + SetCells()");
        Expect(csv.GetParsedText().Length == 0, "an API change drops the kept text (the dock reloads the text editor)");
        var options = new SerializerOptions();
        options.CsvDelimiter = "\t";
        options.CsvHeaders = false;
        string tsv = Csv.Stringify(new Array { new string[] { "a", "b" } }, options);
        ExpectEqual(tsv.StripEdges(), "a\tb", "copy as tab-separated text");
    }

    private void TestRecords()
    {
        var json = new Json();
        string text = "{\"version\": 2, \"items\": [{\"id\": 7, \"name\": \"sword\"}, {\"id\": 8}]}";
        ExpectEqual(json.Parse(text, true), Error.Ok, "Json.Parse() with keepText");
        Array items = json.Data.AsGodotDictionary()["items"].AsGodotArray();
        ExpectEqual(items.Count, 2, "an array of dictionaries under a top-level key is a table");
        Dictionary second = items[1].AsGodotDictionary();
        Expect(!second.ContainsKey("name"), "a missing field is an empty cell");
        string next = Json.Stringify(json.Data, "\t", false);
        Expect(next.IndexOf("\"version\"") < next.IndexOf("\"items\""), "Stringify() without sorting keeps the key order");
        json.Parse(next, true);
        json.Parse(text, true);
        ExpectEqual(json.GetParsedText(), text, "undo parses the old text: the exact text comes back");

        var yaml = new Yaml();
        ExpectEqual(yaml.Parse("# Units\n- name: knight\n  hp: 100\n- name: archer\n  hp: 80\n", true), Error.Ok, "Yaml.Parse()");
        Array units = yaml.Data.AsGodotArray();
        ExpectEqual(units.Count, 2, "a YAML list of mappings is a table");
        Dictionary archer = units[1].AsGodotDictionary();
        ExpectEqual(archer["hp"].AsInt32(), 80, "YAML keeps integer types");
        Expect(yaml.GetParsedText().StartsWith("# Units"), "the kept YAML text has its comment");

        var toml = new Toml();
        ExpectEqual(toml.Parse("[[unit]]\nname = \"knight\"\nhp = 100\n\n[[unit]]\nname = \"archer\"\nhp = 80\n", true), Error.Ok, "Toml.Parse()");
        Array tables = toml.Data.AsGodotDictionary()["unit"].AsGodotArray();
        ExpectEqual(tables.Count, 2, "a TOML array of tables is a table");
        Dictionary knight = tables[0].AsGodotDictionary();
        ExpectEqual(knight["name"].AsString(), "knight", "TOML table values");
    }

    private void TestSectionsAndElements()
    {
        var ini = new Ini();
        ExpectEqual(ini.Parse("[knight]\nhp=100\n[archer]\nhp=80\n"), Error.Ok, "Ini.Parse()");
        ExpectEqual(string.Join(",", ini.GetSections()), "knight,archer", "INI sections are the rows");
        Dictionary sections = ini.Data.AsGodotDictionary().Duplicate(true);
        sections["mage"] = new Dictionary { { "hp", 60 } };
        ini.Data = sections;
        Expect(ini.EncodeToText().Contains("[mage]"), "a new section is written");

        var xml = new Xml();
        ExpectEqual(xml.Parse("<units><unit id=\"knight\" hp=\"100\"/><unit id=\"archer\"/><note>Text</note></units>"), Error.Ok, "Xml.Parse()");
        Array children = xml.Data.AsGodotDictionary()["children"].AsGodotArray();
        int unitCount = 0;
        foreach (Variant child in children)
        {
            if (child.VariantType == Variant.Type.Dictionary && child.AsGodotDictionary()["name"].AsString() == "unit")
            {
                unitCount++;
            }
        }
        ExpectEqual(unitCount, 2, "the children with one tag are the rows");
        Dictionary first = children[0].AsGodotDictionary();
        Dictionary attributes = first["attributes"].AsGodotDictionary();
        ExpectEqual(attributes["id"].AsString(), "knight", "attributes are the columns");
    }
}
