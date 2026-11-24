extends FeatureCheck
## GDScript checks of 23-feature-video-shadow-settings. Check the same things as scripts/cs/Issue23FeatureVideoShadowSettingsCheck.cs.
##
## VideoShadowSettingsServer: the positional shadow atlas goes to the root viewport at once, the directional
## shadow map and both filter qualities to the RenderingServer. A small 3D scene with a directional and an omni
## light shows that the shadows change at once, and the frame time after an atlas change is printed (hitch).

const SHADOW_FILE := "user://settings/video_shadow.ini"
const NAMES := ["directional_shadow_atlas_size", "directional_shadow_16_bits", "directional_soft_shadow_filter_quality", "positional_shadow_atlas_size", "positional_shadow_atlas_16_bits", "positional_shadow_atlas_quad_0", "positional_shadow_atlas_quad_1", "positional_shadow_atlas_quad_2", "positional_shadow_atlas_quad_3", "positional_soft_shadow_filter_quality"]

var _scene: Node3D


func _get_check_name() -> String:
	return "23-feature-video-shadow-settings"


func _run_checks() -> void:
	expect(Engine.has_singleton("VideoShadowSettingsServer"), "VideoShadowSettingsServer is a registered singleton")
	expect(Engine.get_singleton("VideoShadowSettingsServer") is SettingsServer, "VideoShadowSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"video_shadow"), Engine.get_singleton("VideoShadowSettingsServer"), "the domain is registered as \"video_shadow\"")
	expect(VideoShadowSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(VideoShadowSettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the root viewport exists")
	expect_equal(VideoShadowSettingsServer.get_file_path(), SHADOW_FILE, "file path")
	expect(VideoShadowSettingsServer.get_settings() is VideoShadowSettings, "the settings resource is a VideoShadowSettings")
	var names := VideoShadowSettingsServer.get_setting_names()
	expect_equal(names.size(), NAMES.size(), "10 values")
	for setting in NAMES:
		expect(names.has(setting), "setting \"%s\" exists" % setting)
		expect(VideoShadowSettingsServer.get_setting_flags(setting) & SettingsServer.SETTING_FLAG_RESTART_REQUIRED == 0, "\"%s\" takes effect at once" % setting)

	VideoShadowSettingsServer.reset_to_defaults()
	_check_defaults()
	_check_support()
	_check_apply()
	_check_viewports()
	await _check_group()
	_check_validation()
	_check_storage()
	await _check_scene()

	# Leave no player file and no changed values behind.
	VideoShadowSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(SHADOW_FILE)


func _check_defaults() -> void:
	var root := get_tree().root
	expect_equal(VideoShadowSettingsServer.get_default_value(&"directional_shadow_atlas_size"), ProjectSettings.get_setting_with_override("rendering/lights_and_shadows/directional_shadow/size"), "default directional size is the project setting")
	expect_equal(VideoShadowSettingsServer.get_default_value(&"directional_soft_shadow_filter_quality"), ProjectSettings.get_setting_with_override("rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality"), "default directional quality is the project setting")
	expect_equal(VideoShadowSettingsServer.get_default_value(&"positional_shadow_atlas_size"), ProjectSettings.get_setting_with_override("rendering/lights_and_shadows/positional_shadow/atlas_size"), "default positional size is the project setting")
	expect_equal(VideoShadowSettingsServer.get_default_value(&"positional_shadow_atlas_quad_3"), ProjectSettings.get_setting_with_override("rendering/lights_and_shadows/positional_shadow/atlas_quadrant_3_subdiv"), "default quadrant 3 is the project setting")
	expect_equal(root.positional_shadow_atlas_size, VideoShadowSettingsServer.get_positional_shadow_atlas_size(), "the root viewport has the default positional size")
	expect_equal(root.positional_shadow_atlas_quad_0, VideoShadowSettingsServer.get_positional_shadow_atlas_quadrant_subdiv(0), "the root viewport has the default quadrant 0")


func _check_support() -> void:
	var method := RenderingServer.get_current_rendering_method()
	var unsupported := VideoShadowSettingsServer.get_unsupported_setting_names()
	expect(unsupported.is_empty(), "every renderer uses every value")
	for setting in NAMES:
		expect(VideoShadowSettingsServer.is_value_supported(setting), "\"%s\" is supported" % setting)
		expect(VideoShadowSettingsServer.is_renderer_supported(setting), "is_renderer_supported(\"%s\")" % setting)
	print("    renderer: %s, unsupported: %s" % [method, unsupported])


func _check_apply() -> void:
	var root := get_tree().root
	VideoShadowSettingsServer.set_positional_shadow_atlas_size(2048)
	expect_equal(root.positional_shadow_atlas_size, 2048, "positional_shadow_atlas_size reaches the root at once")
	VideoShadowSettingsServer.set_positional_shadow_atlas_16_bits(false)
	expect(not root.positional_shadow_atlas_16_bits, "positional_shadow_atlas_16_bits reaches the root at once")
	for i in 4:
		VideoShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(i, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_16)
		expect_equal(root.get_positional_shadow_atlas_quadrant_subdiv(i), Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_16, "quadrant %d reaches the root at once" % i)

	# Global values: no getter on RenderingServer, the domain keeps them.
	VideoShadowSettingsServer.set_directional_shadow_atlas_size(2048)
	expect_equal(VideoShadowSettingsServer.get_directional_shadow_atlas_size(), 2048, "directional_shadow_atlas_size is kept")
	VideoShadowSettingsServer.set_directional_shadow_16_bits(false)
	expect(not VideoShadowSettingsServer.is_directional_shadow_16_bits_enabled(), "directional_shadow_16_bits is kept")
	VideoShadowSettingsServer.set_directional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_HIGH)
	expect_equal(VideoShadowSettingsServer.get_directional_soft_shadow_filter_quality(), RenderingServer.SHADOW_QUALITY_SOFT_HIGH, "directional quality is kept")
	VideoShadowSettingsServer.set_positional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_HARD)
	expect_equal(VideoShadowSettingsServer.get_positional_soft_shadow_filter_quality(), RenderingServer.SHADOW_QUALITY_HARD, "positional quality is kept")

	# Changed on the root directly, read back.
	root.positional_shadow_atlas_size = 1024
	VideoShadowSettingsServer.read_from_engine()
	expect_equal(VideoShadowSettingsServer.get_positional_shadow_atlas_size(), 1024, "read_from_engine() reads the root back")
	expect_equal(VideoShadowSettingsServer.get_directional_shadow_atlas_size(), 2048, "read_from_engine() keeps the global values")

	VideoShadowSettingsServer.reset_to_defaults()
	expect_equal(root.positional_shadow_atlas_size, ProjectSettings.get_setting_with_override("rendering/lights_and_shadows/positional_shadow/atlas_size"), "reset_to_defaults() gives back the positional size")
	expect_equal(root.positional_shadow_atlas_16_bits, ProjectSettings.get_setting("rendering/lights_and_shadows/positional_shadow/atlas_16_bits"), "reset_to_defaults() gives back the bit depth")


func _check_viewports() -> void:
	var added := SubViewport.new()
	VideoShadowSettingsServer.set_positional_shadow_atlas_size(1024)
	VideoShadowSettingsServer.add_viewport(added)
	expect(VideoShadowSettingsServer.has_viewport(added), "has_viewport()")
	expect_equal(added.positional_shadow_atlas_size, 1024, "an added viewport gets the values at once")
	VideoShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(2, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_1)
	expect_equal(added.positional_shadow_atlas_quad_2, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_1, "an added viewport gets later changes")
	VideoShadowSettingsServer.remove_viewport(added)
	expect(not VideoShadowSettingsServer.has_viewport(added), "remove_viewport()")
	VideoShadowSettingsServer.set_positional_shadow_atlas_size(512)
	expect_equal(added.positional_shadow_atlas_size, 1024, "a removed viewport keeps its values")
	added.free()
	VideoShadowSettingsServer.reset_to_defaults()


func _check_group() -> void:
	expect_equal(VideoShadowSettingsServer.get_viewport_group(), &"video_shadow_settings", "get_viewport_group()")
	VideoShadowSettingsServer.set_positional_shadow_atlas_size(1024)
	var grouped := SubViewport.new()
	grouped.add_to_group(VideoShadowSettingsServer.get_viewport_group())
	add_child(grouped)
	expect_equal(grouped.positional_shadow_atlas_size, 1024, "a viewport in the group gets the values when it enters the tree")
	VideoShadowSettingsServer.set_positional_shadow_atlas_16_bits(false)
	expect(not grouped.positional_shadow_atlas_16_bits, "a viewport in the group gets later changes")
	var other := SubViewport.new()
	add_child(other)
	expect_equal(other.positional_shadow_atlas_size, 2048, "a viewport outside the group is untouched (Viewport default)")
	grouped.queue_free()
	other.queue_free()
	await get_tree().process_frame
	VideoShadowSettingsServer.reset_to_defaults()


func _check_validation() -> void:
	VideoShadowSettingsServer.set_directional_shadow_atlas_size(3000)
	expect_equal(VideoShadowSettingsServer.get_directional_shadow_atlas_size(), 4096, "sizes are rounded up to a power of two")
	VideoShadowSettingsServer.set_directional_shadow_atlas_size(16)
	expect_equal(VideoShadowSettingsServer.get_directional_shadow_atlas_size(), 256, "the directional size is clamped to 256")
	VideoShadowSettingsServer.set_positional_shadow_atlas_size(100000)
	expect_equal(VideoShadowSettingsServer.get_positional_shadow_atlas_size(), 16384, "the positional size is clamped to 16384")
	VideoShadowSettingsServer.set_positional_shadow_atlas_size(0)
	expect_equal(VideoShadowSettingsServer.get_positional_shadow_atlas_size(), 0, "0 disables positional shadows")
	expect_equal(get_tree().root.positional_shadow_atlas_size, 0, "0 reaches the root")
	VideoShadowSettingsServer.set_value(&"directional_soft_shadow_filter_quality", 9.0)
	expect_equal(VideoShadowSettingsServer.get_value(&"directional_soft_shadow_filter_quality"), VideoShadowSettingsServer.get_default_value(&"directional_soft_shadow_filter_quality"), "an invalid quality falls back to the default")
	VideoShadowSettingsServer.reset_to_defaults()


func _check_storage() -> void:
	VideoShadowSettingsServer.set_directional_shadow_atlas_size(2048)
	VideoShadowSettingsServer.set_positional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_ULTRA)
	VideoShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(1, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_64)
	expect_equal(VideoShadowSettingsServer.save(), OK, "save()")
	expect(FileAccess.file_exists(SHADOW_FILE), "the file exists")
	var ini := FileAccess.get_file_as_string(SHADOW_FILE)
	expect(ini.contains("directional_shadow_atlas_size"), "the INI file has directional_shadow_atlas_size")
	VideoShadowSettingsServer.reset_to_defaults()
	expect_equal(VideoShadowSettingsServer.load(), OK, "load()")
	expect_equal(VideoShadowSettingsServer.get_directional_shadow_atlas_size(), 2048, "the directional size is loaded")
	expect_equal(VideoShadowSettingsServer.get_positional_soft_shadow_filter_quality(), RenderingServer.SHADOW_QUALITY_SOFT_ULTRA, "the positional quality is loaded")
	expect_equal(VideoShadowSettingsServer.get_positional_shadow_atlas_quadrant_subdiv(1), Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_64, "quadrant 1 is loaded")
	VideoShadowSettingsServer.apply()
	expect_equal(get_tree().root.positional_shadow_atlas_quad_1, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_64, "the loaded value reaches the root after apply()")
	VideoShadowSettingsServer.reset_to_defaults()


# A box on a floor, lit by a directional and an omni light with shadows.
func _build_scene() -> void:
	_scene = Node3D.new()
	add_child(_scene)
	var camera := Camera3D.new()
	camera.position = Vector3(0, 3, 5)
	_scene.add_child(camera)
	camera.look_at(Vector3.ZERO)
	camera.current = true
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(10, 10)
	floor_mesh.mesh = plane
	_scene.add_child(floor_mesh)
	var box := MeshInstance3D.new()
	box.mesh = BoxMesh.new()
	box.position = Vector3(0, 0.5, 0)
	_scene.add_child(box)
	var sun := DirectionalLight3D.new()
	sun.shadow_enabled = true
	sun.rotation_degrees = Vector3(-50, 30, 0)
	_scene.add_child(sun)
	var omni := OmniLight3D.new()
	omni.shadow_enabled = true
	omni.omni_range = 8.0
	omni.position = Vector3(1.5, 2, 1.5)
	_scene.add_child(omni)


func _frame_image() -> Image:
	await RenderingServer.frame_post_draw
	return get_tree().root.get_texture().get_image()


# Milliseconds of the first frame after a change (the atlas is reallocated there).
func _measure(change: Callable) -> float:
	await get_tree().process_frame
	change.call()
	var start := Time.get_ticks_usec()
	await RenderingServer.frame_post_draw
	return (Time.get_ticks_usec() - start) / 1000.0


func _check_scene() -> void:
	if DisplayServer.get_name() == "headless":
		print("    headless: shadow scene skipped")
		return
	_build_scene()
	for i in 5:
		await get_tree().process_frame
	var method := RenderingServer.get_current_rendering_method()

	var before := await _frame_image()
	var ms := await _measure(func(): VideoShadowSettingsServer.set_directional_shadow_atlas_size(256))
	var after := await _frame_image()
	expect(before.get_data() != after.get_data(), "the directional shadow changes at once (4096 -> 256)")
	print("    [%s] directional atlas 256: %.2f ms" % [method, ms])
	ms = await _measure(func(): VideoShadowSettingsServer.set_directional_shadow_atlas_size(8192))
	print("    [%s] directional atlas 8192: %.2f ms" % [method, ms])
	ms = await _measure(func(): VideoShadowSettingsServer.set_directional_shadow_16_bits(false))
	print("    [%s] directional 32 bits: %.2f ms" % [method, ms])

	before = await _frame_image()
	ms = await _measure(func(): VideoShadowSettingsServer.set_positional_shadow_atlas_size(0))
	after = await _frame_image()
	expect(before.get_data() != after.get_data(), "the omni shadow disappears at once (positional atlas 0)")
	print("    [%s] positional atlas 0: %.2f ms" % [method, ms])
	ms = await _measure(func(): VideoShadowSettingsServer.set_positional_shadow_atlas_size(8192))
	print("    [%s] positional atlas 8192: %.2f ms" % [method, ms])
	ms = await _measure(func(): VideoShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(0, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_1))
	print("    [%s] quadrant 0 = 1 shadow: %.2f ms" % [method, ms])

	VideoShadowSettingsServer.set_directional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_HARD)
	VideoShadowSettingsServer.set_positional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_HARD)
	var hard := await _frame_image()
	VideoShadowSettingsServer.set_directional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_ULTRA)
	VideoShadowSettingsServer.set_positional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_ULTRA)
	var ultra := await _frame_image()
	expect(hard.get_data() != ultra.get_data(), "the soft shadow quality changes the shadows at once (Hard -> Ultra)")

	_scene.queue_free()
	await get_tree().process_frame
