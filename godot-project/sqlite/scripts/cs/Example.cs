using Godot;

namespace TruelyInteractive.Sqlite;

/// <summary>Minimal example of SQLite: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string Items = "res://sqlite/resources/items.db";

    [Export] public SQLiteDatabase ItemsDb { get; set; } // Drag a .db file from the FileSystem dock.

    public override void _Ready()
    {
        // Setup: start with a fresh save database.
        DirAccess.RemoveAbsolute("user://save.db");

        // Table editor: a game reads the saved data as usual.
        var items = GD.Load<SQLiteDatabase>(Items);
        items.Open();
        GD.Print(items.Query("SELECT name, price FROM items ORDER BY id;"));
        items.Close();

        // Database files as resources.
        var shipped = GD.Load<SQLiteDatabase>(Items);
        if (shipped.Open() != Error.Ok) // Read-only: res:// can't be written.
        {
            GD.PushError(shipped.GetLastErrorMessage());
        }
        shipped.Close();

        // Open or create a database.
        var db = new SQLiteDatabase();
        if (db.Open("user://save.db") != Error.Ok) // Creates the file if missing.
        {
            GD.PushError(db.GetLastErrorMessage());
            return;
        }
        // Existing file only: db.Open("user://save.db", SQLiteDatabase.OpenMode.ReadWrite);
        GD.Print("Open: ", db.GetDatabasePath());
        db.Close();
        db.Open("user://save.db"); // The example goes on with the open database.

        // Execute SQL with parameters.
        db.Execute("CREATE TABLE IF NOT EXISTS scores (name TEXT, points INTEGER);");
        db.Execute("INSERT INTO scores VALUES (?, ?);", new Godot.Collections.Array { "Ada", 120 });
        db.Execute("INSERT INTO scores VALUES (:name, :points);",
            new Godot.Collections.Dictionary { { "name", "Bob" }, { "points", 80 } });
        GD.Print(db.GetChanges(), " ", db.GetLastInsertRowid());

        // Read rows.
        var rows = db.Query("SELECT name, points FROM scores WHERE points > :min;",
            new Godot.Collections.Dictionary { { "min", 100 } });
        foreach (var row in rows)
        {
            GD.Print(row["name"], ": ", row["points"]);
        }
        if (db.GetLastError() != Error.Ok)
        {
            GD.Print(db.GetLastErrorMessage());
        }

        // Prepared statements (the guide's entries are two sample scores).
        (string Name, int Points)[] entries = [("Cy", 50), ("Dee", 95)];
        SQLiteStatement insert = db.Prepare("INSERT INTO scores VALUES (?1, ?2);");
        foreach (var entry in entries)
        {
            insert.BindText(1, entry.Name);
            insert.BindInt(2, entry.Points);
            insert.Step();
            insert.Reset();
        }

        SQLiteStatement select = db.Prepare("SELECT name, points FROM scores;");
        while (select.Step() == SQLiteStatement.StepResult.Row)
        {
            GD.Print(select.GetColumnText(0), " ", select.GetColumnInt(1));
        }

        // Transactions.
        db.BeginTransaction();
        for (int i = 0; i < 1000; i++)
        {
            db.Execute("INSERT INTO scores VALUES (?, ?);", new Godot.Collections.Array { "p" + i, i });
        }
        if (db.Commit() != Error.Ok)
        {
            db.Rollback();
        }
        GD.Print("Rows: ", db.GetRowCount("scores"));

        // Databases in res:// and PCK files: copy to user:// to modify a shipped database.
        DirAccess.CopyAbsolute(Items, "user://items.db");
        var copy = new SQLiteDatabase();
        copy.Open("user://items.db");
        Error result = copy.Execute("INSERT INTO items (name) VALUES ('bow');");
        GD.Print("Copy is writable: ", result == Error.Ok);
        copy.Close();
        DirAccess.RemoveAbsolute("user://items.db");

        // Tables and columns.
        foreach (string table in db.GetTableNames())
        {
            GD.Print(table, ": ", db.GetColumnCount(table), " columns, ", db.GetRowCount(table), " rows");
            foreach (SQLiteColumn column in db.GetColumns(table))
            {
                GD.Print("  ", column.Name, " ", column.DeclaredType, " key: ", column.PrimaryKeyIndex);
            }
        }
        db.Close();
        GetTree().Quit();
    }
}
