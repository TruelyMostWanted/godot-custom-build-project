using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TruelyInteractive.CsvFormat;

/// <summary>Minimal example of CSV: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>Item and ItemTable are the GDScript classes of text-serialization/scripts/gd.</remarks>
public partial class Example : Node
{
    public List<Godot.Collections.Dictionary> Units { get; } = [];

    public override void _Ready()
    {
        // Using the API (the guide calls this CSV "units"; here "table", as in the next snippet).
        // new Csv() and Load(path) instead of GD.Load(): the engine prints a false "Resource file not found" for a Keep File CSV.
        var table = new Csv();
        table.Load("res://csv/resources/units.csv");
        int hp = table.FindColumn("hp");
        for (int row = 0; row < table.GetRowCount(); row++)
            GD.Print(table.GetCell(row, 0), ": ", table.GetCell(row, hp).ToInt(), " hp");
        GD.Print(table.GetRowAsDictionary(1));
        var schema = GD.Load<DataSchema>("res://csv/resources/units_schema.tres");
        Godot.Collections.Dictionary archer = table.GetTypedRow(1, schema);
        GD.Print((int)archer["hp"] + 10);

        // Add and remove columns.
        var units = new Csv();
        units.Parse("name,hp\narcher,60\nknight,120\n");
        units.InsertColumn(1, "class", "melee");
        units.SetCell(0, 1, "ranger");
        GD.Print(units.EncodeToText()); // Prints "name,class,hp\narcher,ranger,60\nknight,melee,120\n"
        units.RemoveColumn(units.FindColumn("hp"));
        GD.Print(string.Join(", ", units.Header)); // Prints "name, class"
        var players = new Csv();
        players.InsertColumn(0, "id");
        players.InsertColumn(1, "name");
        players.AppendRow(new[] { "1", "Ann" });
        players.Save("user://players.csv");

        // A unit roster with checked data (print instead of PushWarning: the C# warning trace contains "error", which fails the runner)
        DataValidation check = table.Validate(schema);
        var badRows = check.GetInvalidRows();
        for (int i = 0; i < check.GetIssueCount(); i++)
            GD.Print($"units.csv row {check.GetIssueRow(i) + 1}: {check.GetIssueMessage(i)}");
        for (int row = 0; row < table.GetRowCount(); row++)
        {
            if (!badRows.Contains(row))
                Units.Add(table.GetTypedRow(row, schema));
        }
        GD.Print(Units.Count, " units");

        // Translation CSVs in the dock
        var ui = new Csv();
        ui.Load("res://csv/resources/ui.csv");
        DataValidation translationCheck = ui.ValidateTranslation();
        for (int i = 0; i < translationCheck.GetIssueCount(); i++)
        {
            if (translationCheck.GetIssueKind(i) == DataValidation.IssueKind.EmptyRequired)
                GD.Print("Missing ", translationCheck.GetIssueField(i), " in row ", translationCheck.GetIssueRow(i));
        }

        // Save your own resource as CSV
        var itemScript = GD.Load<GDScript>("res://text-serialization/scripts/gd/item.gd");
        var sword = (Resource)itemScript.New();
        sword.Set("name", "Sword");
        var tableScript = GD.Load<GDScript>("res://text-serialization/scripts/gd/item_table.gd");
        var items = (Resource)tableScript.New();
        var entries = items.Get("entries").AsGodotArray();
        entries.Add(sword);
        if (ResourceTextSerializer.CanSerialize(items, ResourceTextSerializer.Format.Csv))
            ResourceSaver.Save(items, "user://items.csv");
        var loaded = ResourceLoader.Load("user://items.csv", "ItemTable");
        var loadedEntries = loaded.Get("entries").AsGodotArray();
        GD.Print(loadedEntries[0].As<Resource>().Get("name"));
        GetTree().Quit();
    }
}
