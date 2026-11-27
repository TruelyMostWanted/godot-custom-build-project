extends FeatureTest
## Full API test of Render Quality Settings (RenderQualitySettingsServer, RenderQualitySettings).
## Same tests as scripts/cs/Test.cs. Values the current renderer ignores are stored but not pushed,
## so only the values every renderer supports are checked on the viewport.

const QUALITY_FILE := "user://settings/render_quality.ini"
const NAMES: Array[StringName] = [&"scaling_3d_mode", &"scaling_3d_scale", &"fsr_sharpness", &"mesh_lod_threshold", &"msaa_2d", &"msaa_3d", &"screen_space_aa", &"use_taa", &"use_debanding", &"vrs_mode", &"use_occlusion_culling", &"occlusion_rays_per_thread", &"occlusion_culling_build_quality"]

var _received: Dictionary = {}


func _run_tests() -> void:
	RenderQualitySettingsServer.reset_to_defaults()
	_test_defaults()
	_test_values()
	_test_support()
	_test_root_viewport()
	_test_viewports()
	await _test_group()
	_test_validation()
	_test_signals()
	_test_storage()
	_test_resource()
	RenderQualitySettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(QUALITY_FILE)


func _test_defaults() -> void:
	var project_msaa: int = ProjectSettings.get_setting("rendering/anti_aliasing/quality/msaa_3d")
	var project_mode: int = ProjectSettings.get_setting("rendering/scaling_3d/mode")
	var project_scale: float = ProjectSettings.get_setting("rendering/scaling_3d/scale")
	var project_lod: float = ProjectSettings.get_setting("rendering/mesh_lod/lod_change/threshold_pixels")
	expect_equal(RenderQualitySettingsServer.get_msaa_3d(), project_msaa, "the default msaa_3d is the project setting")
	expect_equal(RenderQualitySettingsServer.get_scaling_3d_mode(), project_mode, "the default scaling mode is the project setting")
	expect_equal(RenderQualitySettingsServer.get_scaling_3d_scale(), project_scale, "the default scale is the project setting")
	expect_equal(RenderQualitySettingsServer.get_mesh_lod_threshold(), project_lod, "the default LOD threshold is the project setting")
	expect_equal(get_tree().root.msaa_3d, RenderQualitySettingsServer.get_msaa_3d(), "the root viewport has the default msaa_3d")


func _test_values() -> void:
	RenderQualitySettingsServer.set_scaling_3d_mode(Viewport.SCALING_3D_MODE_NEAREST)
	expect_equal(RenderQualitySettingsServer.get_scaling_3d_mode(), Viewport.SCALING_3D_MODE_NEAREST, "scaling_3d_mode round trip")
	RenderQualitySettingsServer.set_scaling_3d_scale(0.5)
	expect_equal(RenderQualitySettingsServer.get_scaling_3d_scale(), 0.5, "scaling_3d_scale round trip")
	RenderQualitySettingsServer.set_fsr_sharpness(1.0)
	expect_equal(RenderQualitySettingsServer.get_fsr_sharpness(), 1.0, "fsr_sharpness round trip")
	RenderQualitySettingsServer.set_mesh_lod_threshold(8.0)
	expect_equal(RenderQualitySettingsServer.get_mesh_lod_threshold(), 8.0, "mesh_lod_threshold round trip")
	RenderQualitySettingsServer.set_msaa_2d(Viewport.MSAA_4X)
	expect_equal(RenderQualitySettingsServer.get_msaa_2d(), Viewport.MSAA_4X, "msaa_2d round trip")
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	expect_equal(RenderQualitySettingsServer.get_msaa_3d(), Viewport.MSAA_2X, "msaa_3d round trip")
	RenderQualitySettingsServer.set_screen_space_aa(Viewport.SCREEN_SPACE_AA_FXAA)
	expect_equal(RenderQualitySettingsServer.get_screen_space_aa(), Viewport.SCREEN_SPACE_AA_FXAA, "screen_space_aa round trip")
	RenderQualitySettingsServer.set_use_taa(true)
	expect(RenderQualitySettingsServer.is_using_taa(), "use_taa round trip")
	RenderQualitySettingsServer.set_use_debanding(true)
	expect(RenderQualitySettingsServer.is_using_debanding(), "use_debanding round trip")
	RenderQualitySettingsServer.set_vrs_mode(Viewport.VRS_TEXTURE)
	expect_equal(RenderQualitySettingsServer.get_vrs_mode(), Viewport.VRS_TEXTURE, "vrs_mode round trip")
	RenderQualitySettingsServer.set_use_occlusion_culling(true)
	expect(RenderQualitySettingsServer.is_using_occlusion_culling(), "use_occlusion_culling round trip")
	RenderQualitySettingsServer.set_occlusion_rays_per_thread(256)
	expect_equal(RenderQualitySettingsServer.get_occlusion_rays_per_thread(), 256, "occlusion_rays_per_thread round trip")
	RenderQualitySettingsServer.set_occlusion_culling_build_quality(RenderingServer.VIEWPORT_OCCLUSION_BUILD_QUALITY_LOW)
	expect_equal(RenderQualitySettingsServer.get_occlusion_culling_build_quality(), RenderingServer.VIEWPORT_OCCLUSION_BUILD_QUALITY_LOW, "occlusion_culling_build_quality round trip")
	RenderQualitySettingsServer.reset_to_defaults()


func _test_support() -> void:
	var unsupported: PackedStringArray = RenderQualitySettingsServer.get_unsupported_setting_names()
	for setting: StringName in NAMES:
		var supported: bool = RenderQualitySettingsServer.is_value_supported(setting)
		expect_equal(unsupported.has(setting), not supported, "get_unsupported_setting_names matches is_value_supported(%s)" % setting)
		expect_equal(RenderQualitySettingsServer.is_renderer_supported(setting), supported, "is_renderer_supported matches is_value_supported(%s)" % setting)
	expect(RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_BILINEAR), "bilinear scaling works everywhere")
	expect(RenderQualitySettingsServer.is_scaling_3d_mode_supported(Viewport.SCALING_3D_MODE_NEAREST), "nearest scaling works everywhere")
	expect(RenderQualitySettingsServer.is_value_supported(&"msaa_3d"), "msaa_3d works in every renderer")
	expect(RenderQualitySettingsServer.is_value_supported(&"scaling_3d_scale"), "scaling_3d_scale works in every renderer")


func _test_root_viewport() -> void:
	var root: Window = get_tree().root
	RenderQualitySettingsServer.set_scaling_3d_mode(Viewport.SCALING_3D_MODE_NEAREST)
	expect_equal(root.scaling_3d_mode, Viewport.SCALING_3D_MODE_NEAREST, "scaling_3d_mode reaches the root at once")
	RenderQualitySettingsServer.set_scaling_3d_scale(0.5)
	expect_equal(root.scaling_3d_scale, 0.5, "scaling_3d_scale reaches the root at once")
	RenderQualitySettingsServer.set_mesh_lod_threshold(8.0)
	expect_equal(root.mesh_lod_threshold, 8.0, "mesh_lod_threshold reaches the root at once")
	RenderQualitySettingsServer.set_msaa_2d(Viewport.MSAA_4X)
	expect_equal(root.msaa_2d, Viewport.MSAA_4X, "msaa_2d reaches the root at once")
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	expect_equal(root.msaa_3d, Viewport.MSAA_2X, "msaa_3d reaches the root at once")
	root.msaa_2d = Viewport.MSAA_8X
	RenderQualitySettingsServer.read_from_engine()
	expect_equal(RenderQualitySettingsServer.get_msaa_2d(), Viewport.MSAA_8X, "read_from_engine reads the root back")
	RenderQualitySettingsServer.reset_to_defaults()
	expect_equal(root.msaa_2d, Viewport.MSAA_DISABLED, "reset_to_defaults gives msaa_2d back")


func _test_viewports() -> void:
	var added := SubViewport.new()
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_4X)
	RenderQualitySettingsServer.add_viewport(added)
	expect(RenderQualitySettingsServer.has_viewport(added), "add_viewport: has_viewport is true")
	expect_equal(added.msaa_3d, Viewport.MSAA_4X, "an added viewport gets the values at once")
	RenderQualitySettingsServer.set_mesh_lod_threshold(16.0)
	expect_equal(added.mesh_lod_threshold, 16.0, "an added viewport gets later changes")
	RenderQualitySettingsServer.remove_viewport(added)
	expect(not RenderQualitySettingsServer.has_viewport(added), "remove_viewport: has_viewport is false")
	RenderQualitySettingsServer.set_mesh_lod_threshold(2.0)
	expect_equal(added.mesh_lod_threshold, 16.0, "a removed viewport keeps its values")
	added.free()
	RenderQualitySettingsServer.reset_to_defaults()


func _test_group() -> void:
	expect_equal(RenderQualitySettingsServer.get_viewport_group(), &"render_quality_settings", "get_viewport_group returns the group name")
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


func _test_validation() -> void:
	RenderQualitySettingsServer.set_scaling_3d_scale(5.0)
	expect_equal(RenderQualitySettingsServer.get_scaling_3d_scale(), 2.0, "the scale is clamped to 2.0")
	RenderQualitySettingsServer.set_occlusion_rays_per_thread(0)
	expect_equal(RenderQualitySettingsServer.get_occlusion_rays_per_thread(), 1, "the occlusion rays are clamped to 1")
	RenderQualitySettingsServer.set_value(&"msaa_3d", 9)
	expect_equal(RenderQualitySettingsServer.get_msaa_3d(), Viewport.MSAA_DISABLED, "an invalid MSAA value is rejected and the old value is kept")
	RenderQualitySettingsServer.reset_to_defaults()


func _test_signals() -> void:
	RenderQualitySettingsServer.scaling_3d_mode_changed.connect(func(value: int) -> void: _received["scaling_3d_mode"] = value)
	RenderQualitySettingsServer.scaling_3d_scale_changed.connect(func(value: float) -> void: _received["scaling_3d_scale"] = value)
	RenderQualitySettingsServer.fsr_sharpness_changed.connect(func(value: float) -> void: _received["fsr_sharpness"] = value)
	RenderQualitySettingsServer.mesh_lod_threshold_changed.connect(func(value: float) -> void: _received["mesh_lod_threshold"] = value)
	RenderQualitySettingsServer.msaa_2d_changed.connect(func(value: int) -> void: _received["msaa_2d"] = value)
	RenderQualitySettingsServer.msaa_3d_changed.connect(func(value: int) -> void: _received["msaa_3d"] = value)
	RenderQualitySettingsServer.screen_space_aa_changed.connect(func(value: int) -> void: _received["screen_space_aa"] = value)
	RenderQualitySettingsServer.use_taa_changed.connect(func(value: bool) -> void: _received["use_taa"] = value)
	RenderQualitySettingsServer.use_debanding_changed.connect(func(value: bool) -> void: _received["use_debanding"] = value)
	RenderQualitySettingsServer.set_scaling_3d_mode(Viewport.SCALING_3D_MODE_NEAREST)
	RenderQualitySettingsServer.set_scaling_3d_scale(0.5)
	RenderQualitySettingsServer.set_mesh_lod_threshold(8.0)
	RenderQualitySettingsServer.set_msaa_2d(Viewport.MSAA_4X)
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	expect_equal(_received.get("scaling_3d_mode"), Viewport.SCALING_3D_MODE_NEAREST, "scaling_3d_mode_changed passes the mode")
	expect_equal(_received.get("scaling_3d_scale"), 0.5, "scaling_3d_scale_changed passes the scale")
	expect_equal(_received.get("mesh_lod_threshold"), 8.0, "mesh_lod_threshold_changed passes the threshold")
	expect_equal(_received.get("msaa_2d"), Viewport.MSAA_4X, "msaa_2d_changed passes the mode")
	expect_equal(_received.get("msaa_3d"), Viewport.MSAA_2X, "msaa_3d_changed passes the mode")
	# The next four are only pushed, and so only signalled, where the renderer supports them.
	RenderQualitySettingsServer.set_fsr_sharpness(1.0)
	RenderQualitySettingsServer.set_screen_space_aa(Viewport.SCREEN_SPACE_AA_FXAA)
	RenderQualitySettingsServer.set_use_taa(true)
	RenderQualitySettingsServer.set_use_debanding(true)
	if RenderQualitySettingsServer.is_value_supported(&"fsr_sharpness"):
		expect_equal(_received.get("fsr_sharpness"), 1.0, "fsr_sharpness_changed passes the sharpness")
	if RenderQualitySettingsServer.is_value_supported(&"screen_space_aa"):
		expect_equal(_received.get("screen_space_aa"), Viewport.SCREEN_SPACE_AA_FXAA, "screen_space_aa_changed passes the mode")
	if RenderQualitySettingsServer.is_value_supported(&"use_taa"):
		expect_equal(_received.get("use_taa"), true, "use_taa_changed passes the flag")
	if RenderQualitySettingsServer.is_value_supported(&"use_debanding"):
		expect_equal(_received.get("use_debanding"), true, "use_debanding_changed passes the flag")
	RenderQualitySettingsServer.reset_to_defaults()


func _test_storage() -> void:
	RenderQualitySettingsServer.set_scaling_3d_scale(0.75)
	RenderQualitySettingsServer.set_msaa_3d(Viewport.MSAA_2X)
	RenderQualitySettingsServer.set_use_taa(true)
	expect_equal(RenderQualitySettingsServer.save(), OK, "save writes the file")
	RenderQualitySettingsServer.reset_to_defaults()
	expect_equal(RenderQualitySettingsServer.load(), OK, "load reads the file")
	expect_equal(RenderQualitySettingsServer.get_scaling_3d_scale(), 0.75, "the scale is loaded")
	expect_equal(RenderQualitySettingsServer.get_msaa_3d(), Viewport.MSAA_2X, "msaa_3d is loaded")
	expect(RenderQualitySettingsServer.is_using_taa(), "use_taa is loaded, also where the renderer ignores it")
	RenderQualitySettingsServer.apply()
	expect_equal(get_tree().root.msaa_3d, Viewport.MSAA_2X, "the loaded value reaches the root after apply")


func _test_resource() -> void:
	var settings := RenderQualitySettings.new()
	expect_equal(settings.scaling_3d_mode, Viewport.SCALING_3D_MODE_BILINEAR, "RenderQualitySettings: default scaling_3d_mode")
	expect_equal(settings.scaling_3d_scale, 1.0, "RenderQualitySettings: default scaling_3d_scale")
	expect(is_equal_approx(settings.fsr_sharpness, 0.2), "RenderQualitySettings: default fsr_sharpness")
	expect_equal(settings.mesh_lod_threshold, 1.0, "RenderQualitySettings: default mesh_lod_threshold")
	expect_equal(settings.msaa_3d, Viewport.MSAA_DISABLED, "RenderQualitySettings: default msaa_3d")
	expect_equal(settings.occlusion_rays_per_thread, 512, "RenderQualitySettings: default occlusion_rays_per_thread")
	expect_equal(settings.occlusion_culling_build_quality, RenderingServer.VIEWPORT_OCCLUSION_BUILD_QUALITY_HIGH, "RenderQualitySettings: default occlusion_culling_build_quality")
	settings.msaa_3d = Viewport.MSAA_8X
	expect_equal(settings.msaa_3d, Viewport.MSAA_8X, "RenderQualitySettings: msaa_3d is a plain property")
