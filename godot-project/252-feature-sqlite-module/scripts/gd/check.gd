extends FeatureCheck
## GDScript checks of 252-feature-sqlite-module. Check the same things as scripts/cs/Issue252FeatureSqliteModuleCheck.cs.

const DB_PATH := "user://check_252.db"
const SHIPPED_DB := "res://252-feature-sqlite-module/data/items.db"


func _get_check_name() -> String:
	return "252-feature-sqlite-module"


func _remove_db() -> void:
	for suffix in ["", "-journal"]:
		if FileAccess.file_exists(DB_PATH + suffix):
			DirAccess.remove_absolute(DB_PATH + suffix)


func _run_checks() -> void:
	expect(ClassDB.class_exists("SQLiteDatabase"), "ClassDB knows SQLiteDatabase")
	expect(ClassDB.class_exists("SQLiteStatement"), "ClassDB knows SQLiteStatement")
	_remove_db()

	# Open or create.
	var db := SQLiteDatabase.new()
	expect(not db.is_open(), "a new database is not open")
	expect_equal(db.execute("SELECT 1;"), ERR_UNCONFIGURED, "execute on a closed database returns ERR_UNCONFIGURED")
	expect_equal(db.open(DB_PATH), OK, "open creates the database in user://")
	expect(db.is_open(), "is_open is true after open")
	expect_equal(db.get_path(), DB_PATH, "get_path returns the path")
	expect_equal(db.get_open_mode(), SQLiteDatabase.OPEN_MODE_READ_WRITE_CREATE, "default open mode is read-write-create")
	expect(FileAccess.file_exists(DB_PATH), "the database file exists")

	# Execute with Array and Dictionary params.
	expect_equal(db.execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL, data BLOB, note TEXT);"), OK, "CREATE TABLE")
	expect_equal(db.execute("INSERT INTO t (name, score, data, note) VALUES (?, ?, ?, ?);", ["Ada", 2.5, PackedByteArray([1, 2, 3]), null]), OK, "INSERT with Array params")
	expect_equal(db.get_changes(), 1, "get_changes after INSERT")
	expect_equal(db.get_last_insert_rowid(), 1, "get_last_insert_rowid is 1")
	expect_equal(db.execute("INSERT INTO t (name, score) VALUES (:name, :score);", { "name": "Bob", "score": 7 }), OK, "INSERT with Dictionary params")
	expect_equal(db.get_last_insert_rowid(), 2, "get_last_insert_rowid is 2")
	expect_equal(db.execute("INSERT INTO t (name) VALUES ($n);", { "n": "Cy" }), OK, "INSERT with $name param")

	# Query returns typed rows.
	var rows: Array = db.query("SELECT id, name, score, data, note FROM t ORDER BY id;")
	expect_equal(rows.size(), 3, "query returns 3 rows")
	var first: Dictionary = rows[0]
	expect_equal(first["id"], 1, "int column")
	expect(first["id"] is int, "id is an int")
	expect_equal(first["name"], "Ada", "String column")
	expect(first["score"] is float and is_equal_approx(first["score"], 2.5), "float column")
	expect(first["data"] is PackedByteArray and first["data"] == PackedByteArray([1, 2, 3]), "PackedByteArray column")
	expect(first["note"] == null, "NULL column is null")
	expect_equal(db.query("SELECT name FROM t WHERE score > :min;", { "min": 5 }).size(), 1, "query with named param filters")
	expect_equal(db.query("SELECT name FROM t WHERE id = ?;", [99]).size(), 0, "query with no match is empty")

	# Prepared statements.
	var insert := db.prepare("INSERT INTO t (name, score, data, note) VALUES (?1, ?2, ?3, ?4);")
	expect(insert != null, "prepare returns a statement")
	expect_equal(insert.get_bind_parameter_count(), 4, "get_bind_parameter_count")
	expect_equal(insert.bind_text(1, "Dee"), OK, "bind_text")
	expect_equal(insert.bind_float(2, 1.5), OK, "bind_float")
	expect_equal(insert.bind_blob(3, PackedByteArray([9, 8])), OK, "bind_blob")
	expect_equal(insert.bind_null(4), OK, "bind_null")
	expect_equal(insert.step(), SQLiteStatement.STEP_RESULT_DONE, "step of INSERT is done")
	expect_equal(insert.reset(), OK, "reset")
	expect_equal(insert.bind_text(1, "Eve"), OK, "bind again after reset")
	expect_equal(insert.bind_int(2, 4), OK, "bind_int")
	expect_equal(insert.bind_value(3, PackedByteArray()), OK, "bind_value with a blob")
	expect_equal(insert.bind_value(4, "x"), OK, "bind_value with a String")
	expect_equal(insert.step(), SQLiteStatement.STEP_RESULT_DONE, "second INSERT is done")
	expect_equal(insert.bind_int(9, 1), ERR_INVALID_PARAMETER, "bind with an index out of range")
	expect_equal(insert.clear_bindings(), OK, "clear_bindings")
	var named := db.prepare("SELECT 1 WHERE :id = 1;")
	expect_equal(named.get_bind_parameter_index(":id"), 1, "get_bind_parameter_index")
	expect_equal(named.get_sql(), "SELECT 1 WHERE :id = 1;", "get_sql")

	var select := db.prepare("SELECT id, name, score, data, note FROM t WHERE name = ?;")
	expect_equal(select.bind_text(1, "Dee"), OK, "bind_text on SELECT")
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
	expect(is_equal_approx(select.get_column_float(2), 1.5), "get_column_float")
	expect_equal(select.get_column_blob(3), PackedByteArray([9, 8]), "get_column_blob")
	expect(select.get_column_value(4) == null, "get_column_value of NULL is null")
	expect_equal(select.get_row()["name"], "Dee", "get_row")
	expect_equal(select.step(), SQLiteStatement.STEP_RESULT_DONE, "step is done after the last row")
	expect_equal(select.reset(), OK, "reset after the statement finished")
	expect_equal(select.step(), SQLiteStatement.STEP_RESULT_ROW, "reset runs the statement again")
	expect_equal(select.reset(), OK, "reset in the middle of a run")
	expect_equal(select.bind_values(["Eve"]), OK, "bind_values with an Array")
	expect_equal(select.step(), SQLiteStatement.STEP_RESULT_ROW, "row for the new binding")
	expect_equal(select.get_column_text(1), "Eve", "bound text is read back")
	var bad := db.prepare("SELEC")
	expect(bad == null, "prepare of invalid SQL returns null")
	expect_equal(db.get_last_error(), ERR_QUERY_FAILED, "prepare failure sets the last error")

	# Transactions.
	expect(not db.is_in_transaction(), "no transaction at first")
	expect_equal(db.begin_transaction(), OK, "begin_transaction")
	expect(db.is_in_transaction(), "is_in_transaction while active")
	db.execute("INSERT INTO t (name) VALUES ('rolled back');")
	expect_equal(db.rollback(), OK, "rollback")
	expect(not db.is_in_transaction(), "no transaction after rollback")
	expect_equal(db.query("SELECT id FROM t WHERE name = 'rolled back';").size(), 0, "rollback discards the row")
	expect_equal(db.begin_transaction(), OK, "begin_transaction again")
	db.execute("INSERT INTO t (name) VALUES ('kept');")
	expect_equal(db.commit(), OK, "commit")
	expect(not db.is_in_transaction(), "no transaction after commit")
	expect_equal(db.query("SELECT id FROM t WHERE name = 'kept';").size(), 1, "commit keeps the row")
	expect_equal(db.commit(), ERR_QUERY_FAILED, "commit without a transaction fails")

	# Errors.
	expect_equal(db.execute("SELEC 1;"), ERR_QUERY_FAILED, "syntax error returns ERR_QUERY_FAILED")
	expect_equal(db.get_last_error(), ERR_QUERY_FAILED, "get_last_error after a syntax error")
	expect(db.get_last_error_message().contains("syntax error"), "error message names the syntax error")
	expect_equal(db.query("SELEC 1;").size(), 0, "query of invalid SQL returns an empty array")
	expect_equal(db.execute("INSERT INTO t (name, score) VALUES (?, ?);", ["only one"]), ERR_INVALID_PARAMETER, "missing params return ERR_INVALID_PARAMETER")
	expect_equal(db.execute("INSERT INTO t (name) VALUES (?);", [Vector2(1, 2)]), ERR_INVALID_PARAMETER, "unsupported param type returns ERR_INVALID_PARAMETER")
	expect_equal(db.execute("SELECT 1;"), OK, "a successful call clears the error")
	expect_equal(db.get_last_error_message(), "", "message is empty after success")
	db.close()
	expect(not db.is_open(), "close closes the database")

	# Reopen: data is persistent. Modes.
	expect_equal(db.open(DB_PATH, SQLiteDatabase.OPEN_MODE_READ_WRITE), OK, "reopen with OPEN_MODE_READ_WRITE")
	expect_equal(db.query("SELECT id FROM t;").size(), 6, "committed rows are persistent")
	db.close()
	expect_equal(db.open(DB_PATH, SQLiteDatabase.OPEN_MODE_READ_ONLY), OK, "open read-only")
	expect_equal(db.query("SELECT id FROM t;").size(), 6, "read-only database can be queried")
	expect_equal(db.execute("INSERT INTO t (name) VALUES ('no');"), ERR_FILE_NO_PERMISSION, "INSERT on a read-only database returns ERR_FILE_NO_PERMISSION")
	db.close()
	var missing := SQLiteDatabase.new()
	expect_equal(missing.open("user://check_252_missing.db", SQLiteDatabase.OPEN_MODE_READ_ONLY), ERR_FILE_NOT_FOUND, "missing file in read-only mode returns ERR_FILE_NOT_FOUND")
	expect_equal(missing.open("user://check_252_missing.db", SQLiteDatabase.OPEN_MODE_READ_WRITE), ERR_FILE_NOT_FOUND, "missing file in read-write mode returns ERR_FILE_NOT_FOUND")
	expect_equal(db.open("res://252-feature-sqlite-module/new.db"), ERR_FILE_NO_PERMISSION, "writable open on res:// returns ERR_FILE_NO_PERMISSION")
	expect_equal(db.open("res://252-feature-sqlite-module/new.db", SQLiteDatabase.OPEN_MODE_READ_WRITE), ERR_FILE_NO_PERMISSION, "read-write open on res:// returns ERR_FILE_NO_PERMISSION")
	_remove_db()

	# Read-only database shipped in res://.
	var shipped := SQLiteDatabase.new()
	expect_equal(shipped.open(SHIPPED_DB, SQLiteDatabase.OPEN_MODE_READ_ONLY), OK, "open the database in res:// read-only")
	var count: Array = shipped.query("SELECT COUNT(*) AS n FROM items;")
	expect_equal(count[0]["n"], 3, "the shipped database has 3 rows")
	var names: Array = []
	for row in shipped.query("SELECT name FROM items ORDER BY id;"):
		names.append(row["name"])
	expect_equal(names, ["sword", "shield", "potion"], "the shipped rows are read in order")
	expect_equal(shipped.execute("DELETE FROM items;"), ERR_FILE_NO_PERMISSION, "writing to the shipped database is refused")
	shipped.close()

	# Copy to user:// makes it writable.
	expect_equal(DirAccess.copy_absolute(SHIPPED_DB, DB_PATH), OK, "copy_absolute copies the database to user://")
	expect_equal(shipped.open(DB_PATH), OK, "open the copy")
	expect_equal(shipped.execute("INSERT INTO items (name) VALUES (?);", ["bow"]), OK, "the copy is writable")
	expect_equal(shipped.query("SELECT id FROM items;").size(), 4, "the copy has 4 rows")
	shipped.close()
	_remove_db()