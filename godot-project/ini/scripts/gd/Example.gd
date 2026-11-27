extends Node
## Minimal example of INI: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/INI
## In the editor: double-click settings.ini for live errors, select it once for the data tree (no code).


func _ready() -> void:
	# Using the API
	var settings := load("res://ini/resources/settings.ini") as INI
	print(settings.get_value("video", "width", 1280))
	print(settings.get_sections())

	var mine := INI.new()
	mine.from_config_file(settings.to_config_file())
	mine.set_value("audio", "music", 0.5)
	mine.save("user://settings.ini")

	# Settings a launcher can edit
	_apply_settings()

	# Save your own resource as INI
	var sword := Item.new()
	sword.name = "Sword"
	ResourceSaver.save(sword, "user://sword.ini")
	var loaded := load("user://sword.ini") as Item
	var text := INI.serialize(sword)
	var copy := INI.deserialize(text) as Item
	print(loaded.name, " ", copy.name)
	print(text)
	get_tree().quit()


func _apply_settings() -> void:
	var settings := INI.new()
	if settings.load("user://settings.ini") != OK:
		settings.parse(FileAccess.get_file_as_string("res://ini/resources/settings.ini"))

	get_window().size = Vector2i(settings.get_value("video", "width", 1280), settings.get_value("video", "height", 720))
	var vsync: bool = settings.get_value("video", "vsync", true)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED if vsync else DisplayServer.VSYNC_DISABLED)

	var plain := SerializerOptions.new()
	plain.ini_value_style = SerializerOptions.INI_VALUE_STYLE_PLAIN
	settings.options = plain
	settings.save("user://settings.ini")
	print(FileAccess.get_file_as_string("user://settings.ini"))
