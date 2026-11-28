extends Node
## Minimal example of the Dataset dock: the snippets of the wiki guide, in order. The dock edits through this API.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Dataset-Dock


func _ready() -> void:
	# Insert a column and write a block of cells.
	var csv := CSV.new()
	csv.parse("name,hp\nknight,100\narcher,80\n")
	csv.insert_column(1, "speed", "1.0")
	csv.set_cells(0, 2, [PackedStringArray(["120"])])
	print(csv.get_header())
	print(csv.get_cell(0, 2))

	# A JSON array of dictionaries is one row per dictionary.
	var json := JSON.new()
	json.parse("[{\"name\": \"knight\", \"hp\": 100}]", true)
	var rows: Array = json.data
	print(rows.size())
	print(json.get_parsed_text())
	get_tree().quit()
