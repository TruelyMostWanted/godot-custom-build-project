extends Node
## Minimal example of the Truely version: the snippet of the wiki page Releases ("Read the version in a game").
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Releases


func _ready() -> void:
	# Read the version in a game.
	var info: Dictionary = Engine.get_version_info()
	print("Truely v%s on Godot %s" % [info.truely_version, info.string])

	if info.truely_hex >= 0x000200:
		print("This engine has the features of Truely 0.2.0.")
	get_tree().quit()
