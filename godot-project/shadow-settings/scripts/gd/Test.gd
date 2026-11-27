extends FeatureTest
## Full API test of Shadow Settings (ShadowSettingsServer, ShadowSettings). Same tests as scripts/cs/Test.cs.
## The positional values go to the root viewport, the directional ones to the RenderingServer (no getter there,
## so the domain keeps them).

const SHADOW_FILE := "user://settings/shadow.ini"
const SUBDIV_16 := Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_16

var _received: Dictionary = {}


func _run_tests() -> void:
	ShadowSettingsServer.reset_to_defaults()
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
	ShadowSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(SHADOW_FILE)


func _test_defaults() -> void:
	var project_positional: int = ProjectSettings.get_setting_with_override("rendering/lights_and_shadows/positional_shadow/atlas_size")
	var project_directional: int = ProjectSettings.get_setting_with_override("rendering/lights_and_shadows/directional_shadow/size")
	expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_size(), project_positional, "the default positional size is the project setting")
	expect_equal(ShadowSettingsServer.get_directional_shadow_atlas_size(), project_directional, "the default directional size is the project setting")
	expect_equal(get_tree().root.positional_shadow_atlas_size, project_positional, "the root viewport has the default positional size")


func _test_values() -> void:
	ShadowSettingsServer.set_directional_shadow_atlas_size(2048)
	expect_equal(ShadowSettingsServer.get_directional_shadow_atlas_size(), 2048, "directional_shadow_atlas_size round trip")
	ShadowSettingsServer.set_directional_shadow_16_bits(false)
	expect(not ShadowSettingsServer.is_directional_shadow_16_bits_enabled(), "directional_shadow_16_bits round trip")
	ShadowSettingsServer.set_directional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_HIGH)
	expect_equal(ShadowSettingsServer.get_directional_soft_shadow_filter_quality(), RenderingServer.SHADOW_QUALITY_SOFT_HIGH, "directional_soft_shadow_filter_quality round trip")
	ShadowSettingsServer.set_positional_shadow_atlas_size(1024)
	expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_size(), 1024, "positional_shadow_atlas_size round trip")
	ShadowSettingsServer.set_positional_shadow_atlas_16_bits(false)
	expect(not ShadowSettingsServer.is_positional_shadow_atlas_16_bits_enabled(), "positional_shadow_atlas_16_bits round trip")
	ShadowSettingsServer.set_positional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_HARD)
	expect_equal(ShadowSettingsServer.get_positional_soft_shadow_filter_quality(), RenderingServer.SHADOW_QUALITY_HARD, "positional_soft_shadow_filter_quality round trip")
	for quadrant in 4:
		ShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(quadrant, SUBDIV_16)
		expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_quadrant_subdiv(quadrant), SUBDIV_16, "quadrant %d subdiv round trip" % quadrant)
	ShadowSettingsServer.reset_to_defaults()


func _test_support() -> void:
	var unsupported: PackedStringArray = ShadowSettingsServer.get_unsupported_setting_names()
	expect(unsupported.is_empty(), "every renderer uses every value")
	expect(ShadowSettingsServer.is_value_supported(&"directional_shadow_atlas_size"), "directional_shadow_atlas_size is supported")
	expect(ShadowSettingsServer.is_renderer_supported(&"positional_shadow_atlas_size"), "is_renderer_supported is true for positional_shadow_atlas_size")


func _test_root_viewport() -> void:
	var root: Window = get_tree().root
	ShadowSettingsServer.set_positional_shadow_atlas_size(2048)
	expect_equal(root.positional_shadow_atlas_size, 2048, "positional_shadow_atlas_size reaches the root at once")
	ShadowSettingsServer.set_positional_shadow_atlas_16_bits(false)
	expect(not root.positional_shadow_atlas_16_bits, "positional_shadow_atlas_16_bits reaches the root at once")
	for quadrant in 4:
		ShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(quadrant, SUBDIV_16)
		expect_equal(root.get_positional_shadow_atlas_quadrant_subdiv(quadrant), SUBDIV_16, "quadrant %d reaches the root at once" % quadrant)
	root.positional_shadow_atlas_size = 1024
	ShadowSettingsServer.read_from_engine()
	expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_size(), 1024, "read_from_engine reads the root back")
	ShadowSettingsServer.reset_to_defaults()
	var project_bits: bool = ProjectSettings.get_setting("rendering/lights_and_shadows/positional_shadow/atlas_16_bits")
	expect_equal(root.positional_shadow_atlas_16_bits, project_bits, "reset_to_defaults gives the bit depth back")


func _test_viewports() -> void:
	var added := SubViewport.new()
	ShadowSettingsServer.set_positional_shadow_atlas_size(1024)
	ShadowSettingsServer.add_viewport(added)
	expect(ShadowSettingsServer.has_viewport(added), "add_viewport: has_viewport is true")
	expect_equal(added.positional_shadow_atlas_size, 1024, "an added viewport gets the values at once")
	ShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(2, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_1)
	expect_equal(added.positional_shadow_atlas_quad_2, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_1, "an added viewport gets later changes")
	ShadowSettingsServer.remove_viewport(added)
	expect(not ShadowSettingsServer.has_viewport(added), "remove_viewport: has_viewport is false")
	ShadowSettingsServer.set_positional_shadow_atlas_size(512)
	expect_equal(added.positional_shadow_atlas_size, 1024, "a removed viewport keeps its values")
	added.free()
	ShadowSettingsServer.reset_to_defaults()


func _test_group() -> void:
	expect_equal(ShadowSettingsServer.get_viewport_group(), &"shadow_settings", "get_viewport_group returns the group name")
	ShadowSettingsServer.set_positional_shadow_atlas_size(1024)
	var grouped := SubViewport.new()
	grouped.add_to_group(ShadowSettingsServer.get_viewport_group())
	add_child(grouped)
	expect_equal(grouped.positional_shadow_atlas_size, 1024, "a viewport in the group gets the values when it enters the tree")
	ShadowSettingsServer.set_positional_shadow_atlas_16_bits(false)
	expect(not grouped.positional_shadow_atlas_16_bits, "a viewport in the group gets later changes")
	var other := SubViewport.new()
	add_child(other)
	expect_equal(other.positional_shadow_atlas_size, 2048, "a viewport outside the group keeps the Viewport default")
	grouped.queue_free()
	other.queue_free()
	await get_tree().process_frame
	ShadowSettingsServer.reset_to_defaults()


func _test_validation() -> void:
	ShadowSettingsServer.set_directional_shadow_atlas_size(3000)
	expect_equal(ShadowSettingsServer.get_directional_shadow_atlas_size(), 4096, "sizes are rounded up to a power of two")
	ShadowSettingsServer.set_directional_shadow_atlas_size(16)
	expect_equal(ShadowSettingsServer.get_directional_shadow_atlas_size(), 256, "the directional size is clamped to 256")
	ShadowSettingsServer.set_positional_shadow_atlas_size(100000)
	expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_size(), 16384, "the positional size is clamped to 16384")
	ShadowSettingsServer.set_positional_shadow_atlas_size(0)
	expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_size(), 0, "0 disables positional shadows")
	expect_equal(get_tree().root.positional_shadow_atlas_size, 0, "0 reaches the root")
	ShadowSettingsServer.reset_to_defaults()


func _test_signals() -> void:
	ShadowSettingsServer.directional_shadow_atlas_size_changed.connect(func(value: int) -> void: _received["directional_size"] = value)
	ShadowSettingsServer.directional_soft_shadow_filter_quality_changed.connect(func(value: int) -> void: _received["directional_quality"] = value)
	ShadowSettingsServer.positional_shadow_atlas_size_changed.connect(func(value: int) -> void: _received["positional_size"] = value)
	ShadowSettingsServer.positional_soft_shadow_filter_quality_changed.connect(func(value: int) -> void: _received["positional_quality"] = value)
	ShadowSettingsServer.set_directional_shadow_atlas_size(8192)
	ShadowSettingsServer.set_directional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_HIGH)
	ShadowSettingsServer.set_positional_shadow_atlas_size(2048)
	ShadowSettingsServer.set_positional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_VERY_LOW)
	expect_equal(_received.get("directional_size"), 8192, "directional_shadow_atlas_size_changed passes the size")
	expect_equal(_received.get("directional_quality"), RenderingServer.SHADOW_QUALITY_SOFT_HIGH, "directional_soft_shadow_filter_quality_changed passes the quality")
	expect_equal(_received.get("positional_size"), 2048, "positional_shadow_atlas_size_changed passes the size")
	expect_equal(_received.get("positional_quality"), RenderingServer.SHADOW_QUALITY_SOFT_VERY_LOW, "positional_soft_shadow_filter_quality_changed passes the quality")
	ShadowSettingsServer.reset_to_defaults()


func _test_storage() -> void:
	ShadowSettingsServer.set_positional_shadow_atlas_size(2048)
	ShadowSettingsServer.set_directional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_ULTRA)
	ShadowSettingsServer.set_positional_shadow_atlas_quadrant_subdiv(1, SUBDIV_16)
	expect_equal(ShadowSettingsServer.save(), OK, "save writes the file")
	ShadowSettingsServer.reset_to_defaults()
	expect_equal(ShadowSettingsServer.load(), OK, "load reads the file")
	expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_size(), 2048, "the positional size is loaded")
	expect_equal(ShadowSettingsServer.get_directional_soft_shadow_filter_quality(), RenderingServer.SHADOW_QUALITY_SOFT_ULTRA, "the directional quality is loaded")
	expect_equal(ShadowSettingsServer.get_positional_shadow_atlas_quadrant_subdiv(1), SUBDIV_16, "the quadrant subdiv is loaded")
	ShadowSettingsServer.apply()
	expect_equal(get_tree().root.positional_shadow_atlas_size, 2048, "the loaded value reaches the root after apply")


func _test_resource() -> void:
	var settings := ShadowSettings.new()
	expect_equal(settings.directional_shadow_atlas_size, 4096, "ShadowSettings: default directional_shadow_atlas_size")
	expect(settings.directional_shadow_16_bits, "ShadowSettings: directional_shadow_16_bits by default")
	expect_equal(settings.directional_soft_shadow_filter_quality, RenderingServer.SHADOW_QUALITY_SOFT_LOW, "ShadowSettings: default directional quality")
	expect_equal(settings.positional_shadow_atlas_size, 4096, "ShadowSettings: default positional_shadow_atlas_size")
	expect_equal(settings.positional_shadow_atlas_quad_0, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_4, "ShadowSettings: default positional_shadow_atlas_quad_0")
	expect_equal(settings.positional_shadow_atlas_quad_3, Viewport.SHADOW_ATLAS_QUADRANT_SUBDIV_64, "ShadowSettings: default positional_shadow_atlas_quad_3")
	settings.positional_shadow_atlas_quad_1 = SUBDIV_16
	expect_equal(settings.positional_shadow_atlas_quad_1, SUBDIV_16, "ShadowSettings: positional_shadow_atlas_quad_1 is a plain property")
	expect_equal(settings.get_positional_shadow_atlas_quadrant_subdiv(1), SUBDIV_16, "ShadowSettings: get_positional_shadow_atlas_quadrant_subdiv reads quad_1")
	settings.set_positional_shadow_atlas_quadrant_subdiv(2, SUBDIV_16)
	expect_equal(settings.positional_shadow_atlas_quad_2, SUBDIV_16, "ShadowSettings: set_positional_shadow_atlas_quadrant_subdiv writes quad_2")
