extends SettingsServer
## GDScript settings domain "check15_gd" of the 15-feature-settings-server checks.
## Listed in the project setting settings/startup/custom_servers, so SettingsServerManager creates it
## and applies its file at startup. It "applies" its values to Engine metadata, so a check can see
## what was applied and in which frame.


func _get_domain() -> StringName:
	return &"check15_gd"


func _get_apply_phase() -> int:
	return SettingsServer.APPLY_PHASE_SCENE


func _create_settings() -> Resource:
	return Issue15CheckSettings.new()


func _apply(settings: Resource, _names: PackedStringArray) -> void:
	Engine.set_meta(&"check15_gd_applied", {
		"volume": settings.volume,
		"quality": settings.quality,
		"frame": Engine.get_process_frames(),
	})
