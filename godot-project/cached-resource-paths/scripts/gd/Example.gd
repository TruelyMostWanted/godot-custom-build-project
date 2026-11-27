extends Node
## Minimal example of Cached Resource Paths: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Cached-Resource-Paths

const RESOURCES := "res://cached-resource-paths/resources/"


func _ready() -> void:
	# The guide counts the resources of a running game; here a few resources are loaded first.
	var loaded: Array[Resource] = []
	for file: String in ["first.tres", "second.tres", "third.tres", "texture.tres", "widget.tres"]:
		loaded.append(load(RESOURCES + file))

	# Count the loaded resources (PackedStringArray has no filter(), so the guide snippet gets an Array() around it)
	var files: Array = Array(ResourceLoader.get_cached_paths()).filter(func(p: String) -> bool: return not p.contains("::"))
	print("%d resources loaded, %d scenes" % [files.size(), ResourceLoader.get_cached_paths_with_extension("tscn").size()])

	# List the loaded textures of a level
	var textures: Array = Array(ResourceLoader.get_cached_paths_in_directory(RESOURCES)).filter(
			func(p: String) -> bool: return ResourceLoader.get_cached_ref(p) is Texture2D)
	print("Textures: ", textures)
	print("Widgets: ", ResourceLoader.get_cached_paths_for_type(&"Widget", true))

	# Count and filter by kind
	var imported: int = ResourceLoader.get_cached_count(ResourceLoader.CACHED_RESOURCE_KIND_IMPORTED)
	var own_files: int = ResourceLoader.get_cached_count(ResourceLoader.CACHED_RESOURCE_KIND_FILE)
	print("Imported: %d, own files: %d" % [imported, own_files])

	# Scenes and resources of the level, without the imported textures:
	var level: PackedStringArray = ResourceLoader.get_cached_paths_in_directory(RESOURCES, true,
			ResourceLoader.CACHED_RESOURCE_KIND_FILE | ResourceLoader.CACHED_RESOURCE_KIND_BUILT_IN)
	print("Level: ", level)

	for type: StringName in ResourceLoader.get_cached_type_counts():
		print(type, ": ", ResourceLoader.get_cached_type_counts()[type])
	get_tree().quit()
