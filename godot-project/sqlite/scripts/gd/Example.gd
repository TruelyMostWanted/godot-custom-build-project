extends Node
## Minimal example of SQLite: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/SQLite

const ITEMS := "res://sqlite/resources/items.db"

@export var items_db: SQLiteDatabase # Drag a .db file from the FileSystem dock.


func _ready() -> void:
	# Setup: start with a fresh save database.
	DirAccess.remove_absolute("user://save.db")

	# Table editor: a game reads the saved data as usual.
	var items: SQLiteDatabase = load(ITEMS)
	items.open()
	print(items.query("SELECT name, price FROM items ORDER BY id;"))
	items.close()

	# Database files as resources.
	var shipped: SQLiteDatabase = load(ITEMS)
	if shipped.open() != OK: # Read-only: res:// can't be written.
		push_error(shipped.get_last_error_message())
	shipped.close()

	# Open or create a database.
	var db := SQLiteDatabase.new()
	if db.open("user://save.db") != OK: # Creates the file if missing.
		push_error(db.get_last_error_message())
		return
	# Existing file only: db.open("user://save.db", SQLiteDatabase.OPEN_MODE_READ_WRITE)
	print("Open: ", db.get_database_path())
	db.close()
	db.open("user://save.db") # The example goes on with the open database.

	# Execute SQL with parameters.
	db.execute("CREATE TABLE IF NOT EXISTS scores (name TEXT, points INTEGER);")
	db.execute("INSERT INTO scores VALUES (?, ?);", ["Ada", 120])
	db.execute("INSERT INTO scores VALUES (:name, :points);", { "name": "Bob", "points": 80 })
	print(db.get_changes(), " ", db.get_last_insert_rowid())

	# Read rows.
	for row: Dictionary in db.query("SELECT name, points FROM scores WHERE points > :min;", { "min": 100 }):
		print(row.name, ": ", row.points)
	if db.get_last_error() != OK:
		print(db.get_last_error_message()) # query() returns [] on failure.

	# Prepared statements (the guide's entries are two sample scores).
	var entries: Array[Dictionary] = [{ "name": "Cy", "points": 50 }, { "name": "Dee", "points": 95 }]
	var insert: SQLiteStatement = db.prepare("INSERT INTO scores VALUES (?1, ?2);")
	for entry: Dictionary in entries:
		insert.bind_text(1, entry.name) # Parameters start at 1.
		insert.bind_int(2, entry.points)
		insert.step()
		insert.reset() # Bound values stay; clear_bindings() sets them to NULL.

	var select: SQLiteStatement = db.prepare("SELECT name, points FROM scores;")
	while select.step() == SQLiteStatement.STEP_RESULT_ROW:
		print(select.get_column_text(0), " ", select.get_column_int(1)) # Columns start at 0.

	# Transactions.
	db.begin_transaction()
	for i in 1000:
		db.execute("INSERT INTO scores VALUES (?, ?);", ["p%d" % i, i])
	if db.commit() != OK:
		db.rollback()
	print("Rows: ", db.get_row_count("scores"))

	# Databases in res:// and PCK files: copy to user:// to modify a shipped database.
	DirAccess.copy_absolute(ITEMS, "user://items.db")
	var copy: SQLiteDatabase = SQLiteDatabase.new()
	copy.open("user://items.db")
	var result: Error = copy.execute("INSERT INTO items (name) VALUES ('bow');")
	print("Copy is writable: ", result == OK)
	copy.close()
	DirAccess.remove_absolute("user://items.db")

	# Tables and columns.
	for table: String in db.get_table_names():
		print(table, ": ", db.get_column_count(table), " columns, ", db.get_row_count(table), " rows")
		for column: SQLiteColumn in db.get_columns(table):
			print("  ", column.name, " ", column.declared_type, " key: ", column.primary_key_index)
	db.close()
	get_tree().quit()
