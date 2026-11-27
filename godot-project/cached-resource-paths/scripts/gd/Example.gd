extends Node
## Minimal example of Cached Resource Paths: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Cached-Resource-Paths


func _ready() -> void:
	# Replace with the first snippet of the guide.
	var version: Dictionary = Engine.get_version_info()
	print("Engine version: ", version.string)
	get_tree().quit()
