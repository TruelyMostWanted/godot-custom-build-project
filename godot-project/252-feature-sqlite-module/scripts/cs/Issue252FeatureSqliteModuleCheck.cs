using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue252FeatureSqliteModule;

/// <summary>C# checks of 252-feature-sqlite-module. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue252FeatureSqliteModuleCheck : FeatureCheckCs
{
    private const string DbPath = "user://check_252.db";
    private const string ShippedDb = "res://252-feature-sqlite-module/data/items.db";

    protected override string CheckName => "252-feature-sqlite-module";

    private static void RemoveDb()
    {
        foreach (string suffix in new[] { "", "-journal" })
        {
            if (FileAccess.FileExists(DbPath + suffix))
            {
                DirAccess.RemoveAbsolute(DbPath + suffix);
            }
        }
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    protected override Task RunChecks()
    {
        Expect(ClassDB.ClassExists("SQLiteDatabase"), "ClassDB knows SQLiteDatabase");
        Expect(ClassDB.ClassExists("SQLiteStatement"), "ClassDB knows SQLiteStatement");
        RemoveDb();

        // Open or create.
        var db = new SQLiteDatabase();
        Expect(!db.IsOpen(), "a new database is not open");
        ExpectEqual(db.Execute("SELECT 1;"), Error.Unconfigured, "Execute on a closed database returns Unconfigured");
        ExpectEqual(db.Open(DbPath), Error.Ok, "Open creates the database in user://");
        Expect(db.IsOpen(), "IsOpen is true after Open");
        ExpectEqual(db.GetDatabasePath(), DbPath, "GetDatabasePath returns the path");
        ExpectEqual(db.GetOpenMode(), SQLiteDatabase.OpenMode.Create, "default open mode is read-write-create");
        Expect(FileAccess.FileExists(DbPath), "the database file exists");

        // Execute with Array and Dictionary params.
        ExpectEqual(db.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL, data BLOB, note TEXT);"), Error.Ok, "CREATE TABLE");
        ExpectEqual(db.Execute("INSERT INTO t (name, score, data, note) VALUES (?, ?, ?, ?);",
            new Godot.Collections.Array { "Ada", 2.5, new byte[] { 1, 2, 3 }, default(Variant) }), Error.Ok, "INSERT with Array params");
        ExpectEqual(db.GetChanges(), 1L, "GetChanges after INSERT");
        ExpectEqual(db.GetLastInsertRowid(), 1L, "GetLastInsertRowid is 1");
        ExpectEqual(db.Execute("INSERT INTO t (name, score) VALUES (:name, :score);",
            new Godot.Collections.Dictionary { { "name", "Bob" }, { "score", 7 } }), Error.Ok, "INSERT with Dictionary params");
        ExpectEqual(db.GetLastInsertRowid(), 2L, "GetLastInsertRowid is 2");
        ExpectEqual(db.Execute("INSERT INTO t (name) VALUES ($n);",
            new Godot.Collections.Dictionary { { "n", "Cy" } }), Error.Ok, "INSERT with $name param");

        // Query returns typed rows.
        Godot.Collections.Array<Godot.Collections.Dictionary> rows = db.Query("SELECT id, name, score, data, note FROM t ORDER BY id;");
        ExpectEqual(rows.Count, 3, "Query returns 3 rows");
        Godot.Collections.Dictionary first = rows[0];
        ExpectEqual(first["id"].VariantType, Variant.Type.Int, "id is an int");
        ExpectEqual(first["id"].AsInt64(), 1L, "int column");
        ExpectEqual(first["name"].AsString(), "Ada", "String column");
        Expect(first["score"].VariantType == Variant.Type.Float && Near(first["score"].AsDouble(), 2.5), "float column");
        Expect(first["data"].VariantType == Variant.Type.PackedByteArray && first["data"].AsByteArray().SequenceEqual(new byte[] { 1, 2, 3 }), "PackedByteArray column");
        ExpectEqual(first["note"].VariantType, Variant.Type.Nil, "NULL column is null");
        ExpectEqual(db.Query("SELECT name FROM t WHERE score > :min;", new Godot.Collections.Dictionary { { "min", 5 } }).Count, 1, "Query with named param filters");
        ExpectEqual(db.Query("SELECT name FROM t WHERE id = ?;", new Godot.Collections.Array { 99 }).Count, 0, "Query with no match is empty");

        // Prepared statements.
        SQLiteStatement insert = db.Prepare("INSERT INTO t (name, score, data, note) VALUES (?1, ?2, ?3, ?4);");
        Expect(insert != null, "Prepare returns a statement");
        ExpectEqual(insert.GetBindParameterCount(), 4L, "GetBindParameterCount");
        ExpectEqual(insert.BindText(1, "Dee"), Error.Ok, "BindText");
        ExpectEqual(insert.BindFloat(2, 1.5), Error.Ok, "BindFloat");
        ExpectEqual(insert.BindBlob(3, new byte[] { 9, 8 }), Error.Ok, "BindBlob");
        ExpectEqual(insert.BindNull(4), Error.Ok, "BindNull");
        ExpectEqual(insert.Step(), SQLiteStatement.StepResult.Done, "Step of INSERT is done");
        ExpectEqual(insert.Reset(), Error.Ok, "Reset");
        ExpectEqual(insert.BindText(1, "Eve"), Error.Ok, "bind again after Reset");
        ExpectEqual(insert.BindInt(2, 4), Error.Ok, "BindInt");
        ExpectEqual(insert.BindValue(3, new byte[0]), Error.Ok, "BindValue with a blob");
        ExpectEqual(insert.BindValue(4, "x"), Error.Ok, "BindValue with a String");
        ExpectEqual(insert.Step(), SQLiteStatement.StepResult.Done, "second INSERT is done");
        ExpectEqual(insert.BindInt(9, 1), Error.InvalidParameter, "bind with an index out of range");
        ExpectEqual(insert.ClearBindings(), Error.Ok, "ClearBindings");
        SQLiteStatement named = db.Prepare("SELECT 1 WHERE :id = 1;");
        ExpectEqual(named.GetBindParameterIndex(":id"), 1L, "GetBindParameterIndex");
        ExpectEqual(named.GetSql(), "SELECT 1 WHERE :id = 1;", "GetSql");

        SQLiteStatement select = db.Prepare("SELECT id, name, score, data, note FROM t WHERE name = ?;");
        ExpectEqual(select.BindText(1, "Dee"), Error.Ok, "BindText on SELECT");
        ExpectEqual(select.GetColumnCount(), 5L, "GetColumnCount");
        ExpectEqual(select.GetColumnName(1), "name", "GetColumnName");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Row, "Step returns a row");
        ExpectEqual(select.GetColumnType(0), SQLiteStatement.ColumnType.Integer, "column type integer");
        ExpectEqual(select.GetColumnType(1), SQLiteStatement.ColumnType.Text, "column type text");
        ExpectEqual(select.GetColumnType(2), SQLiteStatement.ColumnType.Float, "column type float");
        ExpectEqual(select.GetColumnType(3), SQLiteStatement.ColumnType.Blob, "column type blob");
        ExpectEqual(select.GetColumnType(4), SQLiteStatement.ColumnType.Null, "column type null");
        ExpectEqual(select.GetColumnInt(0), 4L, "GetColumnInt");
        ExpectEqual(select.GetColumnText(1), "Dee", "GetColumnText");
        Expect(Near(select.GetColumnFloat(2), 1.5), "GetColumnFloat");
        Expect(select.GetColumnBlob(3).SequenceEqual(new byte[] { 9, 8 }), "GetColumnBlob");
        ExpectEqual(select.GetColumnValue(4).VariantType, Variant.Type.Nil, "GetColumnValue of NULL is null");
        ExpectEqual(select.GetRow()["name"].AsString(), "Dee", "GetRow");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Done, "Step is done after the last row");
        ExpectEqual(select.Reset(), Error.Ok, "Reset after the statement finished");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Row, "Reset runs the statement again");
        ExpectEqual(select.Reset(), Error.Ok, "Reset in the middle of a run");
        ExpectEqual(select.BindValues(new Godot.Collections.Array { "Eve" }), Error.Ok, "BindValues with an Array");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Row, "row for the new binding");
        ExpectEqual(select.GetColumnText(1), "Eve", "bound text is read back");
        SQLiteStatement bad = db.Prepare("SELEC");
        Expect(bad == null, "Prepare of invalid SQL returns null");
        ExpectEqual(db.GetLastError(), Error.QueryFailed, "Prepare failure sets the last error");

        // Transactions.
        Expect(!db.IsInTransaction(), "no transaction at first");
        ExpectEqual(db.BeginTransaction(), Error.Ok, "BeginTransaction");
        Expect(db.IsInTransaction(), "IsInTransaction while active");
        db.Execute("INSERT INTO t (name) VALUES ('rolled back');");
        ExpectEqual(db.Rollback(), Error.Ok, "Rollback");
        Expect(!db.IsInTransaction(), "no transaction after rollback");
        ExpectEqual(db.Query("SELECT id FROM t WHERE name = 'rolled back';").Count, 0, "Rollback discards the row");
        ExpectEqual(db.BeginTransaction(), Error.Ok, "BeginTransaction again");
        db.Execute("INSERT INTO t (name) VALUES ('kept');");
        ExpectEqual(db.Commit(), Error.Ok, "Commit");
        Expect(!db.IsInTransaction(), "no transaction after commit");
        ExpectEqual(db.Query("SELECT id FROM t WHERE name = 'kept';").Count, 1, "Commit keeps the row");
        ExpectEqual(db.Commit(), Error.QueryFailed, "Commit without a transaction fails");

        // Errors.
        ExpectEqual(db.Execute("SELEC 1;"), Error.QueryFailed, "syntax error returns QueryFailed");
        ExpectEqual(db.GetLastError(), Error.QueryFailed, "GetLastError after a syntax error");
        Expect(db.GetLastErrorMessage().Contains("syntax error"), "error message names the syntax error");
        ExpectEqual(db.Query("SELEC 1;").Count, 0, "Query of invalid SQL returns an empty array");
        ExpectEqual(db.Execute("INSERT INTO t (name, score) VALUES (?, ?);", new Godot.Collections.Array { "only one" }), Error.InvalidParameter, "missing params return InvalidParameter");
        ExpectEqual(db.Execute("INSERT INTO t (name) VALUES (?);", new Godot.Collections.Array { new Vector2(1, 2) }), Error.InvalidParameter, "unsupported param type returns InvalidParameter");
        ExpectEqual(db.Execute("SELECT 1;"), Error.Ok, "a successful call clears the error");
        ExpectEqual(db.GetLastErrorMessage(), "", "message is empty after success");
        db.Close();
        Expect(!db.IsOpen(), "Close closes the database");

        // Reopen: data is persistent. Modes.
        ExpectEqual(db.Open(DbPath, SQLiteDatabase.OpenMode.ReadWrite), Error.Ok, "reopen with ReadWrite");
        ExpectEqual(db.Query("SELECT id FROM t;").Count, 6, "committed rows are persistent");
        db.Close();
        ExpectEqual(db.Open(DbPath, SQLiteDatabase.OpenMode.ReadOnly), Error.Ok, "open read-only");
        ExpectEqual(db.Query("SELECT id FROM t;").Count, 6, "read-only database can be queried");
        ExpectEqual(db.Execute("INSERT INTO t (name) VALUES ('no');"), Error.FileNoPermission, "INSERT on a read-only database returns FileNoPermission");
        db.Close();
        var missing = new SQLiteDatabase();
        ExpectEqual(missing.Open("user://check_252_missing.db", SQLiteDatabase.OpenMode.ReadOnly), Error.FileNotFound, "missing file in read-only mode returns FileNotFound");
        ExpectEqual(missing.Open("user://check_252_missing.db", SQLiteDatabase.OpenMode.ReadWrite), Error.FileNotFound, "missing file in read-write mode returns FileNotFound");
        ExpectEqual(db.Open("res://252-feature-sqlite-module/new.db"), Error.FileNoPermission, "writable open on res:// returns FileNoPermission");
        ExpectEqual(db.Open("res://252-feature-sqlite-module/new.db", SQLiteDatabase.OpenMode.ReadWrite), Error.FileNoPermission, "read-write open on res:// returns FileNoPermission");
        RemoveDb();

        // Read-only database shipped in res://.
        var shipped = new SQLiteDatabase();
        ExpectEqual(shipped.Open(ShippedDb, SQLiteDatabase.OpenMode.ReadOnly), Error.Ok, "open the database in res:// read-only");
        ExpectEqual(shipped.Query("SELECT COUNT(*) AS n FROM items;")[0]["n"].AsInt64(), 3L, "the shipped database has 3 rows");
        string[] names = shipped.Query("SELECT name FROM items ORDER BY id;").Select(row => row["name"].AsString()).ToArray();
        Expect(names.SequenceEqual(new[] { "sword", "shield", "potion" }), "the shipped rows are read in order");
        ExpectEqual(shipped.Execute("DELETE FROM items;"), Error.FileNoPermission, "writing to the shipped database is refused");
        shipped.Close();

        // Copy to user:// makes it writable.
        ExpectEqual(DirAccess.CopyAbsolute(ShippedDb, DbPath), Error.Ok, "CopyAbsolute copies the database to user://");
        ExpectEqual(shipped.Open(DbPath), Error.Ok, "open the copy");
        ExpectEqual(shipped.Execute("INSERT INTO items (name) VALUES (?);", new Godot.Collections.Array { "bow" }), Error.Ok, "the copy is writable");
        ExpectEqual(shipped.Query("SELECT id FROM items;").Count, 4, "the copy has 4 rows");
        shipped.Close();
        RemoveDb();

        // Schema API (#292).
        Expect(ClassDB.ClassExists("SQLiteColumn"), "ClassDB knows SQLiteColumn");
        ExpectEqual(db.Open(DbPath), Error.Ok, "open a database for the schema checks");
        ExpectEqual(db.GetTableNames().Length, 0, "an empty database has no tables");
        ExpectEqual(db.Execute(
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name VARCHAR(40) NOT NULL DEFAULT 'none', weight DOUBLE, price DECIMAL(10,2));"
            + "CREATE TABLE \"odd \"\"name\" (a TEXT, b INT, PRIMARY KEY (b, a));"
            + "CREATE VIEW heavy AS SELECT id FROM items;"
            + "CREATE VIRTUAL TABLE notes USING fts5(body);"
            + "INSERT INTO items (name) VALUES ('a'), ('b');"), Error.Ok, "create tables, a view and a virtual table");
        Expect(db.GetTableNames().SequenceEqual(new[] { "items", "notes", "odd \"name" }), "GetTableNames lists tables and virtual tables by name, without views and shadow tables");
        Expect(db.HasTable("ITEMS"), "HasTable ignores case");
        Expect(db.HasTable("odd \"name"), "HasTable with a quote in the name");
        Expect(!db.HasTable("heavy"), "a view is not a table");
        Expect(!db.HasTable("notes_data"), "FTS5 shadow tables are hidden");
        Godot.Collections.Array<SQLiteColumn> columns = db.GetColumns("items");
        ExpectEqual(columns.Count, 4, "GetColumns returns every column");
        ExpectEqual(columns[0].Name, "id", "column name");
        ExpectEqual(columns[0].PrimaryKeyIndex, 1, "INTEGER PRIMARY KEY is key column 1");
        ExpectEqual(columns[0].Affinity, SQLiteColumn.AffinityEnum.Integer, "INTEGER affinity");
        ExpectEqual(columns[1].DeclaredType, "VARCHAR(40)", "declared type as written");
        Expect(columns[1].NotNull, "NOT NULL");
        ExpectEqual(columns[1].DefaultValue, "'none'", "default value as SQL text");
        ExpectEqual(columns[1].PrimaryKeyIndex, 0, "not part of the key");
        ExpectEqual(columns[1].Affinity, SQLiteColumn.AffinityEnum.Text, "VARCHAR has TEXT affinity");
        ExpectEqual(columns[2].Affinity, SQLiteColumn.AffinityEnum.Real, "DOUBLE has REAL affinity");
        ExpectEqual(columns[2].DefaultValue, "", "no default is empty");
        ExpectEqual(columns[3].Affinity, SQLiteColumn.AffinityEnum.Numeric, "DECIMAL has NUMERIC affinity");
        Godot.Collections.Array<SQLiteColumn> odd = db.GetColumns("odd \"name");
        Expect(odd[0].PrimaryKeyIndex == 2 && odd[1].PrimaryKeyIndex == 1, "composite key positions");
        Expect(db.GetColumnNames("items").SequenceEqual(new[] { "id", "name", "weight", "price" }), "GetColumnNames");
        ExpectEqual(db.GetColumnCount("items"), 4, "GetColumnCount");
        ExpectEqual(db.GetRowCount("items"), 2L, "GetRowCount");
        ExpectEqual(db.GetRowCount("Items"), 2L, "GetRowCount ignores case");
        ExpectEqual(db.GetRowCount("odd \"name"), 0L, "GetRowCount of a quoted name");
        ExpectEqual(db.GetRowCount("missing"), -1L, "GetRowCount of an unknown table is -1");
        ExpectEqual(db.GetLastError(), Error.DoesNotExist, "unknown table sets DoesNotExist");
        ExpectEqual(db.GetColumns("missing").Count, 0, "GetColumns of an unknown table is empty");
        ExpectEqual(db.GetColumnCount("missing"), -1, "GetColumnCount of an unknown table is -1");
        ExpectEqual(db.GetRowCount("items\"; DROP TABLE items; --"), -1L, "a name with SQL in it is just an unknown table");
        Expect(db.HasTable("items"), "the table is still there");
        db.Close();
        RemoveDb();

        // Database files as resources (#292).
        var loaded = GD.Load<SQLiteDatabase>(ShippedDb);
        Expect(loaded != null, "Load returns an SQLiteDatabase for a .db file");
        Expect(!loaded.IsOpen(), "a loaded database is not open yet");
        ExpectEqual(loaded.ResourcePath, ShippedDb, "ResourcePath is the file");
        ExpectEqual(loaded.Open(), Error.Ok, "Open() without a path opens the loaded file");
        ExpectEqual(loaded.GetOpenMode(), SQLiteDatabase.OpenMode.ReadOnly, "a file in res:// opens read-only");
        ExpectEqual(loaded.GetDatabasePath(), ShippedDb, "GetDatabasePath of the loaded file");
        Expect(loaded.GetTableNames().SequenceEqual(new[] { "items" }), "tables of the shipped database");
        ExpectEqual(loaded.GetRowCount("items"), 3L, "rows of the shipped database");
        loaded.Close();
        ExpectEqual(new SQLiteDatabase().Open(), Error.InvalidParameter, "Open() without a path needs a loaded file");
        return Task.CompletedTask;
    }
}