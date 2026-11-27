extends Node
## Minimal example of Children of a Type: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Children-of-a-Type
## The generic methods are C#-only; GDScript uses find_children() with a class name.


func _ready() -> void:
	var level := Node.new()
	level.name = "Level"
	add_child(level)
	level.add_child(Button.new())
	level.add_child(Button.new())

	# All enemies below the level, also in instanced scenes (owned = false).
	var enemies: Array[Node] = $Level.find_children("*", "Button", true, false)
	for enemy in enemies:
		print("Found: ", enemy.name)
	get_tree().quit()
