extends FeatureTest
## Full API test of Render Effect Settings (RenderEffectSettingsServer, RenderEffectSettings).
## Same tests as scripts/cs/Test.cs. The effect quality goes to the RenderingServer, the environment overrides
## (allow_*, brightness, contrast, saturation) are applied at render time: Environment resources keep their authored values.

const EFFECT_FILE := "user://settings/render_effect.ini"

var _received: Dictionary = {}


func _run_tests() -> void:
	RenderEffectSettingsServer.reset_to_defaults()
	_test_defaults()
	_test_values()
	_test_support()
	await _test_overrides()
	await _test_ignore_group()
	_test_validation()
	_test_signals()
	_test_storage()
	_test_resource()
	RenderEffectSettingsServer.reset_to_defaults()
	DirAccess.remove_absolute(EFFECT_FILE)


func _test_defaults() -> void:
	var project_ssao: int = ProjectSettings.get_setting("rendering/environment/ssao/quality")
	var project_blur: int = ProjectSettings.get_setting("rendering/environment/ssil/blur_passes")
	var project_fog: int = ProjectSettings.get_setting("rendering/environment/volumetric_fog/volume_size")
	var project_bokeh: int = ProjectSettings.get_setting("rendering/camera/depth_of_field/depth_of_field_bokeh_shape")
	var project_rays: int = ProjectSettings.get_setting("rendering/global_illumination/sdfgi/probe_ray_count")
	expect_equal(RenderEffectSettingsServer.get_ssao_quality(), project_ssao, "the default SSAO quality is the project setting")
	expect_equal(RenderEffectSettingsServer.get_ssil_blur_passes(), project_blur, "the default SSIL blur passes are the project setting")
	expect_equal(RenderEffectSettingsServer.get_volumetric_fog_volume_size(), project_fog, "the default fog volume size is the project setting")
	expect_equal(RenderEffectSettingsServer.get_dof_bokeh_shape(), project_bokeh, "the default bokeh shape is the project setting")
	expect_equal(RenderEffectSettingsServer.get_sdfgi_ray_count(), project_rays, "the default SDFGI ray count is the project setting")
	expect(RenderEffectSettingsServer.is_ssao_allowed(), "SSAO is allowed by default")
	expect(RenderEffectSettingsServer.is_glow_allowed(), "glow is allowed by default")
	expect_equal(RenderEffectSettingsServer.get_brightness(), 1.0, "brightness is 1.0 by default")


func _test_values() -> void:
	RenderEffectSettingsServer.set_ssao_quality(RenderingServer.ENV_SSAO_QUALITY_HIGH)
	expect_equal(RenderEffectSettingsServer.get_ssao_quality(), RenderingServer.ENV_SSAO_QUALITY_HIGH, "ssao_quality round trip")
	RenderEffectSettingsServer.set_ssao_half_size(false)
	expect(not RenderEffectSettingsServer.is_ssao_half_size_enabled(), "ssao_half_size round trip")
	RenderEffectSettingsServer.set_ssao_adaptive_target(0.25)
	expect_equal(RenderEffectSettingsServer.get_ssao_adaptive_target(), 0.25, "ssao_adaptive_target round trip")
	RenderEffectSettingsServer.set_ssao_blur_passes(1)
	expect_equal(RenderEffectSettingsServer.get_ssao_blur_passes(), 1, "ssao_blur_passes round trip")
	RenderEffectSettingsServer.set_ssao_fadeout_from(10.0)
	expect_equal(RenderEffectSettingsServer.get_ssao_fadeout_from(), 10.0, "ssao_fadeout_from round trip")
	RenderEffectSettingsServer.set_ssao_fadeout_to(100.0)
	expect_equal(RenderEffectSettingsServer.get_ssao_fadeout_to(), 100.0, "ssao_fadeout_to round trip")
	RenderEffectSettingsServer.set_ssil_quality(RenderingServer.ENV_SSIL_QUALITY_LOW)
	expect_equal(RenderEffectSettingsServer.get_ssil_quality(), RenderingServer.ENV_SSIL_QUALITY_LOW, "ssil_quality round trip")
	RenderEffectSettingsServer.set_ssil_half_size(false)
	expect(not RenderEffectSettingsServer.is_ssil_half_size_enabled(), "ssil_half_size round trip")
	RenderEffectSettingsServer.set_ssil_adaptive_target(0.25)
	expect_equal(RenderEffectSettingsServer.get_ssil_adaptive_target(), 0.25, "ssil_adaptive_target round trip")
	RenderEffectSettingsServer.set_ssil_blur_passes(1)
	expect_equal(RenderEffectSettingsServer.get_ssil_blur_passes(), 1, "ssil_blur_passes round trip")
	RenderEffectSettingsServer.set_ssil_fadeout_from(10.0)
	expect_equal(RenderEffectSettingsServer.get_ssil_fadeout_from(), 10.0, "ssil_fadeout_from round trip")
	RenderEffectSettingsServer.set_ssil_fadeout_to(100.0)
	expect_equal(RenderEffectSettingsServer.get_ssil_fadeout_to(), 100.0, "ssil_fadeout_to round trip")
	RenderEffectSettingsServer.set_sdfgi_ray_count(RenderingServer.ENV_SDFGI_RAY_COUNT_32)
	expect_equal(RenderEffectSettingsServer.get_sdfgi_ray_count(), RenderingServer.ENV_SDFGI_RAY_COUNT_32, "sdfgi_ray_count round trip")
	RenderEffectSettingsServer.set_sdfgi_frames_to_converge(RenderingServer.ENV_SDFGI_CONVERGE_IN_15_FRAMES)
	expect_equal(RenderEffectSettingsServer.get_sdfgi_frames_to_converge(), RenderingServer.ENV_SDFGI_CONVERGE_IN_15_FRAMES, "sdfgi_frames_to_converge round trip")
	RenderEffectSettingsServer.set_sdfgi_frames_to_update_light(RenderingServer.ENV_SDFGI_UPDATE_LIGHT_IN_4_FRAMES)
	expect_equal(RenderEffectSettingsServer.get_sdfgi_frames_to_update_light(), RenderingServer.ENV_SDFGI_UPDATE_LIGHT_IN_4_FRAMES, "sdfgi_frames_to_update_light round trip")
	RenderEffectSettingsServer.set_voxel_gi_quality(RenderingServer.VOXEL_GI_QUALITY_HIGH)
	expect_equal(RenderEffectSettingsServer.get_voxel_gi_quality(), RenderingServer.VOXEL_GI_QUALITY_HIGH, "voxel_gi_quality round trip")
	RenderEffectSettingsServer.set_volumetric_fog_volume_size(96)
	expect_equal(RenderEffectSettingsServer.get_volumetric_fog_volume_size(), 96, "volumetric_fog_volume_size round trip")
	RenderEffectSettingsServer.set_volumetric_fog_volume_depth(32)
	expect_equal(RenderEffectSettingsServer.get_volumetric_fog_volume_depth(), 32, "volumetric_fog_volume_depth round trip")
	RenderEffectSettingsServer.set_volumetric_fog_use_filter(false)
	expect(not RenderEffectSettingsServer.is_volumetric_fog_using_filter(), "volumetric_fog_use_filter round trip")
	RenderEffectSettingsServer.set_sss_quality(RenderingServer.SUB_SURFACE_SCATTERING_QUALITY_HIGH)
	expect_equal(RenderEffectSettingsServer.get_sss_quality(), RenderingServer.SUB_SURFACE_SCATTERING_QUALITY_HIGH, "sss_quality round trip")
	RenderEffectSettingsServer.set_sss_scale(0.5)
	expect_equal(RenderEffectSettingsServer.get_sss_scale(), 0.5, "sss_scale round trip")
	RenderEffectSettingsServer.set_sss_depth_scale(0.25)
	expect_equal(RenderEffectSettingsServer.get_sss_depth_scale(), 0.25, "sss_depth_scale round trip")
	RenderEffectSettingsServer.set_dof_bokeh_shape(RenderingServer.DOF_BOKEH_CIRCLE)
	expect_equal(RenderEffectSettingsServer.get_dof_bokeh_shape(), RenderingServer.DOF_BOKEH_CIRCLE, "dof_bokeh_shape round trip")
	RenderEffectSettingsServer.set_dof_blur_quality(RenderingServer.DOF_BLUR_QUALITY_HIGH)
	expect_equal(RenderEffectSettingsServer.get_dof_blur_quality(), RenderingServer.DOF_BLUR_QUALITY_HIGH, "dof_blur_quality round trip")
	RenderEffectSettingsServer.set_dof_use_jitter(true)
	expect(RenderEffectSettingsServer.is_dof_using_jitter(), "dof_use_jitter round trip")
	RenderEffectSettingsServer.set_allow_glow(false)
	expect(not RenderEffectSettingsServer.is_glow_allowed(), "allow_glow round trip")
	RenderEffectSettingsServer.set_allow_ssao(false)
	expect(not RenderEffectSettingsServer.is_ssao_allowed(), "allow_ssao round trip")
	RenderEffectSettingsServer.set_allow_ssil(false)
	expect(not RenderEffectSettingsServer.is_ssil_allowed(), "allow_ssil round trip")
	RenderEffectSettingsServer.set_allow_ssr(false)
	expect(not RenderEffectSettingsServer.is_ssr_allowed(), "allow_ssr round trip")
	RenderEffectSettingsServer.set_allow_fog(false)
	expect(not RenderEffectSettingsServer.is_fog_allowed(), "allow_fog round trip")
	RenderEffectSettingsServer.set_allow_volumetric_fog(false)
	expect(not RenderEffectSettingsServer.is_volumetric_fog_allowed(), "allow_volumetric_fog round trip")
	RenderEffectSettingsServer.set_brightness(1.5)
	expect_equal(RenderEffectSettingsServer.get_brightness(), 1.5, "brightness round trip")
	RenderEffectSettingsServer.set_contrast(1.25)
	expect_equal(RenderEffectSettingsServer.get_contrast(), 1.25, "contrast round trip")
	RenderEffectSettingsServer.set_saturation(0.75)
	expect_equal(RenderEffectSettingsServer.get_saturation(), 0.75, "saturation round trip")
	RenderEffectSettingsServer.reset_to_defaults()


func _test_support() -> void:
	var unsupported: PackedStringArray = RenderEffectSettingsServer.get_unsupported_setting_names()
	for setting: StringName in [&"allow_glow", &"allow_ssao", &"brightness", &"voxel_gi_quality", &"dof_blur_quality", &"volumetric_fog_volume_size"]:
		var supported: bool = RenderEffectSettingsServer.is_value_supported(setting)
		expect_equal(unsupported.has(setting), not supported, "get_unsupported_setting_names matches is_value_supported(%s)" % setting)
		expect_equal(RenderEffectSettingsServer.is_renderer_supported(setting), supported, "is_renderer_supported matches is_value_supported(%s)" % setting)
	for setting: StringName in [&"allow_glow", &"allow_fog", &"brightness", &"contrast", &"saturation"]:
		expect(RenderEffectSettingsServer.is_value_supported(setting), "%s works in every renderer" % setting)
	var forward_plus: bool = RenderingServer.get_current_rendering_method() == "forward_plus"
	expect_equal(RenderEffectSettingsServer.is_value_supported(&"allow_ssao"), forward_plus, "SSAO is supported on Forward+ only")


func _test_overrides() -> void:
	var authored := Environment.new()
	authored.ssr_enabled = true
	authored.glow_enabled = true
	RenderEffectSettingsServer.set_allow_ssr(false)
	RenderEffectSettingsServer.set_allow_glow(false)
	expect(authored.ssr_enabled, "the environment keeps its authored SSR value")
	expect(authored.glow_enabled, "the environment keeps its authored glow value")
	var later := WorldEnvironment.new()
	later.environment = Environment.new()
	later.environment.ssr_enabled = true
	add_child(later)
	expect(later.environment.ssr_enabled, "a later scene keeps its authored value, the override is applied at render time")
	expect(not RenderEffectSettingsServer.is_environment_ignored(later.environment), "a later scene is not excluded")
	RenderEffectSettingsServer.set_brightness(1.5)
	expect(not authored.adjustment_enabled, "the global adjustment does not enable the adjustment of the environment")
	later.queue_free()
	await get_tree().process_frame
	RenderEffectSettingsServer.reset_to_defaults()


func _test_ignore_group() -> void:
	expect_equal(RenderEffectSettingsServer.get_ignore_group(), &"ignore_quality_settings", "get_ignore_group returns the group name")
	var menu := WorldEnvironment.new()
	menu.environment = Environment.new()
	menu.add_to_group(RenderEffectSettingsServer.get_ignore_group())
	add_child(menu)
	expect(RenderEffectSettingsServer.is_environment_ignored(menu.environment), "an environment in the group is excluded when it enters the tree")
	var free_environment := Environment.new()
	RenderEffectSettingsServer.set_environment_ignored(free_environment, true)
	expect(RenderEffectSettingsServer.is_environment_ignored(free_environment), "set_environment_ignored(true) excludes an environment")
	RenderEffectSettingsServer.set_environment_ignored(free_environment, false)
	expect(not RenderEffectSettingsServer.is_environment_ignored(free_environment), "set_environment_ignored(false) includes it again")
	menu.queue_free()
	await get_tree().process_frame


func _test_validation() -> void:
	RenderEffectSettingsServer.set_ssao_adaptive_target(2.0)
	expect_equal(RenderEffectSettingsServer.get_ssao_adaptive_target(), 1.0, "the adaptive target is clamped to 1.0")
	RenderEffectSettingsServer.set_volumetric_fog_volume_size(4)
	expect_equal(RenderEffectSettingsServer.get_volumetric_fog_volume_size(), 16, "the fog volume size is clamped to 16")
	RenderEffectSettingsServer.set_brightness(100.0)
	expect_equal(RenderEffectSettingsServer.get_brightness(), 8.0, "brightness is clamped to 8.0")
	RenderEffectSettingsServer.reset_to_defaults()


func _test_signals() -> void:
	RenderEffectSettingsServer.allow_glow_changed.connect(func(value: bool) -> void: _received["allow_glow"] = value)
	RenderEffectSettingsServer.allow_ssao_changed.connect(func(value: bool) -> void: _received["allow_ssao"] = value)
	RenderEffectSettingsServer.allow_ssil_changed.connect(func(value: bool) -> void: _received["allow_ssil"] = value)
	RenderEffectSettingsServer.allow_ssr_changed.connect(func(value: bool) -> void: _received["allow_ssr"] = value)
	RenderEffectSettingsServer.allow_fog_changed.connect(func(value: bool) -> void: _received["allow_fog"] = value)
	RenderEffectSettingsServer.allow_volumetric_fog_changed.connect(func(value: bool) -> void: _received["allow_volumetric_fog"] = value)
	RenderEffectSettingsServer.brightness_changed.connect(func(value: float) -> void: _received["brightness"] = value)
	RenderEffectSettingsServer.contrast_changed.connect(func(value: float) -> void: _received["contrast"] = value)
	RenderEffectSettingsServer.saturation_changed.connect(func(value: float) -> void: _received["saturation"] = value)
	RenderEffectSettingsServer.set_allow_glow(false)
	RenderEffectSettingsServer.set_allow_ssao(false)
	RenderEffectSettingsServer.set_allow_ssil(false)
	RenderEffectSettingsServer.set_allow_ssr(false)
	RenderEffectSettingsServer.set_allow_fog(false)
	RenderEffectSettingsServer.set_allow_volumetric_fog(false)
	RenderEffectSettingsServer.set_brightness(1.5)
	RenderEffectSettingsServer.set_contrast(1.25)
	RenderEffectSettingsServer.set_saturation(0.5)
	expect_equal(_received.get("allow_glow"), false, "allow_glow_changed passes the flag")
	expect_equal(_received.get("allow_ssao"), false, "allow_ssao_changed passes the flag")
	expect_equal(_received.get("allow_ssil"), false, "allow_ssil_changed passes the flag")
	expect_equal(_received.get("allow_ssr"), false, "allow_ssr_changed passes the flag")
	expect_equal(_received.get("allow_fog"), false, "allow_fog_changed passes the flag")
	expect_equal(_received.get("allow_volumetric_fog"), false, "allow_volumetric_fog_changed passes the flag")
	expect_equal(_received.get("brightness"), 1.5, "brightness_changed passes the brightness")
	expect_equal(_received.get("contrast"), 1.25, "contrast_changed passes the contrast")
	expect_equal(_received.get("saturation"), 0.5, "saturation_changed passes the saturation")
	RenderEffectSettingsServer.reset_to_defaults()


func _test_storage() -> void:
	RenderEffectSettingsServer.set_ssil_quality(RenderingServer.ENV_SSIL_QUALITY_HIGH)
	RenderEffectSettingsServer.set_allow_volumetric_fog(false)
	RenderEffectSettingsServer.set_contrast(1.25)
	expect_equal(RenderEffectSettingsServer.save(), OK, "save writes the file")
	RenderEffectSettingsServer.reset_to_defaults()
	expect_equal(RenderEffectSettingsServer.load(), OK, "load reads the file")
	expect_equal(RenderEffectSettingsServer.get_ssil_quality(), RenderingServer.ENV_SSIL_QUALITY_HIGH, "the SSIL quality is loaded, also where the renderer ignores it")
	expect(not RenderEffectSettingsServer.is_volumetric_fog_allowed(), "the fog switch is loaded")
	expect_equal(RenderEffectSettingsServer.get_contrast(), 1.25, "the contrast is loaded")


func _test_resource() -> void:
	var settings := RenderEffectSettings.new()
	expect_equal(settings.ssao_quality, RenderingServer.ENV_SSAO_QUALITY_MEDIUM, "RenderEffectSettings: default ssao_quality")
	expect(settings.ssao_half_size, "RenderEffectSettings: ssao_half_size by default")
	expect_equal(settings.ssil_blur_passes, 4, "RenderEffectSettings: default ssil_blur_passes")
	expect_equal(settings.volumetric_fog_volume_size, 64, "RenderEffectSettings: default volumetric_fog_volume_size")
	expect_equal(settings.dof_bokeh_shape, RenderingServer.DOF_BOKEH_HEXAGON, "RenderEffectSettings: default dof_bokeh_shape")
	expect(settings.allow_ssr, "RenderEffectSettings: allow_ssr by default")
	expect_equal(settings.brightness, 1.0, "RenderEffectSettings: default brightness")
	expect(is_equal_approx(settings.sss_depth_scale, 0.01), "RenderEffectSettings: default sss_depth_scale")
	settings.brightness = 1.5
	expect_equal(settings.brightness, 1.5, "RenderEffectSettings: brightness is a plain property")
