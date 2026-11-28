using Godot;
using Godot.Collections;

namespace TruelyInteractive.DatasetDock;

/// <summary>Minimal example of the Dataset dock: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Insert a column and write a block of cells.
        var csv = new Csv();
        csv.Parse("name,hp\nknight,100\narcher,80\n");
        csv.InsertColumn(1, "speed", "1.0");
        csv.SetCells(0, 2, new Array { new string[] { "120" } });
        GD.Print(string.Join(",", csv.GetHeader()));
        GD.Print(csv.GetCell(0, 2));

        // A JSON array of dictionaries is one row per dictionary.
        var json = new Json();
        json.Parse("[{\"name\": \"knight\", \"hp\": 100}]", true);
        Array rows = json.Data.AsGodotArray();
        GD.Print(rows.Count);
        GD.Print(json.GetParsedText());
        GetTree().Quit();
    }
}
