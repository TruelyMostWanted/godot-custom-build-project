extends FeatureCheck
## GDScript checks of 22-feature-video-texture-settings. Check the same things as scripts/cs/Issue22FeatureVideoTextureSettingsCheck.cs.
##
## VideoTextureSettingsServer: the viewport values go to the root viewport at once, the global ones to the
## RenderingServer. The Compatibility renderer ignores the light projector and decal filter (stored, not pushed).
## VideoTextureStreamingSettingsServer exists only with the texture_streaming module: it is reached through
## Engine.get_singleton(), so that the script also parses in builds without it (Web).

const TEXTURE_FILE := "user://settings/video_texture.ini"
const STREAMING_FILE := "user://settings/video_texture_streaming.ini"
const NAMES := ["anisotropic_filtering_level", "texture_mipmap_bias", "light_projector_filter", "decal_filter", "canvas_item_default_texture_filter", "canvas_item_default_texture_repeat", "snap_2d_transforms_to_pixel", "snap_2d_vertices_to_pixel", "sdf_oversize", "sdf_scale", "canvas_shadow_atlas_size"]
const STREAMING_NAMES := ["enabled", "memory_budget_enabled", "memory_budget_mb", "min_lod", "max_lod"]


func _get_check_name() -> String:
	return "22-feature-video-texture-settings"


func _run_checks() -> void:
	expect(Engine.has_singleton("VideoTextureSettingsServer"), "VideoTextureSettingsServer is a registered singleton")
	expect(Engine.get_singleton("VideoTextureSettingsServer") is SettingsServer, "VideoTextureSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"video_texture"), Engine.get_singleton("VideoTextureSettingsServer"), "the domain is registered as \"video_texture\"")
	expect(VideoTextureSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(VideoTextureSettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the root viewport exists")
	expect_equal(VideoTextureSettingsServer.get_file_path(), TEXTURE_FILE, "file path")
	expect(VideoTextureSettingsServer.get_settings() is VideoTextureSettings, "the settings resource is a VideoTextureSettings")
	var names := VideoTextureSettingsServer.get_setting_names()
	expect_equal(names.size(), NAMES.size(), "11 values")
	for setting in NAMES:
		expect(names.has(setting), "setting \"%s\" exists" % setting)
		expect(VideoTextureSettingsServer.get_setting_flags(setting) & SettingsServer.SETTING_FLAG_RESTART_REQUIRED == 0, "\"%s\" takes effect at once" % setting)

	VideoTextureSettingsServer.reset_to_defaults()
	_check_defaults()
	_check_support()
	_check_apply()
	_check_viewports()
	await _check_group()
	_check_validation()
	_check_storage()
	await _check_streaming()

	# Leave no player file and no changed root values behind.
	VideoTextureSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(TEXTURE_FILE)


func _supported(setting: StringName) -> bool:
	return VideoTextureSettingsServer.is_value_supported(setting)


func _check_defaults() -> void:
	var root := get_tree().root
	expect_equal(VideoTextureSettingsServer.get_default_value(&"anisotropic_filtering_level"), ProjectSettings.get_setting("rendering/textures/default_filters/anisotropic_filtering_level"), "default anisotropy is the project setting")
	expect_equal(VideoTextureSettingsServer.get_default_value(&"canvas_item_default_texture_filter"), ProjectSettings.get_setting("rendering/textures/canvas_textures/default_texture_filter"), "default canvas filter is the project setting")
	expect_equal(VideoTextureSettingsServer.get_default_value(&"snap_2d_transforms_to_pixel"), ProjectSettings.get_setting("rendering/2d/snap/snap_2d_transforms_to_pixel"), "default pixel snap is the project setting")
	expect_equal(VideoTextureSettingsServer.get_default_value(&"canvas_shadow_atlas_size"), ProjectSettings.get_setting("rendering/2d/shadow_atlas/size"), "default 2D shadow atlas is the project setting")
	expect_equal(VideoTextureSettingsServer.get_default_value(&"decal_filter"), ProjectSettings.get_setting("rendering/textures/decals/filter"), "default decal filter is the project setting")
	expect_equal(root.anisotropic_filtering_level, VideoTextureSettingsServer.get_anisotropic_filtering_level(), "the root viewport has the default anisotropy")
	expect_equal(root.canvas_item_default_texture_filter, VideoTextureSettingsServer.get_canvas_item_default_texture_filter(), "the root viewport has the default canvas filter")


func _check_support() -> void:
	var method := RenderingServer.get_current_rendering_method()
	var unsupported := VideoTextureSettingsServer.get_unsupported_setting_names()
	for setting in NAMES:
		expect_equal(unsupported.has(setting), not _supported(setting), "get_unsupported_setting_names() matches is_value_supported(\"%s\")" % setting)
		expect_equal(_supported(setting), VideoTextureSettingsServer.is_renderer_supported(setting), "is_renderer_supported(\"%s\")" % setting)
	for setting in ["anisotropic_filtering_level", "texture_mipmap_bias", "canvas_item_default_texture_filter", "canvas_item_default_texture_repeat", "snap_2d_transforms_to_pixel", "snap_2d_vertices_to_pixel", "sdf_oversize", "sdf_scale", "canvas_shadow_atlas_size"]:
		expect(_supported(setting), "\"%s\" works in every renderer" % setting)
	expect_equal(_supported(&"decal_filter"), method != "gl_compatibility", "decal filter: not in Compatibility")
	expect_equal(_supported(&"light_projector_filter"), method != "gl_compatibility", "light projector filter: not in Compatibility")
	print("    renderer: %s, unsupported: %s" % [method, unsupported])


func _check_apply() -> void:
	var root := get_tree().root
	VideoTextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_16X)
	expect_equal(root.anisotropic_filtering_level, Viewport.ANISOTROPY_16X, "anisotropic_filtering_level reaches the root at once")
	VideoTextureSettingsServer.set_texture_mipmap_bias(-0.5)
	expect(is_equal_approx(root.texture_mipmap_bias, -0.5), "texture_mipmap_bias reaches the root at once")
	VideoTextureSettingsServer.set_canvas_item_default_texture_filter(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST)
	expect_equal(root.canvas_item_default_texture_filter, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST, "canvas_item_default_texture_filter reaches the root at once")
	VideoTextureSettingsServer.set_canvas_item_default_texture_repeat(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_MIRROR)
	expect_equal(root.canvas_item_default_texture_repeat, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_MIRROR, "canvas_item_default_texture_repeat reaches the root at once")
	VideoTextureSettingsServer.set_snap_2d_transforms_to_pixel(true)
	expect(root.snap_2d_transforms_to_pixel, "snap_2d_transforms_to_pixel reaches the root at once")
	VideoTextureSettingsServer.set_snap_2d_vertices_to_pixel(true)
	expect(root.snap_2d_vertices_to_pixel, "snap_2d_vertices_to_pixel reaches the root at once")
	VideoTextureSettingsServer.set_sdf_oversize(Viewport.SDF_OVERSIZE_200_PERCENT)
	expect_equal(root.sdf_oversize, Viewport.SDF_OVERSIZE_200_PERCENT, "sdf_oversize reaches the root at once")
	VideoTextureSettingsServer.set_sdf_scale(Viewport.SDF_SCALE_100_PERCENT)
	expect_equal(root.sdf_scale, Viewport.SDF_SCALE_100_PERCENT, "sdf_scale reaches the root at once")

	# Global values: no getter on RenderingServer, the domain keeps them.
	VideoTextureSettingsServer.set_canvas_shadow_atlas_size(4096)
	expect_equal(VideoTextureSettingsServer.get_canvas_shadow_atlas_size(), 4096, "canvas_shadow_atlas_size is kept")
	VideoTextureSettingsServer.set_decal_filter(RenderingServer.DECAL_FILTER_NEAREST)
	expect_equal(VideoTextureSettingsServer.get_decal_filter(), RenderingServer.DECAL_FILTER_NEAREST, "decal_filter is stored")
	VideoTextureSettingsServer.set_light_projector_filter(RenderingServer.LIGHT_PROJECTOR_FILTER_LINEAR)
	expect_equal(VideoTextureSettingsServer.get_light_projector_filter(), RenderingServer.LIGHT_PROJECTOR_FILTER_LINEAR, "light_projector_filter is stored")

	# Changed on the root directly, read back.
	root.sdf_scale = Viewport.SDF_SCALE_25_PERCENT
	VideoTextureSettingsServer.read_from_engine()
	expect_equal(VideoTextureSettingsServer.get_sdf_scale(), Viewport.SDF_SCALE_25_PERCENT, "read_from_engine() reads the root back")
	expect_equal(VideoTextureSettingsServer.get_canvas_shadow_atlas_size(), 4096, "read_from_engine() keeps the global values")

	VideoTextureSettingsServer.reset_to_defaults()
	expect_equal(root.anisotropic_filtering_level, ProjectSettings.get_setting("rendering/textures/default_filters/anisotropic_filtering_level"), "reset_to_defaults() gives back the anisotropy")
	expect_equal(root.snap_2d_transforms_to_pixel, ProjectSettings.get_setting("rendering/2d/snap/snap_2d_transforms_to_pixel"), "reset_to_defaults() gives back the pixel snap")


func _check_viewports() -> void:
	var added := SubViewport.new()
	VideoTextureSettingsServer.set_canvas_item_default_texture_filter(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST)
	VideoTextureSettingsServer.add_viewport(added)
	expect(VideoTextureSettingsServer.has_viewport(added), "has_viewport()")
	expect_equal(added.canvas_item_default_texture_filter, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST, "an added viewport gets the values at once")
	VideoTextureSettingsServer.set_snap_2d_transforms_to_pixel(true)
	expect(added.snap_2d_transforms_to_pixel, "an added viewport gets later changes")
	VideoTextureSettingsServer.remove_viewport(added)
	expect(not VideoTextureSettingsServer.has_viewport(added), "remove_viewport()")
	VideoTextureSettingsServer.set_snap_2d_transforms_to_pixel(false)
	expect(added.snap_2d_transforms_to_pixel, "a removed viewport keeps its values")
	added.free()
	VideoTextureSettingsServer.reset_to_defaults()


func _check_group() -> void:
	expect_equal(VideoTextureSettingsServer.get_viewport_group(), &"video_texture_settings", "get_viewport_group()")
	VideoTextureSettingsServer.set_canvas_item_default_texture_filter(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST)
	var grouped := SubViewport.new()
	grouped.add_to_group(VideoTextureSettingsServer.get_viewport_group())
	add_child(grouped)
	expect_equal(grouped.canvas_item_default_texture_filter, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST, "a viewport in the group gets the values when it enters the tree")
	VideoTextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_2X)
	expect_equal(grouped.anisotropic_filtering_level, Viewport.ANISOTROPY_2X, "a viewport in the group gets later changes")
	var other := SubViewport.new()
	add_child(other)
	expect_equal(other.canvas_item_default_texture_filter, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_LINEAR, "a viewport outside the group is untouched")
	grouped.queue_free()
	other.queue_free()
	await get_tree().process_frame
	VideoTextureSettingsServer.reset_to_defaults()


func _check_validation() -> void:
	VideoTextureSettingsServer.set_texture_mipmap_bias(5.0)
	expect(is_equal_approx(VideoTextureSettingsServer.get_texture_mipmap_bias(), 2.0), "the mipmap bias is clamped to 2.0")
	VideoTextureSettingsServer.set_canvas_shadow_atlas_size(16)
	expect_equal(VideoTextureSettingsServer.get_canvas_shadow_atlas_size(), 128, "the 2D shadow atlas is clamped to 128")
	VideoTextureSettingsServer.set_value(&"anisotropic_filtering_level", 9)
	expect_equal(VideoTextureSettingsServer.get_value(&"anisotropic_filtering_level"), VideoTextureSettingsServer.get_default_value(&"anisotropic_filtering_level"), "an invalid anisotropy falls back to the default")
	VideoTextureSettingsServer.reset_to_defaults()


func _check_storage() -> void:
	VideoTextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_8X)
	VideoTextureSettingsServer.set_snap_2d_vertices_to_pixel(true)
	VideoTextureSettingsServer.set_decal_filter(RenderingServer.DECAL_FILTER_LINEAR)
	expect_equal(VideoTextureSettingsServer.save(), OK, "save()")
	expect(FileAccess.file_exists(TEXTURE_FILE), "the file exists")
	var ini := FileAccess.get_file_as_string(TEXTURE_FILE)
	expect(ini.contains("anisotropic_filtering_level"), "the INI file has anisotropic_filtering_level")
	VideoTextureSettingsServer.reset_to_defaults()
	expect_equal(VideoTextureSettingsServer.load(), OK, "load()")
	expect_equal(VideoTextureSettingsServer.get_anisotropic_filtering_level(), Viewport.ANISOTROPY_8X, "the anisotropy is loaded")
	expect(VideoTextureSettingsServer.is_snap_2d_vertices_to_pixel_enabled(), "the vertex snap is loaded")
	expect_equal(VideoTextureSettingsServer.get_decal_filter(), RenderingServer.DECAL_FILTER_LINEAR, "the decal filter is loaded (also where the renderer ignores it)")
	VideoTextureSettingsServer.apply()
	expect_equal(get_tree().root.anisotropic_filtering_level, Viewport.ANISOTROPY_8X, "the loaded value reaches the root after apply()")


func _check_streaming() -> void:
	if not ClassDB.class_exists(&"TextureStreaming"):
		expect(not Engine.has_singleton("VideoTextureStreamingSettingsServer"), "without the texture_streaming module, the streaming domain is missing")
		expect_equal(SettingsServerManager.get_server(&"video_texture_streaming"), null, "no \"video_texture_streaming\" domain")
		return
	expect(Engine.has_singleton("VideoTextureStreamingSettingsServer"), "VideoTextureStreamingSettingsServer exists with the texture_streaming module")
	var streaming: SettingsServer = Engine.get_singleton("VideoTextureStreamingSettingsServer")
	expect_equal(SettingsServerManager.get_server(&"video_texture_streaming"), streaming, "the domain is registered as \"video_texture_streaming\"")
	expect(streaming.is_started(), "the streaming domain was started")
	expect_equal(streaming.get_apply_phase(), SettingsServer.APPLY_PHASE_PRE_DISPLAY, "applied before the renderers start")
	expect_equal(streaming.get_file_path(), STREAMING_FILE, "streaming file path")
	var names := streaming.get_setting_names()
	expect_equal(names.size(), STREAMING_NAMES.size(), "5 streaming values")
	for setting in STREAMING_NAMES:
		expect(names.has(setting), "streaming setting \"%s\" exists" % setting)
	expect(streaming.get_setting_flags(&"enabled") & SettingsServer.SETTING_FLAG_RESTART_REQUIRED != 0, "\"enabled\" needs a restart")
	expect(streaming.get_setting_flags(&"memory_budget_mb") & SettingsServer.SETTING_FLAG_RESTART_REQUIRED == 0, "\"memory_budget_mb\" takes effect at once")
	expect_equal(streaming.get_default_value(&"memory_budget_mb"), ProjectSettings.get_setting("rendering/textures/streaming/memory_budget_mb"), "default budget is the project setting")

	var supported: bool = streaming.call(&"is_renderer_supported")
	expect_equal(supported, RenderingServer.get_current_rendering_method() != "gl_compatibility", "streaming: Forward+ and Mobile only")
	var texture_streaming := Engine.get_singleton("TextureStreaming")
	var used_before: int = streaming.call(&"get_memory_budget_bytes_used")
	streaming.call(&"set_memory_budget_enabled", true)
	streaming.call(&"set_memory_budget_mb", 1)
	streaming.call(&"set_min_lod", 2)
	expect_equal(streaming.call(&"get_memory_budget_mb"), 1, "memory_budget_mb is stored")
	if supported:
		expect_equal(texture_streaming.memory_budget_mb_override, 1, "memory_budget_mb reaches TextureStreaming.memory_budget_mb_override at once")
		expect_equal(texture_streaming.min_lod_override, 2, "min_lod reaches TextureStreaming.min_lod_override at once")
		expect(ProjectSettings.get_setting("rendering/textures/streaming/memory_budget_enabled"), "memory_budget_enabled reaches the project setting")
		for i in 30:
			await get_tree().process_frame
		var used_after: int = streaming.call(&"get_memory_budget_bytes_used")
		if used_before > 0:
			expect(used_after <= used_before, "a lower budget does not raise the VRAM of streamed textures (%d -> %d bytes)" % [used_before, used_after])
		print("    streamed texture memory: %d -> %d bytes (streaming at startup: %s)" % [used_before, used_after, ProjectSettings.get_setting("rendering/textures/streaming/enabled")])

	streaming.call(&"set_enabled", not streaming.get_default_value(&"enabled"))
	expect(streaming.get_restart_required_names().has("enabled"), "changing \"enabled\" asks for a restart")
	streaming.reset_to_defaults()
	if supported:
		expect_equal(texture_streaming.memory_budget_mb_override, ProjectSettings.get_setting("rendering/textures/streaming/memory_budget_mb"), "reset_to_defaults() gives back the project budget")
	await get_tree().process_frame
	DirAccess.remove_absolute(STREAMING_FILE)
