extends FeatureTest
## Full API test of Audio Bus Settings: AudioBusSettingsServer, AudioBusSettings, AudioBusListSettings, AudioBusSettingsRow(s).
## Same tests as scripts/cs/Test.cs. The project's bus layout (resources/bus_layout.tres) has the buses Master, Music (-6 dB) and Sfx.

const DIR := "user://audio-bus-settings-test-gd"
const PROBE_SCENE := "res://audio-bus-settings/scenes/restart_probe.tscn"
const NIGHT_PRESET := "res://audio-bus-settings/resources/night_mode_preset.tres"
const BUSES_FILE := "user://settings/audio_bus.ini"
const LAYOUT_MUSIC_DB := -6.0
const NIGHT_MUSIC_DB := -18.0
const MAX_DB := 24.0

var _volumes: Array = []
var _mutes: Array = []
var _solos: Array = []
var _layout_changes: int = 0


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_test_registration()
	_test_resources()
	_test_buses()
	_test_manual_mode()
	await _test_signals()
	_test_csv()
	_test_missing_bus()
	_test_stored_values()
	_test_layout_change()
	_test_presets()
	await _test_restart()
	AudioBusSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(BUSES_FILE)
	_remove_dir(DIR)


func _bus_volume_db(bus: StringName) -> float:
	return AudioServer.get_bus_volume_db(AudioServer.get_bus_index(bus))


func _bus_mute(bus: StringName) -> bool:
	return AudioServer.is_bus_mute(AudioServer.get_bus_index(bus))


func _bus_solo(bus: StringName) -> bool:
	return AudioServer.is_bus_solo(AudioServer.get_bus_index(bus))


func _test_registration() -> void:
	expect(Engine.has_singleton("AudioBusSettingsServer"), "AudioBusSettingsServer is a registered singleton")
	expect(Engine.get_singleton("AudioBusSettingsServer") is ListSettingsServer, "AudioBusSettingsServer is a ListSettingsServer")
	expect_equal(SettingsServerManager.get_server(&"audio_bus"), AudioBusSettingsServer, "the domain is registered as audio_bus")
	expect(AudioBusSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(AudioBusSettingsServer.file_path, BUSES_FILE, "bus file path")
	expect(ProjectSettings.has_setting("settings/audio_bus/player_buses"), "project setting settings/audio_bus/player_buses")
	expect_equal(ProjectSettings.get_setting("settings/audio_bus/store_volume"), true, "store_volume is on by default")
	expect_equal(ProjectSettings.get_setting("settings/audio_bus/store_mute"), true, "store_mute is on by default")
	expect_equal(ProjectSettings.get_setting("settings/audio_bus/store_solo"), false, "store_solo is off by default")
	expect(AudioBusSettingsServer.store_volume, "store_volume property")
	expect(AudioBusSettingsServer.store_mute, "store_mute property")
	expect(not AudioBusSettingsServer.store_solo, "store_solo property")
	expect(AudioBusSettingsServer.player_buses.is_empty(), "player_buses is empty: the player controls every bus")


func _test_resources() -> void:
	var bus := AudioBusSettings.new()
	bus.bus_name = "Voice"
	bus.volume_db = -9.0
	bus.mute = true
	bus.solo = true
	expect_equal(bus.bus_name, "Voice", "AudioBusSettings.bus_name")
	expect_equal(bus.volume_db, -9.0, "AudioBusSettings.volume_db")
	expect(bus.mute and bus.solo, "AudioBusSettings.mute and solo")
	bus.set_volume_linear(0.5)
	expect(absf(bus.get_volume_linear() - 0.5) < 0.01, "AudioBusSettings volume_linear round trip")
	bus.set_volume_linear(0.0)
	expect_equal(bus.volume_db, -80.0, "linear 0.0 is -80 dB")
	var list := AudioBusListSettings.new()
	list.buses.append(bus)
	expect_equal(list.buses.size(), 1, "AudioBusListSettings.buses holds AudioBusSettings")
	var row := AudioBusSettingsRow.new()
	row.bus_name = "Voice"
	row.volume_db = -9.0
	row.mute = true
	row.solo = true
	expect_equal(row.bus_name, "Voice", "AudioBusSettingsRow.bus_name")
	expect_equal(row.volume_db, -9.0, "AudioBusSettingsRow.volume_db")
	expect(row.mute and row.solo, "AudioBusSettingsRow.mute and solo")
	var rows := AudioBusSettingsRows.new()
	rows.buses.append(row)
	expect_equal(rows.buses.size(), 1, "AudioBusSettingsRows.buses holds rows")
	var sample: AudioBusSettingsRows = load("res://audio-bus-settings/resources/bus_rows.tres")
	expect_equal(sample.buses[0].bus_name, "Sfx", "the sample AudioBusSettingsRows loads")
	var sample_bus: AudioBusSettings = load("res://audio-bus-settings/resources/sfx_bus.tres")
	expect_equal(sample_bus.volume_db, -3.0, "the sample AudioBusSettings loads")


func _test_buses() -> void:
	var defaults: Array = AudioBusSettingsServer.get_default_value(&"buses")
	expect_equal(defaults.size(), 3, "defaults: one entry per bus of the layout")
	expect_equal(defaults[1].volume_db, LAYOUT_MUSIC_DB, "defaults come from the bus layout")
	expect_equal(AudioBusSettingsServer.get_bus_names(), PackedStringArray(["Master", "Music", "Sfx"]), "buses in layout order")
	expect(AudioBusSettingsServer.has_bus(&"Music"), "has_bus for a bus of the layout")
	expect(not AudioBusSettingsServer.has_bus(&"Voice"), "has_bus for an unknown bus")
	expect(AudioBusSettingsServer.is_player_bus(&"Music"), "is_player_bus: every bus with an empty player_buses")

	AudioBusSettingsServer.set_bus_volume_db(&"Music", -12.0)
	expect_equal(_bus_volume_db(&"Music"), -12.0, "set_bus_volume_db reaches AudioServer at once")
	expect_equal(AudioBusSettingsServer.get_bus_volume_db(&"Music"), -12.0, "get_bus_volume_db")
	AudioBusSettingsServer.set_bus_volume_linear(&"Sfx", 0.5)
	expect_equal(_bus_volume_db(&"Sfx"), -6.0, "linear 0.5 is stored as -6.0 dB, not -6.0206")
	expect(absf(AudioBusSettingsServer.get_bus_volume_linear(&"Sfx") - 0.5) < 0.01, "get_bus_volume_linear (50 %)")
	AudioBusSettingsServer.set_bus_volume_linear(&"Sfx", 1.0)
	expect_equal(str(_bus_volume_db(&"Sfx")), "0.0", "linear 1.0 is 0.0 dB, not -0.0")
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	expect(_bus_mute(&"Music"), "set_bus_mute reaches AudioServer")
	expect(AudioBusSettingsServer.is_bus_mute(&"Music"), "is_bus_mute")
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	expect(not _bus_solo(&"Sfx"), "set_bus_solo does nothing while solo is not stored (a warning is printed)")
	AudioBusSettingsServer.store_solo = true
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	expect(_bus_solo(&"Sfx"), "set_bus_solo reaches AudioServer with store_solo")
	expect(AudioBusSettingsServer.is_bus_solo(&"Sfx"), "is_bus_solo")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", 100.0)
	expect_equal(AudioBusSettingsServer.get_bus_volume_db(&"Music"), MAX_DB, "the volume is clamped to 24 dB")

	AudioBusSettingsServer.reset_to_defaults()
	expect_equal(_bus_volume_db(&"Music"), LAYOUT_MUSIC_DB, "reset_to_defaults gives back the layout volume")
	expect(not _bus_mute(&"Music"), "reset_to_defaults unmutes")
	expect(not _bus_solo(&"Sfx"), "reset_to_defaults clears solo")
	AudioBusSettingsServer.store_solo = false


func _test_manual_mode() -> void:
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -30.0)
	expect_equal(_bus_volume_db(&"Music"), LAYOUT_MUSIC_DB, "manual mode: not applied yet")
	expect(AudioBusSettingsServer.is_dirty(), "manual mode: dirty")
	AudioBusSettingsServer.revert()
	expect_equal(AudioBusSettingsServer.get_bus_volume_db(&"Music"), LAYOUT_MUSIC_DB, "revert gives back the applied volume")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -30.0)
	expect_equal(AudioBusSettingsServer.apply(), OK, "apply in manual mode")
	expect_equal(_bus_volume_db(&"Music"), -30.0, "apply reaches AudioServer")
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	AudioBusSettingsServer.reset_to_defaults()


func _test_signals() -> void:
	var on_volume := func(bus_name: StringName, volume_db: float) -> void: _volumes.append([bus_name, volume_db])
	var on_mute := func(bus_name: StringName, muted: bool) -> void: _mutes.append([bus_name, muted])
	var on_solo := func(bus_name: StringName, solo: bool) -> void: _solos.append([bus_name, solo])
	var on_layout := func() -> void: _layout_changes += 1
	AudioBusSettingsServer.bus_volume_changed.connect(on_volume)
	AudioBusSettingsServer.bus_mute_changed.connect(on_mute)
	AudioBusSettingsServer.bus_solo_changed.connect(on_solo)
	AudioBusSettingsServer.bus_layout_changed.connect(on_layout)
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	AudioBusSettingsServer.set_bus_volume_db(&"Master", -12.0)
	expect(_volumes.is_empty(), "bus_volume_changed does not fire for a pending change in manual mode")
	AudioBusSettingsServer.apply()
	expect_equal(_volumes, [[&"Master", -12.0]], "bus_volume_changed fires once on apply with the bus and the volume")
	expect(_mutes.is_empty(), "bus_mute_changed does not fire for an unchanged mute state")
	AudioBusSettingsServer.set_bus_mute(&"Master", true)
	AudioBusSettingsServer.apply()
	expect_equal(_mutes, [[&"Master", true]], "bus_mute_changed fires once on apply")
	expect_equal(_volumes.size(), 1, "bus_volume_changed does not fire again")
	AudioBusSettingsServer.store_solo = true
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	AudioBusSettingsServer.apply()
	expect_equal(_solos, [[&"Sfx", true]], "bus_solo_changed fires once on apply")
	AudioBusSettingsServer.store_solo = false
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	AudioBusSettingsServer.reset_to_defaults()
	expect_equal(_volumes.size(), 2, "bus_volume_changed fires again when the default is applied")
	expect_equal(_mutes.size(), 2, "bus_mute_changed fires again when the default is applied")
	var layout: AudioBusLayout = AudioServer.generate_bus_layout()
	AudioServer.set_bus_layout(layout)
	expect(_layout_changes >= 1, "bus_layout_changed fires after AudioServer got a new bus layout")
	AudioBusSettingsServer.bus_volume_changed.disconnect(on_volume)
	AudioBusSettingsServer.bus_mute_changed.disconnect(on_mute)
	AudioBusSettingsServer.bus_solo_changed.disconnect(on_solo)
	AudioBusSettingsServer.bus_layout_changed.disconnect(on_layout)
	await get_tree().process_frame


func _test_csv() -> void:
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_CSV)
	expect_equal(AudioBusSettingsServer.file_path, "user://settings/audio_bus.csv", "the bus list can be stored as CSV")
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_INI)
	var path: String = DIR.path_join("buses.csv")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -20.0)
	AudioBusSettingsServer.set_bus_mute(&"Sfx", true)
	AudioBusSettingsServer.file_path = path
	expect_equal(AudioBusSettingsServer.save(), OK, "save as CSV")
	var lines: PackedStringArray = FileAccess.get_file_as_string(path).strip_edges().split("\n")
	expect_equal(lines.size(), 4, "CSV: a header and one row per bus")
	expect(lines[0].contains("volume_db") and lines[0].contains("mute"), "CSV: the columns are the stored values")
	expect(not lines[0].contains("solo"), "CSV: no solo column while solo is not stored")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -3.0)
	AudioBusSettingsServer.set_bus_mute(&"Sfx", false)
	expect_equal(AudioBusSettingsServer.load(), OK, "load reads the CSV")
	expect_equal(AudioBusSettingsServer.get_bus_volume_db(&"Music"), -20.0, "CSV: volume loaded")
	expect(AudioBusSettingsServer.is_bus_mute(&"Sfx"), "CSV: mute loaded")
	AudioBusSettingsServer.apply()
	expect_equal(_bus_volume_db(&"Music"), -20.0, "CSV: loaded volume applied")
	AudioBusSettingsServer.file_path = ""
	AudioBusSettingsServer.reset_to_defaults()


func _test_missing_bus() -> void:
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	var voice := AudioBusSettings.new()
	voice.bus_name = "Voice"
	voice.volume_db = -9.0
	AudioBusSettingsServer.add_entry(voice)
	AudioBusSettingsServer.apply()
	expect_equal(AudioServer.get_bus_index(&"Voice"), -1, "a bus missing in the layout is skipped (a warning is printed)")
	var index: int = AudioBusSettingsServer.find_entry("Voice")
	expect(index >= 0, "the entry of a missing bus is kept")
	expect(AudioBusSettingsServer.has_bus(&"Voice"), "has_bus is true for an entry of a missing bus")
	expect_equal(AudioServer.get_bus_count(), 3, "no bus is created: the layout builds the buses")
	AudioBusSettingsServer.remove_entry(index)
	AudioBusSettingsServer.apply()
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE


func _test_stored_values() -> void:
	var path: String = DIR.path_join("stored_values.ini")
	AudioBusSettingsServer.file_path = path
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -14.0)
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	expect_equal(AudioBusSettingsServer.save(), OK, "save with the default store_* properties")
	var text: String = FileAccess.get_file_as_string(path)
	expect(text.contains("volume_db") and text.contains("mute"), "the file holds volume and mute")
	expect(not text.contains("solo"), "the file holds no solo")
	var csv_path: String = DIR.path_join("hand_edited.csv")
	var file := FileAccess.open(csv_path, FileAccess.WRITE)
	file.store_string("bus_name,volume_db,solo\nMusic,-17,true\n")
	file.close()
	AudioBusSettingsServer.file_path = csv_path
	expect_equal(AudioBusSettingsServer.load(), OK, "a file with an unstored value loads")
	expect_equal(AudioBusSettingsServer.get_bus_volume_db(&"Music"), -17.0, "the stored volume is read")
	expect(not AudioBusSettingsServer.is_bus_solo(&"Music"), "the solo of the file is ignored")
	expect(not AudioBusSettingsServer.is_bus_mute(&"Music"), "the mute missing in the file comes from the layout")
	expect_equal(AudioBusSettingsServer.get_bus_names(), PackedStringArray(["Master", "Music", "Sfx"]), "buses missing in the file come from the layout")
	AudioBusSettingsServer.apply()
	AudioBusSettingsServer.file_path = ""
	AudioBusSettingsServer.reset_to_defaults()


func _test_layout_change() -> void:
	var game_layout: AudioBusLayout = AudioServer.generate_bus_layout()
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -21.0)
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	AudioBusSettingsServer.store_solo = true
	AudioBusSettingsServer.set_bus_solo(&"Sfx", true)
	AudioServer.set_bus_layout(game_layout)
	expect_equal(_bus_volume_db(&"Music"), -21.0, "set_bus_layout keeps the stored volume")
	expect(_bus_mute(&"Music"), "set_bus_layout keeps the stored mute")
	expect(_bus_solo(&"Sfx"), "set_bus_layout keeps the stored solo")
	expect(not AudioBusSettingsServer.is_dirty(), "the re-apply changes no setting")

	AudioBusSettingsServer.player_buses = PackedStringArray(["Music"])
	expect(AudioBusSettingsServer.is_player_bus(&"Music"), "is_player_bus: Music is listed")
	expect(not AudioBusSettingsServer.is_player_bus(&"Sfx"), "is_player_bus: Sfx is not listed")
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index(&"Sfx"), -9.0)
	AudioServer.set_bus_solo(AudioServer.get_bus_index(&"Sfx"), false)
	var quiet_layout: AudioBusLayout = AudioServer.generate_bus_layout()
	AudioServer.set_bus_layout(quiet_layout)
	expect_equal(_bus_volume_db(&"Music"), -21.0, "the player bus gets the player value after set_bus_layout")
	expect_equal(_bus_volume_db(&"Sfx"), -9.0, "a bus that is not a player bus keeps the value of the new layout")
	expect(not _bus_solo(&"Sfx"), "the solo of a non-player bus is not pushed again")
	AudioBusSettingsServer.set_bus_volume_db(&"Sfx", -1.0)
	expect_equal(_bus_volume_db(&"Sfx"), -9.0, "set_bus_volume_db on a non-player bus does nothing (a warning is printed)")
	AudioBusSettingsServer.player_buses = PackedStringArray()
	AudioServer.set_bus_layout(game_layout)

	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -40.0)
	AudioServer.set_bus_layout(game_layout)
	expect_equal(_bus_volume_db(&"Music"), -21.0, "manual mode: the applied volume comes back, the pending one waits")
	AudioBusSettingsServer.revert()
	AudioBusSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	AudioBusSettingsServer.reset_to_defaults()
	expect_equal(_bus_volume_db(&"Music"), LAYOUT_MUSIC_DB, "reset_to_defaults after the layout change")
	expect(not _bus_solo(&"Sfx"), "reset_to_defaults clears the stored solo")
	AudioBusSettingsServer.store_solo = false


func _test_presets() -> void:
	var preset: AudioBusListSettings = load(NIGHT_PRESET)
	expect_equal(preset.buses.size(), 1, "the preset resource loads")
	AudioBusSettingsServer.set_bus_volume_db(&"Sfx", -5.0)
	expect(AudioBusSettingsServer.is_custom(), "no preset applied: custom")
	expect(AudioBusSettingsServer.get_current_preset() == null, "no current preset before apply_preset")
	expect_equal(AudioBusSettingsServer.apply_preset(preset), OK, "apply_preset")
	expect_equal(_bus_volume_db(&"Music"), NIGHT_MUSIC_DB, "the preset volume reaches AudioServer")
	expect(_bus_mute(&"Music"), "the preset mute reaches AudioServer")
	expect_equal(_bus_volume_db(&"Sfx"), -5.0, "a bus that the preset does not list keeps its value")
	expect_equal(AudioBusSettingsServer.get_current_preset(), preset, "get_current_preset")
	expect(not AudioBusSettingsServer.is_custom(), "not custom right after apply_preset")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", -3.0)
	expect(AudioBusSettingsServer.is_custom(), "a single change makes it custom")
	expect(not AudioBusSettingsServer.matches_preset(preset), "matches_preset is false after the change")
	AudioBusSettingsServer.set_bus_volume_db(&"Music", NIGHT_MUSIC_DB)
	expect(AudioBusSettingsServer.matches_preset(preset), "matches_preset is true again")
	AudioBusSettingsServer.reset_to_defaults()


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	var volume: float = -randi_range(10, 30)
	AudioBusSettingsServer.set_bus_volume_db(&"Music", volume)
	AudioBusSettingsServer.set_bus_mute(&"Music", true)
	expect_equal(AudioBusSettingsServer.save(), OK, "save writes the player file")
	var probe: Dictionary = _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	expect_equal(probe.get("music_volume_db", 1000.0), volume, "restart: the saved Music volume was applied before the main scene")
	expect(probe.get("music_mute", false), "restart: the saved mute was applied")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: applied before the first frame, so before any sound")


func _run_probe() -> Dictionary:
	var output: Array = []
	var args: PackedStringArray = ["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line: String in str(chunk).split("\n"):
			if line.begins_with("PROBE "):
				var parsed: Variant = JSON.parse_string(line.trim_prefix("PROBE "))
				return parsed
	expect(false, "restart: the probe printed its result")
	return {}


func _remove_dir(path: String) -> void:
	for file_name: String in DirAccess.get_files_at(path):
		DirAccess.remove_absolute(path.path_join(file_name))
	DirAccess.remove_absolute(path)
