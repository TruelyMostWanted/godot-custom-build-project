extends FeatureTest
## Test of the SQLite API the Database dock uses: the SQL of its side bar, pages, Save, Table menu and Query.
## Same tests as scripts/cs/Test.cs. The dock itself is editor UI (editor playtest scenario pt_437).

const SHOP_DB := "res://database-dock/resources/shop.db"
const DB_PATH := "user://database_dock_test.db"
const SCHEMA_SQL := """
CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL DEFAULT 1.0);
CREATE TABLE tags (item_id INTEGER, tag TEXT);
CREATE TABLE pairs (b INTEGER, a TEXT, value TEXT, PRIMARY KEY (a, b)) WITHOUT ROWID;
CREATE VIEW cheap_items AS SELECT name, price FROM items WHERE price < 5;
INSERT INTO items (name, price) VALUES ('Apple', 0.5), ('Sword', 120.0), ('Potion', 4.5);
INSERT INTO tags VALUES (1, 'food'), (2, 'weapon');
"""
const KEY_SQL := "SELECT name FROM pragma_table_info(?) WHERE pk > 0 ORDER BY pk;"

var _db := SQLiteDatabase.new()


func _run_tests() -> void:
	DirAccess.remove_absolute(DB_PATH)
	expect_equal(_db.open(DB_PATH), OK, "open a fresh database")
	expect_equal(_db.execute(SCHEMA_SQL), OK, "create the schema")
	_test_shipped_database()
	_test_side_bar()
	_test_keys()
	_test_page()
	_test_save()
	_test_failed_save()
	_test_table_menu()
	_test_query()
	_db.close()
	DirAccess.remove_absolute(DB_PATH)


func _test_shipped_database() -> void:
	var shop := SQLiteDatabase.new()
	expect_equal(shop.open(SHOP_DB, SQLiteDatabase.OPEN_MODE_READ_ONLY), OK, "the sample database opens read-only")
	expect_equal(shop.get_table_names(), PackedStringArray(["items", "tags"]), "the sample tables")
	expect_equal(shop.get_row_count("items"), 4, "the sample items")
	shop.close()


func _test_side_bar() -> void:
	expect_equal(_db.get_table_names(), PackedStringArray(["items", "pairs", "tags"]), "tables, sorted by name, without views")
	var views := _db.query("SELECT name FROM pragma_table_list WHERE schema = 'main' AND type = 'view' ORDER BY name;")
	expect_equal(views.size(), 1, "one view")
	expect_equal(views[0]["name"], "cheap_items", "the view name")
	var view_columns := _db.query("SELECT name, type FROM pragma_table_info(?) ORDER BY cid;", ["cheap_items"])
	expect_equal(view_columns.size(), 2, "pragma_table_info lists the columns of a view")
	expect_equal(view_columns[0]["name"], "name", "the first view column")


func _test_keys() -> void:
	var item_keys := _db.query(KEY_SQL, ["items"])
	expect_equal(item_keys.size(), 1, "items has one key column")
	expect_equal(item_keys[0]["name"], "id", "the key column of items")
	var tag_keys := _db.query(KEY_SQL, ["tags"])
	expect_equal(tag_keys.size(), 0, "tags has no primary key: the dock uses rowid")
	var pair_keys := _db.query(KEY_SQL, ["pairs"])
	expect_equal(pair_keys.size(), 2, "pairs has a composite key")
	expect_equal(pair_keys[0]["name"], "a", "key columns come in key order, not column order")
	var tag_rows := _db.query("SELECT rowid, * FROM tags ORDER BY rowid;")
	expect_equal(tag_rows[1]["rowid"], 2, "rowid identifies a row without a primary key")


func _test_page() -> void:
	var count := _db.query("SELECT COUNT(*) AS n FROM \"items\" WHERE \"name\" LIKE ?1 ESCAPE '\\' OR \"price\" LIKE ?1 ESCAPE '\\';", ["%o%"])
	expect_equal(count[0]["n"], 2, "the filter matches every column with LIKE")
	var page := _db.query("SELECT \"id\", * FROM \"items\" ORDER BY \"price\" DESC, \"id\" LIMIT ?1 OFFSET ?2;", [2, 1])
	expect_equal(page.size(), 2, "one page with LIMIT and OFFSET")
	expect_equal(page[0]["name"], "Potion", "sorted by a column title")
	var statement := _db.prepare("SELECT \"id\", * FROM \"items\" LIMIT 1;")
	expect_equal(statement.step(), SQLiteStatement.STEP_RESULT_ROW, "a row of the page")
	expect_equal(statement.get_column_count(), 4, "the key value comes first, then the columns")
	statement.reset()


func _test_save() -> void:
	expect_equal(_db.begin_transaction(), OK, "Save starts one transaction")
	expect_equal(_db.execute("UPDATE \"items\" SET \"price\" = ? WHERE \"id\" IS ?;", [0.75, 1]), OK, "an edited cell")
	expect_equal(_db.execute("DELETE FROM \"tags\" WHERE \"rowid\" IS ?;", [2]), OK, "a deleted row")
	expect_equal(_db.execute("INSERT INTO \"items\" (\"name\") VALUES (?);", ["Bread"]), OK, "an inserted row")
	expect_equal(_db.execute("INSERT INTO \"tags\" DEFAULT VALUES;"), OK, "an inserted row left at DEFAULT")
	expect_equal(_db.commit(), OK, "Save commits")
	expect(not _db.is_in_transaction(), "no transaction after Save")
	var bread := _db.query("SELECT price FROM items WHERE name = 'Bread';")
	expect_equal(bread[0]["price"], 1.0, "an inserted row gets the column default")
	expect_equal(_db.get_row_count("tags"), 2, "one tag deleted, one inserted")


func _test_failed_save() -> void:
	expect_equal(_db.begin_transaction(), OK, "a second Save")
	expect_equal(_db.execute("UPDATE \"items\" SET \"name\" = ? WHERE \"id\" IS ?;", ["Green apple", 1]), OK, "an edit of the first table")
	expect(_db.execute("INSERT INTO \"items\" (\"name\") VALUES (?);", [null]) != OK, "a NULL in a NOT NULL column fails")
	expect(_db.get_last_error_message().contains("NOT NULL"), "the message names the constraint")
	expect(_db.is_in_transaction(), "the transaction stays open after a failed row")
	expect_equal(_db.rollback(), OK, "the dock rolls back")
	var apple := _db.query("SELECT name FROM items WHERE id = 1;")
	expect_equal(apple[0]["name"], "Apple", "nothing of the failed Save was written")


func _test_table_menu() -> void:
	expect_equal(_db.begin_transaction(), OK, "Create with SQL runs in a transaction")
	expect_equal(_db.execute("CREATE TABLE new_table (id INTEGER PRIMARY KEY, name TEXT NOT NULL DEFAULT '');"), OK, "the template SQL")
	expect_equal(_db.commit(), OK, "Create with SQL commits")
	expect(_db.has_table("new_table"), "the new table exists")
	expect_equal(_db.begin_transaction(), OK, "a failing Create with SQL")
	expect(_db.execute("CREATE TABLE broken (;") != OK, "invalid SQL fails")
	expect_equal(_db.rollback(), OK, "and is rolled back")
	expect_equal(_db.execute("ALTER TABLE \"new_table\" RENAME TO \"renamed\";"), OK, "Rename")
	expect(_db.has_table("renamed"), "the renamed table")
	expect(not _db.has_table("new_table"), "the old name is gone")
	expect(_db.execute("ALTER TABLE \"cheap_items\" RENAME TO \"x\";") != OK, "views can't be renamed")
	expect_equal(_db.execute("DROP TABLE \"renamed\";"), OK, "Delete a table")
	expect(not _db.has_table("renamed"), "the table is gone")
	expect_equal(_db.execute("CREATE VIEW v AS SELECT 1;"), OK, "a view to delete")
	expect_equal(_db.execute("DROP VIEW \"v\";"), OK, "Delete a view")


func _test_query() -> void:
	var reader := SQLiteDatabase.new()
	expect_equal(reader.open(DB_PATH, SQLiteDatabase.OPEN_MODE_READ_ONLY), OK, "Query opens a read-only connection")
	var statement := reader.prepare("SELECT name, price FROM cheap_items ORDER BY name;")
	expect(statement != null, "a SELECT on a view")
	expect_equal(statement.get_column_name(1), "price", "the result columns")
	expect_equal(statement.step(), SQLiteStatement.STEP_RESULT_ROW, "the first result row")
	expect_equal(statement.get_column_value(0), "Apple", "the first value")
	statement.reset()
	expect(reader.execute("DELETE FROM items;") != OK, "a write on the Query connection fails")
	expect_equal(reader.get_last_error(), ERR_FILE_NO_PERMISSION, "the write is refused as read-only")
	reader.close()
	expect_equal(_db.get_row_count("items"), 4, "Query did not change the rows")
