extends Node
## Minimal example of Resource Database: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Resource-Database

const ITEMS := "res://resource-database/resources/items/"


func _ready() -> void:
	# Preload the items of the game
	var armor := FilesManifest.new()
	armor.folder_paths = PackedStringArray(["res://resource-database/resources/armor"])
	armor.extensions = PackedStringArray(["tres"])

	var list := Preloads.new()
	list.paths = PackedStringArray([ITEMS + "sword.tres", ITEMS + "shield.tres"])
	list.manifests = [armor]
	list.keys = { "hero_sword": ITEMS + "sword.tres" }

	ResourceDatabase.preload_finished.connect(_on_preload_finished)
	ResourceDatabase.preload_from(list)

	# Preload at startup
	# Nothing to run here: the Preloads is saved as resources/preloads.tres and added to the project setting
	# filesystem/resources/preloads. This example does not change the project settings.


func _on_preload_finished(_list: Preloads, error: Error) -> void:
	print("Preload finished: ", error)
	var sword: Resource = ResourceDatabase.get_resource("hero_sword")
	print("hero_sword: ", sword.resource_name)

	# Find resources by pattern and type
	# (the guide finds DatabaseItem resources; the example items are plain Resources, so the type is Resource)
	var swords: Array[Resource] = ResourceDatabase.find_resources("%sword%", "Resource")
	var item_keys: PackedStringArray = ResourceDatabase.find_keys("%items/%")
	print("Swords: ", swords.size(), ", item keys: ", item_keys.size())
	if ResourceDatabase.add_resource_if_missing("player/stats", Resource.new()):
		print("stored")

	# Free the resources of a level
	var removed: int = ResourceDatabase.remove_matching(ITEMS + "%")
	print("Removed: ", removed)
	ResourceDatabase.clear()
	get_tree().quit()
