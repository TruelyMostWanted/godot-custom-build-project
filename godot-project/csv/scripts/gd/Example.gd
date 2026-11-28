extends Node
## Minimal example of CSV: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/CSV
## In the editor: import mode "Keep File" for data tables, the script editor and the CSV Table dock (no code).

var units: Array[Dictionary] = []


func _ready() -> void:
	# Using the API (the guide calls this CSV "units"; here "table", as in the next snippet).
	# CSV.new() and load(path) instead of load(): the engine prints a false "Resource file not found" for a Keep File CSV.
	var table := CSV.new()
	table.load("res://csv/resources/units.csv")
	var hp := table.find_column("hp")
	for row in table.get_row_count():
		print(table.get_cell(row, 0), ": ", table.get_cell(row, hp).to_int(), " hp")
	print(table.get_row_as_dictionary(1))
	var schema := load("res://csv/resources/units_schema.tres") as DataSchema
	var archer := table.get_typed_row(1, schema)
	print(archer["hp"] + 10)

	# Add and remove columns.
	var roster := CSV.new()
	roster.parse("name,hp\narcher,60\nknight,120\n")
	roster.insert_column(1, "class", "melee")
	roster.set_cell(0, 1, "ranger")
	print(roster.encode_to_text()) # Prints "name,class,hp\narcher,ranger,60\nknight,melee,120\n"
	roster.remove_column(roster.find_column("hp"))
	print(roster.header) # Prints ["name", "class"]
	var players := CSV.new()
	players.insert_column(0, "id")
	players.insert_column(1, "name")
	players.append_row(PackedStringArray(["1", "Ann"]))
	players.save("user://players.csv")

	# A unit roster with checked data (print instead of push_warning: the C# warning trace contains "error", which fails the runner)
	var check := table.validate(schema)
	var bad_rows := check.get_invalid_rows()
	for i in check.get_issue_count():
		print("units.csv row %d: %s" % [check.get_issue_row(i) + 1, check.get_issue_message(i)])
	for row in table.get_row_count():
		if not bad_rows.has(row):
			units.append(table.get_typed_row(row, schema))
	print(units.size(), " units")

	# Translation CSVs in the dock
	var ui := CSV.new()
	ui.load("res://csv/resources/ui.csv")
	var translation_check := ui.validate_translation()
	for i in translation_check.get_issue_count():
		if translation_check.get_issue_kind(i) == DataValidation.ISSUE_KIND_EMPTY_REQUIRED:
			print("Missing ", translation_check.get_issue_field(i), " in row ", translation_check.get_issue_row(i))

	# Save your own resource as CSV
	var sword := Item.new()
	sword.name = "Sword"
	var items := ItemTable.new()
	items.entries = [sword]
	if ResourceTextSerializer.can_serialize(items, ResourceTextSerializer.FORMAT_CSV):
		ResourceSaver.save(items, "user://items.csv")
	var loaded := ResourceLoader.load("user://items.csv", "ItemTable") as ItemTable
	print(loaded.entries[0].name)
	get_tree().quit()
