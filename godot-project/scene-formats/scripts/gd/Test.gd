extends FeatureTest
## Full API test of Scene Formats (runtime glTF/GLB/FBX loader, glTF/GLB saver, settings). Same tests as scripts/cs/Test.cs.

const DIR := "user://scene-formats-test"
const HULL_POSITION := Vector3(0, 1, 0)
const SETTINGS := "filesystem/scene_formats/"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_test_settings()
	_test_extensions()
	_test_glb()
	_test_gltf()
	_test_cache()
	await _test_threaded()
	_test_fbx_not_saved()


# A Node3D "Ship" with one MeshInstance3D "Hull" (BoxMesh), packed.
func _make_ship() -> PackedScene:
	var ship := Node3D.new()
	ship.name = "Ship"
	var hull := MeshInstance3D.new()
	hull.name = "Hull"
	hull.mesh = BoxMesh.new()
	hull.position = HULL_POSITION
	ship.add_child(hull)
	hull.owner = ship
	var scene := PackedScene.new()
	expect_equal(scene.pack(ship), OK, "the test scene packs")
	ship.free()
	return scene


func _expect_ship(scene: PackedScene, label: String) -> void:
	expect(scene != null, "%s loads as a PackedScene" % label)
	var root: Node = scene.instantiate()
	var hull: MeshInstance3D = root.find_child("Hull", true, false) as MeshInstance3D
	expect(hull != null, "%s contains the MeshInstance3D Hull" % label)
	expect_equal(hull.mesh.get_surface_count(), 1, "%s: Hull has a mesh with one surface" % label)
	expect(hull.position.is_equal_approx(HULL_POSITION), "%s: Hull keeps its position" % label)
	root.free()


func _test_settings() -> void:
	expect_equal(ProjectSettings.get_setting(SETTINGS + "embedded_image_handling"), GLTFState.HANDLE_BINARY_IMAGE_MODE_EMBED_AS_UNCOMPRESSED, "embedded_image_handling defaults to Embed as Uncompressed")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "import_animations"), true, "import_animations defaults to true")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "animation_fps"), 30.0, "animation_fps defaults to 30")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "animation_trimming"), false, "animation_trimming defaults to false")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "remove_immutable_tracks"), true, "remove_immutable_tracks defaults to true")


func _test_extensions() -> void:
	var load_extensions: PackedStringArray = ResourceLoader.get_recognized_extensions_for_type("PackedScene")
	for extension: String in ["gltf", "glb", "fbx"]:
		expect(load_extensions.has(extension), "ResourceLoader lists .%s for PackedScene" % extension)
	var save_extensions: PackedStringArray = ResourceSaver.get_recognized_extensions(_make_ship())
	expect(save_extensions.has("glb"), "ResourceSaver lists .glb for a PackedScene")
	expect(save_extensions.has("gltf"), "ResourceSaver lists .gltf for a PackedScene")
	expect(not save_extensions.has("fbx"), "ResourceSaver does not list .fbx")


func _test_glb() -> void:
	var path: String = DIR + "/ship.glb"
	expect_equal(ResourceSaver.save(_make_ship(), path), OK, "ResourceSaver.save writes .glb")
	expect(FileAccess.file_exists(path), "the .glb file exists")
	expect(ResourceLoader.exists(path, "PackedScene"), "ResourceLoader.exists finds the .glb")
	_expect_ship(ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE), ".glb")


func _test_gltf() -> void:
	var path: String = DIR + "/ship.gltf"
	expect_equal(ResourceSaver.save(_make_ship(), path), OK, "ResourceSaver.save writes .gltf")
	expect(FileAccess.file_exists(DIR + "/ship0.bin"), ".gltf has an external .bin buffer")
	_expect_ship(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE), ".gltf")


func _test_cache() -> void:
	var first: Resource = ResourceLoader.load(DIR + "/ship.glb")
	var second: Resource = ResourceLoader.load(DIR + "/ship.glb")
	expect(first == second, "cached loads return the same PackedScene")


func _test_threaded() -> void:
	var path: String = DIR + "/ship_threaded.glb"
	ResourceSaver.save(_make_ship(), path)
	expect_equal(ResourceLoader.load_threaded_request(path, "PackedScene"), OK, "load_threaded_request accepts the .glb")
	while ResourceLoader.load_threaded_get_status(path) == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
	expect_equal(ResourceLoader.load_threaded_get_status(path), ResourceLoader.THREAD_LOAD_LOADED, "the threaded load finishes")
	_expect_ship(ResourceLoader.load_threaded_get(path) as PackedScene, "threaded .glb")


# FBX is read-only (ufbx).
func _test_fbx_not_saved() -> void:
	expect(ResourceSaver.save(_make_ship(), DIR + "/ship.fbx") != OK, "ResourceSaver.save to .fbx fails")
