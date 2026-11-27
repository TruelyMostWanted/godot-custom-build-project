using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Sqlite;

/// <summary>Full API test of SQLite (SQLiteDatabase, SQLiteStatement, SQLiteColumn). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string DbPath = "user://sqlite_test.db";
    private const string ShippedDb = "res://sqlite/resources/items.db";
    private const string MissingDb = "user://sqlite_test_missing.db";
    private const string NewResDb = "res://sqlite/resources/new.db";
    private const string OddTable = "odd \"name";
    private const string SchemaSql = """
        CREATE TABLE items (id INTEGER PRIMARY KEY, name VARCHAR(40) NOT NULL DEFAULT 'none', weight DOUBLE, price DECIMAL(10,2));
        CREATE TABLE "odd ""name" (a TEXT, b INT, PRIMARY KEY (b, a));
        CREATE VIEW heavy AS SELECT id FROM items;
        CREATE VIRTUAL TABLE notes USING fts5(body);
        INSERT INTO items (name) VALUES ('a'), ('b');
        """;

    private readonly SQLiteDatabase _db = new();

    protected override Task RunTests()
    {
        RemoveDb();
        TestOpenAndClose();
        TestExecute();
        TestQuery();
        TestStatementBind();
        TestStatementRead();
        TestTransactions();
        TestErrors();
        TestOpenModes();
        TestShippedDatabase();
        TestSchema();
        TestResourceFile();
        RemoveDb();
        return Task.CompletedTask;
    }

    private static void RemoveDb()
    {
        DirAccess.RemoveAbsolute(DbPath);
        DirAccess.RemoveAbsolute(DbPath + "-journal");
    }

    private void ExpectStrings(string[] actual, string[] expected, string message)
    {
        Expect(actual.SequenceEqual(expected), $"{message} (expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}])");
    }

    private void TestOpenAndClose()
    {
        Expect(!_db.IsOpen(), "a new database is not open");
        ExpectEqual(_db.Execute("SELECT 1;"), Error.Unconfigured, "Execute on a closed database returns Unconfigured");
        ExpectEqual(_db.Open(DbPath), Error.Ok, "Open creates the database in user://");
        Expect(_db.IsOpen(), "IsOpen is true after Open");
        ExpectEqual(_db.GetDatabasePath(), DbPath, "GetDatabasePath returns the path");
        ExpectEqual(_db.GetOpenMode(), SQLiteDatabase.OpenMode.Create, "the default open mode is read-write-create");
        Expect(FileAccess.FileExists(DbPath), "the database file exists");
    }

    private void TestExecute()
    {
        ExpectEqual(_db.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL, data BLOB, note TEXT);"), Error.Ok, "CREATE TABLE");
        var arrayParameters = new Godot.Collections.Array { "Ada", 2.5, new byte[] { 1, 2, 3 }, default(Variant) };
        ExpectEqual(_db.Execute("INSERT INTO t (name, score, data, note) VALUES (?, ?, ?, ?);", arrayParameters), Error.Ok, "INSERT with Array parameters");
        ExpectEqual(_db.GetChanges(), 1, "GetChanges after INSERT");
        ExpectEqual(_db.GetLastInsertRowid(), 1L, "GetLastInsertRowid is 1");
        var namedParameters = new Godot.Collections.Dictionary { { "name", "Bob" }, { "score", 7 } };
        ExpectEqual(_db.Execute("INSERT INTO t (name, score) VALUES (:name, :score);", namedParameters), Error.Ok, "INSERT with Dictionary parameters");
        ExpectEqual(_db.GetLastInsertRowid(), 2L, "GetLastInsertRowid is 2");
        var dollarParameters = new Godot.Collections.Dictionary { { "n", "Cy" } };
        ExpectEqual(_db.Execute("INSERT INTO t (name) VALUES ($n);", dollarParameters), Error.Ok, "INSERT with a $name parameter");
    }

    private void TestQuery()
    {
        Godot.Collections.Array<Godot.Collections.Dictionary> rows = _db.Query("SELECT id, name, score, data, note FROM t ORDER BY id;");
        ExpectEqual(rows.Count, 3, "Query returns 3 rows");
        Godot.Collections.Dictionary first = rows[0];
        ExpectEqual(first["id"].AsInt64(), 1L, "an INTEGER column is an int");
        ExpectEqual(first["name"].AsString(), "Ada", "a TEXT column is a String");
        ExpectEqual(first["score"].AsDouble(), 2.5, "a REAL column is a float");
        Expect(first["data"].AsByteArray().SequenceEqual(new byte[] { 1, 2, 3 }), "a BLOB column is a PackedByteArray");
        Expect(first["note"].VariantType == Variant.Type.Nil, "a NULL column is null");
        var minimum = new Godot.Collections.Dictionary { { "min", 5 } };
        ExpectEqual(_db.Query("SELECT name FROM t WHERE score > :min;", minimum).Count, 1, "Query with a named parameter filters");
        var missingId = new Godot.Collections.Array { 99 };
        ExpectEqual(_db.Query("SELECT name FROM t WHERE id = ?;", missingId).Count, 0, "Query without a match is empty");
    }

    private void TestStatementBind()
    {
        SQLiteStatement insert = _db.Prepare("INSERT INTO t (name, score, data, note) VALUES (?1, ?2, ?3, ?4);");
        Expect(insert != null, "Prepare returns a statement");
        ExpectEqual(insert.GetBindParameterCount(), 4, "GetBindParameterCount");
        ExpectEqual(insert.BindText(1, "Dee"), Error.Ok, "BindText");
        ExpectEqual(insert.BindFloat(2, 1.5), Error.Ok, "BindFloat");
        ExpectEqual(insert.BindBlob(3, [9, 8]), Error.Ok, "BindBlob");
        ExpectEqual(insert.BindNull(4), Error.Ok, "BindNull");
        ExpectEqual(insert.Step(), SQLiteStatement.StepResult.Done, "Step of an INSERT is done");
        ExpectEqual(insert.Reset(), Error.Ok, "Reset");
        ExpectEqual(insert.BindText(1, "Eve"), Error.Ok, "bind again after Reset");
        ExpectEqual(insert.BindInt(2, 4), Error.Ok, "BindInt");
        ExpectEqual(insert.BindValue(3, System.Array.Empty<byte>()), Error.Ok, "BindValue with a blob");
        ExpectEqual(insert.BindValue(4, "x"), Error.Ok, "BindValue with a String");
        ExpectEqual(insert.Step(), SQLiteStatement.StepResult.Done, "the second INSERT is done");
        ExpectEqual(insert.BindInt(9, 1), Error.InvalidParameter, "bind with an index out of range fails");
        ExpectEqual(insert.ClearBindings(), Error.Ok, "ClearBindings");
        SQLiteStatement named = _db.Prepare("SELECT 1 WHERE :id = 1;");
        ExpectEqual(named.GetBindParameterIndex(":id"), 1, "GetBindParameterIndex");
        ExpectEqual(named.GetSql(), "SELECT 1 WHERE :id = 1;", "GetSql");
        SQLiteStatement invalid = _db.Prepare("SELEC");
        Expect(invalid == null, "Prepare of invalid SQL returns null");
        ExpectEqual(_db.GetLastError(), Error.QueryFailed, "a Prepare failure sets the last error");
    }

    private void TestStatementRead()
    {
        SQLiteStatement select = _db.Prepare("SELECT id, name, score, data, note FROM t WHERE name = ?;");
        ExpectEqual(select.BindText(1, "Dee"), Error.Ok, "BindText on a SELECT");
        ExpectEqual(select.GetColumnCount(), 5, "GetColumnCount");
        ExpectEqual(select.GetColumnName(1), "name", "GetColumnName");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Row, "Step returns a row");
        ExpectEqual(select.GetColumnType(0), SQLiteStatement.ColumnType.Integer, "column type integer");
        ExpectEqual(select.GetColumnType(1), SQLiteStatement.ColumnType.Text, "column type text");
        ExpectEqual(select.GetColumnType(2), SQLiteStatement.ColumnType.Float, "column type float");
        ExpectEqual(select.GetColumnType(3), SQLiteStatement.ColumnType.Blob, "column type blob");
        ExpectEqual(select.GetColumnType(4), SQLiteStatement.ColumnType.Null, "column type null");
        ExpectEqual(select.GetColumnInt(0), 4L, "GetColumnInt");
        ExpectEqual(select.GetColumnText(1), "Dee", "GetColumnText");
        ExpectEqual(select.GetColumnFloat(2), 1.5, "GetColumnFloat");
        Expect(select.GetColumnBlob(3).SequenceEqual(new byte[] { 9, 8 }), "GetColumnBlob");
        Expect(select.GetColumnValue(4).VariantType == Variant.Type.Nil, "GetColumnValue of NULL is null");
        Godot.Collections.Dictionary row = select.GetRow();
        ExpectEqual(row["name"].AsString(), "Dee", "GetRow");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Done, "Step is done after the last row");
        ExpectEqual(select.Reset(), Error.Ok, "Reset after the statement finished");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Row, "Reset runs the statement again");
        ExpectEqual(select.Reset(), Error.Ok, "Reset in the middle of a run");
        ExpectEqual(select.BindValues(new Godot.Collections.Array { "Eve" }), Error.Ok, "BindValues with an Array");
        ExpectEqual(select.Step(), SQLiteStatement.StepResult.Row, "a row for the new binding");
        ExpectEqual(select.GetColumnText(1), "Eve", "the bound text is read back");
        SQLiteStatement broken = _db.Prepare("SELECT no_such_column FROM t;");
        Expect(broken == null, "Prepare of SQL with an unknown column returns null");
        SQLiteStatement errorStep = _db.Prepare("SELECT abs(-9223372036854775808);");
        ExpectEqual(errorStep.Step(), SQLiteStatement.StepResult.Error, "Step reports Error for a failing statement");
    }

    private void TestTransactions()
    {
        Expect(!_db.IsInTransaction(), "there is no transaction at first");
        ExpectEqual(_db.BeginTransaction(), Error.Ok, "BeginTransaction");
        Expect(_db.IsInTransaction(), "IsInTransaction while active");
        _db.Execute("INSERT INTO t (name) VALUES ('rolled back');");
        ExpectEqual(_db.Rollback(), Error.Ok, "Rollback");
        Expect(!_db.IsInTransaction(), "there is no transaction after Rollback");
        ExpectEqual(_db.Query("SELECT id FROM t WHERE name = 'rolled back';").Count, 0, "Rollback discards the row");
        ExpectEqual(_db.BeginTransaction(), Error.Ok, "BeginTransaction again");
        _db.Execute("INSERT INTO t (name) VALUES ('kept');");
        ExpectEqual(_db.Commit(), Error.Ok, "Commit");
        Expect(!_db.IsInTransaction(), "there is no transaction after Commit");
        ExpectEqual(_db.Query("SELECT id FROM t WHERE name = 'kept';").Count, 1, "Commit keeps the row");
        ExpectEqual(_db.Commit(), Error.QueryFailed, "Commit without a transaction fails");
        ExpectEqual(_db.Rollback(), Error.QueryFailed, "Rollback without a transaction fails");
    }

    private void TestErrors()
    {
        ExpectEqual(_db.Execute("SELEC 1;"), Error.QueryFailed, "a syntax error returns QueryFailed");
        ExpectEqual(_db.GetLastError(), Error.QueryFailed, "GetLastError after a syntax error");
        Expect(_db.GetLastErrorMessage().Contains("syntax error"), "the error message names the syntax error");
        ExpectEqual(_db.Query("SELEC 1;").Count, 0, "Query of invalid SQL returns an empty array");
        var tooFew = new Godot.Collections.Array { "only one" };
        ExpectEqual(_db.Execute("INSERT INTO t (name, score) VALUES (?, ?);", tooFew), Error.InvalidParameter, "missing parameters return InvalidParameter");
        var unsupported = new Godot.Collections.Array { new Vector2(1, 2) };
        ExpectEqual(_db.Execute("INSERT INTO t (name) VALUES (?);", unsupported), Error.InvalidParameter, "an unsupported parameter type returns InvalidParameter");
        ExpectEqual(_db.Execute("SELECT 1;"), Error.Ok, "a successful call clears the error");
        ExpectEqual(_db.GetLastErrorMessage(), "", "the message is empty after success");
        _db.Close();
        Expect(!_db.IsOpen(), "Close closes the database");
    }

    private void TestOpenModes()
    {
        ExpectEqual(_db.Open(DbPath, SQLiteDatabase.OpenMode.ReadWrite), Error.Ok, "reopen with ReadWrite");
        ExpectEqual(_db.Query("SELECT id FROM t;").Count, 6, "the committed rows are persistent");
        _db.Close();
        ExpectEqual(_db.Open(DbPath, SQLiteDatabase.OpenMode.ReadOnly), Error.Ok, "open read-only");
        ExpectEqual(_db.GetOpenMode(), SQLiteDatabase.OpenMode.ReadOnly, "GetOpenMode is read-only");
        ExpectEqual(_db.Query("SELECT id FROM t;").Count, 6, "a read-only database can be queried");
        ExpectEqual(_db.Execute("INSERT INTO t (name) VALUES ('no');"), Error.FileNoPermission, "INSERT on a read-only database returns FileNoPermission");
        _db.Close();
        var missing = new SQLiteDatabase();
        ExpectEqual(missing.Open(MissingDb, SQLiteDatabase.OpenMode.ReadOnly), Error.FileNotFound, "a missing file in read-only mode returns FileNotFound");
        ExpectEqual(missing.Open(MissingDb, SQLiteDatabase.OpenMode.ReadWrite), Error.FileNotFound, "a missing file in read-write mode returns FileNotFound");
        ExpectEqual(_db.Open(NewResDb), Error.FileNoPermission, "a writable open on res:// returns FileNoPermission");
        RemoveDb();
    }

    private void TestShippedDatabase()
    {
        var shipped = new SQLiteDatabase();
        ExpectEqual(shipped.Open(ShippedDb, SQLiteDatabase.OpenMode.ReadOnly), Error.Ok, "open the database in res:// read-only");
        Godot.Collections.Array<Godot.Collections.Dictionary> count = shipped.Query("SELECT COUNT(*) AS n FROM items;");
        ExpectEqual(count[0]["n"].AsInt64(), 3L, "the shipped database has 3 rows");
        var names = new System.Collections.Generic.List<string>();
        foreach (Godot.Collections.Dictionary row in shipped.Query("SELECT name FROM items ORDER BY id;"))
        {
            names.Add(row["name"].AsString());
        }
        ExpectStrings(names.ToArray(), ["sword", "shield", "potion"], "the shipped rows are read in order");
        ExpectEqual(shipped.Execute("DELETE FROM items;"), Error.FileNoPermission, "writing to the shipped database is refused");
        shipped.Close();
        ExpectEqual(DirAccess.CopyAbsolute(ShippedDb, DbPath), Error.Ok, "CopyAbsolute copies the database to user://");
        ExpectEqual(shipped.Open(DbPath), Error.Ok, "open the copy");
        var bow = new Godot.Collections.Array { "bow" };
        ExpectEqual(shipped.Execute("INSERT INTO items (name) VALUES (?);", bow), Error.Ok, "the copy is writable");
        ExpectEqual(shipped.Query("SELECT id FROM items;").Count, 4, "the copy has 4 rows");
        shipped.Close();
        RemoveDb();
    }

    private void TestSchema()
    {
        ExpectEqual(_db.Open(DbPath), Error.Ok, "open a database for the schema tests");
        ExpectStrings(_db.GetTableNames(), [], "an empty database has no tables");
        ExpectEqual(_db.Execute(SchemaSql), Error.Ok, "create tables, a view and a virtual table");
        ExpectStrings(_db.GetTableNames(), ["items", "notes", OddTable], "GetTableNames lists tables and virtual tables by name, without views and shadow tables");
        Expect(_db.HasTable("ITEMS"), "HasTable ignores case");
        Expect(_db.HasTable(OddTable), "HasTable with a quote in the name");
        Expect(!_db.HasTable("heavy"), "a view is not a table");
        Expect(!_db.HasTable("notes_data"), "FTS5 shadow tables are hidden");
        Godot.Collections.Array<SQLiteColumn> columns = _db.GetColumns("items");
        ExpectEqual(columns.Count, 4, "GetColumns returns every column");
        ExpectEqual(columns[0].Name, "id", "SQLiteColumn.Name");
        ExpectEqual(columns[0].PrimaryKeyIndex, 1, "INTEGER PRIMARY KEY is key column 1");
        ExpectEqual(columns[0].Affinity, SQLiteColumn.AffinityEnum.Integer, "INTEGER has INTEGER affinity");
        ExpectEqual(columns[1].DeclaredType, "VARCHAR(40)", "SQLiteColumn.DeclaredType is as written");
        Expect(columns[1].NotNull, "SQLiteColumn.NotNull");
        ExpectEqual(columns[1].DefaultValue, "'none'", "SQLiteColumn.DefaultValue is SQL text");
        ExpectEqual(columns[1].PrimaryKeyIndex, 0, "a column outside the key has index 0");
        ExpectEqual(columns[1].Affinity, SQLiteColumn.AffinityEnum.Text, "VARCHAR has TEXT affinity");
        ExpectEqual(columns[2].Affinity, SQLiteColumn.AffinityEnum.Real, "DOUBLE has REAL affinity");
        ExpectEqual(columns[2].DefaultValue, "", "no default is an empty string");
        ExpectEqual(columns[3].Affinity, SQLiteColumn.AffinityEnum.Numeric, "DECIMAL has NUMERIC affinity");
        Godot.Collections.Array<SQLiteColumn> blobTable = _db.GetColumns("notes");
        ExpectEqual(blobTable[0].Affinity, SQLiteColumn.AffinityEnum.Blob, "a column without a type has BLOB affinity");
        Godot.Collections.Array<SQLiteColumn> odd = _db.GetColumns(OddTable);
        Expect(odd[0].PrimaryKeyIndex == 2 && odd[1].PrimaryKeyIndex == 1, "composite key positions");
        ExpectStrings(_db.GetColumnNames("items"), ["id", "name", "weight", "price"], "GetColumnNames");
        ExpectEqual(_db.GetColumnCount("items"), 4, "GetColumnCount");
        ExpectEqual(_db.GetRowCount("items"), 2, "GetRowCount");
        ExpectEqual(_db.GetRowCount("Items"), 2, "GetRowCount ignores case");
        ExpectEqual(_db.GetRowCount(OddTable), 0, "GetRowCount of a quoted name");
        ExpectEqual(_db.GetRowCount("missing"), -1, "GetRowCount of an unknown table is -1");
        ExpectEqual(_db.GetLastError(), Error.DoesNotExist, "an unknown table sets DoesNotExist");
        ExpectEqual(_db.GetColumns("missing").Count, 0, "GetColumns of an unknown table is empty");
        ExpectEqual(_db.GetColumnCount("missing"), -1, "GetColumnCount of an unknown table is -1");
        ExpectEqual(_db.GetRowCount("items\"; DROP TABLE items; --"), -1, "a name with SQL in it is just an unknown table");
        Expect(_db.HasTable("items"), "the table is still there");
        _db.Close();
        RemoveDb();
    }

    private void TestResourceFile()
    {
        var loaded = GD.Load<SQLiteDatabase>(ShippedDb);
        Expect(!loaded.IsOpen(), "a loaded database is not open yet");
        ExpectEqual(loaded.ResourcePath, ShippedDb, "ResourcePath is the file");
        ExpectEqual(loaded.Open(), Error.Ok, "Open without a path opens the loaded file");
        ExpectEqual(loaded.GetOpenMode(), SQLiteDatabase.OpenMode.ReadOnly, "a file in res:// opens read-only");
        ExpectEqual(loaded.GetDatabasePath(), ShippedDb, "GetDatabasePath of the loaded file");
        ExpectStrings(loaded.GetTableNames(), ["items"], "the tables of the shipped database");
        ExpectEqual(loaded.GetRowCount("items"), 3, "the rows of the shipped database");
        loaded.Close();
        var empty = new SQLiteDatabase();
        ExpectEqual(empty.Open(), Error.InvalidParameter, "Open without a path needs a loaded file");
    }
}
