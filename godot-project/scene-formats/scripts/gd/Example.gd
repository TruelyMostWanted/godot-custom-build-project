extends Node
## Minimal example of Scene Formats: the snippets of the wiki guide, in order. Each snippet is a method.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Scene-Formats


func _ready() -> void:
	_setup()
	_change_import_settings()
	_spawn_model()
	_export_scene()
	await _load_in_background()
	get_tree().quit()


# Setup: sample models in user://mods/ships (the guide assumes them).
func _setup() -> void:
	DirAccess.make_dir_recursive_absolute("user://mods/ships")
	DirAccess.make_dir_recursive_absolute("user://exports")
	var ship := Node3D.new()
	var hull := MeshInstance3D.new()
	hull.mesh = BoxMesh.new()
	ship.add_child(hull)
	hull.owner = ship
	var scene := PackedScene.new()
	scene.pack(ship)
	ship.free()
	ResourceSaver.save(scene, "user://mods/ships/cutter.glb")
	ResourceSaver.save(scene, "user://mods/ships/carrier.glb")


func _change_import_settings() -> void:
	# Change how models are imported at runtime
	ProjectSettings.set_setting("filesystem/scene_formats/import_animations", false)
	print("import_animations: ", ProjectSettings.get_setting("filesystem/scene_formats/import_animations"))


func _spawn_model() -> void:
	# Spawn a model from the mods folder
	var scene := load("user://mods/ships/cutter.glb") as PackedScene
	add_child(scene.instantiate())
	print("Spawned children: ", get_child_count())


func _export_scene() -> void:
	# Export a scene as GLB
	var ship := Node3D.new()
	var hull := MeshInstance3D.new()
	hull.mesh = BoxMesh.new()
	ship.add_child(hull)
	hull.owner = ship

	var scene := PackedScene.new()
	scene.pack(ship)
	ship.free()
	var error: Error = ResourceSaver.save(scene, "user://exports/ship.glb")
	print("Exported GLB: ", error == OK)


func _load_in_background() -> void:
	# Load a large model in the background
	ResourceLoader.load_threaded_request("user://mods/ships/carrier.glb")
	while ResourceLoader.load_threaded_get_status("user://mods/ships/carrier.glb") == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
	var scene := ResourceLoader.load_threaded_get("user://mods/ships/carrier.glb") as PackedScene
	add_child(scene.instantiate())
	print("Background load: ", get_child_count())
