extends Node
## Minimal example of Data File Inspector: the snippets of the wiki guide, in order, one function per section.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Data-File-Inspector
## The view itself is editor UI: click a data file in the FileSystem dock (no code). scripts/gd/measure_inspector.gd
## measures it in the editor (File > Run in the script editor).


func _ready() -> void:
	# The same data from code
	_same_data_from_code()
	# A data check before the game starts
	_data_check()
	get_tree().quit()


func _same_data_from_code() -> void:
	var units := CSV.new()
	units.load("res://csv/resources/units.csv")
	print("%d rows, %d columns" % [units.get_row_count(), units.get_column_count()])
	print(units.header)
	var enemies := load("res://json/resources/enemies.json") as JSON
	print(enemies.data["enemies"].size())


func _data_check() -> void:
	# CSV.new() and load(path) instead of load(): the engine prints a false "Resource file not found" for a Keep File CSV.
	var units := CSV.new()
	units.load("res://csv/resources/units.csv")
	var schema := load("res://csv/resources/units_schema.tres") as DataSchema
	var result := units.validate(schema)
	print("%d issues" % result.get_issue_count())
	for i in result.get_issue_count():
		print("row %d, column %d: %s" % [result.get_issue_row(i), result.get_issue_column(i), result.get_issue_message(i)])
	var enemies := load("res://json/resources/enemies.json") as JSON
	for enemy: Dictionary in enemies.data["enemies"]:
		print(enemy["name"], " ", enemy["hp"])
