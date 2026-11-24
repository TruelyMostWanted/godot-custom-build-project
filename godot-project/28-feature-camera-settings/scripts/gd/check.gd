extends FeatureCheck
## GDScript checks of 28-feature-camera-settings. Check the same things as scripts/cs/Issue28FeatureCameraSettingsCheck.cs.
##
## The camera values go to the Camera3D nodes in the group "camera_settings" and to added cameras, at once.
## Cameras outside the group keep their authored values. The motion values are game-side (never pushed).

const CAMERA_FILE := "user://settings/camera.ini"
const NAMES := ["enabled", "field_of_view", "keep_aspect", "near", "far", "shake_intensity", "head_bob_intensity", "motion_blur_intensity"]
const GAME_SIDE := ["shake_intensity", "head_bob_intensity", "motion_blur_intensity"]

var server = null # Untyped: Engine.get_singleton() returns an Object.


func _get_check_name() -> String:
	return "28-feature-camera-settings"


func _run_checks() -> void:
	expect(Engine.has_singleton("CameraSettingsServer"), "CameraSettingsServer is a registered singleton")
	server = Engine.get_singleton("CameraSettingsServer")
	expect(server is SettingsServer, "CameraSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"camera"), server, "the domain is registered as \"camera\"")
	expect(server.is_started(), "the domain was started before the main scene")
	expect_equal(server.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the SceneTree exists")
	expect_equal(server.get_file_path(), CAMERA_FILE, "file path")
	expect(server.get_settings() is CameraSettings, "the settings resource is a CameraSettings")
	expect_equal(server.get_camera_group(), &"camera_settings", "get_camera_group()")
	var names: PackedStringArray = server.get_setting_names()
	expect_equal(names.size(), NAMES.size(), "8 values")
	for setting in NAMES:
		expect(names.has(setting), "setting \"%s\" exists" % setting)
	for setting in GAME_SIDE:
		expect((server.get_setting_flags(setting) & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET) != 0, "\"%s\" is game-side" % setting)

	server.reset_to_defaults()
	_check_defaults()
	await _check_group()
	_check_added_camera()
	_check_game_side()
	_check_validation()
	await _check_storage_and_scene_change()

	# Leave no player file behind.
	server.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(CAMERA_FILE)


func _authored_camera() -> Camera3D:
	var camera := Camera3D.new()
	camera.fov = 60.0
	camera.keep_aspect = Camera3D.KEEP_WIDTH
	camera.near = 0.2
	camera.far = 500.0
	return camera


func _is_authored(camera: Camera3D) -> bool:
	return is_equal_approx(camera.fov, 60.0) and camera.keep_aspect == Camera3D.KEEP_WIDTH and is_equal_approx(camera.near, 0.2) and is_equal_approx(camera.far, 500.0)


func _check_defaults() -> void:
	expect(is_equal_approx(server.get_default_value(&"field_of_view"), ProjectSettings.get_setting("settings/camera/field_of_view")), "default FOV is the project setting")
	expect_equal(server.get_default_value(&"keep_aspect"), ProjectSettings.get_setting("settings/camera/keep_aspect"), "default keep_aspect is the project setting")
	expect(is_equal_approx(server.get_default_value(&"near"), ProjectSettings.get_setting("settings/camera/near")), "default near is the project setting")
	expect(is_equal_approx(server.get_default_value(&"far"), ProjectSettings.get_setting("settings/camera/far")), "default far is the project setting")
	expect(server.is_enabled(), "enabled by default")
	expect(is_equal_approx(server.get_shake_intensity(), 1.0), "default shake intensity 1")


func _check_group() -> void:
	server.set_field_of_view(90.0)
	server.set_keep_aspect(Camera3D.KEEP_HEIGHT)
	var player := _authored_camera()
	player.add_to_group(server.get_camera_group())
	add_child(player)
	expect(server.has_camera(player), "a camera in the group is managed when it enters the tree")
	expect(is_equal_approx(player.fov, 90.0), "a camera in the group gets the FOV")
	expect_equal(player.keep_aspect, Camera3D.KEEP_HEIGHT, "a camera in the group gets keep_aspect")
	server.set_field_of_view(100.0)
	expect(is_equal_approx(player.fov, 100.0), "a camera in the group gets later changes at once")
	server.set_near(0.5)
	server.set_far(0.1)
	expect(is_equal_approx(player.near, 0.5), "near reaches the camera")
	expect(is_equal_approx(player.far, 0.51), "far stays above near")
	server.set_far(1000.0)
	expect(is_equal_approx(player.far, 1000.0), "far reaches the camera")

	# A cutscene camera outside the group: untouched, also while it is current.
	var cutscene := _authored_camera()
	add_child(cutscene)
	cutscene.make_current()
	server.set_field_of_view(70.0)
	expect(not server.has_camera(cutscene), "a camera outside the group is not managed")
	expect(_is_authored(cutscene), "a camera outside the group keeps its authored values")

	# Off: authored values back; on: the settings again.
	server.set_enabled(false)
	expect(_is_authored(player), "enabled = false gives the authored values back")
	server.set_enabled(true)
	expect(is_equal_approx(player.fov, 70.0), "enabled = true applies the settings again")

	# Joined the group while in the tree.
	cutscene.add_to_group(server.get_camera_group())
	server.apply_to_cameras()
	expect(is_equal_approx(cutscene.fov, 70.0), "apply_to_cameras() picks up cameras that joined the group in the tree")
	server.remove_camera(cutscene)
	expect(_is_authored(cutscene), "remove_camera() gives the authored values back")

	player.queue_free()
	cutscene.queue_free()
	await get_tree().process_frame
	server.reset_to_defaults()


func _check_added_camera() -> void:
	var camera := _authored_camera()
	server.add_camera(camera)
	expect(server.has_camera(camera), "add_camera()")
	expect(server.get_managed_cameras().has(camera), "get_managed_cameras() has the added camera")
	server.set_field_of_view(85.0)
	expect(is_equal_approx(camera.fov, 85.0), "an added camera gets later changes")
	server.remove_camera(camera)
	expect(not server.has_camera(camera), "remove_camera()")
	expect(_is_authored(camera), "a removed camera has its authored values")
	server.set_field_of_view(95.0)
	expect(is_equal_approx(camera.fov, 60.0), "a removed camera is not changed any more")
	camera.free()
	server.reset_to_defaults()


func _check_game_side() -> void:
	var changed := []
	var on_changed := func(setting, _value): changed.append(setting)
	server.value_changed.connect(on_changed)
	server.set_shake_intensity(0.0)
	server.set_head_bob_intensity(0.5)
	server.set_motion_blur_intensity(0.25)
	server.value_changed.disconnect(on_changed)
	expect(is_equal_approx(server.get_shake_intensity(), 0.0), "shake intensity is stored")
	expect(is_equal_approx(server.get_head_bob_intensity(), 0.5), "head bob intensity is stored")
	expect(is_equal_approx(server.get_motion_blur_intensity(), 0.25), "motion blur intensity is stored")
	expect(changed.has(&"shake_intensity") or changed.has("shake_intensity"), "value_changed is emitted for game-side values")
	server.reset_to_defaults()


func _check_validation() -> void:
	server.set_field_of_view(200.0)
	expect(is_equal_approx(server.get_field_of_view(), 179.0), "the FOV is clamped to 179")
	server.set_near(0.0)
	expect(is_equal_approx(server.get_near(), 0.001), "near is clamped to 0.001")
	server.set_shake_intensity(3.0)
	expect(is_equal_approx(server.get_shake_intensity(), 1.0), "the shake intensity is clamped to 1")
	server.set_value(&"keep_aspect", 7)
	expect_equal(server.get_value(&"keep_aspect"), server.get_default_value(&"keep_aspect"), "an invalid keep_aspect falls back")
	server.reset_to_defaults()


func _check_storage_and_scene_change() -> void:
	# FOV changed and saved; a "restart" loads it again.
	server.set_field_of_view(105.0)
	server.set_keep_aspect(Camera3D.KEEP_WIDTH)
	expect_equal(server.save(), OK, "save()")
	expect(FileAccess.file_exists(CAMERA_FILE), "the file exists")
	expect(FileAccess.get_file_as_string(CAMERA_FILE).contains("field_of_view"), "the INI file has field_of_view")
	server.reset_to_defaults()
	expect_equal(server.load(), OK, "load()")
	expect(is_equal_approx(server.get_field_of_view(), 105.0), "the FOV is loaded")
	server.apply()

	# A scene change: the player camera is saved in the group in its scene.
	var level := Node3D.new()
	var camera := _authored_camera()
	camera.name = "PlayerCamera"
	camera.add_to_group(server.get_camera_group(), true)
	level.add_child(camera)
	camera.owner = level
	var packed := PackedScene.new()
	expect_equal(packed.pack(level), OK, "pack the level")
	level.free()
	var instance := packed.instantiate()
	add_child(instance)
	var loaded: Camera3D = instance.get_node("PlayerCamera")
	expect(is_equal_approx(loaded.fov, 105.0), "the camera of a new scene gets the loaded FOV")
	expect_equal(loaded.keep_aspect, Camera3D.KEEP_WIDTH, "the camera of a new scene gets keep_aspect")
	instance.queue_free()
	await get_tree().process_frame
