using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GDict = Godot.Collections.Dictionary;

namespace TruelyInteractive.Issue361FeatureDataFileInspector;

/// <summary>
/// C# checks of 361-feature-data-file-inspector. Check the same things as scripts/gd/check.gd.
/// The inspector view of data files is editor UI without own API. These checks cover what it shows and promises:
/// the sample files of resources/ load as their format class with the counts of the summary line, <c>data</c> (and the
/// CSV <c>header</c>) stay stored, scriptable properties although the view hides their editors, and units.csv has the
/// three schema issues the summary reports. scripts/gd/measure_inspector.gd measures the inspector in the editor.
/// </summary>
public partial class Issue361FeatureDataFileInspectorCheck : FeatureCheckCs
{
    private const string Res = "res://361-feature-data-file-inspector/resources/";

    protected override string CheckName => "361-feature-data-file-inspector";

    protected override Task RunChecks()
    {
        CheckCsv();
        CheckTreeFormats();
        CheckStoredProperties();
        return Task.CompletedTask;
    }

    private void CheckCsv()
    {
        var csv = new Csv();
        ExpectEqual(csv.Load(Res + "units.csv"), Error.Ok, "units.csv loads");
        Expect(csv.GetRowCount() == 4 && csv.GetColumnCount() == 4, "units.csv: 4 rows, 4 columns");
        Expect(csv.Header.SequenceEqual(new[] { "name", "class", "hp", "ranged" }), "units.csv header");
        var schema = GD.Load<DataSchema>(Res + "units_schema.tres");
        Expect(schema != null, "units_schema.tres loads");
        if (schema != null)
        {
            Expect(csv.Validate(schema).GetIssueCount() == 3, "units.csv has 3 schema issues");
        }
    }

    private void CheckTreeFormats()
    {
        var json = GD.Load<Json>(Res + "enemies.json");
        Expect(json != null && json.Data.VariantType == Variant.Type.Dictionary && json.Data.AsGodotDictionary().Count == 2, "enemies.json: Dictionary, 2 keys");
        if (json != null && json.Data.VariantType == Variant.Type.Dictionary)
        {
            GDict data = json.Data.AsGodotDictionary();
            Expect(data["enemies"].AsGodotArray().Count == 2 && data["boss"].VariantType == Variant.Type.Nil, "enemies.json: 2 enemies, boss null");
        }

        var yaml = GD.Load<Yaml>(Res + "enemies.yaml");
        Expect(yaml != null && yaml.Data.VariantType == Variant.Type.Dictionary && yaml.Data.AsGodotDictionary().Count == 2, "enemies.yaml: Dictionary, 2 keys");
        if (yaml != null && yaml.Data.VariantType == Variant.Type.Dictionary)
        {
            Variant hp = yaml.Data.AsGodotDictionary()["enemies"].AsGodotArray()[0].AsGodotDictionary()["hp"];
            Expect(hp.VariantType == Variant.Type.Int, "enemies.yaml: hp is an int");
        }

        var toml = GD.Load<Toml>(Res + "server.toml");
        Expect(toml != null && toml.Data.VariantType == Variant.Type.Dictionary && toml.Data.AsGodotDictionary().Count == 3, "server.toml: Dictionary, 3 keys");
        if (toml != null && toml.Data.VariantType == Variant.Type.Dictionary)
        {
            Expect(toml.Data.AsGodotDictionary()["maps"].AsGodotArray().Count == 2, "server.toml: 2 maps");
        }

        var ini = GD.Load<Ini>(Res + "settings.ini");
        Expect(ini != null && ini.GetSections().SequenceEqual(new[] { "video", "audio" }), "settings.ini: 2 sections");
        if (ini != null)
        {
            Expect(ini.GetSectionKeys("video").Length + ini.GetSectionKeys("audio").Length == 6, "settings.ini: 6 keys");
        }

        var xml = GD.Load<Xml>(Res + "level.xml");
        Expect(xml != null && xml.GetRoot() != null, "level.xml loads");
        if (xml != null && xml.GetRoot() != null)
        {
            XmlElement root = xml.GetRoot();
            Expect(root.Name == "level" && root.GetChildren().Count == 3, "level.xml: root <level>, 3 child elements");
            Expect(root.GetAttributes().Count == 2, "level.xml: 2 attributes");
        }
    }

    private static bool IsStored(GodotObject godotObject, string name)
    {
        foreach (GDict property in godotObject.GetPropertyList())
        {
            int usage = (int)property["usage"];
            if ((string)property["name"] == name && (usage & (int)PropertyUsageFlags.Storage) != 0 && (usage & (int)PropertyUsageFlags.Editor) != 0)
            {
                return true;
            }
        }
        return false;
    }

    private void CheckStoredProperties()
    {
        // The inspector view hides only the editors: the properties stay stored and listed.
        foreach (string typeName in new[] { "CSV", "JSON", "YAML", "TOML", "INI", "XML" })
        {
            Expect(ClassDB.ClassExists(typeName), $"{typeName} exists");
            if (ClassDB.ClassExists(typeName))
            {
                Expect(IsStored(ClassDB.Instantiate(typeName).AsGodotObject(), "data"), $"{typeName}.data is stored and in the editor");
            }
        }
        Expect(IsStored(new Csv(), "header"), "CSV.header is stored and in the editor");

        var csv = new Csv
        {
            Data = new Godot.Collections.Array { new string[] { "a", "b" } },
            Header = new[] { "x", "y" },
        };
        Expect(csv.GetCell(0, 1) == "b" && csv.FindColumn("y") == 1, "data and header stay scriptable");
    }
}
