extends FeatureCheck
## GDScript checks of 16-feature-audio-settings. Check the same things as scripts/cs/Issue16FeatureAudioSettingsCheck.cs.
##
## The project's bus layout (resources/bus_layout.tres, set in audio/buses/default_bus_layout) has the
## buses Master, Music (-6 dB) and Sfx. The restart check starts the project a second time as its own
## process (desktop only) and reads the bus values that were applied before the main scene was ready.

const DIR := "user://16-feature-audio-settings"
const PROBE_SCENE := "res://16-feature-audio-settings/scenes/restart_probe.tscn"
const NIGHT_PRESET := "res://16-feature-audio-settings/resources/night_mode_preset.tres"
const BUSES_FILE := "user://settings/audio_bus.ini"
const DEVICES_FILE := "user://settings/audio_device.ini"


func _get_check_name() -> String:
	return "16-feature-audio-settings"


func _run_checks() -> void:
	await _check_typed_signals()
	DirAccess.make_dir_recursive_absolute(DIR)
	expect(Engine.has_singleton("AudioBusSettingsServer"), "AudioBusSettingsServer is a registered singleton")
	expect(Engine.has_singleton("AudioDeviceSettingsServer"), "AudioDeviceSettingsServer is a registered singleton")
	expect(Engine.get_singleton("AudioBusSettingsServer") is ListSettingsServer, "AudioBusSettingsServer is a ListSettingsServer")
	expect(Engine.get_singleton("AudioDeviceSettingsServer") is SettingsServer, "AudioDeviceSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"audio_bus"), Engine.get_singleton("AudioBusSettingsServer"), "#185: the bus domain is registered as \"audio_bus\"")
	expect_equal(SettingsServerManager.get_server(&"audio_device"), Engine.get_singleton("AudioDeviceSettingsServer"), "#185: the device domain is registered as \"audio_device\"")
	expect(SettingsServerManager.get_server(&"audio_buses") == null and SettingsServerManager.get_server(&"audio") == null, "#185: the old domain names are gone")
	expect(ProjectSettings.has_setting("settings/audio_bus/player_buses"), "#185: project setting settings/audio_bus/player_buses")
	expect(ProjectSettings.get_setting("settings/audio_bus/store_volume") == true and ProjectSettings.get_setting("settings/audio_bus/store_mute") == true and ProjectSettings.get_setting("settings/audio_bus/store_solo") == false, "#185: store_volume and store_mute on, store_solo off")
	expect(not ProjectSettings.has_setting("settings/audio/create_missing_buses"), "#185: create_missing_buses is gone")
	expect(not "create_missing_buses" in AudioBusSettingsServer.get_property_list().map(func(p): return p.name), "#185: the property create_missing_buses is gone")
	expect(AudioBusSettingsServer.is_started(), "the bus domain was started before the main scene")
	expect(AudioDeviceSettingsServer.is_started(), "the device domain was started before the main scene")
	expect_equal(AudioBusSettingsServer.get_file_path(), BUSES_FILE, "bus file path")
	expect_equal(AudioDeviceSettingsServer.get_file_path(), DEVICES_FILE, "device file path")

	_check_buses()
	_check_manual_mode()
	_check_csv()
	_check_missing_bus()
	_check_stored_values()
	_check_layout_change()
	_check_presets()
	_check_devices()
	await _check_restart()

	# Leave no player files behind for later runs.
	AudioBusSettingsServer.reset_to_defaults()
	AudioDeviceSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(BUSES_FILE)
	DirAccess.remove_absolute(DEVICES_FILE)
	_remove_dir(DIR)


func _bus_volume_db(bus: StringName) -> float:
	return AudioServer.get_bus_volume_db(AudioServer.get_bus_index(bus))


func _check_buses() -> void:
	var defaults: Array = AudioBusSettingsServer.get_default_value(&"buses")
	expect_equal(defaults.size(), 3, "defaults: one entry per bus of the layout")
	expect(defaults.size() == 3 and is_equal_approx(defaults[1].volume_db, -6.0), "defaults come from the bus layout")
	expect_equal(AudioBusSettingsServer.get_bus_names(), PackedStringArray(["Master", "Music", "Sfx"]), "buses in layout order")
	expect(AudioBusSettingsServer.has_bus(&"Music"), "has_bus() for a bus of the layout")
	expect(not AudioBusSettingsServer.has_bus(&"Voice"), "has_bus() for an unknown bus")

	AudioBusSettingsServer.set_bus_volume_db(&"Music", -12.0)
	expect(is_equal_approx(_bus_volume_db(&"Music"), -12.0), "set_bus_volume_db() reaches AudioServer at once")
	expect(is_equal_approx(AudioBusSettingsServer.get_bus_volume_db(&"Music"), -12.0), "get_bus_volume_db()")
	AudioBusSettingsServer.set_bus_volume_linear(&"Sfx", 0.5)
	# The dB value is snapped to 0.1 dB, so 50 % comes back as 0.501.
	expect(absf(AudioBusSettingsServer.get_bus_volume_linear(&"Sfx") - 0.5) < 0.01, "linear volume (50 %)")
	expect(is_equal_approx(_bus_volume_db(&"Sfx"), -6.0), "linear volume is stored in dB, snapped to 0.1 dB (#189)")
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	expect(AudioServer.is_bus_mute(AudioServer.get_bus_index(&"Music")), "set_bus_mute() reaches AudioServer")
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	expect(not AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "#185: set_bus_solo() does nothing while solo is not stored (a warning is printed)")
	AudioBusSettingsServer.store_solo = true
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	expect(AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "set_bus_solo() reaches AudioServer with store_solo")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", 100.0)
	expect(is_equal_approx(AudioBusSettingsServer.get_bus_volume_db(&"Music"), 24.0), "volume is clamped to 24 dB")

	AudioBusSettingsServer.reset_to_defaults()
	expect(is_equal_approx(_bus_volume_db(&"Music"), -6.0), "reset_to_defaults() gives back the layout volume")
	expect(not AudioServer.is_bus_mute(AudioServer.get_bus_index(&"Music")), "reset_to_defaults() unmutes")
	expect(not AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "reset_to_defaults() clears solo")
	AudioBusSettingsServer.store_solo = false


func _check_manual_mode() -> void:
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -30.0)
	expect(is_equal_approx(_bus_volume_db(&"Music"), -6.0), "manual mode: not applied yet")
	expect(AudioBusSettingsServer.is_dirty(), "manual mode: dirty")
	AudioBusSettingsServer.revert()
	expect(is_equal_approx(AudioBusSettingsServer.get_bus_volume_db(&"Music"), -6.0), "revert() gives back the applied volume")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -30.0)
	expect_equal(AudioBusSettingsServer.apply(), OK, "apply() in manual mode")
	expect(is_equal_approx(_bus_volume_db(&"Music"), -30.0), "apply() reaches AudioServer")
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	AudioBusSettingsServer.reset_to_defaults()


func _check_csv() -> void:
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_CSV)
	expect_equal(AudioBusSettingsServer.get_file_path(), "user://settings/audio_bus.csv", "the bus list can be stored as CSV")
	expect_equal(AudioDeviceSettingsServer.get_file_path(), DEVICES_FILE, "the devices stay INI (a warning is printed once)")
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_INI)

	var path := DIR.path_join("buses.csv")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -20.0)
	AudioBusSettingsServer.set_bus_mute(&"Sfx", true)
	AudioBusSettingsServer.file_path = path
	expect_equal(AudioBusSettingsServer.save(), OK, "save() as CSV")
	var rows := {"Master": 0, "Music": 0, "Sfx": 0}
	for line in FileAccess.get_file_as_string(path).split("\n"):
		for bus in rows:
			if line.contains(bus):
				rows[bus] += 1
	expect_equal(rows, {"Master": 1, "Music": 1, "Sfx": 1}, "CSV: one row per bus")
	var header := FileAccess.get_file_as_string(path).split("\n")[0]
	expect(header.contains("volume_db") and header.contains("mute") and not header.contains("solo"), "#185: CSV columns are the stored values only: " + header)

	AudioBusSettingsServer.set_bus_volume_db(&"Music", -3.0)
	AudioBusSettingsServer.set_bus_mute(&"Sfx", false)
	expect_equal(AudioBusSettingsServer.load(), OK, "load() reads the CSV")
	expect(is_equal_approx(AudioBusSettingsServer.get_bus_volume_db(&"Music"), -20.0), "CSV: volume loaded")
	expect(AudioBusSettingsServer.is_bus_mute(&"Sfx"), "CSV: mute loaded")
	AudioBusSettingsServer.apply()
	expect(is_equal_approx(_bus_volume_db(&"Music"), -20.0), "CSV: loaded volume applied")
	AudioBusSettingsServer.file_path = ""
	AudioBusSettingsServer.reset_to_defaults()


func _check_missing_bus() -> void:
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	var voice := AudioBusSettings.new()
	voice.bus_name = "Voice"
	voice.volume_db = -9.0
	AudioBusSettingsServer.add_entry(voice)
	AudioBusSettingsServer.apply()
	expect_equal(AudioServer.get_bus_index(&"Voice"), -1, "a bus missing in the layout is skipped (a warning is printed)")
	var index := AudioBusSettingsServer.find_entry("Voice")
	expect(index >= 0, "the entry of a missing bus is kept")

	expect_equal(AudioServer.get_bus_count(), 3, "#185: no bus is created; the layout builds the buses")
	AudioBusSettingsServer.remove_entry(index)
	AudioBusSettingsServer.apply()
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE


# Issue #122: a new bus layout at runtime resets the bus values; the stored values are applied again.
func _check_layout_change() -> void:
	var game_layout := AudioServer.generate_bus_layout()
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -21.0)
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	AudioBusSettingsServer.store_solo = true
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	AudioServer.set_bus_layout(game_layout)
	expect(is_equal_approx(_bus_volume_db(&"Music"), -21.0), "#122: set_bus_layout() keeps the stored volume")
	expect(AudioServer.is_bus_mute(AudioServer.get_bus_index(&"Music")), "#122: set_bus_layout() keeps the stored mute")
	expect(AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "#122: set_bus_layout() keeps the stored solo")
	expect(not AudioBusSettingsServer.is_dirty(), "#122: the re-apply changes no setting")

	# #185: player buses. Sfx is not a player bus any more: a new layout keeps its own Sfx mix.
	AudioBusSettingsServer.player_buses = PackedStringArray(["Music"])
	var sfx := AudioServer.get_bus_index(&"Sfx")
	AudioServer.set_bus_volume_db(sfx, -9.0)
	AudioServer.set_bus_solo(sfx, false)
	var quiet_layout := AudioServer.generate_bus_layout()
	AudioServer.set_bus_layout(quiet_layout)
	expect(is_equal_approx(_bus_volume_db(&"Music"), -21.0), "#185: the player bus gets the player value after set_bus_layout()")
	expect(is_equal_approx(_bus_volume_db(&"Sfx"), -9.0), "#185: a bus that is not a player bus keeps the value of the new layout")
	expect(not AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "#185: the solo of a non-player bus is not pushed again")
	AudioBusSettingsServer.set_bus_volume_db(&"Sfx", -1.0)
	expect(is_equal_approx(_bus_volume_db(&"Sfx"), -9.0), "#185: set_bus_volume_db() on a non-player bus does nothing (a warning is printed)")
	AudioBusSettingsServer.player_buses = PackedStringArray()
	AudioServer.set_bus_layout(game_layout)

	# In manual mode, the applied values come back, not the pending ones.
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -40.0)
	AudioServer.set_bus_layout(game_layout)
	expect(is_equal_approx(_bus_volume_db(&"Music"), -21.0), "#122: manual mode: the applied volume comes back, the pending one waits")
	AudioBusSettingsServer.revert()
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	AudioBusSettingsServer.reset_to_defaults()
	expect(is_equal_approx(_bus_volume_db(&"Music"), -6.0), "#122: reset_to_defaults() after the layout change")
	expect(not AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "#122: reset_to_defaults() clears the stored solo")
	AudioBusSettingsServer.store_solo = false


# Issue #185: only the stored values are written; a file with values that are no longer stored is read without them,
# stored values missing in the file come from the layout.
func _check_stored_values() -> void:
	var path := DIR.path_join("stored_values.ini")
	AudioBusSettingsServer.file_path = path
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -14.0)
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	expect_equal(AudioBusSettingsServer.save(), OK, "#185: save() with the default store_* settings")
	var text := FileAccess.get_file_as_string(path)
	expect(text.contains("volume_db") and text.contains("mute") and not text.contains("solo"), "#185: the file holds volume and mute, no solo")

	var csv_path := DIR.path_join("hand_edited.csv")
	var file := FileAccess.open(csv_path, FileAccess.WRITE)
	file.store_string("bus_name,volume_db,solo\nMusic,-17,true\n")
	file.close()
	AudioBusSettingsServer.file_path = csv_path
	expect_equal(AudioBusSettingsServer.load(), OK, "#185: a file with an unstored value loads")
	expect(is_equal_approx(AudioBusSettingsServer.get_bus_volume_db(&"Music"), -17.0), "#185: the stored volume is read")
	expect(not AudioBusSettingsServer.is_bus_solo(&"Music"), "#185: the solo of the file is ignored")
	expect(not AudioBusSettingsServer.is_bus_mute(&"Music"), "#185: the mute missing in the file comes from the layout")
	expect_equal(AudioBusSettingsServer.get_bus_names(), PackedStringArray(["Master", "Music", "Sfx"]), "#185: buses missing in the file come from the layout")
	AudioBusSettingsServer.apply()
	AudioBusSettingsServer.file_path = ""
	AudioBusSettingsServer.reset_to_defaults()


# Issue #185: an AudioBusListSettings in res:// is a preset; buses it does not list stay unchanged.
func _check_presets() -> void:
	var preset: AudioBusListSettings = load(NIGHT_PRESET)
	expect(preset != null and preset.buses.size() == 1, "#185: the preset resource loads")
	if preset == null:
		return
	AudioBusSettingsServer.set_bus_volume_db(&"Sfx", -5.0)
	expect(AudioBusSettingsServer.is_custom(), "#185: no preset applied: Custom")
	expect_equal(AudioBusSettingsServer.apply_preset(preset), OK, "#185: apply_preset()")
	expect(is_equal_approx(_bus_volume_db(&"Music"), -18.0), "#185: the preset volume reaches AudioServer")
	expect(AudioServer.is_bus_mute(AudioServer.get_bus_index(&"Music")), "#185: the preset mute reaches AudioServer")
	expect(is_equal_approx(_bus_volume_db(&"Sfx"), -5.0), "#185: a bus that the preset does not list keeps its value")
	expect_equal(AudioBusSettingsServer.get_current_preset(), preset, "#185: get_current_preset()")
	expect(not AudioBusSettingsServer.is_custom(), "#185: not Custom right after apply_preset()")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -3.0)
	expect(AudioBusSettingsServer.is_custom(), "#185: a single change makes it Custom")
	expect(not AudioBusSettingsServer.matches_preset(preset), "#185: matches_preset() is false after the change")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -18.0)
	expect(AudioBusSettingsServer.matches_preset(preset), "#185: matches_preset() is true again")
	AudioBusSettingsServer.reset_to_defaults()


func _check_devices() -> void:
	expect_equal(AudioDeviceSettingsServer.get_setting_names(), PackedStringArray(["output_device", "input_device"]), "device setting names")
	expect_equal(AudioDeviceSettingsServer.is_value_supported(&"output_device"), AudioDeviceSettingsServer.is_device_selection_supported(), "is_value_supported() follows the driver")
	AudioDeviceSettingsServer.set_output_device("Check16 device that does not exist")
	expect_equal(AudioDeviceSettingsServer.get_output_device(), "Default", "an unknown output device falls back to \"Default\"")
	expect_equal(AudioServer.output_device, "Default", "AudioServer keeps \"Default\"")
	AudioDeviceSettingsServer.set_input_device("")
	expect_equal(AudioDeviceSettingsServer.get_input_device(), "Default", "an empty input device is \"Default\"")

	var path := DIR.path_join("devices.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(JSON.stringify({"output_device": "Speakers (unplugged)", "input_device": "Microphone (unplugged)"}))
	file.close()
	AudioDeviceSettingsServer.file_path = path
	expect_equal(AudioDeviceSettingsServer.load(), OK, "a file with devices of another machine loads without an error")
	expect_equal(AudioDeviceSettingsServer.get_output_device(), "Default", "a stored output device that no longer exists becomes \"Default\"")
	expect_equal(AudioDeviceSettingsServer.get_input_device(), "Default", "a stored input device that no longer exists becomes \"Default\"")
	AudioDeviceSettingsServer.file_path = ""
	AudioDeviceSettingsServer.apply()

	var devices := AudioServer.get_output_device_list()
	if AudioDeviceSettingsServer.is_device_selection_supported() and devices.size() > 1:
		AudioDeviceSettingsServer.set_output_device(devices[1])
		expect_equal(AudioDeviceSettingsServer.get_output_device(), devices[1], "a listed output device is kept")
		AudioDeviceSettingsServer.set_output_device("Default")
	else:
		print("  SKIP  device switch (the driver has no device list or only one device)")


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return
	var volume := snappedf(randf_range(-30.0, -10.0), 0.1)
	AudioBusSettingsServer.set_bus_volume_db(&"Music", volume)
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	expect_equal(AudioBusSettingsServer.save(), OK, "save() writes the player file")
	expect(FileAccess.file_exists(BUSES_FILE), "the player file is in user://settings/")

	var probe := _run_probe()
	expect(probe.get("buses_started", false), "restart: the bus domain was started")
	expect(probe.get("devices_started", false), "restart: the device domain was started")
	expect(is_equal_approx(probe.get("music_volume_db", 1000.0), volume), "restart: the saved Music volume was applied before the main scene")
	expect(probe.get("music_mute", false), "restart: the saved mute was applied")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: applied before the first frame, so before any sound")


func _run_probe() -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK16_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK16_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}


func _remove_dir(path: String) -> void:
	for file_name in DirAccess.get_files_at(path):
		DirAccess.remove_absolute(path.path_join(file_name))
	DirAccess.remove_absolute(path)


## Issue #173: the typed bus signals fire once per applied change, not for pending changes in manual mode.
func _check_typed_signals() -> void:
	if not Engine.has_singleton("AudioBusSettingsServer") or not Engine.has_singleton("AudioDeviceSettingsServer"):
		return
	var devices: SettingsServer = Engine.get_singleton("AudioDeviceSettingsServer")
	expect(devices.has_signal(&"output_device_changed"), "AudioDeviceSettingsServer has output_device_changed")
	expect(devices.has_signal(&"input_device_changed"), "AudioDeviceSettingsServer has input_device_changed")
	var buses: Object = Engine.get_singleton("AudioBusSettingsServer")
	var s: SettingsServer = buses
	for sig in [&"bus_volume_changed", &"bus_mute_changed", &"bus_solo_changed", &"bus_layout_changed"]:
		expect(s.has_signal(sig), "AudioBusSettingsServer has %s" % sig)
	var file_path := s.get_file_path()
	var had_file := FileAccess.file_exists(file_path)
	var previous_mode := s.apply_mode
	s.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	s.reset_to_defaults()
	var volumes: Array = []
	var mutes: Array = []
	var on_volume := func(bus_name: StringName, volume_db: float) -> void: volumes.append([bus_name, volume_db])
	var on_mute := func(bus_name: StringName, muted: bool) -> void: mutes.append([bus_name, muted])
	s.connect(&"bus_volume_changed", on_volume)
	s.connect(&"bus_mute_changed", on_mute)

	s.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	var volume: float = -6.0 if buses.get_bus_volume_db(&"Master") != -6.0 else -12.0
	buses.set_bus_volume_db(&"Master", volume)
	expect_equal(volumes.size(), 0, "bus_volume_changed does not fire for a pending change in manual mode")
	s.apply()
	expect_equal(volumes, [[&"Master", volume]], "bus_volume_changed fires once on apply() with the bus and the volume")
	expect_equal(mutes.size(), 0, "bus_mute_changed does not fire for an unchanged mute state")
	if not volumes.is_empty():
		expect_equal(typeof(volumes[0][0]), TYPE_STRING_NAME, "bus_volume_changed passes the bus name as StringName")
		expect_equal(typeof(volumes[0][1]), TYPE_FLOAT, "bus_volume_changed passes the volume as float")
	buses.set_bus_mute(&"Master", not buses.is_bus_mute(&"Master"))
	s.apply()
	expect_equal(mutes.size(), 1, "bus_mute_changed fires once on apply()")
	expect_equal(volumes.size(), 1, "bus_volume_changed does not fire again")
	s.reset_to_defaults()
	s.apply()
	expect_equal(volumes.size(), 2, "bus_volume_changed fires again when the default is applied")
	expect_equal(mutes.size(), 2, "bus_mute_changed fires again when the default is applied")

	s.disconnect(&"bus_volume_changed", on_volume)
	s.disconnect(&"bus_mute_changed", on_mute)
	s.apply_mode = previous_mode
	await get_tree().process_frame
	if not had_file:
		DirAccess.remove_absolute(file_path)
