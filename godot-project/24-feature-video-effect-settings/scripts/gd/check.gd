extends FeatureCheck
## GDScript checks of 24-feature-video-effect-settings. Check the same things as scripts/cs/Issue24FeatureVideoEffectSettingsCheck.cs.
##
## VideoEffectSettingsServer: the effect quality goes to RenderingServer globals, the environment overrides
## (allow_*, brightness, contrast, saturation) are applied at render time: the Environment resources keep the
## authored values. The server is reached through Engine.get_singleton().

const FILE := "user://settings/video_effect.ini"
const NAMES := [
	"ssao_quality", "ssao_half_size", "ssao_adaptive_target", "ssao_blur_passes", "ssao_fadeout_from", "ssao_fadeout_to",
	"ssil_quality", "ssil_half_size", "ssil_adaptive_target", "ssil_blur_passes", "ssil_fadeout_from", "ssil_fadeout_to",
	"sdfgi_ray_count", "sdfgi_frames_to_converge", "sdfgi_frames_to_update_light", "voxel_gi_quality",
	"volumetric_fog_volume_size", "volumetric_fog_volume_depth", "volumetric_fog_use_filter",
	"sss_quality", "sss_scale", "sss_depth_scale", "dof_bokeh_shape", "dof_blur_quality", "dof_use_jitter",
	"allow_glow", "allow_ssao", "allow_ssil", "allow_ssr", "allow_fog", "allow_volumetric_fog",
	"brightness", "contrast", "saturation",
]
const FORWARD_PLUS_ONLY := ["ssao_quality", "ssil_quality", "sdfgi_ray_count", "volumetric_fog_volume_size", "sss_quality", "allow_ssao", "allow_ssil", "allow_ssr", "allow_volumetric_fog"]
const EVERY_RENDERER := ["allow_glow", "allow_fog", "brightness", "contrast", "saturation"]

var server # Untyped: the typed methods are called dynamically.


func _get_check_name() -> String:
	return "24-feature-video-effect-settings"


func _run_checks() -> void:
	await _check_typed_signals()
	expect(Engine.has_singleton("VideoEffectSettingsServer"), "VideoEffectSettingsServer is a registered singleton")
	server = Engine.get_singleton("VideoEffectSettingsServer")
	expect(server != null, "Engine.get_singleton() returns the server")
	if server == null:
		return
	expect(server.is_class("VideoEffectSettingsServer"), "the singleton is a VideoEffectSettingsServer")
	expect_equal(SettingsServerManager.get_server(&"video_effect"), server, "the domain is registered as \"video_effect\"")
	expect(server.is_started(), "the domain was started before the main scene")
	expect_equal(server.get_apply_phase(), SettingsServer.APPLY_PHASE_SERVERS, "applied after the renderer has read the project settings")
	expect_equal(server.get_file_path(), FILE, "file path")
	expect(server.get_settings().is_class("VideoEffectSettings"), "the settings resource is a VideoEffectSettings")
	var names: PackedStringArray = server.get_setting_names()
	expect_equal(names.size(), NAMES.size(), "34 values")
	for setting in NAMES:
		expect(names.has(setting), "setting \"%s\" exists" % setting)
		expect(server.get_setting_flags(setting) & SettingsServer.SETTING_FLAG_RESTART_REQUIRED == 0, "\"%s\" takes effect at once" % setting)

	server.reset_to_defaults()
	_check_defaults()
	_check_support()
	_check_quality()
	await _check_overrides()
	await _check_ignore_group()
	await _check_picture()
	_check_validation()
	_check_storage()

	# Leave no player file and no overrides behind.
	server.reset_to_defaults()
	DirAccess.remove_absolute(FILE)


func _check_defaults() -> void:
	expect_equal(int(server.get_default_value(&"ssao_quality")), int(ProjectSettings.get_setting("rendering/environment/ssao/quality")), "default SSAO quality is the project setting")
	expect_equal(int(server.get_default_value(&"ssil_blur_passes")), int(ProjectSettings.get_setting("rendering/environment/ssil/blur_passes")), "default SSIL blur passes are the project setting")
	expect_equal(int(server.get_default_value(&"volumetric_fog_volume_size")), int(ProjectSettings.get_setting("rendering/environment/volumetric_fog/volume_size")), "default fog volume size is the project setting")
	expect_equal(bool(server.get_default_value(&"volumetric_fog_use_filter")), bool(ProjectSettings.get_setting("rendering/environment/volumetric_fog/use_filter")), "default fog filter is the project setting")
	expect_equal(int(server.get_default_value(&"dof_bokeh_shape")), int(ProjectSettings.get_setting("rendering/camera/depth_of_field/depth_of_field_bokeh_shape")), "default bokeh shape is the project setting")
	expect_equal(int(server.get_default_value(&"sdfgi_ray_count")), int(ProjectSettings.get_setting("rendering/global_illumination/sdfgi/probe_ray_count")), "default SDFGI ray count is the project setting")
	for setting in ["allow_glow", "allow_ssao", "allow_ssil", "allow_ssr", "allow_fog", "allow_volumetric_fog"]:
		expect_equal(server.get_default_value(setting), true, "\"%s\" is allowed by default" % setting)
	for setting in ["brightness", "contrast", "saturation"]:
		expect(is_equal_approx(server.get_default_value(setting), 1.0), "\"%s\" is 1.0 by default" % setting)


func _check_support() -> void:
	var method := RenderingServer.get_current_rendering_method()
	var unsupported: PackedStringArray = server.get_unsupported_setting_names()
	for setting in NAMES:
		expect_equal(unsupported.has(setting), not server.is_value_supported(setting), "get_unsupported_setting_names() matches is_value_supported(\"%s\")" % setting)
		expect_equal(server.is_value_supported(setting), server.is_renderer_supported(setting), "is_renderer_supported(\"%s\")" % setting)
	for setting in EVERY_RENDERER:
		expect(server.is_value_supported(setting), "\"%s\" works in every renderer" % setting)
	for setting in FORWARD_PLUS_ONLY:
		expect_equal(server.is_value_supported(setting), method != "gl_compatibility" and method != "mobile", "\"%s\": Forward+ only" % setting)
	for setting in ["voxel_gi_quality", "dof_blur_quality", "dof_bokeh_shape"]:
		expect_equal(server.is_value_supported(setting), method != "gl_compatibility", "\"%s\": Forward+ and Mobile" % setting)
	print("    renderer: %s, unsupported: %s" % [method, unsupported])


func _check_quality() -> void:
	server.set_ssao_quality(RenderingServer.ENV_SSAO_QUALITY_HIGH)
	expect_equal(server.get_ssao_quality(), RenderingServer.ENV_SSAO_QUALITY_HIGH, "SSAO quality")
	server.set_ssao_half_size(false)
	expect(not server.is_ssao_half_size_enabled(), "SSAO half size")
	server.set_ssil_quality(RenderingServer.ENV_SSIL_QUALITY_LOW)
	expect_equal(server.get_ssil_quality(), RenderingServer.ENV_SSIL_QUALITY_LOW, "SSIL quality")
	server.set_sdfgi_ray_count(RenderingServer.ENV_SDFGI_RAY_COUNT_32)
	expect_equal(server.get_sdfgi_ray_count(), RenderingServer.ENV_SDFGI_RAY_COUNT_32, "SDFGI ray count")
	server.set_sdfgi_frames_to_converge(RenderingServer.ENV_SDFGI_CONVERGE_IN_15_FRAMES)
	expect_equal(server.get_sdfgi_frames_to_converge(), RenderingServer.ENV_SDFGI_CONVERGE_IN_15_FRAMES, "SDFGI frames to converge")
	server.set_voxel_gi_quality(RenderingServer.VOXEL_GI_QUALITY_HIGH)
	expect_equal(server.get_voxel_gi_quality(), RenderingServer.VOXEL_GI_QUALITY_HIGH, "VoxelGI quality")
	server.set_volumetric_fog_volume_size(96)
	expect_equal(server.get_volumetric_fog_volume_size(), 96, "volumetric fog volume size")
	server.set_volumetric_fog_use_filter(false)
	expect(not server.is_volumetric_fog_using_filter(), "volumetric fog filter")
	server.set_sss_quality(RenderingServer.SUB_SURFACE_SCATTERING_QUALITY_HIGH)
	expect_equal(server.get_sss_quality(), RenderingServer.SUB_SURFACE_SCATTERING_QUALITY_HIGH, "SSS quality")
	server.set_dof_bokeh_shape(RenderingServer.DOF_BOKEH_CIRCLE)
	expect_equal(server.get_dof_bokeh_shape(), RenderingServer.DOF_BOKEH_CIRCLE, "DoF bokeh shape")
	server.set_dof_blur_quality(RenderingServer.DOF_BLUR_QUALITY_HIGH)
	expect_equal(server.get_dof_blur_quality(), RenderingServer.DOF_BLUR_QUALITY_HIGH, "DoF blur quality")
	expect_equal(server.get_applied_value(&"ssao_quality"), RenderingServer.ENV_SSAO_QUALITY_HIGH, "applied at once (APPLY_MODE_IMMEDIATE)")
	server.reset_to_defaults()


func _check_overrides() -> void:
	# The level author's environment.
	var authored := Environment.new()
	authored.ssr_enabled = true
	authored.glow_enabled = true
	server.set_allow_ssr(false)
	server.set_allow_glow(false)
	expect(not server.is_ssr_allowed(), "SSR is turned off")
	expect(authored.ssr_enabled, "the environment keeps its authored SSR value")
	expect(authored.glow_enabled, "the environment keeps its authored glow value")

	# A scene loaded after the change.
	var later := WorldEnvironment.new()
	later.environment = Environment.new()
	later.environment.ssr_enabled = true
	add_child(later)
	expect(later.environment.ssr_enabled, "a later scene keeps its authored value (the override is applied at render time)")
	expect(not server.is_environment_ignored(later.environment), "a later scene is not excluded")

	server.set_allow_ssr(true)
	server.set_allow_glow(true)
	expect(server.is_ssr_allowed(), "SSR is allowed again")
	expect(authored.ssr_enabled and later.environment.ssr_enabled, "the authored values are still there")

	server.set_brightness(1.3)
	expect(is_equal_approx(server.get_brightness(), 1.3), "brightness")
	expect(not authored.adjustment_enabled, "the global adjustment does not enable the environment's adjustment")
	later.queue_free()
	await get_tree().process_frame
	server.reset_to_defaults()


func _check_ignore_group() -> void:
	expect_equal(server.get_ignore_group(), &"ignore_quality_settings", "get_ignore_group()")
	var menu := WorldEnvironment.new()
	menu.environment = Environment.new()
	menu.add_to_group(server.get_ignore_group())
	add_child(menu)
	expect(server.is_environment_ignored(menu.environment), "an environment in the group is excluded when it enters the tree")
	var free_environment := Environment.new()
	server.set_environment_ignored(free_environment, true)
	expect(server.is_environment_ignored(free_environment), "set_environment_ignored(true)")
	server.set_environment_ignored(free_environment, false)
	expect(not server.is_environment_ignored(free_environment), "set_environment_ignored(false)")
	menu.queue_free()
	await get_tree().process_frame


func _check_picture() -> void:
	if DisplayServer.get_name() == "headless":
		print("    headless: picture check skipped")
		return
	var viewport := SubViewport.new()
	viewport.size = Vector2i(16, 16)
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	var world_environment := WorldEnvironment.new()
	world_environment.environment = Environment.new()
	world_environment.environment.background_mode = Environment.BG_COLOR
	world_environment.environment.background_color = Color(0.2, 0.2, 0.2)
	viewport.add_child(world_environment)
	viewport.add_child(Camera3D.new())
	add_child(viewport)
	var before := await _sample(viewport)
	server.set_brightness(2.0)
	var brighter := await _sample(viewport)
	server.set_brightness(1.0)
	var restored := await _sample(viewport)
	print("    brightness picture: %s -> %s -> %s" % [before, brighter, restored])
	expect(brighter.r > before.r + 0.02, "brightness 2.0 makes the picture brighter at once")
	expect(absf(restored.r - before.r) < 0.02, "brightness 1.0 restores the picture")
	viewport.queue_free()
	await get_tree().process_frame


func _sample(viewport: SubViewport) -> Color:
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image().get_pixel(8, 8)


func _check_validation() -> void:
	server.set_ssao_adaptive_target(2.0)
	expect(is_equal_approx(server.get_ssao_adaptive_target(), 1.0), "adaptive target is clamped to 1.0")
	server.set_volumetric_fog_volume_size(4)
	expect_equal(server.get_volumetric_fog_volume_size(), 16, "fog volume size is clamped to 16")
	server.set_brightness(100.0)
	expect(is_equal_approx(server.get_brightness(), 8.0), "brightness is clamped to 8.0")
	server.set_value(&"ssao_quality", 42)
	expect_equal(int(server.get_ssao_quality()), int(server.get_default_value(&"ssao_quality")), "an invalid enum value becomes the default")
	server.reset_to_defaults()


func _check_storage() -> void:
	server.set_ssil_quality(RenderingServer.ENV_SSIL_QUALITY_HIGH)
	server.set_allow_volumetric_fog(false)
	server.set_contrast(1.2)
	expect_equal(server.save(), OK, "save()")
	expect(FileAccess.file_exists(FILE), "the file exists")
	var ini := FileAccess.get_file_as_string(FILE)
	expect(ini.contains("allow_volumetric_fog"), "the INI file has allow_volumetric_fog")
	server.reset_to_defaults()
	expect_equal(server.load(), OK, "load()")
	expect_equal(server.get_ssil_quality(), RenderingServer.ENV_SSIL_QUALITY_HIGH, "the SSIL quality is loaded (also where the renderer ignores it)")
	expect(not server.is_volumetric_fog_allowed(), "the fog switch is loaded")
	expect(is_equal_approx(server.get_contrast(), 1.2), "the contrast is loaded")


## Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.
func _check_typed_signals() -> void:
	if not Engine.has_singleton("VideoEffectSettingsServer"):
		return
	var s: SettingsServer = Engine.get_singleton("VideoEffectSettingsServer")
	expect(s.has_signal(&"brightness_changed"), "VideoEffectSettingsServer has the typed signal brightness_changed")
	var arg_info: Dictionary = {}
	for info: Dictionary in s.get_signal_list():
		if info.name == "brightness_changed":
			arg_info = info.args[0]
	expect_equal(arg_info.get("type"), TYPE_FLOAT, "brightness_changed has a typed argument")
	var file_path := s.get_file_path()
	var had_file := FileAccess.file_exists(file_path)
	var previous_mode := s.apply_mode
	s.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	s.reset_value(&"brightness")
	var new_value: Variant = 1.5 if s.get_applied_value(&"brightness") != 1.5 else 2.0
	var received: Array = []
	var on_changed := func(v: Variant) -> void: received.append(v)
	s.connect(&"brightness_changed", on_changed)

	s.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	s.set_value(&"brightness", new_value)
	expect_equal(received.size(), 0, "brightness_changed does not fire for a pending change in manual mode")
	s.apply()
	expect_equal(received, [new_value], "brightness_changed fires once on apply() with the applied value")
	if not received.is_empty():
		expect_equal(typeof(received[0]), TYPE_FLOAT, "brightness_changed passes a typed value")
	s.apply()
	expect_equal(received.size(), 1, "brightness_changed does not fire when nothing changed")
	s.reset_value(&"brightness")
	expect_equal(received.size(), 1, "brightness_changed waits for apply() after a reset in manual mode")
	s.apply()
	expect_equal(received.size(), 2, "brightness_changed fires again when the default is applied")

	s.disconnect(&"brightness_changed", on_changed)
	s.apply_mode = previous_mode
	await get_tree().process_frame
	if not had_file:
		DirAccess.remove_absolute(file_path)
