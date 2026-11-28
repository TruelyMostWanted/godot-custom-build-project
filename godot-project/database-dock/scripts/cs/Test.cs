using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.DatabaseDock;

/// <summary>Test of the SQLite API the Database dock uses: the SQL of its side bar, pages, Save, Table menu and Query.
/// Same tests as scripts/gd/Test.gd. The dock itself is editor UI (editor playtest scenario pt_437).</summary>
public partial class Test : FeatureTest
{
    private const string ShopDb = "res://database-dock/resources/shop.db";
    private const string DbPath = "user://database_dock_test_cs.db";
    private const string SchemaSql = """
        CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL DEFAULT 1.0);
        CREATE TABLE tags (item_id INTEGER, tag TEXT);
        CREATE TABLE pairs (b INTEGER, a TEXT, value TEXT, PRIMARY KEY (a, b)) WITHOUT ROWID;
        CREATE VIEW cheap_items AS SELECT name, price FROM items WHERE price < 5;
        INSERT INTO items (name, price) VALUES ('Apple', 0.5), ('Sword', 120.0), ('Potion', 4.5);
        INSERT INTO tags VALUES (1, 'food'), (2, 'weapon');
        """;
    private const string KeySql = "SELECT name FROM pragma_table_info(?) WHERE pk > 0 ORDER BY pk;";

    private readonly SQLiteDatabase _db = new();

    protected override Task RunTests()
    {
        DirAccess.RemoveAbsolute(DbPath);
        ExpectEqual(_db.Open(DbPath), Error.Ok, "open a fresh database");
        ExpectEqual(_db.Execute(SchemaSql), Error.Ok, "create the schema");
        TestShippedDatabase();
        TestSideBar();
        TestKeys();
        TestPage();
        TestSave();
        TestFailedSave();
        TestTableMenu();
        TestQuery();
        _db.Close();
        DirAccess.RemoveAbsolute(DbPath);
        return Task.CompletedTask;
    }

    private void TestShippedDatabase()
    {
        var shop = new SQLiteDatabase();
        ExpectEqual(shop.Open(ShopDb, SQLiteDatabase.OpenMode.ReadOnly), Error.Ok, "the sample database opens read-only");
        ExpectEqual(string.Join(",", shop.GetTableNames()), "items,tags", "the sample tables");
        ExpectEqual(shop.GetRowCount("items"), 4L, "the sample items");
        shop.Close();
    }

    private void TestSideBar()
    {
        ExpectEqual(string.Join(",", _db.GetTableNames()), "items,pairs,tags", "tables, sorted by name, without views");
        var views = _db.Query("SELECT name FROM pragma_table_list WHERE schema = 'main' AND type = 'view' ORDER BY name;");
        ExpectEqual(views.Count, 1, "one view");
        ExpectEqual(views[0]["name"].AsString(), "cheap_items", "the view name");
        var viewColumns = _db.Query("SELECT name, type FROM pragma_table_info(?) ORDER BY cid;", new Godot.Collections.Array { "cheap_items" });
        ExpectEqual(viewColumns.Count, 2, "pragma_table_info lists the columns of a view");
        ExpectEqual(viewColumns[0]["name"].AsString(), "name", "the first view column");
    }

    private void TestKeys()
    {
        var itemKeys = _db.Query(KeySql, new Godot.Collections.Array { "items" });
        ExpectEqual(itemKeys.Count, 1, "items has one key column");
        ExpectEqual(itemKeys[0]["name"].AsString(), "id", "the key column of items");
        var tagKeys = _db.Query(KeySql, new Godot.Collections.Array { "tags" });
        ExpectEqual(tagKeys.Count, 0, "tags has no primary key: the dock uses rowid");
        var pairKeys = _db.Query(KeySql, new Godot.Collections.Array { "pairs" });
        ExpectEqual(pairKeys.Count, 2, "pairs has a composite key");
        ExpectEqual(pairKeys[0]["name"].AsString(), "a", "key columns come in key order, not column order");
        var tagRows = _db.Query("SELECT rowid, * FROM tags ORDER BY rowid;");
        ExpectEqual(tagRows[1]["rowid"].AsInt64(), 2L, "rowid identifies a row without a primary key");
    }

    private void TestPage()
    {
        var count = _db.Query("SELECT COUNT(*) AS n FROM \"items\" WHERE \"name\" LIKE ?1 ESCAPE '\\' OR \"price\" LIKE ?1 ESCAPE '\\';", new Godot.Collections.Array { "%o%" });
        ExpectEqual(count[0]["n"].AsInt64(), 2L, "the filter matches every column with LIKE");
        var page = _db.Query("SELECT \"id\", * FROM \"items\" ORDER BY \"price\" DESC, \"id\" LIMIT ?1 OFFSET ?2;", new Godot.Collections.Array { 2, 1 });
        ExpectEqual(page.Count, 2, "one page with LIMIT and OFFSET");
        ExpectEqual(page[0]["name"].AsString(), "Potion", "sorted by a column title");
        var statement = _db.Prepare("SELECT \"id\", * FROM \"items\" LIMIT 1;");
        ExpectEqual(statement.Step(), SQLiteStatement.StepResult.Row, "a row of the page");
        ExpectEqual(statement.GetColumnCount(), 4, "the key value comes first, then the columns");
        statement.Reset();
    }

    private void TestSave()
    {
        ExpectEqual(_db.BeginTransaction(), Error.Ok, "Save starts one transaction");
        ExpectEqual(_db.Execute("UPDATE \"items\" SET \"price\" = ? WHERE \"id\" IS ?;", new Godot.Collections.Array { 0.75, 1 }), Error.Ok, "an edited cell");
        ExpectEqual(_db.Execute("DELETE FROM \"tags\" WHERE \"rowid\" IS ?;", new Godot.Collections.Array { 2 }), Error.Ok, "a deleted row");
        ExpectEqual(_db.Execute("INSERT INTO \"items\" (\"name\") VALUES (?);", new Godot.Collections.Array { "Bread" }), Error.Ok, "an inserted row");
        ExpectEqual(_db.Execute("INSERT INTO \"tags\" DEFAULT VALUES;"), Error.Ok, "an inserted row left at DEFAULT");
        ExpectEqual(_db.Commit(), Error.Ok, "Save commits");
        Expect(!_db.IsInTransaction(), "no transaction after Save");
        var bread = _db.Query("SELECT price FROM items WHERE name = 'Bread';");
        ExpectEqual(bread[0]["price"].AsDouble(), 1.0, "an inserted row gets the column default");
        ExpectEqual(_db.GetRowCount("tags"), 2L, "one tag deleted, one inserted");
    }

    private void TestFailedSave()
    {
        ExpectEqual(_db.BeginTransaction(), Error.Ok, "a second Save");
        ExpectEqual(_db.Execute("UPDATE \"items\" SET \"name\" = ? WHERE \"id\" IS ?;", new Godot.Collections.Array { "Green apple", 1 }), Error.Ok, "an edit of the first table");
        Expect(_db.Execute("INSERT INTO \"items\" (\"name\") VALUES (?);", new Godot.Collections.Array { default(Variant) }) != Error.Ok, "a NULL in a NOT NULL column fails");
        Expect(_db.GetLastErrorMessage().Contains("NOT NULL"), "the message names the constraint");
        Expect(_db.IsInTransaction(), "the transaction stays open after a failed row");
        ExpectEqual(_db.Rollback(), Error.Ok, "the dock rolls back");
        var apple = _db.Query("SELECT name FROM items WHERE id = 1;");
        ExpectEqual(apple[0]["name"].AsString(), "Apple", "nothing of the failed Save was written");
    }

    private void TestTableMenu()
    {
        ExpectEqual(_db.BeginTransaction(), Error.Ok, "Create with SQL runs in a transaction");
        ExpectEqual(_db.Execute("CREATE TABLE new_table (id INTEGER PRIMARY KEY, name TEXT NOT NULL DEFAULT '');"), Error.Ok, "the template SQL");
        ExpectEqual(_db.Commit(), Error.Ok, "Create with SQL commits");
        Expect(_db.HasTable("new_table"), "the new table exists");
        ExpectEqual(_db.BeginTransaction(), Error.Ok, "a failing Create with SQL");
        Expect(_db.Execute("CREATE TABLE broken (;") != Error.Ok, "invalid SQL fails");
        ExpectEqual(_db.Rollback(), Error.Ok, "and is rolled back");
        ExpectEqual(_db.Execute("ALTER TABLE \"new_table\" RENAME TO \"renamed\";"), Error.Ok, "Rename");
        Expect(_db.HasTable("renamed"), "the renamed table");
        Expect(!_db.HasTable("new_table"), "the old name is gone");
        Expect(_db.Execute("ALTER TABLE \"cheap_items\" RENAME TO \"x\";") != Error.Ok, "views can't be renamed");
        ExpectEqual(_db.Execute("DROP TABLE \"renamed\";"), Error.Ok, "Delete a table");
        Expect(!_db.HasTable("renamed"), "the table is gone");
        ExpectEqual(_db.Execute("CREATE VIEW v AS SELECT 1;"), Error.Ok, "a view to delete");
        ExpectEqual(_db.Execute("DROP VIEW \"v\";"), Error.Ok, "Delete a view");
    }

    private void TestQuery()
    {
        var reader = new SQLiteDatabase();
        ExpectEqual(reader.Open(DbPath, SQLiteDatabase.OpenMode.ReadOnly), Error.Ok, "Query opens a read-only connection");
        var statement = reader.Prepare("SELECT name, price FROM cheap_items ORDER BY name;");
        Expect(statement != null, "a SELECT on a view");
        ExpectEqual(statement.GetColumnName(1), "price", "the result columns");
        ExpectEqual(statement.Step(), SQLiteStatement.StepResult.Row, "the first result row");
        ExpectEqual(statement.GetColumnValue(0).AsString(), "Apple", "the first value");
        statement.Reset();
        Expect(reader.Execute("DELETE FROM items;") != Error.Ok, "a write on the Query connection fails");
        ExpectEqual(reader.GetLastError(), Error.FileNoPermission, "the write is refused as read-only");
        reader.Close();
        ExpectEqual(_db.GetRowCount("items"), 4L, "Query did not change the rows");
    }
}
