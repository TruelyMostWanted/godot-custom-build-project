extends FeatureCheck
## GDScript checks of 25-feature-video-presets. Check the same things as scripts/cs/Issue25FeatureVideoPresetsCheck.cs.
##
## VideoPresetSettingsServer: apply_preset() sets all values of the typed domain resources of a VideoPreset
## (RenderQualitySettings, ...) in the video domains and skips null ones, a single change gives "Custom", the name is
## stored in the domain "video_preset", projects replace and add presets.

const FILE := "user://settings/video_preset.ini"
const DOMAINS := ["render_quality", "texture", "shadow", "render_effect"]
const BUILTIN := ["Low", "Medium", "High", "Ultra"]
const PROJECT_HIGH := "res://25-feature-video-presets/resources/project_high_preset.tres"
const PROJECT_POTATO := "res://25-feature-video-presets/resources/project_potato_preset.tres"

var changed_names: Array[String] = []


func _get_check_name() -> String:
	return "25-feature-video-presets"


func _run_checks() -> void:
	expect(Engine.has_singleton("VideoPresetSettingsServer"), "VideoPresetSettingsServer is a registered singleton")
	expect_equal(SettingsServerManager.get_server(&"video_preset"), Engine.get_singleton("VideoPresetSettingsServer"), "the domain is registered as \"video_preset\"")
	expect(VideoPresetSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(VideoPresetSettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "apply phase")
	expect_equal(VideoPresetSettingsServer.get_file_path(), FILE, "file path")
	expect(VideoPresetSettingsServer.get_settings() is VideoPresetSettings, "the settings resource is a VideoPresetSettings")
	expect_equal(VideoPreset.get_domains(), PackedStringArray(DOMAINS), "a preset covers the four video domains")
	for domain in DOMAINS:
		expect(SettingsServerManager.has_server(domain), "domain \"%s\" is registered" % domain)

	VideoPresetSettingsServer.preset_changed.connect(_on_preset_changed)
	_reset()
	_check_builtin()
	_check_apply("Low")
	_check_apply("Ultra")
	_check_unsupported()
	_check_custom()
	_check_storage()
	_check_project_presets()
	_check_runtime_presets()
	VideoPresetSettingsServer.preset_changed.disconnect(_on_preset_changed)

	# Leave no player files behind.
	_reset()
	DirAccess.remove_absolute(FILE)
	for domain in DOMAINS:
		DirAccess.remove_absolute("user://settings/%s.ini" % domain)


func _on_preset_changed(preset_name: String) -> void:
	changed_names.append(preset_name)


func _reset() -> void:
	for domain in DOMAINS:
		SettingsServerManager.get_server(domain).reset_to_defaults()
		SettingsServerManager.get_server(domain).confirm_changes()
	VideoPresetSettingsServer.reset_to_defaults()


func _confirm_all() -> void:
	for domain in DOMAINS:
		SettingsServerManager.get_server(domain).confirm_changes()


func _values_equal(a: Variant, b: Variant) -> bool:
	if typeof(a) in [TYPE_INT, TYPE_FLOAT] and typeof(b) in [TYPE_INT, TYPE_FLOAT]:
		return is_equal_approx(float(a), float(b))
	return a == b


## The stored values of the domain resource of a preset: setting name to value (empty for a null resource).
func _preset_values(preset: VideoPreset, domain: String) -> Dictionary:
	var values := {}
	var domain_settings: Resource = preset.get_domain_settings(domain)
	if domain_settings == null:
		return values
	for property in domain_settings.get_property_list():
		var property_name: String = property.name
		if not (property.usage & PROPERTY_USAGE_STORAGE) or property_name == "script" or property_name.begins_with("resource_") or property_name.begins_with("metadata/"):
			continue
		values[property_name] = domain_settings.get(property_name)
	return values


func _check_builtin() -> void:
	expect_equal(VideoPresetSettingsServer.get_custom_preset_name(), "Custom", "the custom name")
	expect_equal(VideoPresetSettingsServer.get_builtin_preset_names(), PackedStringArray(BUILTIN), "built-in names")
	expect_equal(VideoPresetSettingsServer.get_preset_names(), PackedStringArray(BUILTIN), "the project uses the built-in presets")
	expect_equal(VideoPresetSettingsServer.get_presets().size(), 4, "four presets")
	var low := VideoPresetSettingsServer.create_builtin_preset("Low")
	var ultra := VideoPresetSettingsServer.create_builtin_preset("Ultra")
	expect(low != null and ultra != null, "create_builtin_preset()")
	expect_equal(low.preset_name, "Low", "preset_name")
	for domain in DOMAINS:
		expect(low.has_domain_settings(domain), "Low has settings for \"%s\"" % domain)
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		expect_equal(low.get_domain_settings(domain).get_class(), server.get_settings().get_class(), "Low's \"%s\" has the class of the domain" % domain)
		for setting in _preset_values(ultra, domain):
			expect(server.has_setting(setting), "\"%s/%s\" exists in the domain" % [domain, setting])
	expect(low.render_quality is RenderQualitySettings and low.texture is TextureSettings and low.shadow is ShadowSettings and low.render_effect is RenderEffectSettings, "typed domain resources")
	expect(low.shadow.directional_shadow_atlas_size < ultra.shadow.directional_shadow_atlas_size, "Ultra has larger shadow maps than Low")
	expect(low.texture.anisotropic_filtering_level < ultra.texture.anisotropic_filtering_level, "Ultra has more anisotropic filtering than Low")
	expect(is_equal_approx(low.render_quality.scaling_3d_scale, 0.75), "Low renders at 75%")
	expect(VideoPresetSettingsServer.has_preset(VideoPresetSettingsServer.suggest_preset()), "suggest_preset() names a preset: %s" % VideoPresetSettingsServer.suggest_preset())


func _check_apply(preset_name: String) -> void:
	changed_names.clear()
	expect_equal(VideoPresetSettingsServer.apply_preset(preset_name), OK, "apply_preset(\"%s\")" % preset_name)
	_confirm_all()
	var preset: VideoPreset = VideoPresetSettingsServer.get_preset(preset_name)
	for domain in DOMAINS:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		expect(not server.is_dirty(), "\"%s\" applied the values at once" % domain)
		var values := _preset_values(preset, domain)
		for setting in values:
			if server.is_value_supported(setting):
				expect(_values_equal(server.get_value(setting), values[setting]), "%s: %s/%s = %s" % [preset_name, domain, setting, values[setting]])
	expect_equal(VideoPresetSettingsServer.get_current_preset(), preset_name, "the current preset")
	expect(not VideoPresetSettingsServer.is_custom(), "not custom")
	expect(VideoPresetSettingsServer.matches_preset(preset_name), "the values match the preset")
	expect(changed_names.has(preset_name), "preset_changed was emitted")


func _check_unsupported() -> void:
	_reset()
	var low: VideoPreset = VideoPresetSettingsServer.get_preset("Low")
	var before := {}
	for domain in DOMAINS:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		for setting in _preset_values(low, domain):
			before[domain + "/" + setting] = server.get_value(setting)
	VideoPresetSettingsServer.apply_preset("Low")
	_confirm_all()
	var skipped := 0
	for domain in DOMAINS:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		for setting in _preset_values(low, domain):
			if not server.is_value_supported(setting):
				skipped += 1
				expect(_values_equal(server.get_value(setting), before[domain + "/" + setting]), "unsupported %s/%s keeps its value" % [domain, setting])
	print("  renderer %s: %d values of Low skipped" % [RenderingServer.get_current_rendering_method(), skipped])


func _check_custom() -> void:
	var shadow: SettingsServer = SettingsServerManager.get_server("shadow")
	VideoPresetSettingsServer.apply_preset("High")
	_confirm_all()
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "High", "High applied")
	changed_names.clear()
	shadow.set_value("directional_shadow_atlas_size", 2048)
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "Custom", "a single change gives Custom")
	expect(VideoPresetSettingsServer.is_custom(), "is_custom()")
	expect(changed_names.has("Custom"), "preset_changed(\"Custom\")")
	shadow.set_value("directional_shadow_atlas_size", 4096)
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "High", "the preset value again gives the preset again")
	shadow.set_value("directional_shadow_atlas_size", 2048)
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "Custom", "Custom again")


func _check_storage() -> void:
	# save() writes the applied values; the domains are in APPLY_MODE_IMMEDIATE.
	for domain in DOMAINS:
		expect_equal(SettingsServerManager.get_server(domain).save(), OK, "save() of \"%s\"" % domain)
	expect_equal(VideoPresetSettingsServer.save(), OK, "save() of \"video_preset\"")
	expect(FileAccess.get_file_as_string(FILE).contains("Custom"), "the file stores \"Custom\"")
	_reset()
	expect_equal(VideoPresetSettingsServer.get_settings().preset, "", "reset: no preset stored")
	for domain in DOMAINS:
		SettingsServerManager.get_server(domain).load()
	expect_equal(VideoPresetSettingsServer.load(), OK, "load()")
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "Custom", "Custom is loaded")
	expect_equal(int(SettingsServerManager.get_server("shadow").get_value("directional_shadow_atlas_size")), 2048, "the changed value is loaded")


func _check_project_presets() -> void:
	ProjectSettings.set_setting("settings/video_preset/presets", PackedStringArray([PROJECT_HIGH, PROJECT_POTATO]))
	VideoPresetSettingsServer.reload_presets()
	expect_equal(VideoPresetSettingsServer.get_preset_names(), PackedStringArray(["Low", "Medium", "High", "Ultra", "Potato"]), "the project replaces High and adds Potato")
	var high: VideoPreset = VideoPresetSettingsServer.get_preset("High")
	expect(high.shadow is ShadowSettings, "the project's High loads a typed ShadowSettings")
	expect_equal(high.shadow.directional_shadow_atlas_size, 2048, "the project's High")
	expect(high.render_effect == null and not high.has_domain_settings("render_effect"), "the project's High leaves render_effect alone")
	var shadow: SettingsServer = SettingsServerManager.get_server("shadow")
	shadow.set_value("directional_shadow_atlas_size", 1024)
	expect_equal(VideoPresetSettingsServer.apply_preset("Potato"), OK, "apply_preset(\"Potato\")")
	_confirm_all()
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "Potato", "Potato is current")
	expect_equal(int(SettingsServerManager.get_server("texture").get_value("anisotropic_filtering_level")), 0, "Potato's anisotropic filtering")
	expect(is_equal_approx(float(SettingsServerManager.get_server("render_quality").get_value("scaling_3d_scale")), 0.5), "Potato's resolution scale")
	expect_equal(int(shadow.get_value("directional_shadow_atlas_size")), 1024, "Potato leaves shadow as it is")
	shadow.set_value("directional_shadow_atlas_size", 2048)
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "Potato", "a change in a domain Potato doesn't touch keeps Potato")
	SettingsServerManager.get_server("texture").set_value("anisotropic_filtering_level", Viewport.ANISOTROPY_16X)
	expect_equal(VideoPresetSettingsServer.get_current_preset(), "Custom", "a change in a domain Potato sets gives Custom")

	ProjectSettings.set_setting("settings/video_preset/use_builtin_presets", false)
	VideoPresetSettingsServer.reload_presets()
	expect_equal(VideoPresetSettingsServer.get_preset_names(), PackedStringArray(["High", "Potato"]), "without the built-in presets")

	ProjectSettings.set_setting("settings/video_preset/use_builtin_presets", true)
	ProjectSettings.set_setting("settings/video_preset/presets", PackedStringArray())
	VideoPresetSettingsServer.reload_presets()
	expect_equal(VideoPresetSettingsServer.get_preset_names(), PackedStringArray(BUILTIN), "back to the built-in presets")


func _check_runtime_presets() -> void:
	var cinematic := VideoPresetSettingsServer.create_builtin_preset("Ultra")
	cinematic.preset_name = "Cinematic"
	cinematic.render_effect.dof_blur_quality = RenderingServer.DOF_BLUR_QUALITY_HIGH
	expect(cinematic.render_effect != VideoPresetSettingsServer.get_preset("Ultra").render_effect, "create_builtin_preset() returns new domain resources")
	VideoPresetSettingsServer.add_preset(cinematic)
	expect(VideoPresetSettingsServer.has_preset("Cinematic"), "add_preset()")
	expect_equal(VideoPresetSettingsServer.get_preset("Cinematic"), cinematic, "get_preset()")
	VideoPresetSettingsServer.remove_preset("Cinematic")
	expect(not VideoPresetSettingsServer.has_preset("Cinematic"), "remove_preset()")
	expect_equal(VideoPresetSettingsServer.apply_preset("Custom"), ERR_INVALID_PARAMETER, "\"Custom\" can't be applied")
	expect_equal(VideoPresetSettingsServer.apply_preset("Missing"), ERR_DOES_NOT_EXIST, "a missing preset")
