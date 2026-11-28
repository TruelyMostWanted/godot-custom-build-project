using Godot;

namespace TruelyInteractive.DatabaseDock;

/// <summary>Minimal example of the Database dock: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Shop = "res://database-dock/resources/shop.db";
    private const string Save = "user://shop_save_cs.db";

    public override void _Ready()
    {
        // Read the rows you edited in the dock.
        var shop = new SQLiteDatabase();
        shop.Open(Shop, SQLiteDatabase.OpenMode.ReadOnly);
        GD.Print(shop.Query("SELECT name, price FROM items ORDER BY id;"));

        // Read a view.
        GD.Print(shop.Query("SELECT name FROM cheap_items ORDER BY name;"));
        shop.Close();

        // Write like Save: all edits in one transaction.
        DirAccess.CopyAbsolute(Shop, Save);
        var save = new SQLiteDatabase();
        save.Open(Save);
        save.BeginTransaction();
        save.Execute("UPDATE items SET price = ? WHERE id = ?;", new Godot.Collections.Array { 0.75, 1 });
        save.Execute("INSERT INTO items (name) VALUES (?);", new Godot.Collections.Array { "Bread" });
        if (save.Commit() != Error.Ok)
        {
            save.Rollback();
        }
        GD.Print(save.Query("SELECT name, price FROM items ORDER BY id;"));
        save.Close();
        DirAccess.RemoveAbsolute(Save);

        GetTree().Quit();
    }
}
