extends FeatureTest
## Full API test of Camera 3D Settings (Camera3DSettingsServer, Camera3DSettings). Same tests as scripts/cs/Test.cs.
## The camera values go to the Camera3D nodes in the group "camera_3d_settings" and to added cameras, at once.
## Cameras outside the group keep their authored values. The motion values are game-side (never pushed).

const CAMERA_FILE := "user://settings/camera_3d.ini"
const AUTHORED_FOV := 60.0
const AUTHORED_NEAR := 0.2
const AUTHORED_FAR := 500.0

var _received: Dictionary = {}
var _changed_settings: Array[StringName] = []


func _run_tests() -> void:
	Camera3DSettingsServer.reset_to_defaults()
	_test_defaults()
	_test_values()
	await _test_group()
	_test_added_camera()
	_test_game_side()
	_test_validation()
	_test_signals()
	await _test_storage_and_scene_change()
	_test_resource()
	Camera3DSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(CAMERA_FILE)


func _authored_camera() -> Camera3D:
	var camera := Camera3D.new()
	camera.fov = AUTHORED_FOV
	camera.keep_aspect = Camera3D.KEEP_WIDTH
	camera.near = AUTHORED_NEAR
	camera.far = AUTHORED_FAR
	return camera


func _is_authored(camera: Camera3D) -> bool:
	return is_equal_approx(camera.fov, AUTHORED_FOV) and camera.keep_aspect == Camera3D.KEEP_WIDTH and is_equal_approx(camera.near, AUTHORED_NEAR) and is_equal_approx(camera.far, AUTHORED_FAR)


func _test_defaults() -> void:
	var project_fov: float = ProjectSettings.get_setting("settings/camera_3d/field_of_view")
	var project_keep: int = ProjectSettings.get_setting("settings/camera_3d/keep_aspect")
	var project_near: float = ProjectSettings.get_setting("settings/camera_3d/near")
	var project_far: float = ProjectSettings.get_setting("settings/camera_3d/far")
	expect_equal(Camera3DSettingsServer.get_field_of_view(), project_fov, "the default FOV is the project setting")
	expect_equal(Camera3DSettingsServer.get_keep_aspect(), project_keep, "the default keep_aspect is the project setting")
	expect(is_equal_approx(Camera3DSettingsServer.get_near(), project_near), "the default near is the project setting")
	expect_equal(Camera3DSettingsServer.get_far(), project_far, "the default far is the project setting")
	expect(Camera3DSettingsServer.is_enabled(), "the settings are enabled by default")
	expect_equal(Camera3DSettingsServer.get_shake_intensity(), 1.0, "the default shake intensity is 1")
	expect_equal(Camera3DSettingsServer.get_camera_group(), &"camera_3d_settings", "get_camera_group returns the group name")


func _test_values() -> void:
	Camera3DSettingsServer.set_enabled(false)
	expect(not Camera3DSettingsServer.is_enabled(), "enabled round trip")
	Camera3DSettingsServer.set_field_of_view(90.0)
	expect_equal(Camera3DSettingsServer.get_field_of_view(), 90.0, "field_of_view round trip")
	Camera3DSettingsServer.set_keep_aspect(Camera3D.KEEP_HEIGHT)
	expect_equal(Camera3DSettingsServer.get_keep_aspect(), Camera3D.KEEP_HEIGHT, "keep_aspect round trip")
	Camera3DSettingsServer.set_near(0.5)
	expect_equal(Camera3DSettingsServer.get_near(), 0.5, "near round trip")
	Camera3DSettingsServer.set_far(2000.0)
	expect_equal(Camera3DSettingsServer.get_far(), 2000.0, "far round trip")
	Camera3DSettingsServer.set_shake_intensity(0.5)
	expect_equal(Camera3DSettingsServer.get_shake_intensity(), 0.5, "shake_intensity round trip")
	Camera3DSettingsServer.set_head_bob_intensity(0.25)
	expect_equal(Camera3DSettingsServer.get_head_bob_intensity(), 0.25, "head_bob_intensity round trip")
	Camera3DSettingsServer.set_motion_blur_intensity(0.75)
	expect_equal(Camera3DSettingsServer.get_motion_blur_intensity(), 0.75, "motion_blur_intensity round trip")
	Camera3DSettingsServer.reset_to_defaults()


func _test_group() -> void:
	Camera3DSettingsServer.set_field_of_view(90.0)
	Camera3DSettingsServer.set_keep_aspect(Camera3D.KEEP_HEIGHT)
	var player: Camera3D = _authored_camera()
	player.add_to_group(Camera3DSettingsServer.get_camera_group())
	add_child(player)
	expect(Camera3DSettingsServer.has_camera(player), "a camera in the group is managed when it enters the tree")
	expect_equal(player.fov, 90.0, "a camera in the group gets the FOV")
	expect_equal(player.keep_aspect, Camera3D.KEEP_HEIGHT, "a camera in the group gets keep_aspect")
	Camera3DSettingsServer.set_field_of_view(100.0)
	expect_equal(player.fov, 100.0, "a camera in the group gets later changes at once")
	Camera3DSettingsServer.set_near(0.5)
	Camera3DSettingsServer.set_far(0.1)
	expect_equal(player.near, 0.5, "near reaches the camera")
	expect(is_equal_approx(player.far, 0.51), "far stays above near")
	Camera3DSettingsServer.set_far(1000.0)
	expect_equal(player.far, 1000.0, "far reaches the camera")
	var cutscene: Camera3D = _authored_camera()
	add_child(cutscene)
	cutscene.make_current()
	Camera3DSettingsServer.set_field_of_view(70.0)
	expect(not Camera3DSettingsServer.has_camera(cutscene), "a camera outside the group is not managed")
	expect(_is_authored(cutscene), "a camera outside the group keeps its authored values, also while it is current")
	Camera3DSettingsServer.set_enabled(false)
	expect(_is_authored(player), "enabled = false gives the authored values back")
	Camera3DSettingsServer.set_enabled(true)
	expect_equal(player.fov, 70.0, "enabled = true applies the settings again")
	cutscene.add_to_group(Camera3DSettingsServer.get_camera_group())
	Camera3DSettingsServer.apply_to_cameras()
	expect_equal(cutscene.fov, 70.0, "apply_to_cameras picks up cameras that joined the group in the tree")
	Camera3DSettingsServer.remove_camera(cutscene)
	expect(_is_authored(cutscene), "remove_camera gives the authored values back")
	player.queue_free()
	cutscene.queue_free()
	await get_tree().process_frame
	Camera3DSettingsServer.reset_to_defaults()


func _test_added_camera() -> void:
	var camera: Camera3D = _authored_camera()
	Camera3DSettingsServer.add_camera(camera)
	expect(Camera3DSettingsServer.has_camera(camera), "add_camera: has_camera is true")
	expect(Camera3DSettingsServer.get_managed_cameras().has(camera), "get_managed_cameras has the added camera")
	Camera3DSettingsServer.set_field_of_view(85.0)
	expect_equal(camera.fov, 85.0, "an added camera gets later changes")
	Camera3DSettingsServer.remove_camera(camera)
	expect(not Camera3DSettingsServer.has_camera(camera), "remove_camera: has_camera is false")
	expect(_is_authored(camera), "a removed camera has its authored values")
	Camera3DSettingsServer.set_field_of_view(95.0)
	expect_equal(camera.fov, AUTHORED_FOV, "a removed camera is not changed any more")
	camera.free()
	Camera3DSettingsServer.reset_to_defaults()


func _test_game_side() -> void:
	var on_changed := func(setting: StringName, _value: Variant) -> void: _changed_settings.append(setting)
	Camera3DSettingsServer.value_changed.connect(on_changed)
	Camera3DSettingsServer.set_shake_intensity(0.0)
	Camera3DSettingsServer.value_changed.disconnect(on_changed)
	expect(_changed_settings.has(&"shake_intensity"), "value_changed is emitted for game-side values")
	var flags: int = Camera3DSettingsServer.get_setting_flags(&"shake_intensity")
	expect(flags & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET != 0, "shake_intensity is game-side")
	Camera3DSettingsServer.reset_to_defaults()


func _test_validation() -> void:
	Camera3DSettingsServer.set_field_of_view(200.0)
	expect_equal(Camera3DSettingsServer.get_field_of_view(), 179.0, "the FOV is clamped to 179")
	Camera3DSettingsServer.set_near(0.0)
	expect(is_equal_approx(Camera3DSettingsServer.get_near(), 0.001), "near is clamped to 0.001")
	Camera3DSettingsServer.set_shake_intensity(3.0)
	expect_equal(Camera3DSettingsServer.get_shake_intensity(), 1.0, "the shake intensity is clamped to 1")
	Camera3DSettingsServer.reset_to_defaults()


func _test_signals() -> void:
	Camera3DSettingsServer.field_of_view_changed.connect(func(value: float) -> void: _received["fov"] = value)
	Camera3DSettingsServer.keep_aspect_changed.connect(func(value: int) -> void: _received["keep_aspect"] = value)
	Camera3DSettingsServer.near_changed.connect(func(value: float) -> void: _received["near"] = value)
	Camera3DSettingsServer.far_changed.connect(func(value: float) -> void: _received["far"] = value)
	Camera3DSettingsServer.shake_intensity_changed.connect(func(value: float) -> void: _received["shake"] = value)
	Camera3DSettingsServer.head_bob_intensity_changed.connect(func(value: float) -> void: _received["head_bob"] = value)
	Camera3DSettingsServer.motion_blur_intensity_changed.connect(func(value: float) -> void: _received["motion_blur"] = value)
	Camera3DSettingsServer.set_field_of_view(90.0)
	Camera3DSettingsServer.set_keep_aspect(Camera3D.KEEP_WIDTH)
	Camera3DSettingsServer.set_near(0.5)
	Camera3DSettingsServer.set_far(2000.0)
	Camera3DSettingsServer.set_shake_intensity(0.5)
	Camera3DSettingsServer.set_head_bob_intensity(0.25)
	Camera3DSettingsServer.set_motion_blur_intensity(0.75)
	expect_equal(_received.get("fov"), 90.0, "field_of_view_changed passes the FOV")
	expect_equal(_received.get("keep_aspect"), Camera3D.KEEP_WIDTH, "keep_aspect_changed passes the mode")
	expect_equal(_received.get("near"), 0.5, "near_changed passes the distance")
	expect_equal(_received.get("far"), 2000.0, "far_changed passes the distance")
	expect_equal(_received.get("shake"), 0.5, "shake_intensity_changed passes the intensity")
	expect_equal(_received.get("head_bob"), 0.25, "head_bob_intensity_changed passes the intensity")
	expect_equal(_received.get("motion_blur"), 0.75, "motion_blur_intensity_changed passes the intensity")
	Camera3DSettingsServer.reset_to_defaults()


func _test_storage_and_scene_change() -> void:
	Camera3DSettingsServer.set_field_of_view(105.0)
	Camera3DSettingsServer.set_keep_aspect(Camera3D.KEEP_WIDTH)
	expect_equal(Camera3DSettingsServer.save(), OK, "save writes the file")
	Camera3DSettingsServer.reset_to_defaults()
	expect_equal(Camera3DSettingsServer.load(), OK, "load reads the file")
	expect_equal(Camera3DSettingsServer.get_field_of_view(), 105.0, "the FOV is loaded")
	Camera3DSettingsServer.apply()
	var level := Node3D.new()
	var camera: Camera3D = _authored_camera()
	camera.name = "PlayerCamera"
	camera.add_to_group(Camera3DSettingsServer.get_camera_group(), true)
	level.add_child(camera)
	camera.owner = level
	var packed := PackedScene.new()
	expect_equal(packed.pack(level), OK, "pack the level")
	level.free()
	var instance: Node = packed.instantiate()
	add_child(instance)
	var loaded: Camera3D = instance.get_node("PlayerCamera")
	expect_equal(loaded.fov, 105.0, "the camera of a new scene gets the loaded FOV")
	expect_equal(loaded.keep_aspect, Camera3D.KEEP_WIDTH, "the camera of a new scene gets keep_aspect")
	instance.queue_free()
	await get_tree().process_frame


func _test_resource() -> void:
	var settings := Camera3DSettings.new()
	expect(settings.enabled, "Camera3DSettings: enabled by default")
	expect_equal(settings.field_of_view, 75.0, "Camera3DSettings: default field_of_view")
	expect_equal(settings.keep_aspect, Camera3D.KEEP_HEIGHT, "Camera3DSettings: default keep_aspect")
	expect(is_equal_approx(settings.near, 0.05), "Camera3DSettings: default near")
	expect_equal(settings.far, 4000.0, "Camera3DSettings: default far")
	expect_equal(settings.shake_intensity, 1.0, "Camera3DSettings: default shake_intensity")
	expect_equal(settings.head_bob_intensity, 1.0, "Camera3DSettings: default head_bob_intensity")
	expect_equal(settings.motion_blur_intensity, 1.0, "Camera3DSettings: default motion_blur_intensity")
	settings.field_of_view = 90.0
	expect_equal(settings.field_of_view, 90.0, "Camera3DSettings: field_of_view is a plain property")
