using Godot;
using Godot.Collections;

namespace TruelyInteractive.DataSchemas;

/// <summary>Minimal example of Data Schemas: the snippets of the wiki guide, in order, one method per section. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        UsingTheApi();
        BuildInCode();
        // Lines, options and the result
        Lines();
        // Validate an INI file
        ValidateIni();
        // Validate JSON
        ValidateJson();
        // Validate YAML and TOML
        ValidateYaml();
        // Validate XML
        ValidateXml();
        // Check all data files in one step: an EditorScript, editor only (File > Run in the script editor).
        GetTree().Quit();
    }

    private static void UsingTheApi()
    {
        // new Csv() and Load(path) instead of GD.Load(): the engine prints a false "Resource file not found" for a Keep File CSV.
        var units = new Csv();
        units.Load("res://csv/resources/units.csv");
        var schema = GD.Load<DataSchema>("res://csv/resources/units_schema.tres");
        DataValidation result = units.Validate(schema);
        for (int i = 0; i < result.GetIssueCount(); i++)
            GD.Print($"Row {result.GetIssueRow(i)}, column {result.GetIssueColumn(i)}: {result.GetIssueMessage(i)}");
    }

    private static void BuildInCode()
    {
        var schema = DataSchema.CreateFromDictionary(new Dictionary
        {
            { "name", (int)Variant.Type.String },
            { "hp", (int)Variant.Type.Int },
            { "ranged", (int)Variant.Type.Bool },
        });
        schema.GetField("name").Unique = true;
        schema.GetField("hp").SetRange(1, 200);
        var unitClass = new DataField { Name = "class" };
        unitClass.SetEnumValues(["warrior", "ranger", "caster"]);
        schema.AddField(unitClass);
        GD.Print("Fields: ", schema.Fields.Count);
    }

    private static void Lines()
    {
        var schema = GD.Load<DataSchema>("res://csv/resources/units_schema.tres");
        var options = new DataValidationOptions { KeepPositions = true };
        var units = new Csv();
        units.Parse(FileAccess.GetFileAsString("res://csv/resources/units.csv"), true);
        DataValidation result = units.Validate(schema, options);
        GD.Print(result.GetIssueLine(0));
    }

    private static void ValidateIni()
    {
        var section = new DataField
        {
            Name = "section",
            Type = Variant.Type.Dictionary,
            Schema = DataSchema.CreateFromDictionary(new Dictionary { { "hp", (int)Variant.Type.Int }, { "name", (int)Variant.Type.String } }),
        };
        var schema = new DataSchema { ExtraFields = DataSchema.ExtraFieldsEnum.MatchTemplate, ExtraFieldTemplate = section };
        DataValidation result = GD.Load<Ini>("res://data-schemas/resources/monsters.ini").Validate(schema);
        for (int i = 0; i < result.GetIssueCount(); i++)
            GD.Print(result.GetIssuePath(i), ": ", result.GetIssueMessage(i));
    }

    private static void ValidateJson()
    {
        var schema = DataSchema.CreateFromDictionary(new Dictionary
        {
            { "name", (int)Variant.Type.String },
            { "hp", (int)Variant.Type.Int },
            { "drops", new Array { (int)Variant.Type.String } },
        });
        string text = FileAccess.GetFileAsString("res://json/resources/enemies.json");
        DataValidation result = schema.Validate(Json.ParseString(text).AsGodotDictionary()["enemies"]);
        result.ResolveLines(text);
        for (int i = 0; i < result.GetIssueCount(); i++)
            GD.Print("Line ", result.GetIssueLine(i), ": ", result.GetIssueMessage(i));
    }

    private static void ValidateYaml()
    {
        var enemy = new Dictionary { { "name", (int)Variant.Type.String }, { "hp", (int)Variant.Type.Int } };
        var schema = DataSchema.CreateFromDictionary(new Dictionary { { "enemies", new Array { enemy } } });
        schema.ExtraFields = DataSchema.ExtraFieldsEnum.Allow;
        schema.GetField("enemies").ElementSchema.ExtraFields = DataSchema.ExtraFieldsEnum.Allow;
        var options = new DataValidationOptions();
        DataValidation result = GD.Load<Yaml>("res://yaml/resources/enemies.yaml").Validate(schema, options);
        GD.Print("enemies.yaml valid: ", result.IsValid());
    }

    private static void ValidateXml()
    {
        var spawn = DataSchema.CreateFromDictionary(new Dictionary
        {
            { "enemy", (int)Variant.Type.String }, { "x", (int)Variant.Type.Int }, { "y", (int)Variant.Type.Int },
        });
        var level = DataSchema.CreateFromDictionary(new Dictionary
        {
            { "name", (int)Variant.Type.String }, { "time", (int)Variant.Type.Int }, { "music", (int)Variant.Type.String },
        });
        level.AddField(new DataField { Name = "spawn", Type = Variant.Type.Array, ElementSchema = spawn });
        DataValidation result = GD.Load<Xml>("res://xml/resources/level.xml").Validate(level);
        GD.Print("level.xml valid: ", result.IsValid());
    }
}
