extends SettingsServer
## Domain "test_gd". Listed in the project setting settings/startup/custom_servers, so SettingsServerManager
## starts it before the main scene. It "applies" its values to Engine metadata, so a test can see what was applied and when.


func _get_domain() -> StringName:
	return &"test_gd"


func _get_apply_phase() -> SettingsServer.ApplyPhase:
	return SettingsServer.APPLY_PHASE_PRE_DISPLAY


func _create_settings() -> Resource:
	return SettingsServersTestSettings.new()


func _apply(settings: Resource, _names: PackedStringArray) -> void:
	Engine.set_meta(&"test_gd_applied", {
		"volume": settings.volume,
		"quality": settings.quality,
		"frame": Engine.get_process_frames(),
	})


func _validate_value(name: StringName, value: Variant) -> Variant:
	if name == &"label":
		return String(value).strip_edges()
	return value


func _is_value_supported(name: StringName) -> bool:
	return name != &"hidden"


func _read_from_engine(settings: Resource) -> void:
	settings.label = "engine"


func _get_format_version() -> int:
	return 2


func _migrate(from_version: int, data: Dictionary) -> Dictionary:
	if from_version < 2 and data.has("legacy_volume"):
		data["volume"] = data["legacy_volume"]
		data.erase("legacy_volume")
	return data
