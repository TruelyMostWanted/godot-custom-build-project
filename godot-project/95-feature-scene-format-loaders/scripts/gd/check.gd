extends FeatureCheck
## GDScript checks of 95-feature-scene-format-loaders. Check the same things as scripts/cs/Issue95FeatureSceneFormatLoadersCheck.cs.

const FOLDER := "user://95-feature-scene-format-loaders"
const HULL_POSITION := Vector3(0, 1, 0)


func _get_check_name() -> String:
	return "95-feature-scene-format-loaders"


# A Node3D "Ship" with one MeshInstance3D "Hull" (BoxMesh), packed.
func _make_ship_scene() -> PackedScene:
	var ship := Node3D.new()
	ship.name = "Ship"
	var hull := MeshInstance3D.new()
	hull.name = "Hull"
	hull.mesh = BoxMesh.new()
	hull.position = HULL_POSITION
	ship.add_child(hull)
	hull.owner = ship
	var scene := PackedScene.new()
	expect(scene.pack(ship) == OK, "the test scene packs")
	ship.free()
	return scene


func _find_hull(root: Node) -> MeshInstance3D:
	if root.name == &"Hull":
		return root as MeshInstance3D
	return root.find_child("Hull", true, false) as MeshInstance3D


func _check_ship(scene: PackedScene, label: String) -> void:
	if not expect(scene != null, "%s: loads as a PackedScene" % label):
		return
	var root := scene.instantiate()
	if not expect(root != null, "%s: instantiates" % label):
		return
	var hull := _find_hull(root)
	if expect(hull != null, "%s: contains the MeshInstance3D 'Hull'" % label):
		expect(hull.mesh != null and hull.mesh.get_surface_count() == 1, "%s: Hull has a mesh with one surface" % label)
		expect(hull.position.is_equal_approx(HULL_POSITION), "%s: Hull keeps its position" % label)
	root.free()


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(FOLDER)
	var ship := _make_ship_scene()

	# Settings: typed, with the documented defaults.
	expect_equal(ProjectSettings.get_setting("filesystem/scene_formats/embedded_image_handling"), GLTFState.HANDLE_BINARY_IMAGE_MODE_EMBED_AS_UNCOMPRESSED, "embedded_image_handling defaults to Embed as Uncompressed")
	expect_equal(ProjectSettings.get_setting("filesystem/scene_formats/import_animations"), true, "import_animations defaults to true")
	expect_equal(ProjectSettings.get_setting("filesystem/scene_formats/animation_fps"), 30.0, "animation_fps defaults to 30")
	expect_equal(ProjectSettings.get_setting("filesystem/scene_formats/animation_trimming"), false, "animation_trimming defaults to false")
	expect_equal(ProjectSettings.get_setting("filesystem/scene_formats/remove_immutable_tracks"), true, "remove_immutable_tracks defaults to true")

	# Extensions are known to ResourceLoader and ResourceSaver.
	var load_extensions := ResourceLoader.get_recognized_extensions_for_type("PackedScene")
	for extension in ["gltf", "glb", "fbx"]:
		expect(load_extensions.has(extension), "ResourceLoader lists .%s for PackedScene" % extension)
	var save_extensions := ResourceSaver.get_recognized_extensions(ship)
	expect(save_extensions.has("glb") and save_extensions.has("gltf"), "ResourceSaver lists .glb and .gltf for a PackedScene")
	expect(not save_extensions.has("fbx"), "ResourceSaver does not list .fbx")

	# .glb round trip.
	var glb_path := FOLDER.path_join("ship.glb")
	expect(ResourceSaver.save(ship, glb_path) == OK, "ResourceSaver.save writes .glb")
	expect(FileAccess.file_exists(glb_path), ".glb file exists")
	expect(ResourceLoader.exists(glb_path, "PackedScene"), "ResourceLoader.exists finds the .glb")
	_check_ship(ResourceLoader.load(glb_path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE), ".glb")

	# .gltf round trip (external .bin buffer).
	var gltf_path := FOLDER.path_join("ship.gltf")
	expect(ResourceSaver.save(ship, gltf_path) == OK, "ResourceSaver.save writes .gltf")
	expect(FileAccess.file_exists(FOLDER.path_join("ship0.bin")), ".gltf has an external .bin buffer")
	_check_ship(ResourceLoader.load(gltf_path, "", ResourceLoader.CACHE_MODE_IGNORE), ".gltf")

	# Cache: two loads give the same resource.
	var cached := ResourceLoader.load(glb_path)
	expect(cached != null and ResourceLoader.load(glb_path) == cached, "cached loads return the same PackedScene")

	# Threaded load.
	var threaded_path := FOLDER.path_join("ship_threaded.glb")
	expect(ResourceSaver.save(ship, threaded_path) == OK, "ResourceSaver.save writes a second .glb")
	expect(ResourceLoader.load_threaded_request(threaded_path, "PackedScene") == OK, "load_threaded_request accepts the .glb")
	var status := ResourceLoader.load_threaded_get_status(threaded_path)
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
		status = ResourceLoader.load_threaded_get_status(threaded_path)
	expect_equal(status, ResourceLoader.THREAD_LOAD_LOADED, "threaded load finishes")
	_check_ship(ResourceLoader.load_threaded_get(threaded_path), "threaded .glb")

	# FBX: loading only. Saving is not supported (ufbx is read-only).
	expect(ResourceSaver.save(ship, FOLDER.path_join("ship.fbx")) != OK, "ResourceSaver.save to .fbx fails")
