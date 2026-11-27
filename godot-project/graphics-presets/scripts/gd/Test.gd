extends FeatureTest
## Full API test of Graphics Presets (GraphicsPresetSettingsServer, GraphicsPreset, GraphicsPresetSettings).
## Same tests as scripts/cs/Test.cs. apply_preset() sets all values of the typed domain resources of a GraphicsPreset
## in the four video domains and skips the values the renderer ignores. A single change gives "Custom".

const PRESET_FILE := "user://settings/graphics_preset.ini"
const DOMAINS: Array[String] = ["render_quality", "texture", "shadow", "render_effect"]
const BUILTIN: Array[String] = ["Low", "Medium", "High", "Ultra"]
const PROJECT_HIGH := "res://graphics-presets/resources/project_high_preset.tres"
const PROJECT_POTATO := "res://graphics-presets/resources/project_potato_preset.tres"

var _changed_names: Array[String] = []


func _run_tests() -> void:
	GraphicsPresetSettingsServer.preset_changed.connect(_on_preset_changed)
	_reset()
	_test_builtin()
	_test_apply()
	_test_unsupported()
	_test_custom()
	_test_storage()
	_test_project_presets()
	_test_runtime_presets()
	_test_resources()
	GraphicsPresetSettingsServer.preset_changed.disconnect(_on_preset_changed)
	_reset()
	DirAccess.remove_absolute(PRESET_FILE)
	for domain: String in DOMAINS:
		DirAccess.remove_absolute("user://settings/%s.ini" % domain)


func _on_preset_changed(preset_name: String) -> void:
	_changed_names.append(preset_name)


func _reset() -> void:
	for domain: String in DOMAINS:
		SettingsServerManager.get_server(domain).reset_to_defaults()
		SettingsServerManager.get_server(domain).confirm_changes()
	GraphicsPresetSettingsServer.reset_to_defaults()


func _confirm_all() -> void:
	for domain: String in DOMAINS:
		SettingsServerManager.get_server(domain).confirm_changes()


func _values_equal(a: Variant, b: Variant) -> bool:
	if typeof(a) in [TYPE_INT, TYPE_FLOAT] and typeof(b) in [TYPE_INT, TYPE_FLOAT]:
		return is_equal_approx(float(a), float(b))
	return a == b


## The stored values of the domain resource of a preset: setting name to value.
func _preset_values(preset: GraphicsPreset, domain: String) -> Dictionary:
	var values: Dictionary = {}
	var domain_settings: Resource = preset.get_domain_settings(domain)
	for property: Dictionary in domain_settings.get_property_list():
		var property_name: String = property.name
		var stored: bool = property.usage & PROPERTY_USAGE_STORAGE != 0
		if stored and property_name != "script" and not property_name.begins_with("resource_") and not property_name.begins_with("metadata/"):
			values[property_name] = domain_settings.get(property_name)
	return values


func _test_builtin() -> void:
	expect_equal(GraphicsPresetSettingsServer.get_custom_preset_name(), "Custom", "the custom name is Custom")
	expect_equal(GraphicsPresetSettingsServer.get_builtin_preset_names(), PackedStringArray(BUILTIN), "the built-in names")
	expect_equal(GraphicsPresetSettingsServer.get_preset_names(), PackedStringArray(BUILTIN), "the project uses the built-in presets")
	expect_equal(GraphicsPresetSettingsServer.get_presets().size(), 4, "get_presets returns four presets")
	var low: GraphicsPreset = GraphicsPresetSettingsServer.create_builtin_preset("Low")
	var ultra: GraphicsPreset = GraphicsPresetSettingsServer.create_builtin_preset("Ultra")
	expect_equal(low.preset_name, "Low", "create_builtin_preset sets the preset name")
	for domain: String in DOMAINS:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		expect(low.has_domain_settings(domain), "Low has settings for %s" % domain)
		expect_equal(low.get_domain_settings(domain).get_class(), server.get_settings().get_class(), "the %s resource of Low has the class of the domain" % domain)
		for setting: String in _preset_values(ultra, domain):
			expect(server.has_setting(setting), "%s/%s exists in the domain" % [domain, setting])
	expect(low.render_quality is RenderQualitySettings, "Low has a typed render_quality")
	expect(low.texture is TextureSettings, "Low has a typed texture")
	expect(low.shadow is ShadowSettings, "Low has a typed shadow")
	expect(low.render_effect is RenderEffectSettings, "Low has a typed render_effect")
	expect(low.shadow.directional_shadow_atlas_size < ultra.shadow.directional_shadow_atlas_size, "Ultra has larger shadow maps than Low")
	expect(low.texture.anisotropic_filtering_level < ultra.texture.anisotropic_filtering_level, "Ultra has more anisotropic filtering than Low")
	expect(is_equal_approx(low.render_quality.scaling_3d_scale, 0.75), "Low renders at 75%")
	var suggestion: String = GraphicsPresetSettingsServer.suggest_preset()
	expect(GraphicsPresetSettingsServer.has_preset(suggestion), "suggest_preset names a preset")


func _test_apply() -> void:
	_changed_names.clear()
	expect_equal(GraphicsPresetSettingsServer.apply_preset("Ultra"), OK, "apply_preset(Ultra) succeeds")
	_confirm_all()
	var preset: GraphicsPreset = GraphicsPresetSettingsServer.get_preset("Ultra")
	for domain: String in DOMAINS:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		expect(not server.is_dirty(), "%s applied the values at once" % domain)
		var values: Dictionary = _preset_values(preset, domain)
		for setting: String in values:
			if server.is_value_supported(setting):
				expect(_values_equal(server.get_value(setting), values[setting]), "Ultra: %s/%s = %s" % [domain, setting, values[setting]])
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "Ultra", "the current preset is Ultra")
	expect(not GraphicsPresetSettingsServer.is_custom(), "the preset is not custom")
	expect(GraphicsPresetSettingsServer.matches_preset("Ultra"), "the values match the preset")
	expect_equal(GraphicsPresetSettingsServer.detect_preset(), "Ultra", "detect_preset finds Ultra")
	expect(_changed_names.has("Ultra"), "preset_changed is emitted with Ultra")


func _test_unsupported() -> void:
	_reset()
	var low: GraphicsPreset = GraphicsPresetSettingsServer.get_preset("Low")
	var before: Dictionary = {}
	for domain: String in DOMAINS:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		for setting: String in _preset_values(low, domain):
			before[domain + "/" + setting] = server.get_value(setting)
	GraphicsPresetSettingsServer.apply_preset("Low")
	_confirm_all()
	for domain: String in DOMAINS:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		for setting: String in _preset_values(low, domain):
			if not server.is_value_supported(setting):
				expect(_values_equal(server.get_value(setting), before[domain + "/" + setting]), "unsupported %s/%s keeps its value" % [domain, setting])


func _test_custom() -> void:
	var shadow: SettingsServer = SettingsServerManager.get_server("shadow")
	GraphicsPresetSettingsServer.apply_preset("High")
	_confirm_all()
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "High", "High is applied")
	_changed_names.clear()
	shadow.set_value("directional_shadow_atlas_size", 2048)
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "Custom", "a single change gives Custom")
	expect(GraphicsPresetSettingsServer.is_custom(), "is_custom is true")
	expect(not GraphicsPresetSettingsServer.matches_preset("High"), "the values no longer match High")
	expect(_changed_names.has("Custom"), "preset_changed is emitted with Custom")
	shadow.set_value("directional_shadow_atlas_size", 4096)
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "High", "the preset value again gives the preset again")
	shadow.set_value("directional_shadow_atlas_size", 2048)


func _test_storage() -> void:
	for domain: String in DOMAINS:
		expect_equal(SettingsServerManager.get_server(domain).save(), OK, "save writes the %s file" % domain)
	expect_equal(GraphicsPresetSettingsServer.save(), OK, "save writes the graphics_preset file")
	expect(FileAccess.get_file_as_string(PRESET_FILE).contains("Custom"), "the file stores Custom")
	_reset()
	expect_equal(GraphicsPresetSettingsServer.get_settings().preset, "", "after a reset no preset is stored")
	for domain: String in DOMAINS:
		SettingsServerManager.get_server(domain).load()
	expect_equal(GraphicsPresetSettingsServer.load(), OK, "load reads the file")
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "Custom", "Custom is loaded")
	var size: int = SettingsServerManager.get_server("shadow").get_value("directional_shadow_atlas_size")
	expect_equal(size, 2048, "the changed value is loaded")


func _test_project_presets() -> void:
	ProjectSettings.set_setting("settings/graphics_preset/presets", PackedStringArray([PROJECT_HIGH, PROJECT_POTATO]))
	GraphicsPresetSettingsServer.reload_presets()
	expect_equal(GraphicsPresetSettingsServer.get_preset_names(), PackedStringArray(["Low", "Medium", "High", "Ultra", "Potato"]), "the project replaces High and adds Potato")
	var high: GraphicsPreset = GraphicsPresetSettingsServer.get_preset("High")
	expect_equal(high.shadow.directional_shadow_atlas_size, 2048, "the project High has smaller shadow maps")
	expect(high.render_effect == null and not high.has_domain_settings("render_effect"), "the project High leaves render_effect alone")
	var shadow: SettingsServer = SettingsServerManager.get_server("shadow")
	shadow.set_value("directional_shadow_atlas_size", 1024)
	expect_equal(GraphicsPresetSettingsServer.apply_preset("Potato"), OK, "apply_preset(Potato) succeeds")
	_confirm_all()
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "Potato", "Potato is current")
	var level: int = SettingsServerManager.get_server("texture").get_value("anisotropic_filtering_level")
	expect_equal(level, 0, "Potato sets the anisotropic filtering")
	var scale: float = SettingsServerManager.get_server("render_quality").get_value("scaling_3d_scale")
	expect_equal(scale, 0.5, "Potato sets the resolution scale")
	var kept: int = shadow.get_value("directional_shadow_atlas_size")
	expect_equal(kept, 1024, "Potato leaves shadow as it is")
	shadow.set_value("directional_shadow_atlas_size", 2048)
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "Potato", "a change in a domain Potato does not set keeps Potato")
	SettingsServerManager.get_server("texture").set_value("anisotropic_filtering_level", Viewport.ANISOTROPY_16X)
	expect_equal(GraphicsPresetSettingsServer.get_current_preset(), "Custom", "a change in a domain Potato sets gives Custom")
	ProjectSettings.set_setting("settings/graphics_preset/use_builtin_presets", false)
	GraphicsPresetSettingsServer.reload_presets()
	expect_equal(GraphicsPresetSettingsServer.get_preset_names(), PackedStringArray(["High", "Potato"]), "without the built-in presets")
	ProjectSettings.set_setting("settings/graphics_preset/use_builtin_presets", true)
	ProjectSettings.set_setting("settings/graphics_preset/presets", PackedStringArray())
	GraphicsPresetSettingsServer.reload_presets()
	expect_equal(GraphicsPresetSettingsServer.get_preset_names(), PackedStringArray(BUILTIN), "back to the built-in presets")


func _test_runtime_presets() -> void:
	var cinematic: GraphicsPreset = GraphicsPresetSettingsServer.create_builtin_preset("Ultra")
	cinematic.preset_name = "Cinematic"
	cinematic.render_effect.dof_blur_quality = RenderingServer.DOF_BLUR_QUALITY_HIGH
	var ultra: GraphicsPreset = GraphicsPresetSettingsServer.get_preset("Ultra")
	expect(cinematic.render_effect != ultra.render_effect, "create_builtin_preset returns new domain resources")
	GraphicsPresetSettingsServer.add_preset(cinematic)
	expect(GraphicsPresetSettingsServer.has_preset("Cinematic"), "add_preset adds a preset")
	expect_equal(GraphicsPresetSettingsServer.get_preset("Cinematic"), cinematic, "get_preset returns the added preset")
	GraphicsPresetSettingsServer.remove_preset("Cinematic")
	expect(not GraphicsPresetSettingsServer.has_preset("Cinematic"), "remove_preset removes it")
	expect_equal(GraphicsPresetSettingsServer.apply_preset("Custom"), ERR_INVALID_PARAMETER, "Custom can't be applied")
	expect_equal(GraphicsPresetSettingsServer.apply_preset("Missing"), ERR_DOES_NOT_EXIST, "a missing preset can't be applied")


func _test_resources() -> void:
	expect_equal(GraphicsPreset.get_domains(), PackedStringArray(DOMAINS), "a preset covers the four video domains")
	var preset := GraphicsPreset.new()
	expect_equal(preset.preset_name, "", "GraphicsPreset: the name is empty by default")
	expect(not preset.has_domain_settings("shadow"), "GraphicsPreset: a new preset has no domain settings")
	var shadow := ShadowSettings.new()
	preset.set_domain_settings("shadow", shadow)
	expect_equal(preset.shadow, shadow, "GraphicsPreset: set_domain_settings sets the shadow property")
	expect_equal(preset.get_domain_settings("shadow"), shadow, "GraphicsPreset: get_domain_settings returns it")
	var stored := GraphicsPresetSettings.new()
	expect_equal(stored.preset, "", "GraphicsPresetSettings: no preset by default")
	stored.preset = "High"
	expect_equal(stored.preset, "High", "GraphicsPresetSettings: preset is a plain property")
