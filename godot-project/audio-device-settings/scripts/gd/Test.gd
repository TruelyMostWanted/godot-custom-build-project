extends FeatureTest
## Full API test of Audio Device Settings: AudioDeviceSettingsServer and AudioDeviceSettings. Same tests as scripts/cs/Test.cs.

const DIR := "user://audio-device-settings-test-gd"
const PROBE_SCENE := "res://audio-device-settings/scenes/restart_probe.tscn"
const DEVICES_FILE := "user://settings/audio_device.ini"
const DEFAULT_DEVICE := "Default"
const UNKNOWN_DEVICE := "Test device that does not exist"

var _outputs: Array[String] = []


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_test_registration()
	_test_resource()
	_test_devices()
	_test_file()
	_test_manual_mode()
	_test_signals()
	_test_restart()
	AudioDeviceSettingsServer.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(DEVICES_FILE)
	_remove_dir(DIR)


func _test_registration() -> void:
	expect(Engine.has_singleton("AudioDeviceSettingsServer"), "AudioDeviceSettingsServer is a registered singleton")
	expect(Engine.get_singleton("AudioDeviceSettingsServer") is SettingsServer, "AudioDeviceSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"audio_device"), AudioDeviceSettingsServer, "the domain is registered as audio_device")
	expect(AudioDeviceSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(AudioDeviceSettingsServer.file_path, DEVICES_FILE, "device file path")
	expect_equal(AudioDeviceSettingsServer.get_setting_names(), PackedStringArray(["output_device", "input_device"]), "setting names")
	expect_equal(AudioDeviceSettingsServer.is_value_supported(&"output_device"), AudioDeviceSettingsServer.is_device_selection_supported(), "is_value_supported follows the driver")


func _test_resource() -> void:
	var settings := AudioDeviceSettings.new()
	expect_equal(settings.output_device, DEFAULT_DEVICE, "AudioDeviceSettings.output_device defaults to Default")
	expect_equal(settings.input_device, DEFAULT_DEVICE, "AudioDeviceSettings.input_device defaults to Default")
	settings.output_device = "Speakers"
	settings.input_device = "Microphone"
	expect_equal(settings.output_device, "Speakers", "AudioDeviceSettings.output_device can be set")
	expect_equal(settings.input_device, "Microphone", "AudioDeviceSettings.input_device can be set")
	var sample: AudioDeviceSettings = load("res://audio-device-settings/resources/devices.tres")
	expect_equal(sample.output_device, DEFAULT_DEVICE, "the sample AudioDeviceSettings loads")


func _test_devices() -> void:
	AudioDeviceSettingsServer.set_output_device(UNKNOWN_DEVICE)
	expect_equal(AudioDeviceSettingsServer.get_output_device(), DEFAULT_DEVICE, "an unknown output device falls back to Default")
	expect_equal(AudioServer.output_device, DEFAULT_DEVICE, "AudioServer keeps Default")
	AudioDeviceSettingsServer.set_input_device(UNKNOWN_DEVICE)
	expect_equal(AudioDeviceSettingsServer.get_input_device(), DEFAULT_DEVICE, "an unknown input device falls back to Default")
	AudioDeviceSettingsServer.set_input_device("")
	expect_equal(AudioDeviceSettingsServer.get_input_device(), DEFAULT_DEVICE, "an empty input device is Default")
	var devices: PackedStringArray = AudioServer.get_output_device_list()
	if AudioDeviceSettingsServer.is_device_selection_supported() and devices.size() > 1:
		AudioDeviceSettingsServer.set_output_device(devices[1])
		expect_equal(AudioDeviceSettingsServer.get_output_device(), devices[1], "a listed output device is kept")
		AudioDeviceSettingsServer.set_output_device(DEFAULT_DEVICE)
	else:
		print("  SKIP  device switch (the driver has no device list or only one device)")


func _test_file() -> void:
	var path: String = DIR.path_join("devices.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(JSON.stringify({"output_device": "Speakers (unplugged)", "input_device": "Microphone (unplugged)"}))
	file.close()
	AudioDeviceSettingsServer.file_path = path
	expect_equal(AudioDeviceSettingsServer.load(), OK, "a file with devices of another machine loads without an error")
	expect_equal(AudioDeviceSettingsServer.get_output_device(), DEFAULT_DEVICE, "a stored output device that no longer exists becomes Default")
	expect_equal(AudioDeviceSettingsServer.get_input_device(), DEFAULT_DEVICE, "a stored input device that no longer exists becomes Default")
	expect_equal(AudioDeviceSettingsServer.save(), OK, "save writes the file")
	expect(FileAccess.file_exists(path), "the file exists")
	AudioDeviceSettingsServer.file_path = ""
	AudioDeviceSettingsServer.apply()


func _test_manual_mode() -> void:
	AudioDeviceSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	AudioDeviceSettingsServer.set_input_device(UNKNOWN_DEVICE)
	expect(not AudioDeviceSettingsServer.is_dirty(), "a device that falls back to Default is no change")
	expect_equal(AudioDeviceSettingsServer.apply(), OK, "apply in manual mode")
	AudioDeviceSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	AudioDeviceSettingsServer.reset_to_defaults()
	expect_equal(AudioDeviceSettingsServer.get_output_device(), DEFAULT_DEVICE, "reset_to_defaults gives back Default")


func _test_signals() -> void:
	expect(AudioDeviceSettingsServer.has_signal(&"output_device_changed"), "AudioDeviceSettingsServer has output_device_changed")
	expect(AudioDeviceSettingsServer.has_signal(&"input_device_changed"), "AudioDeviceSettingsServer has input_device_changed")
	var on_output := func(device: String) -> void: _outputs.append(device)
	AudioDeviceSettingsServer.output_device_changed.connect(on_output)
	AudioDeviceSettingsServer.set_output_device(UNKNOWN_DEVICE)
	expect(_outputs.is_empty(), "output_device_changed does not fire when the device stays Default")
	var devices: PackedStringArray = AudioServer.get_output_device_list()
	if AudioDeviceSettingsServer.is_device_selection_supported() and devices.size() > 1:
		AudioDeviceSettingsServer.set_output_device(devices[1])
		expect_equal(_outputs, [devices[1]], "output_device_changed fires once with the device")
		AudioDeviceSettingsServer.set_output_device(DEFAULT_DEVICE)
	else:
		print("  SKIP  output_device_changed with a real device (the driver has no device list or only one device)")
	AudioDeviceSettingsServer.output_device_changed.disconnect(on_output)


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	expect_equal(AudioDeviceSettingsServer.save(), OK, "save writes the player file")
	var probe: Dictionary = _run_probe()
	expect(probe.get("started", false), "restart: the domain was started")
	expect_equal(probe.get("output_device", ""), DEFAULT_DEVICE, "restart: the output device is Default")
	expect_equal(probe.get("input_device", ""), DEFAULT_DEVICE, "restart: the input device is Default")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: applied before the first frame")


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
