extends FeatureCheck
## GDScript checks of 189-refactoring-settings-float-precision. Check the same things as scripts/cs/Issue189RefactoringSettingsFloatPrecisionCheck.cs.
## Settings float values have at most three decimals: in getters, signals and files.

const TEMP_FOLDER := "user://checks/189-refactoring-settings-float-precision"
# Text of a float with the noise of a 32-bit value or of a conversion.
const NOISE := ["0000000", "9999999", "e-0", "e-1"]


func _get_check_name() -> String:
	return "189-refactoring-settings-float-precision"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(TEMP_FOLDER)
	_check_snap_float()
	_check_values()
	_check_linear_volume()
	_check_files()


func _check_snap_float() -> void:
	expect_equal(SettingsServer.snap_float(0.1 + 0.2), 0.3, "snap_float(0.1 + 0.2) is 0.3")
	expect_equal(SettingsServer.snap_float(1.23456), 1.235, "three decimals by default")
	expect_equal(SettingsServer.snap_float(-6.0206, 0.1), -6.0, "snapped to the step")
	expect_equal(SettingsServer.snap_float(0.12, 0.1, 0.05), 0.15, "snapped relative to the offset")
	expect_equal(SettingsServer.snap_float(0.0123, 0.0001), 0.012, "a step below 0.001 counts as 0.001")
	expect_equal(str(SettingsServer.snap_float(-0.0000014)), "0.0", "-0.0 becomes 0.0")


func _check_values() -> void:
	InputSettingsServer.reset_to_defaults()
	var deadzone: float = InputSettingsServer.get_value(&"gamepad_deadzone")
	expect_equal(deadzone, 0.2, "the default deadzone (a 32-bit member) is 0.2, not 0.20000000298023224")
	expect_equal(str(deadzone), "0.2", "the default deadzone prints as 0.2")
	expect_equal(InputSettingsServer.get_default_value(&"gamepad_deadzone"), 0.2, "get_default_value() is 0.2")

	var changes: Array = []
	var on_changed := func(setting_name: StringName, value: Variant) -> void: changes.append([setting_name, value])
	InputSettingsServer.value_changed.connect(on_changed)
	InputSettingsServer.set_value(&"gamepad_deadzone", 0.123456)
	expect_equal(InputSettingsServer.get_value(&"gamepad_deadzone"), 0.12, "set_value() snaps to the 0.01 step of the range")
	expect_equal(InputSettingsServer.get_applied_value(&"gamepad_deadzone"), 0.12, "the applied value is 0.12")
	expect(changes.size() == 1 and changes[0][1] == 0.12, "value_changed carries 0.12")
	InputSettingsServer.set_value(&"gamepad_deadzone", 0.12)
	expect_equal(changes.size(), 1, "the same value again is no change")
	InputSettingsServer.value_changed.disconnect(on_changed)

	InputSettingsServer.set_value(&"mouse_sensitivity", 1.23456)
	expect_equal(InputSettingsServer.get_value(&"mouse_sensitivity"), 1.23, "mouse_sensitivity snaps to 0.01")
	InputSettingsServer.reset_to_defaults()


func _check_linear_volume() -> void:
	if not expect(AudioBusSettingsServer.has_bus(&"Sfx"), "the project has an Sfx bus"):
		return
	AudioBusSettingsServer.set_bus_volume_linear(&"Sfx", 0.5)
	expect_equal(AudioBusSettingsServer.get_bus_volume_db(&"Sfx"), -6.0, "linear 0.5 is stored as -6.0 dB, not -6.0206")
	var index := AudioBusSettingsServer.find_entry("Sfx")
	if expect(index >= 0, "Sfx has an entry"):
		expect_equal(AudioBusSettingsServer.get_entry_value(index, &"volume_db"), -6.0, "get_entry_value() is -6.0")
	AudioBusSettingsServer.set_bus_volume_linear(&"Sfx", 1.0)
	expect_equal(str(AudioBusSettingsServer.get_bus_volume_db(&"Sfx")), "0.0", "linear 1.0 is 0.0 dB, not -0.0 or -0.000001")
	AudioBusSettingsServer.reset_to_defaults()


func _check_files() -> void:
	var input_path := InputSettingsServer.get_file_path()
	for extension in ["json", "ini"]:
		var path := TEMP_FOLDER.path_join("input." + extension)
		InputSettingsServer.set_file_path(path)
		InputSettingsServer.set_value(&"gamepad_deadzone", 0.3)
		InputSettingsServer.set_value(&"mouse_sensitivity", 1.7)
		InputSettingsServer.apply()
		expect_equal(InputSettingsServer.save(), OK, "%s: save()" % extension)
		var text := FileAccess.get_file_as_string(path)
		expect(text.contains("0.3") and text.contains("1.7"), "%s: the file holds 0.3 and 1.7" % extension)
		_expect_no_noise(text, extension)
		InputSettingsServer.reset_to_defaults()
		expect_equal(InputSettingsServer.load(), OK, "%s: load()" % extension)
		expect_equal(InputSettingsServer.get_value(&"gamepad_deadzone"), 0.3, "%s: loaded deadzone is 0.3" % extension)
	InputSettingsServer.set_file_path(input_path)
	InputSettingsServer.reset_to_defaults()

	if not AudioBusSettingsServer.has_bus(&"Sfx"):
		return
	var audio_path := AudioBusSettingsServer.get_file_path()
	var csv_path := TEMP_FOLDER.path_join("audio_bus.csv")
	AudioBusSettingsServer.set_file_path(csv_path)
	AudioBusSettingsServer.set_bus_volume_db(&"Sfx", -12.3)
	AudioBusSettingsServer.apply()
	expect_equal(AudioBusSettingsServer.save(), OK, "csv: save()")
	var csv := FileAccess.get_file_as_string(csv_path)
	expect(csv.contains("-12.3"), "csv: the file holds -12.3")
	_expect_no_noise(csv, "csv")
	AudioBusSettingsServer.set_file_path(audio_path)
	AudioBusSettingsServer.reset_to_defaults()


func _expect_no_noise(text: String, format: String) -> void:
	for noise: String in NOISE:
		expect(not text.contains(noise), "%s: no float noise (\"%s\")" % [format, noise])
