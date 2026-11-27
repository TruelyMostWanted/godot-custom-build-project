extends Node2D
## Minimal example of XML: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/XML
## In the editor: double-click level.xml for live errors, select it once for the element tree (no code).

@export var level: XML = preload("res://xml/resources/level.xml")
@export var enemy_scenes: Dictionary[String, PackedScene] = {}


func _ready() -> void:
	# Using the API
	var root := level.get_root()
	print(root.get_attribute("name"))
	print(level.find_first("music").text)
	for spawn in level.find_all("spawn"):
		print(spawn.get_attribute("enemy"), " at ", spawn.get_attribute("x"), ",", spawn.get_attribute("y"))

	var save := XML.new()
	var custom_root := save.create_root("level")
	custom_root.set_attribute("name", "Custom")
	var custom_spawn := XMLElement.new()
	custom_spawn.name = "spawn"
	custom_spawn.set_attribute("enemy", "slime")
	custom_root.add_child(custom_spawn)
	save.save("user://custom_level.xml")
	print(FileAccess.get_file_as_string("user://custom_level.xml"))

	# Spawn the enemies of a level: one empty Node2D scene stands in for goblin.tscn and orc.tscn.
	var enemy_scene := PackedScene.new()
	var template := Node2D.new()
	enemy_scene.pack(template)
	template.free()
	enemy_scenes["goblin"] = enemy_scene
	enemy_scenes["orc"] = enemy_scene
	for spawn in level.find_all("spawn"):
		var enemy := enemy_scenes[spawn.get_attribute("enemy")].instantiate() as Node2D
		enemy.position = Vector2(spawn.get_attribute("x").to_int(), spawn.get_attribute("y").to_int()) * 16
		add_child(enemy)
		print("Spawned at ", enemy.position)

	# Save your own resource as XML
	var sword := Item.new()
	sword.name = "Sword"
	ResourceSaver.save(sword, "user://sword.xml")
	var loaded := load("user://sword.xml") as Item
	var text := XML.serialize(sword)
	print(loaded.name)
	print(text)
	get_tree().quit()
