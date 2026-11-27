extends Node
## Minimal example of Text Serialization: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-Serialization
## In the editor: double-click a data file to edit it, select it once to see its data in the Inspector (no code).


func _ready() -> void:
	# Data files as resources
	var server := load("res://toml/resources/server.toml") as TOML
	print(server.get_value("server.limits", "players"))

	var saves := INI.new()
	if saves.load("user://saves.ini") != OK:
		saves.set_value("slot1", "level", 1)
	saves.save("user://saves.ini")
	print("Saves: ", saves.data)

	var sections := INI.parse_string("[a]\nb=1\n")
	print("Sections: ", sections)

	# Save your own resource in any format
	var sword := Item.new()
	sword.name = "Sword"
	sword.rarity = Item.Rarity.Epic
	sword.offset = Vector2(0.5, -2.0)
	sword.code = "007"
	ResourceSaver.save(sword, "user://sword.json")
	var loaded := load("user://sword.json") as Item
	print("Loaded: ", loaded.name)

	var text := ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_YAML)
	var copy := ResourceTextSerializer.deserialize(text, ResourceTextSerializer.FORMAT_YAML) as Item
	print("Copy: ", copy.name)

	var path := "user://sword.yml"
	var format := ResourceTextSerializer.get_format_from_extension(path.get_extension())
	if format != ResourceTextSerializer.FORMAT_MAX and ResourceTextSerializer.is_format_available(format):
		FileAccess.open(path, FileAccess.WRITE).store_string(ResourceTextSerializer.serialize(sword, format))
	print(path, ":\n", FileAccess.get_file_as_string(path))

	# Options
	var options := SerializerOptions.new()
	options.indent = ""
	options.sort_keys = true
	print(ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON, options))

	# Add a format in a C++ module: engine modules only, no script example.
	get_tree().quit()
