using Godot;

namespace TruelyInteractive.DataFileInspector;

/// <summary>Minimal example of Data File Inspector: the snippets of the wiki guide, in order, one method per section. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // The same data from code
        SameDataFromCode();
        // A data check before the game starts
        DataCheck();
        GetTree().Quit();
    }

    private static void SameDataFromCode()
    {
        var units = new Csv();
        units.Load("res://csv/resources/units.csv");
        GD.Print($"{units.GetRowCount()} rows, {units.GetColumnCount()} columns");
        GD.Print(string.Join(", ", units.Header));
        var enemies = GD.Load<Json>("res://json/resources/enemies.json");
        GD.Print(enemies.Data.AsGodotDictionary()["enemies"].AsGodotArray().Count);
    }

    private static void DataCheck()
    {
        // new Csv() and Load(path) instead of GD.Load(): the engine prints a false "Resource file not found" for a Keep File CSV.
        var units = new Csv();
        units.Load("res://csv/resources/units.csv");
        var schema = GD.Load<DataSchema>("res://csv/resources/units_schema.tres");
        DataValidation result = units.Validate(schema);
        GD.Print($"{result.GetIssueCount()} issues");
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            GD.Print($"row {result.GetIssueRow(i)}, column {result.GetIssueColumn(i)}: {result.GetIssueMessage(i)}");
        }
        var enemies = GD.Load<Json>("res://json/resources/enemies.json");
        foreach (Variant enemy in enemies.Data.AsGodotDictionary()["enemies"].AsGodotArray())
        {
            var e = enemy.AsGodotDictionary();
            GD.Print(e["name"], " ", e["hp"]);
        }
    }
}
