extends Node
## Minimal example of Resource Database: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Resource-Database


func _ready() -> void:
	# Replace with the first snippet of the guide.
	var version: Dictionary = Engine.get_version_info()
	print("Engine version: ", version.string)
	get_tree().quit()
