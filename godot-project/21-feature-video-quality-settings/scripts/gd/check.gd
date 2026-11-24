extends FeatureCheck
## GDScript checks of 21-feature-video-quality-settings. Check the same things as scripts/cs/Issue21FeatureVideoQualitySettingsCheck.cs.
##
## The values go to the root viewport at once. Values the current renderer ignores (Compatibility: FSR, FXAA/SMAA,
## TAA, debanding, VRS; Mobile: FSR, TAA) are stored, but not pushed; the checks follow is_value_supported().

const QUALITY_FILE := "user://settings/video_quality.ini"
const NAMES := ["scaling_3d_mode", "scaling_3d_scale", "fsr_sharpness", "mesh_lod_threshold", "msaa_2d", "msaa_3d", "screen_space_aa", "use_taa", "use_debanding", "vrs_mode", "use_occlusion_culling", "occlusion_rays_per_thread", "occlusion_culling_build_quality"]


func _get_check_name() -> String:
	return "21-feature-video-quality-settings"


func _run_checks() -> void:
	expect(Engine.has_singleton("VideoQualitySettingsServer"), "VideoQualitySettingsServer is a registered singleton")
	expect(Engine.get_singleton("VideoQualitySettingsServer") is SettingsServer, "VideoQualitySettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"video_quality"), Engine.get_singleton("VideoQualitySettingsServer"), "the domain is registered as \"video_quality\"")
	expect(VideoQualitySettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(VideoQualitySettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the root viewport exists")
	expect_equal(VideoQualitySettingsServer.get_file_path(), QUALITY_FILE, "file path")
	expect(VideoQualitySettingsServer.get_settings() is VideoQualitySettings, "the settings resource is a VideoQualitySettings")
	var names := VideoQualitySettingsServer.get_setting_names()
	expect_equal(names.size(), NAMES.size(), "13 values")
	for setting in NAMES:
		expect(names.has(setting), "setting \"%s\" exists" % setting)

	VideoQualitySettingsServer.reset_to_defaults()
	_check_defaults()
	_check_support()
	_check_apply()
	_check_viewports()
	await _check_group()
	_check_validation()
	_check_storage()

	# Leave no player file and no changed root values behind.
	VideoQualitySettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(QUALITY_FILE)


func _supported(setting: StringName) -> bool:
	return VideoQualitySettingsServer.is_value_supported(setting)


func _check_defaults() -> void:
	var root := get_tree().root
	expect_equal(VideoQualitySettingsServer.get_default_value(&"msaa_3d"), ProjectSettings.get_setting("rendering/anti_aliasing/quality/msaa_3d"), "default msaa_3d is the project setting")
	expect_equal(VideoQualitySettingsServer.get_default_value(&"scaling_3d_mode"), ProjectSettings.get_setting("rendering/scaling_3d/mode"), "default scaling mode is the project setting")
	expect(is_equal_approx(VideoQualitySettingsServer.get_default_value(&"scaling_3d_scale"), ProjectSettings.get_setting("rendering/scaling_3d/scale")), "default scale is the project setting")
	expect(is_equal_approx(VideoQualitySettingsServer.get_default_value(&"mesh_lod_threshold"), ProjectSettings.get_setting("rendering/mesh_lod/lod_change/threshold_pixels")), "default LOD threshold is the project setting")
	expect_equal(VideoQualitySettingsServer.get_default_value(&"use_occlusion_culling"), ProjectSettings.get_setting("rendering/occlusion_culling/use_occlusion_culling"), "default occlusion culling is the project setting")
	expect_equal(VideoQualitySettingsServer.get_default_value(&"occlusion_rays_per_thread"), ProjectSettings.get_setting("rendering/occlusion_culling/occlusion_rays_per_thread"), "default occlusion rays is the project setting")
	expect_equal(root.msaa_3d, VideoQualitySettingsServer.get_msaa_3d(), "the root viewport has the default msaa_3d")


func _check_support() -> void:
	var method := RenderingServer.get_current_rendering_method()
	var unsupported := VideoQualitySettingsServer.get_unsupported_setting_names()
	for setting in NAMES:
		expect_equal(unsupported.has(setting), not _supported(setting), "get_unsupported_setting_names() matches is_value_supported(\"%s\")" % setting)
		expect_equal(_supported(setting), VideoQualitySettingsServer.is_renderer_supported(setting), "is_renderer_supported(\"%s\")" % setting)
	for setting in ["scaling_3d_mode", "scaling_3d_scale", "mesh_lod_threshold", "msaa_2d", "msaa_3d"]:
		expect(_supported(setting), "\"%s\" works in every renderer" % setting)
	expect(VideoQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_BILINEAR), "bilinear scaling works everywhere")
	expect(VideoQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_NEAREST), "nearest scaling works everywhere")
	if method == "gl_compatibility":
		for setting in ["fsr_sharpness", "screen_space_aa", "use_taa", "use_debanding", "vrs_mode"]:
			expect(not _supported(setting), "compatibility: \"%s\" is unsupported" % setting)
		expect(not VideoQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR), "compatibility: no FSR")
	elif method == "mobile":
		expect(not _supported(&"use_taa"), "mobile: no TAA")
		expect(not VideoQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR2), "mobile: no FSR 2")
	elif method == "forward_plus":
		expect(VideoQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR2), "forward+: FSR 2")
		expect(_supported(&"use_taa"), "forward+: TAA")
	print("    renderer: %s, unsupported: %s" % [method, unsupported])


func _check_apply() -> void:
	var root := get_tree().root
	VideoQualitySettingsServer.set_scaling_3d_mode(Viewport.SCALING_3D_MODE_NEAREST)
	expect_equal(root.scaling_3d_mode, Viewport.SCALING_3D_MODE_NEAREST, "scaling_3d_mode reaches the root at once")
	VideoQualitySettingsServer.set_scaling_3d_scale(0.5)
	expect(is_equal_approx(root.scaling_3d_scale, 0.5), "scaling_3d_scale reaches the root at once")
	VideoQualitySettingsServer.set_mesh_lod_threshold(8.0)
	expect(is_equal_approx(root.mesh_lod_threshold, 8.0), "mesh_lod_threshold reaches the root at once")
	VideoQualitySettingsServer.set_msaa_2d(Viewport.MSAA_4X)
	expect_equal(root.msaa_2d, Viewport.MSAA_4X, "msaa_2d reaches the root at once")
	VideoQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	expect_equal(root.msaa_3d, Viewport.MSAA_2X, "msaa_3d reaches the root at once")

	# Pushed where the renderer supports them, stored either way.
	VideoQualitySettingsServer.set_fsr_sharpness(1.0)
	VideoQualitySettingsServer.set_screen_space_aa(Viewport.SCREEN_SPACE_AA_FXAA)
	VideoQualitySettingsServer.set_use_taa(true)
	VideoQualitySettingsServer.set_use_debanding(true)
	VideoQualitySettingsServer.set_use_occlusion_culling(true)
	expect_equal(VideoQualitySettingsServer.get_screen_space_aa(), Viewport.SCREEN_SPACE_AA_FXAA, "screen_space_aa is stored")
	expect(VideoQualitySettingsServer.is_using_taa(), "use_taa is stored")
	if _supported(&"fsr_sharpness"):
		expect(is_equal_approx(root.fsr_sharpness, 1.0), "fsr_sharpness reaches the root at once")
	expect_equal(root.screen_space_aa == Viewport.SCREEN_SPACE_AA_FXAA, _supported(&"screen_space_aa"), "screen_space_aa reaches the root only if supported")
	expect_equal(root.use_taa, _supported(&"use_taa"), "use_taa reaches the root only if supported")
	expect_equal(root.use_debanding, _supported(&"use_debanding"), "use_debanding reaches the root only if supported")
	expect_equal(root.use_occlusion_culling, _supported(&"use_occlusion_culling"), "use_occlusion_culling reaches the root only if supported")
	if _supported(&"vrs_mode"):
		VideoQualitySettingsServer.set_vrs_mode(Viewport.VRS_TEXTURE)
		expect_equal(root.vrs_mode, Viewport.VRS_TEXTURE, "vrs_mode reaches the root at once")

	# A scaling mode the renderer can't use is stored; the root scales bilinearly.
	VideoQualitySettingsServer.set_scaling_3d_mode(Viewport.SCALING_3D_MODE_FSR2)
	expect_equal(VideoQualitySettingsServer.get_scaling_3d_mode(), Viewport.SCALING_3D_MODE_FSR2, "FSR 2 is stored")
	var expected_mode := Viewport.SCALING_3D_MODE_FSR2 if VideoQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_FSR2) else Viewport.SCALING_3D_MODE_BILINEAR
	expect_equal(root.scaling_3d_mode, expected_mode, "the root gets FSR 2 or the bilinear fallback")

	# The global occlusion values: no getter on RenderingServer, the domain keeps them.
	VideoQualitySettingsServer.set_occlusion_rays_per_thread(256)
	expect_equal(VideoQualitySettingsServer.get_occlusion_rays_per_thread(), 256, "occlusion_rays_per_thread is kept")
	VideoQualitySettingsServer.set_occlusion_culling_build_quality(RenderingServer.VIEWPORT_OCCLUSION_BUILD_QUALITY_LOW)
	expect_equal(VideoQualitySettingsServer.get_occlusion_culling_build_quality(), RenderingServer.VIEWPORT_OCCLUSION_BUILD_QUALITY_LOW, "occlusion_culling_build_quality is kept")

	# Changed on the root directly, read back.
	root.msaa_2d = Viewport.MSAA_8X
	VideoQualitySettingsServer.read_from_engine()
	expect_equal(VideoQualitySettingsServer.get_msaa_2d(), Viewport.MSAA_8X, "read_from_engine() reads the root back")
	expect(VideoQualitySettingsServer.is_using_taa(), "read_from_engine() keeps skipped values as stored")

	VideoQualitySettingsServer.reset_to_defaults()
	expect_equal(root.msaa_2d, ProjectSettings.get_setting("rendering/anti_aliasing/quality/msaa_2d"), "reset_to_defaults() gives back msaa_2d")
	expect(is_equal_approx(root.scaling_3d_scale, ProjectSettings.get_setting("rendering/scaling_3d/scale")), "reset_to_defaults() gives back the scale")


func _check_viewports() -> void:
	var added := SubViewport.new()
	VideoQualitySettingsServer.set_msaa_3d(Viewport.MSAA_4X)
	VideoQualitySettingsServer.add_viewport(added)
	expect(VideoQualitySettingsServer.has_viewport(added), "has_viewport()")
	expect_equal(added.msaa_3d, Viewport.MSAA_4X, "an added viewport gets the values at once")
	VideoQualitySettingsServer.set_mesh_lod_threshold(16.0)
	expect(is_equal_approx(added.mesh_lod_threshold, 16.0), "an added viewport gets later changes")
	VideoQualitySettingsServer.remove_viewport(added)
	expect(not VideoQualitySettingsServer.has_viewport(added), "remove_viewport()")
	VideoQualitySettingsServer.set_mesh_lod_threshold(2.0)
	expect(is_equal_approx(added.mesh_lod_threshold, 16.0), "a removed viewport keeps its values")
	added.free()
	VideoQualitySettingsServer.reset_to_defaults()


func _check_group() -> void:
	expect_equal(VideoQualitySettingsServer.get_viewport_group(), &"video_quality_settings", "get_viewport_group()")
	VideoQualitySettingsServer.set_msaa_3d(Viewport.MSAA_4X)
	var grouped := SubViewport.new()
	grouped.add_to_group(VideoQualitySettingsServer.get_viewport_group())
	add_child(grouped)
	expect_equal(grouped.msaa_3d, Viewport.MSAA_4X, "a viewport in the group gets the values when it enters the tree")
	VideoQualitySettingsServer.set_msaa_3d(Viewport.MSAA_8X)
	expect_equal(grouped.msaa_3d, Viewport.MSAA_8X, "a viewport in the group gets later changes")
	var other := SubViewport.new()
	add_child(other)
	expect_equal(other.msaa_3d, Viewport.MSAA_DISABLED, "a viewport outside the group is untouched")
	grouped.queue_free()
	other.queue_free()
	await get_tree().process_frame
	VideoQualitySettingsServer.reset_to_defaults()


func _check_validation() -> void:
	VideoQualitySettingsServer.set_scaling_3d_scale(5.0)
	expect(is_equal_approx(VideoQualitySettingsServer.get_scaling_3d_scale(), 2.0), "the scale is clamped to 2.0")
	VideoQualitySettingsServer.set_occlusion_rays_per_thread(0)
	expect_equal(VideoQualitySettingsServer.get_occlusion_rays_per_thread(), 1, "the occlusion rays are clamped to 1")
	VideoQualitySettingsServer.set_value(&"msaa_3d", 9)
	expect_equal(VideoQualitySettingsServer.get_value(&"msaa_3d"), VideoQualitySettingsServer.get_default_value(&"msaa_3d"), "an invalid MSAA value falls back to the default")
	VideoQualitySettingsServer.reset_to_defaults()


func _check_storage() -> void:
	VideoQualitySettingsServer.set_scaling_3d_scale(0.75)
	VideoQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	VideoQualitySettingsServer.set_use_taa(true)
	expect_equal(VideoQualitySettingsServer.save(), OK, "save()")
	expect(FileAccess.file_exists(QUALITY_FILE), "the file exists")
	var ini := FileAccess.get_file_as_string(QUALITY_FILE)
	expect(ini.contains("msaa_3d"), "the INI file has msaa_3d")
	VideoQualitySettingsServer.reset_to_defaults()
	expect_equal(VideoQualitySettingsServer.load(), OK, "load()")
	expect(is_equal_approx(VideoQualitySettingsServer.get_scaling_3d_scale(), 0.75), "the scale is loaded")
	expect_equal(VideoQualitySettingsServer.get_msaa_3d(), Viewport.MSAA_2X, "msaa_3d is loaded")
	expect(VideoQualitySettingsServer.is_using_taa(), "use_taa is loaded (also where the renderer ignores it)")
	VideoQualitySettingsServer.apply()
	expect_equal(get_tree().root.msaa_3d, Viewport.MSAA_2X, "the loaded value reaches the root after apply()")
