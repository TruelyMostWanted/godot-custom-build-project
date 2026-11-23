extends FeatureCheck
## GDScript checks of 16-feature-audio-settings. Check the same things as scripts/cs/Issue16FeatureAudioSettingsCheck.cs.
##
## The project's bus layout (resources/bus_layout.tres, set in audio/buses/default_bus_layout) has the
## buses Master, Music (-6 dB) and Sfx. The restart check starts the project a second time as its own
## process (desktop only) and reads the bus values that were applied before the main scene was ready.

const DIR := "user://16-feature-audio-settings"
const PROBE_SCENE := "res://16-feature-audio-settings/scenes/restart_probe.tscn"
const BUSES_FILE := "user://settings/audio_buses.ini"
const DEVICES_FILE := "user://settings/audio.ini"


func _get_check_name() -> String:
	return "16-feature-audio-settings"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	expect(Engine.has_singleton("AudioBusSettingsServer"), "AudioBusSettingsServer is a registered singleton")
	expect(Engine.has_singleton("AudioSettingsServer"), "AudioSettingsServer is a registered singleton")
	expect(AudioBusSettingsServer is ListSettingsServer, "AudioBusSettingsServer is a ListSettingsServer")
	expect(AudioSettingsServer is SettingsServer, "AudioSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"audio_buses"), AudioBusSettingsServer, "the bus domain is registered as \"audio_buses\"")
	expect_equal(SettingsServerManager.get_server(&"audio"), AudioSettingsServer, "the device domain is registered as \"audio\"")
	expect(AudioBusSettingsServer.is_started(), "the bus domain was started before the main scene")
	expect(AudioSettingsServer.is_started(), "the device domain was started before the main scene")
	expect_equal(AudioBusSettingsServer.get_file_path(), BUSES_FILE, "bus file path")
	expect_equal(AudioSettingsServer.get_file_path(), DEVICES_FILE, "device file path")

	_check_buses()
	_check_manual_mode()
	_check_csv()
	_check_missing_bus()
	_check_devices()
	await _check_restart()

	# Leave no player files behind for later runs.
	AudioBusSettingsServer.reset_to_defaults()
	AudioSettingsServer.reset_to_defaults()
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
	expect(absf(AudioBusSettingsServer.get_bus_volume_linear(&"Sfx") - 0.5) < 0.001, "linear volume (50 %)")
	expect(absf(_bus_volume_db(&"Sfx") - linear_to_db(0.5)) < 0.01, "linear volume is stored in dB")
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	expect(AudioServer.is_bus_mute(AudioServer.get_bus_index(&"Music")), "set_bus_mute() reaches AudioServer")
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	expect(AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "set_bus_solo() reaches AudioServer")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", 100.0)
	expect(is_equal_approx(AudioBusSettingsServer.get_bus_volume_db(&"Music"), 24.0), "volume is clamped to 24 dB")

	AudioBusSettingsServer.reset_to_defaults()
	expect(is_equal_approx(_bus_volume_db(&"Music"), -6.0), "reset_to_defaults() gives back the layout volume")
	expect(not AudioServer.is_bus_mute(AudioServer.get_bus_index(&"Music")), "reset_to_defaults() unmutes")
	expect(not AudioServer.is_bus_solo(AudioServer.get_bus_index(&"Sfx")), "reset_to_defaults() clears solo")


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
	ProjectSettings.set_setting("settings/storage/format", "csv")
	expect_equal(AudioBusSettingsServer.get_file_path(), "user://settings/audio_buses.csv", "the bus list can be stored as CSV")
	expect_equal(AudioSettingsServer.get_file_path(), DEVICES_FILE, "the devices stay INI (a warning is printed once)")
	ProjectSettings.set_setting("settings/storage/format", "ini")

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

	AudioBusSettingsServer.create_missing_buses = true
	AudioBusSettingsServer.set_entry_value(index, &"volume_db", -10.0)
	AudioBusSettingsServer.apply()
	var bus := AudioServer.get_bus_index(&"Voice")
	expect(bus >= 0, "create_missing_buses adds the bus")
	expect(bus >= 0 and is_equal_approx(AudioServer.get_bus_volume_db(bus), -10.0), "the created bus gets the stored volume")

	AudioBusSettingsServer.create_missing_buses = false
	AudioBusSettingsServer.remove_entry(AudioBusSettingsServer.find_entry("Voice"))
	AudioBusSettingsServer.apply()
	if bus >= 0:
		AudioServer.remove_bus(bus)
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE


func _check_devices() -> void:
	expect_equal(AudioSettingsServer.get_setting_names(), PackedStringArray(["output_device", "input_device"]), "device setting names")
	expect_equal(AudioSettingsServer.is_value_supported(&"output_device"), AudioSettingsServer.is_device_selection_supported(), "is_value_supported() follows the driver")
	AudioSettingsServer.set_output_device("Check16 device that does not exist")
	expect_equal(AudioSettingsServer.get_output_device(), "Default", "an unknown output device falls back to \"Default\"")
	expect_equal(AudioServer.output_device, "Default", "AudioServer keeps \"Default\"")
	AudioSettingsServer.set_input_device("")
	expect_equal(AudioSettingsServer.get_input_device(), "Default", "an empty input device is \"Default\"")

	var path := DIR.path_join("devices.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(JSON.stringify({"output_device": "Speakers (unplugged)", "input_device": "Microphone (unplugged)"}))
	file.close()
	AudioSettingsServer.file_path = path
	expect_equal(AudioSettingsServer.load(), OK, "a file with devices of another machine loads without an error")
	expect_equal(AudioSettingsServer.get_output_device(), "Default", "a stored output device that no longer exists becomes \"Default\"")
	expect_equal(AudioSettingsServer.get_input_device(), "Default", "a stored input device that no longer exists becomes \"Default\"")
	AudioSettingsServer.file_path = ""
	AudioSettingsServer.apply()

	var devices := AudioServer.get_output_device_list()
	if AudioSettingsServer.is_device_selection_supported() and devices.size() > 1:
		AudioSettingsServer.set_output_device(devices[1])
		expect_equal(AudioSettingsServer.get_output_device(), devices[1], "a listed output device is kept")
		AudioSettingsServer.set_output_device("Default")
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
