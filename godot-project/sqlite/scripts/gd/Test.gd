extends FeatureTest
## Full API test of SQLite (SQLiteDatabase, SQLiteStatement, SQLiteColumn). Same tests as scripts/cs/Test.cs.

const DB_PATH := "user://sqlite_test.db"
const SHIPPED_DB := "res://sqlite/resources/items.db"
const MISSING_DB := "user://sqlite_test_missing.db"
const NEW_RES_DB := "res://sqlite/resources/new.db"
const ODD_TABLE := "odd \"name"
const SCHEMA_SQL := """
CREATE TABLE items (id INTEGER PRIMARY KEY, name VARCHAR(40) NOT NULL DEFAULT 'none', weight DOUBLE, price DECIMAL(10,2));
CREATE TABLE "odd ""name" (a TEXT, b INT, PRIMARY KEY (b, a));
CREATE VIEW heavy AS SELECT id FROM items;
CREATE VIRTUAL TABLE notes USING fts5(body);
INSERT INTO items (name) VALUES ('a'), ('b');
"""

var _db := SQLiteDatabase.new()


func _run_tests() -> void:
	_remove_db()
	_test_open_and_close()
	_test_execute()
	_test_query()
	_test_statement_bind()
	_test_statement_read()
	_test_transactions()
	_test_errors()
	_test_open_modes()
	_test_shipped_database()
	_test_schema()
	_test_resource_file()
	_remove_db()


func _remove_db() -> void:
	for suffix: String in ["", "-journal"]:
		DirAccess.remove_absolute(DB_PATH + suffix)


func _test_open_and_close() -> void:
	expect(not _db.is_open(), "a new database is not open")
	expect_equal(_db.execute("SELECT 1;"), ERR_UNCONFIGURED, "execute on a closed database returns ERR_UNCONFIGURED")
	expect_equal(_db.open(DB_PATH), OK, "open creates the database in user://")
	expect(_db.is_open(), "is_open is true after open")
	expect_equal(_db.get_database_path(), DB_PATH, "get_database_path returns the path")
	expect_equal(_db.get_open_mode(), SQLiteDatabase.OPEN_MODE_CREATE, "the default open mode is read-write-create")
	expect(FileAccess.file_exists(DB_PATH), "the database file exists")


func _test_execute() -> void:
	expect_equal(_db.execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL, data BLOB, note TEXT);"), OK, "CREATE TABLE")
	expect_equal(_db.execute("INSERT INTO t (name, score, data, note) VALUES (?, ?, ?, ?);", ["Ada", 2.5, PackedByteArray([1, 2, 3]), null]), OK, "INSERT with Array parameters")
	expect_equal(_db.get_changes(), 1, "get_changes after INSERT")
	expect_equal(_db.get_last_insert_rowid(), 1, "get_last_insert_rowid is 1")
	expect_equal(_db.execute("INSERT INTO t (name, score) VALUES (:name, :score);", { "name": "Bob", "score": 7 }), OK, "INSERT with Dictionary parameters")
	expect_equal(_db.get_last_insert_rowid(), 2, "get_last_insert_rowid is 2")
	expect_equal(_db.execute("INSERT INTO t (name) VALUES ($n);", { "n": "Cy" }), OK, "INSERT with a $name parameter")


func _test_query() -> void:
	var rows: Array = _db.query("SELECT id, name, score, data, note FROM t ORDER BY id;")
	expect_equal(rows.size(), 3, "query returns 3 rows")
	var first: Dictionary = rows[0]
	expect_equal(first["id"], 1, "an INTEGER column is an int")
	expect_equal(first["name"], "Ada", "a TEXT column is a String")
	expect_equal(first["score"], 2.5, "a REAL column is a float")
	expect_equal(first["data"], PackedByteArray([1, 2, 3]), "a BLOB column is a PackedByteArray")
	expect(first["note"] == null, "a NULL column is null")
	var named: Array = _db.query("SELECT name FROM t WHERE score > :min;", { "min": 5 })
	expect_equal(named.size(), 1, "query with a named parameter filters")
	var none: Array = _db.query("SELECT name FROM t WHERE id = ?;", [99])
	expect_equal(none.size(), 0, "query without a match is empty")


func _test_statement_bind() -> void:
	var insert: SQLiteStatement = _db.prepare("INSERT INTO t (name, score, data, note) VALUES (?1, ?2, ?3, ?4);")
	expect(insert != null, "prepare returns a statement")
	expect_equal(insert.get_bind_parameter_count(), 4, "get_bind_parameter_count")
	expect_equal(insert.bind_text(1, "Dee"), OK, "bind_text")
	expect_equal(insert.bind_float(2, 1.5), OK, "bind_float")
	expect_equal(insert.bind_blob(3, PackedByteArray([9, 8])), OK, "bind_blob")
	expect_equal(insert.bind_null(4), OK, "bind_null")
	expect_equal(insert.step(), SQLiteStatement.STEP_RESULT_DONE, "step of an INSERT is done")
	expect_equal(insert.reset(), OK, "reset")
	expect_equal(insert.bind_text(1, "Eve"), OK, "bind again after reset")
	expect_equal(insert.bind_int(2, 4), OK, "bind_int")
	expect_equal(insert.bind_value(3, PackedByteArray()), OK, "bind_value with a blob")
	expect_equal(insert.bind_value(4, "x"), OK, "bind_value with a String")
	expect_equal(insert.step(), SQLiteStatement.STEP_RESULT_DONE, "the second INSERT is done")
	expect_equal(insert.bind_int(9, 1), ERR_INVALID_PARAMETER, "bind with an index out of range fails")
	expect_equal(insert.clear_bindings(), OK, "clear_bindings")
	var named: SQLiteStatement = _db.prepare("SELECT 1 WHERE :id = 1;")
	expect_equal(named.get_bind_parameter_index(":id"), 1, "get_bind_parameter_index")
	expect_equal(named.get_sql(), "SELECT 1 WHERE :id = 1;", "get_sql")
	var invalid: SQLiteStatement = _db.prepare("SELEC")
	expect(invalid == null, "prepare of invalid SQL returns null")
	expect_equal(_db.get_last_error(), ERR_QUERY_FAILED, "a prepare failure sets the last error")


func _test_statement_read() -> void:
	var select: SQLiteStatement = _db.prepare("SELECT id, name, score, data, note FROM t WHERE name = ?;")
	expect_equal(select.bind_text(1, "Dee"), OK, "bind_text on a SELECT")
	expect_equal(select.get_column_count(), 5, "get_column_count")
	expect_equal(select.get_column_name(1), "name", "get_column_name")
	expect_equal(select.step(), SQLiteStatement.STEP_RESULT_ROW, "step returns a row")
	expect_equal(select.get_column_type(0), SQLiteStatement.COLUMN_TYPE_INTEGER, "column type integer")
	expect_equal(select.get_column_type(1), SQLiteStatement.COLUMN_TYPE_TEXT, "column type text")
	expect_equal(select.get_column_type(2), SQLiteStatement.COLUMN_TYPE_FLOAT, "column type float")
	expect_equal(select.get_column_type(3), SQLiteStatement.COLUMN_TYPE_BLOB, "column type blob")
	expect_equal(select.get_column_type(4), SQLiteStatement.COLUMN_TYPE_NULL, "column type null")
	expect_equal(select.get_column_int(0), 4, "get_column_int")
	expect_equal(select.get_column_text(1), "Dee", "get_column_text")
	expect_equal(select.get_column_float(2), 1.5, "get_column_float")
	expect_equal(select.get_column_blob(3), PackedByteArray([9, 8]), "get_column_blob")
	expect(select.get_column_value(4) == null, "get_column_value of NULL is null")
	var row: Dictionary = select.get_row()
	expect_equal(row["name"], "Dee", "get_row")
	expect_equal(select.step(), SQLiteStatement.STEP_RESULT_DONE, "step is done after the last row")
	expect_equal(select.reset(), OK, "reset after the statement finished")
	expect_equal(select.step(), SQLiteStatement.STEP_RESULT_ROW, "reset runs the statement again")
	expect_equal(select.reset(), OK, "reset in the middle of a run")
	expect_equal(select.bind_values(["Eve"]), OK, "bind_values with an Array")
	expect_equal(select.step(), SQLiteStatement.STEP_RESULT_ROW, "a row for the new binding")
	expect_equal(select.get_column_text(1), "Eve", "the bound text is read back")
	var broken: SQLiteStatement = _db.prepare("SELECT no_such_column FROM t;")
	expect(broken == null, "prepare of SQL with an unknown column returns null")
	var error_step: SQLiteStatement = _db.prepare("SELECT abs(-9223372036854775808);")
	expect_equal(error_step.step(), SQLiteStatement.STEP_RESULT_ERROR, "step reports STEP_RESULT_ERROR for a failing statement")


func _test_transactions() -> void:
	expect(not _db.is_in_transaction(), "there is no transaction at first")
	expect_equal(_db.begin_transaction(), OK, "begin_transaction")
	expect(_db.is_in_transaction(), "is_in_transaction while active")
	_db.execute("INSERT INTO t (name) VALUES ('rolled back');")
	expect_equal(_db.rollback(), OK, "rollback")
	expect(not _db.is_in_transaction(), "there is no transaction after rollback")
	expect_equal(_db.query("SELECT id FROM t WHERE name = 'rolled back';").size(), 0, "rollback discards the row")
	expect_equal(_db.begin_transaction(), OK, "begin_transaction again")
	_db.execute("INSERT INTO t (name) VALUES ('kept');")
	expect_equal(_db.commit(), OK, "commit")
	expect(not _db.is_in_transaction(), "there is no transaction after commit")
	expect_equal(_db.query("SELECT id FROM t WHERE name = 'kept';").size(), 1, "commit keeps the row")
	expect_equal(_db.commit(), ERR_QUERY_FAILED, "commit without a transaction fails")
	expect_equal(_db.rollback(), ERR_QUERY_FAILED, "rollback without a transaction fails")


func _test_errors() -> void:
	expect_equal(_db.execute("SELEC 1;"), ERR_QUERY_FAILED, "a syntax error returns ERR_QUERY_FAILED")
	expect_equal(_db.get_last_error(), ERR_QUERY_FAILED, "get_last_error after a syntax error")
	expect(_db.get_last_error_message().contains("syntax error"), "the error message names the syntax error")
	expect_equal(_db.query("SELEC 1;").size(), 0, "query of invalid SQL returns an empty array")
	expect_equal(_db.execute("INSERT INTO t (name, score) VALUES (?, ?);", ["only one"]), ERR_INVALID_PARAMETER, "missing parameters return ERR_INVALID_PARAMETER")
	expect_equal(_db.execute("INSERT INTO t (name) VALUES (?);", [Vector2(1, 2)]), ERR_INVALID_PARAMETER, "an unsupported parameter type returns ERR_INVALID_PARAMETER")
	expect_equal(_db.execute("SELECT 1;"), OK, "a successful call clears the error")
	expect_equal(_db.get_last_error_message(), "", "the message is empty after success")
	_db.close()
	expect(not _db.is_open(), "close closes the database")


func _test_open_modes() -> void:
	expect_equal(_db.open(DB_PATH, SQLiteDatabase.OPEN_MODE_READ_WRITE), OK, "reopen with OPEN_MODE_READ_WRITE")
	expect_equal(_db.query("SELECT id FROM t;").size(), 6, "the committed rows are persistent")
	_db.close()
	expect_equal(_db.open(DB_PATH, SQLiteDatabase.OPEN_MODE_READ_ONLY), OK, "open read-only")
	expect_equal(_db.get_open_mode(), SQLiteDatabase.OPEN_MODE_READ_ONLY, "get_open_mode is read-only")
	expect_equal(_db.query("SELECT id FROM t;").size(), 6, "a read-only database can be queried")
	expect_equal(_db.execute("INSERT INTO t (name) VALUES ('no');"), ERR_FILE_NO_PERMISSION, "INSERT on a read-only database returns ERR_FILE_NO_PERMISSION")
	_db.close()
	var missing := SQLiteDatabase.new()
	expect_equal(missing.open(MISSING_DB, SQLiteDatabase.OPEN_MODE_READ_ONLY), ERR_FILE_NOT_FOUND, "a missing file in read-only mode returns ERR_FILE_NOT_FOUND")
	expect_equal(missing.open(MISSING_DB, SQLiteDatabase.OPEN_MODE_READ_WRITE), ERR_FILE_NOT_FOUND, "a missing file in read-write mode returns ERR_FILE_NOT_FOUND")
	expect_equal(_db.open(NEW_RES_DB), ERR_FILE_NO_PERMISSION, "a writable open on res:// returns ERR_FILE_NO_PERMISSION")
	_remove_db()


func _test_shipped_database() -> void:
	var shipped := SQLiteDatabase.new()
	expect_equal(shipped.open(SHIPPED_DB, SQLiteDatabase.OPEN_MODE_READ_ONLY), OK, "open the database in res:// read-only")
	var count: Array = shipped.query("SELECT COUNT(*) AS n FROM items;")
	expect_equal(count[0]["n"], 3, "the shipped database has 3 rows")
	var names: Array = []
	for row: Dictionary in shipped.query("SELECT name FROM items ORDER BY id;"):
		names.append(row["name"])
	expect_equal(names, ["sword", "shield", "potion"], "the shipped rows are read in order")
	expect_equal(shipped.execute("DELETE FROM items;"), ERR_FILE_NO_PERMISSION, "writing to the shipped database is refused")
	shipped.close()
	expect_equal(DirAccess.copy_absolute(SHIPPED_DB, DB_PATH), OK, "copy_absolute copies the database to user://")
	expect_equal(shipped.open(DB_PATH), OK, "open the copy")
	expect_equal(shipped.execute("INSERT INTO items (name) VALUES (?);", ["bow"]), OK, "the copy is writable")
	expect_equal(shipped.query("SELECT id FROM items;").size(), 4, "the copy has 4 rows")
	shipped.close()
	_remove_db()


func _test_schema() -> void:
	expect_equal(_db.open(DB_PATH), OK, "open a database for the schema tests")
	expect_equal(_db.get_table_names(), PackedStringArray(), "an empty database has no tables")
	expect_equal(_db.execute(SCHEMA_SQL), OK, "create tables, a view and a virtual table")
	var names: PackedStringArray = PackedStringArray(["items", "notes", ODD_TABLE])
	expect_equal(_db.get_table_names(), names, "get_table_names lists tables and virtual tables by name, without views and shadow tables")
	expect(_db.has_table("ITEMS"), "has_table ignores case")
	expect(_db.has_table(ODD_TABLE), "has_table with a quote in the name")
	expect(not _db.has_table("heavy"), "a view is not a table")
	expect(not _db.has_table("notes_data"), "FTS5 shadow tables are hidden")
	var columns: Array[SQLiteColumn] = _db.get_columns("items")
	expect_equal(columns.size(), 4, "get_columns returns every column")
	expect_equal(columns[0].name, "id", "SQLiteColumn.name")
	expect_equal(columns[0].primary_key_index, 1, "INTEGER PRIMARY KEY is key column 1")
	expect_equal(columns[0].affinity, SQLiteColumn.AFFINITY_INTEGER, "INTEGER has INTEGER affinity")
	expect_equal(columns[1].declared_type, "VARCHAR(40)", "SQLiteColumn.declared_type is as written")
	expect(columns[1].not_null, "SQLiteColumn.not_null")
	expect_equal(columns[1].default_value, "'none'", "SQLiteColumn.default_value is SQL text")
	expect_equal(columns[1].primary_key_index, 0, "a column outside the key has index 0")
	expect_equal(columns[1].affinity, SQLiteColumn.AFFINITY_TEXT, "VARCHAR has TEXT affinity")
	expect_equal(columns[2].affinity, SQLiteColumn.AFFINITY_REAL, "DOUBLE has REAL affinity")
	expect_equal(columns[2].default_value, "", "no default is an empty string")
	expect_equal(columns[3].affinity, SQLiteColumn.AFFINITY_NUMERIC, "DECIMAL has NUMERIC affinity")
	var blob_table: Array[SQLiteColumn] = _db.get_columns("notes")
	expect_equal(blob_table[0].affinity, SQLiteColumn.AFFINITY_BLOB, "a column without a type has BLOB affinity")
	var odd: Array[SQLiteColumn] = _db.get_columns(ODD_TABLE)
	expect_equal([odd[0].primary_key_index, odd[1].primary_key_index], [2, 1], "composite key positions")
	expect_equal(_db.get_column_names("items"), PackedStringArray(["id", "name", "weight", "price"]), "get_column_names")
	expect_equal(_db.get_column_count("items"), 4, "get_column_count")
	expect_equal(_db.get_row_count("items"), 2, "get_row_count")
	expect_equal(_db.get_row_count("Items"), 2, "get_row_count ignores case")
	expect_equal(_db.get_row_count(ODD_TABLE), 0, "get_row_count of a quoted name")
	expect_equal(_db.get_row_count("missing"), -1, "get_row_count of an unknown table is -1")
	expect_equal(_db.get_last_error(), ERR_DOES_NOT_EXIST, "an unknown table sets ERR_DOES_NOT_EXIST")
	expect(_db.get_columns("missing").is_empty(), "get_columns of an unknown table is empty")
	expect_equal(_db.get_column_count("missing"), -1, "get_column_count of an unknown table is -1")
	expect_equal(_db.get_row_count("items\"; DROP TABLE items; --"), -1, "a name with SQL in it is just an unknown table")
	expect(_db.has_table("items"), "the table is still there")
	_db.close()
	_remove_db()


func _test_resource_file() -> void:
	var loaded: SQLiteDatabase = load(SHIPPED_DB)
	expect(not loaded.is_open(), "a loaded database is not open yet")
	expect_equal(loaded.resource_path, SHIPPED_DB, "resource_path is the file")
	expect_equal(loaded.open(), OK, "open without a path opens the loaded file")
	expect_equal(loaded.get_open_mode(), SQLiteDatabase.OPEN_MODE_READ_ONLY, "a file in res:// opens read-only")
	expect_equal(loaded.get_database_path(), SHIPPED_DB, "get_database_path of the loaded file")
	expect_equal(loaded.get_table_names(), PackedStringArray(["items"]), "the tables of the shipped database")
	expect_equal(loaded.get_row_count("items"), 3, "the rows of the shipped database")
	loaded.close()
	var empty := SQLiteDatabase.new()
	expect_equal(empty.open(), ERR_INVALID_PARAMETER, "open without a path needs a loaded file")
