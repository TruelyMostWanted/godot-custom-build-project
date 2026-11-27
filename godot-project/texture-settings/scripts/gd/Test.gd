extends FeatureTest
## Full API test of Texture Settings (TextureSettingsServer, TextureSettings). Same tests as scripts/cs/Test.cs.
## The Compatibility renderer ignores the light projector and decal filter (stored, not pushed).

const TEXTURE_FILE := "user://settings/texture.ini"
const NEAREST := Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST

var _received: Dictionary = {}


func _run_tests() -> void:
	TextureSettingsServer.reset_to_defaults()
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
	TextureSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(TEXTURE_FILE)


func _test_defaults() -> void:
	var project_level: int = ProjectSettings.get_setting("rendering/textures/default_filters/anisotropic_filtering_level")
	var project_filter: int = ProjectSettings.get_setting("rendering/textures/canvas_textures/default_texture_filter")
	var project_snap: bool = ProjectSettings.get_setting("rendering/2d/snap/snap_2d_transforms_to_pixel")
	var project_atlas: int = ProjectSettings.get_setting("rendering/2d/shadow_atlas/size")
	var project_decal: int = ProjectSettings.get_setting("rendering/textures/decals/filter")
	expect_equal(TextureSettingsServer.get_anisotropic_filtering_level(), project_level, "the default anisotropy is the project setting")
	expect_equal(TextureSettingsServer.get_canvas_item_default_texture_filter(), project_filter, "the default canvas filter is the project setting")
	expect_equal(TextureSettingsServer.is_snap_2d_transforms_to_pixel_enabled(), project_snap, "the default pixel snap is the project setting")
	expect_equal(TextureSettingsServer.get_canvas_shadow_atlas_size(), project_atlas, "the default 2D shadow atlas is the project setting")
	expect_equal(TextureSettingsServer.get_decal_filter(), project_decal, "the default decal filter is the project setting")
	expect_equal(get_tree().root.anisotropic_filtering_level, project_level, "the root viewport has the default anisotropy")


func _test_values() -> void:
	TextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_16X)
	expect_equal(TextureSettingsServer.get_anisotropic_filtering_level(), Viewport.ANISOTROPY_16X, "anisotropic_filtering_level round trip")
	TextureSettingsServer.set_texture_mipmap_bias(-0.5)
	expect_equal(TextureSettingsServer.get_texture_mipmap_bias(), -0.5, "texture_mipmap_bias round trip")
	TextureSettingsServer.set_canvas_item_default_texture_filter(NEAREST)
	expect_equal(TextureSettingsServer.get_canvas_item_default_texture_filter(), NEAREST, "canvas_item_default_texture_filter round trip")
	TextureSettingsServer.set_canvas_item_default_texture_repeat(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_MIRROR)
	expect_equal(TextureSettingsServer.get_canvas_item_default_texture_repeat(), Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_MIRROR, "canvas_item_default_texture_repeat round trip")
	TextureSettingsServer.set_snap_2d_transforms_to_pixel(true)
	expect(TextureSettingsServer.is_snap_2d_transforms_to_pixel_enabled(), "snap_2d_transforms_to_pixel round trip")
	TextureSettingsServer.set_snap_2d_vertices_to_pixel(true)
	expect(TextureSettingsServer.is_snap_2d_vertices_to_pixel_enabled(), "snap_2d_vertices_to_pixel round trip")
	TextureSettingsServer.set_sdf_oversize(Viewport.SDF_OVERSIZE_200_PERCENT)
	expect_equal(TextureSettingsServer.get_sdf_oversize(), Viewport.SDF_OVERSIZE_200_PERCENT, "sdf_oversize round trip")
	TextureSettingsServer.set_sdf_scale(Viewport.SDF_SCALE_100_PERCENT)
	expect_equal(TextureSettingsServer.get_sdf_scale(), Viewport.SDF_SCALE_100_PERCENT, "sdf_scale round trip")
	TextureSettingsServer.set_canvas_shadow_atlas_size(4096)
	expect_equal(TextureSettingsServer.get_canvas_shadow_atlas_size(), 4096, "canvas_shadow_atlas_size round trip")
	TextureSettingsServer.set_decal_filter(RenderingServer.DECAL_FILTER_NEAREST)
	expect_equal(TextureSettingsServer.get_decal_filter(), RenderingServer.DECAL_FILTER_NEAREST, "decal_filter round trip")
	TextureSettingsServer.set_light_projector_filter(RenderingServer.LIGHT_PROJECTOR_FILTER_LINEAR)
	expect_equal(TextureSettingsServer.get_light_projector_filter(), RenderingServer.LIGHT_PROJECTOR_FILTER_LINEAR, "light_projector_filter round trip")
	TextureSettingsServer.reset_to_defaults()


func _test_support() -> void:
	var unsupported: PackedStringArray = TextureSettingsServer.get_unsupported_setting_names()
	for setting: StringName in [&"anisotropic_filtering_level", &"decal_filter", &"light_projector_filter", &"sdf_scale"]:
		var supported: bool = TextureSettingsServer.is_value_supported(setting)
		expect_equal(unsupported.has(setting), not supported, "get_unsupported_setting_names matches is_value_supported(%s)" % setting)
		expect_equal(TextureSettingsServer.is_renderer_supported(setting), supported, "is_renderer_supported matches is_value_supported(%s)" % setting)
	var compatibility: bool = RenderingServer.get_current_rendering_method() == "gl_compatibility"
	expect_equal(TextureSettingsServer.is_value_supported(&"decal_filter"), not compatibility, "the decal filter is not supported in Compatibility")
	expect_equal(TextureSettingsServer.is_value_supported(&"light_projector_filter"), not compatibility, "the light projector filter is not supported in Compatibility")
	expect(TextureSettingsServer.is_value_supported(&"anisotropic_filtering_level"), "anisotropic_filtering_level works in every renderer")


func _test_root_viewport() -> void:
	var root: Window = get_tree().root
	TextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_16X)
	expect_equal(root.anisotropic_filtering_level, Viewport.ANISOTROPY_16X, "anisotropic_filtering_level reaches the root at once")
	TextureSettingsServer.set_texture_mipmap_bias(-0.5)
	expect_equal(root.texture_mipmap_bias, -0.5, "texture_mipmap_bias reaches the root at once")
	TextureSettingsServer.set_canvas_item_default_texture_filter(NEAREST)
	expect_equal(root.canvas_item_default_texture_filter, NEAREST, "canvas_item_default_texture_filter reaches the root at once")
	TextureSettingsServer.set_canvas_item_default_texture_repeat(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_MIRROR)
	expect_equal(root.canvas_item_default_texture_repeat, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_MIRROR, "canvas_item_default_texture_repeat reaches the root at once")
	TextureSettingsServer.set_snap_2d_transforms_to_pixel(true)
	expect(root.snap_2d_transforms_to_pixel, "snap_2d_transforms_to_pixel reaches the root at once")
	TextureSettingsServer.set_snap_2d_vertices_to_pixel(true)
	expect(root.snap_2d_vertices_to_pixel, "snap_2d_vertices_to_pixel reaches the root at once")
	TextureSettingsServer.set_sdf_oversize(Viewport.SDF_OVERSIZE_200_PERCENT)
	expect_equal(root.sdf_oversize, Viewport.SDF_OVERSIZE_200_PERCENT, "sdf_oversize reaches the root at once")
	TextureSettingsServer.set_sdf_scale(Viewport.SDF_SCALE_100_PERCENT)
	expect_equal(root.sdf_scale, Viewport.SDF_SCALE_100_PERCENT, "sdf_scale reaches the root at once")
	root.sdf_scale = Viewport.SDF_SCALE_25_PERCENT
	TextureSettingsServer.read_from_engine()
	expect_equal(TextureSettingsServer.get_sdf_scale(), Viewport.SDF_SCALE_25_PERCENT, "read_from_engine reads the root back")
	TextureSettingsServer.reset_to_defaults()
	expect_equal(root.snap_2d_transforms_to_pixel, false, "reset_to_defaults gives the pixel snap back")


func _test_viewports() -> void:
	var added := SubViewport.new()
	TextureSettingsServer.set_canvas_item_default_texture_filter(NEAREST)
	TextureSettingsServer.add_viewport(added)
	expect(TextureSettingsServer.has_viewport(added), "add_viewport: has_viewport is true")
	expect_equal(added.canvas_item_default_texture_filter, NEAREST, "an added viewport gets the values at once")
	TextureSettingsServer.set_snap_2d_transforms_to_pixel(true)
	expect(added.snap_2d_transforms_to_pixel, "an added viewport gets later changes")
	TextureSettingsServer.remove_viewport(added)
	expect(not TextureSettingsServer.has_viewport(added), "remove_viewport: has_viewport is false")
	TextureSettingsServer.set_snap_2d_transforms_to_pixel(false)
	expect(added.snap_2d_transforms_to_pixel, "a removed viewport keeps its values")
	added.free()
	TextureSettingsServer.reset_to_defaults()


func _test_group() -> void:
	expect_equal(TextureSettingsServer.get_viewport_group(), &"texture_settings", "get_viewport_group returns the group name")
	TextureSettingsServer.set_canvas_item_default_texture_filter(NEAREST)
	var grouped := SubViewport.new()
	grouped.add_to_group(TextureSettingsServer.get_viewport_group())
	add_child(grouped)
	expect_equal(grouped.canvas_item_default_texture_filter, NEAREST, "a viewport in the group gets the values when it enters the tree")
	TextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_2X)
	expect_equal(grouped.anisotropic_filtering_level, Viewport.ANISOTROPY_2X, "a viewport in the group gets later changes")
	var other := SubViewport.new()
	add_child(other)
	expect_equal(other.canvas_item_default_texture_filter, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_LINEAR, "a viewport outside the group is untouched")
	grouped.queue_free()
	other.queue_free()
	await get_tree().process_frame
	TextureSettingsServer.reset_to_defaults()


func _test_validation() -> void:
	TextureSettingsServer.set_texture_mipmap_bias(5.0)
	expect_equal(TextureSettingsServer.get_texture_mipmap_bias(), 2.0, "the mipmap bias is clamped to 2.0")
	TextureSettingsServer.set_canvas_shadow_atlas_size(16)
	expect_equal(TextureSettingsServer.get_canvas_shadow_atlas_size(), 128, "the 2D shadow atlas is clamped to 128")
	TextureSettingsServer.reset_to_defaults()


func _test_signals() -> void:
	TextureSettingsServer.anisotropic_filtering_level_changed.connect(func(value: int) -> void: _received["level"] = value)
	TextureSettingsServer.texture_mipmap_bias_changed.connect(func(value: float) -> void: _received["bias"] = value)
	TextureSettingsServer.canvas_item_default_texture_filter_changed.connect(func(value: int) -> void: _received["filter"] = value)
	TextureSettingsServer.canvas_item_default_texture_repeat_changed.connect(func(value: int) -> void: _received["repeat"] = value)
	TextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_8X)
	TextureSettingsServer.set_texture_mipmap_bias(1.0)
	TextureSettingsServer.set_canvas_item_default_texture_filter(NEAREST)
	TextureSettingsServer.set_canvas_item_default_texture_repeat(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_ENABLED)
	expect_equal(_received.get("level"), Viewport.ANISOTROPY_8X, "anisotropic_filtering_level_changed passes the level")
	expect_equal(_received.get("bias"), 1.0, "texture_mipmap_bias_changed passes the bias")
	expect_equal(_received.get("filter"), NEAREST, "canvas_item_default_texture_filter_changed passes the filter")
	expect_equal(_received.get("repeat"), Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_REPEAT_ENABLED, "canvas_item_default_texture_repeat_changed passes the repeat mode")
	TextureSettingsServer.reset_to_defaults()


func _test_storage() -> void:
	TextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_8X)
	TextureSettingsServer.set_snap_2d_vertices_to_pixel(true)
	TextureSettingsServer.set_decal_filter(RenderingServer.DECAL_FILTER_LINEAR)
	expect_equal(TextureSettingsServer.save(), OK, "save writes the file")
	TextureSettingsServer.reset_to_defaults()
	expect_equal(TextureSettingsServer.load(), OK, "load reads the file")
	expect_equal(TextureSettingsServer.get_anisotropic_filtering_level(), Viewport.ANISOTROPY_8X, "the anisotropy is loaded")
	expect(TextureSettingsServer.is_snap_2d_vertices_to_pixel_enabled(), "the vertex snap is loaded")
	expect_equal(TextureSettingsServer.get_decal_filter(), RenderingServer.DECAL_FILTER_LINEAR, "the decal filter is loaded, also where the renderer ignores it")
	TextureSettingsServer.apply()
	expect_equal(get_tree().root.anisotropic_filtering_level, Viewport.ANISOTROPY_8X, "the loaded value reaches the root after apply")


func _test_resource() -> void:
	var settings := TextureSettings.new()
	expect_equal(settings.anisotropic_filtering_level, Viewport.ANISOTROPY_4X, "TextureSettings: default anisotropic_filtering_level")
	expect_equal(settings.texture_mipmap_bias, 0.0, "TextureSettings: default texture_mipmap_bias")
	expect_equal(settings.canvas_item_default_texture_filter, Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_LINEAR, "TextureSettings: default canvas_item_default_texture_filter")
	expect_equal(settings.canvas_shadow_atlas_size, 2048, "TextureSettings: default canvas_shadow_atlas_size")
	expect_equal(settings.decal_filter, RenderingServer.DECAL_FILTER_LINEAR_MIPMAPS, "TextureSettings: default decal_filter")
	expect(not settings.snap_2d_transforms_to_pixel, "TextureSettings: default snap_2d_transforms_to_pixel")
	settings.texture_mipmap_bias = 1.5
	expect_equal(settings.texture_mipmap_bias, 1.5, "TextureSettings: texture_mipmap_bias is a plain property")
