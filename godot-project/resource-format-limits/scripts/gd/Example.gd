extends Node
## Minimal example of Resource Format Limits: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Resource-Format-Limits

const MyLoader := preload("res://resource-format-limits/scripts/gd/my_loader.gd")
const MySaver := preload("res://resource-format-limits/scripts/gd/my_saver.gd")


func _ready() -> void:
	# Register many formats
	for extension: String in ["foo", "bar", "baz"]:
		ResourceLoader.add_resource_format_loader(MyLoader.new(extension))
		ResourceSaver.add_resource_format_saver(MySaver.new(extension))
	var extensions: PackedStringArray = ResourceLoader.get_recognized_extensions_for_type("Resource")
	print("Recognized: ", extensions.has("foo"), " ", extensions.has("bar"), " ", extensions.has("baz"))
	get_tree().quit()
