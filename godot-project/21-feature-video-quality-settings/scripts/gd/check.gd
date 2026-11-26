extends FeatureCheck
## GDScript checks of 21-feature-video-quality-settings. Check the same things as scripts/cs/Issue21FeatureVideoQualitySettingsCheck.cs.
##
## The values go to the root viewport at once. Values the current renderer ignores (Compatibility: FSR, FXAA/SMAA,
## TAA, debanding, VRS; Mobile: FSR, TAA) are stored, but not pushed; the checks follow is_value_supported().

const QUALITY_FILE := "user://settings/render_quality.ini"
const NAMES := ["scaling_3d_mode", "scaling_3d_scale", "fsr_sharpness", "mesh_lod_threshold", "msaa_2d", "msaa_3d", "screen_space_aa", "use_taa", "use_debanding", "vrs_mode", "use_occlusion_culling", "occlusion_rays_per_thread", "occlusion_culling_build_quality"]


func _get_check_name() -> String:
	return "21-feature-video-quality-settings"


func _run_checks() -> void:
	await _check_typed_signals()
	expect(Engine.has_singleton("RenderQualitySettingsServer"), "RenderQualitySettingsServer is a registered singleton")
	expect(Engine.get_singleton("RenderQualitySettingsServer") is SettingsServer, "RenderQualitySettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"render_quality"), Engine.get_singleton("RenderQualitySettingsServer"), "the domain is registered as \"render_quality\"")
	expect(RenderQualitySettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(RenderQualitySettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the root viewport exists")
	expect_equal(RenderQualitySettingsServer.get_file_path(), QUALITY_FILE, "file path")
	expect(RenderQualitySettingsServer.get_settings() is RenderQualitySettings, "the settings resource is a RenderQualitySettings")
	var names := RenderQualitySettingsServer.get_setting_names()
	expect_equal(names.size(), NAMES.size(), "13 values")
	for setting in NAMES:
		expect(names.has(setting), "setting \"%s\" exists" % setting)

	RenderQualitySettingsServer.reset_to_defaults()
	_check_defaults()
	_check_support()
	_check_apply()
	_check_viewports()
	await _check_group()
	_check_validation()
	_check_storage()

	# Leave no player file and no changed root values behind.
	RenderQualitySettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(QUALITY_FILE)


func _supported(setting: StringName) -> bool:
	return RenderQualitySettingsServer.is_value_supported(setting)


func _check_defaults() -> void:
	var root := get_tree().root
	expect_equal(RenderQualitySettingsServer.get_default_value(&"msaa_3d"), ProjectSettings.get_setting("rendering/anti_aliasing/quality/msaa_3d"), "default msaa_3d is the project setting")
	expect_equal(RenderQualitySettingsServer.get_default_value(&"scaling_3d_mode"), ProjectSettings.get_setting("rendering/scaling_3d/mode"), "default scaling mode is the project setting")
	expect(is_equal_approx(RenderQualitySettingsServer.get_default_value(&"scaling_3d_scale"), ProjectSettings.get_setting("rendering/scaling_3d/scale")), "default scale is the project setting")
	expect(is_equal_approx(RenderQualitySettingsServer.get_default_value(&"mesh_lod_threshold"), ProjectSettings.get_setting("rendering/mesh_lod/lod_change/threshold_pixels")), "default LOD threshold is the project setting")
	expect_equal(RenderQualitySettingsServer.get_default_value(&"use_occlusion_culling"), ProjectSettings.get_setting("rendering/occlusion_culling/use_occlusion_culling"), "default occlusion culling is the project setting")
	expect_equal(RenderQualitySettingsServer.get_default_value(&"occlusion_rays_per_thread"), ProjectSettings.get_setting("rendering/occlusion_culling/occlusion_rays_per_thread"), "default occlusion rays is the project setting")
	expect_equal(root.msaa_3d, RenderQualitySettingsServer.get_msaa_3d(), "the root viewport has the default msaa_3d")


func _check_support() -> void:
	var method := RenderingServer.get_current_rendering_method()
	var unsupported := RenderQualitySettingsServer.get_unsupported_setting_names()
	for setting in NAMES:
		expect_equal(unsupported.has(setting), not _supported(setting), "get_unsupported_setting_names() matches is_value_supported(\"%s\")" % setting)
		expect_equal(_supported(setting), RenderQualitySettingsServer.is_renderer_supported(setting), "is_renderer_supported(\"%s\")" % setting)
	for setting in ["scaling_3d_mode", "scaling_3d_scale", "mesh_lod_threshold", "msaa_2d", "msaa_3d"]:
		expect(_supported(setting), "\"%s\" works in every renderer" % setting)
	expect(RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_BILINEAR), "bilinear scaling works everywhere")
	expect(RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_NEAREST), "nearest scaling works everywhere")
	if method == "gl_compatibility":
		for setting in ["fsr_sharpness", "screen_space_aa", "use_taa", "use_debanding", "vrs_mode"]:
			expect(not _supported(setting), "compatibility: \"%s\" is unsupported" % setting)
		expect(not RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR), "compatibility: no FSR")
	elif method == "mobile":
		expect(not _supported(&"use_taa"), "mobile: no TAA")
		expect(not RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR2), "mobile: no FSR 2")
	elif method == "forward_plus":
		expect(RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR2), "forward+: FSR 2")
		expect(_supported(&"use_taa"), "forward+: TAA")
	print("    renderer: %s, unsupported: %s" % [method, unsupported])


func _check_apply() -> void:
	var root := get_tree().root
	RenderQualitySettingsServer.set_scaling_3d_mode(Viewport.SCALING_3D_MODE_NEAREST)
	expect_equal(root.scaling_3d_mode, Viewport.SCALING_3D_MODE_NEAREST, "scaling_3d_mode reaches the root at once")
	RenderQualitySettingsServer.set_scaling_3d_scale(0.5)
	expect(is_equal_approx(root.scaling_3d_scale, 0.5), "scaling_3d_scale reaches the root at once")
	RenderQualitySettingsServer.set_mesh_lod_threshold(8.0)
	expect(is_equal_approx(root.mesh_lod_threshold, 8.0), "mesh_lod_threshold reaches the root at once")
	RenderQualitySettingsServer.set_msaa_2d(Viewport.MSAA_4X)
	expect_equal(root.msaa_2d, Viewport.MSAA_4X, "msaa_2d reaches the root at once")
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	expect_equal(root.msaa_3d, Viewport.MSAA_2X, "msaa_3d reaches the root at once")

	# Pushed where the renderer supports them, stored either way.
	RenderQualitySettingsServer.set_fsr_sharpness(1.0)
	RenderQualitySettingsServer.set_screen_space_aa(Viewport.SCREEN_SPACE_AA_FXAA)
	RenderQualitySettingsServer.set_use_taa(true)
	RenderQualitySettingsServer.set_use_debanding(true)
	RenderQualitySettingsServer.set_use_occlusion_culling(true)
	expect_equal(RenderQualitySettingsServer.get_screen_space_aa(), Viewport.SCREEN_SPACE_AA_FXAA, "screen_space_aa is stored")
	expect(RenderQualitySettingsServer.is_using_taa(), "use_taa is stored")
	if _supported(&"fsr_sharpness"):
		expect(is_equal_approx(root.fsr_sharpness, 1.0), "fsr_sharpness reaches the root at once")
	expect_equal(root.screen_space_aa == Viewport.SCREEN_SPACE_AA_FXAA, _supported(&"screen_space_aa"), "screen_space_aa reaches the root only if supported")
	expect_equal(root.use_taa, _supported(&"use_taa"), "use_taa reaches the root only if supported")
	expect_equal(root.use_debanding, _supported(&"use_debanding"), "use_debanding reaches the root only if supported")
	expect_equal(root.use_occlusion_culling, _supported(&"use_occlusion_culling"), "use_occlusion_culling reaches the root only if supported")
	if _supported(&"vrs_mode"):
		RenderQualitySettingsServer.set_vrs_mode(Viewport.VRS_TEXTURE)
		expect_equal(root.vrs_mode, Viewport.VRS_TEXTURE, "vrs_mode reaches the root at once")

	# A scaling mode the renderer can't use is stored; the root scales bilinearly.
	RenderQualitySettingsServer.set_scaling_3d_mode(Viewport.SCALING_3D_MODE_FSR2)
	expect_equal(RenderQualitySettingsServer.get_scaling_3d_mode(), Viewport.SCALING_3D_MODE_FSR2, "FSR 2 is stored")
	var expected_mode := Viewport.SCALING_3D_MODE_FSR2 if RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR2) else Viewport.SCALING_3D_MODE_BILINEAR
	expect_equal(root.scaling_3d_mode, expected_mode, "the root gets FSR 2 or the bilinear fallback")

	# The global occlusion values: no getter on RenderingServer, the domain keeps them.
	RenderQualitySettingsServer.set_occlusion_rays_per_thread(256)
	expect_equal(RenderQualitySettingsServer.get_occlusion_rays_per_thread(), 256, "occlusion_rays_per_thread is kept")
	RenderQualitySettingsServer.set_occlusion_culling_build_quality(RenderingServer.VIEWPORT_OCCLUSION_BUILD_QUALITY_LOW)
	expect_equal(RenderQualitySettingsServer.get_occlusion_culling_build_quality(), RenderingServer.VIEWPORT_OCCLUSION_BUILD_QUALITY_LOW, "occlusion_culling_build_quality is kept")

	# Changed on the root directly, read back.
	root.msaa_2d = Viewport.MSAA_8X
	RenderQualitySettingsServer.read_from_engine()
	expect_equal(RenderQualitySettingsServer.get_msaa_2d(), Viewport.MSAA_8X, "read_from_engine() reads the root back")
	expect(RenderQualitySettingsServer.is_using_taa(), "read_from_engine() keeps skipped values as stored")

	RenderQualitySettingsServer.reset_to_defaults()
	expect_equal(root.msaa_2d, ProjectSettings.get_setting("rendering/anti_aliasing/quality/msaa_2d"), "reset_to_defaults() gives back msaa_2d")
	expect(is_equal_approx(root.scaling_3d_scale, ProjectSettings.get_setting("rendering/scaling_3d/scale")), "reset_to_defaults() gives back the scale")


func _check_viewports() -> void:
	var added := SubViewport.new()
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_4X)
	RenderQualitySettingsServer.add_viewport(added)
	expect(RenderQualitySettingsServer.has_viewport(added), "has_viewport()")
	expect_equal(added.msaa_3d, Viewport.MSAA_4X, "an added viewport gets the values at once")
	RenderQualitySettingsServer.set_mesh_lod_threshold(16.0)
	expect(is_equal_approx(added.mesh_lod_threshold, 16.0), "an added viewport gets later changes")
	RenderQualitySettingsServer.remove_viewport(added)
	expect(not RenderQualitySettingsServer.has_viewport(added), "remove_viewport()")
	RenderQualitySettingsServer.set_mesh_lod_threshold(2.0)
	expect(is_equal_approx(added.mesh_lod_threshold, 16.0), "a removed viewport keeps its values")
	added.free()
	RenderQualitySettingsServer.reset_to_defaults()


func _check_group() -> void:
	expect_equal(RenderQualitySettingsServer.get_viewport_group(), &"render_quality_settings", "get_viewport_group()")
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_4X)
	var grouped := SubViewport.new()
	grouped.add_to_group(RenderQualitySettingsServer.get_viewport_group())
	add_child(grouped)
	expect_equal(grouped.msaa_3d, Viewport.MSAA_4X, "a viewport in the group gets the values when it enters the tree")
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_8X)
	expect_equal(grouped.msaa_3d, Viewport.MSAA_8X, "a viewport in the group gets later changes")
	var other := SubViewport.new()
	add_child(other)
	expect_equal(other.msaa_3d, Viewport.MSAA_DISABLED, "a viewport outside the group is untouched")
	grouped.queue_free()
	other.queue_free()
	await get_tree().process_frame
	RenderQualitySettingsServer.reset_to_defaults()


func _check_validation() -> void:
	RenderQualitySettingsServer.set_scaling_3d_scale(5.0)
	expect(is_equal_approx(RenderQualitySettingsServer.get_scaling_3d_scale(), 2.0), "the scale is clamped to 2.0")
	RenderQualitySettingsServer.set_occlusion_rays_per_thread(0)
	expect_equal(RenderQualitySettingsServer.get_occlusion_rays_per_thread(), 1, "the occlusion rays are clamped to 1")
	RenderQualitySettingsServer.set_value(&"msaa_3d", 9)
	expect_equal(RenderQualitySettingsServer.get_value(&"msaa_3d"), RenderQualitySettingsServer.get_default_value(&"msaa_3d"), "an invalid MSAA value falls back to the default")
	RenderQualitySettingsServer.reset_to_defaults()


func _check_storage() -> void:
	RenderQualitySettingsServer.set_scaling_3d_scale(0.75)
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	RenderQualitySettingsServer.set_use_taa(true)
	expect_equal(RenderQualitySettingsServer.save(), OK, "save()")
	expect(FileAccess.file_exists(QUALITY_FILE), "the file exists")
	var ini := FileAccess.get_file_as_string(QUALITY_FILE)
	expect(ini.contains("msaa_3d"), "the INI file has msaa_3d")
	RenderQualitySettingsServer.reset_to_defaults()
	expect_equal(RenderQualitySettingsServer.load(), OK, "load()")
	expect(is_equal_approx(RenderQualitySettingsServer.get_scaling_3d_scale(), 0.75), "the scale is loaded")
	expect_equal(RenderQualitySettingsServer.get_msaa_3d(), Viewport.MSAA_2X, "msaa_3d is loaded")
	expect(RenderQualitySettingsServer.is_using_taa(), "use_taa is loaded (also where the renderer ignores it)")
	RenderQualitySettingsServer.apply()
	expect_equal(get_tree().root.msaa_3d, Viewport.MSAA_2X, "the loaded value reaches the root after apply()")


## Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.
func _check_typed_signals() -> void:
	if not Engine.has_singleton("RenderQualitySettingsServer"):
		return
	var s: SettingsServer = Engine.get_singleton("RenderQualitySettingsServer")
	expect(s.has_signal(&"scaling_3d_scale_changed"), "RenderQualitySettingsServer has the typed signal scaling_3d_scale_changed")
	var arg_info: Dictionary = {}
	for info: Dictionary in s.get_signal_list():
		if info.name == "scaling_3d_scale_changed":
			arg_info = info.args[0]
	expect_equal(arg_info.get("type"), TYPE_FLOAT, "scaling_3d_scale_changed has a typed argument")
	var file_path := s.get_file_path()
	var had_file := FileAccess.file_exists(file_path)
	var previous_mode := s.apply_mode
	s.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	s.reset_value(&"scaling_3d_scale")
	var new_value: Variant = 0.5 if s.get_applied_value(&"scaling_3d_scale") != 0.5 else 0.75
	var received: Array = []
	var on_changed := func(v: Variant) -> void: received.append(v)
	s.connect(&"scaling_3d_scale_changed", on_changed)

	s.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	s.set_value(&"scaling_3d_scale", new_value)
	expect_equal(received.size(), 0, "scaling_3d_scale_changed does not fire for a pending change in manual mode")
	s.apply()
	expect_equal(received, [new_value], "scaling_3d_scale_changed fires once on apply() with the applied value")
	if not received.is_empty():
		expect_equal(typeof(received[0]), TYPE_FLOAT, "scaling_3d_scale_changed passes a typed value")
	s.apply()
	expect_equal(received.size(), 1, "scaling_3d_scale_changed does not fire when nothing changed")
	s.reset_value(&"scaling_3d_scale")
	expect_equal(received.size(), 1, "scaling_3d_scale_changed waits for apply() after a reset in manual mode")
	s.apply()
	expect_equal(received.size(), 2, "scaling_3d_scale_changed fires again when the default is applied")

	s.disconnect(&"scaling_3d_scale_changed", on_changed)
	s.apply_mode = previous_mode
	await get_tree().process_frame
	if not had_file:
		DirAccess.remove_absolute(file_path)
