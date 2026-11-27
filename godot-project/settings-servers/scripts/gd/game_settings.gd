extends SettingsServer
## The "game" domain of the wiki guide. Registered by Example.gd (a game lists it in settings/startup/custom_servers).

signal difficulty_changed(difficulty: int)


func _init() -> void:
	bind_setting_singleton_property("max_fps", "Engine", "max_fps")
	bind_setting_callbacks("music_volume", _set_music_volume, _get_music_volume)


func _get_domain() -> StringName:
	return &"game"


func _create_settings() -> Resource:
	return GameSettingsResource.new()


func _value_applied(name: StringName, value: Variant) -> void:
	if name == &"difficulty":
		difficulty_changed.emit(value)


func _get_format_version() -> int:
	return 2


func _migrate(from_version: int, data: Dictionary) -> Dictionary:
	if from_version < 2 and data.has("volume"): # Renamed in version 2.
		data["music_volume"] = data["volume"]
		data.erase("volume")
	return data


func _set_music_volume(value: float) -> void:
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index("Master"), linear_to_db(value))


func _get_music_volume() -> float:
	return db_to_linear(AudioServer.get_bus_volume_db(AudioServer.get_bus_index("Master")))
